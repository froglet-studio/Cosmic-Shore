#!/usr/bin/env python3
"""Prove the SHIPPED VesselVisionShading.hlsl behaves — by compiling and running it.

The vessel vision band (Docs/VESSEL_VISION.md) is a platform law whose failure modes are all
SILENT: a band that never reaches 1 just looks like a weak effect, a band with a discontinuity
looks like a pop that somebody will blame on the network, and a quantizer that overshoots its top
tone just looks like the mark is too bright. None of those can be settled by reading the file, so
this compiles the real thing and measures it.

Five properties are asserted, and each one is a mistake that was available while writing it:

  1. THE OFF SENTINEL IS INERT. farEnd <= 0 must return exactly 0 at every distance, because that
     is the state the publisher writes before anything else has run and on every teardown.
  2. THE BAND IS EXACTLY ZERO OUTSIDE ITSELF, AND EXACTLY ONE ACROSS ITS PLATEAU. A plateau that
     peaks at 0.999 is a law that never actually reaches full strength; the mark would be
     permanently slightly wrong and nothing would say so.
  3. BOTH EDGES ARE MONOTONE AND CONTINUOUS. The whole reason the edges are graded rather than
     thresholded is that a mark which pops reads as a new object appearing. A bounded
     frame-to-frame step is what "graded" actually means, so it is measured rather than asserted.
  4. THE QUANTIZER PRODUCES EXACTLY celSteps TONES AND NEVER EXCEEDS 1. floor(ndv * steps) lands on
     `steps` itself at ndv == 1, which without the min() guard pushes the top band to
     steps/(steps-1) and blows the brightest tone out by a factor the config cannot see.
  5. AN UNSTAMPED OBJECT IS UNTOUCHED, BIT FOR BIT. VesselGraph is also worn by a projectile
     material; alpha 0 must return the base colour exactly, not "within a pixel".

What is measured is the shipped file, not a paraphrase of it — the /asset-surgery 4.5c contract.

Usage:  python3 Tools/Shaders/verify_vessel_vision_band.py [--keep]
Exit 0 on pass. Needs clang++; nothing else, and no Unity.
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/VesselVisionShading.hlsl")

SHIM = r"""// Minimal HLSL->C++ shim so the SHIPPED VesselVisionShading.hlsl can be compiled and executed
// by clang++ (/asset-surgery 4.5c). Only what the file actually uses.
#pragma once
#include <cmath>
#include <algorithm>

struct float3 {
    union { struct { float x, y, z; }; struct { float r, g, b; }; };
    float3() : x(0), y(0), z(0) {}
    float3(float a) : x(a), y(a), z(a) {}
    float3(float a, float b_, float c) : x(a), y(b_), z(c) {}
    float3 v3() const { return *this; }
    float3 zyx() const { return float3(z, y, x); }
};
struct float4 {
    union { struct { float x, y, z, w; }; struct { float r, g, b, a; }; };
    float4() : x(0), y(0), z(0), w(0) {}
    float4(float a_, float b_, float c, float d) : x(a_), y(b_), z(c), w(d) {}
    float3 v3() const { return float3(x, y, z); }
};
static inline float3 operator+(float3 a, float3 b) { return float3(a.x+b.x, a.y+b.y, a.z+b.z); }
static inline float3 operator-(float3 a, float3 b) { return float3(a.x-b.x, a.y-b.y, a.z-b.z); }
static inline float3 operator*(float3 a, float3 b) { return float3(a.x*b.x, a.y*b.y, a.z*b.z); }
static inline float3 operator*(float3 a, float s)  { return float3(a.x*s, a.y*s, a.z*s); }
static inline float3 operator*(float s, float3 a)  { return a * s; }
static inline float3 &operator+=(float3 &a, float s) { a.x += s; a.y += s; a.z += s; return a; }

