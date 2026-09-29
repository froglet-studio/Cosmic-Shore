#!/usr/bin/env python3
"""
Fit a SHIELDED lattice species' leaf size so its octahedra have room on its own lattice.

    python3 Tools/Build/fit_shield_clearance.py            # measure + report every species
    python3 Tools/Build/fit_shield_clearance.py --write    # + author the Charge assets
    python3 Tools/Build/fit_shield_clearance.py --check    # CI: assert the shipped fits

WHY THIS IS MEASURED AND NOT AUTHORED BY EYE
--------------------------------------------
Charge is the element that SHIELDS its leaves (Flora.ResolveShieldPeriod - the law, and
FloraVariantTuning.ShieldPeriod - the cadence).  A shielded prism does not merely change
colour: PrismStateManager engages a PrismOctahedronShield, which replaces the box with the
octahedron that CIRCUMSCRIBES it - OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE = 3 applied
to the box HALF-extents, i.e. a body reaching 1.5 x leafSize from the prism centre along
each local axis and enclosing 4.5x the box volume.

So a shield TRIPLES a prism's reach, and on a LATTICE species that is the whole problem.
Both shipped lattice species were fitted for the box they draw unshielded, and both were
fitted TIGHT - the gyroid's leaf nearly spans its bond (9u against 7.84u) and Schwarz P's
plates are literally flush.  Tripling that reach therefore does not draw a lattice of
octahedra, it draws one interpenetrating solid.  Measured at the sizes each shipped with:

    gyroid Charge     9.00 x 3.40 x 1.50    shields overlap at 1.89x    826 of 15,880 pairs
    Schwarz P Charge  4.72 x 2.92 x 1.00    shields overlap at 2.25x  3,654 of 74,952 pairs

"The octahedra have clearance" is a geometric claim about a specific point set - each
species' own measured site arrangement - so it is fitted here rather than guessed.  Each
prism is an oriented box, its shield is the octahedron conv{+-1.5*Lx*x, +-1.5*Ly*y,
+-1.5*Lz*z}, and whether two neighbouring shields interpenetrate is an exact
separating-axis question.  Same idiom as Tools/Build/fit_schwarz_p_leaf_sizes.py, which
fits that species' PLATES flush against its measured site set.

WHAT IS FITTED, AND WHAT IS NOT
-------------------------------
Only the leaf SIZE moves, uniformly on all three axes, because a species' leaf ASPECT is
its identity - the gyroid Charge's 9 : 3.4 : 1.5 (shared with Time), Schwarz P Charge's
4.72 : 2.92 : 1 (a thin plate lying ON a minimal surface, thickness 0.34 of its short
axis).  A uniform shrink keeps both exactly.

    Measured, and worth knowing: on Schwarz P the binding axes are the two in the TANGENT
    plane - shrinking the footprint alone to 2.10 x 1.30 clears every shield with z left
    at 1.0, i.e. the thickness buys nothing either way.  It is shrunk anyway, because at
    1.88 x 1.16 x 1.00 the "plate" is very nearly a cube and stops reading as a plate on a
    surface.  Uniform is a look decision the geometry permits, not one it forces.

The LATTICE is untouched on both - separationDistance / periodScale / LatticeScale, the
bond and tile tables, and every coherence tolerance (snap, mate-search radius, reservation
floor, AssembledFlora.MisalignmentRadius) stay exactly as shipped.  That is deliberate and
is the cheap half of Docs/ECOSYSTEM.md 34.8: those tolerances are absolute distances
measured against the lattice, so scaling the lattice drags a whole family of constants with
it, while scaling the PRISM drags nothing - the assemblers read Prism.TargetScale only to
stamp it onto the next prism, never to place one.

The trade is stated plainly by the numbers below: shrinking the leaf to clear the shields
makes the UNSHIELDED plant a sparser skeleton, and the shields are what fill the lattice
back in.  That is why the fit targets shields that TOUCH rather than shields that merely
stop being a blob.

THE SILENT-CLAMP TRAP
---------------------
PrismScaleAnimator.SetTargetScale clamps per axis into [minScale, maxScale], and
HealthBlock.prefab ships (0.5, 0.5, 0.5) - so Schwarz P Charge's fitted 0.39 THICKNESS is
below the floor and would be silently clamped UP to 0.5 (Docs/ECOSYSTEM.md 34.9).  It
survives only because Flora.AddHealthBlock calls Prism.AdmitTargetScale first, which lowers
minScale to admit the stated size.  That is checked here rather than assumed: any fitted
axis outside the prefab's authored window is reported, and the check fails if the species'
creation path does not admit.

VOLUME
------
Prism volume is Lx*Ly*Lz, so a uniform k shrink is a k^3 volume change landing straight on
the host cell's Frenzy ladder (Docs/ECOSYSTEM.md 4.6 / 34.8).  Both fits shrink, so Frenzy
arrives LATER - the safe direction, and no ladder is re-authored.  The Blob cell arithmetic
they land in is recorded in Docs/ECOSYSTEM.md 35.

IDEMPOTENCE
-----------
The fit is the FIXED POINT s* = 1 / (1 - CLEARANCE), not a fresh multiply of whatever leaf
is currently authored - so --write is safe to re-run and --check does not fail on the asset
this tool itself wrote.  FIT_TOLERANCE is the band that absorbs the 2-decimal rounding
residue.
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
from verify_gyroid_lattice_scale import (  # noqa: E402
    MEASURED_SEPARATION, look_rotation, parse_bond_table,
)

ROOT = pathlib.Path(__file__).resolve().parents[2]
LIFEFORMS = ROOT / "Assets/_SO_Assets/Lifeforms"
OCTAHEDRON_CS = ROOT / "Assets/_Scripts/Utility/OctahedronMeshGenerator.cs"
HEALTH_BLOCK = ROOT / "Assets/_Prefabs/Trails/HealthBlock.prefab"
VARIANT_CS = ROOT / "Assets/_Scripts/Utility/DataContainers/FloraConfigurationSO.cs"
FLORA_CS = ROOT / "Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs"

SITES = ("TopRight", "TopLeft", "BottomLeft", "BottomRight")
ELEMENTS = ("Charge", "Mass", "Space", "Time")

# How much room to leave beyond "just touching", as a fraction of the touching size. The
# shields are a VISUAL of the plant's armour: at exactly 1.0 they kiss, which reads as one
# fused surface again the moment growth drift moves a prism a fraction of a unit. A tenth
# is enough that the individual octahedra read while the lattice still fills in.
CLEARANCE = 0.10

# How far a shipped leaf may sit from the fit before it is re-authored. The fit is a fixed
# point (s* = 1 / (1 - CLEARANCE)), and without a band a 2-decimal rounding residue would
# make every run report drift and --check fail on an asset this very tool wrote.
FIT_TOLERANCE = 0.02

# Enough of each lattice that the interior sites have their full neighbourhood - the worst
# pair is always interior, so a bigger patch cannot lower a fitted size, only cost time.
WALK_NODES = 220          # gyroid: bonded nodes grown from one seed
SCHWARZ_RADIUS = 1        # Schwarz P: a (2r+1)^3 tile block, 36 sites per tile


# ----------------------------------------------------------------------------
# Shipped constants - read, never re-typed
# ----------------------------------------------------------------------------


def circumscribing_scale():
    """OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE - the factor on the box HALF-extents."""
    src = OCTAHEDRON_CS.read_text(encoding="utf-8-sig")
    return float(re.search(r"CIRCUMSCRIBING_SCALE\s*=\s*([\d.]+)f", src).group(1))


def health_block_box():
    """(BoxCollider size, centre, minScale, maxScale) from HealthBlock.prefab, LOCAL units.

    The shield's half-extents come from the COLLIDER (PrismOctahedronShield.CacheGeometry
    reads BoxCollider.size, not transform.localScale, because growth animates the scale),
    so a leaf size only means "the prism's world size" while this stays a unit cube centred
    on the origin. The scale window is the silent clamp a fitted size has to survive."""
    src = HEALTH_BLOCK.read_text(encoding="utf-8-sig")
    body = src[src.index("BoxCollider:"):]

    def vec3(pattern, text):
        m = re.search(pattern + r": \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}", text)
        return np.array([float(v) for v in m.groups()])

    return (vec3("m_Size", body), vec3("m_Center", body),
            vec3("minScale", src), vec3("maxScale", src))


def variant(species_asset, element):
    """(leafSize, latticeScale, shieldPeriod) as shipped for one species/element asset."""
    text = (LIFEFORMS / f"{species_asset} Flora {element}.asset").read_text()
    leaf = re.search(r"^    LeafSize: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}$",
                     text, re.M)
    lattice = re.search(r"^    LatticeScale: ([-\d.]+)$", text, re.M)
    shield = re.search(r"^    ShieldPeriod: ([-\d.]+)$", text, re.M)
    scale = float(lattice.group(1)) if lattice else -1.0
    return (np.array([float(v) for v in leaf.groups()]),
            scale if scale > 0 else 1.0,          # -1 is the keep-the-prefab sentinel
            float(shield.group(1)) if shield else -1.0)


# ----------------------------------------------------------------------------
# The prism frames, per species
#
# Both return (centres, R) with R's COLUMNS the prism's local x/y/z axes in world space -
# the convention semi_axes() consumes. The gyroid's own walk produces that directly; the
# Schwarz P fitter stores its bases as ROWS, so they are transposed at this boundary rather
# than at every use.
# ----------------------------------------------------------------------------


def gyroid_frames(lattice_scale):
    """Grow the lattice exactly as GyroidAssembler does, keeping each node's ROTATION.

    A prism's transform IS the node: GetGrowthInfo returns CalculateGlobalBondSite /
    CalculateRotation and the spawned prism is placed there, so these frames are the prism
    poses the octahedra are drawn in."""
    table = parse_bond_table()
    separation = MEASURED_SEPARATION * lattice_scale
    tol = 2.5 * separation / MEASURED_SEPARATION
    nodes = [(np.zeros(3), np.eye(3), "AB")]
    grid = {}

    def key(p):
        return tuple(np.floor(p / tol).astype(int))

    def seen(p):
        k = key(p)
        for d in np.ndindex(3, 3, 3):
            for q in grid.get(tuple(k[i] + d[i] - 1 for i in range(3)), ()):
                if np.linalg.norm(q - p) < tol:
                    return True
        return False

    grid.setdefault(key(nodes[0][0]), []).append(nodes[0][0])
    frontier, head = [0], 0
    while head < len(frontier) and len(nodes) < WALK_NODES:
        i = frontier[head]
        head += 1
        pos, R, bt = nodes[i]
        for s in SITES:
            e = table.get((bt, s))
            if e is None:
                continue
            child = pos + R.dot(np.array(e["DeltaPosition"]) * separation)
            Rc = look_rotation(R.dot(np.array(e["DeltaForward"]) + [0, 0, 1.0]),
                               R.dot(np.array(e["DeltaUp"]) + [0, 1.0, 0]))
            if Rc is None or seen(child) or len(nodes) >= WALK_NODES:
                continue
            grid.setdefault(key(child), []).append(child)
            nodes.append((child, Rc, e["BlockType"]))
            frontier.append(len(nodes) - 1)

    return np.array([n[0] for n in nodes]), np.array([n[1] for n in nodes])


def schwarz_frames(lattice_scale):
    """A 3x3x3 tile block of Schwarz P, from the SHIPPED tile table.

    Borrows fit_schwarz_p_leaf_sizes' own frame builder rather than re-deriving the tile,
    so this cannot measure a different surface than the one that species is fitted against.
    A LatticeScale multiplies every POSITION and leaves every rotation alone, exactly as
    SchwarzPAssembler does - and the subdivision LEVEL is invariant under it by
    construction (Docs/ECOSYSTEM.md 34.7), which is what lets one level serve all four."""
    import fit_schwarz_p_leaf_sizes as sp

    _, levels = sp.parse_cs()
    level = sp.resolve_level(levels)
    centres, bases, _ = sp.prism_frames(levels, level, radius=SCHWARZ_RADIUS,
                                        world_scale=sp.WORLD_SCALE * lattice_scale)
    return np.asarray(centres, float), np.asarray(bases, float).transpose(0, 2, 1)


# (report name, the "<prefix> Flora <Element>.asset" prefix, the frame builder). Every
# species here stamps its leaf through Flora.AddHealthBlock, which is what admits_target_scale
# checks - add one whose prisms are sized elsewhere and that check needs to learn about it.
SPECIES = (
    ("gyroid", "Gyroid", gyroid_frames),
    ("SchwarzP", "SchwarzP", schwarz_frames),
)


# ----------------------------------------------------------------------------
# Separating-axis test, in the ONE form that answers the question asked
# ----------------------------------------------------------------------------


def _faces(S):
    """The 8 face normals of an octahedron conv{+-S0, +-S1, +-S2}, one per sign octant.

    S is (..., 3, 3) with rows = the semi-axis vectors, so this batches over pairs."""
    out = []
    for e in itertools.product((1, -1), repeat=3):
        out.append(np.cross(e[1] * S[..., 1, :] - e[0] * S[..., 0, :],
                            e[2] * S[..., 2, :] - e[0] * S[..., 0, :]))
    return np.stack(out, axis=-2)


def _edges(S):
    """The 6 distinct edge DIRECTIONS: S_j -+ S_i over the three axis pairs. The 12 edges
    reduce to 6 directions because opposite edges of an octahedron are parallel."""
    out = []
    for i, j in ((0, 1), (0, 2), (1, 2)):
        out.append(S[..., j, :] - S[..., i, :])
        out.append(S[..., j, :] + S[..., i, :])
    return np.stack(out, axis=-2)


def touching_scales(cA, SA, cB, SB):
    """Touching scale of octahedron A against a BATCH of octahedra B.

    Face normals of both bodies plus every edge-edge cross product is the complete SAT
    candidate set for convex polytopes, and both bodies are centrally symmetric about their
    prism's centre - so scaling a pair by s scales every projection radius by s while the
    centre offset d is fixed, and

        s* = max over candidate axes of  |d.u| / (rA(u) + rB(u))

    is exact rather than a bisection: below s* that axis separates them, above it none
    does. s* >= 1 means the pair is clear as shipped; s* < 1 is the factor the pair would
    have to shrink by to stop interpenetrating.

    Batched over B because the fit runs this over every near pair of a 972-prism block, and
    a per-pair Python loop makes the tool too slow to put in front of CI."""
    m = len(SB)
    fa = np.broadcast_to(_faces(SA), (m, 8, 3))
    fb = _faces(SB)
    ea, eb = _edges(SA), _edges(SB)                       # (6,3) and (m,6,3)
    ec = np.cross(ea[None, :, None, :], eb[:, None, :, :]).reshape(m, 36, 3)
    u = np.concatenate([fa, fb, ec], axis=1)              # (m, 52, 3)

    n = np.linalg.norm(u, axis=2)
    ok = n > 1e-9
    u = np.divide(u, np.where(ok, n, 1.0)[..., None])

    d = np.abs(np.einsum("mkj,mj->mk", u, cB - cA))
    rA = np.abs(np.einsum("mkj,ij->mki", u, SA)).max(axis=2)
    rB = np.abs(np.einsum("mkj,mij->mki", u, SB)).max(axis=2)
    ratio = np.where(ok, d / np.maximum(rA + rB, 1e-12), -np.inf)
    return ratio.max(axis=1)


def semi_axes(R, half_extents, shield_scale):
    """The three semi-axis VECTORS of one prism's shield, in world units. R's COLUMNS are
    the prism's local axes; the returned rows are those axes scaled to the shield."""
    return (R * (half_extents * shield_scale)).T


