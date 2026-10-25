#!/usr/bin/env python3
"""
Prove the vortex ring lens (Docs/BLACK_HOLE.md §5.3) draws a smooth doughnut of bent light where the ring is, on any
perspective camera. The SHIPPED code runs here, not a re-implementation: VortexRingLens.hlsl is translated
mechanically to C++ and compiled with clang++, and VortexRingLens.ScreenRing is cut out of the C# and compiled with
dotnet against a tiny UnityEngine stub. The references are brute force in double precision.

A. THE PIXEL (clang++, VortexRingLens.hlsl).
   1. NEAREST POINT: the nearest point on the core ellipse agrees with a brute-force search, for round, eccentric,
      thin and edge-on (b = 0) ellipses, inside and outside, near and far.
   2. PROFILE: the bend and the rim are zero on the core (u = 0) and exactly zero from the reach (u >= 3), and the
      bend falls to the reach continuously (no edge to the lens).
   3. NO CREASE: walking straight lines across random rings in fine steps, the displacement never jumps — including
      a nearly edge-on ring, whose nearest point flips across the major axis inside the doughnut's hole.
   4. FOREGROUND: an opaque pixel in front of the ring's core by more than its margin is not bent.
   5. NEGATIVE CONTROL: the HLSL with the medial-axis fade removed must FAIL check 3.

B. THE CAMERA'S NUMBERS (dotnet, VortexRingLens.ScreenRing). Random rings on random cameras — Unity's symmetric
   perspective and the Pistol's OFF-AXIS Matrix4x4.Frustum, a ring seen edge-on among them. Every point of the ring,
   projected through the full matrices by brute force, lies on the ellipse ScreenRing returns, and its inverse depth
   is the row's x + y·cos τ + z·sin τ. NEGATIVE CONTROL: ScreenRing with the aberration map's sign flipped must FAIL.
   SKIPPED loudly without dotnet (--require-real fails it).

C. COMPILE (glslang, HLSL mode). VortexRingLens.shader's vertex and fragment stages with the shipped .hlsl included,
   against the black hole lens's per-file URP mock (verify_black_hole_lens.py: a symbol is visible only if the shader
   includes the file that declares it). NEGATIVE CONTROL: without the DeclareDepthTexture include it must FAIL.
   SKIPPED loudly without glslangValidator (--require-real fails it). This is not Unity's compiler: the real
   ShaderLibrary is compiled only when Unity imports the shader.

Exit 0 on pass. Usage:  python3 Tools/Shaders/verify_vortex_ring_lens.py [--keep] [--require-real]
"""

import math
import os
import random
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/VortexRingLens.hlsl")
SHADER = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/VortexRingLens.shader")
LENS_CS = os.path.join(ROOT, "Assets/_Scripts/Controller/Vessel/Crew/GobyPistol/VortexRingLens.cs")

SHIM = r"""// Minimal HLSL -> C++ shim: enough vector maths for VortexRingLens.hlsl.
#pragma once
#include <cmath>
struct float2 { float x=0,y=0; float2(){} float2(float a,float b):x(a),y(b){} float2 xy() const { return *this; } };
struct float3 { float x=0,y=0,z=0; float3(){} float3(float a,float b,float c):x(a),y(b),z(c){} };
struct float4 { float x=0,y=0,z=0,w=0; float4(){} float4(float a,float b,float c,float d):x(a),y(b),z(c),w(d){}
  float2 xy() const { return float2(x,y); } float3 xyz() const { return float3(x,y,z); } };
#define OP2(op) \
static inline float2 operator op(float2 a,float2 b){return float2(a.x op b.x,a.y op b.y);} \
static inline float2 operator op(float2 a,float b){return float2(a.x op b,a.y op b);} \
static inline float2 operator op(float a,float2 b){return float2(a op b.x,a op b.y);} \
static inline float3 operator op(float3 a,float3 b){return float3(a.x op b.x,a.y op b.y,a.z op b.z);} \
static inline float3 operator op(float3 a,float b){return float3(a.x op b,a.y op b,a.z op b);} \
static inline float3 operator op(float a,float3 b){return float3(a op b.x,a op b.y,a op b.z);}
OP2(+) OP2(-) OP2(*) OP2(/)
static inline float2& operator+=(float2&a,float2 b){a=a+b;return a;}
static inline float2& operator-=(float2&a,float2 b){a=a-b;return a;}
static inline float3& operator+=(float3&a,float3 b){a=a+b;return a;}
static inline float dot(float2 a,float2 b){return a.x*b.x+a.y*b.y;}
static inline float min(float a,float b){return a<b?a:b;}
static inline float max(float a,float b){return a>b?a:b;}
static inline float abs(float a){return a<0?-a:a;}
static inline float2 abs(float2 a){return float2(abs(a.x),abs(a.y));}
static inline float clamp(float v,float a,float b){return v<a?a:(v>b?b:v);}
static inline float saturate(float v){return clamp(v,0.0f,1.0f);}
static inline float smoothstep(float e0,float e1,float x){float t=saturate((x-e0)/(e1-e0));return t*t*(3.0f-2.0f*t);}
static inline float exp(float v){return std::exp(v);}
static inline float sin(float v){return std::sin(v);}
static inline float sqrt(float v){return std::sqrt(v);}
"""

