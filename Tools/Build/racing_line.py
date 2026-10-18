#!/usr/bin/env python3
"""racing_line.py - the best way round a Regatta course, averaged from a pilot's own clean flights.

Yash, 2026-10-16: "take the average of the datas and understand how the path can be navigated; sometimes even I make
mistakes, hit things, so you have to ignore those datas. Maybe I can give you 100 gameplays and you can figure out the
best way to create the AI."

For every leg of the course (from one ring to the next; the race start is its own leg), it takes each flight's leg,
throws away the ones with a mistake (a rail hit, a missed mouth, the AI holding the hull: situation_features.py's
`clean`), keeps the faster half of what is left, resamples each kept leg by distance along it, and averages them into
ONE line per leg: where to fly, how fast, where you boost, drift and skim, and how far apart your clean runs were (the
spread: a narrow spread is a line you have learned, a wide one is a leg you have not settled yet). It also reports:
- the IDEAL lap: your fastest clean time on every leg added up (what you could do on one perfect lap);
- where your mistakes happen (rail hits and misses per leg), so the AI knows the dangerous legs;
- how many clean legs each leg still needs before its line can be trusted (MIN_CLEAN).

Nothing here changes the game. `--write` saves the line to Docs/AI_SYSTEM/racing_lines/ for review; the AI only flies it
after the reviewed Unity port (AI_TRAINING_PLAN.md L4). Definitions: Docs/AI_SYSTEM/RACING_LINE.md.

    python3 Tools/Build/racing_line.py --dir <folder of flight_*.json>      # dry run: the report only
    python3 Tools/Build/racing_line.py --dir <folder> --write               # also save the line (JSON) and the report
    python3 Tools/Build/racing_line.py --self-test
"""
import argparse
import glob
import json
import math
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
import situation_features as sf  # noqa: E402

OUT_DIR = os.path.join(REPO, "Docs", "AI_SYSTEM", "racing_lines")
POINTS = 32          # resampled points per leg
MIN_CLEAN = 5        # clean legs before a leg's line is trusted
KEEP_SHARE = 0.5     # the faster half of the clean legs make the line
STATE_BOOST, STATE_DRIFT, STATE_SLOWED = 1, 4, 8
# A leg's STYLE is read from what the recorder already writes, so the pilot never labels anything (Yash, 2026-10-16:
# "I cannot type commands and fly at the same time; record rail riding, drifting, abilities, hard AIs automatically"):
RAIL_SHARE = 0.4     # rail: riding the pilot's OWN rail for this share of the leg...
RAIL_NEAR = 20.0     # ...within this many u of one of its prisms (people skim ~14 u from its centre: |SkimHeight| + lift)
DRIFT_SHARE = 0.2    # drift: the Drifting state bit on for this share of the leg
ABILITY_EVENTS = ("Button1Action", "Button2Action", "Button3Action")   # ability buttons pressed during the leg
STYLES = ("plain", "rail", "drift", "ability", "boost")


def resample(pts, vals, n=POINTS):
    """Points (and per-point values) at n even steps of distance along the polyline."""
    d = [0.0]
    for a, b in zip(pts, pts[1:]):
        d.append(d[-1] + math.dist(a, b))
    total = d[-1]
    if total <= 1e-6:
        return [pts[0]] * n, [[v[0]] * n for v in vals], 0.0
    out_p, out_v, j = [], [[] for _ in vals], 0
    for k in range(n):
        s = total * k / (n - 1)
        while j < len(d) - 2 and d[j + 1] < s:
            j += 1
        span = d[j + 1] - d[j]
        u = 0.0 if span <= 1e-9 else (s - d[j]) / span
        out_p.append([pts[j][c] + (pts[j + 1][c] - pts[j][c]) * u for c in range(3)])
        for vi, v in enumerate(vals):
            out_v[vi].append(v[j] + (v[j + 1] - v[j]) * u)
    return out_p, out_v, total


