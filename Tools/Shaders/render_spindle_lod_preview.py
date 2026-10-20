#!/usr/bin/env python3
"""
Render what the spindle LOD DRAWS, offline, from the shipped shader code (asset-surgery 4.5c).

    python3 Tools/Shaders/render_spindle_lod_preview.py OUT_DIR [--style overt|covert] [--strength 0.7]

Writes OUT_DIR/spindle_lod_<style>_sheet.png (a star of limbs, like one lattice node, at five points
of the prisms' depth ramp) and OUT_DIR/spindle_lod_<style>_alive.gif (the fully opened star over two
seconds, the wave travelling root to tip).

The VERTICES are the game's: SpindleLod.hlsl + IncorporealDepth.hlsl + SpindleSway.hlsl compiled by
clang (the substitutions in verify_spindle_lod.py), run on the real SpindleBraidMid mesh from
author_spindle_lod_meshes.py, posed through the Branch prefab's own child transform. The colour is the
far tier's (SpindleLodThread_float, shipped): the lace's Voronoi is a Shader Graph node with no HLSL
here, so the nearest frame shows the thread shading on the braid instead of the lace. The raster is a
plain painter's-order rasteriser with 3x supersampling: it judges SHAPE and MOTION, not final colour
grading or bloom. The camera that DRIVES the effect sits at each frame's distance; the camera that
PHOTOGRAPHS it is framed to the star, so a far form is seen large enough to judge.
"""

import math
import os
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(REPO, "Tools", "Build"))
import verify_spindle_lod as V  # noqa: E402
import author_spindle_lod_meshes as A  # noqa: E402

GRAPHS = os.path.join(REPO, "Assets", "_Graphics", "Materials", "Graphs")
DULL = (0.0, 0.027586222, 1.0)
BRIGHT = (0.36976376, 0.39671606, 0.9559748)
SWAY_AMPLITUDE, SWAY_FREQUENCY = 0.1625, 1.4     # GyroidSpindleMaterial
DANGER = (0.09, 0.29, 0.8)
RECEDE = (2.25, 16.0)            # Resources/IncorporealDepth
KEEP = 1.0 - 0.08                # SpindleLodProfile.hysteresis
SQR = 100000.0
LIMBS = 8

PROGRAM = r'''
#include <cstdio>
// Reads: style dist clock minpx nverts nlimbs, then per vertex: px py pz nx ny nz u v tx ty tz rx ry rz sx sy sz, then per limb
// a 4x4 matrix (row-major) and a phase. Writes per limb per vertex: wx wy wz r g b a.
int main() {
    _ScreenParams = float4{1920, 1080, 0, 0};
    unity_OrthoParams = float4{0, 0, 0, 0};
    UNITY_MATRIX_P = f4x4{}; UNITY_MATRIX_P._m11 = 1.7320508f;
    float style, dist, clock, minpx, strength; int nv, nl;
    if (std::scanf("%f %f %f %f %f %d %d", &style, &dist, &clock, &minpx, &strength, &nv, &nl) != 7) return 1;
    _IncorporealRecede = float4{%RS%f, %RE%f, 0, 0};
    _IncorporealSpread = float4{%DX%f, %DY%f, %DZ%f, %SQR%f};
    _IncorporealFlow = float4{0.35f, 1.5f, 0.6f, 0.8f};
    if (style > 0.5f) { _SpindleLodBands = float4{%RS%f, %RE%f, 36, 64}; }
    else { _SpindleLodBands = float4{110, 386.4f, 1300, 1900}; }
    _SpindleLodLook = float4{minpx, 0.35f, 0.55f, 1.35f};
    _SpindleLodLook2 = float4{0.85f, style, 0, 0};
    std::vector<float> vd(nv * 17);
    for (int i = 0; i < nv * 17; i++) if (std::scanf("%f", &vd[i]) != 1) return 2;
    for (int l = 0; l < nl; l++) {
        float M[16]; float phase;
        for (int k = 0; k < 16; k++) if (std::scanf("%f", &M[k]) != 1) return 3;
        if (std::scanf("%f", &phase) != 1) return 4;
        g_model = f4x4{M[0],M[1],M[2],M[3], M[4],M[5],M[6],M[7], M[8],M[9],M[10],M[11], M[12],M[13],M[14],M[15]};
        // the effect's camera: `dist` from the star's centre, on the -z side
        _WorldSpaceCameraPos = float4{0, 0, -dist, 0};
        for (int i = 0; i < nv; i++) {
            float* d = &vd[i * 17];
            float3 p = mk3(d[0], d[1], d[2]), n = mk3(d[3], d[4], d[5]), uv = mk3(d[6], d[7], 0);
            float3 ta = mk3(d[8], d[9], d[10]), ra = mk3(d[11], d[12], d[13]), sp = mk3(d[14], d[15], d[16]);
            float3 o, s;
            SpindleLodVertex_float(p, n, uv, ta, ra, sp, %GIRTH%f, %STRAND%f, strength, phase, clock, o);
            SpindleSway_float(o, clock, phase, %SWAYA%f, %SWAYF%f, s);
            float3 w = mk3(M[0]*s.x + M[1]*s.y + M[2]*s.z + M[3], M[4]*s.x + M[5]*s.y + M[6]*s.z + M[7],
                           M[8]*s.x + M[9]*s.y + M[10]*s.z + M[11]);
            float3 nw = normalize(mk3(M[0]*n.x + M[1]*n.y + M[2]*n.z, M[4]*n.x + M[5]*n.y + M[6]*n.z, M[8]*n.x + M[9]*n.y + M[10]*n.z));
            float3 view = normalize(mk3(_WorldSpaceCameraPos.x, _WorldSpaceCameraPos.y, _WorldSpaceCameraPos.z) - w);
            float3 c; float a;
            SpindleLodThread_float(mk3(%DULL%), mk3(%BRIGHT%), 0.f, nw, view, uv, %GIRTH%f, %STRAND%f, strength, phase, clock, c, a);
            std::printf("%g %g %g %g %g %g %g\n", w.x, w.y, w.z, c.x, c.y, c.z, a);
        }
    }
    return 0;
}
'''


