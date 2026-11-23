#!/usr/bin/env python3
"""
The Posts - Standoff's arena (GameModes.Standoff = 125, the Grizzly's CHARGE game) - as a MIRROR of
SpawnableStandoff.cs, not an estimate. author_standoff_assets.py imports it for the census, the laid volume and the
PhaseThresholds, and the stills are drawn from it, so the picture, the numbers and the build cannot drift apart.

There are no draws at all: square POSTS of tiles stand on a sphere round an open centre, one per point of a Fibonacci
sphere, each facing the centre; an inner ring of smaller posts (turned INNER_TWIST_DEGREES so none sits square in front
of an outer one) from I3. Every tile's place is closed form, so the census is exact arithmetic.

Run it to print the ladder:   python3 Tools/Build/standoff_budget.py
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from jackstraw_budget import fibonacci, add, sub, dot, cross, norm  # noqa: E402

# ── Constants: must match SpawnableStandoff.cs (the generator greps for each) ──
TILE_WIDTH = 8.0
TILE_THICKNESS = 3.0
TILE_PITCH = 9.0
INNER_TWIST_DEGREES = 23.0
INNER_TWIST_AXIS = (0.6, 0.48, 0.64)
DEFAULT_SEED = 12525
LAY_CAPACITY = 1536

# Two tiles of DIFFERENT posts must be farther apart than this (a tile's full 3D diagonal: two tiles any closer could
# touch however they are turned).
TILE_DIAGONAL = math.sqrt(2 * TILE_WIDTH ** 2 + TILE_THICKNESS ** 2)


def rotate(v, axis, degrees):
    """Quaternion.AngleAxis(degrees, axis) * v (Rodrigues)."""
    k = norm(axis)
    t = math.radians(degrees)
    c, s = math.cos(t), math.sin(t)
    kxv = cross(k, v)
    kdv = dot(k, v)
    return tuple(v[i] * c + kxv[i] * s + k[i] * kdv * (1 - c) for i in range(3))


def post_direction(p, r):
    n = r["postCount"]
    if p < n:
        return fibonacci(p, n)
    return rotate(fibonacci(p - n, r["innerPostCount"]), INNER_TWIST_AXIS, INNER_TWIST_DEGREES)


def build(r):
    """Lay the posts for one prefab's fields. Returns the tiles (for drawing), the posts and the census.

    r: postCount, postRadius, postGrid, innerPostCount, innerPostRadius, innerPostGrid, shieldedRim, dangerCorners.
    """
    tiles = []      # (centre, normal, kind, post)
    posts = []      # (direction, radius, grid)
    total = r["postCount"] + r["innerPostCount"]
    for p in range(total):
        d = post_direction(p, r)
        outer = p < r["postCount"]
        radius = r["postRadius"] if outer else r["innerPostRadius"]
        grid = r["postGrid"] if outer else r["innerPostGrid"]
        posts.append((d, radius, grid))
        u = norm(cross(d, (0.0, 1.0, 0.0) if abs(d[1]) < 0.9 else (1.0, 0.0, 0.0)))
        v = cross(d, u)
        centre = tuple(c * radius for c in d)
        half = 0.5 * (grid - 1)
        last = grid - 1
        for a in range(grid):
            for b in range(grid):
                edge_a, edge_b = a in (0, last), b in (0, last)
                kind = ("danger" if r["dangerCorners"] and edge_a and edge_b
                        else "shielded" if r["shieldedRim"] and (edge_a or edge_b)
                        else "plain")
                pos = add(add(centre, u, (a - half) * TILE_PITCH), v, (b - half) * TILE_PITCH)
                tiles.append((pos, d, kind, p))

    # Least distance between two tiles of different posts (every pair: under 1,000 tiles, about a second).
    least = float("inf")
    by_post = [[t[0] for t in tiles if t[3] == p] for p in range(total)]
    for i in range(total):
        for j in range(i + 1, total):
            for a in by_post[i]:
                for b in by_post[j]:
                    d = math.dist(a, b)
                    if d < least:
                        least = d

    vol = TILE_WIDTH * TILE_WIDTH * TILE_THICKNESS
    census = {
        "prisms": len(tiles),
        "plain": sum(1 for t in tiles if t[2] == "plain"),
        "danger": sum(1 for t in tiles if t[2] == "danger"),
        "shielded": sum(1 for t in tiles if t[2] == "shielded"),
        "volume": len(tiles) * vol,
        "posts": total,
        "leastPostGap": least,
        "halfWidthOuter": 0.5 * (r["postGrid"] - 1) * TILE_PITCH + 0.5 * TILE_WIDTH,
    }
    return {"tiles": tiles, "posts": posts, "census": census}


# ── The ladder: one SpawnableStandoff prefab per intensity ──
# spawnRing: the ServerPlayerVesselInitializer's per-intensity ring, outside the posts, so every pilot starts in open
# ground facing them. omni: the scene's CrystalManager count per intensity, in a small ball at the open centre (an
# omni crystal raises every element: a bigger cannon, faster Energy at a post). The membrane is the standard 1,200 u
# cell at every rung.
RUNGS = [
    dict(i=1, label="Open Ground", seed=12501, postCount=8, postRadius=260, postGrid=5,
         innerPostCount=0, innerPostRadius=130, innerPostGrid=4, shieldedRim=0, dangerCorners=0,
         spawnRing=360, omni=3,
         blurb="eight posts round an open centre and three omni crystals - room to learn the sticky bomb"),
    dict(i=2, label="Earthworks", seed=12502, postCount=12, postRadius=280, postGrid=6,
         innerPostCount=0, innerPostRadius=130, innerPostGrid=4, shieldedRim=0, dangerCorners=0,
         spawnRing=380, omni=2,
         blurb="twelve wider posts and two omni crystals - cover everywhere you look"),
    dict(i=3, label="Redoubts", seed=12503, postCount=14, postRadius=300, postGrid=6,
         innerPostCount=4, innerPostRadius=130, innerPostGrid=4, shieldedRim=1, dangerCorners=0,
         spawnRing=400, omni=2,
         blurb="fourteen posts with shielded rims and an inner ring of four round the crystals"),
    dict(i=4, label="Killing Field", seed=12504, postCount=18, postRadius=320, postGrid=7,
         innerPostCount=6, innerPostRadius=140, innerPostGrid=4, shieldedRim=1, dangerCorners=1,
         spawnRing=420, omni=1,
         blurb="eighteen big posts and an inner six, shielded rims and danger corners, one omni crystal"),
]


if __name__ == "__main__":
    for r in RUNGS:
        c = build(r)["census"]
        print(f"I{r['i']} {r['label']:14} prisms {c['prisms']:5}  shielded {c['shielded']:4}  danger {c['danger']:3}"
              f"  volume {c['volume']:8.0f}  posts {c['posts']:3}  least gap {c['leastPostGap']:6.1f} u")
