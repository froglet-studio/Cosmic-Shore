#!/usr/bin/env python3
"""Reproduce shipped textures from their genome texture programs and measure the match.

Each program is the byte string the compression study fitted (template id, three parameter bytes, a K-knot
least-squares LUT, a seed byte). This script decodes it exactly as
Assets/_Scripts/Utility/ProceduralTexturePrograms.cs does, renders it, and prints PSNR / SSIM against the
shipped file. Doc: Docs/GENOME_TEXTURE_PROGRAMS.md.

  python3 Tools/Build/texture_programs.py            # table
  python3 Tools/Build/texture_programs.py --check    # exit 1 if any program falls below its floor
  python3 Tools/Build/texture_programs.py --png DIR  # also write each rendered texture (full size) to DIR

Measurements:
  fit256   - target Lanczos-resized to 256x256, program rendered at 256 (the study's metric, reproduces it)
  grid64   - original sampled at 64x64 pixel centres (nearest) against the program at the same uv; this is
             what the Unity EditMode test (ProceduralTextureProgramsTests) measures, its floors come from here
  baked    - program baked to the proposed small size, bilinear-upsampled to the shipped size, PSNR at full res
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image

try:
    from scipy import ndimage
except ImportError:  # SSIM needs scipy; PSNR does not
    ndimage = None

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))

# name: (asset path, program hex, channels, proposed baked size (w, h), floors {metric: dB})
PROGRAMS = {
    "SunsetGradient": (
        "Assets/_Graphics/Texture/SunsetGradient.png",
        "063f9f9d000000000000000000ee3095ee3594ee3e91ef498bef5485ef5f7df06a74f0736af07c5ff08453f08b47f0903af0942f00",
        "rgb", (16, 256), {"fit256": 56.0, "grid64": 54.0, "baked": 54.0}),
    "Grad_00": (
        "Assets/_Graphics/Materials/CrystalMaterials/Textures/Grad_00.tga",
        "06ff80650002132e537c9eb2b7b09a774e2a100000",
        "grey", (256, 4), {"fit256": 40.0, "grid64": 40.0, "baked": 40.0}),
    "Circle01_v1": (
        "Assets/Effects Library/Froglet Stuff/Textures/Circle01_v1.png",
        "078080aa404040403f3f3e3e3d3d3c3c3b3b393937373535333331312f2f2c2c29292727242422221f1f1d1d1b1b181816161414121210100f0f0d0d0c0c0a0a09090808070706060505050509090f0f16161f1f26263d3d5353e4e40000040400000101000000000000000000000000000000000000000000000000000000000000000000",
        "pgrey_a", (256, 256), {"fit256": 28.0, "grid64": 26.5, "baked": 26.5}),
}

CHANNELS = {"rgb": 3, "grey": 1, "pgrey_a": 2}
LINGRAD, RADIAL = 6, 7


def decode(hexstr, channels):
    b = bytes.fromhex(hexstr)
    tpl, p = b[0], [x / 255.0 for x in b[1:4]]
    c = CHANNELS[channels]
    lut = np.frombuffer(b[4:-1], np.uint8).astype(np.float64).reshape(-1, c) / 255.0
    if tpl == LINGRAD:
        params = dict(angle=2 * np.pi * p[0], pos=-1 + 2 * p[1], length=0.25 * 16.0 ** p[2])
    elif tpl == RADIAL:
        params = dict(cx=0.4 + 0.2 * p[0], cy=0.4 + 0.2 * p[1], length=0.5 * 4.0 ** p[2])
    else:
        raise ValueError(f"unknown template {tpl}")
    return tpl, params, lut


def field(tpl, d, u, v):
    """u, v in [0,1], v measured from the TOP row of the image (PIL row order)."""
    if tpl == LINGRAD:
        a = d["angle"]
        return np.clip(((u - .5) * np.cos(a) + (v - .5) * np.sin(a)) / d["length"] + 0.5 + d["pos"] * 0.5, 0, 1)
    return np.clip(np.hypot(u - d["cx"], v - d["cy"]) * 2 / d["length"], 0, 1)


def apply_lut(g, lut):
    k = lut.shape[0]
    xs = np.linspace(0, 1, k)
    return np.stack([np.interp(g, xs, lut[:, ch]) for ch in range(lut.shape[1])], -1)


def render(name, w, h):
    _, hx, ch, _, _ = PROGRAMS[name]
    tpl, d, lut = decode(hx, ch)
    v, u = np.mgrid[0:h, 0:w].astype(np.float64)
    return apply_lut(field(tpl, d, (u + .5) / w, (v + .5) / h), lut)


def as_channels(rgba, ch):
    """Shipped RGBA in [0,1] -> the program's channel layout (premultiplied grey + alpha for sprites)."""
    rgb, a = rgba[..., :3], rgba[..., 3]
    if ch == "rgb":
        return rgb
    luma = rgb @ np.array([0.299, 0.587, 0.114])
    if ch == "grey":
        return rgb.mean(-1)[..., None]
    return np.stack([luma * a, a], -1)


