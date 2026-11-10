#!/usr/bin/env python3
"""Headlong's stampede, raced offline: is every choice the ladder asks a REAL one?

A competent-pilot model (Tools/Build/regatta_balance.py's, the same class of artefact): every corner at the fastest
speed the Rhino can hold through it (`regatta_balance.rhino_model`, read off Rhino.prefab and RhinoRampBoostAction),
straights on the ramp's linear wind-up x Ramp Spool (the Time level), braking instant. The course, the hedges and the
crystals are the SHIPPED C# (HeadlongCircuit + HeadlongHedges), exported by Tools/Build/headlong_course_harness.

What the model adds to the lap model, all from the Rhino's shipped kit:
  plough       a plain hedge costs nothing: the hull carries no VesselChangeSpeedByPrismEffect (RHINO_PLOUGH.md)
  burn         ramming a danger hedge burns one petal of every element (the Tuned PetalBurnRule, one contact a
               second, so one per hedge): Time -1, so Ramp Spool -0.15 for the rest of the race
  Momentum     at Time >= 5 and >= 70% of top speed (588 u/s) the burn does not land (VesselElementalImmunity)
  window       a thorn hedge's plain window is a jink of |window - centre| across a run-up: an S of two arcs whose
               radius caps the speed there (the Rhino's own corner curve), then the ramp winds back up
  crystal      a Time crystal inside a corner's apex: +1 Time, for a corner tightened by the inset (70 u)

Pilots (each decision is per event, per race):
  plough       rams every hedge, never takes a crystal       (the naive line)
  dodge        takes every window, never takes a crystal
  greedy       rams every hedge, takes every crystal
  read         decides each event by rolling the rest of the race out under each fixed pilot and taking the
               cheaper branch (the pilot who reads the race)

What a fun ladder shows: at every level the race takes about the target time; from I2 the reader beats every fixed
pilot (a choice that pays only when read); and the levels get harder in order.

    python3 Tools/Build/headlong_stampede_model.py              # the table, 24 seeds a level
    python3 Tools/Build/headlong_stampede_model.py --seeds 60
    python3 Tools/Build/headlong_stampede_model.py --self-test  # the model's constants still match the shipped assets
"""
import json
import math
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
import regatta_balance as RB  # noqa: E402

LAPS = 5                      # HeadlongCourseSource.DefaultLaps (headlongGateTarget 40 = 5 x 8)
START_SPEED = 50.0            # cruise, at the start line
MOMENTUM_FRACTION = 0.7       # Rhino.prefab VesselElementalImmunity.topSpeedFraction
BURN = 1.0                    # Tuned PetalBurnRule: one petal (one level) per element per contact
CRYSTAL = 1.0                 # one petal
APEX_INSET = 70.0             # RedlineTimeCrystals.ApexInset
JINK_RUN_UP = 400.0           # how far before a hedge a pilot starts the S into its window (u)
MIN_LEVEL, MAX_LEVEL = -5.0, 10.0


def shipped():
    c = RB.read_constants()
    ramp = RB._read("Assets/_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset")
    m = re.search(r"timeAccelerationMultiplier:\s*\n(?:\s+\w+:.*\n)*?\s+Max:\s*([0-9.]+)", ramp)
    spool_max = float(m.group(1))
    m = re.search(r"timeAccelerationMultiplier:\s*\n(?:\s+\w+:.*\n)*?\s+Floor:\s*([0-9.]+)", ramp)
    spool_floor = float(m.group(1))
    return c, spool_max, spool_floor


C, SPOOL_MAX, SPOOL_FLOOR = shipped()
MODEL = RB.rhino_model(C, 0)
TOP = MODEL["top"]
ACCEL = C["rhino_accel"]
MOMENTUM = MOMENTUM_FRACTION * TOP


def spool(level):
    return max(SPOOL_FLOOR, 1.0 + (SPOOL_MAX - 1.0) * level / 10.0)


def corner_speed(r):
    return TOP if not math.isfinite(r) else MODEL["corner"](r)


def run_to(v0, d, a, cap=None):
    """Time and speed after `d` u of wind-up from v0 at `a` to TOP (no braking)."""
    top = TOP
    x1 = max(0.0, (top * top - v0 * v0) / (2 * a))
    if x1 >= d:
        v = math.sqrt(v0 * v0 + 2 * a * d)
        return (v - v0) / a, v
    return (top - v0) / a + (d - x1) / top, top


def harness_export(intensity, seeds):
    out = subprocess.run(["bash", os.path.join(HERE, "headlong_course_harness", "run.sh"), "--export", str(intensity)]
                         + [str(s) for s in seeds], capture_output=True, text=True, check=True).stdout
    return [json.loads(l) for l in out.splitlines() if l.strip()]


