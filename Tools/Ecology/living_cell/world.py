"""The living cell's WORLD: one conserved mass store, one nutrient pool, pilots, and a spatial hash.

Everything that exists in the cell is in one of these ledger buckets (the closed ledger of Direction E,
hierarchy/world.py, carried over):

    prisms     live prism volume (flora, pilot trail, skeleton, hoard, fortress wall, physarum tube, trap body)
    N          soil nutrient: what metabolism burns goes here, what flora grows comes from here
    held       everything a species holds that is not a prism: bodies, stomachs, macro cohorts, reserves,
               carried prisms are still prisms (a carried prism stays in the store, it only moves)

The only SOURCES are pilot trails (an ability lays mass; booked in `laid`) and the only SINK is a pilot
destroying a prism (an ability; booked in `destroyed`). `audit()` = (initial + laid - destroyed) - (prisms +
N + held) and must be 0 to rounding every step. There is no clock anywhere that removes mass.

Locked rules enforced HERE, so no species can forget them:
  * shielded mass is never food: `eat()` refuses a shielded prism and counts the refusal attempt
  * nothing pops: species report births/expansions with a position; `continuity` keeps the closest distance
    to any pilot of anything that appeared or vanished (the gate reads it)
"""
from __future__ import annotations

import math

import numpy as np
from numba import njit

# prism kinds
FLORA, TRAIL, SKEL, HOARD, WALL, TUBE, TRAP = range(7)
KIND_NAMES = ("flora", "trail", "skel", "hoard", "wall", "tube", "trap")
K_HERB = (1 << FLORA) | (1 << TRAIL) | (1 << SKEL)          # what a grazer / locust eats
K_LOOSE = (1 << TRAIL) | (1 << SKEL) | (1 << HOARD)         # what a builder can carry off
K_DIGEST = (1 << FLORA) | (1 << TRAIL) | (1 << SKEL)        # what a physarum tube digests


# ----------------------------------------------------------------------------------------------------------
# spatial hash (numba): counting sort of LIVE prisms by cell. Rebuilt every micro tick (~1 ms at 40k prisms).
@njit(cache=True)
def _build(pos, alive, n, R, h, G, start, order):
    cnt = np.zeros(G * G * G + 1, np.int64)
    keys = np.empty(n, np.int64)
    for i in range(n):
        if alive[i]:
            x = min(G - 1, max(0, int((pos[i, 0] + R) / h)))
            y = min(G - 1, max(0, int((pos[i, 1] + R) / h)))
            z = min(G - 1, max(0, int((pos[i, 2] + R) / h)))
            k = (x * G + y) * G + z
        else:
            k = G * G * G
        keys[i] = k
        cnt[k] += 1
    s = 0
    for k in range(G * G * G + 1):
        start[k] = s
        s += cnt[k]
    start[G * G * G + 1] = s
    fill = start[:G * G * G + 1].copy()
    for i in range(n):
        k = keys[i]
        order[fill[k]] = i
        fill[k] += 1


@njit(cache=True)
def _nearest(Q, r, pos, alive, kind, shield, kmask, R, h, G, start, order, excl, born, tmin):
    """For each query point: nearest live, unshielded prism of a kind in kmask within r (or -1).
    excl: an int array of prisms already claimed (marked 1)."""
    out = np.full(len(Q), -1, np.int64)
    for q in range(len(Q)):
        best = r * r
        lo = np.empty(3, np.int64); hi = np.empty(3, np.int64)
        for a in range(3):
            lo[a] = min(G - 1, max(0, int((Q[q, a] - r + R) / h)))
            hi[a] = min(G - 1, max(0, int((Q[q, a] + r + R) / h)))
        for x in range(lo[0], hi[0] + 1):
            for y in range(lo[1], hi[1] + 1):
                for z in range(lo[2], hi[2] + 1):
                    k = (x * G + y) * G + z
                    for jj in range(start[k], start[k + 1]):
                        j = order[jj]
                        if not alive[j] or shield[j] or excl[j]:
                            continue
                        if not (kmask >> kind[j]) & 1:
                            continue
                        if born[j] < tmin:
                            continue
                        dx = pos[j, 0] - Q[q, 0]; dy = pos[j, 1] - Q[q, 1]; dz = pos[j, 2] - Q[q, 2]
                        d2 = dx * dx + dy * dy + dz * dz
                        if d2 < best:
                            best = d2; out[q] = j
    return out


