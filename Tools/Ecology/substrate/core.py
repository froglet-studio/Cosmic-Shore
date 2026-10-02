"""The substrate: one agents + fields + quorum + bodies simulation that every species is a PARAMETER SET of.

Nothing here knows what a "grazer" or a "pack" is. A species is a `SpeciesParams`: two `Regime`s (the
solitary and the gregarious end of ONE parameter set), drive gains, a quorum rule, and optionally a body
plan. Each step:

  1. fields   - the cell's stigmergy grids diffuse/decay and take deposits (fields.py)
  2. hash     - all live agents are hashed into cells of the neighbour radius; per-cell MOMENTS (count, sum
                pos, sum vel, sum phase) are reduced once
  3. drives   - hunger, fear, curiosity, aggression, attachment relax toward targets read from the world
                (fields, pilots, neighbour moments). Continuous, no branch.
  4. quorum   - phase (solitary 0 .. gregarious 1) relaxes toward a hysteretic sigmoid of
                density x hunger, plus contagion from neighbours' phase. Every weight below is
                lerp(solitary, gregarious, phase), so a phase change IS a behaviour change.
  5. steer    - only 1/k of agents (a rotating slice) re-steer: every drive paints interest and danger over
                D directions, danger soft-masks interest, the best direction (refined by a soft-argmax over
                its neighbours) becomes the agent's stored INTENT. The other (k-1)/k coast on their intent.
  6. integrate- every agent turns toward its intent at a bounded rate and speed (smooth by construction)
  7. world    - eat (prism volume moves into the agent's stock), bite pilots, die to pilots or starvation
                (wither: stock is re-laid as prisms), reproduce (stock splits; the child grows in from 0).

Locked laws (Tools/Ecology/PROGRAM.md §1), audited in laws.py:
  - mass is conserved: live prism volume + agent stock + laid volume is constant (no metabolism destroys
    volume - hunger is a DRIVE clock, not a mass sink);
  - no imposed death: no lifespan; an agent dies only to a pilot or to starvation;
  - continuity: agents grow in from size 0 and wither out to 0; nothing teleports (per-step displacement is
    bounded by max speed x dt).
"""
from __future__ import annotations

import time
from dataclasses import dataclass, field, replace

import numpy as np

from .fields import Fields

try:
    from . import kernels_nb as knb
    HAVE_NUMBA = knb.HAVE_NUMBA
except Exception:  # pragma: no cover
    knb, HAVE_NUMBA = None, False


# ------------------------------------------------------------------------------------------------ params
@dataclass
class Regime:
    """Weights at ONE end of the phase axis. Effective = lerp(solitary, gregarious, phase)."""
    speed: float = 60.0          # cruise u/s
    burst: float = 1.0           # speed multiplier at full urgency (fear or aggression)
    turn: float = 3.0            # rad/s
    accel: float = 120.0         # u/s^2
    w_food: float = 1.0          # follow food-scent gradient (scaled by hunger)
    w_coh: float = 0.3           # toward neighbour centroid
    w_align: float = 0.3         # match neighbour heading
    w_sep: float = 0.6           # away from crowding (danger)
    w_wander: float = 0.3        # smooth noise
    w_curious: float = 0.0       # spring to the nearest pilot's comfort ring (scaled by curiosity)
    comfort: float = 120.0       # comfort ring radius
    w_flee: float = 1.0          # pilot proximity as danger (scaled by fear)
    w_hunt: float = 0.0          # toward the pilot's predicted position (scaled by aggression)
    w_ring: float = 0.0          # encircle: toward a slot on a ring around the pilot (scaled by aggression)
    ring_r: float = 90.0
    w_trail: float = 0.0         # follow own species' trail gradient
    w_alarm: float = 0.5         # alarm field gradient as danger
    w_threat: float = 0.5        # threat field gradient as danger
    w_home: float = 0.0          # toward the agent's own home (where it was born/spawned)
    trample: float = 0.0         # >0.5: contact harms a pilot when moving fast (a stampede), regardless of aggression
    size: float = 3.0
    color: tuple = (0.5, 0.9, 1.0)


@dataclass
class BodyPlan:
    """A creature the agents can assemble into: slot offsets in the body frame (+z forward) and the role
    each slot accepts. Agent i is assigned slot i % K (fixed - no per-step assignment)."""
    slots: np.ndarray                  # (K,3)
    scale: float = 1.0
    well: float = 4.0                  # flat-bottom radius: no pull inside it
    speed: float = 50.0                # the assembled body cruises at this


