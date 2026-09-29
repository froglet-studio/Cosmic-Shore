#!/usr/bin/env python3
"""
Raise the resolution of the Sparrow's skyburst missile mesh.

WHY
---
The skyburst projectile swells to 20x its launch size over the first fifth of its
flight (Docs: SPARROW_SKYBURST_BAY.md), which puts a ~33 unit missile on screen.
The extracted model is an EIGHT-SIDED barrel of 312 quads, so at that scale it
reads as a faceted tube - the shading was never the problem (the mesh is already
fully smooth: every control point carries exactly one normal), the silhouette was.

WHAT IT DOES
------------
Catmull-Clark subdivision of `Cube.003`, in place, inside the shipped FBX:

  level 0   312 quads,     314 verts   barrel  8-sided   (as authored)
  level 1  1248 quads,   1,250 verts   barrel 16-sided
  level 2  4992 quads,   4,994 verts   barrel 32-sided   <- shipped

IN PLACE is the point. The mesh keeps its name (`Cube.003`), so it keeps its
Unity fileID; the file keeps its guid; the material layer, the material order,
the UV layout and the FBX unit scale are all preserved. Nothing outside this
file changes - not the projectile prefab, not its material array, not the
import settings, and not the size constants the growth math is written against.

Catmull-Clark converges to a limit surface INSIDE its control mesh - about 20%
thinner radially for an octagonal barrel - so the result is affinely renormalized
back onto the original bounding box. That is not cosmetic: the missile's launch
size (1.659 u long x 0.381 u across at ProjectileScale 10) is measured from these
bounds by SPARROW_SKYBURST_BAY.md and asserted by SparrowRoundGrowthTests. A
smoother missile is wanted; a SMALLER one is not.

The four element blend shapes (Space/Charge/Time/Mass) are DROPPED. They index
control points by position in the original 314, which a subdivided mesh no longer
has in any meaningful way, so keeping them would leave a shape key that tears the
mesh if anything ever drove one. Nothing does: the projectile renders through a
MeshFilter, not a SkinnedMeshRenderer, and the elemental hull morphs are a VESSEL
system (see CLAUDE.md, "Elemental Hull Morphs") that never looked at this asset.

USAGE
-----
    python3 Tools/Build/subdivide_sparrow_missile.py --check      # verify shipped
    python3 Tools/Build/subdivide_sparrow_missile.py --levels 2   # re-derive

Re-deriving needs the ORIGINAL mesh, which the shipped file no longer is. Recover
it from git and point the tool at it:

    git log --oneline -- "Assets/_Models/Sparrow Missile.fbx"
    git show <commit-before>:"Assets/_Models/Sparrow Missile.fbx" > /tmp/orig.fbx
    python3 Tools/Build/subdivide_sparrow_missile.py --input /tmp/orig.fbx

`--check` does not re-derive; it proves the SHIPPED file satisfies every invariant
a correct run produces (poly count, all-quad, closed manifold, material split,
bounds, outward unit normals). That is the check that keeps working once the
original is only in history.
"""

import argparse
import math
import os
import sys
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary as fbx

ASSET = "Assets/_Models/Sparrow Missile.fbx"
MESH_NAME = "Cube.003"

# Measured from the mesh as the artist exported it. Every one of these is a
# checkable consequence, not a preference.
SOURCE_QUADS = 312
SOURCE_VERTS = 314
SOURCE_MATERIAL_SPLIT = {0: 240, 1: 72}
SOURCE_BOUNDS = (                       # (min, max) per axis, FBX units
    (-0.9601097106933594, 0.9452416896820068),
    (-4.038069725036621, 4.257071971893311),
    (-1.0165445804595947, 0.8888082504272461),
)
BOUNDS_TOLERANCE = 0.01                 # 1% - a shared radial factor keeps the
                                        # cross-section circular rather than
                                        # matching x and z bounds independently


# ----------------------------------------------------------------- mesh access

def find_mesh(nodes):
    objects = next(n for n in nodes if n.name == "Objects")
    for g in objects.find("Geometry"):
        if g.props[1][1].split(b"\x00")[0].decode() == MESH_NAME and g.props[2][1] == b"Mesh":
            return objects, g
    raise SystemExit("no Mesh geometry named %r in the file" % MESH_NAME)


