#!/usr/bin/env python3
"""
Prove the SHIPPED black hole lens (Docs/BLACK_HOLE.md §5.1) draws what the Vessel Studio draws. The studio's
lens is the reference (Docs/Studios/StoatFlightStudio.html, `lensMat` and `setLensUniforms`), so this script
READS THE STUDIO'S OWN CODE out of the page and runs it beside the shipped Unity code. Nothing here
re-implements either side.

A. THE PIXEL (clang++). The studio's fragment shader (GLSL, extracted from the page's `fragmentShader` array)
   and the shipped lens (BlackHoleLens.hlsl + the fragment function of BlackHoleLens.shader) are both translated
   mechanically to C++ and run on the same inputs: random black holes, white holes and smooth wells (no waves, no
   crystal mouths: the Unity lens draws neither), random pixels, random scene depths, one procedural scene.
     1. PARITY: every pixel's colour agrees with the studio's (a pixel the shipped lens leaves untouched is
        compared as the scene itself; the studio adds a ring glow below 1/1024 there).
     2. NO OCCLUSION: the holes' order never changes a pixel. One lens sphere per hole, drawn in order, let the
        hole drawn last paint over its partner — the black hole hid the white hole (2026-10-10).
     3. NO DISC EDGE: walking out from a hole across its reach, the bend falls to zero continuously and is exactly
        zero past it, so the lens never ends at an edge. (The sphere's sky swap drew a large disc in lava lamp.)
     4. FOREGROUND: a pixel whose depth is in front of a hole by more than its margin is not bent by that hole.
     5. NEGATIVE CONTROL: the shipped HLSL with the photon ring moved (1.03 -> 1.10) must FAIL parity.

B. COMPILE. BlackHoleLens.shader's vertex and fragment stages with the shipped .hlsl included.
   B1 (glslang, HLSL mode) against a declarations-only mock of the URP library laid out FILE BY FILE at the
      shader's own include paths (a symbol is visible only if the shader includes the file that declares it).
      The first lens shipped calling DecodeHDREnvironment without its include, a one-blob mock passed, and every
      hole drew magenta. Negative control: without the DeclareDepthTexture include it must FAIL.
   B2 (DXC) against the REAL URP + core ShaderLibrary (the graphics checkout Tools/Build/unity_refcompile
      fetches) for D3D11, Vulkan and Metal. Needs dxc (PATH or $DXC) and the checkout ($URP_GRAPHICS_ROOT, else
      $UNITY_REFCOMPILE_CACHE/graphics, else ${TMPDIR:-/tmp}/unity_refcompile_cache/graphics); SKIPPED loudly
      without them, a failure under --require-real. Same negative control.

C. THE CAMERA'S NUMBERS (dotnet + node). BlackHoleLens.ScreenWell and BlackHoleLens.Angular, extracted from the
   C# source and compiled against a tiny UnityEngine stub, against the studio's `setLensUniforms` JavaScript
   extracted from the page and run in node: a hole's angular radius, Einstein term, reach and margin, for black
   holes, white holes and smooth wells, near and far. SKIPPED loudly without dotnet or node (--require-real
   fails it). Negative control: the C# with the Einstein cap moved (1.2 -> 1.3) must FAIL.

Exit 0 on pass. Usage:  python3 Tools/Shaders/verify_black_hole_lens.py [--keep] [--require-real]
"""

import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/BlackHoleLens.hlsl")
SHADER = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/BlackHoleLens.shader")
LENS_CS = os.path.join(ROOT, "Assets/_Scripts/Controller/Environment/BlackHole/BlackHoleLens.cs")
STUDIO = os.path.join(ROOT, "Docs/Studios/StoatFlightStudio.html")

