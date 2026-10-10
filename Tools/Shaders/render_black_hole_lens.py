#!/usr/bin/env python3
"""
Render the black and white hole lens offline to a PNG, the Vessel Studio's and the SHIPPED Unity one side by side,
so the look can be judged without the editor.

Neither lens is re-implemented: the studio's fragment shader (GLSL, extracted from
Docs/Studios/StoatFlightStudio.html) and the shipped lens (BlackHoleLens.hlsl + BlackHoleLens.shader's fragment
function) are translated to C++ by verify_black_hole_lens.py's mechanical step, compiled with clang++, and every
pixel runs them. What is NOT the game's: the scene behind the holes (a procedural star field and grid, standing in
for the camera's copy) and the holes' screen numbers, which this tool works out with the studio's formulas
(setLensUniforms: f = 0.5/tan(fov/2), r_c = f·tan asin(r_s/D), θ_E = f·tan √(2 r_s/D), reach f·tan atan(30 r_s/D));
verify_black_hole_lens.py tier C proves the shipped C# computes the same.

Display transform = the project's: linear HDR, NO tonemapper, so each channel clips at 1, then sRGB.

    python3 Tools/Shaders/render_black_hole_lens.py --out pair.png                 # a black + white pair
    python3 Tools/Shaders/render_black_hole_lens.py --gap 2 --out overlap.png       # the pair overlapping
    python3 Tools/Shaders/render_black_hole_lens.py --only black --out black.png

Defaults match Resources/BlackHoleConfig.asset (shadow 2.6, reach 30, fade 0.55, ring 0.55 / 0.06, core 4 / 0.8 /
2.6). Left half: the studio. Right half: Unity. A reader tool: it writes only the PNG it is given.
"""

import argparse
import math
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import verify_black_hole_lens as lens  # noqa: E402  (the shim and the GLSL / HLSL -> C++ steps)

DRIVER = r"""
#include "shim.h"
#include <cstdio>
#include <cstdlib>
#include <cmath>
#include <vector>

// The stand-in scene, linear RGB: a dark blue field with a grid and stars (the studio's tooltip previews draw the
// same kind of thing), so every bend shows.
static float3 scene_colour(float2 uv)
{
    float u = uv.x * 160.0f, v = uv.y * 90.0f;
    float n = 0.5f + 0.5f * std::sin(u * 0.045f + v * 0.021f) * std::cos(v * 0.037f - u * 0.012f);
    float3 c(0.012f + 0.03f * n, 0.016f + 0.025f * n, 0.06f + 0.08f * n);
    float gu = std::fabs(std::fmod(u / 6.0f, 1.0f) - 0.5f), gv = std::fabs(std::fmod(v / 6.0f, 1.0f) - 0.5f);
    float line = std::max(0.0f, 1.0f - std::min(gu, gv) * 14.0f);
    c = c + float3(0.04f, 0.1f, 0.22f) * line;
    int iu = (int)std::floor(u * 1.3f), iv = (int)std::floor(v * 1.3f);
    unsigned h = (unsigned)(iu * 374761393 + iv * 668265263); h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
    if (h / 4294967296.0 > 0.985) c = float3(1.0f, 1.0f, 1.0f);
    return c;
}

namespace studio {
static float4 texture2D(int tex, float2 uv) { if (tex == 1) return float4(1.0f, 0, 0, 0); return float4(scene_colour(uv), 1.0f); }
#include "studio.h"
}
namespace unity {
#include "shipped.h"
static float4 _BHWellC[BLACK_HOLE_LENS_MAX_WELLS], _BHWellP[BLACK_HOLE_LENS_MAX_WELLS], _BHWellM[BLACK_HOLE_LENS_MAX_WELLS], _BHWellT[BLACK_HOLE_LENS_MAX_WELLS];
static float _BHWellCount;
static float4 _BHLook, _BHLook2;
static float2 g_uv;
static float g_sceneZ;
#include "frag.h"
}

int main(int argc, char** argv)
{
    // args: w h n [c.x c.y c.z c.w p.x p.y p.z p.w m.x m.y m.z m.w]*n look(8) out
    int W = atoi(argv[1]), H = atoi(argv[2]), n = atoi(argv[3]);
    int a = 4;
    std::vector<float4> C(n), P(n), M(n);
    for (int i = 0; i < n; i++) {
        C[i] = float4(atof(argv[a]), atof(argv[a+1]), atof(argv[a+2]), atof(argv[a+3])); a += 4;
        P[i] = float4(atof(argv[a]), atof(argv[a+1]), atof(argv[a+2]), atof(argv[a+3])); a += 4;
        M[i] = float4(atof(argv[a]), atof(argv[a+1]), atof(argv[a+2]), atof(argv[a+3])); a += 4;
    }
    float L[8]; for (int i = 0; i < 8; i++) L[i] = atof(argv[a++]);
    FILE* out = std::fopen(argv[a], "wb");
    {
        using namespace studio;
        camNear = 0.5f; camFar = 9000.0f; aspect = L[7]; soft = 0.65f; gwCam = 0; shellWash = 0; gwGlow = 1;
        kShadow = L[0]; kCore = L[1]; ringGlow = L[2]; ringWidth = L[3]; lensFade = L[4]; coreBright = L[5]; coreMix = L[6];
        for (int i = 0; i < n && i < 4; i++) { wC[i] = C[i]; wP[i] = P[i]; wM[i] = M[i]; }
    }
    {
        using namespace unity;
        for (int i = 0; i < n; i++) { _BHWellC[i] = C[i]; _BHWellP[i] = P[i]; _BHWellM[i] = M[i]; }
        _BHWellCount = (float)n;
        _BHLook = float4(L[0], L[1], L[2], L[3]);
        _BHLook2 = float4(L[4], L[5], L[6], L[7]);
        g_sceneZ = 1e6f;
    }
    for (int y = H - 1; y >= 0; y--)
        for (int side = 0; side < 2; side++)
            for (int x = 0; x < W; x++) {
                float2 uv((x + 0.5f) / W, (y + 0.5f) / H);
                float3 col;
                if (side == 0) { studio::vUv = uv; studio::studio_main(); col = studio::gl_FragColor.xyz(); }
                else {
                    unity::g_uv = uv;
                    float4 r = unity::shipped_frag();
                    col = (r.x == -1.0f && r.w == -1.0f) ? scene_colour(uv) : r.xyz();
                }
                float px[3] = { col.x, col.y, col.z };
                std::fwrite(px, sizeof(float), 3, out);
            }
    std::fclose(out);
    return 0;
}
"""


