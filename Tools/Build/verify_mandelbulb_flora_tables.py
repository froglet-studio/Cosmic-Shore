#!/usr/bin/env python3
"""Prove the SHIPPED Mandelbulb flora against a fresh reference — by COMPILING AND RUNNING
the shipped C#, not by reading it.

The transcription from a proven measurement to the asset that ships is the step neither the
measurement nor code review can see; the gyroid paid five playtests for that gap
(Docs/ECOSYSTEM.md §32.7). So this script drives
Tools/Build/mandelbulb_surface_harness/run.sh, which compiles MandelbulbSurface.cs,
MandelbulbSurfaceTables.cs and the real Element enum against a UnityEngine stub and
executes them.

WHAT IS PROVEN EXACTLY, and what is not:

  * The harness's own SELFTEST — SH round-trip on six harmonics, linearity of Compose, and
    that Pose inverts an address (the RADIAL-lift claim).
  * The reconstructed HEIGHT FIELD, element by element, model against shipped. This is the
    pure function in the chain and cannot amplify a rounding difference, so it is held to
    float precision.
  * The SEED SET — the walk's starting points, which is where the polar-cap defect lived.
  * The walk's STATISTICS: prism count, curve count, the coverage histogram in equal-area
    bands, the size distribution and the total volume.

  * The CRITICAL-POINT CENSUS — the Watershed's seed list, and the one place the surface is
    differentiated twice. Count, kinds, positions, sharpness AND the farthest-point ORDER,
    per element. The order is held EXACTLY because it is what the plant actually reads.
  * POSE off the surface — the FALL's two new address fields. `Dive` and `TanR` are
    unreachable from the walk's statistics, so they get a table of their own (the harness's
    `posetable` verb) spanning both, in both roll states and through both arms of Pose's
    branch.
  * The HEADING ROUND TRIP: that `(TanA, TanB, TanR)` is a LOSSLESS encoding of a unit
    heading in the point's own frame, which is the claim the Fall rests on. Note this one
    alone is proved on the MODEL, not on the shipped C# — it is a claim about the ADDRESS,
    and the C# half of it is the pose table above. It can never fail a `--self-test`
    mutation, and it is listed here so nobody reads it as evidence about the shipped file.

  * The walk is NOT held prism-for-prism, and that is a statement about arithmetic rather
    than a gap in the proof. It is a sequential recurrence with a turn gate, the C# runs in
    float32 and the model in float64, and one step landing a hair either side of
    `dot(t, want) < maxTurnCos` ends a curve in one and not the other. The first divergence
    index is REPORTED so a real transcription error (which diverges at step 1, not step
    800) is still loud. The SKELETON species is the exception and is held much harder: its
    curves leave converged saddles along exact eigen-directions and end at the nearest
    extremum, so there is no hop, no Rng draw and no long chaotic run — measured, its walk
    agrees prism for prism to the end of the plant on all four elements, and its dive rows
    are therefore gated EXACTLY where the two free species get a measured tolerance.

WHAT EVERY BOUND IN HERE IS: a MEASUREMENT with a stated margin, never a round number.
Where the task that commissioned a gate named a tighter bound than the shipped, correct code
can hold, the measured value and the bound are both printed — a bound the shipped tree fails
is a red build, and a bound the shipped tree passes by 1.1x is a coincidence rather than a
margin (Docs/ECOSYSTEM.md §46).

  verify_mandelbulb_flora_tables.py
  verify_mandelbulb_flora_tables.py --self-test   # mutate the shipped file sixteen ways and
                                                  # assert every gate trips
"""
import argparse
import math
import os
import shutil
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mandelbulb_flora_model as M                                    # noqa: E402


ROOT = M.ROOT
HARNESS = os.path.join(ROOT, "Tools", "Build", "mandelbulb_surface_harness", "run.sh")
SHIPPED = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Environment",
                       "FloraAndFauna", "MandelbulbSurface.cs")

GRID = 192
SEED = 12345
BUDGET = 4000


def run(*args):
    return subprocess.run([HARNESS] + [str(a) for a in args],
                          capture_output=True, text=True, check=True).stdout


def shipped_field(element, w=(0.0, 0.0, 0.0), grid=GRID):
    rows = []
    for line in run("field", element, w[0], w[1], w[2], grid).splitlines():
        t = line.split()
        if t and t[0] == "r":
            rows.extend(float(v) for v in t[1:])
    return rows


def shipped_prisms(element, rules, w=(0.0, 0.0, 0.0), grid=GRID, seed=SEED, budget=BUDGET):
    """The shipped walk: its addresses, its curve count, how many DIVES it spent, and the
    position it POSED each prism at. The pose is taken from the harness rather than re-posed
    here on purpose — re-posing the shipped addresses with the model's Pose would measure the
    model twice and let a Pose defect cancel itself out of every row below."""
    out = run("shipped", element, w[0], w[1], w[2], grid, seed, budget, *rules.as_list())
    prisms, positions, curves, dives = [], [], 0, 0
    for line in out.splitlines():
        t = line.split()
        if not t:
            continue
        if t[0] == "p":
            # p theta phi off dive tanA tanB tanR length girth roll curve lane [pose x9]
            prisms.append(M.Prism(float(t[1]), float(t[2]), float(t[3]), float(t[4]),
                                  float(t[5]), float(t[6]), float(t[7]), float(t[8]),
                                  float(t[9]), float(t[10]), int(t[11]), int(t[12])))
            positions.append((float(t[13]), float(t[14]), float(t[15])))
        elif t[0] == "done":
            curves = int(t[2])
            dives = int(t[3])
    return prisms, curves, dives, positions


