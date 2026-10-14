#!/usr/bin/env python3
"""
Compiles the SHIPPED OmniDepthPrismShader.shader's vertex functions (DepthPrismPosition and PrismPose, the
driven/undriven selection the vertex stage makes) with clang and runs them on the real prism meshes -
OmniCrystalTriangles.asset (the omni) and MassCrystalPrisms.asset (the Mass crystal) - with each crystal's own
material, then holds them against the Omni Shepard Lab's JS twin (window.__lab.vertexAt at the Shipped style,
the column place given) at several column places (/asset-surgery 4.5c, the same shape as
omni_shepard_shader_parity.py):

  1. LAB: every vertex at every place matches the lab. The shader works in SEATED units (the mesh at the
     prefab's scale is the crystal's own triangles) and the lab in its shell units (s from _Start 1 down to
     _Stop), so the shader's position x _Stop is the lab's. Since 2026-10-13 the thickness is BAKED into the
     shipped meshes and the materials carry _Thickness 1, while the lab still stretches its raw mesh by the
     pick (8 / 6.25): this check is what proves the bake is the shader's own stretch.
  2. FLUSH: sealed (u = 1), every top-face vertex sits exactly where the mesh has it - "Thickness grows inward".
  3. FUSION: CrystalHullFusion draws a re-tessellated copy of the crystal on this material with its vertices
     moved onto a hull, and TEXCOORD2 holding its discharge heights (zero on filler vertices). Undriven, the
     vertex stage must be the identity for ANY vertex and ANY TEXCOORD2: each moved vertex draws exactly where
     fusion put it. Also the formula itself (DepthPrismPosition, u = 1, real plate centres, thickness 1) is the
     identity up to float rounding: p = dir |c| + (v - c) = v.

Negative controls - each must FAIL: the thickness left in the shader (_Thickness = the baked value, so the
mesh is stretched twice) against the lab and against the fusion identity; _FaceSplit forced to 0 against the lab.

    python3 Tools/Build/omni_depth_prism_shader_parity.py          # exit 0 = every check passed
"""

import json
import math
import os
import random
import re
import struct
import subprocess
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from omni_shepard_shader_parity import SHIM  # noqa: E402  (the clang shim for HLSL-shaped C++)
from author_omni_crystal_triangles import DEPTH_THICKNESS  # noqa: E402
from author_mass_crystal_look import MASS_DEPTH_THICKNESS  # noqa: E402

SHADER = os.path.join(ROOT, "Assets/_Graphics/Materials/Shaders/OmniDepthPrismShader.shader")
MATS = os.path.join(ROOT, "Assets/_Graphics/Materials/CrystalMaterials")
LAB = os.path.join(ROOT, "Docs/Studios/OmniShepardLab.html")
CRYSTALS = [  # (label, mesh, material, lab subject, thickness baked into the mesh)
    ("omni", "Assets/_Models/OmniCrystalTriangles.asset", "OmniDepthPrisms", 0, DEPTH_THICKNESS),
    ("Mass", "Assets/_Models/MassCrystalPrisms.asset", "MassDepthPrisms", 2, MASS_DEPTH_THICKNESS),
]
PLACES = [0.0, 0.13, 0.5, 0.87, 1.0]
DIALS = ["_Reach", "_PlateScaleStart", "_Thickness", "_ThicknessInward", "_TopHeight", "_FaceSplit", "_SplitUnit"]
# The lab's meshes are baked at 5 decimals (+-5e-6 per coordinate); the shader stretches a prism's local offset
# by its thickness along the column and by _Reach at the top, so the rounding grows with both.
ROUND = 1e-5

EXTRA = r"""
static float sign(float x) { return x > 0 ? 1.0f : (x < 0 ? -1.0f : 0.0f); }
static float3 lerp(float3 a, float3 b, float t) { return a + (b - a) * t; }
static float4 mk4f(float3 v, float w) { return (float4){v.x, v.y, v.z, w}; }
#define float4(a, b) mk4f(a, b)
"""

