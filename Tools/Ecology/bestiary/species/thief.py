"""THIEVES - MISCHIEF. Quick little magpies that dart into your wake, snatch the freshest trail prisms and
carry them home to a hoard. Nothing is destroyed: a prism CHANGES HANDS (the serpent walls' `Steal`) and is
carried, so the mass is conserved and your loss is their nest.

Local rules:
  * scout:   a free thief wants only the WARM wake (laid < 2 s ago): the freshest unclaimed one within 400 u,
             claimed in a claim book; a ship in sight (700 u) with no warm wake in reach is tailed (a claim book, like `PrismSpatialIndex.TryReserve`)
  * snatch:  inside 5 u the prism changes hands (`steal`) and rides in the thief's grip
  * homing:  laden, it flies home at HALF speed (75 u/s) and drops the prism on its nest's hoard shell
  * timid:   a free thief with a pilot pointing at it inside 120 u veers off (they are bold, not brave)
Telegraph (intent): a thief locked onto your wake and within 250 u of you (gulls behind a trawler) - you see it come in on your tail.
Counterplay: turn back - a laden thief is slow and anything you hit drops its prism back to YOU (recapture);
weave so your wake is not where they expect; or raid the hoard (the nest is all your stolen mass in one
place, a fat target). Payoff: each thief is a crystal, and recaptured prisms are yours again.
"""
import numpy as np

from core import Herd, unit, steer, contain, pairwise, separation

EMOTION = "mischief (cheeky, annoying)"
COUNTER = "turn back on laden thieves (recapture); weave your wake; raid the hoard"
FREE_V, LADEN_V, SCOUT, WARM, SPOT = 150.0, 75.0, 400.0, 1.5, 700.0
THIEF_OWNER = -2