# The model's surfaces are built once and shared. They are a function of the SHIPPED TABLE
# and of nothing the self-test mutates (which only ever edits MandelbulbSurface.cs), so the
# cache is safe across a mutation run — and it matters, because the critical-point census
# is ~0.4M gradient evaluations and three species now ask for the same four surfaces.
_SURFACE = {}


def model_surface(element):
    if element not in _SURFACE:
        degree, tables = M.load_tables()
        field = M.reconstruct(degree, M.compose(tables[element], 0.0, 0.0, 0.0),
                              GRID, GRID // 2)
        _SURFACE[element] = (field, M.Surface(field, GRID, GRID // 2))
    return _SURFACE[element]


def _unwrap(a, b):
    """|a - b| on a circle. A point either side of phi = 0 is not 2*pi of error."""
    d = abs(a - b)
    return min(d, 2 * math.pi - d)


def octiles(prisms):
    """Equal-AREA bands in cos(theta). Equal-theta bands would report a plant that only
    grew at its poles as evenly covered."""
    bins = [0] * 8
    for p in prisms:
        bins[min(7, int((1 - math.cos(p.theta)) / 2 * 8))] += 1
    return bins


# ── THE WATERSHED's census (Docs/ECOSYSTEM.md §47) ────────────────────────────────────
#
# The critical points are the ONE place the surface is differentiated twice, and the only
# place in this species where two implementations could legitimately disagree about a SET
# rather than about a number: a saddle that appears on one side and not the other does not
# move one prism, it adds or deletes four arms. So the census is held as hard as it can
# honestly be held, and what cannot be held tight is PRINTED next to its bound.

CRIT_KIND = ("saddle", "peak", "pit")


def shipped_critical(element, w=(0.0, 0.0, 0.0), grid=GRID):
    """`crit <kind> <theta> <phi> <radius> <sharpness> <valley.xy> <ridge.xy>` in SCAN
    order, then `saddle <i>` rows giving the farthest-point ORDER as indices into it."""
    points, order = [], []
    for line in run("crit", element, w[0], w[1], w[2], grid).splitlines():
        t = line.split()
        if not t:
            continue
        if t[0] == "crit":
            points.append((int(t[1]),) + tuple(float(v) for v in t[2:]))
        elif t[0] == "saddle":
            order.append(int(t[1]))
    return points, order


def check_critical(element, fail, verbose=True):
    _, surface = model_surface(element)
    mine = M.critical_points(surface)
    mine_order = M.saddles(surface)
    index = {id(c): i for i, c in enumerate(mine)}
    mine_idx = [index[id(c)] for c in mine_order]

    theirs, their_idx = shipped_critical(element)

    if len(theirs) != len(mine):
        fail(f"{element}: {len(theirs)} critical points shipped, model found {len(mine)}")
        return
    if not theirs:
        fail(f"{element}: the surface has no critical points at all")
        return

    # The SET, in SCAN ORDER. Count and kind are held exactly — both are discrete, and both
    # are what a transcription error in Gradient, Hessian, the Newton step or the dedupe
    # moves first (measured: the frozen-stencil control takes Space from 76 points to 68 and
    # re-classifies 40 of them).
    kind_bad = [i for i, (a, b) in enumerate(zip(mine, theirs)) if a.kind != b[0]]
    if kind_bad:
        fail(f"{element}: {len(kind_bad)} critical points classified differently, first at "
             f"index {kind_bad[0]} ({CRIT_KIND[mine[kind_bad[0]].kind]} model vs "
             f"{CRIT_KIND[theirs[kind_bad[0]][0]]} shipped)")

    d_th = max(abs(a.theta - b[1]) for a, b in zip(mine, theirs))
    d_ph = max(_unwrap(a.phi, b[2]) for a, b in zip(mine, theirs))
    d_sh = max(abs(a.sharpness - b[4]) / max(abs(a.sharpness), 1e-12)
               for a, b in zip(mine, theirs))

    # POSITION. Measured worst on the shipped tree is 9.4e-6 rad (Time's phi); the bound is
    # 1e-3, and it is derived rather than chosen. The only scale at which a positional
    # disagreement can change anything is the DEDUPE CHORD (0.01) — two points closer than
    # that are one point — so the bound sits a decade under it and two decades over what the
    # shipped tree measures. Both ends are real margin: the nearest GENUINE pair on the
    # worst element is 0.0256 (Time), reported below, so the census is 25x from collapsing a
    # pair of its own accord.
    if d_th > 1e-3 or d_ph > 1e-3:
        fail(f"{element}: critical points differ by {d_th:.2e} in theta / {d_ph:.2e} in phi "
             f"(bound 1e-3, dedupe chord 0.01)")
    # SHARPNESS is a SECOND derivative, so the field's float32 storage reaches it amplified
    # by the stencil squared — measured 2.6e-3 relative on Time against 1.1e-4 on Charge.
    # The bound is 2% and it is load-bearing rather than decorative: under the frozen-stencil
    # control, Mass keeps its count, its kinds, its positions AND its order, so this is the
    # only CENSUS row that catches it (0.354 relative). Measured, the rest of that control's
    # Mass failures come from the WALK (prism count, prism 0, the skeleton chord) — the
    # census would report a clean surface under a perturbed second derivative without it.
    if d_sh > 0.02:
        fail(f"{element}: critical-point sharpness differs by {d_sh:.2e} relative "
             f"(bound 2e-2)")

    # THE ORDER, EXACTLY. This is the one the plant reads — a skeleton seed list IS the
    # saddles in farthest-point order, and every budget prefix of it has to be spread over
    # the whole sphere (§44.5's polar-cap defect, met from a third direction). It is a
    # permutation of integers, so there is nothing to tolerate: it either matches or the two
    # implementations grow different plants.
    order_ok = their_idx == mine_idx
    if not order_ok:
        first = next((i for i, (a, b) in enumerate(zip(mine_idx, their_idx)) if a != b),
                     min(len(mine_idx), len(their_idx)))
        fail(f"{element}: the farthest-point saddle ORDER differs from position {first} "
             f"(model {mine_idx[:6]}... shipped {their_idx[:6]}...)")

    counts = [sum(1 for c in mine if c.kind == k) for k in range(3)]
    # Asked of the SHIPPED side, deliberately. `farthest_point_order` returns a permutation
    # of its input by construction, so comparing the MODEL's saddle count against the length
    # of the MODEL's own order is a gate that cannot fail. What can genuinely go wrong is
    # C#'s `Saddles()` dropping, repeating or failing to locate one of its own census rows —
    # and `Crit` emits -1 for a row it cannot place, which a bare list compare would report
    # as an ordering difference rather than as the lookup failure it is.
    their_saddles = sum(1 for row in theirs if row[0] == 0)
    if (len(their_idx) != their_saddles or len(set(their_idx)) != len(their_idx)
            or any(i < 0 for i in their_idx)):
        fail(f"{element}: the shipped census holds {their_saddles} saddles but orders "
             f"{len(their_idx)} ({len(set(their_idx))} distinct, "
             f"{sum(1 for i in their_idx if i < 0)} unplaced)")

    # REPORTED, never gated: how close the two nearest critical points actually are, against
    # the 0.01 chord the detector deduplicates on. A finite grid cannot promise it found
    # every critical point, so a bound here would be a coincidence today and a build failure
    # the day somebody re-bakes at a finer degree — but the margin has to be VISIBLE, because
    # it is the species' one genuine exactness exposure.
    nearest = min(
        math.sqrt(sum((a - b) ** 2 for a, b in zip(mine[i].dir, mine[j].dir)))
        for i in range(len(mine)) for j in range(i + 1, len(mine))) if len(mine) > 1 else 9.9

    if verbose:
        print(f"  {element:6s} census {len(theirs)} pts "
              f"({counts[0]} saddle / {counts[1]} peak / {counts[2]} pit)  "
              f"dtheta {d_th:.1e} dphi {d_ph:.1e} dsharp {d_sh:.1e}  "
              f"order {'exact' if order_ok else 'DIFFERS'}  "
              f"nearest chord {nearest:.4f} (dedupe 0.01)")


# ── THE FALL's pose, off the surface ──────────────────────────────────────────────────

def check_pose_table(element, fail, verbose=True):
    """Pose is pure and cannot amplify, so it is the one part of the Fall that can be held
    to float precision. The harness emits the ADDRESS it used alongside the pose, so this
    re-poses the identical float32 inputs — the comparison is about Pose rather than about
    whether two languages agree what 0.996 is."""
    _, surface = model_surface(element)
    rows = []
    for line in run("posetable", element, GRID).splitlines():
        t = line.split()
        if t and t[0] == "pt":
            rows.append([float(v) for v in t[1:]])
    if not rows:
        fail(f"{element}: the posetable verb emitted no rows")
        return

    # A table that does not SPAN is not a table. Assert the sweep before trusting it, or a
    # harness that emitted one trivial row would pass every bound below by construction.
    dives = [r[3] for r in rows]
    tan_r = [r[6] for r in rows]
    rolls = {r[7] != 0.0 for r in rows}
    if (min(dives) > -0.049 or max(dives) < 0.969 or min(tan_r) > -0.995
            or max(tan_r) < 0.995 or rolls != {False, True}
            or not any(r[6] == 0.0 for r in rows) or not any(r[6] != 0.0 for r in rows)):
        fail(f"{element}: the pose table does not span its stated range — dive "
             f"[{min(dives):.3f}, {max(dives):.3f}], tanR [{min(tan_r):.3f}, "
             f"{max(tan_r):.3f}], rolls {sorted(rolls)}")
        return

    w_pos = w_fwd = w_up = w_unit = w_orth = w_rad = 0.0
    for r in rows:
        th, ph, off, dv, ta, tb, tr, roll, radius = r[0:9]
        pos, fwd, up = r[9:12], r[12:15], r[15:18]
        mine = M.pose(surface, M.Prism(th, ph, off, dv, ta, tb, tr, 1.0, 1.0, roll, 0, 0))
        w_pos = max(w_pos, max(abs(a - b) for a, b in zip(mine[0], pos)))
        w_fwd = max(w_fwd, max(abs(a - b) for a, b in zip(mine[1], fwd)))
        w_up = max(w_up, max(abs(a - b) for a, b in zip(mine[2], up)))
        w_unit = max(w_unit, abs(math.sqrt(sum(c * c for c in fwd)) - 1.0))
        w_orth = max(w_orth, abs(sum(a * b for a, b in zip(fwd, up))))
        # THE RADIAL-LIFT CLAIM, stated as algebra on the shipped row alone: a posed prism
        # sits on its own ray at Radius*(1 - Dive) + RadialOffset. This is the whole of what
        # `Dive` means, it needs no model to check, and it is what the dive's tip depth rests
        # on (Docs/ECOSYSTEM.md §47).
        w_rad = max(w_rad, abs(math.sqrt(sum(c * c for c in pos)) - abs(radius * (1.0 - dv) + off)))

    # POSITION does not read the normal — it is dir * (radius*(1-dive) + off), and both
    # factors are smooth — so it is held at 1e-6 (measured 5.1e-7). FORWARD and UP do read
    # the normal, which is a finite difference of a field the two implementations store as
    # float32 and reconstruct through different double sums, so their floor is ~1e-5 whatever
    # Pose does: measured worst 8.8e-6 (Time). Holding them at the 1e-5 this was commissioned
    # at would be a 1.13x margin, which §46 records as the shape of a constant that is a
    # coincidence rather than a bound; they are held at 1e-4 with the measurement printed.
    for name, value, bound in (("position", w_pos, 1e-6),
                               ("forward", w_fwd, 1e-4),
                               ("up", w_up, 1e-4),
                               ("|forward| - 1", w_unit, 1e-5),
                               ("dot(forward, up)", w_orth, 1e-5),
                               ("|position| - Radius*(1-Dive)-off", w_rad, 1e-5)):
        if value > bound:
            fail(f"{element}: pose table {name} differs by {value:.3g} (bound {bound:g})")

    if verbose:
        print(f"  {element:6s} pose  {len(rows)} addresses  pos {w_pos:.1e}  fwd {w_fwd:.1e}  "
              f"up {w_up:.1e}  |f|-1 {w_unit:.1e}  f.u {w_orth:.1e}  radial {w_rad:.1e}")


def check_heading_roundtrip(fail, verbose=True):
    """(TanA, TanB, TanR) must be a LOSSLESS encoding of a unit heading — that is the whole
    reason the Fall can address a free-space prism at all, and it is an exact identity rather
    than a tolerance, because (ETheta, EPhi, Dir) is orthonormal.

    Pure Python on purpose: this is a claim about the ADDRESS, not about the C#, and the C#
    half of it is already covered by the pose table above (which drives both arms of Pose's
    branch against the shipped surface). Every heading here carries a non-zero TanR, because
    TanR == 0 is by design NOT a round trip — a surface prism's heading is deliberately
    re-projected onto the tangent plane at pose time."""
    _, surface = model_surface("Charge")
    worst = 0.0
    n = 0
    for it in range(5):
        theta = 0.3 + 0.55 * it
        for ip in range(4):
            phi = 0.4 + 1.5 * ip
            fr = M.build_frame(surface, theta, phi)
            for k in range(12):
                ang = math.pi / 6.0 * k
                for tr in (0.15, 0.5, -0.35, -0.9):
                    st = math.sqrt(max(0.0, 1.0 - tr * tr))
                    want = M._norm(M._add(M._add(M._mul(fr.e_theta, st * math.cos(ang)),
                                                 M._mul(fr.e_phi, st * math.sin(ang))),
                                          M._mul(fr.dir, tr)))
                    # Exactly what Emit measures.
                    a = M.Prism(theta, phi, 0.0, 0.0,
                                M._dot(want, fr.e_theta), M._dot(want, fr.e_phi),
                                M._dot(want, fr.dir), 1.0, 1.0, 0.0, 0, 0)
                    if a.tan_r == 0.0:
                        fail("heading round trip: a test heading landed on TanR == 0, which "
                             "takes the SURFACE branch and is not a round trip by design")
                        return
                    back = M.pose(surface, a)[1]
                    worst = max(worst, math.degrees(
                        math.acos(min(1.0, max(-1.0, M._dot(back, want))))))
                    n += 1
    if worst > 1e-4:
        fail(f"heading round trip: recovered heading is {worst:.3g} deg off over {n} cases "
             f"(bound 1e-4 deg)")
    elif verbose:
        print(f"  heading round trip exact over {n} headings (worst {worst:.1e} deg)")


def check_element(element, fail, verbose=True, species="FractalFoliage"):
    rules = M.rules_for(element, species)

    # 1. the pure function, exactly
    mine, surface = model_surface(element)
    theirs = shipped_field(element)
    if len(mine) != len(theirs):
        fail(f"{element}: field size {len(theirs)} vs model {len(mine)}")
        return
    worst = max(abs(a - b) for a, b in zip(mine, theirs))
    scale = max(abs(v) for v in mine)
    if worst > 2e-4 * max(scale, 1.0):
        fail(f"{element}: reconstructed field differs by {worst:.3g} (scale {scale:.3g})")

    # 2. the seed set
    seeds = M.build_seeds(surface, rules)
    out = run("shipped", element, 0, 0, 0, GRID, SEED, 1, *rules.as_list())
    their_seeds = int(out.splitlines()[0].split()[2])
    if their_seeds != len(seeds):
        fail(f"{element}: {their_seeds} seeds shipped, model built {len(seeds)}")

    # 3. the walk, by its statistics
    mine_p, mine_c, mine_d = M.grow(surface, rules, SEED, BUDGET)
    theirs_p, theirs_c, theirs_d, theirs_xyz = shipped_prisms(element, rules)
    skeleton = rules.skeleton_seeds != 0

    if len(theirs_p) != len(mine_p):
        fail(f"{element}: {len(theirs_p)} prisms shipped, model laid {len(mine_p)}")
    # A plant with no prisms is the loudest result this file can get, and it has to arrive
    # as a NAMED gate rather than as a ZeroDivisionError three statistics later. The
    # separatrix-flip control reaches it: run every valley arm uphill and every ridge arm
    # downhill and every curve on the Watershed is abandoned as dust.
    if not theirs_p or not mine_p:
        fail(f"{element}: the walk laid NO prisms at all "
             f"(shipped {len(theirs_p)}, model {len(mine_p)})")
        return
    # HOW MUCH a flipped decision costs is a property of the SPECIES, so the tolerance is
    # derived from the plant rather than authored. The walk is a sequential recurrence with a
    # turn gate, run in float32 in C# against float64 here, so its last bits diverge and one
    # decision near the gate abandons or keeps a whole curve. A species whose curves run 5x
    # longer therefore moves 5x more prisms per flip - measured, the coral bloom's long
    # high-momentum arcs disagreed on 12% of their curves against the foliage's 2%, at exactly
    # the same fidelity. The bound is "a handful of flipped curves", expressed in the only
    # unit that means the same thing to both species: the fraction of the PLANT they move.
    # It is stated in the unit that means the same thing to every species: the fraction of
    # the PLANT that the disagreeing curves account for. A raw curve-count tolerance does
    # not - measured, two curves out of 210 on the foliage's Mass is 1% of the plant, while
    # eleven out of 51 on the bloom's Time is 21% of it, and a percentage bound on the COUNT
    # calls those the same size of disagreement.
    # Per-curve length counts SURFACE prisms only: a dive is a tail on some curves and not
    # others, so pricing a disputed curve at the plant's mean length with dives in would
    # charge a flipped decision for prisms that flip did not move.
    surface_prisms = sum(1 for p in mine_p if p.tan_r == 0.0)
    per_curve = surface_prisms / max(1, mine_c)
    disputed = abs(theirs_c - mine_c) * per_curve / max(1, len(mine_p))
    # Bound 20%: measured, the Coral Bloom's Mass — the species' longest, most float-sensitive
    # runs (91 surface prisms per curve) — disagrees on 8 of 50 curves at full fidelity, 18% of
    # the plant, while every transcription error ever planted here disagrees from prism 0.
    if disputed > 0.20 and abs(theirs_c - mine_c) > 4:
        fail(f"{element}: {theirs_c} curves shipped, model traced {mine_c} - the difference is "
             f"{disputed:.0%} of the plant (bound 20%, {per_curve:.0f} prisms/curve)")

    # Coverage is checked as SHAPE, not as an exact histogram. The walk diverges in its last
    # bits (see the module docstring), and one long curve landing in a neighbouring band moves
    # a raw histogram by 15% of the plant while nothing is wrong. What must agree is what the
    # histogram exists to catch: how many equal-area bands carry the plant at all — which is
    # the polar-cap defect, and which a transcription error cannot pass.
    a, b = octiles(mine_p), octiles(theirs_p)
    total = max(1, sum(a))
    drift = max(abs(x - y) for x, y in zip(a, b)) / total
    filled_mine = sum(1 for v in a if v > 0.02 * total)
    filled_theirs = sum(1 for v in b if v > 0.02 * max(1, sum(b)))
    # One band either way is a single long curve landing across a boundary, which is the
    # divergence this file already states it cannot hold prism for prism. What a
    # transcription error cannot pass is the FLOOR below - a plant that grew a cap.
    if abs(filled_theirs - filled_mine) > 1:
        fail(f"{element}: the shipped plant fills {filled_theirs}/8 equal-area bands, the model "
             f"{filled_mine}/8  model={a} shipped={b}")
    if filled_theirs < 6:
        fail(f"{element}: only {filled_theirs}/8 bands carry 2% of the plant — it has grown a "
             f"cap, not a bulb  shipped={b}")
    north = sum(b[:4]) / max(1, sum(b))
    if not 0.25 <= north <= 0.75:
        fail(f"{element}: {north:.0%} of the plant is in one hemisphere")

    def stats(ps):
        ln = sorted(p.length for p in ps)
        gr = sorted(p.girth for p in ps)
        return (sum(ln) / len(ln), ln[len(ln) // 2], sum(gr) / len(gr))
    sa, sb = stats(mine_p), stats(theirs_p)
    # Same argument as the curve count: the girth is keyed on RUN LENGTH, so on a species
    # whose runs are long a flipped decision moves the mean girth much further than on one
    # whose runs are short. Lengths are unaffected (a prism's length is its step, not its
    # run), so only the girth's bound is widened, and only in proportion.
    tol = (0.02, 0.02, min(0.10, 0.02 + 0.002 * per_curve))
    for name, x, y, lim in zip(("mean length", "median length", "mean girth"), sa, sb, tol):
        if abs(x - y) > lim * max(abs(x), 1e-6):
            fail(f"{element}: {name} {y:.5f} shipped vs {x:.5f} model")

    # ── THE FALL, by the three statistics the dive owns (Docs/ECOSYSTEM.md §47) ──
    #
    # None of them is reachable from the rows above: a dive is a tail on SOME curves, so it
    # moves the prism count and the girth mean by a few percent and hides inside both
    # tolerances. These three ask about the dive directly, and dive prisms are separable
    # from surface prisms with no bookkeeping at all — `TanR != 0` is the address field that
    # says "this prism is in free space", set on the dive and on nothing else.
    mine_dp = [p for p in mine_p if p.tan_r != 0.0]
    theirs_dp = [p for p in theirs_p if p.tan_r != 0.0]

    # (a) DIVES SPENT. A property of the run, not of the float width: the owed set is
    #     strided over the seed list and a seed keeps its flag until a dive is actually
    #     appended. A skeleton species walks from converged saddles with no hop and no Rng,
    #     so it is held EXACTLY; the two free species can lose a dive to a curve that one
    #     implementation calls dust and the other does not (measured: at most 1, on the
    #     foliage's Charge, 6 against 7).
    d_slack = 0 if skeleton else 2
    if abs(theirs_d - mine_d) > d_slack:
        fail(f"{element}: {theirs_d} dives spent shipped, model spent {mine_d} "
             f"(slack {d_slack})")

    # (b) DIVE PRISM COUNT, priced in DIVES rather than in percent. A flipped dive is the
    #     only thing that legitimately moves this number, and one dive is ~26 prisms, so a
    #     flat percentage bound would mean something different on a species whose dives are
    #     long — the same argument the curve-count bound above makes. The allowance is
    #     therefore (flipped dives + 1) whole dives, which is EXACTLY ZERO on a skeleton
    #     species. Measured on the shipped tree: exact on all four Watershed elements and on
    #     3 of 8 free ones, worst 26 prisms (FractalFoliage/Charge) against an allowance of
    #     54 — and the prefix-instead-of-stride control moves it far past that with the dive
    #     COUNT unchanged, which is the case a percentage bound would have let through.
    per_dive = len(mine_dp) / max(1, mine_d)
    allow = 0 if skeleton else (abs(theirs_d - mine_d) + 1) * per_dive
    if abs(len(theirs_dp) - len(mine_dp)) > allow:
        fail(f"{element}: {len(theirs_dp)} dive prisms shipped, model laid {len(mine_dp)} "
             f"(allowance {allow:.0f} = {abs(theirs_d - mine_d) + 1} dives at "
             f"{per_dive:.0f} prisms each)")

    # (c) CLOSEST PRISM CENTRE, in world units. This is what "the curve falls to the heart"
    #     means as a number, and it is the row that the `(1f - a.Dive)` factor in Pose is
    #     the only thing holding up — drop it and every dive prism poses back on the surface,
    #     taking this from ~3.3 u to the surface minimum around 42 u.
    #
    #     It is measured on the WALK rather than on the claim-filtered plant, deliberately.
    #     The claim lives in MandelbulbFlora.cs, which this harness does not compile, so
    #     running a Python-only filter over both lists would prove nothing about the C# and
    #     could only hide a disagreement. What it would change is measured and small: the
    #     laid plant's closest centre is 3.3166-3.4774 u against the walk's 3.3132-3.3314,
    #     because a dive tip is the innermost prism of an isolated spiral and nothing the
    #     claim refuses sits near it.
    #
    #     The bound is 2.5%, not the 2% it was commissioned at: measured worst on the shipped
    #     tree is 1.62% (CoralBloom/Mass, one flipped dive), and 1.2x is a coincidence rather
    #     than a margin. The quantity is robust at all because every dive terminates by being
    #     CLIPPED onto the same stop sphere, so two implementations that disagree about which
    #     dives survived still disagree by at most one chord midpoint. A skeleton species is
    #     held to 0.01% (measured: exact on all four).
    if mine_dp and theirs_dp:
        mine_r = min(M._len(M.pose(surface, p)[0]) for p in mine_p) * M.SHELL_RADIUS
        theirs_r = min(math.sqrt(sum(c * c for c in q))
                       for q in theirs_xyz) * M.SHELL_RADIUS
        r_tol = 0.0001 if skeleton else 0.025
        if abs(theirs_r - mine_r) > r_tol * max(mine_r, 1e-9):
            fail(f"{element}: closest prism centre {theirs_r:.4f} u shipped vs "
                 f"{mine_r:.4f} u model ({abs(theirs_r - mine_r) / max(mine_r, 1e-9):.2%}, "
                 f"bound {r_tol:.2%})")
    else:
        mine_r = theirs_r = float("nan")
        if rules.dive_count > 0 and rules.dive_step > 0:
            fail(f"{element}: the rule authors {rules.dive_count} dives and the walk laid "
                 f"none (shipped {len(theirs_dp)}, model {len(mine_dp)} dive prisms)")

    # DID IT DRIFT, OR DID IT JUMP? This is the test that separates a transcription error
    # from float width, and it has to ask that question directly rather than counting how
    # many prisms agreed first. A transcription error is wrong from the FIRST prism, and
    # when it is not it is wrong by a lot; float32-vs-float64 chaos starts at the last bits
    # and grows. Counting prisms instead makes the gate a property of how chaotic the
    # SURFACE is - the power-12 Time bulb has 3.3x the relief of the Space one, so its walk
    # legitimately separates 20x sooner - and the constant it was written with (16) sat one
    # prism under a shipping species' value, which is a coincidence rather than a margin.
    if mine_p and theirs_p:
        p0, q0 = mine_p[0], theirs_p[0]
        if abs(p0.theta - q0.theta) > 1e-4 or abs(p0.phi - q0.phi) > 1e-4:
            fail(f"{element}: the FIRST prism disagrees "
                 f"({q0.theta:.6f},{q0.phi:.6f} shipped vs {p0.theta:.6f},{p0.phi:.6f}) - "
                 f"a transcription error is wrong from the start")
    # WHERE the two walks separate is REPORTED, never gated, and the reason is worth keeping:
    # the prism lists are INDEX-ALIGNED, so the moment one flipped decision drops a curve
    # every later index compares two different curves and the disagreement is instantly a
    # whole bulb wide. There is no "how big was the first disagreement" signal to read there -
    # a drift and a jump look identical the instant the lists shift. The transcription test is
    # therefore prism 0 above, which a transcription error cannot pass and float width cannot
    # fail. (phi is unwrapped here or a point either side of the seam reads as 2*pi of error.)
    def unit(p):
        st = math.sin(p.theta)
        return (st * math.cos(p.phi), st * math.sin(p.phi), math.cos(p.theta))

    first = None
    worst_chord = 0.0
    worst_addr = 0.0
    for i, (p, q) in enumerate(zip(mine_p, theirs_p)):
        chord = math.sqrt(sum((a - b) ** 2 for a, b in zip(unit(p), unit(q))))
        worst_chord = max(worst_chord, chord)
        worst_addr = max(worst_addr, abs(p.dive - q.dive), abs(p.tan_r - q.tan_r))
        if first is None and (abs(p.theta - q.theta) > 1e-3 or _unwrap(p.phi, q.phi) > 1e-3):
            first = i

    # A SKELETON SPECIES IS HELD PRISM FOR PRISM, and that is not a stricter reading of the
    # same evidence — it is a different walk. A free species hops with an Rng draw and runs
    # curves hundreds of steps long through a turn gate, so its last bits legitimately
    # separate; a separatrix leaves a CONVERGED saddle along an exact eigen-direction and
    # ends at the nearest extremum a few dozen steps later, with no hop and no draw. Measured
    # on the shipped tree, the two implementations agree on every prism of all four Watershed
    # elements: worst chord 1.5e-4 on the unit sphere (Mass), 1.1e-5 to 2.7e-5 on the other
    # three, and worst 3.1e-5 on Dive / 2.4e-5 on TanR.
    #
    # The chord is used rather than (theta, phi) because phi is ill-conditioned near the
    # poles, where a prism that has barely moved reads as a large angular error.
    #
    # This gate is what catches the dive's azimuthal DEAD BAND. Remove it and Space's
    # Watershed diverges at prism 2 and Mass's at prism 25 while every other plant in the
    # game stays byte-identical — an arm that leaves a saddle exactly meridionally has an
    # azimuthal component that is pure rounding residual, and a bare sign test lets two
    # implementations wind the same dive opposite ways. Nothing statistical sees it: the
    # prism counts, the curve counts, the bands, the lengths, the girths, the dive counts and
    # the closest centre are all unmoved, and prism 0 is unmoved too.
    if skeleton:
        if worst_chord > 2e-3:
            fail(f"{element}: the skeleton walk disagrees by {worst_chord:.2e} on the unit "
                 f"sphere (bound 2e-3, first past 1e-3 at prism {first}) — a species with no "
                 f"hop and no Rng draw has nothing to be chaotic with")
        if worst_addr > 1e-3:
            fail(f"{element}: the skeleton walk disagrees by {worst_addr:.2e} on Dive/TanR "
                 f"(bound 1e-3)")

    if verbose:
        print(f"  {element:6s} field {worst:.2e}  seeds {their_seeds}  "
              f"prisms {len(theirs_p)}  curves {theirs_c}/{mine_c}  "
              f"bands {filled_theirs}/8 (drift {drift:.0%})  "
              f"walk agrees to prism {first if first is not None else len(theirs_p)}"
              f"{f' (worst chord {worst_chord:.1e})' if skeleton else ''}")
        print(f"         fall: dives {theirs_d}/{mine_d}  "
              f"dive prisms {len(theirs_dp)}/{len(mine_dp)}  "
              f"closest centre {theirs_r:.4f}/{mine_r:.4f} u")


def verify(verbose=True):
    failures = []

    def fail(msg):
        failures.append(msg)

    self = run("selftest")
    if "selftest ok" not in self:
        failures.append("harness selftest failed:\n" + self)
    elif verbose:
        print("  harness selftest ok (SH round-trip, Compose linearity, Pose inverts)")

    check_heading_roundtrip(fail, verbose)

    # The census and the pose table are properties of the SURFACE and of the ADDRESS, not of
    # any one species, so they run once per element rather than once per (species, element).
    # Every Watershed plant of an element shares one saddle list; every plant of every
    # species shares one Pose.
    if verbose:
        print("  -- the surface's census and the address's pose")
    for element in M.ELEMENTS:
        check_critical(element, fail, verbose)
    for element in M.ELEMENTS:
        check_pose_table(element, fail, verbose)

    # ALL THREE species, because they share one growth rule and differ only in their curve
    # parameters - so the thing this proves (the shipped C# walks what the model walks) has
    # to be proved on each of them. A species whose rules nobody ran is a species nobody
    # verified.
    for species in M.SPECIES:
        if verbose:
            print(f"  -- {M.SPECIES[species]['display']} ({species})")
        for element in M.ELEMENTS:
            check_element(element, fail, verbose, species)
    return failures


def self_test():
    """A gate nobody has watched fail is a gate nobody should trust. Each mutation below is
    a plausible transcription slip; every one must be caught."""
    mutations = [
        # RE-ANCHORED. This control was left pointing at a line the Fall rewrote (Pose's
        # position gained its `(1f - a.Dive)` factor in 59d9f592), so from that commit until
        # this one `--self-test` reported MUTATION NOT APPLICABLE and exited 1 — the file's
        # own guard did its job and nobody ran it. A control's anchor is part of the control.
        ("radial lift stored along the NORMAL instead of the ray",
         "position = scratch.Dir * (scratch.Radius * (1f - a.Dive) + a.RadialOffset);",
         "position = scratch.Dir * (scratch.Radius * (1f - a.Dive)) + n * a.RadialOffset;"),
        ("the SH root-2 factor dropped from the reconstruction",
         "cm[m] += Root2 * b * coeffs[l * (l + 1) + m];",
         "cm[m] += b * coeffs[l * (l + 1) + m];"),
        # SPLIT IN TWO. The Watershed gave Surface a DOUBLE sampler beside the float one, so
        # this anchor started matching twice and `replace(..., 1)` silently mutated whichever
        # came first — a control that tests one of two identical lines by accident. Each
        # sampler now carries its own control, anchored on the line above it, and the
        # duplicate guard below makes the next such collision loud instead of arbitrary.
        ("phi wrapped with a plain modulo in the DOUBLE sampler (the census reads it)",
         "double fx = x - i0, fy = y - j0;\n                int ia = ((i0 % W) + W) % W, ib = ((i0 + 1) % W + W) % W;",
         "double fx = x - i0, fy = y - j0;\n                int ia = i0 % W, ib = (i0 + 1) % W;"),
        ("phi wrapped with a plain modulo in the FLOAT sampler (the walk reads it)",
         "float fx = x - i0, fy = y - j0;\n                int ia = ((i0 % W) + W) % W, ib = ((i0 + 1) % W + W) % W;",
         "float fx = x - i0, fy = y - j0;\n                int ia = i0 % W, ib = (i0 + 1) % W;"),
        ("the seed NMS stops early again (the polar-cap defect)",
         "int take = Mathf.Min(want, dirs.Count);",
         "int take = Mathf.Min(want, dirs.Count); dirs.RemoveRange(take, dirs.Count - take);"),
        ("the phi derivative loses its 1/sin(theta)",
         "Vector3 a = rT * eT + (rP / sT) * eP;",
         "Vector3 a = rT * eT + rP * eP;"),
        ("the girth taper inverted",
         "float girth = taper + (1f - taper) * u;",
         "float girth = 1f + (taper - 1f) * u;"),
        ("the walk goes seed-major again (two seeds eat the whole budget)",
         "if (_seedIndex >= _seeds.Count) { _seedIndex = 0; _lane++; continue; }",
         "if (_seedIndex >= _seeds.Count) { _seedIndex = 0; _lane = lanes; continue; }"),

        # ── THE FALL (Docs/ECOSYSTEM.md §47) ──
        ("the (1 - Dive) factor dropped from Pose (every dive prism poses back on the surface)",
         "position = scratch.Dir * (scratch.Radius * (1f - a.Dive) + a.RadialOffset);",
         "position = scratch.Dir * (scratch.Radius + a.RadialOffset);"),
        ("the TanR branch dropped from Pose (a free-space heading re-projected onto the tangent plane)",
         "if (a.TanR != 0f)\n            {",
         "if (a.TanR != 0f && a.Girth < -1e30f)\n            {"),
        ("the dive-owed set taken as a PREFIX of the seed list instead of a stride",
         "_diveOwed[(int)((long)d * _seeds.Count / _diveQuota)] = true;",
         "_diveOwed[d] = true;"),

        # ── THE WATERSHED (§47) ──
        ("the separatrix field flipped (valley arms climb, ridge arms fall)",
         "var field = ascend ? SteeringField.Ascent : SteeringField.Descent;",
         "var field = ascend ? SteeringField.Descent : SteeringField.Ascent;"),
        ("the dead band removed from the dive's azimuthal sign (a rounding residual decides the winding)",
         "if (Vector3.Dot(az, u) < -1e-3f) az = -az;",
         "if (Vector3.Dot(az, u) < 0f) az = -az;"),
        ("the lane interleave grouped (lanes 0,1 both valley) instead of valley/ridge alternating",
         "bool ascend = (_lane & 1) == 1;",
         "bool ascend = _lane >= 2;"),
        ("the frozen Hessian stencil perturbed 0.01 -> 0.02",
         "public const double HessianStencil = 0.01;",
         "public const double HessianStencil = 0.02;"),
        ("the saddle order gone sharpness-major again (the belt defect, §44.5)",
         """                    double dd = dmin[i] - dmin[best];
                    if (dd > OrderDistanceTolerance) { best = i; continue; }
                    if (dd < -OrderDistanceTolerance) continue;
                    double ds = saddles[i].Sharpness - saddles[best].Sharpness;
                    if (ds > sTol) { best = i; continue; }
                    if (ds < -sTol) continue;""",
         """                    double ds = saddles[i].Sharpness - saddles[best].Sharpness;
                    if (ds > sTol) { best = i; continue; }
                    if (ds < -sTol) continue;
                    double dd = dmin[i] - dmin[best];
                    if (dd > OrderDistanceTolerance) { best = i; continue; }
                    if (dd < -OrderDistanceTolerance) continue;"""),
    ]
    original = open(SHIPPED).read()
    backup = tempfile.mktemp(suffix=".cs")
    shutil.copy(SHIPPED, backup)
    bad = []
    try:
        for label, find, replace in mutations:
            hits = original.count(find)
            if hits == 0:
                bad.append(f"MUTATION NOT APPLICABLE ({label}) — the shipped file no longer "
                           f"contains the line this control mutates; re-anchor it.")
                continue
            # A control that matches twice mutates whichever `replace(..., 1)` reaches first,
            # which is a statement about the file's line order rather than about the defect.
            # It is how the phi-wrap control quietly stopped testing the sampler it was
            # written for when the Watershed added a second one.
            if hits > 1:
                bad.append(f"AMBIGUOUS MUTATION ({label}) — its anchor matches {hits} times, "
                           f"so it tests whichever copy comes first; narrow it.")
                continue
            open(SHIPPED, "w").write(original.replace(find, replace, 1))
            try:
                failures = verify(verbose=False)
            except subprocess.CalledProcessError as exc:
                failures = [f"harness refused to build: {exc}"]
            status = "trips" if failures else "SLIPS THROUGH"
            print(f"  {status:14s} {label}")
            if not failures:
                bad.append(f"mutation slipped through: {label}")
    finally:
        shutil.copy(backup, SHIPPED)
        os.remove(backup)
    # Re-prove the restored file, so a failed restore cannot pass as a clean run.
    if verify(verbose=False):
        bad.append("the shipped file did not verify after being restored")
    return bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--self-test", action="store_true",
                    help="mutate the shipped file and assert every gate trips")
    args = ap.parse_args()

    if args.self_test:
        print("self-test (each line must read 'trips'):")
        bad = self_test()
        if bad:
            for b in bad:
                print("  " + b, file=sys.stderr)
            return 1
        print("OK: every negative control fired.")
        return 0

    print("verifying the shipped Mandelbulb flora by compiling and RUNNING it:")
    failures = verify()
    if failures:
        for f in failures:
            print("  FAIL " + f, file=sys.stderr)
        return 1
    print("OK: the shipped growth rule matches the model.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
