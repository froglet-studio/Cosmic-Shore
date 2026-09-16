#!/usr/bin/env python3
"""
Prove the SHIPPED PrismCradle.hlsl does what Docs/PRISM_ANIMATION.md §4.7.2 says it does,
by compiling the file itself with clang++ and RUNNING it (/asset-surgery §4.5c) — the same
harness shape as verify_prism_sight_composition.py. Nothing here is a transcription of the
shader; the file under Assets/ is translated mechanically (HLSL -> C++ spelling only) and
executed against a real cube mesh under a real non-uniform model matrix.

What it proves, each on randomized inputs:

  1. IDENTITY with no live slot (count 0): bit-identical pass-through. This is "a match with
     no Urchin in it costs nothing and changes nothing".
  2. IDENTITY outside the band: a face whose centroid is past outerRange is untouched.
  3. THE CONTRACT at full weight: a face inside innerRange, facing the hull, comes out with
     (a) its centroid ON the hull's surface, on the line from its old centroid to the hull's
         centre — the "exact position" clause, which is why the radius is a uniform;
     (b) its normal pointing AT the hull's centre;
     (c) every pairwise vertex distance preserved (rigid), measured in WORLD space under a
         (3, 1, 6) scale — a rigid motion is only rigid in an isotropic frame;
     (d) the OUTPUT normal, carried back to world, equal to the geometric normal of the moved
         face — the inverse-transpose bookkeeping is checked, not assumed.
  4. THE FACING GATE: the face on the far side of the prism (normal pointing away from the
     hull) is untouched, so the 180-degree flip with no defined axis never happens.
  5. THE HAND-OFF: with the hull over an EDGE, both adjacent faces land on the surface at
     once — two triangles touching the sphere as one hands off to the other.
  6. CONTINUITY: sweeping the hull along the ribbon and into the band in small steps, no
     vertex jumps by more than a bounded multiple of the step. This is the "no snap" claim,
     measured, including across the seam and across the band's outer edge.
  7. PARTIAL WEIGHT: at strength 0.5 the centroid is half-way and the rotation half-angle,
     i.e. the blend is of the rigid MOTION, not of the vertices.
  8. NEAREST SLOT: with two hulls live, a face uses the nearer one.
  9. NEGATIVE CONTROL: rebuilt with the facing gate disabled (-D overrides of the file's own
     #ifndef dials), the far face DOES move — so the gate is what holds it, not luck.

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
static inline float smoothstep(float e0,float e1,float x){float t=saturate((x-e0)/(e1-e0));return t*t*(3.0f-2.0f*t);}
static inline float3 lerp(float3 a,float3 b,float t){return a+(b-a)*t;}
static inline void sincos(float a,float&s,float&c){s=std::sin(a);c=std::cos(a);}
using std::atan2;

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

MAIN = r"""#include "shipped.h"
#include <cstdio>
#include <cstdlib>
#include <random>
#include <vector>

float4x4 g_objectToWorld, g_worldToObject;

static int failures = 0;
#define CHECK(cond, ...) do { if (!(cond)) { failures++; printf("FAIL: "); printf(__VA_ARGS__); printf("\n"); } } while (0)

static std::mt19937 rng(20260916);
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
    // inverse: S^-1 R^T (p - t)
    for(int i=0;i<3;i++){ for(int j=0;j<3;j++){ Mi.m[i][j] = R[j][i] / (&s.x)[i]; } }
    for(int i=0;i<3;i++){ float acc=0; for(int j=0;j<3;j++) acc += Mi.m[i][j]*(&t.x)[j]; Mi.m[i][3] = -acc; }
    Mi.m[3][0]=Mi.m[3][1]=Mi.m[3][2]=0; Mi.m[3][3]=1;
    g_objectToWorld = M; g_worldToObject = Mi;
}
static float3 toWorld(float3 p){ return mul(g_objectToWorld, float4(p,1)).xyz(); }
static float3 normalToWorld(float3 n){ float3 w = mul(n, (float3x3)g_worldToObject); return w / length(w); }

