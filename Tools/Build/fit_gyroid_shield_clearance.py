#!/usr/bin/env python3
"""
Fit a SHIELDED gyroid element's leaf size so its octahedra have room on the lattice.

    python3 Tools/Build/fit_gyroid_shield_clearance.py            # measure + report
    python3 Tools/Build/fit_gyroid_shield_clearance.py --write    # + author the Charge asset
    python3 Tools/Build/fit_gyroid_shield_clearance.py --check    # CI: assert the shipped fit

WHY THIS IS MEASURED AND NOT AUTHORED BY EYE
--------------------------------------------
Charge is the element that SHIELDS its leaves (FloraVariantTuning.ShieldPeriod - the
Charge gyroid has shipped at 1 since the elemental contract landed).  A shielded prism
does not merely change colour: PrismStateManager engages a PrismOctahedronShield, which
replaces the box with the octahedron that CIRCUMSCRIBES it -
OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE = 3 applied to the box HALF-extents, i.e. a
body reaching 1.5 x leafSize from the prism centre along each local axis and enclosing
4.5x the box volume.

So a shield TRIPLES a prism's reach, and that is the whole problem: MEASURED against the
shipped bond table, the gyroid's plain leaves are already clear of one another with only
59% of headroom to spare (Charge / Time s* = 1.59, Mass 1.99, Space 1.26 - the leaf very
nearly spans its bond, 9u against a 7.84u mean bond, but it does not touch its
neighbours).  Tripling that reach therefore does not draw a lattice of octahedra, it draws
one interpenetrating solid: at the ORIGINAL 9 x 3.4 x 1.5 Charge leaf the shields overlapped
at 1.89x the size that would let them merely touch, 826 interpenetrating pairs out of 15,880
near pairs in a 220-prism walk.  That is what this fitted away.

That headroom is why only the SHIELDED element has to answer for this: the other three
elements draw the box they were fitted as, and Charge is the one that draws something 3x
bigger than the body it was fitted for.

"The octahedra have clearance" is a geometric claim about a specific point set - the
SHIPPED bond table walked at the element's own LatticeScale - so it is fitted here rather
than guessed.  Each prism is an oriented box (Unity LookRotation from the bond table's
DeltaForward / DeltaUp), its shield is the octahedron conv{+-1.5*Lx*x, +-1.5*Ly*y,
+-1.5*Lz*z}, and whether two neighbouring shields interpenetrate is an exact
separating-axis question.  The same idiom as Tools/Build/fit_schwarz_p_leaf_sizes.py,
which fits that species' plates flush against ITS measured site set.

WHAT IS FITTED, AND WHAT IS NOT
-------------------------------
Only the leaf SIZE moves, uniformly on all three axes: the Charge leaf's ASPECT is its
identity (9 : 3.4 : 1.5, shared with Time) and a uniform shrink keeps it exactly.  The LATTICE is
untouched - separationDistance / LatticeScale, the bond table, and every coherence
tolerance (snap, mate-search radius, reservation floor, AssembledFlora.MisalignmentRadius)
stay exactly as shipped.  That is deliberate and is the cheap half of Docs/ECOSYSTEM.md
34.8: those tolerances are absolute distances measured against this lattice, so scaling
the lattice drags a whole family of constants with it, while scaling the PRISM drags
nothing - GyroidAssembler reads Prism.TargetScale only to stamp it onto the next prism
(GyroidAssembler.ConvertBlock), never to place one.

The trade the fit makes is stated plainly by the numbers it prints: shrinking the leaf to
clear the shields makes the UNSHIELDED plant a sparser skeleton of small plates, and the
shields are what fill the lattice back in.  That is the shape of the ask - clearance for
the octahedra that fill in - and it is why the fit targets shields that TOUCH rather than
shields that merely stop being a blob.

THE OTHER SHIELDED LATTICE SPECIES
----------------------------------
Schwarz P's Charge variant now shields too, and its plates were fitted FLUSH against its own
site set (fit_schwarz_p_leaf_sizes.py), so it has exactly the same problem.  This reports it
- read-only, using that fitter's own frames so the number is reproducible - but does not
change it: the ask was the gyroid, and shrinking a second species' prisms is a look decision
somebody has to want.  Fitting it is the same arithmetic (uniform x its own s*).

VOLUME
------
Prism volume is Lx*Ly*Lz, so a uniform k shrink is a k^3 volume change landing straight on
the host cell's Frenzy ladder (Docs/ECOSYSTEM.md 4.6 / 34.8).  This shrinks, so Frenzy
arrives LATER - the safe direction, and no ladder is re-authored for it.  The report prints
the resulting per-plant volume; the Blob cell arithmetic it lands in (gyroid ceiling
85% -> 71% of FrenzyEnterVolume) is recorded in Docs/ECOSYSTEM.md 35.4.

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

SITES = ("TopRight", "TopLeft", "BottomLeft", "BottomRight")

# How much room to leave beyond "just touching", as a fraction of the touching size. The
# shields are a VISUAL of the plant's armour: at exactly 1.0 they kiss, which reads as one
# fused surface again the moment growth drift moves a prism a fraction of a unit. A tenth
# is enough that the individual octahedra read while the lattice still fills in.
CLEARANCE = 0.10

# How far the shipped leaf may sit from the fit before it is re-authored. The fit is a fixed
# point (s* = 1 / (1 - CLEARANCE)), and without a band a 2-decimal rounding residue would make
# every run report drift and --check fail on an asset this very tool wrote.
FIT_TOLERANCE = 0.02

# Enough of the lattice that the interior sites have their full neighbourhood - the worst
# pair is always interior, so a bigger walk cannot lower the fitted size, only cost time.
WALK_NODES = 220


# ----------------------------------------------------------------------------
# Shipped constants - read, never re-typed
# ----------------------------------------------------------------------------


def circumscribing_scale():
    """OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE - the factor on the box HALF-extents."""
    src = OCTAHEDRON_CS.read_text(encoding="utf-8-sig")
    return float(re.search(r"CIRCUMSCRIBING_SCALE\s*=\s*([\d.]+)f", src).group(1))


def health_block_box():
    """The authored BoxCollider on HealthBlock.prefab, in LOCAL units.

    The shield's half-extents come from this collider (PrismOctahedronShield.CacheGeometry
    reads BoxCollider.size, NOT transform.localScale, because growth animates the scale),
    so a leaf size only means "the prism's world size" while this stays a unit cube
    centred on the origin. Asserted rather than assumed."""
    src = HEALTH_BLOCK.read_text(encoding="utf-8-sig")
    body = src[src.index("BoxCollider:"):]
    size = re.search(r"m_Size: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}", body)
    centre = re.search(r"m_Center: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}", body)
    return (np.array([float(v) for v in size.groups()]),
            np.array([float(v) for v in centre.groups()]))


def gyroid_variant(element):
    """(leafSize, latticeScale, shieldPeriod) as shipped for one gyroid element."""
    text = (LIFEFORMS / f"Gyroid Flora {element}.asset").read_text()
    leaf = re.search(r"^    LeafSize: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}$",
                     text, re.M)
    lattice = re.search(r"^    LatticeScale: ([-\d.]+)$", text, re.M)
    shield = re.search(r"^    ShieldPeriod: ([-\d.]+)$", text, re.M)
    return (np.array([float(v) for v in leaf.groups()]),
            float(lattice.group(1)) if lattice else 1.0,
            float(shield.group(1)) if shield else -1.0)


# ----------------------------------------------------------------------------
# The prism frames - the same walk verify_gyroid_lattice_scale.py asserts against
# ----------------------------------------------------------------------------


def walk_frames(table, separation, budget=WALK_NODES):
    """Grow the lattice exactly as GyroidAssembler does, keeping each node's ROTATION.

    A prism's transform IS the node: GetGrowthInfo returns CalculateGlobalBondSite /
    CalculateRotation and the spawned prism is placed there, so these frames are the
    prism poses the octahedra are drawn in."""
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
    frontier = [0]
    head = 0
    while head < len(frontier) and len(nodes) < budget:
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
            if Rc is None or seen(child) or len(nodes) >= budget:
                continue
            grid.setdefault(key(child), []).append(child)
            nodes.append((child, Rc, e["BlockType"]))
            frontier.append(len(nodes) - 1)

    return (np.array([n[0] for n in nodes]),
            np.array([n[1] for n in nodes]))


# ----------------------------------------------------------------------------
# Separating-axis test, in the ONE form that answers the question asked
# ----------------------------------------------------------------------------
#
# Both bodies are centrally symmetric about their prism's centre, so scaling a pair by s
# scales every projection radius by s while the centre offset d is fixed. Two convex
# polytopes are disjoint iff SOME candidate axis separates them, so
#
#     touching scale  s* = max over candidate axes of  |d.u| / (rA(u) + rB(u))
#
# is exact, not a bisection: at any s < s* that axis separates them, at any s > s* no axis
# does. s* >= 1 means the shipped pair is already clear; s* < 1 is the factor the pair
# would have to shrink by to stop interpenetrating.


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
    candidate set for convex polytopes, and both bodies are centrally symmetric about
    their prism's centre - so scaling a pair by s scales every projection radius by s
    while the centre offset d is fixed, and

        s* = max over candidate axes of  |d.u| / (rA(u) + rB(u))

    is exact rather than a bisection: below s* that axis separates them, above it none
    does. s* >= 1 means the pair is clear as shipped; s* < 1 is the factor the pair would
    have to shrink by to stop interpenetrating.

    Batched over B because the fit runs this over every near pair of a 220-prism walk and
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
    """The three semi-axis VECTORS of one prism's shield, in world units."""
    return (R * (half_extents * shield_scale)).T      # rows: x, y, z semi-axes


