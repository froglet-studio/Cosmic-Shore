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

GARDEN = (250.0, 850.0)          # the band the plants live in and the pilots fly through
VIEW = 450.0                     # how far a pilot can READ a telegraph
BURN_COOLDOWN = 1.0              # one hazard element burns one pilot at most once per this many seconds


# ------------------------------------------------------------------------------------------------- pilots
class FloraArena(Arena):
    def __init__(self, seed=7, **kw):
        super().__init__(seed=seed, **kw)
        self.species = None                  # the flora species under test (set by run())
        self.goals_reached = {}
        self.path_len = {}
        self.boost_left = {}
        self.boost_cool = {}
        self.courier_route = self._ball(3, GARDEN[0] + 50, GARDEN[1] - 50)
        self.courier_i = {}
        self.base_speed = {}

    def add_pilot(self, p: Pilot):
        super().add_pilot(p)
        p.pos = self._ball(1, GARDEN[0], GARDEN[1])[0]
        p.goal = self._ball(1, GARDEN[0], GARDEN[1])[0]
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
        super().step(dt)
        for p, b in zip(self.pilots, before):
            self.path_len[p.name] += float(np.linalg.norm(p.pos - b))
            p.prev = b

    def _new_goal(self, p):
        self.goals_reached[p.name] += 1
        p.goal = self._ball(1, GARDEN[0], GARDEN[1])[0]

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
        if p.policy == "reader" and self.species is not None:
            return self._read(p)
        return p.goal

    def _read(self, p):
        """Context steering over 26 directions, scored by goal alignment minus VISIBLE danger ahead."""
        base = self.base_speed[p.name]
        H, Rr, W, soft = self.species.hazards()
        want = p.goal - p.pos; want /= max(np.linalg.norm(want), 1e-6)
        v = p.vel / max(np.linalg.norm(p.vel), 1e-6)
        speed = base * (1.7 if self.boost_left[p.name] > 0 else 1.0)
        if len(H):
            d = np.linalg.norm(H - p.pos, axis=1); m = d < 260
            H, Rr, W = H[m], Rr[m], W[m]
        if not len(H):
            p.speed = speed
            return p.goal
        dirs = _DIRS
        look = np.array([40.0, 90.0, 150.0, 220.0])
        # sample points along each candidate direction; penalty = sum of hazards those points fall inside
        pts = p.pos[None, None, :] + dirs[:, None, :] * look[None, :, None]           # (26,4,3)
        dd = np.linalg.norm(pts[:, :, None, :] - H[None, None, :, :], axis=3)          # (26,4,h)
        inside = np.clip(1.0 - (dd - Rr[None, None, :]) / 40.0, 0, 1) * W[None, None, :]
        danger = (inside * np.array([1.0, 0.8, 0.5, 0.3])[None, :, None]).sum(axis=(1, 2))
        score = dirs @ want * 1.0 + dirs @ v * 0.6 - 3.0 * danger
        best = int(np.argmax(score))
        # unavoidable danger right ahead: boost through it (the "speed" counter)
        if danger[best] > 0.6 and self.boost_cool[p.name] <= 0 and self.boost_left[p.name] <= 0:
            self.boost_left[p.name] = 1.0; self.boost_cool[p.name] = 4.0
        # light touch: near a pod that says "do not disturb", throttle down
        if soft is not None and len(soft):
            if np.min(np.linalg.norm(soft - p.pos, axis=1)) < 90 and self.boost_left[p.name] <= 0:
                speed = min(speed, 45.0)
        p.speed = speed
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
        self.burn_cd = {}                    # (prism, pilot) -> t until it can burn again
        self.n = 0
        self.laid = 0; self.laid_vol = 0.0; self.cut_volume = 0.0

    def _grow(self):
        cap = len(self.vol) * 2
        for k in ("pos", "half", "vol", "danger", "owner", "alive", "hot"):
            a = getattr(self, k); b = np.zeros((cap,) + a.shape[1:], a.dtype)
            if k == "owner": b[:] = -1
            b[:len(a)] = a; setattr(self, k, b)

    def lay(self, p, half, vol, owner=-1, danger=False):
        if self.n >= len(self.vol): self._grow()
        i = self.n; self.n += 1
        self.pos[i] = p; self.half[i] = half; self.vol[i] = vol; self.owner[i] = owner
        self.danger[i] = danger; self.alive[i] = True; self.hot[i] = 0.0
        self.laid += 1; self.laid_vol += vol
        return i

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
        threat_elements()          (pos (t,3), radius (t,)) - what can actually hurt a pilot right now (for lane
                                   coverage and threat density; hidden state included - it is a measurement)
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
        r = np.cbrt(GARDEN[0] ** 3 + rng.random((n_lanes, 2)) * (GARDEN[1] ** 3 - GARDEN[0] ** 3))
        self.lanes = d * r[..., None]
        self.dt, self.every, self.k = dt, every, 0
        self.coverage = []; self.density = []; self.mass_series = []; self.prisms = []
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
        else:
            self.coverage.append(0.0)
        self.prisms.append(sp.body.alive[:sp.body.n].sum() if hasattr(sp, "body") else 0)

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
def run(make_species, params, seed, policy, minutes=2.0, dt=0.1, record=None, extra_pilot=None, cut_event=None):
    """One species x one pilot policy x one seed. Returns a run dict (common fields + plant metrics)."""
    from common.scorecard import Probe, run_score
    ar = FloraArena(seed=seed)
    ar.scatter_mass(1600, r_lo=GARDEN[0] / ar.R, r_hi=GARDEN[1] / ar.R, clumps=20)
    mk = dict(wander=Pilot.wanderer, reader=lambda: Pilot("reader", speed=120.0, name="reader"),
              cutter=lambda: Pilot("cutter", speed=140.0, name="cutter"),
              courier=lambda: Pilot("courier", speed=120.0, name="courier"))[policy]
    sp = make_species(ar, params)
    ar.species = sp
    pilot = ar.add_pilot(mk())
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
    m1 = ar.live_volume() + sp.mass_total() + sp.cut_volume
    out = run_score(ar, sp, pr, minutes)
    out.update(fp.summary(minutes))
    out["goals_per_min"] = round(ar.goals_reached[pilot.name] / minutes, 2)
    out["leads"] = [round(x, 2) for x in getattr(sp, "leads", [])]
    out["mass_drift"] = (m1 - m0) / max(m0, 1e-9)
    out["crystals"] = sp.crystals; out["deaths"] = sp.deaths
    out["kills_per_min"] = round(sp.crystals / minutes, 2)
    out["py_ms_per_step"] = round(ms, 2)
    out["burns"] = sum(1 for h in pilot.hits)
    if cut_report is not None:
        out["cut"] = cut_report
    return out