SHIM = r"""// Minimal HLSL/GLSL -> C++ shim: enough vector maths for the studio's lens shader and the shipped HLSL.
#pragma once
#include <cmath>
#include <algorithm>

struct float2 {
    float x=0,y=0;
    float2(){}
    explicit float2(float a):x(a),y(a){}
    float2(float a,float b):x(a),y(b){}
};
struct float3 {
    float x=0,y=0,z=0;
    float3(){}
    explicit float3(float a):x(a),y(a),z(a){}
    float3(float a,float b,float c):x(a),y(b),z(c){}
};
struct float4 {
    float x=0,y=0,z=0,w=0;
    float4(){}
    float4(float a,float b,float c,float d):x(a),y(b),z(c),w(d){}
    float4(float3 v,float d):x(v.x),y(v.y),z(v.z),w(d){}
    float3 xyz() const { return float3(x,y,z); }
    float2 xy() const { return float2(x,y); }
};
#define OP2(op) \
static inline float2 operator op(float2 a,float2 b){return float2(a.x op b.x,a.y op b.y);} \
static inline float2 operator op(float2 a,float b){return float2(a.x op b,a.y op b);} \
static inline float2 operator op(float a,float2 b){return float2(a op b.x,a op b.y);} \
static inline float3 operator op(float3 a,float3 b){return float3(a.x op b.x,a.y op b.y,a.z op b.z);} \
static inline float3 operator op(float3 a,float b){return float3(a.x op b,a.y op b,a.z op b);} \
static inline float3 operator op(float a,float3 b){return float3(a op b.x,a op b.y,a op b.z);}
OP2(+) OP2(-) OP2(*) OP2(/)
static inline float2 operator-(float2 a){return float2(-a.x,-a.y);}
static inline float3 operator-(float3 a){return float3(-a.x,-a.y,-a.z);}
static inline float2& operator+=(float2&a,float2 b){a=a+b;return a;}
static inline float2& operator-=(float2&a,float2 b){a=a-b;return a;}
static inline float3& operator+=(float3&a,float3 b){a=a+b;return a;}
static inline float dot(float2 a,float2 b){return a.x*b.x+a.y*b.y;}
static inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static inline float length(float2 a){return std::sqrt(dot(a,a));}
static inline float length(float3 a){return std::sqrt(dot(a,a));}
static inline float min(float a,float b){return a<b?a:b;}
static inline float max(float a,float b){return a>b?a:b;}
static inline float abs(float a){return a<0?-a:a;}
static inline float2 abs(float2 a){return float2(abs(a.x),abs(a.y));}
static inline float clamp(float v,float a,float b){return v<a?a:(v>b?b:v);}
static inline float saturate(float v){return clamp(v,0.0f,1.0f);}
static inline float lerp(float a,float b,float t){return a+(b-a)*t;}
static inline float3 lerp(float3 a,float3 b,float t){return a+(b-a)*t;}
static inline float3 mix(float3 a,float3 b,float t){return lerp(a,b,t);}
static inline float smoothstep(float e0,float e1,float x){float t=saturate((x-e0)/(e1-e0));return t*t*(3.0f-2.0f*t);}
static inline float exp(float v){return std::exp(v);}
static inline float sin(float v){return std::sin(v);}
static inline float pow(float a,float b){return std::pow(a,b);}
static inline float sqrt(float v){return std::sqrt(v);}
"""

# ---------------------------------------------------------------------------------------------------------
# A. extraction + translation


def studio_fragment():
    """The studio's lens fragment shader, as the page builds it: the `fragmentShader` array of `lensMat`."""
    page = open(STUDIO, encoding="utf-8").read()
    start = page.index("const lensMat = new THREE.ShaderMaterial(")
    a = page.index("fragmentShader: [", start)
    b = page.index("].join('\\n')", a)
    lines = []
    for line in page[a + len("fragmentShader: ["):b].splitlines():
        m = re.search(r"'((?:[^'\\]|\\.)*)'", line)
        if m:
            lines.append(m.group(1).replace("\\'", "'"))
    src = "\n".join(lines)
    assert "void main(){" in src and "gl_FragColor" in src, "the studio's lens shader moved or was renamed"
    return src


def studio_to_cpp(glsl):
    """GLSL -> C++ spelling only."""
    out = glsl
    # each sampler becomes a texture id: tDepth = 1 (the depth), anything else the scene
    out = re.sub(r"\buniform sampler2D (\w+);", lambda m: f"static int {m.group(1)} = {1 if m.group(1) == 'tDepth' else 0};", out)
    out = re.sub(r"\buniform ", "static ", out)
    out = re.sub(r"\bvarying ", "static ", out)
    out = re.sub(r"\bvec2\b", "float2", out)
    out = re.sub(r"\bvec3\b", "float3", out)
    out = re.sub(r"\bvec4\b", "float4", out)
    out = out.replace(".rgb", ".xyz()")
    out = re.sub(r"(\w+\[i\])\.xy\b", r"\1.xy()", out)
    out = out.replace("void main(){", "static float4 gl_FragColor;\nstatic void studio_main(){")
    return out


def shipped_hlsl_to_cpp(hlsl):
    """HLSL -> C++ spelling only (no semantic edits to the shipped file)."""
    out = hlsl
    out = re.sub(r"\binout float2 (\w+)", r"float2 &\1", out)
    out = re.sub(r"\binout float3 (\w+)", r"float3 &\1", out)
    out = re.sub(r"\binout float (\w+)", r"float &\1", out)
    out = out.replace(".rgb", ".xyz()").replace("tint.a", "tint.w")
    out = re.sub(r"\b(\w+)\.xy\b(?!\()", r"\1.xy()", out)
    for name in ("void BlackHoleLensWell(", "float2 BlackHoleLensSampleUV(", "float3 BlackHoleLensComposite(",
                 "bool BlackHoleLensUntouched(", "BLACK_HOLE_LENS_MAX_WELLS"):
        assert name in out, f"{name} missing from the shipped HLSL"
    return out


