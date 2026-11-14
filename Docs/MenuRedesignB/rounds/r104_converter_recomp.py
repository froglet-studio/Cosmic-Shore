#!/usr/bin/env python3
"""The Converter's three clips, recomposed (Rev 104, Shombith 2026-11-13: "The actual crystals are clipping and does
not seem to be the center of attraction"). r67_comp_converter.py laid a 960 capture whose top crystal ran off its own
frame over the pier, so the crystal was cut and sat on the ceiling beam. Here the combine is captured at 1920 x 1080
with the lab's camera pulled back (r67_omni_capture.cjs, VD=10, pitch 0.12) so nothing leaves the frame, and laid over
the clean pier still (Frogma blob artifacts_open, already graded) 0.62 of its size, centred on the pier's deck, under
the ceiling beam: the crystals span y 362-809 of 1080 over every frame of all three runs; the beam's foot is at 350.

    python3 r104_converter_recomp.py <pier.png> <capture_dir> <out_dir> 960 586 0.62   (ONLY=1: two test frames)
then, per run (loop, convert, done):
    ffmpeg -framerate 10 -i <out_dir>/<run>/f_%04d.png -vf "hqdn3d=2:2:8:8,framerate=fps=30" -c:v libx264
           -preset slow -crf 24 -pix_fmt yuv420p -movflags +faststart -an Assets/_Graphics/Video/Ark/converter_Nocturne_<run>.mp4
"""
import os, sys
import numpy as np
from PIL import Image, ImageFilter

BG, CAP, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
CX, CY, S = (float(v) for v in sys.argv[4:7])
SRC_C = (953.0, 496.0)            # the capture's cluster centre
bg = np.asarray(Image.open(BG).convert("RGB").resize((1920, 1080), Image.LANCZOS)).astype(np.float32) / 255


def lay(fg_path, dst):
    fg = Image.open(fg_path).convert("RGBA")
    fg = fg.resize((round(fg.width * S), round(fg.height * S)), Image.LANCZOS)
    layer = Image.new("RGBA", (1920, 1080), (0, 0, 0, 0))
    layer.paste(fg, (round(CX - SRC_C[0] * S), round(CY - SRC_C[1] * S)))
    a = np.asarray(layer).astype(np.float32) / 255
    rgb, al = a[..., :3], a[..., 3:4]
    glow_src = Image.fromarray((np.clip(rgb * al, 0, 1) * 255).astype(np.uint8))
    g1 = np.asarray(glow_src.filter(ImageFilter.GaussianBlur(12))).astype(np.float32) / 255
    g2 = np.asarray(glow_src.filter(ImageFilter.GaussianBlur(52))).astype(np.float32) / 255
    comp = bg * (1 - al) + rgb * al + g1 * 0.55 + g2 * 0.9
    Image.fromarray((np.clip(comp, 0, 1) * 255).astype(np.uint8)).save(dst)


def run(name, seq):
    d = os.path.join(OUT, name)
    os.makedirs(d, exist_ok=True)
    for f, src in enumerate(seq):
        lay(src, os.path.join(d, f"f_{f:04d}.png"))
    return len(seq)


if __name__ == "__main__":
    idle = sorted(os.listdir(os.path.join(CAP, "idle")))
    comb = sorted(os.listdir(os.path.join(CAP, "combine")))
    I = lambda k: os.path.join(CAP, "idle", idle[k])
    C = lambda k: os.path.join(CAP, "combine", comb[k])
    only = os.environ.get("ONLY")
    if only:
        lay(I(10), os.path.join(OUT, "test_idle.png")); lay(C(130), os.path.join(OUT, "test_done.png")); sys.exit()
    MORPH_END = 107
    bounce = lambda a, n: [a + i for i in range(n // 2 + 1)] + [a + i for i in range(n // 2 - 1, 0, -1)]
    print(run("loop", [I(k) for k in bounce(0, 32)]), run("convert", [C(k) for k in range(MORPH_END)]),
          run("done", [C(min(len(comb) - 1, k)) for k in bounce(MORPH_END + 4, 32)]))
