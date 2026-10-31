#!/usr/bin/env python3
"""coevo.py - co-evolve escort (AI vessel) parameters against fauna parameters on the sim farm.

    # in one terminal: the farm (or pass --spawn-farm here)
    python Tools/SimFarm/farm.py run --root D:/VoyageFarm -n 4
    # in another: the driver (resumes the session if it exists)
    python Tools/SimFarm/coevo.py --root D:/VoyageFarm --session s1 --space Tools/SimFarm/space.example.json \
        --generations 40
    # a dry run with a synthetic evaluator, no Unity:
    python Tools/SimFarm/coevo.py --root D:/VoyageFarm --session fake1 --fake --generations 6

Two populations, each a flat dict of named genes (space.json): vessels (P_v) and fauna (P_f). Generations alternate
the evolving side: even generations evolve vessels against frozen fauna, odd ones the reverse. Each evolving
individual meets K opponents (K-1 from the frozen population, one from the frozen side's hall of fame), each pairing
one farm job of R runs at difficulties cycled from --difficulties. A job's params are the two genomes' params merged
(vessel keys first, fauna keys over them), passed opaquely to -arkEscortParams; genes named "__..." are the driver's
own (e.g. "__squad", the vessel's squad) and never sent.

Objectives (both maximised, per side, scored over ALL of a genome's runs so far, mean minus half a standard error):
  vessel fit        difficulty-weighted win rate + 0.25 * mean lowest hull
  vessel diversity  novelty: mean distance of its behaviour descriptor to its k nearest in archive + population
  fauna fit         -mean_d |win(d) - target(d)|, target(d) = 1 - (d-1)/8, + 0.1 * tension (close calls)
  fauna diversity   spread of the descriptors of the runs WON against it (many strategies can beat it)
The evolving side's selection alternates FIT / DIVERSIFY each time it evolves (--regime alternate; or 'patience',
the diamond controller's plateau rule); in DIVERSIFY the score is diversity minus a leaky penalty below
(peak fit - --floor-margin): the soft floor. Each side keeps a Pareto archive over (fit, diversity[, -complexity]).

Session folder <root>/coevo/<session>/: state.json (resumable at any point), runs.jsonl, gen_NNN.json,
archive_vessel.json, archive_fauna.json, summary.md, best/*.json (params ready for -arkEscortParams).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import statistics
import subprocess
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from farmlib import DEFAULT_ROOT, FINISHED, Farm, read_json, write_json_atomic  # noqa: E402
from pareto import Regime, non_dominated, soft_floor_score, truncate  # noqa: E402

SIDES = ("vessel", "fauna")

# The "idle-ish" escort: every gene at its laziest end (gates the fauna: it must lose at d >= 3). Genes the space lacks
# are skipped; "min"/"max" pick the end of the gene's range.
IDLE = {
    "ai.escort.guardRadius": "min", "ai.escort.stationRadius": "max", "ai.escort.retargetMargin": "max",
    "ai.escort.threatW_closing": "min",
    "ai.sparrow.gunRange": "min", "ai.sparrow.gunAimDeg": "min", "ai.sparrow.rocketAimDeg": "min",
    "ai.sparrow.rocketCooldown": "max", "ai.sparrow.rocketMaxRange": "min", "ai.sparrow.standoff": "max",
    "ai.sparrow.nucleusSweepWhenThreatsBelow": "min",
    "ai.squirrel.joustMargin": "max", "ai.squirrel.joustMaxTurnDeg": "min", "ai.squirrel.cultivate": "min",
    "ai.dolphin.reachUse": "min", "ai.dolphin.approachAlignDeg": "min", "ai.dolphin.crystalSearchRadius": "min",
}
DESC_LEGS = 5


# ---- genome space ---------------------------------------------------------------------------------------------
class Space:
    """{"vessel": {"genes": {name: spec}}, "fauna": {"genes": {...}}}; spec = {"type": "float"|"int"|"choice"|"bool",
    "min", "max", "default", "choices", "sigma" (fraction of range), "log": bool}."""

    def __init__(self, path: Path | None, default_squad: list[str]):
        self.spec = read_json(path) if path else {"vessel": {"genes": {}}, "fauna": {"genes": {}}}
        for side in SIDES:
            self.spec.setdefault(side, {}).setdefault("genes", {})
        g = self.spec["vessel"]["genes"]
        if "__squad" not in g:
            g["__squad"] = {"type": "choice", "choices": [",".join(default_squad)], "default": ",".join(default_squad)}

    def genes(self, side: str) -> dict:
        return self.spec[side]["genes"]

    def default(self, side: str) -> dict:
        out = {}
        for k, s in self.genes(side).items():
            if "default" in s:
                out[k] = s["default"]
            elif s.get("type") in ("choice",):
                out[k] = s["choices"][0]
            elif s.get("type") == "bool":
                out[k] = False
            else:
                out[k] = (s["min"] + s["max"]) / 2
        return out

    def sample(self, side: str, rng: np.random.Generator) -> dict:
        out = {}
        for k, s in self.genes(side).items():
            t = s.get("type", "float")
            if t == "choice":
                out[k] = s["choices"][int(rng.integers(len(s["choices"])))]
            elif t == "bool":
                out[k] = bool(rng.random() < 0.5)
            else:
                out[k] = self._from_unit(s, float(rng.random()))
        return out

    @staticmethod
    def _to_unit(s, v) -> float:
        lo, hi = float(s["min"]), float(s["max"])
        if s.get("log") and lo > 0:
            return (math.log(v) - math.log(lo)) / (math.log(hi) - math.log(lo))
        return (float(v) - lo) / (hi - lo) if hi > lo else 0.0

    @staticmethod
    def _from_unit(s, u) -> float | int:
        u = min(1.0, max(0.0, u))
        lo, hi = float(s["min"]), float(s["max"])
        v = math.exp(math.log(lo) + u * (math.log(hi) - math.log(lo))) if s.get("log") and lo > 0 else lo + u * (hi - lo)
        return int(round(v)) if s.get("type") == "int" else round(v, 5)

    def mutate(self, side: str, g: dict, rng: np.random.Generator, sigma: float, flip: float, rate: float) -> dict:
        out = dict(g)
        specs = self.genes(side)
        keys = list(specs)
        touched = False
        for k in keys:
            if rng.random() > rate:
                continue
            touched = True
            s = specs[k]
            t = s.get("type", "float")
            if t == "choice":
                if rng.random() < flip * 3 and len(s["choices"]) > 1:
                    out[k] = s["choices"][int(rng.integers(len(s["choices"])))]
            elif t == "bool":
                if rng.random() < flip * 3:
                    out[k] = not out.get(k, False)
            else:
                u = self._to_unit(s, out.get(k, self.default(side)[k]))
                out[k] = self._from_unit(s, u + rng.normal(0, sigma * s.get("sigma", 1.0)))
        if not touched and keys:  # always change something
            k = keys[int(rng.integers(len(keys)))]
            s = specs[k]
            if s.get("type", "float") in ("float", "int"):
                u = self._to_unit(s, out.get(k, self.default(side)[k]))
                out[k] = self._from_unit(s, u + rng.normal(0, sigma))
        return out

    def crossover(self, side: str, a: dict, b: dict, rng: np.random.Generator) -> dict:
        return {k: (a if rng.random() < 0.5 else b).get(k) for k in self.genes(side)}

    def complexity(self, side: str, g: dict) -> int:
        d = self.default(side)
        n = 0
        for k, s in self.genes(side).items():
            if k.startswith("__"):
                continue
            if s.get("type", "float") in ("float", "int"):
                if abs(self._to_unit(s, g.get(k, d[k])) - self._to_unit(s, d[k])) > 0.05:
                    n += 1
            elif g.get(k) != d.get(k):
                n += 1
        return n

    def params(self, side: str, g: dict) -> dict:
        """The genes the run sees: numbers only, switches as 0/1, and only those off their default, so the default
        genome is the game as shipped (a gene at its default is not sent)."""
        out = {}
        specs = self.genes(side)
        for k, v in g.items():
            if k.startswith("__"):
                continue
            d = specs.get(k, {}).get("default")
            if d is not None and not isinstance(v, str) and abs(float(v) - float(d)) <= 1e-6 * max(1.0, abs(float(d))):
                continue
            out[k] = int(v) if isinstance(v, bool) else v
        return out

    def unit_vector(self, side: str, g: dict) -> np.ndarray:
        v = []
        for k, s in self.genes(side).items():
            t = s.get("type", "float")
            if t == "choice":
                v.append(s["choices"].index(g[k]) / max(1, len(s["choices"]) - 1) if g.get(k) in s["choices"] else 0)
            elif t == "bool":
                v.append(1.0 if g.get(k) else 0.0)
            else:
                v.append(self._to_unit(s, g.get(k, self.default(side)[k])))
        return np.array(v, dtype=float)


def params_of(genes: dict) -> dict:
    """The genes the run sees: numbers only (the params registry takes "key": number), switches as 0/1."""
    return {k: (int(v) if isinstance(v, bool) else v) for k, v in genes.items() if not k.startswith("__")}


def gid_of(side: str, genes: dict) -> str:
    h = hashlib.sha1(json.dumps(genes, sort_keys=True).encode()).hexdigest()[:10]
    return f"{side[0]}{h}"


# ---- descriptors and objectives -------------------------------------------------------------------------------
DESC_NAMES = ["killsOnArkShare", "near300", "near900", "pacifiedShare", "killsPerMin/10", "legsReached"]


def descriptor(r: dict) -> list[float]:
    """A run's strategy: where the squad's kills land, how close it flies to the Ark, whether it pacifies worlds,
    how fast it kills, how far the Ark got. All in about [0, 1]."""
    dur = max(1.0, float(r.get("EndedAt", 0)) - float(r.get("UnderwayAt", 0)))
    legs = r.get("LegReports") or []
    kills = float(r.get("Kills", 0))
    kos = float(r.get("KillsOnArk", 0)) / kills if kills > 0 else 0.0
    n300 = n900 = samples = 0
    for l in legs:
        for st in l.get("Seats") or []:
            n300 += int(st.get("Near300", 0))
            n900 += int(st.get("Near900", 0))
            samples += int(st.get("Samples", 0))
    if samples == 0:  # older reports: the leg's escort-near count (within guard radius)
        samples = sum(int(l.get("EscortSamples", 0)) for l in legs) * 3
        n900 = sum(int(l.get("EscortNear", 0)) for l in legs)
    near300 = n300 / samples if samples else 0.0
    near900 = n900 / samples if samples else 0.0
    nlegs = max(1, int(r.get("Legs", 1) or 1))
    pacified = float(r.get("PacifiedLegs", 0)) / nlegs
    kpm = kills / (dur / 60.0) / 10.0
    reached = float(r.get("ReachedLegs", 0)) / nlegs
    return [min(1.0, kos), min(1.0, near300), min(1.0, near900), min(1.0, pacified), min(2.0, kpm), min(1.0, reached)]


def target(d: int) -> float:
    """The win rate the best escorts should have at difficulty d: (100 - 8d)%."""
    return max(0.0, 1.0 - 0.08 * d)


def lcb(xs: list[float]) -> float:
    if not xs:
        return -math.inf
    m = statistics.mean(xs)
    return m - 0.5 * (statistics.stdev(xs) / math.sqrt(len(xs)) if len(xs) > 1 else 0.25)


GATE_KOS = 0.5   # squad kills of fauna attacking the Ark >= 50% of squad kills
GATE_FF = 5.0    # friendly-fire plates per run <= 5


def vessel_gates(runs: list[dict]) -> float:
    """How far a vessel genome breaks the hard constraints (0 = feasible)."""
    kills = sum(r.get("kills", 0) for r in runs)
    kos = sum(r.get("kos", 0) for r in runs) / kills if kills > 0 else 0.0
    ff = statistics.mean(r.get("ff", 0) for r in runs) if runs else 0.0
    return max(0.0, GATE_KOS - kos) * 2 + max(0.0, ff - GATE_FF) / GATE_FF


def vessel_fit(runs: list[dict]) -> float:
    """Wins weighted by difficulty, + a quarter of the lowest hull."""
    if not runs:
        return -math.inf
    per = [(r["d"] * (1.0 if r["win"] else 0.0)) / 5.0 + 0.25 * r["hull"] for r in runs]
    return lcb(per)


def closeness(r: dict) -> float:
    """1 for the tensest ending: a win with the hull near zero, or a loss in the last world; 0 for a win with the hull
    untouched or a loss stripped in the first world."""
    if r["win"]:
        return 1.0 - r["hull"]
    return r.get("fallFrac", 0.0)


def fauna_fit(runs: list[dict], idle_runs: list[dict]) -> float:
    """Difficulty fit: per d, the strong escorts' win rate near (100 - 8d)%, the endings close; penalties when the
    idle-ish squad wins at d >= 3, or the strong escorts cannot win at the lowest difficulty."""
    if not runs:
        return -math.inf
    by: dict[int, list[float]] = {}
    for r in runs:
        by.setdefault(r["d"], []).append(1.0 if r["win"] else 0.0)
    err = statistics.mean(abs(statistics.mean(w) - target(d)) for d, w in by.items())
    close = statistics.mean(closeness(r) for r in runs)
    pen = 0.0
    idle_hi = [r for r in idle_runs if r["d"] >= 3]
    if idle_hi:
        pen += 0.5 * statistics.mean(1.0 if r["win"] else 0.0 for r in idle_hi)
    low = min(by)
    if low <= 3:
        wr = statistics.mean(by[low])
        pen += max(0.0, 0.5 - wr)
    n = len(runs)
    return -err + 0.15 * close - pen - 0.2 / math.sqrt(n)


def kmeans(x: np.ndarray, k: int, rng: np.random.Generator, iters: int = 30) -> np.ndarray:
    """Centroids of k clusters of the rows of x (plain Lloyd's, k-means++ seeding)."""
    c = [x[int(rng.integers(len(x)))]]
    while len(c) < k:
        d = np.min(((x[:, None, :] - np.array(c)[None]) ** 2).sum(-1), axis=1)
        if d.sum() <= 0:
            break
        c.append(x[int(rng.choice(len(x), p=d / d.sum()))])
    c = np.array(c)
    for _ in range(iters):
        lab = np.argmin(((x[:, None, :] - c[None]) ** 2).sum(-1), axis=1)
        new = np.array([x[lab == j].mean(0) if np.any(lab == j) else c[j] for j in range(len(c))])
        if np.allclose(new, c):
            break
        c = new
    return c


def nearest(c: np.ndarray, v) -> int:
    return int(np.argmin(((c - np.asarray(v)) ** 2).sum(-1)))


def spread(descs: list[list[float]]) -> float:
    if len(descs) < 2:
        return 0.0
    a = np.array(descs)
    d = np.sqrt(((a[:, None, :] - a[None, :, :]) ** 2).sum(-1))
    n = len(a)
    return float(d.sum() / (n * (n - 1))) * min(1.0, n / 3.0)


def novelty(desc: np.ndarray, others: list[np.ndarray], k: int = 5) -> float:
    if not others:
        return 0.0
    d = sorted(float(np.linalg.norm(desc - o)) for o in others)
    return float(np.mean(d[:k]))


# ---- fake evaluator (no Unity) --------------------------------------------------------------------------------
class FakeWorld:
    """A synthetic escort: vessel strength vs fauna strength vs difficulty, with a few strategy niches, so the driver's
    logic can be exercised in seconds."""

    def __init__(self, space: Space, seed: int = 7):
        r = np.random.default_rng(seed)
        self.space = space
        self.wv = r.normal(0, 1, len(space.genes("vessel")))
        self.wf = r.normal(0, 1, len(space.genes("fauna")))
        self.niches = r.random((3, len(space.genes("vessel"))))

    def run(self, vg: dict, fg: dict, d: int, rng: np.random.Generator) -> dict:
        v = self.space.unit_vector("vessel", vg)
        f = self.space.unit_vector("fauna", fg)
        # a vessel is strong near any niche; fauna strength is a linear score
        vs = 1.5 - min(float(np.linalg.norm(v - n)) for n in self.niches) + 0.2 * float(self.wv @ v) / len(v)
        fs = float(self.wf @ f) / max(1, len(f))
        p = 1 / (1 + math.exp(-(3 * (vs - fs) - 0.6 * (d - 5))))
        win = rng.random() < p
        hull = float(np.clip(rng.normal(0.6 if win else 0.2, 0.15), 0, 1))
        niche = int(np.argmin([np.linalg.norm(v - n) for n in self.niches]))
        legs = [{"Reached": True, "HullAtEntry": 100, "HullMin": int(100 - 30 * rng.random() * (1 + niche) / 3),
                 "ThreatSeconds": 30 * rng.random(), "EscortSamples": 100, "EscortNear": int(30 * (niche + 1) * rng.random())}
                for _ in range(5)]
        kills = int(10 * niche + 20 * rng.random()) + 1
        for l in legs:
            l["Seats"] = [{"Near300": int(40 * rng.random() * (niche + 1) / 3), "Near900": int(80 * rng.random()),
                           "Samples": 100} for _ in range(3)]
        return {"Difficulty": d, "Result": 1 if win else 2, "HullMinFraction": hull, "Kills": kills,
                "KillsOnArk": int(kills * (0.4 + 0.5 * rng.random())), "FriendlyFire": int(rng.integers(0, 8)),
                "PacifiedLegs": niche, "FallenLeg": -1 if win else int(rng.integers(0, 5)),
                "UnderwayAt": 10, "EndedAt": 280, "HullTotal": 150, "Legs": 5, "ReachedLegs": 5 if win else 3,
                "LegReports": legs}


# ---- the driver -----------------------------------------------------------------------------------------------
class Driver:
    def __init__(self, a):
        self.a = a
        self.warned_params = False
        self.farm = Farm(a.root).ensure()
        self.dir = self.farm.root / "coevo" / a.session
        self.dir.mkdir(parents=True, exist_ok=True)
        (self.dir / "best").mkdir(exist_ok=True)
        self.state_path = self.dir / "state.json"
        if self.state_path.exists():
            self.s = read_json(self.state_path)
            print(f"[coevo] resuming {a.session}: generation {self.s['gen']}, phase {self.s['phase']}", flush=True)
            space_path = Path(self.s["spacePath"]) if self.s.get("spacePath") else None
        else:
            self.s = None
            space_path = Path(a.space) if a.space else None
        self.space = Space(space_path, a.squad.split(","))
        self.rng = np.random.default_rng(a.seed)
        if self.s is None:
            self.init_state(space_path)
        else:
            self.rng.bit_generator.state = self.s["rng"]
        self.regime = {side: Regime.from_json(self.s["regime"][side]) for side in SIDES}
        self.fake = FakeWorld(self.space) if a.fake else None
        self.farm_proc = None

    # ---- state ------------------------------------------------------------------------------------------------
    def init_state(self, space_path):
        a = self.a
        self.s = {"session": a.session, "spacePath": str(space_path.resolve()) if space_path else None,
                  "args": {k: v for k, v in vars(a).items() if isinstance(v, (int, float, str, bool, type(None)))},
                  "gen": 0, "phase": "plan", "genomes": {}, "pop": {}, "archive": {"vessel": [], "fauna": []},
                  "elites": {"vessel": [], "fauna": []}, "peakFit": {"vessel": None, "fauna": None},
                  "jobs": [], "regime": {s: Regime(mode=a.regime, patience=a.patience).to_json() for s in SIDES}}
        for side, n in (("vessel", a.pop_vessel), ("fauna", a.pop_fauna)):
            pop = [self.add_genome(side, self.space.default(side), born=0)]
            while len(pop) < n:
                pop.append(self.add_genome(side, self.space.mutate(side, self.space.default(side), self.rng,
                                                                   a.init_sigma, 0.3, 0.5), born=0))
            self.s["pop"][side] = pop
        idle = dict(self.space.default("vessel"))
        for k, end in IDLE.items():
            spec = self.space.genes("vessel").get(k)
            if spec:
                idle[k] = spec[end]
        self.s["idle"] = self.add_genome("vessel", idle, born=0)
        self.s["genomes"][self.s["idle"]]["idle"] = True
        self.save()

    def save(self):
        self.s["rng"] = self.rng.bit_generator.state
        if hasattr(self, "regime"):
            self.s["regime"] = {side: self.regime[side].to_json() for side in SIDES}
        write_json_atomic(self.state_path, self.s)

    def add_genome(self, side: str, genes: dict, born: int) -> str:
        gid = gid_of(side, genes)
        if gid not in self.s["genomes"]:
            self.s["genomes"][gid] = {"side": side, "genes": genes, "born": born, "runs": []}
        return gid

    # ---- one generation ---------------------------------------------------------------------------------------
    def evolving(self, gen: int) -> str:
        return "vessel" if gen % 2 == 0 else "fauna"

    def hall_of_fame(self, side: str) -> list[str]:
        hof = list(dict.fromkeys(self.s["archive"][side] + self.s["elites"][side]))
        return hof

    def plan(self):
        gen = self.s["gen"]
        side = self.evolving(gen)
        other = "fauna" if side == "vessel" else "vessel"
        a = self.a
        k = a.opponents_vessel if side == "vessel" else a.opponents_fauna
        frozen = list(self.s["pop"][other])
        hof = [g for g in self.hall_of_fame(other) if g not in frozen]
        diffs = [int(x) for x in a.difficulties.split(",")]
        jobs = []
        for i, gid in enumerate(self.s["pop"][side]):
            opps = list(self.rng.choice(frozen, size=min(len(frozen), max(1, k - (1 if hof else 0))), replace=False))
            if hof:
                opps.append(str(self.rng.choice(hof)))
            for j, opp in enumerate(opps):
                vg, fg = (gid, opp) if side == "vessel" else (opp, gid)
                off = int(self.rng.integers(len(diffs)))
                runs = []
                squad = self.s["genomes"][vg]["genes"].get("__squad", a.squad).split(",")
                for r in range(a.runs_per_pairing):
                    runs.append({"index": r, "difficulty": diffs[(off + r) % len(diffs)], "squad": squad})
                params = dict(self.space.params("vessel", self.s["genomes"][vg]["genes"]))
                params.update(self.space.params("fauna", self.s["genomes"][fg]["genes"]))
                job_id = f"{a.session}-g{gen:03d}-{i:02d}{j}-{vg[:7]}-{fg[:7]}"
                jobs.append({"jobId": job_id, "seed": int(self.rng.integers(1, 2**30)), "runs": runs,
                             "params": params or None, "tag": f"coevo:{a.session}",
                             "meta": {"gen": gen, "vessel": vg, "fauna": fg}})
        idle = self.s.get("idle")
        if side == "fauna" and idle and a.idle_runs > 0:
            hi = [d for d in diffs if d >= 3] or diffs
            for i, gid in enumerate(self.s["pop"]["fauna"]):
                runs = [{"index": r, "difficulty": hi[(i + r) % len(hi)],
                         "squad": self.s["genomes"][idle]["genes"].get("__squad", a.squad).split(",")}
                        for r in range(a.idle_runs)]
                params = dict(self.space.params("vessel", self.s["genomes"][idle]["genes"]))
                params.update(self.space.params("fauna", self.s["genomes"][gid]["genes"]))
                jobs.append({"jobId": f"{a.session}-g{gen:03d}-idle{i:02d}-{gid[:7]}",
                             "seed": int(self.rng.integers(1, 2**30)),
                             "runs": runs, "params": params or None, "tag": f"coevo:{a.session}",
                             "meta": {"gen": gen, "vessel": idle, "fauna": gid}})
        self.s["jobs"] = [{"jobId": j["jobId"], "vessel": j["meta"]["vessel"], "fauna": j["meta"]["fauna"],
                           "runs": j["runs"], "job": j} for j in jobs]
        if not self.fake:
            for j in jobs:
                if self.farm.job_state(j["jobId"]) is None:
                    self.farm.submit(j)
        self.s["phase"] = "submitted"
        self.save()
        n_runs = sum(len(j["runs"]) for j in jobs)
        print(f"[coevo] gen {gen}: {side} side evolves ({self.regime[side].current}), {len(jobs)} jobs, {n_runs} runs",
              flush=True)

    def wait(self) -> dict[str, dict]:
        """jobId -> {RunIndex: result line}, once every job is done (resubmitting any that vanished)."""
        out = {}
        if self.fake:
            for j in self.s["jobs"]:
                vg = self.s["genomes"][j["vessel"]]["genes"]
                fg = self.s["genomes"][j["fauna"]]["genes"]
                out[j["jobId"]] = {r["index"]: self.fake.run(vg, fg, r["difficulty"], self.rng) for r in j["runs"]}
            return out
        t0 = time.time()
        last = -1
        while True:
            left = []
            for j in self.s["jobs"]:
                st = self.farm.job_state(j["jobId"])
                if st == "done":
                    continue
                if st is None:
                    print(f"[coevo] {j['jobId']} vanished - resubmitting", flush=True)
                    self.farm.submit(j["job"])
                left.append(j)
            if not left:
                break
            if len(left) != last:
                last = len(left)
                print(f"[coevo] gen {self.s['gen']}: {len(left)}/{len(self.s['jobs'])} jobs left "
                      f"({(time.time() - t0) / 60:.1f} min)", flush=True)
            if self.farm_proc and self.farm_proc.poll() is not None:
                sys.exit(f"[coevo] the spawned farm exited ({self.farm_proc.returncode}); resume to continue")
            time.sleep(self.a.poll)
        for j in self.s["jobs"]:
            out[j["jobId"]] = self.farm.read_results(j["jobId"])
        return out

    def ingest(self, results: dict[str, dict]):
        gen = self.s["gen"]
        n_ok = n_bad = 0
        with open(self.dir / "runs.jsonl", "a", encoding="utf-8") as f:
            for j in self.s["jobs"]:
                res = results.get(j["jobId"], {})
                for r in j["runs"]:
                    rec = res.get(r["index"])
                    if not rec or rec.get("Result", 0) not in FINISHED or rec.get("Result") == 3:
                        n_bad += 1
                        continue
                    n_ok += 1
                    legs = max(1, int(rec.get("Legs", 5) or 5))
                    fallen = int(rec.get("FallenLeg", -1))
                    refused = [p for p in rec.get("Params") or [] if not p.get("Applied", True)]
                    if refused and not self.warned_params:
                        self.warned_params = True
                        print(f"[coevo] WARNING refused params in {j['jobId']}: "
                              + ", ".join(f"{p.get('Key')} ({p.get('Note')})" for p in refused[:6]), flush=True)
                    run = {"gen": gen, "d": int(rec.get("Difficulty", r["difficulty"])), "win": rec["Result"] == 1,
                           "hull": float(rec.get("HullMinFraction", 0)), "desc": descriptor(rec),
                           "kills": int(rec.get("Kills", 0)), "kos": int(rec.get("KillsOnArk", 0)),
                           "ff": int(rec.get("FriendlyFire", 0)),
                           "fallFrac": (fallen / max(1, legs - 1)) if rec["Result"] == 2 and fallen >= 0 else 0.0,
                           "idle": j["vessel"] == self.s.get("idle")}
                    self.s["genomes"][j["vessel"]]["runs"].append(dict(run, opp=j["fauna"]))
                    self.s["genomes"][j["fauna"]]["runs"].append(dict(run, opp=j["vessel"]))
                    f.write(json.dumps(dict(rec, coevo={"gen": gen, "vessel": j["vessel"], "fauna": j["fauna"],
                                                        "session": self.a.session})) + "\n")
        print(f"[coevo] gen {gen}: {n_ok} runs scored, {n_bad} without a usable result", flush=True)
        return n_ok, n_bad

    def clusters(self):
        """Strategy clusters: k-means over the descriptors of every winning (non-idle) escort run in memory."""
        g = self.s["genomes"]
        wins = [r["desc"] for k, v in g.items() if v["side"] == "vessel" and not v.get("idle")
                for r in v["runs"][-self.a.memory:] if r["win"]]
        if len(wins) < 8:
            return None
        k = min(self.a.clusters, len(wins) // 4)
        return kmeans(np.array(wins), k, np.random.default_rng(self.s["gen"] + 7))

    def vessel_cluster(self, gid: str, c) -> int:
        runs = self.s["genomes"][gid]["runs"][-self.a.memory:]
        won = [r["desc"] for r in runs if r["win"]] or [r["desc"] for r in runs]
        return nearest(c, np.mean(won, axis=0))

    def objectives(self, side: str, gids: list[str]) -> dict[str, tuple[float, ...]]:
        """(fit, diversity[, -complexity]); a vessel that breaks the gates is pushed below every feasible one."""
        g = self.s["genomes"]
        out = {}
        gids = [k for k in gids if not g[k].get("idle")]
        if side == "vessel":
            descs = {}
            for k in gids:
                runs = g[k]["runs"][-self.a.memory:]
                won = [r["desc"] for r in runs if r["win"]]
                if runs:
                    descs[k] = np.mean(won or [r["desc"] for r in runs], axis=0)
            for k in gids:
                runs = g[k]["runs"][-self.a.memory:]
                if not runs:
                    continue
                others = [v for o, v in descs.items() if o != k]
                fit, div = vessel_fit(runs), novelty(descs[k], others)
                viol = vessel_gates(runs)
                if viol > 0:
                    fit, div = fit - 10 - viol, div - 10 - viol
                obj = (fit, div)
                out[k] = obj + ((-self.space.complexity(side, g[k]["genes"]),) if self.a.parsimony else ())
        else:
            c = self.clusters()
            cl = {}
            if c is not None:
                for k, v in g.items():
                    if v["side"] == "vessel" and not v.get("idle") and v["runs"]:
                        cl[k] = self.vessel_cluster(k, c)
            for k in gids:
                runs = g[k]["runs"][-self.a.memory:]
                strong = [r for r in runs if not r.get("idle")]
                idle = [r for r in runs if r.get("idle")]
                if not strong:
                    continue
                fit = fauna_fit(strong, idle)
                # Diversity: the fauna should beat some strategy clusters, not all and not none.
                per: dict[int, list[float]] = {}
                for r in strong:
                    if r.get("opp") in cl:
                        per.setdefault(cl[r["opp"]], []).append(1.0 if r["win"] else 0.0)
                div = 0.0
                if len(per) >= 2:
                    beaten = sum(1 for w in per.values() if statistics.mean(w) < 0.5)
                    frac = beaten / len(per)
                    div = (1.0 - abs(2 * frac - 1)) * min(1.0, len(per) / 3.0)
                    div += 0.1 * float(np.std([statistics.mean(w) for w in per.values()]))
                obj = (fit, div)
                out[k] = obj + ((-self.space.complexity(side, g[k]["genes"]),) if self.a.parsimony else ())
        return out

    def select_score(self, side: str, obj: tuple[float, ...]) -> float:
        fit, div = obj[0], obj[1]
        if self.regime[side].current == Regime.FIT:
            return fit
        peak = self.s["peakFit"][side]
        return soft_floor_score(fit, div, peak if peak is not None else fit, self.a.floor_margin, self.a.floor_beta)

    def evolve(self, side: str, objs: dict[str, tuple[float, ...]]) -> list[str]:
        a = self.a
        n = a.pop_vessel if side == "vessel" else a.pop_fauna
        score = {k: self.select_score(side, o) for k, o in objs.items()}
        ranked = sorted(score, key=lambda k: -score[k])
        elites = ranked[: max(1, n // 3)]
        pool = ranked + [k for k in self.s["archive"][side] if k not in score]
        pool_score = dict(score)
        for k in pool:
            pool_score.setdefault(k, -math.inf)

        def tournament() -> str:
            c = self.rng.choice(pool, size=min(3, len(pool)), replace=False)
            return max(c, key=lambda k: pool_score[k])

        new = list(elites)
        tries = 0
        while len(new) < n and tries < n * 20:
            tries += 1
            p = self.s["genomes"][tournament()]["genes"]
            if self.rng.random() < a.crossover:
                p = self.space.crossover(side, p, self.s["genomes"][tournament()]["genes"], self.rng)
            child = self.space.mutate(side, p, self.rng, a.sigma, a.flip, a.rate)
            gid = self.add_genome(side, child, born=self.s["gen"] + 1)
            if gid not in new:
                new.append(gid)
        return new

    def step(self):
        gen = self.s["gen"]
        side = self.evolving(gen)
        if self.s["phase"] == "plan":
            self.plan()
        results = self.wait()
        n_ok, n_bad = self.ingest(results)

        # Archives over everything this side has ever fielded that has runs: the front, capped by crowding.
        report = {"gen": gen, "evolving": side, "regime": self.regime[side].current, "runs": n_ok, "bad": n_bad,
                  "sides": {}}
        for sd in SIDES:
            cands = list(dict.fromkeys(self.s["archive"][sd] + self.s["pop"][sd]))
            objs = self.objectives(sd, cands)
            front = truncate(objs, self.a.archive_cap) if objs else []
            # the archive is the non-dominated part of the kept set
            self.s["archive"][sd] = non_dominated({k: objs[k] for k in front}) if front else []
            fits = [o[0] for o in objs.values() if math.isfinite(o[0])]
            if fits:
                pf = self.s["peakFit"][sd]
                self.s["peakFit"][sd] = max(fits) if pf is None else max(pf, max(fits))
            report["sides"][sd] = {
                "pop": {k: list(objs.get(k, ())) for k in self.s["pop"][sd]},
                "archive": {k: list(objs[k]) for k in self.s["archive"][sd]},
                "peakFit": self.s["peakFit"][sd]}
        # Evolve the side that played this generation.
        pop_objs = {k: o for k, o in self.objectives(side, self.s["pop"][side]).items()}
        best_regime = max((self.select_score(side, o) for o in pop_objs.values()), default=-math.inf)
        report["bestRegimeScore"] = best_regime
        self.s["elites"][side] = list(dict.fromkeys(
            self.s["elites"][side] + sorted(pop_objs, key=lambda k: -pop_objs[k][0])[:1]))[-self.a.elites:]
        self.s["pop"][side] = self.evolve(side, pop_objs) if pop_objs else self.s["pop"][side]
        self.regime[side].step(best_regime)
        report["nextPop"] = self.s["pop"][side]

        write_json_atomic(self.dir / f"gen_{gen:03d}.json", report)
        for sd in SIDES:
            write_json_atomic(self.dir / f"archive_{sd}.json",
                              [{"gid": k, "objectives": report["sides"][sd]["archive"][k],
                                "genes": self.s["genomes"][k]["genes"], "runs": len(self.s["genomes"][k]["runs"])}
                               for k in self.s["archive"][sd]])
        self.write_best(report)
        self.write_summary(report)
        self.s["gen"] = gen + 1
        self.s["phase"] = "plan"
        self.s["jobs"] = []
        self.save()

    def write_best(self, report):
        index = {}
        for sd in SIDES:
            arch = report["sides"][sd]["archive"]
            if not arch:
                continue
            by_fit = max(arch, key=lambda k: arch[k][0])
            by_div = max(arch, key=lambda k: arch[k][1])
            for name, k in (("fit", by_fit), ("diverse", by_div)):
                p = self.dir / "best" / f"{sd}_{name}.json"
                write_json_atomic(p, self.space.params(sd, self.s["genomes"][k]["genes"]))
                index[f"{sd}_{name}"] = {"gid": k, "objectives": arch[k],
                                         "squad": self.s["genomes"][k]["genes"].get("__squad")}
            for i, k in enumerate(sorted(arch, key=lambda k: -arch[k][0])):
                write_json_atomic(self.dir / "best" / f"{sd}_front_{i:02d}.json",
                                  self.space.params(sd, self.s["genomes"][k]["genes"]))
        # one combined file: best-fit fauna over the most diverse winning escort
        if "vessel_diverse" in index and "fauna_fit" in index:
            comb = dict(self.space.params("vessel", self.s["genomes"][index["vessel_fit"]["gid"]]["genes"]))
            comb.update(self.space.params("fauna", self.s["genomes"][index["fauna_fit"]["gid"]]["genes"]))
            write_json_atomic(self.dir / "best" / "combined.json", comb)
        # The fauna per difficulty: the genome whose strong-escort win rate at d is nearest the target (>= 2 runs).
        by_d = {}
        for k, v in self.s["genomes"].items():
            if v["side"] != "fauna":
                continue
            per: dict[int, list[float]] = {}
            for r in v["runs"][-self.a.memory:]:
                if not r.get("idle"):
                    per.setdefault(r["d"], []).append(1.0 if r["win"] else 0.0)
            for d, w in per.items():
                if len(w) < 2:
                    continue
                err = abs(statistics.mean(w) - target(d)) + 0.3 / math.sqrt(len(w))
                if d not in by_d or err < by_d[d]["err"]:
                    by_d[d] = {"gid": k, "err": round(err, 3), "win": round(statistics.mean(w), 3), "runs": len(w),
                               "target": target(d)}
        for d, b in by_d.items():
            write_json_atomic(self.dir / "best" / f"fauna_d{d}.json",
                              self.space.params("fauna", self.s["genomes"][b["gid"]]["genes"]))
        index["fauna_by_difficulty"] = {str(d): b for d, b in sorted(by_d.items())}
        write_json_atomic(self.dir / "best" / "index.json", dict(index, gen=report["gen"]))

    def write_summary(self, report):
        p = self.dir / "summary.md"
        if not p.exists():
            p.write_text(f"# coevo session {self.a.session}\n\n| gen | evolving | regime | runs | bad | "
                         "vessel best fit | vessel best div | vessel front | fauna best fit | fauna best div | fauna front |\n"
                         "|---|---|---|---|---|---|---|---|---|---|---|\n", encoding="utf-8")
        cells = []
        for sd in SIDES:
            arch = report["sides"][sd]["archive"]
            fits = [o[0] for o in arch.values()]
            divs = [o[1] for o in arch.values()]
            cells += [f"{max(fits):.3f}" if fits else "-", f"{max(divs):.3f}" if divs else "-", str(len(arch))]
        with open(p, "a", encoding="utf-8") as f:
            f.write(f"| {report['gen']} | {report['evolving']} | {report['regime']} | {report['runs']} | "
                    f"{report['bad']} | " + " | ".join(cells) + " |\n")
        print("[coevo] " + f"gen {report['gen']} done: vessel fit/div/front {cells[0]}/{cells[1]}/{cells[2]}, "
              f"fauna {cells[3]}/{cells[4]}/{cells[5]}", flush=True)

    def spawn_farm(self):
        a = self.a
        cmd = [sys.executable, str(Path(__file__).resolve().parent / "farm.py"), "run", "--root", str(self.farm.root),
               "-n", str(a.farm_n), "--mode", a.farm_mode]
        if a.exe:
            cmd += ["--exe", a.exe]
        log = open(self.dir / "farm.out", "a", encoding="utf-8")
        self.farm_proc = subprocess.Popen(cmd, stdout=log, stderr=subprocess.STDOUT)
        print(f"[coevo] farm spawned (pid {self.farm_proc.pid}), output in {self.dir / 'farm.out'}", flush=True)

    def run(self):
        if self.a.spawn_farm and not self.fake:
            self.spawn_farm()
        try:
            while self.s["gen"] < self.a.generations:
                self.step()
        finally:
            if self.farm_proc and self.farm_proc.poll() is None:
                self.farm.stop_file.touch()
                try:
                    self.farm_proc.wait(600)
                except subprocess.TimeoutExpired:
                    self.farm_proc.kill()
        print(f"[coevo] {self.a.session}: {self.s['gen']} generations; see {self.dir}", flush=True)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", default=str(DEFAULT_ROOT))
    ap.add_argument("--session", required=True)
    ap.add_argument("--space", default=None, help="genome space JSON (ignored on resume: the session keeps its own)")
    ap.add_argument("--generations", type=int, default=20, help="total generations (resume continues to this)")
    ap.add_argument("--squad", default="Sparrow,Rhino,Manta", help="squad when the space has no __squad gene")
    ap.add_argument("--difficulties", default="2,4,6,8")
    ap.add_argument("--pop-vessel", type=int, default=12)
    ap.add_argument("--pop-fauna", type=int, default=8)
    ap.add_argument("--opponents-vessel", type=int, default=3, help="fauna each vessel meets in a vessel generation")
    ap.add_argument("--opponents-fauna", type=int, default=4, help="vessels each fauna meets in a fauna generation")
    ap.add_argument("--runs-per-pairing", type=int, default=4,
                    help="runs per job (difficulties cycled); 4 keeps the ~40 s player boot small")
    ap.add_argument("--memory", type=int, default=40, help="a genome is scored over its last N runs")
    ap.add_argument("--archive-cap", type=int, default=16)
    ap.add_argument("--elites", type=int, default=4, help="hall-of-fame elites kept from earlier generations")
    ap.add_argument("--regime", choices=("alternate", "patience"), default="alternate")
    ap.add_argument("--patience", type=int, default=3)
    ap.add_argument("--floor-margin", type=float, default=0.1, help="soft floor: peak fit minus this")
    ap.add_argument("--floor-beta", type=float, default=4.0)
    ap.add_argument("--parsimony", action="store_true", help="add -complexity as a third objective")
    ap.add_argument("--sigma", type=float, default=0.12, help="mutation sd, in fractions of each gene's range")
    ap.add_argument("--init-sigma", type=float, default=0.25)
    ap.add_argument("--rate", type=float, default=0.3, help="per-gene mutation probability")
    ap.add_argument("--flip", type=float, default=0.1, help="categorical flip probability (x3 when touched)")
    ap.add_argument("--crossover", type=float, default=0.3)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--poll", type=float, default=10)
    ap.add_argument("--idle-runs", type=int, default=1, help="runs per fauna against the idle escort (fauna gens)")
    ap.add_argument("--clusters", type=int, default=3, help="strategy clusters for the fauna's diversity")
    ap.add_argument("--fake", action="store_true", help="synthetic evaluator, no farm")
    ap.add_argument("--spawn-farm", action="store_true", help="start farm.py run for the session's lifetime")
    ap.add_argument("--farm-n", type=int, default=4)
    ap.add_argument("--farm-mode", default="nographics")
    ap.add_argument("--exe", default=None)
    a = ap.parse_args()
    Driver(a).run()


if __name__ == "__main__":
    main()
