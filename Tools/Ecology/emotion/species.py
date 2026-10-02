"""Today's game species, modelled from their SHIPPED numbers so the probe can read them.

Every constant is quoted from an asset or a doc; where a number had to be assumed it says ASSUMED.
  * Worm colony   Assets/_SO_Assets/Lifeforms/WormColonyConfig.asset + Docs/ECOSYSTEM.md §23
  * Shark         Assets/_SO_Assets/Light Fauna Data/MassSharkFaunaDataSO.asset + LightFaunaDataSO defaults
                  + ECOSYSTEM.md §7.3 (v3.3 hunt pulses). It hunts PREY FAUNA, never pilots.
  * Tadpole flock Assets/_Prefabs/FloraAndFauna/TadPoleFauna.prefab (Boid numbers)
  * Swarm whale / dragonfly  Docs/SWARM_FAUNA.md on cece/swarm-fauna-game (vessel reaction, mobbing)
"""
from __future__ import annotations

import math

import numpy as np

from archetypes import Body, _unit, _rand_unit, _sep


def _turn_toward(cur, want, max_rad):
    cur = _unit(cur[None])[0]; want = _unit(want[None])[0]
    ang = math.acos(float(np.clip(cur @ want, -1, 1)))
    if ang < 1e-6:
        return want
    k = min(1.0, max_rad / ang)
    return _unit((cur + (want - cur) * k)[None])[0]


class WormColony(Body):
    """8 segments following the head's path. Cruise 18 u/s at 40 deg/s with a 12 deg / 2.2 Hz yaw
    undulation; hunt windows of 12 s every 26 s, rest first. In a window a pilot inside AggroRadius 220 is
    pursued at 18 x 1.45 = 26 u/s turning 80 deg/s; inside StrikeRange 90: telegraph 1.2 s (near-stopped,
    coil x2.5) -> lunge at 70 u/s at the point locked at telegraph end (<= 2.5 s, arrive 10) -> recover
    2.5 s at 35% speed. Segment spacing 8.4 x KaijuScale 3 = 25 u; segment radius ASSUMED 9 u."""
    def __init__(self, rng, pilot, start_dist=(150, 260), always_hunt=False):
        n = 8; super().__init__(rng, n, 9.0, 1.3, 200.0, pilot.pos)
        self.head = pilot.pos + _rand_unit(rng) * rng.uniform(*start_dist)
        self.dir = _rand_unit(rng); self.spacing = 25.0
        self.path = [self.head.copy() - self.dir * i for i in range(400)]
        self.agent_pos = np.array([self.head - self.dir * self.spacing * i for i in range(n)])
        self.state, self.st_t, self.lock = "cruise", 0.0, None
        self.goal = self.head + _rand_unit(rng) * 300; self.always_hunt = always_hunt

    def step(self, arena, dt):
        p = arena.pilots[0]; t = self.t
        hunting = self.always_hunt or (t % 26.0) >= (26.0 - 12.0)     # rest first, then the 12 s window
        d = p.pos - self.head; dist = np.linalg.norm(d)
        self.st_t += dt
        if self.state in ("cruise", "pursue"):
            if hunting and dist < 220:
                self.state = "pursue" if dist > 90 else "telegraph"; self.st_t = 0.0 if self.state == "telegraph" else self.st_t
            elif self.state == "pursue":
                self.state = "cruise"
        if self.state == "telegraph" and self.st_t >= 1.2:
            self.state, self.st_t, self.lock = "lunge", 0.0, p.pos.copy()
        elif self.state == "lunge" and (self.st_t >= 2.5 or np.linalg.norm(self.lock - self.head) < 10):
            self.state, self.st_t = "recover", 0.0
        elif self.state == "recover" and self.st_t >= 2.5:
            self.state = "cruise"
        und = math.radians(12) * math.sin(2 * math.pi * 2.2 * t)
        if self.state == "cruise":
            if np.linalg.norm(self.goal - self.head) < 40:
                self.goal = p.pos + _rand_unit(self.rng) * 250
            self.dir = _turn_toward(self.dir, self.goal - self.head, math.radians(40) * dt); speed = 18.0
        elif self.state == "pursue":
            self.dir = _turn_toward(self.dir, d, math.radians(80) * dt); speed = 26.1
        elif self.state == "telegraph":
            self.dir = _turn_toward(self.dir, d, math.radians(80) * dt); speed = 2.0; und *= 2.5
        elif self.state == "lunge":
            self.dir = _turn_toward(self.dir, self.lock - self.head, math.radians(80) * dt); speed = 70.0
        else:
            speed = 18.0 * 0.35
        side = _unit(np.cross(self.dir, [0, 1, 0])[None])[0]
        v = (self.dir * math.cos(und) + side * math.sin(und)) * speed
        self.head = self.head + v * dt
        self.path.insert(0, self.head.copy()); self.path = self.path[:600]
        # follow-the-leader: segment i sits spacing*i of arc length back along the head's path
        pts, acc, out, i = self.path, 0.0, [self.head.copy()], 1
        for j in range(1, len(pts)):
            seg = np.linalg.norm(pts[j] - pts[j - 1]); acc += seg
            while i < self.n and acc >= self.spacing * i:
                out.append(pts[j].copy()); i += 1
            if i >= self.n:
                break
        while len(out) < self.n:
            out.append(out[-1] - self.dir * self.spacing)
        newP = np.array(out)
        self.agent_vel = (newP - self.agent_pos) / dt; self.agent_pos = newP; self.t += dt
        self.agent_heading = np.tile(self.dir, (self.n, 1))
        self.intent = np.full(self.n, 1.0 if self.state in ("telegraph", "lunge") else 0.0)