@njit(cache=True)
def _within(q, r, pos, alive, kind, kmask, R, h, G, start, order):
    res = []
    lo = np.empty(3, np.int64); hi = np.empty(3, np.int64)
    for a in range(3):
        lo[a] = min(G - 1, max(0, int((q[a] - r + R) / h)))
        hi[a] = min(G - 1, max(0, int((q[a] + r + R) / h)))
    for x in range(lo[0], hi[0] + 1):
        for y in range(lo[1], hi[1] + 1):
            for z in range(lo[2], hi[2] + 1):
                k = (x * G + y) * G + z
                for jj in range(start[k], start[k + 1]):
                    j = order[jj]
                    if alive[j] and (kmask >> kind[j]) & 1:
                        dx = pos[j, 0] - q[0]; dy = pos[j, 1] - q[1]; dz = pos[j, 2] - q[2]
                        if dx * dx + dy * dy + dz * dz < r * r:
                            res.append(j)
    return np.array(res, np.int64) if len(res) else np.zeros(0, np.int64)


# ----------------------------------------------------------------------------------------------------------
class Pilot:
    """A scripted vessel (the probes of common/arena.Pilot, re-implemented on this world because the cell
    needs per-pilot domains and the EXPLORER). Policies:
        explore  tours the cell: visits a fixed shuffled list of region centres spread over the whole volume
                 (stopping nowhere) - the player who goes looking
        wander   cruises between random points
        hunter   flies at the nearest live fauna agent and kills it on contact (the engaging player)
        evader   flies away from the nearest threat
    Every pilot lays a trail prism (its domain) every `trail_spacing` u."""

    def __init__(self, policy, rng, R, speed=130.0, domain=1, name=None, trail_spacing=22.0, trail_vol=4.0):
        self.policy, self.rng, self.R = policy, rng, R
        self.speed, self.turn, self.radius = speed, 2.0, 6.0
        self.domain, self.name = domain, name or policy
        self.trail_spacing, self.trail_vol = trail_spacing, trail_vol
        d = rng.normal(size=3); d /= np.linalg.norm(d)
        self.pos = d * R * rng.uniform(0.45, 0.8)
        v = rng.normal(size=3); self.vel = v / np.linalg.norm(v) * speed
        self.goal = self._rand_point()
        self.acc = 0.0
        self.tour = []
        if policy == "explore":
            # a tour of 24 points spread over the shell 0.25-0.9 R (Fibonacci directions, shuffled radii)
            k = 24; i = np.arange(k) + 0.5
            phi = np.arccos(1 - 2 * i / k); th = math.pi * (1 + 5 ** 0.5) * i
            dirs = np.stack([np.cos(th) * np.sin(phi), np.sin(th) * np.sin(phi), np.cos(phi)], 1)
            rad = rng.uniform(0.3, 0.88, k) * R
            pts = dirs * rad[:, None]
            # greedy nearest-neighbour tour from a random start: the tour still crosses the whole cell
            left = list(range(k)); cur = int(rng.integers(k)); order = [cur]; left.remove(cur)
            while left:
                nxt = min(left, key=lambda j: np.linalg.norm(pts[j] - pts[cur])); order.append(nxt); left.remove(nxt); cur = nxt
            self.tour = [pts[j] for j in order]; self.wp = 0
            self.goal = self.tour[0]
        self.hits = []

    def _rand_point(self):
        d = self.rng.normal(size=3); d /= np.linalg.norm(d)
        return d * self.R * np.cbrt(self.rng.uniform(0.04, 0.75))

    def target(self, world):
        if self.policy == "explore":
            if np.linalg.norm(self.goal - self.pos) < 80:
                self.wp = (self.wp + 1) % len(self.tour); self.goal = self.tour[self.wp]
            return self.goal
        if self.policy == "hunter" and len(world.targets):
            T = world.targets; return T[np.argmin(np.linalg.norm(T - self.pos, axis=1))]
        if self.policy == "evader" and len(world.threats):
            T = world.threats; d = T[np.argmin(np.linalg.norm(T - self.pos, axis=1))]
            away = self.pos - d
            return self.pos + away / max(np.linalg.norm(away), 1e-6) * 300.0
        if np.linalg.norm(self.goal - self.pos) < 80:
            self.goal = self._rand_point()
        return self.goal

    def step(self, world, dt):
        want = self.target(world) - self.pos
        n = np.linalg.norm(want)
        slow = world.slow.get(id(self), 0.0)
        spd = self.speed * (1.0 - 0.6 * min(1.0, slow))
        if n > 1e-6:
            v = self.vel / max(np.linalg.norm(self.vel), 1e-6); w = want / n
            ang = math.acos(float(np.clip(v @ w, -1, 1)))
            k = min(1.0, self.turn * dt / max(ang, 1e-6))
            nd = v + (w - v) * k
            self.vel = nd / max(np.linalg.norm(nd), 1e-6) * spd
        self.pos = self.pos + self.vel * dt
        r = np.linalg.norm(self.pos)
        if r > self.R * 0.95:
            self.pos *= self.R * 0.95 / r
        if slow > 0:
            world.slow[id(self)] = max(0.0, slow - dt)
        # trail: one conserved prism of the pilot's domain every trail_spacing u (an ability = a SOURCE)
        self.acc += spd * dt
        while self.acc >= self.trail_spacing:
            self.acc -= self.trail_spacing
            back = self.vel / max(np.linalg.norm(self.vel), 1e-6) * (self.radius * 2 + self.acc)
            world.add(self.pos - back, self.trail_vol, self.domain, TRAIL, owner=world.pilots.index(self), source=True)


