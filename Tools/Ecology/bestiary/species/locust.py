"""LOCUST PHASE-CHANGER - CUTE when sparse, a TERRIFYING storm when dense and hungry. Nothing scripted
between the two: the phase is a continuous per-agent drive set by CROWDING.

Local rules:
  * graze + breed:  a locust chews the nearest environment prism (one every 3 s); a full gut (>= 40) buds a
                    newborn whose body (6) is paid out of that gut - births are funded by eaten mass, so the
                    swarm's size is the food it found (mass conserved; cell cap 360 = production gating)
  * phase g:        relaxes (tau 4 s) toward a sigmoid of how many neighbours are within 40 u - and hunger
                    lowers the threshold. That is the only switch.
  * solitary (g~0): spread out (strong separation), SHY - flee a pilot inside 90 u - small and green
  * gregarious (g~1): align + cohere (Vicsek), march at 135 u/s, eat TRAIL as well as plants, and when a
                    pilot is inside 250 u swarm it and nip (`bite`, 2 s per locust)
Telegraph (intent): g of the locusts near you - the cloud darkens, thickens and turns as one before it hits.
Counterplay: leave before it tips (a sparse cloud is harmless), outrun the march (135 < 140), or thin it -
every locust is frail (a charging pilot kills it: crystals by the handful).
"""
import numpy as np

from core import Herd, unit, steer, contain, pairwise, separation

EMOTION = "cute & shy when sparse -> terrifying storm when dense"
COUNTER = "leave before the cloud tips; outrun the march; thin it (every locust is frail)"
CAP = 360
CHEW, BREED, FULL = 5.0, 12.0, 50.0


