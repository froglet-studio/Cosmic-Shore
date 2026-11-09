#!/usr/bin/env python3
"""
Analytic prism budget for Flatline - the Brittle Star's charge arena (GameModes.Flatline = 84).

This is the model of `FlatlinePanes.cs` (and so of `SpawnableFlatline.cs`), and it is a MIRROR, not an estimate
(boneyard_budget.py's contract): `author_flatline_assets.py` imports it, so the arena's PhaseThresholds come from the
same numbers that build the arena. It is exact because the C# was written to make it exact:

  * which tiles a pane has is decided on the INTEGER grid (a tile is in when a*a + b*b <= R*R), so the per-pane count
    is integer arithmetic;
  * the only float comparison that can move a count is the seam test (a later pane leaves a slot round an earlier
    pane's plane), computed here in float32 in the C#'s order (numpy float32 scalars);
  * there is no random draw: the holes are `Hash01` of the grid cell, ported verbatim.

A change to any constant, the basis rule, the normals, the seam test, the hole hash or the build order in
FlatlinePanes.cs must land here too; `author_flatline_assets.py --check` fails when the C# constants move, and
`FlatlineTests` assert the C# counts this file prints.

    python3 Tools/Build/flatline_budget.py            # the table that belongs in FLATLINE.md
    python3 Tools/Build/flatline_budget.py --sites    # plus each rung's crystal sites

CONFIRM IN EDITOR: FrogletTools > Ecology > Measure Cell Environment Baselines. If the measurer disagrees with this
table, the C# and this file have drifted - fix both, do not paper over it in the asset.
"""
import math
import sys

import numpy as np

f32 = np.float32

# ── Constants: must match FlatlinePanes.cs ────────────────────────────────────
TILE = 10.0
THICKNESS = 2.0
FLUSH = 0.995
SEAM_HALF_WIDTH = 8.5
SEAM_REACH_MARGIN = TILE
HOLE_SALT = 5

THREE_PLANES, FIVE_PAGES, ICOSA_PANES, PANE_GEODE = 0, 1, 2, 3
PLAIN, DANGER, SHIELDED, SUPER = 0, 1, 2, 3

# ── The ladder (the generator authors these onto the four prefabs) ────────────
# (arrangement, pane radius in tiles, offset, rib every, danger rim tiles, hole share, hole cell, crystals, spacing)
RUNGS = [
    dict(i=1, label="Three Planes", arrangement=THREE_PLANES, radiusTiles=23, offset=0.0, ribEvery=6,
         dangerRimTiles=0, holeShare=0.0, holeCellTiles=4, crystalCount=6, crystalSpacing=110.0,
         blurb="three panes on the three axes, eight open rooms between them, six crystals on the seams"),
    dict(i=2, label="Five Pages", arrangement=FIVE_PAGES, radiusTiles=24, offset=0.0, ribEvery=6,
         dangerRimTiles=0, holeShare=0.0, holeCellTiles=4, crystalCount=5, crystalSpacing=60.0,
         blurb="five panes round one spine like the pages of a book, ten narrow rooms, five crystals up the spine"),
    dict(i=3, label="Icosa Panes", arrangement=ICOSA_PANES, radiusTiles=21, offset=0.0, ribEvery=6,
         dangerRimTiles=2, holeShare=0.0, holeCellTiles=4, crystalCount=6, crystalSpacing=110.0,
         blurb="six panes on the icosahedral axes crossing at every angle, danger on every rim, six crystals on "
               "six of the fifteen seams"),
    dict(i=4, label="Pane Geode", arrangement=PANE_GEODE, radiusTiles=15, offset=150.0, ribEvery=5,
         dangerRimTiles=2, holeShare=0.25, holeCellTiles=4, crystalCount=4, crystalSpacing=0.0,
         blurb="ten panes standing off the centre as a hollow shell with two open faces and holes punched "
               "through, danger on every rim, four crystals in the cracks"),
]


