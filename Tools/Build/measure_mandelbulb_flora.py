#!/usr/bin/env python3
"""
The offline MODEL for the Mandelbulb flora species - the authority for its prism counts, its
volume ladder, its per-element plate scale and the measurements its C# cites in comments.

This is a FRESH transcription of the growth rule, deliberately independent of
Assets/_Scripts/Controller/Environment/FloraAndFauna/MandelbulbLattice.cs. It is the
"measurement" half of the pair; verify_mandelbulb_flora_tables.py is the "the shipped file really
does this" half, and the two are separate scripts on purpose - the transcription from a proven
measurement into the asset is the step neither the measurement nor code review can see
(Docs/ECOSYSTEM.md §34, the Schwarz P precedent).

Usage
    measure_mandelbulb_flora.py                 table + the authored-constant audit
    measure_mandelbulb_flora.py --check         fail (exit 1) on any drift from the shipped C#
    measure_mandelbulb_flora.py --render DIR    write oriented-box PNGs of each element's plant
    measure_mandelbulb_flora.py --fit           report the plate fit and its headroom
"""
from __future__ import annotations

import argparse
import math
import os
import re
import struct
import sys
import zlib
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FLORA_CS = ROOT / "Assets/_Scripts/Controller/Environment/FloraAndFauna/MandelbulbFlora.cs"
OCTAHEDRON_CS = ROOT / "Assets/_Scripts/Utility/OctahedronMeshGenerator.cs"

FACE6 = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
N26 = [(a, b, c) for a in (-1, 0, 1) for b in (-1, 0, 1) for c in (-1, 0, 1) if (a, b, c) != (0, 0, 0)]

# The largest FRACTION of touching plate pairs allowed to interpenetrate at all. Two patches that
# meet along a ridge have bounding boxes that must overlap near the seam - it is geometry, not a
# defect, and it reads as a joint - so this species states a BOUND instead of claiming a zero it
# cannot have. ContainDrop is the other half of the same statement: it bounds how DEEP any
# overlap goes. Both are gates; neither is slack to spend.
MAX_INTERPENETRATING_FRACTION = 0.25


# ── the set ───────────────────────────────────────────────────────────────────

def inside(px, py, pz, power, iterations, bailout):
    """The Mandelbulb membership test: v <- v^power + c from v = 0, c = the point.

    The triplex power is the standard White/Nylander form, (r, th, ph)^n = (r^n, n*th, n*ph).
    Nothing here describes a bulb.
    """
    x = y = z = 0.0
    for _ in range(iterations):
        r = math.sqrt(x * x + y * y + z * z)
        if r > bailout:
            return False
        if r < 1e-12:
            x, y, z = px, py, pz
            continue
        theta = math.acos(max(-1.0, min(1.0, z / r)))
        phi = math.atan2(y, x)
        rn = r ** power
        st = math.sin(power * theta)
        x = rn * st * math.cos(power * phi) + px
        y = rn * st * math.sin(power * phi) + py
        z = rn * math.cos(power * theta) + pz
    return True


