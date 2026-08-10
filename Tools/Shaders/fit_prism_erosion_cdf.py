#!/usr/bin/env python3
"""
Re-fit the prism erosion's CDF remap (PRISM_EROSION_CDF_LO / _HI in
PrismOcclusionCorridor.hlsl).

The erosion's raw threshold — phase*(1-BAND) + BAND*dNorm over the object-space chunk
lattice — is NOT uniform (the distance term skews it), so it is pushed through a
smoothstep fitted to its own measured CDF, the same rescue that took Worley from 0.140
to 0.0048 coverage error (Docs/PRISM_ANIMATION.md §4.7). THE FIT IS TIED TO THE FIELD'S
PARAMETERS: re-run this after moving PRISM_EROSION_CELL, _BAND, or _REACH, or the
coverage error silently returns and the debris fade-curve bends.

This mirrors the shipped HLSL exactly — the Hoskins hash33/hash13, the 2x2x2 octant
search, frozen sites — and reads the constants out of the HLSL so it cannot drift from
the file it tunes. Pure Python (no numpy): ~200k samples is plenty for a 2-parameter
fit. Prints the fitted LO/HI and the before/after coverage error; pass --bake to write
them into the HLSL (anchored, count-asserted).

Validated 2026-08-10 against a clang-compiled build of the shipped HLSL itself
(/asset-surgery §4.5c): identical raw distribution, and the fitted constants took the
end-to-end |coverage - alpha| from 0.038 to 0.0068 mean / 0.017 worst.
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
    # PrismOcclusionHash3, mirrored (float32 rounding is irrelevant at fit precision)
    p = [frac(p[0] * 0.1031), frac(p[1] * 0.1030), frac(p[2] * 0.0973)]
    d = p[0] * (p[1] + 33.33) + p[1] * (p[0] + 33.33) + p[2] * (p[2] + 33.33)
    p = [x + d for x in p]
    return [frac((p[0] + p[1]) * p[2]), frac((p[0] + p[0]) * p[1]), frac((p[1] + p[0]) * p[0])]


def hash13(p):
    p = [frac(x * 0.1031) for x in p]
    d = p[0] * (p[2] + 31.32) + p[1] * (p[1] + 31.32) + p[2] * (p[0] + 31.32)
    p = [x + d for x in p]
    return frac((p[0] + p[1]) * p[2])


def raw_samples(cell, band, reach, n):
    rng = random.Random(20260810)
    out = []
    for _ in range(n):
        pos = [rng.random() - 0.5 for _ in range(3)]
        ent = [rng.uniform(-20.0, 20.0) for _ in range(3)]
        h = hash33(ent)
        q = [pos[i] / cell + h[i] * 64.0 for i in range(3)]
        base = [math.floor(q[i] - 0.5) for i in range(3)]
        best, owner = 1e9, base
        for dz in (0, 1):
            for dy in (0, 1):
                for dx in (0, 1):
                    c = [base[0] + dx, base[1] + dy, base[2] + dz]
                    s = hash33(c)
                    dd = sum((c[i] + s[i] - q[i]) ** 2 for i in range(3))
                    if dd < best:
                        best, owner = dd, c
        dn = min(1.0, math.sqrt(best) / reach)
        phase = hash13([owner[i] + 17.0 for i in range(3)])
        out.append(phase * (1.0 - band) + band * dn)
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
    cell = read_const(text, "PRISM_EROSION_CELL")
    band = read_const(text, "PRISM_EROSION_BAND")
    reach = read_const(text, "PRISM_EROSION_REACH")
    cur_lo = read_const(text, "PRISM_EROSION_CDF_LO")
    cur_hi = read_const(text, "PRISM_EROSION_CDF_HI")

    print(f"lattice: CELL={cell} BAND={band} REACH={reach}; current fit LO={cur_lo} HI={cur_hi}")
    raws = raw_samples(cell, band, reach, 200_000)

    best = None
    lo = -0.30
    while lo <= 0.35:
        hi = max(lo + 0.2, 0.60)
        while hi <= 1.40:
            e = coverage_error(raws[::20], lo, hi)   # coarse pass on a decimated set
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
            print("\nThe baked constants are STALE for this lattice — re-run with --bake.",
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
