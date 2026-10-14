#!/usr/bin/env python3
"""
Hold the COLLISION BURST LAB (Docs/Studios/PrismCollisionBurstLab.html) to the code the game ships.

The page claims it runs the shipped burst HLSL and the shipped explosion graph. This proves both, on
what the page's GPU actually computes (transform feedback reads its vertex stage back, through
Playwright + headless Chromium):

  0. FRESH: the page is the build of the current shipped sources (build.py --check).
  1. BURST: for random prisms (rotation, non-uniform scale up to 20:1), all five kinds (destruction on
     the linear fade, sheds on the grade's curve and life, which drive the flake's wave), several ages,
     every vertex of the unwelded high-poly mesh — the page's pose and normal against the C++
     EXECUTION of the shipped PrismCollisionBurst.hlsl (translated exactly as
     Tools/Shaders/verify_prism_collision_burst.py translates it).
  2. EXPLOSION: for random velocities, speeds and times, every vertex of the 72-vertex Prism.asset —
     the page's closed form of RotateFacesAlongAxis against the shipped .shadersubgraph EVALUATED as a
     dataflow graph (Tools/Shaders/verify_prism_face_pivot.py's evaluator).
  NEGATIVE CONTROLS (each must FAIL):
  3. the burst compared against the NEXT kind's C++ pose;
  4. the explosion compared against the graph run with ExplosiveRotation doubled.

    python3 Tools/Build/prism_collision_burst_lab/parity.py      # exit 0 = every check passed
"""

