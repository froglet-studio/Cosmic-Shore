#!/usr/bin/env python3
"""
Repair polygons Unity's FBX importer DISCARDS as self-intersecting.

Unity does not merely warn about a self-intersecting polygon - it drops the whole
face, so the warning is a report of MISSING GEOMETRY, not console noise. In the
Dolphin's rig every offender is the same modelling artifact: a 13-gon carrying a
ZERO-LENGTH edge, i.e. two adjacent corners that index different vertices sitting
at the identical position. Any robust simplicity test calls that non-simple, so
Unity throws away all thirteen sides over one corner that occupies no space.

SCOPE: only the polygons Unity DISCARDS are repaired. The same file also carries
zero-area triangles (two of three corners coincident). Unity keeps those - they are
not self-intersecting, they simply render nothing - so they are reported and left
alone. Repairing one would leave a 2-gon, and widening the blast radius of a fix
beyond what was reported is how an art file acquires changes nobody asked for.

The repair is the minimal one: drop the redundant CORNER, leaving a 12-gon whose
outline, vertex positions and UVs are unchanged. Nothing is moved, nothing is
added, and no vertex is merged (both vertices stay - they are used by four faces
each).

What has to move with it:
  PolygonVertexIndex   one entry removed per repaired corner
  LayerElementNormal   ByPolygonVertex Direct  -> 3 floats removed
  LayerElementUV       ByPolygonVertex IndexToDirect -> 1 UVIndex entry removed
  LayerElementMaterial ByPolygon -> UNCHANGED (polygon count does not change)
  Edges + Smoothing    REBUILT (see below)

Edges is an array of corner positions, one per unique undirected edge. Removing a
corner shifts every later position, so the array is rebuilt rather than patched.
That is safe because the rebuild rule was PROVEN against the shipped file first:
emitting the first occurrence of each undirected vertex pair in polygon-corner
order reproduces the original Edges array exactly - same length, same order, same
values. Smoothing (ByEdge, Direct, parallel to Edges) is carried across by vertex
pair, with a position-pair fallback for the one edge per repair that is newly
formed - geometrically identical to the real edge it replaces, because the corner
that was dropped occupied the same point as the one that remains.

Which of the two coincident corners is dropped is a MEASUREMENT, not a coin flip:
both carry the same UV, so the only thing at stake is the normal, and the corner
whose normal sits farther from the polygon's own Newell normal is the one removed.

Run with --check in CI: it re-reads the shipped file and fails if any polygon is
still self-intersecting. Idempotent - a repaired file reports "already clean".
"""

import argparse, math, os, shutil, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary as F

DEFAULT_TARGETS = ["Assets/_Models/Vessel Models/dolphin_shapekey_with_animations.fbx"]


def _dec(b):
    return b.decode("utf8", "replace") if isinstance(b, bytes) else b


def _name(node):
    return _dec(node.props[1][1]).split("\x00")[0] if len(node.props) > 1 else ""


def _sub(node):
    return _dec(node.props[2][1]) if len(node.props) > 2 else ""


def _layer_str(elem, key):
    child = elem.first(key)
    return _dec(child.props[0][1]) if child else ""


def polygons(pi):
    """Split a PolygonVertexIndex into [(start, [vertex ids...]), ...]."""
    out, cur, start = [], [], 0
    for k, x in enumerate(pi):
        if not cur:
            start = k
        if x < 0:
            cur.append(~x)
            out.append((start, cur))
            cur = []
        else:
            cur.append(x)
    if cur:
        raise ValueError("PolygonVertexIndex does not end on a polygon terminator")
    return out


def newell(points):
    nx = ny = nz = 0.0
    n = len(points)
    for i in range(n):
        a, b = points[i], points[(i + 1) % n]
        nx += (a[1] - b[1]) * (a[2] + b[2])
        ny += (a[2] - b[2]) * (a[0] + b[0])
        nz += (a[0] - b[0]) * (a[1] + b[1])
    return (nx, ny, nz)


def project(points):
    n = newell(points)
    axis = max(range(3), key=lambda k: abs(n[k]))
    u, v = [k for k in range(3) if k != axis]
    return [(p[u], p[v]) for p in points]


