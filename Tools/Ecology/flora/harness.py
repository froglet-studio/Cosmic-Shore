"""Direction F (threat flora) harness, on top of the shared arena (Tools/Ecology/common/).

Everything flora-specific lives here so `common/` stays untouched for the five sibling sessions:

* `FloraArena(Arena)` - the same cell, plus a GARDEN band the plants live in and the pilots fly through,
  and four extra pilot policies a stationary threat needs to be scored against:
      wander   (common)   flies to random goals in the garden, blind to the plants
      reader   flies the SAME kind of goals but reads only what the plants TELEGRAPH (`hazards()`):
               context-steers around visible danger, boosts across a primed mouth it cannot avoid, and
               throttles to a light touch near a pod that says "do not disturb". It never sees hidden state,
               so `counterplay` measures the quality of the telegraph, not a cheat.
      cutter   (the engaging player) flies at the nearest plant heart and CUTS prisms in its path (an active
               force - a vessel ability), collecting the heart -> the plant's crystal.
      courier  loops a fixed 3-waypoint route through the garden (the player who keeps using one lane) -
               used for re-route / lane-blocking measurements and for traffic-sensing plants.
* `PlantBody` - an SoA prism store a species lays its body into: position, half-size, volume, danger flag,
  owner plant, alive. Laying debits the species' reserve, resorbing credits it, cutting moves volume to
  `cut_volume` (the vessel ability that removed it). Nothing here removes mass on a clock.
* contact tests (swept pilot segment vs prisms), the plant metrics (lane coverage, threat density on the
  pilot's path, growth rate, re-route time), a precise telegraph metric, the mass audit, and the composite
  REPLAYABLE score R every search in this direction maximises.

Hit kinds (all logged through `Arena.hit`, so the common scorecard counts them):
    burn  a danger prism (an opposing-domain danger prism is the elemental economy's only SINK)
    snap  a trap closing on the pilot (in game: a danger-prism jaw, i.e. also a burn)
Plain plant prisms are counted separately as BUMPS (ramming plain mass slows you; it is not a burn).
"""
from __future__ import annotations

import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from common.arena import Arena, Pilot, Grid  # noqa: E402

GARDEN = (250.0, 850.0)          # (legacy shell band; unused since round 1 - see GROVE)
GROVE_C = np.array([0.0, 0.0, 600.0])
GROVE_R = 450.0                  # the GROVE: a ball the plants live in and the pilots fly through. A stationary
                                 # threat is a PLACE; in a 600 u-thick shell a wanderer's mean free path between
                                 # plant clumps was ~8000 u and it met no plant at all (round 0, see DISCOVERIES).
VIEW = 450.0                     # how far a pilot can READ a telegraph
BURN_COOLDOWN = 1.0
SLOW_STRENGTH, SLOW_S = 1.5, 3.0  # danger slow (fleet tuning): strength 0.5 x 3, duration 1 s x 3              # one hazard element burns one pilot at most once per this many seconds


