#!/usr/bin/env python3
"""
The offline MODEL for the Mandelbulb flora species - the authority for its prism counts, its
volume ladder, its fitted plate and the two measurements its C# cites in comments.

This is a FRESH transcription from the implicit function, deliberately independent of
Assets/_Scripts/Controller/Environment/FloraAndFauna/MandelbulbLattice.cs. It is the
"measurement" half of the pair; verify_mandelbulb_flora_tables.py is the "the shipped file really
does this" half, and the two are separate scripts on purpose - the transcription from a proven
measurement into the asset is the step neither the measurement nor code review can see
(Docs/ECOSYSTEM.md 34, the Schwarz P precedent).

Usage
    measure_mandelbulb_flora.py                 table + the authored-constant audit
    measure_mandelbulb_flora.py --check         fail (exit 1) on any drift from the shipped C#
    measure_mandelbulb_flora.py --render DIR    write orthographic PNGs of each element's bulb
    measure_mandelbulb_flora.py --cavities      re-measure the sealed-void claim in the C# comment
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

FACE6 = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
N26 = [(a, b, c) for a in (-1, 0, 1) for b in (-1, 0, 1) for c in (-1, 0, 1) if (a, b, c) != (0, 0, 0)]


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
    """Membership + shell + normals on the ambient integer lattice, memoised."""

    def __init__(self, power, pitch, iterations=10, bailout=2.0):
        self.power, self.pitch = power, pitch
        self.iterations, self.bailout = iterations, bailout
        self._in = {}

    def inside(self, site):
        v = self._in.get(site)
        if v is None:
            v = inside(site[0] * self.pitch, site[1] * self.pitch, site[2] * self.pitch,
                       self.power, self.iterations, self.bailout)
            self._in[site] = v
        return v

    def is_shell(self, site):
        if not self.inside(site):
            return False
        return any(not self.inside((site[0] + a, site[1] + b, site[2] + c)) for a, b, c in FACE6)

    def normal(self, site):
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
        """The reachable shell: the 26-neighbour walk MandelbulbFlora grows with, from its seed."""
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
        return order


def bound_for(pitch):
    return math.ceil(1.45 / pitch)


# ── what the C# actually says ─────────────────────────────────────────────────

def authored():
    """Parse the shipped MandelbulbFlora.cs so this tool cannot drift from the asset."""
    src = FLORA_CS.read_text()

    def num(field, cast=float):
        m = re.search(rf"\b{field}\s*=\s*(-?[\d.]+)f?\s*;", src)
        if not m:
            sys.exit(f"measure_mandelbulb_flora: could not read '{field}' from {FLORA_CS.name}")
        return cast(m.group(1))

    powers, plates = {}, {}
    for block in re.findall(r"new\(\)\s*\{([^}]*)\}", src):
        elem = re.search(r"Element\s*=\s*Element\.(\w+)", block)
        pw = re.search(r"Power\s*=\s*(\d+)", block)
        if not elem or not pw:
            continue
        powers[elem.group(1)] = int(pw.group(1))
        pl = re.search(r"Plate\s*=\s*new\(([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f\)", block)
        if pl:
            vals = tuple(float(g) for g in pl.groups())
            if any(v > 0 for v in vals):
                plates[elem.group(1)] = vals
    if not powers:
        sys.exit("measure_mandelbulb_flora: no formByElement entries found")

    plate = re.search(r"plateScale\s*=\s*new\(([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f\)", src)
    if not plate:
        sys.exit("measure_mandelbulb_flora: could not read plateScale")

    return {
        "powers": powers,
        "power_fallback": num("power", int),
        "pitch": num("latticePitch"),
        "iterations": num("escapeIterations", int),
        "bailout": num("bailout"),
        "shell_radius": num("shellRadius"),
        "plate": tuple(float(g) for g in plate.groups()),
        "plates": plates,
        "budget": num("maxTotalSpawnedObjects", int),
    }


OCTAHEDRON_CS = ROOT / "Assets/_Scripts/Utility/OctahedronMeshGenerator.cs"


def circumscribing_scale():
    """OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE - read, never assumed, because it is the
    factor that turns a fitted prism into a fused plant if it ever moves."""
    m = re.search(r"CIRCUMSCRIBING_SCALE\s*=\s*([\d.]+)f", OCTAHEDRON_CS.read_text())
    if not m:
        sys.exit("measure_mandelbulb_flora: could not read CIRCUMSCRIBING_SCALE")
    return float(m.group(1))


def plate_for(authored_values, element):
    """This element's plate, in multiples of the lattice pitch."""
    return authored_values["plates"].get(element, authored_values["plate"])


