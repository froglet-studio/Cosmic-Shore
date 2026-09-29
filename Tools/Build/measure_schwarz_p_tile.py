#!/usr/bin/env python3
"""
Measure the non-Euclidean tile of the Schwarz P minimal surface, and emit the C#
table SchwarzPTileData.cs is built from.

    python3 Tools/Build/measure_schwarz_p_tile.py            # verify + print the emit
    python3 Tools/Build/measure_schwarz_p_tile.py --check    # verify only (CI gate)
    python3 Tools/Build/measure_schwarz_p_tile.py --write    # verify + rewrite the C# file

WHAT THE TILE IS
----------------
The Schwarz P surface  f(x,y,z) = cos x + cos y + cos z = 0  is intrinsically
HYPERBOLIC, so it carries no Euclidean lattice - which is why marching a
quasi-square grid across it (Newton-project, step, repeat) drifts, never closes
up, and has no repeat unit.  It does carry an exact non-Euclidean one.

The surface's full symmetry group is the space group Im-3m; its quotient orbifold
is the hyperbolic triangle group *246.  The regular tiling commensurate with it is
the hyperbolic {6,4}: hexagons with 90-degree corners, four to a vertex.  Realized
on the surface it is astonishingly concrete:

    THE TILE IS THE PATCH OF SURFACE INSIDE ONE HALF-PERIOD CUBE.

Space is cut by the {100} mirror planes  x,y,z in pi*Z  into cubes of side pi
(half a period).  Each cube holds exactly one FLAT POINT of the surface (K = 0,
normal along a body diagonal), and the surface patch inside it is one hyperbolic
hexagon:

  * 6 EDGES, one on each cube face, each a planar geodesic (the face is a mirror).
  * 6 CORNERS, on the 6 cube edges whose two ends straddle the surface; every corner
    is a 4-fold point of the surface and lies exactly in the flat point's TANGENT
    PLANE, six of them at the vertices of a regular hexagon.
  * 6 NEIGHBOURS - the 6 face-adjacent cubes.  Tile adjacency IS simple-cubic
    adjacency, so occupancy is exact integer keying with no quantization at all.

Each hexagon is 12 copies of the *246 Schwarz triangle, whose corners are
    A = (pi/2, pi/2, pi/2)   order 6   the flat point / tile centre
    B = (0,    pi/2, pi  )   order 4   a tile corner (shared by 4 tiles)
    C = (0,   2pi/3, 2pi/3)  order 2   a tile edge midpoint (shared by 2 tiles)
with edges on the mirrors {y = pi/2, x+z = pi} (a STRAIGHT LINE lying in the
surface), {y = z}, and {x = 0}.  This script measures its angles and gets
(30, 45, 90) degrees - the *246 signature - rather than asserting it.

Adjacent tiles are MIRROR IMAGES across their shared cube face, so the tile at
integer index (i,j,k) is the canonical tile mapped by T_ijk, which acts per axis:

    x -> x + pi*i          when i is even
    x -> pi*(i+1) - x      when i is odd

f is invariant under every T_ijk, so the whole tiling is one canonical patch plus
integer arithmetic.  No Newton iteration, no accumulated drift, exactly periodic.

WHAT IS MEASURED
----------------
The tile's combinatorics are exact and are PROVEN here, not fitted.  The only
fitted quantity is the prism layout INSIDE the tile: a hyperbolic patch admits no
uniform Euclidean lattice, so a covering of it has to be measured.  Each level is
seeded from a triangular lattice on the flat point's 6-fold axis, lifted onto the
patch, then equalized by a centroidal Voronoi relaxation in which every site
competes with every image of every site under the full symmetry group - including
its own mirror image across each tile seam.  Uniform spacing inside a tile and
uniform spacing ACROSS a tile boundary therefore fall out of one computation, with
nothing tuned for the seam.  The levels come out at the CENTERED HEXAGONAL numbers
(1, 7, 19, 37, 61, 91) because complete hexagonal shells are what the tile's own
6-fold symmetry admits.  Those positions are what gets emitted.

Verification of the SHIPPED C# is a separate script, and deliberately so:
Tools/Build/verify_schwarz_p_tile_tables.py re-derives every claim from the
implicit function by parsing SchwarzPTileData.cs, because the transcription
between a proven measurement and the asset is exactly the step neither the
measurement nor code review can see.
"""

from __future__ import annotations

import argparse
import itertools
import math
import pathlib
import sys

import numpy as np

PI = math.pi
TAU = 2.0 * PI

# ----------------------------------------------------------------------------
# The surface
# ----------------------------------------------------------------------------


def f(p):
    p = np.asarray(p, dtype=float)
    return np.cos(p[..., 0]) + np.cos(p[..., 1]) + np.cos(p[..., 2])


def grad(p):
    p = np.asarray(p, dtype=float)
    return -np.sin(p)


def unit_normal(p):
    g = grad(p)
    n = np.linalg.norm(g, axis=-1, keepdims=True)
    return g / np.where(n < 1e-12, 1.0, n)


