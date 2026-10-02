"""Species F5 - WALKING MIMIC (a treadmilling thicket that walks toward food and baits pilots with a fake crystal).

Locomotion is TREADMILLING (actin-like, and mass-conserving by construction): each walker is a heart + a shell of
body prisms around it. To move it lays new prisms on its LEADING hemisphere (from its reserve) and RESORBS the ones
left on its trailing side (back into the reserve). The heart drifts after the shell's centroid. Nothing travels as
a rigid body, so in game it is lays + resorbs + one heart pose, and it reads as a thicket that grows forward and
withers behind - the walking-forest silhouette.

It walks up the FOOD gradient (`sense` u, 8 sampled directions; food includes pilots' TRAILS, so a walker drifts
into the lanes you fly) at `speed` u/s, faster when hungry (`hunger` = reserve below a body's worth).

The LURE: a pseudo-crystal glowing at its crown. Pilots divert to anything that looks like a crystal; a reader can
see the TELL (the lure breathes in time with the plant; a real crystal does not) only inside `tell` u. When a pilot
comes within `prime` u the thicket TREMBLES (the telegraph, `t_prime` s), and when it reaches `strike` u of the lure
the thorns ERUPT: danger thorns extend outward from the shell to `thorn` u over `t_erupt` s (animated), then retract.
Contact with an extended thorn burns. Counterplay: read the tell and skip the bait, or take the bait FAST - the
eruption takes `t_erupt` s to reach full reach.

Reproduction: a walker whose reserve exceeds a body's worth BUDS a new walker beside it (crystal first, then shell).
Collecting a heart drops its crystal; the shell stays as an inert skeleton.
"""
from __future__ import annotations

import math

import numpy as np

from harness import FloraSpecies, PlantBody, VIEW, seg_point_dist

IDLE, TREMBLE, ERUPT, RETRACT = 0, 1, 2, 3
DEFAULTS = dict(n_walkers=12, shell=24, r_body=28.0, prism_vol=14.0, thorns=10, thorn_vol=6.0, speed=5.0,
                hunger_boost=2.0, sense=160.0, root=60.0, absorb_every=2.0, tell=110.0, prime=130.0, t_prime=0.6,
                strike=40.0, thorn=55.0, t_erupt=0.45, t_hold=0.6, t_retract=1.2, bud=1, ram=True, armour=0.0, guard=0.0)


