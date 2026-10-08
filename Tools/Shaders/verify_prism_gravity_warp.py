#!/usr/bin/env python3
"""
Prove the SHIPPED PrismGravityWarp.hlsl draws SPAGHETTIFICATION as the physics says it is
(Docs/BLACK_HOLE.md §5), by compiling the file itself with clang++ and RUNNING it (/asset-surgery
§4.5c) under a real non-uniform model matrix. Nothing here is a transcription of the shader; the
file under Assets/ is translated mechanically (HLSL -> C++ spelling only) and executed.

The stretch is the general-relativistic tidal tensor for radial free fall, (GM/r³)·diag(2,−1,−1),
applied to each prism about its own centre for a response time τ: log-stretch ε = GM·τ²/r³ along
the line to the hole and −ε/2 across it (the bank carries GM·τ² as one coefficient K).

What it proves:

  1. IDENTITY with no live slot (count 0): bit-identical pass-through.
  2. IDENTITY beyond the reach: a prism whose centre is past r_s + reach is bit-identical.
  3. THE CENTRE STAYS PUT: the prism's own centre maps to itself — tides deform, the gravity field
     (not this file) moves mass.
  4. THE TIDAL TENSOR: measured log-stretches equal +K/d³ along the radial and −K/(2d³) across it,
     at d = 1.5, 2, 3 and 5 horizon radii — the trace-free 2 : −1 : −1 tensor, number for number —
     out to 3 r_s at the SHIPPED reach (the fade touches only the outer half of the shell).
  5. 1/r³, STRONGEST AT THE HORIZON: the stretch falls strictly with distance, doubling the distance
     divides it by 8, and a hole with twice the mass and twice the horizon stretches a prism at
     the same multiple of its horizon 4× LESS (ε_h ∝ 1/M²: small holes shred, big ones swallow).
  6. VOLUME IS CONSERVED, at every strength including the ceiling.
  7. NO FOLD, A CEILING: every stretch is positive and the length never exceeds the configured
     maximum, however small the hole — a prism at a tiny horizon is a needle, not a line.
 7b. THE CEILING BENDS ONLY NEAR ITSELF: at the shipped ceiling, a tide of half of it is drawn within
     1.5% of the law and a quarter within 0.1%, and the drawn stretch never falls as the tide grows.
  8. ONE AFFINE MAP PER PRISM: the image of a midpoint is the midpoint of the images, so flat faces
     stay flat and the authored 24-triangle prism is exact (no high-poly residency needed).
  9. THE NORMAL IS THE INVERSE TRANSPOSE: on every face of a rotated, non-uniformly scaled prism,
     the output normal is perpendicular to the stretched face.
 10. THE EASE IS LINEAR IN THE LOG: half the coefficient (a half-eased spawn) is half the stretch.
 11. NO SEAM AT THE REACH: as a prism's centre crosses r_s + reach the stretch has already faded
     to nothing.
 12. DOMINANT SLOT: with two holes live, the one with the larger tide at the prism stretches it.
 13. NEGATIVE CONTROL: rebuilt with the normal correction switched off (-D override of the file's
     own #ifndef dial), test 9 FAILS — the inverse transpose is what holds it.

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
static inline float sign(float a){return a>0?1.0f:(a<0?-1.0f:0.0f);}
static inline float3 operator-(float3 a){return float3(-a.x,-a.y,-a.z);}
static inline float smoothstep(float e0,float e1,float x){float t=saturate((x-e0)/(e1-e0));return t*t*(3.0f-2.0f*t);}
using std::pow; using std::sqrt; using std::exp;

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

static std::mt19937 rng(20261008);
static float rnd(float a, float b) { return a + (b - a) * (rng() / (float)rng.max()); }
static float3 rndDir() { for(;;){ float3 v(rnd(-1,1),rnd(-1,1),rnd(-1,1)); float l=length(v); if(l>0.1f) return v/l; } }

static void rotationMatrix(float3 axis, float angle, float R[3][3]) {
    float s = std::sin(angle), c = std::cos(angle), t = 1 - c;
    float x = axis.x, y = axis.y, z = axis.z;
    float M[3][3] = {{t*x*x+c, t*x*y-s*z, t*x*z+s*y},{t*x*y+s*z, t*y*y+c, t*y*z-s*x},{t*x*z-s*y, t*y*z+s*x, t*z*z+c}};
    for(int i=0;i<3;i++)for(int j=0;j<3;j++)R[i][j]=M[i][j];
}
// A prism at world centre t, rotated, scaled non-uniformly (the shipped prisms are 3 x 1 x 6).
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
static float3 toWorld(float3 p){ return mul(g_objectToWorld, float4(p,1)).xyz(); }
static float3 toObject(float3 p){ return mul(g_worldToObject, float4(p,1)).xyz(); }
static float3 normalToWorld(float3 n){ float3 w = mul(n, (float3x3)g_worldToObject); return w / length(w); }

static const float RS = 20.0f;          // a strength-10 hole's horizon at the shipped config
static const float GM = 20000.0f;       // its GM (gmPerStrength 2000 x 10)
static const float TAU = 0.9f;          // the shipped tidalResponseSeconds
static const float K = GM * TAU * TAU;  // what the bank carries: GM·τ²
static const float REACH = 100.0f;      // (warpReachMultiplier 6 - 1) x r_s
static const float LN_MAX = 2.4849066f; // ln(maxTidalStretch 12)

static float g_soft0 = 0.0f;   // slot 0's Plummer core (a smooth well); 0 = the black hole's tide
static void setBank(int count, float3 U0, float rs0, float k0, float reach0, float lnMax = LN_MAX,
                    float3 U1 = float3(0,0,0), float rs1 = 0, float k1 = 0, float reach1 = 0) {
    _PrismGravityWarpCentre[0] = float4(U0, rs0); _PrismGravityWarpWeight[0] = float4(k0, reach0, g_soft0, 0);
    _PrismGravityWarpCentre[1] = float4(U1, rs1); _PrismGravityWarpWeight[1] = float4(k1, reach1, 0, 0);
    _PrismGravityWarpCentre[2] = float4(0,0,0,0); _PrismGravityWarpWeight[2] = float4(0,0,0,0);
    _PrismGravityWarpCentre[3] = float4(0,0,0,0); _PrismGravityWarpWeight[3] = float4(0,0,0,0);
    _PrismGravityWarpParams = float4(lnMax, (float)count, 0.0f, 0.0f);
}

// Run the shipped function on an object-space point (+ a normal), return world space.
static float3 mapWorld(float3 pObj, float3 nObj = float3(0,0,1)) {
    float3 op, on; PrismGravityWarpDeform_float(pObj, nObj, op, on); return toWorld(op);
}
// The stretch the shipped map applies along world direction dir (unit) about centre c.
static float stretchAlong(float3 c, float3 dir, float L = 0.5f) {
    float3 img = mapWorld(toObject(c + dir * L));
    return length(img - c) / L;
}
static float3 anyPerp(float3 n) { float3 a = std::fabs(n.x) < 0.9f ? float3(1,0,0) : float3(0,1,0); float3 p = cross(n, a); return p / length(p); }

// A prism at distance d from a hole at the origin, along a random direction; returns that direction.
static float3 placePrism(float d) {
    float3 n = rndDir();
    setModel(n * d, rndDir(), rnd(0, 6.28f), float3(3, 1, 6));
    return n;
}
"""

