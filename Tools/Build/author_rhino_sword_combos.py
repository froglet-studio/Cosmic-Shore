#!/usr/bin/env python3
"""Author + prove the Rhino sword COMBO library (RHINO_SWORD_COMBOS.md).

Every two- and three-press trigger sequence (RR, LL, RL, LR, RRR ... LRR) calls its own
flourish, with an upgraded set for an ENERGIZED blade: 12 + 12 = 24 authored paths through the
sword's pose channels (yaw, roll, pitch in degrees; thrust as a fraction of the hilt anchor).

This script is the ONLY author of `RhinoSwordComboLibrary.asset`. It also wires the library onto
`RhinoShieldSwipeConfig.asset`, and before writing anything it RUNS the shipped pose math (a
bit-for-bit mirror of `RhinoSwordComboPath.SamplePath` and `ShieldSwipeActionExecutor.
ApplyShieldPose`) over every path and fails the build unless:

  1. all 12 sequences exist in BOTH sets, each path's keys start at 0, end at 1, and increase;
  2. every path OPENS toward its first press and ENDS on the side of its last press
     (R = right, L = left) -- "it follows the expected left/right motions";
  3. every path is DIFFERENT: the time-aligned mean tip separation between any two paths in a
     set is at least MIN_UNIQUE of the blade length, and each energized path differs from its
     own base path by the same margin -- a mirror, a retime, or a copy all fail this;
  4. every energized path carries MORE flourish than its base (tip travel >= ENERGIZED_TRAVEL x);
  5. no path sweeps the blade through the pilot's camera (Rhino follow offset z = -70) at the
     resting blade length, and the camera clearance at the energized length is REPORTED;
  6. every path ends inside +/-180 on the wrapped channels only by wrapping (checked: the
     runtime wraps the end pose before blending back, so a full turn is never unwound).

  --check   verify the assets on disk match (exit 1 on drift) without writing
  --svg P   also write a top/side-view contact sheet of every tip trajectory to P
  --self-test  prove each gate fires on a deliberately broken library
"""
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from arcade_mode_lib import ROOT, header_for, num, asset_meta_text, script_meta_text  # noqa: E402

LIB_REL = "Assets/_SO_Assets/VesselActions/Rhino/RhinoSwordComboLibrary.asset"
LIB_GUID = "93cd91bfb2a74d648db6762a71dfc3e9"
SCRIPT_REL = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/RhinoSwordComboLibrarySO.cs"
SCRIPT_GUID = "80297f0e643a4fc29678db12756af97d"
DETECTOR_REL = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordComboDetector.cs"
DETECTOR_GUID = "7f6d054b64c84b93b7c6706387b17d7d"
CONFIG_REL = "Assets/_SO_Assets/VesselActions/Rhino/RhinoShieldSwipeConfig.asset"
PREFAB_REL = "Assets/_Prefabs/Spacevessels/Rhino.prefab"
CAMERA_REL = "Assets/_SO_Assets/Camera/RhinoCameraSettingsSO.asset"

# Detection + playback (the SO's own defaults, authored explicitly so the asset is complete).
DETECTION = dict(pressThreshold=0.5, releaseThreshold=0.2, comboWindowSeconds=0.35,
                 tapMaxHoldSeconds=0.3, chordWindowSeconds=0.08, finisherLockoutFraction=0.7,
                 blendInSeconds=0.06, blendOutSeconds=0.18)

SEQUENCES = ["RR", "LL", "RL", "LR", "RRR", "LLL", "RLR", "LRL", "RRL", "LLR", "RLL", "LRR"]

# Gates
MIN_UNIQUE = 0.25          # mean tip separation / blade length between any two paths
ENERGIZED_TRAVEL = 1.2     # energized tip travel >= this x the base path's
CAMERA_CLEARANCE = 20.0    # world units between the blade segment and the camera, resting length
REST_ANCHOR = 30.0         # ForceFieldSkimmer local scale y at rest (blade = 2 x anchor = 60 long)
ENERGIZED_ANCHOR = 60.0    # a charged blade (reported, not gated -- the camera also pulls back)

