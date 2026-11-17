#!/usr/bin/env python3
"""Spearline, raced offline: does every claim of the fun pass hold up? (A MODEL - read its limits below.)

The course and every seal outcome are the SHIPPED C#: Tools/Build/spearline_harness --export lays Headlong's circuit with
Spearline's mouths, picks the sealed rings, lays each seal and its armour, and plays every pilot policy through it with
SpearlineSeals' own hole rule, stroke model and read (passes to get through under the worn seal, and the line it
crossed on). This file only TIMES those passes on the Swordfish's airframe:

  legs        Headlong's competent-pilot lap (headlong_stampede_model: every corner at the fastest speed the airframe
              holds through it, the ramp's linear wind-up between, braking instant). The Swordfish flies the Rhino's
              airframe and its own ramp asset with the same numbers (checked by --self-test), Time scaling off.
  closed pass the pilot comes round: the fastest full loop the airframe can fly (min over radius of 2 pi r / v(r)), and
              the leg after it starts from that loop's speed (the ramp winds back up)
  line        a crossing off the line costs its offset x the corner's turn / speed (small: seals are the gentle corners)
  human       one factor k fitted so the bare circuit at I1 takes what the one recorded human Headlong I1 race took
              (2.13 s a gate, BugLedger owner flight 2026-11-12, 40 rings); everything human-scaled is k x model

Policies (Runner.cs): hull; six fixed habits (centre / the corner's inside line x left / right / up, banked into the
corner); reader (SpearlineSeals.Read from the line, any roll); and the AI coach at the four difficulties (Read from the
centre with its three strokes, the controller's miss-read chance and lane error).

What it measures (the bars are in BARS):
  M1 the hull alone never opens a seal on the first pass
  M2 reading the seals saves time against the best FIXED habit: about nothing at I1 (the intro: any stroke opens), real
     at I2-I4
  M3 the race lands near 90 s for a human reader (k x model), 5 laps; 4 laps falls short at I1 (the negative control)
  M4 every AI finishes every seal (the worn seal bounds the passes), and the ladder costs the AI more as it climbs
  M5 Legendary races within 25% of the reader

What it cannot say: whether the bill really cuts the band the model says (its width is ModelStrokeReach, read off the
capsule, not measured in play), anything about feel, and whether a human reads armour as fast as the model.

    py -3 Tools/Build/spearline_race_model.py               # the table, 40 seeds a rung
    py -3 Tools/Build/spearline_race_model.py --seeds 100
    py -3 Tools/Build/spearline_race_model.py --self-test   # constants match the shipped assets; the bars' controls fire
"""
import json
import math
import os
import re
import statistics
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import headlong_stampede_model as HS  # noqa: E402

LAPS = 5                         # SpearlineCourseSource.DefaultLaps
HUMAN_I1_SECONDS_PER_GATE = 2.13  # flight_Headlong_Rhino_20261112_225220.json (origin/bugledger/owner), I1, 40 rings
HUMAN_I4_SECONDS_PER_GATE = 3.65  # ...225604.json, I4 (its Time-burn spiral is Headlong's, not Spearline's)
MISS_READ = [0.3, 0.15, 0.05, 0.0]   # SpearlineController.aiMissReadByDifficulty (and Runner.cs)
LANE_ERROR = [10.0, 7.0, 4.0, 2.0]   # SpearlineController.aiLaneErrorByDifficulty (and Runner.cs)
FIXED = ["centre_left", "centre_right", "centre_up", "line_left", "line_right", "line_up"]
AIS = ["ai_easy", "ai_medium", "ai_hard", "ai_legendary"]
BARS = dict(
    reading_saves={1: (-0.01, 0.03), 2: (0.05, 0.60), 3: (0.05, 0.60), 4: (0.05, 0.60)},
    human_seconds=(75.0, 115.0),
    legendary_over_reader=1.25,
)


def export(intensity, seeds):
    out = subprocess.run(["bash", os.path.join(HERE, "spearline_harness", "run.sh"), "--export", str(intensity)]
                         + [str(s) for s in seeds], capture_output=True, text=True, check=True).stdout
    return [json.loads(l) for l in out.splitlines() if l.strip().startswith("{")]


def loop():
    """The fastest full loop the airframe flies: (seconds, speed on it)."""
    best = None
    for r in range(120, 900, 10):
        v = HS.corner_speed(float(r))
        t = 2 * math.pi * r / v
        if best is None or t < best[0]:
            best = (t, v)
    return best


LOOP_SECONDS, LOOP_SPEED = loop()