class Bulb:
    """Membership, shell and normals on the ambient integer lattice, memoised."""

    def __init__(self, power, pitch, iterations=10, bailout=2.0):
        self.power, self.pitch = power, pitch
        self.iterations, self.bailout = iterations, bailout
        self._in, self._shell = {}, {}

    def inside(self, site):
        v = self._in.get(site)
        if v is None:
            v = inside(site[0] * self.pitch, site[1] * self.pitch, site[2] * self.pitch,
                       self.power, self.iterations, self.bailout)
            self._in[site] = v
        return v

    def is_shell(self, site):
        v = self._shell.get(site)
        if v is None:
            v = self.inside(site) and any(
                not self.inside((site[0] + a, site[1] + b, site[2] + c)) for a, b, c in FACE6)
            self._shell[site] = v
        return v

    def normal(self, site):
        """The EXPOSED-FACE census. Docs/ECOSYSTEM.md §44 records why this and not the analytic
        distance estimator's gradient: on a fractal boundary at voxel scale the gradient gives
        neighbouring sites wildly different normals and the plant renders as confetti."""
        nx = ny = nz = 0.0
        for a, b, c in N26:
            if self.inside((site[0] + a, site[1] + b, site[2] + c)):
                continue
            w = 1.0 / math.sqrt(a * a + b * b + c * c)
            nx += a * w; ny += b * w; nz += c * w
        m = math.sqrt(nx * nx + ny * ny + nz * nz)
        if m < 1e-9:
            r = math.sqrt(sum(t * t for t in site)) or 1.0
            return (site[0] / r, site[1] / r, site[2] / r)
        return (nx / m, ny / m, nz / m)

    def seed(self, bound):
        """The plant's starting site: march -z from the origin to the last site still inside."""
        last = (0, 0, 0)
        for s in range(1, bound + 1):
            probe = (0, 0, -s)
            if not self.inside(probe):
                break
            last = probe
        return last

    def walk(self, bound):
        """The reachable shell in GROWTH ORDER: the 26-neighbour walk the plant spreads with."""
        start = self.seed(bound)
        seen, order = {start}, []
        q = deque([start])
        while q:
            site = q.popleft()
            order.append(site)
            for a, b, c in N26:
                nxt = (site[0] + a, site[1] + b, site[2] + c)
                if max(abs(t) for t in nxt) > bound or nxt in seen:
                    continue
                seen.add(nxt)
                if self.is_shell(nxt):
                    q.append(nxt)
        return start, order


def bound_for(pitch):
    return math.ceil(1.45 / pitch)


# ── small vector / eigen helpers ──────────────────────────────────────────────

def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def unit(v):
    m = math.sqrt(dot(v, v))
    return (v[0] / m, v[1] / m, v[2] / m) if m > 1e-12 else (0.0, 0.0, 1.0)


def jacobi3(a):
    """Symmetric 3x3 eigen-decomposition by cyclic Jacobi rotations, eigenvalues descending."""
    A = [row[:] for row in a]
    V = [[1.0 if i == j else 0.0 for j in range(3)] for i in range(3)]
    for _ in range(60):
        if abs(A[0][1]) + abs(A[0][2]) + abs(A[1][2]) < 1e-14:
            break
        for p, q in ((0, 1), (0, 2), (1, 2)):
            if abs(A[p][q]) < 1e-18:
                continue
            theta = (A[q][q] - A[p][p]) / (2.0 * A[p][q])
            t = (1.0 if theta >= 0 else -1.0) / (abs(theta) + math.sqrt(theta * theta + 1.0))
            c = 1.0 / math.sqrt(t * t + 1.0)
            s = t * c
            for k in range(3):
                akp, akq = A[k][p], A[k][q]
                A[k][p] = c * akp - s * akq
                A[k][q] = s * akp + c * akq
            for k in range(3):
                apk, aqk = A[p][k], A[q][k]
                A[p][k] = c * apk - s * aqk
                A[q][k] = s * apk + c * aqk
            for k in range(3):
                vkp, vkq = V[k][p], V[k][q]
                V[k][p] = c * vkp - s * vkq
                V[k][q] = s * vkp + c * vkq
    vals = [A[0][0], A[1][1], A[2][2]]
    order = sorted(range(3), key=lambda i: -vals[i])
    return [vals[i] for i in order], [[V[0][i], V[1][i], V[2][i]] for i in order]


def fit_points(points):
    n = len(points)
    c = [sum(p[k] for p in points) / n for k in range(3)]
    m = [[0.0] * 3 for _ in range(3)]
    for p in points:
        v = (p[0] - c[0], p[1] - c[1], p[2] - c[2])
        for i in range(3):
            for j in range(3):
                m[i][j] += v[i] * v[j]
    for i in range(3):
        for j in range(3):
            m[i][j] /= n
    vals, vecs = jacobi3(m)
    return c, vals, vecs


# Smallest MINOR in-plane variance, in cells squared, at which a patch counts as genuinely
# two-dimensional. Below it the covariance has rank under 2, the two smallest eigenvectors are
# interchangeable, and the frame a principal-axis fit hands back can flip on a rounding
# difference. Mirrors MandelbulbLattice.PlanarRankTolerance, which documents the choice.
PLANAR_RANK_TOLERANCE = 0.02