# ---------------------------------------------------------------------------------------------
# THE DESIGNS. Keys: (t, yaw, roll, pitch, thrust). +yaw right, +roll CCW from the pilot's seat,
# +pitch = chop forward/down (rest is 20 deg forward of vertical; -20 = straight up), thrust =
# fraction of the anchor the blade slides out along itself. A plain right swipe is (90, 90, 0).
# Revolutions are done with the blade RAISED (pitch near -20..-40, roll <= ~60) so the tip
# circles above the ship instead of through the camera behind it.
# ---------------------------------------------------------------------------------------------
BASE = {
    "RR": ("Twin Fang", 0.70, [
        (0.00, 0, 0, 0, 0),
        (0.22, 95, 85, 10, 0),        # first right cut
        (0.42, 25, 15, -30, 0),       # recoil up through centre
        (0.72, 115, 95, 45, 0.15),    # deeper, lower second cut
        (1.00, 40, 30, 10, 0)]),
    "LL": ("Rising Crescent", 0.75, [
        (0.00, 0, 0, 0, 0),
        (0.28, -80, -60, 55, 0),      # low left sweep
        (0.55, -45, -25, -40, 0),     # rising
        (0.80, -15, -10, -75, 0),     # crest overhead, leaning back
        (1.00, -25, -15, -20, 0)]),
    "RL": ("Scissor", 0.65, [
        (0.00, 0, 0, 0, 0),
        (0.28, 90, 90, 15, 0),        # right
        (0.52, 0, 0, 55, 0.25),       # cross low through centre with a push
        (0.82, -95, -90, 15, 0),      # left
        (1.00, -30, -25, 5, 0)]),
    "LR": ("Hook and Draw", 0.75, [
        (0.00, 0, 0, 0, 0),
        (0.28, -70, -100, -25, 0),    # high over-rolled left hook
        (0.55, 10, 20, 40, 0),        # draw down through centre
        (0.82, 85, 60, 70, 0.35),     # low right thrust
        (1.00, 40, 30, 30, 0.1)]),
    "RRR": ("Cyclone", 0.90, [
        (0.00, 0, 0, 0, 0),
        (0.18, 90, 55, -25, 0),       # blade raised and coned outward...
        (0.42, 180, 50, -30, 0),
        (0.66, 270, 50, -30, 0),      # ...one full clockwise turn overhead
        (0.86, 360, 45, -10, 0),
        (1.00, 380, 20, 5, 0)]),      # wraps to 20: ends right
    "LLL": ("Windmill", 0.85, [
        (0.00, 0, 0, 0, 0),
        (0.22, -30, -90, 10, 0),
        (0.46, -30, -180, 10, 0),     # blade down beside the hull
        (0.72, -30, -270, 10, 0),
        (1.00, -15, -360, 0, 0)]),    # one full counter-wheel on the left
    "RLR": ("Figure Eight", 0.90, [
        (0.00, 0, 0, 0, 0),
        (0.17, 80, 70, 20, 0),        # right
        (0.34, 0, 0, -35, 0),         # over the top
        (0.50, -80, -70, 20, 0),      # left
        (0.67, 0, 0, 60, 0),          # under
        (0.84, 80, 70, 20, 0),        # right again
        (1.00, 30, 20, 0, 0)]),
    "LRL": ("Serpent Weave", 0.85, [
        (0.00, 0, 0, 0, 0),
        (0.20, -70, -50, 50, 0.2),    # low left strike
        (0.40, -10, -10, -40, 0),     # snap high
        (0.60, 70, 50, 50, 0.2),      # low right strike
        (0.80, 10, 10, -40, 0),       # snap high
        (1.00, -50, -40, 30, 0.3)]),  # closing left jab
    "RRL": ("Rising Reversal", 0.90, [
        (0.00, 0, 0, 0, 0),
        (0.18, 70, 70, 40, 0),        # right, low
        (0.33, 30, 20, 0, 0),
        (0.50, 110, 110, -10, 0),     # right again, higher
        (0.72, 0, 0, -95, 0),         # overhead backhand, blade laid back
        (0.88, -90, -80, 20, 0),      # comes down left
        (1.00, -40, -30, 10, 0)]),
    "LLR": ("Low Sweep Uppercut", 0.90, [
        (0.00, 0, 0, 0, 0),
        (0.18, -60, -40, 65, 0),      # low left
        (0.33, -20, -10, 30, 0),
        (0.50, -90, -60, 75, 0),      # lower, wider left
        (0.74, 40, 20, 40, 0),
        (0.90, 90, 60, -60, 0),       # rising right uppercut
        (1.00, 40, 30, -20, 0)]),
    "RLL": ("Crosscut Spiral", 0.90, [
        (0.00, 0, 0, 0, 0),
        (0.20, 90, 90, 10, 0),        # right
        (0.44, -30, -120, 30, 0),     # cross, starting to spiral
        (0.66, -90, -200, 40, 0),
        (0.86, -100, -300, 20, 0),
        (1.00, -50, -360, 0, 0)]),    # full roll spiral ending left
    "LRR": ("Lunge", 0.85, [
        (0.00, 0, 0, 0, 0),
        (0.20, -60, -60, 10, 0),      # left feint
        (0.40, 80, 80, 30, 0),        # right cut
        (0.60, 40, 30, 60, 0.1),
        (0.80, 0, 0, 72, 0.6),        # straight forward lunge
        (0.90, 0, 0, 70, 0.6),
        (1.00, 10, 5, 30, 0.1)]),
}