def _orient(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def self_intersecting(points):
    """True when the polygon is not simple: a proper edge crossing, or a
    non-adjacent edge pair meeting at a point (which is what a zero-length edge
    or a pinched corner produces)."""
    n = len(points)
    if n < 4:
        return False
    q = project(points)
    scale = max((max(abs(c) for c in p) for p in q), default=1.0) or 1.0
    eps = 1e-12 * scale * scale
    for i in range(n):
        a, b = q[i], q[(i + 1) % n]
        for j in range(i + 1, n):
            if (j + 1) % n == i or (i + 1) % n == j:
                continue
            c, d = q[j], q[(j + 1) % n]
            d1, d2 = _orient(c, d, a), _orient(c, d, b)
            d3, d4 = _orient(a, b, c), _orient(a, b, d)
            if ((d1 > 0) != (d2 > 0)) and ((d3 > 0) != (d4 > 0)):
                return True
            for s, t, p in ((a, b, c), (a, b, d), (c, d, a), (c, d, b)):
                if abs(_orient(s, t, p)) <= eps and \
                   min(s[0], t[0]) - eps <= p[0] <= max(s[0], t[0]) + eps and \
                   min(s[1], t[1]) - eps <= p[1] <= max(s[1], t[1]) + eps:
                    return True
    return False


def mesh_geometries(nodes):
    objs = [n for n in nodes if n.name == "Objects"]
    if not objs:
        return []
    return [o for o in objs[0].children
            if o.name == "Geometry" and _sub(o) == "Mesh" and o.first("Vertices")]


def scan(geo):
    """Return (polygon list, indices of self-intersecting polygons, degeneracies)."""
    verts = geo.first("Vertices").props[0][1]
    pi = geo.first("PolygonVertexIndex").props[0][1]
    polys = polygons(pi)

    def pt(i):
        return (verts[3 * i], verts[3 * i + 1], verts[3 * i + 2])

    bad, degen = [], {}
    for pidx, (start, idx) in enumerate(polys):
        pts = [pt(i) for i in idx]
        zero = [i for i in range(len(idx))
                if pts[i] == pts[(i + 1) % len(idx)]]
        if zero:
            degen[pidx] = zero
        if self_intersecting(pts):
            bad.append(pidx)
    return polys, bad, degen


def repair_geometry(geo, report):
    verts = geo.first("Vertices").props[0][1]
    pi_node = geo.first("PolygonVertexIndex")
    pi = pi_node.props[0][1]
    polys, bad, degen = scan(geo)

    if not bad:
        report.append(f"  {_name(geo)}: already clean ({len(polys)} polygons)")
        return False

    unexplained = [p for p in bad if p not in degen]
    if unexplained:
        raise SystemExit(
            f"  {_name(geo)}: {len(unexplained)} self-intersecting polygons are NOT "
            f"zero-length-edge degeneracies ({unexplained[:8]}). This tool only "
            f"repairs the degenerate case; refusing to guess at real crossings.")

    # --- locate the layer elements and assert the domains this tool understands
    normal_el = next((c for c in geo.children if c.name == "LayerElementNormal"), None)
    uv_el = next((c for c in geo.children if c.name == "LayerElementUV"), None)
    mat_el = next((c for c in geo.children if c.name == "LayerElementMaterial"), None)
    smooth_el = next((c for c in geo.children if c.name == "LayerElementSmoothing"), None)
    edges_node = geo.first("Edges")

    handled = {"LayerElementNormal", "LayerElementUV", "LayerElementMaterial",
               "LayerElementSmoothing", "Layer", "Properties70", "GeometryVersion",
               "Vertices", "PolygonVertexIndex", "Edges"}
    stray = sorted({c.name for c in geo.children} - handled)
    if stray:
        raise SystemExit(f"  {_name(geo)}: unhandled geometry children {stray}; refusing to write.")

    if normal_el and (_layer_str(normal_el, "MappingInformationType") != "ByPolygonVertex"
                      or _layer_str(normal_el, "ReferenceInformationType") != "Direct"):
        raise SystemExit("  normals are not ByPolygonVertex/Direct; refusing to write.")
    if uv_el and (_layer_str(uv_el, "MappingInformationType") != "ByPolygonVertex"
                  or _layer_str(uv_el, "ReferenceInformationType") != "IndexToDirect"):
        raise SystemExit("  UVs are not ByPolygonVertex/IndexToDirect; refusing to write.")
    if mat_el and _layer_str(mat_el, "MappingInformationType") != "ByPolygon":
        raise SystemExit("  materials are not ByPolygon; refusing to write.")
    if smooth_el and _layer_str(smooth_el, "MappingInformationType") != "ByEdge":
        raise SystemExit("  smoothing is not ByEdge; refusing to write.")

    normals = normal_el.first("Normals").props[0][1] if normal_el else None
    uvindex = uv_el.first("UVIndex").props[0][1] if uv_el else None
    smoothing = smooth_el.first("Smoothing").props[0][1] if smooth_el else None
    edges = edges_node.props[0][1] if edges_node else None

    def pt(i):
        return (verts[3 * i], verts[3 * i + 1], verts[3 * i + 2])

    # --- prove the Edges rebuild rule against the SHIPPED array before relying on it
    if edges is not None:
        seen, rebuilt = set(), []
        for start, idx in polys:
            n = len(idx)
            for i in range(n):
                a, b = idx[i], idx[(i + 1) % n]
                key = (min(a, b), max(a, b))
                if key in seen:
                    continue
                seen.add(key)
                rebuilt.append(start + i)
        if rebuilt != list(edges):
            raise SystemExit("  Edges does not follow the first-occurrence rule on this "
                             "file; refusing to rebuild it.")
        report.append(f"  {_name(geo)}: Edges rebuild rule verified against the shipped "
                      f"array ({len(edges)} edges, exact)")

    # --- carry smoothing across by vertex pair, with a position-pair fallback
    smooth_by_pair, smooth_by_pos = {}, {}
    if edges is not None and smoothing is not None:
        starts = {start: idx for start, idx in polys}
        span_of = {}
        for start, idx in polys:
            for i in range(len(idx)):
                span_of[start + i] = (start, i, len(idx))
        for e_i, corner in enumerate(edges):
            start, i, n = span_of[corner]
            idx = starts[start]
            a, b = idx[i], idx[(i + 1) % n]
            smooth_by_pair[(min(a, b), max(a, b))] = smoothing[e_i]
            pa, pb = pt(a), pt(b)
            smooth_by_pos[tuple(sorted((pa, pb)))] = smoothing[e_i]

    # --- choose the corner to drop, per degenerate edge, by normal agreement
    drops = set()
    chosen = []
    for pidx in sorted(bad):
        start, idx = polys[pidx]
        n = len(idx)
        if n - len(degen[pidx]) < 3:
            raise SystemExit(f"  poly {pidx}: repairing it would leave fewer than 3 corners.")
        face = newell([pt(i) for i in idx])
        mag = math.sqrt(sum(c * c for c in face)) or 1.0
        face = tuple(c / mag for c in face)
        for i in degen[pidx]:
            a_pos, b_pos = start + i, start + ((i + 1) % n)
            if normals is None:
                keep, drop = a_pos, b_pos
            else:
                def dot(p):
                    return (normals[3 * p] * face[0] + normals[3 * p + 1] * face[1]
                            + normals[3 * p + 2] * face[2])
                keep, drop = (a_pos, b_pos) if dot(a_pos) >= dot(b_pos) else (b_pos, a_pos)
            drops.add(drop)
            chosen.append((pidx, n, keep, drop))

    if len(drops) != sum(len(degen[p]) for p in bad):
        raise SystemExit("  a corner was selected for removal twice; refusing to write.")

    # --- rebuild the corner-domain arrays
    new_pi, new_normals, new_uvindex = [], [], []
    new_polys = []
    for start, idx in polys:
        n = len(idx)
        kept = [start + i for i in range(n) if (start + i) not in drops]
        if len(kept) < 3:
            raise SystemExit("  a repair would leave a polygon with fewer than 3 corners.")
        base = len(new_pi)
        for j, corner in enumerate(kept):
            vid = idx[corner - start]
            new_pi.append(~vid if j == len(kept) - 1 else vid)
            if normals is not None:
                new_normals.extend(normals[3 * corner:3 * corner + 3])
            if uvindex is not None:
                new_uvindex.append(uvindex[corner])
        new_polys.append((base, [idx[c - start] for c in kept]))

    # --- rebuild Edges + Smoothing from the repaired topology
    new_edges, new_smoothing, fallbacks, unresolved = [], [], 0, 0
    if edges is not None:
        seen = set()
        for start, idx in new_polys:
            n = len(idx)
            for i in range(n):
                a, b = idx[i], idx[(i + 1) % n]
                key = (min(a, b), max(a, b))
                if key in seen:
                    continue
                seen.add(key)
                new_edges.append(start + i)
                if smoothing is None:
                    continue
                if key in smooth_by_pair:
                    new_smoothing.append(smooth_by_pair[key])
                else:
                    poskey = tuple(sorted((pt(a), pt(b))))
                    if poskey in smooth_by_pos:
                        new_smoothing.append(smooth_by_pos[poskey]); fallbacks += 1
                    else:
                        new_smoothing.append(1); unresolved += 1

    # --- write the arrays back into the node tree
    pi_node.props[0] = (pi_node.props[0][0], new_pi)
    if normals is not None:
        nn = normal_el.first("Normals"); nn.props[0] = (nn.props[0][0], new_normals)
    if uvindex is not None:
        un = uv_el.first("UVIndex"); un.props[0] = (un.props[0][0], new_uvindex)
    if edges is not None:
        edges_node.props[0] = (edges_node.props[0][0], new_edges)
    if smoothing is not None:
        sn = smooth_el.first("Smoothing"); sn.props[0] = (sn.props[0][0], new_smoothing)

    report.append(
        f"  {_name(geo)}: repaired {len(chosen)} degenerate corners in "
        f"{len(bad)} discarded polygons "
        f"(corners {len(pi)} -> {len(new_pi)}, edges {len(edges or [])} -> {len(new_edges)}, "
        f"smoothing carried by pair, {fallbacks} by position, {unresolved} defaulted)")
    for pidx, n, keep, drop in chosen[:16]:
        report.append(f"      poly {pidx:5d}: {n}-gon -> {n-1}-gon, dropped corner {drop}")
    return True


def verify(path, expect_polys=None, label=""):
    nodes, ver, _ = F.read(path)
    ok = True
    for geo in mesh_geometries(nodes):
        verts = geo.first("Vertices").props[0][1]
        pi = geo.first("PolygonVertexIndex").props[0][1]
        polys, bad, degen = scan(geo)
        normal_el = next((c for c in geo.children if c.name == "LayerElementNormal"), None)
        uv_el = next((c for c in geo.children if c.name == "LayerElementUV"), None)
        mat_el = next((c for c in geo.children if c.name == "LayerElementMaterial"), None)
        smooth_el = next((c for c in geo.children if c.name == "LayerElementSmoothing"), None)
        edges = geo.first("Edges")
        n_norm = len(normal_el.first("Normals").props[0][1]) if normal_el else 0
        n_uvi = len(uv_el.first("UVIndex").props[0][1]) if uv_el else 0
        n_mat = len(mat_el.first("Materials").props[0][1]) if mat_el else 0
        n_edge = len(edges.props[0][1]) if edges else 0
        n_sm = len(smooth_el.first("Smoothing").props[0][1]) if smooth_el else 0
        print(f"{label}{_name(geo)}: verts={len(verts)//3} polys={len(polys)} "
              f"corners={len(pi)} normals={n_norm} uvindex={n_uvi} materials={n_mat} "
              f"edges={n_edge} smoothing={n_sm}")
        problems = []
        if bad:
            problems.append(f"{len(bad)} SELF-INTERSECTING polygons {bad[:8]}")
        slivers = [p for p in degen if p not in bad]
        if slivers:
            print(f"  note: {len(slivers)} zero-area polygons carry a zero-length edge but "
                  f"are NOT discarded by Unity; left untouched")
        if [p for p in degen if p in bad]:
            problems.append("a discarded polygon still carries a zero-length edge")
        if normal_el and n_norm != 3 * len(pi):
            problems.append(f"normals {n_norm} != 3*corners {3*len(pi)}")
        if uv_el and n_uvi != len(pi):
            problems.append(f"uvindex {n_uvi} != corners {len(pi)}")
        if mat_el and n_mat != len(polys):
            problems.append(f"materials {n_mat} != polygons {len(polys)}")
        if edges and smooth_el and n_edge != n_sm:
            problems.append(f"edges {n_edge} != smoothing {n_sm}")
        if edges and (max(edges.props[0][1]) >= len(pi)):
            problems.append("an Edges entry points past the last corner")
        if expect_polys is not None and len(polys) != expect_polys:
            problems.append(f"polygon count changed: {len(polys)} != {expect_polys}")
        for p in problems:
            print(f"  FAIL: {p}")
            ok = False
    return ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true",
                    help="verify only; fail if any polygon is still self-intersecting")
    ap.add_argument("targets", nargs="*", default=None)
    args = ap.parse_args()
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    targets = args.targets or [os.path.join(root, t) for t in DEFAULT_TARGETS]

    rc = 0
    for path in targets:
        print(f"== {os.path.relpath(path, root)}")
        if args.check:
            if not verify(path, label="  "):
                rc = 1
            continue

        nodes, ver, footer = F.read(path)
        geos = mesh_geometries(nodes)
        before = {_name(g): scan(g)[0] for g in geos}
        report, changed = [], False
        for geo in geos:
            changed |= repair_geometry(geo, report)
        for line in report:
            print(line)
        if not changed:
            print("  nothing to do")
            continue

        tmp = path + ".repair.tmp"
        F.write(tmp, nodes, ver, footer)
        print("  -- verifying the WRITTEN file --")
        expect = len(next(iter(before.values()))) if len(before) == 1 else None
        if not verify(tmp, expect_polys=expect, label="  "):
            os.unlink(tmp)
            raise SystemExit("  written file failed verification; original left untouched")
        # vertex positions must be byte-identical
        reread, _, _ = F.read(tmp)
        for geo in mesh_geometries(reread):
            orig = next(g for g in geos if _name(g) == _name(geo))
            if list(geo.first("Vertices").props[0][1]) != list(orig.first("Vertices").props[0][1]):
                os.unlink(tmp)
                raise SystemExit("  vertex positions changed; refusing to ship")
        print("  vertex positions unchanged")
        shutil.move(tmp, path)
        print(f"  wrote {os.path.relpath(path, root)}")
    return rc


if __name__ == "__main__":
    sys.exit(main())
