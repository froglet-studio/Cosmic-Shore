#!/usr/bin/env python3
"""
Verify the SHIPPED SchwarzPTileData.cs against a fresh reference walk of the
Schwarz P surface.

    python3 Tools/Build/verify_schwarz_p_tile_tables.py

Run it after ANY edit to the measured table or to the tile arithmetic.

WHY THIS EXISTS SEPARATELY FROM THE MEASURING SCRIPT
----------------------------------------------------
measure_schwarz_p_tile.py proves the GEOMETRY. It cannot prove the FILE: between
the measurement and the asset there is a transcription, and that step is invisible
to both the measurement and to code review. The gyroid octagon tables were
validated end-to-end as rotation matrices and still shipped wrong, because three
of four rows were filled in by a plausible symmetry ansatz after the fact - a
defect that cost five playtests because every plant was internally perfect and
only the JOINS were wrong (Docs/ECOSYSTEM.md 32.7).

So this script reads the C# that actually ships, parses the table out of it, and
re-derives every claim from the implicit function - never from the measuring
script's own state:

  * every site lies on f = cos x + cos y + cos z = 0
  * every site is strictly inside its own half-period cube (so tiles never
    double-claim, and a prism's owner is unambiguous)
  * the site set is closed under the flat point's 12-element site group -3m,
    which is what makes a tile a unit the symmetry can carry anywhere
  * every tangent is unit and lies in the surface's tangent plane at its own site
  * the emitted mean spacing is the spacing the sites actually have
  * SiteParam's sign-flip rule (the linear part of T_ijk) really does land on the
    surface for tiles of every axis parity - checked against the surface, not
    against the rule that generated it
  * spacing across a tile seam is no worse than spacing inside a tile
"""

from __future__ import annotations

import itertools
import math
import pathlib
import re
import sys

PI = math.pi
ROOT = pathlib.Path(__file__).resolve().parents[2]
CS_PATH = ROOT / "Assets/_Scripts/Controller/Assemblers/SchwarzPTileData.cs"

# The C# table is printed at 7 decimals, so a site's f value carries about
# 3 x 5e-8 of rounding, plus float32 error in the pi/2 tile centre.
TOLERANCE = 1e-5


def f(p):
    return math.cos(p[0]) + math.cos(p[1]) + math.cos(p[2])


def normal(p):
    g = (-math.sin(p[0]), -math.sin(p[1]), -math.sin(p[2]))
    n = math.sqrt(sum(c * c for c in g))
    return (0.0, 1.0, 0.0) if n < 1e-8 else tuple(c / n for c in g)


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def dist(a, b):
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(a, b)))


def tile_center(ijk):
    return tuple(PI * (i + 0.5) for i in ijk)


def axis_signs(ijk):
    return tuple(1.0 if i % 2 == 0 else -1.0 for i in ijk)


def site_param(ijk, offset):
    """The C# SiteParam rule, re-implemented from the documented contract."""
    s = axis_signs(ijk)
    return add(tile_center(ijk), tuple(s[a] * offset[a] for a in range(3)))


def site_tangent(ijk, tangent):
    s = axis_signs(ijk)
    return tuple(s[a] * tangent[a] for a in range(3))


def neighbour_tile(ijk, delta):
    """SchwarzPTileData.NeighbourTile: a bond delta is measured in the canonical tile,
    and tile transforms do NOT add - T_a(T_b(x)) is T_(a-b) when a is odd, because an
    odd tile is a mirror image and a mirror reverses the step through it."""
    s = axis_signs(ijk)
    return tuple(ijk[a] + int(s[a]) * delta[a] for a in range(3))


def naive_neighbour_tile(ijk, delta):
    """The WRONG rule (`tile + delta`), kept so the gate below can prove it fails."""
    return tuple(ijk[a] + delta[a] for a in range(3))


# The flat point's site group -3m: the 6 coordinate permutations, each optionally
# composed with the inversion p -> (pi,pi,pi) - p.
SITE_OPS = [(perm, inv)
            for perm in itertools.permutations(range(3))
            for inv in (False, True)]


def apply_site_op(op, p):
    perm, inv = op
    q = tuple(p[i] for i in perm)
    return tuple(PI - c for c in q) if inv else q


# ----------------------------------------------------------------------------

SITE_RE = re.compile(
    r"new TileSite\(new Vector3\(\s*(-?[\d.]+)f,\s*(-?[\d.]+)f,\s*(-?[\d.]+)f\s*\),"
    r"\s*new Vector3\(\s*(-?[\d.]+)f,\s*(-?[\d.]+)f,\s*(-?[\d.]+)f\s*\)\)")
LEVEL_RE = re.compile(r"new TileLevel\(\s*([\d.]+)f,\s*new\[\]")
AREA_RE = re.compile(r"TilePatchParamArea\s*=\s*([\d.]+)f")


