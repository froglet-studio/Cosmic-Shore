#!/usr/bin/env python3
"""
Hold the COLLISION BURST LAB (Docs/Studios/PrismCollisionBurstLab.html) to the code the game ships.

The page claims it runs the shipped burst HLSL and the shipped explosion graph. This proves both, on
what the page's GPU actually computes (transform feedback reads its vertex stage back, through
Playwright + headless Chromium):

  0. FRESH: the page is the build of the current shipped sources (build.py --check).
  1. BURST: for random prisms (rotation, non-uniform scale up to 20:1), all six pose kinds (the roll, with a stamped camera) (destruction on
     the linear fade, sheds on the grade's curve and life, which drive the flake's wave), several ages,
     every vertex of the unwelded high-poly mesh — the page's pose and normal against the C++
     EXECUTION of the shipped PrismCollisionBurst.hlsl (translated exactly as
     Tools/Shaders/verify_prism_collision_burst.py translates it).
  2. EXPLOSION: for random velocities, speeds and times, every vertex of the 72-vertex Prism.asset —
     the page's closed form of RotateFacesAlongAxis against the shipped .shadersubgraph EVALUATED as a
     dataflow graph (Tools/Shaders/verify_prism_face_pivot.py's evaluator).
  3. THE SIERPINSKI CUT: the page's port of PrismSierpinskiTiling against the shipped C# file COMPILED and
     run with the .NET SDK — every triangle of a face at every depth, exactly; depth 0 against the 24
     triangles of Prism.asset (the explosion's own wedges); and the page's meshes: every depth covers the
     whole surface (area 6) with the C#'s triangle count, and no triangle of the shed meshes straddles
     either face diagonal (the union-jack cut).
  3a. THE SILVER GNOMON CUT: the page's port against PrismSilverGnomonTiling.cs compiled, every depth 0-12, as float32.
  3b. THE FRAGMENT (the destruction's motion since round 9, both cuts, the noise on and off, random rotations, scales
     and fragment shapes): the page's FRAGMENT pose of every vertex against the C++ EXECUTION of
     PrismBurstFragmentPose, in world space, the flight included. The cut's depth 0 still wears Prism.asset's UV0
     vertex for vertex (no shader reads it since round 9; the mesh keeps the explosion's layout).
  NEGATIVE CONTROLS (each must FAIL):
  4. the burst compared against the NEXT kind's C++ pose;
  5. the explosion compared against the graph run with ExplosiveRotation doubled;
  6. the page's cut compared against the C#'s at the NEXT depth;
  7. the page's fragment compared against the HLSL run with ExplosiveRotation doubled, and with the burst doubled;
  8. the cut's plain face coordinates (round 4's UV0) compared against Prism.asset's UVs.

    python3 Tools/Build/prism_collision_burst_lab/parity.py      # exit 0 = every check passed
"""

import json
import math
import os
import random
import shutil
import struct
import subprocess
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
HERE = os.path.dirname(os.path.abspath(__file__))
PAGE = os.path.join(ROOT, "Docs/Studios/PrismCollisionBurstLab.html")
sys.path.insert(0, os.path.join(ROOT, "Tools/Shaders"))
import verify_prism_collision_burst as burst_verifier  # noqa: E402
import verify_prism_face_pivot as graph_eval            # noqa: E402

# A vertex is "the same" within this many prism radii (positions) / this much (unit normals). Float32 on
# a software GPU against float32 C++: rounding, not structure.
POS_TOL = 2e-3
NRM_TOL = 2e-3
# No vertex may fall outside the tolerance. Three earlier misses were real portability defects in the
# shipped shader or the page, not rounding: a petal tie on the grid's anti-diagonal (now biased), a
# double-precision mesh in the page against C#'s float one (now Math.fround), and 1 - cos a over a tiny
# curvature (now 2 sin^2(a/2)). See Docs/Studios/COLLISION_BURST_LAB.md, Found.
OUTLIER_SHARE = 0.0

DRIVER = r"""
const { chromium } = require('playwright');
(async () => {
  const [page, casesPath] = process.argv.slice(2);
  const cases = JSON.parse(require('fs').readFileSync(casesPath, 'utf8'));
  const b = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  const p = await b.newPage();
  const errors = [];
  p.on('pageerror', (e) => errors.push(e.message));
  await p.goto('file://' + page);
  await p.waitForFunction(() => window.__lab && window.__lab.state);
  const out = await p.evaluate((cases) => {
    const L = window.__lab;
    if (L.state().glError) return { error: L.state().glError };
    const tiling = [0, 1, 2, 3, 4, 5].map((d) => L.sierpinskiTriangles(d));
    const silver = Array.from({ length: 13 }, (_, d) => L.silverTriangles(d));
    return { burst: cases.burst.map((c) => L.probeBurstCase(c)), explosion: cases.explosion.map((c) => L.probeExplosionCase(c)), fragment: cases.fragment.map((c) => L.probeFragmentCase(c)), tiling, silver, meshes: L.meshChecks(), sier0: L.sierpinskiMesh(0) };
  }, cases);
  out.pageErrors = errors;
  process.stdout.write(JSON.stringify(out));
  await b.close();
})();
"""