# ── plate fitting (exact OBB / separating-axis) ───────────────────────────────

def basis(normal):
    """The plate's frame: +z along the normal, mirroring MandelbulbFlora.PlateRotation."""
    ref = (1.0, 0.0, 0.0) if abs(normal[2]) > 0.95 else (0.0, 0.0, 1.0)
    t = cross(normal, ref)
    if dot(t, t) < 1e-12:
        t = cross(normal, (0.0, 1.0, 0.0))
    t = unit(t)
    # Unity's LookRotation(forward=normal, up=tangent): z = normal, x = up x z, y = z x x
    ax = unit(cross(t, normal))
    ay = cross(normal, ax)
    return ax, ay, normal


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def unit(v):
    m = math.sqrt(dot(v, v))
    return (v[0] / m, v[1] / m, v[2] / m) if m > 1e-12 else (0.0, 0.0, 1.0)


def support_box(u, axes, half):
    """Support radius of an oriented box along u."""
    return sum(half[i] * abs(dot(u, axes[i])) for i in range(3))


def support_octa(u, semi):
    """Support radius of the octahedron conv{+-S0, +-S1, +-S2} along u."""
    return max(abs(dot(u, s)) for s in semi)


def box_axes(frame):
    return list(frame)


def box_cross_axes(fa, fb):
    out = list(fa) + list(fb)
    for i in range(3):
        for j in range(3):
            ax = cross(fa[i], fb[j])
            if dot(ax, ax) > 1e-12:
                out.append(unit(ax))
    return out


def octa_faces(semi):
    """The 8 face normals: one per sign octant of conv{+-S0, +-S1, +-S2}."""
    out = []
    for sx in (1, -1):
        for sy in (1, -1):
            for sz in (1, -1):
                a = tuple(sx * t for t in semi[0])
                b = tuple(sy * t for t in semi[1])
                c = tuple(sz * t for t in semi[2])
                n = cross((b[0]-a[0], b[1]-a[1], b[2]-a[2]), (c[0]-a[0], c[1]-a[1], c[2]-a[2]))
                if dot(n, n) > 1e-18:
                    out.append(unit(n))
    return out


def octa_edges(semi):
    """6 distinct edge directions - opposite edges of an octahedron are parallel."""
    out = []
    for i, j in ((0, 1), (0, 2), (1, 2)):
        a, b = semi[i], semi[j]
        for sign in (1, -1):
            e = (b[0] + sign*a[0], b[1] + sign*a[1], b[2] + sign*a[2])
            if dot(e, e) > 1e-18:
                out.append(unit(e))
    return out


def touching_scale(d, axes, ra, rb):
    """The largest uniform scale at which two CENTRALLY SYMMETRIC convex bodies still separate.

    Both bodies are symmetric about their own prism centre, so scaling by s scales every
    projection radius by s while the centre offset d is fixed:

        s* = max over candidate axes of  |d.u| / (rA(u) + rB(u))

    which is EXACT rather than a bisection - below s* some axis separates them, above it none
    does. s* >= 1 means the pair is clear as authored. Transcribed from the numpy version in
    Tools/Build/fit_shield_clearance.py, which owns the same question for the gyroid and
    Schwarz P Charge leaves; pure stdlib here because nothing else in this tool needs numpy.
    Self-tested against two closed-form octahedron cases - see self_test().
    """
    best = 0.0
    for u in axes:
        denom = ra(u) + rb(u)
        if denom <= 1e-12:
            continue
        best = max(best, abs(dot(d, u)) / denom)
    return best


def near_pairs(sites):
    index = set(sites)
    out = []
    for s in sites:
        for a, b, c in N26:
            n = (s[0] + a, s[1] + b, s[2] + c)
            if n in index and n > s:
                out.append((s, n))
    return out


