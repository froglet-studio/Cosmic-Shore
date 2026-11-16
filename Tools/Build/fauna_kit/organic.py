#!/usr/bin/env python3
"""GROWN wire: curved, tapered, woven limbs and membranes, for bodies that should look alive and not assembled.

Garrett, 2026-11-16: "Relax the fauna rules a bit, so you can get us some fauna that are more lifelike. these feel like
you are using tinker toys, when I want you to be making hyper realistic ethereal lifeforms in a celestial environment."

The braid kit stretched one straight mesh between two points. This grows the wire instead, and it is still wire (a
body the Space element can thin, with nothing solid in it):
- tube: strands wound round a smooth curve through the spec's `path`, tapering from the root girth to the tip girth,
  cross-tied every few steps. A limb, a tentacle, a spine, a leading edge.
- sheet: a membrane woven between two limbs: fine filaments from one curve to the other and threads along them, with
  a faint fill. A wing, a bell, a frill.
Every vertex carries its SWAY (an offset and a phase): the limb's own slow wave, largest at its tip, which the lab
plays in its vertex stage the way the game's SpindleSway does, so nothing here is animated by hand.
A READER of specs: it returns vertices, edges, triangles and sway, and writes nothing.
"""
import math

import celestial as C


def smooth(points, n):
    """n points along a Catmull-Rom curve through `points`, evenly spaced by arc length."""
    if len(points) == 2:
        return [[points[0][i] + (points[1][i] - points[0][i]) * k / (n - 1) for i in range(3)] for k in range(n)]
    p = [points[0]] + list(points) + [points[-1]]
    dense = []
    for i in range(1, len(p) - 2):
        for s in range(12):
            t = s / 12.0
            dense.append([0.5 * ((2 * p[i][c]) + (-p[i - 1][c] + p[i + 1][c]) * t + (2 * p[i - 1][c] - 5 * p[i][c] + 4 * p[i + 1][c] - p[i + 2][c]) * t * t
                                 + (-p[i - 1][c] + 3 * p[i][c] - 3 * p[i + 1][c] + p[i + 2][c]) * t ** 3) for c in range(3)])
    dense.append(list(points[-1]))
    acc = [0.0]
    for a, b in zip(dense, dense[1:]):
        acc.append(acc[-1] + C.norm(C.sub(b, a)))
    out, j = [], 0
    for k in range(n):
        want = acc[-1] * k / (n - 1)
        while j < len(acc) - 2 and acc[j + 1] < want:
            j += 1
        span = max(acc[j + 1] - acc[j], 1e-9)
        t = min(1.0, max(0.0, (want - acc[j]) / span))
        out.append([dense[j][c] + (dense[j + 1][c] - dense[j][c]) * t for c in range(3)])
    return out


def length(points):
    return sum(C.norm(C.sub(b, a)) for a, b in zip(points, points[1:]))


def _sway(sw, p, s, normal):
    """(offset vector, phase) of the limb's wave at a point p, a share s of the way down the limb."""
    if not sw:
        return [0.0, 0.0, 0.0], 0.0
    d = sw.get("dir", "normal")
    if d == "radial":
        r = [p[0], p[1], 0.0]
        d = C.unit(r) if C.norm(r) > 1e-6 else normal
    elif d == "tangent":
        r = [p[0], p[1], 0.0]
        d = C.unit(C.cross([0.0, 0.0, 1.0], r)) if C.norm(r) > 1e-6 else normal
    elif d == "normal":
        d = normal
    amp = sw["amp"] * s ** sw.get("pow", 1.4)
    return C.mul(d, amp), sw.get("phase", 0.0) + 2 * math.pi * sw.get("waves", 1.0) * s


def tube(spindle):
    """A limb grown along spindle['path']: (verts, edges, sway [(dx, dy, dz, phase)], centre points)."""
    path = spindle["path"]
    n = max(6, int(length(path) / spindle.get("step", 0.9)) + 1)
    P = smooth(path, n)
    g0, g1 = spindle.get("girth", [0.5, 0.2])
    strands, twist = spindle.get("strands", 3), spindle.get("twist", 1.5)
    normal = C.unit(spindle.get("normal", [0.0, 1.0, 0.0]))
    verts, edges, sway = [], [], []
    for i, p in enumerate(P):
        s = i / (n - 1)
        t = C.unit(C.sub(P[min(i + 1, n - 1)], P[max(i - 1, 0)]))
        ref = normal if abs(C.dot(t, normal)) < 0.95 else [1.0, 0.0, 0.0]
        a1 = C.unit(C.cross(ref, t))
        a2 = C.cross(t, a1)
        r = g0 + (g1 - g0) * s
        off, ph = _sway(spindle.get("sway"), p, s, normal)
        for k in range(strands):
            ang = 2 * math.pi * (k / strands + twist * s)
            verts.append([p[c] + r * (math.cos(ang) * a1[c] + math.sin(ang) * a2[c]) for c in range(3)])
            sway.append(off + [ph])
            if i > 0:
                edges.append(((i - 1) * strands + k, i * strands + k))
        if strands > 1 and i % 3 == 0:
            for k in range(strands):
                edges.append((i * strands + k, i * strands + (k + 1) % strands))
    return verts, edges, sway, P


def sheet(a, b, across=9):
    """A membrane woven between limb a and limb b (two spindles with paths): filaments across, threads along, a fill."""
    n = max(8, int(max(length(a["path"]), length(b["path"])) / 1.3) + 1)
    A, B = smooth(a["path"], n), smooth(b["path"], n)
    na, nb = C.unit(a.get("normal", [0, 1, 0])), C.unit(b.get("normal", [0, 1, 0]))
    verts, edges, sway, tris = [], [], [], []
    for i in range(n):
        s = i / (n - 1)
        oa, pa = _sway(a.get("sway"), A[i], s, na)
        ob, pb = _sway(b.get("sway"), B[i], s, nb)
        for j in range(across):
            u = j / (across - 1)
            verts.append([A[i][c] + (B[i][c] - A[i][c]) * u for c in range(3)])
            sway.append([oa[c] + (ob[c] - oa[c]) * u for c in range(3)] + [pa + (pb - pa) * u])
            v = i * across + j
            if j > 0:
                edges.append((v - 1, v))
            if i > 0 and j % 2 == 0:
                edges.append((v - across, v))
            if i > 0 and j > 0:
                tris += [(v - across - 1, v - across, v), (v - across - 1, v, v - 1)]
    return verts, edges, sway, tris