static inline float saturate(float v) { return v < 0.f ? 0.f : (v > 1.f ? 1.f : v); }
static inline float lerp(float a, float b, float t) { return a + (b - a) * t; }
static inline float3 lerp(float3 a, float3 b, float t) {
    return float3(lerp(a.x,b.x,t), lerp(a.y,b.y,t), lerp(a.z,b.z,t));
}
static inline float dot(float3 a, float3 b) { return a.x*b.x + a.y*b.y + a.z*b.z; }
static inline float3 normalize(float3 a) {
    float l = std::sqrt(dot(a,a)); return l > 0.f ? float3(a.x/l, a.y/l, a.z/l) : a;
}
static inline float distance(float3 a, float3 b) { return std::sqrt(dot(a-b, a-b)); }
// HLSL's max/min promote across float and double literals; C++'s std:: templates refuse to.
// The shipped file writes `max(x, 1.0)` because that is legal HLSL, so the shim must accept it
// rather than the file being rewritten to suit the harness.
static inline float floor(float v) { return std::floor(v); }
static inline float max(float a, float b) { return a > b ? a : b; }
static inline float max(float a, double b) { return max(a, (float)b); }
static inline float max(double a, float b) { return max((float)a, b); }
static inline float max(double a, double b) { return max((float)a, (float)b); }
static inline float min(float a, float b) { return a < b ? a : b; }
static inline float min(float a, double b) { return min(a, (float)b); }
static inline float min(double a, float b) { return min((float)a, b); }
static inline float min(double a, double b) { return min((float)a, (float)b); }

static inline float frac(float v) { return v - std::floor(v); }
static inline float3 frac(float3 v) { return float3(frac(v.x), frac(v.y), frac(v.z)); }
static inline float3 floor(float3 v) { return float3(std::floor(v.x), std::floor(v.y), std::floor(v.z)); }
static inline float3 operator+(float3 a, float s) { return float3(a.x+s, a.y+s, a.z+s); }

// Per-draw state the shipped file reads. Set by the harness before each call.
struct float3x3 {
    float m00=1,m01=0,m02=0, m10=0,m11=1,m12=0, m20=0,m21=0,m22=1;
};
struct M4 {
    float _m03 = 0, _m13 = 0, _m23 = 0;
    // The harness runs the object frame at identity, so object space == world space. That is
    // enough to exercise every branch of the cell address; what it cannot check is Unity's own
    // choice of matrix for a skinned renderer, which is a runtime question, not a maths one.
    operator float3x3() const { return float3x3(); }
};
static M4 g_objectToWorld;
static inline M4 GetObjectToWorldMatrix() { return g_objectToWorld; }
static inline M4 GetWorldToObjectMatrix() { return M4(); }
static inline float3 mul(float3x3 m, float3 v) {
    return float3(m.m00*v.x + m.m01*v.y + m.m02*v.z,
                  m.m10*v.x + m.m11*v.y + m.m12*v.z,
                  m.m20*v.x + m.m21*v.y + m.m22*v.z);
}
static float4 _WorldSpaceCameraPos = float4(0, 0, 0, 0);
"""

HARNESS = r"""
#include <cstdio>
#include <vector>

static int failures = 0;
static void check(bool ok, const char *what) {
    if (!ok) { std::printf("  FAIL  %s\n", what); ++failures; }
}

static void setBand(float a, float b, float c, float d) { _VesselVisionBand = float4(a,b,c,d); }
static void setShape(float s, float steps, float floor_, float gain) {
    _VesselVisionShape = float4(s, steps, floor_, gain);
}
static void setRim(float i, float o, float g) { _VesselVisionRim = float4(i, o, g, 0.f); }
static void setBreakup(float cells, float reach, float strength, float endDist) {
    _VesselVisionBreakup = float4(cells, reach, strength, endDist);
}

// A synthetic spherical hull of radius R centred on the object origin, sampled over its
// CAMERA-FACING hemisphere — the fragments that actually rasterize. Returns the shipped entry
// point's output plus the facing term, so a test can say "this sample is near the centre" or
// "this sample is on the silhouette" the same way the shader does.
struct HullSample { float3 color; float ndv; };
static HullSample shadeHull(float distanceToCamera, float hullRadius, int i, int n,
                            float3 tintRgb, float tintA, float3 baseColor) {
    g_objectToWorld = M4();
    _WorldSpaceCameraPos = float4(0, 0, -distanceToCamera, 0);

    // Fibonacci-ish sweep over the facing hemisphere: u drives the polar angle from dead-centre
    // (facing the camera) out to the silhouette, phi spins around it.
    float u = (float)i / (float)(n - 1);
    float polar = u * 1.5707963f;                       // 0 = facing, pi/2 = silhouette
    float phi = (float)i * 2.399963f;                   // golden angle, so samples never line up
    float3 nrm = float3(std::sin(polar) * std::cos(phi), std::sin(polar) * std::sin(phi),
                        -std::cos(polar));
    float3 pos = float3(nrm.x * hullRadius, nrm.y * hullRadius, nrm.z * hullRadius);

    float3 out_;
    VesselVisionShade_float(pos, nrm, float4(tintRgb.x, tintRgb.y, tintRgb.z, tintA),
                            baseColor, out_);

    float3 view = normalize(float3(0, 0, -distanceToCamera) - pos);
    HullSample s; s.color = out_; s.ndv = saturate(dot(normalize(nrm), view));
    return s;
}

