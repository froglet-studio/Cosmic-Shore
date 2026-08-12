#!/usr/bin/env python3
"""
Re-fit the mass crystal's Shepard-tone dither CDF remap (SHEPARD_DITHER_CDF_LO / _HI in
ShepardToneDither.hlsl).

The kernel is a distance-to-owner cellular fill over a jittered lattice, sampled on the
crystal's own DIRECTION sphere (Docs/SHEPARD_TONE.md). Raw gauge distance is far from
uniform — it clusters around its mean with almost nothing at either extreme — so the
threshold is pushed through a smoothstep fitted to its measured CDF. Without that remap,
coverage does not track alpha and a shell thins out in visible lurches instead of smoothly.

THE FIT IS TIED TO THE FIELD'S PARAMETERS. Re-run this after changing SHEPARD_DITHER_CELLS,
either gauge normalisation, the gauge selection, SHEPARD_DITHER_SEED_SPAN, the seed
derivation, or the hash. Every one of those is read out of the HLSL below rather than
restated here, so this tool cannot silently drift from the file it tunes.

It fits against the POOLED distribution of the four shipped shell windows rather than a
single seed: each shell offsets the hash by a seed derived from its own [Start, Stop], and
the Hoskins hash's uniformity varies a little with its argument, so a fit to one seed is
biased for the other three. Pooling costs ~0.004 on the best shell and buys ~0.01 on the
worst.

WHY THE NUMBER IS ALLOWED TO BE LOOSER THAN THE PRISM CORRIDOR'S. Alpha on this shader is a
UNIFORM — one value for the whole shell, from the Shepard window — so there is no spatial
gradient band anywhere in the effect and a coverage error cannot become a spatial artefact.
It is purely time-domain: a ~1% bend in how fast a shell thins. (Same reasoning
fit_prism_erosion_cdf.py records for its own trapezoidal fit.)

`frac` here is FLOOR-based, matching HLSL. That is not a detail: a trunc-based fractional
part (Python's `math.modf`, numpy's `np.modf`) disagrees for every negative input, the
lattice produces negative coordinates constantly, and a fit made that way is a fit to a
different hash than the one that ships. Caught by compiling the shipped HLSL with clang
(/asset-surgery §4.5c) and diffing — do that again if these constants ever look wrong.

THE MIRROR IS STATISTICAL, NOT BITWISE. hash3 folds a ~1e4 magnitude through frac(), so its
low bits do not survive float32 rounding — this float64 mirror and the shipped build produce
a statistically identical but pointwise different pattern. Validated 2026-08-12 against a
clang build of the shipped HLSL: compiles clean under -Wall in all three gauge branches,
alpha pass-through exact, KS distance mirror-vs-shipped <= 0.006 per window, and end-to-end
coverage measured through the compiled function at 0.0094 mean / 0.0151 max (ensemble),
0.0117 mean on the worst shell. Those are the numbers quoted in the HLSL.

Pure Python, no numpy. Prints the fitted LO/HI and the per-window errors; pass --bake to
write them into the HLSL (anchored, count-asserted).
"""

import math
import os
import random
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(REPO, "Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl")

# The four shipped shell windows (Start, Stop), read off the mass-crystal materials:
# ActiveMassCrystalMaterial{,1,2,3}. Each shell's lattice seed is derived from its own
# window, so these ARE the four realisations the fit has to serve.
WINDOWS = [(0.33, 0.0), (0.66, 0.33), (1.0, 0.66), (1.03, 0.98)]

GAUGE_NAMES = {0: "SPHERE", 1: "OCTA", 2: "CUBE"}


def read_const(text, name):
    m = re.search(rf"^static const float {re.escape(name)} = ([-\d.]+);", text, re.M)
    assert m, f"{name} not found in the HLSL"
    return float(m.group(1))


def read_gauge(text):
    m = re.search(r"^#define SHEPARD_DITHER_GAUGE SHEPARD_DITHER_GAUGE_(\w+)$", text, re.M)
    assert m, "SHEPARD_DITHER_GAUGE selection not found in the HLSL"
    inverse = {v: k for k, v in GAUGE_NAMES.items()}
    assert m.group(1) in inverse, f"unknown gauge {m.group(1)}"
    return inverse[m.group(1)]


def frac(x):
    return x - math.floor(x)


def hash3(p):
    """PrismOcclusionHash3 / ShepardToneHash3 — the float-only Hoskins family."""
    p = [frac(p[0] * 0.1031), frac(p[1] * 0.1030), frac(p[2] * 0.0973)]
    d = p[0] * (p[1] + 33.33) + p[1] * (p[0] + 33.33) + p[2] * (p[2] + 33.33)  # dot(p3, p3.yxz + 33.33)
    p = [p[0] + d, p[1] + d, p[2] + d]
    return [frac((p[0] + p[1]) * p[2]),   # (p3.xxy + p3.yxx) * p3.zyx
            frac((p[0] + p[0]) * p[1]),
            frac((p[1] + p[0]) * p[0])]


def seed_of(start, stop, span):
    return [frac(start * 17.0 + stop * 7.0) * span,
            frac(start * 23.0 + stop * 13.0) * span,
            frac(start * 29.0 + stop * 19.0) * span]


