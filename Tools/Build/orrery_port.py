#!/usr/bin/env python3
"""SlingshotOrrery.cs, line for line, for the offline Stoat model (stoat_orbit_sim.py --orrery) and its counts.

    py -3 Tools/Build/orrery_port.py            # prisms, moons and shepherds per intensity on the shipped courses
    py -3 Tools/Build/orrery_port.py --self-test

A port: when SlingshotOrrery.cs changes, this changes with it (the self-test pins the ladder's constants against the C#).
"""
import math, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
CS = os.path.join(REPO, "Assets", "_Scripts", "Controller", "Arcade", "Slingshot", "SlingshotOrrery.cs")

PRESS_AHEAD, PRESS_SIDE = 150.0, 45.0
MOON_RIM_GAP, LINE_CLEARANCE, MIN_TURN = 8.0, 40.0, 15.0
MOUTH_CLEARANCE = 30.0
MOON_CUBE, BELT_PITCH, TAIL_PITCH, TAIL_SPOKES, TAIL_CUBE = 5.0, 13.0, 22.0, 12, 7.0
SHEPHERD_OFFSET, SHEPHERD_CUBE, SHEPHERD_PERIOD = 22.0, 6.0, 9.0
LADDER = {
    "MoonRadius": [9.0, 10.0, 11.0, 12.0],
    "BeltGapDegrees": [0.0, 70.0, 52.0, 40.0],
    "BeltRows": [0, 1, 2, 3],
    "BeltCube": [0.0, 6.0, 6.5, 7.0],
    "TailRadius": [0.0, 0.0, 58.0, 46.0],
    "TailFill": [0.0, 0.0, 0.55, 0.75],
    "DangerShare": [0.0, 0.0, 0.12, 0.18],
    "TotalCap": [900, 1600, 2400, 3000],
    "MovingPrismCap": [0, 0, 0, 140],
}


def L(name, i): return LADDER[name][min(max(i, 1), 4) - 1]
def add(a, b): return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]
def sub(a, b): return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]
def mul(a, s): return [a[0] * s, a[1] * s, a[2] * s]
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
def norm(a): return math.sqrt(dot(a, a))
def unit(a):
    n = norm(a)
    return [a[0] / n, a[1] / n, a[2] / n] if n > 1e-9 else [0.0, 0.0, 0.0]


def rotate(v, axis, deg):
    """Quaternion.AngleAxis(deg, axis) * v (Unity is left-handed: a positive angle about +Y turns +Z toward +X)."""
    k = unit(axis); r = math.radians(deg); c, s = math.cos(r), -math.sin(r)
    return add(add(mul(v, c), mul(cross(k, v), s)), mul(k, dot(k, v) * (1 - c)))


def angle(a, b):
    na, nb = norm(a), norm(b)
    if na < 1e-9 or nb < 1e-9: return 0.0
    return math.degrees(math.acos(max(-1.0, min(1.0, dot(a, b) / (na * nb)))))


def seg_dist(p, a, b):
    ab = sub(b, a); l2 = dot(ab, ab)
    t = max(0.0, min(1.0, dot(sub(p, a), ab) / l2)) if l2 > 1e-8 else 0.0
    return norm(sub(p, add(a, mul(ab, t))))


def h32(a, b, c):
    h = 2166136261
    for x in (a, b, c):
        h = ((h ^ (x & 0xffffffff)) * 16777619) & 0xffffffff
    return h & 0x7fffffff


def try_moon(course, g, i):
    n = len(course); p = course[g]["p"]
    a = sub(p, course[(g - 1) % n]["p"]); b = sub(course[(g + 1) % n]["p"], p)
    if norm(a) < 1 or norm(b) < 1: return None
    a, b = unit(a), unit(b)
    if angle(a, b) < MIN_TURN: return None
    inward, normal = sub(b, a), cross(a, b)
    if norm(inward) < 1e-3 or norm(normal) < 1e-4: return None
    inward, normal = unit(inward), unit(normal)
    r = L("MoonRadius", i); ring = course[g]["r"]
    d = ring + r + MOON_RIM_GAP
    centre = add(p, mul(inward, d))
    belt = d + ring + MOUTH_CLEARANCE if i >= 2 else 0.0
    press = sub(sub(centre, mul(a, PRESS_AHEAD)), mul(inward, PRESS_SIDE))
    return {"gate": g, "centre": centre, "r": r, "normal": normal, "inb": a, "outb": b, "belt": belt, "press": press}