def project(p, iterations=24):
    """Nearest-point Newton projection onto f = 0.  Independent per point, so it
    cannot accumulate drift the way a marching walk does."""
    p = np.array(p, dtype=float)
    for _ in range(iterations):
        v = f(p)
        g = grad(p)
        denom = np.sum(g * g, axis=-1)
        denom = np.where(denom < 1e-14, 1.0, denom)
        step = (v / denom)[..., None] * g
        p = p - step
        if np.max(np.abs(f(p))) < 1e-14:
            break
    return p


# ----------------------------------------------------------------------------
# The tile lattice:  T_ijk, per-axis reflect-and-translate
# ----------------------------------------------------------------------------


def axis_signs(ijk):
    """Diagonal of T_ijk's linear part: -1 on every odd axis."""
    return np.array([1.0 if (v % 2 == 0) else -1.0 for v in ijk])


def tile_transform(ijk, p):
    """Map a point of the canonical tile (the cube [0,pi]^3) into tile (i,j,k)."""
    p = np.asarray(p, dtype=float)
    out = np.empty_like(p)
    for a, i in enumerate(ijk):
        if i % 2 == 0:
            out[..., a] = p[..., a] + PI * i
        else:
            out[..., a] = PI * (i + 1) - p[..., a]
    return out


def tile_center(ijk):
    return np.array([PI * (i + 0.5) for i in ijk])


def tile_normal_dir(ijk):
    """Body-diagonal the tile's flat point normal runs along."""
    return axis_signs(ijk)


# ----------------------------------------------------------------------------
# The site symmetry group of a flat point:  -3m, order 12
# ----------------------------------------------------------------------------

# The 6 coordinate permutations, each optionally composed with the inversion
# p -> (pi,pi,pi) - p through the flat point.  f is invariant under permutation
# and negates under the inversion, so all 12 preserve the zero set and the cube.
SITE_OPS = []
for perm in itertools.permutations(range(3)):
    for invert in (False, True):
        SITE_OPS.append((perm, invert))
assert len(SITE_OPS) == 12


def apply_site_op(op, p):
    perm, invert = op
    p = np.asarray(p, dtype=float)
    q = p[..., list(perm)]
    if invert:
        q = PI - q
    return q




# ----------------------------------------------------------------------------
# The *246 Schwarz triangle - the fundamental domain of the whole group
# ----------------------------------------------------------------------------

CORNER_A = np.array([PI / 2, PI / 2, PI / 2])          # order 6 - flat point
CORNER_B = np.array([0.0, PI / 2, PI])                 # order 4 - tile corner
CORNER_C = np.array([0.0, 2 * PI / 3, 2 * PI / 3])     # order 2 - edge midpoint


def fold_to_canonical_tile(p):
    """T_ijk^-1: reflect-fold an arbitrary surface point back into [0,pi]^3, and
    report which tile it came from."""
    p = np.array(p, dtype=float)
    ijk = []
    for a in range(3):
        i = math.floor(p[a] / PI)
        local = p[a] - PI * i
        if i % 2 != 0:
            local = PI - local
        ijk.append(i)
        p[a] = local
    return p, tuple(ijk)


def fold_to_fundamental(p):
    """Fold a canonical-tile point into the *246 triangle: sort (S3), then halve
    with the involution sigma: (x,y,z) -> (pi-z, pi-y, pi-x)."""
    q = np.sort(np.asarray(p, dtype=float))
    if q.sum() > 1.5 * PI:
        q = np.sort(np.array([PI - q[2], PI - q[1], PI - q[0]]))
    return q




# ----------------------------------------------------------------------------
# Verification
# ----------------------------------------------------------------------------


class Failures(list):
    def check(self, ok, label, detail=""):
        status = "ok  " if ok else "FAIL"
        print(f"  [{status}] {label}" + (f"   {detail}" if detail else ""))
        if not ok:
            self.append(label)


def flat_points_in_cell():
    """Flat points of the surface inside one cubic (2pi) cell."""
    return [np.array([PI / 2 + PI * i, PI / 2 + PI * j, PI / 2 + PI * k])
            for i in range(2) for j in range(2) for k in range(2)]


def tile_corners(ijk=(0, 0, 0)):
    """The 6 corners of a tile: the 4-fold points on the cube edges."""
    c = tile_center(ijk)
    n = tile_normal_dir(ijk)
    out = []
    for a, b in itertools.permutations(range(3), 2):
        if a > b:
            continue
        for sa, sb in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
            v = np.zeros(3)
            v[a] = sa
            v[b] = sb
            if abs(np.dot(v, n)) > 1e-12:
                continue                      # corner offsets are perpendicular to the normal
            out.append(c + v * (PI / 2))
    return out


def surface_curve_tangent(p, plane_normal):
    """Tangent of the planar geodesic through p cut by a mirror plane."""
    n = unit_normal(p)
    t = np.cross(n, plane_normal)
    return t / np.linalg.norm(t)