def read_mesh(geom):
    """-> (verts, faces, face_corner_uvs, face_materials)"""
    flat = geom.first("Vertices").props[0][1]
    verts = [(flat[i], flat[i + 1], flat[i + 2]) for i in range(0, len(flat), 3)]

    pvi = geom.first("PolygonVertexIndex").props[0][1]
    faces, run = [], []
    for i in pvi:
        run.append(i if i >= 0 else ~i)
        if i < 0:
            faces.append(tuple(run)); run = []
    if run:
        raise SystemExit("PolygonVertexIndex does not end on a polygon boundary")

    uv_layer = geom.first("LayerElementUV")
    uv_pool = uv_layer.first("UV").props[0][1]
    uv_index = uv_layer.first("UVIndex").props[0][1]
    # UVs are read in the CORNER domain, never collapsed to per-vertex: a UV seam
    # is exactly a control point carrying different UVs in different faces, and
    # flattening it would weld the seam shut.
    uvs, at = [], 0
    for face in faces:
        corner = []
        for _ in face:
            j = uv_index[at]; at += 1
            corner.append((uv_pool[2 * j], uv_pool[2 * j + 1]))
        uvs.append(corner)

    mats = list(geom.first("LayerElementMaterial").first("Materials").props[0][1])
    if len(mats) != len(faces):
        raise SystemExit("material layer is not ByPolygon (%d values, %d faces)"
                         % (len(mats), len(faces)))
    return verts, faces, uvs, mats


# ------------------------------------------------------------- Catmull-Clark

def catmull_clark(verts, faces, uvs, mats):
    """One subdivision step on a CLOSED all-quad (or general n-gon) mesh.

    Output vertex order is [updated originals] + [face points] + [edge points],
    which keeps the original vertices at their original indices - the property
    that makes a second level nest cleanly on the first.
    """
    def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
    def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
    def avg(ps):
        out = (0.0, 0.0, 0.0)
        for p in ps:
            out = add(out, p)
        return mul(out, 1.0 / len(ps))

    face_pts = [avg([verts[i] for i in f]) for f in faces]

    edge_faces = defaultdict(list)
    for fi, f in enumerate(faces):
        for k in range(len(f)):
            a, b = f[k], f[(k + 1) % len(f)]
            edge_faces[(min(a, b), max(a, b))].append(fi)

    for e, fs in edge_faces.items():
        if len(fs) != 2:
            raise SystemExit("mesh is not closed: edge %s borders %d faces" % (e, len(fs)))

    edge_pts, edge_index = {}, {}
    for e, fs in sorted(edge_faces.items()):
        a, b = e
        edge_pts[e] = avg([verts[a], verts[b], face_pts[fs[0]], face_pts[fs[1]]])
        edge_index[e] = len(edge_pts) - 1

    vert_faces = defaultdict(list)
    for fi, f in enumerate(faces):
        for i in f:
            vert_faces[i].append(fi)
    vert_edges = defaultdict(list)
    for e in edge_faces:
        vert_edges[e[0]].append(e); vert_edges[e[1]].append(e)

    updated = []
    for i, v in enumerate(verts):
        n = len(vert_faces[i])
        F = avg([face_pts[fi] for fi in vert_faces[i]])
        R = avg([avg([verts[e[0]], verts[e[1]]]) for e in vert_edges[i]])
        updated.append(mul(add(add(F, mul(R, 2.0)), mul(v, n - 3.0)), 1.0 / n))

    base_face = len(updated)
    base_edge = base_face + len(faces)
    new_verts = updated + face_pts + [edge_pts[e] for e in sorted(edge_pts)]

    def uv_add(a, b): return (a[0] + b[0], a[1] + b[1])
    def uv_avg(ps):
        s = (0.0, 0.0)
        for p in ps:
            s = uv_add(s, p)
        return (s[0] / len(ps), s[1] / len(ps))

    new_faces, new_uvs, new_mats = [], [], []
    for fi, f in enumerate(faces):
        n = len(f)
        corner_uv = uvs[fi]
        centre_uv = uv_avg(corner_uv)
        for k in range(n):
            prev, cur, nxt = f[(k - 1) % n], f[k], f[(k + 1) % n]
            e_prev = (min(prev, cur), max(prev, cur))
            e_next = (min(cur, nxt), max(cur, nxt))
            new_faces.append((cur,
                              base_edge + edge_index[e_next],
                              base_face + fi,
                              base_edge + edge_index[e_prev]))
            # UVs subdivide LINEARLY and per-face, so a seam stays a seam and the
            # texture does not creep as the geometry smooths.
            new_uvs.append([corner_uv[k],
                            uv_avg([corner_uv[k], corner_uv[(k + 1) % n]]),
                            centre_uv,
                            uv_avg([corner_uv[(k - 1) % n], corner_uv[k]])])
            new_mats.append(mats[fi])   # a child quad is the same surface as its parent
    return new_verts, new_faces, new_uvs, new_mats


# ------------------------------------------------------------------ geometry

def bounds(verts):
    return tuple((min(v[a] for v in verts), max(v[a] for v in verts)) for a in range(3))