def build_program(td):
    clang = shutil.which("clang++") or shutil.which("clang")
    if not clang:
        sys.exit("clang is required (it compiles the shipped shader code)")
    for name, path in (("spindle_lod_extract.hlsl", "SpindleLod.hlsl"), ("IncorporealDepth.hlsl", "IncorporealDepth.hlsl"),
                       ("SpindleSway.hlsl", "SpindleSway.hlsl")):
        open(os.path.join(td, name), "w").write(V.extract(open(os.path.join(GRAPHS, path)).read()))
    shim = V.SHIM[:V.SHIM.index("static int g_fail")]
    shim = shim.replace('#include <algorithm>', '#include <algorithm>\n#include <vector>')
    shim = shim.replace('#include "spindle_lod_extract.hlsl"', '#include "spindle_lod_extract.hlsl"\nstatic float3 sin(float3 v) { return mk3(std::sin(v.x), std::sin(v.y), std::sin(v.z)); }\n#include "SpindleSway.hlsl"')
    pos, quads = A.read_braid()
    m = A.measure(pos)
    prog = (PROGRAM.replace("%DX%", repr(DANGER[0])).replace("%DY%", repr(DANGER[1])).replace("%DZ%", repr(DANGER[2]))
            .replace("%SQR%", repr(SQR)).replace("%RS%", repr(RECEDE[0])).replace("%RE%", repr(RECEDE[1])).replace("%GIRTH%", repr(m["girth"])).replace("%STRAND%", repr(m["r_ref"]))
            .replace("%SWAYA%", repr(SWAY_AMPLITUDE)).replace("%SWAYF%", repr(SWAY_FREQUENCY))
            .replace("%DULL%", ", ".join(f"{c}f" for c in DULL)).replace("%BRIGHT%", ", ".join(f"{c}f" for c in BRIGHT)))
    src = os.path.join(td, "preview.cpp")
    open(src, "w").write(shim + prog)
    exe = os.path.join(td, "preview")
    r = subprocess.run([clang, "-x", "c++", "-std=c++17", "-O2", "-w", "-I", td, src, "-o", exe, "-lm"], capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit(r.stderr)
    meshes = {"mid": A.build_mid(pos, quads, m), "low": A.build_mid(pos, quads, m, A.LOW_RINGS), "thread": A.build_far(m)}
    return exe, meshes


def limb_matrices():
    """A star of limbs radiating from one node, each posed as the Branch prefab poses its braid:
    root at the node, the child at z 3.42, rotated 180 about x, scaled (1, 1, 6.2)."""
    child = np.array([[1, 0, 0, 0], [0, -1, 0, 0], [0, 0, -1, 3.42], [0, 0, 0, 1]], float) @ np.diag([1, 1, 6.2, 1])
    mats = []
    for i in range(LIMBS):
        a = 2 * math.pi * i / LIMBS + 0.3
        tilt = 0.5 * math.sin(3 * a)
        fwd = np.array([math.cos(a) * math.cos(tilt), math.sin(a) * math.cos(tilt), math.sin(tilt)])
        up = np.array([0.0, 0.0, 1.0]) if abs(fwd[2]) < 0.9 else np.array([1.0, 0.0, 0.0])
        right = np.cross(up, fwd); right /= np.linalg.norm(right)
        up2 = np.cross(fwd, right)
        root = np.eye(4); root[:3, 0] = right; root[:3, 1] = up2; root[:3, 2] = fwd
        mats.append((root @ child, (i * 0.785398) % (2 * math.pi)))
    return mats


def run(exe, mid, style, dist, clock, mats, minpx=1.25, strength=1.0):
    lines = [f"{1 if style == 'overt' else 0} {dist} {clock} {minpx} {strength} {len(mid['v'])} {len(mats)}"]
    for p, n, uv, ta, ra, sp in mid["v"]:
        lines.append(" ".join(repr(float(x)) for x in (*p, *n, *uv, *ta, *ra, *sp)))
    for M, phase in mats:
        lines.append(" ".join(repr(float(x)) for x in M.flatten()) + f" {phase}")
    r = subprocess.run([exe], input="\n".join(lines), capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit(f"preview program failed ({r.returncode}) {r.stderr}")
    return np.array([[float(x) for x in line.split()] for line in r.stdout.splitlines()])


def raster(verts, tris, nlimbs, nv, size=360, fov_fit=None, ss=3, cam_dist=60.0, focal_px=None):
    """Painter's order, alpha blended, supersampled. Photograph camera on -z looking at the node:
    framed to the star (fov_fit), or - focal_px - a window of a real screen at the real distance."""
    W = size * ss
    img = np.zeros((W, W, 3), float)
    img[:] = np.array([0.02, 0.01, 0.08])
    cam = np.array([0.0, 0.0, -cam_dist])
    pts = verts[:, :3] - cam
    f = focal_px * ss if focal_px else W / (2 * math.tan(math.radians(fov_fit) / 2))
    sx = W / 2 + f * pts[:, 0] / pts[:, 2]
    sy = W / 2 - f * pts[:, 1] / pts[:, 2]
    allt = []
    for l in range(nlimbs):
        base = l * nv
        for t in tris:
            idx = [base + i for i in t]
            allt.append((pts[idx, 2].mean(), idx))
    allt.sort(key=lambda e: -e[0])
    yy, xx = np.mgrid[0:W, 0:W]
    for _, idx in allt:
        x0, y0, x1, y1, x2, y2 = sx[idx[0]], sy[idx[0]], sx[idx[1]], sy[idx[1]], sx[idx[2]], sy[idx[2]]
        minx, maxx = int(max(0, math.floor(min(x0, x1, x2)))), int(min(W - 1, math.ceil(max(x0, x1, x2))))
        miny, maxy = int(max(0, math.floor(min(y0, y1, y2)))), int(min(W - 1, math.ceil(max(y0, y1, y2))))
        if minx > maxx or miny > maxy:
            continue
        den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
        if abs(den) < 1e-9:
            continue
        X = xx[miny:maxy + 1, minx:maxx + 1] + 0.5
        Y = yy[miny:maxy + 1, minx:maxx + 1] + 0.5
        a = ((y1 - y2) * (X - x2) + (x2 - x1) * (Y - y2)) / den
        b = ((y2 - y0) * (X - x2) + (x0 - x2) * (Y - y2)) / den
        mask = (a >= 0) & (b >= 0) & (a + b <= 1)
        if not mask.any():
            continue
        c = verts[idx, 3:6].mean(axis=0)
        al = float(np.clip(verts[idx, 6].mean(), 0, 1))
        region = img[miny:maxy + 1, minx:maxx + 1]
        region[mask] = region[mask] * (1 - al) + np.clip(c, 0, 4) * al
    img = img.reshape(size, ss, size, ss, 3).mean(axis=(1, 3))
    img = 1 - np.exp(-img * 1.6)            # a soft tonemap so the glow above 1 reads
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))


