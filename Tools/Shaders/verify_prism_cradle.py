#!/usr/bin/env python3
"""
Prove the SHIPPED PrismCradle.hlsl does what Docs/PRISM_ANIMATION.md §4.7.2 says it does, by
compiling the file itself with clang++ and RUNNING it (/asset-surgery §4.5c) — the same harness
shape as verify_prism_sight_composition.py. Nothing here is a transcription of the shader; the
file under Assets/ is translated mechanically (HLSL -> C++ spelling only) and executed under a
real non-uniform model matrix.

The cradle is a DRAPE: every vertex within the drape reach of the hull's SURFACE slides along
its own radius toward that surface. Mass inside the hull closes onto it (the wrap); mass just
outside rises to meet it (the lip); mass past the reach is perfectly still.

What it proves, each on randomized inputs:

  1. IDENTITY with no live slot (count 0): bit-identical pass-through. "A match with no Urchin
     in it costs nothing and changes nothing."
  2. IDENTITY beyond the reach: a vertex farther than R + drapeReach from the hull centre is
     bit-identical.
  3. THE WRAP at full weight: a vertex INSIDE the hull comes out exactly on its surface
     (|p' - U| = R) and on the OUTWARD radial from U through p — it is pushed out onto the
     sphere, never dragged across it.
  4. THE LIP: a vertex outside the hull moves TOWARD it, never past the surface and never
     outward — and the map never FOLDS (f is strictly increasing in d, so two vertices at
     different radii keep their order; a fold would turn the prism inside out).
  5. RADIAL PURITY: the displacement is exactly along the vertex's own radius from U, so the
     vertex's DIRECTION from the hull is bit-preserved. This is what makes the analytic
     normal below derivable at all.
  6. THE NORMAL IS THE MAP'S DERIVATIVE: a CONVERGENCE test, not a tolerance. Take a tiny
     triangle in a face's plane, deform its three corners with the face normal exactly as the
     mesh does, and compare the GEOMETRIC normal of the moved triangle against the normal the
     shader returns at the centroid — inside the hull, in the lip, and across the far edge.
     The claim is not "these agree to 1e-6": a flat patch of ANY size disagrees with the true
     surface normal at second order in its size, so a single tolerance only ever measures which
     patch size was chosen. The claim is that HALVING the patch QUARTERS the error, which is the
     signature of an exact first derivative and nothing else — a wrong Jacobian converges to a
     nonzero constant (test 10 shows exactly that). This is the property the rejected "lerp
     toward the sphere normal" shortcut fails, and it is why the shipped file inverts the
     Jacobian instead.
  7. NO SEAM AT THE FAR EDGE: sweeping a vertex through s = drapeReach, neither the position
     nor the normal jumps. The falloff is C1 at both ends by construction; this measures it.
  8. PARTIAL WEIGHT: at strength 0.5 an interior vertex travels half-way to the surface, and
     the whole map is linear in the weight at fixed geometry — the eased engage/release is a
     blend of the MAP, so the drape is never a differently-shaped drape.
  9. DOMINANT SLOT: with two hulls live, a vertex is draped by the one with the greater
     authority (k x w), and only by that one.
 10. NEGATIVE CONTROL: rebuilt with the Jacobian's RADIAL term neutered (-D override of the
     file's own #ifndef dial), test 6's error STOPS CONVERGING — it plateaus instead of
     quartering — so the analytic normal is what holds it, not luck.

Exit 0 on pass. Needs clang++; nothing else, and no Unity.
Usage:  python3 Tools/Shaders/verify_prism_cradle.py [--keep]
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HLSL = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs/PrismCradle.hlsl")

SHIM = r"""// Minimal HLSL->C++ shim so the SHIPPED PrismCradle.hlsl can be compiled and executed by
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
using std::atan2;
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

