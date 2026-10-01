"""Direction "creature": one playable fauna out of the two round-1 winners.

BODY  = evo's learned rule (evo_model.EvoRule: the G2 network over results/swarm_coevo_g2/rule.pt plus
        the evolved lay homeostat + egg choice genome). It owns growth, look and composition, and
        with no vessel near it is BIT-FOR-BIT the evo model (every layer below is gated on a ship, a
        wound or a majority change), so the 16-transition yardstick is inherited, not re-earned.
SHELL = a player-facing reaction layer in the spirit of field_swarm's predator response, but applied
        as an ELASTIC OFFSET on top of the learned motion instead of replacing it:

  pos_shown = pos_learned + off,  off <- off * (1 - k_ret * calm) + reaction

  so a startled tadpole is pushed off its learned place, and once the ship is gone the offset springs
  back to zero and the learned body is exactly where the rule would have it. The learned rule keeps
  perceiving the displaced positions (it is not frozen), which is what lets a carved body re-knit.

Temperaments (per ELEMENT of the tadpole, plus a plan-level response keyed on the swarm's majority):
  Mass   ponderous: barely flees (0.3); the WHALE plan hunkers - its minority elements tuck into the
         body and the Mass hull closes toward the ship's side (shielding).
  Space  drifter: flees in pulsed JETS (an impulse every `jet_period` steps); the JELLYFISH plan's bell
         contracts on each jet and the whole body jets away from the ship (translation is free).
  Charge holds (0.5) and SPIKES (a per-tadpole danger flag, the game's danger prism); the PUFFERFISH
         plan inflates (radial swell 1 + inflate * threat) and deflates when the ship is gone.
  Time   darts (2.0, evasive zig-zag); a LOITERING ship (slower than mob_speed) is MOBBED: Time units
         orbit it at 1.4 ship radii and stream home when it leaves.
Startle relays neighbour-to-neighbour (a school's startle wave) and decays; the school parts ahead of
the ship's PATH (look-ahead), not only around its current position.

Two more player-facing behaviours, both production-side (nothing is culled, nothing dies on a clock):
  WOUND MEMORY  when tadpoles vanish between steps (eaten, rammed - not their own death), their last
                places (relative to the centroid) are remembered; an egg laid near a remembered wound is
                placed INTO it, so a carved hole fills from its edges instead of the body regrowing
                elsewhere. Wounds are consumed by eggs and forgotten after `wound_life` steps.
  SWITCH TELL   when the living majority element changes, the body SHIVERS for `tell_steps` (a decaying
                jitter + a slow swirl about the vertical) - a readable "something is about to change"
                before the learned rule re-forms it into the new plan.

Per-step visual flags for the renderer live on the model: `self.flags` (startle, danger, tell, mob).
"""
import math
import os
import sys

import numpy as np
import torch
import torch.nn.functional as F

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import evo_model as em  # noqa: E402

GENOME = os.path.join(HERE, "results", "evo", "genome.npy")


DEFAULTS = dict(
    # perception of a ship
    sense=2.4,            # startle within sense x ship radius (now, or ahead on its path)
    lookahead=10.0,       # steps of path look-ahead
    relay=0.75,           # startle passed to neighbours within relay_r
    relay_r=4.5,
    decay=0.9,            # startle decay per step
    # flee per element C M S T
    flee=(0.5, 0.3, 1.2, 2.0),
    flee_swirl=0.6,
    k_ret=0.10,           # elastic return of the offset per calm step
    max_off=30.0,         # offset clamp (voxels)
    # temperaments
    jet_period=6,         # Space: jets on 2 of every jet_period steps
    jet_gain=2.2,
    bell=0.35,            # jellyfish plan: bell contraction per jet
    body_jet=0.6,         # jellyfish plan: whole-body escape per jet (voxels/step)
    inflate=0.55,         # pufferfish plan: radial swell at full threat
    danger_at=0.35,       # Charge startle above this shows DANGER spikes
    tuck=0.35,            # whale plan: minority elements pulled toward the centroid at full threat
    shield=0.25,          # whale plan: Mass hull pushed toward the ship's side
    dart=0.8,             # Time: evasive zig-zag amplitude
    mob=(0.0, 0.0, 0.0, 1.0),
    mob_speed=1.0,
    mob_r=1.4,
    # wound memory
    wounds=1,
    wound_reach=12.0,     # an egg within this of a wound is placed into it
    wound_life=160,
    anchor=0,             # 1: wounds anchored to their nearest survivor (else to the centroid)
    # switch tell
    tell=1,
    tell_steps=36,
    tell_jitter=0.7,
    tell_swirl=0.25,
)