def triangle_angles():
    """Interior angles of the *246 triangle, measured from the real edge tangents."""
    # A: edge AB runs along the straight line {y=pi/2, x+z=pi}; edge AC lies in the
    # mirror y = z.  B: edges BA (same straight line) and BC (mirror x = 0).
    # C: edges CA (mirror y = z) and CB (mirror x = 0).
    line_dir = np.array([1.0, 0.0, -1.0]) / math.sqrt(2.0)

    def toward(p, q, plane_normal):
        t = surface_curve_tangent(p, plane_normal)
        return t if np.dot(t, q - p) > 0 else -t

    ab_at_a = line_dir if np.dot(line_dir, CORNER_B - CORNER_A) > 0 else -line_dir
    ac_at_a = toward(CORNER_A, CORNER_C, np.array([0.0, 1.0, -1.0]))
    angle_a = math.degrees(math.acos(np.clip(np.dot(ab_at_a, ac_at_a), -1, 1)))

    ba_at_b = line_dir if np.dot(line_dir, CORNER_A - CORNER_B) > 0 else -line_dir
    bc_at_b = toward(CORNER_B, CORNER_C, np.array([1.0, 0.0, 0.0]))
    angle_b = math.degrees(math.acos(np.clip(np.dot(ba_at_b, bc_at_b), -1, 1)))

    ca_at_c = toward(CORNER_C, CORNER_A, np.array([0.0, 1.0, -1.0]))
    cb_at_c = toward(CORNER_C, CORNER_B, np.array([1.0, 0.0, 0.0]))
    angle_c = math.degrees(math.acos(np.clip(np.dot(ca_at_c, cb_at_c), -1, 1)))

    return angle_a, angle_b, angle_c


_PATCH_CACHE = {}


def sample_patch(resolution=192, band=0.035):
    """A weighted point cloud of the tile patch, by the CO-AREA formula.

    Sample the cube on a regular grid, keep the narrow band |f| < band, and project
    each survivor onto the surface.  A band point's projection density is
    proportional to the band thickness 2*band/|grad f|, so weighting each point by
    |grad f| makes the cloud UNIFORM IN SURFACE AREA - and summing those weights
    times the grid cell volume over 2*band gives the patch area directly.

    (An earlier version lifted a tangent-plane grid along the flat point's normal
    and measured 3.2275 a^2 per cubic cell against the literature's 2.3451: the
    patch is not a graph over its own tangent plane out at the corners, where the
    surface normal turns a full 54.7 degrees away from it, and nearest-point
    projection there silently lands on a neighbouring sheet.)"""
    key = (resolution, band)
    if key in _PATCH_CACHE:
        return _PATCH_CACHE[key]

    axis = (np.arange(resolution) + 0.5) * (PI / resolution)
    gx, gy, gz = np.meshgrid(axis, axis, axis, indexing="ij")
    pts = np.stack([gx.ravel(), gy.ravel(), gz.ravel()], axis=1)
    v = f(pts)
    keep = np.abs(v) < band
    pts = pts[keep]
    g = grad(pts)
    weight = np.linalg.norm(g, axis=1)
    pts = project(pts)
    inside = np.all((pts > 0.0) & (pts < PI), axis=1)
    pts, weight = pts[inside], weight[inside]
    cell = (PI / resolution) ** 3
    weight = weight * (cell / (2.0 * band))          # weight now carries area
    _PATCH_CACHE[key] = (pts, weight)
    return pts, weight


def patch_area():
    _, weight = sample_patch()
    return float(np.sum(weight))


def fundamental_cloud(stride=3):
    """The patch cloud restricted to the *246 triangle.  Strided: the relaxation
    below places at most 7 points, so a few thousand area samples resolve a
    centroid far past the precision the emit is printed at."""
    pts, weight = sample_patch()
    m = (pts[:, 0] <= pts[:, 1]) & (pts[:, 1] <= pts[:, 2]) & (pts.sum(axis=1) <= 1.5 * PI)
    return pts[m][::stride], weight[m][::stride]


def nearest_index(points, targets):
    """argmin over `targets` for each of `points`, by the |a|^2 + |b|^2 - 2 a.b
    expansion (one matmul, no N x M x 3 intermediate)."""
    d2 = (np.sum(points * points, axis=1)[:, None]
          - 2.0 * (points @ targets.T)
          + np.sum(targets * targets, axis=1)[None, :])
    return np.argmin(d2, axis=1)