MAIN = r"""
int main(int argc, char** argv) {
    FILE* in = std::fopen(argv[1], "rb");
    int n, nu; std::fread(&n, 4, 1, in); std::fread(&nu, 4, 1, in);
    float* V = (float*)std::malloc(sizeof(float) * n * 10);
    std::fread(V, sizeof(float), n * 10, in);          // per vertex: position, normal, TEXCOORD2 (xyzw)
    float* U = (float*)std::malloc(sizeof(float) * nu * 3);
    std::fread(U, sizeof(float), nu * 3, in);          // per run: (u, driven, leaving)
    std::fread(&_Reach, 4, 1, in); std::fread(&_PlateScaleStart, 4, 1, in); std::fread(&_Thickness, 4, 1, in);
    std::fread(&_ThicknessInward, 4, 1, in); std::fread(&_TopHeight, 4, 1, in); std::fread(&_FaceSplit, 4, 1, in);
    std::fread(&_SplitUnit, 4, 1, in);
    for (int k = 0; k < nu; k++)
        for (int i = 0; i < n; i++) {
            float* v = V + 10 * i;
            float u = U[3 * k];
            // The state CrystalDepthPrisms writes for a settled column at place u: (u, fade 1, u, -).
            float4 st = mk4(u, 1.0f, u, 0.0f);
            float4 q = PrismPose(mk3(v[0], v[1], v[2]), mk3(v[3], v[4], v[5]), mk4(v[6], v[7], v[8], v[9]), st,
                                 U[3 * k + 1], U[3 * k + 2]);
            std::printf("%.9g %.9g %.9g %.9g\n", q.x, q.y, q.z, q.w);
        }
    return 0;
}
"""


def extract_function(src, name):
    """The shipped function's text, brace-matched from its signature. Hard fail if it moved."""
    m = re.search(r"^\s*float[34] " + name + r"\(", src, re.M)
    if not m:
        raise SystemExit(f"{name} not found in the shader - the harness no longer covers it")
    i = src.index("{", m.end())
    depth = 0
    for j in range(i, len(src)):
        depth += {"{": 1, "}": -1}.get(src[j], 0)
        if depth == 0:
            return src[m.start():j + 1]
    raise SystemExit(f"{name}: unbalanced braces")


def material_dials(name):
    t = open(os.path.join(MATS, name + ".mat")).read()
    out = {}
    for d in DIALS:
        m = re.search(r"^\s+- " + re.escape(d) + r": (-?[0-9.eE+-]+)$", t, re.M)
        if not m:
            raise SystemExit(f"{name}.mat lacks {d}")
        out[d] = float(m.group(1))
    return out


def mesh_vertices(path):
    """(position, normal, TEXCOORD2 xyzw) per vertex, read through the asset's own channel table."""
    t = open(os.path.join(ROOT, path)).read()
    vd = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]+)", t).group(1))
    n = int(re.search(r"m_VertexCount: (\d+)", t).group(1))
    chans = [tuple(int(x) for x in c) for c in re.findall(
        r"- stream: (\d+)\n\s+offset: (\d+)\n\s+format: (\d+)\n\s+dimension: (\d+)",
        t.split("m_Channels:")[1].split("m_DataSize")[0])]
    stride = len(vd) // n
    if chans[6][3] != 4:
        raise SystemExit(f"{path}: TEXCOORD2 is not (centre xyz, plate index)")
    out = []
    for i in range(n):
        p = struct.unpack_from("<3f", vd, i * stride + chans[0][1])
        nn = struct.unpack_from("<3f", vd, i * stride + chans[1][1])
        c = struct.unpack_from("<4f", vd, i * stride + chans[6][1])
        out.append((p, nn, c))
    return out


def run_shader(fn_src, verts, runs, dials, work):
    """Each run is (u, driven, leaving); returns, per run, (x, y, z, alpha) per vertex. A bare float u is a
    driven arriving prism at that place."""
    runs = [(r, 1.0, 0.0) if isinstance(r, (int, float)) else r for r in runs]
    cpp = os.path.join(work, "prism.cpp")
    globals_ = "".join(f"static float {d};\n" for d in DIALS)
    with open(cpp, "w") as f:
        f.write(SHIM + EXTRA + globals_ + fn_src + MAIN)
    exe = os.path.join(work, "prism")
    r = subprocess.run(["clang++", "-std=c++17", "-O1", "-Wall", "-Wno-unused-function", "-Wno-unused-variable",
                        "-o", exe, cpp], capture_output=True, text=True)
    if r.returncode != 0 or r.stderr.strip():
        raise SystemExit("clang:\n" + r.stderr)
    data = os.path.join(work, "in.bin")
    with open(data, "wb") as f:
        f.write(struct.pack("<ii", len(verts), len(runs)))
        for p, nn, c in verts:
            f.write(struct.pack("<10f", *p, *nn, *(tuple(c) + (1.0,) * (4 - len(c)))))
        for run in runs:
            f.write(struct.pack("<3f", *run))
        f.write(struct.pack("<7f", *[dials[d] for d in DIALS]))
    out = subprocess.run([exe, data], capture_output=True, text=True, check=True).stdout.split("\n")
    rows = [tuple(float(x) for x in line.split()) for line in out if line.strip()]
    n = len(verts)
    return [rows[k * n:(k + 1) * n] for k in range(len(runs))]