ENERGIZED = {
    "RR": ("Thunder Fang", 0.85, [
        (0.00, 0, 0, 0, 0),
        (0.16, 105, 95, 15, 0),
        (0.30, 30, 20, -35, 0),
        (0.46, 125, 100, 55, 0.25),   # deep second fang...
        (0.60, 100, 30, -40, 0),      # ...rolls flat and rises...
        (0.80, 95, 0, -200, 0),       # ...into a full wheel down the right side
        (1.00, 60, 25, -350, 0.1)]),  # wraps to +10: ends right
    "LL": ("Eclipse Crescent", 0.95, [
        (0.00, 0, 0, 0, 0),
        (0.20, -90, -70, 65, 0),
        (0.40, -50, -30, -45, 0),
        (0.58, -20, -10, -80, 0),
        (0.74, -80, -60, -30, 0.2),
        (0.88, -120, -100, 40, 0.5),  # second, wider crescent with a lunge
        (1.00, -40, -30, 10, 0.1)]),
    "RL": ("Shear Storm", 0.85, [
        (0.00, 0, 0, 0, 0),
        (0.16, 100, 95, 15, 0),
        (0.32, -100, -95, 30, 0.2),
        (0.50, 100, 95, 45, 0),
        (0.68, -100, -95, 60, 0.3),   # four crossing shears, each lower
        (0.84, 0, 0, 80, 0.7),        # centre drive
        (1.00, -35, -30, 20, 0.1)]),
    "LR": ("Hook Lance", 0.90, [
        (0.00, 0, 0, 0, 0),
        (0.20, -80, -120, -30, 0),
        (0.40, -20, -40, 30, 0),
        (0.58, 60, 60, 75, 0.5),
        (0.76, 90, 150, 80, 1.0),     # full lance with a twist
        (0.90, 70, 90, 60, 0.6),
        (1.00, 40, 30, 20, 0.1)]),
    "RRR": ("Tempest", 1.10, [
        (0.00, 0, 0, 0, 0),
        (0.12, 90, 60, -25, 0),
        (0.26, 200, 55, -35, 0),
        (0.40, 330, 55, -35, 0),
        (0.54, 460, 55, -35, 0),
        (0.68, 590, 55, -35, 0),      # two full turns, raised
        (0.84, 720, 50, -10, 0),
        (1.00, 740, 25, 20, 0.3)]),   # wraps to 20: ends right with a push
    "LLL": ("Hurricane Wheel", 1.05, [
        (0.00, 0, 0, 0, 0),
        (0.14, -35, -100, 10, 0),
        (0.28, -35, -200, 15, 0),
        (0.42, -35, -300, 15, 0),
        (0.56, -35, -400, 15, 0),
        (0.70, -35, -500, 15, 0),     # two counter-wheels...
        (0.86, -35, -620, 10, 0),
        (1.00, -25, -720, 30, 0.3)]), # ...landing with a push
    "RLR": ("Infinity Blade", 1.10, [
        (0.00, 0, 0, 0, 0),
        (0.10, 90, 80, 20, 0),
        (0.20, 0, 0, -40, 0),
        (0.30, -90, -80, 20, 0),
        (0.40, 0, 0, 65, 0.3),
        (0.52, 90, 80, 20, 0),
        (0.62, 0, 0, -40, 0),
        (0.72, -90, -80, 20, 0),      # the eight, twice, faster the second time
        (0.84, 0, 0, 65, 0.3),
        (0.93, 95, 90, 25, 0),
        (1.00, 40, 30, 5, 0)]),
    "LRL": ("Hydra Weave", 1.05, [
        (0.00, 0, 0, 0, 0),
        (0.14, -75, -55, 55, 0.4),
        (0.28, -10, -10, -45, 0),
        (0.42, 75, 55, 55, 0.4),
        (0.56, 10, 10, -45, 0),
        (0.70, -75, -55, 55, 0.4),    # three heads striking
        (0.84, -20, -15, -95, 0),     # rear back overhead...
        (1.00, -40, -30, 70, 0.6)]),  # ...and slam down left
    "RRL": ("Meteor Reversal", 1.05, [
        (0.00, 0, 0, 0, 0),
        (0.12, 75, 75, 45, 0),
        (0.24, 30, 20, 0, 0),
        (0.36, 120, 115, -15, 0),
        (0.48, 20, 10, -70, 0),       # overhead backhand...
        (0.58, -90, -10, -60, 0),     # ...carried to the left side...
        (0.72, -95, 0, -200, 0),      # ...into a wheel and a quarter down the left
        (0.86, -95, 0, -380, 0),
        (0.95, -80, -40, -420, 0.4),  # meteor coming down
        (1.00, -45, -35, -390, 0.2)]),# pitch wraps to -30: ends left
    "LLR": ("Skyfall Uppercut", 1.05, [
        (0.00, 0, 0, 0, 0),
        (0.14, -70, -50, 70, 0),
        (0.26, -25, -10, 30, 0),
        (0.40, -100, -70, 80, 0.3),
        (0.56, 40, 20, 40, 0),
        (0.70, 100, 70, -70, 0),      # uppercut...
        (0.84, 80, 60, -200, 0),      # ...over into a loop
        (1.00, 45, 35, -330, 0.5)]),  # skyfall: wraps to +30, driving down right
    "RLL": ("Vortex Spiral", 1.10, [
        (0.00, 0, 0, 0, 0),
        (0.14, 95, 95, 10, 0),
        (0.30, -30, -140, 30, 0),
        (0.46, -80, -260, 40, 0),
        (0.62, -100, -380, 40, 0.2),
        (0.78, -110, -500, 30, 0.2),
        (0.90, -90, -640, 20, 0),     # two full roll spirals down the left
        (1.00, -50, -720, 10, 0)]),
    "LRR": ("Piercing Charge", 1.05, [
        (0.00, 0, 0, 0, 0),
        (0.14, -70, -70, 10, 0),
        (0.28, 90, 90, 30, 0),
        (0.42, 30, 20, 70, 0.8),      # first lance
        (0.54, 20, 10, 65, 0.1),
        (0.66, 0, 0, 72, 1.0),        # second, deeper lance
        (0.80, 120, 60, -30, 0),      # then a rising wheel off to the right
        (1.00, 60, 40, -10, 0)]),
}


