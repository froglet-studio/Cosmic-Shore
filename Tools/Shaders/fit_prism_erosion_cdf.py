#!/usr/bin/env python3
"""
Re-fit the prism erosion's CDF remap (PRISM_EROSION_CDF_LO / _HI in
PrismOcclusionCorridor.hlsl).

The erosion is a per-face WIPE anchored to UV0 (spin-proof — 2026-08-11): each face
gets one jagged front (per-prism hashed direction) sweeping across as the clock
Opacity falls. Its raw wipe coordinate — the projection of the UV square onto a
hashed direction, plus the value-noise jag — is not exactly uniform, so it is pushed
through a smoothstep fitted to its measured CDF before the END_MARGIN compression
(Docs/PRISM_ANIMATION.md §4.7). THE FIT IS TIED TO THE FIELD'S PARAMETERS: re-run
this after moving PRISM_EROSION_WIGGLE or _WIGGLE_FREQ, or the debris fade-curve
bends. (END_MARGIN and FRINGE sit OUTSIDE the fitted quantity and can be tuned
freely.)

It mirrors the shipped HLSL exactly — the Hoskins hashes, the normalized projection,
the value-noise jag — and reads every constant out of the HLSL, so it cannot drift
from the file it tunes.

IT SAMPLES THE CANONICAL FACE TRIANGLE, NOT THE UV SQUARE, and that correction matters
(2026-09-16). This used to sample the uniform square "which is exactly what renders" —
it is not. Every debris mesh in the game is a TRIANGLE FAN: the shipped
`Assets/_Models/Testing/Prism.asset` is 24 wedges, and both shield meshes are 8 and 24
triangles, and all of them now carry the same canonical UV triangle (0,0) (1,0) (0.5,1).
A triangle is HALF the square's area and is not centrally symmetric, so `w01` — while it
was normalized against the SQUARE's support — never reached both ends, and the thresholds
bunched into the middle of the fade. Measured on the shipped cube BEFORE that pass: the
wipe occupied only 62% of the fade (38% at worst), starting 15% late and finishing a
third early, against a design that says every fragment is gone by END_MARGIN. The shader
now normalizes against the TRIANGLE's own support and this mirrors it, so the fitted
quantity is the one that renders.

Pure Python, no numpy. Prints the fitted LO/HI and both errors; pass --bake to write
them into the HLSL (anchored, count-asserted).

Validated 2026-08-11 against a clang-compiled build of the shipped HLSL itself
(/asset-surgery §4.5c): identical raw distribution; end-to-end coverage through the
real compiled function tracks the margin-compressed ramp within the fringe smear.
"""

import math
import os
import random
import re
import sys

HLSL = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                    "Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl")


def read_const(text, name):
    m = re.search(rf"^static const float {re.escape(name)} = ([-\d.]+);", text, re.M)
    assert m, f"{name} not found in the HLSL"
    return float(m.group(1))


def frac(x):
    return x - math.floor(x)


def hash33(p):
    p = [frac(p[0] * 0.1031), frac(p[1] * 0.1030), frac(p[2] * 0.0973)]
    d = p[0] * (p[1] + 33.33) + p[1] * (p[0] + 33.33) + p[2] * (p[2] + 33.33)
    p = [x + d for x in p]
    return [frac((p[0] + p[1]) * p[2]), frac((p[0] + p[0]) * p[1]), frac((p[1] + p[0]) * p[0])]


def hash13(p):
    p = [frac(x * 0.1031) for x in p]
    d = p[0] * (p[2] + 31.32) + p[1] * (p[1] + 31.32) + p[2] * (p[0] + 31.32)
    p = [x + d for x in p]
    return frac((p[0] + p[1]) * p[2])


