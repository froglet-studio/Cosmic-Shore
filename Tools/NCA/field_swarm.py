"""Designed fields + boids: the game-ready swarm baseline (research direction `field`).

No learned per-particle rule. Every tadpole is a boid (separation / alignment / arrive, a per-element
top speed) steering toward a HOME in a body-plan ATTRACTOR. The attractor has two layers:

  * SLOTS (crisp): the plan's own units, animated through its eight frames, are assigned to tadpoles
    by a min-cost matching (Hungarian, scipy) on position + element + domain region, with a stickiness
    bonus so a tadpole keeps its slot unless something clearly better comes along. A tadpole that wins
    no slot (the swarm is larger than the plan) shares the nearest slot of its own element.
  * FIELD (soft): per plan and per element, an RBF density sum over that element's target units. A
    tadpole with no home climbs its element's density gradient. `mode="field"` uses the field alone,
    so the two can be compared (a field-only swarm is the cheapest possible port).

The plan is chosen by element RATIOS with time hysteresis: the living majority element names the plan,
and a new majority must hold for `dwell` steps before the swarm commits. While the majority is
contested the swarm neither lays nor molts (that is what kept the learned rules flipping back).
A committed switch MORPHS: every tadpole is re-assigned to the new plan and travels there along a
vortex around the swarm's axis whose strength peaks mid-morph, while the old plan's field fades out.

Composition (all mass-conserving):
  * LAYING breeds true (element AND domain = the parent's). A parent of a deficit (element, domain)
    lays beside the nearest empty slot of that kind, so a body grows from its holes. Headcount goal =
    the plan's unit count; no laying past it.
  * MOLTING (`molt=1`, the one relaxed constraint): a tadpole of a SURPLUS element may change element
    into a deficit one over `molt_steps` (its crystal re-forms; nothing appears or vanishes). Without
    it a switched swarm keeps the old plan's leftovers forever (they cannot die on a clock and cannot
    be un-laid). Domain never changes.
  * Nothing dies on its own. Deaths come only from outside (the eval cull, a vessel strike).

Player-facing behaviour: `model.predators` is a list of (centre, radius, velocity). Tadpoles near one
are STARTLED; startle propagates neighbour to neighbour as a wave (a fish school's startle cascade)
and decays. Startled tadpoles flee radially with a swirl around the predator's path (the school PARTS
around a ship and closes behind it), each element in its own way: Time darts, Space jets away, Mass
shoulders aside, and Charge holds and flashes its plates to DANGER (a pufferfish spiking).

    python Tools/NCA/field_swarm.py rollout              # score through swarm_nca.rollout, print the tables
    python Tools/NCA/field_swarm.py publish              # write results/field/ (summary, rollout, probe, params)
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
import time
from dataclasses import dataclass, asdict, field

import numpy as np
import torch
from scipy.optimize import linear_sum_assignment

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import swarm_nca as sn  # noqa: E402

A, FAC, PR, TI, SP, DIE = sn.A, sn.FAC, sn.PR, sn.TI, sn.SP, sn.DIE
VEL = slice(12, 15)      # velocity
HOME = 15                # assigned slot index + 1 (0 = none)
MOLT = 16                # molt progress (0 = not molting)
MOLT_TO = 17             # element being molted into
STARTLE = 18             # startle level 0..1
BIRTH = 19               # steps since birth (grow-in)

ELEM_KIND = {sn.MAJOR[k]: k for k in sn.KINDS}


@dataclass
class FieldCfg:
    mode: str = "slots"          # "slots" (assignment + field fallback) | "field" (field only)
    frame_steps: int = 6         # steps per animation frame
    dwell: int = 12              # steps a new majority must hold before the swarm commits to its plan
    k_arrive: float = 0.35       # arrive gain toward home
    accel: float = 0.45          # velocity blend per step (inertia)
    sep_r: float = 2.0           # separation radius (target spacing 2.5-3.5)
    sep_k: float = 0.5
    align_r: float = 5.0
    align_k: float = 0.15
    vmax: tuple = (0.8, 0.8, 0.8, 2.0)   # per-element top speed (the World's)
    reassign_every: int = 8
    sticky: float = 4.0          # cost bonus for keeping the current slot
    lay_rate: float = 0.04       # eggs per step as a share of the headcount
    lay_max: int = 4
    molt: int = 1
    molt_rate: float = 0.03
    molt_steps: int = 10
    morph_steps: int = 60        # length of the switch spectacle
    swirl: float = 0.9           # vortex strength at mid-morph
    field_sigma: float = 3.0     # RBF width of the element density field
    field_k: float = 1.2
    cruise: float = 0.0          # anchor drift per step (0 for scoring; the viewer looks better > 0)
    # predator response
    sense: float = 2.2           # startle within sense x predator radius
    relay: float = 0.8           # startle passed to neighbours within align_r
    startle_decay: float = 0.9
    flee: tuple = (0.6, 0.5, 1.4, 2.0)   # per element (Charge holds, Mass shoulders, Space jets, Time darts)
    flee_swirl: float = 0.8
    inflate: dict = field(default_factory=lambda: {"charge": 0.45})   # plan-level threat response: the body swells
    lookahead: float = 10.0      # steps of the ship's path the school reacts to
    seed: int = 0


def _logit(x):
    x = np.clip(x, 1e-3, 1 - 1e-3)
    return np.log(x / (1 - x))


def invert_state(e, h, tier, f, sp):
    """Hidden-free state vector (channels FAC, PR, TI, SP) whose decode() gives this unit's attributes."""
    s = np.zeros(sn.C, np.float32)
    s[A] = 1.0
    s[DIE] = -12.0
    s[FAC] = np.asarray(f) * 4.0
    h = np.asarray(h, float)
    if e == 0:
        r = _logit((h - 0.3) / 1.9)
    elif e == 1:
        r = _logit((h - 0.5) / 1.3)
    elif e == 2:
        L = h[0]; cmax = min(max(L / 4, 0.25), 0.42)
        r = np.array([_logit((L - 0.9) / 2.1)] + [_logit((c - 0.25) / (cmax - 0.25)) if cmax > 0.2501 else 0.0 for c in h[1:]])
    else:
        L = h[0]; cmax = min(max(L / 1.5, 0.3), 0.6)
        r = np.array([_logit((L - 0.45) / 0.75)] + [_logit((c - 0.3) / (cmax - 0.3)) if cmax > 0.3001 else 0.0 for c in h[1:]])
    s[PR] = r
    s[TI] = np.eye(3)[int(tier)] * 8.0
    s[SP.start] = _logit(np.clip(sp[0] / 0.6, 0.02, 0.98))
    s[SP.start + 1] = np.arctanh(np.clip(sp[1] / 0.5, -0.98, 0.98))
    return s


