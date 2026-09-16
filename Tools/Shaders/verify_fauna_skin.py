#!/usr/bin/env python3
"""
Compile the SHIPPED FaunaSkin.hlsl with clang and prove what its header claims.

/asset-surgery §4.5c: a numpy port validates the design, only compiling the real file
validates the FILE. This reads Assets/_Graphics/Materials/Graphs/FaunaSkin.hlsl off
disk, applies the same mechanical substitution list the other prism-shader harnesses
use, compiles it with -Wall -Werror, and calls the real entry points.

The two claims that matter most are claims about code that does NOTHING, because a
splice into a graph twelve prefabs draw with is only reviewable if its defaults are a
provable passthrough (the argument _SwayAmplitude makes in SpindleSway.hlsl):

  T1  it compiles clean
  T2  RimStrength = 0, Pulse = (1,1) is a BIT-IDENTICAL passthrough of Base
  T3  FlowSpeed = (0,0) is a BIT-IDENTICAL passthrough of BaseOffset
  T4  the rim is exactly LINEAR in Fresnel and in RimStrength
  T5  the breath stays inside [Pulse.x, Pulse.y] and REACHES both ends
  T6  the rim can only ever BRIGHTEN            <- why it is an add, not a multiply
  T7  the flow is exactly LINEAR in Clock
  T8  the breath and the flow do not phase-lock <- reported, not gated

T2 and T3 each carry a negative control (a non-default dial must differ), because a
passthrough test passes just as quietly against a function that was never called.

Usage:  python3 Tools/Shaders/verify_fauna_skin.py
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

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/FaunaSkin.hlsl")

ABI = r"""
extern "C" void shade(float br, float bg, float bb,
                      float rr, float rg, float rb,
                      float fres, float clock, float rim, float pmin, float pmax,
                      float *ox, float *oy, float *oz)
{
    float3 out;
    FaunaSkinShade_float(mk3(br, bg, bb), mk3(rr, rg, rb), fres, clock, rim,
                         mk2(pmin, pmax), out);
    *ox = out.x; *oy = out.y; *oz = out.z;
}

