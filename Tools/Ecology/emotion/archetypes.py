"""The reference set: synthetic motions that are CLEAR archetypes of six emotions (Direction C).

Three generator FAMILIES per emotion, each a different mechanism for the same feeling, so the probe can be
tested by holding a whole family out (does it recognise "terrifying" from encirclement when it only ever saw
a looming swarm and a charging giant?). Every family draws its parameters from a range per variant.

Labels are OURS, written from the literature (emotion/LITERATURE.md); they are not human ratings. The
viewer collects human ratings so the labels can be checked (emotion/viewer).

Laws every generator keeps (the same ones the search is held to): positions come only from integrating a
velocity under a speed cap and an acceleration cap; nothing teleports; a body's acceleration cap falls with
size (a whale cannot snap-turn).
"""
from __future__ import annotations

import math

import numpy as np

EMOTIONS = ("cute", "playful", "eerie", "majestic", "menacing", "terrifying")


def accel_cap(size):
    """u/s^2 a body of radius `size` may use: 600 at radius 3, falling as size^-0.5 (a law, not a style)."""
    return 600.0 * math.sqrt(3.0 / max(size, 0.5))


def _unit(v):
    n = np.linalg.norm(v, axis=-1, keepdims=True)
    return v / np.maximum(n, 1e-9)


def _rand_unit(rng, n=None):
    d = rng.normal(size=(3,) if n is None else (n, 3))
    return _unit(d)


class Body:
    """n agents with pos/vel/size/aspect; `drive(want_vel)` integrates under the caps."""

    def __init__(self, rng, n, size, aspect, vmax, centre, spread=20.0):
        self.rng = rng
        self.n = n
        self.agent_size = np.broadcast_to(np.asarray(size, float), (n,)).copy()
        self.agent_aspect = np.broadcast_to(np.asarray(aspect, float), (n,)).copy()
        self.agent_pos = centre + rng.normal(0, spread, (n, 3))
        self.agent_vel = np.zeros((n, 3))
        self.agent_heading = None              # None = heading follows velocity (the recorder holds it)
        self.vmax = np.broadcast_to(np.asarray(vmax, float), (n,)).copy()
        self.amax = np.array([accel_cap(s) for s in self.agent_size])
        self.t = 0.0
        self.kills = 0; self.crystals = 0; self.intent = np.zeros(n)

    def drive(self, want, dt):
        want = np.asarray(want, float)
        sp = np.linalg.norm(want, axis=1)
        want = want * np.minimum(1.0, self.vmax / np.maximum(sp, 1e-9))[:, None]
        dv = want - self.agent_vel
        m = np.linalg.norm(dv, axis=1)
        dv *= np.minimum(1.0, self.amax * dt / np.maximum(m, 1e-9))[:, None]
        self.agent_vel = self.agent_vel + dv
        self.agent_pos = self.agent_pos + self.agent_vel * dt
        self.t += dt

    def render(self, out, col=(1.0, 0.55, 0.25), name="agents"):
        out[name] = dict(pos=self.agent_pos, col=np.tile(col, (self.n, 1)), size=self.agent_size)


def _sep(P, r):
    """Pairwise separation push for small groups."""
    if len(P) < 2:
        return np.zeros_like(P)
    D = P[:, None, :] - P[None, :, :]
    d = np.linalg.norm(D, axis=2) + np.eye(len(P)) * 1e9
    w = np.clip(1 - d / r, 0, 1)
    return (D / d[..., None] * w[..., None]).sum(1)


