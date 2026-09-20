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
RADIAL_FRACTION_MAX = 0.55       # the sunburst gate, over the WHOLE plant
RADIAL_COS = 0.707               # 45 degrees off the ray to the heart
DIVE_BAND_MIN = 4                # equal-area theta bands the dive set alone must populate
DIVE_ROLL_MEDIAN_MAX = 10.0      # degrees, transport-corrected, net of the authored twist
DIVE_ROLL_PAIR_MAX = 60.0
DIVE_CONDITIONING_MIN = 0.30     # |sin angle(ray, forward)| — how well-posed `up` is

# ── THE WATERSHED's bounds ─────────────────────────────────────────────────────
NET_SADDLE_SURVIVAL = 0.65       # saddles keeping >= 3 laid arms
NET_ARMS = 3
NET_MEAN_ARM = 5.0               # laid SURFACE prisms per surviving arm
LANE_SHARE_MIN = 0.15            # no lane starved by the budget
RING_GAP = 0.08                  # radians of theta that separate two latitude rings
SEED_SPREAD_BANDS = 6            # equal-area bands the first quarter of the order must fill
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
    budget = budget or M.PRISM_BUDGET
    key = (species, element, budget)
    if key in _GROWN:
        return _GROWN[key]
    degree, tables = M.load_tables()
    surface = M.surface_for(element, tables=tables, degree=degree, width=M.FIELD_WIDTH)
    rules = M.rules_for(element, species)
    raw, raw_curves, dives_spent = M.grow(surface, rules, 12345, budget * CANDIDATE_FACTOR)
    centres = [M._mul(M.pose(surface, p)[0], M.SHELL_RADIUS) for p in raw]
    # AFTER THE CLAIM, BEFORE THE BUDGET, kept separately: the two answer different
    # questions and conflating them reads a budget cut as a broken curve (see
    # gasket_report's ring integrity, which is the gate that found this out the hard way).
    claimed = M.claim_filter(raw, centres)
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


def grow_element(element, budget=None, species="FractalFoliage"):
    """The plant the game LAYS: the walk, then the claim (MandelbulbFlora.Claim), then the
    budget. Measuring the raw walk would describe candidates rather than prisms.

    The two species share the surface FAMILY (one bake per element) and differ in their
    curve rules, so the surface is keyed on the element alone and the walk on both."""
    d = grow_detail(species, element, budget)
    return d["surface"], d["rules"], d["kept"], d["curves"]


def element_report(element, shell=None, cross=None, budget=None, species="FractalFoliage"):
    shell = shell or M.SHELL_RADIUS
    cross = cross or M.cross_section_for(element, species)
    surface, rules, prisms, curves = grow_element(element, budget, species)
    boxes = [obb(surface, p, shell, cross) for p in prisms]

    vols = [8 * b[2][0] * b[2][1] * b[2][2] for b in boxes]
    dims = [d * 2 for b in boxes for d in b[2]]
    reach = 2 * max(math.sqrt(sum(h * h for h in b[2])) for b in boxes)

    # Consecutive prisms of one CURVE are a chain — they are laid end to end by
    # construction and a curve that bends brings their boxes together, exactly as a
    # lattice species' bonded neighbours touch. They are measured separately: the
    # interpenetration BOUND is about ribbons that cross, which is the thing a growth rule
    # can get wrong.
    pairs = []
    chain_worst = 1e9
    for i, j in near_pairs(boxes, reach):
        if prisms[i].curve == prisms[j].curve and abs(i - j) == 1:
            chain_worst = min(chain_worst, touching_scale(boxes[i], boxes[j]))
            continue
        pairs.append((i, j))
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
        # the assembly" is measured against (Docs/ECOSYSTEM.md §45).
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
        "chain_worst": chain_worst,
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


