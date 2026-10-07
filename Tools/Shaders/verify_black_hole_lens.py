#!/usr/bin/env python3
"""
Prove the SHIPPED black hole lens (Docs/BLACK_HOLE.md §5.1) does what its header says, in two tiers —
the same shape as verify_prism_slice.py:

A. EXECUTION (clang++). Assets/_Graphics/Materials/Graphs/BlackHoleLens.hlsl is translated
   mechanically (HLSL -> C++ spelling only) and RUN. Nothing here re-implements the shader.
     1. MISS: a ray that never comes within the lens radius is returned bit-identical, escaped,
        with no disc light.
     2. THE SHADOW: rays are captured below, and escape above, the critical impact parameter
        b_c = (3*sqrt(3)/2) r_s = 2.598 r_s — the black disc on screen is ~2.6x the horizon,
        which is what makes the image a black hole and not a black ball.
     3. EINSTEIN DEFLECTION: far out, a ray at impact parameter b is bent toward the hole by
        2 r_s / b + (15 pi / 16)(r_s / b)^2 (Schwarzschild to second order) — within 3%, at
        b = 20, 40 and 80 r_s.
     4. THE DISC OVER THE TOP: a ray parallel to the disc plane, passing ABOVE the shadow, would
        never cross the plane in flat space — bent, it crosses it behind the hole and picks up the
        FAR side of the disc. That is the Interstellar image.
     5. DOPPLER: the side of the disc turning toward the camera is brighter (by more than 3x at
        r = 6 r_s) and bluer than the side turning away; with Doppler off both sides are equal.
     6. THE ISCO GAP AND THE OUTER EDGE: no disc light inside the inner edge or past the outer
        edge; light where the gas is hottest.
     7. INSIDE THE HORIZON: an eye inside r_s sees nothing escape.
     8. SANITY over random rays: escaped directions are unit length, colours finite, alpha <= 1.
     9. FADE: the bend is exactly the straight ray at the lens edge and exactly the traced ray
        inside the fade start — no seam where the lens ends.
    10. NEGATIVE CONTROL: rebuilt with BLACK_HOLE_LENS_STEP_FRACTION blown up (-D override of the
        file's own #ifndef dial), the physics tests FAIL — the integration step is what holds them.

B. COMPILE. The vertex and fragment stages of BlackHoleLens.shader, with the shipped .hlsl included.
   B1 (glslang, HLSL mode) against a declarations-only mock of the URP library, laid out FILE BY FILE
      at the shader's own #include paths: a symbol is visible only if the shader includes the file
      that really declares it. The first lens shipped calling DecodeHDREnvironment (core's
      EntityLighting.hlsl) with only URP's Core.hlsl included; a single-blob mock declared everything
      and passed, Unity failed the compile, and every hole drew magenta. Negative control: the
      program with the EntityLighting include removed must FAIL here.
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
#include <cstdio>
#include <random>
static int failures = 0;
#define CHECK(cond, ...) do { if (!(cond)) { failures++; printf("FAIL: "); printf(__VA_ARGS__); printf("\n"); } } while (0)
static float lum(float3 c){ return 0.2126f*c.x + 0.7152f*c.y + 0.0722f*c.z; }

struct Trace { float3 dir; float escaped; float4 disk; };
static Trace trace(float3 x0, float3 d, float lensR, float4 disk, float4 disk2, float3 axis = float3(0,0,1), int steps = 192)
{
    Trace t; BlackHoleLensTrace(x0, d, lensR, steps, axis, disk, disk2, t.dir, t.escaped, t.disk); return t;
}
static const float4 NO_DISK(3, 14, 0, 1);
static const float4 DISK(3, 14, 1, 1);
static const float4 DISK2(6500, 1, 0, 1);
"""

