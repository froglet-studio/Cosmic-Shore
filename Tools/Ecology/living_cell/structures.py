"""The living cell's sessile life: flora plants, snap-trap clumps, the fortress colony, the physarum network.

None of these is LOD'd (Direction E: flora is never LOD'd; structures are prisms, which are real everywhere).
Every one is on the world's ledger:
  Flora       plants grow prisms OUT OF THE SOIL NUTRIENT N (logistic, nutrient-limited) - the base of the web
  SnapTraps   F's Venus flytrap clumps (flora/snaptrap.py): five-state jaws, telegraph glow, heliotropism.
              New here: fauna that wander into an open mouth are CAUGHT and digested into the clump's reserve,
              and the roots absorb skeleton + trail. The reserve re-buds lost prisms and buds new traps.
  Fortress    D's fortress colony (builders/fortress.py): workers steal LOOSE mass (trail, skeletons, a thief
              hoard), carry it home and wall the core; a cut wall raises alarm and is mended; defenders bite.
              New here: the workers EAT - surplus mass past a complete shell goes to a brood store that feeds
              them and funds new workers. The fortress is therefore the one predator of thieves' hoards.
  Physarum    F's slime-mould network (flora/physarum.py, Jones 2010 in 3D): agents on a trail field condense
              into tube prisms laid from a reserve; tubes DIGEST the mass they cover; an excitable wave runs
              the tubes as danger. New here: the reserve pays a maintenance cost (tubes resorb when it runs
              out), so a starving network shrinks instead of hoarding mass forever.
"""
from __future__ import annotations

import math

import numpy as np
from numba import njit

from .world import FLORA, TRAIL, SKEL, HOARD, WALL, TUBE, TRAP, K_LOOSE, K_DIGEST
from .fauna import unit


# ==========================================================================================================
class Flora:
    name = "flora"

    def __init__(self, w, n_plants=150, cap=40, vol=8.0, r=0.03, N_half=20000.0, meadows=18, shield_frac=0.12,
                 seed_frac=0.6, seed_c=3.0, recruit=0.0, N_ref=60000.0, n_max=1200):
        self.w = w; self.cap = cap; self.seed_c = seed_c; self.vol = vol; self.r = r; self.N_half = N_half
        self.shield_frac = shield_frac
        # recruitment (round 9): plants SEED new plants when the soil holds more than N_ref - the soil's sink
        # that is not a timer. Expected new plants/s = recruit * (N - N_ref) / N_ref; n_max is a backstop.
        self.recruit = recruit; self.N_ref = N_ref; self.n_max = n_max; self.recruited = 0
        cen = w.ball(meadows, 0.3 * w.R, 0.85 * w.R)
        self.pos = cen[w.rng.integers(0, meadows, n_plants)] + w.rng.normal(0, 70.0, (n_plants, 3))
        self.shielded = w.rng.random(n_plants) < shield_frac         # CHARGE plants: armoured, never food
        self.n = n_plants; self.grown = 0
        for i in range(n_plants):
            k = int(cap * seed_frac)
            for _ in range(k):
                self._lay(i)
            w.book_initial(k * vol)

    def _lay(self, i):
        w = self.w
        w.add(self.pos[i] + w.rng.normal(0, 18.0, 3), self.vol, 0, FLORA, owner=i, shield=bool(self.shielded[i]))

    def counts(self):
        w = self.w; n = w.n
        m = w.alive[:n] & (w.kind[:n] == FLORA)
        return np.bincount(w.owner[:n][m], minlength=self.n)

    def volume(self):
        w = self.w; n = w.n
        m = w.alive[:n] & (w.kind[:n] == FLORA)
        return float(w.vol[:n][m].sum())

    def grow(self, dt):
        """Logistic growth limited by the soil: expected new prisms/s = r (c+1)(1 - c/cap) N/(N+N_half)."""
        w = self.w
        c = self.counts()
        lam = self.r * (c + self.seed_c) * np.clip(1 - c / self.cap, 0, 1) * w.N / (w.N + self.N_half) * dt
        k = w.rng.poisson(lam)
        for i in np.flatnonzero(k):
            for _ in range(int(k[i])):
                if w.N < self.vol:
                    return
                w.N -= self.vol; self._lay(i); self.grown += 1
        if self.recruit > 0 and w.N > self.N_ref and self.n < self.n_max:
            k = int(w.rng.poisson(self.recruit * (w.N - self.N_ref) / self.N_ref * dt))
            c = self.counts().astype(float)
            for _ in range(min(k, self.n_max - self.n)):
                if w.N < self.vol:
                    return
                # a seed falls from a plant (weighted by its standing crop) and roots inside the shell
                par = w.rng.choice(self.n, p=(c + 1.0) / (c + 1.0).sum())
                p = self.pos[par] + w.rng.normal(0, 60.0, 3)
                rr = np.linalg.norm(p)
                p = p * np.clip(rr, 0.3 * w.R, 0.9 * w.R) / max(rr, 1e-6)
                self.pos = np.vstack([self.pos, p]); self.shielded = np.append(self.shielded, w.rng.random() < self.shield_frac)
                self.n += 1; c = np.append(c, 0.0)
                w.N -= self.vol; self._lay(self.n - 1); self.recruited += 1


