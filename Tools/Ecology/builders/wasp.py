"""Nest weavers v2 - the WASP COMB (Theraulaz & Bonabeau 1995 lattice swarms). The same steal-carry-deposit
colony as builders/nest.py, with a different LOCAL RULE TABLE: it builds a stalk (petiole) out from its core,
hangs flat combs off the stalk at intervals, and wraps the combs in an envelope.

"Up" is the cell's outward radial at the anchor - one vector, like gravity, read locally. The lattice is aligned to
it (z = up). A site's brick TYPE (stalk 1 / comb 2 / envelope 3) is part of the local configuration the rules read.

Rules (q = a free site; z-neighbours = the sites directly below/above; ring = the 8 in-plane neighbours):
  STALK     the site below is stalk, and either the bare stalk run under q (stalk sites with no comb in their ring,
            probed at most `gap`+1 down) is shorter than `gap`, or the comb hanging at the level below is mature
            (>= `comb_mature` comb sites in a 5x5 window). -> stalk climbs, pauses for a comb, climbs again.
  COMB      q's ring touches the stalk or comb, neither z-neighbour is comb (one sheet per level), and the stalk at
            this level has a bare run >= `gap` under it (a new comb seeds) or q's level already holds comb.
            Weight grows with ring count (compact, Eden-like sheets) and falls with the brood pheromone the stalk
            emits, exp(-(rho / comb_R)^2), rho = in-plane distance to the stalk.
  ENVELOPE  q has NO ring contact but a comb site diagonally below it in the next ring out (the comb's rim), or an
            envelope site in its 26-neighbourhood with a comb within 2 below -> a skirt that curls down over the
            rims and closes into a paper envelope with a mouth.
Brood: comb cells are brood cells; one crystal per `per_brood` comb cells.
"""
from __future__ import annotations

import numpy as np

from builders.core import Colony, Lattice, N26

RING = [(x, y, 0) for x in (-1, 0, 1) for y in (-1, 0, 1) if (x, y) != (0, 0)]


