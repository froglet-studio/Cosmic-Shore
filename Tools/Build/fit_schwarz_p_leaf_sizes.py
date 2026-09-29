#!/usr/bin/env python3
"""
Fit the Schwarz P flora's per-element prism sizes to the tile's OWN site arrangement.

    python3 Tools/Build/fit_schwarz_p_leaf_sizes.py            # measure + report
    python3 Tools/Build/fit_schwarz_p_leaf_sizes.py --render   # + write a preview sheet
    python3 Tools/Build/fit_schwarz_p_leaf_sizes.py --write    # + author the four assets

WHY THIS IS MEASURED AND NOT AUTHORED BY EYE
--------------------------------------------
"The plates sit flush with no overlaps" is a geometric claim about a specific point
set, and the point set is the measured tile layout in SchwarzPTileData
(Docs/ECOSYSTEM.md 34).  Each prism is an oriented box: local +z is the surface
NORMAL (the thin axis), local +y is the site's baked tangent, and local +x is
cross(tangent, normal) - Unity's LookRotation(forward: normal, up: tangent).  So a
leaf size (x, y, z) is a footprint in the surface's tangent plane, and whether two
neighbouring footprints touch or interpenetrate is an exact OBB question, not a
matter of taste.

This script reads the SHIPPED table, builds every prism of a 3x3x3 tile block at the
authored periodScale, and runs a separating-axis test over every neighbouring pair -
including pairs across a tile seam, which is where a layout fitted inside one tile
would be wrong.  It then binary-searches, per aspect ratio, the largest footprint
that still has ZERO overlaps, and reports the surface coverage that buys.

The site arrangement is hexagonal shells around the flat point, so it is very nearly
isotropic: the coverage-maximizing aspect is near square, and pushing the aspect up
costs coverage in a way this script quantifies instead of guessing at.

THE FOUR ELEMENTS
-----------------
The gyroid already establishes the fleet pattern, and these follow it:

  Charge / Time  the reference pair - a pleasant aspect, flush, no overlaps
  Mass           chunkier: a squarer footprint and a thicker slab
  Space          the largest aspect - a long skinny STRUT that deliberately spans
                 several site spacings, so the structure reads skeletal and takes
                 up the largest bounds (the gyroid's Space is 20 x 1 x 1 against a
                 separationDistance of 3, i.e. 6.7 spacings - the same idea)

z stays thin on all four: these are plates lying ON a minimal surface, and a slab
thick enough to read as a block stops reading as a surface.
"""

from __future__ import annotations

import argparse
import itertools
import math
import pathlib
import re
import sys

import numpy as np

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from verify_schwarz_p_tile_tables import (  # noqa: E402
    CS_PATH, PI, parse_cs, site_param, site_tangent, normal as surface_normal,
)

ROOT = pathlib.Path(__file__).resolve().parents[2]
LIFEFORMS = ROOT / "Assets/_SO_Assets/Lifeforms"

# The authored SchwarzPFlora / SchwarzPBlock Variant values these are fitted against.
PERIOD_SCALE = 60.0
SEPARATION_DISTANCE = 6.0
WORLD_SCALE = PERIOD_SCALE / (2.0 * PI)

# SPACE scales its whole LATTICE (FloraVariantTuning.LatticeScale) rather than coarsening its
# subdivision. periodScale and separationDistance move TOGETHER, so ResolveLevel's argmin - the
# level - is invariant and the plant keeps its elemental peers' exact mesh: level 2, 36 sites per
# tile, same bonds, same prism count. Only the distances grow.
#
# The earlier pass scaled separationDistance alone, which re-resolved to level 0 (6 sites) - a
# structure with visibly fewer subdivisions. That is a DIFFERENT plant, not a bigger one, and it
# is the thing this constant exists to prevent; assert_level_invariant() proves it every run.
#
# The prism itself is then derived from the GYROID Space's shipped proportions (imported, not
# copied), so the element reads the same on both surfaces: the same spans-per-spacing and the
# same thickness-per-spacing.
SPACE_LATTICE_SCALE = 5.0