CPP_MAIN = r"""#include "shipped.h"
#include <cstdio>
#include <vector>
// Reads cases from stdin: kind age seed speed spread tau spin bloom unravel strips cells life curve rings segments
// noise camera(3) rot(9, column-major) scale(3) origin(3). Writes pos3 nrm3 per vertex of the unwelded mesh.
static const int N = 10;
int main() {
    int count; if (scanf("%d", &count) != 1) return 1;
    float3 fn[6] = {float3(1,0,0),float3(-1,0,0),float3(0,1,0),float3(0,-1,0),float3(0,0,1),float3(0,0,-1)};
    float3 fu[6] = {float3(0,1,0),float3(0,0,1),float3(0,0,1),float3(1,0,0),float3(1,0,0),float3(0,1,0)};
    float3 fv[6] = {float3(0,0,1),float3(0,1,0),float3(1,0,0),float3(0,0,1),float3(0,1,0),float3(1,0,0)};
    for (int c = 0; c < count; c++) {
        float kind, age, seed, speed, spread, tau, spin, bloom, unravel, strips, cells, life, curve, rings, segments, noise, cam[3], r[9], s[3], o[3];
        scanf("%f %f %f %f %f %f %f %f %f %f %f %f %f %f %f %f", &kind, &age, &seed, &speed, &spread, &tau, &spin, &bloom, &unravel, &strips, &cells, &life, &curve, &rings, &segments, &noise);
        for (int i = 0; i < 3; i++) scanf("%f", &cam[i]);
        for (int i = 0; i < 9; i++) scanf("%f", &r[i]);
        for (int i = 0; i < 3; i++) scanf("%f", &s[i]);
        for (int i = 0; i < 3; i++) scanf("%f", &o[i]);
        float3 c0(r[0], r[1], r[2]), c1(r[3], r[4], r[5]), c2(r[6], r[7], r[8]), sc(s[0], s[1], s[2]), org(o[0], o[1], o[2]);
        auto lin = [&](float3 v) { return c0 * (v.x * sc.x) + c1 * (v.y * sc.y) + c2 * (v.z * sc.z); };
        auto wld = [&](float3 v) { return org + lin(v); };
        auto wnm = [&](float3 n) { return normalize(c0 * (n.x / sc.x) + c1 * (n.y / sc.y) + c2 * (n.z / sc.z)); };
        PrismBurstParams q; q.age = age; q.speed = speed; q.seed = seed; q.spread = spread; q.tau = tau; q.spin = spin;
        q.bloomTurns = bloom; q.unravelTurns = unravel; q.strips = strips; q.cells = cells;
        q.rings = rings; q.segments = segments; q.noise = noise;
        q.camera = float3(cam[0], cam[1], cam[2]); q.life = life; q.subdivision = N; q.rollRadius = 0.05f; q.rollGap = 0.01f;
        q.progress = PrismBurstProgress(age, life, curve);
        float3 hx = lin(float3(0.5f, 0, 0)), hy = lin(float3(0, 0.5f, 0)), hz = lin(float3(0, 0, 0.5f));
        float3 lng = hx;
        if (dot(hy, hy) > dot(lng, lng)) lng = hy;
        if (dot(hz, hz) > dot(lng, lng)) lng = hz;
        for (int f = 0; f < 6; f++) for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) {
            float2 cq[4] = { float2((float)i/N,(float)j/N), float2((float)(i+1)/N,(float)j/N), float2((float)(i+1)/N,(float)(j+1)/N), float2((float)i/N,(float)(j+1)/N) };
            for (int t = 0; t < 2; t++) {
                float2 a = cq[0], b = t == 0 ? cq[1] : cq[2], d = t == 0 ? cq[2] : cq[3];
                float2 cen((a.x + b.x + d.x) / 3.0f, (a.y + b.y + d.y) / 3.0f);
                float2 vs[3] = {a, b, d};
                for (int k = 0; k < 3; k++) {
                    float3 p = fn[f] * 0.5f + fu[f] * (vs[k].x - 0.5f) + fv[f] * (vs[k].y - 0.5f);
                    PrismBurstFrame fr;
                    fr.rest = wld(p); fr.normal = wnm(fn[f]); fr.centre = wld(float3(0,0,0)); fr.faceCentre = wld(fn[f] * 0.5f);
                    fr.faceU = lin(fu[f] * 0.5f); fr.faceV = lin(cross(fn[f], fu[f]) * 0.5f);
                    fr.triUV = cen * 2.0f - float2(1, 1); fr.faceKey = fn[f] * 3.7f + seed * 11.0f;
                    fr.radius = length(lin(float3(0.5f, 0.5f, 0.5f)));
                    fr.longAxis = lng;
                    float3 pp, nn; float h;
                    PrismBurstPose(kind, fr, q, pp, nn, h);
                    printf("%.7g %.7g %.7g %.7g %.7g %.7g\n", pp.x, pp.y, pp.z, nn.x, nn.y, nn.z);
                }
            }
        }
    }
    return 0;
}
"""


