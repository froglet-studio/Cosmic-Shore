#!/usr/bin/env python3
"""Find the moments worth watching in real players' flights, without rendering anything.

Stage 1 of the highlight-reel plan (Docs/HIGHLIGHT_REELS.md). Reads the human flight records the
editor sends on its own (`HumanFlightRecorder`, schema `cosmic-shore.flight/3`, 10 Hz; they land on
the `bugledger/<user>` branches under `BugLedger/shared/reports/<user>/flights/`) and emits candidate
moments: a window, a peak, a subject, a type, a feature vector and a first hand-weighted score.

The weights are a starting guess. Stage 3 refits them from what people rewatch
(`refit_weights.py` reads the reel viewer's log), so every number here is expected to move.

    python3 Tools/Build/highlights/mine_moments.py FLIGHTS_DIR --out moments.json [--top 40]
    python3 Tools/Build/highlights/mine_moments.py --fetch-bugledger DIR   # copy every flight off the branches
    python3 Tools/Build/highlights/mine_moments.py --self-test
"""
from __future__ import annotations

import argparse
import glob
import json
import math
import os
import subprocess
import sys

# ---- tunables (the stage 3 refit rewrites WEIGHTS; the thresholds are detection, not taste) ----
NEAR_PASS_RADIUS = 60.0       # units: a rival this close at speed is a near pass
CRYSTAL_GRAB_RADIUS = 45.0    # the crystal that moves away while the pilot is this close was taken by the pilot
SNATCH_RIVAL_RADIUS = 120.0   # ...and a rival this close to it makes it a snatch
PRE_ROLL, POST_ROLL = 3.0, 2.0  # seconds of build-up and aftermath around a peak
MERGE_GAP = 1.0               # peaks closer than this (s) become one moment
STUNTS_PER_FLIGHT = 6         # the turn-rate peaks kept per flight

WEIGHTS = {
    "near_pass": 1.0,
    "overtake": 1.4,
    "crystal_grab": 0.5,
    "crystal_snatch": 1.3,
    "stunt": 0.8,
    "close_finish": 1.6,
    "finish": 0.6,
    "pack": 0.7,          # bonus per extra rival within NEAR_PASS_RADIUS * 2 at the peak
    "speed": 0.5,         # bonus for pilot speed at the peak, as a fraction of the flight's max
    "combo": 0.6,         # bonus per extra event type merged into the same moment
}


def _col(columns, name):
    return columns.index(name) if name in columns else -1


def _dist(a, b):
    return math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)