def corner_radius(prev, at, nxt):
    inbound = [a - b for a, b in zip(at, prev)]
    outbound = [a - b for a, b in zip(nxt, at)]
    na, nb = math.hypot(*inbound), math.hypot(*outbound)
    cosang = max(-1.0, min(1.0, sum(x * y for x, y in zip(inbound, outbound)) / max(na * nb, 1e-9)))
    turn = math.acos(cosang)
    if turn < 1e-6:
        return math.inf
    return min(na, nb) * 0.5 / math.tan(turn * 0.5)


def events_for(course):
    """The race as a list of legs; each leg: (length, corner radius at its start, crystal at its start?, hedges)."""
    g = [x["p"] for x in course["gates"]]
    n = len(g)
    tight = sorted(range(n), key=lambda i: (corner_radius(g[i - 1], g[i], g[(i + 1) % n]), i))
    crystal_corners = set(tight[:len(course["crystals"])])
    by_leg = {}
    for h in course["hedges"]:
        by_leg.setdefault(h["leg"], []).append(h)
    legs = []
    for i in range(n):
        legs.append(dict(
            length=math.dist(g[i], g[(i + 1) % n]),
            radius=corner_radius(g[i - 1], g[i], g[(i + 1) % n]),
            crystal=i in crystal_corners,
            hedges=sorted(by_leg.get(i, []), key=lambda h: h["along"]),
        ))
    return legs


def jink_cap(hedge):
    """The speed an S of |window - centre| across JINK_RUN_UP allows (two arcs: D = 2R(1 - cos t), L = 2R sin t)."""
    d = math.dist(hedge["w"], hedge["c"])
    if d <= 0:
        return TOP
    L = min(JINK_RUN_UP, hedge["along"])
    # R from D and L: L^2 + D^2 = 4 R D  (chord geometry of the S's half)
    r = (L * L + d * d) / (4.0 * d)
    return corner_speed(r)


class Race:
    """State of one pilot's race; `step` advances one leg under decisions (crystal?, per-hedge 'plough'|'window')."""

    def __init__(self, legs, start_level, laps=LAPS):
        self.legs, self.level, self.t, self.v, self.k = legs, float(start_level), 0.0, START_SPEED, 0
        self.total = len(legs) * laps
        self.burns = self.crystals = self.windows = 0

    def copy(self):
        r = Race.__new__(Race)
        r.__dict__.update(self.__dict__)
        return r

    @property
    def done(self):
        return self.k >= self.total

    def leg(self):
        return self.legs[self.k % len(self.legs)]

    def step(self, take_crystal, hedge_choice):
        leg = self.leg()
        first = self.k == 0
        r = leg["radius"]
        if take_crystal and leg["crystal"] and not first:
            r = max(30.0, r - APEX_INSET)
        v = START_SPEED if first else min(self.v, corner_speed(r))
        if take_crystal and leg["crystal"]:
            self.level = min(MAX_LEVEL, self.level + CRYSTAL)
            self.crystals += 1
        x = 0.0
        for h in leg["hedges"]:
            a = ACCEL * spool(self.level)
            choice = hedge_choice(h) if h["kind"] != "Plain" else "plough"
            if choice == "window" and h["wr"] > 0:
                cap = jink_cap(h)
                start = max(x, h["along"] - JINK_RUN_UP)
                dt, v = run_to(v, start - x, a)
                self.t += dt
                # through the S at no more than the cap (instant lift), ramp back to the hedge
                v = min(v, cap)
                dt, v = run_to(v, h["along"] - start, a)
                v = min(v, cap)
                self.t += dt
                self.windows += 1
            else:
                dt, v = run_to(v, h["along"] - x, a)
                self.t += dt
                if h["kind"] != "Plain" and not (self.level >= 5.0 and v >= MOMENTUM):
                    self.level = max(MIN_LEVEL, self.level - BURN)
                    self.burns += 1
            x = h["along"]
        dt, v = run_to(v, leg["length"] - x, ACCEL * spool(self.level))
        self.t += dt
        self.v = v
        self.k += 1


FIXED = {
    "plough": (lambda race, leg: False, lambda h: "plough"),
    "dodge": (lambda race, leg: False, lambda h: "window"),
    "greedy": (lambda race, leg: True, lambda h: "plough"),
    "greedy dodge": (lambda race, leg: True, lambda h: "window"),
}