# ---------------------------------------------------------------------------------------------
# The shipped math, mirrored.
# ---------------------------------------------------------------------------------------------
def sample(keys, t01):
    """RhinoSwordComboPath.SamplePath: piecewise cubic Hermite, finite-difference tangents,
    zero end tangents. keys: list of (t, yaw, roll, pitch, thrust)."""
    t = min(1.0, max(0.0, t01))
    last = len(keys) - 1
    if t <= keys[0][0]:
        return keys[0][1:]
    if t >= keys[last][0]:
        return keys[last][1:]
    i = 0
    while i < last - 1 and t > keys[i + 1][0]:
        i += 1
    k0, k1 = keys[i], keys[i + 1]
    span = max(1e-5, k1[0] - k0[0])
    u = (t - k0[0]) / span

    def tangent(j):
        if j <= 0 or j >= last:
            return (0.0, 0.0, 0.0, 0.0)
        a, b = keys[j - 1], keys[j + 1]
        s = span / max(1e-5, b[0] - a[0])
        return tuple((b[c] - a[c]) * s for c in range(1, 5))

    m0, m1 = tangent(i), tangent(i + 1)
    u2, u3 = u * u, u * u * u
    h00, h10, h01, h11 = 2 * u3 - 3 * u2 + 1, u3 - 2 * u2 + u, -2 * u3 + 3 * u2, u3 - u2
    return tuple(h00 * k0[c + 1] + h10 * m0[c] + h01 * k1[c + 1] + h11 * m1[c] for c in range(4))


