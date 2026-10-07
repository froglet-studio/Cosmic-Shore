#!/usr/bin/env python3
"""
Prove the SHIPPED PrismGravityWarp.hlsl does what Docs/BLACK_HOLE.md §5 says it does, by compiling
the file itself with clang++ and RUNNING it (/asset-surgery §4.5c) — the same harness shape as
verify_prism_cradle.py. Nothing here is a transcription of the shader; the file under Assets/ is
translated mechanically (HLSL -> C++ spelling only) and executed under a real non-uniform model
matrix.

The warp is a TIDAL STRETCH: every vertex within the reach of a hole's horizon slides along its
own radius toward the singularity by a strain that is largest at the horizon and zero at the
reach. Near faces move more than far faces, so a prism elongates along the radial and squeezes
across it — spaghettification.

What it proves, each on randomized inputs:

  1. IDENTITY with no live slot (count 0): bit-identical pass-through. "A match with no hole in
     it costs nothing and changes nothing."
  2. IDENTITY beyond the reach: a vertex farther than r_s + reach from the hole is bit-identical.
  3. THE PULL: an affected vertex moves TOWARD the hole, never away, and never crosses the centre
     (f > 0) — and at the horizon with strain w it lands at exactly (1 - w) of its distance.
  4. NO FOLD: f is strictly increasing in d along any ray, so two vertices at different radii keep
     their order and the prism never turns inside out.
  5. RADIAL PURITY: the displacement is exactly along the vertex's own radius, so the direction
     from the hole is bit-preserved — what makes the analytic normal derivable at all.
  6. TIDAL STRETCH: inside the shell the radial stretch a = f'(d) exceeds the tangential stretch
     b = f/d (strictly, wherever the falloff is not flat) — the theorem the header states, which
     is the physical claim "spaghettification" rests on.
  7. THE NORMAL IS THE MAP'S DERIVATIVE: a CONVERGENCE test, not a tolerance. Halving the test
     patch must QUARTER the error (an exact first derivative's signature); a wrong Jacobian
     plateaus (test 11).
  8. NO SEAM AT THE FAR EDGE: sweeping a vertex through s = reach, neither the position nor the
     normal jumps.
  9. STRAIN IS AFFINE: f = d - d·k·w is affine in w at fixed geometry, so the eased spawn/despawn
     is a blend of the MAP and never a differently-shaped warp.
 10. DOMINANT SLOT: with two holes live, a vertex is warped by the one with the greater authority
     (w × k), and only by that one.
 11. NEGATIVE CONTROL: rebuilt with the Jacobian's RADIAL term neutered (-D override of the file's
     own #ifndef dial), test 7's error STOPS CONVERGING — so the analytic normal is what holds it.

Exit 0 on pass. Needs clang++; nothing else, and no Unity.
Usage:  python3 Tools/Shaders/verify_prism_gravity_warp.py [--keep]
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/PrismGravityWarp.hlsl")

SHIM = r"""// Minimal HLSL->C++ shim so the SHIPPED PrismGravityWarp.hlsl can be compiled and executed by
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

