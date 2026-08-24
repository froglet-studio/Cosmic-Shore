#!/usr/bin/env python3
"""Prove the SHIPPED PrismDestructionSight.hlsl composes correctly — by compiling and running it.

The Dolphin's Echo Sight became visible to every player on 2026-08-19: your own cone still paints
in the pale cool cast, and every OTHER pilot's cone paints in their domain colour. That turned one
volume test into a priority rule over up to five sources, and three of its properties are the kind
that read as obviously true and are not:

  1. YOUR OWN SIGHT IS UNCHANGED. A prism your cone covers must be painted exactly as it was before
     peers existed — not "within a pixel", exactly, including when four rivals are aiming at the
     same prism. The first version of the rewrite ran every source (yours included) through one
     weighted average, which is algebraically identity-preserving for a single source and was NOT
     bit-identical: x/x*x rounds. This script caught that at 3,381 samples in 89,301.
  2. OVERLAPPING PEERS DO NOT BRIGHTEN. Four Dolphins is the roster of both Dolphin-only modes and
     their cones overlap constantly, so the peer channel blends HUE at the brightness of the
     strongest single contributor. Summing would blow the arena out to white exactly where the
     fight is thickest.
  3. THE IDLE PATH IS INERT. Nothing published anywhere must leave the graph's colour untouched.

Static reading cannot settle any of the three, so this runs the real thing: it translates the
shipped HLSL into compilable C++ with the smallest possible edit set (strip the [loop] attribute,
turn .xyz/.rgb swizzles into accessors, make the out parameter a reference), compiles it with
clang++, and executes it against a transcription of the pre-change function. What is measured is
the shipped file, not a paraphrase of it — the /asset-surgery 4.5c contract.

Usage:  python3 Tools/Shaders/verify_prism_sight_composition.py [--keep]
Exit 0 on pass. Needs clang++; nothing else, and no Unity.
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl")

SHIM = r"""// Minimal HLSL->C++ shim so the SHIPPED PrismDestructionSight.hlsl can be compiled and executed
// by clang++ (/asset-surgery 4.5c). Only what the file actually uses.
#pragma once
#include <cmath>
#include <algorithm>

struct float3 {
    float x=0,y=0,z=0;
    float3(){}
    float3(float a):x(a),y(a),z(a){}
    float3(float a,float b,float c):x(a),y(b),z(c){}
    float3 v3() const { return *this; }
};
struct float4 {
    float x=0,y=0,z=0,w=0;
    float4(){}
    float4(float a,float b,float c,float d):x(a),y(b),z(c),w(d){}
    float3 v3() const { return float3(x,y,z); }
};
static inline float3 operator+(float3 a,float3 b){return float3(a.x+b.x,a.y+b.y,a.z+b.z);}
static inline float3 operator-(float3 a,float3 b){return float3(a.x-b.x,a.y-b.y,a.z-b.z);}
static inline float3 operator*(float3 a,float b){return float3(a.x*b,a.y*b,a.z*b);}
static inline float3 operator*(float a,float3 b){return b*a;}
static inline float3 operator*(float3 a,float3 b){return float3(a.x*b.x,a.y*b.y,a.z*b.z);}
static inline float3 operator/(float3 a,float b){return float3(a.x/b,a.y/b,a.z/b);}
static inline float3& operator+=(float3&a,float3 b){a=a+b;return a;}
static inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static inline float length(float3 a){return std::sqrt(dot(a,a));}
static inline float saturate(float v){return std::min(1.0f,std::max(0.0f,v));}
static inline float clamp(float v,float lo,float hi){return std::min(hi,std::max(lo,v));}
static inline float lerp(float a,float b,float t){return a+(b-a)*t;}
static inline float3 lerp(float3 a,float3 b,float t){return float3(lerp(a.x,b.x,t),lerp(a.y,b.y,t),lerp(a.z,b.z,t));}
using std::pow;
// HLSL is loose about mixing float and double literals (min(total, 1.0)); C++ is not.
static inline float min(float a, float b){return a<b?a:b;}
static inline float min(float a, double b){return a<(float)b?a:(float)b;}
static inline float max(float a, float b){return a>b?a:b;}
static inline float max(float a, double b){return a>(float)b?a:(float)b;}
static inline int   min(int a, int b){return a<b?a:b;}

// The object matrix the whole-prism sample point is read from. The harness drives it.
struct M4 { float _m03=0,_m13=0,_m23=0; };
extern M4 g_objectToWorld;
static inline M4 GetObjectToWorldMatrix(){ return g_objectToWorld; }
"""

MAIN = r"""#include "shipped.h"
#include <cstdio>
#include <cstdlib>
#include <random>

M4 g_objectToWorld;

