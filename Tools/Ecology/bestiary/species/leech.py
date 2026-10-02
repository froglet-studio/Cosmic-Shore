"""HITCHHIKER LEECHES - CUTE (with a sting). Tiny, sleepy, round; they drift in loose puddles until a ship
passes close, then POUNCE, cling to the hull and sip an element.

Local rules:
  * drift:   a slow random walk that keeps a loose puddle (cohesion to a few neighbours, separation)
  * pounce:  a pilot inside 110 u, a stored-up hop (1.4 s of dart stamina) - aim at where the hull WILL be
             (intercept), intent = 1 while airborne. That hop is the telegraph: a flick of little bodies
             leaping off the puddle toward you.
  * cling:   inside hull + 3 u it latches at the spot it hit, riding the hull in the ship's own frame, and
             every 1.5 s attached it drains an element (`drain`). Clinging leeches stack.
  * grip:    grip falls while the host's turn rate exceeds 1.0 rad/s and recovers while it flies straight.
             At zero grip the leech is FLUNG off sideways and is dazed for 2.5 s (no pounce).
Counterplay: a hard turn or drift shakes them off; skirt the puddle beyond 110 u; a pilot RAMMING a free leech
at speed (> 100 u/s relative) squashes it (payoff: its crystal).
"""
import numpy as np

from core import Herd, unit, steer, contain, pairwise, separation

EMOTION = "cute (curious, clingy) with a sting"
MAX_PER_HULL = 6
SENSE = 140.0
COUNTER = "hard turn / drift shakes them off; give the puddle a wide berth; ram free ones"