# Below this a dot product is not evidence about which way a plate faces: a patch's fitted normal
# can come out very nearly PERPENDICULAR to the cell's own census normal, and then "flip it if the
# dot is negative" is a coin toss decided by float noise. Mirrors
# MandelbulbLattice.OrientationTolerance, which documents the choice.
ORIENTATION_TOLERANCE = 1e-3


def points_inward(normal, census, centroid):
    """Whether a fitted normal points INTO the plant, deterministically: the census normal when
    it is a real answer, else the radial, else a sign convention."""
    d = dot(normal, census)
    if abs(d) > ORIENTATION_TOLERANCE:
        return d < 0.0
    d = dot(normal, centroid)
    if abs(d) > ORIENTATION_TOLERANCE:
        return d < 0.0
    for t in normal:
        if abs(t) > ORIENTATION_TOLERANCE:
            return t < 0.0
    return False


# ── the plating ───────────────────────────────────────────────────────────────

def touching_scale(a, b):
    """The uniform scale at which two centrally symmetric convex bodies first touch, exactly:

        s* = max over the candidate axes of  |d.u| / (rA(u) + rB(u))

    Below s* some axis separates them; above it none does, so s* >= 1 means the pair is clear as
    it stands. Same closed form Tools/Build/fit_shield_clearance.py uses; pure stdlib here
    because nothing else in this tool needs numpy. Self-tested in self_test().
    """
    axes, ra, rb = a["axes"], a["support"], b["support"]
    cand = list(axes) + list(b["axes"])
    for u in axes:
        for v in b["axes"]:
            c = cross(u, v)
            if dot(c, c) > 1e-12:
                cand.append(unit(c))
    d = tuple(b["c"][k] - a["c"][k] for k in range(3))
    best = 0.0
    for u in cand:
        den = ra(u) + rb(u)
        if den <= 1e-12:
            continue
        best = max(best, abs(dot(d, u)) / den)
    return best


def as_box(plate, scale=1.0):
    e, s = plate["e"], plate["size"]
    half = tuple(0.5 * s[k] * scale for k in range(3))
    return {"c": plate["c"], "axes": tuple(e),
            "support": lambda u, e=e, h=half: sum(h[i] * abs(dot(u, e[i])) for i in range(3)),
            "radius": math.sqrt(sum(h * h for h in half))}


def as_octahedron(plate, circumscribing, scale=1.0):
    """What a SHIELDED prism draws: the octahedron conv{+-S0, +-S1, +-S2} circumscribing the box,
    reaching circumscribing/2 x size along each of its own axes (Docs/ECOSYSTEM.md §35)."""
    e, s = plate["e"], plate["size"]
    semi = [tuple(e[i][k] * 0.5 * s[i] * circumscribing * scale for k in range(3)) for i in range(3)]
    axes = []
    for sx in (1, -1):
        for sy in (1, -1):
            for sz in (1, -1):
                a = tuple(sx * t for t in semi[0])
                b = tuple(sy * t for t in semi[1])
                c = tuple(sz * t for t in semi[2])
                n = cross((b[0] - a[0], b[1] - a[1], b[2] - a[2]),
                          (c[0] - a[0], c[1] - a[1], c[2] - a[2]))
                if dot(n, n) > 1e-18:
                    axes.append(unit(n))
    return {"c": plate["c"], "axes": tuple(axes),
            "support": lambda u, S=semi: max(abs(dot(u, s)) for s in S),
            "radius": max(math.sqrt(dot(s, s)) for s in semi)}


