#!/usr/bin/env python3
"""
Author the shipped space-crystal mesh from the artist's export, with CORRECT blend-shape normals.

    python3 Tools/Build/author_space_crystal_mesh.py            # rewrite the shipped FBX
    python3 Tools/Build/author_space_crystal_mesh.py --check    # exit 1 if it has drifted
    python3 Tools/Build/author_space_crystal_mesh.py --report   # print the normal-error table

SOURCE  Assets/_Models/SpaceCrystalExport1_7-17-25.fbx   (Blender export: 60 rigid blocks,
        four shape keys - 5PointRotate 1st/2nd half spin, 3PointRotate 1st/2nd half spin)
TARGET  Assets/_Models/spacecrystalanim.fbx              (the mesh every space crystal renders:
        CrystalSpace, ActiveCrystalSpace, SpaceDandruff and the crystals on GyroidFlora,
        TadPoleFauna, MassSharkFauna and MassBrittlestarFauna - 11 renderers, one mesh)

WHY THE NORMALS WERE WRONG
    A spin is two keys STACKED: the 1st half runs 0->100, then the 2nd half runs 0->100 with the
    1st still held at 100, and both snap back to 0 on a pose congruent to the start. Every FBX
    exporter and Unity's "Calculate" mode derive each key's normal delta against the BASE mesh,
    and Unity adds the deltas of all active keys. Rotations do not add, so during the 2nd half
    the summed normal is wrong by up to ~20 deg (5-point) / ~42 deg (3-point) and the reset pops
    by exactly that much. Blender also writes all-zero shape normals, so "Import" mode is worse.
    Two smaller faults ride along: shape normals are per CONTROL POINT in FBX while this mesh is
    hard-edged (three normals per corner), and a single target linearly interpolates normals
    between its endpoints, which misses the true face normal mid-key by up to ~8 deg.

WHAT THIS WRITES
    * Vertex MOTION is the artist's, unchanged: every in-between position lies exactly on the
      straight line of the original key, so piecewise-linear playback is identical to it.
    * The mesh is unwelded (one control point per polygon corner), so a per-control-point shape
      normal IS a per-corner normal and each face of each block carries its own exact delta.
    * The 2nd-half keys' normal deltas are authored RELATIVE TO THE 1st-HALF END POSE, so the
      sum Unity computes (base + full 1st + partial 2nd) is the correct normal, and the end
      normal equals the base normal of the face that now occupies that slot: no pop on reset.
      Consequence: the 2nd-half keys are only correct with their 1st-half key at 100.
      SpaceCrystalAnimator always drives them that way.
    * IN_BETWEENS exact frames per key (FBX in-between targets) cap the mid-key error.
    * Geometry is converted into the target file's axis system and scaled so the crystal's
      outer radius matches the mesh it replaces - every prefab's scale and collider still fit.
    * The target keeps its guid, its Model/Geometry names and its object ids, so Unity's
      name-derived mesh fileID (-5993354799466719267) is unchanged and no prefab is edited.

The target's .meta must import blend-shape normals from the file
(`blendShapeNormalImportMode: 0`); `--check` asserts that too.
"""

import argparse
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary as F  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Assets/_Models/SpaceCrystalExport1_7-17-25.fbx")
TARGET = os.path.join(ROOT, "Assets/_Models/spacecrystalanim.fbx")
TARGET_META = TARGET + ".meta"

# (1st half, 2nd half) per spin. Channel ORDER in the output is this flattened order, which is
# the blend-shape index order SpaceCrystalAnimator drives: 0,1 = 5-point spin, 2,3 = 3-point.
SPINS = [("5PointRotate-1stHalfSpin", "5PointRotate-2ndHalfSpin"),
         ("3PointRotate-1stHalfSpin", "3PointRotate-2ndHalfSpin")]
IN_BETWEENS = 4             # frames per key at 25/50/75/100 - see --report for the error table

# Object ids the target already uses for its mesh geometry and blend-shape deformer. Kept so the
# rewrite touches as little identity as possible; channel/shape ids below are fixed constants so
# a re-run is byte-identical.
MESH_GEOMETRY_ID = 703293738
BLENDSHAPE_DEFORMER_ID = 802377929
CHANNEL_ID_BASE = 1_700_000_000
SHAPE_ID_BASE = 1_710_000_000

