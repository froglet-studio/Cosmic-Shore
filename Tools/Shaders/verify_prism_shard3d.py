#!/usr/bin/env python3
"""Fit SHARD3D's CDF remap and prove it cannot plate-flash the way SHATTER3D does.

Kernel 6 (PrismOcclusionShard3D) fills Voronoi polyhedra by Euclidean distance-to-owner.
Its level sets are spheres, so they cannot lie flat against a viewed face — the geometric
failure that REJECTED SHATTER3D ON LOOK (2026-08-10). This script compiles the SHIPPED
PrismOcclusionCorridor.hlsl (/asset-surgery §4.5c: clang++, smallest SUBS list, call the
shipped functions, do not reimplement the octave/owner math in Python) and:

  * grid-searches PRISM_OCCLUSION_SHARD3D_CDF_LO / _HI so |coverage − alpha| < 0.01
  * constructs the glancing-plane that makes SHATTER3D exactly constant (range ≈ 0)
    and shows SHARD3D's F1 still varies (sphere curvature L²/(2d), small-but-nonzero)

Usage:
  python3 Tools/Shaders/verify_prism_shard3d.py --bake   # fit + write the two HLSL constants
  python3 Tools/Shaders/verify_prism_shard3d.py --check  # compile + coverage + glancing-plane
  python3 Tools/Shaders/verify_prism_shard3d.py --keep   # leave the temp dir (with either)

Needs clang++. No Unity, no numpy.
"""

from __future__ import annotations

import bisect
import ctypes
import math
import os
import random
import re
import shutil
import subprocess
import sys
import tempfile
import uuid

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl")

# Keep this list SHORT and listed. Order is load-bearing: out float3 before out float,
# or "out float3 owner" becomes "float&3 owner". Do NOT #define out.
SUBS = [
    (r"\[loop\]", ""),
    (r"\[unroll\]", ""),
    (r"\bout float3\b", "float3&"),
    (r"\bout float2\b", "float2&"),
    (r"\bout float\b", "float&"),
    (r"\bfloat3\s*\(", "mk3("),
    (r"\bfloat2\s*\(", "mk2("),
]

SHIM = r"""
// HLSL → C++ shim for PrismOcclusionCorridor.hlsl (/asset-surgery §4.5c).
// ext_vector_type keeps .xyx / .yzx / .zy / .xx / .xxy / .yxx / .zyx / .yxz / .yxx.
#include <cmath>
#include <cstdint>
#include <type_traits>

using std::sin;
using std::cos;
using std::sqrt;
using std::atan2;
using std::abs;
using std::floor;
using std::log2;
using std::exp2;
using std::pow;

using float2 = float __attribute__((ext_vector_type(2)));
using float3 = float __attribute__((ext_vector_type(3)));
using float4 = float __attribute__((ext_vector_type(4)));

template<class A, class B>
static inline float2 mk2(A a, B b) { return float2{(float)a, (float)b}; }
template<class A, class B, class C>
static inline float3 mk3(A a, B b, C c) { return float3{(float)a, (float)b, (float)c}; }

static inline float min(float a, float b) { return a < b ? a : b; }
static inline float max(float a, float b) { return a > b ? a : b; }
static inline float min(float a, double b) { return min(a, (float)b); }
static inline float max(float a, double b) { return max(a, (float)b); }
static inline float min(double a, float b) { return min((float)a, b); }
static inline float max(double a, float b) { return max((float)a, b); }
static inline double min(double a, double b) { return a < b ? a : b; }
static inline double max(double a, double b) { return a > b ? a : b; }
static inline int min(int a, int b) { return a < b ? a : b; }
static inline int max(int a, int b) { return a > b ? a : b; }

static inline float saturate(float v) { return min(1.0f, max(0.0f, v)); }
static inline float clamp(float v, float lo, float hi) { return min(hi, max(lo, v)); }
static inline float lerp(float a, float b, float t) { return a + (b - a) * t; }
static inline float smoothstep(float lo, float hi, float x)
{
    float t = saturate((x - lo) / (hi - lo));
    return t * t * (3.0f - 2.0f * t);
}
static inline float frac(float x) { return x - floor(x); }
static inline float2 frac(float2 v) { return float2{frac(v.x), frac(v.y)}; }
static inline float3 frac(float3 v) { return float3{frac(v.x), frac(v.y), frac(v.z)}; }
static inline float2 floor(float2 v) { return float2{floor(v.x), floor(v.y)}; }
static inline float3 floor(float3 v) { return float3{floor(v.x), floor(v.y), floor(v.z)}; }
static inline float2 sin(float2 v) { return float2{sin(v.x), sin(v.y)}; }
static inline float3 sin(float3 v) { return float3{sin(v.x), sin(v.y), sin(v.z)}; }

static inline float dot(float2 a, float2 b) { return a.x * b.x + a.y * b.y; }
static inline float dot(float3 a, float3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
static inline float length(float2 a) { return sqrt(dot(a, a)); }
static inline float length(float3 a) { return sqrt(dot(a, a)); }

#define SHADERGRAPH_PREVIEW 1
"""

