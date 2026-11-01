"""The people's seed: the genes a Unity trainer run starts from, measured from the people's own recorded races.

    python3 Tools/Compute/people_seed.py --mode SkimRace --vessel Squirrel --out seed.json [--token HexRace] [--no-fetch]
    python3 Tools/Compute/people_seed.py --self-test

This is where the two learning paths join (Shombith, 2026-11-01: "join the two learning paths"). The hourly AI learning
sweep (Yash's, Docs/AI_SYSTEM/AUTO_LEARNING.md) learns from every race a person plays in the editor; the Unity trainer
(a Froglet Compute run, README section 16) breeds its own AI population from all-AI matches. Until now the trainer
started every fresh population from the gene registry's defaults, so what people had taught the game was not in the
run. Now `unity_run.py` calls this before a run: it reads the same flight records the sweep learns from (the
`bugledger/<user>` branches, `learn_from_flights.flights_on_branches`, which leaves out every flight the AI flew), keeps
the ones of this pair, and measures the gene-shaped quantities a person's flying shows:

    throttle.base        the throttle they hold when not boosting (median)
    boost.min_charge     the charge they wait for before a boost (median at each press)
    boost.hold_seconds   how long they hold a boost (median press)
    boost.cooldown       how long they wait between boosts (median gap)

The trainer (FrogletComputeTraining.ApplySeed) lays these over the registry defaults gene by gene for the FIRST
population of a fresh scenario; a resumed population keeps what it learned. The seed is a proposal of where to start,
never a rule: the search moves on from it. Every number needs enough evidence (MIN_FLIGHTS races, MIN_EVENTS presses) or
it is left to the default, and the seed says what it measured (`note`, `flights`, `people`). The file is the
trainer's genome.json shape (geneNames / geneValues), so the game reads it with its own importer.
"""
import argparse
import json
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))

MIN_FLIGHTS = 2        # races of this pair before any gene is written
LINE_FLIGHTS = 5       # the people's lines: this many of their best races of the pair (most rings, then longest)
LINE_STEP = 10.0       # units between the points of a resampled line
LINE_POINTS = 600      # points per line at most
MIN_EVENTS = 3         # boost presses before a boost gene is written
MIN_SAMPLES = 200      # throttle samples before throttle.base is written
GENES = {              # gene -> (lo, hi): the registry's bounds, so a measurement never leaves the search space
    "throttle.base": (0.3, 1.0),
    "boost.min_charge": (0.0, 1.0),
    "boost.hold_seconds": (0.2, 3.0),
    "boost.cooldown": (0.5, 6.0),
}


def _col(d, name):
    cols = d.get("columns") or []
    return cols.index(name) if name in cols else -1


def _rows(d):
    return d.get("samples") or []


def this_pair(d, mode, vessel, token=None):
    """A record of this pair: the mode by its GameModes name (or the trainer's token for it) and the hull by name."""
    m = str(d.get("mode") or "")
    h = str(d.get("hull") or d.get("vessel") or "")
    return h.lower() == str(vessel).lower() and (m.lower() == str(mode).lower() or (token and m.lower() == str(token).lower()))


def measure(records):
    """The gene measurements from a list of parsed flight records (dicts with columns and samples), with evidence."""
    throttle, charges, holds, gaps = [], [], [], []
    for d in records:
        it, ib, ith, ich, ihd = (_col(d, n) for n in ("t", "boost", "throttle", "charge", "held"))
        if it < 0 or ib < 0:
            continue
        on, t_on, t_off = False, None, None
        for r in _rows(d):
            if ihd >= 0 and len(r) > ihd and r[ihd]:
                continue                                  # the AI held the hull for this sample: not the person
            boost = bool(r[ib]) if len(r) > ib else False
            if ith >= 0 and len(r) > ith and not boost and isinstance(r[ith], (int, float)):
                throttle.append(float(r[ith]))
            t = float(r[it])
            if boost and not on:
                on, t_on = True, t
                if t_off is not None:
                    gaps.append(t - t_off)
                if ich >= 0 and len(r) > ich and isinstance(r[ich], (int, float)):
                    charges.append(float(r[ich]))
            elif not boost and on:
                on, t_off = False, t
                holds.append(t - t_on)
    out, why = {}, []
    def put(gene, values, need, what):
        if len(values) >= need:
            lo, hi = GENES[gene]
            out[gene] = round(min(hi, max(lo, statistics.median(values))), 4)
            why.append(f"{gene} from {len(values)} {what}")
        else:
            why.append(f"{gene} left to the default ({len(values)} {what}, {need} needed)")
    put("throttle.base", throttle, MIN_SAMPLES, "samples")
    put("boost.min_charge", charges, MIN_EVENTS, "boost presses")
    put("boost.hold_seconds", holds, MIN_EVENTS, "boost presses")
    put("boost.cooldown", gaps, MIN_EVENTS, "boost gaps")
    return out, why


