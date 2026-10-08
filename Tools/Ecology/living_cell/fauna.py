"""Fauna of the living cell: ONE level-of-detail population class (`Guild`) and five species on it.

A Guild is Direction E's hierarchy (hierarchy/macro.py, sim.py) cut to what a whole-cell study needs:
  * far from every pilot, a species is a MACRO cohort per 200-u region: count, Σstomach, Σstomach² (a Normal
    closure; E kept up to 10 cohorts per region, here ONE - the simplification is stated in the DISCOVERIES
    section, and the consistency check in run.py measures what it costs);
  * near a pilot a region EXPANDS into individual agents (stomachs sampled from the cohort, shifted so the sum
    is exact) and agents that drift far from every pilot are ABSORBED back. Expansion is far enough out that
    nothing appears where a pilot can see it (the continuity gate reads `world.continuity`).
Both levels run the SAME energetics, and both are mass-exact:
    metabolism  stomach -> soil nutrient N (burns only what is there)
    grazing     a whole prism -> stomach (shielded mass refused by World.eat)
    birth       parent stomach -> child body + child stomach (no mass from nothing)
    starvation  stomach <= 0: body -> one SKELETON prism where it died, crystal dropped
    predation   prey body + stomach -> predator stomach
No timer, no lifespan, no cull. Production gating (a species cap on births) is allowed and used.

The five species are PORTS of round 1's picks onto this one ledger (each class cites its source and the
constants it keeps): grazer (A's grazer school), locust (B's phase changer), pack (B's encircler, now also
a predator of grazers and locusts), thief (B's magpie, now with a larder), lurker (B's ambusher, now also
ambushing fauna).
"""
from __future__ import annotations

import math

import numpy as np

from .world import K_HERB, FLORA, TRAIL, SKEL, HOARD, K_GRAZE, K_LOCUST, flock_terms, nearest_point, flee_push

SQ2 = math.sqrt(2.0)


def count_within(P, Q, r):
    """How many points of Q lie within r of each point of P (packs x prey: both small in micro)."""
    if len(P) == 0 or len(Q) == 0:
        return np.zeros(len(P), np.int64)
    d2 = ((P[:, None, :] - Q[None, :, :]) ** 2).sum(-1)
    return (d2 < r * r).sum(1)


def unit(v):
    n = np.linalg.norm(v, axis=-1, keepdims=True)
    return v / np.maximum(n, 1e-9)


def steer(vel, desired, accel, dt):
    d = desired - vel
    n = np.linalg.norm(d, axis=-1, keepdims=True)
    a = np.asarray(accel, float)
    if a.ndim == 1:
        a = a[:, None]
    return vel + d * np.minimum(1.0, a * dt / np.maximum(n, 1e-9))


def contain(pos, vel, R, nucleus=0.0):
    r = np.linalg.norm(pos, axis=1)
    over = np.clip((r - 0.9 * R) / (0.06 * R), 0, None)
    vel = vel - pos / np.maximum(r[:, None], 1e-6) * (over[:, None] * 80.0)
    return vel


def _phi(x):
    return math.exp(-0.5 * x * x) / math.sqrt(2 * math.pi)


def _Phi(x):
    return 0.5 * (1.0 + math.erf(x / SQ2))