# ------------------------------------------------------------------------------------------------- pilots
class FloraArena(Arena):
    """`trails`: every pilot lays a trail prism (`trail_vol`) every `trail_gap` u it flies - the game's truth (a
    vessel IS a mass source), and the one continuous food supply a creeping plant can follow. Created volume is
    tracked in `created` so the mass audit stays exact."""

    def __init__(self, seed=7, trails=True, trail_gap=24.0, trail_vol=6.0, n_crystals=6, divert=300.0, **kw):
        super().__init__(seed=seed, **kw)
        # real omni crystals: a pilot diverts to any it can see within `divert` (players go for crystals). A
        # species may publish LURES (mimics) through `lures()`; a pilot cannot tell a lure from a crystal unless
        # it is a reader inside the lure's tell range.
        self.crys = self.grove(n_crystals, 30.0) if n_crystals else np.zeros((0, 3))
        self.divert = divert; self.collected = {}; self.lured = {}; self.known_lures = {}; self.goal_t = {}; self.slow_t = {}
        self.bait_t = {}; self.bait_skip = {}
        self.trails, self.trail_gap, self.trail_vol = trails, trail_gap, trail_vol
        self.created = 0.0; self.trail_acc = {}; self.n_trail = 0
        self.species = None                  # the flora species under test (set by run())
        self.goals_reached = {}
        self.path_len = {}
        self.boost_left = {}
        self.boost_cool = {}
        self.courier_route = self.grove(3, 60.0)
        self.courier_i = {}
        self.base_speed = {}

    def hit(self, pilot, kind, amount=1.0):
        """A burn also SLOWS, as in game: VesselChangeSpeedByPrismEffectSO on a danger prism slows at
        maxSlowStrength x dangerSlowMultiplier (0.5 x 3 = 1.5, i.e. clamped to a stop) for speedModifierDuration x
        dangerSlowDurationMultiplier (1 x 3 = 3 s) - the fleet's shared tuning (CLAUDE.md). Modelled as a linear
        recovery 1 - 1.5 (1 - t/3), floored at 0.1. The arena may do this; a vessel may not (ELEMENTAL_ECONOMY §9)."""
        super().hit(pilot, kind, amount)
        self.slow_t[pilot.name] = self.t

    def speed_factor(self, p):
        t0 = self.slow_t.get(p.name)
        if t0 is None: return 1.0
        x = (self.t - t0) / SLOW_S
        return 1.0 if x >= 1 else max(0.1, 1.0 - SLOW_STRENGTH * (1.0 - x))

    def add_pilot(self, p: Pilot):
        super().add_pilot(p)
        p.pos = self.grove(1)[0]
        p.goal = self.grove(1)[0]
        self.goals_reached[p.name] = 0; self.path_len[p.name] = 0.0
        self.boost_left[p.name] = 0.0; self.boost_cool[p.name] = 0.0
        self.courier_i[p.name] = 0; self.base_speed[p.name] = p.speed
        return p

    def step(self, dt):
        before = [p.pos.copy() for p in self.pilots]
        for p in self.pilots:
            self.boost_left[p.name] = max(0.0, self.boost_left[p.name] - dt)
            self.boost_cool[p.name] = max(0.0, self.boost_cool[p.name] - dt)
            if p.policy not in ("reader",):
                p.speed = self.base_speed[p.name]
            p.speed *= self.speed_factor(p)
        super().step(dt)
        for p, b in zip(self.pilots, before):
            L = float(np.linalg.norm(p.pos - b))
            self.path_len[p.name] += L
            p.prev = b
            if self.trails:
                self.trail_acc[p.name] = self.trail_acc.get(p.name, 0.0) + L
                if self.trail_acc[p.name] >= self.trail_gap:
                    self.trail_acc[p.name] = 0.0
                    back = b - (p.pos - b) / max(L, 1e-6) * 8.0
                    self.lay_mass(back, self.trail_vol, elem=4 % 4)
                    self.created += self.trail_vol; self.n_trail += 1
                    self.mass_grid.build(self.mass_pos, self.mass_alive)

    def _new_goal(self, p, reached=True):
        if reached: self.goals_reached[p.name] += 1
        p.goal = self.grove(1)[0]; self.goal_t[p.name] = self.t

    def grove(self, n, margin=0.0):
        """n points volume-uniform in the grove ball (shrunk by margin)."""
        return GROVE_C + self._ball(n, 0.0, GROVE_R - margin)

    def grove_mass(self, n, clumps=20, vol=(8.0, 40.0), spread=25.0):
        """Food prisms in clumps inside the grove (the shared arena's scatter_mass, relocated)."""
        rng = self.rng
        centres = self.grove(clumps, 40.0); which = rng.integers(0, clumps, n)
        p = centres[which] + rng.normal(0, spread, (n, 3))
        self.mass_pos = np.concatenate([self.mass_pos, p]); self.mass_vol = np.concatenate([self.mass_vol, rng.uniform(*vol, n)])
        self.mass_elem = np.concatenate([self.mass_elem, (which % 4).astype(np.int8)])
        self.mass_alive = np.concatenate([self.mass_alive, np.ones(n, bool)])
        self.mass_shielded = np.concatenate([self.mass_shielded, np.zeros(n, bool)])
        self.mass_grid.build(self.mass_pos, self.mass_alive)

    def _pilot_goal(self, p):
        if p.policy == "courier":
            g = self.courier_route[self.courier_i[p.name] % 3]
            if np.linalg.norm(g - p.pos) < 60.0:
                self.courier_i[p.name] += 1; self.goals_reached[p.name] += 1
            return self.courier_route[self.courier_i[p.name] % 3]
        if p.policy == "cutter":
            T = self.species.cut_targets() if self.species is not None else np.zeros((0, 3))
            if len(T):
                return T[np.argmin(np.linalg.norm(T - p.pos, axis=1))]
        if np.linalg.norm(p.goal - p.pos) < 60.0:
            self._new_goal(p)
        elif self.t - self.goal_t.get(p.name, 0.0) > 15.0:
            self._new_goal(p, reached=False)          # players re-plan: an unreachable goal is dropped after 15 s
        bait = self._bait(p)
        if bait is not None:
            if p.policy == "reader":
                return self._read(p, bait)
            return bait
        if p.policy == "reader" and self.species is not None:
            return self._read(p)
        return p.goal

    def _bait(self, p):
        """Opportunism: the nearest crystal (or lure) within `divert`. Collect real ones on contact (they respawn).
        A reader skips a lure whose TELL it can read (inside the lure's tell range)."""
        C = self.crys; L, tell = (self.species.lures() if self.species is not None and hasattr(self.species, "lures")
                                 else (np.zeros((0, 3)), np.zeros(0)))
        for k in np.flatnonzero(np.linalg.norm(C - p.pos, axis=1) < 12.0) if len(C) else []:
            self.collected[p.name] = self.collected.get(p.name, 0) + 1
            C[k] = self.grove(1, 30.0)[0]
        P = np.concatenate([C, L]); is_lure = np.r_[np.zeros(len(C), bool), np.ones(len(L), bool)]
        if not len(P): return None
        d = np.linalg.norm(P - p.pos, axis=1)
        ok = d < self.divert
        if p.policy == "reader" and len(L):
            # a reader that has READ a lure's tell remembers it (lures walk slowly; 80 u matches it next time)
            known = self.known_lures.setdefault(p.name, [])
            seen = is_lure & (d < np.r_[np.zeros(len(C)), tell])
            for q in P[seen]:
                if not any(np.linalg.norm(q - k) < 80.0 for k in known): known.append(q.copy())
            for m in np.flatnonzero(is_lure):
                for k in known:
                    if np.linalg.norm(P[m] - k) < 80.0:
                        ok[m] = False; k[:] = P[m]; break
        for q, until in self.bait_skip.get(p.name, []):          # baits given up on, for 20 s
            if until > self.t: ok &= np.linalg.norm(P - q, axis=1) > 40.0
        if not ok.any(): return None
        j = int(np.flatnonzero(ok)[np.argmin(d[ok])])
        q0, t0 = self.bait_t.get(p.name, (None, self.t))
        if q0 is None or np.linalg.norm(P[j] - q0) > 40.0:
            self.bait_t[p.name] = (P[j].copy(), self.t)
        elif self.t - t0 > 10.0:                                    # chased it 10 s and never got it: give up
            self.bait_skip.setdefault(p.name, []).append((P[j].copy(), self.t + 20.0)); self.bait_t.pop(p.name)
            return None
        if is_lure[j]: self.lured[p.name] = self.lured.get(p.name, 0) + 1
        return P[j]

    def _read(self, p, goal=None):
        """Context steering over 26 directions, scored by goal alignment minus VISIBLE danger ahead."""
        base = self.base_speed[p.name]
        H, Rr, W, soft = self.species.hazards()
        goal = p.goal if goal is None else goal
        want = goal - p.pos; want /= max(np.linalg.norm(want), 1e-6)
        v = p.vel / max(np.linalg.norm(p.vel), 1e-6)
        speed = base * (1.7 if self.boost_left[p.name] > 0 else 1.0)
        if len(H):
            d = np.linalg.norm(H - p.pos, axis=1); m = d < 260
            H, Rr, W = H[m], Rr[m], W[m]
        if not len(H):
            p.speed = speed * self.speed_factor(p)
            return goal
        # candidates: the exact goal direction and the current heading first (26 fixed directions alone are ~40
        # degrees apart, so a reader could never home onto a 12 u crystal - it orbited them; round 3), then the sphere
        dirs = np.vstack([want, v, _DIRS])
        look = np.array([40.0, 90.0, 150.0, 220.0])
        # sample points along each candidate direction; penalty = sum of hazards those points fall inside
        pts = p.pos[None, None, :] + dirs[:, None, :] * look[None, :, None]           # (26,4,3)
        dd = np.linalg.norm(pts[:, :, None, :] - H[None, None, :, :], axis=3)          # (26,4,h)
        # MAX over hazards per sample point (a cluster of teeth is one hazard, not eight), a 25 u margin, and the
        # near look points weigh most. Round 3's reader SUMMED penalties with a 40 u margin: a trap cluster vetoed
        # every direction and the reader collected 10 crystals to the blind wanderer's 44 - `access` was measuring
        # the pilot model, not the plant.
        inside = np.clip(1.0 - (dd - Rr[None, None, :]) / 25.0, 0, 1) * W[None, None, :]
        danger = (inside.max(axis=2) * np.array([1.0, 0.7, 0.4, 0.2])[None, :]).sum(axis=1)
        score = dirs @ want * 1.0 + dirs @ v * 0.6 - 2.5 * danger
        best = int(np.argmax(score))
        # unavoidable danger right ahead: boost through it (the "speed" counter)
        if danger[best] > 0.6 and self.boost_cool[p.name] <= 0 and self.boost_left[p.name] <= 0:
            self.boost_left[p.name] = 1.0; self.boost_cool[p.name] = 4.0
        # light touch: near a pod that says "do not disturb", throttle down
        if soft is not None and len(soft):
            if np.min(np.linalg.norm(soft - p.pos, axis=1)) < 90 and self.boost_left[p.name] <= 0:
                speed = min(speed, 45.0)
        p.speed = speed * self.speed_factor(p)
        return p.pos + dirs[best] * 200.0