# How far past "touching" the report still measures. A pair further apart than S_MAX x its
# own size is not the pair that decides anything; the cutoff only exists so the O(n^2) SAT
# does not run over the whole patch. ">S_MAX" is printed rather than a silent infinity -
# "no pair can touch" is a real answer and has to look different from "the cutoff hid them".
S_MAX = 2.5


def worst_pair(P, Rs, half_extents, shield_scale):
    """(min touching scale, interpenetrating pairs, near pairs, pairs AT the worst)."""
    S = np.array([semi_axes(Rs[i], half_extents, shield_scale) for i in range(len(P))])
    reach = np.linalg.norm(S, axis=2).max()   # a pair beyond r_A + r_B cannot touch at 1x
    cutoff = 2.0 * reach * S_MAX
    every = []
    for i in range(len(P)):
        d = np.linalg.norm(P[i + 1:] - P[i], axis=1)
        near = np.nonzero(d < cutoff)[0] + i + 1
        if len(near) == 0:
            continue
        every.append(touching_scales(P[i], S[i], P[near], S[near]))
    if not every:
        return math.inf, 0, 0, 0
    s = np.concatenate(every)
    worst = float(s.min())
    # How many pairs sit AT the worst value. A fit set by one accidental pair is a fit
    # nobody should trust; a worst value shared by a dozen pairs is a repeating lattice
    # relationship, which is what a lattice fit ought to be answering to.
    ties = int((s < worst * (1.0 + 1e-4)).sum())
    return worst, int((s < 1.0).sum()), len(s), ties