# How far past "touching" the report still measures. A pair whose bodies are further
# apart than S_MAX x their own size is not the pair that decides anything; the cutoff
# only exists so the O(n^2) SAT does not run over the whole walk. Report ">S_MAX" rather
# than a silent infinity when NOTHING is near - "no pair can touch" is a real answer and
# has to look different from "the cutoff hid them".
S_MAX = 2.5


def worst_pair(P, Rs, half_extents, shield_scale):
    """(min touching scale, overlapping pair count) over the whole walk.

    The min is exact for every pair whose touching scale is <= S_MAX, which is every pair
    that can decide a fit; beyond that it saturates and the report prints the bound."""
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
    # nobody should trust; a worst value shared by a dozen pairs is a repeating bond
    # relationship, which is what a lattice fit ought to be answering to.
    ties = int((s < worst * (1.0 + 1e-4)).sum())
    return worst, int((s < 1.0).sum()), len(s), ties


def fmt_s(s):
    return f">{S_MAX:g}" if s > S_MAX else f"{s:.2f}"


def self_test():
    """Two known pairs, so the SAT above is proven rather than trusted.

    Axis-aligned unit octahedra (semi-axes 1) whose centres are d apart along x touch at
    d = 2, so the touching scale is exactly d/2. Offset them along the (1,1,1) diagonal
    instead and the octahedron's support in that direction is 1/sqrt(3) x |d| ... which
    is the case a sphere approximation gets wrong, so it is the one worth asserting."""
    I = np.eye(3)
    for d in (1.0, 2.0, 5.0):
        s = touching_scales(np.zeros(3), I, np.array([[d, 0, 0]]), I[None])[0]
        assert abs(s - d / 2.0) < 1e-9, f"axis pair at {d}: {s} != {d / 2}"
    # Along the diagonal the flat face is what meets: the support of the unit octahedron
    # in direction (1,1,1)/sqrt(3) is 1/sqrt(3), so two of them touch at |d| = 2/sqrt(3).
    diag = np.ones(3) / math.sqrt(3.0)
    for d in (1.0, 3.0):
        s = touching_scales(np.zeros(3), I, (diag * d)[None], I[None])[0]
        expected = d / (2.0 / math.sqrt(3.0))
        assert abs(s - expected) < 1e-9, f"diagonal pair at {d}: {s} != {expected}"


