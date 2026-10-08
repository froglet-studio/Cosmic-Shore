#!/usr/bin/env python3
"""
Render the SHIPPED black hole lens (Assets/_Graphics/Materials/Graphs/BlackHoleLens.hlsl) to a PNG,
offline, so the look of the shadow and the lens can be judged without the editor.

The HLSL is not re-implemented: it is translated to C++ by verify_black_hole_lens.py's mechanical
HLSL -> C++ step, compiled with clang++, and every pixel runs the shipped BlackHoleLensTrace +
BlackHoleLensFadeDir, composited exactly as BlackHoleLens.shader's fragment stage does (the bent
scene, or the shadow's black). What is NOT the game's: the background. The shader bends the
camera's opaque copy of the scene; here the bent ray samples a stand-in - a procedural sky (blue
zenith, pale horizon, brown ground, like the test scene's skybox) and a field of dark-blue "prisms"
around the hole - because the point is the bend, not the scene.

Display transform = the project's: linear HDR, NO tonemapper (DefaultVolumeProfile: Tonemapping
None), so the final blit clips each channel at 1, then sRGB.

    python3 Tools/Shaders/render_black_hole_lens.py --out lens.png
    python3 Tools/Shaders/render_black_hole_lens.py --dist 12 --out inside.png      # inside the lens
    python3 Tools/Shaders/render_black_hole_lens.py --hlsl other.hlsl --out other.png

Defaults match Resources/BlackHoleConfig.asset (lens 30 r_s, fade from 0.55, 128 steps), seen from
the side. A reader tool: it writes only the PNG it is given.
"""

import argparse
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import verify_black_hole_lens as lens  # noqa: E402  (the shim and the HLSL -> C++ translation)

