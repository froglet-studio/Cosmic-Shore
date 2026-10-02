"""The MICRO level: individual agents near pilots. Struct-of-arrays, vectorised, fixed tick.

The SAME biology as the macro level (params.Species): stomach, satiation-scaled Holling II grazing on the
shared flora voxels, metabolism to the region's nutrient pool, a split at e_birth, starvation at 0, and
predators that eat a herbivore whole. What the micro level ADDS is space: herbivores climb the local food
gradient, flee predators inside a flee radius, and migrate when hungry; predators chase the nearest
herbivore inside a sense radius and catch it inside a catch radius. The macro rates (occupancy theta,
attack a, handling h, hop rates) are FITTED to this level (calibrate.py), never the other way round.

Render-only state (bloom/merge) never feeds back into the simulation: an emerging agent is a real agent from
its first tick; only its drawn size eases in from its representative.
"""
from __future__ import annotations

import numpy as np
from scipy.spatial import cKDTree

from .params import Params, HERB, PRED

SPECIES = (HERB, PRED)


class Agents:
    def __init__(self, world, P: Params, rng: np.random.Generator, cap: int = 4096):
        self.W, self.P, self.rng = world, P, rng
        self.n = 0
        self._alloc(cap)
        self.next_id = 1
        self.kills = 0
        self.births = np.zeros(2, np.int64)
        self.deaths = np.zeros(2, np.int64)
        self.sprinting = np.zeros(0, bool)

    def _alloc(self, cap):
        def grow(a, shape, dtype, fill=0):
            new = np.full(shape, fill, dtype)
            if a is not None:
                new[:len(a)] = a
            return new
        g = lambda name, shape, dtype, fill=0: setattr(self, name, grow(getattr(self, name, None), shape, dtype, fill))
        g("pos", (cap, 3), float); g("vel", (cap, 3), float); g("E", cap, float)
        g("sp", cap, np.int8); g("elem", cap, np.int8); g("id", cap, np.int64)
        g("heading", (cap, 3), float); g("bloom", cap, float, 1.0); g("origin", (cap, 3), float)
        g("target", cap, np.int64, -1); g("emerge", cap, float, 1.0)
        self.cap = cap

    # ---- creation / removal ------------------------------------------------------------------------------
    def add(self, pos, sp, elem, E, origin=None, bloom=1.0, vel=None, emerge=1.0):
        pos = np.atleast_2d(pos); k = len(pos)
        if self.n + k > self.cap:
            self._alloc(max(self.cap * 2, self.n + k))
        s = slice(self.n, self.n + k)
        self.pos[s] = pos
        self.sp[s] = sp; self.elem[s] = elem; self.E[s] = E
        self.id[s] = np.arange(self.next_id, self.next_id + k); self.next_id += k
        d = self.rng.normal(size=(k, 3)); d /= np.linalg.norm(d, axis=1, keepdims=True)
        self.heading[s] = d
        self.vel[s] = vel if vel is not None else d * np.array([x.speed for x in SPECIES])[np.atleast_1d(sp)][:, None]
        self.origin[s] = pos if origin is None else origin
        self.bloom[s] = bloom
        self.emerge[s] = emerge
        self.target[s] = -1
        self.n += k
        return np.arange(s.start, s.stop)

    def remove(self, mask):
        """Compact away rows where mask is True (the caller has already booked their mass)."""
        keep = ~mask[: self.n]
        m = int(keep.sum())
        for name in ("pos", "vel", "E", "sp", "elem", "id", "heading", "bloom", "origin", "target", "emerge"):
            a = getattr(self, name)
            a[:m] = a[: self.n][keep]
        self.n = m

    def mass(self):
        n = self.n
        body = np.where(self.sp[:n] == 0, HERB.body, PRED.body)
        return float(self.E[:n].sum() + body.sum())

    def view(self, name):
        return getattr(self, name)[: self.n]

    # ---- one micro tick ----------------------------------------------------------------------------------
    def step(self, dt: float):
        W, P, rng = self.W, self.P, self.rng
        n = self.n
        if n == 0:
            return
        pos, vel, E, sp = self.pos[:n], self.vel[:n], self.E[:n], self.sp[:n]
        reg = W.region_of(pos)
        reg = np.where(reg < 0, W.region_of(pos * 0.98), reg)
        reg = np.maximum(reg, 0)
        vox = W.voxel_of(pos, reg)
        isH = sp == 0
        isP = ~isH
        emax = np.where(isH, HERB.e_max, PRED.e_max)
        sat = np.clip(1.0 - E / emax, 0.0, 1.0)
        frac = E / emax
        hungry = frac < np.where(isH, HERB.phase_lo, PRED.phase_lo)
        sprint = np.zeros(n, bool)

        # ---- grazing (herbivores) on the shared voxel field
        hi = np.flatnonzero(isH)
        if len(hi):
            G = W.F[reg[hi], vox[hi]] + W.K[reg[hi], vox[hi]]
            want = P.h_intake * G / (G + P.h_half) * sat[hi] * dt
            key = reg[hi] * W.nvox + vox[hi]
            uk, inv = np.unique(key, return_inverse=True)
            dem = np.bincount(inv, weights=want)
            ur, uv = uk // W.nvox, uk % W.nvox
            Gk = W.F[ur, uv] + W.K[ur, uv]
            sc = np.where(dem > Gk, Gk / np.maximum(dem, 1e-12), 1.0)
            took_k = dem * sc
            fF = np.where(Gk > 0, W.F[ur, uv] / np.maximum(Gk, 1e-12), 0.0)
            W.F[ur, uv] -= took_k * fF
            W.K[ur, uv] -= took_k * (1 - fF)
            E[hi] += want * sc[inv]

        # ---- predators: chase the nearest herbivore inside the sense radius, catch inside the catch radius
        pi = np.flatnonzero(isP & (frac < P.p_hunt_below))
        dead = np.zeros(n, bool)
        steer = np.zeros((n, 3))
        if len(hi) and len(pi):
            tree = cKDTree(pos[hi])
            d, j = tree.query(pos[pi], k=1, distance_upper_bound=P_sense(P))
            ok = np.isfinite(d)
            if ok.any():
                pp, vv = pi[ok], hi[j[ok]]
                steer[pp] = (pos[vv] - pos[pp]) / np.maximum(d[ok], 1e-6)[:, None] * 3.0
                sprint[pp] = True
                catch = d[ok] < P_catch(P)
                # resolve each victim once (first predator wins)
                cv, first = np.unique(vv[catch], return_index=True)
                cp = pp[catch][first]
                if len(cv):
                    meal = HERB.body + E[cv]
                    E[cp] += meal
                    dead[cv] = True
                    self.kills += len(cv)
        # ---- herbivores: flee predators inside the flee radius
        if len(hi) and isP.any():
            ptree = cKDTree(pos[isP])
            d, j = ptree.query(pos[hi], k=1, distance_upper_bound=P_flee(P))
            ok = np.isfinite(d)
            if ok.any():
                pidx = np.flatnonzero(isP)[j[ok]]
                away = pos[hi[ok]] - pos[pidx]
                steer[hi[ok]] += away / np.maximum(d[ok], 1e-6)[:, None] * 4.0
                sprint[hi[ok]] = True
        # ---- herbivores: climb the voxel food gradient; hungry ones wander far (migration)
        if len(hi):
            g = self._food_gradient(pos[hi], reg[hi], vox[hi])
            calm = ~sprint[hi]
            steer[hi[calm]] += g[calm] * np.where(hungry[hi][calm], 0.4, 1.0)[:, None]
        # ---- persistent wander heading (correlated random walk; hungry -> longer persistence, faster)
        th = self.heading[:n]
        th += rng.normal(0, 0.35, (n, 3)) * np.sqrt(dt) * np.where(hungry, 0.4, 1.0)[:, None]
        th /= np.maximum(np.linalg.norm(th, axis=1, keepdims=True), 1e-9)
        steer += th * np.where(hungry, 1.2, 0.6)[:, None]
        # ---- velocity
        spd = np.where(isH, np.where(sprint, HERB.sprint, HERB.speed), np.where(sprint, PRED.sprint, PRED.speed))
        sated = frac > np.where(isH, HERB.phase_hi, PRED.phase_hi)
        spd = spd * np.where(sated & ~sprint, 0.5, 1.0)
        nrm = np.linalg.norm(steer, axis=1, keepdims=True)
        want_v = np.where(nrm > 1e-9, steer / np.maximum(nrm, 1e-9), th) * spd[:, None]
        k = min(1.0, 4.0 * dt)
        vel += (want_v - vel) * k
        pos += vel * dt
        r = np.linalg.norm(pos, axis=1)
        out = r > W.R * 0.985
        if out.any():
            nrm_o = pos[out] / r[out, None]
            pos[out] = nrm_o * W.R * 0.985
            vn = (vel[out] * nrm_o).sum(1, keepdims=True)
            vel[out] -= 2 * np.maximum(vn, 0) * nrm_o
            self.heading[:n][out] -= 2 * np.maximum((self.heading[:n][out] * nrm_o).sum(1, keepdims=True), 0) * nrm_o
        # ---- metabolism -> the nutrient pool of the region the agent stands in
        burn = np.where(isH, HERB.metab + sprint * HERB.sprint_metab, PRED.metab + sprint * PRED.sprint_metab) * dt
        burn = np.minimum(burn, np.maximum(E, 0.0))
        burn[dead] = 0.0
        E -= burn
        np.add.at(W.N, reg, burn)
        # ---- starvation: stomach at 0 -> body to skeleton in the voxel it stands in
        starve = (E <= 1e-9) & ~dead
        if starve.any():
            body = np.where(isH, HERB.body, PRED.body)
            np.add.at(W.K, (reg[starve], vox[starve]), body[starve] + np.maximum(E[starve], 0))
            for s in (0, 1):
                self.deaths[s] += int((starve & (sp == s)).sum())
        # ---- births: split at e_birth, offspring at the parent's side
        ebirth = np.where(isH, HERB.e_birth, PRED.e_birth)
        breed = (E >= ebirth) & ~dead & ~starve
        nb = np.flatnonzero(breed)
        self.bloom[:n] = np.minimum(self.bloom[:n] + dt / max(self.P.bloom_s, 1e-6), 1.0)
        self.emerge[:n] = np.minimum(self.emerge[:n] + dt / max(self.P.bloom_s, 1e-6), 1.0)
        if len(nb):
            bs = sp[nb]
            body = np.where(bs == 0, HERB.body, PRED.body)
            e0 = np.where(bs == 0, HERB.e0, PRED.e0)
            E[nb] -= body + e0
            for s in (0, 1):
                self.births[s] += int((bs == s).sum())
            off = pos[nb] + rng.normal(0, 2.0, (len(nb), 3))
            rem = dead | starve
            # removal first (indices stay valid for nb because add appends)
            self.add(off, bs, self.elem[nb].copy(), e0, origin=pos[nb].copy(), bloom=0.0)
            rem = np.concatenate([rem, np.zeros(self.n - n, bool)])
            self.remove(rem)
        else:
            self.remove(dead | starve)

    def _food_gradient(self, p, reg, vox):
        """Direction toward the richest of the 6 neighbouring voxels (crossing into the neighbour region at a
        region face), as a unit vector scaled by the relative improvement."""
        W = self.W
        h = W.vox_h
        best = np.zeros((len(p), 3))
        here = W.F[reg, vox] + W.K[reg, vox]
        bestv = here.copy()
        for d in np.eye(3).tolist() + (-np.eye(3)).tolist():
            q = p + np.array(d) * h
            rq = W.region_of(q)
            ok = rq >= 0
            v = np.zeros(len(p))
            rqo = np.where(ok, rq, 0)
            vq = W.voxel_of(q, rqo)
            v[ok] = (W.F[rqo, vq] + W.K[rqo, vq])[ok]
            bet = v > bestv * 1.05
            best[bet] = d
            bestv[bet] = v[bet]
        gain = np.clip((bestv - here) / np.maximum(here + 10.0, 1e-6), 0, 1)
        return best * (0.3 + gain)[:, None]


def P_sense(P):
    return P.p_sense


def P_catch(P):
    return P.p_catch


def P_flee(P):
    return P.h_flee