# ==========================================================================================================
OPEN, PRIMING, ARMED, CLOSING, SHUT = 0, 1, 2, 3, 4


def _trap_layout():
    L = [(0, r, 0.0, False) for r in (5.0, 12.0, 19.0)]
    for side in (1, -1):
        L += [(side, r, s, False) for r in (10.0, 22.0, 34.0, 46.0) for s in (-12.0, 12.0)]
        L += [(side, 53.0, s, True) for s in (-18.0, -6.0, 6.0, 18.0)]
    return L


TL = _trap_layout()


class SnapTraps:
    """Constants from flora/snaptrap.py DEFAULTS: sense 170, t_prime 0.7, t_close 0.3, t_reset 3 (9 after a
    catch), gape 35+18 deg, mouth 55 x 24, turn 4 deg/s, root 90, absorb every 4 s, prism 12 / tooth 8."""
    name = "snaptrap"
    sense, t_prime, t_close, t_reset, t_digest = 170.0, 0.7, 0.3, 3.0, 9.0
    mouth_r = 30.0; root = 90.0; absorb_every = 4.0; pv, tv = 12.0, 8.0
    upkeep = 0.02          # vol/s per trap from the reserve (no hoarding mass forever)
    bud_cost = 8 * 12.0 * 2 + 3 * 12.0 + 8 * 8.0

    def __init__(self, w, n_clumps=5, per=6, cap=10, clump_r=70.0, centres=None, prey=()):
        self.w = w; self.cap = cap; self.clump_r = clump_r
        self.centres = w.ball(n_clumps, 0.35 * w.R, 0.8 * w.R) if centres is None else np.asarray(centres)
        self.reserve = np.zeros(n_clumps)
        self.heart = []; self.axis = []; self.clump = []; self.state = []; self.timer = []; self.slots = []
        self.ema = []; self.absorb_t = []
        self.prey = prey
        self.catches = 0; self.snaps_pilot = 0; self.fired = 0; self.buds = 0
        w.held_fns.append(lambda: float(self.reserve.sum()))
        for c in range(n_clumps):
            for _ in range(per):
                self._new_trap(c, pay=False)

    def _frame(self, a):
        up = np.array([0, 1.0, 0]) if abs(a[1]) < 0.9 else np.array([1.0, 0, 0])
        n = np.cross(a, up); n /= np.linalg.norm(n); b = np.cross(a, n)
        return n, b

    def slot_pos(self, t, k, gape_deg=35.0):
        kind, r, s, _ = TL[k]
        a = self.axis[t]; n, b = self._frame(a)
        if kind == 0:
            return self.heart[t] - a * r
        g = math.radians(gape_deg)
        return self.heart[t] + a * r * math.cos(g) + kind * n * r * math.sin(g) + b * s

    def _new_trap(self, c, pay=True):
        w = self.w
        h = self.centres[c] + w.rng.normal(0, self.clump_r * 0.6, 3)
        a = unit(w.rng.normal(size=3))
        self.heart.append(h); self.axis.append(a); self.clump.append(c); self.state.append(OPEN)
        self.timer.append(0.0); self.ema.append(a.copy()); self.absorb_t.append(w.rng.uniform(0, 4))
        t = len(self.heart) - 1
        sl = []
        for k, (kind, r, s, dg) in enumerate(TL):
            v = self.tv if dg else self.pv
            if pay:
                self.reserve[c] -= v
            else:
                w.book_initial(v)
            sl.append(w.add(self.slot_pos(t, k), v, 0, TRAP, owner=10000 + t, danger=dg))
        self.slots.append(sl)

    def heart_pos(self):
        return np.array(self.heart) if self.heart else np.zeros((0, 3))

    def mouth(self, t):
        return self.heart[t] + self.axis[t] * 32.0

    def intact(self, t):
        w = self.w
        return np.mean([w.alive[j] and w.owner[j] == 10000 + t for j in self.slots[t]])

    def step(self, dt, guilds):
        from .world import nearest_point
        w = self.w
        PP = w.pilot_pos()
        H = self.heart_pos(); nT = len(H)
        AX = np.array(self.axis); MO = H + AX * 32.0
        DP = np.linalg.norm(H[:, None, :] - PP[None], axis=2) if len(PP) else np.full((nT, 0), np.inf)
        DM = np.linalg.norm(MO[:, None, :] - PP[None], axis=2) if len(PP) else np.full((nT, 0), np.inf)
        preyP, preyRef = [], []
        for g in guilds:
            a = np.flatnonzero(g.alive)
            preyP.append(g.pos[a]); preyRef += [(g, i) for i in a]
        preyP = np.concatenate(preyP) if preyP else np.zeros((0, 3))
        pj, pd = nearest_point(MO, preyP, self.mouth_r) if len(preyP) else (np.full(nT, -1), np.full(nT, np.inf))
        self.timer = list(np.maximum(0.0, np.array(self.timer) - dt))
        for t in range(nT):
            c = self.clump[t]; st = self.state[t]
            near = np.flatnonzero(DP[t] < self.sense)
            if st == OPEN and not len(near) and pj[t] < 0:
                pass
            else:
                if len(near):
                    d = unit(PP[near].mean(0) - self.heart[t]); self.ema[t] = unit(0.98 * self.ema[t] + 0.02 * d)
                in_mouth_p = list(np.flatnonzero(DM[t] < self.mouth_r))
                prey_in = [preyRef[pj[t]]] if pj[t] >= 0 else []
                if st == OPEN:
                    can = self.intact(t) >= 0.5
                    if can and len(near):
                        self.state[t] = PRIMING; self.timer[t] = self.t_prime
                    elif can and prey_in:
                        self.state[t] = CLOSING; self.timer[t] = self.t_close; self.fired += 1
                elif st == PRIMING:
                    if not len(near):
                        self.state[t] = OPEN
                    elif self.timer[t] <= 0:
                        self.state[t] = ARMED
                elif st == ARMED:
                    if in_mouth_p or prey_in:
                        self.state[t] = CLOSING; self.timer[t] = self.t_close; self.fired += 1
                    elif not len(near):
                        self.state[t] = OPEN
                elif st == CLOSING and self.timer[t] <= 0:
                    caught = False
                    for i in in_mouth_p:
                        w.hit(int(i), self.name, "burn"); self.snaps_pilot += 1
                    for g, i in prey_in:
                        if g.alive[i]:
                            self.reserve[c] += g.kill_agent(i, by="predator", to=self); self.catches += 1; caught = True
                    self.state[t] = SHUT; self.timer[t] = self.t_digest if caught else self.t_reset
                elif st == SHUT and self.timer[t] <= 0:
                    self.state[t] = OPEN
            # heliotropism: the mouth turns 4 deg/s toward the EMA of where vessels passed
            ax = self.axis[t]; tgt = self.ema[t]
            cs = float(np.clip(ax @ tgt, -1, 1))
            if cs < 0.99999:
                ang = math.acos(cs); k = min(1.0, math.radians(4.0) * dt / ang); self.axis[t] = unit(ax + (tgt - ax) * k)
            # roots: absorb loose trail / skeleton every 4 s while the reserve is not full
            self.absorb_t[t] -= dt
            if self.absorb_t[t] <= 0:
                self.absorb_t[t] = self.absorb_every
                if self.reserve[c] < 2 * self.bud_cost:
                    j = w.nearest(self.heart[t], self.root, (1 << TRAIL) | (1 << SKEL))[0]
                    if j >= 0 and not w.excl[j]:
                        self.reserve[c] += w.eat(int(j), self.name)
                # re-bud one lost prism from the reserve
                for k, j in enumerate(self.slots[t]):
                    if not (w.alive[j] and w.owner[j] == 10000 + t):
                        v = self.tv if TL[k][3] else self.pv
                        if self.reserve[c] >= v:
                            self.reserve[c] -= v
                            self.slots[t][k] = w.add(self.slot_pos(t, k), v, 0, TRAP, owner=10000 + t, danger=TL[k][3])
                        break
        # upkeep and budding per clump; a full reserve EXUDES its excess to the soil (no hoarding mass forever)
        for c in range(len(self.centres)):
            n_t = sum(1 for x in self.clump if x == c)
            up = min(self.reserve[c], self.upkeep * n_t * dt); self.reserve[c] -= up; w.N += up
            ex = self.reserve[c] - 3 * self.bud_cost
            if ex > 0:
                self.reserve[c] -= ex; w.N += ex
            if self.reserve[c] >= self.bud_cost * 1.5 and n_t < self.cap:
                self._new_trap(c); self.buds += 1

    def threat_points(self):
        h = self.heart_pos()
        act = np.array([s in (PRIMING, ARMED, CLOSING) for s in self.state], bool)
        return h, act

    def render(self, out):
        h = self.heart_pos()
        col = np.array([[0.4, 0.75, 0.3] if s == OPEN else [1.0, 0.85, 0.2] if s in (PRIMING, ARMED) else
                        [1.0, 0.25, 0.1] if s == CLOSING else [0.35, 0.45, 0.3] for s in self.state])
        out["snaptrap"] = dict(pos=h, col=col.reshape(-1, 3), size=np.full(len(h), 14.0))