def shipped_frag_to_cpp(shader):
    """The fragment function of BlackHoleLens.shader, as C++: the URP calls bound to the test's inputs."""
    prog = re.search(r"HLSLPROGRAM(.*?)ENDHLSL", shader, re.S).group(1)
    frag = re.search(r"half4 BlackHoleLensFrag\(Varyings input\) : SV_Target\s*\{.*?\n            \}", prog, re.S)
    assert frag, "BlackHoleLensFrag not found in BlackHoleLens.shader"
    body = frag.group(0)
    body = body.replace("half4 BlackHoleLensFrag(Varyings input) : SV_Target", "static float4 shipped_frag()")
    body = body.replace("GetNormalizedScreenSpaceUV(input.positionCS)", "g_uv")
    body = body.replace("LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams)", "g_sceneZ")
    body = body.replace("SAMPLE_TEXTURE2D_LOD(_BlackHoleSceneColor, sampler_LinearClamp, suv, 0).rgb", "scene_colour(suv)")
    body = body.replace("discard;", "return float4(-1.0f, -1.0f, -1.0f, -1.0f);")
    body = body.replace("half4(", "float4(")
    body = re.sub(r"\(int\)(\w+)", r"(int)\1", body)
    assert "g_uv" in body and "g_sceneZ" in body and "scene_colour(suv)" in body, "the shipped fragment changed shape"
    return body


