#!/usr/bin/env python3
"""Read a binary FBX model the way Unity imports it: nodes, meshes and skins in UNITY space and UNITY units.

    python3 Tools/Build/fauna_kit/fbx_model.py Assets/_Models/Fauna/Shark_model.fbx     # a survey of one file

A READER (it writes nothing). It exists so a creature's body can be measured without the editor
(/fauna section 6: "a raw FBX extent is meaningless until it is normalised by that file's own UnitScaleFactor").
What it applies, and where each rule was checked:
- the file scale: lengths x UnitScaleFactor / 100 (useFileScale), then the importer's globalScale (the .meta);
- handedness: FBX is right-handed and Unity left-handed, so x is negated (positions (-x, y, z), rotations
  (x, -y, -z, w)) and triangle winding flips;
- a node's local matrix: T x PreRotation x Lcl Rotation x S, Euler XYZ (the FBX default);
- the axis system: a Z-up file (UpAxis 2, Blender's armature exports) has every ROOT node turned onto Unity's Y-up
  by the quaternion (0, 0.7071, 0.7071, 0); a Y-up file is left as it is. Any other axis system raises;
The shark prefab is an UNPACKED copy of Shark_model.fbx (its bones are plain Transform documents), so
dossier.py --self-test holds this reader's bone poses to the prefab's own (the handedness and unit rules above
are proven there, not assumed).
"""
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import fbx_binary  # noqa: E402


# ------------------------------------------------------------------ small linear algebra (row-major 4x4)

def ident():
    return [[1.0, 0, 0, 0], [0, 1.0, 0, 0], [0, 0, 1.0, 0], [0, 0, 0, 1.0]]


def mmul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def mpoint(m, p):
    return [m[0][0] * p[0] + m[0][1] * p[1] + m[0][2] * p[2] + m[0][3],
            m[1][0] * p[0] + m[1][1] * p[1] + m[1][2] * p[2] + m[1][3],
            m[2][0] * p[0] + m[2][1] * p[1] + m[2][2] * p[2] + m[2][3]]


def mdir(m, p):
    return [m[0][0] * p[0] + m[0][1] * p[1] + m[0][2] * p[2],
            m[1][0] * p[0] + m[1][1] * p[1] + m[1][2] * p[2],
            m[2][0] * p[0] + m[2][1] * p[1] + m[2][2] * p[2]]


def minv(m):
    """Inverse of an affine 4x4 (general 3x3 part)."""
    a = [r[:3] for r in m[:3]]
    det = (a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1]) - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0])
           + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0]))
    if abs(det) < 1e-18:
        return ident()
    c = [[0.0] * 3 for _ in range(3)]
    for i in range(3):
        for j in range(3):
            i1, i2 = (i + 1) % 3, (i + 2) % 3
            j1, j2 = (j + 1) % 3, (j + 2) % 3
            c[j][i] = (a[i1][j1] * a[i2][j2] - a[i1][j2] * a[i2][j1]) / det
    t = [m[0][3], m[1][3], m[2][3]]
    out = ident()
    for i in range(3):
        for j in range(3):
            out[i][j] = c[i][j]
        out[i][3] = -sum(c[i][k] * t[k] for k in range(3))
    return out


def quat_to_m(q):
    x, y, z, w = q
    return [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 0.0],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 0.0],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y), 0.0],
            [0.0, 0.0, 0.0, 1.0]]


def trs(t, q, s):
    m = quat_to_m(q)
    for i in range(3):
        for j in range(3):
            m[i][j] *= s[j]
        m[i][3] = t[i]
    return m


def euler_xyz_to_quat(rx, ry, rz):
    """FBX eEulerXYZ in degrees: R = Rz * Ry * Rx (x applied first). Returns (x, y, z, w)."""
    hx, hy, hz = math.radians(rx) / 2, math.radians(ry) / 2, math.radians(rz) / 2
    cx, sx, cy, sy, cz, sz = math.cos(hx), math.sin(hx), math.cos(hy), math.sin(hy), math.cos(hz), math.sin(hz)
    return (sx * cy * cz - cx * sy * sz, cx * sy * cz + sx * cy * sz, cx * cy * sz - sx * sy * cz, cx * cy * cz + sx * sy * sz)


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)