def _fib(n):
    i = np.arange(n) + 0.5
    phi = np.arccos(1 - 2 * i / n); th = math.pi * (1 + 5 ** 0.5) * i
    return np.stack([np.cos(th) * np.sin(phi), np.sin(th) * np.sin(phi), np.cos(phi)], 1)


_DIRS = _fib(26)


def seg_point_dist(a, b, P):
    """Distance from each point in P (n,3) to segment a-b."""
    ab = b - a; L2 = float(ab @ ab)
    if L2 < 1e-12:
        return np.linalg.norm(P - a, axis=1)
    t = np.clip(((P - a) @ ab) / L2, 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)


# ------------------------------------------------------------------------------------------------- bodies
class PlantBody:
    """Prism store for one species. Growable arrays; laying debits a reserve, resorbing credits it."""

    def __init__(self, cap=4096):
        self.pos = np.zeros((cap, 3)); self.half = np.zeros(cap); self.vol = np.zeros(cap)
        self.danger = np.zeros(cap, bool); self.owner = np.full(cap, -1); self.alive = np.zeros(cap, bool)
        self.hot = np.zeros(cap)            # 0..1 visible glow (the telegraph a reader can see)
        self.shield = np.zeros(cap, bool)   # CHARGE armour: the first ram/cut sheds it instead of breaking the prism
        self.burn_cd = {}                    # (prism, pilot) -> t until it can burn again
        self.n = 0
        self.laid = 0; self.laid_vol = 0.0; self.cut_volume = 0.0

    def _grow(self):
        cap = len(self.vol) * 2
        for k in ("pos", "half", "vol", "danger", "owner", "alive", "hot", "shield"):
            a = getattr(self, k); b = np.zeros((cap,) + a.shape[1:], a.dtype)
            if k == "owner": b[:] = -1
            b[:len(a)] = a; setattr(self, k, b)

    def lay(self, p, half, vol, owner=-1, danger=False, shield=False):
        if self.n >= len(self.vol): self._grow()
        i = self.n; self.n += 1
        self.pos[i] = p; self.half[i] = half; self.vol[i] = vol; self.owner[i] = owner
        self.danger[i] = danger; self.alive[i] = True; self.hot[i] = 0.0; self.shield[i] = shield
        self.laid += 1; self.laid_vol += vol
        return i

    def take(self, j) -> float:
        """An active force (ram, cut) hits plain prism j: a shielded prism SHEDS its shield (0 volume removed), an
        unshielded one breaks and its volume is returned (the caller books it as cut mass)."""
        if not self.alive[j]: return 0.0
        if self.shield[j]:
            self.shield[j] = False; return 0.0
        self.alive[j] = False; return float(self.vol[j])

    def live(self):
        return np.flatnonzero(self.alive[:self.n])

    def total_volume(self):
        return float(self.vol[:self.n][self.alive[:self.n]].sum())

    def resorb(self, i) -> float:
        if not self.alive[i]: return 0.0
        self.alive[i] = False; return float(self.vol[i])

    def contacts(self, a, b, radius, idx=None):
        """Live prisms whose box (as a sphere of radius half) a pilot segment a->b of hull radius touches."""
        idx = self.live() if idx is None else idx
        if not len(idx): return idx
        d = seg_point_dist(a, b, self.pos[idx])
        return idx[d < self.half[idx] + radius]

    def burn_check(self, arena, pilot, idx, kind="burn"):
        """Burn the pilot for each danger prism in idx (per-prism cooldown). Returns #burns."""
        k = 0
        for i in idx:
            if not self.danger[i]: continue
            key = (int(i), pilot.name)
            if self.burn_cd.get(key, -1e9) > arena.t: continue
            self.burn_cd[key] = arena.t + BURN_COOLDOWN
            arena.hit(pilot, kind); k += 1
        return k


