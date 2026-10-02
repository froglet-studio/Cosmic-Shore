"""HierSim: the two levels, the LOD manager between them, impostors and the ledger.

    sim = HierSim(Params(), seed=7)
    sim.populate(n_herb=45000, n_pred=4000)
    sim.add_pilot(Pilot.wanderer())
    for _ in range(steps): sim.step()          # one micro tick (P.dt_micro); macro + LOD run every dt_macro

LOD rules (all in one place, `_lod`):
  HOT   a region whose centre is within expand_radius of a pilot, or within expand_ahead inside its forward
        cone (prefetch: the region is populated before the pilot gets there).
  COLD  a hot region with no pilot inside collapse_radius (hysteresis: expand 420 / collapse 620).
  EXPAND (cold -> hot): every macro individual of the region becomes an agent NOW, sampled from the macro
        state: stomach from its bin (+ its exact share of the pool), position from the region's food
        occupancy. Each agent is drawn LERPED from one of the region's impostor representatives to its sampled
        spot over bloom_s, at full size - so the cloud a player was looking at disperses into the herd; the
        drawn body volume is the same before, during and after (popping metric below).
  ABSORB (agent -> macro): an agent standing in a COLD region is folded into that region's bins only when NO
        pilot can see it (outside visible_radius, or outside every pilot's forward cone beyond 60 u). A seen
        individual is never deleted; it just keeps being simulated until it is not seen.
  ARRIVE: a macro hop from a cold region into a hot one becomes an agent that walks out of the source
        region's nearest representative toward the hot region.
"""
from __future__ import annotations

import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from common.arena import Arena, Pilot  # noqa: E402

from .params import Params, HERB, PRED, ELEMENTS  # noqa: E402
from .world import World  # noqa: E402
from .macro import Macro, NE  # noqa: E402
from .micro import Agents  # noqa: E402

SP = (HERB, PRED)