ABI = r"""
extern "C" {

float shard3d_threshold(float x, float y, float z, float angularScale, float time)
{
    return PrismOcclusionShard3D(mk3(x, y, z), angularScale, time);
}

float shatter3d_threshold(float x, float y, float z, float angularScale, float time)
{
    return PrismOcclusionShatter3D(mk3(x, y, z), angularScale, time);
}

float shard3d_f1(float x, float y, float z, float angularScale, float time)
{
    float phase = time * PrismOcclusionMorphRate() * 6.28318530718f;
    float cellWorld = PrismOcclusionOctaveCellWorld(mk3(x, y, z), angularScale, PrismOcclusionShatter3DCell());
    float3 q = mk3(x, y, z) / cellWorld;
    float3 owner;
    float bestSq;
    PrismOcclusionOwner3D(q, phase, owner, bestSq);
    return sqrt(bestSq);
}

void owner3d(float x, float y, float z, float angularScale, float time,
             float* ox, float* oy, float* oz, float* bestSq, float* cellWorld)
{
    float phase = time * PrismOcclusionMorphRate() * 6.28318530718f;
    float cw = PrismOcclusionOctaveCellWorld(mk3(x, y, z), angularScale, PrismOcclusionShatter3DCell());
    float3 q = mk3(x, y, z) / cw;
    float3 owner;
    float bs;
    PrismOcclusionOwner3D(q, phase, owner, bs);
    *ox = owner.x; *oy = owner.y; *oz = owner.z;
    *bestSq = bs;
    *cellWorld = cw;
}

void shatter3d_crack_dir(float ox, float oy, float oz, float* dx, float* dy, float* dz)
{
    // Byte-for-byte the Hash3 + az/cz/sz construction inside PrismOcclusionShatter3D.
    float3 owner = mk3(ox, oy, oz);
    float3 h = PrismOcclusionHash3(owner + 61.0f);
    float az = 6.28318530718f * h.y;
    float cz = 2.0f * h.z - 1.0f;
    float sz = sqrt(max(1.0f - cz * cz, 0.0f));
    *dx = sz * cos(az);
    *dy = sz * sin(az);
    *dz = cz;
}

}
"""


def translate(text: str, live_tuning: int) -> str:
    if live_tuning:
        text, n = re.subn(
            r"^#define PRISM_OCCLUSION_LIVE_TUNING 0\s*$",
            "#define PRISM_OCCLUSION_LIVE_TUNING 1",
            text, count=1, flags=re.M)
        if n != 1:
            raise SystemExit("could not flip PRISM_OCCLUSION_LIVE_TUNING to 1")
    for pat, repl in SUBS:
        text = re.sub(pat, repl, text)
    # clang ext_vector mixes a vector with a float scalar, not a double literal.
    # Suffix every decimal / scientific literal that is not already f/F.
    text = re.sub(
        r'(?<![\w.])(\d+\.\d*(?:[eE][+-]?\d+)?|\.\d+(?:[eE][+-]?\d+)?|\d+[eE][+-]?\d+)(?![fF\w.])',
        r'\1f', text)
    return text


