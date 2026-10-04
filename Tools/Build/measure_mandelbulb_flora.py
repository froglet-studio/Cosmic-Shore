#!/usr/bin/env python3
"""Measure the Mandelbulb flora — the MODEL half of the species' tool trio.

Everything this species claims about itself is a number from here: prism counts, per-prism
and per-plant volume, the size spread, how evenly a plant covers its own surface, how
deeply two prisms may interleave, the Charge shield fit, THE FALL (the log-spiral dive to
the heart) and THE WATERSHED (the Morse–Smale skeleton species). The growth rule itself
lives in mandelbulb_flora_model.py, which walks the SHIPPED spherical-harmonic table.

    measure_mandelbulb_flora.py                 # the report
    measure_mandelbulb_flora.py --check         # fail the build on a broken bound
    measure_mandelbulb_flora.py --render DIR    # PNGs — you cannot judge a plant you have
                                                # not looked at, and an offline model can
                                                # report a perfect size distribution for a
                                                # form that reads as gravel
    measure_mandelbulb_flora.py --shields       # re-solve Charge's cross-section
    measure_mandelbulb_flora.py --fit-volume    # re-solve VOLUME_GAIN

EVERY GATE IS A MEASUREMENT ON THE PLANT THE GAME LAYS — the walk, then the claim
(MandelbulbFlora.Claim), then the budget. A gate on the emitted path would describe
candidates, and the claim filter is exactly the thing that puts holes in a dive.

THE TWO BOUNDS, and why they are bounds rather than a zero. A lattice species can claim
zero overlapping pairs because its prisms sit on a regular lattice. This one cannot and
does not try: prisms are laid along CURVES that cross each other, so two ribbons meeting at
an angle have bounding boxes that must overlap. It states instead (1) what fraction of
TOUCHING pairs interpenetrate at all and (2) how deeply the worst of them does — a zero you
cannot have is worse than a bound you can measure.

THE CHARGE ORDERING. Armouring multiplies a plant's own silhouette by exactly
0.5 * CIRCUMSCRIBING_SCALE^2 = 4.5, so a correctly fitted Charge plant is the DENSEST of
the four while shielded and the sparsest once stripped. That ordering is the two-pass
grazing cost made visible, and --check fails if it ever flips.

CHARGE IS THEREFORE MEASURED ARMOURED WHEREVER THE QUESTION IS "WHAT IS ON SCREEN" — a
Charge plant's leaves are shielded by law, so the bare box is a body nobody sees. That is
the gasket LADDER (`arena_octaves(shield=True)`, measured: it is what takes the tuned
Apollonia Charge from 1 legible octave to 3) and it is NOT the interpenetration bounds,
which are about how the plant is BUILT. Where the armour is measured, the bare figure is
reported beside it, because the pair IS the ordering above.

A BOUND'S POPULATION IS PART OF THE BOUND. Three of the gates here were stated over a
population wider than the one their own prose described and their own levers could reach —
the core armour over a curve's CHAIN as well as its bundle, the sunburst over a surface
census a gradient-flow species cannot move, the seed spread against a constant while its own
sample is a per-element fraction. Each is now stated over the population the message names,
with the rest MEASURED AND REPORTED beside it. A number that cannot be moved belongs in the
report, not in the verdict.

THE FALL's gates are separable by construction: a dive prism carries `TanR != 0` and a
surface prism carries exactly 0, so every Fall statistic below is a clean partition of the
laid plant rather than a re-derivation of which prisms were the dive.

APOLLONIA's gates measure a PACKING rather than a set of curves, and most of them are
things nothing above them can see — the FULL FORM as a BAND (a one-sided ceiling passes an
81% plant in silence while it is priced at 100% of its collider budget), RING INTEGRITY
(measured before the budget, because the budget truncates exactly the ring a naive reading
would call broken), THE LADDER stated in PIXELS (stated in rings it is satisfied by exactly
the octaves that were already visible), a LADDER HOLE (a rung the disc set skipped, which
the ladder gate can only score as illegible) and TANGENCY (a statement about the disc set,
which the laid plant cannot show). Two of them are MODEL-REGRESSION guards and say so at
the point of measurement: the ORDERING equality and the level-0 DISC count are identities
of the model and cannot fire against the shipped algorithm — which is why level 0 is gated
on the rings that reach the LAID plant instead.

THE WATERSHED's gates only run where the species authors the mechanism
(`SkeletonSeeds != 0`), the Fall's only where it authors a dive
(`DiveStepFraction > 0 && DiveCount > 0`), and the gasket's only where it authors one
(`GasketLevels != 0`) — a gate that runs on a species with no such mechanism is measuring a
zero and reporting it as a pass.
"""
import argparse
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mandelbulb_flora_model as M                                    # noqa: E402

# PrismStateManager.ActivateShield engages the octahedron CIRCUMSCRIBING the prism:
# OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE on the box HALF-extents.
CIRCUMSCRIBING_SCALE = 3.0

# PrismScaleAnimator.SetTargetScale clamps per axis inside the setter, with no log and no
# return value. Flora.AddHealthBlock calls AdmitTargetScale first, which widens it — but a
# size under the FLOOR is still worth reporting, because it is the shape of a fitted size
# that does not read on screen (Docs/ECOSYSTEM.md §34.9).
CLAMP_FLOOR, CLAMP_CEILING = 0.5, 10.0

# ── THE FALL's bounds ──────────────────────────────────────────────────────────
DIVE_ARRIVAL_MIN = 0.60          # laid dives / requested dives
DIVE_TIP_MARGIN = 0.50           # world units of clear air between a tip and the crystal
DIVE_CENTRE_BAND = (2.0, 6.0)    # the innermost laid prism's CENTRE, world units
DIVE_HOLE_FACTOR = 3.0           # a hole, in multiples of the MEDIAN surface prism length
DIVE_WINDING_PER_EFOLD = 80.0    # degrees of azimuth per e-fold of radius, median over dives
DIVE_SHARE_BAND = (0.05, 0.25)   # dive prisms as a share of the plant
RADIAL_FRACTION_MAX = 0.55       # the sunburst gate, over the WHOLE plant — a WALKING species
RADIAL_FRACTION_MAX_SKELETON = 0.80   # ...and a GRADIENT-FLOW one; see radial_bound_for
RADIAL_COS = 0.707               # 45 degrees off the ray to the heart
DIVE_BAND_MIN = 4                # equal-area theta bands the dive set alone must populate
DIVE_END_STOP_FACTOR = 3.5       # a dive has ARRIVED when its LAST LAID prism is inside this many
                                 # stop radii: past it the dive was amputated by the claim filter.
                                 # 3.5 rather than 2 because the dives of one plant all wind about
                                 # ONE axis (DiveAxisAlign), so near the heart they converge into a
                                 # single braid and the claim filter ends every dive but the first
                                 # where it joins it - measured at ~3x the stop radius on every
                                 # walking species. A dive that joins the braid has arrived; a dive
                                 # ending at 85 u (CoralBloom/Space, 5 of 45 prisms laid) has not,
                                 # and it is ARRIVAL that gates it, not a second bound.
DIVE_ROLL_MEDIAN_MAX = 10.0      # degrees, transport-corrected, net of the authored twist
DIVE_ROLL_PAIR_MAX = 60.0
DIVE_CONDITIONING_MIN = 0.30     # |sin angle(ray, forward)| — how well-posed `up` is

# ── THE WATERSHED's bounds ─────────────────────────────────────────────────────
NET_SADDLE_SURVIVAL = 0.65       # saddles keeping >= 3 laid arms
NET_ARMS = 3
NET_MEAN_ARM = 5.0               # laid SURFACE prisms per surviving arm
LANE_SHARE_MIN = 0.15            # no lane starved by the budget
RING_GAP = 0.08                  # the FALLBACK grouping threshold — see ring_gap_for, which
                                 # derives one per element from that element's own histogram
RING_GAP_RATIO_MIN = 2.0         # the ratio that makes a band in the histogram "empty"
AREA_BANDS = 8                   # equal-area latitude bands, pole to pole (see area_band)
SEED_SPREAD_BANDS = 6            # the CEILING on the bound; the bound itself is per element
SEED_SPREAD_DISCOUNT = 0.80      # of the uniform expectation — see seed_spread_bound
SEED_SPREAD_CONTROL_FACTOR = 2   # ...and at least this multiple of the negative control
SEED_SPREAD_PREFIX = 0.25
LENGTH_FACTOR_MIN = 0.85         # a net drawn in dashes reads as dots
PEAKS_ON_PLANT_MIN = 0.50        # a FIRST CUT, not a measured bar — see watershed_report
PEAK_TOUCH_STEPS = 1.5           # walk steps within which a ridge end "reaches" its peak

# The fractal's exponent per element — what the peak rings are a census OF. `order - 1` is
# the fold of the surface's azimuthal symmetry (rotating c by delta is a symmetry of
# v -> v^n + c only when delta*(n-1) is a whole turn), so it is the modal ring size and the
# azimuthal spectrum's line.
BULB_ORDER = {"Charge": 8, "Mass": 5, "Space": 3, "Time": 12}

# Charge's armour at the CORE. The Fall converges every dive on one point, so the tightest
# packing in the species is the innermost bundle and the whole-plant armour figure cannot
# see it (53 of 2800 prisms). Measured inside this fraction of the plant's own radius.
#
# THE BOUND IS OVER THE BUNDLE — cross-curve pairs — AND NOT OVER A CURVE'S OWN CHAIN, which
# is the same separation the BARE bar above already makes and which this one was missing.
# The chain is not a tuning miss, it is a CLOSED FORM: two consecutive armoured prisms sit
# one step apart, a prism is `LengthFactor` x step long and its octahedron reaches
# CIRCUMSCRIBING_SCALE x its half-extents, so they first touch at
#
#     s* = step / (2 x 0.5 x CIRCUMSCRIBING_SCALE x LengthFactor x step) = 1 / (3 x LengthFactor)
#
# — no dive parameter and no cross-section appears in it, which is why the three levers the
# old message named (DiveStopRadius, DiveGirthFloor, DiveCount) are measurably inert against
# it. The core IS the dive bundle (measured: 100% of core prisms carry TanR != 0 on every
# element of all four species), and there the closed form is EXACT — core chain worst-pair
# against 1/(3 x LengthFactor), to four decimal places on all four: FractalFoliage
# 1.1111/1.1111, CoralBloom 0.7407/0.7407, Watershed 1.0700/1.0700, Apollonia 0.7407/0.7407.
#
# SO THE CHAIN FUSES IFF LengthFactor > 1/3, AND THAT CONDITION IS THE WHOLE STORY — do not
# quote one species as the example, because two of the four sit on each side of the cliff.
# Measured core chain, Charge: CoralBloom 55/55 and Apollonia 50/50 = 100% (LengthFactor
# 0.4500, s* 0.7407 < 1), against FractalFoliage 0/46 and Watershed 0/106 = 0.0%
# (LengthFactor 0.3000 and 0.3115, s* 1.1111 and 1.0700 > 1). Where it does fuse it is the
# overwhelming majority of the core's offenders — CoralBloom/Charge 55 of 59, with the
# bundle at 4 of 88 = 4.5% — and it then DOMINATES an un-split figure and drags it the wrong
# way under --shields, because thinning the ribbon shrinks the bundle denominator while the
# chain stays pinned at 100%: over k=1.00 -> 0.25 CoralBloom's un-split core runs 39.0% ->
# 47.8% while its BUNDLE falls 8.3% -> 0.0%. Below the cliff there is no pathology to see —
# FractalFoliage's un-split core runs 10.3% -> 0.0% over the same sweep — which is exactly
# why a species-shaped claim here would be wrong half the time.
#
# CHARGE_DASH is 0.45 by law — fitted on the SURFACE, where that inversion (34.6% fused
# armoured against the siblings' 94.9% bare) is the whole Docs/ECOSYSTEM.md §50 Charge
# result. So the chain is MEASURED AND REPORTED beside the bound, with its closed form, and
# the bound states what the gate's own prose always described: "the Fall's BUNDLE has fused
# into a rod".
CORE_RADIUS_FRACTION = 0.15
CORE_ARMOURED_MAX = 0.15

# The heart. A lifeform's crystal size is authored PER ELEMENT in that species' own variant
# tuning (Docs/ECOSYSTEM.md §40), so the clearance a dive has to leave is read out of the
# shipped asset rather than typed here. A species with no asset yet falls back, and the
# report says which it used.
LIFEFORMS = os.path.join(M.ROOT, "Assets", "_SO_Assets", "Lifeforms")
DEFAULT_HEART_WORLD_SCALE = 1.52


def _asset_prefix(species):
    """"MandelbulbFlora" -> "Mandelbulb Flora" — the shipped assets are named for the
    prefab with its CamelCase split, so the prefix is derived rather than tabulated."""
    return re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", M.SPECIES[species]["prefab"])


def heart_world_scale(species):
    """(scale, provenance). Read from this species' TIME config asset if it exists."""
    path = os.path.join(LIFEFORMS, f"{_asset_prefix(species)} Time.asset")
    if os.path.exists(path):
        m = re.search(r"HeartWorldScale:\s*([-0-9.eE+]+)", open(path).read())
        if m:
            return float(m.group(1)), os.path.basename(path)
    return DEFAULT_HEART_WORLD_SCALE, f"fallback {DEFAULT_HEART_WORLD_SCALE} (no {os.path.basename(path)})"


# ── geometry ───────────────────────────────────────────────────────────────────

def obb(surface, prism, shell, cross):
    """World OBB: centre, the three axes, the three half-extents — exactly what
    MandelbulbFlora.Decide lays (Pose, scaled by the shell radius)."""
    p, fwd, up = M.pose(surface, prism)
    p = M._mul(p, shell)
    right = M._norm(M._cross(up, fwd))
    up = M._cross(fwd, right)
    girth = max(0.05, prism.girth) * shell
    return (p, (right, up, fwd),
            (max(0.005, cross[0] * girth) * 0.5,
             max(0.005, cross[1] * girth) * 0.5,
             max(0.005, prism.length * shell) * 0.5))


def support(axes, half, u):
    return (abs(M._dot(axes[0], u)) * half[0]
            + abs(M._dot(axes[1], u)) * half[1]
            + abs(M._dot(axes[2], u)) * half[2])


def box_axes(a, b):
    """The 15 separating-axis candidates for two boxes: six face normals and nine edge
    crosses. A cross of two near-parallel axes is degenerate and is dropped rather than
    normalised — a zero axis reports every pair as touching."""
    out = list(a[1]) + list(b[1])
    for u in a[1]:
        for v in b[1]:
            c = M._cross(u, v)
            if M._dot(c, c) > 1e-12:
                out.append(M._norm(c))
    return out


def octa_support(axes, half, u):
    """An octahedron with semi-axes `half` along `axes`: its vertices are the six
    +-half[i]*axes[i], so the support is the max rather than the sum."""
    return max(abs(M._dot(axes[0], u)) * half[0],
               abs(M._dot(axes[1], u)) * half[1],
               abs(M._dot(axes[2], u)) * half[2])


def octa_axes(a, b):
    """Face normals (the eight octant planes, one per sign triple) plus edge crosses."""
    def faces(o):
        ax, h = o[1], o[2]
        inv = [1.0 / max(h[i], 1e-9) for i in range(3)]
        out = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                n = M._add(M._add(M._mul(ax[0], sx * inv[0]), M._mul(ax[1], sy * inv[1])),
                           M._mul(ax[2], inv[2]))
                out.append(M._norm(n))
                n = M._add(M._add(M._mul(ax[0], sx * inv[0]), M._mul(ax[1], sy * inv[1])),
                           M._mul(ax[2], -inv[2]))
                out.append(M._norm(n))
        return out

    def edges(o):
        ax, h = o[1], o[2]
        v = [M._mul(ax[i], h[i] * s) for i in range(3) for s in (-1, 1)]
        out = []
        for i in range(6):
            for j in range(i + 1, 6):
                d = M._sub(v[i], v[j])
                if M._dot(d, d) > 1e-12:
                    out.append(M._norm(d))
        return out

    out = faces(a) + faces(b)
    for u in edges(a):
        for w in edges(b):
            c = M._cross(u, w)
            if M._dot(c, c) > 1e-12:
                out.append(M._norm(c))
    return out


def touching_scale(a, b, shield=False):
    """s* = max over the candidate axes of |d.u| / (rA(u) + rB(u)). Both bodies are
    centrally symmetric about the prism centre, so this is closed form rather than a
    bisection. s* >= 1 means the pair is clear as authored."""
    d = M._sub(b[0], a[0])
    if shield:
        ha = tuple(h * CIRCUMSCRIBING_SCALE for h in a[2])
        hb = tuple(h * CIRCUMSCRIBING_SCALE for h in b[2])
        axes = octa_axes((a[0], a[1], ha), (b[0], b[1], hb))
        sup = octa_support
    else:
        ha, hb, axes, sup = a[2], b[2], box_axes(a, b), support
    best = 0.0
    for u in axes:
        den = sup(a[1], ha, u) + sup(b[1], hb, u)
        if den < 1e-12:
            continue
        s = abs(M._dot(d, u)) / den
        if s > best:
            best = s
    return best


def near_pairs(boxes, reach, scale=1.0):
    """Pairs whose bounding spheres meet, via a uniform hash grid keyed on `reach`.

    `scale` inflates the radii the acceptance test uses — the armoured pass has to find the
    pairs whose OCTAHEDRA meet, and testing the bare boxes' spheres there returns only pairs
    that were already touching, which reports 100% fused whatever the fit is."""
    cell = max(reach, 1e-6)
    grid = {}
    for i, b in enumerate(boxes):
        k = tuple(int(math.floor(b[0][a] / cell)) for a in range(3))
        grid.setdefault(k, []).append(i)
    seen = set()
    for k, members in grid.items():
        neigh = []
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    neigh.extend(grid.get((k[0] + dx, k[1] + dy, k[2] + dz), ()))
        for i in members:
            for j in neigh:
                if j <= i:
                    continue
                if (i, j) in seen:
                    continue
                seen.add((i, j))
                d = M._sub(boxes[j][0], boxes[i][0])
                ri = math.sqrt(sum(h * h for h in boxes[i][2]))
                rj = math.sqrt(sum(h * h for h in boxes[j][2]))
                if M._dot(d, d) <= ((ri + rj) * scale) ** 2:
                    yield i, j


