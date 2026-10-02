"""Species F4 - REACTION-DIFFUSION CORAL (Gray-Scott, made mass-conserving) creeping through the grove's volume.

Gray-Scott is   u' = Du lap u - u v^2 + F (1 - u)        v' = Dv lap v + u v^2 - (F + k) v
Its F term injects substrate from nowhere and its F+k term deletes coral into nowhere - both break the locked
mass law. So the substrate's ONLY source here is food: a food prism within reach of living coral (v > `vth`)
DISSOLVES into u at that voxel (an active force: the coral eats it). The kill term becomes RESORPTION, k v moving
coral back into substrate (conserved), and the autocatalytic step u + 2v -> 3v moves substrate into coral. So
c0 * (sum u + sum v) is exactly the coral's mass, and it only grows by eating.

    u, v       per-voxel substrate and coral, in units of c0 volume (one prism's worth)
    Du > Dv    the substrate spreads faster than the coral (the Turing condition): coral FRONTS chase nutrient
               halos around food, leave branching walls behind, and coarsen as resorbed interior feeds the tips
    sting      `sting_mode="dense"` (round 5): living coral denser than `v_danger` is FIRE CORAL - danger, glowing;
               the thinner new growth around it is a soft wall, and the creeping front is the telegraph.
               `sting_mode="front"` (round 4, kept as the negative): only the actively reacting tips (rate u v^2 >
               `sting`) stung - momentary and few, a blind pilot met none.
    walls      coral voxels (v > `vth`) behind the front are plain prisms: a pilot RAMS through them, which
               breaks them (an active force: their mass leaves as cut mass) - so flying through coral carves it
    hearts     the crystals the coral was planted from; only coral CONNECTED to a living heart reacts or
               resorbs. Collecting a heart freezes its colony into an inert skeleton.

Counterplay: out-pace it (a front creeps at ~`front` u/s), cut it (the cutter carves a lane, the coral regrows
into it only if it still has substrate there), or route around the fronts (they glow).
"""
from __future__ import annotations

import math

import numpy as np

from harness import FloraSpecies, PlantBody, VIEW, GROVE_C, GROVE_R, seg_point_dist
from physarum import _blur
from scipy_free import components

DEFAULTS = dict(G=48, n_hearts=10, c0=6.0, plant_vol=12000.0, Du=0.9, Dv=0.25, rate=1.2, k=0.05, vth=0.35,
                sting=0.04, reach=2, eat_per_s=0.5, substeps=2, ram=True, label_every=10, seed_r=1, armour=0.0,
                sting_mode="dense", v_danger=1.1, warmup=600)


def _lap(F, M):
    """6-neighbour Laplacian with ZERO FLUX across the mask boundary: flux flows only between pairs of voxels that
    are both inside M, so the sum over M is conserved exactly (a masked Laplacian that is merely multiplied by M
    leaked 7.6% of the coral's mass through the ball's surface in 60 s - round 2)."""
    L = np.zeros_like(F)
    for ax in range(3):
        a = np.swapaxes(F, 0, ax); m = np.swapaxes(M, 0, ax); l = np.swapaxes(L, 0, ax)
        pair = m[1:] & m[:-1]
        flux = (a[1:] - a[:-1]) * pair
        l[:-1] += flux
        l[1:] -= flux
    return L


