#!/usr/bin/env python3
"""Generate the space crystal's prism-dart model from the flat-kite original.

    python3 Tools/Models/build_space_crystal_prism_darts.py [--force]

Source  : Assets/_Models/spacecrystalanim.fbx
Output  : Assets/_Models/SpaceCrystalPrismDartsAnim_8-12-26.fbx (+ .meta)

WHY A GENERATOR AND NOT A HAND-AUTHORED FBX
-------------------------------------------
The source is a deltoidal hexecontahedron: 60 kite ("dart") faces, each exported as
an INDEPENDENT 4-vertex island, plus three blend shapes that shuffle those 60 darts
into a second closed solid and back. Measured from the source, every animated shape
moves every dart as an EXACT rigid rotation about an axis through the origin —
uniformly 72 deg for '5pin' and 120 deg for 'pin' (residual < 1e-4 model units) —
while 'Key 3' is a small in-plane shrink with zero rotation.

That rigidity is what makes this mechanical: each flat kite is replaced by a small
faceted PRISM built in its face's own frame (crown cap, four crown bevels, four side
walls, back cap => 10 faces and 12 vertices per dart, 600 faces / 720 vertices total),
and each blend shape is rebuilt by constructing the same prism in THAT POSE's frame
and taking the difference. The thickness therefore rotates with its dart instead of
being frozen in the rest orientation, so the solid darts stay correctly oriented all
the way through the shuffle.

WHY THE ANIMATED SHAPES CARRY IN-BETWEEN FRAMES
-----------------------------------------------
Unity interpolates a single-frame blend shape LINEARLY in vertex space, so between
rest and a rotated pose every vertex cuts the straight chord. At the midpoint of a
120 deg rotation the chord passes through HALF the radius — the darts dive toward
the centre and the gaps between them yawn open, reading as the crystal blowing
apart instead of darts sliding around on the sphere. The fix is data, not code:
each animated channel is authored as a PROGRESSIVE morph (FBX in-between targets,
which Unity imports as blend shape frames) sampled along each dart's true rotation
arc via Rodrigues at equal angle steps. Between adjacent frames the residual chord
is under 1 percent of the radius, so the darts stay on the sphere at constant
spread for the whole sweep. SetBlendShapeWeight(i, 0..100) walks the frames
automatically — SpaceCrystalAnimator needs no change.

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


# ---------------------------------------------------------------- rigid motion
def dart_frame(pts):
    """Orthonormal frame (u, v, n) + centroid of a kite, built from corner 0."""
    c, n = face_frame(pts)
    u = sub(pts[0], c)
    u = normalize(sub(u, mul(n, dot(u, n))))
    v = cross(n, u)
    return c, (u, v, n)


def rotation_between(F0, F1):
    """R = F1 * F0^T where the frames' vectors are columns."""
    return [[sum(F1[k][i] * F0[k][j] for k in range(3)) for j in range(3)]
            for i in range(3)]


def rot_apply(R, v):
    return tuple(sum(R[i][j] * v[j] for j in range(3)) for i in range(3))


def axis_angle(R):
    tr = R[0][0] + R[1][1] + R[2][2]
    ang = math.acos(max(-1.0, min(1.0, (tr - 1.0) / 2.0)))
    ax = normalize((R[2][1] - R[1][2], R[0][2] - R[2][0], R[1][0] - R[0][1]))
    return ax, ang


def rodrigues(axis, ang, v):
    c, s = math.cos(ang), math.sin(ang)
    return add(add(mul(v, c), mul(cross(axis, v), s)),
               mul(axis, dot(axis, v) * (1.0 - c)))


