#!/usr/bin/env python3
"""A Time game's I1-I4 ladder flown by its own vessel's SHIPPED AI in the real game (Amoebius headless, the race judge).

Elemental loop step 2 (Docs/ElementalLoop/PLAYBOOK.md): does each rung add one demand without a cliff? The human check is
rung_bars.py (recorded flights); this is the machine half, so a ladder can be read the night it changes instead of after
a week of flights. Each rung is raced alone by the game's vessel at the asked AI levels on the same course seeds, and the
table shows per rung: finished, median time, the step from the rung below, prism hits, the slowest gate (a stall), and
the element levels the hull actually started at (so the I2 level-5 start is visible).

    python3 Tools/Build/time_ladder_judge.py --mode Switchback --hull Dolphin [--levels Medium,Legendary] [--seeds 1,2,3,4]
    python3 Tools/Build/time_ladder_judge.py --mode Breakwater --hull Sparrow --out ladder.json

Read it with the bars in Docs/ElementalLoop/rung_bars.json in mind: I1 intro should finish for everyone, each rung
should cost more than the one below it (time, hits or both) and no rung should be a cliff (more than about 1.6x the
rung below in time, or a stall that ends races).
"""
import argparse
import concurrent.futures
import json
import os
import statistics
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "Compute", "evaluators"))
import regatta_judge  # noqa: E402

WORKERS = int(os.environ.get("LADDER_WORKERS", "4"))


def run(mode, hull, levels, seeds, limit_s, overrides=None, controller_overrides=None, start=None, rungs=(1, 2, 3, 4)):
    keys = [(level, rung) for level in levels for rung in rungs]

    def one(k):
        return regatta_judge.judge(None, [hull], seeds, limit_s=limit_s, mode=mode, intensity=k[1], difficulty=k[0],
                                   overrides=overrides, controller_overrides=controller_overrides, start=start)
    # One process per rung (each races its seeds back to back); at most WORKERS at once (4 game processes a box).
    with concurrent.futures.ThreadPoolExecutor(WORKERS) as ex:
        results = list(ex.map(one, keys))
    return {f"{lv}/I{r}": res for (lv, r), res in zip(keys, results)}


def summarize(result):
    seats = [s for r in result["races"] for s in r.get("seats", [])]
    times = [s["finish_s"] for s in seats if s.get("finish_s") is not None]
    hits = [s.get("prism_hits", 0) for s in seats]
    worst_gate = 0.0
    for s in seats:
        g = s.get("gate_s") or []
        steps = [b - a for a, b in zip([0.0] + g[:-1], g)]
        if steps:
            worst_gate = max(worst_gate, max(steps))
    levels = {}
    for r in result["races"]:
        for s in r.get("seats", []):
            if s.get("start_levels"):
                levels = s["start_levels"]
    return {"finished": f"{len(times)}/{len(seats)}", "median_s": round(statistics.median(times), 1) if times else None,
            "times": [round(t, 1) for t in times], "hits": round(statistics.mean(hits), 0) if hits else 0,
            "worst_gate_s": round(worst_gate, 1), "dnf": [s.get("dnf_reason") for s in seats if s.get("finish_s") is None],
            "start": levels, "notes": result.get("notes", [])}


def table(mode, hull, runs):
    lines = [f"{mode} ({hull}), the shipped AI alone, real game", "level/rung  finished  median   step   hits  worst gate  times"]
    prev = {}
    for key, res in runs.items():
        level, rung = key.split("/")
        s = summarize(res)
        step = ""
        if s["median_s"] and prev.get(level):
            step = f"{s['median_s'] / prev[level]:.2f}x"
        prev[level] = s["median_s"]
        lines.append(f"{key:<11} {s['finished']:>8}  {str(s['median_s']):>6}  {step:>5}  {s['hits']:>5.0f}  {s['worst_gate_s']:>9}  "
                     f"{s['times']} {s['dnf'] or ''}")
    return "\n".join(lines)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--mode", required=True)
    ap.add_argument("--hull", required=True)
    ap.add_argument("--levels", default="Medium,Legendary")
    ap.add_argument("--seeds", default="1,2,3,4")
    ap.add_argument("--limit", type=float, default=420)
    ap.add_argument("--overrides", help="JSON file [{asset, fields}] for every race (a candidate fix)")
    ap.add_argument("--controller-overrides", help="JSON file {field: value} on the race controller")
    ap.add_argument("--rungs", default="1,2,3,4")
    ap.add_argument("--start", help="card (default), rest, L5, or a JSON table (regatta_judge --start)")
    ap.add_argument("--out")
    a = ap.parse_args(argv)
    ov = json.load(open(a.overrides)) if a.overrides else None
    co = json.load(open(a.controller_overrides)) if a.controller_overrides else None
    runs = run(a.mode, a.hull, a.levels.split(","), [int(x) for x in a.seeds.split(",")], a.limit, ov, co,
               start=(json.loads(a.start) if a.start and a.start.startswith("{") else a.start),
               rungs=[int(x) for x in a.rungs.split(",")])
    print(table(a.mode, a.hull, runs))
    if a.out:
        with open(a.out, "w") as fh:
            json.dump({k: dict(summarize(v), races=v["races"]) for k, v in runs.items()}, fh, indent=1)


if __name__ == "__main__":
    main()