def fmt_s(s):
    return f">{S_MAX:g}" if s > S_MAX else f"{s:.2f}"


def self_test():
    """Two known pairs, so the SAT above is proven rather than trusted.

    Axis-aligned unit octahedra (semi-axes 1) whose centres are d apart along x touch at
    d = 2, so the touching scale is exactly d/2. Along the (1,1,1) diagonal a flat FACE is
    what meets - the octahedron's support there is 1/sqrt(3), so they touch at
    |d| = 2/sqrt(3). That second case is the one a sphere approximation gets wrong, which
    is why it is the one worth asserting."""
    I = np.eye(3)
    for d in (1.0, 2.0, 5.0):
        s = touching_scales(np.zeros(3), I, np.array([[d, 0, 0]]), I[None])[0]
        assert abs(s - d / 2.0) < 1e-9, f"axis pair at {d}: {s} != {d / 2}"
    diag = np.ones(3) / math.sqrt(3.0)
    for d in (1.0, 3.0):
        s = touching_scales(np.zeros(3), I, (diag * d)[None], I[None])[0]
        expected = d / (2.0 / math.sqrt(3.0))
        assert abs(s - expected) < 1e-9, f"diagonal pair at {d}: {s} != {expected}"


# ----------------------------------------------------------------------------
# Authoring
# ----------------------------------------------------------------------------