MAIN = r"""
#include <cstdio>
#include <cstdlib>
#include <random>
#include <vector>
#include "shim.h"
#include "lens.h"

static std::mt19937 rng(20261024);
static double U(double a, double b) { return std::uniform_real_distribution<double>(a, b)(rng); }

// Brute force: the nearest point of the ellipse (a, b) to (x, y), in double, by dense sampling then golden refining.
static double brute(double x, double y, double a, double b) {
    const int N = 20000; double best = 1e30, bt = 0;
    for (int i = 0; i < N; i++) { double t = 2 * M_PI * i / N; double dx = a * cos(t) - x, dy = b * sin(t) - y;
        double d = dx * dx + dy * dy; if (d < best) { best = d; bt = t; } }
    double lo = bt - 2 * M_PI / N, hi = bt + 2 * M_PI / N;
    for (int k = 0; k < 80; k++) { double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
        auto f = [&](double t) { double dx = a * cos(t) - x, dy = b * sin(t) - y; return dx * dx + dy * dy; };
        if (f(m1) < f(m2)) hi = m2; else lo = m1; }
    double t = 0.5 * (lo + hi), dx = a * cos(t) - x, dy = b * sin(t) - y; return sqrt(dx * dx + dy * dy);
}

struct Ring { float4 rc, ra, rz, rl, rt; float sigma; };

// A random ring row: centre, axes, a constant inverse depth (depth 50), σ, a bend of 1.
static Ring randomRing(int kind) {
    Ring r; float a = (float)U(0.05, 0.6), ratio;
    if (kind == 0) ratio = (float)U(0.6, 1.0); else if (kind == 1) ratio = (float)U(0.08, 0.4);
    else if (kind == 2) ratio = (float)U(0.0025, 0.04); else ratio = 0.0f;
    float ang = (float)U(0, 2 * M_PI);
    float sigma = (float)(a * U(0.03, 0.15));
    r.rc = float4((float)U(-0.3, 0.3), (float)U(-0.2, 0.2), a, a * ratio);
    float invZ = 1.0f / 50.0f;
    r.rz = float4(invZ, (float)U(-0.3, 0.3) * invZ, (float)U(-0.3, 0.3) * invZ, sigma / invZ);
    // the cull reach, as ScreenRing sets it: 3σ at the ring's nearest point
    r.ra = float4(cosf(ang), sinf(ang), 3.0f * r.rz.w * (r.rz.x + sqrtf(r.rz.y * r.rz.y + r.rz.z * r.rz.z)), 1.0f);
    r.rl = float4(1.0f, 1.0f, 0.3f, 1.1f);
    r.rt = float4(0.62f, 0.91f, 1.0f, 3.0f);
    r.sigma = sigma;
    return r;
}

int main() {
    int fails = 0;
    // 1. NEAREST POINT
    double worstNear = 0; int nearCases = 0;
    for (int kind = 0; kind < 4; kind++)
        for (int n = 0; n < 1500; n++) {
            double a = U(0.05, 0.6), ratio = kind == 0 ? U(0.6, 1.0) : kind == 1 ? U(0.08, 0.4) : kind == 2 ? U(0.0025, 0.04) : 0.0;
            double b = a * ratio, x = U(-1.6, 1.6) * a, y = U(-1.6, 1.6) * a * (kind >= 2 ? 0.2 : 1.0);
            float2 q = VortexRingLensNearest(float2((float)x, (float)y), (float)a, (float)b);
            double ds = sqrt((q.x - x) * (q.x - x) + (q.y - y) * (q.y - y));
            double db = b > 0 ? brute(x, y, a, b) : sqrt(pow(fmax(fabs(x) - a, 0.0), 2) + y * y);
            double on = b > 0 ? fabs((q.x / a) * (q.x / a) + (q.y / b) * (q.y / b) - 1) : fabs(q.y) + fmax(0.0, fabs(q.x) - a);
            double err = fabs(ds - db) / a;
            if (b > 0 && b < a * 0.002) err = fmax(0.0, err - b / a);   // below the segment threshold: the segment
            worstNear = fmax(worstNear, err); nearCases++;
            if (err > 2e-4 || (b >= a * 0.002 && on > 2e-3)) {
                if (fails < 12) printf("A1 FAIL a=%g b=%g p=(%g,%g): shader %g brute %g on-ellipse %g\n", a, b, x, y, ds, db, on);
                fails++;
            }
        }
    printf("A1. nearest point: %d cases, worst error %.2g of a\n", nearCases, worstNear);

    // 2. PROFILE
    int profileBad = 0;
    if (VortexRingLensProfile(0.0f) != 0.0f || VortexRingLensRim(0.0f) != 0.0f) profileBad++;
    for (float u = 3.0f; u < 20.0f; u += 0.37f) if (VortexRingLensProfile(u) != 0.0f || VortexRingLensRim(u) != 0.0f) profileBad++;
    if (VortexRingLensProfile(2.999f) > 1e-5f) profileBad++;
    float peakU = 0, peak = 0; for (float u = 0; u < 3; u += 0.001f) { float v = VortexRingLensProfile(u); if (v > peak) { peak = v; peakU = u; } }
    printf("A2. profile: peak %.3f at u=%.2f, %d bad samples (zero on the core, zero from the reach)\n", peak, peakU, profileBad);
    if (profileBad) fails++;

    // 3. NO CREASE: walk lines across random rings in steps of σ/2000; a jump shows as a slope far above the profile's.
    double worstSlope = 0; int walks = 0;
    for (int kind = 0; kind < 4; kind++)
        for (int n = 0; n < 60; n++) {
            Ring r = randomRing(kind);
            double h = r.sigma / 2000.0, ang = U(0, M_PI);
            double ex = r.ra.x, ey = r.ra.y;
            // through the ellipse's interior near a tip, the centre or anywhere: the medial axis is crossed
            double along = U(-1.0, 1.0) * r.rc.z, across = U(-1.0, 1.0) * r.rc.w;
            double px0 = r.rc.x + along * ex - across * ey, py0 = r.rc.y + along * ey + across * ex;
            double dx = cos(ang), dy = sin(ang);
            int steps = (int)(4.0 * r.sigma / h);
            float2 prev; bool have = false;
            for (int s = -steps; s <= steps; s++) {
                float2 p((float)(px0 + dx * s * h), (float)(py0 + dy * s * h));
                float2 disp(0, 0); float3 glow(0, 0, 0);
                VortexRingLensRing(p, 1e9f, r.rc, r.ra, r.rz, r.rl, r.rt, disp, glow);
                if (have) { double j = sqrt(dot(disp - prev, disp - prev)) / h; if (j > worstSlope) worstSlope = j; }
                prev = disp; have = true;
            }
            walks++;
        }
    printf("A3. no crease: %d walks, worst slope %.2f (a crease reads > 30)\n", walks, worstSlope);
    if (worstSlope > 30.0) fails++;

    // 4. FOREGROUND: an opaque pixel in front of the core by more than the margin is not bent.
    int fgBad = 0, fgBent = 0;
    for (int n = 0; n < 2000; n++) {
        Ring r = randomRing(n % 3);
        float2 p((float)(r.rc.x + r.rc.z * U(-1.1, 1.1) * r.ra.x), (float)(r.rc.y + r.rc.z * U(-1.1, 1.1) * r.ra.y));
        float2 d1(0, 0), d2(0, 0); float3 g1(0, 0, 0), g2(0, 0, 0);
        VortexRingLensRing(p, 1e9f, r.rc, r.ra, r.rz, r.rl, r.rt, d1, g1);
        float zMin = 1.0f / (r.rz.x + sqrtf(r.rz.y * r.rz.y + r.rz.z * r.rz.z));
        VortexRingLensRing(p, zMin - r.rt.w - 0.01f, r.rc, r.ra, r.rz, r.rl, r.rt, d2, g2);
        if (dot(d1, d1) > 0) fgBent++;
        if (dot(d2, d2) != 0 || g2.x != 0) fgBad++;
    }
    printf("A4. foreground: %d bent pixels in the open, %d bent behind a foreground object (want 0)\n", fgBent, fgBad);
    if (fgBad || fgBent == 0) fails++;

    // 5. far pixels are untouched
    int farBad = 0;
    for (int n = 0; n < 2000; n++) {
        Ring r = randomRing(n % 4);
        double ang = U(0, 2 * M_PI), d = r.rc.z + 3.2 * (r.rz.w * (r.rz.x + sqrtf(r.rz.y * r.rz.y + r.rz.z * r.rz.z))) + U(0, 1);
        float2 p((float)(r.rc.x + d * cos(ang)), (float)(r.rc.y + d * sin(ang)));
        float2 disp(0, 0); float3 glow(0, 0, 0);
        VortexRingLensRing(p, 1e9f, r.rc, r.ra, r.rz, r.rl, r.rt, disp, glow);
        if (!VortexRingLensUntouched(disp, glow)) farBad++;
    }
    printf("A5. beyond the reach: %d of 2000 touched (want 0)\n", farBad);
    if (farBad) fails++;

    printf(fails ? "RESULT FAIL\n" : "RESULT PASS\n");
    return fails ? 1 : 0;
}
"""


