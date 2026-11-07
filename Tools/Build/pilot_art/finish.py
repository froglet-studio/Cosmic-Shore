#!/usr/bin/env python3
"""The FINISH: a matted painting at any size becomes the 220x280 sprite, framed the way the painted set is framed.

The 32 references were measured (measure.py --refs, Docs/Pilots/art/style_envelope.json): the figure stands with
its boots on the bottom edge (bottom median 1.000, min 0.989) and its head just under the top (top median 0.009),
its head and torso centres a hair left of centre (0.478 / 0.482, because the pilots turn left and their tails hang
right). The finish puts every new portrait on that same stage, by measurement:

  1. the silhouette (alpha > 0.5) is scaled so its height spans TOP..BOTTOM of the frame (0.985 of it), unless its
     width would pass MAX_WIDTH, in which case the width rules and the figure stands a little shorter (the winged
     and the big-tailed, like Echo and Jugger in the set);
  2. its ANCHOR, the mean of the head band's centre and the torso band's centre (measure.band), goes to CX of the
     frame; the whole silhouette is then shifted the least that keeps it inside the frame (a tail may hang to the
     edge, it never leaves it);
  3. resampling is done on PREMULTIPLIED colour with Lanczos (an RGBA resize on straight colour drags the black of
     the transparent pixels into every edge: the dark halo), then un-premultiplied, with the edge colour extended
     a few pixels into the transparent area so a bilinear sample in the UI never reads a dark pixel;
  4. a light unsharp mask puts back the crispness a 4-5x downsample takes (the set is crisp at 220 px).

Nothing here changes a colour or a value: a tone decision is the painter's (the brief) and is judged on the sheet.

    python3 Tools/Build/pilot_art/finish.py <matted.png> --out <sprite.png>
    python3 Tools/Build/pilot_art/finish.py --self-test
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import measure  # noqa: E402

SIZE = measure.SIZE           # (220, 280)
TOP = 0.010                   # the head's top, as a fraction of the frame height (set median 0.009)
BOTTOM = 0.995                # the boots' soles (set median 1.000: the set touches the edge; one pixel of air here)
CX = 0.49                     # where the head/torso anchor goes (set medians 0.478 / 0.482)
MAX_WIDTH = 0.96              # the widest a silhouette may be (set max 0.991 had its elbow on the edge)
MARGIN_PX = 1                 # the least air at a side when a tail or a wing forces a shift
EXTEND_PX = 4                 # how far the edge colour is pushed into the transparent area
UNSHARP = dict(radius=0.8, percent=40, threshold=2)


def _extend_edges(rgb, alpha, px=EXTEND_PX):
    """Push the nearest opaque colour px pixels into the transparent area (a cheap dilation by neighbour average)."""
    out = rgb.copy()
    have = alpha > 0.0
    for _ in range(px):
        p = np.pad(out, ((1, 1), (1, 1), (0, 0)), mode="edge")
        ph = np.pad(have, 1, constant_values=False)
        acc = np.zeros_like(out)
        cnt = np.zeros(out.shape[:2], dtype=np.float32)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            sl = p[1 + dy:p.shape[0] - 1 + dy, 1 + dx:p.shape[1] - 1 + dx]
            hv = ph[1 + dy:ph.shape[0] - 1 + dy, 1 + dx:ph.shape[1] - 1 + dx]
            acc += sl * hv[..., None]
            cnt += hv
        fill = (~have) & (cnt > 0)
        out[fill] = acc[fill] / cnt[fill][:, None]
        have = have | fill
    return out


def plan(rgba, size=SIZE):
    """Where the silhouette goes: dict(scale, box (x0, y0, x1, y1) of the figure in the frame, crop (source bbox))."""
    W, H = size
    a = rgba[..., 3]
    m = a > measure.ALPHA
    if not m.any():
        raise ValueError("the matte has no figure")
    ys, xs = np.nonzero(m)
    y0, y1 = int(ys.min()), int(ys.max()) + 1
    x0, x1 = int(xs.min()), int(xs.max()) + 1
    fig_h, fig_w = y1 - y0, x1 - x0
    scale = (BOTTOM - TOP) * H / fig_h
    if fig_w * scale > MAX_WIDTH * W:
        scale = MAX_WIDTH * W / fig_w
    out_h = max(1, int(round(fig_h * scale)))
    out_w = max(1, int(round(fig_w * scale)))
    head_cx, _ = measure.band(m, ys, *measure.HEAD_BAND)
    torso_cx, _ = measure.band(m, ys, *measure.TORSO_BAND)
    anchor = ((head_cx + torso_cx) / 2 - x0) * scale          # in output px from the figure's left
    left = int(round(CX * W - anchor))
    left = min(max(left, MARGIN_PX), W - out_w - MARGIN_PX) if out_w + 2 * MARGIN_PX <= W else (W - out_w) // 2
    bottom = int(round(BOTTOM * H))
    top = bottom - out_h
    if top < 0:
        top, bottom = 0, out_h
    return dict(scale=scale, box=(left, top, left + out_w, bottom), crop=(x0, y0, x1, y1), size=(out_w, out_h))


def finish(rgba, size=SIZE):
    """RGBA float (any size, matted) -> PIL RGBA sprite of `size`."""
    p = plan(rgba, size)
    x0, y0, x1, y1 = p["crop"]
    piece = rgba[y0:y1, x0:x1]
    alpha = piece[..., 3:4]
    rgb = _extend_edges(piece[..., :3], piece[..., 3])
    pre = np.concatenate([rgb * alpha, alpha], axis=-1)
    pre8 = Image.fromarray((np.clip(pre, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")
    small = np.asarray(pre8.resize(p["size"], Image.LANCZOS), dtype=np.float32) / 255.0
    a = small[..., 3:4]
    straight = np.where(a > 1e-3, small[..., :3] / np.maximum(a, 1e-3), 0.0)
    straight = np.clip(straight, 0, 1)
    straight = _extend_edges(straight, small[..., 3])
    rgb8 = Image.fromarray((straight * 255 + 0.5).astype(np.uint8), "RGB").filter(ImageFilter.UnsharpMask(**UNSHARP))
    sprite = Image.new("RGBA", size, (0, 0, 0, 0))
    layer = Image.merge("RGBA", (*rgb8.split(), Image.fromarray((small[..., 3] * 255 + 0.5).astype(np.uint8), "L")))
    sprite.paste(layer, (p["box"][0], p["box"][1]))
    return sprite


def finish_file(src, out, size=SIZE):
    sprite = finish(measure.load(src), size)
    sprite.save(out)
    return sprite


def self_test():
    failures = []
    # 1. A tall thin figure lands on the stage: top ~ TOP, bottom ~ BOTTOM, anchor at CX.
    src = np.zeros((1000, 800, 4), dtype=np.float32)
    src[100:900, 300:420, :3] = (0.6, 0.5, 0.4)
    src[100:900, 300:420, 3] = 1.0
    sp = finish(src)
    m = measure.metrics(np.asarray(sp, dtype=np.float32) / 255.0)
    if abs(m["top"] - TOP) > 0.012 or abs(m["bottom"] - BOTTOM) > 0.012:
        failures.append(f"vertical stage missed: top {m['top']:.3f} bottom {m['bottom']:.3f}")
    if abs(m["torso_cx"] - CX) > 0.02:
        failures.append(f"anchor missed: torso_cx {m['torso_cx']:.3f}")
    if sp.size != SIZE:
        failures.append(f"size {sp.size}")
    # 2. A wide figure (wings) is width-ruled and stays inside the frame.
    wide = np.zeros((1000, 1000, 4), dtype=np.float32)
    wide[200:900, 100:900, 3] = 1.0
    wide[200:900, 100:900, :3] = 0.5
    m2 = measure.metrics(np.asarray(finish(wide), dtype=np.float32) / 255.0)
    if m2["width"] > MAX_WIDTH + 0.01 or m2["left"] < 0 or m2["right"] > 1.0:
        failures.append(f"a wide figure left the frame: {m2['left']:.3f}..{m2['right']:.3f}")
    if m2["height"] > BOTTOM - TOP + 0.01:
        failures.append("a wide figure was not shortened")
    # 3. A big tail to the right shifts the anchor only as far as the frame needs.
    tail = np.zeros((1000, 1000, 4), dtype=np.float32)
    tail[100:900, 300:420, 3] = 1.0                 # the body
    tail[600:700, 420:900, 3] = 1.0                 # a long tail to the right
    tail[..., :3] = 0.5
    m3 = measure.metrics(np.asarray(finish(tail), dtype=np.float32) / 255.0)
    if m3["right"] > 1.0 or m3["left"] < 0:
        failures.append("the tail left the frame")
    if not m3["head_cx"] < CX:
        failures.append("the body was not shifted left to make room for the tail")
    # 4. No dark halo: a light figure's edge pixels stay light after the resample.
    light = np.zeros((1000, 800, 4), dtype=np.float32)
    light[100:900, 300:500, :3] = 0.9
    light[100:900, 300:500, 3] = 1.0
    arr = np.asarray(finish(light), dtype=np.float32) / 255.0
    edge = (arr[..., 3] > 0.05) & (arr[..., 3] < 0.95)
    if edge.any() and arr[edge][:, :3].mean() < 0.8:
        failures.append(f"a dark halo on a light figure's edge: {arr[edge][:, :3].mean():.2f}")
    # 5. An empty matte is refused.
    try:
        finish(np.zeros((10, 10, 4), dtype=np.float32))
        failures.append("an empty matte was accepted")
    except ValueError:
        pass
    # 6. Deterministic: the same input twice gives the same bytes.
    if np.asarray(finish(src)).tobytes() != np.asarray(sp).tobytes():
        failures.append("finish is not deterministic")
    for f in failures:
        print("SELF-TEST FAIL:", f)
    print("self-test:", "FAILED" if failures else "OK (6 checks)")
    return 1 if failures else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    paths = [a for a in argv if not a.startswith("--")]
    out = argv[argv.index("--out") + 1] if "--out" in argv else None
    if not paths or not out:
        print(__doc__)
        return 2
    finish_file(paths[0], out)
    print("wrote", out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
