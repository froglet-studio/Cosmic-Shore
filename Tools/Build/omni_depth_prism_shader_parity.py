#!/usr/bin/env python3
"""
Compiles the SHIPPED OmniDepthPrismShader.shader's vertex function (DepthPrismPosition) with clang and runs
it on the real prism meshes - OmniCrystalTriangles.asset (the omni) and MassCrystalPrisms.asset (the Mass
crystal) - with each crystal's own material, then holds it against the Omni Shepard Lab's JS twin
(window.__lab.vertexAt at the Shipped style, the column place given) at several column places
(/asset-surgery 4.5c, the same shape as omni_shepard_shader_parity.py):

  1. LAB: every vertex at every place matches the lab. The shader works in SEATED units (the mesh at the
     prefab's scale is the crystal's own triangles) and the lab in its shell units (s from _Start 1 down to
     _Stop), so the shader's position x _Stop is the lab's.
  2. FLUSH: sealed (u = 1), every top-face vertex sits exactly where the mesh has it, at any thickness -
     "Thickness grows inward".
  3. INERT: undriven (CrystalDepthPrisms absent) the shader poses u = 1, which (1) and (2) already cover.

Negative controls - each must FAIL the lab check: _ThicknessInward forced to 0, _FaceSplit forced to 0.

    python3 Tools/Build/omni_depth_prism_shader_parity.py          # exit 0 = every check passed
"""

import json
import os
import re
import struct
import subprocess
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from omni_shepard_shader_parity import SHIM  # noqa: E402  (the clang shim for HLSL-shaped C++)

SHADER = os.path.join(ROOT, "Assets/_Graphics/Materials/Shaders/OmniDepthPrismShader.shader")
MATS = os.path.join(ROOT, "Assets/_Graphics/Materials/CrystalMaterials")
LAB = os.path.join(ROOT, "Docs/Studios/OmniShepardLab.html")
CRYSTALS = [  # (label, mesh, material, lab subject)
    ("omni", "Assets/_Models/OmniCrystalTriangles.asset", "OmniDepthPrisms", 0),
    ("Mass", "Assets/_Models/MassCrystalPrisms.asset", "MassDepthPrisms", 2),
]
PLACES = [0.0, 0.13, 0.5, 0.87, 1.0]
DIALS = ["_Reach", "_PlateScaleStart", "_Thickness", "_ThicknessInward", "_TopHeight", "_FaceSplit", "_SplitUnit"]
# The lab's meshes are baked at 5 decimals (+-5e-6 per coordinate); the shader stretches a prism's local offset
# by its thickness along the column and by _Reach at the top, so the rounding grows with both.
ROUND = 1e-5

EXTRA = r"""
static float sign(float x) { return x > 0 ? 1.0f : (x < 0 ? -1.0f : 0.0f); }
static float3 lerp(float3 a, float3 b, float t) { return a + (b - a) * t; }
"""

MAIN = r"""
int main(int argc, char** argv) {
    FILE* in = std::fopen(argv[1], "rb");
    int n, nu; std::fread(&n, 4, 1, in); std::fread(&nu, 4, 1, in);
    float* V = (float*)std::malloc(sizeof(float) * n * 9);
    std::fread(V, sizeof(float), n * 9, in);           // per vertex: position, normal, plate centre
    float* U = (float*)std::malloc(sizeof(float) * nu);
    std::fread(U, sizeof(float), nu, in);
    std::fread(&_Reach, 4, 1, in); std::fread(&_PlateScaleStart, 4, 1, in); std::fread(&_Thickness, 4, 1, in);
    std::fread(&_ThicknessInward, 4, 1, in); std::fread(&_TopHeight, 4, 1, in); std::fread(&_FaceSplit, 4, 1, in);
    std::fread(&_SplitUnit, 4, 1, in);
    for (int k = 0; k < nu; k++)
        for (int i = 0; i < n; i++) {
            float* v = V + 9 * i;
            float3 p = DepthPrismPosition(mk3(v[0], v[1], v[2]), mk3(v[3], v[4], v[5]), mk3(v[6], v[7], v[8]), U[k]);
            std::printf("%.9g %.9g %.9g\n", p.x, p.y, p.z);
        }
    return 0;
}
"""


def extract_function(src, name):
    """The shipped function's text, brace-matched from its signature. Hard fail if it moved."""
    m = re.search(r"^\s*float3 " + name + r"\(", src, re.M)
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
    """(position, normal, plate centre) per vertex, read through the asset's own channel table."""
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
        c = struct.unpack_from("<3f", vd, i * stride + chans[6][1])
        out.append((p, nn, c))
    return out


def run_shader(fn_src, verts, places, dials, work):
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
        f.write(struct.pack("<ii", len(verts), len(places)))
        for p, nn, c in verts:
            f.write(struct.pack("<9f", *p, *nn, *c))
        f.write(struct.pack(f"<{len(places)}f", *places))
        f.write(struct.pack("<7f", *[dials[d] for d in DIALS]))
    out = subprocess.run([exe, data], capture_output=True, text=True, check=True).stdout.split("\n")
    rows = [tuple(float(x) for x in line.split()) for line in out if line.strip()]
    n = len(verts)
    return [rows[k * n:(k + 1) * n] for k in range(len(places))]


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
    return { rows, stop: P.stop };
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
    return max(max(abs(a * stop - b) for a, b in zip(s, l)) for srow, lrow in zip(shader_rows, lab_rows)
               for s, l in zip(srow, lrow))


def main():
    src = open(SHADER).read()
    fn = extract_function(src, "DepthPrismPosition")
    ok = True

    def check(name, cond, detail=""):
        nonlocal ok
        print(("PASS  " if cond else "FAIL  ") + name + (f"  ({detail})" if detail else ""))
        ok = ok and cond

    with tempfile.TemporaryDirectory() as work:
        for label, mesh, mat, subject in CRYSTALS:
            verts = mesh_vertices(mesh)
            dials = material_dials(mat)
            lab = run_lab(subject, len(verts), PLACES)
            rows = run_shader(fn, verts, PLACES, dials, work)
            err = worst(rows, lab["rows"], lab["stop"])
            tol = ROUND * dials["_Reach"] * max(1.0, dials["_Thickness"])
            check(f"{label}: shader = lab at every vertex, places {PLACES}", err < tol, f"max |diff| {err:.2e}, tol {tol:.1e}")
            # Flush: sealed, the top face is where the mesh has it.
            sealed = rows[PLACES.index(1.0)]
            top = 0.0
            for (p, _n, c), q in zip(verts, sealed):
                r = sum(x * x for x in c) ** 0.5
                d = [x / r for x in c]
                if sum((p[k] - c[k]) * d[k] for k in range(3)) > dials["_TopHeight"] - 1e-5:
                    top = max(top, max(abs(p[k] - q[k]) for k in range(3)))
            check(f"{label}: sealed, every top-face vertex is where the mesh has it (thickness {dials['_Thickness']} inward)",
                  top < ROUND, f"max |diff| {top:.2e}")
            for dial, value in (("_ThicknessInward", 0.0), ("_FaceSplit", 0.0)):
                bent = dict(dials, **{dial: value})
                e = worst(run_shader(fn, verts, PLACES, bent, work), lab["rows"], lab["stop"])
                check(f"{label}: negative control - {dial} {value} must NOT match the lab", e > 1e-3, f"max |diff| {e:.2e}")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