def hlsl_to_cpp(src, mutate=False):
    """HLSL -> C++ spelling only."""
    out = src
    if mutate:
        out = out.replace("g = smoothstep(0.0, sigma * VORTEX_RING_LENS_MEDIAL_FADE, m);", "g = 1.0;")
        assert "g = 1.0;\n    }" in out, "the medial fade moved: update the negative control"
    out = re.sub(r"\binout (float[234]) (\w+)", r"\1& \2", out)
    out = re.sub(r"\.rgb\b", ".xyz()", out)
    out = re.sub(r"\.xy\b(?!\()", ".xy()", out)
    out = re.sub(r"\b(glow)\.r\b", r"\1.x", out)
    out = re.sub(r"\b(glow)\.g\b", r"\1.y", out)
    out = re.sub(r"\b(glow)\.b\b", r"\1.z", out)
    # float literals: 1.0 -> 1.0f, 1e-14 -> 1e-14f
    out = re.sub(r"(?<![\w.])(\d+\.\d*(?:e-?\d+)?|\d+e-?\d+)(?![\w.])", r"\1f", out)
    return out


def build_and_run(work, mutate, label):
    d = os.path.join(work, label)
    os.makedirs(d, exist_ok=True)
    open(os.path.join(d, "shim.h"), "w").write(SHIM)
    open(os.path.join(d, "lens.h"), "w").write(hlsl_to_cpp(open(HLSL, encoding="utf-8").read(), mutate))
    open(os.path.join(d, "main.cpp"), "w").write(MAIN)
    exe = os.path.join(d, "lens")
    cxx = shutil.which("clang++") or shutil.which("g++")
    if not cxx:
        print("A SKIPPED: no clang++ - FAIL")
        return None
    b = subprocess.run([cxx, "-std=c++17", "-O2", "-o", exe, os.path.join(d, "main.cpp")], capture_output=True, text=True)
    if b.returncode != 0:
        print(f"A: the HLSL did not compile as C++ ({label}):\n" + b.stderr[-3000:])
        return None
    r = subprocess.run([exe], capture_output=True, text=True)
    return r