def rx(a):
    c, s = math.cos(a), math.sin(a)
    return ((1, 0, 0), (0, c, -s), (0, s, c))


def ry(a):
    c, s = math.cos(a), math.sin(a)
    return ((c, 0, s), (0, 1, 0), (-s, 0, c))


def rz(a):
    c, s = math.cos(a), math.sin(a)
    return ((c, -s, 0), (s, c, 0), (0, 0, 1))


def mm(a, b):
    return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))


def mv(a, v):
    return tuple(sum(a[i][k] * v[k] for k in range(3)) for i in range(3))


def blade(ch, rest_pitch, base_pos, anchor):
    """ShieldSwipeActionExecutor.ApplyShieldPose in the Fusilage frame: returns (hilt, tip)."""
    yaw, roll, pitch, thrust = ch
    d2r = math.pi / 180
    sweep = mm(ry(yaw * d2r), mm(rz(roll * d2r), rx(pitch * d2r)))
    pose = mm(sweep, rx(rest_pitch * d2r))
    up = mv(pose, (0, 1, 0))
    mount = mv(sweep, base_pos)
    hilt = tuple(mount[i] + up[i] * anchor * thrust for i in range(3))
    tip = tuple(hilt[i] + up[i] * 2 * anchor for i in range(3))
    return hilt, tip


def seg_point_dist(a, b, p):
    ab = [b[i] - a[i] for i in range(3)]
    ap = [p[i] - a[i] for i in range(3)]
    L2 = sum(x * x for x in ab) or 1e-9
    t = max(0.0, min(1.0, sum(ab[i] * ap[i] for i in range(3)) / L2))
    q = [a[i] + ab[i] * t for i in range(3)]
    return math.dist(q, p)


def wrap(d):
    w = ((d + 180.0) % 360.0) - 180.0
    return 180.0 if w <= -180.0 else w


# ---------------------------------------------------------------------------------------------
# Rig facts, read off the shipped assets (never typed).
# ---------------------------------------------------------------------------------------------
def read_rig():
    text = open(os.path.join(ROOT, PREFAB_REL), encoding="utf-8").read()
    block = None
    for m in re.finditer(r"--- !u!1001 &\d+\nPrefabInstance:.*?(?=\n--- )", text, re.S):
        if re.search(r"propertyPath: m_Name\n\s+value: ForceFieldSkimmer\n", m.group(0)):
            block = m.group(0)
    assert block, "ForceFieldSkimmer instance not found in Rhino.prefab"

    def f(prop):
        mm_ = re.search(r"propertyPath: " + re.escape(prop) + r"\n\s+value: ([-\d.eE]+)", block)
        assert mm_, f"{prop} not overridden on the ForceFieldSkimmer instance"
        return float(mm_.group(1))

    qx, qw = f("m_LocalRotation.x"), f("m_LocalRotation.w")
    rest_pitch = math.degrees(2 * math.atan2(qx, qw))
    base_pos = (f("m_LocalPosition.x"), f("m_LocalPosition.y"), f("m_LocalPosition.z"))
    cam = open(os.path.join(ROOT, CAMERA_REL), encoding="utf-8").read()
    mo = re.search(r"followOffset: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}", cam)
    camera = tuple(float(x) for x in mo.groups())
    return rest_pitch, base_pos, camera


# ---------------------------------------------------------------------------------------------
# Gates
# ---------------------------------------------------------------------------------------------
N = 96


def tip_track(keys, rig, anchor):
    rest_pitch, base_pos, _ = rig
    return [blade(sample(keys, i / (N - 1)), rest_pitch, base_pos, anchor) for i in range(N)]