def renormalize(verts, target):
    """Map the mesh's bounding box back onto `target`.

    x and z share ONE factor so the barrel stays circular; matching them
    independently would make the cross-section a very slight ellipse.
    """
    cur = bounds(verts)
    ext = lambda b, a: max(b[a][1] - b[a][0], 1e-12)
    sx, sz = ext(target, 0) / ext(cur, 0), ext(target, 2) / ext(cur, 2)
    if abs(sx - sz) / max(sx, sz) > 0.05:
        raise SystemExit("radial shrink is not symmetric (x %.4f vs z %.4f)" % (sx, sz))
    s = ((sx + sz) / 2.0, ext(target, 1) / ext(cur, 1), (sx + sz) / 2.0)

    out = []
    for v in verts:
        p = []
        for a in range(3):
            centre_cur = (cur[a][0] + cur[a][1]) / 2.0
            centre_tgt = (target[a][0] + target[a][1]) / 2.0
            p.append(centre_tgt + (v[a] - centre_cur) * s[a])
        out.append(tuple(p))
    return out


def smooth_normals(verts, faces):
    """Area-weighted vertex normals - the original mesh is fully smooth (every
    control point carries exactly one normal), so a subdivided one must be too."""
    acc = [[0.0, 0.0, 0.0] for _ in verts]
    for f in faces:
        # Newell's method: correct for a quad that is not perfectly planar, and
        # its magnitude is twice the face area, which is the weighting we want.
        nx = ny = nz = 0.0
        for k in range(len(f)):
            a, b = verts[f[k]], verts[f[(k + 1) % len(f)]]
            nx += (a[1] - b[1]) * (a[2] + b[2])
            ny += (a[2] - b[2]) * (a[0] + b[0])
            nz += (a[0] - b[0]) * (a[1] + b[1])
        for i in f:
            acc[i][0] += nx; acc[i][1] += ny; acc[i][2] += nz
    out = []
    for n in acc:
        m = math.sqrt(n[0] ** 2 + n[1] ** 2 + n[2] ** 2) or 1.0
        out.append((n[0] / m, n[1] / m, n[2] / m))
    return out


def edge_records(faces):
    """FBX `Edges`: one polygon-vertex slot per unique undirected edge."""
    seen, out, slot = set(), [], 0
    for f in faces:
        for k in range(len(f)):
            e = (min(f[k], f[(k + 1) % len(f)]), max(f[k], f[(k + 1) % len(f)]))
            if e not in seen:
                seen.add(e); out.append(slot + k)
        slot += len(f)
    return out


# ------------------------------------------------------------------- writing

def write_mesh(geom, verts, faces, uvs, mats, normals):
    flat = []
    for v in verts:
        flat.extend(v)
    geom.first("Vertices").props[0] = ("d", flat)

    pvi = []
    for f in faces:
        pvi.extend(f[:-1]); pvi.append(~f[-1])
    geom.first("PolygonVertexIndex").props[0] = ("i", pvi)
    geom.first("Edges").props[0] = ("i", edge_records(faces))

    nrm = []
    for n in normals:
        nrm.extend(n)
    layer = geom.first("LayerElementNormal")
    layer.first("Normals").props[0] = ("d", nrm)
    layer.first("NormalsIndex").props[0] = ("i", [i for f in faces for i in f])

    pool, index, seen = [], [], {}
    for face_uv in uvs:
        for uv in face_uv:
            key = (round(uv[0], 9), round(uv[1], 9))
            if key not in seen:
                seen[key] = len(pool) // 2
                pool.extend(uv)
            index.append(seen[key])
    layer = geom.first("LayerElementUV")
    layer.first("UV").props[0] = ("d", pool)
    layer.first("UVIndex").props[0] = ("i", index)

    geom.first("LayerElementMaterial").first("Materials").props[0] = ("i", list(mats))


def drop_blend_shapes(nodes):
    """Remove the Shape geometries, their deformers, and every connection to them."""
    objects = next(n for n in nodes if n.name == "Objects")
    doomed = set()
    for g in list(objects.find("Geometry")):
        if g.props[2][1] == b"Shape":
            doomed.add(g.props[0][1]); objects.children.remove(g)
    for d in list(objects.find("Deformer")):
        if d.props[2][1] in (b"BlendShape", b"BlendShapeChannel"):
            doomed.add(d.props[0][1]); objects.children.remove(d)

    conns = next(n for n in nodes if n.name == "Connections")
    conns.children = [c for c in conns.children
                      if not (c.props[1][1] in doomed or c.props[2][1] in doomed)]

    # Definitions counts are advisory, but a file that disagrees with itself is a
    # file someone will one day debug for an hour.
    defs = next((n for n in nodes if n.name == "Definitions"), None)
    if defs:
        for ot in defs.find("ObjectType"):
            kind = ot.props[0][1]
            if kind in (b"Geometry", b"Deformer"):
                live = len(objects.find(kind.decode()))
                if ot.first("Count"):
                    ot.first("Count").props[0] = ("I", live)
        if defs.first("Count"):
            defs.first("Count").props[0] = ("I", sum(
                len(objects.find(ot.props[0][1].decode())) for ot in defs.find("ObjectType")))
    return len(doomed)


