"""The shared arena every Living Ecology experiment runs in (Tools/Ecology/PROGRAM.md).

numpy only, deterministic. It gives every direction the SAME world, pilots, mass and recorder, so their
scorecards compare. Units are world units (a Cosmic Shore cell is ~1200 u in radius; a vessel cruises at
~60-300 u/s; a tadpole is ~5 u; a prism is ~2-10 u).

    from common.arena import Arena, Pilot
    arena = Arena(seed=7)                      # a cell: membrane radius 1200, a nucleus of 200
    arena.scatter_mass(4000)                   # prism "mass" points: pos, volume, element, domain, alive
    arena.add_pilot(Pilot.wanderer())          # scripted pilots (see Pilot)
    for t in range(n): species.step(arena, dt); arena.step(dt); rec.frame(arena, species)

A SPECIES is any object with `step(arena, dt)` and `render(out)` (fills positions/colours/sizes for the
recorder). The arena never decides anything for a species - it is the world, not a director.

Mass is CONSERVED: `Arena.consume(i, by)` moves a prism's volume to whoever ate it and returns the volume;
nothing in here removes mass on a clock. Pilots never die (they are scripted probes); a species that
"hits" a pilot calls `Arena.hit(pilot, kind, amount)` and the hit is logged for the threat scorecard.
"""
from __future__ import annotations

import json
import math
from dataclasses import dataclass, field

import numpy as np

ELEMENTS = ("charge", "mass", "space", "time")


class Grid:
    """A uniform spatial hash over points in a sphere of radius R: cell size `h`. Rebuild once per step
    (O(n)); `near(p, r)` returns candidate indices (caller filters by exact distance)."""

    def __init__(self, R: float, h: float):
        self.R, self.h = R, h
        self.n = int(math.ceil(2 * R / h)) + 1
        self.order = np.zeros(0, np.int64)
        self.start = np.zeros(self.n ** 3 + 1, np.int64)

    def key(self, p: np.ndarray) -> np.ndarray:
        c = np.clip(((p + self.R) / self.h).astype(np.int64), 0, self.n - 1)
        return (c[..., 0] * self.n + c[..., 1]) * self.n + c[..., 2]

    def build(self, p: np.ndarray, mask: np.ndarray | None = None):
        k = self.key(p)
        if mask is not None:
            k = np.where(mask, k, self.n ** 3)          # masked-out points go to a sentinel bucket
        self.order = np.argsort(k, kind="stable")
        counts = np.bincount(k, minlength=self.n ** 3 + 1)
        self.start = np.concatenate([[0], np.cumsum(counts)])
        return self

    def near(self, q: np.ndarray, r: float) -> np.ndarray:
        lo = np.clip(((q - r + self.R) / self.h).astype(int), 0, self.n - 1)
        hi = np.clip(((q + r + self.R) / self.h).astype(int), 0, self.n - 1)
        out = []
        for x in range(lo[0], hi[0] + 1):
            for y in range(lo[1], hi[1] + 1):
                b = (x * self.n + y) * self.n
                s, e = self.start[b + lo[2]], self.start[b + hi[2] + 1]
                if e > s:
                    out.append(self.order[s:e])
        return np.concatenate(out) if out else np.zeros(0, np.int64)


