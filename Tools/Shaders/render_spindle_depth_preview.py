#!/usr/bin/env python3
"""
Render what SpindleDepthGraph DRAWS, offline, from the shipped shader code (Docs/SPINDLE_LOD.md §8).

    python3 Tools/Shaders/render_spindle_depth_preview.py OUT_DIR [--body shark|brittlestar|worm|fish|ring]
                                                          [--strength S] [--reach R]

Writes OUT_DIR/spindle_depth_<body>_<strength>_sheet.png (the body at seven points of the prisms' depth
ramp, from rest through the peak to settled back) and OUT_DIR/spindle_depth_<body>_<strength>_alive.gif
(the body at its peak over two seconds, the wave travelling heart -> extremities, or round a ring).

The VERTICES are the game's: SpindleLod.hlsl + IncorporealDepth.hlsl compiled by clang (the substitutions
in verify_spindle_lod.py). A creature is its real model (the FBX the prefab draws), BLOOM form, posed
about its bounds centre (the game blooms a skinned body about its root bone, a static one about its
pivot) and scaled so its farthest point is --reach world units out. The ring is ToyFactory.RingMesh
rebuilt here (12 x 6 flat facets, the same UVs), FACETS form, at a 20-unit radius. Lines are drawn in the
material's dull colour brightened by the shipped pulse (SpindleDepthGlow_float); the lace's Voronoi is a
Shader Graph node with no HLSL here. Judge SHAPE and MOTION, not grading.
"""

import argparse
import math
import os
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(REPO, "Tools", "Build"))
import verify_spindle_lod as V  # noqa: E402
import fbx_binary as F  # noqa: E402

GRAPHS = os.path.join(REPO, "Assets", "_Graphics", "Materials", "Graphs")
FAUNA = os.path.join(REPO, "Assets", "_Models", "Fauna")
BODIES = {"shark": "Shark_model.fbx", "brittlestar": "Brittlestar_Export.fbx", "worm": "wormbody.fbx",
          "fish": "mediumfish.fbx"}
DANGER = (0.09, 0.29, 0.8)
RECEDE = (2.25, 4.9)            # Resources/IncorporealDepth
FLOW = (0.35, 1.5, 0.6, 0.8)
SQR = 100000.0
DULL = (0.0, 0.027586222, 1.0)   # SpindleMaterial
RING_RADIUS = 20.0
STOPS = [(0.15, "at rest"), (0.6, "opening"), (2.0, "the peak"), (3.4, "settling back"), (4.9, "settled"),
         (15.625, "fading"), (19.0, "nearly gone")]

PROGRAM = r'''
#include <cstdio>
#include <vector>
// Reads: form strength reach dist clock sx sy sz nverts, then per vertex: px py pz nx ny nz u v.
// Writes per vertex: wx wy wz glow.
int main() {
    float form, strength, reach, dist, clock, sx, sy, sz; int nv;
    if (std::scanf("%f %f %f %f %f %f %f %f %d", &form, &strength, &reach, &dist, &clock, &sx, &sy, &sz, &nv) != 9) return 1;
    _IncorporealRecede = float4{%RS%f, %RE%f, 0, 0};
    _IncorporealSpread = float4{%DX%f, %DY%f, %DZ%f, %SQR%f};
    _IncorporealFlow = float4{%FA%f, %FW%f, %FS%f, %FG%f};
    _SpindleLodLook2 = float4{0.85f, 1.f, 0, 0};                 // overt
    _SpindleLodBands = float4{%RS%f, %RE%f, 15.625f, 22.5f};       // the shipped overt ladder
    g_model = f4x4{sx,0,0,0, 0,sy,0,0, 0,0,sz,0, 0,0,0,1};
    _WorldSpaceCameraPos = float4{0, 0, -dist, 0};
    for (int i = 0; i < nv; i++) {
        float d[8];
        for (int k = 0; k < 8; k++) if (std::scanf("%f", &d[k]) != 1) return 2;
        float3 p = mk3(d[0], d[1], d[2]), n = mk3(d[3], d[4], d[5]), uv = mk3(d[6], d[7], 0);
        float3 o, g; float ga;
        SpindleDepthVertex_float(p, n, uv, reach, form, strength, 0.f, clock, o);
        float3 w = mk3(o.x * sx, o.y * sy, o.z * sz);
        SpindleDepthGlow_float(mk3(1, 1, 1), 1.f, w, uv, reach, form, strength, 0.f, clock, g, ga);
        std::printf("%g %g %g %g\n", w.x, w.y, w.z, g.x * ga);
    }
    return 0;
}
'''


