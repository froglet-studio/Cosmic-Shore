"""The cell as regions x voxels, the flora field both LOD levels share, and the mass ledger.

Flora is the WORLD and is never LOD'd: a voxel field (region, voxel) of grazeable volume F plus the skeletons
K that starving fauna leave (§26 - a frame is conserved as ordinary grazeable mass). Only FAUNA change
representation. That is the key architectural decision: the thing a player can damage and the game scores
on (prism volume) has exactly one representation, so expand/collapse cannot desync it.

Mass is CLOSED: every unit of volume is in exactly one bucket
    flora F  +  skeleton K  +  nutrient N (per region)  +  macro bodies + macro stomachs  +  agent bodies + agent stomachs
Metabolism moves stomach -> N. Flora growth moves N -> F (no flora is minted from nothing). Grazing moves
F/K -> stomach. Predation moves prey body+stomach -> predator stomach (overflow above e_max -> N). Birth moves
stomach -> offspring body + stomach. Starvation moves body (+ any residual stomach) -> K at the corpse's voxel.
There is no other flow, and no flow runs on a clock.
"""
from __future__ import annotations

import numpy as np

from .params import Params


class World:
    def __init__(self, P: Params, rng: np.random.Generator, R: float | None = None):
        self.P = P
        self.R = R if R is not None else P.R
        L = P.L
        n = int(np.ceil(2 * self.R / L))
        ax = (np.arange(n) + 0.5) * L - n * L / 2
        cx, cy, cz = np.meshgrid(ax, ax, ax, indexing="ij")
        C = np.stack([cx.ravel(), cy.ravel(), cz.ravel()], 1)
        keep = np.linalg.norm(C, axis=1) <= self.R
        self.grid_n = n
        self.origin = -n * L / 2
        self.lin_to_reg = np.full(n ** 3, -1, np.int64)
        self.centers = C[keep]
        self.lin_to_reg[np.flatnonzero(keep)] = np.arange(keep.sum())
        self.nreg = len(self.centers)
        ijk = np.stack(np.unravel_index(np.flatnonzero(keep), (n, n, n)), 1)
        # face neighbours (-1 = none / outside the membrane)
        nb = np.full((self.nreg, 6), -1, np.int64)
        for k, d in enumerate([(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]):
            q = ijk + np.array(d)
            ok = np.all((q >= 0) & (q < n), axis=1)
            lin = np.ravel_multi_index(q[ok].T, (n, n, n))
            nb[np.flatnonzero(ok), k] = self.lin_to_reg[lin]
        self.nb = nb
        # voxels
        V = P.vox_per_axis
        self.nvox = V ** 3
        h = L / V
        o = (np.arange(V) + 0.5) * h - L / 2
        vx, vy, vz = np.meshgrid(o, o, o, indexing="ij")
        self.vox_off = np.stack([vx.ravel(), vy.ravel(), vz.ravel()], 1)
        self.vox_h = h
        self.F = np.zeros((self.nreg, self.nvox))
        self.K = np.zeros((self.nreg, self.nvox))
        self.N = np.zeros(self.nreg)
        # voxels whose centre lies outside the membrane carry no flora
        vpos = self.centers[:, None, :] + self.vox_off[None]
        self.vox_ok = (np.linalg.norm(vpos, axis=2) <= self.R).astype(float)
        self.rng = rng

    # ---- geometry --------------------------------------------------------------------------------------
    def region_of(self, p: np.ndarray) -> np.ndarray:
        c = np.clip(((p - self.origin) / self.P.L).astype(np.int64), 0, self.grid_n - 1)
        lin = (c[..., 0] * self.grid_n + c[..., 1]) * self.grid_n + c[..., 2]
        return self.lin_to_reg[lin]

    def voxel_of(self, p: np.ndarray, reg: np.ndarray) -> np.ndarray:
        V = self.P.vox_per_axis
        loc = p - self.centers[reg] + self.P.L / 2
        c = np.clip((loc / self.vox_h).astype(np.int64), 0, V - 1)
        return (c[..., 0] * V + c[..., 1]) * V + c[..., 2]

    def seed_flora(self, fill: float, nutrient_per_region: float, patchiness: float = 1.0):
        """Plant flora at `fill` x cap on average, patchy (lognormal), plus a nutrient pool per region."""
        w = self.rng.lognormal(0.0, patchiness, self.F.shape) * self.vox_ok
        w /= max(w.mean(), 1e-9)
        self.F = np.minimum(fill * self.P.flora_cap * w, self.P.flora_cap) * self.vox_ok
        self.N[:] = nutrient_per_region

    # ---- flora + soil (both levels share this; it runs at the macro rate everywhere) -------------------
    def step_flora(self, dt: float):
        P = self.P
        F, K = self.F, self.K
        meanF = F.mean(1, keepdims=True)
        lim = self.N / (self.N + P.nutrient_half)
        room = np.clip(1.0 - (F + K) / P.flora_cap, 0.0, 1.0)
        want = P.flora_r * (F + P.seed_rain * meanF) * room * lim[:, None] * dt * self.vox_ok
        tot = want.sum(1)
        scale = np.where(tot > self.N, self.N / np.maximum(tot, 1e-12), 1.0)
        g = want * scale[:, None]
        self.F = F + g
        self.N = self.N - g.sum(1)
        # soil: nutrient diffuses between face neighbours (pairwise, so exactly conservative)
        if P.nutrient_diffuse > 0:
            for k in (0, 2, 4):
                j = self.nb[:, k]
                i = np.flatnonzero(j >= 0)
                j = j[i]
                flow = P.nutrient_diffuse * dt * (self.N[i] - self.N[j])
                np.add.at(self.N, i, -flow)
                np.add.at(self.N, j, flow)

    def flora_total(self) -> float:
        return float(self.F.sum() + self.K.sum())