# ------------------------------------------------------------------ the reader

def _s(v):
    return v.decode("utf-8", "replace") if isinstance(v, (bytes, bytearray)) else v


def _props70(node):
    out = {}
    p70 = node.first("Properties70")
    for p in (p70.find("P") if p70 else []):
        vals = [_s(v) for _t, v in p.props]
        if vals:
            out[vals[0]] = vals[4:]
    return out


def _name(node):
    raw = _s(node.props[1][1]) if len(node.props) > 1 else ""
    return raw.split("\x00")[0]


def importer_scale(fbx_path):
    """The importer's globalScale from the .meta (worm.fbx is the one creature file that is not 1)."""
    meta = fbx_path + ".meta"
    if not os.path.exists(meta):
        return 1.0
    vals = re.findall(r"^\s+globalScale: ([-\d.eE]+)", open(meta).read(), re.M)
    return float(vals[-1]) if vals else 1.0


def load(fbx_path):
    """{'unit', 'nodes': {id: {...}}, 'roots': [ids], 'meshes': [{node, name, verts, tris, skin}]} in Unity space."""
    tops, _version, _footer = fbx_binary.read(fbx_path)
    top = {n.name: n for n in tops}
    gs = _props70(top["GlobalSettings"]) if "GlobalSettings" in top else {}
    unit_factor = float(gs.get("UnitScaleFactor", [1.0])[-1])
    unit = unit_factor / 100.0 * importer_scale(fbx_path)

    objects = top["Objects"]
    models, geos, deformers = {}, {}, {}
    for m in objects.find("Model"):
        mid = m.props[0][1]
        p = _props70(m)
        t = [float(v) for v in p.get("Lcl Translation", [0, 0, 0])[:3]]
        r = [float(v) for v in p.get("Lcl Rotation", [0, 0, 0])[:3]]
        s = [float(v) for v in p.get("Lcl Scaling", [1, 1, 1])[:3]]
        pre = [float(v) for v in p.get("PreRotation", [0, 0, 0])[:3]]
        q = qmul(euler_xyz_to_quat(*pre), euler_xyz_to_quat(*r))
        # right-handed FBX to left-handed Unity: negate x
        models[mid] = {"id": mid, "name": _name(m), "kind": _s(m.props[2][1]) if len(m.props) > 2 else "",
                       "t": [-t[0] * unit, t[1] * unit, t[2] * unit], "q": (q[0], -q[1], -q[2], q[3]), "s": s,
                       "parent": None, "children": [], "geo": None}
    for g in objects.find("Geometry"):
        gid = g.props[0][1]
        v = g.first("Vertices")
        pvi = g.first("PolygonVertexIndex")
        if not v or not pvi:
            continue
        flat = v.props[0][1]
        verts = [[-flat[i] * unit, flat[i + 1] * unit, flat[i + 2] * unit] for i in range(0, len(flat), 3)]
        polys, cur = [], []
        for idx in pvi.props[0][1]:
            if idx < 0:
                cur.append(~idx)
                polys.append(cur)
                cur = []
            else:
                cur.append(idx)
        geos[gid] = {"name": _name(g), "verts": verts, "polys": polys, "skin": None}
    for d in objects.find("Deformer"):
        did = d.props[0][1]
        kind = _s(d.props[2][1]) if len(d.props) > 2 else ""
        entry = {"kind": kind, "clusters": [], "bone": None}
        if kind == "Cluster":
            ix = d.first("Indexes")
            wt = d.first("Weights")
            entry["indexes"] = ix.props[0][1] if ix and ix.props else []
            entry["weights"] = wt.props[0][1] if wt and wt.props else []
        deformers[did] = entry

    for c in top["Connections"].find("C"):
        kind = _s(c.props[0][1])
        child, parent = c.props[1][1], c.props[2][1]
        if kind != "OO":
            continue
        if child in models and parent in models:
            models[child]["parent"] = parent
            models[parent]["children"].append(child)
        elif child in geos and parent in models:
            models[parent]["geo"] = child
        elif child in deformers and parent in geos:
            geos[parent]["skin"] = child
        elif child in deformers and parent in deformers:
            deformers[parent]["clusters"].append(child)
        elif child in models and parent in deformers:
            deformers[parent]["bone"] = child

    def world(mid, cache={}):
        if mid in cache:
            return cache[mid]
        m = models[mid]
        loc = trs(m["t"], m["q"], m["s"])
        w = loc if m["parent"] is None else mmul(world(m["parent"]), loc)
        cache[mid] = w
        return w

    wcache = {}
    # the axis system: Unity turns a Z-up scene onto Y-up at the root nodes (measured on Shark_model.fbx against the
    # unpacked copy of it in MassSharkFauna.prefab: mesh root (0, .7071, .7071, 0), Armature (-.7071, 0, 0, .7071))
    up = int(gs.get("UpAxis", [1])[0])
    if up == 2:
        c = (0.0, math.sqrt(0.5), math.sqrt(0.5), 0.0)
        cm = quat_to_m(c)
        for mid, m in models.items():
            if m["parent"] is None:
                m["q"] = qmul(c, m["q"])
                m["t"] = mpoint(cm, m["t"])
    elif up != 1:
        raise ValueError(f"{fbx_path}: UpAxis {up} is an axis system this reader has not been proven on")
    for mid in models:
        models[mid]["world"] = world(mid, wcache)
    meshes = []
    for mid, m in models.items():
        if m["geo"] is None or m["geo"] not in geos:
            continue
        g = geos[m["geo"]]
        tris = []
        for poly in g["polys"]:
            for k in range(1, len(poly) - 1):
                tris.append((poly[0], poly[k + 1], poly[k]))   # winding flipped with the handedness
        skin = None
        if g["skin"] is not None:
            skin = []
            for cid in deformers[g["skin"]]["clusters"]:
                cl = deformers[cid]
                if cl["bone"] is not None:
                    skin.append({"bone": models[cl["bone"]]["name"], "indexes": cl["indexes"], "weights": cl["weights"]})
        meshes.append({"node": mid, "name": m["name"], "geo_name": g["name"], "verts": g["verts"], "polys": g["polys"],
                       "tris": tris, "skin": skin})
    return {"path": fbx_path, "unit": unit, "unit_factor": unit_factor, "nodes": models,
            "roots": [mid for mid, m in models.items() if m["parent"] is None], "meshes": meshes}


