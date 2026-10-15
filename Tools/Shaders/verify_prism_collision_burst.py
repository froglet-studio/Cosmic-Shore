#!/usr/bin/env python3
"""
Prove the SHIPPED PrismCollisionBurst.hlsl / .shader do what Docs/PRISM_ANIMATION.md §4.11 says a
collision burst does. Two halves, neither a transcription of the shader:

A. EXECUTION (clang++, /asset-surgery §4.5c). The file under Assets/ is translated mechanically
   (HLSL -> C++ spelling only) and RUN on randomized prisms (rotation, translation, non-uniform scale
   up to 20:1), every vertex of the unwelded high-poly mesh the burst draws, all five kinds:

   1. THE FIRST FRAME IS THE PRISM: at age 0 every vertex and normal of every kind is where the prism
      had it — the burst replaces the prism with no pop (continuity of existence).
   2. A TRIANGLE IS NEVER TORN: the three vertices of a triangle always name the same piece.
   3. RIGID PIECES STAY RIGID (shatter, glitter, flake): every distance inside a piece is preserved at every
      age, under the prism's non-uniform scale — the reason the motion is done in world space.
   4. BENDS ARE ISOMETRIC AND THE NORMAL IS THEIR DERIVATIVE (bloom, unravel): a small step along the
      petal or ribbon keeps its length, and the returned normal is perpendicular to both surface
      directions — exact lighting, not the cheap-normal shortcut the cradle rejected.
   5. NORMALS ARE UNIT, every kind, every age.
   6. THE ENVELOPE: every pose of every kind stays within PrismBurstEnvelope(spread) prism radii of the
      centre, which is what the CPU sizes the culling bounds by — and what keeps the SQUINT: the cloud
      is the prism's size, not a spray.
   7. THE CLOUD'S CENTRE STAYS PUT (before the flight): the mean of every vertex stays close to the
      prism centre at every age, so with the flight added the cloud goes where the explosion's would.
      The tolerance is reported against the measured worst case.
   8. THE FADE IS LINEAR: the fraction of fragments surviving at progress q is 1 - q (to sampling
      noise) for every grain — the explosion's opacity curve, the squint's other half.
   9. GONE BEFORE RETIREMENT: progress reaches 1 before age reaches 95% of the life, and is monotone,
      on both curves.
   9b. A SHED IS THE GRADE: with the life stamped as the grade's duration x (1 + overshoot), a shed's
      progress equals the colour weight the SHIPPED grade (PrismColorLerp_float, extracted from
      PrismClockAnimation.hlsl and executed) has reached at every instant — so the skin's coverage,
      1 - progress, is the grade's course.
  10. VORONOI: every point lies inside its own cell (non-negative border) and seeds stay on the face.
  11. AN UNKNOWN KIND FALLS BACK to the shatter, never to garbage.
  W. THE WEDGE (destruction, every Sierpinski depth 0-3): amount 0 is the prism; level 0 IS the explosion —
     equal to the harness's own Rodrigues statement of RotateFacesAlongAxis' closed form with the graph's
     constants; every fragment is rigid in the world; normals unit; every pose inside PrismBurstWedgeReach
     (the CPU's bounds); no fragment turns faster than the explosion's own cap (2 x amount x rotation); and every
     fragment SPINS as its root wedge does in the explosion (W7: the wedge's tangent and cross(velocity, N), so
     its normal is the wedge's).
  NEGATIVE CONTROLS, each must FAIL the test it guards:
  12. -DPRISM_BURST_DISSOLVE_OVERSHOOT=0  — fragments survive to retirement (test 9 fires).
  13. -DPRISM_BURST_UNIFORM_VALUE=0       — the fade sags off the explosion's curve (test 8 fires).
  14. -DPRISM_BURST_ENVELOPE_BASE=1       — a pose escapes the bounds (test 6 fires).
  15. -DPRISM_BURST_BEND_NORMAL=0         — the bend keeps its rest normal (test 4 fires).
  16. -DPRISM_BURST_GRADE_SMOOTH=0        — a shed fades linearly, off the grade (test 9b fires).
  17. -DPRISM_BURST_PIVOT_BIAS=0          — level 0 leaves the explosion (test W fires).
  18. -DPRISM_BURST_WEDGE_PIVOT_UNSCALED=1 — the turn in the unscaled frame shears fragments (test W fires).
  19. -DPRISM_BURST_WEDGE_OWN_TANGENT=1    — each fragment's own radial tangent: off its wedge's spin (W7 fires).

B. FRONT-END COMPILE (glslang, HLSL mode). Every pass of PrismCollisionBurst.shader, vertex and
   fragment, plain and DOTS-instanced, against a mock of the URP library (the slice verifier's). It
   proves the shader's own code is well-formed HLSL; it cannot prove the URP library behaves as the
   mock says. That half is the editor's.

Exit 0 on pass. Needs clang++ and glslangValidator; no Unity.
Usage:  python3 Tools/Shaders/verify_prism_collision_burst.py [--keep]
"""