class Shark(Body):
    """1 territorial predator. Cruise 25-35 u/s (min/maxSpeed), rotationLerp 5; hunt windows 10 s of every
    20 s (rest first) at pursuit x1.5 on PREY FAUNA (modelled as 6 drifting herbivore points it chases),
    never on the pilot. Its den is ASSUMED within 200 u of the pilot so the encounter happens at all.
    Body radius ASSUMED 30 (the shark model reads ~134 u across in the heart-size table, i.e. radius
    ~67, so `big=True` runs it at 60 too)."""
    def __init__(self, rng, pilot, big=False):
        super().__init__(rng, 1, 60.0 if big else 30.0, 4.0, 55.0, pilot.pos + _rand_unit(rng) * 180)
        self.den = pilot.pos + _rand_unit(rng) * 150; self.dir = _rand_unit(rng)
        self.prey = self.den + rng.normal(0, 120, (6, 3)); self.prey_v = _rand_unit(rng, 6) * 12
        self.speed = rng.uniform(25, 35)

    def step(self, arena, dt):
        p = arena.pilots[0]
        self.den = self.den + (p.pos - self.den) * min(1, 0.05 * dt)       # territory stays near the scene
        self.prey += self.prey_v * dt
        far = np.linalg.norm(self.prey - self.den, axis=1) > 250
        self.prey_v[far] = _unit(self.den - self.prey[far]) * 12
        hunting = (self.t % 20.0) >= 10.0
        me = self.agent_pos[0]
        if hunting:
            j = int(np.argmin(np.linalg.norm(self.prey - me, axis=1)))
            want = _unit((self.prey[j] - me)[None])[0] * self.speed * 1.5
            if np.linalg.norm(self.prey[j] - me) < 15:       # devour; a new herbivore wanders in
                self.prey[j] = self.den + self.rng.normal(0, 150, 3)
        else:
            self.dir = _unit((self.dir + 0.4 * dt * np.cross([0, 1, 0], self.dir) + 0.3 * dt * _unit((self.den - me)[None])[0])[None])[0]
            want = self.dir * self.speed
        self.drive(want[None], dt)
        self.intent = np.array([1.0 if hunting else 0.0])


