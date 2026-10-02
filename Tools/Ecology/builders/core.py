"""Builders and thieves: the shared substrate for Direction D (Tools/Ecology/PROGRAM.md §4).

Everything a building/stealing colony needs, on top of common/arena.py:

  Field     a coarse scalar grid over the cell that agents WRITE and READ (stigmergy: cement pheromone,
            alarm, the vessel wake). Fields are not mass - they may decay. Mass may not.
  Lattice   the colony's construction lattice: integer sites around an anchor, each holding at most one prism
            (an arena mass index). Construction rules only ever read a site's LOCAL neighbourhood.
  Colony    struct-of-arrays workers that FORAGE loose prisms, STEAL them on pickup (Arena.steal: the prism
            changes hands, never leaves), CARRY them (Arena.move_mass, counted - the expensive part in game),
            and DEPOSIT them on lattice sites when a species' local rule says so.

The construction rule is the only thing a species overrides (`deposit_score`), plus its own threat behaviour.
No species reads a blueprint: every rule is a function of the site's neighbourhood and of the fields at the site
(Grasse 1959; Theraulaz & Bonabeau 1995 lattice swarms; Bonabeau et al. 1998 pillars/walls/royal chambers;
Ladley & Bullock 2005 logistic constraints - agents must FETCH material, which is what changes the shapes).

Ledgers every species keeps (read by builders/score.py):
  placed        prisms ever deposited into the structure
  placed_trail  ... of which came from a pilot's trail (player-derived mass)
  moves         carried-prism position writes (arena.moves also counts them)
  pickups       steals-on-pickup
"""
from __future__ import annotations

import math

import numpy as np

# 26-neighbourhood offsets and the 6 face neighbours
N26 = np.array([(x, y, z) for x in (-1, 0, 1) for y in (-1, 0, 1) for z in (-1, 0, 1) if (x, y, z) != (0, 0, 0)])
N6 = np.array([(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)])


class Field:
    """Coarse scalar field over the cell's bounding cube. deposit/sample are trilinear-free (nearest cell) -
    the game would hold the same thing as a per-cell NativeArray."""

    def __init__(self, R: float, h: float):
        self.R, self.h = R, h
        self.n = int(math.ceil(2 * R / h)) + 1
        self.a = np.zeros((self.n, self.n, self.n), np.float32)

    def idx(self, p):
        c = np.clip(((np.asarray(p, float) + self.R) / self.h).astype(np.int64), 0, self.n - 1)
        return c

    def deposit(self, p, amount: float):
        c = self.idx(p)
        if c.ndim == 1:
            self.a[c[0], c[1], c[2]] += amount
        else:
            np.add.at(self.a, (c[:, 0], c[:, 1], c[:, 2]), amount)

    def sample(self, p):
        c = self.idx(p)
        if c.ndim == 1:
            return float(self.a[c[0], c[1], c[2]])
        return self.a[c[:, 0], c[:, 1], c[:, 2]]

    def decay(self, k: float):
        self.a *= (1.0 - k)

    def blur(self, w: float = 0.15):
        """One 6-neighbour diffusion step (in place)."""
        a = self.a
        s = np.zeros_like(a)
        s[1:] += a[:-1]; s[:-1] += a[1:]
        s[:, 1:] += a[:, :-1]; s[:, :-1] += a[:, 1:]
        s[:, :, 1:] += a[:, :, :-1]; s[:, :, :-1] += a[:, :, 1:]
        self.a = (1 - w) * a + (w / 6.0) * s

    def grad(self, p):
        p = np.asarray(p, float); h = self.h
        return np.array([self.sample(p + d * h) - self.sample(p - d * h) for d in np.eye(3)]) / (2 * h)


