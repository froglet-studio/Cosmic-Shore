#!/usr/bin/env python3
"""
Compile the SHIPPED SpindleSway.hlsl with clang and prove what the doc claims.

/asset-surgery §4.5c: a numpy port validates the design, only compiling the real file
validates the FILE. This reads Assets/_Graphics/Materials/Graphs/SpindleSway.hlsl off
disk, applies the same short mechanical substitution list the other prism-shader
harnesses use, compiles it with -Wall -Werror, and calls the real entry point.

Every claim in the HLSL's header comment is a test here, because the one that matters
most is a claim about code that does NOTHING:

  T1  it compiles clean
  T2  Amplitude = 0 is a BIT-IDENTICAL passthrough      <- the blast-radius control
  T3  z = 0 gives exactly zero offset                   <- a spindle cannot tear off its root
  T4  the offset is exactly LINEAR in z                 <- the unit-free shear claim
  T5  the offset is exactly LINEAR in Amplitude         <- "Amplitude is a slope"
  T6  the tip traces a 2D path, not a line              <- never invisible edge-on
  T7  _Phase actually decorrelates                      <- Spindle.cs's 8 buckets now do something
  T8  peak |offset| / (|z| * Amplitude) is reported     <- the culling-envelope number

T2 carries its own negative control (a non-zero Amplitude must differ), because a
passthrough test passes just as quietly against a function that was never called.

Usage:  python3 Tools/Shaders/verify_spindle_sway.py
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
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/SpindleSway.hlsl")

ABI = r"""
extern "C" void sway(float px, float py, float pz,
                     float clock, float phase, float amp, float freq,
                     float *ox, float *oy, float *oz)
{
    float3 out;
    SpindleSway_float(mk3(px, py, pz), clock, phase, amp, freq, out);
    *ox = out.x; *oy = out.y; *oz = out.z;
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
    tmp = tempfile.mkdtemp(prefix="spindlesway_")
    cpp = os.path.join(tmp, "harness.cpp")
    so = os.path.join(tmp, "harness.so")
    with open(cpp, "w") as fh:
        fh.write(H.SHIM)
        fh.write("\n#undef min\n#undef max\n" if False else "")
        fh.write(translate(src_text))
        fh.write(ABI)
    cmd = H.clang_cmd() + ["-shared", "-fPIC", cpp, "-o", so]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stdout); print(r.stderr, file=sys.stderr)
        raise SystemExit("T1 FAIL: SpindleSway.hlsl did not compile")
    lib = ctypes.CDLL(so)
    lib.sway.restype = None
    lib.sway.argtypes = [ctypes.c_float] * 7 + [ctypes.POINTER(ctypes.c_float)] * 3
    return lib


def f32(v):
    """Round a Python double through float32.

    The harness passes probes as c_float and reads c_float back, so comparing a
    RESULT against the original double measures the literal's own rounding
    (c_float(-217.1).value is -217.10000610351562) and reports it as shader error.
    Every exactness claim below compares float32 to float32.
    """
    return ctypes.c_float(v).value


def call(lib, p, clock, phase, amp, freq):
    ox, oy, oz = (ctypes.c_float() for _ in range(3))
    lib.sway(*[ctypes.c_float(v) for v in (p[0], p[1], p[2], clock, phase, amp, freq)],
             ctypes.byref(ox), ctypes.byref(oy), ctypes.byref(oz))
    return (ox.value, oy.value, oz.value)


def offset(lib, p, clock, phase, amp, freq):
    o = call(lib, p, clock, phase, amp, freq)
    return (o[0] - f32(p[0]), o[1] - f32(p[1]), o[2] - f32(p[2]))