// ---- the built-in cube: 6 faces x 4 verts, hard-edged (per-face normals) ----
struct Face { float3 n; float3 v[4]; };
static std::vector<Face> cube() {
    std::vector<Face> f;
    float3 axes[3] = { float3(1,0,0), float3(0,1,0), float3(0,0,1) };
    for (int a = 0; a < 3; a++) for (int sgn = -1; sgn <= 1; sgn += 2) {
        float3 n = axes[a] * (float)sgn;
        float3 u = axes[(a+1)%3], w = axes[(a+2)%3];
        Face fc; fc.n = n;
        // wound so that cross(v1-v0, v2-v0) points along n
        float3 c = n * 0.5f;
        float3 q[4] = { c - u*0.5f - w*0.5f, c + u*0.5f - w*0.5f, c + u*0.5f + w*0.5f, c - u*0.5f + w*0.5f };
        float3 gn = cross(q[1]-q[0], q[2]-q[0]);
        if (dot(gn, n) < 0) { std::swap(q[1], q[3]); }
        for (int i = 0; i < 4; i++) fc.v[i] = q[i];
        f.push_back(fc);
    }
    return f;
}

static void setBank(int count, float3 U0, float R0, float S0, float3 U1 = float3(0,0,0), float R1 = 0, float S1 = 0) {
    _PrismCradleCentre[0] = float4(U0, R0); _PrismCradleWeight[0] = float4(S0,0,0,0);
    _PrismCradleCentre[1] = float4(U1, R1); _PrismCradleWeight[1] = float4(S1,0,0,0);
    _PrismCradleCentre[2] = float4(0,0,0,0); _PrismCradleWeight[2] = float4(0,0,0,0);
    _PrismCradleCentre[3] = float4(0,0,0,0); _PrismCradleWeight[3] = float4(0,0,0,0);
    _PrismCradleParams = float4(15.0f, 10.0f, (float)count, 0);
}

struct MovedFace { float3 pw[4]; float3 nw; float3 cw; float3 nObj[4]; float3 pObj[4]; };
static MovedFace run(const Face& f) {
    MovedFace r; r.cw = float3(0,0,0);
    for (int i = 0; i < 4; i++) {
        PrismCradleDeform_float(f.v[i], f.n, r.pObj[i], r.nObj[i]);
        r.pw[i] = toWorld(r.pObj[i]);
        r.cw = r.cw + r.pw[i] * 0.25f;
    }
    r.nw = normalToWorld(r.nObj[0]);
    return r;
}
static float3 faceCentroidWorld(const Face& f) { float3 c(0,0,0); for(int i=0;i<4;i++) c = c + toWorld(f.v[i]) * 0.25f; return c; }
static bool identical(const Face& f, const MovedFace& r) {
    for (int i = 0; i < 4; i++) {
        if (r.pObj[i].x != f.v[i].x || r.pObj[i].y != f.v[i].y || r.pObj[i].z != f.v[i].z) return false;
        if (r.nObj[i].x != f.n.x || r.nObj[i].y != f.n.y || r.nObj[i].z != f.n.z) return false;
    }
    return true;
}
static float maxPairDistanceError(const Face& f, const MovedFace& r) {
    float e = 0;
    for (int i = 0; i < 4; i++) for (int j = i+1; j < 4; j++) {
        float d0 = length(toWorld(f.v[i]) - toWorld(f.v[j]));
        float d1 = length(r.pw[i] - r.pw[j]);
        e = std::max(e, std::fabs(d0 - d1));
    }
    return e;
}
static float3 geometricNormal(const MovedFace& r) { float3 n = cross(r.pw[1]-r.pw[0], r.pw[2]-r.pw[0]); return n / length(n); }

