"""AMBUSH LURKER - EERIE. It sits perfectly still beside a plant and looks like a crystal. When a ship comes
close and keeps coming, it GAPES (swells and darkens for 0.9 s) and then SNAPS - a 380 u/s lunge at where
the hull will be.

Local rules:
  * settle:  crawl (20 u/s) to an environment prism no other lurker sits within 60 u of, then hold still -
             ambush where the food is, which is where farming pilots come
  * gape:    a pilot inside 170 u that is CLOSING (or anything inside 80 u) -> the gape ramps to 1 over 0.9 s
             (intent = gape). If the pilot leaves 230 u the mouth closes again.
  * snap:    gape complete -> lunge at the pilot's predicted position for 0.35 s (133 u reach); a hull inside
             the jaws is bitten (`bite`)
  * creep:   within 600 u of a pilot but OUTSIDE its forward 50-degree cone, it slides (35 u/s) toward where
             that pilot will be in 3 s; looked at, it freezes dead still (it moves only when you are not looking)
  * spent:   3 s slack and slow after a snap (it cannot gape), then it creeps off to a new prism
Counterplay: read the gape and swerve - the snap is aimed at where you WERE going; or hit it first (it is
helpless before the gape completes and while spent). Payoff: a crystal-mimic is a real crystal once killed.
"""
import numpy as np

from core import Herd, unit, steer, contain

EMOTION = "eerie (stillness, mimicry, the sudden snap)"
COUNTER = "read the gape and swerve off your line; or strike first / while it is spent"
GAPE, REACH_T, LUNGE, SENSE = 0.9, 0.35, 380.0, 170.0


