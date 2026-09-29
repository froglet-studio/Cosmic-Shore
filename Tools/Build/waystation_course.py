#!/usr/bin/env python3
"""The offline authority for Waystation's course.

It MIRRORS Assets/_Scripts/Controller/Arcade/Waystation/WaystationCourse.cs - the same xorshift,
the same draw ORDER, the same arithmetic - sweeps every intensity over many seeds, and FAILS on
any course the Butterfly could not fly or the cell could not hold. The tables in the C# were
DERIVED here rather than chosen; `--check` is what stops them drifting.

What it proves, per intensity, over the whole sweep:

  corner   every corner clears the Butterfly's own minimum turning circle by a margin. Read off
           Butterfly.prefab (DefaultMinimumSpeed 12 + DefaultThrottleScaler 55 = 67 u/s top,
           Pitch/YawScaler 45 deg/s, RotationThrottleScaler 0) -> 85.3 u. Not a constant typed
           here: a vessel retune moves it, which is the point of reading it.
  mouth    every ring's whole MOUTH (centre +- its radius) stays inside the cell's shell.
  fold     every fold gap is inside the fold's RESTING reach (ButterflyFoldAction.reachRange),
           because a mode whose top rung needed the Time-5 upgrade would play differently
           depending on whether a pilot found a crystal.
  worth    every fold gap is LONGER than the cluster it leaves, so folding always beats flying.
  gap      no two consecutive clusters interpenetrate.
  cone     the exit gate's facing is within the authored cone EXACTLY - it is that direction
           deflected by that cone and nothing else, so anything else is a transcription error.

`--compile` goes one better than mirroring: it BUILDS and RUNS the shipped C# (Tools/Build/
waystation_harness compiles WaystationCourse.cs and RaceCourseGeometry.cs verbatim out of Assets/
against a Unity shim) and compares gate for gate. Measured: 3,880 gates over 4 intensities x 40
seeds agree to 0.0059 u of position and 0.0005 deg of axis - float32 against float64 on a course
spanning a thousand units, which is what makes "the model verified it" mean "the game does it".
It needs a dotnet SDK, so it is opt-in rather than part of --check.

Three negative controls (--self-test) reproduce the defects that shaped the design: a positional
clamp on the cluster chain instead of a geodesic hop, a positional clamp on the EXIT GATE (which
shortens the one leg a corner is measured on - the design's only walked leg, and the reason the
retired walk-based cluster produced corners at 38 units), and a coil ring used as the aiming
device instead of its own exit gate.
"""

import argparse
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
assert (ROOT / "Assets").is_dir(), f"ROOT is wrong: {ROOT}"

CS_COURSE = ROOT / "Assets/_Scripts/Controller/Arcade/Waystation/WaystationCourse.cs"
BUTTERFLY = ROOT / "Assets/_Prefabs/Spacevessels/Butterfly.prefab"
FOLD_ASSET = ROOT / "Assets/_SO_Assets/VesselActions/Butterfly/ButterflyFoldAction.asset"


# ---------------------------------------------------------------- vector helpers
def norm(v):
    m = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2)
    return (v[0] / m, v[1] / m, v[2] / m) if m > 1e-5 else None


def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def mag(a): return math.sqrt(dot(a, a))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def safe(v, fb): return norm(v) or fb


def angle(a, b):
    return math.degrees(math.acos(max(-1.0, min(1.0, dot(safe(a, (0, 0, 1)), safe(b, (0, 0, 1)))))))


def perp(v):
    a = (1, 0, 0) if abs(v[0]) < 0.9 else (0, 1, 0)
    return safe(cross(v, a), (0, 1, 0))


def rotate_about(v, axis, rad):
    c, s = math.cos(rad), math.sin(rad)
    return add(add(mul(v, c), mul(cross(axis, v), s)), mul(axis, dot(axis, v) * (1 - c)))


class Rng:
    """RaceCourseGeometry.Rng - specified 32-bit xorshift, identical on every runtime."""

    def __init__(self, seed):
        s = seed & 0xFFFFFFFF
        self.s = s if s else 0x9E3779B9

    def next_uint(self):
        x = self.s
        x ^= (x << 13) & 0xFFFFFFFF
        x ^= x >> 17
        x ^= (x << 5) & 0xFFFFFFFF
        self.s = x & 0xFFFFFFFF
        return self.s

    def unit(self): return self.next_uint() / 4294967296.0
    def range(self, a, b): return a + (b - a) * self.unit()


