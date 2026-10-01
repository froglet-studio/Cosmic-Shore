"""lite_sortfeel: sortfeel_model.SortFeel made lighter, one flag at a time (all default to the published
behaviour, so LiteSortFeel() with no flags reproduces SortFeel step for step).

Flags (LiteCfg, on top of SortFeelCfg):
  vec_look  1: the per-tadpole "look" pass (a Python loop over every live tadpole writing its type's state
            vector, ~1/3 of a step) becomes one write per TYPE. Bit-identical output (well_look=0 only).
  frac      k >= 1: FRACTIONAL UPDATE (the lead's "only update a fraction of a larger swarm each frame").
            Each step only tadpoles with (slot + t) % k == 0 recompute their neighbour forces
            (collision, adhesion, swaps - the O(N^2) part); the rest COAST on their last velocity. The
            neighbour work becomes O(N^2 / k). Per-unit-time dynamics are held: an updated tadpole blends
            toward its wish with 1 - inertia^k (the k-step-equivalent of the per-step inertia blend), and
            everything rate-based (laying, molting, hatching, wander, the plan's dwell) still runs every step.
            Chemotaxis (O(N)) is still evaluated for everyone - it is needed for the swap energies anyway.
  ease      > 0: SMOOTHNESS RAMP. After a composition change (plan switch, or the live count changing by
            more than ease_frac of the body in one step - a cull or a strike) every tadpole's speed cap
            eases from ease_floor back to 1 x vmax over `ease` steps. An acceleration limit on the jolt the
            smoothness calibration found (lurch 6-7 right after a cull).
Profiling: self.prof accumulates seconds per phase when self.profile = True.
"""
import math
import os
import sys
import time
from dataclasses import dataclass

import numpy as np
import torch

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sort_model as sm  # noqa: E402
import sortfeel_model as fm  # noqa: E402
import swarm_nca as sn  # noqa: E402
from field_swarm import invert_state  # noqa: E402

H_ROLE, H_EST, H_VEL, H_AGE, H_FATE, H_FKEY = sm.H_ROLE, sm.H_EST, sm.H_VEL, sm.H_AGE, sm.H_FATE, sm.H_FKEY
H_WAND = fm.H_WAND


@dataclass
class LiteCfg(fm.SortFeelCfg):
    vec_look: int = 0
    frac: int = 1
    ease: float = 0.0
    ease_floor: float = 0.35
    ease_frac: float = 0.04