class Coral(FloraSpecies):
    name = "coral"

    def __init__(self, arena, params=None):
        self.p = dict(DEFAULTS); self.p.update(params or {}); p = self.p
        self.rng = np.random.default_rng(int(arena.rng.integers(1 << 30)))
        G = self.G = int(p["G"]); self.h = 2 * GROVE_R / G; self.o = GROVE_C - GROVE_R
        c = (np.arange(G) + 0.5) * self.h - GROVE_R
        X, Y, Z = np.meshgrid(c, c, c, indexing="ij")
        self.inside = np.ones((G, G, G), bool)        # the whole cube: a ball mask zeroed seeds/food at its rim
        self.centres = (np.stack([X, Y, Z], -1) + GROVE_C).reshape(-1, 3)
        self.u = np.zeros((G, G, G)); self.v = np.zeros((G, G, G))
        # sown on fertile ground: each heart is planted beside a food prism (where a gardener would plant coral)
        fa = np.flatnonzero(arena.mass_alive)
        pick = self.rng.choice(fa, p["n_hearts"], replace=False)
        self.hearts = arena.mass_pos[pick] + self.rng.normal(0, 10.0, (p["n_hearts"], 3))
        self.heart_alive = np.ones(p["n_hearts"], bool)
        per = p["plant_vol"] / p["c0"] / p["n_hearts"]
        for hpos in self.hearts:
            g = np.clip(((hpos - self.o) / self.h).astype(int), 1, G - 2); r = p["seed_r"]
            sl = tuple(slice(x - r, x + r + 1) for x in g)
            n = self.v[sl].size; self.v[sl] += per / n
        self.living = np.ones((G, G, G), bool)
        self.cut_volume = 0.0; self.crystals = 0; self.deaths = 0; self.leads = []; self.bumps = 0
        self.front_t = np.full(G ** 3, np.inf); self.burn_cd = {}; self.step_k = 0
        self.ever = np.zeros(G ** 3, np.int32)
        self.body = PlantBody(16)          # coral's body IS the field; prisms are a view of it (render only)
        self.rate_f = np.zeros((G, G, G)); self.shield = np.zeros(G ** 3, bool); self.occ_prev = np.zeros(G ** 3, bool)
        self.front = np.zeros(G ** 3, bool)
        class _Quiet:                                    # the grove before you arrive: grow with no pilots present
            pilots = []
        for _ in range(int(p["warmup"])):
            q = _Quiet(); q.t = 0.0; q.mass_alive = arena.mass_alive; q.mass_pos = arena.mass_pos
            q.mass_vol = arena.mass_vol; q.consume = arena.consume
            self.step(q, 0.1)

    def vox(self, P):
        g = np.clip(((P - self.o) / self.h).astype(np.int64), 0, self.G - 1)
        return (g[..., 0] * self.G + g[..., 1]) * self.G + g[..., 2]

    # ---- the reaction-diffusion ---------------------------------------------------------------------------
    def _rd(self, dt):
        """Operator split per substep: diffusion (explicit, stable for D*h*6 < 1), then the reaction and the
        resorption as EXACT exponential transfers - the amount moved is a fraction of what exists, so a step can
        never overshoot, nothing goes negative, and no clamp ever has to invent mass (round 2: explicit Euler at
        rate 10-40 overflowed to NaN and its clamps created 3% mass)."""
        p = self.p; n = p["substeps"]; h = dt / n; L = self.living
        for _ in range(n):
            self.u += h * p["Du"] * _lap(self.u, self.inside)
            self.v += h * p["Dv"] * _lap(self.v, self.inside)
            r = self.u * (1.0 - np.exp(-p["rate"] * self.v * self.v * h)) * L      # u + 2v -> 3v
            back = self.v * (1.0 - np.exp(-p["k"] * h)) * L                       # resorption v -> u
            self.u += back - r; self.v += r - back
            self.rate_f = r / h
    def _eat(self, arena, dt):
        """Living coral eats food within `reach` voxels (a dilation of its mask). The substrate is deposited where
        the FOOD was, so the front then grows OUT toward it - foraging tips. With reach 1 (round 3) the coral could
        only eat what it already touched and froze as compact blobs on the food clumps it was planted beside."""
        fa = np.flatnonzero(arena.mass_alive)
        if not len(fa): return
        M = (self.v > self.p["vth"]) & self.living
        for _ in range(int(self.p["reach"])):
            D = M.copy()
            for ax in range(3):
                D |= np.roll(M, 1, ax) | np.roll(M, -1, ax)
            M = D
        vx = self.vox(arena.mass_pos[fa])
        cand = fa[M.ravel()[vx]]
        pr = self.p["eat_per_s"] * dt
        for j in cand[self.rng.random(len(cand)) < pr]:
            self.u.ravel()[self.vox(arena.mass_pos[j])] += arena.consume(j, "coral") / self.p["c0"]

    def _label(self):
        """Coral connected to a living heart is alive; the rest is skeleton."""
        occ = (self.u + self.v) > 0.05
        lab, nc = components(occ)
        keep = set()
        for k in np.flatnonzero(self.heart_alive):
            l = lab.ravel()[self.vox(self.hearts[k])]
            if l: keep.add(int(l))
        alive = np.isin(lab, list(keep)) if keep else np.zeros_like(occ)
        # substrate-only voxels next to living coral stay reactive, so the front can advance into them
        grow = alive.copy()
        for ax in range(3):
            grow |= np.roll(alive, 1, ax) | np.roll(alive, -1, ax)
        self.living = grow

    # ---- step ---------------------------------------------------------------------------------------------
    def step(self, arena, dt):
        p = self.p
        if self.step_k % p["label_every"] == 0: self._label()
        self.step_k += 1
        self._eat(arena, dt)
        self._rd(dt)
        occ = (self.v > p["vth"]).ravel()
        if p["armour"]:
            new = occ & ~self.occ_prev
            self.shield[new] = self.rng.random(int(new.sum())) < p["armour"]
            self.occ_prev = occ.copy()
        if p["sting_mode"] == "front":
            # round 4 negative: only the actively reacting tips stung; they are momentary and few, so a blind pilot
            # met none (threat 0.01, coverage 0.01, R 0.12 after 5 search steps)
            front = occ & (self.rate_f.ravel() > p["sting"])
        else:
            # FIRE CORAL: living coral denser than v_danger stings; thinner growth is a soft wall you can carve.
            # The danger is persistent and visible, so its lead is how long it has stood there
            front = occ & (self.v.ravel() > p["v_danger"]) & self.living.ravel()
        newf = front & ~np.isfinite(self.front_t); self.front_t[newf] = arena.t
        self.front_t[~front] = np.inf
        self.ever += front
        self.front = front
        for pi in arena.pilots:
            seg = pi.pos - pi.prev; L = np.linalg.norm(seg); n = max(2, int(L / 4.0) + 1)
            vs = np.unique(self.vox(pi.prev + np.linspace(0, 1, n)[:, None] * seg))
            vs = vs[occ[vs]]
            if not len(vs): continue
            hot = vs[front[vs]]
            if len(hot) and self.burn_cd.get(pi.name, -1e9) <= arena.t:
                self.burn_cd[pi.name] = arena.t + 1.0
                self.leads.append(arena.t - float(self.front_t[hot].min()))
                arena.hit(pi, "burn")
            cold = vs[~front[vs]]
            if len(cold):
                self.bumps += len(cold)
                if p["ram"]: self._remove(cold)

    def _remove(self, vs):
        vs = np.asarray(vs, np.int64)
        sh = vs[self.shield[vs]]
        self.shield[sh] = False                          # CHARGE armour: the first hit sheds the shield
        vs = np.setdiff1d(vs, sh)
        u, v = self.u.ravel(), self.v.ravel()
        self.cut_volume += float((u[vs] + v[vs]).sum()) * self.p["c0"]
        u[vs] = 0; v[vs] = 0

    # ---- reader, threats, cutting -------------------------------------------------------------------------
    def hazards(self):
        occ = np.flatnonzero((self.v > self.p["vth"]).ravel())
        fr = np.flatnonzero(getattr(self, "front", np.zeros(self.G ** 3, bool)))
        H = np.concatenate([self.centres[fr], self.centres[occ]])
        R = np.concatenate([np.full(len(fr), self.h * 1.2), np.full(len(occ), self.h * 0.6)])
        W = np.concatenate([np.ones(len(fr)), np.full(len(occ), 0.15)])
        return H, R, W, None

    def threat_elements(self):
        fr = np.flatnonzero(getattr(self, "front", np.zeros(self.G ** 3, bool)))
        return self.centres[fr], np.full(len(fr), self.h * 0.5)

    def cut_targets(self):
        return self.hearts[self.heart_alive]

    def cut(self, arena, pilot, a, b):
        cen = self.centres
        lo = np.clip(((np.minimum(a, b) - 20 - self.o) / self.h).astype(int), 0, self.G - 1)
        hi = np.clip(((np.maximum(a, b) + 20 - self.o) / self.h).astype(int), 0, self.G - 1)
        r = [np.arange(lo[i], hi[i] + 1) for i in range(3)]
        I, J, K = np.meshgrid(*r, indexing="ij")
        vs = ((I * self.G + J) * self.G + K).ravel()
        vs = vs[seg_point_dist(a, b, cen[vs]) < 14.0 + self.h * 0.5]
        occ = (self.v.ravel()[vs] > self.p["vth"])
        fr = getattr(self, "front", np.zeros(self.G ** 3, bool))[vs]
        self._remove(vs[occ & ~fr])                       # the stinging front is not cut, it burns
        d = seg_point_dist(a, b, self.hearts)
        for k in np.flatnonzero(self.heart_alive & (d < 12.0)):
            self.heart_alive[k] = False; self.crystals += 1; self.deaths += 1; self.step_k = 0

    def lane_cut(self, arena, ev):
        return None

    def remove_ball(self, arena, c, r):
        self._remove(np.flatnonzero(np.linalg.norm(self.centres - c, axis=1) < r))

    def mass_total(self):
        return float(self.u.sum() + self.v.sum()) * self.p["c0"]

    def signature(self):
        return (self.v.ravel() > self.p["vth"]).astype(float)

    @property
    def agent_pos(self):
        return self.hearts[self.heart_alive]

    @property
    def agent_vel(self):
        return np.zeros((int(self.heart_alive.sum()), 3))

    @property
    def intent(self):
        return np.zeros(int(self.heart_alive.sum()))

    @property
    def prism_count(self):
        return int((self.v > self.p["vth"]).sum())

    def render(self, out):
        occ = np.flatnonzero((self.v.ravel() > self.p["vth"]))
        fr = getattr(self, "front", np.zeros(self.G ** 3, bool))[occ]
        lv = self.living.ravel()[occ]
        col = np.where(fr[:, None], np.array([[1.0, 0.35, 0.55]]),
                       np.where(lv[:, None], np.array([[0.95, 0.55, 0.45]]) * 0.7, np.array([[0.5, 0.5, 0.5]])))
        size = np.clip(np.cbrt(self.v.ravel()[occ]) * self.h * 0.6, 3, self.h)
        out["coral"] = dict(pos=self.centres[occ], col=col, size=size)
        out["hearts"] = dict(pos=self.hearts[self.heart_alive], col=np.tile([[0.4, 0.7, 1.0]], (int(self.heart_alive.sum()), 1)),
                             size=np.full(int(self.heart_alive.sum()), 10.0))