class World:
    def __init__(self, seed=7, R=1200.0, nucleus=200.0, cap=250_000, N0=0.0, region=200.0):
        self.rng = np.random.default_rng(seed)
        self.R, self.nucleus, self.t = R, nucleus, 0.0
        self.cap = cap
        self.pos = np.zeros((cap, 3)); self.vol = np.zeros(cap); self.dom = np.zeros(cap, np.int8)
        self.kind = np.zeros(cap, np.int8); self.alive = np.zeros(cap, np.bool_)
        self.shield = np.zeros(cap, np.bool_); self.danger = np.zeros(cap, np.bool_)
        self.owner = np.full(cap, -1, np.int32); self.born = np.zeros(cap)
        self.free = list(range(cap - 1, -1, -1)); self.n = 0           # high-water mark
        self.N = float(N0)
        self.initial = float(N0); self.laid = 0.0; self.destroyed = 0.0
        self.eaten_by = {}; self.shield_refusals = 0; self.shield_eaten = 0
        self.held_fns = []           # callables -> volume held by a species (bodies, stomachs, reserves)
        self.pilots: list[Pilot] = []
        self.targets = np.zeros((0, 3)); self.threats = np.zeros((0, 3))
        self.slow = {}
        self.events = []             # (t, pilot_index, species, kind, amount)
        self.crystals = 0            # elemental crystals dropped (every lifeform death drops one)
        self.continuity = []         # (t, species, 'in'/'out', distance to nearest pilot)
        self.bug = ""
        # hash
        self.h = 50.0; self.G = int(math.ceil(2 * R / self.h)) + 1
        self.start = np.zeros(self.G ** 3 + 2, np.int64); self.order = np.zeros(cap, np.int64)
        self.excl = np.zeros(cap, np.int8)
        # regions (the macro grid of Direction E)
        self.L = region; self.RG = int(math.ceil(2 * R / region))
        c = (np.arange(self.RG) + 0.5) * region - R
        cx, cy, cz = np.meshgrid(c, c, c, indexing="ij")
        cen = np.stack([cx.ravel(), cy.ravel(), cz.ravel()], 1)
        self.rcen_all = cen
        self.rin = np.linalg.norm(cen, axis=1) < R + 0.5 * region            # regions touching the cell
        self.nreg = len(cen)
        nb = []
        for r in range(self.nreg):
            x, y, z = r // (self.RG * self.RG), (r // self.RG) % self.RG, r % self.RG
            l = []
            for dx, dy, dz in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
                X, Y, Z = x + dx, y + dy, z + dz
                if 0 <= X < self.RG and 0 <= Y < self.RG and 0 <= Z < self.RG:
                    q = (X * self.RG + Y) * self.RG + Z
                    if self.rin[q]:
                        l.append(q)
            nb.append(l)
        self.rnb = nb

    # ---- mass ------------------------------------------------------------------------------------------
    def add(self, p, vol, dom=0, kind=FLORA, owner=-1, shield=False, danger=False, source=False):
        """Lay ONE prism. Unless `source` (a pilot ability), the volume must already have been taken out of
        some ledger bucket by the caller (N, a stomach, a reserve) - that is what keeps the audit closed."""
        i = self.free.pop()
        self.n = max(self.n, i + 1)
        self.pos[i] = p; self.vol[i] = vol; self.dom[i] = dom; self.kind[i] = kind
        self.alive[i] = True; self.shield[i] = shield; self.danger[i] = danger; self.owner[i] = owner
        self.born[i] = self.t
        if source:
            self.laid += vol
        return i

    def _remove(self, i):
        self.alive[i] = False; self.danger[i] = False; self.shield[i] = False
        self.free.append(int(i))

    def eat(self, i, by):
        """An eater takes prism i whole. Returns its volume (0 = refused). SHIELDED MASS IS NEVER FOOD."""
        if not self.alive[i]:
            return 0.0
        if self.shield[i]:
            self.shield_refusals += 1
            if self.bug != "eat_shield":
                return 0.0
            self.shield_eaten += 1
        v = float(self.vol[i]); self._remove(i)
        self.eaten_by[by] = self.eaten_by.get(by, 0.0) + v
        return v

    def destroy(self, i):
        """A pilot ability removes prism i (the one sink). Shielded survives."""
        if not self.alive[i] or self.shield[i]:
            return 0.0
        v = float(self.vol[i]); self._remove(i); self.destroyed += v
        return v

    def resorb(self, i):
        """A species takes back a prism IT laid (a tube resorbed into its reserve). Not eating: no refusal."""
        v = float(self.vol[i]); self._remove(i)
        return v

    def live_volume(self):
        n = self.n
        return float(self.vol[:n][self.alive[:n]].sum())

    def held(self):
        return float(sum(f() for f in self.held_fns))

    def audit(self):
        return (self.initial + self.laid - self.destroyed) - (self.live_volume() + self.N + self.held())

    def book_initial(self, vol):
        self.initial += vol

    # ---- queries ---------------------------------------------------------------------------------------
    def rebuild(self):
        _build(self.pos, self.alive, self.n, self.R, self.h, self.G, self.start, self.order)

    def nearest(self, Q, r, kmask, excl=None, tmin=-1e18):
        Q = np.ascontiguousarray(np.asarray(Q, float).reshape(-1, 3))
        ex = self.excl if excl is None else excl
        return _nearest(Q, float(r), self.pos, self.alive, self.kind, self.shield, int(kmask), self.R, self.h,
                        self.G, self.start, self.order, ex, self.born, float(tmin))

    def within(self, q, r, kmask=0x7F):
        return _within(np.asarray(q, float), float(r), self.pos, self.alive, self.kind, int(kmask), self.R,
                       self.h, self.G, self.start, self.order)

    def region_of(self, P):
        c = np.clip(((np.asarray(P) + self.R) / self.L).astype(np.int64), 0, self.RG - 1)
        return (c[..., 0] * self.RG + c[..., 1]) * self.RG + c[..., 2]

    def ball(self, n, r_lo, r_hi):
        d = self.rng.normal(size=(n, 3)); d /= np.linalg.norm(d, axis=1, keepdims=True)
        r = np.cbrt(r_lo ** 3 + self.rng.random(n) * (r_hi ** 3 - r_lo ** 3))
        return d * r[:, None]

    # ---- pilots ----------------------------------------------------------------------------------------
    def add_pilot(self, p):
        self.pilots.append(p); return p

    def pilot_pos(self):
        return np.array([p.pos for p in self.pilots]) if self.pilots else np.zeros((0, 3))

    def dist_to_pilots(self, P):
        PP = self.pilot_pos()
        if len(PP) == 0:
            return np.full(len(P), np.inf), np.zeros(len(P), int)
        D = np.linalg.norm(np.asarray(P)[:, None, :] - PP[None, :, :], axis=2)
        k = D.argmin(1)
        return D[np.arange(len(P)), k], k

    def hit(self, k, species, kind, amount=1.0):
        """A species struck pilot k. Hits slow the pilot (the fleet's danger slow), as they do in game."""
        self.events.append((round(self.t, 2), int(k), species, kind, float(amount)))
        p = self.pilots[k]
        self.slow[id(p)] = min(1.5, self.slow.get(id(p), 0.0) + 0.5)

    def appeared(self, species, P, how="in"):
        """Something became visible / was removed at P: log the distance to the nearest pilot (continuity)."""
        if len(self.pilots) == 0 or len(P) == 0:
            return
        d, _ = self.dist_to_pilots(np.asarray(P).reshape(-1, 3))
        self.continuity.append((self.t, species, how, float(d.min())))


