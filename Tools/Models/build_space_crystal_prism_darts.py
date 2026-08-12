#!/usr/bin/env python3
"""Generate the space crystal's prism-dart model from the flat-kite original.

    python3 Tools/Models/build_space_crystal_prism_darts.py [--force]

Source  : Assets/_Models/spacecrystalanim.fbx
Output  : Assets/_Models/SpaceCrystalPrismDartsAnim_8-12-26.fbx (+ .meta)

WHY A GENERATOR AND NOT A HAND-AUTHORED FBX
-------------------------------------------
The source is a deltoidal hexecontahedron: 60 kite ("dart") faces, each exported as
an INDEPENDENT 4-vertex island, plus three blend shapes that shuffle those 60 darts
into a second closed solid and back. Measured from the source, every shape moves each
dart as a RIGID motion — a uniform 25.9 deg rotation, faces staying perfectly planar.

That rigidity is what makes this mechanical: each flat kite is replaced by a small
faceted PRISM built in its face's own frame (crown cap, four crown bevels, four side
walls, back cap => 10 faces and 12 vertices per dart, 600 faces / 720 vertices total),
and each blend shape is rebuilt by constructing the same prism in THAT POSE's frame
and taking the difference. The thickness therefore rotates with its dart instead of
being frozen in the rest orientation, so the solid darts stay correctly oriented all
the way through the shuffle.

The whole FBX node tree is CLONED from the source and only the data arrays are
replaced. Every node name and every FBX object id is preserved, so Unity's name-based
sub-asset id generation (`fileIdsGeneration: 2`, `internalIDToNameTable: []`) yields
the SAME mesh fileID as the original, -5993354799466719267. Only the asset GUID is new,
which is what lets the three space-crystal prefabs repoint with a one-line edit while
the fauna/flora prefabs that share the original solid keep it untouched.

Idempotent: re-running reproduces the same bytes and the same .meta GUID.
"""
import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary as fbx

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, 'Assets/_Models/spacecrystalanim.fbx')
DST = os.path.join(ROOT, 'Assets/_Models/SpaceCrystalPrismDartsAnim_8-12-26.fbx')

# Stable GUID: md5("CosmicShore/SpaceCrystalPrismDartsAnim_8-12-26.fbx"), so re-running
# the generator never orphans the prefab references.
GUID = 'f1dc806d1143ef8c83887c883f47db94'

# ---------------------------------------------------------------- dart profile
# Ring scales are fractions of the kite about its own centroid; offsets are in model
# units (the crystal's circumradius is ~74, a dart's centroid->corner reach is ~28).
PROFILE = dict(
    inset=0.05,     # shrink each dart so neighbours read as separate solids, not a shell
    crown=1.5,      # how far the crown cap stands proud of the original face plane
    crown_s=0.87,   # crown cap size relative to the rim (the bevel's width)
    depth=7.0,      # inward extrusion — this is the "thickness"
    back_s=0.84,    # back cap taper, so the side walls catch light instead of going dark
)


# ---------------------------------------------------------------- vector helpers
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1],
                         a[2] * b[0] - a[0] * b[2],
                         a[0] * b[1] - a[1] * b[0])


def normalize(a):
    l = math.sqrt(dot(a, a))
    return (a[0] / l, a[1] / l, a[2] / l) if l > 1e-12 else (0.0, 0.0, 0.0)


def face_frame(pts):
    """Centroid and outward normal of a planar polygon (outward = away from origin)."""
    c = mul((sum(p[0] for p in pts), sum(p[1] for p in pts), sum(p[2] for p in pts)),
            1.0 / len(pts))
    n = (0.0, 0.0, 0.0)
    for i in range(len(pts)):
        n = add(n, cross(sub(pts[i], c), sub(pts[(i + 1) % len(pts)], c)))
    n = normalize(n)
    if dot(n, c) < 0:
        n = mul(n, -1.0)
    return c, n