def validate(base, energized, rig, report=True):
    errors = []
    L = 2 * REST_ANCHOR
    sets = {"base": base, "energized": energized}
    tracks = {}
    for set_name, lib in sets.items():
        missing = [s for s in SEQUENCES if s not in lib]
        extra = [s for s in lib if s not in SEQUENCES]
        if missing:
            errors.append(f"{set_name}: missing sequences {missing}")
        if extra:
            errors.append(f"{set_name}: unknown sequences {extra}")
        for seq, (name, dur, keys) in lib.items():
            ts = [k[0] for k in keys]
            if len(keys) < 2 or ts[0] != 0.0 or ts[-1] != 1.0 or any(b <= a for a, b in zip(ts, ts[1:])):
                errors.append(f"{set_name} {seq}: keys must start at 0, end at 1 and strictly increase")
                continue
            if dur <= 0.05:
                errors.append(f"{set_name} {seq}: duration {dur} too short")
            first = keys[1][1]
            want_first = 1 if seq[0] == "R" else -1
            if first * want_first <= 0:
                errors.append(f"{set_name} {seq} '{name}': opens toward {'right' if first > 0 else 'left'}, "
                              f"first press is {seq[0]}")
            end_yaw = wrap(keys[-1][1])
            want_last = 1 if seq[-1] == "R" else -1
            if end_yaw * want_last <= 0:
                errors.append(f"{set_name} {seq} '{name}': ends {'right' if end_yaw > 0 else 'left'} "
                              f"(wrapped yaw {end_yaw:.0f}), last press is {seq[-1]}")
            tracks[(set_name, seq)] = tip_track(keys, rig, REST_ANCHOR)

    def mean_sep(a, b):
        return sum(math.dist(a[i][1], b[i][1]) for i in range(N)) / N

    def travel(tr):
        return sum(math.dist(tr[i][1], tr[i + 1][1]) for i in range(N - 1))

    worst = (1e9, None)
    for set_name in sets:
        keys_ = [k for k in tracks if k[0] == set_name]
        for i in range(len(keys_)):
            for j in range(i + 1, len(keys_)):
                d = mean_sep(tracks[keys_[i]], tracks[keys_[j]]) / L
                if d < worst[0]:
                    worst = (d, (keys_[i], keys_[j]))
                if d < MIN_UNIQUE:
                    errors.append(f"{set_name}: {keys_[i][1]} and {keys_[j][1]} are too alike "
                                  f"(mean tip separation {d:.2f} blade lengths < {MIN_UNIQUE})")
    for seq in SEQUENCES:
        a, b = tracks.get(("base", seq)), tracks.get(("energized", seq))
        if not a or not b:
            continue
        d = mean_sep(a, b) / L
        if d < MIN_UNIQUE:
            errors.append(f"energized {seq} barely differs from base (separation {d:.2f} < {MIN_UNIQUE})")
        ta, tb = travel(a), travel(b)
        if tb < ENERGIZED_TRAVEL * ta:
            errors.append(f"energized {seq} is not bigger: tip travel {tb:.0f} < {ENERGIZED_TRAVEL} x base {ta:.0f}")

    camera = rig[2]
    rows = []
    for set_name, lib in sets.items():
        for seq in SEQUENCES:
            if seq not in lib:
                continue
            name, dur, keys = lib[seq]
            clr_rest = min(seg_point_dist(h, t, camera) for h, t in tip_track(keys, rig, REST_ANCHOR))
            clr_charged = min(seg_point_dist(h, t, camera) for h, t in tip_track(keys, rig, ENERGIZED_ANCHOR))
            tr = tracks.get((set_name, seq))
            peak = 0.0
            if tr:
                dt = dur / (N - 1)
                peak = max(math.dist(tr[i][1], tr[i + 1][1]) / dt for i in range(N - 1))
                rows.append((set_name, seq, name, dur, travel(tr), peak, clr_rest, clr_charged))
            if clr_rest < CAMERA_CLEARANCE:
                errors.append(f"{set_name} {seq} '{name}': blade passes {clr_rest:.1f} u from the camera "
                              f"at resting length (< {CAMERA_CLEARANCE})")

    if report:
        print(f"rig: rest pitch {rig[0]:.1f} deg, mount {rig[1]}, camera {rig[2]}; blade {L:.0f} u at rest")
        print(f"{'set':10} {'seq':4} {'name':22} {'dur':>5} {'tip travel':>10} {'peak u/s':>9} "
              f"{'cam clr':>8} {'@charged':>9}")
        for r in rows:
            print(f"{r[0]:10} {r[1]:4} {r[2]:22} {r[3]:5.2f} {r[4]:10.0f} {r[5]:9.0f} {r[6]:8.1f} {r[7]:9.1f}")
        if worst[1]:
            print(f"closest pair: {worst[1][0]} vs {worst[1][1]} at {worst[0]:.2f} blade lengths "
                  f"(gate {MIN_UNIQUE})")
    return errors