# ----------------------------------------------------------------------------
# The prism frames
# ----------------------------------------------------------------------------


def resolve_level(levels):
    """SchwarzPTileData.ResolveLevel: the subdivision whose measured spacing lands
    closest to the authored separationDistance."""
    best, best_err = 0, float("inf")
    for i, (mean_spacing, _) in enumerate(levels):
        err = abs(mean_spacing * WORLD_SCALE - SEPARATION_DISTANCE)
        if err < best_err:
            best, best_err = i, err
    return best


def resolve_level_for(levels, separation, world_scale=None):
    """ResolveLevel for an element that authors its own separationDistance."""
    if world_scale is None:
        world_scale = WORLD_SCALE
    best, best_err = 0, float("inf")
    for i, (mean_spacing, _) in enumerate(levels):
        err = abs(mean_spacing * world_scale - separation)
        if err < best_err:
            best, best_err = i, err
    return best


def largest_clear_strut(centres, bases, is_central, thickness, hi=40.0, iterations=24):
    """Longest ZERO-overlap strut at a fixed square cross-section - the Space shape.
    Distinct from largest_clear, which scales both footprint axes together."""
    lo = 0.5
    if measure_fit(centres, bases, is_central, (lo, thickness, thickness))[1] > 0:
        return None
    for _ in range(iterations):
        mid = 0.5 * (lo + hi)
        if measure_fit(centres, bases, is_central, (mid, thickness, thickness))[1] == 0:
            lo = mid
        else:
            hi = mid
    return (math.floor(lo * 100) / 100, thickness, thickness)


def prism_frames(levels, level, radius=1, world_scale=None):
    """Every prism of a (2r+1)^3 tile block, in world units: centre plus the
    orthonormal basis Unity's LookRotation(normal, tangent) produces.

    world_scale defaults to the prefab's; an element that authors a LatticeScale passes
    its own, which scales every POSITION and leaves every rotation alone."""
    if world_scale is None:
        world_scale = WORLD_SCALE
    offsets = [s[0] for s in levels[level][1]]
    tangents = [s[1] for s in levels[level][1]]

    centres, bases, is_central = [], [], []
    for ijk in itertools.product(range(-radius, radius + 1), repeat=3):
        for off, tan in zip(offsets, tangents):
            p = np.array(site_param(ijk, off))
            n = np.array(surface_normal(p))
            t = np.array(site_tangent(ijk, tan))
            # Unity: right = normalize(cross(up, forward)), up' = cross(forward, right)
            ex = np.cross(t, n)
            ex /= np.linalg.norm(ex)
            ey = np.cross(n, ex)
            centres.append(p * world_scale)
            bases.append(np.stack([ex, ey, n]))
            is_central.append(ijk == (0, 0, 0))
    return np.array(centres), np.array(bases), np.array(is_central)


# ----------------------------------------------------------------------------
# Oriented-box overlap, by the separating-axis theorem
# ----------------------------------------------------------------------------


def obb_penetration(ca, Ra, ha, cb, Rb, hb):
    """Penetration depth of two OBBs (0 if separated).  Rows of R are the box axes,
    h the half-extents.  Tests the 3 + 3 face normals and the 9 edge cross products;
    the minimum positive overlap across all of them is the penetration."""
    d = cb - ca
    worst = float("inf")
    axes = [Ra[0], Ra[1], Ra[2], Rb[0], Rb[1], Rb[2]]
    for i in range(3):
        for j in range(3):
            axis = np.cross(Ra[i], Rb[j])
            if np.dot(axis, axis) > 1e-12:
                axes.append(axis / np.linalg.norm(axis))
    for axis in axes:
        ra = float(np.sum(ha * np.abs(Ra @ axis)))
        rb = float(np.sum(hb * np.abs(Rb @ axis)))
        gap = ra + rb - abs(float(np.dot(d, axis)))
        if gap <= 0.0:
            return 0.0                      # a separating axis exists
        worst = min(worst, gap)
    return worst


