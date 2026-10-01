"""The zoo: `field` (designed fields + boids, round-1 winner) with switchable BEHAVIOUR GENES, so one
model spans many creatures. Research direction `zoo` (quality-diversity search, see zoo_search.py).

Everything `field` does is kept (slot assignment, element-ratio plan choice with hysteresis, true-breeding
laying, molting, morph vortex, startle cascade, flee / mob / inflate). On top, each a gene that can be off:

  breathe   the whole body pulses (home scale x (1 + amp sin(2 pi t / period)))         rhythm
  jitter    every tadpole carries an Ornstein-Uhlenbeck wobble                            liveliness
  orbit     every tadpole circles its own slot on a private small loop (a shimmering body) liveliness
  burst     a committed switch starts with a radial explosion before the vortex reforms it  drama
  curious   a ship at middle distance (1-3x sense) is APPROACHED; up close the flee rules win
  hunt      all elements mob a ship, and `mob_speed` decides how fast a ship they will chase
  bristle   any startled tadpole flashes DANGER (not just Charge)                          visual
  rush      a sudden headcount drop (a strike) multiplies laying for a while              heal
  inflate   per-plan threat swell (field: only the pufferfish, 0.45)                     defence

Genes live in ZooCfg (a FieldCfg plus the new fields). `wander`/`turn` (swimming) are presentation
genes: the yardstick is orientation-locked, so evaluation always runs them at 0 (exactly as field did).

    python Tools/NCA/zoo_model.py           # smoke test: field-equivalent genome through swarm_eval
"""
from __future__ import annotations

import math
import os
import sys
from dataclasses import dataclass, field, asdict, fields

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402
import field_swarm as fs  # noqa: E402

A, FAC, PR, TI, SP, DIE = sn.A, sn.FAC, sn.PR, sn.TI, sn.SP, sn.DIE
VEL, HOME, MOLT, MOLT_TO, STARTLE, BIRTH = fs.VEL, fs.HOME, fs.MOLT, fs.MOLT_TO, fs.STARTLE, fs.BIRTH
JIT = slice(20, 23)          # OU wobble state
ELEM_KIND = fs.ELEM_KIND
PLAN_IDX = {k: i for i, k in enumerate(sn.KINDS)}


@dataclass
class ZooCfg(fs.FieldCfg):
    vscale: float = 1.0               # multiplies the per-element top speeds
    breathe: float = 0.0              # amplitude of the body pulse (share of size)
    breathe_period: float = 48.0
    jitter: float = 0.0               # OU wobble acceleration
    jitter_tau: float = 8.0
    orbit: float = 0.0                # radius of each tadpole's private loop around its slot
    orbit_w: float = 0.25             # rad/step
    burst: float = 0.0                # radial explosion at the start of a morph
    curious: float = 0.0              # approach a ship at middle distance
    hunt: float = 0.0                 # every element mobs (adds to the per-element mob)
    bristle: int = 0                  # every startled tadpole flashes danger
    rush: float = 0.0                 # laying multiplier after a sudden loss
    inflate4: tuple = (0.0, 0.0, 0.45, 0.0)   # per-plan threat swell (mass, space, charge, time)

    def __post_init__(self):
        self.vmax = tuple(self.vmax)
        self.flee = tuple(self.flee)
        self.mob = tuple(self.mob)
        self.inflate4 = tuple(self.inflate4)
        self.inflate = {k: float(self.inflate4[i]) for i, k in enumerate(sn.KINDS) if self.inflate4[i] > 0}


def cfg_from_dict(d):
    names = {f.name for f in fields(ZooCfg)}
    d = {k: v for k, v in d.items() if k in names}
    for k in ("vmax", "flee", "mob", "inflate4"):
        if k in d:
            d[k] = tuple(d[k])
    d.pop("inflate", None)
    return ZooCfg(**d)


def cfg_to_dict(c):
    d = asdict(c)
    d.pop("inflate", None)
    return d


def _hash01(i, salt):
    """Deterministic per-tadpole pseudo-random numbers in [0, 1) (index-based, no RNG state)."""
    x = np.sin(np.asarray(i, np.float64) * 12.9898 + salt * 78.233) * 43758.5453
    return x - np.floor(x)