@dataclass
class Pilot:
    """A scripted vessel - the probe every threat is scored against. Policies:
       wander   cruises between random points (the player who is not engaging)
       hunter   flies at the nearest live agent of a species (the player who is engaging)
       evader   flies away from the nearest threat (the player who is fleeing)
       skimmer  orbits mass at close range (the player farming prisms)
       circuit  loops a fixed list of waypoints (the player racing the same lanes)
    Any policy lays a TRAIL of conserved prisms in its `domain` when `trail_every` > 0."""
    policy: str = "wander"
    pos: np.ndarray = field(default_factory=lambda: np.zeros(3))
    vel: np.ndarray = field(default_factory=lambda: np.zeros(3))
    speed: float = 120.0
    turn: float = 2.0                      # rad/s
    radius: float = 6.0                    # hull radius
    goal: np.ndarray = field(default_factory=lambda: np.zeros(3))
    hits: list = field(default_factory=list)
    name: str = "pilot"
    # --- optional (additive, defaults keep the original behaviour) ---
    domain: int = 1                        # 1..3 playable domains (0 = neutral / environment, Domains.Blue)
    trail_every: float = 0.0               # seconds between trail prisms it lays; 0 = lays no trail
    trail_vol: float = 6.0                 # volume of each trail prism (a Squirrel trail prism is ~3-6)
    waypoints: list = field(default_factory=list)   # the "circuit" policy loops these
    _trail_t: float = 0.0
    _wp: int = 0

    @staticmethod
    def wanderer(speed=120.0): return Pilot("wander", speed=speed, name="wanderer")
    @staticmethod
    def hunter(speed=160.0): return Pilot("hunter", speed=speed, name="hunter")
    @staticmethod
    def evader(speed=140.0): return Pilot("evader", speed=speed, name="evader")
    @staticmethod
    def skimmer(speed=90.0): return Pilot("skimmer", speed=speed, name="skimmer")
    @staticmethod
    def circuit(waypoints, speed=140.0, name="circuit"):
        """Flies a fixed loop of waypoints forever: the player who RACES the same lanes (makes trail lanes)."""
        return Pilot("circuit", speed=speed, name=name, waypoints=[np.asarray(w, float) for w in waypoints])