# ==========================================================================================================
class Fortress:
    """Constants from builders/fortress.py: 48 workers, core shell radius ~40 + width 7 (here a 220-site
    template at r 46), gap rule (fill where many neighbours are filled), alarm on a cut site. Workers forage
    loose mass within 600 u of the core, steal it on pickup, carry it at 60 u/s."""
    name = "fortress"
    n_sites = 220; Rs = 46.0; forage_r = 600.0; carry_v = 60.0; free_v = 90.0
    body, metab, e_feed = 4.0, 0.02, 8.0
    cap = 64
    birth_cost = 4.0 + 6.0
    intrude_r, defend_every, bite_cd, store_full = 150.0, 6, 4.0, 400.0   # iteration 1 (was 220 / every 3rd / 1.5 s / none)

    def __init__(self, w, core, n=40):
        self.w = w; self.core = np.asarray(core, float)
        k = self.n_sites; i = np.arange(k) + 0.5
        phi = np.arccos(1 - 2 * i / k); th = math.pi * (1 + 5 ** 0.5) * i
        self.sites = self.core + self.Rs * np.stack([np.cos(th) * np.sin(phi), np.sin(th) * np.sin(phi), np.cos(phi)], 1)
        D = np.linalg.norm(self.sites[:, None] - self.sites[None], axis=2)
        self.snb = [np.flatnonzero((D[s] > 0) & (D[s] < 14.0)) for s in range(k)]
        self.site_prism = np.full(k, -1, np.int64)
        C = 128
        self.pos = np.zeros((C, 3)); self.vel = np.zeros((C, 3)); self.st = np.zeros(C); self.alive = np.zeros(C, bool)
        self.carry = np.full(C, -1, np.int64); self.claim = np.full(C, -1, np.int64); self.bitecd = np.zeros(C)
        self.pos[:n] = self.core + w.rng.normal(0, 30, (n, 3)); self.st[:n] = 6.0; self.alive[:n] = True
        self.store = 60.0
        w.book_initial(n * (self.body + 6.0) + self.store)
        self.alarm = np.zeros(k)
        self.placed = 0; self.placed_trail = 0; self.from_hoard = 0; self.steals = 0; self.cuts = 0; self.repairs = 0
        self.births = 0; self.starved = 0; self.bites = 0; self.killed = 0
        self.intent = np.zeros(C)
        w.held_fns.append(self.held)

    def held(self):
        a = self.alive
        return float(self.body * a.sum() + self.st[a].sum() + self.store)

    def count(self):
        return int(self.alive.sum())

    def shell_fill(self):
        return float(self._filled().mean())

    def _filled(self):
        w = self.w; sp = self.site_prism; j = np.maximum(sp, 0)
        return (sp >= 0) & w.alive[j] & (w.kind[j] == WALL)

    def _choose_site(self, p, filled):
        w = self.w
        empty = np.flatnonzero(~filled)
        if len(empty) == 0:
            return -1
        nb = np.array([filled[self.snb[s]].sum() for s in empty])
        score = 1.0 + 2.5 * np.maximum(nb - 1, 0) / 6.0 + 5.0 * self.alarm[empty] - np.linalg.norm(self.sites[empty] - p, axis=1) / 60.0
        return int(empty[np.argmax(score + w.rng.random(len(empty)) * 0.5)])

    def kill_agent(self, i, by="pilot", to=None):
        w = self.w
        if not self.alive[i]:
            return 0.0
        self.alive[i] = False; w.crystals += 1
        j = self.carry[i]
        if j >= 0:
            w.excl[j] = 0; w.kind[j] = HOARD; self.carry[i] = -1
        if self.claim[i] >= 0:
            w.excl[self.claim[i]] = 0; self.claim[i] = -1
        st = max(0.0, float(self.st[i]))
        if to is not None:
            self.killed += 1
            return self.body + st
        w.N += st; w.add(self.pos[i].copy(), self.body, 0, SKEL)
        if by == "starve":
            self.starved += 1
        else:
            self.killed += 1
        return 0.0

    def step(self, dt):
        w = self.w
        sp = self.site_prism
        # breaches: a wall prism that is gone raises alarm at its site
        for s in range(self.n_sites):
            j = sp[s]
            if j >= 0 and not (w.alive[j] and w.kind[j] == WALL):
                sp[s] = -1; self.alarm[s] = 1.0; self.cuts += 1
        self.alarm *= math.exp(-dt / 20.0)
        idx = np.flatnonzero(self.alive)
        dp, k = w.dist_to_pilots(self.pos[idx]) if len(idx) else (np.zeros(0), np.zeros(0, int))
        pc, kc = w.dist_to_pilots(self.core[None])
        intruder = len(w.pilots) and pc[0] < self.intrude_r
        filled = self._filled(); shell_done = filled.mean() > 0.97
        des = np.zeros((len(self.pos), 3))
        for kk, i in enumerate(idx):
            p = self.pos[i]
            # metabolism from own stomach; refuel from the store at home
            b = min(self.st[i], self.metab * dt); self.st[i] -= b; w.N += b
            if self.st[i] <= 1e-9:
                self.kill_agent(i, by="starve"); continue
            home = np.linalg.norm(p - self.core)
            if self.st[i] < 3.0 and home < 70 and self.store > 0:
                f = min(self.store, self.e_feed - self.st[i]); self.store -= f; self.st[i] += f
            j = self.carry[i]
            if j >= 0:
                home_d = np.linalg.norm(p - self.core)
                s = self._choose_site(p, filled) if (not shell_done and home_d < 90) else -1
                tgt = self.sites[s] if s >= 0 else self.core
                to = tgt - p
                des[i] = unit(to) * self.carry_v
                w.pos[j] = p + self.vel[i] * dt
                if np.linalg.norm(to) < 8 and (s >= 0 or shell_done):
                    w.excl[j] = 0; self.carry[i] = -1
                    if s >= 0 and sp[s] < 0:
                        w.pos[j] = self.sites[s]; w.kind[j] = WALL; sp[s] = j; self.placed += 1; filled[s] = True
                        if self.alarm[s] > 0.05:
                            self.repairs += 1
                    else:
                        self.store += w.eat(int(j), self.name)      # surplus feeds the brood
                continue
            # defend: an intruder near the core draws the idle workers onto it (bite on contact)
            if intruder and home < 260 and kk % self.defend_every == 0:
                PPk = w.pilots[int(kc[0])].pos
                des[i] = unit(PPk - p) * 120.0
                self.intent[i] = 1.0
                self.bitecd[i] = max(0.0, self.bitecd[i] - dt)
                if np.linalg.norm(PPk - p) < 12 and self.bitecd[i] <= 0:
                    w.hit(int(kc[0]), self.name, "bite", 0.1); self.bites += 1; self.bitecd[i] = self.bite_cd
                continue
            self.intent[i] = 0.0
            c = self.claim[i]
            if c >= 0 and (not w.alive[c] or not ((K_LOOSE >> int(w.kind[c])) & 1) or w.shield[c]):
                w.excl[c] = 0; self.claim[i] = c = -1
            if c < 0 and (i + int(w.t * 10)) % 4 == 0 and (not shell_done or self.store < self.store_full):
                q = self.core + unit(p - self.core) * min(home, 200.0)
                j2 = w.nearest(q, self.forage_r - min(home, 200.0), K_LOOSE)[0]
                if j2 >= 0 and np.linalg.norm(w.pos[j2] - self.core) < self.forage_r:
                    self.claim[i] = c = j2; w.excl[j2] = 1
            if c >= 0:
                to = w.pos[c] - p
                des[i] = unit(to) * self.free_v
                if np.linalg.norm(to) < 6:
                    self.claim[i] = -1
                    if w.kind[c] == TRAIL:
                        self.placed_trail += 1
                    if w.kind[c] == HOARD:
                        self.from_hoard += 1
                    w.dom[c] = 1; self.steals += 1; self.carry[i] = c
            else:
                des[i] = unit(self.core - p + w.rng.normal(0, 40, 3)) * 25.0
        idx = np.flatnonzero(self.alive)
        if len(idx):
            dv = des[idx] - self.vel[idx]; n = np.linalg.norm(dv, axis=1, keepdims=True)
            self.vel[idx] += dv * np.minimum(1.0, 300 * dt / np.maximum(n, 1e-9))
            self.pos[idx] += self.vel[idx] * dt
        # births from the brood store
        if self.store > 3 * self.birth_cost and self.count() < self.cap:
            free = np.flatnonzero(~self.alive)
            if len(free):
                j = free[0]; self.store -= self.birth_cost; self.alive[j] = True; self.st[j] = 6.0
                self.pos[j] = self.core + w.rng.normal(0, 10, 3); self.vel[j] = 0; self.carry[j] = -1; self.claim[j] = -1
                self.births += 1

    def threat_points(self):
        a = np.flatnonzero(self.alive)
        return np.concatenate([self.core[None], self.pos[a]]), np.concatenate([[False], self.intent[a] > 0])

    def render(self, out):
        a = np.flatnonzero(self.alive)
        c = np.tile([0.95, 0.35, 0.45], (len(a), 1)); c[self.carry[a] >= 0] = [1.0, 0.7, 0.3]; c[self.intent[a] > 0] = [1.0, 0.1, 0.2]
        out["fortress"] = dict(pos=self.pos[a], col=c, size=np.full(len(a), 3.0))