# ------------------------------------------------------------------ Unity's fileIDs for a model's meshes

_M64 = (1 << 64) - 1
_P1, _P2, _P3, _P4, _P5 = (11400714785074694791, 14029467366897019727, 1609587929392839161, 9650029242287828579,
                           2870177450012600261)


def _rotl(x, r):
    return ((x << r) | (x >> (64 - r))) & _M64


def _round(acc, inp):
    return (_rotl((acc + inp * _P2) & _M64, 31) * _P1) & _M64


def xxh64(data, seed=0):
    """xxHash64 (stdlib only). Known answer: xxh64(b'') == 0xEF46DB3751D8E999 (asserted in dossier.py --self-test)."""
    n, i = len(data), 0
    if n >= 32:
        v = [(seed + _P1 + _P2) & _M64, (seed + _P2) & _M64, seed, (seed - _P1) & _M64]
        while i <= n - 32:
            for k in range(4):
                v[k] = _round(v[k], int.from_bytes(data[i + 8 * k:i + 8 * k + 8], "little"))
            i += 32
        h = (_rotl(v[0], 1) + _rotl(v[1], 7) + _rotl(v[2], 12) + _rotl(v[3], 18)) & _M64
        for k in range(4):
            h = ((h ^ _round(0, v[k])) * _P1 + _P4) & _M64
    else:
        h = (seed + _P5) & _M64
    h = (h + n) & _M64
    while i <= n - 8:
        h ^= _round(0, int.from_bytes(data[i:i + 8], "little"))
        h = (_rotl(h, 27) * _P1 + _P4) & _M64
        i += 8
    if i <= n - 4:
        h ^= (int.from_bytes(data[i:i + 4], "little") * _P1) & _M64
        h = (_rotl(h, 23) * _P2 + _P3) & _M64
        i += 4
    while i < n:
        h ^= (data[i] * _P5) & _M64
        h = (_rotl(h, 11) * _P1) & _M64
        i += 1
    h ^= h >> 33
    h = (h * _P2) & _M64
    h ^= h >> 29
    h = (h * _P3) & _M64
    h ^= h >> 32
    return h