class Ctx:
    def __init__(s, course, centre, clear, cap):
        s.course, s.centre, s.clear, s.cap, s.prisms, s.movers = course, centre, clear, cap, [], []

    def ok(s, at, size, own, own_next=False):
        if norm(sub(at, s.centre)) < s.clear + size: return False
        n = len(s.course)
        for g in range(n):
            gate = s.course[g]
            rim = norm(sub(at, gate["p"])) - gate["r"]
            if g != own and rim < MOUTH_CLEARANCE + size: return False
            if g == own and rim < MOON_RIM_GAP * 0.5: return False
            nxt = (g + 1) % n
            own_leg = g == own or nxt == own
            if own_next: own_leg = g == own
            line = seg_dist(at, gate["p"], s.course[nxt]["p"])
            if not own_leg and line < LINE_CLEARANCE + size: return False
            if own_next and g == own and line < LADDER["TailRadius"][3] * 0.5: return False
        return True

    def add(s, at, size, kind, own, own_next=False):
        if len(s.prisms) >= s.cap or not s.ok(at, size, own, own_next): return
        s.prisms.append({"p": at, "size": size, "kind": kind})


def danger(h, i): return "danger" if (h32(h, 17, 3) & 1023) / 1024.0 < L("DangerShare", i) else "plain"


def crossing(frm, d, centre, radius):
    f = sub(frm, centre); b = dot(f, d); c = dot(f, f) - radius * radius
    t = -b + math.sqrt(max(0.0, b * b - c))
    return unit(sub(add(frm, mul(d, t)), centre))


def layout(course, i, centre=(0.0, 0.0, 0.0), clear=423.0):
    ctx = Ctx(course, list(centre), clear, L("TotalCap", i))
    moons = [m for m in (try_moon(course, g, i) for g in range(len(course))) if m]
    for m in moons:                                                # EmitMoon
        r, c = m["r"], MOON_CUBE
        cells = math.ceil(r / c)
        fwd, up = m["inb"], m["normal"]; right = cross(up, fwd)    # LookRotation(fwd, up)
        for x in range(-cells, cells + 1):
            for y in range(-cells, cells + 1):
                for z in range(-cells, cells + 1):
                    d = math.sqrt(x * x + y * y + z * z) * c
                    if d > r or d < r - c * 1.2: continue
                    at = add(m["centre"], add(add(mul(right, x * c), mul(up, y * c)), mul(fwd, z * c)))
                    ctx.add(at, c - 0.6, "plain", m["gate"])
    if i <= 1:                                                     # EmitBeacon
        for m in moons:
            for k in range(3):
                ctx.add(add(m["press"], rotate(mul(m["normal"], 6.0), m["inb"], 120.0 * k)), 3.0, "plain", m["gate"])
    if i >= 4:                                                     # EmitShepherds
        for mi, m in enumerate(moons):
            orbit = m["belt"] + SHEPHERD_OFFSET
            u = unit(sub(course[m["gate"]]["p"], m["centre"]))
            if not all(ctx.ok(add(m["centre"], mul(rotate(u, m["normal"], 10.0 * k), orbit)), SHEPHERD_CUBE * 2, m["gate"]) for k in range(36)):
                continue
            for s_ in range(2):
                if len(ctx.movers) * 4 + 4 > L("MovingPrismCap", i): break
                ctx.movers.append({"moon": mi, "centre": m["centre"], "normal": m["normal"], "radius": orbit,
                                   "phase_dir": rotate(u, m["normal"], 90.0 + 180.0 * s_), "omega": 2 * math.pi / SHEPHERD_PERIOD})
    if i >= 2:                                                     # EmitBelt
        for mi, m in enumerate(moons):
            rb = m["belt"]
            gate = course[m["gate"]]["p"]
            in_gap = crossing(gate, mul(m["inb"], -1), m["centre"], rb)
            out_gap = crossing(gate, m["outb"], m["centre"], rb)
            half = L("BeltGapDegrees", i) * 0.5
            u = unit(sub(gate, m["centre"]))
            count = max(12, round(2 * math.pi * rb / BELT_PITCH))
            rows, cube = L("BeltRows", i), L("BeltCube", i)
            for k in range(count):
                dr = rotate(u, m["normal"], 360.0 * k / count)
                if angle(dr, in_gap) < half or angle(dr, out_gap) < half: continue
                for row in range(rows):
                    lift = (row - (rows - 1) * 0.5) * (cube + 3) if rows > 1 else 0.0
                    ctx.add(add(add(m["centre"], mul(dr, rb)), mul(m["normal"], lift)), cube, danger(k * 7 + row + mi * 131, i), -1)
    if i >= 3:                                                     # EmitTail
        n = len(course)
        for g in range(n):
            a, b = course[g]["p"], course[(g + 1) % n]["p"]
            d = sub(b, a); length = norm(d)
            if length < 4 * TAIL_PITCH: continue
            d = unit(d)
            s1 = unit(cross(d, [0, 1, 0] if abs(d[1]) < 0.9 else [1, 0, 0]))
            radius, fill = L("TailRadius", i), L("TailFill", i)
            t = 0.3 * length
            while t <= 0.7 * length:
                c = add(a, mul(d, t))
                if not any(norm(sub(c, m["centre"])) < max(m["belt"], m["r"]) + radius + 20 for m in moons):
                    for k in range(TAIL_SPOKES):
                        h = h32(g, int(round(t)), k)
                        if (h & 1023) / 1024.0 > fill: continue
                        ang = 360.0 * (k + 0.5 * ((h >> 10) & 1)) / TAIL_SPOKES
                        ctx.add(add(c, mul(rotate(s1, d, ang), radius)), TAIL_CUBE, danger(h, i), g, own_next=True)
                t += TAIL_PITCH
    return moons, ctx.prisms, ctx.movers


