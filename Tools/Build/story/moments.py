#!/usr/bin/env python3
"""Find the moments worth showing in a real match recording (.cstheater), seen from each human in it.

The match-replay twin of `Tools/Build/highlights/mine_moments.py`, which reads the editor's flight records. The
same moment types and the same weights (imported from there, so a refit of one is a refit of both), detected from
what a replay carries: every hull's pose at 30 Hz. A replay has every hull's orientation, so turns are measured, not
guessed; it has no rings or crystals, so overtakes and crystal snatches come only from flight records for now. Its
world stream has every prism's birth and death, so creation (a pilot's own trail going up) and destruction (prisms
dying around a pilot; a raid when they were a rival's) are measured too.

Every moment names its ELEMENTS, the four things a Cosmic Shore video has to show (Garrett, 2026-11-12): PvP,
maneuvering, destruction and creation. The shoot list films each element with its own camera.

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

WEIGHTS.setdefault("destroy", 1.2)
WEIGHTS.setdefault("raid", 1.6)
WEIGHTS.setdefault("create", 0.9)

ELEMENTS = ("pvp", "maneuvering", "destruction", "creation")
# what each event shows; the first is the element its moment is filmed as
ELEMENTS_OF = {"near_pass": ("pvp",), "close_finish": ("pvp",), "overtake": ("pvp",), "crystal_snatch": ("pvp",),
               "stunt": ("maneuvering",), "raid": ("destruction", "pvp"), "destroy": ("destruction",),
               "create": ("creation",)}

DESTROY_RADIUS = 25.0    # prisms dying this close to a pilot are that pilot's doing (or happening to them)
EXPIRY_SHARE = 0.3       # when this share of all deaths come at one age, that age is a trail's lifetime running out
EXPIRY_BAND = 0.1        # deaths within 10% of that age are expiry, not destruction
DESTROY_BUCKET = 0.5
DESTROY_MIN = 5
DESTROY_KEEP = 4
CREATE_RADIUS = 35.0     # the pilot's own prisms going up this close behind it: its trail
CREATE_BUCKET = 1.0
CREATE_MIN = 8
CREATE_KEEP = 3
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
                           f"{other.vessel_name} ({'AI' if other.is_ai else 'human'}) at {d[i]:.0f}u",
                           {"with": other.name, "dist": round(d[i], 1)}))
    turns = []
    for i in range(1, len(mine)):
        dt = mine[i][0] - mine[i - 1][0]
        if dt > 0:
            turns.append((_angle(mine[i - 1], mine[i]) / dt, mine[i][0]))
    peaks = [(r, t) for k, (r, t) in enumerate(turns)
             if r > STUNT_MIN_DEG_S and (k == 0 or r >= turns[k - 1][0]) and (k + 1 == len(turns) or r > turns[k + 1][0])]
    for r, t in sorted(peaks, reverse=True)[:STUNT_KEEP]:
        ev.append((t, "stunt", min(1.0, r / 720.0), f"turn {r:.0f} deg/s"))
    ev += _world_events(rec, me, mine)
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


def _world_events(rec, me, mine):
    """Destruction and creation around one pilot, from the prism stream: bursts, not every prism."""
    if not rec.world:
        return []
    expiry = _expiry_age(rec.world)
    dead, born, where, birth = {}, {}, {}, {}
    for t, kind, pid, x, y, z, dom in rec.world:
        if kind == theater.GONE:
            x, y, z, dom = where.get(pid, (x, y, z, dom))   # a death is placed where the prism last stood
            age = t - birth[pid] if pid in birth else None
            if expiry and age is not None and abs(age - expiry) <= EXPIRY_BAND * expiry:
                continue   # a trail fading out on time: nothing broke it
        else:
            where[pid] = (x, y, z, dom)
            if kind in (theater.BORN, theater.RESTORED) and t > 0.5:   # standing at the start: age unknown
                birth[pid] = t
        if kind not in (theater.BORN, theater.GONE) or t < mine[0][0] or t > mine[-1][0]:
            continue
        p = _at(mine, t)
        d = math.sqrt((p[1] - x) ** 2 + (p[2] - y) ** 2 + (p[3] - z) ** 2)
        if kind == theater.GONE and d < DESTROY_RADIUS:
            dead.setdefault(int(t / DESTROY_BUCKET), []).append((t, dom))
        elif kind == theater.BORN and d < CREATE_RADIUS and dom == me.domain and t > 0.5:
            born.setdefault(int(t / CREATE_BUCKET), []).append(t)
    ev = []
    for k, hits in sorted(dead.items(), key=lambda kv: -len(kv[1]))[:DESTROY_KEEP]:
        if len(hits) < DESTROY_MIN:
            continue
        rival = sum(1 for _, dom in hits if dom and dom != me.domain)
        kind = "raid" if rival * 2 > len(hits) else "destroy"
        t = sorted(h[0] for h in hits)[len(hits) // 2]
        ev.append((t, kind, min(1.0, len(hits) / 25.0), f"{len(hits)} prisms" + (" of a rival's" if kind == "raid" else "")))
    for k, hits in sorted(born.items(), key=lambda kv: -len(kv[1]))[:CREATE_KEEP]:
        if len(hits) < CREATE_MIN:
            continue
        ev.append((sorted(hits)[len(hits) // 2], "create", min(1.0, len(hits) / 30.0), f"{len(hits)} prisms laid"))
    return ev


def _expiry_age(world):
    """The age most prisms die at, when one age dominates: a trail's lifetime, not destruction. None otherwise."""
    birth, ages = {}, []
    for t, kind, pid, *_ in world:
        if kind in (theater.BORN, theater.RESTORED) and t > 0.5:
            birth[pid] = t
        elif kind == theater.GONE and pid in birth and t - birth[pid] > 0.5:
            ages.append(t - birth[pid])
    if len(ages) < 50:
        return None
    best, n = None, 0
    for a in sorted(ages)[:: max(1, len(ages) // 200)]:
        k = sum(1 for b in ages if abs(b - a) <= EXPIRY_BAND * a)
        if k > n:
            best, n = a, k
    return best if n >= EXPIRY_SHARE * len(ages) else None


def elements(events):
    """The elements a moment shows, the filmed one first (the strongest event's)."""
    out = []
    for e in sorted(events, key=lambda e: -WEIGHTS.get(e["type"], 0) * e.get("strength", 1)):
        for el in ELEMENTS_OF.get(e["type"], ()):
            if el not in out:
                out.append(el)
    return out


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
            events = [{"type": e[1], "t": round(e[0], 2), "strength": round(e[2], 3), "detail": e[3],
                       **(e[4] if len(e) > 4 else {})} for e in g]
            out.append({"pilot": me.name, "hull": me.vessel_name, "t": round(peak[0], 2),
                        "start": round(max(0.0, g[0][0] - PRE_ROLL), 2), "end": round(g[-1][0] + POST_ROLL, 2),
                        "events": events, "elements": elements(events),
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
    assert any(e.get("with") == "Ava" for m in ms for e in m["events"] if e["type"] == "near_pass"), "who was passed"
    assert "close_finish" in types, types
    # the world stream: Garrett's own trail is creation, tearing through Ava's wall is a raid (destruction + pvp),
    # and Kai's prisms dying 2000 units from anyone are nobody's moment (negative control)
    assert "create" in types and "raid" in types, types
    # a rival's trail fading out on time right beside Garrett is not destruction (negative control)
    fade = theater.synthetic()
    g = fade.tracks[0].poses
    extra, pid = [], 10000
    for k in range(200):
        p = g[int((2 + k * 0.08) * fade.sample_hz)]
        extra += [(1.0 + k * 0.08 - 0.9, theater.BORN, pid, p[1] + 5, p[2], p[3], 2),
                  (1.0 + k * 0.08 + 0.1, theater.GONE, pid, 0, 0, 0, 0)]
        pid += 1
    fade.world = sorted(fade.world + extra, key=lambda e: e[0])
    assert _expiry_age(fade.world) is not None
    fms = moments(fade)
    assert not any(e["type"] in ("raid", "destroy") and e["t"] < 10 for m in fms for e in m["events"]), "expiry"
    assert any(e["type"] == "raid" for m in fms for e in m["events"]), "the real raid still counts"
    raid = next(m for m in ms if any(e["type"] == "raid" for e in m["events"]))
    assert raid["elements"][0] == "destruction" and "pvp" in raid["elements"], raid["elements"]
    assert all(11 < e["t"] < 13 for m in ms for e in m["events"] if e["type"] in ("raid", "destroy")), "only the wall"
    assert all(4 <= e["t"] <= 7 for m in ms for e in m["events"] if e["type"] == "create")
    assert {el for m in ms for el in m["elements"]} >= {"pvp", "destruction", "creation"}
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
