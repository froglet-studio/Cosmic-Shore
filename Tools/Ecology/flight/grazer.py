"""GRAZERS - the CUTE base of the food web (Direction H's one new species; bestiary shape, Python first so the JS
port in flight/ has a reference to be gated against, like every other species).

A drifting school of small grazers on the cell's scattered flora mass. They are the "nothing is attacking me"
baseline a pilot needs before a threat can read as a threat, and the mass sink that keeps an untended cell from
silting up (the food web, CLAUDE.md "Mass is conserved"). Numbers follow the substrate's `grazer` regime
(substrate/species.py: speed 34, comfort ring 70, sense 200, eat_r 7), re-expressed on the bestiary's Herd.

Local rules (weighted sum, no branch picks a behaviour):
  * graze:    each grazer re-searches the nearest ENVIRONMENT prism inside 120 u on its turn (1 in 4 per step) and
              chews it on contact (one prism per CHEW s). Grazers never eat trail: they are not your problem.
  * school:   align + cohere with schoolmates inside 30 u, separate inside 9 u
  * curious:  a pilot inside SENSE that is NOT rushing them pulls them to a COMFORT ring around it (a spring),
              so a slow pilot collects an escort; a pilot closing fast inside 60 u scatters them (fear, decays)
  * breed:    a gut >= FULL buds a newborn whose body is paid from the gut (cap CAP: production gating)
Threat: none (intent 0, no hits). Payoff: each grazer a crystal, but they scatter from a charging pilot.
"""
import numpy as np

from core import Herd, unit, steer, contain, pairwise, separation

EMOTION = "cute (curious, harmless)"
COUNTER = "none needed; they are the calm the threats are measured against"
CAP = 240
SPEED, FLEE_V, SENSE, COMFORT, CHEW, FULL, BREED = 34.0, 90.0, 200.0, 70.0, 4.0, 30.0, 10.0


class Grazer(Herd):
    name = "grazer"

    def __init__(self, arena, n=120, cap=CAP):
        super().__init__(arena, cap, spread=60.0, size=2.5, body=4.0)
        env = np.flatnonzero(arena.mass_owner < 0)
        c = arena.mass_pos[arena.rng.choice(env, 4, replace=False)]
        self.pos = c[np.arange(cap) % 4] + arena.rng.normal(0, 40.0, (cap, 3))
        self.alive[n:] = False; self.body[n:] = 0.0
        self.gut[:n] = 10.0
        self.fear = np.zeros(cap)
        self.chew = arena.rng.uniform(0, CHEW, cap)
        self.breed = arena.rng.uniform(2, BREED, cap)
        self.food = np.full(cap, -1)
        self.tick = 0
        self.births = 0
        self.prev_d = np.full(cap, np.inf)

    def act(self, arena, dt):
        idx = np.flatnonzero(self.alive)
        if len(idx) == 0:
            return
        P = self.pos[idx]; V = self.vel[idx]
        D, d = pairwise(P)
        m = d < 30
        cnt = np.maximum(m.sum(1, keepdims=True), 1)
        align = (m[:, :, None] * V[None, :, :]).sum(1) / cnt
        coh = (m[:, :, None] * D).sum(1) / cnt
        sep = separation(D, d, 9.0)
        self.tick += 1
        turn = (idx + self.tick) % 4 == 0
        for ii in np.flatnonzero(turn | (self.food[idx] < 0)):
            c = arena.mass_near(P[ii], 120.0)
            c = c[arena.mass_owner[c] < 0] if len(c) else c
            self.food[idx[ii]] = c[np.argmin(np.linalg.norm(arena.mass_pos[c] - P[ii], axis=1))] if len(c) else -1
        fj = self.food[idx]
        has = fj >= 0
        has[has] &= arena.mass_alive[fj[has]]
        food = np.zeros_like(P)
        food[has] = unit(arena.mass_pos[fj[has]] - P[has])
        close = has.copy()
        close[has] &= np.linalg.norm(arena.mass_pos[fj[has]] - P[has], axis=1) < 7
        for ii in np.flatnonzero(close & (self.chew[idx] <= 0)):
            self.gut[idx[ii]] += arena.consume(int(fj[ii]), "grazer"); self.chew[idx[ii]] = CHEW
        off, dist, k = self.pilot_vectors(arena)
        closing = dist < self.prev_d - 1e-3
        self.prev_d = dist.copy()
        off, dist, k, closing = off[idx], dist[idx], k[idx], closing[idx]
        PV = np.array([p.vel for p in arena.pilots]) if arena.pilots else np.zeros((1, 3))
        rush = (dist < 60) & closing & (np.linalg.norm(PV[k], axis=1) > 90)
        fear = self.fear[idx]
        fear = np.where(rush, 1.0, np.maximum(0, fear - 0.6 * dt))
        curious = (dist < SENSE) & (fear < 0.3)
        ring = unit(off) * np.clip(dist - COMFORT, -40, 60)[:, None] / 60.0
        desired = (unit(align + 1e-9) * 0.5 + coh * 0.02 + food * 1.0 + ring * 1.2 * curious[:, None]
                   + self.rng.normal(0, 0.35, P.shape))
        desired = unit(desired) * SPEED + sep * 40.0
        flee = fear > 0.3
        desired[flee] = (-unit(off[flee]) * FLEE_V + sep[flee] * 40.0)
        V = steer(V, desired, 60.0 + 200.0 * fear, dt)
        self.vel[idx] = V
        self.vel = contain(self.pos, self.vel, arena.R)
        self.fear[idx] = fear
        self.intent[idx] = 0.0
        self.chew = np.maximum(0, self.chew - dt)
        self.breed = np.maximum(0, self.breed - dt)
        free = np.flatnonzero(~self.alive)
        for i in idx[(self.gut[idx] >= FULL) & (self.breed[idx] <= 0)]:
            if len(free) == 0:
                break
            j, free = free[0], free[1:]
            self.alive[j] = True; self.body[j] = 4.0; self.gut[i] -= 4.0; self.gut[j] = 0.0
            self.pos[j] = self.pos[i] + self.rng.normal(0, 3, 3); self.vel[j] = self.vel[i]
            self.fear[j] = 0.0; self.breed[i] = BREED; self.breed[j] = BREED; self.births += 1
        self.hunter_contacts(arena)
        self.publish(arena, threat_mask=np.zeros(len(self.pos), bool))

    def colours(self):
        return np.array([0.45, 0.95, 1.0]) * (1 - 0.4 * self.fear[:, None]) + np.array([1.0, 1.0, 1.0]) * 0.4 * self.fear[:, None]

    def phase_stats(self):
        return dict(births=self.births)


ABLATIONS = {"nofear": "never scatters (fear pinned 0)"}


def make(arena, ablate=None):
    sp = Grazer(arena); sp.ablate = ablate
    return sp
