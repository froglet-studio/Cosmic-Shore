#!/usr/bin/env python3
"""
Author GENOME CRYSTAL meshes: tiny SDF operator graphs (the Amoebius genome lab's node format) meshed
offline into faceted, flat-normal Unity Mesh assets at three LODs.

    python3 Tools/Build/author_sdf_crystal_meshes.py              # write meshes + contact sheet
    python3 Tools/Build/author_sdf_crystal_meshes.py --check      # regenerate in memory, exit 1 on drift
    python3 Tools/Build/author_sdf_crystal_meshes.py --report     # print the measurements only
    python3 Tools/Build/author_sdf_crystal_meshes.py --self-test  # prove the checks fire
    python3 Tools/Build/author_sdf_crystal_meshes.py --new-guids  # three fresh guids for a new spec

Docs/GENOME_SDF_CRYSTALS.md is the plain-language version. In short:

INPUT  Tools/Build/sdf_crystals/specs/*.json, one shape each: {"name", "guids": {lod0, lod1, lod2},
       "nodes": [{"id", "op", "in", "values"}], optional "root" (default: the last node), and for the
       marching-cubes path optional "bounds", "res", "lod_tris"}. Values are the lab's DECODED values
       (what seeds/sdf4k/*.json store), used as written - this tool does not quantise them to bytes.

THREE PATHS, chosen per spec, most exact first:
  1. PIECES (exact). If every node is a half-space shape (sdf.octa, sdf.box with round 0, sdf.plane)
     or a sharp (k <= 0.0011) intersect / union, translate / rotate / scale, polar or mirror of one,
     the shape IS a union of convex polytopes. Each polytope is cut exactly with scipy's half-space
     intersection. Polar and mirror become COPIES: the child is clipped to the fold's wedge or
     half-space first, so a child that straddles the fold is still exact. Pieces inside other pieces
     and faces buried inside the union are dropped. LOD1/LOD2 greedily drop each piece's least
     useful planes (bounded volume growth) and the smallest pieces.
  2. MARCHING CUBES for everything else (gyroids, smooth unions, sphere/torus/cylinder, twist,
     displace, shell, subtract): skimage marching cubes on a res^3 grid, then fast_simplification
     quadric decimation to each LOD's triangle target (the study measured >= ~800 to keep a
     gyroid's holes; thin shells break decimation, so expect to raise res for them).
  3. There is no third path: a graph is never part-exact; one non-convex node sends the whole graph
     to marching cubes.

OUTPUT Assets/_Models/GenomeCrystals/<name>_LOD{0,1,2}.asset (+ .meta): split vertices, one normal
       per face (faceted), UV0 from a box projection, no tangents (position 12 + normal 12 + uv 8 =
       32 bytes a vertex), 16-bit indices. Positions are rounded to 1e-4 so --check is stable.
       The asset text follows Assets/_Models/Testing/Prism.asset, as author_spindle_lod_meshes.py does.
       A contact sheet PNG goes OUTSIDE the repo (--sheet, default: the temp dir); back faces draw red
       there, so a hole or a flipped triangle is visible.

Requires numpy, scipy, scikit-image, fast_simplification, Pillow (see sdf_crystals/requirements.txt).
"""

import argparse
import glob
import json
import math
import os
import re
import struct
import subprocess
import sys
import tempfile
import time
import uuid

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
assert os.path.isdir(os.path.join(REPO, "Assets")), "REPO must be the project root"

from author_primitive_meshes import fmt  # noqa: E402

try:
    import numpy as np
    from scipy.optimize import linprog
    from scipy.spatial import ConvexHull, HalfspaceIntersection
except ImportError as e:  # pragma: no cover
    sys.exit(f"missing dependency ({e}); pip install -r Tools/Build/sdf_crystals/requirements.txt")

SPEC_DIR = os.path.join(HERE, "sdf_crystals", "specs")
OUT_DIR = "Assets/_Models/GenomeCrystals"
FOLDER_GUID = "07e7d07958c24f7e8a1a9ec00b93e8a6"
DONOR_MESH = "Assets/_Models/Testing/Prism.asset"
SHARP_K = 0.0011                 # the lab's smin treats k <= this as a hard min/max
POS_ROUND = 4                    # decimals kept on every position
LOD_PLANE_TOL = (0.0, 0.02, 0.06)   # per LOD: relative volume a piece may grow when a plane is dropped
LOD_DROP_FRAC = (0.0, 0.05, 0.12)   # per LOD: pieces smaller than this share of the LARGEST piece are dropped
MC_DEFAULT = dict(bounds=4.0, res=96, lod_tris=(1200, 800, 500))
LODS = 3
CONVEX_LEAVES = ("sdf.octa", "sdf.box", "sdf.plane")
PASS_THROUGH = ("sdf.spin", "sdf.bob", "sdf.material")   # time ops at t = 0, materials ignored
DEFAULTS = {"k": 0.001, "round": 0.0, "offset": 0.0, "axes": 1, "axis": 1, "count": 5,
            "x": 0.0, "y": 0.0, "z": 0.0, "ax": 0.0, "ay": 0.0, "az": 0.0, "s": 1.0, "seed": 0.0}
KNOWN_OPS = CONVEX_LEAVES + PASS_THROUGH + (
    "sdf.sphere", "sdf.torus", "sdf.cylinder", "sdf.gyroid", "sdf.union", "sdf.subtract",
    "sdf.intersect", "sdf.translate", "sdf.rotate", "sdf.scale", "sdf.polar", "sdf.mirror",
    "sdf.twist", "sdf.displace", "sdf.shell", "sdf.invert")


