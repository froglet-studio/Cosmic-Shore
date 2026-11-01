#!/usr/bin/env python3
"""Refit the moment weights from what people rewatched in the Reel Room.

Highlight reels, stage 3 (Docs/HIGHLIGHT_REELS.md). Inputs:
  - the Reel Room's `watch` collection, exported as JSON (one doc per viewer:
    {reels: {<reelId>: {starts, finishes, replays, skips, rewinds, secs: {"<second>": count}}}})
  - each reel's cut list (render_reel.py writes <reel>.json: its moments with reel_start, reel_end, features)

A moment's measured score is how many times its seconds were watched per start of its reel, summed over
viewers, minus a skip penalty; a small prior keeps a reel two people saw from topping the list. The weights
are a ridge least-squares fit of that score on the miner's features, written in the form
`mine_moments.py --weights` reads. A quarter of the next batch stays exploratory (render_reel.py picks).

    python3 Tools/Build/highlights/refit_weights.py --watch watch.json --reels DIR [--out weights.json]
    python3 Tools/Build/highlights/refit_weights.py --self-test
"""
from __future__ import annotations

import argparse
import glob
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from mine_moments import WEIGHTS  # noqa: E402

PRIOR_STARTS = 3      # pseudo-starts at the batch's mean, so few views pull toward the mean
SKIP_PENALTY = 0.5
RIDGE = 0.5


def measured(watch_docs, reels):
    """[(features, score)] for every moment of every reel anybody started."""
    rows = []
    for reel_id, reel in reels.items():
        agg = {"starts": 0, "skips": 0, "secs": {}}
        for doc in watch_docs:
            s = (doc.get("reels") or {}).get(reel_id)
            if not s:
                continue
            agg["starts"] += s.get("starts", 0)
            agg["skips"] += s.get("skips", 0)
            for k, n in (s.get("secs") or {}).items():
                agg["secs"][int(k)] = agg["secs"].get(int(k), 0) + n
        if agg["starts"] == 0:
            continue
        for m in reel["moments"]:
            a, b = int(m["reel_start"]), max(int(m["reel_start"]) + 1, int(m["reel_end"]))
            views = sum(agg["secs"].get(t, 0) for t in range(a, b)) / (b - a)
            rows.append((m["features"], views, agg["starts"], agg["skips"]))
    if not rows:
        return []
    mean = sum(v / s for _, v, s, _ in rows) / len(rows)
    return [(f, (v + PRIOR_STARTS * mean) / (s + PRIOR_STARTS) - SKIP_PENALTY * k / s) for f, v, s, k in rows]


def fit(rows, keys):
    """Ridge least squares, no numpy: solve (X'X + rI) w = X'y by Gaussian elimination."""
    n = len(keys)
    A = [[0.0] * n for _ in range(n)]
    y = [0.0] * n
    for f, score in rows:
        x = [float(f.get(k, 0.0)) for k in keys]
        for i in range(n):
            y[i] += x[i] * score
            for j in range(n):
                A[i][j] += x[i] * x[j]
    for i in range(n):
        A[i][i] += RIDGE
    M = [A[i] + [y[i]] for i in range(n)]
    for c in range(n):
        p = max(range(c, n), key=lambda r: abs(M[r][c]))
        M[c], M[p] = M[p], M[c]
        for r in range(n):
            if r != c and M[c][c]:
                k = M[r][c] / M[c][c]
                M[r] = [a - k * b for a, b in zip(M[r], M[c])]
    return {keys[i]: round(M[i][n] / M[i][i], 4) if M[i][i] else 0.0 for i in range(n)}


def self_test():
    keys = list(WEIGHTS)
    # viewers rewatch near passes and skip stunts: the refit must rank near_pass above stunt
    reels = {"r1": {"moments": [
        {"reel_start": 0, "reel_end": 5, "features": {"near_pass": 1.0}},
        {"reel_start": 5, "reel_end": 10, "features": {"stunt": 1.0}},
    ]}, "r2": {"moments": [
        {"reel_start": 0, "reel_end": 5, "features": {"stunt": 0.8}},
        {"reel_start": 5, "reel_end": 10, "features": {"near_pass": 0.9}},
    ]}}
    secs1 = {str(t): (4 if t < 5 else 1) for t in range(10)}
    secs2 = {str(t): (1 if t < 5 else 4) for t in range(10)}
    watch = [{"reels": {"r1": {"starts": 2, "secs": secs1}, "r2": {"starts": 2, "secs": secs2}}}]
    w = fit(measured(watch, reels), keys)
    assert w["near_pass"] > w["stunt"], w
    assert measured([], reels) == [], "no views must give no rows"
    print("self-test ok:", {k: w[k] for k in ("near_pass", "stunt")})


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--watch")
    ap.add_argument("--reels")
    ap.add_argument("--out", default="weights.json")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if not (a.watch and a.reels):
        ap.error("give --watch and --reels")
    raw = json.load(open(a.watch))
    watch = raw if isinstance(raw, list) else raw.get("docs") or raw.get("documents") or [raw]
    watch = [d.get("data", d) for d in watch]
    reels = {}
    for p in glob.glob(os.path.join(a.reels, "**", "*.json"), recursive=True):
        d = json.load(open(p))
        if d.get("schema") == "cosmic-shore.reel/1":
            reels[d.get("id") or os.path.splitext(os.path.basename(p))[0]] = d
    rows = measured(watch, reels)
    if len(rows) < 8:
        print(f"only {len(rows)} watched moments: keep the hand weights until there are more")
        return
    w = fit(rows, list(WEIGHTS))
    json.dump({"schema": "cosmic-shore.moment-weights/1", "moments": len(rows), "weights": w}, open(a.out, "w"), indent=1)
    print(f"{len(rows)} moments -> {a.out}: {w}")


if __name__ == "__main__":
    sys.exit(main())
