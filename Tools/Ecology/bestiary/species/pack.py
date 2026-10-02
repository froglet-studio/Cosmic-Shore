"""PACK HUNTERS that encircle - DREAD. A pack slower than you catches you by GEOMETRY, never by speed.

Local rules only (each hunter, every step):
  * predict:   the nearest pilot inside 900 u, extrapolated tau = dist / sprint seconds ahead (capped)
  * spread:    take a bearing around that prediction; push it AWAY from packmates' bearings (angular separation
               on the sphere around the pilot) and TOWARD the pilot's heading (cut-off) - so the pack fans out
               into a cone in front of you, nobody told to "flank"
  * quorum:    each hunter reads the pack's CLOSURE around its pilot (how evenly the bearings of packmates within
               350 u cover the sphere, times how many there are). Past 0.55 the ring radius collapses and they
               sprint - the whole pack strikes at once, from every side.
  * stamina:   a sprint drains 3 s of stamina; a winded hunter drops back to cruise and widens out.
The telegraph (intent) is the closure itself: you SEE the ring forming before it closes.
Counterplay: fly at the gap (the side the ring has not covered) before closure, or break line of travel
(the cut-off is aimed at where you WERE going). Payoff: a winded hunter is easy prey for a charging pilot.
"""
import numpy as np

from core import Herd, unit, steer, contain, pairwise, separation

EMOTION = "dread (menacing, coordinated)"
COUNTER = "break through the gap before the ring closes; change heading so the cut-off is wrong"
CRUISE, SPRINT, RANGE = 95.0, 175.0, 900.0


class Pack(Herd):
    name = "pack"

    def __init__(self, arena, n=9):
        super().__init__(arena, n, spread=50.0, size=5.0, body=12.0)
        self.stamina = np.full(n, 3.0)
        self.cool = np.zeros(n)
        self.closure = np.zeros(n)
        self.bear = unit(arena.rng.normal(size=(n, 3)))
        self.strikes = 0

    def act(self, arena, dt):
        a = self.alive
        if not a.any():
            return
        off, dist, k = self.pilot_vectors(arena)
        n = len(self.pos)
        PP = np.array([p.pos for p in arena.pilots]); PV = np.array([p.vel for p in arena.pilots])
        tau = np.clip(dist / SPRINT, 0, 2.0)
        pred = PP[k] + PV[k] * tau[:, None]
        head = unit(PV[k])
        b = unit(self.pos - pred)                          # my bearing around the predicted pilot
        D, d = pairwise(self.pos)
        same = (k[:, None] == k[None, :]) & a[None, :] & a[:, None] & (dist[None, :] < 350) & (dist[:, None] < RANGE)
        # angular separation of bearings (only packmates hunting the same pilot)
        cosb = b @ b.T
        push = np.where(same, np.clip(cosb - 0.2, 0, None), 0.0)
        ang = -(push[:, :, None] * (b[None, :, :] - cosb[:, :, None] * b[:, None, :])).sum(1)
        # closure: resultant of my packmates' bearings around their pilot - small resultant + many = surrounded
        cnt = same.sum(1) + 1
        res = np.linalg.norm((same[:, :, None] * b[None, :, :]).sum(1) + b, axis=1) / cnt
        closure = np.clip((1.0 - res) * np.clip((cnt - 1) / 4.0, 0, 1) * 1.6, 0, 1)
        closure = np.where(dist < 350, closure, 0.0)
        self.closure = closure
        want_b = unit(b + 1.2 * ang + 0.9 * head)
        striking = (closure > 0.55) & (self.stamina > 0.3) & (self.cool <= 0)
        ring = np.where(striking, 0.0, np.clip(dist * 0.5, 120, 220))
        goal = pred + want_b * ring[:, None]
        speed = np.where(striking, SPRINT, CRUISE)
        desired = unit(goal - self.pos) * speed[:, None]
        # roam when no pilot in range: drift with the pack
        roam = dist > RANGE
        centre = self.pos[a].mean(0)
        desired[roam] = unit(centre - self.pos[roam] + self.rng.normal(0, 40, (roam.sum(), 3))) * 40
        desired += separation(D, d, 30.0, a) * 60
        # winded / cooling hunters fall back and widen
        back = self.cool > 0
        desired[back] = unit(self.pos[back] - PP[k[back]]) * CRUISE
        self.vel = steer(self.vel, desired, 260.0, dt)
        self.vel = contain(self.pos, self.vel, arena.R)
        self.stamina = np.where(striking, self.stamina - dt, np.minimum(3.0, self.stamina + 0.5 * dt))
        self.cool = np.maximum(0, self.cool - dt)
        self.intent = np.where(back, 0.0, closure)
        # bite
        for i in np.flatnonzero(a & (dist < arena.pilots[0].radius + self.size + 4) & (self.cool <= 0)):
            p = arena.pilots[k[i]]
            arena.hit(p, "bite"); self.strikes += 1
            self.cool[same[i] | (np.arange(n) == i)] = 3.0       # the pack backs off together after a bite
        self.hunter_contacts(arena, can_kill=None)
        self.publish(arena)

    def colours(self):
        c = np.tile([0.55, 0.25, 0.2], (len(self.pos), 1))
        c[:, 0] += 0.45 * self.closure
        c[self.cool > 0] = [0.35, 0.3, 0.3]
        return c

    def phase_stats(self):
        return dict(strikes=self.strikes)


def make(arena):
    return Pack(arena)
