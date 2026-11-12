#!/usr/bin/env python3
"""Find the moments worth showing in a real match recording (.cstheater), seen from each human in it.

The match-replay twin of `Tools/Build/highlights/mine_moments.py`, which reads the editor's flight records. The
same moment types and the same weights (imported from there, so a refit of one is a refit of both), detected from
what a replay carries: every hull's pose at 30 Hz. A replay has every hull's orientation, so turns are measured, not
guessed; it has no rings or crystals, so overtakes and crystal snatches come only from flight records for now.

    python3 Tools/Build/story/moments.py FILE.cstheater [...]     # print each human's moments
    python3 Tools/Build/story/moments.py --self-test
"""
from __future__ import annotations

import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "highlights"))
import theater  # noqa: E402

try:
    from mine_moments import WEIGHTS as _W, NEAR_PASS_RADIUS, PRE_ROLL, POST_ROLL, MERGE_GAP  # noqa: E402
    WEIGHTS = dict(_W)
except ImportError:  # the highlight tools are not on this branch: the same starting numbers
    WEIGHTS = {"near_pass": 1.0, "overtake": 1.4, "crystal_grab": 0.5, "crystal_snatch": 1.3, "stunt": 0.8,
               "close_finish": 1.6, "finish": 0.6, "pack": 0.7, "speed": 0.5, "combo": 0.6}
    NEAR_PASS_RADIUS, PRE_ROLL, POST_ROLL, MERGE_GAP = 60.0, 3.0, 2.0, 1.0

STUNT_KEEP = 6          # turn-rate peaks kept per human per match
STUNT_MIN_DEG_S = 120.0  # slower than this is flying, not a stunt
ANALYSIS_HZ = 10.0       # poses are thinned to this before detection (30 Hz buys nothing here)


def _thin(poses, hz_in):
    step = max(1, int(round(hz_in / ANALYSIS_HZ)))
    return poses[::step]


