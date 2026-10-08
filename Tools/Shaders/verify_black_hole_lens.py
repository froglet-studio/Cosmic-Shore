#!/usr/bin/env python3
"""
Prove the SHIPPED black hole lens (Docs/BLACK_HOLE.md §5.1) does what its header says, in two tiers —
the same shape as verify_prism_slice.py:

A. EXECUTION (clang++). Assets/_Graphics/Materials/Graphs/BlackHoleLens.hlsl is translated
   mechanically (HLSL -> C++ spelling only) and RUN. Nothing here re-implements the shader.
     1. MISS: a ray that never comes within the lens radius is returned bit-identical and escaped.
     2. THE SHADOW: rays are captured below, and escape above, the critical impact parameter
        b_c = (3*sqrt(3)/2) r_s = 2.598 r_s — the black disc on screen is ~2.6x the horizon,
        which is what makes the image a black hole and not a black ball.
     3. EINSTEIN DEFLECTION: far out, a ray at impact parameter b is bent toward the hole by
        2 r_s / b + (15 pi / 16)(r_s / b)^2 (Schwarzschild to second order) — within 3%, at
        b = 20, 40 and 80 r_s.
     4. INSIDE THE HORIZON: an eye inside r_s sees nothing escape.
     5. SANITY over random rays (eyes inside and outside the lens): escaped directions are unit
        length and finite.
     6. FADE: the bend is exactly the straight ray at the lens edge and exactly the traced ray
        inside the fade start — no seam where the lens ends.
     7. NEGATIVE CONTROL: rebuilt with BLACK_HOLE_LENS_STEP_FRACTION blown up (-D override of the
        file's own #ifndef dial), the physics tests FAIL — the integration step is what holds them.
     8. THE SKY IS THE ONE BlackHoleSky.cs RENDERS: a ray bent off the screen samples the scene's own
        skybox, which BlackHoleSky.cs draws into six faces, face i a 90-degree camera along its
        FaceForward[i] with FaceUp[i] up. Its table is READ FROM THE C# FILE, each face's projection
        is rebuilt the way Unity builds it (rows right, up, -forward; Matrix4x4.Perspective(90, 1)),
        and the shipped BlackHoleSkyFaceUV must land every direction — random, on the axes, on the
        cube's edges and corners — on the same face at the same uv, inside 0..1. Negative control:
        the same check against a table with one face's up vector flipped must FAIL.

   There is no accretion disc to test: a painted disc (thermal, Doppler-shifted, fed by captures)
   was built, read in the editor as a disc slicing through the hole, and was removed on 2026-10-07.

B. COMPILE. The vertex and fragment stages of BlackHoleLens.shader, with the shipped .hlsl included.
   B1 (glslang, HLSL mode) against a declarations-only mock of the URP library, laid out FILE BY FILE
      at the shader's own #include paths: a symbol is visible only if the shader includes the file
      that really declares it. The first lens shipped calling DecodeHDREnvironment (core's
      EntityLighting.hlsl) with only URP's Core.hlsl included; a single-blob mock declared everything
      and passed, Unity failed the compile, and every hole drew magenta. Negative control: the
      program with its DeclareDepthTexture include removed must FAIL here (SampleSceneDepth). (The
      lens no longer decodes an HDR environment cubemap — its sky is BlackHoleSky's linear array —
      nor reads URP's opaque copy: it bends BlackHoleLensPass's after-transparents copy.)
   B2 (DXC) against the REAL URP + core ShaderLibrary - the graphics checkout that
      Tools/Build/unity_refcompile fetches - for the D3D11, Vulkan and Metal API branches. This is
      the compile that would have caught it; it needs dxc (on PATH or $DXC) and the checkout
      ($URP_GRAPHICS_ROOT, else $UNITY_REFCOMPILE_CACHE/graphics, else
      ${TMPDIR:-/tmp}/unity_refcompile_cache/graphics). Without them B2 says SKIPPED, loudly, and
      --require-real turns that into a failure. Same negative control. The checkout is the 6000.0
      graphics branch (URP 17.0); the project runs 17.3, so B2 is strong evidence, not the Editor.

Exit 0 on pass. Needs clang++ and glslangValidator; no Unity.
Usage:  python3 Tools/Shaders/verify_black_hole_lens.py [--keep] [--require-real]
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/BlackHoleLens.hlsl")
SHADER = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/BlackHoleLens.shader")
SKY_CS = os.path.join(ROOT, "Assets/_Scripts/Controller/Environment/BlackHole/BlackHoleSky.cs")

SHIM = r"""// Minimal HLSL->C++ shim so the SHIPPED BlackHoleLens.hlsl compiles and runs under clang++.
#pragma once
#include <cmath>
#include <algorithm>

