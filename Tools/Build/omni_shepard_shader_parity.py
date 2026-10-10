#!/usr/bin/env python3
"""
Compiles the SHIPPED OmniShepardFresnelShader.shader with clang and runs its vert() on the real
OmniCrystalTriangles.asset, then holds it against two references (/asset-surgery 4.5c):

  1. INERT: with the round-4 dials at their defaults (_Breathe 0, _PhaseOffset 0, _PlateScaleStart 1,
     _Thickness 1) every vertex is v * ShepardBand(), the pre-round-4 formula, so the Mass crystal's
     shells (which author none of them) draw exactly as before. Checked on the real mesh WITH its
     plate centres and again with them zeroed (a mesh without TEXCOORD2).
  2. LAB: with each shipped OmniShepardTriangles k.mat's own values, vert() matches the Omni
     Shepard Lab's JS twin (window.__lab.vertexAt at the shipped settings, layer k) at several times.
     The lab is driven headless through Playwright.

Negative controls: the lab compared against a shader run with _Breathe forced off, and the inert
check run with _Thickness 1.25, must both FAIL.

    python3 Tools/Build/omni_shepard_shader_parity.py          # exit 0 = every check passed
"""

import json
import os
import re
import struct
import subprocess
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SHADER = os.path.join(ROOT, "Assets/_Graphics/Materials/Shaders/OmniShepardFresnelShader.shader")
MESH = os.path.join(ROOT, "Assets/_Models/OmniCrystalTriangles.asset")
MATS = os.path.join(ROOT, "Assets/_Graphics/Materials/CrystalMaterials")
LAB = os.path.join(ROOT, "Docs/Studios/OmniShepardLab.html")
TIMES = [0.5, 1.7, 2.95, 4.2, 7.7, 9.9, 13.3]
TOL = 2e-5          # object-space units on a ~1.6-unit mesh
DIALS = ["_Start", "_Stop", "_Period", "_ScaleDistance", "_Breathe", "_PhaseOffset", "_PlateScaleStart", "_Thickness"]
DEFAULTS = {"_Breathe": 0.0, "_PhaseOffset": 0.0, "_PlateScaleStart": 1.0, "_Thickness": 1.0}

SHIM = r"""
#include <cmath>
#include <cstdio>
#include <cstdlib>
typedef float float2 __attribute__((ext_vector_type(2)));
typedef float float3 __attribute__((ext_vector_type(3)));
typedef float float4 __attribute__((ext_vector_type(4)));
typedef float4 half4; typedef float half;
static float3 mk3(float a, float b, float c) { return (float3){a, b, c}; }
static float4 mk4(float3 v, float w) { return (float4){v.x, v.y, v.z, w}; }
static float4 mk4(float a, float b, float c, float d) { return (float4){a, b, c, d}; }
// HLSL-shaped scalar overloads (C's integer abs would truncate silently - /asset-surgery 4.5c).
static float abs(float x) { return std::fabs(x); }
static float abs(double x) { return (float)std::fabs(x); }   // HLSL literals are float; C++ makes 1.0 a double
static float3 abs(float3 v) { return mk3(std::fabs(v.x), std::fabs(v.y), std::fabs(v.z)); }
static float min(float a, float b) { return a < b ? a : b; }
static float max(float a, float b) { return a > b ? a : b; }
static float frac(float x) { return x - std::floor(x); }
static float fmod(float a, float b) { return std::fmod(a, b); }
static float saturate(float x) { return x < 0 ? 0 : x > 1 ? 1 : x; }
static float lerp(float a, float b, float t) { return a + (b - a) * t; }
static float4 lerp(float4 a, float4 b, float t) { return a + (b - a) * t; }
static float dot(float3 a, float3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
static float length(float3 v) { return std::sqrt(dot(v, v)); }
static float3 normalize(float3 v) { return v / length(v); }
static float pow(float a, float b) { return std::pow(a, b); }
static void clip(float) {}
// Object space in, object space out: the harness compares positions BEFORE the transform.
static float4 UnityObjectToClipPos(float4 v) { return v; }
static float4 _Time;
static float3 _WorldSpaceCameraPos;
#define CBUFFER
"""

