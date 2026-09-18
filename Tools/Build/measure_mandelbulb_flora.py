#!/usr/bin/env python3
"""Measure the Mandelbulb flora — the MODEL half of the species' tool trio.

Everything this species claims about itself is a number from here: prism counts, per-prism
and per-plant volume, the size spread, how evenly a plant covers its own surface, how
deeply two prisms may interleave, and the Charge shield fit. The growth rule itself lives
in mandelbulb_flora_model.py, which walks the SHIPPED spherical-harmonic table.

    measure_mandelbulb_flora.py                 # the report
    measure_mandelbulb_flora.py --check         # fail the build on a broken bound
    measure_mandelbulb_flora.py --render DIR    # PNGs — you cannot judge a plant you have
                                                # not looked at, and an offline model can
                                                # report a perfect size distribution for a
                                                # form that reads as gravel
    measure_mandelbulb_flora.py --shields       # re-solve Charge's cross-section

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
"""
import argparse
import math
import os
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


# ── the report ─────────────────────────────────────────────────────────────────

_GROWN = {}


CANDIDATE_FACTOR = 4   # MandelbulbFlora.AddressCandidateFactor


def grow_element(element, budget=None, species="FractalFoliage"):
    """The plant the game LAYS: the walk, then the claim (MandelbulbFlora.Claim), then the
    budget. Measuring the raw walk would describe candidates rather than prisms.

    The two species share the surface FAMILY (one bake per element) and differ in their
    curve rules, so the surface is keyed on the element alone and the walk on both."""
    budget = budget or M.PRISM_BUDGET
    key = (species, element, budget)
    if key in _GROWN:
        return _GROWN[key]
    degree, tables = M.load_tables()
    surface = M.surface_for(element, tables=tables, degree=degree, width=M.FIELD_WIDTH)
    rules = M.rules_for(element, species)
    raw, _ = M.grow(surface, rules, 12345, budget * CANDIDATE_FACTOR)
    centres = [M._mul(M.pose(surface, p)[0], M.SHELL_RADIUS) for p in raw]
    kept = M.claim_filter(raw, centres)[:budget]
    curves = len({p.curve for p in kept})
    _GROWN[key] = (surface, rules, kept, curves)
    return _GROWN[key]


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
        bins[min(7, int((1 - math.cos(p.theta)) / 2 * 8))] += 1

    # Silhouette: how much of its own bounding sphere the plant's prisms cover, bare and
    # armoured. The ratio of the two is a fact about the shield, not about the fit.
    area = sum(2 * (b[2][0] * b[2][1] + b[2][1] * b[2][2] + b[2][2] * b[2][0]) * 4
               for b in boxes)

    return {
        "element": element,
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


SHIELD_SAMPLE = 3000     # octahedron SAT is 88 axes; the estimate is sampled, seed fixed


def sample(pairs, n=SHIELD_SAMPLE, seed=1):
    """A fixed-seed sample. The armoured test is 88 separating axes per pair and a plant has
    tens of thousands of armoured pairs, so the fraction is ESTIMATED — stated rather than
    disguised, and at n=3000 the standard error on a fraction is under 1%."""
    if len(pairs) <= n:
        return pairs
    import random as _r
    return _r.Random(seed).sample(pairs, n)


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
    reach = 2 * CIRCUMSCRIBING_SCALE * max(
        math.sqrt(sum(h * h for h in b[2])) for b in charge["boxes"])
    pairs = sample(list(near_pairs(charge["boxes"], reach, CIRCUMSCRIBING_SCALE)))
    inter = sum(1 for i, j in pairs
                if touching_scale(charge["boxes"][i], charge["boxes"][j], shield=True) < 1.0)
    out["armoured_pairs"] = len(pairs)
    out["armoured_interpenetrating"] = inter
    out["armoured_fraction"] = inter / max(1, len(pairs))
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
        return solve_charge()
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
    print("=" * 96)
    print(f"{spec['display']}  ({species}) — measured from the shipped surface table")
    print(f"  concept: {'HELICOIDAL twist, ' + str(spec['twist']) + ' deg/step' if spec['twist'] else 'SMOOTH CROSSING CURVES, no twist'}"
          f"   neutral prism {spec['neutral_cross']} x {spec['neutral_step']}")
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

    s = shield_report(reports, species)
    print(f"\n  Charge armour: {s['armoured_interpenetrating']}/{s['armoured_pairs']} "
          f"({s['armoured_fraction']:.1%}) against its siblings' bare {s['sibling_bare']:.1%}")
    print(f"  silhouette: Charge bare {s['charge_bare_area']:,.0f}, armoured "
          f"{s['armoured_area']:,.0f}, siblings bare {s['sibling_bare_area']:,.0f}")

    cap = M.MAX_LIVE_POPULATION
    heaviest = max(r["volume"] for r in reports.values())
    print(f"\n  budget: {M.PRISM_BUDGET} live prisms/plant, cap {cap} plants "
          f"-> {cap * heaviest:,.0f} volume and {cap} always-on heart colliders")
    print("  in NO SpawnProfile — opt-in from the Lifeform Matrix toy, so it costs no "
          "shipped cell anything until somebody puts it in one.")

    if args.render:
        render_all(reports, os.path.join(args.render, species))

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
            print("\nFAIL")
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


def solve_charge():
    """Shrink Charge's ribbon uniformly (its ASPECT is its identity, so uniformly) until
    its armour is no more fused than a sibling is bare."""
    reports = {e: element_report(e, species=species) for e in ("Mass", "Space", "Time")}
    bar = max(r["interpenetrating_fraction"] for r in reports.values())
    print(f"bar: a sibling's bare interpenetration is {bar:.1%}")
    base = M.CROSS_SECTION["Space"]
    for k in (1.0, 0.8, 0.65, 0.55, 0.50, 0.45, 0.40, 0.35, 0.30, 0.25):
        cross = (base[0] * k, base[1] * k)
        r = element_report("Charge", cross=cross, species=species)
        reach = 2 * CIRCUMSCRIBING_SCALE * max(
            math.sqrt(sum(h * h for h in b[2])) for b in r["boxes"])
        pairs = sample(list(near_pairs(r["boxes"], reach, CIRCUMSCRIBING_SCALE)))
        inter = sum(1 for i, j in pairs
                    if touching_scale(r["boxes"][i], r["boxes"][j], shield=True) < 1.0)
        frac = inter / max(1, len(pairs))
        mark = "  <= clears" if frac <= bar else ""
        print(f"  k={k:.2f}  cross=({cross[0]:.4f}, {cross[1]:.4f})  "
              f"armoured {inter}/{len(pairs)} = {frac:.1%}{mark}")
    return 0


def render_all(reports, out_dir):
    import mandelbulb_flora_render as R
    os.makedirs(out_dir, exist_ok=True)
    for element, r in reports.items():
        path = os.path.join(out_dir, f"mandelbulb_{element.lower()}.png")
        R.render(r["boxes"], path)
        print(f"  rendered {path}")


if __name__ == "__main__":
    sys.exit(main())