def race_time(course, policy, laps=LAPS, seals=True):
    """One pilot's race under `policy` (a key of each seal's policies), model seconds."""
    g = [x["p"] for x in course["gates"]]
    n = len(g)
    by_ring = {s["ring"]: s for s in course["seals"]}
    t, v = 0.0, HS.START_SPEED
    for k in range(n * laps):
        i = k % n
        r = HS.corner_radius(g[i - 1], g[i], g[(i + 1) % n])
        if k > 0:
            v = min(v, HS.corner_speed(r))
        seal = by_ring.get(i) if seals else None
        if seal and k > 0:
            passes, dev = seal["policies"][policy]
            turn = math.radians(course["gates"][i]["turn"])
            t += dev * (turn + 0.02) / max(v, 1.0)
            if passes > 1:
                t += (passes - 1) * LOOP_SECONDS
                v = min(v, LOOP_SPEED)
        length = math.dist(g[i], g[(i + 1) % n])
        dt, v = HS.run_to(v, length, HS.ACCEL)
        t += dt
    return t


def human_factor(courses_i1, courses_i4):
    bare1 = statistics.median(race_time(c, "hull", laps=5, seals=False) for c in courses_i1) / 40.0
    bare4 = statistics.median(race_time(c, "hull", laps=5, seals=False) for c in courses_i4) / 40.0
    k = HUMAN_I1_SECONDS_PER_GATE / bare1
    return k, bare1, bare4


def measure(seeds, laps=LAPS):
    courses = {i: export(i, seeds) for i in range(1, 5)}
    k, bare1, bare4 = human_factor(courses[1], courses[4])
    rows = {}
    for i in range(1, 5):
        cs = courses[i]
        med = {p: statistics.median(race_time(c, p, laps) for c in cs) for p in ["hull", "reader"] + FIXED + AIS}
        best_fixed = min(FIXED, key=lambda p: med[p])
        seals = [s for c in cs for s in c["seals"]]
        first_pass_hull = sum(s["policies"]["hull"][0] == 1 for s in seals) / len(seals)
        max_ai_passes = max(s["policies"][a][0] for s in seals for a in AIS)
        closed_per_lap = {a: statistics.mean(sum(s["policies"][a][0] - 1 for s in c["seals"]) for c in cs) for a in AIS}
        reader_closed = statistics.mean(sum(s["policies"]["reader"][0] - 1 for s in c["seals"]) for c in cs)
        fixed_closed = statistics.mean(sum(s["policies"][best_fixed][0] - 1 for s in c["seals"]) for c in cs)
        rows[i] = dict(med=med, best_fixed=best_fixed, saves=1 - med["reader"] / med[best_fixed],
                       hull_first=first_pass_hull, max_ai_passes=max_ai_passes, closed_per_lap=closed_per_lap,
                       reader_closed=reader_closed, fixed_closed=fixed_closed, human=k * med["reader"],
                       human_fixed=k * med[best_fixed], seals_per_lap=len(seals) / len(cs))
    return rows, k, bare1, bare4


def verdicts(rows, laps=LAPS):
    fails = []
    for i, r in rows.items():
        if r["hull_first"] > 0:
            fails.append(f"M1 I{i}: the hull alone opened {r['hull_first']:.0%} of seals on the first pass")
        lo, hi = BARS["reading_saves"][i]
        if not lo <= r["saves"] <= hi:
            fails.append(f"M2 I{i}: reading saves {r['saves']:.1%}, outside {lo:.0%}..{hi:.0%}")
        lo, hi = BARS["human_seconds"]
        if not lo <= r["human"] <= hi:
            fails.append(f"M3 I{i}: a human reader takes {r['human']:.0f} s over {laps} laps, outside {lo:.0f}-{hi:.0f}")
        if r["max_ai_passes"] > 6:
            fails.append(f"M4 I{i}: an AI needed {r['max_ai_passes']} passes at one seal")
        if r["med"]["ai_legendary"] > BARS["legendary_over_reader"] * r["med"]["reader"]:
            fails.append(f"M5 I{i}: Legendary {r['med']['ai_legendary']:.1f} s vs reader {r['med']['reader']:.1f} s")
    ai_cost = [rows[i]["closed_per_lap"]["ai_hard"] for i in range(1, 5)]
    if not ai_cost[3] >= ai_cost[0]:
        fails.append(f"M4: the AI's closed passes do not grow up the ladder: {ai_cost}")
    return fails


def report(seeds):
    rows, k, bare1, bare4 = measure(seeds)
    print(f"Spearline race model: {len(seeds)} courses a rung, {LAPS} laps, top {HS.TOP:.0f} u/s; a closed pass costs "
          f"{LOOP_SECONDS:.1f} s (a loop at {LOOP_SPEED:.0f} u/s)")
    print(f"human factor k = {k:.2f} (bare circuit {bare1:.2f} s/gate at I1 in the model vs {HUMAN_I1_SECONDS_PER_GATE} "
          f"recorded); at I4 the model's bare circuit x k = {k * bare4:.2f} s/gate vs {HUMAN_I4_SECONDS_PER_GATE} recorded "
          f"(Headlong's I4 burn spiral; {abs(k * bare4 / HUMAN_I4_SECONDS_PER_GATE - 1):.0%} off)")
    print(f"{'':3} {'seals':>5} {'hull':>6} {'best fixed':>18} {'reader':>7} {'saves':>6} {'human s':>8} "
          f"{'fixed s':>8} {'easy':>6} {'med':>6} {'hard':>6} {'legend':>6}  closed/lap: reader fixed easy legend")
    for i, r in rows.items():
        m = r["med"]
        print(f"I{i}: {r['seals_per_lap']:5.1f} {m['hull']:6.1f} {r['best_fixed']:>12} {m[r['best_fixed']]:5.1f} "
              f"{m['reader']:7.1f} {r['saves']:6.1%} {r['human']:8.0f} {r['human_fixed']:8.0f} {m['ai_easy']:6.1f} "
              f"{m['ai_medium']:6.1f} {m['ai_hard']:6.1f} {m['ai_legendary']:6.1f}  "
              f"{r['reader_closed']:6.2f} {r['fixed_closed']:5.2f} {r['closed_per_lap']['ai_easy']:5.2f} "
              f"{r['closed_per_lap']['ai_legendary']:6.2f}")
    fails = verdicts(rows)
    for f in fails:
        print("FAIL", f)
    print("bars:", "all held" if not fails else f"{len(fails)} failed")
    return rows, fails


