"""SEALED-2: a 6th mechanism per emotion, written after sealed-1 was spent (it becomes training family 5)
and BEFORE any 5-family model was fitted. Scored once by `evaluate.py --sealed2`."""
from __future__ import annotations

import math

import numpy as np

from archetypes import Body, _unit, _rand_unit, _sep


class CuteTumble(Body):
    """2-4 tiny round things rolling round each other in a little knot that trails the pilot slowly."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(2, 5)); s = rng.uniform(1.8, 3.5)
        super().__init__(rng, n, s, rng.uniform(1.0, 1.15), pilot.speed * 1.2 + 30, pilot.pos + _rand_unit(rng) * 50, 8)
        self.w = rng.uniform(2.0, 4.0); self.r = rng.uniform(4, 9); self.ph = np.arange(n) / n * 2 * math.pi
        self.back = rng.uniform(25, 45); self.axis = _rand_unit(rng)

    def step(self, arena, dt):
        p = arena.pilots[0]
        fwd = _unit(p.vel[None])[0] if np.linalg.norm(p.vel) > 1 else np.array([0, 0, 1.0])
        c = p.pos - fwd * self.back
        e1 = _unit(np.cross(self.axis, [1, 0, 0])[None])[0]; e2 = np.cross(self.axis, e1)
        a = self.w * self.t + self.ph
        tgt = c + self.r * (np.cos(a)[:, None] * e1 + np.sin(a)[:, None] * e2)
        self.drive((tgt - self.agent_pos) * 3.0 + p.vel, dt)


class PlaySkipper(Body):
    """One medium sprite skipping in long leaping arcs round the pilot, sometimes racing alongside it."""
    def __init__(self, rng, pilot):
        s = rng.uniform(4, 8)
        super().__init__(rng, 1, s, rng.uniform(1.6, 2.4), pilot.speed * 1.6 + 90, pilot.pos + _rand_unit(rng) * 80)
        self.T = rng.uniform(1.2, 2.0); self.H = rng.uniform(25, 50); self.rw = rng.uniform(0.2, 0.4)

    def step(self, arena, dt):
        p = arena.pilots[0]
        a = 2 * math.pi * self.rw * self.t
        hop = self.H * abs(math.sin(math.pi * self.t / self.T))
        tgt = p.pos + np.array([70 * math.cos(a), hop, 70 * math.sin(a)])
        self.drive(((tgt - self.agent_pos[0]) * 3.0 + p.vel)[None], dt)


class EerieCarousel(Body):
    """A ring of identical figures turning rigidly round the pilot at one constant rate, all facing in."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(6, 16)); s = rng.uniform(3, 7)
        super().__init__(rng, n, s, rng.uniform(1.6, 2.6), pilot.speed + 60, pilot.pos)
        self.R = rng.uniform(100, 200); self.w = rng.uniform(0.15, 0.35); self.ph = np.arange(n) / n * 2 * math.pi
        self.axis = _unit((np.array([0, 1.0, 0]) + rng.normal(0, 0.15, 3))[None])[0]

    def step(self, arena, dt):
        p = arena.pilots[0]
        e1 = _unit(np.cross(self.axis, [1, 0, 0])[None])[0]; e2 = np.cross(self.axis, e1)
        a = self.w * self.t + self.ph
        tgt = p.pos + self.R * (np.cos(a)[:, None] * e1 + np.sin(a)[:, None] * e2)
        self.drive((tgt - self.agent_pos) * 4.0 + p.vel, dt)
        self.agent_heading = _unit(p.pos - self.agent_pos)


class MajProcession(Body):
    """5-12 large bodies in a slow single-file procession crossing far off, each following the one ahead."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(5, 13)); s = rng.uniform(25, 50)
        c = pilot.pos + _rand_unit(rng) * rng.uniform(300, 450)
        super().__init__(rng, n, s, rng.uniform(2.0, 3.2), rng.uniform(20, 35), c, 1)
        self.dir = _rand_unit(rng); self.gap = s * 3.0
        self.agent_pos = c - self.dir[None] * self.gap * np.arange(n)[:, None]
        self.turn = rng.uniform(0.03, 0.08)

    def step(self, arena, dt):
        p = arena.pilots[0]
        head = self.agent_pos[0]; r = p.pos - head
        self.dir = _unit((self.dir + self.turn * dt * np.cross([0, 1, 0], self.dir) + 0.005 * _unit(r[None])[0] * (np.linalg.norm(r) > 500))[None])[0]
        lead = np.vstack([head[None] + self.dir * 40, self.agent_pos[:-1]])
        d = lead - self.agent_pos; dist = np.linalg.norm(d, axis=1)
        want = _unit(d) * np.clip((dist - self.gap) * 0.8 + self.vmax.mean(), 0, None)[:, None] + p.vel * 0.5
        self.drive(want, dt)


class MenOverhead(Body):
    """A big body hanging directly above the pilot, matching it, slowly sinking toward it and rising again."""
    def __init__(self, rng, pilot):
        s = rng.uniform(30, 70)
        super().__init__(rng, 1, s, rng.uniform(1.8, 3.0), pilot.speed * 1.3 + 40, pilot.pos + np.array([0, 250.0, 0]))
        self.h0, self.h1 = rng.uniform(200, 300), rng.uniform(80, 130); self.T = rng.uniform(6, 10)

    def step(self, arena, dt):
        p = arena.pilots[0]
        h = self.h1 + (self.h0 - self.h1) * (0.5 + 0.5 * math.cos(2 * math.pi * self.t / self.T))
        tgt = p.pos + np.array([0, h, 0])
        self.drive(((tgt - self.agent_pos[0]) * 1.2 + p.vel)[None], dt)
        self.agent_heading = _unit((p.pos - self.agent_pos[0])[None])


class TerRise(Body):
    """A giant rising from far below straight at the pilot at full speed, sweeping past, sinking away to
    rise again."""
    def __init__(self, rng, pilot):
        s = rng.uniform(70, 140)
        super().__init__(rng, 1, s, rng.uniform(1.8, 3.0), pilot.speed * 1.4 + 70, pilot.pos + np.array([0, -450.0, 0]))
        self.cyc = rng.uniform(6, 9)

    def step(self, arena, dt):
        p = arena.pilots[0]
        c = self.t % self.cyc
        me = self.agent_pos[0]
        if c < self.cyc * 0.45:
            want = _unit((p.pos - me)[None])[0] * self.vmax[0]
            self.intent = np.array([1.0])
        else:
            want = (p.pos + np.array([0, -450.0, 0]) - me) * 0.6 + p.vel
            self.intent = np.array([0.0])
        self.drive(want[None], dt)


SEALED2 = {
    "cute": CuteTumble, "playful": PlaySkipper, "eerie": EerieCarousel,
    "majestic": MajProcession, "menacing": MenOverhead, "terrifying": TerRise,
}
