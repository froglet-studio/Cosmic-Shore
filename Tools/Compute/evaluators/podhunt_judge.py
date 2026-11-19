#!/usr/bin/env python3
"""Pod Hunt judge: how long the Orca's Charge game takes to win, per intensity, in the real game (Amoebius headless).

Pod Hunt (PODHUNT.md) is Orca against Orca in the Boneyard: every missile or bomb blast on a rival hull is one point
and the first domain to the blast target (EndConditionOverridesSO.podHuntBlastTarget) wins. The race judge runs it
through `"mode": "PodHunt"` (RaceJudge.cs, Broadside's combat path): boot, launch on a fixed frame, Orcas placed on
two teams, Ready, and read each seat's CombatPoints and the time the turn ended. Nothing is modelled.

A match counts when the turn ended (a domain reached the target); "timeout" means nobody did inside --limit.

Command line:
    python3 Tools/Compute/evaluators/podhunt_judge.py [--per-team 2] [--matches 4] [--intensities 1,2,3,4]
        [--limit 600] [--seed 1] [--overrides o.json] [--out r.json]
    python3 Tools/Compute/evaluators/podhunt_judge.py --report r.json[,r2.json]

Setup: as regatta_judge.py (.NET 10; the player is built into REGATTA_JUDGE_BIN on first use).
"""
import argparse
import concurrent.futures
import json
import os
import statistics
import sys
import tempfile
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import regatta_judge as rj  # noqa: E402  (build, the player runner, the commit)

TEAMS = ("Jade", "Ruby")


def judge(per_team=2, matches=4, intensities=(1, 2, 3, 4), limit_s=600, seed=1, overrides=None, start="rest",
          workers=None):
    rj.build()
    t0 = time.time()
    workdir = tempfile.mkdtemp(prefix="podhunt-judge-")
    seats = [{"hull": "Orca", "domain": TEAMS[i % 2]} for i in range(2 * per_team)]
    jobs = []
    for intensity in intensities:
        for i in range(matches):
            spec = {"mode": "PodHunt", "seats": seats, "seeds": [seed * 1000 + intensity * 100 + i],
                    "limit_s": limit_s, "intensity": intensity, "start": start}
            if overrides:
                spec["overrides"] = overrides
            jobs.append((f"i{intensity}m{i:02d}", intensity, spec))
    workers = workers or int(os.environ.get("REGATTA_JUDGE_WORKERS", 0)) or 4
    with concurrent.futures.ThreadPoolExecutor(workers) as ex:
        results = list(ex.map(lambda j: rj._run_one(j[2], 240 + 2 * limit_s, workdir, j[0]), jobs))
    out = []
    for (tag, intensity, _), res in zip(jobs, results):
        for race in res.get("races", []):
            race["match"], race["intensity"] = tag, intensity
            out.append(race)
    return {"races": out, "wall_s": round(time.time() - t0, 1), "engine_commit": rj._commit(), "backend": "amoebius",
            "limit_s": limit_s, "start": start, "per_team": per_team, "workdir": workdir}


def table(results):
    """Per intensity: matches won inside the limit, median and mean time to win, blasts per Orca per minute."""
    rows = {}
    for result in results:
        for race in result["races"]:
            r = rows.setdefault(race["intensity"], {"won": [], "timeouts": 0, "bpm": [], "other": 0})
            seats = race.get("seats") or []
            secs = race.get("race_s") or 0
            if race.get("outcome") == "ended":
                r["won"].append(secs)
            elif race.get("outcome") == "timeout":
                r["timeouts"] += 1
            else:
                r["other"] += 1
                continue
            if secs > 0:
                r["bpm"].extend(s.get("missile_hits", 0) / (secs / 60) for s in seats)
    lines = ["intensity  won  timeout  other  median_s  mean_s  blasts/Orca/min"]
    for k in sorted(rows):
        r = rows[k]
        med = f"{statistics.median(r['won']):8.1f}" if r["won"] else "       -"
        mean = f"{statistics.mean(r['won']):7.1f}" if r["won"] else "      -"
        bpm = f"{statistics.mean(r['bpm']):6.2f}" if r["bpm"] else "     -"
        lines.append(f"I{k}        {len(r['won']):3d}  {r['timeouts']:7d}  {r['other']:5d}  {med}  {mean}  {bpm}")
    return "\n".join(lines)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--per-team", type=int, default=2)
    ap.add_argument("--matches", type=int, default=4, help="matches per intensity")
    ap.add_argument("--intensities", default="1,2,3,4")
    ap.add_argument("--limit", type=float, default=600)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--start", default="rest")
    ap.add_argument("--overrides", help="a JSON file of in-memory asset overrides (as broadside_judge.py)")
    ap.add_argument("--out")
    ap.add_argument("--report", help="comma-separated earlier --out files, pooled")
    a = ap.parse_args()
    if a.report:
        print(table([json.load(open(p)) for p in a.report.split(",")]))
        return
    overrides = json.load(open(a.overrides)) if a.overrides else None
    res = judge(a.per_team, a.matches, [int(x) for x in a.intensities.split(",")], a.limit, a.seed, overrides, a.start)
    if a.out:
        json.dump(res, open(a.out, "w"), indent=1)
    print(f"{len(res['races'])} matches, {res['wall_s']} s wall, engine {res['engine_commit']}")
    print(table([res]))


if __name__ == "__main__":
    main()