# --------------------------------------------------------------------------------------------
# Specs
# --------------------------------------------------------------------------------------------

def load_specs(spec_dir=SPEC_DIR):
    specs = []
    for path in sorted(glob.glob(os.path.join(spec_dir, "*.json"))):
        with open(path) as fh:
            specs.append(json.load(fh))
    return specs


def spec_errors(spec):
    errs = []
    name = spec.get("name", "?")
    if not re.fullmatch(r"[A-Z][A-Za-z0-9]+", name):
        errs.append(f"{name}: name must be PascalCase letters/digits")
    ids = [n.get("id") for n in spec.get("nodes", [])]
    if ids != list(range(len(ids))) or not ids:
        errs.append(f"{name}: node ids must be 0..n-1 in order")
    for n in spec.get("nodes", []):
        if n.get("op") not in KNOWN_OPS:
            errs.append(f"{name}: node {n.get('id')} op {n.get('op')} is not in the supported subset")
        for j in n.get("in", []):
            if not (isinstance(j, int) and 0 <= j < n.get("id", 0)):
                errs.append(f"{name}: node {n.get('id')} input {j} must name an earlier node")
    g = spec.get("guids", {})
    for k in range(LODS):
        if not re.fullmatch(r"[0-9a-f]{32}", str(g.get(f"lod{k}", ""))):
            errs.append(f"{name}: guids.lod{k} missing or not 32 hex chars (run --new-guids)")
    return errs


def val(node, key):
    v = node.get("values", {})
    if key in v:
        return float(v[key])
    if key in DEFAULTS:
        return float(DEFAULTS[key])
    raise KeyError(f"node {node['id']} ({node['op']}) needs value '{key}'")


def root_of(spec):
    return int(spec.get("root", len(spec["nodes"]) - 1))


# --------------------------------------------------------------------------------------------
# SDF evaluation (numpy port of the lab's glsl.py shape ops, via the mesh study's sdfmesh.py)
# --------------------------------------------------------------------------------------------

def _len(v):
    return np.sqrt((v * v).sum(-1))


def _smin(a, b, k):
    if k <= SHARP_K:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


def _smax(a, b, k):
    return -_smin(-a, -b, k)


def _rot_matrix(ax, ay, az):
    cx, sx, cy, sy, cz, sz = math.cos(ax), math.sin(ax), math.cos(ay), math.sin(ay), math.cos(az), math.sin(az)
    return np.array([[cz * cy, cz * sy * sx - sz * cx, cz * sy * cx + sz * sx],
                     [sz * cy, sz * sy * sx + cz * cx, sz * sy * cx - cz * sx],
                     [-sy, cy * sx, cy * cx]])


def _plane_normal(theta, phi):
    return np.array([math.sin(theta) * math.cos(phi), math.cos(theta), math.sin(theta) * math.sin(phi)])


def _fract(x):
    return x - np.floor(x)


def _h31(p):
    p3 = _fract(p * 0.1031)
    p3 = p3 + (p3 * (p3[..., [2, 1, 0]] + 31.32)).sum(-1, keepdims=True)
    return _fract((p3[..., 0] + p3[..., 1]) * p3[..., 2])


def _vn3(p):
    i = np.floor(p)
    fr = p - i
    u = fr * fr * (3 - 2 * fr)

    def H(o):
        return _h31(i + np.array(o, float))

    def lerp(a, b, k):
        return a + (b - a) * u[..., k]
    return lerp(lerp(lerp(H((0, 0, 0)), H((1, 0, 0)), 0), lerp(H((0, 1, 0)), H((1, 1, 0)), 0), 1),
                lerp(lerp(H((0, 0, 1)), H((1, 0, 1)), 0), lerp(H((0, 1, 1)), H((1, 1, 1)), 0), 1), 2)


def _rotax(p, ax, a):
    q = p.copy()
    i, j = [(1, 2), (0, 2), (0, 1)][ax]
    c, s = np.cos(a), np.sin(a)
    x, y = p[..., i], p[..., j]
    q[..., i] = c * x - s * y
    q[..., j] = s * x + c * y
    return q