def finish(race, pilot):
    crystal, hedge = pilot
    while not race.done:
        race.step(crystal(race, race.leg()), hedge)
    return race.t


def read_pilot(race):
    """Decide each leg's choices by rolling the rest out under every fixed pilot, per branch."""
    while not race.done:
        leg = race.leg()
        thorns = [h for h in leg["hedges"] if h["wr"] > 0]
        options = []
        for take in ([False, True] if leg["crystal"] else [False]):
            for mode in (["plough", "window"] if thorns else ["plough"]):
                branch = race.copy()
                branch.step(take, lambda h, m=mode: m)
                best = min(finish(branch.copy(), p) for p in FIXED.values())
                options.append((best, take, mode))
        _, take, mode = min(options, key=lambda o: o[0])
        race.step(take, lambda h, m=mode: m)
    return race.t


def measure(intensity, seeds):
    rows = {name: [] for name in list(FIXED) + ["read"]}
    stats = {"burns": [], "crystals": [], "windows": []}
    for course in harness_export(intensity, seeds):
        legs = events_for(course)
        for name, pilot in FIXED.items():
            rows[name].append(finish(Race(legs, course["start_time"]), pilot))
        r = Race(legs, course["start_time"])
        rows["read"].append(read_pilot(r))
        stats["burns"].append(r.burns)
        stats["crystals"].append(r.crystals)
        stats["windows"].append(r.windows)
    return rows, stats


def mean(xs):
    return sum(xs) / len(xs)


def report(seeds):
    print(f"Headlong stampede model: {len(seeds)} courses a level, {LAPS} laps, top {TOP:.0f} u/s, "
          f"Momentum {MOMENTUM:.0f} u/s, spool x{SPOOL_MAX} at Time 10")
    print(f"{'':4} {'plough':>8} {'dodge':>8} {'greedy':>8} {'gr.dodge':>8} {'read':>8}  {'read vs best fixed':>18}  "
          f"burns  crystals  windows")
    out = {}
    for i in range(1, 5):
        rows, st = measure(i, seeds)
        m = {k: mean(v) for k, v in rows.items()}
        best_fixed = min(m[k] for k in FIXED)
        gain = (best_fixed - m["read"]) / best_fixed
        out[i] = dict(means=m, gain=gain, stats={k: mean(v) for k, v in st.items()})
        print(f"I{i}:  {m['plough']:8.1f} {m['dodge']:8.1f} {m['greedy']:8.1f} {m['greedy dodge']:8.1f} {m['read']:8.1f}  "
              f"{gain:17.1%}   {mean(st['burns']):5.1f}  {mean(st['crystals']):8.1f}  {mean(st['windows']):7.1f}")
    return out


def self_test():
    ok = True
    # The C# constants the stampede is cut with are the shipped asset's.
    src = open(os.path.join(ROOT, "Assets/_Scripts/Controller/Arcade/Headlong/HeadlongHedges.cs")).read()
    accel = float(re.search(r"RampAcceleration = ([0-9.]+)f", src).group(1))
    smax = float(re.search(r"SpoolAtTimeTen = ([0-9.]+)f", src).group(1))
    mom = float(re.search(r"MomentumFraction = ([0-9.]+)f", src).group(1))
    prefab = RB._read("Assets/_Prefabs/Spacevessels/Rhino.prefab")
    frac = float(re.search(r"topSpeedFraction:\s*([0-9.]+)", prefab).group(1))
    for name, a, b in (("accelerationPerSecond", accel, ACCEL), ("spool at Time 10", smax, SPOOL_MAX),
                       ("Momentum fraction", mom, frac)):
        if abs(a - b) > 1e-6:
            print(f"DRIFT: HeadlongHedges {name} {a} != shipped {b}")
            ok = False
    # A burn costs spool; Momentum stops it.
    legs = [dict(length=900.0, radius=math.inf, crystal=False,
                 hedges=[dict(kind="Stampede", along=600.0, wr=0.0, w=[0, 0, 0], c=[0, 0, 0])])]
    slow = Race(legs, 4, laps=1); slow.step(False, lambda h: "plough")
    fast = Race(legs, 5, laps=1); fast.step(False, lambda h: "plough")
    if not (slow.burns == 1 and fast.burns == 0):
        print(f"Momentum check failed: Time 4 burns {slow.burns}, Time 5 burns {fast.burns}")
        ok = False
    print("headlong_stampede_model self-test:", "ok" if ok else "FAILED")
    return ok


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(0 if self_test() else 1)
    n = int(sys.argv[sys.argv.index("--seeds") + 1]) if "--seeds" in sys.argv else 24
    report(list(range(1, n + 1)))
