#!/usr/bin/env python3
"""Rev 129 - Skim Race's card picture made to look like the cell (2026-11-23). Writes r129_assets/*.png.

Shombith, on Rev 128: "its too blue and it does not look the cell in any of the card bg". Three touch-ups of the
shipped render (Assets/_Graphics/ARCADE/CardBackgrounds/SkimRace.png), each a stand-in for a change to
Tools/Build/render_card_backgrounds.py, which would apply it to every card:
  regrade  - the blue wash taken out of the ground and the nucleus, the race's own colours kept
  inside   - regrade + the cell's wall behind the race: the net of many-sided panes the game draws
             (Docs/Pilots/art/refs/sea_flight_trail.jpg), and the white dust
  outside  - the race inside the cell seen from outside: a wire ball of panes in dusty space
             (Docs/Pilots/art/refs/sea_cell_outside.jpg)
Seeded, so a rerun writes the same files.
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, '..', '..', '..')
OUT = os.path.join(HERE, 'r129_assets')
src = np.asarray(Image.open(os.path.join(ROOT, 'Assets/_Graphics/ARCADE/CardBackgrounds/SkimRace.png')).convert('RGB')).astype(float) / 255
H, W, _ = src.shape
rng = np.random.default_rng(129)
GROUND = np.array([0, 4, 31]) / 255          # deep-sea ground #00041F
LINE = np.array([67, 76, 137]) / 255         # #434C89, the net's line


def regrade(a):
    """Take the blue out: the dark ground goes to the deep-sea ground, the glass nucleus to a faint grey glass;
    the race's rings, trails and arrows (bright and saturated) keep their own colour."""
    mx, mn = a.max(2), a.min(2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    lum = a @ np.array([0.3, 0.55, 0.15])
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    nuc = np.sqrt((xx - 518) ** 2 + (yy - 80) ** 2) < 228          # the nucleus's disc in this render
    race = np.clip((mx - 0.42) / 0.18, 0, 1) * np.clip((sat - 0.25) / 0.2, 0, 1)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    # Inside the glass the wash is blue-violet (red and green equal, blue high); a ring or trail differs in hue.
    hue_off = np.maximum(np.clip((g - r - 0.08) * 6, 0, 1), np.clip((r - b + 0.05) * 6, 0, 1))
    inner = np.maximum(np.clip((sat - 0.5) / 0.15, 0, 1), hue_off) * np.clip((mx - 0.3) / 0.2, 0, 1)
    race = np.where(nuc, inner, race)
    ring_w = np.clip((lum - 0.55) / 0.2, 0, 1) * (~nuc)                # the white rings outside the glass
    keep = np.maximum(race, ring_w)[..., None]
    glass = GROUND + (lum[..., None] - 0.12).clip(0) * np.array([0.42, 0.44, 0.55])
    ground = GROUND + (lum[..., None] - 0.05).clip(0) * np.array([0.35, 0.35, 0.5])
    neutral = np.where(nuc[..., None], glass, ground)
    return np.clip(keep * a + (1 - keep) * neutral, 0, 1), keep[..., 0]


def voronoi_net(w, h, n, width, seed):
    r = np.random.default_rng(seed)
    pts = r.uniform([-40, -40], [w + 40, h + 40], size=(n, 2))
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.sqrt((xx[..., None] - pts[:, 0]) ** 2 + (yy[..., None] - pts[:, 1]) ** 2)
    d.sort(axis=2)
    edge = np.clip(1 - (d[..., 1] - d[..., 0]) / width, 0, 1)
    return edge


def dust(w, h, n, seed):
    im = Image.new('L', (w, h), 0)
    dr = ImageDraw.Draw(im)
    r = np.random.default_rng(seed)
    for _ in range(n):
        x, y = r.uniform(0, w), r.uniform(0, h)
        ln, b = r.uniform(1, 5), int(r.uniform(90, 255))
        dr.line([(x, y), (x + ln, y - ln * 0.25)], fill=b, width=1)
    return np.asarray(im).astype(float) / 255


def save(a, name):
    Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, name))


os.makedirs(OUT, exist_ok=True)
g, race = regrade(src)
save(g, 'skim_regrade.png')

# Inside: the wall's panes behind the race, brighter toward the edges (the wall curves away from the middle).
yy, xx = np.mgrid[0:H, 0:W]
rad = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2)
net = voronoi_net(W, H, 70, 2.2, 7) * np.clip(0.25 + rad * 0.55, 0, 0.8)
race = race[..., None]   # where the race is, the net hides behind it
inside = g + (1 - race) * net[..., None] * LINE * 1.05
inside = inside + dust(W, H, 220, 3)[..., None] * 0.55 * (1 - race)
save(inside, 'skim_inside.png')

# Outside: the cell as a wire ball in dusty space, the race inside it; the panes show most at the ball's edge,
# where the wall turns away, as in the game's frame.
cx, cy, R = W * 0.5, H * 0.5, H * 0.47
dd = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / R
fade = np.clip((1.02 - dd) / 0.1, 0, 1)[..., None]
space = GROUND * 0.5 + dust(W, H, 420, 5)[..., None] * 0.8
ball_net = voronoi_net(W, H, 130, 1.8, 11) * np.clip(dd ** 4, 0.08, 1) * (dd < 1)
rim = np.exp(-((dd - 1) / 0.01) ** 2)
out = space * (1 - fade) + g * fade
out = out + (ball_net[..., None] * (1 - race) + rim[..., None] * 0.8) * LINE * 1.8
save(out, 'skim_outside.png')
print('written', OUT)