FRAGMENT_MAIN = r"""#include "shipped.h"
#include <cstdio>
#include <cmath>
#include <vector>
@@SILVER_CPP@@
// Reads cases from stdin: cut (0 Sierpinski, 1 silver gnomon) depth t life speed velocity(3) scale(3) rot(9,
// column-major) origin(3) rotation tau shape(burst drift lift aim sizeSpeed sizeSpin spinCap levelRef endSmall endBig
// shrink speedGain speedRef variety) noise(spin amplitude, frequency, travel amplitude) seed. Writes the FRAGMENT pose (PrismBurstFragmentPose,
// the shipped HLSL executed) of every vertex of the cut at the depth, in HighPolyPrismMesh's order, in WORLD with the
// flight: BurstVert's own frame, as the shader builds it.
static void split(float2 a, float2 b, float2 c, int level, int depth, std::vector<float>& out) {
    auto emit = [&](float2 x, float2 y, float2 z, int l) { float t[7] = {x.x,x.y,y.x,y.y,z.x,z.y,(float)l}; out.insert(out.end(), t, t + 7); };
    if (level >= depth) { emit(a, b, c, level); return; }
    float2 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
    emit(ab, bc, ca, level + 1);
    split(a, ab, ca, level + 1, depth, out); split(ab, b, bc, level + 1, depth, out); split(ca, bc, c, level + 1, depth, out);
}
int main() {
    int count; if (scanf("%d", &count) != 1) return 1;
    float3 fn[6] = {float3(1,0,0),float3(-1,0,0),float3(0,1,0),float3(0,-1,0),float3(0,0,1),float3(0,0,-1)};
    float3 fu[6] = {float3(0,1,0),float3(0,0,1),float3(0,0,1),float3(1,0,0),float3(1,0,0),float3(0,1,0)};
    float3 fv[6] = {float3(0,0,1),float3(0,1,0),float3(1,0,0),float3(0,0,1),float3(0,1,0),float3(1,0,0)};
    for (int c = 0; c < count; c++) {
        float cut, depth, t, life, speed, v[3], sc[3], r[9], o[3], rotation, tau, sh[14], nz[3], seed;
        scanf("%f %f %f %f %f", &cut, &depth, &t, &life, &speed);
        for (int i = 0; i < 3; i++) scanf("%f", &v[i]);
        for (int i = 0; i < 3; i++) scanf("%f", &sc[i]);
        for (int i = 0; i < 9; i++) scanf("%f", &r[i]);
        for (int i = 0; i < 3; i++) scanf("%f", &o[i]);
        scanf("%f %f", &rotation, &tau);
        for (int i = 0; i < 14; i++) scanf("%f", &sh[i]);
        scanf("%f %f %f %f", &nz[0], &nz[1], &nz[2], &seed);
        std::vector<float> tri;
        if (cut > 0.5f) {
            double cu[4] = {0, 1, 1, 0}, cv[4] = {0, 0, 1, 1};
            const double h = 0.70710678118654757;
            int dd = (int)depth;
            for (int k = 0; k < 4; k++) {
                int n = (k + 1) % 4;
                if (dd <= 0) { silverEmit(0.5, 0.5, cu[k], cv[k], cu[n], cv[n], tri); continue; }
                double xu = cu[k] + (cu[n] - cu[k]) * h, xv = cv[k] + (cv[n] - cv[k]) * h;
                silverS(cu[k], cv[k], xu, xv, 0.5, 0.5, dd - 2, tri);
                silverG(cu[n], cv[n], 0.5, 0.5, xu, xv, dd - 1, tri);
            }
        } else {
            float2 cs[4] = {float2(0,0), float2(1,0), float2(1,1), float2(0,1)};
            for (int k = 0; k < 4; k++) split(float2(0.5f,0.5f), cs[k], cs[(k+1)%4], 0, (int)depth, tri);
        }
        float3 vel(v[0], v[1], v[2]), s3(sc[0], sc[1], sc[2]), org(o[0], o[1], o[2]);
        float3 c0(r[0], r[1], r[2]), c1(r[3], r[4], r[5]), c2(r[6], r[7], r[8]);
        auto lin = [&](float3 q) { return c0 * (q.x * s3.x) + c1 * (q.y * s3.y) + c2 * (q.z * s3.z); };
        auto wld = [&](float3 q) { return org + lin(q); };
        auto wnm = [&](float3 n) { return normalize(c0 * (n.x / s3.x) + c1 * (n.y / s3.y) + c2 * (n.z / s3.z)); };
        PrismBurstFragmentShape shape;
        shape.burst = sh[0]; shape.drift = sh[1]; shape.lift = sh[2]; shape.aim = sh[3]; shape.sizeSpeed = sh[4];
        shape.sizeSpin = sh[5]; shape.spinCap = sh[6]; shape.levelRef = sh[7]; shape.endSmall = sh[8]; shape.endBig = sh[9];
        shape.shrink = sh[10]; shape.speedGain = sh[11]; shape.speedRef = sh[12]; shape.variety = sh[13]; shape.tau = tau;
        float radius = length(lin(float3(0.5f, 0.5f, 0.5f)));
        for (int f = 0; f < 6; f++)
        for (size_t i = 0; i < tri.size(); i += 7) {
            float cu = (tri[i] + tri[i+2] + tri[i+4]) / 3.0f, cv = (tri[i+1] + tri[i+3] + tri[i+5]) / 3.0f;
            // the shader's own centroid: normal x 0.5 + u (cu - 0.5) + v (cv - 0.5), v = cross(N, u)
            float3 vf = cross(fn[f], fu[f]);
            float3 cen = fn[f]*0.5f + fu[f]*(cu - 0.5f) + vf*(cv - 0.5f);
            for (int k = 0; k < 3; k++) {
                float3 P = fn[f]*0.5f + fu[f]*(tri[i+2*k] - 0.5f) + fv[f]*(tri[i+2*k+1] - 0.5f);
                float3 pos, nrm;
                // length(triUV), as BurstVert passes it
                float tu = cu * 2.0f - 1.0f, tv = cv * 2.0f - 1.0f, outward = std::sqrt(tu * tu + tv * tv);
                PrismBurstFragmentPose(wld(P), wnm(fn[f]), wld(cen), wld(fn[f] * 0.5f), outward, cen, tri[i+6], radius, t, life,
                                       speed * t, vel, rotation, float4(nz[0], nz[1], seed, nz[2]), shape, pos, nrm);
                float3 w = pos + vel * t;
                printf("%.7g %.7g %.7g %.7g %.7g %.7g\n", w.x, w.y, w.z, nrm.x, nrm.y, nrm.z);
            }
        }
    }
    return 0;
}
"""