import json
import math
import os
import random
import shutil
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
    return { burst: cases.burst.map((c) => L.probeBurstCase(c)), explosion: cases.explosion.map((c) => L.probeExplosionCase(c)) };
  }, cases);
  out.pageErrors = errors;
  process.stdout.write(JSON.stringify(out));
  await b.close();
})();
"""

CPP_MAIN = r"""#include "shipped.h"
#include <cstdio>
#include <vector>
// Reads cases from stdin: kind age seed speed spread tau spin bloom unravel strips cells life curve
// rot(9, column-major) scale(3) origin(3). Writes pos3 nrm3 per vertex of the unwelded mesh.
static const int N = 10;
int main() {
    int count; if (scanf("%d", &count) != 1) return 1;
    float3 fn[6] = {float3(1,0,0),float3(-1,0,0),float3(0,1,0),float3(0,-1,0),float3(0,0,1),float3(0,0,-1)};
    float3 fu[6] = {float3(0,1,0),float3(0,0,1),float3(0,0,1),float3(1,0,0),float3(1,0,0),float3(0,1,0)};
    float3 fv[6] = {float3(0,0,1),float3(0,1,0),float3(1,0,0),float3(0,0,1),float3(0,1,0),float3(1,0,0)};
    for (int c = 0; c < count; c++) {
        float kind, age, seed, speed, spread, tau, spin, bloom, unravel, strips, cells, life, curve, r[9], s[3], o[3];
        scanf("%f %f %f %f %f %f %f %f %f %f %f %f %f", &kind, &age, &seed, &speed, &spread, &tau, &spin, &bloom, &unravel, &strips, &cells, &life, &curve);
        for (int i = 0; i < 9; i++) scanf("%f", &r[i]);
        for (int i = 0; i < 3; i++) scanf("%f", &s[i]);
        for (int i = 0; i < 3; i++) scanf("%f", &o[i]);
        float3 c0(r[0], r[1], r[2]), c1(r[3], r[4], r[5]), c2(r[6], r[7], r[8]), sc(s[0], s[1], s[2]), org(o[0], o[1], o[2]);
        auto lin = [&](float3 v) { return c0 * (v.x * sc.x) + c1 * (v.y * sc.y) + c2 * (v.z * sc.z); };
        auto wld = [&](float3 v) { return org + lin(v); };
        auto wnm = [&](float3 n) { return normalize(c0 * (n.x / sc.x) + c1 * (n.y / sc.y) + c2 * (n.z / sc.z)); };
        PrismBurstParams q; q.age = age; q.speed = speed; q.seed = seed; q.spread = spread; q.tau = tau; q.spin = spin;
        q.bloomTurns = bloom; q.unravelTurns = unravel; q.strips = strips; q.cells = cells;
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


def rnd_rotation(rng):
    a = [rng.uniform(-1, 1) for _ in range(3)]
    n = math.sqrt(sum(x * x for x in a)); a = [x / n for x in a]
    b = [rng.uniform(-1, 1) for _ in range(3)]
    c = [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]
    n = math.sqrt(sum(x * x for x in c)); c = [x / n for x in c]
    d = [c[1] * a[2] - c[2] * a[1], c[2] * a[0] - c[0] * a[2], c[0] * a[1] - c[1] * a[0]]
    return a + d + c   # columns: a, d, c — orthonormal


KINDS = 5            # shatter, bloom, unravel, glitter, flake
SHEDS = (1, 2, 4)    # bloom, unravel, flake — stamped with the grade's curve and life


def burst_cases(rng):
    cases = []
    for trial in range(6):
        rot = rnd_rotation(rng)
        base = rng.uniform(0.5, 4)
        scale = [base * rng.uniform(1, 20), base * rng.uniform(0.2, 3), base * rng.uniform(0.5, 6)]
        for kind in range(KINDS):
            shed = kind in SHEDS
            life = 0.8 * 1.06 if shed else 7.5
            cases.append(dict(kind=kind, age=rng.choice([0.0, 0.07, 0.3, 0.6]) if shed else rng.choice([0.0, 0.07, 0.4, 1.6, 5.0]),
                              seed=rng.random(), speed=rng.uniform(10, 33), spread=rng.uniform(0.2, 1.0), tau=0.22, spin=0.3,
                              bloomTurns=0.7, unravelTurns=0.8, strips=5, cells=3, life=life, curve=1.0 if shed else 0.0,
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
        lines.append(" ".join(str(x) for x in [(c["kind"] + kind_shift) % KINDS, c["age"], c["seed"], c["speed"], c["spread"],
                                                 c["tau"], c["spin"], c["bloomTurns"], c["unravelTurns"], c["strips"], c["cells"],
                                                 c["life"], c["curve"]]
                              + c["rot"] + c["scale"] + c["origin"]))
    r = subprocess.run([os.path.join(work, "burst_ref")], input="\n".join(lines), capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    vals = [list(map(float, l.split())) for l in r.stdout.strip().splitlines()]
    per = len(vals) // len(cases)
    return [vals[i * per:(i + 1) * per] for i in range(len(cases))]


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
    cases = {"burst": burst_cases(rng), "explosion": explosion_cases(rng)}
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
        print(f"burst: {total} vertices over {len(cases['burst'])} cases (5 kinds); worst {wp:.2e} radii / {wn:.2e} normal; "
              f"{bad} past tolerance ({share:.3%}) -> {'PASS' if good else 'FAIL'}")
        ok &= good

        ebad, etotal, ew = compare_explosion(page["explosion"], cases["explosion"])
        egood = ebad == 0
        print(f"explosion: {etotal} vertices over {len(cases['explosion'])} cases vs the shipped subgraph evaluated; "
              f"worst {ew:.2e}; {ebad} past tolerance -> {'PASS' if egood else 'FAIL'}")
        ok &= egood

        # negative controls
        nref = run_cpp(work, cases["burst"], kind_shift=1)
        nbad, ntotal, _, _ = compare_burst(page["burst"], nref, cases["burst"])
        fired = nbad / ntotal > 0.2
        print(f"negative control [next kind]: {nbad / ntotal:.1%} of vertices differ -> {'FIRED' if fired else 'DID NOT FIRE'}")
        ok &= fired
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