struct float3 {
    float x=0,y=0,z=0;
    float3(){}
    float3(float a):x(a),y(a),z(a){}
    float3(float a,float b,float c):x(a),y(b),z(c){}
};
struct float2 {
    float x=0,y=0;
    float2(){}
    float2(float a,float b):x(a),y(b){}
};
struct float4 {
    float x=0,y=0,z=0,w=0;
    float4(){}
    float4(float a,float b,float c,float d):x(a),y(b),z(c),w(d){}
    float4(float3 v,float d):x(v.x),y(v.y),z(v.z),w(d){}
    float3 xyz() const { return float3(x,y,z); }
};
static inline float3 operator+(float3 a,float3 b){return float3(a.x+b.x,a.y+b.y,a.z+b.z);}
static inline float3 operator-(float3 a,float3 b){return float3(a.x-b.x,a.y-b.y,a.z-b.z);}
static inline float3 operator*(float3 a,float3 b){return float3(a.x*b.x,a.y*b.y,a.z*b.z);}
static inline float3 operator*(float3 a,float b){return float3(a.x*b,a.y*b,a.z*b);}
static inline float3 operator*(float a,float3 b){return b*a;}
static inline float3 operator/(float3 a,float b){return float3(a.x/b,a.y/b,a.z/b);}
static inline float3 operator-(float a,float3 b){return float3(a-b.x,a-b.y,a-b.z);}
static inline float3 operator+(float3 a,float b){return float3(a.x+b,a.y+b,a.z+b);}
static inline float3 operator-(float3 a){return float3(-a.x,-a.y,-a.z);}
static inline float3& operator+=(float3&a,float3 b){a=a+b;return a;}
static inline float3& operator*=(float3&a,float b){a=a*b;return a;}
static inline float4 operator*(float a,float4 b){return float4(a*b.x,a*b.y,a*b.z,a*b.w);}
static inline float4& operator+=(float4&a,float4 b){a.x+=b.x;a.y+=b.y;a.z+=b.z;a.w+=b.w;return a;}
static inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static inline float3 cross(float3 a,float3 b){return float3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
static inline float length(float3 a){return std::sqrt(dot(a,a));}
static inline float3 normalize(float3 a){return a/length(a);}
static inline float rsqrt(float a){return 1.0f/std::sqrt(a);}
static inline float saturate(float v){return std::min(1.0f,std::max(0.0f,v));}
static inline float min(float a,float b){return a<b?a:b;}
static inline float max(float a,float b){return a>b?a:b;}
static inline float abs(float a){return a<0?-a:a;}
static inline float clamp(float v,float a,float b){return v<a?a:(v>b?b:v);}
static inline float lerp(float a,float b,float t){return a+(b-a)*t;}
static inline float frac(float v){return v-std::floor(v);}
static inline float3 frac(float3 v){return float3(frac(v.x),frac(v.y),frac(v.z));}
static inline float3 floor(float3 v){return float3(std::floor(v.x),std::floor(v.y),std::floor(v.z));}
static inline float smoothstep(float e0,float e1,float x){float t=saturate((x-e0)/(e1-e0));return t*t*(3.0f-2.0f*t);}
using std::pow; using std::log; using std::sqrt; using std::cos; using std::sin;
"""

COMMON = r"""#include "shipped.h"
#include "sky_table.h"
#include <cstdio>
#include <random>
static int failures = 0;
#define CHECK(cond, ...) do { if (!(cond)) { failures++; printf("FAIL: "); printf(__VA_ARGS__); printf("\n"); } } while (0)
static float lum(float3 c){ return 0.2126f*c.x + 0.7152f*c.y + 0.0722f*c.z; }

struct Trace { float3 dir; float escaped; };
static Trace trace(float3 x0, float3 d, float lensR, int steps = 192)
{
    Trace t; BlackHoleLensTrace(x0, d, lensR, steps, t.dir, t.escaped); return t;
}
static Trace traceSource(float3 x0, float3 d, float lensR, int steps = 192)
{
    Trace t; BlackHoleLensTraceSigned(x0, d, lensR, steps, -1.0f, t.dir, t.escaped); return t;
}
"""

HARNESS = COMMON + r"""
int main()
{
    // 1. miss
    {
        float3 d(1,0,0);
        Trace t = trace(float3(-100, 50, 0), d, 30);
        bool same = t.dir.x == d.x && t.dir.y == d.y && t.dir.z == d.z;
        CHECK(same && t.escaped == 1.0f, "a ray outside the lens was touched");
        printf("1. ray outside the lens: bit-identical, escaped: %s\n", same ? "ok" : "BROKEN");
    }

    // 2. the shadow: capture threshold at b_c = 2.598
    {
        const float bc = 2.5980762f;
        float lastCaptured = -1, firstEscaped = 99; int wrong = 0;
        for (float b = 2.30f; b <= 2.90f; b += 0.002f) {
            Trace t = trace(float3(-200, b, 0), float3(1,0,0), 250);
            if (t.escaped < 0.5f) { lastCaptured = std::max(lastCaptured, b); if (b > bc + 0.05f) wrong++; }
            else { firstEscaped = std::min(firstEscaped, b); if (b < bc - 0.05f) wrong++; }
        }
        CHECK(wrong == 0, "%d rays on the wrong side of the shadow edge", wrong);
        CHECK(std::fabs(lastCaptured - bc) < 0.03f, "shadow edge at %.4f, expected %.4f", lastCaptured, bc);
        printf("2. shadow edge: captured up to b = %.4f, escaping from b = %.4f (b_c = %.4f r_s)\n", lastCaptured, firstEscaped, bc);
    }

    // 3. Einstein deflection far out
    {
        const float bs[3] = { 20, 40, 80 };
        for (float b : bs) {
            Trace t = trace(float3(-1500, b, 0), float3(1,0,0), 2000);
            float angle = std::acos(std::min(1.0f, t.dir.x));
            float expected = 2.0f / b + 15.0f * 3.14159265f / (16.0f * b * b);
            float err = std::fabs(angle - expected) / expected;
            CHECK(t.escaped > 0.5f, "a ray at b = %.0f was captured", b);
            CHECK(t.dir.y < 0.0f, "the ray at b = %.0f was bent AWAY from the hole", b);
            CHECK(err < 0.03f, "deflection at b = %.0f: %.5f rad, expected %.5f (%.1f%% off)", b, angle, expected, err * 100);
            printf("3. deflection at b = %2.0f r_s: %.5f rad (Schwarzschild %.5f, %.2f%% off)\n", b, angle, expected, err * 100);
        }
    }

    // 3w. a WHITE HOLE (Docs/BLACK_HOLE.md §12): the same trace with the force negated — every ray
    //     escapes (no shadow, even through the centre and from inside its horizon), and far out it is
    //     bent AWAY from the hole by the sink's first-order angle, 2 r_s / b.
    {
        int captured = 0;
        for (float b = 0.0f; b <= 6.0f; b += 0.05f) {
            Trace t = traceSource(float3(-200, b, 0), float3(1,0,0), 250);
            if (t.escaped < 0.5f || !std::isfinite(t.dir.x)) captured++;
        }
        Trace inside = traceSource(float3(0.5f, 0, 0), float3(1,0,0), 30);
        CHECK(captured == 0, "%d rays were captured by a white hole", captured);
        CHECK(inside.escaped > 0.5f, "an eye inside a white hole's horizon saw nothing escape");
        const float bs[2] = { 40, 80 };
        for (float b : bs) {
            Trace t = traceSource(float3(-1500, b, 0), float3(1,0,0), 2000);
            float angle = std::acos(std::min(1.0f, t.dir.x));
            float expected = 2.0f / b - 15.0f * 3.14159265f / (16.0f * b * b);
            float err = std::fabs(angle - expected) / expected;
            CHECK(t.dir.y > 0.0f, "the white hole bent the ray at b = %.0f TOWARD itself", b);
            CHECK(err < 0.04f, "white-hole deflection at b = %.0f: %.5f rad, expected %.5f (%.1f%% off)", b, angle, expected, err * 100);
            printf("3w. white hole, b = %2.0f r_s: bent AWAY by %.5f rad (2/b - 15pi/16b^2 = %.5f, %.2f%% off)\n", b, angle, expected, err * 100);
        }
        printf("3w. white hole: no ray captured for b in [0, 6] r_s nor from inside its horizon\n");
    }

    // 3m. the SMOOTH lens (a smooth well, Docs/CRYSTAL_WORMHOLE.md): monotone (the image never folds,
    //     so no ring and no caustic) for |A| < 1; an attractor and a repulsor are mirror images; two equal
    //     and opposite wells at one point cancel EXACTLY; and the bend is gone at the lens sphere's edge.
    {
        const float W = 30.0f, D = 400.0f;
        float3 eyeRel(-D, 0, 0);
        int folds = 0; float worstMirror = 0, worstCancel = 0;
        const float As[3] = { 0.3f, 0.6f, 0.85f };
        for (float A : As) {
            float prevSrcA = -1e9f, prevSrcR = -1e9f;
            for (int i = 0; i <= 400; i++) {
                float theta = 0.8f * (float)i / 400.0f * (4.0f * W / D);   // out to the 4w edge
                float3 d = normalize(float3(std::cos(theta), std::sin(theta), 0));
                float3 ba = BlackHoleSmoothLensDir(eyeRel, d, W, A);
                float3 br = BlackHoleSmoothLensDir(eyeRel, d, W, -A);
                float srcA = std::atan2(ba.y, ba.x), srcR = std::atan2(br.y, br.x);
                if (srcA < prevSrcA - 1e-7f || srcR < prevSrcR - 1e-7f) folds++;
                prevSrcA = srcA; prevSrcR = srcR;
                worstMirror = std::max(worstMirror, std::fabs((srcA - theta) + (srcR - theta)));
                float3 sum = BlackHoleSmoothLensDeflection(eyeRel, d, W, A) + BlackHoleSmoothLensDeflection(eyeRel, d, W, -A);
                worstCancel = std::max(worstCancel, length(sum));
            }
        }
        float3 dEdge = normalize(float3(std::cos(4.0f * W / D), std::sin(4.0f * W / D), 0));
        float edge = length(BlackHoleSmoothLensDeflection(eyeRel, dEdge, W, 0.85f));
        CHECK(folds == 0, "the smooth lens folded the image %d times", folds);
        CHECK(worstMirror < 1e-4f, "the attractor and repulsor are not mirror images (worst %.3g rad)", worstMirror);
        CHECK(worstCancel < 1e-6f, "two opposite wells at one point did not cancel (worst %.3g)", worstCancel);
        CHECK(edge < 2e-4f, "the smooth lens still bends %.3g rad at its sphere's edge — a seam", edge);
        printf("3m. smooth lens: monotone for A up to 0.85, mirror %.2g, opposite wells cancel %.2g, edge bend %.2g rad\n",
               worstMirror, worstCancel, edge);
    }

    // 4. inside the horizon
    {
        Trace t = trace(float3(0.5f, 0, 0), float3(1,0,0), 30);
        CHECK(t.escaped < 0.5f, "an eye inside the horizon saw a ray escape");
        printf("4. eye inside the horizon: nothing escapes\n");
    }

    // 5. sanity over random rays, eyes inside and outside the lens
    {
        std::mt19937 rng(20261008);
        auto rnd = [&](float a, float b){ return a + (b - a) * (rng() / (float)rng.max()); };
        int bad = 0, n = 0;
        for (int i = 0; i < 4000; i++) {
            float3 eye(rnd(-80,80), rnd(-80,80), rnd(-80,80));
            if (length(eye) < 2) continue;
            float3 target(rnd(-6,6), rnd(-6,6), rnd(-6,6));
            float3 d = normalize(target - eye);
            Trace t = trace(eye, d, 30, 128);
            n++;
            bool finite = std::isfinite(t.dir.x) && std::isfinite(t.dir.y) && std::isfinite(t.dir.z);
            if (!finite || (t.escaped > 0.5f && std::fabs(length(t.dir) - 1) > 1e-3f)) bad++;
        }
        CHECK(bad == 0, "%d of %d random rays produced a non-finite or non-unit direction", bad, n);
        printf("5. %d random rays: finite, unit escape direction\n", n);
    }

    // 6. fade
    {
        float3 d(1,0,0), bent = normalize(float3(1, -0.4f, 0));
        float3 atEdge = BlackHoleLensFadeDir(d, bent, 30, 30, 0.55f);
        float3 inside = BlackHoleLensFadeDir(d, bent, 10, 30, 0.55f);
        CHECK(atEdge.x == d.x && atEdge.y == d.y && atEdge.z == d.z, "the bend is not zero at the lens edge");
        CHECK(std::fabs(dot(inside, bent) - 1) < 1e-6f, "the bend is not the traced ray inside the fade start");
        printf("6. fade: straight at the lens edge, exactly traced inside the fade start\n");
    }

    // 8. the sky faces: the shipped lookup against BlackHoleSky.cs's own table (SKY_TABLE, injected)
    {
        int wrongFace = 0, wrongUV = 0, outside = 0, n = 0;
        float worst = 0;
        std::mt19937 rng(8);
        std::uniform_real_distribution<float> U(-1.0f, 1.0f);
        auto check = [&](float3 d) {
            d = normalize(d);
            // The render side, as Unity builds it from the C# table: view rows (right, up, -forward),
            // Matrix4x4.Perspective(90, 1, n, f): clip.x = view.x, clip.y = view.y, clip.w = -view.z.
            int expectFace = 0; float best = -2;
            for (int i = 0; i < 6; i++) { float f = dot(d, SKY_FWD[i]); if (f > best + 1e-6f) { best = f; expectFace = i; } }
            float3 F = SKY_FWD[expectFace], Up = SKY_UP[expectFace], R = cross(Up, F);
            float vx = dot(R, d), vy = dot(Up, d), vz = -dot(F, d);
            float eu = 0.5f + 0.5f * vx / -vz, ev = 0.5f + 0.5f * vy / -vz;
            float face; float2 uv = BlackHoleSkyFaceUV(d, face);
            n++;
            // A direction exactly on an edge belongs to either face; both are correct there.
            bool tie = false;
            for (int i = 0; i < 6; i++) if (i != expectFace && std::fabs(dot(d, SKY_FWD[i]) - best) < 1e-5f) tie |= (int)face == i;
            if ((int)face != expectFace && !tie) { wrongFace++; return; }
            if (tie) { F = SKY_FWD[(int)face]; Up = SKY_UP[(int)face]; R = cross(Up, F);
                       vx = dot(R, d); vy = dot(Up, d); vz = -dot(F, d); eu = 0.5f + 0.5f * vx / -vz; ev = 0.5f + 0.5f * vy / -vz; }
            float err = std::max(std::fabs(uv.x - eu), std::fabs(uv.y - ev));
            worst = std::max(worst, err);
            if (err > 1e-5f) wrongUV++;
            if (uv.x < -1e-6f || uv.x > 1 + 1e-6f || uv.y < -1e-6f || uv.y > 1 + 1e-6f) outside++;
        };
        for (int i = 0; i < 20000; i++) check(float3(U(rng), U(rng), U(rng)));
        for (int i = 0; i < 6; i++) check(SKY_FWD[i]);
        for (int a = -1; a <= 1; a += 2) for (int b = -1; b <= 1; b += 2) {
            check(float3(a, b, 0)); check(float3(a, 0, b)); check(float3(0, a, b));
            for (int c = -1; c <= 1; c += 2) check(float3(a, b, c));
        }
        CHECK(wrongFace == 0, "%d of %d directions sampled from the wrong sky face", wrongFace, n);
        CHECK(wrongUV == 0, "%d of %d directions sampled the wrong place on their face (worst %.3g)", wrongUV, n, worst);
        CHECK(outside == 0, "%d directions fell outside their face", outside);
        printf("8. sky faces: %d directions land on the face and uv BlackHoleSky.cs renders (worst %.2g)\n", n, worst);
    }

    if (failures) { printf("\n%d FAILURE(S)\n", failures); return 1; }
    printf("\nall properties hold\n");
    return 0;
}
"""

# The mock URP library, ONE FILE PER REAL INCLUDE PATH: each entry declares only what that real
# file (or what it includes) declares, so the shader sees a symbol only when it includes its home.
URP = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/"
CORE = "Packages/com.unity.render-pipelines.core/ShaderLibrary/"
URP_MOCK = {
    URP + "Core.hlsl": r"""// mock: URP Core.hlsl (+ Common, Input, UnityInput, SpaceTransforms, ShaderVariablesFunctions)
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
#define UNITY_VERTEX_INPUT_INSTANCE_ID uint instanceID : SV_InstanceID;
#define UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_SETUP_INSTANCE_ID(v)
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)
#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)
#define TEXTURE2D(t) Texture2D t
#define TEXTURECUBE(t) TextureCube t
#define SAMPLER(s) SamplerState s
#define SAMPLE_TEXTURECUBE_LOD(t, s, c, l) t.SampleLevel(s, c, l)
#define TEXTURE2D_ARRAY(t) Texture2DArray t
#define SAMPLE_TEXTURE2D_LOD(t, s, c, l) t.SampleLevel(s, c, l)
#define SAMPLE_TEXTURE2D_ARRAY_LOD(t, s, c, i, l) t.SampleLevel(s, float3(c, i), l)
float4x4 unity_ObjectToWorld;
float4x4 unity_MatrixVP;
float4x4 UNITY_MATRIX_V;
float4x4 UNITY_MATRIX_VP;
float3 _WorldSpaceCameraPos;
float4 _ZBufferParams;
float4 _ProjectionParams;
float4x4 GetObjectToWorldMatrix() { return unity_ObjectToWorld; }
float3 TransformObjectToWorld(float3 p) { return mul(unity_ObjectToWorld, float4(p, 1.0)).xyz; }
float4 TransformWorldToHClip(float3 p) { return mul(unity_MatrixVP, float4(p, 1.0)); }
float4 ComputeScreenPos(float4 positionCS) { float4 o = positionCS * 0.5; o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w; o.zw = positionCS.zw; return o; }
float LinearEyeDepth(float depth, float4 zBufferParam) { return 1.0 / (zBufferParam.z * depth + zBufferParam.w); }
float4 _ScaledScreenParams;
float2 GetNormalizedScreenSpaceUV(float4 positionCS) { return positionCS.xy / _ScaledScreenParams.xy; }
// URP Input.hlsl: the sky reflection URP keeps of the skybox
TEXTURECUBE(_GlossyEnvironmentCubeMap);
SAMPLER(sampler_GlossyEnvironmentCubeMap);
half4 _GlossyEnvironmentCubeMap_HDR;
SamplerState sampler_PointClamp;
SamplerState sampler_LinearClamp;   // core GlobalSamplers.hlsl, which URP's Core.hlsl includes
""",
    CORE + "EntityLighting.hlsl": r"""// mock: core EntityLighting.hlsl - NOT reached from URP Core.hlsl
