#!/usr/bin/env python3
"""The case for AI_PLAN.md decision 7 on the Manta: Redline raced at several AI nose rates, the best Soar bands at each.

In Redline's model (manta_redline_sim.py) the fleet's AI nose lag (VesselTransformer.LERP_AMOUNT 1.5/s) makes the AI
Manta 1.7x (I1) to 5x (I4) slower than one without it, more than any Soar band buys (REDLINE.md section 5.1).
Switchback already sets an AI's nose rate per intensity and difficulty (SwitchbackController.aiNoseFollowByIntensity,
SWITCHBACK.md "Lag by difficulty"). This sweeps that knob the same way for Redline: for each nose rate and intensity it
races a grid of Soar bands (MantaSoarAITuningSO's BoostStickBand x YawsteryStickBand, plus every Manta genome) and reports
the best pair, its finished races, median time and missed mouths, against the shipped bands at the same rate.

The score is redline_fit's per-intensity score (time over the flat-out path, + 0.5 x the worst race; a DNF always
scores worse than a finish), so a row reads the same as the compute job. A model, never the editor: see
manta_redline_sim.LIMITS. The sweep is a READER: it writes nothing but its own JSON (--out).

    python3 Tools/Build/manta_redline_nose_sweep.py                       # the table, 6 courses an intensity
    python3 Tools/Build/manta_redline_nose_sweep.py --seeds 6 --seedbase 1001 --out sweep.json --cores 4
    python3 Tools/Build/manta_redline_nose_sweep.py --rates 1.5,3,0 --boost 0.35 --yawstery 0.75   # one band pair
    python3 Tools/Build/manta_redline_nose_sweep.py --self-test
"""
import json
import os
import sys
from concurrent.futures import ProcessPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "Compute", "evaluators"))
import manta_redline_sim as sim   # noqa: E402

RATES = (1.5, 2.0, 3.0, 4.0, 6.0, 9.0, 0.0)          # 1.5 = the fleet lag, 0 = none (a human's nose: no lag)
BOOST = (0.0, 0.1, 0.2, 0.35, 0.5, 0.75, 1.0)
YAWSTERY = (0.5, 0.65, 0.75, 0.85, 1.0)
GENOMES = ((0.35, 0.75), (0.975, 0.975))             # the executor's shipped bands; MantaSoarAITuning_Regatta
LIMIT = 900.0
FLAT_OUT = 720.0                                       # redline_fit.FLAT_OUT, RedlineCourse.MantaTopSpeed
WORST = 0.5                                            # redline_fit.COMBINE_WORST


def rate_label(r):
    return "none" if r == 0 else ("fleet 1.5" if r == 1.5 else f"{r:g}")


def score(rows, limit=LIMIT):
    """redline_fit.score, inlined so this reader needs no compute context."""
    v = [r["time"] / r["flat"] if r["time"] is not None else limit / r["flat"] + 2.0 * (r["of"] - r["rings"]) / max(1, r["of"])
         for r in rows]
    return sum(v) / len(v) + WORST * max(v)


_COURSES = {}


def _course(i, s):
    k = (i, s)
    if k not in _COURSES:
        c = sim.course_for(i, s)
        _COURSES[k] = (c, sum(c["legLengths"]) * sim.LAPS / FLAT_OUT)
    return _COURSES[k]