def sdf(spec, i, p):
    n = spec["nodes"][i]
    op = n["op"]
    ins = n.get("in", [])

    def A(q):
        return sdf(spec, ins[0], q) if ins else np.full(q.shape[:-1], 1e9)

    def B(q):
        return sdf(spec, ins[1], q) if len(ins) > 1 else np.full(q.shape[:-1], 1e9)

    if op == "sdf.sphere":
        return _len(p) - val(n, "r")
    if op == "sdf.box":
        s = np.array([val(n, "sx"), val(n, "sy"), val(n, "sz")])
        r = val(n, "round") * s.min()
        q = np.abs(p) - (s - r)
        return _len(np.maximum(q, 0)) + np.minimum(q.max(-1), 0) - r
    if op == "sdf.torus":
        q = np.stack([_len(p[..., [0, 2]]) - val(n, "R"), p[..., 1]], -1)
        return _len(q) - val(n, "R") * val(n, "r")
    if op == "sdf.cylinder":
        dd = np.abs(np.stack([_len(p[..., [0, 2]]), p[..., 1]], -1)) - np.array([val(n, "r"), val(n, "h")])
        return np.minimum(dd.max(-1), 0) + _len(np.maximum(dd, 0))
    if op == "sdf.octa":
        return (np.abs(p).sum(-1) - val(n, "r")) * 0.57735027
    if op == "sdf.gyroid":
        s = val(n, "scale")
        gy = (np.sin(p * s) * np.cos(p[..., [1, 2, 0]] * s)).sum(-1)
        return np.maximum((np.abs(gy) - val(n, "thick")) / (s * 1.8), _len(p) - val(n, "bound"))
    if op == "sdf.plane":
        return p @ _plane_normal(val(n, "theta"), val(n, "phi")) + val(n, "offset")
    if op == "sdf.shell":
        return np.abs(A(p)) - val(n, "thick")
    if op == "sdf.invert":
        return -A(p)
    if op == "sdf.union":
        return _smin(A(p), B(p), val(n, "k"))
    if op == "sdf.subtract":
        return _smax(A(p), -B(p), val(n, "k"))
    if op == "sdf.intersect":
        return _smax(A(p), B(p), val(n, "k"))
    if op == "sdf.translate":
        return A(p - np.array([val(n, "x"), val(n, "y"), val(n, "z")]))
    if op == "sdf.rotate":
        return A(p @ _rot_matrix(val(n, "ax"), val(n, "ay"), val(n, "az")))
    if op == "sdf.scale":
        return A(p / val(n, "s")) * val(n, "s")
    if op == "sdf.polar":
        cnt = max(1, int(val(n, "count")))
        i0, i1 = [(1, 2), (0, 2), (0, 1)][int(val(n, "axis"))]
        sec = 2 * math.pi / cnt
        an = np.mod(np.arctan2(p[..., i1], p[..., i0]) + sec * 0.5, sec) - sec * 0.5
        rr = np.sqrt(p[..., i0] ** 2 + p[..., i1] ** 2)
        q = p.copy()
        q[..., i0] = rr * np.cos(an)
        q[..., i1] = rr * np.sin(an)
        return A(q)
    if op == "sdf.twist":
        ax = int(val(n, "axis"))
        return A(_rotax(p, ax, val(n, "amount") * p[..., ax])) * 0.6
    if op == "sdf.displace":
        return A(p) + val(n, "amp") * (_vn3(p * val(n, "freq") + val(n, "seed") * 1.37) - 0.5) * 2
    if op == "sdf.mirror":
        q = p.copy()
        for k in range(3):
            if int(val(n, "axes")) & (1 << k):
                q[..., k] = np.abs(q[..., k])
        return A(q)
    if op in PASS_THROUGH:
        return A(p)
    raise ValueError(op)


# --------------------------------------------------------------------------------------------
# Path 1: exact pieces (a union of convex polytopes, each a list of planes n.x + d <= 0)
# --------------------------------------------------------------------------------------------

class NotConvex(Exception):
    pass


def _octa_planes(r):
    k = 1 / math.sqrt(3)
    return [(np.array([sx, sy, sz]) * k, -r * k) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]


def _box_planes(s):
    out = []
    for k in range(3):
        for sgn in (-1.0, 1.0):
            nn = np.zeros(3)
            nn[k] = sgn
            out.append((nn, -s[k]))
    return out


def _xform(pieces, R=None, t=None, s=None):
    out = []
    for piece in pieces:
        q = []
        for nn, d in piece:
            if s is not None:
                d = d * s
            if R is not None:
                nn = R @ nn
            if t is not None:
                d = d - float(nn @ t)
            q.append((nn, d))
        out.append(q)
    return out


def _axis_rot(axis, a):
    """Rotation of a point by angle a in the polar fold's plane (i0 -> i1)."""
    i0, i1 = [(1, 2), (0, 2), (0, 1)][axis]
    R = np.eye(3)
    c, s = math.cos(a), math.sin(a)
    R[i0, i0], R[i0, i1], R[i1, i0], R[i1, i1] = c, -s, s, c
    return R


def pieces_of(spec, i):
    """The shape as a list of convex pieces, or raise NotConvex."""
    n = spec["nodes"][i]
    op = n["op"]
    ins = n.get("in", [])
    if op == "sdf.octa":
        return [_octa_planes(val(n, "r"))]
    if op == "sdf.box":
        if val(n, "round") > 0:
            raise NotConvex("rounded box")
        return [_box_planes([val(n, "sx"), val(n, "sy"), val(n, "sz")])]
    if op == "sdf.plane":
        return [[(_plane_normal(val(n, "theta"), val(n, "phi")), val(n, "offset"))]]
    if op in PASS_THROUGH:
        return pieces_of(spec, ins[0])
    if op in ("sdf.union", "sdf.intersect"):
        if val(n, "k") > SHARP_K:
            raise NotConvex("smooth " + op)
        a, b = pieces_of(spec, ins[0]), pieces_of(spec, ins[1])
        return a + b if op == "sdf.union" else [pa + pb for pa in a for pb in b]
    if op == "sdf.translate":
        return _xform(pieces_of(spec, ins[0]), t=np.array([val(n, "x"), val(n, "y"), val(n, "z")]))
    if op == "sdf.rotate":
        return _xform(pieces_of(spec, ins[0]), R=_rot_matrix(val(n, "ax"), val(n, "ay"), val(n, "az")))
    if op == "sdf.scale":
        return _xform(pieces_of(spec, ins[0]), s=val(n, "s"))
    if op == "sdf.mirror":
        pieces = pieces_of(spec, ins[0])
        for k in range(3):
            if int(val(n, "axes")) & (1 << k):
                e = np.zeros(3)
                e[k] = -1.0
                F = np.eye(3)
                F[k, k] = -1.0
                clipped = [pc + [(e, 0.0)] for pc in pieces]          # the child where x_k >= 0 ...
                pieces = clipped + _xform(clipped, R=F)              # ... and its reflection
        return pieces
    if op == "sdf.polar":
        cnt = max(1, int(val(n, "count")))
        axis = int(val(n, "axis"))
        pieces = pieces_of(spec, ins[0])
        if cnt == 1:
            return pieces
        h = math.pi / cnt
        i0, i1 = [(1, 2), (0, 2), (0, 1)][axis]
        w1, w2 = np.zeros(3), np.zeros(3)
        w1[i0], w1[i1] = -math.sin(h), math.cos(h)        # angle <= +h
        w2[i0], w2[i1] = -math.sin(h), -math.cos(h)       # angle >= -h
        wedge = [pc + [(w1, 0.0), (w2, 0.0)] for pc in pieces]
        out = []
        for k in range(cnt):
            out += _xform(wedge, R=_axis_rot(axis, 2 * h * k))
        return out
    raise NotConvex(op)


