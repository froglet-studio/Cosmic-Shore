#!/usr/bin/env python3
"""The circuit generator every lapped gate race builds its course with, in Python, for the compute evaluators.

A line-for-line port of three pure C# files, so a compute job can ask "what course do THESE settings make?" on any
contributor's machine without a .NET SDK:

    Assets/_Scripts/Controller/Arcade/Racing/RaceCourseGeometry.cs   (Rng, SafeNormalize, Angle, Deflect, CornerRadius)
    Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs    (Generate: the profile solve, relaxation, gates)
    Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourse.cs       (ForIntensity, BuildRails: the three-lane braid)

and of the UnityEngine stub the repo's own course harness compiles them against
(Tools/Build/regatta_course_harness/Stubs.cs: FromToRotation, Vector3.Angle, SignedAngle, RoundToInt). Every float is a
numpy float32 and every operation is rounded where the C# rounds it, so the port reproduces the harness's output, not
merely its shape.

THE GATE (`--self-test`): the port must reproduce, for the shipped settings at all four intensities, the two files the
C# harness wrote -- Tools/Build/regatta_course_harness/course.js (every gate position, axis and radius, every lane
point) and Tools/Build/regatta_course_measurements.json (spine and lane lengths, corner radii, leg lengths, gate 0) --
to their printed precision. A negative control moves one corner of the I3 profile by a degree and must FAIL. If a
C# file above changes, this gate goes red the next time someone regenerates the harness output: re-port the change.

    python3 Tools/Compute/evaluators/course_port.py --self-test
    python3 Tools/Compute/evaluators/course_port.py --print 3      # one course, as JSON
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))

f = np.float32
F0, F1, F2 = f(0.0), f(1.0), f(2.0)
PI = f(math.pi)
DEG2RAD = PI / f(180.0)
RAD2DEG = f(180.0) / PI
UP = (F0, F1, F0)
RIGHT = (F1, F0, F0)
FORWARD = (F0, F0, F1)
ZERO = (F0, F0, F0)


# ── UnityEngine stub (Stubs.cs) ───────────────────────────────────────────────

def v3(x, y, z): return (f(x), f(y), f(z))
def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def div(a, s): return (a[0] / s, a[1] / s, a[2] / s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def sqr(a): return dot(a, a)
def fsin(x): return f(math.sin(float(x)))
def fcos(x): return f(math.cos(float(x)))
def ftan(x): return f(math.tan(float(x)))
def facos(x): return f(math.acos(float(x)))
def fsqrt(x): return f(math.sqrt(float(x)))
def clamp(v, a, b): return a if v < a else (b if v > b else v)
def clamp01(v): return clamp(v, F0, F1)


def magnitude(a): return fsqrt(sqr(a))


def normalized(a):
    m = magnitude(a)
    return div(a, m) if m > f(1e-5) else ZERO


def lerp3(a, b, t):
    t = clamp01(t)
    return add(a, mul(sub(b, a), t))


def stub_angle(a, b):
    """Vector3.Angle as the harness stub computes it."""
    d = fsqrt(sqr(a) * sqr(b))
    if d < f(1e-15):
        return F0
    return facos(clamp(dot(a, b) / d, f(-1.0), F1)) * RAD2DEG


def signed_angle(frm, to, axis):
    u = stub_angle(frm, to)
    s = F1 if dot(axis, cross(frm, to)) >= F0 else f(-1.0)
    return u * s


def angle_axis(deg, axis):
    axis = normalized(axis)
    h = deg * DEG2RAD * f(0.5)
    s = fsin(h)
    return (axis[0] * s, axis[1] * s, axis[2] * s, fcos(h))


def from_to_rotation(frm, to):
    a, b = normalized(frm), normalized(to)
    d = dot(a, b)
    if d > f(0.999999):
        return (F0, F0, F0, F1)
    if d < f(-0.999999):
        ax = cross(RIGHT, a)
        if sqr(ax) < f(1e-6):
            ax = cross(UP, a)
        return angle_axis(f(180.0), ax)
    c = cross(a, b)
    s = fsqrt((F1 + d) * F2)
    return (c[0] / s, c[1] / s, c[2] / s, s * f(0.5))


def qmul(q, v):
    x, y, z = q[0] * F2, q[1] * F2, q[2] * F2
    xx, yy, zz = q[0] * x, q[1] * y, q[2] * z
    xy, xz, yz = q[0] * y, q[0] * z, q[1] * z
    wx, wy, wz = q[3] * x, q[3] * y, q[3] * z
    return ((F1 - (yy + zz)) * v[0] + (xy - wz) * v[1] + (xz + wy) * v[2],
            (xy + wz) * v[0] + (F1 - (xx + zz)) * v[1] + (yz - wx) * v[2],
            (xz - wy) * v[0] + (yz + wx) * v[1] + (F1 - (xx + yy)) * v[2])


def round_to_int(v):
    return int(round(float(v)))   # Python's round() is banker's rounding, as Math.Round(v, ToEven)


# ── RaceCourseGeometry.cs ─────────────────────────────────────────────────────

class Rng:
    """RaceCourseGeometry.Rng: xorshift32."""

    def __init__(self, seed):
        s = int(seed) & 0xFFFFFFFF
        self.s = s if s != 0 else 0x9E3779B9

    def next_uint(self):
        x = self.s
        x ^= (x << 13) & 0xFFFFFFFF
        x ^= x >> 17
        x ^= (x << 5) & 0xFFFFFFFF
        self.s = x
        return x

    def unit(self):
        return f(self.next_uint()) / f(4294967296.0)

    def range(self, a, b):
        a, b = f(a), f(b)
        return a + (b - a) * self.unit()


def safe_normalize(v, fallback):
    return normalized(v) if sqr(v) > f(1e-10) else fallback


def angle(a, b):
    return facos(clamp(dot(safe_normalize(a, FORWARD), safe_normalize(b, FORWARD)), f(-1.0), F1)) * RAD2DEG


def perpendicular(v):
    a = RIGHT if abs(v[0]) < f(0.9) else UP
    return safe_normalize(cross(v, a), UP)


def rotate_about(v, axis, radians):
    c, s = fcos(radians), fsin(radians)
    return add(add(mul(v, c), mul(cross(axis, v), s)), mul(axis, dot(axis, v) * (F1 - c)))


def deflect(rng, v, max_degrees):
    if max_degrees <= F0:
        return v
    u = perpendicular(v)
    w = cross(v, u)
    phi = rng.range(0.0, F2 * PI)
    spin = safe_normalize(add(mul(u, fcos(phi)), mul(w, fsin(phi))), u)
    ang = max_degrees * DEG2RAD * fsqrt(rng.unit())
    return safe_normalize(rotate_about(v, spin, ang), v)


def corner_radius(previous, p, nxt):
    inbound, outbound = sub(p, previous), sub(nxt, p)
    turn = angle(inbound, outbound)
    if turn < f(1e-4):
        return float("inf")
    tangent = min(magnitude(inbound), magnitude(outbound)) * f(0.5)
    return tangent / ftan(turn * f(0.5) * DEG2RAD)


def min_turn_radius(speed, rts, turn):
    omega = f(speed) * f(rts) + f(turn)
    return float("inf") if omega <= f(1e-4) else f(speed) / (omega * DEG2RAD)


# ── HeadlongCircuit.cs ────────────────────────────────────────────────────────

RHINO_TOP_SPEED = f(50.0) * f(16.8) + f(0.0)
FLAT_OUT_RADIUS = min_turn_radius(RHINO_TOP_SPEED, 0.2, 90.0) / f(0.28)
RELAXATION_STEPS, RELAXATION_RATE, PROFILE_SWEEPS, BISECTION_STEPS, SHELL_MARGIN = 48, f(0.88), 6, 24, f(24.0)

SETTING_KEYS = ("GateCount", "RingRadius", "BaseRadius", "LateralPerturbation", "CornerRadiusFactor",
                "CornerFloorRadius", "CornerProfile", "RadialSwing", "AngularSpread", "InnerRadius", "OuterRadius",
                "AxisJitterDegrees", "MaxPresentDegrees", "FirstGateDirection")


def _settings(s):
    """Fill a settings dict the way a C# struct initializer leaves unnamed fields: zero."""
    out = {k: 0.0 for k in SETTING_KEYS}
    out["GateCount"], out["CornerProfile"], out["FirstGateDirection"] = 8, None, (0.0, 0.0, 0.0)
    out.update(s)
    return out