class Arena:
    def __init__(self, seed: int = 7, R: float = 1200.0, nucleus: float = 200.0, grid_h: float = 40.0):
        self.rng = np.random.default_rng(seed)
        self.R, self.nucleus, self.t = R, nucleus, 0.0
        self.mass_pos = np.zeros((0, 3)); self.mass_vol = np.zeros(0)
        self.mass_elem = np.zeros(0, np.int8); self.mass_alive = np.zeros(0, bool)
        self.mass_shielded = np.zeros(0, bool)
        self.mass_grid = Grid(R, grid_h)
        self.pilots: list[Pilot] = []
        self.targets: list = []                # agent positions a hunter pilot may chase (species publish here)
        self.threats: list = []                # positions an evader flees (species publish here)
        self.log: list = []                    # (t, pilot, kind, amount)
        self.eaten: float = 0.0                # total volume consumed (conservation audit)
        # OPTIONAL pilot trails (off by default - Direction B, bestiary). When `enable_trails` is called, every
        # pilot lays one prism of `trail_vol` every `trail_spacing` u behind it, owned by that pilot (mass_owner =
        # pilot index; environment mass is -1). Vessel-laid mass is a SOURCE (an ability creates it), so the
        # conservation audit adds `trail_laid`. The skimmer then skims ENVIRONMENT mass only (never its own trail).
        self.trail_spacing: float | None = None
        self.trail_vol: float = 10.0
        self.trail_laid: float = 0.0
        self._owner = np.zeros(0, np.int16)
        self._trail_acc: dict = {}
        # --- optional ledgers (additive): domain ownership, steals, active destruction, motion ---
        self.mass_dom = np.zeros(0, np.int8)   # 0 = environment (Domains.Blue), 1..3 = playable domains
        self.mass_danger = np.zeros(0, bool)
        self.mass_trail = np.zeros(0, bool)    # True = laid by a pilot's trail (player-derived mass)
        self.laid: float = 0.0                 # volume created by lay_mass / trails
        self.scattered: float = 0.0            # volume created by scatter_mass
        self.destroyed: float = 0.0            # volume removed by an ACTIVE force other than eating (abilities)
        self.stolen: float = 0.0               # volume that changed hands (never removed)
        self.steals: int = 0
        self.moves: int = 0                    # prism position writes (move_mass) - the expensive part in game
        self.dirty: set = set()                # mass indices whose pos/style changed since the recorder last looked

    # ---- mass -------------------------------------------------------------------------------------------
    @property
    def mass_owner(self) -> np.ndarray:
        """-1 = environment, k = laid by pilot k. Padded lazily so code that grows the mass arrays directly
        stays valid."""
        n = len(self.mass_vol)
        if len(self._owner) < n:
            self._owner = np.concatenate([self._owner, np.full(n - len(self._owner), -1, np.int16)])
        return self._owner

    def enable_trails(self, spacing: float = 15.0, vol: float = 10.0):
        self.trail_spacing, self.trail_vol = spacing, vol
        return self

    def scatter_mass(self, n: int, r_lo: float = 0.3, r_hi: float = 0.9, vol=(8.0, 40.0), clumps: int = 24,
                     shielded_frac: float = 0.0):
        """`n` prism points in `clumps` plant-like clusters inside the band [r_lo, r_hi] x R (volume-uniform)."""
        rng = self.rng
        centres = self._ball(clumps, r_lo * self.R, r_hi * self.R)
        which = rng.integers(0, clumps, n)
        p = centres[which] + rng.normal(0, 25.0, (n, 3))
        self.mass_pos = np.concatenate([self.mass_pos, p])
        self.mass_vol = np.concatenate([self.mass_vol, rng.uniform(*vol, n)])
        self.mass_elem = np.concatenate([self.mass_elem, (which % 4).astype(np.int8)])
        self.mass_alive = np.concatenate([self.mass_alive, np.ones(n, bool)])
        self.mass_shielded = np.concatenate([self.mass_shielded, rng.random(n) < shielded_frac])
        self.mass_dom = np.concatenate([self.mass_dom, np.zeros(n, np.int8)])
        self.mass_danger = np.concatenate([self.mass_danger, np.zeros(n, bool)])
        self.mass_trail = np.concatenate([self.mass_trail, np.zeros(n, bool)])
        self.scattered += float(self.mass_vol[-n:].sum()) if n else 0.0
        self.mass_grid.build(self.mass_pos, self.mass_alive)

    def lay_mass(self, p, vol, elem=0, owner: int = -1) -> int:
        """Create ONE prism (a species laying mass it PAID for - e.g. a builder's wall). Returns its index."""
        self.mass_owner  # pad before growing
        self.mass_pos = np.vstack([self.mass_pos, p]); self.mass_vol = np.append(self.mass_vol, vol)
        self.mass_elem = np.append(self.mass_elem, np.int8(elem)); self.mass_alive = np.append(self.mass_alive, True)
        self.mass_shielded = np.append(self.mass_shielded, False)
        self._owner = np.append(self._owner, np.int16(owner))
    def lay_mass(self, p, vol, elem=0, dom=0, danger=False, trail=False, shielded=False) -> int:
        """Create ONE prism (a species laying mass it PAID for - e.g. a builder's wall, or a pilot's trail).
        Returns its index."""
        self.mass_pos = np.vstack([self.mass_pos, p]); self.mass_vol = np.append(self.mass_vol, vol)
        self.mass_elem = np.append(self.mass_elem, np.int8(elem)); self.mass_alive = np.append(self.mass_alive, True)
        self.mass_shielded = np.append(self.mass_shielded, bool(shielded))
        self._pad_ledgers()
        self.mass_dom[-1] = dom; self.mass_danger[-1] = bool(danger); self.mass_trail[-1] = bool(trail)
        self.laid += float(vol); self.dirty.add(len(self.mass_vol) - 1)
        return len(self.mass_vol) - 1

    def _pad_ledgers(self):
        """Keep the optional ledgers as long as mass_pos (a sibling may append to the core arrays directly)."""
        n = len(self.mass_vol)
        for name, dt in (("mass_dom", np.int8), ("mass_danger", bool), ("mass_trail", bool)):
            a = getattr(self, name)
            if len(a) < n:
                setattr(self, name, np.concatenate([a, np.zeros(n - len(a), dt)]))

    # ---- active forces other than eating (all conserve mass in the ledger sense) -------------------------
    def steal(self, i: int, dom: int, by: str = "") -> float:
        """Change hands: prism i now belongs to domain `dom`. NEVER removes mass. Shielded (and super-shielded)
        mass is immune, as are dead prisms. Returns the volume that changed hands (0 = refused / no-op)."""
        self._pad_ledgers()
        if not self.mass_alive[i] or self.mass_shielded[i] or self.mass_dom[i] == dom:
            return 0.0
        self.mass_dom[i] = dom; v = float(self.mass_vol[i])
        self.stolen += v; self.steals += 1; self.dirty.add(int(i))
        return v

    def move_mass(self, i: int, p) -> None:
        """Move a live prism (one transform write + one spatial-index notify in game). Counted in `moves`."""
        self.mass_pos[i] = p; self.moves += 1; self.dirty.add(int(i))

    def set_danger(self, i: int, on: bool = True) -> None:
        self._pad_ledgers(); self.mass_danger[i] = on; self.dirty.add(int(i))

    def destroy(self, i: int, by: str = "") -> float:
        """An ACTIVE force (a vessel ability) removes prism i. Shielded mass survives. Returns its volume."""
        if not self.mass_alive[i] or self.mass_shielded[i]:
            return 0.0
        self.mass_alive[i] = False
        v = float(self.mass_vol[i]); self.destroyed += v
        return v

    def audit(self) -> float:
        """Conservation residual: (everything ever created) - (live + eaten + destroyed). 0 means conserved."""
        return (self.scattered + self.laid) - (self.live_volume() + self.eaten + self.destroyed)

    def consume(self, i: int, by: str = "") -> float:
        if not self.mass_alive[i] or self.mass_shielded[i]:
            return 0.0
        self.mass_alive[i] = False
        v = float(self.mass_vol[i]); self.eaten += v
        return v

    def mass_near(self, q, r) -> np.ndarray:
        c = self.mass_grid.near(np.asarray(q, float), r)
        if len(c) == 0:
            return c
        c = c[self.mass_alive[c]]
        d = np.linalg.norm(self.mass_pos[c] - q, axis=1)
        return c[d <= r]

    def live_volume(self) -> float:
        return float(self.mass_vol[self.mass_alive].sum())

    # ---- pilots -----------------------------------------------------------------------------------------
    def add_pilot(self, p: Pilot):
        p.pos = self._ball(1, 0.5 * self.R, 0.8 * self.R)[0]
        d = self.rng.normal(size=3); p.vel = d / np.linalg.norm(d) * p.speed
        p.goal = self._ball(1, 0.2 * self.R, 0.9 * self.R)[0]
        self.pilots.append(p)
        return p

    def hit(self, pilot: Pilot, kind: str, amount: float = 1.0):
        pilot.hits.append((self.t, kind, amount)); self.log.append((self.t, pilot.name, kind, amount))

    def step(self, dt: float):
        for p in self.pilots:
            want = self._pilot_goal(p) - p.pos
            n = np.linalg.norm(want)
            if n > 1e-6:
                v = p.vel / max(np.linalg.norm(p.vel), 1e-6); w = want / n
                ang = math.acos(float(np.clip(v @ w, -1, 1)))
                k = min(1.0, p.turn * dt / max(ang, 1e-6))
                nd = v + (w - v) * k
                p.vel = nd / max(np.linalg.norm(nd), 1e-6) * p.speed
            p.pos = p.pos + p.vel * dt
            r = np.linalg.norm(p.pos)
            if r > self.R * 0.97:
                p.pos *= self.R * 0.97 / r
            if self.trail_spacing:
                k = self.pilots.index(p)
                acc = self._trail_acc.get(k, 0.0) + p.speed * dt
                while acc >= self.trail_spacing:
                    acc -= self.trail_spacing
                    back = p.vel / max(np.linalg.norm(p.vel), 1e-6) * (p.radius + 4.0 + acc)
                    self.lay_mass(p.pos - back, self.trail_vol, elem=k % 4, owner=k)
                    self.trail_laid += self.trail_vol
                self._trail_acc[k] = acc
            if p.trail_every > 0:
                p._trail_t += dt
                while p._trail_t >= p.trail_every:
                    p._trail_t -= p.trail_every
                    back = p.vel / max(np.linalg.norm(p.vel), 1e-6) * (p.radius * 2.0)
                    self.lay_mass(p.pos - back, p.trail_vol, elem=0, dom=p.domain, trail=True)
        self.mass_grid.build(self.mass_pos, self.mass_alive)
        self.t += dt

    def _pilot_goal(self, p: Pilot):
        if p.policy == "hunter" and len(self.targets):
            T = np.asarray(self.targets); return T[np.argmin(np.linalg.norm(T - p.pos, axis=1))]
        if p.policy == "evader" and len(self.threats):
            T = np.asarray(self.threats); d = T[np.argmin(np.linalg.norm(T - p.pos, axis=1))]
            away = p.pos - d
            return p.pos + away / max(np.linalg.norm(away), 1e-6) * 300.0
        if p.policy == "circuit" and p.waypoints:
            if np.linalg.norm(p.waypoints[p._wp] - p.pos) < 60.0:
                p._wp = (p._wp + 1) % len(p.waypoints)
            return p.waypoints[p._wp]
        if p.policy == "skimmer":
            live = np.flatnonzero(self.mass_alive & (self.mass_owner < 0))
            if len(live):
                j = live[np.argmin(np.linalg.norm(self.mass_pos[live] - p.pos, axis=1))]
                side = np.cross(p.vel, [0, 1, 0]); side /= max(np.linalg.norm(side), 1e-6)
                return self.mass_pos[j] + side * 20.0
        if np.linalg.norm(p.goal - p.pos) < 60.0:
            p.goal = self._ball(1, 0.2 * self.R, 0.9 * self.R)[0]
        return p.goal

    def _ball(self, n, r_lo, r_hi):
        d = self.rng.normal(size=(n, 3)); d /= np.linalg.norm(d, axis=1, keepdims=True)
        u = self.rng.random(n)
        r = np.cbrt(r_lo ** 3 + u * (r_hi ** 3 - r_lo ** 3))       # volume-uniform (Docs/ECOSYSTEM.md §27)
        return d * r[:, None]