HARNESS = COMMON + r"""
int main()
{
    // 1. miss
    {
        float3 d(1,0,0);
        Trace t = trace(float3(-100, 50, 0), d, 30, DISK, DISK2);
        bool same = t.dir.x == d.x && t.dir.y == d.y && t.dir.z == d.z;
        CHECK(same && t.escaped == 1.0f && t.disk.w == 0.0f, "a ray outside the lens was touched");
        printf("1. ray outside the lens: bit-identical, escaped, no disc light: %s\n", same ? "ok" : "BROKEN");
    }

    // 2. the shadow: capture threshold at b_c = 2.598
    {
        const float bc = 2.5980762f;
        float lastCaptured = -1, firstEscaped = 99; int wrong = 0;
        for (float b = 2.30f; b <= 2.90f; b += 0.002f) {
            Trace t = trace(float3(-200, b, 0), float3(1,0,0), 250, NO_DISK, DISK2);
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
            Trace t = trace(float3(-1500, b, 0), float3(1,0,0), 2000, NO_DISK, DISK2);
            float angle = std::acos(std::min(1.0f, t.dir.x));
            float expected = 2.0f / b + 15.0f * 3.14159265f / (16.0f * b * b);
            float err = std::fabs(angle - expected) / expected;
            CHECK(t.escaped > 0.5f, "a ray at b = %.0f was captured", b);
            CHECK(t.dir.y < 0.0f, "the ray at b = %.0f was bent AWAY from the hole", b);
            CHECK(err < 0.03f, "deflection at b = %.0f: %.5f rad, expected %.5f (%.1f%% off)", b, angle, expected, err * 100);
            printf("3. deflection at b = %2.0f r_s: %.5f rad (Schwarzschild %.5f, %.2f%% off)\n", b, angle, expected, err * 100);
        }
    }

    // 4. the far side of the disc appears over the top of the shadow
    {
        float hits = 0;
        for (float zb = 3.2f; zb <= 6.0f; zb += 0.4f) {
            Trace t = trace(float3(-200, 0, zb), float3(1,0,0), 250, DISK, DISK2);
            if (t.disk.w > 0.05f) hits++;
        }
        CHECK(hits >= 5, "only %.0f of 8 rays passing above the shadow picked up the far disc", hits);
        printf("4. rays parallel to the disc, passing ABOVE the shadow (they never cross the plane in flat space): %.0f / 8 see the far side of the disc\n", hits);
    }

    // 5. Doppler: approaching side brighter and bluer
    {
        float3 xi(6, 0, 0), axis(0, 0, 1);   // gas here orbits toward +y
        float4 toward = BlackHoleDiskEmission(xi, float3(0,-1,0), axis, DISK, DISK2);   // light travels +y
        float4 away   = BlackHoleDiskEmission(xi, float3(0, 1,0), axis, DISK, DISK2);
        float lt = lum(toward.xyz()), la = lum(away.xyz());
        float blueT = toward.z / std::max(toward.x, 1e-6f), blueA = away.z / std::max(away.x, 1e-6f);
        CHECK(lt > 3.0f * la, "approaching side only %.2fx brighter", lt / std::max(la, 1e-9f));
        CHECK(blueT > blueA, "approaching side is not bluer (b/r %.3f vs %.3f)", blueT, blueA);
        float4 flat2(6500, 0, 0, 1);
        float4 t0 = BlackHoleDiskEmission(xi, float3(0,-1,0), axis, DISK, flat2);
        float4 a0 = BlackHoleDiskEmission(xi, float3(0, 1,0), axis, DISK, flat2);
        CHECK(std::fabs(lum(t0.xyz()) - lum(a0.xyz())) < 1e-5f, "with Doppler off the two sides differ");
        printf("5. Doppler at r = 6: approaching side %.1fx brighter and bluer (b/r %.2f vs %.2f); equal with Doppler off\n", lt / la, blueT, blueA);
    }

    // 6. the ISCO gap and the outer edge
    {
        float3 axis(0,0,1);
        float4 inGap = BlackHoleDiskEmission(float3(2.95f, 0, 0), float3(0,0,-1), axis, DISK, DISK2);
        float4 past  = BlackHoleDiskEmission(float3(14.5f, 0, 0), float3(0,0,-1), axis, DISK, DISK2);
        float4 hot   = BlackHoleDiskEmission(float3(4.08f, 0, 0), float3(0,0,-1), axis, DISK, DISK2);
        CHECK(inGap.w == 0.0f && past.w == 0.0f, "disc light inside the ISCO or past the outer edge");
        CHECK(hot.w > 0.0f && lum(hot.xyz()) > 0.0f, "no light at the hottest ring");
        printf("6. no disc inside r_in or past r_out; the hottest ring (1.36 r_in) glows\n");
    }

    // 7. inside the horizon
    {
        Trace t = trace(float3(0.5f, 0, 0), float3(1,0,0), 30, NO_DISK, DISK2);
        CHECK(t.escaped < 0.5f, "an eye inside the horizon saw a ray escape");
        printf("7. eye inside the horizon: nothing escapes\n");
    }

    // 8. sanity over random rays
    {
        std::mt19937 rng(20261008);
        auto rnd = [&](float a, float b){ return a + (b - a) * (rng() / (float)rng.max()); };
        int bad = 0, n = 0;
        for (int i = 0; i < 4000; i++) {
            float3 eye(rnd(-80,80), rnd(-80,80), rnd(-80,80));
            if (length(eye) < 2) continue;
            float3 target(rnd(-6,6), rnd(-6,6), rnd(-6,6));
            float3 d = normalize(target - eye);
            float3 axis = normalize(float3(rnd(-1,1), rnd(-1,1), rnd(-1,1)) + float3(0,0,1e-3f));
            float4 dk(3, 14, rnd(0, 2), rnd(0, 10));
            float4 dk2(rnd(2000, 20000), rnd(0,1), rnd(0, 500), rnd(0.2f, 4));
            Trace t = trace(eye, d, 30, dk, dk2, axis, 128);
            n++;
            bool finite = std::isfinite(t.disk.x) && std::isfinite(t.disk.y) && std::isfinite(t.disk.z) && std::isfinite(t.disk.w);
            if (!finite || t.disk.w > 1.0001f || t.disk.w < 0 || (t.escaped > 0.5f && std::fabs(length(t.dir) - 1) > 1e-3f)) bad++;
        }
        CHECK(bad == 0, "%d of %d random rays produced a non-finite colour, alpha out of range or a non-unit direction", bad, n);
        printf("8. %d random rays: finite colour, alpha in [0,1], unit escape direction\n", n);
    }

    // 9. fade
    {
        float3 d(1,0,0), bent = normalize(float3(1, -0.4f, 0));
        float3 atEdge = BlackHoleLensFadeDir(d, bent, 30, 30, 0.55f);
        float3 inside = BlackHoleLensFadeDir(d, bent, 10, 30, 0.55f);
        CHECK(atEdge.x == d.x && atEdge.y == d.y && atEdge.z == d.z, "the bend is not zero at the lens edge");
        CHECK(std::fabs(dot(inside, bent) - 1) < 1e-6f, "the bend is not the traced ray inside the fade start");
        printf("9. fade: straight at the lens edge, exactly traced inside the fade start\n");
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
// URP Input.hlsl: the sky reflection URP keeps of the skybox
TEXTURECUBE(_GlossyEnvironmentCubeMap);
SAMPLER(sampler_GlossyEnvironmentCubeMap);
half4 _GlossyEnvironmentCubeMap_HDR;
SamplerState sampler_PointClamp;
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
ENTITY_LIGHTING_INCLUDE = '#include "' + CORE + 'EntityLighting.hlsl"'

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
    for name in ("BlackHoleDiskEmission", "BlackHoleLensFadeDir", "BlackHoleLensEntry",
                 "BLACK_HOLE_LENS_MAX_STEPS", "BLACK_HOLE_LENS_STEP_FRACTION"):
        assert name in out, f"{name} missing from the shipped HLSL"
    return out


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
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write('#pragma once\n#include "shim.h"\n' + translate(open(HLSL).read()))

        print("A. execution of the shipped BlackHoleLens.hlsl")
        rc, out = build_and_run(work, HARNESS, [], "verify")
        sys.stdout.write(out)
        ok &= rc == 0

        rc, out = build_and_run(work, HARNESS, ["-DBLACK_HOLE_LENS_STEP_FRACTION=2.5"], "control")
        fired = rc is not None and rc != 0
        last = out.strip().splitlines()[-1] if out.strip() else "(no output)"
        print(f"\n10. negative control [integration step x31]: {'FIRED' if fired else 'DID NOT FIRE'} ({last})")
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
        assert ENTITY_LIGHTING_INCLUDE in prog, "the shader no longer includes EntityLighting.hlsl"
        unlit = prog.replace(ENTITY_LIGHTING_INCLUDE, "")
        rc, out = glslang_compile(work, unlit, "frag", frag)
        fired = rc != 0 and "DecodeHDREnvironment" in out
        print(f"B1 negative control [EntityLighting.hlsl include removed]: {'FIRED' if fired else 'DID NOT FIRE'}")
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
            fired = rc != 0 and any("DecodeHDREnvironment" in e for e in errors)
            print(f"B2 negative control [EntityLighting.hlsl include removed]: {'FIRED' if fired else 'DID NOT FIRE'}")
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