def _flat_ring(sharp, s, n, scale):
    gap, total, mean_sharp = [], F0, F0
    for i in range(n):
        squeeze = max(sharp[i], sharp[(i + 1) % n])
        g = F1 + f(s["AngularSpread"]) * scale * (F1 - squeeze)
        gap.append(g)
        total = total + g
        mean_sharp = mean_sharp + sharp[i]
    mean_sharp = mean_sharp / f(n)
    inner, outer = f(s["InnerRadius"]) + SHELL_MARGIN, f(s["OuterRadius"]) - SHELL_MARGIN
    pts, ang = [], F0
    for i in range(n):
        r = f(s["BaseRadius"]) * (F1 + F2 * f(s["RadialSwing"]) * scale * (sharp[i] - mean_sharp))
        if outer > inner:
            r = clamp(r, inner, outer)
        pts.append((r * fcos(ang), r * fsin(ang), F0))
        ang = ang + F2 * PI * gap[i] / total
    return pts


def _flat_turn_degrees(sharp, s, n, i):
    pts = _flat_ring(sharp, s, n, F1)
    return angle(sub(pts[i], pts[(i - 1 + n) % n]), sub(pts[(i + 1) % n], pts[i]))


def _solve_profile(sharp, targets, s, n):
    for _ in range(PROFILE_SWEEPS):
        for i in range(n):
            lo, hi = F0, F1
            for _ in range(BISECTION_STEPS):
                mid = f(0.5) * (lo + hi)
                sharp[i] = mid
                if _flat_turn_degrees(sharp, s, n, i) < targets[i]:
                    lo = mid
                else:
                    hi = mid
            sharp[i] = f(0.5) * (lo + hi)