# ============================================================================================ CUTE
class CuteHopper(Body):
    """1-3 small round critters: hop (|sin| vertical), sidle up to the pilot, dart back, repeat."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(1, 4)); s = rng.uniform(2, 4.5)
        super().__init__(rng, n, s, rng.uniform(1.0, 1.2), pilot.speed * 1.3 + 40, pilot.pos + _rand_unit(rng) * 80)
        self.f = rng.uniform(1.4, 2.8); self.A = rng.uniform(15, 35)
        self.near, self.far = rng.uniform(18, 40), rng.uniform(80, 130)
        self.mode = np.zeros(n, bool); self.phase = rng.uniform(0, 1, n)
        self.side = _rand_unit(rng, n)

    def step(self, arena, dt):
        p = arena.pilots[0]
        d = p.pos - self.agent_pos; dist = np.linalg.norm(d, axis=1); u = _unit(d)
        self.mode = np.where(dist < self.near, True, np.where(dist > self.far, False, self.mode))
        speed = 35 + 0.6 * p.speed
        want = np.where(self.mode[:, None], -u, u) * speed + p.vel * 0.7 + self.side * 12
        hop = self.A * np.cos(2 * math.pi * (self.f * self.t + self.phase)) * 2 * math.pi * self.f / 4
        want[:, 1] += hop
        want += _sep(self.agent_pos, 20) * 30
        self.drive(want, dt)


class CutePuppy(Body):
    """A small round follower trailing the pilot, tail-wag weave, occasional zoomie loop around it."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(1, 3)); s = rng.uniform(3, 6)
        super().__init__(rng, n, s, rng.uniform(1.0, 1.3), pilot.speed * 1.4 + 50, pilot.pos - pilot.vel * 0.3)
        self.trail = rng.uniform(25, 50); self.f = rng.uniform(1.5, 2.6); self.A = rng.uniform(10, 25)
        self.zoom_every = rng.uniform(5, 9); self.phase = rng.uniform(0, 1, n)

    def step(self, arena, dt):
        p = arena.pilots[0]
        fwd = _unit(p.vel[None])[0] if np.linalg.norm(p.vel) > 1 else np.array([0, 0, 1.0])
        side = _unit(np.cross(fwd, [0, 1, 0])[None])[0]
        zoom = (self.t % self.zoom_every) < 1.6
        k = 2 * math.pi * self.f * self.t + 2 * math.pi * self.phase
        if zoom:
            ang = (self.t % self.zoom_every) / 1.6 * 2 * math.pi
            off = (math.cos(ang) * side + math.sin(ang) * fwd)[None] * 45
        else:
            off = (-fwd * self.trail)[None] + np.sin(k)[:, None] * side * self.A
        tgt = p.pos + off
        want = (tgt - self.agent_pos) * 2.5 + p.vel
        want[:, 1] += np.abs(np.sin(k)) * 20
        want += _sep(self.agent_pos, 15) * 30
        self.drive(want, dt)


class CuteBunch(Body):
    """3-8 tiny round hoppers milling near the pilot, each on its own rhythm, each poking in and out."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(3, 9)); s = rng.uniform(1.5, 3.5)
        super().__init__(rng, n, s, rng.uniform(1.0, 1.25), pilot.speed * 1.3 + 40, pilot.pos + _rand_unit(rng) * 60, 30)
        self.f = rng.uniform(1.6, 3.2, n); self.phase = rng.uniform(0, 1, n)
        self.r = rng.uniform(25, 70, n); self.wf = rng.uniform(0.15, 0.35, n)

    def step(self, arena, dt):
        p = arena.pilots[0]
        ang = 2 * math.pi * (self.wf * self.t + self.phase)
        rr = self.r * (0.7 + 0.5 * np.sin(2 * math.pi * 0.4 * self.t + 7 * self.phase))
        off = np.stack([np.cos(ang) * rr, np.zeros(self.n), np.sin(ang) * rr], 1)
        want = (p.pos + off - self.agent_pos) * 2.0 + p.vel
        want[:, 1] += np.cos(2 * math.pi * (self.f * self.t + self.phase)) * 2 * math.pi * self.f * 6
        want += _sep(self.agent_pos, 12) * 30
        self.drive(want, dt)


# ============================================================================================ PLAYFUL
class PlayPorpoise(Body):
    """2-5 sleek medium swimmers pacing the pilot, porpoising, cutting across its bow and back."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(2, 6)); s = rng.uniform(6, 12)
        super().__init__(rng, n, s, rng.uniform(2.0, 3.2), pilot.speed * 1.5 + 60, pilot.pos + _rand_unit(rng) * 100, 40)
        self.f = rng.uniform(0.5, 1.0); self.A = rng.uniform(25, 45)
        self.lat = rng.uniform(40, 90, n) * rng.choice([-1, 1], n); self.phase = rng.uniform(0, 1, n)
        self.cross_f = rng.uniform(0.08, 0.16)

    def step(self, arena, dt):
        p = arena.pilots[0]
        fwd = _unit(p.vel[None])[0] if np.linalg.norm(p.vel) > 1 else np.array([0, 0, 1.0])
        side = _unit(np.cross(fwd, [0, 1, 0])[None])[0]
        k = 2 * math.pi * (self.cross_f * self.t + self.phase)
        lat = self.lat * np.cos(k)
        ahead = 40 + 40 * np.sin(2 * k)
        off = lat[:, None] * side + ahead[:, None] * fwd
        off[:, 1] += self.A * np.sin(2 * math.pi * (self.f * self.t + self.phase))
        want = (p.pos + off - self.agent_pos) * 2.0 + p.vel
        want += _sep(self.agent_pos, 25) * 40
        self.drive(want, dt)