# ----------------------------------------------------------------------------------------------------------
@njit(cache=True)
def flock_terms(P, V, r_count, r_align, r_sep):
    """O(n) neighbour terms over a local hash (cell = r_align): neighbour count inside r_count, mean
    neighbour velocity and mean offset inside r_align, separation push (sum of unit pushes x (1 - d/r_sep))."""
    n = P.shape[0]
    nn = np.zeros(n, np.int64); al = np.zeros((n, 3)); co = np.zeros((n, 3)); se = np.zeros((n, 3))
    if n == 0:
        return nn, al, co, se
    h = max(r_align, r_count, r_sep)
    lo = np.empty(3)
    for a in range(3):
        lo[a] = P[0, a]
        for i in range(n):
            lo[a] = min(lo[a], P[i, a])
    G = np.empty(3, np.int64)
    for a in range(3):
        hi = lo[a]
        for i in range(n):
            hi = max(hi, P[i, a])
        G[a] = int((hi - lo[a]) / h) + 1
    while G[0] * G[1] * G[2] > 200_000:
        h *= 1.5
        for a in range(3):
            hi = lo[a]
            for i in range(n):
                hi = max(hi, P[i, a])
            G[a] = int((hi - lo[a]) / h) + 1
    key = np.empty(n, np.int64)
    cnt = np.zeros(G[0] * G[1] * G[2] + 1, np.int64)
    for i in range(n):
        x = int((P[i, 0] - lo[0]) / h); y = int((P[i, 1] - lo[1]) / h); z = int((P[i, 2] - lo[2]) / h)
        key[i] = (x * G[1] + y) * G[2] + z
        cnt[key[i] + 1] += 1
    for k in range(1, len(cnt)):
        cnt[k] += cnt[k - 1]
    order = np.empty(n, np.int64); fill = cnt.copy()
    for i in range(n):
        order[fill[key[i]]] = i; fill[key[i]] += 1
    ca = np.zeros(n, np.int64)
    for i in range(n):
        x = int((P[i, 0] - lo[0]) / h); y = int((P[i, 1] - lo[1]) / h); z = int((P[i, 2] - lo[2]) / h)
        for dx in range(-1, 2):
            X = x + dx
            if X < 0 or X >= G[0]:
                continue
            for dy in range(-1, 2):
                Y = y + dy
                if Y < 0 or Y >= G[1]:
                    continue
                for dz in range(-1, 2):
                    Z = z + dz
                    if Z < 0 or Z >= G[2]:
                        continue
                    k = (X * G[1] + Y) * G[2] + Z
                    for jj in range(cnt[k], cnt[k + 1]):
                        j = order[jj]
                        if j == i:
                            continue
                        ox = P[j, 0] - P[i, 0]; oy = P[j, 1] - P[i, 1]; oz = P[j, 2] - P[i, 2]
                        d = math.sqrt(ox * ox + oy * oy + oz * oz)
                        if d < r_count:
                            nn[i] += 1
                        if d < r_align:
                            ca[i] += 1
                            al[i, 0] += V[j, 0]; al[i, 1] += V[j, 1]; al[i, 2] += V[j, 2]
                            co[i, 0] += ox; co[i, 1] += oy; co[i, 2] += oz
                        if d < r_sep and d > 1e-6:
                            wgt = (1.0 - d / r_sep) / d
                            se[i, 0] -= ox * wgt; se[i, 1] -= oy * wgt; se[i, 2] -= oz * wgt
        if ca[i] > 0:
            for a in range(3):
                al[i, a] /= ca[i]; co[i, a] /= ca[i]
    return nn, al, co, se


