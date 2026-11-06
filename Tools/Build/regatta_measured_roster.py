#!/usr/bin/env python3
"""The Regatta roster as the REAL GAME races it: every Regatta-ready hull's model, calibrated to its measured race.

The offline race models were 1.5-2.6x faster than the editor for every hull but the Urchin (whose rail model was right),
so a course search on them saw the Urchin as mid-pack and never touched it (2026-11-06, Garrett: "the urchin was still
overpowered"). This calibrates each hull to the Regatta Hull Benchmark's measured I1 races on the shipped course (each
AI alone, Legendary; Docs/Reports/REGATTA_AI.md 2026-11-03) and keeps the model's COURSE dependence:

    measured  every hull but the Urchin: ANCHORED at its measured seconds on the shipped I1 course and moved, on any other
              course or intensity, by the geometric mean of two models' ratios (new / shipped I1): the race-line model
              (regatta_line_sim at the solved LINE_SPEED, LaneShare 0.35: line length, missed mouths, the AI's turn law)
              and the lap model (regatta_balance.py at the card's Time level: per-hull corner speed and acceleration).
              The Squirrel and the Stoat have no race-line row and move by the lap model alone. Neither model alone is
              trusted: the lap model's absolute times are 0.4-5.3x off the measurements (the Rhino 5.3x, the Serpent
              0.41x), and the line model flies one speed, so on its own a course's shape scales every hull alike.
    rail      Urchin: rides its own colour at RAIL u/s and flies a neutral stretch at FREE u/s, both fitted to its three
              measured races (neutral share 0 / 0.15 / 0.3: 51 / 60.5 / 74.5 s).

The line model alone was tried first: the search could not move the spread (I1: 0.686 -> 0.682 in 33 evaluations).

    python3 Tools/Build/regatta_measured_roster.py            # solve and print the roster (JSON)
    python3 Tools/Build/regatta_measured_roster.py --check    # the committed ROSTER still reproduces the measurements
"""
import json
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(ROOT, "Tools", "Compute", "evaluators"))
import course_port as cp      # noqa: E402
import race_models as rm      # noqa: E402

# Seconds for three laps, I1, shipped course, each AI alone (the middle of each hull's measured range, 2026-11-03).
MEASURED = {"Manta": 67.0, "Rhino": 95.0, "Dolphin": 97.5, "Serpent": 100.0, "Sparrow": 133.0,
            "Squirrel": 75.0, "Stoat": 70.0}
URCHIN_MEASURED = {0.0: 51.0, 0.15: 60.5, 0.3: 74.5}     # neutral share -> seconds
LINE_SPEED = {"Manta": 210.5, "Dolphin": 146.2, "Rhino": 148.0, "Serpent": 142.4, "Sparrow": 107.3}  # line_check, solved
CARD = os.path.join(ROOT, "Assets", "_SO_Assets", "Games", "ArcadeGameRegatta.asset")
CLASS = {1: "Manta", 2: "Dolphin", 3: "Rhino", 4: "Urchin", 6: "Squirrel", 7: "Serpent", 11: "Sparrow", 12: "Scarab",
         15: "Stoat"}

# The solved references, 2026-11-06: each hull's lap-model and race-line-model seconds on the SHIPPED I1 course at its
# card level and solved line speed (regenerate with this script after a new measurement or a card change; --check).
BAL_REF = {"Manta": 23.94, "Rhino": 17.82, "Dolphin": 41.88, "Serpent": 241.68, "Sparrow": 73.62, "Squirrel": 48.54,
           "Stoat": 75.72}
LINE_REF = {"Manta": 67.0, "Rhino": 95.0, "Dolphin": 97.4, "Serpent": 100.0, "Sparrow": 133.0}
URCHIN = {"speed": 288.3, "free_speed": 112.7}