def verify(fail: Failures):
    print("\nTHE TILE - combinatorics (exact, proven not fitted)")

    # --- one flat point per half-period cube, on the surface, normal on a diagonal
    fps = flat_points_in_cell()
    fail.check(len(fps) == 8, "8 flat points per cubic (2pi) unit cell", f"n={len(fps)}")
    fail.check(max(abs(f(p)) for p in fps) < 1e-12, "every flat point lies on f = 0")
    diagonal = all(
        np.allclose(np.abs(unit_normal(p)), np.array([1, 1, 1]) / math.sqrt(3), atol=1e-12)
        for p in fps)
    fail.check(diagonal, "every flat point's normal runs along a body diagonal")

    # --- the corners
    corners = tile_corners()
    fail.check(len(corners) == 6, "6 corners per tile", f"n={len(corners)}")
    fail.check(max(abs(f(np.array(p))) for p in corners) < 1e-12, "every corner lies on f = 0")
    n0 = np.array([1.0, 1.0, 1.0]) / math.sqrt(3.0)
    offs = [np.array(p) - CORNER_A for p in corners]
    fail.check(max(abs(float(o @ n0)) for o in offs) < 1e-12,
               "all 6 corners lie exactly in the flat point's tangent plane")
    radii = [float(np.linalg.norm(o)) for o in offs]
    fail.check(max(radii) - min(radii) < 1e-12,
               "the 6 corners are equidistant from the centre",
               f"r={radii[0]:.6f} (= pi/sqrt2 = {PI/math.sqrt(2):.6f})")
    # the 6 offsets, sorted by angle, must step by exactly 60 degrees
    e1 = offs[0] / np.linalg.norm(offs[0])
    e2 = np.cross(n0, e1)
    angs = sorted(math.degrees(math.atan2(float(o @ e2), float(o @ e1))) % 360 for o in offs)
    steps = [(angs[(i + 1) % 6] - angs[i]) % 360 for i in range(6)]
    fail.check(max(abs(s - 60.0) for s in steps) < 1e-9,
               "the 6 corners form a REGULAR hexagon in the tangent plane")
    # corners are 4-fold points: their normal runs along a coordinate axis
    axis_aligned = all(
        sorted(np.abs(unit_normal(np.array(p))))[1] < 1e-12 for p in corners)
    fail.check(axis_aligned, "every corner is a 4-fold point (normal on a coordinate axis)")

    # --- corners sit on cube edges, one per 'straddling' edge
    on_edges = 0
    for p in corners:
        fixed = sum(1 for a in range(3) if min(abs(p[a]), abs(p[a] - PI)) < 1e-12)
        if fixed == 2:
            on_edges += 1
    fail.check(on_edges == 6, "all 6 corners lie on cube EDGES", f"n={on_edges}")

    # --- the *246 signature
    a, b, c = triangle_angles()
    fail.check(abs(a - 30) < 1e-6 and abs(b - 45) < 1e-6 and abs(c - 90) < 1e-6,
               "fundamental triangle angles are (30, 45, 90) - the *246 signature",
               f"({a:.9f}, {b:.9f}, {c:.9f})")
    for name, p in (("A", CORNER_A), ("B", CORNER_B), ("C", CORNER_C)):
        fail.check(abs(f(p)) < 1e-12, f"triangle corner {name} lies on f = 0")
    # the A-B edge is a straight line lying in the surface
    ts = np.linspace(0, 1, 64)[:, None]
    line = CORNER_A + ts * (CORNER_B - CORNER_A)
    fail.check(float(np.max(np.abs(f(line)))) < 1e-12,
               "edge A-B is a STRAIGHT LINE contained in the surface")

    print("\nTHE TILE - the symmetry groups")
    # --- 12 site ops preserve the surface and the cube
    probe = project(np.random.default_rng(7).uniform(0.15, PI - 0.15, size=(400, 3)))
    probe = probe[np.all((probe > 0) & (probe < PI), axis=1)]
    ok_ops = True
    for op in SITE_OPS:
        q = apply_site_op(op, probe)
        ok_ops &= float(np.max(np.abs(f(q)))) < 1e-9
        ok_ops &= bool(np.all((q > -1e-9) & (q < PI + 1e-9)))
    fail.check(ok_ops, "all 12 site ops preserve BOTH f = 0 and the cube (-3m)")
    seen = {tuple(np.round(apply_site_op(op, CORNER_A + np.array([0.11, 0.03, -0.07])), 9))
            for op in SITE_OPS}
    fail.check(len(seen) == 12, "the 12 site ops are distinct", f"n={len(seen)}")

    # --- T_ijk preserves the surface and lands on the right tile
    ok_t = True
    for ijk in itertools.product(range(-2, 3), repeat=3):
        q = tile_transform(ijk, probe)
        ok_t &= float(np.max(np.abs(f(q)))) < 1e-9
        lo = np.array([PI * i for i in ijk])
        ok_t &= bool(np.all((q > lo - 1e-9) & (q < lo + PI + 1e-9)))
        ok_t &= bool(np.allclose(tile_transform(ijk, CORNER_A), tile_center(ijk), atol=1e-12))
    fail.check(ok_t, "T_ijk preserves f = 0 and maps the canonical tile onto tile (i,j,k)")
    ok_n = all(
        np.allclose(np.abs(unit_normal(tile_center(ijk))) * np.sign(unit_normal(tile_center(ijk))),
                    unit_normal(tile_center(ijk)))
        and np.allclose(unit_normal(tile_center(ijk)) * math.sqrt(3) * -1, tile_normal_dir(ijk))
        for ijk in itertools.product(range(-2, 3), repeat=3))
    fail.check(ok_n, "tile normal direction is ((-1)^i, (-1)^j, (-1)^k)")

    print("\nTHE TILE - adjacency and topology")
    # --- edge <-> neighbour: reflecting the centre in a cube face gives the neighbour centre
    ok_edge = True
    for axis in range(3):
        for side, delta in ((0.0, -1), (PI, +1)):
            mirrored = CORNER_A.copy()
            mirrored[axis] = 2 * side - mirrored[axis]
            expect = tile_center([delta if a == axis else 0 for a in range(3)])
            ok_edge &= bool(np.allclose(mirrored, expect, atol=1e-12))
    fail.check(ok_edge, "each of the 6 cube faces reflects the centre onto a face-adjacent tile")
    # two corners per face -> the face's edge has 2 endpoints
    per_face = []
    for axis in range(3):
        for side in (0.0, PI):
            per_face.append(sum(1 for p in corners if abs(p[axis] - side) < 1e-12))
    fail.check(all(v == 2 for v in per_face),
               "each cube face carries exactly 2 corners (one hexagon edge)", f"{per_face}")

    # --- {6,4} counts and Euler characteristic per cubic cell
    faces = 8                                      # one hexagon per half-period cube
    edges = faces * 6 // 2                         # each hexagon edge shared by 2 tiles
    verts = faces * 6 // 4                         # each corner shared by 4 tiles
    chi = verts - edges + faces
    fail.check((faces, edges, verts) == (8, 24, 12),
               "{6,4} per cubic cell: F=8, E=24, V=12", f"F={faces} E={edges} V={verts}")
    fail.check(chi == -4, "Euler characteristic per cubic cell = -4 (genus 3)", f"chi={chi}")
    # the 12 vertices really are 12 distinct 4-fold points in the cell
    all_corners = set()
    for ijk in itertools.product(range(2), repeat=3):
        for p in tile_corners(ijk):
            all_corners.add(tuple(np.round(np.mod(p, TAU), 9)))
    fail.check(len(all_corners) == 12,
               "the 8 tiles of a cell share exactly 12 distinct corners", f"n={len(all_corners)}")

    # --- area: 12 triangles per hexagon, 8 hexagons per cell
    area = patch_area()
    cell_area = area * 8 / (TAU ** 2)               # in units of a^2
    fail.check(abs(cell_area - 2.3451) < 0.01,
               "surface area per cubic cell matches the literature 2.3451 a^2",
               f"measured {cell_area:.4f} a^2")
    return area


