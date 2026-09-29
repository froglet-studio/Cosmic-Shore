#!/usr/bin/env python3
"""
Compile the SHIPPED PrismSway.hlsl with clang and prove the lockup claim.

/asset-surgery §4.5c: a numpy port validates the design, only compiling the real file
validates the FILE. This reads both HLSL files off disk, resolves the `#include` the
way Unity's shader compiler will, compiles the pair with -Wall -Werror, and calls both
entry points so the two can be compared against each other in ONE process.

The claim that matters is not that this shader moves a prism — it is that it moves the
prism to exactly where the LIMB'S OWN SURFACE went. So the headline test evaluates both
shaders and demands bit-identical output:

  T1  the pair compiles clean, with the include resolved
  T2  SpanX = SpanY = 0 is a BIT-IDENTICAL passthrough         <- the blast-radius control
  T3  degenerate basis == SpindleSway, BIT-IDENTICALLY         <- one field, not two
  T4  the offset is exactly linear in the limb height          <- it is still a pure shear
  T5  a ROTATED, non-uniformly SCALED prism lands on the limb  <- the change of basis
  T6  the wave constants are INCLUDED, never copied            <- a drift that hides for 8s
  T7  Z0 alone translates the whole prism rigidly              <- what holds a rib on a fin
  T8  peak |offset| / (|zl| * Amplitude) is reported           <- the culling-envelope number

T2 and T3 each carry a negative control, because a passthrough test and an
equality test both pass just as quietly against a function that was never called.

Usage:  python3 Tools/Shaders/verify_prism_sway.py
"""

import ctypes
import math
import os
import re
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import verify_prism_shard3d as H  # noqa: E402  (shared shim + clang flags)
import verify_spindle_sway as S   # noqa: E402  (shared translate())

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPHS = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs")
PRISM = os.path.join(GRAPHS, "PrismSway.hlsl")
SPINDLE = os.path.join(GRAPHS, "SpindleSway.hlsl")

ABI = r"""
extern "C" void prism_sway(float px, float py, float pz, float clock,
                           float sxx, float sxy, float sxz,
                           float syx, float syy, float syz,
                           float ax, float ay, float az,
                           float freq, float phase, float z0,
                           float *ox, float *oy, float *oz)
{
    float3 out;
    PrismSway_float(mk3(px, py, pz), clock, mk3(sxx, sxy, sxz), mk3(syx, syy, syz),
                    mk3(ax, ay, az), mk3(freq, phase, z0), out);
    *ox = out.x; *oy = out.y; *oz = out.z;
}

extern "C" void spindle_sway(float px, float py, float pz,
                             float clock, float phase, float amp, float freq,
                             float *ox, float *oy, float *oz)
{
    float3 out;
    SpindleSway_float(mk3(px, py, pz), clock, phase, amp, freq, out);
    *ox = out.x; *oy = out.y; *oz = out.z;
}
"""


def resolve_includes(text, folder):
    """Inline `#include "Sibling.hlsl"` the way Unity's compiler will.

    Deliberately a real resolve rather than a stub: T6 asserts the constants are
    INCLUDED, and a harness that pasted its own copy would prove nothing about the
    shipped file.
    """
    def sub(m):
        path = os.path.join(folder, m.group(1))
        if not os.path.exists(path):
            raise SystemExit("include not found: " + m.group(1))
        return open(path).read()
    return re.sub(r'^\s*#include\s+"([^"]+)"\s*$', sub, text, flags=re.M)


def build():
    src = resolve_includes(open(PRISM).read(), GRAPHS)
    assert "PrismSway_float" in src, "entry point renamed — the graph node would not bind"
    assert "SpindleSway_float" in src, "the include did not resolve"
    tmp = tempfile.mkdtemp(prefix="prismsway_")
    cpp = os.path.join(tmp, "harness.cpp")
    so = os.path.join(tmp, "harness.so")
    with open(cpp, "w") as fh:
        fh.write(H.SHIM)
        fh.write(S.translate(src))
        fh.write(ABI)
    r = subprocess.run(H.clang_cmd() + ["-shared", "-fPIC", cpp, "-o", so],
                       capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stdout)
        print(r.stderr, file=sys.stderr)
        raise SystemExit("T1 FAIL: PrismSway.hlsl did not compile")
    lib = ctypes.CDLL(so)
    lib.prism_sway.restype = None
    lib.prism_sway.argtypes = [ctypes.c_float] * 16 + [ctypes.POINTER(ctypes.c_float)] * 3
    lib.spindle_sway.restype = None
    lib.spindle_sway.argtypes = [ctypes.c_float] * 7 + [ctypes.POINTER(ctypes.c_float)] * 3
    return lib


def f32(v):
    return ctypes.c_float(v).value


