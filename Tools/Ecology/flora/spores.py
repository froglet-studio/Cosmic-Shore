"""Species F2 - SPORE BURSTER (puffball). A colony of pods, each a crystal heart -> a stalk -> a shell of prisms
holding a CHARGE of spore mass.

The colony is an EXCITABLE MEDIUM (Greenberg-Hastings / FitzHugh-Nagumo in spirit, on the pods' neighbour graph):
    u      each pod's excitation, 0..1+; visible as glow (the colony getting AGITATED is itself a telegraph)
    kick   a pilot touching a pod adds `kick_hard` if it is moving faster than `v_soft`, else `kick_soft`
           (the LIGHT TOUCH counter); a fast pass within `wake` adds a smaller pressure kick
    alarm  a bursting pod sends a pulse to every pod within `alarm_r`, arriving after d / `alarm_speed` s
           and adding `alarm_gain` - the alarm WAVE you can watch spread through a clump
    swell  u >= 1 commits the pod: it swells for `t_swell` s (the strongest telegraph), then BURSTS
    burst  the charge leaves as spores (danger prisms, `spore_vol` each) launched at `launch` u/s
    refractory is MASS-limited, not a clock: an empty pod re-arms only after its roots refill the charge
u relaxes toward 0 (`relax`/s) - a signal, not mass.

Spores drift on a divergence-free curl-noise wind with drag. A spore that touches a pod with room re-joins its
charge; a slowed spore that lands near food GERMINATES into a sprout, which absorbs food until it can afford a
pod (an animated growth crystal -> stalk -> shell). So disturbing the colony is how it SPREADS - and the cloud
you leave behind is the next colony.

Every volume moves between charge, spores, sprouts, shells and the colony reserve; food comes in only through
roots and germination. Collecting a heart drops its crystal; the pod's charge spills as spores (it bursts), the
shell stays as an inert skeleton.
"""
from __future__ import annotations

import math

import numpy as np

from harness import FloraSpecies, PlantBody, VIEW, GARDEN, seg_point_dist

IDLE, SWELL, EMPTY, GROW = 0, 1, 2, 3
DEFAULTS = dict(ram=True, n_pods=80, clumps=10, clump_r=60.0, charge_cap=60.0, spore_vol=4.0, shell_vol=10.0,
                touch=24.0, wake=70.0, v_soft=70.0, kick_hard=1.2, kick_soft=0.25, kick_wake=0.15,
                alarm_r=90.0, alarm_speed=70.0, alarm_gain=0.75, relax=0.35, t_swell=0.8, launch=45.0,
                drag=0.6, wind=12.0, wind_k=0.006, root=80.0, absorb_every=3.0, settle_v=12.0, spore_cd=0.6, spore_r=4.0, armour=0.0)
SHELL = 6


