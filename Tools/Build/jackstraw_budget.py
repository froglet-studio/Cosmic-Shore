#!/usr/bin/env python3
"""
The Heap - Jackstraw's arena (GameModes.Jackstraw = 84, the Thresher's CHARGE game) - as a MIRROR of
SpawnableJackstraw.cs, not an estimate. author_jackstraw_assets.py imports it for the census, the laid
volume and the PhaseThresholds, and the stills and the card art are drawn from it, so the picture, the
numbers and the build cannot drift apart. That is possible because the C# was written for it:

  * every draw is Hash01 of an integer key (ported verbatim below), never the shared System.Random;
  * every per-unit prism count is a const;
  * the only count-affecting rejections are deterministic geometry: a stick (or a jack's three axes)
    whose segment enters a clearing or comes within STICK_GAP of a stick already laid re-draws, at most
    STICK_TRIES times, then is skipped.

The C# computes in float32 and this in float64, so a draw that lands within a few millionths of a unit of
a rejection boundary could resolve differently. The generator asserts every accepted stick clears every
boundary by a margin (MIN_MARGIN), so no draw the census counts sits that close.

Run it to print the ladder:   python3 Tools/Build/jackstraw_budget.py
"""
import math

# ── Constants: must match SpawnableJackstraw.cs (the generator greps for each) ──
STICK_PRISMS = 12
SEGMENT = 10.0
SECTION = 3.0
IRONWOOD_FROM, IRONWOOD_TO = 4, 8
STICK_GAP = 7.5
STICK_TRIES = 8
JACK_ARM_PRISMS = 5
KNOT = 9.0
FLUSH = 0.995
JACK_KEY_BASE, STICK_KEY_BASE = 1_000_000, 2_000_000
DEFAULT_SEED = 8484
GOLDEN_ANGLE = 2.39996323

STICK_HALF = STICK_PRISMS * SEGMENT * 0.5
JACK_REACH = KNOT * 0.5 + JACK_ARM_PRISMS * SEGMENT

MIN_MARGIN = 1e-3   # u; no accepted piece sits closer than this to a rejection boundary (float32 vs float64)

M32 = 0xFFFFFFFF


def hash01(n: int) -> float:
    """Verbatim port of CellEnvironmentSpawnableBase.Hash01 (unchecked uint arithmetic)."""
    h = n & M32
    h = (h ^ 61) ^ (h >> 16)
    h = (h * 9) & M32
    h ^= h >> 4
    h = (h * 0x27D4EB2D) & M32
    h ^= h >> 15
    return (h & 0xFFFFFF) / float(0x1000000)


def i32(n: int) -> int:
    n &= M32
    return n - (1 << 32) if n >= (1 << 31) else n


def key(seed, family, slot, attempt, draw):
    return i32(seed * 7919 + family + (slot * STICK_TRIES + attempt) * 4 + draw)


def unit_from(k):
    u = 2.0 * hash01(k) - 1.0
    phi = 2.0 * math.pi * hash01(i32(k + 7919))
    s = math.sqrt(max(0.0, 1.0 - u * u))
    return (s * math.cos(phi), s * math.sin(phi), u)


def in_ball(k, r):
    d = unit_from(k)
    m = r * hash01(i32(k + 104729)) ** (1.0 / 3.0)
    return (d[0] * m, d[1] * m, d[2] * m)


def fibonacci(k, n):
    y = 1.0 - 2.0 * (k + 0.5) / n
    s = math.sqrt(max(0.0, 1.0 - y * y))
    a = GOLDEN_ANGLE * k
    return (math.cos(a) * s, y, math.sin(a) * s)


def add(a, b, s=1.0):
    return (a[0] + b[0] * s, a[1] + b[1] * s, a[2] + b[2] * s)


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def norm(v):
    m = math.sqrt(dot(v, v)) or 1.0
    return (v[0] / m, v[1] / m, v[2] / m)


def clamp01(x):
    return 0.0 if x < 0.0 else 1.0 if x > 1.0 else x


def point_segment_sqr(p, a, b):
    ab = sub(b, a)
    t = clamp01(dot(sub(p, a), ab) / max(1e-6, dot(ab, ab)))
    c = add(a, ab, t)
    d = sub(c, p)
    return dot(d, d)