// ---- The function EXACTLY as it stood before the peer channel (git HEAD version), for the
//      identity proof: with no peer sight published the new file must compute the same bits.
static void Reference(float3 PositionWS, float3 Apex, float3 Axis, float3 Gape, float3 Params,
                      float Strength, float3 BaseColor, float3 &Color)
{
    Color = BaseColor;
    float Highlight = 0.0;
    float height = Params.x;
    if (height <= 0.0 || Strength <= 0.0) return;
    float3 samplePos = float3(GetObjectToWorldMatrix()._m03,
                              GetObjectToWorldMatrix()._m13,
                              GetObjectToWorldMatrix()._m23);
    float3 rel = samplePos - Apex;
    float s = dot(rel, Axis);
    if (s <= 0.0 || s > height) return;
    float3 radial = rel - Axis * s;
    float halfLength = Params.z * s;
    float along = dot(radial, Gape);
    float3 offAxis = radial - Gape * clamp(along, -halfLength, halfLength);
    float coreRadius = Params.y * s;
    if (coreRadius <= 0.0) return;
    float d = length(offAxis);
    if (d > coreRadius) return;
    float edge = saturate(d / coreRadius);
    float fill = lerp(PRISM_SIGHT_CORE_FILL, 1.0, pow(edge, PRISM_SIGHT_EDGE_POWER));
    Highlight = fill * Strength;
    Color = BaseColor + PRISM_SIGHT_COLOR * (Highlight * PRISM_SIGHT_GAIN);
}

static std::mt19937 rng(20260819);
static float rnd(float a, float b) { return a + (b-a) * (rng() / (float)rng.max()); }
static float3 rndDir() {
    for(;;){ float3 v(rnd(-1,1),rnd(-1,1),rnd(-1,1)); float l=length(v); if(l>0.1f) return v/l; }
}

