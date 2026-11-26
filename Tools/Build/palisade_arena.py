#!/usr/bin/env python3
"""A Python twin of Palisade's arena layout (Assets/_Scripts/Controller/Arcade/Palisade/PalisadeArena.cs), for the
card renderer, which draws offline in Python. Never a second source of truth: `bash Tools/Build/palisade_harness/run.sh
--self-test` compiles the shipped C#, prints its parts (`--parts`) and fails unless this twin lays the same prisms.

    python3 Tools/Build/palisade_arena.py [intensity]          # the rung's parts as JSON lines
    python3 Tools/Build/palisade_arena.py --parity FILE        # compare with the harness's --parts output

Standard library only. Positions are relative to the cell's centre; kinds are PrismKind (0 plain, 1 danger, 2 shielded);
pieces are 0 post, 1 frame, 2 rubble.
"""
import json
import math
import sys

POST, FRAME, RUBBLE = 0, 1, 2
PLAIN, DANGER, SHIELDED = 0, 1, 2

BY_RUNG = [
    dict(lanes=3, posts=14, shield_every=0, closure=1.0, tips=False, rubble=0, rubble_danger_every=0),
    dict(lanes=3, posts=10, shield_every=5, closure=0.75, tips=False, rubble=0, rubble_danger_every=0),
    dict(lanes=2, posts=8, shield_every=3, closure=0.5, tips=True, rubble=10, rubble_danger_every=0),
    dict(lanes=2, posts=6, shield_every=2, closure=1.0 / 3.0, tips=True, rubble=18, rubble_danger_every=3),
]
INNER, OUTER, LANE_RADIUS = 600.0, 950.0, 640.0
POST_SPACING, RUN_OFFSET, RUN_IN, FRAME_GAP = 30.0, 14.0, 60.0, 90.0
FRAME_RADIUS, FRAME_STONES = 30.0, 12
RUBBLE_LANE_CLEARANCE, RUBBLE_SPACING = 90.0, 60.0
POST_SCALE, STONE_SCALE, TIP_SCALE = (8.0, 8.0, 34.0), (12.0, 12.0, 16.0), (9.0, 9.0, 28.0)


def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def norm(a):
    m = math.sqrt(dot(a, a))
    return mul(a, 1.0 / m) if m > 1e-12 else (0.0, 0.0, 0.0)
def dist(a, b): return math.sqrt(dot(sub(a, b), sub(a, b)))


def rotate(v, deg, axis):
    """Unity's Quaternion.AngleAxis(deg, axis) * v (Rodrigues)."""
    k = norm(axis)
    t = math.radians(deg)
    c, s = math.cos(t), math.sin(t)
    return add(add(mul(v, c), mul(cross(k, v), s)), mul(k, dot(k, v) * (1 - c)))


def hash01(key):
    m = 0xFFFFFFFF
    h = ((key & m) * 2654435761 + 0x9E3779B9) & m
    h ^= h >> 16
    h = (h * 0x85EBCA6B) & m
    h ^= h >> 13
    h = (h * 0xC2B2AE35) & m
    h ^= h >> 16
    return (h >> 8) / 16777216.0


def stones_standing(closure):
    return max(2, min(FRAME_STONES, int(round(FRAME_STONES * closure))))


def lane_of(k, s):
    around = (360.0 / max(1, s["lanes"])) * k + 20.0
    tip = 24.0 if k % 2 == 0 else -24.0
    up = (0.0, 1.0, 0.0)
    flat = rotate((0.0, 0.0, 1.0), around, up)
    tip_axis = norm(cross(up, flat))
    radial = norm(rotate(flat, -tip, tip_axis))
    across = norm(cross(up, radial))
    axis = norm(rotate(across, 18.0 + 47.0 * k, radial))
    middle = mul(radial, LANE_RADIUS)
    half = (s["posts"] - 1) * POST_SPACING * 0.5
    first, last = sub(middle, mul(axis, half)), add(middle, mul(axis, half))
    return dict(first=first, last=last, frame=add(last, mul(axis, FRAME_GAP)), axis=axis, side=radial)


def distance_to_run(p, lane):
    a = sub(lane["first"], mul(lane["axis"], RUN_IN))
    b = add(lane["frame"], mul(lane["axis"], FRAME_RADIUS))
    ab = sub(b, a)
    t = max(0.0, min(1.0, dot(sub(p, a), ab) / dot(ab, ab)))
    return dist(p, add(a, mul(ab, t)))