class Recorder:
    """Frames for the shared HTML viewer (common/viewer.py): every `every` steps, the pilots, live mass
    (positions sent once + alive bitmask) and each species' render buffer
    {name: {"pos": (n,3), "col": (n,3) 0-1, "size": (n,)}}.

    Mass that MOVES, is LAID, or changes owner/state after the first frame (Arena.move_mass / lay_mass / steal /
    set_danger) is sent as a per-frame delta `md` = [[i, x, y, z, style], ...]; the viewer replays deltas, so
    stolen prisms visibly travel and change colour. style: 0-3 element (neutral), 4-6 domain 1-3, 7 danger,
    8 shielded."""

    def __init__(self, every: int = 2):
        self.every, self.k, self.frames = every, 0, []
        self.mass0 = None
        self.n_max = 0

    @staticmethod
    def style(arena: Arena, idx) -> np.ndarray:
        idx = np.asarray(idx, np.int64)
        arena._pad_ledgers()
        st = arena.mass_elem[idx].astype(np.int64) % 4
        dom = arena.mass_dom[idx]
        st = np.where(dom > 0, 3 + dom, st)
        st = np.where(arena.mass_danger[idx], 7, st)
        st = np.where(arena.mass_shielded[idx], 8, st)
        return st

    def frame(self, arena: Arena, species: list):
        self.k += 1
        if self.k % self.every:
            return
        if self.mass0 is None:
            self.mass0 = dict(pos=np.round(arena.mass_pos, 1).tolist(), elem=arena.mass_elem.tolist(),
                              style=self.style(arena, np.arange(len(arena.mass_vol))).tolist())
            arena.dirty = set()
        f = dict(t=round(arena.t, 2), pilots=[np.round(p.pos, 1).tolist() for p in arena.pilots],
                 alive=np.packbits(arena.mass_alive).tobytes().hex(), species={})
        dirty = getattr(arena, "dirty", None)
        if dirty:
            d = np.array(sorted(dirty), np.int64)
            st = self.style(arena, d)
            f["md"] = [[int(i), *np.round(arena.mass_pos[i], 1).tolist(), int(s)] for i, s in zip(d, st)]
            arena.dirty = set()
        self.n_max = max(self.n_max, len(arena.mass_vol))
        for s in species:
            out = {}
            s.render(out)
            for name, b in out.items():
                f["species"][name] = dict(pos=np.round(np.asarray(b["pos"]), 1).tolist(),
                                          col=np.round(np.asarray(b["col"]), 2).tolist(),
                                          size=np.round(np.asarray(b["size"]), 1).tolist())
        self.frames.append(f)

    def save(self, path: str, meta: dict | None = None):
        meta = dict(meta or {}); meta.setdefault("mass_n_max", self.n_max)
        with open(path, "w") as fh:
            json.dump(dict(meta=meta, mass0=self.mass0, frames=self.frames), fh, separators=(",", ":"))