def measure_fit(bulb, sites, plate, shield=None):
    """Largest uniform scale of `plate` at which no two neighbouring prisms interpenetrate.

    shield=None measures the BOX a prism draws unshielded; shield=<CIRCUMSCRIBING_SCALE>
    measures the octahedron a CHARGE plant's armour engages (Docs/ECOSYSTEM.md 35: the shield
    replaces the box with the octahedron circumscribing it, reaching shield/2 x leafSize from
    the centre along each local axis).

    Returns (overlapping_pairs, tested_pairs, largest_clear_scale).
    """
    half = tuple(0.5 * p for p in plate)
    frames = {s: basis(bulb.normal(s)) for s in sites}
    pairs = near_pairs(sites)
    worst = float("inf")
    bad = 0
    for s, n in pairs:
        fa, fb = frames[s], frames[n]
        d = (n[0] - s[0], n[1] - s[1], n[2] - s[2])
        if shield is None:
            axes = box_cross_axes(fa, fb)
            ra = lambda u, f=fa: support_box(u, f, half)
            rb = lambda u, f=fb: support_box(u, f, half)
        else:
            sa = [tuple(fa[i][t] * half[i] * shield for t in range(3)) for i in range(3)]
            sb = [tuple(fb[i][t] * half[i] * shield for t in range(3)) for i in range(3)]
            axes = octa_faces(sa) + octa_faces(sb) + [
                unit(c) for c in (cross(ea, eb) for ea in octa_edges(sa) for eb in octa_edges(sb))
                if dot(c, c) > 1e-12]
            ra = lambda u, S=sa: support_octa(u, S)
            rb = lambda u, S=sb: support_octa(u, S)
        s_star = touching_scale(d, axes, ra, rb)
        worst = min(worst, s_star)
        if s_star < 1.0:
            bad += 1
    return bad, len(pairs), (worst if pairs else float("inf"))


def self_test():
    """Closed-form controls for touching_scale - a fitter nobody has watched fail is not a gate."""
    e = [(1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)]
    axes = octa_faces(e) + octa_faces(e) + [
        unit(c) for c in (cross(a, b) for a in octa_edges(e) for b in octa_edges(e))
        if dot(c, c) > 1e-12]
    ra = rb = lambda u: support_octa(u, e)
    # Two unit octahedra offset along +x touch vertex-to-vertex at separation 2, so s* = d/2.
    got = touching_scale((3.0, 0.0, 0.0), axes, ra, rb)
    assert abs(got - 1.5) < 1e-9, f"axis-offset control: expected 1.5, got {got}"
    # Offset along (1,1,1): support there is 1/sqrt(3), so they touch at d = 2/sqrt(3).
    d = (1.0, 1.0, 1.0)
    got = touching_scale(d, axes, ra, rb)
    expect = math.sqrt(3.0) * math.sqrt(3.0) / 2.0
    assert abs(got - expect) < 1e-9, f"diagonal control: expected {expect}, got {got}"
    # A box control: two unit cubes offset 3 along x separate until scale 3.
    fa = fb = (e[0], e[1], e[2])
    half = (0.5, 0.5, 0.5)
    got = touching_scale((3.0, 0.0, 0.0), box_cross_axes(fa, fb),
                         lambda u: support_box(u, fa, half), lambda u: support_box(u, fb, half))
    assert abs(got - 3.0) < 1e-9, f"box control: expected 3.0, got {got}"
    return True


# ── cavity measurement (the claim in MandelbulbLattice.IsShellSite) ───────────

def measure_cavities(power, pitch, iterations, bailout):
    """How many shell sites are walls of SEALED voids - i.e. what the local test over-counts."""
    b = Bulb(power, pitch, iterations, bailout)
    n = bound_for(pitch)
    rng = range(-n, n + 1)
    local = [s for s in ((i, j, k) for i in rng for j in rng for k in rng) if b.is_shell(s)]
    start = (-n, -n, -n)
    outside = set()
    if not b.inside(start):
        outside.add(start)
        q = deque([start])
        while q:
            i, j, k = q.popleft()
            for a, bb, c in FACE6:
                nb = (i + a, j + bb, k + c)
                if nb in outside or max(abs(t) for t in nb) > n or b.inside(nb):
                    continue
                outside.add(nb)
                q.append(nb)
    reachable = [s for s in local
                 if any((s[0] + a, s[1] + bb, s[2] + c) in outside for a, bb, c in FACE6)]
    return len(local), len(reachable)