def deflect(rng, v, max_deg):
    if max_deg <= 0:
        return v
    u = perp(v)
    w = cross(v, u)
    phi = rng.range(0.0, 2.0 * math.pi)
    spin = safe(add(mul(u, math.cos(phi)), mul(w, math.sin(phi))), u)
    return safe(rotate_about(v, spin, math.radians(max_deg) * math.sqrt(rng.unit())), v)


def clamp_turn(prev, want, max_deg):
    if angle(prev, want) <= max_deg:
        return want
    ax = cross(prev, want)
    ax = perp(prev) if mag(ax) < 1e-5 else norm(ax)
    return safe(rotate_about(prev, ax, math.radians(max_deg)), prev)


# ---------------------------------------------------------------- the tables, read from the C#
def read_tables():
    """Read ForIntensity's own arrays out of the shipped C#, so the model cannot describe a
    course the game does not lay."""
    src = CS_COURSE.read_text(encoding="utf-8")
    out = {}
    for field in ("RingsPerCluster", "RingRadius", "CoilRadius", "CoilStepDegrees", "CoilPitch",
                  "MinHop", "MaxHop", "ExitConeDegrees"):
        m = re.search(rf"{field}\s*=\s*new\[\]\s*\{{([^}}]*)\}}", src)
        assert m, f"{field} array not found in {CS_COURSE.name}"
        out[field] = [float(x.strip().rstrip("f")) for x in m.group(1).split(",")]
        assert len(out[field]) == 4, f"{field} is not four rungs"
    for field in ("ExitLead", "ExitApproachDegrees", "WanderDegrees", "CentreSphereOuterFraction"):
        m = re.search(rf"{field}\s*=\s*([0-9.]+)f", src)
        assert m, f"{field} scalar not found in {CS_COURSE.name}"
        out[field] = float(m.group(1))
    return out


def read_vessel():
    """The hull's own numbers, off the prefab. A constant copied out of an asset is true only on
    the day it is copied."""
    src = BUTTERFLY.read_text(encoding="utf-8")

    def one(key):
        m = re.search(rf"^\s*{key}:\s*(-?[0-9.]+)\s*$", src, re.M)
        assert m, f"{key} not found in Butterfly.prefab"
        return float(m.group(1))

    top = one("DefaultMinimumSpeed") + one("DefaultThrottleScaler")
    turn = min(one("PitchScaler"), one("YawScaler"))
    scaler = one("RotationThrottleScaler")
    omega = top * scaler + turn
    return top, omega, top / math.radians(omega)


def read_fold_reach():
    src = FOLD_ASSET.read_text(encoding="utf-8")
    m = re.search(r"^\s*reachRange:\s*([0-9.]+)\s*$", src, re.M)
    assert m, "reachRange not found in ButterflyFoldAction.asset"
    return float(m.group(1))


# the cell shell the gate-race platform lays a course inside (GateRaceController's own defaults:
# courseOuterRadius, and the nucleus the inner edge is derived from)
NUCLEUS, COURSE_OUTER = 392.0, 1080.0
INNER = 478.0
PAD = 20.0
CORNER_MARGIN = 1.20