def run_lab(subject, n, places):
    js = r"""
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  const p = await b.newPage();
  await p.goto('file://' + process.argv[2], { waitUntil: 'load' });
  const out = await p.evaluate(([subject, n, places]) => {
    const L = window.__lab;
    L.applyStyle(L.STYLES.find((s) => s.id === 'shipped'));
    L.setParam('subject', subject);
    const P = Object.assign({}, L.P), rows = [];
    for (const u of places) { const r = []; for (let i = 0; i < n; i++) r.push(L.vertexAt(P, i, 0, 0, null, u)); rows.push(r); }
    return { rows, stop: L.stopOf(P) };   // the crystal's own column: the Mass has its own seat since 2026-10-14
  }, [+process.argv[3], +process.argv[4], JSON.parse(process.argv[5])]);
  console.log(JSON.stringify(out));
  await b.close();
})();
"""
    with tempfile.NamedTemporaryFile("w", suffix=".cjs", delete=False) as f:
        f.write(js)
        path = f.name
    env = dict(os.environ)
    npm_root = subprocess.run(["npm", "root", "-g"], capture_output=True, text=True).stdout.strip()
    env["NODE_PATH"] = npm_root
    r = subprocess.run(["node", path, LAB, str(subject), str(n), json.dumps(places)], capture_output=True, text=True, env=env)
    os.unlink(path)
    if r.returncode != 0:
        raise SystemExit("lab:\n" + r.stderr)
    return json.loads(r.stdout)


def worst(shader_rows, lab_rows, stop):
    return max(max(abs(a * stop - b) for a, b in zip(s[:3], l)) for srow, lrow in zip(shader_rows, lab_rows)
               for s, l in zip(srow, lrow))


def _rotate(v, axis, angle):
    n = math.sqrt(sum(a * a for a in axis))
    k = [a / n for a in axis]
    c, s = math.cos(angle), math.sin(angle)
    d = sum(v[i] * k[i] for i in range(3))
    x = (k[1] * v[2] - k[2] * v[1], k[2] * v[0] - k[0] * v[2], k[0] * v[1] - k[1] * v[0])
    return tuple(v[i] * c + x[i] * s + k[i] * d * (1 - c) for i in range(3))


def fused(verts, seed):
    """The mesh moved the way CrystalHullFusion moves it: every face carried off by its own rigid motion (peel,
    flight onto a hull far from the crystal's origin), written relative to the hull. Three TEXCOORD2 cases: the
    real plate data, the fusion template's discharge heights (small, signed, w = 1 for a float3 channel), and
    zero (its filler vertices on a crystal with no discharge channels)."""
    rnd = random.Random(seed)
    moved = []
    for i, (p, nn, c) in enumerate(verts):
        face = i // 3
        frnd = random.Random(seed * 1000 + face)
        axis = (frnd.uniform(-1, 1), frnd.uniform(-1, 1), frnd.uniform(-1, 1))
        angle = frnd.uniform(-math.pi, math.pi)
        shift = (frnd.uniform(-40, 40), frnd.uniform(-40, 40), frnd.uniform(-40, 40))
        q = _rotate(p, axis, angle)
        moved.append(((q[0] + shift[0], q[1] + shift[1], q[2] + shift[2]), _rotate(nn, axis, angle), c))
    heights = [(q, nn, (rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), 1.0)) for q, nn, _ in moved]
    zero = [(q, nn, (0.0, 0.0, 0.0, 1.0)) for q, nn, _ in moved]
    return {"real plate data": moved, "discharge heights": heights, "zero (filler)": zero}


def identity_error(rows, verts):
    """Against the vertex AS THE SHADER RECEIVES IT (float32), so an exact identity reads 0."""
    err = 0.0
    for r, (p, _n, _c) in zip(rows, verts):
        if any(math.isnan(x) or math.isinf(x) for x in r[:3]):
            return float("inf")
        p32 = struct.unpack("<3f", struct.pack("<3f", *p))
        r32 = struct.unpack("<3f", struct.pack("<3f", *r[:3]))   # %.9g round-trips a float32 exactly
        err = max(err, max(abs(r32[k] - p32[k]) for k in range(3)))
    return err


