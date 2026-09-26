#!/usr/bin/env python3
"""
Prove the SHIPPED PrismSlice.hlsl / PrismSlice.shader do what Docs/PRISM_ANIMATION.md §4.10 says
the Rhino sword's slice does. Two independent halves, neither a transcription of the shader:

A. EXECUTION (clang++, /asset-surgery §4.5c). The file under Assets/ is translated mechanically
   (HLSL -> C++ spelling only) and RUN on randomized cuts of randomized prisms:

   1. IDENTITY, UNSTAMPED: a zero plane leaves every vertex and normal bit-identical and far-free.
   2. THE KEPT SIDE IS UNTOUCHED: every vertex on the kept side of a real cut is bit-identical.
   3. THE CAP IS THE CROSS-SECTION: every far vertex lands ON the cut (to float noise) and INSIDE
      the prism (all six slabs) — no overhang past the true edge, the failure an orthogonal
      projection has on a slanted face.
   4. THE CAP COVERS THE CROSS-SECTION, ONCE: random points of the true cross-section are each
      hit by exactly the far-surface point on the ray from the centre through them, and that
      point maps back onto them. With (3) that is the bijection the header claims.
   5. CONTINUITY: a vertex approaching the cut from beyond maps to itself in the limit — the kept
      skin and the cap meet without a crack.
   6. WATERTIGHT: two copies of one position with different face normals (a cube edge, the way
      HighPolyPrismMesh duplicates it) map to bit-identical positions.
   7. THE CAP FACES OUT: a far triangle wound outward on its face stays wound outward (toward m)
      after the map, and the returned normal is exactly m.
   8. RIGID: the motion preserves every distance, at every age, to float noise.
   9. NO INTERPENETRATION: with the hinge on the trailing edge (the rule PrismSliceGeometry
      stamps), each half stays in its own closed half-space at every age for every opening up to
      90 degrees, so the two halves can never pass through each other.
  10. THE DISSOLVE WINDOW: progress is 0 before the window, monotone through it, and past 1 by
      its end — so every fragment is gone BEFORE retirement.
  11. THE CUT GOES FIRST: at equal noise, the cut face (depth 0) is clipped strictly before the
      far end (depth 1), for every progress in the window.
  12. THE RIM CLIP: a straddling fragment beyond the cut is clipped, a cap fragment is not, the
      kept skin is not (before the dissolve).
  13. NOISE is in [0, 1] and continuous.
  NEGATIVE CONTROLS, each must FAIL the test it guards:
  14. -DPRISM_SLICE_CAP_FLAG=2   — the rim clip eats the whole cap (test 12 fires).
  15. -DPRISM_SLICE_DISSOLVE_OVERSHOOT=0 — the deepest fragment survives to retirement (test 10).
  16. a centre OUTSIDE the kept half (a broken stamp) — the cap stops being the cross-section
      (test 3/4 fire), which is what the CPU's "centre strictly inside" check exists to prevent.

B. FRONT-END COMPILE (glslang, HLSL mode). Every pass of PrismSlice.shader, vertex and fragment,
   in both the plain and the DOTS-instanced variant, against a mock of the URP library that
   declares exactly the functions and macros the shader uses. This proves the shader's own code
   is well-formed HLSL (types, intrinsics, semantics, the DOTS macro usage); it cannot prove the
   URP library behaves as the mock says. That half is the editor's.

Exit 0 on pass. Needs clang++ and glslangValidator; no Unity.
Usage:  python3 Tools/Shaders/verify_prism_slice.py [--keep]
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/PrismSlice.hlsl")
SHADER = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/PrismSlice.shader")

SHIM = r"""#pragma once
#include <cmath>
#include <algorithm>
struct float2 { float x=0,y=0; float2(){} float2(float a,float b):x(a),y(b){} };
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
    float3 xyz() const { return float3(x,y,z); }
    float2 xy() const { return float2(x,y); }
};
static inline float3 operator+(float3 a,float3 b){return float3(a.x+b.x,a.y+b.y,a.z+b.z);}
static inline float3 operator-(float3 a,float3 b){return float3(a.x-b.x,a.y-b.y,a.z-b.z);}
static inline float3 operator*(float3 a,float3 b){return float3(a.x*b.x,a.y*b.y,a.z*b.z);}
static inline float3 operator*(float3 a,float b){return float3(a.x*b,a.y*b,a.z*b);}
static inline float3 operator*(float a,float3 b){return b*a;}
static inline float3 operator-(float a,float3 b){return float3(a-b.x,a-b.y,a-b.z);}
static inline float3 operator/(float3 a,float b){return float3(a.x/b,a.y/b,a.z/b);}
static inline float3 operator-(float3 a){return float3(-a.x,-a.y,-a.z);}
static inline float3& operator*=(float3&a,float b){a=a*b;return a;}
static inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static inline float3 cross(float3 a,float3 b){return float3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
static inline float length(float3 a){return std::sqrt(dot(a,a));}
static inline float saturate(float v){return std::min(1.0f,std::max(0.0f,v));}
static inline float min(float a,float b){return a<b?a:b;}
static inline float max(float a,float b){return a>b?a:b;}
static inline float lerp(float a,float b,float t){return a+(b-a)*t;}
static inline float frac(float v){return v-std::floor(v);}
static inline float3 frac(float3 v){return float3(frac(v.x),frac(v.y),frac(v.z));}
static inline float3 floor(float3 v){return float3(std::floor(v.x),std::floor(v.y),std::floor(v.z));}
static inline void sincos(float a,float&s,float&c){s=std::sin(a);c=std::cos(a);}
using std::exp;
"""

HARNESS = r"""#include "shipped.h"
#include <cstdio>
#include <random>
#include <vector>
#include <cstring>

static int failures = 0;
static int reported = 0;
#define CHECK(cond, ...) do { if (!(cond)) { failures++; if (reported++ < 12) { printf("FAIL: "); printf(__VA_ARGS__); printf("\n"); } } } while (0)

static std::mt19937 rng(20260926);
static float rnd(float a, float b) { return a + (b - a) * (rng() / (float)rng.max()); }
static float3 rndDir() { for(;;){ float3 v(rnd(-1,1),rnd(-1,1),rnd(-1,1)); float l=length(v); if(l>0.1f) return v/l; } }
static bool bitEq(float3 a, float3 b) { return std::memcmp(&a,&b,sizeof(float3))==0; }

// ---- a cut of the unit prism (object space, half-extent 0.5) ----
struct Cut { float4 plane; float3 centre; };
static const float H = 0.5f;
static float3 corner(int i){ return float3((i&1)?H:-H, (i&2)?H:-H, (i&4)?H:-H); }
static const int EDGES[24] = {0,1,2,3,4,5,6,7, 0,2,1,3,4,6,5,7, 0,4,1,5,2,6,3,7};
static float dist(const Cut& c, float3 p){ return dot(c.plane.xyz(), p) - c.plane.w; }

// A random cut: random direction, random non-unit length (the stamp is Mᵀn, never unit), an
// offset that keeps both halves solid. Centre = mean of the half's polytope vertices — the rule
// PrismSliceGeometry uses, reproduced here only to BUILD an input the shader is promised.
static bool makeCut(Cut& c) {
    float3 m = rndDir() * rnd(0.3f, 6.0f);
    float support = H * (std::fabs(m.x)+std::fabs(m.y)+std::fabs(m.z));
    float d = rnd(-0.6f, 0.6f) * support;
    c.plane = float4(m.x,m.y,m.z,d);
    float3 acc(0); int n = 0;
    for (int i=0;i<8;i++) if (dist(c,corner(i)) <= 0) { acc = acc + corner(i); n++; }
    for (int e=0;e<12;e++){ float3 a=corner(EDGES[2*e]), b=corner(EDGES[2*e+1]); float da=dist(c,a), db=dist(c,b);
        if ((da<0&&db>0)||(da>0&&db<0)) { acc = acc + (a + (b-a)*(da/(da-db))); n++; } }
    if (n < 4) return false;
    c.centre = acc / (float)n;
    return dist(c, c.centre) < -1e-4f * length(m);
}

struct Surf { float3 p, n; };
static float3 faceN(int f){ switch(f){case 0:return float3(1,0,0);case 1:return float3(-1,0,0);case 2:return float3(0,1,0);case 3:return float3(0,-1,0);case 4:return float3(0,0,1);default:return float3(0,0,-1);} }
static void faceUV(int f, float3& u, float3& v){ // cross(u,v) == n
    switch(f){case 0:u=float3(0,1,0);v=float3(0,0,1);break;case 1:u=float3(0,0,1);v=float3(0,1,0);break;
              case 2:u=float3(0,0,1);v=float3(1,0,0);break;case 3:u=float3(1,0,0);v=float3(0,0,1);break;
              case 4:u=float3(1,0,0);v=float3(0,1,0);break;default:u=float3(0,1,0);v=float3(1,0,0);break;} }
static Surf surfPoint(int f, float a, float b){ float3 u,v; faceUV(f,u,v); Surf s; s.n=faceN(f); s.p=s.n*H + u*(a*H) + v*(b*H); return s; }

static bool insideBox(float3 p, float tol){ return std::fabs(p.x)<=H+tol && std::fabs(p.y)<=H+tol && std::fabs(p.z)<=H+tol; }

// Ray from c through y (both inside the box): where it leaves the box.
static float3 exitPoint(float3 c, float3 y) {
    float3 dir = y - c; float tmax = 1e30f;
    float cc[3]={c.x,c.y,c.z}, dd[3]={dir.x,dir.y,dir.z};
    for(int i=0;i<3;i++){ if (std::fabs(dd[i])>1e-12f){ float t=((dd[i]>0?H:-H)-cc[i])/dd[i]; if(t<tmax) tmax=t; } }
    return c + dir*tmax;
}

static void run_cut_tests(bool brokenCentre) {
    // 1. unstamped identity
    if (!brokenCentre) {
        for (int k=0;k<2000;k++){ Surf s=surfPoint(rng()%6, rnd(-1,1), rnd(-1,1));
            float3 op, on; float rd, far;
            PrismSliceCut(s.p, s.n, float4(0,0,0,0), float3(0,0,0), op, on, rd, far);
            CHECK(bitEq(op,s.p) && bitEq(on,s.n) && far==0.0f, "unstamped plane moved a vertex"); }
    }

    int cuts = 0;
    while (cuts < 300) {
        Cut c; if (!makeCut(c)) continue; cuts++;
        if (brokenCentre) {
            // Break the contract: put the centre on the FAR side of the cut, still inside the box.
            float3 m = c.plane.xyz(); float3 un = m / length(m);
            c.centre = c.centre + un * (std::fabs(dist(c, c.centre)) / length(m) * 2.2f);
            if (!insideBox(c.centre, 0)) { cuts--; continue; }
        }
        float mlen = length(c.plane.xyz());
        float3 mhat = c.plane.xyz() / mlen;

        for (int k=0;k<400;k++) {
            int f = rng()%6; Surf s = surfPoint(f, rnd(-1,1), rnd(-1,1));
            float3 op, on; float rd, far;
            PrismSliceCut(s.p, s.n, c.plane, c.centre, op, on, rd, far);
            float dp = dist(c, s.p);
            if (dp <= 0) {
                // 2. kept side untouched
                CHECK(bitEq(op,s.p) && bitEq(on,s.n) && far==0.0f, "kept vertex moved");
            } else {
                // 3. on the cut and inside the prism
                CHECK(std::fabs(dist(c, op)) <= 2e-5f * mlen, "far vertex not on the cut (%g)", dist(c,op)/mlen);
                CHECK(insideBox(op, 2e-5f), "far vertex projected OUTSIDE the prism (%g %g %g)", op.x, op.y, op.z);
                CHECK(far == 1.0f, "far flag not set");
                // 7. returned normal is exactly m
                CHECK(bitEq(on, c.plane.xyz()), "cap normal is not m");
                CHECK(rd == -dp, "rest depth is not -dist");
            }
            // 6. watertight: same position, a different normal
            float3 op2, on2; float rd2, far2;
            PrismSliceCut(s.p, rndDir(), c.plane, c.centre, op2, on2, rd2, far2);
            CHECK(bitEq(op, op2), "duplicate edge vertex mapped to a different position");
        }

        // 4. coverage: random cross-section points
        float3 u = cross(mhat, std::fabs(mhat.x) < 0.9f ? float3(1,0,0) : float3(0,1,0)); u = u / length(u);
        float3 v = cross(mhat, u);
        float3 foot = mhat * (c.plane.w / mlen);
        int hits = 0;
        for (int k=0;k<200;k++) {
            float3 y = foot + u*rnd(-1,1) + v*rnd(-1,1);
            if (!insideBox(y, -1e-3f)) continue;
            if (dist(c, c.centre) >= 0) { CHECK(false, "centre not inside the kept half"); break; }
            float3 x = exitPoint(c.centre, y);
            CHECK(dist(c, x) >= -1e-5f * mlen, "ray from the centre through the cross-section left on the KEPT side");
            // the surface normal at x: the face it left through
            float3 nx = std::fabs(std::fabs(x.x)-H)<1e-5f ? float3(x.x>0?1:-1,0,0) : std::fabs(std::fabs(x.y)-H)<1e-5f ? float3(0,x.y>0?1:-1,0) : float3(0,0,x.z>0?1:-1);
            float3 op, on; float rd, far;
            PrismSliceCut(x, nx, c.plane, c.centre, op, on, rd, far);
            CHECK(length(op - y) <= 2e-4f, "cross-section point not covered by its own ray (%g)", length(op-y));
            hits++;
        }

        if (!brokenCentre) {
            // 5. continuity at the cut, as a CONVERGENCE: a point a distance e beyond the cut moves
            //    by O(e) (the map's Lipschitz constant is |p - C| / |dist(C)|, large when the centre
            //    is near the cut, so a fixed tolerance would only measure the cut). Halving e must
            //    halve the displacement — the signature of a map continuous at the plane.
            for (int k=0;k<50;k++) {
                int f = rng()%6; Surf s = surfPoint(f, rnd(-1,1), rnd(-1,1));
                float dp = dist(c, s.p);
                float3 onPlane = s.p - mhat * (dp / mlen);
                if (!insideBox(onPlane, -0.01f)) continue;
                float moved[3];
                for (int j=0;j<3;j++) {
                    float e = 2e-3f / (float)(1 << j);
                    float3 near = onPlane + mhat * e;
                    float3 op, on; float rd, far;
                    PrismSliceCut(near, s.n, c.plane, c.centre, op, on, rd, far);
                    moved[j] = length(op - near);
                }
                if (moved[0] < 1e-6f) continue;
                float r1 = moved[1] / moved[0], r2 = moved[2] / moved[1];
                CHECK(r1 > 0.4f && r1 < 0.6f && r2 > 0.4f && r2 < 0.6f,
                      "discontinuity at the cut (ratios %g %g)", r1, r2);
            }
            // 7. cap winding: a small outward-wound far triangle stays outward-wound (toward m)
            for (int k=0;k<200;k++) {
                int f = rng()%6; float a=rnd(-0.8f,0.8f), b=rnd(-0.8f,0.8f), e=0.02f;
                Surf p0=surfPoint(f,a,b), p1=surfPoint(f,a+e,b), p2=surfPoint(f,a,b+e);
                if (dist(c,p0.p)<=1e-3f || dist(c,p1.p)<=1e-3f || dist(c,p2.p)<=1e-3f) continue;
                float3 q0,q1,q2,nn; float rd, far;
                PrismSliceCut(p0.p,p0.n,c.plane,c.centre,q0,nn,rd,far);
                PrismSliceCut(p1.p,p1.n,c.plane,c.centre,q1,nn,rd,far);
                PrismSliceCut(p2.p,p2.n,c.plane,c.centre,q2,nn,rd,far);
                float3 g = cross(q1-q0, q2-q0);
                if (length(g) < 1e-12f) continue;          // edge-on to the centre: degenerate image
                CHECK(dot(g, c.plane.xyz()) > 0, "cap triangle wound INWARD");
            }
        }
    }
}

// ---- world-space rigid motion ----
static void run_motion_tests() {
    for (int k=0;k<3000;k++) {
        float3 pivot(rnd(-50,50),rnd(-50,50),rnd(-50,50)), axis=rndDir(), away=rndDir(), drift(rnd(-40,40),rnd(-40,40),rnd(-40,40));
        float angle = rnd(0, 1.5708f), sep = rnd(0, 3), age = rnd(0, 2);
        float S, O, D; PrismSliceMotion(age, float4(0.09f,0.3f,0.35f,0), S, O, D);
        float3 a(rnd(-60,60),rnd(-60,60),rnd(-60,60)), b(rnd(-60,60),rnd(-60,60),rnd(-60,60));
        float3 ma = PrismSliceMove(a, pivot, away, sep, axis, angle*O, drift, S, D);
        float3 mb = PrismSliceMove(b, pivot, away, sep, axis, angle*O, drift, S, D);
        float d0 = length(a-b), d1 = length(ma-mb);
        CHECK(std::fabs(d0-d1) <= 1e-4f * (1 + d0), "motion is not rigid (%g vs %g)", d0, d1);   // 8
        CHECK(S>=0 && S<=1 && O>=0 && O<=1 && D>=0 && D<=0.35f+1e-6f, "envelope out of range");
    }
    // 9. no interpenetration with the hinge on the trailing edge
    for (int trial=0; trial<400; trial++) {
        float3 n = rndDir();                         // outward normal of half A (toward B)
        float3 T = cross(n, rndDir()); T = T / length(T);
        float3 Q(rnd(-5,5),rnd(-5,5),rnd(-5,5));
        std::vector<float3> A, B;
        for (int i=0;i<300;i++){ float3 p = Q + rndDir()*rnd(0,4); if (dot(p-Q,n) <= 0) A.push_back(p); else B.push_back(p); }
        auto hinge = [&](const std::vector<float3>& pts, float3 out) {
            float best=1e30f; float3 bp(0);
            for (auto& p: pts){ float t=dot(T,p); if (t<best){best=t;bp=p;} }
            return bp - out * dot(out, bp - Q);
        };
        float3 hA = hinge(A, n), hB = hinge(B, -n);
        float3 axA = cross(n, T), axB = cross(-n, T);
        float angle = rnd(0, 1.5708f), sep = rnd(0, 1);
        float3 drift(rnd(-20,20),rnd(-20,20),rnd(-20,20));
        for (int s=0;s<12;s++) {
            float age = s * 0.1f;
            float S, O, D; PrismSliceMotion(age, float4(0.09f,0.3f,0.35f,0), S, O, D);
            float3 carry = drift * D;
            for (auto& p: A){ float3 q = PrismSliceMove(p, hA, -n, sep, axA, angle*O, drift, S, D);
                CHECK(dot(q - carry - Q, n) <= 1e-3f, "half A crossed the cut (%g)", dot(q-carry-Q,n)); }
            for (auto& p: B){ float3 q = PrismSliceMove(p, hB, n, sep, axB, angle*O, drift, S, D);
                CHECK(dot(q - carry - Q, n) >= -1e-3f, "half B crossed the cut (%g)", dot(q-carry-Q,n)); }
        }
    }
}

// ---- the dissolve ----
static void run_dissolve_tests() {
    float2 window(0.3f, 0.08f);
    float life = 1.15f;
    float prev = -1;
    for (int i=0;i<=1000;i++) {
        float age = life * i / 1000.0f;
        float p = PrismSliceDissolveProgress(age, life, window);
        if (age < window.x*life - 1e-4f) CHECK(p == 0.0f, "dissolve started before its window");
        CHECK(p >= prev, "dissolve progress went backwards");
        prev = p;
        if (age >= (1 - window.y)*life + 1e-4f)
            CHECK(!(PrismSliceDissolveValue(1.0f, 1.0f, 0.32f) >= p) && PrismSliceClipped(0.5f, 0.0f, 1.0f, p),
                  "a fragment survives past the dissolve window (progress %g)", p);    // 10
    }
    CHECK(PrismSliceDissolveProgress(5.0f, 0.0f, window) == 0.0f, "unstamped life dissolved");

    // 11. the cut goes first
    for (int k=0;k<5000;k++) {
        float noise = rnd(0,1), amt = rnd(0,0.95f), p = rnd(0.001f, 1.0f);
        bool capGone = PrismSliceClipped(0.0f, 1.0f, PrismSliceDissolveValue(0.0f, noise, amt), p);
        bool farGone = PrismSliceClipped(1.0f, 0.0f, PrismSliceDissolveValue(1.0f, noise, amt), p);
        CHECK(capGone || !farGone, "the far end dissolved before the cut face");
    }

    // 12. the rim clip
    CHECK(PrismSliceClipped(-0.01f, 0.4f, 1.0f, 0.0f), "straddling fragment beyond the cut survived");
    CHECK(!PrismSliceClipped(-0.3f, 1.0f, 1.0f, 0.0f), "a cap fragment was rim-clipped");
    CHECK(!PrismSliceClipped(0.2f, 0.0f, 1.0f, 0.0f), "the kept skin was clipped before the dissolve");

    // 13. noise range + continuity
    for (int k=0;k<20000;k++) {
        float3 x(rnd(-100,100),rnd(-100,100),rnd(-100,100));
        float a = PrismSliceNoise(x), b = PrismSliceNoise(x + rndDir()*1e-3f);
        CHECK(a >= 0 && a <= 1, "noise out of range (%g)", a);
        CHECK(std::fabs(a-b) <= 0.02f, "noise not continuous (%g)", std::fabs(a-b));
    }
}

int main(int argc, char** argv) {
    bool broken = argc > 1 && std::strcmp(argv[1], "broken-centre") == 0;
    if (broken) { run_cut_tests(true); }
    else { run_cut_tests(false); run_motion_tests(); run_dissolve_tests(); }
    printf("%s: %d failure(s)\n", broken ? "broken-centre control" : "slice", failures);
    return failures ? 1 : 0;
}
"""


def translate(src):
    """HLSL -> C++ spelling. Mechanical only."""
    out = src
    out = re.sub(r"\bout float3 (\w+)", r"float3 &\1", out)
    out = re.sub(r"\bout float (\w+)", r"float &\1", out)
    out = re.sub(r"\.xyz\b", ".xyz()", out)
    out = re.sub(r"\.xy\b", ".xy()", out)
    for name in ("void PrismSliceCut(", "float3 PrismSliceMove(", "void PrismSliceMotion(",
                 "float PrismSliceDissolveProgress(", "bool PrismSliceClipped(", "float PrismSliceNoise("):
        assert name in out, f"{name} missing from the shipped HLSL"
    return out


def build_and_run(work, flags, arg, label):
    exe = os.path.join(work, "slice_" + re.sub(r"\W", "_", label))
    cmd = ["clang++", "-std=c++17", "-O1", "-ffp-contract=off", "-o", exe,
           os.path.join(work, "main.cpp"), "-I", work] + flags
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr)
        raise SystemExit(f"[{label}] compile failed")
    r = subprocess.run([exe] + ([arg] if arg else []), capture_output=True, text=True)
    return r.returncode, r.stdout.strip()


# ---------------------------------------------------------------------------------------------
# B. glslang front-end compile of every pass.
# ---------------------------------------------------------------------------------------------

URP_MOCK = r"""// Mock of the URP library surface PrismSlice.shader uses — declarations only, so glslang can
// type-check the shader's OWN code. It proves nothing about the library itself.
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
#define UNITY_VERTEX_INPUT_INSTANCE_ID uint instanceID : SV_InstanceID;
#define UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_SETUP_INSTANCE_ID(v)
#define UNITY_TRANSFER_INSTANCE_ID(a, b) b.instanceID = a.instanceID
#define UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o)
#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i)
#define FRONT_FACE_TYPE bool
#define FRONT_FACE_SEMANTIC SV_IsFrontFace
#define IS_FRONT_VFACE(v, a, b) ((v) ? (a) : (b))
#ifdef DOTS_INSTANCING_ON
    #define UNITY_DOTS_INSTANCING_ENABLED
    #define UNITY_DOTS_INSTANCING_START(name) cbuffer name {
    #define UNITY_DOTS_INSTANCED_PROP(type, var) uint unity_DOTSInstancing_##var;
    #define UNITY_DOTS_INSTANCING_END(name) };
    #define UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var) ((type)(var) + (type)(unity_DOTSInstancing_##var * 0))
#endif
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4x4 unity_MatrixVP;
float3 _WorldSpaceCameraPos;
float4x4 GetObjectToWorldMatrix() { return unity_ObjectToWorld; }
float4x4 GetWorldToObjectMatrix() { return unity_WorldToObject; }
float3 TransformObjectToWorld(float3 p) { return mul(unity_ObjectToWorld, float4(p, 1.0)).xyz; }
float3 TransformObjectToWorldNormal(float3 n) { return normalize(mul(n, (float3x3)unity_WorldToObject)); }
float4 TransformWorldToHClip(float3 p) { return mul(unity_MatrixVP, float4(p, 1.0)); }
float3 GetWorldSpaceViewDir(float3 p) { return _WorldSpaceCameraPos - p; }
float3 SafeNormalize(float3 v) { return v * rsqrt(max(1.175494351e-38, dot(v, v))); }
float2 PackNormalOctQuadEncode(float3 n) { return n.xy; }
float4 _ScreenParams;
float4 _Time;
float4x4 UNITY_MATRIX_V;
float4x4 UNITY_MATRIX_P;
float3 PackFloat2To888(float2 f) { return float3(f, 0.0); }
"""


def extract_passes(shader_src):
    inc = re.search(r"HLSLINCLUDE(.*?)ENDHLSL", shader_src, re.S)
    assert inc, "no HLSLINCLUDE block"
    programs = re.findall(r"HLSLPROGRAM(.*?)ENDHLSL", shader_src, re.S)
    assert len(programs) == 3, f"expected 3 passes, found {len(programs)}"
    passes = []
    for prog in programs:
        vert = re.search(r"#pragma vertex (\w+)", prog).group(1)
        frag = re.search(r"#pragma fragment (\w+)", prog).group(1)
        passes.append((vert, frag, prog))
    return inc.group(1), passes


def glslang_compile(work, include, program, stage, entry, defines):
    body = include + "\n" + program
    body = body.replace('#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"',
                        '#include "urp_mock.hlsl"')
    body = re.sub(r"#include_with_pragmas[^\n]*\n", "\n", body)
    body = re.sub(r"#pragma[^\n]*\n", "\n", body)
    path = os.path.join(work, f"pass_{entry}_{stage}_{'_'.join(defines) or 'plain'}.hlsl")
    with open(path, "w") as f:
        f.write(body)
    cmd = ["glslangValidator", "-D", "-V", "--target-env", "vulkan1.1", "-S", stage, "-e", entry,
           "-I" + work, "-o", os.devnull] + [f"-D{d}" for d in defines] + [path]
    r = subprocess.run(cmd, capture_output=True, text=True)
    return r.returncode, (r.stdout + r.stderr).strip()


def main():
    keep = "--keep" in sys.argv
    for tool in ("clang++", "glslangValidator"):
        if shutil.which(tool) is None:
            raise SystemExit(f"{tool} not found")
    src = open(HLSL).read()
    shader = open(SHADER).read()
    work = tempfile.mkdtemp(prefix="verify_prism_slice_")
    ok = True
    try:
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(SHIM)
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write('#pragma once\n#include "shim.h"\n' + translate(src))
        with open(os.path.join(work, "main.cpp"), "w") as f:
            f.write(HARNESS)

        # A. execution
        rc, out = build_and_run(work, [], None, "shipped")
        print(out)
        ok &= rc == 0

        for flag, label in (("-DPRISM_SLICE_CAP_FLAG=2.0", "cap-flag control"),
                            ("-DPRISM_SLICE_DISSOLVE_OVERSHOOT=0.0", "overshoot control")):
            rc, out = build_and_run(work, [flag], None, label)
            fired = rc != 0
            print(f"negative control [{label}]: {'FIRED' if fired else 'DID NOT FIRE'} ({out.splitlines()[-1]})")
            ok &= fired
        rc, out = build_and_run(work, [], "broken-centre", "broken centre")
        fired = rc != 0
        print(f"negative control [broken centre]: {'FIRED' if fired else 'DID NOT FIRE'} ({out.splitlines()[-1]})")
        ok &= fired

        # B. front-end compile
        with open(os.path.join(work, "urp_mock.hlsl"), "w") as f:
            f.write(URP_MOCK)
        shutil.copy(HLSL, os.path.join(work, "PrismSlice.hlsl"))
        # The corridor is included by the slice shader (the platform law); compile the shipped one.
        shutil.copy(os.path.join(os.path.dirname(HLSL), "PrismOcclusionCorridor.hlsl"),
                    os.path.join(work, "PrismOcclusionCorridor.hlsl"))
        include, passes = extract_passes(shader)
        for vert, frag, prog in passes:
            for defines in ([], ["DOTS_INSTANCING_ON"], ["_GBUFFER_NORMALS_OCT"]):
                for stage, entry in (("vert", vert), ("frag", frag)):
                    rc, out = glslang_compile(work, include, prog, stage, entry, defines)
                    label = f"{entry} [{stage}{' ' + ','.join(defines) if defines else ''}]"
                    if rc != 0:
                        print(f"COMPILE FAIL {label}\n{out}")
                        ok = False
                    else:
                        print(f"compiled {label}")
    finally:
        if keep:
            print("kept:", work)
        else:
            shutil.rmtree(work, ignore_errors=True)
    print("PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