class ZooSwarm(fs.FieldSwarm):
    """FieldSwarm with the behaviour genes of ZooCfg. Keeps per-swarm state on itself (stateless=False)."""
    stateless = False

    def __init__(self, cfg: ZooCfg | None = None, world=None):
        super().__init__(cfg or ZooCfg(), world)

    def _reset(self, b, sw):
        super()._reset(b, sw)
        m = self.mem[b]
        m["n_hist"] = None; m["rush"] = 0.0
        sw.s[b, :, JIT] = 0.0

    def _at(self, m, plan):
        sp, sv, ss = super()._at(m, plan)
        c = self.cfg
        if c.breathe > 0:
            w = 2 * math.pi / max(c.breathe_period, 4.0)
            f = 1.0 + c.breathe * math.sin(w * m["t"])
            df = c.breathe * w * math.cos(w * m["t"])
            sv = sv * f + sp * df
            sp = sp * f
        return sp, sv, ss

    def _lay(self, sw, b, plan, perm, gen):
        c = self.cfg
        r = self.mem[b].get("rush", 0.0)
        if c.rush > 0 and r > 0.01:
            lr, lm = c.lay_rate, c.lay_max
            c.lay_rate, c.lay_max = lr * (1 + c.rush * r), int(round(lm * (1 + c.rush * r)))
            try:
                return super()._lay(sw, b, plan, perm, gen)
            finally:
                c.lay_rate, c.lay_max = lr, lm
        return super()._lay(sw, b, plan, perm, gen)

    # ------------------------------------------------------------------ the step (field's, with genes)

    def _step(self, sw, b, gen):
        cfg, m = self.cfg, self.mem[b]
        al = (sw.active[b] & sw.hatched[b]).numpy()
        idx = np.nonzero(al)[0]
        if len(idx) == 0:
            return
        pos = sw.pos[b].numpy()
        S = sw.s[b].numpy()
        elem = sw.elem[b].numpy()
        dom = sw.dom[b].numpy()
        clock = int(sw.clock[b])

        # rush: a sudden loss (strike) primes a laying burst
        n_now = len(idx)
        if m["n_hist"] is not None and n_now < 0.9 * m["n_hist"]:
            m["rush"] = 1.0
        m["rush"] *= 0.97
        m["n_hist"] = n_now

        # --- 1. which plan? element ratios, with time hysteresis
        e_eff = np.where(S[:, MOLT] > 0, S[:, MOLT_TO].astype(int), elem)
        counts = np.bincount(e_eff[idx], minlength=4)
        cur = sn.MAJOR[m["plan"]]
        top = int(np.argmax(counts))
        maj = cur if counts[cur] == counts[top] else top
        if maj != cur:
            if m["cand"] == ELEM_KIND[maj]:
                m["cand_n"] += 1
            else:
                m["cand"], m["cand_n"] = ELEM_KIND[maj], 1
            if m["cand_n"] >= cfg.dwell:
                m["morph"], m["plan"], m["morph_t"] = m["plan"], m["cand"], 0
                m["last_assign"] = -10 ** 9; m["perm"] = None
                m["switches"].append((clock, m["morph"], m["plan"]))
                S[idx, MOLT] = 0.0
                contested = False
            else:
                contested = True
        else:
            m["cand"], m["cand_n"], contested = m["plan"], 0, False
        plan = self.plans[m["plan"]]

        # --- 2. composition
        dcounts = np.bincount(dom[idx], minlength=3)
        if m["perm"] is None:
            m["perm"] = self._perm(plan, dcounts)
        perm = m["perm"]
        if not contested:
            if cfg.molt:
                self._molt(sw, b, idx, plan, counts, gen)
            self._lay(sw, b, plan, perm, gen)
            al = (sw.active[b] & sw.hatched[b]).numpy(); idx = np.nonzero(al)[0]
        mol = idx[S[idx, MOLT] > 0]
        if len(mol):
            S[mol, MOLT] += 1.0 / cfg.molt_steps
            done = mol[S[mol, MOLT] >= 1.0]
            elem[done] = S[done, MOLT_TO].astype(int); S[done, MOLT] = 0.0
            if len(done):
                m["last_assign"] = -10 ** 9

        # --- 3. homes
        m["t"] += 1.0
        if cfg.wander > 0:
            m["wphase"] += 1.0
            w = m["wphase"]
            m["psi"] += cfg.turn * (0.7 * math.sin(w * 0.013) + 0.3 * math.sin(w * 0.031 + 1.7))
            c_, s_ = math.cos(m["psi"]), math.sin(m["psi"])
            m["R"] = np.array([[c_, 0, s_], [0, 1, 0], [-s_, 0, c_]], np.float32)
            fwd = m["R"] @ np.array([0.0, 1.0, 0.0] if m["plan"] == "space" else [1.0, 0.0, 0.0], np.float32)
            if m["plan"] == "space":
                fwd = fwd * (0.4 + 0.6 * max(0.0, math.sin(w * 2 * math.pi / (2 * cfg.frame_steps * 7))))
            m["heading"] = fwd.astype(np.float32)
        sp, sv, ss = self._at(m, plan)
        anchor = m["anchor"]
        sig = (len(idx), int(elem[idx].sum()))
        if cfg.mode == "slots" and (clock - m["last_assign"] >= cfg.reassign_every or sig != m["alive_sig"]):
            self._assign(S, pos, elem, dom, idx, plan, perm, sp + anchor)
            m["last_assign"] = clock
        m["alive_sig"] = sig
        home = np.full((len(pos), 3), np.nan, np.float32); hv = np.zeros((len(pos), 3), np.float32)
        swell = 1.0 + cfg.inflate.get(m["plan"], 0.0) * m.get("threat", 0.0)
        if cfg.mode == "slots":
            hs = S[idx, HOME].astype(int) - 1
            ok = hs >= 0
            home[idx[ok]] = sp[hs[ok]] * swell + anchor
            hv[idx[ok]] = sv[hs[ok]]
        if cfg.orbit > 0:
            # each tadpole circles its home on a private loop (radius, phase, plane from its index)
            ii = idx
            ph = 2 * math.pi * _hash01(ii, 1.0) + cfg.orbit_w * (1 + 0.5 * _hash01(ii, 2.0)) * m["t"]
            th = math.pi * _hash01(ii, 3.0); ps = 2 * math.pi * _hash01(ii, 4.0)
            n_ = np.stack([np.sin(th) * np.cos(ps), np.cos(th), np.sin(th) * np.sin(ps)], 1)
            u1 = np.cross(n_, np.array([0.31, 0.62, 0.72]))
            u1 /= np.maximum(np.linalg.norm(u1, axis=1, keepdims=True), 1e-6)
            u2 = np.cross(n_, u1)
            wv = cfg.orbit_w * (1 + 0.5 * _hash01(ii, 2.0))
            off = cfg.orbit * (np.cos(ph)[:, None] * u1 + np.sin(ph)[:, None] * u2)
            dof = cfg.orbit * wv[:, None] * (-np.sin(ph)[:, None] * u1 + np.cos(ph)[:, None] * u2)
            okh = ~np.isnan(home[ii, 0])
            home[ii[okh]] += off[okh].astype(np.float32); hv[ii[okh]] += dof[okh].astype(np.float32)

        # --- 4. steering
        x = pos[idx]; v = S[idx, VEL]
        desired = np.zeros_like(x)
        hh = home[idx]; has = ~np.isnan(hh[:, 0])
        desired[has] = hv[idx][has] + cfg.k_arrive * (hh[has] - x[has])
        if (~has).any():
            g = self._grad(plan, m, x[~has] - anchor, elem[idx][~has], cfg.field_sigma)
            desired[~has] = cfg.field_k * g
        if m["morph"] is not None:
            u = m["morph_t"] / cfg.morph_steps
            if u >= 1:
                m["morph"] = None
            else:
                r = x - anchor
                axis = np.array([0.0, 1.0, 0.0], np.float32)
                tang = np.cross(axis, r)
                tang /= np.maximum(np.linalg.norm(tang, axis=-1, keepdims=True), 1e-3)
                amp = cfg.swirl * math.sin(math.pi * u)
                old = self.plans[m["morph"]]
                g_old = self._grad(old, m, r, elem[idx], cfg.field_sigma * 2)
                desired = desired + amp * tang * np.linalg.norm(r, axis=-1, keepdims=True) ** 0.5 * 0.3 \
                          + (1 - u) ** 2 * 0.5 * g_old
                if cfg.burst > 0 and u < 0.3:
                    rn = r / np.maximum(np.linalg.norm(r, axis=-1, keepdims=True), 1e-3)
                    desired = desired + cfg.burst * math.sin(math.pi * u / 0.3) * rn
                m["morph_t"] += 1
        if cfg.jitter > 0:
            rng = np.random.default_rng((clock * 7919 + b * 104729 + cfg.seed) % (2 ** 32))
            a_ = 1.0 / max(cfg.jitter_tau, 1.0)
            J = S[idx, JIT] * (1 - a_) + math.sqrt(2 * a_) * rng.normal(size=(len(idx), 3)).astype(np.float32)
            S[idx, JIT] = J
            desired = desired + cfg.jitter * J
        d = x[:, None] - x[None]
        dist = np.sqrt((d * d).sum(-1)) + np.eye(len(x)) * 1e6
        near = dist < cfg.sep_r
        sep = (d / np.maximum(dist, 1e-3)[..., None] * (near * (cfg.sep_r - dist))[..., None]).sum(1)
        nb = dist < cfg.align_r
        cnt = nb.sum(1, keepdims=True)
        align = np.where(cnt > 0, (nb[..., None] * v[None]).sum(1) / np.maximum(cnt, 1), v) - v
        st = S[idx, STARTLE] * cfg.startle_decay
        flee = np.zeros_like(x)
        mob_e = np.clip(np.array(cfg.mob) + cfg.hunt, 0, 1)
        for (c, rad, pv) in self.predators:
            rel = x - c
            dd = np.linalg.norm(rel, axis=-1)
            sense = cfg.sense * rad
            spd = max(float(np.linalg.norm(pv)), 1e-6)
            pvn = pv / spd
            along = rel @ pvn
            lat = rel - along[:, None] * pvn
            dl = np.linalg.norm(lat, axis=-1)
            latn = lat / np.maximum(dl, 1e-3)[:, None]
            ahead = np.clip(1 - along / (cfg.lookahead * spd + rad), 0, 1) * (along > -rad)
            w_path = np.clip(1 - dl / sense, 0, 1) * ahead
            w_here = np.clip(1 - dd / sense, 0, 1)
            w = np.maximum(w_path, w_here)
            st = np.maximum(st, np.clip(1.4 * w, 0, 1))
            radial = rel / np.maximum(dd, 1e-3)[:, None]
            swirl = np.cross(pvn, latn)
            fk = np.array(cfg.flee)[elem[idx]][:, None]
            if cfg.curious > 0:
                # middle distance: drift toward the ship (a curious school comes to look)
                wc = np.clip((dd - sense) / sense, 0, 1) * np.clip((3 * sense - dd) / sense, 0, 1)
                flee += -cfg.curious * wc[:, None] * radial
            if spd < cfg.mob_speed:
                mk = mob_e[elem[idx]][:, None]
                w_mob = np.clip(1 - dd / (2.5 * sense), 0, 1)[:, None] * mk
                up = np.array([0.0, 1.0, 0.0])
                tang = np.cross(up, radial); tang /= np.maximum(np.linalg.norm(tang, axis=-1, keepdims=True), 1e-3)
                orbit = 0.4 * (1.4 * rad - dd)[:, None] * radial + 1.5 * tang
                flee += w_mob * orbit
                fk = fk * (1 - mk)
            flee += w[:, None] * fk * (0.75 * latn + 0.25 * radial + cfg.flee_swirl * 0.5 * swirl)
        if len(x) > 1:
            relay = (nb * st[None]).max(1) * cfg.relay
            st = np.maximum(st, relay)
        S[idx, STARTLE] = st
        m["threat"] = 0.85 * m.get("threat", 0.0) + 0.15 * min(1.0, 3.0 * float(st.mean()))
        calm = (1 - 0.8 * st)[:, None]
        steer = calm * desired + cfg.sep_k * sep + cfg.align_k * align + flee * 2.0
        vmax = np.array(cfg.vmax)[elem[idx]] * cfg.vscale * (1 + 0.8 * st)
        v = (1 - cfg.accel) * v + cfg.accel * steer
        sp_ = np.linalg.norm(v, axis=-1)
        v = v * np.minimum(1, vmax / np.maximum(sp_, 1e-6))[:, None]
        x = x + v
        r = np.linalg.norm(x, axis=-1, keepdims=True)
        x = np.where(r > self.world.membrane, x * self.world.membrane / r, x)
        pos[idx] = x; S[idx, VEL] = v
        m["anchor"] = (0.9 * anchor + 0.1 * (x.mean(0) - sp.mean(0))).astype(np.float32) + (cfg.cruise + cfg.wander) * m["heading"]

        # --- 5. looks
        look = np.concatenate([np.arange(FAC.start, FAC.stop), np.arange(PR.start, PR.stop), np.arange(TI.start, TI.stop),
                               np.arange(SP.start, SP.stop)])
        if cfg.mode == "slots":
            hs = S[idx, HOME].astype(int) - 1
            ok = hs >= 0
            S[np.ix_(idx[ok], look)] = 0.7 * S[np.ix_(idx[ok], look)] + 0.3 * ss[hs[ok]][:, look]
        else:
            pr = sp + anchor
            for e in range(4):
                me = idx[elem[idx] == e]
                if not len(me):
                    continue
                cand = np.nonzero(plan.elem == e)[0]
                if not len(cand):
                    cand = np.arange(plan.n)
                nn_ = cand[np.argmin(((pos[me][:, None] - pr[cand][None]) ** 2).sum(-1), 1)]
                S[np.ix_(me, look)] = 0.7 * S[np.ix_(me, look)] + 0.3 * ss[nn_][:, look]
        flash = (S[idx, STARTLE] > 0.4) & ((elem[idx] == 0) | bool(cfg.bristle))
        ch = idx[flash]
        if len(ch):
            S[ch, TI.start:TI.stop] = np.array([-4.0, 8.0, -4.0])
        S[idx, A] = 1.0; S[idx, DIE] = -12.0
        S[idx, BIRTH] += 1


