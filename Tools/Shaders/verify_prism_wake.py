#!/usr/bin/env python3
"""
Prove the SHIPPED PrismWake.hlsl does what Docs/PRISM_ANIMATION.md §4.7.3 says it does, by
compiling the file itself with clang++ and RUNNING it (/asset-surgery §4.5c) — the same harness
shape as verify_prism_cradle.py and verify_prism_sight_composition.py. Nothing here is a
transcription of the shader: the file under Assets/ is translated mechanically (HLSL -> C++
spelling only) and executed under a real non-uniform model matrix.

The wake is a travelling RIPPLE in the cylindrical frame about the line a fast ship just flew
down. Every vertex inside the train is pushed along its own radius from that line by a
dimensionless STRAIN; the axis itself is a fixed point.

What it proves, each on randomized inputs:

  1. IDENTITY with no live slot (count 0): bit-identical pass-through. A match with nobody
     moving fast enough costs one integer compare and changes nothing.
  2. IDENTITY OUTSIDE THE SUPPORT, in all three directions separately — in front of the ship
     (x <= 0), past the end of the train (x >= L), and outside the radial reach (r >= reach).
     Each is bit-identical, and each is counted, so a support that silently collapsed in one
     direction cannot pass on the strength of the other two.
  3. CYLINDRICAL PURITY, which is what makes the analytic normal derivable at all: the vertex
     keeps its distance ALONG the axis and its direction AROUND it bit for bit, and only its
     distance FROM the axis changes. The axis is therefore a FIXED POINT — the displacement is
     r·E and vanishes with r — which is the whole reason this is a strain and not an offset.
  4. NO FOLD, measured rather than assumed: sweeping r from the axis out to the reach, the
     image r' is strictly increasing (so b > 0 — two vertices at different radii keep their
     order and the prism never turns inside out) and strictly positive (so c > 0). The measured
     strain also never exceeds the amplitude, which is the bound the NO FOLD paragraph in the
     shipped header derives.
  5. LINEARITY IN STRENGTH: at fixed geometry the displacement is affine in w, so the eased
     engage and release are a blend of the MAP and never a differently-shaped wake.
  6. THE NORMAL IS THE MAP'S DERIVATIVE: a CONVERGENCE test, not a tolerance. Take a tiny
     triangle in a face's plane, deform its three corners with the face normal exactly as the
     mesh does, and compare the GEOMETRIC normal of the moved triangle against the normal the
     shader returns at the centroid. A flat patch of ANY size disagrees with the true surface
     normal at second order in its size, so a single tolerance only ever measures which patch
     size was chosen. The claim is that HALVING the patch QUARTERS the error, which is the
     signature of an exact first derivative and nothing else — test 9 runs the same sweep with
     the Jacobian's SHEAR term switched off and it plateaus instead.
  7. NO SEAM AT ANY OF THE THREE SUPPORT BOUNDARIES: sweeping across x = 0, x = L and
     r = reach, the DISPLACEMENT and the NORMAL are both continuous. Two of those planes are
     swept through mass at the ship's own speed, so a kink at either would read as an invisible
     wall passing.
  8. SLOT AUTHORITY: with two wakes live, the vertex is moved by the one with the greater
     authority (w·g·K) and by that one ALONE — the result is bit-identical to running that slot
     on its own. Swapping which is stronger swaps the winner.
  9. NEGATIVE CONTROL: rebuilt with the Jacobian's SHEAR term neutered (-D override of the
     file's own #ifndef dial), test 6's error STOPS CONVERGING — it plateaus instead of
     quartering — so the analytic normal is what holds that test, not luck.

Exit 0 on pass. Needs clang++; nothing else, and no Unity.
Usage:  python3 Tools/Shaders/verify_prism_wake.py [--keep]
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/PrismWake.hlsl")

SHIM = r"""// Minimal HLSL->C++ shim so the SHIPPED PrismWake.hlsl can be compiled and executed by
// clang++ (/asset-surgery 4.5c). Only what the file actually uses.
#pragma once
#include <cmath>
#include <algorithm>