def hash01(n):
    """CellEnvironmentSpawnableBase.Hash01 / FlatlinePanes.Hash01, verbatim (uint32 wraparound)."""
    h = n & 0xFFFFFFFF
    h = ((h ^ 61) ^ (h >> 16)) & 0xFFFFFFFF
    h = (h * 9) & 0xFFFFFFFF
    h ^= h >> 4
    h = (h * 0x27D4EB2D) & 0xFFFFFFFF
    h ^= h >> 15
    return float(f32(h & 0xFFFFFF) / f32(0x1000000))


def v3(x, y, z):
    return np.array([x, y, z], dtype=np.float32)


def dot(a, b):
    # Unity: a.x*b.x + a.y*b.y + a.z*b.z, left to right, in float32.
    return f32(f32(f32(a[0] * b[0]) + f32(a[1] * b[1])) + f32(a[2] * b[2]))


def cross(a, b):
    return v3(f32(a[1] * b[2]) - f32(a[2] * b[1]), f32(a[2] * b[0]) - f32(a[0] * b[2]),
              f32(a[0] * b[1]) - f32(a[1] * b[0]))


def normalized(a):
    # Unity's Vector3.normalized: divide by the magnitude when it exceeds 1e-5.
    m = f32(math.sqrt(float(dot(a, a))))
    return (a / m).astype(np.float32) if m > f32(1e-5) else v3(0, 0, 0)


def normals(arrangement):
    if arrangement == THREE_PLANES:
        return [v3(1, 0, 0), v3(0, 1, 0), v3(0, 0, 1)]
    if arrangement == FIVE_PAGES:
        out = []
        for k in range(5):
            a = f32(f32(math.pi) * f32(k) / f32(5))
            out.append(v3(math.cos(a), 0, math.sin(a)))
        return out
    phi = f32((f32(1) + f32(math.sqrt(5.0))) * f32(0.5))
    out = [normalized(v3(0, 1, phi)), normalized(v3(0, 1, -phi)), normalized(v3(1, phi, 0)),
           normalized(v3(1, -phi, 0)), normalized(v3(phi, 0, 1)), normalized(v3(phi, 0, -1))]
    if arrangement == PANE_GEODE:
        out += [(-out[k]).astype(np.float32) for k in range(4)]
    return out


def basis(n):
    t = v3(1, 0, 0) if abs(float(n[0])) < 0.9 else v3(0, 1, 0)
    u = normalized(cross(n, t))
    w = cross(n, u)
    return u, w


def panes(arrangement, radius_tiles, offset):
    out = []
    for n in normals(arrangement):
        u, w = basis(n)
        out.append(dict(n=n, c=(n * f32(offset)).astype(np.float32), u=u, w=w, rt=radius_tiles,
                        r=f32(radius_tiles * TILE)))
    return out


def floor_div(a, b):
    return a // b   # Python floors, as FlatlinePanes.FloorDiv does


def in_seam(ps, j, p):
    for i in range(j):
        o = ps[i]
        rel = (p - o["c"]).astype(np.float32)
        dist = dot(rel, o["n"])
        if abs(float(dist)) >= SEAM_HALF_WIDTH:
            continue
        in_plane = (rel - (o["n"] * dist).astype(np.float32)).astype(np.float32)
        reach = f32(o["r"] + f32(SEAM_REACH_MARGIN))
        if dot(in_plane, in_plane) < f32(reach * reach):
            return True
    return False