def parse_cs():
    if not CS_PATH.exists():
        raise SystemExit(f"missing {CS_PATH}")
    text = CS_PATH.read_text()

    begin = text.index("MEASURED TABLE BEGIN")
    end = text.index("MEASURED TABLE END")
    block = text[begin:end]

    area = AREA_RE.search(block)
    if not area:
        raise SystemExit("TilePatchParamArea not found in the measured block")

    levels = []
    starts = [m for m in LEVEL_RE.finditer(block)]
    for i, m in enumerate(starts):
        stop = starts[i + 1].start() if i + 1 < len(starts) else len(block)
        chunk = block[m.start():stop]
        sites = [((float(g[0]), float(g[1]), float(g[2])),
                  (float(g[3]), float(g[4]), float(g[5])))
                 for g in SITE_RE.findall(chunk)]
        levels.append((float(m.group(1)), sites))
    return float(area.group(1)), levels


# ----------------------------------------------------------------------------

FAILURES = []


def check(ok, label, detail=""):
    print(f"  [{'ok  ' if ok else 'FAIL'}] {label}" + (f"   {detail}" if detail else ""))
    if not ok:
        FAILURES.append(label)


PARITY_TILES = [(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1), (1, 1, 0),
                (1, 0, 1), (0, 1, 1), (1, 1, 1), (-1, 2, 3), (4, -3, -2)]


def verify_level(index, mean_spacing, sites):
    tag = f"level {index}"
    offsets = [s[0] for s in sites]
    tangents = [s[1] for s in sites]

    # --- on the surface, for tiles of EVERY axis parity. This is the check that
    # would catch a wrong sign rule as well as a mistyped offset.
    worst = 0.0
    worst_at = None
    for ijk in PARITY_TILES:
        for off in offsets:
            v = abs(f(site_param(ijk, off)))
            if v > worst:
                worst, worst_at = v, ijk
    check(worst < TOLERANCE, f"{tag}: every site lies on f = 0, in tiles of every parity",
          f"max |f| = {worst:.2e} (worst tile {worst_at})")

    # --- strictly inside its own cube: tiles never double-claim a prism
    margin = min(min(min(c, PI - c) for c in add(tile_center((0, 0, 0)), off))
                 for off in offsets)
    check(margin > 1e-4, f"{tag}: every site is strictly inside its own cube",
          f"min margin {margin:.4f} rad")

    # --- closed under the site group
    canonical = [add(tile_center((0, 0, 0)), off) for off in offsets]
    drift = max(min(dist(apply_site_op(op, p), q) for q in canonical)
                for p in canonical for op in SITE_OPS)
    check(drift < 1e-4, f"{tag}: the site set is closed under the 12 site ops (-3m)",
          f"max drift {drift:.2e}")

    # --- tangents: unit, and in the tangent plane at their OWN site
    worst_len = max(abs(math.sqrt(sum(c * c for c in t)) - 1.0) for t in tangents)
    worst_dot = 0.0
    for ijk in PARITY_TILES:
        for off, tan in zip(offsets, tangents):
            n = normal(site_param(ijk, off))
            t = site_tangent(ijk, tan)
            worst_dot = max(worst_dot, abs(sum(a * b for a, b in zip(n, t))))
    check(worst_len < 1e-5, f"{tag}: every tangent is unit length",
          f"max error {worst_len:.2e}")
    check(worst_dot < 1e-4, f"{tag}: every tangent lies in its site's tangent plane",
          f"max |n.t| = {worst_dot:.2e}")

    # --- spacing: measured against the 27-tile block, exactly as the layout was
    # relaxed. The site set is already closed under the site group, so composing
    # those ops back in would only duplicate every point twelve times.
    images = []
    for ijk in itertools.product((-1, 0, 1), repeat=3):
        images.extend(site_param(ijk, off) for off in offsets)
    nearest = []
    for p in canonical:
        nearest.append(min(d for d in (dist(p, q) for q in images) if d > 1e-6))
    measured = sum(nearest) / len(nearest)
    check(abs(measured - mean_spacing) < 1e-4,
          f"{tag}: emitted mean spacing matches the sites' actual spacing",
          f"emitted {mean_spacing:.6f} vs measured {measured:.6f}")

    # --- seam spacing: a layout relaxed only inside its own tile gets this wrong
    faces = ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))
    seam_images = [site_param(ijk, off) for ijk in faces for off in offsets]
    seam_min = min(min(dist(p, q) for q in seam_images) for p in canonical)
    intra_min = min(nearest)
    check(seam_min > intra_min * 0.92,
          f"{tag}: cross-seam spacing is no worse than intra-tile spacing",
          f"seam {seam_min:.4f} vs intra {intra_min:.4f} ({seam_min / intra_min:.2f}x)")

    # --- the bond-carrying rule. Canonical bonds: everything within 1.45x a site's own
    # nearest neighbour, which is what SchwarzPTileData.BuildBonds computes.
    canonical_bonds = {}
    for s in range(len(offsets)):
        p = canonical[s]
        cand = sorted(
            (dist(p, site_param(d, offsets[k])), d, k)
            for d in itertools.product((-1, 0, 1), repeat=3)
            for k in range(len(offsets))
            if dist(p, site_param(d, offsets[k])) > 1e-6)
        limit = cand[0][0] * 1.45
        canonical_bonds[s] = [(d, k, dd) for dd, d, k in cand if dd <= limit]

    # Carried into a tile of ANY parity, every bond must still land on a prism at the
    # same distance. This is checked against the SURFACE, not against the rule.
    worst_right = worst_naive = 0.0
    for ijk in PARITY_TILES:
        for s, blist in canonical_bonds.items():
            here = site_param(ijk, offsets[s])
            for d, k, expected in blist:
                right = dist(here, site_param(neighbour_tile(ijk, d), offsets[k]))
                naive = dist(here, site_param(naive_neighbour_tile(ijk, d), offsets[k]))
                worst_right = max(worst_right, abs(right - expected))
                worst_naive = max(worst_naive, abs(naive - expected))
    check(worst_right < 1e-4,
          f"{tag}: bonds carry correctly into tiles of every parity (NeighbourTile)",
          f"max distance error {worst_right:.2e}")
    # A gate nobody has seen fail is not a gate: the naive `tile + delta` rule must be
    # demonstrably wrong here, or the check above is not testing anything. Level 0 is
    # exempt and it is not a weakening - its single site sits ON the tile's centre of
    # symmetry, so T + delta and T - delta are mirror images at equal distance and no
    # measurement can separate them. Every level that has an off-centre site can.
    if len(offsets) > 1:
        check(worst_naive > 1.0,
              f"{tag}: the naive `tile + delta` rule is provably wrong (gate is live)",
              f"it misplaces a bond by up to {worst_naive:.2f} rad")
    else:
        print(f"  [n/a ] {tag}: single centred site - the two bond rules are mirror "
              f"images at equal distance, so this level cannot discriminate")

    return measured


