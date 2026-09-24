#!/usr/bin/env python3
"""
Prove the SHIPPED PrismWake.hlsl does what Docs/PRISM_ANIMATION.md §4.7.3 says it does, by
compiling the file itself with clang++ and RUNNING it (/asset-surgery §4.5c) — the same harness
shape as verify_prism_cradle.py and verify_prism_sight_composition.py. Nothing here is a
transcription of the shader: the file under Assets/ is translated mechanically (HLSL -> C++
spelling only) and executed under a real non-uniform model matrix.

The wake is a SHOCKWAVE FRONT — a thin spherical shell about a warhead in flight, travelling out
to that warhead's own blast radius, over and over, all the way in. Every vertex inside the shell
is pushed along its own radius from the round by a WAVELET `(1-s^2)^2 * sin(2*pi*Q*s)`, where
`s = (r - c)/sigma` says where the vertex sits across the shell. Outside the shell nothing moves
at all, and the round's centre is a fixed point.

What it proves, each on randomized inputs:

  1. IDENTITY with no live slot (count 0): bit-identical pass-through. A match with no warhead in
     the air costs one integer compare and changes nothing.
  2. IDENTITY OUTSIDE THE SHELL, in both directions separately — inside the inner face
     (r <= c - sigma) and beyond the outer one (r >= c + sigma). Each is bit-identical and each is
     counted, so a support that silently collapsed on one side cannot pass on the other's
     strength. The inner half is what makes the round's centre a fixed point.
  3. RADIAL PURITY, which is what makes the analytic normal derivable at all: the vertex keeps its
     DIRECTION from the round bit for bit and only its DISTANCE changes. And that distance changes
     by at most `sigma * A * w` — the bound the shipped header claims, and the whole reason the
     amplitude is absolute rather than the old cylinder's strain (a strain would have moved the
     outermost mass of a 95-unit reach by 40 units).
  4. BANDWIDTH: the pulse carries EXACTLY Q cycles across the shell and nothing else — measured as
     4Q-1 sign changes of the displacement along a radial sweep, at the exact radii `s = j/(2Q)`.
     This is the property the design is FOR: one wavelet arriving and passing, not a standing
     corrugation filling the whole support the way the cylinder's train did.
  5. NO FOLD, measured rather than assumed, over the config's whole authored (amplitude, Q) range:
     sweeping r out through the shell, the image radius f(r) is strictly increasing (so the radial
     stretch a > 0 — two vertices at different radii keep their order and the prism never turns
     inside out) and strictly positive (so the tangential stretch b > 0). The shipped header
     derives both from the single bound A < 1/(2*pi*Q); this measures them.
  6. LINEARITY IN STRENGTH: at fixed geometry the displacement is affine in w, so the eased engage
     and release are a blend of the MAP and never a differently-shaped front.
  7. THE NORMAL IS THE MAP'S DERIVATIVE: a CONVERGENCE test, not a tolerance. Take a tiny triangle
     in a face's plane, deform its three corners with the face normal exactly as the mesh does, and
     compare the GEOMETRIC normal of the moved triangle against the normal the shader returns at
     the centroid. A flat patch of ANY size disagrees with the true surface normal at second order
     in its size, so a single tolerance only ever measures which patch size was chosen. The claim
     is that HALVING the patch QUARTERS the error, which is the signature of an exact first
     derivative and nothing else — test 10 runs the same sweep with the Jacobian's radial stretch
     term switched off and it plateaus instead.
  8. NO SEAM AT EITHER FACE OF THE SHELL: sweeping across r = c - sigma and r = c + sigma, the
     DISPLACEMENT and the NORMAL are both continuous. Both faces are swept through mass at the
     front's own speed, so a kink at either would read as an invisible wall passing.
  9. SLOT AUTHORITY: with two fronts live, the vertex is moved by the one with the greater
     authority (w * window) and by that one ALONE — the result is bit-identical to running that
     slot on its own. Swapping which is stronger swaps the winner. Summing two spherical fields
     about two different centres is not a spherical field about anything, so its normal could not
     be derived analytically at all.
 10. NEGATIVE CONTROL: rebuilt with the Jacobian's RADIAL STRETCH term neutered (-D override of
     the file's own #ifndef dial), test 7's error STOPS CONVERGING — it plateaus instead of
     quartering — so the analytic normal is what holds that test, not luck. That term carries the
     entire derivative of the wavelet, so it is exactly what a "close enough" normal would omit.

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
static inline float lerp(float a,float b,float t){return a+(b-a)*t;}
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

static std::mt19937 rng(20260924);
static float rnd(float a, float b) { return a + (b - a) * (rng() / (float)rng.max()); }
static float3 rndDir() { for(;;){ float3 v(rnd(-1,1),rnd(-1,1),rnd(-1,1)); float l=length(v); if(l>0.1f) return v/l; } }

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

// One published shockwave. Every number here is DERIVED ON THE CPU by PrismWake.cs from the
// warhead's own blast radius and PrismWakeConfig (the front radius is INTEGRATED there, so a pulse
// keeps travelling at one speed while the round's hit radius grows under it); the shader takes them
// as given. The defaults are what the shipped config produces for a skyburst at resting Mass —
// reach 95.2 u, sigma 0.25 of that — so the harness is exercising the numbers the game publishes.
struct Wake {
    float3 U = float3(0,0,0);
    float front = 45.0f;     // c: where the shell is right now, world units
    float sigma = 23.8f;     // the front's half-thickness
    float reach = 95.2f;     // the warhead's blast radius (carried for tooling; the map ignores it)
    float w = 1.0f;          // eased strength
};
static const float AMP = 0.13f;      // PrismWakeConfig.amplitude
static const float CYCLES = 1.0f;    // PrismWakeConfig.wavesInFront — ONE wavelet

static void setBank(int count, const Wake& a, const Wake& b = Wake(), float amp = AMP, float cycles = CYCLES) {
    const Wake* ws[2] = { &a, &b };
    for (int i = 0; i < 4; i++) {
        bool live = (i < 2) && (i < count);
        if (live) {
            _PrismWakeCentre[i] = float4(ws[i]->U, ws[i]->front);
            _PrismWakeShape[i]  = float4(ws[i]->w, ws[i]->sigma, ws[i]->reach, 0.0f);
        } else {
            _PrismWakeCentre[i] = float4(0,0,0,0);
            _PrismWakeShape[i]  = float4(0,0,0,0);
        }
    }
    _PrismWakeParams = float4(amp, cycles, (float)count, 0.0f);
}

// The amplitude ceiling PrismWakeConfigSO derives from Q: A < 1/(2*pi*Q) is exactly the no-fold
// condition, and the asset clamps to 90% of it. The harness never authors an amplitude the config
// could not have produced, so a pass is a statement about the shipped range.
static float foldingAmplitude(float cycles) { return 1.0f / (6.28318530718f * max(cycles, 1.0f)); }

// A shockwave with a randomized but realistic shape. The front is born at sigma and dies at the
// reach (PrismWakeConfigSO.FrontRadiusAt), which is the publisher's guarantee that the shell never
// straddles the round's centre — the thing that makes b > 0 follow from a > 0.
static Wake rndWake() {
    Wake k;
    k.reach = rnd(40.0f, 140.0f);
    k.sigma = k.reach * rnd(0.05f, 0.45f);
    k.front = lerp(k.sigma, k.reach, rnd(0.02f, 1.0f));
    k.w = rnd(0.2f, 1.0f);
    return k;
}
// Put the round so the given WORLD point sits at radius r from it, in direction rhat.
static void placeAt(Wake& k, float3 pw, float r, float3 rhat) { k.U = pw - rhat * r; }
// The radius at which a vertex sits at shell coordinate s.
static float radiusAtS(const Wake& k, float s) { return k.front + s * k.sigma; }

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

// The map's CURVATURE SCALE at a point, used to size the differential patch in test 7. The
// smallest length over which the field can turn over: the wavelet's own length across the shell
// (sigma / Q) and the distance from the round (the radial frame itself turns on that scale).
// Sizing the patch as a fraction of THIS rather than absolutely is what makes the convergence rate
// a statement about the shader and not about the number somebody picked.
static float featureScale(const Wake& k, float r, float cycles) {
    return min(r, k.sigma / max(cycles, 1.0f));
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
            placeAt(k, toWorld(float3(0,0,0)), k.front, rndDir());
            setBank(0, k);
            for (const Vtx& x : vs) { tested++; if (!identical(x, run(x))) bad++; }
        }
        CHECK(bad == 0, "identity with count 0 broken on %d of %d vertices", bad, tested);
        printf("1. count 0 -> bit-identical pass-through: %s (%d vertices)\n", bad == 0 ? "ok" : "BROKEN", tested);
    }

    // ---------------- 2. identity outside the shell, on BOTH faces ----------------
    {
        int bad[2] = {0,0}, tested[2] = {0,0}, skipped = 0;
        const char* where[2] = { "inside the inner face (r <= c - sigma)",
                                 "beyond the outer face (r >= c + sigma)" };
        for (int trial = 0; trial < 6000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            Wake k = rndWake();
            float3 rhat = rndDir();
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            float3 cw = toWorld(c);
            float inner = k.front - k.sigma;
            if (!(inner > 0.5f)) { skipped++; continue; }   // a front only just born: no interior to sample

            float radii[2] = { rnd(0.02f, 1.0f) * inner,                          // inside
                               (k.front + k.sigma) * rnd(1.0f, 3.0f) };           // outside
            for (int ci = 0; ci < 2; ci++) {
                placeAt(k, cw, radii[ci], rhat);
                setBank(1, k);
                Vtx x; x.pObj = c; x.nObj = nrm;
                tested[ci]++;
                if (!identical(x, run(x))) bad[ci]++;
            }
        }
        for (int ci = 0; ci < 2; ci++) {
            CHECK(tested[ci] > 3000, "too few samples %s (%d)", where[ci], tested[ci]);
            CHECK(bad[ci] == 0, "a vertex %s was moved (%d of %d)", where[ci], bad[ci], tested[ci]);
        }
        printf("2. outside the shell -> untouched: %d inside + %d outside vertices (%d fronts skipped as "
               "newborn)\n", tested[0], tested[1], skipped);
    }

    // ---------------- 3. radial purity, and the displacement bounded by sigma*A*w ----------------
    {
        float worstTheta = 0, worstOverBound = 0; int tested = 0, moved = 0;
        for (int trial = 0; trial < 8000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            Wake k = rndWake();
            float3 rhat = rndDir();
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            float r0 = radiusAtS(k, rnd(-0.98f, 0.98f));
            if (!(r0 > 1e-3f)) continue;
            placeAt(k, toWorld(c), r0, rhat);
            setBank(1, k);

            Vtx vx; vx.pObj = c; vx.nObj = nrm;
            Out o = run(vx);
            tested++;
            if (identical(vx, o)) continue;
            moved++;

            float3 q = o.pW - k.U;
            float r1 = length(q);
            worstTheta = max(worstTheta, 1.0f - dot(q / r1, rhat));
            worstOverBound = max(worstOverBound, abs(r1 - r0) / (k.sigma * AMP * k.w));
        }
        CHECK(moved > 3000, "too few moved vertices to test purity (%d of %d)", moved, tested);
        CHECK(worstTheta < 1e-5f, "the vertex slid SIDEWAYS off its radius: worst 1-dot = %g", worstTheta);
        CHECK(worstOverBound < 1.0f + 1e-3f,
              "the displacement exceeded the sigma*A*w bound by %gx — the amplitude is not absolute",
              worstOverBound);
        printf("3. radial purity (%d moved of %d): direction held to 1-dot %.2e, displacement <= %.4f of "
               "sigma*A*w\n", moved, tested, worstTheta, worstOverBound);
    }

    // ---------------- 4. bandwidth: exactly Q cycles across the shell, and nowhere else ----------------
    {
        // The design IS the bandwidth: one wavelet arriving and passing, not a train filling the
        // support. A wavelet of Q cycles changes sign exactly 4Q-1 times strictly inside the shell,
        // at the radii s = j/(2Q) and nowhere else.
        //
        // The harness recovers the displacement by subtracting a large radius from a large radius,
        // so within a few ULPs of that radius the answer is float arithmetic rather than map — and
        // near a zero of the wavelet that is exactly where the signal is. Samples inside that GATE
        // therefore carry no sign information and are skipped; the gate is a statement about float
        // precision (8 ULPs at the sample radius) and not about the shader. The round sits at the
        // origin for the same reason: an offset centre only adds cancellation.
        int wrongCount = 0, wrongPlace = 0, trials = 0;
        float worstZeroErrAll = 0;
        for (int Q = 1; Q <= 3; Q++) {
            for (int trial = 0; trial < 200; trial++) {
                setIdentityModel();                 // the bandwidth is a property of the MAP
                Wake k = rndWake();
                k.w = 1.0f;
                k.front = lerp(k.sigma, k.reach, rnd(0.3f, 1.0f));   // room for a fine sweep either side
                k.U = float3(0, 0, 0);
                // The upper half of the authored amplitude range at this Q, so every lobe of the
                // wavelet — including the shallow outermost ones — clears the precision gate by a
                // wide margin. Test 5 covers the range all the way down.
                float amp = foldingAmplitude((float)Q) * rnd(0.5f, 0.9f);
                setBank(1, k, Wake(), amp, (float)Q);
                float3 rhat = rndDir();
                trials++;

                float gate = 8.0f * 1.1921e-7f * (k.front + k.sigma) + 1e-7f;
                const int N = 40000;
                int signChanges = 0, lastSign = 0;
                float lastS = 0, worstZeroErr = 0;
                for (int i = 1; i < N; i++) {
                    float s = -1.0f + 2.0f * i / N;
                    float r = radiusAtS(k, s);
                    if (!(r > 1e-3f)) continue;
                    float3 pw = rhat * r;
                    Out o = runAt(pw, rhat);        // normal along the radius: irrelevant to position
                    float d = length(o.pW) - r;
                    if (abs(d) <= gate) continue;   // inside the precision gate: no sign information
                    int sg = d > 0.0f ? 1 : -1;
                    if (lastSign != 0 && sg != lastSign) {
                        signChanges++;
                        float mid = 0.5f * (s + lastS);         // the crossing is bracketed here
                        float j = mid * 2.0f * Q;               // predicted zeros sit at integer j
                        worstZeroErr = max(worstZeroErr, abs(j - std::round(j)));
                    }
                    lastSign = sg; lastS = s;
                }
                if (signChanges != 4 * Q - 1) wrongCount++;
                if (worstZeroErr > 0.01f) wrongPlace++;
                worstZeroErrAll = max(worstZeroErrAll, worstZeroErr);
            }
        }
        CHECK(trials > 500, "too few bandwidth trials (%d)", trials);
        CHECK(wrongCount == 0, "%d of %d sweeps did not carry exactly Q cycles across the shell",
              wrongCount, trials);
        CHECK(wrongPlace == 0, "%d of %d sweeps put a zero crossing somewhere other than s = j/(2Q) "
              "(worst |j - round(j)| = %g)", wrongPlace, trials, worstZeroErrAll);
        printf("4. bandwidth (%d sweeps, Q = 1..3): %d sweeps with a count other than 4Q-1, %d with a "
               "crossing off s = j/(2Q) (worst %.2e) — one wavelet, not a train\n",
               trials, wrongCount, wrongPlace, worstZeroErrAll);
    }

    // ---------------- 5. no fold: f(r) strictly increasing and strictly positive ----------------
    {
        int folded = 0, nonPositive = 0; long samples = 0;
        float worstOverBound = 0, tightestSlope = 1e30f;
        for (int Q = 1; Q <= 6; Q++) {
            for (int trial = 0; trial < 120; trial++) {
                setIdentityModel();                 // the fold is a property of the MAP, not the transform
                Wake k = rndWake();
                k.U = float3(rnd(-20,20), rnd(-20,20), rnd(-20,20));
                // the config's whole authored amplitude range at this Q, up to its own 90% clamp
                float amp = foldingAmplitude((float)Q) * rnd(0.05f, 0.9f);
                setBank(1, k, Wake(), amp, (float)Q);
                float3 rhat = rndDir();
                const int N = 3000;
                float prevOut = -1e30f, prevIn = 0;
                for (int i = 0; i <= N; i++) {
                    float r = max((float)i / N * (k.front + k.sigma) * 1.05f, 1e-4f);
                    float3 pw = k.U + rhat * r;
                    Out o = runAt(pw, rhat);
                    float r1 = length(o.pW - k.U);
                    samples++;
                    if (!(r1 > 0.0f)) nonPositive++;
                    worstOverBound = max(worstOverBound, abs(r1 - r) / (k.sigma * amp * k.w));
                    if (i > 0) {
                        float slope = (r1 - prevOut) / max(r - prevIn, 1e-12f);
                        if (slope <= 0.0f) folded++;
                        tightestSlope = min(tightestSlope, slope);
                    }
                    prevOut = r1; prevIn = r;
                }
            }
        }
        CHECK(samples > 2000000, "too few no-fold samples (%ld)", samples);
        CHECK(nonPositive == 0, "%d samples left the image radius non-positive (b <= 0)", nonPositive);
        CHECK(folded == 0, "the map FOLDS in %d places (f not increasing in r — the prism turns inside out)", folded);
        CHECK(worstOverBound < 1.0f + 1e-3f, "displacement exceeded sigma*A*w by %gx", worstOverBound);
        printf("5. no fold (%ld samples over the config's whole authored amplitude range at Q = 1..6): "
               "tightest df/dr = %.4f, displacement <= %.4f of sigma*A*w\n",
               samples, tightestSlope, worstOverBound);
    }

    // ---------------- 6. the map is affine in the strength ----------------
    {
        float worst = 0, worstAbs = 0; int tested = 0, absSamples = 0;
        for (int trial = 0; trial < 4000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            Wake k = rndWake();
            float3 rhat = rndDir();
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            // near a crest of the wavelet, where the displacement is largest and the test has teeth
            float s = (rng() % 2 ? 0.25f : -0.25f) + rnd(-0.08f, 0.08f);
            float r0 = radiusAtS(k, s);
            if (!(r0 > 1e-3f)) continue;
            placeAt(k, toWorld(c), r0, rhat);
            Vtx vx; vx.pObj = c; vx.nObj = nrm;
            float3 base = toWorld(c);

            k.w = 1.0f;  setBank(1, k); float3 d1 = run(vx).pW - base;
            k.w = 0.5f;  setBank(1, k); float3 dh = run(vx).pW - base;
            k.w = 0.25f; setBank(1, k); float3 dq = run(vx).pW - base;
            float mag = length(d1);
            float rh = length(dh * 2.0f - d1), rq = length(dq * 4.0f - d1);
            worstAbs = max(worstAbs, max(rh, rq));
            absSamples++;
            // A RELATIVE figure is only meaningful where the displacement is larger than the float
            // noise on a 20-unit world coordinate (~2e-6 u, quadrupled by the w=0.25 extrapolation).
            // Below that the ratio measures the arithmetic, not the map — so the relative claim is
            // made where the map is visibly doing something, and the absolute residual above covers
            // every trial including the ones that barely move.
            if (!(mag > 0.25f)) continue;
            tested++;
            worst = max(worst, rh / mag);
            worst = max(worst, rq / mag);
        }
        CHECK(absSamples > 3000, "too few strength samples (%d)", absSamples);
        CHECK(tested > 1500, "too few strength samples with a visible displacement (%d)", tested);
        CHECK(worstAbs < 1e-3f, "the displacement is not affine in the strength: worst residual %g u", worstAbs);
        CHECK(worst < 1e-4f, "the displacement is not affine in the strength: worst relative error %g", worst);
        printf("6. affine in strength (%d trials, %d of them past 0.25 u of travel): half strength is half "
               "the travel to %.2e u absolute, %.2e relative\n", absSamples, tested, worstAbs, worst);
    }

    // ---------------- 7. the normal is the map's DERIVATIVE ----------------
    {
        // A tiny triangle in the face's own plane, deformed exactly as the mesh would deform it
        // (all three corners carrying the face normal), against the normal the shader returns at
        // its centroid. The assertion is the CONVERGENCE RATE: halving the patch must quarter the
        // error, which is the signature of an exact first derivative and nothing else.
        const int LEVELS = 4;
        const float FRACS[LEVELS] = { 0.04f, 0.02f, 0.01f, 0.005f };
        float worst[LEVELS] = { 0, 0, 0, 0 };
        int tested = 0, skipped = 0;
        for (int trial = 0; trial < 8000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            Wake k = rndWake();
            float3 rhat = rndDir();
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            // Well inside the shell: the patch is at most 0.04 of the feature scale, so no corner
            // can straddle a face and the test measures the interior derivative.
            float r0 = radiusAtS(k, rnd(-0.90f, 0.90f));
            if (!(r0 > 1.0f)) { skipped++; continue; }
            placeAt(k, toWorld(c), r0, rhat);
            setBank(1, k);

            Vtx mid; mid.pObj = c; mid.nObj = nrm;
            Out rm = run(mid);
            if (identical(mid, rm)) { skipped++; continue; }

            float scale = featureScale(k, r0, CYCLES);
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
        printf("7. analytic normal == d(map) (%d trials, %d skipped): worst 1-dot", tested, skipped);
        for (int lv = 0; lv < LEVELS; lv++) printf("  %.4gxs:%.3g", FRACS[lv], worst[lv]);
        printf("  (quartering => exact derivative)\n");
    }

    // ---------------- 8. no seam at either face of the shell ----------------
    {
        const char* names[2] = { "r = c - sigma (the inner face)", "r = c + sigma (the outer face)" };
        float worstDisp[2] = {0,0}, worstNrm[2] = {0,0};
        const float step = 0.001f;
        for (int trial = 0; trial < 80; trial++) {
            setIdentityModel();
            Wake k = rndWake();
            k.w = 1.0f;
            k.front = lerp(k.sigma, k.reach, rnd(0.5f, 1.0f));   // both faces well clear of the centre
            k.U = float3(rnd(-10,10), rnd(-10,10), rnd(-10,10));
            setBank(1, k);
            float3 rhat = rndDir();
            float3 nrm = rndDir();
            for (int b = 0; b < 2; b++) {
                float centreR = (b == 0) ? (k.front - k.sigma) : (k.front + k.sigma);
                bool haveP = false; float3 prevD(0,0,0), prevN(0,0,0);
                for (int i = -600; i <= 600; i++) {
                    float r = centreR + i * step;
                    if (!(r > 1e-3f)) continue;
                    float3 pw = k.U + rhat * r;
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
        for (int b = 0; b < 2; b++) {
            // A genuine seam is the whole local displacement appearing in one step — order 1 u
            // here. A continuous field moves by (gradient x step), which these bounds sit well
            // above and a seam sits far beyond.
            CHECK(worstDisp[b] < 0.02f, "displacement seam at %s: %g u of jump across a %g u step",
                  names[b], worstDisp[b], step);
            CHECK(worstNrm[b] < 1e-4f, "normal seam at %s: worst 1-dot between adjacent samples = %g",
                  names[b], worstNrm[b]);
        }
        printf("8. no seam at either face: worst adjacent displacement jump %.2e / %.2e u, normal 1-dot "
               "%.2e / %.2e\n", worstDisp[0], worstDisp[1], worstNrm[0], worstNrm[1]);
    }

    // ---------------- 9. slot authority: one front wins outright ----------------
    {
        int tested = 0, notExclusive = 0, wrongWinner = 0, noSwap = 0;
        for (int trial = 0; trial < 3000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            float3 cw = toWorld(c);
            Vtx vx; vx.pObj = c; vx.nObj = nrm;

            // A holds the vertex at the MIDDLE of its shell (window 1) at full strength; B holds it
            // half way out (window 0.5625) and is faded. A must win; flipping the strengths must
            // flip the winner.
            Wake A = rndWake(); A.w = 1.0f;
            placeAt(A, cw, radiusAtS(A, 0.0f), rndDir());
            Wake B = rndWake(); B.w = 0.05f;
            float rB = radiusAtS(B, 0.5f);
            if (!(rB > 1e-3f)) continue;
            placeAt(B, cw, rB, rndDir());

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
            if (!same(both2, onlyB2)) noSwap++;
        }
        CHECK(tested > 1500, "too few authority trials (%d)", tested);
        CHECK(wrongWinner == 0, "%d of %d trials: the slot with the greater authority did not win", wrongWinner, tested);
        CHECK(notExclusive == 0, "%d of %d trials: the result was neither slot alone (the fields SUMMED)", notExclusive, tested);
        CHECK(noSwap == 0, "%d of %d trials: swapping the strengths did not swap the winner", noSwap, tested);
        printf("9. slot authority (%d trials): the greater w*window wins and the loser contributes "
               "bit-exactly nothing\n", tested);
    }

    if (failures) { printf("\n%d FAILURE(S)\n", failures); return 1; }
    printf("\nall properties hold\n");
    return 0;
}
"""