# -------------------------------------------------------------------- checking

def check(path, levels):
    nodes, _, _ = fbx.read(path)
    _, geom = find_mesh(nodes)
    verts, faces, uvs, mats = read_mesh(geom)

    expect_quads = SOURCE_QUADS * 4 ** levels
    problems = []

    if any(len(f) != 4 for f in faces):
        problems.append("mesh is no longer all-quad")
    if len(faces) != expect_quads:
        problems.append("expected %d quads at level %d, found %d"
                        % (expect_quads, levels, len(faces)))

    edges = set()
    for f in faces:
        for k in range(len(f)):
            edges.add((min(f[k], f[(k + 1) % len(f)]), max(f[k], f[(k + 1) % len(f)])))
    euler = len(verts) - len(edges) + len(faces)
    if euler != 2:
        problems.append("not a closed genus-0 surface (V-E+F = %d, want 2)" % euler)

    split = {m: mats.count(m) for m in set(mats)}
    want = {m: c * 4 ** levels for m, c in SOURCE_MATERIAL_SPLIT.items()}
    if split != want:
        problems.append("material split %s, expected %s" % (split, want))

    got = bounds(verts)
    for axis, name in enumerate("xyz"):
        want_ext = SOURCE_BOUNDS[axis][1] - SOURCE_BOUNDS[axis][0]
        got_ext = got[axis][1] - got[axis][0]
        if abs(got_ext - want_ext) / want_ext > BOUNDS_TOLERANCE:
            problems.append("%s extent %.6f, authored %.6f (>%.0f%%)"
                            % (name, got_ext, want_ext, BOUNDS_TOLERANCE * 100))

    normals = geom.first("LayerElementNormal").first("Normals").props[0][1]
    worst = max(abs(1.0 - math.sqrt(normals[i] ** 2 + normals[i + 1] ** 2 + normals[i + 2] ** 2))
                for i in range(0, len(normals), 3))
    if worst > 1e-6:
        problems.append("normals are not unit length (worst error %.2e)" % worst)

    print("%s: %d quads, %d verts, V-E+F=%d, materials %s" %
          (os.path.basename(path), len(faces), len(verts), euler, split))
    print("  bounds  x %.6f  y %.6f  z %.6f" %
          tuple(got[a][1] - got[a][0] for a in range(3)))
    print("  authored x %.6f  y %.6f  z %.6f" %
          tuple(SOURCE_BOUNDS[a][1] - SOURCE_BOUNDS[a][0] for a in range(3)))
    if problems:
        for p in problems:
            print("  FAIL: %s" % p)
        return 1
    print("  OK: subdivided %d level(s), closed, on the authored bounding box" % levels)
    return 0


# ------------------------------------------------------------------------ main

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--input", default=ASSET, help="FBX to subdivide (default: the asset)")
    ap.add_argument("--output", default=ASSET, help="where to write (default: in place)")
    ap.add_argument("--levels", type=int, default=2, help="Catmull-Clark steps (default 2)")
    ap.add_argument("--check", action="store_true",
                    help="verify the shipped file's invariants instead of re-deriving")
    args = ap.parse_args()

    if args.check:
        sys.exit(check(args.input, args.levels))

    nodes, version, footer = fbx.read(args.input)
    _, geom = find_mesh(nodes)
    verts, faces, uvs, mats = read_mesh(geom)

    if len(verts) != SOURCE_VERTS or len(faces) != SOURCE_QUADS:
        raise SystemExit("input is not the authored mesh (%d verts, %d faces) - "
                         "subdividing an already-subdivided file would compound it"
                         % (len(verts), len(faces)))

    target = bounds(verts)
    print("in   %d quads, %d verts" % (len(faces), len(verts)))
    for level in range(args.levels):
        verts, faces, uvs, mats = catmull_clark(verts, faces, uvs, mats)
        print("  level %d -> %d quads, %d verts" % (level + 1, len(faces), len(verts)))

    shrunk = bounds(verts)
    print("  limit-surface shrink: x %.1f%%  y %.1f%%  z %.1f%%" % tuple(
        100 * (1 - (shrunk[a][1] - shrunk[a][0]) / (target[a][1] - target[a][0]))
        for a in range(3)))
    verts = renormalize(verts, target)

    dropped = drop_blend_shapes(nodes)
    write_mesh(geom, verts, faces, uvs, mats, smooth_normals(verts, faces))
    fbx.write(args.output, nodes, version, footer)
    print("out  %s (%d blend-shape objects dropped)" % (args.output, dropped))
    sys.exit(check(args.output, args.levels))


if __name__ == "__main__":
    main()
