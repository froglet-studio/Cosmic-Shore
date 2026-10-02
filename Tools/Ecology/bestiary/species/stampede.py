"""STAMPEDE GRAZERS - MAJESTIC. A big, slow, peaceful herd whose PANIC is the danger: alarm spreads body to
body, the whole herd bolts in one direction and tramples (eats) every prism in its path - your trail too.
Bulls turn to face what spooked them and charge.

Local rules:
  * graze:   wander + cohesion to herdmates within 90 u + chew environment mass (one prism per 6 s)
  * alarm a: sees a pilot inside 220 u -> a = 1; otherwise relaxes toward 0.9 x the loudest herdmate within
             70 u at 3/s (a visible wave rolling through the herd), decays 0.12/s when nothing feeds it
  * flee f:  away from the pilot if it saw one, else the alarm-weighted mean flee of herdmates (one direction)
  * stampede: speed and alignment scale with a (to 125 u/s); a stampeding body eats every prism it touches
             and tramples a pilot it runs into (`bite`)
  * bulls (1 in 5): alarmed with a pilot inside 140 u, lower their heads (intent ramps over 1.0 s), then
             charge it at 170 u/s for 1.5 s and rest 4 s
Telegraph: the alarm wave (heads up, the herd turning) and the bull's head-down. Counterplay: approach from
outside the 220 u alarm radius, never sit in front of a bolting herd, bait a bull's charge and sidestep it.
Payoff: a calf separated from the herd (nobody within 45 u) is easy prey - so the counterplay is to SPLIT it.
"""
import numpy as np

from core import Herd, unit, steer, contain, pairwise, separation

SENSE, BULL_R, HEAD_DOWN, SCENT = 250.0, 260.0, 0.9, 900.0
EMOTION = "majestic (awe, then danger in its panic)"
COUNTER = "approach from outside the alarm radius; never sit in a bolting herd's path; sidestep a bull; split off a calf"


