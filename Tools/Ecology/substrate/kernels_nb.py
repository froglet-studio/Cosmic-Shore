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


# ------------------------------------------------------------------------------------------------ fused step
# The Burst-shaped step: ONE parallel-for over agents that does drives + (for the 1/k slice) neighbour read,
# quorum, interest/danger painting and the context choice + integration. Only the hash build is a separate
# pass. Semantics mirror core.py's numpy path term for term (verified statistically in equiv.py).
REG_FIELDS = ("speed", "burst", "turn", "accel", "w_food", "w_coh", "w_align", "w_sep", "w_wander",
              "w_curious", "comfort", "w_flee", "w_hunt", "w_ring", "ring_r", "w_trail", "w_alarm",
              "w_threat", "w_home")
SP_FIELDS = ("metabolism", "fear_gain", "fear_decay", "sense", "curiosity_rate", "q_up", "q_down", "q_width",
             "q_contagion", "q_rate", "dens_norm", "nbr_r", "ring_roles")

if HAVE_NUMBA:
    @nb.njit(cache=True, inline="always")
    def _paint(I, dirs, vx, vy, vz, w):
        n = (vx * vx + vy * vy + vz * vz) ** 0.5
        if n <= 1e-9 or w == 0.0:
            return
        vx /= n; vy /= n; vz /= n
        for d in range(dirs.shape[0]):
            c = vx * dirs[d, 0] + vy * dirs[d, 1] + vz * dirs[d, 2]
            if c > 0.0:
                I[d] += c * w

    @nb.njit(cache=True, inline="always")
    def _paintD(G, dirs, vx, vy, vz, w):
        n = (vx * vx + vy * vy + vz * vz) ** 0.5
        if n <= 1e-9 or w == 0.0:
            return
        vx /= n; vy /= n; vz /= n
        for d in range(dirs.shape[0]):
            c = vx * dirs[d, 0] + vy * dirs[d, 1] + vz * dirs[d, 2]
            if c > 0.0:
                G[d] += c * c * w

    @nb.njit(parallel=True, fastmath=True, cache=True)
    def fused_step(A, pos, vel, idir, ispeed, hunger, fear, curious, aggr, attach, phase, qtarget, wseed, role,
                   Rs, Rg, SPv, dirs, PP, PV, fcell_R, fG, food_g, trail_g, alarm_v, alarm_g, threat_v, threat_g,
                   home, has_home, tick, k, dt, R):
        n = len(A)
        h = SPv[11]
        M = np.int64(2 * R / h) + 4
        # ---- hash build over live agents ----
        key = np.empty(n, np.int64)
        for q in nb.prange(n):
            i = A[q]
            cx = min(max(np.int64(np.floor((pos[i, 0] + R) / h)) + 1, 0), M - 1)
            cy = min(max(np.int64(np.floor((pos[i, 1] + R) / h)) + 1, 0), M - 1)
            cz = min(max(np.int64(np.floor((pos[i, 2] + R) / h)) + 1, 0), M - 1)
            key[q] = (cx * M + cy) * M + cz
        cap = 1
        while cap < 2 * n + 8:
            cap *= 2
        tab, slot = _hash_build(key, cap)
        agg = np.zeros((cap, 8))
        for q in range(n):
            i = A[q]; s = slot[q]
            agg[s, 0] += 1.0
            agg[s, 1] += pos[i, 0]; agg[s, 2] += pos[i, 1]; agg[s, 3] += pos[i, 2]
            agg[s, 4] += vel[i, 0]; agg[s, 5] += vel[i, 1]; agg[s, 6] += vel[i, 2]
            agg[s, 7] += phase[i]
        npil = PP.shape[0]
        fh = 2.0 * fcell_R / fG
        tt = tick * 0.05
        K = dirs.shape[0]
        for q in nb.prange(n):
            i = A[q]
            px, py, pz = pos[i, 0], pos[i, 1], pos[i, 2]
            ph = phase[i]
            # field cell
            fx = min(max(np.int64((px + fcell_R) / fh), 0), fG - 1)
            fy = min(max(np.int64((py + fcell_R) / fh), 0), fG - 1)
            fz = min(max(np.int64((pz + fcell_R) / fh), 0), fG - 1)
            fc = (fx * fG + fy) * fG + fz
            # ---- drives ----
            hu = min(1.0, hunger[i] + SPv[0] * dt); hunger[i] = hu
            pd = 1e9; pj = -1
            for j in range(npil):
                dx = px - PP[j, 0]; dy = py - PP[j, 1]; dz = pz - PP[j, 2]
                d = (dx * dx + dy * dy + dz * dz) ** 0.5
                if d < pd:
                    pd = d; pj = j
            prox = min(max(1.0 - pd / SPv[3], 0.0), 1.0)
            fe = fear[i]
            fe += dt * (SPv[1] * (prox * prox + 0.5 * min(threat_v[fc], 2.0) + min(alarm_v[fc], 2.0))) - dt * SPv[2] * fe
            fe = min(max(fe, 0.0), 1.0); fear[i] = fe
            calm = (1.0 - fe) * (1.0 - hu)
            cu = curious[i] + dt * SPv[4] * (calm * (1.0 - ph) - curious[i]); curious[i] = cu
            capw = min(1.0, (Rs[12] + Rs[13]) + ((Rg[12] + Rg[13]) - (Rs[12] + Rs[13])) * ph)
            ag = min(max(hu * 1.4 - 0.3, 0.0), 1.0) * capw; aggr[i] = ag
            # ---- re-steer the 1/k slice ----
            if (i + tick) % k == 0:
                acc = np.zeros(8)
                k0 = key[q]
                for ddx in range(-1, 2):
                    for ddy in range(-1, 2):
                        for ddz in range(-1, 2):
                            jj = _hash_find(tab, k0 + (ddx * M + ddy) * M + ddz)
                            if jj >= 0:
                                for c in range(8):
                                    acc[c] += agg[jj, c]
                cnt = acc[0] - 1.0
                inv = 1.0 / max(cnt, 1.0)
                cx_ = (acc[1] - px) * inv; cy_ = (acc[2] - py) * inv; cz_ = (acc[3] - pz) * inv
                ax_ = (acc[4] - vel[i, 0]) * inv; ay_ = (acc[5] - vel[i, 1]) * inv; az_ = (acc[6] - vel[i, 2]) * inv
                mph = (acc[7] - ph) * inv if cnt > 0 else ph
                # quorum target
                if SPv[5] < 9.0:
                    s = cnt / SPv[10] * hu
                    th = SPv[6] if qtarget[i] > 0.5 else SPv[5]
                    tg = 1.0 / (1.0 + np.exp(-(s - th) / SPv[7]))
                    c = SPv[8]
                    if cnt > 0:
                        tg = (1 - c) * tg + c * max(tg, mph)
                    qtarget[i] = tg
                W = np.empty(19)
                for f in range(19):
                    W[f] = Rs[f] + (Rg[f] - Rs[f]) * ph
                I = np.zeros(K); G = np.zeros(K)
                _paint(I, dirs, food_g[fc, 0], food_g[fc, 1], food_g[fc, 2], W[4] * hu)
                if cnt > 0:
                    _paint(I, dirs, cx_ - px, cy_ - py, cz_ - pz, W[5] * (1.0 + attach[i]))
                    _paint(I, dirs, ax_, ay_, az_, W[6])
                wx = np.sin(wseed[i, 0] + tt) + 0.6 * np.sin(1.7 * wseed[i, 2] + tt * 2.1)
                wy = np.sin(wseed[i, 1] + tt * 1.3) + 0.6 * np.sin(1.7 * wseed[i, 1] + tt * 2.1)
                wz = np.sin(wseed[i, 2] + tt * 0.7) + 0.6 * np.sin(1.7 * wseed[i, 0] + tt * 2.1)
                _paint(I, dirs, wx, wy, wz, W[8])
                if pj >= 0:
                    tpx = PP[pj, 0] - px; tpy = PP[pj, 1] - py; tpz = PP[pj, 2] - pz
                    near = 1.0 if pd < SPv[3] * 1.5 else 0.0
                    sp_ = min(max((pd - W[10]) / max(W[10], 1.0), -1.0), 1.0)
                    _paint(I, dirs, tpx * sp_, tpy * sp_, tpz * sp_, W[9] * cu * abs(sp_) * near)
                    ld = min(max(pd / 150.0, 0.0), 2.0)
                    _paint(I, dirs, tpx + PV[pj, 0] * ld, tpy + PV[pj, 1] * ld, tpz + PV[pj, 2] * ld, W[12] * ag * near)
                    if SPv[12] > 0:
                        vn = (PV[pj, 0] ** 2 + PV[pj, 1] ** 2 + PV[pj, 2] ** 2) ** 0.5 + 1e-9
                        fx_ = PV[pj, 0] / vn; fy_ = PV[pj, 1] / vn; fz_ = PV[pj, 2] / vn
                        a0 = -fz_ + 1e-6; a1 = 1e-6; a2 = fx_ + 1e-6          # cross(f, up)
                        an = (a0 * a0 + a1 * a1 + a2 * a2) ** 0.5
                        a0 /= an; a1 /= an; a2 /= an
                        b0 = fy_ * a2 - fz_ * a1; b1 = fz_ * a0 - fx_ * a2; b2 = fx_ * a1 - fy_ * a0
                        an_ = 2 * np.pi * role[i] / SPv[12]
                        ca, sa = np.cos(an_), np.sin(an_)
                        sx = PP[pj, 0] + fx_ * 40 + W[14] * (ca * a0 + sa * b0) - px
                        sy = PP[pj, 1] + fy_ * 40 + W[14] * (ca * a1 + sa * b1) - py
                        sz = PP[pj, 2] + fz_ * 40 + W[14] * (ca * a2 + sa * b2) - pz
                        _paint(I, dirs, sx, sy, sz, W[13] * ag * near)
                    _paintD(G, dirs, -tpx, -tpy, -tpz, W[11] * fe * prox)
                _paint(I, dirs, trail_g[fc, 0], trail_g[fc, 1], trail_g[fc, 2], W[15])
                if has_home:
                    _paint(I, dirs, home[0] - px, home[1] - py, home[2] - pz, W[18] * (1.0 - hu))
                r = (px * px + py * py + pz * pz) ** 0.5
                _paint(I, dirs, -px, -py, -pz, min(max((r - 0.8 * R) / (0.15 * R), 0.0), 1.0) * 3.0)
                _paintD(G, dirs, px, py, pz, min(max((r - 0.85 * R) / (0.1 * R), 0.0), 1.0) * 3.0)
                if cnt > 0:
                    sc = cnt / SPv[10] / h
                    sx = (px - (cx_)) * sc; sy = (py - cy_) * sc; sz = (pz - cz_) * sc
                    _paintD(G, dirs, sx, sy, sz, W[7] * min((sx * sx + sy * sy + sz * sz) ** 0.5, 2.0))
                _paintD(G, dirs, -alarm_g[fc, 0], -alarm_g[fc, 1], -alarm_g[fc, 2], W[16] * (0.3 + fe))
                _paintD(G, dirs, -threat_g[fc, 0], -threat_g[fc, 1], -threat_g[fc, 2], W[17] * (0.3 + fe))
                # context choice
                mx = -1e30
                for d in range(K):
                    c = idir[i, 0] * dirs[d, 0] + idir[i, 1] * dirs[d, 1] + idir[i, 2] * dirs[d, 2]
                    if c > 0:
                        I[d] += 0.15 * c
                    e = I[d] * (1.0 - min(max(G[d], 0.0), 1.0)) - 0.25 * max(G[d] - 1.0, 0.0)
                    I[d] = e
                    if e > mx:
                        mx = e
                vx = 0.0; vy = 0.0; vz = 0.0
                if mx > 1e-6:
                    for d in range(K):
                        w = I[d] - 0.75 * mx
                        if w > 0:
                            w *= w
                            vx += w * dirs[d, 0]; vy += w * dirs[d, 1]; vz += w * dirs[d, 2]
                nn = (vx * vx + vy * vy + vz * vz) ** 0.5
                if nn > 1e-9:
                    idir[i, 0] = vx / nn; idir[i, 1] = vy / nn; idir[i, 2] = vz / nn
                urg = max(fe if W[11] > 0 else 0.0, ag)
                ispeed[i] = W[0] * (1.0 + (W[1] - 1.0) * urg)
            # ---- integrate ----
            turn = Rs[2] + (Rg[2] - Rs[2]) * ph
            acl = Rs[3] + (Rg[3] - Rs[3]) * ph
            vs = (vel[i, 0] ** 2 + vel[i, 1] ** 2 + vel[i, 2] ** 2) ** 0.5
            if vs > 1e-6:
                hx = vel[i, 0] / vs; hy = vel[i, 1] / vs; hz = vel[i, 2] / vs
            else:
                hx, hy, hz = idir[i, 0], idir[i, 1], idir[i, 2]
            c = min(max(hx * idir[i, 0] + hy * idir[i, 1] + hz * idir[i, 2], -1.0), 1.0)
            ang = np.arccos(c)
            kk = min(1.0, turn * dt / max(ang, 1e-6))
            nx = hx + (idir[i, 0] - hx) * kk; ny = hy + (idir[i, 1] - hy) * kk; nz = hz + (idir[i, 2] - hz) * kk
            nn = max((nx * nx + ny * ny + nz * nz) ** 0.5, 1e-9)
            ns = vs + min(max(ispeed[i] - vs, -acl * dt), acl * dt)
            vel[i, 0] = nx / nn * ns; vel[i, 1] = ny / nn * ns; vel[i, 2] = nz / nn * ns
            px += vel[i, 0] * dt; py += vel[i, 1] * dt; pz += vel[i, 2] * dt
            r = (px * px + py * py + pz * pz) ** 0.5
            if r > 0.98 * R:
                s = 0.98 * R / r; px *= s; py *= s; pz *= s
            pos[i, 0] = px; pos[i, 1] = py; pos[i, 2] = pz
            phase[i] = ph + dt * SPv[9] * (qtarget[i] - ph)

    @nb.njit(parallel=True, cache=True)
    def eat_query(mpos, apos, h, R):
        """For each agent position, the index (into mpos) of the nearest prism within h, or -1."""
        M = np.int64(2 * R / h) + 4
        m = len(mpos)
        mk = np.empty(m, np.int64)
        for i in range(m):
            cx = min(max(np.int64(np.floor((mpos[i, 0] + R) / h)) + 1, 0), M - 1)
            cy = min(max(np.int64(np.floor((mpos[i, 1] + R) / h)) + 1, 0), M - 1)
            cz = min(max(np.int64(np.floor((mpos[i, 2] + R) / h)) + 1, 0), M - 1)
            mk[i] = (cx * M + cy) * M + cz
        o = np.argsort(mk)
        sk = mk[o]
        out = np.full(len(apos), -1, np.int64)
        for q in nb.prange(len(apos)):
            cx = min(max(np.int64(np.floor((apos[q, 0] + R) / h)) + 1, 0), M - 1)
            cy = min(max(np.int64(np.floor((apos[q, 1] + R) / h)) + 1, 0), M - 1)
            cz = min(max(np.int64(np.floor((apos[q, 2] + R) / h)) + 1, 0), M - 1)
            k0 = (cx * M + cy) * M + cz
            bd = h
            for ddx in range(-1, 2):
                for ddy in range(-1, 2):
                    for ddz in range(-1, 2):
                        kq = k0 + (ddx * M + ddy) * M + ddz
                        j = np.searchsorted(sk, kq)
                        while j < m and sk[j] == kq:
                            mi = o[j]
                            dx = mpos[mi, 0] - apos[q, 0]; dy = mpos[mi, 1] - apos[q, 1]; dz = mpos[mi, 2] - apos[q, 2]
                            d = (dx * dx + dy * dy + dz * dz) ** 0.5
                            if d < bd:
                                bd = d; out[q] = mi
                            j += 1
        return out