half3 DecodeHDREnvironment(half4 encoded, half4 instructions) { return encoded.rgb * instructions.x; }
""",
    URP + "DeclareOpaqueTexture.hlsl": r"""// mock: URP DeclareOpaqueTexture.hlsl
TEXTURE2D(_CameraOpaqueTexture);
SAMPLER(sampler_CameraOpaqueTexture);
float3 SampleSceneColor(float2 uv) { return _CameraOpaqueTexture.SampleLevel(sampler_CameraOpaqueTexture, uv, 0).rgb; }
""",
    URP + "DeclareDepthTexture.hlsl": r"""// mock: URP DeclareDepthTexture.hlsl
TEXTURE2D(_CameraDepthTexture);
float SampleSceneDepth(float2 uv) { return _CameraDepthTexture.SampleLevel(sampler_PointClamp, uv, 0).r; }
""",
}
DEPTH_INCLUDE = '#include "' + URP + 'DeclareDepthTexture.hlsl"'

# B2: the real library's API branches (Common.hlsl picks API/<x>.hlsl from these), and the defines
# Unity's compiler sets that the library reads. INSTANCING_ON is left out: the stock URP library
# itself does not compile under it outside Unity (the instancing array macros come from Unity).
REAL_APIS = ("SHADER_API_D3D11", "SHADER_API_VULKAN", "SHADER_API_METAL")
REAL_DEFINES = ["UNITY_VERSION=600030", "SHADER_TARGET=35"]


def translate(src):
    """HLSL -> C++ spelling. Mechanical only: no semantic edits to the shipped file."""
    out = src
    out = re.sub(r"\bout float3 (\w+)", r"float3 &\1", out)
    out = re.sub(r"\bout float4 (\w+)", r"float4 &\1", out)
    out = re.sub(r"\bout float (\w+)", r"float &\1", out)
    out = re.sub(r"\.a\b", ".w", out)
    assert "void BlackHoleLensTrace(" in out, "entry point missing"
    for name in ("BlackHoleLensFadeDir", "BlackHoleLensEntry", "BlackHoleSkyFaceUV",
                 "BLACK_HOLE_LENS_MAX_STEPS", "BLACK_HOLE_LENS_STEP_FRACTION"):
        assert name in out, f"{name} missing from the shipped HLSL"
    return out


def sky_table_header():
    """BlackHoleSky.cs's face table, read from the C# source itself, as C++ constants. Under
    -DSKY_TABLE_MUTATE face 2's up vector is flipped: the negative control for test 8."""
    src = open(SKY_CS).read()

    def vectors(name):
        block = re.search(name + r"\s*=\s*\{(.*?)\};", src, re.S)
        assert block, f"BlackHoleSky.cs no longer declares {name}"
        triples = re.findall(r"new\(\s*([-\d.]+)f\s*,\s*([-\d.]+)f\s*,\s*([-\d.]+)f\s*\)", block.group(1))
        assert len(triples) == 6, f"BlackHoleSky.cs's {name} has {len(triples)} entries, not 6"
        return [tuple(float(c) for c in t) for t in triples]

    fwd, up = vectors("FaceForward"), vectors("FaceUp")
    row = lambda v: "float3(%g, %g, %g)" % v
    mutated = list(up)
    mutated[2] = tuple(-c for c in mutated[2])
    return ("#pragma once\n// generated from BlackHoleSky.cs by verify_black_hole_lens.py\n"
            "static const float3 SKY_FWD[6] = { " + ", ".join(map(row, fwd)) + " };\n"
            "#ifndef SKY_TABLE_MUTATE\n"
            "static const float3 SKY_UP[6] = { " + ", ".join(map(row, up)) + " };\n"
            "#else\n"
            "static const float3 SKY_UP[6] = { " + ", ".join(map(row, mutated)) + " };\n"
            "#endif\n")