class Stampede(Herd):
    name = "stampede"

    def __init__(self, arena, n=72, herds=6):
        super().__init__(arena, n, size=10.0, body=40.0)
        env = np.flatnonzero(arena.mass_owner < 0)
        c = arena.mass_pos[arena.rng.choice(env, herds, replace=False)]
        self.pos = c[np.arange(n) % herds] + arena.rng.normal(0, 50.0, (n, 3))
        self.bull = (np.arange(n) % 4) == 0
        self.size = np.where(self.bull, 14.0, 10.0)
        self.a = np.zeros(n)
        self.f = unit(arena.rng.normal(size=(n, 3)))
        self.chew = arena.rng.uniform(0, 6, n)
        self.prep = np.zeros(n); self.charge = np.zeros(n); self.rest = np.zeros(n)
        self.cd = np.zeros(n)
        self.trail_eaten = 0.0; self.env_trampled = 0.0; self.charges = 0; self.stampede_s = 0.0

    def act(self, arena, dt):
        al = self.alive
        off, dist, k = self.pilot_vectors(arena)
        D, d = pairwise(self.pos)
        d = np.where(al[None, :], d, np.inf)
        # alarm: sight, then contagion
        saw = al & (dist < SENSE)
        nb = d < 70
        loud = np.where(nb, self.a[None, :], 0.0).max(1)
        target = np.maximum(0.9 * loud, 0)
        self.a = np.where(saw, 1.0, np.where(target > self.a, self.a + (target - self.a) * min(1, 3 * dt),
                                             np.maximum(0, self.a - 0.12 * dt)))
        w = np.where(nb, self.a[None, :], 0.0)
        inherited = unit((w[:, :, None] * self.f[None, :, :]).sum(1) + 1e-9)
        herd = np.where(d < 90, 1.0, 0.0)
        cen = (herd[:, :, None] * D).sum(1) / np.maximum(herd.sum(1, keepdims=True), 1)
        # a seer flees away from the threat AS SEEN FROM ITS HERD'S CENTRE (where the herd is, minus where the
        # threat is) - so a whole herd bolts ONE way, and the animals on the far side of you run THROUGH you
        herd_away = unit(cen - off)
        self.f = np.where(saw[:, None], unit(0.35 * unit(-off) + herd_away),
                          unit(self.f + (inherited - self.f) * min(1, 2 * dt)))
        a = self.a[:, None]
        alignv = (herd[:, :, None] * self.vel[None, :, :]).sum(1) / np.maximum(herd.sum(1, keepdims=True), 1)
        # grazers love fresh trail (it is food): a herd drifts toward the nearest trail prism within 400 u,
        # so herds come to graze YOUR lanes - and are there, skittish, when you fly back through
        lure = np.zeros_like(self.pos)
        own = np.flatnonzero(arena.mass_alive & (arena.mass_owner >= 0))
        if len(own):
            # trail scent: walk UP the trail - toward the newest trail prism within scent range
            T = arena.mass_pos[own[-400:]]
            dd = np.linalg.norm(T[None, :, :] - self.pos[:, None, :], axis=2)
            ok = dd < SCENT
            newest = np.where(ok.any(1), T.shape[0] - 1 - np.argmax(ok[:, ::-1], axis=1), -1)
            has = newest >= 0
            lure[has] = unit(T[newest[has]] - self.pos[has])
        graze = unit(self.rng.normal(0, 1, self.pos.shape) * 0.6 + cen * 0.02 + 0.3 * unit(self.drift()) + 1.2 * lure) * 30.0
        bolt = unit(self.f * 1.0 + unit(alignv) * 0.8) * 125.0
        desired = graze * (1 - a) + bolt * a + separation(D, d, 2.2 * self.size.mean(), al) * 50.0
        # bulls: head down, then charge
        self.rest = np.maximum(0, self.rest - dt)
        cand = al & self.bull & (dist < BULL_R) & (self.charge <= 0) & (self.rest <= 0)
        self.prep = np.where(cand, self.prep + dt, np.where(self.charge > 0, self.prep, 0.0))
        go = cand & (self.prep >= HEAD_DOWN)
        was = self.charge > 0
        self.charge = np.where(go, 1.5, np.maximum(0, self.charge - dt))
        self.charges += int(go.sum())
        self.prep[go] = 0.0
        self.rest = np.where(was & (self.charge <= 0), 4.0, self.rest)
        face = cand
        desired[face] = unit(off[face]) * 20.0                     # turns to face you, slowing
        ch = self.charge > 0
        PV = np.array([p.vel for p in arena.pilots])
        lead = off + PV[k] * np.clip(dist / 170.0, 0, 1.2)[:, None]       # a bull leads its target
        desired[ch] = unit(lead[ch]) * 170.0
        acc = np.where(ch, 400.0, 60.0 + 200.0 * self.a)
        self.vel = steer(self.vel, desired, acc, dt)
        self.vel = contain(self.pos, self.vel, arena.R)
        sp = np.linalg.norm(self.vel, axis=1)
        tow = np.sum(unit(self.vel) * unit(off), axis=1)
        self.intent = np.where(self.bull, np.clip(self.prep / HEAD_DOWN, 0, 1) + (ch * 1.0), 0.0)
        self.intent = np.maximum(self.intent, self.a * np.clip(2.0 * tow, 0, 1) * (sp > 60))   # a bolting herd heading at you
        # the herd PARTS around a bull lowering its head: herdmates within 70 u step aside (perpendicular to the
        # bull's line) and carry its posture - the lane opening is the herd-scale tell
        hot = al & self.bull & ((self.prep > 0) | ch)
        if hot.any():
            near = (d[:, hot] < 70)
            if near.any():
                b = np.flatnonzero(hot)[np.argmax(near, axis=1)]
                m = near.any(1) & ~self.bull
                line = unit(off[b])
                side = self.pos - self.pos[b]
                side = unit(side - np.sum(side * line, axis=1, keepdims=True) * line)
                self.vel[m] += side[m] * 60.0 * dt * 10
                self.intent[m] = np.maximum(self.intent[m], self.intent[b[m]])
        self.intent = np.clip(self.intent, 0, 1)
        self.stampede_s += dt * float((self.a[al] > 0.5).mean()) if al.any() else 0
        # trample mass in the path, graze otherwise
        self.chew = np.maximum(0, self.chew - dt)
        self.cd = np.maximum(0, self.cd - dt)
        for i in np.flatnonzero(al & ((sp > 60) | (self.chew <= 0))):
            c = arena.mass_near(self.pos[i], self.size[i] + 3)
            if sp[i] <= 60:
                c = c[:1]
                if len(c): self.chew[i] = 6.0
            for j in c:
                v = arena.consume(int(j), "stampede"); self.gut[i] += v
                if arena.mass_owner[j] >= 0: self.trail_eaten += v
                elif sp[i] > 60: self.env_trampled += v
        # trample / gore a pilot
        # a trample is a body running INTO you (not you rear-ending a fleeing animal - that is your ram)
        closing = np.sum(self.vel * unit(off), axis=1) > 0.3 * np.maximum(sp, 1e-6)
        for i in np.flatnonzero(al & (dist < self.size + 6 + 2) & (self.cd <= 0) & (((sp > 60) & closing) | ch)):
            arena.hit(arena.pilots[k[i]], "bite", 0.2 if self.bull[i] else 0.1); self.cd[i] = 2.0
        # a calf alone is prey
        alone = np.min(np.where(al[None, :], d, np.inf), axis=1) > 45
        self.hunter_contacts(arena, can_kill=alone)
        self.publish(arena)

    def drift(self):
        # the herd's slow migration: a shared, slowly turning heading (grazers follow the grass)
        th = 0.05 * self.t
        return np.tile([np.cos(th), 0.3 * np.sin(0.7 * th), np.sin(th)], (len(self.pos), 1))

    def colours(self):
        c = np.where(self.bull[:, None], [0.85, 0.7, 0.5], [0.7, 0.75, 0.9])
        c = c * (1 - 0.5 * self.a[:, None]) + np.array([1.0, 0.3, 0.15]) * 0.5 * self.a[:, None]
        c[self.charge > 0] = [1.0, 0.15, 0.1]
        return c

    def phase_stats(self):
        return dict(charges=self.charges, trail_eaten=round(self.trail_eaten, 1),
                    env_trampled=round(self.env_trampled, 1), stampede_s=round(self.stampede_s, 1))


def make(arena):
    return Stampede(arena)