def path_of(d):
    """The person's path in a record: the samples' x y z, the AI-held ones left out."""
    ix, iy, iz, ihd = (_col(d, n) for n in ("x", "y", "z", "held"))
    if min(ix, iy, iz) < 0:
        return []
    out = []
    for r in _rows(d):
        if ihd >= 0 and len(r) > ihd and r[ihd]:
            continue
        if len(r) > max(ix, iy, iz) and all(isinstance(r[i], (int, float)) for i in (ix, iy, iz)):
            out.append((float(r[ix]), float(r[iy]), float(r[iz])))
    return out


def resample(points, step=LINE_STEP, cap=LINE_POINTS):
    """The path every `step` units of its own length, at most `cap` points (the step widens to fit)."""
    if len(points) < 2:
        return []
    length = sum(((b[0] - a[0]) ** 2 + (b[1] - a[1]) ** 2 + (b[2] - a[2]) ** 2) ** 0.5 for a, b in zip(points, points[1:]))
    step = max(step, length / max(1, cap - 1)) if length > 0 else step
    out, carry = [points[0]], 0.0
    for a, b in zip(points, points[1:]):
        seg = ((b[0] - a[0]) ** 2 + (b[1] - a[1]) ** 2 + (b[2] - a[2]) ** 2) ** 0.5
        if seg <= 0:
            continue
        pos = carry
        while pos + step <= seg + 1e-9:
            pos += step
            t = pos / seg
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t))
        carry = pos - seg
    if out[-1] != points[-1]:
        out.append(points[-1])
    return out[:cap]


