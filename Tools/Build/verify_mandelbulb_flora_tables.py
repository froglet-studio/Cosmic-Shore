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

  * APOLLONIA's GASKET, split into a half held exactly and a half held statistically — and
    the split is arithmetic, not convenience. LEVEL 0 is closed form (a peak of the census,
    given half the angle to its nearest neighbour) and is held to 1e-6 on rho and 1e-5 on the
    axis; `RingSamplesFor` and `rho_ref` are held exactly, and the lay order's level-0
    subsequence is held exactly in its CANONICAL form (see the next bullet — the raw order
    inside a rho tie is not a thing two float widths can be held to, and saying so is part of
    the proof); the FIRST RING is held prism for prism across its whole address, because a ring
    is N directions on a small circle and has no recurrence to be chaotic with. THE CHILDREN
    are a fixed-iteration relaxation behind a visibility floor, so a near-degenerate triple
    can land either side of it across float widths (measured: Space, 97 discs against 99) —
    they are held by disc count, counts per size octave, a log rho histogram and a tangency
    median. NOTE which tangency: the commissioned form (a child's gap to its NEAREST
    lower-index disc) is a tautology of `Inscribe`'s own last line and measures 0 at every
    relaxation rate down to 0.01 — it is kept, labelled, and joined by the form that can
    actually fail, the child's THIRD smallest gap, which is the Apollonian step's own
    definition and is 0 at the shipped rate and 0.163 at a rate of 0.10. RING INTEGRITY (every ring emits its full N
    prisms) and WHICH RINGS RELEASE THE FALL are asked of the shipped side alone, off the
    harness's own two outputs, so neither can be satisfied by the model agreeing with itself.

  * THE LAY ORDER INSIDE A SYMMETRY ORBIT IS NOT COMPARABLE ACROSS FLOAT WIDTHS, and saying
    so is part of the proof rather than a hole in it. The bulb's symmetry puts level-0 discs
    in orbits that share a rho EXACTLY in float32 and only to ~1e-16 in float64, so the
    shipped TOTAL key (-Rho, Level, index) falls through to the index in C# and is separated
    by residual bits in the model. It is compared CANONICALLY instead — grouped where
    consecutive rhos agree to 1e-5, each group's indices sorted — which is the whole of what
    "sorted by rho descending, ties by index" can mean to two implementations, and which
    still catches a wrong key, a flipped direction, a missing disc or a moved rho. The raw
    order is REPORTED so the ties are visible.

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
margin (Docs/ECOSYSTEM.md §52).

  verify_mandelbulb_flora_tables.py
  verify_mandelbulb_flora_tables.py --self-test   # mutate the shipped file 26 ways and assert
                                                  # every gate trips (one is an INERT control:
                                                  # a claimed pure optimisation, proven to move
                                                  # nothing rather than assumed to)