class WaspComb(Colony):
    color = (1.0, 0.8, 0.2)
    note = ("Wasp comb (lattice-swarm rules): a stalk climbs out from the core, flat combs hang off it, an envelope "
            "skirts the rims. Every brick is a stolen prism. Raid the combs for brood crystals.")

    def __init__(self, arena, seed=0, n=48, gap=3, comb_mature=14, comb_R=26.0, envelope=True, per_brood=12,
                 alarm=120.0, name="wasp", env_gap=2, mouth=2.5):
        rng = np.random.default_rng(seed + 5)
        d = rng.normal(size=3); d /= np.linalg.norm(d); anchor = d * 450.0
        up = anchor / np.linalg.norm(anchor)
        a = np.cross(up, [0.3, 1, 0.1]); a /= np.linalg.norm(a); b = np.cross(up, a)
        super().__init__(arena, n, anchor, dom=2, s=8.0, speed=70.0, sense=220.0, frac=4, name=name, seed=seed)
        self.lat = Lattice(anchor, 8.0, half=24, basis=np.array([a, b, up]))
        m = self.lat.m
        self.typ = np.zeros((m, m, m), np.int8)
        h = self.lat.half
        self.root = (h, h, h)
        self.typ[self.root] = 1; self.lat.blocked[self.root] = True        # the core is the petiole's base
        self.gap, self.mature, self.comb_R, self.envelope, self.per_brood = gap, comb_mature, comb_R, envelope, per_brood
        self.alarm_r = alarm; self.env_gap = env_gap; self.mouth = mouth
        self.cool = np.zeros(n); self.store = 0; self.raided = 0
        self.extent = 120.0
        self.top = 0
        self.tgt = np.tile(self.lat.pos(self.root), (n, 1))

    # -- helpers ----------------------------------------------------------------------------------------------
    def T(self, q):
        return self.typ[q] if self.lat.inside(q) else 0

    def bare_run(self, q, limit):
        """Consecutive stalk sites directly below q that carry no comb in their ring (<= limit)."""
        n = 0; z = q[2] - 1
        while n < limit and z >= 0:
            s = (q[0], q[1], z)
            if self.T(s) != 1:
                break
            if any(self.T((s[0] + o[0], s[1] + o[1], z)) == 2 for o in RING):
                break
            n += 1; z -= 1
        return n

    def win(self, q, r, types):
        x, y, z = q; a = self.typ[max(0, x - r):x + r + 1, max(0, y - r):y + r + 1, max(0, z - r):z + r + 1]
        return bool(np.isin(a, types).any())

    def comb_at(self, s, w=2):
        z = s[2]; n = 0
        for x in range(-w, w + 1):
            for y in range(-w, w + 1):
                if self.T((s[0] + x, s[1] + y, z)) == 2:
                    n += 1
        return n

    # -- the rule table ------------------------------------------------------------------------------------------
    def classify(self, q):
        """Returns (brick type, probability) for a free site from its local configuration only."""
        below, above = (q[0], q[1], q[2] - 1), (q[0], q[1], q[2] + 1)
        ring = [(q[0] + o[0], q[1] + o[1], q[2]) for o in RING]
        tb = self.T(below)
        # STALK
        if tb == 1:
            run = self.bare_run(q, self.gap + 1)
            if run < self.gap:
                return 1, 0.6
            if self.comb_at((q[0], q[1], q[2] - 1)) >= self.mature:
                return 1, 0.4
        # COMB: a sheet in the plane of a stalk site
        rt = [self.T(r) for r in ring]
        if (1 in rt or 2 in rt) and self.T(below) != 2 and self.T(above) != 2 and tb != 1:
            stalk = [r for r, t in zip(ring, rt) if t == 1]
            level_has_comb = 2 in rt
            seed_ok = any(self.bare_run((st[0], st[1], st[2] + 1), self.gap + 1) >= self.gap or self.comb_at(st) > 0 for st in stalk)
            if level_has_comb or seed_ok:
                rho = np.hypot(q[0] - self.root[0], q[1] - self.root[1]) * self.lat.s
                nb = sum(1 for t in rt if t in (1, 2))
                return 2, min(1.0, 0.25 * nb) * float(np.exp(-(rho / self.comb_R) ** 2))
        # ENVELOPE: a paper shell held off the comb stack - no comb/stalk within 1 site, some comb within
        # `env_gap` sites, touching the rim zone or existing envelope; never in the mouth under the stalk
        if self.envelope and 2 not in rt and 1 not in rt:
            if self.win(q, 1, (1, 2)):
                return 0, 0.0
            if not self.win(q, self.env_gap, (2,)):
                return 0, 0.0
            rho = np.hypot(q[0] - self.root[0], q[1] - self.root[1])
            if rho < self.mouth and q[2] <= self.root[2] + 1:
                return 0, 0.0                                   # the mouth stays open
            nb3 = sum(1 for o in N26 if self.T((q[0] + o[0], q[1] + o[1], q[2] + o[2])) == 3)
            near_rim = self.win(q, 2, (2,))
            if nb3 or near_rim:
                return 3, (0.03 if not nb3 else 0.06 + 0.05 * min(nb3, 4))
        return 0, 0.0

    def deposit_score(self, arena, site):
        t, p = self.classify(site)
        self._last = t
        return p

    def try_deposit(self, arena, k):
        here = self.lat.site_of(self.agent_pos[k])
        cands = []
        for o in N26[self.rng.choice(26, 10, replace=False)]:
            q = (here[0] + o[0], here[1] + o[1], here[2] + o[2])
            if self.lat.free(q):
                t, p = self.classify(q)
                if p > 0:
                    cands.append((p, q, t))
        if not cands:
            return False
        p, q, t = max(cands)
        if self.rng.random() >= p:
            return False
        i = int(self.carry[k])
        arena.move_mass(i, self.lat.pos(q))
        self.lat.place(q, i, arena.t); self.typ[q] = t
        self.carry[k] = -1; self.placed += 1; self.register(arena, i)
        if arena.mass_trail[i]:
            self.placed_trail += 1
        if t == 1:
            self.top = max(self.top, q[2] - self.root[2])
        if t == 2 and (self.typ == 2).sum() % self.per_brood == 0:
            self.store += 1
        return True

    def home(self, arena, k):
        # a laden wasp WALKS ON THE NEST: it heads for a brick (or the core) and, on arrival, steps to a random
        # neighbouring brick - a random walk over the structure's own surface, where every rule can fire
        if np.linalg.norm(self.tgt[k] - self.agent_pos[k]) < 5:
            here = self.lat.site_of(self.agent_pos[k])
            nb = [(here[0] + o[0], here[1] + o[1], here[2] + o[2]) for o in N26]
            built = [q for q in nb if self.lat.inside(q) and self.typ[q] > 0]
            if built and self.rng.random() < 0.85:
                q = built[self.rng.integers(len(built))]
            else:
                bricks = list(self.lat.sites.values()) + [self.root]
                q = bricks[self.rng.integers(len(bricks))]
            self.tgt[k] = self.lat.pos(q) + self.rng.normal(0, 2.5, 3)
        return self.tgt[k]

    def behave(self, arena, dt):
        for s in self.sweep_destroyed(arena):
            self.typ[s] = 0
        self.cool -= dt; self.intent[:] = 0
        arena.targets = [self.lat.anchor + np.array([0, 0, (self.top / 2) * self.lat.s]) @ self.lat.B]
        arena.threats = list(self.agent_pos[self.alive][::4])
        for p in arena.pilots:
            dn = np.linalg.norm(p.pos - arena.targets[0])
            if dn < self.alarm_r:
                d = np.linalg.norm(self.agent_pos - p.pos, axis=1)
                for k in np.flatnonzero(self.alive & (self.carry < 0) & (d < 200)):
                    self.steer(k, p.pos + p.vel * 0.3, dt, speed=self.speed * 1.6)
                    self.intent[k] = float(np.clip(1 - d[k] / 120, 0, 1))
                    if d[k] < p.radius + 4 and self.cool[k] <= 0:
                        arena.hit(p, "sting"); self.cool[k] = 1.5
            if p.policy in ("hunter", "cutter") and dn < 15 and self.store > 0:
                self.crystals += self.store; self.raided += self.store; self.store = 0

    def metrics(self, arena, minutes):
        m = super().metrics(arena, minutes)
        m.update(stalk=int((self.typ == 1).sum()) - 1, comb=int((self.typ == 2).sum()), envelope=int((self.typ == 3).sum()),
                 tiers=self.tiers(), brood_store=self.store, raided=self.raided)
        return m

    def tiers(self):
        z = np.unique(np.argwhere(self.typ == 2)[:, 2]) if (self.typ == 2).any() else []
        return len(z)

    def hud(self, arena):
        return f"stalk {(self.typ == 1).sum() - 1} comb {(self.typ == 2).sum()} ({self.tiers()} tiers) envelope {(self.typ == 3).sum()} brood {self.store}"