class Lattice:
    """Construction lattice: site (i,j,k) <-> world anchor + s*(i,j,k). Dense int array of mass indices
    (-1 empty), extent `half` sites each way."""

    def __init__(self, anchor, s: float, half: int = 40, basis=None):
        self.anchor = np.asarray(anchor, float); self.s = s; self.half = half
        self.B = np.eye(3) if basis is None else np.asarray(basis, float)     # rows = the lattice axes in world
        self.m = 2 * half + 1
        self.occ = np.full((self.m,) * 3, -1, np.int64)
        self.t_placed = np.full((self.m,) * 3, -1.0, np.float32)
        self.blocked = np.zeros((self.m,) * 3, bool)        # sites the species reserves (e.g. its core)
        self.sites: dict[int, tuple] = {}                    # mass index -> site

    def site_of(self, p):
        return tuple(np.round(self.B @ (np.asarray(p, float) - self.anchor) / self.s).astype(int) + self.half)

    def pos(self, site):
        return self.anchor + ((np.asarray(site, float) - self.half) * self.s) @ self.B

    def inside(self, site):
        return all(0 <= c < self.m for c in site)

    def occupied(self, site) -> bool:
        return self.inside(site) and self.occ[site] >= 0

    def free(self, site) -> bool:
        return self.inside(site) and self.occ[site] < 0 and not self.blocked[site]

    def count(self, site, offs=N26) -> int:
        n = 0
        for o in offs:
            q = (site[0] + o[0], site[1] + o[1], site[2] + o[2])
            if self.inside(q) and self.occ[q] >= 0:
                n += 1
        return n

    def place(self, site, mass_idx, t):
        self.occ[site] = mass_idx; self.t_placed[site] = t; self.sites[mass_idx] = site

    def remove(self, mass_idx):
        s = self.sites.pop(mass_idx, None)
        if s is not None:
            self.occ[s] = -1
        return s

    def n_built(self) -> int:
        return len(self.sites)

    def occupancy_points(self):
        return np.array([self.pos(s) for s in self.sites.values()]) if self.sites else np.zeros((0, 3))


class SparseLattice(Lattice):
    """The same interface over an unbounded dict (for colonies that build all over the cell, e.g. lane traps)."""

    def __init__(self, anchor, s: float):
        self.anchor = np.asarray(anchor, float); self.s = s; self.half = 0
        self.d: dict[tuple, int] = {}
        self.tp: dict[tuple, float] = {}
        self.sites = {}

    def site_of(self, p):
        return tuple(np.round((np.asarray(p, float) - self.anchor) / self.s).astype(int))

    def pos(self, site):
        return self.anchor + np.asarray(site, float) * self.s

    def inside(self, site):
        return True

    def occupied(self, site):
        return site in self.d

    def free(self, site):
        return site not in self.d

    def count(self, site, offs=N26):
        d = self.d
        return sum(1 for o in offs if (site[0] + o[0], site[1] + o[1], site[2] + o[2]) in d)

    def place(self, site, mass_idx, t):
        self.d[site] = mass_idx; self.tp[site] = t; self.sites[mass_idx] = site

    def remove(self, mass_idx):
        s = self.sites.pop(mass_idx, None)
        if s is not None:
            self.d.pop(s, None)
        return s