# ---------------------------------------------------------------------- the genome

# name: (lo, hi, kind)  kind: "f" float, "i" int, "b" on/off bit (gates the matching amplitude gene)
GENES = {
    "k_arrive": (0.15, 0.6, "f"), "accel": (0.2, 0.8, "f"), "sep_r": (1.4, 2.8, "f"), "sep_k": (0.2, 1.0, "f"),
    "align_r": (2.5, 8.0, "f"), "align_k": (0.0, 0.5, "f"), "vscale": (0.6, 1.6, "f"),
    "sticky": (0.0, 10.0, "f"), "lay_rate": (0.015, 0.1, "f"), "lay_max": (1, 8, "i"),
    "molt_rate": (0.01, 0.08, "f"), "molt_steps": (4, 24, "i"), "morph_steps": (20, 120, "i"),
    "swirl": (0.0, 2.5, "f"), "dwell": (4, 24, "i"),
    "sense": (1.2, 3.5, "f"), "relay": (0.0, 0.95, "f"), "startle_decay": (0.75, 0.97, "f"),
    "flee_c": (0.0, 2.5, "f"), "flee_m": (0.0, 2.5, "f"), "flee_s": (0.0, 2.5, "f"), "flee_t": (0.0, 2.5, "f"),
    "flee_swirl": (0.0, 2.0, "f"), "lookahead": (2.0, 20.0, "f"),
    "mob_c": (0.0, 1.0, "f"), "mob_m": (0.0, 1.0, "f"), "mob_s": (0.0, 1.0, "f"), "mob_t": (0.0, 1.0, "f"),
    "mob_speed": (0.3, 4.0, "f"),
    "inflate_m": (0.0, 0.8, "f"), "inflate_s": (0.0, 0.8, "f"), "inflate_c": (0.0, 1.0, "f"), "inflate_t": (0.0, 0.8, "f"),
    "breathe": (0.0, 0.2, "f"), "breathe_on": (0, 1, "b"), "breathe_period": (16.0, 120.0, "f"),
    "jitter": (0.0, 0.6, "f"), "jitter_on": (0, 1, "b"), "jitter_tau": (2.0, 20.0, "f"),
    "orbit": (0.0, 1.8, "f"), "orbit_on": (0, 1, "b"), "orbit_w": (0.05, 0.5, "f"),
    "burst": (0.0, 2.5, "f"), "burst_on": (0, 1, "b"),
    "curious": (0.0, 1.5, "f"), "curious_on": (0, 1, "b"),
    "hunt": (0.0, 1.0, "f"), "hunt_on": (0, 1, "b"),
    "rush": (0.0, 4.0, "f"), "rush_on": (0, 1, "b"),
    "bristle": (0, 1, "b"),
}
GATED = ("breathe", "jitter", "orbit", "burst", "curious", "hunt", "rush")
NAMES = list(GENES)