COMMON = r"""#include "shipped.h"
#include <cstdio>
#include <cstdlib>
#include <random>
#include <vector>

float4x4 g_objectToWorld, g_worldToObject;

static int failures = 0;
#define CHECK(cond, ...) do { if (!(cond)) { failures++; printf("FAIL: "); printf(__VA_ARGS__); printf("\n"); } } while (0)

static std::mt19937 rng(20261007);
static float rnd(float a, float b) { return a + (b - a) * (rng() / (float)rng.max()); }
static float3 rndDir() { for(;;){ float3 v(rnd(-1,1),rnd(-1,1),rnd(-1,1)); float l=length(v); if(l>0.1f) return v/l; } }

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
static void setIdentity() {
    for(int i=0;i<4;i++)for(int j=0;j<4;j++){ g_objectToWorld.m[i][j]=(i==j); g_worldToObject.m[i][j]=(i==j); }
}
static float3 toWorld(float3 p){ return mul(g_objectToWorld, float4(p,1)).xyz(); }
static float3 toObject(float3 p){ return mul(g_worldToObject, float4(p,1)).xyz(); }
static float3 xformDir(float3 d){ return mul(g_objectToWorld, float4(d,0)).xyz(); }
static float3 normalToWorld(float3 n){ float3 w = mul(n, (float3x3)g_worldToObject); return w / length(w); }

static const float RS    = 20.0f;   // a strength-10 hole's horizon at the shipped config
static const float REACH = 100.0f;  // (warpReachMultiplier 6 - 1) x r_s
static const float EXPO  = 2.0f;
static const float W     = 0.6f;    // the shipped warpStrength

static void setBank(int count, float3 U0, float rs0, float w0, float reach0,
                    float3 U1 = float3(0,0,0), float rs1 = 0, float w1 = 0, float reach1 = 0,
                    float expo = EXPO) {
    _PrismGravityWarpCentre[0] = float4(U0, rs0); _PrismGravityWarpWeight[0] = float4(w0, reach0, 0, 0);
    _PrismGravityWarpCentre[1] = float4(U1, rs1); _PrismGravityWarpWeight[1] = float4(w1, reach1, 0, 0);
    _PrismGravityWarpCentre[2] = float4(0,0,0,0); _PrismGravityWarpWeight[2] = float4(0,0,0,0);
    _PrismGravityWarpCentre[3] = float4(0,0,0,0); _PrismGravityWarpWeight[3] = float4(0,0,0,0);
    _PrismGravityWarpParams = float4(expo, (float)count, 0.0f, 0.0f);
}

struct Vtx { float3 pObj; float3 nObj; };
static float3 faceNormal(int f) {
    switch (f) { case 0: return float3(1,0,0); case 1: return float3(-1,0,0);
                 case 2: return float3(0,1,0); case 3: return float3(0,-1,0);
                 case 4: return float3(0,0,1); default: return float3(0,0,-1); }
}
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
            float su = (float)i / n, sv = (float)j / n;
            Vtx x; x.nObj = nrm;
            x.pObj = c + u * ((su - 0.5f)) + v * ((sv - 0.5f));
            out.push_back(x);
        }
    }
    return out;
}

struct Out { float3 pObj, nObj, pW, nW; };
static Out run(const Vtx& x) {
    Out r;
    PrismGravityWarpDeform_float(x.pObj, x.nObj, r.pObj, r.nObj);
    r.pW = toWorld(r.pObj);
    r.nW = normalToWorld(r.nObj);
    return r;
}
static bool identical(const Vtx& x, const Out& r) {
    return r.pObj.x == x.pObj.x && r.pObj.y == x.pObj.y && r.pObj.z == x.pObj.z
        && r.nObj.x == x.nObj.x && r.nObj.y == x.nObj.y && r.nObj.z == x.nObj.z;
}

// Shared by test 7 and the negative control: the convergence sweep of the differential-normal
// test over LEVELS patch sizes. Returns the worst 1-dot per level and the trial count.
static const int LEVELS = 4;
static const float FRACS[LEVELS] = { 0.02f, 0.01f, 0.005f, 0.0025f };
static int convergenceSweep(float worst[LEVELS], int trials, bool nonUniformModel) {
    const float3 SCALE(3, 1, 6);
    int tested = 0;
    for (int k = 0; k < LEVELS; k++) worst[k] = 0;
    for (int trial = 0; trial < trials; trial++) {
        if (nonUniformModel) setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
        else setIdentity();
        int f = rng() % 6;
        float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
        float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
        float3 cw = toWorld(c);
        // The hole somewhere that puts this patch inside the shell, near the horizon, or past the reach.
        float3 U = cw + rndDir() * rnd(RS + 1.0f, RS + REACH + 10.0f);
        setBank(1, U, RS, rnd(0.2f, W), REACH);

        Vtx mid; mid.pObj = c; mid.nObj = nrm;
        Out rm = run(mid);
        if (identical(mid, rm)) continue;

        float d = length(cw - U);
        float unit = std::max(length(xformDir(u)), length(xformDir(v)));

        float e[LEVELS]; bool usable = true;
        for (int k = 0; k < LEVELS && usable; k++) {
            float eps = FRACS[k] * d / unit;
            float3 moved[3], flat[3];
            for (int i = 0; i < 3; i++) {
                float a = 2.0944f * i;
                Vtx x; x.nObj = nrm;
                x.pObj = c + u * (eps * std::cos(a)) + v * (eps * std::sin(a));
                flat[i] = toWorld(x.pObj);
                moved[i] = run(x).pW;
            }
            float3 g = cross(moved[1] - moved[0], moved[2] - moved[0]);
            float3 g0 = cross(flat[1] - flat[0], flat[2] - flat[0]);
            float gl = length(g), gl0 = length(g0);
            if (!(gl > 1e-16f) || !(gl0 > 1e-16f) || gl / gl0 < 0.01f) { usable = false; break; }
            e[k] = 1.0f - dot(g / gl, rm.nW);
        }
        if (!usable) continue;
        tested++;
        for (int k = 0; k < LEVELS; k++) worst[k] = std::max(worst[k], e[k]);
    }
    return tested;
}
"""