# ------------------------------------------------------------------------------------------------- species base
class FloraSpecies:
    """What every threat-flora species implements (on top of common's `step/render`):

        step(arena, dt)            simulate one tick, test pilot contacts, call arena.hit
        render(out)                viewer buffers
        hazards()                  (pos (h,3), radius (h,), weight (h,), soft (s,3)|None) - ONLY what the plant
                                   telegraphs (glow, gape, swell). A reading pilot steers on this.
        threat_elements()          (pos (t,3), radius (t,)) - every LATENT threat zone: what would hurt a pilot who
                                   flew there now, including a dormant trap that would fire (lane coverage and
                                   threat density; hidden state included - it is a measurement). Round 2 counted
                                   only ACTIVE threats and scored every ambush plant ~0 presence.
        cut_targets()              hearts a cutter flies at
        cut(arena, pilot, a, b)    the pilot's ability removes prisms along its path / collects hearts
        mass_total()               volume held by the species (reserve + body + any free-floating mass)
        cut_volume                 volume removed by vessel abilities
        crystals, deaths           crystals dropped / plants that died (must be equal: every plant drops one)
        agent_pos/agent_vel/intent the common Probe's channels (one entry per plant or threat site)
    """
    name = "flora"
    crystals = 0
    deaths = 0
    kills = 0

    def plant_count(self):
        return 0