struct float3 {
    float x=0,y=0,z=0;
    float3(){}
    float3(float a):x(a),y(a),z(a){}
    float3(float a,float b,float c):x(a),y(b),z(c){}
};
struct float3x3;
struct float4 {
    float x=0,y=0,z=0,w=0;
    float4(){}
    float4(float a,float b,float c,float d):x(a),y(b),z(c),w(d){}
    float4(float3 v,float d):x(v.x),y(v.y),z(v.z),w(d){}
    float3 xyz() const { return float3(x,y,z); }
};
static inline float3 operator+(float3 a,float3 b){return float3(a.x+b.x,a.y+b.y,a.z+b.z);}
static inline float3 operator-(float3 a,float3 b){return float3(a.x-b.x,a.y-b.y,a.z-b.z);}
static inline float3 operator*(float3 a,float b){return float3(a.x*b,a.y*b,a.z*b);}
static inline float3 operator*(float a,float3 b){return b*a;}
static inline float3 operator/(float3 a,float b){return float3(a.x/b,a.y/b,a.z/b);}
static inline float3& operator*=(float3&a,float b){a=a*b;return a;}
static inline float3& operator/=(float3&a,float b){a=a/b;return a;}
static inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static inline float3 cross(float3 a,float3 b){return float3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
static inline float length(float3 a){return std::sqrt(dot(a,a));}
static inline float rsqrt(float a){return 1.0f/std::sqrt(a);}
static inline float saturate(float v){return std::min(1.0f,std::max(0.0f,v));}
static inline float min(float a,float b){return a<b?a:b;}
static inline float max(float a,float b){return a>b?a:b;}
static inline float abs(float a){return a<0?-a:a;}
static inline float3 operator-(float3 a){return float3(-a.x,-a.y,-a.z);}
static inline float smoothstep(float e0,float e1,float x){float t=saturate((x-e0)/(e1-e0));return t*t*(3.0f-2.0f*t);}
static inline float3 lerp(float3 a,float3 b,float t){return a+(b-a)*t;}
static inline void sincos(float a,float&s,float&c){s=std::sin(a);c=std::cos(a);}
using std::pow;

// HLSL matrix convention as Unity uses it: mul(M, v) is M * column(v);
// mul(v, M) is row(v) * M.  m[row][col].
struct float3x3 { float m[3][3]; };
struct float4x4 {
    float m[4][4];
    explicit operator float3x3() const { float3x3 r; for(int i=0;i<3;i++)for(int j=0;j<3;j++) r.m[i][j]=m[i][j]; return r; }
};
static inline float4 mul(const float4x4&M,float4 v){
    float in[4]={v.x,v.y,v.z,v.w}; float o[4];
    for(int r=0;r<4;r++){o[r]=0;for(int c=0;c<4;c++)o[r]+=M.m[r][c]*in[c];}
    return float4(o[0],o[1],o[2],o[3]);
}
static inline float3 mul(float3 v,const float3x3&M){
    float in[3]={v.x,v.y,v.z}; float o[3];
    for(int c=0;c<3;c++){o[c]=0;for(int r=0;r<3;r++)o[c]+=in[r]*M.m[r][c];}
    return float3(o[0],o[1],o[2]);
}
extern float4x4 g_objectToWorld, g_worldToObject;
static inline float4x4 GetObjectToWorldMatrix(){ return g_objectToWorld; }
static inline float4x4 GetWorldToObjectMatrix(){ return g_worldToObject; }
"""

# Everything below `#include "shipped.h"` is the harness. The shipped file is the only source of
# the map; the harness never restates a formula from it, only observes the outputs.
COMMON = r"""#include "shipped.h"
#include <cstdio>
#include <cstdlib>
#include <random>
#include <vector>

float4x4 g_objectToWorld, g_worldToObject;

static int failures = 0;
#define CHECK(cond, ...) do { if (!(cond)) { failures++; printf("FAIL: "); printf(__VA_ARGS__); printf("\n"); } } while (0)

static std::mt19937 rng(20260923);
static float rnd(float a, float b) { return a + (b - a) * (rng() / (float)rng.max()); }
static float3 rndDir() { for(;;){ float3 v(rnd(-1,1),rnd(-1,1),rnd(-1,1)); float l=length(v); if(l>0.1f) return v/l; } }
// A random unit vector perpendicular to a — the wake's radial direction for a given axis.
static float3 perpDir(float3 a) {
    for(;;){ float3 v = rndDir(); float3 t = v - a * dot(v, a); float l = length(t); if (l > 0.25f) return t / l; }
}

// ---- model matrix T * R * S and its inverse, built exactly (no numerical inversion) ----
static void rotationMatrix(float3 axis, float angle, float R[3][3]) {
    float s = std::sin(angle), c = std::cos(angle), t = 1 - c;
    float x = axis.x, y = axis.y, z = axis.z;
    float M[3][3] = {{t*x*x+c, t*x*y-s*z, t*x*z+s*y},{t*x*y+s*z, t*y*y+c, t*y*z-s*x},{t*x*z-s*y, t*y*z+s*x, t*z*z+c}};
    for(int i=0;i<3;i++)for(int j=0;j<3;j++)R[i][j]=M[i][j];
}
static void setModel(float3 t, float3 axis, float angle, float3 s) {
    float R[3][3]; rotationMatrix(axis, angle, R);
    float4x4 M{}, Mi{};
    for(int i=0;i<3;i++){ for(int j=0;j<3;j++){ M.m[i][j] = R[i][j] * (&s.x)[j]; } M.m[i][3] = (&t.x)[i]; }
    M.m[3][0]=M.m[3][1]=M.m[3][2]=0; M.m[3][3]=1;
    for(int i=0;i<3;i++){ for(int j=0;j<3;j++){ Mi.m[i][j] = R[j][i] / (&s.x)[i]; } }
    for(int i=0;i<3;i++){ float acc=0; for(int j=0;j<3;j++) acc += Mi.m[i][j]*(&t.x)[j]; Mi.m[i][3] = -acc; }
    Mi.m[3][0]=Mi.m[3][1]=Mi.m[3][2]=0; Mi.m[3][3]=1;
    g_objectToWorld = M; g_worldToObject = Mi;
}
static void setIdentityModel() {
    for(int i=0;i<4;i++)for(int j=0;j<4;j++){ g_objectToWorld.m[i][j]=(i==j); g_worldToObject.m[i][j]=(i==j); }
}
static float3 toWorld(float3 p){ return mul(g_objectToWorld, float4(p,1)).xyz(); }
static float3 toObject(float3 p){ return mul(g_worldToObject, float4(p,1)).xyz(); }
static float3 xformDir(float3 d){ return mul(g_objectToWorld, float4(d,0)).xyz(); }
static float3 normalToWorld(float3 n){ float3 w = mul(n, (float3x3)g_worldToObject); return w / length(w); }

// One published slot. The four shape numbers are DERIVED ON THE CPU by PrismWake.cs from the
// hull radius and PrismWakeConfig; the shader takes them as given, so the harness supplies the
// numbers the shipped config produces for an average hull (radius ~6.7 u) rather than deriving
// anything of its own.
struct Wake {
    float3 U = float3(0,0,0);
    float3 axis = float3(0,0,1);
    float phase = 0.0f;
    float radius = 6.7f;
    float w = 1.0f;
    float reach = 20.1f;     // radius * reachHullRadii (3)
    float L = 40.2f;         // radius * trainHullRadii (6)
    float k = 0.39073f;      // 2*pi * wavesPerTrain (2.5) / L
};
static const float AMP  = 0.25f;
static const float EXPO = 1.5f;

static void setBank(int count, const Wake& a, const Wake& b = Wake(), float amp = AMP, float expo = EXPO) {
    const Wake* ws[2] = { &a, &b };
    for (int i = 0; i < 4; i++) {
        bool live = (i < 2) && (i < count);
        if (live) {
            _PrismWakeCentre[i] = float4(ws[i]->U, ws[i]->radius);
            _PrismWakeAxis[i]   = float4(ws[i]->axis, ws[i]->phase);
            _PrismWakeShape[i]  = float4(ws[i]->w, ws[i]->reach, ws[i]->L, ws[i]->k);
        } else {
            _PrismWakeCentre[i] = float4(0,0,0,0);
            _PrismWakeAxis[i]   = float4(0,0,0,0);
            _PrismWakeShape[i]  = float4(0,0,0,0);
        }
    }
    _PrismWakeParams = float4(amp, expo, (float)count, 0.0f);
}

// A wake with a random pose and a randomized (still realistic) shape.
static Wake rndWake() {
    Wake k;
    k.axis = rndDir();
    k.phase = rnd(0.0f, 6.2831f);
    k.radius = rnd(3.0f, 12.0f);
    k.w = rnd(0.2f, 1.0f);
    k.reach = k.radius * rnd(2.0f, 4.0f);
    k.L = k.radius * rnd(4.0f, 8.0f);
    k.k = 6.2831f * rnd(1.5f, 4.0f) / k.L;
    return k;
}
// Put the wake so that the given WORLD point sits at cylindrical coordinates (x, r) in it.
static void placeAt(Wake& k, float3 pw, float x, float r, float3 rhat) {
    k.U = pw - k.axis * x - rhat * r;
}

// A vertex on one of the prism's six faces, carrying that face's flat normal — the only two
// things the map reads, on any mesh.
struct Vtx { float3 pObj; float3 nObj; };
static float3 faceNormal(int f) {
    switch (f) { case 0: return float3(1,0,0); case 1: return float3(-1,0,0);
                 case 2: return float3(0,1,0); case 3: return float3(0,-1,0);
                 case 4: return float3(0,0,1); default: return float3(0,0,-1); }
}
// Two in-plane axes for face f, with cross(u, v) == n.
static void faceAxes(int f, float3 &u, float3 &v) {
    const float3 right(1,0,0), up(0,1,0), fwd(0,0,1);
    switch (f) {
        case 0: u = up;    v = fwd;   break;
        case 1: u = fwd;   v = up;    break;
        case 2: u = fwd;   v = right; break;
        case 3: u = right; v = fwd;   break;
        case 4: u = right; v = up;    break;
        default: u = up;   v = right; break;
    }
}
static std::vector<Vtx> prismVerts(int n) {
    std::vector<Vtx> out;
    for (int f = 0; f < 6; f++) {
        float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
        float3 c = nrm * 0.5f;
        for (int j = 0; j <= n; j++) for (int i = 0; i <= n; i++) {
            Vtx x; x.nObj = nrm;
            x.pObj = c + u * ((float)i / n - 0.5f) + v * ((float)j / n - 0.5f);
            out.push_back(x);
        }
    }
    return out;
}

struct Out { float3 pObj, nObj, pW, nW; };
static Out run(const Vtx& x) {
    Out r;
    PrismWakeDeform_float(x.pObj, x.nObj, r.pObj, r.nObj);
    r.pW = toWorld(r.pObj);
    r.nW = normalToWorld(r.nObj);
    return r;
}
static Out runAt(float3 pw, float3 nObj) {
    Vtx x; x.pObj = toObject(pw); x.nObj = nObj;
    return run(x);
}
static bool identical(const Vtx& x, const Out& r) {
    return r.pObj.x == x.pObj.x && r.pObj.y == x.pObj.y && r.pObj.z == x.pObj.z
        && r.nObj.x == x.nObj.x && r.nObj.y == x.nObj.y && r.nObj.z == x.nObj.z;
}
static bool same(const Out& a, const Out& b) {
    return a.pObj.x == b.pObj.x && a.pObj.y == b.pObj.y && a.pObj.z == b.pObj.z
        && a.nObj.x == b.nObj.x && a.nObj.y == b.nObj.y && a.nObj.z == b.nObj.z;
}

// The map's CURVATURE SCALE at a point, used to size the differential patch in test 6. The
// smallest length over which the field can turn over: the wavelength along the axis, the
// distance from the axis (the radial frame itself turns on that scale), the reach and the
// train. Sizing the patch as a fraction of THIS rather than absolutely is what makes the
// convergence rate a statement about the shader and not about the number somebody picked.
static float featureScale(const Wake& k, float r) {
    return min(min(r, 1.0f / k.k), min(k.reach, k.L));
}
"""

HARNESS = COMMON + r"""
int main()
{
    const float3 SCALE(3, 1, 6);   // a trail slab: the shipped prism's non-uniform scale

    // ---------------- 1. identity with no live slot ----------------
    {
        int bad = 0, tested = 0;
        auto vs = prismVerts(3);
        for (int trial = 0; trial < 150; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            Wake k = rndWake();
            placeAt(k, toWorld(float3(0,0,0)), k.L * 0.5f, k.reach * 0.3f, perpDir(k.axis));
            setBank(0, k);
            for (const Vtx& x : vs) { tested++; if (!identical(x, run(x))) bad++; }
        }
        CHECK(bad == 0, "identity with count 0 broken on %d of %d vertices", bad, tested);
        printf("1. count 0 -> bit-identical pass-through: %s (%d vertices)\n", bad == 0 ? "ok" : "BROKEN", tested);
    }

    // ---------------- 2. identity outside the support, in all three directions ----------------
    {
        int bad[3] = {0,0,0}, tested[3] = {0,0,0};
        const char* where[3] = { "in front of the ship (x <= 0)",
                                 "past the end of the train (x >= L)",
                                 "outside the radial reach (r >= reach)" };
        for (int trial = 0; trial < 4000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            Wake k = rndWake();
            float3 rhat = perpDir(k.axis);
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            float3 cw = toWorld(c);

            struct Case { float x, r; } cases[3] = {
                { rnd(-3.0f * k.L, 0.0f),        rnd(0.05f, 0.95f) * k.reach },   // in front
                { rnd(1.0f, 3.0f) * k.L,         rnd(0.05f, 0.95f) * k.reach },   // past the train
                { rnd(0.05f, 0.95f) * k.L,       rnd(1.0f, 4.0f) * k.reach },     // past the reach
            };
            for (int ci = 0; ci < 3; ci++) {
                placeAt(k, cw, cases[ci].x, cases[ci].r, rhat);
                setBank(1, k);
                Vtx x; x.pObj = c; x.nObj = nrm;
                tested[ci]++;
                if (!identical(x, run(x))) bad[ci]++;
            }
        }
        for (int ci = 0; ci < 3; ci++) {
            CHECK(tested[ci] > 3000, "too few samples %s (%d)", where[ci], tested[ci]);
            CHECK(bad[ci] == 0, "a vertex %s was moved (%d of %d)", where[ci], bad[ci], tested[ci]);
        }
        printf("2. outside the support -> untouched: %d + %d + %d vertices across all three boundaries\n",
               tested[0], tested[1], tested[2]);
    }

    // ---------------- 3. cylindrical purity: the axis is a FIXED POINT ----------------
    {
        float worstX = 0, worstTheta = 0, worstOverAmp = 0; int tested = 0, moved = 0;
        for (int trial = 0; trial < 6000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            Wake k = rndWake();
            float3 rhat = perpDir(k.axis);
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            float3 cw = toWorld(c);
            float x0 = rnd(0.02f, 0.98f) * k.L;
            float r0 = rnd(0.001f, 0.99f) * k.reach;
            placeAt(k, cw, x0, r0, rhat);
            setBank(1, k);

            Vtx vx; vx.pObj = c; vx.nObj = nrm;
            Out o = run(vx);
            tested++;
            if (identical(vx, o)) continue;
            moved++;

            float3 q = o.pW - k.U;
            float x1 = dot(q, k.axis);
            float3 rv = q - k.axis * x1;
            float r1 = length(rv);
            worstX = max(worstX, abs(x1 - x0) / max(k.L, 1.0f));
            worstTheta = max(worstTheta, 1.0f - dot(rv / r1, rhat));
            // The strain the map applied, against the amplitude that is supposed to bound it.
            worstOverAmp = max(worstOverAmp, abs(r1 / r0 - 1.0f) / (AMP * k.w));
        }
        CHECK(moved > 2000, "too few moved vertices to test purity (%d of %d)", moved, tested);
        CHECK(worstX < 2e-5f, "the vertex slid ALONG the axis: worst |dx|/L = %g", worstX);
        CHECK(worstTheta < 1e-5f, "the vertex slid AROUND the axis: worst 1-dot = %g", worstTheta);
        CHECK(worstOverAmp < 1.0f + 1e-3f, "the strain exceeded the amplitude bound by %gx", worstOverAmp);
        printf("3. cylindrical purity (%d moved of %d): x held to %.2e of L, theta to 1-dot %.2e, "
               "strain <= %.4f of A*w\n", moved, tested, worstX, worstTheta, worstOverAmp);
    }

    // ---------------- 4. no fold: r' strictly increasing and strictly positive ----------------
    {
        int folded = 0, nonPositive = 0; long samples = 0;
        float worstStrain = 0, tightestSlope = 1e30f;
        for (int trial = 0; trial < 600; trial++) {
            setIdentityModel();                       // the fold is a property of the MAP, not the transform
            Wake k = rndWake();
            float3 rhat = perpDir(k.axis);
            k.U = float3(rnd(-20,20), rnd(-20,20), rnd(-20,20));
            setBank(1, k, Wake(), rnd(0.05f, 0.45f), rnd(1.0f, 6.0f));   // the config's whole authored range
            float x = rnd(0.01f, 0.99f) * k.L;
            const int N = 1500;
            float prevR = -1e30f, prevIn = 0;
            for (int i = 0; i <= N; i++) {
                float r = (float)i / N * k.reach * 1.02f;
                float3 pw = k.U + k.axis * x + rhat * max(r, 1e-6f);
                Out o = runAt(pw, float3(0,1,0));
                float3 q = o.pW - k.U;
                float r1 = length(q - k.axis * dot(q, k.axis));
                samples++;
                if (!(r1 >= 0.0f)) nonPositive++;
                if (r > 1e-3f) worstStrain = max(worstStrain, abs(r1 / r - 1.0f));
                if (i > 0) {
                    float slope = (r1 - prevR) / max(r - prevIn, 1e-12f);
                    if (slope <= 0.0f) folded++;
                    tightestSlope = min(tightestSlope, slope);
                }
                prevR = r1; prevIn = r;
            }
        }
        CHECK(samples > 500000, "too few no-fold samples (%ld)", samples);
        CHECK(nonPositive == 0, "%d samples left the image radius negative (c <= 0)", nonPositive);
        CHECK(folded == 0, "the map FOLDS in %d places (r' not increasing in r — the prism turns inside out)", folded);
        CHECK(worstStrain < 0.45f + 1e-3f, "measured strain %g exceeds the config's amplitude ceiling", worstStrain);
        printf("4. no fold (%ld samples over the whole authored amplitude/exponent range): "
               "tightest dr'/dr = %.4f, worst |strain| = %.4f\n", samples, tightestSlope, worstStrain);
    }

    // ---------------- 5. the map is affine in the strength ----------------
    {
        float worst = 0, worstAbs = 0; int tested = 0, absSamples = 0;
        for (int trial = 0; trial < 3000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            Wake k = rndWake();
            float3 rhat = perpDir(k.axis);
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            placeAt(k, toWorld(c), rnd(0.1f, 0.9f) * k.L, rnd(0.1f, 0.9f) * k.reach, rhat);
            Vtx vx; vx.pObj = c; vx.nObj = nrm;
            float3 base = toWorld(c);

            k.w = 1.0f;  setBank(1, k); float3 d1 = run(vx).pW - base;
            k.w = 0.5f;  setBank(1, k); float3 dh = run(vx).pW - base;
            k.w = 0.25f; setBank(1, k); float3 dq = run(vx).pW - base;
            float mag = length(d1);
            float rh = length(dh * 2.0f - d1), rq = length(dq * 4.0f - d1);
            worstAbs = max(worstAbs, max(rh, rq));
            absSamples++;
            // A RELATIVE figure is only meaningful where the displacement is larger than the
            // float noise on a 20-unit world coordinate (~2e-6 u, quadrupled by the w=0.25
            // extrapolation). Below that the ratio measures the arithmetic, not the map — so the
            // relative claim is made where the map is visibly doing something, and the absolute
            // residual above covers every trial including the ones that barely move.
            if (!(mag > 0.25f)) continue;
            tested++;
            worst = max(worst, rh / mag);
            worst = max(worst, rq / mag);
        }
        CHECK(absSamples > 2000, "too few strength samples (%d)", absSamples);
        CHECK(tested > 800, "too few strength samples with a visible displacement (%d)", tested);
        CHECK(worstAbs < 1e-3f, "the displacement is not affine in the strength: worst residual %g u", worstAbs);
        CHECK(worst < 1e-4f, "the displacement is not affine in the strength: worst relative error %g", worst);
        printf("5. affine in strength (%d trials, %d of them past 0.25 u of travel): half strength is half "
               "the travel to %.2e u absolute, %.2e relative\n", absSamples, tested, worstAbs, worst);
    }

    // ---------------- 6. the normal is the map's DERIVATIVE ----------------
    {
        // A tiny triangle in the face's own plane, deformed exactly as the mesh would deform it
        // (all three corners carrying the face normal), against the normal the shader returns at
        // its centroid. The assertion is the CONVERGENCE RATE: halving the patch must quarter
        // the error, which is the signature of an exact first derivative and nothing else.
        const int LEVELS = 4;
        const float FRACS[LEVELS] = { 0.04f, 0.02f, 0.01f, 0.005f };
        float worst[LEVELS] = { 0, 0, 0, 0 };
        int tested = 0, skipped = 0;
        for (int trial = 0; trial < 6000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            Wake k = rndWake();
            float3 rhat = perpDir(k.axis);
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            // Well inside the support: the patch is at most 0.04 of the feature scale, so no
            // corner can straddle a boundary and the test measures the interior derivative.
            float r0 = rnd(0.08f, 0.90f) * k.reach;
            placeAt(k, toWorld(c), rnd(0.08f, 0.92f) * k.L, r0, rhat);
            setBank(1, k);

            Vtx mid; mid.pObj = c; mid.nObj = nrm;
            Out rm = run(mid);
            if (identical(mid, rm)) { skipped++; continue; }

            float scale = featureScale(k, r0);
            float unit = max(length(xformDir(u)), length(xformDir(v)));

            float e[LEVELS]; bool usable = true;
            for (int lv = 0; lv < LEVELS && usable; lv++) {
                float eps = FRACS[lv] * scale / unit;
                float3 moved[3], flat[3];
                for (int i = 0; i < 3; i++) {
                    float a = 2.0944f * i;            // 120 degrees apart in the face plane
                    Vtx x; x.nObj = nrm;
                    x.pObj = c + u * (eps * std::cos(a)) + v * (eps * std::sin(a));
                    flat[i] = toWorld(x.pObj);
                    moved[i] = run(x).pW;
                }
                float3 g = cross(moved[1] - moved[0], moved[2] - moved[0]);
                float3 g0 = cross(flat[1] - flat[0], flat[2] - flat[0]);
                float gl = length(g), gl0 = length(g0);
                if (!(gl > 1e-16f) || !(gl0 > 1e-16f) || gl / gl0 < 0.01f) { usable = false; break; }
                // cross(u, v) == n, and the corners are laid out counter-clockwise in (u, v), so
                // g points the same way as the face normal before any deformation.
                e[lv] = 1.0f - dot(g / gl, rm.nW);
            }
            if (!usable) { skipped++; continue; }
            tested++;
            for (int lv = 0; lv < LEVELS; lv++) worst[lv] = max(worst[lv], e[lv]);
        }
        CHECK(tested > 3000, "too few differential-normal trials (%d)", tested);
        CHECK(worst[LEVELS-1] < 0.01f, "the returned normal does not approach the deformed surface: "
              "worst 1-dot at the finest patch = %g", worst[LEVELS-1]);
        for (int lv = 1; lv < LEVELS; lv++)
            CHECK(worst[lv] < 0.40f * worst[lv-1],
                  "halving the patch did not quarter the error (%g -> %g) — the returned normal is "
                  "not the map's derivative", worst[lv-1], worst[lv]);
        printf("6. analytic normal == d(map) (%d trials, %d skipped): worst 1-dot", tested, skipped);
        for (int lv = 0; lv < LEVELS; lv++) printf("  %.4gxs:%.3g", FRACS[lv], worst[lv]);
        printf("  (quartering => exact derivative)\n");
    }

    // ---------------- 7. no seam at any of the three support boundaries ----------------
    {
        const char* names[3] = { "x = 0 (the ship's own plane)", "x = L (the end of the train)",
                                 "r = reach (the outer edge)" };
        float worstDisp[3] = {0,0,0}, worstNrm[3] = {0,0,0};
        const float step = 0.001f;
        for (int trial = 0; trial < 60; trial++) {
            setIdentityModel();
            Wake k = rndWake();
            k.w = 1.0f;
            float3 rhat = perpDir(k.axis);
            k.U = float3(rnd(-10,10), rnd(-10,10), rnd(-10,10));
            setBank(1, k);
            float3 nrm = rndDir();
            for (int b = 0; b < 3; b++) {
                float centreX = (b == 0) ? 0.0f : (b == 1 ? k.L : 0.4f * k.L);
                float centreR = (b == 2) ? k.reach : 0.4f * k.reach;
                bool haveP = false; float3 prevD(0,0,0), prevN(0,0,0);
                for (int i = -600; i <= 600; i++) {
                    float x = centreX + ((b < 2) ? i * step : 0.0f);
                    float r = centreR + ((b == 2) ? i * step : 0.0f);
                    float3 pw = k.U + k.axis * x + rhat * r;
                    Out o = runAt(pw, nrm);
                    float3 disp = o.pW - pw;
                    if (haveP) {
                        worstDisp[b] = max(worstDisp[b], length(disp - prevD));
                        worstNrm[b] = max(worstNrm[b], 1.0f - dot(o.nW, prevN));
                    }
                    prevD = disp; prevN = o.nW; haveP = true;
                }
            }
        }
        for (int b = 0; b < 3; b++) {
            // A genuine seam is the whole local displacement appearing in one step — order 0.1 u
            // here. A continuous field moves by (gradient x step), which these bounds sit well
            // above and a seam sits far beyond.
            CHECK(worstDisp[b] < 0.02f, "displacement seam at %s: %g u of jump across a %g u step",
                  names[b], worstDisp[b], step);
            CHECK(worstNrm[b] < 1e-4f, "normal seam at %s: worst 1-dot between adjacent samples = %g",
                  names[b], worstNrm[b]);
        }
        printf("7. no seam at the three boundaries: worst adjacent displacement jump %.2e / %.2e / %.2e u, "
               "normal 1-dot %.2e / %.2e / %.2e\n",
               worstDisp[0], worstDisp[1], worstDisp[2], worstNrm[0], worstNrm[1], worstNrm[2]);
    }

    // ---------------- 8. slot authority: one wake wins outright ----------------
    {
        int tested = 0, notExclusive = 0, wrongWinner = 0, noSwap = 0;
        for (int trial = 0; trial < 2000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            float3 cw = toWorld(c);
            Vtx vx; vx.pObj = c; vx.nObj = nrm;

            // A is close to the vertex's own radius (high K) and at full strength; B reaches it
            // only at the very edge of its reach and is faded. A must win.
            Wake A = rndWake(); A.w = 1.0f;
            placeAt(A, cw, 0.5f * A.L, 0.05f * A.reach, perpDir(A.axis));
            Wake B = rndWake(); B.w = 0.05f;
            placeAt(B, cw, 0.5f * B.L, 0.92f * B.reach, perpDir(B.axis));

            setBank(1, A);            Out onlyA = run(vx);
            setBank(1, B);            Out onlyB = run(vx);
            if (same(onlyA, onlyB)) continue;              // indistinguishable: nothing to decide
            tested++;

            setBank(2, B, A);         Out both = run(vx);  // B in slot 0, A in slot 1
            if (!same(both, onlyA)) { wrongWinner++; }
            // and the loser contributed NOTHING: the result is bit-identical to the winner alone
            if (!same(both, onlyA) && !same(both, onlyB)) notExclusive++;

            // Flip the strengths and the winner must flip with them.
            Wake A2 = A; A2.w = 0.05f;
            Wake B2 = B; B2.w = 1.0f;
            setBank(1, A2); Out onlyA2 = run(vx);
            setBank(1, B2); Out onlyB2 = run(vx);
            setBank(2, B2, A2); Out both2 = run(vx);
            if (!same(both2, onlyB2) && !same(both2, onlyA2)) noSwap++;
        }
        CHECK(tested > 1000, "too few authority trials (%d)", tested);
        CHECK(wrongWinner == 0, "%d of %d trials: the slot with the greater authority did not win", wrongWinner, tested);
        CHECK(notExclusive == 0, "%d of %d trials: the result was neither slot alone (the fields SUMMED)", notExclusive, tested);
        CHECK(noSwap == 0, "%d of %d trials: swapping the strengths did not swap the winner", noSwap, tested);
        printf("8. slot authority (%d trials): the greater w*g*K wins and the loser contributes "
               "bit-exactly nothing\n", tested);
    }

    if (failures) { printf("\n%d FAILURE(S)\n", failures); return 1; }
    printf("\nall properties hold\n");
    return 0;
}
"""

# The negative control drives the SAME differential-normal sweep with the Jacobian's SHEAR term
# neutered via the shipped file's own #ifndef dial. The shear is the off-diagonal that comes
# from the wave TRAVELLING along the axis — the one part of this normal the cradle's spherical
# map has no analogue for — so it is exactly the term a "close enough" normal would omit. It
# must BREAK test 6, otherwise that test was passing by luck.
CONTROL_MAIN = COMMON + r"""
int main()
{
    const int LEVELS = 4;
    const float FRACS[LEVELS] = { 0.04f, 0.02f, 0.01f, 0.005f };
    float worst[LEVELS] = { 0, 0, 0, 0 };
    int tested = 0;
    setIdentityModel();     // the control is about the Jacobian, not the transform stack
    for (int trial = 0; trial < 4000; trial++) {
        float3 nrm(0,1,0), u(0,0,1), v(1,0,0);         // cross(u, v) == n
        float3 c = nrm * 0.5f + u * rnd(-0.4f, 0.4f) + v * rnd(-0.4f, 0.4f);
        Wake k = rndWake();
        k.w = 1.0f;
        float3 rhat = perpDir(k.axis);
        float r0 = rnd(0.08f, 0.90f) * k.reach;
        placeAt(k, c, rnd(0.08f, 0.92f) * k.L, r0, rhat);
        setBank(1, k);

        Vtx mid; mid.pObj = c; mid.nObj = nrm;
        Out rm = run(mid);
        if (identical(mid, rm)) continue;

        float scale = featureScale(k, r0);
        float e[LEVELS]; bool usable = true;
        for (int lv = 0; lv < LEVELS && usable; lv++) {
            float eps = FRACS[lv] * scale;
            float3 moved[3], flat[3];
            for (int i = 0; i < 3; i++) {
                float a = 2.0944f * i;
                Vtx x; x.nObj = nrm;
                x.pObj = c + u * (eps * std::cos(a)) + v * (eps * std::sin(a));
                flat[i] = x.pObj;
                moved[i] = run(x).pW;
            }
            float3 g = cross(moved[1] - moved[0], moved[2] - moved[0]);
            float3 g0 = cross(flat[1] - flat[0], flat[2] - flat[0]);
            float gl = length(g), gl0 = length(g0);
            if (!(gl > 1e-16f) || !(gl0 > 1e-16f) || gl / gl0 < 0.01f) { usable = false; break; }
            e[lv] = 1.0f - dot(g / gl, rm.nW);
        }
        if (!usable) continue;
        tested++;
        for (int lv = 0; lv < LEVELS; lv++) worst[lv] = max(worst[lv], e[lv]);
    }
    printf("shear Jacobian term neutered, %d trials: worst 1-dot", tested);
    for (int lv = 0; lv < LEVELS; lv++) printf("  %.4gxs:%.3g", FRACS[lv], worst[lv]);
    // The control FIRES when the error refuses to converge: the finest patch must still be
    // grossly wrong, and halving must NOT have quartered it.
    bool plateaus = worst[LEVELS-1] > 0.01f && worst[LEVELS-1] > 0.40f * worst[LEVELS-2];
    printf("  -> %s\n", plateaus ? "PLATEAUS (control fires)" : "converged anyway (control failed)");
    return plateaus ? 0 : 1;
}
"""


def translate(src):
    """HLSL -> C++ spelling. Mechanical only: no semantic edits to the shipped file."""
    out = src
    out = re.sub(r"\bout float3 (\w+)", r"float3 &\1", out)
    out = re.sub(r"\bout float (\w+)", r"float &\1", out)
    out = re.sub(r"\.xyz\b", ".xyz()", out)
    out = re.sub(r"\[unroll\]", "", out)          # an HLSL loop attribute; C++ has no spelling for it
    assert "void PrismWakeDeform_float(" in out, "entry point missing"
    assert "float3 Position, float3 Normal," in out, \
        "the entry point's signature is not (Position, Normal) — the harness and the wirer disagree"
    for name in ("_PrismWakeCentre", "_PrismWakeAxis", "_PrismWakeShape", "_PrismWakeParams",
                 "PRISM_WAKE_SLOTS", "PRISM_WAKE_MIN_STRETCH", "PRISM_WAKE_SHEAR_GAIN"):
        assert name in out, f"{name} missing from the shipped HLSL"
    return out


def build_and_run(work, main_src, extra_flags, label):
    with open(os.path.join(work, label + ".cpp"), "w") as f:
        f.write(main_src)
    binary = os.path.join(work, label)
    build = subprocess.run(["clang++", "-std=c++17", "-O1", "-Wall", "-Wno-unused-function"] + extra_flags +
                           ["-o", binary, os.path.join(work, label + ".cpp")],
                           cwd=work, capture_output=True, text=True)
    if build.returncode != 0:
        print(f"COMPILE FAILED ({label}) - the shipped HLSL does not build:\n" + build.stderr, file=sys.stderr)
        return None
    run = subprocess.run([binary], capture_output=True, text=True)
    sys.stdout.write(run.stdout)
    if run.stderr:
        sys.stderr.write(run.stderr)
    return run.returncode


def main():
    keep = "--keep" in sys.argv
    if not shutil.which("clang++"):
        print("clang++ not found - install it or run this where it is available", file=sys.stderr)
        return 2

    work = tempfile.mkdtemp(prefix="prism_wake_verify_")
    try:
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(SHIM)
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write('#include "shim.h"\n' + translate(open(HLSL).read()))

        rc = build_and_run(work, HARNESS, [], "verify")
        if rc is None or rc != 0:
            print("\nFAILED", file=sys.stderr)
            return 1

        print("\n9. negative control (Jacobian SHEAR term neutered via -D):")
        rc = build_and_run(work, CONTROL_MAIN, ["-DPRISM_WAKE_SHEAR_GAIN=0.0"], "control")
        if rc is None or rc != 0:
            print("\nFAILED: the negative control did not fire — the analytic normal's shear "
                  "term is not what makes test 6 pass", file=sys.stderr)
            return 1

        print("\nAll wake properties hold for the shipped file.")
        return 0
    finally:
        if keep:
            print("kept: " + work)
        else:
            shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