def fall_report(species, element, report):
    """Every number THE FALL is gated on, measured on the LAID plant.

    Dive prisms separate on `TanR != 0` — the stamp `Emit` puts on a prism whose heading is
    absolute rather than tangential — so this is a partition of the plant, never a guess at
    which prisms were the dive."""
    d = grow_detail(species, element)
    rules, kept, boxes = d["rules"], d["kept"], report["boxes"]
    walk = d["walk_index"]
    shell = M.SHELL_RADIUS
    stop_world = rules.dive_stop * shell
    twist = abs(M.SPECIES[species]["twist"])

    dive_i = [i for i, p in enumerate(kept) if p.tan_r != 0.0]
    surf_i = [i for i, p in enumerate(kept) if p.tan_r == 0.0]
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
    out["arrival"] = len(by_curve) / max(1, out["requested"])

    # (b) the crystal's clear air. The dive loop tests `r <= stop` at the TOP, so the last
    # appended point is BELOW the stop sphere by up to one step — which is why the gate is
    # on the laid prism's TIP and not on DiveStopRadius * shell.
    out["tip_min"] = min((M._len(boxes[i][0]) - kept[i].length * shell * 0.5
                          for i in dive_i), default=float("nan"))
    out["centre_min"] = min(M._len(b[0]) for b in boxes)

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
    for p in d["raw"]:
        if p.tan_r != 0.0:
            emitted[p.curve] = emitted.get(p.curve, 0) + 1
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
    out["dive_band_bound"] = min(DIVE_BAND_MIN, out["laid"])

    # (j) the body roll, and the conditioning of the frame it is measured in. `Pose` hangs
    # a dive prism's `up` off the RAY rather than off the surface normal, which is
    # ill-posed exactly where the heading is radial — so the roll is only meaningful while
    # |sin angle(ray, forward)| stays off zero, and both are reported.
    #
    # The roll is measured NET OF THE AUTHORED TWIST: Fractal Foliage rolls every prism
    # 12 deg/step about its own tangent BY DESIGN, so the raw figure measures the concept
    # rather than a defect (Docs/ECOSYSTEM.md §46 — a gate written against one species is
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
        bad.append(f"{tag} FALL arrival: {f['laid']} dives laid of {f['requested']} requested "
                   f"= {f['arrival']:.0%} (bound {DIVE_ARRIVAL_MIN:.0%}) — the mechanism is "
                   f"authored and most of it is not reaching the plant")
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
    if f["radial"] >= RADIAL_FRACTION_MAX:
        bad.append(f"{tag} SUNBURST: {f['radial']:.1%} of the plant points within 45 deg of the "
                   f"ray to the heart (bound {RADIAL_FRACTION_MAX:.0%}; surface "
                   f"{f['radial_surface']:.1%}, dive {f['radial_dive']:.1%})")
    if f["dive_start_bands"] < f["dive_band_bound"]:
        bad.append(f"{tag} FALL spread: the {f['laid']} laid dives LEAVE THE SURFACE in "
                   f"{f['dive_start_bands']}/8 equal-area theta bands (bound "
                   f"{f['dive_band_bound']} = min({DIVE_BAND_MIN}, dives laid); their prisms "
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


def _prism_tuple(p):
    return (p.theta, p.phi, p.radial, p.dive, p.tan_a, p.tan_b, p.tan_r,
            p.length, p.girth, p.roll, p.curve, p.lane)


def _lay(surface, rules, budget):
    raw, _, _ = M.grow(surface, rules, 12345, budget * CANDIDATE_FACTOR)
    centres = [M._mul(M.pose(surface, p)[0], M.SHELL_RADIUS) for p in raw]
    return [_prism_tuple(p) for p in M.claim_filter(raw, centres)[:budget]]


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
                   gap_split_min=float("nan"), gap_kept_max=float("nan"))
        return out

    ordered = sorted(peaks, key=lambda c: c.theta)
    rings, current, split, kept_gaps = [], [ordered[0]], [], []
    for a, b in zip(ordered, ordered[1:]):
        gap = b.theta - a.theta
        if gap > RING_GAP:
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
    shell = M.SHELL_RADIUS
    sads = M.saddles(surface)
    out = ring_census(surface, element)
    out["walk_step"] = rules.walk_step if rules.walk_step > 0 else rules.step
    out["length_factor"] = rules.length_factor
    out["candidates"] = len(d["raw"])
    out["candidate_cap"] = d["candidate_cap"]
    out["lanes_authored"] = max(1, rules.lanes)

    # Surface prisms per curve, in WALK order — the arm proper. A dive is a tail on an arm,
    # not part of the net, so it is excluded from every net statistic below.
    arm = {}
    for i, p in enumerate(kept):
        if p.tan_r == 0.0:
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
    lane_count = {}
    for p in kept:
        lane_count[p.lane] = lane_count.get(p.lane, 0) + 1
    out["lane_share"] = {k: lane_count.get(k, 0) / max(1, len(kept))
                         for k in range(out["lanes_authored"])}

    # (e) SEED SPREAD, and its negative control. Farthest-point ordering makes "every
    # prefix is spatially spread" true by construction; sharpness-major, which the design
    # shipped before it was measured, puts a plant's strongest quarter into 2 of 8 bands.
    take = max(1, int(math.ceil(SEED_SPREAD_PREFIX * len(sads))))
    out["prefix"] = take
    out["spread_bands"] = len({area_band(c.theta) for c in sads[:take]})
    control = sorted(sads, key=lambda c: (-c.sharpness, c.theta, c.phi))
    out["spread_bands_control"] = len({area_band(c.theta) for c in control[:take]})

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
                got = _lay(surface, M.Rules(*values), M.PRISM_BUDGET)
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
        bad.append(f"{tag} RING CENSUS: the modal peak-ring size is {w['modal']}, not "
                   f"order-1 = {w['want']} (rings {w['sizes']}, grouped at {RING_GAP} rad; "
                   f"gaps bracketing that threshold: kept up to {w['gap_kept_max']:.4f}, split "
                   f"from {w['gap_split_min']:.4f}) — the plant is drawing the wrong surface, "
                   f"or the grouping threshold is deciding the answer")
    want = w["want"]
    if want > 0 and w["spectrum_peak"] % want != 0:
        bad.append(f"{tag} RING CENSUS: the azimuthal power spectrum of the peak set peaks at "
                   f"m={w['spectrum_peak']} ({w['spectrum_value']:.3f}), which is not a multiple "
                   f"of order-1 = {want} — the (n-1)-fold fold is not in the surface")
    if w["spread_bands"] < SEED_SPREAD_BANDS:
        bad.append(f"{tag} SEED SPREAD: the first {w['prefix']} of {w['saddle_count']} saddles "
                   f"occupy {w['spread_bands']}/8 equal-area bands (bound {SEED_SPREAD_BANDS}; "
                   f"the sharpness-major control gets {w['spread_bands_control']}/8) — a budget "
                   f"prefix of this order is not spread over the sphere")
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
# that draws CURVES, and this one draws a PACKING (Docs/ECOSYSTEM.md §46 — a gate written
# against one species is a gate calibrated on one species). Measured on this species the
# two interpenetration bounds are near-vacuous — non-chain touching pairs are 17/1112/603/658
# against thousands on the ribbon species and deep-interleave is 0.0% on all four — because
# a packing cannot overlap by construction. What does the work here is RING INTEGRITY and
# THE LADDER IN SCREEN TERMS.