def card_levels():
    """{hull: {"1": level, ...}} from the card's StartingElements (a hull with no row is at 0)."""
    text = open(CARD, encoding="utf-8").read()
    out = {}
    for m in re.finditer(r"  - Class: (\d+)\n    Intensity: (\d+)\n    Levels:\n(?:      \w+: [-\d.]+\n)*?      Time: ([-\d.]+)",
                         text):
        h = CLASS.get(int(m.group(1)))
        if h:
            out.setdefault(h, {})[m.group(2)] = int(round(float(m.group(3)) * 10))
    return out


def roster(bal_ref=None, line_ref=None, levels=None):
    bal_ref, line_ref = bal_ref or BAL_REF, line_ref or LINE_REF
    levels = levels if levels is not None else card_levels()
    out = {}
    for h in MEASURED:
        d = {"anchor": MEASURED[h], "bal_ref": bal_ref[h],
             "time_levels": {str(i): levels.get(h, {}).get(str(i), 0) for i in range(1, 5)}}
        if h in LINE_SPEED:
            d.update(speed=LINE_SPEED[h], share=0.35, line_ref=line_ref[h])
        out[h] = {"model": "measured", "drive": d}
    out["Urchin"] = {"model": "rail", "drive": dict(URCHIN)}
    return out


ROSTER = roster() if BAL_REF else None


def race(course, hull, entry, intensity=1, neutral=0.0):
    d = dict(entry["drive"])
    if entry["model"] == "rail":
        d["neutral"] = neutral
    t, _ = rm.race(hull, entry["model"], course, intensity, drive=d, limit=600)
    return t


def line_check(course, intensity=1):
    """The constant-speed race-line model (missed mouths, the AI's turn law) at the solved speeds: {hull: (s, rings)}."""
    out = {}
    for h, v in LINE_SPEED.items():
        t, r = rm.race(h, "line", course, intensity, drive={"speed": v, "share": 0.35}, limit=600)
        out[h] = (round(t, 1), r["rings"])
    return out


def solve():
    course = cp.regatta_course(1)
    lv = card_levels()
    bal, line = {}, {}
    for h in MEASURED:
        bal[h] = round(race(course, h, {"model": "balance", "drive": {"time_level": lv.get(h, {}).get("1", 0)}}), 3)
        if h in LINE_SPEED:
            line[h] = round(race(course, h, {"model": "line", "drive": {"speed": LINE_SPEED[h], "share": 0.35}}), 3)
    L = course["laneLengths"][0] * 3
    rows = [((1 - n) * L, n * L, t) for n, t in URCHIN_MEASURED.items()]
    a11 = sum(r[0] * r[0] for r in rows); a12 = sum(r[0] * r[1] for r in rows); a22 = sum(r[1] * r[1] for r in rows)
    b1 = sum(r[0] * r[2] for r in rows); b2 = sum(r[1] * r[2] for r in rows)
    det = a11 * a22 - a12 * a12
    inv_r, inv_f = (b1 * a22 - b2 * a12) / det, (a11 * b2 - a12 * b1) / det
    return bal, line, {"speed": round(1 / inv_r, 1), "free_speed": round(1 / inv_f, 1)}


def check(r):
    course = cp.regatta_course(1)
    bad = []
    for h, m in MEASURED.items():
        t = race(course, h, r[h])
        if abs(math.log(t / m)) > 0.03:
            bad.append(f"{h}: {t:.1f} s against {m} s measured")
    for n, m in URCHIN_MEASURED.items():
        t = race(course, "Urchin", r["Urchin"], 1, n)
        if abs(math.log(t / m)) > 0.05:
            bad.append(f"Urchin at neutral {n}: {t:.1f} s against {m} s measured")
    return bad


if __name__ == "__main__":
    if "--check" in sys.argv:
        problems = check(ROSTER)
        print("\n".join(problems) or "regatta_measured_roster --check: ok")
        sys.exit(1 if problems else 0)
    bal, line, urchin = solve()
    print(json.dumps({"BAL_REF": bal, "LINE_REF": line, "URCHIN": urchin}, indent=1))
    print("\n".join(check(roster(bal, line))) or "reproduces every measured race", file=sys.stderr)
