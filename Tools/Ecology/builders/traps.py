"""Species 3 - TRAP BUILDERS. A colony learns where pilots fly from the trail they leave, strips that trail, and
spins it into WEBS OF DANGER PRISMS strung across the lanes.

How it learns (stigmergy, no pilot tracking): every trail prism a pilot lays is a mark in the world with an
orientation (the heading it was laid on). The colony keeps a coarse LANE field L (trail prisms per cell, an
exponential memory) and a FLOW field D (their summed headings). The memory outlives the trail - the colony steals
the very prisms that taught it - so a lane is remembered for ~a minute after the pilot stopped using it.

How it builds (local rules on a sparse lattice, s = 10 u):
  * a laden worker climbs L (six probes one field cell away);
  * a free site is a candidate if L there is above `lane_min`;
  * it must touch the web (26-neighbourhood) - or, rarely, nucleate where L is near its local peak;
  * the attach direction e (site minus the neighbour it grows from) is weighted by (1 - |e . D|)^2: strands grow
    ACROSS the flow, not along it -> curtains across the lane;
  * the FILAMENT rule: a site with more than `max_nb` web neighbours is refused, so strands stay thin and the web
    keeps HOLES a pilot can thread (counterplay) instead of becoming a wall.
Every placed prism is made DANGEROUS (Arena.set_danger - in game Prism.MakeDangerous, exactly what
WallAssembler does to its bottom mates). A pilot that touches one is burned and the prism is sprung (destroyed by
the collision, an active force); the colony re-spins.
"""
from __future__ import annotations

import numpy as np

from builders.core import Colony, Field, SparseLattice, N26


