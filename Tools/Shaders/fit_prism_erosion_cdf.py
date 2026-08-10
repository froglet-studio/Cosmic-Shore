#!/usr/bin/env python3
"""
Re-fit the prism erosion's CDF remap (PRISM_EROSION_CDF_LO / _HI in
PrismOcclusionCorridor.hlsl).

The erosion is a per-face WIPE: each face of the debris cube gets one jagged front
(hashed direction + phase) that sweeps across as the clock Opacity falls. Its raw
threshold — face stagger + the wipe coordinate — is NOT uniform over rendered
fragments (the projection of a square onto a hashed direction is trapezoidal), so it
is pushed through a smoothstep fitted to its measured CDF, the same rescue that took
Worley from 0.140 to 0.0048 (Docs/PRISM_ANIMATION.md §4.7). THE FIT IS TIED TO THE
FIELD'S PARAMETERS: re-run this after moving PRISM_EROSION_STAGGER, _WIGGLE, or
_WIGGLE_FREQ, or the coverage error silently returns and the debris fade-curve bends.

Two things keep this honest:
  * It mirrors the shipped HLSL exactly — the Hoskins hashes, the face pick, the
    normalized projection, the value-noise jag — and reads every constant out of the
    HLSL, so it cannot drift from the file it tunes.
  * It samples CUBE FACES, not the cube volume — only face fragments ever render, and
    the two distributions differ enough to matter (a volume-sampled fit measured
    0.0067 on itself and 0.040 on the real face population; the face-sampled fit
    measures 0.0034 end-to-end).

Pure Python, no numpy. Prints the fitted LO/HI and both errors; pass --bake to write
them into the HLSL (anchored, count-asserted).

Validated 2026-08-10 against a clang-compiled build of the shipped HLSL itself
(/asset-surgery §4.5c): identical raw distribution, and the fitted constants measure
0.0034 mean / 0.016 worst |coverage - alpha| through the real compiled function.
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


def raw_samples(half_extent, stagger, wiggle, wiggle_freq, n):
    rng = random.Random(20260810)
    out = []
    for _ in range(n):
        # FACE fragments — the only ones that render on a closed box
        pos = [rng.uniform(-0.5, 0.5) for _ in range(3)]
        axis = rng.randrange(3)
        pos[axis] = half_extent if pos[axis] >= 0.0 else -half_extent
        ent = [rng.uniform(-20.0, 20.0) for _ in range(3)]

        mag = [abs(v) for v in pos]
        if mag[0] >= mag[1] and mag[0] >= mag[2]:
            uv, face = (pos[1], pos[2]), (1.0 if pos[0] >= 0 else 2.0)
        elif mag[1] >= mag[2]:
            uv, face = (pos[0], pos[2]), (3.0 if pos[1] >= 0 else 4.0)
        else:
            uv, face = (pos[0], pos[1]), (5.0 if pos[2] >= 0 else 6.0)
        uv = (uv[0] / half_extent, uv[1] / half_extent)

        e = hash33(ent)
        h = hash33([face, e[0] * 64.0, e[1] * 64.0])
        ang = 6.28318530718 * h[0]
        dx, dy = math.cos(ang), math.sin(ang)
        w01 = (uv[0] * dx + uv[1] * dy) / (abs(dx) + abs(dy)) * 0.5 + 0.5
        c = (uv[0] * -dy + uv[1] * dx) * wiggle_freq + h[2] * 64.0
        ci = math.floor(c)
        cf = c - ci
        cf = cf * cf * (3.0 - 2.0 * cf)
        jag = (1 - cf) * hash13([ci, face, e[2] * 64.0]) + cf * hash13([ci + 1.0, face, e[2] * 64.0])
        w01 = max(0.0, min(1.0, w01 + (jag - 0.5) * wiggle))
        out.append(h[1] * stagger + w01 * (1.0 - stagger))
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
    half_extent = read_const(text, "PRISM_EROSION_HALF_EXTENT")
    stagger = read_const(text, "PRISM_EROSION_STAGGER")
    wiggle = read_const(text, "PRISM_EROSION_WIGGLE")
    wiggle_freq = read_const(text, "PRISM_EROSION_WIGGLE_FREQ")
    cur_lo = read_const(text, "PRISM_EROSION_CDF_LO")
    cur_hi = read_const(text, "PRISM_EROSION_CDF_HI")

    print(f"wipe: HALF_EXTENT={half_extent} STAGGER={stagger} WIGGLE={wiggle} "
          f"FREQ={wiggle_freq}; current fit LO={cur_lo} HI={cur_hi}")
    raws = raw_samples(half_extent, stagger, wiggle, wiggle_freq, 200_000)

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