HARNESS = COMMON + r"""
int main()
{
    const float3 SCALE(3, 1, 6);   // a trail slab

    // ---------------- 1. identity with no live slot ----------------
    {
        int bad = 0, tested = 0;
        auto vs = prismVerts(3);
        for (int trial = 0; trial < 150; trial++) {
            setModel(float3(rnd(-50,50),rnd(-50,50),rnd(-50,50)), rndDir(), rnd(0, 6.28f), SCALE);
            setBank(0, float3(rnd(-5,5),rnd(-5,5),rnd(-5,5)), RS, W, REACH);
            for (const Vtx& x : vs) { tested++; if (!identical(x, run(x))) bad++; }
        }
        CHECK(bad == 0, "identity with count 0 broken on %d of %d vertices", bad, tested);
        printf("1. count 0 -> bit-identical pass-through: %s (%d vertices)\n", bad == 0 ? "ok" : "BROKEN", tested);
    }

    // ---------------- 2. identity beyond the reach ----------------
    {
        int bad = 0, tested = 0;
        auto vs = prismVerts(3);
        for (int trial = 0; trial < 200; trial++) {
            setModel(float3(0,0,0), rndDir(), rnd(0, 6.28f), SCALE);
            float3 U = rndDir() * rnd(RS + REACH + 8.0f, RS + REACH + 200.0f);
            setBank(1, U, RS, W, REACH);
            for (const Vtx& x : vs) {
                if (length(toWorld(x.pObj) - U) < RS + REACH) continue;
                tested++;
                if (!identical(x, run(x))) bad++;
            }
        }
        CHECK(tested > 2000, "too few beyond-reach samples (%d)", tested);
        CHECK(bad == 0, "identity beyond the reach broken on %d of %d vertices", bad, tested);
        printf("2. vertex past r_s + reach -> untouched: %s (%d vertices)\n", bad == 0 ? "ok" : "BROKEN", tested);
    }

    // ---------------- 3. the pull: toward, never past the centre, (1-w) at the horizon ----------------
    {
        int tested = 0, away = 0, crossed = 0;
        float worstHorizon = 0;
        for (int trial = 0; trial < 300; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            float3 U = toWorld(float3(0,0,0)) + rndDir() * rnd(0.0f, RS + REACH - 1.0f);
            setBank(1, U, RS, W, REACH);
            auto vs = prismVerts(4);
            for (const Vtx& x : vs) {
                float3 pw = toWorld(x.pObj);
                float d = length(pw - U);
                if (!(d > 1e-3f) || d >= RS + REACH) continue;
                Out r = run(x);
                tested++;
                float f = length(r.pW - U);
                if (f > d + 1e-4f) away++;
                if (dot(r.pW - U, pw - U) <= 0.0f) crossed++;
            }
            // Exactly at the horizon the strain is w: f = (1 - w) r_s.
            float3 dir = rndDir();
            Vtx h; h.pObj = toObject(U + dir * RS); h.nObj = float3(0,1,0);
            float fh = length(run(h).pW - U);
            worstHorizon = std::max(worstHorizon, std::fabs(fh - (1.0f - W) * RS));
        }
        CHECK(tested > 5000, "too few pull samples (%d)", tested);
        CHECK(away == 0, "%d vertices moved AWAY from the hole", away);
        CHECK(crossed == 0, "%d vertices crossed the centre to the far side", crossed);
        CHECK(worstHorizon < 2e-3f, "horizon vertex not at (1 - w) r_s: worst err %g", worstHorizon);
        printf("3. pull (%d samples): always toward the hole, never through it; horizon lands at (1-w) r_s (err %.2e)\n",
               tested, worstHorizon);
    }

    // ---------------- 4. no fold ----------------
    {
        int tested = 0, folded = 0;
        for (int trial = 0; trial < 400; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            float3 U = toWorld(rndDir() * 0.4f);
            float w = rnd(0.1f, 0.95f);
            setBank(1, U, RS, w, REACH);
            float3 dir = rndDir();
            float prevF = -1e30f;
            for (float d = 0.5f; d <= RS + REACH + 5.0f; d += 0.25f) {
                float3 pw = U + dir * d;
                Vtx x; x.pObj = toObject(pw); x.nObj = float3(0,1,0);
                float f = length(run(x).pW - U);
                tested++;
                if (f <= prevF) folded++;
                prevF = f;
            }
        }
        CHECK(tested > 100000, "too few fold samples (%d)", tested);
        CHECK(folded == 0, "the map FOLDS in %d places (f not strictly increasing in d)", folded);
        printf("4. no fold (%d samples along %d rays, strain up to 0.95): f strictly increasing in d\n", tested, 400);
    }

    // ---------------- 5. radial purity ----------------
    {
        float worstDrift = 0; int tested = 0;
        auto vs = prismVerts(4);
        for (int trial = 0; trial < 200; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            float3 U = toWorld(rndDir() * rnd(0.0f, 0.8f)) + rndDir() * rnd(RS * 0.5f, RS + REACH * 0.5f);
            setBank(1, U, RS, rnd(0.2f, W), REACH);
            for (const Vtx& x : vs) {
                float3 pw = toWorld(x.pObj);
                float d = length(pw - U);
                if (!(d > 0.5f) || d > RS + REACH) continue;
                Out r = run(x);
                tested++;
                float3 d0 = (pw - U) / d, d1 = (r.pW - U) / length(r.pW - U);
                worstDrift = std::max(worstDrift, 1.0f - dot(d0, d1));
            }
        }
        CHECK(tested > 2000, "too few radial-purity samples (%d)", tested);
        CHECK(worstDrift < 1e-5f, "displacement is not radial: worst direction drift 1-dot = %g", worstDrift);
        printf("5. radial purity (%d samples): direction from the hole bit-preserved (worst 1-dot %.2e)\n",
               tested, worstDrift);
    }

    // ---------------- 6. tidal stretch: radial a exceeds tangential b inside the shell ----------------
    {
        // Measured from the shipped map by finite difference along a ray, against f/d: wherever the
        // falloff is not flat (strictly between the horizon and the reach) the radial stretch must
        // exceed the tangential one. This is the theorem the header states and the look rests on.
        setIdentity();
        float3 U(0,0,0);
        setBank(1, U, RS, W, REACH);
        float3 dir = rndDir();
        int tested = 0, notStretched = 0; float minRatio = 1e30f, maxRatio = 0;
        const float h = 0.01f;
        for (float d = RS + 2.0f; d < RS + REACH - 2.0f; d += 0.5f) {
            auto f_at = [&](float dd) {
                Vtx x; x.pObj = U + dir * dd; x.nObj = float3(0,1,0);
                return length(run(x).pW - U);
            };
            float a = (f_at(d + h) - f_at(d - h)) / (2 * h);
            float b = f_at(d) / d;
            tested++;
            float ratio = a / b;
            minRatio = std::min(minRatio, ratio); maxRatio = std::max(maxRatio, ratio);
            if (!(a > b)) notStretched++;
        }
        CHECK(tested > 100, "too few tidal samples (%d)", tested);
        CHECK(notStretched == 0, "radial stretch did not exceed tangential at %d of %d radii", notStretched, tested);
        printf("6. tidal stretch (%d radii): a/b in [%.3f, %.3f] > 1 everywhere inside the shell — spaghettification\n",
               tested, minRatio, maxRatio);
    }

    // ---------------- 7. the normal is the map's DERIVATIVE ----------------
    {
        float worst[LEVELS];
        int tested = convergenceSweep(worst, 4000, true);
        CHECK(tested > 1500, "too few differential-normal trials (%d)", tested);
        CHECK(worst[LEVELS-1] < 0.02f, "the returned normal does not approach the deformed surface: "
              "worst 1-dot at the finest patch = %g", worst[LEVELS-1]);
        for (int k = 1; k < LEVELS; k++)
            CHECK(worst[k] < 0.40f * worst[k-1],
                  "halving the patch did not quarter the error (%g -> %g) — the returned normal is "
                  "not the map's derivative", worst[k-1], worst[k]);
        printf("7. analytic normal == d(map) (%d trials): worst 1-dot", tested);
        for (int k = 0; k < LEVELS; k++) printf("  %.4gxd:%.3g", FRACS[k], worst[k]);
        printf("  (quartering => exact derivative)\n");
    }

    // ---------------- 8. no seam at the far edge ----------------
    {
        setModel(float3(0,0,0), float3(0,0,1), 0.3f, SCALE);
        float3 U = toWorld(float3(0,0,0));
        setBank(1, U, RS, W, REACH);
        float3 dir = normalToWorld(float3(0,1,0));
        const float step = 0.002f;
        float worstPos = 0, worstNrm = 0;
        float3 prevP(0,0,0), prevN(0,0,0); bool have = false;
        for (float s = REACH - 1.0f; s <= REACH + 1.0f; s += step) {
            float3 pw = U + dir * (RS + s);
            Vtx x; x.pObj = toObject(pw); x.nObj = float3(0,1,0);
            Out r = run(x);
            if (have) {
                worstPos = std::max(worstPos, length(r.pW - prevP) - step);
                worstNrm = std::max(worstNrm, 1.0f - dot(r.nW, prevN));
            }
            prevP = r.pW; prevN = r.nW; have = true;
        }
        CHECK(worstPos < 1e-3f, "position seam at the reach: %g u of jump beyond the %g u step", worstPos, step);
        CHECK(worstNrm < 1e-5f, "normal seam at the reach: worst 1-dot between adjacent samples = %g", worstNrm);
        printf("8. no seam at s = reach: worst excess position jump %.2e u, worst normal 1-dot %.2e\n",
               worstPos, worstNrm);
    }

    // ---------------- 9. strain is affine ----------------
    {
        float worstLinear = 0; int tested = 0;
        for (int trial = 0; trial < 300; trial++) {
            setModel(float3(rnd(-10,10),rnd(-10,10),rnd(-10,10)), rndDir(), rnd(0, 6.28f), SCALE);
            float3 U = toWorld(rndDir() * 0.3f);
            float3 dir = rndDir();
            float d = rnd(RS * 0.5f, RS + REACH * 0.9f);
            float3 pw = U + dir * d;
            Vtx x; x.pObj = toObject(pw); x.nObj = float3(0,1,0);
            setBank(1, U, RS, 0.8f, REACH);  float f1 = length(run(x).pW - U);
            setBank(1, U, RS, 0.2f, REACH);  float fq = length(run(x).pW - U);
            tested++;
            worstLinear = std::max(worstLinear, std::fabs((fq - d) * 4.0f - (f1 - d)));
        }
        CHECK(worstLinear < 2e-3f, "the map is not affine in the strain (worst err %g)", worstLinear);
        printf("9. strain affine (%d trials): the eased spawn is a blend of the MAP (worst err %.2e)\n",
               tested, worstLinear);
    }

    // ---------------- 10. dominant slot ----------------
    {
        setIdentity();
        float3 pw(0, 0.5f, 0);
        Vtx x; x.pObj = pw; x.nObj = float3(0,1,0);
        // Hole A close (near its horizon, high authority), hole B far out in its shell: A must win.
        float3 A = pw + float3(0, RS + 2.0f, 0);
        float3 B = pw - float3(0, RS + REACH - 5.0f, 0);
        setBank(2, B, RS, W, REACH, A, RS, W, REACH);
        Out r = run(x);
        float dA = length(pw - A), fA = length(r.pW - A);
        CHECK(fA < dA - 1e-3f, "dominant slot: the vertex was not pulled toward the NEAR hole");
        CHECK(std::fabs(dot((r.pW - A) / fA, (pw - A) / dA) - 1.0f) < 1e-5f, "dominant slot: the vertex left the near hole's radial");
        // Same pair, but the near hole faded almost out: the far one must take over.
        setBank(2, B, RS, W, REACH, A, RS, 0.001f, REACH);
        Out r2 = run(x);
        CHECK(length(r2.pW - r.pW) > 1e-3f, "dominant slot: authority (w x k) is not what selects the slot");
        printf("10. two holes live: the greater authority (w x k) warps the vertex, and only it\n");
    }

    if (failures) { printf("\n%d FAILURE(S)\n", failures); return 1; }
    printf("\nall properties hold\n");
    return 0;
}
"""