class LiteSortFeel(fm.SortFeel):
    def __init__(self, cfg=None, world=None):
        super().__init__(cfg or LiteCfg(), world)
        assert not self.cfg.local_centre, "lite: local_centre not supported"
        self.profile = False
        self.prof = {}
        self.ops = {}          # per-step operation counts (for the C# cost estimate)

    def _tic(self, name, t0):
        if self.profile:
            t = time.perf_counter(); self.prof[name] = self.prof.get(name, 0.0) + t - t0; return t
        return t0

    def _count(self, name, n):
        if self.profile:
            self.ops[name] = self.ops.get(name, 0) + int(n)

    # sortfeel's wander wrapper, around the lite core
    def _step(self, sw, b):
        cfg = self.cfg
        self._t = int(sw.clock[b])
        if cfg.wander <= 0:
            return self._core(sw, b)
        t0 = time.perf_counter()
        S = sw.s[b].numpy()
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        idx = np.nonzero(act & hat)[0]
        wsave = S[:, H_WAND].copy()
        self._core(sw, b)
        t0 = time.perf_counter() if self.profile else t0
        S = sw.s[b].numpy(); pos = sw.pos[b].numpy()
        S[:, H_WAND] = wsave
        if len(idx) == 0:
            return
        rng = self.mem[b]["rng"]
        idx2 = np.nonzero(sw.active[b].numpy() & sw.hatched[b].numpy())[0]
        keep = np.intersect1d(idx, idx2)
        if len(keep) == 0:
            return
        tau = cfg.wander_tau * (0.6 + 0.8 * ((keep * 0.6180339) % 1.0))
        a = np.exp(-1.0 / tau)[:, None]
        w = S[keep, H_WAND]
        w = a * w + np.sqrt(1 - a ** 2) * cfg.wander * rng.normal(size=(len(keep), 3))
        S[keep, H_WAND] = w
        pos[keep] += w
        fresh = np.setdiff1d(np.nonzero(act & ~hat)[0], idx2)
        S[fresh, H_WAND] = 0.0
        self._tic("wander", t0)

    def _core(self, sw, b):
        cfg, m = self.cfg, self.mem[b]
        t0 = time.perf_counter()
        rng = m["rng"]
        act = sw.active[b].numpy(); hat = sw.hatched[b].numpy()
        pos = sw.pos[b].numpy(); S = sw.s[b].numpy()
        elem = sw.elem[b].numpy(); dom = sw.dom[b].numpy()
        idx = np.nonzero(act & hat)[0]
        if len(idx) == 0:
            return
        counts = np.bincount(elem[idx], minlength=4)
        cur = sn.MAJOR[m["plan"]]; top = int(np.argmax(counts))
        maj = cur if counts[cur] == counts[top] else top
        contested = False
        switched = False
        if maj != cur:
            cand = sn.PLAN_OF[maj]
            m["cand_n"] = m["cand_n"] + 1 if m["cand"] == cand else 1
            m["cand"] = cand
            if m["cand_n"] >= cfg.dwell:
                m["plan"], m["perm"], m["cand_n"] = cand, None, 0
                switched = True
            else:
                contested = True
        else:
            m["cand"], m["cand_n"] = m["plan"], 0
        code = self.codes[m["plan"]]
        if m["perm"] is None or m["t"] % cfg.role_every == 0:
            m["perm"] = self._pick_perm(code, elem[idx], dom[idx], m["perm"])
        perm = m["perm"]
        role_of_dom = np.full(3, -1)
        for s_, d_ in enumerate(perm):
            role_of_dom[d_] = s_
        m["t"] += 1
        # smoothness ramp trigger
        if cfg.ease > 0:
            nprev = m.get("nlive", len(idx))
            if switched or abs(len(idx) - nprev) > cfg.ease_frac * max(nprev, 1):
                m["ease_t"] = 0
            m["nlive"] = len(idx)
            m["ease_t"] = m.get("ease_t", 1e9) + 1
        eggs = np.nonzero(act & ~hat)[0]
        if len(eggs):
            S[eggs, H_AGE] += 1
            ready = eggs[S[eggs, H_AGE] >= cfg.hatch_steps]
            if len(ready):
                sw.hatched[b, torch.as_tensor(ready)] = True
                hat = sw.hatched[b].numpy()
        idx = np.nonzero(act & hat)[0]
        n = len(idx)
        role = self._eff_role(S, idx, dom, role_of_dom, m)
        c0 = pos[idx].mean(0)
        xb = pos[idx] - c0
        t0 = self._tic("plan+hatch", t0)
        # --- own-type chemotaxis (everyone: O(N))
        E = np.zeros(n); G = np.zeros((n, 3))
        MU = xb.copy(); INV = np.zeros((n, 3, 3))
        pid = sn.KINDS.index(m["plan"])
        el_i = elem[idx]
        for e in range(4):
            for s in range(3):
                sel = (el_i == e) & (role == s)
                if not sel.any() or (e, s) not in code.wells:
                    continue
                js = idx[sel]
                key = 1 + pid * 16 + e * 4 + s
                stale = (S[js, H_FATE] < 1) | (S[js, H_FKEY] != key)
                if stale.any():
                    w = code.wells[(e, s)][0]
                    occ = np.bincount((S[js[~stale], H_FATE] - 1).astype(int), minlength=len(w)).astype(float)
                    need = w * sel.sum() - occ
                    for j in js[stale]:
                        f = int(np.argmax(need + 1e-3 * rng.random(len(w))))
                        S[j, H_FATE] = f + 1; S[j, H_FKEY] = key; need[f] -= 1
                kk = (S[js, H_FATE] - 1).astype(int)
                E[sel], G[sel] = code.energy_grad_fate(xb[sel], e, s, kk)
                MU[sel] = code.wells[(e, s)][1][kk]; INV[sel] = code.wells[(e, s)][2][kk]
        has = np.zeros((4, 4), bool)
        for (e2, s2) in code.wells:
            has[e2, s2] = True
        orph = ~has[el_i, np.where(role < 0, 3, role)]
        if orph.any():
            Eb = np.full(orph.sum(), np.inf); Gb = np.zeros((orph.sum(), 3))
            for key in code.wells:
                e2, g2 = code.energy_grad(xb[orph], *key)
                better = e2 < Eb
                Eb[better], Gb[better] = e2[better], g2[better]
            E[orph], G[orph] = Eb, Gb
        step = -cfg.k_well * G
        nrm = np.linalg.norm(step, axis=1, keepdims=True)
        step = step * np.minimum(1, cfg.well_clip / np.maximum(nrm, 1e-9))
        self._count("chemotaxis", n)
        t0 = self._tic("chemotaxis", t0)
        # --- neighbours for the updating share U only: rows U, columns everyone
        k = max(1, int(cfg.frac))
        if k == 1:
            U = np.arange(n)
        else:
            U = np.nonzero(((idx + m["t"]) % k) == 0)[0]
        P = pos[idx]
        nu = len(U)
        vel = S[idx, H_VEL]
        v_all = vel.copy()
        if nu:
            dx = P[None] - P[U][:, None]                       # [nu, n]: j - i
            d = np.linalg.norm(dx, axis=-1)
            d[np.arange(nu), U] = 1e3
            rep = np.clip(cfg.r0 - d, 0, None) / d
            f_col = -cfg.k_rep * (rep[..., None] * dx).sum(1)
            tid = el_i * 4 + (role + 1)
            same_t = tid[U][:, None] == tid[None]
            same_e = el_i[U][:, None] == el_i[None]
            same_r = role[U][:, None] == role[None]
            Aij = np.where(same_t, cfg.a_same, np.where(same_e, cfg.a_elem, np.where(same_r, cfg.a_role, cfg.a_other)))
            near = (d < cfg.R_adh) & (d > cfg.r0 * 0.9)
            f_adh = ((Aij * near / d)[..., None] * dx).sum(1)
            self._count("pairs", nu * n)
            t0 = self._tic("neighbours", t0)
            f_swap = np.zeros((nu, 3))
            if cfg.swap > 0 and n > 1:
                ui, jj = np.nonzero(d < cfg.r_swap)            # each pair seen from its U side only
                if len(ui):
                    ii = U[ui]
                    def e_at(a, y):
                        dd = y - MU[a]
                        return 0.5 * (dd * np.einsum("nij,nj->ni", INV[a], dd)).sum(1)
                    gain = (e_at(ii, xb[ii]) + e_at(jj, xb[jj])) - (e_at(ii, xb[jj]) + e_at(jj, xb[ii]))
                    go = gain > cfg.swap_margin
                    if go.any():
                        gu, gj = ui[go], jj[go]
                        vv = P[gj] - P[U[gu]]
                        np.add.at(f_swap, gu, cfg.swap * vv * 0.5)
                        r = (np.clip(cfg.r0 - d[gu, gj], 0, None) / d[gu, gj])[:, None] * vv
                        np.add.at(f_col, gu, cfg.k_rep * r)
                    self._count("swap_pairs", len(ui))
            t0 = self._tic("swaps", t0)
            want = step[U] + f_col + f_adh + f_swap
            if cfg.swirl_time:
                tm = el_i[U] == 3
                if tm.any():
                    want[tm] += cfg.swirl_time * np.cross(code.axis, xb[U][tm])
            if self.predators:
                want += self._flee(P[U], el_i[U])
            inr = cfg.inertia ** k
            v = inr * vel[U] + (1 - inr) * want
            v_all[U] = v
        vmax = np.array(cfg.vmax)[el_i][:, None]
        if cfg.ease > 0:
            ph = min(1.0, m["ease_t"] / cfg.ease)
            vmax = vmax * (cfg.ease_floor + (1 - cfg.ease_floor) * ph * ph * (3 - 2 * ph))
        sp = np.linalg.norm(v_all, axis=1, keepdims=True)
        v_all = v_all * np.minimum(1, vmax / np.maximum(sp, 1e-9))
        pos[idx] += v_all
        S[idx, H_VEL] = v_all
        t0 = self._tic("integrate", t0)
        # --- look
        if cfg.vec_look and not cfg.well_look:
            keep = S[idx, H_ROLE:H_FKEY + 1].copy()
            kid = el_i * 4 + (role + 1)
            for u in np.unique(kid):
                e, r = int(u) // 4, int(u) % 4 - 1
                st = code.state.get((e, r))
                if st is None:
                    for s2 in range(3):
                        st = code.state.get((e, s2))
                        if st is not None:
                            break
                if st is None:
                    st = invert_state(e, [1, 1, 1] if e != 2 else [2, .3, .3], 0, [0, 0, 1], [.3, 0])
                S[idx[kid == u]] = st
            S[idx, H_ROLE:H_FKEY + 1] = keep
        else:
            for i, (e, r) in enumerate(zip(el_i, role)):
                key = (int(e), int(r))
                st = code.state.get(key)
                j0 = idx[i]
                if cfg.well_look and st is not None and S[j0, H_FATE] >= 1 and \
                        int(S[j0, H_FKEY]) == 1 + pid * 16 + key[0] * 4 + key[1]:
                    st = code.wstate[key][int(S[j0, H_FATE]) - 1]
                if st is None:
                    for s2 in range(3):
                        st = code.state.get((int(e), s2))
                        if st is not None:
                            break
                if st is None:
                    st = invert_state(int(e), [1, 1, 1] if e != 2 else [2, .3, .3], 0, [0, 0, 1], [.3, 0])
                keep = S[j0, H_ROLE:H_FKEY + 1].copy()
                S[j0] = st
                S[j0, H_ROLE:H_FKEY + 1] = keep
        t0 = self._tic("look", t0)
        if not contested:
            self._lay(sw, b, code, perm, role_of_dom, rng)
            if cfg.molt:
                self._molt(sw, b, code, perm, role_of_dom, rng)
        self._tic("compose", t0)


def load(path, **kw):
    import json
    d = json.load(open(path))
    c = d["cfg"]; c["vmax"] = tuple(c["vmax"]); c["lap_elems"] = tuple(c.get("lap_elems", (3,)))
    c.update(kw)
    return LiteSortFeel(LiteCfg(**c))
