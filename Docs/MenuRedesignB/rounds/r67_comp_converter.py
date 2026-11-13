#!/usr/bin/env python3
"""The Converter tab's frames: Garrett's omni combine (captured from Docs/Studios/OmniCombineLab.html by
r67_omni_capture.cjs, transparent, from a low camera to match a view from inside the hangar) laid over the pier loop
(r67_ark_tabs_render.py's `pier` scene: from inside the Ark, out of the open mouth, down the pier into the cell), over
the round deck in the pier's middle, with the crystals' own light bloomed into the cell around them.

    python3 r67_comp_converter.py <render_out> <capture_dir> <set> [cx cy scale]

writes three runs under <render_out>, each graded and encoded by r67_assemble.py:
  converter_<set>_loop     32 frames: the four crystals at rest doing their effects (1.6 s forward and back)
  converter_<set>_convert  the combine up to the Omni made (the lab's morph, 10.7 s), played once
  converter_<set>_done     32 frames: the Omni held over the deck (its hold, forward and back)
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

out, cap, SET = sys.argv[1], sys.argv[2], sys.argv[3]
CX, CY, S = (float(v) for v in sys.argv[4:7]) if len(sys.argv) > 6 else (480.0, 270.0, 0.44)
MORPH_END = 107                                             # frame of the lab's morph end (10.7 s at 10 fps)
bg_dir = os.path.join(out, f"pier_{SET}_loop")
BG = sorted(f for f in os.listdir(bg_dir) if f.endswith(".png"))


def lay(bg_path, fg_path, dst):
    bg = np.asarray(Image.open(bg_path).convert("RGB")).astype(np.float32) / 255
    fg = Image.open(fg_path).convert("RGBA")
    k = bg.shape[1] / 960.0                                  # placement is given at 960 wide
    fg = fg.resize((round(fg.width * S * k), round(fg.height * S * k)), Image.LANCZOS)
    layer = Image.new("RGBA", (bg.shape[1], bg.shape[0]), (0, 0, 0, 0))
    layer.paste(fg, (round(CX * k - fg.width / 2), round(CY * k - fg.height / 2)))
    a = np.asarray(layer).astype(np.float32) / 255
    rgb, al = a[..., :3], a[..., 3:4]
    glow_src = Image.fromarray((np.clip(rgb * al, 0, 1) * 255).astype(np.uint8))
    g1 = np.asarray(glow_src.filter(ImageFilter.GaussianBlur(6 * k))).astype(np.float32) / 255
    g2 = np.asarray(glow_src.filter(ImageFilter.GaussianBlur(26 * k))).astype(np.float32) / 255
    comp = bg * (1 - al) + rgb * al + g1 * 0.55 + g2 * 0.9
    Image.fromarray((np.clip(comp, 0, 1) * 255).astype(np.uint8)).save(dst)


def run(name, seq):
    d = os.path.join(out, f"converter_{SET}_{name}")
    os.makedirs(d, exist_ok=True)
    for f, src in enumerate(seq):
        lay(os.path.join(bg_dir, BG[f % len(BG)]), src, os.path.join(d, f"f_{f:04d}.png"))
    return len(seq)


idle = sorted(os.listdir(os.path.join(cap, "idle")))
comb = sorted(os.listdir(os.path.join(cap, "combine")))
I = lambda k: os.path.join(cap, "idle", idle[k])            # noqa: E731
C = lambda k: os.path.join(cap, "combine", comb[k])          # noqa: E731
bounce = lambda a, n: [a + i for i in range(n // 2 + 1)] + [a + i for i in range(n // 2 - 1, 0, -1)]   # noqa: E731
n1 = run("loop", [I(k) for k in bounce(0, 32)])
n2 = run("convert", [C(k) for k in range(MORPH_END)])
n3 = run("done", [C(min(len(comb) - 1, k)) for k in bounce(MORPH_END + 4, 32)])
print("converter", SET, n1, n2, n3)
