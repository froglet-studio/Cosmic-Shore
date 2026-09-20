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

  * The walk is NOT held prism-for-prism, and that is a statement about arithmetic rather
    than a gap in the proof. It is a sequential recurrence with a turn gate, the C# runs in
    float32 and the model in float64, and one step landing a hair either side of
    `dot(t, want) < maxTurnCos` ends a curve in one and not the other. The first divergence
    index is REPORTED so a real transcription error (which diverges at step 1, not step
    800) is still loud.

  verify_mandelbulb_flora_tables.py
  verify_mandelbulb_flora_tables.py --self-test   # mutate the shipped file six ways and
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
    out = run("shipped", element, w[0], w[1], w[2], grid, seed, budget, *rules.as_list())
    prisms, curves = [], 0
    for line in out.splitlines():
        t = line.split()
        if not t:
            continue
        if t[0] == "p":
            # p theta phi off dive tanA tanB tanR length girth roll curve lane [pose x9]
            prisms.append(M.Prism(float(t[1]), float(t[2]), float(t[3]), float(t[4]),
                                  float(t[5]), float(t[6]), float(t[7]), float(t[8]),
                                  float(t[9]), float(t[10]), int(t[11]), int(t[12])))
        elif t[0] == "done":
            curves = int(t[2])
    return prisms, curves


def octiles(prisms):
    """Equal-AREA bands in cos(theta). Equal-theta bands would report a plant that only
    grew at its poles as evenly covered."""
    bins = [0] * 8
    for p in prisms:
        bins[min(7, int((1 - math.cos(p.theta)) / 2 * 8))] += 1
    return bins


def check_element(element, fail, verbose=True, species="FractalFoliage"):
    rules = M.rules_for(element, species)
    degree, tables = M.load_tables()

    # 1. the pure function, exactly
    mine = M.reconstruct(degree, M.compose(tables[element], 0.0, 0.0, 0.0), GRID, GRID // 2)
    theirs = shipped_field(element)
    if len(mine) != len(theirs):
        fail(f"{element}: field size {len(theirs)} vs model {len(mine)}")
        return
    worst = max(abs(a - b) for a, b in zip(mine, theirs))
    scale = max(abs(v) for v in mine)
    if worst > 2e-4 * max(scale, 1.0):
        fail(f"{element}: reconstructed field differs by {worst:.3g} (scale {scale:.3g})")

    # 2. the seed set
    surface = M.Surface(mine, GRID, GRID // 2)
    seeds = M.build_seeds(surface, rules)
    out = run("shipped", element, 0, 0, 0, GRID, SEED, 1, *rules.as_list())
    their_seeds = int(out.splitlines()[0].split()[2])
    if their_seeds != len(seeds):
        fail(f"{element}: {their_seeds} seeds shipped, model built {len(seeds)}")

    # 3. the walk, by its statistics
    mine_p, mine_c, _ = M.grow(surface, rules, SEED, BUDGET)
    theirs_p, theirs_c = shipped_prisms(element, rules)

    if len(theirs_p) != len(mine_p):
        fail(f"{element}: {len(theirs_p)} prisms shipped, model laid {len(mine_p)}")
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
    first = None
    for i, (p, q) in enumerate(zip(mine_p, theirs_p)):
        dphi = abs(p.phi - q.phi)
        dphi = min(dphi, 2 * math.pi - dphi)
        if abs(p.theta - q.theta) > 1e-3 or dphi > 1e-3:
            first = i
            break

    if verbose:
        print(f"  {element:6s} field {worst:.2e}  seeds {their_seeds}  "
              f"prisms {len(theirs_p)}  curves {theirs_c}/{mine_c}  "
              f"bands {filled_theirs}/8 (drift {drift:.0%})  "
              f"walk agrees to prism {first if first is not None else len(theirs_p)}")


def verify(verbose=True):
    failures = []

    def fail(msg):
        failures.append(msg)

    self = run("selftest")
    if "selftest ok" not in self:
        failures.append("harness selftest failed:\n" + self)
    elif verbose:
        print("  harness selftest ok (SH round-trip, Compose linearity, Pose inverts)")

    # BOTH species, because they share one growth rule and differ only in their curve
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
        ("radial lift stored along the NORMAL instead of the ray",
         "position = scratch.Dir * (scratch.Radius + a.RadialOffset);",
         "position = scratch.Dir * scratch.Radius + n * a.RadialOffset;"),
        ("the SH root-2 factor dropped from the reconstruction",
         "cm[m] += Root2 * b * coeffs[l * (l + 1) + m];",
         "cm[m] += b * coeffs[l * (l + 1) + m];"),
        ("phi wrapped with a plain modulo (negative index)",
         "int ia = ((i0 % W) + W) % W, ib = ((i0 + 1) % W + W) % W;",
         "int ia = i0 % W, ib = (i0 + 1) % W;"),
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
    ]
    original = open(SHIPPED).read()
    backup = tempfile.mktemp(suffix=".cs")
    shutil.copy(SHIPPED, backup)
    bad = []
    try:
        for label, find, replace in mutations:
            if find not in original:
                bad.append(f"MUTATION NOT APPLICABLE ({label}) — the shipped file no longer "
                           f"contains the line this control mutates; re-anchor it.")
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