def plating(bulb, rules, bound):
    """The prisms one plant lays, in growth order. Independent transcription of the rule in
    MandelbulbLattice.Plating - see this module's docstring for why that is deliberate."""
    riser, coplanar, tau = rules["riser"], rules["coplanar"], rules["tau"]
    max_cells, pad, thickness, drop = rules["max_cells"], rules["pad"], rules["thickness"], rules["drop"]

    _, order = bulb.walk(bound)

    normal, selected = {}, set()
    for s in order:
        n = bulb.normal(s)
        normal[s] = n
        rm = math.sqrt(sum(t * t for t in s)) or 1.0
        if 1.0 - abs(dot(s, n) / rm) >= riser:
            selected.add(s)

    claimed, plates, bucket = set(), [], {}
    bucket_size = max(4.0, float(max_cells))

    def near(centre):
        key = tuple(int(math.floor(centre[k] / bucket_size)) for k in range(3))
        out = []
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    out += bucket.get((key[0] + dx, key[1] + dy, key[2] + dz), [])
        return out

    for start in order:
        if start not in selected or start in claimed:
            continue
        patch = [start]
        claimed.add(start)
        mean = list(normal[start])
        q = deque([start])
        while q and len(patch) < max_cells:
            cur = q.popleft()
            for a, b, c in N26:
                nxt = (cur[0] + a, cur[1] + b, cur[2] + c)
                if nxt not in selected or nxt in claimed:
                    continue
                nn = normal[nxt]
                mm = math.sqrt(dot(mean, mean)) or 1.0
                if dot(mean, nn) / mm < coplanar:
                    continue
                patch.append(nxt)
                if len(patch) >= 4:
                    _, vals, _ = fit_points(patch)
                    if math.sqrt(max(0.0, vals[2])) > tau:
                        patch.pop()
                        continue
                claimed.add(nxt)
                q.append(nxt)
                for k in range(3):
                    mean[k] += nn[k]
                if len(patch) >= max_cells:
                    break

        c, vals, vecs = fit_points(patch)
        seed_normal = normal[start]
        if len(patch) >= 3 and vals[1] > PLANAR_RANK_TOLERANCE:
            e0, e1, e2 = vecs[0], vecs[1], vecs[2]
            if points_inward(e2, seed_normal, c):
                e2 = tuple(-t for t in e2)
                e0 = tuple(-t for t in e0)
        else:
            e2 = unit(seed_normal)
            axis = vecs[0] if vals[0] > PLANAR_RANK_TOLERANCE else (
                (1.0, 0.0, 0.0) if abs(e2[2]) > 0.95 else (0.0, 0.0, 1.0))
            along = dot(axis, e2)
            e0 = tuple(axis[k] - along * e2[k] for k in range(3))
            if dot(e0, e0) < 1e-12:
                e0 = cross(e2, (0.0, 1.0, 0.0))
            if dot(e0, e0) < 1e-12:
                e0 = cross(e2, (1.0, 0.0, 0.0))
            e0 = unit(e0)
            e1 = cross(e2, e0)

        h0 = h1 = 0.0
        for p in patch:
            v = (p[0] - c[0], p[1] - c[1], p[2] - c[2])
            h0 = max(h0, abs(dot(v, e0)))
            h1 = max(h1, abs(dot(v, e1)))
        plate = {"seed": start, "c": tuple(c), "e": (tuple(e0), tuple(e1), tuple(e2)),
                 "size": ((2 * h0 + 1.0) * pad, (2 * h1 + 1.0) * pad, thickness),
                 "cells": len(patch)}

        if drop > 0.0:
            box = as_box(plate)
            if any(touching_scale(box, as_box(o)) < drop for o in near(plate["c"])):
                continue    # a hidden plate is a decision, not a retry: its cells stay claimed

        key = tuple(int(math.floor(plate["c"][k] / bucket_size)) for k in range(3))
        bucket.setdefault(key, []).append(plate)
        plates.append(plate)

    return order, selected, plates