def cell(job):
    """One (rate, intensity, bands) over the courses: the rows the table is built from."""
    rate, i, bands, seeds = job
    rows = []
    for s in seeds:
        c, flat = _course(i, s)
        r = sim.race(c, i, bands=bands, nose_rate=rate, limit=LIMIT)
        r["flat"] = flat
        r["seed"] = s
        rows.append(r)
    fin = sorted(r["time"] for r in rows if r["time"] is not None)
    return {"rate": rate, "intensity": i, "bands": list(bands), "score": round(score(rows), 4),
            "finished": len(fin), "of": len(rows), "median_s": round(fin[len(fin) // 2], 1) if fin else None,
            "worst_s": round(max(fin), 1) if len(fin) == len(rows) else None,
            "misses": sum(r["misses"] for r in rows), "breakoffs": sum(r["breakoffs"] for r in rows),
            "soar": round(sum(r["soar"] for r in rows) / len(rows), 3)}


def sweep(rates, intensities, pairs, seeds, cores):
    jobs = [(r, i, p, tuple(seeds)) for r in rates for i in intensities for p in pairs]
    if cores > 1:
        with ProcessPoolExecutor(cores) as ex:
            return list(ex.map(cell, jobs, chunksize=2))
    return [cell(j) for j in jobs]


def best_by(cells, shipped=GENOMES[0]):
    """{(rate, intensity): {"best": cell, "shipped": cell}}: the best pair by score (a full finish always wins)."""
    out = {}
    for c in cells:
        k = (c["rate"], c["intensity"])
        slot = out.setdefault(k, {"best": None, "shipped": None})
        if slot["best"] is None or c["score"] < slot["best"]["score"]:
            slot["best"] = c
        if tuple(c["bands"]) == tuple(shipped):
            slot["shipped"] = c
    return out


def render(best, rates, intensities):
    lines = ["| nose rate /s | " + " | ".join(f"I{i} shipped bands" for i in intensities) + " | "
             + " | ".join(f"I{i} best bands" for i in intensities) + " |",
             "|---|" + "---:|" * (2 * len(intensities))]
    fmt = lambda c: ("-" if c is None else
                     f"{c['median_s'] if c['median_s'] is not None else '-'} s ({c['finished']}/{c['of']}, {c['misses']} miss)")
    fmtb = lambda c: fmt(c) + f" @ {c['bands'][0]:g}/{c['bands'][1]:g}"
    for r in rates:
        row = [fmt(best[(r, i)]["shipped"]) for i in intensities] + [fmtb(best[(r, i)]["best"]) for i in intensities]
        lines.append(f"| {rate_label(r)} | " + " | ".join(row) + " |")
    return "\n".join(lines)


def self_test():
    bad = []
    seeds = (1,)
    a = cell((1.5, 1, GENOMES[0], seeds))
    b = cell((None, 1, GENOMES[0], seeds))
    if a != dict(b, rate=1.5):
        bad.append("rate 1.5 is not the fleet lag's race")
    if a["finished"] != 1:
        bad.append(f"the shipped AI did not finish I1 course 1 at the fleet lag: {a}")
    none = cell((0.0, 1, GENOMES[0], seeds))
    if not (none["median_s"] and none["median_s"] < a["median_s"]):
        bad.append(f"no nose lag was not faster at I1 ({none['median_s']} vs {a['median_s']})")
    never = cell((1.5, 1, (0.0, 1.0), seeds))
    if not never["score"] > a["score"]:
        bad.append("negative control: a Manta that never Soars did not score worse")
    best = best_by([a, never])
    if best[(1.5, 1)]["best"] is not a or best[(1.5, 1)]["shipped"] is not a:
        bad.append("best_by did not pick the lower score and the shipped pair")
    if "1.5" not in render(best, [1.5], [1]):
        bad.append("render lost the rate row")
    print("manta_redline_nose_sweep self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
    return 0 if not bad else 1


def _arg(argv, name, default, conv):
    return conv(argv[argv.index(name) + 1]) if name in argv else default


def main(argv):
    if "--self-test" in argv:
        return self_test()
    floats = lambda s: tuple(float(x) for x in s.split(","))
    rates = _arg(argv, "--rates", RATES, floats)
    intensities = _arg(argv, "--intensities", (1, 2, 3, 4), lambda s: tuple(int(x) for x in s.split(",")))
    n, base = _arg(argv, "--seeds", 6, int), _arg(argv, "--seedbase", 1, int)
    boost, yawstery = _arg(argv, "--boost", BOOST, floats), _arg(argv, "--yawstery", YAWSTERY, floats)
    pairs = sorted(set([(b, y) for b in boost for y in yawstery] + list(GENOMES)))
    if "--boost" in argv and "--yawstery" in argv:
        pairs = [(b, y) for b in boost for y in yawstery]
    cores = _arg(argv, "--cores", os.cpu_count() or 1, int)
    seeds = [base + k for k in range(n)]
    cells = sweep(rates, intensities, pairs, seeds, cores)
    best = best_by(cells)
    print(f"Redline offline (a model: {sim.LIMITS}); {n} courses an intensity from seed {base}, {len(pairs)} band pairs")
    print(render(best, rates, intensities))
    out = _arg(argv, "--out", None, str)
    if out:
        with open(out, "w", encoding="utf-8") as fh:
            json.dump({"seeds": seeds, "rates": rates, "pairs": pairs, "limits": sim.LIMITS, "cells": cells,
                       "best": [{"rate": r, "intensity": i, **v} for (r, i), v in best.items()]}, fh, indent=1)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