def own_rail_near(f, pts):
    """For each point: is it within RAIL_NEAR of a prism of the pilot's own rail, as the recorder copied it (flight/3)?
    The flight's own rails, not the generated course: they are what was really there."""
    rails, own = (f or {}).get("rails") or [], (f or {}).get("ownRail", -1)
    if not (0 <= own < len(rails)):
        return None
    cell, grid = RAIL_NEAR, {}
    for q in rails[own]["prisms"]:
        grid.setdefault((int(q[0] // cell), int(q[1] // cell), int(q[2] // cell)), []).append(q[:3])
    out = []
    for p in pts:
        cx, cy, cz = int(p[0] // cell), int(p[1] // cell), int(p[2] // cell)
        out.append(any(math.dist(p, q) <= RAIL_NEAR for dx in (-1, 0, 1) for dy in (-1, 0, 1) for dz in (-1, 0, 1)
                       for q in grid.get((cx + dx, cy + dy, cz + dz), ())))
    return out


def leg_runs(A, flight_name):
    """Every leg of one analysed flight, with its resampled path and the pilot's actions along it."""
    N, ev = A["norm"], A["events"]
    state = None
    f = A.get("flight")
    if f and "state" in (f.get("columns") or []):
        k = f["columns"].index("state")
        state = [int(r[k] or 0) for r in f["samples"]]
    skims = set(ev.get("skim", []))
    near = own_rail_near(f, N["p"])
    presses = []   # (t, name) of the pilot's own presses (flight/3 buttonEvents, 2026-10-16 on)
    if f and f.get("buttonEvents"):
        names = {v: k for k, v in (f.get("buttonNames") or {}).items()}
        presses = [(e[0], names.get(int(e[1]), str(e[1]))) for e in f["buttonEvents"] if int(e[2]) == 1]
    rivals = (f or {}).get("rivalDifficulty") or "unknown"
    runs = []
    for n, g in enumerate(A["legs"]):
        i0, i1 = g["i0"], g["i1"]
        if i1 - i0 < 2:
            continue
        idx = list(range(i0, i1 + 1))
        pts = [N["p"][i] for i in idx]
        spd = [N["spd"][i] for i in idx]
        boost = [1.0 if (state and state[i] & STATE_BOOST) or N["lt"][i] > 0.5 else 0.0 for i in idx]
        drift = [1.0 if state and state[i] & STATE_DRIFT else 0.0 for i in idx]
        skim = [1.0 if i in skims else 0.0 for i in idx]
        rp, (rs, rb, rd, rk), length = resample(pts, [spd, boost, drift, skim])
        key = "start" if n == 0 and i0 == 0 else str(g["ring"])
        t0, t1 = N["t"][i0], N["t"][i1]
        on_rail = sum(1 for i in idx if near[i]) / len(idx) if near else 0.0
        pressed = [name for (t, name) in presses if t0 <= t < t1]
        styles = [s for s, on in (("rail", on_rail >= RAIL_SHARE), ("drift", sum(drift) / len(drift) >= DRIFT_SHARE),
                                  ("ability", any(p in ABILITY_EVENTS for p in pressed)),
                                  ("boost", any(N["lt"][i] > 0.5 for i in idx))) if on] or ["plain"]
        runs.append({"flight": flight_name, "key": key, "ring": g["ring"], "time": g["time"], "clean": g["clean"],
                     "rams": g["rams"], "missed": g["missed"], "length": length, "path": rp, "speed": rs,
                     "boost": sum(boost) / len(boost), "drift": sum(drift) / len(drift), "skims": sum(skim),
                     "boostAt": rb, "driftAt": rd, "styles": styles, "onRail": on_rail, "pressed": pressed, "rivals": rivals})
    return runs


def average_line(runs):
    n = len(runs)
    path = [[sum(r["path"][k][c] for r in runs) / n for c in range(3)] for k in range(POINTS)]
    speed = [sum(r["speed"][k] for r in runs) / n for k in range(POINTS)]
    spread = [statistics.pstdev([math.dist(r["path"][k], path[k]) for r in runs]) if n > 1 else 0.0 for k in range(POINTS)]
    off = [sum(math.dist(r["path"][k], path[k]) for r in runs) / n for k in range(POINTS)]
    boost_at = [sum(r["boostAt"][k] for r in runs) / n for k in range(POINTS)]
    drift_at = [sum(r["driftAt"][k] for r in runs) / n for k in range(POINTS)]
    return {"path": [[round(v, 2) for v in p] for p in path], "speed": [round(v, 1) for v in speed],
            "offset": [round(v, 2) for v in off], "spread": [round(v, 2) for v in spread],
            "boostShare": [round(v, 2) for v in boost_at], "driftShare": [round(v, 2) for v in drift_at]}


def build(runs):
    keys = sorted({r["key"] for r in runs}, key=lambda k: (k != "start", int(k) if k.isdigit() else -1))
    legs = []
    for key in keys:
        mine = [r for r in runs if r["key"] == key]
        clean = sorted([r for r in mine if r["clean"]], key=lambda r: r["time"])
        kept = clean[:max(1, math.ceil(len(clean) * KEEP_SHARE))] if clean else []
        leg = {"key": key, "ring": mine[0]["ring"], "runs": len(mine), "clean": len(clean), "kept": len(kept),
               "trusted": len(clean) >= MIN_CLEAN, "railHits": sum(r["rams"] for r in mine),
               "misses": sum(1 for r in mine if r["missed"]),
               "hitShare": round(sum(1 for r in mine if r["rams"]) / len(mine), 2)}
        if clean:
            leg.update({"best": round(clean[0]["time"], 2), "bestFlight": clean[0]["flight"],
                        "median": round(statistics.median(r["time"] for r in clean), 2),
                        "keptMean": round(statistics.mean(r["time"] for r in kept), 2),
                        "lengthKept": round(statistics.mean(r["length"] for r in kept), 1),
                        "boost": round(statistics.mean(r["boost"] for r in kept), 2),
                        "drift": round(statistics.mean(r["drift"] for r in kept), 2),
                        "skims": round(statistics.mean(r["skims"] for r in kept), 1),
                        "line": average_line(kept)})
        by_style = {}
        for st in STYLES:
            sr = [r for r in clean if st in r["styles"]]
            if sr:
                by_style[st] = {"clean": len(sr), "median": round(statistics.median(r["time"] for r in sr), 2),
                                "best": round(min(r["time"] for r in sr), 2)}
            all_st = [r for r in mine if st in r["styles"]]
            if all_st:
                by_style.setdefault(st, {})["hitShare"] = round(sum(1 for r in all_st if r["rams"]) / len(all_st), 2)
                by_style[st]["runs"] = len(all_st)
        dirty = [r for r in mine if not r["clean"]]
        cost = statistics.mean(r["time"] for r in dirty) - statistics.median(r["time"] for r in clean) if dirty and clean else 0.0
        for st, d in by_style.items():
            # What a run of this style costs ON AVERAGE, mistakes included: the clean median plus the chance of a hit
            # times what a hit costs on this leg. A safe style can beat a faster but riskier one here.
            if "median" in d:
                d["expected"] = round(d["median"] + d.get("hitShare", 0) * max(0.0, cost), 2)
        leg["styles"] = by_style
        if dirty and clean:
            leg["mistakeCost"] = round(statistics.mean(r["time"] for r in dirty) - statistics.median(r["time"] for r in clean), 2)
        legs.append(leg)
    return legs


def lap_summary(legs, analyses):
    ring_legs = [l for l in legs if l["key"] != "start"]
    ideal = sum(l["best"] for l in ring_legs if "best" in l) if all("best" in l for l in ring_legs) else None
    typical = sum(l["median"] for l in ring_legs if "median" in l) if all("median" in l for l in ring_legs) else None
    laps = []
    for A in analyses:
        lg, gates = A["legs"], len(A["course"]["gates"])
        for s in range(1, len(lg) - gates + 1, gates):   # skip the start leg; whole laps of `gates` legs
            chunk = lg[s:s + gates]
            if len(chunk) == gates:
                laps.append(sum(g["time"] for g in chunk))
    return {"idealLap": round(ideal, 2) if ideal else None, "typicalCleanLap": round(typical, 2) if typical else None,
            "bestActualLap": round(min(laps), 2) if laps else None, "medianActualLap": round(statistics.median(laps), 2) if laps else None,
            "laps": len(laps)}


def load(dirs):
    COURSE = sf.load_course_js()
    analyses, skipped = [], []
    for d in dirs:
        for p in sorted(glob.glob(os.path.join(d, "flight_*.json"))):
            f = json.load(open(p))
            name = os.path.basename(p)
            if f.get("mode") != "Regatta" or (f.get("hull") or "").lower() in ("", "unknown"):
                skipped.append(f"{name}: not a Regatta flight with a known hull")
                continue
            C = sf.course_from(COURSE, int(f.get("intensity") or 1))
            if not C:
                skipped.append(f"{name}: no course for intensity {f.get('intensity')}")
                continue
            A = sf.analyze(f, C)
            A["flight"], A["course"], A["name"] = f, C, name
            analyses.append(A)
    return analyses, skipped


def report(analyses, skipped, write):
    groups = {}
    for A in analyses:
        groups.setdefault((A["norm"]["hullKey"], A["norm"]["intensity"]), []).append(A)
    for s in skipped:
        print("skipped:", s)
    for (hull, intensity), mine in sorted(groups.items()):
        runs = [r for A in mine for r in leg_runs(A, A["name"])]
        legs = build(runs)
        laps = lap_summary(legs, mine)
        print(f"\n{hull} I{intensity}: {len(mine)} flight(s), {len(runs)} legs, {sum(1 for r in runs if r['clean'])} clean")
        print(f"  ideal lap (your best clean time on every leg): {laps['idealLap']} s | typical clean lap: {laps['typicalCleanLap']} s | "
              f"your best actual lap: {laps['bestActualLap']} s | median lap: {laps['medianActualLap']} s ({laps['laps']} laps)")
        print("  | Leg | Runs | Clean | Best s | Median s | Hit share | Hits | Mistake costs s | Boost | Drift | Skims | Spread u | Trusted |")
        print("  |---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|")
        for l in legs:
            sp = round(statistics.mean(l["line"]["spread"]), 1) if "line" in l else "-"
            print(f"  | {l['key']} | {l['runs']} | {l['clean']} | {l.get('best', '-')} | {l.get('median', '-')} | {int(l['hitShare'] * 100)}% | "
                  f"{l['railHits']} | {l.get('mistakeCost', '-')} | {int(l.get('boost', 0) * 100)}% | {int(l.get('drift', 0) * 100)}% | "
                  f"{l.get('skims', '-')} | {sp} | {'yes' if l['trusted'] else 'no'} |")
        print("  styles, read from the recording (clean legs: count / median s; hit share over all legs of that style):")
        print("  | Leg | " + " | ".join(STYLES) + " |")
        print("  |---|" + "---|" * len(STYLES))
        for l in legs:
            cells = []
            for st in STYLES:
                d = l["styles"].get(st)
                cells.append("-" if not d else f"{d.get('clean', 0)} / {d.get('median', '-')} s, hits {int(d.get('hitShare', 0) * 100)}%")
            print(f"  | {l['key']} | " + " | ".join(cells) + " |")
        for l in legs:
            ok = {st: d for st, d in l["styles"].items() if d.get("clean", 0) >= 3 and "expected" in d}
            if len(ok) < 2:
                continue
            fast = min(ok, key=lambda st: ok[st]["median"])
            best = min(ok, key=lambda st: ok[st]["expected"])
            l["bestStyle"] = best
            note = "" if best == fast else f" (clean, {fast} is faster: {ok[fast]['median']} s, but it hits {int(ok[fast].get('hitShare', 0) * 100)}% of the time)"
            print(f"  -> leg {l['key']}: fly {best}: {ok[best]['expected']} s on average with mistakes, hits {int(ok[best].get('hitShare', 0) * 100)}%{note}")
        diffs = {}
        for r in runs:
            diffs[r["rivals"]] = diffs.get(r["rivals"], set()) | {r["flight"]}
        print("  rival difficulty per flight: " + ", ".join(f"{k}: {len(v)}" for k, v in sorted(diffs.items())))
        counts = {}
        for r in runs:
            for name in r["pressed"]:
                counts[name] = counts.get(name, 0) + 1
        print("  buttons pressed: " + (", ".join(f"{k} x{v}" for k, v in sorted(counts.items(), key=lambda kv: -kv[1])) or
                                       "none recorded (flights before 2026-10-16's recorder have no buttonEvents)"))
        risky = sorted([l for l in legs if l["runs"]], key=lambda l: -l["hitShare"])[:3]
        print("  most dangerous legs: " + ", ".join(f"{l['key']} ({int(l['hitShare'] * 100)}% of runs hit)" for l in risky))
        need = [l["key"] for l in legs if not l["trusted"]]
        print(f"  legs that need more clean runs (< {MIN_CLEAN}): {', '.join(need) or 'none'}")
        if write:
            os.makedirs(OUT_DIR, exist_ok=True)
            out = os.path.join(OUT_DIR, f"regatta_{hull}_I{intensity}.json")
            json.dump({"schema": "cosmic-shore.racing-line/1", "mode": "Regatta", "hull": hull, "intensity": intensity,
                       "flights": [A["name"] for A in mine], "points": POINTS, "keepShare": KEEP_SHARE, "minClean": MIN_CLEAN,
                       "laps": laps, "legs": legs}, open(out, "w"), indent=1)
            print(f"  wrote {os.path.relpath(out, REPO)}")


def self_test():
    # Resampling a straight line gives evenly spaced points with linear values.
    p, (v,), L = resample([[0, 0, 0], [10, 0, 0], [30, 0, 0]], [[0, 1, 3]], n=4)
    assert abs(L - 30) < 1e-9 and [round(x[0], 3) for x in p] == [0, 10, 20, 30] and [round(x, 3) for x in v] == [0, 1, 2, 3], (p, v)
    # The average of two parallel lines is the line between them; the spread is half their distance apart... per point 0.
    a = {"path": [[0, 0, k] for k in range(POINTS)], "speed": [100] * POINTS, "boostAt": [0] * POINTS, "driftAt": [0] * POINTS}
    b = {"path": [[2, 0, k] for k in range(POINTS)], "speed": [120] * POINTS, "boostAt": [1] * POINTS, "driftAt": [0] * POINTS}
    line = average_line([a, b])
    assert line["path"][5] == [1, 0, 5] and line["speed"][0] == 110 and line["offset"][0] == 1 and line["boostShare"][0] == 0.5, line
    # Dirty legs never make the line; only the faster half of the clean ones do.
    mk = lambda t, clean, x: {"flight": "f", "key": "3", "ring": 3, "time": t, "clean": clean, "rams": 0 if clean else 1, "missed": False,
                              "length": 100, "path": [[x, 0, k] for k in range(POINTS)], "speed": [100] * POINTS, "boost": 0, "drift": 0,
                              "skims": 0, "boostAt": [0] * POINTS, "driftAt": [0] * POINTS,
                              "styles": ["plain"] if x == 0 else ["drift"], "onRail": 0, "pressed": [], "rivals": "Medium"}
    legs = build([mk(5, True, 0), mk(6, True, 0), mk(9, True, 50), mk(10, True, 50), mk(3, False, 99)])
    l = legs[0]
    assert l["clean"] == 4 and l["kept"] == 2 and l["best"] == 5 and l["line"]["path"][0][0] == 0 and l["railHits"] == 1, l
    assert l["styles"]["plain"]["clean"] == 2 and l["styles"]["drift"]["median"] == 9.5, l["styles"]
    # Negative control: if the dirty leg were kept it would drag the line to x = 99.
    bad = average_line([mk(3, False, 99), mk(5, True, 0)])
    assert bad["path"][0][0] != 0
    print("racing_line self-test: OK (resample, average, dirty legs excluded, faster half kept, styles split, negative control)")


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--dir", action="append", default=[], help="a folder of flight_*.json (repeatable)")
    ap.add_argument("--write", action="store_true", help="save each line to Docs/AI_SYSTEM/racing_lines/")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if not a.dir:
        ap.error("--dir is required (a folder of flight_*.json)")
    analyses, skipped = load(a.dir)
    report(analyses, skipped, a.write)


if __name__ == "__main__":
    main()