# ------------------------------------------------------------------------------------------------- metrics
class FloraProbe:
    """Plant-specific metrics, sampled every `every` steps."""

    def __init__(self, arena, dt, n_lanes=64, every=10):
        rng = np.random.default_rng(12345)
        d = rng.normal(size=(n_lanes, 2, 3)); d /= np.linalg.norm(d, axis=2, keepdims=True)
        r = np.cbrt(rng.random((n_lanes, 2))) * GROVE_R
        self.lanes = GROVE_C + d * r[..., None]
        self.dt, self.every, self.k = dt, every, 0
        self.coverage = []; self.density = []; self.mass_series = []; self.prisms = []; self.route_bias = []
        r = arena.courier_route; self.route = [(r[i], r[(i + 1) % 3]) for i in range(3)]
        self.route_len = sum(float(np.linalg.norm(b - a)) for a, b in self.route)
        self.lane_len = float(np.linalg.norm(self.lanes[:, 1] - self.lanes[:, 0], axis=1).sum())
        self.lead_samples = []
        self.vis_since = {}                 # (pilot) -> {element key -> first t visible-hot within VIEW}

    def observe(self, arena, sp):
        self.k += 1
        # threat density on the pilot's path: threat elements within 60 u of each pilot, every step
        T, TR = sp.threat_elements()
        for p in arena.pilots:
            if len(T):
                self.density.append(float(np.sum(np.linalg.norm(T - p.pos, axis=1) < TR + 60.0)))
            else:
                self.density.append(0.0)
        if self.k % self.every: return
        if len(T):
            cov = 0
            for a, b in self.lanes:
                if np.any(seg_point_dist(a, b, T) < TR + 8.0): cov += 1
            self.coverage.append(cov / len(self.lanes))
            on_route = sum(int(np.sum(seg_point_dist(a, b, T) < TR + 60.0)) for a, b in self.route) / self.route_len
            on_lanes = sum(int(np.sum(seg_point_dist(a, b, T) < TR + 60.0)) for a, b in self.lanes) / self.lane_len
            self.route_bias.append((on_route + 1e-4) / (on_lanes + 1e-4))
        else:
            self.coverage.append(0.0); self.route_bias.append(1.0)
        pc = getattr(sp, "prism_count", None)          # a field species (coral) counts its occupied voxels
        self.prisms.append(pc if pc is not None else (sp.body.alive[:sp.body.n].sum() if hasattr(sp, "body") else 0))

    def summary(self, minutes):
        pr = np.asarray(self.prisms, float)
        return dict(lane_coverage=round(float(np.mean(self.coverage)), 3) if self.coverage else 0.0,
                    lane_coverage_end=round(float(self.coverage[-1]), 3) if self.coverage else 0.0,
                    threat_density=round(float(np.mean(self.density)), 3) if self.density else 0.0,
                    prisms_end=int(pr[-1]) if len(pr) else 0,
                    growth_per_min=round(float((pr[-1] - pr[0]) / max(minutes, 1e-6)), 1) if len(pr) else 0.0)