def measure_fit(centres, bases, is_central, leaf, cutoff_scale=1.15):
    """Worst penetration and overlapping-pair count for a leaf size, measured from
    every CENTRAL-tile prism against every prism in the block (so seams count)."""
    half = np.array(leaf) * 0.5
    reach = float(np.linalg.norm(half)) * 2.0 * cutoff_scale
    idx_central = np.where(is_central)[0]

    worst, pairs = 0.0, 0
    for i in idx_central:
        d = np.linalg.norm(centres - centres[i], axis=1)
        near = np.where((d > 1e-6) & (d < reach))[0]
        for j in near:
            pen = obb_penetration(centres[i], bases[i], half, centres[j], bases[j], half)
            if pen > 1e-6:
                pairs += 1
                worst = max(worst, pen)
    return worst, pairs // 1        # each central pair counted once per direction


def largest_clear(centres, bases, is_central, aspect, thickness,
                  lo=0.5, hi=14.0, iterations=26):
    """Binary-search the largest footprint of the given aspect with ZERO overlaps.
    `aspect` is the long:short ratio, applied as x = s*sqrt(a), y = s/sqrt(a)."""
    root = math.sqrt(aspect)

    def leaf_for(s):
        return (s * root, s / root, thickness)

    if measure_fit(centres, bases, is_central, leaf_for(lo))[1] > 0:
        return None
    for _ in range(iterations):
        mid = 0.5 * (lo + hi)
        if measure_fit(centres, bases, is_central, leaf_for(mid))[1] == 0:
            lo = mid
        else:
            hi = mid
    # Round DOWN to two decimals: an authored number a human can read, and rounding
    # down is what keeps the zero-overlap result true of the value actually shipped.
    leaf = leaf_for(lo)
    return (math.floor(leaf[0] * 100) / 100, math.floor(leaf[1] * 100) / 100, thickness)


def coverage(levels, level, leaf):
    """Footprint area of one tile's prisms over the tile patch's own area."""
    from verify_schwarz_p_tile_tables import parse_cs as _p
    area_param, _ = _p()
    patch_world = area_param * WORLD_SCALE ** 2
    return len(levels[level][1]) * leaf[0] * leaf[1] / patch_world


# ----------------------------------------------------------------------------
# Rendering
# ----------------------------------------------------------------------------


def render(sheets, path):
    from PIL import Image, ImageDraw
    S, PAD = 460, 14
    img = Image.new("RGB", (S * len(sheets) + PAD * (len(sheets) + 1), S + PAD * 2 + 22),
                    (13, 15, 19))
    dr = ImageDraw.Draw(img)

    for col, (label, centres, bases, leaf, tint) in enumerate(sheets):
        ox = PAD + col * (S + PAD)
        dr.rectangle([ox, PAD, ox + S, PAD + S], outline=(56, 60, 70))

        # Viewed straight down the flat point's normal, which is the only projection in
        # which "do these plates tile?" is a question you can actually answer by eye.
        n = np.array([1.0, 1.0, 1.0]) / math.sqrt(3.0)
        e1 = np.array([1.0, -1.0, 0.0]) / math.sqrt(2.0)
        e2 = np.cross(n, e1)

        c = centres - centres.mean(axis=0)
        u, v, w = c @ e1, c @ e2, c @ n
        scale = (S * 0.44) / max(np.abs(u).max(), np.abs(v).max())
        hx, hy = leaf[0] * 0.5, leaf[1] * 0.5

        for j in np.argsort(w):
            ex, ey = bases[j][0], bases[j][1]
            quad = []
            for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                p = c[j] + ex * (sx * hx) + ey * (sy * hy)
                quad.append((ox + S / 2 + float(p @ e1) * scale,
                             PAD + S / 2 - float(p @ e2) * scale))
            t = (w[j] - w.min()) / max(1e-6, float(np.ptp(w)))
            fill = tuple(int(x * (0.35 + 0.65 * t)) for x in tint)
            dr.polygon(quad, fill=fill, outline=(10, 11, 14))
        dr.text((ox + 8, PAD + S + 6), label, fill=(205, 210, 220))

    img.save(path)
    return path