FORM_FIT_BAND = (0.90, 1.05)     # after-claim count / PRISM_BUDGET — a BAND, not a ceiling
RING_INTEGRITY_MIN = 0.80        # of its N samples, through the claim, BEFORE the budget
OCTAVE_FRAME_MIN = 0.010         # share of the arena frame one octave must paint
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


def arena_octaves(boxes, lanes, axis, tile=ARENA_TILE):
    """Per-octave UNION coverage of the arena frame and the median projected prism LENGTH.

    UNION, never a sum of areas: an octave's rings overlap each other on screen and a sum
    would report a dense small octave as covering more of the frame than it can."""
    import mandelbulb_flora_render as R
    ppu, dist, ext = R.arena_scale(boxes, tile)
    a, u, v = _basis(axis)
    half, masks, lengths = tile / 2.0, {}, {}
    for b, lane in zip(boxes, lanes):
        mask = masks.get(lane)
        if mask is None:
            mask = masks[lane] = bytearray(tile * tile)
            lengths[lane] = []
        c, ax, h = b
        corners = []
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
        # as summing the areas.
        d = M._dot(ax[2], a)
        lengths[lane].append(2 * h[2] * ppu * math.sqrt(max(0.0, 1.0 - d * d)))
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
           "budget": M.PRISM_BUDGET, "fit": len(claimed) / float(M.PRISM_BUDGET),
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
    oct_cam, _, _, _ = arena_octaves(boxes, lanes, cd)
    out["ppu"], out["eye"], out["extent"] = ppu, dist, ext
    out["octaves"] = oct_z
    out["octaves_camera"] = oct_cam
    legible = [k for k, v in oct_z.items()
               if v["frame"] >= OCTAVE_FRAME_MIN and v["px"] >= OCTAVE_PX_MIN]
    out["legible"] = sorted(legible)
    out["legible_camera"] = sorted(k for k, v in oct_cam.items()
                                   if v["frame"] >= OCTAVE_FRAME_MIN and v["px"] >= OCTAVE_PX_MIN)
    # A sum of per-OCTAVE unions, so two octaves overlapping on screen are counted twice.
    # Reported only, never gated (the gate is per octave); measured against the shipped
    # renderer's own arena tile it lands within 15% of the lit pixels, the difference being
    # the render's perspective spread and its heart.
    out["frame_total"] = sum(v["frame"] for v in oct_z.values())
    out["invisible_spend"] = sum(v["prisms"] for k, v in oct_z.items()
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
    for p in kept:
        if p.tan_r != 0.0:
            dives[p.lane] = dives.get(p.lane, 0) + 1
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
                got = _lay(d["surface"], M.Rules(*values), M.PRISM_BUDGET)
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
                           for k, v in sorted(g["octaves"].items()))
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

    RING COARSENESS is this species' §45 identity, and it is the only place in the family
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


