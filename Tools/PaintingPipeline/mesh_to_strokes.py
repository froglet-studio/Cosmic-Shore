"""Camp B: mesh -> flight strokes converter.

Turns a real 3D scan into the strokes a vessel can fly: section contours (the
engraving look — true cross-sections of the form) + sharp-feature lines (ridges
and valleys: mane locks, feather edges on a sculpture). Output satisfies the
painting conventions: base at y=0, front +Z, flyable segments, Jade/Ruby/Gold.
"""
import math
import numpy as np
import trimesh

def _polyline_len(p):
    return float(np.linalg.norm(np.diff(p, axis=0), axis=1).sum())

def _rdp(points, eps):
    """Douglas-Peucker simplification (iterative)."""
    pts = np.asarray(points, dtype=np.float64)
    n = len(pts)
    if n < 3: return pts
    keep = np.zeros(n, dtype=bool); keep[0] = keep[-1] = True
    stack = [(0, n - 1)]
    while stack:
        a, b = stack.pop()
        if b <= a + 1: continue
        seg = pts[b] - pts[a]
        L = np.linalg.norm(seg)
        if L < 1e-12:
            d = np.linalg.norm(pts[a+1:b] - pts[a], axis=1)
        else:
            v = seg / L
            w = pts[a+1:b] - pts[a]
            proj = w @ v
            d = np.linalg.norm(w - np.outer(proj, v), axis=1)
        i = int(np.argmax(d))
        if d[i] > eps:
            m = a + 1 + i
            keep[m] = True
            stack.append((a, m)); stack.append((m, b))
    return pts[keep]

def _min_seg(pts, min_seg):
    out = [pts[0]]
    for p in pts[1:-1]:
        if np.linalg.norm(p - out[-1]) >= min_seg: out.append(p)
    if np.linalg.norm(pts[-1] - out[-1]) >= min_seg * 0.5: out.append(pts[-1])
    else: out[-1] = pts[-1]
    return np.asarray(out)

def _max_seg(pts, max_seg):
    out = [pts[0]]
    for p in pts[1:]:
        prev = out[-1]
        d = np.linalg.norm(p - prev)
        n = max(1, int(math.ceil(d / max_seg)))
        for k in range(1, n + 1):
            out.append(prev + (p - prev) * (k / n))
    return np.asarray(out)

def load_and_orient(path, up='y', front='z', flip_up=False, flip_front=False):
    """Load a mesh/scene, weld, and axis-permute so up->+Y and front->+Z."""
    m = trimesh.load(path, force='mesh')
    m.remove_unreferenced_vertices()
    axes = {'x': 0, 'y': 1, 'z': 2}
    ui, fi = axes[up], axes[front]
    ri = ({0, 1, 2} - {ui, fi}).pop()
    V = m.vertices[:, [ri, ui, fi]].copy()
    if flip_up: V[:, 1] = -V[:, 1]
    if flip_front: V[:, 2] = -V[:, 2]
    m = trimesh.Trimesh(vertices=V, faces=m.faces, process=True)
    return m

def normalize(m, W):
    """Center on x/z, base at y=0, scale so the largest horizontal extent is ~0.9*W."""
    lo, hi = m.bounds
    size = hi - lo
    scale = 0.9 * W / max(size[0], size[2])
    V = m.vertices.copy()
    V[:, 0] -= (lo[0] + hi[0]) / 2
    V[:, 2] -= (lo[2] + hi[2]) / 2
    V[:, 1] -= lo[1]
    V *= scale
    return trimesh.Trimesh(vertices=V, faces=m.faces, process=False)

def section_contours(m, n_slices, axis=1, min_len_frac=0.05, W=1000.0):
    """Horizontal (or other-axis) section contours — the engraving rings."""
    lo, hi = m.bounds
    span = hi[axis] - lo[axis]
    normal = np.zeros(3); normal[axis] = 1.0
    heights = lo[axis] + span * (np.arange(n_slices) + 0.5) / n_slices
    out = []
    for h in heights:
        origin = np.zeros(3); origin[axis] = h
        sec = m.section(plane_origin=origin, plane_normal=normal)
        if sec is None: continue
        for entity_pts in sec.discrete:   # list of (n,3) polylines in world space
            if len(entity_pts) < 3: continue
            if _polyline_len(entity_pts) < min_len_frac * W: continue
            out.append(np.asarray(entity_pts))
    return out