# ----------------------------------------------------------------------------
# Authoring
# ----------------------------------------------------------------------------


VARIANT_CS = ROOT / "Assets/_Scripts/Utility/DataContainers/FloraConfigurationSO.cs"


def variant_fields():
    """Every serialized field of FloraVariantTuning, with its keep-the-prefab sentinel,
    read from the C# so this cannot drift when a field is added."""
    src = VARIANT_CS.read_text()
    body = re.search(r"public class FloraVariantTuning\s*\{(.*?)\n    \}", src, re.S).group(1)
    body = re.sub(r"^(?:\s*\[[^\]]*\]\s*)+", "", body, flags=re.M)
    out = []
    for line in body.splitlines():
        m = re.match(r"\s*public ([\w<>]+) (\w+)\s*=\s*([^;]+);", line)
        if m:
            out.append((m.group(2), m.group(1)))
    return out


def set_leaf_size_only(path, leaf):
    """Replace just the LeafSize row of an already-populated Variant block, so a cell's
    own decisions in that block (budget, planting radius, tempo) survive untouched."""
    text = path.read_text()
    row = f"    LeafSize: {{x: {leaf[0]:g}, y: {leaf[1]:g}, z: {leaf[2]:g}}}"
    new, n = re.subn(r"^    LeafSize: \{[^}]*\}$", row, text, count=1, flags=re.M)
    if n != 1:
        raise SystemExit(f"could not find a LeafSize row in {path.name}")
    path.write_text(new)
    return path


def write_variant(element, leaf, lattice_scale=None):
    """Set ONLY the leaf size on the element asset's Variant block, leaving every other
    field at its sentinel.

    The sentinels are load-bearing and are NOT zero: FloraVariantTuning keeps the
    prefab's value at -1 for numbers and Vector3.zero for vectors, and the consumers
    test `>= 0`. Writing `MaxTotalSpawnedObjects: 0` would therefore not mean "keep" -
    it would set the plant's live-prism budget to zero and the flora would never grow
    a single prism. Every field is emitted explicitly rather than omitted, because a
    nested serialized class can zero-initialize (see the MaxTotalSpawnedObjectsScale
    tooltip, which defends against exactly that), and a zero that arrives by omission
    is indistinguishable from one that was authored."""
    path = LIFEFORMS / f"SchwarzP Flora {element}.asset"
    text = path.read_text()

    # PRESERVE every field this script does not own. author_flora_populations.py writes the
    # per-plant budget (MaxTotalSpawnedObjects) into this same block, so emitting a blanket
    # sentinel here would silently revert it depending on which script ran last - a run-order
    # hazard rather than a bug, which is worse, because it only shows up when someone
    # re-runs the fitter months later.
    existing = dict(re.findall(r"^    (\w+): (.+)$",
                               re.search(r"  Variant:\n((?:    [^\n]*\n)*)", text).group(1),
                               re.M))

    lines = ["  Variant:", "    Enabled: 1"]
    for name, kind in variant_fields():
        if name == "Enabled":
            continue
        if name == "LeafSize":
            lines.append(f"    LeafSize: {{x: {leaf[0]:g}, y: {leaf[1]:g}, z: {leaf[2]:g}}}")
        elif name == "LatticeScale" and lattice_scale is not None:
            lines.append(f"    LatticeScale: {lattice_scale:g}")
        elif name in existing:
            lines.append(f"    {name}: {existing[name]}")
        elif kind == "Vector3":
            lines.append(f"    {name}: {{x: 0, y: 0, z: 0}}")
        else:
            lines.append(f"    {name}: -1")
    block = "\n".join(lines) + "\n"

    new, n = re.subn(r"  Variant:\n(?:    [^\n]*\n)*", block, text, count=1)
    if n != 1:
        raise SystemExit(f"could not locate the Variant block in {path.name}")
    path.write_text(new)

    # Unity serializes by NAME: a key the class does not declare is silently dropped,
    # and a field we failed to emit takes whatever the class initializes it to. Assert
    # the written block is exactly the class's field set.
    written = set(re.findall(r"^    (\w+):", block, re.M))
    declared = {n for n, _ in variant_fields()}
    if written != declared:
        raise SystemExit(f"{path.name}: variant key mismatch "
                         f"missing={declared - written} unknown={written - declared}")
    return path