def field(direction, cells, seed, gauge, octa_norm, cube_norm):
    q = [direction[0] * cells, direction[1] * cells, direction[2] * cells]
    o = [math.floor(q[0]), math.floor(q[1]), math.floor(q[2])]
    best = 8.0
    for dz in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                c = [o[0] + dx, o[1] + dy, o[2] + dz]
                h = hash3([c[0] + seed[0], c[1] + seed[1], c[2] + seed[2]])
                ox, oy, oz = (c[0] + h[0]) - q[0], (c[1] + h[1]) - q[1], (c[2] + h[2]) - q[2]
                if gauge == 1:
                    g = (abs(ox) + abs(oy) + abs(oz)) * octa_norm
                elif gauge == 2:
                    g = max(abs(ox), abs(oy), abs(oz)) * cube_norm
                else:
                    g = ox * ox + oy * oy + oz * oz
                best = min(best, g)
    return math.sqrt(best) if gauge == 0 else best


def smoothstep(lo, hi, x):
    t = min(max((x - lo) / (hi - lo), 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def smoothstep_inverse(a):
    return 0.5 - math.sin(math.asin(max(-1.0, min(1.0, 1.0 - 2.0 * a))) / 3.0)


def sphere_samples(n, rng):
    out = []
    for _ in range(n):
        while True:
            x, y, z = rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)
            r = math.sqrt(x * x + y * y + z * z)
            if r > 1e-6:
                out.append((x / r, y / r, z / r))
                break
    return out


def errors(raws, lo, hi, alphas):
    u = sorted(smoothstep(lo, hi, r) for r in raws)
    n = len(u)
    devs = []
    for a in alphas:
        # count of u < a, by bisection on the sorted list
        import bisect
        devs.append(abs(bisect.bisect_left(u, a) / n - a))
    return sum(devs) / len(devs), max(devs)


def main():
    text = open(HLSL, encoding="utf-8").read()
    cells = read_const(text, "SHEPARD_DITHER_CELLS")
    span = read_const(text, "SHEPARD_DITHER_SEED_SPAN")
    octa = read_const(text, "SHEPARD_DITHER_OCTA_NORM")
    cube = read_const(text, "SHEPARD_DITHER_CUBE_NORM")
    gauge = read_gauge(text)
    cur_lo = read_const(text, "SHEPARD_DITHER_CDF_LO")
    cur_hi = read_const(text, "SHEPARD_DITHER_CDF_HI")

    n = 60000
    for i, arg in enumerate(sys.argv):
        if arg == "--samples":
            n = int(sys.argv[i + 1])

    print(f"  gauge={GAUGE_NAMES[gauge]} cells={cells:g} seedSpan={span:g} "
          f"octaNorm={octa:g} cubeNorm={cube:g}  ({n} samples/window)")

    rng = random.Random(20260812)
    dirs = sphere_samples(n, rng)
    alphas = [0.02 + 0.01 * k for k in range(97)]

    per_window = {}
    pooled = []
    for w in WINDOWS:
        seed = seed_of(w[0], w[1], span)
        raws = [field(d, cells, seed, gauge, octa, cube) for d in dirs]
        per_window[w] = raws
        pooled += raws

    # Least-squares fit of smoothstep(lo,hi,.) to the pooled empirical CDF. Because the
    # remap is monotone, coverage(alpha) = F(lo + (hi-lo)*S^-1(alpha)); asking that to
    # equal alpha is a straight linear regression of the empirical quantiles against
    # S^-1(alpha). Exact and instant — no grid search.
    pooled.sort()
    xs = [smoothstep_inverse(a) for a in alphas]
    ys = [pooled[min(len(pooled) - 1, int(a * len(pooled)))] for a in alphas]
    mx = sum(xs) / len(xs)
    my = sum(ys) / len(ys)
    num = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    den = sum((x - mx) ** 2 for x in xs)
    slope = num / den
    lo = my - slope * mx
    hi = lo + slope

    print(f"  fitted   LO={lo:.5f} HI={hi:.5f}   (file currently LO={cur_lo:.5f} HI={cur_hi:.5f})")
    worst = 0.0
    for w in WINDOWS:
        mean, mx_ = errors(per_window[w], lo, hi, alphas)
        worst = max(worst, mean)
        print(f"    window {str(w):14s} |coverage-alpha| mean={mean:.5f} max={mx_:.5f}")
    emean, emax = errors(pooled, lo, hi, alphas)
    print(f"    ENSEMBLE                 mean={emean:.5f} max={emax:.5f}   worst shell mean={worst:.5f}")

    if "--bake" not in sys.argv:
        print("\n  (--bake to write these into the HLSL)")
        return 0

    new = text
    for name, value in (("SHEPARD_DITHER_CDF_LO", lo), ("SHEPARD_DITHER_CDF_HI", hi)):
        new, k = re.subn(rf"^(static const float {name} = )[-\d.]+;",
                         rf"\g<1>{value:.5f};", new, count=1, flags=re.M)
        assert k == 1, f"could not rewrite {name}"
    if new == text:
        print("\n  already baked.")
        return 0
    open(HLSL, "w", encoding="utf-8").write(new)
    print(f"\n  baked LO={lo:.5f} HI={hi:.5f} into {os.path.relpath(HLSL, REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
