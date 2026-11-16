#!/usr/bin/env python3
"""The Regatta race judge's MODEL backend: a course genome raced by every seat's offline model (Plan step: time-arena-course).

The shared contract (Regatta flywheel round 1, /mnt/project-files/regatta-flywheel/BRIEFS.md): a judge takes a course
GENOME and the seats to race, and returns what each seat did. The real judge is the game's own code in Amoebius, headless
(`Tools/Compute/evaluators/regatta_judge.py`, backend "engine", worker W1). This module is its stand-in until that lands,
and stays as `backend="model"` after, so the search can be run and checked without an engine:

    judge(genome, seats, seeds, limit_s=400) -> {"races": [{"seed", "field", "seats": [{"hull", "finish_s" | None, "laps",
                                                  "lap_s", "prism_hits", "dnf_reason"}]}],
                                                 "wall_s", "engine_commit": "model", "backend": "model"}

GENOME (the contract's): {"version", "intensity", "seed", "circuit": {RegattaCourse settings}, "features":
{RegattaCourseFeatures}, "placements": [...], "flora": [...], "crystals": {...}}. The model sees `circuit`, `seed` and
`features` only; `placements` and `flora` are INVISIBLE to it (W2's genome adds them; only the engine judge can price
them), and `metrics_note` in the result says so whenever a genome carries any.

HOW A SEAT IS PRICED: regatta_features.race on the laid course with the hull's roster row (the measured roster of the
lead-on pool job, `Docs/Compute/jobs/regatta-pool-lead.json`: every hull anchored at its 2026-11-03 editor race on the
shipped I1 course and moved by the line / lap / drive models, the Urchin on its rail model). The models are
deterministic, so every seed gives the same time: race-to-race noise is NOT measured by this backend, and a field race
(several seats in one race) is priced as each seat alone (the models have no contact). Every number is a MODEL.

    python3 Tools/Compute/evaluators/regatta_judge_model.py --self-test
    python3 Tools/Compute/evaluators/regatta_judge_model.py GENOME.json [--hulls Urchin Manta ...]
"""
import copy
import json
import os
import sys
import time

_HERE = os.path.dirname(os.path.abspath(__file__))
_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(_HERE)))
for _p in (_HERE, os.path.join(_ROOT, "Tools", "Build")):
    if _p not in sys.path:
        sys.path.insert(0, _p)

GENOME_VERSION = 1
LAPS = 3
ROSTER_JOB = os.path.join(_ROOT, "Docs", "Compute", "jobs", "regatta-pool-lead.json")
HULLS = ("Dolphin", "Manta", "Rhino", "Serpent", "Sparrow", "Squirrel", "Stoat", "Urchin")
_CACHE = {}


def _mr():
    import regatta_measured_roster as mr
    return mr


def today_genome():
    """The empty genome on the course the roster was measured on (regatta_measured_roster.MEASURED_ON and its seed):
    no features, no placements. It lays that course prism for prism."""
    mr = _mr()
    circuit = {k: (list(v) if isinstance(v, (list, tuple)) else v) for k, v in mr.MEASURED_ON.items()}
    return {"version": GENOME_VERSION, "intensity": 1, "seed": int(mr.MEASURED_SEED), "circuit": circuit,
            "features": {"Sections": [], "AlternateGates": [], "Crystals": {"PerLap": 0, "Phase": 0.0}},
            "placements": [], "flora": [], "crystals": {}}


def lay(genome):
    """The genome's course as regatta_features lays it (the C#-gated port). Raises when the generator finds no circuit."""
    import regatta_features as rf
    c = dict(genome["circuit"])
    if isinstance(c.get("FirstGateDirection"), list):
        c["FirstGateDirection"] = tuple(c["FirstGateDirection"])
    return rf.course(settings=c, seed=int(genome["seed"]), features=copy.deepcopy(genome.get("features")) or None)


def roster(job=ROSTER_JOB):
    """{hull: roster row ready to race} from the pool job's evaluator roster (regatta_pool.roster_of)."""
    key = os.path.abspath(job)
    if key not in _CACHE:
        import regatta_pool as rp
        args = json.load(open(job, encoding="utf-8"))["evaluator"]["args"]
        _CACHE[key] = rp.roster_of(args, _ROOT)
    return _CACHE[key]