class Flight:
    """One flight file, read into per-sample arrays."""

    def __init__(self, path: str, data: dict):
        self.path = path
        self.name = os.path.basename(path)
        self.mode = data.get("mode", "?")
        self.hull = data.get("hull", "?")
        self.pilot = path.replace("\\", "/").split("/reports/")[-1].split("/")[0] if "/reports/" in path.replace("\\", "/") else "?"
        self.finished = bool(data.get("finished"))
        self.hz = float(data.get("sampleHz") or 10)
        cols = data["columns"]
        rows = data["samples"]
        ix = {k: _col(cols, k) for k in ("t", "x", "y", "z", "speed", "rings", "pitchRate", "yawRate", "rollRate", "state", "boost")}
        g = lambda r, k: (r[ix[k]] if ix[k] >= 0 and ix[k] < len(r) else 0.0)
        self.t = [g(r, "t") for r in rows]
        self.pos = [(g(r, "x"), g(r, "y"), g(r, "z")) for r in rows]
        self.speed = [g(r, "speed") for r in rows]
        self.rings = [g(r, "rings") for r in rows]
        self.turn = [math.sqrt(g(r, "pitchRate") ** 2 + g(r, "yawRate") ** 2 + g(r, "rollRate") ** 2) for r in rows]
        # rivals: per sample, 4 numbers per rival (x, y, z, rings)
        self.rival_hulls = data.get("rivalHulls") or []
        nr = len(self.rival_hulls)
        self.rivals = []
        for r in (data.get("rivals") or []):
            self.rivals.append([(r[4 * k], r[4 * k + 1], r[4 * k + 2], r[4 * k + 3]) for k in range(nr) if 4 * k + 3 < len(r)])
        # crystals: per sample, 4 numbers per crystal slot (x, y, z, element)
        self.crystals = []
        for r in (data.get("crystals") or []):
            self.crystals.append([(r[4 * k], r[4 * k + 1], r[4 * k + 2]) for k in range(len(r) // 4)])
        self.n = len(self.t)
        self.max_speed = max(self.speed) if self.speed else 1.0

    def rivals_at(self, i):
        return self.rivals[i] if i < len(self.rivals) else []

    def rank_at(self, i):
        """1 = leading. Ties go to the pilot (a ring count is all the record carries for a rival)."""
        mine = self.rings[i]
        return 1 + sum(1 for r in self.rivals_at(i) if r[3] > mine)


def detect(f: Flight):
    """Every raw event in a flight: (time, index, type, strength 0..1, detail)."""
    ev = []
    if f.n < 20:
        return ev
    fast = 0.6 * f.max_speed

    # near passes: a local minimum of rival distance under the radius, at speed
    for k in range(len(f.rival_hulls)):
        d = [(_dist(f.pos[i], f.rivals_at(i)[k]) if k < len(f.rivals_at(i)) else 1e9) for i in range(f.n)]
        for i in range(1, f.n - 1):
            if d[i] < NEAR_PASS_RADIUS and d[i] <= d[i - 1] and d[i] < d[i + 1] and f.speed[i] > fast:
                ev.append((f.t[i], i, "near_pass", 1.0 - d[i] / NEAR_PASS_RADIUS, f"{f.rival_hulls[k]} at {d[i]:.0f}u"))

    # overtakes: the pilot's rank improves (and holds for a second, so a flicker is not a pass)
    hold = int(f.hz)
    for i in range(1, f.n - hold):
        a, b = f.rank_at(i - 1), f.rank_at(i)
        if b < a and all(f.rank_at(j) <= b for j in range(i, i + hold)):
            ev.append((f.t[i], i, "overtake", min(1.0, (a - b) / 2 + 0.5), f"P{a} to P{b}"))

    # crystals: a slot's crystal jumps away (taken and respawned) while the pilot is close
    for i in range(1, min(f.n, len(f.crystals))):
        prev, cur = f.crystals[i - 1], f.crystals[i]
        for s in range(min(len(prev), len(cur))):
            if _dist(prev[s], cur[s]) > 5.0 and _dist(f.pos[i], prev[s]) < CRYSTAL_GRAB_RADIUS:
                near = min((_dist(r, prev[s]) for r in f.rivals_at(i)), default=1e9)
                if near < SNATCH_RIVAL_RADIUS:
                    ev.append((f.t[i], i, "crystal_snatch", 1.0 - near / SNATCH_RIVAL_RADIUS, f"rival {near:.0f}u off"))
                else:
                    ev.append((f.t[i], i, "crystal_grab", 0.5, ""))

    # stunts: the top of the flight's turn-rate x speed, as a peak above its 95th percentile
    tv = [f.turn[i] * f.speed[i] for i in range(f.n)]
    srt = sorted(tv)
    p95 = srt[int(0.95 * (len(srt) - 1))]
    top = srt[-1] or 1.0
    stunts = [(f.t[i], i, "stunt", (tv[i] - p95) / max(1e-6, top - p95), f"turn {f.turn[i]:.0f}")
              for i in range(1, f.n - 1)
              if tv[i] > p95 and tv[i] >= tv[i - 1] and tv[i] > tv[i + 1] and f.speed[i] > fast]
    ev += sorted(stunts, key=lambda e: -e[3])[:STUNTS_PER_FLIGHT]  # a flight's few best, not every wiggle

    # the finish
    if f.finished:
        i = f.n - 1
        gaps = [_dist(f.pos[i], r) for r in f.rivals_at(i)]
        ring_gap = min((abs(r[3] - f.rings[i]) for r in f.rivals_at(i)), default=99)
        if ring_gap <= 1:
            ev.append((f.t[i], i, "close_finish", 1.0 if ring_gap == 0 else 0.6, f"P{f.rank_at(i)}, nearest rival {min(gaps, default=0):.0f}u"))
        else:
            ev.append((f.t[i], i, "finish", 0.5, f"P{f.rank_at(i)}"))
    return ev


def moments(f: Flight):
    """Merge raw events into scored moments with a pre-roll and post-roll."""
    ev = sorted(detect(f))
    out = []
    for t, i, kind, strength, detail in ev:
        if out and t - out[-1]["peak"] <= MERGE_GAP:
            m = out[-1]
        else:
            m = {"flight": f.name, "path": f.path, "pilot": f.pilot, "mode": f.mode, "hull": f.hull,
                 "peak": t, "peak_index": i, "events": [], "start": max(0.0, t - PRE_ROLL), "end": t + POST_ROLL}
            out.append(m)
        m["events"].append({"type": kind, "t": round(t, 2), "strength": round(strength, 3), "detail": detail})
        m["end"] = min(f.t[-1], max(m["end"], t + POST_ROLL))
    for m in out:
        i = m["peak_index"]
        types = {e["type"] for e in m["events"]}
        pack = sum(1 for r in f.rivals_at(i) if _dist(f.pos[i], r) < 2 * NEAR_PASS_RADIUS)
        feats = {k: 0.0 for k in WEIGHTS}
        for e in m["events"]:
            feats[e["type"]] = max(feats[e["type"]], e["strength"])
        feats["pack"] = float(max(0, pack - 1))
        feats["speed"] = f.speed[i] / f.max_speed if f.max_speed else 0.0
        feats["combo"] = float(len(types) - 1)
        m["features"] = {k: round(v, 3) for k, v in feats.items()}
        m["score"] = round(score(feats), 3)
        m["start"], m["end"], m["peak"] = round(m["start"], 2), round(m["end"], 2), round(m["peak"], 2)
        del m["peak_index"]
    return out


def score(features, weights=None):
    w = weights or WEIGHTS
    return sum(w.get(k, 0.0) * v for k, v in features.items())


def load(path):
    with open(path, encoding="utf-8") as fh:
        return Flight(path, json.load(fh))


def fetch_bugledger(dest):
    """Copy every flight record off the bugledger/<user> branches (read-only; nothing is checked out)."""
    os.makedirs(dest, exist_ok=True)
    refs = subprocess.run(["git", "ls-remote", "origin", "refs/heads/bugledger/*"], capture_output=True, text=True, check=True).stdout.split()
    branches = [r[len("refs/heads/"):] for r in refs if r.startswith("refs/heads/")]
    subprocess.run(["git", "fetch", "-q", "origin"] + branches, check=True)
    n = 0
    for b in branches:
        user = b.split("/", 1)[1]
        tree = subprocess.run(["git", "ls-tree", "-r", "--name-only", f"origin/{b}", f"BugLedger/shared/reports/{user}/flights"],
                              capture_output=True, text=True).stdout.split("\n")
        for p in filter(None, tree):
            out = os.path.join(dest, user, os.path.basename(p))
            os.makedirs(os.path.dirname(out), exist_ok=True)
            if not os.path.exists(out):
                with open(out, "wb") as fh:
                    fh.write(subprocess.run(["git", "show", f"origin/{b}:{p}"], capture_output=True, check=True).stdout)
            n += 1
    print(f"{n} flights from {len(branches)} branches in {dest}")


def self_test():
    """A synthetic race: the pilot passes a rival closely, takes a crystal with the rival 100u off, finishes a ring ahead."""
    n = 100
    cols = ["t", "x", "y", "z", "speed", "rings", "pitchRate", "yawRate", "rollRate"]
    samples, rivals, crystals = [], [], []
    for i in range(n):
        t = i / 10
        rings = 0 if i < 50 else 1
        samples.append([t, 10.0 * i, 0, 0, 200.0, rings, 0, 0, 0])
        rx = 10.0 * i + (250 - 5 * i)  # starts ahead, the pilot catches it at i=50
        rivals.append([rx, 20.0, 0, 0])
        crystals.append([700.0, 0, 0, 5] if i < 70 else [5000.0, 0, 0, 5])
    data = {"mode": "Test", "hull": "Squirrel", "finished": True, "sampleHz": 10, "columns": cols, "samples": samples,
            "rivalHulls": ["sparrow"], "rivals": rivals, "crystals": crystals}
    f = Flight("reports/test/flights/flight_Test.json", data)
    kinds = {e[2] for e in detect(f)}
    for need in ("near_pass", "crystal_snatch", "close_finish"):
        assert need in kinds, f"self-test: {need} not detected, got {sorted(kinds)}"
    # negative control: a lone flight in a straight line has nothing but its finish
    lone = Flight("x", {"mode": "T", "hull": "S", "finished": False, "sampleHz": 10, "columns": cols,
                        "samples": [[i / 10, 10.0 * i, 0, 0, 200.0, 0, 0, 0, 0] for i in range(n)]})
    assert not [e for e in detect(lone) if e[2] != "stunt"], "self-test: a lone straight flight produced events"
    print("self-test ok:", sorted(kinds))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("flights", nargs="?", help="folder of flight_*.json (searched recursively)")
    ap.add_argument("--out", default="moments.json")
    ap.add_argument("--top", type=int, default=40)
    ap.add_argument("--weights", help="a weights JSON written by refit_weights.py")
    ap.add_argument("--fetch-bugledger", metavar="DIR")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        return self_test()
    if a.fetch_bugledger:
        return fetch_bugledger(a.fetch_bugledger)
    if not a.flights:
        ap.error("give a flights folder")
    if a.weights:
        WEIGHTS.update(json.load(open(a.weights))["weights"])
    allm, bad = [], 0
    files = sorted(glob.glob(os.path.join(a.flights, "**", "flight_*.json"), recursive=True))
    for p in files:
        try:
            f = load(p)
        except Exception:
            bad += 1
            continue
        for m in moments(f):
            m["score"] = round(score(m["features"]), 3)
            allm.append(m)
    allm.sort(key=lambda m: -m["score"])
    by_type = {}
    for m in allm:
        for e in m["events"]:
            by_type[e["type"]] = by_type.get(e["type"], 0) + 1
    report = {"schema": "cosmic-shore.moments/1", "flights": len(files), "unreadable": bad, "moments": len(allm),
              "by_type": by_type, "weights": WEIGHTS, "top": allm[: a.top], "all": allm}
    with open(a.out, "w") as fh:
        json.dump(report, fh, indent=1)
    print(f"{len(files)} flights, {len(allm)} moments -> {a.out}; by type {by_type}")
    for m in allm[:10]:
        print(f"  {m['score']:5.2f}  {m['mode']:<12} {m['hull']:<9} {m['pilot']:<9} t={m['peak']:6.1f}  "
              + ", ".join(f"{e['type']}({e['detail']})" if e['detail'] else e['type'] for e in m["events"]))


if __name__ == "__main__":
    sys.exit(main())