def build_and_run(work, main_src, flags, label):
    with open(os.path.join(work, label + ".cpp"), "w") as f:
        f.write(main_src)
    binary = os.path.join(work, label.replace(" ", "_"))
    build = subprocess.run(["clang++", "-std=c++17", "-O1", "-Wall", "-Wno-unused-function"] + flags +
                           ["-o", binary, os.path.join(work, label + ".cpp")], cwd=work, capture_output=True, text=True)
    if build.returncode != 0:
        print(f"COMPILE FAILED ({label}) - the shipped HLSL does not build:\n" + build.stderr, file=sys.stderr)
        return None, ""
    run = subprocess.run([binary], capture_output=True, text=True)
    return run.returncode, run.stdout


def write_mock_library(work):
    for rel, text in URP_MOCK.items():
        path = os.path.join(work, "mock", rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w") as f:
            f.write(text)


def glslang_compile(work, program, stage, entry):
    """B1: the shader's own #include lines resolve into the per-file mock library."""
    body = re.sub(r"#pragma[^\n]*\n", "\n", program)
    path = os.path.join(work, f"lens_{stage}.hlsl")
    with open(path, "w") as f:
        f.write(body)
    cmd = ["glslangValidator", "-D", "-V", "--target-env", "vulkan1.1", "-S", stage, "-e", entry,
           "-I" + os.path.join(work, "mock"), "-o", os.devnull, path]
    r = subprocess.run(cmd, capture_output=True, text=True)
    return r.returncode, (r.stdout + r.stderr).strip()


def find_real_toolchain():
    """B2's compiler and library: (dxc, graphics root) or (None, why)."""
    dxc = os.environ.get("DXC") or shutil.which("dxc")
    if not dxc or not os.path.exists(dxc):
        return None, "dxc not found (put it on PATH or set $DXC)"
    roots = [os.environ.get("URP_GRAPHICS_ROOT")]
    if os.environ.get("UNITY_REFCOMPILE_CACHE"):
        roots.append(os.path.join(os.environ["UNITY_REFCOMPILE_CACHE"], "graphics"))
    roots.append(os.path.join(os.environ.get("TMPDIR") or "/tmp", "unity_refcompile_cache", "graphics"))
    for root in roots:
        if root and os.path.exists(os.path.join(root, URP, "Core.hlsl")):
            return (dxc, root), None
    return None, "no URP graphics checkout (run Tools/Build/unity_refcompile/run.sh once, or set $URP_GRAPHICS_ROOT)"


def dxc_compile(work, toolchain, program, profile, entry, stage_define, api):
    """B2: the shader against the REAL URP + core ShaderLibrary."""
    dxc, root = toolchain
    body = re.sub(r"#pragma[^\n]*\n", "\n", program)
    path = os.path.join(work, f"lens_real_{profile}.hlsl")
    with open(path, "w") as f:
        f.write(body)
    cmd = [dxc, "-T", profile, "-E", entry, "-HV", "2018", "-I", root, "-D", api, "-D", stage_define]
    for d in REAL_DEFINES:
        cmd += ["-D", d]
    cmd += ["-Fo", os.devnull, path]
    env = dict(os.environ)
    libdir = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(dxc))), "lib")
    if os.path.isdir(libdir):
        env["LD_LIBRARY_PATH"] = libdir + os.pathsep + env.get("LD_LIBRARY_PATH", "")
    r = subprocess.run(cmd, capture_output=True, text=True, env=env)
    errors = [line for line in (r.stdout + r.stderr).splitlines() if "error:" in line]
    return r.returncode, errors