def _interior_point(H):
    """Chebyshev centre of {x : A x + b <= 0}; None if the piece is empty or a sliver."""
    A, b = H[:, :3], H[:, 3]
    norm = np.linalg.norm(A, axis=1)
    res = linprog(np.r_[0, 0, 0, -1], A_ub=np.c_[A, norm], b_ub=-b,
                  bounds=[(None, None)] * 3 + [(0, None)], method="highs")
    if not res.success or res.x[3] < 1e-4:
        return None
    return res.x[:3]


def polytope(planes, bound=1e3):
    """Vertices (rounded) of one convex piece, or None if empty. Raises if unbounded."""
    H = np.array([np.r_[nn / np.linalg.norm(nn), d / np.linalg.norm(nn)] for nn, d in planes])
    H = np.round(H, 9)
    H = np.unique(H, axis=0)
    H = np.vstack([H, np.array([np.r_[nn, -bound] for nn, _ in _box_planes([1, 1, 1])])])
    c = _interior_point(H)
    if c is None:
        return None, None
    V = HalfspaceIntersection(H, c).intersections
    if np.abs(V).max() > bound * 0.5:
        raise ValueError("an unbounded piece (a lone sdf.plane?)")
    V = np.unique(np.round(V, POS_ROUND + 2), axis=0)
    return V, H[:-6]


def _hull(V):
    h = ConvexHull(V)
    F = h.simplices.copy()
    # orient every triangle so its cross product points along the outward facet normal
    for k, (a, b, c) in enumerate(F):
        if np.cross(V[b] - V[a], V[c] - V[a]) @ h.equations[k, :3] < 0:
            F[k] = (a, c, b)
    return F, h.volume


def _inside(H, P, eps):
    return (P @ H[:, :3].T + H[:, 3] <= eps).all(-1)


def simplify_piece(H, V, vol, tol):
    """Greedily drop planes while the piece grows by at most tol of its volume (exact convex LOD)."""
    if tol <= 0:
        return H, V
    while len(H) > 4:
        best = None
        for j in range(len(H)):
            Hj = np.delete(H, j, axis=0)
            try:
                Vj, _ = polytope([(h[:3], h[3]) for h in Hj])
            except (ValueError, Exception):
                continue
            if Vj is None or len(Vj) < 4:
                continue
            gain = ConvexHull(Vj).volume / vol - 1
            if gain <= tol and (best is None or gain < best[0] - 1e-12):
                best = (gain, Hj, Vj)
        if best is None:
            break
        H, V = best[1], best[2]
    return H, V


def mesh_pieces(pieces, lod):
    """Triangles (V, F) of the union of the pieces at an LOD, with buried pieces/faces removed."""
    solid = []
    for planes in pieces:
        V, H = polytope(planes)
        if V is None or len(V) < 4:
            continue
        try:
            _, vol = _hull(V)
        except Exception:
            continue
        if vol < 1e-6:
            continue
        solid.append([H, V, vol])
    largest = max((s[2] for s in solid), default=0.0)
    solid = [s for s in solid if s[2] >= LOD_DROP_FRAC[lod] * largest]
    for s in solid:
        s[0], s[1] = simplify_piece(s[0], s[1], s[2], LOD_PLANE_TOL[lod])
    # drop pieces contained in another (identical pieces: keep the first)
    keep = []
    for a, (Ha, Va, _) in enumerate(solid):
        buried = False
        for b, (Hb, Vb, _) in enumerate(solid):
            if a != b and _inside(Hb, Va, 1e-6).all():
                if not (_inside(Ha, Vb, 1e-6).all() and b > a):
                    buried = True
                    break
        if not buried:
            keep.append((Ha, Va))
    Vs, Fs, base = [], [], 0
    for a, (Ha, Va) in enumerate(keep):
        F, _ = _hull(Va)
        others = [Hb for b, (Hb, _) in enumerate(keep) if b != a]
        out = []
        for tri in F:
            P = Va[tri]
            nrm = np.cross(P[1] - P[0], P[2] - P[0])
            nrm /= np.linalg.norm(nrm) + 1e-12
            probe = np.vstack([P, P.mean(0)]) * 0.98 + P.mean(0) * 0.02 + nrm * 1e-3
            # buried: every probe just outside this face is strictly inside some other piece
            if any(_inside(Hb, probe, -1e-6).all() for Hb in others):
                continue
            out.append(tri)
        if out:
            Vs.append(Va)
            Fs.append(np.array(out) + base)
            base += len(Va)
    if not Fs:
        raise ValueError("no geometry")
    return np.vstack(Vs), np.vstack(Fs).astype(np.int64), len(keep)


# --------------------------------------------------------------------------------------------
# Path 2: marching cubes + quadric decimation
# --------------------------------------------------------------------------------------------