# ---------------------------------------------------------------- the mirror
def generate(seed, intensity, ring_target, T, inner=INNER, outer=COURSE_OUTER,
             clamp_instead_of_geodesic=False, coil_ring_aims=False, clamp_exit_gate=False):
    i = intensity - 1
    per = max(1, int(T["RingsPerCluster"][i]))
    coil_rings = max(1, per - 1)
    rad = T["RingRadius"][i]
    coil_r = T["CoilRadius"][i]
    step = math.radians(T["CoilStepDegrees"][i])
    pitch = T["CoilPitch"][i]
    hlo, hhi = T["MinHop"][i], T["MaxHop"][i]
    cone = T["ExitConeDegrees"][i]
    lead, apprch = T["ExitLead"], T["ExitApproachDegrees"]
    wander, frac = T["WanderDegrees"], T["CentreSphereOuterFraction"]

    centre_radius = max(min(inner, outer), min(outer, outer * frac))
    clusters = max(1, math.ceil(max(1, ring_target) / per))
    rng = Rng(seed)
    gates, clust, centres, folds = [], [], [], []

    pole = (0.0, 1.0, 0.0)
    centre = mul(pole, centre_radius)
    tangent = deflect(rng, perp(pole), 180.0)
    coil_axis = mul(pole, -1)

    for c in range(clusters):
        has_next = c + 1 < clusters
        if has_next:
            radial = safe(centre, pole)
            tangent = deflect(rng, tangent, wander)
            hop = None
            if clamp_instead_of_geodesic:
                # NEGATIVE CONTROL: the deflect-and-clamp walk this design replaced.
                hop = rng.range(hlo, hhi)
                cand = add(centre, mul(tangent, hop))
                r = mag(cand)
                nxt = mul(cand, max(inner, min(outer, r)) / r) if r > 1e-3 else centre
            else:
                tangent = safe(sub(tangent, mul(radial, dot(tangent, radial))), perp(radial))
                hop = rng.range(hlo, hhi)
                sweep = 2.0 * math.asin(min(1.0, hop / (2.0 * centre_radius)))
                axis = safe(cross(radial, tangent), perp(radial))
                nxt = rotate_about(centre, axis, sweep)
                tangent = safe(rotate_about(tangent, axis, sweep), tangent)
        else:
            nxt = add(centre, mul(coil_axis, hlo))

        # --- the coil ---
        axis = safe(coil_axis, (0, 1, 0))
        u = perp(axis)
        w = safe(cross(axis, u), u)
        theta0 = rng.range(0.0, 2.0 * math.pi)
        pts, last, last_leg = [], None, axis
        for k in range(coil_rings):
            th = theta0 + k * step
            rim = add(mul(u, math.cos(th)), mul(w, math.sin(th)))
            p = add(add(centre, mul(rim, coil_r)),
                    mul(axis, (k - (coil_rings - 1) * 0.5) * pitch))
            t = add(mul(add(mul(u, -math.sin(th)), mul(w, math.cos(th))), coil_r * step),
                    mul(axis, pitch))
            gates.append((p, safe(t, axis), rad))
            if k > 0:
                last_leg = safe(sub(p, last), last_leg)
            last = p
            pts.append(p)
        if coil_rings == 1:
            last_leg = axis

        if coil_ring_aims:
            # NEGATIVE CONTROL: roll the coil so its LAST RING aims at the fold, instead of
            # laying an exit gate. A coil ring's facing is its own tangent, so it can be rolled
            # in azimuth and never in elevation.
            exitd = safe(sub(nxt, centre), axis)
            aim = deflect(rng, exitd, cone)
            gates[-1] = (gates[-1][0], aim, rad)
        else:
            to_next = safe(sub(nxt, last), last_leg)
            ep = add(last, mul(clamp_turn(last_leg, to_next, apprch), lead))
            if clamp_exit_gate:
                # NEGATIVE CONTROL: contain the exit gate by pulling its POSITION back into the
                # shell. This is the one walked leg in the whole course, so shortening it is
                # exactly the defect the retired walk-based cluster had everywhere.
                r = mag(ep)
                if r > 1e-3 and not (inner <= r <= outer):
                    ep = mul(ep, max(inner, min(outer, r)) / r)
            facing = deflect(rng, safe(sub(nxt, ep), to_next), cone)
            gates.append((ep, facing, rad))
            pts.append(ep)

        clust.append(pts)
        centres.append(centre)
        if has_next:
            folds.append(mag(sub(nxt, pts[-1])))
            coil_axis = safe(sub(nxt, centre), coil_axis)
            centre = nxt

    return gates, clust, centres, folds, per


def corner_radius(a, b, c):
    inb, out = sub(b, a), sub(c, b)
    t = angle(inb, out)
    if t < 1e-4:
        return float("inf")
    return min(mag(inb), mag(out)) * 0.5 / math.tan(math.radians(t * 0.5))


def measure(intensity, T, seeds, target, **kw):
    i = intensity - 1
    rad, cone = T["RingRadius"][i], T["ExitConeDegrees"][i]
    m = dict(corner=float("inf"), lo=1e9, hi=0.0, extent=0.0,
             fold_lo=1e9, fold_hi=0.0, gap=1e9, cone=0.0, rings=0)
    for s in range(1, seeds + 1):
        gates, clust, centres, folds, per = generate(s, intensity, target, T, **kw)
        m["rings"] = len(gates)
        # the stride is what a cluster ACTUALLY laid, not what the table asks for - a negative
        # control that omits the exit gate lays one fewer, and indexing by the table would read
        # past the end rather than measuring the control.
        stride = len(clust[0]) if clust else per
        for p, _, r in gates:
            m["hi"] = max(m["hi"], mag(p) + r)
            m["lo"] = min(m["lo"], mag(p) - r)
        for pts in clust:
            for k in range(1, len(pts) - 1):
                m["corner"] = min(m["corner"], corner_radius(pts[k - 1], pts[k], pts[k + 1]))
            if len(pts) > 1:
                m["extent"] = max(m["extent"], max(mag(sub(a, b)) for a in pts for b in pts))
        for f in folds:
            m["fold_lo"] = min(m["fold_lo"], f)
            m["fold_hi"] = max(m["fold_hi"], f)
        for c in range(len(clust) - 1):
            gate = gates[(c + 1) * stride - 1]
            m["cone"] = max(m["cone"],
                            angle(gate[1], safe(sub(centres[c + 1], gate[0]), gate[1])))
            m["gap"] = min(m["gap"], min(mag(sub(a, b)) for a in clust[c] for b in clust[c + 1]))
    m["mouth_span"] = 2 * rad
    m["cone_authored"] = cone
    return m