"""
import argparse
import collections
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


# ── THE WATERSHED's census (Docs/ECOSYSTEM.md §53) ────────────────────────────────────
#
# The critical points are the ONE place the surface is differentiated twice, and the only
# place in this species where two implementations could legitimately disagree about a SET
# rather than about a number: a saddle that appears on one side and not the other does not
# move one prism, it adds or deletes four arms. So the census is held as hard as it can
# honestly be held, and what cannot be held tight is PRINTED next to its bound.

CRIT_KIND = ("saddle", "peak", "pit")


def shipped_critical(element, w=(0.0, 0.0, 0.0), grid=GRID):
    """`crit <kind> <theta> <phi> <radius> <sharpness> <valley.xy> <ridge.xy>` in SCAN
    order, then `saddle <i>` rows giving the farthest-point ORDER as indices into it, then
    `peak <i>` rows giving the same for the PEAKS (Apollonia's level 0)."""
    points, order, peaks = [], [], []
    for line in run("crit", element, w[0], w[1], w[2], grid).splitlines():
        t = line.split()
        if not t:
            continue
        if t[0] == "crit":
            points.append((int(t[1]),) + tuple(float(v) for v in t[2:]))
        elif t[0] == "saddle":
            order.append(int(t[1]))
        elif t[0] == "peak":
            peaks.append(int(t[1]))
    return points, order, peaks


# Everything the harness answers about the SHIPPED file is cached for one `verify()` call and
# thrown away at the start of the next. The cache matters (a gasket build is a peak census
# plus an O(n^3) triple search, and three rows below ask for the same one) and the DISCARD
# matters more: `--self-test` edits MandelbulbSurface.cs between calls, so a cache that
# outlived a mutation would prove the previous build.
_SHIPPED = {}


def shipped_gasket(element, rules, w=(0.0, 0.0, 0.0), grid=GRID):
    """APOLLONIA's disc set as the SHIPPED Growth built it:
    `(discs, lay, samples, rho_ref, seed_count)` with each disc `(level, lane, rho, axis)` in
    disc-index order. The harness reflects it out of Growth's private state — see the verb's
    own comment for why that is the honest reading rather than a re-derivation."""
    key = ("gasket", element, tuple(rules.as_list()), w, grid)
    if key in _SHIPPED:
        return _SHIPPED[key]
    discs, lay, samples, rho_ref, seeds = [], None, None, None, None
    for line in run("gasket", element, w[0], w[1], w[2], grid, *rules.as_list()).splitlines():
        t = line.split()
        if not t:
            continue
        if t[0] == "disc":
            discs.append((int(t[2]), int(t[3]), float(t[4]),
                          (float(t[5]), float(t[6]), float(t[7]))))
        elif t[0] == "order":
            lay = [int(v) for v in t[1:]]
        elif t[0] == "ring":
            samples, rho_ref = int(t[1]), float(t[2])
        elif t[0] == "gasket":
            seeds = int(t[2])
    _SHIPPED[key] = (discs, lay, samples, rho_ref, seeds)
    return _SHIPPED[key]


_MODEL_GASKET = {}


def model_gasket(element, rules):
    """The model's disc set. Cached across a whole run — it is a function of the shipped
    TABLE and of the rules, neither of which `--self-test` touches."""
    key = (element, tuple(rules.as_list()))
    if key not in _MODEL_GASKET:
        _, surface = model_surface(element)
        discs, lay, rho_ref = M.build_gasket(surface, rules)
        _MODEL_GASKET[key] = (discs, lay, M.ring_samples_for(surface, rules, rho_ref), rho_ref)
    return _MODEL_GASKET[key]


def check_critical(element, fail, verbose=True):
    _, surface = model_surface(element)
    mine = M.critical_points(surface)
    mine_order = M.saddles(surface)
    index = {id(c): i for i, c in enumerate(mine)}
    mine_idx = [index[id(c)] for c in mine_order]
    mine_peaks = [index[id(c)] for c in M.peaks(surface)]

    theirs, their_idx, their_peaks = shipped_critical(element)

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
    # the whole sphere (§50.5's polar-cap defect, met from a third direction). It is a
    # permutation of integers, so there is nothing to tolerate: it either matches or the two
    # implementations grow different plants.
    order_ok = their_idx == mine_idx
    if not order_ok:
        first = next((i for i, (a, b) in enumerate(zip(mine_idx, their_idx)) if a != b),
                     min(len(mine_idx), len(their_idx)))
        fail(f"{element}: the farthest-point saddle ORDER differs from position {first} "
             f"(model {mine_idx[:6]}... shipped {their_idx[:6]}...)")

    # THE PEAK ORDER, EXACTLY — the same gate as the saddle order above, for the OTHER half
    # of the census. It is not a second opinion about one list: `Saddles()` and `Peaks()` are
    # separate filters over the census and separate farthest-point walks, and Apollonia reads
    # only the second. Its level-0 discs are a PREFIX of this order, so an ordering difference
    # here is not one ring moved, it is a different set of thirteen lobes crowned — the
    # polar-cap defect (§50.5) met from a fourth direction, since every prefix of a
    # farthest-point order must be spread over the whole sphere.
    peak_ok = their_peaks == mine_peaks
    if not peak_ok:
        first = next((i for i, (a, b) in enumerate(zip(mine_peaks, their_peaks)) if a != b),
                     min(len(mine_peaks), len(their_peaks)))
        fail(f"{element}: the farthest-point PEAK order differs from position {first} "
             f"(model {mine_peaks[:6]}... shipped {their_peaks[:6]}...)")

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
    their_peak_rows = sum(1 for row in theirs if row[0] == 1)
    if (len(their_peaks) != their_peak_rows or len(set(their_peaks)) != len(their_peaks)
            or any(i < 0 for i in their_peaks)):
        fail(f"{element}: the shipped census holds {their_peak_rows} peaks but orders "
             f"{len(their_peaks)} ({len(set(their_peaks))} distinct, "
             f"{sum(1 for i in their_peaks if i < 0)} unplaced)")

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
              f"order {'exact' if order_ok else 'DIFFERS'}/"
              f"{'exact' if peak_ok else 'DIFFERS'}  "
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
        # on (Docs/ECOSYSTEM.md §53).
        w_rad = max(w_rad, abs(math.sqrt(sum(c * c for c in pos)) - abs(radius * (1.0 - dv) + off)))

    # POSITION does not read the normal — it is dir * (radius*(1-dive) + off), and both
    # factors are smooth — so it is held at 1e-6 (measured 5.1e-7). FORWARD and UP do read
    # the normal, which is a finite difference of a field the two implementations store as
    # float32 and reconstruct through different double sums, so their floor is ~1e-5 whatever
    # Pose does: measured worst 8.8e-6 (Time). Holding them at the 1e-5 this was commissioned
    # at would be a 1.13x margin, which §52 records as the shape of a constant that is a
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


# ── APOLLONIA's gasket (Docs/ECOSYSTEM.md §54) ────────────────────────────────────────
#
# The disc set IS the species — one ring per disc, in lay order — and it splits cleanly into
# a half that can be held EXACTLY and a half that cannot, for a reason that is arithmetic
# rather than a gap in the proof:
#
#   LEVEL 0 is closed form. A level-0 disc is a PEAK of the census (already ordered exactly,
#   above) given half the angle to its nearest neighbour: one `min` over `acos(dot(..))` and
#   a halving, with no recurrence and no threshold. It is held to 1e-6.
#
#   THE CHILDREN are a fixed-iteration relaxation behind a `DiscMinRadius` threshold and a
#   greedy overlap test, so a near-degenerate triple can land either side of the floor across
#   float widths and take a whole subtree with it (measured: Space, 97 discs against 99).
#   They are held by their STATISTICS — counts per size octave, the rho histogram, and the
#   tangency median that says the relaxation still converges.
#
# There is a THIRD thing here and it is the finding of this pass: the LAY ORDER inside a
# symmetry orbit is not comparable across float widths at all. The bulb's (n-1)-fold symmetry
# puts level-0 discs in orbits that share a rho EXACTLY in float32 (Mass: eleven discs at
# 0.260037362575531 to the bit) and only to ~1e-16 in float64. The shipped sort key is TOTAL
# — (-Rho, Level, index) — so in C# the tie falls through to the index and yields 0,2,3,4,…
# while in the model the residual bits separate the rhos and yield 2,3,6,12,0,… That is not
# a transcription error and no total key can fix it; it is what "exact ties in exact
# arithmetic" means when one side rounds to float32 first. So the lay order is compared in a
# CANONICAL form: the sequence is grouped where consecutive rhos agree to LAY_TIE, each
# group's indices are sorted, and the result must match exactly. That is precisely the
# statement "sorted by rho descending, ties by index" — everything an observer can hold — and
# it still catches a wrong key, a flipped direction, a missing disc or a moved rho.
#
# LAY_TIE is 1e-5, and the window it sits in is narrower than it looks — measured, the whole
# fleet: 30x above the model-vs-shipped rho difference (3.3e-7, Space) and 32x below the
# nearest level-0 rho gap that float32 can resolve at all (3.2e-4, Charge). Space carries two
# level-0 rhos 6e-9 apart, which is UNDER float32's own quantum at 0.47, so the shipped file
# genuinely holds them as one number and grouping them is right rather than lenient. Grouping
# is chained off the previous member, which is what "consecutive" means for a sorted run; at
# a 32x margin no chain of genuine steps can walk a group across a real gap.

LAY_TIE = 1e-5


def _canonical_lay(order, rho_of):
    """The lay order with each rho-tie group's indices sorted — the only form of it two float
    widths can be held to (see above). Returns the flattened sequence and the group sizes, so
    a caller can report that the TIE STRUCTURE itself matched."""
    groups, cur = [], []
    for idx in order:
        if cur and abs(rho_of(cur[-1]) - rho_of(idx)) > LAY_TIE:
            groups.append(cur)
            cur = []
        cur.append(idx)
    if cur:
        groups.append(cur)
    flat = []
    for g in groups:
        flat.extend(sorted(g))
    return flat, [len(g) for g in groups]


def _tangency(discs, level_of, rho_of, axis_of):
    """TWO readings of the same claim, because the commissioned one turns out to be a
    tautology and finding that out is part of the job.

    (a) `nearest` — each child's MIN, over the three nearest lower-index discs, of
        |angle - rho_child - rho_parent| / rho_child. This is the gate as commissioned and as
        the spec's §10 gate 4 states it, and MEASURED it is 0.00000 at EVERY relaxation rate
        from the shipped 0.6 down to 0.01. It cannot be otherwise: `Inscribe` DEFINES rho as
        `min(angle(x, d_i) - rho_i)` over the three parents, so the gap to whichever parent
        achieved that min is zero by construction, however badly x converged. It is kept
        because it still catches a child whose nearest neighbours are none of its parents,
        and it is labelled here so nobody reads a row of zeroes as evidence that `Inscribe`
        converged.

    (b) `third` — each child's THIRD SMALLEST gap over ALL lower-index discs. This is the
        Apollonian step's own definition ("the disc inscribed in a curvilinear triangle of
        three mutually adjacent discs") asked without needing to know WHICH three, and it is
        not a tautology: a child that touches one parent and hangs off the other two scores
        its real shortfall. MEASURED as a sensitivity curve, median over children, worst
        element: 0.00000 at the shipped rate 0.6, 0.0022 at 0.30, 0.055 at 0.15, 0.163 at
        0.10. That curve is why the negative control perturbs the rate to 0.10 — it is the
        largest perturbation-free-of-doubt that fires this row on all four elements rather
        than leaving it to the disc-count row to catch.

    The MAX over the three nearest was measured too and rejected: at the shipped rate it
    reads 2.12 on Space, because in a dense gasket a child's three nearest lower-index discs
    are routinely not its three parents. A real per-parent tangency needs `Disc` to carry the
    triple it was inscribed from, which is a C# change and is recorded as one.

    Returns (nearest_sorted, third_sorted)."""
    nearest, third = [], []
    for i in range(len(discs)):
        if level_of(i) == 0:
            continue
        gaps = [abs(M._angle(axis_of(i), axis_of(j)) - rho_of(i) - rho_of(j))
                / max(rho_of(i), 1e-9) for j in range(i)]
        if not gaps:
            continue
        near = sorted(range(i), key=lambda j: M._angle(axis_of(i), axis_of(j)))[:3]
        nearest.append(min(gaps[j] for j in near))
        if len(gaps) >= 3:
            third.append(sorted(gaps)[2])
    nearest.sort()
    third.sort()
    return nearest, third


def _rho_histogram(rhos, lo, hi):
    """Eight LOG-spaced bins between the visibility floor and the reference ring. Log because
    the species is a size ladder — linear bins put five octaves into the bottom bin and report
    a gasket and a bag of hoops as the same shape."""
    lo = max(1e-4, lo)
    hi = max(hi, lo * 1.0001)
    span = math.log(hi / lo)
    bins = [0] * 8
    for r in rhos:
        u = math.log(max(r, lo) / lo) / span
        bins[min(7, max(0, int(u * 8)))] += 1
    return bins


def check_gasket(element, rules, fail, verbose=True):
    _, surface = model_surface(element)
    mine, mine_lay, mine_n, mine_rho_ref = model_gasket(element, rules)
    theirs, their_lay, their_n, their_rho_ref, their_seeds = shipped_gasket(element, rules)

    if not theirs or their_lay is None or their_n is None:
        fail(f"{element}: the gasket verb emitted no disc set")
        return None
    if len(their_lay) != len(theirs) or sorted(their_lay) != list(range(len(theirs))):
        fail(f"{element}: the shipped lay order is not a permutation of its {len(theirs)} discs")
        return None
    if their_seeds != len(theirs):
        fail(f"{element}: the shipped gasket built {len(theirs)} discs but {their_seeds} seeds "
             f"— one seed per disc, same index, is what every per-disc array rests on")

    # ── LEVEL 0, EXACTLY ─────────────────────────────────────────────────────────────
    n_peaks = len(M.peaks(surface))
    want0 = min(rules.disc_seeds, n_peaks) if rules.disc_seeds > 0 else n_peaks
    mine0 = sum(1 for d in mine if d.level == 0)
    their0 = sum(1 for d in theirs if d[0] == 0)
    if mine0 != want0 or their0 != want0:
        fail(f"{element}: {their0} level-0 discs shipped / {mine0} model, against "
             f"min(DiscSeeds {rules.disc_seeds}, {n_peaks} peaks) = {want0}")
        return None
    # BuildGasket appends the peaks first and in order, so index i is peak i on both sides —
    # which is only a fair comparison because the PEAK ORDER itself is already held exactly
    # by the census above. Without that row this would be comparing two different lobe lists.
    d_rho0 = max(abs(mine[i].rho - theirs[i][2]) for i in range(want0))
    d_ax0 = max(max(abs(a - b) for a, b in zip(mine[i].axis, theirs[i][3]))
                for i in range(want0))
    # 1e-6 as commissioned; measured worst on the shipped tree is 3.3e-7 (Space), so the
    # margin is 3x. It is a float-width floor rather than a tuning: the level-0 rho inherits
    # the census's own ~1e-6 rad positional disagreement through `0.5 * min(angle)`.
    if d_rho0 > 1e-6:
        fail(f"{element}: level-0 disc rho differs by {d_rho0:.3g} (bound 1e-6)")
    if d_ax0 > 1e-5:
        fail(f"{element}: level-0 disc axis differs by {d_ax0:.3g} (bound 1e-5)")

    # ── THE LAY ORDER's level-0 subsequence, canonically ─────────────────────────────
    mine_top = [i for i in mine_lay if mine[i].level == 0]
    their_top = [i for i in their_lay if theirs[i][0] == 0]
    mine_c, mine_groups = _canonical_lay(mine_top, lambda i: mine[i].rho)
    their_c, their_groups = _canonical_lay(their_top, lambda i: theirs[i][2])
    if mine_c != their_c or mine_groups != their_groups:
        fail(f"{element}: the level-0 lay order differs canonically — shipped "
             f"{their_c[:8]}... (groups {their_groups}) vs model {mine_c[:8]}... "
             f"(groups {mine_groups})")
    raw_same = mine_top == their_top
    # The sort itself, asked of the SHIPPED side alone: the lay order must be rho-DESCENDING,
    # which is the whole of what "a budget-stopped plant loses the smallest rings" rests on.
    # A model-vs-model comparison cannot see a broken sort (both sides would be broken the
    # same way); a monotonicity test on the shipped sequence can.
    drop = max((theirs[b][2] - theirs[a][2]
                for a, b in zip(their_lay, their_lay[1:])), default=0.0)
    if drop > 1e-6:
        fail(f"{element}: the shipped lay order is not rho-descending — it rises by "
             f"{drop:.3g} somewhere (the lay order IS the budget's truncation order)")

    # ── N and the girth reference, exactly ───────────────────────────────────────────
    if their_n != mine_n:
        fail(f"{element}: {their_n} ring samples shipped, model derived {mine_n} "
             f"(RingSamples {rules.ring_samples}, StepSize {rules.step})")
    if abs(their_rho_ref - mine_rho_ref) > 1e-6:
        fail(f"{element}: rho_ref {their_rho_ref:.9f} shipped vs {mine_rho_ref:.9f} model "
             f"(bound 1e-6)")

    # ── THE CHILDREN, STATISTICALLY ──────────────────────────────────────────────────
    #
    # Total, per size octave, and as a log histogram of rho. All three are taken over EVERY
    # disc rather than over the children alone, deliberately: the level-0 half is already
    # pinned to 1e-6 above, so including it adds no slack and makes each row a statement
    # about the shape the player sees rather than about an internal split.
    lanes = max(1, rules.gasket_levels)
    mine_lanes = [sum(1 for d in mine if d.lane == k) for k in range(lanes)]
    their_lanes = [sum(1 for d in theirs if d[1] == k) for k in range(lanes)]
    # 10% on the total: measured worst is Space at 2.1% (97 against 99), and the whole
    # difference is two children at the visibility floor. A tighter bound would be a bound on
    # the float width rather than on the transcription.
    if abs(len(theirs) - len(mine)) > 0.10 * max(1, len(mine)):
        fail(f"{element}: {len(theirs)} discs shipped, model built {len(mine)} "
             f"({abs(len(theirs) - len(mine)) / max(1, len(mine)):.1%}, bound 10%)")
    # 15% per lane, with a ONE-DISC floor: a lane holding four discs cannot express 15%, and
    # a gate that fails on a single disc moving between two adjacent octaves is measuring the
    # floor of `log2(rho_ref/rho)`, not the gasket. Measured worst: Space lane 4, 33 vs 36.
    for k in range(lanes):
        if abs(their_lanes[k] - mine_lanes[k]) > max(1.0, 0.15 * mine_lanes[k]):
            fail(f"{element}: size octave {k} holds {their_lanes[k]} discs shipped against "
                 f"{mine_lanes[k]} model (bound 15% or 1 disc) — lanes shipped "
                 f"{their_lanes}, model {mine_lanes}")
    # ONE set of bin edges for both sides (the model's). Binning each side against its own
    # rho_ref would let a 5e-8 difference in the edge move a disc across a boundary and report
    # a histogram disagreement that is really an edge disagreement — and the edge is already
    # gated above, to 1e-6.
    mine_h = _rho_histogram([d.rho for d in mine], rules.disc_min_radius, mine_rho_ref)
    their_h = _rho_histogram([d[2] for d in theirs], rules.disc_min_radius, mine_rho_ref)
    bad = [k for k in range(8)
           if abs(their_h[k] - mine_h[k]) > max(3.0, 0.15 * mine_h[k])]
    if bad:
        fail(f"{element}: the rho histogram differs in bins {bad} (bound 15% or 3 discs) — "
             f"shipped {their_h}, model {mine_h}")

    # ── TANGENCY — that the relaxation still converges ───────────────────────────────
    #
    # The one row standing between `Inscribe` and a silent degradation to "a smaller circle
    # roughly in the middle", which still renders as circles and stops being a gasket. It is
    # gated on BOTH sides because a model-only reading would pass a shipped file whose
    # relaxation had stopped converging altogether.
    mt, mt3 = _tangency(mine, lambda i: mine[i].level, lambda i: mine[i].rho,
                        lambda i: mine[i].axis)
    tt, tt3 = _tangency(theirs, lambda i: theirs[i][0], lambda i: theirs[i][2],
                        lambda i: theirs[i][3])

    def med(v):
        return v[len(v) // 2] if v else float("nan")

    med_m, med_t, med_m3, med_t3 = med(mt), med(tt), med(mt3), med(tt3)
    if not mt or not tt:
        fail(f"{element}: the gasket has no children to measure tangency on "
             f"(shipped {len(tt)}, model {len(mt)})")
    else:
        # Both sides, because a model-only reading would pass a shipped file whose relaxation
        # had stopped converging altogether. Both forms, because (a) is the commissioned row
        # and (b) is the one that can fail — see `_tangency`.
        for side, near, thi in (("shipped", med_t, med_t3), ("model", med_m, med_m3)):
            if not near <= 0.05:
                fail(f"{element}: the {side} gasket's median child-to-NEAREST tangency gap is "
                     f"{near:.4f} of rho_child (bound 0.05) — a child's nearest discs are not "
                     f"the ones it was inscribed between")
            if thi == thi and not thi <= 0.05:
                fail(f"{element}: the {side} gasket's median THIRD tangency gap is "
                     f"{thi:.4f} of rho_child (bound 0.05) — its children are no longer "
                     f"tangent to three discs, so Inscribe has stopped converging")

    # REPORTED, never gated, because both are statements about the RULE's authoring rather
    # than about the transcription — but both are knife edges somebody has to be able to see:
    #
    #  * the LONE-DISC FALLBACK. A level-0 disc with no neighbour inside pi/3 takes the frozen
    #    `nn = pi/3` instead of a measurement, so its rho is a magic constant. rho_ref is the
    #    LARGEST disc, so when a fallback disc wins, the girth reference, the octave lanes and
    #    N are all set by that constant (measured: Space 2, Time 1).
    #  * the N DERIVATION's margin. N = round(circ / StepSize) is shared by every ring of the
    #    plant, so a half-unit swing is a different plant everywhere at once. Measured thinnest
    #    is Charge at 0.088 of a sample.
    fallback = sum(1 for d in theirs if d[0] == 0 and abs(d[2] - math.pi / 6) < 1e-6)
    q = (2 * math.pi * math.sin(their_rho_ref) * surface.mean_radius
         / max(1e-6, rules.step)) if rules.ring_samples <= 0 else float("nan")
    margin = abs(q - round(q)) if q == q else float("nan")

    if verbose:
        print(f"  {element:6s} gasket {len(theirs)}/{len(mine)} discs ({their0} level-0, "
              f"{fallback} lone-disc)  lanes {their_lanes}  N {their_n} (margin {margin:.3f})  "
              f"rhoRef {their_rho_ref:.6f}  L0 rho {d_rho0:.1e} axis {d_ax0:.1e}  "
              f"lay L0 canonical{'' if raw_same else ' (raw order differs: rho ties)'}")
        print(f"         children: rho histogram {their_h} vs {mine_h} "
              f"(8 log bins, {rules.disc_min_radius:.3f}..{mine_rho_ref:.3f})  "
              f"tangency nearest {med_t:.5f}/{med_m:.5f}  third {med_t3:.5f}/{med_m3:.5f}")
    return their_lay, theirs, their_n


def check_girth_floor(fail, verbose=True):
    """`RingGirthFloor`, held against the CODE rather than against the authoring.

    The mean-girth row of `check_element` runs on the SHIPPED rules, and on the shipped
    Apollonia the floor is nearly inert: after the tuning pass (exponent 0.30, floor 0.55)
    every element's smallest ring already sits within a few percent of it, so deleting the
    floor line in the C# moved the mean girth under 2% on all four — the self-test control
    for that line stopped firing, and what it had been measuring the whole time was where the
    species happened to be authored, not whether the code applied the field. A control that
    reads the authoring is a control that goes dark on the next retune.

    So this row FORCES the floor to bind: one gasket element grown twice, once with the floor
    at 0 and once at 0.95, on both the shipped harness and the model. Two things are
    asserted. (1) The lift the floor buys — mean girth at 0.95 less mean girth at 0 — must
    match between the two sides, which is the transcription claim, and it must be LARGE on
    the model (the probe must actually bind, or this row proves nothing and says so).
    (2) The floored plant's mean girth must agree to the gasket girth tolerance. With the
    floor line deleted the shipped lift is exactly zero against a model lift of ~0.26 and the
    row fires by name whatever the shipped table authors."""
    on = sorted(sp for sp in M.SPECIES
                if any(M.rules_for(e, sp).gasket_levels != 0 for e in M.ELEMENTS))
    idx = M.Rules.FIELDS.index("ring_girth_floor")
    # Space: the deepest octave ladder of the four, so the floor has the most rings to lift.
    element = "Space"
    for species in on:
        base = M.rules_for(element, species)

        def mean_girth(floor):
            values = base.as_list()
            values[idx] = floor
            rules = M.Rules(*values)
            mine_p, _, _ = M.grow(model_surface(element)[1], rules, SEED, BUDGET)
            theirs_p, _, _, _ = shipped_prisms(element, rules)
            if not mine_p or not theirs_p:
                fail(f"{species}/{element}: the girth-floor probe at {floor} grew nothing "
                     f"(model {len(mine_p)}, shipped {len(theirs_p)})")
                return None, None
            return (sum(p.girth for p in mine_p) / len(mine_p),
                    sum(p.girth for p in theirs_p) / len(theirs_p))

        mine_lo, theirs_lo = mean_girth(0.0)
        mine_hi, theirs_hi = mean_girth(0.95)
        if None in (mine_lo, theirs_lo, mine_hi, theirs_hi):
            continue
        mine_lift, theirs_lift = mine_hi - mine_lo, theirs_hi - theirs_lo
        # The probe must BIND on the model, or a passing row is vacuous. Measured on the
        # shipped tree the model lifts the mean girth 0.258 at a 0.95 floor; a lift under 0.05
        # means the species' rings all already sit above 0.95 and this element is the wrong
        # probe for it.
        if mine_lift < 0.05:
            fail(f"{species}/{element}: the girth-floor probe does not bind on the model "
                 f"(lift {mine_lift:.4f} from floor 0 to 0.95) — this row proves nothing here")
            continue
        if abs(theirs_hi - mine_hi) > 0.02 * mine_hi:
            fail(f"{species}/{element}: girth floor forced to 0.95 — mean girth "
                 f"{theirs_hi:.5f} shipped vs {mine_hi:.5f} model (tolerance 2.0%)")
        if abs(theirs_lift - mine_lift) > 0.10 * mine_lift:
            fail(f"{species}/{element}: girth floor forced to 0.95 lifts the mean girth by "
                 f"{theirs_lift:.4f} shipped vs {mine_lift:.4f} model — the floor is not "
                 f"applied by the shipped code the way the model applies it")
        elif verbose:
            print(f"  girth floor ({species}/{element}): forced to 0.95 it lifts the mean "
                  f"girth {theirs_lift:.4f} shipped / {mine_lift:.4f} model "
                  f"(from {theirs_lo:.4f} / {mine_lo:.4f}); the floor binds and the two "
                  f"sides agree")


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

    gasket = rules.gasket_levels != 0

    # 2. the DISC SET, for a gasket species — its seeds are one per disc, same index, and
    #    `BuildSeeds` is not on its path at all (asking it would compare against a one-seed
    #    Fibonacci fallback, which is what the first cut of this file did: it reported "1
    #    seed" on all four elements while the plant was growing forty).
    if gasket:
        gas = check_gasket(element, rules, fail, verbose)
        if gas is None:
            return
        their_lay, their_discs, ring_n = gas
        their_seeds = len(their_discs)
        _, mine_lay, _, _ = model_gasket(element, rules)
        mine_lay_first = mine_lay[0] if mine_lay else None
    else:
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

    # A GASKET species has no walk to diverge, so its prism count is a restatement of its
    # DISC count: every ring emits exactly N prisms (asserted below), so the only thing that
    # can move this number is a child landing either side of the visibility floor. 3% as
    # commissioned; measured worst is Space at 1.9% — two children out of 97, the same two
    # the 10% disc-count row above prices at 2.1%. The two rows are deliberately not
    # independent: this one is what a reader of the plant sees, that one is the cause.
    if gasket:
        if abs(len(theirs_p) - len(mine_p)) > 0.03 * max(1, len(mine_p)):
            fail(f"{element}: {len(theirs_p)} prisms shipped, model laid {len(mine_p)} "
                 f"({abs(len(theirs_p) - len(mine_p)) / max(1, len(mine_p)):.2%}, bound 3%)")
    elif len(theirs_p) != len(mine_p):
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
    # A GASKET's girth is a closed form of its disc's rho, not a function of a run length,
    # so the run-length formula above is not a loose bound here - it is a bound derived from
    # a quantity (`per_curve`, which for a ring is just N) that has nothing to do with what
    # makes the two sides differ. Measured true divergence on the shipped tree: Charge
    # 1.2e-7, Mass 1.5e-5, Time 1.1e-7, Space 2.5e-3 - Space alone, and only because two
    # children land either side of the visibility floor and change WHICH rings are in the
    # mean. 2% is that worst case with an 8x margin, against the 10% the formula was handing
    # out. It is a tightening rather than a new gate, and it is the one that makes
    # `RingGirthFloor` gateable at all: measured, deleting that line entirely moves the mean
    # girth 11.78% on Space and 2.18% on Time (caught) and 0.31% / 0.07% on Mass and Charge
    # (NOT caught - there the floor is nearly inert by authoring, since their smallest ring's
    # allometric girth already sits within a few percent of it, so no bound on this statistic
    # can see it and the measure tool's volume-span gate is where that has to land).
    # The LENGTH statistics of a gasket are a different channel from its girth: a prism's
    # length is its ring's chord, so the median is set by which of the SMALLEST rings exist,
    # and that is exactly the population the two float widths disagree on (measured after
    # the tuning pass: Space's smallest octave holds 74 of 137 rings and the median length
    # moved 2.3%). 3% on the lengths; the girth keeps 2%, which is what makes RingGirthFloor
    # gateable (see above).
    tol = ((0.03, 0.03, 0.02) if gasket
           else (0.02, 0.02, min(0.10, 0.02 + 0.002 * per_curve)))
    for name, x, y, lim in zip(("mean length", "median length", "mean girth"), sa, sb, tol):
        if abs(x - y) > lim * max(abs(x), 1e-6):
            fail(f"{species}/{element}: {name} {y:.5f} shipped vs {x:.5f} model "
                 f"(tolerance {lim:.1%})")

    # ── THE FALL, by the three statistics the dive owns (Docs/ECOSYSTEM.md §53) ──
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
    #     A GASKET species is held EXACTLY for a different reason from the skeleton's, and the
    #     reason is narrower than it first looks. Its quota is min(DiveCount, |level 0|) and
    #     every owed ring is laid (the lay order is ring-major and the budget does not bite),
    #     so no walk decides whether a dive happens and no float width can drop one. What the
    #     two sides do NOT agree on is WHICH DISC each stride step picks, because the level-0
    #     lay order inside a rho tie is not comparable across float widths (see check_gasket).
    #     They agree on the POSITION it picks, which is what becomes the curve index — so the
    #     count below is exact and the dive-curve row further down is exact, while the dive
    #     PRISM count keeps the free species' allowance because two different rings of the
    #     same rho release spirals of slightly different length (measured: Mass, 239 vs 241).
    d_slack = 0 if (skeleton or gasket) else 2
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

    # ── APOLLONIA, by the three things a RING owns (Docs/ECOSYSTEM.md §54) ───────────
    integ_t = integ_m = float("nan")
    dive_curves = expect_curves = set()
    first_ring = float("nan")
    if gasket:
        def rings(prisms, n):
            """Per curve, the number of SURFACE prisms it emitted. A closed ring of N samples
            has N+1 points and therefore exactly N segments, and a dive is appended to the
            same curve with TanR != 0 — the address field that already separates the two
            everywhere else in this file. The last curve is dropped when the plant hit its
            budget, because a budget cut is not a broken ring (conflating the two reports a
            truncated plant as a dashed one; the spec's own §10 records making that mistake)."""
            per = collections.Counter()
            dive = set()
            for q in prisms:
                if q.tan_r == 0.0:
                    per[q.curve] += 1
                else:
                    dive.add(q.curve)
            if len(prisms) >= BUDGET and per:
                del per[max(per)]
            whole = sum(1 for v in per.values() if v == n)
            return (whole / len(per) if per else 0.0), dive, len(per)

        integ_t, dive_curves, n_rings_t = rings(theirs_p, ring_n)
        integ_m, mine_dive_curves, _ = rings(mine_p, ring_n)
        # RING INTEGRITY. This is a statement about what the growth rule EMITS, not about the
        # plant after `MandelbulbFlora.Claim` — the claim lives in a file this harness does
        # not compile, so filtering here in Python would prove nothing about the C#. The
        # claim-side integrity is `measure_mandelbulb_flora.py`'s gate; this one is the half
        # that can be held against the shipped code, and it is the half a transcription error
        # lands in: drop `into.Add(into[0])` from RingPoints and every ring becomes an ARC
        # while the prism count moves by one part in N and hides inside every other bound.
        if abs(integ_t - integ_m) > 0.05:
            fail(f"{element}: {integ_t:.1%} of shipped rings emit their full {ring_n} prisms "
                 f"against the model's {integ_m:.1%} (bound 5 points)")
        if integ_t < 0.95:
            fail(f"{element}: only {integ_t:.1%} of the shipped plant's {n_rings_t} rings are "
                 f"CLOSED ({ring_n} prisms each) — the repeated unit is an arc, not a ring")

        # WHICH RINGS RELEASE THE FALL, asked of the SHIPPED side alone. Both halves come out
        # of the harness — the lay order and the levels from `gasket`, the dive-bearing curves
        # from the prism stream — so this is the shipped file held against its own documented
        # rule rather than against the model. That independence is the point: the level-0
        # stride and the model's are NOT comparable disc for disc (rho ties, above), but the
        # POSITIONS they pick in the lay order are, and it is the position that becomes the
        # curve index. A stride taken over ALL discs instead of the level-0 ones puts dives on
        # rings too small to release a readable spiral, and nothing else in this file sees it.
        #
        # It is also this file's only check that the `gasket` verb and the `shipped` verb are
        # describing the SAME disc set. They are separate harness invocations at different
        # Rng seeds (the verb builds at seed 1, the plant at 12345), which is sound only
        # because BuildGasketSeeds draws no Rng — an assumption worth holding rather than
        # stating, and this row holds it: the expected curve indices come from the verb's lay
        # order and the actual ones from the plant's prism stream, so two different disc sets
        # could not agree on them.
        top = [i for i in their_lay if their_discs[i][0] == 0]
        quota = min(max(0, rules.dive_count), len(top))
        pos = {d: k for k, d in enumerate(their_lay)}
        expect_curves = {pos[top[(d * len(top)) // quota]] + 1 for d in range(quota)}
        if not dive_curves <= expect_curves:
            fail(f"{element}: dives left from curves {sorted(dive_curves - expect_curves)}, "
                 f"which are not on the level-0 stride {sorted(expect_curves)}")
        if len(dive_curves) != theirs_d:
            fail(f"{element}: {theirs_d} dives spent but {len(dive_curves)} curves carry dive "
                 f"prisms — a dive belongs to exactly one ring")
        if theirs_d == quota and dive_curves != expect_curves:
            fail(f"{element}: all {quota} dives were spent but on curves "
                 f"{sorted(dive_curves)} rather than the stride's {sorted(expect_curves)}")

        # THE FIRST RING, PRISM FOR PRISM. A ring is closed form — N directions on a small
        # circle, each sampled off R(theta, phi) and pulled `flatten` toward the ring's own
        # mean — so unlike every walking species there IS something here that can be held hard
        # past prism 0. It is held on the FIRST ring only, because that is the one both sides
        # provably lay first (it is rho_ref, and the lay order is rho-descending); everything
        # after it can be reordered by a rho tie the two float widths break differently.
        if their_lay and mine_lay_first is not None and their_lay[0] == mine_lay_first:
            k = min(ring_n, len(theirs_p), len(mine_p))
            # The WHOLE address, field by field, because each one is a different part of the
            # ring's closed form and three of them are invisible in the direction alone:
            # `RadialOffset` is the only place `RingFlatten` and the lift onto R(theta, phi)
            # land, `Length` is the chord, and `Girth` is the allometry. A chord-only test
            # would pass a plant whose rings had been flattened onto a sphere.
            ring_worst = {"chord": 0.0, "offset": 0.0, "length": 0.0, "girth": 0.0, "heading": 0.0}
            for i in range(k):
                a, b = mine_p[i], theirs_p[i]
                ring_worst["chord"] = max(ring_worst["chord"], math.sqrt(
                    sum((x - y) ** 2 for x, y in zip(unit(a), unit(b)))))
                ring_worst["offset"] = max(ring_worst["offset"], abs(a.radial - b.radial))
                ring_worst["length"] = max(ring_worst["length"], abs(a.length - b.length))
                ring_worst["girth"] = max(ring_worst["girth"], abs(a.girth - b.girth))
                ring_worst["heading"] = max(ring_worst["heading"], abs(a.tan_a - b.tan_a),
                                            abs(a.tan_b - b.tan_b), abs(a.tan_r - b.tan_r))
            first_ring = ring_worst["chord"]
            # Every bound below is the float32 floor of a closed-form expression with a stated
            # margin — measured worst across the fleet: chord 4.3e-7, offset 9.1e-7, length
            # 4.7e-7, girth 0 exactly (the reference ring's allometry is (rho_ref/rho_ref)^e,
            # which is 1 on both sides by construction), heading 3.3e-6.
            for name, bound in (("chord", 1e-5), ("offset", 1e-5), ("length", 1e-5),
                                ("girth", 1e-6), ("heading", 3e-5)):
                if ring_worst[name] > bound:
                    fail(f"{element}: the FIRST ring's {name} disagrees by {ring_worst[name]:.2e} "
                         f"over its {k} prisms (bound {bound:g}) — a ring is closed form and "
                         f"has nothing to be chaotic with")
        else:
            fail(f"{element}: the shipped and model gaskets lay a different ring first "
                 f"(disc {their_lay[0] if their_lay else None} vs {mine_lay_first})")

    if verbose:
        print(f"  {element:6s} field {worst:.2e}  seeds {their_seeds}  "
              f"prisms {len(theirs_p)}  curves {theirs_c}/{mine_c}  "
              f"bands {filled_theirs}/8 (drift {drift:.0%})  "
              f"walk agrees to prism {first if first is not None else len(theirs_p)}"
              f"{f' (worst chord {worst_chord:.1e})' if skeleton else ''}")
        print(f"         fall: dives {theirs_d}/{mine_d}  "
              f"dive prisms {len(theirs_dp)}/{len(mine_dp)}  "
              f"closest centre {theirs_r:.4f}/{mine_r:.4f} u")
        if gasket:
            # The model's dive curves are REPORTED and never gated. Measured they match the
            # shipped set on all four elements, but that is a coincidence rather than a
            # property: a level-0 disc's POSITION in the lay order shifts if a child that the
            # two float widths disagree about lands ahead of it (Space ships two the model
            # does not). A gate that holds by coincidence is worse than no gate.
            print(f"         rings: integrity {integ_t:.3f}/{integ_m:.3f} at N {ring_n}  "
                  f"first ring {first_ring:.1e}  "
                  f"dive curves {sorted(dive_curves)} of stride {sorted(expect_curves)}"
                  + ("" if mine_dive_curves == dive_curves
                     else f"  (model {sorted(mine_dive_curves)})"))


def check_gasket_switch(fail, verbose=True):
    """THE MASTER SWITCH, from both sides.

    (a) The gasket BLOCK is unreachable on a species that does not switch it on — and that
        is asked of the SHIPPED CODE, not of the rules table. The first cut of this row read
        the table and reported that the three prior species author `GasketLevels 0`, which
        is a RESTATEMENT rather than a gate: the species that "use the gasket" were derived
        from that same column two lines earlier, so its failure branch could never fire. What
        can actually fail, and what §2 actually claims, is that with `GasketLevels 0` the
        other TEN gasket columns are inert — so this row POISONS all ten on a walking species
        and requires the prism stream to come back byte for byte. One element per species,
        because the three differ in PATH (two free walks and a skeleton) and a column read
        outside the switch would be a leak in one of those three paths rather than in one
        element's numbers.

        It is deliberately not a claim about a DIFF, and the difference matters: those three
        plants DID move at the Apollonia commit, because the same commit retuned `FALL_SHARED`
        (`dive_stride_ceiling` 2.0 -> 1.10, `dive_max_steps` 96 -> 160). That is a rule change,
        authored in the model and in the assets, and it reaches the three prior species
        through their own rule rows rather than through anything the gasket added. Their
        streams are held against the model everywhere else in this file, which is the other
        half of the statement: the code cannot reach the new branch, and the new branch is
        not why they look different from last week.

    (b) Apollonia's own rule row with `GasketLevels` forced to 0 must fall back to the
        ordinary walk rather than crash — and this file says WHAT it does, because "does not
        crash" and "grows nothing" are very different answers to give a reader.

        Measured, it grows NOTHING: the row authors SeedCount 0, so `BuildSeeds` takes its
        `max(1, ...)` floor and emits ONE seed, and it authors RadiusMin = RadiusMax = 0, so
        `Trace` breaks on its first frame (`Radius > rMax`) and every curve is dust. One
        seed, zero curves, zero prisms, zero dives, on both sides. That is the honest reading
        of a rule row whose nineteen walk columns are all authored 0 to say they are inert:
        with the gasket switched off there is nothing left to walk with. It is NOT a usable
        escape hatch, and nothing should be authored expecting it to be one."""
    # Which species use the gasket is DERIVED from the rules, never a name written here: a
    # second gasket species added next month would otherwise be skipped by the switch-off
    # test and counted as a violation by the loop above, both silently.
    on = sorted(sp for sp in M.SPECIES
                if any(M.rules_for(e, sp).gasket_levels != 0 for e in M.ELEMENTS))
    for species in on:
        # Half-on is its own defect: the four elements of a species share one growth rule
        # and one prefab, so a species that gasketed two of them would ship two plants under
        # one name. (The mirror of this — "a species not in `on` must author 0" — is not a
        # gate at all, because `on` is derived from that very column; the row that holds the
        # switch against the shipped code is the poison test below.)
        off_els = [e for e in M.ELEMENTS if M.rules_for(e, species).gasket_levels == 0]
        if off_els:
            fail(f"{species}: {off_els} author GasketLevels 0 while its other elements do "
                 f"not — a species must be a gasket on all four or none")

    # THE BLOCK IS INERT WITH THE SWITCH OFF, held against the SHIPPED C#. Every gasket
    # column AFTER `gasket_levels` is set to Apollonia's own value on a species that leaves
    # `gasket_levels` 0; the stream must be byte for byte what the untouched row lays. A
    # column read outside `if (Gasket)` — a girth floor applied unconditionally, a
    # `RingShrink` that reached `Emit`, a `DiscRelaxRate` that leaked into the walk — moves
    # this and nothing else in this file, because the three prior species never author a
    # non-zero value in that block for any row to notice.
    block = M.Rules.FIELDS[M.Rules.FIELDS.index("gasket_levels") + 1:]
    donor = M.rules_for("Mass", on[0]) if on else None
    leak_element = "Mass"
    poisoned = 0
    for species in M.SPECIES:
        if species in on or donor is None:
            continue
        base = M.rules_for(leak_element, species)
        values = base.as_list()
        for f in block:
            values[M.Rules.FIELDS.index(f)] = getattr(donor, f)
        loud = M.Rules(*values)
        if loud.as_list() == base.as_list():
            fail(f"{species}: the gasket-column poison test changed nothing — the donor row "
                 f"({on[0]}) authors the same ten columns, so this row proves nothing")
            continue
        try:
            clean_out = run("shipped", leak_element, 0, 0, 0, GRID, SEED, BUDGET,
                            *base.as_list())
            loud_out = run("shipped", leak_element, 0, 0, 0, GRID, SEED, BUDGET,
                           *loud.as_list())
        except subprocess.CalledProcessError as exc:
            fail(f"{species}/{leak_element}: the shipped walk failed with the gasket columns "
                 f"set and GasketLevels 0 ({exc}) — the block must be unreachable, not fatal")
            continue
        if clean_out != loud_out:
            fail(f"{species}/{leak_element}: setting the ten gasket columns changed the "
                 f"shipped plant while GasketLevels is 0 — a gasket field is read outside "
                 f"the master switch")
        else:
            poisoned += 1

    for species in on:
        for element in M.ELEMENTS:
            values = M.rules_for(element, species).as_list()
            values[M.Rules.FIELDS.index("gasket_levels")] = 0
            off = M.Rules(*values)
            try:
                out = run("shipped", element, 0, 0, 0, GRID, SEED, BUDGET, *off.as_list())
            except subprocess.CalledProcessError as exc:
                fail(f"{species}/{element}: the rule row with GasketLevels 0 made the shipped "
                     f"walk fail ({exc}) — the master switch must fall back, not throw")
                continue
            lines = out.splitlines()
            their_seeds = int(lines[0].split()[2])
            laid = sum(1 for line in lines if line.startswith("p "))
            curves = int(lines[-1].split()[2])
            mine_p, mine_c, _ = M.grow(model_surface(element)[1], off, SEED, BUDGET)
            if (laid, curves) != (len(mine_p), mine_c):
                fail(f"{species}/{element}: with GasketLevels 0 the shipped row lays {laid} "
                     f"prisms over {curves} curves and the model lays {len(mine_p)}/{mine_c} "
                     f"— the fallback walk must be the same walk on both sides")
            if species == "Apollonia" and (their_seeds, laid, curves) != (1, 0, 0):
                fail(f"Apollonia/{element}: with GasketLevels 0 it lays {laid} prisms over "
                     f"{curves} curves from {their_seeds} seeds; measured and documented, the "
                     f"answer is 1 seed and nothing grown")
    if verbose:
        print(f"  gasket switch: {poisoned} species keep their plant byte for byte with all "
              f"ten gasket columns set and GasketLevels 0 (the block is unreachable); "
              f"{', '.join(on)} at 0 falls back to the walk and grows nothing "
              f"(1 seed, 0 prisms)")


def verify(verbose=True):
    failures = []
    # Everything the harness said about the PREVIOUS build of MandelbulbSurface.cs is stale
    # the moment `--self-test` edits it.
    _SHIPPED.clear()

    def fail(msg):
        failures.append(msg)

    # The harness's own selftest EXITS NON-ZERO when it fails, so it has to be caught here or
    # it leaves this file as a bare CalledProcessError — which the `--self-test` loop then
    # reports as "the harness refused to build". That is a lie about four of its own controls
    # (the radial lift and both phi wraps are caught precisely BY this selftest, and they
    # compile perfectly), and it is exactly the shape §52 warns about: a gate that fires for
    # the right reason while naming the wrong one.
    try:
        self = run("selftest")
    except subprocess.CalledProcessError as exc:
        self = (exc.stdout or "") + (exc.stderr or "")
    if "selftest ok" not in self:
        failures.append("the harness's own selftest failed (SH round-trip / Compose "
                        "linearity / Pose inverts an address):\n" + self.strip())
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
    check_gasket_switch(fail, verbose)
    check_girth_floor(fail, verbose)

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
        # RE-ANCHORED (a second time). Apollonia gave Emit a `girthOverride` — a ring's girth
        # is a function of its rho, not of a run length — so this control's line grew a
        # ternary in front of it and the control went MUTATION NOT APPLICABLE, which exits 1
        # and tests nothing. Same lesson the radial-lift control above already carries: a
        # control's anchor is part of the control, and appending a species is exactly the kind
        # of edit that moves one.
        ("the girth taper inverted",
         "float girth = girthOverride > 0f ? girthOverride : taper + (1f - taper) * u;",
         "float girth = girthOverride > 0f ? girthOverride : 1f + (taper - 1f) * u;"),
        ("the walk goes seed-major again (two seeds eat the whole budget)",
         "if (_seedIndex >= _seeds.Count) { _seedIndex = 0; _lane++; continue; }",
         "if (_seedIndex >= _seeds.Count) { _seedIndex = 0; _lane = lanes; continue; }"),

        # ── THE FALL (Docs/ECOSYSTEM.md §53) ──
        ("the (1 - Dive) factor dropped from Pose (every dive prism poses back on the surface)",
         "position = scratch.Dir * (scratch.Radius * (1f - a.Dive) + a.RadialOffset);",
         "position = scratch.Dir * (scratch.Radius + a.RadialOffset);"),
        ("the TanR branch dropped from Pose (a free-space heading re-projected onto the tangent plane)",
         "if (a.TanR != 0f)\n            {",
         "if (a.TanR != 0f && a.Girth < -1e30f)\n            {"),
        ("the dive-owed set taken as a PREFIX of the seed list instead of a stride",
         "_diveOwed[(int)((long)d * _seeds.Count / _diveQuota)] = true;",
         "_diveOwed[d] = true;"),

        # ── THE WATERSHED (§53) ──
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
        # ── APOLLONIA (§54) ──
        #
        # Every control below is anchored inside the gasket block, which is unreachable while
        # GasketLevels == 0 — so each of them leaves the other three species byte for byte
        # untouched and can only be caught by an Apollonia row. That is the point: a species
        # whose defects are invisible to every other species' gates is a species that needs
        # its own, and these are the eight places its disc set can silently stop being a
        # gasket while still rendering as a bag of circles.
        ("the gasket's candidates taken smallest-first (the greedy pack keeps the specks)",
         "int c = cl[q].Rho.CompareTo(cl[p].Rho);",
         "int c = cl[p].Rho.CompareTo(cl[q].Rho);", "trip", "rho histogram"),
        ("the overlap epsilon's sign flipped (tangency itself reads as a clash)",
         "const float GasketOverlapEps = 1e-4f;",
         "const float GasketOverlapEps = -1e-4f;", "trip", "discs shipped, model built"),
        ("RingPoints stops leaving the ring CLOSED (every repeated unit becomes an arc)",
         "                into.Add(into[0]);",
         "                if (samples < 0) into.Add(into[0]);", "trip", "rings emit their full"),
        ("the lane taken as the RECURSION LEVEL instead of the size OCTAVE",
         "d.Lane = Mathf.Clamp(Mathf.FloorToInt(o), 0, lanes - 1);",
         "d.Lane = Mathf.Clamp(d.Level, 0, lanes - 1);", "trip", "size octave"),
        ("RingSamplesFor stops reading StepSize (the element's long axis stops buying coarseness)",
         "int n = Mathf.RoundToInt(circ / Mathf.Max(1e-6f, _rules.StepSize));",
         "int n = 48;", "trip", "ring samples shipped"),
        ("the dive-owed set strided over ALL discs instead of the level-0 ones",
         "_diveOwed[top[(int)((long)d * top.Count / _diveQuota)]] = true;",
         "_diveOwed[(int)((long)d * _discs.Count / _diveQuota)] = true;",
         "trip", "not on the level-0 stride"),
        # 0.10 rather than the 0.15 this was first written at, and the difference is the
        # point: at 0.15 only Mass's tangency row fires and every other element is caught by
        # the DISC-COUNT row instead, so the control reads as trip-by-accident. 0.10 is the
        # largest rate at which the median third-gap clears 0.05 on all four (measured
        # 0.055-0.163), i.e. the smallest perturbation that makes the tangency gate itself do
        # the catching.
        ("Inscribe's relaxation rate dropped 0.6 -> 0.10 (children stop being tangent to three)",
         "const float GasketRelaxRateDefault = 0.6f;",
         "const float GasketRelaxRateDefault = 0.10f;", "trip", "median THIRD tangency gap"),
        # RE-ANCHORED to a gate that does not depend on the authoring. This control was first
        # held by `check_element`'s mean-girth row on the SHIPPED rules, and measured there
        # deleting the line moved the mean girth 11.78% on Space and 2.18% on Time (caught at
        # 2%) but 0.31% / 0.07% on Mass and Charge (not). Then the tuning pass moved Apollonia
        # to exponent 0.30 / floor 0.55, every element's smallest ring landed within a few
        # percent of the floor, and the control went dark on all four: `--self-test` reported
        # it SLIPPING THROUGH, on an unchanged C# line. A control that can be switched off by
        # a retune of the species it guards was measuring the species, not the code — so it
        # is now held by `check_girth_floor`, which forces the floor to 0.95 (where it binds
        # on any authoring) and compares the lift it buys on both sides.
        ("the ring girth FLOOR dropped (the finest octaves go back to invisible threads)",
         "                    girth = Mathf.Max(girth, Mathf.Clamp01(_rules.RingGirthFloor));\n",
         "", "trip", "girth floor forced to 0.95"),
        # THE MASTER SWITCH's own control: a gasket column read on the path every species
        # takes. Nothing else in this file can see it — the three prior species author 0 in
        # every gasket column, so on their real rows this line is a no-op; only the poison
        # test, which sets all ten to Apollonia's values while leaving GasketLevels 0, makes
        # it visible.
        ("a gasket column read OUTSIDE the master switch (the girth floor applied to every "
         "species)",
         "                float girth = girthOverride > 0f ? girthOverride : taper + (1f - taper) * u;\n",
         "                float girth = girthOverride > 0f ? girthOverride : taper + (1f - taper) * u;\n"
         "                girth = Mathf.Max(girth, Mathf.Clamp01(_rules.RingGirthFloor));\n",
         "trip", "read outside the master switch"),
        # A NON-CONTROL, and it is here to be PROVEN inert rather than to fire. The skip is an
        # optimisation — past level 1 a triple of three old discs was already searched last
        # level — but "already searched" is only the same as "cannot contribute" if the greedy
        # clash test would have rejected the re-found child anyway, and removing it also shifts
        # every candidate's enumeration index, which is the TIE-BREAK KEY. Either could have
        # changed the disc set. It is checked by comparing the gasket verb's output byte for
        # byte rather than by running the gates, because a small change could pass them.
        ("(inert) the level-skip dropped from the triple search — a pure optimisation",
         "                            if (lvl > 1 && a < start && b < start && c < start) continue;\n",
         "",
         "inert"),

        ("the saddle order gone sharpness-major again (the belt defect, §50.5)",
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

    def gasket_fingerprint():
        """Every disc, its lay order, N and rho_ref, for all four Apollonia elements — the
        whole of what the species is, as raw harness text. An `inert` control has to be held
        against THIS rather than against the gates: a mutation that moved two children would
        pass every statistical bound and still not be a pure optimisation."""
        return "\n".join(run("gasket", e, 0, 0, 0, GRID,
                             *M.rules_for(e, "Apollonia").as_list()) for e in M.ELEMENTS)

    baseline = gasket_fingerprint()
    try:
        for entry in mutations:
            label, find, replace = entry[0], entry[1], entry[2]
            mode = entry[3] if len(entry) > 3 else "trip"
            # The gate this control was WRITTEN for, as a substring of its message. Without
            # it a control is only ever watched to trip SOMETHING, and "something" is usually
            # the prism count — measured, the relaxation-rate control's first failure is the
            # per-octave disc count on Charge while the tangency row it exists to exercise is
            # thirty-odd failures further down the list. A control that trips the wrong gate
            # is a control nobody has actually watched fire.
            expect = entry[4] if len(entry) > 4 else None
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
            if mode == "inert":
                # No gate should fire, and nothing about the disc set should move.
                try:
                    same = gasket_fingerprint() == baseline
                    failures = verify(verbose=False)
                except subprocess.CalledProcessError as exc:
                    same, failures = False, [f"the harness exited non-zero: {exc}"]
                if same and not failures:
                    print(f"  {'inert':14s} {label}\n                 -> confirmed: the disc "
                          f"set, the lay order, N and rho_ref are byte-identical on all four "
                          f"elements, and no gate fires")
                else:
                    moved = "the disc set MOVED" if not same else "the disc set held"
                    fired = failures[0] if failures else "no gate fired"
                    print(f"  {'FINDING':14s} {label}\n                 -> {moved}; {fired}")
                    bad.append(f"NOT a pure optimisation ({label}): {moved}; {fired}")
                continue
            try:
                failures = verify(verbose=False)
            except subprocess.CalledProcessError as exc:
                # The FIRST stderr line, not the last: a .NET abort (exit 134) opens with
                # "Unhandled exception. System.…" and then prints a stack whose last line is
                # the shell's own path, and a csc failure opens with the first error. Both
                # name the cause at the top and bury it at the bottom.
                head = [ln for ln in (exc.stderr or "").splitlines() if ln.strip()]
                failures = [f"the harness exited non-zero ({exc.returncode}): "
                            + (head[0].strip() if head else "no diagnostic")]
            named = next((f for f in failures if expect and expect in f), None)
            if failures and expect and named is None:
                status = "WRONG GATE"
                bad.append(f"tripped, but not on the gate it was written for "
                           f"({label}): nothing among {len(failures)} failures says "
                           f"{expect!r}; first was {failures[0]!r}")
            else:
                status = "trips" if failures else "SLIPS THROUGH"
            why = named or (failures[0] if failures else "")
            if len(why) > 96:
                why = why[:93] + "..."
            if len(failures) > 1:
                why += f"   (+{len(failures) - 1} more)"
            print(f"  {status:14s} {label}" + (f"\n                 -> {why}" if why else ""))
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
                    help="mutate the shipped file and assert every gate trips (and "
                         "that the one claimed pure optimisation moves nothing)")
    args = ap.parse_args()

    if args.self_test:
        print("self-test (every control must read 'trips'; the one non-control, which is "
              "a\nclaimed pure optimisation, must read 'inert'):")
        bad = self_test()
        if bad:
            for b in bad:
                print("  " + b, file=sys.stderr)
            return 1
        print("OK: every negative control fired, and the one non-control did not.")
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