class PlayTag(Body):
    """1-2 quick critters playing tag: dash in, tap, flee in zigzags, come back for more."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(1, 3)); s = rng.uniform(3, 7)
        super().__init__(rng, n, s, rng.uniform(1.2, 2.0), pilot.speed * 1.5 + 80, pilot.pos + _rand_unit(rng) * 150)
        self.mode = np.zeros(n, bool); self.zz = rng.uniform(1.5, 3.0)
        self.tap, self.far = rng.uniform(12, 25), rng.uniform(120, 200)
        self.flee = _rand_unit(rng, n)

    def step(self, arena, dt):
        p = arena.pilots[0]
        d = p.pos - self.agent_pos; dist = np.linalg.norm(d, axis=1); u = _unit(d)
        newly = (dist < self.tap) & ~self.mode
        if newly.any():
            self.flee[newly] = _unit(-u[newly] + _rand_unit(self.rng, int(newly.sum())) * 0.8)
        self.mode = np.where(dist < self.tap, True, np.where(dist > self.far, False, self.mode))
        zig = _unit(np.cross(self.flee, [0, 1, 0])) * np.sign(np.sin(2 * math.pi * self.zz * self.t))
        sp = p.speed + 70
        want = np.where(self.mode[:, None], (self.flee + 0.7 * zig) * sp, u * sp + p.vel * 0.3)
        self.drive(want, dt)


class PlayLoop(Body):
    """3-6 agents corkscrewing round each other along the pilot's path, energetic, out of phase."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(3, 7)); s = rng.uniform(4, 9)
        super().__init__(rng, n, s, rng.uniform(1.5, 2.5), pilot.speed * 1.6 + 80, pilot.pos + _rand_unit(rng) * 80, 30)
        self.w = rng.uniform(1.8, 3.2); self.R = rng.uniform(15, 35); self.phase = np.arange(n) / n + rng.uniform(0, 1)
        self.off = rng.uniform(50, 110)
        self.drift = rng.uniform(0.1, 0.25)

    def step(self, arena, dt):
        p = arena.pilots[0]
        fwd = _unit(p.vel[None])[0] if np.linalg.norm(p.vel) > 1 else np.array([0, 0, 1.0])
        side = _unit(np.cross(fwd, [0, 1, 0])[None])[0]; up = np.cross(side, fwd)
        a = self.w * self.t + 2 * math.pi * self.phase
        c = p.pos + self.off * (math.cos(self.drift * self.t * 6.28) * side + math.sin(self.drift * self.t * 6.28) * 0.5 * fwd)
        off = (np.cos(a)[:, None] * side + np.sin(a)[:, None] * up) * self.R
        want = (c + off - self.agent_pos) * 3.0 + p.vel
        self.drive(want, dt)