class Plan:
    """One body plan, precomputed: animated slot positions (centred), slot attributes as states, the
    element density field's centres."""

    def __init__(self, T: sn.Target, frame_steps):
        self.T, self.kind, self.n = T, T.kind, T.n
        P = torch.stack([fr["p"] for fr in T.frames]).numpy()
        P = P - P[0].mean(0)                                    # common centring (frame 0's centroid)
        steps = np.linalg.norm(P[1:] - P[:-1], axis=-1).mean()
        wrap = np.linalg.norm(P[0] - P[-1], axis=-1).mean()
        order = list(range(len(P))) if wrap < 1.5 * steps else list(range(len(P))) + list(range(len(P) - 2, 0, -1))
        self.order, self.P = order, P.astype(np.float32)        # [F, M, 3]
        self.frame_steps = frame_steps
        fr0 = T.frames[0]
        self.elem = fr0["elem"].numpy()
        self.slot = fr0["slot"].numpy()
        self.S = np.stack([np.stack([invert_state(int(self.elem[i]), fr["h"][i].numpy(), int(fr["tier"][i]), fr["f"][i].numpy(),
                                                  fr["sp"][i].numpy()) for i in range(self.n)]) for fr in T.frames])   # [F, M, C]
        self.mix = np.bincount(self.elem, minlength=4)
        self.slot_mix = np.bincount(self.slot, minlength=3)

    def at(self, t):
        """Slot positions, velocities and states at continuous time t (steps)."""
        u = t / self.frame_steps
        L = len(self.order)
        i = int(math.floor(u)) % L; j = (i + 1) % L; a = u - math.floor(u)
        fi, fj = self.order[i], self.order[j]
        p = (1 - a) * self.P[fi] + a * self.P[fj]
        v = (self.P[fj] - self.P[fi]) / self.frame_steps
        s = (1 - a) * self.S[fi] + a * self.S[fj]
        return p, v, s

    def field_grad(self, x, elem, t, sigma):
        """Gradient (normalised to <= 1) of each tadpole's own-element RBF density at positions x."""
        p, _, _ = self.at(t)
        g = np.zeros_like(x)
        for e in range(4):
            m = elem == e
            c = p[self.elem == e]
            if not m.any() or len(c) == 0:
                if m.any():
                    c = p
                else:
                    continue
            d = c[None] - x[m][:, None]                       # [n, M, 3]
            w = np.exp(-(d * d).sum(-1) / (2 * sigma * sigma))
            gr = (w[..., None] * d).sum(1) / (sigma * sigma)
            # far from every centre the RBF vanishes: fall back to the nearest centre
            far = w.sum(1) < 1e-3
            if far.any():
                nn_ = np.argmin((d[far] ** 2).sum(-1), 1)
                gr[far] = d[far][np.arange(far.sum()), nn_]
            nrm = np.linalg.norm(gr, axis=-1, keepdims=True)
            g[m] = gr / np.maximum(nrm, 1.0)
        return g