# ----------------------------------------------------------------------------


def assert_level_invariant(levels):
    """Prove that scaling the lattice leaves the SUBDIVISION - and so the prism count and
    the mesh topology - exactly as the peers'.

    This is the whole contract of LatticeScale, and it is one line of arithmetic that is
    easy to get wrong in the other direction: ResolveLevel compares
    `MeanParamSpacing * periodScale / 2pi` against `separationDistance`, so scaling ONLY
    separationDistance re-resolves to a coarser level (Space landed on level 0, 6 sites,
    instead of its peers' level 2, 36) and quietly ships a different plant."""
    peer = resolve_level(levels)
    scaled = resolve_level_for(levels, SEPARATION_DISTANCE * SPACE_LATTICE_SCALE,
                               world_scale=WORLD_SCALE * SPACE_LATTICE_SCALE)
    if scaled != peer:
        raise SystemExit(
            f"LatticeScale {SPACE_LATTICE_SCALE:g} moves Space off its peers' subdivision "
            f"(level {peer} -> {scaled}). periodScale and separationDistance must scale "
            f"together or the plant changes topology instead of size.")
    return peer


# The Space prism is AUTHORED OUTRIGHT, not derived from the lattice.
#
# It used to be expressed as RATIOS to the prism spacing, so the strut tracked the lattice
# automatically. That coupling is now actively wrong: prism size and lattice spacing are tuned
# INDEPENDENTLY - the strut is picked for how the skeleton reads, the spacing for how open the
# structure is - so a ratio silently re-sizes the prism every time the spacing moves. On the
# spacing change that retired it, the ratio would have tripled this strut to 90 x 1.5 x 1.5
# with nobody asking for it.
#
# Zero-overlap is likewise not the objective for a strut: one short enough to clear every
# neighbour reaches none of them and reads as disconnected bars. Crossings are accepted, and
# only REPORTED below. Docs/ECOSYSTEM.md 34.7.
SPACE_LEAF = (30.0, 0.5, 0.5)


def space_leaf():
    """The Space prism: authored outright. See SPACE_LEAF."""
    return SPACE_LEAF