# Outer radius (file units) of the deltoidal-hexecontahedron mesh this replaced, measured from
# spacecrystalanim.fbx before the first rewrite. Pinned rather than re-measured so a re-run cannot
# drift by an ulp and so every prefab's scale/collider keeps describing the same silhouette size.
TARGET_OUTER_RADIUS = 74.39975486705237

# Removed from the target: the old shape geometries and channels (identified by subtype).
OLD_SUBTYPES = {b"Shape", b"BlendShapeChannel"}


# ----------------------------------------------------------------------------- helpers

def _objects(nodes):
    return next(n for n in nodes if n.name == "Objects")


def _connections(nodes):
    return next(n for n in nodes if n.name == "Connections")


def _arr(node, name):
    return np.array(node.first(name).props[0][1])


def _name(node):
    return node.props[1][1].split(b"\x00")[0]


def _axes(nodes):
    gs = next(n for n in nodes if n.name == "GlobalSettings").first("Properties70")
    p = {c.props[0][1]: c.props[4][1] for c in gs.children}
    return p


def _frame(axes):
    """Columns = (right, up, front) unit vectors expressed in file coordinates."""
    def axis(i, s):
        v = np.zeros(3); v[i] = s; return v
    return np.column_stack([axis(axes[b"CoordAxis"], axes[b"CoordAxisSign"]),
                            axis(axes[b"UpAxis"], axes[b"UpAxisSign"]),
                            axis(axes[b"FrontAxis"], axes[b"FrontAxisSign"])])


def face_normals(pos, quads):
    """Newell normal per quad - robust to the slight non-planarity of the exported quads."""
    p = pos[quads]
    n = np.zeros((len(quads), 3))
    for i in range(4):
        a, b = p[:, i], p[:, (i + 1) % 4]
        n += np.stack([(a[:, 1] - b[:, 1]) * (a[:, 2] + b[:, 2]),
                       (a[:, 2] - b[:, 2]) * (a[:, 0] + b[:, 0]),
                       (a[:, 0] - b[:, 0]) * (a[:, 1] + b[:, 1])], 1)
    return n / np.linalg.norm(n, axis=1, keepdims=True)


def angle_deg(a, b):
    a = a / np.linalg.norm(a, axis=1, keepdims=True)
    b = b / np.linalg.norm(b, axis=1, keepdims=True)
    return np.degrees(np.arccos(np.clip((a * b).sum(1), -1.0, 1.0)))


# ----------------------------------------------------------------------------- source

def load_source():
    nodes, _, _ = F.read(SOURCE)
    obj = _objects(nodes)
    geos = obj.find("Geometry")
    mesh = [g for g in geos if g.props[2][1] == b"Mesh"]
    assert len(mesh) == 1, "source must hold exactly one mesh geometry"
    mesh = mesh[0]

    V = _arr(mesh, "Vertices").reshape(-1, 3)
    pvi = _arr(mesh, "PolygonVertexIndex")
    assert len(pvi) % 4 == 0 and all(pvi[3::4] < 0) and all(pvi[0::4] >= 0) \
        and all(pvi[1::4] >= 0) and all(pvi[2::4] >= 0), "source must be all quads"
    quads = np.where(pvi < 0, -pvi - 1, pvi).reshape(-1, 4)

    deltas = {}
    for g in geos:
        if g.props[2][1] != b"Shape":
            continue
        d = np.zeros_like(V)
        d[_arr(g, "Indexes")] = _arr(g, "Vertices").reshape(-1, 3)
        deltas[_name(g).decode()] = d
    want = {k for pair in SPINS for k in pair}
    assert set(deltas) == want, "source shape keys %s != expected %s" % (sorted(deltas), sorted(want))

    # Stored base normals must be the geometric face normals (hard-edged blocks) - otherwise the
    # exact-normal recomputation below would change the look of the rest pose.
    ln = mesh.first("LayerElementNormal")
    N = _arr(ln, "Normals").reshape(-1, 3)[_arr(ln, "NormalsIndex")].reshape(-1, 4, 3)
    fn = face_normals(V, quads)
    assert angle_deg(N.reshape(-1, 3), np.repeat(fn, 4, 0)).max() < 0.5, "source normals are not flat"

    # Each spin must land on a pose congruent to the start (the whole point of the design).
    for a, b in SPINS:
        end = V + deltas[a] + deltas[b]
        d2 = ((end[:, None, :] - V[None, :, :]) ** 2).sum(2)
        assert np.sqrt(d2.min(1)).max() < 1e-4 and len(set(d2.argmin(1))) == len(V), \
            "%s + %s does not end on a permutation of the base pose" % (a, b)
    return _axes(nodes), V, quads, deltas