def _shuffled_targets(rng, n, s):
    profile = s["CornerProfile"]
    targets = [f(profile[i % len(profile)]) if profile else f(360.0) / f(n) for i in range(n)]
    targets.sort(reverse=True)
    placed, slot = [F0] * n, 0
    for k in range(n):
        placed[slot] = targets[k]
        slot += 2
        if slot >= n:
            slot = 1
    rotate = min(max(int(rng.range(0.0, f(n))), 0), n - 1)
    flip = rng.range(0.0, 1.0) < f(0.5)
    return [placed[((n - i) % n if flip else i + rotate) % n] for i in range(n)]


def _build(sharp, lateral, u, w, normal, phase, s, n, scale):
    flat = _flat_ring(sharp, s, n, scale)
    cp, sp = fcos(phase), fsin(phase)
    pts = []
    for i in range(n):
        x = flat[i][0] * cp - flat[i][1] * sp
        y = flat[i][0] * sp + flat[i][1] * cp
        pts.append(add(add(mul(u, x), mul(w, y)), mul(normal, lateral[i] * scale)))
    return pts


def _is_legal(pts, floor, inner, outer, ring_radius):
    n = len(pts)
    min_sep = ring_radius * F2
    for i in range(n):
        r = magnitude(pts[i])
        if r < inner or r > outer:
            return False
        if corner_radius(pts[(i - 1 + n) % n], pts[i], pts[(i + 1) % n]) < floor:
            return False
        for j in range(i + 1, n):
            if sqr(sub(pts[i], pts[j])) < min_sep * min_sep:
                return False
    return True