import os
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GRAPHS = os.path.join(ROOT, "Assets/_Graphics/Materials/Graphs")
HLSL = os.path.join(GRAPHS, "PrismCollisionBurst.hlsl")
CLOCK = os.path.join(GRAPHS, "PrismClockAnimation.hlsl")
SHADER = os.path.join(GRAPHS, "PrismCollisionBurst.shader")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import verify_prism_slice as slice_verifier  # noqa: E402  (the URP mock + the pass extraction)

SHIM = r"""#pragma once
#include <cmath>
#include <algorithm>
using std::exp; using std::sin; using std::cos; using std::sqrt; using std::floor;
struct float2 { float x=0,y=0; float2(){} float2(float a,float b):x(a),y(b){} };
struct float3 {
    float x=0,y=0,z=0;
    float3(){}
    float3(float a):x(a),y(a),z(a){}
    float3(float a,float b,float c):x(a),y(b),z(c){}
};
static inline float2 operator+(float2 a,float2 b){return float2(a.x+b.x,a.y+b.y);}
static inline float2 operator-(float2 a,float2 b){return float2(a.x-b.x,a.y-b.y);}
static inline float2 operator*(float2 a,float b){return float2(a.x*b,a.y*b);}
static inline float2 operator/(float2 a,float b){return float2(a.x/b,a.y/b);}
static inline float dot(float2 a,float2 b){return a.x*b.x+a.y*b.y;}
static inline float3 operator+(float3 a,float3 b){return float3(a.x+b.x,a.y+b.y,a.z+b.z);}
static inline float3 operator-(float3 a,float3 b){return float3(a.x-b.x,a.y-b.y,a.z-b.z);}
static inline float3 operator*(float3 a,float3 b){return float3(a.x*b.x,a.y*b.y,a.z*b.z);}
static inline float3 operator*(float3 a,float b){return float3(a.x*b,a.y*b,a.z*b);}
static inline float3 operator*(float a,float3 b){return b*a;}
static inline float3 operator+(float3 a,float b){return float3(a.x+b,a.y+b,a.z+b);}
static inline float3 operator-(float3 a,float b){return float3(a.x-b,a.y-b,a.z-b);}
static inline float3 operator-(float a,float3 b){return float3(a-b.x,a-b.y,a-b.z);}
static inline float3 operator/(float3 a,float b){return float3(a.x/b,a.y/b,a.z/b);}
static inline float3 operator-(float3 a){return float3(-a.x,-a.y,-a.z);}
static inline float dot(float3 a,float3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
static inline float3 cross(float3 a,float3 b){return float3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
static inline float length(float3 a){return std::sqrt(dot(a,a));}
static inline float3 normalize(float3 a){return a/length(a);}
static inline float abs(float v){return std::fabs(v);}
static inline float saturate(float v){return std::min(1.0f,std::max(0.0f,v));}
static inline float min(float a,float b){return a<b?a:b;}
static inline float max(float a,float b){return a>b?a:b;}
static inline float clamp(float v,float a,float b){return min(max(v,a),b);}
static inline float lerp(float a,float b,float t){return a+(b-a)*t;}
static inline float frac(float v){return v-std::floor(v);}
static inline float3 frac(float3 v){return float3(frac(v.x),frac(v.y),frac(v.z));}
static inline float3 floor(float3 v){return float3(std::floor(v.x),std::floor(v.y),std::floor(v.z));}
static inline float2 floor(float2 v){return float2(std::floor(v.x),std::floor(v.y));}
static inline float2 min(float2 a,float2 b){return float2(min(a.x,b.x),min(a.y,b.y));}
static inline float2 operator+(float2 a,float b){return float2(a.x+b,a.y+b);}
static inline float smoothstep(float a,float b,float x){float t=saturate((x-a)/(b-a));return t*t*(3.0f-2.0f*t);}
struct float4 { float x=0,y=0,z=0,w=0; float4(){} float4(float a,float b,float c,float d):x(a),y(b),z(c),w(d){} };
static inline float4 lerp(float4 a,float4 b,float t){return float4(lerp(a.x,b.x,t),lerp(a.y,b.y,t),lerp(a.z,b.z,t),lerp(a.w,b.w,t));}
static inline float3 lerp(float3 a,float3 b,float t){return float3(lerp(a.x,b.x,t),lerp(a.y,b.y,t),lerp(a.z,b.z,t));}
static inline float3 abs(float3 v){return float3(std::fabs(v.x),std::fabs(v.y),std::fabs(v.z));}
static inline float3 max(float3 a,float3 b){return float3(max(a.x,b.x),max(a.y,b.y),max(a.z,b.z));}
static inline float3 operator/(float3 a,float3 b){return float3(a.x/b.x,a.y/b.y,a.z/b.z);}
static inline float exp2(float v){return std::exp2(v);}
"""