def prism(lib, p, clock, spanx, spany, axis, freq, phase, z0):
    ox, oy, oz = (ctypes.c_float() for _ in range(3))
    args = list(p) + [clock] + list(spanx) + list(spany) + list(axis) + [freq, phase, z0]
    lib.prism_sway(*[ctypes.c_float(v) for v in args],
                   ctypes.byref(ox), ctypes.byref(oy), ctypes.byref(oz))
    return (ox.value, oy.value, oz.value)


def spindle(lib, p, clock, phase, amp, freq):
    ox, oy, oz = (ctypes.c_float() for _ in range(3))
    lib.spindle_sway(*[ctypes.c_float(v) for v in (p[0], p[1], p[2], clock, phase, amp, freq)],
                     ctypes.byref(ox), ctypes.byref(oy), ctypes.byref(oz))
    return (ox.value, oy.value, oz.value)


PROBES = [(1.0, -2.0, 3.0), (0.0, 0.0, 0.0), (-103.4, 114.2, -217.1),
          (7.0, 7.0, 132.0), (0.5, -0.5, 1.0), (0.0, 0.0, 6.2)]


def main():
    lib = build()
    print("T1 PASS  compiles clean with -Wall -Werror, include resolved")
    fails = []

    # ---- T2: no span is a bit-identical passthrough --------------------------
    worst = 0.0
    for p in PROBES:
        for clock in (0.0, 1.7, 93.25):
            o = prism(lib, p, clock, (0, 0, 0), (0, 0, 0), (0, 0, 1), 1.4, 2.1, 5.0)
            worst = max(worst, max(abs(o[i] - f32(p[i])) for i in range(3)))
    moved = prism(lib, PROBES[0], 1.7, (0.12, 0, 0), (0, 0.12, 0), (0, 0, 1), 1.4, 2.1, 5.0)
    ctl = max(abs(moved[i] - f32(PROBES[0][i])) for i in range(3))
    if worst != 0.0:
        fails.append(f"T2 FAIL  zero span moved a vertex by {worst:g}")
    elif ctl == 0.0:
        fails.append("T2 FAIL  negative control did not move either — nothing was called")
    else:
        print(f"T2 PASS  zero span is bit-identical (control moved {ctl:.4f})")

    # ---- T3: the degenerate basis IS SpindleSway ------------------------------
    amp, freq, phase = 0.12, 1.4, 2.1
    worst = 0.0
    for p in PROBES:
        for clock in (0.0, 0.37, 1.7, 93.25, 611.0):
            a = prism(lib, p, clock, (amp, 0, 0), (0, amp, 0), (0, 0, 1), freq, phase, 0.0)
            b = spindle(lib, p, clock, phase, amp, freq)
            worst = max(worst, max(abs(a[i] - b[i]) for i in range(3)))
    # negative control: the wrong secondary axis must NOT agree
    bad = prism(lib, PROBES[0], 1.7, (amp, 0, 0), (0, 0, amp), (0, 0, 1), freq, phase, 0.0)
    ref = spindle(lib, PROBES[0], 1.7, phase, amp, freq)
    ctl = max(abs(bad[i] - ref[i]) for i in range(3))
    if worst != 0.0:
        fails.append(f"T3 FAIL  prism and limb disagree by {worst:g} — two fields, not one")
    elif ctl == 0.0:
        fails.append("T3 FAIL  negative control also agreed — the comparison proves nothing")
    else:
        print(f"T3 PASS  bit-identical to SpindleSway (control differs by {ctl:.4f})")

    # ---- T4: still a pure shear — linear in the limb height ------------------
    p = (1.0, -2.0, 3.0)
    base = None
    worst = 0.0
    for z0 in (1.0, 2.0, 4.0, 8.0):
        o = prism(lib, p, 1.7, (amp, 0, 0), (0, amp, 0), (0, 0, 0), freq, phase, z0)
        d = tuple(o[i] - f32(p[i]) for i in range(3))
        if base is None:
            base, base_z = d, z0
        else:
            exp = tuple(base[i] * (z0 / base_z) for i in range(3))
            worst = max(worst, max(abs(d[i] - exp[i]) for i in range(3)))
    if worst > 2e-6:
        fails.append(f"T4 FAIL  offset is not linear in limb height (err {worst:g})")
    else:
        print(f"T4 PASS  offset is linear in limb height (max err {worst:.2e})")

    # ---- T5: a rotated, non-uniformly scaled prism still lands on the limb ---
    # A rib pitched 17.7 degrees about x (the Clawfish's), carrying a 2.6 x 0.4 x 3.0
    # leaf scale. Bake the basis the way the C# does, then demand that the prism's
    # vertex and the limb point it is bolted to move by the SAME world vector.
    th = math.radians(17.7)
    # prism object -> limb: R_x(th) then the leaf scale
    def p2l(v):
        sx, sy, sz = 2.6, 0.4, 3.0
        x, y, z = v[0] * sx, v[1] * sy, v[2] * sz
        return (x, y * math.cos(th) - z * math.sin(th), y * math.sin(th) + z * math.cos(th))

    def l2p(v):
        x, y, z = v
        y, z = y * math.cos(-th) - z * math.sin(-th), y * math.sin(-th) + z * math.cos(-th)
        return (x / 2.6, y / 0.4, z / 3.0)

    Z0 = 4.3
    spanx = tuple(c * amp for c in l2p((1.0, 0.0, 0.0)))
    spany = tuple(c * amp for c in l2p((0.0, 1.0, 0.0)))
    # Axis is the linear FUNCTIONAL giving the limb-z of a prism-space point: row 3 of p2l
    axis = (p2l((1, 0, 0))[2], p2l((0, 1, 0))[2], p2l((0, 0, 1))[2])
    worst = 0.0
    for v in [(0.4, -0.3, 0.9), (0.0, 0.0, 0.0), (-1.0, 0.5, -0.7)]:
        for clock in (0.31, 2.9, 40.0):
            o = prism(lib, v, clock, spanx, spany, axis, freq, phase, Z0)
            got = p2l(tuple(o[i] - f32(v[i]) for i in range(3)))          # in limb units
            zl = Z0 + p2l(v)[2]
            ref = spindle(lib, (0.0, 0.0, zl), clock, phase, amp, freq)   # the limb point
            exp = (ref[0], ref[1], 0.0)
            worst = max(worst, max(abs(got[i] - exp[i]) for i in range(3)))
    if worst > 4e-5:
        fails.append(f"T5 FAIL  a rotated/scaled prism misses the limb by {worst:g}")
    else:
        print(f"T5 PASS  rotated + non-uniformly scaled prism lands on the limb "
              f"(max err {worst:.2e} limb units)")

    # ---- T6: the wave constants are included, not copied ---------------------
    src = open(PRISM).read()
    redefined = [n for n in ("SPINDLE_SWAY_SECONDARY_WEIGHT", "SPINDLE_SWAY_SECONDARY_RATIO")
                 if re.search(r"#define\s+%s" % n, src)]
    if redefined:
        fails.append("T6 FAIL  PrismSway.hlsl redefines " + ", ".join(redefined))
    elif '#include "SpindleSway.hlsl"' not in src:
        fails.append("T6 FAIL  PrismSway.hlsl does not include SpindleSway.hlsl")
    else:
        print("T6 PASS  both wave constants come from SpindleSway.hlsl")

    # ---- T7: Z0 alone is a rigid translation --------------------------------
    # Tolerance rather than exactness, and the reason is float addition rather than the
    # shader: `(p + c) - p` is not `c` for arbitrary p, so recovering the offset by
    # subtracting the probe measures the SUM's rounding. What is asserted is that the
    # recovered offsets agree to within one ulp of the operands, which is the strongest
    # true statement available from outside the shader.
    worst = 0.0
    axis0 = (0.0, 0.0, 0.0)
    ref = None
    for v in [(0.4, -0.3, 0.9), (0.0, 0.0, 0.0), (-1.0, 0.5, -0.7)]:
        o = prism(lib, v, 2.2, (amp, 0, 0), (0, amp, 0), axis0, freq, phase, 7.0)
        d = tuple(o[i] - f32(v[i]) for i in range(3))
        if ref is None:
            ref = d
        else:
            worst = max(worst, max(abs(d[i] - ref[i]) for i in range(3)))
    if worst > 1e-6:
        fails.append(f"T7 FAIL  a zero axis is not a rigid translation (spread {worst:g})")
    else:
        print(f"T7 PASS  a zero axis translates every vertex alike (spread {worst:.2e})")

    # ---- T8: report the culling envelope ------------------------------------
    peak = 0.0
    for i in range(4001):
        clock = i * 0.01
        o = prism(lib, (0, 0, 0), clock, (1.0, 0, 0), (0, 1.0, 0), (0, 0, 0), freq, phase, 1.0)
        peak = max(peak, math.hypot(o[0], o[1]))
    print(f"T8 INFO  peak |offset| = {peak:.4f} * Amplitude * limbHeight")
    print(f"         -> a prism bolted at limb height Z sweeps up to {peak:.3f}*A*Z sideways;")
    print("            expand its renderer bounds by that if edge-of-screen culling pops.")

    if fails:
        print()
        for f in fails:
            print(f)
        raise SystemExit(1)
    print("\nOK")


if __name__ == "__main__":
    main()