def admits_target_scale():
    """Does the leaf-stamping path widen the clamp before stating the size?

    PrismScaleAnimator.SetTargetScale clamps into [minScale, maxScale] INSIDE the setter,
    with no log and no return value, so a fitted axis outside that window is silently
    replaced and every offline measurement here would describe a prism the engine never
    built (Docs/ECOSYSTEM.md 34.9). Read from the C#, because this is exactly the kind of
    guarantee that gets refactored away."""
    src = FLORA_CS.read_text(encoding="utf-8-sig")
    return "AdmitTargetScale(leafSize)" in src


def write_leaf_size(path, leaf):
    """Replace just the LeafSize row, so every other decision in that Variant block -
    budget, tempo, planting radius, the shield cadence itself - survives untouched.

    Only ever a REPLACE: both Charge assets already ship a populated Variant block, and a
    fitter that rewrites the whole block races author_flora_populations.py for ownership of
    MaxTotalSpawnedObjects depending on which ran last."""
    text = path.read_text()
    row = f"    LeafSize: {{x: {leaf[0]:g}, y: {leaf[1]:g}, z: {leaf[2]:g}}}"
    new, n = re.subn(r"^    LeafSize: \{[^}]*\}$", row, text, count=1, flags=re.M)
    if n != 1:
        raise SystemExit(f"could not find a LeafSize row in {path.name}")
    if new != text:
        path.write_text(new)
    return path