def self_test(seeds):
    ok = True
    ctl = open(os.path.join(ROOT, "Assets/_Scripts/Controller/Arcade/Spearline/SpearlineController.cs"),
               encoding="utf-8").read()
    runner = open(os.path.join(HERE, "spearline_harness", "Runner.cs"), encoding="utf-8").read()

    def row(src, name):
        m = re.search(name + r"\s*=\s*\{([^}]*)\}", src)
        return [float(x.strip().rstrip("f")) for x in m.group(1).split(",")] if m else None
    for name, mine, theirs_ctl, theirs_runner in (
            ("aiMissReadByDifficulty", MISS_READ, row(ctl, "aiMissReadByDifficulty"), row(runner, "MissRead")),
            ("aiLaneErrorByDifficulty", LANE_ERROR, row(ctl, "aiLaneErrorByDifficulty"), row(runner, "LaneError"))):
        if not (mine == theirs_ctl == theirs_runner):
            print(f"DRIFT: {name}: model {mine}, controller {theirs_ctl}, Runner {theirs_runner}")
            ok = False
    src = open(os.path.join(ROOT, "Assets/_Scripts/Controller/Arcade/Spearline/SpearlineCourseSource.cs"),
               encoding="utf-8").read()
    if f"DefaultLaps = {LAPS};" not in src:
        print(f"DRIFT: SpearlineCourseSource.DefaultLaps is not {LAPS}")
        ok = False
    # The Swordfish's own ramp asset carries the numbers the model flies (the Rhino's), Time scaling off.
    sw = open(os.path.join(ROOT, "Assets/_SO_Assets/VesselActions/Swordfish/SwordfishRampBoostAction.asset"),
              encoding="utf-8").read()
    rh = open(os.path.join(ROOT, "Assets/_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset"),
              encoding="utf-8").read()
    for key in ("maxBoostMultiplier", "accelerationPerSecond"):
        a = re.search(rf"{key}:\s*([0-9.]+)", sw)
        b = re.search(rf"{key}:\s*([0-9.]+)", rh)
        if not (a and b and float(a.group(1)) == float(b.group(1))):
            print(f"DRIFT: the Swordfish ramp's {key} differs from the Rhino's the model flies")
            ok = False
    if not re.search(r"timeAccelerationMultiplier:\s*\n\s+Enabled: 0", sw):
        print("DRIFT: the Swordfish ramp scales with Time now - the model flies it at Time 0")
        ok = False

    rows, fails = report(seeds)
    if fails:
        ok = False
    # Negative controls: each must make a bar fail.
    if not any(f.startswith("M3") for f in verdicts(measure(seeds, laps=4)[0], laps=4)):
        print("NEGATIVE CONTROL DID NOT FIRE: 4 laps passes the length bar")
        ok = False
    else:
        print("negative control fired: 4 laps falls short of the length bar")
    flat = {i: dict(r, saves=0.0) for i, r in rows.items()}
    if not any(f.startswith("M2 I3") for f in verdicts(flat)):
        print("NEGATIVE CONTROL DID NOT FIRE: a reader no faster than the habit passes M2")
        ok = False
    else:
        print("negative control fired: a reader no better than the best habit fails M2")
    slow = {i: dict(r, med=dict(r["med"], ai_legendary=2 * r["med"]["reader"])) for i, r in rows.items()}
    if not any(f.startswith("M5") for f in verdicts(slow)):
        print("NEGATIVE CONTROL DID NOT FIRE: a Legendary at twice the reader passes M5")
        ok = False
    else:
        print("negative control fired: a Legendary at twice the reader fails M5")
    print("spearline_race_model self-test:", "ok" if ok else "FAILED")
    return ok


if __name__ == "__main__":
    n = int(sys.argv[sys.argv.index("--seeds") + 1]) if "--seeds" in sys.argv else 40
    seeds = list(range(1, n + 1))
    if "--self-test" in sys.argv:
        sys.exit(0 if self_test(seeds) else 1)
    report(seeds)