def field_genome():
    """The `field` creature, in genome form (all new behaviours off)."""
    c = ZooCfg()
    g = dict(k_arrive=c.k_arrive, accel=c.accel, sep_r=c.sep_r, sep_k=c.sep_k, align_r=c.align_r, align_k=c.align_k,
             vscale=1.0, sticky=c.sticky, lay_rate=c.lay_rate, lay_max=c.lay_max, molt_rate=c.molt_rate,
             molt_steps=c.molt_steps, morph_steps=c.morph_steps, swirl=c.swirl, dwell=c.dwell, sense=c.sense,
             relay=c.relay, startle_decay=c.startle_decay, flee_c=c.flee[0], flee_m=c.flee[1], flee_s=c.flee[2],
             flee_t=c.flee[3], flee_swirl=c.flee_swirl, lookahead=c.lookahead, mob_c=0.0, mob_m=0.0, mob_s=0.0,
             mob_t=1.0, mob_speed=c.mob_speed, inflate_m=0.0, inflate_s=0.0, inflate_c=0.45, inflate_t=0.0,
             breathe=0.08, breathe_on=0, breathe_period=48.0, jitter=0.2, jitter_on=0, jitter_tau=8.0,
             orbit=0.6, orbit_on=0, orbit_w=0.25, burst=1.0, burst_on=0, curious=0.5, curious_on=0,
             hunt=0.5, hunt_on=0, rush=1.5, rush_on=0, bristle=0)
    return g