def _dist(a, b):
    return math.sqrt((a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2 + (a[3] - b[3]) ** 2)


def _angle(q1, q2):
    dot = abs(q1[4] * q2[4] + q1[5] * q2[5] + q1[6] * q2[6] + q1[7] * q2[7])
    return math.degrees(2 * math.acos(min(1.0, dot)))


def _at(poses, t):
    """The pose nearest time t (poses are time-ordered)."""
    lo, hi = 0, len(poses) - 1
    while lo < hi:
        mid = (lo + hi) // 2
        if poses[mid][0] < t:
            lo = mid + 1
        else:
            hi = mid
    return poses[lo] if poses else None


def detect(rec: theater.Recording, me: theater.Track):
    """Raw events for one human: (t, type, strength 0..1, detail)."""
    mine = _thin(me.poses, rec.sample_hz)
    if len(mine) < 20:
        return []
    others = [(t, _thin(t.poses, rec.sample_hz)) for t in rec.tracks if t is not me and t.poses]
    top = max(p[8] for p in mine) or 1.0
    fast = 0.6 * top
    ev = []
    for other, poses in others:
        d = []
        for p in mine:
            q = _at(poses, p[0])
            d.append(_dist(p, q) if q and abs(q[0] - p[0]) < 0.5 else 1e9)
        for i in range(1, len(mine) - 1):
            if d[i] < NEAR_PASS_RADIUS and d[i] <= d[i - 1] and d[i] < d[i + 1] and mine[i][8] > fast:
                ev.append((mine[i][0], "near_pass", 1.0 - d[i] / NEAR_PASS_RADIUS,
                           f"{other.vessel_name} ({'AI' if other.is_ai else 'human'}) at {d[i]:.0f}u"))
    turns = []
    for i in range(1, len(mine)):
        dt = mine[i][0] - mine[i - 1][0]
        if dt > 0:
            turns.append((_angle(mine[i - 1], mine[i]) / dt, mine[i][0]))
    peaks = [(r, t) for k, (r, t) in enumerate(turns)
             if r > STUNT_MIN_DEG_S and (k == 0 or r >= turns[k - 1][0]) and (k + 1 == len(turns) or r > turns[k + 1][0])]
    for r, t in sorted(peaks, reverse=True)[:STUNT_KEEP]:
        ev.append((t, "stunt", min(1.0, r / 720.0), f"turn {r:.0f} deg/s"))
    results = rec.summary.get("Results") or []
    if results:
        mine_r = next((x for x in results if x.get("Name") == me.name), None)
        if mine_r:
            end = mine[-1][0]
            scores = sorted((x.get("Score", 0) for x in results), reverse=True)
            gap = abs(scores[0] - scores[1]) / max(1e-6, abs(scores[0])) if len(scores) > 1 else 1.0
            if mine_r.get("Rank") in (1, 2) and len(scores) > 1 and gap < 0.05:
                ev.append((end, "close_finish", 1.0 - gap / 0.05, f"rank {mine_r.get('Rank')}, {gap:.1%} apart"))
            else:
                ev.append((end, "finish", 1.0 if mine_r.get("Rank") == 1 else 0.4, f"rank {mine_r.get('Rank')}"))
    return sorted(ev)


def moments(rec: theater.Recording, weights=None):
    """Every human's events, merged into windows and scored: the same shape mine_moments.py writes."""
    w = weights or WEIGHTS
    out = []
    for me in rec.humans:
        ev = detect(rec, me)
        groups = []
        for e in ev:
            if groups and e[0] - groups[-1][-1][0] <= MERGE_GAP:
                groups[-1].append(e)
            else:
                groups.append([e])
        mine = _thin(me.poses, rec.sample_hz)
        top = max(p[8] for p in mine) or 1.0
        for g in groups:
            peak = max(g, key=lambda e: w.get(e[1], 0) * e[2])
            feats = {}
            for e in g:
                feats[e[1]] = max(feats.get(e[1], 0.0), e[2])
            p = _at(mine, peak[0])
            pack = sum(1 for t in rec.tracks if t is not me and t.poses and
                       _dist(p, _at(t.poses, peak[0])) < NEAR_PASS_RADIUS * 2)
            feats["pack"] = float(pack)
            feats["speed"] = p[8] / top
            feats["combo"] = float(len({e[1] for e in g}) - 1)
            score = sum(w.get(k, 0) * v for k, v in feats.items())
            out.append({"pilot": me.name, "hull": me.vessel_name, "t": round(peak[0], 2),
                        "start": round(max(0.0, g[0][0] - PRE_ROLL), 2), "end": round(g[-1][0] + POST_ROLL, 2),
                        "events": [{"type": e[1], "t": round(e[0], 2), "strength": round(e[2], 3), "detail": e[3]}
                                   for e in g],
                        "features": {k: round(v, 3) for k, v in feats.items()}, "score": round(score, 3)})
    return _dedupe(sorted(out, key=lambda m: -m["score"]))


def _dedupe(ms):
    """Two humans passing each other is one moment seen twice: keep the higher-scored window, note the other pilot."""
    kept = []
    for m in ms:
        twin = next((k for k in kept if k["pilot"] != m["pilot"] and abs(k["t"] - m["t"]) <= MERGE_GAP), None)
        if twin:
            twin.setdefault("also", []).append(m["pilot"])
            continue
        kept.append(m)
    return kept


def self_test() -> int:
    rec = theater.synthetic()
    rec.summary = {"Results": [{"Rank": 1, "Name": "Garrett", "Score": 100.0}, {"Rank": 2, "Name": "Ava", "Score": 99.0}]}
    ms = moments(rec)
    types = {e["type"] for m in ms for e in m["events"]}
    assert "near_pass" in types, types
    assert "close_finish" in types, types
    assert all(m["pilot"] == "Garrett" for m in ms), "only humans get moments"
    assert ms == sorted(ms, key=lambda m: -m["score"])
    # two humans passing each other: one moment, not two
    both = theater.synthetic()
    both.tracks[1].is_ai = False
    two = moments(both)
    near = [m for m in two if any(e["type"] == "near_pass" for e in m["events"])]
    assert near and all(sum(1 for k in near if abs(k["t"] - m["t"]) <= MERGE_GAP) == 1 for m in near), near
    assert any("also" in m for m in near)
    # a lone flyer far from everyone has no near pass (negative control)
    rec.tracks = [rec.tracks[0], rec.tracks[2]]
    assert not any(e["type"] == "near_pass" for m in moments(rec) for e in m["events"])
    print("moments: self-test OK")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    for path in argv:
        rec = theater.read(path)
        print(json.dumps({"file": os.path.basename(path), "mode": rec.mode_name, "moments": moments(rec)[:10]}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