MAIN = r"""
int main(int argc, char** argv) {
    FILE* in = std::fopen(argv[1], "rb");
    int n; std::fread(&n, 4, 1, in);
    float* P = (float*)std::malloc(sizeof(float) * n * 9);
    std::fread(P, sizeof(float), n * 9, in);   // per vertex: position, normal, plate centre
    int nt; std::fread(&nt, 4, 1, in);
    float* T = (float*)std::malloc(sizeof(float) * nt);
    std::fread(T, sizeof(float), nt, in);
    float dial[8]; std::fread(dial, sizeof(float), 8, in);
    std::fclose(in);
    _Start = dial[0]; _Stop = dial[1]; _Period = dial[2]; _ScaleDistance = dial[3];
    _Breathe = dial[4]; _PhaseOffset = dial[5]; _PlateScaleStart = dial[6]; _Thickness = dial[7];
    _Opacity = 1; _RimPower = 1; _FaceForward = 0;
    for (int k = 0; k < nt; k++) {
        _Time = mk4(T[k] / 20, T[k], T[k] * 2, T[k] * 3);
        for (int i = 0; i < n; i++) {
            appdata v;
            v.vertex = mk4(P[9 * i], P[9 * i + 1], P[9 * i + 2], 1);
            v.normal = mk3(P[9 * i + 3], P[9 * i + 4], P[9 * i + 5]);
            v.plateCentre = mk3(P[9 * i + 6], P[9 * i + 7], P[9 * i + 8]);
            v2f o = vert(v);
            std::printf("%.9g %.9g %.9g %.9g\n", o.vertex.x, o.vertex.y, o.vertex.z, o.s);
        }
    }
    return 0;
}
"""


def extract_hlsl():
    src = open(SHADER).read()
    body = src[src.index("CGPROGRAM") + len("CGPROGRAM"):src.index("ENDCG")]
    body = re.sub(r"^\s*#pragma.*$", "", body, flags=re.M)
    body = re.sub(r'^\s*#include "UnityCG.cginc".*$', "", body, flags=re.M)
    # The short, listed substitutions (keep it short: everything else must compile unmodified).
    subs = [
        (r"(\w+\s+\w+)\s*:\s*[A-Z_]+[0-9]*\s*;", r"\1;"),                  # struct member semantics
        (r"\)\s*:\s*SV_Target", ")"),                                        # entry-point semantic
        (r"mul\(\(float3x3\)UNITY_MATRIX_M,\s*v\.normal\)", "v.normal"),     # object = world (identity)
        (r"mul\(unity_ObjectToWorld,\s*v\.vertex\)\.xyz", "v.vertex.xyz"),
        (r"\bhalf4\(", "mk4("), (r"\bfloat4\(", "mk4("), (r"\bfloat3\(", "mk3("),
    ]
    for pat, rep in subs:
        body = re.sub(pat, rep, body)
    return body


def mesh_vertices(zero_centres=False):
    t = open(MESH).read()
    vd = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]+)", t).group(1))
    n = int(re.search(r"m_VertexCount: (\d+)", t).group(1))
    chans = [tuple(int(x) for x in c) for c in re.findall(
        r"- stream: (\d+)\n\s+offset: (\d+)\n\s+format: (\d+)\n\s+dimension: (\d+)",
        t.split("m_Channels:")[1].split("m_DataSize")[0])]
    stride = len(vd) // n
    if not (chans[6][3] == 3 and chans[6][2] == 0):
        raise SystemExit("OmniCrystalTriangles.asset has no float3 TEXCOORD2 (plate centre) - re-run author_omni_crystal_triangles.py")
    out = []
    for i in range(n):
        p = struct.unpack_from("<3f", vd, i * stride + chans[0][1])
        nm = struct.unpack_from("<3f", vd, i * stride + chans[1][1])
        c = (0.0, 0.0, 0.0) if zero_centres else struct.unpack_from("<3f", vd, i * stride + chans[6][1])
        out.append(p + nm + c)
    return out


def material(k):
    t = open(os.path.join(MATS, f"OmniShepardTriangles {k}.mat")).read()
    f = {a: float(b) for a, b in re.findall(r"^\s+- (_\w+): (-?[0-9.eE+-]+)\s*$", t, re.M)}
    return [f.get(d, DEFAULTS.get(d, 0.0)) for d in DIALS]


def build(tmp):
    cpp = os.path.join(tmp, "shader.cpp")
    with open(cpp, "w") as fh:
        fh.write(SHIM + extract_hlsl() + MAIN)
    exe = os.path.join(tmp, "shader")
    r = subprocess.run(["clang++", "-std=c++17", "-O1", "-Wall", "-Wno-unused-function", "-Wno-unused-variable",
                        cpp, "-o", exe], capture_output=True, text=True)
    if r.returncode != 0 or r.stderr.strip():
        raise SystemExit("clang++ on the shipped shader:\n" + r.stderr)
    return exe