def clang_cmd() -> list[str]:
    return [
        "clang++", "-std=c++17", "-O2",
        "-fno-exceptions", "-fno-rtti",
        "-Wall", "-Werror",
        "-Wno-unused-function", "-Wno-unused-variable", "-Wno-unused-parameter",
        "-Wno-unused-but-set-variable",
    ]


def lib_flags() -> tuple[list[str], str]:
    if sys.platform == "darwin":
        return ["-dynamiclib"], ".dylib"
    return ["-shared", "-fPIC"], ".so"


def compile_lib(workdir: str, hlsl_text: str, live_tuning: int) -> str:
    extract = os.path.join(workdir, f"extract_{live_tuning}.h")
    shim = os.path.join(workdir, "hlsl_shim.h")
    abi = os.path.join(workdir, f"abi_{live_tuning}.cpp")
    with open(shim, "w", encoding="utf-8") as f:
        f.write(SHIM)
    with open(extract, "w", encoding="utf-8") as f:
        f.write(translate(hlsl_text, live_tuning))
    with open(abi, "w", encoding="utf-8") as f:
        f.write(f'#include "hlsl_shim.h"\n#include "extract_{live_tuning}.h"\n')
        f.write(ABI)

    link, ext = lib_flags()
    # Unique filename: dyld/ctypes cache the first CDLL of a given path, so a
    # same-process --bake recompile of shard3d_0.dylib would keep serving the
    # pre-bake constants and fail the compiled coverage check.
    out = os.path.join(workdir, f"shard3d_{live_tuning}_{uuid.uuid4().hex[:8]}{ext}")
    cmd = clang_cmd() + link + ["-o", out, abi]
    r = subprocess.run(cmd, cwd=workdir, capture_output=True, text=True)
    if r.returncode != 0:
        sys.stderr.write(r.stdout)
        sys.stderr.write(r.stderr)
        raise SystemExit(f"clang++ failed (LIVE_TUNING={live_tuning})")
    return out


def load(path: str):
    lib = ctypes.CDLL(path)
    lib.shard3d_threshold.argtypes = [ctypes.c_float] * 5
    lib.shard3d_threshold.restype = ctypes.c_float
    lib.shatter3d_threshold.argtypes = [ctypes.c_float] * 5
    lib.shatter3d_threshold.restype = ctypes.c_float
    lib.shard3d_f1.argtypes = [ctypes.c_float] * 5
    lib.shard3d_f1.restype = ctypes.c_float
    lib.owner3d.argtypes = [ctypes.c_float] * 5 + [ctypes.POINTER(ctypes.c_float)] * 5
    lib.owner3d.restype = None
    lib.shatter3d_crack_dir.argtypes = [ctypes.c_float] * 3 + [ctypes.POINTER(ctypes.c_float)] * 3
    lib.shatter3d_crack_dir.restype = None
    return lib


def read_const(text: str, name: str) -> float:
    m = re.search(rf"^static const float {re.escape(name)} = ([-\d.]+);", text, re.M)
    if not m:
        raise SystemExit(f"missing {name}")
    return float(m.group(1))


def bake_const(text: str, name: str, value: float) -> str:
    new, n = re.subn(
        rf"^(static const float {name} = )[-\d.]+(;)",
        rf"\g<1>{value:.3f}\g<2>",
        text, count=1, flags=re.M)
    assert n == 1, f"could not rewrite {name}"
    return new


def rand_pos(rng: random.Random) -> tuple[float, float, float]:
    # Spread across octaves: tens to thousands of world units.
    r = 10.0 ** rng.uniform(1.0, 3.5)
    theta = rng.uniform(0, 6.28318530718)
    phi = rng.uniform(0, 3.14159265359)
    st, ct = math.sin(theta), math.cos(theta)
    sp, cp = math.sin(phi), math.cos(phi)
    return r * sp * ct, r * sp * st, r * cp