def verdicts(m, hull_r, fold_reach):
    return {
        "corner": m["corner"] > hull_r * CORNER_MARGIN,
        "mouth": m["lo"] >= NUCLEUS + PAD and m["hi"] <= COURSE_OUTER - PAD,
        "fold": m["fold_hi"] < fold_reach,
        "worth": m["fold_lo"] > m["extent"],
        "gap": m["gap"] > m["mouth_span"],
        "cone": m["cone"] <= m["cone_authored"] + 0.5,
    }


def run(seeds, target, quiet=False, **kw):
    T = read_tables()
    top, omega, hull_r = read_vessel()
    fold_reach = read_fold_reach()
    if not quiet:
        print(f"Butterfly  top {top:.0f} u/s, turn {omega:.0f} deg/s "
              f"-> minimum circle {hull_r:.1f} u   (read off Butterfly.prefab)")
        print(f"Fold       resting reach {fold_reach:.0f} u   (ButterflyFoldAction.reachRange)")
        print(f"Cell       course shell [{INNER:.0f}, {COURSE_OUTER:.0f}], nucleus {NUCLEUS:.0f}\n")
    ok = True
    for i in (1, 2, 3, 4):
        m = measure(i, T, seeds, target, **kw)
        v = verdicts(m, hull_r, fold_reach)
        ok = ok and all(v.values())
        if quiet:
            continue
        per = int(T["RingsPerCluster"][i - 1])
        print(f"i{i}: {m['rings']} rings, {per} per cluster ({per - 1} coil + 1 exit)   "
              f"coil R {T['CoilRadius'][i-1]:.0f} pitch {T['CoilPitch'][i-1]:.0f}")
        print(f"    corner  min {m['corner']:7.1f}  x{m['corner']/hull_r:.2f} the hull's circle"
              f"        {'OK' if v['corner'] else 'FAIL'}")
        print(f"    mouth   [{m['lo']:7.1f},{m['hi']:8.1f}] inside "
              f"[{NUCLEUS+PAD:.0f},{COURSE_OUTER-PAD:.0f}]            {'OK' if v['mouth'] else 'FAIL'}")
        print(f"    fold    {m['fold_lo']:7.1f}..{m['fold_hi']:.1f} vs resting reach "
              f"{fold_reach:.0f}         {'OK' if v['fold'] else 'FAIL'}")
        print(f"    worth   shortest fold {m['fold_lo']:.0f} vs widest cluster "
              f"{m['extent']:.0f}       {'OK' if v['worth'] else 'FAIL'}")
        print(f"    gap     min {m['gap']:7.1f} vs two mouths {m['mouth_span']:.0f}"
              f"                  {'OK' if v['gap'] else 'FAIL'}")
        print(f"    cone    max {m['cone']:7.1f} vs authored {m['cone_authored']:.0f}"
              f"                    {'OK' if v['cone'] else 'FAIL'}")
    if not quiet:
        print("\n" + ("ALL CHECKS PASS" if ok else "FAILED"))
    return ok


def self_test(seeds, target):
    """Every negative control must make the named check FAIL, or that check is not measuring what
    it claims. `bounds` tightens the shell for a control that would otherwise be VACUOUS - the
    exit-gate clamp never engages on the shipped course, which is itself evidence for the design
    (containment has margin to spare), and a control that cannot engage proves nothing. Overall
    failure is not asserted separately: if a named check fired, the run failed by definition."""
    T = read_tables()
    _, _, hull_r = read_vessel()
    fold_reach = read_fold_reach()

    print("self-test: the shipped tables")
    assert run(seeds, target, quiet=True), "the shipped tables do not pass their own checks"
    print("  pass\n")

    controls = (
        ("a positional clamp instead of a geodesic hop",
         dict(clamp_instead_of_geodesic=True), None, ("mouth", "worth")),
        ("a positional clamp on the exit gate, in a shell tight enough for it to engage",
         dict(clamp_exit_gate=True), (620.0, 900.0), ("corner",)),
        ("a coil ring as the aiming device instead of an exit gate",
         dict(coil_ring_aims=True), None, ("cone",)),
    )
    for label, kw, bounds, expect in controls:
        print(f"self-test: {label}")
        gen_kw = dict(kw)
        if bounds:
            gen_kw["inner"], gen_kw["outer"] = bounds
        fired = set()
        for i in (1, 2, 3, 4):
            m = measure(i, T, seeds, target, **gen_kw)
            for name, good in verdicts(m, hull_r, fold_reach).items():
                if not good:
                    fired.add(name)
        print(f"  fired: {', '.join(sorted(fired)) or '(nothing)'}")
        missing = [e for e in expect if e not in fired]
        assert not missing, f"expected these to fire and they did not: {missing}"
        print("  pass\n")
    print("SELF-TEST PASS")
    return True


