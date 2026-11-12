#!/usr/bin/env python3
"""The Converter tab's frames: Garrett's omni combine (captured from Docs/Studios/OmniCombineLab.html by
r67_omni_capture.cjs, transparent) laid over the deck loop (the Ark's open hangar mouth seen from outside, in the cell,
rendered by r67_ark_tabs_render.py's `deck` scene), with the crystals' own light bloomed into the cell around them.
    python3 r67_comp_converter.py <render_out> <capture_dir> <set> [cx cy scale]
writes <render_out>/converter_<set>_loop (32 frames: the four crystals idling, ping-ponged over 1.6 s) and
<render_out>/converter_<set>_convert (the whole combine, 13.7 s, played once)."""
import os, sys
import numpy as np
from PIL import Image, ImageFilter

out, cap, SET = sys.argv[1], sys.argv[2], sys.argv[3]
CX, CY, S = (float(v) for v in sys.argv[4:7]) if len(sys.argv) > 6 else (650.0, 420.0, 0.52)
bg_dir = os.path.join(out, f"deck_{SET}_loop")
BG = sorted(f for f in os.listdir(bg_dir) if f.endswith(".png"))


def lay(bg_path, fg_path, dst):
    bg = np.asarray(Image.open(bg_path).convert("RGB")).astype(np.float32) / 255
    fg = Image.open(fg_path).convert("RGBA")
    w, h = fg.size
    fg = fg.resize((round(w * S), round(h * S)), Image.LANCZOS)
    layer = Image.new("RGBA", (bg.shape[1], bg.shape[0]), (0, 0, 0, 0))
    layer.paste(fg, (round(CX - fg.width / 2), round(CY - fg.height / 2)))
    a = np.asarray(layer).astype(np.float32) / 255
    rgb, al = a[..., :3], a[..., 3:4]
    glow_src = Image.fromarray((np.clip(rgb * al, 0, 1) * 255).astype(np.uint8))
    g1 = np.asarray(glow_src.filter(ImageFilter.GaussianBlur(6))).astype(np.float32) / 255
    g2 = np.asarray(glow_src.filter(ImageFilter.GaussianBlur(26))).astype(np.float32) / 255
    comp = bg * (1 - al) + rgb * al + g1 * 0.55 + g2 * 0.9
    Image.fromarray((np.clip(comp, 0, 1) * 255).astype(np.uint8)).save(dst)


idle = sorted(os.listdir(os.path.join(cap, "idle")))
seq = list(range(0, 17)) + list(range(15, 0, -1))            # 32 frames: forward 1.6 s and back, so the loop closes
d = os.path.join(out, f"converter_{SET}_loop"); os.makedirs(d, exist_ok=True)
for f, k in enumerate(seq):
    lay(os.path.join(bg_dir, BG[f % len(BG)]), os.path.join(cap, "idle", idle[k]), os.path.join(d, f"f_{f:04d}.png"))
comb = sorted(os.listdir(os.path.join(cap, "combine")))
d = os.path.join(out, f"converter_{SET}_convert"); os.makedirs(d, exist_ok=True)
for f, name in enumerate(comb):
    lay(os.path.join(bg_dir, BG[f % len(BG)]), os.path.join(cap, "combine", name), os.path.join(d, f"f_{f:04d}.png"))
print("converter", SET, len(seq), len(comb))