HARNESS = r"""#include "shipped.h"
#include <cstdio>
#include <cstdlib>
#include <random>
#include <vector>

float4x4 g_objectToWorld, g_worldToObject;

static int failures = 0;
#define CHECK(cond, ...) do { if (!(cond)) { failures++; printf("FAIL: "); printf(__VA_ARGS__); printf("\n"); } } while (0)

static std::mt19937 rng(20260922);
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
static float3 toWorld(float3 p){ return mul(g_objectToWorld, float4(p,1)).xyz(); }
static float3 toObject(float3 p){ return mul(g_worldToObject, float4(p,1)).xyz(); }
static float3 xformDir(float3 d){ return mul(g_objectToWorld, float4(d,0)).xyz(); }
static float3 normalToWorld(float3 n){ float3 w = mul(n, (float3x3)g_worldToObject); return w / length(w); }

static const float REACH = 6.0f;
static const float EXPO  = 1.5f;

static void setBank(int count, float3 U0, float R0, float S0,
                    float3 U1 = float3(0,0,0), float R1 = 0, float S1 = 0,
                    float reach = REACH, float expo = EXPO) {
    _PrismCradleCentre[0] = float4(U0, R0); _PrismCradleWeight[0] = float4(S0,0,0,0);
    _PrismCradleCentre[1] = float4(U1, R1); _PrismCradleWeight[1] = float4(S1,0,0,0);
    _PrismCradleCentre[2] = float4(0,0,0,0); _PrismCradleWeight[2] = float4(0,0,0,0);
    _PrismCradleCentre[3] = float4(0,0,0,0); _PrismCradleWeight[3] = float4(0,0,0,0);
    _PrismCradleParams = float4(reach, expo, (float)count, 0.0f);
}

// A vertex on one of the prism's six faces, carrying that face's flat normal — the only two
// things the drape reads, on any mesh.
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
// A grid of vertices over all six faces — the high-poly prism's shape, at whatever density the
// caller asks for. HighPolyPrismMesh builds exactly this; the drape reads only position and
// normal, so the density here is a sampling choice and not a claim about the shipped mesh.
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
    PrismCradleDeform_float(x.pObj, x.nObj, r.pObj, r.nObj);
    r.pW = toWorld(r.pObj);
    r.nW = normalToWorld(r.nObj);
    return r;
}
static bool identical(const Vtx& x, const Out& r) {
    return r.pObj.x == x.pObj.x && r.pObj.y == x.pObj.y && r.pObj.z == x.pObj.z
        && r.nObj.x == x.nObj.x && r.nObj.y == x.nObj.y && r.nObj.z == x.nObj.z;
}

int main()
{
    const float3 SCALE(3, 1, 6);   // a trail slab
    const float R = 6.7f;          // the Urchin's measured circumscribing radius

    // ---------------- 1. identity with no live slot ----------------
    {
        int bad = 0, tested = 0;
        auto vs = prismVerts(3);
        for (int trial = 0; trial < 150; trial++) {
            setModel(float3(rnd(-50,50),rnd(-50,50),rnd(-50,50)), rndDir(), rnd(0, 6.28f), SCALE);
            setBank(0, float3(rnd(-5,5),rnd(-5,5),rnd(-5,5)), R, 1);
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
            // Put the hull far enough that every vertex of the slab is past R + reach.
            float3 U = rndDir() * rnd(R + REACH + 8.0f, 60.0f);
            setBank(1, U, R, 1);
            for (const Vtx& x : vs) {
                if (length(toWorld(x.pObj) - U) < R + REACH) continue;   // the test's own premise
                tested++;
                if (!identical(x, run(x))) bad++;
            }
        }
        CHECK(tested > 2000, "too few beyond-reach samples (%d)", tested);
        CHECK(bad == 0, "identity beyond the reach broken on %d of %d vertices", bad, tested);
        printf("2. vertex past R + drapeReach -> untouched: %s (%d vertices)\n", bad == 0 ? "ok" : "BROKEN", tested);
    }

    // ---------------- 3. the wrap: inside lands ON the surface, outward ----------------
    {
        float worstSurface = 0, worstLine = 0; int tested = 0, backwards = 0;
        auto vs = prismVerts(4);
        for (int trial = 0; trial < 300; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            // Hull centred ON the slab, so a lot of it is inside the sphere.
            float3 U = toWorld(float3(rnd(-0.3f,0.3f), rnd(-0.3f,0.3f), rnd(-0.3f,0.3f)));
            setBank(1, U, R, 1);
            for (const Vtx& x : vs) {
                float3 pw = toWorld(x.pObj);
                float d = length(pw - U);
                if (!(d > 0.5f) || d >= R - 0.05f) continue;    // strictly inside
                Out r = run(x);
                tested++;
                worstSurface = std::max(worstSurface, std::fabs(length(r.pW - U) - R));
                // on the OUTWARD radial: same direction from U as it started
                float3 d0 = (pw - U) / d, d1 = (r.pW - U) / length(r.pW - U);
                worstLine = std::max(worstLine, 1.0f - dot(d0, d1));
                if (length(r.pW - U) < d) backwards++;
            }
        }
        CHECK(tested > 1000, "too few interior samples (%d)", tested);
        CHECK(worstSurface < 2e-3f, "interior vertex not on the hull surface: worst |d - R| = %g", worstSurface);
        CHECK(worstLine < 1e-5f, "interior vertex left its own radial: worst 1-dot = %g", worstLine);
        CHECK(backwards == 0, "%d interior vertices moved INWARD (the wrap must push them out)", backwards);
        printf("3. wrap (%d interior vertices): on the surface (|d-R| %.2e), on the outward radial (1-dot %.2e)\n",
               tested, worstSurface, worstLine);
    }

    // ---------------- 4. the lip: toward, never past, never folding ----------------
    {
        int tested = 0, outward = 0, past = 0, folded = 0;
        float worstRise = 0;
        for (int trial = 0; trial < 400; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            float3 U = toWorld(rndDir() * 0.4f);
            setBank(1, U, R, 1);
            float3 dir = rndDir();
            // Walk a ray outward through the lip and check monotonicity of the image.
            float prevF = -1e30f;
            for (float s = -0.5f; s <= REACH + 1.0f; s += 0.05f) {
                float d = R + s;
                float3 pw = U + dir * d;
                Vtx x; x.pObj = toObject(pw); x.nObj = float3(0,1,0);
                Out r = run(x);
                float f = length(r.pW - U);
                if (s > 0.01f) {
                    tested++;
                    if (f > d + 1e-3f) outward++;                  // must move toward the hull
                    if (f < R - 1e-3f) past++;                     // must never cross the surface
                    worstRise = std::max(worstRise, d - f);
                }
                if (f < prevF - 1e-3f) folded++;                   // f must be non-decreasing in d
                prevF = f;
            }
        }
        CHECK(tested > 20000, "too few lip samples (%d)", tested);
        CHECK(outward == 0, "%d exterior vertices moved AWAY from the hull", outward);
        CHECK(past == 0, "%d exterior vertices were pulled INSIDE the hull surface", past);
        CHECK(folded == 0, "the map FOLDS in %d places (f not increasing in d — the prism turns inside out)", folded);
        printf("4. lip (%d samples): always toward the hull, never past the surface, never folding; deepest rise %.3f u\n",
               tested, worstRise);
    }

    // ---------------- 5. radial purity ----------------
    {
        float worstDrift = 0; int tested = 0;
        auto vs = prismVerts(4);
        for (int trial = 0; trial < 200; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            float3 U = toWorld(rndDir() * rnd(0.0f, 0.8f));
            setBank(1, U, R, rnd(0.2f, 1.0f));
            for (const Vtx& x : vs) {
                float3 pw = toWorld(x.pObj);
                float d = length(pw - U);
                if (!(d > 0.5f) || d > R + REACH) continue;
                Out r = run(x);
                tested++;
                float3 d0 = (pw - U) / d, d1 = (r.pW - U) / length(r.pW - U);
                worstDrift = std::max(worstDrift, 1.0f - dot(d0, d1));
            }
        }
        CHECK(tested > 2000, "too few radial-purity samples (%d)", tested);
        CHECK(worstDrift < 1e-5f, "displacement is not radial: worst direction drift 1-dot = %g", worstDrift);
        printf("5. radial purity (%d samples): direction from the hull bit-preserved (worst 1-dot %.2e)\n",
               tested, worstDrift);
    }

    // ---------------- 6. the normal is the map's DERIVATIVE ----------------
    {
        // A tiny triangle in the face's own plane, deformed exactly as the mesh would deform it
        // (all three corners carrying the face normal), against the normal the shader returns at
        // its centroid. The patch is sized as a FRACTION OF d rather than absolutely, because the
        // map's curvature scale is the vertex's own distance from the hull; a fixed patch is
        // coarse deep inside and fine far out, which measures the choice rather than the shader.
        //
        // The assertion is the CONVERGENCE RATE. Halving the patch must quarter the error — the
        // signature of an exact first derivative. Test 10 runs this same sweep with the radial
        // Jacobian term neutered and it plateaus, which is what makes this a proof rather than a
        // tolerance somebody picked.
        const int LEVELS = 4;
        const float FRACS[LEVELS] = { 0.02f, 0.01f, 0.005f, 0.0025f };
        float worst[LEVELS] = { 0, 0, 0, 0 };
        int tested = 0, collapsed = 0;
        for (int trial = 0; trial < 4000; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            int f = rng() % 6;
            float3 nrm = faceNormal(f), u, v; faceAxes(f, u, v);
            float3 c = nrm * 0.5f + u * rnd(-0.45f, 0.45f) + v * rnd(-0.45f, 0.45f);
            float3 cw = toWorld(c);
            // Hull somewhere that puts this patch inside, in the lip, or past the edge.
            float3 U = cw + rndDir() * rnd(0.5f, R + REACH + 2.0f);
            setBank(1, U, R, rnd(0.3f, 1.0f));

            Vtx mid; mid.pObj = c; mid.nObj = nrm;
            Out rm = run(mid);
            if (identical(mid, rm)) continue;         // untouched: nothing to check

            float d = length(cw - U);
            // The object-space offset that yields the wanted WORLD patch size on this face,
            // under this (non-uniform) model scale.
            float unit = std::max(length(xformDir(u)), length(xformDir(v)));

            float e[LEVELS]; bool usable = true;
            for (int k = 0; k < LEVELS && usable; k++) {
                float eps = FRACS[k] * d / unit;
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
                // A patch the map has COLLAPSED (a face plane containing the radial direction,
                // squashed onto the sphere) has no normal for either side of this test to be
                // right about. It also draws nothing. Skip it and say how often.
                if (!(gl > 1e-16f) || !(gl0 > 1e-16f) || gl / gl0 < 0.01f) { usable = false; break; }
                // cross(u, v) == n, and the corners are laid out counter-clockwise in (u, v), so
                // g points the same way as the face normal before any deformation.
                e[k] = 1.0f - dot(g / gl, rm.nW);
            }
            if (!usable) { collapsed++; continue; }
            tested++;
            for (int k = 0; k < LEVELS; k++) worst[k] = std::max(worst[k], e[k]);
        }
        CHECK(tested > 2000, "too few differential-normal trials (%d)", tested);
        CHECK(worst[LEVELS-1] < 0.02f, "the returned normal does not approach the deformed surface: "
              "worst 1-dot at the finest patch = %g", worst[LEVELS-1]);
        for (int k = 1; k < LEVELS; k++)
            CHECK(worst[k] < 0.40f * worst[k-1],
                  "halving the patch did not quarter the error (%g -> %g) — the returned normal is "
                  "not the map's derivative", worst[k-1], worst[k]);
        printf("6. analytic normal == d(map) (%d trials, %d collapsed patches skipped): worst 1-dot",
               tested, collapsed);
        for (int k = 0; k < LEVELS; k++) printf("  %.4gxd:%.3g", FRACS[k], worst[k]);
        printf("  (quartering => exact derivative)\n");
    }

    // ---------------- 7. no seam at the far edge ----------------
    {
        setModel(float3(0,0,0), float3(0,0,1), 0.3f, SCALE);
        float3 U = toWorld(float3(0,0,0));
        setBank(1, U, R, 1);
        float3 dir = normalToWorld(float3(0,1,0));
        const float step = 0.002f;
        float worstPos = 0, worstNrm = 0;
        float3 prevP(0,0,0), prevN(0,0,0); bool have = false;
        for (float s = REACH - 1.0f; s <= REACH + 1.0f; s += step) {
            float3 pw = U + dir * (R + s);
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
        printf("7. no seam at s = drapeReach: worst excess position jump %.2e u, worst normal 1-dot %.2e\n",
               worstPos, worstNrm);
    }

    // ---------------- 8. partial weight blends the MAP ----------------
    {
        float worstHalf = 0, worstLinear = 0; int tested = 0;
        for (int trial = 0; trial < 300; trial++) {
            setModel(float3(rnd(-10,10),rnd(-10,10),rnd(-10,10)), rndDir(), rnd(0, 6.28f), SCALE);
            float3 U = toWorld(rndDir() * 0.3f);
            float3 dir = rndDir();
            float d = rnd(1.0f, R - 0.2f);                       // strictly inside
            float3 pw = U + dir * d;
            Vtx x; x.pObj = toObject(pw); x.nObj = float3(0,1,0);

            setBank(1, U, R, 0.5f);
            float half = length(run(x).pW - U);
            tested++;
            worstHalf = std::max(worstHalf, std::fabs(half - (d + 0.5f * (R - d))));

            // linear in w at fixed geometry: f(w) = d - s*k*w is affine in w
            setBank(1, U, R, 1.0f);  float f1 = length(run(x).pW - U);
            setBank(1, U, R, 0.25f); float fq = length(run(x).pW - U);
            worstLinear = std::max(worstLinear, std::fabs((fq - d) * 4.0f - (f1 - d)));
        }
        CHECK(worstHalf < 2e-3f, "strength 0.5: interior vertex not half-way to the surface (worst err %g)", worstHalf);
        CHECK(worstLinear < 2e-3f, "the map is not linear in the weight (worst err %g)", worstLinear);
        printf("8. partial weight (%d trials): half strength is half the travel (%.2e), map affine in w (%.2e)\n",
               tested, worstHalf, worstLinear);
    }

    // ---------------- 9. dominant slot ----------------
    {
        setModel(float3(0,0,0), float3(0,1,0), 0, SCALE);
        float3 pw = toWorld(float3(0, 0.5f, 0));
        Vtx x; x.pObj = toObject(pw); x.nObj = float3(0,1,0);
        // Hull A close (full authority), hull B farther out in the lip: A must win.
        float3 A = pw + float3(0, R - 1.0f, 0);
        float3 B = pw - float3(0, R + REACH - 0.5f, 0);
        setBank(2, B, R, 1, A, R, 1);
        Out r = run(x);
        CHECK(std::fabs(length(r.pW - A) - R) < 2e-3f, "dominant slot: the vertex did not land on the NEAR hull's surface");
        // Same pair, but the near hull is faded almost out: the far one should take over.
        setBank(2, B, R, 1, A, R, 0.02f);
        Out r2 = run(x);
        CHECK(length(r2.pW - r.pW) > 1e-3f, "dominant slot: authority (k x w) is not what selects the slot");
        printf("9. two hulls live: the greater authority (k x w) drapes the vertex, and only it\n");
    }

    if (failures) { printf("\n%d FAILURE(S)\n", failures); return 1; }
    printf("\nall properties hold\n");
    return 0;
}
"""

