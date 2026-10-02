"""SEALED test families: a 5th mechanism per emotion, written BEFORE the round-3 features (tracking,
mass_log, sneak) were designed from the LOFO failures, and NOT used for any model or feature decision.
Evaluated once at the end (evaluate.py --sealed). If you change the probe after reading the sealed
number, the number is no longer sealed - say so in DISCOVERIES."""
from __future__ import annotations

import math

import numpy as np

from archetypes import Body, _unit, _rand_unit


class CuteDuckling(Body):
    """3-6 tiny round followers in single file behind the pilot, each bobbing on its own beat."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(3, 7)); s = rng.uniform(1.5, 3.0)
        super().__init__(rng, n, s, rng.uniform(1.0, 1.2), pilot.speed * 1.3 + 40, pilot.pos - pilot.vel * 0.5, 10)
        self.gap = rng.uniform(10, 18); self.f = rng.uniform(1.5, 2.8, n); self.ph = rng.uniform(0, 1, n)

    def step(self, arena, dt):
        p = arena.pilots[0]
        lead = np.vstack([p.pos[None], self.agent_pos[:-1]])
        d = lead - self.agent_pos; dist = np.linalg.norm(d, axis=1)
        want = _unit(d) * ((dist - self.gap) * 3.0)[:, None] + p.vel * 0.9
        want[:, 1] += np.cos(2 * math.pi * (self.f * self.t + self.ph)) * 2 * math.pi * self.f * 3
        self.drive(want, dt)


class PlaySpiral(Body):
    """One quick companion spiralling round the pilot's line, shooting ahead and doubling back."""
    def __init__(self, rng, pilot):
        s = rng.uniform(4, 9)
        super().__init__(rng, 1, s, rng.uniform(1.8, 2.8), pilot.speed * 1.7 + 90, pilot.pos + _rand_unit(rng) * 50)
        self.w = rng.uniform(1.5, 2.8); self.R = rng.uniform(20, 40); self.T = rng.uniform(4, 7)

    def step(self, arena, dt):
        p = arena.pilots[0]
        fwd = _unit(p.vel[None])[0] if np.linalg.norm(p.vel) > 1 else np.array([0, 0, 1.0])
        side = _unit(np.cross(fwd, [0, 1, 0])[None])[0]; up = np.cross(side, fwd)
        a = self.w * self.t
        ahead = 80 * math.sin(2 * math.pi * self.t / self.T)
        tgt = p.pos + fwd * ahead + self.R * (math.cos(a) * side + math.sin(a) * up)
        self.drive(((tgt - self.agent_pos[0]) * 3.0 + p.vel)[None], dt)


class EerieFlicker(Body):
    """A ring of figures that hold perfectly still, all facing the pilot, then all jump a short way at
    once - stop-motion, in unison - and hold still again."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(4, 12)); s = rng.uniform(3, 7)
        super().__init__(rng, n, s, rng.uniform(1.6, 2.6), pilot.speed + 120, pilot.pos)
        self.off = _rand_unit(rng, n) * rng.uniform(120, 220, n)[:, None]
        self.agent_pos = pilot.pos + self.off
        self.hold, self.jump = rng.uniform(1.5, 3.0), rng.uniform(0.3, 0.5)
        self.tgt = self.agent_pos.copy()

    def step(self, arena, dt):
        p = arena.pilots[0]
        c = self.t % (self.hold + self.jump)
        if c < dt:
            self.off = self.off * 0.93
            self.tgt = p.pos + self.off
        want = (self.tgt - self.agent_pos) * (6.0 if c < self.jump else 0.0)
        self.drive(want, dt)
        self.agent_heading = _unit(p.pos - self.agent_pos)


class MajRay(Body):
    """One vast, wide, rounded ray passing slowly overhead on a long line, a slow wingbeat."""
    def __init__(self, rng, pilot):
        s = rng.uniform(70, 160)
        super().__init__(rng, 1, s, rng.uniform(1.0, 1.4), rng.uniform(20, 35), pilot.pos + np.array([0, 1.0, 0]) * rng.uniform(150, 300) + _rand_unit(rng) * 250)
        self.dir = _unit(np.array([rng.normal(), 0.0, rng.normal()])[None])[0]; self.f = rng.uniform(0.12, 0.25)

    def step(self, arena, dt):
        p = arena.pilots[0]
        r = p.pos - self.agent_pos[0]
        if np.linalg.norm(r) > 650:
            self.dir = _unit((self.dir + 0.01 * _unit(r[None])[0])[None])[0]
        want = self.dir * self.vmax[0] + np.array([0, 8 * math.sin(2 * math.pi * self.f * self.t), 0]) + p.vel * 0.5
        self.drive(want[None], dt)


class MenPacing(Body):
    """A large hunter hanging 100-160 u off, pacing back and forth across the pilot's front like a caged
    cat, never taking its eyes off it."""
    def __init__(self, rng, pilot):
        s = rng.uniform(15, 35)
        super().__init__(rng, 1, s, rng.uniform(2.5, 4.0), pilot.speed + 60, pilot.pos + _rand_unit(rng) * 130)
        self.dist = rng.uniform(100, 160); self.span = rng.uniform(60, 120); self.T = rng.uniform(4, 7)
        self.side0 = _rand_unit(rng)

    def step(self, arena, dt):
        p = arena.pilots[0]
        fwd = _unit(p.vel[None])[0] if np.linalg.norm(p.vel) > 1 else self.side0
        side = _unit(np.cross(fwd, [0, 1, 0])[None])[0]
        x = self.span * (2 * abs((self.t / self.T) % 1 - 0.5) * 2 - 1)
        tgt = p.pos + fwd * self.dist + side * x
        self.drive(((tgt - self.agent_pos[0]) * 1.5 + p.vel)[None], dt)
        self.agent_heading = _unit((p.pos - self.agent_pos[0])[None])


class TerStampede(Body):
    """A dense column of 30-80 medium bodies thundering in from behind and straight through the pilot,
    wheeling, and coming through again."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(30, 81)); s = rng.uniform(6, 12)
        super().__init__(rng, n, s, rng.uniform(2.0, 3.0), pilot.speed * 1.6 + 90, pilot.pos - pilot.vel * 2 + _rand_unit(rng) * 250, 30)
        self.slot = rng.normal(0, 25, (n, 3)); self.c = self.agent_pos.mean(0); self.cv = np.zeros(3)

    def step(self, arena, dt):
        p = arena.pilots[0]
        d = p.pos + p.vel * 0.3 - self.c
        self.cv = self.cv + (_unit(d[None])[0] * (p.speed * 1.5 + 80) - self.cv) * min(1, 0.8 * dt)
        self.c = self.c + self.cv * dt
        want = (self.c + self.slot - self.agent_pos) * 2.0 + self.cv
        self.drive(want, dt)


SEALED = {
    "cute": CuteDuckling, "playful": PlaySpiral, "eerie": EerieFlicker,
    "majestic": MajRay, "menacing": MenPacing, "terrifying": TerStampede,
}