HARNESS = COMMON + r"""
int main()
{
    // 1. no live slot
    {
        setBank(0, float3(0,0,0), RS, K, REACH);
        float3 n = placePrism(1.5f * RS); (void)n;
        bool same = true;
        for (int i = 0; i < 50; i++) {
            float3 p(rnd(-0.5f,0.5f), rnd(-0.5f,0.5f), rnd(-0.5f,0.5f)), nn = rndDir(), op, on;
            PrismGravityWarpDeform_float(p, nn, op, on);
            same &= op.x == p.x && op.y == p.y && op.z == p.z && on.x == nn.x && on.y == nn.y && on.z == nn.z;
        }
        CHECK(same, "a vertex moved with no hole live");
        printf("1. no live slot: bit-identical\n");
    }

    // 2. beyond the reach
    {
        setBank(1, float3(0,0,0), RS, K, REACH);
        placePrism(RS + REACH + 0.5f);
        bool same = true;
        for (int i = 0; i < 50; i++) {
            float3 p(rnd(-0.5f,0.5f), rnd(-0.5f,0.5f), rnd(-0.5f,0.5f)), nn = rndDir(), op, on;
            PrismGravityWarpDeform_float(p, nn, op, on);
            same &= op.x == p.x && op.y == p.y && op.z == p.z && on.x == nn.x && on.y == nn.y && on.z == nn.z;
        }
        CHECK(same, "a prism past the reach was touched");
        printf("2. prism centre past r_s + reach: bit-identical\n");
    }

    // 3. the centre stays put
    {
        setBank(1, float3(0,0,0), RS, K, REACH);
        float worst = 0;
        for (int i = 0; i < 200; i++) {
            placePrism(rnd(RS, RS + REACH));
            float3 c = toWorld(float3(0,0,0));
            worst = std::max(worst, length(mapWorld(float3(0,0,0)) - c));
        }
        CHECK(worst < 1e-3f, "a prism's centre moved by %.2g", worst);
        printf("3. the prism's centre maps to itself (worst %.2g u over 200 prisms)\n", worst);
    }

    // 4. the tidal tensor, number for number — at the SHIPPED reach out to 3 r_s (the window is exactly 1
    //    across the inner half of the shell), and with the fade pushed away at 5 r_s; a high ceiling so
    //    the soft minimum is the identity and the law itself is what is measured
    {
        const float SMALL_K = 0.25f * RS * RS * RS;    // eps(1.5 r_s) = 0.074: well under the ceiling
        const float ds[4] = { 1.5f, 2.0f, 3.0f, 5.0f };
        float worst = 0;
        for (float m : ds) {
            setBank(1, float3(0,0,0), RS, SMALL_K, m <= 3.0f ? REACH : 1e7f, 13.8f);
            float d = m * RS;
            float3 n = placePrism(d);
            float3 c = toWorld(float3(0,0,0));
            // A long probe: the map is affine, so length costs no accuracy and buys float headroom
            // (ε is 0.002 at 5 r_s, on a prism 100 u from the origin).
            float er = std::log(stretchAlong(c, n, 4.0f));
            float et = std::log(stretchAlong(c, anyPerp(n), 4.0f));
            float expect = SMALL_K / (d * d * d);
            float relR = std::fabs(er - expect) / expect, relT = std::fabs(-et - 0.5f * expect) / (0.5f * expect);
            worst = std::max(worst, std::max(relR, relT));
            printf("4. d = %.1f r_s: radial log-stretch %.5f (GM tau^2/d^3 = %.5f), transverse %.5f (-half = %.5f)\n",
                   m, er, expect, et, -0.5f * expect);
        }
        CHECK(worst < 3e-3f, "the stretch is not the tidal tensor (worst relative error %.3g)", worst);
    }

    // 5. 1/r^3, strongest at the horizon, eps_h ~ 1/M^2
    {
        const float SMALL_K = 0.25f * RS * RS * RS;
        setBank(1, float3(0,0,0), RS, SMALL_K, 1e7f, 13.8f);
        float prev = 1e9f; bool falls = true;
        for (float m = 1.0f; m <= 6.0f; m += 0.25f) {
            float3 n = placePrism(m * RS);
            float e = std::log(stretchAlong(toWorld(float3(0,0,0)), n));
            falls &= e < prev; prev = e;
        }
        float3 n1 = placePrism(2.0f * RS); float e1 = std::log(stretchAlong(toWorld(float3(0,0,0)), n1));
        float3 n2 = placePrism(4.0f * RS); float e2 = std::log(stretchAlong(toWorld(float3(0,0,0)), n2));
        // twice the mass and twice the horizon: K doubles, and the prism sits at the same 1.5 r_s
        setBank(1, float3(0,0,0), RS, K, REACH);
        float3 nA = placePrism(1.5f * RS); float eA = std::log(stretchAlong(toWorld(float3(0,0,0)), nA));
        setBank(1, float3(0,0,0), 2.0f * RS, 2.0f * K, 2.0f * REACH);
        float3 nB = placePrism(3.0f * RS); float eB = std::log(stretchAlong(toWorld(float3(0,0,0)), nB));
        CHECK(falls, "the stretch does not fall strictly with distance");
        CHECK(std::fabs(e1 / e2 - 8.0f) < 0.05f, "doubling the distance divided the stretch by %.3f, not 8", e1 / e2);
        CHECK(std::fabs(eA / eB - 4.0f) < 0.3f, "a hole twice as massive stretched %.2fx less at 1.5 r_s, not 4x", eA / eB);
        printf("5. falls strictly from the horizon; 2x distance -> /%.3f; 2x the mass -> %.2fx less at the same r/r_s\n", e1 / e2, eA / eB);
    }

    // 6 + 7. volume conserved, no fold, the ceiling holds — from mild to absurd strength
    {
        float worstVol = 0, longest = 0; bool positive = true;
        const float ks[4] = { K, 10.0f * K, 1000.0f * K, 1e6f * K };
        for (float k : ks) {
            setBank(1, float3(0,0,0), RS, k, REACH);
            for (int i = 0; i < 100; i++) {
                float3 n = placePrism(rnd(RS, RS + 0.5f * REACH));
                float3 c = toWorld(float3(0,0,0));
                float3 a = mapWorld(toObject(c + float3(1,0,0))) - c;
                float3 b = mapWorld(toObject(c + float3(0,1,0))) - c;
                float3 e = mapWorld(toObject(c + float3(0,0,1))) - c;
                float vol = dot(cross(a, b), e);
                positive &= vol > 0;
                worstVol = std::max(worstVol, std::fabs(vol - 1.0f));
                longest = std::max(longest, stretchAlong(c, n));
            }
        }
        CHECK(worstVol < 2e-3f, "volume changed by up to %.3g", worstVol);
        CHECK(positive, "the stretch folded a prism (non-positive volume)");
        CHECK(longest <= 12.0f * 1.0005f, "a prism was drawn %.3fx long, past the 12x ceiling", longest);
        CHECK(longest > 11.5f, "the ceiling is never approached (longest %.2fx) - the strong end is not being exercised", longest);
        printf("6. volume conserved to %.2g at every strength\n7. never folds; longest %.3fx against the 12x ceiling\n", worstVol, longest);
    }

    // 7b. the ceiling bends only near itself: at the shipped ceiling (ln 12), half of it is drawn within 1.5%
    //     of the law and a quarter within 0.1%; and the drawn stretch never falls as the tide grows
    {
        float3 n = placePrism(2.0f * RS); float3 c = toWorld(float3(0,0,0));
        const float d = 2.0f * RS, d3 = d * d * d;
        setBank(1, float3(0,0,0), RS, 0.5f * LN_MAX * d3, REACH);  float half = std::log(stretchAlong(c, n, 4.0f));
        setBank(1, float3(0,0,0), RS, 0.25f * LN_MAX * d3, REACH); float quarter = std::log(stretchAlong(c, n, 4.0f));
        float relHalf = 1.0f - half / (0.5f * LN_MAX), relQuarter = 1.0f - quarter / (0.25f * LN_MAX);
        CHECK(relHalf >= 0.0f && relHalf < 0.016f, "at half the ceiling the drawn stretch is %.2f%% under the law", 100.0f * relHalf);
        CHECK(relQuarter >= 0.0f && relQuarter < 0.0015f, "at a quarter of the ceiling the drawn stretch is %.3f%% under the law", 100.0f * relQuarter);
        float prev = 0; bool monotone = true;
        for (float f = 0.05f; f < 1e5f; f *= 1.3f) {
            setBank(1, float3(0,0,0), RS, f * LN_MAX * d3, REACH);
            float e = std::log(stretchAlong(c, n, 4.0f));
            monotone &= e >= prev - 1e-5f; prev = e;
        }
        CHECK(monotone, "the drawn stretch fell while the tide grew - the ceiling is not monotone");
        printf("7b. the ceiling: %.2f%% under the law at half of it, %.3f%% at a quarter; monotone to 1e5x\n",
               100.0f * relHalf, 100.0f * relQuarter);
    }

    // 8. one affine map per prism
    {
        setBank(1, float3(0,0,0), RS, K, REACH);
        float worst = 0;
        for (int i = 0; i < 200; i++) {
            placePrism(rnd(RS, RS + REACH));
            float3 p(rnd(-0.5f,0.5f), rnd(-0.5f,0.5f), rnd(-0.5f,0.5f)), q(rnd(-0.5f,0.5f), rnd(-0.5f,0.5f), rnd(-0.5f,0.5f));
            float3 mid = mapWorld((p + q) * 0.5f), avg = (mapWorld(p) + mapWorld(q)) * 0.5f;
            worst = std::max(worst, length(mid - avg));
        }
        CHECK(worst < 1e-3f, "the map is not affine across a prism (midpoint off by %.2g)", worst);
        printf("8. affine across each prism (midpoint error %.2g u): flat faces stay flat, 24 triangles are exact\n", worst);
    }

    // 9. the normal is the inverse transpose
    {
        setBank(1, float3(0,0,0), RS, 10.0f * K, REACH);
        float worst = 0;
        for (int i = 0; i < 300; i++) {
            placePrism(rnd(RS, RS + 0.5f * REACH));
            int f = rng() % 6;
            float3 nObj = f==0?float3(1,0,0):f==1?float3(-1,0,0):f==2?float3(0,1,0):f==3?float3(0,-1,0):f==4?float3(0,0,1):float3(0,0,-1);
            float3 u = std::fabs(nObj.x) > 0.5f ? float3(0,1,0) : float3(1,0,0);
            float3 v = cross(nObj, u);
            float3 base = nObj * 0.5f;
            float3 op, on; PrismGravityWarpDeform_float(base, nObj, op, on);
            float3 nW = normalToWorld(on);
            float3 du = mapWorld(base + u * 0.3f, nObj) - toWorld(op);
            float3 dv = mapWorld(base + v * 0.3f, nObj) - toWorld(op);
            worst = std::max(worst, std::max(std::fabs(dot(nW, du / length(du))), std::fabs(dot(nW, dv / length(dv)))));
        }
        CHECK(worst < 1e-3f, "the output normal is not perpendicular to the stretched face (worst |cos| %.3g)", worst);
        printf("9. normal perpendicular to every stretched face (worst |cos| %.2g)\n", worst);
    }

    // 10. the ease is linear in the log
    {
        const float SMALL_K = 0.25f * RS * RS * RS;
        float3 n = placePrism(2.0f * RS); float3 c = toWorld(float3(0,0,0));
        setBank(1, float3(0,0,0), RS, SMALL_K, 1e7f, 13.8f);       float full = std::log(stretchAlong(c, n));
        setBank(1, float3(0,0,0), RS, 0.5f * SMALL_K, 1e7f, 13.8f); float half = std::log(stretchAlong(c, n));
        CHECK(std::fabs(half / full - 0.5f) < 2e-3f, "half the coefficient gave %.4f of the stretch, not 0.5", half / full);
        printf("10. half-eased: %.4f of the full log-stretch\n", half / full);
    }

    // 11. no seam at the reach
    {
        setBank(1, float3(0,0,0), RS, 1000.0f * K, REACH);
        float3 n = placePrism(RS + REACH - 0.05f);
        float justInside = stretchAlong(toWorld(float3(0,0,0)), n);
        CHECK(std::fabs(justInside - 1.0f) < 1e-3f, "just inside the reach the prism is still %.4fx long - a seam", justInside);
        printf("11. 0.05 u inside the reach the stretch is %.6fx (faded out, no seam)\n", justInside);
    }

    // 12. dominant slot
    {
        float3 U0(0,0,0), U1(1000,0,0);
        setBank(2, U0, RS, K, REACH, LN_MAX, U1, RS, K, REACH);
        setModel(float3(1.3f * RS, 0, 0), float3(0,1,0), 0.3f, float3(3,1,6));   // much nearer hole 0
        float3 c = toWorld(float3(0,0,0));
        float alongNear = stretchAlong(c, float3(1,0,0)), across = stretchAlong(c, float3(0,1,0));
        CHECK(alongNear > 1.2f && across < 1.0f, "the nearer hole did not set the stretch (along %.3f, across %.3f)", alongNear, across);
        printf("12. two holes: the nearer one's tide stretches the prism (x%.2f along, x%.2f across)\n", alongNear, across);
    }

    // 14. a WHITE HOLE (negative k, Docs/BLACK_HOLE.md §12) is the black hole's tide NEGATED: along the
    //     line to it the prism is squashed by exactly what the sink stretches it by, across it spread by
    //     what the sink squeezes — volume still conserved — and the ceiling holds in that sign too.
    {
        const float SMALL_K = 0.25f * RS * RS * RS;
        float worst = 0, worstVol = 0;
        for (int i = 0; i < 50; i++) {
            float d = rnd(1.2f, 3.0f) * RS;
            float3 n = placePrism(d);
            float3 c = toWorld(float3(0,0,0));
            float3 t = anyPerp(n);
            setBank(1, float3(0,0,0), RS, SMALL_K, REACH, 13.8f);
            float erSink = std::log(stretchAlong(c, n, 4.0f)), etSink = std::log(stretchAlong(c, t, 4.0f));
            setBank(1, float3(0,0,0), RS, -SMALL_K, REACH, 13.8f);
            float erSrc = std::log(stretchAlong(c, n, 4.0f)), etSrc = std::log(stretchAlong(c, t, 4.0f));
            worst = std::max(worst, std::max(std::fabs(erSrc + erSink), std::fabs(etSrc + etSink)) / std::fabs(erSink));
            worstVol = std::max(worstVol, std::fabs(erSrc + 2.0f * etSrc));
        }
        CHECK(worst < 3e-3f, "the white hole's tide is not the black hole's negated (worst relative %.3g)", worst);
        CHECK(worstVol < 1e-3f, "the white hole's map does not conserve volume (worst log-volume %.3g)", worstVol);
        setBank(1, float3(0,0,0), RS, -1e9f, REACH);
        float3 n = placePrism(1.1f * RS);
        float squash = stretchAlong(toWorld(float3(0,0,0)), n);
        CHECK(squash > 1.0f / 12.0f * 0.99f && squash < 1.0f, "an absurd white hole squashed past the ceiling (x%.4f)", squash);
        printf("14. white hole: the tide negated (worst %.2g), volume kept (%.2g), ceiling x%.3f >= 1/12\n", worst, worstVol, squash);
    }

    // 15. a SMOOTH well (Plummer core eps, Docs/CRYSTAL_WORMHOLE.md): the tide is k / (d^2 + eps^2)^1.5 —
    //     finite at the centre, exactly the law at any d inside the window, smooth through the core.
    {
        const float EPS = 30.0f, SMALL_K = 0.05f * EPS * EPS * EPS;
        g_soft0 = EPS;
        float worst = 0;
        const float ds[4] = { 2.0f, 15.0f, 30.0f, 45.0f };
        for (float d : ds) {
            setBank(1, float3(0,0,0), 1.0f, SMALL_K, 1e7f, 13.8f);
            float3 n = placePrism(d);
            float3 c = toWorld(float3(0,0,0));
            float er = std::log(stretchAlong(c, n, 4.0f));
            float expect = SMALL_K / std::pow(d * d + EPS * EPS, 1.5f);
            worst = std::max(worst, std::fabs(er - expect) / expect);
        }
        g_soft0 = 0.0f;
        CHECK(worst < 3e-3f, "the smooth well's tide is not k/(d^2+eps^2)^1.5 (worst relative %.3g)", worst);
        printf("15. smooth well: tide = k/(d^2+eps^2)^1.5 through the core (worst %.2g)\n", worst);
    }

    if (failures) { printf("\n%d FAILURE(S)\n", failures); return 1; }
    printf("\nall properties hold\n");
    return 0;
}
"""