def race_seat(course, hull, limit_s=400.0, job=ROSTER_JOB):
    """One hull alone on a laid course: (seconds | None, raw)."""
    import regatta_features as rf
    h = roster(job)[hull]
    d = dict(h.get("drive") or {})
    knobs = {"urchin_free": float(d["free_speed"])} if h["model"] == "rail" and "free_speed" in d else None
    t, raw = rf.race(hull, h["model"], course, 1, drive=d, limit=limit_s, knobs=knobs, laps=LAPS, **h.get("nose", {}))
    if t is None or raw.get("time") is None or t > limit_s:
        return None, raw
    return float(t), raw


def judge_course(course, seats, seeds, limit_s=400.0, job=ROSTER_JOB, field=False):
    """judge() on an already-laid course (the search lays once and reuses it)."""
    t0 = time.time()
    priced = {}
    for s in seats:
        hull = s["hull"]
        if hull not in priced:
            priced[hull] = race_seat(course, hull, limit_s, job) if hull in roster(job) else (None, {"why": "no roster row"})
    races = []
    for seed in seeds:
        out = []
        for s in seats:
            t, raw = priced[s["hull"]]
            out.append({"hull": s["hull"], "ai": s.get("ai", "shipped"),
                        "finish_s": None if t is None else round(t, 3), "laps": LAPS if t is not None else 0,
                        "lap_s": [] if t is None else [round(t / LAPS, 3)] * LAPS, "prism_hits": 0,
                        "dnf_reason": None if t is not None else (raw.get("why") or "limit")})
        races.append({"seed": int(seed), "field": bool(field), "seats": out})
    return {"races": races, "wall_s": round(time.time() - t0, 3), "engine_commit": "model", "backend": "model"}


def judge(genome, seats, seeds=(0,), limit_s=400.0, job=ROSTER_JOB, field=False):
    """The contract's judge on the model backend. `field`=True asks for one race with every seat together (the model
    prices it as each seat alone)."""
    course = lay(genome)
    out = judge_course(course, seats, seeds, limit_s, job, field)
    if genome.get("placements") or genome.get("flora"):
        out["metrics_note"] = "the model cannot see placements or flora: these times ignore them (engine judge only)"
    return out


def self_test():
    bad = []
    mr = _mr()
    g = today_genome()
    seats = [{"hull": h, "ai": "shipped"} for h in ("Manta", "Squirrel", "Stoat", "Urchin")]
    r = judge(g, seats, seeds=[1, 2])
    if len(r["races"]) != 2 or len(r["races"][0]["seats"]) != 4:
        bad.append("regatta_judge_model: the result does not have one race per seed and one row per seat")
    got = {s["hull"]: s["finish_s"] for s in r["races"][0]["seats"]}
    for h in ("Manta", "Squirrel", "Stoat"):
        if got[h] is None or abs(got[h] - mr.MEASURED[h]) > 2.0:
            bad.append(f"regatta_judge_model: today's {h} is {got[h]} s, the roster measured {mr.MEASURED[h]}")
    if got["Urchin"] is None or abs(got["Urchin"] - mr.URCHIN_MEASURED[0.0]) > 3.0:
        bad.append(f"regatta_judge_model: today's Urchin is {got['Urchin']} s, measured {mr.URCHIN_MEASURED[0.0]}")
    # Negative control: half of every leg in no team's colour must slow the rail rider (the Urchin) and nobody else much.
    g2 = copy.deepcopy(g)
    g2["features"]["Sections"].append({"Leg": -1, "From": 0.25, "To": 0.75, "Kind": 0, "Neutral": True, "Detour": 0.0,
                                        "DetourBearing": 0.0})
    got2 = {s["hull"]: s["finish_s"] for s in judge(g2, seats)["races"][0]["seats"]}
    if not (got2["Urchin"] > got["Urchin"] * 1.2 and abs(got2["Manta"] - got["Manta"]) < 0.05 * got["Manta"]):
        bad.append(f"regatta_judge_model: a neutral half-lap does not slow only the Urchin ({got} -> {got2})")
    g3 = copy.deepcopy(g)
    g3["placements"] = [{"kind": "anything"}]
    if "metrics_note" not in judge(g3, seats[:1]):
        bad.append("regatta_judge_model: a genome with placements is priced without saying the model cannot see them")
    return bad


def main():
    import argparse
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("genome", nargs="?")
    ap.add_argument("--hulls", nargs="*", default=list(HULLS))
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test or not a.genome:
        problems = self_test()
        print("\n".join(problems) or "regatta_judge_model --self-test: ok")
        sys.exit(1 if problems else 0)
    g = json.load(open(a.genome, encoding="utf-8"))
    print(json.dumps(judge(g, [{"hull": h} for h in a.hulls]), indent=1))


if __name__ == "__main__":
    main()