# ============================================================================================ EERIE
class EerieMimic(Body):
    """1-5 figures holding a fixed offset from the pilot, copying its every move, always facing it."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(1, 6)); s = rng.uniform(4, 10)
        super().__init__(rng, n, s, rng.uniform(1.4, 2.5), pilot.speed * 1.5 + 50, pilot.pos)
        self.off = _rand_unit(rng, n) * rng.uniform(60, 140, n)[:, None]
        self.agent_pos = pilot.pos + self.off
        self.lag = rng.uniform(0.0, 0.4)

    def step(self, arena, dt):
        p = arena.pilots[0]
        want = (p.pos - p.vel * self.lag + self.off - self.agent_pos) * 4.0 + p.vel
        self.drive(want, dt)
        self.agent_heading = _unit(p.pos - self.agent_pos)


class EerieSync(Body):
    """6-20 identical figures in a rigid lattice, moving in perfect unison: stop together, go together."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(6, 21)); s = rng.uniform(3, 7)
        g = int(math.ceil(n ** (1 / 2)))
        lat = np.array([[i % g, 0, i // g] for i in range(n)], float) * rng.uniform(14, 26)
        lat -= lat.mean(0)
        super().__init__(rng, n, s, rng.uniform(1.6, 2.6), pilot.speed + 40, pilot.pos)
        self.lat = lat; self.c = pilot.pos + _rand_unit(rng) * rng.uniform(80, 180)
        self.agent_pos = self.c + lat
        self.go, self.stop = rng.uniform(1.0, 2.5), rng.uniform(0.8, 2.0)
        self.dir = _rand_unit(rng); self.v = rng.uniform(25, 50)

    def step(self, arena, dt):
        p = arena.pilots[0]
        cyc = self.t % (self.go + self.stop)
        tow = _unit((p.pos + _unit(p.vel[None])[0] * 60 * (np.linalg.norm(p.vel) > 1) - self.c)[None])[0]
        if cyc < self.go:
            self.dir = _unit((0.85 * self.dir + 0.15 * tow)[None])[0]
            cv = self.dir * self.v + p.vel * 0.8
        else:
            cv = p.vel * 0.8
        self.c = self.c + cv * dt
        want = (self.c + self.lat - self.agent_pos) * 6.0 + cv
        self.drive(want, dt)
        self.agent_heading = np.tile(_unit(p.pos - self.c)[None][0], (self.n, 1))


class EerieWatchers(Body):
    """5-15 still figures ringed at a distance, turning to keep their faces on the pilot, gliding to keep
    exactly the same distance as it moves - never closer, never further."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(5, 16)); s = rng.uniform(3, 8)
        super().__init__(rng, n, s, rng.uniform(1.5, 3.0), pilot.speed + 40, pilot.pos)
        self.off = _rand_unit(rng, n) * rng.uniform(140, 300, n)[:, None]
        self.agent_pos = pilot.pos + self.off

    def step(self, arena, dt):
        p = arena.pilots[0]
        want = (p.pos + self.off - self.agent_pos) * 3.0 + p.vel
        self.drive(want, dt)
        self.agent_heading = _unit(p.pos - self.agent_pos)


# ============================================================================================ MAJESTIC
class MajWhale(Body):
    """One huge, slow, smooth giant on a long arc past the pilot, a slow body undulation."""
    def __init__(self, rng, pilot):
        s = rng.uniform(60, 150)
        side = _rand_unit(rng)
        super().__init__(rng, 1, s, rng.uniform(2.5, 4.0), rng.uniform(15, 40), pilot.pos + side * rng.uniform(250, 450))
        self.f = rng.uniform(0.08, 0.2); self.dir = _unit(np.cross(side, _rand_unit(rng))[None])[0]
        self.turn = rng.uniform(0.04, 0.12) * rng.choice([-1, 1]); self.up = _rand_unit(rng)
        self.agent_vel = self.dir[None] * self.vmax[0]

    def step(self, arena, dt):
        p = arena.pilots[0]
        r = p.pos - self.agent_pos[0]; dd = np.linalg.norm(r)
        a = self.turn * dt
        self.dir = _unit((self.dir + a * np.cross(self.up, self.dir))[None])[0]
        if dd > 600:                                   # come back into the pilot's sky, slowly
            self.dir = _unit((self.dir + 0.01 * _unit(r[None])[0])[None])[0]
        want = self.dir * self.vmax[0] + self.up * math.sin(2 * math.pi * self.f * self.t) * 4 + p.vel * 0.5
        self.drive(want[None], dt)


class MajGlider(Body):
    """2-4 large gliders in a loose formation crossing the pilot's view, a slow wingbeat."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(2, 5)); s = rng.uniform(30, 60)
        c = pilot.pos + _rand_unit(rng) * rng.uniform(200, 350)
        super().__init__(rng, n, s, rng.uniform(1.8, 2.6), rng.uniform(25, 50), c, 60)
        self.form = rng.normal(0, 70, (n, 3)); self.c = c
        self.dir = _rand_unit(rng); self.f = rng.uniform(0.15, 0.3); self.ph = rng.uniform(0, 1, n) * 0.2
        self.turn = rng.uniform(0.03, 0.08)

    def step(self, arena, dt):
        p = arena.pilots[0]
        r = p.pos - self.c
        self.dir = _unit((self.dir + self.turn * dt * np.cross([0, 1, 0], self.dir) + 0.004 * _unit(r[None])[0] * (np.linalg.norm(r) > 450))[None])[0]
        cv = self.dir * self.vmax.mean() + p.vel * 0.5
        self.c = self.c + cv * dt
        bob = np.sin(2 * math.pi * (self.f * self.t + self.ph)) * 6
        want = (self.c + self.form - self.agent_pos) * 0.8 + cv
        want[:, 1] += bob
        self.drive(want, dt)


class MajAssembly(Body):
    """80-160 small members holding the shape of one vast body that drifts slowly and smoothly."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(80, 161)); s = rng.uniform(2.5, 5)
        R = rng.uniform(70, 150)
        u = rng.normal(size=(n, 3)); u = _unit(u) * np.cbrt(rng.random(n))[:, None]
        shape = u * np.array([R * rng.uniform(1.8, 2.6), R * 0.7, R])
        c = pilot.pos + _rand_unit(rng) * rng.uniform(300, 450)
        super().__init__(rng, n, s, rng.uniform(1.5, 2.5), rng.uniform(25, 45), c, 1)
        self.shape = shape; self.c = c; self.agent_pos = c + shape
        self.dir = _rand_unit(rng); self.turn = rng.uniform(0.03, 0.07); self.f = rng.uniform(0.08, 0.15)

    def step(self, arena, dt):
        p = arena.pilots[0]
        r = p.pos - self.c
        self.dir = _unit((self.dir + self.turn * dt * np.cross([0, 1, 0], self.dir) + 0.004 * _unit(r[None])[0] * (np.linalg.norm(r) > 500))[None])[0]
        cv = self.dir * 30 + p.vel * 0.5
        self.c = self.c + cv * dt
        breathe = 1 + 0.06 * math.sin(2 * math.pi * self.f * self.t)
        want = (self.c + self.shape * breathe - self.agent_pos) * 1.5 + cv
        self.drive(want, dt)


# ============================================================================================ MENACING
class MenCircle(Body):
    """3-6 long, medium-large hunters circling the pilot at a distance, the ring slowly tightening."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(3, 7)); s = rng.uniform(8, 20)
        super().__init__(rng, n, s, rng.uniform(2.8, 4.0), pilot.speed + 90, pilot.pos)
        self.R0 = rng.uniform(170, 260); self.R1 = rng.uniform(90, 140); self.T = rng.uniform(20, 35)
        self.w = rng.uniform(0.25, 0.45) * rng.choice([-1, 1]); self.ph = np.arange(n) / n * 2 * math.pi
        self.axis = _unit(np.array([0, 1, 0]) + rng.normal(0, 0.2, 3))
        self.agent_pos = pilot.pos + self._ring(0, pilot) - pilot.pos

    def _ring(self, t, p):
        R = self.R0 + (self.R1 - self.R0) * min(1, t / self.T)
        e1 = _unit(np.cross(self.axis, [1, 0, 0])[None])[0]; e2 = np.cross(self.axis, e1)
        a = self.w * t + self.ph
        return p.pos + R * (np.cos(a)[:, None] * e1 + np.sin(a)[:, None] * e2)

    def step(self, arena, dt):
        p = arena.pilots[0]
        want = (self._ring(self.t + dt, p) - self.agent_pos) * 1.5 + p.vel
        self.drive(want, dt)


class MenStalker(Body):
    """1-2 hunters holding station on the pilot's tail: matching it, never closing all the way, and
    holding dead still whenever it stops - then easing forward again."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(1, 3)); s = rng.uniform(10, 25)
        super().__init__(rng, n, s, rng.uniform(2.5, 4.0), pilot.speed * 1.2 + 30, pilot.pos - pilot.vel * 1.2, 30)
        self.dist = rng.uniform(70, 140, n); self.creep = rng.uniform(0.15, 0.35)
        self.pause = rng.uniform(3, 6); self.hold = rng.uniform(1.0, 2.5)

    def step(self, arena, dt):
        p = arena.pilots[0]
        d = p.pos - self.agent_pos; dist = np.linalg.norm(d, axis=1); u = _unit(d)
        freeze = (self.t % self.pause) < self.hold
        gap = dist - self.dist
        want = u * (gap * self.creep * 3)[:, None] + p.vel
        if freeze:
            want = want * 0.0
        self.drive(want, dt)
        self.agent_heading = u


class MenPatrol(Body):
    """1-3 shark-like hunters cruising a patch near the pilot; every few seconds one makes a straight,
    fast run at it and veers off close - a pass, not a kill. (The game shark's hunt pulse, pointed at you.)"""
    def __init__(self, rng, pilot):
        n = int(rng.integers(1, 4)); s = rng.uniform(15, 35)
        super().__init__(rng, n, s, rng.uniform(3.0, 4.5), pilot.speed * 1.3 + 40, pilot.pos + _rand_unit(rng) * 200, 60)
        self.every = rng.uniform(6, 11); self.run = rng.uniform(1.8, 3.0); self.cruise = rng.uniform(20, 40)
        self.ph = rng.uniform(0, self.every, n); self.dir = _rand_unit(rng, n); self.veer = rng.uniform(30, 60)

    def step(self, arena, dt):
        p = arena.pilots[0]
        d = p.pos - self.agent_pos; dist = np.linalg.norm(d, axis=1); u = _unit(d)
        tt = (self.t + self.ph) % self.every
        run = (tt < self.run) & (dist > self.veer)
        self.dir = _unit(self.dir + 0.4 * dt * np.cross([0, 1, 0], self.dir) + 0.3 * dt * u * (dist > 220)[:, None])
        want = np.where(run[:, None], u * (p.speed * 1.25 + 30), self.dir * self.cruise + p.vel * 0.85)
        self.drive(want, dt)
        self.intent = run.astype(float)


# ============================================================================================ TERRIFYING
class TerSwarm(Body):
    """40-120 small hunters attacking in WAVES: they pull back to a wide shell, then converge on the pilot
    from every side at once, faster than it, pass through, and pull back for the next wave - a looming,
    closing mass. (Round 3: the first version let the separation push pile them into a ball milling on top
    of the pilot, which read - correctly - as a magnified cute bunch. Fixed to match this docstring.)"""
    def __init__(self, rng, pilot):
        n = int(rng.integers(40, 121)); s = rng.uniform(3, 6)
        super().__init__(rng, n, s, rng.uniform(1.6, 2.6), pilot.speed * 1.5 + 60, pilot.pos)
        self.dirs = _rand_unit(rng, n); self.R = rng.uniform(220, 380, n)
        self.agent_pos = pilot.pos + self.dirs * self.R[:, None]
        self.jit = rng.uniform(0.05, 0.2); self.attack, self.regroup = rng.uniform(2.5, 4.0), rng.uniform(2.0, 3.5)

    def step(self, arena, dt):
        p = arena.pilots[0]
        c = self.t % (self.attack + self.regroup)
        if c < self.attack:
            u = _unit(p.pos + p.vel * 0.3 - self.agent_pos)
            want = u * self.vmax[:, None] + _rand_unit(self.rng, self.n) * self.vmax[:, None] * self.jit
            self.intent = np.ones(self.n)
        else:
            want = (p.pos + self.dirs * self.R[:, None] - self.agent_pos) * 1.5 + p.vel
            self.intent = np.zeros(self.n)
        self.drive(want, dt)


class TerEncircle(Body):
    """10-30 hunters forming a shell around the pilot and closing it, then a simultaneous strike, then
    re-forming wide and closing again."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(10, 31)); s = rng.uniform(5, 12)
        super().__init__(rng, n, s, rng.uniform(2.0, 3.0), pilot.speed * 1.6 + 90, pilot.pos)
        self.dirs = _rand_unit(rng, n); self.R0, self.R1 = rng.uniform(220, 320), rng.uniform(30, 60)
        self.close_s, self.cycle = rng.uniform(3, 6), rng.uniform(7, 10)
        self.agent_pos = pilot.pos + self.dirs * self.R0

    def step(self, arena, dt):
        p = arena.pilots[0]
        c = self.t % self.cycle
        if c < self.close_s:
            R = self.R0 + (self.R1 - self.R0) * (c / self.close_s) ** 1.5
        elif c < self.close_s + 0.8:
            R = 5.0
        else:
            R = self.R0
        want = (p.pos + self.dirs * R - self.agent_pos) * 3.0 + p.vel
        self.drive(want, dt)
        self.agent_heading = _unit(p.pos - self.agent_pos)
        self.intent = np.full(self.n, float(c > self.close_s * 0.7 and c < self.close_s + 0.8))


class TerCharge(Body):
    """One giant: still, watching - then a charge straight through where the pilot will be, a wide turn,
    still again, charge again."""
    def __init__(self, rng, pilot):
        s = rng.uniform(35, 90)
        super().__init__(rng, 1, s, rng.uniform(1.6, 3.0), pilot.speed * 1.4 + 60, pilot.pos + _rand_unit(rng) * rng.uniform(200, 350))
        self.wait, self.go = rng.uniform(1.5, 3.5), rng.uniform(2.5, 4.0)
        self.aim = np.zeros(3)

    def step(self, arena, dt):
        p = arena.pilots[0]
        c = self.t % (self.wait + self.go)
        d = p.pos - self.agent_pos[0]
        if c < self.wait:
            want = _unit(d[None])[0] * max(0, np.linalg.norm(d) - 300) * 0.5      # hold dead still
            self.aim = _unit((p.pos + p.vel * 1.0 - self.agent_pos[0])[None])[0]
            self.agent_heading = _unit(d[None])
        else:
            want = self.aim * self.vmax[0]
            self.agent_heading = None
        self.drive(want[None], dt)
        self.intent = np.array([float(c >= self.wait)])


# ============================================================================================ 4th families
class CuteNuzzler(Body):
    """One small round creature that drifts slowly up to the pilot, bumps it gently, bobs there a moment,
    wanders off, and comes back. Slow cute - cute does not have to hop."""
    def __init__(self, rng, pilot):
        s = rng.uniform(2.5, 5)
        super().__init__(rng, 1, s, rng.uniform(1.0, 1.15), pilot.speed * 1.1 + 25, pilot.pos + _rand_unit(rng) * 70)
        self.cyc = rng.uniform(6, 10); self.bob = rng.uniform(0.5, 1.0); self.wander = _rand_unit(rng)

    def step(self, arena, dt):
        p = arena.pilots[0]
        c = self.t % self.cyc
        d = p.pos - self.agent_pos[0]
        if c < self.cyc * 0.6:
            want = _unit(d[None])[0] * min(25, max(0, np.linalg.norm(d) - 8)) + p.vel
        else:
            self.wander = _unit((self.wander + 0.2 * _rand_unit(self.rng))[None])[0]
            want = self.wander * 18 + p.vel * 0.8
        want = want + np.array([0, 6 * math.sin(2 * math.pi * self.bob * self.t), 0])
        self.drive(want[None], dt)


class PlayBurst(Body):
    """A small group hiding still in a knot, then bursting out round the pilot in looping arcs, regrouping,
    hiding again - surprise in a friendly key (does still-then-burst always mean threat?)."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(4, 9)); s = rng.uniform(3, 6)
        super().__init__(rng, n, s, rng.uniform(1.3, 2.0), pilot.speed * 1.5 + 70, pilot.pos + _rand_unit(rng) * 90, 6)
        self.hide, self.play = rng.uniform(1.5, 3.0), rng.uniform(2.5, 4.0)
        self.home = _rand_unit(rng) * rng.uniform(60, 110); self.ph = rng.uniform(0, 1, n); self.w = rng.uniform(1.5, 2.5)

    def step(self, arena, dt):
        p = arena.pilots[0]
        c = self.t % (self.hide + self.play)
        knot = p.pos + self.home
        if c < self.hide:
            want = (knot - self.agent_pos) * 3.0 + p.vel
            want *= (np.linalg.norm(knot - self.agent_pos, axis=1) > 6)[:, None]
            want += p.vel * 0.0
        else:
            a = self.w * (c - self.hide) + 2 * math.pi * self.ph
            R = 40 + 30 * np.sin(a * 0.5)
            off = np.stack([np.cos(a) * R, np.sin(2 * a) * 20, np.sin(a) * R], 1)
            want = (p.pos + off - self.agent_pos) * 3.0 + p.vel
        self.drive(want, dt)


class EerieAngel(Body):
    """A figure that never moves while the pilot comes toward it, and is always a little closer whenever
    the pilot turns away. Smooth, silent, facing."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(1, 4)); s = rng.uniform(4, 9)
        super().__init__(rng, n, s, rng.uniform(1.8, 3.0), pilot.speed + 30, pilot.pos + _rand_unit(rng) * 200, 60)
        self.creep = rng.uniform(15, 35); self.stop = rng.uniform(40, 70)

    def step(self, arena, dt):
        p = arena.pilots[0]
        d = p.pos - self.agent_pos; dist = np.linalg.norm(d, axis=1); u = _unit(d)
        fwd = _unit(p.vel[None])[0] if np.linalg.norm(p.vel) > 1 else np.array([0, 0, 1.0])
        watched = np.sum(-u * fwd, axis=1) > 0.2            # the pilot flies toward it -> it holds still
        move = ~watched & (dist > self.stop)
        want = np.where(move[:, None], u * self.creep + p.vel, p.vel * 0.0)
        want = np.where(watched[:, None], p.vel * 0.0, want)
        self.drive(want, dt)
        self.agent_heading = u


class MajSchool(Body):
    """A large school of medium fish wheeling in slow, smooth, coherent arcs at a distance - every member
    its own small wobble (not a rigid lattice)."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(60, 121)); s = rng.uniform(5, 10)
        c = pilot.pos + _rand_unit(rng) * rng.uniform(250, 400)
        super().__init__(rng, n, s, rng.uniform(2.0, 3.0), 60, c, 40)
        self.c = c; self.dir = _rand_unit(rng); self.turn = rng.uniform(0.15, 0.3) * rng.choice([-1, 1])
        self.slot = rng.normal(0, 45, (n, 3)) * np.array([1.6, 0.6, 1.0]); self.ph = rng.uniform(0, 6.28, n)

    def step(self, arena, dt):
        p = arena.pilots[0]
        r = p.pos - self.c
        self.dir = _unit((self.dir + self.turn * dt * np.cross([0, 1, 0], self.dir) + 0.01 * _unit(r[None])[0] * (np.linalg.norm(r) > 450))[None])[0]
        cv = self.dir * 35 + p.vel * 0.5; self.c = self.c + cv * dt
        wob = np.stack([np.sin(0.7 * self.t + self.ph), np.cos(0.5 * self.t + self.ph), np.sin(0.6 * self.t + 2 * self.ph)], 1) * 8
        want = (self.c + self.slot + wob - self.agent_pos) * 0.8 + cv
        self.drive(want, dt)


class MenShadow(Body):
    """A big dark body pacing alongside the pilot at a fixed distance, matching every turn, watching -
    never closing, never leaving."""
    def __init__(self, rng, pilot):
        s = rng.uniform(25, 60)
        super().__init__(rng, 1, s, rng.uniform(2.5, 4.0), pilot.speed * 1.3 + 40, pilot.pos + _rand_unit(rng) * 200)
        self.dist = rng.uniform(140, 240); self.side = rng.choice([-1, 1]); self.sway = rng.uniform(0.05, 0.12)

    def step(self, arena, dt):
        p = arena.pilots[0]
        fwd = _unit(p.vel[None])[0] if np.linalg.norm(p.vel) > 1 else np.array([0, 0, 1.0])
        side = _unit(np.cross(fwd, [0, 1, 0])[None])[0] * self.side
        tgt = p.pos + side * self.dist * (1 + 0.15 * math.sin(2 * math.pi * self.sway * self.t))
        want = (tgt - self.agent_pos[0]) * 1.2 + p.vel
        self.drive(want[None], dt)
        self.agent_heading = _unit((p.pos - self.agent_pos[0])[None])


class TerAmbush(Body):
    """A pack lying motionless in a wide scatter round the pilot - then every member launches at it at
    once, at full speed, from every side; scatters through, goes still again, and waits."""
    def __init__(self, rng, pilot):
        n = int(rng.integers(8, 25)); s = rng.uniform(6, 14)
        super().__init__(rng, n, s, rng.uniform(2.0, 3.5), pilot.speed * 1.6 + 90, pilot.pos)
        self.dirs = _rand_unit(rng, n); self.R = rng.uniform(150, 260)
        self.agent_pos = pilot.pos + self.dirs * self.R
        self.wait, self.go = rng.uniform(2.0, 4.0), rng.uniform(2.0, 3.0)

    def step(self, arena, dt):
        p = arena.pilots[0]
        c = self.t % (self.wait + self.go)
        if c < self.wait:
            want = (p.pos + self.dirs * self.R - self.agent_pos) * 0.4
            want *= (np.linalg.norm(want, axis=1) > 6)[:, None]
            self.intent = np.zeros(self.n)
        else:
            want = _unit(p.pos - self.agent_pos) * self.vmax[:, None]
            self.intent = np.ones(self.n)
        self.drive(want, dt)


FAMILIES = {
    "cute": [CuteHopper, CutePuppy, CuteBunch, CuteNuzzler],
    "playful": [PlayPorpoise, PlayTag, PlayLoop, PlayBurst],
    "eerie": [EerieMimic, EerieSync, EerieWatchers, EerieAngel],
    "majestic": [MajWhale, MajGlider, MajAssembly, MajSchool],
    "menacing": [MenCircle, MenStalker, MenPatrol, MenShadow],
    "terrifying": [TerSwarm, TerEncircle, TerCharge, TerAmbush],
}