@dataclass
class SpeciesParams:
    name: str = "species"
    n0: int = 200
    capacity: int = 0                  # pool size (0 = 2 x n0)
    solitary: Regime = field(default_factory=Regime)
    gregarious: Regime = field(default_factory=Regime)
    nbr_r: float = 30.0                # neighbour hash cell (= interaction radius)
    dens_norm: float = 6.0             # neighbour count that reads as "crowded" (density 1)
    # drives
    metabolism: float = 0.01           # hunger per second (a DRIVE clock - no mass is destroyed)
    eat_r: float = 8.0
    eat_hunger: float = 0.25           # only eat above this hunger
    hunger_per_vol: float = 0.02       # hunger removed per unit volume eaten
    fear_gain: float = 1.0
    fear_decay: float = 0.5            # per second
    sense: float = 250.0               # pilot perception radius
    curiosity_rate: float = 0.3
    attach_rate: float = 0.5
    attach_on_h: float = 0.3           # body quorum: assemble when the group's mean hunger falls below this
    attach_off_h: float = 0.55         # ... and dissolve when it rises above this (hysteresis)
    attach_on_f: float = 0.35          # ... or assemble when the group's mean fear rises above this
    # quorum (locust): s = density * hunger; hysteresis
    q_up: float = 9.0                  # s above this -> gregarious (9 = never)
    q_down: float = 9.0
    q_width: float = 0.05
    q_rate: float = 0.4                # phase relaxation per second
    q_contagion: float = 0.5           # weight of neighbours' mean phase in the target
    q_w_dens: float = 1.0              # quorum signal weights (see _quorum)
    q_w_prox: float = 0.0
    q_w_alarm: float = 0.0
    q_hunger: float = 1.0              # hunger exponent gating the signal (0 = hunger-independent)
    # world
    bite_r: float = 10.0
    bite_cool: float = 1.5
    starve_s: float = 30.0             # seconds at hunger 1 before withering
    birth_stock: float = 60.0          # stock needed to split
    stock0: float = 25.0               # body volume a spawned seed starts with (re-laid as prisms if it dies)
    grow_s: float = 2.0                # grow-in / wither-out duration
    deposit_trail: float = 0.0
    deposit_threat: float = 0.0        # predators make the cell feel dangerous to others
    deposit_alarm: float = 0.5         # scaled by fear
    n_dirs: int = 18
    frac_k: int = 4
    attn_r: float = 0.0                # attention LOD: within this distance of a pilot, re-steer every step
    attn_urg: float = 1.0              # ... or when max(fear, aggression) exceeds this (1 = off)
    ring_roles: int = 0                # pack: number of ring slots (role = i % ring_roles)
    body: BodyPlan | None = None
    seed: int = 0


def fib_dirs(n):
    i = np.arange(n) + 0.5
    phi = np.arccos(1 - 2 * i / n); th = np.pi * (1 + 5 ** 0.5) * i
    return np.stack([np.cos(th) * np.sin(phi), np.sin(th) * np.sin(phi), np.cos(phi)], 1).astype(np.float64)


def _unit(v):
    n = np.linalg.norm(v, axis=-1, keepdims=True)
    return v / np.maximum(n, 1e-9), n[..., 0]


def _lerp(a, b, t):
    return a + (b - a) * t