def main():
    src = open(HLSL).read()
    # Guard-rail: the substitution list is written against the LANGUAGE, but the
    # entry point must still be there under the name the graph node calls.
    assert "SpindleSway_float" in src, "entry point renamed — the graph node would not bind"
    lib = build(src)
    print("T1 PASS  compiles clean with -Wall -Werror")

    fails = []

    # ---- T2: Amplitude 0 is a bit-identical passthrough -----------------------
    probes = [(1.0, -2.0, 3.0), (0.0, 0.0, 0.0), (-103.4, 114.2, -217.1),
              (7.0, 7.0, 132.0), (0.5, -0.5, 1.0)]
    worst = 0.0
    for p in probes:
        for clock in (0.0, 1.0, 37.25, 1e4):
            for phase in (0.0, 2.35, math.pi):
                for freq in (0.0, 1.9, 12.6):
                    o = call(lib, p, clock, phase, 0.0, freq)
                    worst = max(worst, max(abs(o[i] - f32(p[i])) for i in range(3)))
    if worst == 0.0:
        print("T2 PASS  Amplitude=0 is bit-identical passthrough (worst delta exactly 0)")
    else:
        fails.append(f"T2 FAIL  Amplitude=0 moved a vertex by {worst}")
    # negative control: the function must actually be doing something otherwise
    ctrl = max(abs(v) for v in offset(lib, (1.0, 1.0, 10.0), 3.0, 0.7, 0.12, 2.0))
    if ctrl > 1e-6:
        print(f"T2 ctrl  PASS  Amplitude=0.12 does move it ({ctrl:.4f}) — T2 is not vacuous")
    else:
        fails.append("T2 ctrl FAIL  a non-zero Amplitude moved nothing — the test is vacuous")

    # ---- T3: z = 0 is exactly anchored ---------------------------------------
    worst = 0.0
    for clock in (0.0, 3.3, 99.0):
        for phase in (0.0, 1.1, 5.9):
            worst = max(worst, max(abs(v) for v in offset(lib, (9.0, -4.0, 0.0), clock, phase, 0.5, 4.0)))
    if worst == 0.0:
        print("T3 PASS  z=0 offset is exactly zero — the root can never tear away")
    else:
        fails.append(f"T3 FAIL  z=0 moved by {worst}")

    # ---- T4/T5: exact linearity ----------------------------------------------
    base = offset(lib, (1.0, 1.0, 1.0), 5.0, 0.9, 0.1, 3.0)
    worst_z = worst_a = 0.0
    for k in (0.5, 2.0, 17.0, -3.0, 217.105):
        got = offset(lib, (1.0, 1.0, k), 5.0, 0.9, 0.1, 3.0)
        want = tuple(v * k for v in base)
        worst_z = max(worst_z, max(abs(got[i] - want[i]) / max(1.0, abs(want[i])) for i in range(3)))
    for k in (0.5, 2.0, 10.0):
        got = offset(lib, (1.0, 1.0, 1.0), 5.0, 0.9, 0.1 * k, 3.0)
        want = tuple(v * k for v in base)
        worst_a = max(worst_a, max(abs(got[i] - want[i]) / max(1.0, abs(want[i])) for i in range(3)))
    print(f"T4 {'PASS' if worst_z < 1e-5 else 'FAIL'}  offset is linear in z         (rel err {worst_z:.2e})")
    print(f"T5 {'PASS' if worst_a < 1e-5 else 'FAIL'}  offset is linear in Amplitude (rel err {worst_a:.2e})")
    if worst_z >= 1e-5: fails.append("T4")
    if worst_a >= 1e-5: fails.append("T5")

    # ---- T6: the tip traces a 2D path, never a line ---------------------------
    freq = 2.0
    xs, ys = [], []
    for i in range(4000):
        t = i * (2 * math.pi / freq) / 400.0     # ten primary periods
        o = offset(lib, (0.0, 0.0, 1.0), t, 0.0, 1.0, freq)
        xs.append(o[0]); ys.append(o[1])
    rx, ry = max(xs) - min(xs), max(ys) - min(ys)
    if rx > 1e-3 and ry > 1e-3:
        print(f"T6 PASS  2D path: x range {rx:.4f}, y range {ry:.4f} "
              f"(secondary is {ry / rx:.2f} of primary — subordinate, not a corkscrew)")
    else:
        fails.append(f"T6 FAIL  degenerate path x={rx} y={ry}")

    # ---- T7: _Phase decorrelates Spindle.cs's 8 buckets ------------------------
    buckets = [2 * math.pi * i / 8 for i in range(8)]
    sigs = [tuple(round(offset(lib, (0, 0, 1.0), t, ph, 1.0, 2.0)[0], 6)
                  for t in (0.0, 0.11, 0.27)) for ph in buckets]
    if len(set(sigs)) == 8:
        print("T7 PASS  all 8 Spindle.cs phase buckets produce distinct motion")
    else:
        fails.append(f"T7 FAIL  only {len(set(sigs))} of 8 phase buckets are distinct")

    # ---- T8: culling envelope --------------------------------------------------
    peak = 0.0
    for i in range(6000):
        t = i * 0.01
        o = offset(lib, (0.0, 0.0, 1.0), t, 0.0, 1.0, 2.0)
        peak = max(peak, math.hypot(o[0], o[1]))
    print(f"T8 INFO  peak lateral |offset| = {peak:.4f} x (|z| x Amplitude)")
    print(f"         -> a mesh reaching |z|=Z sweeps up to {peak:.3f}*Amplitude*Z sideways;")
    print(f"            expand a renderer's bounds by that if edge-of-screen culling pops.")

    if fails:
        for f in fails:
            print(f, file=sys.stderr)
        sys.exit(1)
    print("OK")


if __name__ == "__main__":
    main()
