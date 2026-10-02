"""The MACRO level: per-region COHORT population dynamics (an Escalator Boxcar Train).

Why cohorts and not stomach bins: individuals born together, eating the same regional food, stay together -
a micro generation breeds within ~60 s of itself. The first macro level (macro_bins.py, kept) was an Eulerian
stomach-bin model with upwind fluxes, and it failed the consistency gate by a phase-mix TV distance of 0.37:
upwind transport on 8 bins diffuses stomach far faster than real individuals diffuse (32 bins: still 0.27).
A cohort is Lagrangian - it MOVES along the stomach axis - so it carries no numerical diffusion at all.

State per species, per region r, per cohort slot c (C slots):
    N[r,c,e]  int64   individuals of element e
    S[r,c]    float   Σ stomach of the cohort      (exact - this is what conserves mass)
    Q[r,c]    float   Σ stomach² of the cohort     (the cohort's spread: var = Q/N - (S/N)²)
All three moments are ADDITIVE, so merging cohorts, moving individuals, births and kills are plain additions
and subtractions - conservation is exact by construction, and any number of arrivals can merge into one slot
in one vectorised `np.add.at`.

Within a cohort the stomach is taken as Normal(mean, var). Births are the upper tail past e_birth (they leave at
the truncated-normal tail mean and split into parent + offspring), starvation is the lower tail past 0 (the
corpse's body goes to skeleton; the tail's stomach stays with the survivors, exactly). Grazing is Holling II on the
shared flora voxels with satiation (1 - e/e_max); predation is Holling II on the region's herbivore count with
rates FITTED to the micro level. Movement is per-neighbour hops by phase. Every count moves by a Binomial /
Poisson / hypergeometric / multinomial draw - a region of 3 grazers behaves like 3 grazers.
"""
from __future__ import annotations

import numpy as np
from scipy.special import ndtr

from .params import Params, Species, HERB, PRED

NE = 4
SQ2PI = 1.0 / np.sqrt(2 * np.pi)


def _phi(x):
    return SQ2PI * np.exp(-0.5 * x * x)


def tail_moments(m, sd, cut, upper):
    """Fraction, mean and second moment of Normal(m, sd) beyond `cut` (upper=True: x > cut)."""
    sd = np.maximum(sd, 1e-6)
    with np.errstate(over="ignore", invalid="ignore", divide="ignore"):
        return _tail(m, sd, cut, upper)


def _tail(m, sd, cut, upper):
    a = (cut - m) / sd
    if upper:
        p = 1.0 - ndtr(a)
        lam = _phi(a) / np.maximum(p, 1e-300)
        mu = m + sd * lam
        var = sd * sd * (1 + a * lam - lam * lam)
    else:
        p = ndtr(a)
        lam = _phi(a) / np.maximum(p, 1e-300)
        mu = m - sd * lam
        var = sd * sd * (1 - a * lam - lam * lam)
    var = np.maximum(var, 0.0)
    far = (p < 1e-12) | ~np.isfinite(mu) | ~np.isfinite(var)   # deep tails lose precision; pin to the cut
    mu = np.where(far, cut, mu)
    var = np.where(far, 0.0, var)
    return p, mu, mu * mu + var