def lay(intensity):
    s = BY_RUNG[max(1, min(4, intensity)) - 1]
    parts, lanes = [], []
    for k in range(s["lanes"]):
        lane = lane_of(k, s)
        lanes.append(lane)
        across = norm(cross(lane["axis"], lane["side"]))
        for i in range(s["posts"]):
            shielded = s["shield_every"] > 0 and i % s["shield_every"] == s["shield_every"] - 1
            parts.append(dict(pos=add(lane["first"], mul(lane["axis"], i * POST_SPACING)), fwd=across, up=lane["axis"],
                              scale=POST_SCALE, kind=SHIELDED if shielded else PLAIN, piece=POST, lane=k))
        standing = stones_standing(s["closure"])
        other = norm(cross(lane["axis"], lane["side"]))
        step = 360.0 / FRAME_STONES
        first_angle = -(standing - 1) * step * 0.5
        for j in range(standing):
            a = math.radians(first_angle + j * step)
            radial = add(mul(lane["side"], math.cos(a)), mul(other, math.sin(a)))
            tip = s["tips"] and standing < FRAME_STONES and (j == 0 or j == standing - 1)
            parts.append(dict(pos=add(lane["frame"], mul(radial, FRAME_RADIUS + STONE_SCALE[0] * 0.5)), fwd=lane["axis"],
                              up=radial, scale=TIP_SCALE if tip else STONE_SCALE, kind=DANGER if tip else SHIELDED,
                              piece=FRAME, lane=k))
    first_rubble = len(parts)
    for j in range(s["rubble"]):
        for attempt in range(64):
            key = j * 64 + attempt
            z = hash01(key * 4 + 1) * 2 - 1
            around = hash01(key * 4 + 2) * math.pi * 2
            ring = math.sqrt(max(0.0, 1 - z * z))
            d = (ring * math.cos(around), z, ring * math.sin(around))
            size = 14 + 8 * hash01(key * 4 + 3)
            radius = (INNER + size) + ((OUTER - size) - (INNER + size)) * hash01(key * 4 + 4)
            p = mul(d, radius)
            if any(distance_to_run(p, ln) < RUBBLE_LANE_CLEARANCE for ln in lanes):
                continue
            if any(dist(p, q["pos"]) < RUBBLE_SPACING for q in parts[first_rubble:]):
                continue
            danger = s["rubble_danger_every"] > 0 and j % s["rubble_danger_every"] == s["rubble_danger_every"] - 1
            tilt = norm(cross(d, (0.0, 1.0, 0.0) if abs(d[1]) < 0.9 else (1.0, 0.0, 0.0)))
            fwd = norm(rotate(tilt, hash01(key * 4 + 5) * 360.0, d))
            parts.append(dict(pos=p, fwd=fwd, up=d, scale=(size, size * 0.75, size * 1.25),
                              kind=DANGER if danger else SHIELDED, piece=RUBBLE, lane=-1))
            break
    return parts


def parity(path):
    """Compare this twin with the shipped C#'s parts (palisade_harness --parts). Returns a list of problems."""
    problems = []
    shipped = {}
    for line in open(path):
        line = line.strip()
        if line.startswith("{"):
            row = json.loads(line)
            shipped.setdefault(row["intensity"], []).append(row)
    for rung in (1, 2, 3, 4):
        mine, theirs = lay(rung), shipped.get(rung, [])
        if len(mine) != len(theirs):
            problems.append(f"I{rung}: twin lays {len(mine)} prisms, the C# {len(theirs)}")
            continue
        for i, (a, b) in enumerate(zip(mine, theirs)):
            if a["kind"] != b["kind"] or a["piece"] != b["piece"] or a["lane"] != b["lane"]:
                problems.append(f"I{rung} part {i}: kind/piece/lane differ")
            for key in ("pos", "fwd", "up", "scale"):
                if dist(a[key], tuple(b[key])) > 0.05:
                    problems.append(f"I{rung} part {i}: {key} {a[key]} vs {b[key]}")
    return problems


if __name__ == "__main__":
    if len(sys.argv) > 2 and sys.argv[1] == "--parity":
        bad = parity(sys.argv[2])
        for b in bad[:20]:
            print("PARITY:", b)
        print(f"palisade_arena parity: {'OK' if not bad else f'{len(bad)} problem(s)'}")
        sys.exit(1 if bad else 0)
    rung = int(sys.argv[1]) if len(sys.argv) > 1 else 2
    for p in lay(rung):
        print(json.dumps(p))
