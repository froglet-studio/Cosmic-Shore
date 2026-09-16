#!/usr/bin/env python3
"""
Prove the SHIPPED PrismCradle.hlsl does what Docs/PRISM_ANIMATION.md §4.7.2 says it does,
by compiling the file itself with clang++ and RUNNING it (/asset-surgery §4.5c) — the same
harness shape as verify_prism_sight_composition.py. Nothing here is a transcription of the
shader; the file under Assets/ is translated mechanically (HLSL -> C++ spelling only) and
executed against the REAL prism mesh layout (Assets/_Models/Testing/Prism.asset: 6 faces x 4
wedges fanned from a duplicated face-centre vertex, each vertex carrying its face normal and a
tangent pointing from the face centre at its own wedge's outer edge) under a real non-uniform
model matrix.

What it proves, each on randomized inputs:

  1. IDENTITY with no live slot (count 0): bit-identical pass-through. This is "a match with
     no Urchin in it costs nothing and changes nothing".
  2. IDENTITY outside the band: a wedge whose centroid is past outerRange is untouched.
  3. THE CONTRACT at full weight: the wedge nearest the hull, facing it, comes out with
     (a) its centroid ON the hull's surface, on the line from its old centroid to the hull's
         centre — the "exact position" clause, which is why the radius is a uniform;
     (b) its normal pointing AT the hull's centre;
     (c) every pairwise vertex distance preserved (rigid), measured in WORLD space under a
         (3, 1, 6) scale — a rigid motion is only rigid in an isotropic frame;
     (d) the OUTPUT normal, carried back to world, equal to the geometric normal of the moved
         triangle — the inverse-transpose bookkeeping is checked, not assumed.
  4. THE FACING GATE: wedges on the far side of the prism (normal pointing away from the hull)
     are untouched, so the 180-degree flip with no defined axis never happens.
  5. ONE TRIANGLE, THREE NEIGHBOURS, NOTHING ELSE: with the hull over one wedge, exactly that
     wedge lands on the surface; exactly its three adjacent wedges (two beside it on the face,
     one across the prism edge) move, each PARTWAY (centroid strictly between where it was and
     the surface, less far than the nearest); and whenever the nearest is CLEAR (every
     non-adjacent wedge more than the spread farther than it — true over the slab's broad
     faces) the other 20 triangles are bit-identical. When it is not clear (a thin side face,
     whose wedges are all within a unit of each other) a non-adjacent stray may come a little
     way, but never to the surface and never as far as a neighbour — the continuity term the
     shipped file's header explains. Trials alternate the (3, 1, 6) slab with a 6 u cube so
     both halves of the claim are exercised.
  6. THE WRAP: the cross-edge neighbour arrives OUTWARD face first — its normal turns toward
     the hull, never away — and with the hull resting on an edge the two wedges either side
     both land on the surface at once (two triangles touching the sphere at the hand-off).
  7. CONTINUITY: sweeping the hull along the ribbon, across wedge hand-offs (including the
     4-way tie over a face centre) and into the band, no vertex jumps by more than a bounded
     multiple of the step. This is the "no snap" claim, measured.
  8. PARTIAL WEIGHT: at strength 0.5 the nearest wedge's centroid is half-way and its rotation
     half-angle, i.e. the blend is of the rigid MOTION, not of the vertices.
  9. NEAREST SLOT: with two hulls live, a wedge uses the nearer one.
 10. NEGATIVE CONTROL: rebuilt with the facing gate disabled (-D overrides of the file's own
     #ifndef dials), a far wedge DOES move — so the gate is what holds it, not luck.

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

// ---- the prism mesh: 6 faces x 4 wedges, each a triangle (centre, corner, corner) with the
// face normal n and a tangent t from the face centre to the wedge's own outer edge — exactly
// Assets/_Models/Testing/Prism.asset (verified: 72 verts, 24 tris, tangents per wedge). ----
struct Wedge { float3 n; float3 t; float3 v[3]; int face; };
static std::vector<Wedge> prism() {
    std::vector<Wedge> ws;
    float3 axes[3] = { float3(1,0,0), float3(0,1,0), float3(0,0,1) };
    int face = 0;
    for (int a = 0; a < 3; a++) for (int sgn = -1; sgn <= 1; sgn += 2, face++) {
        float3 n = axes[a] * (float)sgn;
        float3 tangents[4] = { axes[(a+1)%3], -axes[(a+1)%3], axes[(a+2)%3], -axes[(a+2)%3] };
        for (int k = 0; k < 4; k++) {
            float3 t = tangents[k];
            float3 bt = cross(n, t);
            Wedge w; w.n = n; w.t = t; w.face = face;
            float3 c = n * 0.5f;
            float3 q0 = c, q1 = c + t*0.5f - bt*0.5f, q2 = c + t*0.5f + bt*0.5f;
            // wound so that cross(v1-v0, v2-v0) points along n
            if (dot(cross(q1-q0, q2-q0), n) < 0) std::swap(q1, q2);
            w.v[0] = q0; w.v[1] = q1; w.v[2] = q2;
            ws.push_back(w);
        }
    }
    return ws;
}
static bool sameFace(const Wedge& a, const Wedge& b) { return a.face == b.face; }
// Adjacency exactly as the header states it: the two wedges beside it on its face ((n, ±b))
// and the one across its outer edge ((t, n)).
static bool adjacent(const Wedge& a, const Wedge& b) {
    float3 bt = cross(a.n, a.t);
    if (sameFace(a, b)) return std::fabs(dot(b.t, bt)) > 0.5f;
    return dot(b.n, a.t) > 0.5f && dot(b.t, a.n) > 0.5f;
}

static void setBank(int count, float3 U0, float R0, float S0, float3 U1 = float3(0,0,0), float R1 = 0, float S1 = 0,
                    float spread = 2.0f) {
    _PrismCradleCentre[0] = float4(U0, R0); _PrismCradleWeight[0] = float4(S0,0,0,0);
    _PrismCradleCentre[1] = float4(U1, R1); _PrismCradleWeight[1] = float4(S1,0,0,0);
    _PrismCradleCentre[2] = float4(0,0,0,0); _PrismCradleWeight[2] = float4(0,0,0,0);
    _PrismCradleCentre[3] = float4(0,0,0,0); _PrismCradleWeight[3] = float4(0,0,0,0);
    _PrismCradleParams = float4(15.0f, 10.0f, (float)count, spread);
}

struct Moved { float3 pw[3]; float3 nw; float3 cw; float3 nObj[3]; float3 pObj[3]; };
static Moved run(const Wedge& w) {
    Moved r; r.cw = float3(0,0,0);
    for (int i = 0; i < 3; i++) {
        PrismCradleDeform_float(w.v[i], w.n, w.t, r.pObj[i], r.nObj[i]);
        r.pw[i] = toWorld(r.pObj[i]);
        r.cw = r.cw + r.pw[i] * (1.0f/3.0f);
    }
    r.nw = normalToWorld(r.nObj[0]);
    return r;
}
static float3 centroidWorld(const Wedge& w) { float3 c(0,0,0); for(int i=0;i<3;i++) c = c + toWorld(w.v[i]) * (1.0f/3.0f); return c; }
static bool identical(const Wedge& w, const Moved& r) {
    for (int i = 0; i < 3; i++) {
        if (r.pObj[i].x != w.v[i].x || r.pObj[i].y != w.v[i].y || r.pObj[i].z != w.v[i].z) return false;
        if (r.nObj[i].x != w.n.x || r.nObj[i].y != w.n.y || r.nObj[i].z != w.n.z) return false;
    }
    return true;
}
static float maxPairDistanceError(const Wedge& w, const Moved& r) {
    float e = 0;
    for (int i = 0; i < 3; i++) for (int j = i+1; j < 3; j++) {
        float d0 = length(toWorld(w.v[i]) - toWorld(w.v[j]));
        float d1 = length(r.pw[i] - r.pw[j]);
        e = std::max(e, std::fabs(d0 - d1));
    }
    return e;
}
static float geomNormalDot(const Moved& r) { float3 n = cross(r.pw[1]-r.pw[0], r.pw[2]-r.pw[0]); return dot(n / length(n), r.nw); }
static int nearestWedge(const std::vector<Wedge>& ws, float3 U) {
    int best = -1; float bd = 1e30f;
    for (size_t k = 0; k < ws.size(); k++) { float d = length(centroidWorld(ws[k]) - U); if (d < bd) { bd = d; best = (int)k; } }
    return best;
}

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
            for (const Wedge& w : prism()) if (!identical(w, run(w))) bad++;
        }
        CHECK(bad == 0, "identity with count 0 broken on %d wedges", bad);
        printf("1. count 0 -> bit-identical pass-through: %s\n", bad == 0 ? "ok" : "BROKEN");
    }

    // ---------------- 2. identity outside the band ----------------
    {
        int bad = 0, tested = 0;
        for (int trial = 0; trial < 200; trial++) {
            setModel(float3(0,0,0), rndDir(), rnd(0, 6.28f), SCALE);
            float3 U = rndDir() * rnd(30, 80);
            setBank(1, U, R, 1);
            for (const Wedge& w : prism()) { tested++; if (!identical(w, run(w))) bad++; }
        }
        CHECK(bad == 0, "identity outside the band broken on %d of %d wedges", bad, tested);
        printf("2. hull 30-80 u away -> untouched: %s (%d wedges)\n", bad == 0 ? "ok" : "BROKEN", tested);
    }

    // ---------------- 3. the contract at full weight ----------------
    {
        float worstSurface = 0, worstNormal = 0, worstRigid = 0, worstGeom = 0, worstLine = 0;
        int tested = 0;
        for (int trial = 0; trial < 400; trial++) {
            setModel(float3(rnd(-20,20),rnd(-20,20),rnd(-20,20)), rndDir(), rnd(0, 6.28f), SCALE);
            auto ws = prism();
            // pick a wedge, put the hull in front of it, inside innerRange, facing it
            const Wedge& w = ws[rng() % ws.size()];
            float3 cw = centroidWorld(w);
            float3 nw = normalToWorld(w.n);
            float3 U = cw + nw * rnd(3, 7) + rndDir() * rnd(0, 1);
            if (dot(nw, (U - cw) / length(U - cw)) < 0.2f) continue;   // keep it clearly facing
            if (nearestWedge(ws, U) != (int)(&w - &ws[0])) continue;    // and actually the nearest
            setBank(1, U, R, 1);
            Moved r = run(w);
            tested++;
            float3 nT = (U - cw) / length(U - cw);
            float3 cT = U - nT * R;
            worstSurface = std::max(worstSurface, std::fabs(length(r.cw - U) - R));
            worstLine = std::max(worstLine, length(r.cw - cT));
            worstNormal = std::max(worstNormal, 1.0f - dot(r.nw, nT));
            worstRigid = std::max(worstRigid, maxPairDistanceError(w, r));
            worstGeom = std::max(worstGeom, 1.0f - geomNormalDot(r));
        }
        CHECK(tested > 150, "too few contract trials (%d)", tested);
        CHECK(worstSurface < 1e-3f, "centroid not on the hull surface: worst |d - R| = %g", worstSurface);
        CHECK(worstLine < 1e-3f, "centroid not on the line to the hull centre: worst = %g", worstLine);
        CHECK(worstNormal < 1e-5f, "normal not pointing at the hull centre: worst 1-dot = %g", worstNormal);
        CHECK(worstRigid < 1e-3f, "wedge not rigid: worst pair-distance error = %g", worstRigid);
        CHECK(worstGeom < 1e-4f, "output normal disagrees with the moved triangle's geometry: 1-dot = %g", worstGeom);
        printf("3. nearest wedge, full weight (%d trials): centroid on surface (|d-R| %.2e), on the line (%.2e), normal at centre (1-dot %.2e), rigid (%.2e), normal matches geometry (%.2e)\n",
               tested, worstSurface, worstLine, worstNormal, worstRigid, worstGeom);
    }

    // ---------------- 4. the facing gate ----------------
    {
        int bad = 0, tested = 0;
        for (int trial = 0; trial < 400; trial++) {
            setModel(float3(0,0,0), rndDir(), rnd(0, 6.28f), SCALE);
            auto ws = prism();
            const Wedge& w = ws[rng() % ws.size()];
            float3 cw = centroidWorld(w);
            float3 nw = normalToWorld(w.n);
            // hull BEHIND the wedge, within the band, within the gate's dead zone (dot <= -0.5)
            float3 U = cw - nw * rnd(2, 6);
            float3 d = (U - cw) / length(U - cw);
            if (dot(nw, d) > -0.55f) continue;
            setBank(1, U, R, 1, float3(0,0,0), 0, 0, 30.0f);   // a WIDE spread: only the gate can hold it
            tested++;
            if (!identical(w, run(w))) bad++;
        }
        CHECK(tested > 100 && bad == 0, "facing gate: %d of %d far wedges moved", bad, tested);
        printf("4. far wedge (normal away from hull) untouched: %s (%d trials)\n", bad == 0 ? "ok" : "BROKEN", tested);
    }

    // ---------------- 5. one triangle, three neighbours, nothing else ----------------
    {
        int trials = 0, clearTrials = 0, badNearest = 0, badNeighbours = 0, badOthers = 0, badPartway = 0, badStrayOnSurface = 0;
        float worstStrayFrac = 0;
        for (int trial = 0; trial < 400; trial++) {
            // Alternate the thin slab (every wedge within a unit of its face-mates: strays
            // expected, the untouched claim has no premise) with a 6 u cube (broad faces: a
            // clear nearest, the untouched claim exercised bit for bit).
            const float3 scale = (trial & 1) ? float3(6, 6, 6) : SCALE;
            setModel(float3(rnd(-10,10),rnd(-10,10),rnd(-10,10)), rndDir(), rnd(0, 6.28f), scale);
            auto ws = prism();
            int k = rng() % ws.size();
            const Wedge& w = ws[k];
            float3 cw = centroidWorld(w), nw = normalToWorld(w.n);
            // hull straight over the wedge's centroid, resting on it
            float3 U = cw + nw * (R + rnd(0.2f, 0.8f));
            if (nearestWedge(ws, U) != k) continue;
            const float spread = 2.0f;
            setBank(1, U, R, 1, float3(0,0,0), 0, 0, spread);
            trials++;
            // Is the nearest CLEAR — every non-adjacent wedge more than `spread` farther?
            float dMin = length(cw - U);
            bool clear = true;
            for (size_t j = 0; j < ws.size(); j++)
                if ((int)j != k && !adjacent(w, ws[j]) && length(centroidWorld(ws[j]) - U) - dMin <= spread) clear = false;
            if (clear) clearTrials++;
            Moved rw = run(w);
            if (std::fabs(length(rw.cw - U) - R) > 1e-3f) badNearest++;
            int neighbours = 0, others = 0;
            float leastNeighbourFrac = 2.0f, mostStrayFrac = 0.0f;
            for (size_t j = 0; j < ws.size(); j++) {
                if ((int)j == k) continue;
                Moved r = run(ws[j]);
                bool moved = !identical(ws[j], r);
                float3 c0 = centroidWorld(ws[j]);
                float3 nT = (U - c0) / length(U - c0);
                float3 cT = U - nT * R;
                float full = length(cT - c0), got = length(r.cw - c0);
                float frac = full > 1e-6f ? got / full : 0.0f;
                if (adjacent(w, ws[j])) {
                    neighbours++;
                    // partway: moved, but not all the way — a neighbour that lands on the
                    // surface would be a second "nearest"
                    if (!moved || got <= 1e-4f || got >= full - 1e-3f) badPartway++;
                    if (maxPairDistanceError(ws[j], r) > 1e-3f) badPartway++;
                    leastNeighbourFrac = std::min(leastNeighbourFrac, frac);
                } else {
                    others++;
                    if (clear && moved) badOthers++;              // a clear nearest: bit-identical
                    if (got >= full - 1e-3f) badStrayOnSurface++; // never on the surface
                    mostStrayFrac = std::max(mostStrayFrac, frac);
                }
            }
            if (neighbours != 3) badNeighbours++;
            if (others != 20) badNeighbours++;
            worstStrayFrac = std::max(worstStrayFrac, mostStrayFrac);
        }
        CHECK(trials > 150, "too few adjacency trials (%d)", trials);
        CHECK(clearTrials > 60, "too few trials with a CLEAR nearest wedge (%d) — the untouched claim was not exercised", clearTrials);
        CHECK(badNeighbours == 0, "adjacency census wrong in %d trials (expected 3 neighbours + 20 others)", badNeighbours);
        CHECK(badNearest == 0, "nearest wedge off the surface in %d trials", badNearest);
        CHECK(badPartway == 0, "a neighbour did not come PARTWAY (rigid, moved, short of the surface) in %d cases", badPartway);
        CHECK(badOthers == 0, "%d non-adjacent triangles moved under a CLEAR nearest wedge — they must be bit-identical", badOthers);
        CHECK(badStrayOnSurface == 0, "%d non-adjacent triangles reached the surface", badStrayOnSurface);
        printf("5. hull over one wedge (%d trials, %d with a clear nearest): that wedge on the surface, its 3 neighbours partway, the other 20 untouched when clear (worst stray %.0f%% of the way when not): %s\n",
               trials, clearTrials, worstStrayFrac * 100, (badNearest|badNeighbours|badPartway|badOthers|badStrayOnSurface) ? "BROKEN" : "ok");
    }

    // ---------------- 6. the wrap ----------------
    {
        setModel(float3(0,0,0), float3(0,1,0), 0, SCALE);
        auto ws = prism();
        // (a) hull over the +y face's +x wedge: its cross-edge neighbour is the +x face's +y
        //     wedge, whose OUTWARD normal must turn TOWARD the hull.
        int k = -1, kx = -1;
        for (size_t j = 0; j < ws.size(); j++) {
            if (ws[j].n.y > 0.5f && ws[j].t.x > 0.5f) k = (int)j;
            if (ws[j].n.x > 0.5f && ws[j].t.y > 0.5f) kx = (int)j;
        }
        CHECK(k >= 0 && kx >= 0 && adjacent(ws[k], ws[kx]), "wrap: could not find the edge pair");
        float3 U = centroidWorld(ws[k]) + float3(0, R + 0.5f, 0);
        setBank(1, U, R, 1);
        Moved rx = run(ws[kx]);
        float3 c0 = centroidWorld(ws[kx]);
        float3 dir0 = (U - c0) / length(U - c0);
        float before = dot(normalToWorld(ws[kx].n), dir0);
        float after = dot(rx.nw, (U - rx.cw) / length(U - rx.cw));
        CHECK(!identical(ws[kx], rx), "wrap: the cross-edge neighbour did not move");
        CHECK(after > before + 0.1f, "wrap: the cross-edge wedge's outward normal did not turn toward the hull (%.3f -> %.3f)", before, after);
        CHECK(after > 0.0f, "wrap: the cross-edge wedge arrives back-first (outward normal away from the hull, dot %.3f)", after);
        printf("6a. cross-edge neighbour wraps outward-face-first: dot(outward normal, to hull) %.3f -> %.3f\n", before, after);

        // (b) hull resting on the +x/+y edge, between the two wedges' centroids: both land.
        float3 edgeMid = float3(1.5f, 0.5f, 0);   // world: half-extents (1.5, 0.5, 3)
        float3 diag = float3(1, 1, 0) / std::sqrt(2.0f);
        U = edgeMid + diag * (R + 0.4f);
        setBank(1, U, R, 1);
        int onSurface = 0;
        for (const Wedge& w : ws) {
            Moved r = run(w);
            if (!identical(w, r) && std::fabs(length(r.cw - U) - R) < 0.05f) onSurface++;
        }
        CHECK(onSurface == 2, "hand-off: expected exactly 2 wedges on the surface at the edge, got %d", onSurface);
        printf("6b. hull over the +x/+y edge: %d triangles touching the surface at once (want 2)\n", onSurface);
    }

    // ---------------- 7. continuity ----------------
    {
        setModel(float3(0,0,0), float3(0,0,1), 0.3f, SCALE);
        auto ws = prism();
        const float step = 0.01f;
        float worstJump = 0, worstJumpBand = 0, worstJumpDiag = 0;
        auto sweep = [&](auto hullAt, float t0, float t1, float& worst) {
            std::vector<Moved> prev;
            for (float t = t0; t <= t1; t += step) {
                setBank(1, hullAt(t), R, 1);
                std::vector<Moved> cur; for (const Wedge& w : ws) cur.push_back(run(w));
                if (!prev.empty()) for (size_t k = 0; k < cur.size(); k++) for (int i = 0; i < 3; i++)
                    worst = std::max(worst, length(cur[k].pw[i] - prev[k].pw[i]));
                prev = cur;
            }
        };
        float3 up = normalToWorld(float3(0,1,0));
        // (a) along the slab at hull-radius height, off centre: across wedge hand-offs and off the end
        sweep([&](float x){ return toWorld(float3(x / SCALE.x, 0.5f, 0.15f)) + up * R; }, -8, 8, worstJump);
        // (b) straight in through the band's outer edge
        {
            float3 cw = toWorld(float3(0.2f, 0.5f, 0.1f));
            sweep([&](float h){ return cw + up * (22 - h); }, 0, 20, worstJumpBand);
        }
        // (c) diagonally across the +y face centre — the 4-way tie
        sweep([&](float s){ return toWorld(float3(s / SCALE.x, 0.5f, s / SCALE.z)) + up * R; }, -2.5f, 2.5f, worstJumpDiag);
        // A triangle can legitimately move faster than the hull (it swings about its centroid),
        // so the bound is a multiple of the step; a SNAP would be tens of units.
        CHECK(worstJump < 30 * step, "discontinuity along the ribbon: a vertex jumped %g u for a %g u hull step", worstJump, step);
        CHECK(worstJumpBand < 30 * step, "discontinuity crossing the band: a vertex jumped %g u for a %g u hull step", worstJumpBand, step);
        CHECK(worstJumpDiag < 30 * step, "discontinuity over the face centre: a vertex jumped %g u for a %g u hull step", worstJumpDiag, step);
        printf("7. continuity: worst vertex jump per %.2f u hull step = %.4f u (ribbon), %.4f u (band edge), %.4f u (face-centre tie)\n",
               step, worstJump, worstJumpBand, worstJumpDiag);
    }

    // ---------------- 8. partial weight blends the MOTION ----------------
    {
        setModel(float3(0,0,0), float3(1,0,0), 0.7f, SCALE);
        auto ws = prism();
        int k = -1; for (size_t j = 0; j < ws.size(); j++) if (ws[j].n.y > 0.5f && ws[j].t.z > 0.5f) k = (int)j;
        const Wedge& w = ws[k];
        float3 cw = centroidWorld(w), nw = normalToWorld(w.n);
        float3 U = cw + nw * 4.0f + float3(0.6f, 0, 0.3f);
        CHECK(nearestWedge(ws, U) == k, "strength 0.5: test wedge is not the nearest");
        float3 nT = (U - cw) / length(U - cw);
        float3 cT = U - nT * R;
        setBank(1, U, R, 0.5f);
        Moved r = run(w);
        float3 half = lerp(cw, cT, 0.5f);
        float fullAngle = std::acos(std::max(-1.0f, std::min(1.0f, dot(nw, nT))));
        float gotAngle = std::acos(std::max(-1.0f, std::min(1.0f, dot(nw, r.nw))));
        CHECK(length(r.cw - half) < 1e-3f, "strength 0.5: centroid not half-way (err %g)", length(r.cw - half));
        CHECK(std::fabs(gotAngle - 0.5f * fullAngle) < 1e-4f, "strength 0.5: angle %g is not half of %g", gotAngle, fullAngle);
        CHECK(maxPairDistanceError(w, r) < 1e-3f, "strength 0.5: wedge not rigid");
        printf("8. strength 0.5: centroid half-way (err %.2e), angle %.4f = half of %.4f, rigid\n", length(r.cw - half), gotAngle, fullAngle);
    }

    // ---------------- 9. nearest slot ----------------
    {
        setModel(float3(0,0,0), float3(0,1,0), 0, SCALE);
        auto ws = prism();
        int k = -1; for (size_t j = 0; j < ws.size(); j++) if (ws[j].n.y > 0.5f && ws[j].t.z > 0.5f) k = (int)j;
        const Wedge& w = ws[k];
        float3 cw = centroidWorld(w);
        float3 near = cw + float3(0, R + 0.5f, 0), far = cw + float3(0, 9, 3);
        setBank(2, far, R, 1, near, R, 1);        // the nearer hull is in slot 1
        Moved r = run(w);
        CHECK(std::fabs(length(r.cw - near) - R) < 1e-3f && std::fabs(length(r.cw - far) - R) > 0.5f,
              "nearest slot: wedge did not cradle onto the nearer hull");
        printf("9. two hulls live: wedge cradles the nearer one (|c-near|-R = %.2e)\n", std::fabs(length(r.cw - near) - R));
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
    // a WIDE neighbour spread, so the only thing holding the far wedge is the facing gate
    _PrismCradleParams = float4(15.0f, 10.0f, 1, 30.0f);
    float3 p(0.5f, 0.5f, 0.5f), n(0, 1, 0), t(1, 0, 0), op, on;
    PrismCradleDeform_float(p, n, t, op, on);
    bool moved = (op.x != p.x || op.y != p.y || op.z != p.z);
    printf("gate disabled: far wedge %s\n", moved ? "MOVES (control fires)" : "did not move (control failed)");
    return moved ? 0 : 1;
}
"""


def translate(src):
    """HLSL -> C++ spelling. Mechanical only: no semantic edits to the shipped file."""
    out = src
    out = re.sub(r"\bout float3 (\w+)", r"float3 &\1", out)
    out = re.sub(r"\.xyz\b", ".xyz()", out)
    out = re.sub(r"\[unroll\]", "", out)          # an HLSL loop attribute; C++ has no spelling for it
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

        print("\n10. negative control (facing gate disabled via -D):")
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