class Guild:
    name = "guild"
    body = 6.0; e0 = 4.0; e_birth = 18.0; e_max = 24.0; metab = 0.05
    imax = 0.5; F_half = 300.0; diet = K_HERB
    cap = 3000; capacity = 3000
    hop = 0.004                  # per-neighbour hop rate /s (macro migration)
    food_bias = 1.0
    size = 2.5; aspect = 1.2
    color = (0.5, 0.9, 1.0)
    threat = False                # does this species ever strike a pilot (encounter metric)
    sense = 160.0
    eat_r = 8.0
    predator = False
    prey_names: tuple = ()
    a_attack = 0.0; h_handle = 1.0          # macro Holling II (fitted, see run.py calibrate)
    body_paid = True                         # NEGATIVE CONTROL hook: False = births conjure the child's body

    def __init__(self, world, params=None):
        self.w = world
        for k, v in (params or {}).items():
            setattr(self, k, v)
        C = self.capacity
        self.pos = np.zeros((C, 3)); self.vel = np.zeros((C, 3)); self.st = np.zeros(C)
        self.alive = np.zeros(C, bool); self.claim = np.full(C, -1, np.int64); self.chew = np.zeros(C)
        self.bcool = np.zeros(C)
        self.intent = np.zeros(C)
        R = world.nreg
        self.cnt = np.zeros(R, np.int64); self.S = np.zeros(R); self.S2 = np.zeros(R)
        self.hot = np.zeros(R, bool)
        self.births = 0; self.starved = 0; self.killed = 0; self.eaten_vol = 0.0; self.by_pilot = 0
        self.expands = 0; self.absorbs = 0
        self.extra_init()
        world.held_fns.append(self.held)

    def extra_init(self):
        pass

    # ---- ledger ----------------------------------------------------------------------------------------
    def count(self):
        return int(self.cnt.sum() + self.alive.sum())

    def held(self):
        a = self.alive
        return float(self.body * (self.cnt.sum() + a.sum()) + self.S.sum() + self.st[a].sum()) + self.extra_held()

    def extra_held(self):
        return 0.0

    def biomass(self):
        return self.held()

    # ---- seeding ---------------------------------------------------------------------------------------
    def seed(self, n, centres=None, spread=150.0, st_frac=0.6):
        """Put n individuals into the MACRO state (cohorts). The volume comes out of N (book it)."""
        w = self.w
        if centres is None:
            P = w.ball(n, 0.3 * w.R, 0.85 * w.R)
        else:
            c = np.asarray(centres, float)
            P = c[w.rng.integers(0, len(c), n)] + w.rng.normal(0, spread, (n, 3))
        r = w.region_of(P)
        st = np.full(n, st_frac * self.e_birth)
        np.add.at(self.cnt, r, 1); np.add.at(self.S, r, st); np.add.at(self.S2, r, st * st)
        vol = n * self.body + st.sum()
        w.book_initial(vol)

    # ---- LOD -------------------------------------------------------------------------------------------
    def update_lod(self, hot_new):
        w = self.w
        turned = hot_new & ~self.hot
        self.hot = hot_new.copy()
        for r in np.flatnonzero(turned & (self.cnt > 0)):
            self.expand_region(r)

    def expand_region(self, r, n=None, at=None):
        """Cohort -> agents. `at`: (3,) a face point (migrants) else uniform in the region cube."""
        w = self.w
        c = int(self.cnt[r]) if n is None else int(n)
        if c <= 0:
            return
        S = self.S[r] * (c / self.cnt[r]); S2 = self.S2[r] * (c / self.cnt[r])
        free = np.flatnonzero(~self.alive)[:c]
        c = len(free)
        if c == 0:
            return
        m = S / c; sd = math.sqrt(max(S2 / c - m * m, 0.0))
        x = np.clip(w.rng.normal(m, sd, c), 0.0, 1.5 * self.e_max)
        if x.sum() > 0:
            x *= S / x.sum()
        else:
            x[:] = S / c
        cen = w.rcen_all[r]
        if at is None and self.clump_size > 0:
            # a cohort of a schooling species is a BAND, not a gas: expand into clumps of ~clump_size members
            k = max(1, int(math.ceil(c / self.clump_size)))
            cc = cen + w.rng.uniform(-0.4, 0.4, (k, 3)) * w.L
            P = cc[w.rng.integers(0, k, c)] + w.rng.normal(0, self.clump_spread, (c, 3))
        elif at is None:
            P = cen + w.rng.uniform(-0.5, 0.5, (c, 3)) * w.L
        else:
            P = np.asarray(at) + w.rng.normal(0, 15.0, (c, 3))
        rr = np.linalg.norm(P, axis=1); P = np.where((rr > 0.95 * w.R)[:, None], P / rr[:, None] * 0.95 * w.R, P)
        self.pos[free] = P; self.vel[free] = w.rng.normal(0, 5, (c, 3)); self.st[free] = x
        self.alive[free] = True; self.claim[free] = -1; self.chew[free] = 0; self.bcool[free] = 0
        self.on_spawn(free, from_macro=True)
        self.cnt[r] -= c; self.S[r] -= x.sum(); self.S2[r] -= float((x * x).sum())
        if self.cnt[r] == 0:
            self.S[r] = 0.0; self.S2[r] = 0.0
        self.expands += c
        w.appeared(self.name, P, "expand")

    def absorb(self, absorb_r):
        """Agents far from every pilot fold back into their region's cohort (only where no pilot can see)."""
        w = self.w
        idx = np.flatnonzero(self.alive)
        if len(idx) == 0:
            return
        if self.keep_micro():
            return
        d, _ = w.dist_to_pilots(self.pos[idx])
        r = w.region_of(self.pos[idx])
        go = (d > absorb_r) & ~self.hot[r] & self.absorbable(idx)
        if not go.any():
            return
        g = idx[go]; rg = r[go]
        self.release_claims(g)
        st = self.st[g]
        np.add.at(self.cnt, rg, 1); np.add.at(self.S, rg, st); np.add.at(self.S2, rg, st * st)
        self.alive[g] = False
        self.absorbs += len(g)
        w.appeared(self.name, self.pos[g], "absorb")

    def keep_micro(self):
        return False

    def absorbable(self, idx):
        return np.ones(len(idx), bool)

    def release_claims(self, idx):
        pass

    def on_spawn(self, idx, from_macro=False):
        pass

    # ---- energetics shared by both levels ---------------------------------------------------------------
    def kill_agent(self, i, by="pilot", to=None):
        """Agent i dies. to = None: starvation / pilot (skeleton + crystal); to = predator: returns the volume
        it delivers (body + stomach) instead of a skeleton."""
        w = self.w
        if not self.alive[i]:
            return 0.0
        self.alive[i] = False
        self.release_claims(np.array([i]))
        st = max(0.0, float(self.st[i]))
        w.crystals += 1
        if to is not None:
            self.killed += 1
            return self.body + st
        w.N += st
        w.add(self.pos[i].copy(), self.body, 0, SKEL)
        if by == "pilot":
            self.by_pilot += 1
        else:
            self.starved += 1
        return 0.0

    def micro_energetics(self, dt):
        w = self.w
        a = np.flatnonzero(self.alive)
        if len(a) == 0:
            return
        burn = np.minimum(self.st[a], self.metab * dt)
        self.st[a] -= burn; w.N += float(burn.sum())
        dead = a[self.st[a] <= 1e-9]
        for i in dead:
            self.kill_agent(i, by="starve")
        # births (production-gated by the species cap)
        self.bcool = np.maximum(0, self.bcool - dt)
        a = np.flatnonzero(self.alive)
        par = a[(self.st[a] >= self.e_birth) & (self.bcool[a] <= 0)]
        if len(par) == 0:
            return
        room = self.cap - self.count()
        free = np.flatnonzero(~self.alive)
        par = par[:max(0, min(room, len(free)))]
        for i, j in zip(par, free[:len(par)]):
            self.st[i] -= (self.body if self.body_paid else 0.0) + self.e0
            self.alive[j] = True; self.st[j] = self.e0
            self.pos[j] = self.pos[i] + w.rng.normal(0, 3, 3); self.vel[j] = self.vel[i]
            self.claim[j] = -1; self.chew[j] = 2.0; self.bcool[j] = self.bcool[i] = 6.0
            self.on_spawn(np.array([j]))
            self.births += 1

    def micro_graze(self, idx, dt, kmask=None, sense=None):
        """Herbivore grazing for agents idx: claim the nearest edible prism, eat it on contact, chew time =
        volume / imax (so intake <= imax). Returns the desired direction toward food (unit) and has-food."""
        w = self.w
        kmask = self.diet if kmask is None else kmask
        sense = self.sense if sense is None else sense
        if len(idx) == 0:
            return np.zeros((0, 3)), np.zeros(0, bool)
        hungry = self.st[idx] < self.e_max
        c = self.claim[idx]
        bad = (c < 0) | ~w.alive[np.maximum(c, 0)] | ~hungry
        bad |= np.isin(c, [-1])
        bad = bad | ((c >= 0) & ~((self.diet_ok(np.maximum(c, 0), kmask))))
        if bad.any():
            for i in idx[bad]:
                if self.claim[i] >= 0:
                    w.excl[self.claim[i]] = 0
                self.claim[i] = -1
            need = idx[bad & hungry]
            # 1-in-3 fractional re-search (Direction A's frac_k): the rest keep wandering this tick
            need = need[(need + int(w.t * 10)) % 3 == 0]
            if len(need):
                got = w.nearest(self.pos[need], sense, kmask)
                for i, j in zip(need, got):
                    if j >= 0 and self.edible_here(j):
                        self.claim[i] = j; w.excl[j] = 1
        c = self.claim[idx]; has = c >= 0
        dirv = np.zeros((len(idx), 3))
        if has.any():
            to = w.pos[c[has]] - self.pos[idx[has]]
            dd = np.linalg.norm(to, axis=1)
            dirv[has] = to / np.maximum(dd[:, None], 1e-9)
            self.chew[idx] = np.maximum(0, self.chew[idx] - dt)
            eat = np.flatnonzero(has)[(dd < self.eat_r) & (self.chew[idx[has]] <= 0)]
            for k in eat:
                i = idx[k]; j = self.claim[i]
                w.excl[j] = 0; self.claim[i] = -1
                v = w.eat(j, self.name)
                self.st[i] += v; self.eaten_vol += v
                self.chew[i] = v / self.imax
        else:
            self.chew[idx] = np.maximum(0, self.chew[idx] - dt)
        return dirv, has

    def diet_ok(self, j, kmask):
        w = self.w
        return ((kmask >> w.kind[j].astype(np.int64)) & 1).astype(bool) & w.alive[j] & ~w.shield[j]

    def edible_here(self, j):
        """Nucleus cell semantics (CLAUDE.md 'Volume is the spine'): outside the nucleus any domain is food;
        inside it, nothing is."""
        return np.linalg.norm(self.w.pos[j]) > self.w.nucleus

    def release_grazing_claims(self, idx):
        for i in idx:
            j = self.claim[i]
            if j >= 0:
                self.w.excl[j] = 0
                self.claim[i] = -1

    # ---- macro -----------------------------------------------------------------------------------------
    def macro_food(self, food_by_region):
        return food_by_region

    def macro_step(self, dt, food, edible_lists, guilds):
        """One macro second for every COLD region holding this species."""
        w = self.w
        cold = (~self.hot) & (self.cnt > 0)
        rs = np.flatnonzero(cold)
        if len(rs) == 0:
            return
        c = self.cnt[rs].astype(float)
        # grazing (Holling II on the region's edible volume, scaled by satiation)
        if self.imax > 0 and food is not None:
            F = food[rs]
            m = self.S[rs] / c
            sat = np.clip(1.0 - m / self.e_max, 0.0, 1.0)
            demand = c * self.imax * F / (F + self.F_half) * sat * dt
            self.credit[rs] += demand
            for k, r in enumerate(rs):
                lst = edible_lists.get(r)
                got = 0.0
                while self.credit[r] > 0 and lst:
                    j = lst.pop()
                    if not w.alive[j] or w.excl[j]:
                        continue
                    v = w.eat(j, self.name)
                    got += v; self.credit[r] -= v
                if not lst:
                    self.credit[r] = min(self.credit[r], 0.0)
                if got > 0:
                    g = got / self.cnt[r]
                    self.S2[r] += 2 * g * self.S[r] + self.cnt[r] * g * g
                    self.S[r] += got; self.eaten_vol += got
        self.macro_tail(rs, dt)
        self.macro_hops(rs, dt, food, guilds)

    def macro_tail(self, rs, dt):
        """Metabolism, births and starvation for cohorts rs (Normal closure)."""
        w = self.w
        for r in rs:
            c = int(self.cnt[r])
            if c == 0:
                continue
            S, S2 = self.S[r], self.S2[r]
            burn = min(S, c * self.metab * dt)
            mb = burn / c
            S2 = max(S2 - 2 * mb * S + c * mb * mb, 0.0); S -= burn; w.N += burn
            m = S / c; var = max(S2 / c - m * m, 0.25); sd = math.sqrt(var)
            # starvation: the part of the cohort below ~0 (a stomach that cannot pay one second of metabolism)
            beta = (self.metab * dt - m) / sd
            pl = _Phi(beta)
            ns = w.rng.binomial(c, min(1.0, pl)) if pl > 1e-6 else 0
            if ns:
                ns = min(ns, c)
                lam = _phi(beta) / max(pl, 1e-12)
                mlo = max(0.0, m - sd * lam)
                take = min(S, ns * mlo)
                S -= take; w.N += take
                S2 = max(S2 - ns * (mlo * mlo), 0.0)
                c -= ns; self.starved += ns; w.crystals += ns
                cen = w.rcen_all[r]
                for _ in range(ns):
                    w.add(cen + w.rng.uniform(-0.5, 0.5, 3) * w.L, self.body, 0, SKEL)
                if c == 0:
                    w.N += S; S = 0.0; S2 = 0.0
            # births: the part above e_birth splits off one offspring each (paid body + e0)
            if c > 0 and self.count() < self.cap:
                m = S / c; var = max(S2 / c - m * m, 0.25); sd = math.sqrt(var)
                a = (self.e_birth - m) / sd
                pa = 1.0 - _Phi(a)
                nb = w.rng.binomial(c, min(1.0, pa)) if pa > 1e-6 else 0
                nb = min(nb, max(0, self.cap - self.count()), int(S // (self.body + self.e0)))
                if nb > 0:
                    lam = _phi(a) / max(pa, 1e-12)
                    mu = m + sd * lam
                    m2 = m * m + 2 * m * sd * lam + var * (1 + a * lam)
                    cost = self.body + self.e0
                    S2 = S2 - nb * m2 + nb * (m2 - 2 * cost * mu + cost * cost) + nb * self.e0 ** 2
                    S -= nb * self.body if self.body_paid else 0.0
                    c += nb; self.births += nb
            self.cnt[r] = c; self.S[r] = S; self.S2[r] = max(S2, S * S / max(c, 1))

    def macro_hops(self, rs, dt, food, guilds):
        w = self.w
        if self.hop <= 0:
            return
        moves = []
        for r in rs:
            c = int(self.cnt[r])
            nb = w.rnb[r]
            if c == 0 or not nb:
                continue
            k = w.rng.binomial(c, min(1.0, self.hop * len(nb) * dt))
            if k == 0:
                continue
            wt = np.array([self.hop_weight(r, q, food, guilds) for q in nb])
            wt = wt / wt.sum() if wt.sum() > 0 else np.full(len(nb), 1 / len(nb))
            split = w.rng.multinomial(k, wt)
            m = self.S[r] / c; m2 = self.S2[r] / c
            for q, kk in zip(nb, split):
                if kk:
                    moves.append((r, q, kk, m, m2))
        for r, q, kk, m, m2 in moves:
            kk = min(kk, int(self.cnt[r]))
            if kk <= 0:
                continue
            if self.hot[q]:
                # migrants into a hot region arrive as agents at the shared face - only if no pilot can see
                # that face; otherwise the hop simply does not happen this second (continuity first)
                face = 0.5 * (w.rcen_all[r] + w.rcen_all[q])
                if len(w.pilots) and w.dist_to_pilots(face[None])[0][0] < self.face_clear:
                    continue
            self.cnt[r] -= kk; self.S[r] -= kk * m; self.S2[r] -= kk * m2
            if self.cnt[r] == 0:
                # the last members out take the cohort's rounding dust with them (stays in the ledger)
                m += self.S[r] / kk; self.S[r] = 0.0; self.S2[r] = 0.0
            self.cnt[q] += kk; self.S[q] += kk * m; self.S2[q] += kk * m2
            if self.hot[q]:
                # migrants into a hot region arrive as agents at the shared face (never in the middle)
                face = 0.5 * (w.rcen_all[r] + w.rcen_all[q])
                self.expand_region(q, n=kk, at=face)

    face_clear = 330.0
    clump_size = 0; clump_spread = 40.0      # >0: expand cohorts as clumps (schooling species; iter 5)

    def hop_weight(self, r, q, food, guilds):
        if food is None:
            return 1.0
        return (1.0 + food[q]) ** self.food_bias / (1.0 + food[r]) ** self.food_bias

    # ---- interface -------------------------------------------------------------------------------------
    def step(self, dt):
        """Micro step for the agents (subclass act), then energetics and integration."""
        a = np.flatnonzero(self.alive)
        if len(a):
            self.act(a, dt)
            self.pos[a] += self.vel[a] * dt
        self.micro_energetics(dt)

    def act(self, idx, dt):
        pass

    def agents(self):
        a = self.alive
        return self.pos[a], self.vel[a]

    def threat_agents(self):
        """(pos, vel, size, active) of agents that count as a THREAT encounter for a pilot."""
        a = np.flatnonzero(self.alive)
        return self.pos[a], self.vel[a], np.full(len(a), self.size), np.zeros(len(a), bool)

    def render(self, out):
        a = self.alive
        out[self.name] = dict(pos=self.pos[a], col=np.tile(self.color, (int(a.sum()), 1)),
                              size=np.full(int(a.sum()), self.size))

    def macro_render(self):
        """Impostor centroids for regions held in macro (viewer: a faint cloud per region)."""
        r = np.flatnonzero(self.cnt > 0)
        return self.w.rcen_all[r], self.cnt[r]


def pair(P):
    D = P[None, :, :] - P[:, None, :]
    d = np.linalg.norm(D, axis=2); np.fill_diagonal(d, np.inf)
    return D, d


def separation(D, d, radius):
    wgt = np.clip(1.0 - d / radius, 0, None)
    return -(D / np.maximum(d[:, :, None], 1e-6) * wgt[:, :, None]).sum(1)


# ==========================================================================================================
class Grazer(Guild):
    """A's grazer school (substrate/species.py `grazer`): food scent + align + cohesion + separation, a
    CURIOUS comfort ring around a pilot (70 u), flee predators. Never aggressive: prey and background life."""
    name = "grazer"
    diet = K_GRAZE                   # flora + skeleton (E's herbivore diet)
    body, e0, e_birth, e_max, metab = 6.0, 4.0, 18.0, 26.0, 0.045
    imax, F_half = 0.5, 250.0
    clump_size, clump_spread = 40, 50.0
    cap = 2600; capacity = 2600
    hop = 0.003; food_bias = 0.5
    size = 2.5; color = (0.45, 0.95, 1.0)
    speed, flee_speed = 34.0, 70.0

    def extra_init(self):
        self.credit = np.zeros(self.w.nreg); self.fear = np.zeros(self.capacity)

    def release_claims(self, idx):
        self.release_grazing_claims(idx)

    def act(self, idx, dt):
        w = self.w
        P = self.pos[idx]; V = self.vel[idx]
        food, has = self.micro_graze(idx, dt)
        _, align, coh, sep = flock_terms(P, V, 30.0, 30.0, 10.0)
        # predators near: flee (alarm), the predator list is filled by the Cell each tick
        flee = np.zeros_like(P); afraid = np.zeros(len(idx))
        for Q in w.predator_pos:
            if len(Q) == 0:
                continue
            f, af = flee_push(P, np.ascontiguousarray(Q), 70.0)
            flee += f; afraid = np.maximum(afraid, af)
        # curiosity: a comfort ring around the nearest pilot (approach to 70 u, back off inside)
        dp, k = w.dist_to_pilots(P)
        cur = np.zeros_like(P)
        if len(w.pilots):
            PP = w.pilot_pos()[k]
            to = PP - P
            cur = unit(to) * np.clip((dp - 70.0) / 70.0, -1.5, 1.0)[:, None] * (dp < 250)[:, None]
        wander = unit(w.rng.normal(size=P.shape))
        des = unit(1.0 * food + 0.5 * unit(align) + 0.02 * coh + 0.35 * wander + 1.2 * cur + 0.8 * sep) * self.speed
        des = np.where(afraid[:, None] > 0, unit(flee + 0.3 * sep) * self.flee_speed, des)
        V = steer(V, des, 70.0, dt)
        self.vel[idx] = contain(P, V, w.R)
        self.intent[idx] = 0.0


class Locust(Guild):
    """B's phase changer (bestiary/species/locust.py; constants kept: crowd radius 40, threshold 9 - 3*hunger,
    relax tau 4 s, march 135 u/s, swarm-a-pilot range 250, bite cool 2 s). Breeds on food; the cap is
    production gating. In macro the whole cohort is solitary (a sparse cloud far from anyone)."""
    name = "locust"
    diet = K_LOCUST                  # flora + pilot TRAIL (B: the gregarious swarm eats your wake)
    body, e0, e_birth, e_max, metab = 3.0, 2.0, 10.0, 14.0, 0.05
    imax, F_half = 0.6, 250.0
    cap = 1200; capacity = 1200
    clump_size, clump_spread = 40, 30.0
    hop = 0.006; food_bias = 1.0
    size = 2.0; color = (0.45, 0.95, 0.4); threat = True

    def extra_init(self):
        self.credit = np.zeros(self.w.nreg); self.g = np.zeros(self.capacity)
        self.tips = 0

    def release_claims(self, idx):
        self.release_grazing_claims(idx)

    def on_spawn(self, idx, from_macro=False):
        self.g[idx] = 0.0

    def act(self, idx, dt):
        w = self.w
        P = self.pos[idx]; V = self.vel[idx]; g = self.g[idx]
        hunger = np.clip(1.0 - self.st[idx] / self.e_birth, 0, 1)
        nn, align, coh, sep = flock_terms(P, V, 40.0, 60.0, 18.0)
        target = 1.0 / (1.0 + np.exp(-(nn - (9.0 - 3.0 * hunger)) * 0.9))
        g0 = g.copy()
        g = g + (target - g) * (dt / 4.0)
        if self.ablate == "solitary":
            g = np.zeros_like(g)
        self.tips += int(((g0 < 0.5) & (g >= 0.5)).sum())
        # gregarious locusts also take trail: diet stays K_HERB for both (the nucleus-cell law), but the
        # solitary ones search only half as far
        food, has = self.micro_graze(idx, dt)
        dp, k = w.dist_to_pilots(P)
        topilot = np.zeros_like(P); shy = np.zeros(len(idx), bool); swarm = np.zeros(len(idx), bool)
        if len(w.pilots):
            topilot = unit(w.pilot_pos()[k] - P)
            shy = (dp < 90) & (g < 0.5); swarm = (dp < 250) & (g >= 0.5)
        # pack predators: solitary locusts flee them
        flee = np.zeros_like(P)
        for Q in w.predator_pos:
            if len(Q):
                flee += flee_push(P, np.ascontiguousarray(Q), 50.0)[0]
        sol = unit(w.rng.normal(0, 1, P.shape) + 0.6 * food + 1.5 * flee) * 25.0 + sep * 40.0 - topilot * shy[:, None] * 70.0
        gre = unit(align / 135.0 * 1.5 + coh * 0.02 + 0.8 * food + 1.6 * topilot * swarm[:, None]) * 135.0 + sep * 30.0
        des = sol * (1 - g)[:, None] + gre * g[:, None]
        V = steer(V, des, 80.0 + 300.0 * g, dt)
        self.vel[idx] = contain(P, V, w.R)
        self.g[idx] = g
        self.intent[idx] = g * np.clip(1.6 - dp / 400.0, 0, 1)
        self.bcool_bite = getattr(self, "bcool_bite", np.zeros(self.capacity))
        self.bcool_bite[idx] = np.maximum(0, self.bcool_bite[idx] - dt)
        for kk in np.flatnonzero(swarm & (dp < 10)):
            i = idx[kk]
            if self.bcool_bite[i] <= 0:
                w.hit(int(k[kk]), self.name, "bite", 0.05); self.bcool_bite[i] = 2.0

    ablate = None

    def threat_agents(self):
        a = np.flatnonzero(self.alive)
        return self.pos[a], self.vel[a], 2.0 * (1 + 0.5 * self.g[a]), self.g[a] >= 0.5

    def render(self, out):
        a = self.alive; g = self.g[a][:, None]
        out[self.name] = dict(pos=self.pos[a], col=np.array([0.45, 0.95, 0.4]) * (1 - g) + np.array([1.0, 0.8, 0.1]) * g,
                              size=2.0 * (1 + 0.5 * self.g[a]))


class Pack(Guild):
    """B's encircling pack (bestiary/species/pack.py; constants kept: cruise 95, sprint 175, range 900, ring
    radius clip(0.5 d, 120, 220), quorum closure > 0.55, stamina 3 s, back-off 3 s) - and, new here, a
    PREDATOR: a hunter whose stomach is under 60% of e_max chases the nearest grazer/locust/thief inside
    300 u instead, and lives or starves on what it catches."""
    name = "pack"
    body, e0, e_birth, e_max, metab = 30.0, 15.0, 70.0, 90.0, 0.04
    imax = 0.0
    cap = 160; capacity = 400
    hop = 0.004
    size = 12.0; aspect = 2.6; color = (0.95, 0.35, 0.3); threat = True
    predator = True; prey_names = ("grazer", "locust", "thief")
    a_attack, h_handle = 6.0e-4, 40.0
    hunt_below = 0.6; catch_r = 10.0; prey_sense = 300.0
    hunt_prey_below = 0.0
    hunger_gate = True     # a pack stalks a PILOT only while hungry and with no prey in range (iteration 1)
    switch_ref = 0.0       # >0: Holling III. Macro: rate x Np/(Np+switch_ref). Micro: a hunter commits to a chase
                           # only with >= switch_ref/4 prey inside prey_sense (round 2; 0 = off, the R8 cell)

    def extra_init(self):
        C = self.capacity
        self.stamina = np.full(C, 3.0); self.cool = np.zeros(C); self.closure = np.zeros(C)
        self.weave = self.w.rng.uniform(0, 6.28, C); self.strikes = 0; self.prey_kills = 0
        self.heading = np.zeros((C, 3)); self.mode = np.zeros(C, np.int8); self.stalking = np.zeros(C, bool)

    def on_spawn(self, idx, from_macro=False):
        self.stamina[idx] = 3.0; self.cool[idx] = 0.0

    eff = 1.0      # round 2: the share of a kill the pack eats; the rest stays as a CARCASS (skeleton prism)

    def carcass(self, v, at):
        """A kill of volume v: the pack keeps eff * v, the remainder is left where the prey died as carrion
        (a SKEL prism - scavengers' food). Conserved: nothing is lost."""
        if self.eff >= 1.0 or v <= 0:
            return v
        keep = self.eff * v
        self.w.add(np.asarray(at, float).copy(), v - keep, 0, SKEL)
        self.carrion = getattr(self, "carrion", 0.0) + (v - keep)
        return keep

    def hop_weight(self, r, q, food, guilds):
        tot_r = sum(g.cnt[r] for g in guilds.values() if g.name in self.prey_names)
        tot_q = sum(g.cnt[q] for g in guilds.values() if g.name in self.prey_names)
        return (1.0 + tot_q) / (1.0 + tot_r)

    def act(self, idx, dt):
        w = self.w
        P = self.pos[idx]; V = self.vel[idx]; n = len(idx)
        des = np.zeros_like(P)
        # --- prey: hungry hunters chase the nearest prey agent
        hungry = self.st[idx] < self.hunt_below * self.e_max
        # round 2: a hunter keeps hunting PREY up to hunt_prey_below (>= e_birth / e_max, so a fed pack can
        # breed); pilots are still stalked only below hunt_below. Default = hunt_below (the R8 cell).
        hp = self.hunt_prey_below if self.hunt_prey_below > 0 else self.hunt_below
        hungry_prey = self.st[idx] < hp * self.e_max
        preyP, preyRef = [], []
        for g in w.guild_list:
            if g.name in self.prey_names:
                a = np.flatnonzero(g.alive)
                preyP.append(g.pos[a]); preyRef += [(g, i) for i in a]
        self.prey_near = np.zeros(n, bool)
        preyP = np.concatenate(preyP) if preyP else np.zeros((0, 3))
        chase = np.zeros(n, bool); self.prey_near = np.zeros(n, bool); self._dprey = np.full(n, np.inf)
        if len(preyP) and hungry_prey.any():
            j, dj = nearest_point(P, preyP, self.prey_sense)
            chase = hungry_prey & (j >= 0); self.prey_near = j >= 0; self._dprey = dj
            if self.switch_ref > 0:
                dens = count_within(P, preyP, self.prey_sense)
                chase &= dens >= self.switch_ref / 4.0
            # a pack goes for whichever is nearer: the prey, or a pilot (iter 5: grazers crowd every pilot)
            dp0, _ = w.dist_to_pilots(P)
            chase &= ~(dp0 < dj)
            des[chase] = unit(preyP[j[chase]] - P[chase]) * 175.0
            for kk in np.flatnonzero(chase & (dj < self.catch_r)):
                g, i = preyRef[j[kk]]
                if g.alive[i]:
                    v = g.kill_agent(i, by="predator", to=self)
                    self.st[idx[kk]] += self.carcass(v, preyP[j[kk]]); self.prey_kills += 1
        # --- pilots: the encirclement (B's pack, unchanged rule) for hunters not chasing prey
        dp, k = w.dist_to_pilots(P)
        if len(w.pilots):
            PP = w.pilot_pos(); PV = np.array([p.vel for p in w.pilots])
            tau = np.clip(dp / 175.0, 0, 2.0)
            pred = PP[k] + PV[k] * tau[:, None]
            head = unit(PV[k]); b = unit(P - pred)
            same = (k[:, None] == k[None, :]) & (dp[None, :] < 350) & (dp[:, None] < 900) & ~chase[None, :] & ~chase[:, None]
            np.fill_diagonal(same, False)
            cosb = b @ b.T
            push = np.where(same, np.clip(cosb - 0.2, 0, None), 0.0)
            ang = -(push[:, :, None] * (b[None, :, :] - cosb[:, :, None] * b[:, None, :])).sum(1)
            cnt = same.sum(1) + 1
            res = np.linalg.norm((same[:, :, None] * b[None, :, :]).sum(1) + b, axis=1) / cnt
            closure = np.clip((1.0 - res) * np.clip((cnt - 1) / 3.0, 0, 1) * 1.6, 0, 1) * (dp < 350)
            self.closure[idx] = closure
            want_b = unit(b + 1.2 * ang + 0.9 * head)
            striking = (closure > 0.55) & (self.stamina[idx] > 0.3) & (self.cool[idx] <= 0)
            ring = np.where(striking, 0.0, np.clip(dp * 0.5, 120, 220))
            goal = pred + want_b * ring[:, None]
            dprey = self._dprey
            stalk = (dp < 900) & ~chase & (hungry | (not self.hunger_gate)) & (~self.prey_near | (dp < dprey))
            spd = np.where(striking, 175.0, 95.0)
            des[stalk] = unit(goal - P)[stalk] * spd[stalk, None]
            side = unit(np.cross(des, [0.0, 1.0, 0.0]) + 1e-6)
            des += side * (np.sin(1.3 * w.t + self.weave[idx]) * 35.0 * (~striking) * stalk)[:, None]
            back = (self.cool[idx] > 0) & stalk
            des[back] = unit(P[back] - PP[k[back]]) * 95.0
            self.stamina[idx] = np.where(striking & stalk, self.stamina[idx] - dt, np.minimum(3.0, self.stamina[idx] + 0.5 * dt))
            self.intent[idx] = np.where(back, 0.0, closure)
            self.heading[idx] = np.where((dp < 900)[:, None], unit(pred - P), unit(V))
            for kk in np.flatnonzero(stalk & (dp < 6 + self.size + 4) & (self.cool[idx] <= 0)):
                w.hit(int(k[kk]), self.name, "bite"); self.strikes += 1
                mates = idx[same[kk]]
                self.cool[mates] = 3.0; self.cool[idx[kk]] = 3.0
            roam = ~chase & ~stalk
            self.stalking[idx] = stalk
        else:
            roam = ~chase; self.stalking[idx] = False
        # roam: drift toward the pack centroid of nearby mates
        if roam.any():
            D, d = pair(P)
            near = d < 250
            cen = np.where(near.any(1)[:, None], (near[:, :, None] * P[None]).sum(1) / np.maximum(near.sum(1, keepdims=True), 1), P)
            des[roam] = unit(cen[roam] - P[roam] + w.rng.normal(0, 40, (roam.sum(), 3))) * 40.0
        des += flock_terms(P, V, 30.0, 30.0, 30.0)[3] * 60
        self.cool[idx] = np.maximum(0, self.cool[idx] - dt)
        V = steer(V, des, 260.0, dt)
        self.vel[idx] = contain(P, V, w.R)

    def macro_step(self, dt, food, edible_lists, guilds):
        """Holling II predation of the region's prey cohorts (fitted: run.py calibrate)."""
        w = self.w
        rs = np.flatnonzero((~self.hot) & (self.cnt > 0))
        for r in rs:
            c = int(self.cnt[r])
            m = self.S[r] / c
            hp = self.hunt_prey_below if self.hunt_prey_below > 0 else self.hunt_below
            if m > hp * self.e_max:
                continue
            prey = [g for g in guilds.values() if g.name in self.prey_names and g.cnt[r] > 0 and not g.hot[r]]
            Np = sum(int(g.cnt[r]) for g in prey)
            if Np == 0:
                continue
            rate = self.a_attack * Np * c / (1.0 + self.a_attack * self.h_handle * Np)
            if self.switch_ref > 0:
                # Holling III (round 2): attack efficiency falls when prey is scarce - the hunter switches to
                # searching / resting instead of grinding the last prey of a region to zero (a prey refuge)
                rate *= Np / (Np + self.switch_ref)
            kills = min(Np, w.rng.poisson(rate * dt))
            for _ in range(kills):
                cw = np.array([g.cnt[r] for g in prey], float)
                g = prey[w.rng.choice(len(prey), p=cw / cw.sum())]
                if g.cnt[r] == 0:
                    continue
                gm = g.S[r] / g.cnt[r]; gm2 = g.S2[r] / g.cnt[r]
                g.cnt[r] -= 1; g.S[r] -= gm; g.S2[r] -= gm2
                if g.cnt[r] == 0:
                    gm += g.S[r]; g.S[r] = 0.0; g.S2[r] = 0.0
                g.killed += 1; w.crystals += 1
                v = g.body + max(gm, 0.0)
                v = self.carcass(v, w.rcen_all[r] + w.rng.uniform(-0.5, 0.5, 3) * w.L)
                gg = v / self.cnt[r]
                self.S2[r] += 2 * gg * self.S[r] + self.cnt[r] * gg * gg; self.S[r] += v
                self.prey_kills += 1; self.macro_kills = getattr(self, "macro_kills", 0) + 1
        self.macro_tail(rs, dt)
        self.macro_hops(rs, dt, food, guilds)

    def threat_agents(self):
        a = np.flatnonzero(self.alive)
        return self.pos[a], self.vel[a], np.full(len(a), self.size), self.stalking[a]

    def render(self, out):
        a = np.flatnonzero(self.alive)
        c = np.tile([0.55, 0.25, 0.2], (len(a), 1)); c[:, 0] += 0.45 * self.closure[a]
        out[self.name] = dict(pos=self.pos[a], col=c, size=np.full(len(a), self.size))


class Thief(Guild):
    """B's magpie (bestiary/species/thief.py; constants kept: free 150 u/s, laden 75, scout 400, warm wake
    1.5 s, spot 700, timid 120). New here: thieves eat (nectar: flora, slowly) and keep a LARDER - a hungry
    thief at its nest eats one hoard prism. Stolen trail therefore feeds thieves, and the hoard is the thing
    the fortress raids. Nest-bound: no macro migration."""
    name = "thief"
    body, e0, e_birth, e_max, metab = 3.0, 2.0, 12.0, 16.0, 0.025
    imax, F_half = 0.25, 300.0
    cap = 220; capacity = 400
    hop = 0.0
    size = 2.2; color = (0.75, 0.75, 1.0); threat = True
    diet = (1 << FLORA) | (1 << SKEL)    # nectar + scavenging (round 1 of iteration: nectar alone starved them)
    leash = 900.0                         # territory: a thief never tails a ship beyond this from its nest (iter 3)
    feed_fix = False                      # round 2: starving thieves go home; the larder feeds a thief to e_max

    def extra_init(self):
        C = self.capacity
        self.credit = np.zeros(self.w.nreg)
        self.carry = np.full(C, -1, np.int64); self.tclaim = np.full(C, -1, np.int64)
        self.nest_of = np.zeros(C, np.int64); self.from_dom = {}
        self.nests = np.zeros((0, 3)); self.steals = 0; self.stolen_vol = 0.0; self.hoarded = 0
        self.larder = 0.0; self.recaptured = 0

    def set_nests(self, nests):
        self.nests = np.asarray(nests, float)

    def on_spawn(self, idx, from_macro=False):
        if len(self.nests):
            d = np.linalg.norm(self.pos[idx][:, None, :] - self.nests[None], axis=2)
            self.nest_of[idx] = d.argmin(1)
        self.carry[idx] = -1; self.tclaim[idx] = -1

    def release_claims(self, idx):
        self.release_grazing_claims(idx)
        w = self.w
        for i in idx:
            j = self.tclaim[i]
            if j >= 0:
                w.excl[j] = 0; self.tclaim[i] = -1
            j = self.carry[i]
            if j >= 0:
                # a carried prism is dropped where the thief is (it stays a prism: nothing lost)
                w.excl[j] = 0; w.kind[j] = HOARD; self.carry[i] = -1

    def act(self, idx, dt):
        w = self.w
        P = self.pos[idx]; V = self.vel[idx]
        des = np.zeros_like(P)
        nest = self.nests[self.nest_of[idx]] if len(self.nests) else np.zeros_like(P)
        dp, k = w.dist_to_pilots(P)
        food, has = self.micro_graze(idx, dt)
        # warm wake: trail laid in the last 1.5 s
        laden = self.carry[idx] >= 0
        for kk in np.flatnonzero(laden):
            i = idx[kk]; j = self.carry[i]
            to = nest[kk] - P[kk]
            des[kk] = unit(to) * 75.0
            w.pos[j] = P[kk] + V[kk] * dt
            if np.linalg.norm(to) < 12:
                w.pos[j] = nest[kk] + unit(w.rng.normal(size=3)) * (8 + 2.0 * np.cbrt(self.hoarded + 1))
                w.kind[j] = HOARD; w.excl[j] = 0; self.carry[i] = -1; self.hoarded += 1
        free = np.flatnonzero(~laden)
        # drop stale claims
        tc = self.tclaim[idx]
        stale = (tc >= 0) & ((~w.alive[np.maximum(tc, 0)]) | (w.kind[np.maximum(tc, 0)] != TRAIL))
        for kk in np.flatnonzero(stale):
            w.excl[tc[kk]] = 0; self.tclaim[idx[kk]] = -1
        dn = np.linalg.norm(P - nest, axis=1)
        far = dn > self.leash
        tc = self.tclaim[idx]
        for kk in np.flatnonzero(far & (tc >= 0)):
            w.excl[tc[kk]] = 0; self.tclaim[idx[kk]] = -1
        tc = self.tclaim[idx]
        look = free[(tc[free] < 0) & (dp[free] < 700) & ~far[free]]
        if len(look):
            got = w.nearest(P[look], 400.0, 1 << TRAIL, tmin=w.t - 1.5)
            for kk, j in zip(look, got):
                if j >= 0 and not w.excl[j]:
                    self.tclaim[idx[kk]] = j; w.excl[j] = 1
        tc = self.tclaim[idx]
        claimed = free[tc[free] >= 0]
        if len(claimed):
            to = w.pos[tc[claimed]] - P[claimed]
            des[claimed] = unit(to) * 150.0
            for kk in claimed[np.linalg.norm(to, axis=1) < 5]:
                i = idx[kk]; j = self.tclaim[i]
                self.from_dom[int(j)] = (int(w.dom[j]), int(w.owner[j]))
                w.dom[j] = 1; self.carry[i] = j; self.tclaim[i] = -1
                self.steals += 1; self.stolen_vol += w.vol[j]
                w.hit(int(k[kk]), self.name, "steal", float(w.vol[j]))
        # round 2: a STARVING thief stops tailing and goes home to its larder (before: it tailed a nearby pilot
        # until it starved, and only ever ate at the nest while under 40% of e_birth, so it never bred)
        starving = (self.st[idx] < 0.4 * self.e_birth) if self.feed_fix else np.zeros(len(idx), bool)
        tail = free[(tc[free] < 0) & (dp[free] < 700) & ~far[free] & ~starving[free]]
        if len(tail) and len(w.pilots):
            PPv = w.pilot_pos(); PVv = np.array([p.vel for p in w.pilots])
            des[tail] = unit(PPv[k[tail]] - unit(PVv[k[tail]]) * 70.0 - P[tail]) * 150.0
        home_ = free[(tc[free] < 0) & ((dp[free] >= 700) | far[free] | starving[free])]
        if len(home_):
            hv = nest[home_] - P[home_]
            hungry = self.st[idx[home_]] < 0.4 * self.e_birth
            if self.feed_fix:
                # go home while the larder has food; graze on the way / when it is empty
                hl = np.array([len(w.within(nest[kk], 60.0, 1 << HOARD)) > 0 for kk in home_], bool) if hungry.any() else np.zeros(len(home_), bool)
                graze = has[home_] & ~(hungry & hl)
                hungry = hungry | ((self.st[idx[home_]] < self.e_max - 3.0) & (np.linalg.norm(hv, axis=1) < 30))
            else:
                graze = has[home_] & ~hungry
            des[home_] = np.where(graze[:, None], unit(0.5 * food[home_] + 0.02 * hv + 0.3 * w.rng.normal(size=hv.shape)) * 30.0,
                                  unit(hv + w.rng.normal(0, 30, hv.shape)) * 30.0)
            # larder: a hungry thief at the nest eats one hoard prism
            for kk in home_[hungry & (np.linalg.norm(hv, axis=1) < 30)]:
                i = idx[kk]
                if self.feed_fix:
                    if self.chew[i] > 0:
                        continue
                    self.chew[i] = 3.0 / self.imax         # one 3-vol hoard prism per 12 s = imax
                h = w.within(nest[kk], 60.0, 1 << HOARD)
                h = h[w.excl[h] == 0] if len(h) else h
                if len(h):
                    v = w.eat(int(h[0]), self.name); self.st[i] += v; self.larder += v; self.eaten_vol += v
        # timid: veer off a pilot pointing at a free thief inside 120 u
        if len(w.pilots):
            PV = np.array([p.vel for p in w.pilots])
            pointing = np.sum(unit(PV[k]) * unit(P - w.pilot_pos()[k]), axis=1) > 0.85
            shy = (~laden) & (dp < 120) & pointing
            if shy.any():
                side = unit(np.cross(PV[k], [0.0, 1.0, 0.0]) + 1e-6)
                des[shy] = side[shy] * 150.0
        des += flock_terms(P, V, 8.0, 8.0, 8.0)[3] * 40
        for Q in w.predator_pos:
            if len(Q):
                f, af = flee_push(P, np.ascontiguousarray(Q), 60.0)
                des = np.where(af[:, None] > 0, unit(f) * 150.0, des)
        V = steer(V, des, np.where(laden, 200.0, 500.0), dt)
        self.vel[idx] = contain(P, V, w.R)
        self.intent[idx] = np.where((self.tclaim[idx] >= 0) & (dp < 300), 1.0, np.where(laden, 0.3, 0.0))

    def keep_micro(self):
        return False

    def absorbable(self, idx):
        return (self.carry[idx] < 0) & (self.tclaim[idx] < 0)

    def recapture(self, i, w):
        j = self.carry[i]
        if j >= 0:
            d, o = self.from_dom.get(int(j), (0, -1)); w.dom[j] = d; w.kind[j] = TRAIL; w.excl[j] = 0
            self.carry[i] = -1; self.recaptured += 1

    def threat_agents(self):
        a = np.flatnonzero(self.alive)
        act = (self.tclaim[a] >= 0) | (self.carry[a] >= 0)
        return self.pos[a], self.vel[a], np.full(len(a), self.size), act

    def render(self, out):
        a = np.flatnonzero(self.alive)
        c = np.tile([0.3, 0.3, 0.45], (len(a), 1)); c[self.tclaim[a] >= 0] = [0.85, 0.85, 1.0]
        c[self.carry[a] >= 0] = [1.0, 0.85, 0.3]
        out[self.name] = dict(pos=self.pos[a], col=c, size=np.full(len(a), self.size))


class Lurker(Guild):
    """B's ambusher (bestiary/species/lurker.py; constants kept: gape 0.9 s, lunge 380 u/s for 0.35 s, sense
    170, creep 35 u/s while unwatched, spent 3 s). New here: it ambushes FAUNA too - a grazer/locust that
    wanders inside 60 u of a settled lurker triggers the same gape-and-snap, and a caught one is eaten."""
    name = "lurker"
    body, e0, e_birth, e_max, metab = 12.0, 6.0, 40.0, 55.0, 0.012
    imax = 0.0
    cap = 120; capacity = 200
    hop = 0.0015
    size = 4.0; color = (0.75, 1.0, 0.45); threat = True
    predator = True; prey_names = ("grazer", "locust")
    a_attack, h_handle = 1.5e-4, 20.0
    switch_ref = 0.0
    creep_r = 350.0          # creep toward a pilot's line only at ambush range (B used 600 in a 450-u grove; iter 3)

    def extra_init(self):
        C = self.capacity
        self.gape = np.zeros(C); self.lunge = np.zeros(C); self.spent = np.zeros(C)
        self.seat = np.full(C, -1, np.int64); self.prev_d = np.full(C, np.inf); self.target = np.zeros((C, 3))
        self.snaps = 0; self.prey_kills = 0; self.bites = 0
        self.tk = np.zeros(C, np.int64)

    def on_spawn(self, idx, from_macro=False):
        self.gape[idx] = 0; self.lunge[idx] = 0; self.spent[idx] = 0; self.seat[idx] = -1; self.prev_d[idx] = np.inf

    hop_weight = Pack.hop_weight

    def act(self, idx, dt):
        w = self.w
        P = self.pos[idx]; V = self.vel[idx]; n = len(idx)
        dp, k = w.dist_to_pilots(P)
        # nearest prey agent
        preyP, preyRef = [], []
        for g in w.guild_list:
            if g.name in self.prey_names:
                a = np.flatnonzero(g.alive); preyP.append(g.pos[a]); preyRef += [(g, i) for i in a]
        preyP = np.concatenate(preyP) if preyP else np.zeros((0, 3))
        if len(preyP):
            jq, dmin = nearest_point(P, preyP, 200.0); jq = np.maximum(jq, 0)
        else:
            dmin = np.full(n, np.inf); jq = np.zeros(n, int)
        hungry = self.st[idx] < 0.8 * self.e_max
        closing = dp < self.prev_d[idx] - 1e-3
        self.prev_d[idx] = dp
        idle = (self.lunge[idx] <= 0) & (self.spent[idx] <= 0)
        trig_p = ((dp < 170) & closing) | (dp < 80)
        trig_f = hungry & (dmin < 60)
        trig = idle & (trig_p | trig_f)
        g = self.gape[idx]
        g = np.where(trig | (idle & (g > 0) & ((dp < 230) | (dmin < 90))), g + dt / 0.9, np.maximum(0, g - 2 * dt))
        go = idle & (g >= 1.0)
        for kk in np.flatnonzero(go):
            i = idx[kk]
            if (trig_f[kk] and (dmin[kk] < dp[kk] or not trig_p[kk])) or not len(w.pilots) or dmin[kk] < dp[kk]:
                aim = preyP[jq[kk]] - P[kk]; self.tk[i] = 1
            else:
                p = w.pilots[k[kk]]; aim = p.pos + p.vel * min(dp[kk] / 380.0, 0.5) - P[kk]; self.tk[i] = 0
            V[kk] = unit(aim) * 380.0
            self.lunge[i] = 0.35; self.snaps += 1
        g[go] = 0.0
        self.gape[idx] = g
        lunging = self.lunge[idx] > 0
        # bites during the lunge
        for kk in np.flatnonzero(lunging):
            i = idx[kk]
            if dp[kk] < 6 + 9 + 4:
                w.hit(int(k[kk]), self.name, "bite", 0.3); self.bites += 1
                self.lunge[i] = 0; self.spent[i] = 3.0; V[kk] *= 0.1
            elif dmin[kk] < 12:
                gg, j = preyRef[jq[kk]]
                if gg.alive[j]:
                    self.st[i] += gg.kill_agent(j, by="predator", to=self); self.prey_kills += 1
                    self.lunge[i] = 0; self.spent[i] = 3.0; V[kk] *= 0.1
        ending = lunging & (self.lunge[idx] - dt <= 0)
        self.lunge[idx] = np.maximum(0, self.lunge[idx] - dt)
        self.spent[idx] = np.where(ending, 3.0, np.maximum(0, self.spent[idx] - dt))
        # settle by a flora prism; creep toward the pilot's path while unwatched
        settle = (self.lunge[idx] <= 0) & (g <= 0)
        des = np.zeros_like(P)
        for kk in np.flatnonzero(settle):
            i = idx[kk]; s = self.seat[i]
            if s < 0 or not w.alive[s]:
                j = w.nearest(P[kk], 400.0, 1 << FLORA)[0]
                self.seat[i] = s = j
            if s >= 0:
                to = w.pos[s] - P[kk]; dd = np.linalg.norm(to)
                des[kk] = unit(to) * min(20.0, dd * 2.0) if dd > 10 else 0.0
        if len(w.pilots):
            PV = np.array([p.vel for p in w.pilots]); PP = w.pilot_pos()
            ahead = PP[k] + PV[k] * 3.0
            look = np.sum(unit(PV[k]) * unit(P - PP[k]), axis=1) > np.cos(np.radians(50))
            dorm = settle & (dp < self.creep_r) & (dp > 120)
            creep = dorm & ~look
            des[creep] = unit(ahead[creep] - P[creep]) * 35.0
            des[dorm & look] = 0.0; V[dorm & look] = 0.0
            self.seat[idx[creep]] = -1
        acc = np.where(lunging, 0.0, 120.0)
        V = np.where(lunging[:, None], V, steer(V, des, acc, dt))
        self.vel[idx] = contain(P, V, w.R)
        gp = np.clip(g, 0, 1)
        self.intent[idx] = np.where(self.lunge[idx] > 0, 1.0, np.where(gp > 0.02, 0.6 + 0.4 * gp, 0.0))

    def absorbable(self, idx):
        return (self.lunge[idx] <= 0) & (self.gape[idx] <= 0)

    macro_step = Pack.macro_step
    eff = 1.0
    carcass = Pack.carcass
    hunt_below = 0.8
    hunt_prey_below = 0.0

    def threat_agents(self):
        a = np.flatnonzero(self.alive)
        sz = np.where(self.gape[a] > 0, 5.6 + 3.4 * np.clip(self.gape[a], 0, 1), 4.0) + 3.0 * (self.lunge[a] > 0)
        return self.pos[a], self.vel[a], sz, (self.gape[a] > 0.05) | (self.lunge[a] > 0)

    def render(self, out):
        a = np.flatnonzero(self.alive)
        c = np.tile([0.75, 1.0, 0.45], (len(a), 1))
        gp = np.clip(self.gape[a], 0, 1)[:, None] * (self.gape[a] > 0.02)[:, None]
        c = c * (1 - gp) + np.array([0.5, 0.05, 0.1]) * gp
        c[self.lunge[a] > 0] = [1.0, 0.1, 0.1]
        out[self.name] = dict(pos=self.pos[a], col=c, size=self.threat_agents()[2])
