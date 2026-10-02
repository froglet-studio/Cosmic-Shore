"""MOBBING FLOCK - PLAYFUL NUISANCE. A twittering flock of tiny birds roosting on a plant. A ship that
dawdles near them (slow, or loitering by the roost) gets MOBBED: they swirl round it and take turns diving
in for a peck, calling the rest of the flock in. Fly fast and they cannot keep up and lose interest.

Local rules:
  * mob drive m: rises while a pilot within 300 u is SLOW (< 100 u/s) or inside 260 u of the roost; spreads
                 by alarm calls (relaxes toward 0.9 x the loudest flockmate within 120 u); decays when the
                 pilot is fast or gone
  * mobbing:     orbit the pilot at ~30 u (radial spring + tangential swirl + separation), capped at 110 u/s
  * dive:        each mobbing bird, in turn (a staggered 2.5 s rhythm), pulls up for 0.8 s (intent ramps) and
                 dives at 160 u/s; a peck inside 6 u of the hull drains a sliver (`drain`)
  * agile:       a pilot pointing straight at a bird inside 60 u makes it jink aside
Telegraph: the swirl tightening and a bird pulling up above you before it dives.
Counterplay: keep your speed up (>110 they cannot follow), do not loiter at the roost, jink as one pulls up.
Payoff: low - they are agile - but a pilot that drives through the swirl at speed swats one now and then.
"""
import numpy as np

from core import Herd, unit, steer, contain, pairwise, separation

EMOTION = "playful nuisance (cheeky, swarming, harmless-ish)"
COUNTER = "keep speed up; don't loiter at the roost; jink when one pulls up"
MAXV, DIVE_V, PULL = 110.0, 160.0, 0.8


class Mobber(Herd):
    name = "mobber"

    def __init__(self, arena, n=50, roosts=5):
        super().__init__(arena, n, size=1.5, body=1.5)
        env = np.flatnonzero(arena.mass_owner < 0)
        self.roost = arena.mass_pos[arena.rng.choice(env, roosts, replace=False)]
        self.home = np.arange(n) % roosts
        self.pos = self.roost[self.home] + arena.rng.normal(0, 25, (n, 3))
        self.m = np.zeros(n)
        self.clock = arena.rng.uniform(0, 2.5, n)       # staggered dive rhythm
        self.pull = np.zeros(n); self.dive = np.zeros(n)
        self.pecks = 0; self.mob_s = 0.0
        self.prevpos = {}

    def act(self, arena, dt):
        al = self.alive
        off, dist, k = self.pilot_vectors(arena)
        PP = np.array([p.pos for p in arena.pilots]); PV = np.array([p.vel for p in arena.pilots])
        pspeed = np.linalg.norm(PV, axis=1)
        D, d = pairwise(self.pos)
        d = np.where(al[None, :], d, np.inf)
        near_roost = np.linalg.norm(PP[k] - self.roost[self.home], axis=1) < 260
        provoke = (dist < 300) & ((pspeed[k] < 100) | near_roost)
        loud = np.where(d < 120, self.m[None, :], 0).max(1)
        tgt = np.where(provoke, 1.0, 0.9 * loud * (dist < 400))
        self.m = np.clip(np.where(tgt > self.m, self.m + (tgt - self.m) * min(1, 2 * dt), self.m - 0.25 * dt), 0, 1)
        mob = al & (self.m > 0.4)
        self.mob_s += dt * float(mob.mean())
        # orbit
        r = np.maximum(dist, 1e-6)
        radial = unit(off) * np.clip(r - 30.0, -40, 80)[:, None] * 2.0
        tang = unit(np.cross(off, [0.0, 1.0, 0.0]) + np.cross(off, [1.0, 0.0, 0.0]) * 0.3) * 60.0
        orbit = PV[k] + radial + tang
        home = unit(self.roost[self.home] - self.pos + self.rng.normal(0, 15, self.pos.shape)) * 30.0
        des = np.where(mob[:, None], orbit, home) + separation(D, d, 6.0, al) * 40
        # dive rhythm
        self.clock = np.where(mob, self.clock - dt, self.clock)
        startpull = mob & (self.clock <= 0) & (self.pull <= 0) & (self.dive <= 0) & (dist < 80)
        was = self.pull > 0
        self.pull = np.where(startpull, PULL, np.maximum(0, self.pull - dt))
        godive = was & (self.pull <= 0) & mob
        self.dive = np.where(godive, 0.6, np.maximum(0, self.dive - dt))
        self.clock = np.where(startpull, 2.5 + PULL, self.clock)
        up = np.cross(PV[k], np.cross(off, PV[k]))
        des[self.pull > 0] = (PV[k] + unit(-off + np.array([0.0, 1.0, 0.0])) * 50.0)[self.pull > 0]
        des[self.dive > 0] = (PV[k] + unit(off) * DIVE_V)[self.dive > 0]
        # agile: a pilot pointing straight at me inside 60 u
        pointing = (np.sum(unit(PV[k]) * unit(-off), axis=1) > 0.9) & (dist < 60)
        side = unit(np.cross(PV[k], [0.0, 1.0, 0.0]) + 1e-6)
        des[pointing] += side[pointing] * 140.0
        vmax = np.where(self.dive > 0, DIVE_V + pspeed[k], MAXV)
        self.vel = steer(self.vel, des, 500.0, dt)
        sp = np.linalg.norm(self.vel, axis=1)
        self.vel *= np.minimum(1.0, vmax / np.maximum(sp, 1e-6))[:, None]
        self.vel = contain(self.pos, self.vel, arena.R)
        swirl = mob & (dist < 60)                         # circling you at strike range IS the posture
        self.intent = np.where(self.dive > 0, 1.0, np.where(self.pull > 0, 0.6 + 0.4 * (1 - self.pull / PULL), np.where(swirl, 0.55, 0.3 * mob)))
        for i in np.flatnonzero(al & (self.dive > 0) & (dist < arena.pilots[0].radius + 6)):
            arena.hit(arena.pilots[k[i]], "drain", 0.02); self.pecks += 1
            self.dive[i] = 0.0; self.vel[i] = PV[k[i]] - unit(off[i]) * 60
        self.hunter_contacts(arena, extra=1.0, can_kill=~pointing)
        self.publish(arena, threat_mask=mob)

    def colours(self):
        c = np.tile([0.6, 0.85, 1.0], (len(self.pos), 1))
        c = c * (1 - 0.5 * self.m[:, None]) + np.array([1.0, 0.6, 0.9]) * 0.5 * self.m[:, None]
        c[self.pull > 0] = [1.0, 1.0, 0.6]
        c[self.dive > 0] = [1.0, 0.5, 0.2]
        return c

    def phase_stats(self):
        return dict(pecks=self.pecks, mob_s=round(self.mob_s, 1))


def make(arena):
    return Mobber(arena)