HARNESS = r"""
#include "shim.h"
#include <cstdio>
#include <random>
#include <vector>
static int failures = 0;
#define CHECK(cond, ...) do { if (!(cond)) { failures++; if (failures < 12) { printf("FAIL: "); printf(__VA_ARGS__); printf("\n"); } } } while (0)

// One procedural scene both sides sample: smooth, coloured, with structure at every scale the lens moves.
static float3 scene_colour(float2 uv)
{
    return float3(0.5f + 0.5f * std::sin(7.1f * uv.x + 2.3f * uv.y),
                  0.5f + 0.5f * std::sin(5.3f * uv.y - 1.7f * uv.x + 1.0f),
                  0.5f + 0.5f * std::cos(9.7f * uv.x * uv.y + 0.4f));
}

// ---- the studio (its GLSL, translated) ----
namespace studio {
static float scene_depth_raw = 0.5f;
static float4 texture2D(int tex, float2 uv) { if (tex == 1) return float4(scene_depth_raw, 0, 0, 0); return float4(scene_colour(uv), 1.0f); }
#include "studio.h"
}

// ---- Unity (the shipped HLSL and the shader's fragment, translated) ----
namespace unity {
#include "shipped.h"
static float4 _BHWellC[BLACK_HOLE_LENS_MAX_WELLS], _BHWellP[BLACK_HOLE_LENS_MAX_WELLS], _BHWellM[BLACK_HOLE_LENS_MAX_WELLS], _BHWellT[BLACK_HOLE_LENS_MAX_WELLS];
static float _BHWellCount;
static float4 _BHLook, _BHLook2;
static float2 g_uv;
static float g_sceneZ;
#include "frag.h"
}

struct Well { float4 c, p, m; };
struct Look { float kShadow, kCore, ringGlow, ringWidth, lensFade, coreBright, coreMix, aspect; };

static float3 run_studio(const std::vector<Well>& wells, const Look& L, float2 uv, float sceneZ)
{
    using namespace studio;
    camNear = 0.5f; camFar = 9000.0f;
    // the raw depth whose linDepth is sceneZ
    float z = (camFar + camNear - 2.0f * camNear * camFar / sceneZ) / (camFar - camNear);
    scene_depth_raw = (z + 1.0f) * 0.5f;
    aspect = L.aspect; soft = 0.65f; gwCam = 0; shellWash = 0; gwGlow = 1;
    kShadow = L.kShadow; kCore = L.kCore; ringGlow = L.ringGlow; ringWidth = L.ringWidth; lensFade = L.lensFade;
    coreBright = L.coreBright; coreMix = L.coreMix;
    for (int i = 0; i < 4; i++) { wvC[i] = float4(0,0,0,0); wC[i] = float4(0,0,0,0); wP[i] = float4(0,0,0,0); wM[i] = float4(0,0,0,0); }
    for (size_t i = 0; i < wells.size() && i < 4; i++) { wC[i] = wells[i].c; wP[i] = wells[i].p; wM[i] = wells[i].m; }
    vUv = uv;
    studio_main();
    return gl_FragColor.xyz();
}

static bool run_unity(const std::vector<Well>& wells, const Look& L, float2 uv, float sceneZ, float3& out)
{
    using namespace unity;
    for (int i = 0; i < BLACK_HOLE_LENS_MAX_WELLS; i++) { _BHWellC[i] = _BHWellP[i] = _BHWellM[i] = _BHWellT[i] = float4(0,0,0,0); }
    for (size_t i = 0; i < wells.size(); i++) { _BHWellC[i] = wells[i].c; _BHWellP[i] = wells[i].p; _BHWellM[i] = float4(wells[i].m.x, 0, 0, wells[i].m.w); }
    _BHWellCount = (float)wells.size();
    _BHLook = float4(L.kShadow, L.kCore, L.ringGlow, L.ringWidth);
    _BHLook2 = float4(L.lensFade, L.coreBright, L.coreMix, L.aspect);
    g_uv = uv; g_sceneZ = sceneZ;
    float4 r = shipped_frag();
    if (r.x == -1.0f && r.w == -1.0f) { out = scene_colour(uv); return false; }   // discarded: the camera's own pixel
    out = r.xyz();
    return true;
}

static Well random_well(std::mt19937& rng, float type)
{
    std::uniform_real_distribution<float> U(0.0f, 1.0f);
    Well w;
    float depth = 40.0f + 900.0f * U(rng);
    w.c = float4(-0.9f + 1.8f * U(rng), -0.45f + 0.9f * U(rng), depth, 1.0f);
    float rc = 0.004f + 0.05f * U(rng);
    if (type < 2.5f) {
        float thE = rc * (1.5f + 3.0f * U(rng));
        w.p = float4(type, rc, 0.0f, thE * thE * (0.3f + 1.5f * U(rng)));
        w.m = float4(rc * (8.0f + 30.0f * U(rng)), 0.0f, 0.0f, 2.6f * rc * depth);
    } else {
        w.p = float4(type, rc * 2.0f, 0.1f + 0.85f * U(rng), 0.0f);
        w.m = float4(0.0f, 0.0f, 0.0f, rc * depth);
    }
    return w;
}

int main(int argc, char** argv)
{
    std::mt19937 rng(1234);
    std::uniform_real_distribution<float> U(0.0f, 1.0f);
    Look L { 2.6f, 2.6f, 0.55f, 0.06f, 0.55f, 4.0f, 0.8f, 16.0f / 9.0f };

    // 1. parity, and 2. order
    int pixels = 0, untouched = 0, worstCase = -1; float worst = 0.0f, worstOrder = 0.0f;
    for (int c = 0; c < 400; c++) {
        int n = 1 + (int)(U(rng) * 4.0f); if (n > 4) n = 4;
        std::vector<Well> wells;
        for (int i = 0; i < n; i++) wells.push_back(random_well(rng, 1.0f + (float)(int)(U(rng) * 4.0f)));
        if (c % 3 == 0 && n >= 2) { wells[1].c.x = wells[0].c.x + 0.05f * (U(rng) - 0.5f); wells[1].c.y = wells[0].c.y + 0.05f * (U(rng) - 0.5f); }
        std::vector<Well> reversed(wells.rbegin(), wells.rend());
        for (int k = 0; k < 300; k++) {
            float2 uv(U(rng), U(rng));
            if (k % 2 == 0) {   // half the pixels near a hole, where the lens does its work
                const Well& w = wells[k % n];
                float r = w.p.y * 12.0f * U(rng), a = 6.2831853f * U(rng);
                uv = float2(w.c.x / L.aspect + 0.5f + r * std::cos(a) / L.aspect, w.c.y + 0.5f + r * std::sin(a));
                if (uv.x < 0.0f || uv.x > 1.0f || uv.y < 0.0f || uv.y > 1.0f) continue;   // a pixel is on the screen
            }
            float sceneZ = (k % 5 == 0) ? 5.0f + 60.0f * U(rng) : 1500.0f + 6000.0f * U(rng);
            float3 a = run_studio(wells, L, uv, sceneZ);
            float3 b; bool touched = run_unity(wells, L, uv, sceneZ, b);
            float3 b2; run_unity(reversed, L, uv, sceneZ, b2);
            float d = std::max(std::fabs(a.x - b.x), std::max(std::fabs(a.y - b.y), std::fabs(a.z - b.z)));
            float tol = touched ? 2e-4f : 1.0f / 1024.0f + 1e-5f;
            if (d > worst && touched) { worst = d; worstCase = c; }
            CHECK(d <= tol, "case %d pixel (%.4f, %.4f): studio (%.5f %.5f %.5f) vs Unity (%.5f %.5f %.5f)%s", c, uv.x, uv.y, a.x, a.y, a.z, b.x, b.y, b.z, touched ? "" : " [untouched]");
            float o = std::max(std::fabs(b.x - b2.x), std::max(std::fabs(b.y - b2.y), std::fabs(b.z - b2.z)));
            worstOrder = std::max(worstOrder, o);
            pixels++; if (!touched) untouched++;
        }
    }
    CHECK(worstOrder < 1e-4f, "reversing the holes changed a pixel by %.6f", worstOrder);
    printf("1. parity with the studio's lens: %d pixels over 400 random sets of 1-4 holes, worst %.2g (case %d); %d left untouched\n", pixels, worst, worstCase, untouched);
    printf("2. order: reversing the holes changes no pixel (worst %.2g)\n", worstOrder);

    // 3. no disc edge: walk out across the reach
    {
        std::vector<Well> one { random_well(rng, 1.0f) };
        Well& w = one[0];
        w.c.x = 0.0f; w.c.y = 0.0f; w.p.y = 0.02f; w.p.w = 0.0036f; w.m.x = 0.4f;
        float reach = w.m.x, maxStep = 0.0f, beyond = 0.0f;
        float3 prev; bool havePrev = false;
        for (int i = 0; i <= 4000; i++) {
            float th = reach * (0.5f + 0.75f * i / 4000.0f);
            float2 uv(0.5f + th / L.aspect, 0.5f);
            float3 col; run_unity(one, L, uv, 9000.0f, col);
            float3 base = scene_colour(uv);
            float dev = std::max(std::fabs(col.x - base.x), std::max(std::fabs(col.y - base.y), std::fabs(col.z - base.z)));
            if (th > reach * 1.0001f) beyond = std::max(beyond, dev);
            if (havePrev) maxStep = std::max(maxStep, std::max(std::fabs(col.x - prev.x), std::max(std::fabs(col.y - prev.y), std::fabs(col.z - prev.z))));
            prev = col; havePrev = true;
        }
        CHECK(beyond == 0.0f, "past its reach the lens still changes the scene by %.6f", beyond);
        CHECK(maxStep < 0.01f, "a step of %.5f across the reach: an edge", maxStep);
        printf("3. no edge: past the reach the scene is untouched (%.2g), the largest step between neighbouring pixels is %.4f\n", beyond, maxStep);
    }

    // 4. foreground
    {
        std::vector<Well> one { random_well(rng, 1.0f) };
        Well& w = one[0];
        w.c = float4(0.0f, 0.0f, 300.0f, 1.0f); w.p.y = 0.02f; w.m.w = 30.0f;
        float2 uv(0.5f + 0.01f / L.aspect, 0.5f);   // inside the shadow
        float3 behind, front;
        run_unity(one, L, uv, 2000.0f, behind);
        bool bent = run_unity(one, L, uv, 200.0f, front);
        float3 base = scene_colour(uv);
        CHECK(behind.x == 0.0f && behind.y == 0.0f && behind.z == 0.0f, "the shadow is not black behind the hole");
        CHECK(!bent && front.x == base.x, "an object in front of the hole was bent or shadowed");
        printf("4. foreground: a pixel in front of the hole is left alone; behind it, the shadow is black\n");
    }

    if (failures) { printf("\n%d FAILURE(S)\n", failures); return 1; }
    printf("\nall properties hold\n");
    return 0;
}
"""