def build_program(td):
    clang = shutil.which("clang++") or shutil.which("clang")
    if not clang:
        sys.exit("clang is required (it compiles the shipped shader code)")
    for name, path in (("spindle_lod_extract.hlsl", "SpindleLod.hlsl"), ("IncorporealDepth.hlsl", "IncorporealDepth.hlsl")):
        open(os.path.join(td, name), "w").write(V.extract(open(os.path.join(GRAPHS, path)).read()))
    shim = V.SHIM[:V.SHIM.index("static int g_fail")]
    prog = PROGRAM
    for key, val in (("%RS%", RECEDE[0]), ("%RE%", RECEDE[1]), ("%DX%", DANGER[0]), ("%DY%", DANGER[1]),
                     ("%DZ%", DANGER[2]), ("%SQR%", SQR), ("%FA%", FLOW[0]), ("%FW%", FLOW[1]), ("%FS%", FLOW[2]),
                     ("%FG%", FLOW[3])):
        prog = prog.replace(key, repr(val))
    src = os.path.join(td, "depth_preview.cpp")
    open(src, "w").write(shim + prog)
    exe = os.path.join(td, "depth_preview")
    r = subprocess.run([clang, "-x", "c++", "-std=c++17", "-O2", "-w", "-I", td, src, "-o", exe, "-lm"],
                       capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit(r.stderr)
    return exe


def read_body(name):
    """Every geometry in the creature's FBX, as (vertices, edges, uv-less normals) in one model space."""
    r = F.read(os.path.join(FAUNA, BODIES[name]))
    nodes = r[0] if isinstance(r, tuple) else r

    def walk(ns):
        for n in ns:
            yield n
            yield from walk(n.children)

    verts, edges = [], set()
    for n in walk(nodes):
        if n.name != "Geometry" or not n.first("Vertices") or not n.first("PolygonVertexIndex"):
            continue
        base = len(verts)
        vs = np.array(n.first("Vertices").props[0][1], float).reshape(-1, 3)
        verts.extend(vs.tolist())
        poly = []
        for i in n.first("PolygonVertexIndex").props[0][1]:
            poly.append(i if i >= 0 else ~i)
            if i < 0:
                for a, b in zip(poly, poly[1:] + poly[:1]):
                    edges.add((base + min(a, b), base + max(a, b)))
                poly = []
    v = np.array(verts)
    v -= (v.min(0) + v.max(0)) / 2
    return v, sorted(edges)


def ring_mesh():
    """ToyFactory.RingMesh: a unit torus, 12 x 6 flat facets, every quad its own four vertices, UVs
    u = RingLaceWraps (4) along the ring, v round the tube."""
    major, minor, R, r, wraps = 12, 6, 0.5, 0.04, 4.0

    def P(t, p):
        return np.array([math.cos(t) * (R + r * math.cos(p)), math.sin(t) * (R + r * math.cos(p)), r * math.sin(p)])

    verts, normals, uvs, quads = [], [], [], []
    for i in range(major):
        t0, t1 = 2 * math.pi * i / major, 2 * math.pi * (i + 1) / major
        for j in range(minor):
            p0, p1 = 2 * math.pi * j / minor, 2 * math.pi * (j + 1) / minor
            q = [P(t0, p0), P(t1, p0), P(t1, p1), P(t0, p1)]
            nrm = np.cross(q[1] - q[0], q[2] - q[0])
            nrm /= np.linalg.norm(nrm)
            b = len(verts)
            for k, (pt, (u, w)) in enumerate(zip(q, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)))):
                verts.append(pt)
                normals.append(nrm)
                uvs.append((u / major * wraps, w / minor))
            quads.append([b, b + 1, b + 2, b + 3])
    return np.array(verts), np.array(normals), np.array(uvs), quads