# The negative control drives the SAME differential-normal sweep with the Jacobian's RADIAL term
# neutered via the shipped file's own #ifndef dial: clamping the radial stretch to >= 1 leaves
# only the tangential correction. It must BREAK test 7 — otherwise that test was passing by luck.
CONTROL_MAIN = COMMON + r"""
int main()
{
    float worst[LEVELS];
    int tested = convergenceSweep(worst, 3000, false);
    printf("radial Jacobian neutered, %d trials: worst 1-dot", tested);
    for (int k = 0; k < LEVELS; k++) printf("  %.4gxd:%.3g", FRACS[k], worst[k]);
    bool plateaus = worst[LEVELS-1] > 0.02f && worst[LEVELS-1] > 0.40f * worst[LEVELS-2];
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
    out = re.sub(r"\[unroll\]", "", out)
    assert "void PrismGravityWarpDeform_float(" in out, "entry point missing"
    assert "float3 Position, float3 Normal," in out, \
        "the entry point's signature is not (Position, Normal) — the harness and the wirer disagree"
    for name in ("_PrismGravityWarpCentre", "_PrismGravityWarpWeight", "_PrismGravityWarpParams",
                 "PRISM_GRAVITY_WARP_SLOTS", "PRISM_GRAVITY_WARP_MIN_RADIAL"):
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

    work = tempfile.mkdtemp(prefix="prism_gravity_warp_verify_")
    try:
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(SHIM)
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write('#include "shim.h"\n' + translate(open(HLSL).read()))

        rc = build_and_run(work, HARNESS, [], "verify")
        if rc is None or rc != 0:
            print("\nFAILED", file=sys.stderr)
            return 1

        print("\n11. negative control (radial Jacobian term neutered via -D):")
        rc = build_and_run(work, CONTROL_MAIN, ["-DPRISM_GRAVITY_WARP_MIN_RADIAL=1.0"], "control")
        if rc is None or rc != 0:
            print("\nFAILED: the negative control did not fire — the analytic normal's radial "
                  "term is not what makes test 7 pass", file=sys.stderr)
            return 1

        print("\nAll gravity-warp properties hold for the shipped file.")
        return 0
    finally:
        if keep:
            print("kept: " + work)
        else:
            shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