class FieldSwarm:
    """model(sw, gen) -> sw, with model.world a swarm_nca.World. Per-sample memory lives on the model and
    is reset whenever sw.clock[b] == 0; per-tadpole memory lives in the Swarm's hidden channels."""

    def __init__(self, cfg: FieldCfg | None = None, world: sn.World | None = None):
        self.cfg = cfg or FieldCfg()
        self.world = world or sn.World()
        self.plans = {k: Plan(T, self.cfg.frame_steps) for k, T in sn.load_targets().items()}
        self.mem = {}
        self.predators = []          # [(centre np[3], radius, velocity np[3])], set by a probe each step
        self.timing = []

    def eval(self):
        return self

    # ------------------------------------------------------------------ bookkeeping

    def _reset(self, b, sw):
        al = (sw.active[b] & sw.hatched[b]).numpy()
        c = np.bincount(sw.elem[b].numpy()[al], minlength=4)
        kind = ELEM_KIND[int(np.argmax(c))]
        p = sw.pos[b].numpy()[al]
        self.mem[b] = dict(plan=kind, cand=kind, cand_n=0, morph=None, morph_t=0, t=0.0,
                           anchor=p.mean(0).astype(np.float32), perm=None, last_assign=-10 ** 9,
                           heading=np.array([1.0, 0.0, 0.0], np.float32), alive_sig=None, switches=[])

    def _perm(self, plan: Plan, dom_counts):
        """Domain -> slot mapping that best matches the swarm's domain counts to the plan's slot sizes."""
        ns = int((plan.slot_mix > 0).sum())
        best = None
        for perm in sn.PERMS[ns]:
            # perm[slot] = domain (as in the loss: tdom = perm[t.slot])
            cost = sum(abs(int(dom_counts[perm[sl]]) - int(plan.slot_mix[sl])) for sl in range(ns))
            if best is None or cost < best[0]:
                best = (cost, perm)
        return np.array(best[1])

    # ------------------------------------------------------------------ the step

    def __call__(self, sw: sn.Swarm, gen=None, **_):
        t0 = time.perf_counter()
        sw = sw.clone()
        for b in range(sw.B):
            if int(sw.clock[b]) == 0 or b not in self.mem:
                self._reset(b, sw)
            self._step(sw, b, gen)
        sw.clock = sw.clock + 1
        sw.since = sw.since + 1
        self.timing.append(time.perf_counter() - t0)
        return sw

    def _step(self, sw, b, gen):
        cfg, m = self.cfg, self.mem[b]
        al = (sw.active[b] & sw.hatched[b]).numpy()
        idx = np.nonzero(al)[0]
        if len(idx) == 0:
            return
        pos = sw.pos[b].numpy()          # views (sw was cloned): writes land in sw
        S = sw.s[b].numpy()
        elem = sw.elem[b].numpy()
        dom = sw.dom[b].numpy()

        # --- 1. which plan? element ratios, with time hysteresis
        e_eff = np.where(S[:, MOLT] > 0, S[:, MOLT_TO].astype(int), elem)   # a molting tadpole counts as what it becomes
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
                m["switches"].append((int(sw.clock[b]), m["morph"], m["plan"]))
                S[idx, MOLT] = 0.0                                          # cancel molts aimed at the old plan
                contested = False
            else:
                contested = True
        else:
            m["cand"], m["cand_n"], contested = m["plan"], 0, False
        plan = self.plans[m["plan"]]

        # --- 2. composition: molt surplus into deficit, lay deficits (paused while contested)
        dcounts = np.bincount(dom[idx], minlength=3)
        if m["perm"] is None:
            m["perm"] = self._perm(plan, dcounts)
        perm = m["perm"]
        if not contested:
            if cfg.molt:
                self._molt(sw, b, idx, plan, counts, gen)
            self._lay(sw, b, plan, perm, gen)
            al = (sw.active[b] & sw.hatched[b]).numpy(); idx = np.nonzero(al)[0]
        # finish molts
        mol = idx[S[idx, MOLT] > 0]
        if len(mol):
            S[mol, MOLT] += 1.0 / cfg.molt_steps
            done = mol[S[mol, MOLT] >= 1.0]
            elem[done] = S[done, MOLT_TO].astype(int); S[done, MOLT] = 0.0
            if len(done):
                m["last_assign"] = -10 ** 9

        # --- 3. homes
        m["t"] += 1.0
        sp, sv, ss = plan.at(m["t"])
        anchor = m["anchor"]
        sig = (len(idx), int(elem[idx].sum()))
        clock = int(sw.clock[b])
        if cfg.mode == "slots" and (clock - m["last_assign"] >= cfg.reassign_every or sig != m["alive_sig"]):
            self._assign(S, pos, elem, dom, idx, plan, perm, sp + anchor)
            m["last_assign"] = clock
        m["alive_sig"] = sig
        home = np.full((len(pos), 3), np.nan, np.float32); hv = np.zeros((len(pos), 3), np.float32)
        swell = 1.0 + cfg.inflate.get(m["plan"], 0.0) * m.get("threat", 0.0)    # the pufferfish inflates
        if cfg.mode == "slots":
            hs = S[idx, HOME].astype(int) - 1
            ok = hs >= 0
            home[idx[ok]] = sp[hs[ok]] * swell + anchor
            hv[idx[ok]] = sv[hs[ok]]
        # --- 4. steering (boids + attractor + morph vortex + predators)
        x = pos[idx]; v = S[idx, VEL]
        desired = np.zeros_like(x)
        hh = home[idx]; has = ~np.isnan(hh[:, 0])
        desired[has] = hv[idx][has] + cfg.k_arrive * (hh[has] - x[has])
        if (~has).any():
            g = plan.field_grad(x[~has] - anchor, elem[idx][~has], m["t"], cfg.field_sigma)
            desired[~has] = cfg.field_k * g
        if m["morph"] is not None:
            # the switch spectacle: a vortex around the swarm's vertical axis that peaks mid-morph, while the
            # old plan's field fades out under the new plan's
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
                g_old = old.field_grad(r, elem[idx], m["t"], cfg.field_sigma * 2)
                desired = desired + amp * tang * np.linalg.norm(r, axis=-1, keepdims=True) ** 0.5 * 0.3 \
                          + (1 - u) ** 2 * 0.5 * g_old
                m["morph_t"] += 1
        d = x[:, None] - x[None]
        dist = np.sqrt((d * d).sum(-1)) + np.eye(len(x)) * 1e6
        near = dist < cfg.sep_r
        sep = (d / np.maximum(dist, 1e-3)[..., None] * (near * (cfg.sep_r - dist))[..., None]).sum(1)
        nb = dist < cfg.align_r
        cnt = nb.sum(1, keepdims=True)
        align = np.where(cnt > 0, (nb[..., None] * v[None]).sum(1) / np.maximum(cnt, 1), v) - v
        st = S[idx, STARTLE] * cfg.startle_decay
        flee = np.zeros_like(x)
        for (c, rad, pv) in self.predators:
            rel = x - c
            dd = np.linalg.norm(rel, axis=-1)
            sense = cfg.sense * rad
            spd = max(float(np.linalg.norm(pv)), 1e-6)
            pvn = pv / spd
            along = rel @ pvn                                     # > 0: ahead of the ship
            lat = rel - along[:, None] * pvn
            dl = np.linalg.norm(lat, axis=-1)
            latn = lat / np.maximum(dl, 1e-3)[:, None]
            # threat = near the ship now, OR near its PATH within the look-ahead (the school parts before it arrives)
            ahead = np.clip(1 - along / (cfg.lookahead * spd + rad), 0, 1) * (along > -rad)
            w_path = np.clip(1 - dl / sense, 0, 1) * ahead
            w_here = np.clip(1 - dd / sense, 0, 1)
            w = np.maximum(w_path, w_here)
            st = np.maximum(st, np.clip(1.4 * w, 0, 1))
            radial = rel / np.maximum(dd, 1e-3)[:, None]
            swirl = np.cross(pvn, latn)
            fk = np.array(cfg.flee)[elem[idx]][:, None]
            flee += w[:, None] * fk * (0.75 * latn + 0.25 * radial + cfg.flee_swirl * 0.5 * swirl)
        if len(x) > 1:                      # startle cascade through the school
            relay = (nb * st[None]).max(1) * cfg.relay
            st = np.maximum(st, relay)
        S[idx, STARTLE] = st
        m["threat"] = 0.85 * m.get("threat", 0.0) + 0.15 * min(1.0, 3.0 * float(st.mean()))
        calm = (1 - 0.8 * st)[:, None]
        steer = calm * desired + cfg.sep_k * sep + cfg.align_k * align + flee * 2.0
        vmax = np.array(cfg.vmax)[elem[idx]] * (1 + 0.8 * st)
        v = (1 - cfg.accel) * v + cfg.accel * steer
        sp_ = np.linalg.norm(v, axis=-1)
        v = v * np.minimum(1, vmax / np.maximum(sp_, 1e-6))[:, None]
        x = x + v
        r = np.linalg.norm(x, axis=-1, keepdims=True)                    # the cell membrane
        x = np.where(r > self.world.membrane, x * self.world.membrane / r, x)
        pos[idx] = x; S[idx, VEL] = v
        # anchor follows the swarm (translation is free) plus an optional cruise
        m["anchor"] = (0.9 * anchor + 0.1 * (x.mean(0) - sp.mean(0))).astype(np.float32) + cfg.cruise * m["heading"]

        # --- 5. looks: blend toward the home slot's prism / tier / facing / spindle
        look = np.concatenate([np.arange(FAC.start, FAC.stop), np.arange(PR.start, PR.stop), np.arange(TI.start, TI.stop),
                               np.arange(SP.start, SP.stop)])
        if cfg.mode == "slots":
            hs = S[idx, HOME].astype(int) - 1
            ok = hs >= 0
            # a slot of another element (shared after a switch) still lends its facing / spindle; the prism is
            # mapped into the tadpole's own element identity by decode(), so it can never break element
            S[np.ix_(idx[ok], look)] = 0.7 * S[np.ix_(idx[ok], look)] + 0.3 * ss[hs[ok]][:, look]
        else:
            # field mode: borrow the attributes of the nearest same-element target unit
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
        # Charge flashes to DANGER when startled (a pufferfish spiking)
        ch = idx[(elem[idx] == 0) & (S[idx, STARTLE] > 0.4)]
        if len(ch):
            S[ch, TI.start:TI.stop] = np.array([-4.0, 8.0, -4.0])
        S[idx, A] = 1.0; S[idx, DIE] = -12.0
        S[idx, BIRTH] += 1

    # ------------------------------------------------------------------ assignment

    def _assign(self, S, pos, elem, dom, idx, plan: Plan, perm, slots_world):
        x = pos[idx]
        d2 = ((x[:, None] - slots_world[None]) ** 2).sum(-1) / (2 * 2.0 ** 2)
        cost = np.where(d2 < 16, d2, 8 * np.sqrt(d2) - 16)
        cost = cost + 60.0 * (elem[idx][:, None] != plan.elem[None]) + 25.0 * (dom[idx][:, None] != perm[plan.slot][None])
        cur = S[idx, HOME].astype(int) - 1
        ok = (cur >= 0) & (cur < plan.n)
        cost[np.nonzero(ok)[0], cur[ok]] -= self.cfg.sticky
        r, c = linear_sum_assignment(cost)
        new = np.full(len(idx), -1)
        new[r] = c
        # tadpoles that won no slot share the cheapest slot of their own kind
        lone = np.nonzero(new < 0)[0]
        if len(lone):
            new[lone] = np.argmin(cost[lone], 1)
        S[idx, HOME] = new + 1

    # ------------------------------------------------------------------ composition

    def _lay(self, sw, b, plan: Plan, perm, gen):
        cfg = self.cfg
        al = (sw.active[b] & sw.hatched[b]).numpy(); idx = np.nonzero(al)[0]
        n = len(idx)
        if n >= plan.n:
            return
        k = min(cfg.lay_max, plan.n - n, max(1, int(math.ceil(cfg.lay_rate * n))))
        free = np.nonzero(~sw.active[b].numpy())[0]
        if not len(free):
            return
        pos = sw.pos[b].numpy(); S = sw.s[b].numpy(); elem = sw.elem[b].numpy(); dom = sw.dom[b].numpy()
        e_eff = np.where(S[:, MOLT] > 0, S[:, MOLT_TO].astype(int), elem)
        # wanted (element, domain) counts: slot kinds of the plan under the domain mapping
        tdom = perm[plan.slot]
        want = np.zeros((4, 3), int)
        np.add.at(want, (plan.elem, tdom), 1)
        have = np.zeros((4, 3), int)
        np.add.at(have, (e_eff[idx], dom[idx]), 1)
        deficit = want - have
        m = self.mem[b]
        sp, _, _ = plan.at(m["t"])
        slots_world = sp + m["anchor"]
        taken = set((S[idx, HOME].astype(int) - 1).tolist())
        laid = 0
        for _ in range(k):
            # the most-wanted (element, domain) pair that has a parent to breed true
            order = np.dstack(np.unravel_index(np.argsort(-deficit, axis=None), deficit.shape))[0]
            pick = None
            for e, dd in order:
                if deficit[e, dd] <= 0:
                    break
                par = idx[(elem[idx] == e) & (dom[idx] == dd) & (S[idx, MOLT] == 0)]
                if len(par):
                    pick = (e, dd, par)
                    break
            if pick is None or laid >= len(free):
                break
            e, dd, par = pick
            empt = [j for j in np.nonzero((plan.elem == e) & (tdom == dd))[0] if j not in taken]
            if empt:
                ej = np.array(empt)
                dmat = ((pos[par][:, None] - slots_world[ej][None]) ** 2).sum(-1)
                pi, sj = np.unravel_index(np.argmin(dmat), dmat.shape)
                p0, slot = par[pi], ej[sj]
                dirn = slots_world[slot] - pos[p0]
            else:
                p0, slot = par[int(torch.randint(len(par), (1,), generator=gen))], -1
                dirn = np.random.default_rng(int(sw.clock[b]) + laid).normal(size=3)
            dirn = dirn / max(np.linalg.norm(dirn), 1e-6)
            j = free[laid]
            pos[j] = pos[p0] + self.world.r_bud * dirn
            S[j] = S[p0]; S[j, VEL] = S[p0, VEL]; S[j, HOME] = slot + 1; S[j, MOLT] = 0; S[j, STARTLE] = 0; S[j, BIRTH] = 0
            elem[j], dom[j] = e, dd
            sw.active[b, j] = True; sw.hatched[b, j] = True; sw.age[b, j] = 0
            deficit[e, dd] -= 1
            if slot >= 0:
                taken.add(int(slot))
            laid += 1
        if laid:
            m["last_assign"] = -10 ** 9

    def _molt(self, sw, b, idx, plan: Plan, counts, gen):
        """Surplus elements molt into deficit ones (headcount-relative to the plan's mix)."""
        cfg = self.cfg
        S = sw.s[b].numpy(); elem = sw.elem[b].numpy(); pos = sw.pos[b].numpy()
        n = len(idx)
        goal = plan.mix / plan.mix.sum() * max(n, 1)
        surplus = counts - goal
        k = max(1, int(math.ceil(cfg.molt_rate * n)))
        m = self.mem[b]
        sp, _, _ = plan.at(m["t"]); slots_world = sp + m["anchor"]
        for _ in range(k):
            e_from = int(np.argmax(surplus)); e_to = int(np.argmin(surplus))
            if surplus[e_from] < 1.0 or surplus[e_to] > -1.0:
                break
            cand = idx[(elem[idx] == e_from) & (S[idx, MOLT] == 0)]
            if not len(cand):
                break
            # the surplus tadpole nearest a slot of the element it will become
            tgt = slots_world[plan.elem == e_to]
            dmat = ((pos[cand][:, None] - tgt[None]) ** 2).sum(-1).min(1)
            j = cand[int(np.argmin(dmat))]
            S[j, MOLT] = 1e-3; S[j, MOLT_TO] = e_to
            surplus[e_from] -= 1; surplus[e_to] += 1