class HierSim:
    def __init__(self, P: Params | None = None, seed: int = 7, R: float | None = None, lod: bool = True):
        self.P = P or Params()
        self.rng = np.random.default_rng(seed)
        self.W = World(self.P, self.rng, R=R)
        self.M = Macro(self.W, self.P, self.rng)
        self.A = Agents(self.W, self.P, self.rng)
        self.arena = Arena(seed=seed, R=self.W.R)
        self.t = 0.0
        self.k = 0
        self.hot = np.zeros(self.W.nreg, bool)
        self.lod = lod                    # False: everything stays macro unless forced
        self.force_hot = None             # bool mask overriding the pilot rule (tests)
        nr = self.W.nreg
        k = self.P.reps_per_region
        self.reps = self.W.centers[:, None, None, :] + self.rng.uniform(-60, 60, (nr, 2, k, 3))
        self.rep_target = self.reps.copy()
        # telemetry
        self.events = dict(expand=0, absorb=0, arrive=0, seen_deleted=0, settle=0.0)
        self.timing = dict(macro=0.0, micro=0.0, lod=0.0, flora=0.0, steps=0, macro_steps=0)
        self.ledger0 = None

    # ---- setup -------------------------------------------------------------------------------------------
    def populate(self, n_herb=45000, n_pred=4000, flora_fill=0.5, nutrient=3000.0):
        W, rng = self.W, self.rng
        W.seed_flora(flora_fill, nutrient)
        G = W.F.sum(1); pr = G / G.sum()
        for pop, N, e in ((self.M.H, n_herb, 12.0), (self.M.Pr, n_pred, 60.0)):
            if N == 0:
                continue
            r = rng.choice(W.nreg, N, p=pr); el = rng.integers(0, NE, N)
            pop.place(r, el, np.clip(rng.normal(e, 0.15 * e, N), 0.2 * e, 1.6 * e))
        self._retarget_reps(all_regions=True)
        self.reps[:] = self.rep_target
        self.ledger0 = self.ledger()

    def add_pilot(self, p: Pilot, pos=None):
        self.arena.add_pilot(p)
        if pos is not None:
            p.pos = np.asarray(pos, float)
        return p

    # ---- ledger ------------------------------------------------------------------------------------------
    def ledger(self):
        W = self.W
        parts = dict(flora=float(W.F.sum()), skeleton=float(W.K.sum()), nutrient=float(W.N.sum()),
                     macro=self.M.mass(), agents=self.A.mass())
        parts["total"] = sum(parts.values())
        return parts

    # ---- stepping ----------------------------------------------------------------------------------------
    def step(self):
        P = self.P
        dt = P.dt_micro
        per = max(1, int(round(P.dt_macro / dt)))
        if self.k % per == 0:
            t0 = time.perf_counter()
            self.W.step_flora(P.dt_macro)
            t1 = time.perf_counter()
            self._lod()
            t2 = time.perf_counter()
            self.M.step(P.dt_macro, ~self.hot, self.hot)
            self._drain_inbox()
            self._advect_reps(P.dt_macro)
            t3 = time.perf_counter()
            self.timing["flora"] += t1 - t0; self.timing["lod"] += t2 - t1; self.timing["macro"] += t3 - t2
            self.timing["macro_steps"] += 1
        t0 = time.perf_counter()
        self.A.step(dt)
        self.timing["micro"] += time.perf_counter() - t0
        self.arena.step(dt)
        self.t += dt; self.k += 1; self.timing["steps"] += 1

    # ---- LOD ---------------------------------------------------------------------------------------------
    def _pilot_geo(self):
        Ps = np.array([p.pos for p in self.arena.pilots]) if self.arena.pilots else np.zeros((0, 3))
        Vs = np.array([p.vel / max(np.linalg.norm(p.vel), 1e-9) for p in self.arena.pilots]) if self.arena.pilots else np.zeros((0, 3))
        return Ps, Vs

    def visible(self, pos):
        """Can any pilot SEE a body at pos? Within visible_radius and inside the forward cone, or within 60 u."""
        Ps, Vs = self._pilot_geo()
        vis = np.zeros(len(pos), bool)
        for p, v in zip(Ps, Vs):
            d = pos - p
            r = np.linalg.norm(d, axis=1)
            cos = (d @ v) / np.maximum(r, 1e-9)
            vis |= (r < 60.0) | ((r < self.P.visible_radius) & (cos > self.P.cone_cos))
        return vis

    def _lod(self):
        W, P = self.W, self.P
        if self.force_hot is not None:
            want = self.force_hot.copy()
        elif not self.lod:
            want = np.zeros(W.nreg, bool)
        else:
            Ps, Vs = self._pilot_geo()
            want = self.hot.copy()
            near = np.zeros(W.nreg, bool); keep = np.zeros(W.nreg, bool)
            for p, v in zip(Ps, Vs):
                d = W.centers - p
                r = np.linalg.norm(d, axis=1)
                cos = (d @ v) / np.maximum(r, 1e-9)
                near |= (r < P.expand_radius) | ((r < P.expand_ahead) & (cos > P.cone_cos))
                keep |= (r < P.collapse_radius) | ((r < P.expand_ahead + 100) & (cos > P.cone_cos))
            want = near | (self.hot & keep)
        newly = want & ~self.hot
        self.hot = want
        for r in np.flatnonzero(newly):
            self._expand(r)
        self._absorb()

    def _expand(self, r):
        W, rng, A = self.W, self.rng, self.A
        occ = self.M.occupancy()[r]
        for s, pop in enumerate(self.M.pops):
            n, m, v = pop.mean_var()
            for c in np.flatnonzero(n[r] > 0):  # noqa: B007
                Ne = pop.N[r, c]
                cnt = int(Ne.sum())
                target = float(pop.S[r, c])
                el = np.repeat(np.arange(NE), Ne).astype(np.int8)
                sd = np.sqrt(v[r, c])
                E = np.clip(rng.normal(m[r, c], sd, cnt), 0.05 * pop.sp.e0, pop.sp.e_birth - 1e-3)
                E += (target - E.sum()) / cnt
                if self.P.bug == "expand_flat_energy":
                    E[:] = target / cnt
                low = E < 0.01
                if low.any():          # never hand an agent an empty stomach: borrow from the rest
                    need = (0.01 - E[low]).sum()
                    E[low] = 0.01
                    E[~low] -= need / max((~low).sum(), 1)
                if self.P.bug == "expand_lose_pool":
                    E -= 0.05 * target / cnt
                resid = target - E.sum()
                if self.P.bug == "expand_lose_pool":
                    resid = 0.0
                if abs(resid) > 0:
                    W.N[r] += resid; self.events["settle"] += abs(resid)
                vv = rng.choice(W.nvox, cnt, p=occ)
                pos = W.centers[r] + W.vox_off[vv] + rng.uniform(-0.5, 0.5, (cnt, 3)) * W.vox_h
                orig = self.reps[r, s][rng.integers(0, P_reps(self.P), cnt)]
                A.add(pos, np.full(cnt, s, np.int8), el, E, origin=orig, emerge=0.0)
                self.events["expand"] += cnt
            pop.N[r] = 0; pop.S[r] = 0.0; pop.Q[r] = 0.0

    def _absorb(self):
        A, W = self.A, self.W
        n = A.n
        if n == 0:
            return
        pos = A.view("pos")
        reg = W.region_of(pos)
        reg = np.where(reg < 0, W.region_of(pos * 0.98), reg)
        cold = ~self.hot[np.maximum(reg, 0)]
        if not cold.any():
            return
        idx = np.flatnonzero(cold)
        vis = self.visible(pos[idx])
        go = idx[~vis]
        if len(go) == 0:
            return
        sp = A.view("sp")[go]; el = A.view("elem")[go]; E = A.view("E")[go]; rr = np.maximum(reg[go], 0)
        mh = sp == 0
        if mh.any():
            self.M.absorb_occupancy(rr[mh], W.voxel_of(pos[go][mh], rr[mh]))
        for s, pop in enumerate(self.M.pops):
            m = sp == s
            if m.any():
                pop.place(rr[m], el[m].astype(np.int64), E[m] * (0.99 if self.P.bug == "absorb_drop_stomach" else 1.0))
        mask = np.zeros(n, bool); mask[go] = True
        A.remove(mask)
        self.events["absorb"] += len(go)

    def _drain_inbox(self):
        A, W = self.A, self.W
        for (s, dst, src, e, energy) in self.M.inbox:
            reps = self.reps[src, s]
            j = int(np.argmin(np.linalg.norm(reps - W.centers[dst], axis=1)))
            p = reps[j]
            d = W.centers[dst] - p; d /= max(np.linalg.norm(d), 1e-9)
            A.add(p[None], np.array([s], np.int8), np.array([e], np.int8), energy, vel=d[None] * SP[s].speed)
            A.heading[A.n - 1] = d
            self.events["arrive"] += 1
        self.M.inbox.clear()

    # ---- impostors ---------------------------------------------------------------------------------------
    def _retarget_reps(self, all_regions=False, frac=0.1):
        """Representatives drift toward fresh samples of the region's occupancy (herbivores: food; predators:
        where the herbivores are, approximated by the same field). Each macro step re-targets a fraction."""
        W, rng, k = self.W, self.rng, self.P.reps_per_region
        occ = self.M.occupancy()
        nr = W.nreg
        sel = np.arange(nr) if all_regions else rng.choice(nr, max(1, int(frac * nr)), replace=False)
        cum = np.cumsum(occ[sel], 1)
        u = rng.random((len(sel), 2 * k))
        v = np.minimum((u[:, :, None] > cum[:, None, :]).sum(2), W.nvox - 1)
        pos = W.centers[sel][:, None] + W.vox_off[v] + rng.uniform(-0.5, 0.5, (len(sel), 2 * k, 3)) * W.vox_h
        self.rep_target[sel] = pos.reshape(len(sel), 2, k, 3)

    def _advect_reps(self, dt, tau=12.0):
        self._retarget_reps()
        self.reps += (self.rep_target - self.reps) * min(1.0, dt / tau)

    # ---- render buffers ----------------------------------------------------------------------------------
    def render(self, out):
        """Agents bright, impostor representatives dim and scaled by the count each stands for (drawn body
        volume conserved: a rep standing for m individuals is drawn at radius r_body * m^(1/3))."""
        A = self.A
        n = A.n
        if n:
            e = A.view("emerge")[:, None]
            ease = e * e * (3 - 2 * e)
            pos = A.view("origin") * (1 - ease) + A.view("pos") * ease
            sp = A.view("sp")
            col = np.where(sp[:, None] == 0, [[0.45, 1.0, 0.55]], [[1.0, 0.35, 0.3]])
            rad = np.where(sp == 0, 3.0, 6.0) * np.maximum(A.view("bloom"), 0.05) ** (1 / 3)
            out["agents"] = dict(pos=pos, col=col, size=rad * 2)
        for s, pop in enumerate(self.M.pops):
            cnt = pop.count()
            reg = np.flatnonzero(cnt > 0)
            if len(reg) == 0:
                continue
            k = self.P.reps_per_region
            m = cnt[reg] / k
            pos = self.reps[reg, s].reshape(-1, 3)
            rad = (3.0 if s == 0 else 6.0) * np.repeat(m, k) ** (1 / 3)
            c = [0.2, 0.5, 0.28] if s == 0 else [0.5, 0.18, 0.15]
            out[f"impostor_{SP[s].name}"] = dict(pos=pos, col=np.tile(c, (len(pos), 1)), size=rad * 2)

    # ---- state summaries (consistency test) --------------------------------------------------------------
    def summary(self, regions=None):
        """Counts, mean stomach, phase fractions per species over `regions` (default all), macro + agents."""
        W = self.W
        sel = np.ones(W.nreg, bool) if regions is None else np.isin(np.arange(W.nreg), regions)
        out = {}
        A = self.A
        n = A.n
        areg = W.region_of(A.view("pos")) if n else np.zeros(0, np.int64)
        for s, pop in enumerate(self.M.pops):
            sp = SP[s]
            cnt = int(pop.N[sel].sum())
            st = float(pop.S[sel].sum())
            ph = pop.phase_counts(sel)
            el = pop.elem_counts(sel).astype(float)
            m = (A.view("sp") == s) & (sel[np.maximum(areg, 0)] if n else np.zeros(0, bool))
            if m.any():
                E = A.view("E")[m]
                cnt += int(m.sum()); st += float(E.sum())
                ph = ph + np.bincount(phase_of(sp, E), minlength=3)
                el += np.bincount(A.view("elem")[m], minlength=4)
            out[sp.name] = dict(count=cnt, mean_e=st / max(cnt, 1), phase=(ph / max(cnt, 1)).tolist(),
                                elem=el.tolist())
        out["flora"] = float(W.F[sel].sum() + W.K[sel].sum())
        return out


def P_reps(P):
    return P.reps_per_region


def phase_of(sp, E):
    """0 sated, 1 forage, 2 hungry - the same thresholds the macro cohorts integrate their Normals over."""
    f = np.asarray(E) / sp.e_birth
    return np.where(f < sp.phase_lo, 2, np.where(f > sp.phase_hi, 0, 1))