# ----------------------------------------------------------------------------- build

def convert(source_axes, target_axes, V, deltas, target_radius):
    """Re-express source data in the target's declared axis system, scaled to target_radius."""
    M = _frame(target_axes) @ np.linalg.inv(_frame(source_axes))
    assert abs(np.linalg.det(M) - 1.0) < 1e-9, "axis conversion must be a proper rotation"
    s = target_radius / np.linalg.norm(V, axis=1).max()
    return (V @ M.T) * s, {k: (d @ M.T) * s for k, d in deltas.items()}, s


def build_frames(V, quads, deltas):
    """Unwelded positions/normals and per-key in-between frames (position + normal deltas)."""
    corner = quads.reshape(-1)                       # control point per corner
    base_pos = V[corner]
    base_n = np.repeat(face_normals(V, quads), 4, 0)
    channels = []
    for first, second in SPINS:
        for key, start in ((first, V), (second, V + deltas[first])):
            d = deltas[key]
            start_n = face_normals(start, quads)
            frames = []
            for k in range(1, IN_BETWEENS + 1):
                w = k / IN_BETWEENS
                dpos = (w * d)[corner]
                dn = np.repeat(face_normals(start + w * d, quads) - start_n, 4, 0)
                frames.append((100.0 * w, dpos, dn))
            channels.append((key, frames))
    return base_pos, base_n, channels


def report(V, quads, deltas):
    ws = np.linspace(0, 1, 201)
    print("max normal error vs the true face normal (deg), over each key's 0..100 sweep")
    print("%-14s %-24s %8s %8s" % ("spin", "scheme", "1st half", "2nd half"))
    for first, second in SPINS:
        dA, dB = deltas[first], deltas[second]
        n0 = face_normals(V, quads)
        calcA, calcB = face_normals(V + dA, quads) - n0, face_normals(V + dB, quads) - n0
        e1 = max(angle_deg(n0 + w * calcA, face_normals(V + w * dA, quads)).max() for w in ws)
        e2 = max(angle_deg(n0 + calcA + w * calcB, face_normals(V + dA + w * dB, quads)).max() for w in ws)
        spin = first.split("-")[0]
        print("%-14s %-24s %8.2f %8.2f" % (spin, "Unity Calculate (before)", e1, e2))
        for K in (1, 2, 4, 8):
            errs = []
            for start, d in ((V, dA), (V + dA, dB)):
                fr = [face_normals(start + (k / K) * d, quads) for k in range(K + 1)]
                m = 0.0
                for w in ws:
                    k = min(int(w * K), K - 1); t = w * K - k
                    m = max(m, angle_deg(fr[k] + (fr[k + 1] - fr[k]) * t, face_normals(start + w * d, quads)).max())
                errs.append(m)
            tag = "this tool, %d frame%s/key%s" % (K, "" if K == 1 else "s", " *" if K == IN_BETWEENS else "")
            print("%-14s %-24s %8.2f %8.2f" % ("", tag, errs[0], errs[1]))