# ---------------------------------------------------------------------------------------------------------
# B. compile: the mock URP library, one file per real include path

URP = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/"
CORE = "Packages/com.unity.render-pipelines.core/ShaderLibrary/"
URP_MOCK = {
    URP + "Core.hlsl": r"""// mock: URP Core.hlsl (+ core Common.hlsl, Input, UnityInput, ShaderVariablesFunctions, GlobalSamplers)
#define TEXTURE2D(t) Texture2D t
#define SAMPLER(s) SamplerState s
#define SAMPLE_TEXTURE2D_LOD(t, s, c, l) t.SampleLevel(s, c, l)
#define UNITY_NEAR_CLIP_VALUE (1.0)
float4 _ZBufferParams;
float4 _ScaledScreenParams;
float2 GetNormalizedScreenSpaceUV(float4 positionCS) { return positionCS.xy / _ScaledScreenParams.xy; }
float LinearEyeDepth(float depth, float4 zBufferParam) { return 1.0 / (zBufferParam.z * depth + zBufferParam.w); }
float4 GetFullScreenTriangleVertexPosition(uint vertexID, float z = UNITY_NEAR_CLIP_VALUE)
{
    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    return float4(uv * 2.0 - 1.0, z, 1.0);
}
SamplerState sampler_PointClamp;
SamplerState sampler_LinearClamp;
""",
    CORE + "EntityLighting.hlsl": r"""// mock: core EntityLighting.hlsl - NOT reached from URP Core.hlsl
half3 DecodeHDREnvironment(half4 encoded, half4 instructions) { return encoded.rgb * instructions.x; }
""",
    URP + "DeclareDepthTexture.hlsl": r"""// mock: URP DeclareDepthTexture.hlsl
TEXTURE2D(_CameraDepthTexture);
float SampleSceneDepth(float2 uv) { return _CameraDepthTexture.SampleLevel(sampler_PointClamp, uv, 0).r; }
""",
}
DEPTH_INCLUDE = '#include "' + URP + 'DeclareDepthTexture.hlsl"'
REAL_APIS = ("SHADER_API_D3D11", "SHADER_API_VULKAN", "SHADER_API_METAL")
REAL_DEFINES = ["UNITY_VERSION=600030", "SHADER_TARGET=35"]