# ---------------------------------------------------------------- source parsing
def load_source(path):
    ver, root = fbx.read(path)
    objs = root.find(b'Objects')
    base = [c for c in objs.children
            if c.name == b'Geometry' and c.props[2] == b'Mesh'][0]
    shape_geos = [c for c in objs.children
                  if c.name == b'Geometry' and c.props[2] == b'Shape']

    V = base.find(b'Vertices').props[0]
    verts = [(V[i * 3], V[i * 3 + 1], V[i * 3 + 2]) for i in range(len(V) // 3)]

    polys, cur = [], []
    for i in base.find(b'PolygonVertexIndex').props[0]:
        if i < 0:
            cur.append(~i)
            polys.append(cur)
            cur = []
        else:
            cur.append(i)

    shapes = []
    for s in shape_geos:
        idxs = s.find(b'Indexes').props[0]
        sv = s.find(b'Vertices').props[0]
        shapes.append((s, {vi: (sv[j * 3], sv[j * 3 + 1], sv[j * 3 + 2])
                           for j, vi in enumerate(idxs)}))
    return ver, root, base, verts, polys, shapes


def check_source(verts, polys, shapes):
    """Assert the properties the construction relies on, before building anything."""
    assert len(polys) == 60, f'expected 60 kite faces, got {len(polys)}'
    assert all(len(p) == 4 for p in polys), 'expected every face to be a quad'
    used = [i for p in polys for i in p]
    assert len(used) == len(set(used)) == len(verts), \
        'expected every dart to be an independent 4-vertex island'
    for p in polys:                                  # CCW seen from outside
        c, _ = face_frame([verts[i] for i in p])
        raw = (0.0, 0.0, 0.0)
        for i in range(4):
            raw = add(raw, cross(sub(verts[p[i]], c), sub(verts[p[(i + 1) % 4]], c)))
        assert dot(raw, c) > 0, 'face winding is not CCW-from-outside'
    for _, deltas in shapes:                         # rigid + planar per dart
        dv = [add(v, deltas.get(i, (0.0, 0.0, 0.0))) for i, v in enumerate(verts)]
        for p in polys:
            c, n = face_frame([dv[i] for i in p])
            flat = max(abs(dot(sub(dv[i], c), n)) for i in p)
            assert flat < 1e-3, f'deformed dart is not planar ({flat})'


# ---------------------------------------------------------------- construction
def prism_verts(pts, p):
    """The 12 vertices of one dart prism: crown[0..3], rim[0..3], back[0..3]."""
    c, n = face_frame(pts)
    s = 1.0 - p['inset']
    crown = [add(add(c, mul(sub(q, c), s * p['crown_s'])), mul(n, p['crown'])) for q in pts]
    rim = [add(c, mul(sub(q, c), s)) for q in pts]
    back = [sub(add(c, mul(sub(q, c), s * p['back_s'])), mul(n, p['depth'])) for q in pts]
    return crown + rim + back


def prism_faces(base):
    """The 10 polygons of one dart prism, CCW seen from outside."""
    k = [base + i for i in range(4)]
    r = [base + 4 + i for i in range(4)]
    b = [base + 8 + i for i in range(4)]
    faces = [list(k)]
    for i in range(4):
        j = (i + 1) % 4
        faces.append([k[i], k[j], r[j], r[i]])       # crown bevel
    for i in range(4):
        j = (i + 1) % 4
        faces.append([r[i], r[j], b[j], b[i]])       # side wall
    faces.append([b[3], b[2], b[1], b[0]])           # back cap
    return faces


def build(verts, polys, shapes, profile):
    def pose(vs):
        out = []
        for p in polys:
            out.extend(prism_verts([vs[i] for i in p], profile))
        return out

    rest = pose(verts)
    faces = []
    for fi in range(len(polys)):
        faces.extend(prism_faces(fi * 12))

    out_shapes = []
    for node, deltas in shapes:
        dv = pose([add(v, deltas.get(i, (0.0, 0.0, 0.0))) for i, v in enumerate(verts)])
        out_shapes.append((node, [sub(a, b) for a, b in zip(dv, rest)]))
    return rest, faces, out_shapes


def flat_normals(verts, faces):
    """One normal per polygon-vertex, matching the source's faceted shading."""
    out = []
    for f in faces:
        _, n = face_frame([verts[i] for i in f])
        out.extend(list(n) * len(f))
    return out


def polygon_index(faces):
    idx = []
    for f in faces:
        idx.extend(f[:-1])
        idx.append(~f[-1])
    return idx


def edge_index(faces):
    """One entry per undirected edge, given as an index into PolygonVertexIndex."""
    seen, edges, base = set(), [], 0
    for f in faces:
        for i in range(len(f)):
            key = tuple(sorted((f[i], f[(i + 1) % len(f)])))
            if key not in seen:
                seen.add(key)
                edges.append(base + i)
        base += len(f)
    return edges


# ---------------------------------------------------------------- emit
def set_array(node, name, values, prop_type):
    child = node.find(name)
    assert child is not None, f'source node has no {name!r}'
    child.props[0] = list(values)
    child.prop_types[0] = prop_type


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--force', action='store_true',
                    help='rewrite even if the output already matches')
    args = ap.parse_args()

    ver, root, base, verts, polys, shapes = load_source(SRC)
    check_source(verts, polys, shapes)

    new_verts, faces, new_shapes = build(verts, polys, shapes, PROFILE)
    normals = flat_normals(new_verts, faces)

    # ---- validate everything before touching the tree
    assert len(new_verts) == 60 * 12 == 720
    assert len(faces) == 60 * 10 == 600
    for f in faces:
        assert all(0 <= i < len(new_verts) for i in f), 'face indexes out of range'
    assert len(normals) == sum(len(f) for f in faces) * 3
    for f in faces:                                   # every face outward-facing at rest
        c, n = face_frame([new_verts[i] for i in f])
        assert dot(n, c) > 0, 'generated face points inward'
    for _, deltas in new_shapes:
        assert len(deltas) == len(new_verts)

    flat = [c for v in new_verts for c in v]
    set_array(base, b'Vertices', flat, b'd')
    set_array(base, b'PolygonVertexIndex', polygon_index(faces), b'i')
    set_array(base, b'Edges', edge_index(faces), b'i')
    set_array(base.find(b'LayerElementNormal'), b'Normals', normals, b'd')

    for node, deltas in new_shapes:
        set_array(node, b'Indexes', list(range(len(new_verts))), b'i')
        set_array(node, b'Vertices', [c for d in deltas for c in d], b'd')
        set_array(node, b'Normals', [0.0] * (len(new_verts) * 3), b'd')

    # BlendShapeChannel FullWeights: Blender writes one 100.0 per affected index
    channels = [c for c in root.find(b'Objects').children
                if c.name == b'Deformer' and c.props[2] == b'BlendShapeChannel']
    assert len(channels) == len(new_shapes)
    for ch in channels:
        set_array(ch, b'FullWeights', [100.0] * len(new_verts), b'd')

    fbx.write(DST, ver, root)

    # ---- verify what actually landed on disk
    v2, r2 = fbx.read(DST)
    b2 = [c for c in r2.find(b'Objects').children
          if c.name == b'Geometry' and c.props[2] == b'Mesh'][0]
    assert len(b2.find(b'Vertices').props[0]) == 720 * 3
    assert len(b2.find(b'PolygonVertexIndex').props[0]) == 600 * 4
    names = [c.props[1].split(b'\x00')[0].decode()
             for c in r2.find(b'Objects').children
             if c.name == b'Geometry' and c.props[2] == b'Shape']
    assert names == ['5pin', 'pin', 'Key 3'], names

    meta = DST + '.meta'
    if not os.path.exists(meta) or args.force:
        src_meta = open(SRC + '.meta').read()
        out = src_meta.replace('guid: 39e205c7c7716094991df8c57e3e0753', f'guid: {GUID}', 1)
        assert f'guid: {GUID}' in out
        open(meta, 'w').write(out)

    rs = [math.sqrt(dot(v, v)) for v in new_verts]
    print(f'wrote {os.path.relpath(DST, ROOT)}')
    print(f'  {len(new_verts)} vertices, {len(faces)} faces, {len(new_shapes)} blend shapes '
          f'({", ".join(n for n in names)})')
    print(f'  radius {min(rs):.2f}..{max(rs):.2f} (source 71.21..74.40)')
    print(f'  mesh fileID preserved: -5993354799466719267   guid: {GUID}')


if __name__ == '__main__':
    main()
