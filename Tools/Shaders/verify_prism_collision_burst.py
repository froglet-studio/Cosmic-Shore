#!/usr/bin/env python3
"""
Prove the SHIPPED PrismCollisionBurst.hlsl / .shader do what Docs/PRISM_ANIMATION.md §4.11 says a
collision burst does. Two halves, neither a transcription of the shader:

A. EXECUTION (clang++, /asset-surgery §4.5c). The file under Assets/ is translated mechanically
   (HLSL -> C++ spelling only) and RUN on randomized prisms (rotation, translation, non-uniform scale
   up to 20:1), every vertex of the unwelded high-poly mesh the burst draws, all four kinds:

   1. THE FIRST FRAME IS THE PRISM: at age 0 every vertex and normal of every kind is where the prism
      had it — the burst replaces the prism with no pop (continuity of existence).
   2. A TRIANGLE IS NEVER TORN: the three vertices of a triangle always name the same piece.
   3. RIGID PIECES STAY RIGID (shatter, glitter): every distance inside a piece is preserved at every
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
   9. GONE BEFORE RETIREMENT: progress reaches 1 before age reaches 95% of the life, and is monotone.
  10. VORONOI: every point lies inside its own cell (non-negative border) and seeds stay on the face.
  11. AN UNKNOWN KIND FALLS BACK to the shatter, never to garbage.
  NEGATIVE CONTROLS, each must FAIL the test it guards:
  12. -DPRISM_BURST_DISSOLVE_OVERSHOOT=0  — fragments survive to retirement (test 9 fires).
  13. -DPRISM_BURST_UNIFORM_VALUE=0       — the fade sags off the explosion's curve (test 8 fires).
  14. -DPRISM_BURST_ENVELOPE_BASE=1       — a pose escapes the bounds (test 6 fires).
  15. -DPRISM_BURST_BEND_NORMAL=0         — the bend keeps its rest normal (test 4 fires).

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
    return f;
}
static PrismBurstParams params(float age, float seed, float spread) {
    PrismBurstParams q;
    q.age = age; q.speed = rnd(10, 53); q.seed = seed; q.spread = spread; q.tau = 0.22f; q.spin = 0.3f;
    q.bloomTurns = 0.7f; q.unravelTurns = 0.9f; q.strips = 5; q.cells = 3;
    return q;
}

static void run_pose_tests(bool envelopeOnly) {
    std::vector<Vert> m = mesh();
    float worstCentroid[4] = {0,0,0,0};
    float worstEnvelope = 0;
    for (int trial = 0; trial < 24; trial++) {
        Pose P = rndPose();
        float seed = rnd(0, 1);
        float spread = rnd(0.2f, 1.5f);
        for (int kind = 0; kind < 4; kind++) {
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
                    // 3. rigid pieces stay rigid (shatter, glitter): the triangle's own edges
                    if (kind == 0 || kind == 3) {
                        for (int a = 0; a < 3; a++) {
                            int b = (a + 1) % 3;
                            float3 restA = world(P, m[i+a].pos), restB = world(P, m[i+b].pos);
                            float d0 = length(restA - restB), d1 = length(pos[i+a] - pos[i+b]);
                            CHECK(std::fabs(d0 - d1) <= 1e-4f * R, "kind %d: a rigid piece stretched %g -> %g", kind, d0, d1);
                        }
                    }
                }
                // 3b. rigid across a whole piece: pairs of triangles naming the same shard
                if (kind == 0) {
                    for (int k = 0; k < 400; k++) {
                        size_t i = (rng() % (m.size() / 3)) * 3, j = (rng() % (m.size() / 3)) * 3;
                        if (piece[i] != piece[j] || !(m[i].n.x == m[j].n.x && m[i].n.y == m[j].n.y && m[i].n.z == m[j].n.z)) continue;
                        float d0 = length(world(P, m[i].pos) - world(P, m[j].pos)), d1 = length(pos[i] - pos[j]);
                        CHECK(std::fabs(d0 - d1) <= 2e-4f * R, "shatter: a shard deformed %g -> %g", d0, d1);
                    }
                }
            }
        }
    }
    printf("worst envelope: %.3f radii; worst cloud-centre drift (radii) shatter %.3f bloom %.3f unravel %.3f glitter %.3f\n",
           worstEnvelope, worstCentroid[0], worstCentroid[1], worstCentroid[2], worstCentroid[3]);
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
                    float tilt[2];
                    for (int h = 0; h < 2; h++) {
                        float eps = (h == 0 ? 2e-2f : 1e-2f) * faceScale;
                        PrismBurstFrame g = f; g.rest = f.rest + dirs[d] * eps;
                        float3 p1, n1; float h1;
                        PrismBurstPose((float)kind, g, q, p1, n1, h1);
                        float3 step = p1 - p0;
                        float ratio = length(step) / eps;
                        CHECK(std::fabs(ratio - 1.0f) <= 2e-3f, "kind %d: the bend stretched a step by %g", kind, ratio);
                        tilt[h] = std::fabs(dot(normalize(step), n0));
                    }
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
    float life = 7.5f;
    CHECK(PrismBurstProgress(0.0f, life) == 0.0f, "progress at age 0 is not 0");
    CHECK(PrismBurstProgress(life * 0.95f, life) >= 1.0f, "progress has not reached 1 at 95%% of the life (%g): fragments survive to retirement", PrismBurstProgress(life * 0.95f, life));
    float prev = -1;
    for (int k = 0; k <= 100; k++) { float pr = PrismBurstProgress(life * k / 100.0f, life); CHECK(pr >= prev, "progress not monotone"); prev = pr; }
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
    if (envelopeOnly) run_pose_tests(true);
    else if (fadeOnly) run_fade_tests();
    else if (bendOnly) run_bend_tests();
    else { run_pose_tests(false); run_bend_tests(); run_fade_tests(); run_misc_tests(); }
    printf("collision burst: %d failure(s)\n", failures);
    return failures ? 1 : 0;
}
"""


def translate(src):
    """HLSL -> C++ spelling. Mechanical only."""
    out = src
    out = re.sub(r"\bout float3 (\w+)", r"float3 &\1", out)
    out = re.sub(r"\bout float2 (\w+)", r"float2 &\1", out)
    out = re.sub(r"\bout float (\w+)", r"float &\1", out)
    for name in ("void PrismBurstPose(", "void PrismBurstBend(", "void PrismBurstVoronoi(",
                 "float PrismBurstEnvelope(", "float PrismBurstProgress(", "float PrismBurstDissolveValue(",
                 "bool PrismBurstClipped(", "float PrismBurstNoise("):
        assert name in out, f"{name} missing from the shipped HLSL"
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
            f.write('#pragma once\n#include "shim.h"\n' + translate(src))
        with open(os.path.join(work, "main.cpp"), "w") as f:
            f.write(HARNESS)

        # A. execution
        rc, out = build_and_run(work, [], None, "shipped")
        print(out)
        ok &= rc == 0

        for flag, arg, label in (("-DPRISM_BURST_DISSOLVE_OVERSHOOT=0.0", "fade", "overshoot control"),
                                 ("-DPRISM_BURST_UNIFORM_VALUE=0", "fade", "uniform-value control"),
                                 ("-DPRISM_BURST_ENVELOPE_BASE=1.0", "envelope", "envelope control"),
                                 ("-DPRISM_BURST_BEND_NORMAL=0", "bend", "cheap-normal control")):
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