# ---------------------------------------------------------------------------------------------------------
# B. the camera's numbers: the shipped C# ScreenRing against brute-force projection

CS_STUB = r"""
using System;
namespace UnityEngine {
  public static class Mathf {
    public static float Abs(float v) => Math.Abs(v);
    public static float Max(float a, float b) => a > b ? a : b;
    public static float Min(float a, float b) => a < b ? a : b;
    public static float Sqrt(float v) => (float)Math.Sqrt(v);
    public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
  }
  public struct Vector3 {
    public float x, y, z; public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
    public static Vector3 up => new Vector3(0, 1, 0); public static Vector3 right => new Vector3(1, 0, 0);
    public static Vector3 forward => new Vector3(0, 0, 1);
    public float sqrMagnitude => x * x + y * y + z * z;
    public Vector3 normalized { get { float m = (float)Math.Sqrt(sqrMagnitude); return new Vector3(x / m, y / m, z / m); } }
    public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  }
  public struct Vector4 { public float x, y, z, w; public Vector4(float a, float b, float c, float d) { x = a; y = b; z = c; w = d; } }
  public struct Matrix4x4 {
    public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33;
    public float this[int r, int c] {
      get { switch (r * 4 + c) { case 0: return m00; case 1: return m01; case 2: return m02; case 3: return m03; case 4: return m10;
        case 5: return m11; case 6: return m12; case 7: return m13; case 8: return m20; case 9: return m21; case 10: return m22;
        case 11: return m23; case 12: return m30; case 13: return m31; case 14: return m32; default: return m33; } }
      set { switch (r * 4 + c) { case 0: m00 = value; break; case 1: m01 = value; break; case 2: m02 = value; break; case 3: m03 = value; break;
        case 4: m10 = value; break; case 5: m11 = value; break; case 6: m12 = value; break; case 7: m13 = value; break;
        case 8: m20 = value; break; case 9: m21 = value; break; case 10: m22 = value; break; case 11: m23 = value; break;
        case 12: m30 = value; break; case 13: m31 = value; break; case 14: m32 = value; break; default: m33 = value; break; } }
    }
    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) {
      var m = new Matrix4x4();
      for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) { float s = 0; for (int k = 0; k < 4; k++) s += a[r, k] * b[k, c]; m[r, c] = s; }
      return m;
    }
  }
}
"""