# ----------------------------------------------------------------------------
# The prism layout: symmetric relaxation inside the *246 triangle
# ----------------------------------------------------------------------------




def expand(points, ops):
    """All images of `points` under `ops`, as one array."""
    points = np.atleast_2d(points)
    return np.concatenate([points @ a.T + b for _, a, b in ops], axis=0)


TANGENT_N = np.array([1.0, 1.0, 1.0]) / math.sqrt(3.0)
TANGENT_E1 = np.array([1.0, -1.0, 0.0]) / math.sqrt(2.0)
TANGENT_E2 = np.cross(TANGENT_N, TANGENT_E1)
HEX_CIRCUMRADIUS = PI / math.sqrt(2.0)          # measured above: the corner radius


def lift(uv):
    """Lift a tangent-plane offset onto the patch ALONG THE FLAT POINT'S NORMAL.

    A 1-D Newton in s, started at s = 0, so it cannot wander onto a neighbouring
    sheet the way a nearest-point projection does out near the corners."""
    uv = np.atleast_2d(uv)
    base = CORNER_A + uv[:, :1] * TANGENT_E1 + uv[:, 1:2] * TANGENT_E2
    s = np.zeros(len(base))
    for _ in range(48):
        p = base + s[:, None] * TANGENT_N
        v = f(p)
        d = grad(p) @ TANGENT_N
        d = np.where(np.abs(d) < 1e-9, 1e-9, d)
        s = s - v / d
        if np.max(np.abs(v)) < 1e-14:
            break
    return base + s[:, None] * TANGENT_N


def seed_lattice(rings):
    """Seed sites from a triangular lattice in the tangent plane, aligned to the
    tile's 6-fold axis and lifted onto the patch.

    A triangular lattice on a 6-fold axis is exactly invariant under the flat
    point's site group, so it hands the relaxation the natural SHELL structure -
    centre, 6, 6, 12, 6, ... - that a fixed 12-orbit ansatz cannot express.  It is
    only a seed: the lift stretches spacing by 1/(n . N), up to sqrt(3) out at the
    corners where the surface normal has turned the full 54.7 degrees away."""
    a = HEX_CIRCUMRADIUS / rings
    cand = []
    span = rings + 2
    for i in range(-span, span + 1):
        for j in range(-span, span + 1):
            u = a * (i + 0.5 * j)
            v = a * (math.sqrt(3.0) / 2.0) * j
            cand.append((u, v))
    pts = lift(np.array(cand))
    margin = 0.18 * a
    inside = np.all((pts > margin) & (pts < PI - margin), axis=1)
    return pts[inside]