class Locust(Herd):
    name = "locust"

    def __init__(self, arena, n=30):
        super().__init__(arena, CAP, size=2.0, body=6.0)
        env = np.flatnonzero(arena.mass_owner < 0)
        c = arena.mass_pos[arena.rng.choice(env, 3, replace=False)]
        self.pos = c[np.arange(CAP) % 3] + arena.rng.normal(0, 90.0, (CAP, 3))
        self.gut[:n] = 30.0                               # they start fed (and solitary)
        self.alive[n:] = False; self.body[n:] = 0.0
        self.g = np.zeros(CAP)
        self.chew = arena.rng.uniform(0, 3, CAP)
        self.breed = arena.rng.uniform(4, 8, CAP)
        self.age = np.full(CAP, 10.0)
        self.bitecd = np.zeros(CAP)
        self.births = 0
        self.gsum = []
        self.food = np.full(CAP, -1)
        self.tick = 0
        self.trail_eaten = 0.0
        self.feel_ph = {0: [0.0, 0.0, 0], 1: [0.0, 0.0, 0]}   # phase -> [sum speed, sum approach, n]

    def act(self, arena, dt):
        a = self.alive
        idx = np.flatnonzero(a)
        if len(idx) == 0:
            return
        P = self.pos[idx]; V = self.vel[idx]; g = self.g[idx]
        D, d = pairwise(P)
        nn = (d < 40).sum(1)
        hunger = np.clip(1.0 - self.gut[idx] / 40.0, 0, 1)
        target = 1.0 / (1.0 + np.exp(-(nn - (9.0 - 3.0 * hunger)) * 0.9))
        g = g + (target - g) * (dt / 4.0)
        # neighbours inside 60 u: alignment + cohesion, weighted by MY phase
        m = d < 60
        cntm = np.maximum(m.sum(1, keepdims=True), 1)
        align = (m[:, :, None] * V[None, :, :]).sum(1) / cntm
        coh = (m[:, :, None] * D).sum(1) / cntm
        sep = separation(D, d, 10.0 + 25.0 * (1 - g)[None, :].T[:, 0][:, None] * 0 + 18.0)
        # food: nearest edible prism within 120 u (gregarious ones also take trail)
        # food: each locust re-searches only on its turn (1 in 4 per step - the fractional update), else it
        # keeps steering at its cached prism; gregarious ones also take TRAIL
        self.tick += 1
        turn = (idx + self.tick) % 4 == 0
        for ii in np.flatnonzero(turn | (self.food[idx] < 0)):
            c = arena.mass_near(P[ii], 120.0)
            if len(c) and g[ii] < 0.5:
                c = c[arena.mass_owner[c] < 0]
            self.food[idx[ii]] = c[np.argmin(np.linalg.norm(arena.mass_pos[c] - P[ii], axis=1))] if len(c) else -1
        fj = self.food[idx]
        has = (fj >= 0)
        has[has] &= arena.mass_alive[fj[has]]
        food = np.zeros_like(P)
        food[has] = unit(arena.mass_pos[fj[has]] - P[has])
        close = has.copy()
        close[has] &= np.linalg.norm(arena.mass_pos[fj[has]] - P[has], axis=1) < 7
        for ii in np.flatnonzero(close & (self.chew[idx] <= 0)):
            j = int(fj[ii])
            v = arena.consume(j, "locust")
            self.gut[idx[ii]] += v; self.chew[idx[ii]] = CHEW
            if v > 0 and arena.mass_owner[j] >= 0:
                self.trail_eaten += v                     # indirect harm: reported, not a strike
        off, dist, k = self.pilot_vectors(arena)
        off, dist, k = off[idx], dist[idx], k[idx]
        topilot = unit(off)
        shy = (dist < 90) & (g < 0.5)
        swarm = (dist < 250) & (g >= 0.5)
        sol = unit(self.rng.normal(0, 1, P.shape) + 0.6 * food) * 25.0 + sep * 40.0 - topilot * shy[:, None] * 70.0
        gre = unit(align / 135.0 * 1.5 + coh * 0.02 + 0.8 * food + 1.6 * topilot * swarm[:, None]) * 135.0 + sep * 30.0
        desired = sol * (1 - g)[:, None] + gre * g[:, None]
        acc = 80.0 + 300.0 * g
        V = steer(V, desired, acc, dt)
        self.vel[idx] = V
        self.vel = contain(self.pos, self.vel, arena.R)
        self.g[idx] = g
        # telegraph: how gregarious the locusts are AND whether they are already converging on you
        self.intent[idx] = g * np.clip(1.6 - dist / 400.0, 0, 1)
        # bites
        self.bitecd = np.maximum(0, self.bitecd - dt)
        self.chew = np.maximum(0, self.chew - dt)
        for ii in np.flatnonzero(swarm & (dist < 6 + 2 + 2)):
            i = idx[ii]
            if self.bitecd[i] <= 0:
                arena.hit(arena.pilots[k[ii]], "bite", 0.05); self.bitecd[i] = 2.0
        # phase-split feel (what does each phase FEEL like near a pilot?)
        near = dist < 300
        for ph, sel in ((0, near & (g < 0.5)), (1, near & (g >= 0.5))):
            if sel.any():
                rel = V[sel] - arena.pilots[0].vel
                appr = np.sum(rel * (-off[sel]), axis=1) / np.maximum(dist[sel], 1e-6)
                f = self.feel_ph[ph]; f[0] += np.linalg.norm(V[sel], axis=1).sum(); f[1] += appr.sum(); f[2] += sel.sum()
        # breed: a full gut buds a newborn (body paid from the gut)
        self.breed = np.maximum(0, self.breed - dt)
        self.age += dt
        free = np.flatnonzero(~self.alive)
        for i in idx[(self.gut[idx] >= FULL) & (self.breed[idx] <= 0)]:
            if len(free) == 0:
                break
            j, free = free[0], free[1:]
            self.alive[j] = True; self.body[j] = 6.0; self.gut[i] -= 6.0; self.gut[j] = 0.0
            self.pos[j] = self.pos[i] + self.rng.normal(0, 3, 3); self.vel[j] = self.vel[i]
            self.g[j] = self.g[i]; self.age[j] = 0.0; self.breed[i] = BREED; self.breed[j] = BREED
            self.chew[j] = 3.0; self.births += 1
        self.size = 2.0 * np.clip(self.age / 2.0, 0.05, 1.0) * (1.0 + 0.5 * self.g)   # newborns grow in
        self.gsum.append(float((self.g[self.alive] > 0.5).mean()))
        self.hunter_contacts(arena)
        self.publish(arena)

    def colours(self):
        g = self.g[:, None]
        return np.array([0.45, 0.95, 0.4]) * (1 - g) + np.array([1.0, 0.8, 0.1]) * g

    def phase_stats(self):
        out = dict(births=self.births, trail_eaten=round(self.trail_eaten, 1), greg_frac_end=round(self.gsum[-1], 3) if self.gsum else 0.0,
                   greg_frac_max=round(max(self.gsum), 3) if self.gsum else 0.0,
                   t_tip=next((round(0.1 * i, 1) for i, x in enumerate(self.gsum) if x > 0.3), -1))
        for ph, nm in ((0, "solitary"), (1, "gregarious")):
            s, ap, n = self.feel_ph[ph]
            out[f"{nm}_speed"] = round(s / n, 1) if n else 0.0
            out[f"{nm}_approach"] = round(ap / n, 1) if n else 0.0
        return out


def make(arena):
    return Locust(arena)