class MacroPop:
    def __init__(self, sp: Species, nreg: int, P: Params, C: int):
        self.sp, self.P, self.C = sp, P, C
        self.N = np.zeros((nreg, C, NE), np.int64)
        self.S = np.zeros((nreg, C))
        self.Q = np.zeros((nreg, C))
        self.merge_tol = P.merge_tol * sp.e_birth

    # ---- moments -----------------------------------------------------------------------------------------
    def n_rc(self):
        return self.N.sum(2)

    def mean_var(self):
        n = self.n_rc().astype(float)
        m = np.where(n > 0, self.S / np.maximum(n, 1), 0.0)
        v = np.where(n > 0, self.Q / np.maximum(n, 1) - m * m, 0.0)
        return n, m, np.maximum(v, 0.0)

    def count(self):
        return self.N.sum(axis=(1, 2))

    def stomach(self):
        return self.S.sum(1)

    def mass(self):
        return float(self.S.sum() + self.N.sum() * self.sp.body)

    def phase_counts(self, sel=None):
        """(sated, forage, hungry) expected counts from each cohort's Normal."""
        n, m, v = self.mean_var()
        if sel is not None:
            n, m, v = n[sel], m[sel], v[sel]
        sd = np.sqrt(v) + 1e-6
        lo, hi = self.sp.phase_lo * self.sp.e_birth, self.sp.phase_hi * self.sp.e_birth
        f_lo = ndtr((lo - m) / sd)
        f_hi = 1 - ndtr((hi - m) / sd)
        return np.array([(n * f_hi).sum(), (n * (1 - f_lo - f_hi)).sum(), (n * f_lo).sum()])

    def elem_counts(self, sel=None):
        N = self.N if sel is None else self.N[sel]
        return N.sum(axis=(0, 1))

    # ---- insertion: a batch of new cohorts (r, Ne[4], S, Q) merged / slotted ------------------------------
    def insert(self, r, Ne, S, Q):
        r = np.asarray(r, np.int64)
        if len(r) == 0:
            return
        Ne = np.asarray(Ne, np.int64).reshape(len(r), NE)
        S = np.asarray(S, float).reshape(-1); Q = np.asarray(Q, float).reshape(-1)
        k = Ne.sum(1)
        ok = k > 0
        r, Ne, S, Q, k = r[ok], Ne[ok], S[ok], Q[ok], k[ok]
        if len(r) == 0:
            return
        m_new = S / k
        n, m, _ = self.mean_var()
        d = np.abs(m[r] - m_new[:, None])
        d = np.where(n[r] > 0, d, np.inf)
        near = np.argmin(d, 1)
        dist = d[np.arange(len(r)), near]
        need = ~(dist <= self.merge_tol)
        tgt = near.copy()
        if need.any():
            # hand out free slots in order within each region (vectorised rank-within-region)
            ri = r[need]
            order = np.argsort(ri, kind="stable")
            ri_s = ri[order]
            first = np.r_[0, np.flatnonzero(np.diff(ri_s)) + 1]
            rank = np.arange(len(ri_s)) - np.repeat(first, np.diff(np.r_[first, len(ri_s)]))
            free = n[ri_s] == 0                                    # (k, C)
            cf = np.cumsum(free, 1)
            slot = np.argmax((cf == (rank + 1)[:, None]) & free, 1)
            has = cf[:, -1] > rank
            slot = np.where(has, slot, near[need][order])          # no free slot left: merge into nearest
            t2 = np.empty_like(slot); t2[order] = slot
            tgt[need] = t2
        np.add.at(self.N, (r, tgt), Ne)
        np.add.at(self.S, (r, tgt), S)
        np.add.at(self.Q, (r, tgt), Q)
        self.compact()

    def compact(self, min_free=2):
        """Keep >= min_free free slots per region by merging the two closest cohorts (additive moments)."""
        for _ in range(6):
            n, m, _ = self.mean_var()
            free = (n == 0).sum(1)
            rr = np.flatnonzero(free < min_free)
            if len(rr) == 0:
                return
            mm = np.where(n[rr] > 0, m[rr], np.inf)
            o = np.argsort(mm, 1)
            ms = np.take_along_axis(mm, o, 1)
            gap = np.diff(ms, axis=1)
            j = np.argmin(gap, 1)
            a = o[np.arange(len(rr)), j]; b = o[np.arange(len(rr)), j + 1]
            self.N[rr, a] += self.N[rr, b]; self.S[rr, a] += self.S[rr, b]; self.Q[rr, a] += self.Q[rr, b]
            self.N[rr, b] = 0; self.S[rr, b] = 0.0; self.Q[rr, b] = 0.0

    def place(self, r, e, energy, k=1):
        """Add individuals (one zero-spread cohort each, merged where close) - used by absorb and seeding."""
        r = np.atleast_1d(np.asarray(r, np.int64)); e = np.atleast_1d(np.asarray(e, np.int64))
        energy = np.broadcast_to(np.asarray(energy, float), r.shape).astype(float)
        Ne = np.zeros((len(r), NE), np.int64); Ne[np.arange(len(r)), e] = k
        self.insert(r, Ne, energy * k, energy * energy * k)

    def take(self, r, c, k_e, mu, mu2):
        """Remove individuals k_e[...,4] from cohorts (r, c) carrying stomach mean mu / second moment mu2 each."""
        k = k_e.sum(-1)
        np.subtract.at(self.N, (r, c), k_e)
        np.subtract.at(self.S, (r, c), k * mu)
        np.subtract.at(self.Q, (r, c), k * mu2)

    def tidy(self):
        """An empty cohort must carry exactly nothing: its residual stomach (rounding) is returned for the soil."""
        n = self.n_rc()
        z = n == 0
        resid = self.S * z
        self.S[z] = 0.0; self.Q[z] = 0.0
        self.Q = np.maximum(self.Q, self.S * self.S / np.maximum(n, 1))
        return resid.sum(1)