# The negative control runs test 9 with the normal correction switched off via the shipped file's
# own #ifndef dial. It must FAIL — otherwise test 9 passes for some reason other than the
# inverse transpose.
CONTROL_MAIN = COMMON + r"""
int main()
{
    setBank(1, float3(0,0,0), RS, 10.0f * K, REACH);
    float worst = 0;
    for (int i = 0; i < 300; i++) {
        placePrism(rnd(RS, RS + 0.5f * REACH));
        float3 nObj(0,0,1), u(1,0,0), v(0,1,0), base(0,0,0.5f), op, on;
        PrismGravityWarpDeform_float(base, nObj, op, on);
        float3 nW = normalToWorld(on);
        float3 du = mapWorld(base + u * 0.3f, nObj) - toWorld(op);
        float3 dv = mapWorld(base + v * 0.3f, nObj) - toWorld(op);
        worst = std::max(worst, std::max(std::fabs(dot(nW, du / length(du))), std::fabs(dot(nW, dv / length(dv)))));
    }
    bool fired = worst > 0.05f;
    printf("normal correction off: worst |cos| %.3g -> %s\n", worst, fired ? "FIRES" : "did not fire");
    return fired ? 0 : 1;
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
                 "PRISM_GRAVITY_WARP_SLOTS", "PRISM_GRAVITY_WARP_NORMAL_CORRECTION"):
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

        print("\n13. negative control (normal correction switched off via -D):")
        rc = build_and_run(work, CONTROL_MAIN, ["-DPRISM_GRAVITY_WARP_NORMAL_CORRECTION=0"], "control")
        if rc is None or rc != 0:
            print("\nFAILED: the negative control did not fire — the inverse transpose is not "
                  "what makes test 9 pass", file=sys.stderr)
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