def tiles(ps, danger_rim, rib_every, hole_share, hole_cell, keep=False):
    """[(pane, a, b, kind, position)] in the C#'s order (pane, then a, then b)."""
    out = []
    for j, pane in enumerate(ps):
        r = pane["rt"]
        inner2 = max(0, r - danger_rim) ** 2
        for a in range(-r, r + 1):
            for b in range(-r, r + 1):
                d2 = a * a + b * b
                if d2 > r * r:
                    continue
                p = (pane["c"] + (pane["u"] * f32(a * TILE)).astype(np.float32)).astype(np.float32)
                p = (p + (pane["w"] * f32(b * TILE)).astype(np.float32)).astype(np.float32)
                if in_seam(ps, j, p):
                    continue
                if danger_rim > 0 and d2 > inner2:
                    kind = DANGER
                elif rib_every > 0 and (a % rib_every == 0 or b % rib_every == 0):
                    kind = SUPER
                else:
                    if hole_share > 0 and hole_cell > 0:
                        cx, cy = floor_div(a, hole_cell), floor_div(b, hole_cell)
                        if hash01(j * 7919 + cx * 131 + cy * 17 + HOLE_SALT) < f32(hole_share):
                            continue
                    kind = PLAIN
                out.append((j, a, b, kind, p if keep else None))
    return out


def crystal_sites(ps, count, spacing):
    points, dirs = [], []
    for i in range(len(ps)):
        for j in range(i + 1, len(ps)):
            n1, n2 = ps[i]["n"], ps[j]["n"]
            d = cross(n1, n2)
            dd = dot(d, d)
            if dd < f32(1e-6):
                continue
            h1, h2 = dot(n1, ps[i]["c"]), dot(n2, ps[j]["c"])
            p0 = ((cross(n2, d) * h1 + cross(d, n1) * h2) / dd).astype(np.float32)
            if np.linalg.norm(p0 - ps[i]["c"]) > ps[i]["r"] or np.linalg.norm(p0 - ps[j]["c"]) > ps[j]["r"]:
                continue
            dn = normalized(d)
            seen = False
            for k in range(len(dirs)):
                if abs(float(dot(dirs[k], dn))) < 0.999:
                    continue
                off = p0 - points[k]
                perp = off - dirs[k] * dot(off, dirs[k])
                if float(dot(perp, perp)) < 1.0:
                    seen = True
                    break
            if seen:
                continue
            points.append(p0)
            dirs.append(dn)
    sites = []
    for k in range(count if points else 0):
        line, step = k % len(points), k // len(points)
        t = spacing * (step // 2 + 1) * (1.0 if step % 2 == 0 else -1.0)
        sites.append(tuple(float(x) for x in points[line] + dirs[line] * f32(t)))
    return sites


def rung_layout(r, keep=False):
    ps = panes(r["arrangement"], r["radiusTiles"], r["offset"])
    ts = tiles(ps, r["dangerRimTiles"], r["ribEvery"], r["holeShare"], r["holeCellTiles"], keep)
    return ps, ts


TILE_VOLUME = (TILE * FLUSH) ** 2 * THICKNESS


def census(r):
    _, ts = rung_layout(r)
    kinds = [t[3] for t in ts]
    return dict(total=len(ts), plain=kinds.count(PLAIN), danger=kinds.count(DANGER), super=kinds.count(SUPER),
                volume=len(ts) * TILE_VOLUME)


def extent(r):
    """The farthest any tile reaches from the centre (u)."""
    return math.hypot(r["offset"], r["radiusTiles"] * TILE + TILE)


if __name__ == "__main__":
    print(f"{'rung':16} {'total':>7} {'plain':>7} {'danger':>7} {'super':>7} {'volume':>10} {'extent':>7}")
    for r in RUNGS:
        c = census(r)
        print(f"I{r['i']} {r['label']:13} {c['total']:7,} {c['plain']:7,} {c['danger']:7,} {c['super']:7,} "
              f"{c['volume']:10,.0f} {extent(r):7.0f}")
        if "--sites" in sys.argv:
            ps = panes(r["arrangement"], r["radiusTiles"], r["offset"])
            for s in crystal_sites(ps, r["crystalCount"], r["crystalSpacing"]):
                print(f"     site ({s[0]:.2f}, {s[1]:.2f}, {s[2]:.2f})")