def generate(seed, settings):
    """HeadlongCircuit.Generate: a list of gates {p, a, r} (float32 tuples), cell-local."""
    s = _settings(settings)
    n = max(3, int(s["GateCount"]))
    rng = Rng(seed)
    normal = safe_normalize((rng.range(-1, 1), rng.range(-1, 1), rng.range(-1, 1)), UP)
    u = perpendicular(normal)
    w = cross(normal, u)
    phase = rng.range(0.0, F2 * PI)
    targets = _shuffled_targets(rng, n, s)
    lp = f(s["LateralPerturbation"])
    lateral = [rng.range(-lp, lp) for _ in range(n)]
    sharp = [f(0.5)] * n
    _solve_profile(sharp, targets, s, n)
    floor = f(s["CornerFloorRadius"]) if f(s["CornerFloorRadius"]) > F0 else f(s["CornerRadiusFactor"]) * FLAT_OUT_RADIUS
    scale, pts = F1, None
    for step in range(RELAXATION_STEPS + 1):
        pts = _build(sharp, lateral, u, w, normal, phase, s, n, scale)
        if _is_legal(pts, floor, f(s["InnerRadius"]), f(s["OuterRadius"]), f(s["RingRadius"])):
            break
        scale = scale * RELAXATION_RATE
        if step == RELAXATION_STEPS:
            pts = _build(sharp, lateral, u, w, normal, phase, s, n, F0)
    pole = safe_normalize(tuple(f(x) for x in s["FirstGateDirection"]), UP)
    align = from_to_rotation(safe_normalize(pts[0], pole), pole)
    pts = [qmul(align, p) for p in pts]
    gates = []
    jit_cap, present = f(s["AxisJitterDegrees"]), f(s["MaxPresentDegrees"])
    for i in range(n):
        inbound = safe_normalize(sub(pts[i], pts[(i - 1 + n) % n]), FORWARD)
        outbound = safe_normalize(sub(pts[(i + 1) % n], pts[i]), inbound)
        axis = safe_normalize(add(inbound, outbound), inbound)
        half_turn = angle(inbound, outbound) * f(0.5)
        jitter = max(F0, min(jit_cap, present - half_turn))
        gates.append({"p": pts[i], "a": deflect(rng, axis, jitter), "r": f(s["RingRadius"])})
    return gates


# ── RegattaCourse.cs ──────────────────────────────────────────────────────────

REGATTA_DEFAULT_SEED = 20260915
REGATTA_INNER, REGATTA_OUTER, REGATTA_BASE = 478.0, 1080.0, 820.0
REGATTA_RAILS = {"LaneOffset": 22.0, "TwistTurnsPerLap": 1.0, "PrismSpacing": 8.0, "SamplesPerLeg": 64}
LANE_COUNT = 3


def seed_for_intensity(base_seed, intensity):
    v = (int(base_seed) + 7919 * min(max(int(intensity), 1), 4)) & 0xFFFFFFFF
    return v - (1 << 32) if v >= (1 << 31) else v


def regatta_settings(intensity):
    """RegattaCourse.ForIntensity, as plain numbers (the knobs a course search moves)."""
    i = min(max(int(intensity), 1), 4) - 1
    return {
        "GateCount": 8,
        "BaseRadius": REGATTA_BASE,
        "CornerProfile": [[90, 72, 58, 48, 38, 28, 16, 10], [118, 92, 66, 40, 22, 12, 6, 4],
                          [135, 112, 78, 22, 7, 3, 2, 1], [150, 124, 86, 0, 0, 0, 0, 0]][i],
        "CornerRadiusFactor": 0.0,
        "CornerFloorRadius": [200.0, 150.0, 120.0, 100.0][i],
        "RadialSwing": 0.42,
        "AngularSpread": [1.2, 3.0, 2.8, 4.8][i],
        "LateralPerturbation": [160.0, 200.0, 240.0, 280.0][i],
        "RingRadius": [110.0, 88.0, 72.0, 54.0][i],
        "AxisJitterDegrees": [20.0, 28.0, 36.0, 44.0][i],
        "MaxPresentDegrees": [50.0, 72.0, 76.0, 80.0][i],
        "InnerRadius": REGATTA_INNER,
        "OuterRadius": REGATTA_OUTER,
        "FirstGateDirection": (0.0, 1.0, 0.0),
    }