def pair_report(plates, body, radius_mult=1.05):
    """Broadphase by bounding sphere, then the exact test. Returns (pairs, interpenetrating,
    smallest s*)."""
    bodies = [body(p) for p in plates]
    cell = max(b["radius"] for b in bodies) * 2.0 or 1.0
    grid = {}
    for i, b in enumerate(bodies):
        grid.setdefault(tuple(int(math.floor(b["c"][k] / cell)) for k in range(3)), []).append(i)
    pairs = over = 0
    worst = float("inf")
    for key, idxs in grid.items():
        near = []
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    near += grid.get((key[0] + dx, key[1] + dy, key[2] + dz), [])
        for i in idxs:
            for j in near:
                if j <= i:
                    continue
                a, b = bodies[i], bodies[j]
                if math.dist(a["c"], b["c"]) > (a["radius"] + b["radius"]) * radius_mult:
                    continue
                pairs += 1
                s = touching_scale(a, b)
                worst = min(worst, s)
                if s < 1.0:
                    over += 1
    return pairs, over, worst


# ── what the C# actually says ─────────────────────────────────────────────────

def authored():
    """Parse the shipped MandelbulbFlora.cs so this tool cannot drift from the asset."""
    src = FLORA_CS.read_text()

    def num(field, cast=float):
        m = re.search(rf"\b{field}\s*=\s*(-?[\d.]+)f?\s*;", src)
        if not m:
            sys.exit(f"measure_mandelbulb_flora: could not read '{field}' from {FLORA_CS.name}")
        return cast(m.group(1))

    powers, scales = {}, {}
    for block in re.findall(r"new\(\)\s*\{([^}]*)\}", src):
        elem = re.search(r"Element\s*=\s*Element\.(\w+)", block)
        pw = re.search(r"Power\s*=\s*(\d+)", block)
        if not elem or not pw:
            continue
        powers[elem.group(1)] = int(pw.group(1))
        sc = re.search(r"PlateScale\s*=\s*([\d.]+)f", block)
        if sc and float(sc.group(1)) > 0.0:
            scales[elem.group(1)] = float(sc.group(1))
    if not powers:
        sys.exit("measure_mandelbulb_flora: no formByElement entries found")

    return {
        "powers": powers,
        "scales": scales,
        "power_fallback": num("power", int),
        "pitch": num("latticePitch"),
        "iterations": num("escapeIterations", int),
        "bailout": num("bailout"),
        "shell_radius": num("shellRadius"),
        "budget": num("maxTotalSpawnedObjects", int),
        "rules": {
            "riser": num("riserBias"),
            "coplanar": num("coplanarCos"),
            "tau": num("planarTau"),
            "max_cells": num("maxPatchCells", int),
            "pad": num("platePad"),
            "thickness": num("plateThickness"),
            "drop": num("containDrop"),
        },
    }


def circumscribing_scale():
    """OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE - read, never assumed, because it is the
    factor that turns a fitted prism into a fused plant if it ever moves."""
    m = re.search(r"CIRCUMSCRIBING_SCALE\s*=\s*([\d.]+)f", OCTAHEDRON_CS.read_text())
    if not m:
        sys.exit("measure_mandelbulb_flora: could not read CIRCUMSCRIBING_SCALE")
    return float(m.group(1))


def self_test():
    """Closed-form controls for touching_scale - a fitter nobody has watched fail is not a gate."""
    e = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))

    def octa(centre):
        return {"c": centre, "axes": tuple(unit((sx, sy, sz)) for sx in (1, -1)
                                           for sy in (1, -1) for sz in (1, -1)),
                "support": lambda u: max(abs(dot(u, s)) for s in e), "radius": 1.0}

    got = touching_scale(octa((0.0, 0.0, 0.0)), octa((3.0, 0.0, 0.0)))
    assert abs(got - 1.5) < 1e-9, f"octahedron axis control: expected 1.5, got {got}"

    def box(centre):
        return {"c": centre, "axes": e,
                "support": lambda u: sum(0.5 * abs(dot(u, e[i])) for i in range(3)), "radius": 0.87}

    got = touching_scale(box((0.0, 0.0, 0.0)), box((3.0, 0.0, 0.0)))
    assert abs(got - 3.0) < 1e-9, f"box control: expected 3.0, got {got}"
    return True


# ── render ────────────────────────────────────────────────────────────────────

def encode_png(w, h, rgb):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        raw += rgb[y * w * 3:(y + 1) * w * 3]

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff))

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(raw), 6)) + chunk(b"IEND", b""))