class TrapBuilders(Colony):
    color = (0.7, 0.95, 0.3)
    note = ("Trap builders: they read the lanes from your trail (and steal it), then string webs of DANGER prisms "
            "(orange) across the lanes, strands perpendicular to the flow with holes you can thread.")

    def __init__(self, arena, seed=0, n=60, lane_min=1.2, max_nb=2, nucleate=0.02, perp=True, memory_s=60.0,
                 name="traps", danger=True, cooldown=0.5):
        rng = np.random.default_rng(seed + 9)
        d = rng.normal(size=3); d /= np.linalg.norm(d)
        super().__init__(arena, n, d * 300.0, dom=2, s=10.0, speed=85.0, sense=200.0, frac=4, name=name, seed=seed)
        self.lat = SparseLattice(self.lat.anchor, 10.0)
        self.agent_pos = self.lat.anchor + self.rng.normal(0, 200, (n, 3))
        self.L = Field(arena.R, 40.0); self.D = [Field(arena.R, 40.0) for _ in range(3)]
        self.S = Field(arena.R, 40.0)          # lane SCENT: L diffused wide, the recruitment cue that pulls workers in
        self.lane_min, self.max_nb, self.nuc, self.perp, self.danger = lane_min, max_nb, nucleate, perp, danger
        self.decay = 1.0 - 0.5 ** (0.1 / memory_s)     # per-step at dt 0.1
        self.seen = 0
        self.last_trail = {}                            # pilot domain -> last trail prism index (heading)
        self.sprung = []                                # (t, age_s) of every trap a pilot touched
        self.burn_cool = {}; self.cooldown = cooldown
        self.extent = 600.0

    # -- learning the lanes from the trail -------------------------------------------------------------------
    def learn(self, arena):
        n = len(arena.mass_vol)
        new = np.arange(self.seen, n)
        new = new[arena.mass_trail[new]] if len(new) else new
        for i in new:
            dom = int(arena.mass_dom[i]) if arena.mass_dom[i] != self.dom else -1
            j = self.last_trail.get(dom)
            p = arena.mass_pos[i]
            self.L.deposit(p, 1.0)
            if j is not None:
                h = p - arena.mass_pos[j]; nh = np.linalg.norm(h)
                if 1e-6 < nh < 60:
                    for a in range(3):
                        self.D[a].deposit(p, h[a] / nh)
            self.last_trail[dom] = i
        self.seen = n
        self.L.decay(self.decay)
        for f in self.D:
            f.decay(self.decay)
        if self.tick % 10 == 0:
            self.S.a = self.L.a.copy()
            for _ in range(4):
                self.S.blur(0.6)

    def flow(self, p):
        v = np.array([f.sample(p) for f in self.D]); n = np.linalg.norm(v)
        return v / n if n > 1e-6 else None

    # -- hooks ---------------------------------------------------------------------------------------------------
    def wants_material(self, arena, k):
        return self.S.sample(self.agent_pos[k]) > 0.05 * self.lane_min

    def forage_bias(self, arena, k):
        g = self.S.grad(self.agent_pos[k]); n = np.linalg.norm(g)
        return g / n * 60.0 if n > 1e-9 else np.zeros(3)

    def home(self, arena, k):
        p = self.agent_pos[k]; h = self.L.h
        F = self.L if self.L.sample(p) > 0.2 * self.lane_min else self.S
        here = F.sample(p)
        best, bv = None, here
        for d in np.vstack([np.eye(3), -np.eye(3)]):
            v = F.sample(p + d * h)
            if v > bv:
                best, bv = d, v
        if best is None:                   # at a local peak of the lane: hover around it
            return p + self.rng.normal(0, 15, 3)
        return p + best * h

    def deposit_score(self, arena, site):
        x = self.lat.pos(site)
        L = self.L.sample(x)
        if L < self.lane_min:
            return 0.0
        nb = [(site[0] + o[0], site[1] + o[1], site[2] + o[2]) for o in N26]
        attached = [q for q in nb if q in self.lat.d]
        if not attached:
            return self.nuc * min(1.0, L / (4 * self.lane_min))
        if len(attached) > self.max_nb:
            return 0.0
        w = 1.0
        if self.perp:
            D = self.flow(x)
            if D is not None:
                e = np.asarray(site, float) - np.asarray(attached[0], float); e /= np.linalg.norm(e)
                w = (1.0 - abs(float(e @ D))) ** 2
        return min(1.0, 0.6 * w * min(1.0, L / (3 * self.lane_min)))

    def on_placed(self, arena, i, site):
        if self.danger:
            arena.set_danger(i, True)

    def behave(self, arena, dt):
        self.learn(arena)
        self.sweep_destroyed(arena)
        arena.targets = list(self.agent_pos[self.alive][::3])
        web = np.array(list(self.lat.sites.keys()), np.int64)
        arena.threats = list(arena.mass_pos[web[::4]]) if len(web) else []
        if not len(web):
            return
        for p in arena.pilots:
            for c in arena.mass_near(p.pos, p.radius + 4.0):
                if c in self.lat.sites and arena.mass_danger[c]:
                    site = self.lat.sites[c]
                    age = arena.t - self.lat.tp.get(site, arena.t)
                    if arena.t >= self.burn_cool.get(p.name, -1):
                        arena.hit(p, "burn"); self.burn_cool[p.name] = arena.t + self.cooldown
                    self.sprung.append((round(arena.t, 1), round(age, 1)))
                    arena.destroy(int(c), by=p.name)          # the collision springs (removes) the trap prism

    def metrics(self, arena, minutes):
        m = super().metrics(arena, minutes)
        ages = [a for _, a in self.sprung]
        m.update(sprung=len(self.sprung), trap_age_med=round(float(np.median(ages)), 1) if ages else None,
                 web_components=len(set()) if False else None)
        pts = self.lat.occupancy_points()
        if len(pts) > 3:
            # how much of the web sits on a lane, and how perpendicular its strands run to the flow
            Ls = self.L.sample(pts)
            m["on_lane_frac"] = round(float(np.mean(Ls >= self.lane_min)), 3)
        return m

    def hud(self, arena):
        return f"web {self.lat.n_built()}  sprung {len(self.sprung)}"