def headlong_settings(intensity):
    """HeadlongCircuitSettings.ForIntensity (the Rhino's circuit), with the shell a Headlong cell resolves to
    (RaceCourseSource.ResolveShell: the nucleus x 1.22, 1080) and gate 0 on the spawn pole."""
    i = min(max(int(intensity), 1), 4) - 1
    return {
        "GateCount": 8, "BaseRadius": 800.0,
        "CornerProfile": [[125, 90, 60, 40, 25, 12, 5, 3], [120, 95, 70, 40, 20, 8, 5, 2],
                          [135, 110, 80, 25, 6, 2, 1, 1], [150, 122, 88, 0, 0, 0, 0, 0]][i],
        "CornerRadiusFactor": [0.75, 0.42, 0.28, 0.20][i], "CornerFloorRadius": 0.0,
        "RadialSwing": 0.42, "AngularSpread": [1.2, 2.0, 2.8, 3.6][i],
        "LateralPerturbation": [120.0, 170.0, 215.0, 260.0][i], "RingRadius": [96.0, 72.0, 58.0, 46.0][i],
        "AxisJitterDegrees": [20.0, 28.0, 36.0, 44.0][i], "MaxPresentDegrees": [50.0, 64.0, 74.0, 78.0][i],
        "InnerRadius": REGATTA_INNER, "OuterRadius": REGATTA_OUTER, "FirstGateDirection": (0.0, 1.0, 0.0),
    }


def redline_settings(intensity):
    """RedlineCourse.ForIntensity (the Manta's circuit), same shell as headlong_settings."""
    i = min(max(int(intensity), 1), 4) - 1
    full = 720.0 / ((720.0 * 0.2 + 30.0) * math.pi / 180.0)   # RedlineCourse.FullBoostRadius
    return {
        "GateCount": 8, "BaseRadius": 820.0,
        "CornerProfile": [[90, 72, 58, 48, 38, 28, 16, 10], [118, 92, 66, 40, 22, 12, 6, 4],
                          [135, 112, 78, 22, 7, 3, 2, 1], [150, 124, 86, 0, 0, 0, 0, 0]][i],
        "CornerRadiusFactor": 0.0, "CornerFloorRadius": [0.75, 0.45, 0.42, 0.38][i] * full,
        "RadialSwing": 0.42, "AngularSpread": [1.2, 3.0, 2.8, 4.8][i],
        "LateralPerturbation": [160.0, 200.0, 240.0, 280.0][i], "RingRadius": [110.0, 88.0, 72.0, 44.0][i],
        "AxisJitterDegrees": [20.0, 28.0, 36.0, 44.0][i], "MaxPresentDegrees": [50.0, 72.0, 76.0, 80.0][i],
        "InnerRadius": REGATTA_INNER, "OuterRadius": REGATTA_OUTER, "FirstGateDirection": (0.0, 1.0, 0.0),
    }


def hermite(p0, m0, p1, m1, t):
    t2 = t * t
    t3 = t2 * t
    return add(add(add(mul(p0, F2 * t3 - f(3) * t2 + F1), mul(m0, t3 - F2 * t2 + t)),
                   mul(p1, f(-2) * t3 + f(3) * t2)), mul(m1, t3 - t2))


def hermite_derivative(p0, m0, p1, m1, t):
    t2 = t * t
    return add(add(add(mul(p0, f(6) * t2 - f(6) * t), mul(m0, f(3) * t2 - f(4) * t + F1)),
                   mul(p1, f(-6) * t2 + f(6) * t)), mul(m1, f(3) * t2 - F2 * t))


def _transport(x0, t0, r0, x1, t1):
    v1 = sub(x1, x0)
    c1 = dot(v1, v1)
    if c1 < f(1e-10):
        return r0
    rl = sub(r0, mul(v1, F2 / c1 * dot(v1, r0)))
    tl = sub(t0, mul(v1, F2 / c1 * dot(v1, t0)))
    v2 = sub(t1, tl)
    c2 = dot(v2, v2)
    r1 = rl if c2 < f(1e-10) else sub(rl, mul(v2, F2 / c2 * dot(v2, rl)))
    r1 = sub(r1, mul(t1, dot(r1, t1)))
    return safe_normalize(r1, perpendicular(t1))