def symmetrize(sites):
    """Force exact site-group symmetry: match each site's image under each of the
    12 ops to the nearest site, average the pull-backs, and regenerate the orbit
    from that mean.  Orbit SIZE is discovered, not imposed - a representative on a
    mirror line yields 6 coincident matches and stays a 6-orbit."""
    n = len(sites)
    assigned = np.zeros(n, dtype=bool)
    out = np.array(sites)
    for i in range(n):
        if assigned[i]:
            continue
        pulls, members = [], []
        for op in SITE_OPS:
            j = int(np.argmin(np.linalg.norm(sites - apply_site_op(op, sites[i]), axis=1)))
            members.append((op, j))
            # pull site j back into i's frame through op^-1
            perm, invert = op
            q = PI - sites[j] if invert else sites[j].copy()
            back = np.empty(3)
            for row, src in enumerate(perm):
                back[src] = q[row]
            pulls.append(back)
        rep = project(np.mean(pulls, axis=0)[None, :])[0]
        for op, j in members:
            out[j] = apply_site_op(op, rep)
            assigned[j] = True
    return out


def relax_sites(rings, iterations=60):
    """Centroidal Voronoi relaxation of the whole tile, re-symmetrized every step.

    Each site competes with every image of every site in the 26 surrounding tiles -
    which includes its own mirror image across each tile seam.  Uniform spacing
    inside a tile and uniform spacing ACROSS a tile boundary therefore fall out of
    one computation, with nothing tuned for the seam.

    (Force repulsion over 12-orbit representatives was tried first: it left the
    flat point bare - every orbit settled into an outer band and the centre's
    nearest neighbour came out 2.5x the typical spacing.  A CVT has no such basin,
    because every site takes the centroid of the area it actually owns.)"""
    tile_ops = [(ijk, np.diag(axis_signs(ijk)),
                 np.array([PI * i if i % 2 == 0 else PI * (i + 1) for i in ijk]))
                for ijk in itertools.product((-1, 0, 1), repeat=3)]
    cloud, weight = sample_patch()
    cloud, weight = cloud[::7], weight[::7]

    sites = symmetrize(seed_lattice(rings))
    inv = [(a.T, b) for _, a, b in tile_ops]

    for _ in range(iterations):
        images = np.concatenate([sites @ a.T + b for _, a, b in tile_ops], axis=0)
        owner_site = np.tile(np.arange(len(sites)), len(tile_ops))
        owner_op = np.repeat(np.arange(len(tile_ops)), len(sites))
        nearest = np.empty(len(cloud), dtype=int)
        for lo in range(0, len(cloud), 4096):    # chunked: the full matrix is large
            hi = min(lo + 4096, len(cloud))
            nearest[lo:hi] = nearest_index(cloud[lo:hi], images)
        moved = np.array(sites)
        for s in range(len(sites)):
            m = owner_site[nearest] == s
            if not np.any(m):
                continue
            pulled = np.empty((int(m.sum()), 3))
            ops_used, pts_used = owner_op[nearest[m]], cloud[m]
            for o in np.unique(ops_used):
                sel = ops_used == o
                at, b = inv[o]
                pulled[sel] = (pts_used[sel] - b) @ at.T
            w = weight[m][:, None]
            moved[s] = np.sum(pulled * w, axis=0) / np.sum(w)
        moved = symmetrize(project(moved))
        delta = float(np.max(np.linalg.norm(moved - sites, axis=1)))
        sites = moved
        if delta < 1e-7:
            break
    return sites


def tile_sites(rings):
    """The canonical tile's sites, ordered outward from the flat point and, within
    a shell, by angle - so growth reads as a patch spreading rather than a tendril."""
    sites = relax_sites(rings)
    order = sorted(range(len(sites)), key=lambda i: (
        round(float(np.linalg.norm(sites[i] - CORNER_A)), 4),
        math.atan2(*_tangent_coords(sites[i]))))
    return sites[order], len(sites)


def _tangent_coords(p):
    n = np.array([1.0, 1.0, 1.0]) / math.sqrt(3.0)
    e1 = np.array([1.0, -1.0, 0.0]) / math.sqrt(2.0)
    e2 = np.cross(n, e1)
    d = p - CORNER_A
    return float(d @ e2), float(d @ e1)


def site_tangents(sites):
    """Per-site 'up' for the prism frame: the radial direction away from the tile
    centre, projected into the surface's tangent plane.  Deterministic, exactly
    symmetric under the site group, and it transforms as a plain vector under
    T_ijk - so no baked quaternion can be chirality-corrupted the way the gyroid's
    seed rotations were (Docs/ECOSYSTEM.md 32.7)."""
    out = []
    for p in sites:
        n = unit_normal(p)
        radial = p - CORNER_A
        t = radial - float(radial @ n) * n
        if np.linalg.norm(t) < 1e-6:
            t = np.array([1.0, -1.0, 0.0]) / math.sqrt(2.0)
            t = t - float(t @ n) * n
        out.append(t / np.linalg.norm(t))
    return np.array(out)