class TadpoleFlock(Body):
    """30 tadpoles (Boid): speed 10-15, separation radius 15 (w 12), cohesion 50 (w 1), alignment w 2,
    goal w 3 orbiting a food goal at radius 60 (goalOrbitRadius). Body radius ASSUMED 2.5, aspect 2.5.
    They graze; the Boid has no vessel reaction."""
    def __init__(self, rng, pilot, n=30):
        super().__init__(rng, n, 2.5, 2.5, 15.0, pilot.pos + _rand_unit(rng) * 120, 25)
        self.goal = self.agent_pos.mean(0); self.ph = rng.uniform(0, 6.28)
        self.agent_vel = _rand_unit(rng, n) * 12

    def step(self, arena, dt):
        P, V = self.agent_pos, self.agent_vel
        D = P[:, None] - P[None]; d = np.linalg.norm(D, axis=2) + np.eye(self.n) * 1e9
        sep = (D / d[..., None] * (d < 15)[..., None]).sum(1)
        coh_m = (d < 50); cnt = np.maximum(coh_m.sum(1), 1)
        coh = (coh_m[..., None] * P[None]).sum(1) / cnt[:, None] - P
        ali = (coh_m[..., None] * V[None]).sum(1) / cnt[:, None]
        a = self.ph + 0.3 * self.t
        orbit = self.goal + 60 * np.array([math.cos(a), 0.2 * math.sin(2 * a), math.sin(a)])
        want = 12 * _unit(sep) + 1 * _unit(coh) + 2 * _unit(ali) + 3 * _unit(orbit - P)
        want = _unit(want) * np.clip(np.linalg.norm(V, axis=1), 10, 15)[:, None]
        self.drive(want, dt)


class SwarmBody(Body):
    """A swarm creature: `n` tadpoles holding a body plan (ellipsoid), the anchor cruising at 7 u/s
    (Cruise 0.35 voxel/step). Vessel reaction: members within SENSE of the pilot's look-ahead startle and
    flee (flee weight Charge .6 Mass .5 Space 1.4 Time 2.0 - the body's majority decides); if the pilot is
    loitering (speed < MOB_SPEED) and the body is a dragonfly, its Time members MOB it. SENSE / MOB_SPEED /
    body extent ASSUMED (60 u, 30 u/s, from the doc's descriptions)."""
    def __init__(self, rng, pilot, plan="whale"):
        n, ext, flee, mob = dict(whale=(192, (80, 28, 32), 0.6, 0.0), dragonfly=(76, (60, 12, 30), 1.7, 0.7))[plan]
        u = _unit(rng.normal(size=(n, 3))) * np.cbrt(rng.random(n))[:, None]
        self.shape = u * np.array(ext)
        c = pilot.pos + _rand_unit(rng) * rng.uniform(120, 200)
        super().__init__(rng, n, 2.5, 2.5, 40.0, c, 1)
        self.c = c; self.agent_pos = c + self.shape; self.dir = _rand_unit(rng)
        self.flee, self.mob = flee, mob; self.is_time = rng.random(n) < (0.7 if plan == "dragonfly" else 0.1)
        self.startle = np.zeros(n)

    def step(self, arena, dt):
        p = arena.pilots[0]
        r = p.pos - self.c
        self.dir = _turn_toward(self.dir, r if np.linalg.norm(r) > 220 else np.cross([0, 1, 0], self.dir) + self.dir, 0.25 * dt)
        cv = self.dir * 7.0; self.c = self.c + cv * dt
        home = self.c + self.shape
        look = p.pos + p.vel * 0.5
        dl = self.agent_pos - look; dist = np.linalg.norm(dl, axis=1)
        hit = dist < 60
        self.startle = np.maximum(self.startle * math.exp(-dt / 1.5), hit.astype(float))
        want = (home - self.agent_pos) * 1.2 * (1 - 0.7 * self.startle)[:, None] + cv
        want += _unit(dl) * (self.flee * 30 * self.startle)[:, None]
        if self.mob > 0 and np.linalg.norm(p.vel) < 30:
            m = self.is_time
            want[m] = want[m] * (1 - self.mob) + _unit(p.pos - self.agent_pos[m]) * 30 * self.mob
        want += _sep(self.agent_pos, 4) * 10
        self.drive(want, dt)


GAME = {
    "worm_colony": lambda rng, p: WormColony(rng, p),
    # the attack cycle isolated: a hunt window that never closes, starting inside aggro range
    "worm_attack_cycle": lambda rng, p: WormColony(rng, p, start_dist=(120, 200), always_hunt=True),
    "shark_r30": lambda rng, p: Shark(rng, p, big=False),
    "shark_r60": lambda rng, p: Shark(rng, p, big=True),
    "tadpole_flock": lambda rng, p: TadpoleFlock(rng, p),
    "swarm_whale": lambda rng, p: SwarmBody(rng, p, "whale"),
    "swarm_dragonfly": lambda rng, p: SwarmBody(rng, p, "dragonfly"),
}