@njit(cache=True)
def nearest_point(Q, P, rmax):
    """For each Q: index and distance of the nearest P within rmax (brute force over a hash-free grid is
    fine for |P| up to a few thousand; this is the predator/prey lookup)."""
    nq = Q.shape[0]; npp = P.shape[0]
    idx = np.full(nq, -1, np.int64); dist = np.full(nq, np.inf)
    r2 = rmax * rmax
    for q in range(nq):
        best = r2
        for j in range(npp):
            dx = P[j, 0] - Q[q, 0]; dy = P[j, 1] - Q[q, 1]; dz = P[j, 2] - Q[q, 2]
            d2 = dx * dx + dy * dy + dz * dz
            if d2 < best:
                best = d2; idx[q] = j
        if idx[q] >= 0:
            dist[q] = math.sqrt(best)
    return idx, dist


@njit(cache=True)
def flee_push(P, Q, r):
    n = P.shape[0]; out = np.zeros((n, 3)); afraid = np.zeros(n)
    for i in range(n):
        for j in range(Q.shape[0]):
            dx = P[i, 0] - Q[j, 0]; dy = P[i, 1] - Q[j, 1]; dz = P[i, 2] - Q[j, 2]
            d = math.sqrt(dx * dx + dy * dy + dz * dz)
            if d < r and d > 1e-6:
                out[i, 0] += dx / d; out[i, 1] += dy / d; out[i, 2] += dz / d; afraid[i] = 1.0
    return out, afraid