class Leech(Herd):
    name = "leech"

    def __init__(self, arena, n=64, puddles=16):
        super().__init__(arena, n, spread=70.0, size=1.6, body=2.0)
        # leeches live in small puddles ON the mass clumps (where skimming pilots come to farm)
        env = np.flatnonzero(arena.mass_owner < 0)
        c = arena.mass_pos[arena.rng.choice(env, puddles, replace=False)]
        self.pos = c[np.arange(n) % puddles] + arena.rng.normal(0, 25.0, (n, 3))
        self.host = np.full(n, -1)
        self.off = np.zeros((n, 3))
        self.grip = np.ones(n)
        self.daze = np.zeros(n)
        self.hop = np.full(n, 1.4)
        self.sip = np.zeros(n)
        self.prev_v = {}
        self.attached_time = 0.0
        self.shaken = 0

    @staticmethod
    def frame(v):
        f = unit(v)
        s = unit(np.cross(f, [0.0, 1.0, 0.0]) + 1e-6)
        u = np.cross(s, f)
        return np.stack([f, s, u])

    def act(self, arena, dt):
        a = self.alive
        off, dist, k = self.pilot_vectors(arena)
        free = a & (self.host < 0)
        D, d = pairwise(self.pos)
        # turn rate of every pilot (host stress)
        omega = []
        for j, p in enumerate(arena.pilots):
            pv = self.prev_v.get(j, p.vel)
            c = np.clip(unit(pv) @ unit(p.vel), -1, 1)
            omega.append(np.arccos(c) / dt)
            self.prev_v[j] = p.vel.copy()
        omega = np.array(omega)
        # free behaviour
        coh = np.where(d < 60, 1.0, 0.0) * free[None, :]
        cen = (coh[:, :, None] * D).sum(1) / np.maximum(coh.sum(1, keepdims=True), 1)
        wander = self.rng.normal(0, 1, (len(self.pos), 3))
        desired = unit(cen * 0.05 + wander) * 12.0 + separation(D, d, 8.0, free) * 15
        # trail scent: a free leech near a fresh trail prism creeps toward it - they gather along your lanes
        own = np.flatnonzero(arena.mass_alive & (arena.mass_owner >= 0))
        if len(own):
            T = arena.mass_pos[own[-200:]]
            dd = np.linalg.norm(T[None, :, :] - self.pos[:, None, :], axis=2)
            j = np.argmin(dd, axis=1); near = free & (dd[np.arange(len(j)), j] < 220)
            desired[near] += unit(T[j[near]] - self.pos[near]) * 25.0
        PP = np.array([p.pos for p in arena.pilots]); PV = np.array([p.vel for p in arena.pilots])
        tau = np.clip(dist / 150.0, 0, 0.8)
        aim = PP[k] + PV[k] * tau[:, None]
        pounce = free & (dist < SENSE) & (self.hop > 0.05) & (self.daze <= 0)
        if getattr(self, 'ablate', None) == 'nopounce':
            pounce[:] = False
        desired[pounce] = unit(aim[pounce] - self.pos[pounce]) * 150.0
        self.vel[free] = steer(self.vel[free], desired[free], np.where(pounce[free], 900.0, 60.0), dt)
        self.vel = contain(self.pos, self.vel, arena.R)
        self.hop = np.where(pounce, self.hop - dt, np.minimum(1.4, self.hop + 0.4 * dt))
        self.daze = np.maximum(0, self.daze - dt)
        self.intent = np.where(pounce, 1.0, np.where(self.host >= 0, 0.8, 0.0))   # a leech ON your hull is a visible warning
        # latch (only from a pounce, and not on a pilot ramming it - that is a squash)
        load = np.bincount(self.host[self.host >= 0], minlength=len(arena.pilots))
        for i in np.flatnonzero(free & (dist < arena.pilots[0].radius + self.size + 3)):
            p = arena.pilots[k[i]]
            if load[k[i]] >= MAX_PER_HULL:
                continue                                   # no room on the hull: it bounces off
            load[k[i]] += 1
            rel = np.linalg.norm(p.vel - self.vel[i])
            if p.policy == "hunter" and rel > 100 and not pounce[i]:
                self.kill(i, arena); continue
            if pounce[i] or rel < 100:
                F = self.frame(p.vel)
                self.host[i] = k[i]; self.off[i] = F @ (unit(self.pos[i] - p.pos) * (p.radius + 1.5))
                self.grip[i] = 1.0; self.sip[i] = 0.0
        # ride, sip, lose grip
        att = a & (self.host >= 0)
        for i in np.flatnonzero(att):
            j = self.host[i]; p = arena.pilots[j]
            F = self.frame(p.vel)
            self.pos[i] = p.pos + F.T @ self.off[i] - p.vel * dt     # minus: the base loop adds vel*dt
            self.vel[i] = p.vel
            self.grip[i] += (-(omega[j] - 1.0) * 1.6 if omega[j] > 1.0 else 0.3) * dt
            self.grip[i] = 1.0 if getattr(self, 'ablate', None) == 'nogrip' else min(self.grip[i], 1.0)
            self.sip[i] += dt
            self.attached_time += dt
            if self.sip[i] >= 1.5:
                self.sip[i] = 0.0
                arena.hit(p, "drain", 0.1)
                self.gut[i] += 0.0                       # a drain is an elemental TRANSFER, not mass
            if self.grip[i] <= 0:
                F = self.frame(p.vel)
                self.host[i] = -1; self.daze[i] = 2.5; self.shaken += 1
                self.vel[i] = p.vel * 0.5 + F[1] * self.rng.choice([-1, 1]) * 90.0
        self.publish(arena, threat_mask=self.host < 0, target_mask=self.host < 0)

    def colours(self):
        c = np.tile([0.95, 0.75, 0.85], (len(self.pos), 1))
        c[self.intent > 0.5] = [1.0, 0.95, 0.5]
        c[self.host >= 0] = [1.0, 0.45, 0.7]
        c[self.daze > 0] = [0.6, 0.55, 0.7]
        return c

    def phase_stats(self):
        return dict(shaken=self.shaken, attached_s=round(self.attached_time, 1))


ABLATIONS = {"nogrip": "a latched leech never loses its grip (turning does nothing)",
             "nopounce": "no hop: a leech only latches if the hull brushes it (no intercept dart)"}


def make(arena, ablate=None):
    sp = Leech(arena); sp.ablate = ablate
    return sp