def mesh_file_id(mesh_name):
    """The fileID Unity gives a model's MESH sub-asset (fileIdsGeneration 2): xxHash64 of 'Type:Mesh-><name>0', signed.
    Measured, not remembered: mediumfish.fbx 'BezierCurve' is 150581138598605843 and Shark_model.fbx
    'Shark_creature_mesh' is -5517701702760522128 in the shipped prefabs, and this reproduces both
    (dossier.py --self-test). The same string for a Transform or a GameObject does NOT reproduce their ids, so an
    inner node is still found another way (prefab_read: the rig's constraints name the bones)."""
    h = xxh64(("Type:Mesh->%s0" % mesh_name).encode())
    return h - (1 << 64) if h >= (1 << 63) else h


def mesh_by_file_id(model, file_id):
    for me in model["meshes"]:
        if file_id in (mesh_file_id(me["name"]), mesh_file_id(me["geo_name"])):
            return me
    return None


def node_by_name(model, name):
    for m in model["nodes"].values():
        if m["name"] == name:
            return m
    return None


def components(verts, polys, weld=1e-5):
    """Connected components of a mesh (coincident vertices welded): a list of sets of polygon indices.
    /fauna section 6: "split the mesh into connected components" before reading a shape off a bounding box."""
    key = {}
    rep = []
    for v in verts:
        k = (round(v[0] / weld), round(v[1] / weld), round(v[2] / weld))
        rep.append(key.setdefault(k, len(key)))
    parent = list(range(len(key)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for poly in polys:
        r0 = find(rep[poly[0]])
        for i in poly[1:]:
            r1 = find(rep[i])
            if r1 != r0:
                parent[r1] = r0
    groups = {}
    for pi, poly in enumerate(polys):
        groups.setdefault(find(rep[poly[0]]), set()).add(pi)
    return list(groups.values())


def bounds(points):
    lo = [min(p[i] for p in points) for i in range(3)]
    hi = [max(p[i] for p in points) for i in range(3)]
    return lo, hi


def main(argv):
    if not argv:
        print(__doc__)
        return 2
    for path in argv:
        model = load(path)
        print(f"{path}: UnitScaleFactor {model['unit_factor']:g} (x{model['unit']:g} to Unity units), "
              f"{len(model['nodes'])} nodes, {len(model['meshes'])} meshes")

        def walk(mid, depth):
            m = model["nodes"][mid]
            print("  " * (depth + 1) + f"{m['name']} [{m['kind']}] t={[round(v, 3) for v in m['t']]} "
                  f"s={[round(v, 3) for v in m['s']]}" + (" MESH" if m["geo"] is not None else ""))
            for c in m["children"]:
                walk(c, depth + 1)
        for r in model["roots"]:
            walk(r, 0)
        for me in model["meshes"]:
            w = model["nodes"][me["node"]]["world"]
            pts = [mpoint(w, v) for v in me["verts"]]
            lo, hi = bounds(pts)
            comps = components(me["verts"], me["polys"])
            print(f"  mesh {me['name']}: {len(me['verts'])} verts, {len(me['polys'])} polys, {len(comps)} components, "
                  f"bounds {[round(v, 2) for v in lo]} .. {[round(v, 2) for v in hi]}"
                  + (f", skinned to {len(me['skin'])} bones" if me["skin"] else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