def build_and_run(work, main_src, flags, label):
    with open(os.path.join(work, label + ".cpp"), "w") as f:
        f.write(main_src)
    binary = os.path.join(work, label.replace(" ", "_"))
    build = subprocess.run(["clang++", "-std=c++17", "-O1", "-Wall", "-Wno-unused-function", "-Wno-unused-variable",
                            "-Wno-unused-but-set-variable"] + flags + ["-o", binary, os.path.join(work, label + ".cpp")],
                           cwd=work, capture_output=True, text=True)
    if build.returncode != 0:
        print(f"COMPILE FAILED ({label}):\n" + build.stderr[:4000], file=sys.stderr)
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
    body = re.sub(r"#pragma[^\n]*\n", "\n", program)
    path = os.path.join(work, f"lens_{stage}.hlsl")
    with open(path, "w") as f:
        f.write(body)
    cmd = ["glslangValidator", "-D", "-V", "--target-env", "vulkan1.1", "-S", stage, "-e", entry,
           "-I" + os.path.join(work, "mock"), "-o", os.devnull, path]
    r = subprocess.run(cmd, capture_output=True, text=True)
    return r.returncode, (r.stdout + r.stderr).strip()


def find_real_toolchain():
    dxc = os.environ.get("DXC") or shutil.which("dxc")
    if not dxc or not os.path.exists(dxc):
        return None, "dxc not found (put it on PATH or set $DXC)"
    roots = [os.environ.get("URP_GRAPHICS_ROOT")]
    if os.environ.get("UNITY_REFCOMPILE_CACHE"):
        roots.append(os.path.join(os.environ["UNITY_REFCOMPILE_CACHE"], "graphics"))
    roots.append(os.path.join(os.environ.get("TMPDIR") or "/tmp", "unity_refcompile_cache", "graphics"))
    roots.append(os.path.join("/tmp", "unity_refcompile_cache", "graphics"))
    for root in roots:
        if root and os.path.exists(os.path.join(root, URP, "Core.hlsl")):
            return (dxc, root), None
    return None, "no URP graphics checkout (run Tools/Build/unity_refcompile/run.sh once, or set $URP_GRAPHICS_ROOT)"


def dxc_compile(work, toolchain, program, profile, entry, stage_define, api):
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


# ---------------------------------------------------------------------------------------------------------
# C. the camera's numbers: the shipped C# against the page's JavaScript

CS_STUB = r"""
using System;
namespace UnityEngine {
  public static class Mathf {
    public const float Deg2Rad = (float)(Math.PI / 180.0);
    public static float Tan(float v) => (float)Math.Tan(v);
    public static float Atan(float v) => (float)Math.Atan(v);
    public static float Asin(float v) => (float)Math.Asin(v);
    public static float Sqrt(float v) => (float)Math.Sqrt(v);
    public static float Min(float a, float b) => a < b ? a : b;
    public static float Max(float a, float b) => a > b ? a : b;
  }
  public struct Vector3 { public float x, y, z; public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
    public static float Distance(Vector3 a, Vector3 b) => (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z)); }
  public struct Vector4 { public float x, y, z, w; public Vector4(float a, float b, float c, float d) { x = a; y = b; z = c; w = d; } }
  public struct Color { public float r, g, b, a; }
  // only the view matrix the harness needs: a camera at the origin looking down +z (Unity's view space is -z forward)
  public struct Matrix4x4 { public Vector3 MultiplyPoint3x4(Vector3 p) => new Vector3(p.x, p.y, -p.z); }
}
"""

CS_MAIN = r"""
using System;
using System.Globalization;
using UnityEngine;
public static class Program {
  public static void Main(string[] args) {
    var lines = Console.In.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    var inv = CultureInfo.InvariantCulture;
    foreach (var line in lines) {
      var f = Array.ConvertAll(line.Split(' '), s => float.Parse(s, inv));
      // fov, x, y, z, rs, kind, strength, reach
      var well = new Lens.Well { Position = new Vector3(f[1], f[2], f[3]), Radius = f[4], Kind = f[5], LensStrength = f[6] };
      bool ok = Lens.ScreenWell(well, new Matrix4x4(), new Vector3(0, 0, 0), f[0], 0.3f, f[7], out var c, out var p, out var m);
      Console.WriteLine(ok ? string.Format(inv, "{0:R} {1:R} {2:R} {3:R} {4:R} {5:R} {6:R} {7:R}", p.x, p.y, p.z, p.w, m.x, m.y, m.z, m.w) : "skip");
    }
  }
}
"""