# ----------------------------------------------------------------------------
# Authoring
# ----------------------------------------------------------------------------


def variant_fields():
    """Every serialized field of FloraVariantTuning with its type, read from the C# so
    this cannot drift when a field is added (same helper as the Schwarz P fitter)."""
    src = VARIANT_CS.read_text()
    body = re.search(r"public class FloraVariantTuning\s*\{(.*?)\n    \}", src, re.S).group(1)
    body = re.sub(r"^(?:\s*\[[^\]]*\]\s*)+", "", body, flags=re.M)
    out = []
    for line in body.splitlines():
        m = re.match(r"\s*public ([\w<>]+) (\w+)\s*=\s*([^;]+);", line)
        if m:
            out.append((m.group(2), m.group(1)))
    return out


def write_leaf_size(path, leaf):
    """Replace just the LeafSize row, so every other decision in that Variant block -
    budget, tempo, planting radius, the shield cadence itself - survives untouched.

    Only ever a REPLACE: the Charge gyroid already ships a populated Variant block, and a
    fitter that rewrites the whole block races author_flora_populations.py for ownership
    of MaxTotalSpawnedObjects depending on which ran last."""
    text = path.read_text()
    row = f"    LeafSize: {{x: {leaf[0]:g}, y: {leaf[1]:g}, z: {leaf[2]:g}}}"
    new, n = re.subn(r"^    LeafSize: \{[^}]*\}$", row, text, count=1, flags=re.M)
    if n != 1:
        raise SystemExit(f"could not find a LeafSize row in {path.name}")
    if new != text:
        path.write_text(new)
    return path


# ----------------------------------------------------------------------------