def _resample(dense, spacing):
    m = len(dense)
    seg, length = [], F0
    for j in range(m):
        d = magnitude(sub(dense[(j + 1) % m], dense[j]))
        seg.append(d)
        length = length + d
    count = max(3, round_to_int(length / max(f(0.5), spacing)))
    step = length / f(count)
    out, j2, into = [], 0, F0
    for k in range(count):
        target = f(k) * step
        while j2 < m - 1 and into + seg[j2] < target:
            into = into + seg[j2]
            j2 += 1
        u = clamp01((target - into) / seg[j2]) if seg[j2] > f(1e-6) else F0
        out.append(lerp3(dense[j2], dense[(j2 + 1) % m], u))
    return out, length


def build_rails(gates, rs=None):
    """RegattaCourse.BuildRails without the prism rotations: {spine, lanes (prism points), laneLengths}."""
    rs = dict(REGATTA_RAILS, **(rs or {}))
    n = len(gates)
    per = max(8, int(rs["SamplesPerLeg"]))
    total = n * per
    spine, tangent = [None] * total, [None] * total
    for i in range(n):
        a, b = gates[i], gates[(i + 1) % n]
        chord = magnitude(sub(b["p"], a["p"]))
        m0, m1 = mul(normalized(a["a"]), chord), mul(normalized(b["a"]), chord)
        for k in range(per):
            t = f(k) / f(per)
            spine[i * per + k] = hermite(a["p"], m0, b["p"], m1, t)
            tangent[i * per + k] = safe_normalize(hermite_derivative(a["p"], m0, b["p"], m1, t), a["a"])
    s = [F0] * (total + 1)
    for j in range(total):
        s[j + 1] = s[j] + magnitude(sub(spine[(j + 1) % total], spine[j]))
    length = s[total]
    normal = [None] * total
    normal[0] = perpendicular(tangent[0])
    for j in range(total - 1):
        normal[j + 1] = _transport(spine[j], tangent[j], normal[j], spine[j + 1], tangent[j + 1])
    wrapped = _transport(spine[total - 1], tangent[total - 1], normal[total - 1], spine[0], tangent[0])
    mismatch = signed_angle(wrapped, normal[0], tangent[0]) * DEG2RAD
    for j in range(total):
        normal[j] = safe_normalize(rotate_about(normal[j], tangent[j], mismatch * (s[j] / length)), normal[j])
    twist = f(rs["TwistTurnsPerLap"]) * F2 * PI
    lanes, lane_lengths = [], []
    for lane in range(LANE_COUNT):
        phase = f(lane) * F2 * PI / f(LANE_COUNT)
        dense = []
        for j in range(total):
            theta = phase + twist * (s[j] / length)
            binorm = cross(tangent[j], normal[j])
            radial = add(mul(normal[j], fcos(theta)), mul(binorm, fsin(theta)))
            dense.append(add(spine[j], mul(radial, f(rs["LaneOffset"]))))
        pts, lane_len = _resample(dense, f(rs["PrismSpacing"]))
        lanes.append(pts)
        lane_lengths.append(lane_len)
    return {"spineLength": float(length), "lanes": lanes, "laneLengths": [float(x) for x in lane_lengths]}


# ── what the evaluators read ──────────────────────────────────────────────────

def course(settings, seed, rails=True):
    """One circuit, measured: gates, legs, corners, spine and lanes. Plain floats (the sims' format:
    situation_features.course_from's {gates: [{p, a, r}], lanes: [[[x, y, z], ...] x 3]})."""
    gates = generate(seed, settings)
    n = len(gates)
    out = {
        "seed": int(seed),
        "gates": [{"p": [float(x) for x in g["p"]], "a": [float(x) for x in g["a"]], "r": float(g["r"])} for g in gates],
        "cornerRadii": [min(1e9, float(corner_radius(gates[(i - 1) % n]["p"], gates[i]["p"], gates[(i + 1) % n]["p"])))
                        for i in range(n)],
        "legLengths": [float(magnitude(sub(gates[(i + 1) % n]["p"], gates[i]["p"]))) for i in range(n)],
        "turnDegrees": [float(angle(sub(gates[i]["p"], gates[(i - 1) % n]["p"]), sub(gates[(i + 1) % n]["p"], gates[i]["p"])))
                        for i in range(n)],
    }
    if rails:
        r = build_rails(gates)
        out["spineLength"] = r["spineLength"]
        out["laneLengths"] = r["laneLengths"]
        out["lanes"] = [[[float(x) for x in p] for p in lane] for lane in r["lanes"]]
    return out