def segment_segment_sqr(p1, q1, p2, q2):
    """Ericson 5.1.9, line for line as SpawnableJackstraw.SegmentSegmentSqr."""
    d1, d2, r = sub(q1, p1), sub(q2, p2), sub(p1, p2)
    a, e, f = dot(d1, d1), dot(d2, d2), dot(d2, r)
    if a <= 1e-6 and e <= 1e-6:
        return dot(r, r)
    if a <= 1e-6:
        s, t = 0.0, clamp01(f / e)
    else:
        c = dot(d1, r)
        if e <= 1e-6:
            t, s = 0.0, clamp01(-c / a)
        else:
            b = dot(d1, d2)
            denom = a * e - b * b
            s = clamp01((b * f - c * e) / denom) if denom > 1e-6 else 0.0
            t = (b * s + f) / e
            if t < 0.0:
                t, s = 0.0, clamp01(-c / a)
            elif t > 1.0:
                t, s = 1.0, clamp01((b - c) / a)
    d = sub(add(p1, d1, s), add(p2, d2, t))
    return dot(d, d)


def clearings(p):
    out = []
    if p["centreClearingRadius"] > 0:
        out.append(((0.0, 0.0, 0.0), p["centreClearingRadius"]))
    for k in range(p["ringClearingCount"]):
        f = fibonacci(k, p["ringClearingCount"])
        out.append(((f[0] * p["ringClearingDistance"], f[1] * p["ringClearingDistance"],
                     f[2] * p["ringClearingDistance"]), p["ringClearingRadius"]))
    return out


def build(p):
    """Lay the heap for one prefab's fields. Returns pieces (for drawing) and the census.

    p: seed, heapRadius, stickCount, jackCount, centreClearingRadius, ringClearingCount, ringClearingDistance,
       ringClearingRadius, dangerTipEveryNth, ironwoodEveryNth.
    """
    seed = p["seed"] or DEFAULT_SEED
    clear = clearings(p)
    segs = []
    prisms = []      # (centre, axis, size(x, y, z), kind)
    margins = []     # least distance past each boundary an ACCEPTED piece has (for the float32 guard)
    jacks = sticks = 0

    def room(a, b):
        worst = float("inf")
        for c, r in clear:
            d = math.sqrt(point_segment_sqr(c, a, b)) - (r + SECTION)
            if d < 0:
                return None
            worst = min(worst, d)
        for sa, sb in segs:
            d = math.sqrt(segment_segment_sqr(a, b, sa, sb)) - STICK_GAP
            if d < 0:
                return None
            worst = min(worst, d)
        return worst

    stick_size = (SECTION, SECTION, SEGMENT * FLUSH)

    for j in range(p["jackCount"]):
        for t in range(STICK_TRIES):
            centre = in_ball(key(seed, JACK_KEY_BASE, j, t, 0), p["heapRadius"] - JACK_REACH)
            x = unit_from(key(seed, JACK_KEY_BASE, j, t, 1))
            helper = (0.0, 0.0, 1.0) if abs(x[2]) < 0.9 else (1.0, 0.0, 0.0)
            y = norm(cross(x, helper))
            z = cross(x, y)
            worst = float("inf")
            ok = True
            for ax in (x, y, z):
                m = room(add(centre, ax, -JACK_REACH), add(centre, ax, JACK_REACH))
                if m is None:
                    ok = False
                    break
                worst = min(worst, m)
            if not ok:
                continue
            margins.append(worst)
            for ax in (x, y, z):
                segs.append((add(centre, ax, -JACK_REACH), add(centre, ax, JACK_REACH)))
            prisms.append((centre, z, (KNOT, KNOT, KNOT), "shielded"))
            for ax in (x, y, z, (-x[0], -x[1], -x[2]), (-y[0], -y[1], -y[2]), (-z[0], -z[1], -z[2])):
                for k in range(JACK_ARM_PRISMS):
                    prisms.append((add(centre, ax, KNOT * 0.5 + SEGMENT * (k + 0.5)), ax, stick_size, "plain"))
            jacks += 1
            break

    d_every, i_every = p["dangerTipEveryNth"], p["ironwoodEveryNth"]
    for s in range(p["stickCount"]):
        for t in range(STICK_TRIES):
            centre = in_ball(key(seed, STICK_KEY_BASE, s, t, 0), p["heapRadius"] - STICK_HALF)
            axis = unit_from(key(seed, STICK_KEY_BASE, s, t, 1))
            a, b = add(centre, axis, -STICK_HALF), add(centre, axis, STICK_HALF)
            m = room(a, b)
            if m is None:
                continue
            margins.append(m)
            segs.append((a, b))
            red = d_every > 0 and s % d_every == 0
            iron = i_every > 1 and s % i_every == 1
            start = -STICK_HALF + SEGMENT * 0.5
            for k in range(STICK_PRISMS):
                kind = "plain"
                if red and k in (0, STICK_PRISMS - 1):
                    kind = "danger"
                elif iron and IRONWOOD_FROM <= k < IRONWOOD_TO:
                    kind = "shielded"
                prisms.append((add(centre, axis, start + SEGMENT * k), axis, stick_size, kind))
            sticks += 1
            break

    census = {
        "prisms": len(prisms),
        "plain": sum(1 for q in prisms if q[3] == "plain"),
        "shielded": sum(1 for q in prisms if q[3] == "shielded"),
        "danger": sum(1 for q in prisms if q[3] == "danger"),
        "volume": sum(q[2][0] * q[2][1] * q[2][2] for q in prisms),
        "sticks": sticks, "jacks": jacks,
        "skippedSticks": p["stickCount"] - sticks, "skippedJacks": p["jackCount"] - jacks,
        "minMargin": min(margins) if margins else float("inf"),
    }
    return {"prisms": prisms, "clearings": clear, "census": census}


