#!/usr/bin/env python3
"""ark_escort_report.py - read the escort sim's JSON lines (ArkEscortSim) and print how each difficulty played.

    python3 Tools/Build/ark_escort_report.py path/to/escort.jsonl [--since N]   # N: skip the first N lines

One row per difficulty: runs, survived, mean lowest hull, the world the Ark fell in most, and each world's mean hull
loss (entry minus lowest), so a tuning session sees where the damage happens. Docs/Voyages/ARK_ESCORT.md, "Tuning".
"""
import argparse, collections, json, statistics, sys

OUTCOME = {0: "none", 1: "survived", 2: "fallen", 3: "abandoned"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("path")
    ap.add_argument("--since", type=int, default=0)
    a = ap.parse_args()
    runs = []
    with open(a.path, encoding="utf-8") as f:
        for i, line in enumerate(f):
            if i < a.since or not line.strip():
                continue
            rec = json.loads(line)
            if not rec.get("done"):  # the sim's last line under -arkEscortQuit ({"done":true,...}) is not a run
                runs.append(rec)
    by = collections.defaultdict(list)
    for r in runs:
        if r.get("Result", 0) in (1, 2):
            by[r["Difficulty"]].append(r)
    names = []
    for r in runs:
        for l in r.get("LegReports", []):
            if l["Name"] not in names:
                names.append(l["Name"])
    print(f"{'d':>2} {'runs':>4} {'won':>4} {'low hull':>8}  fell in          " + "  ".join(n[:10].rjust(10) for n in names))
    for d in sorted(by):
        rs = by[d]
        won = sum(1 for r in rs if r["Result"] == 1)
        low = statistics.mean(r["HullMinFraction"] for r in rs)
        fell = collections.Counter(r["LegReports"][r["FallenLeg"]]["Name"] for r in rs if r["Result"] == 2)
        loss = []
        for n in names:
            v = [l["HullAtEntry"] - l["HullMin"] for r in rs for l in r["LegReports"] if l["Name"] == n and l["Reached"]]
            loss.append(f"{statistics.mean(v):10.0f}" if v else " " * 10)
        print(f"{d:>2} {len(rs):>4} {won:>4} {low:8.0%}  {(fell.most_common(1)[0][0] if fell else '-'):16} " + "  ".join(loss))
    bad = [r for r in runs if r.get("Result", 0) not in (1, 2)]
    if bad:
        print(f"{len(bad)} runs did not finish: " + ", ".join(f"d{r['Difficulty']} {OUTCOME.get(r.get('Result', 0))}" for r in bad))


if __name__ == "__main__":
    sys.exit(main())