CS_MAIN = r"""
using System;
using System.Globalization;
using UnityEngine;
public static class Program {
  public static void Main(string[] args) {
    var inv = CultureInfo.InvariantCulture;
    foreach (var line in Console.In.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
      var f = Array.ConvertAll(line.Split(' '), s => float.Parse(s, inv));
      Matrix4x4 P = new Matrix4x4(), V = new Matrix4x4();
      for (int i = 0; i < 16; i++) { P[i / 4, i % 4] = f[i]; V[i / 4, i % 4] = f[16 + i]; }
      bool ok = Lens.ScreenRing(P, V, f[32], f[33], new Vector3(f[34], f[35], f[36]), new Vector3(f[37], f[38], f[39]), f[40], f[41],
        out var rc, out var ra, out var rz, out float depth, out float fade);
      Console.WriteLine(!ok ? "skip" : string.Format(inv, "{0:R} {1:R} {2:R} {3:R} {4:R} {5:R} {6:R} {7:R} {8:R} {9:R} {10:R} {11:R} {12:R}",
        rc.x, rc.y, rc.z, rc.w, ra.x, ra.y, ra.z, rz.x, rz.y, rz.z, rz.w, depth, fade));
    }
  }
}
"""


def csharp_source(mutate=False):
    src = open(LENS_CS, encoding="utf-8").read()

    def member(pattern):
        m = re.search(pattern, src)
        assert m, f"{pattern} not found in VortexRingLens.cs"
        i = src.index("{", m.end())
        depth, j = 0, i
        while True:
            if src[j] == "{":
                depth += 1
            elif src[j] == "}":
                depth -= 1
                if depth == 0:
                    return src[m.start():j + 1]
            j += 1

    consts = re.search(r"public const float SigmaPerTube[^;]*;", src).group(0) + "\n" + \
        re.search(r"public const float Reach[^;]*;", src).group(0)
    screen = member(r"public static bool ScreenRing\(")
    clip = member(r"static void Clip\(")
    if mutate:
        assert "double cx = (hx - lam * arx) * k0" in screen, "ScreenRing's centre moved: update the negative control"
        screen = screen.replace("double cx = (hx - lam * arx) * k0", "double cx = (hx + lam * arx) * k0")
    return "using UnityEngine;\npublic static class Lens {\n" + consts + "\n" + screen + "\n" + clip + "\n}\n"


