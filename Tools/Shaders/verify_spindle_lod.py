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
INCORPOREAL = os.path.join(REPO, "Assets/_Graphics/Materials/Graphs/IncorporealDepth.hlsl")

SUBS = [
    (r"\bout float3\b", "float3&"),
    (r"\bout float\b", "float&"),
    (r"\bfloat3\(", "mk3("),
    (r"\bfloat4x4\b", "f4x4"),
    (r"\(float3x3\)GetObjectToWorldMatrix\(\)", "to3x3(GetObjectToWorldMatrix())"),
    (r"\bfloat3x3\b", "f3x3"),
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
static float3 hmax(float3 a, float3 b) { return mk3(hmax(a.x, b.x), hmax(a.y, b.y), hmax(a.z, b.z)); }
struct f3x3 { float _m00,_m01,_m02,_m10,_m11,_m12,_m20,_m21,_m22; };
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
static f3x3 to3x3(f4x4 m) { return f3x3{m._m00,m._m01,m._m02,m._m10,m._m11,m._m12,m._m20,m._m21,m._m22}; }
static float3 mul(f3x3 m, float3 v) { return mk3(m._m00*v.x + m._m01*v.y + m._m02*v.z, m._m10*v.x + m._m11*v.y + m._m12*v.z, m._m20*v.x + m._m21*v.y + m._m22*v.z); }
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
static void globals(float cs, float ce, float fs, float fe, float minPx, float style = 0.f) {
    _SpindleLodBands = float4{cs, ce, fs, fe};
    _SpindleLodLook = float4{minPx, 0.35f, 0.55f, 1.35f};
    _SpindleLodLook2 = float4{0.85f, style, 0, 0};
}
// An INDEPENDENT transcription of the prisms' depth effect (DistanceSpreadAndColors + SpreadSubGraph,
// Docs/SWARM_FAUNA.md section 15): eff = over ? far : lerp(-7, far, norm); eff = max(eff, spread).
static float prismEff(float spread, float gain, float norm) {
    float far = spread * gain;
    float e = norm > 1.f ? far : (-7.f + (far + 7.f) * norm);
    return e > spread ? e : spread;
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
    const float3 nrmOS = normalize(mk3(0.6f, -0.7f, 0.2f)), uvf = mk3(0.37f, 0.5f, 0.f);
    const float3 splay = normalize(mk3(0.8f, -0.6f, 0.f));
    float3 out; float3 col; float a;

    // 1. pass-through: girth 0, with the real globals and with all-zero globals
    for (int zero = 0; zero < 2; zero++) {
        if (zero) globals(0, 0, 0, 0, 0); else globals(180, 386, 1300, 1900, 1.25f);
        for (float d : {1.f, 50.f, 400.f, 5000.f}) {
            place(d * G, 1.f);
            SpindleLodVertex_float(pos, nrmOS, uvf, toAxis, radial, splay, 0.f, R, 1.f, 0.f, 0.f, out);
            CHECK(length(out - pos) == 0.f, "girth 0 moved a vertex (zero=%d d=%g)", zero, d);
            SpindleLodSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, uvf, 0.f, R, 1.f, 0.f, 0.f, col, a);
            CHECK(length(col - lace) == 0.f && a == 0.4f, "girth 0 changed the lace (zero=%d)", zero);
        }
    }
    globals(0, 0, 0, 0, 0);
    for (float d : {1.f, 400.f, 5000.f}) {
        place(d * G, 1.f);
        SpindleLodVertex_float(pos, nrmOS, uvf, toAxis, radial, splay, G, R, 1.f, 0.f, 0.f, out);
        CHECK(finite3(out) && length(out - pos) < 1e-6f, "unset globals moved a vertex at %g girths", d);
        SpindleLodSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, uvf, G, R, 1.f, 0.f, 0.f, col, a);
        CHECK(finite3(col) && std::isfinite(a) && length(col - lace) < 1e-6f && std::fabs(a - 0.4f) < 1e-6f,
              "unset globals changed the lace at %g girths", d);
        SpindleLodThread_float(dull, bright, 0.f, nrm, view, uvf, G, R, 1.f, 0.f, 0.f, col, a);
        CHECK(finite3(col) && std::isfinite(a) && a > 0.f, "unset globals: the far thread must still draw");
    }

    const float cs = 180, ce = 386, fs = 1300, fe = 1900, minPx = 1.25f;
    globals(cs, ce, fs, fe, minPx);

    // 2. below convergeStart: exact lace, vertex where the mesh put it (no inflation yet at 120 girths:
    //    the thinnest-on-average strand is still > minPx wide there).
    place(120 * G, 1.f);
    SpindleLodVertex_float(pos, nrmOS, uvf, toAxis, radial, splay, G, R, 1.f, 0.f, 0.f, out);
    CHECK(length(out - pos) < 1e-6f, "below convergeStart the vertex moved");
    SpindleLodSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, uvf, G, R, 1.f, 0.f, 0.f, col, a);
    CHECK(length(col - lace) < 1e-6f && std::fabs(a - 0.4f) < 1e-6f, "below convergeStart the lace changed");

    // 3. at/after convergeEnd: mid == far construction, mid surface == far thread
    for (float m : {ce, ce * 1.05f, 900.f, fs, 1600.f}) {
        place(m * G, 1.f);
        SpindleLodVertex_float(pos, nrmOS, uvf, toAxis, radial, splay, G, R, 1.f, 0.f, 0.f, out);
        float trueWidth = 2.f * R * _ScreenParams.y * 0.5f * UNITY_MATRIX_P._m11 / (m * G);
        float k = clamp(minPx / trueWidth, 1.f, 32.f);
        float3 farCtor = (pos - radial) + toAxis + radial * k;
        CHECK(length(out - farCtor) < 1e-5f, "at %g girths the mid vertex is not the far thread's", m);
        float3 c2; float a2;
        SpindleLodSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, uvf, G, R, 1.f, 0.f, 0.f, col, a);
        SpindleLodThread_float(dull, bright, 0.f, nrm, view, uvf, G, R, 1.f, 0.f, 0.f, c2, a2);
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
    { // URP's flipped projection (rendering into a texture on D3D / Metal / Vulkan: _m11 < 0) draws the
      // same width - it once read as a negative width and held every strand at the 32x cap
        place(700 * G, 1.f);
        float kUp = SpindleLod_Inflate(SpindleLod_View(G), R);
        UNITY_MATRIX_P._m11 = -UNITY_MATRIX_P._m11;
        float kFlip = SpindleLod_Inflate(SpindleLod_View(G), R);
        UNITY_MATRIX_P._m11 = -UNITY_MATRIX_P._m11;
        CHECK(std::fabs(kUp - kFlip) < 1e-5f && kUp < 32.f, "a flipped projection inflates %g, an upright one %g", kFlip, kUp);
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
    SpindleLodThread_float(dull, bright, 0.f, nrm, view, uvf, G, R, 1.f, 0.f, 0.f, col, a);
    CHECK(a == 0.f, "alpha at farFadeEnd is %g", a);
    place(600 * G, 1.f);
    SpindleLodThread_float(dull, bright, 1.f, nrm, view, uvf, G, R, 1.f, 0.f, 0.f, col, a);
    CHECK(a == 0.f, "a fully evaporated thread still has alpha %g", a);
    SpindleLodThread_float(dull, bright, 0.f, nrm, view, uvf, G, R, 1.f, 0.f, 0.f, col, a);
    CHECK(a > 0.f, "a living thread at 600 girths has no alpha");

    // 6. convergence is monotone in distance
    float prev = -1.f;
    for (float m = 1.f; m < 3000.f; m *= 1.02f) {
        float c = SpindleLod_Converge(m);
        CHECK(c >= prev - 1e-7f, "convergence went backwards at %g girths", m);
        prev = c;
    }
    CHECK(SpindleLod_Converge(cs) == 0.f && SpindleLod_Converge(ce) == 1.f, "convergence band edges");


    // ── OVERT style: the incorporeal depth effect, in lockstep with the prisms ──────────────────
    // Danger tier (DangerBlockMaterial): _Spread (0.09, 0.29, 0.8), _SqrDistance 100000.
    const float3 danger = mk3(0.09f, 0.29f, 0.8f);
    const float SQR = 100000.f;
    _IncorporealSpread = float4{danger.x, danger.y, danger.z, SQR};
    _IncorporealFlow = float4{0.35f, 1.5f, 0.6f, 0.8f};
    globals(0, 1, 16, 36, minPx, 1.f);
    const float gains[3] = {50.f, 35.f, 20.f};
    const float sp[3] = {danger.x, danger.y, danger.z};
    // the norm where the FIRST axis starts to open
    float t0 = 1.f;
    for (int k = 0; k < 3; k++) t0 = std::min(t0, (sp[k] + 7.f) / (sp[k] * gains[k] + 7.f));

    // O1. lockstep: the opening IS the prism spread beyond rest, at every distance
    for (float n = 0.f; n < 4.f; n += 0.013f) {
        float3 o = IncorporealDepth_Opening(n, danger);
        for (int k = 0; k < 3; k++) {
            float want = prismEff(sp[k], gains[k], n) - sp[k];
            CHECK(std::fabs(o[k] - want) < 1e-4f, "norm %g axis %d: opening %g, prisms open %g", n, k, o[k], want);
        }
    }
    // O2. closed up close: below t0 the mid tier is the near tier - no opening, no hold, the exact lace
    for (float n : {0.f, 0.1f, t0 * 0.95f}) {
        place(std::sqrt(n * SQR), 1.f);
        SpindleLodVertex_float(pos, nrmOS, uvf, toAxis, radial, splay, G, R, 1.f, 1.3f, 7.7f, out);
        CHECK(length(out - pos) < 1e-5f, "overt, closed at norm %g: the vertex moved %g", n, length(out - pos));
        SpindleLodSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, uvf, G, R, 1.f, 1.3f, 7.7f, col, a);
        CHECK(length(col - lace) < 1e-6f && std::fabs(a - 0.4f) < 1e-6f, "overt, closed at norm %g: the lace changed", n);
    }
    // O3. fully open at the prisms' over-distance: progress 1, so the mid surface IS the far thread,
    //     and the opening is the danger prisms' full spread (4.41, 9.86, 15.2 world units).
    for (float n : {1.f, 1.2f, 9.f}) {
        place(std::sqrt(n * SQR), 1.f);
        SpindleLodView v = SpindleLod_View(G);
        CHECK(std::fabs(v.progress - 1.f) < 1e-5f, "overt at norm %g: progress %g, not fully open", n, v.progress);
        float3 c2; float a2;
        SpindleLodSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, uvf, G, R, 1.f, 1.3f, 7.7f, col, a);
        SpindleLodThread_float(dull, bright, 0.f, nrm, view, uvf, G, R, 1.f, 1.3f, 7.7f, c2, a2);
        CHECK(length(col - c2) < 1e-5f && std::fabs(a - a2) < 1e-5f, "overt at norm %g: mid surface != far thread", n);
        float3 o = IncorporealDepth_Opening(n, danger);
        CHECK(std::fabs(o.x - 4.41f) < 1e-3f && std::fabs(o.y - 9.86f) < 1e-3f && std::fabs(o.z - 15.2f) < 1e-3f,
              "overt full opening (%g, %g, %g) is not the danger prisms'", o.x, o.y, o.z);
    }
    // O4. opening is monotone in distance (the parts never close as you pull away)
    float prevOpen = -1.f;
    for (float n = 0.f; n < 3.f; n += 0.01f) {
        float o = IncorporealDepth_Openness(n, danger);
        CHECK(o >= prevOpen - 1e-6f, "openness fell at norm %g", n);
        prevOpen = o;
    }
    // O5. alive: once open, the clock moves the vertices (the wave travels); while closed, nothing moves
    {
        place(std::sqrt(2.f * SQR), 1.f);
        float3 a1, a2b;
        SpindleLodVertex_float(pos, nrmOS, uvf, toAxis, radial, splay, G, R, 1.f, 1.3f, 0.0f, a1);
        SpindleLodVertex_float(pos, nrmOS, uvf, toAxis, radial, splay, G, R, 1.f, 1.3f, 0.4f, a2b);
        CHECK(length(a1 - a2b) > 0.05f, "open spindle is still: the wave moved it only %g", length(a1 - a2b));
        place(std::sqrt(0.05f * SQR), 1.f);
        SpindleLodVertex_float(pos, nrmOS, uvf, toAxis, radial, splay, G, R, 1.f, 1.3f, 0.0f, a1);
        SpindleLodVertex_float(pos, nrmOS, uvf, toAxis, radial, splay, G, R, 1.f, 1.3f, 0.4f, a2b);
        CHECK(length(a1 - a2b) < 1e-6f, "closed spindle moves with the clock");
        // and the pulse of light rides the crest only while open
        CHECK(IncorporealDepth_Pulse(0.2f, 0.f, 0.f, 1.f) == 0.f, "a closed part pulses");
    }
    // O6. the opening is in WORLD units, as a prism's is: a scaled limb opens the same distance
    {
        place(std::sqrt(2.f * SQR), 1.f);
        float3 o1, o3; float op;
        IncorporealDepthOpen_float(pos, nrmOS, 0.f, 0.f, 0.f, 1.f, o1, op);
        g_model._m00 = g_model._m11 = 3.f;
        IncorporealDepthOpen_float(pos, nrmOS, 0.f, 0.f, 0.f, 1.f, o3, op);
        CHECK(std::fabs((o1.x - pos.x) - 3.f * (o3.x - pos.x)) < 1e-4f, "opening is not divided by object scale");
    }
    // O6b. a strand PARTS by the danger prisms' farthest face travel, in WORLD units, whatever the
    //      limb's scale (a lattice limb stretched 6x along its length parts no further)
    for (float sz : {1.f, 6.2f}) {
        place(std::sqrt(2.f * SQR), 1.f);
        g_model._m22 = sz;
        float3 o; float op;
        const float3 dir = mk3(0.3f, -0.4f, 0.5f);
        IncorporealDepthPart_float(pos, dir, 0.f, 0.f, 0.f, 1.f, o, op);   // wave at sin(0) = 0: flow 1
        float3 dOS = o - pos;
        float3 dW = mul(to3x3(g_model), dOS);
        CHECK(std::fabs(length(dW) - 15.2f) < 1e-3f, "scale z %g: a strand parted %g world units, not the danger face's 15.2", sz, length(dW));
        CHECK(length(normalize(dOS) - normalize(dir)) < 1e-5f, "a strand parted off its direction");
    }
    _IncorporealSpread = float4{danger.x, danger.y, danger.z, SQR};
    // O6c. the fan: at full opening the ROOT (u = 0) never leaves its node - the limb stays a bond -
    //      and the TIP (u = 1) travels exactly the danger face's 15.2 world units along its own splay
    //      (clock 0, phase 0: the wave reads sin(3 pi) = 0 at the tip, so flow is 1).
    {
        place(std::sqrt(2.f * SQR), 1.f);
        float3 root, tip;
        SpindleLodVertex_float(pos, nrmOS, mk3(0.f, 0.5f, 0.f), toAxis, radial, splay, G, R, 1.f, 0.f, 0.f, root);
        SpindleLodVertex_float(pos, nrmOS, mk3(1.f, 0.5f, 0.f), toAxis, radial, splay, G, R, 1.f, 0.f, 0.f, tip);
        // the width hold moves both the same way (radial arm only); compare against that held point
        SpindleLodView v = SpindleLod_View(G);
        float3 held = (pos - radial) + radial * SpindleLod_Hold(v, R);
        CHECK(length(root - held) < 1e-5f, "overt: the root moved %g off its node", length(root - held));
        CHECK(std::fabs(length(tip - held) - 15.2f) < 1e-3f, "overt: the tip fanned %g, not the danger face's 15.2", length(tip - held));
        CHECK(length(normalize(tip - held) - splay) < 1e-5f, "overt: the tip fanned off its splay");
    }
    // R. The far side (rounds 3-5): the effect settles back with distance, and a limb's Strength scales it.
    //    The shipped recede (Resources/IncorporealDepth): 2.25 -> 4.9, ~474 -> ~700 units (round 5).
    const float RE = 4.9f;
    _IncorporealRecede = float4{2.25f, RE, 0, 0};
    {
        // R1. the recede is 1 to its start, 0 from its end, monotone and SMOOTH in distance between (an
        //     eased curve, never a step: no more than 5% of the effect per 1% of distance)
        float prevR = 2.f, worstStep = 0.f;
        for (float d = 200.f; d < 3000.f; d *= 1.01f) {
            float r = IncorporealDepth_Recede(d * d / SQR);
            CHECK(r <= prevR + 1e-6f, "recede rose with distance at %g units", d);
            if (prevR <= 1.f) worstStep = std::max(worstStep, prevR - r);
            prevR = r;
        }
        // ... reported against the NEAR side, the prisms' own ramp (the part Garrett called great). Round 3
        //     asked for a far side gentler than it; round 5 asked for the recede to end by ~700 units,
        //     which makes it a little steeper - so this is a figure to read, and the guard is the 5%.
        float prevO = -1.f, nearWorst = 0.f;
        for (float d = 100.f; d < 400.f; d *= 1.01f) {
            float o = IncorporealDepth_RampOpenness(d * d / SQR, danger);
            if (prevO >= 0.f) nearWorst = std::max(nearWorst, o - prevO);
            prevO = o;
        }
        CHECK(worstStep < 0.05f, "far side steps %g of the effect per 1%% of distance: the recede is a step, not an ease", worstStep);
        std::printf("onset: far side %.4f, near ramp %.4f per 1%% of distance\n", worstStep, nearWorst);
        CHECK(IncorporealDepth_Recede(2.25f) == 1.f && IncorporealDepth_Recede(RE) == 0.f, "recede band edges");
        // R2. at the recede's end the braid IS the thread's construction: no fan, strands on the centreline
        place(std::sqrt(RE * SQR), 1.f);
        SpindleLodView v = SpindleLod_View(G);
        float3 o;
        SpindleLodVertex_float(pos, nrmOS, mk3(0.8f, 0.5f, 0.f), toAxis, radial, splay, G, R, 1.f, 0.3f, 1.7f, o);
        float3 thread = (pos - radial) + toAxis + radial * SpindleLod_Hold(v, R);
        CHECK(length(o - thread) < 1e-5f, "a fully receded braid is %g off the thread's construction", length(o - thread));
        // ... and past the peak the lace stays closed (progress 1) while it recedes
        CHECK(std::fabs(v.progress - 1.f) < 1e-5f, "the lace came back on the far side");
        // R3. Strength: at the peak a limb's tip travels Strength x the danger face's 15.2 units
        for (float S : {0.01f, 0.005f, 0.175f}) {
            place(std::sqrt(2.f * SQR), 1.f);
            SpindleLodView vp = SpindleLod_View(G);
            float3 tip;
            SpindleLodVertex_float(pos, nrmOS, mk3(1.f, 0.5f, 0.f), toAxis, radial, splay, G, R, S, 0.f, 0.f, tip);
            float3 held = (pos - radial) + radial * SpindleLod_Hold(vp, R);
            CHECK(std::fabs(length(tip - held) - 15.2f * S) < 1e-3f, "strength %g: the tip fanned %g, not %g", S, length(tip - held), 15.2f * S);
        }
    }

    // ── BEYOND THE BRAID (SpindleDepthGraph, Docs/SPINDLE_LOD.md section 8) ─────────────────────
    // B. BLOOM (form 0): a creature's own model blooms outward from its pivot by position alone.
    {
        auto pose = [](float sx, float sy, float sz, float dist) {
            g_model = f4x4{sx,0,0,0, 0,sy,0,0, 0,0,sz,0, 0,0,0,1};
            _WorldSpaceCameraPos = float4{0, 0, -dist, 0};
        };
        auto worldOf = [](float3 p) { return mul(to3x3(g_model), p); };
        const float reach = 40.f;
        const float peak = std::sqrt(2.f * SQR);                 // past the over-distance, before the recede
        const float3 p0 = mk3(3.f, -2.f, 5.f);
        float3 o; float3 o2;
        // B1. pass-through: covert style, strength 0, closed up close, and settled back far away
        globals(0, 1, 16, 36, minPx, 0.f);
        pose(1, 1, 1, peak);
        SpindleDepthVertex_float(p0, nrmOS, uvf, reach, 0.f, 0.35f, 0.f, 0.f, o);
        CHECK(length(o - p0) == 0.f, "covert: a body bloomed");
        globals(0, 1, 16, 36, minPx, 1.f);
        SpindleDepthVertex_float(p0, nrmOS, uvf, reach, 0.f, 0.f, 0.f, 0.f, o);
        CHECK(length(o - p0) < 1e-6f, "strength 0: a body bloomed %g", length(o - p0));
        pose(1, 1, 1, std::sqrt(0.2f * SQR));
        SpindleDepthVertex_float(p0, nrmOS, uvf, reach, 0.f, 0.35f, 1.f, 2.f, o);
        CHECK(length(o - p0) < 1e-6f, "closed up close: a body bloomed %g", length(o - p0));
        pose(1, 1, 1, std::sqrt(45.f * SQR));
        SpindleDepthVertex_float(p0, nrmOS, uvf, reach, 0.f, 0.35f, 1.f, 2.f, o);
        CHECK(length(o - p0) < 1e-5f, "settled back: a body is still bloomed %g", length(o - p0));
        // B2. at the peak, with the wave at rest (flow amplitude 0): the pivot stays, a point at or past
        //     Reach travels exactly the danger face's 15.2 x strength in WORLD units straight out from
        //     the pivot, and a point inside travels in proportion to its radius (a fan, root to tip) -
        //     whatever the object's scale.
        _IncorporealFlow = float4{0.f, 1.5f, 0.6f, 0.8f};
        for (float3 sc : {mk3(1, 1, 1), mk3(2.f, 3.f, 0.5f)}) {
            pose(sc.x, sc.y, sc.z, peak);
            SpindleDepthVertex_float(mk3(0, 0, 0), nrmOS, uvf, reach, 0.f, 0.35f, 0.f, 0.f, o);
            CHECK(length(o) < 1e-6f, "the pivot moved");
            for (float rr : {10.f, 20.f, 40.f, 90.f}) {
                float3 dirW = normalize(mk3(0.3f, -0.5f, 0.8f));
                float3 pW = dirW * rr;
                float3 pO = mk3(pW.x / sc.x, pW.y / sc.y, pW.z / sc.z);
                SpindleDepthVertex_float(pO, nrmOS, uvf, reach, 0.f, 0.35f, 0.f, 0.f, o);
                float3 dW = worldOf(o) - pW;
                float want = 15.2f * 0.35f * std::min(rr / reach, 1.f);
                CHECK(std::fabs(length(dW) - want) < 1e-3f, "scale (%g,%g,%g) r %g: bloomed %g world units, not %g", sc.x, sc.y, sc.z, rr, length(dW), want);
                CHECK(length(normalize(dW) - dirW) < 1e-4f, "a body bloomed off its radial");
            }
        }
        _IncorporealFlow = float4{0.35f, 1.5f, 0.6f, 0.8f};
        // B3. by POSITION ALONE: two copies of a seam vertex (different normal, different UV) land
        //     on the same point at every distance and clock - the bloom never tears a mesh.
        for (float n : {0.5f, 1.f, 2.f, 9.f}) {
            pose(1, 1, 1, std::sqrt(n * SQR));
            SpindleDepthVertex_float(p0, nrmOS, uvf, reach, 0.f, 0.35f, 0.7f, 1.9f, o);
            SpindleDepthVertex_float(p0, mk3(0, 1, 0), mk3(0.91f, 0.1f, 0.f), reach, 0.f, 0.35f, 0.7f, 1.9f, o2);
            CHECK(length(o - o2) == 0.f, "norm %g: a seam's two copies bloomed %g apart", n, length(o - o2));
        }
        // B4. it never folds and B5. it inverts: over a full sweep of radius, clocks and a body so
        //     small that the travel cap bites, the displaced radius strictly increases, and the
        //     fragment's inverse returns the rest radius.
        for (float rch : {4.f, 40.f}) {
            for (float clk : {0.f, 0.37f, 1.1f}) {
                pose(1, 1, 1, peak);
                float3 sp3 = _IncorporealSpread.xyz * 0.35f;
                float nrm2 = IncorporealDepth_Norm(IncorporealDepth_Origin());
                float3 opn = IncorporealDepth_Opening(nrm2, sp3) * IncorporealDepth_Recede(nrm2);
                float opns = IncorporealDepth_Openness(nrm2, sp3);
                float amt = IncorporealDepth_BloomAmount(opn, rch, opns);
                float prevRd = -1.f, worstInv = 0.f;
                for (float rr = 0.f; rr < 3.f * rch; rr += rch * 0.002f) {
                    float rd, drd;
                    IncorporealDepth_BloomRadius(rr, amt, rch, opns, 0.4f, clk, rd, drd);
                    CHECK(rd > prevRd, "reach %g clock %g: the bloom folds at r %g", rch, clk, rr);
                    prevRd = rd;
                    float back = IncorporealDepth_BloomRestRadius(rd, amt, rch, opns, 0.4f, clk);
                    worstInv = std::max(worstInv, std::fabs(back - rr));
                }
                CHECK(worstInv < 1e-3f * rch, "reach %g clock %g: the inverse misses by %g", rch, clk, worstInv);
            }
        }
        // B6. the pulse rides the crest the VERTEX stage put there: at a displaced world position the
        //     glow is the pulse of that point's REST radius.
        {
            pose(1, 1, 1, peak);
            const float rch = 12.f, S = 0.35f, ph = 0.4f, clk = 0.9f;
            float3 sp3 = _IncorporealSpread.xyz * S;
            float nrm2 = IncorporealDepth_Norm(IncorporealDepth_Origin());
            float opns = IncorporealDepth_Openness(nrm2, sp3);
            float worst = 0.f;
            for (float rr = 0.5f; rr < 2.f * rch; rr += 0.37f) {
                float3 pO = normalize(mk3(0.2f, 0.9f, -0.4f)) * rr;
                SpindleDepthVertex_float(pO, nrmOS, uvf, rch, 0.f, S, ph, clk, o);
                float3 g; float ga;
                SpindleDepthGlow_float(mk3(1, 1, 1), 1.f, worldOf(o), uvf, rch, 0.f, S, ph, clk, g, ga);
                float crest = saturate(IncorporealDepth_Wave(rr / rch, ph, clk));
                float want = 1.f + 0.8f * opns * crest * crest;
                worst = std::max(worst, std::fabs(g.x - want));
            }
            CHECK(worst < 1e-3f, "the bloom's pulse is off the vertex stage's crest by %g", worst);
        }
        // B7. alive: once bloomed, the clock moves the body
        pose(1, 1, 1, peak);
        SpindleDepthVertex_float(p0, nrmOS, uvf, reach, 0.f, 0.35f, 0.f, 0.f, o);
        SpindleDepthVertex_float(p0, nrmOS, uvf, reach, 0.f, 0.35f, 0.f, 0.4f, o2);
        CHECK(length(o - o2) > 0.01f, "a bloomed body is still: the wave moved it %g", length(o - o2));

        // F. FACETS (form 1, the switch rings): prism-exact, each flat facet along its own normal.
        pose(40.f, 40.f, 40.f, peak);
        const float3 fn = normalize(mk3(0.f, 0.8f, 0.6f));
        float3 want; float wop;
        SpindleDepthVertex_float(p0 / 40.f, fn, uvf, 0.f, 1.f, 0.1f, 0.2f, 0.3f, o);
        IncorporealDepthOpen_float(p0 / 40.f, fn, uvf.x, 0.2f, 0.3f, 0.1f, want, wop);
        CHECK(length(o - want) < 1e-6f && length(o - p0 / 40.f) > 1e-4f, "form 1 is not the prism-exact facet opening");
        // a facet's corners move together (same normal, same flow coordinate): the facet stays flat
        SpindleDepthVertex_float(p0 / 40.f + mk3(0.01f, 0, 0), fn, uvf, 0.f, 1.f, 0.1f, 0.2f, 0.3f, o2);
        CHECK(length((o2 - o) - mk3(0.01f, 0, 0)) < 1e-6f, "a ring facet sheared");
        float3 g; float ga;
        SpindleDepthGlow_float(mk3(1, 1, 1), 0.6f, worldOf(o), uvf, 0.f, 1.f, 0.1f, 0.2f, 0.3f, g, ga);
        CHECK(std::fabs(g.x - (1.f + IncorporealDepth_Pulse(uvf.x, 0.2f, 0.3f, 0.1f))) < 1e-6f, "form 1's pulse is not along u");
        // B8. every spindle leaves at the same distance: a BODY's alpha fades on the thread's band
        //     (_SpindleLodBands z -> w), a RING's never does (a switch you fly toward stays readable).
        for (float n : {10.f, 16.f, 26.f, 36.f, 50.f}) {
            pose(1, 1, 1, std::sqrt(n * SQR));
            SpindleDepthGlow_float(mk3(1, 1, 1), 0.6f, mk3(1, 2, 3), uvf, reach, 0.f, 0.35f, 0.f, 0.f, g, ga);
            float want = 0.6f * (1.f - smoothstep(16.f, 36.f, n));
            CHECK(std::fabs(ga - want) < 1e-6f, "norm %g: a body's alpha %g, not the thread's fade %g", n, ga, want);
            SpindleDepthGlow_float(mk3(1, 1, 1), 0.6f, mk3(1, 2, 3), uvf, 0.f, 1.f, 0.05f, 0.f, 0.f, g, ga);
            CHECK(ga == 0.6f, "norm %g: a ring faded (%g)", n, ga);
        }
        globals(0, 1, 16, 36, minPx, 0.f);
        pose(1, 1, 1, std::sqrt(50.f * SQR));
        SpindleDepthGlow_float(mk3(1, 1, 1), 0.6f, mk3(1, 2, 3), uvf, reach, 0.f, 0.35f, 0.f, 0.f, g, ga);
        CHECK(ga == 0.6f && g.x == 1.f, "covert: a body's look changed");
        globals(0, 1, 16, 36, minPx, 1.f);

        // W. THE SYNTHETIC WIRE (the switch rings, SpindleWireGraph): solid up close, a spindle's lace
        //    while the effect is open, solid again once settled - and it never fades.
        {
            const float WG = 0.076f, WR = 0.016f;
            const float3 wuv = mk3(1.6f, 0.5f, 0.f);
            auto wireAt = [&](float n, float3& c, float& al) {
                place(std::sqrt(n * SQR), 40.f);
                SpindleWireSurface_float(lace, 0.4f, dull, bright, 0.f, nrm, view, wuv, WG, WR, 0.01f, 0.f, 0.f, c, al);
            };
            auto solidAt = [&](float n, float3& c, float& al) {
                place(std::sqrt(n * SQR), 40.f);
                SpindleLod_ThreadLook(dull, bright, 0.f, nrm, view, SpindleLod_View(WG), WR, true, c, al);
            };
            float3 wc, sc; float wa, sa;
            // W1. up close: the solid, faceted thread look - a synthetic construct, no lace at all
            wireAt(0.1f, wc, wa); solidAt(0.1f, sc, sa);
            CHECK(length(wc - sc) < 1e-6f && std::fabs(wa - sa) < 1e-6f && wa > 0.5f, "up close the wire is not solid");
            CHECK(length(wc - lace) > 0.05f, "up close the wire draws the lace");
            // W2. at the peak: the spindle's lace (plus its pulse), exactly
            wireAt(2.f, wc, wa);
            float3 laced = SpindleLod_Pulse(lace, wuv.x, 0.f, 0.f, 0.01f);
            CHECK(length(wc - laced) < 1e-6f && std::fabs(wa - 0.4f) < 1e-6f, "at the peak the wire is not the spindle's lace");
            // W3. far beyond the limbs' fade: solid again, and still drawn (a limb is gone there)
            wireAt(50.f, wc, wa); solidAt(50.f, sc, sa);
            CHECK(length(wc - sc) < 1e-6f && std::fabs(wa - sa) < 1e-6f && wa > 0.f, "far away the wire faded or kept its lace");
            float3 tc; float ta;
            SpindleLodThread_float(dull, bright, 0.f, nrm, view, wuv, WG, WR, 0.01f, 0.f, 0.f, tc, ta);
            CHECK(ta == 0.f, "a limb's thread should be gone where the wire still draws");
            // W5. its facets READ: dark edge-on, the signal colour square on (the shipped shade 0 / 1 and a
            //     body at 0.18 of the signal) - a wire between two bright colours washed flat in the game
            {
                const float4 savedLook2 = _SpindleLodLook2;
                _SpindleLodLook2.z = 0.f; _SpindleLodLook2.w = 1.f;
                const float3 sig = mk3(0.4f, 0.5f, 1.f), body = sig * 0.18f;
                place(std::sqrt(0.1f * SQR), 40.f);
                float3 on, edge; float aon, aedge;
                SpindleLod_ThreadLook(body, sig, 0.f, mk3(0, 0, -1), mk3(0, 0, -1), SpindleLod_View(WG), WR, true, on, aon);
                SpindleLod_ThreadLook(body, sig, 0.f, mk3(1, 0, 0), mk3(0, 0, -1), SpindleLod_View(WG), WR, true, edge, aedge);
                CHECK(length(on - sig) < 1e-5f && length(edge - body) < 1e-5f, "the wire's facets do not span dark to bright");
                CHECK(on.z > 4.f * edge.z, "the wire's facets read %g against %g: too flat", on.z, edge.z);
                _SpindleLodLook2 = savedLook2;
            }
            // W4. covert: never the lace
            globals(0, 1, 16, 36, minPx, 0.f);
            wireAt(2.f, wc, wa); solidAt(2.f, sc, sa);
            CHECK(length(wc - sc) < 1e-6f, "covert: the wire turned to lace");
            globals(0, 1, 16, 36, minPx, 1.f);
        }
    }

    _IncorporealRecede = float4{0, 0, 0, 0};
    // O7. nothing published (all-zero globals) = a no-op, never a NaN
    _IncorporealSpread = float4{0, 0, 0, 0};
    _IncorporealFlow = float4{0, 0, 0, 0};
    place(5000.f, 1.f);
    {
        float3 o; float op;
        IncorporealDepthOpen_float(pos, nrmOS, 0.3f, 0.f, 3.f, 1.f, o, op);
        CHECK(finite3(o) && length(o - pos) < 1e-6f && op == 0.f, "unpublished incorporeal depth moved a vertex");
    }

    if (g_fail) { std::fprintf(stderr, "%d check(s) failed\n", g_fail); return 1; }
    std::printf("SpindleLod.hlsl: every contract check passed\n");
    return 0;
}
'''


def extract(text):
    for pat, rep in SUBS:
        text = re.sub(pat, rep, text)
    return text


def compile_and_run(hlsl_text, incorporeal_text=None):
    clang = shutil.which("clang++") or shutil.which("clang")
    if not clang:
        print("clang not found - cannot compile SpindleLod.hlsl here (install clang; this check is not skippable in review)")
        return 2, ""
    with tempfile.TemporaryDirectory() as td:
        open(os.path.join(td, "spindle_lod_extract.hlsl"), "w").write(extract(hlsl_text))
        open(os.path.join(td, "IncorporealDepth.hlsl"), "w").write(extract(incorporeal_text or open(INCORPOREAL).read()))
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
        "the braid does not converge on the recede": ("v.converge = 1.0 - IncorporealDepth_Recede(v.metric);", "v.converge = 0.0;"),
        "strength ignored by the fan": ("IncorporealDepthPart_float(held, Splay, UV.x, Phase, Clock, Strength, parted, openness);",
                                        "IncorporealDepthPart_float(held, Splay, UV.x, Phase, Clock, 1.0, parted, openness);"),
        "the fan is not root-anchored": ("Out = held + (parted - held) * saturate(UV.x);", "Out = parted;"),
        "convergence ignores its band": ("return SpindleLod_Ramp(_SpindleLodBands.x, _SpindleLodBands.y, metric);",
                                         "return SpindleLod_Ramp(_SpindleLodBands.x, _SpindleLodBands.y * 1.5, metric);"),
        "overt lace closes off the opening schedule": ("Color = SpindleLod_Pulse(lerp(LaceColor, threadColor, v.progress), UV.x, Phase, Clock, Strength);",
                                                       "Color = SpindleLod_Pulse(lerp(LaceColor, threadColor, saturate(v.metric)), UV.x, Phase, Clock, Strength);"),
        "a flipped projection reads as a negative width": ("v.pxPerUnit = _ScreenParams.y * 0.5 * abs(UNITY_MATRIX_P._m11) / dist;",
                                                           "v.pxPerUnit = _ScreenParams.y * 0.5 * UNITY_MATRIX_P._m11 / dist;"),
        "coverage not paid": ("float coverage = 1.0 / SpindleLod_Hold(v, strandRadius);", "float coverage = 1.0;"),
        "girth guard dropped": ("    if (!(Girth > 0.0)) return;\n    SpindleLodView v = SpindleLod_View(Girth);\n    float inflate",
                                "    SpindleLodView v = SpindleLod_View(Girth);\n    float inflate"),
        "width hold not ramped on the transformation": ("return lerp(1.0, SpindleLod_Inflate(v, strandRadius), v.progress);",
                                                        "return SpindleLod_Inflate(v, strandRadius);"),
        "far fade never reaches zero": ("if (!synthetic) farFade = 1.0 - SpindleLod_Ramp(", "if (!synthetic) farFade = 1.02 - SpindleLod_Ramp("),
        "the wire shades like the thread (washes flat)": ("float facet = synthetic ? _SpindleLodLook2.w : _SpindleLodLook.z;", "float facet = _SpindleLodLook.z;"),
        "a body's bloom reads its normal (tears seams)": (
            "        IncorporealDepthBloom_float(PositionOS, Reach, Phase, Clock, Strength, Out, openness);",
            "        IncorporealDepthOpen_float(PositionOS, NormalOS, UV.x, Phase, Clock, Strength, Out, openness);"),
        "the depth graph ignores the style": ("    Out = PositionOS;\n#if !defined(SHADERGRAPH_PREVIEW)\n    if (!SpindleLod_Overt()) return;\n    float openness;",
                                              "    Out = PositionOS;\n#if !defined(SHADERGRAPH_PREVIEW)\n    float openness;"),
        "the ring's facet form is ignored": ("    if (Form > 0.5)\n        IncorporealDepthOpen_float", "    if (Form > 1.5)\n        IncorporealDepthOpen_float"),
        "the ring fades with the limbs": ("    if (!synthetic) farFade = 1.0 - SpindleLod_Ramp(", "    farFade = 1.0 - SpindleLod_Ramp("),
        "the ring never turns to lace": ("float lace = SpindleLod_Overt() ? IncorporealDepth_Openness(v.metric, _IncorporealSpread.xyz) : 0.0;",
                                         "float lace = 0.0;"),
        "the ring is lace up close": ("float lace = SpindleLod_Overt() ? IncorporealDepth_Openness(v.metric, _IncorporealSpread.xyz) : 0.0;",
                                      "float lace = SpindleLod_Overt() ? 1.0 : 0.0;"),
        "bodies never leave": ("    OutAlpha = Alpha * (1.0 - SpindleLod_Ramp(_SpindleLodBands.z, _SpindleLodBands.w, norm));", "    OutAlpha = Alpha;"),
        "rings fade with the bodies": ("        OutColor = Color * (1.0 + IncorporealDepth_Pulse(UV.x, Phase, Clock, Strength));\n        return;",
                                       "        OutColor = Color * (1.0 + IncorporealDepth_Pulse(UV.x, Phase, Clock, Strength));\n        OutAlpha = 0.0;\n        return;"),
    }
    failures = 0
    for name, (old, new) in mutants.items():
        assert old in text, f"self-test mutant '{name}' no longer matches the shader - update it"
        code, _ = compile_and_run(text.replace(old, new, 1))
        if code == 0:
            print(f"  mutant NOT caught: {name}")
            failures += 1
    inc = open(INCORPOREAL).read()
    inc_mutants = {
        "opening keeps the resting spread": ("return max(eff - spread, float3(0.0, 0.0, 0.0));", "return eff;"),
        "ramp not the prisms' (norm doubled)": ("return dot(d, d) / max(_IncorporealSpread.w, 1.0);",
                                                "return 2.0 * dot(d, d) / max(_IncorporealSpread.w, 1.0);"),
        # (A wave left running while closed is NOT a mutant: a closed part has zero opening for it to move.)
        "the wave does not travel": ("_IncorporealFlow.z * clock) + phase;", "0.0 * clock) + phase;"),
        "the bloom's fold cap is gone": ("return min(max(opening.x, max(opening.y, opening.z)), share * reach);",
                                         "return max(opening.x, max(opening.y, opening.z));"),
        "the bloom's cap ignores the wave": ("float share = min(INCORPOREAL_DEPTH_BLOOM_MAX_SHARE, (1.0 - INCORPOREAL_DEPTH_BLOOM_MIN_SLOPE) / max(q, 1e-6));",
                                             "float share = INCORPOREAL_DEPTH_BLOOM_MAX_SHARE;"),
        "the bloom is not anchored at the pivot": ("rd = r + amount * flow * r / reach;", "rd = r + amount * flow;"),
        "the bloom's pulse reads the displaced radius": (
            "float r = IncorporealDepth_BloomRestRadius(length(PositionWS - origin), amount, reach, openness, Phase, Clock);",
            "float r = length(PositionWS - origin);"),
        "the bloom ignores the object's scale": ("float r = length(mul((float3x3)GetObjectToWorldMatrix(), PositionOS));",
                                                 "float r = length(PositionOS);"),
        "the recede is ignored by a part": ("    float3 opening = IncorporealDepth_Opening(norm, spread) * IncorporealDepth_Recede(norm);\n    Openness = IncorporealDepth_Openness(norm, spread);\n    if (!(dirLen",
                                            "    float3 opening = IncorporealDepth_Opening(norm, spread);\n    Openness = IncorporealDepth_Openness(norm, spread);\n    if (!(dirLen"),
        "the recede steps (no ease)": ("return 1.0 - smoothstep(sqrt(a), sqrt(b), sqrt(max(norm, 0.0)));", "return norm > 0.5 * (a + b) ? 0.0 : 1.0;"),
        "a part's push not in world units": ("Out = PositionOS + DirectionOS * (amount * flow / max(worldLen, 1e-6));",
                                             "Out = PositionOS + DirectionOS * (amount * flow);"),
        "opening not divided by object scale": ("Out = PositionOS + NormalOS * (opening / IncorporealDepth_ObjectScale()) * flow;",
                                                "Out = PositionOS + NormalOS * opening * flow;"),
    }
    for name, (old, new) in inc_mutants.items():
        assert old in inc, f"self-test mutant '{name}' no longer matches IncorporealDepth.hlsl - update it"
        code, _ = compile_and_run(text, inc.replace(old, new, 1))
        if code == 0:
            print(f"  mutant NOT caught: {name}")
            failures += 1
    total = len(mutants) + len(inc_mutants)
    print("self-test:", "OK (%d mutants caught)" % total if failures == 0 else f"FAILED ({failures})")
    return 1 if failures else 0


def main():
    if "--self-test" in sys.argv:
        return self_test()
    code, out = compile_and_run(open(HLSL).read())
    print(out.strip())
    return code


if __name__ == "__main__":
    sys.exit(main())
