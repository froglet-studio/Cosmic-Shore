#!/usr/bin/env python3
"""
Compile the SHIPPED SpindleGrownSway.hlsl with clang and prove what its header claims.

    python3 Tools/Shaders/verify_spindle_grown_sway.py

  T1  it compiles clean (-Wall -Werror, the prism-shader harness's shim)
  T2  Strength = 0 is a BIT-IDENTICAL passthrough, whatever the two vectors hold   <- the blast-radius control
      (with its negative control: Strength = 1 must differ)
  T3  the offset is the kit's wave, SwaySin * sin(t) + SwayCos * cos(t), t = Clock * Frequency + Phase,
      to float precision, on 2,000 random vertices (the formula organic.py bakes and the lab plays)
  T4  it is exactly periodic in the clock: a whole period later the vertex is back
  T5  the peak |offset| is never more than |SwaySin| + |SwayCos| (the number unity_port.py grows a mesh's
      bounds by, so a creature at the edge of the screen is not culled while its wingtip is in view)
"""
import ctypes
import math
import os
import random
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import verify_prism_shard3d as H  # noqa: E402  (shared shim + clang flags)
from verify_spindle_sway import translate, f32  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/SpindleGrownSway.hlsl")

ABI = r"""
extern "C" void grown(float px, float py, float pz, float sx, float sy, float sz, float cx, float cy, float cz,
                      float clock, float phase, float strength, float freq, float *ox, float *oy, float *oz)
{
    float3 out;
    SpindleGrownSway_float(mk3(px, py, pz), mk3(sx, sy, sz), mk3(cx, cy, cz), clock, phase, strength, freq, out);
    *ox = out.x; *oy = out.y; *oz = out.z;
}
"""


def build(src_text):
    tmp = tempfile.mkdtemp(prefix="spindlegrown_")
    cpp, so = os.path.join(tmp, "harness.cpp"), os.path.join(tmp, "harness.so")
    with open(cpp, "w") as fh:
        fh.write(H.SHIM)
        fh.write(translate(src_text))
        fh.write(ABI)
    r = subprocess.run(H.clang_cmd() + ["-shared", "-fPIC", cpp, "-o", so], capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stdout)
        print(r.stderr, file=sys.stderr)
        raise SystemExit("T1 FAIL: SpindleGrownSway.hlsl did not compile")
    lib = ctypes.CDLL(so)
    lib.grown.restype = None
    lib.grown.argtypes = [ctypes.c_float] * 13 + [ctypes.POINTER(ctypes.c_float)] * 3
    return lib


def call(lib, p, s, c, clock, phase, strength, freq):
    o = [ctypes.c_float() for _ in range(3)]
    lib.grown(*[ctypes.c_float(v) for v in (*p, *s, *c, clock, phase, strength, freq)], *[ctypes.byref(x) for x in o])
    return tuple(x.value for x in o)


def main():
    src = open(HLSL).read()
    assert "SpindleGrownSway_float" in src, "entry point renamed: the graph node would not bind"
    lib = build(src)
    print("T1 OK    compiles clean")
    rng = random.Random(7)
    vec = lambda k: [rng.uniform(-k, k) for _ in range(3)]

    differs = 0
    for _ in range(2000):
        p, s, c = vec(80.0), vec(1e6), vec(1e6)
        clock, phase, freq = rng.uniform(0, 5000), rng.uniform(0, 6.3), rng.uniform(0, 12)
        out = call(lib, p, s, c, clock, phase, 0.0, freq)
        assert out == tuple(f32(v) for v in p), f"T2 FAIL: Strength 0 moved {p} to {out}"
        differs += call(lib, p, s, c, clock, phase, 1.0, freq) != out
    assert differs > 1990, "T2 FAIL: the negative control did not fire (Strength 1 left the vertex where it was)"
    print("T2 OK    Strength 0 is a bit-identical passthrough on 2,000 vertices; Strength 1 moves them")

    worst = 0.0
    for _ in range(2000):
        p, s, c = vec(80.0), vec(12.0), vec(12.0)
        clock, phase, freq = rng.uniform(0, 600), rng.uniform(0, 6.3), rng.uniform(0.5, 8)
        t = f32(f32(clock) * f32(freq)) + f32(phase)
        out = call(lib, p, s, c, clock, phase, 1.0, freq)
        for i in range(3):
            want = f32(p[i]) + f32(s[i]) * math.sin(f32(t)) + f32(c[i]) * math.cos(f32(t))
            worst = max(worst, abs(out[i] - want))
    assert worst < 2e-4, f"T3 FAIL: {worst} from the kit's wave"
    print(f"T3 OK    the kit's wave to {worst:.1e} u on 2,000 vertices")

    p, s, c = [3.0, -2.0, 11.0], [1.5, 7.5, 0.0], [0.0, -2.0, 0.4]
    freq = 2 * math.pi * 0.5
    a, b = call(lib, p, s, c, 10.0, 0.7, 1.0, freq), call(lib, p, s, c, 12.0, 0.7, 1.0, freq)
    assert max(abs(a[i] - b[i]) for i in range(3)) < 1e-3, f"T4 FAIL: a period later {a} is {b}"
    print("T4 OK    periodic in the clock")

    peak = 0.0
    for k in range(4000):
        out = call(lib, p, s, c, k * 0.0005, 0.0, 1.0, freq)
        peak = max(peak, math.dist(out, [f32(v) for v in p]))
    bound = math.hypot(*s) + math.hypot(*c)
    assert peak <= bound + 1e-4, f"T5 FAIL: peak {peak} over |S| + |C| = {bound}"
    print(f"T5 OK    peak offset {peak:.3f} u, inside |SwaySin| + |SwayCos| = {bound:.3f}")
    print("OK")


if __name__ == "__main__":
    main()