# ── render ────────────────────────────────────────────────────────────────────

def encode_png(rows, w, h):
    raw = bytearray()
    for row in rows:
        raw.append(0)
        for r, g, b in row:
            raw += bytes((r, g, b))

    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xffffffff)

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))


def render(bulb, sites, plate=(1.0, 1.0, 1.0), w=460, view=(1.0, 0.5, 0.34), tint=(1.0, 0.86, 0.46)):
    fwd = unit(view)
    up0 = (0.0, 0.0, 1.0) if abs(fwd[2]) < 0.9 else (0.0, 1.0, 0.0)
    right = unit(cross(fwd, up0))
    up = cross(right, fwd)
    key = unit((0.42, 0.26, 0.87))
    extent = max(math.sqrt(sum((t * bulb.pitch) ** 2 for t in s)) for s in sites) * 1.12
    scale = w / (2.0 * extent)
    # HALF the plate's true footprint - judge the species at the size it will be judged.
    half = max(1, int(round(bulb.pitch * max(plate[0], plate[1]) * scale * 0.5)))
    buf = [[(9, 10, 16)] * w for _ in range(w)]
    zb = [[-1e9] * w for _ in range(w)]
    for s in sites:
        p = tuple(t * bulb.pitch for t in s)
        d = dot(p, fwd)
        cx = int(w / 2 + dot(p, right) * scale)
        cy = int(w / 2 - dot(p, up) * scale)
        lam = max(0.0, dot(bulb.normal(s), key))
        sh = 0.22 + 0.92 * lam
        col = tuple(int(255 * min(1.0, tint[t] * sh)) for t in range(3))
        for yy in range(cy - half, cy + half + 1):
            if not 0 <= yy < w:
                continue
            row, zr = buf[yy], zb[yy]
            for xx in range(cx - half, cx + half + 1):
                if not 0 <= xx < w:
                    continue
                if d > zr[xx]:
                    zr[xx] = d
                    row[xx] = col
    return encode_png(buf, w, w)