def shipped_leaf(element):
    """The LeafSize currently authored on one element asset."""
    text = (LIFEFORMS / f"SchwarzP Flora {element}.asset").read_text()
    m = re.search(r"^    LeafSize: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}$", text, re.M)
    return tuple(float(v) for v in m.groups())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--render", action="store_true", help="write a preview sheet")
    ap.add_argument("--write", action="store_true", help="author the four element assets")
    args = ap.parse_args()

    _, levels = parse_cs()
    level = resolve_level(levels)
    centres, bases, is_central = prism_frames(levels, level)
    n_sites = len(levels[level][1])

    # site spacing, for reporting the strut lengths in spacings
    central = centres[is_central]
    d = np.linalg.norm(centres[:, None, :] - central[None, :, :], axis=2)
    d[d < 1e-6] = np.inf
    spacing_min = float(d.min())
    spacing_mean = float(np.mean(d.min(axis=0)))

    print("=" * 78)
    print("Schwarz P prism fit")
    print("=" * 78)
    print(f"  periodScale {PERIOD_SCALE:g}, separationDistance {SEPARATION_DISTANCE:g}"
          f"  ->  level {level}, {n_sites} sites/tile")
    print(f"  world site spacing: min {spacing_min:.3f}, mean {spacing_mean:.3f}")

    print("\nASPECT SWEEP - largest footprint with ZERO overlaps, and what it covers")
    print("  aspect    x       y     coverage")
    best_aspect, best_cov = 1.0, 0.0
    for aspect in (1.0, 1.3, 1.6, 2.0, 2.4, 2.8, 3.4, 4.0, 5.0):
        leaf = largest_clear(centres, bases, is_central, aspect, thickness=1.0)
        if leaf is None:
            print(f"  {aspect:>5.1f}    (no clear fit)")
            continue
        cov = coverage(levels, level, leaf)
        mark = ""
        if cov > best_cov:
            best_aspect, best_cov, mark = aspect, cov, "   <-- most coverage"
        print(f"  {aspect:>5.1f}  {leaf[0]:5.2f}  {leaf[1]:5.2f}     {cov:5.1%}{mark}")

    # ---- the four elements
    #
    # Charge / Time: the reference pair. The GOLDEN ratio is the "pleasant" aspect, and
    # on this arrangement it is also near the coverage optimum - the sweep peaks at a
    # near-square 1.3 and 1.618 gives up only ~1.5 points for a much better-looking
    # rectangle. Sized at the zero-overlap maximum, so the plates are as large as they
    # can be while still literally never interpenetrating.
    #
    # Mass: chunkier, exactly as the gyroid's Mass is chunkier than its Charge/Time -
    # a squarer footprint AND a thicker slab. Still measured at zero overlap.
    #
    # Space: the largest aspect, and the ONE element that is not a flush plate. It is a
    # STRUT that deliberately spans several site spacings so the structure reads
    # skeletal and takes the largest bounds - the same thing the gyroid's Space does
    # (20 x 1 x 1, measured at 2.56 of its own prism spacings, interpenetrating 99% of
    # its neighbours). Sized by SPAN, not by clearance.
    #
    # z stays thin on all four: these are plates lying ON a minimal surface. The gyroid's
    # Charge/Time runs a thickness of 0.19 spacings and its Mass 0.45; these match.
    golden = (1.0 + math.sqrt(5.0)) / 2.0
    pleasant = largest_clear(centres, bases, is_central, golden, thickness=1.0)
    chunky = largest_clear(centres, bases, is_central, 1.3, thickness=2.0)
    # Space keeps its PEERS' level and scales the whole lattice instead.
    space_level = assert_level_invariant(levels)
    space_world_scale = WORLD_SCALE * SPACE_LATTICE_SCALE
    sc, sb, sic = prism_frames(levels, space_level, world_scale=space_world_scale)
    sd = np.linalg.norm(sc[:, None, :] - sc[sic][None, :, :], axis=2)
    sd[sd < 1e-6] = np.inf
    space_spacing = float(np.mean(sd.min(axis=0)))
    space = space_leaf()

    # CHARGE IS NO LONGER THIS FITTER'S TO SIZE, and re-imposing the flush fit here would
    # silently revert it depending on which script ran last - the exact run-order hazard
    # write_variant's own comment warns about. Charge SHIELDS (Flora.ResolveShieldPeriod),
    # and a shield is the CIRCUMSCRIBING octahedron - 3x the prism's reach - so what decides
    # its size is whether those octahedra interpenetrate, not whether the plates do. That is
    # fitted by Tools/Build/fit_shield_clearance.py (Docs/ECOSYSTEM.md 35); this reads it
    # back so the report and the writes both describe what actually ships.
    charge = shipped_leaf("Charge")
    print(f"\n  NOTE Charge is sized by fit_shield_clearance.py, not by the flush fit: its")
    print(f"       shields are 3x its plates and they, not the plates, are what crowd. The")
    print(f"       flush fit would be {pleasant[0]:g} x {pleasant[1]:g} x {pleasant[2]:g}; "
          f"it ships {charge[0]:g} x {charge[1]:g} x {charge[2]:g}.")

    elements = [
        ("Charge", charge, (86, 196, 232)),
        ("Time", pleasant, (150, 214, 130)),
        ("Mass", chunky, (232, 168, 96)),
        ("Space", space, (196, 140, 232)),
    ]

    for name, leaf, _ in elements:
        own_level = space_level if name == "Space" else level
        oc, ob, oic = (sc, sb, sic) if name == "Space" else (centres, bases, is_central)
        own_spacing = space_spacing if name == "Space" else spacing_mean
        worst, pairs = measure_fit(oc, ob, oic, leaf)
        cov = coverage(levels, own_level, leaf)
        span = max(leaf[0], leaf[1]) / own_spacing
        state = "flush, no overlaps" if pairs == 0 else \
                f"{pairs} overlapping pairs, max penetration {worst:.2f}u"
        if name == "Space":
            state += f"  [lattice x{SPACE_LATTICE_SCALE:.4f}: level {space_level} " \
                     f"(SAME as peers), {len(levels[space_level][1])} sites at " \
                     f"{own_spacing:.2f}]"
        print(f"  {name:<7} {leaf[0]:5.2f} x {leaf[1]:4.2f} x {leaf[2]:4.2f}   "
              f"aspect {max(leaf[0], leaf[1]) / min(leaf[0], leaf[1]):4.1f}:1   "
              f"span {span:4.2f} spacings   coverage {cov:5.1%}   {state}")

    # The LEVEL trap this used to demonstrate here - LeafScalePerLevel scaling the leaf but
    # not the lattice, so a levelled plant interpenetrates itself - cannot arise at all any
    # more: lifeform LEVELS are retired outright, so a leaf is its authored size for the whole
    # of a plant's life and the fit below is the ONLY size that ever renders
    # (Docs/ECOSYSTEM.md 40.2). Flora.PrismSizeFixedByGrowthRule is kept as a standing guard
    # against the rule coming back on a growth path; it is not this fitter's business either
    # way. What varies per plant now is its ELEMENT - which is what the four fitted leaves
    # below are - and the size of the heart that element drops
    # (FloraVariantTuning.HeartWorldScale, authored by Tools/Build/author_lifeform_heart_sizes.py
    # and deliberately NOT touched from here: two fitters must never own one asset field).

    if args.render:
        sheets = [(f"{n}  {l[0]:g}x{l[1]:g}x{l[2]:g}",
                   centres[is_central], bases[is_central], l, tint)
                  for n, l, tint in elements]
        out = render(sheets, ROOT / "Tools/Build/schwarz_p_leaf_fit.png")
        print(f"\n  rendered {out}")

    if args.write:
        print()
        for name, leaf, _ in elements:
            p = write_variant(name, leaf,
                             SPACE_LATTICE_SCALE if name == "Space" else None)
            print(f"  wrote {p.relative_to(ROOT)}")

        # The two CELL-level configs are producers too, and a fit applied to four of six
        # is a fit that shows up wrong in two cells. The Hesperides topiary is Element 2
        # (Mass) and carries its own budget/planting decisions, so only its LeafSize row
        # moves. The Blob cell config is NOT written: it has no Variant of its own (it
        # delegates to the element palette above), and the level fields this fitter used to
        # pin there no longer exist - lifeform levels are retired (Docs/ECOSYSTEM.md 40.2).
        hesperides = (ROOT / "Assets/_SO_Assets/Cell Configs/Hesperides Cell"
                             "/Hesperides SchwarzP Topiary Config Data.asset")
        set_leaf_size_only(hesperides, chunky)
        print(f"  wrote {hesperides.relative_to(ROOT)}")


    return 0


if __name__ == "__main__":
    sys.exit(main())