def precise_leads(arena, sp_leads):
    """Species report, per hit, how long the element that hit was visibly hot before the hit (capped 5 s)."""
    return [min(5.0, x) for x in sp_leads]


# ------------------------------------------------------------------------------------------------- runner
def resolve(spec):
    """'module:Class' -> a species factory (strings keep the multiprocessing pool picklable)."""
    if callable(spec): return spec
    import importlib
    mod, cls = spec.split(":")
    C = getattr(importlib.import_module(mod), cls)
    return lambda a, p: C(a, p)


def run(spec, params, seed, policy, minutes=2.0, dt=0.1, record=None, perturb=0.0, cut_event=None, trails=True):
    """One species x one pilot policy x one seed. Returns a run dict (common fields + plant metrics).
    `perturb` nudges the pilot's start by that many units with an independent rng (twin runs for variety)."""
    from common.scorecard import Probe, run_score
    make_species = resolve(spec)
    ar = FloraArena(seed=seed, trails=trails)
    ar.grove_mass(1600, clumps=20)
    mk = dict(wander=Pilot.wanderer, reader=lambda: Pilot("reader", speed=120.0, name="reader"),
              cutter=lambda: Pilot("cutter", speed=140.0, name="cutter"),
              courier=lambda: Pilot("courier", speed=120.0, name="courier"))[policy]
    sp = make_species(ar, params)
    ar.species = sp
    pilot = ar.add_pilot(mk())
    if perturb:
        pilot.pos = pilot.pos + np.random.default_rng(seed + 999).normal(0, perturb, 3)
    pilot.prev = pilot.pos.copy()
    m0 = ar.live_volume() + sp.mass_total() + sp.cut_volume
    pr = Probe(dt); fp = FloraProbe(ar, dt)
    t0 = time.perf_counter(); steps = int(minutes * 60 / dt)
    cut_report = None
    for k in range(steps):
        sp.step(ar, dt)
        if policy == "cutter":
            sp.cut(ar, pilot, pilot.prev, pilot.pos)
        if cut_event is not None and abs(ar.t - cut_event["t"]) < dt / 2:
            cut_report = sp.lane_cut(ar, cut_event)
        ar.step(dt)
        pr.observe(ar, sp); fp.observe(ar, sp)
        if record is not None: record.frame(ar, [sp])
    ms = (time.perf_counter() - t0) / steps * 1000
    m1 = ar.live_volume() + sp.mass_total() + sp.cut_volume - ar.created
    out = run_score(ar, sp, pr, minutes)
    out.update(fp.summary(minutes))
    out["goals_per_min"] = round((ar.goals_reached[pilot.name] + ar.collected.get(pilot.name, 0)) / minutes, 2)
    out["crystals_collected_per_min"] = round(ar.collected.get(pilot.name, 0) / minutes, 2)
    out["leads"] = [round(min(5.0, x), 2) for x in getattr(sp, "leads", [])]
    out["mass_drift"] = (m1 - m0) / max(m0, 1e-9)
    out["crystals"] = sp.crystals; out["deaths"] = sp.deaths
    out["kills_per_min"] = round(sp.crystals / minutes, 2)
    out["py_ms_per_step"] = round(ms, 2)
    kinds = {}
    for (_t, _n, kd, _a) in ar.log: kinds[kd] = kinds.get(kd, 0) + 1
    out["kinds"] = kinds
    rb = np.asarray(fp.route_bias); h = len(rb) // 2
    out["route_bias_early"] = float(rb[:h].mean()) if h else 1.0
    out["route_bias_late"] = float(rb[h:].mean()) if h else 1.0
    out["signature"] = np.asarray(sp.signature(), float).tolist() if hasattr(sp, "signature") else []
    if cut_report is not None:
        out["cut"] = cut_report
    return out


def _job(args):
    spec, params, seed, policy, minutes, perturb = args
    return (policy if not perturb else policy + "_twin", seed), run(spec, params, seed, policy, minutes, perturb=perturb)