# The negative control drives the SAME differential-normal sweep with the Jacobian's RADIAL STRETCH
# term neutered via the shipped file's own #ifndef dial. That term is a = 1 + A*w*P'(s) — it carries
# the ENTIRE derivative of the wavelet, so it is exactly what a normal that "looks about right"
# would omit, and with it at 1 the map is still deformed and the normal is still plausible. It must
# BREAK test 7, otherwise that test was passing by luck.
CONTROL_MAIN = COMMON + r"""
int main()
{
    const int LEVELS = 4;
    const float FRACS[LEVELS] = { 0.04f, 0.02f, 0.01f, 0.005f };
    float worst[LEVELS] = { 0, 0, 0, 0 };
    int tested = 0;
    setIdentityModel();     // the control is about the Jacobian, not the transform stack
    for (int trial = 0; trial < 6000; trial++) {
        float3 nrm(0,1,0), u(0,0,1), v(1,0,0);         // cross(u, v) == n
        float3 c = nrm * 0.5f + u * rnd(-0.4f, 0.4f) + v * rnd(-0.4f, 0.4f);
        Wake k = rndWake();
        k.w = 1.0f;
        float3 rhat = rndDir();
        float r0 = radiusAtS(k, rnd(-0.90f, 0.90f));
        if (!(r0 > 1.0f)) continue;
        placeAt(k, c, r0, rhat);
        setBank(1, k);

        Vtx mid; mid.pObj = c; mid.nObj = nrm;
        Out rm = run(mid);
        if (identical(mid, rm)) continue;

        float scale = featureScale(k, r0, CYCLES);
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
    printf("radial Jacobian term neutered, %d trials: worst 1-dot", tested);
    for (int lv = 0; lv < LEVELS; lv++) printf("  %.4gxs:%.3g", FRACS[lv], worst[lv]);
    // The control FIRES when the error refuses to converge: the finest patch must still be grossly
    // wrong, and halving must NOT have quartered it.
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
    for name in ("_PrismWakeCentre", "_PrismWakeShape", "_PrismWakeParams", "PrismWakeFront",
                 "PRISM_WAKE_SLOTS", "PRISM_WAKE_MIN_STRETCH", "PRISM_WAKE_RADIAL_GAIN"):
        assert name in out, f"{name} missing from the shipped HLSL"
    # A sphere has no axis. If this array comes back the frame is not spherical any more, and the
    # whole of what this harness measures (radial purity, the shear-free Jacobian) is about a
    # different map — so it fails LOUD here rather than passing tests written for the old one.
    code = "\n".join(l for l in out.splitlines() if not l.lstrip().startswith("//"))
    assert "_PrismWakeAxis" not in code, \
        "_PrismWakeAxis is back in the shipped HLSL — the map is no longer spherical and this " \
        "harness proves the wrong thing"
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

        print("\n10. negative control (Jacobian RADIAL STRETCH term neutered via -D):")
        rc = build_and_run(work, CONTROL_MAIN, ["-DPRISM_WAKE_RADIAL_GAIN=0.0"], "control")
        if rc is None or rc != 0:
            print("\nFAILED: the negative control did not fire — the analytic normal's radial "
                  "stretch term is not what makes test 7 pass", file=sys.stderr)
            return 1

        print("\nAll shockwave-front properties hold for the shipped file.")
        return 0
    finally:
        if keep:
            print("kept: " + work)
        else:
            shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