# ---------------------------------------------------------------------- probes

def predator_pass(model, kind, steps=240, seed=5, speed=3.0, radius_frac=0.6, L=None):
    """A predator sphere (a vessel) flies straight through the grown swarm's centroid and out again; it
    kills nothing. Reports the share of tadpoles that were ever inside the sphere (lower = the school
    parted), the score before / at worst / after re-forming, and how many steps the re-forming took."""
    L = L or sn.LossCfg()
    T = sn.load_targets()
    gen = sn.make_gen(seed)
    sw = sn.seed_swarm([T[kind]], model.world, gen)
    for _ in range(steps):
        sw = model(sw, gen)
    score = lambda: sn.swarm_loss(sn.decode(sw, 0), T[kind], L)[1]["sink"]
    before = score()
    al = (sw.active[0] & sw.hatched[0]).numpy()
    p = sw.pos[0].numpy()[al]
    c = p.mean(0); rms = float(np.sqrt(((p - c) ** 2).sum(-1).mean()))
    rad = radius_frac * rms
    rng = np.random.default_rng(seed)
    d = rng.normal(size=3); d /= np.linalg.norm(d)
    start = c - d * (rms * 2.5 + rad)
    span = int(2 * (rms * 2.5 + rad) / speed)
    touched = np.zeros(sw.pos.shape[1], bool)
    worst, trace = before, []
    for t in range(span + 120):
        if t < span:
            pc = start + d * speed * t
            model.predators = [(pc, rad, d * speed)]
            al = (sw.active[0] & sw.hatched[0]).numpy()
            inside = al & (np.linalg.norm(sw.pos[0].numpy() - pc, axis=-1) < rad)
            touched |= inside
        else:
            model.predators = []
        sw = model(sw, gen)
        if t % 10 == 0:
            s_ = score(); trace.append(round(s_, 2)); worst = max(worst, s_)
    after = score()
    model.predators = []
    nal = int((sw.active[0] & sw.hatched[0]).sum())
    reform = None
    for i, s_ in enumerate(trace):
        if i * 10 > span and s_ <= before * 1.2 + 0.5:
            reform = i * 10 - span
            break
    return dict(before=round(before, 2), worst=round(worst, 2), after=round(after, 2),
                touched_share=round(float(touched.sum()) / max(nal, 1), 3), pass_steps=span,
                reform_steps_after_pass=reform, radius=round(rad, 1), trace=trace)


