#!/usr/bin/env python3
"""
The Kelp Ball - Kelpwood's arena (GameModes.Kelpwood = 86, the Angler's CHARGE game) - as a MIRROR of
SpawnableKelpwood.cs, not an estimate. author_kelpwood_assets.py imports it for the census, the laid volume and
the PhaseThresholds, and the approved drawing and the stills are drawn from it, so the picture, the numbers and the
build cannot drift apart. The C# was written for it:

  * every draw is Hash01 of an integer key (the verbatim port below, shared with jackstraw_budget.py);
  * every per-unit prism count is a const, and nothing is ever rejected: a strand is laid in full, every time.

So the census is exact arithmetic and the only float32/float64 difference is a hair of volume.

The kelp ball: STRANDS radiate from a small core in every direction (one per point of a Fibonacci sphere: no up),
each STRAND_PRISMS flush prisms along a gently swaying curve out to the kelp radius. I3 tips every third strand in
danger; I4 adds a danger COLLAR round every second strand, mid-way out. The herbivores the Angler tames and the omni
crystals are the cell's own (the spawn profile and the scene's CrystalManager), not laid here.

Run it to print the ladder:   python3 Tools/Build/kelpwood_budget.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jackstraw_budget import hash01, i32, fibonacci, add, sub, dot, cross, norm, segment_segment_sqr  # noqa: E402

# ── Constants: must match SpawnableKelpwood.cs (the generator greps for each) ──
STRAND_PRISMS = 20
SECTION = 3.0
STRAND_INNER = 50.0
SWAY_MIN, SWAY_SPAN = 6.0, 8.0
SWAY_TURNS = 3.2
TIP_PRISMS = 4
COLLAR_PRISMS = 6
COLLAR_RADIUS = 7.0
COLLAR_SECTION = 2.5
COLLAR_LENGTH = 6.0
COLLAR_AT = 11
FLUSH = 0.98
STRAND_KEY_BASE = 3_000_000
DEFAULT_SEED = 8686

STRAND_GAP = 6.0     # least distance between two strands' axes (a box is 3 u wide: 4.25 u is touching corners)
MIN_FLUSH_SLACK = 0.02  # u of air left at the worst bend between two prisms of a strand


def key(seed, family, slot, draw):
    return i32(seed * 7919 + family + slot * 4 + draw)


def helper_for(a):
    return (0.0, 1.0, 0.0) if abs(a[1]) < 0.9 else (1.0, 0.0, 0.0)


def strand_curve(seed, s, n, kelp_radius):
    """The STRAND_PRISMS + 1 knots of strand s of n, in the structure's local space."""
    d = fibonacci(s, n)
    u = norm(cross(d, helper_for(d)))
    v = cross(d, u)
    phase = 2.0 * math.pi * hash01(key(seed, STRAND_KEY_BASE, s, 0))
    amp = SWAY_MIN + SWAY_SPAN * hash01(key(seed, STRAND_KEY_BASE, s, 1))
    side = add(u, v, 0.5)
    pts = []
    for k in range(STRAND_PRISMS + 1):
        t = k / STRAND_PRISMS
        off = amp * math.sin(phase + SWAY_TURNS * t) * t
        pts.append(add(tuple(c * (STRAND_INNER + (kelp_radius - STRAND_INNER) * t) for c in d), side, off))
    return pts