# ── The ladder: one SpawnableJackstraw prefab per intensity (the approved drawing, 2026-11-09) ──
# spawnRing is the ServerPlayerVesselInitializer's per-intensity ring floor: just outside the heap, so every pilot
# starts in open water facing it. The membrane is the standard 1,200 u cell at every rung (the Boneyard's).
RUNGS = [
    dict(i=1, label="Loose Sticks", seed=8401, heapRadius=360, stickCount=34, jackCount=0,
         centreClearingRadius=120, ringClearingCount=0, ringClearingDistance=0, ringClearingRadius=0,
         dangerTipEveryNth=0, ironwoodEveryNth=0, spawnRing=450,
         blurb="a loose heap of 34 sticks round one clearing - room to learn the whip"),
    dict(i=2, label="Woodpile", seed=8402, heapRadius=470, stickCount=78, jackCount=6,
         centreClearingRadius=100, ringClearingCount=4, ringClearingDistance=280, ringClearingRadius=70,
         dangerTipEveryNth=8, ironwoodEveryNth=10, spawnRing=560,
         blurb="78 sticks and 6 jacks round five clearings; red tips on one stick in eight, ironwood hearts on one in ten"),
    dict(i=3, label="Bramble", seed=8403, heapRadius=570, stickCount=140, jackCount=14,
         centreClearingRadius=90, ringClearingCount=6, ringClearingDistance=330, ringClearingRadius=65,
         dangerTipEveryNth=5, ironwoodEveryNth=6, spawnRing=660,
         blurb="140 sticks and 14 jacks round seven clearings; every swing clips something; red tips on one stick in five"),
    dict(i=4, label="Thicket", seed=8404, heapRadius=680, stickCount=230, jackCount=26,
         centreClearingRadius=80, ringClearingCount=8, ringClearingDistance=400, ringClearingRadius=60,
         dangerTipEveryNth=3, ironwoodEveryNth=4, spawnRing=770,
         blurb="230 sticks and 26 jacks round nine small clearings; red tips on one stick in three, ironwood on one in four"),
]


if __name__ == "__main__":
    for r in RUNGS:
        c = build(r)["census"]
        print(f"I{r['i']} {r['label']:13} prisms {c['prisms']:5}  shielded {c['shielded']:4}  danger {c['danger']:4}"
              f"  volume {c['volume']:9.0f}  sticks {c['sticks']}/{r['stickCount']}  jacks {c['jacks']}/{r['jackCount']}"
              f"  margin {c['minMargin']:.3f}")
