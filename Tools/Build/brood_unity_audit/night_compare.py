#!/usr/bin/env python3
"""Per-hull index at 60 fps beside 30 fps, from the night run's results_60.csv and results_30.csv. The two blocks
play the same rosters (rosters.py --seed 11), so the difference per hull is paired by roster.

    py -3 Tools/Build/brood_unity_audit/night_compare.py [results_60.csv results_30.csv]
"""
import os
import statistics as st
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from summarize import load, table  # noqa: E402

here = os.path.dirname(os.path.abspath(__file__))
a = sys.argv[1] if len(sys.argv) > 2 else os.path.join(here, "results_60.csv")
b = sys.argv[2] if len(sys.argv) > 2 else os.path.join(here, "results_30.csv")
pa, _ = table(load(a))
pb, _ = table(load(b))
print("| Hull | Index 60 fps | n | Index 30 fps | n | 30 minus 60 | +- 1 se | Win 60 | Win 30 |")
print("|---|---|---|---|---|---|---|---|---|")
def se(h):
    v = [pa[h]["idx"], pb[h]["idx"]]
    return sum((st.stdev(x) ** 2 / len(x)) if len(x) > 1 else 0.0 for x in v) ** 0.5


rows = []
for h in sorted(set(pa) | set(pb)):
    ia = st.mean(pa[h]["idx"]) if h in pa else float("nan")
    ib = st.mean(pb[h]["idx"]) if h in pb else float("nan")
    rows.append((ia, h, ib))
for ia, h, ib in sorted(rows, reverse=True):
    print(f"| {h} | {ia:.2f} | {len(pa[h]['idx'])} | {ib:.2f} | {len(pb[h]['idx'])} | {ib - ia:+.2f} | {se(h):.2f} | "
          f"{st.mean(pa[h]['win']):.0%} | {st.mean(pb[h]['win']):.0%} |")
d = [ib - ia for ia, _, ib in rows]
big = [h for ia, h, ib in rows if abs(ib - ia) > 2 * se(h)]
print(f"\nOutside 2 se: {', '.join(big) or 'none'}.")
print(f"Mean absolute change {st.mean(abs(x) for x in d):.3f}; largest {max(d, key=abs):+.2f}.")