def main():
    src = open(SHADER).read()
    fn = extract_function(src, "DepthPrismPosition") + "\n" + extract_function(src, "PrismPose")
    ok = True

    def check(name, cond, detail=""):
        nonlocal ok
        print(("PASS  " if cond else "FAIL  ") + name + (f"  ({detail})" if detail else ""))
        ok = ok and cond

    with tempfile.TemporaryDirectory() as work:
        for label, mesh, mat, subject, baked in CRYSTALS:
            verts = mesh_vertices(mesh)
            dials = material_dials(mat)
            check(f"{label}: {mat}.mat ships _Thickness 1 (the thickness is baked into the mesh)", dials["_Thickness"] == 1.0,
                  f"_Thickness {dials['_Thickness']}")
            lab = run_lab(subject, len(verts), PLACES)
            rows = run_shader(fn, verts, PLACES, dials, work)
            err = worst(rows, lab["rows"], lab["stop"])
            tol = ROUND * dials["_Reach"] * max(1.0, baked)
            check(f"{label}: shader (baked thickness {baked}) = lab (raw mesh x {baked}) at every vertex, places {PLACES}",
                  err < tol, f"max |diff| {err:.2e}, tol {tol:.1e}")
            # Flush: sealed, the top face is where the mesh has it.
            sealed = rows[PLACES.index(1.0)]
            top = 0.0
            for (p, _n, c), q in zip(verts, sealed):
                r = sum(x * x for x in c[:3]) ** 0.5
                d = [x / r for x in c[:3]]
                if sum((p[k] - c[k]) * d[k] for k in range(3)) > dials["_TopHeight"] - 1e-5:
                    top = max(top, max(abs(p[k] - q[k]) for k in range(3)))
            check(f"{label}: sealed, every top-face vertex is where the mesh has it", top < ROUND, f"max |diff| {top:.2e}")
            doubled = dict(dials, _Thickness=float(baked))
            for name, bent in (("thickness left in the shader (_Thickness %s on the baked mesh)" % baked, doubled),
                               ("_FaceSplit 0", dict(dials, _FaceSplit=0.0))):
                e = worst(run_shader(fn, verts, PLACES, bent, work), lab["rows"], lab["stop"])
                check(f"{label}: negative control - {name} must NOT match the lab", e > 1e-3, f"max |diff| {e:.2e}")

            # FUSION: undriven, any moved vertex with any TEXCOORD2 draws where fusion put it, arriving solid and
            # the leaving copy gone.
            radius = max(math.sqrt(sum(x * x for x in p)) for p, _n, _c in verts)
            for case, moved in fused(verts, 7).items():
                arriving, leaving = run_shader(fn, moved, [(1.0, 0.0, 0.0), (1.0, 0.0, 1.0)], dials, work)
                e = identity_error(arriving, moved)
                alphas = {r[3] for r in arriving}, {r[3] for r in leaving}
                check(f"{label}: fusion, undriven, TEXCOORD2 = {case}: every moved vertex draws where fusion put it",
                      e == 0.0 and alphas == ({1.0}, {0.0}), f"max |p - v| {e:.1e}, alpha arriving {alphas[0]} leaving {alphas[1]}")
            # Mutant: the undriven branch removed, so an undriven vertex runs the column math at u = 1 (the round-8
            # shader). A filler vertex with no plate centre then normalizes zero.
            early = re.search(r"\n\s*if \(driven <= 0\.5\) return [^\n]*", fn)
            if not early:
                raise SystemExit("PrismPose's undriven early return moved - update the mutant")
            mutant = fn.replace(early.group(0), "\n            if (driven <= 0.5) { st = mk4(1.0f, 1.0f, 1.0f, 0.0f); }")
            filler = fused(verts, 7)["zero (filler)"]
            e = identity_error(run_shader(mutant, filler, [(1.0, 0.0, 0.0)], dials, work)[0], filler)
            check(f"{label}: negative control - without the undriven identity, filler vertices do not draw where fusion put them",
                  e > 1e-3, f"max |p - v| {e:.2e}")
            moved = fused(verts, 7)["real plate data"]
            reach = max(math.sqrt(sum(x * x for x in p)) for p, _n, _c in moved)
            e = identity_error(run_shader(fn, moved, [(1.0, 1.0, 0.0)], dials, work)[0], moved)
            check(f"{label}: fusion, the formula itself (sealed, thickness 1, real centres) is the identity: dir|c| + (v - c) = v",
                  e < 1e-5 * reach, f"max |p - v| {e:.2e} on vertices up to {reach:.0f} from the origin")
            e = identity_error(run_shader(fn, moved, [(1.0, 1.0, 0.0)], doubled, work)[0], moved)
            check(f"{label}: negative control - thickness left in the shader blows the moved faces apart",
                  e > 0.5 * radius, f"max |p - v| {e:.2f} mesh units, crystal radius {radius:.2f}")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