class CreatureRule(em.EvoRule):
    stateless = False

    def __init__(self, genome=None, **cfg):
        super().__init__(np.load(GENOME) if genome is None else genome)
        self.cfg = dict(DEFAULTS)
        self.cfg.update(cfg)
        self.vessels = []          # [(centre np[3], radius, velocity np[3])], set by the driver each step
        self.react = True          # False: ignore ships (no startle / flee / plan responses)
        self.shell = True          # False: the whole shell is off - the bare evo model (inert baseline)
        self._st = None
        self._rng = torch.Generator().manual_seed(1234)

    # ----------------------------------------------------------------- state
    def _reset(self, sw):
        B, N = sw.pos.shape[:2]
        if self._st is None or self._st["off"].shape[:2] != (B, N):
            self._st = dict(off=torch.zeros(B, N, 3), startle=torch.zeros(B, N), threat=torch.zeros(B),
                            alive=torch.zeros(B, N, dtype=torch.bool), last=torch.zeros(B, N, 3),
                            maj=torch.full((B,), -1, dtype=torch.long), tell=torch.zeros(B),
                            wounds=[[] for _ in range(B)], swell=torch.zeros(B), t=torch.zeros(B, dtype=torch.long))
        fresh = (sw.clock == 0).nonzero().squeeze(1).tolist()
        for b in fresh:
            st = self._st
            st["off"][b] = 0; st["startle"][b] = 0; st["threat"][b] = 0; st["alive"][b] = False
            st["maj"][b] = -1; st["tell"][b] = 0; st["wounds"][b] = []; st["swell"][b] = 0; st["t"][b] = 0

    def forward(self, sw, gen=None, bud=True, fire=None):
        if not self.shell:
            return super().forward(sw, gen, bud, fire)
        with torch.no_grad():
            self._reset(sw)
            st = self._st
            c = self.cfg
            alive_in = sw.active & sw.hatched
            # --- wounds: alive at the end of my last step, gone now = removed from outside
            if c["wounds"]:
                gone = st["alive"] & ~sw.active
                for b in gone.any(1).nonzero().squeeze(1).tolist():
                    keep = alive_in[b]
                    if int(keep.sum()) < 4:
                        continue
                    cen = sw.pos[b][keep].mean(0)
                    kidx = keep.nonzero().squeeze(1)
                    for i in gone[b].nonzero().squeeze(1).tolist():
                        # anchor the wound to the nearest SURVIVOR (robust to the body drifting / pulsing);
                        # the centroid-relative place is the fallback if that survivor is gone too
                        dd = (sw.pos[b, kidx] - st["last"][b, i]).norm(dim=-1)
                        a = int(kidx[int(dd.argmin())])
                        st["wounds"][b].append([st["last"][b, i] - cen, int(sw.elem[b, i]), int(st["t"][b]),
                                                a, st["last"][b, i] - sw.pos[b, a]])
                for b in range(sw.B):
                    if st["wounds"][b]:
                        st["wounds"][b] = [w for w in st["wounds"][b] if int(st["t"][b]) - w[2] < c["wound_life"]]
            st["off"][~sw.active] = 0
            st["startle"][~sw.active] = 0
            out = super().forward(sw, gen, bud, fire)
            self._shell(out, gen)
            st["alive"] = (out.active & out.hatched).clone()
            st["last"] = out.pos.clone()
            st["t"] += 1
            return out

    # ------------------------------------------------------------- laying hook
    def lay(self, sw, gi, gj, gen=None):
        before = sw.active.clone()
        super().lay(sw, gi, gj, gen)
        if not self.shell or not self.cfg["wounds"] or self._st is None:
            return
        new = sw.active & ~before
        for b in new.any(1).nonzero().squeeze(1).tolist():
            ws = self._st["wounds"][b]
            if not ws:
                continue
            al = sw.active[b] & sw.hatched[b]
            if int(al.sum()) < 4:
                continue
            cen = sw.pos[b][al].mean(0)
            if self.cfg.get("anchor", 1):
                wp = torch.stack([sw.pos[b, w[3]] + w[4] if bool(sw.active[b, w[3]]) else w[0] + cen for w in ws])
            else:
                wp = torch.stack([w[0] for w in ws]) + cen       # [W,3] absolute wound sites
            used = torch.zeros(len(ws), dtype=torch.bool)
            for i in new[b].nonzero().squeeze(1).tolist():
                d = (wp - sw.pos[b, i]).norm(dim=-1)
                d[used] = 1e9
                j = int(d.argmin())
                if float(d[j]) < self.cfg["wound_reach"]:
                    sw.pos[b, i] = wp[j]
                    used[j] = True
            self._st["wounds"][b] = [w for w, u in zip(ws, used.tolist()) if not u]

    # ------------------------------------------------------------- the shell
    def _shell(self, sw, gen):
        st, c = self._st, self.cfg
        B, N, _ = sw.pos.shape
        al = sw.active & sw.hatched
        alf = al.float()
        cnt = alf.sum(1).clamp(min=1)
        cen = (sw.pos * alf[..., None]).sum(1) / cnt[:, None]                     # [B,3]
        rel = sw.pos - cen[:, None]
        counts = (alf[..., None] * F.one_hot(sw.elem, 4).float()).sum(1)          # [B,4]
        maj = counts.argmax(1)
        flags = dict(startle=torch.zeros(B, N), danger=torch.zeros(B, N, dtype=torch.bool),
                     tell=torch.zeros(B), mob=torch.zeros(B, N, dtype=torch.bool))
        react = torch.zeros(B, N, 3)
        startle = st["startle"] * c["decay"]
        any_ship = bool(self.vessels) and self.react
        tvec = st["t"].float()
        if any_ship:
            el = sw.elem
            fk = torch.tensor(c["flee"])[el]                                      # [B,N]
            for (pc, rad, pv) in self.vessels:
                pc = torch.as_tensor(pc, dtype=torch.float32); pv = torch.as_tensor(pv, dtype=torch.float32)
                rel_s = sw.pos - pc
                dd = rel_s.norm(dim=-1).clamp(min=1e-3)
                sense = c["sense"] * rad
                spd = float(pv.norm())
                pvn = pv / max(spd, 1e-6)
                along = rel_s @ pvn
                lat = rel_s - along[..., None] * pvn
                dl = lat.norm(dim=-1).clamp(min=1e-3)
                latn = lat / dl[..., None]
                ahead = (1 - along / (c["lookahead"] * spd + rad)).clamp(0, 1) * (along > -rad).float()
                w = torch.maximum((1 - dl / sense).clamp(0, 1) * ahead, (1 - dd / sense).clamp(0, 1)) * alf
                startle = torch.maximum(startle, (1.4 * w).clamp(0, 1))
                radial = rel_s / dd[..., None]
                if spd < 1e-3:
                    latn = radial
                swirl = torch.cross(pvn.expand_as(latn), latn, dim=-1)
                gain = fk.clone()
                # Space jets: pulsed
                jet_on = ((tvec % c["jet_period"]) < 2).float()[:, None]
                gain = torch.where(el == 2, gain * (c["jet_gain"] * jet_on + 0.15), gain)
                flee = w[..., None] * gain[..., None] * (0.75 * latn + 0.25 * radial + c["flee_swirl"] * 0.5 * swirl)
                # Time darts: evasive zig-zag across the flee direction
                zig = torch.sin(tvec[:, None] * 1.3 + torch.arange(N)[None].float() * 2.1)
                perp = torch.cross(latn, radial, dim=-1)
                flee = flee + ((el == 3).float() * w * c["dart"] * zig)[..., None] * perp
                if spd < c["mob_speed"]:
                    mk = torch.tensor(c["mob"])[el] * alf
                    w_mob = (1 - dd / (2.5 * sense)).clamp(0, 1) * mk
                    up = torch.tensor([0.0, 1.0, 0.0]).expand_as(radial)
                    tang = torch.cross(up, radial, dim=-1)
                    tang = tang / tang.norm(dim=-1, keepdim=True).clamp(min=1e-3)
                    orbit = 0.4 * (c["mob_r"] * rad - dd)[..., None] * radial + 1.5 * tang
                    react = react + w_mob[..., None] * orbit
                    flee = flee * (1 - mk)[..., None]
                    flags["mob"] |= w_mob > 0.05
                react = react + flee
                # plan-level responses (keyed on the swarm's majority element)
                to_ship = pc - cen
                tsn = to_ship / to_ship.norm(dim=-1, keepdim=True).clamp(min=1e-3)
                thr = st["threat"][:, None, None]
                whale = (maj == 1).float()[:, None, None]
                jelly = (maj == 2).float()[:, None, None]
                tuck = -c["tuck"] * 0.1 * rel * (el != 1).float()[..., None]
                proj = (rel * tsn[:, None]).sum(-1).clamp(min=0)                  # how far toward the ship's side
                shield = c["shield"] * 0.05 * tsn[:, None] * ((el == 1).float() * proj)[..., None]
                react = react + whale * thr * (tuck + shield)
                bodyjet = -tsn[:, None] * c["body_jet"] * jet_on[..., None]
                bell = -c["bell"] * 0.1 * rel * jet_on[..., None]
                react = react + jelly * thr * (bodyjet + bell)
        # startle wave
        if bool((startle > 0.02).any()):
            d = torch.cdist(sw.pos, sw.pos)
            nb = (d < c["relay_r"]) & al[:, None, :] & al[:, :, None]
            relay = (nb.float() * startle[:, None, :]).amax(-1) * c["relay"]
            startle = torch.maximum(startle, relay) * alf
        st["startle"] = startle
        sm = (startle * alf).sum(1) / cnt
        st["threat"] = 0.85 * st["threat"] + 0.15 * (3.0 * sm).clamp(max=1.0)
        # pufferfish: radial swell toward target (a shape offset, part of off)
        puff = (maj == 0).float()
        swell_t = c["inflate"] * st["threat"] * puff
        dsw = swell_t - st["swell"]
        st["swell"] = swell_t
        react = react + dsw[:, None, None] * rel * alf[..., None]
        # switch tell
        if c["tell"]:
            changed = (st["maj"] >= 0) & (maj != st["maj"]) & (cnt >= sn.MIN_TEST_BODY)
            st["tell"] = torch.where(changed, torch.full_like(st["tell"], float(c["tell_steps"])), (st["tell"] - 1).clamp(min=0))
            st["maj"] = maj
            if bool((st["tell"] > 0).any()):
                a = (st["tell"] / c["tell_steps"])[:, None, None]
                jit = torch.randn(B, N, 3, generator=self._rng) * c["tell_jitter"]   # own stream: never shifts the rule's
                up = torch.tensor([0.0, 1.0, 0.0]).expand_as(rel)
                tang = torch.cross(up, rel, dim=-1)
                react = react + a * (jit + c["tell_swirl"] * 0.1 * tang) * alf[..., None]
                flags["tell"] = st["tell"] / c["tell_steps"]
        # elastic offset
        calm = 1 - startle.clamp(0, 1)
        old = st["off"]
        new = old * (1 - c["k_ret"] * calm)[..., None] + react
        nrm = new.norm(dim=-1, keepdim=True)
        new = new * (c["max_off"] / nrm.clamp(min=c["max_off"]))
        new = new * sw.active[..., None].float()
        if any_ship or bool(old.abs().sum() > 0) or bool(react.abs().sum() > 0):
            sw.pos = sw.pos + (new - old)
            rad_ = sw.pos.norm(dim=-1, keepdim=True).clamp(min=1e-6)
            sw.pos = sw.pos - 0.5 * (rad_ - self.world.membrane).clamp(min=0) * sw.pos / rad_
        st["off"] = new
        flags["startle"] = startle
        flags["danger"] = (sw.elem == 0) & (startle > c["danger_at"]) & al
        self.flags = flags