def lines(records, intensity=None):
    """The people's lines for the fitness term (FitnessComponents.PeopleLineFitness): the LINE_FLIGHTS best races of
    the pair (at this intensity when any; most rings, then longest), each resampled. Returns (flat xyz, starts, n)."""
    def rings(d):
        i = _col(d, "rings")
        rows = _rows(d)
        return float(rows[-1][i]) if i >= 0 and rows and len(rows[-1]) > i and isinstance(rows[-1][i], (int, float)) else 0.0
    pool = [d for d in records if intensity is None or d.get("intensity") in (None, intensity)] or list(records)
    pool.sort(key=lambda d: (-rings(d), -len(_rows(d))))
    flat, starts, n = [], [], 0
    for d in pool[:LINE_FLIGHTS]:
        pts = resample(path_of(d))
        if len(pts) < 2:
            continue
        starts.append(len(flat) // 3)
        for x, y, z in pts:
            flat += [round(x, 2), round(y, 2), round(z, 2)]
        n += 1
    return flat, starts, n


def seed(records, mode, vessel, intensity=None):
    """The seed file's content for these records (already this pair's), or None when there is too little."""
    people = sorted({str(d.get("user") or d.get("person") or "").strip() for d in records} - {""})
    if len(records) < MIN_FLIGHTS:
        return None
    genes, why = measure(records)
    flat, starts, n = lines(records, intensity)
    if not genes and not n:
        return None
    return {"geneNames": list(genes), "geneValues": [genes[k] for k in genes], "enabledModules": [],
            "GenerationBorn": 0, "EvaluationCount": 0, "Fitness": 0.0, "NoveltyScore": 0.0, "Lineage": "people-seed",
            "schema": "froglet-compute/people-seed/2", "mode": mode, "vessel": vessel, "flights": len(records),
            "people": people, "measured": why, "linePoints": flat, "lineStarts": starts, "lineFlights": n,
            "note": f"{len(records)} race(s) by {', '.join(people) if people else 'the team'}: " + (", ".join(k for k in genes) or "no gene")
                    + (f"; {n} line(s), {len(flat) // 3} points" if n else "; no line")}


def records_for(mode, vessel, token=None, fetch=True):
    import learn_from_flights as F
    if fetch:
        F.git("fetch", "-q", "origin", "+refs/heads/bugledger/*:refs/remotes/origin/bugledger/*")
    out = []
    for name, text in F.flights_on_branches():
        d, err = F.parse(name, text)
        if d is None or not this_pair(d, mode, vessel, token):
            continue
        d.setdefault("user", name.split("/reports/")[1].split("/")[0] if "/reports/" in name else "")
        out.append(d)
    return out


def self_test():
    bad = []
    cols = ["t", "boost", "throttle", "charge", "held"]
    def flight(mode="SkimRace", hull="Squirrel", presses=4, held=False, n=400):
        rows, t = [], 0.0
        for i in range(n):
            b = 1 if (i // 40) % 2 == 1 and (i // 40) // 2 < presses else 0       # a 2 s press every 4 s
            rows.append([t, b, 0.8 if not b else 1.0, 0.6 if b else 0.1, 1 if held else 0])
            t += 0.05
        return {"mode": mode, "hull": hull, "columns": cols, "samples": rows, "user": "shombith"}
    s = seed([flight(), flight()], "SkimRace", "Squirrel")
    if not s or "throttle.base" not in s["geneNames"] or "boost.min_charge" not in s["geneNames"]:
        bad.append(f"two races did not seed the throttle and boost genes: {s}")
    else:
        g = dict(zip(s["geneNames"], s["geneValues"]))
        if abs(g["throttle.base"] - 0.8) > 0.01 or abs(g["boost.min_charge"] - 0.6) > 0.01 or abs(g["boost.hold_seconds"] - 2.0) > 0.2:
            bad.append(f"the measurements are off: {g}")
        if s["flights"] != 2 or s["people"] != ["shombith"] or "2 race(s)" not in s["note"]:
            bad.append(f"the provenance is wrong: {s['note']} {s['people']}")
    if seed([flight()], "SkimRace", "Squirrel") is not None:
        bad.append("one race seeded (MIN_FLIGHTS)")
    if not this_pair(flight(), "SkimRace", "Squirrel") or this_pair(flight(), "Regatta", "Squirrel") or this_pair(flight(), "SkimRace", "Rhino"):
        bad.append("the pair filter is wrong")
    if not this_pair(flight(mode="HexRace"), "SkimRace", "Squirrel", token="HexRace"):
        bad.append("the trainer's token for a mode is not accepted")
    held = seed([flight(held=True), flight(held=True)], "SkimRace", "Squirrel")
    if held is not None:
        bad.append("samples the AI held were measured as the person's")
    few = seed([flight(presses=1), flight(presses=1)], "SkimRace", "Squirrel")
    if few and "boost.min_charge" in few["geneNames"]:
        bad.append("two presses wrote a boost gene (MIN_EVENTS)")
    # the people's lines: a straight 1000-unit race resamples to 101 points every 10 units; the AI-held tail is left
    # out; the best races (rings, then length) are taken, at the requested intensity first
    lc = ["t", "x", "y", "z", "rings", "held", "intensity"]
    def race(n=201, rings_at_end=5, held_tail=0, intensity=4):
        rows = [[i * 0.1, i * 5.0, 0.0, 0.0, rings_at_end if i == n - 1 else 0, 1 if i >= n - held_tail else 0] for i in range(n)]
        return {"mode": "SkimRace", "hull": "Squirrel", "columns": lc[:6], "samples": rows, "intensity": intensity}
    flat, starts, n = lines([race()], 4)
    if n != 1 or len(flat) // 3 != 101 or starts != [0] or flat[:3] != [0.0, 0.0, 0.0] or flat[-3:] != [1000.0, 0.0, 0.0]:
        bad.append(f"a straight race did not resample to 101 points: {n} {len(flat) // 3} {starts} {flat[:3]} {flat[-3:]}")
    flat2, _, _ = lines([race(held_tail=50)], 4)
    if flat2[-3] >= 760:
        bad.append("the AI-held tail of a race went into the line")
    many = [race(rings_at_end=1, intensity=4)] * 3 + [race(rings_at_end=9, intensity=2, n=41)]
    flat3, starts3, n3 = lines(many, 2)
    if n3 != 1 or len(flat3) // 3 != 21:
        bad.append(f"the requested intensity was not preferred: {n3} line(s), {len(flat3) // 3} points")
    flat4, starts4, n4 = lines([race()] * 7, 4)
    if n4 != LINE_FLIGHTS or len(starts4) != LINE_FLIGHTS or starts4[1] != 101:
        bad.append(f"more than LINE_FLIGHTS lines, or wrong starts: {n4} {starts4[:3]}")
    s2 = seed([flight(), flight()], "SkimRace", "Squirrel", 4)
    if not s2 or "lineFlights" not in s2 or s2["lineFlights"] != 0 or "no line" not in s2["note"]:
        bad.append(f"a seed without positions did not say 'no line': {s2 and s2['note']}")
    big = [dict(race(), columns=lc[:6]), dict(race(), columns=lc[:6])]
    s3 = seed(big, "SkimRace", "Squirrel", 4)
    if not s3 or s3["lineFlights"] != 2 or "2 line(s)" not in s3["note"] or s3["geneNames"]:
        bad.append(f"races with positions but no boost column did not seed the line alone: {s3 and s3['note']}")
    return bad


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--mode", help="the GameModes name")
    ap.add_argument("--vessel")
    ap.add_argument("--token", help="the trainer's token for the mode, when it differs (HexRace for SkimRace)")
    ap.add_argument("--intensity", type=int, help="prefer races at this intensity for the people's lines")
    ap.add_argument("--out")
    ap.add_argument("--no-fetch", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args(argv)
    if a.self_test:
        bad = self_test()
        print("people_seed self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
        return 1 if bad else 0
    if not (a.mode and a.vessel):
        ap.error("--mode and --vessel")
    recs = records_for(a.mode, a.vessel, a.token, fetch=not a.no_fetch)
    s = seed(recs, a.mode, a.vessel, a.intensity)
    if s is None:
        print(f"no seed: {len(recs)} race(s) of {a.vessel} in {a.mode} by people ({MIN_FLIGHTS} needed, with enough boosts)")
        return 2
    if a.out:
        with open(a.out, "w", encoding="utf-8") as fh:
            json.dump(s, fh, indent=1)
        print(f"seed: {s['note']} -> {a.out}")
    else:
        print(json.dumps(s, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