# The negative control drives the SAME differential-normal test with the Jacobian's RADIAL term
# neutered via the shipped file's own #ifndef dial: clamping the radial stretch to >= 1 leaves
# only the tangential correction, which is what "lerp the normal toward something plausible"
# amounts to. It must BREAK test 6 — otherwise that test was passing by luck.
CONTROL_MAIN = r"""#include "shipped.h"
#include <cstdio>
#include <cmath>
#include <algorithm>
#include <random>
float4x4 g_objectToWorld, g_worldToObject;
static std::mt19937 rng(20260922);
static float rnd(float a, float b) { return a + (b - a) * (rng() / (float)rng.max()); }
static float3 rndDir(){for(;;){float3 v(rnd(-1,1),rnd(-1,1),rnd(-1,1));float l=length(v);if(l>0.1f)return v/l;}}
int main()
{
    // Identity model: the control is about the Jacobian, not the transform stack.
    for(int i=0;i<4;i++)for(int j=0;j<4;j++){ g_objectToWorld.m[i][j]=(i==j); g_worldToObject.m[i][j]=(i==j); }
    const float R = 6.7f, REACH = 6.0f;
    const int LEVELS = 4;
    const float FRACS[LEVELS] = { 0.02f, 0.01f, 0.005f, 0.0025f };
    float worst[LEVELS] = { 0, 0, 0, 0 };
    int tested = 0;
    for (int trial = 0; trial < 3000; trial++) {
        float3 n(0,1,0), u(0,0,1), v(1,0,0);           // cross(u, v) == n
        float3 c = n * 0.5f + u * rnd(-0.4f, 0.4f) + v * rnd(-0.4f, 0.4f);
        float3 U = c + rndDir() * rnd(0.5f, R + REACH + 2.0f);
        _PrismCradleCentre[0] = float4(U, R); _PrismCradleWeight[0] = float4(rnd(0.3f,1.0f),0,0,0);
        for (int i = 1; i < 4; i++) { _PrismCradleCentre[i] = float4(0,0,0,0); _PrismCradleWeight[i] = float4(0,0,0,0); }
        _PrismCradleParams = float4(REACH, 1.5f, 1, 0);
        float3 mp, mn; PrismCradleDeform_float(c, n, mp, mn);
        if (mp.x == c.x && mp.y == c.y && mp.z == c.z) continue;
        float mnl = length(mn); if (!(mnl > 1e-9f)) continue;
        float3 nW = mn / mnl;
        float d = length(c - U);
        float e[LEVELS]; bool usable = true;
        for (int k = 0; k < LEVELS && usable; k++) {
            float eps = FRACS[k] * d;
            float3 moved[3], flat[3];
            for (int i = 0; i < 3; i++) {
                float a = 2.0944f * i;
                float3 p = c + u * (eps * std::cos(a)) + v * (eps * std::sin(a));
                flat[i] = p;
                float3 op, on; PrismCradleDeform_float(p, n, op, on);
                moved[i] = op;
            }
            float3 g = cross(moved[1] - moved[0], moved[2] - moved[0]);
            float3 g0 = cross(flat[1] - flat[0], flat[2] - flat[0]);
            float gl = length(g), gl0 = length(g0);
            if (!(gl > 1e-16f) || !(gl0 > 1e-16f) || gl / gl0 < 0.01f) { usable = false; break; }
            e[k] = 1.0f - dot(g / gl, nW);
        }
        if (!usable) continue;
        tested++;
        for (int k = 0; k < LEVELS; k++) worst[k] = std::max(worst[k], e[k]);
    }
    printf("radial Jacobian neutered, %d trials: worst 1-dot", tested);
    for (int k = 0; k < LEVELS; k++) printf("  %.4gxd:%.3g", FRACS[k], worst[k]);
    // The control FIRES when the error refuses to converge: the finest patch must still be
    // grossly wrong, and halving must NOT have quartered it.
    bool plateaus = worst[LEVELS-1] > 0.05f && worst[LEVELS-1] > 0.40f * worst[LEVELS-2];
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
    assert "void PrismCradleDeform_float(" in out, "entry point missing"
    assert "float3 Position, float3 Normal," in out, \
        "the entry point's signature is not (Position, Normal) — the harness and the wirer disagree"
    for name in ("_PrismCradleCentre", "_PrismCradleWeight", "_PrismCradleParams",
                 "PRISM_CRADLE_SLOTS", "PRISM_CRADLE_MIN_RADIAL"):
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

    work = tempfile.mkdtemp(prefix="prism_cradle_verify_")
    try:
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(SHIM)
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write('#include "shim.h"\n' + translate(open(HLSL).read()))

        rc = build_and_run(work, HARNESS, [], "verify")
        if rc is None or rc != 0:
            print("\nFAILED", file=sys.stderr)
            return 1

        print("\n10. negative control (radial Jacobian term neutered via -D):")
        rc = build_and_run(work, CONTROL_MAIN, ["-DPRISM_CRADLE_MIN_RADIAL=1.0"], "control")
        if rc is None or rc != 0:
            print("\nFAILED: the negative control did not fire — the analytic normal's radial "
                  "term is not what makes test 6 pass", file=sys.stderr)
            return 1

        print("\nAll cradle properties hold for the shipped file.")
        return 0
    finally:
        if keep:
            print("kept: " + work)
        else:
            shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    sys.exit(main())