def feature_lines(m, angle_deg=38.0, min_len_frac=0.03, W=1000.0):
    """Chain sharp edges (dihedral angle over threshold) into ridge/valley polylines."""
    ang = np.degrees(m.face_adjacency_angles)
    sharp = m.face_adjacency_edges[ang > angle_deg]
    if len(sharp) == 0: return []
    # build adjacency: vertex -> connected sharp vertices
    adj = {}
    for a, b in sharp:
        adj.setdefault(int(a), set()).add(int(b))
        adj.setdefault(int(b), set()).add(int(a))
    unused = {tuple(sorted(e)) for e in sharp.tolist()}
    V = m.vertices
    chains = []
    while unused:
        e = unused.pop()
        chain = [e[0], e[1]]
        # extend forward then backward
        for end in (1, 0):
            while True:
                tip = chain[-1] if end else chain[0]
                nxts = [v for v in adj.get(tip, ()) if tuple(sorted((tip, v))) in unused]
                if not nxts: break
                v = nxts[0]
                unused.discard(tuple(sorted((tip, v))))
                if end: chain.append(v)
                else: chain.insert(0, v)
        pts = V[chain]
        if _polyline_len(pts) >= min_len_frac * W:
            chains.append(pts)
    return chains

def to_strokes(contours, features, W, contour_dom='Gold', feature_dom='Ruby',
               max_strokes=260, max_points=7000, eps_frac=0.004,
               min_seg_frac=0.010, max_seg_frac=0.10):
    """Simplify, budget, order broad->fine, and tag domains."""
    eps = eps_frac * W
    ms = max(6.0, min_seg_frac * W)
    Ms = max_seg_frac * W
    def prep(polys):
        out = []
        for p in polys:
            q = _rdp(p, eps)
            q = _min_seg(q, ms)
            q = _max_seg(q, Ms)
            if len(q) >= 2 and _polyline_len(q) > 2.2 * ms:
                out.append(q)
        return out
    cont = prep(contours); feat = prep(features)
    # budget: keep longest first within each family, contours before features
    cont.sort(key=_polyline_len, reverse=True)
    feat.sort(key=_polyline_len, reverse=True)
    strokes = []
    pts_used = 0
    def take(polys, dom, tag, cap):
        nonlocal pts_used
        n = 0
        for p in polys:
            if len(strokes) >= max_strokes or pts_used + len(p) > max_points: break
            if n >= cap: break
            strokes.append((dom, tag + f" {n+1}", p))
            pts_used += len(p); n += 1
    take(cont, contour_dom, "Contour", max_strokes * 2 // 3)
    take(feat, feature_dom, "Feature", max_strokes)
    return strokes

def stats(strokes, W):
    allp = np.vstack([p for _, _, p in strokes])
    segs = np.concatenate([np.linalg.norm(np.diff(p, axis=0), axis=1) for _, _, p in strokes])
    bb = allp.max(0) - allp.min(0)
    return dict(strokes=len(strokes), points=len(allp),
                pathW=float(segs.sum() / W), minSeg=float(segs.min()), maxSeg=float(segs.max()),
                bboxW=tuple(round(float(e) / W, 2) for e in bb),
                minY=float(allp[:, 1].min()))

if __name__ == '__main__':
    # smoke test on a synthetic mesh
    m = trimesh.creation.torus(major_radius=300, minor_radius=120)
    m = normalize(m, 1000)
    cont = section_contours(m, 24, axis=1, W=1000)
    feat = feature_lines(m, 38, W=1000)
    s = to_strokes(cont, feat, 1000)
    print("torus smoke:", stats(s, 1000))