def build(p, check_gaps=True):
    """Lay the kelp ball for one prefab's fields. Returns pieces (for drawing), the strand axes and the census.

    p: seed, kelpRadius, strandCount, dangerTipEveryNth, collarEveryNth.
    """
    seed = p["seed"] or DEFAULT_SEED
    n, R = p["strandCount"], p["kelpRadius"]
    tip_every, collar_every = p["dangerTipEveryNth"], p["collarEveryNth"]
    prisms = []           # (centre, axis, size(x, y, z), kind)
    strands = []          # list of knot lists (the axes)
    worst_slack = float("inf")
    for s in range(n):
        pts = strand_curve(seed, s, n, R)
        strands.append(pts)
        tips = tip_every > 0 and s % tip_every == 0
        axes, chords = [], []
        for k in range(STRAND_PRISMS):
            a, b = pts[k], pts[k + 1]
            ab = sub(b, a)
            chord = math.sqrt(dot(ab, ab))
            axis = norm(ab)
            axes.append(axis)
            chords.append(chord)
            kind = "danger" if tips and k >= STRAND_PRISMS - TIP_PRISMS else "plain"
            prisms.append((add(a, ab, 0.5), axis, (SECTION, SECTION, chord * FLUSH), kind))
        # Flush at every bend: the air the two prisms leave between them must cover their corners' swing.
        for k in range(STRAND_PRISMS - 1):
            theta = math.acos(max(-1.0, min(1.0, dot(axes[k], axes[k + 1]))))
            air = 0.5 * (chords[k] + chords[k + 1]) * (1.0 - FLUSH)
            worst_slack = min(worst_slack, air - SECTION * math.tan(theta * 0.5))
        if collar_every > 1 and s % collar_every == 1:
            c = add(pts[COLLAR_AT], sub(pts[COLLAR_AT + 1], pts[COLLAR_AT]), 0.5)
            ax = axes[COLLAR_AT]
            cu = norm(cross(ax, helper_for(ax)))
            cv = cross(ax, cu)
            for j in range(COLLAR_PRISMS):
                ang = 2.0 * math.pi * j / COLLAR_PRISMS
                pos = add(add(c, cu, math.cos(ang) * COLLAR_RADIUS), cv, math.sin(ang) * COLLAR_RADIUS)
                prisms.append((pos, ax, (COLLAR_SECTION, COLLAR_SECTION, COLLAR_LENGTH), "danger"))

    # Strands keep apart (checked against near neighbours only: two strands 60 degrees apart never meet).
    min_gap = float("inf")
    if check_gaps:
        dirs = [fibonacci(s, n) for s in range(n)]
        for i in range(n):
            for j in range(i + 1, n):
                if dot(dirs[i], dirs[j]) < 0.5:
                    continue
                a, b = strands[i], strands[j]
                for x in range(STRAND_PRISMS):
                    for y in range(STRAND_PRISMS):
                        d2 = segment_segment_sqr(a[x], a[x + 1], b[y], b[y + 1])
                        if d2 < min_gap * min_gap:
                            min_gap = math.sqrt(d2)

    census = {
        "prisms": len(prisms),
        "plain": sum(1 for q in prisms if q[3] == "plain"),
        "danger": sum(1 for q in prisms if q[3] == "danger"),
        "shielded": 0,
        "volume": sum(q[2][0] * q[2][1] * q[2][2] for q in prisms),
        "strands": n,
        "minStrandGap": min_gap,
        "worstFlushSlack": worst_slack,
    }
    return {"prisms": prisms, "strands": strands, "census": census}


# ── The ladder: one SpawnableKelpwood prefab per intensity (the approved drawing, Shombith 2026-11-09) ──
# spawnRing: the ServerPlayerVesselInitializer's per-intensity ring, just outside the kelp, so every pilot starts in
# open water facing it. herdPoints / herdsPerPoint: the cell's herbivore spawn (the spawn profile). omni: the scene's
# CrystalManager count per intensity (each omni crystal is one creature for an Angler). The membrane is the standard
# 1,200 u cell at every rung.
RUNGS = [
    dict(i=1, label="Kelp Bed", seed=8601, kelpRadius=288, strandCount=28, dangerTipEveryNth=0, collarEveryNth=0,
         spawnRing=368, herdPoints=8, herdsPerPoint=3, omni=3,
         blurb="28 strands round an open core, eight grazing herds and three omni crystals - room to learn the lock"),
    dict(i=2, label="Kelp Forest", seed=8602, kelpRadius=320, strandCount=44, dangerTipEveryNth=0, collarEveryNth=0,
         spawnRing=400, herdPoints=6, herdsPerPoint=3, omni=2,
         blurb="44 strands, six herds and two omni crystals - rivals start to vanish into the strands"),
    dict(i=3, label="Kelp Tangle", seed=8603, kelpRadius=352, strandCount=64, dangerTipEveryNth=3, collarEveryNth=0,
         spawnRing=432, herdPoints=5, herdsPerPoint=3, omni=2,
         blurb="64 strands, every third tipped in danger; five herds and two omni crystals"),
    dict(i=4, label="Kelp Snare", seed=8604, kelpRadius=384, strandCount=88, dangerTipEveryNth=3, collarEveryNth=2,
         spawnRing=464, herdPoints=4, herdsPerPoint=3, omni=1,
         blurb="88 strands, danger tips and danger collars mid-strand; four herds and one omni crystal"),
]


if __name__ == "__main__":
    for r in RUNGS:
        c = build(r)["census"]
        print(f"I{r['i']} {r['label']:12} prisms {c['prisms']:5}  danger {c['danger']:4}  volume {c['volume']:9.0f}"
              f"  strands {c['strands']:3}  min gap {c['minStrandGap']:6.2f} u  flush slack {c['worstFlushSlack']:.3f} u")