def run(exe, form, strength, reach, dist, clock, scale, verts, normals, uvs):
    lines = [f"{form} {strength} {reach} {dist} {clock} {scale[0]} {scale[1]} {scale[2]} {len(verts)}"]
    for p, n, uv in zip(verts, normals, uvs):
        lines.append(" ".join(repr(float(x)) for x in (*p, *n, *uv)))
    r = subprocess.run([exe], input="\n".join(lines), capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit(f"preview program failed ({r.returncode}) {r.stderr}")
    return np.array([[float(x) for x in line.split()] for line in r.stdout.splitlines()])


def project(w, size, extent, yaw=0.5, pitch=0.35):
    cy, sy, cp, sp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    x = w[:, 0] * cy + w[:, 2] * sy
    z = -w[:, 0] * sy + w[:, 2] * cy
    y = w[:, 1] * cp - z * sp
    zz = w[:, 1] * sp + z * cp
    s = size * 0.42 / extent
    return np.stack([size / 2 + x * s, size / 2 - y * s, zz], axis=1)


def draw_lines(out, edges, size, extent):
    im = Image.new("RGB", (size, size), (5, 3, 20))
    d = ImageDraw.Draw(im)
    p = project(out[:, :3], size, extent)
    order = sorted(edges, key=lambda e: -(p[e[0], 2] + p[e[1], 2]))
    for a, b in order:
        g = 0.5 * (out[a, 3] + out[b, 3])
        c = tuple(int(255 * min(1.0, (0.25 + 0.75 * DULL[k]) * 0.75 * g + 0.12 * (g - 1.0))) for k in range(3))
        d.line([(p[a, 0], p[a, 1]), (p[b, 0], p[b, 1])], fill=c, width=1)
    return im


def draw_facets(out, normals, quads, size, extent):
    im = Image.new("RGB", (size, size), (5, 3, 20))
    d = ImageDraw.Draw(im)
    p = project(out[:, :3], size, extent)
    view = np.array([0.0, 0.0, -1.0])
    order = sorted(quads, key=lambda q: -p[q, 2].mean())
    for q in order:
        n = normals[q[0]]
        facing = abs(float(np.dot(n, view)))
        g = out[q, 3].mean()
        shade = (0.35 + 0.55 * facing * facing) * g
        c = tuple(int(255 * min(1.0, DULL[k] * shade + 0.15 * shade)) for k in range(3))
        d.polygon([(p[i, 0], p[i, 1]) for i in q], fill=c)
    return im


def label(im, text):
    ImageDraw.Draw(im).text((6, 5), text, fill=(220, 220, 255))
    return im


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out")
    ap.add_argument("--body", default="shark", choices=sorted(BODIES) + ["ring"])
    ap.add_argument("--strength", type=float, default=None)
    ap.add_argument("--reach", type=float, default=24.0)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    ring = a.body == "ring"
    strength = a.strength if a.strength is not None else 0.1
    with tempfile.TemporaryDirectory() as td:
        exe = build_program(td)
        if ring:
            verts, normals, uvs, quads = ring_mesh()
            scale = (2 * RING_RADIUS,) * 3
            form, reach, extent = 1, 0.0, RING_RADIUS * 1.25
        else:
            verts, edges = read_body(a.body)
            far = float(np.linalg.norm(verts, axis=1).max())
            k = a.reach / far
            scale = (k, k, k)
            normals = np.zeros_like(verts)
            uvs = np.zeros((len(verts), 2))
            form, reach, extent = 0, a.reach, a.reach * 1.25
        frames = []
        for n, what in STOPS:
            dist = math.sqrt(n * SQR)
            out = run(exe, form, strength, reach, dist, 0.0, scale, verts, normals, uvs)
            im = draw_facets(out, normals, quads, 300, extent) if ring else draw_lines(out, edges, 300, extent)
            frames.append(label(im, f"norm {n:g} (~{dist:,.0f} u): {what}"))
        sheet = Image.new("RGB", (300 * len(frames), 300))
        for i, f in enumerate(frames):
            sheet.paste(f, (300 * i, 0))
        tag = f"spindle_depth_{a.body}_{int(round(strength * 100))}"
        sheet_path = os.path.join(a.out, f"{tag}_sheet.png")
        sheet.save(sheet_path)
        gif = []
        for t in range(24):
            out = run(exe, form, strength, reach, math.sqrt(2.0 * SQR), t / 12.0, scale, verts, normals, uvs)
            im = draw_facets(out, normals, quads, 360, extent) if ring else draw_lines(out, edges, 360, extent)
            gif.append(label(im, f"{a.body}, strength {strength:g}, the peak"))
        gif_path = os.path.join(a.out, f"{tag}_alive.gif")
        gif[0].save(gif_path, save_all=True, append_images=gif[1:], duration=83, loop=0)
        print(sheet_path)
        print(gif_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