def report_schwarz_p():
    """The same measurement on Schwarz P, whose Charge variant also shields now.

    Read-only, and it borrows fit_schwarz_p_leaf_sizes' OWN frames rather than re-deriving
    the tile - so this cannot report a different surface than the one that species is
    actually fitted against."""
    try:
        import fit_schwarz_p_leaf_sizes as sp
    except Exception as exc:                                  # pragma: no cover
        print(f"\n(Schwarz P report unavailable: {exc})")
        return

    _, levels = sp.parse_cs()
    level = sp.resolve_level(levels)
    centres, bases, _ = sp.prism_frames(levels, level)
    centres = np.asarray(centres, float)
    bases = np.asarray(bases, float)                          # rows = the local axes

    text = (LIFEFORMS / "SchwarzP Flora Charge.asset").read_text()
    leaf = np.array([float(v) for v in re.search(
        r"^    LeafSize: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}$", text, re.M).groups()])

    print("\n" + "-" * 78)
    print("The other shielded lattice species (reported, NOT fitted)")
    print("-" * 78)
    print(f"SchwarzP Flora Charge  leaf {leaf[0]:g} x {leaf[1]:g} x {leaf[2]:g}  "
          f"over {len(centres)} prisms of a 3x3x3 tile block")

    for scale, label in ((1.0, "plates"), (circumscribing_scale(), "shields")):
        S = np.array([(bases[i].T * (leaf / 2.0 * scale)).T for i in range(len(bases))])
        reach = np.linalg.norm(S, axis=2).max()
        cutoff = 2.0 * reach * S_MAX
        worst, overlaps, total = math.inf, 0, 0
        for i in range(len(centres)):
            d = np.linalg.norm(centres[i + 1:] - centres[i], axis=1)
            near = np.nonzero(d < cutoff)[0] + i + 1
            if len(near) == 0:
                continue
            s = touching_scales(centres[i], S[i], centres[near], S[near])
            worst = min(worst, float(s.min()))
            overlaps += int((s < 1.0).sum())
            total += len(s)
        print(f"  {label:>8}  s* {fmt_s(worst):>6}   interpenetrating {overlaps} of {total}")

    print("  the plates are flush by construction (fit_schwarz_p_leaf_sizes.py); the shields")
    print("  are not, for the same reason the gyroid's were not. Fitting it is a uniform")
    print("  shrink by its own s*, exactly as above - deliberately left for a decision.")


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--write", action="store_true",
                    help="author the fitted leaf size onto Gyroid Flora Charge.asset")
    ap.add_argument("--check", action="store_true",
                    help="fail if the shipped Charge leaf is not the fitted one")
    args = ap.parse_args()

    self_test()

    shield_scale = circumscribing_scale()
    box_size, box_centre = health_block_box()
    if not np.allclose(box_size, 1.0) or not np.allclose(box_centre, 0.0):
        raise SystemExit(f"HealthBlock.prefab BoxCollider is {box_size} @ {box_centre}; "
                         "this fit assumes the shipped unit cube centred on the origin - "
                         "re-derive the half-extent mapping before trusting the numbers")

    table = parse_bond_table()

    print("=" * 78)
    print("Gyroid shield clearance - fitting the SHIELDED element's leaf to its lattice")
    print("=" * 78)
    print(f"OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE = {shield_scale:g}  ->  a shield "
          f"reaches {shield_scale / 2:g} x leafSize from the prism centre")
    print(f"HealthBlock BoxCollider {box_size[0]:g} x {box_size[1]:g} x {box_size[2]:g} "
          f"centred - so world half-extent = leafSize / 2\n")

    print(f"{'element':>8} {'shield':>7} {'lattice':>8} {'bond':>7} "
          f"{'leaf':>20} {'box s*':>8} {'shield s*':>10}  overlapping pairs")

    rows = {}
    for element in ("Charge", "Mass", "Space", "Time"):
        leaf, lattice, shield_period = gyroid_variant(element)
        sep = MEASURED_SEPARATION * lattice
        P, Rs = walk_frames(table, sep)
        D = np.linalg.norm(P[:, None, :] - P[None, :, :], axis=2)
        np.fill_diagonal(D, np.inf)
        bond = float(D.min(axis=1).mean())

        half = leaf / 2.0
        box_s, *_ = worst_pair(P, Rs, half, 1.0)
        sh_s, sh_pairs, sh_total, sh_ties = worst_pair(P, Rs, half, shield_scale)
        rows[element] = dict(leaf=leaf, lattice=lattice, bond=bond, shield=shield_period,
                             box_s=box_s, shield_s=sh_s, ties=sh_ties, P=P, Rs=Rs)

        leaf_txt = f"{leaf[0]:g} x {leaf[1]:g} x {leaf[2]:g}"
        print(f"{element:>8} {('ON' if shield_period > 0 else 'off'):>7} "
              f"{lattice:>8.4f} {bond:>7.2f} {leaf_txt:>20} "
              f"{fmt_s(box_s):>8} {fmt_s(sh_s):>10}  {sh_pairs} of {sh_total}")

    print("\ns* is the uniform scale at which the WORST pair exactly touches:")
    print("  s* >= 1  the bodies are clear as shipped")
    print("  s* <  1  they interpenetrate, and s* is the factor that would stop it")
    print("Every element's plain BOXES are clear - the leaf nearly spans its bond but does")
    print("not touch its neighbours. Tripling that reach is what fuses the plant, so only")
    print("the SHIELDED element has to answer for its octahedra.")

    charge = rows["Charge"]
    if charge["shield"] <= 0:
        print("\nCharge gyroid does not shield (ShieldPeriod <= 0) - nothing to fit.")
        return 0

    s = charge["shield_s"]
    # The CORRECTION to apply to whatever is shipped, so re-running is idempotent: the fit is
    # the FIXED POINT s* = 1 / (1 - CLEARANCE), not a fresh multiply of the current leaf. A
    # fitter that re-multiplies its own output walks the value every time somebody runs it.
    k = s * (1.0 - CLEARANCE)
    settled = abs(k - 1.0) <= FIT_TOLERANCE
    fitted = charge["leaf"] if settled else np.floor(charge["leaf"] * k * 100.0) / 100.0

    print("\n" + "-" * 78)
    print("The fit")
    print("-" * 78)
    print(f"shipped Charge leaf   {charge['leaf'][0]:g} x {charge['leaf'][1]:g} x "
          f"{charge['leaf'][2]:g}   (volume {np.prod(charge['leaf']):.2f} per prism)")
    print(f"shields touch at      x{s:.4f}  ->  "
          + (f"they overlap {1.0 / s:.2f}x oversize" if s < 1.0
             else f"they clear by {s - 1.0:.0%}"))
    print(f"worst pair shared by  {charge['ties']} pairs - a repeating bond relationship, "
          f"not one outlier")
    print(f"target                {CLEARANCE:.0%} clearance, i.e. s* = "
          f"{1.0 / (1.0 - CLEARANCE):.4f}  ->  correction x{k:.4f}")
    if settled:
        print(f"fitted Charge leaf    unchanged - already inside the +-{FIT_TOLERANCE:.0%} band")
    else:
        print(f"fitted Charge leaf    {fitted[0]:g} x {fitted[1]:g} x {fitted[2]:g}   "
              f"(volume {np.prod(fitted):.2f} per prism)")

    # Prove the fit on the same geometry it was derived from.
    check_s, check_pairs, *_ = worst_pair(charge["P"], charge["Rs"], fitted / 2.0, shield_scale)
    print(f"\nverify at the fitted size: worst pair s* = {check_s:.3f}, "
          f"overlapping pairs = {check_pairs}")
    if check_pairs or check_s < 1.0:
        raise SystemExit("FAILED: the fitted size still interpenetrates")
    box_check, *_ = worst_pair(charge["P"], charge["Rs"], fitted / 2.0, 1.0)
    print(f"unshielded boxes at the fitted size: s* = {fmt_s(box_check)} - the plates "
          f"are loose plates the shields fill back in")

    budget = re.search(r"^    MaxTotalSpawnedObjects: (\d+)$",
                       (LIFEFORMS / "Gyroid Flora Charge.asset").read_text(), re.M)
    if budget:
        n = int(budget.group(1))
        print(f"\nper-plant volume at its {n}-prism budget: {np.prod(fitted) * n:.0f}   "
              f"(a shrink only ever moves Frenzy LATER - Docs/ECOSYSTEM.md 4.6 - so no")
        print("ladder is re-authored for it)")

    if args.write:
        if settled:
            print("\nGyroid Flora Charge.asset is already at the fit - nothing written.")
        else:
            path = write_leaf_size(LIFEFORMS / "Gyroid Flora Charge.asset", fitted)
            print(f"\nwrote {path.relative_to(ROOT)}")
        return 0

    if args.check:
        if not settled:
            print(f"\nFAILED: shipped {charge['leaf']} wants a x{k:.4f} correction "
                  f"(fit {fitted})")
            return 1
        print("\nshipped Charge leaf is at the fit")
        return 0

    report_schwarz_p()

    if not settled:
        print("\n(re-run with --write to author it)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