class Colony:
    """Workers that forage, steal, carry and deposit. Subclasses provide `deposit_score(site) -> p in [0,1]`,
    `home(i)` (where a laden worker heads), and their own threat behaviour in `behave(arena, dt)`."""

    color = (1.0, 0.5, 0.2)

    def __init__(self, arena, n: int, anchor, dom: int = 2, s: float = 8.0, speed: float = 70.0,
                 sense: float = 140.0, frac: int = 4, name: str = "colony", seed: int = 0):
        self.rng = np.random.default_rng(seed + 991)
        self.name, self.dom, self.speed, self.sense, self.frac = name, dom, speed, sense, frac
        self.lat = Lattice(anchor, s)
        self.n = n
        self.agent_pos = np.asarray(anchor, float) + self.rng.normal(0, 20, (n, 3))
        self.agent_vel = np.zeros((n, 3))
        self.agent_size = np.full(n, 3.0)
        self.intent = np.zeros(n)
        self.alive = np.ones(n, bool)
        self.carry = np.full(n, -1, np.int64)
        self.goal = np.full(n, -1, np.int64)          # mass index the worker is heading for
        self.wander = self.rng.normal(size=(n, 3))
        self.tick = 0
        self.kills = 0; self.crystals = 0
        self.placed = 0; self.placed_trail = 0; self.pickups = 0; self.carry_moves = 0
        self.claimed: set = set()                     # mass indices some worker is already heading for
        self.taken: set = set()                       # mass indices this colony is carrying or has built with
        self.build_log = []                           # (t, n_built)
        self.pickup_pos = []                          # where every stolen prism was picked up (the supply)

    # ---------------------------------------------------------------- foraging
    def stealable(self, arena, i) -> bool:
        """Loose, live, unshielded, not this colony's structure, not already being fetched."""
        return (arena.mass_alive[i] and not arena.mass_shielded[i] and i not in self.taken
                and i not in self.claimed and not getattr(arena, "struct_owner", {}).get(int(i)))

    def forage_target(self, arena, k):
        self.queries = getattr(self, "queries", 0) + 1          # = one PrismSpatialIndex.QuerySphere in game
        c = arena.mass_near(self.agent_pos[k], self.sense)
        if len(c) == 0:
            return -1
        best, bd = -1, 1e18
        for i in c:
            if not self.stealable(arena, i):
                continue
            d = float(np.sum((arena.mass_pos[i] - self.agent_pos[k]) ** 2)) * self.prefer(arena, i)
            if d < bd:
                best, bd = int(i), d
        return best

    def prefer(self, arena, i) -> float:
        """Distance multiplier for a candidate (lower = preferred). Default: prefer a pilot's trail 4x."""
        return 0.25 if arena.mass_trail[i] else 1.0

    # ---------------------------------------------------------------- hooks
    def home(self, arena, k):
        return self.lat.anchor

    def deposit_score(self, arena, site) -> float:
        raise NotImplementedError

    def on_placed(self, arena, i, site):
        pass

    def on_pickup(self, arena, k, i):
        pass

    note = ""
    extent = 120.0

    def register(self, arena, i):
        if not hasattr(arena, "struct_owner"):
            arena.struct_owner = {}
        arena.struct_owner[int(i)] = self.name

    def sweep_destroyed(self, arena):
        """Sites whose prism an active force removed: free the site and return them (breaches)."""
        # a site is breached when its prism was destroyed OR stolen away (it changed hands back to a pilot)
        dead = [i for i in self.lat.sites if not arena.mass_alive[i] or arena.mass_dom[i] != self.dom]
        out = []
        for i in dead:
            out.append(self.lat.remove(i)); self.taken.discard(i)
            getattr(arena, "struct_owner", {}).pop(i, None)
        return out

    def on_rammed(self, arena, k, pilot):
        """A pilot flew through worker k: it dies (crystal drop), whatever it carried falls loose."""
        self.alive[k] = False; self.kills += 1; self.crystals += 1
        if self.carry[k] >= 0:
            self.taken.discard(int(self.carry[k])); self.carry[k] = -1
        if self.goal[k] >= 0:
            self.claimed.discard(int(self.goal[k])); self.goal[k] = -1

    def metrics(self, arena, minutes):
        idx = np.array(list(self.lat.sites.keys()), np.int64)
        trail_frac = float(arena.mass_trail[idx].mean()) if len(idx) else 0.0
        return dict(built=self.lat.n_built(), placed=self.placed, placed_trail=self.placed_trail,
                    trail_frac=round(trail_frac, 3), build_per_min=round(self.placed / minutes, 1),
                    pickups=self.pickups, carry_moves_per_s=round(self.carry_moves / (minutes * 60), 1),
                    workers_alive=int(self.alive.sum()), kills=self.kills,
                    queries_per_s=round(getattr(self, "queries", 0) / (minutes * 60), 1))

    def behave(self, arena, dt):
        pass

    # ---------------------------------------------------------------- one step
    def steer(self, k, target, dt, speed=None):
        d = target - self.agent_pos[k]; n = np.linalg.norm(d)
        sp = self.speed if speed is None else speed
        v = d / max(n, 1e-6) * min(sp, n / max(dt, 1e-6))
        self.agent_vel[k] = 0.7 * self.agent_vel[k] + 0.3 * v

    def try_deposit(self, arena, k):
        """Look at the lattice sites around this worker; deposit on the best one by the species' LOCAL rule."""
        here = self.lat.site_of(self.agent_pos[k])
        best, bp = None, 0.0
        for o in N26[self.rng.choice(26, 8, replace=False)]:
            q = (here[0] + o[0], here[1] + o[1], here[2] + o[2])
            if not self.lat.free(q):
                continue
            p = self.deposit_score(arena, q)
            if p > bp:
                best, bp = q, p
        if best is not None and self.rng.random() < bp:
            i = int(self.carry[k])
            arena.move_mass(i, self.lat.pos(best))     # settle: in game a GPU flight stamp, collider final now
            self.lat.place(best, i, arena.t)
            self.carry[k] = -1; self.placed += 1; self.register(arena, i)
            if arena.mass_trail[i]:
                self.placed_trail += 1
            self.on_placed(arena, i, best)
            return True
        return False

    def step(self, arena, dt):
        self.tick += 1
        R = arena.R
        for k in range(self.n):
            if not self.alive[k]:
                continue
            refresh = (k + self.tick) % self.frac == 0
            if self.carry[k] >= 0:
                if not arena.mass_alive[self.carry[k]]:            # destroyed in our jaws by a pilot
                    self.taken.discard(int(self.carry[k])); self.carry[k] = -1
                    continue
                self.steer(k, self.home(arena, k), dt)
                self.try_deposit(arena, k)
            else:
                g = self.goal[k]
                if g >= 0 and (not arena.mass_alive[g] or int(g) in self.taken):
                    self.claimed.discard(int(g)); self.goal[k] = g = -1
                if g < 0 and refresh and self.wants_material(arena, k):
                    g = self.forage_target(arena, k)
                    if g >= 0:
                        self.goal[k] = g; self.claimed.add(g)
                if g >= 0:
                    self.steer(k, arena.mass_pos[g], dt)
                    if np.linalg.norm(arena.mass_pos[g] - self.agent_pos[k]) < 6.0:
                        arena.steal(g, self.dom, by=self.name)      # changes hands (refused if shielded)
                        self.claimed.discard(int(g)); self.goal[k] = -1
                        if arena.mass_dom[g] == self.dom:
                            self.carry[k] = g; self.taken.add(int(g)); self.pickups += 1
                            self.pickup_pos.append(arena.mass_pos[g].copy())
                            self.on_pickup(arena, k, g)
                else:
                    if refresh:
                        w = self.wander[k] + self.rng.normal(0, 0.5, 3)
                        self.wander[k] = w / max(np.linalg.norm(w), 1e-6)
                    self.steer(k, self.agent_pos[k] + self.wander[k] * 50 + self.forage_bias(arena, k), dt,
                               speed=self.speed * 0.6)
        self.behave(arena, dt)
        self.agent_pos = self.agent_pos + self.agent_vel * dt
        r = np.linalg.norm(self.agent_pos, axis=1); out = r > R * 0.95
        self.agent_pos[out] *= (R * 0.95 / r[out])[:, None]
        # carried prisms ride their carrier (live gameplay data: the mover contract, one write each)
        for k in np.flatnonzero(self.alive & (self.carry >= 0)):
            c = int(self.carry[k]); tgt = self.agent_pos[k] + np.array([0, -4.0, 0])
            if np.sum((arena.mass_pos[c] - tgt) ** 2) > 0.25:      # a carrier at rest costs nothing
                arena.move_mass(c, tgt); self.carry_moves += 1
        self.build_log.append((round(arena.t, 2), self.lat.n_built()))

    def forage_bias(self, arena, k):
        return np.zeros(3)

    def defend(self, arena, dt, centre, alarm_r, guard_r=None, strike_at=0.6, rise=0.6, fall=0.3, cooldown=1.5,
               caste=None):
        """Shared colony defence with a readable escalation (no branch picks a behaviour; one alarm level does):
        a pilot inside `alarm_r` of `centre` raises the colony's ALARM (local: the workers that see it). Below
        `strike_at` the unladen workers near home form a GUARD SCREEN between the core and the pilot (the
        telegraph - the swarm visibly rises to meet you); at/above it they STRIKE and sting on contact."""
        if not hasattr(self, "alarm_level"):
            self.alarm_level = 0.0; self.cool = np.zeros(self.n)
        guard_r = guard_r or alarm_r * 0.5
        self.cool -= dt; self.intent[:] = 0.0
        inside = [p for p in arena.pilots if np.linalg.norm(p.pos - centre) < alarm_r]
        cap = 1.0 / getattr(self, "defend_caste", None) if getattr(self, "defend_caste", None) else 1.0
        # the stimulus keeps ACCUMULATING while an intruder stays (up to 1/caste): a pass-through recruits the
        # low-threshold few, a siege recruits everyone - escalating recruitment, not a fixed caste
        self.alarm_level = min(cap, self.alarm_level + rise * dt) if inside else max(0.0, self.alarm_level - fall * dt)
        if self.alarm_level <= 0:
            return
        p = min(inside or arena.pilots, key=lambda q: np.linalg.norm(q.pos - centre))
        d = np.linalg.norm(self.agent_pos - p.pos, axis=1)
        dh = np.linalg.norm(self.agent_pos - centre, axis=1)
        ks = np.flatnonzero(self.alive & (self.carry < 0) & (dh < alarm_r * 2.2))
        caste = getattr(self, "defend_caste", None) if caste is None else caste
        if caste is not None:
            # RESPONSE THRESHOLDS (Bonabeau, Theraulaz & Deneubourg 1996): each worker has a fixed threshold; it
            # answers the alarm only if alarm_level exceeds it. With thresholds spread over [0, 1/caste] at most a
            # `caste` fraction ever defends - the rest keep fetching material, so defence stops starving repair.
            if not hasattr(self, "theta"):
                self.theta = self.rng.random(self.n) / max(caste, 1e-6)
            ks = ks[self.theta[ks] < self.alarm_level]
        to = p.pos - centre; to = to / max(np.linalg.norm(to), 1e-6)
        for k in ks:
            if self.alarm_level < strike_at:
                # screen: a loose shell between core and intruder (each worker its own offset on the screen)
                off = self.wander[k] - to * (self.wander[k] @ to)
                self.steer(k, centre + to * guard_r + off * guard_r * 0.6, dt, speed=self.speed * 1.4)
                self.intent[k] = 0.25 + 0.5 * self.alarm_level / strike_at
            else:
                self.steer(k, p.pos + p.vel * 0.25, dt, speed=self.speed * 1.8)
                self.intent[k] = 1.0
                if d[k] < p.radius + 4 and self.cool[k] <= 0:
                    arena.hit(p, "sting"); self.cool[k] = cooldown

    def wants_material(self, arena, k) -> bool:
        """Does an unladen worker pick material up now? (A trap builder only fetches once it smells a lane.)"""
        return True

    # ---------------------------------------------------------------- render
    def render(self, out):
        a = self.alive
        col = np.tile(self.color, (a.sum(), 1)).astype(float)
        col[self.carry[a] >= 0] = (1.0, 1.0, 0.6)
        out[self.name] = dict(pos=self.agent_pos[a], col=col, size=self.agent_size[a])


