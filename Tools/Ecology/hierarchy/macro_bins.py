"""LEGACY (kept as the negative result): the stomach-BIN macro level. Failed the consistency gate (upwind
numerical diffusion); superseded by the cohort model in macro.py. See DISCOVERIES "Hierarchical ecology".

The MACRO level: per-region, energy-structured, integer-count population dynamics.

State per species s (herb, pred):
    n[r, e, b]   int64   individuals in region r, element e, stomach bin b
    pool[r]      float   the population's unassigned stomach (see below)
An individual's stomach is its bin CENTRE; `pool` holds the exact remainder, so the population's stomach is
Σ n*centre + pool EXACTLY. Every quantised change (a move up a bin takes `w` from the pool, a move down
gives `w` back, a death returns the corpse's stomach, a birth settles the offspring's and the parent's
rounding) books its difference against the pool, which is what makes conservation exact while counts stay
integers. The pool is fed back into the next step's per-capita drift, so it mean-reverts to ~0.

This is the Rosenzweig-MacArthur model with physiology:
    grazing   Holling II on voxel flora, with satiation (1 - e/e_max) - herbivores spread over a region's
              voxels in proportion to food ** theta (theta fitted to micro)
    predation Holling II on the region's herbivore count, hunters only below p_hunt_below * e_max
    births    an individual whose stomach crosses e_birth splits off one offspring (cost body + e0)
    deaths    an individual whose stomach falls through 0 starves: body -> skeleton K (no clock anywhere)
    movement  per-neighbour hops by phase, biased toward food / prey and away from predators
All counts move by Binomial / Poisson / Multinomial draws, so a region of 3 grazers behaves like 3 grazers.
"""
from __future__ import annotations

import numpy as np

from .params import Params, Species, HERB, PRED

NE = 4


class MacroPop:
    def __init__(self, sp: Species, nreg: int, P: Params):
        self.sp = sp
        B = P.n_bins
        self.B = B
        self.w = sp.e_birth / B
        self.centre = (np.arange(B) + 0.5) * self.w
        self.n = np.zeros((nreg, NE, B), np.int64)
        self.pool = np.zeros(nreg)
        sat = np.clip(1.0 - self.centre / sp.e_max, 0.0, 1.0)
        self.sat = sat
        f = self.centre / sp.e_birth
        self.phase_of_bin = np.where(f < sp.phase_lo, 2, np.where(f > sp.phase_hi, 0, 1))  # 0 sated 1 forage 2 hungry

    def bin_of(self, e):
        return np.clip((np.asarray(e) / self.w).astype(np.int64), 0, self.B - 1)

    # ---- totals ----------------------------------------------------------------------------------------
    def count(self):
        return self.n.sum(axis=(1, 2))

    def stomach(self):
        return (self.n * self.centre).sum(axis=(1, 2)) + self.pool

    def mass(self):
        return float(self.stomach().sum() + self.n.sum() * self.sp.body)

    def place(self, r, e, energy, k=1):
        """Add k individuals of element e with stomach `energy` each to region(s) r. Returns nothing; the
        rounding (energy - centre) is booked to the pool so the population's stomach is exact."""
        b = self.bin_of(energy)
        np.add.at(self.n, (r, e, b), k)
        np.add.at(self.pool, r, k * (np.asarray(energy, float) - self.centre[b]))


