#!/usr/bin/env python3
"""The HyperSea map's frames for r67_ark_tabs_render.py: the Voyage Board's Sector chart (board_sector.png) with a vessel
marker in the squad's domain travelling the route from the Ark at Tumult toward Tollmoor and back (a seamless loop),
a short wake behind it, and the Ark's ring at Tumult breathing. One PNG per loop frame.

    python3 r67_map_frames.py <assets_dir> [frames=32] [domain=Jade]   writes <assets_dir>/map[_<domain>]/map_NNNN.png
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ASSETS = sys.argv[1]
N = int(sys.argv[2]) if len(sys.argv) > 2 else 32
DOM = sys.argv[3] if len(sys.argv) > 3 else "Jade"
MARK = {"Jade": (60, 255, 245), "Ruby": (235, 60, 255), "Gold": (255, 170, 40)}[DOM]           # the squad's domain
WAKE = {"Jade": (0, 1, 1), "Ruby": (1, 0.2, 1), "Gold": (1, 0.6, 0.1)}[DOM]
ROUTE = [(1000, 890), (1070, 937), (1097, 953), (1167, 991), (1185, 1007)]   # Tumult -> Tollmoor, in the 2x chart's pixels
SEG = [math.dist(ROUTE[i], ROUTE[i + 1]) for i in range(len(ROUTE) - 1)]
TOT = sum(SEG)


def at(s):
    d = s * TOT
    for i, L in enumerate(SEG):
        if d <= L or i == len(SEG) - 1:
            u = min(1.0, d / L)
            a, b = ROUTE[i], ROUTE[i + 1]
            return (a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u), math.atan2(b[1] - a[1], b[0] - a[0])
        d -= L


base = Image.open(os.path.join(ASSETS, "board_sector.png")).convert("RGB")
out_dir = os.path.join(ASSETS, "map" if DOM == "Jade" else f"map_{DOM}")
os.makedirs(out_dir, exist_ok=True)
for f in range(N):
    t = f / N
    s = 0.5 - 0.5 * math.cos(math.tau * t)                 # out and back, so the loop closes
    going = math.sin(math.tau * t) >= 0
    (x, y), ang = at(s)
    if not going:
        ang += math.pi
    glow = Image.new("RGB", base.size, (0, 0, 0))
    g = ImageDraw.Draw(glow)
    pr = 34 + 10 * math.sin(math.tau * 2 * t)              # the Ark's ring at Tumult, breathing
    g.ellipse((1000 - pr, 890 - pr, 1000 + pr, 890 + pr), outline=(255, 190, 70), width=5)
    for k in range(6):                                     # the wake
        (wx, wy), _ = at(max(0.0, min(1.0, s - (0.035 * (k + 1) if going else -0.035 * (k + 1)))))
        r = 10 - k * 1.3
        c = int(200 - k * 30)
        g.ellipse((wx - r, wy - r, wx + r, wy + r), fill=tuple(int(c * w) for w in WAKE))
    pts = [(x + 26 * math.cos(ang), y + 26 * math.sin(ang)), (x + 15 * math.cos(ang + 2.5), y + 15 * math.sin(ang + 2.5)),
           (x + 15 * math.cos(ang - 2.5), y + 15 * math.sin(ang - 2.5))]
    g.polygon(pts, fill=MARK)
    halo = glow.filter(ImageFilter.GaussianBlur(14))
    a = np.asarray(base).astype(np.float32) + np.asarray(glow).astype(np.float32) * 0.9 + np.asarray(halo).astype(np.float32) * 1.4
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).resize((1400, 1027), Image.LANCZOS).save(os.path.join(out_dir, f"map_{f:04d}.png"))
print("maps", N)
