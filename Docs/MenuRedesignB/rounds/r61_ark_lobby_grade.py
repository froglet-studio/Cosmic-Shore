"""Grade the Rev 60 lobby renders for the boards, as a camera would: mid-tones a little deeper, a cold cast in the shadows,
bloom on what glows (a near halo and a wide one), lateral chromatic fringing toward the frame's edges, a vignette so the
cards at the edges sit on dark ground, and fine film grain (seeded). JPEG out.

    python3 r60_ark_lobby_grade.py <src_dir> <dst_dir>        every lobby_*.png -> <dst_dir>/lobby_*.jpg
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter


INSIDE = ("_pads_", "_cross_", "_board_")


def grade(path, out):
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.float32) / 255.0
    if any(k in os.path.basename(path) for k in INSIDE):        # the hangar's cameras: lift the dark bay a stop's worth
        a = 1.0 - np.power(1.0 - np.clip(a * 1.25, 0, 1), 1.6)
    lum = (0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2])[..., None]
    # mid-tones down, highlights kept: a gamma that leaves the brightest tenth where it was
    keep = np.clip((lum - 0.72) / 0.28, 0.0, 1.0)
    dark = np.power(a, 1.18)
    a = dark * (1 - keep) + a * keep
    # a cold cast in the shadows, as the cell's light falls in
    a[..., 2] = np.clip(a[..., 2] + 0.025 * (1 - lum[..., 0]), 0, 1)
    # bloom: the bright pass, blurred wide and near, added back
    bright = np.clip((a - 0.62) * 2.2, 0, 1)
    b_img = Image.fromarray((bright * 255).astype(np.uint8))
    near = np.asarray(b_img.filter(ImageFilter.GaussianBlur(6))).astype(np.float32) / 255.0
    wide = np.asarray(b_img.filter(ImageFilter.GaussianBlur(30))).astype(np.float32) / 255.0
    a = np.clip(a + near * 0.35 + wide * 0.45, 0, 1)
    # vignette
    h, w = a.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((xx - w / 2) / (w * 0.62)) ** 2 + ((yy - h * 0.52) / (h * 0.66)) ** 2)
    v = np.clip(1.0 - np.power(np.clip(d - 0.35, 0, None) / 0.75, 1.6) * 0.78, 0.18, 1.0)[..., None]
    a = a * v
    # lateral chromatic aberration: red scaled out, blue scaled in, about the centre (up to ~2 px at the corners)
    def scaled(ch, k):
        im_ = Image.fromarray((ch * 255).astype(np.uint8))
        W2, H2 = int(round(w * k)), int(round(h * k))
        im_ = im_.resize((W2, H2), Image.BICUBIC)
        x0, y0 = (W2 - w) // 2, (H2 - h) // 2
        if k >= 1:
            im_ = im_.crop((x0, y0, x0 + w, y0 + h))
        else:
            pad = Image.new("L", (w, h)); pad.paste(im_, (-x0, -y0)); im_ = pad
        return np.asarray(im_).astype(np.float32) / 255.0
    a = np.stack([scaled(a[..., 0], 1.0018), a[..., 1], scaled(a[..., 2], 0.9982)], axis=-1)
    # film grain, finer in the lights
    g = np.random.default_rng(60).normal(0.0, 1.0, (h, w)).astype(np.float32)
    lum2 = (0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2])
    a = np.clip(a + (g * (0.022 - 0.012 * lum2))[..., None], 0, 1)
    Image.fromarray((a * 255).astype(np.uint8)).save(out, quality=90, optimize=True)


if __name__ == "__main__":
    src, dst = sys.argv[1], sys.argv[2]
    os.makedirs(dst, exist_ok=True)
    for f in sorted(os.listdir(src)):
        if f.startswith("lobby_") and f.endswith(".png"):
            grade(os.path.join(src, f), os.path.join(dst, f[:-4] + ".jpg"))
            print("graded", f)