class Macro:
    def __init__(self, world, P: Params, rng: np.random.Generator):
        self.W, self.P, self.rng = world, P, rng
        self.H = MacroPop(HERB, world.nreg, P)
        self.Pr = MacroPop(PRED, world.nreg, P)
        self.pops = (self.H, self.Pr)
        self.theta = P.theta                   # occupancy exponent (fitted: micro herbivores do NOT pile onto food)
        # arrivals into HOT regions (the LOD manager turns them into agents): list of (sp, dst, src, e, energy)
        self.inbox = []
        # telemetry
        self.kills = 0
        self.births = np.zeros(2, np.int64)
        self.deaths = np.zeros(2, np.int64)
        self.hops = 0

    # ---- occupancy: how a region's herbivores spread over its voxels ------------------------------------
    def occupancy(self):
        G = self.W.F + self.W.K
        w = np.power(np.maximum(G, 0.0), self.theta) * self.W.vox_ok
        s = w.sum(1, keepdims=True)
        uni = self.W.vox_ok / np.maximum(self.W.vox_ok.sum(1, keepdims=True), 1)
        return np.where(s > 1e-9, w / np.maximum(s, 1e-12), uni)

    # ---- one macro step over the COLD regions ------------------------------------------------------------
    def step(self, dt: float, cold: np.ndarray, hot: np.ndarray):
        W, P, rng = self.W, self.P, self.rng
        gainH = self._graze(dt, cold)
        gainP = self._predate(dt, cold)
        for k, (pop, gain) in enumerate(((self.H, gainH), (self.Pr, gainP))):
            self._energetics(k, pop, gain, dt, cold)
        self._move(dt, cold, hot)

    def _graze(self, dt, cold):
        W, P, H = self.W, self.P, self.H
        n = H.n[cold]                                   # (c, E, B)
        eff = (n * H.sat).sum(axis=(1, 2)).astype(float)  # satiation-weighted headcount
        G = W.F[cold] + W.K[cold]
        occ = self.occupancy()[cold]
        g = G / (G + P.h_half)
        want = eff[:, None] * occ * P.h_intake * g * dt
        if P.bug == "macro_graze_x1.5":
            want = want * 1.5
        took = np.minimum(want, G)
        fracF = np.where(G > 0, W.F[cold] / np.maximum(G, 1e-12), 0.0)
        W.F[cold] -= took * fracF
        W.K[cold] -= took * (1 - fracF)
        tot = took.sum(1)
        gain = np.zeros(W.nreg)
        gain[cold] = tot
        return gain                                       # booked to the herbivore pool below

    def _predate(self, dt, cold):
        P, H, Pr, rng = self.P, self.H, self.Pr, self.rng
        gain = np.zeros(self.W.nreg)
        hunt = Pr.centre < P.p_hunt_below * Pr.sp.e_max     # (B,)
        hunters = (Pr.n[:, :, hunt]).sum(axis=(1, 2)) * cold
        nH = H.count() * cold
        rate = hunters * P.p_attack * nH / (1.0 + P.p_attack * P.p_handle * nH) * dt
        k = np.minimum(rng.poisson(rate), nH)
        for r in np.flatnonzero(k):
            kk = int(k[r])
            # victims: uniform over the region's herbivores
            flat = H.n[r].ravel()
            vic = rng.multivariate_hypergeometric(flat, kk)
            H.n[r] -= vic.reshape(H.n[r].shape)
            vb = vic.reshape(H.n[r].shape).sum(0)
            meal = float(kk * H.sp.body + (vb * H.centre).sum())
            # the victims' share of the herbivore pool goes with them (their stomachs were centre + share)
            tot_before = flat.sum()
            share = H.pool[r] * kk / max(tot_before, 1)
            H.pool[r] -= share
            meal += share
            # killers: hunters chosen uniformly; each moves up by round(meal_per_kill / w) bins
            hn = Pr.n[r][:, hunt].ravel()
            ks = rng.multivariate_hypergeometric(hn, min(kk, int(hn.sum())))
            ks = ks.reshape(NE, hunt.sum())
            hb = np.flatnonzero(hunt)
            per = meal / max(kk, 1)
            # move killers: centre + per -> new bin; rounding to pool
            for e in range(NE):
                for j, b in enumerate(hb):
                    c = int(ks[e, j])
                    if c == 0:
                        continue
                    Pr.n[r, e, b] -= c
                    newE = Pr.centre[b] + per
                    nb = min(int(newE / Pr.w), Pr.B - 1)
                    Pr.n[r, e, nb] += c
                    Pr.pool[r] += c * (newE - Pr.centre[nb])
            # meal not handed to a killer (more kills than hunters, rare) also goes to the pool
            Pr.pool[r] += meal - per * ks.sum()
            self.kills += kk
        return gain

    def _energetics(self, k, pop, gain, dt, cold):
        """Grazing gain + metabolism into the pool, then quantised bin moves, births at the top, starvation
        at the bottom - all with the remainder booked to the pool."""
        W, rng, sp, w = self.W, self.rng, pop.sp, pop.w
        idx = np.flatnonzero(cold)
        if len(idx) == 0:
            return
        n = pop.n[idx]                                   # (c,E,B) view copy
        ntot = n.sum(axis=(1, 2))
        # metabolism: every individual burns m*dt (capped by what the population holds) -> nutrient
        burn = np.minimum(ntot * sp.metab * dt, np.maximum(pop.stomach()[idx], 0.0))
        pop.pool[idx] += gain[idx] - burn
        W.N[idx] += burn
        # per-capita drift by bin: gain shared by satiation, minus metabolism, plus the pool's mean share
        eff = (n * pop.sat).sum(axis=(1, 2))
        gshare = np.where(eff > 0, gain[idx] / np.maximum(eff, 1e-12), 0.0)
        drift = gshare[:, None] * pop.sat[None] - (burn / np.maximum(ntot, 1))[:, None]  # (c,B)
        # feed back the PRIOR residual (pool before this step's gain - burn): what the bins still owe / are owed
        prior = pop.pool[idx] - (gain[idx] - burn)
        drift = drift + (prior / np.maximum(ntot, 1))[:, None]
        pup = np.clip(drift / w, 0, 1)[:, None, :] * np.ones((1, NE, 1))
        pdn = np.clip(-drift / w, 0, 1)[:, None, :] * np.ones((1, NE, 1))
        up = rng.binomial(n, pup)
        dn = rng.binomial(n - up, pdn / np.maximum(1 - pup, 1e-12) * (pup < 1))
        newn = n - up - dn
        # shifts
        newn[:, :, 1:] += up[:, :, :-1]
        newn[:, :, :-1] += dn[:, :, 1:]
        pool = pop.pool[idx] - w * up.sum(axis=(1, 2)) + w * dn.sum(axis=(1, 2))
        # births: up-moves out of the top bin. stomach centre_top + w -> parent + offspring + body
        bt = up[:, :, -1]                                 # (c,E)
        nbirth = bt.sum(1)
        if nbirth.any():
            e_cross = pop.centre[-1] + w
            pe = e_cross - sp.body - sp.e0
            pb = min(int(pe / w), pop.B - 1)
            ob = min(int(sp.e0 / w), pop.B - 1)
            newn[:, :, pb] += bt
            newn[:, :, ob] += bt
            pool += nbirth * ((pe - pop.centre[pb]) + (sp.e0 - pop.centre[ob]))
            self.births[k] += int(nbirth.sum())
        # deaths: down-moves out of bin 0 starve. they gave back w; the corpse's stomach is 0, so the pool
        # pays the difference (centre0 - w = -w/2: a starving individual had w/2 left and spent it)
        dd = dn[:, :, 0]
        ndead = dd.sum(1)
        if ndead.any():
            pool += ndead * (pop.centre[0] - w)
            # body -> skeleton, spread over the region's voxels by occupancy
            occ = self.occupancy()[idx]
            W.K[idx] += (ndead * sp.body)[:, None] * occ
            self.deaths[k] += int(ndead.sum())
        pop.n[idx] = newn
        # an emptied region's pool settles to the soil (positive or negative - the ledger stays exact)
        empty = newn.sum(axis=(1, 2)) == 0
        W.N[idx[empty]] += pool[empty]
        pool[empty] = 0.0
        pop.pool[idx] = pool

    def _move(self, dt, cold, hot):
        """Regional hops. Hops out of cold regions only (a hot region has no macro population). A hop INTO a
        hot region goes to the inbox (the LOD manager makes it an agent walking in from the source's cloud)."""
        W, P, rng = self.W, self.P, self.rng
        G = (W.F + W.K).sum(1)
        nH = self.H.count().astype(float)
        nP = self.Pr.count().astype(float)
        nb = W.nb
        valid = nb >= 0
        nbs = np.where(valid, nb, 0)
        for k, pop in enumerate(self.pops):
            rates = np.asarray(P.hop_rate[pop.sp.name])[pop.phase_of_bin]   # (B,)
            if k == 0:
                own = G + 1.0
                w = np.power((G[nbs] + 1.0) / own[:, None], P.food_bias) * \
                    np.power((nP[:, None] + 1.0) / (nP[nbs] + 1.0), P.flee_bias)
            else:
                own = nH + 1.0
                w = np.power((nH[nbs] + 1.0) / own[:, None], P.prey_bias)
            w = np.where(valid, w, 0.0)
            w = w / np.maximum(w.sum(1, keepdims=True) / np.maximum(valid.sum(1, keepdims=True), 1), 1e-12)
            # per-neighbour probability, bin-dependent: p[r,d,b]
            p = np.clip(w[:, :, None] * rates[None, None, :] * dt, 0, None)
            s = p.sum(1)
            over = s > 0.9
            if over.any():
                p = p / np.where(over, s / 0.9, 1.0)[:, None, :]
                s = p.sum(1)
            idx = np.flatnonzero(cold & (pop.count() > 0))
            if len(idx) == 0:
                continue
            pr = np.concatenate([np.transpose(p[idx], (0, 2, 1)), (1 - s[idx])[:, :, None]], 2)   # (c,B,7)
            pv = np.broadcast_to(pr[:, None], (len(idx), NE) + pr.shape[1:])
            m = rng.multinomial(pop.n[idx], pv)[..., :6]          # (c,E,B,6)
            per_r = m.sum(axis=(1, 2, 3))
            ntot0 = pop.n[idx].sum(axis=(1, 2))
            share = pop.pool[idx] / np.maximum(ntot0, 1)
            pop.n[idx] -= m.sum(3)
            pop.pool[idx] -= share * per_r
            self.hops += int(per_r.sum())
            for d in range(6):
                md = m[..., d]                                     # (c,E,B)
                cnt = md.sum(axis=(1, 2))
                sel = cnt > 0
                if not sel.any():
                    continue
                src = idx[sel]
                dst = nb[src, d]
                mdd = md[sel]
                toh = hot[dst]
                if (~toh).any():
                    np.add.at(pop.n, dst[~toh], mdd[~toh])
                    np.add.at(pop.pool, dst[~toh], (share[sel] * cnt[sel])[~toh])
                for j in np.flatnonzero(toh):
                    for e, b in zip(*np.nonzero(mdd[j])):
                        for _ in range(int(mdd[j][e, b])):
                            self.inbox.append((k, int(dst[j]), int(src[j]), int(e),
                                               float(pop.centre[b] + share[sel][j])))

    # ---- macro telemetry -------------------------------------------------------------------------------
    def phase_counts(self, k):
        pop = self.pops[k]
        by_bin = pop.n.sum(axis=(0, 1))
        return np.bincount(pop.phase_of_bin, weights=by_bin, minlength=3)

    def mass(self):
        inbox = sum((self.pops[s].sp.body + e) for (s, _d, _r, _el, e) in self.inbox)
        return self.H.mass() + self.Pr.mass() + inbox

    def inbox_count(self):
        return len(self.inbox)