def main():
    print("=" * 78)
    print("Verify the SHIPPED Schwarz P tile table")
    print(f"  {CS_PATH.relative_to(ROOT)}")
    print("=" * 78)

    area, levels = parse_cs()
    print(f"\nparsed {len(levels)} levels, tile patch area {area:.6f} rad^2\n")

    check(len(levels) > 0, "the measured table is present and parses")
    # The patch area is 1/8 of one cubic cell's surface, and the P surface's area
    # per cubic cell is 2.3451 a^2 - a literature value this file must reproduce.
    cell_area = area * 8 / ((2 * PI) ** 2)
    check(abs(cell_area - 2.3451) < 0.01,
          "tile patch area reproduces the literature 2.3451 a^2 per cubic cell",
          f"{cell_area:.4f} a^2")

    counts = []
    for index, (mean_spacing, sites) in enumerate(levels):
        print(f"\n{'-' * 78}\nlevel {index}: {len(sites)} sites, "
              f"emitted mean spacing {mean_spacing:.6f} rad")
        check(len(sites) > 0, f"level {index}: has sites")
        verify_level(index, mean_spacing, sites)
        counts.append(len(sites))

    # Site counts should be the CENTERED HEXAGONAL numbers 1 + 3n(n+1): the layout
    # is hexagonal shells around the flat point, which is the arrangement the tile's
    # own 6-fold symmetry admits. A count off this sequence means a shell came out
    # incomplete - a real defect, and invisible in a spacing statistic.
    hexagonal = [1 + 3 * n * (n + 1) for n in range(len(counts))]
    check(counts == hexagonal,
          "site counts are the centered hexagonal numbers (complete shells)",
          f"{counts}")

    # Levels must be ordered fine-to-coarse consistently, or ResolveLevel's
    # nearest-spacing search picks an arbitrary one of two equal candidates.
    spacings = [m for m, _ in levels]
    check(all(a > b for a, b in zip(spacings, spacings[1:])),
          "levels are ordered by strictly decreasing spacing",
          f"{[round(s, 4) for s in spacings]}")

    print("\n" + "=" * 78)
    if FAILURES:
        print(f"FAILED {len(FAILURES)} check(s):")
        for x in FAILURES:
            print(f"  - {x}")
        return 1
    print("all checks passed - the shipped table is the surface")
    print("=" * 78)
    return 0


if __name__ == "__main__":
    sys.exit(main())
