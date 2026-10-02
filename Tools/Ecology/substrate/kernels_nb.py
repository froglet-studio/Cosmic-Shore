"""numba kernels for the substrate's two hot stages - the same semantics as core.py's numpy path, written
the way a Burst IJobParallelFor would be: flat arrays, one agent per iteration, no allocation in the loop.

neighbours(): an open-addressing hash of occupied cells (cell size = neighbour radius) accumulating per-cell
MOMENTS (count, sum pos, sum vel, sum phase), then for each re-steering agent a 27-cell read. O(N) build,
O(N/k) read. (The numpy path does the same with np.unique + searchsorted; results match to float rounding.)

context_choose(): interest/danger painted over D directions, soft danger mask, soft-argmax refinement.
"""
from __future__ import annotations

import numpy as np

try:
    import numba as nb
    HAVE_NUMBA = True
except Exception:  # pragma: no cover
    HAVE_NUMBA = False

if HAVE_NUMBA:
    @nb.njit(cache=True)
    def _hash_build(key, cap):
        tab = np.full(cap, -1, np.int64)       # stored key
        slot = np.empty(len(key), np.int64)
        mask = cap - 1
        for i in range(len(key)):
            k = key[i]
            hh = (k * 0x9E3779B97F4A7C15) & 0x7FFFFFFFFFFFFFFF
            j = hh & mask
            while tab[j] != -1 and tab[j] != k:
                j = (j + 1) & mask
            tab[j] = k
            slot[i] = j
        return tab, slot

    @nb.njit(cache=True, inline="always")
    def _hash_find(tab, k):
        mask = len(tab) - 1
        hh = (k * 0x9E3779B97F4A7C15) & 0x7FFFFFFFFFFFFFFF
        j = hh & mask
        while tab[j] != -1:
            if tab[j] == k:
                return j
            j = (j + 1) & mask
        return -1

    @nb.njit(parallel=True, fastmath=True, cache=True)
    def _nbr_kernel(pos, vel, ph, sl, h, R, dens_dummy):
        n = len(pos)
        M = np.int64(2 * R / h) + 4
        key = np.empty(n, np.int64)
        for i in nb.prange(n):
            cx = min(max(np.int64(np.floor((pos[i, 0] + R) / h)) + 1, 0), M - 1)
            cy = min(max(np.int64(np.floor((pos[i, 1] + R) / h)) + 1, 0), M - 1)
            cz = min(max(np.int64(np.floor((pos[i, 2] + R) / h)) + 1, 0), M - 1)
            key[i] = (cx * M + cy) * M + cz
        cap = 1
        while cap < 2 * n + 8:
            cap *= 2
        tab, slot = _hash_build(key, cap)
        agg = np.zeros((cap, 8))
        for i in range(n):                   # reduction (a Burst port uses per-thread partials or a sort)
            s = slot[i]
            agg[s, 0] += 1.0
            agg[s, 1] += pos[i, 0]; agg[s, 2] += pos[i, 1]; agg[s, 3] += pos[i, 2]
            agg[s, 4] += vel[i, 0]; agg[s, 5] += vel[i, 1]; agg[s, 6] += vel[i, 2]
            agg[s, 7] += ph[i]
        m = len(sl)
        out = np.zeros((m, 8))
        for q in nb.prange(m):
            i = sl[q]
            k0 = key[i]
            acc = np.zeros(8)
            for dx in range(-1, 2):
                for dy in range(-1, 2):
                    for dz in range(-1, 2):
                        j = _hash_find(tab, k0 + (dx * M + dy) * M + dz)
                        if j >= 0:
                            for c in range(8):
                                acc[c] += agg[j, c]
            acc[0] -= 1.0
            acc[1] -= pos[i, 0]; acc[2] -= pos[i, 1]; acc[3] -= pos[i, 2]
            acc[4] -= vel[i, 0]; acc[5] -= vel[i, 1]; acc[6] -= vel[i, 2]
            acc[7] -= ph[i]
            for c in range(8):
                out[q, c] = acc[c]
        return out

    @nb.njit(parallel=True, fastmath=True, cache=True)
    def _context_kernel(T, Wt, D, Wd, dirs, cur):
        m = T.shape[0]; nt = T.shape[1]; nd = D.shape[1]; K = dirs.shape[0]
        out = np.empty((m, 3))
        for i in nb.prange(m):
            Ie = np.empty(K)
            mx = -1e30
            for d in range(K):
                dx, dy, dz = dirs[d, 0], dirs[d, 1], dirs[d, 2]
                I = 0.0
                for t in range(nt):
                    w = Wt[i, t]
                    if w != 0.0:
                        c = T[i, t, 0] * dx + T[i, t, 1] * dy + T[i, t, 2] * dz
                        if c > 0.0:
                            I += c * w
                c = cur[i, 0] * dx + cur[i, 1] * dy + cur[i, 2] * dz
                if c > 0.0:
                    I += 0.15 * c
                G = 0.0
                for t in range(nd):
                    w = Wd[i, t]
                    if w != 0.0:
                        c = D[i, t, 0] * dx + D[i, t, 1] * dy + D[i, t, 2] * dz
                        if c > 0.0:
                            G += c * c * w
                e = I * (1.0 - min(max(G, 0.0), 1.0)) - 0.25 * max(G - 1.0, 0.0)
                Ie[d] = e
                if e > mx:
                    mx = e
            vx = 0.0; vy = 0.0; vz = 0.0
            if mx > 1e-6:
                for d in range(K):
                    w = Ie[d] - 0.75 * mx
                    if w > 0.0:
                        w = w * w
                        vx += w * dirs[d, 0]; vy += w * dirs[d, 1]; vz += w * dirs[d, 2]
            nn = (vx * vx + vy * vy + vz * vz) ** 0.5
            if nn > 1e-9:
                out[i, 0] = vx / nn; out[i, 1] = vy / nn; out[i, 2] = vz / nn
            else:
                out[i, 0] = cur[i, 0]; out[i, 1] = cur[i, 1]; out[i, 2] = cur[i, 2]
        return out


def neighbours(pos, vel, ph, sl, h, R, dens_norm=6.0):
    o = _nbr_kernel(np.ascontiguousarray(pos), np.ascontiguousarray(vel), np.ascontiguousarray(ph), sl,
                    float(h), float(R), 0.0)
    n_c = o[:, 0]
    inv = 1.0 / np.maximum(n_c, 1)
    has = n_c > 0
    p = pos[sl]
    cen = o[:, 1:4] * inv[:, None]
    coh = np.where(has[:, None], cen - p, 0.0)
    align = np.where(has[:, None], o[:, 4:7] * inv[:, None], 0.0)
    return dict(count=n_c, coh=coh, align=align, _cen=cen, _has=has, mphase=np.where(has, o[:, 7] * inv, ph[sl]))


def context_choose(T, Wt, D, Wd, dirs, cur):
    return _context_kernel(np.ascontiguousarray(T), np.ascontiguousarray(Wt), np.ascontiguousarray(D),
                           np.ascontiguousarray(Wd), np.ascontiguousarray(dirs), np.ascontiguousarray(cur))