def sample_f1s(lib, n: int, seed: int = 1) -> list[float]:
    rng = random.Random(seed)
    out = []
    for _ in range(n):
        x, y, z = rand_pos(rng)
        scale = rng.uniform(0.5, 2.0)
        t = rng.uniform(0.0, 40.0)
        out.append(lib.shard3d_f1(x, y, z, scale, t))
    return out


def safe_threshold(n: float) -> float:
    return n * 0.998 + 0.001


def python_smoothstep(lo: float, hi: float, x: float) -> float:
    if hi == lo:
        return 0.0 if x < lo else 1.0
    t = max(0.0, min(1.0, (x - lo) / (hi - lo)))
    return t * t * (3.0 - 2.0 * t)


def coverage_error_from_f1(f1s: list[float], lo: float, hi: float) -> float:
    th = sorted(safe_threshold(python_smoothstep(lo, hi, f)) for f in f1s)
    n = len(th)
    err = 0.0
    count = 0
    a = 0.02
    while a < 0.98:
        # fraction with threshold ≤ alpha
        cov = bisect.bisect_right(th, a) / n
        err += abs(cov - a)
        count += 1
        a += 0.02
    return err / count


def coverage_error_compiled(lib, n: int, seed: int = 2) -> float:
    rng = random.Random(seed)
    th = []
    for _ in range(n):
        x, y, z = rand_pos(rng)
        scale = rng.uniform(0.5, 2.0)
        t = rng.uniform(0.0, 40.0)
        th.append(lib.shard3d_threshold(x, y, z, scale, t))
    th.sort()
    err = 0.0
    count = 0
    a = 0.02
    while a < 0.98:
        cov = bisect.bisect_right(th, a) / n
        err += abs(cov - a)
        count += 1
        a += 0.02
    return err / count


def fit_cdf(f1s: list[float]) -> tuple[float, float, float]:
    best = None
    lo = 0.00
    while lo <= 0.40:
        hi = max(lo + 0.20, 0.50)
        while hi <= 1.20:
            e = coverage_error_from_f1(f1s, lo, hi)
            if best is None or e < best[0]:
                best = (e, lo, hi)
            hi += 0.02
        lo += 0.02
    _, blo, bhi = best
    # refine
    lo0, hi0 = blo, bhi
    lo = max(0.0, lo0 - 0.04)
    while lo <= lo0 + 0.04 + 1e-9:
        hi = max(lo + 0.15, hi0 - 0.04)
        while hi <= hi0 + 0.04 + 1e-9:
            e = coverage_error_from_f1(f1s, lo, hi)
            if e < best[0]:
                best = (e, lo, hi)
            hi += 0.005
        lo += 0.005
    return best[1], best[2], best[0]


def orthonormal(dx: float, dy: float, dz: float) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
    ax, ay, az = abs(dx), abs(dy), abs(dz)
    if ax < ay and ax < az:
        bx, by, bz = 0.0, -dz, dy
    elif ay < az:
        bx, by, bz = -dz, 0.0, dx
    else:
        bx, by, bz = -dy, dx, 0.0
    bl = math.sqrt(bx * bx + by * by + bz * bz) or 1.0
    bx, by, bz = bx / bl, by / bl, bz / bl
    cx = dy * bz - dz * by
    cy = dz * bx - dx * bz
    cz = dx * by - dy * bx
    return (bx, by, bz), (cx, cy, cz)


def plane_ok(lib, corners, scale: float, t: float, ox, oy, oz, cw) -> bool:
    for x, y, z in corners:
        cox = ctypes.c_float()
        coy = ctypes.c_float()
        coz = ctypes.c_float()
        bs = ctypes.c_float()
        ccw = ctypes.c_float()
        lib.owner3d(x, y, z, scale, t, ctypes.byref(cox), ctypes.byref(coy),
                    ctypes.byref(coz), ctypes.byref(bs), ctypes.byref(ccw))
        if (abs(cox.value - ox) > 1e-4 or abs(coy.value - oy) > 1e-4
                or abs(coz.value - oz) > 1e-4 or abs(ccw.value - cw) / max(cw, 1e-6) > 1e-4):
            return False
    return True