def look_at(pos, fwd, up):
    def norm(v):
        m = math.sqrt(sum(c * c for c in v))
        return [c / m for c in v]

    def cross(a, b):
        return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]

    def dot(a, b):
        return sum(x * y for x, y in zip(a, b))
    f = norm(fwd)
    r = norm(cross(up, f))
    u = cross(f, r)
    # Unity's worldToCameraMatrix: right-handed view space, the camera looks down -z
    return [[r[0], r[1], r[2], -dot(r, pos)], [u[0], u[1], u[2], -dot(u, pos)],
            [-f[0], -f[1], -f[2], dot(f, pos)], [0, 0, 0, 1]]


def frustum(l, r, b, t, n, f):
    return [[2 * n / (r - l), 0, (r + l) / (r - l), 0], [0, 2 * n / (t - b), (t + b) / (t - b), 0],
            [0, 0, -(f + n) / (f - n), -2 * f * n / (f - n)], [0, 0, -1, 0]]


def perspective(fov, aspect, n, f):
    t = n * math.tan(math.radians(fov) * 0.5)
    return frustum(-t * aspect, t * aspect, -t, t, n, f)


def cases(rnd):
    out = []
    for i in range(600):
        aspect = rnd.choice([16 / 9, 1.0, 0.6, 2.1])
        near = rnd.choice([0.3, 1.0])
        if i % 3 == 0:
            P = perspective(rnd.uniform(35, 90), aspect, near, 5000)
        else:
            # the Pistol's off-axis lens: a window off the camera's axis (PistolPlane.OffAxis)
            w = rnd.uniform(0.5, 2.0) * near
            h = w / aspect
            ox, oy = rnd.uniform(-1.2, 1.2) * w, rnd.uniform(-1.2, 1.2) * h
            P = frustum(ox - w / 2, ox + w / 2, oy - h / 2, oy + h / 2, near, 5000)
        cam = [rnd.uniform(-50, 50) for _ in range(3)]
        fwd = [rnd.uniform(-1, 1) for _ in range(3)]
        V = look_at(cam, fwd, [0, 1, 0])
        dist = rnd.uniform(40, 400)
        fl = math.sqrt(sum(c * c for c in fwd))
        side = [rnd.uniform(-0.4, 0.4) * dist for _ in range(3)]
        c = [cam[k] + fwd[k] / fl * dist + side[k] for k in range(3)]
        axis = [rnd.uniform(-1, 1) for _ in range(3)]
        if i % 10 == 0:
            # edge-on: the axis square to the line of sight, so the ring's plane holds the eye
            to = [c[k] - cam[k] for k in range(3)]
            tl = math.sqrt(sum(v * v for v in to))
            to = [v / tl for v in to]
            d = sum(a * b for a, b in zip(axis, to))
            axis = [axis[k] - d * to[k] for k in range(3)]
        radius = rnd.choice([22.0, 8.0, 40.0])
        tube = radius * 0.16
        row = [x for rr in P for x in rr] + [x for rr in V for x in rr] + [aspect, near] + c + axis + [radius, tube]
        out.append(row)
    return out