def dart_rotations(verts, polys, deltas):
    """Per dart: (axis, angle) of the pure origin rotation this shape applies to it.

    Asserts the motion really is that rotation (both the corner residual and the
    'axis passes through the origin' condition R*c0 == c1), so a future re-export
    that breaks the assumption fails loudly instead of producing bent arcs.
    """
    dv = [add(v, deltas.get(i, (0.0, 0.0, 0.0))) for i, v in enumerate(verts)]
    rots = []
    for p in polys:
        P = [verts[i] for i in p]
        Q = [dv[i] for i in p]
        c0, F0 = dart_frame(P)
        c1, F1 = dart_frame(Q)
        R = rotation_between(F0, F1)
        for pi, qi in zip(P, Q):
            r = sub(add(c1, rot_apply(R, sub(pi, c0))), qi)
            assert math.sqrt(dot(r, r)) < 1e-2, 'dart motion is not rigid'
        drift = sub(rot_apply(R, c0), c1)
        assert math.sqrt(dot(drift, drift)) < 1e-2, \
            'dart rotation axis does not pass through the origin'
        axis, ang = axis_angle(R)
        # Guard the axis-angle extraction itself: at ang ~ 180 deg the skew part of R
        # vanishes and the axis degenerates, which would emit garbage arcs while every
        # assert above still passes. Round-tripping through Rodrigues catches it.
        for pi, qi in zip(P, Q):
            r = sub(rodrigues(axis, ang, pi), qi)
            assert math.sqrt(dot(r, r)) < 1e-2, \
                'axis-angle decomposition does not reproduce the dart motion'
        rots.append((axis, ang))
    return rots


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


# How many segments each animated channel's rotation is split into. 7 in-between
# frames + the full pose = 8 segments: 'pin' rotates 120 deg total, so 15 deg per
# segment — the residual linear chord dips the darts under 0.9% of the radius,
# versus 50% with no in-betweens.
IB_SEGMENTS = 8


def inbetween_deltas(prism_rest, rots, t):
    """Deltas from the rest prisms with every dart rigidly rotated t of the way."""
    out = []
    for di, (axis, ang) in enumerate(rots):
        for v in prism_rest[di * 12:(di + 1) * 12]:
            out.append(sub(rodrigues(axis, ang * t, v), v))
    return out


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