class SporeBurster(FloraSpecies):
    name = "spores"

    def __init__(self, arena, params=None):
        self.p = dict(DEFAULTS); self.p.update(params or {}); p = self.p; rng = arena.rng
        self.rng_a = np.random.default_rng(99)
        self.cap = 1024
        self.h = np.zeros((self.cap, 3)); self.ax = np.zeros((self.cap, 3)); self.u = np.zeros(self.cap)
        self.charge = np.zeros(self.cap); self.state = np.zeros(self.cap, int); self.timer = np.zeros(self.cap)
        self.alive = np.zeros(self.cap, bool); self.grow = np.zeros(self.cap); self.slots = np.full((self.cap, 2 + SHELL), -1)
        self.absorb_t = np.zeros(self.cap); self.sprout_res = np.zeros(self.cap)
        self.n = 0; self.fired = np.zeros(self.cap)
        self.body = PlantBody(4096)
        self.shell_cost = (2 + SHELL) * p["shell_vol"]
        self.pod_cost = self.shell_cost + p["charge_cap"]
        self.reserve = p["n_pods"] * self.pod_cost
        self.spos = np.zeros((0, 3)); self.svel = np.zeros((0, 3)); self.sbirth = np.zeros(0); self.svol = np.zeros(0)
        self.pulses = []                       # (t_arrive, pod, amount)
        self.cut_volume = 0.0; self.crystals = 0; self.deaths = 0; self.leads = []; self.bursts = 0
        self.burn_cd = {}; self.germinated = 0; self.rejoined = 0
        centres = arena.grove(p["clumps"], 60.0)
        for i in range(p["n_pods"]):
            c = centres[i % p["clumps"]]
            j = self._new_pod(c + rng.normal(0, p["clump_r"], 3), rng, 0.0)
            self.reserve -= self.pod_cost - self.shell_cost
            self.charge[j] = p["charge_cap"]; self._lay(j, 1.0); self.grow[j] = 1.0; self.state[j] = IDLE
        self._pose()

    # ---- pods -------------------------------------------------------------------------------------------
    def _new_pod(self, pos, rng, res):
        if self.n >= self.cap: return -1
        i = self.n; self.n += 1
        d = pos / max(np.linalg.norm(pos), 1e-6) + 0.6 * rng.normal(size=3)   # stalks lean outward-ish
        self.ax[i] = d / np.linalg.norm(d); self.h[i] = pos; self.alive[i] = True; self.state[i] = GROW
        self.sprout_res[i] = res; self.grow[i] = 0.0; self.absorb_t[i] = rng.uniform(0, self.p["absorb_every"])
        return i

    def _lay(self, i, g):
        for k in range(2 + SHELL):
            if self.slots[i, k] >= 0 or k / (2 + SHELL) > g: continue
            v = self.p["shell_vol"]
            if self.reserve >= v: self.reserve -= v
            elif self.sprout_res[i] >= v: self.sprout_res[i] -= v
            else: return
            self.slots[i, k] = self.body.lay(self.h[i], 3.5, v, owner=i, shield=bool(self.rng_a.random() < self.p["armour"]))

    def _centre(self, idx):
        return self.h[idx] + self.ax[idx] * 16.0

    def _pose(self):
        idx = np.flatnonzero(self.slots[:self.n].max(axis=1) >= 0)
        if not len(idx): return
        c = self._centre(idx); a = self.ax[idx]
        up = np.where(np.abs(a[:, 1:2]) < 0.9, np.array([[0, 1, 0]]), np.array([[1, 0, 0]]))
        n = np.cross(a, up); n /= np.linalg.norm(n, axis=1, keepdims=True); b = np.cross(a, n)
        sw = np.where(self.state[idx] == SWELL, 1.0 + 0.6 * np.clip(self.timer[idx] / self.p["t_swell"], 0, 1), 1.0)
        fill = 0.5 + 0.5 * self.charge[idx] / self.p["charge_cap"]
        rad = 7.0 * sw * fill
        S = self.slots[idx]
        for k in range(2 + SHELL):
            ok = S[:, k] >= 0
            if not ok.any(): continue
            if k < 2:
                P = self.h[idx] + a * (4.0 + 7.0 * k)
            else:
                ang = 2 * math.pi * (k - 2) / SHELL
                P = c + rad[:, None] * (math.cos(ang) * n + math.sin(ang) * b) + a * (2.0 if k % 2 else -2.0)
            self.body.pos[S[ok, k]] = P[ok]

    def plant_count(self):
        return int(self.alive[:self.n].sum())

    # ---- step -------------------------------------------------------------------------------------------
    def step(self, arena, dt):
        p = self.p; rng = arena.rng; t = arena.t; N = self.n
        live = np.flatnonzero(self.alive[:N])
        # growth (sprouts and buds) + roots
        for i in live:
            if self.state[i] == GROW:
                self.grow[i] = min(1.0, self.grow[i] + dt / 5.0); self._lay(i, self.grow[i])
                if self.grow[i] >= 1.0 and (self.slots[i] >= 0).all():
                    self.state[i] = EMPTY
            self.absorb_t[i] -= dt
            if self.absorb_t[i] <= 0:
                self.absorb_t[i] = p["absorb_every"] * rng.uniform(0.8, 1.2)
                f = arena.mass_near(self.h[i], p["root"])
                if len(f):
                    j = f[np.argmin(np.linalg.norm(arena.mass_pos[f] - self.h[i], axis=1))]
                    v = arena.consume(j, "spores")
                    if self.state[i] == GROW:
                        self.sprout_res[i] += v
                    else:
                        room = p["charge_cap"] - self.charge[i]; take = min(room, v)
                        self.charge[i] += take; self.reserve += v - take
            if self.state[i] != GROW and not (self.slots[i] >= 0).all():
                self._lay(i, 1.0)                           # repair rammed/cut shell from the reserve
            if self.state[i] == GROW and self.sprout_res[i] > 0 and (self.slots[i] >= 0).all():
                self.charge[i] += self.sprout_res[i]; self.sprout_res[i] = 0
            if self.state[i] == EMPTY and self.charge[i] >= 0.8 * p["charge_cap"]:
                self.state[i] = IDLE
        if self.reserve >= self.pod_cost and len(live):
            par = live[rng.integers(len(live))]
            d = rng.normal(size=3); d /= np.linalg.norm(d)
            j = self._new_pod(self.h[par] + d * rng.uniform(30, 60), rng, 0.0)
            if j >= 0:
                self.reserve -= p["charge_cap"]; self.sprout_res[j] = p["charge_cap"]
        # disturbance from pilots
        c = self._centre(np.arange(N))
        for pi in arena.pilots:
            spd = float(np.linalg.norm(pi.pos - pi.prev)) / dt
            d = seg_point_dist(pi.prev, pi.pos, c)
            touch = self.alive[:N] & (d < p["touch"])
            self.u[:N][touch] += (p["kick_hard"] if spd > p["v_soft"] else p["kick_soft"]) * dt / 0.1 * 0.5
            wake = self.alive[:N] & (d < p["wake"]) & ~touch & (spd > p["v_soft"])
            self.u[:N][wake] += p["kick_wake"] * dt / 0.1 * 0.5
        # alarm pulses that arrive now
        keep = []
        for (ta, j, amt) in self.pulses:
            if ta <= t:
                if self.alive[j]: self.u[j] += amt
            else:
                keep.append((ta, j, amt))
        self.pulses = keep
        # excitation dynamics
        idle = self.alive[:N] & (self.state[:N] == IDLE)
        fire = idle & (self.u[:N] >= 1.0)
        self.state[:N][fire] = SWELL; self.timer[:N][fire] = 0.0
        self.u[:N] = np.where(self.state[:N] == SWELL, self.u[:N], self.u[:N] * math.exp(-p["relax"] * dt))
        sw = self.alive[:N] & (self.state[:N] == SWELL)
        self.timer[:N][sw] += dt
        for i in np.flatnonzero(sw & (self.timer[:N] >= p["t_swell"])):
            self._burst(i, arena, rng)
        # spores: drift on curl-noise wind with drag
        if len(self.spos):
            k = p["wind_k"]; ph = 0.05 * t
            P = self.spos
            wind = -p["wind"] * np.stack([np.cos(k * P[:, 2] + ph), np.cos(k * P[:, 0] + 1.3 * ph), np.cos(k * P[:, 1] + 0.7 * ph)], 1)
            self.svel += (wind - self.svel) * (1 - math.exp(-p["drag"] * dt))
            self.spos = P + self.svel * dt
            r = np.linalg.norm(self.spos, axis=1); out = r > arena.R * 0.97
            self.spos[out] *= (arena.R * 0.97 / r[out])[:, None]
            self._settle(arena, rng)
        # spore contact burns (a cloud is a DOSE: one burn per pilot per spore_cd)
        for pi in arena.pilots:
            if not len(self.spos): break
            d = seg_point_dist(pi.prev, pi.pos, self.spos)
            hit = np.flatnonzero(d < p["spore_r"] + pi.radius)
            if len(hit) and self.burn_cd.get(pi.name, -1e9) <= t:
                self.burn_cd[pi.name] = t + p["spore_cd"]
                self.leads.append(t - float(self.sbirth[hit].min()))
                arena.hit(pi, "burn")
        self._ram(arena)
        self._pose()
        bi = self.body.live(); ow = self.body.owner[bi]; ok = ow >= 0
        self.body.hot[bi] = 0.0
        self.body.hot[bi[ok]] = np.clip(self.u[ow[ok]], 0, 1) * self.alive[ow[ok]]

    def _burst(self, i, arena, rng):
        p = self.p; self.bursts += 1; self.fired[i] += 1
        n = int(self.charge[i] // p["spore_vol"]); rem = self.charge[i] - n * p["spore_vol"]
        self.reserve += rem; self.charge[i] = 0.0
        self.state[i] = EMPTY; self.u[i] = 0.0; self.timer[i] = 0.0
        if n:
            c = self._centre(np.array([i]))[0]
            d = rng.normal(size=(n, 3)); d /= np.linalg.norm(d, axis=1, keepdims=True)
            d += self.ax[i] * 0.8
            self.spos = np.vstack([self.spos, c + d * 6.0])
            self.svel = np.vstack([self.svel, d * p["launch"] * rng.uniform(0.6, 1.2, (n, 1))])
            self.sbirth = np.append(self.sbirth, np.full(n, arena.t)); self.svol = np.append(self.svol, np.full(n, p["spore_vol"]))
        # alarm wave to neighbours
        live = np.flatnonzero(self.alive[:self.n]); live = live[live != i]
        if len(live):
            dd = np.linalg.norm(self.h[live] - self.h[i], axis=1)
            for j, dj in zip(live[dd < p["alarm_r"]], dd[dd < p["alarm_r"]]):
                self.pulses.append((arena.t + dj / p["alarm_speed"], j, p["alarm_gain"]))

    def _settle(self, arena, rng):
        p = self.p
        spd = np.linalg.norm(self.svel, axis=1)
        gone = np.zeros(len(self.spos), bool)
        # rejoin: a spore touching a pod (shell centre) with room flows back into its charge
        live = np.flatnonzero(self.alive[:self.n] & np.isin(self.state[:self.n], (IDLE, EMPTY)))
        if len(live):
            c = self._centre(live)
            for k in np.flatnonzero(spd < p["settle_v"] * 2):
                d = np.linalg.norm(c - self.spos[k], axis=1); j = int(np.argmin(d))
                if d[j] < 12.0 and self.charge[live[j]] + self.svol[k] <= p["charge_cap"]:
                    self.charge[live[j]] += self.svol[k]; gone[k] = True; self.rejoined += 1
        # germinate: slowed spore near food
        for k in np.flatnonzero((spd < p["settle_v"]) & ~gone):
            f = arena.mass_near(self.spos[k], 20.0)
            if len(f):
                j = self._new_pod(self.spos[k].copy(), rng, self.svol[k])
                if j < 0: continue
                self.sprout_res[j] += arena.consume(f[0], "spores"); gone[k] = True; self.germinated += 1
        if gone.any():
            keep = ~gone
            self.spos, self.svel, self.sbirth, self.svol = self.spos[keep], self.svel[keep], self.sbirth[keep], self.svol[keep]

    # ---- reading pilot, threats, cutting ----------------------------------------------------------------
    def hazards(self):
        N = self.n; live = np.flatnonzero(self.alive[:N])
        agitated = live[(self.u[live] > 0.3) | (self.state[live] == SWELL)]
        H = np.concatenate([self._centre(agitated), self.spos])
        R = np.concatenate([np.full(len(agitated), 40.0), np.full(len(self.spos), self.p["spore_r"] + 6.0)])
        W = np.concatenate([np.clip(self.u[agitated], 0.3, 1.0), np.full(len(self.spos), 1.0)])
        calm = live[np.isin(self.state[live], (IDLE,))]
        return H, R, W, self._centre(calm)

    def threat_elements(self):
        live = np.flatnonzero(self.alive[:self.n] & np.isin(self.state[:self.n], (IDLE, SWELL)))
        return (np.concatenate([self.spos, self._centre(live)]),
                np.concatenate([np.full(len(self.spos), self.p["spore_r"]), np.full(len(live), self.p["touch"])]))

    def cut_targets(self):
        live = np.flatnonzero(self.alive[:self.n])
        return self._centre(live)                  # the crystal is INSIDE the pod: taking it bursts it on you

    def cut(self, arena, pilot, a, b):
        c = self.body.contacts(a, b, 14.0)
        for j in c:
            o = self.body.owner[j]
            if o >= 0 and self.alive[o] and self.state[o] == IDLE and self.charge[o] > 0:
                self.u[o] += 1.0                      # cutting into a full pod sets it off
            v = self.body.take(j)
            if not v: continue
            self.cut_volume += v
            if o >= 0: self.slots[o][self.slots[o] == j] = -1
        live = np.flatnonzero(self.alive[:self.n])
        if len(live):
            d = seg_point_dist(a, b, self._centre(live))
            for i in live[d < 12.0]:
                self._die(i, arena, arena.rng)

    def _die(self, i, arena, rng):
        """Heart collected: crystal drops; the charge spills (it bursts); the shell stays as a skeleton."""
        self.crystals += 1; self.deaths += 1
        self._burst(i, arena, rng)
        self.reserve += self.sprout_res[i]; self.sprout_res[i] = 0
        self.alive[i] = False
        for j in self.slots[i]:
            if j >= 0: self.body.owner[j] = -1

    def _ram(self, arena):
        """A pilot flying through PLAIN plant prisms breaks them (an active force; mass leaves as cut mass)."""
        if not self.p["ram"]: return
        for pi in arena.pilots:
            for j in self.body.contacts(pi.prev, pi.pos, pi.radius):
                if self.body.danger[j]: continue
                v = self.body.take(j)
                if not v: continue
                self.cut_volume += v; o = self.body.owner[j]
                if o >= 0: self.slots[o][self.slots[o] == j] = -1

    def remove_ball(self, arena, c, r):
        bi = self.body.live(); m = bi[np.linalg.norm(self.body.pos[bi] - c, axis=1) < r]
        for j in m:
            v = self.body.take(j)
            if not v: continue
            self.cut_volume += v; o = self.body.owner[j]
            if o >= 0: self.slots[o][self.slots[o] == j] = -1
        if len(self.spos):
            g = np.linalg.norm(self.spos - c, axis=1) < r
            self.cut_volume += float(self.svol[g].sum()); k = ~g
            self.spos, self.svel, self.sbirth, self.svol = self.spos[k], self.svel[k], self.sbirth[k], self.svol[k]

    def mass_total(self):
        return (self.reserve + self.body.total_volume() + float(self.charge[:self.n][self.alive[:self.n]].sum())
                + float(self.sprout_res[:self.n][self.alive[:self.n]].sum()) + float(self.svol.sum()))

    def lane_cut(self, arena, ev):
        return None

    @property
    def agent_pos(self):
        return self._centre(np.flatnonzero(self.alive[:self.n]))

    @property
    def agent_vel(self):
        return np.zeros((int(self.alive[:self.n].sum()), 3))

    @property
    def agent_size(self):
        return np.full(int(self.alive[:self.n].sum()), 12.0)

    @property
    def intent(self):
        live = np.flatnonzero(self.alive[:self.n])
        return np.where(self.state[live] == SWELL, 1.0, np.clip(self.u[live], 0, 1))

    def render(self, out):
        bi = self.body.live(); hot = self.body.hot[bi]
        col = np.array([[0.75, 0.6, 0.35]]) * (1 - hot[:, None]) + np.array([[1.0, 0.9, 0.2]]) * hot[:, None]
        out["pods"] = dict(pos=self.body.pos[bi], col=col, size=np.full(len(bi), 6.0))
        out["spores"] = dict(pos=self.spos, col=np.tile([[1.0, 0.35, 0.1]], (len(self.spos), 1)), size=np.full(len(self.spos), 3.0))


SporeBurster.signature = lambda self: self.fired[:self.n]