# ---------------------------------------------------------------------------------------------- substrate
class Substrate:
    """A population of ONE species. Exposes the scorecard interface (agent_pos/vel/size, intent, kills,
    crystals) and the arena species interface (step, render)."""

    TERMS = ("food", "coh", "align", "wander", "curious", "hunt", "ring", "trail", "home", "body", "inward")
    DANGERS = ("sep", "flee", "alarm", "threat", "wall")

    def __init__(self, arena, P: SpeciesParams, backend="numpy", center=None, spread=60.0, G=40, init_hunger=None,
                 anchor=None):
        self.P, self.backend = P, backend
        if backend in ("numba", "fused") and not HAVE_NUMBA:
            raise RuntimeError("numba backend requested but numba missing")
        self.rng = np.random.default_rng(P.seed * 7919 + 13)
        cap = P.capacity or 2 * P.n0
        self.cap = cap
        self.R = arena.R
        z = lambda *s: np.zeros((cap,) + s)
        self.pos, self.vel = z(3), z(3)
        self.idir, self.ispeed = z(3), z()
        self.hunger, self.fear, self.curious, self.aggr, self.attach, self.phase = (z() for _ in range(6))
        self.stock, self.grow, self.starve, self.cool, self.wseed = z(), z(), z(), z(), z(3)
        self.alive = np.zeros(cap, bool)     # in the pool (includes withering)
        self.dying = np.zeros(cap, bool)
        self.qtarget = np.zeros(cap)
        self.role = np.arange(cap) % max(P.ring_roles, 1)
        self.slot = np.arange(cap) % (len(P.body.slots) if P.body else 1)
        self.dirs = fib_dirs(P.n_dirs)
        self.fields = Fields.of(arena, G, backend)
        self.trail_ch = f"trail:{P.name}"
        self.fields.add(self.trail_ch, 0.15, 0.97)
        self.home = None
        self.kills = 0; self.crystals = 0; self.births = 0; self.starved = 0; self.laid = 0.0
        self.hits = 0
        self.tick = 0
        self.publish = True
        self._last_hit = {}
        self.assembling = False
        self.death_log = []
        self._freed_tick = np.full(cap, -1)
        self._steered = np.zeros(cap, bool)
        self._dt = 0.1
        self.arena_pilots = []
        self.timers: dict[str, float] = {}
        self.body_c = None; self.body_f = np.array([0, 0, 1.0]); self.body_v = np.zeros(3)
        c = arena._ball(1, 0.3 * arena.R, 0.7 * arena.R)[0] if center is None else np.asarray(center, float)
        n = P.n0
        self.pos[:n] = c + self.rng.normal(0, spread, (n, 3))
        if anchor == "mass" and arena.mass_alive.any():
            # each agent's home is a live prism (an ambusher waits IN the flora, not in open water)
            live = np.flatnonzero(arena.mass_alive)
            self.pos[:n] = arena.mass_pos[self.rng.choice(live, n)] + self.rng.normal(0, 6, (n, 3))
        d = self.rng.normal(size=(n, 3)); self.idir[:n] = _unit(d)[0]
        self.vel[:n] = self.idir[:n] * P.solitary.speed * 0.5
        self.ispeed[:n] = P.solitary.speed
        self.alive[:n] = True; self.grow[:n] = 1.0
        self.hunger[:n] = self.rng.uniform(0.1, 0.4, n) if init_hunger is None else \
            np.clip(init_hunger + self.rng.normal(0, 0.03, n), 0, 1)
        self.stock[:n] = P.stock0          # a spawned seed's body (a spawner is a seeder, as in the game)
        self.wseed[:n] = self.rng.uniform(0, 100, (n, 3))
        self.home = c.copy()
        self.homes = self.pos.copy()           # per-agent territory anchor (a child inherits its parent's)
        self._expose()

    # ---- scorecard / arena views -------------------------------------------------------------------
    def _expose(self):
        a = np.flatnonzero(self.alive & ~self.dying)
        self._live = a
        self.agent_pos = self.pos[a]; self.agent_vel = self.vel[a]
        self.agent_size = self._sizes()[a]
        self.intent = self._intent()[a]

    def _sizes(self):
        r = _lerp(self.P.solitary.size, self.P.gregarious.size, self.phase)
        return r * np.clip(self.grow, 0, 1)

    def _intent(self):
        # the telegraph: aggression for hunters/locusts, scaled by closeness of the ring quorum for packs
        return np.clip(self.aggr * (0.4 + 0.6 * self.phase), 0, 1)

    def render(self, out):
        a = np.flatnonzero(self.alive)
        S, G = self.P.solitary, self.P.gregarious
        col = _lerp(np.array(S.color), np.array(G.color), self.phase[a, None])
        col = _lerp(col, np.array([1.0, 0.25, 0.2]), np.clip(self._intent()[a, None] - 0.5, 0, 0.5) * 1.6)
        out[self.P.name] = dict(pos=self.pos[a], col=col, size=self._sizes()[a] * 2.0)

    def mass_held(self):
        return float(self.stock[self.alive].sum())

    # ---- step -------------------------------------------------------------------------------------
    def _t(self, k, t0):
        t1 = time.perf_counter(); self.timers[k] = self.timers.get(k, 0.0) + t1 - t0; return t1

    def step(self, arena, dt):
        P = self.P
        self._arena = arena
        t0 = time.perf_counter()
        F = self.fields
        F.update(arena)
        t0 = self._t("fields", t0)
        A = np.flatnonzero(self.alive & ~self.dying)
        self.tick += 1
        if len(A) == 0:
            self._world(arena, dt, A); self._expose(); return
        if self.backend == "fused" and P.body is None:
            self._fused(arena, dt, A)
            t0 = time.perf_counter()
            self._world(arena, dt, A)
            t0 = self._t("world", t0)
            self._expose()
            if self.publish:
                arena.targets = list(self.agent_pos); arena.threats = list(self.agent_pos)
            return
        pos = self.pos[A]
        cells = F.cell(pos)
        # ---- drives (all agents, elementwise) ----
        self._drives(arena, dt, A, cells, None)
        t0 = self._t("drives", t0)
        # ---- the re-steering slice: a rotating 1/k, PLUS every agent that is near a pilot or urgent
        # (attention LOD: calm agents coast on stale intent, engaged ones re-steer every step) ----
        k = max(1, P.frac_k)
        rot = ((A + self.tick) % k == 0) if k > 1 else np.ones(len(A), bool)
        if P.attn_r > 0:
            rot |= self._pd < P.attn_r
        if P.attn_urg < 1:
            rot |= np.maximum(self.fear[A], self.aggr[A]) > P.attn_urg
        S = A[rot]
        sl = np.flatnonzero(rot)                         # S's rows inside A
        self._S = S
        nb = self._neighbours(A, sl)
        t0 = self._t("neighbours", t0)
        # ---- steer the slice ----
        if len(S):
            self._steer(arena, S, sl, cells[sl], nb)
        t0 = self._t("steer", t0)
        # ---- integrate all ----
        self._integrate(dt, A)
        t0 = self._t("integrate", t0)
        # ---- deposits ----
        if P.deposit_trail:
            F.deposit(self.trail_ch, self.pos[A], P.deposit_trail * dt)
        if P.deposit_alarm:
            m = self.fear[A] > 0.3
            F.deposit("alarm", self.pos[A][m], P.deposit_alarm * self.fear[A][m] * dt)
        if P.deposit_threat:
            F.deposit("threat", self.pos[A], P.deposit_threat * dt)
        t0 = self._t("deposit", t0)
        self._world(arena, dt, A)
        t0 = self._t("world", t0)
        self._expose()
        if self.publish:
            arena.targets = list(self.agent_pos); arena.threats = list(self.agent_pos)

    # ---- neighbours: per-cell moments, read for the slice ----
    def _neighbours(self, A, sl):
        P = self.P
        pos, vel, ph = self.pos[A], self.vel[A], self.phase[A]
        if self.backend in ("numba", "fused"):
            nb = knb.neighbours(pos, vel, ph, sl.astype(np.int64), P.nbr_r, self.R)
            nb["sep"] = np.where(nb["_has"][:, None], (pos[sl] - nb["_cen"]) / P.nbr_r, 0.0) * \
                (nb["count"] / P.dens_norm)[:, None]
            return nb
        h = P.nbr_r
        M = int(2 * self.R / h) + 4
        c = np.clip(np.floor((pos + self.R) / h).astype(np.int64) + 1, 0, M - 1)
        key = (c[:, 0] * M + c[:, 1]) * M + c[:, 2]
        uq, inv = np.unique(key, return_inverse=True)
        nU = len(uq)
        cnt = np.bincount(inv, minlength=nU).astype(np.float64)
        sp = np.stack([np.bincount(inv, pos[:, j], nU) for j in range(3)], 1)
        sv = np.stack([np.bincount(inv, vel[:, j], nU) for j in range(3)], 1)
        sph = np.bincount(inv, ph, nU)
        m = len(sl)
        n_c = np.zeros(m); n_p = np.zeros((m, 3)); n_v = np.zeros((m, 3)); n_ph = np.zeros(m)
        ks = key[sl]
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    q = ks + (dx * M + dy) * M + dz
                    j = np.searchsorted(uq, q); j = np.minimum(j, nU - 1)
                    ok = uq[j] == q
                    jj = j[ok]
                    n_c[ok] += cnt[jj]; n_p[ok] += sp[jj]; n_v[ok] += sv[jj]; n_ph[ok] += sph[jj]
        # remove self
        n_c -= 1; n_p -= pos[sl]; n_v -= vel[sl]; n_ph -= ph[sl]
        inv_c = 1.0 / np.maximum(n_c, 1)
        cen = n_p * inv_c[:, None]
        has = n_c > 0
        coh = np.where(has[:, None], cen - pos[sl], 0.0)
        align = np.where(has[:, None], n_v * inv_c[:, None], 0.0)
        # separation from moments: away from the neighbour centroid, stronger when crowded
        sep = np.where(has[:, None], (pos[sl] - cen) / h, 0.0) * (n_c / P.dens_norm)[:, None]
        mph = np.where(has, n_ph * inv_c, ph[sl])
        return dict(count=n_c, coh=coh, align=align, sep=sep, mphase=mph)

    def _nearest_pilot(self, arena, p):
        if not arena.pilots:
            return None, np.full(len(p), 1e9), None
        PP = np.stack([q.pos for q in arena.pilots]); PV = np.stack([q.vel for q in arena.pilots])
        d = np.linalg.norm(p[:, None, :] - PP[None], axis=2)
        j = np.argmin(d, axis=1)
        return PP[j], d[np.arange(len(p)), j], PV[j]

    def _drives(self, arena, dt, A, cells, nb):
        P, F = self.P, self.fields
        h = self.hunger
        h[A] = np.minimum(1.0, h[A] + P.metabolism * dt)
        pp, pd, _ = self._nearest_pilot(arena, self.pos[A])
        self._pd = pd
        prox = np.clip(1 - pd / P.sense, 0, 1)
        threat = F.sample("threat", cells); alarm = F.sample("alarm", cells)
        f = self.fear[A]
        f += dt * (P.fear_gain * (prox ** 2 + 0.5 * np.minimum(threat, 2) + 1.0 * np.minimum(alarm, 2))) - dt * P.fear_decay * f
        self.fear[A] = np.clip(f, 0, 1)
        calm = (1 - self.fear[A]) * (1 - h[A])
        self.curious[A] += dt * P.curiosity_rate * (calm * (1 - self.phase[A]) - self.curious[A])
        # aggression = hunger, gated by how much this phase can DO with it (hunt/ring weights): a grazer's
        # hunger is never aggression, a solitary locust's is not either, a gregarious one's is
        ph = self.phase[A]
        cap = np.minimum(1.0, _lerp(P.solitary.w_hunt + P.solitary.w_ring, P.gregarious.w_hunt + P.gregarious.w_ring, ph))
        self.aggr[A] = np.clip(h[A] * 1.4 - 0.3, 0, 1) * cap
        # attachment (assembly into the body): sated and/or collectively afraid
        # a GROUP quorum with hysteresis on the school's mean hunger and fear: individual satiety is out of
        # phase across members, so an individual rule never reaches a body (measured: assembled <= 22%)
        if P.body is not None:
            gh, gf = float(np.mean(h[A])), float(np.mean(self.fear[A]))
            if not self.assembling and (gh < P.attach_on_h or gf > P.attach_on_f):
                self.assembling = True
            elif self.assembling and gh > P.attach_off_h and gf < 0.5 * P.attach_on_f:
                self.assembling = False
            tgt = 1.0 if self.assembling else 0.0
            self.attach[A] += dt * P.attach_rate * (tgt - self.attach[A])
        # quorum: phase relaxes toward a hysteretic sigmoid of density x hunger (+ contagion), for the
        # slice whose neighbour counts we have (others hold their target)

    def _quorum(self, dt, S, nb):
        P = self.P
        if P.q_up >= 9:
            return
        # ONE quorum signal for every species: a weighted sum of local density, pilot proximity and alarm,
        # gated by hunger. Locusts and packs read density; an ambusher reads proximity; a herd reads alarm.
        dens = nb["count"] / P.dens_norm
        sig = P.q_w_dens * dens
        if P.q_w_prox:
            _, pd, _ = self._nearest_pilot(self._arena, self.pos[S])
            sig = sig + P.q_w_prox * np.clip(1 - pd / P.sense, 0, 1)
        if P.q_w_alarm:
            sig = sig + P.q_w_alarm * np.minimum(self.fields.sample("alarm", self.fields.cell(self.pos[S])), 2)
        s = sig * self.hunger[S] ** P.q_hunger
        theta = np.where(self.qtarget[S] > 0.5, P.q_down, P.q_up)
        tgt = 1 / (1 + np.exp(-(s - theta) / P.q_width))
        tgt = (1 - P.q_contagion) * tgt + P.q_contagion * np.maximum(tgt, nb["mphase"]) * (nb["count"] > 0) + \
              P.q_contagion * tgt * (nb["count"] == 0)
        self.qtarget[S] = tgt

    def _steer(self, arena, S, sl, cells, nb):
        P, F = self.P, self.fields
        ph = self.phase[S]
        Rs, Rg = P.solitary, P.gregarious
        W = lambda name: _lerp(getattr(Rs, name), getattr(Rg, name), ph)
        p = self.pos[S]; m = len(S)
        hu, fe, cu, ag = self.hunger[S], self.fear[S], self.curious[S], self.aggr[S]
        self._quorum(0, S, nb)
        pp, pd, pv = self._nearest_pilot(arena, p)
        T = np.zeros((m, len(self.TERMS), 3)); Wt = np.zeros((m, len(self.TERMS)))
        D = np.zeros((m, len(self.DANGERS), 3)); Wd = np.zeros((m, len(self.DANGERS)))
        ti = {k: i for i, k in enumerate(self.TERMS)}; di = {k: i for i, k in enumerate(self.DANGERS)}

        def term(k, v, w):
            u, n = _unit(v); T[:, ti[k]] = u; Wt[:, ti[k]] = w * (n > 1e-9)

        def danger(k, v, w):
            u, n = _unit(v); D[:, di[k]] = u; Wd[:, di[k]] = w * (n > 1e-9)

        term("food", F.sample_grad("food", cells), W("w_food") * hu)
        term("coh", nb["coh"], W("w_coh") * (1 + self.attach[S]))
        term("align", nb["align"], W("w_align"))
        tt = self.tick * 0.05
        wv = np.sin(self.wseed[S] + tt * np.array([1.0, 1.3, 0.7])) + 0.6 * np.sin(1.7 * self.wseed[S][:, ::-1] + tt * 2.1)
        term("wander", wv, W("w_wander"))
        if pp is not None:
            to_p = pp - p
            # curiosity: a spring to the comfort ring (approach outside it, retreat inside) - continuous
            spring = np.clip((pd - W("comfort")) / np.maximum(W("comfort"), 1), -1, 1)
            near = pd < P.sense * 1.5
            term("curious", to_p * spring[:, None], W("w_curious") * cu * np.abs(spring) * near)
            lead = pp + pv * np.clip(pd / 150.0, 0, 2.0)[:, None]
            term("hunt", lead - p, W("w_hunt") * ag * near)
            if P.ring_roles:
                # ring slots around the pilot in the plane normal to its velocity; role picks the slot
                fwd = _unit(pv)[0]
                a = np.cross(fwd, np.array([0.0, 1.0, 0.0])); a = _unit(a + 1e-6)[0]
                b = np.cross(fwd, a)
                ang = 2 * np.pi * self.role[S] / P.ring_roles
                # the ring sits slightly AHEAD of the pilot: a cut-off, not a tail chase
                slot = pp + fwd * 40.0 + W("ring_r")[:, None] * (np.cos(ang)[:, None] * a + np.sin(ang)[:, None] * b)
                term("ring", slot - p, W("w_ring") * ag * near)
            danger("flee", p - pp, W("w_flee") * fe * np.clip(1 - pd / P.sense, 0, 1))
        term("trail", F.sample_grad(self.trail_ch, cells), W("w_trail"))
        term("home", self.homes[S] - p, W("w_home") * (1 - hu))
        if P.body is not None and self.body_c is not None:
            # the member's desired VELOCITY is its slot's own velocity plus a closing speed along the
            # displacement (flat-bottom well: zero inside `well`), bounded by the turn radius
            # (v <= turn * distance) or members orbit their slot. Steering takes its direction, the speed
            # stage below takes its magnitude, so heading and speed always agree.
            sw = self._slot_world(S)
            d = sw - p; dn = np.linalg.norm(d, axis=1)
            sv = self.slot_v[self.slot[S]] if hasattr(self, "slot_v") else np.tile(self.body_v, (len(S), 1))
            close = np.minimum(np.minimum(np.clip(dn - P.body.well, 0, None) * 2.0, 0.5 * W("turn") * dn), 150.0)
            vstar = sv + d / np.maximum(dn, 1e-6)[:, None] * close[:, None]
            self._vstar = vstar
            term("body", vstar, 4.0 * self.attach[S])
        r = np.linalg.norm(p, axis=1)
        term("inward", -p, np.clip((r - 0.8 * self.R) / (0.15 * self.R), 0, 1) * 3.0)
        danger("wall", p, np.clip((r - 0.85 * self.R) / (0.1 * self.R), 0, 1) * 3.0)
        # an assembled member accepts crowding (its slot does the spacing)
        danger("sep", nb["sep"], W("w_sep") * np.minimum(np.linalg.norm(nb["sep"], axis=1), 2.0) * (1 - self.attach[S]))
        danger("alarm", -F.sample_grad("alarm", cells), W("w_alarm") * (0.3 + fe))
        danger("threat", -F.sample_grad("threat", cells), W("w_threat") * (0.3 + fe))
        if P.body is not None:
            # an assembled member hands its own drives to the body (the body is the agent now): mute them
            mute = (1 - 0.9 * self.attach[S])[:, None]
            keep = np.array([k in ("body", "inward") for k in self.TERMS])
            Wt[:, ~keep] *= mute
            Wd[:, [di["sep"], di["flee"], di["alarm"], di["threat"]]] *= mute
        # ---- context map ----
        t1 = time.perf_counter()
        if self.backend in ("numba", "fused"):
            best = knb.context_choose(T, Wt, D, Wd, self.dirs, self.idir[S])
        else:
            best = self._context_np(T, Wt, D, Wd, self.idir[S])
        self.timers["context"] = self.timers.get("context", 0) + time.perf_counter() - t1
        self.idir[S] = best
        urg = np.maximum(fe * (W("w_flee") > 0), ag)
        sp = W("speed") * (1 + (W("burst") - 1) * urg)
        if P.body is not None and self.body_c is not None:
            # assembled members catch up with their slot, then match the body's pace
            sp = _lerp(sp, np.linalg.norm(self._vstar, axis=1), self.attach[S])
        self.ispeed[S] = sp

    def _context_np(self, T, Wt, D, Wd, cur):
        dirs = self.dirs
        I = np.einsum("mtc,dc->mtd", T, dirs)
        I = (np.maximum(I, 0) * Wt[:, :, None]).sum(1)
        Dg = np.einsum("mtc,dc->mtd", D, dirs)
        Dg = (np.maximum(Dg, 0) ** 2 * Wd[:, :, None]).sum(1)
        # momentum: a little interest along the current heading (hysteresis, prevents dithering)
        I += 0.15 * np.maximum(cur @ dirs.T, 0)
        Ie = I * (1 - np.clip(Dg, 0, 1)) - 0.25 * np.maximum(Dg - 1, 0)
        mx = Ie.max(1, keepdims=True)
        w = np.maximum(Ie - 0.75 * mx, 0) ** 2
        w = np.where(mx > 1e-6, w, 0)
        v = w @ dirs
        u, n = _unit(v)
        return np.where((n > 1e-9)[:, None], u, cur)

    def _integrate(self, dt, A):
        P = self.P
        ph = self.phase[A]
        turn = _lerp(P.solitary.turn, P.gregarious.turn, ph)
        acc = _lerp(P.solitary.accel, P.gregarious.accel, ph)
        v = self.vel[A]; vh, vs = _unit(v)
        vh = np.where((vs > 1e-6)[:, None], vh, self.idir[A])
        t = self.idir[A]
        c = np.clip(np.sum(vh * t, 1), -1, 1); ang = np.arccos(c)
        k = np.minimum(1.0, turn * dt / np.maximum(ang, 1e-6))
        nd = _unit(vh + (t - vh) * k[:, None])[0]
        ns = vs + np.clip(self.ispeed[A] - vs, -acc * dt, acc * dt)
        self.vel[A] = nd * ns[:, None]
        self.pos[A] = self.pos[A] + self.vel[A] * dt
        r = np.linalg.norm(self.pos[A], axis=1)
        out = r > 0.98 * self.R
        if out.any():
            ii = A[out]; self.pos[ii] *= (0.98 * self.R / r[out])[:, None]
        # phase relaxes every step toward the last computed quorum target
        self.phase[A] += dt * self.P.q_rate * (self.qtarget[A] - ph)
        self._dt = dt

    # ---- fused (Burst-shaped) path ----
    def _fused(self, arena, dt, A):
        P, F = self.P, self.fields
        t0 = time.perf_counter()
        if not hasattr(self, "_Rs"):
            self._Rs = np.array([float(getattr(P.solitary, f)) for f in knb.REG_FIELDS])
            self._Rg = np.array([float(getattr(P.gregarious, f)) for f in knb.REG_FIELDS])
            self._SPv = np.array([float(getattr(P, f)) for f in knb.SP_FIELDS])
            self._role = self.role.astype(np.int64)
        PP = np.stack([q.pos for q in arena.pilots]) if arena.pilots else np.zeros((0, 3))
        PV = np.stack([q.vel for q in arena.pilots]) if arena.pilots else np.zeros((0, 3))
        G = F.G
        knb.fused_step(A.astype(np.int64), self.pos, self.vel, self.idir, self.ispeed, self.hunger, self.fear,
                       self.curious, self.aggr, self.attach, self.phase, self.qtarget, self.wseed, self._role,
                       self._Rs, self._Rg, self._SPv, self.dirs, PP, PV, float(F.R), G,
                       F.grad["food"].reshape(-1, 3), F.grad[self.trail_ch].reshape(-1, 3),
                       F.ch["alarm"].reshape(-1), F.grad["alarm"].reshape(-1, 3),
                       F.ch["threat"].reshape(-1), F.grad["threat"].reshape(-1, 3),
                       self.homes, True,
                       self.tick, max(1, P.frac_k), dt, float(self.R), float(P.attn_r), float(P.attn_urg),
                       self._steered)
        self._S = A[self._steered[A]]
        t0 = self._t("fused", t0)
        if P.deposit_trail:
            F.deposit(self.trail_ch, self.pos[A], P.deposit_trail * dt)
        if P.deposit_alarm:
            m = self.fear[A] > 0.3
            F.deposit("alarm", self.pos[A][m], P.deposit_alarm * self.fear[A][m] * dt)
        if P.deposit_threat:
            F.deposit("threat", self.pos[A], P.deposit_threat * dt)
        self._t("deposit", t0)

    # ---- body frame ----
    def _update_body(self, A):
        P = self.P
        if P.body is None:
            return
        att = A[self.attach[A] > 0.5]
        if len(att) == 0:
            self.body_c = None
            return
        # the body owns its position: it flies at body_v and is only gently tied to its members' centroid
        # (tying it hard makes it lag its own members, and that feedback spirals the shape apart)
        cm = self.pos[att].mean(0) if len(att) else self.body_c
        if self.body_c is None or len(att) < max(4, len(P.body.slots) // 3):
            self.body_c = cm
        else:
            self.body_c = self.body_c + self.body_v * self._dt + 0.05 * (cm - self.body_c)
        c = self.body_c
        # the BODY is an agent one level up: its heading is steered by the school's summed drives at the
        # body centre (food scent when hungry, away from the nearest pilot when afraid, off the wall), turned
        # at a slow rate; members feed its velocity forward
        F = self.fields
        cell = F.cell(c[None])
        hu = float(self.hunger[A].mean()); fe = float(self.fear[A].mean())
        want = F.sample_grad("food", cell)[0] * 50 * hu
        if self.arena_pilots:
            pp = min(self.arena_pilots, key=lambda q: np.linalg.norm(q - c))
            want = want + _unit(c - pp)[0] * 2.0 * fe
        r = np.linalg.norm(c)
        want = want - c / max(r, 1) * np.clip((r - 0.6 * self.R) / (0.2 * self.R), 0, 2)
        tt = self.tick * 0.02
        want = want + 0.3 * np.array([np.sin(tt), np.sin(1.3 * tt + 1), np.sin(0.7 * tt + 2)])
        self._f_prev = self.body_f.copy()
        u, n = _unit(want)
        if n > 1e-6:
            ang = np.arccos(np.clip(self.body_f @ u, -1, 1))
            k = min(1.0, 0.35 * self._dt / max(ang, 1e-6))
            self.body_f = _unit(self.body_f + (u - self.body_f) * k)[0]
        spd = P.body.speed * (1 + 1.5 * fe)
        self.body_v = 0.9 * self.body_v + 0.1 * self.body_f * spd
        # every slot's own world velocity = body_v + omega x r (rigid body), omega from the heading's turn this
        # step. (A finite difference of slot positions was tried first: the centroid re-seat during formation
        # made it jump, and members were flung at the 300 u/s clip.)
        om = np.cross(self._f_prev, self.body_f) / self._dt
        r = self._slot_world_k(np.arange(len(P.body.slots))) - self.body_c
        self.slot_v = self.body_v + np.cross(om, r)

    def _slot_world(self, S):
        return self._slot_world_k(self.slot[S])

    def _slot_world_k(self, ks):
        f = self.body_f
        a = _unit(np.cross(np.array([0.0, 1.0, 0.0]), f) + 1e-6)[0]
        b = np.cross(f, a)
        off = self.P.body.slots[ks] * self.P.body.scale
        return self.body_c + off[:, 0:1] * a + off[:, 1:2] * b + off[:, 2:3] * f

    # ---- world: eat, bite, die, reproduce, grow ----
    def _world(self, arena, dt, A):
        P = self.P
        self.arena_pilots = [q.pos for q in arena.pilots]
        self._update_body(A)
        # grow in / wither out
        self.grow[self.alive & ~self.dying] = np.minimum(1.0, self.grow[self.alive & ~self.dying] + dt / P.grow_s)
        wd = np.flatnonzero(self.dying)
        if len(wd):
            self.grow[wd] -= dt / P.grow_s
            gone = wd[self.grow[wd] <= 0]
            self.alive[gone] = False; self.dying[gone] = False; self.grow[gone] = 0
            self.vel[gone] = 0
            self._freed_tick[gone] = self.tick
        if len(A) == 0:
            return
        # eat: hungry agents take a whole prism within eat_r (volume -> stock)
        # eating is checked by the re-steering slice only (fractional, like steering)
        Sx = getattr(self, "_S", A)
        Sx = Sx[self.alive[Sx] & ~self.dying[Sx]]
        hungry = Sx[self.hunger[Sx] > P.eat_hunger]
        if len(hungry) and arena.mass_alive.any():
            self._eat(arena, hungry)
        # pilots: bites (aggressive agents) and kills (a hunting pilot rams an agent)
        if arena.pilots:
            for pl in arena.pilots:
                d = np.linalg.norm(self.pos[A] - pl.pos, axis=1)
                self.cool[A] -= dt / max(len(arena.pilots), 1)
                tr = _lerp(P.solitary.trample, P.gregarious.trample, self.phase[A])
                fast = np.linalg.norm(self.vel[A], axis=1) > 60
                harm = (self.aggr[A] > 0.5) | ((tr > 0.5) & fast)
                b = A[(d < P.bite_r + pl.radius) & harm & (self.cool[A] <= 0)]
                # one harm EVENT per pilot per bite_cool (a swarm nibbles, it does not machine-gun); the event's
                # amount is how many agents were in contact
                key = id(pl)
                if len(b) and arena.t - self._last_hit.get(key, -1e9) >= P.bite_cool:
                    arena.hit(pl, "bite", float(len(b))); self.hits += 1
                    self._last_hit[key] = arena.t
                    self.cool[b] = P.bite_cool
                if pl.policy == "hunter":
                    kd = A[d < pl.radius + self._sizes()[A] + 2]
                    for i in kd:
                        if not self.dying[i]:
                            self._die(arena, i, "pilot"); self.kills += 1
        # starvation (no imposed death: only hunger maxed for starve_s)
        st = A[self.hunger[A] >= 1.0]
        self.starve[A] = np.where(self.hunger[A] >= 1.0, self.starve[A] + dt, 0)
        for i in st[self.starve[st] > P.starve_s]:
            if not self.dying[i]:
                self._die(arena, i, "starvation"); self.starved += 1
        # reproduce: stock splits, child grows in at the parent (production gated by free pool slots)
        par = A[(self.stock[A] >= P.birth_stock) & ~self.dying[A] & (self.grow[A] >= 1)]
        if len(par):
            # never reuse a slot freed THIS tick: an index must not change identity inside one tick (a GPU
            # interpolating last->this frame would draw a streak from the dead agent to the newborn)
            free = np.flatnonzero(~self.alive & (self._freed_tick != self.tick))
            nb = min(len(par), len(free))
            for i, j in zip(par[:nb], free[:nb]):
                half = self.stock[i] * 0.5
                self.stock[i] -= half; self.stock[j] = half
                self.pos[j] = self.pos[i]; self.vel[j] = self.vel[i] * 0.5; self.homes[j] = self.homes[i]
                self.idir[j] = self.idir[i]; self.ispeed[j] = self.ispeed[i]
                for arr in (self.hunger, self.fear, self.curious, self.aggr, self.attach, self.phase, self.qtarget):
                    arr[j] = arr[i]
                self.starve[j] = 0; self.cool[j] = 0; self.grow[j] = 0.0
                self.wseed[j] = self.rng.uniform(0, 100, 3)
                self.alive[j] = True; self.dying[j] = False
                self.births += 1

    def _eat(self, arena, hungry):
        P = self.P
        live = np.flatnonzero(arena.mass_alive & ~arena.mass_shielded)
        if len(live) == 0:
            return
        if self.backend == "fused":
            j = knb.eat_query(arena.mass_pos[live], self.pos[hungry], float(P.eat_r), float(self.R))
            got = j >= 0
            best = np.full(len(hungry), -1); best[got] = live[j[got]]
            return self._consume(arena, hungry, best)
        h = P.eat_r
        M = int(2 * self.R / h) + 4
        def keys(p):
            c = np.clip(np.floor((p + self.R) / h).astype(np.int64) + 1, 0, M - 1)
            return (c[:, 0] * M + c[:, 1]) * M + c[:, 2]
        mk = keys(arena.mass_pos[live])
        o = np.argsort(mk, kind="stable"); mk = mk[o]; live = live[o]
        ak = keys(self.pos[hungry])
        best = np.full(len(hungry), -1); bd = np.full(len(hungry), np.inf)
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    q = ak + (dx * M + dy) * M + dz
                    j = np.searchsorted(mk, q)
                    ok = (j < len(mk)) & (mk[np.minimum(j, len(mk) - 1)] == q)
                    if not ok.any():
                        continue
                    jj = live[j[ok]]
                    d = np.linalg.norm(arena.mass_pos[jj] - self.pos[hungry[ok]], axis=1)
                    better = (d < bd[ok]) & (d < h)
                    idx = np.flatnonzero(ok)[better]
                    best[idx] = jj[better]; bd[idx] = d[better]
        return self._consume(arena, hungry, best)

    def _consume(self, arena, hungry, best):
        P = self.P
        got = best >= 0
        if not got.any():
            return
        eaters, prisms = hungry[got], best[got]
        prisms, first = np.unique(prisms, return_index=True)
        eaters = eaters[first]
        for i, pi in zip(eaters, prisms):
            v = arena.consume(int(pi), self.P.name)
            if v > 0:
                self.stock[i] += v
                self.hunger[i] = max(0.0, self.hunger[i] - v * P.hunger_per_vol)

    def _die(self, arena, i, cause="?"):
        """Wither (continuity) and leave the stock behind as prisms (mass conserved); one crystal drops."""
        if self.dying[i] or not self.alive[i]:
            return
        self.dying[i] = True
        self.death_log.append((round(arena.t, 2), cause, float(self.hunger[i]), float(self.starve[i])))
        v = float(self.stock[i])
        if v > 0:
            # re-lay the body's stock as up to 3 prisms where it fell (a skeleton the food web can graze)
            parts = max(1, min(3, int(v // 10) or 1))
            for k in range(parts):
                arena.lay_mass(self.pos[i] + self.rng.normal(0, 3, 3), v / parts, 0)
            self.laid += v
        self.stock[i] = 0.0
        self.crystals += 1