def csharp_lens_source(mutate=False):
    """BlackHoleLens.ScreenWell, BlackHoleLens.Angular, the Well struct and the kind constants, cut out of the
    shipped C# file and wrapped in a class of their own."""
    src = open(LENS_CS, encoding="utf-8").read()

    def member(start_pattern):
        m = re.search(start_pattern, src)
        assert m, f"{start_pattern} not found in BlackHoleLens.cs"
        i = src.index("{", m.end() - 1) if src[m.end() - 1] != ";" else m.end()
        depth = 0
        j = i
        while True:
            if src[j] == "{":
                depth += 1
            elif src[j] == "}":
                depth -= 1
                if depth == 0:
                    return src[m.start():j + 1]
            j += 1

    kinds = re.search(r"public const float KindBlackHole[^;]*;", src).group(0)
    well = member(r"public struct Well\b")
    screen = member(r"public static bool ScreenWell\(")
    angular = re.search(r"public static float Angular\([^;]*;", src, re.S).group(0)
    if mutate:
        screen = screen.replace("Mathf.Min(1.2f,", "Mathf.Min(1.3f,")
    return "using UnityEngine;\npublic static class Lens {\n" + kinds + "\n" + well + "\n" + screen + "\n" + angular + "\n}\n"


def studio_lens_js():
    """The per-hole part of the page's setLensUniforms: from `const ang` to the smooth well's wM row."""
    page = open(STUDIO, encoding="utf-8").read()
    a = page.index("function setLensUniforms(cam, wells) {")
    s = page.index("const ang = (r) =>", a)
    e = page.index("U.wM.value[i].set(0, ang(w.rs), tex, w.rs);", s)
    body = page[s:e + len("U.wM.value[i].set(0, ang(w.rs), tex, w.rs);")] + "\n      }"
    return body


JS_RUNNER = r"""
const lines = require('fs').readFileSync(0, 'utf8').trim().split('\n');
const out = [];
for (const line of lines) {
  const [fov, x, y, z, rs, kind, strength, reach] = line.split(' ').map(Number);
  const f = 0.5 / Math.tan(fov * Math.PI / 360);
  const D = Math.hypot(x, y, z);
  const row = () => ({ v: [0, 0, 0, 0], set(a, b, c, d) { this.v = [a, b, c, d]; } });
  const U = { wC: { value: [row()] }, wP: { value: [row()] }, wM: { value: [row()] } };
  const P = { bhLensStrength: strength, whLensStrength: strength, bhLensReach: reach, lensWidth: 1, lensA: strength };
  const w = { style: kind < 2.5 ? 'A' : 'B', sign: (kind === 1 || kind === 3) ? 1 : -1, rs, amp: 1, p: null, other: null };
  const sx = 0, sy = 0, vz = z; let slot = 0, mouthTex = 3; const thruJobs = [];
  class V3 { subVectors() { return this; } }
  (function () {
__BODY__
  })();
  out.push(U.wP.value[0].v.concat(U.wM.value[0].v).join(' '));
}
console.log(out.join('\n'));
"""