def tile_only_ops():
    """The 27 tile placements.  The site list is ALREADY closed under the 12 site
    ops, so composing them in again would just duplicate every point twelve times
    and inflate every adjacency count by the same factor."""
    return [(ijk, np.diag(axis_signs(ijk)),
             np.array([PI * i if i % 2 == 0 else PI * (i + 1) for i in ijk]))
            for ijk in itertools.product((-1, 0, 1), repeat=3)]


def measure_layout(sites, fail: Failures, level):
    ops = tile_only_ops()
    images = expand(sites, ops)
    stats = []
    for p in sites:
        r = np.linalg.norm(images - p, axis=1)
        r = r[r > 1e-7]
        stats.append(float(np.min(r)))
    stats = np.array(stats)

    # seam spacing: distance from each site to its images in the 6 face-adjacent
    # tiles only - this is what a naive 'own tile only' layout gets wrong
    faces = {(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)}
    seam_images = expand(sites, [o for o in ops if o[0] in faces])
    seam = np.array([float(np.min(np.linalg.norm(seam_images - p, axis=1))) for p in sites])

    inside = np.all((sites > 1e-9) & (sites < PI - 1e-9), axis=1)
    on_surface = float(np.max(np.abs(f(sites))))

    # every site's orbit under the site group must land on other sites of the SAME
    # tile - that is what makes the tile a closed unit the group can carry anywhere
    closed = max(
        float(np.min(np.linalg.norm(sites - apply_site_op(op, p), axis=1)))
        for p in sites for op in SITE_OPS)

    fail.check(bool(np.all(inside)), f"L{level}: every site is strictly inside its own cube")
    fail.check(on_surface < 1e-12, f"L{level}: every site lies on f = 0", f"max|f|={on_surface:.2e}")
    fail.check(closed < 1e-6, f"L{level}: the site set is closed under the 12 site ops",
               f"max drift {closed:.2e}")
    ratio = float(stats.max() / stats.min())
    fail.check(ratio < 1.60, f"L{level}: spacing is near-uniform",
               f"min {stats.min():.4f} mean {stats.mean():.4f} max {stats.max():.4f} (max/min {ratio:.2f})")
    seam_ratio = float(seam.min() / stats.min())
    fail.check(seam_ratio > 0.92,
               f"L{level}: cross-seam spacing matches intra-tile spacing",
               f"seam min {seam.min():.4f} vs overall min {stats.min():.4f} ({seam_ratio:.2f}x)")
    return stats


def bonds(sites, factor=1.45):
    """Growth adjacency: for each site, the (siteIndex, tileDelta) of every site
    within `factor` x its own nearest-neighbour distance.  Derived from the
    measured positions, so the table cannot drift away from the geometry."""
    ops = tile_only_ops()
    images = np.concatenate([sites @ a.T + b for _, a, b in ops], axis=0)
    idx_of = np.tile(np.arange(len(sites)), len(ops))
    tile_of = np.repeat(np.arange(len(ops)), len(sites))
    deltas = [o[0] for o in ops]

    out = []
    for i, p in enumerate(sites):
        d = np.linalg.norm(images - p, axis=1)
        live = d > 1e-7
        if not np.any(live):
            out.append([])
            continue
        limit = float(d[live].min()) * factor
        sel = np.where(live & (d <= limit))[0]
        sel = sel[np.argsort(d[sel])]
        picked, seen = [], set()
        for s in sel:
            key = (int(idx_of[s]), deltas[int(tile_of[s])])
            if key in seen:
                continue
            seen.add(key)
            picked.append(key)
        out.append(picked)
    return out


# ----------------------------------------------------------------------------
# Emit
# ----------------------------------------------------------------------------

# Ring counts to emit. Ring 1 would be the flat point alone, which is now the CRYSTAL
# site and not a growth site, so it would emit an empty level.
LEVELS = (2, 3, 4, 5, 6)


def drop_center(sites):
    """Strip the flat point from a relaxed layout.

    The tile's centre is where the plant's CRYSTAL sits - one heart per tile, never
    growing (Docs/ECOSYSTEM.md 34.6) - so it is not a prism site. It is dropped AFTER
    the relaxation rather than excluded from it, deliberately: with the centre
    participating, the innermost shell settles at the distance a real neighbour would
    hold it, which leaves a correctly-sized hole for the crystal instead of a shell
    collapsed into the middle.

    Closure under the site group survives the drop, because the flat point is a FIXED
    POINT of all twelve ops - removing it removes a whole orbit, never part of one."""
    centre = min(range(len(sites)), key=lambda i: float(np.linalg.norm(sites[i] - CORNER_A)))
    if float(np.linalg.norm(sites[centre] - CORNER_A)) > 1e-9:
        raise RuntimeError("expected a site exactly on the flat point")
    return np.delete(sites, centre, axis=0)