def mesh_mc(spec, targets):
    from skimage.measure import marching_cubes
    import fast_simplification
    B = float(spec.get("bounds", MC_DEFAULT["bounds"]))
    res = int(spec.get("res", MC_DEFAULT["res"]))
    xs = np.linspace(-B, B, res)
    P = np.stack(np.meshgrid(xs, xs, xs, indexing="ij"), -1)
    v = sdf(spec, root_of(spec), P).astype(np.float32)
    for sl in (np.s_[0], np.s_[-1], np.s_[:, 0], np.s_[:, -1], np.s_[:, :, 0], np.s_[:, :, -1]):
        v[sl] = np.maximum(v[sl], 1e-3)          # close the volume at the grid edge
    sp = xs[1] - xs[0]
    V, F, _, _ = marching_cubes(v, 0.0, spacing=(sp, sp, sp))
    V = V - B
    # skimage's winding already gives an outward cross product (positive signed volume, validated)
    lods = []
    Vc, Fc = V.astype(np.float32), F.astype(np.int32)
    for tgt in targets:
        for _ in range(10):  # one pass stalls on sharp features; iterate until target or no progress
            n0 = len(Fc)
            if n0 <= tgt:
                break
            Vc, Fc = fast_simplification.simplify(Vc, Fc, target_count=int(tgt), agg=10)
            if len(Fc) >= n0 * 0.98:
                break
        lods.append((Vc.astype(np.float64), Fc.astype(np.int64)))
    return lods, len(F)


# --------------------------------------------------------------------------------------------
# Building, validating and writing one spec
# --------------------------------------------------------------------------------------------

_BUILT = {}


def build(spec):
    """{'path', 'lods': [(V, F)], 'pieces', 'mc_tris', 'ms'} for one spec (memoised on the spec's content)."""
    key = json.dumps(spec, sort_keys=True)
    if key not in _BUILT:
        _BUILT[key] = _build(spec)
    return _BUILT[key]


def _build(spec):
    t0 = time.perf_counter()
    info = {}
    try:
        pieces = pieces_of(spec, root_of(spec))
        info["path"] = "pieces"
        lods = []
        for lod in range(LODS):
            V, F, kept = mesh_pieces(pieces, lod)
            lods.append((V, F))
            info.setdefault("pieces", []).append(kept)
        info["raw_pieces"] = len(pieces)
    except NotConvex as why:
        info["path"] = f"marching cubes ({why})"
        targets = spec.get("lod_tris", MC_DEFAULT["lod_tris"])
        lods, info["mc_tris"] = mesh_mc(spec, targets)
    info["ms"] = (time.perf_counter() - t0) * 1e3
    info["lods"] = lods
    return info


def flat(V, F):
    """Split vertices: (positions, normals, uvs) per corner, positions rounded first."""
    P = np.round(V[F].reshape(-1, 3), POS_ROUND)
    T = P.reshape(-1, 3, 3)
    N = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    ln = np.linalg.norm(N, axis=1)
    ok = ln > 1e-10
    T, N, ln = T[ok], N[ok], ln[ok]
    N = np.round(N / ln[:, None], 5)
    N = np.repeat(N, 3, axis=0)
    P = T.reshape(-1, 3)
    dom = np.abs(N).argmax(1)
    pairs = np.array([(2, 1), (0, 2), (0, 1)])[dom]
    UV = np.round(np.stack([P[np.arange(len(P)), pairs[:, 0]], P[np.arange(len(P)), pairs[:, 1]]], 1) * 0.5, 5)
    return P, N, UV


def weld(P, N, UV):
    """Share a corner between triangles only where position, normal and uv all match: the triangles of
    one planar facet share vertices, every facet keeps its own (the mesh stays faceted)."""
    R = np.hstack([P, N, UV])
    _, first, inv = np.unique(R, axis=0, return_index=True, return_inverse=True)
    order = np.argsort(first)                    # keep first-use order
    rank = np.empty_like(order)
    rank[order] = np.arange(len(order))
    keep = first[order]
    return P[keep], N[keep], UV[keep], rank[inv.ravel()]