def per_step_cost(model, kind="mass", steps=60):
    T = sn.load_targets()
    gen = sn.make_gen(1)
    sw = sn.seed_swarm([T[kind]], model.world, gen)
    for _ in range(240):
        sw = model(sw, gen)
    model.timing = []
    for _ in range(steps):
        sw = model(sw, gen)
    return dict(kind=kind, n=int((sw.active[0] & sw.hatched[0]).sum()), ms_per_step=round(1000 * float(np.mean(model.timing)), 2))


def run_all(cfg: FieldCfg, out=None, quick=False):
    import swarm_probe
    model = FieldSwarm(cfg)
    t0 = time.time()
    data, summary = sn.rollout(model, 240)
    passed, close = sn.tests_passed(summary)
    sn.print_cross(summary); sn.print_geo(summary); sn.print_switch(summary)
    print(f"TESTS PASSED {passed}/8  (summed wanted divergence {close:.2f})  rollout {time.time() - t0:.0f}s")
    res = dict(passed=passed, close=close, summary=summary, data=data)
    if quick:
        return res
    probe = swarm_probe.probe(FieldSwarm(cfg))
    print("strike probe:", json.dumps({k: {kk: v[kk] for kk in ("before", "cut", "recovered", "heal", "n_before", "n_after")} for k, v in probe.items()}))
    pred = {k: predator_pass(FieldSwarm(cfg), k) for k in sn.KINDS}
    print("predator pass:", json.dumps({k: {kk: v[kk] for kk in v if kk != "trace"} for k, v in pred.items()}))
    cost = [per_step_cost(FieldSwarm(cfg), k) for k in sn.KINDS]
    print("cost:", cost)
    res.update(probe=probe, predator=pred, cost=cost)
    if out:
        os.makedirs(out, exist_ok=True)
        json.dump(sn.pack(data, 240), open(os.path.join(out, "rollout.json"), "w"))
        summary["meta"] = {"device": "cpu", "tag": "field", "note": "Designed fields + boids (no learning): field_swarm.py",
                           "cfg": asdict(cfg)}
        summary["rule"] = "params.json"
        json.dump(summary, open(os.path.join(out, "summary.json"), "w"), indent=1)
        json.dump(dict(strike=probe, predator_pass=pred, cost=cost), open(os.path.join(out, "probe.json"), "w"), indent=1)
        json.dump(dict(cfg=asdict(cfg), passed=passed, close=round(close, 2),
                       switches={k: data[k].get("switched_at") for k in sn.KINDS}),
                  open(os.path.join(out, "params.json"), "w"), indent=1)
    return res


def main():
    torch.set_num_threads(4)
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["rollout", "publish"])
    ap.add_argument("--set", action="append", default=[])
    ap.add_argument("--out", default=os.path.join(HERE, "results", "field"))
    a = ap.parse_args()
    cfg = FieldCfg()
    for kv in a.set:
        k, v = kv.split("=", 1)
        cur = getattr(cfg, k)
        setattr(cfg, k, type(cur)(eval(v)) if isinstance(cur, tuple) else type(cur)(v))
    run_all(cfg, out=a.out if a.cmd == "publish" else None, quick=a.cmd == "rollout")


if __name__ == "__main__":
    main()