def clone_node(n):
    return fbx.Node(n.name,
                    [list(p) if isinstance(p, list) else p for p in n.props],
                    list(n.prop_types),
                    [clone_node(k) for k in n.children])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--force', action='store_true',
                    help='rewrite even if the output already matches')
    args = ap.parse_args()

    ver, root, base, verts, polys, shapes = load_source(SRC)
    check_source(verts, polys, shapes)

    src_names = [s.props[1].split(b'\x00')[0].decode() for s, _ in shapes]
    assert src_names == ['5pin', 'pin', 'Key 3'], src_names

    # The two shuffle shapes rotate every dart rigidly about the origin (72 and 120
    # deg); 'Key 3' is a small in-plane shrink and interpolates fine with one frame.
    animated = {name: dart_rotations(verts, polys, deltas)
                for (node, deltas), name in zip(shapes, src_names)
                if name in ('5pin', 'pin')}

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

    # ---- in-between frames along each dart's true rotation arc (progressive morph)
    objects = root.find(b'Objects')
    conns = root.find(b'Connections')
    used_ids = {c.props[0] for c in objects.children if isinstance(c.props[0], int)}
    next_id = 910000001
    channel_of = {}                      # shape object id -> channel object id
    for cn in conns.children:
        if len(cn.props) == 3 and cn.props[0] == b'OO':
            src, dst = cn.props[1], cn.props[2]
            if any(s.props[0] == src for s, _ in shapes):
                channel_of[src] = dst

    frame_weights = {}                   # channel id -> FullWeights list
    for (node, _), name in zip(shapes, src_names):
        ch_id = channel_of[node.props[0]]
        if name not in animated:
            frame_weights[ch_id] = [100.0]
            continue
        rots = animated[name]
        conn_pos = next(i for i, cn in enumerate(conns.children)
                        if len(cn.props) == 3 and cn.props[0] == b'OO'
                        and cn.props[1] == node.props[0] and cn.props[2] == ch_id)
        obj_pos = objects.children.index(node)
        weights = []
        for k in range(1, IB_SEGMENTS):
            t = k / IB_SEGMENTS
            while next_id in used_ids:
                next_id += 1
            sid = next_id
            used_ids.add(sid)
            ib = clone_node(node)
            ib.props[0] = sid
            ib.props[1] = f'{name} ib{k}'.encode() + b'\x00\x01Geometry'
            deltas = inbetween_deltas(new_verts, rots, t)
            set_array(ib, b'Indexes', list(range(len(new_verts))), b'i')
            set_array(ib, b'Vertices', [c for d in deltas for c in d], b'd')
            set_array(ib, b'Normals', [0.0] * (len(new_verts) * 3), b'd')
            objects.children.insert(obj_pos, ib)      # keep shapes grouped in file
            obj_pos += 1
            cc = clone_node(conns.children[conn_pos])
            cc.props[1] = sid
            conns.children.insert(conn_pos, cc)       # before the full shape => ordered
            conn_pos += 1
            weights.append(100.0 * t)
        weights.append(100.0)
        frame_weights[ch_id] = weights

    channels = [c for c in objects.children
                if c.name == b'Deformer' and c.props[2] == b'BlendShapeChannel']
    assert len(channels) == len(new_shapes)
    for ch in channels:
        set_array(ch, b'FullWeights', frame_weights[ch.props[0]], b'd')

    # Definitions counts are advisory (preallocation hints), but keep them honest so
    # third-party FBX tools don't flag the file: 14 new Geometry objects were added.
    added = 2 * (IB_SEGMENTS - 1)
    defs = root.find(b'Definitions')
    total = defs.find(b'Count')
    total.props[0] += added
    geo_def = next(c for c in defs.children
                   if c.name == b'ObjectType' and c.props[0] == b'Geometry')
    geo_def.find(b'Count').props[0] += added

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
    assert [n for n in names if ' ib' not in n] == ['5pin', 'pin', 'Key 3'], names
    assert len(names) == 3 + 2 * (IB_SEGMENTS - 1), names

    # every channel's shape connections must be ordered to match its FullWeights
    conns2 = r2.find(b'Connections')
    shape_ids = {c.props[0]: c.props[1].split(b'\x00')[0].decode()
                 for c in r2.find(b'Objects').children
                 if c.name == b'Geometry' and c.props[2] == b'Shape'}
    chans2 = {c.props[0]: c for c in r2.find(b'Objects').children
              if c.name == b'Deformer' and c.props[2] == b'BlendShapeChannel'}
    per_chan = {}
    for cn in conns2.children:
        if len(cn.props) == 3 and cn.props[0] == b'OO' and cn.props[1] in shape_ids \
                and cn.props[2] in chans2:
            per_chan.setdefault(cn.props[2], []).append(cn.props[1])
    for ch_id, sids in per_chan.items():
        fw = chans2[ch_id].find(b'FullWeights').props[0]
        assert len(fw) == len(sids), 'FullWeights count != connected shape count'
        assert fw == sorted(fw) and fw[-1] == 100.0, f'frame weights not ascending: {fw}'

    meta = DST + '.meta'
    if not os.path.exists(meta) or args.force:
        src_meta = open(SRC + '.meta').read()
        out = src_meta.replace('guid: 39e205c7c7716094991df8c57e3e0753', f'guid: {GUID}', 1)
        assert f'guid: {GUID}' in out
        open(meta, 'w').write(out)

    rs = [math.sqrt(dot(v, v)) for v in new_verts]
    print(f'wrote {os.path.relpath(DST, ROOT)}')
    print(f'  {len(new_verts)} vertices, {len(faces)} faces, {len(new_shapes)} blend shape '
          f'channels ({", ".join(src_names)}); the two shuffle channels carry '
          f'{IB_SEGMENTS - 1} in-between frames each')
    print(f'  radius {min(rs):.2f}..{max(rs):.2f} (source 71.21..74.40)')
    print(f'  mesh fileID preserved: -5993354799466719267   guid: {GUID}')


if __name__ == '__main__':
    main()