def raw_samples(wiggle, wiggle_freq, n):
    rng = random.Random(20260811)
    out = []
    # The canonical face triangle in CENTRED UV, i.e. (0,0) (1,0) (0.5,1) mapped through
    # `uv * 2 - 1`. This is what every debris mesh renders — see the module docstring.
    A, B, C = (-1.0, -1.0), (1.0, -1.0), (0.0, 1.0)
    for _ in range(n):
        r1, r2 = rng.random(), rng.random()
        if r1 + r2 > 1.0:            # fold: uniform BY AREA over the triangle
            r1, r2 = 1.0 - r1, 1.0 - r2
        uv = (A[0] + (B[0] - A[0]) * r1 + (C[0] - A[0]) * r2,
              A[1] + (B[1] - A[1]) * r1 + (C[1] - A[1]) * r2)
        # Stands for the shader's `Velocity + Tangent * 17` — per prism AND per piece.
        ent = [rng.uniform(-20.0, 20.0) for _ in range(3)]
        e = hash33(ent)
        h = hash33([e[0] * 64.0 + 17.0, e[1] * 64.0 + 17.0, e[2] * 64.0 + 17.0])
        ang = 6.28318530718 * h[0]
        dx, dy = math.cos(ang), math.sin(ang)
        # Support of the canonical face triangle along (dx, dy) — mirrors the shipped
        # normalizer exactly (see "the wipe coordinate" in PrismOcclusionCorridor.hlsl).
        s0, s1, s2 = -dx - dy, dx - dy, dy
        s_lo, s_hi = min(s0, s1, s2), max(s0, s1, s2)
        w01 = ((uv[0] * dx + uv[1] * dy) - s_lo) / max(s_hi - s_lo, 1e-5)
        c = (uv[0] * -dy + uv[1] * dx) * wiggle_freq + h[2] * 64.0
        ci = math.floor(c)
        cf = c - ci
        cf = cf * cf * (3.0 - 2.0 * cf)
        jag = (1 - cf) * hash13([ci, h[1] * 64.0, e[2] * 64.0]) + cf * hash13([ci + 1.0, h[1] * 64.0, e[2] * 64.0])
        out.append(max(0.0, min(1.0, w01 + (jag - 0.5) * wiggle)))
    out.sort()
    return out


def smoothstep(lo, hi, x):
    t = max(0.0, min(1.0, (x - lo) / (hi - lo)))
    return t * t * (3.0 - 2.0 * t)


def coverage_error(raws, lo, hi):
    n = len(raws)
    return sum(abs(smoothstep(lo, hi, x) - (i + 0.5) / n) for i, x in enumerate(raws)) / n


def main():
    text = open(HLSL, encoding="utf-8").read()
    wiggle = read_const(text, "PRISM_EROSION_WIGGLE")
    wiggle_freq = read_const(text, "PRISM_EROSION_WIGGLE_FREQ")
    cur_lo = read_const(text, "PRISM_EROSION_CDF_LO")
    cur_hi = read_const(text, "PRISM_EROSION_CDF_HI")

    print(f"wipe: WIGGLE={wiggle} FREQ={wiggle_freq}; current fit LO={cur_lo} HI={cur_hi}")
    raws = raw_samples(wiggle, wiggle_freq, 200_000)

    best = None
    lo = -0.30
    while lo <= 0.40:
        hi = max(lo + 0.2, 0.60)
        while hi <= 1.40:
            e = coverage_error(raws[::20], lo, hi)
            if best is None or e < best[0]:
                best = (e, lo, hi)
            hi += 0.005
        lo += 0.005
    _, flo, fhi = best
    err_fit = coverage_error(raws, flo, fhi)
    err_cur = coverage_error(raws, cur_lo, cur_hi)
    print(f"fitted:  LO={flo:.3f} HI={fhi:.3f}  mean|coverage-alpha| = {err_fit:.5f}")
    print(f"current: LO={cur_lo} HI={cur_hi}  mean|coverage-alpha| = {err_cur:.5f}")

    if "--bake" not in sys.argv:
        if err_cur > err_fit + 0.005:
            print("\nThe baked constants are STALE for this wipe — re-run with --bake.",
                  file=sys.stderr)
            return 1
        print("\nBaked constants are within tolerance of the fresh fit; nothing to do.")
        return 0

    for name, value in (("PRISM_EROSION_CDF_LO", flo), ("PRISM_EROSION_CDF_HI", fhi)):
        new, n = re.subn(rf"^(static const float {name} = )[-\d.]+(;)",
                         rf"\g<1>{value:.3f}\g<2>", text, count=1, flags=re.M)
        assert n == 1, f"could not rewrite {name}"
        text = new
    open(HLSL, "w", encoding="utf-8").write(text)
    print(f"\nBaked LO={flo:.3f} HI={fhi:.3f} into {os.path.relpath(HLSL)}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
