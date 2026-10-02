"""LEVIATHAN - MAJESTIC / DREADFUL. A loose shoal of glimmering units that, once enough of them gather,
ASSEMBLES into one 320-u body that swims - slow, curious, and lethal to touch. Kill enough of it and it
falls apart back into a shoal, which regathers.

The split the swarm research found (Tools/NCA/DISCOVERIES.md, end of day): COMPOSITION is designed, SHAPE is
local. Here:
  * composition (one cheap controller per body): a heading that turns at 0.35 rad/s toward food and,
    curiously, toward the nearest pilot inside 700 u; a speed of 35 u/s; a travelling wave down the body
    whose amplitude grows toward the tail and SURGES (a tail sweep) when a pilot lingers near the tail flank
  * shape (every unit, locally): a spring to its own slot in the body frame (slots = a tapered ellipsoid,
    assigned once by a greedy nearest-slot match), capped at 120 u/s - which is why it ASSEMBLES visibly
    instead of teleporting into shape
  * quorum: the shoal assembles when >= 60 units are within 260 u of their centroid; the body dissolves back
    into a shoal when it drops under 45 (units killed) - and regathers by cohesion
  * danger: an assembled unit is a danger prism - touching one BURNS (`burn`, game: a hostile danger prism
    burns petals). The mouth (front) eats every prism it passes, trail included.
Telegraph (intent): units near you light up with proximity; tail units with the sweep surge.
Counterplay: keep your distance from a slow giant; never linger at its tail flank; pick off LOOSE units
(a shoal is harmless and every unit drops a crystal) to keep it from re-forming.
"""
import numpy as np

from core import Herd, unit, steer, contain, pairwise, separation

EMOTION = "majestic awe -> dread up close"
COUNTER = "keep clear of a slow giant; never linger at the tail; pick off loose units so it cannot re-form"
N, LEN, RAD = 140, 320.0, 55.0
ASSEMBLE_N, DISSOLVE_N = 60, 45


def body_slots(n, rng):
    """n slot positions on a tapered ellipsoid, body frame (x forward)."""
    i = np.arange(n) + 0.5
    x = 1.0 - 2.0 * i / n                          # front .. back
    phi = i * np.pi * (3 - np.sqrt(5))
    r = RAD * np.sqrt(np.clip(1 - x ** 2, 0, 1)) * np.where(x < -0.4, 0.55 + 0.45 * (x + 1) / 0.6, 1.0)
    return np.stack([x * LEN / 2, r * np.cos(phi), r * np.sin(phi)], 1)