# ----------------------------------------------------------------------------


def measure_species(name, asset_prefix, frames, shield_scale):
    """Report every element of one species; return the Charge row (the shielded one)."""
    print(f"\n{name}")
    print(f"{'element':>8} {'shield':>7} {'lattice':>8} {'spacing':>8} "
          f"{'leaf':>22} {'box s*':>8} {'shield s*':>10}  interpenetrating")

    charge = None
    cache = {}
    for element in ELEMENTS:
        leaf, lattice, shield_period = variant(asset_prefix, element)
        if lattice not in cache:
            cache[lattice] = frames(lattice)
        P, Rs = cache[lattice]

        D = np.linalg.norm(P[:, None, :] - P[None, :, :], axis=2)
        np.fill_diagonal(D, np.inf)
        spacing = float(D.min(axis=1).mean())

        half = leaf / 2.0
        box_s, *_ = worst_pair(P, Rs, half, 1.0)
        sh_s, sh_pairs, sh_total, sh_ties = worst_pair(P, Rs, half, shield_scale)

        leaf_txt = f"{leaf[0]:g} x {leaf[1]:g} x {leaf[2]:g}"
        print(f"{element:>8} {('ON' if shield_period > 0 else 'off'):>7} "
              f"{lattice:>8.4f} {spacing:>8.2f} {leaf_txt:>22} "
              f"{fmt_s(box_s):>8} {fmt_s(sh_s):>10}  {sh_pairs} of {sh_total}")

        if element == "Charge":
            charge = dict(species=name, asset=asset_prefix, leaf=leaf, shield_s=sh_s,
                          ties=sh_ties, shield=shield_period, P=P, Rs=Rs)
    return charge