def rebuild_target(src_axes, V, quads, deltas):
    nodes, version, footer = F.read(TARGET)
    obj, con = _objects(nodes), _connections(nodes)
    mesh = next(g for g in obj.find("Geometry") if g.props[0][1] == MESH_GEOMETRY_ID)
    assert mesh.props[2][1] == b"Mesh"
    deformer = next(d for d in obj.find("Deformer") if d.props[0][1] == BLENDSHAPE_DEFORMER_ID)
    assert deformer.props[2][1] == b"BlendShape"

    V, deltas, scale = convert(src_axes, _axes(nodes), V, deltas, TARGET_OUTER_RADIUS)
    base_pos, base_n, channels = build_frames(V, quads, deltas)
    n_corner = len(base_pos)

    # --- mesh geometry: rebuilt in place (same id, same name) ---
    props70 = F.Node("Properties70", [], [
        F.Node("P", [("S", key.encode()), ("S", b"Number"), ("S", b""), ("S", b"A"), ("D", 0.0)])
        for key, _ in channels])
    pvi = [i if i % 4 != 3 else -i - 1 for i in range(n_corner)]
    mesh.children = [
        props70,
        F.Node("GeometryVersion", [("I", 124)]),
        F.Node("Vertices", [("d", [float(x) for x in base_pos.reshape(-1)])]),
        F.Node("PolygonVertexIndex", [("i", pvi)]),
        # Unwelded quads share no edge, so the edge list (first corner of each undirected edge,
        # in corner order - the rule the source's own Edges array reproduces) is every corner.
        F.Node("Edges", [("i", list(range(n_corner)))]),
        F.Node("LayerElementNormal", [("I", 0)], [
            F.Node("Version", [("I", 101)]),
            F.Node("Name", [("S", b"")]),
            F.Node("MappingInformationType", [("S", b"ByPolygonVertex")]),
            F.Node("ReferenceInformationType", [("S", b"Direct")]),
            F.Node("Normals", [("d", [float(x) for x in base_n.reshape(-1)])]),
        ]),
        F.Node("Layer", [("I", 0)], [
            F.Node("Version", [("I", 100)]),
            F.Node("LayerElement", [], [
                F.Node("Type", [("S", b"LayerElementNormal")]),
                F.Node("TypedIndex", [("I", 0)]),
            ]),
        ]),
    ]

    # --- drop the old channels + shapes and every connection that names them ---
    doomed = {o.props[0][1] for o in obj.children
              if len(o.props) >= 3 and o.props[2][1] in OLD_SUBTYPES}
    obj.children = [o for o in obj.children
                    if not (len(o.props) >= 3 and o.props[2][1] in OLD_SUBTYPES)]
    con.children = [c for c in con.children
                    if c.props[1][1] not in doomed and c.props[2][1] not in doomed]

    # --- new channels + in-between shapes, inserted where the old ones sat (after the deformer) ---
    new_objects, new_links = [], []
    indexes = list(range(n_corner))
    for ci, (key, frames) in enumerate(channels):
        cid = CHANNEL_ID_BASE + ci
        new_objects.append(F.Node("Deformer", [("L", cid), ("S", key.encode() + b"\x00\x01SubDeformer"),
                                               ("S", b"BlendShapeChannel")], [
            F.Node("Version", [("I", 100)]),
            F.Node("DeformPercent", [("D", 0.0)]),
            F.Node("FullWeights", [("d", [w for w, _, _ in frames])]),
        ]))
        new_links.append(F.Node("C", [("S", b"OO"), ("L", cid), ("L", BLENDSHAPE_DEFORMER_ID)]))
        for fi, (w, dpos, dn) in enumerate(frames):
            sid = SHAPE_ID_BASE + ci * 100 + fi
            sname = key if fi == len(frames) - 1 else "%s_%d" % (key, int(round(w)))
            new_objects.append(F.Node("Geometry", [("L", sid), ("S", sname.encode() + b"\x00\x01Geometry"),
                                                   ("S", b"Shape")], [
                F.Node("Properties70", [], [], empty_scope=True),
                F.Node("Version", [("I", 100)]),
                F.Node("Indexes", [("i", indexes)]),
                F.Node("Vertices", [("d", [float(x) for x in dpos.reshape(-1)])]),
                F.Node("Normals", [("d", [float(x) for x in dn.reshape(-1)])]),
            ]))
            new_links.append(F.Node("C", [("S", b"OO"), ("L", sid), ("L", cid)]))

    at = obj.children.index(deformer) + 1
    obj.children[at:at] = new_objects
    link_at = next(i for i, c in enumerate(con.children)
                   if c.props[1][1] == BLENDSHAPE_DEFORMER_ID) + 1
    con.children[link_at:link_at] = new_links

    # --- Definitions counts ---
    defs = next(n for n in nodes if n.name == "Definitions")
    counts = {}
    for o in obj.children:
        counts[o.name] = counts.get(o.name, 0) + 1
    total = 0
    for ot in defs.find("ObjectType"):
        kind = ot.props[0][1].decode()
        c = 1 if kind == "GlobalSettings" else counts.get(kind, 0)
        ot.first("Count").props[0] = ("I", c)
        total += c
    defs.first("Count").props[0] = ("I", total)

    validate(nodes, base_pos, quads, channels)
    return nodes, version, footer, scale