def v3(p):
    return f"new Vector3({p[0]:.7f}f, {p[1]:.7f}f, {p[2]:.7f}f)"


def emit(levels_data, area):
    L = []
    a = L.append
    a("        // ---- MEASURED by Tools/Build/measure_schwarz_p_tile.py. Do not hand-edit,")
    a("        // and never derive one level from another by a symmetry ansatz - that is the")
    a("        // mistake the gyroid's seed rotations paid five playtests for")
    a("        // (Docs/ECOSYSTEM.md 32.7). Regenerate with --write instead.")
    a(f"        /// <summary>Area of one tile patch in parameter space (radians squared).</summary>")
    a(f"        public const float TilePatchParamArea = {area:.6f}f;")
    a("")
    a("        static readonly TileLevel[] LevelTable =")
    a("        {")
    for level, (sites, tangents, stats) in sorted(levels_data.items()):
        a(f"            // level {level}: {len(sites)} sites, mean spacing "
          f"{stats.mean():.4f} rad ({stats.min():.4f}..{stats.max():.4f})")
        a(f"            new TileLevel({stats.mean():.6f}f, new[]")
        a("            {")
        for p, t in zip(sites, tangents):
            a(f"                new TileSite({v3(p - CORNER_A)}, {v3(t)}),")
        a("            }),")
    a("        };")
    return "\n".join(L)


CS_BEGIN = "        // <<< MEASURED TABLE BEGIN"
CS_END = "        // <<< MEASURED TABLE END"


def write_cs(block):
    path = pathlib.Path(__file__).resolve().parents[2] / (
        "Assets/_Scripts/Controller/Assemblers/SchwarzPTileData.cs")
    if not path.exists():
        print(f"\n  (no {path.name} yet - printing the block only)")
        return False
    text = path.read_text()
    if CS_BEGIN not in text or CS_END not in text:
        print(f"\n  (markers missing in {path.name} - printing the block only)")
        return False
    head = text[: text.index(CS_BEGIN) + len(CS_BEGIN)]
    tail = text[text.index(CS_END):]
    path.write_text(head + "\n" + block + "\n" + tail)
    print(f"\n  wrote {path}")
    return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="verify only, no emit")
    ap.add_argument("--write", action="store_true", help="rewrite the C# measured block")
    args = ap.parse_args()

    fail = Failures()
    print("=" * 78)
    print("Schwarz P - the non-Euclidean tile")
    print("=" * 78)
    area = verify(fail)

    print(f"\nTHE PRISM LAYOUT - hexagonal shells, seam-aware CVT")
    print(f"  tile patch area = {area:.6f} rad^2  "
          f"(x (periodScale/2pi)^2 for world units)")
    # Levels are reported by their C# INDEX (0-based, as SchwarzPTileData.LevelTable is
    # indexed and as ResolveLevel returns), not by the ring count that generates them -
    # two numberings for one thing is how a doc ends up naming the wrong level.
    levels_data = {}
    for rings in LEVELS:
        index = rings - 2
        sites, count = tile_sites(rings)
        sites = drop_center(sites)          # the flat point is the crystal, not a prism
        stats = measure_layout(sites, fail, index)
        tangents = site_tangents(sites)
        levels_data[index] = (sites, tangents, stats)
        b = bonds(sites)
        deg = [len(x) for x in b]
        cross = sum(1 for row in b for _, ijk in row if ijk != (0, 0, 0))
        shells = sorted({round(float(np.linalg.norm(p - CORNER_A)), 3) for p in sites})
        fail.check(min(deg) >= 3, f"L{index}: every site has a growth bond",
                   f"degree {min(deg)}-{max(deg)} (mean {sum(deg)/len(deg):.1f})")
        print(f"         -> {len(sites)} sites in {len(shells)} shells ({rings} rings), "
              f"growth degree {min(deg)}-{max(deg)} "
              f"(mean {sum(deg)/len(deg):.1f}), {cross} cross-tile bonds")

    print("\nWORLD SPACING at the shipped SchwarzPFlora (periodScale 60, separation 6):")
    world = 60.0 / TAU
    best = min(abs(s[2].mean() * world - 6.0) for s in levels_data.values())
    for index, (sites, _, stats) in sorted(levels_data.items()):
        print(f"  level {index}: {len(sites):>3} sites/tile, spacing "
              f"{stats.mean()*world:6.2f} world units"
              + ("   <-- ResolveLevel picks this one"
                 if abs(stats.mean() * world - 6.0) == best else ""))

    print("\n" + "=" * 78)
    if fail:
        print(f"FAILED {len(fail)} check(s):")
        for x in fail:
            print(f"  - {x}")
        return 1
    print("all checks passed")
    print("=" * 78)

    if args.check:
        return 0

    block = emit(levels_data, area)
    if args.write:
        if write_cs(block):
            return 0
    print("\n" + block)
    return 0


if __name__ == "__main__":
    sys.exit(main())