class Lurker(Herd):
    name = "lurker"

    def __init__(self, arena, n=16):
        super().__init__(arena, n, size=4.0, body=20.0)
        env = np.flatnonzero(arena.mass_owner < 0)
        self.pos = arena.mass_pos[arena.rng.choice(env, n, replace=False)] + arena.rng.normal(0, 8, (n, 3))
        self.gape = np.zeros(n); self.lunge = np.zeros(n); self.spent = np.zeros(n)
        self.seat = np.full(n, -1)
        self.snaps = 0; self.hits = 0; self.aborts = 0
        self.prev_d = np.full(n, np.inf)
        self.heading = unit(arena.rng.normal(size=(n, 3)))      # it never turns to face you
        self.crept = 0.0

    def act(self, arena, dt):
        al = self.alive
        off, dist, k = self.pilot_vectors(arena)
        closing = dist < self.prev_d - 1e-3
        self.prev_d = dist.copy()
        PV = np.array([p.vel for p in arena.pilots])
        idle = al & (self.lunge <= 0) & (self.spent <= 0)
        trig = idle & (((dist < SENSE) & closing) | (dist < 80))
        before = self.gape.copy()
        gt = 0.05 if getattr(self, 'ablate', None) == 'nogape' else GAPE
        self.gape = np.where(trig | (idle & (self.gape > 0) & (dist < 230)), self.gape + dt / gt, np.maximum(0, self.gape - 2 * dt))
        self.aborts += int(((before > 0.3) & (self.gape < before) & idle).sum())
        go = idle & (self.gape >= 1.0)
        aim = off + PV[k] * np.clip(dist / LUNGE, 0, 0.5)[:, None]
        self.vel[go] = unit(aim[go]) * LUNGE
        self.lunge = np.where(go, REACH_T, np.maximum(0, self.lunge - dt))
        self.snaps += int(go.sum())
        self.gape[go] = 0.0
        ending = (self.lunge <= 0) & (self.lunge + dt > 0) & ~go & (np.linalg.norm(self.vel, axis=1) > 200)
        self.spent = np.where(ending, 3.0, np.maximum(0, self.spent - dt))
        # movement: still when seated, crawl to a free prism when unseated / spent over
        settle = al & (self.lunge <= 0) & (self.gape <= 0)
        des = np.zeros_like(self.vel)
        for i in np.flatnonzero(settle):
            s = self.seat[i]
            if s < 0 or not arena.mass_alive[s]:
                c = arena.mass_near(self.pos[i], 400.0)
                c = c[arena.mass_owner[c] < 0]
                if len(c):
                    others = self.pos[al & (np.arange(len(self.pos)) != i)]
                    dd = np.linalg.norm(arena.mass_pos[c][:, None, :] - others[None, :, :], axis=2).min(1) if len(others) else np.full(len(c), 1e9)
                    c = c[dd > 60] if (dd > 60).any() else c
                    self.seat[i] = c[np.argmin(np.linalg.norm(arena.mass_pos[c] - self.pos[i], axis=1))]
                s = self.seat[i]
            if s >= 0:
                to = arena.mass_pos[s] - self.pos[i]
                dd = np.linalg.norm(to)
                des[i] = unit(to) * min(20.0, dd * 2.0) if dd > 6 else 0.0
        # CREEP WHILE UNWATCHED: a dormant lurker within 600 u of a pilot, OUTSIDE that pilot's forward 50 deg
        # cone, slides toward the point the pilot will pass in 3 s; inside the cone it freezes dead still
        PP = np.array([p.pos for p in arena.pilots])
        ahead = PP[k] + PV[k] * 3.0
        look = np.sum(unit(PV[k]) * unit(-off), axis=1) > np.cos(np.radians(50))
        dorm = settle & (dist < 600) & (dist > 120) & (getattr(self, 'ablate', None) != 'nocreep')
        creep = dorm & ~look
        des[creep] = unit(ahead[creep] - self.pos[creep]) * 35.0
        des[dorm & look] = 0.0
        self.vel[dorm & look] = 0.0
        self.crept += float(np.linalg.norm(des[creep], axis=1).sum()) * dt
        self.seat[creep] = -1
        acc = np.where(self.lunge > 0, 0.0, 120.0)
        self.vel = np.where((self.lunge > 0)[:, None], self.vel, steer(self.vel, des, acc, dt))
        self.vel = contain(self.pos, self.vel, arena.R)
        # the gape FLARES on its first frame (colour flips, body jumps 40%), then swells - so the posture is
        # readable from the moment it starts, not only once it is half-open
        gp = np.clip(self.gape, 0, 1)
        self.size = np.where(gp > 0, 5.6 + 3.4 * gp, 4.0) + 3.0 * (self.lunge > 0)
        self.intent = np.where(self.lunge > 0, 1.0, np.where(gp > 0.02, 0.6 + 0.4 * gp, 0.0))
        for i in np.flatnonzero(al & (self.lunge > 0) & (dist < arena.pilots[0].radius + self.size + 4)):
            arena.hit(arena.pilots[k[i]], "bite", 0.3); self.hits += 1
            self.lunge[i] = 0.0; self.spent[i] = 3.0; self.vel[i] *= 0.1
        self.hunter_contacts(arena)
        # a crystal-seeker sees EVERY lurker as a crystal; a wary pilot can only see the ones that show themselves
        self.publish(arena, threat_mask=(self.gape > 0.05) | (self.lunge > 0) | (self.spent > 0))

    def colours(self):
        c = np.tile([0.75, 1.0, 0.45], (len(self.pos), 1))           # crystal-lime when still (mimicry)
        g = np.clip(0.5 + 0.5 * self.gape, 0, 1)[:, None] * (self.gape > 0.02)[:, None]
        c = c * (1 - g) + np.array([0.5, 0.05, 0.1]) * g
        c[self.lunge > 0] = [1.0, 0.1, 0.1]
        c[self.spent > 0] = [0.3, 0.35, 0.3]
        return c

    def phase_stats(self):
        return dict(crept=round(self.crept, 1), snaps=self.snaps, snap_hit_rate=round(self.hits / max(self.snaps, 1), 2), aborts=self.aborts)


ABLATIONS = {"nogape": "snaps the instant it triggers (no 0.9 s gape)",
             "nocreep": "never creeps while unwatched (stays on its prism)"}


def make(arena, ablate=None):
    sp = Lurker(arena); sp.ablate = ablate
    return sp