# ==========================================================================================================
@njit(cache=True)
def _phys_agents(A, D, ph, T, G, h, c0, Rg, so, sa, ra, ss, dt, jit, rnd):
    n = A.shape[0]
    for i in range(n):
        d0 = D[i, 0]; d1 = D[i, 1]; d2 = D[i, 2]
        if abs(d1) < 0.9:
            ux, uy, uz = -d2, 0.0, d0
        else:
            ux, uy, uz = 0.0, d2, -d1
        un = math.sqrt(ux * ux + uy * uy + uz * uz) + 1e-9
        ux /= un; uy /= un; uz /= un
        vx = d1 * uz - d2 * uy; vy = d2 * ux - d0 * uz; vz = d0 * uy - d1 * ux
        best = -1.0; bx = d0; by = d1; bz = d2
        for k in range(5):
            if k == 0:
                sx, sy, sz = d0, d1, d2
            else:
                th = ph[i] + (k - 1) * math.pi / 2
                cs = math.cos(sa); sn = math.sin(sa)
                sx = cs * d0 + sn * (math.cos(th) * ux + math.sin(th) * vx)
                sy = cs * d1 + sn * (math.cos(th) * uy + math.sin(th) * vy)
                sz = cs * d2 + sn * (math.cos(th) * uz + math.sin(th) * vz)
            px = A[i, 0] + sx * so - c0[0]; py = A[i, 1] + sy * so - c0[1]; pz = A[i, 2] + sz * so - c0[2]
            gx = min(G - 1, max(0, int((px + Rg) / h))); gy = min(G - 1, max(0, int((py + Rg) / h))); gz = min(G - 1, max(0, int((pz + Rg) / h)))
            s = T[gx, gy, gz]
            if k == 0:
                f = s; best = s
            elif s > best:
                best = s; bx, by, bz = sx, sy, sz
        if best > f:
            d0 += (bx - d0) * ra; d1 += (by - d1) * ra; d2 += (bz - d2) * ra
        d0 += rnd[i, 0] * jit; d1 += rnd[i, 1] * jit; d2 += rnd[i, 2] * jit
        nn = math.sqrt(d0 * d0 + d1 * d1 + d2 * d2) + 1e-9
        d0 /= nn; d1 /= nn; d2 /= nn
        A[i, 0] += d0 * ss * dt; A[i, 1] += d1 * ss * dt; A[i, 2] += d2 * ss * dt
        rx = A[i, 0] - c0[0]; ry = A[i, 1] - c0[1]; rz = A[i, 2] - c0[2]
        rr = math.sqrt(rx * rx + ry * ry + rz * rz)
        if rr > Rg - 1.5 * h:
            d0 = -d0; d1 = -d1; d2 = -d2
            sc = (Rg - 2 * h) / rr
            A[i, 0] = c0[0] + rx * sc; A[i, 1] = c0[1] + ry * sc; A[i, 2] = c0[2] + rz * sc
        D[i, 0] = d0; D[i, 1] = d1; D[i, 2] = d2
        gx = min(G - 1, max(0, int((A[i, 0] - c0[0] + Rg) / h))); gy = min(G - 1, max(0, int((A[i, 1] - c0[1] + Rg) / h))); gz = min(G - 1, max(0, int((A[i, 2] - c0[2] + Rg) / h)))
        T[gx, gy, gz] += 1.0