HARNESS = ROOT / "Tools/Build/waystation_harness"
COMPILE_POS_TOLERANCE = 0.05     # units; measured worst 0.0059
COMPILE_AXIS_TOLERANCE = 0.05    # degrees; measured worst 0.0005


def compile_and_compare(seeds, target):
    """Build and run the SHIPPED C#, then compare it to this model gate for gate."""
    import shutil
    import subprocess

    dotnet = shutil.which("dotnet") or str(Path.home() / ".dotnet/dotnet")
    if not Path(dotnet).exists():
        print("--compile needs a dotnet SDK; install one and re-run (see .claude/skills/"
              "asset-surgery). SKIPPED, not passed.")
        return False

    build = subprocess.run([dotnet, "build", "-v", "q", "--nologo"], cwd=HARNESS,
                           capture_output=True, text=True)
    if build.returncode != 0:
        print(build.stdout[-4000:] or build.stderr[-4000:])
        print("the shipped course files DO NOT COMPILE")
        return False

    dll = HARNESS / "bin/Debug/net8.0/waystation_harness.dll"
    run_out = subprocess.run([dotnet, str(dll), str(seeds), str(target)],
                             capture_output=True, text=True)
    if run_out.returncode != 0:
        print(run_out.stderr[-4000:])
        return False

    rows = {}
    for line in run_out.stdout.splitlines():
        f = line.split()
        if len(f) != 10:
            continue
        rows[(int(f[0]), int(f[1]), int(f[2]))] = tuple(float(x) for x in f[3:])

    T = read_tables()
    worst_pos = worst_axis = 0.0
    compared = 0
    for i in (1, 2, 3, 4):
        for seed in range(1, seeds + 1):
            gates, _, _, _, _ = generate(seed, i, target, T)
            laid = sum(1 for k in rows if k[0] == i and k[1] == seed)
            if laid != len(gates):
                print(f"i{i} seed {seed}: the C# laid {laid} gates, the model {len(gates)}")
                return False
            for k, (p, ax, rad) in enumerate(gates):
                cs = rows.get((i, seed, k))
                if cs is None:
                    print(f"i{i} seed {seed} gate {k}: missing from the C# output")
                    return False
                worst_pos = max(worst_pos, mag(sub(p, cs[0:3])))
                worst_axis = max(worst_axis, angle(ax, cs[3:6]))
                if abs(rad - cs[6]) > 1e-3:
                    print(f"i{i} seed {seed} gate {k}: mouth radius disagrees")
                    return False
                compared += 1

    ok = worst_pos <= COMPILE_POS_TOLERANCE and worst_axis <= COMPILE_AXIS_TOLERANCE
    print(f"compiled and RAN the shipped C#: {compared} gates compared")
    print(f"  worst position disagreement {worst_pos:.6f} u   "
          f"(tolerance {COMPILE_POS_TOLERANCE})   {'OK' if worst_pos <= COMPILE_POS_TOLERANCE else 'FAIL'}")
    print(f"  worst axis disagreement     {worst_axis:.6f} deg "
          f"(tolerance {COMPILE_AXIS_TOLERANCE})   {'OK' if worst_axis <= COMPILE_AXIS_TOLERANCE else 'FAIL'}")
    return ok


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="exit 1 on any failing check")
    ap.add_argument("--self-test", action="store_true", help="prove the checks can fail")
    ap.add_argument("--compile", action="store_true",
                    help="build and RUN the shipped C# and compare it gate for gate")
    ap.add_argument("--seeds", type=int, default=200)
    ap.add_argument("--target", type=int, default=24, help="the authored ring target")
    a = ap.parse_args()
    if a.self_test:
        return 0 if self_test(min(a.seeds, 60), a.target) else 1
    if a.compile:
        return 0 if compile_and_compare(min(a.seeds, 40), a.target) else 1
    return 0 if run(a.seeds, a.target) else (1 if a.check else 0)


if __name__ == "__main__":
    sys.exit(main())