HARNESS = r"""#include "shipped.h"
#include <cstdio>
#include <cstring>
#include <random>
#include <vector>

static int failures = 0;
static int reported = 0;
#define CHECK(cond, ...) do { if (!(cond)) { failures++; if (reported++ < 12) { printf("FAIL: "); printf(__VA_ARGS__); printf("\n"); } } } while (0)

static std::mt19937 rng(20261014);
static float rnd(float a, float b) { return a + (b - a) * (rng() / (float)rng.max()); }
static float3 rndDir() { for(;;){ float3 v(rnd(-1,1),rnd(-1,1),rnd(-1,1)); float l=length(v); if(l>0.1f) return v/l; } }

// ---- a random prism pose: rotation (orthonormal columns), translation, non-uniform scale ----
struct Pose { float3 c0, c1, c2; float3 t; float3 s; };
static Pose rndPose() {
    float3 a = rndDir(); float3 b = normalize(cross(a, rndDir())); float3 c = cross(a, b);
    Pose p; p.c0 = a; p.c1 = b; p.c2 = c; p.t = float3(rnd(-50,50),rnd(-50,50),rnd(-50,50));
    float base = rnd(0.5f, 4.0f);
    p.s = float3(base * rnd(1, 20), base * rnd(0.2f, 3), base * rnd(0.5f, 6));
    return p;
}
static float3 linear(const Pose& p, float3 v) { return p.c0 * (v.x * p.s.x) + p.c1 * (v.y * p.s.y) + p.c2 * (v.z * p.s.z); }
static float3 world(const Pose& p, float3 v) { return p.t + linear(p, v); }
static float3 worldNormal(const Pose& p, float3 n) { return normalize(p.c0 * (n.x / p.s.x) + p.c1 * (n.y / p.s.y) + p.c2 * (n.z / p.s.z)); }

// ---- the unwelded mesh, built the way HighPolyPrismMesh.GetUnwelded lays it out ----
struct Vert { float3 pos, n, u; float2 tri; int triIndex; };
static const int N = 10;
static std::vector<Vert> mesh() {
    float3 fn[6] = {float3(1,0,0),float3(-1,0,0),float3(0,1,0),float3(0,-1,0),float3(0,0,1),float3(0,0,-1)};
    float3 fu[6] = {float3(0,1,0),float3(0,0,1),float3(0,0,1),float3(1,0,0),float3(1,0,0),float3(0,1,0)};
    float3 fv[6] = {float3(0,0,1),float3(0,1,0),float3(1,0,0),float3(0,0,1),float3(0,1,0),float3(1,0,0)};
    std::vector<Vert> out; int tri = 0;
    for (int f = 0; f < 6; f++)
    for (int j = 0; j < N; j++)
    for (int i = 0; i < N; i++) {
        float2 q[4] = { float2((float)i/N,(float)j/N), float2((float)(i+1)/N,(float)j/N),
                        float2((float)(i+1)/N,(float)(j+1)/N), float2((float)i/N,(float)(j+1)/N) };
        int idx[2][3] = {{0,1,2},{0,2,3}};
        for (int t = 0; t < 2; t++, tri++) {
            float2 cen = (q[idx[t][0]] + q[idx[t][1]] + q[idx[t][2]]) / 3.0f;
            for (int k = 0; k < 3; k++) {
                float2 uv = q[idx[t][k]];
                Vert v; v.n = fn[f]; v.u = fu[f];
                v.pos = fn[f]*0.5f + fu[f]*(uv.x - 0.5f) + fv[f]*(uv.y - 0.5f);
                v.tri = cen; v.triIndex = tri; out.push_back(v);
            }
        }
    }
    return out;
}

// ---- the frame, built the way PrismCollisionBurst.shader's BurstVert builds it ----
static PrismBurstFrame frameOf(const Pose& p, const Vert& v, float seed) {
    PrismBurstFrame f;
    f.rest = world(p, v.pos);
    f.normal = worldNormal(p, v.n);
    f.centre = world(p, float3(0,0,0));
    f.faceCentre = world(p, v.n * 0.5f);
    float3 vOS = cross(v.n, v.u);
    f.faceU = linear(p, v.u * 0.5f);
    f.faceV = linear(p, vOS * 0.5f);
    f.triUV = v.tri * 2.0f - float2(1, 1);
    f.faceKey = v.n * 3.7f + seed * 11.0f;
    f.radius = length(linear(p, float3(0.5f, 0.5f, 0.5f)));
    float3 hx = linear(p, float3(0.5f, 0, 0)), hy = linear(p, float3(0, 0.5f, 0)), hz = linear(p, float3(0, 0, 0.5f));
    float3 lng = hx;
    if (dot(hy, hy) > dot(lng, lng)) lng = hy;
    if (dot(hz, hz) > dot(lng, lng)) lng = hz;
    f.longAxis = lng;
    return f;
}
static PrismBurstParams params(float age, float seed, float spread) {
    PrismBurstParams q;
    q.age = age; q.speed = rnd(10, 53); q.seed = seed; q.spread = spread; q.tau = 0.22f; q.spin = 0.3f;
    q.bloomTurns = 0.7f; q.unravelTurns = 0.9f; q.strips = 5; q.cells = 3;
    q.rings = 3; q.segments = 3; q.noise = 0.6f;
    q.progress = PrismBurstProgress(age, 7.5f, 0.0f);
    return q;
}

static void run_pose_tests(bool envelopeOnly) {
    std::vector<Vert> m = mesh();
    float worstCentroid[5] = {0,0,0,0,0};
    float worstEnvelope = 0;
    for (int trial = 0; trial < 24; trial++) {
        Pose P = rndPose();
        float seed = rnd(0, 1);
        float spread = rnd(0.2f, 1.5f);
        for (int kind = 0; kind < 5; kind++) {
            float ages[6] = {0.0f, 0.05f, 0.3f, 1.0f, 3.0f, 7.5f};
            for (float age : ages) {
                PrismBurstParams q = params(age, seed, spread);
                float3 sum(0); int count = 0;
                float R = 0;
                std::vector<float3> pos(m.size()), nrm(m.size());
                std::vector<float> piece(m.size());
                for (size_t i = 0; i < m.size(); i++) {
                    PrismBurstFrame f = frameOf(P, m[i], seed);
                    R = f.radius;
                    float3 pp, nn; float h;
                    PrismBurstPose((float)kind, f, q, pp, nn, h);
                    pos[i] = pp; nrm[i] = nn; piece[i] = h;
                    sum = sum + pp; count++;
                    // 6. the envelope
                    float r = length(pp - f.centre) / f.radius;
                    if (r > worstEnvelope) worstEnvelope = r;
                    CHECK(r <= PrismBurstEnvelope(spread) + 1e-4f, "kind %d age %.2f: a vertex at %.3f radii escapes the envelope %.3f", kind, age, r, PrismBurstEnvelope(spread));
                    if (envelopeOnly) continue;
                    // 1. the first frame is the prism
                    if (age == 0.0f) {
                        CHECK(length(pp - f.rest) <= 2e-5f * f.radius, "kind %d: age 0 moved a vertex by %g radii", kind, length(pp - f.rest) / f.radius);
                        CHECK(length(nn - f.normal) <= 2e-5f, "kind %d: age 0 turned a normal", kind);
                    }
                    // 5. unit normals
                    CHECK(std::fabs(length(nn) - 1.0f) <= 1e-4f, "kind %d: normal length %g", kind, length(nn));
                }
                if (envelopeOnly) continue;
                // 7. the cloud's centre
                float3 mean = sum / (float)count;
                float drift = length(mean - world(P, float3(0,0,0))) / R;
                if (drift > worstCentroid[kind]) worstCentroid[kind] = drift;
                CHECK(drift <= 0.45f * std::max(spread, 1.0f), "kind %d age %.2f: the cloud's centre wandered %.3f radii", kind, age, drift);
                for (size_t i = 0; i < m.size(); i += 3) {
                    // 2. a triangle is never torn
                    CHECK(piece[i] == piece[i+1] && piece[i] == piece[i+2], "kind %d: a triangle straddles two pieces", kind);
                    // 3. rigid pieces stay rigid (shatter, glitter, flake): the triangle's own edges
                    if (kind == 0 || kind == 3 || kind == 4) {
                        for (int a = 0; a < 3; a++) {
                            int b = (a + 1) % 3;
                            float3 restA = world(P, m[i+a].pos), restB = world(P, m[i+b].pos);
                            float d0 = length(restA - restB), d1 = length(pos[i+a] - pos[i+b]);
                            CHECK(std::fabs(d0 - d1) <= 1e-4f * R, "kind %d: a rigid piece stretched %g -> %g", kind, d0, d1);
                        }
                    }
                }
                // 3b. rigid across a whole piece: pairs of triangles naming the same shard or scale
                if (kind == 0 || kind == 4) {
                    for (int k = 0; k < 400; k++) {
                        size_t i = (rng() % (m.size() / 3)) * 3, j = (rng() % (m.size() / 3)) * 3;
                        if (piece[i] != piece[j] || !(m[i].n.x == m[j].n.x && m[i].n.y == m[j].n.y && m[i].n.z == m[j].n.z)) continue;
                        float d0 = length(world(P, m[i].pos) - world(P, m[j].pos)), d1 = length(pos[i] - pos[j]);
                        CHECK(std::fabs(d0 - d1) <= 2e-4f * R, "kind %d: a piece deformed %g -> %g", kind, d0, d1);
                    }
                }
            }
        }
    }
    printf("worst envelope: %.3f radii; worst cloud-centre drift (radii) shatter %.3f bloom %.3f unravel %.3f glitter %.3f flake %.3f\n",
           worstEnvelope, worstCentroid[0], worstCentroid[1], worstCentroid[2], worstCentroid[3], worstCentroid[4]);
}

// 4. bends: isometric, and the normal is the derivative.
static void run_bend_tests() {
    std::vector<Vert> m = mesh();
    for (int trial = 0; trial < 60; trial++) {
        Pose P = rndPose();
        P.t = float3(0, 0, 0);   // float precision is relative to the coordinates; keep them small
        float seed = rnd(0, 1);
        for (int kind = 1; kind <= 2; kind++) {
            PrismBurstParams q = params(rnd(0.05f, 7.5f), seed, rnd(0.2f, 1.0f));
            for (int k = 0; k < 200; k++) {
                const Vert& v = m[rng() % m.size()];
                PrismBurstFrame f = frameOf(P, v, seed);
                float3 p0, n0; float h0;
                PrismBurstPose((float)kind, f, q, p0, n0, h0);
                // Two small steps in the face plane, staying on the same petal / ribbon (same
                // triangle). The step is sized to the FACE, not the prism: a petal's curl radius on
                // a thin slab is far smaller than the prism's radius, and a chord across a curl tilts
                // off the tangent by kappa*eps/2. So the normal is proved by CONVERGENCE (the
                // prism-morph protocol): halving the step must halve the chord's tilt off the
                // returned normal. A wrong normal plateaus at its error.
                float faceScale = std::min(length(f.faceU), length(f.faceV));
                float3 dirs[2] = { normalize(f.faceU), normalize(f.faceV) };
                for (int d = 0; d < 2; d++) {
                    float tilt[2], stretch[2], floors[2];
                    for (int h = 0; h < 2; h++) {
                        float eps = (h == 0 ? 2e-2f : 1e-2f) * faceScale;
                        PrismBurstFrame g = f; g.rest = f.rest + dirs[d] * eps;
                        float3 p1, n1; float h1;
                        PrismBurstPose((float)kind, g, q, p1, n1, h1);
                        float3 step = p1 - p0;
                        float ratio = length(step) / eps;
                        // The chord of a step this size is within 2e-3 of its arc; below that, float32
                        // rounding of coordinates of size |p0| over the step (the tilt's floor, below).
                        float stretchFloor = std::max(2e-3f, 8.0f * 1.2e-7f * (length(p0) + f.radius) / eps);
                        stretch[h] = std::fabs(ratio - 1.0f); floors[h] = stretchFloor;
                        tilt[h] = std::fabs(dot(normalize(step), n0));
                    }
                    // A tight curl (the bloom's short pieces) makes the chord fall short of its arc by
                    // (k eps)^2 / 24 — so isometry is proved by CONVERGENCE too: halving the step must
                    // quarter the shortfall. A real stretch is O(1) and does not shrink with the step.
                    bool small = stretch[0] <= floors[0] && stretch[1] <= floors[1];
                    CHECK(small || stretch[1] <= 0.35f * stretch[0] + floors[1], "kind %d: the bend stretched a step by %g (eps) and %g (eps/2)", kind, stretch[0], stretch[1]);
                    // Below the float noise floor of a step this size the tilt carries no
                    // information either way (rounding of coordinates of size |p0| over a step eps).
                    float floorTilt = 8.0f * 1.2e-7f * (length(p0) + f.radius) / (1e-2f * faceScale);
                    bool exact = tilt[0] < floorTilt && tilt[1] < floorTilt;
                    CHECK(exact || tilt[1] <= 0.62f * tilt[0], "kind %d: the normal is not the bend's derivative (tilt %g at eps, %g at eps/2)", kind, tilt[0], tilt[1]);
                }
            }
        }
    }
}

// 8. the fade is linear; 9. gone before retirement.
static void run_fade_tests() {
    const float grains[3] = {0.0f, 0.35f, 1.0f};
    for (float grain : grains) {
        for (int qi = 1; qi <= 9; qi++) {
            float q = qi / 10.0f;
            int survive = 0, total = 200000;
            for (int k = 0; k < total; k++) {
                float piece = rnd(0, 0.99999f);
                float noise = PrismBurstNoise(float3(rnd(-50,50), rnd(-50,50), rnd(-50,50)));
                if (!PrismBurstClipped(PrismBurstDissolveValue(piece, noise, grain), q)) survive++;
            }
            float frac = survive / (float)total;
            CHECK(std::fabs(frac - (1.0f - q)) <= 0.01f, "grain %.2f: at progress %.1f %.3f survive, the explosion's curve says %.3f", grain, q, frac, 1.0f - q);
        }
    }
    for (float curve : {0.0f, 1.0f}) {
        float life = curve > 0.5f ? 0.848f : 7.5f;
        CHECK(PrismBurstProgress(0.0f, life, curve) == 0.0f, "curve %g: progress at age 0 is not 0", curve);
        CHECK(PrismBurstProgress(life * 0.95f, life, curve) >= 1.0f, "curve %g: progress has not reached 1 at 95%% of the life (%g): fragments survive to retirement", curve, PrismBurstProgress(life * 0.95f, life, curve));
        float prev = -1;
        for (int k = 0; k <= 100; k++) { float pr = PrismBurstProgress(life * k / 100.0f, life, curve); CHECK(pr >= prev, "curve %g: progress not monotone", curve); prev = pr; }
    }
}

// 9b. a shed is the grade: its progress is the shipped grade's colour weight at every instant.
static void run_grade_tests() {
    const float overshoot = PRISM_BURST_DISSOLVE_OVERSHOOT;
    for (float duration : {0.8f, 0.35f, 2.0f}) {
        float life = duration * (1.0f + overshoot);
        float worst = 0;
        for (int k = 0; k <= 400; k++) {
            float age = duration * 1.2f * k / 400.0f;
            float4 bright, dark; float3 spread;
            PrismColorLerp_float(age, 0.0f, duration, float4(0,0,0,0), float4(0,0,0,0), float3(0,0,0),
                                 float4(1,1,1,1), float4(1,1,1,1), float3(1,1,1), bright, dark, spread);
            float err = std::fabs(PrismBurstProgress(age, life, 1.0f) - bright.x);
            if (err > worst) worst = err;
        }
        CHECK(worst <= 1e-4f, "duration %g: a shed's progress leaves the grade's course by %g", duration, worst);
    }
}

// ---- W. the WEDGE kind: every Sierpinski fragment moves by the explosion's own law ----
// The cut, built the way PrismSierpinskiTiling lays it out (a port; parity.py holds the lab's port to the C#).
struct WVert { float3 pos, n, centroid; float level; };
static void wSplit(float2 a, float2 b, float2 c, int level, int depth, std::vector<float>& out) {
    auto emit = [&](float2 x, float2 y, float2 z, int l) { float t[7] = {x.x,x.y,y.x,y.y,z.x,z.y,(float)l}; out.insert(out.end(), t, t + 7); };
    if (level >= depth) { emit(a, b, c, level); return; }
    float2 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
    emit(ab, bc, ca, level + 1);
    wSplit(a, ab, ca, level + 1, depth, out); wSplit(ab, b, bc, level + 1, depth, out); wSplit(ca, bc, c, level + 1, depth, out);
}
static std::vector<WVert> sierpinski(int depth) {
    float3 fn[6] = {float3(1,0,0),float3(-1,0,0),float3(0,1,0),float3(0,-1,0),float3(0,0,1),float3(0,0,-1)};
    float3 fu[6] = {float3(0,1,0),float3(0,0,1),float3(0,0,1),float3(1,0,0),float3(1,0,0),float3(0,1,0)};
    std::vector<float> t;
    float2 cs[4] = {float2(0,0), float2(1,0), float2(1,1), float2(0,1)};
    for (int k = 0; k < 4; k++) wSplit(float2(0.5f,0.5f), cs[k], cs[(k+1)%4], 0, depth, t);
    std::vector<WVert> out;
    for (int f = 0; f < 6; f++) {
        float3 fv = cross(fn[f], fu[f]);
        auto at = [&](float u, float v) { return fn[f]*0.5f + fu[f]*(u - 0.5f) + fv*(v - 0.5f); };
        for (size_t i = 0; i < t.size(); i += 7) {
            float cu = (t[i] + t[i+2] + t[i+4]) / 3.0f, cv = (t[i+1] + t[i+3] + t[i+5]) / 3.0f;
            for (int k = 0; k < 3; k++) { WVert w; w.pos = at(t[i+2*k], t[i+2*k+1]); w.n = fn[f]; w.centroid = at(cu, cv); w.level = t[i+6]; out.push_back(w); }
        }
    }
    return out;
}
// The harness's OWN statement of the explosion's law for a wedge (RotateFacesAlongAxis' closed form with the
// graph's constants pivot bias -0.5, slide bias 0.5, gain 0.5, written with Rodrigues' formula — not the
// shader's matrix): the reference level 0 must equal.
static float3 rodrigues(float3 v, float3 k, float a) {
    float l = length(k); if (l <= 0) return v; k = k / l;
    return v * std::cos(a) + cross(k, v) * std::sin(a) + k * (dot(k, v) * (1 - std::cos(a)));
}
static void explosionReference(float3 P, float3 N, float3 T, float3 os, float amount, float3 vel, float3 S, float ER, float ES, float3& pos, float3& nrm) {
    float3 pivot = N * dot(P, N) + (S + 0.5f) * T * 0.5f;
    float3 axis2 = cross(vel, N);
    float3 r = rodrigues(rodrigues((P - pivot) * os, T, amount * ER), axis2, amount * ER);
    nrm = rodrigues(rodrigues(N, T, amount * ER), axis2, amount * ER);
    pos = r / os + pivot + T * ((0.5f - 0.5f + amount * ES) * 0.5f);
}
static void run_wedge_tests() {
    const float3 S(0.18f, 0.1f, 0.1f); const float ER = 0.0169f, ES = 0.05f;
    float worstReach = 0, worstLevel0 = 0, worstSpin = 0, worstWedgeSpin = 0;
    for (int depth = 0; depth <= 3; depth++) {
        std::vector<WVert> m = sierpinski(depth);
        for (int trial = 0; trial < 8; trial++) {
            Pose P = rndPose(); P.t = float3(0,0,0);
            float3 vel = rndDir() * rnd(5, 60);
            float speed = rnd(10, 53);
            float R = length(linear(P, float3(0.5f,0.5f,0.5f)));
            float maxScale = std::max(P.s.x, std::max(P.s.y, P.s.z));
            for (float t : {0.0f, 0.4f, 2.0f, 7.5f}) {
                float amount = speed * t;
                std::vector<float3> wp(m.size());
                for (size_t i = 0; i < m.size(); i++) {
                    const WVert& v = m[i];
                    float3 pos, nrm;
                    PrismBurstWedgePose(v.pos, v.n, v.centroid, v.level, P.s, amount, vel, S, ER, ES, pos, nrm);
                    wp[i] = world(P, pos);
                    // W1. the first frame is the prism
                    if (amount == 0.0f) {
                        CHECK(length(pos - v.pos) <= 1e-5f && length(nrm - v.n) <= 1e-5f, "wedge depth %d: amount 0 moved a vertex", depth);
                    }
                    // W3. unit normals
                    CHECK(std::fabs(length(nrm) - 1.0f) <= 1e-4f, "wedge: normal length %g", length(nrm));
                    // W4. the reach the CPU sizes the bounds by
                    float reach = PrismBurstWedgeReach(R, maxScale, amount, ES, S);
                    float r = length(wp[i]);
                    if (r / reach > worstReach) worstReach = r / reach;
                    CHECK(r <= reach * (1 + 1e-4f), "wedge depth %d: a vertex %.3f escapes the reach %.3f", depth, r, reach);
                    // W5. the spin is the explosion's, capped by its speed: never more than two turns of amount x rotation
                    float turn = std::acos(std::min(1.0f, std::max(-1.0f, dot(nrm, v.n))));
                    if (amount > 0) worstSpin = std::max(worstSpin, turn / (2 * amount * ER));
                    CHECK(turn <= 2 * amount * ER + 1e-3f, "wedge: a fragment turned %g rad, more than the explosion's 2 x %g", turn, amount * ER);
                    // W7. every fragment SPINS as its root wedge does in the explosion: the same two axes (the wedge's
                    // tangent, cross(velocity, N)) and angle, so its normal is the explosion wedge's normal.
                    {
                        float3 off = v.centroid - v.n * dot(v.pos, v.n), a = abs(off);
                        float3 Tw = a.x >= a.y && a.x >= a.z ? float3(off.x >= 0 ? 1.f : -1.f, 0, 0)
                                  : a.y >= a.z ? float3(0, off.y >= 0 ? 1.f : -1.f, 0) : float3(0, 0, off.z >= 0 ? 1.f : -1.f);
                        float3 rp, rn;
                        explosionReference(v.pos, v.n, Tw, P.s, amount, vel, S, ER, ES, rp, rn);
                        worstWedgeSpin = std::max(worstWedgeSpin, length(rn - nrm));
                        CHECK(length(rn - nrm) <= 1e-4f, "wedge depth %d: a fragment turned off its root wedge's spin by %g", depth, length(rn - nrm));
                    }
                    // W2. level 0 IS the explosion
                    if (v.level == 0.0f) {
                        float3 T = normalize(v.centroid - v.n * dot(v.pos, v.n));
                        float3 rp, rn;
                        explosionReference(v.pos, v.n, T, P.s, amount, vel, S, ER, ES, rp, rn);
                        float err = length(world(P, rp) - world(P, pos)) / R;
                        worstLevel0 = std::max(worstLevel0, err);
                        CHECK(err <= 1e-4f && length(rn - nrm) <= 1e-4f, "wedge: level 0 leaves the explosion's law by %g radii", err);
                    }
                }
                // W6. every fragment is RIGID in the world, at every depth (the turn is done in the scaled frame)
                for (size_t i = 0; i < m.size(); i += 3)
                    for (int a = 0; a < 3; a++) {
                        int b = (a + 1) % 3;
                        float d0 = length(world(P, m[i+a].pos) - world(P, m[i+b].pos)), d1 = length(wp[i+a] - wp[i+b]);
                        CHECK(std::fabs(d0 - d1) <= 2e-4f * R, "wedge depth %d: a fragment stretched %g -> %g", depth, d0, d1);
                    }
            }
        }
    }
    printf("wedge: worst reach use %.3f, worst level-0 departure %.2g radii, worst spin %.3f of the explosion's cap, worst departure from the root wedge's spin %.2g\n", worstReach, worstLevel0, worstSpin, worstWedgeSpin);
}

// 10. voronoi; 11. unknown kind.
static void run_misc_tests() {
    for (int k = 0; k < 20000; k++) {
        float2 uv(rnd(0,1), rnd(0,1)); float3 key = rndDir() * 3.7f + rnd(0, 11);
        float2 seed; float border;
        PrismBurstVoronoi(uv, key, 3.0f, seed, border);
        CHECK(border >= -1e-5f, "a point lies outside its own Voronoi cell (%g)", border);
        CHECK(seed.x >= 0 && seed.x <= 1 && seed.y >= 0 && seed.y <= 1, "a seed left the face");
    }
    std::vector<Vert> m = mesh();
    Pose P = rndPose();
    for (int k = 0; k < 300; k++) {
        const Vert& v = m[rng() % m.size()];
        PrismBurstFrame f = frameOf(P, v, 0.37f);
        PrismBurstParams q = params(2.0f, 0.37f, 0.6f); q.speed = 20;
        float3 a, an, b, bn; float ha, hb;
        PrismBurstPose(0.0f, f, q, a, an, ha);
        PrismBurstPose(7.0f, f, q, b, bn, hb);
        CHECK(length(a - b) == 0.0f && ha == hb, "an unknown kind did not fall back to the shatter");
    }
}

int main(int argc, char** argv) {
    bool envelopeOnly = argc > 1 && std::strcmp(argv[1], "envelope") == 0;
    bool fadeOnly = argc > 1 && std::strcmp(argv[1], "fade") == 0;
    bool bendOnly = argc > 1 && std::strcmp(argv[1], "bend") == 0;
    bool gradeOnly = argc > 1 && std::strcmp(argv[1], "grade") == 0;
    bool wedgeOnly = argc > 1 && std::strcmp(argv[1], "wedge") == 0;
    if (envelopeOnly) run_pose_tests(true);
    else if (fadeOnly) run_fade_tests();
    else if (bendOnly) run_bend_tests();
    else if (gradeOnly) run_grade_tests();
    else if (wedgeOnly) run_wedge_tests();
    else { run_pose_tests(false); run_bend_tests(); run_fade_tests(); run_grade_tests(); run_wedge_tests(); run_misc_tests(); }
    printf("collision burst: %d failure(s)\n", failures);
    return failures ? 1 : 0;
}
"""