def load(name):
    path = os.path.join(ROOT, PROGRAMS[name][0])
    return Image.open(path).convert("RGBA")


def psnr(a, b):
    mse = float(np.mean((a - b) ** 2))
    return 99.0 if mse < 1e-12 else 10 * np.log10(1.0 / mse)


def ssim(a, b):
    if ndimage is None:
        return float("nan")
    a, b = a[..., 0], b[..., 0]
    s = 1.5
    ma, mb = ndimage.gaussian_filter(a, s), ndimage.gaussian_filter(b, s)
    saa = ndimage.gaussian_filter(a * a, s) - ma ** 2
    sbb = ndimage.gaussian_filter(b * b, s) - mb ** 2
    sab = ndimage.gaussian_filter(a * b, s) - ma * mb
    c1, c2 = 0.01 ** 2, 0.03 ** 2
    return float((((2 * ma * mb + c1) * (2 * sab + c2)) / ((ma ** 2 + mb ** 2 + c1) * (saa + sbb + c2))).mean())


def measure(name):
    path, hx, ch, (bw, bh), floors = PROGRAMS[name]
    im = load(name)
    W, H = im.size
    full = np.asarray(im).astype(np.float64) / 255.0
    t256 = as_channels(np.asarray(im.resize((256, 256), Image.LANCZOS)).astype(np.float64) / 255.0, ch)
    p256 = render(name, 256, 256)
    # grid64: nearest pixel centres, same as the C# test
    idx_x = ((np.arange(64) + .5) / 64 * W).astype(int)
    idx_y = ((np.arange(64) + .5) / 64 * H).astype(int)
    tgrid = as_channels(full[idx_y][:, idx_x], ch)
    v, u = np.meshgrid((idx_y + .5) / H, (idx_x + .5) / W, indexing="ij")
    tpl, d, lut = decode(hx, ch)
    pgrid = apply_lut(field(tpl, d, u, v), lut)
    # baked: small texture, bilinear upsample to full size
    small = render(name, bw, bh)
    up = np.stack([np.asarray(Image.fromarray(small[..., c].astype(np.float32), "F").resize((W, H), Image.BILINEAR))
                   for c in range(small.shape[-1])], -1).astype(np.float64)
    tfull = as_channels(full, ch)
    return dict(name=name, path=path, file_bytes=os.path.getsize(os.path.join(ROOT, path)), size=(W, H),
                program_bytes=len(bytes.fromhex(hx)), baked=(bw, bh),
                fit256=psnr(p256, t256), ssim256=ssim(p256, t256), grid64=psnr(pgrid, tgrid),
                baked_psnr=psnr(up, tfull), floors=floors)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="exit 1 if any measurement is below its floor")
    ap.add_argument("--png", metavar="DIR", help="write each program rendered at the shipped size")
    args = ap.parse_args()
    bad = []
    print(f"{'texture':16} {'shipped':>10} {'size':>10} {'prog B':>6} {'fit256 dB':>9} {'SSIM':>6} "
          f"{'grid64 dB':>9} {'baked':>8} {'baked dB':>8}")
    for name in PROGRAMS:
        m = measure(name)
        print(f"{name:16} {m['file_bytes']:>10} {'%dx%d' % m['size']:>10} {m['program_bytes']:>6} "
              f"{m['fit256']:>9.2f} {m['ssim256']:>6.3f} {m['grid64']:>9.2f} {'%dx%d' % m['baked']:>8} "
              f"{m['baked_psnr']:>8.2f}")
        f = m["floors"]
        for key, val in (("fit256", m["fit256"]), ("grid64", m["grid64"]), ("baked", m["baked_psnr"])):
            if val < f[key]:
                bad.append(f"{name}: {key} {val:.2f} dB < floor {f[key]} dB")
        if args.png:
            os.makedirs(args.png, exist_ok=True)
            W, H = m["size"]
            img = render(name, W, H)
            ch = PROGRAMS[name][2]
            if ch == "rgb":
                out = img
            elif ch == "grey":
                out = np.repeat(img, 3, -1)
            else:
                a = img[..., 1:2]
                out = np.concatenate([np.repeat(np.where(a > 1e-6, img[..., :1] / np.maximum(a, 1e-6), 0), 3, -1), a], -1)
            Image.fromarray((np.clip(out, 0, 1) * 255 + .5).astype(np.uint8)).save(os.path.join(args.png, name + ".png"))
    if args.check:
        if bad:
            print("FAIL\n  " + "\n  ".join(bad))
            return 1
        print("OK: every program is at or above its floor")
    return 0


if __name__ == "__main__":
    sys.exit(main())
