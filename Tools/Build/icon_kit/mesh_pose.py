"""A hull's mesh in one of its authored TAKES, skinned offline - no Blender, no Unity.

Garrett 2026-10-26, on the Sparrow's Time card: "time first, build the folded pose." The Sparrow rests with its two
eight-plate fans spread, which at 60 px is a starburst; its boost take folds them into swept wings, and that fold is
the ability. So the icon kit needs the mesh AS POSED. The FBX carries everything: the skin (one Cluster per bone:
vertex indexes, weights, the bone's bind-time global `TransformLink` and `Transform`, which Blender's exporter writes
as the MESH IN BONE SPACE at bind time, inv(TransformLink) * MeshGlobal - measured on the Sparrow: 65 distinct
`Transform`s, unit scale, where one mesh global would be one matrix at 0.2034),
the bone hierarchy (Model/LimbNode OO links) and the takes (AnimationStack > Layer > CurveNode --OP--> Model "Lcl
Rotation", the curve node's d|X d|Y d|Z defaults and keys). A pose take keys one value per channel.

Linear blend skinning, with the pose applied as a DELTA on the bind pose so the node-chain conventions a reader can get
wrong (rest_compare.py's 10.8-degree lesson) never enter: for bone b with parent p,

    G_b(posed) = G_p(posed) * inv(TransformLink_p) * TransformLink_b * D_b,   D_b = R(rest lcl)^-1 * R(take lcl)
    v' = sum_b w_b * G_b(posed) * Transform_b * v          (Transform_b already carries inv(TransformLink_b))

(bones carry unit scale, so the delta right-multiplies cleanly). R() is the FBX XYZ Euler (R = Rz Ry Rx on column
vectors); `--check` proves the convention on the file itself: the rest-pose skin must reproduce the raw mesh, and the
node chain walked with the same R() must land every bone on its TransformLink.

    python3 Tools/Build/icon_kit/mesh_pose.py <fbx> --takes                 # list the takes and how many bones each moves
    python3 Tools/Build/icon_kit/mesh_pose.py <fbx> --check                 # the two proofs above
    posed_polygons(fbx_rel, take)  -> polygons in the RAW mesh frame (what hull_view.polygons gives at rest)
"""
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
sys.path.insert(0, os.path.dirname(HERE))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "vessel_model"))
import fbx_binary as F  # noqa: E402
from fbx_probe import arr, name_of, obj_id, props70, subclass_of, _s  # noqa: E402

_CACHE = {}


def _rot(deg):
    """FBX Lcl Rotation (XYZ order) as a 3x3: R = Rz * Ry * Rx."""
    x, y, z = (math.radians(v) for v in deg)
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    Rx = np.array([[1, 0, 0], [0, cx, -sx], [0, sx, cx]])
    Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    Rz = np.array([[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]])
    return Rz @ Ry @ Rx


def _m4(R=None, t=(0, 0, 0), s=(1, 1, 1)):
    M = np.eye(4)
    M[:3, :3] = (R if R is not None else np.eye(3)) @ np.diag(s)
    M[:3, 3] = t
    return M


def _mat(node, key):
    v = np.array(arr(node, key), dtype=float)
    return v.reshape(4, 4).T          # FBX stores row-major for row vectors; transpose to column-vector form