def structure_signature(points, centre, bins=10, extent=None):
    """A rotation-sensitive occupancy histogram of a structure around its anchor (for the replayability check):
    a bins^3 boolean grid over [centre - extent, centre + extent]."""
    if len(points) == 0:
        return np.zeros((bins,) * 3, bool)
    P = np.asarray(points) - centre
    ext = extent or float(np.abs(P).max() + 1e-6)
    c = np.clip(((P / ext + 1) * 0.5 * bins).astype(int), 0, bins - 1)
    g = np.zeros((bins,) * 3, bool); g[c[:, 0], c[:, 1], c[:, 2]] = True
    return g


def shape_stats(points, centre):
    """Rotation-INVARIANT shape descriptors: count, radius mean/std, inertia eigenvalue ratios, number of
    clusters ('pillars') at the colony's lattice scale."""
    if len(points) < 4:
        return dict(n=len(points))
    P = np.asarray(points) - centre
    r = np.linalg.norm(P, axis=1)
    C = np.cov((P - P.mean(0)).T); ev = np.sort(np.linalg.eigvalsh(C))[::-1]
    return dict(n=len(points), r_mean=round(float(r.mean()), 1), r_std=round(float(r.std()), 1),
                aniso=round(float(ev[2] / max(ev[0], 1e-6)), 3), flat=round(float(ev[1] / max(ev[0], 1e-6)), 3))


def components(lat: Lattice):
    """Connected components of the built lattice (26-connectivity). Returns sizes, largest first."""
    seen = set(); sizes = []
    occ = {s for s in lat.sites.values()}
    for s in occ:
        if s in seen:
            continue
        stack = [s]; seen.add(s); n = 0
        while stack:
            q = stack.pop(); n += 1
            for o in N26:
                r = (q[0] + o[0], q[1] + o[1], q[2] + o[2])
                if r in occ and r not in seen:
                    seen.add(r); stack.append(r)
        sizes.append(n)
    return sorted(sizes, reverse=True)