# ---------------------------------------------------------------------------------------------
# Emit
# ---------------------------------------------------------------------------------------------
def emit_library():
    out = [header_for(SCRIPT_GUID, "RhinoSwordComboLibrary")]
    for k in ["pressThreshold", "releaseThreshold", "comboWindowSeconds", "tapMaxHoldSeconds",
              "chordWindowSeconds", "finisherLockoutFraction", "blendInSeconds", "blendOutSeconds"]:
        out.append(f"  {k}: {num(DETECTION[k])}\n")
    out.append("  combos:\n")
    for energized, lib in ((0, BASE), (1, ENERGIZED)):
        for seq in SEQUENCES:
            name, dur, keys = lib[seq]
            out.append(f"  - sequence: {seq}\n")
            out.append(f"    energized: {energized}\n")
            out.append(f"    displayName: {name}\n")
            out.append(f"    durationSeconds: {num(dur)}\n")
            out.append("    keys:\n")
            for t, yaw, roll, pitch, thrust in keys:
                out.append(f"    - time: {num(float(t))}\n")
                out.append(f"      yaw: {num(float(yaw))}\n")
                out.append(f"      roll: {num(float(roll))}\n")
                out.append(f"      pitch: {num(float(pitch))}\n")
                out.append(f"      thrust: {num(float(thrust))}\n")
            out.append("    sound:\n      Guid:\n        Data1: 0\n        Data2: 0\n"
                       "        Data3: 0\n        Data4: 0\n      Path: \n")
    return "".join(out)


def patched_config(text):
    line = f"  comboLibrary: {{fileID: 11400000, guid: {LIB_GUID}, type: 2}}\n"
    if "comboLibrary:" in text:
        return re.sub(r"  comboLibrary: \{[^}]*\}\n", line, text)
    return text.rstrip("\n") + "\n" + line


def planned_files():
    cfg = open(os.path.join(ROOT, CONFIG_REL), encoding="utf-8").read()
    return {
        LIB_REL: emit_library(),
        LIB_REL + ".meta": asset_meta_text(LIB_GUID),
        SCRIPT_REL + ".meta": script_meta_text(SCRIPT_GUID),
        DETECTOR_REL + ".meta": script_meta_text(DETECTOR_GUID),
        CONFIG_REL: patched_config(cfg),
    }


# ---------------------------------------------------------------------------------------------
# SVG contact sheet
# ---------------------------------------------------------------------------------------------
def write_svg(path, rig):
    cell, pad = 190, 14
    cols = len(SEQUENCES)
    w, h = cols * cell + pad * 2, 4 * cell + 90
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" '
             f'font-family="sans-serif" font-size="11">',
             f'<rect width="{w}" height="{h}" fill="#10131a"/>',
             f'<text x="{pad}" y="22" fill="#e8ecf4" font-size="15">Rhino sword combos - blade TIP '
             f'trajectory (resting length). Rows: base top view, base side view, energized top, '
             f'energized side. Cyan = start, magenta = end.</text>']
    scale = cell / 260.0
    rows = [("base", BASE, "top"), ("base", BASE, "side"), ("energized", ENERGIZED, "top"),
            ("energized", ENERGIZED, "side")]
    for r, (set_name, lib, view) in enumerate(rows):
        for c, seq in enumerate(SEQUENCES):
            x0, y0 = pad + c * cell, 40 + r * cell
            cx, cy = x0 + cell / 2, y0 + cell / 2 + 6
            parts.append(f'<rect x="{x0 + 2}" y="{y0 + 2}" width="{cell - 4}" height="{cell - 4}" '
                         f'fill="#171b25" stroke="#2a3040"/>')
            name, dur, keys = lib[seq]
            parts.append(f'<text x="{x0 + 8}" y="{y0 + 16}" fill="#e8ecf4">{seq} {name}</text>')
            parts.append(f'<text x="{x0 + 8}" y="{y0 + 29}" fill="#8a93a8">{set_name} {view} {dur:.2f}s</text>')
            parts.append(f'<circle cx="{cx}" cy="{cy}" r="3" fill="#ffffff"/>')
            tr = tip_track(keys, rig, REST_ANCHOR)

            def proj(p):
                if view == "top":   # x right, z up the page (forward)
                    return cx + p[0] * scale, cy - p[2] * scale
                return cx + p[2] * scale, cy - p[1] * scale  # side: forward right, up up

            pts = " ".join(f"{a:.1f},{b:.1f}" for a, b in (proj(t[1]) for t in tr))
            parts.append(f'<polyline points="{pts}" fill="none" stroke="#f5b642" stroke-width="1.6"/>')
            a = proj(tr[0][1])
            b = proj(tr[-1][1])
            parts.append(f'<circle cx="{a[0]:.1f}" cy="{a[1]:.1f}" r="3.5" fill="#39d0e6"/>')
            parts.append(f'<circle cx="{b[0]:.1f}" cy="{b[1]:.1f}" r="3.5" fill="#e64aa7"/>')
            if view == "top":
                cam = proj(rig[2])
                parts.append(f'<rect x="{cam[0] - 3:.1f}" y="{cam[1] - 3:.1f}" width="6" height="6" fill="#7d8597"/>')
    parts.append("</svg>")
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("\n".join(parts))
    print("wrote", path)


