#!/usr/bin/env python3
"""GROWN wire: curved, tapered, woven limbs and membranes, for bodies that should look alive and not assembled.

Garrett, 2026-11-16: "Relax the fauna rules a bit, so you can get us some fauna that are more lifelike. these feel like
you are using tinker toys, when I want you to be making hyper realistic ethereal lifeforms in a celestial environment."
and then: "this is a far better direction. now go far in this direction".

Still wire (a body the Space element can thin, nothing solid in it), but grown:
- tube: strands wound round a smooth curve through the spec's `path`, the girth running from its root value to its tip
  value (a taper, or a shell that widens), cross-tied every few steps.
- barbs: fine side filaments along a limb (a feather's barbs, a comb's cilia, a tentacle's side lines), on one, two or
  more sides, shortening toward the tip.
- sheet: a membrane woven between two limbs: filaments from one curve to the other, threads along them, a faint fill.
Every vertex carries two things the lab's vertex stage plays, so nothing is animated by hand:
- SWAY, as a sine part and a cosine part (six numbers): the limb's own slow wave, largest at its tip, PLUS whatever
  the limb it grows from is doing where they join. Same tempo everywhere, so the parts add and a tendril rides its bell.
- GLOW, a brightness and a pulse phase: light that travels down the limb.
A READER of specs: it returns vertices, edges, triangles, sway and glow, and writes nothing.
"""
import math

import celestial as C

ZERO6 = [0.0] * 6


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


def _tangential(p, fallback):
    r = [p[0], p[1], 0.0]
    return C.unit(C.cross([0.0, 0.0, 1.0], r)) if C.norm(r) > 1e-6 else fallback


def sway6(sw, p, s, normal, base=None):
    """A limb's wave at point p, a share s down the limb, as [sine part xyz, cosine part xyz], added to `base` (what
    the limb it grows from is doing). offset(t) = S sin(t) + Cc cos(t), so d * A * sin(t - phase) is (d A cos, -d A sin)."""
    out = list(base or ZERO6)
    if not sw:
        return out
    d = sw.get("dir", "normal")
    if d == "radial":
        r = [p[0], p[1], 0.0]
        d = C.unit(r) if C.norm(r) > 1e-6 else normal
    elif d == "tangent":
        d = _tangential(p, normal)
    elif d == "normal":
        d = normal
    amp = sw["amp"] * s ** sw.get("pow", 1.4)
    ph = sw.get("phase", 0.0) + 2 * math.pi * sw.get("waves", 1.0) * s
    for c in range(3):
        out[c] += d[c] * amp * math.cos(ph)
        out[3 + c] -= d[c] * amp * math.sin(ph)
    return out


def glow2(gl, s):
    """(brightness, pulse phase) a share s down a limb. No `glow`: full brightness, a pulse that follows the limb."""
    gl = gl or {}
    b0, b1 = gl.get("bright", [1.0, 0.75])
    return [b0 + (b1 - b0) * s, gl.get("phase", 0.0) + 2 * math.pi * gl.get("waves", 1.0) * s]


def centre(spindle):
    n = max(6, int(length(spindle["path"]) / spindle.get("step", 0.9)) + 1)
    return smooth(spindle["path"], n)


def sway_at(spindle, at, base=None):
    """The limb's whole sway (its own plus `base`) at the point of it nearest `at`: what a child or a prism rides."""
    P = centre(spindle)
    i = min(range(len(P)), key=lambda k: C.norm(C.sub(P[k], at)))
    return sway6(spindle.get("sway"), P[i], i / (len(P) - 1), C.unit(spindle.get("normal", [0.0, 1.0, 0.0])), base)


