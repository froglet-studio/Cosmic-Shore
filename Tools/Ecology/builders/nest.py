"""Species 1 - NEST WEAVERS. A colony that steals loose and trail prisms and weaves them into a royal chamber
around its core, where it keeps its brood (crystals). Raiding the nest pays.

Construction (no blueprint - Bonabeau, Theraulaz et al. 1998 + Ladley & Bullock 2005):
  * the CORE emits a template pheromone Q(r) that peaks in a shell at r = Rc (the queen's royal-chamber cue);
  * every placed prism emits CEMENT that diffuses and decays (Khuong et al. 2016's time cue): deposit probability
    rises with cement (positive feedback -> pillars that merge into walls);
  * a worker may deposit only on a site touching the structure (or, rarely, nucleate a new pillar where Q is high);
  * logistics: a worker must fetch real mass, so build rate is set by the supply, and the nest grows on the side
    the material comes from (Ladley & Bullock's finding, which is what makes two nests differ).
Brood: one crystal per GROWTH_PER_BROOD prisms woven (flora's GrowthPerOffspring currency).
Threat: workers near the nest turn on a pilot that comes within the alarm radius and sting (danger contact).
"""
from __future__ import annotations

import numpy as np

from builders.core import Colony, N26


class NestWeavers(Colony):
    color = (1.0, 0.55, 0.15)
    note = ("Nest weavers: steal loose + trail prisms (watch them turn ruby), weave a royal chamber around the "
            "core by a Q-template + cement rule. Gold = laden worker. Raid the core for the brood crystals.")

    def __init__(self, arena, seed=0, n=48, Rc=36.0, w=9.0, k_cement=0.6, nucleate=0.01, alarm=110.0,
                 growth_per_brood=20, anchor=None, name="nest", homing="shell"):
        rng = np.random.default_rng(seed + 5)
        if anchor is None:
            d = rng.normal(size=3); d /= np.linalg.norm(d); anchor = d * 450.0
        super().__init__(arena, n, anchor, dom=2, s=8.0, speed=70.0, sense=220.0, frac=4, name=name, seed=seed)
        self.Rc, self.w, self.kc, self.nuc, self.alarm_r = Rc, w, k_cement, nucleate, alarm
        self.gpb = growth_per_brood
        self.homing = homing          # "shell": walk to a drifting bearing on the Rc shell; "core": walk home to the core
        m = self.lat.m
        self.cement = np.zeros((m, m, m), np.float32)
        h = self.lat.half
        g = np.indices((m, m, m)).transpose(1, 2, 3, 0) - h
        self.rsite = np.linalg.norm(g, axis=-1) * self.lat.s          # site distance from the core
        self.Q = np.exp(-((self.rsite - Rc) / w) ** 2).astype(np.float32)
        self.lat.blocked[self.rsite < Rc * 0.55] = True              # the brood chamber stays open
        self.dirs = rng.normal(size=(n, 3)); self.dirs /= np.linalg.norm(self.dirs, axis=1, keepdims=True)
        self.store = 0; self.raided = 0
        self.out = np.zeros(n, bool)
        self.cool = np.zeros(n)
        self.extent = Rc + 3 * w
        self.breaches = []

    # -- the LOCAL rule ---------------------------------------------------------------------------------------
    def deposit_score(self, arena, site):
        q = float(self.Q[site])
        if q < 0.05:
            return 0.0
        c = float(self.cement[site])
        attach = self.lat.count(site) > 0
        if not attach:
            return q * self.nuc
        return q * (0.05 + c * c / (c * c + self.kc * self.kc))

    def home(self, arena, k):
        if self.homing == "core":
            # laden: walk home to the core; reaching the empty brood chamber without having dropped, walk back
            # out along a fresh bearing and try again (the termite's loaded random walk, Ladley & Bullock)
            r = np.linalg.norm(self.agent_pos[k] - self.lat.anchor)
            if r < self.Rc * 0.6 and not self.out[k]:
                self.out[k] = True; d = self.rng.normal(size=3); self.dirs[k] = d / np.linalg.norm(d)
            elif r > self.Rc * 1.8:
                self.out[k] = False
            return self.lat.anchor + (self.dirs[k] * self.Rc * 2.2 if self.out[k] else 0.0)
        # a laden worker walks to "its" bearing on the shell; the bearing drifts (a random walk, not a plan)
        d = self.dirs[k] + self.rng.normal(0, 0.05, 3); self.dirs[k] = d / np.linalg.norm(d)
        return self.lat.anchor + self.dirs[k] * self.Rc

    def on_placed(self, arena, i, site):
        self.cement[site] += 1.0
        if self.placed % self.gpb == 0:
            self.store += 1

    # -- threat + upkeep --------------------------------------------------------------------------------------
    def behave(self, arena, dt):
        for s in self.sweep_destroyed(arena):
            self.breaches.append((arena.t, s))
        if self.tick % 5 == 0:
            a = self.cement
            sm = np.zeros_like(a)
            sm[1:] += a[:-1]; sm[:-1] += a[1:]; sm[:, 1:] += a[:, :-1]; sm[:, :-1] += a[:, 1:]
            sm[:, :, 1:] += a[:, :, :-1]; sm[:, :, :-1] += a[:, :, 1:]
            self.cement = (0.97 * (0.8 * a + 0.2 / 6 * sm)).astype(np.float32)
        arena.targets = [self.lat.anchor]                 # a raider flies at the core
        arena.threats = list(self.agent_pos[self.alive][::4])
        self.defend(arena, dt, self.lat.anchor, self.alarm_r)
        for p in arena.pilots:
            dn = np.linalg.norm(p.pos - self.lat.anchor)
            if p.policy in ("hunter", "cutter") and dn < 12 and self.store > 0:
                self.crystals += self.store; self.raided += self.store; self.store = 0

    def metrics(self, arena, minutes):
        m = super().metrics(arena, minutes)
        pts = self.lat.occupancy_points()
        if len(pts):
            r = np.linalg.norm(pts - self.lat.anchor, axis=1)
            m["shell_frac"] = round(float(np.mean(np.abs(r - self.Rc) < 1.5 * self.w)), 3)
        m.update(brood_store=self.store, raided=self.raided, breaches=len(self.breaches))
        return m

    def hud(self, arena):
        return f"built {self.lat.n_built()}  brood {self.store}  raided {self.raided}"

    def render(self, out):
        super().render(out)
        out[self.name + "_core"] = dict(pos=[self.lat.anchor], col=[(1.0, 0.9, 0.4)],
                                        size=[4 + 1.5 * min(self.store, 12)])
