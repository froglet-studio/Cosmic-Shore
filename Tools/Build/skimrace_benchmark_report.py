#!/usr/bin/env python3
"""Summarise Skim Race AI benchmark records.

Usage:
    python3 Tools/Build/skimrace_benchmark_report.py FILE.jsonl [FILE.jsonl ...] [--markdown]

Each input line is one race written by SkimRaceRaceRecorder (Assets/_Scripts/Controller/AI/
SkimRace/). A race counts as a SUCCESS only when the recorder said so: the AI's domain won,
its collected crystals reached the authoritative target, and the authoritative finish time
(the winning seat's Score, written by SkimRaceController) is <= the limit. Incomplete races are
never counted as successes, and their (non-)finish times are excluded from the time statistics.
"""
import json
import statistics
import sys


def load(paths):
    races = []
    for p in paths:
        with open(p, encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if line:
                    races.append(json.loads(line))
    return races


def ai_seat(race):
    for s in race.get("seats", []):
        if s.get("policy"):
            return s
    return None


def main(argv):
    md = "--markdown" in argv
    paths = [a for a in argv if not a.startswith("--")]
    if not paths:
        print(__doc__)
        return 2
    races = load(paths)
    if not races:
        print("no records")
        return 1

    rows = []
    for r in races:
        seat = ai_seat(r) or {}
        times = seat.get("collectionTimes", [])
        rows.append({
            "session": r["session"], "race": r["raceIndex"], "commit": r.get("commit", ""),
            "intensity": r["intensity"], "seed": r["trackSeed"], "required": r["requiredCrystals"],
            "collected": r["aiDomainCrystals"], "finished": r["finished"], "winner": r["winnerDomain"],
            "ai": r["aiDomain"], "finish": r["authoritativeFinishTime"], "success": r["success"],
            "reason": r.get("failureReason", ""), "recoveries": seat.get("recoveries", 0),
            "speedloss": seat.get("speedLossEvents", 0), "policy": seat.get("policy", ""),
            "first": times[0] if times else None, "meanSpeed": seat.get("meanSpeed", 0.0),
            "frameMs": r.get("meanFrameMs", 0.0), "maxFrameMs": r.get("maxFrameMs", 0.0),
        })

    n = len(rows)
    complete = [x for x in rows if x["finished"] and x["winner"] == x["ai"] and x["collected"] >= x["required"]]
    ok = [x for x in rows if x["success"]]
    ft = [x["finish"] for x in complete]

    out = []
    if md:
        out.append("| # | session | race | int | seed | crystals | finish (s) | result | recov | speed-loss | mean u/s | frame ms |")
        out.append("|---|---|---|---|---|---|---|---|---|---|---|---|")
        for i, x in enumerate(rows, 1):
            res = "PASS" if x["success"] else ("FAIL: " + x["reason"])
            fin = f"{x['finish']:.2f}" if x["finished"] and x["winner"] == x["ai"] else "-"
            out.append(f"| {i} | {x['session']} | {x['race']} | {x['intensity']} | {x['seed']} | "
                       f"{x['collected']}/{x['required']} | {fin} | {res} | {x['recoveries']} | "
                       f"{x['speedloss']} | {x['meanSpeed']:.0f} | {x['frameMs']:.1f} |")
        out.append("")
    else:
        for x in rows:
            print(f"{x['session']} r{x['race']} I{x['intensity']} seed={x['seed']} "
                  f"{x['collected']}/{x['required']} finish={x['finish']:.2f} "
                  f"{'PASS' if x['success'] else 'FAIL ' + x['reason']} recov={x['recoveries']} "
                  f"meanSpeed={x['meanSpeed']:.0f} frame={x['frameMs']:.1f}ms max={x['maxFrameMs']:.0f}ms")

    out.append(f"races: {n}")
    out.append(f"full-sequence completion rate: {len(complete)}/{n} = {100.0 * len(complete) / n:.1f}%")
    out.append(f"completed within limit: {len(ok)}/{n} = {100.0 * len(ok) / n:.1f}%")
    if ft:
        out.append(f"finish time best/median/mean/worst: {min(ft):.2f} / {statistics.median(ft):.2f} / "
                   f"{statistics.mean(ft):.2f} / {max(ft):.2f} s")
    cc = [x["collected"] for x in rows]
    out.append(f"crystals mean/worst: {statistics.mean(cc):.1f} / {min(cc)}")
    stalls = sum(1 for x in rows if not x["finished"])
    out.append(f"timeouts (no finish): {stalls}/{n}")
    print("\n".join(out))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