DRIVER = r"""#include "shipped.h"
#include <cstdio>
#include <cstdlib>
#include <cmath>

static float hash2(float i, float j) { float s = std::sin(i * 12.9898f + j * 78.233f) * 43758.5453f; return s - std::floor(s); }

// The stand-in background, linear RGB: sky / horizon / ground, and a field of dark-blue prisms
// within `field` radians of the hole's direction (as seen from the eye).
static float3 background(float3 d, float3 toHole, float field, float seed)
{
    float3 c;
    if (d.y >= 0.0f) {
        float t = std::pow(1.0f - d.y, 4.0f);
        c = float3(0.16f, 0.34f, 0.72f) * (1.0f - t) + float3(0.78f, 0.82f, 0.86f) * t;
    } else {
        float t = std::pow(1.0f + d.y, 6.0f);
        c = float3(0.26f, 0.22f, 0.19f) * (1.0f - t) + float3(0.62f, 0.62f, 0.62f) * t;
    }
    float ang = std::acos(std::max(-1.0f, std::min(1.0f, dot(d, toHole))));
    if (ang < field) {
        const float cell = 0.006f;
        float lon = std::atan2(d.x, d.z), lat = std::asin(std::max(-1.0f, std::min(1.0f, d.y)));
        float i = std::floor(lon / cell), j = std::floor(lat / cell);
        if (hash2(i + seed, j) < 0.45f) c = float3(0.015f, 0.05f, 0.26f) * (0.6f + 0.8f * hash2(j, i + seed));
    }
    return c;
}

int main(int argc, char** argv)
{
    int W = std::atoi(argv[1]), H = std::atoi(argv[2]);
    float dist = std::atof(argv[3]), yaw = std::atof(argv[4]) * 0.0174533f, pitch = std::atof(argv[5]) * 0.0174533f;
    float fov = std::atof(argv[6]) * 0.0174533f;
    float lensR = std::atof(argv[7]), fadeStart = std::atof(argv[8]); int steps = std::atoi(argv[9]);
    float fieldR = std::atof(argv[10]);
    FILE* out = std::fopen(argv[11], "wb");

    float3 eye(dist * std::cos(pitch) * std::sin(yaw), dist * std::sin(pitch), -dist * std::cos(pitch) * std::cos(yaw));
    float3 fwd = normalize(float3(0, 0, 0) - eye);
    float3 right = normalize(cross(float3(0, 1, 0), fwd));
    float3 up = cross(fwd, right);
    float th = std::tan(fov * 0.5f), aspect = (float)W / (float)H;
    float field = std::atan(fieldR / dist);

    for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++) {
            float u = ((x + 0.5f) / W * 2.0f - 1.0f) * th * aspect, v = (1.0f - (y + 0.5f) / H * 2.0f) * th;
            float3 d = normalize(fwd + right * u + up * v);
            float b = length(cross(eye, d));
            float3 colour;
            if (b >= lensR) colour = background(d, fwd, field, 0.0f);
            else {
                float3 bent; float escaped;
                BlackHoleLensTrace(eye, d, lensR, steps, bent, escaped);
                float3 bg(0, 0, 0);
                if (escaped > 0.5f) bg = background(BlackHoleLensFadeDir(d, bent, b, lensR, fadeStart), fwd, field, 0.0f);
                colour = bg;   // the bent scene, or the shadow's black
            }
            // prisms in front of the hole are not lensed: the depth test draws them over the lens
            float3 fd = d;
            float ang = std::acos(std::max(-1.0f, std::min(1.0f, dot(fd, fwd))));
            if (ang < field) {
                const float cell = 0.006f;
                float lon = std::atan2(fd.x, fd.z), lat = std::asin(std::max(-1.0f, std::min(1.0f, fd.y)));
                float i = std::floor(lon / cell), j = std::floor(lat / cell);
                if (hash2(i + 91.0f, j) < 0.22f) colour = float3(0.02f, 0.07f, 0.32f) * (0.6f + 0.8f * hash2(j, i + 91.0f));
            }
            float px[3] = { colour.x, colour.y, colour.z };
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


def render(args, out_png):
    from PIL import Image
    import array

    hlsl = open(args.hlsl, encoding="utf-8").read()
    work = tempfile.mkdtemp(prefix="render_black_hole_lens_")
    try:
        with open(os.path.join(work, "shim.h"), "w") as f:
            f.write(lens.SHIM)
        with open(os.path.join(work, "shipped.h"), "w") as f:
            f.write('#pragma once\n#include "shim.h"\n' + lens.translate(hlsl))
        with open(os.path.join(work, "render.cpp"), "w") as f:
            f.write(DRIVER)
        flags = []
        binary = os.path.join(work, "render")
        build = subprocess.run(["clang++", "-std=c++17", "-O2", "-w"] + flags + ["-o", binary, "render.cpp"],
                               cwd=work, capture_output=True, text=True)
        if build.returncode != 0:
            sys.exit("render: the HLSL did not build:\n" + build.stderr)
        raw = os.path.join(work, "image.f32")
        a = args
        cmd = [binary, a.size, a.size, a.dist, a.yaw, a.pitch, a.fov,
               a.lens, a.fade, a.steps, a.field, raw]
        subprocess.run([str(c) for c in cmd], check=True)
        data = array.array("f")
        with open(raw, "rb") as f:
            data.frombytes(f.read())
        n = int(a.size)
        img = Image.new("RGB", (n, n))
        img.putdata([(srgb8(data[i]), srgb8(data[i + 1]), srgb8(data[i + 2])) for i in range(0, len(data), 3)])
        img.save(out_png)
    finally:
        shutil.rmtree(work, ignore_errors=True)
    return out_png


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--hlsl", default=lens.HLSL, help="the lens HLSL to render (default: the shipped file)")
    p.add_argument("--out", required=True)
    p.add_argument("--size", default="480")
    p.add_argument("--dist", default="30", help="eye distance from the hole, in r_s")
    p.add_argument("--yaw", default="60", help="degrees around +y; 0 looks along +z")
    p.add_argument("--pitch", default="8")
    p.add_argument("--fov", default="60")
    p.add_argument("--lens", default="30")
    p.add_argument("--fade", default="0.55")
    p.add_argument("--steps", default="128")
    p.add_argument("--field", default="14.4", help="radius of the stand-in prism field, r_s")
    args = p.parse_args()
    if shutil.which("clang++") is None:
        sys.exit("clang++ not found")
    print(render(args, args.out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