extern "C" void flow(float bx, float by, float clock, float sx, float sy,
                     float *ox, float *oy)
{
    float2 out;
    FaunaSkinFlow_float(mk2(bx, by), clock, mk2(sx, sy), out);
    *ox = out.x; *oy = out.y;
}
"""


def translate(text):
    for pat, repl in H.SUBS:
        text = re.sub(pat, repl, text)
    # clang ext_vector mixes a vector with a float scalar, not a double literal.
    return re.sub(
        r'(?<![\w.])(\d+\.\d*(?:[eE][+-]?\d+)?|\.\d+(?:[eE][+-]?\d+)?|\d+[eE][+-]?\d+)(?![fF\w.])',
        r'\1f', text)


def build(src_text):
    tmp = tempfile.mkdtemp(prefix="faunaskin_")
    cpp = os.path.join(tmp, "harness.cpp")
    link, ext = H.lib_flags()
    so = os.path.join(tmp, "harness" + ext)
    with open(cpp, "w") as fh:
        fh.write(H.SHIM)
        fh.write(translate(src_text))
        fh.write(ABI)
    r = subprocess.run(H.clang_cmd() + link + [cpp, "-o", so],
                       capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stdout)
        print(r.stderr, file=sys.stderr)
        raise SystemExit("T1 FAIL: FaunaSkin.hlsl did not compile")
    lib = ctypes.CDLL(so)
    F = ctypes.c_float
    P = ctypes.POINTER(F)
    lib.shade.restype = None
    lib.shade.argtypes = [F] * 11 + [P] * 3
    lib.flow.restype = None
    lib.flow.argtypes = [F] * 5 + [P] * 2
    return lib


def f32(v):
    return ctypes.c_float(v).value


def main():
    src = open(HLSL).read()
    lib = build(src)
    print("T1 PASS  FaunaSkin.hlsl compiles clean (-Wall -Werror)")

    F = ctypes.c_float

    def shade(base, rim, fres, clock, strength, pulse):
        ox, oy, oz = F(), F(), F()
        lib.shade(*[F(x) for x in base], *[F(x) for x in rim],
                  F(fres), F(clock), F(strength), F(pulse[0]), F(pulse[1]),
                  ctypes.byref(ox), ctypes.byref(oy), ctypes.byref(oz))
        return (ox.value, oy.value, oz.value)

    def flow(base, clock, speed):
        ox, oy = F(), F()
        lib.flow(F(base[0]), F(base[1]), F(clock), F(speed[0]), F(speed[1]),
                 ctypes.byref(ox), ctypes.byref(oy))
        return (ox.value, oy.value)

    probes = [(0.0, 0.0, 0.0), (0.03, 0.11, 0.87), (1.11, 1.13, 1.26),
              (0.47, 0.54, 1.22), (2.5, 0.0, 0.25)]
    clocks = [0.0, 0.37, 1.0, 3.1415926, 9.9, 60.0, 601.7]
    fresnels = [0.0, 0.17, 0.5, 0.83, 1.0]
    RIM = (1.1131275, 1.1268682, 1.2596959)   # Blue shielded rim, the shipped neutral

    # ── T2 rim off + flat pulse is a bit-identical passthrough ───────────────
    worst = 0.0
    for b in probes:
        for c in clocks:
            for f in fresnels:
                out = shade(b, RIM, f, c, 0.0, (1.0, 1.0))
                for got, want in zip(out, b):
                    worst = max(worst, abs(got - f32(want)))
    assert worst == 0.0, "T2 FAIL: default shade is not bit-identical (max %g)" % worst
    ctrl = shade(probes[1], RIM, 1.0, 0.0, 0.6, (1.0, 1.0))
    assert ctrl != tuple(f32(x) for x in probes[1]), \
        "T2 FAIL: negative control did not fire — the rim term is unreachable"
    print("T2 PASS  RimStrength=0, Pulse=(1,1) is bit-identical over %d cases "
          "(control fires)" % (len(probes) * len(clocks) * len(fresnels)))

    # ── T3 flow off is a bit-identical passthrough ───────────────────────────
    worst = 0.0
    for b in [(0.0, 0.5), (-1.25, 3.0), (0.0, 0.0)]:
        for c in clocks:
            got = flow(b, c, (0.0, 0.0))
            worst = max(worst, abs(got[0] - f32(b[0])), abs(got[1] - f32(b[1])))
    assert worst == 0.0, "T3 FAIL: default flow is not bit-identical (max %g)" % worst
    assert flow((0.0, 0.5), 10.0, (0.0, 0.013)) != (0.0, f32(0.5)), \
        "T3 FAIL: negative control did not fire — the flow term is unreachable"
    print("T3 PASS  FlowSpeed=(0,0) is bit-identical (control fires)")

    # ── T4 the rim is linear in Fresnel and in RimStrength ───────────────────
    base = (0.0, 0.0, 0.0)
    unit = shade(base, RIM, 1.0, 0.0, 1.0, (1.0, 1.0))
    worst = 0.0
    for f in fresnels:
        for s in (0.0, 0.25, 0.6, 1.0, 4.0):
            got = shade(base, RIM, f, 0.0, s, (1.0, 1.0))
            for g, u in zip(got, unit):
                worst = max(worst, abs(g - f32(u * f * s)))
    assert worst < 2e-6, "T4 FAIL: rim not linear in Fresnel x RimStrength (%g)" % worst
    print("T4 PASS  rim = Rim * Fresnel * RimStrength exactly (max dev %.2e)" % worst)

    # ── T5 the breath stays inside its band and reaches both ends ────────────
    lo, hi = 0.82, 1.18
    seen_lo = seen_hi = False
    out_of_band = 0
    for i in range(20001):
        c = i * 0.001
        v = shade((1.0, 1.0, 1.0), RIM, 0.0, c, 0.0, (lo, hi))[0]
        if v < f32(lo) - 1e-6 or v > f32(hi) + 1e-6:
            out_of_band += 1
        if abs(v - lo) < 1e-4: seen_lo = True
        if abs(v - hi) < 1e-4: seen_hi = True
    assert out_of_band == 0, "T5 FAIL: %d samples outside [min,max]" % out_of_band
    assert seen_lo and seen_hi, "T5 FAIL: breath never reaches an end (lo=%s hi=%s)" % (seen_lo, seen_hi)
    period = 2 * math.pi / 1.42
    print("T5 PASS  breath spans exactly [%.2f, %.2f], period %.2f s" % (lo, hi, period))

    # ── T6 the rim can only brighten ─────────────────────────────────────────
    bad = 0
    for b in probes:
        for f in fresnels:
            for s in (0.0, 0.3, 0.6, 1.0):
                got = shade(b, RIM, f, 0.0, s, (1.0, 1.0))
                for g, x in zip(got, b):
                    if g < f32(x) - 1e-6: bad += 1
    assert bad == 0, "T6 FAIL: %d samples where the rim DARKENED the base" % bad
    print("T6 PASS  the rim never darkens — it is an add, not a multiply")

    # ── T7 the flow is linear in Clock ───────────────────────────────────────
    worst = 0.0
    sp = (0.0, 0.013)
    for c in clocks:
        got = flow((0.0, 0.5), c, sp)
        worst = max(worst, abs(got[0] - f32(0.0 + c * sp[0])),
                    abs(got[1] - f32(0.5 + c * sp[1])))
    assert worst < 1e-5, "T7 FAIL: flow not linear in Clock (%g)" % worst
    print("T7 PASS  flow = BaseOffset + Clock * FlowSpeed (max dev %.2e)" % worst)

    # ── T8 report the phase relationship ─────────────────────────────────────
    print("T8 INFO  breath 1.42 rad/s (%.2f s); a FlowSpeed of 0.013 uv/s walks one "
          "cell in %.0f s — %.1f breaths, not an integer" %
          (period, 1 / 0.013, (1 / 0.013) / period))

    print("\nALL PASS")


if __name__ == "__main__":
    main()
