#!/usr/bin/env python3
"""
The Corona - Corona's arena (GameModes.Corona = 87, the Manta's CHARGE game) - as a MIRROR of SpawnableCorona.cs, not
an estimate. author_corona_assets.py imports it for the census, the laid volume and the PhaseThresholds, and the
approved drawing and the stills are drawn from it, so the picture, the numbers and the build cannot drift apart.

The C# was written for it:
  * there are no random draws: every tile is a point of a Fibonacci sphere, every per-unit size is a const;
  * the only comparisons that change a count are "is this tile inside a gate" and "inside a gate's danger rim".
    build() measures how far every tile is from both boundaries and the generator fails under MIN_MARGIN, so a
    float32 (C#) against float64 (here) difference cannot move a tile across one.

The arena: SHELLS of flat tiles round an open crystal HEART, each with round GATES cut through it, each shell's gates
turned SHELL_TWIST_DEGREES further than the last, so no straight line runs from the rim to the heart. The omni
crystals (the Manta's cash-out) are the scene's CrystalManager's, in a small ball at the heart - not laid here.

Run it to print the ladder:   python3 Tools/Build/corona_budget.py
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jackstraw_budget import fibonacci, cross, norm  # noqa: E402

# ── Constants: must match SpawnableCorona.cs (the generator greps for each) ──
TILE_SPACING = 32.0
TILE_WIDTH = 18.0
TILE_THICKNESS = 3.0
SHELL_TWIST_DEGREES = 37.0
TWIST_AXIS = (0.48, 0.64, 0.6)
DEFAULT_SEED = 8787

MIN_MARGIN = 1e-4     # least distance (in cosine) from a tile to a gate or rim boundary
HEART_CLEAR = 60.0    # the omni crystals' ball must leave this much air inside the innermost shell


def tiles_for(radius):
    # Mathf.RoundToInt rounds half to even, as Python's round() does.
    return int(round(4.0 * math.pi * radius * radius / (TILE_SPACING * TILE_SPACING)))


def rotate(v, axis, degrees):
    """Quaternion.AngleAxis(degrees, axis) * v (Rodrigues; AngleAxis normalises the axis)."""
    k = norm(axis)
    t = math.radians(degrees)
    c, s = math.cos(t), math.sin(t)
    kxv = cross(k, v)
    kdv = k[0] * v[0] + k[1] * v[1] + k[2] * v[2]
    return tuple(v[i] * c + kxv[i] * s + k[i] * kdv * (1 - c) for i in range(3))


def shell_radius(p, s):
    return p["innerShellRadius"] + p["shellSpacing"] * s


def gate_direction(p, s, g):
    return norm(rotate(fibonacci(g, p["gatesPerShell"]), TWIST_AXIS, SHELL_TWIST_DEGREES * s))


def build(p):
    """Lay the shells for one prefab's fields. Returns the tiles (for drawing), the gates and the census.

    p: shellCount, innerShellRadius, shellSpacing, gatesPerShell, gateHalfAngle, rimDegrees.
    """
    cos_gate = math.cos(math.radians(p["gateHalfAngle"]))
    cos_rim = math.cos(math.radians(p["gateHalfAngle"] + p["rimDegrees"]))
    tiles = []            # (centre, normal, tangent, kind, shell)
    gates = []            # (shell, direction, radius)
    margin = float("inf")
    least_gap = float("inf")   # per shell: least distance between two neighbouring Fibonacci points
    for s in range(p["shellCount"]):
        r = shell_radius(p, s)
        n = tiles_for(r)
        gd = [gate_direction(p, s, g) for g in range(p["gatesPerShell"])]
        gates += [(s, d, r) for d in gd]
        pts = np.array([fibonacci(k, n) for k in range(n)])
        G = np.array(gd)
        nearest = (pts @ G.T).max(axis=1)
        margin = min(margin, float(np.abs(nearest - cos_gate).min()))
        if p["rimDegrees"] > 0:
            margin = min(margin, float(np.abs(nearest - cos_rim).min()))
        # Least neighbour distance: Fibonacci points are ordered by height, so a point's neighbours are within a
        # window of about 2 sqrt(n) indices; compare against the next W points.
        W = int(3 * math.sqrt(n)) + 8
        for off in range(1, W):
            d = np.linalg.norm(pts[off:] - pts[:-off], axis=1)
            if d.size:
                least_gap = min(least_gap, float(d.min()) * r)
        for k in range(n):
            if nearest[k] > cos_gate:
                continue
            kind = "danger" if p["rimDegrees"] > 0 and nearest[k] > cos_rim else "plain"
            q = tuple(float(x) for x in pts[k])
            helper = (0.0, 1.0, 0.0) if abs(q[1]) < 0.9 else (1.0, 0.0, 0.0)
            tangent = norm(cross(q, helper))
            tiles.append((tuple(x * r for x in q), q, tangent, kind, s))

    census = {
        "prisms": len(tiles),
        "plain": sum(1 for t in tiles if t[3] == "plain"),
        "danger": sum(1 for t in tiles if t[3] == "danger"),
        "shielded": 0,
        "volume": len(tiles) * TILE_WIDTH * TILE_WIDTH * TILE_THICKNESS,
        "shells": p["shellCount"],
        "gates": p["shellCount"] * p["gatesPerShell"],
        "boundaryMargin": margin,
        "leastTileGap": least_gap,
        # A square tile at any in-plane angle stays inside a circle of its half diagonal: two tiles cannot meet while
        # their centres are further apart than one full diagonal.
        "tileDiagonal": TILE_WIDTH * math.sqrt(2.0),
        "gateWidthInner": 2.0 * shell_radius(p, 0) * math.radians(p["gateHalfAngle"]),
        "outerRadius": shell_radius(p, p["shellCount"] - 1),
    }
    return {"tiles": tiles, "gates": gates, "census": census}


# ── The ladder: one SpawnableCorona prefab per intensity (the approved drawing, Shombith 2026-11-10) ──
# spawnRing: the ServerPlayerVesselInitializer's per-intensity ring, outside the outer shell, so every pilot starts in
# open water facing in. omni: the scene's CrystalManager count per intensity, in a heartRadius ball at the centre.
# The membrane is the standard 1,200 u cell at every rung.
RUNGS = [
    dict(i=1, label="Halo", seed=8701, shellCount=1, innerShellRadius=280, shellSpacing=120, gatesPerShell=8,
         gateHalfAngle=24.25, rimDegrees=0, spawnRing=380, omni=4,
         blurb="one shell with eight wide gates round a heart of four omni crystals - room to learn skim, graze, cash"),
    dict(i=2, label="Corona", seed=8702, shellCount=2, innerShellRadius=190, shellSpacing=120, gatesPerShell=6,
         gateHalfAngle=19.75, rimDegrees=0, spawnRing=410, omni=3,
         blurb="two shells, six gates each, the inner gates turned away from the outer; three omni crystals"),
    dict(i=3, label="Chromosphere", seed=8703, shellCount=3, innerShellRadius=150, shellSpacing=100, gatesPerShell=5,
         gateHalfAngle=17.25, rimDegrees=6.25, spawnRing=450, omni=2,
         blurb="three shells, five gates each with a danger rim; two omni crystals"),
    dict(i=4, label="Flare", seed=8704, shellCount=4, innerShellRadius=130, shellSpacing=90, gatesPerShell=4,
         gateHalfAngle=14.25, rimDegrees=6.75, spawnRing=500, omni=1,
         blurb="four shells, four narrow rimmed gates each; one omni crystal in the heart"),
]
HEART_RADIUS = 45     # CrystalManager.noNucleusSpawnRadius: the omni crystals float in a ball this wide at the centre


if __name__ == "__main__":
    for r in RUNGS:
        c = build(r)["census"]
        print(f"I{r['i']} {r['label']:13} prisms {c['prisms']:5}  danger {c['danger']:4}  volume {c['volume']:9.0f}"
              f"  shells {c['shells']}  gates {c['gates']:2}  inner gate {c['gateWidthInner']:5.1f} u"
              f"  least tile gap {c['leastTileGap']:5.2f} u (diagonal {c['tileDiagonal']:.2f})"
              f"  boundary margin {c['boundaryMargin']:.5f}")