def to_unit(g):
    return np.array([(g[k] - GENES[k][0]) / (GENES[k][1] - GENES[k][0]) for k in NAMES], np.float64)


def from_unit(u):
    g = {}
    for k, x in zip(NAMES, np.clip(u, 0, 1)):
        lo, hi, kind = GENES[k]
        v = lo + float(x) * (hi - lo)
        g[k] = int(round(v)) if kind in ("i", "b") else round(v, 4)
    return g


def genome_to_cfg(g, wander=0.0):
    on = lambda k: g[k] if g.get(k + "_on", 1) else 0.0
    return ZooCfg(k_arrive=g["k_arrive"], accel=g["accel"], sep_r=g["sep_r"], sep_k=g["sep_k"], align_r=g["align_r"],
                  align_k=g["align_k"], vscale=g["vscale"], sticky=g["sticky"], lay_rate=g["lay_rate"],
                  lay_max=int(g["lay_max"]), molt_rate=g["molt_rate"], molt_steps=int(g["molt_steps"]),
                  morph_steps=int(g["morph_steps"]), swirl=g["swirl"], dwell=int(g["dwell"]), sense=g["sense"],
                  relay=g["relay"], startle_decay=g["startle_decay"],
                  flee=(g["flee_c"], g["flee_m"], g["flee_s"], g["flee_t"]), flee_swirl=g["flee_swirl"],
                  lookahead=g["lookahead"], mob=(g["mob_c"], g["mob_m"], g["mob_s"], g["mob_t"]), mob_speed=g["mob_speed"],
                  inflate4=(g["inflate_m"], g["inflate_s"], g["inflate_c"], g["inflate_t"]),
                  breathe=on("breathe"), breathe_period=g["breathe_period"], jitter=on("jitter"), jitter_tau=g["jitter_tau"],
                  orbit=on("orbit"), orbit_w=g["orbit_w"], burst=on("burst"), curious=on("curious"), hunt=on("hunt"),
                  rush=on("rush"), bristle=int(g["bristle"]), wander=wander, assign="hungarian")


def make(g, wander=0.0):
    return ZooSwarm(genome_to_cfg(g, wander))


if __name__ == "__main__":
    import time
    import swarm_eval as se
    torch.set_num_threads(1)
    t0 = time.time()
    r = se.evaluate(make(field_genome()), samples=1, gate=(4, 8))
    print(r["passed"], r["feasible"], r["na"], round(time.time() - t0, 1), "s")