class Thief(Herd):
    name = "thief"

    def __init__(self, arena, n=18):
        super().__init__(arena, n, spread=30.0, size=2.2, body=3.0)
        env = np.flatnonzero(arena.mass_owner < 0)
        self.nest = arena.mass_pos[arena.rng.choice(env)].copy()
        self.pos = self.nest + arena.rng.normal(0, 20, (n, 3))
        self.claim = np.full(n, -1); self.carry = np.full(n, -1)
        self.stolen_by = {}            # prism -> pilot it was stolen from (for recapture)
        self.laid_t = np.zeros(0)        # when each prism appeared (a thief only wants the WARM wake)
        self.steals = 0; self.stolen_vol = 0.0; self.recaptured = 0; self.hoard = 0

    def act(self, arena, dt):
        al = self.alive
        off, dist, k = self.pilot_vectors(arena)
        own = arena.mass_owner
        n = len(arena.mass_vol)
        if len(self.laid_t) < n:
            self.laid_t = np.concatenate([self.laid_t, np.full(n - len(self.laid_t), self.t)])
        warm = 1e9 if getattr(self, 'ablate', None) == 'cold' else WARM
        trail = np.flatnonzero(arena.mass_alive & (own >= 0) & (self.t - self.laid_t <= warm))
        claimed = set(self.claim[self.claim >= 0].tolist()) | set(self.carry[self.carry >= 0].tolist())
        D, d = pairwise(self.pos)
        des = np.zeros_like(self.vel)
        PV = np.array([p.vel for p in arena.pilots])
        for i in np.flatnonzero(al):
            if self.carry[i] >= 0:
                des[i] = unit(self.nest - self.pos[i]) * LADEN_V
                if np.linalg.norm(self.nest - self.pos[i]) < 12:
                    j = self.carry[i]
                    arena.mass_pos[j] = self.nest + unit(self.rng.normal(size=3)) * (8 + 1.5 * np.cbrt(self.hoard + 1))
                    self.carry[i] = -1; self.hoard += 1
                continue
            c = self.claim[i]
            if c < 0 or not arena.mass_alive[c] or own[c] < 0 or self.t - self.laid_t[c] > warm + 1.5:
                self.claim[i] = -1
                if len(trail):
                    cand = trail[np.linalg.norm(arena.mass_pos[trail] - self.pos[i], axis=1) < SCOUT]
                    cand = np.array([x for x in cand if x not in claimed], int)
                    if len(cand):
                        self.claim[i] = cand.max()           # freshest = highest index (laid last)
                        claimed.add(int(self.claim[i]))
            c = self.claim[i]
            if c >= 0:
                to = arena.mass_pos[c] - self.pos[i]
                des[i] = unit(to) * FREE_V
                if np.linalg.norm(to) < 5:
                    o = int(own[c])
                    own[c] = THIEF_OWNER; self.carry[i] = c; self.claim[i] = -1
                    self.stolen_by[int(c)] = o; self.steals += 1; self.stolen_vol += arena.mass_vol[c]
                    arena.hit(arena.pilots[o], "steal", float(arena.mass_vol[c]))
            elif dist[i] < SPOT and warm < 1e9:
                # a ship in sight: fall in behind it, like gulls behind a trawler
                p = arena.pilots[k[i]]
                des[i] = unit(p.pos - unit(p.vel) * 70.0 - self.pos[i]) * FREE_V
            else:
                des[i] = unit(self.nest - self.pos[i] + self.rng.normal(0, 30, 3)) * 30.0
        # timid: a pilot pointing at a free thief inside 120 u
        pointing = np.sum(unit(PV[k]) * unit(-off), axis=1) > 0.85
        shy = al & (self.carry < 0) & (dist < 120) & pointing & (getattr(self, 'ablate', None) != 'bold')
        side = unit(np.cross(PV[k], [0.0, 1.0, 0.0]) + 1e-6)
        des[shy] = (side[shy] * np.sign(np.sum(side[shy] * -off[shy], axis=1))[:, None]) * FREE_V
        des += separation(D, d, 8.0, al) * 40
        self.vel = steer(self.vel, des, np.where(self.carry >= 0, 200.0, 500.0), dt)
        self.vel = contain(self.pos, self.vel, arena.R)
        # the carried prism rides in the grip (positions set before the base loop moves the thief)
        for i in np.flatnonzero(al & (self.carry >= 0)):
            arena.mass_pos[self.carry[i]] = self.pos[i] + self.vel[i] * dt - unit(self.vel[i]) * 3.0
        locked = al & (self.claim >= 0) & (dist < 300)
        tailing = al & (self.claim < 0) & (self.carry < 0) & (dist < 300)      # gulls gathering in your wake
        self.intent = np.where(locked, 1.0, np.where(tailing, 0.7, np.where(self.carry >= 0, 0.3, 0.0)))
        # a hunter knocks a thief down: its prism goes back to the pilot it was stolen from
        for p in arena.pilots:
            if p.policy != "hunter":
                continue
            dd = np.linalg.norm(self.pos - p.pos, axis=1)
            for i in np.flatnonzero(al & (dd < p.radius + self.size + 4)):
                j = self.carry[i]
                if j >= 0:
                    own[j] = self.stolen_by.get(int(j), 0); self.recaptured += 1; self.carry[i] = -1
                self.kill(i, arena)
        self.publish(arena)

    def colours(self):
        c = np.tile([0.3, 0.3, 0.45], (len(self.pos), 1))
        c[self.claim >= 0] = [0.85, 0.85, 1.0]
        c[self.carry >= 0] = [1.0, 0.85, 0.3]
        return c

    def render_extra(self, out, arena):
        """Viewer only: the prisms the thieves hold (carried + the hoard), in gold."""
        h = np.flatnonzero(arena.mass_alive & (arena.mass_owner == THIEF_OWNER))
        out["hoard"] = dict(pos=arena.mass_pos[h], col=np.tile([0.95, 0.75, 0.2], (len(h), 1)), size=np.full(len(h), 3.0))

    def phase_stats(self):
        return dict(steals=self.steals, stolen_vol=round(self.stolen_vol, 1), recaptured=self.recaptured,
                    hoard=self.hoard)


ABLATIONS = {"cold": "takes ANY trail prism (not only the warm wake) and never tails a ship",
             "bold": "never veers off a pilot pointing at it"}


def make(arena, ablate=None):
    sp = Thief(arena); sp.ablate = ablate
    return sp