class Walker(FloraSpecies):
    name = "walker"

    def __init__(self, arena, params=None):
        self.p = dict(DEFAULTS); self.p.update(params or {}); p = self.p
        self.rng = np.random.default_rng(int(arena.rng.integers(1 << 30)))
        self.cap = 128; self.n = 0
        self.h = np.zeros((self.cap, 3)); self.dir = np.zeros((self.cap, 3)); self.alive = np.zeros(self.cap, bool)
        self.state = np.zeros(self.cap, int); self.timer = np.zeros(self.cap); self.ext = np.zeros(self.cap)
        self.res = np.zeros(self.cap); self.absorb_t = np.zeros(self.cap); self.phase = np.zeros(self.cap)
        self.shell = [[] for _ in range(self.cap)]; self.thorn_idx = [[] for _ in range(self.cap)]
        self.thorn_dir = [None] * self.cap
        self.body = PlantBody(4096)
        self.body_cost = p["shell"] * p["prism_vol"] + p["thorns"] * p["thorn_vol"]
        self.cut_volume = 0.0; self.crystals = 0; self.deaths = 0; self.leads = []; self.fired = np.zeros(self.cap)
        self.reserve = 0.0
        self.hot_since = {}; self.seen_since = {}; self.burn_cd = {}; self.strikes = 0
        for pos in arena.grove(p["n_walkers"], 60.0):
            self._spawn(pos, self.body_cost * 1.3, grown=True)

    def _spawn(self, pos, res, grown=False):
        if self.n >= self.cap: return -1
        i = self.n; self.n += 1; p = self.p
        d = self.rng.normal(size=3); self.dir[i] = d / np.linalg.norm(d); self.h[i] = pos; self.alive[i] = True
        self.res[i] = res; self.absorb_t[i] = self.rng.uniform(0, p["absorb_every"]); self.phase[i] = self.rng.uniform(0, 6.28)
        td = self.rng.normal(size=(p["thorns"], 3)); self.thorn_dir[i] = td / np.linalg.norm(td, axis=1, keepdims=True)
        if grown:
            for _ in range(p["shell"]): self._grow_one(i, front=False)
            self._thorns(i)
        return i

    def _thorns(self, i):
        p = self.p
        while len(self.thorn_idx[i]) < p["thorns"] and self.res[i] >= p["thorn_vol"]:
            self.res[i] -= p["thorn_vol"]
            self.thorn_idx[i].append(self.body.lay(self.h[i], 3.0, p["thorn_vol"], owner=i, danger=True))

    def _grow_one(self, i, front=True):
        """Lay one shell prism: on the leading hemisphere when walking (front), anywhere when filling."""
        p = self.p
        if self.res[i] < p["prism_vol"]: return False
        d = self.rng.normal(size=3); d /= np.linalg.norm(d)
        if front and d @ self.dir[i] < 0: d = -d
        self.res[i] -= p["prism_vol"]
        self.shell[i].append(self.body.lay(self.h[i] + d * p["r_body"] * self.rng.uniform(0.7, 1.0), 5.0, p["prism_vol"], owner=i,
                                           shield=bool(self.rng.random() < p["armour"])))
        return True

    # ---- step -------------------------------------------------------------------------------------------
    def step(self, arena, dt):
        p = self.p; rng = self.rng; t = arena.t
        for i in np.flatnonzero(self.alive[:self.n]):
            # drop shell prisms the pilots broke
            self.shell[i] = [j for j in self.shell[i] if self.body.alive[j]]
            self.thorn_idx[i] = [j for j in self.thorn_idx[i] if self.body.alive[j]]
            # roots
            self.absorb_t[i] -= dt
            if self.absorb_t[i] <= 0:
                self.absorb_t[i] = p["absorb_every"] * rng.uniform(0.8, 1.2)
                f = arena.mass_near(self.h[i], p["root"] + p["r_body"])
                if len(f):
                    j = f[np.argmin(np.linalg.norm(arena.mass_pos[f] - self.h[i], axis=1))]
                    self.res[i] += arena.consume(j, "walker")
            # steer up the food gradient (trails included)
            fa = arena.mass_near(self.h[i], p["sense"])
            if len(fa):
                w = arena.mass_vol[fa][:, None] * (arena.mass_pos[fa] - self.h[i])
                g = w.sum(0); ng = np.linalg.norm(g)
                if ng > 1e-6:
                    want = g / ng
                    self.dir[i] = self.dir[i] + (want - self.dir[i]) * min(1.0, 0.8 * dt)
                    self.dir[i] /= np.linalg.norm(self.dir[i])
            hungry = self.res[i] < self.p["prism_vol"] * 3
            v = p["speed"] * (p["hunger_boost"] if hungry else 1.0)
            if self.state[i] in (ERUPT,): v = 0.0
            # treadmill: the heart moves; trailing prisms beyond the body are resorbed, leading ones laid
            new_h = self.h[i] + self.dir[i] * v * dt
            if np.linalg.norm(new_h - (arena.grove(1)[0] * 0 + np.array([0, 0, 600.0]))) < 450 - 40:
                self.h[i] = new_h
            keep = []
            for j in self.shell[i]:
                if np.linalg.norm(self.body.pos[j] - self.h[i]) > p["r_body"] * 1.25:
                    self.res[i] += self.body.resorb(j)
                else:
                    keep.append(j)
            self.shell[i] = keep
            while len(self.shell[i]) < p["shell"] and self._grow_one(i, front=True):
                pass
            self._thorns(i)
            # bud
            if p["bud"] and self.res[i] > self.body_cost * 1.2:
                d = rng.normal(size=3); d /= np.linalg.norm(d)
                j = self._spawn(self.h[i] + d * p["r_body"] * 2.5, 0.0)
                if j >= 0:
                    give = self.body_cost * 1.0; self.res[i] -= give; self.res[j] += give
        # lure + thorn state machine
        lure = self.lure_pos()
        for i in np.flatnonzero(self.alive[:self.n]):
            near = min((np.linalg.norm(pi.pos - lure[i]) for pi in arena.pilots), default=1e9)
            st = self.state[i]; self.timer[i] += dt
            if st == IDLE and near < p["prime"]:
                self.state[i] = TREMBLE; self.timer[i] = 0
            if self.state[i] == TREMBLE:
                # GUARD: a trembling thicket half-extends its thorns toward `guard` - the heart is defended during
                # the approach, so a fast dive no longer slips in under an all-or-nothing eruption (round 4)
                self.ext[i] += (p["guard"] - self.ext[i]) * min(1.0, dt / max(p["t_prime"], 1e-3))
            elif self.state[i] == IDLE:
                self.ext[i] = max(0.0, self.ext[i] - dt / p["t_retract"])
            elif st == TREMBLE:
                if near > p["prime"] * 1.3: self.state[i] = IDLE
                elif near < p["strike"] or self.timer[i] > p["t_prime"] + 3.0:
                    if self.timer[i] >= p["t_prime"] or near < p["strike"] * 0.6:
                        self.state[i] = ERUPT; self.timer[i] = 0; self.fired[i] += 1; self.strikes += 1
            elif st == ERUPT:
                self.ext[i] = max(self.ext[i], min(1.0, self.timer[i] / p["t_erupt"]))
                if self.timer[i] > p["t_erupt"] + p["t_hold"]: self.state[i] = RETRACT; self.timer[i] = 0
            elif st == RETRACT:
                self.ext[i] = max(0.0, 1.0 - self.timer[i] / p["t_retract"])
                if self.ext[i] <= 0: self.state[i] = IDLE
            # pose thorns: from the shell surface outward along their directions
            reach = p["r_body"] + self.ext[i] * p["thorn"]
            for k, j in enumerate(self.thorn_idx[i]):
                self.body.pos[j] = self.h[i] + self.thorn_dir[i][k] * (p["r_body"] * 0.9 + (reach - p["r_body"] * 0.9) * (k % 3 + 1) / 3)
        # contacts: extended thorns burn; plain shell is rammed
        for pi in arena.pilots:
            for j in self.body.contacts(pi.prev, pi.pos, pi.radius):
                o = self.body.owner[j]
                if self.body.danger[j]:
                    if o < 0 or self.ext[o] < 0.25: continue
                    if self.burn_cd.get(pi.name, -1e9) > t: continue
                    self.burn_cd[pi.name] = t + 1.0
                    t0 = self.hot_since.get((o, pi.name), self.seen_since.get((o, pi.name), t))
                    self.leads.append(t - t0); arena.hit(pi, "burn")
                elif p["ram"]:
                    self.cut_volume += self.body.take(j)
        for pi in arena.pilots:
            d = np.linalg.norm(self.h[:self.n] - pi.pos, axis=1)
            for i in np.flatnonzero(self.alive[:self.n] & (d < VIEW)):
                self.seen_since.setdefault((i, pi.name), t)
                if self.state[i] in (TREMBLE, ERUPT): self.hot_since.setdefault((i, pi.name), t)
            for i in np.flatnonzero(self.state[:self.n] == IDLE):
                self.hot_since.pop((i, pi.name), None)
            for i in np.flatnonzero(d >= VIEW):
                self.seen_since.pop((i, pi.name), None)
        bi = self.body.live(); ow = self.body.owner[bi]; ok = ow >= 0
        self.body.hot[bi] = 0.0
        self.body.hot[bi[ok]] = np.where(self.state[ow[ok]] == TREMBLE, 0.5, 0.0) + self.ext[ow[ok]]

    def lure_pos(self):
        up = self.h[:self.n] - np.array([0, 0, 600.0]); up /= np.maximum(np.linalg.norm(up, axis=1, keepdims=True), 1e-6)
        return self.h[:self.n] + up * (self.p["r_body"] * 0.4)

    def lures(self):
        live = np.flatnonzero(self.alive[:self.n])
        return self.lure_pos()[live], np.full(len(live), self.p["tell"])

    # ---- reader, threats, cutting -----------------------------------------------------------------------
    def hazards(self):
        live = np.flatnonzero(self.alive[:self.n])
        hot = live[np.isin(self.state[live], (TREMBLE, ERUPT, RETRACT))]
        R = self.p["r_body"] + np.maximum(self.ext[hot], 0.3) * self.p["thorn"]
        return self.h[hot], R, np.ones(len(hot)), None

    def threat_elements(self):
        live = np.flatnonzero(self.alive[:self.n])           # latent: a live walker erupts on what comes close
        return self.h[live], np.full(len(live), self.p["r_body"] + self.p["thorn"] * 0.6)

    def cut_targets(self):
        return self.h[np.flatnonzero(self.alive[:self.n])]

    def cut(self, arena, pilot, a, b):
        for j in self.body.contacts(a, b, 14.0):
            if self.body.danger[j]: continue
            self.cut_volume += self.body.take(j)
        live = np.flatnonzero(self.alive[:self.n])
        if len(live):
            d = seg_point_dist(a, b, self.h[live])
            for i in live[d < 12.0]:
                self.alive[i] = False; self.crystals += 1; self.deaths += 1; self.ext[i] = 0
                self.reserve += self.res[i]; self.res[i] = 0          # the unspent reserve stays as the skeleton's
                for j in self.shell[i] + self.thorn_idx[i]:
                    if self.body.alive[j]: self.body.owner[j] = -1; self.body.danger[j] = False

    def remove_ball(self, arena, c, r):
        bi = self.body.live(); m = bi[np.linalg.norm(self.body.pos[bi] - c, axis=1) < r]
        for j in m:
            self.cut_volume += self.body.take(j)

    def mass_total(self):
        return self.reserve + float(self.res[:self.n].sum()) + self.body.total_volume()

    def lane_cut(self, arena, ev):
        return None

    def signature(self):
        return np.concatenate([self.fired[:self.n], self.h[:self.n].ravel() / 100.0])

    @property
    def agent_pos(self):
        return self.h[np.flatnonzero(self.alive[:self.n])]

    @property
    def agent_vel(self):
        live = np.flatnonzero(self.alive[:self.n])
        return self.dir[live] * self.p["speed"]

    @property
    def agent_size(self):
        return np.full(int(self.alive[:self.n].sum()), self.p["r_body"])

    @property
    def intent(self):
        live = np.flatnonzero(self.alive[:self.n])
        return np.where(np.isin(self.state[live], (TREMBLE, ERUPT)), 1.0, 0.0)

    def render(self, out):
        bi = self.body.live(); hot = np.clip(self.body.hot[bi], 0, 1); dg = self.body.danger[bi]
        col = np.where(dg[:, None], np.array([[1.0, 0.2, 0.3]]), np.array([[0.3, 0.55, 0.3]]))
        col = col * (0.6 + 0.4 * hot[:, None])
        out["walker"] = dict(pos=self.body.pos[bi], col=col, size=np.where(dg, 4.0, 8.0))
        live = np.flatnonzero(self.alive[:self.n])
        out["lures"] = dict(pos=self.lure_pos()[live], col=np.tile([[0.6, 1.0, 0.3]], (len(live), 1)), size=np.full(len(live), 9.0))