def self_test():
    src = open(CS, encoding="utf-8").read()
    bad = []
    for name, vals in LADDER.items():
        m = re.search(r"public static \w+ " + name + r"\(int intensity\) => new\[\] \{ ([^}]*) \}", src)
        got = [float(x.strip().rstrip("f")) for x in m.group(1).split(",")] if m else None
        if got is None or [float(v) for v in vals] != got: bad.append(f"{name}: port {vals} vs C# {got}")
    for name, val in (("PressAhead", PRESS_AHEAD), ("PressSide", PRESS_SIDE), ("MoonRimGap", MOON_RIM_GAP), ("LineClearance", LINE_CLEARANCE),
                      ("MoonCube", MOON_CUBE), ("BeltPitch", BELT_PITCH), ("TailPitch", TAIL_PITCH), ("TailCube", TAIL_CUBE),
                      ("ShepherdOffset", SHEPHERD_OFFSET), ("ShepherdCube", SHEPHERD_CUBE), ("ShepherdPeriod", SHEPHERD_PERIOD)):
        m = re.search(r"const float " + name + r" = ([0-9.]+)f;", src)
        if not m or float(m.group(1)) != val: bad.append(f"{name}: port {val} vs C# {m and m.group(1)}")
    m = re.search(r"const int TailSpokes = (\d+);", src)
    if not m or int(m.group(1)) != TAIL_SPOKES: bad.append("TailSpokes")
    # a square course: four moons inside the corners, each clear of its ring by the rim gap
    sq = [{"p": [x, 0.0, z], "r": 40.0} for x, z in ((800, 0), (0, 800), (-800, 0), (0, -800))]
    moons, prisms, _ = layout(sq, 1, clear=100.0)
    if len(moons) != 4: bad.append(f"square: {len(moons)} moons")
    for m in moons:
        gap = norm(sub(m["centre"], sq[m["gate"]]["p"])) - 40 - m["r"]
        if abs(gap - MOON_RIM_GAP) > 1e-3: bad.append(f"moon gap {gap}")
        if norm(m["centre"]) > norm(sq[m["gate"]]["p"]): bad.append("a moon outside its corner")
    print("orrery_port self-test:", "FAIL " + "; ".join(bad) if bad else "ok")
    return 1 if bad else 0


if __name__ == "__main__":
    if "--self-test" in sys.argv: sys.exit(self_test())
    sys.path.insert(0, HERE)
    import stoat_orbit_sim as S
    for i in (1, 2, 3, 4):
        rows = []
        for s in range(8):
            moons, prisms, movers = layout(S.course(i, 20260000 + 97 * s + i), i)
            rows.append((len(moons), len(prisms), sum(1 for p in prisms if p["kind"] == "danger"), len(movers)))
        mean = lambda k: sum(r[k] for r in rows) / len(rows)
        print(f"I{i}: moons {mean(0):.1f}  prisms {mean(1):.0f} (max {max(r[1] for r in rows)})  danger {mean(2):.0f}  shepherds {mean(3):.1f}")
