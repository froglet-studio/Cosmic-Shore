"""NEUTRAL: background life that evokes nothing in particular - it neither attends to the pilot nor is vast.
Added in round 3 because a six-way forced choice had nowhere to put a small indifferent thing but 'majestic'
(the searched 'majestic' was a radius-1.5 dot). Awe needs VASTNESS (Keltner & Haidt 2003); indifference alone
is not awe. Every family keeps its anchor in the pilot's scene the way the majestic families do (it drifts with
half the pilot's velocity and is pulled back past 500 u) so 'neutral' cannot be learned as 'far away'.
NEUTRAL[0..4] are training families; SEALED_NEUTRAL is scored once with sealed-2."""
from __future__ import annotations

import math

import numpy as np

from archetypes import Body, _unit, _rand_unit


class _Scene(Body):
    def _keep(self, p, c, dt):
        r = p.pos - c; d = np.linalg.norm(r)
        return c + (p.vel * 0.5 + (_unit(r[None])[0] * (d - 450) * 0.2 if d > 500 else 0)) * dt


class NeuGrazer(_Scene):
    """1-6 small/medium creatures nosing slowly round a food patch, ignoring the pilot."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(1, 7)); s = rng.uniform(2, 10)
        c = pilot.pos + _rand_unit(rng) * rng.uniform(120, 320)
        super().__init__(rng, n, s, rng.uniform(1.2, 3.0), 30, c, 30)
        self.c = c; self.dir = _rand_unit(rng, n); self.sp = rng.uniform(4, 15, n)

    def step(self, arena, dt):
        p = arena.pilots[0]; self.c = self._keep(p, self.c, dt)
        self.dir = _unit(self.dir + 0.5 * dt * _rand_unit(self.rng, self.n) + 0.3 * dt * _unit(self.c - self.agent_pos) * (np.linalg.norm(self.c - self.agent_pos, axis=1) > 40)[:, None])
        self.drive(self.dir * self.sp[:, None] + p.vel * 0.5, dt)


class NeuWanderer(_Scene):
    """1-3 medium creatures on smooth random walks through the scene, never reacting."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(1, 4)); s = rng.uniform(4, 14)
        super().__init__(rng, n, s, rng.uniform(1.5, 3.0), 60, pilot.pos + _rand_unit(rng) * rng.uniform(100, 300), 60)
        self.c = self.agent_pos.mean(0); self.dir = _rand_unit(rng, n); self.sp = rng.uniform(10, 40, n)

    def step(self, arena, dt):
        p = arena.pilots[0]; self.c = self._keep(p, self.c, dt)
        far = (np.linalg.norm(self.agent_pos - self.c, axis=1) > 250)[:, None]
        self.dir = _unit(self.dir + 0.4 * dt * _rand_unit(self.rng, self.n) + 0.5 * dt * _unit(self.c - self.agent_pos) * far)
        self.drive(self.dir * self.sp[:, None] + p.vel * 0.5, dt)


class NeuSchool(_Scene):
    """20-50 small fish milling loosely round a point, each on its own heading, unbothered."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(20, 51)); s = rng.uniform(1.5, 4)
        c = pilot.pos + _rand_unit(rng) * rng.uniform(150, 330)
        super().__init__(rng, n, s, rng.uniform(2.0, 3.0), 30, c, 40)
        self.c = c; self.dir = _rand_unit(rng, n); self.sp = rng.uniform(6, 20, n)

    def step(self, arena, dt):
        p = arena.pilots[0]; self.c = self._keep(p, self.c, dt)
        off = self.c - self.agent_pos
        self.dir = _unit(self.dir + 0.6 * dt * _rand_unit(self.rng, self.n) + 0.02 * dt * off)
        self.drive(self.dir * self.sp[:, None] + p.vel * 0.5, dt)


class NeuDrift(_Scene):
    """3-10 small/medium bodies carried on one slow current, barely self-moving (plankton, seeds, debris)."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(3, 11)); s = rng.uniform(2, 12)
        c = pilot.pos + _rand_unit(rng) * rng.uniform(100, 300)
        super().__init__(rng, n, s, rng.uniform(1.0, 2.5), 25, c, 80)
        self.c = c; self.cur = _rand_unit(rng) * rng.uniform(3, 12); self.j = rng.uniform(0.5, 3.0)

    def step(self, arena, dt):
        p = arena.pilots[0]; self.c = self._keep(p, self.c, dt)
        want = self.cur + _rand_unit(self.rng, self.n) * self.j + (self.c - self.agent_pos) * 0.02 + p.vel * 0.5
        self.drive(want, dt)


class NeuCommuter(_Scene):
    """1-4 creatures travelling purposefully between far points across the scene - busy, not interested."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(1, 5)); s = rng.uniform(3, 12)
        super().__init__(rng, n, s, rng.uniform(1.8, 3.0), 70, pilot.pos + _rand_unit(rng) * rng.uniform(100, 300), 50)
        self.c = self.agent_pos.mean(0); self.goal = self.c + _rand_unit(rng, n) * 300; self.sp = rng.uniform(20, 50, n)

    def step(self, arena, dt):
        p = arena.pilots[0]; self.c = self._keep(p, self.c, dt)
        arrived = np.linalg.norm(self.goal - self.agent_pos, axis=1) < 30
        if arrived.any():
            self.goal[arrived] = self.c + _rand_unit(self.rng, int(arrived.sum())) * 300
        self.goal = self.goal + p.vel * 0.5 * dt
        self.drive(_unit(self.goal - self.agent_pos) * self.sp[:, None] + p.vel * 0.5, dt)


class NeuForager(_Scene):
    """SEALED: 2-8 creatures hopping patch to patch - travel, stop and feed, travel - ignoring the pilot."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(2, 9)); s = rng.uniform(2, 9)
        super().__init__(rng, n, s, rng.uniform(1.3, 2.5), 40, pilot.pos + _rand_unit(rng) * rng.uniform(120, 300), 50)
        self.c = self.agent_pos.mean(0); self.goal = self.agent_pos.copy(); self.feed = rng.uniform(0, 3, n); self.sp = rng.uniform(15, 35, n)

    def step(self, arena, dt):
        p = arena.pilots[0]; self.c = self._keep(p, self.c, dt)
        self.goal = self.goal + p.vel * 0.5 * dt
        d = self.goal - self.agent_pos; at = np.linalg.norm(d, axis=1) < 8
        self.feed = np.where(at, self.feed - dt, self.feed)
        done = at & (self.feed <= 0)
        if done.any():
            k = int(done.sum()); self.goal[done] = self.c + _rand_unit(self.rng, k) * self.rng.uniform(40, 200, k)[:, None]
            self.feed[done] = self.rng.uniform(1.5, 4, k)
        want = np.where(at[:, None], p.vel * 0.5, _unit(d) * self.sp[:, None] + p.vel * 0.5)
        self.drive(want, dt)


NEUTRAL = [NeuGrazer, NeuWanderer, NeuSchool, NeuDrift, NeuCommuter]
SEALED_NEUTRAL = NeuForager