@njit(cache=True)
def _blur_decay(T, out, diffuse, evap, inside):
    G = T.shape[0]
    for x in range(G):
        for y in range(G):
            for z in range(G):
                if not inside[x, y, z]:
                    out[x, y, z] = 0.0; continue
                s = 0.0; c = 0
                for dx in range(-1, 2):
                    X = min(G - 1, max(0, x + dx))
                    for dy in range(-1, 2):
                        Y = min(G - 1, max(0, y + dy))
                        for dz in range(-1, 2):
                            Z = min(G - 1, max(0, z + dz))
                            s += T[X, Y, Z]; c += 1
                v = (1 - diffuse) * T[x, y, z] + diffuse * s / c
                out[x, y, z] = max(0.0, v * (1 - evap))


class Physarum:
    """Constants from flora/physarum.py DEFAULTS: sensor 28 u at 30 deg, rotate 35 deg, 40 u/s, deposit 1,
    diffuse 0.5, evap 0.08, food 1.5, tube on/off 6/3, ema 0.05, prism 10, digest 0.3/s, pacemaker period
    3 s, wave 50 u/s, excited 2 ticks then refractory 4. Agents 6,000 on a 40^3 grove (F used 16k on 56^3)."""
    name = "physarum"
    so, sa, ra, ss = 28.0, math.radians(30), 35.0 / 30.0, 40.0
    diffuse, evap, food_dep, on, off, ema, pv, digest = 0.5, 0.08, 1.5, 6.0, 3.0, 0.05, 10.0, 0.3
    period, wave_speed, ex_ticks, refr = 3.0, 50.0, 2, 4
    upkeep = 0.004            # vol/s per tube from the reserve
    tube_cap = 1400
    # round 3 (feed and reproduce): `keep` is a reserve floor a tube is never laid from (except one over food);
    # `sporulate` lets a network that is starving on a bare grove resorb itself into a sclerotium and re-germinate
    # where the food is (mass carried, nothing created). 0 / False = the round-2 behaviour.
    keep = 0.0
    sporulate = False
    spore_window = 180.0      # s of digest history a starvation verdict reads
    spore_cool = 300.0        # s after germinating before it may sporulate again
    spore_lo = 0.3            # starving = reserve < spore_lo * keep and income < half the upkeep

    def __init__(self, w, centre, Rg=380.0, G=40, n_agents=6000, n_hearts=3, reserve=5000.0):
        self.w = w; self.c = np.asarray(centre, float); self.Rg = Rg; self.G = G; self.h = 2 * Rg / G
        g = (np.arange(G) + 0.5) * self.h - Rg
        X, Y, Z = np.meshgrid(g, g, g, indexing="ij")
        self.cen = self.c + np.stack([X, Y, Z], -1)
        self.inside = (X * X + Y * Y + Z * Z) < (Rg - self.h) ** 2
        self.T = np.zeros((G, G, G)); self.T2 = np.zeros_like(self.T); self.S = np.zeros_like(self.T)
        d = w.rng.normal(size=(n_agents, 3)); d /= np.linalg.norm(d, axis=1, keepdims=True)
        self.A = self.c + d * (Rg * 0.6) * np.cbrt(w.rng.random(n_agents))[:, None]
        self.D = unit(w.rng.normal(size=(n_agents, 3))); self.ph = w.rng.uniform(0, 6.28, n_agents)
        self.vox_prism = np.full((G, G, G), -1, np.int64)
        self.E = np.zeros((G, G, G), np.int64)
        self.reserve = reserve; w.book_initial(reserve)
        self.hearts = self.c + w.ball(n_hearts, 0.0, 0.5 * Rg)
        self.next_beat = w.rng.uniform(0, self.period, n_hearts)
        self.wave_acc = 0.0; self.sec = 0.0
        self.digested = 0.0; self.laid = 0; self.resorbed = 0; self.burns = 0; self.food_vox = np.zeros(0, np.int64)
        self.food_idx = np.zeros(0, np.int64)
        self.dig_hist = []; self.germ_t = 0.0; self.spores = 0; self.moves = []
        w.held_fns.append(lambda: float(self.reserve))

    def vox(self, P):
        c = np.clip(((np.asarray(P) - self.c + self.Rg) / self.h).astype(np.int64), 0, self.G - 1)
        return c

    def n_tubes(self):
        return int((self.vox_prism >= 0).sum())

    def step(self, dt):
        w = self.w
        rnd = w.rng.normal(0, 1, (len(self.A), 3))
        _phys_agents(self.A, self.D, self.ph, self.T, self.G, self.h, self.c, self.Rg, self.so, self.sa,
                     min(1.0, self.ra), self.ss, dt, 0.15, rnd)
        self.sec += dt
        if self.sec >= 1.0:
            self.sec -= 1.0
            self._second()
        if len(self.food_vox):
            fv = self.food_vox
            np.add.at(self.T, (fv[:, 0], fv[:, 1], fv[:, 2]), self.food_dep)
        _blur_decay(self.T, self.T2, self.diffuse, self.evap, self.inside)
        self.T, self.T2 = self.T2, self.T
        self.S += (self.T - self.S) * self.ema
        self._waves(dt)

    def _second(self):
        """Once a second: refresh the food list, lay / resorb tubes, digest, pay upkeep."""
        w = self.w
        f = w.within(self.c, self.Rg, K_DIGEST)
        sh = w.shield[f] if w.bug != "eat_shield" else np.zeros(len(f), bool)   # control hook: the shield ignored
        f = f[~sh & (w.excl[f] == 0)] if len(f) else f
        self.food_idx = f; self.food_vox = self.vox(w.pos[f]) if len(f) else np.zeros((0, 3), np.int64)
        tube = self.vox_prism >= 0
        # resorb tubes that faded, or whose prism someone else removed (a pilot's ram)
        dead = tube & ~w.alive[np.maximum(self.vox_prism, 0)]
        self.vox_prism[dead] = -1
        tube = self.vox_prism >= 0
        fade = np.argwhere(tube & (self.S < self.off))
        for x, y, z in fade:
            j = self.vox_prism[x, y, z]
            if w.alive[j] and w.kind[j] == TUBE:
                self.reserve += w.resorb(int(j)); self.resorbed += 1
            self.vox_prism[x, y, z] = -1
        grow = np.argwhere(~tube & self.inside & (self.S > self.on))
        if len(grow):
            grow = grow[np.argsort(-self.S[grow[:, 0], grow[:, 1], grow[:, 2]])]
        if self.keep > 0 and len(grow):
            fm = np.zeros(self.T.shape, bool)
            if len(self.food_vox):
                fm[self.food_vox[:, 0], self.food_vox[:, 1], self.food_vox[:, 2]] = True
            grow = grow[np.argsort(~fm[grow[:, 0], grow[:, 1], grow[:, 2]], kind="stable")]   # food voxels first
        for x, y, z in grow:
            if self.reserve < self.pv or self.n_tubes() >= self.tube_cap:
                break
            if self.keep > 0 and self.reserve - self.pv < self.keep and not fm[x, y, z]:
                break
            self.reserve -= self.pv
            self.vox_prism[x, y, z] = w.add(self.cen[x, y, z], self.pv, 0, TUBE, owner=20000)
            self.laid += 1
        # digest: an edible prism inside a tube voxel is absorbed with probability `digest` per second
        if len(self.food_idx):
            fv = self.food_vox
            on = self.vox_prism[fv[:, 0], fv[:, 1], fv[:, 2]] >= 0
            for j in self.food_idx[on]:
                if w.rng.random() < self.digest and w.alive[j] and not w.excl[j]:
                    v = w.eat(int(j), self.name); self.reserve += v; self.digested += v
        self.dig_hist.append(self.digested)
        if self.sporulate:
            self._maybe_sporulate()
        # upkeep: the network pays to exist; a starving network resorbs its weakest tubes
        n = self.n_tubes(); due = self.upkeep * n
        pay = min(self.reserve, due); self.reserve -= pay; w.N += pay
        short = due - pay
        if short > 1e-9 and n:
            tv = np.argwhere(self.vox_prism >= 0)
            weak = tv[np.argsort(self.S[tv[:, 0], tv[:, 1], tv[:, 2]])]
            k = int(math.ceil(short / self.pv))
            for x, y, z in weak[:k]:
                j = self.vox_prism[x, y, z]
                if w.alive[j]:
                    v = w.resorb(int(j)); take = min(v, short); w.N += take; self.reserve += v - take; short -= take
                self.vox_prism[x, y, z] = -1

    def _maybe_sporulate(self):
        w = self.w
        win = int(self.spore_window)
        if len(self.dig_hist) <= win or w.t - self.germ_t < self.spore_cool:
            return
        income = (self.dig_hist[-1] - self.dig_hist[-1 - win]) / win
        if not (self.reserve < self.spore_lo * self.keep and income < 0.5 * self.upkeep * max(self.n_tubes(), 1)):
            return
        site = self.best_site()
        if site is None:
            return
        # resorb the whole network into the sclerotium, drift there as spores, germinate
        for j in self.vox_prism[self.vox_prism >= 0]:
            if w.alive[j] and w.kind[j] == TUBE:
                self.reserve += w.resorb(int(j)); self.resorbed += 1
        self.vox_prism[:] = -1
        self.moves.append(dict(t=round(w.t, 1), frm=np.round(self.c).tolist(), to=np.round(site).tolist(),
                               reserve=round(self.reserve, 1)))
        self.place(site)
        self.spores += 1; self.germ_t = w.t; self.dig_hist = []

    def place(self, centre):
        """(Re)germinate the grove around `centre`: fields cleared, agents and hearts re-seeded there."""
        w = self.w
        old = self.c.copy(); self.c = np.asarray(centre, float)
        self.cen = self.cen - old + self.c
        self.T[:] = 0; self.T2[:] = 0; self.S[:] = 0; self.E[:] = 0
        d = w.rng.normal(size=(len(self.A), 3)); d /= np.linalg.norm(d, axis=1, keepdims=True)
        self.A[:] = self.c + d * (self.Rg * 0.6) * np.cbrt(w.rng.random(len(self.A)))[:, None]
        self.hearts = self.c + w.ball(len(self.hearts), 0.0, 0.5 * self.Rg)
        self.food_idx = np.zeros(0, np.int64); self.food_vox = np.zeros((0, 3), np.int64)

    def best_site(self, avoid=None, avoid_r=500.0):
        """The candidate centre (every plant, carcass and trail prism, pulled inside the cell) with the most
        digestible volume within 0.6 Rg - where a slime mould would land."""
        w = self.w
        f = w.within(np.zeros(3), w.R, K_DIGEST)
        f = f[~w.shield[f] & (w.excl[f] == 0)] if len(f) else f
        if not len(f):
            return None
        cand = w.pos[f[w.rng.choice(len(f), min(len(f), 300), replace=False)]]
        r = np.linalg.norm(cand, axis=1, keepdims=True)
        lim = w.R - self.Rg - 20.0
        cand = np.where(r > lim, cand / np.maximum(r, 1e-9) * lim, cand)
        if avoid is not None:
            cand = cand[np.linalg.norm(cand - avoid, axis=1) > avoid_r]
            if not len(cand):
                return None
        best, bv = None, -1.0
        for q in cand:
            g = w.within(q, 0.6 * self.Rg, K_DIGEST)
            g = g[~w.shield[g]] if len(g) else g
            v = float(w.vol[g].sum()) if len(g) else 0.0
            if v > bv:
                best, bv = q, v
        return best

    def _waves(self, dt):
        """Greenberg-Hastings on the tube voxels, stepped at wave_speed / voxel."""
        w = self.w
        self.wave_acc += dt
        step = self.h / self.wave_speed
        tube = self.vox_prism >= 0
        while self.wave_acc >= step:
            self.wave_acc -= step
            E = self.E
            ex = (E >= 1) & (E <= self.ex_ticks)
            nb = np.zeros_like(ex)
            nb[1:] |= ex[:-1]; nb[:-1] |= ex[1:]; nb[:, 1:] |= ex[:, :-1]; nb[:, :-1] |= ex[:, 1:]
            nb[:, :, 1:] |= ex[:, :, :-1]; nb[:, :, :-1] |= ex[:, :, 1:]
            newE = np.where(E > 0, E + 1, 0)
            newE[newE > self.ex_ticks + self.refr] = 0
            newE[(E == 0) & nb & tube] = 1
            self.E = newE
        self.next_beat -= dt
        for k in np.flatnonzero(self.next_beat <= 0):
            self.next_beat[k] += self.period
            tv = np.argwhere(tube)
            if len(tv):
                v = tv[np.argmin(np.linalg.norm(self.cen[tv[:, 0], tv[:, 1], tv[:, 2]] - self.hearts[k], axis=1))]
                if self.E[tuple(v)] == 0:
                    self.E[tuple(v)] = 1
        # danger flags on the excited tube prisms (a state change on the prism, not a new prism)
        exm = tube & (self.E >= 1) & (self.E <= self.ex_ticks)
        js = self.vox_prism[tube]
        w.danger[js] = False
        w.danger[self.vox_prism[exm]] = True
        self.exm = exm

    def contacts(self):
        """Burns: a pilot inside an excited tube voxel (or within 10 u of its centre)."""
        w = self.w
        for k, p in enumerate(w.pilots):
            if np.linalg.norm(p.pos - self.c) > self.Rg:
                continue
            v = self.vox(p.pos)
            x, y, z = v
            if self.vox_prism[x, y, z] >= 0 and getattr(self, "exm", None) is not None and self.exm[x, y, z]:
                if w.t - getattr(self, "_cd", {}).get(k, -1e9) > 1.0:
                    self._cd = getattr(self, "_cd", {}); self._cd[k] = w.t
                    w.hit(k, self.name, "burn"); self.burns += 1

    def threat_points(self):
        tv = np.argwhere(self.vox_prism >= 0)
        if len(tv) == 0:
            return np.zeros((0, 3)), np.zeros(0, bool)
        P = self.cen[tv[:, 0], tv[:, 1], tv[:, 2]]
        exm = getattr(self, "exm", None)
        act = exm[tv[:, 0], tv[:, 1], tv[:, 2]] if exm is not None else np.zeros(len(tv), bool)
        return P, act

    def render(self, out):
        P, act = self.threat_points()
        col = np.where(act[:, None], np.array([[1.0, 0.95, 0.4]]), np.array([[0.85, 0.65, 0.15]]))
        out["physarum"] = dict(pos=P, col=col.reshape(-1, 3), size=np.full(len(P), 6.0))