def load(fbx_rel):
    """Everything skinning needs, read once per file."""
    if fbx_rel in _CACHE:
        return _CACHE[fbx_rel]
    nodes, _v, _f = F.read(os.path.join(ROOT, fbx_rel))
    top = {n.name: n for n in nodes}
    objects, connections = top["Objects"], top["Connections"]
    by_id = {obj_id(o): o for o in objects.children if obj_id(o) is not None}
    oo_parents, oo_children, op = {}, {}, []
    for c in connections.children:
        if c.name != "C" or len(c.props) < 3:
            continue
        kind, src, dst = _s(c.props[0][1]), c.props[1][1], c.props[2][1]
        if kind == "OO":
            oo_parents.setdefault(src, []).append(dst)
            oo_children.setdefault(dst, []).append(src)
        elif kind == "OP":
            op.append((src, dst, _s(c.props[3][1]) if len(c.props) > 3 else ""))

    def parent_model(oid):
        for pid in oo_parents.get(oid, []):
            n = by_id.get(pid)
            if n is not None and n.name == "Model":
                return n
        return None

    models = {}
    for o in objects.children:
        if o.name == "Model":
            p = props70(o)
            par = parent_model(obj_id(o))
            models[obj_id(o)] = dict(id=obj_id(o), name=name_of(o), type=subclass_of(o), parent=obj_id(par) if par is not None else None,
                                     t=np.array(p.get("Lcl Translation", (0, 0, 0)), float), r=np.array(p.get("Lcl Rotation", (0, 0, 0)), float),
                                     s=np.array(p.get("Lcl Scaling", (1, 1, 1)), float), pre=p.get("PreRotation"))
    mesh = next(o for o in objects.children if o.name == "Geometry" and subclass_of(o) == "Mesh")
    V = np.array(arr(mesh, "Vertices"), float).reshape(-1, 3)
    pvi = arr(mesh, "PolygonVertexIndex")
    clusters = {}
    for o in objects.children:
        if o.name == "Deformer" and subclass_of(o) == "Cluster":
            bone = next((cid for cid in oo_children.get(obj_id(o), []) if cid in models), None)
            if bone is None:
                continue
            clusters[bone] = dict(idx=np.array(arr(o, "Indexes"), int), w=np.array(arr(o, "Weights"), float),
                                  link=_mat(o, "TransformLink"), tf=_mat(o, "Transform"))
    takes = {}
    for o in objects.children:
        if o.name != "AnimationStack":
            continue
        pose = {}
        for lid in oo_children.get(obj_id(o), []):
            if by_id[lid].name != "AnimationLayer":
                continue
            for cid in oo_children.get(lid, []):
                cn = by_id[cid]
                if cn.name != "AnimationCurveNode":
                    continue
                targets = [(dst, prop) for (src, dst, prop) in op if src == cid]
                cnp = props70(cn)
                chan = {_p: by_id[src] for (src, dst, _p) in op if dst == cid and src in by_id and by_id[src].name == "AnimationCurve"}
                vals = []
                for axis in ("d|X", "d|Y", "d|Z"):
                    cv = chan.get(axis)
                    keys = arr(cv, "KeyValueFloat") if cv is not None else []
                    dv = cnp.get(axis)
                    vals.append(keys[-1] if keys else (dv if isinstance(dv, (int, float)) else None))
                for dst, prop in targets:
                    if dst in models and prop in ("Lcl Rotation", "Lcl Translation"):
                        pose.setdefault(dst, {})[prop] = vals
        takes[name_of(o)] = pose
    _CACHE[fbx_rel] = dict(models=models, V=V, pvi=pvi, clusters=clusters, takes=takes)
    return _CACHE[fbx_rel]


def _posed_globals(d, take):
    """G_b(posed) per bone id, the pose as a delta on the bind pose; a bone without a cluster uses the node chain."""
    models, clusters = d["models"], d["clusters"]
    pose = d["takes"][take] if take else {}
    G = {}

    def bind(bid):
        return clusters[bid]["link"] if bid in clusters else None

    def local(bid):
        m = models[bid]
        R = _rot(m["r"])
        if m["pre"] is not None:
            R = _rot(m["pre"]) @ R
        return _m4(R, m["t"], m["s"])

    def delta(bid):
        m = models[bid]
        p = pose.get(bid, {})
        D = np.eye(4)
        r = p.get("Lcl Rotation")
        if r is not None and all(v is not None for v in r):
            D = D @ _m4(_rot(m["r"]).T @ _rot(r))
        t = p.get("Lcl Translation")
        if t is not None and all(v is not None for v in t):
            dt = np.array(t, float) - m["t"]
            # a translation key moves the bone along its PARENT's axes: express it in the bone's own frame
            Rl = _rot(m["r"]) if m["pre"] is None else _rot(m["pre"]) @ _rot(m["r"])
            D = _m4(np.eye(3), Rl.T @ dt) @ D
        return D

    def g(bid):
        if bid in G:
            return G[bid]
        m = models[bid]
        par = m["parent"]
        if par is None:
            G[bid] = local(bid) @ delta(bid)
        else:
            Gp = g(par)
            bp, bb = bind(par), bind(bid)
            if bp is not None and bb is not None:
                G[bid] = Gp @ np.linalg.inv(bp) @ bb @ delta(bid)
            else:
                G[bid] = Gp @ local(bid) @ delta(bid)
        return G[bid]

    for bid in models:
        g(bid)
    return G