def main():
    keep = "--keep" in sys.argv
    require_real = "--require-real" in sys.argv
    for tool in ("clang++", "glslangValidator"):
        if shutil.which(tool) is None:
            print(f"{tool} not found", file=sys.stderr)
            return 2
    work = tempfile.mkdtemp(prefix="verify_black_hole_lens_")
    ok = True
    try:
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(SHIM)
        with open(os.path.join(work, "sky_table.h"), "w") as f:
            f.write(sky_table_header())
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write('#pragma once\n#include "shim.h"\n' + translate(open(HLSL).read()))

        print("A. execution of the shipped BlackHoleLens.hlsl")
        rc, out = build_and_run(work, HARNESS, [], "verify")
        sys.stdout.write(out)
        ok &= rc == 0

        rc, out = build_and_run(work, HARNESS, ["-DBLACK_HOLE_LENS_STEP_FRACTION=2.5"], "control")
        fired = rc is not None and rc != 0
        last = out.strip().splitlines()[-1] if out.strip() else "(no output)"
        print(f"\n7. negative control [integration step x31]: {'FIRED' if fired else 'DID NOT FIRE'} ({last})")
        ok &= fired

        rc, out = build_and_run(work, HARNESS, ["-DSKY_TABLE_MUTATE"], "sky_control")
        fired = rc is not None and rc != 0 and "sky face" in out
        print(f"8. negative control [BlackHoleSky.cs face 2 up flipped]: {'FIRED' if fired else 'DID NOT FIRE'}")
        ok &= fired

        print("\nB1. glslang compile of BlackHoleLens.shader against the per-file URP mock")
        write_mock_library(work)
        shutil.copy(HLSL, os.path.join(work, "BlackHoleLens.hlsl"))
        shader = open(SHADER).read()
        programs = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", shader, re.S)
        assert len(programs) == 1, f"expected 1 pass, found {len(programs)}"
        prog = programs[0]
        vert = re.search(r"#pragma vertex (\w+)", prog).group(1)
        frag = re.search(r"#pragma fragment (\w+)", prog).group(1)
        for stage, entry in (("vert", vert), ("frag", frag)):
            rc, out = glslang_compile(work, prog, stage, entry)
            if rc != 0:
                print(f"COMPILE FAIL {entry} [{stage}]\n{out}")
                ok = False
            else:
                print(f"compiled {entry} [{stage}]")
        assert DEPTH_INCLUDE in prog, "the shader no longer includes DeclareDepthTexture.hlsl"
        unlit = prog.replace(DEPTH_INCLUDE, "")
        rc, out = glslang_compile(work, unlit, "frag", frag)
        fired = rc != 0 and "SampleSceneDepth" in out
        print(f"B1 negative control [DeclareDepthTexture.hlsl include removed]: {'FIRED' if fired else 'DID NOT FIRE'}")
        ok &= fired

        print("\nB2. DXC compile of BlackHoleLens.shader against the REAL URP + core ShaderLibrary")
        toolchain, why = find_real_toolchain()
        if toolchain is None:
            print(f"B2 SKIPPED: {why}" + (" - FAIL (--require-real)" if require_real else ""))
            ok &= not require_real
        else:
            print(f"library: {toolchain[1]}")
            for api in REAL_APIS:
                for profile, entry, stage_define in (("vs_6_0", vert, "SHADER_STAGE_VERTEX"),
                                                     ("ps_6_0", frag, "SHADER_STAGE_FRAGMENT")):
                    rc, errors = dxc_compile(work, toolchain, prog, profile, entry, stage_define, api)
                    if rc != 0:
                        print(f"COMPILE FAIL {entry} [{api}, {profile}]\n  " + "\n  ".join(errors[:8]))
                        ok = False
                    else:
                        print(f"compiled {entry} [{api}, {profile}]")
            rc, errors = dxc_compile(work, toolchain, unlit, "ps_6_0", frag, "SHADER_STAGE_FRAGMENT", REAL_APIS[0])
            fired = rc != 0 and any("SampleSceneDepth" in e for e in errors)
            print(f"B2 negative control [DeclareDepthTexture.hlsl include removed]: {'FIRED' if fired else 'DID NOT FIRE'}")
            ok &= fired
    finally:
        if keep:
            print("kept:", work)
        else:
            shutil.rmtree(work, ignore_errors=True)
    print("\nPASS" if ok else "\nFAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
