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
       skimmer  orbits mass at close range (the player farming prisms)"""
    policy: str = "wander"
    pos: np.ndarray = field(default_factory=lambda: np.zeros(3))
    vel: np.ndarray = field(default_factory=lambda: np.zeros(3))
    speed: float = 120.0
    turn: float = 2.0                      # rad/s
    radius: float = 6.0                    # hull radius
    goal: np.ndarray = field(default_factory=lambda: np.zeros(3))
    hits: list = field(default_factory=list)
    name: str = "pilot"

    @staticmethod
    def wanderer(speed=120.0): return Pilot("wander", speed=speed, name="wanderer")
    @staticmethod
    def hunter(speed=160.0): return Pilot("hunter", speed=speed, name="hunter")
    @staticmethod
    def evader(speed=140.0): return Pilot("evader", speed=speed, name="evader")
    @staticmethod
    def skimmer(speed=90.0): return Pilot("skimmer", speed=speed, name="skimmer")


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

    # ---- mass -------------------------------------------------------------------------------------------
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
        self.mass_grid.build(self.mass_pos, self.mass_alive)

    def lay_mass(self, p, vol, elem=0) -> int:
        """Create ONE prism (a species laying mass it PAID for - e.g. a builder's wall). Returns its index."""
        self.mass_pos = np.vstack([self.mass_pos, p]); self.mass_vol = np.append(self.mass_vol, vol)
        self.mass_elem = np.append(self.mass_elem, np.int8(elem)); self.mass_alive = np.append(self.mass_alive, True)
        self.mass_shielded = np.append(self.mass_shielded, False)
        return len(self.mass_vol) - 1

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
        self.mass_grid.build(self.mass_pos, self.mass_alive)
        self.t += dt

    def _pilot_goal(self, p: Pilot):
        if p.policy == "hunter" and len(self.targets):
            T = np.asarray(self.targets); return T[np.argmin(np.linalg.norm(T - p.pos, axis=1))]
        if p.policy == "evader" and len(self.threats):
            T = np.asarray(self.threats); d = T[np.argmin(np.linalg.norm(T - p.pos, axis=1))]
            away = p.pos - d
            return p.pos + away / max(np.linalg.norm(away), 1e-6) * 300.0
        if p.policy == "skimmer":
            live = np.flatnonzero(self.mass_alive)
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
    """Frames for the shared HTML viewer (common/viewer.html.tpl): every `every` steps, the pilots, live mass
    (positions sent once + alive bitmask deltas) and each species' render buffer
    {name: {"pos": (n,3), "col": (n,3) 0-1, "size": (n,)}}."""

    def __init__(self, every: int = 2):
        self.every, self.k, self.frames = every, 0, []
        self.mass0 = None

    def frame(self, arena: Arena, species: list):
        self.k += 1
        if self.k % self.every:
            return
        if self.mass0 is None:
            self.mass0 = dict(pos=np.round(arena.mass_pos, 1).tolist(), elem=arena.mass_elem.tolist())
        f = dict(t=round(arena.t, 2), pilots=[np.round(p.pos, 1).tolist() for p in arena.pilots],
                 alive=np.packbits(arena.mass_alive).tobytes().hex(), species={})
        for s in species:
            out = {}
            s.render(out)
            for name, b in out.items():
                f["species"][name] = dict(pos=np.round(np.asarray(b["pos"]), 1).tolist(),
                                          col=np.round(np.asarray(b["col"]), 2).tolist(),
                                          size=np.round(np.asarray(b["size"]), 1).tolist())
        self.frames.append(f)

    def save(self, path: str, meta: dict | None = None):
        with open(path, "w") as fh:
            json.dump(dict(meta=meta or {}, mass0=self.mass0, frames=self.frames), fh, separators=(",", ":"))