def median(values):
    v = sorted(values)
    n = len(v)
    if n == 0:
        return float("nan")
    return v[n // 2] if n % 2 else 0.5 * (v[n // 2 - 1] + v[n // 2])


def area_band(theta):
    """The equal-AREA latitude band, pole to pole — the same binning the coverage row
    uses, so "populates N of 8 bands" means one thing everywhere in this file."""
    return min(7, int((1 - math.cos(theta)) / 2 * 8))


def transported_roll(f0, up0, f1, up1):
    """Degrees of roll of `up` about the ribbon between two consecutive prisms, with the
    BEND taken out: rotate up0 by the minimal rotation carrying f0 onto f1 (parallel
    transport), then measure what is left against up1. Without the transport a curve that
    merely turns reads as a curve that rolls."""
    ax = M._cross(f0, f1)
    s, c = M._len(ax), M._dot(f0, f1)
    if s < 1e-12:
        moved = up0 if c > 0 else M._mul(up0, -1.0)
    else:
        k = M._mul(ax, 1.0 / s)
        ang = math.atan2(s, c)
        ca, sa = math.cos(ang), math.sin(ang)
        moved = M._add(M._add(M._mul(up0, ca), M._mul(M._cross(k, up0), sa)),
                       M._mul(k, M._dot(k, up0) * (1.0 - ca)))
    d = max(-1.0, min(1.0, M._dot(M._norm(moved), M._norm(up1))))
    return math.degrees(math.acos(d))


# ── the plant the game lays ────────────────────────────────────────────────────

_GROWN = {}


CANDIDATE_FACTOR = 4   # MandelbulbFlora.AddressCandidateFactor


def grow_detail(species, element, budget=None):
    """The whole growth record, cached: the surface, the rule, the RAW walk, the laid
    plant, and the map from a laid prism back to its index in the walk.

    The raw walk is kept because two Fall gates are statements about what the walk EMITTED
    that cannot be recovered from the laid plant — whether a dive ran out of steps rather
    than arriving, and which saddle a curve left (the claim filter can eat a curve's first
    prisms, and the first RAW prism of a curve is half a step from its seed)."""
    budget = budget or M.budget_for(species)
    key = (species, element, budget)
    if key in _GROWN:
        return _GROWN[key]
    degree, tables = M.load_tables()
    surface = M.surface_for(element, tables=tables, degree=degree, width=M.FIELD_WIDTH)
    rules = M.rules_for(element, species)
    raw, raw_curves, dives_spent = M.grow(surface, rules, 12345, budget * CANDIDATE_FACTOR)
    shell = M.shell_for(element)
    centres = [M._mul(M.pose(surface, p)[0], shell) for p in raw]
    # AFTER THE CLAIM, BEFORE THE BUDGET, kept separately: the two answer different
    # questions and conflating them reads a budget cut as a broken curve (see
    # gasket_report's ring integrity, which is the gate that found this out the hard way).
    claimed = M.claim_filter(raw, centres, shell=shell)
    kept = claimed[:budget]
    # A gasket species' seed population is its DISC set and the Fall's owed set strides over
    # the LEVEL-0 discs alone, so `build_seeds` — which reads SeedCount, authored 0 here —
    # answers 1 and every Fall arrival figure is measured against a denominator of one.
    gasket = None
    if rules.gasket_levels != 0:
        discs, lay, rho_ref = M.build_gasket(surface, rules)
        gasket = {"discs": discs, "lay": lay, "rho_ref": rho_ref,
                  "samples": M.ring_samples_for(surface, rules, rho_ref),
                  "peaks": len(M.peaks(surface))}
        seeds = [i for i in lay if discs[i].level == 0]
    else:
        seeds = M.build_seeds(surface, rules)
    _GROWN[key] = {
        "surface": surface,
        "rules": rules,
        "raw": raw,
        "raw_curves": raw_curves,
        "dives_spent": dives_spent,
        "claimed": claimed,
        "kept": kept,
        "gasket": gasket,
        "curves": len({p.curve for p in kept}),
        "seeds": seeds,
        "walk_index": {id(p): i for i, p in enumerate(raw)},
        "candidate_cap": budget * CANDIDATE_FACTOR,
    }
    return _GROWN[key]


def skeleton_report(species, element, budget=None):
    """THE PLANT AS ONE OBJECT (the /flora skill §2, Docs/ECOSYSTEM.md §50.6).

    The growth law has two testable properties and this measures both, on the RAW walk —
    before the claim — because they are properties of the growth RULE and the claim is a
    per-plant accident on top of it:

      (b) every prism hangs off something that already exists, i.e. `parent[i] < i`;
      (a) the plant is ONE connected object at every tick, which follows from (b) plus
          "exactly one prism has no parent" and is asserted directly anyway, by union-find
          over the whole set, because the law is worth stating rather than deriving.

    And one thing neither of those sees, which is what actually separates this branch from
    what it replaced: **a bond has to be a BOND**. Give every curve the heart as its parent
    with no connector and both properties above still hold — the plant is a formally
    connected star whose limbs each span most of the bulb. So the third row is the bond
    LENGTH, in units of the prism it lands on: a limb spans one walk step, one ring chord or
    one lane hop, and nothing else."""
    d = grow_detail(species, element, budget)
    surface, raw = d["surface"], d["raw"]
    shell = M.shell_for(element)
    centres = [M._mul(M.pose(surface, p)[0], shell) for p in raw]
    forward = [-1] * 0
    roots, forward_edges = [], []
    for i, p in enumerate(raw):
        if p.parent < 0:
            roots.append(i)
        elif p.parent >= i:
            forward_edges.append((i, p.parent))

    # Union-find over {heart} + every prism. The heart is index -1, kept as `len(raw)`.
    n = len(raw)
    up = list(range(n + 1))

    def find(a):
        while up[a] != a:
            up[a] = up[up[a]]
            a = up[a]
        return a

    for i, p in enumerate(raw):
        a, b = find(i), find(p.parent if p.parent >= 0 else n)
        if a != b:
            up[a] = b
    components = len({find(i) for i in range(n + 1)})

    # Bond lengths, priced in the prism they land on. A prism's own length is the walk step
    # (or the ring chord), so a bond of one prism-length is a limb between neighbours and a
    # bond of fifty is a limb across the plant.
    # The unit is the species' own STRIDE, not the prism's length. A connector segment is a
    # walk step or a ring chord, and the widest legitimate bond is a lane HOP, so those three
    # are what a limb is measured in. Pricing in the prism instead punishes CHARGE for being
    # DASHED (§51: its plates are shorter than the step that spaces them), which is a fact
    # about the leaf and nothing to do with how far a limb reaches.
    rules = d["rules"]
    unit = max(1e-6, max(rules.step,
                         rules.walk_step if rules.walk_step > 0 else rules.step,
                         rules.lane_gap) * M.shell_for(element))
    bonds = []
    for i, p in enumerate(raw):
        anchor_pt = centres[p.parent] if p.parent >= 0 else (0.0, 0.0, 0.0)
        bonds.append(M._len(M._sub(centres[i], anchor_pt)) / unit)
    bonds.sort()
    return {
        "prisms": n,
        "roots": len(roots),
        "forward": len(forward_edges),
        "components": components,
        "bond_median": bonds[len(bonds) // 2] if bonds else 0.0,
        "bond_p99": bonds[min(len(bonds) - 1, int(0.99 * len(bonds)))] if bonds else 0.0,
        "bond_max": bonds[-1] if bonds else 0.0,
    }


# A limb may span a HOP — the gap between one lane and the next, which is what makes the
# plant a cage rather than a mat — so the bound is stated in the species' own STRIDE (see
# skeleton_report). MEASURED across all four species and all four elements at their shipped
# rules: the worst legitimate bond is 4.74 strides (the Coral Bloom's Charge, whose lane gap
# is its widest), so 8 is a 1.7x margin.
#
# The row is not decoration — it is what caught a gasket ring stemming from its parent disc's
# CENTRE while the prism it hangs off sits on that disc's RIM, which measured 15.2 strides
# and which connectivity alone structurally cannot see: a star of trunks straight out of the
# heart is formally ONE component too, and is exactly the shape this branch replaced.
BOND_MAX_STRIDES = 8.0


def skeleton_gates(species, element, s):
    bad = []
    if s["forward"]:
        bad.append(f"{species}/{element}: {s['forward']} prisms hang off a LATER prism — the "
                   f"growth order is not a growth order (the /flora skill §2)")
    if s["roots"] != 1:
        bad.append(f"{species}/{element}: the plant has {s['roots']} prisms with no parent — "
                   f"a plant grows out of ONE crystal")
    if s["components"] != 1:
        bad.append(f"{species}/{element}: the plant is {s['components']} disconnected objects, "
                   f"not one (union-find over every prism plus the heart)")
    if s["bond_max"] > BOND_MAX_STRIDES:
        bad.append(f"{species}/{element}: a limb spans {s['bond_max']:.1f} strides "
                   f"(bound {BOND_MAX_STRIDES:.1f}) — that is not a bond, it is a wire "
                   f"across the plant. Connectivity alone cannot see this: a star of trunks "
                   f"out of the heart is formally connected too")
    return bad


def grow_element(element, budget=None, species="FractalFoliage"):
    """The plant the game LAYS: the walk, then the claim (MandelbulbFlora.Claim), then the
    budget. Measuring the raw walk would describe candidates rather than prisms.

    The two species share the surface FAMILY (one bake per element) and differ in their
    curve rules, so the surface is keyed on the element alone and the walk on both."""
    d = grow_detail(species, element, budget)
    return d["surface"], d["rules"], d["kept"], d["curves"]


def seam_pairs(species, element, budget=None):
    """A CLOSED curve's own closure seam, as (i, j) pairs of indices into the LAID plant.

    The chain exclusion below is `same curve and |i - j| == 1` — the correct exclusion for a
    WALK, where consecutive prisms are laid end to end and clear by construction. A gasket
    species' curve is a RING, and `ring_points` closes it (the first point repeated), so its
    LAST prism ends exactly where its FIRST begins: they are chain neighbours at an index
    difference of N-1, and every ring's seam was being measured as an ordinary crossing pair.
    Measured, the four Apollonia elements: 5 / 46 / 93 / 50 seam pairs reach the near-pair
    set, and taking them out moves the worst pair on two of the four (Mass 0.717 -> 0.749,
    Time 0.650 -> 0.709) while leaving Charge's and Space's binding pair — a genuine tangency
    between two different rings — exactly where it was. So it was eating margin on every
    element without yet changing a verdict.

    A ring's seam is identified in the RAW walk rather than by an index arithmetic on the
    laid plant: `emit` lays exactly `samples` prisms per complete ring, so the seam is that
    ring's first and last EMITTED prism, and a ring the budget truncated is an ARC with no
    seam at all. Doing it by `|i - j| == laid count - 1` instead would mis-fire on a ring
    whose FIRST prism the claim ate, where the surviving ends are not neighbours."""
    d = grow_detail(species, element, budget)
    if d["gasket"] is None:
        return frozenset()               # a walk is OPEN: it has no seam to exclude
    samples = d["gasket"]["samples"]
    emitted = {}
    for wi, p in enumerate(d["raw"]):
        if p.tan_r == 0.0:
            emitted.setdefault(p.curve, []).append(wi)
    walk = d["walk_index"]
    kept_of_walk = {walk[id(p)]: i for i, p in enumerate(d["kept"])}
    out = set()
    for ws in emitted.values():
        if len(ws) != samples:
            continue
        a, b = kept_of_walk.get(ws[0]), kept_of_walk.get(ws[-1])
        if a is not None and b is not None and a != b:
            out.add((min(a, b), max(a, b)))
    return frozenset(out)


def element_report(element, shell=None, cross=None, budget=None, species="FractalFoliage"):
    shell = shell or M.shell_for(element)
    cross = cross or M.cross_section_for(element, species)
    surface, rules, prisms, curves = grow_element(element, budget, species)
    boxes = [obb(surface, p, shell, cross) for p in prisms]
    seams = seam_pairs(species, element, budget)

    vols = [8 * b[2][0] * b[2][1] * b[2][2] for b in boxes]
    dims = [d * 2 for b in boxes for d in b[2]]
    reach = 2 * max(math.sqrt(sum(h * h for h in b[2])) for b in boxes)

    # Consecutive prisms of one CURVE are a chain — they are laid end to end by
    # construction and a curve that bends brings their boxes together, exactly as a
    # lattice species' bonded neighbours touch. They are measured separately: the
    # interpenetration BOUND is about ribbons that cross, which is the thing a growth rule
    # can get wrong.
    pairs = []
    worst_pair = (1e9, -1, -1)
    chain_worst = 1e9
    seams_seen = 0
    for i, j in near_pairs(boxes, reach):
        same = prisms[i].curve == prisms[j].curve
        if same and (i, j) in seams:
            # The closure seam of a CLOSED ring — the same chain pair as `|i - j| == 1`,
            # reached the long way round. See seam_pairs.
            seams_seen += 1
            chain_worst = min(chain_worst, touching_scale(boxes[i], boxes[j]))
            continue
        if same and abs(i - j) == 1:
            chain_worst = min(chain_worst, touching_scale(boxes[i], boxes[j]))
            continue
        pairs.append((i, j))
        # WHICH pair is the worst, and whether a CONNECTOR is in it — the only way to tell a
        # ribbon-vs-ribbon crossing (which the bound is about) from a limb passing through one.
        _s = touching_scale(boxes[i], boxes[j])
        if _s < worst_pair[0]:
            worst_pair = (_s, i, j)
    inter = deep = 0
    worst = 1e9
    for i, j in pairs:
        s = touching_scale(boxes[i], boxes[j])
        if s < 1.0:
            inter += 1
        if s < 0.5:
            deep += 1
        worst = min(worst, s)

    # The same statistic with the chain kept, which is what the armour has to be compared
    # against (see shield_report).
    all_touching = all_inter = 0
    for i, j in near_pairs(boxes, reach):
        all_touching += 1
        if touching_scale(boxes[i], boxes[j]) < 1.0:
            all_inter += 1

    bins = [0] * 8
    for p in prisms:
        bins[area_band(p.theta)] += 1

    # Silhouette: how much of its own bounding sphere the plant's prisms cover, bare and
    # armoured. The ratio of the two is a fact about the shield, not about the fit.
    area = sum(2 * (b[2][0] * b[2][1] + b[2][1] * b[2][2] + b[2][2] * b[2][0]) * 4
               for b in boxes)

    return {
        "element": element,
        "species": species,
        # The plant's own BOUNDING radius - what "Space increases the bounding volume of
        # the assembly" is measured against (Docs/ECOSYSTEM.md §51).
        "radius": max(math.sqrt(sum(c * c for c in b[0])) for b in boxes),
        "prisms": len(prisms),
        "curves": curves,
        "volume": sum(vols),
        "prism_volume": (min(vols), sum(vols) / len(vols), max(vols)),
        "dims": (min(dims), max(dims)),
        "size_span": max(vols) / max(min(vols), 1e-9),
        "coverage": bins,
        "pairs": len(pairs),
        "interpenetrating": inter,
        "interpenetrating_fraction": inter / max(1, len(pairs)),
        "deep": deep,
        "deep_fraction": deep / max(1, len(pairs)),
        "all_pairs": all_touching,
        "all_fraction": all_inter / max(1, all_touching),
        "worst_scale": worst,
        "worst_pair_link": (prisms[worst_pair[1]].link, prisms[worst_pair[2]].link)
                           if worst_pair[1] >= 0 else (False, False),
        "chain_worst": chain_worst,
        "seam_pairs": len(seams),
        "seam_pairs_excluded": seams_seen,
        # The SET, not just its size, because `core_boxes` has to make the same chain/bundle
        # split this function just made and a second derivation of "is this pair a chain
        # pair" is a second answer to one question. Measured today: no seam lands inside any
        # species' core (it is 100% dive prisms), so this is a latent disagreement rather
        # than a live one — which is exactly when it is cheap to close.
        "seams": seams,
        "prisms_list": prisms,
        "area": area,
        "boxes": boxes,
    }


# ── THE FALL ───────────────────────────────────────────────────────────────────

def authors_fall(rules):
    """The gate on the gates: MandelbulbSurface.Growth runs no dive code at all unless
    both of these are set, so a species without them would be measured at zero and
    reported as passing."""
    return rules.dive_step > 0 and rules.dive_count > 0


def dive_owed_bands(species, element, budget=None):
    """How many equal-area theta bands the OWED dive set's release points can occupy at all.

    Mirrors `M.grow`'s gasket stride verbatim — the level-0 discs in rho-descending LAY order,
    the owed index `top[(d * len(top)) // quota]` — so the answer is the set of rings the
    walk will actually offer a dive to, not a re-derivation of it. 0 means "not a gasket, so
    this cap does not apply"; see the note at `dive_band_bound`.

    THE BAND IS THE RING'S RELEASE POINT, NOT THE DISC'S CENTRE, and the two are not the same
    band. `try_dive` releases from `pts[-1]`, the ring's closing point, which sits a whole rho
    (up to ~0.5 rad on a level-0 disc) from the axis the disc is named by. Measured on
    Apollonia, centre against release: Charge 4 vs 3, Mass 4 vs 4, Space 3 vs 3, Time 7 vs 7 —
    and the RELEASE count equals the laid dives' own start bands on all four. So the centre is
    a proxy that over-states by a band on one element of four, and it over-states it in the
    one direction that matters: it would leave Charge asking for a 4th band its rings can
    never release into, which is the "have MORE dives" reading of this gate that the cap
    exists to remove (measured: Charge reaches 4 centre-bands only by raising DiveCount from 6
    to all 13 level-0 discs).

    The stated cost of the cap, which is the item's own construction: on a gasket where every
    owed dive is laid FROM the ring it was owed on, the clause is then satisfied by
    construction. What it still catches is a dive owed and not laid, or laid from somewhere
    other than the ring that owed it — and on the three WALKING species the cap does not
    apply at all, so DIVE_BAND_MIN stands there unchanged."""
    d = grow_detail(species, element, budget)
    g, rules = d["gasket"], d["rules"]
    if g is None or rules.dive_step <= 0:
        return 0
    discs, lay = g["discs"], g["lay"]
    top = [i for i in lay if discs[i].level == 0]
    quota = min(max(0, rules.dive_count), len(top))
    if quota <= 0:
        return 0
    owed = {top[(k * len(top)) // quota] for k in range(quota)}
    shrink = rules.ring_shrink if rules.ring_shrink > 0 else 1.0
    bands = set()
    for i in owed:
        pts = M.ring_points(d["surface"], discs[i].axis, discs[i].rho * shrink,
                            g["samples"], rules.ring_flatten)
        bands.add(area_band(M._spherical(pts[-1])[0]))
    return len(bands)


def radial_bound_for(rules):
    """THE SUNBURST BOUND IS PER GROWTH RULE, because on a GRADIENT-FLOW species this
    statistic is not a property of the plant's design at all.

    The measure splits exactly (measured, `total == surface x (1 - share)` to the digit):

      * THE DIVE CAN NEVER BE RADIAL, by construction rather than by tuning. A dive heading
        is EXACTLY psi off the inward radial (`t = -rhat cos psi + u sin psi`, and the
        axis-alignment blend turns `u` inside the tangent plane, so it cannot move the radial
        component), every species authors psi in 50..64 deg, and |cos psi| <= 0.64 < 0.707.
        Measured 0.0% on every element of every species. So the dive can only ever DILUTE.
      * THE SURFACE TERM IS THEREFORE THE WHOLE STATISTIC — and a Watershed curve is a
        SEPARATRIX, which IS a gradient flow line, so its surface term is a CENSUS OF THE
        BAKE'S OWN STEEPNESS. Measured: Watershed surface radial 27.9 / 41.6 / 68.7 / 75.1%
        for Space / Mass / Charge / Time — the two that broke the 55% bound are exactly the
        two whose bakes are steepest, and the plant is faithfully tracing them.

    So 55% on a skeleton species separates FIELD TYPES rather than good plants from bad, and
    the reachable floor (`surface x (1 - DIVE_SHARE_BAND ceiling)`) is 51.5% for Charge and
    56.3% for Time — Time is unreachable at ANY legal dive share, and Charge is reachable
    only by inflating dive prisms to dilute a surface census, which is tuning around a gate.
    The bound is 0.80 there: a genuine spoke-burst still reads >= 0.95, so the gate keeps its
    teeth against the failure it was written for.

    On a WALKING species the bound stays 0.55 and it is a CLIFF rather than a slope, which is
    the thing to know before tuning toward it: a swirled walk either can climb a terrace riser
    or cannot, and the power-12 bulb's risers are steeper than the 45 deg this measure tests,
    so FractalFoliage/Time measures 60.6% at swirl 0 and 1.9% at swirl 15. Every value on the
    passing side looks identical to the gate and very different on screen — choose the swirl
    on the render and use this only to confirm which side of the switch it landed on."""
    return RADIAL_FRACTION_MAX_SKELETON if authors_skeleton(rules) else RADIAL_FRACTION_MAX


def fall_report(species, element, report):
    """Every number THE FALL is gated on, measured on the LAID plant.

    Dive prisms separate on `TanR != 0` — the stamp `Emit` puts on a prism whose heading is
    absolute rather than tangential — so this is a partition of the plant, never a guess at
    which prisms were the dive."""
    d = grow_detail(species, element)
    rules, kept, boxes = d["rules"], d["kept"], report["boxes"]
    walk = d["walk_index"]
    shell = M.shell_for(element)
    stop_world = rules.dive_stop * shell
    twist = abs(M.SPECIES[species]["twist"])

    # The DIVE is a curve's free-space SUFFIX. The trunk's rise is a free-space PREFIX and
    # is emphatically not a dive: it runs from the heart OUT, so counted as one it reads as
    # the spiral that never arrived (M.free_space_runs).
    rise_i, dive_i = M.free_space_runs(kept)
    free = set(rise_i) | set(dive_i)
    surf_i = [i for i in range(len(kept)) if i not in free]
    out = {
        # The population the owed set STRIDES over, which is not the same object on every
        # species: a walking species strides its seed list, a gasket its LEVEL-0 discs in
        # rho-descending LAY order (`grow`). Naming it is not cosmetic — `build_seeds` reads
        # SeedCount, which a gasket authors 0, so a denominator taken from there is 1 and
        # every arrival figure below reads 600%.
        "owed_from": ("the LEVEL-0 discs in rho-descending lay order"
                      if rules.gasket_levels != 0 else "a z-monotone seed list"),
        "requested": min(rules.dive_count, len(d["seeds"])),
        "spent": d["dives_spent"],
        "seeds": len(d["seeds"]),
        "dive_prisms": len(dive_i),
        "share": len(dive_i) / max(1, len(kept)),
        "stop_world": stop_world,
    }
    by_curve = {}
    for i in dive_i:
        by_curve.setdefault(kept[i].curve, []).append(i)
    for lst in by_curve.values():
        lst.sort(key=lambda i: walk[id(kept[i])])
    out["laid"] = len(by_curve)

    # (b) the crystal's clear air. The dive loop tests `r <= stop` at the TOP, so the last
    # appended point is BELOW the stop sphere by up to one step — which is why the gate is
    # on the laid prism's TIP and not on DiveStopRadius * shell.
    out["tip_min"] = min((M._len(boxes[i][0]) - kept[i].length * shell * 0.5
                          for i in dive_i), default=float("nan"))
    out["centre_min"] = min(M._len(b[0]) for b in boxes)

    # (b2) AMPUTATION — where each dive actually ENDS, which is a different question from
    # (b) and is a real hole in this file's coverage rather than a re-statement of one.
    #
    # `centre_min` is a MINIMUM over the whole plant, so ONE dive that arrives satisfies it
    # for all of them. A dive whose remaining prisms the claim filter refused simply STOPS,
    # far outside the stop sphere, and nothing above can see it: the HOLE gate measures the
    # gap between CONSECUTIVE LAID prisms and an amputated dive has none (it ends), the
    # TRUNCATION gate tests `emitted >= dive_max_steps` where `emitted` counts steps
    # ATTEMPTED rather than laid (so it reads 0), and ARRIVAL counts the dive as laid because
    # it laid something. Measured, it is not hypothetical: CoralBloom/Space has a dive that
    # lays 5 prisms and ends at r = 84.9 u against a 3.4 u stop sphere — an arc trailing off
    # into space that every other Fall bound calls healthy.
    #
    # The bound is DIVE_END_STOP_FACTOR x the stop radius rather than the stop radius itself
    # because the dive loop tests `r <= stop` at the TOP, so the last point is legitimately
    # below the stop sphere by up to one step and the prism's own length sits outside it.
    ends = []
    for lst in by_curve.values():
        ends.append(M._len(boxes[lst[-1]][0]))
    out["dive_end_max"] = max(ends, default=0.0)
    out["dive_end_bound"] = DIVE_END_STOP_FACTOR * stop_world
    out["dive_ends_over"] = sum(1 for r in ends if r > out["dive_end_bound"])
    out["dive_ends"] = sorted(ends, reverse=True)
    # ARRIVAL counts only the dives that reached the braid: an amputated dive laid something,
    # and "laid something" is not the promise the Fall makes.
    out["arrived"] = len(by_curve) - out["dive_ends_over"]
    out["arrival"] = out["arrived"] / max(1, out["requested"])

    # (c) the stride ceiling, self-calibrated: the dive's own prisms against the plant's.
    dive_len = [kept[i].length * shell for i in dive_i]
    surf_len = [kept[i].length * shell for i in surf_i]
    out["dive_len_max"] = max(dive_len, default=0.0)
    out["surf_len_max"] = max(surf_len, default=0.0)
    out["surf_len_median"] = median(surf_len) if surf_len else float("nan")
    out["hole_bound"] = DIVE_HOLE_FACTOR * out["surf_len_median"]

    # (d) the largest HOLE the claim filter left in a dive. A dive is emitted end to end,
    # so every hole here is a prism the claim refused — and a spiral with a hole in the
    # middle still winds the full angle, which is why the winding gate cannot see this.
    holes = []
    for lst in by_curve.values():
        for a, b in zip(lst, lst[1:]):
            gap = (M._len(M._sub(boxes[b][0], boxes[a][0]))
                   - kept[a].length * shell * 0.5 - kept[b].length * shell * 0.5)
            holes.append(gap)
    out["hole_max"] = max(holes, default=0.0)

    # (e) winding PER E-FOLD of radius, not total: r_release is emergent (it is wherever
    # the surface run died), so a species whose curves die low would be penalised on total
    # winding for something that is not the dive's shape. A log spiral winds a constant
    # angle per e-fold by definition, so this is the quantity the mechanism actually sets.
    winds, per_efold, short = [], [], 0
    for lst in by_curve.values():
        if len(lst) < 2:
            short += 1
            continue
        total = 0.0
        for a, b in zip(lst, lst[1:]):
            pa, pb = boxes[a][0], boxes[b][0]
            step = math.atan2(pb[1], pb[0]) - math.atan2(pa[1], pa[0])
            while step > math.pi:
                step -= 2 * math.pi
            while step < -math.pi:
                step += 2 * math.pi
            total += abs(step)
        release = M._len(boxes[lst[0]][0])
        efolds = math.log(max(release, stop_world * 1.0001) / max(stop_world, 1e-9))
        winds.append(math.degrees(total))
        per_efold.append(math.degrees(total) / max(efolds, 1e-9))
    out["winding_median"] = median(winds) if winds else float("nan")
    out["winding_per_efold_median"] = median(per_efold) if per_efold else float("nan")
    out["single_prism_dives"] = short

    # (f/g) truncation. A dive that stopped because it ran out of STEPS is a truncated
    # dive: it never reached the heart and the spiral just ends in mid-air. The laid plant
    # cannot say how many prisms were emitted (the claim eats some), so the emitted count
    # comes from the walk.
    emitted = {}
    for i in M.free_space_runs(d["raw"])[1]:
        c = d["raw"][i].curve
        emitted[c] = emitted.get(c, 0) + 1
    out["emitted_max"] = max(emitted.values(), default=0)
    out["dive_max_steps"] = rules.dive_max_steps
    truncated = 0
    for c, lst in by_curve.items():
        inner = min(M._len(boxes[i][0]) for i in lst)
        if inner > 1.5 * stop_world and emitted.get(c, 0) >= rules.dive_max_steps:
            truncated += 1
    out["truncated"] = truncated

    # (h) THE SUNBURST GATE, over the WHOLE plant — and it is almost entirely a statement
    # about the SURFACE family, which is worth knowing before reading a failure. A dive
    # heading is EXACTLY psi off the inward radial (`t = -rhat cos psi + u sin psi`, and
    # the axis-alignment blend turns `u` inside the tangent plane, so it cannot move the
    # radial component), and every species authors psi in 50..64 deg, where
    # |cos psi| <= 0.64 < RADIAL_COS. So a dive prism can NEVER register as radial here —
    # measured 0.0% on all twelve — and a plant that reads as spokes reads that way because
    # its surface curves are fall lines. The split is reported beside the gate for exactly
    # that reason.
    def radial_fraction(indices):
        hit = 0
        for i in indices:
            c = boxes[i][0]
            m = M._len(c)
            if m < 1e-9:
                continue
            if abs(M._dot(M._mul(c, 1.0 / m), boxes[i][1][2])) > RADIAL_COS:
                hit += 1
        return hit / max(1, len(indices))

    out["radial"] = radial_fraction(range(len(kept)))
    out["radial_surface"] = radial_fraction(surf_i)
    out["radial_dive"] = radial_fraction(dive_i)
    out["radial_bound"] = radial_bound_for(rules)
    out["skeleton"] = authors_skeleton(rules)

    # (i) the strided-not-prefix control. The seed list is z-monotone, so a dive set taken
    # as a PREFIX of it is a polar cap; `Growth.EnsureSeeds` strides instead, and this is
    # what proves the stride survived the claim filter.
    #
    # ONE ENTRY PER DIVE, not per dive PRISM. A dive spirals in to the origin, so its own
    # prisms sweep 3-4 of the eight equal-area bands on their own (measured over all twelve
    # shipped plants) — which means a per-prism count reaches the bound off a SINGLE dive
    # and is structurally blind to the polar cap this gate exists to catch. Measured, the
    # two disagree on three of twelve: CoralBloom/Space reads 7 bands per prism against 3
    # dives' worth, and Watershed/Time reads 4 against 2. The band of a dive is the band of
    # its OUTERMOST laid prism, which is where it left the surface. The per-prism histogram
    # is kept and REPORTED, because it is the one that says where the dive MASS ended up.
    bands = [0] * 8
    for i in dive_i:
        bands[area_band(kept[i].theta)] += 1
    out["dive_bands"] = bands
    out["dive_bands_filled"] = sum(1 for b in bands if b > 0)
    out["dive_start_bands"] = len({area_band(kept[lst[0]].theta) for lst in by_curve.values()})
    # A plant cannot occupy more bands than it laid dives, so the bound is capped by the
    # arrival — otherwise a plant that fails the ARRIVAL gate fails this one too, for the
    # same reason, and the second failure carries no information.
    #
    # ...and on a GASKET it is capped a third time, by the bands the OWED SET CAN REACH. The
    # owed set STRIDES the level-0 discs in rho-descending lay order, so which bands are
    # available to it is a function of `DiveCount` against those thirteen discs and not a
    # free choice: measured on Apollonia, the owed set's own release points occupy 4 / 4 / 3
    # / 7 bands on Charge / Mass / Space / Time. Against a flat bound of 4 the gate therefore
    # stops saying "spread your dives out" and starts saying "have MORE dives" — a different
    # claim, and one the concept (thirteen rings crowning thirteen lobes) has already
    # answered. It cost a real reduction: a Space `dive_count` cut wanted for the converging
    # bundle was reverted purely because it dropped the span from 4 bands to 3.
    #
    # It is scoped to the gasket deliberately. There, a ring IS its disc, so the disc's own
    # axis is where its dive leaves the surface and the owed set's occupancy is knowable up
    # front. On a walking species WHERE a dive leaves is emergent — the run decides when it
    # is released — so the seed's position is not the release point and there is nothing to
    # cap the bound with.
    out["dive_bands_occupiable"] = dive_owed_bands(species, element)
    out["dive_band_bound"] = min(DIVE_BAND_MIN, out["laid"],
                                 out["dive_bands_occupiable"] or DIVE_BAND_MIN)

    # (j) the body roll, and the conditioning of the frame it is measured in. `Pose` hangs
    # a dive prism's `up` off the RAY rather than off the surface normal, which is
    # ill-posed exactly where the heading is radial — so the roll is only meaningful while
    # |sin angle(ray, forward)| stays off zero, and both are reported.
    #
    # The roll is measured NET OF THE AUTHORED TWIST: Fractal Foliage rolls every prism
    # 12 deg/step about its own tangent BY DESIGN, so the raw figure measures the concept
    # rather than a defect (Docs/ECOSYSTEM.md §52 — a gate written against one species is
    # a gate calibrated on one species). A species with no twist is unaffected.
    raw_roll, residual = [], []
    for lst in by_curve.values():
        for a, b in zip(lst, lst[1:]):
            if walk[id(kept[b])] - walk[id(kept[a])] != 1:
                continue        # not walk-adjacent: the claim took the prism between them
            r = transported_roll(boxes[a][1][2], boxes[a][1][1],
                                 boxes[b][1][2], boxes[b][1][1])
            raw_roll.append(r)
            residual.append(abs(r - twist))
    out["roll_pairs"] = len(raw_roll)
    out["roll_raw_median"] = median(raw_roll) if raw_roll else 0.0
    out["roll_raw_max"] = max(raw_roll, default=0.0)
    out["roll_median"] = median(residual) if residual else 0.0
    out["roll_max"] = max(residual, default=0.0)
    out["twist"] = twist
    out["conditioning_min"] = min(
        (M._len(M._cross(M._norm(boxes[i][0]), boxes[i][1][2])) for i in dive_i),
        default=float("nan"))
    return out


def fall_gates(species, element, f, heart_half):
    """Every Fall bound, as (ok, message) — the message states the measured number and the
    bound on every line, passing or failing, so a reader never has to go and look one up."""
    bad = []
    tag = f"{species}/{element}"
    if f["arrival"] < DIVE_ARRIVAL_MIN:
        bad.append(f"{tag} FALL arrival: {f['arrived']} dives ARRIVED of {f['requested']} requested "
                   f"= {f['arrival']:.0%} (bound {DIVE_ARRIVAL_MIN:.0%}; {f['laid']} laid, "
                   f"{f['dive_ends_over']} of them amputated by the claim filter outside "
                   f"{f['dive_end_bound']:.1f} u, the worst ending at {f['dive_end_max']:.1f} u) "
                   f"— the mechanism is authored and most of it is not reaching the heart")
    if not (f["tip_min"] >= heart_half + DIVE_TIP_MARGIN):
        bad.append(f"{tag} FALL clearance: the nearest dive TIP is {f['tip_min']:.3f} u from "
                   f"the centre (bound {heart_half + DIVE_TIP_MARGIN:.3f} = heart half-extent "
                   f"{heart_half:.3f} + {DIVE_TIP_MARGIN} margin) — a prism is inside the crystal")
    lo, hi = DIVE_CENTRE_BAND
    if not (lo <= f["centre_min"] <= hi):
        bad.append(f"{tag} FALL reach: the innermost laid prism CENTRE is {f['centre_min']:.3f} u "
                   f"(band [{lo}, {hi}]) — a thread that REACHES the crystal is a different, "
                   f"worse object than one that almost does, and one that stops short is invisible")
    if f["dive_len_max"] > f["surf_len_max"]:
        bad.append(f"{tag} FALL stride: the longest dive prism is {f['dive_len_max']:.3f} u "
                   f"against the plant's longest surface prism {f['surf_len_max']:.3f} u "
                   f"(bound: no longer) — DiveStrideCeiling {f_stride(species, element):.2f} is "
                   f"letting the spiral lay a spike")
    if f["hole_max"] > f["hole_bound"]:
        bad.append(f"{tag} FALL hole: the claim left a {f['hole_max']:.3f} u gap inside a dive "
                   f"(bound {f['hole_bound']:.3f} = {DIVE_HOLE_FACTOR}x the median surface prism "
                   f"{f['surf_len_median']:.3f}) — a spiral with a hole in it still winds the "
                   f"full angle, so the winding gate cannot see this")
    if not (f["winding_per_efold_median"] >= DIVE_WINDING_PER_EFOLD):
        bad.append(f"{tag} FALL winding: {f['winding_per_efold_median']:.1f} deg per e-fold of "
                   f"radius, median over dives (bound {DIVE_WINDING_PER_EFOLD:.0f}) — that is a "
                   f"spoke with a bend in it, not a spiral")
    lo, hi = DIVE_SHARE_BAND
    if not (lo <= f["share"] <= hi):
        bad.append(f"{tag} FALL share: dive prisms are {f['share']:.2%} of the plant "
                   f"(band [{lo:.0%}, {hi:.0%}]) — below the floor the promise is not visible, "
                   f"above the ceiling the plant is a sunburst")
    if f["truncated"] > 0:
        bad.append(f"{tag} FALL truncation: {f['truncated']} dive(s) ran out of steps "
                   f"(DiveMaxSteps {f['dive_max_steps']}, longest emitted {f['emitted_max']}) "
                   f"instead of arriving on the stop sphere (bound 0)")
    if f["radial"] >= f["radial_bound"]:
        kind = ("a GRADIENT-FLOW species, so the surface term is a census of the BAKE's own "
                "steepness" if f["skeleton"] else
                "a WALKING species, where this bound is a CLIFF and not a slope — a swirl "
                "either clears the terrace risers or does not, so choose it on the render")
        bad.append(f"{tag} SUNBURST: {f['radial']:.1%} of the plant points within 45 deg of the "
                   f"ray to the heart (bound {f['radial_bound']:.0%} — {kind}; surface "
                   f"{f['radial_surface']:.1%}, dive {f['radial_dive']:.1%} = 0 BY "
                   f"CONSTRUCTION, since |cos psi| <= 0.64 < {RADIAL_COS} for every authored "
                   f"dive angle, so the dive can only ever DILUTE: total = surface x "
                   f"(1 - share {f['share']:.1%}) = {f['radial_surface'] * (1 - f['share']):.1%})")
    # AMPUTATION is REPORTED (the dive ENDS line) and counted against ARRIVAL above, never
    # gated twice: a dive the claim filter ended outside the braid is a dive that did not arrive.
    if f["dive_start_bands"] < f["dive_band_bound"]:
        bad.append(f"{tag} FALL spread: the {f['laid']} laid dives LEAVE THE SURFACE in "
                   f"{f['dive_start_bands']}/8 equal-area theta bands (bound "
                   f"{f['dive_band_bound']} = min({DIVE_BAND_MIN}, dives laid {f['laid']}"
                   + (f", bands the OWED SET's rings RELEASE into {f['dive_bands_occupiable']}"
                      if f["dive_bands_occupiable"] else "") + f"); their prisms "
                   f"then sweep {f['dive_bands_filled']}/8, which is why the per-prism count "
                   f"cannot see this) — the owed set strides {f['owed_from']}, and on "
                   f"this plant that stride is landing in a cap rather than over the bulb")
    if f["roll_median"] > DIVE_ROLL_MEDIAN_MAX:
        bad.append(f"{tag} FALL roll: the ribbon rolls a median {f['roll_median']:.2f} deg per "
                   f"step net of the authored twist {f['twist']:.0f} (bound "
                   f"{DIVE_ROLL_MEDIAN_MAX:.0f}) — `up` is hung off the ray and is drifting")
    if f["roll_max"] > DIVE_ROLL_PAIR_MAX:
        bad.append(f"{tag} FALL roll: one pair rolls {f['roll_max']:.2f} deg net of twist "
                   f"(bound {DIVE_ROLL_PAIR_MAX:.0f}) — that is a face flip")
    if not (f["conditioning_min"] >= DIVE_CONDITIONING_MIN):
        bad.append(f"{tag} FALL conditioning: |sin angle(ray, forward)| falls to "
                   f"{f['conditioning_min']:.3f} (bound {DIVE_CONDITIONING_MIN}) — `up` is "
                   f"hung off the ray, so at a radial heading the frame has no answer")
    return bad


def f_stride(species, element):
    return grow_detail(species, element)["rules"].dive_stride_ceiling


# ── THE WATERSHED ──────────────────────────────────────────────────────────────

# Nine authored columns a skeleton species leaves at a do-nothing value, each given two
# wildly different values. An authored value that cannot affect anything must never be
# mistakable for one that can — and the per-field breakdown is the point, because "the
# output changed" does not say WHICH column is still wired.
INERT_PROBES = {
    "field": (4, 5), "swirl": (40.0, -37.0), "field_mix": (0.13, 0.77),
    "momentum": (0.61, 0.29), "hop_seek": (3.0, 0.9), "hop_jitter": (0.8, 0.4),
    "seeds": (7, 133), "seed_spread": (3.0, 47.0), "lane_gap": (0.9, 0.2),
}


def authors_skeleton(rules):
    return rules.skeleton_seeds != 0


def seed_spread_bound(prefix):
    """(bound, uniform expectation) — the SEED SPREAD bound as a function of its OWN sample.

    The bound was a constant 6 while the sample is a species-dependent FRACTION of the saddle
    count: the prefix is 25% of the saddles, which is 8 points on Mass, 11 on Space, 21 on
    Charge and 37 on Time. Eight uniform draws into eight equal-area bands fill an expected
    `8(1 - (7/8)^8) = 5.25` of them, so a flat 6 was asking the SMALLEST sample in the fleet
    for BETTER THAN UNIFORM coverage — and the gate duly failed exactly where its own sample
    was smallest while the ordering it tests was working hard (Mass measured 4 against a
    sharpness-major control of 1, i.e. 4x the control).

    So the bound is the uniform expectation DISCOUNTED, capped at the old constant — 4 / 4 /
    6 / 6 for Mass / Space / Charge / Time. Farthest-point ordering should beat a random
    draw, not be held to a number a random draw usually misses.

    The gate is not weakened, because the bound is a PAIR: the count must also be at least
    SEED_SPREAD_CONTROL_FACTOR x the sharpness-major NEGATIVE CONTROL's count. That second
    clause is the one that actually proves the property, and it needs no distribution
    assumption at all — a genuinely un-spread ordering measures at or near its own control by
    construction, and the control is re-measured per element rather than remembered."""
    n = AREA_BANDS
    uniform = n * (1.0 - ((n - 1.0) / n) ** prefix)
    return min(SEED_SPREAD_BANDS, int(math.floor(SEED_SPREAD_DISCOUNT * uniform))), uniform


def _prism_tuple(p):
    return (p.theta, p.phi, p.radial, p.dive, p.tan_a, p.tan_b, p.tan_r,
            p.length, p.girth, p.roll, p.curve, p.lane)


def _lay(surface, rules, budget, shell=None):
    shell = shell or M.SHELL_RADIUS
    raw, _, _ = M.grow(surface, rules, 12345, budget * CANDIDATE_FACTOR)
    centres = [M._mul(M.pose(surface, p)[0], shell) for p in raw]
    return [_prism_tuple(p) for p in M.claim_filter(raw, centres, shell=shell)[:budget]]


def census_resolution():
    """The chord below which the critical-point census cannot tell two points apart, read
    out of the model's own dedupe rather than typed here (`DEDUPE_DOT = 1 - chord^2 / 2`)."""
    return math.sqrt(max(0.0, 2.0 * (1.0 - M.DEDUPE_DOT)))


def ring_gap_for(gaps):
    """The grouping threshold, DERIVED per element from that element's own gap histogram.

    Returns (threshold, ratio, lo, hi, derived). A hand-chosen angle was deciding the answer
    and saying so in its own failure text: Space's peaks sit `gap kept <= 0.0603 | split >=
    0.3470` about a 0.08 threshold, so 0.08 is INSIDE the "same ring" population rather than
    in the empty band between the two, and the element read as [4,4,4,4] (modal 4) where the
    surface's own two-fold fold says [2,2,4,4,2,2] (modal 2). One absolute angle cannot serve
    four bakes whose peak counts run 16..72.

    THE METHOD: sort the consecutive theta gaps, find the largest RATIO between neighbouring
    sorted gaps — the empty band in the histogram — and take its GEOMETRIC MIDPOINT.

    THE ONE REPAIR, and it is necessary rather than a taste: every gap is first FLOORED at
    the census's own resolution. A ring's peaks sit at the SAME latitude by symmetry, so
    their gaps are EXACTLY 0.0 in double (measured: 18 of Charge's 41, 12 of Mass's 15, 10 of
    Space's 15, 32 of Time's 71) — and a ratio against zero is infinite while a geometric
    mean against zero is zero, so the unfloored ladder picks the bottom of the noise on all
    four and a threshold of ~0 splits every peak into its own ring. The floor is not a tuning
    constant: a theta gap below the chord at which `find_critical_points` DEDUPES is not a
    measured separation at all, so it cannot be a ring boundary. Floored, the largest ratio
    lands on the real empty band on every element — Charge 18.5x at (0.0100, 0.1854),
    Mass 24.1x at (0.0100, 0.2414), Space 6.0x at (0.0100, 0.0603), Time 10.8x at (0.0100,
    0.1078) — giving 0.0431 / 0.0491 / 0.0246 / 0.0328 and the modal sizes 7 / 4 / 2 / 11,
    which are order-1 on all four.

    Note what the floor also buys: Mass and Space have NO positive within-ring gaps at all,
    so their smallest positive gap is already a ring BOUNDARY and a ladder over the positive
    gaps alone would put the threshold above it (0.62 and 0.14 — both still modal 4). The
    floor is what supplies the lower edge of that first band."""
    floor = census_resolution()
    g = sorted(max(x, floor) for x in gaps)
    if len(g) < 3:
        return RING_GAP, float("nan"), float("nan"), float("nan"), False
    best, bi = 0.0, -1
    for i in range(len(g) - 1):
        r = g[i + 1] / max(g[i], 1e-12)
        if r > best:
            best, bi = r, i
    if best <= RING_GAP_RATIO_MIN:
        return RING_GAP, best, float("nan"), float("nan"), False
    return math.sqrt(g[bi] * g[bi + 1]), best, g[bi], g[bi + 1], True


def ring_census(surface, element):
    """The census the species exists for: the surface's PEAKS, grouped into latitude rings.

    The fold is `order - 1` because rotating the Julia constant by delta is a symmetry of
    v -> v^n + c only when delta*(n-1) is a whole turn. Two independent statements of the
    same claim are measured, because the grouping one has a threshold in it and the
    spectral one does not:

      * the MODAL ring size against `order - 1`, with the gaps either side of the grouping
        threshold reported so a knife-edge is visible rather than inferred. It is not
        hypothetical: at gap 0.06 Space groups as [2,2,4,2,4,2,2] and at 0.08 as [4,4,4,4],
        so the threshold decides the answer;
      * the azimuthal POWER SPECTRUM of the whole peak set, which needs no grouping at all
        and is therefore the statement that cannot be moved by a tolerance.
    """
    order = BULB_ORDER[element]
    peaks = [c for c in M.critical_points(surface) if c.kind == M.PEAK]
    pits = [c for c in M.critical_points(surface) if c.kind == M.PIT]
    sads = M.saddles(surface)
    out = {"order": order, "want": order - 1, "peaks": len(peaks),
           "saddles": len(sads), "pits": len(pits),
           "euler": len(peaks) - len(sads) + len(pits)}
    if not peaks:
        out.update(sizes=[], modal=0, phases=[], spectrum=[], spectrum_peak=0,
                   gap_split_min=float("nan"), gap_kept_max=float("nan"),
                   ring_gap=RING_GAP, ring_gap_ratio=float("nan"),
                   ring_gap_band=(float("nan"), float("nan")), ring_gap_derived=False)
        return out

    ordered = sorted(peaks, key=lambda c: c.theta)
    all_gaps = [b.theta - a.theta for a, b in zip(ordered, ordered[1:])]
    threshold, ratio, lo, hi, derived = ring_gap_for(all_gaps)
    out["ring_gap"] = threshold
    out["ring_gap_ratio"] = ratio
    out["ring_gap_band"] = (lo, hi)
    out["ring_gap_derived"] = derived
    out["ring_gap_floor"] = census_resolution()
    rings, current, split, kept_gaps = [], [ordered[0]], [], []
    for a, b in zip(ordered, ordered[1:]):
        gap = b.theta - a.theta
        if gap > threshold:
            split.append(gap)
            rings.append(current)
            current = [b]
        else:
            kept_gaps.append(gap)
            current.append(b)
    rings.append(current)
    sizes = [len(r) for r in rings]
    # Ties go to the LARGER ring, so the modal size is a function of the data rather than
    # of dictionary order.
    out["modal"] = max(set(sizes), key=lambda s: (sizes.count(s), s))
    out["sizes"] = sizes
    out["gap_split_min"] = min(split) if split else float("nan")
    out["gap_kept_max"] = max(kept_gaps) if kept_gaps else float("nan")

    # Each ring's phase, as a fraction of ONE LOBE of its own fold: a ring rotated half a
    # lobe from its neighbour reads 0, 0.5, 0, 0.5. Reported, never gated — it alternates
    # on Charge, Mass and Time and does not on Space, which is a fact about the surface.
    phases = []
    for r in rings:
        q = max(1, len(r))
        sx = sum(math.cos(q * c.phi) for c in r)
        sy = sum(math.sin(q * c.phi) for c in r)
        phases.append(round((math.atan2(sy, sx) % (2 * math.pi)) / (2 * math.pi), 3))
    out["phases"] = phases

    spectrum = []
    for m in range(1, 2 * order + 1):
        sx = sum(math.cos(m * c.phi) for c in peaks)
        sy = sum(math.sin(m * c.phi) for c in peaks)
        spectrum.append(math.hypot(sx, sy) / len(peaks))
    out["spectrum"] = spectrum
    out["spectrum_peak"] = max(range(len(spectrum)), key=lambda i: spectrum[i]) + 1
    out["spectrum_value"] = max(spectrum)
    return out


def watershed_report(species, element, report, inert=True):
    """Every number THE WATERSHED is gated on, measured on the LAID plant."""
    d = grow_detail(species, element)
    rules, kept, boxes = d["rules"], d["kept"], report["boxes"]
    walk, surface = d["walk_index"], d["surface"]
    shell = M.shell_for(element)
    sads = M.saddles(surface)
    out = ring_census(surface, element)
    out["walk_step"] = rules.walk_step if rules.walk_step > 0 else rules.step
    out["length_factor"] = rules.length_factor
    out["candidates"] = len(d["raw"])
    out["candidate_cap"] = d["candidate_cap"]
    out["lanes_authored"] = max(1, rules.lanes)

    # Surface prisms per curve, in WALK order — the arm proper. A dive is a tail on an arm,
    # not part of the net, so it is excluded from every net statistic below.
    # `p.link` drops the CONNECTOR — the trunk's rise or the stem that carried growth to
    # this saddle. Those prisms are laid inside the curve's batch and are not part of the
    # arm: left in, the first one is at the PARENT saddle and every question below ("where
    # did this arm start", "which saddle does it belong to") is answered about the limb.
    arm = {}
    for i, p in enumerate(kept):
        if p.tan_r == 0.0 and not p.link:
            arm.setdefault(p.curve, []).append(i)
    for lst in arm.values():
        lst.sort(key=lambda i: walk[id(kept[i])])

    # (a) SEPARATRIX SIGN. Lanes 1 and 3 leave along the saddle's RIDGE eigen-direction and
    # must end HIGHER; 0 and 2 leave along the VALLEY and must end LOWER.
    #
    # WHAT IT PROVES, stated narrowly, because an earlier version of this comment claimed it
    # catches "a flipped eigenvector selection" and two negative controls say otherwise.
    # Swapping every saddle's e_ridge/e_valley, and separately inverting the fall-line sign,
    # each lay a plant of ZERO prisms (measured, all four Watershed elements): the seed
    # tangent then disagrees with the field by 180 deg, `Trace`'s max-turn gate closes on
    # step 2, every run dies under MinRun, and this gate reports a clean 0 of 0. It can only
    # ever see a mismatch between the LANE PARITY `grow` walks and the lane parity this file
    # reads — a two-copies-of-one-convention check, which is worth its zero cost and is not
    # a check on the geometry. What actually holds the geometry is that `field_mix` is 1.0,
    # so the walk IS the gradient flow; if that column is ever authored below 1 the gate
    # starts carrying real weight.
    violations, single = [], 0
    for c, lst in arm.items():
        if len(lst) < 2:
            single += 1
            continue
        r0 = M._len(boxes[lst[0]][0]) / shell
        r1 = M._len(boxes[lst[-1]][0]) / shell
        ascend = (kept[lst[0]].lane & 1) == 1
        if (r1 > r0) != ascend:
            violations.append((c, kept[lst[0]].lane, round(r0, 4), round(r1, 4)))
    out["sign_violations"] = len(violations)
    out["sign_curves"] = len(arm)
    out["sign_single"] = single
    out["sign_examples"] = violations[:3]

    # (b) THE NET SURVIVES. Which saddle a curve left cannot be read off the laid plant
    # (the claim can eat a curve's first prisms, and the emitted order skips dusted arms),
    # so a curve is attributed by its FIRST RAW prism, which sits half a walk step from its
    # own seed. The worst attribution angle is reported so the reader can see that it is
    # not close: saddles are ~0.3 rad apart and the worst attribution is ~2 deg.
    first_raw = {}
    for p in d["raw"]:
        if p.link:
            continue
        if p.curve not in first_raw:
            first_raw[p.curve] = p
    owner, worst_attr = {}, 0.0
    for c, p in first_raw.items():
        pos = M._norm(M.pose(surface, p)[0])
        best, best_dot = -1, -2.0
        for si, s in enumerate(sads):
            v = M._dot(pos, s.dir)
            if v > best_dot:
                best_dot, best = v, si
        owner.setdefault(best, []).append(c)
        worst_attr = max(worst_attr, math.degrees(math.acos(max(-1.0, min(1.0, best_dot)))))
    keeps, lengths = 0, []
    for si in range(len(sads)):
        live = [c for c in owner.get(si, ()) if len(arm.get(c, ())) > 0]
        if len(live) >= NET_ARMS:
            keeps += 1
        lengths += [len(arm[c]) for c in live]
    out["saddle_count"] = len(sads)
    out["saddles_keeping_arms"] = keeps
    out["saddle_survival"] = keeps / max(1, len(sads))
    out["mean_arm"] = sum(lengths) / max(1, len(lengths))
    out["live_arms"] = len(lengths)
    out["attribution_worst_deg"] = worst_attr

    # (c) no lane starved by the budget. The lanes are INTERLEAVED valley+, ridge+,
    # valley-, ridge-, so a budget that runs out inside lane 2 lays the falls and none of
    # the rises — which is the half that draws the silhouette.
    #
    # Over the NET, not over the plant: connector prisms are stamped with the lane of the
    # curve they fed, and a seed's first appearance is almost always lane 0, so counting
    # them puts every stem in the plant into one lane and drives the other three under a
    # bound that is about the net being EVEN. (Measured on the Watershed's Mass at the
    # shipped budget: 14.9 / 14.1 / 12.0 with them in, against a 15% floor.)
    lane_count, net = {}, 0
    for p in kept:
        if p.link:
            continue
        net += 1
        lane_count[p.lane] = lane_count.get(p.lane, 0) + 1
    out["lane_share"] = {k: lane_count.get(k, 0) / max(1, net)
                         for k in range(out["lanes_authored"])}

    # (e) SEED SPREAD, and its negative control. Farthest-point ordering makes "every
    # prefix is spatially spread" true by construction; sharpness-major, which the design
    # shipped before it was measured, puts a plant's strongest quarter into 2 of 8 bands.
    take = max(1, int(math.ceil(SEED_SPREAD_PREFIX * len(sads))))
    out["prefix"] = take
    out["spread_bands"] = len({area_band(c.theta) for c in sads[:take]})
    control = sorted(sads, key=lambda c: (-c.sharpness, c.theta, c.phi))
    out["spread_bands_control"] = len({area_band(c.theta) for c in control[:take]})
    out["spread_bound"], out["spread_uniform"] = seed_spread_bound(take)
    out["spread_control_bound"] = SEED_SPREAD_CONTROL_FACTOR * out["spread_bands_control"]

    # (i) THE PEAKS ARE ON THE PLANT. A ridge separatrix runs uphill to a peak, so a peak
    # with two or more ridge-arm ends on it is a node of the net that a player can see.
    # A FIRST CUT at 50%: the true bar is a look judgement and the tolerance is one and a
    # half walk steps, which is tight for an arm the claim filter has truncated.
    ends = [M._mul(boxes[lst[-1]][0], 1.0 / shell)
            for c, lst in arm.items() if (kept[lst[0]].lane & 1) == 1]
    tol = PEAK_TOUCH_STEPS * out["walk_step"]
    peaks = [c for c in M.critical_points(surface) if c.kind == M.PEAK]
    on = 0
    for c in peaks:
        pp = M.build_frame(surface, c.theta, c.phi).position
        if sum(1 for q in ends if M._len(M._sub(q, pp)) <= tol) >= 2:
            on += 1
    out["peaks_on_plant"] = on / max(1, len(peaks))
    out["peaks_on_plant_n"] = on
    out["ridge_ends"] = len(ends)
    out["peak_tolerance"] = tol

    # (f) the inert columns, per field. Expensive (two full walks per column) and it is
    # what the 33 s `--check` mostly spends; `inert=False` is the opt-out, and nothing in
    # this file takes it today — "six of these nine authored numbers do nothing and three
    # of them do" is a fact a reader needs BEFORE they try tuning one, so the plain report
    # pays for it too.
    if inert:
        base = [_prism_tuple(p) for p in kept]
        read, drift = [], {}
        for name, (a, b) in INERT_PROBES.items():
            idx = M.Rules.FIELDS.index(name)
            worst = 0
            for value in (a, b):
                values = rules.as_list()
                values[idx] = value
                got = _lay(surface, M.Rules(*values), M.budget_for(species), M.shell_for(element))
                if got != base:
                    n = abs(len(got) - len(base)) + sum(1 for x, y in zip(got, base) if x != y)
                    worst = max(worst, n)
            if worst:
                read.append(name)
                drift[name] = worst
        out["inert_read"] = read
        out["inert_drift"] = drift
        out["inert_tested"] = list(INERT_PROBES)
    return out


def watershed_gates(species, element, w):
    bad = []
    tag = f"{species}/{element}"
    if w["sign_violations"]:
        bad.append(f"{tag} SEPARATRIX SIGN: {w['sign_violations']} of {w['sign_curves']} laid "
                   f"arms end on the wrong side of their saddle (bound 0) — a ridge arm must "
                   f"end ABOVE its saddle and a valley arm BELOW; e.g. {w['sign_examples']}")
    if w["saddle_survival"] < NET_SADDLE_SURVIVAL:
        bad.append(f"{tag} NET: {w['saddles_keeping_arms']}/{w['saddle_count']} saddles keep "
                   f">= {NET_ARMS} laid arms = {w['saddle_survival']:.1%} "
                   f"(bound {NET_SADDLE_SURVIVAL:.0%}) — the net has become a starburst")
    if w["mean_arm"] < NET_MEAN_ARM:
        bad.append(f"{tag} NET: the mean laid arm is {w['mean_arm']:.2f} surface prisms "
                   f"(bound {NET_MEAN_ARM}) — the X junctions are there and the edges are not")
    for lane, share in sorted(w["lane_share"].items()):
        if share < LANE_SHARE_MIN:
            bad.append(f"{tag} LANE {lane} holds {share:.1%} of the plant "
                       f"(bound {LANE_SHARE_MIN:.0%}) — the budget ran out inside a lane, so "
                       f"one of the four separatrix families is missing from the net")
    if w["modal"] != w["want"]:
        how = (f"DERIVED {w['ring_gap']:.4f} rad = the geometric midpoint of this element's "
               f"own widest empty band ({w['ring_gap_band'][0]:.4f}..{w['ring_gap_band'][1]:.4f}, "
               f"a {w['ring_gap_ratio']:.1f}x step, gaps floored at the census's "
               f"{w['ring_gap_floor']:.4f} dedupe chord)" if w["ring_gap_derived"] else
               f"the {RING_GAP} rad FALLBACK — no band in this element's gap histogram "
               f"exceeds {RING_GAP_RATIO_MIN}x, so there is no empty band to derive from")
        bad.append(f"{tag} RING CENSUS: the modal peak-ring size is {w['modal']}, not "
                   f"order-1 = {w['want']} (rings {w['sizes']}, grouped at {how}; the two gap "
                   f"populations it separates: kept up to {w['gap_kept_max']:.4f}, split from "
                   f"{w['gap_split_min']:.4f}) — the plant is drawing the wrong surface. The "
                   f"threshold is no longer a hand-chosen angle that could be deciding this")
    want = w["want"]
    if want > 0 and w["spectrum_peak"] % want != 0:
        bad.append(f"{tag} RING CENSUS: the azimuthal power spectrum of the peak set peaks at "
                   f"m={w['spectrum_peak']} ({w['spectrum_value']:.3f}), which is not a multiple "
                   f"of order-1 = {want} — the (n-1)-fold fold is not in the surface")
    if w["spread_bands"] < w["spread_bound"] or w["spread_bands"] < w["spread_control_bound"]:
        bad.append(f"{tag} SEED SPREAD: the first {w['prefix']} of {w['saddle_count']} saddles "
                   f"occupy {w['spread_bands']}/{AREA_BANDS} equal-area bands — bound "
                   f"max({w['spread_bound']} = min({SEED_SPREAD_BANDS}, "
                   f"{SEED_SPREAD_DISCOUNT:.0%} of the {w['spread_uniform']:.2f} bands "
                   f"{w['prefix']} UNIFORM draws would fill), {w['spread_control_bound']} = "
                   f"{SEED_SPREAD_CONTROL_FACTOR}x the sharpness-major control's "
                   f"{w['spread_bands_control']}/{AREA_BANDS}) — a budget prefix of this order "
                   f"is not spread over the sphere. The bound is a function of the PREFIX "
                   f"because the prefix is a fraction of a per-element saddle count, and the "
                   f"control clause is the half that needs no distribution assumption")
    if w["candidates"] >= w["candidate_cap"]:
        bad.append(f"{tag} LANES: the walk returned {w['candidates']} candidates, which is the "
                   f"cap ({w['candidate_cap']}) — at least one of the four lanes did not "
                   f"complete inside the address budget")
    if any(v == 0 for v in w["lane_share"].values()):
        bad.append(f"{tag} LANES: only {sum(1 for v in w['lane_share'].values() if v)} of "
                   f"{w['lanes_authored']} authored lanes reached the plant at all")
    if element != "Charge":
        lo, hi = LENGTH_FACTOR_MIN, 1.0 / M.CLAIM_FACTOR
        if not (lo <= w["length_factor"] < hi):
            bad.append(f"{tag} LENGTH FACTOR {w['length_factor']:.4f} outside [{lo}, {hi:.4f}) "
                       f"— below it a net is drawn in dashes and reads as dots; at or above "
                       f"1/ClaimFactor a curve's own chain stops clearing by construction")
    if w["peaks_on_plant"] < PEAKS_ON_PLANT_MIN:
        bad.append(f"{tag} PEAKS ON THE PLANT: {w['peaks_on_plant_n']}/{w['peaks']} peaks carry "
                   f">= 2 ridge-arm ends within {w['peak_tolerance']:.4f} "
                   f"= {w['peaks_on_plant']:.1%} (bound {PEAKS_ON_PLANT_MIN:.0%}, a FIRST CUT)")
    if w.get("inert_read"):
        bad.append(f"{tag} INERT COLUMNS: {', '.join(w['inert_read'])} CHANGE the plant "
                   f"(prism-tuple drift {w['inert_drift']}) — a skeleton species authors them "
                   f"at a do-nothing value, but Trace/TryFieldDirection read them whatever the "
                   f"seeds are, so an authored value that looks inert is not")
    return bad


# ── APOLLONIA ──────────────────────────────────────────────────────────────────
#
# The gasket's gates. Four of the five are things NO existing gate in this file can see,
# and the reason is the same each time: every gate above was written against a species
# that draws CURVES, and this one draws a PACKING (Docs/ECOSYSTEM.md §52 — a gate written
# against one species is a gate calibrated on one species). Measured on this species the
# two interpenetration bounds are near-vacuous — non-chain touching pairs are 17/1112/603/658
# against thousands on the ribbon species and deep-interleave is 0.0% on all four — because
# a packing cannot overlap by construction. What does the work here is RING INTEGRITY and
# THE LADDER IN SCREEN TERMS.

FORM_FIT_BAND = (0.90, 1.05)     # after-claim count / PRISM_BUDGET — a BAND, not a ceiling
RING_INTEGRITY_MIN = 0.80        # of its N samples, through the claim, BEFORE the budget
OCTAVE_FRAME_MIN = 0.005         # share of the arena frame one octave must paint.
                                 # RECALIBRATED 1.0% -> 0.5% on the plant that LOOKS right: the
                                 # 1.0% bar landed beside a tuning that had tripled the species'
                                 # cross-width (a pile of plates, reverted on its render - the
                                 # lace scores 3/3/1/2 legible octaves at that bar against the
                                 # plates' 3/3/4/4). On the accepted lace the gasket's SECOND
                                 # generation is a genuinely small number of discs (Space 9, Time
                                 # 6, between much larger neighbours) and its frame share PEAKS
                                 # at 0.68% / 0.87% at ANY disc count (swept disc_min_radius to
                                 # 6x the shipped value); reaching 1.0% needs 1.7x / 1.32x the
                                 # cross, which is the plate again. The PIXEL bar, which is what
                                 # catches the sub-pixel octaves the gate was written for, is
                                 # unchanged. A bound and the data it was measured on, changed in
                                 # separate commits, silently makes the bound a claim about data
                                 # that is gone (Docs/ECOSYSTEM.md §54.5).
OCTAVE_PX_MIN = 2.0              # its median prism's projected LENGTH, pixels
OCTAVE_LEGIBLE_MIN = 3           # octaves that must clear BOTH
TANGENCY_MEDIAN_MAX = 0.05       # a child's worst gap to its three parents, over rho_child
COARSENESS_ORDER = ("Space", "Charge", "Time", "Mass")   # N: <, <=, <
COARSENESS_RATIO_MIN = 1.5       # N_Mass / N_Space
SAMPLES_ROUND_MARGIN = 0.05      # |circ/step - round(circ/step)|: REPORTED, see gasket_report
ARENA_TILE = 700                 # the judging sheet's tile, so "1% of the frame" is ITS frame

# Twenty authored columns a gasket species leaves at a do-nothing value. Same instrument as
# the Watershed's nine probes and the same argument: an authored value that cannot affect
# anything must never be mistakable for one that can, and the per-field breakdown is the
# point, because "the output changed" does not say WHICH column is still wired.
GASKET_INERT_PROBES = {
    "field": (4, 5), "swirl": (40.0, -37.0), "field_mix": (0.13, 0.77),
    "momentum": (0.61, 0.29), "max_steps": (7, 311), "lanes": (2, 9),
    "lane_gap": (0.9, 0.2), "hop_seek": (3.0, 0.9), "hop_jitter": (0.8, 0.4),
    "seeds": (7, 133), "seed_spread": (3.0, 47.0), "max_turn": (11.0, 171.0),
    "r_min": (0.1, 0.9), "r_max": (1.2, 9.0), "min_run": (2, 17),
    "girth_taper": (0.2, 0.9), "girth_reference": (3.0, 41.0),
    "skeleton_seeds": (5, 64), "walk_step": (0.02, 0.9), "min_persistence": (0.05, 0.4),
}


def authors_gasket(rules):
    """The gate on the gates: `Growth` runs no gasket code at all unless this is set, so a
    species without it would be measured at zero and reported as passing."""
    return rules.gasket_levels != 0


# ── the arena frame ────────────────────────────────────────────────────────────
#
# THE LADDER'S GATE IS A SCREEN MEASUREMENT, and that is the whole point of it. Stated in
# RINGS ("three octaves carrying six rings each") it is satisfied by exactly the octaves
# that were already visible, so it cannot see the failure it exists for — measured, a plant
# spending 41% of its prisms on 1.0% of the frame passes the ring-count form of this gate
# with room to spare. A gate satisfied by the visible part cannot detect an invisible part.
#
# ORTHOGRAPHIC, at the SHEET's own arena pixels-per-unit (`mandelbulb_flora_render
# .arena_scale`, read from the renderer rather than retyped so the two cannot drift). The
# render is a perspective view; orthographic is the honest instrument for a per-octave
# statistic because a perspective frame spreads the near half of the plant over more pixels
# than the far half, which would make an octave's share a function of which side of the
# plant its rings happened to land on.

def _basis(axis):
    a = M._norm(axis)
    ref = (0.0, 0.0, 1.0) if abs(a[2]) < 0.9 else (1.0, 0.0, 0.0)
    u = M._norm(M._cross(ref, a))
    return a, u, M._cross(a, u)


def _hull(points):
    """Monotone chain over the 8 projected corners — a box's silhouette is a hexagon, and
    filling the 6 faces' quads instead would paint the same pixels several times over."""
    pts = sorted(set(points))
    if len(pts) < 3:
        return pts

    def half(seq):
        out = []
        for p in seq:
            while len(out) >= 2 and ((out[-1][0] - out[-2][0]) * (p[1] - out[-2][1])
                                     - (out[-1][1] - out[-2][1]) * (p[0] - out[-2][0])) <= 0:
                out.pop()
            out.append(p)
        return out

    lo, hi = half(pts), half(list(reversed(pts)))
    return lo[:-1] + hi[:-1]


def _fill(mask, hull, tile):
    """Scanline-fill a convex polygon by PIXEL CENTRE, and — if it covers none — paint the
    one pixel it sits in. A sub-pixel prism is not nothing on screen (a thousand of them
    read as speckle); scoring it zero would say an octave paints no pixels when it paints
    a dusting of them, which is the opposite of the failure this gate is looking for."""
    ys = [p[1] for p in hull]
    painted = 0
    for y in range(max(0, int(math.floor(min(ys)))), min(tile - 1, int(math.ceil(max(ys)))) + 1):
        yc, xs, n = y + 0.5, [], len(hull)
        for i in range(n):
            ax, ay = hull[i]
            bx, by = hull[(i + 1) % n]
            if (ay <= yc) == (by <= yc):
                continue
            xs.append(ax + (bx - ax) * (yc - ay) / (by - ay))
        if len(xs) < 2:
            continue
        for x in range(max(0, int(math.ceil(min(xs) - 0.5))),
                       min(tile - 1, int(math.floor(max(xs) - 0.5))) + 1):
            o = y * tile + x
            if not mask[o]:
                mask[o] = 1
                painted += 1
    if painted == 0:
        cx = sum(p[0] for p in hull) / len(hull)
        cy = sum(p[1] for p in hull) / len(hull)
        x, y = int(math.floor(cx)), int(math.floor(cy))
        if 0 <= x < tile and 0 <= y < tile and not mask[y * tile + x]:
            mask[y * tile + x] = 1
            painted = 1
    return painted


def arena_octaves(boxes, lanes, axis, tile=ARENA_TILE, shield=False):
    """Per-octave UNION coverage of the arena frame and the median projected prism LENGTH.

    UNION, never a sum of areas: an octave's rings overlap each other on screen and a sum
    would report a dense small octave as covering more of the frame than it can.

    `shield` RASTERISES THE ARMOUR RATHER THAN THE BOX, which is what a CHARGE plant actually
    draws: a Charge plant's leaves are shielded by law (Flora.ResolveShieldPeriod floors every
    Charge plant's shield period) and `PrismStateManager.ActivateShield` engages the
    octahedron CIRCUMSCRIBING the box, reaching CIRCUMSCRIBING_SCALE x the HALF-extents — i.e.
    1.5 x leafSize from the centre on all three axes. Measuring Charge's ladder on the bare
    box measures a plant that never renders. An octahedron's silhouette is the hull of its six
    VERTICES (`centre +- 3h` along each axis), which projects exactly the way the box's eight
    corners do, so this is the same instrument pointed at the body that is on screen.

    The FRAMING stays the caller's `boxes`, deliberately: bare and armoured then share one
    ppu and one extent and the two numbers are directly comparable. A rim prism's octahedron
    can therefore reach past the tile, where `_fill` clips it — so an armoured share is a
    LOWER bound, which is the safe direction for a legibility floor."""
    import mandelbulb_flora_render as R
    ppu, dist, ext = R.arena_scale(boxes, tile)
    a, u, v = _basis(axis)
    reach = CIRCUMSCRIBING_SCALE if shield else 1.0
    half, masks, lengths = tile / 2.0, {}, {}
    for b, lane in zip(boxes, lanes):
        mask = masks.get(lane)
        if mask is None:
            mask = masks[lane] = bytearray(tile * tile)
            lengths[lane] = []
        c, ax, h = b
        corners = []
        if shield:
            for i in range(3):
                for s in (-1, 1):
                    p = M._add(c, M._mul(ax[i], s * reach * h[i]))
                    corners.append((half + ppu * M._dot(p, u), half - ppu * M._dot(p, v)))
        else:
            for sx in (-1, 1):
                for sy in (-1, 1):
                    for sz in (-1, 1):
                        p = M._add(M._add(M._add(c, M._mul(ax[0], sx * h[0])),
                                          M._mul(ax[1], sy * h[1])), M._mul(ax[2], sz * h[2]))
                        corners.append((half + ppu * M._dot(p, u), half - ppu * M._dot(p, v)))
        hull = _hull(corners)
        if len(hull) >= 3:
            _fill(mask, hull, tile)
        # The LENGTH the eye reads is the long axis foreshortened by the view: a prism seen
        # end-on is a dot whatever its length, and calling it long would be the same mistake
        # as summing the areas. Armoured, the body on screen is the octahedron, whose extent
        # along that axis is the same CIRCUMSCRIBING_SCALE multiple of the box's.
        d = M._dot(ax[2], a)
        lengths[lane].append(2 * reach * h[2] * ppu * math.sqrt(max(0.0, 1.0 - d * d)))
    out = {}
    for lane, mask in masks.items():
        out[lane] = {"frame": sum(mask) / float(tile * tile),
                     "px": median(lengths[lane]),
                     "prisms": len(lengths[lane])}
    return out, ppu, dist, ext


# ── the gasket's report ────────────────────────────────────────────────────────

def gasket_report(species, element, report, inert=True):
    """Every number APOLLONIA is gated on, measured on the LAID plant — except the two that
    a laid plant structurally cannot show, both named where they are taken:

      * RING INTEGRITY is measured on the CLAIM-FILTERED list BEFORE budget truncation.
        Conflating the two reads a budget cut as a broken ring — the last ring laid is
        exactly the one the budget truncates, so a plant with 55 perfect rings reports one
        of them at integrity 0.0 if the two are not kept apart.
      * TANGENCY is a statement about the DISC SET (`M.build_gasket`), which is the thing
        `Inscribe` produces. The laid plant can only show the rings that survived, so a
        gasket that had silently degraded to "a smaller circle roughly in the middle" would
        still render as circles and still pass every gate above."""
    import mandelbulb_flora_render as R
    d = grow_detail(species, element)
    rules, kept, claimed, boxes = d["rules"], d["kept"], d["claimed"], report["boxes"]
    g = d["gasket"]
    discs, lay = g["discs"], g["lay"]
    samples, rho_ref = g["samples"], g["rho_ref"]
    out = {"samples": samples, "rho_ref": rho_ref, "discs": len(discs),
           "raw": len(d["raw"]), "claimed": len(claimed), "laid": len(kept),
           "budget": M.budget_for(species),
           "fit": len(claimed) / float(M.budget_for(species)),
           "truncated_prisms": max(0, len(claimed) - len(kept)),
           "peaks": g["peaks"], "disc_seeds": rules.disc_seeds}

    # (a) THE FULL FORM FITS — raw, after-claim and laid, because the three answer different
    # questions: raw says what the rule produced, after-claim what the game keeps, laid what
    # the collider budget pays for. A one-sided ceiling passes an 81% plant in silence while
    # it is priced at 100% of that budget.
    out["want0"] = min(rules.disc_seeds, out["peaks"]) if rules.disc_seeds > 0 else out["peaks"]
    by_level, by_lane = {}, {}
    for disc in discs:
        by_level[disc.level] = by_level.get(disc.level, 0) + 1
        by_lane[disc.lane] = by_lane.get(disc.lane, 0) + 1
    out["by_level"] = [by_level.get(i, 0) for i in range(max(by_level) + 1)]
    out["by_lane"] = [by_lane.get(i, 0) for i in range(max(1, rules.gasket_levels))]
    out["level0"] = by_level.get(0, 0)
    # The LAID half of the same promise, and the only half that can fail. `want0` above
    # re-types `build_gasket`'s OWN `want` expression and `build_gasket` neither adds nor
    # removes a level-0 disc afterwards, so `level0 == want0` is an identity of the model
    # (measured equal for DiscSeeds in {0, -3, 5, 9, 13, 20, 999}) — a gate on it cannot
    # fire against the shipped algorithm. What is NOT an identity is whether every one of
    # those coarsest rings REACHED the plant: the claim filter deletes prisms, and a whole
    # ring swallowed by one already laid is exactly the failure "level 0 is the surface's
    # peaks" is a claim against. Curve ids are handed out one per ring in LAY order
    # (`grow`'s gasket branch increments curve_count immediately before each emit and
    # breaks without incrementing), so curve k+1 IS lay[k] — CHECKED rather than assumed,
    # because a ring the walk never reached would shift every id after it and silently
    # re-label every per-ring statement below.
    level_of_curve = {k + 1: discs[i].level for k, i in enumerate(lay)}
    laid_curves = {p.curve for p in kept if p.tan_r == 0.0}
    out["curve_map_ok"] = all(c in level_of_curve for c in laid_curves)
    out["level0_laid"] = sum(1 for c in laid_curves if level_of_curve.get(c) == 0)

    # rho_ref's LONE-DISC FALLBACK. A level-0 ring is half the angle to its nearest
    # neighbour, and a disc with no neighbour inside pi/3 takes that frozen constant
    # instead — so rho_ref, which sets the girth ladder, the octave ladder AND N, can be a
    # CONSTANT rather than a measurement on the surface. Reported per element for exactly
    # that reason (measured: the largest disc takes it on Space and Time).
    top = [disc for disc in discs if disc.level == 0]
    out["fallback0"] = sum(1 for disc in top if abs(disc.rho - math.pi / 6.0) < 1e-9)
    out["rho_ref_is_fallback"] = abs(rho_ref - math.pi / 6.0) < 1e-9
    out["rho_top"] = (min(disc.rho for disc in top), max(disc.rho for disc in top)) if top else (0, 0)
    out["rho_span"] = (max(disc.rho for disc in discs)
                       / max(1e-9, min(disc.rho for disc in discs))) if discs else 0.0

    # N's ROUNDING MARGIN. `RingSamplesFor` is `round(circ / step)` and N is shared by every
    # ring, so half a ULP either side of a boundary is a DIFFERENT PLANT — two independent
    # transcriptions of this species disagreed by one sample on exactly one element and it
    # moved the ring count 89 -> 78 and the fill 95% -> 81%. Reported, never gated: the
    # honest fix is to author N per element, which is a model change and not a bound.
    circ = 2 * math.pi * math.sin(rho_ref) * d["surface"].mean_radius
    x = circ / max(1e-9, rules.step)
    out["samples_exact"] = x
    out["samples_margin"] = abs(x - round(x))
    out["samples_authored"] = rules.ring_samples > 0
    # `ring_samples_for` CLAMPS into [RING_MIN_SAMPLES, RING_MAX_SAMPLES]. Where the clamp
    # bites, the printed `round(circ/step)` is not N and the margin below describes a
    # boundary nothing is standing near — say so rather than print a number that is not
    # the one the plant used.
    out["samples_clamped"] = (not out["samples_authored"]
                              and samples != min(M.RING_MAX_SAMPLES,
                                                 max(M.RING_MIN_SAMPLES, int(round(x)))))

    # (b) RING INTEGRITY, before the budget. One curve IS one ring; its dive (TanR != 0) is a
    # tail on the ring and is not part of it.
    emitted, survived = {}, {}
    for p in d["raw"]:
        if p.tan_r == 0.0:
            emitted[p.curve] = emitted.get(p.curve, 0) + 1
    for p in claimed:
        if p.tan_r == 0.0:
            survived[p.curve] = survived.get(p.curve, 0) + 1
    integrity = {c: survived.get(c, 0) / float(samples) for c in emitted}
    out["rings"] = len(emitted)
    out["ring_integrity_min"] = min(integrity.values()) if integrity else float("nan")
    out["ring_integrity_median"] = median(list(integrity.values())) if integrity else float("nan")
    out["rings_broken"] = sum(1 for v in integrity.values() if v < RING_INTEGRITY_MIN)
    out["rings_short_emit"] = sum(1 for v in emitted.values() if v != samples)

    # The budget's own cut, which gate (b) is deliberately blind to: a truncated plant stops
    # mid-ring, so the last ring laid is an ARC. It is REPORTED rather than gated because the
    # fix is in the growth rule (refuse to START a ring whose N prisms cannot fit — free,
    # since the lay order is already ring-major), not in a bound this file can set.
    laid_by_curve = {}
    for p in kept:
        if p.tan_r == 0.0:
            laid_by_curve[p.curve] = laid_by_curve.get(p.curve, 0) + 1
    last = kept[-1].curve if kept else None
    out["last_ring_laid"] = laid_by_curve.get(last, 0)
    out["last_ring_survived"] = survived.get(last, 0)
    out["cut_mid_ring"] = out["last_ring_laid"] < out["last_ring_survived"]

    # (c) THE LADDER, in screen terms. Both axes are measured: the gate is on +z, and the
    # SHEET's own arena camera direction is reported beside it so a reader can see the answer
    # does not hinge on which axis was picked.
    lanes = [p.lane for p in kept]
    oct_z, ppu, dist, ext = arena_octaves(boxes, lanes, (0.0, 0.0, 1.0))
    view = R.SHEET_VIEWS[0]
    cd = (math.cos(view[2]) * math.cos(view[1]), math.cos(view[2]) * math.sin(view[1]),
          math.sin(view[2]))
    # The camera cross-check is measured on the SAME BODY as the gate (armoured for Charge),
    # or it stops being a cross-check on the axis and becomes one on the armour as well.
    oct_cam, _, _, _ = arena_octaves(boxes, lanes, cd, shield=(element == "Charge"))
    out["ppu"], out["eye"], out["extent"] = ppu, dist, ext
    out["octaves"] = oct_z
    out["octaves_camera"] = oct_cam
    # CHARGE IS MEASURED ARMOURED, because a Charge plant ships armoured — the ladder is a
    # claim about what is ON SCREEN and the bare box is not what renders. A shield reaches
    # 1.5 x leafSize on all three half-extents, so the silhouette it draws is
    # 0.5 x CIRCUMSCRIBING_SCALE^2 = 4.5x the box's, which is the whole reason a Charge plant
    # is the DENSEST of the four shielded and the sparsest bare (Docs/ECOSYSTEM.md §50). The
    # BARE table is kept and reported beside it: the pair is the inversion made visible, and
    # a bound read against the wrong one of them is a bound on a plant nobody sees.
    out["armoured"] = element == "Charge"
    if out["armoured"]:
        oct_arm, _, _, _ = arena_octaves(boxes, lanes, (0.0, 0.0, 1.0), shield=True)
    else:
        oct_arm = oct_z
    out["octaves_ladder"] = oct_arm
    legible = [k for k, v in oct_arm.items()
               if v["frame"] >= OCTAVE_FRAME_MIN and v["px"] >= OCTAVE_PX_MIN]
    out["legible"] = sorted(legible)
    out["legible_bare"] = sorted(k for k, v in oct_z.items()
                                 if v["frame"] >= OCTAVE_FRAME_MIN and v["px"] >= OCTAVE_PX_MIN)
    out["legible_camera"] = sorted(k for k, v in oct_cam.items()
                                   if v["frame"] >= OCTAVE_FRAME_MIN and v["px"] >= OCTAVE_PX_MIN)
    # A sum of per-OCTAVE unions, so two octaves overlapping on screen are counted twice.
    # Reported only, never gated (the gate is per octave); measured against the shipped
    # renderer's own arena tile it lands within 15% of the lit pixels, the difference being
    # the render's perspective spread and its heart.
    out["frame_total"] = sum(v["frame"] for v in oct_arm.values())
    out["frame_total_bare"] = sum(v["frame"] for v in oct_z.values())
    # Against the LADDER's own table, so "spent below both bars" names the same octaves the
    # gate just failed rather than a second opinion measured on a different body.
    out["invisible_spend"] = sum(v["prisms"] for k, v in oct_arm.items()
                                 if k not in legible) / max(1, len(kept))

    # (d) TANGENCY. A child is inscribed in a curvilinear triangle of three discs that were
    # already there, so its three nearest LOWER-INDEX discs are its parents; its value is the
    # WORST of those three gaps, because tangency to all three is the claim being made.
    gaps = []
    for i, disc in enumerate(discs):
        if disc.level < 1 or i == 0:
            continue
        near = sorted((M._angle(disc.axis, discs[j].axis) - disc.rho - discs[j].rho)
                      / max(disc.rho, 1e-9) for j in range(i))
        if len(near) >= 3:
            gaps.append(max(near[:3]))
    out["children"] = len(gaps)
    out["tangency_median"] = median(gaps) if gaps else 0.0
    out["tangency_max"] = max(gaps, default=0.0)

    # (e) THE ORDERING PROMISE. The lay order is rho-DESCENDING and the lane IS the size
    # octave, so a budget-stopped plant has laid every ring larger than some rho and no ring
    # smaller — the whole packing minus its finest generation. That is the property the whole
    # lane-major contract rests on here, and it is one equality: the LAST prism laid belongs
    # to the highest octave present.
    out["last_lane"] = kept[-1].lane if kept else -1
    out["max_lane"] = max(lanes) if lanes else -1
    # ...and the half of that promise which is not a theorem. The equality IS one: `lane`
    # is floor(log2(rho_ref / rho) / octave) clamped, a monotone function of -rho, and the
    # lay order is rho-descending, so the lane sequence of every emitted prism is
    # non-decreasing and its last element is its maximum BY CONSTRUCTION (measured: true in
    # `raw` and in `kept`, on all four elements). It is kept as a model-regression guard and
    # is labelled as one. A HOLE in the ladder is not a theorem — the octave bands are fixed
    # widths in log rho and nothing makes the disc set occupy every one of them, so a plant
    # can reach octave 3 with nothing at all in octave 2, which THE LADDER above scores as
    # "illegible" and structurally cannot tell apart from "absent".
    out["lanes_present"] = sorted({p.lane for p in kept})
    out["lanes_contiguous"] = out["lanes_present"] == list(range(len(out["lanes_present"])))
    out["lane_prisms"] = {k: v["prisms"] for k, v in sorted(oct_z.items())}
    dives = {}
    for i in M.free_space_runs(kept)[1]:
        lane = kept[i].lane
        dives[lane] = dives.get(lane, 0) + 1
    out["lane_dives"] = {k: dives.get(k, 0) for k in sorted(oct_z)}

    # (f) the inert columns, per field — the same instrument as the Watershed's.
    if inert:
        base = [_prism_tuple(p) for p in kept]
        read, drift = [], {}
        for name, (a, b) in GASKET_INERT_PROBES.items():
            idx = M.Rules.FIELDS.index(name)
            worst = 0
            for value in (a, b):
                values = rules.as_list()
                values[idx] = value
                got = _lay(d["surface"], M.Rules(*values), M.budget_for(species), M.shell_for(element))
                if got != base:
                    worst = max(worst, abs(len(got) - len(base))
                                + sum(1 for p, q in zip(got, base) if p != q))
            if worst:
                read.append(name)
                drift[name] = worst
        out["inert_read"] = read
        out["inert_drift"] = drift
        out["inert_tested"] = list(GASKET_INERT_PROBES)
    return out


def gasket_gates(species, element, g):
    bad = []
    tag = f"{species}/{element}"
    lo, hi = FORM_FIT_BAND
    if not (lo <= g["fit"] <= hi):
        bad.append(f"{tag} FULL FORM: {g['claimed']} prisms survive the claim against a "
                   f"{g['budget']} budget = {g['fit']:.0%} (band [{lo:.0%}, {hi:.0%}]; raw "
                   f"{g['raw']}, laid {g['laid']}) — below the floor the plant is priced at "
                   f"{g['budget']} colliders and lays a fraction of them, above the ceiling "
                   f"the budget is cutting the form; DiscMinRadius is the dial")
    if g["rings_broken"] > 0:
        bad.append(f"{tag} RING INTEGRITY: {g['rings_broken']} of {g['rings']} rings keep "
                   f"under {RING_INTEGRITY_MIN:.0%} of their {g['samples']} samples through "
                   f"the claim (worst {g['ring_integrity_min']:.1%}, median "
                   f"{g['ring_integrity_median']:.1%}) — a ring that loses half its prisms is "
                   f"an ARC, and this species' whole claim is that the repeated unit is a "
                   f"CLOSED ring. Measured before the budget, so a budget cut cannot cause it")
    if len(g["legible"]) < OCTAVE_LEGIBLE_MIN:
        shares = ", ".join(f"oct{k} {v['frame']:.2%}/{v['px']:.1f}px"
                           for k, v in sorted(g["octaves_ladder"].items()))
        if g["armoured"]:
            shares += ("  [ARMOURED, which is what a Charge plant draws; bare: "
                       + ", ".join(f"oct{k} {v['frame']:.2%}/{v['px']:.1f}px"
                                   for k, v in sorted(g["octaves"].items())) + "]")
        bad.append(f"{tag} LADDER: {len(g['legible'])} octaves paint >= "
                   f"{OCTAVE_FRAME_MIN:.1%} of the {ARENA_TILE}px arena frame AND carry a "
                   f"median prism >= {OCTAVE_PX_MIN}px (bound {OCTAVE_LEGIBLE_MIN}) — "
                   f"[{shares}], {g['invisible_spend']:.0%} of the plant's prisms spent below "
                   f"both bars. A ladder stated in RINGS is satisfied by exactly the octaves "
                   f"that were already visible, which is why this one is stated in pixels")
    if g["tangency_median"] > TANGENCY_MEDIAN_MAX:
        bad.append(f"{tag} TANGENCY: the median child disc's worst gap to its three parents "
                   f"is {g['tangency_median']:.4f} of its own rho (bound "
                   f"{TANGENCY_MEDIAN_MAX}; max {g['tangency_max']:.4f} over {g['children']} "
                   f"children) — Inscribe has degraded to 'a smaller circle roughly in the "
                   f"middle', which still renders as circles and stops being a gasket")
    if not g["curve_map_ok"]:
        bad.append(f"{tag} LEVEL 0: a laid ring carries a curve id the lay order does not "
                   f"account for, so no per-ring statement here can be trusted — the gasket "
                   f"branch has stopped emitting exactly one curve per disc, in lay order")
    elif g["level0_laid"] != g["want0"]:
        bad.append(f"{tag} LEVEL 0: {g['level0_laid']} of the coarsest generation's rings "
                   f"reach the LAID plant against min(DiscSeeds {g['disc_seeds']}, peaks "
                   f"{g['peaks']}) = {g['want0']} — {g['level0']} level-0 discs were built, "
                   f"so the claim ate {g['want0'] - g['level0_laid']} whole ring(s). Level 0 "
                   f"is the packing's coarsest ring and the first thing laid; a plant "
                   f"missing one is missing a lobe of the bulb. (The DISC count is measured "
                   f"too and is an identity of the model — it cannot fire.)")
    if not g["lanes_contiguous"]:
        bad.append(f"{tag} LADDER HOLE: the laid plant occupies octaves "
                   f"{g['lanes_present']} — a rung is MISSING rather than merely small, "
                   f"which THE LADDER above scores as illegible and cannot tell apart. The "
                   f"octave bands are fixed widths in log rho, so a gap means the disc set "
                   f"SKIPPED a size rather than drew it too thin, and the two want opposite "
                   f"fixes (DiscMinRadius / GasketOctave, not girth)")
    if g["last_lane"] != g["max_lane"]:
        bad.append(f"{tag} ORDERING: the last prism laid is in octave {g['last_lane']} while "
                   f"the plant reaches octave {g['max_lane']} (bound: equal) — a MODEL "
                   f"REGRESSION guard rather than a measurement of this plant: with the lane "
                   f"a monotone function of -rho and the lay order rho-descending the two "
                   f"are equal by construction, so a difference means the lane has stopped "
                   f"being the size octave and 'the whole bulb drawn thinly' with it")
    if g.get("inert_read"):
        bad.append(f"{tag} INERT COLUMNS: {', '.join(g['inert_read'])} CHANGE the plant "
                   f"(prism-tuple drift {g['inert_drift']}) — a gasket species authors all "
                   f"{len(g['inert_tested'])} of these at a do-nothing value to say that "
                   f"nothing here walks")
    return bad


def gasket_species_gates(species, gs):
    """The one gate that is a statement about the FOUR ELEMENTS rather than about one plant.

    RING COARSENESS is this species' §51 identity, and it is the only place in the family
    where an element's long axis is spent on SAMPLING rather than on a longer prism: the
    ring's chord is set by its own geometry, so what the law's step buys here is how many
    bars go round it. Space draws the coarsest polygon of long blades and Mass the finest
    mosaic of bricks — and if N ever comes out equal across the four, the element has
    stopped being visible in the form."""
    bad = []
    n = {e: g["samples"] for e, g in gs.items()}
    if n and len(n) != len(COARSENESS_ORDER):
        # The one gate here that is a statement about the FOUR, so a partial roster leaves
        # it unable to run — and a gate that cannot run must say so rather than return []
        # (`authors_gasket`'s own argument, one level up: a zero reported as a pass).
        bad.append(f"{species} RING COARSENESS: only {sorted(n)} of "
                   f"{list(COARSENESS_ORDER)} author a gasket, so the cross-element "
                   f"identity cannot be measured at all — this gate returned no verdict "
                   f"rather than a passing one")
    if len(n) == len(COARSENESS_ORDER):
        a, b, c, dd = (n[e] for e in COARSENESS_ORDER)
        if not (a < b <= c < dd):
            rel = (f"{COARSENESS_ORDER[0]} < {COARSENESS_ORDER[1]} <= "
                   f"{COARSENESS_ORDER[2]} < {COARSENESS_ORDER[3]}")
            bad.append(f"{species} RING COARSENESS: N must run {rel} (Space strictly "
                       f"coarsest, Mass strictly finest); measured {n} — an element's "
                       f"long axis is spent "
                       f"here as ring COARSENESS, so equal N is the element not showing up")
        if n["Space"] and n["Mass"] / float(n["Space"]) < COARSENESS_RATIO_MIN:
            bad.append(f"{species} RING COARSENESS: N_Mass / N_Space = "
                       f"{n['Mass'] / float(n['Space']):.2f} (bound "
                       f"{COARSENESS_RATIO_MIN}) — the four are ordered but too close "
                       f"together to read as four different materials")
    return bad


# ── Charge's armour at the core ────────────────────────────────────────────────

SHIELD_SAMPLE = 3000     # octahedron SAT is 88 axes; the estimate is sampled, seed fixed


def sample(pairs, n=SHIELD_SAMPLE, seed=1):
    """A fixed-seed sample. The armoured test is 88 separating axes per pair and a plant has
    tens of thousands of armoured pairs, so the fraction is ESTIMATED — stated rather than
    disguised, and at n=3000 the standard error on a fraction is under 1%."""
    if len(pairs) <= n:
        return pairs
    import random as _r
    return _r.Random(seed).sample(pairs, n)


def armoured_census(boxes, chain=frozenset()):
    """Charge's octahedra against each other over a given set of boxes, SPLIT into the two
    populations the armour has — a curve's own consecutive prisms (`chain`, given as index
    pairs into `boxes`) and everything else (the BUNDLE: ribbons that cross).

    Both are returned because they answer different questions and only one of them is a
    tuning surface. See CORE_ARMOURED_MAX: the chain's touching scale is the closed form
    1/(3 x LengthFactor), which contains no dive parameter and no cross-section, so it is a
    fact about CHARGE_DASH against the 1/3 cliff rather than a number this species can move."""
    out = {"pairs": 0, "inter": 0, "fraction": 0.0,
           "chain_pairs": 0, "chain_inter": 0, "chain_fraction": 0.0,
           "bundle_pairs": 0, "bundle_inter": 0, "bundle_fraction": 0.0}
    if len(boxes) < 2:
        return out
    reach = 2 * CIRCUMSCRIBING_SCALE * max(
        math.sqrt(sum(h * h for h in b[2])) for b in boxes)
    pairs = sample(list(near_pairs(boxes, reach, CIRCUMSCRIBING_SCALE)))
    for i, j in pairs:
        hit = touching_scale(boxes[i], boxes[j], shield=True) < 1.0
        key = "chain" if (i, j) in chain else "bundle"
        out["pairs"] += 1
        out[key + "_pairs"] += 1
        if hit:
            out["inter"] += 1
            out[key + "_inter"] += 1
    for key in ("", "chain_", "bundle_"):
        n = out[(key or "") + "pairs"]
        out[key + "fraction"] = out[key + "inter"] / max(1, n)
    return out


def armoured_fraction(boxes):
    """The WHOLE-PLANT figure, which deliberately keeps the chain — see shield_report."""
    c = armoured_census(boxes)
    return c["inter"], c["pairs"], c["fraction"]


def chain_touching_scale(length_factor):
    """s* for two consecutive ARMOURED prisms of one curve, closed form. They sit one step
    apart, each is `length_factor x step` long, and a shield reaches CIRCUMSCRIBING_SCALE x
    the half-extent — so they first touch at 1 / (3 x length_factor), independent of the
    step, the cross-section and every dive dial.

    IT IS EXACT ON THE POPULATION IT IS PRINTED AGAINST AND ONLY THERE. The core is 100%
    DIVE prisms on all four species, a dive steps at a fixed `dive_step`, and the measured
    core chain worst-pair matches this to four decimal places on every one (1.1111 / 0.7407
    / 1.0700 / 0.7407 for FractalFoliage / CoralBloom / Watershed / Apollonia). The
    WHOLE-PLANT chain is mostly SURFACE, where a traced chord is only nominally one step, so
    there the same comparison is off by 0.3% (Apollonia 0.7377) to 2.2% (FractalFoliage
    1.0864) — the right side of 1 in every case, but not a four-decimal match. Quote it
    beside the core figure, never as a fact about a whole plant."""
    return 1.0 / max(1e-9, 0.5 * CIRCUMSCRIBING_SCALE * 2.0 * length_factor)


def core_boxes(report, fraction=CORE_RADIUS_FRACTION):
    """(boxes, limit, chain) for the prisms inside `fraction` of the plant's own bounding
    radius — where the Fall converges every dive it lays, and therefore the tightest packing
    in the species. The whole-plant armour figure cannot see it: it is 53 boxes of 2800.

    `chain` is the pairs of those boxes that are one curve's consecutive prisms, re-indexed
    into the returned list, so the core census can make the same chain/bundle split the bare
    bar has always made — and it takes the CLOSURE SEAM with it (`element_report`'s `seams`),
    because a closed ring's last prism is chain-adjacent to its first the long way round and
    filing that pair as BUNDLE would put it in the gated population. Measured, the core is
    100% dive prisms on all four species so no seam reaches it today; the point is that the
    two functions now answer "is this a chain pair" the same way by construction."""
    limit = fraction * report["radius"]
    prisms = report["prisms_list"]
    seams = report.get("seams", frozenset())
    idx = [i for i, b in enumerate(report["boxes"]) if M._len(b[0]) <= limit]
    pos = {g: k for k, g in enumerate(idx)}
    chain = set()
    for g in idx:
        h = pos.get(g + 1)
        if h is not None and prisms[g].curve == prisms[g + 1].curve:
            k = pos[g]
            chain.add((min(k, h), max(k, h)))
    for a, b in seams:
        ka, kb = pos.get(a), pos.get(b)
        if ka is not None and kb is not None:
            chain.add((min(ka, kb), max(ka, kb)))
    return [report["boxes"][i] for i in idx], limit, frozenset(chain)


def shield_report(reports, species="FractalFoliage"):
    """Charge armoured against its siblings bare — the bar this species has to clear."""
    out = {}
    charge = reports["Charge"]
    # The armour measurement keeps the CHAIN, unlike the bare one. A shield reaches
    # 1.5 x leafSize, and a prism's leafSize includes its LENGTH, so a ribbon laid end to end
    # fuses into a solid tube along its own curve unless something is done about it — which is
    # exactly the Skein rail's finding (a rail's armour meets its neighbour's and "which rail am
    # I on" loses its answer). Excluding the chain here would measure everything except the one
    # thing that goes wrong. What is done about it is that Charge's ribbon is DASHED
    # (LengthFactor 0.45): its prisms are shorter than the step that spaces them, so the armour
    # has room to close and the dashes are what the octahedra fill in.
    inter, n, frac = armoured_fraction(charge["boxes"])
    out["armoured_pairs"] = n
    out["armoured_interpenetrating"] = inter
    out["armoured_fraction"] = frac
    # And again over the CORE alone, because the Fall converges every dive on one point and
    # a 2% subset cannot move a whole-plant fraction. SPLIT chain/bundle there, unlike the
    # whole-plant figure above: at the core the chain is the MAJORITY of the pairs and it is
    # 100% fused by the closed form, so an un-split fraction is dominated by a number no
    # lever in this file can move. The BUNDLE is what the gate has always described in prose.
    cb, limit, cchain = core_boxes(charge)
    cc = armoured_census(cb, cchain)
    out["core_radius"] = limit
    out["core_prisms"] = len(cb)
    out["core_pairs"] = cc["pairs"]
    out["core_interpenetrating"] = cc["inter"]
    out["core_fraction"] = cc["fraction"]
    out["core_chain_pairs"] = cc["chain_pairs"]
    out["core_chain_interpenetrating"] = cc["chain_inter"]
    out["core_chain_fraction"] = cc["chain_fraction"]
    out["core_bundle_pairs"] = cc["bundle_pairs"]
    out["core_bundle_interpenetrating"] = cc["bundle_inter"]
    out["core_bundle_fraction"] = cc["bundle_fraction"]
    out["charge_length_factor"] = M.rules_for("Charge", species).length_factor
    out["chain_touching_scale"] = chain_touching_scale(out["charge_length_factor"])
    # The bar is measured the same way: ALL touching pairs, chain included.
    out["sibling_bare"] = max(reports[e]["all_fraction"] for e in ("Mass", "Space", "Time"))
    out["charge_bare_area"] = charge["area"]
    out["armoured_area"] = charge["area"] * 0.5 * CIRCUMSCRIBING_SCALE ** 2
    out["sibling_bare_area"] = sum(reports[e]["area"] for e in ("Mass", "Space", "Time")) / 3
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--render", metavar="DIR")
    ap.add_argument("--shields", action="store_true",
                    help="solve Charge's cross-section against its armour and print it")
    ap.add_argument("--species", default=None,
                    help="measure ONE species (default: every species in the model)")
    ap.add_argument("--fit-volume", action="store_true",
                    help="solve VOLUME_GAIN so the measured cumulative volume per PLANT "
                         "lands on the elemental law's ratios, and print it")
    args = ap.parse_args()

    if args.shields:
        return solve_charge(args.species)
    if args.fit_volume:
        return fit_volume(args.species)

    names = [args.species] if args.species else list(M.SPECIES)
    rc = 0
    for name in names:
        rc |= measure_species(name, args)
    return rc


def measure_species(species, args):
    spec = M.SPECIES[species]
    reports = {}
    heart, heart_src = heart_world_scale(species)
    heart_half = 0.5 * heart
    print("=" * 96)
    print(f"{spec['display']}  ({species}) — measured from the shipped surface table")
    shape = ('HELICOIDAL twist, ' + str(spec['twist']) + ' deg/step' if spec['twist']
             else spec.get('concept') or 'SMOOTH CROSSING CURVES, no twist')
    print(f"  concept: {shape}"
          f"\n           neutral prism {spec['neutral_cross']} x {spec['neutral_step']}")
    print(f"  heart: world scale {heart:.3f} (half-extent {heart_half:.3f}) from {heart_src}")
    print("=" * 96 + "\n")
    print(f"  {'element':8s} {'prisms':>6} {'curves':>6} {'volume':>10} {'per prism':>22} "
          f"{'dims':>16} {'touching / deep':>28}")
    for element in M.ELEMENTS:
        r = element_report(element, species=species)
        reports[element] = r
        lo, mean, hi = r["prism_volume"]
        print(f"  {element:8s} {r['prisms']:>6} {r['curves']:>6} {r['volume']:>10,.0f} "
              f"{lo:>6.2f}/{mean:>6.2f}/{hi:>6.2f} "
              f"{r['dims'][0]:>6.2f}..{r['dims'][1]:>6.2f} "
              f"{r['interpenetrating']:>5}/{r['pairs']:<5} "
              f"{r['interpenetrating_fraction']:>5.1%}  deep {r['deep_fraction']:>5.1%}")

    print("\n  coverage (equal-AREA bands in cos theta, pole to pole):")
    for element in M.ELEMENTS:
        print(f"    {element:8s} {reports[element]['coverage']}")

    # ── THE PLANT AS ONE OBJECT (the /flora skill §2) ─────────────────────────
    skeletons = {e: skeleton_report(species, e) for e in M.ELEMENTS}
    print("\n  GROWTH LAW: one object, out of one crystal, bonded limb by limb")
    print(f"    {'element':8s} {'prisms':>7} {'roots':>6} {'fwd':>4} {'parts':>6} "
          f"{'bond med/p99/max (strides)':>34}")
    for element in M.ELEMENTS:
        k = skeletons[element]
        print(f"    {element:8s} {k['prisms']:>7} {k['roots']:>6} {k['forward']:>4} "
              f"{k['components']:>6} {k['bond_median']:>12.2f} {k['bond_p99']:>10.2f} "
              f"{k['bond_max']:>10.2f}")

    # ── THE FALL ───────────────────────────────────────────────────────────────
    falls = {}
    for element in M.ELEMENTS:
        rules = grow_detail(species, element)["rules"]
        if authors_fall(rules):
            falls[element] = fall_report(species, element, reports[element])
    if falls:
        f0 = next(iter(falls.values()))
        print(f"\n  THE FALL (dive prisms separate on TanR != 0; stop sphere "
              f"{f0['stop_world']:.2f} u, crystal half-extent {heart_half:.3f} u)")
        print(f"    {'element':8s} {'dives':>9} {'share':>7} {'tip':>7} {'centre':>7} "
              f"{'dive/surf len':>14} {'hole/bound':>14} {'wind':>8} {'/e-fold':>8} "
              f"{'radial':>7} {'bands':>5} {'roll med/max':>14} {'cond':>6} {'trunc':>5}")
        for element in M.ELEMENTS:
            f = falls.get(element)
            if not f:
                continue
            print(f"    {element:8s} {f['laid']:>4}/{f['requested']:<4} {f['share']:>7.2%} "
                  f"{f['tip_min']:>7.2f} {f['centre_min']:>7.2f} "
                  f"{f['dive_len_max']:>6.2f}/{f['surf_len_max']:<7.2f} "
                  f"{f['hole_max']:>6.2f}/{f['hole_bound']:<7.2f} "
                  f"{f['winding_median']:>8.0f} {f['winding_per_efold_median']:>8.0f} "
                  f"{f['radial']:>7.1%} {f['dive_bands_filled']:>5} "
                  f"{f['roll_median']:>6.2f}/{f['roll_max']:<7.2f} "
                  f"{f['conditioning_min']:>6.2f} {f['truncated']:>5}")
        print(f"    radial split (surface / dive), raw roll before the authored "
              f"{f0['twist']:.0f} deg twist is removed, and emitted dive steps vs the cap:")
        for element in M.ELEMENTS:
            f = falls.get(element)
            if not f:
                continue
            print(f"      {element:8s} radial {f['radial_surface']:>6.1%} / "
                  f"{f['radial_dive']:>6.1%}   raw roll med {f['roll_raw_median']:>5.2f} "
                  f"max {f['roll_raw_max']:>6.2f} over {f['roll_pairs']:>4} walk-adjacent pairs"
                  f"   emitted {f['emitted_max']:>3}/{f['dive_max_steps']}"
                  f"   dive bands {f['dive_bands']}"
                  f" (starts in {f['dive_start_bands']}/8, bound {f['dive_band_bound']}"
                  + (f" = min({DIVE_BAND_MIN}, laid {f['laid']}, owed rings release into "
                     f"{f['dive_bands_occupiable']})" if f["dive_bands_occupiable"] else "")
                  + f")   spent {f['spent']} of {f['requested']} owed, {f['seeds']} seeds")
            print(f"               sunburst bound {f['radial_bound']:.0%} "
                  f"({'gradient-flow' if f['skeleton'] else 'walking'} species)   "
                  f"dive ENDS {[round(r, 1) for r in f['dive_ends'][:5]]} u, worst "
                  f"{f['dive_end_max']:.2f} against {f['dive_end_bound']:.2f} = "
                  f"{DIVE_END_STOP_FACTOR:.0f}x the stop sphere "
                  f"({f['dive_ends_over']} amputated)")

    # ── THE WATERSHED ──────────────────────────────────────────────────────────
    sheds = {}
    for element in M.ELEMENTS:
        rules = grow_detail(species, element)["rules"]
        if authors_skeleton(rules):
            # The inert-column probe is two extra walks per column and it is in the plain
            # report as well as in --check: "six of these nine authored numbers do nothing
            # and three of them do" is a fact a reader needs BEFORE they try tuning one.
            sheds[element] = watershed_report(species, element, reports[element], inert=True)
    if sheds:
        print("\n  THE WATERSHED (skeleton species: every curve is a separatrix of R(theta, phi))")
        print(f"    {'element':8s} {'order':>5} {'peaks':>6} {'sadl':>5} {'pits':>5} {'chi':>5} "
              f"{'rings':>26} {'modal/want':>10} {'spec m':>7} {'sign':>7} {'net':>11} "
              f"{'survival':>8} {'mean arm':>8}")
        for element in M.ELEMENTS:
            w = sheds.get(element)
            if not w:
                continue
            print(f"    {element:8s} {w['order']:>5} {w['peaks']:>6} {w['saddles']:>5} "
                  f"{w['pits']:>5} {w['euler']:>5} {str(w['sizes']):>26} "
                  f"{w['modal']:>5}/{w['want']:<4} {w['spectrum_peak']:>7} "
                  f"{w['sign_violations']:>3}/{w['sign_curves']:<3} "
                  f"{w['saddles_keeping_arms']:>5}/{w['saddle_count']:<5} "
                  f"{w['saddle_survival']:>8.1%} {w['mean_arm']:>8.2f}")
        for element in M.ELEMENTS:
            w = sheds.get(element)
            if not w:
                continue
            lanes = ", ".join(f"{k}:{v:.1%}" for k, v in sorted(w["lane_share"].items()))
            print(f"      {element:8s} lanes [{lanes}]  candidates {w['candidates']}/"
                  f"{w['candidate_cap']}  LengthFactor {w['length_factor']:.4f}  "
                  f"walk step {w['walk_step']:.4f}")
            print(f"               ring phases (fraction of one lobe) {w['phases']}   "
                  f"gap kept <= {w['gap_kept_max']:.4f} | split >= {w['gap_split_min']:.4f} "
                  + (f"about a DERIVED {w['ring_gap']:.4f} — the geometric midpoint of this "
                     f"element's widest empty band "
                     f"({w['ring_gap_band'][0]:.4f}..{w['ring_gap_band'][1]:.4f}, "
                     f"{w['ring_gap_ratio']:.1f}x), gaps floored at the census's "
                     f"{w['ring_gap_floor']:.4f} dedupe chord"
                     if w["ring_gap_derived"] else
                     f"about the {RING_GAP} FALLBACK (no band exceeds "
                     f"{RING_GAP_RATIO_MIN}x)"))
            print(f"               spectrum |sum exp(i m phi)|/N, m=1..{2 * w['order']}: "
                  f"{[round(v, 3) for v in w['spectrum']]}")
            print(f"               seed spread {w['spread_bands']}/{AREA_BANDS} bands over the "
                  f"first {w['prefix']} saddles — bound max({w['spread_bound']} = "
                  f"{SEED_SPREAD_DISCOUNT:.0%} of the {w['spread_uniform']:.2f} a UNIFORM draw "
                  f"of {w['prefix']} would fill, capped at {SEED_SPREAD_BANDS}; "
                  f"{w['spread_control_bound']} = {SEED_SPREAD_CONTROL_FACTOR}x the "
                  f"sharpness-major control's {w['spread_bands_control']})   arm attribution "
                  f"worst {w['attribution_worst_deg']:.2f} deg")
            print(f"               peaks carrying >= 2 ridge ends {w['peaks_on_plant_n']}/"
                  f"{w['peaks']} = {w['peaks_on_plant']:.1%} (tolerance "
                  f"{w['peak_tolerance']:.4f}, {w['ridge_ends']} ridge ends)")
            if "inert_read" in w:
                inert = [k for k in w["inert_tested"] if k not in w["inert_read"]]
                print(f"               inert columns: {len(inert)}/{len(w['inert_tested'])} "
                      f"prove byte-identical ({', '.join(inert)}); READ: "
                      f"{', '.join(w['inert_read']) or 'none'}")
            print(f"               Euler chi = peaks - saddles + pits = {w['euler']} "
                  f"(REPORTED, never gated: a finite grid cannot promise it found every "
                  f"critical point)")

    # ── APOLLONIA ──────────────────────────────────────────────────────────────
    gaskets = {}
    for element in M.ELEMENTS:
        rules = grow_detail(species, element)["rules"]
        if authors_gasket(rules):
            gaskets[element] = gasket_report(species, element, reports[element], inert=True)
    if gaskets:
        import mandelbulb_flora_render as _R
        arena_factor = _R.SHEET_VIEWS[0][3]
        g0 = next(iter(gaskets.values()))
        print(f"\n  APOLLONIA (one ring per disc, laid rho-DESCENDING; the lane IS the size "
              f"octave, {len(g0['by_lane'])} authored)")
        print(f"    {'element':8s} {'N':>4} {'round':>6} {'rho_ref':>8} {'discs':>6} "
              f"{'n0 laid/built':>13} {'by level':>22} {'raw/claim/laid':>18} {'fit':>6} {'rings':>6} "
              f"{'worst ring':>10} {'<80%':>5} {'tangency med/max':>18}")
        for element in M.ELEMENTS:
            g = gaskets.get(element)
            if not g:
                continue
            print(f"    {element:8s} {g['samples']:>4} {g['samples_margin']:>6.3f} "
                  f"{g['rho_ref']:>8.4f} {g['discs']:>6} "
                  f"{str(g['level0_laid']) + '/' + str(g['level0']):>13} "
                  f"{str(g['by_level']):>22} "
                  f"{g['raw']:>5}/{g['claimed']:<5}/{g['laid']:<5} {g['fit']:>6.0%} "
                  f"{g['rings']:>6} {g['ring_integrity_min']:>10.1%} {g['rings_broken']:>5} "
                  f"{g['tangency_median']:>8.4f}/{g['tangency_max']:<9.4f}")
        lo, hi = FORM_FIT_BAND
        print(f"    fit band [{lo:.0%}, {hi:.0%}] of the {M.PRISM_BUDGET} budget; ring "
              f"integrity is measured on the CLAIM-FILTERED list BEFORE budget truncation, "
              f"so a budget cut cannot read as a broken ring")
        print(f"\n    THE LADDER, IN SCREEN TERMS — share of the {ARENA_TILE}px arena frame "
              f"(the judging sheet's own tile at {arena_factor}x extent) and the median "
              f"prism's projected LENGTH. An octave is LEGIBLE at >= {OCTAVE_FRAME_MIN:.1%} "
              f"and >= {OCTAVE_PX_MIN}px; the bound is {OCTAVE_LEGIBLE_MIN} of them. CHARGE is "
              f"gated ARMOURED — its leaves are shielded by law and a shield draws the "
              f"circumscribing octahedron (0.5 x {CIRCUMSCRIBING_SCALE:.0f}^2 = "
              f"{0.5 * CIRCUMSCRIBING_SCALE ** 2:.1f}x the silhouette), so the bare row is the "
              f"body nobody sees; the pair IS the §50 inversion.")
        for element in M.ELEMENTS:
            g = gaskets.get(element)
            if not g:
                continue
            def band(table):
                return "  ".join(
                    f"oct{k} {v['frame']:>6.2%}/{v['px']:>5.1f}px/{v['prisms']:>4}p"
                    for k, v in sorted(table.items()))
            print(f"      {element:8s} +z        {band(g['octaves'])}"
                  + ("   <- BARE, reported only" if g["armoured"] else ""))
            if g["armoured"]:
                print(f"               ARMOURED  {band(g['octaves_ladder'])}   <- GATED")
            print(f"               camera    {band(g['octaves_camera'])}")
            print(f"               legible {g['legible']}"
                  + (f" armoured (bare would be {g['legible_bare']})" if g["armoured"] else "")
                  + f" (camera {g['legible_camera']}), "
                  # The header promises the bare figure is REPORTED beside the armoured one
                  # wherever the armour is gated; `frame_total` follows `octaves_ladder`, so
                  # on Charge it is the armoured number and saying only "frame total" would
                  # have made this the one place that promise was not kept.
                  + (f"frame total {g['frame_total']:.2%} armoured / "
                     f"{g['frame_total_bare']:.2%} bare" if g["armoured"] else
                     f"frame total {g['frame_total']:.2%}")
                  + f", invisible spend {g['invisible_spend']:.0%} of prisms   "
                  f"{g['ppu']:.2f} px/u at eye {g['eye']:.0f} u, extent {g['extent']:.1f} u")
            print(f"               lay order: last prism in octave {g['last_lane']} of "
                  f"{g['max_lane']} present; discs per octave {g['by_lane']}, prisms per "
                  f"octave {g['lane_prisms']}"
                  + ("" if g['lanes_contiguous'] else " (A RUNG IS MISSING — the octaves "
                     f"present are {g['lanes_present']}, not 0..{g['max_lane']})")
                  + f", of which the FALL's are {g['lane_dives']} "
                  f"(a dive carries its ring's lane, so the largest octave is the one it "
                  f"flatters)")
            print(f"               budget cut {g['truncated_prisms']} prisms"
                  + (f" and it cut the LAST RING mid-way ({g['last_ring_laid']}/"
                     f"{g['last_ring_survived']} of its samples) — REPORTED, not gated: the "
                     f"fix is to refuse to START a ring that cannot fit"
                     if g['cut_mid_ring'] else " (no ring was cut mid-way)"))
            print(f"               rho {g['rho_top'][0]:.4f}..{g['rho_top'][1]:.4f} at level 0, "
                  f"span {g['rho_span']:.2f}x over the plant; {g['fallback0']} of "
                  f"{g['level0']} level-0 discs took the lone-disc pi/6 fallback"
                  + (" AND rho_ref IS that constant, so the girth, octave and N ladders are "
                     "keyed on a frozen number rather than on this surface"
                     if g['rho_ref_is_fallback'] else ""))
            print(f"               N = round(circ/step) = round({g['samples_exact']:.3f}), "
                  f"margin to the rounding boundary {g['samples_margin']:.3f}"
                  + (f" but N is {g['samples']} — CLAMPED into "
                     f"[{M.RING_MIN_SAMPLES}, {M.RING_MAX_SAMPLES}], so the margin is "
                     f"describing a boundary this plant is not standing near"
                     if g['samples_clamped'] else "")
                  + (" (AUTHORED, so the margin is decoration)" if g['samples_authored'] else
                     " — N is shared by every ring, so one unit either way is a different "
                     "plant; REPORTED, never gated")
                  + (f"; {g['rings_short_emit']} rings emitted a count other than N"
                     if g['rings_short_emit'] else ""))
            if "inert_read" in g:
                inert = [k for k in g["inert_tested"] if k not in g["inert_read"]]
                print(f"               inert columns: {len(inert)}/{len(g['inert_tested'])} "
                      f"prove byte-identical; READ: {', '.join(g['inert_read']) or 'none'}")
        n = {e: g["samples"] for e, g in gaskets.items()}
        print(f"    ring coarseness N {n} — the element's long axis spent as SAMPLING, "
              f"required {COARSENESS_ORDER[0]} < {COARSENESS_ORDER[1]} <= "
              f"{COARSENESS_ORDER[2]} < {COARSENESS_ORDER[3]} and N_Mass/N_Space >= "
              f"{COARSENESS_RATIO_MIN}")

    s = shield_report(reports, species)
    print(f"\n  Charge armour: {s['armoured_interpenetrating']}/{s['armoured_pairs']} "
          f"({s['armoured_fraction']:.1%}) against its siblings' bare {s['sibling_bare']:.1%}")
    print(f"  Charge armour at the CORE (inside {CORE_RADIUS_FRACTION:.2f} R = "
          f"{s['core_radius']:.1f} u, {s['core_prisms']} prisms): BUNDLE "
          f"{s['core_bundle_interpenetrating']}/{s['core_bundle_pairs']} = "
          f"{s['core_bundle_fraction']:.1%} (bound {CORE_ARMOURED_MAX:.0%}) | CHAIN "
          f"{s['core_chain_interpenetrating']}/{s['core_chain_pairs']} = "
          f"{s['core_chain_fraction']:.1%} at the closed form s* = 1/(3 x LengthFactor "
          f"{s['charge_length_factor']:.4f}) = {s['chain_touching_scale']:.4f}, REPORTED and "
          f"not gated | both together {s['core_interpenetrating']}/{s['core_pairs']} = "
          f"{s['core_fraction']:.1%}")
    print(f"  silhouette: Charge bare {s['charge_bare_area']:,.0f}, armoured "
          f"{s['armoured_area']:,.0f}, siblings bare {s['sibling_bare_area']:,.0f}")

    cap = M.MAX_LIVE_POPULATION
    heaviest = max(r["volume"] for r in reports.values())
    print(f"\n  budget: {M.PRISM_BUDGET} live prisms/plant, cap {cap} plants "
          f"-> {cap * heaviest:,.0f} volume and {cap} always-on heart colliders")
    print("  in NO SpawnProfile — opt-in from the Spawn Matrix toy, so it costs no "
          "shipped cell anything until somebody puts it in one.")

    if args.render:
        render_all(reports, os.path.join(args.render, species), heart_half)

    if args.check:
        bad = []
        for element, r in reports.items():
            if r["deep_fraction"] > 0.05:
                bad.append(f"{element}: {r['deep_fraction']:.1%} of touching pairs are DEEPLY "
                           f"interleaved (s* < 0.5; bound 5%) — the claim rule is not holding")
            if r["worst_scale"] < 0.35:
                bad.append(f"{element}: two prisms interleave to s* {r['worst_scale']:.3f} "
                           f"(bound 0.35) — one is essentially inside the other")
            if r["size_span"] < 3.0:
                bad.append(f"{element}: prism volume spans only {r['size_span']:.1f}x — the "
                           f"girth taper is what stops a plant reading as one material at "
                           f"one scale")
            filled = sum(1 for b in r["coverage"] if b > 0.02 * r["prisms"])
            if filled < 6:
                bad.append(f"{element}: only {filled}/8 equal-area bands carry 2% of the "
                           f"plant — it has grown a cap, not a bulb")
        if s["armoured_fraction"] > s["sibling_bare"]:
            bad.append(f"Charge wearing its shields is MORE fused ({s['armoured_fraction']:.1%}) "
                       f"than a sibling is bare ({s['sibling_bare']:.1%})")
        if not s["armoured_area"] > s["sibling_bare_area"] > s["charge_bare_area"]:
            bad.append("the Charge ordering flipped: armoured must be the DENSEST of the four "
                       "and bare the sparsest — that ordering IS the two-pass grazing cost")
        # THE FALL's convergence is the tightest packing in the species, and it is 2% of the
        # plant, so the whole-plant armour figure above cannot see it.
        if s["core_bundle_fraction"] > CORE_ARMOURED_MAX:
            bad.append(f"Charge ARMOUR AT THE CORE: {s['core_bundle_interpenetrating']}/"
                       f"{s['core_bundle_pairs']} = {s['core_bundle_fraction']:.1%} of armoured "
                       f"BUNDLE pairs inside {CORE_RADIUS_FRACTION:.2f} R "
                       f"({s['core_radius']:.1f} u, {s['core_prisms']} prisms) interpenetrate "
                       f"(bound {CORE_ARMOURED_MAX:.0%}) — the Fall's bundle has fused into a "
                       f"rod. The levers are DiveStopRadius (pull the tips out of the tightest "
                       f"shell), DiveGirthFloor and DiveCount, never the whole-plant fit. "
                       f"(The CHAIN — a curve's own consecutive prisms — is measured "
                       f"separately at {s['core_chain_interpenetrating']}/"
                       f"{s['core_chain_pairs']} = {s['core_chain_fraction']:.1%} and is NOT "
                       f"this bound: its touching scale is the closed form 1/(3 x "
                       f"LengthFactor {s['charge_length_factor']:.4f}) = "
                       f"{s['chain_touching_scale']:.4f}, which no dive dial and no "
                       f"cross-section appears in — the lever there is CHARGE_DASH or this "
                       f"species' own Charge walk step, and both are §35/§51 decisions)")

        for element in M.ELEMENTS:
            bad += skeleton_gates(species, element, skeletons[element])
            if element in falls:
                bad += fall_gates(species, element, falls[element], heart_half)
            if element in sheds:
                bad += watershed_gates(species, element, sheds[element])
            if element in gaskets:
                bad += gasket_gates(species, element, gaskets[element])
        if gaskets:
            bad += gasket_species_gates(species, gaskets)

        # THE ELEMENTAL LAW (Docs/ECOSYSTEM.md §51). This species is EXEMPT from the runtime
        # leaf transform (Flora.PrismSizeFixedByGrowthRule), so it has to state the law in its
        # own data - and the claim the player can actually see is about the PLANT, so it is
        # gated on measured CUMULATIVE volume rather than on the authored prism.
        vol = {e: r["volume"] for e, r in reports.items()}
        asp = {}
        for e in M.ELEMENTS:
            x, y, z, lf = M.elemental_prism(species, e)
            axes = (x, y, z * lf)
            asp[e] = max(axes) / min(axes)
        if max(vol, key=vol.get) != "Mass":
            bad.append(f"MASS must carry the most cumulative prism volume; "
                       f"{max(vol, key=vol.get)} does ({vol})")
        if vol["Space"] >= vol["Time"]:
            bad.append(f"SPACE ({vol['Space']:,.0f}) must carry LESS cumulative prism volume "
                       f"than TIME ({vol['Time']:,.0f}) - its long axis is what it trades")
        if max(asp, key=asp.get) != "Space":
            bad.append(f"SPACE must have the highest prism aspect ratio; "
                       f"{max(asp, key=asp.get)} does ({ {k: round(v,2) for k,v in asp.items()} })")
        if min(asp, key=asp.get) != "Mass":
            bad.append(f"MASS must have the most CUBIC prism (x, y and z closest together); "
                       f"{min(asp, key=asp.get)} does ({ {k: round(v,2) for k,v in asp.items()} })")
        # SPACE trades that volume for the assembly's BOUNDING VOLUME.
        reach = {e: reports[e]["dims"][1] for e in M.ELEMENTS}
        if reports["Space"]["radius"] <= reports["Mass"]["radius"]:
            bad.append(f"SPACE's plant ({reports['Space']['radius']:.0f}) must reach further "
                       f"than MASS's ({reports['Mass']['radius']:.0f}) - the bounding volume "
                       f"is what its lost prism volume buys")
        if bad:
            print(f"\nFAIL ({len(bad)} bounds broken)")
            # The failures go to stderr and the report to stdout, so a redirect that merges
            # them interleaves on the buffer rather than on the writes unless stdout is
            # flushed first — a failure list printed above its own measurements is unreadable.
            sys.stdout.flush()
            for b in bad:
                print("  " + b, file=sys.stderr)
            return 1
        print("\nOK: every bound holds.")
    return 0


def fit_volume(only=None):
    """Solve VOLUME_GAIN so the measured CUMULATIVE volume per plant lands on the elemental
    law's ratios (Docs/ECOSYSTEM.md §51), against TIME as the neutral.

    The law sets the AUTHORED prism; what the player sees is the plant, and every prism's
    cross-section is additionally multiplied by its curve's emergent GIRTH. So a fit is the
    honest instrument here - and it is one iteration, not a search, because volume goes
    exactly as the cross-section SQUARED and nothing else in the walk depends on it (the
    claim radius is a fraction of a prism's LENGTH, so the prism set is unchanged).
    """
    for species in ([only] if only else list(M.SPECIES)):
        print(f"\n{species}: VOLUME_GAIN, one iteration from the current measurement")
        reports = {e: element_report(e, species=species) for e in M.ELEMENTS}
        ref = reports["Time"]["volume"]
        current = M.VOLUME_GAIN.get(species, {})
        solved = {}
        for element in M.ELEMENTS:
            want = M.ELEMENT_VOLUME[element]
            if element == "Charge":
                # Charge's armour fit deliberately takes it BELOW the neutral, so its target
                # is the neutral scaled by the shrink it was fitted to - the law does not
                # claim Charge's bare prism is a neutral one, only that its SHAPE is.
                want *= M.CHARGE_SHIELD_SHRINK ** 2 * M.CHARGE_DASH
            got = reports[element]["volume"] / ref
            gain = current.get(element, 1.0) * math.sqrt(want / got)
            solved[element] = round(gain, 4)
            print(f"    {element:7s} want x{want:6.3f} of Time, measured x{got:6.3f} "
                  f"-> gain {current.get(element, 1.0):.4f} -> {gain:.4f}")
        print(f'    "{species}": ' + str(solved).replace("'", '"') + ",")
    print("\nPaste into VOLUME_GAIN in mandelbulb_flora_model.py and re-run --check.")
    return 0


def solve_charge(only=None):
    """Shrink Charge's ribbon uniformly (its ASPECT is its identity, so uniformly) until
    its armour is no more fused than a sibling is bare.

    TWO bars, because the Fall gave this species a second place to fuse: the WHOLE plant
    against a sibling's bare interpenetration, and the CORE — the prisms inside
    CORE_RADIUS_FRACTION of the plant's own radius, where every dive converges. The core is
    2% of the plant, so the whole-plant figure is structurally blind to it, and the
    `<= clears` mark is against BOTH bars because --check gates both.

    MEASURED, and it is the reason this sweep exists as a REPORT rather than as a solver:
    the cross-section is not a lever on the core at all. On a species whose chain has fused
    — LengthFactor > 1/3, so CoralBloom and Apollonia and not the other two, see
    CORE_ARMOURED_MAX — shrinking it makes the UN-SPLIT core fraction monotonically WORSE
    (CoralBloom 39.0% at k=1.00 -> 47.8% at k=0.25, Apollonia 23.7% -> 35.8%), while the
    BUNDLE this file actually gates falls (8.3% -> 0.0% and 14.5% -> 3.1%). Below the cliff
    there is nothing to invert and the un-split figure simply falls with the bundle
    (FractalFoliage 10.3% -> 0.0%, Watershed 7.5% -> 5.1%). The inversion happens
    because a shield reaches 1.5 x leafSize on all three half-extents INCLUDING the length,
    so a thinner prism keeps its armour's reach along the ribbon while the touching-pair
    denominator collapses to the pairs that were already fused. The levers that do reach it
    are the Fall's own: DiveStopRadius, DiveGirthFloor and DiveCount."""
    for species in ([only] if only else list(M.SPECIES)):
        print(f"\n{species}: Charge cross-section against its armour")
        reports = {e: element_report(e, species=species) for e in ("Mass", "Space", "Time")}
        # The same statistic --check compares against: ALL touching pairs, chain included.
        # `interpenetrating_fraction` excludes a curve's own chain and is reported beside it,
        # because a --shields run that said "clears" on a different statistic from the one
        # --check gates would be a tool disagreeing with its own build gate.
        bar = max(r["all_fraction"] for r in reports.values())
        bar_nochain = max(r["interpenetrating_fraction"] for r in reports.values())
        print(f"  bar: a sibling's bare interpenetration is {bar:.1%} over ALL touching pairs "
              f"({bar_nochain:.1%} with the chain excluded); the CORE bar is "
              f"{CORE_ARMOURED_MAX:.0%}")
        # The sweep's base is THIS species' own Space cross-section, not the module-level
        # CROSS_SECTION view — that one is the FractalFoliage back-compat table, so a sweep
        # keyed on it measured the wrong species' ribbon on the other two. Same class of
        # single-species leftover as the undefined `species` this function used to carry.
        base = M.cross_section_for("Space", species)
        shipped = M.cross_section_for("Charge", species)
        print(f"  sweep base = this species' SPACE cross {base[0]:.4f} x {base[1]:.4f}; the "
              f"SHIPPED Charge cross {shipped[0]:.4f} x {shipped[1]:.4f} sits at "
              f"k={shipped[0] / max(base[0], 1e-9):.3f}")
        lenf = M.rules_for("Charge", species).length_factor
        s_star = chain_touching_scale(lenf)
        print(f"  the CORE column is the BUNDLE (cross-curve) fraction, which is what --check "
              f"gates; the CHAIN is printed beside it and sits at the closed form "
              f"s* = 1/(3 x LengthFactor {lenf:.4f}) = {s_star:.4f} — INVARIANT under this "
              f"sweep by construction, which is why the cross-section is not a lever on it. "
              + (f"s* < 1, so this species' chain is FUSED and pinned at 100%: it dominates "
                 f"the un-split column, which therefore moves the WRONG way as k falls while "
                 f"the bundle improves." if s_star < 1.0 else
                 f"s* >= 1, so this species' chain is CLEAR and contributes nothing: the "
                 f"un-split column falls with the bundle here, and the pathology that split "
                 f"this statistic is not visible on this species at all."))
        for k in (1.0, 0.8, 0.65, 0.55, 0.50, 0.45, 0.40, 0.35, 0.30, 0.25):
            cross = (base[0] * k, base[1] * k)
            r = element_report("Charge", cross=cross, species=species)
            _, n, frac = armoured_fraction(r["boxes"])
            cb, limit, cchain = core_boxes(r)
            cc = armoured_census(cb, cchain)
            cfrac = cc["bundle_fraction"]
            mark = "  <= clears both" if (frac <= bar and cfrac <= CORE_ARMOURED_MAX) else (
                "  <= clears the plant only" if frac <= bar else "")
            print(f"  k={k:.2f}  cross=({cross[0]:.4f}, {cross[1]:.4f})  "
                  f"plant {frac:>6.1%} of {n:<5}  core(<= {limit:.1f} u, {len(cb):>3} prisms) "
                  f"bundle {cfrac:>6.1%} of {cc['bundle_pairs']:<5} chain "
                  f"{cc['chain_fraction']:>6.1%} of {cc['chain_pairs']:<4} "
                  f"both {cc['fraction']:>6.1%}{mark}")
    return 0


def render_all(reports, out_dir, heart_half=0.0):
    """Three passes per element, because they answer three different questions.

    The SHEET is the plant (four framings, the heart drawn at its real size). The DIVES-ONLY
    sheet is THE FALL alone — at 3-15% of the plant a dive is invisible in a full render,
    and "the spiral reads as a spiral" is a look claim no statistic settles. The CLOSE-UP is
    framed on an ABSOLUTE 30 world units around the origin rather than on the plant's own
    extent, which is the only framing in which "the spindles almost connect to their
    crystal" can be looked at."""
    import mandelbulb_flora_render as R
    os.makedirs(out_dir, exist_ok=True)
    for element, r in reports.items():
        stem = os.path.join(out_dir, f"mandelbulb_{element.lower()}")
        R.render_sheet(r["boxes"], stem + ".png", heart=heart_half)
        print(f"  rendered {stem}.png")
        dive_ix = M.free_space_runs(r["prisms_list"])[1]
        dives = [r["boxes"][i] for i in dive_ix]
        if dives:
            R.render_sheet(dives, stem + "_dives.png", heart=heart_half)
            print(f"  rendered {stem}_dives.png  ({len(dives)} dive prisms)")
        R.render_closeup(r["boxes"], stem + "_heart.png", span=CLOSEUP_SPAN,
                         heart=heart_half)
        print(f"  rendered {stem}_heart.png  ({CLOSEUP_SPAN:.0f} u across, heart drawn)")


CLOSEUP_SPAN = 30.0   # world units across the frame: the Fall's arrival, at its real size


if __name__ == "__main__":
    sys.exit(main())