def validate(name, lod, V, F, path):
    errs = []
    if len(F) * 3 > 65535:
        errs.append(f"{name} LOD{lod}: {len(F) * 3} split vertices exceed 16-bit indices")
    T = V[F]
    cr = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    if (np.linalg.norm(cr, axis=1) < 1e-10).sum() > len(F) * 0.02:
        errs.append(f"{name} LOD{lod}: more than 2% degenerate triangles")
    if path == "pieces":
        return errs   # pieces are hulls (closed by construction); buried faces are removed on purpose
    # marching cubes: closed and consistently wound - every directed edge a->b is matched by one b->a
    # (an edge where two sheets pinch is used four times, which is fine; an odd or one-sided edge is not)
    d = np.vstack([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
    key = np.sort(d, axis=1)
    sign = np.where(d[:, 0] < d[:, 1], 1, -1)
    uk, inv = np.unique(key, axis=0, return_inverse=True)
    bal = np.zeros(len(uk), np.int64)
    np.add.at(bal, inv.ravel(), sign)
    bad = int((bal != 0).sum())
    if bad:
        errs.append(f"{name} LOD{lod}: {bad} open or mis-wound edges (a hole or a flipped triangle)")
    vol = float((T[:, 0] * np.cross(T[:, 1], T[:, 2])).sum() / 6)
    if vol <= 0:
        errs.append(f"{name} LOD{lod}: signed volume {vol:.3f} <= 0 (faces point inward)")
    return errs


def mesh_text(P, N, UV, I, name):
    """Unity Mesh YAML, following the donor asset as author_spindle_lod_meshes.mesh_text does."""
    vbuf = bytearray()
    for p, n, uv in zip(P, N, UV):
        vbuf += struct.pack("<3f3f2f", *p, *n, *uv)
    ibuf = struct.pack("<%dH" % len(I), *[int(i) for i in I])
    lo, hi = P.min(0), P.max(0)
    ctr, ext = (lo + hi) / 2, (hi - lo) / 2
    v3 = "{x: %s, y: %s, z: %s}"
    c3, e3 = v3 % tuple(fmt(c) for c in ctr), v3 % tuple(fmt(c) for c in ext)
    used = {0: (0, 3), 1: (12, 3), 4: (24, 2)}
    channels = "".join("    - stream: 0\n      offset: %d\n      format: 0\n      dimension: %d\n"
                       % used.get(i, (0, 0)) for i in range(14))
    with open(os.path.join(REPO, DONOR_MESH)) as fh:
        donor = fh.read()
    head = donor[:donor.index("  m_SubMeshes:")].replace("m_Name: Prism", f"m_Name: {name}")
    tail = donor[donor.index("  m_CompressedMesh:"):]
    tail = re.sub(r"  m_LocalAABB:\n    m_Center: .*\n    m_Extent: .*\n",
                  f"  m_LocalAABB:\n    m_Center: {c3}\n    m_Extent: {e3}\n", tail)
    return (head
            + "  m_SubMeshes:\n  - serializedVersion: 2\n    firstByte: 0\n"
            + f"    indexCount: {len(I)}\n    topology: 0\n    baseVertex: 0\n"
            + f"    firstVertex: 0\n    vertexCount: {len(P)}\n    localAABB:\n"
            + f"      m_Center: {c3}\n      m_Extent: {e3}\n"
            + donor[donor.index("  m_Shapes:"):donor.index("  m_IndexBuffer:")]
            + f"  m_IndexBuffer: {ibuf.hex()}\n"
            + "  m_VertexData:\n    serializedVersion: 3\n"
            + f"    m_VertexCount: {len(P)}\n    m_Channels:\n{channels}"
            + f"    m_DataSize: {len(vbuf)}\n    _typelessdata: {vbuf.hex()}\n"
            + tail)


MESH_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 4300000
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def outputs(specs):
    files = {f"{OUT_DIR}.meta": FOLDER_META.format(guid=FOLDER_GUID)}
    errs, rows, panels = [], [], []
    seen = {FOLDER_GUID: "folder"}
    for spec in specs:
        e = spec_errors(spec)
        if e:
            errs += e
            continue
        for k in range(LODS):
            g = spec["guids"][f"lod{k}"]
            if g in seen:
                errs.append(f"{spec['name']}: guid {g} also used by {seen[g]}")
            seen[g] = spec["name"]
        info = build(spec)
        row = dict(name=spec["name"], path=info["path"], ms=info["ms"], nodes=len(spec["nodes"]),
                   pieces=info.get("pieces"), raw_pieces=info.get("raw_pieces"), mc_tris=info.get("mc_tris"),
                   lods=[])
        prev = None
        for k, (V, F) in enumerate(info["lods"]):
            errs += validate(spec["name"], k, V, F, "pieces" if info["path"] == "pieces" else "mc")
            P, N, UV = flat(V, F)
            Pw, Nw, UVw, I = weld(P, N, UV)
            nm = f"{spec['name']}_LOD{k}"
            text = mesh_text(Pw, Nw, UVw, I, nm)
            # same geometry as the LOD above (the same corners and triangle count, whatever qhull's order)
            sig = (len(I), np.unique(np.round(Pw, POS_ROUND), axis=0).tobytes())
            same = prev is not None and sig == prev[1]
            if same:      # an LOD that equals the one above it is not written: use that one
                row["lods"].append(dict(tris=len(I) // 3, verts=len(Pw), bytes=0, same_as=prev[0]))
            else:
                files[f"{OUT_DIR}/{nm}.asset"] = text
                files[f"{OUT_DIR}/{nm}.asset.meta"] = MESH_META.format(guid=spec["guids"][f"lod{k}"])
                row["lods"].append(dict(tris=len(I) // 3, verts=len(Pw), bytes=len(text.encode())))
                prev = (k, sig)
            panels.append((nm + (f" (= LOD{prev[0]})" if same else ""), P, len(I) // 3))
        rows.append(row)
    return files, errs, rows, panels


def guid_collisions(specs):
    """Guids this tool owns that some OTHER file in the repo also carries."""
    mine = [FOLDER_GUID] + [s["guids"][f"lod{k}"] for s in specs for k in range(LODS) if "guids" in s]
    args = ["git", "-C", REPO, "grep", "-l", "-F"]
    for g in mine:
        args += ["-e", f"guid: {g}"]
    try:
        out = subprocess.run(args, capture_output=True, text=True, timeout=120).stdout.split()
    except Exception:
        return []
    return [p for p in out if not p.startswith(OUT_DIR)]


# --------------------------------------------------------------------------------------------
# Contact sheet (outside the repo)
# --------------------------------------------------------------------------------------------

def render(P, size=220, yaw=0.6, pitch=0.45):
    from PIL import Image
    cy, sy, cp, spp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    Ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    Rx = np.array([[1, 0, 0], [0, cp, -spp], [0, spp, cp]])
    W = P @ Ry.T @ Rx.T
    W = W - (W.max(0) + W.min(0)) / 2
    sc = 0.45 * size / (np.abs(W[:, :2]).max() + 1e-9)
    T = W.reshape(-1, 3, 3)
    # camera looks down -z toward the origin from +z; screen y up. Unity winding: cross points outward.
    # Projected to a right-handed screen (x right, y up) a front face's cross has z > 0.
    N = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0])
    N /= np.linalg.norm(N, axis=1, keepdims=True) + 1e-12
    L = np.array([-0.45, 0.6, 0.65])
    L /= np.linalg.norm(L)
    # a z-buffer, so a back face (red) shows only through a real hole, never from sort order
    img = np.zeros((size, size, 3)) + np.array([14, 16, 28]) / 255
    zbuf = np.full((size, size), -np.inf)
    S = np.stack([size / 2 + T[..., 0] * sc, size / 2 - T[..., 1] * sc, T[..., 2]], -1)
    for t in range(len(T)):
        front = N[t, 2] > 0
        sh = 0.2 + 0.75 * max(0.0, float(N[t] @ L)) if front else 0.6
        col = np.array((0.55, 0.78, 1.0) if front else (1.0, 0.1, 0.1)) * sh
        (x0, y0, z0), (x1, y1, z1), (x2, y2, z2) = S[t]
        lo_x, hi_x = int(max(0, math.floor(min(x0, x1, x2)))), int(min(size - 1, math.ceil(max(x0, x1, x2))))
        lo_y, hi_y = int(max(0, math.floor(min(y0, y1, y2)))), int(min(size - 1, math.ceil(max(y0, y1, y2))))
        if lo_x > hi_x or lo_y > hi_y:
            continue
        den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(den) < 1e-12:
            continue
        X, Y = np.meshgrid(np.arange(lo_x, hi_x + 1) + 0.5, np.arange(lo_y, hi_y + 1) + 0.5)
        w0 = ((y1 - y2) * (X - x2) + (x2 - x1) * (Y - y2)) / den
        w1 = ((y2 - y0) * (X - x2) + (x0 - x2) * (Y - y2)) / den
        w2 = 1 - w0 - w1
        inside = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
        z = w0 * z0 + w1 * z1 + w2 * z2 - (0 if front else 1e-3)
        sub = zbuf[lo_y:hi_y + 1, lo_x:hi_x + 1]
        win = inside & (z > sub)
        sub[win] = z[win]
        img[lo_y:hi_y + 1, lo_x:hi_x + 1][win] = np.minimum(col, 1)
    img = Image.fromarray((img * 255).astype(np.uint8))
    return img


def contact_sheet(panels, path):
    from PIL import Image, ImageDraw
    cell = 220
    rows = (len(panels) + 2) // 3
    img = Image.new("RGB", (cell * 3, rows * (cell + 18)), (8, 8, 14))
    d = ImageDraw.Draw(img)
    for i, (nm, P, tris) in enumerate(panels):
        x, y = cell * (i % 3), (cell + 18) * (i // 3)
        img.paste(render(P, cell), (x, y + 18))
        d.text((x + 4, y + 3), f"{nm}  {tris} tris", fill=(225, 225, 225))
    img.save(path)


# --------------------------------------------------------------------------------------------
# Entry points
# --------------------------------------------------------------------------------------------

def orphans(files):
    """Files under OUT_DIR this run does not produce (a renamed spec, an LOD that now equals the one above)."""
    root = os.path.join(REPO, OUT_DIR)
    if not os.path.isdir(root):
        return []
    return sorted(f"{OUT_DIR}/{f}" for f in os.listdir(root) if f"{OUT_DIR}/{f}" not in files)


def drift(files):
    out = orphans(files)
    for rel, text in files.items():
        path = os.path.join(REPO, rel)
        on_disk = open(path, newline="").read() if os.path.exists(path) else None
        if on_disk != text:
            out.append(rel)
    return out


def report(rows):
    print(f"{'spec':22} {'path':44} {'ms':>7}  LOD0 / LOD1 / LOD2 tris   bytes")
    total = 0
    for r in rows:
        tris = " / ".join(str(l["tris"]) + ("=" if l.get("same_as") is not None else "") for l in r["lods"])
        b = sum(l["bytes"] for l in r["lods"])
        total += b
        extra = f" pieces {r['raw_pieces']}->{r['pieces']}" if r["pieces"] else f" mc {r['mc_tris']}"
        print(f"{r['name']:22} {(r['path'] + extra)[:44]:44} {r['ms']:7.1f}  {tris:24} {b}")
    print(f"total mesh asset bytes: {total}")


def self_test():
    specs = load_specs()
    failures = 0
    files, errs, _, _ = outputs(specs)
    if errs:
        print("shipped specs do not validate:", errs)
        return 1
    if drift(files):
        print("self-test needs the committed assets current first (run without --check)")
        return 1
    # 1. Negative control: a perturbed spec must fail --check (the drift gate).
    bad = json.loads(json.dumps(specs))
    bad[0]["nodes"][0]["values"][next(iter(bad[0]["nodes"][0]["values"]))] *= 1.05
    f2, _, _, _ = outputs(bad)
    failures += not drift(f2)
    # 2. A flipped triangle in a marching-cubes mesh must be caught as a hole/inward mesh.
    gy = [s for s in specs if "lod_tris" in s][0]
    V, F = build(gy)["lods"][2]
    F2 = F.copy()
    F2[0] = F2[0][[0, 2, 1]]
    failures += not any("open or mis-wound" in e for e in validate("t", 2, V, F2, "mc"))
    # 3. A dropped triangle (a hole) must be caught.
    failures += not any("open or mis-wound" in e for e in validate("t", 2, V, F[1:], "mc"))
    # 4. A whole mesh turned inside out must be caught.
    failures += not any("<= 0" in e for e in validate("t", 2, V, F[:, [0, 2, 1]], "mc"))
    # 5. A duplicated guid must be caught.
    dup = json.loads(json.dumps(specs))
    dup[1]["guids"]["lod0"] = dup[0]["guids"]["lod0"]
    failures += not any("also used by" in e for e in outputs(dup)[1])
    # 6. A smooth union must leave the exact path; a sharp one must stay on it.
    sm = {"name": "T", "nodes": [{"id": 0, "op": "sdf.octa", "in": [], "values": {"r": 1}},
                                 {"id": 1, "op": "sdf.box", "in": [], "values": {"sx": 1, "sy": 1, "sz": 1}},
                                 {"id": 2, "op": "sdf.union", "in": [0, 1], "values": {"k": 0.3}}]}
    try:
        pieces_of(sm, 2)
        failures += 1
    except NotConvex:
        pass
    sm["nodes"][2]["values"]["k"] = 0.001
    failures += len(pieces_of(sm, 2)) != 2
    # 7. The exact path agrees with the SDF: no LOD0 vertex lies outside the shape, and inside/outside of
    #    20k random points by the pieces matches the SDF's sign (this is what proves the mirror/polar clipping).
    rng = np.random.default_rng(7)
    for s in specs:
        try:
            pieces = pieces_of(s, root_of(s))
        except NotConvex:
            continue
        V, F = build(s)["lods"][0]
        out = sdf(s, root_of(s), V[np.unique(F)]).max()
        lo, hi = V.min(0) - 0.2, V.max(0) + 0.2
        X = rng.uniform(lo, hi, size=(20000, 3))
        by_sdf = sdf(s, root_of(s), X) < 0
        by_pieces = np.zeros(len(X), bool)
        for planes in pieces:
            H = np.array([np.r_[nn, d] for nn, d in planes])
            by_pieces |= _inside(H, X, 0.0)
        agree = (by_sdf == by_pieces).mean()
        if out > 1e-3 or agree < 0.999:
            print(f"  {s['name']}: vertex outside by {out:.4f}, inside/outside agreement {agree:.4f}")
            failures += 1
    # 8. The agreement test itself fires: a mirror copied WITHOUT clipping the child to x >= 0 first (a tilted
    #    bar that straddles the fold) disagrees with the SDF; the clipped copies agree.
    bar = {"name": "T", "nodes": [{"id": 0, "op": "sdf.box", "in": [], "values": {"sx": 2.0, "sy": 0.3, "sz": 0.3}},
                                  {"id": 1, "op": "sdf.rotate", "in": [0], "values": {"az": 0.7}},
                                  {"id": 2, "op": "sdf.translate", "in": [1], "values": {"x": -0.3}},
                                  {"id": 3, "op": "sdf.mirror", "in": [2], "values": {"axes": 1}}]}
    X = rng.uniform(-3, 3, size=(20000, 3))
    by_sdf = sdf(bar, 3, X) < 0

    def inside_any(pcs):
        r = np.zeros(len(X), bool)
        for planes in pcs:
            r |= _inside(np.array([np.r_[nn, d] for nn, d in planes]), X, 0.0)
        return r
    child = pieces_of(bar, 2)
    bad = (by_sdf == inside_any(child + _xform(child, R=np.diag([-1.0, 1.0, 1.0])))).mean()
    good = (by_sdf == inside_any(pieces_of(bar, 3))).mean()
    failures += bad > 0.995 or good < 0.999
    print("self-test:", "OK (8 negative controls fire; the exact pieces match the SDF inside/outside)"
          if failures == 0 else f"FAILED ({failures})")
    return 1 if failures else 0


def new_guids():
    gs = [uuid.uuid4().hex for _ in range(LODS)]
    args = ["git", "-C", REPO, "grep", "-l", "-F"]
    for g in gs:
        args += ["-e", g]
    hits = subprocess.run(args, capture_output=True, text=True).stdout.strip()
    if hits:
        print("collision, run again:", hits)
        return 1
    print(json.dumps({f"lod{k}": g for k, g in enumerate(gs)}))
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--new-guids", action="store_true")
    ap.add_argument("--sheet", default=os.path.join(tempfile.gettempdir(), "genome_crystals_sheet.png"))
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    if args.new_guids:
        return new_guids()
    specs = load_specs()
    files, errs, rows, panels = outputs(specs)
    errs += [f"guid also used outside {OUT_DIR}: {p}" for p in guid_collisions(specs)]
    if args.report:
        report(rows)
        return 0
    if errs:
        for e in errs:
            print("ERROR:", e)
        return 1
    changed = drift(files)
    if args.check:
        if changed:
            print("DRIFT:", ", ".join(changed))
            return 1
        print(f"genome crystal meshes ({len(specs)} specs, {len(files)} files): OK")
        return 0
    for rel in changed:
        path = os.path.join(REPO, rel)
        if rel not in files:
            os.remove(path)
            continue
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", newline="\n") as fh:
            fh.write(files[rel])
    sheet = os.path.abspath(args.sheet)
    if sheet.startswith(REPO + os.sep):
        print("ERROR: the contact sheet goes outside the repo")
        return 1
    contact_sheet(panels, sheet)
    print("wrote:" if changed else "no change:", ", ".join(changed) if changed else "all files current")
    print("contact sheet:", sheet)
    report(rows)
    return 0


if __name__ == "__main__":
    sys.exit(main())