def scorecard(spec, params, seeds=(7, 23, 41), minutes=2.0, policies=("wander", "reader", "cutter", "courier"), procs=4, twins=True):
    jobs = [(spec, params, s, p, minutes, 0.0) for p in policies for s in seeds]
    if twins:
        jobs += [(spec, params, s, "wander", minutes, 5.0) for s in seeds]
    if procs > 1 and isinstance(spec, str):
        import multiprocessing as mp
        with mp.get_context("fork").Pool(min(procs, len(jobs))) as pool:
            runs = dict(pool.map(_job, jobs))
    else:
        runs = dict(_job(j) for j in jobs)
    return summarize(runs, minutes), runs


def _sigdist(a, b):
    a, b = np.asarray(a, float), np.asarray(b, float)
    n = max(len(a), len(b))
    a = np.pad(a, (0, n - len(a))); b = np.pad(b, (0, n - len(b)))
    s = np.abs(a).sum() + np.abs(b).sum()
    return float(np.abs(a - b).sum() / s) if s > 0 else 0.0


def summarize(runs, minutes):
    def mean(pol, key):
        v = [r[key] for (p, s), r in runs.items() if p == pol and r.get(key) is not None]
        return float(np.mean(v)) if v else None
    w = mean("wander", "hits_per_min"); rd = mean("reader", "hits_per_min")
    leads = [x for (p, s), r in runs.items() if p in ("wander", "reader") for x in r["leads"]]
    # variety (twin): same world, the pilot nudged 5 u at the start -> how differently did the PLANTS respond?
    tw = [_sigdist(runs[("wander", s)]["signature"], r["signature"]) for (p, s), r in runs.items() if p == "wander_twin"]
    cut_cpm = mean("cutter", "kills_per_min"); cut_bpm = mean("cutter", "hits_per_min")
    card = dict(
        hits_per_min_wander=_r(w), hits_per_min_reader=_r(rd),
        counterplay=_r(rd / w) if w else None,
        telegraph_s=_r(float(np.median(leads))) if leads else None,
        telegraph_p10=_r(float(np.percentile(leads, 10))) if leads else None,
        unwarned_frac=_r(float(np.mean(np.asarray(leads) < 0.7))) if leads else None,
        payoff_per_min=_r(cut_cpm), cutter_burns_per_min=_r(cut_bpm),
        crystals_per_burn=_r(cut_cpm / max(cut_bpm, 0.25)) if cut_cpm is not None else None,
        variety=_r(float(np.mean(tw))) if tw else 0.0,
        avoid_cost=_r(mean("reader", "goals_per_min") / max(mean("wander", "goals_per_min") or 1e-6, 1e-6))
        if mean("wander", "goals_per_min") else None,
        lane_coverage=_r(mean("wander", "lane_coverage")),
        threat_density=_r(mean("wander", "threat_density")),
        growth_per_min=_r(mean("wander", "growth_per_min")),
        prisms_end=_r(mean("wander", "prisms_end")),
        mass_drift_max=float(max(abs(r["mass_drift"]) for r in runs.values())),
        crystal_law=all(r["crystals"] == r["deaths"] for r in runs.values()),
        py_ms_per_step=_r(mean("wander", "py_ms_per_step")),
        courier_hits_per_min=_r(mean("courier", "hits_per_min")),
        adapt=_r(mean("courier", "route_bias_late") / max(mean("courier", "route_bias_early"), 1e-3))
        if mean("courier", "route_bias_early") is not None else None,
        kinds={k: sum(r["kinds"].get(k, 0) for (p, s), r in runs.items() if p == "wander") for k in
               sorted({k for r in runs.values() for k in r["kinds"]})},
    )
    card["R"] = replay_score(card)
    card["R_hard"] = replay_score_hard(card)
    return card


def _r(x, n=3):
    return None if x is None else round(float(x), n)


def _band(x, lo, hi):
    """1 inside [lo, hi], log falloff outside (a factor of 4 away -> 0)."""
    if x is None or x <= 0: return 0.0
    if lo <= x <= hi: return 1.0
    f = math.log(lo / x) if x < lo else math.log(x / hi)
    return max(0.0, 1.0 - f / math.log(4.0))