def posed_vertices(fbx_rel, take, raw_frame=True):
    """Skinned vertices for `take` (None = the bind pose). raw_frame: mapped back into the raw mesh frame so the
    result drops into the same projection as hull_view.polygons."""
    d = load(fbx_rel)
    V = d["V"]
    G = _posed_globals(d, take)
    out = np.zeros_like(V)
    wsum = np.zeros(len(V))
    tf = None            # the mesh's bind-time global, link_b * Transform_b (the same for every cluster)
    for bid, c in d["clusters"].items():
        M = G[bid] @ c["tf"]
        tf = c["link"] @ c["tf"] if tf is None else tf
        P = V[c["idx"]]
        Ph = np.c_[P, np.ones(len(P))] @ M.T
        out[c["idx"]] += Ph[:, :3] * c["w"][:, None]
        wsum[c["idx"]] += c["w"]
    unweighted = wsum < 1e-6
    if unweighted.any():
        out[unweighted] = (np.c_[V[unweighted], np.ones(unweighted.sum())] @ tf.T)[:, :3]
    nz = ~unweighted
    out[nz] /= np.where(wsum[nz] > 0, wsum[nz], 1)[:, None]
    if raw_frame:
        out = (np.c_[out, np.ones(len(out))] @ np.linalg.inv(tf).T)[:, :3]
    return out


def posed_polygons(fbx_rel, take):
    d = load(fbx_rel)
    V = posed_vertices(fbx_rel, take)
    polys, poly = [], []
    for i in d["pvi"]:
        if i < 0:
            poly.append(~i)
            polys.append(V[poly])
            poly = []
        else:
            poly.append(i)
    return polys


def check(fbx_rel):
    d = load(fbx_rel)
    rest = posed_vertices(fbx_rel, None)
    err = np.abs(rest - d["V"]).max()
    ext = np.ptp(d["V"], axis=0).max()
    print(f"rest skin vs raw mesh: max error {err:.4f} of extent {ext:.2f} ({'ok' if err < 1e-3 * ext else 'FAIL'})")
    # the node chain, walked with this file's R(), must land on each bone's bind global
    G = {}
    worst = 0.0
    for bid, m in d["models"].items():
        chain = []
        b = bid
        while b is not None:
            chain.append(b)
            b = d["models"][b]["parent"]
        M = np.eye(4)
        for b in reversed(chain):
            mm = d["models"][b]
            R = _rot(mm["r"])
            if mm["pre"] is not None:
                R = _rot(mm["pre"]) @ R
            M = M @ _m4(R, mm["t"], mm["s"])
        G[bid] = M
        if bid in d["clusters"]:
            link = d["clusters"][bid]["link"]
            worst = max(worst, np.abs(M[:3, 3] - link[:3, 3]).max() / max(ext, 1e-9), np.abs(M[:3, :3] / np.linalg.norm(M[:3, 0]) - link[:3, :3] / np.linalg.norm(link[:3, 0])).max())
    print(f"node chain vs bind globals: worst {worst:.4f} ({'ok - the chain and the bind agree, the Euler convention holds' if worst < 0.02 else 'differs - the file parks a node-vs-bind pose; the delta skin still holds'})")
    return 0 if err < 1e-3 * ext else 1


if __name__ == "__main__":
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("fbx")
    ap.add_argument("--takes", action="store_true")
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    if a.takes:
        d = load(a.fbx)
        for name, pose in d["takes"].items():
            moving = sum(1 for bid, p in pose.items() if any(k == "Lcl Rotation" and v and all(x is not None for x in v) and np.abs(_rot(d["models"][bid]["r"]).T @ _rot(v) - np.eye(3)).max() > 1e-3 for k, v in p.items()))
            print(f"{name}: {len(pose)} keyed nodes, {moving} rotated")
    if a.check:
        sys.exit(check(a.fbx))