def scorecard(make_species, params, seeds=(7, 23, 41), minutes=2.0, policies=("wander", "reader", "cutter")):
    runs = {(p, s): run(make_species, params, s, p, minutes) for p in policies for s in seeds}
    return summarize(runs, minutes), runs


def summarize(runs, minutes):
    def mean(pol, key):
        v = [r[key] for (p, s), r in runs.items() if p == pol and r.get(key) is not None]
        return float(np.mean(v)) if v else None
    w = mean("wander", "hits_per_min"); rd = mean("reader", "hits_per_min")
    leads = [x for (p, s), r in runs.items() if p in ("wander", "reader") for x in r["leads"]]
    hs = []
    for (p, s), r in runs.items():
        if p == "wander" and r["hit_times"]:
            h, _ = np.histogram(r["hit_times"], bins=12, range=(0, minutes * 60)); hs.append(h / max(h.sum(), 1))
    var = float(np.mean([np.abs(a - b).sum() for i, a in enumerate(hs) for b in hs[i + 1:]])) if len(hs) > 1 else 0.0
    card = dict(
        hits_per_min_wander=_r(w), hits_per_min_reader=_r(rd),
        counterplay=_r(rd / w) if w else None,
        telegraph_s=_r(float(np.median(leads))) if leads else None,
        telegraph_p10=_r(float(np.percentile(leads, 10))) if leads else None,
        payoff_per_min=_r(mean("cutter", "kills_per_min")),
        cutter_burns_per_min=_r(mean("cutter", "hits_per_min")),
        variety=_r(var),
        avoid_cost=_r(mean("reader", "goals_per_min") / max(mean("wander", "goals_per_min") or 1e-6, 1e-6))
        if mean("wander", "goals_per_min") else None,
        lane_coverage=_r(mean("wander", "lane_coverage")),
        threat_density=_r(mean("wander", "threat_density")),
        growth_per_min=_r(mean("wander", "growth_per_min")),
        prisms_end=_r(mean("wander", "prisms_end")),
        mass_drift_max=float(max(abs(r["mass_drift"]) for r in runs.values())),
        crystal_law=all(r["crystals"] == r["deaths"] for r in runs.values()),
        py_ms_per_step=_r(mean("wander", "py_ms_per_step")),
    )
    card["R"] = replay_score(card)
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
    """Composite replayable-threat score in [0,1] - the geometric mean of six terms (PROGRAM.md §2):
       threat      hits/min vs a blind wanderer inside [1, 6] (present, not a meat grinder)
       counterplay reader/wanderer hit ratio, <= 0.3 is full marks (reading the telegraph is the counter)
       telegraph   median visible lead before a hit, >= 0.8 s full marks
       payoff      cutter crystals/min, >= 1/min full marks
       variety     hit-time histogram distance across seeds, >= 0.5 full marks
       access      reader goals/min over wanderer goals/min >= 0.75 (the counter is not 'stay home')
       Hard gates: mass drift < 1e-6 and every dead plant dropped its crystal, else R = 0."""
    if c["mass_drift_max"] > 1e-6 or not c["crystal_law"]:
        return 0.0
    cp = c["counterplay"]
    terms = dict(
        threat=max(0.01, _band(c["hits_per_min_wander"], 1.0, 6.0)),
        counterplay=0.01 if cp is None else float(np.clip((1.0 - cp) / 0.7, 0.05, 1.0)),
        telegraph=0.05 if c["telegraph_s"] is None else float(np.clip(c["telegraph_s"] / 0.8, 0.05, 1.0)),
        payoff=float(np.clip((c["payoff_per_min"] or 0) / 1.0, 0.05, 1.0)),
        variety=float(np.clip(c["variety"] / 0.5, 0.05, 1.0)),
        access=float(np.clip((c["avoid_cost"] or 0) / 0.75, 0.05, 1.0)),
    )
    c["R_terms"] = {k: round(v, 3) for k, v in terms.items()}
    return round(float(np.exp(np.mean(np.log(list(terms.values()))))), 4)
