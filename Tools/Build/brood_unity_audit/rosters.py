#!/usr/bin/env python3
"""Rosters for the real-Unity Brood Rush audit: N matches of 3 teams of 3 drawn from the 17 card hulls so every hull
sits the same number of seats (+-1), no hull twice in one match, and team-mates and teams rotate. Seeded, standard library.

    py -3 Tools/Build/brood_unity_audit/rosters.py --matches 40 --seed 7 --split 3
prints one -broodRosters string per process (A,B,C/D,E,F/G,H,I;...).
"""
import argparse
import random

HULLS = ["Meerkat", "Wrasse", "Urchin", "Honeyguide", "Stoat", "Sparrow", "Scarab", "Squirrel", "Dolphin", "Orca",
         "TigerMoth", "Thresher", "Shrike", "Grizzly", "Swordfish", "Rhino", "Manta"]


def build(n, seed):
    rng = random.Random(seed)
    count = {h: 0 for h in HULLS}
    out = []
    for _ in range(n):
        lo = sorted(HULLS, key=lambda h: (count[h], rng.random()))[:9]
        rng.shuffle(lo)
        for h in lo:
            count[h] += 1
        out.append([lo[0:3], lo[3:6], lo[6:9]])
    return out, count


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--matches", type=int, default=40)
    ap.add_argument("--seed", type=int, default=7)
    ap.add_argument("--split", type=int, default=1)
    a = ap.parse_args()
    ms, count = build(a.matches, a.seed)
    for k in range(a.split):
        print(";".join("/".join(",".join(t) for t in m) for m in ms[k::a.split]))
    print("# seats per hull:", " ".join(f"{h}={c}" for h, c in count.items()))


if __name__ == "__main__":
    main()
