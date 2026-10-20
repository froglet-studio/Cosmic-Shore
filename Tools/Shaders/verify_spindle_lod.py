#!/usr/bin/env python3
"""
Compile the SHIPPED SpindleLod.hlsl with clang and prove its contract (asset-surgery 4.5c).

    python3 Tools/Shaders/verify_spindle_lod.py              # compile + run every check
    python3 Tools/Shaders/verify_spindle_lod.py --self-test  # prove the checks fire on broken shaders

What is proven, each against the real file (only a short, listed set of spelling substitutions is
applied - every constant and expression is the shipped one):

  1. PASS-THROUGH. Girth 0 (not an LOD material) returns the vertex and the lace untouched, and an
     all-zero global set (no SpindleLodSystem running) never produces a NaN or moves a vertex.
  2. THE NEAR -> MID SWAP IS INVISIBLE in the shader: below convergeStart the mid surface returns
     the lace exactly and the vertex sits where the mesh put it.
  3. THE MID -> FAR SWAP IS INVISIBLE: at and past convergeEnd every mid vertex sits on the
     centreline plus its (inflated) radial arm - the far thread's own construction - and the mid
     surface returns exactly what the far thread does.
  4. THE THREAD NEVER GOES SUB-PIXEL: once a strand's true width falls under minThreadPixels it is
     drawn at exactly minThreadPixels, and its alpha is paid down by exactly the inflation
     (coverage x drawn width = true width), up to the inflation cap.
  5. IT LEAVES: alpha is zero at farFadeEnd, and zero when the death clock reads 1.
  6. CONVERGENCE IS MONOTONE in distance (a sweep: no tier ever un-converges as you pull away).
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(REPO, "Assets/_Graphics/Materials/Graphs/SpindleLod.hlsl")

SUBS = [
    (r"\bout float3\b", "float3&"),
    (r"\bout float\b", "float&"),
    (r"\bfloat3\(", "mk3("),
    (r"\bfloat4x4\b", "f4x4"),
]

SHIM = r'''
#include <cmath>
#include <cstdio>
#include <algorithm>
typedef float float3 __attribute__((ext_vector_type(3)));
typedef float float4 __attribute__((ext_vector_type(4)));
static float3 mk3(float a, float b, float c) { float3 r = {a, b, c}; return r; }
struct f4x4 { float _m00,_m01,_m02,_m03,_m10,_m11,_m12,_m13,_m20,_m21,_m22,_m23,_m30,_m31,_m32,_m33; };
static float hmax(float a, float b) { return a > b ? a : b; }
static float hmin(float a, float b) { return a < b ? a : b; }
#define max hmax
#define min hmin
static float hlsl_abs(float a) { return std::fabs(a); }
#define abs hlsl_abs
static float saturate(float x) { return x < 0.f ? 0.f : (x > 1.f ? 1.f : x); }
static float clamp(float x, float a, float b) { return x < a ? a : (x > b ? b : x); }
static float smoothstep(float a, float b, float x) { float t = saturate((x - a) / (b - a)); return t * t * (3.f - 2.f * t); }
static float lerp(float a, float b, float t) { return a + (b - a) * t; }
static float3 lerp(float3 a, float3 b, float t) { return a + (b - a) * t; }
static float dot(float3 a, float3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
static float length(float3 a) { return std::sqrt(dot(a, a)); }
static float3 normalize(float3 a) { return a / length(a); }
static f4x4 g_model;
static f4x4 GetObjectToWorldMatrix() { return g_model; }
static float4 _WorldSpaceCameraPos;
static float4 _ScreenParams;
static float4 unity_OrthoParams;
static f4x4 UNITY_MATRIX_P;
#include "spindle_lod_extract.hlsl"
#undef max
#undef min
#undef abs

static int g_fail = 0;
#define CHECK(cond, ...) do { if (!(cond)) { std::fprintf(stderr, "FAIL: " __VA_ARGS__); std::fprintf(stderr, "\n"); g_fail++; } } while (0)

static void place(float dist, float scaleXY) {
    g_model = f4x4{scaleXY,0,0,0, 0,scaleXY,0,0, 0,0,6.2f,0, 0,0,0,1};
    _WorldSpaceCameraPos = float4{0, 0, -dist, 0};
}
static void globals(float cs, float ce, float fs, float fe, float minPx) {
    _SpindleLodBands = float4{cs, ce, fs, fe};
    _SpindleLodLook = float4{minPx, 0.35f, 0.55f, 1.35f};
    _SpindleLodLook2 = float4{0.85f, 0, 0, 0};
}
static bool finite3(float3 v) { return std::isfinite(v.x) && std::isfinite(v.y) && std::isfinite(v.z); }

int main() {
    _ScreenParams = float4{1920, 1080, 0, 0};
    unity_OrthoParams = float4{0, 0, 0, 0};
    UNITY_MATRIX_P = f4x4{}; UNITY_MATRIX_P._m11 = 1.7320508f;           // 60 degree vertical FOV
    const float G = 1.4011f, R = 0.0808f;
    const float3 pos = mk3(0.31f, -0.12f, -0.4f), toAxis = mk3(-0.25f, 0.1f, 0.0f), radial = mk3(0.06f, -0.02f, 0.01f);
    const float3 lace = mk3(0.2f, 0.3f, 0.9f), dull = mk3(0.0f, 0.03f, 1.0f), bright = mk3(0.37f, 0.4f, 0.96f);
    const float3 nrm = mk3(0, 0, -1), view = mk3(0, 0.2f, -1);
    float3 out; float3 col; float a;

    // 1. pass-through: girth 0, with the real globals and with all-zero globals
    for (int zero = 0; zero < 2; zero++) {
        if (zero) globals(0, 0, 0, 0, 0); else globals(180, 386, 1300, 1900, 1.25f);
        for (float d : {1.f, 50.f, 400.f, 5000.f}) {
            place(d * G, 1.f);
            SpindleLodVertex_float(pos, toAxis, radial, 0.f, R, out);
            CHECK(length(out - pos) == 0.f, "girth 0 moved a vertex (zero=%d d=%g)", zero, d);
            SpindleLodSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, 0.f, R, col, a);
            CHECK(length(col - lace) == 0.f && a == 0.4f, "girth 0 changed the lace (zero=%d)", zero);
        }
    }
    globals(0, 0, 0, 0, 0);
    for (float d : {1.f, 400.f, 5000.f}) {
        place(d * G, 1.f);
        SpindleLodVertex_float(pos, toAxis, radial, G, R, out);
        CHECK(finite3(out) && length(out - pos) < 1e-6f, "unset globals moved a vertex at %g girths", d);
        SpindleLodSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, G, R, col, a);
        CHECK(finite3(col) && std::isfinite(a) && length(col - lace) < 1e-6f && std::fabs(a - 0.4f) < 1e-6f,
              "unset globals changed the lace at %g girths", d);
        SpindleLodThread_float(dull, bright, 0.f, nrm, view, G, R, col, a);
        CHECK(finite3(col) && std::isfinite(a) && a > 0.f, "unset globals: the far thread must still draw");
    }

    const float cs = 180, ce = 386, fs = 1300, fe = 1900, minPx = 1.25f;
    globals(cs, ce, fs, fe, minPx);

    // 2. below convergeStart: exact lace, vertex where the mesh put it (no inflation yet at 120 girths:
    //    the thinnest-on-average strand is still > minPx wide there).
    place(120 * G, 1.f);
    SpindleLodVertex_float(pos, toAxis, radial, G, R, out);
    CHECK(length(out - pos) < 1e-6f, "below convergeStart the vertex moved");
    SpindleLodSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, G, R, col, a);
    CHECK(length(col - lace) < 1e-6f && std::fabs(a - 0.4f) < 1e-6f, "below convergeStart the lace changed");

    // 3. at/after convergeEnd: mid == far construction, mid surface == far thread
    for (float m : {ce, ce * 1.05f, 900.f, fs, 1600.f}) {
        place(m * G, 1.f);
        SpindleLodVertex_float(pos, toAxis, radial, G, R, out);
        float trueWidth = 2.f * R * _ScreenParams.y * 0.5f * UNITY_MATRIX_P._m11 / (m * G);
        float k = clamp(minPx / trueWidth, 1.f, 32.f);
        float3 farCtor = (pos - radial) + toAxis + radial * k;
        CHECK(length(out - farCtor) < 1e-5f, "at %g girths the mid vertex is not the far thread's", m);
        float3 c2; float a2;
        SpindleLodSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, G, R, col, a);
        SpindleLodThread_float(dull, bright, 0.f, nrm, view, G, R, c2, a2);
        CHECK(length(col - c2) < 1e-5f && std::fabs(a - a2) < 1e-5f, "at %g girths mid surface != far thread", m);
    }

    // 4. minimum width: drawn width = minPx below it, coverage pays exactly
    for (float m : {500.f, 800.f, 1200.f}) {
        place(m * G, 1.f);
        SpindleLodView v = SpindleLod_View(G);
        float trueWidth = 2.f * R * v.scaleXY * v.pxPerUnit;
        float k = SpindleLod_Inflate(v, R);
        if (trueWidth < minPx && minPx / trueWidth < 32.f)
            CHECK(std::fabs(trueWidth * k - minPx) < 1e-4f, "at %g girths the thread is drawn %g px, not %g", m, trueWidth * k, minPx);
        float3 c; float al;
        SpindleLod_Thread(dull, bright, 0.f, nrm, view, v, R, c, al);
        float fade = 1.f - smoothstep(fs, fe, m);
        CHECK(std::fabs(al - 0.85f * fade / k) < 1e-5f, "at %g girths coverage alpha %g != %g", m, al, 0.85f * fade / k);
    }
    { // a scaled-up limb is the same picture at the same girth count
        place(700 * G * 3.f, 3.f);
        SpindleLodView v3 = SpindleLod_View(G);
        place(700 * G, 1.f);
        SpindleLodView v1 = SpindleLod_View(G);
        CHECK(std::fabs(v3.metric - v1.metric) < 1e-3f, "girth metric is not scale-free");
        CHECK(std::fabs(SpindleLod_Inflate(v3, R) - SpindleLod_Inflate(v1, R)) < 1e-4f, "inflation is not scale-free");
    }

    // 5. it leaves
    place(fe * G, 1.f);
    SpindleLodThread_float(dull, bright, 0.f, nrm, view, G, R, col, a);
    CHECK(a == 0.f, "alpha at farFadeEnd is %g", a);
    place(600 * G, 1.f);
    SpindleLodThread_float(dull, bright, 1.f, nrm, view, G, R, col, a);
    CHECK(a == 0.f, "a fully evaporated thread still has alpha %g", a);
    SpindleLodThread_float(dull, bright, 0.f, nrm, view, G, R, col, a);
    CHECK(a > 0.f, "a living thread at 600 girths has no alpha");

    // 6. convergence is monotone in distance
    float prev = -1.f;
    for (float m = 1.f; m < 3000.f; m *= 1.02f) {
        float c = SpindleLod_Converge(m);
        CHECK(c >= prev - 1e-7f, "convergence went backwards at %g girths", m);
        prev = c;
    }
    CHECK(SpindleLod_Converge(cs) == 0.f && SpindleLod_Converge(ce) == 1.f, "convergence band edges");

    if (g_fail) { std::fprintf(stderr, "%d check(s) failed\n", g_fail); return 1; }
    std::printf("SpindleLod.hlsl: every contract check passed\n");
    return 0;
}
'''


def extract(text):
    for pat, rep in SUBS:
        text = re.sub(pat, rep, text)
    return text


def compile_and_run(hlsl_text):
    clang = shutil.which("clang++") or shutil.which("clang")
    if not clang:
        print("clang not found - cannot compile SpindleLod.hlsl here (install clang; this check is not skippable in review)")
        return 2, ""
    with tempfile.TemporaryDirectory() as td:
        open(os.path.join(td, "spindle_lod_extract.hlsl"), "w").write(extract(hlsl_text))
        src = os.path.join(td, "harness.cpp")
        open(src, "w").write(SHIM)
        exe = os.path.join(td, "harness")
        build = subprocess.run([clang, "-x", "c++", "-std=c++17", "-O1", "-Wall", "-Werror", "-Wno-unused-function",
                                "-I", td, src, "-o", exe, "-lm"], capture_output=True, text=True)
        if build.returncode != 0:
            return 1, build.stderr
        run = subprocess.run([exe], capture_output=True, text=True)
        return run.returncode, run.stdout + run.stderr


def self_test():
    text = open(HLSL).read()
    mutants = {
        "convergence ignores its band": ("return SpindleLod_Ramp(_SpindleLodBands.x, _SpindleLodBands.y, metric);",
                                         "return SpindleLod_Ramp(_SpindleLodBands.x, _SpindleLodBands.y * 1.5, metric);"),
        "coverage not paid": ("float coverage = 1.0 / SpindleLod_Hold(v, strandRadius);", "float coverage = 1.0;"),
        "girth guard dropped": ("    if (!(Girth > 0.0)) return;\n    SpindleLodView v = SpindleLod_View(Girth);\n    float converge",
                                "    SpindleLodView v = SpindleLod_View(Girth);\n    float converge"),
        "lace closes on another schedule": ("float close = SpindleLod_Converge(v.metric);",
                                            "float close = SpindleLod_Ramp(_SpindleLodBands.x, _SpindleLodBands.z, v.metric);"),
        "width hold not ramped on convergence": ("return lerp(1.0, SpindleLod_Inflate(v, strandRadius), SpindleLod_Converge(v.metric));",
                                                 "return SpindleLod_Inflate(v, strandRadius);"),
        "far fade never reaches zero": ("float farFade = 1.0 - SpindleLod_Ramp(", "float farFade = 1.02 - SpindleLod_Ramp("),
    }
    failures = 0
    for name, (old, new) in mutants.items():
        assert old in text, f"self-test mutant '{name}' no longer matches the shader - update it"
        code, _ = compile_and_run(text.replace(old, new, 1))
        if code == 0:
            print(f"  mutant NOT caught: {name}")
            failures += 1
    print("self-test:", "OK (%d mutants caught)" % len(mutants) if failures == 0 else f"FAILED ({failures})")
    return 1 if failures else 0


def main():
    if "--self-test" in sys.argv:
        return self_test()
    code, out = compile_and_run(open(HLSL).read())
    print(out.strip())
    return code


if __name__ == "__main__":
    sys.exit(main())