// Drive the shipped entry point at a chosen camera distance, straight down -z from the origin.
static float3 shadeAt(float distanceToCamera, float3 tintRgb, float tintA, float3 baseColor,
                      float3 normal) {
    g_objectToWorld = M4();                       // object at the world origin
    _WorldSpaceCameraPos = float4(0, 0, -distanceToCamera, 0);
    float3 positionWS = float3(0, 0, 0);
    float3 out_;
    VesselVisionShade_float(positionWS, normal, float4(tintRgb.x, tintRgb.y, tintRgb.z, tintA),
                            baseColor, out_);
    return out_;
}

int main() {
    const float NEAR_START = 150.f, NEAR_FULL = 350.f, FAR_FULL = 2000.f, FAR_END = 3500.f;
    setShape(0.85f, 3.f, 0.35f, 1.15f);
    setRim(0.55f, 0.95f, 1.1f);

    // ---- 1. the off sentinel is inert -------------------------------------------------
    setBand(0, 0, 0, 0);
    for (float d = 0; d <= 6000.f; d += 7.f)
        check(VesselVisionBand01(d) == 0.f, "off sentinel returned a non-zero band");
    setBand(NEAR_START, NEAR_FULL, FAR_FULL, FAR_END);

    // ---- 2. exactly zero outside, exactly one on the plateau --------------------------
    for (float d = 0; d <= NEAR_START; d += 1.f)
        check(VesselVisionBand01(d) == 0.f, "band is non-zero at or below nearStart");
    for (float d = FAR_END; d <= 12000.f; d += 13.f)
        check(VesselVisionBand01(d) == 0.f, "band is non-zero at or beyond farEnd");
    for (float d = NEAR_FULL; d <= FAR_FULL; d += 3.f)
        check(VesselVisionBand01(d) == 1.f, "band is not exactly 1 across the plateau");

    // ---- 3. both edges monotone and continuous ----------------------------------------
    // A frame at 60fps crossing the rising edge at a boosted 357 u/s moves ~6 units, so the step
    // is measured over a 6-unit sample and must stay small enough to read as a fade.
    float prev = VesselVisionBand01(0.f), worstStep = 0.f;
    for (float d = 0.f; d <= 6000.f; d += 6.f) {
        float v = VesselVisionBand01(d);
        worstStep = max(worstStep, std::fabs(v - prev));
        prev = v;
    }
    check(worstStep < 0.06f, "band steps too hard between adjacent frames to read as a fade");

    prev = -1.f;
    for (float d = NEAR_START; d <= NEAR_FULL; d += 1.f) {
        float v = VesselVisionBand01(d);
        check(v >= prev - 1e-6f, "rising edge is not monotone");
        prev = v;
    }
    prev = 2.f;
    for (float d = FAR_FULL; d <= FAR_END; d += 1.f) {
        float v = VesselVisionBand01(d);
        check(v <= prev + 1e-6f, "falling edge is not monotone");
        prev = v;
    }

    // ---- 4. the quantizer: exactly celSteps tones, never over 1 ------------------------
    for (int steps = 2; steps <= 6; ++steps) {
        setShape(1.f, (float)steps, 0.35f, 1.f);
        setRim(2.f, 3.f, 0.f);                    // rim window off the [0,1] domain: tone only
        std::vector<float> tones;
        float worstTone = 0.f;
        for (int i = 0; i <= 20000; ++i) {
            float ndv = (float)i / 20000.f;
            // A normal whose dot with the view direction is exactly ndv.
            float3 n = float3(std::sqrt(max(0.f, 1.f - ndv * ndv)), 0.f, -ndv);
            float rimUnused;
            float3 c = VesselVisionCel(n, float3(0, 0, -1), float3(1, 1, 1), rimUnused);
            worstTone = max(worstTone, c.x);
            bool seen = false;
            for (float t : tones) if (std::fabs(t - c.x) < 1e-5f) { seen = true; break; }
            if (!seen) tones.push_back(c.x);
        }
        char msg[128];
        std::snprintf(msg, sizeof msg, "celSteps=%d produced %d distinct tones", steps, (int)tones.size());
        check((int)tones.size() == steps, msg);
        std::snprintf(msg, sizeof msg, "celSteps=%d top tone overshot 1 (%.6f)", steps, worstTone);
        check(worstTone <= 1.f + 1e-6f, msg);
    }
    setShape(0.85f, 3.f, 0.35f, 1.15f);
    setRim(0.55f, 0.95f, 1.1f);

    // ---- 5. an unstamped object is untouched, bit for bit -----------------------------
    float3 base(0.13f, 0.71f, 0.97f);
    float3 nrm(0.f, 0.f, -1.f);
    for (float d = 0.f; d <= 6000.f; d += 11.f) {
        float3 c = shadeAt(d, float3(1, 0, 0), 0.f, base, nrm);     // alpha 0 -> not a vessel
        check(c.x == base.x && c.y == base.y && c.z == base.z,
              "an object with no published tint was modified");
    }
    // ...and a stamped one inside the near floor is likewise untouched, which is what excludes
    // the local pilot's own hull.
    for (float d = 0.f; d <= NEAR_START; d += 3.f) {
        float3 c = shadeAt(d, float3(1, 0, 0), 1.f, base, nrm);
        check(c.x == base.x && c.y == base.y && c.z == base.z,
              "a stamped vessel inside the near floor was modified");
    }
    // ...and a stamped one on the plateau IS marked.
    float3 marked = shadeAt(1000.f, float3(1, 0, 0), 1.f, base, nrm);
    check(marked.x != base.x || marked.y != base.y || marked.z != base.z,
          "a stamped vessel on the plateau was NOT marked");

    // ---- 6-10. the centre break-up -----------------------------------------------------
    {
        const int N = 4000;
        const float HULL = 12.f, MID = 700.f;
        float3 hullBase(0.13f, 0.71f, 0.97f), tint(1.f, 0.2f, 0.2f);

        // 6. inert when authored off: bit-identical to the undithered mark.
        setBreakup(3.5f, 0.75f, 0.f, 900.f);
        std::vector<float3> off;
        for (int i = 0; i < N; ++i) off.push_back(shadeHull(MID, HULL, i, N, tint, 1.f, hullBase).color);

        setBreakup(0.f, 0.75f, 0.85f, 900.f);            // zero cells is also "off"
        for (int i = 0; i < N; ++i) {
            float3 c = shadeHull(MID, HULL, i, N, tint, 1.f, hullBase).color;
            check(c.x == off[i].x && c.y == off[i].y && c.z == off[i].z,
                  "break-up with zero cells was not bit-identical to break-up disabled");
        }

        setBreakup(3.5f, 0.75f, 0.85f, 900.f);

        // 7 + 8 + 9: sweep the hull once and measure.
        int centreBroken = 0, centreSamples = 0, rimSolid = 0, rimSamples = 0;
        float worstOvershoot = 0.f;
        for (int i = 0; i < N; ++i) {
            HullSample s = shadeHull(MID, HULL, i, N, tint, 1.f, hullBase);
            check(!std::isnan(s.color.x) && !std::isnan(s.color.y) && !std::isnan(s.color.z),
                  "break-up produced a NaN");

            // 9. the dither may only move a fragment back TOWARD the hull's own colour: it can
            // never overshoot the undithered mark, and it can never darken past the base.
            for (int ch = 0; ch < 3; ++ch) {
                float b  = ch == 0 ? hullBase.x : (ch == 1 ? hullBase.y : hullBase.z);
                float u  = ch == 0 ? off[i].x  : (ch == 1 ? off[i].y  : off[i].z);
                float d  = ch == 0 ? s.color.x : (ch == 1 ? s.color.y : s.color.z);
                float lo = min(b, u), hi = max(b, u);
                worstOvershoot = max(worstOvershoot, max(lo - d, d - hi));
            }

            if (s.ndv > 0.9f) {                          // dead-centre of the facing hemisphere
                ++centreSamples;
                float diff = std::fabs(s.color.x - off[i].x) + std::fabs(s.color.y - off[i].y) +
                             std::fabs(s.color.z - off[i].z);
                if (diff > 1e-4f) ++centreBroken;
            }
            if (s.ndv < 0.05f) {                         // the silhouette
                ++rimSamples;
                float diff = std::fabs(s.color.x - off[i].x) + std::fabs(s.color.y - off[i].y) +
                             std::fabs(s.color.z - off[i].z);
                if (diff <= 1e-5f) ++rimSolid;
            }
        }
        check(worstOvershoot <= 1e-5f,
              "the break-up moved a fragment OUTSIDE the segment between the hull colour and the "
              "undithered mark — it must modulate the blend, never the colour");
        check(centreSamples > 0 && centreBroken * 3 > centreSamples,
              "the centre was barely broken up at all — the whole point is that the flat interior "
              "gets the hull's own shading back");
        check(rimSamples > 0 && rimSolid == rimSamples,
              "the silhouette was dithered — the rim is the part of the mark that survives at "
              "range and must be exempt from the break-up");

        // 10. it closes back into a solid mark by endDistance, while the band is still at full
        // strength (endDistance 900 sits inside the plateau).
        for (int i = 0; i < N; ++i) {
            float3 far_ = shadeHull(950.f, HULL, i, N, tint, 1.f, hullBase).color;
            setBreakup(3.5f, 0.75f, 0.f, 900.f);
            float3 solid = shadeHull(950.f, HULL, i, N, tint, 1.f, hullBase).color;
            setBreakup(3.5f, 0.75f, 0.85f, 900.f);
            check(std::fabs(far_.x - solid.x) < 1e-5f && std::fabs(far_.y - solid.y) < 1e-5f &&
                  std::fabs(far_.z - solid.z) < 1e-5f,
                  "the break-up had not closed by endDistance — past there the cells fall under a "
                  "pixel and read as noise instead of texture");
        }
    }

    if (failures == 0) std::printf("  VesselVisionShading.hlsl: all properties hold.\n");
    return failures == 0 ? 0 : 1;
}
"""


def to_cpp(hlsl_text):
    """Smallest edit set that makes the shipped HLSL compilable as C++."""
    src = hlsl_text
    src = src.replace("#ifndef VESSEL_VISION_SHADING_INCLUDED", "")
    src = src.replace("#define VESSEL_VISION_SHADING_INCLUDED", "")
    src = src.replace("#endif // VESSEL_VISION_SHADING_INCLUDED", "")
    # out parameter -> reference
    # HLSL `out` parameters -> C++ references. Applied to the whole file rather than to one
    # known signature, because a second out-parameter (VesselVisionCel's rim) was added later and
    # a hard-coded replacement silently stopped translating the file it claims to verify.
    src = re.sub(r"\bout (float3|float2|float) ", r"\1 &", src)
    # swizzles the shim exposes as accessors
    src = re.sub(r"\.rgb\b", ".v3()", src)
    src = re.sub(r"\.zyx\b", ".zyx()", src)
    src = re.sub(r"\.xyz\b", ".v3()", src)
    return src


def main():
    keep = "--keep" in sys.argv
    if shutil.which("clang++") is None:
        print("clang++ not found — cannot verify the shipped HLSL.", file=sys.stderr)
        return 2

    hlsl = open(HLSL, encoding="utf-8").read()

    # Guard rails on the transform: if the shipped file stops containing what we translate, the
    # translation is silently testing something else.
    for needle in ("VesselVisionBand01", "VesselVisionCel", "VesselVisionShade_float",
                   "VesselVisionBreakup01", "GetObjectToWorldMatrix", "GetWorldToObjectMatrix",
                   "_WorldSpaceCameraPos"):
        if needle not in hlsl:
            print(f"shipped HLSL no longer contains {needle} — refusing to verify a stale shape.",
                  file=sys.stderr)
            return 2

    tmp = tempfile.mkdtemp(prefix="vesselvision_")
    try:
        open(os.path.join(tmp, "shim.h"), "w").write(SHIM)
        open(os.path.join(tmp, "main.cpp"), "w").write(
            '#include "shim.h"\n' + to_cpp(hlsl) + HARNESS)
        exe = os.path.join(tmp, "verify")
        build = subprocess.run(
            ["clang++", "-std=c++17", "-O0", "-Wno-gnu-anonymous-struct", "-Wno-nested-anon-types",
             os.path.join(tmp, "main.cpp"), "-o", exe],
            capture_output=True, text=True)
        if build.returncode != 0:
            print(build.stderr, file=sys.stderr)
            return 2
        run = subprocess.run([exe], capture_output=True, text=True)
        print(run.stdout, end="")
        if run.stderr:
            print(run.stderr, file=sys.stderr, end="")
        return run.returncode
    finally:
        if keep:
            print(f"  (kept {tmp})")
        else:
            shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