def regatta_course(intensity, settings=None, base_seed=REGATTA_DEFAULT_SEED, rails=True):
    return course(settings or regatta_settings(intensity), seed_for_intensity(base_seed, intensity), rails)


# ── the parity gate ───────────────────────────────────────────────────────────

def _load_course_js():
    sys.path.insert(0, os.path.join(REPO, "Tools", "Build"))
    import situation_features as sf
    return sf.load_course_js()


def self_test():
    bad = []
    js = _load_course_js()
    meas = json.load(open(os.path.join(REPO, "Tools", "Build", "regatta_course_measurements.json"), encoding="utf-8"))
    r1 = lambda x: round(float(x) + 0.0, 1)
    r3 = lambda x: round(float(x) + 0.0, 3)
    for i in (1, 2, 3, 4):
        c = regatta_course(i)
        ref = js[str(i)]
        if c["seed"] != ref["seed"]:
            bad.append(f"I{i} seed {c['seed']} != {ref['seed']}")
        for k, (g, rg) in enumerate(zip(c["gates"], ref["gates"])):
            dp = max(abs(r1(a) - b) for a, b in zip(g["p"], rg["p"]))
            da = max(abs(r3(a) - b) for a, b in zip(g["a"], rg["a"]))
            if dp > 0.11 or da > 0.0011 or abs(g["r"] - rg["r"]) > 1e-6:
                bad.append(f"I{i} gate {k}: position off {dp:.2f}, axis off {da:.4f}")
        for lane, (mine, theirs) in enumerate(zip(c["lanes"], ref["lanes"])):
            flat = [r1(x) for p in mine for x in p]
            if len(flat) != len(theirs):
                bad.append(f"I{i} lane {lane}: {len(flat) // 3} prisms, harness {len(theirs) // 3}")
                continue
            worst = max(abs(a - b) for a, b in zip(flat, theirs))
            if worst > 0.11:
                bad.append(f"I{i} lane {lane}: a point is {worst:.2f} u off")
        m = meas["intensities"][str(i)]
        checks = [("spineLength", [c["spineLength"]], [m["spineLength"]]), ("laneLengths", c["laneLengths"], m["laneLengths"]),
                  ("cornerRadii", c["cornerRadii"], m["cornerRadii"]), ("legLengths", c["legLengths"], m["legLengths"])]
        for name, mine, theirs in checks:
            worst = max(abs(r1(a) - b) for a, b in zip(mine, theirs))
            if worst > 0.11:
                bad.append(f"I{i} {name}: off by {worst:.2f}")
    # Negative control: a one-degree move of one corner must be visible.
    s = regatta_settings(3)
    s["CornerProfile"] = list(s["CornerProfile"])
    s["CornerProfile"][1] += 1
    moved = course(s, seed_for_intensity(REGATTA_DEFAULT_SEED, 3), rails=False)
    ref = js["3"]["gates"]
    if max(abs(a - b) for g, rg in zip(moved["gates"], ref) for a, b in zip(g["p"], rg["p"])) < 0.5:
        bad.append("negative control: a moved corner reproduced the shipped course (the gate cannot see a change)")
    print("course_port self-test: " + ("ok (4 intensities: every gate, lane point and measurement matches the C# harness)"
                                       if not bad else "FAILED\n  " + "\n  ".join(bad[:20])))
    return 0 if not bad else 1


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(self_test())
    if "--print" in sys.argv:
        i = int(sys.argv[sys.argv.index("--print") + 1])
        c = regatta_course(i)
        c.pop("lanes")
        print(json.dumps(c, indent=1))
        sys.exit(0)
    print(__doc__)