def armoured_fraction(boxes):
    """Charge's octahedra against each other over a given set of boxes."""
    if len(boxes) < 2:
        return 0, 0, 0.0
    reach = 2 * CIRCUMSCRIBING_SCALE * max(
        math.sqrt(sum(h * h for h in b[2])) for b in boxes)
    pairs = sample(list(near_pairs(boxes, reach, CIRCUMSCRIBING_SCALE)))
    inter = sum(1 for i, j in pairs if touching_scale(boxes[i], boxes[j], shield=True) < 1.0)
    return inter, len(pairs), inter / max(1, len(pairs))


def core_boxes(report, fraction=CORE_RADIUS_FRACTION):
    """The prisms inside `fraction` of the plant's own bounding radius — where the Fall
    converges every dive it lays, and therefore the tightest packing in the species. The
    whole-plant armour figure cannot see it: it is 53 boxes of 2800."""
    limit = fraction * report["radius"]
    return [b for b in report["boxes"] if M._len(b[0]) <= limit], limit


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
    # a 2% subset cannot move a whole-plant fraction.
    cb, limit = core_boxes(charge)
    ci, cn, cf = armoured_fraction(cb)
    out["core_radius"] = limit
    out["core_prisms"] = len(cb)
    out["core_pairs"] = cn
    out["core_interpenetrating"] = ci
    out["core_fraction"] = cf
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
                  f" (starts in {f['dive_start_bands']}/8, bound {f['dive_band_bound']})"
                  f"   spent {f['spent']} of {f['requested']} owed, {f['seeds']} seeds")

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
                  f"about the {RING_GAP} threshold")
            print(f"               spectrum |sum exp(i m phi)|/N, m=1..{2 * w['order']}: "
                  f"{[round(v, 3) for v in w['spectrum']]}")
            print(f"               seed spread {w['spread_bands']}/8 bands over the first "
                  f"{w['prefix']} saddles (sharpness-major control "
                  f"{w['spread_bands_control']}/8)   arm attribution worst "
                  f"{w['attribution_worst_deg']:.2f} deg")
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
              f"and >= {OCTAVE_PX_MIN}px; the bound is {OCTAVE_LEGIBLE_MIN} of them.")
        for element in M.ELEMENTS:
            g = gaskets.get(element)
            if not g:
                continue
            def band(table):
                return "  ".join(
                    f"oct{k} {v['frame']:>6.2%}/{v['px']:>5.1f}px/{v['prisms']:>4}p"
                    for k, v in sorted(table.items()))
            print(f"      {element:8s} +z        {band(g['octaves'])}")
            print(f"               camera    {band(g['octaves_camera'])}")
            print(f"               legible {g['legible']} (camera {g['legible_camera']}), "
                  f"frame total {g['frame_total']:.2%}, invisible spend "
                  f"{g['invisible_spend']:.0%} of prisms   "
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
          f"{s['core_radius']:.1f} u, {s['core_prisms']} prisms): "
          f"{s['core_interpenetrating']}/{s['core_pairs']} = {s['core_fraction']:.1%} "
          f"(bound {CORE_ARMOURED_MAX:.0%})")
    print(f"  silhouette: Charge bare {s['charge_bare_area']:,.0f}, armoured "
          f"{s['armoured_area']:,.0f}, siblings bare {s['sibling_bare_area']:,.0f}")

    cap = M.MAX_LIVE_POPULATION
    heaviest = max(r["volume"] for r in reports.values())
    print(f"\n  budget: {M.PRISM_BUDGET} live prisms/plant, cap {cap} plants "
          f"-> {cap * heaviest:,.0f} volume and {cap} always-on heart colliders")
    print("  in NO SpawnProfile — opt-in from the Lifeform Matrix toy, so it costs no "
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
        if s["core_fraction"] > CORE_ARMOURED_MAX:
            bad.append(f"Charge ARMOUR AT THE CORE: {s['core_interpenetrating']}/"
                       f"{s['core_pairs']} = {s['core_fraction']:.1%} of armoured pairs inside "
                       f"{CORE_RADIUS_FRACTION:.2f} R ({s['core_radius']:.1f} u, "
                       f"{s['core_prisms']} prisms) interpenetrate (bound "
                       f"{CORE_ARMOURED_MAX:.0%}) — the Fall's bundle has fused into a rod. "
                       f"The levers are DiveStopRadius (pull the tips out of the tightest "
                       f"shell), DiveGirthFloor and DiveCount, never the whole-plant fit")

        for element in M.ELEMENTS:
            if element in falls:
                bad += fall_gates(species, element, falls[element], heart_half)
            if element in sheds:
                bad += watershed_gates(species, element, sheds[element])
            if element in gaskets:
                bad += gasket_gates(species, element, gaskets[element])
        if gaskets:
            bad += gasket_species_gates(species, gaskets)

        # THE ELEMENTAL LAW (Docs/ECOSYSTEM.md §45). This species is EXEMPT from the runtime
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
    law's ratios (Docs/ECOSYSTEM.md §45), against TIME as the neutral.

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
    the cross-section is not a lever on the core at all. Shrinking it makes the core
    fraction monotonically WORSE (FractalFoliage 21.3% at k=1.00 -> 45.6% at k=0.25),
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
        for k in (1.0, 0.8, 0.65, 0.55, 0.50, 0.45, 0.40, 0.35, 0.30, 0.25):
            cross = (base[0] * k, base[1] * k)
            r = element_report("Charge", cross=cross, species=species)
            _, n, frac = armoured_fraction(r["boxes"])
            cb, limit = core_boxes(r)
            _, cn, cfrac = armoured_fraction(cb)
            mark = "  <= clears both" if (frac <= bar and cfrac <= CORE_ARMOURED_MAX) else (
                "  <= clears the plant only" if frac <= bar else "")
            print(f"  k={k:.2f}  cross=({cross[0]:.4f}, {cross[1]:.4f})  "
                  f"plant {frac:>6.1%} of {n:<5}  core(<= {limit:.1f} u, {len(cb):>3} prisms) "
                  f"{cfrac:>6.1%} of {cn:<5}{mark}")
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
        dives = [b for b, p in zip(r["boxes"], r["prisms_list"]) if p.tan_r != 0.0]
        if dives:
            R.render_sheet(dives, stem + "_dives.png", heart=heart_half)
            print(f"  rendered {stem}_dives.png  ({len(dives)} dive prisms)")
        R.render_closeup(r["boxes"], stem + "_heart.png", span=CLOSEUP_SPAN,
                         heart=heart_half)
        print(f"  rendered {stem}_heart.png  ({CLOSEUP_SPAN:.0f} u across, heart drawn)")


CLOSEUP_SPAN = 30.0   # world units across the frame: the Fall's arrival, at its real size


if __name__ == "__main__":
    sys.exit(main())