int main()
{
    const float3 SCALE(3, 1, 6);   // a trail slab
    const float R = 2.0f;          // the hull radius

    // ---------------- 1. identity with no live slot ----------------
    {
        int bad = 0;
        for (int trial = 0; trial < 200; trial++) {
            setModel(float3(rnd(-50,50),rnd(-50,50),rnd(-50,50)), rndDir(), rnd(0, 6.28f), SCALE);
            setBank(0, float3(rnd(-5,5),rnd(-5,5),rnd(-5,5)), R, 1);
            for (const Face& f : cube()) if (!identical(f, run(f))) bad++;
        }
        CHECK(bad == 0, "identity with count 0 broken on %d faces", bad);
        printf("1. count 0 -> bit-identical pass-through: %s\n", bad == 0 ? "ok" : "BROKEN");
    }

    // ---------------- 2. identity outside the band ----------------
    {
        int bad = 0, tested = 0;
        for (int trial = 0; trial < 200; trial++) {
            setModel(float3(0,0,0), rndDir(), rnd(0, 6.28f), SCALE);
            float3 U = rndDir() * rnd(30, 80);
            setBank(1, U, R, 1);
            for (const Face& f : cube()) { tested++; if (!identical(f, run(f))) bad++; }
        }
        CHECK(bad == 0, "identity outside the band broken on %d of %d faces", bad, tested);
        printf("2. hull 30-80 u away -> untouched: %s (%d faces)\n", bad == 0 ? "ok" : "BROKEN", tested);
    }

    // ---------------- 3. the contract at full weight ----------------
    {
        float worstSurface = 0, worstNormal = 0, worstRigid = 0, worstGeom = 0, worstLine = 0;
        int tested = 0;
        for (int trial = 0; trial < 300; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            auto faces = cube();
            // pick a face, put the hull in front of it, inside innerRange, facing it
            const Face& f = faces[rng() % 6];
            float3 cw = faceCentroidWorld(f);
            float3 nw = normalToWorld(f.n);
            float3 off = nw * rnd(3, 8) + rndDir() * rnd(0, 2);   // in front, a little off-axis
            float3 U = cw + off;
            if (dot(nw, (U - cw) / length(U - cw)) < 0.2f) continue; // keep it clearly facing
            setBank(1, U, R, 1);
            MovedFace r = run(f);
            tested++;
            float3 nT = (U - cw) / length(U - cw);
            float3 cT = U - nT * R;
            worstSurface = std::max(worstSurface, std::fabs(length(r.cw - U) - R));
            worstLine = std::max(worstLine, length(r.cw - cT));
            worstNormal = std::max(worstNormal, 1.0f - dot(r.nw, nT));
            worstRigid = std::max(worstRigid, maxPairDistanceError(f, r));
            worstGeom = std::max(worstGeom, 1.0f - dot(geometricNormal(r), r.nw));
        }
        CHECK(tested > 200, "too few contract trials (%d)", tested);
        CHECK(worstSurface < 1e-3f, "centroid not on the hull surface: worst |d - R| = %g", worstSurface);
        CHECK(worstLine < 1e-3f, "centroid not on the line to the hull centre: worst = %g", worstLine);
        CHECK(worstNormal < 1e-5f, "normal not pointing at the hull centre: worst 1-dot = %g", worstNormal);
        CHECK(worstRigid < 1e-3f, "face not rigid: worst pair-distance error = %g", worstRigid);
        CHECK(worstGeom < 1e-4f, "output normal disagrees with the moved face's geometry: 1-dot = %g", worstGeom);
        printf("3. full weight (%d trials): centroid on surface (|d-R| %.2e), on the line (%.2e), normal at centre (1-dot %.2e), rigid (%.2e), normal matches geometry (%.2e)\n",
               tested, worstSurface, worstLine, worstNormal, worstRigid, worstGeom);
    }

    // ---------------- 4. the facing gate ----------------
    {
        int bad = 0, tested = 0;
        for (int trial = 0; trial < 300; trial++) {
            setModel(float3(0,0,0), rndDir(), rnd(0, 6.28f), SCALE);
            auto faces = cube();
            const Face& f = faces[rng() % 6];
            float3 cw = faceCentroidWorld(f);
            float3 nw = normalToWorld(f.n);
            // hull BEHIND the face, within the band, within the gate's dead zone (dot <= -0.5)
            float3 U = cw - nw * rnd(2, 6);
            float3 d = (U - cw) / length(U - cw);
            if (dot(nw, d) > -0.55f) continue;
            setBank(1, U, R, 1);
            tested++;
            if (!identical(f, run(f))) bad++;
        }
        CHECK(tested > 100 && bad == 0, "facing gate: %d of %d far faces moved", bad, tested);
        printf("4. far face (normal away from hull) untouched: %s (%d trials)\n", bad == 0 ? "ok" : "BROKEN", tested);
    }

    // ---------------- 5. the hand-off at an edge ----------------
    {
        setModel(float3(0,0,0), float3(0,1,0), 0, SCALE);
        auto faces = cube();
        // world half-extents (3, 0.5, 3): the +x face centroid is (3,0,0), the +y face's (0,0.5,0)
        float3 edge(3, 0.5f, 0);
        float3 diag = float3(1, 1, 0) / std::sqrt(2.0f);
        float3 U = edge + diag * (R + 0.5f);   // hull resting on the edge
        setBank(1, U, R, 1);
        int onSurface = 0;
        for (const Face& f : faces) {
            MovedFace r = run(f);
            bool moved = !identical(f, r);
            bool on = std::fabs(length(r.cw - U) - R) < 1e-3f;
            if (moved && on) onSurface++;
        }
        CHECK(onSurface == 2, "hand-off: expected exactly 2 faces on the surface at the edge, got %d", onSurface);
        printf("5. hull over the +x/+y edge: %d faces touching the surface at once (want 2)\n", onSurface);
    }

    // ---------------- 6. continuity ----------------
    {
        setModel(float3(0,0,0), float3(0,0,1), 0.3f, SCALE);
        auto faces = cube();
        const float step = 0.01f;
        float worstJump = 0, worstJumpBand = 0;
        // (a) along the slab, over the top edge and off the side, hull at hull-radius height
        {
            std::vector<MovedFace> prev;
            for (float x = -8; x <= 8; x += step) {
                float3 U = toWorld(float3(x / SCALE.x, 0.5f, 0)) + normalToWorld(float3(0,1,0)) * R;
                setBank(1, U, R, 1);
                std::vector<MovedFace> cur; for (const Face& f : faces) cur.push_back(run(f));
                if (!prev.empty()) for (size_t k = 0; k < cur.size(); k++) for (int i = 0; i < 4; i++)
                    worstJump = std::max(worstJump, length(cur[k].pw[i] - prev[k].pw[i]));
                prev = cur;
            }
        }
        // (b) straight in through the band's outer edge, and out again
        {
            std::vector<MovedFace> prev;
            float3 nw = normalToWorld(float3(0,1,0)); float3 cw = faceCentroidWorld(faces[3]);
            for (float h = 20; h >= 2; h -= step) {
                setBank(1, cw + nw * h + float3(0.3f, 0, 0.2f), R, 1);
                std::vector<MovedFace> cur; for (const Face& f : faces) cur.push_back(run(f));
                if (!prev.empty()) for (size_t k = 0; k < cur.size(); k++) for (int i = 0; i < 4; i++)
                    worstJumpBand = std::max(worstJumpBand, length(cur[k].pw[i] - prev[k].pw[i]));
                prev = cur;
            }
        }
        // A face can legitimately move faster than the hull (it swings about its centroid), so
        // the bound is a multiple of the step; a SNAP would be tens of units.
        CHECK(worstJump < 30 * step, "discontinuity along the ribbon: a vertex jumped %g u for a %g u hull step", worstJump, step);
        CHECK(worstJumpBand < 30 * step, "discontinuity crossing the band: a vertex jumped %g u for a %g u hull step", worstJumpBand, step);
        printf("6. continuity: worst vertex jump per %.2f u hull step = %.4f u (ribbon), %.4f u (band edge)\n", step, worstJump, worstJumpBand);
    }

    // ---------------- 7. partial weight blends the MOTION ----------------
    {
        setModel(float3(0,0,0), float3(1,0,0), 0.7f, SCALE);
        auto faces = cube();
        const Face& f = faces[3];   // +y
        float3 cw = faceCentroidWorld(f), nw = normalToWorld(f.n);
        float3 U = cw + nw * 5.0f + float3(2.0f, 0, 1.0f);
        float3 nT = (U - cw) / length(U - cw);
        float3 cT = U - nT * R;
        setBank(1, U, R, 0.5f);
        MovedFace r = run(f);
        float3 half = lerp(cw, cT, 0.5f);
        float fullAngle = std::acos(std::max(-1.0f, std::min(1.0f, dot(nw, nT))));
        float gotAngle = std::acos(std::max(-1.0f, std::min(1.0f, dot(nw, r.nw))));
        CHECK(length(r.cw - half) < 1e-3f, "strength 0.5: centroid not half-way (err %g)", length(r.cw - half));
        CHECK(std::fabs(gotAngle - 0.5f * fullAngle) < 1e-4f, "strength 0.5: angle %g is not half of %g", gotAngle, fullAngle);
        CHECK(maxPairDistanceError(f, r) < 1e-3f, "strength 0.5: face not rigid");
        printf("7. strength 0.5: centroid half-way (err %.2e), angle %.4f = half of %.4f, rigid\n", length(r.cw - half), gotAngle, fullAngle);
    }

    // ---------------- 8. nearest slot ----------------
    {
        setModel(float3(0,0,0), float3(0,1,0), 0, SCALE);
        auto faces = cube();
        const Face& f = faces[3];   // +y, centroid (0,0.5,0)
        float3 cw = faceCentroidWorld(f);
        float3 near = cw + float3(0, 4, 0), far = cw + float3(0, 9, 3);
        setBank(2, far, R, 1, near, R, 1);        // the nearer hull is in slot 1
        MovedFace r = run(f);
        CHECK(std::fabs(length(r.cw - near) - R) < 1e-3f && std::fabs(length(r.cw - far) - R) > 0.5f,
              "nearest slot: face did not cradle onto the nearer hull");
        printf("8. two hulls live: face cradles the nearer one (|c-near|-R = %.2e)\n", std::fabs(length(r.cw - near) - R));
    }

    if (failures) { printf("\n%d FAILURE(S)\n", failures); return 1; }
    printf("\nall properties hold\n");
    return 0;
}
"""

# The negative control drives the same harness with the gate disabled and only asks one
# question: does the far face move now?
CONTROL_MAIN = r"""#include "shipped.h"
#include <cstdio>
float4x4 g_objectToWorld, g_worldToObject;
int main()
{
    // identity model; unit cube; hull 3 u BELOW the -y face, i.e. behind the +y face's plane,
    // so the +y face's normal points straight away from the hull.
    for(int i=0;i<4;i++)for(int j=0;j<4;j++){ g_objectToWorld.m[i][j]=(i==j); g_worldToObject.m[i][j]=(i==j); }
    _PrismCradleCentre[0] = float4(0, -3.0f, 0, 2.0f); _PrismCradleWeight[0] = float4(1,0,0,0);
    _PrismCradleParams = float4(15.0f, 10.0f, 1, 0);
    float3 p(0.5f, 0.5f, 0.5f), n(0, 1, 0), op, on;
    PrismCradleDeform_float(p, n, op, on);
    bool moved = (op.x != p.x || op.y != p.y || op.z != p.z);
    printf("gate disabled: far face %s\n", moved ? "MOVES (control fires)" : "did not move (control failed)");
    return moved ? 0 : 1;
}
"""


def translate(src):
    """HLSL -> C++ spelling. Mechanical only: no semantic edits to the shipped file."""
    out = src
    out = re.sub(r"\bout float3 (\w+)", r"float3 &\1", out)
    out = re.sub(r"\.xyz\b", ".xyz()", out)
    assert "void PrismCradleDeform_float(" in out, "entry point missing"
    for name in ("_PrismCradleCentre", "_PrismCradleWeight", "_PrismCradleParams", "PRISM_CRADLE_SLOTS"):
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

        rc = build_and_run(work, MAIN, [], "verify")
        if rc is None or rc != 0:
            print("\nFAILED", file=sys.stderr)
            return 1

        print("\n9. negative control (facing gate disabled via -D):")
        rc = build_and_run(work, CONTROL_MAIN,
                           ["-DPRISM_CRADLE_FACING_LO=-2.0", "-DPRISM_CRADLE_FACING_HI=-1.5"], "control")
        if rc is None or rc != 0:
            print("\nFAILED: the negative control did not fire — the gate is not what holds the far face", file=sys.stderr)
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