def check_rows(rows_in, rows_out):
    """Every projected ring point on the ellipse; its inverse depth on the row's formula."""
    worst_on, worst_z, bad, drawn = 0.0, 0.0, 0, 0
    for row, res in zip(rows_in, rows_out):
        if res == "skip":
            continue
        drawn += 1
        v = [float(x) for x in res.split()]
        cx, cy, a, b, ex, ey = v[0], v[1], v[2], v[3], v[4], v[5]
        k0, k1, k2 = v[7], v[8], v[9]
        P = [row[0:4], row[4:8], row[8:12], row[12:16]]
        V = [row[16:20], row[20:24], row[24:28], row[28:32]]
        aspect = row[32]
        c, axis, R = row[34:37], row[37:40], row[40]
        al = math.sqrt(sum(x * x for x in axis))
        n = [x / al for x in axis]
        ref = [0, 1, 0] if abs(n[1]) < 0.9 else [1, 0, 0]
        e1 = [n[1] * ref[2] - n[2] * ref[1], n[2] * ref[0] - n[0] * ref[2], n[0] * ref[1] - n[1] * ref[0]]
        m = math.sqrt(sum(x * x for x in e1))
        e1 = [x / m for x in e1]
        e2 = [n[1] * e1[2] - n[2] * e1[1], n[2] * e1[0] - n[0] * e1[2], n[0] * e1[1] - n[1] * e1[0]]
        fx, fy = -ey, ex
        for k in range(180):
            t = 2 * math.pi * k / 180
            w = [c[j] + R * (math.cos(t) * e1[j] + math.sin(t) * e2[j]) for j in range(3)] + [1.0]
            view = [sum(V[r][j] * w[j] for j in range(4)) for r in range(4)]
            clip = [sum(P[r][j] * view[j] for j in range(4)) for r in range(4)]
            px, py = clip[0] / clip[3] * aspect * 0.5, clip[1] / clip[3] * 0.5
            lx, ly = (px - cx) * ex + (py - cy) * ey, (px - cx) * fx + (py - cy) * fy
            if b > 2e-3 * a:
                on = abs(math.sqrt((lx / a) ** 2 + (ly / b) ** 2) - 1.0) * b / a
            else:
                on = (abs(ly) + max(0.0, abs(lx) - a)) / a
            worst_on = max(worst_on, on)
            if b > 0.02 * a:
                ct = max(-1.0, min(1.0, lx / a))
                st = math.copysign(math.sqrt(max(0.0, 1 - ct * ct)), ly)
                z = abs((k0 + k1 * ct + k2 * st) * clip[3] - 1.0)
                worst_z = max(worst_z, z)
            else:
                z = 0.0
            if on > 1e-3 or z > 2e-3:
                bad += 1
                break
    return drawn, worst_on, worst_z, bad


def dotnet_path():
    d = shutil.which("dotnet")
    if d:
        return d
    home = os.path.expanduser("~/.dotnet/dotnet")
    return home if os.path.exists(home) else None