# ---------------------------------------------------------------------------------------------
def self_test(rig):
    import copy
    cases = []
    b, e = copy.deepcopy(BASE), copy.deepcopy(ENERGIZED)
    # 1. a RETIMED copy: the metric is time-normalized, so playing RR's path at another
    #    speed under another sequence must still read as the same flourish.
    b2 = copy.deepcopy(b)
    name, dur, keys = b2["RR"]
    b2["RRR"] = ("retimed copy", dur * 1.3, list(keys))
    cases.append(("near-copy trips uniqueness", b2, e, "too alike"))
    # 2. direction: RR opening left
    b3 = copy.deepcopy(b)
    n_, d_, k_ = b3["RR"]
    k_[1] = (k_[1][0], -95, -85, 10, 0)
    cases.append(("wrong opening direction", b3, e, "opens toward left"))
    # 3. energized not bigger: energized RR = base RR
    e4 = copy.deepcopy(e)
    e4["RR"] = b["RR"]
    cases.append(("energized not bigger", b, e4, "not bigger"))
    # 4. camera: a flat revolution with the blade laid horizontal
    b5 = copy.deepcopy(b)
    b5["RRR"] = ("flat", 0.9, [(0.0, 0, 0, 0, 0), (0.5, 180, 90, 0, 0), (1.0, 380, 90, 0, 0)])
    cases.append(("flat revolution through the camera", b5, e, "from the camera"))
    # 5. missing sequence
    b6 = copy.deepcopy(b)
    del b6["LRL"]
    cases.append(("missing sequence", b6, e, "missing sequences"))
    ok = True
    for label, bb, ee, needle in cases:
        errs = validate(bb, ee, rig, report=False)
        fired = any(needle in x for x in errs)
        print(f"  {'FIRES' if fired else 'SILENT'}: {label}")
        ok &= fired
    clean = validate(BASE, ENERGIZED, rig, report=False)
    print(f"  shipped library: {'clean' if not clean else clean}")
    return ok and not clean


def main():
    args = sys.argv[1:]
    rig = read_rig()
    if "--self-test" in args:
        ok = self_test(rig)
        print("self-test", "PASSED" if ok else "FAILED")
        sys.exit(0 if ok else 1)

    errors = validate(BASE, ENERGIZED, rig)
    if errors:
        print("\nFAILED:")
        for x in errors:
            print("  -", x)
        sys.exit(1)

    if "--svg" in args:
        write_svg(args[args.index("--svg") + 1], rig)

    files = planned_files()
    if "--check" in args:
        drift = []
        for rel, content in files.items():
            full = os.path.join(ROOT, rel)
            cur = open(full, encoding="utf-8").read() if os.path.exists(full) else None
            if cur != content:
                drift.append(rel)
        if drift:
            print("--check: DRIFT in", drift)
            sys.exit(1)
        print(f"--check: OK, {len(files)} file(s) match.")
        return

    for rel, content in files.items():
        full = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(content)
    print(f"Wrote {len(files)} file(s).")


if __name__ == "__main__":
    main()