def grade_function():
    """The SHIPPED grade, PrismColorLerp_float, cut out of PrismClockAnimation.hlsl verbatim."""
    clock = open(CLOCK).read()
    m = re.search(r"void PrismColorLerp_float\(.*?\n}\n", clock, re.S)
    assert m, "PrismColorLerp_float missing from PrismClockAnimation.hlsl"
    return m.group(0)


def translate(src, required=True):
    """HLSL -> C++ spelling. Mechanical only."""
    out = src
    out = re.sub(r"\bout float4 (\w+)", r"float4 &\1", out)
    out = re.sub(r"\bout float3 (\w+)", r"float3 &\1", out)
    out = re.sub(r"\bout float2 (\w+)", r"float2 &\1", out)
    out = re.sub(r"\bout float (\w+)", r"float &\1", out)
    for name in ("void PrismBurstPose(", "void PrismBurstBend(", "void PrismBurstVoronoi(",
                 "float PrismBurstEnvelope(", "float PrismBurstProgress(", "float PrismBurstDissolveValue(",
                 "bool PrismBurstClipped(", "float PrismBurstNoise(", "void PrismBurstWedgePose(",
                 "float PrismBurstWedgeReach(", "void PrismBurstExplosionMap("):
        assert not required or name in out, f"{name} missing from the shipped HLSL"
    return out