def validate(nodes, base_pos, quads, channels):
    obj, con = _objects(nodes), _connections(nodes)
    ids = [o.props[0][1] for o in obj.children if o.props and o.props[0][0] == "L"]
    assert len(ids) == len(set(ids)), "duplicate object ids"
    idset = set(ids) | {0}
    for c in con.children:
        assert c.props[1][1] in idset and c.props[2][1] in idset, "dangling connection %r" % (c.props,)
    defs = next(n for n in nodes if n.name == "Definitions")
    for ot in defs.find("ObjectType"):
        kind = ot.props[0][1].decode()
        if kind != "GlobalSettings":
            assert ot.first("Count").props[0][1] == len(obj.find(kind)), "Definitions count for %s" % kind

    # Channel order under the deformer is the blend-shape index order the animator drives.
    chans = [c.props[1][1] for c in con.children if c.props[2][1] == BLENDSHAPE_DEFORMER_ID]
    assert chans == [CHANNEL_ID_BASE + i for i in range(len(channels))], "channel order"

    # Replay Unity's blend: normal = base + sum(active keys' lerped frame deltas), normalized.
    # Every sample of every spin must match the true face normal of the deformed pose, and the
    # end of each spin must equal the base normal of whichever face now sits in that slot.
    corner = np.arange(len(base_pos)).reshape(-1, 4)
    n0 = np.repeat(face_normals(base_pos, corner), 4, 0)

    def key_delta(frames, w):
        if w <= 0:
            return 0.0, 0.0
        ws = [0.0] + [f[0] / 100.0 for f in frames]
        ps = [0.0] + [f[1] for f in frames]
        ns = [0.0] + [f[2] for f in frames]
        k = min(int(w * len(frames)), len(frames) - 1)
        t = (w - ws[k]) / (ws[k + 1] - ws[k])
        return ps[k] + (ps[k + 1] - ps[k]) * t, ns[k] + (ns[k + 1] - ns[k]) * t

    worst = 0.0
    for si in range(len(SPINS)):
        a, b = channels[2 * si][1], channels[2 * si + 1][1]
        for wa, wb in [(w, 0.0) for w in np.linspace(0, 1, 41)] + [(1.0, w) for w in np.linspace(0, 1, 41)]:
            pa, na = key_delta(a, wa)
            pb, nb = key_delta(b, wb)
            pos = base_pos + pa + pb
            truth = np.repeat(face_normals(pos, corner), 4, 0)
            worst = max(worst, angle_deg(n0 + na + nb, truth).max())
        end = base_pos + a[-1][1] + b[-1][1]
        end_n = n0 + a[-1][2] + b[-1][2]
        cen_end, cen0 = end.reshape(-1, 4, 3).mean(1), base_pos.reshape(-1, 4, 3).mean(1)
        j = ((cen_end[:, None] - cen0[None]) ** 2).sum(2).argmin(1)
        pop = angle_deg(end_n.reshape(-1, 4, 3)[:, 0], n0.reshape(-1, 4, 3)[j, 0]).max()
        assert pop < 0.1, "spin %d pops %.2f deg on reset" % (si, pop)
    assert worst < 1.0, "blended normal error %.2f deg exceeds 1 deg" % worst
    return worst


def check_meta():
    meta = open(TARGET_META).read()
    assert "blendShapeNormalImportMode: 0" in meta, \
        "%s must import blend-shape normals from the file (blendShapeNormalImportMode: 0)" % TARGET_META
    assert "normalImportMode: 0" in meta and "importBlendShapes: 1" in meta


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--check", action="store_true", help="fail if the shipped FBX differs from a rebuild")
    ap.add_argument("--report", action="store_true", help="print the normal-error table and exit")
    args = ap.parse_args()

    src_axes, V, quads, deltas = load_source()
    if args.report:
        report(V, quads, deltas)
        return 0

    nodes, version, footer, scale = rebuild_target(src_axes, V, quads, deltas)
    tmp = TARGET + ".tmp"
    F.write(tmp, nodes, version, footer)
    new = open(tmp, "rb").read()
    os.remove(tmp)
    old = open(TARGET, "rb").read()
    check_meta()

    if args.check:
        if new != old:
            print("DRIFT: %s differs from a rebuild - run without --check" % os.path.relpath(TARGET, ROOT))
            return 1
        print("ok: %s matches its rebuild" % os.path.relpath(TARGET, ROOT))
        return 0
    if new == old:
        print("already up to date: %s" % os.path.relpath(TARGET, ROOT))
        return 0
    open(TARGET, "wb").write(new)
    print("wrote %s (%d bytes, source scaled x%.4f, %d channels x %d frames)"
          % (os.path.relpath(TARGET, ROOT), len(new), scale, len(SPINS) * 2, IN_BETWEENS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