def glancing_plane_proof(lib, n_try: int = 800, seed: int = 3) -> dict:
    rng = random.Random(seed)
    constructed_ok = 0
    constructed_skip = 0
    shatter_const = 0
    shard_varies = 0
    shatter_ranges = []
    shard_ranges = []
    f1_ranges = []

    while constructed_ok + constructed_skip < n_try:
        x, y, z = rand_pos(rng)
        scale = rng.uniform(0.5, 2.0)
        t = rng.uniform(0.0, 40.0)
        ox = ctypes.c_float()
        oy = ctypes.c_float()
        oz = ctypes.c_float()
        bs = ctypes.c_float()
        cw = ctypes.c_float()
        lib.owner3d(x, y, z, scale, t, ctypes.byref(ox), ctypes.byref(oy),
                    ctypes.byref(oz), ctypes.byref(bs), ctypes.byref(cw))
        dx = ctypes.c_float()
        dy = ctypes.c_float()
        dz = ctypes.c_float()
        lib.shatter3d_crack_dir(ox.value, oy.value, oz.value,
                                ctypes.byref(dx), ctypes.byref(dy), ctypes.byref(dz))
        e1, e2 = orthonormal(dx.value, dy.value, dz.value)
        # Stay inside one 8-world-unit octave-jitter chunk (floor(p*0.125)) AND
        # mostly inside one Voronoi cell. 0.25*cellWorld was large enough that
        # ~90% of random centres skipped; the construction only needs a finite
        # patch, not a quarter-cell.
        L = min(0.08 * cw.value, 3.0)
        corners = []
        for s1, s2 in ((-1, -1), (-1, 1), (1, -1), (1, 1), (0, 0)):
            corners.append((
                x + L * (s1 * e1[0] + s2 * e2[0]),
                y + L * (s1 * e1[1] + s2 * e2[1]),
                z + L * (s1 * e1[2] + s2 * e2[2]),
            ))
        if not plane_ok(lib, corners, scale, t, ox.value, oy.value, oz.value, cw.value):
            constructed_skip += 1
            continue
        constructed_ok += 1
        sh = []
        sd = []
        f1 = []
        for u in range(-3, 4):
            for v in range(-3, 4):
                px = x + (u / 3.0) * L * e1[0] + (v / 3.0) * L * e2[0]
                py = y + (u / 3.0) * L * e1[1] + (v / 3.0) * L * e2[1]
                pz = z + (u / 3.0) * L * e1[2] + (v / 3.0) * L * e2[2]
                sh.append(lib.shatter3d_threshold(px, py, pz, scale, t))
                sd.append(lib.shard3d_threshold(px, py, pz, scale, t))
                f1.append(lib.shard3d_f1(px, py, pz, scale, t))
        sh_range = max(sh) - min(sh)
        sd_range = max(sd) - min(sd)
        f1_range = max(f1) - min(f1)
        shatter_ranges.append(sh_range)
        shard_ranges.append(sd_range)
        f1_ranges.append(f1_range)
        if sh_range < 1e-4:
            shatter_const += 1
        if f1_range > 1e-6:
            shard_varies += 1

    return {
        "ok": constructed_ok,
        "skip": constructed_skip,
        "shatter_const": shatter_const,
        "shard_varies": shard_varies,
        "shatter_median": sorted(shatter_ranges)[len(shatter_ranges) // 2] if shatter_ranges else 0.0,
        "shatter_max": max(shatter_ranges) if shatter_ranges else 0.0,
        "f1_median": sorted(f1_ranges)[len(f1_ranges) // 2] if f1_ranges else 0.0,
        "f1_min": min(f1_ranges) if f1_ranges else 0.0,
        "shard_median": sorted(shard_ranges)[len(shard_ranges) // 2] if shard_ranges else 0.0,
    }


def run(bake: bool, keep: bool) -> int:
    text = open(HLSL, encoding="utf-8").read()
    cur_lo = read_const(text, "PRISM_OCCLUSION_SHARD3D_CDF_LO")
    cur_hi = read_const(text, "PRISM_OCCLUSION_SHARD3D_CDF_HI")
    print(f"HLSL SHARD3D CDF  LO={cur_lo} HI={cur_hi}")

    workdir = tempfile.mkdtemp(prefix="shard3d_")
    try:
        print("compiling LIVE_TUNING=0 …")
        lib0 = compile_lib(workdir, text, 0)
        print("compiling LIVE_TUNING=1 (compile-only) …")
        compile_lib(workdir, text, 1)
        lib = load(lib0)

        if bake:
            print("sampling F1 (n=12000) …")
            f1s = sample_f1s(lib, 12000, seed=1)
            flo, fhi, ferr = fit_cdf(f1s)
            print(f"fitted:  LO={flo:.3f} HI={fhi:.3f}  python |coverage-alpha| = {ferr:.5f}")
            text = bake_const(text, "PRISM_OCCLUSION_SHARD3D_CDF_LO", flo)
            text = bake_const(text, "PRISM_OCCLUSION_SHARD3D_CDF_HI", fhi)
            open(HLSL, "w", encoding="utf-8").write(text)
            print(f"baked into {os.path.relpath(HLSL)}")
            print("recompiling LIVE_TUNING=0 with baked constants …")
            lib0 = compile_lib(workdir, text, 0)
            lib = load(lib0)
            cur_lo, cur_hi = flo, fhi

        print("compiled coverage (n=8000) …")
        err = coverage_error_compiled(lib, 8000, seed=2)
        print(f"compiled SHARD3D |coverage − alpha| = {err:.5f}  (target < 0.01)")
        if err >= 0.01:
            print("FAIL: coverage fidelity is outside the admission rule. Re-run --bake.",
                  file=sys.stderr)
            return 1

        print("glancing-plane proof (constructed SHATTER3D crack planes) …")
        g = glancing_plane_proof(lib)
        print(f"  valid planes {g['ok']}  skipped (octave/owner change) {g['skip']}")
        print(f"  SHATTER3D threshold range < 1e-4 : {g['shatter_const']}/{g['ok']}  "
              f"(median {g['shatter_median']:.2e}, max {g['shatter_max']:.2e})")
        print(f"  SHARD3D F1 range > 1e-6         : {g['shard_varies']}/{g['ok']}  "
              f"(min {g['f1_min']:.2e}, median {g['f1_median']:.2e})")
        print(f"  SHARD3D threshold median range    {g['shard_median']:.4f}  "
              "(spheres, not plates — range is the look, not a defect)")
        if g["ok"] < 40:
            print("FAIL: too few valid constructed planes to trust the proof.", file=sys.stderr)
            return 1
        if g["shatter_const"] / g["ok"] < 0.90:
            print("FAIL: constructed crack planes are not constant on SHATTER3D — "
                  "the plate-flash construction drifted from the shipped kernel.",
                  file=sys.stderr)
            return 1
        if g["shard_varies"] != g["ok"]:
            print("FAIL: SHARD3D F1 was constant on a constructed plane — Euclidean "
                  "distance-to-owner must vary across a finite patch.",
                  file=sys.stderr)
            return 1

        print("\nOK  coverage < 0.01 and SHARD3D cannot plate-flash a SHATTER3D crack plane.")
        print("    Look on real mass at speed is still unearned — do not Bake as CURRENT.")
        return 0
    finally:
        if keep:
            print(f"kept {workdir}")
        else:
            shutil.rmtree(workdir, ignore_errors=True)


def main() -> int:
    args = set(sys.argv[1:])
    if args - {"--bake", "--check", "--keep", "-h", "--help"}:
        print(__doc__)
        return 2
    if "-h" in args or "--help" in args:
        print(__doc__)
        return 0
    bake = "--bake" in args
    keep = "--keep" in args
    # --check is the default (and what CI would call). --bake implies a check after.
    return run(bake=bake, keep=keep)


if __name__ == "__main__":
    sys.exit(main())