int main()
{
    // ---------- 1. IDENTITY: no peer sight published => bit-identical to the old function ----------
    _PrismSightPeerCount = 0.0f;
    int checked = 0, lit = 0, mismatch = 0;
    for (int t = 0; t < 200000; ++t) {
        float3 apex(rnd(-500,500), rnd(-500,500), rnd(-500,500));
        float3 axis = rndDir(), gape = rndDir();
        gape = gape - axis * dot(gape, axis);
        float gl = length(gape); if (gl < 1e-3f) continue; gape = gape / gl;
        float3 params(rnd(-50, 2400), rnd(0.0f, 0.4f), rnd(0.0f, 0.4f));
        float strength = rnd(-0.1f, 1.0f);
        float3 base(rnd(0,1), rnd(0,1), rnd(0,1));
        g_objectToWorld._m03 = apex.x + rnd(-2500,2500);
        g_objectToWorld._m13 = apex.y + rnd(-2500,2500);
        g_objectToWorld._m23 = apex.z + rnd(-2500,2500);
        // bias half the samples to land inside the cone so the lit branch is actually exercised
        if (t % 2 == 0 && params.x > 0) {
            float s = rnd(0.01f, 1.0f) * params.x;
            float3 p = apex + axis * s + gape * (rnd(-1,1) * params.z * s);
            float3 perp(-axis.y, axis.x, axis.z * 0.0f);
            float pl = length(perp); if (pl > 1e-3f) p += (perp / pl) * (rnd(-1,1) * params.y * s);
            g_objectToWorld._m03 = p.x; g_objectToWorld._m13 = p.y; g_objectToWorld._m23 = p.z;
        }
        float3 a, b;
        PrismDestructionSight_float(float3(0,0,0), apex, axis, gape, params, strength, base, a);
        Reference(float3(0,0,0), apex, axis, gape, params, strength, base, b);
        ++checked;
        if (a.x != base.x || a.y != base.y || a.z != base.z) ++lit;
        if (a.x != b.x || a.y != b.y || a.z != b.z) {
            if (++mismatch <= 3)
                printf("  MISMATCH new(%.9g,%.9g,%.9g) ref(%.9g,%.9g,%.9g)\n", a.x,a.y,a.z, b.x,b.y,b.z);
        }
    }
    printf("1. local-only identity : %d samples, %d lit, %d mismatches -> %s\n",
           checked, lit, mismatch, mismatch == 0 ? "EXACT" : "FAIL");

    // ---------- 2. CLAMP: N overlapping sights never exceed one sight's brightness ----------
    // Every source aimed at the same point at full fill.
    float3 apex(0,0,0), axis(0,0,1), gape(0,1,0);
    float3 params(1000.0f, 0.5f, 0.5f);
    g_objectToWorld._m03 = 0; g_objectToWorld._m13 = 0; g_objectToWorld._m23 = 500; // dead centre
    float3 base(0.1f, 0.1f, 0.1f);

    float3 off0(-1,-1,-1); float3 noParams(-1,0,0);
    _PrismSightPeerCount = 1.0f;
    _PrismSightPeerApex[0] = float4(apex.x, apex.y, apex.z, params.x);
    _PrismSightPeerAxis[0] = float4(axis.x, axis.y, axis.z, params.y);
    _PrismSightPeerGape[0] = float4(gape.x, gape.y, gape.z, params.z);
    _PrismSightPeerTint[0] = float4(1.0f, 0.0f, 0.4f, 1.0f);
    float3 one; PrismDestructionSight_float(float3(0,0,0), off0, axis, gape, noParams, 0.0f, base, one);
    float onePeak = max(one.x - base.x, max(one.y - base.y, one.z - base.z));

    float worst = 0.0f;
    for (int n = 1; n <= 4; ++n) {
        _PrismSightPeerCount = (float)n;
        for (int i = 0; i < n; ++i) {
            _PrismSightPeerApex[i] = float4(apex.x, apex.y, apex.z, params.x);
            _PrismSightPeerAxis[i] = float4(axis.x, axis.y, axis.z, params.y);
            _PrismSightPeerGape[i] = float4(gape.x, gape.y, gape.z, params.z);
            _PrismSightPeerTint[i] = float4(1.0f, 0.0f, 0.4f, 1.0f);   // identical: count is the only variable
        }
        float3 c; PrismDestructionSight_float(float3(0,0,0), off0, axis, gape, noParams, 0.0f, base, c);
        float peak = max(c.x - base.x, max(c.y - base.y, c.z - base.z));
        worst = max(worst, peak);
        printf("   %d overlapping peers -> added (%.4f,%.4f,%.4f) peak %.4f\n", n, c.x-base.x, c.y-base.y, c.z-base.z, peak);
    }
    printf("2. peer overlap        : one peer peak %.4f, worst of 1-4 peers %.4f -> %s\n",
           onePeak, worst, worst <= onePeak + 1e-6f ? "BOUNDED (never brighter than one)" : "FAIL (blowout)");

    // ---------- 2b. OWN SIGHT IS EXCLUSIVE: rivals in the same volume cannot recolour it --------
    _PrismSightPeerCount = 4.0f;
    for (int i = 0; i < 4; ++i) {
        _PrismSightPeerApex[i] = float4(apex.x, apex.y, apex.z, params.x);
        _PrismSightPeerAxis[i] = float4(axis.x, axis.y, axis.z, params.y);
        _PrismSightPeerGape[i] = float4(gape.x, gape.y, gape.z, params.z);
        _PrismSightPeerTint[i] = float4(1.0f, 0.0f, 0.0f, 1.0f);
    }
    float3 mineCrowded, mineAlone, refAlone;
    PrismDestructionSight_float(float3(0,0,0), apex, axis, gape, params, 1.0f, base, mineCrowded);
    _PrismSightPeerCount = 0.0f;
    PrismDestructionSight_float(float3(0,0,0), apex, axis, gape, params, 1.0f, base, mineAlone);
    Reference(float3(0,0,0), apex, axis, gape, params, 1.0f, base, refAlone);
    bool exclusive = mineCrowded.x==mineAlone.x && mineCrowded.y==mineAlone.y && mineCrowded.z==mineAlone.z
                  && mineAlone.x==refAlone.x && mineAlone.y==refAlone.y && mineAlone.z==refAlone.z;
    printf("2b. own sight exclusive: mine+4 rivals (%.6f,%.6f,%.6f) vs mine alone (%.6f,%.6f,%.6f) -> %s\n",
           mineCrowded.x,mineCrowded.y,mineCrowded.z, mineAlone.x,mineAlone.y,mineAlone.z,
           exclusive ? "IDENTICAL (and equals pre-change)" : "FAIL");

    // ---------- 3. PEER-ONLY: a rival's sight lights mass with nobody holding locally ----------
    _PrismSightPeerCount = 1.0f;
    _PrismSightPeerApex[0] = float4(apex.x, apex.y, apex.z, params.x);
    _PrismSightPeerAxis[0] = float4(axis.x, axis.y, axis.z, params.y);
    _PrismSightPeerGape[0] = float4(gape.x, gape.y, gape.z, params.z);
    _PrismSightPeerTint[0] = float4(1.0f, 0.15f, 0.15f, 1.0f);   // a Ruby-ish signal colour
    float3 peerOnly; float3 off(-1,-1,-1);
    PrismDestructionSight_float(float3(0,0,0), off, axis, gape, float3(-1,0,0), 0.0f, base, peerOnly);
    printf("3. peer with no own    : added (%.4f,%.4f,%.4f) -> %s\n",
           peerOnly.x-base.x, peerOnly.y-base.y, peerOnly.z-base.z,
           (peerOnly.x - base.x) > (peerOnly.z - base.z) ? "TINTED BY DOMAIN" : "FAIL (no hue)");

    // ---------- 3b. TWO DOMAINS BLEND in hue but not in brightness ----------
    {
        _PrismSightPeerCount = 2.0f;
        for (int i = 0; i < 2; ++i) {
            _PrismSightPeerApex[i] = float4(apex.x, apex.y, apex.z, params.x);
            _PrismSightPeerAxis[i] = float4(axis.x, axis.y, axis.z, params.y);
            _PrismSightPeerGape[i] = float4(gape.x, gape.y, gape.z, params.z);
        }
        _PrismSightPeerTint[0] = float4(1.0f, 0.10f, 0.10f, 1.0f);   // a red-ish domain
        _PrismSightPeerTint[1] = float4(0.10f, 1.0f, 0.40f, 1.0f);   // a green-ish domain
        float3 both; PrismDestructionSight_float(float3(0,0,0), off, axis, gape, float3(-1,0,0), 0.0f, base, both);
        _PrismSightPeerCount = 1.0f;
        float3 redOnly; PrismDestructionSight_float(float3(0,0,0), off, axis, gape, float3(-1,0,0), 0.0f, base, redOnly);
        float peakBoth = max(both.x-base.x, max(both.y-base.y, both.z-base.z));
        float peakRed  = max(redOnly.x-base.x, max(redOnly.y-base.y, redOnly.z-base.z));
        bool blended = (both.y-base.y) > (redOnly.y-base.y) && (both.x-base.x) < (redOnly.x-base.x);
        printf("3b. two domains        : red (%.4f,%.4f,%.4f) + green -> (%.4f,%.4f,%.4f); peak %.4f vs %.4f -> %s\n",
               redOnly.x-base.x, redOnly.y-base.y, redOnly.z-base.z, both.x-base.x, both.y-base.y, both.z-base.z,
               peakBoth, peakRed,
               (blended && peakBoth <= peakRed + 1e-6f) ? "HUE BLENDS, BRIGHTNESS DOES NOT" : "FAIL");
    }

    // ---------- 4. FULLY IDLE: nothing published anywhere leaves the colour untouched ----------
    _PrismSightPeerCount = 0.0f;
    float3 idle; PrismDestructionSight_float(float3(0,0,0), off, axis, gape, float3(0,0,0), 0.0f, base, idle);
    printf("4. idle passthrough    : %s\n",
           (idle.x==base.x && idle.y==base.y && idle.z==base.z) ? "UNTOUCHED" : "FAIL");

    return mismatch == 0 ? 0 : 1;
}
"""


def translate(src):
    """The shipped HLSL, made compilable. Every edit here is mechanical and asserted."""
    out = src.replace("[loop]\n", "")
    assert out != src, "[loop] attribute not found - has the peer loop been removed?"
    out, n = re.subn(r"\.xyz\b", ".v3()", out)
    assert n >= 3, f"expected the peer loop's .xyz swizzles, found {n}"
    out, n = re.subn(r"\.rgb\b", ".v3()", out)
    assert n >= 1, "expected the peer tint's .rgb swizzle"
    out = re.sub(r"\b(tint)\.a\b", r"\1.w", out)
    assert "out float3 Color)" in out, "entry point signature changed"
    out = out.replace("out float3 Color)", "float3 &Color)")
    for name in ("_PrismSightPeerApex", "_PrismSightPeerAxis", "_PrismSightPeerGape",
                 "_PrismSightPeerTint", "_PrismSightPeerCount"):
        assert name in out, f"{name} missing from the shipped HLSL"
    return out


def main():
    keep = "--keep" in sys.argv
    if not shutil.which("clang++"):
        print("clang++ not found - install it or run this where it is available", file=sys.stderr)
        return 2

    work = tempfile.mkdtemp(prefix="prism_sight_verify_")
    try:
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(SHIM)
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write('#include "shim.h"\n' + translate(open(HLSL).read()))
        with open(os.path.join(work, "main.cpp"), "w") as f:
            f.write(MAIN)

        binary = os.path.join(work, "verify")
        build = subprocess.run(["clang++", "-std=c++17", "-O2", "-Wall",
                                "-o", binary, os.path.join(work, "main.cpp")],
                               cwd=work, capture_output=True, text=True)
        if build.returncode != 0:
            print("COMPILE FAILED - the shipped HLSL does not build:\n" + build.stderr, file=sys.stderr)
            return 1

        run = subprocess.run([binary], capture_output=True, text=True)
        sys.stdout.write(run.stdout)
        if run.stderr:
            sys.stderr.write(run.stderr)
        if run.returncode != 0 or "FAIL" in run.stdout:
            print("\nFAILED", file=sys.stderr)
            return 1
        print("\nAll composition properties hold for the shipped file.")
        return 0
    finally:
        if keep:
            print("kept: " + work)
        else:
            shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