class Macro:
    def __init__(self, world, P: Params, rng: np.random.Generator):
        self.W, self.P, self.rng = world, P, rng
        C = P.cohorts
        self.H = MacroPop(HERB, world.nreg, P, C)
        self.Pr = MacroPop(PRED, world.nreg, P, C)
        self.pops = (self.H, self.Pr)
        self.occ = self.occ_target_init(world)
        self.inbox = []                    # arrivals into HOT regions: (sp, dst, src, elem, stomach)
        self.kills = 0
        self.births = np.zeros(2, np.int64)
        self.deaths = np.zeros(2, np.int64)
        self.hops = 0
        self.settled = 0.0                 # |stomach| of emptied cohorts settled to soil (rounding valve, ~0)

    def occ_target(self):
        G = self.W.F + self.W.K
        w = np.power(np.maximum(G, 0.0), self.P.occ_theta) * self.W.vox_ok
        s = w.sum(1, keepdims=True)
        uni = self.W.vox_ok / np.maximum(self.W.vox_ok.sum(1, keepdims=True), 1)
        return np.where(s > 1e-9, w / np.maximum(s, 1e-12), uni)

    def occupancy(self):
        """Where a region's grazers stand, over its voxels: a STATE that relaxes toward food ** occ_theta with time
        constant occ_tau (both fitted to micro). Grazers deplete where they stand, so a lagging field reproduces the
        anti-correlation a static exponent cannot (theta 0.5 fits a fresh field, 0 a grazed one)."""
        return self.occ

    def relax_occupancy(self, dt):
        k = min(1.0, dt / max(self.P.occ_tau, 1e-6))
        self.occ += (self.occ_target() - self.occ) * k
        self.occ *= self.W.vox_ok
        self.occ /= np.maximum(self.occ.sum(1, keepdims=True), 1e-12)

    def absorb_occupancy(self, r, vox, w=1.0):
        """Fold absorbed agents' voxels into the occupancy field (their positions are the best evidence)."""
        if len(r) == 0:
            return
        cnt = np.zeros_like(self.occ)
        np.add.at(cnt, (r, vox), w)
        rr = np.unique(r)
        nH = self.H.count()[rr].astype(float)
        add = cnt[rr]
        tot = add.sum(1, keepdims=True)
        mix = tot / np.maximum(tot + nH[:, None], 1e-9)
        self.occ[rr] = self.occ[rr] * (1 - mix) + add / np.maximum(tot, 1e-9) * mix

    @staticmethod
    def occ_target_init(W):
        return W.vox_ok / np.maximum(W.vox_ok.sum(1, keepdims=True), 1)

    def mass(self):
        inbox = sum((self.pops[s].sp.body + e) for (s, _d, _r, _el, e) in self.inbox)
        return self.H.mass() + self.Pr.mass() + inbox

    def inbox_count(self):
        return len(self.inbox)

    def settle_empty(self):
        for pop in self.pops:
            res = pop.tidy()
            self.W.N += res
            self.settled += float(np.abs(res).sum())

    # ---- one macro step over the COLD regions ------------------------------------------------------------
    def step(self, dt, cold, hot):
        self.relax_occupancy(dt)
        _, betaH = self._graze(dt, cold)
        self._predate(dt, cold)
        self._energetics(0, self.H, betaH, dt, cold)
        self._energetics(1, self.Pr, np.zeros_like(betaH), dt, cold)
        self._move(dt, cold, hot)
        self.settle_empty()

    def _graze(self, dt, cold):
        """Grazing: per-capita intake = sat(cohort mean) * f, f = the region's food per unit satiation.
        Returns the per-individual gain RATE (R, C) and its slope d(gain)/d(stomach) (for the spread update)."""
        W, P, H = self.W, self.P, self.H
        n, m, v = H.mean_var()
        sat = np.clip(1.0 - m / H.sp.e_max, 0.0, 1.0)
        eff = (n * sat).sum(1)
        G = W.F + W.K
        occ = self.occupancy()
        g = G / (G + P.h_half)
        want = eff[:, None] * occ * P.h_intake * g * dt
        if P.bug == "macro_graze_x1.5":
            want = want * 1.5
        want[~cold] = 0.0
        took = np.minimum(want, G)
        fracF = np.where(G > 0, W.F / np.maximum(G, 1e-12), 0.0)
        W.F -= took * fracF
        W.K -= took * (1 - fracF)
        tot = took.sum(1)
        f = np.where(eff > 0, tot / np.maximum(eff, 1e-12), 0.0) / dt
        gain = f[:, None] * sat * dt                       # per individual, this step (cohort mean's satiation)
        gain = np.where(n > 0, gain, 0.0)
        # credit NOW (before predation removes anyone): every member shifts by `gain`, so S and Q shift exactly
        H.Q += 2 * gain * H.S + n * gain * gain
        H.S += n * gain                                     # Σ n*gain == tot, exactly what left the flora
        beta = np.where(sat > 0, -f[:, None] / H.sp.e_max, 0.0)
        return gain, beta

    def _predate(self, dt, cold):
        P, H, Pr, rng = self.P, self.H, self.Pr, self.rng
        n, m, v = Pr.mean_var()
        thr = P.p_hunt_below * Pr.sp.e_max
        fh = ndtr((thr - m) / np.maximum(np.sqrt(v), 1e-6))
        hunters_c = n * fh
        hunters = hunters_c.sum(1) * cold
        nH = H.count() * cold
        rate = hunters * P.p_attack * nH / (1.0 + P.p_attack * P.p_handle * nH) * dt
        k = np.minimum(rng.poisson(rate), nH)
        rr = np.flatnonzero(k)
        if len(rr) == 0:
            return
        nh, mh, vh = H.mean_var()
        new_r, new_N, new_S, new_Q = [], [], [], []
        for r in rr:
            kk = int(k[r])
            vic = rng.multivariate_hypergeometric(H.N[r].ravel(), kk).reshape(H.C, NE)
            vc = vic.sum(1)
            mu = mh[r]; mu2 = vh[r] + mh[r] ** 2
            meal = float((vc * (H.sp.body + mu)).sum())
            H.N[r] -= vic; H.S[r] -= vc * mu; H.Q[r] -= vc * mu2
            w = hunters_c[r]
            if w.sum() <= 0:
                w = n[r].astype(float)
            ks = np.minimum(rng.multinomial(kk, w / w.sum()), n[r].astype(np.int64))
            got = int(ks.sum())
            per = meal / max(got, 1)
            for c in np.flatnonzero(ks):
                kc = int(ks[c])
                ke = rng.multivariate_hypergeometric(Pr.N[r, c], kc)
                mc = m[r, c]; vc_ = v[r, c]
                Pr.N[r, c] -= ke; Pr.S[r, c] -= kc * mc; Pr.Q[r, c] -= kc * (vc_ + mc * mc)
                newm = mc + per
                new_r.append(r); new_N.append(ke); new_S.append(kc * newm); new_Q.append(kc * (vc_ + newm * newm))
            if got == 0:
                self.W.N[r] += meal
            self.kills += kk
        if new_r:
            Pr.insert(np.array(new_r), np.array(new_N), np.array(new_S), np.array(new_Q))

    def _hyper_rows(self, pop, r, c, k):
        """Element split of k individuals drawn from each cohort (r[i], c[i])."""
        out = np.zeros((len(r), NE), np.int64)
        for i, (a, b, x) in enumerate(zip(r, c, k)):
            out[i] = self.rng.multivariate_hypergeometric(pop.N[a, b], int(x))
        return out

    def _energetics(self, k, pop, beta, dt, cold):
        W, P, rng, sp = self.W, self.P, self.rng, pop.sp
        n, m, v = pop.mean_var()
        live = (n > 0) & cold[:, None]
        if not live.any():
            return
        # metabolism + the expected SPRINT cost: a hunter chases whenever a herbivore is inside its sense radius,
        # a grazer flees whenever a predator is inside its flee radius - Poisson odds from the region's density
        Vreg = self.P.L ** 3
        if k == 0:
            other = self.Pr.count().astype(float)
            p_spr = 1.0 - np.exp(-other / Vreg * (4.0 / 3.0) * np.pi * P.h_flee ** 3)
            spr = p_spr[:, None] * np.ones_like(m)
        else:
            other = self.H.count().astype(float)
            p_spr = 1.0 - np.exp(-other / Vreg * (4.0 / 3.0) * np.pi * P.p_sense ** 3)
            hunt = ndtr((P.p_hunt_below * sp.e_max - m) / np.maximum(np.sqrt(v), 1e-6))
            spr = p_spr[:, None] * hunt
        if P.bug == "macro_no_sprint_cost":
            spr = spr * 0.0
        burn_pc = (sp.metab + sp.sprint_metab * spr) * dt
        burn = np.where(live, n * burn_pc, 0.0)
        W.N += burn.sum(1)                                           # metabolism: stomach -> soil, exactly
        pop.S -= burn
        m2 = np.where(live, pop.S / np.maximum(n, 1), 0.0)
        v2 = np.where(live, np.maximum(v * (1 + 2 * beta * dt) + 2 * P.e_diffuse[k] * dt, 0.0), v)
        pop.Q = np.where(live, n * (v2 + m2 * m2), pop.Q)
        # births: the upper tail past e_birth splits into parent + offspring (bodies paid from stomachs)
        pb, mub, mu2b = tail_moments(m2, np.sqrt(v2), sp.e_birth, True)
        nb = rng.binomial(np.where(live, n, 0).astype(np.int64), np.clip(pb, 0, 1))
        rb, cb = np.nonzero(nb)
        if len(rb):
            kb = nb[rb, cb]
            ke = self._hyper_rows(pop, rb, cb, kb)
            pop.take(rb, cb, ke, mub[rb, cb], mu2b[rb, cb])
            pm = mub[rb, cb] - sp.body - sp.e0
            pv = np.maximum(mu2b[rb, cb] - mub[rb, cb] ** 2, 0.0)
            S_new = kb * pm + kb * sp.e0
            Q_new = kb * (pv + pm * pm) + kb * sp.e0 * sp.e0
            pop.insert(rb, 2 * ke, S_new, Q_new)
            self.births[k] += int(kb.sum())
        # starvation: the lower tail past 0. body -> skeleton; the tail's (sub-zero) stomach stays with the
        # survivors, whose spread becomes the upper truncated part - exact in S
        n, m, v = pop.mean_var()
        live = (n > 0) & cold[:, None]
        pd, _, _ = tail_moments(m, np.sqrt(v), 0.0, False)
        nd = rng.binomial(np.where(live, n, 0).astype(np.int64), np.clip(pd, 0, 1))
        rd, cd = np.nonzero(nd)
        if len(rd):
            kd = nd[rd, cd]
            ke = self._hyper_rows(pop, rd, cd, kd)
            _, mus, mu2s = tail_moments(m[rd, cd], np.sqrt(v[rd, cd]), 0.0, True)
            pop.N[rd, cd] -= ke
            rest = pop.N[rd, cd].sum(1)
            mm = np.where(rest > 0, pop.S[rd, cd] / np.maximum(rest, 1), 0.0)
            vv = np.maximum(mu2s - mus * mus, 0.0)
            pop.Q[rd, cd] = np.where(rest > 0, rest * (vv + mm * mm), 0.0)
            occ = self.occupancy()
            np.add.at(W.K, rd, (kd * sp.body)[:, None] * occ[rd])
            self.deaths[k] += int(kd.sum())
            # an all-dead cohort's remaining S (~0) is settled by settle_empty()

    def _move(self, dt, cold, hot):
        """Regional hops out of cold regions. Movers keep their cohort's mean and spread; into a cold region they
        are inserted as a cohort (merging with a close one there); into a hot region they go to the inbox as
        individuals (stomachs drawn from the cohort's Normal, shifted so the movers' total is exact)."""
        W, P, rng = self.W, self.P, self.rng
        G = (W.F + W.K).sum(1)
        nH = self.H.count().astype(float)
        nP = self.Pr.count().astype(float)
        nb = W.nb
        valid = nb >= 0
        nbs = np.where(valid, nb, 0)
        for k, pop in enumerate(self.pops):
            n, m, v = pop.mean_var()
            if k == 0:
                w = np.power((G[nbs] + 1.0) / (G[:, None] + 1.0), P.food_bias) * \
                    np.power((nP[:, None] + 1.0) / (nP[nbs] + 1.0), P.flee_bias)
            else:
                w = np.power((nH[nbs] + 1.0) / (nH[:, None] + 1.0), P.prey_bias)
            w = np.where(valid, w, 0.0)
            w = w / np.maximum(w.sum(1, keepdims=True) / np.maximum(valid.sum(1, keepdims=True), 1), 1e-12)
            f = m / pop.sp.e_birth
            ph = np.where(f < pop.sp.phase_lo, 2, np.where(f > pop.sp.phase_hi, 0, 1))
            rates = np.asarray(P.hop_rate[pop.sp.name])[ph]                       # (R,C)
            p = np.clip(w[:, None, :] * rates[:, :, None] * dt, 0, None)        # (R,C,6)
            s = p.sum(2)
            p = np.where((s > 0.9)[..., None], p * (0.9 / np.maximum(s, 1e-12))[..., None], p)
            s = p.sum(2)
            live = (n > 0) & cold[:, None]
            rr, cc = np.nonzero(live)
            if len(rr) == 0:
                continue
            pv = np.concatenate([p[rr, cc], (1 - s[rr, cc])[:, None]], 1)       # (L,7)
            mv = rng.multinomial(pop.N[rr, cc], pv[:, None, :])[..., :6]         # (L,E,6)
            tot = mv.sum(axis=(1, 2))
            sel = tot > 0
            if not sel.any():
                continue
            rr, cc, mv, tot = rr[sel], cc[sel], mv[sel], tot[sel]
            mu = m[rr, cc]; mu2 = v[rr, cc] + mu * mu
            pop.take(rr, cc, mv.sum(2), mu, mu2)
            self.hops += int(tot.sum())
            for d in range(6):
                md = mv[:, :, d]
                c = md.sum(1)
                s_ = c > 0
                if not s_.any():
                    continue
                src = rr[s_]; dst = nb[src, d]; mdd = md[s_]; cc_ = c[s_]
                mu_s, mu2_s = mu[s_], mu2[s_]
                toh = hot[dst]
                if (~toh).any():
                    pop.insert(dst[~toh], mdd[~toh], (cc_ * mu_s)[~toh], (cc_ * mu2_s)[~toh])
                for j in np.flatnonzero(toh):
                    sdv = np.sqrt(max(mu2_s[j] - mu_s[j] ** 2, 0.0))
                    es = np.repeat(np.arange(NE), mdd[j])
                    e_draw = np.clip(rng.normal(mu_s[j], sdv, len(es)), 0.05, pop.sp.e_birth - 0.05)
                    e_draw += (cc_[j] * mu_s[j] - e_draw.sum()) / len(es)
                    for e, en in zip(es, e_draw):
                        self.inbox.append((k, int(dst[j]), int(src[j]), int(e), float(en)))

    def phase_counts(self, k):
        return self.pops[k].phase_counts()