def srgb8(v):
    v = min(max(v, 0.0), 1.0)   # the final blit's per-channel clip (no tonemapper)
    s = 12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055
    return int(round(255 * s))


def screen_well(kind, x, y, z, rs, strength, reach, fov):
    """The studio's setLensUniforms for one hole at view position (x, y, z), camera at the origin."""
    f = 0.5 / math.tan(math.radians(fov) / 2)
    d = math.sqrt(x * x + y * y + z * z)
    ang = lambda r: f * math.tan(min(1.45, math.asin(min(0.999, r / max(d, r * 1.001)))))
    the = f * math.tan(min(1.2, math.sqrt(2 * rs / d)))
    c = (f * x / z, f * y / z, z, 1)
    p = (kind, ang(rs), 0, the * the * strength)
    m = (f * math.tan(min(1.45, math.atan(reach * rs / d))), 0, 0, 2.6 * rs)
    return c + p + m


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", required=True)
    ap.add_argument("--width", type=int, default=480, help="each half's width")
    ap.add_argument("--height", type=int, default=270)
    ap.add_argument("--fov", type=float, default=68.0)
    ap.add_argument("--dist", type=float, default=220.0, help="the pair's distance from the camera (world units)")
    ap.add_argument("--rs", type=float, default=6.0, help="each horizon radius (world units)")
    ap.add_argument("--gap", type=float, default=14.0, help="the poles' separation, in horizons")
    ap.add_argument("--only", choices=("pair", "black", "white"), default="pair")
    ap.add_argument("--shadow", type=float, default=2.6)
    ap.add_argument("--core-size", type=float, default=2.6)
    ap.add_argument("--ring-glow", type=float, default=0.55)
    ap.add_argument("--ring-width", type=float, default=0.06)
    ap.add_argument("--fade", type=float, default=0.55)
    ap.add_argument("--reach", type=float, default=30.0)
    ap.add_argument("--core-brightness", type=float, default=4.0)
    ap.add_argument("--core-sky-mix", type=float, default=0.8)
    ap.add_argument("--lens-strength", type=float, default=1.0)
    args = ap.parse_args()
    if shutil.which("clang++") is None:
        sys.exit("clang++ not found")
    from PIL import Image
    import array

    half = args.gap * args.rs / 2
    wells = []
    if args.only in ("pair", "black"):
        wells.append(screen_well(1, -half if args.only == "pair" else 0, 0, args.dist, args.rs, args.lens_strength, args.reach, args.fov))
    if args.only in ("pair", "white"):
        wells.append(screen_well(2, half if args.only == "pair" else 0, 0, args.dist, args.rs, args.lens_strength, args.reach, args.fov))
    aspect = args.width / args.height
    look = (args.shadow, args.core_size, args.ring_glow, max(0.005, args.ring_width), args.fade, args.core_brightness,
            args.core_sky_mix, aspect)

    work = tempfile.mkdtemp(prefix="render_black_hole_lens_")
    try:
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(lens.SHIM)
        with open(os.path.join(work, "studio.h"), "w") as f:
            f.write(lens.studio_to_cpp(lens.studio_fragment()))
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write(lens.shipped_hlsl_to_cpp(open(lens.HLSL).read()))
        with open(os.path.join(work, "frag.h"), "w") as f:
            f.write(lens.shipped_frag_to_cpp(open(lens.SHADER).read()))
        with open(os.path.join(work, "render.cpp"), "w") as f:
            f.write(DRIVER)
        binary = os.path.join(work, "render")
        build = subprocess.run(["clang++", "-std=c++17", "-O2", "-w", "-o", binary, "render.cpp"], cwd=work,
                               capture_output=True, text=True)
        if build.returncode != 0:
            sys.exit("render: the lens did not build:\n" + build.stderr[:4000])
        raw = os.path.join(work, "image.f32")
        cmd = [binary, args.width, args.height, len(wells)] + [v for w in wells for v in w] + list(look) + [raw]
        subprocess.run([str(c) for c in cmd], check=True)
        data = array.array("f")
        with open(raw, "rb") as f:
            data.frombytes(f.read())
        img = Image.new("RGB", (args.width * 2, args.height))
        img.putdata([(srgb8(data[i]), srgb8(data[i + 1]), srgb8(data[i + 2])) for i in range(0, len(data), 3)])
        img.save(args.out)
    finally:
        shutil.rmtree(work, ignore_errors=True)
    print(args.out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