def rnd_rotation(rng):
    a = [rng.uniform(-1, 1) for _ in range(3)]
    n = math.sqrt(sum(x * x for x in a)); a = [x / n for x in a]
    b = [rng.uniform(-1, 1) for _ in range(3)]
    c = [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
    n = math.sqrt(sum(x * x for x in c)); c = [x / n for x in c]
    d = [c[1] * a[2] - c[2] * a[1], c[2] * a[0] - c[0] * a[2], c[0] * a[1] - c[1] * a[0]]
    return a + d + c   # columns: a, d, c — orthonormal


KINDS = (0, 1, 2, 3, 4, 6)   # shatter, bloom, unravel, glitter, flake, roll (the fragment has its own probe)
SHEDS = (1, 2, 4, 6)         # bloom, unravel, flake, roll — stamped with the grade's curve and life


def burst_cases(rng):
    cases = []
    for trial in range(6):
        rot = rnd_rotation(rng)
        base = rng.uniform(0.5, 4)
        scale = [base * rng.uniform(1, 20), base * rng.uniform(0.2, 3), base * rng.uniform(0.5, 6)]
        for kind in KINDS:
            shed = kind in SHEDS
            life = (0.8 * 1.6 if kind == 6 else 0.8 * 1.06) if shed else 7.5
            camera = [rng.uniform(-40, 40) for _ in range(3)]
            cases.append(dict(kind=kind, age=rng.choice([0.0, 0.07, 0.3, 0.6, 0.95, 1.2]) if kind == 6 else rng.choice([0.0, 0.07, 0.3, 0.6]) if shed else rng.choice([0.0, 0.07, 0.4, 1.6, 5.0]),
                              camera=camera, subdivision=10, rollRadius=0.05, rollGap=0.01,
                              seed=rng.random(), speed=rng.uniform(10, 33), spread=rng.uniform(0.2, 1.0), tau=0.22, spin=0.3,
                              bloomTurns=0.7, unravelTurns=0.8, strips=5, cells=3, life=life, curve=1.0 if shed else 0.0,
                              # Ring and segment counts whose boundaries no centroid of the 10x10 probe grid sits on
                              # (a tie would round differently on the GPU and the CPU, which no game mesh allows).
                              rings=rng.choice([1, 2, 5]), segments=rng.choice([1, 2, 5]), noise=rng.random(),
                              rot=rot, scale=scale, origin=[rng.uniform(-30, 30) for _ in range(3)]))
    return cases


def explosion_cases(rng):
    cases = []
    for _ in range(10):
        v = [rng.uniform(-30, 30) for _ in range(3)]
        cases.append(dict(velocity=v, speed=rng.uniform(10, 33), t=rng.uniform(0, 7.5),
                          scale=[rng.uniform(0.3, 8), rng.uniform(0.2, 3), rng.uniform(0.5, 6)],
                          spreadValue=[0.18, 0.1, 0.1], rotation=0.0169, spreadPerAmount=0.05))
    return cases


def run_cpp(work, cases, kind_shift=0):
    lines = [str(len(cases))]
    for c in cases:
        kind = KINDS[(KINDS.index(c["kind"]) + kind_shift) % len(KINDS)]
        lines.append(" ".join(str(x) for x in [kind, c["age"], c["seed"], c["speed"], c["spread"],
                                                 c["tau"], c["spin"], c["bloomTurns"], c["unravelTurns"], c["strips"], c["cells"],
                                                 c["life"], c["curve"], c["rings"], c["segments"], c["noise"]] + c["camera"]
                              + c["rot"] + c["scale"] + c["origin"]))
    r = subprocess.run([os.path.join(work, "burst_ref")], input="\n".join(lines), capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    vals = [list(map(float, l.split())) for l in r.stdout.strip().splitlines()]
    per = len(vals) // len(cases)
    return [vals[i * per:(i + 1) * per] for i in range(len(cases))]


SHIPPED_SHAPE = dict(burst=0.36, drift=0.216, lift=2.0, aim=0.95, sizeSpeed=0.8, sizeSpin=0.25, spinCap=1.6, levelRef=4.0,
                     endSmall=0.45, endBig=0.92, shrink=0.35, speedGain=1.65, speedRef=10.0, variety=0.5)
SHAPE_KEYS = ("burst", "drift", "lift", "aim", "sizeSpeed", "sizeSpin", "spinCap", "levelRef", "endSmall", "endBig", "shrink",
              "speedGain", "speedRef", "variety")


def fragment_cases(rng):
    cases = []
    # both cuts, the noise on and off, the shipped shape and random ones, random rotations and non-uniform scales
    for cut, depth, noise in (("sier", 0, 0.0), ("sier", 2, 0.15), ("sier", 3, 0.15), ("silver", 0, 0.15),
                              ("silver", 4, 0.0), ("silver", 6, 0.15), ("silver", 8, 0.3)):
        shape = dict(SHIPPED_SHAPE) if not cases else dict(
            burst=rng.uniform(0, 4), drift=rng.uniform(0, 2), lift=rng.uniform(0, 3), aim=rng.uniform(0, 2),
            sizeSpeed=rng.uniform(0, 1), sizeSpin=rng.uniform(0, 1), spinCap=rng.uniform(1, 3), levelRef=rng.uniform(1, 6),
            endSmall=rng.uniform(0.1, 0.5), endBig=rng.uniform(0.5, 0.95), shrink=rng.uniform(0.05, 1),
            speedGain=rng.uniform(0, 2), speedRef=rng.uniform(5, 20), variety=rng.uniform(0, 1))
        base = rng.uniform(0.5, 4)
        cases.append(dict(cut=cut, depth=depth, noise=[noise, 3.0, noise], seed=rng.random(),
                          t=rng.uniform(0.05, 7.0), life=7.5, speed=rng.uniform(10, 33), tau=0.22,
                          velocity=[rng.uniform(-30, 30) for _ in range(3)],
                          scale=[base * rng.uniform(1, 8), base * rng.uniform(0.2, 3), base * rng.uniform(0.5, 6)],
                          rot=rnd_rotation(rng), origin=[rng.uniform(-30, 30) for _ in range(3)],
                          rotation=0.0169, shape=shape))
    return cases


def run_fragment_cpp(work, cases, rotation_scale=1.0, burst_scale=1.0):
    lines = [str(len(cases))]
    for c in cases:
        sh = dict(c["shape"]); sh["burst"] *= burst_scale
        lines.append(" ".join(str(x) for x in [1 if c["cut"] == "silver" else 0, c["depth"], c["t"], c["life"], c["speed"]]
                              + c["velocity"] + c["scale"] + c["rot"] + c["origin"]
                              + [c["rotation"] * rotation_scale, c["tau"]] + [sh[k] for k in SHAPE_KEYS]
                              + c["noise"] + [c["seed"]]))
    r = subprocess.run([os.path.join(work, "fragment_ref")], input="\n".join(lines), capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    rows = [list(map(float, l.split())) for l in r.stdout.strip().splitlines()]
    # split by each case's own vertex count (3 x 24 x per-wedge triangles)
    out, i = [], 0
    for c in cases:
        def per(d):
            if c["cut"] == "silver":
                a, b = 1, 1
                for _ in range(d):
                    a, b = b, a + b
                return 1 if d <= 0 else b
            return 1 if d <= 0 else 1 + 3 * per(d - 1)
        n = 3 * 24 * per(c["depth"])
        out.append(rows[i:i + n]); i += n
    assert i == len(rows), f"the C++ wrote {len(rows)} vertices, the cases expect {i}"
    return out


def compare_rows(page, ref, cases):
    worst, bad, total = 0.0, 0, 0
    for c, (pos, nrm), rows in zip(cases, page, ref):
        radius = 0.5 * math.sqrt(sum(s * s for s in c["scale"]))
        # tolerance grows with how far the fragments are from the origin (float32 in both)
        reach = 1 + (math.sqrt(sum(x * x for x in c.get("origin", [0, 0, 0]))) + math.sqrt(sum(v * v for v in c["velocity"])) * c["t"]) / radius * 0.05
        for i, row in enumerate(rows):
            d = max(math.dist(pos[3 * i:3 * i + 3], row[0:3]) / radius, math.dist(nrm[3 * i:3 * i + 3], row[3:6]))
            total += 1
            if d > 2e-3 * reach:
                bad += 1
            else:
                worst = max(worst, d)
    return bad, total, worst


def uv_vs_prism(sier0):
    """The page's depth-0 cut wears Prism.asset's UV0 vertex for vertex (matched by triangle and position): the mesh
    keeps the explosion's layout (no shader reads it since round 9)."""
    key = lambda tri: frozenset(tuple(round(x, 5) for x in v) for v in tri)
    theirs = {}
    for t in range(0, len(MESH) // 11, 3):
        tri = [tuple(MESH[(t + j) * 11:(t + j) * 11 + 3]) for j in range(3)]
        theirs[key(tri)] = {tuple(round(x, 5) for x in tri[j]): MESH[(t + j) * 11 + 9:(t + j) * 11 + 11] for j in range(3)}
    pos, uv = sier0["pos"], sier0["uv"]
    bad, total = 0, 0
    for t in range(0, len(pos) // 3, 3):
        tri = [tuple(pos[(t + j) * 3:(t + j) * 3 + 3]) for j in range(3)]
        match = theirs.get(key(tri)) or {}
        for j in range(3):
            total += 1
            ref = match.get(tuple(round(x, 5) for x in tri[j]))
            if ref is None or abs(ref[0] - uv[(t + j) * 2]) > 1e-6 or abs(ref[1] - uv[(t + j) * 2 + 1]) > 1e-6:
                bad += 1
    return bad, total


def sierpinski_rest(depth):
    """The rest positions of the cut at a depth, in HighPolyPrismMesh.PopulateSierpinski's vertex order."""
    out = []
    cut = CUT[depth]
    for n, u, v in FACES:
        for k in range(0, len(cut), 7):
            for j in range(3):
                pu, pv = cut[k + 2 * j], cut[k + 2 * j + 1]
                out.append(tuple(0.5 * n[i] + u[i] * (pu - 0.5) + v[i] * (pv - 0.5) for i in range(3)))
    return out


CUT = None


def compare_burst(page, ref, cases):
    worst_p, worst_n, bad, total = 0.0, 0.0, 0, 0
    for c, (pos, nrm), rows in zip(cases, page, ref):
        radius = 0.5 * math.sqrt(sum(s * s for s in c["scale"]))
        for i, row in enumerate(rows):
            dp = math.dist(pos[3 * i:3 * i + 3], row[0:3]) / radius
            dn = math.dist(nrm[3 * i:3 * i + 3], row[3:6])
            total += 1
            if dp > POS_TOL or dn > NRM_TOL:
                bad += 1
            else:
                worst_p, worst_n = max(worst_p, dp), max(worst_n, dn)
    return bad, total, worst_p, worst_n


def graph_reference(c, rotation_scale=1.0):
    docs = graph_eval.load_docs(os.path.join(ROOT, graph_eval.SUBGRAPH))
    page_mesh = json.loads(json.dumps(MESH))
    out = []
    amount = c["speed"] * c["t"]
    for i in range(len(page_mesh) // 11):
        v = page_mesh[i * 11:(i + 1) * 11]
        g = graph_eval.Graph(docs, dict(normal=tuple(v[3:6]), tangent=tuple(v[6:9]), objectScale=tuple(c["scale"])))
        props = dict(Position=tuple(v[0:3]), velocity=tuple(c["velocity"]), SpreadValue=tuple(c["spreadValue"]),
                     ExplosionAmount=amount, ExplosiveRotation=c["rotation"] * rotation_scale,
                     ExplosiveSpread=c["spreadPerAmount"], FaceCentroid=(0.0, 0.0, 0.0), CentroidPivotWeight=0.0)
        pos = g.output(1, **props)
        nrm = g.output(2, **props)
        out.append(list(pos) + list(nrm))
    return out


def compare_explosion(page, cases, rotation_scale=1.0):
    worst, bad, total = 0.0, 0, 0
    for c, (pos, nrm) in zip(cases, page):
        ref = graph_reference(c, rotation_scale)
        for i, row in enumerate(ref):
            d = max(math.dist(pos[3 * i:3 * i + 3], row[0:3]), math.dist(nrm[3 * i:3 * i + 3], row[3:6]))
            total += 1
            if d > 2e-3 * (1 + c["speed"] * c["t"] * 0.05):
                bad += 1
            else:
                worst = max(worst, d)
    return bad, total, worst


MESH = None

TILING_CS = os.path.join(ROOT, "Assets/_Scripts/Utility/Foundation/PrismSierpinskiTiling.cs")
SILVER_CS = os.path.join(ROOT, "Assets/_Scripts/Utility/Foundation/PrismSilverGnomonTiling.cs")
TILING_MAIN = r"""
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CosmicShore.Utility;
var sb = new StringBuilder("{\"cut\":[");
for (int d = 0; d <= PrismSierpinskiTiling.MaxDepth; d++)
{
    var t = new List<float>();
    PrismSierpinskiTiling.Triangles(d, t);
    sb.Append(d == 0 ? "[" : ",[");
    for (int i = 0; i < t.Count; i++) sb.Append(i == 0 ? "" : ",").Append(t[i].ToString("G9", CultureInfo.InvariantCulture));
    sb.Append(']');
}
sb.Append("],\"silver\":[");
for (int d = 0; d <= PrismSilverGnomonTiling.MaxDepth; d++)
{
    var t = new List<float>();
    PrismSilverGnomonTiling.Triangles(d, t);
    sb.Append(d == 0 ? "[" : ",[");
    for (int i = 0; i < t.Count; i++) sb.Append(i == 0 ? "" : ",").Append(t[i].ToString("G9", CultureInfo.InvariantCulture));
    sb.Append(']');
}
sb.Append("],\"silverTriangles\":[");
for (int d = 0; d <= PrismSilverGnomonTiling.MaxDepth; d++) sb.Append(d == 0 ? "" : ",").Append(PrismSilverGnomonTiling.TriangleCount(d));
sb.Append("],\"triangles\":[");
for (int d = 0; d <= PrismSierpinskiTiling.MaxDepth; d++) sb.Append(d == 0 ? "" : ",").Append(PrismSierpinskiTiling.TriangleCount(d));
sb.Append("]}");
System.Console.Write(sb.ToString());
"""

FACES = [((1, 0, 0), (0, 1, 0), (0, 0, 1)), ((-1, 0, 0), (0, 0, 1), (0, 1, 0)), ((0, 1, 0), (0, 0, 1), (1, 0, 0)),
         ((0, -1, 0), (1, 0, 0), (0, 0, 1)), ((0, 0, 1), (1, 0, 0), (0, 1, 0)), ((0, 0, -1), (0, 1, 0), (1, 0, 0))]


def run_tiling_cs(work):
    """Compile the SHIPPED PrismSierpinskiTiling.cs with the .NET SDK and dump every depth's cut."""
    dotnet = shutil.which("dotnet") or os.path.expanduser("~/.dotnet/dotnet")
    proj = os.path.join(work, "tiling")
    os.makedirs(proj, exist_ok=True)
    with open(os.path.join(proj, "tiling.csproj"), "w") as f:
        f.write('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net8.0</TargetFramework><Nullable>disable</Nullable></PropertyGroup></Project>')
    shutil.copy(TILING_CS, os.path.join(proj, "PrismSierpinskiTiling.cs"))
    shutil.copy(SILVER_CS, os.path.join(proj, "PrismSilverGnomonTiling.cs"))
    with open(os.path.join(proj, "Program.cs"), "w") as f:
        f.write(TILING_MAIN)
    r = subprocess.run([dotnet, "run", "--project", proj], capture_output=True, text=True, timeout=600,
                       env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1"))
    assert r.returncode == 0, r.stdout + r.stderr
    return json.loads(r.stdout.strip().splitlines()[-1])


def same_cut(a, b):
    return len(a) == len(b) and all(abs(x - y) <= 1e-9 for x, y in zip(a, b))


def depth0_is_the_explosion(cut0, prism_mesh):
    """Depth 0 on all six faces, placed as HighPolyPrismMesh places it, against Prism.asset's 24 triangles."""
    key = lambda tri: frozenset(tuple(round(c, 5) for c in v) for v in tri)
    ours = set()
    for n, u, v in FACES:
        for k in range(0, len(cut0), 7):
            tri = []
            for j in range(3):
                pu, pv = cut0[k + 2 * j], cut0[k + 2 * j + 1]
                tri.append(tuple(0.5 * n[i] + u[i] * (pu - 0.5) + v[i] * (pv - 0.5) for i in range(3)))
            ours.add(key(tri))
    theirs = {key([tuple(prism_mesh[(t + j) * 11:(t + j) * 11 + 3]) for j in range(3)]) for t in range(0, len(prism_mesh) // 11, 3)}
    return ours == theirs, len(ours), len(theirs)


def main():
    global MESH
    ok = True
    r = subprocess.run([sys.executable, os.path.join(HERE, "build.py"), "--check"], capture_output=True, text=True)
    print(r.stdout.strip())
    ok &= r.returncode == 0

    sys.path.insert(0, HERE)
    import build
    MESH = build.prism_mesh()

    rng = random.Random(20261014)
    cases = {"burst": burst_cases(rng), "explosion": explosion_cases(rng), "fragment": fragment_cases(rng)}
    work = tempfile.mkdtemp(prefix="burst_lab_parity_")
    try:
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(burst_verifier.SHIM)
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write('#pragma once\n#include "shim.h"\n' + burst_verifier.translate(open(burst_verifier.HLSL).read()))
        with open(os.path.join(work, "main.cpp"), "w") as f:
            f.write(CPP_MAIN)
        cc = subprocess.run(["clang++", "-std=c++17", "-O1", "-ffp-contract=off", "-o", os.path.join(work, "burst_ref"),
                             os.path.join(work, "main.cpp"), "-I", work], capture_output=True, text=True)
        assert cc.returncode == 0, cc.stderr
        with open(os.path.join(work, "fragment.cpp"), "w") as f:
            f.write(FRAGMENT_MAIN.replace("@@SILVER_CPP@@", burst_verifier.SILVER_CPP))
        cc = subprocess.run(["clang++", "-std=c++17", "-O1", "-ffp-contract=off", "-o", os.path.join(work, "fragment_ref"),
                             os.path.join(work, "fragment.cpp"), "-I", work], capture_output=True, text=True)
        assert cc.returncode == 0, cc.stderr
        with open(os.path.join(work, "cases.json"), "w") as f:
            json.dump(cases, f)
        with open(os.path.join(work, "driver.cjs"), "w") as f:
            f.write(DRIVER)
        env = dict(os.environ)
        npm_root = subprocess.run(["npm", "root", "-g"], capture_output=True, text=True).stdout.strip()
        env["NODE_PATH"] = npm_root + (os.pathsep + env["NODE_PATH"] if env.get("NODE_PATH") else "")
        d = subprocess.run(["node", os.path.join(work, "driver.cjs"), PAGE, os.path.join(work, "cases.json")],
                           capture_output=True, text=True, env=env, timeout=600)
        assert d.returncode == 0, d.stderr
        page = json.loads(d.stdout)
        if page.get("error") or page.get("pageErrors"):
            print("PAGE ERROR:", page.get("error"), page.get("pageErrors"))
            return 1

        ref = run_cpp(work, cases["burst"])
        bad, total, wp, wn = compare_burst(page["burst"], ref, cases["burst"])
        share = bad / total
        good = share <= OUTLIER_SHARE
        print(f"burst: {total} vertices over {len(cases['burst'])} cases ({len(KINDS)} kinds, the roll included); worst {wp:.2e} radii / {wn:.2e} normal; "
              f"{bad} past tolerance ({share:.3%}) -> {'PASS' if good else 'FAIL'}")
        ok &= good

        ebad, etotal, ew = compare_explosion(page["explosion"], cases["explosion"])
        egood = ebad == 0
        print(f"explosion: {etotal} vertices over {len(cases['explosion'])} cases vs the shipped subgraph evaluated; "
              f"worst {ew:.2e}; {ebad} past tolerance -> {'PASS' if egood else 'FAIL'}")
        ok &= egood

        # the Sierpinski cut, against the C# compiled — and depth 0 against the explosion's own mesh
        cs = run_tiling_cs(work)
        global CUT
        CUT = cs["cut"]

        # the FRAGMENT kind: every depth against the shipped HLSL executed, in world space with the flight
        fref = run_fragment_cpp(work, cases["fragment"])
        wbad, wtotal, ww = compare_rows(page["fragment"], fref, cases["fragment"])
        wgood = wbad == 0
        print(f"fragment: {wtotal} vertices over {len(cases['fragment'])} cases (both cuts, the noise on and off, random shapes) "
              f"vs PrismBurstFragmentPose executed; worst {ww:.2e}; {wbad} past tolerance -> {'PASS' if wgood else 'FAIL'}")
        ok &= wgood
        ubad, utotal = uv_vs_prism(page["sier0"])
        ugood = ubad == 0 and utotal == 72
        print(f"cut UVs: depth 0's {utotal} vertices vs Prism.asset's UV0, triangle by triangle; {ubad} differ -> "
              f"{'PASS (the explosion layout)' if ugood else 'FAIL'}")
        ok &= ugood
        depths = len(cs["cut"])
        tmiss = sum(not same_cut(page["tiling"][d], cs["cut"][d]) for d in range(depths))
        ttris = sum(len(c) // 7 for c in cs["cut"])
        tgood = tmiss == 0
        print(f"sierpinski cut: {ttris} face triangles over depths 0-{depths - 1} vs PrismSierpinskiTiling.cs compiled; "
              f"{tmiss} depth(s) differ -> {'PASS' if tgood else 'FAIL'}")
        ok &= tgood
        # the silver gnomon cut: positions value for value (both double inside, float out); the size level, which
        # takes a log, to 1e-6
        # (the C# prints G9, which names a float32 exactly once rounded back to float32)
        f32 = lambda x: struct.unpack("<f", struct.pack("<f", x))[0]
        def same_silver(a, b):
            return len(a) == len(b) and all(abs(f32(x) - f32(y)) <= (1e-6 if i % 7 == 6 else 0.0) for i, (x, y) in enumerate(zip(a, b)))
        smiss = sum(not same_silver(page["silver"][d], cs["silver"][d]) for d in range(len(cs["silver"])))
        stris = sum(len(c) // 7 for c in cs["silver"])
        sgood = smiss == 0
        print(f"silver gnomon cut: {stris} face triangles over depths 0-{len(cs['silver']) - 1} vs PrismSilverGnomonTiling.cs compiled; "
              f"{smiss} depth(s) differ -> {'PASS' if sgood else 'FAIL'}")
        ok &= sgood
        same, n_ours, n_theirs = depth0_is_the_explosion(cs["cut"][0], MESH)
        print(f"depth 0 vs Prism.asset: {n_ours} triangles vs the explosion's {n_theirs} -> {'PASS (the same wedges)' if same else 'FAIL'}")
        ok &= same
        mg = page["meshes"]
        sier = sorted(k for k in mg if k.startswith("sier"))
        silv = sorted(k for k in mg if k.startswith("silver"))
        silver_depths = build.config()["silverDepths"]
        mgood = bool(sier) and all(abs(mg[k]["area"] - 6.0) < 1e-3 and mg[k]["triangles"] == cs["triangles"][int(k[4:])] for k in sier) \
            and bool(silv) and all(abs(mg[k]["area"] - 6.0) < 1e-3 and mg[k]["triangles"] == cs["silverTriangles"][silver_depths[int(k[6:]) - 1]] for k in silv) \
            and all(mg["shed" + str(l)]["straddles"] == 0 for l in (1, 2, 3))
        print("meshes: " + ", ".join(f"depth {k[4:]} {mg[k]['triangles']} tris area {mg[k]['area']}" for k in sier)
              + "; silver " + ", ".join(f"level {k[6:]} {mg[k]['triangles']} tris area {mg[k]['area']}" for k in silv)
              + "; shed " + ", ".join(f"{mg['shed' + str(l)]['subdivision']}: {mg['shed' + str(l)]['straddles']} straddles" for l in (1, 2, 3))
              + f" -> {'PASS' if mgood else 'FAIL'}")
        ok &= mgood

        # negative controls
        snext = sum(not same_silver(page["silver"][d], cs["silver"][(d + 1) % len(cs["silver"])]) for d in range(len(cs["silver"])))
        sfired = snext == len(cs["silver"])
        print(f"negative control [the next silver depth]: {snext} of {len(cs['silver'])} depths differ -> {'FIRED' if sfired else 'DID NOT FIRE'}")
        ok &= sfired
        tnext = sum(not same_cut(page["tiling"][d], cs["cut"][(d + 1) % depths]) for d in range(depths))
        tfired = tnext == depths
        print(f"negative control [the next depth's cut]: {tnext} of {depths} depths differ -> {'FIRED' if tfired else 'DID NOT FIRE'}")
        ok &= tfired
        nref = run_cpp(work, cases["burst"], kind_shift=1)
        nbad, ntotal, _, _ = compare_burst(page["burst"], nref, cases["burst"])
        fired = nbad / ntotal > 0.2
        print(f"negative control [next kind]: {nbad / ntotal:.1%} of vertices differ -> {'FIRED' if fired else 'DID NOT FIRE'}")
        ok &= fired
        # the cut's plain FACE coordinates (round 4's UV0) in place of the wedge UVs must not pass as Prism.asset's
        faceuv = []
        for t in range(len(page["sier0"]["pos"]) // 3):
            p3 = page["sier0"]["pos"][t * 3:t * 3 + 3]
            n, u, v = next(f for f in FACES if abs(sum(a * b for a, b in zip(f[0], p3)) - 0.5) < 1e-6)
            faceuv += [sum(a * b for a, b in zip(u, p3)) + 0.5, sum(a * b for a, b in zip(v, p3)) + 0.5]
        fbad, ftotal = uv_vs_prism({"pos": page["sier0"]["pos"], "uv": faceuv})
        ffired = fbad > 0
        print(f"negative control [face coordinates as UV0]: {fbad} of {ftotal} vertices differ -> {'FIRED' if ffired else 'DID NOT FIRE'}")
        ok &= ffired
        for label, kw in (("fragment, ExplosiveRotation x2", dict(rotation_scale=2.0)), ("fragment, burst x2", dict(burst_scale=2.0))):
            nwref = run_fragment_cpp(work, cases["fragment"], **kw)
            nwbad, nwtotal, _ = compare_rows(page["fragment"], nwref, cases["fragment"])
            nwfired = nwbad / nwtotal > 0.2
            print(f"negative control [{label}]: {nwbad / nwtotal:.1%} of vertices differ -> {'FIRED' if nwfired else 'DID NOT FIRE'}")
            ok &= nwfired
        xbad, xtotal, _ = compare_explosion(page["explosion"], cases["explosion"], rotation_scale=2.0)
        xfired = xbad / xtotal > 0.2
        print(f"negative control [ExplosiveRotation x2]: {xbad / xtotal:.1%} of vertices differ -> {'FIRED' if xfired else 'DID NOT FIRE'}")
        ok &= xfired
    finally:
        shutil.rmtree(work, ignore_errors=True)
    print("PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