def run_tier_c(work, require_real):
    dotnet = shutil.which("dotnet") or (os.path.expanduser("~/.dotnet/dotnet") if os.path.exists(os.path.expanduser("~/.dotnet/dotnet")) else None)
    node = shutil.which("node")
    if not dotnet or not node:
        why = "dotnet" if not dotnet else "node"
        print(f"C SKIPPED: {why} not found" + (" - FAIL (--require-real)" if require_real else ""))
        return not require_real

    cases = []
    import random
    rnd = random.Random(7)
    for k in range(600):
        kind = 1 + k % 4
        fov = rnd.choice([50.0, 60.0, 68.0, 90.0])
        z = rnd.choice([20.0, 80.0, 400.0, 2500.0]) * (0.5 + rnd.random())
        x, y = (rnd.random() - 0.5) * z * 0.8, (rnd.random() - 0.5) * z * 0.5
        rs = rnd.choice([0.5, 3.0, 12.0, 40.0]) * (0.5 + rnd.random())
        strength = rnd.choice([0.3, 0.6, 1.0, 2.2]) if kind < 3 else rnd.choice([0.2, 0.6, 0.9, 1.4])
        reach = rnd.choice([14.0, 30.0, 55.0])
        cases.append(f"{fov} {x:.6f} {y:.6f} {z:.6f} {rs:.6f} {kind} {strength} {reach}")
    stdin = "\n".join(cases) + "\n"

    with open(os.path.join(work, "runner.js"), "w") as f:
        f.write(JS_RUNNER.replace("__BODY__", studio_lens_js()))
    js = subprocess.run([node, os.path.join(work, "runner.js")], input=stdin, capture_output=True, text=True)
    if js.returncode != 0:
        print("C: the studio's setLensUniforms did not run:\n" + js.stderr[:2000])
        return False
    studio_rows = js.stdout.strip().splitlines()

    def build_cs(label, mutate):
        proj = os.path.join(work, label)
        os.makedirs(proj, exist_ok=True)
        with open(os.path.join(proj, f"{label}.csproj"), "w") as f:
            f.write('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                    '<TargetFramework>net' + dotnet_major(dotnet) + '.0</TargetFramework><Nullable>disable</Nullable>'
                    '<ImplicitUsings>disable</ImplicitUsings><LangVersion>latest</LangVersion></PropertyGroup></Project>')
        with open(os.path.join(proj, "Stub.cs"), "w") as f:
            f.write(CS_STUB)
        with open(os.path.join(proj, "Lens.cs"), "w") as f:
            f.write(csharp_lens_source(mutate))
        with open(os.path.join(proj, "Program.cs"), "w") as f:
            f.write(CS_MAIN)
        b = subprocess.run([dotnet, "build", "-nologo", "-v", "q", "-o", os.path.join(proj, "out")], cwd=proj,
                           capture_output=True, text=True)
        if b.returncode != 0:
            print(f"C: the shipped C# did not build ({label}):\n" + (b.stdout + b.stderr)[-3000:])
            return None
        r = subprocess.run([dotnet, os.path.join(proj, "out", f"{label}.dll")], input=stdin, capture_output=True, text=True)
        return r.stdout.strip().splitlines()

    def compare(rows):
        worst, bad = 0.0, 0
        for i, (a, b) in enumerate(zip(studio_rows, rows)):
            if b == "skip":
                bad += 1
                continue
            av = [float(v) for v in a.split()]
            bv = [float(v) for v in b.split()]
            # a smooth well's wP.w and wM.y are its crystal MOUTH's weight and radius (mouths the Unity lens does not
            # draw — Unity's crystal pair carries its own mouth meshes): not compared
            cols = (0, 1, 2, 3, 4, 7) if av[0] < 2.5 else (0, 1, 2, 4, 7)
            for j in cols:
                d = abs(av[j] - bv[j]) / max(1e-6, abs(av[j]))
                worst = max(worst, d)
                if d > 2e-4:
                    bad += 1
                    break
        return worst, bad

    rows = build_cs("shipped", False)
    if rows is None or len(rows) != len(studio_rows):
        return False
    worst, bad = compare(rows)
    print(f"C. ScreenWell vs the page's setLensUniforms: {len(rows)} holes, worst relative error {worst:.2g}, {bad} mismatched")
    ok = bad == 0
    rows = build_cs("mutated", True)
    fired = rows is not None and compare(rows)[1] > 0
    print(f"C negative control [Einstein cap 1.2 -> 1.3]: {'FIRED' if fired else 'DID NOT FIRE'}")
    return ok and fired


def dotnet_major(dotnet):
    r = subprocess.run([dotnet, "--version"], capture_output=True, text=True)
    m = re.match(r"(\d+)", r.stdout.strip())
    return m.group(1) if m else "8"


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
        hlsl = open(HLSL).read()
        shader = open(SHADER).read()
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(SHIM)
        with open(os.path.join(work, "studio.h"), "w") as f:
            f.write(studio_to_cpp(studio_fragment()))
        with open(os.path.join(work, "frag.h"), "w") as f:
            f.write(shipped_frag_to_cpp(shader))

        print("A. the shipped lens against the studio's own GLSL (extracted from Docs/Studios/StoatFlightStudio.html)")
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write(shipped_hlsl_to_cpp(hlsl))
        rc, out = build_and_run(work, HARNESS, [], "verify")
        sys.stdout.write(out)
        ok &= rc == 0

        mutated = hlsl.replace("sh * 1.03", "sh * 1.10")
        assert mutated != hlsl, "the photon ring's 1.03 moved: update the negative control"
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write(shipped_hlsl_to_cpp(mutated))
        rc, out = build_and_run(work, HARNESS, [], "control")
        fired = rc is not None and rc != 0 and "studio (" in out
        print(f"\n5. negative control [photon ring 1.03 -> 1.10]: {'FIRED' if fired else 'DID NOT FIRE'}")
        ok &= fired

        print("\nB1. glslang compile of BlackHoleLens.shader against the per-file URP mock")
        write_mock_library(work)
        shutil.copy(HLSL, os.path.join(work, "BlackHoleLens.hlsl"))
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
        nodepth = prog.replace(DEPTH_INCLUDE, "")
        rc, out = glslang_compile(work, nodepth, "frag", frag)
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
            rc, errors = dxc_compile(work, toolchain, nodepth, "ps_6_0", frag, "SHADER_STAGE_FRAGMENT", REAL_APIS[0])
            fired = rc != 0 and any("SampleSceneDepth" in e for e in errors)
            print(f"B2 negative control [DeclareDepthTexture.hlsl include removed]: {'FIRED' if fired else 'DID NOT FIRE'}")
            ok &= fired

        print("\nC. the camera's numbers: the shipped C# against the page's JavaScript")
        ok &= run_tier_c(work, require_real)
    finally:
        if keep:
            print("kept:", work)
        else:
            shutil.rmtree(work, ignore_errors=True)
    print("\nPASS" if ok else "\nFAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
