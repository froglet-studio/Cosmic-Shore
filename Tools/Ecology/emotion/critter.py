"""One parametric creature family: a single agent model whose PARAMETERS alone move it across emotions.

theta is a vector in [0, 1]^D (what the search moves); `decode` maps it to physical parameters. Every
behaviour is a weighted sum of steering terms - there is no mode switch per emotion, no script.

Game laws, enforced by construction (the search cannot buy an emotion by breaking them):
  * speed <= 160 u/s absolute (1.6x a 100 u/s cruise) - no impossible speed;
  * acceleration <= 600 * sqrt(3 / size) u/s^2 - a big body cannot snap (archetypes.accel_cap);
  * conserved body budget: n * size^3 <= MASS_BUDGET (one whale OR a swarm, not both);
  * positions only by integrating velocity - nothing teleports.
"""
from __future__ import annotations

import math

import numpy as np

from archetypes import Body, _unit, _rand_unit

VMAX_ABS = 160.0
MASS_BUDGET = 120.0 ** 3          # one radius-120 body, or 1000 radius-12 bodies

PARAMS = [  # name, lo, hi, scale
    ("n", 1, 120, "log"),
    ("size", 1.5, 150, "log"),
    ("aspect", 1.0, 4.0, "lin"),
    ("vmax", 10, VMAX_ABS, "log"),
    ("approach", -1.0, 1.0, "lin"),       # radial drive toward the preferred distance (signed)
    ("d0", 15, 450, "log"),               # preferred distance to the pilot
    ("orbit", 0.0, 1.0, "lin"),           # tangential drive around the pilot
    ("ar_amp", 0.0, 1.0, "lin"),          # approach-retreat oscillation of d0
    ("ar_period", 1.0, 10.0, "log"),
    ("bounce_amp", 0.0, 40.0, "lin"),
    ("bounce_hz", 0.1, 3.5, "log"),
    ("sync", 0.0, 1.0, "lin"),            # shared phases and shared noise across members
    ("noise", 0.0, 1.0, "lin"),
    ("still_duty", 0.0, 0.8, "lin"),      # fraction of each cycle held still
    ("still_period", 1.0, 10.0, "log"),
    ("rigid", 0.0, 1.0, "lin"),           # members hold fixed slots in a formation
    ("spread", 5.0, 200.0, "log"),        # group radius
    ("face", 0.0, 1.0, "lin"),            # >0.5: always faces the pilot
    ("pace", 0.0, 1.3, "lin"),            # carry the pilot's velocity: 0 ignores it, ~1 keeps pace/copies it
    ("sneak", 0.0, 1.0, "lin"),           # freeze while inside the pilot's forward view
]
D = len(PARAMS)


def decode(theta):
    th = np.clip(np.asarray(theta, float), 0, 1); out = {}
    for x, (k, lo, hi, sc) in zip(th, PARAMS):
        out[k] = math.exp(math.log(lo) + x * (math.log(hi) - math.log(lo))) if sc == "log" else lo + x * (hi - lo)
    out["n"] = int(round(out["n"]))
    out["size"] = min(out["size"], (MASS_BUDGET / out["n"]) ** (1 / 3))
    return out


class Critter(Body):
    def __init__(self, rng, pilot, theta):
        q = self.q = decode(theta)
        n = q["n"]
        super().__init__(rng, n, q["size"], q["aspect"], q["vmax"], pilot.pos, 1)
        self.slot = _rand_unit(rng, n) * (q["spread"] * np.cbrt(rng.random(n)))[:, None]
        self.dir0 = _rand_unit(rng)
        self.agent_pos = pilot.pos + self.dir0 * q["d0"] * 1.5 + self.slot
        self.ph = rng.random(n) * (1 - q["sync"])
        self.axis = _rand_unit(rng)
        self.noise_dir = _rand_unit(rng, n)

    def step(self, arena, dt):
        q, p, rng = self.q, arena.pilots[0], self.rng
        P = self.agent_pos
        c = P.mean(0)
        anchor = np.where(q["rigid"] > 0.5, 1, 0)
        ref = c[None] if anchor else P                # rigid: the formation's centre decides; else each member
        d = p.pos - ref; dist = np.linalg.norm(d, axis=1); u = _unit(d)
        # approach / retreat around a preferred distance that may oscillate
        d0 = q["d0"] * (1 + q["ar_amp"] * 0.8 * np.sin(2 * math.pi * (self.t / q["ar_period"] + self.ph)))
        radial = np.tanh((dist - d0) / max(q["d0"] * 0.3, 10)) * q["approach"]
        if q["approach"] < 0:                          # negative = avoids the pilot outright
            radial = -np.abs(q["approach"]) * np.ones_like(dist)
        tang = _unit(np.cross(self.axis, u)) * q["orbit"]
        want = (u * radial[:, None] + tang) * q["vmax"]
        if len(want) != self.n:
            want = np.repeat(want, self.n, axis=0)
        # noise (shared when synced)
        nd = self.noise_dir
        jitter = _rand_unit(rng, self.n)
        shared = _rand_unit(rng)
        mix = q["sync"] * shared[None] + (1 - q["sync"]) * jitter
        self.noise_dir = _unit(nd + 0.6 * mix)
        want += self.noise_dir * q["noise"] * q["vmax"]
        # bounce on a vertical rhythm
        want[:, 1] += q["bounce_amp"] * 2 * math.pi * q["bounce_hz"] * np.cos(2 * math.pi * (q["bounce_hz"] * self.t + self.ph)) * 0.25
        # formation: rigid slots around the centre
        if q["rigid"] > 0:
            want = want + (c + self.slot - P) * 2.0 * q["rigid"] + (np.mean(want, 0) - want) * q["rigid"]
        # cohesion so a group stays a group at its spread
        dc = np.linalg.norm(P - c, axis=1)
        want += _unit(c - P) * np.clip(dc - q["spread"], 0, None)[:, None] * 0.5
        # carry / copy the pilot's motion
        want += p.vel * q["pace"]
        # still phases
        if q["still_duty"] > 0.02:
            cyc = ((self.t / q["still_period"]) + self.ph) % 1.0
            still = cyc < q["still_duty"]
            want[still] = 0.0
        # sneak: freeze while in the pilot's forward view
        if q["sneak"] > 0.05:
            pv = np.linalg.norm(p.vel)
            if pv > 1:
                inview = np.sum(-_unit(p.pos - P) * (p.vel / pv), axis=1) > math.cos(math.radians(45))
                want[inview] *= (1 - q["sneak"])
        self.drive(want, dt)
        self.agent_heading = _unit(p.pos - P) if q["face"] > 0.5 else None


def factory(theta):
    return lambda rng, pilot: Critter(rng, pilot, theta)