def run(exe, tmp, verts, dials):
    path = os.path.join(tmp, "in.bin")
    with open(path, "wb") as fh:
        fh.write(struct.pack("<i", len(verts)))
        for v in verts:
            fh.write(struct.pack("<9f", *v))
        fh.write(struct.pack("<i", len(TIMES)) + struct.pack(f"<{len(TIMES)}f", *TIMES))
        fh.write(struct.pack("<8f", *dials))
    out = subprocess.run([exe, path], capture_output=True, text=True, check=True).stdout.split("\n")
    rows = [tuple(float(x) for x in line.split()) for line in out if line.strip()]
    return [rows[k * len(verts):(k + 1) * len(verts)] for k in range(len(TIMES))]


def lab_positions():
    js = r"""
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  const p = await b.newPage(); await p.goto('file://' + process.argv[2], { waitUntil: 'load' });
  const out = await p.evaluate((T) => { const L = window.__lab, S = L.SHIPPED, n = L.ASSETS.triMesh.p.length / 3, res = [];
    for (let k = 0; k < S.layers; k++) { const layer = []; for (const t of T) { const f = []; for (let v = 0; v < n; v++) f.push(L.vertexAt(S, v, k, t)); layer.push(f); } res.push(layer); }
    return res; }, JSON.parse(process.argv[3]));
  console.log(JSON.stringify(out)); await b.close();
})();"""
    with tempfile.NamedTemporaryFile("w", suffix=".cjs", delete=False) as fh:
        fh.write(js)
        script = fh.name
    env = dict(os.environ, NODE_PATH=os.environ.get("NODE_PATH", "/usr/local/lib/node_modules_global"))
    r = subprocess.run(["node", script, LAB, json.dumps(TIMES)], capture_output=True, text=True, env=env)
    os.unlink(script)
    if r.returncode != 0:
        raise SystemExit("lab: " + r.stderr[-2000:])
    return json.loads(r.stdout)


def max_err(a, b):
    return max(abs(x - y) for fa, fb in zip(a, b) for va, vb in zip(fa, fb) for x, y in zip(va[:3], vb[:3]))


def main():
    ok = True

    def check(name, cond, detail):
        nonlocal ok
        print(("PASS  " if cond else "FAIL  ") + name + "  (" + detail + ")")
        ok = ok and cond

    with tempfile.TemporaryDirectory() as tmp:
        exe = build(tmp)
        print("compiled the shipped OmniShepardFresnelShader vert() with clang++ -Wall: no warnings")
        verts = mesh_vertices()

        # 1. INERT at the defaults: v * s, with and without plate centres; negative control _Thickness 1.25.
        base = material(0)
        base[4:] = [DEFAULTS[d] for d in DIALS[4:]]
        for label, vs in (("with plate centres", verts), ("mesh without TEXCOORD2", mesh_vertices(zero_centres=True))):
            got = run(exe, tmp, vs, base)
            want = [[tuple(c * row[3] for c in v[:3]) for v, row in zip(vs, frame)] for frame in got]
            e = max_err(got, want)
            check("defaults are inert: vert() == v * ShepardBand(), " + label, e < 1e-6, f"max |diff| {e:.2e}")
        bad = list(base); bad[7] = 1.25
        got = run(exe, tmp, verts, bad)
        e = max_err(got, [[tuple(c * row[3] for c in v[:3]) for v, row in zip(verts, frame)] for frame in got])
        check("negative control: _Thickness 1.25 is NOT inert", e > 1e-3, f"max |diff| {e:.2e}")

        # 2. LAB parity per shipped layer; negative control _Breathe off.
        lab = lab_positions()
        for k in range(len(lab)):
            dials = material(k)
            e = max_err(run(exe, tmp, verts, dials), lab[k])
            check(f"OmniShepardTriangles {k}.mat matches the lab's shipped layer {k}", e < TOL, f"max |diff| {e:.2e} over {len(TIMES)} times x {len(verts)} vertices")
        dials = material(1); dials[4] = 0.0
        e = max_err(run(exe, tmp, verts, dials), lab[1])
        check("negative control: layer 1 with _Breathe off does NOT match", e > 1e-2, f"max |diff| {e:.2e}")
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
