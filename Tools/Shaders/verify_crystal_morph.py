#!/usr/bin/env python3
"""
Compile and RUN the shipped CrystalMorph.hlsl, and assert the four properties the Squirrel's
omni-crystal morph rests on. Per /asset-surgery §4.5c: the file under test is read from the
repo and passed through a short, LISTED set of mechanical substitutions, so what is proven is
the file that ships — not a paraphrase of it.

  1. UNSTAMPED IS IDENTITY. Duration 0 returns the input position untouched at any clock
     value. Every crystal material in the project carries `_CrystalMorph = (0,0,0)`, so
     wiring this node into ShepardGraph changed nothing about any of them.
  2. t = 0 IS THE SOURCE, EXACTLY. This is what makes the hand-off seamless: the morph
     object draws the crystal's own mesh in the crystal's own materials, and on its first
     frame it must be bit-identical to the crystal it replaced.
  3. t = 1 IS THE TARGET, EXACTLY, at every phase. The morph ends ON the octahedra of the
     real shielded prisms — including removing ShepardGraph's own outward shell
     displacement, which the lerp does for free precisely because the splice is LAST.
  4. THE PHASE ORDERS THE TWO JOBS. The crystal's leftover quad faces (phase 0) are fully
     absorbed before the panels (phase 1) begin to land, so nothing is left hanging around
     the shape it was absorbed into. Stagger 0 collapses both to one synchronised move.

Usage:  python3 Tools/Shaders/verify_crystal_morph.py
"""

import os
import re
import subprocess
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(REPO, "Assets/_Graphics/Materials/Graphs/CrystalMorph.hlsl")

# Mechanical substitutions ONLY. Every constant and every expression passes through
# untouched — those are what is being verified. Written as patterns over the language
# feature, never over the current text, so a second `out` parameter cannot slip past.
SUBS = [
    (r"\bout (float4|float3|float2|float)\b", r"\1 &"),   # HLSL out-param -> C++ reference
    (r"\bfloat3\(", "mk3("),                              # vector constructor spelling
    (r"\[unroll\]", ""),
]

SHIM = r"""
#include <cstdio>
#include <cmath>
#include <initializer_list>

// clang's ext_vector_type gives elementwise arithmetic AND arbitrary swizzles, so the
// shipped HLSL compiles with only the substitutions listed above.
typedef float float2 __attribute__((ext_vector_type(2)));
typedef float float3 __attribute__((ext_vector_type(3)));
typedef float float4 __attribute__((ext_vector_type(4)));
static inline float3 mk3(float a, float b, float c) { return (float3){a, b, c}; }
static inline float saturate(float v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }
static inline float3 lerp(float3 a, float3 b, float t) { return a + (b - a) * t; }
// HLSL's max/min promote across float and double literals; C's do not.
static inline float hlsl_max(float a, float b) { return a > b ? a : b; }
#define max hlsl_max
#include "CrystalMorph_extract.hlsl"
#undef max

static const float3 SRC = (float3){1, 2, 3};
static const float4 DST0 = (float4){10, 20, 30, 0};
static const float4 DST1 = (float4){10, 20, 30, 1};

static float ease(float4 tgt, float clock, float3 morph) {
    float3 out;
    CrystalMorph_float(SRC, tgt, clock, morph, out);
    return (out.x - 1.0f) / 9.0f;   // 0 at the source, 1 at the target
}

int main() {
    float3 out;

    // 1. unstamped is identity, at any clock
    for (float clock : {0.0f, 5.0f, 1e9f}) {
        CrystalMorph_float(SRC, DST1, clock, mk3(0, 0, 0), out);
        if (out.x != 1 || out.y != 2 || out.z != 3) { printf("FAIL identity\n"); return 1; }
    }

    const float3 M = mk3(100.0f, 0.45f, 0.35f);   // start, duration, stagger

    // 2 & 3. exact at both ends, at every phase
    for (float ph = 0; ph <= 1.0001f; ph += 0.125f) {
        float4 T = (float4){10, 20, 30, ph};
        if (ease(T, 100.0f, M) != 0.0f) { printf("FAIL t0 phase %f\n", ph); return 1; }
        if (fabsf(ease(T, 100.45f, M) - 1.0f) > 1e-6f) { printf("FAIL t1 phase %f\n", ph); return 1; }
    }

    // monotone, in range, filler never lags the panel
    float p0 = -1, p1 = -1;
    for (int i = 0; i <= 400; i++) {
        float clock = 100.0f + (i / 400.0f) * 0.45f;
        float e0 = ease(DST0, clock, M), e1 = ease(DST1, clock, M);
        if (e0 < -1e-6f || e0 > 1 + 1e-6f || e1 < -1e-6f || e1 > 1 + 1e-6f) { printf("FAIL range\n"); return 1; }
        if (e0 < p0 - 1e-6f || e1 < p1 - 1e-6f) { printf("FAIL monotonic\n"); return 1; }
        if (e1 > e0 + 1e-6f) { printf("FAIL phase order\n"); return 1; }
        p0 = e0; p1 = e1;
    }

    // 4. the phase orders the two jobs
    float fillerDone = ease(DST0, 100.0f + 0.65f * 0.45f, M);
    float panelStart = ease(DST1, 100.0f + 0.35f * 0.45f, M);
    printf("  filler settled by t=0.65: %.7f (want 1)\n", fillerDone);
    printf("  panel still at rest at t=0.35: %.7f (want 0)\n", panelStart);
    if (fabsf(fillerDone - 1.0f) > 1e-6f) { printf("FAIL filler not settled\n"); return 1; }
    if (fabsf(panelStart) > 1e-6f) { printf("FAIL panel already moving\n"); return 1; }

    // stagger 0 collapses to one synchronised move
    const float3 S0 = mk3(100.0f, 0.45f, 0.0f);
    if (fabsf(ease(DST0, 100.225f, S0) - ease(DST1, 100.225f, S0)) > 1e-7f) {
        printf("FAIL stagger 0 not synchronised\n"); return 1;
    }

    printf("  OK\n");
    return 0;
}
"""


def main():
    src = open(HLSL, encoding="utf-8").read()
    for name in ("CrystalMorph_float",):
        assert name in src, f"{HLSL} no longer declares {name} — re-derive the harness"
    for pattern, repl in SUBS:
        src = re.sub(pattern, repl, src)
    assert " out " not in src, "an `out` parameter survived the substitution — it would pass BY VALUE"

    with tempfile.TemporaryDirectory() as tmp:
        open(os.path.join(tmp, "CrystalMorph_extract.hlsl"), "w").write(src)
        open(os.path.join(tmp, "main.cpp"), "w").write(SHIM)
        compiler = "clang++" if _has("clang++") else "g++"
        build = subprocess.run(
            [compiler, "-Wall", "-Wextra", "-O0", "-std=c++17", "-o", "t", "main.cpp"],
            cwd=tmp, capture_output=True, text=True)
        if build.returncode != 0:
            print(build.stdout + build.stderr, file=sys.stderr)
            return 1
        if build.stderr.strip():
            # -Wall is part of the gate: C's scalar overloads are not HLSL's, and a warning
            # here means the harness is silently computing something else.
            print(build.stderr, file=sys.stderr)
            return 1
        run = subprocess.run([os.path.join(tmp, "t")], capture_output=True, text=True)
        print(run.stdout, end="")
        if run.returncode != 0:
            print(run.stderr, file=sys.stderr)
            return 1
    return 0


def _has(exe):
    return subprocess.run(["which", exe], capture_output=True).returncode == 0


if __name__ == "__main__":
    sys.exit(main())