class Leviathan(Herd):
    name = "leviathan"

    def __init__(self, arena):
        super().__init__(arena, N, spread=220.0, size=5.0, body=8.0)
        self.slots = body_slots(N, arena.rng)
        self.slot = np.full(N, -1)
        self.assembled = False
        self.centre = self.pos.mean(0)
        self.head = unit(arena.rng.normal(size=3))
        self.amp = 0.0
        self.burncd = 0.0
        self.assemblies = 0; self.dissolves = 0; self.t_assembled = 0.0
        self.eaten = 0.0
        self.gulp_prep = 0.0; self.gulp = 0.0; self.gulp_rest = 0.0; self.gulps = 0; self.trail_eaten = 0.0

    def frame(self):
        f = self.head
        s = unit(np.cross(f, [0.0, 1.0, 0.0]) + 1e-6)
        u = np.cross(s, f)
        return np.stack([f, s, u])            # rows: body x, y, z in world

    def act(self, arena, dt):
        al = self.alive
        off, dist, k = self.pilot_vectors(arena)
        cnt = int(al.sum())
        if cnt == 0:
            return
        cen = self.pos[al].mean(0)
        tight = int((np.linalg.norm(self.pos[al] - cen, axis=1) < 260).sum())
        if not self.assembled and tight >= ASSEMBLE_N:
            self.assembled = True; self.assemblies += 1
            self.centre = cen
            v = self.vel[al].mean(0)
            self.head = unit(v) if np.linalg.norm(v) > 1 else self.head
            F = self.frame()
            world = self.centre + self.slots @ F
            free_slots = list(range(N))
            for i in np.flatnonzero(al)[np.argsort(-((self.pos[al] - cen) @ self.head))]:   # front units first
                j = free_slots[int(np.argmin(np.linalg.norm(world[free_slots] - self.pos[i], axis=1)))]
                self.slot[i] = j; free_slots.remove(j)
        elif self.assembled and cnt < DISSOLVE_N:
            self.assembled = False; self.dissolves += 1; self.slot[:] = -1
        if self.assembled:
            self.t_assembled += dt
            # composition controller: heading toward food / a curious look at the nearest pilot
            PP = np.array([p.pos for p in arena.pilots])
            dp = np.linalg.norm(PP - self.centre, axis=1)
            want = self.head.copy()
            if dp.min() < 700:
                want = unit(want + 0.8 * unit(PP[np.argmin(dp)] - self.centre))
            c = arena.mass_near(self.centre + self.head * 200, 250)
            if len(c):
                want = unit(want + 0.5 * unit(arena.mass_pos[c].mean(0) - self.centre))
            # trail scent: it follows the newest trail it can smell (it grazes your lanes behind you)
            own = np.flatnonzero(arena.mass_alive & (arena.mass_owner >= 0))
            if len(own):
                T = arena.mass_pos[own[-300:]]
                ok = np.flatnonzero(np.linalg.norm(T - self.centre, axis=1) < 600)
                if len(ok):
                    want = unit(want + 0.9 * unit(T[ok[-1]] - self.centre))
            want = unit(want - self.centre / arena.R * np.clip(np.linalg.norm(self.centre) / arena.R - 0.7, 0, 1) * 5)
            ang = np.arccos(np.clip(self.head @ want, -1, 1))
            if ang > 1e-4:
                self.head = unit(self.head + (want - self.head) * min(1.0, 0.35 * dt / ang))
            # GULP: a pilot ahead of the mouth (inside 220 u, 45 deg) -> the jaws open for 1.2 s (front units
            # flare outward = the tell), then the whole body surges at 115 u/s for 1.6 s
            mouth = self.centre + self.head * LEN * 0.5
            dm = PP - mouth
            ahead = (np.linalg.norm(dm, axis=1) < 220) & ((unit(dm) @ self.head) > 0.7)
            self.gulp_rest = max(0.0, self.gulp_rest - dt)
            if self.gulp > 0:
                self.gulp -= dt
                if self.gulp <= 0: self.gulp_rest = 4.0
            elif ahead.any() and self.gulp_rest <= 0:
                self.gulp_prep += dt
                if self.gulp_prep >= 1.2:
                    self.gulp, self.gulp_prep = 1.6, 0.0; self.gulps += 1
            else:
                self.gulp_prep = max(0.0, self.gulp_prep - 2 * dt)
            spd = 115.0 if self.gulp > 0 else 35.0
            self.centre = self.centre + self.head * spd * dt
            F = self.frame()
            # tail sweep: a pilot lingering near the tail flank makes the wave surge
            tail = self.centre - self.head * LEN * 0.4
            near_tail = (np.linalg.norm(PP - tail, axis=1) < 170).any()
            self.amp = min(1.0, self.amp + dt * 0.8) if near_tail else max(0.0, self.amp - dt * 0.4)
            s = self.slots[np.maximum(self.slot, 0)].copy()
            xb = s[:, 0] / (LEN / 2)                       # +1 head .. -1 tail
            w = np.clip(-xb, 0, 1) ** 1.5
            s[:, 1] += (12.0 + 70.0 * self.amp) * w * np.sin(2.2 * self.t - 3.0 * xb)
            jaw = np.clip(xb - 0.6, 0, 1) / 0.4 * min(1.0, self.gulp_prep / 1.2 + (self.gulp > 0))
            s[:, 1:] *= (1.0 + 1.2 * jaw)[:, None]           # the jaws flare open
            goal = self.centre + s @ F
            desired = np.where((self.slot >= 0)[:, None], (goal - self.pos) * 3.0, 0.0)
            desired = desired + self.head * spd
            self.vel = steer(self.vel, np.where((self.slot >= 0)[:, None], desired, self.vel), 600.0, dt)
            self.vel = np.where((self.slot >= 0)[:, None], np.minimum(1, (120.0 + spd) / np.maximum(np.linalg.norm(self.vel, axis=1), 1e-6))[:, None] * self.vel, self.vel)
            # mouth eats what it swims through
            mouth = self.centre + self.head * LEN * 0.5
            for j in arena.mass_near(mouth, 35.0):
                v = arena.consume(int(j), "leviathan")
                if v > 0:
                    i = np.flatnonzero(al)[0]; self.gut[i] += v; self.eaten += v
                    if arena.mass_owner[j] >= 0: self.trail_eaten += v
            if self.gulp > 0:
                for p in arena.pilots:
                    if np.linalg.norm(p.pos - mouth) < 45 + p.radius and self.burncd <= 0:
                        arena.hit(p, "bite", 0.4); self.burncd = 1.0
            xbw = np.where(self.slot >= 0, xb, 0)
            self.intent = np.clip(1.6 - dist / 150.0, 0, 1) * al
            self.intent = np.maximum(self.intent, np.where((xbw < -0.3) & (dist < 250), self.amp, 0.0))
            g = min(1.0, self.gulp_prep / 0.6) if self.gulp <= 0 else 1.0
            self.intent = np.maximum(self.intent, np.where((xbw > 0.3) & (dist < 400), g, 0.0))
        else:
            # shoal: cohesion + alignment + separation + a lazy wander; harmless
            D, d = pairwise(self.pos)
            d = np.where(al[None, :], d, np.inf)
            m = d < 120
            coh = (m[:, :, None] * D).sum(1) / np.maximum(m.sum(1, keepdims=True), 1)
            gl = unit(cen - self.pos)
            desired = unit(coh * 0.03 + gl * 0.6 + self.rng.normal(0, 0.3, self.pos.shape)) * 45.0 + separation(D, d, 14.0, al) * 30
            self.vel = steer(self.vel, desired, 90.0, dt)
            self.intent = np.zeros(N)
        self.vel = contain(self.pos, self.vel, arena.R)
        # burn: touching an assembled unit
        self.burncd = max(0.0, self.burncd - dt)
        if self.assembled and self.burncd <= 0:
            touch = al & (self.slot >= 0) & (dist < arena.pilots[0].radius + self.size + 2)
            if touch.any():
                arena.hit(arena.pilots[k[np.flatnonzero(touch)[0]]], "burn", 0.2); self.burncd = 0.5
        self.hunter_contacts(arena)
        self.publish(arena)

    def colours(self):
        c = np.tile([0.35, 0.55, 0.95], (N, 1))
        if self.assembled:
            c = c * 0.6 + np.array([1.0, 0.35, 0.2]) * 0.4
            c = c + np.clip(self.intent, 0, 1)[:, None] * np.array([0.6, 0.0, 0.0])
        return np.clip(c, 0, 1)

    def phase_stats(self):
        return dict(gulps=self.gulps, trail_eaten=round(self.trail_eaten, 1), assemblies=self.assemblies, dissolves=self.dissolves, assembled_s=round(self.t_assembled, 1),
                    eaten=round(self.eaten, 1))


def make(arena):
    return Leviathan(arena)