def fit(charge, shield_scale, window):
    """Print the fit for one species' Charge leaf; return (fitted leaf, settled?)."""
    s = charge["shield_s"]
    # The CORRECTION to apply to whatever is shipped, so re-running is idempotent: the fit
    # is the FIXED POINT s* = 1 / (1 - CLEARANCE), not a fresh multiply of the current leaf.
    # A fitter that re-multiplies its own output walks the value every time it is run.
    k = s * (1.0 - CLEARANCE)
    settled = abs(k - 1.0) <= FIT_TOLERANCE
    fitted = charge["leaf"] if settled else np.floor(charge["leaf"] * k * 100.0) / 100.0

    print(f"\n  {charge['species']} Charge")
    print(f"    shipped leaf      {charge['leaf'][0]:g} x {charge['leaf'][1]:g} x "
          f"{charge['leaf'][2]:g}   (volume {np.prod(charge['leaf']):.2f} per prism)")
    print(f"    shields touch at  x{s:.4f}  ->  "
          + (f"they overlap {1.0 / s:.2f}x oversize" if s < 1.0
             else f"they clear by {s - 1.0:.0%}"))
    print(f"    worst shared by   {charge['ties']} pairs - a repeating lattice "
          f"relationship, not one outlier")
    print(f"    target            {CLEARANCE:.0%} clearance, s* = "
          f"{1.0 / (1.0 - CLEARANCE):.4f}  ->  correction x{k:.4f}")
    if settled:
        print(f"    fitted leaf       unchanged - inside the +-{FIT_TOLERANCE:.0%} band")
    else:
        print(f"    fitted leaf       {fitted[0]:g} x {fitted[1]:g} x {fitted[2]:g}   "
              f"(volume {np.prod(fitted):.2f} per prism, "
              f"x{np.prod(fitted) / np.prod(charge['leaf']):.3f})")

    check_s, check_pairs, *_ = worst_pair(charge["P"], charge["Rs"], fitted / 2.0,
                                          shield_scale)
    if check_pairs or check_s < 1.0:
        raise SystemExit(f"FAILED: the fitted {charge['species']} size still interpenetrates")
    box_check, *_ = worst_pair(charge["P"], charge["Rs"], fitted / 2.0, 1.0)
    print(f"    verified          shields s* {check_s:.3f}, 0 interpenetrating; "
          f"plates s* {fmt_s(box_check)}")

    # The silent clamp (Docs/ECOSYSTEM.md 34.9): a fitted axis outside the prefab's authored
    # [minScale, maxScale] is replaced inside the setter with no log and no return value.
    min_scale, max_scale = window
    outside = [(ax, v, lo, hi) for ax, v, lo, hi
               in zip("xyz", fitted, min_scale, max_scale) if v < lo or v > hi]
    if outside:
        for ax, v, lo, hi in outside:
            print(f"    NOTE              {ax} = {v:g} is outside the prefab's clamp "
                  f"[{lo:g}, {hi:g}]")
        if not admits_target_scale():
            raise SystemExit("FAILED: the fitted size would be silently clamped - "
                             "Flora.AddHealthBlock no longer calls AdmitTargetScale")
        print("    ...admitted by Flora.AddHealthBlock's AdmitTargetScale, which lowers "
              "minScale before stating the size")

    return fitted, settled


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--write", action="store_true",
                    help="author the fitted leaf size onto each species' Charge asset")
    ap.add_argument("--check", action="store_true",
                    help="fail if a shipped Charge leaf is not the fitted one")
    args = ap.parse_args()

    self_test()

    shield_scale = circumscribing_scale()
    box_size, box_centre, min_scale, max_scale = health_block_box()
    if not np.allclose(box_size, 1.0) or not np.allclose(box_centre, 0.0):
        raise SystemExit(f"HealthBlock.prefab BoxCollider is {box_size} @ {box_centre}; "
                         "this fit assumes the shipped unit cube centred on the origin - "
                         "re-derive the half-extent mapping before trusting the numbers")

    print("=" * 78)
    print("Shield clearance - fitting each SHIELDED lattice species to its own lattice")
    print("=" * 78)
    print(f"OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE = {shield_scale:g}  ->  a shield "
          f"reaches {shield_scale / 2:g} x leafSize from the prism centre")
    print(f"HealthBlock BoxCollider {box_size[0]:g} x {box_size[1]:g} x {box_size[2]:g} "
          f"centred - so world half-extent = leafSize / 2")
    print(f"HealthBlock scale clamp min {min_scale[0]:g}, max {max_scale[0]:g} "
          f"(per axis; widened by AdmitTargetScale for an AUTHORED size)")

    charges = [measure_species(name, prefix, frames, shield_scale)
               for name, prefix, frames in SPECIES]

    print("\n" + "-" * 78)
    print("The fit")
    print("-" * 78)
    print("s* is the uniform scale at which the WORST pair exactly touches: >= 1 is clear")
    print("as shipped, < 1 is the factor that would stop the interpenetration. Only the")
    print("SHIELDED element has to answer for its octahedra - the other three draw the box")
    print("they were fitted as.")

    results = []
    for charge in charges:
        if charge["shield"] <= 0:
            print(f"\n  {charge['species']} Charge does not shield "
                  f"(ShieldPeriod <= 0) - nothing to fit.")
            continue
        results.append((charge, *fit(charge, shield_scale, (min_scale, max_scale))))

    if args.write:
        print()
        for charge, fitted, settled in results:
            path = LIFEFORMS / f"{charge['asset']} Flora Charge.asset"
            if settled:
                print(f"  {path.name} is already at the fit - nothing written.")
            else:
                write_leaf_size(path, fitted)
                print(f"  wrote {path.relative_to(ROOT)}")
        return 0

    if args.check:
        drift = [c["species"] for c, _, settled in results if not settled]
        if drift:
            print(f"\nFAILED: not at the fit: {', '.join(drift)}")
            return 1
        print("\nevery shielded lattice species is at its fit")
        return 0

    if any(not settled for _, _, settled in results):
        print("\n(re-run with --write to author it)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