def replay_score(c):
    """Composite replayable-threat score in [0,1]: the geometric mean of seven terms (PROGRAM.md §2), each floored
    at 0.01 so one dead axis drags the whole score down without zeroing it.
       threat      hits/min vs a blind wanderer inside [1, 6]       (present, not a meat grinder)
       counterplay reader/wanderer hit ratio; <= 0.3 is full marks  (reading the telegraph IS the counter)
       telegraph   the WORST decile of visible lead before a hit, >= 0.7 s full marks (unwarned hits are what
                   frustrate; a median hides them)
       payoff      cutter crystals per burn taken inside [0.5, 3] AND >= 1 crystal/min (killing it pays, but it
                   costs exposure - free crystals are not a threat, unwinnable ones are not a reward)
       variety     twin-run divergence: same world, the pilot nudged 5 u at the start; >= 0.3 full marks
       access      reader goals/min over wanderer goals/min >= 0.75 (the counter is not 'stay home')
       presence    lane coverage inside [0.1, 0.4]: flora's job is to SHAPE routes - block some lanes, not all
       adapt       ROUTE BIAS growth for a COURIER that keeps flying one 3-waypoint loop: threat per unit length near
                   its route over threat per unit length near 64 random lanes, late half / early half. 1.5x is full
                   marks; a plant that ignores traffic scores 0.5 (the place changes because you were there).
                   (Round 1 used raw density late/early - confounded by plain growth; physarum read 2.48 for that.)
       payoff is the EXCHANGE RATE (crystals per burn) and only needs >= 1 crystal/min: the absolute harvest rate
       is set by how many plants the colony grows from food - a cell population budget, not a plant property
       (a [1, 6]/min band was tried in round 3 and punished every colony that fed well; it is still reported).
       Hard gates: mass drift < 1e-6 and every dead plant dropped its crystal, else R = 0."""
    if c["mass_drift_max"] > 1e-6 or not c["crystal_law"]:
        return 0.0
    cp = c["counterplay"]; f = 0.01
    terms = dict(
        threat=max(f, _band(c["hits_per_min_wander"], 1.0, 6.0)),
        counterplay=f if cp is None else float(np.clip((1.0 - cp) / 0.7, f, 1.0)),
        telegraph=f if c["telegraph_p10"] is None else float(np.clip(c["telegraph_p10"] / 0.7, f, 1.0)),
        payoff=max(f, _band(c["crystals_per_burn"], 0.5, 3.0) * min(1.0, (c["payoff_per_min"] or 0) / 1.0)),
        variety=float(np.clip(c["variety"] / 0.3, f, 1.0)),
        access=float(np.clip((c["avoid_cost"] or 0) / 0.75, f, 1.0)),
        presence=max(f, _band(c["lane_coverage"], 0.1, 0.4)),
        adapt=0.5 if c["adapt"] is None else float(np.clip(0.5 + (c["adapt"] - 1.0), 0.5, 1.0)),
    )
    c["R_terms"] = {k: round(v, 3) for k, v in terms.items()}
    return round(float(np.exp(np.mean(np.log(list(terms.values()))))), 4)


def replay_score_hard(c):
    """The stretch bar, for when R saturates (snap trap reached 0.97 in two search steps). Same seven axes, tighter:
       threat [1.5, 4]/min, counterplay <= 0.15, telegraph worst decile >= 1.0 s, exchange rate [0.75, 2] crystals per
       burn, twin variety >= 0.5, reader access >= 0.9, coverage [0.15, 0.3], route-bias growth >= 1.75x (static 0.3)."""
    if c["mass_drift_max"] > 1e-6 or not c["crystal_law"]:
        return 0.0
    cp = c["counterplay"]; f = 0.01
    t = dict(
        threat=max(f, _band(c["hits_per_min_wander"], 1.5, 4.0)),
        counterplay=f if cp is None else float(np.clip((1.0 - cp) / 0.85, f, 1.0)),
        telegraph=f if c["telegraph_p10"] is None else float(np.clip(c["telegraph_p10"] / 1.0, f, 1.0)),
        payoff=max(f, _band(c["crystals_per_burn"], 0.75, 2.0) * min(1.0, (c["payoff_per_min"] or 0) / 1.0)),
        variety=float(np.clip(c["variety"] / 0.5, f, 1.0)),
        access=float(np.clip((c["avoid_cost"] or 0) / 0.9, f, 1.0)),
        presence=max(f, _band(c["lane_coverage"], 0.15, 0.3)),
        adapt=0.3 if c["adapt"] is None else float(np.clip(0.3 + 0.7 * (c["adapt"] - 1.0) / 0.75, 0.3, 1.0)),
    )
    c["R_hard_terms"] = {k: round(v, 3) for k, v in t.items()}
    return round(float(np.exp(np.mean(np.log(list(t.values()))))), 4)