def tube(spindle, base=None):
    """A limb grown along spindle['path']: (verts, edges, sway [6 each], glow [2 each], lines).
    `lines` is what spindle.py sweeps into the game's mesh: each strand and each barb as (vertex indices, kind)."""
    P = centre(spindle)
    n = len(P)
    g0, g1 = spindle.get("girth", [0.5, 0.2])
    strands, twist = spindle.get("strands", 3), spindle.get("twist", 1.5)
    normal = C.unit(spindle.get("normal", [0.0, 1.0, 0.0]))
    ties = spindle.get("ties", 3)
    verts, edges, sway, glow, lines = [], [], [], [], []
    frames = []
    for i, p in enumerate(P):
        s = i / (n - 1)
        t = C.unit(C.sub(P[min(i + 1, n - 1)], P[max(i - 1, 0)]))
        ref = normal if abs(C.dot(t, normal)) < 0.95 else [1.0, 0.0, 0.0]
        a1 = C.unit(C.cross(ref, t))
        a2 = C.cross(t, a1)
        frames.append((t, a1, a2))
        r = g0 + (g1 - g0) * s
        sw, gl = sway6(spindle.get("sway"), p, s, normal, base), glow2(spindle.get("glow"), s)
        for k in range(strands):
            ang = 2 * math.pi * (k / strands + twist * s)
            verts.append([p[c] + r * (math.cos(ang) * a1[c] + math.sin(ang) * a2[c]) for c in range(3)])
            sway.append(sw)
            glow.append(gl)
            if i > 0:
                edges.append(((i - 1) * strands + k, i * strands + k))
        if strands > 1 and i % ties == 0:
            for k in range(strands):
                edges.append((i * strands + k, i * strands + (k + 1) % strands))
    for k in range(strands):
        lines.append(([i * strands + k for i in range(n)], "strand"))
    barbs = spindle.get("barbs")
    if barbs:      # fine side filaments: each a short curve leaving the limb, leaning toward its tip, fluttering a little
        count, sides = barbs["count"], barbs.get("sides", 2)
        l0, l1 = barbs.get("length", [3.0, 1.0])
        lean = math.radians(barbs.get("angle", 60.0))
        for b in range(count):
            s = barbs.get("from", 0.08) + (barbs.get("to", 0.97) - barbs.get("from", 0.08)) * b / max(1, count - 1)
            i = min(n - 1, int(round(s * (n - 1))))
            p, (t, a1, a2) = P[i], frames[i]
            lat0 = _tangential(p, a1) if barbs.get("dir") == "tangent" else a1
            lat0 = C.unit(C.sub(lat0, C.mul(t, C.dot(lat0, t))))
            other = C.cross(t, lat0)
            ln = l0 + (l1 - l0) * s
            sw0, gl0 = sway6(spindle.get("sway"), p, s, normal, base), glow2(spindle.get("glow"), s)
            for k in range(sides):
                ang = 2 * math.pi * k / sides
                lat = [math.cos(ang) * lat0[c] + math.sin(ang) * other[c] for c in range(3)]
                d = [math.sin(lean) * lat[c] + math.cos(lean) * t[c] for c in range(3)]
                first = len(verts)
                for j in range(4):
                    u = j / 3.0
                    q = [p[c] + d[c] * ln * u + t[c] * ln * 0.18 * u * u for c in range(3)]      # a slight curl toward the tip
                    fl = barbs.get("flutter", 0.25) * ln * u * u
                    verts.append(q)
                    sway.append([sw0[c] + other[c] * fl * math.cos(b * 0.7) if c < 3 else sw0[c] - other[c - 3] * fl * math.sin(b * 0.7) for c in range(6)])
                    glow.append([gl0[0] * (0.9 - 0.45 * u), gl0[1] + 0.6 * u])
                    if j > 0:
                        edges.append((first + j - 1, first + j))
                lines.append(([first + j for j in range(4)], "barb"))
    return verts, edges, sway, glow, lines


def sheet(a, b, across=9, base_a=None, base_b=None):
    """A membrane woven between limb a and limb b: (verts, edges, sway, glow, tris)."""
    n = max(8, int(max(length(a["path"]), length(b["path"])) / 1.3) + 1)
    A, B = smooth(a["path"], n), smooth(b["path"], n)
    na, nb = C.unit(a.get("normal", [0, 1, 0])), C.unit(b.get("normal", [0, 1, 0]))
    verts, edges, sway, glow, tris = [], [], [], [], []
    for i in range(n):
        s = i / (n - 1)
        sa, sb = sway6(a.get("sway"), A[i], s, na, base_a), sway6(b.get("sway"), B[i], s, nb, base_b)
        ga, gb = glow2(a.get("glow"), s), glow2(b.get("glow"), s)
        for j in range(across):
            u = j / (across - 1)
            verts.append([A[i][c] + (B[i][c] - A[i][c]) * u for c in range(3)])
            sway.append([sa[c] + (sb[c] - sa[c]) * u for c in range(6)])
            glow.append([0.55 * (ga[0] + (gb[0] - ga[0]) * u), ga[1] + (gb[1] - ga[1]) * u])
            v = i * across + j
            if j > 0:
                edges.append((v - 1, v))
            if i > 0 and j % 2 == 0:
                edges.append((v - across, v))
            if i > 0 and j > 0:
                tris += [(v - across - 1, v - across, v), (v - across - 1, v, v - 1)]
    return verts, edges, sway, glow, tris