def build_and_run(work, flags, arg, label):
    exe = os.path.join(work, "burst_" + re.sub(r"\W", "_", label))
    cmd = ["clang++", "-std=c++17", "-O2", "-ffp-contract=off", "-o", exe,
           os.path.join(work, "main.cpp"), "-I", work] + flags
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr)
        raise SystemExit(f"[{label}] compile failed")
    r = subprocess.run([exe] + ([arg] if arg else []), capture_output=True, text=True)
    return r.returncode, r.stdout.strip()


def main():
    keep = "--keep" in sys.argv
    for tool in ("clang++", "glslangValidator"):
        if shutil.which(tool) is None:
            raise SystemExit(f"{tool} not found")
    src = open(HLSL).read()
    shader = open(SHADER).read()
    work = tempfile.mkdtemp(prefix="verify_prism_collision_burst_")
    ok = True
    try:
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(SHIM)
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write('#pragma once\n#include "shim.h"\n' + translate(src) + "\n" + translate(grade_function(), required=False))
        with open(os.path.join(work, "main.cpp"), "w") as f:
            f.write(HARNESS)

        # A. execution
        rc, out = build_and_run(work, [], None, "shipped")
        print(out)
        ok &= rc == 0

        for flag, arg, label in (("-DPRISM_BURST_DISSOLVE_OVERSHOOT=0.0", "fade", "overshoot control"),
                                 ("-DPRISM_BURST_UNIFORM_VALUE=0", "fade", "uniform-value control"),
                                 ("-DPRISM_BURST_ENVELOPE_BASE=1.0", "envelope", "envelope control"),
                                 ("-DPRISM_BURST_BEND_NORMAL=0", "bend", "cheap-normal control"),
                                 ("-DPRISM_BURST_GRADE_SMOOTH=0", "grade", "linear-shed control"),
                                 ("-DPRISM_BURST_PIVOT_BIAS=0.0", "wedge", "wedge-pivot control"),
                                 ("-DPRISM_BURST_WEDGE_PIVOT_UNSCALED=1", "wedge", "unscaled-turn control"),
                                 ("-DPRISM_BURST_WEDGE_OWN_TANGENT=1", "wedge", "radial-tangent control")):
            rc, out = build_and_run(work, [flag], arg, label)
            fired = rc != 0
            print(f"negative control [{label}]: {'FIRED' if fired else 'DID NOT FIRE'} ({out.splitlines()[-1]})")
            ok &= fired

        # B. front-end compile
        with open(os.path.join(work, "urp_mock.hlsl"), "w") as f:
            f.write(slice_verifier.URP_MOCK)
        shutil.copy(HLSL, os.path.join(work, "PrismCollisionBurst.hlsl"))
        shutil.copy(os.path.join(GRAPHS, "PrismOcclusionCorridor.hlsl"),
                    os.path.join(work, "PrismOcclusionCorridor.hlsl"))
        include, passes = slice_verifier.extract_passes(shader)
        for vert, frag, prog in passes:
            for defines in ([], ["DOTS_INSTANCING_ON"], ["_GBUFFER_NORMALS_OCT"]):
                for stage, entry in (("vert", vert), ("frag", frag)):
                    rc, out = slice_verifier.glslang_compile(work, include, prog, stage, entry, defines)
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