def run_tier_b(work, require_real):
    dotnet = dotnet_path()
    if not dotnet:
        print("B SKIPPED: dotnet not found" + (" - FAIL (--require-real)" if require_real else ""))
        return not require_real
    major = subprocess.run([dotnet, "--version"], capture_output=True, text=True).stdout.strip().split(".")[0]
    rnd = random.Random(1024)
    rows = cases(rnd)
    stdin = "\n".join(" ".join(repr(float(x)) for x in r) for r in rows) + "\n"

    def build(label, mutate):
        proj = os.path.join(work, label)
        os.makedirs(proj, exist_ok=True)
        open(os.path.join(proj, f"{label}.csproj"), "w").write(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            f'<TargetFramework>net{major}.0</TargetFramework><Nullable>disable</Nullable>'
            '<ImplicitUsings>disable</ImplicitUsings><LangVersion>latest</LangVersion></PropertyGroup></Project>')
        open(os.path.join(proj, "Stub.cs"), "w").write(CS_STUB)
        open(os.path.join(proj, "Lens.cs"), "w").write(csharp_source(mutate))
        open(os.path.join(proj, "Program.cs"), "w").write(CS_MAIN)
        b = subprocess.run([dotnet, "build", "-nologo", "-v", "q", "-o", os.path.join(proj, "out")], cwd=proj,
                           capture_output=True, text=True)
        if b.returncode != 0:
            print(f"B: the shipped C# did not build ({label}):\n" + (b.stdout + b.stderr)[-3000:])
            return None
        r = subprocess.run([dotnet, os.path.join(proj, "out", f"{label}.dll")], input=stdin, capture_output=True, text=True)
        return r.stdout.strip().splitlines()

    out = build("shipped", False)
    if out is None or len(out) != len(rows):
        return False
    drawn, worst_on, worst_z, bad = check_rows(rows, out)
    edge = sum(1 for i, r in enumerate(out) if i % 10 == 0 and r != "skip" and float(r.split()[3]) < 0.02 * float(r.split()[2]))
    print(f"B. ScreenRing vs brute-force projection: {drawn} rings drawn of {len(rows)} ({edge} seen edge-on), "
          f"worst off-ellipse {worst_on:.2g} of a, worst inverse depth error {worst_z:.2g}, {bad} bad")
    ok = bad == 0 and drawn > 300 and edge > 10
    neg = build("negative", True)
    if neg is None:
        return False
    _, _, _, nbad = check_rows(rows, neg)
    print(f"B5. negative control (aberration map's sign flipped): {nbad} bad rings (want many)")
    return ok and nbad > 50


# ---------------------------------------------------------------------------------------------------------
# C. compile against the black hole lens's per-file URP mock

def run_tier_c(work, require_real):
    if not shutil.which("glslangValidator"):
        print("C SKIPPED: glslangValidator not found" + (" - FAIL (--require-real)" if require_real else ""))
        return not require_real
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import verify_black_hole_lens as bh
    d = os.path.join(work, "compile")
    os.makedirs(d, exist_ok=True)
    bh.write_mock_library(d)
    shutil.copy(HLSL, os.path.join(d, "VortexRingLens.hlsl"))
    shader = open(SHADER, encoding="utf-8").read()
    programs = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", shader, re.S)
    assert len(programs) == 1, f"expected 1 pass, found {len(programs)}"
    prog = programs[0]
    vert = re.search(r"#pragma vertex (\w+)", prog).group(1)
    frag = re.search(r"#pragma fragment (\w+)", prog).group(1)
    ok = True
    for stage, entry in (("vert", vert), ("frag", frag)):
        rc, out = bh.glslang_compile(d, prog, stage, entry)
        print(f"C. glslang {entry} [{stage}]: " + ("compiled" if rc == 0 else "FAIL\n" + out[-2000:]))
        ok &= rc == 0
    assert bh.DEPTH_INCLUDE in prog, "the shader no longer includes DeclareDepthTexture.hlsl"
    rc, out = bh.glslang_compile(d, prog.replace(bh.DEPTH_INCLUDE, ""), "frag", frag)
    fired = rc != 0 and "SampleSceneDepth" in out
    print(f"C2. negative control (DeclareDepthTexture.hlsl include removed): {'FIRED' if fired else 'DID NOT FIRE'}")
    return ok and fired


def main():
    keep = "--keep" in sys.argv
    require_real = "--require-real" in sys.argv
    work = tempfile.mkdtemp(prefix="vortex_ring_lens_")
    ok = True
    try:
        r = build_and_run(work, False, "shipped")
        if r is None:
            return 1
        print(r.stdout.rstrip())
        ok &= r.returncode == 0
        neg = build_and_run(work, True, "negative")
        if neg is None:
            return 1
        m = re.search(r"worst slope ([\d.]+)", neg.stdout)
        slope = float(m.group(1)) if m else 0.0
        print(f"A6. negative control (medial-axis fade removed): worst slope {slope:.1f} (want > 30)")
        ok &= slope > 30.0
        ok &= run_tier_b(work, require_real)
        ok &= run_tier_c(work, require_real)
    finally:
        if keep:
            print("kept", work)
        else:
            shutil.rmtree(work, ignore_errors=True)
    print("PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