# ── report ────────────────────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="exit 1 on drift from the shipped C#")
    ap.add_argument("--render", metavar="DIR")
    ap.add_argument("--cavities", action="store_true")
    ap.add_argument("--fit", action="store_true")
    args = ap.parse_args()

    a = authored()
    pitch, iters, bail = a["pitch"], a["iterations"], a["bailout"]
    radius, plate = a["shell_radius"], a["plate"]
    world_pitch = pitch * radius
    prism_vol = world_pitch ** 3 * plate[0] * plate[1] * plate[2]
    bound = bound_for(pitch)
    failures = []

    print(f"Mandelbulb flora - offline model   (pitch {pitch}, iters {iters}, bailout {bail:g}, "
          f"shellRadius {radius:g})")
    print(f"  world pitch {world_pitch:.3f}u   default plate "
          f"{plate[0]:g}x{plate[1]:g}x{plate[2]:g} = {world_pitch*plate[0]:.2f} x "
          f"{world_pitch*plate[1]:.2f} x {world_pitch*plate[2]:.2f}u"
          f"   (per-element plates below)")
    print(f"  authored per-plant budget {a['budget']}\n")

    print(f"  {'element':<8} {'power':>5} {'prisms':>7} {'span':>5} {'maxR(u)':>8} "
          f"{'diam(w)':>8} {'vol/prism':>9} {'volume':>10} {'%budget':>8}")
    total = 0
    totals_volume = 0.0
    bulbs = {}
    for elem in ("Charge", "Mass", "Space", "Time"):
        power = a["powers"].get(elem)
        if power is None:
            failures.append(f"{elem} has no authored power")
            continue
        b = Bulb(power, pitch, iters, bail)
        sites = b.walk(bound)
        bulbs[elem] = (b, sites)
        span = max(max(abs(t) for t in s) for s in sites) * 2 + 1
        maxr = max(math.sqrt(sum(t * t for t in s)) for s in sites) * pitch
        pl = plate_for(a, elem)
        elem_prism_vol = world_pitch ** 3 * pl[0] * pl[1] * pl[2]
        vol = len(sites) * elem_prism_vol
        total += len(sites)
        pct = 100.0 * len(sites) / a["budget"]
        totals_volume += vol
        print(f"  {elem:<8} {power:>5} {len(sites):>7} {span:>5} {maxr:>8.3f} "
              f"{2*maxr*radius:>8.1f} {elem_prism_vol:>8.2f} {vol:>10,.0f} {pct:>7.0f}%")
        if len(sites) > a["budget"]:
            failures.append(
                f"{elem} (power {power}) needs {len(sites)} prisms but the authored budget is "
                f"{a['budget']} - a plant of this element can never complete its form")

    print(f"\n  a full set of all four = {total:,} prisms, {totals_volume:,.0f} volume")

    if args.fit or args.check:
        shield_scale = circumscribing_scale()
        print(f"\n  plate fit - exact separating-axis over each element's own measured sites")
        print(f"  (a CHARGE plant's leaves are SHIELDED by law, and a shield replaces the box "
              f"with the\n   octahedron circumscribing it - {shield_scale:g}x the HALF-extents, "
              f"Docs/ECOSYSTEM.md 35 - so Charge is\n   fitted against its ARMOUR and the other "
              f"three against the box they draw):\n")
        print(f"    {'element':<8} {'body':<11} {'overlap pairs':>14} {'clear at':>9} {'fitted plate (xyz mult)':>26}")
        for elem, (b, sites) in bulbs.items():
            pl = plate_for(a, elem)
            shield = shield_scale if elem == "Charge" else None
            bad, pairs, clear = measure_fit(b, sites, pl, shield=shield)
            fitted = tuple(round(x * clear, 3) for x in pl)
            body = "octahedron" if shield else "box"
            print(f"    {elem:<8} {body:<11} {bad:>6} of {pairs:<5} {clear:>9.3f} "
                  f"{str(fitted):>26}")
            if bad:
                failures.append(
                    f"{elem} {body}s interpenetrate at the authored plate {pl} "
                    f"({bad}/{pairs} neighbour pairs) - it clears only at x{clear:.3f}, "
                    f"i.e. plate {fitted}")
            if shield:
                # What the player actually sees on a CHARGE plant: its leaves are shielded by law,
                # so the body drawn is the octahedron, not the plate. Fitted for its ARMOUR, the
                # plate is sparse bare and DENSER THAN ANY OTHER ELEMENT armoured - which is the
                # gameplay read (a Charge bulb looks solid until you strip it, then it is a
                # skeleton you can graze).
                bare = max(pl[0], pl[1])
                armoured = bare * shield_scale
                default = max(a["plate"][0], a["plate"][1])
                print(f"    {'':<8} {'':<11} {'':>14} {'':>9} "
                      f"bare {bare:.3f} of pitch, armoured {armoured:.3f} "
                      f"(other elements bare {default:.3f})")
                if armoured <= default:
                    failures.append(
                        f"Charge's armoured footprint ({armoured:.3f} of the pitch) no longer "
                        f"exceeds the other elements' bare plate ({default:.3f}) - the shielded "
                        f"plant would read SPARSER than its siblings, which inverts the element")

    if args.cavities:
        print("\n  sealed-void audit (the claim in MandelbulbLattice.IsShellSite):")
        for p in (0.085, pitch):
            local, reach = measure_cavities(a["powers"]["Charge"], p, iters, bail)
            print(f"    power {a['powers']['Charge']} pitch {p:<6g} local test {local:>5}, "
                  f"outside-reachable {reach:>5}  -> {local-reach} cavity-wall sites "
                  f"({100.0*(local-reach)/local:.1f}%)")

    if args.render:
        os.makedirs(args.render, exist_ok=True)
        for elem, (b, sites) in bulbs.items():
            path = os.path.join(args.render, f"mandelbulb_{elem.lower()}.png")
            with open(path, "wb") as fh:
                fh.write(render(b, sites, plate_for(a, elem)))
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