def label(im, text):
    from PIL import ImageDraw
    d = ImageDraw.Draw(im)
    d.text((8, 6), text, fill=(220, 220, 255))
    return im


def tier_mesh(meshes, style, n):
    """The mesh SpindleLodSystem draws at this distance (the near tier's authored braid is stood in for
    by the mid braid: the lace is a Shader Graph node with no HLSL here)."""
    if style != "overt":
        return ("mid", meshes["mid"]) if n < 420 else ("thread", meshes["thread"])
    if n < 1.0 / KEEP or n < RECEDE[0]:
        return ("braid 144", meshes["mid"])
    if n < RECEDE[1] / KEEP:
        return ("braid 72", meshes["low"])
    return ("thread 12", meshes["thread"])


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    out = sys.argv[1]
    style = sys.argv[sys.argv.index("--style") + 1] if "--style" in sys.argv else "overt"
    strength = float(sys.argv[sys.argv.index("--strength") + 1]) if "--strength" in sys.argv else 1.0
    tag = f"{style}_{int(round(strength * 100))}" if style == "overt" else style
    os.makedirs(out, exist_ok=True)
    with tempfile.TemporaryDirectory() as td:
        exe, meshes = build_program(td)
        mats = limb_matrices()
        t0 = min((s + 7) / (s * g + 7) for s, g in zip(DANGER, (50, 35, 20)))
        if style == "overt":
            stops = [(0.15, "at rest"), (0.6, "opening"), (1.0, "fully open"), (2.25, "peak ends"),
                     (5.0, "settling back"), (16.0, "settled: the thread"), (45.0, "thread fading")]
            dist_of = lambda n: math.sqrt(n * SQR)
        else:
            stops = [(60, "braid"), (200, "drawing together"), (300, "drawing together"), (420, "one thread"), (1200, "far thread")]
            dist_of = lambda g: g * 1.4011
        frames, true_size = [], []
        unit = "norm" if style == "overt" else "girths"
        focal_1080p = 1080 / (2 * math.tan(math.radians(30)))
        for n, text in stops:
            name, mesh = tier_mesh(meshes, style, n)
            nv = len(mesh["v"])
            # top row: the SHAPE, framed to the star, the width hold off so the form reads
            v = run(exe, mesh, style, dist_of(n), 0.0, mats, minpx=0.0, strength=strength)
            frames.append(label(raster(v, mesh["t"], len(mats), nv, fov_fit=55),
                                f"{unit} {n:g} (~{dist_of(n):.0f} u): {text} [{name}]"))
            # bottom row: what a 1080p, 60-degree screen shows at that distance, hold on (true size)
            v = run(exe, mesh, style, dist_of(n), 0.0, mats, minpx=1.25, strength=strength)
            true_size.append(label(raster(v, mesh["t"], len(mats), nv, cam_dist=dist_of(n), focal_px=focal_1080p),
                                   "true size, 1080p"))
        sheet = Image.new("RGB", (360 * len(frames), 720))
        for i, f in enumerate(frames):
            sheet.paste(f, (360 * i, 0))
            sheet.paste(true_size[i], (360 * i, 360))
        sheet_path = os.path.join(out, f"spindle_lod_{tag}_sheet.png")
        sheet.save(sheet_path)
        anim = []
        n_alive = 2.0 if style == "overt" else 1200
        name, mesh = tier_mesh(meshes, style, n_alive)
        for k in range(20):
            v = run(exe, mesh, style, dist_of(n_alive), k * 0.1, mats, minpx=0.0, strength=strength)
            anim.append(label(raster(v, mesh["t"], len(mats), len(mesh["v"]), fov_fit=55), f"t = {k * 0.1:.1f} s, at the peak"))
        gif_path = os.path.join(out, f"spindle_lod_{tag}_alive.gif")
        anim[0].save(gif_path, save_all=True, append_images=anim[1:], duration=100, loop=0)
        print(sheet_path)
        print(gif_path)


if __name__ == "__main__":
    main()