CORNERS = [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
           (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]
BOX_FACES = [([0, 3, 2, 1], 2, -1), ([4, 5, 6, 7], 2, 1), ([0, 1, 5, 4], 1, -1),
             ([3, 7, 6, 2], 1, 1), ([0, 4, 7, 3], 0, -1), ([1, 2, 6, 5], 0, 1)]


def render(plates, path, w=560, eye=(0.75, 0.45, 1.0), light=(0.45, 0.85, 0.40)):
    """An ORIENTED-BOX z-buffer render. Screen-aligned squares cannot show what this species is
    for - a prism's own frame, and the fact that no two of them share one."""
    fwd = unit(eye)
    right = unit(cross((0, 1, 0), fwd))
    up = cross(fwd, right)
    key = unit(light)

    verts = []
    for p in plates:
        c, e, s = p["c"], p["e"], p["size"]
        h = (s[0] * 0.5, s[1] * 0.5, s[2] * 0.5)
        vs = []
        for sx, sy, sz in CORNERS:
            world = tuple(c[k] + e[0][k] * sx * h[0] + e[1][k] * sy * h[1] + e[2][k] * sz * h[2]
                          for k in range(3))
            vs.append((dot(world, right), dot(world, up), dot(world, fwd)))
        verts.append(vs)
    if not verts:
        return

    xs = [v[0] for vs in verts for v in vs]
    ys = [v[1] for vs in verts for v in vs]
    cx, cy = (min(xs) + max(xs)) * 0.5, (min(ys) + max(ys)) * 0.5
    ext = max(max(xs) - min(xs), max(ys) - min(ys)) * 0.53 or 1.0
    sc = w * 0.5 / ext

    fb = bytearray(b"\x0b\x0c\x12" * (w * w))
    zb = [1e30] * (w * w)

    def tri(p0, p1, p2, col):
        minx = max(0, int(math.floor(min(p0[0], p1[0], p2[0]))))
        maxx = min(w - 1, int(math.ceil(max(p0[0], p1[0], p2[0]))))
        miny = max(0, int(math.floor(min(p0[1], p1[1], p2[1]))))
        maxy = min(w - 1, int(math.ceil(max(p0[1], p1[1], p2[1]))))
        den = (p1[1] - p2[1]) * (p0[0] - p2[0]) + (p2[0] - p1[0]) * (p0[1] - p2[1])
        if abs(den) < 1e-9 or minx > maxx:
            return
        inv = 1.0 / den
        r, g, b = col
        for py in range(miny, maxy + 1):
            fy = py + 0.5
            row = py * w
            for px in range(minx, maxx + 1):
                fx = px + 0.5
                w0 = ((p1[1] - p2[1]) * (fx - p2[0]) + (p2[0] - p1[0]) * (fy - p2[1])) * inv
                if w0 < 0.0 or w0 > 1.0:
                    continue
                w1 = ((p2[1] - p0[1]) * (fx - p2[0]) + (p0[0] - p2[0]) * (fy - p2[1])) * inv
                if w1 < 0.0 or w1 > 1.0:
                    continue
                w2 = 1.0 - w0 - w1
                if w2 < 0.0:
                    continue
                z = w0 * p0[2] + w1 * p1[2] + w2 * p2[2]
                i = row + px
                if z >= zb[i]:
                    continue
                zb[i] = z
                o = i * 3
                fb[o] = r; fb[o + 1] = g; fb[o + 2] = b

    for pi, p in enumerate(plates):
        e = p["e"]
        for idx, axis, sign in BOX_FACES:
            n = tuple(e[axis][k] * sign for k in range(3))
            if dot(n, fwd) <= 0.0:
                continue
            lam = max(0.0, dot(n, key))
            k = 0.20 + 0.80 * (lam ** 0.75)
            col = tuple(min(255, int(c * k)) for c in (208, 214, 226))
            q = [((verts[pi][i][0] - cx) * sc + w * 0.5,
                  w * 0.5 - (verts[pi][i][1] - cy) * sc, -verts[pi][i][2]) for i in idx]
            tri(q[0], q[1], q[2], col)
            tri(q[0], q[2], q[3], col)

    with open(path, "wb") as fh:
        fh.write(encode_png(w, w, bytes(fb)))


# ── report ────────────────────────────────────────────────────────────────────

def pct(sorted_values, q):
    return sorted_values[min(len(sorted_values) - 1, int(q * len(sorted_values)))]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="exit 1 on drift from the shipped C#")
    ap.add_argument("--render", metavar="DIR")
    ap.add_argument("--fit", action="store_true")
    args = ap.parse_args()

    self_test()
    a = authored()
    pitch, rules = a["pitch"], a["rules"]
    radius, budget = a["shell_radius"], a["budget"]
    world_pitch = pitch * radius
    bound = bound_for(pitch)
    shield = circumscribing_scale()
    failures = []

    print(f"Mandelbulb flora - offline model   (pitch {pitch}, iters {a['iterations']}, "
          f"bailout {a['bailout']:g}, shellRadius {radius:g})")
    print(f"  world cell {world_pitch:.3f}u   plating: riser {rules['riser']:g}, coplanar "
          f"{rules['coplanar']:g}, tau {rules['tau']:g} cells, patch <= {rules['max_cells']} cells, "
          f"pad {rules['pad']:g}, thickness {rules['thickness']:g} cells, drop {rules['drop']:g}")
    print(f"  authored per-plant budget {budget}\n")

    print(f"  {'element':<8} {'power':>5} {'cells':>7} {'plated':>7} {'prisms':>7} {'radius':>7} "
          f"{'long side p10/50/90/max (u)':>30} {'aspect p50/p90':>15} {'volume':>10} {'%budget':>8}")
    grown = {}
    total_volume = 0.0
    for elem in ("Charge", "Mass", "Space", "Time"):
        power = a["powers"].get(elem)
        if power is None:
            failures.append(f"{elem} has no authored power")
            continue
        bulb = Bulb(power, pitch, a["iterations"], a["bailout"])
        order, selected, plates = plating(bulb, rules, bound)
        scale = a["scales"].get(elem, 1.0)
        grown[elem] = (bulb, plates, scale)

        k = world_pitch * scale
        longs = sorted(max(p["size"][0], p["size"][1]) * k for p in plates)
        asp = sorted(max(p["size"][0], p["size"][1]) / max(1e-9, min(p["size"][0], p["size"][1]))
                     for p in plates)
        maxr = max(math.sqrt(dot(p["c"], p["c"])) for p in plates) * world_pitch
        volume = sum(p["size"][0] * p["size"][1] * p["size"][2] for p in plates) * k ** 3
        total_volume += volume
        span = (f"{pct(longs,.1):.1f}/{pct(longs,.5):.1f}/{pct(longs,.9):.1f}/{longs[-1]:.1f}")
        print(f"  {elem:<8} {power:>5} {len(order):>7} {len(selected):>7} {len(plates):>7} "
              f"{maxr:>6.0f}u {span:>30} "
              f"{pct(asp,.5):>7.2f}/{pct(asp,.9):<7.2f} {volume:>10,.0f} "
              f"{100.0*len(plates)/budget:>7.0f}%")

        if len(plates) > budget:
            failures.append(
                f"{elem} (power {power}) needs {len(plates)} prisms but the authored budget is "
                f"{budget} - a plant of this element can never complete its form")

    print(f"\n  a full set of all four = {sum(len(p) for _, p, _ in grown.values()):,} prisms, "
          f"{total_volume:,.0f} volume")

    if args.fit or args.check:
        print(f"\n  plate fit - exact separating-axis over each element's own measured plates")
        print(f"  (a CHARGE plant's leaves are SHIELDED by law, and a shield replaces the box with "
              f"the\n   octahedron circumscribing it - {shield:g}x the HALF-extents, "
              f"Docs/ECOSYSTEM.md §35 - so Charge is\n   fitted against its ARMOUR and the other "
              f"three against the box they draw):\n")
        print(f"    {'element':<8} {'body':<11} {'interpenetrating':>18} {'deepest s*':>11} "
              f"{'scale':>7}")

        bare_fraction = {}
        armoured_fraction = {}
        for elem, (bulb, plates, scale) in grown.items():
            pairs, over, worst = pair_report(plates, lambda p, s=scale: as_box(p, s))
            frac = over / max(1, pairs)
            bare_fraction[elem] = frac
            print(f"    {elem:<8} {'box':<11} {over:>7} of {pairs:<7} {worst:>11.3f} {scale:>7.3f}")
            if frac > MAX_INTERPENETRATING_FRACTION:
                failures.append(
                    f"{elem}: {100*frac:.1f}% of touching plate pairs interpenetrate, over the "
                    f"stated {100*MAX_INTERPENETRATING_FRACTION:.0f}% bound - the plant is fusing")
            if worst < rules["drop"] - 1e-3:
                failures.append(
                    f"{elem}: two plates interleave to s* {worst:.3f}, deeper than containDrop "
                    f"{rules['drop']:g} - the drop is not bounding what it claims to")

        for elem, (bulb, plates, scale) in grown.items():
            if elem not in a["scales"]:
                continue
            pairs, over, worst = pair_report(plates, lambda p, s=scale: as_octahedron(p, shield, s))
            armoured_fraction[elem] = over / max(1, pairs)
            print(f"    {elem:<8} {'octahedron':<11} {over:>7} of {pairs:<7} {worst:>11.3f} "
                  f"{scale:>7.3f}")

        # An armoured element's bar is its SIBLINGS: a Charge plant wearing its shields must be no
        # more fused than an ordinary plant is bare. That is a measured bar rather than an
        # invented one, and it moves if the species is ever retuned.
        peer_worst = max((f for e, f in bare_fraction.items() if e not in a["scales"]), default=0.0)
        for elem, frac in armoured_fraction.items():
            if frac > peer_worst:
                failures.append(
                    f"{elem} armoured: {100*frac:.1f}% of its octahedron pairs interpenetrate, "
                    f"worse than the worst BARE element ({100*peer_worst:.1f}%) - a shielded "
                    f"{elem} plant would read more solid than an ordinary one")

        # Charge's density inversion: sparse bare, DENSEST armoured. Measured as the silhouette a
        # plant's prisms present - the box's is 4 x h0 h1, the circumscribing octahedron's rhombus
        # is 18 x h0 h1, so armouring multiplies a plant's own coverage by exactly 4.5.
        octahedron_gain = 0.5 * shield ** 2
        for elem, (bulb, plates, scale) in grown.items():
            if elem not in a["scales"]:
                continue
            bare = sum(p["size"][0] * p["size"][1] for p in plates) * scale ** 2
            armoured = bare * octahedron_gain
            peers = [sum(q["size"][0] * q["size"][1] for q in pl) * sc ** 2
                     for e2, (_, pl, sc) in grown.items() if e2 not in a["scales"]]
            peer = sum(peers) / len(peers) if peers else 0.0
            print(f"\n    {elem} covers {bare:,.0f} cells^2 bare and {armoured:,.0f} armoured "
                  f"({octahedron_gain:g}x), against the other elements' bare {peer:,.0f} "
                  f"({armoured/max(peer,1e-9):.2f}x)")
            if armoured <= peer:
                failures.append(
                    f"{elem}'s armoured footprint ({armoured:,.0f}) no longer exceeds the other "
                    f"elements' bare plate ({peer:,.0f}) - the shielded plant would read SPARSER "
                    f"than its siblings, which inverts the element")

    if args.render:
        os.makedirs(args.render, exist_ok=True)
        for elem, (bulb, plates, scale) in grown.items():
            path = os.path.join(args.render, f"mandelbulb_{elem.lower()}.png")
            render(plates, path)
            print(f"  wrote {path}")

    if failures:
        print("\nFAIL")
        for f in failures:
            print(f"  - {f}")
        return 1
    if args.check:
        print("\nOK - the shipped constants are self-consistent and every element can complete.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
