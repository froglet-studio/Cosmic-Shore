#!/usr/bin/env python3
"""Author the Thresher ability row's icon sprites (the art pass that replaced the 2026-10 placeholders).

    python3 Tools/Build/author_thresher_icons.py           # render + write the four PNGs
    python3 Tools/Build/author_thresher_icons.py --check   # re-render, compare to the committed PNGs
    python3 Tools/Build/author_thresher_icons.py --sheet F # also write a preview sheet (tinted, on the HUD navy)

The house icon language (Assets/_Graphics/Icons/AbilityIcons/Manta/): a bold WHITE glyph on a transparent
128 px canvas, multiply-tinted at runtime by the four-icon row, read at a glance. Drawn at 8x and
downsampled (Lanczos) so every edge is anti-aliased - the placeholders were 1-bit coverage. Each names the
ACT, in the vessel's own vocabulary (THRESHER.md §1-2c):

  Charge  Wrecking Ball  the morning star (twelve-spike ball, as the game now draws it) cracking a prism,
                         with the whip's motion arcs behind it
  Mass    Mass Mode      the ball BUILDING: a row of prisms laid behind it, growing toward it
  Space   Winch          a chain hauled round a drum, the reel-in arrow over it
  Time    Plant          the planted ball as the pivot, the orbit round it, the hull swinging on it

The PNGs keep the guids the placeholders had (the prefab and HUD variant reference them), so this writes
ONLY the PNG bytes - every .meta is untouched. Mass Mode's file was renamed from Thresher_HeavyIron.png
with its .meta (same guid). --check decodes and compares pixels (tolerance 2/255) rather than bytes, so a
different zlib build cannot fail it.
"""
import math
import os
import sys

from PIL import Image, ImageChops, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "AbilityIcons", "Thresher")
SIZE = 128
K = 8                      # supersampling
W = SIZE * K
# The guids the row's references name (asserted against each .meta before writing).
GUIDS = {
    "Thresher_WreckingBall.png": "372925148d56fc4971f260a7f5cdd470",
    "Thresher_MassMode.png": "290bd445e0cc6921b2c8e07e2e99edf2",
    "Thresher_Winch.png": "91ad84c1b3d7d9508b8aa3323cfb54fe",
    "Thresher_Plant.png": "0cd003be08a8ace88fb858daf3916010",
}


def P(x, y):
    """Icon coordinates (0..128, y DOWN) to the supersampled canvas."""
    return (x * K, y * K)


def circle(d, cx, cy, r, fill=255):
    d.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], fill=fill)


def ring(d, cx, cy, r, w, fill=255):
    d.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], outline=fill, width=int(w * K))


def line(d, pts, w, fill=255):
    d.line([P(*p) for p in pts], fill=fill, width=int(w * K), joint="curve")
    for p in (pts[0], pts[-1]):
        circle(d, p[0], p[1], w / 2, fill)


def poly(d, pts, fill=255):
    d.polygon([P(*p) for p in pts], fill=fill)


def arc(d, cx, cy, r, a0, a1, w, fill=255, cap=True):
    """Clockwise in screen space from a0 to a1 degrees (0 = +x), as a thick polyline with round caps."""
    n = max(8, int(abs(a1 - a0) / 3))
    pts = [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n)))
           for i in range(n + 1)]
    d.line([P(*p) for p in pts], fill=fill, width=int(w * K), joint="curve")
    if cap:
        for p in (pts[0], pts[-1]):
            circle(d, p[0], p[1], w / 2, fill)
    return pts


def arrowhead(d, tip, direction, size, fill=255):
    dx, dy = direction
    m = math.hypot(dx, dy)
    dx, dy = dx / m, dy / m
    nx, ny = -dy, dx
    base = (tip[0] - dx * size, tip[1] - dy * size)
    poly(d, [tip, (base[0] + nx * size * 0.62, base[1] + ny * size * 0.62), (base[0] - nx * size * 0.62, base[1] - ny * size * 0.62)], fill)


def morning_star(d, cx, cy, r, spikes=8, spike=0.55, rot=0.0, hole=True):
    """The ball as the game now draws it (ThresherBallForm): a core and spikes all round, with a crescent
    highlight cut out so it reads as a sphere under the tint."""
    for i in range(spikes):
        a = math.radians(rot + i * 360.0 / spikes)
        half = math.radians(360.0 / spikes * 0.2)
        tip = (cx + math.cos(a) * r * (1 + spike), cy + math.sin(a) * r * (1 + spike))
        b1 = (cx + math.cos(a - half) * r * 0.92, cy + math.sin(a - half) * r * 0.92)
        b2 = (cx + math.cos(a + half) * r * 0.92, cy + math.sin(a + half) * r * 0.92)
        poly(d, [tip, b1, b2])
    circle(d, cx, cy, r)
    if hole:
        # Crescent highlight: a thin arc cut out of the upper-left of the core.
        arc(d, cx, cy, r * 0.62, 200, 262, r * 0.2, fill=0)


def chain_link(d, cx, cy, length, width, angle, w):
    """One link: a stadium outline along `angle` degrees."""
    a = math.radians(angle)
    ux, uy = math.cos(a), math.sin(a)
    half = (length - width) / 2
    r = width / 2
    p0 = (cx - ux * half, cy - uy * half)
    p1 = (cx + ux * half, cy + uy * half)
    nx, ny = -uy, ux
    line(d, [(p0[0] + nx * r, p0[1] + ny * r), (p1[0] + nx * r, p1[1] + ny * r)], w)
    line(d, [(p0[0] - nx * r, p0[1] - ny * r), (p1[0] - nx * r, p1[1] - ny * r)], w)
    arc(d, p1[0], p1[1], r, angle - 90, angle + 90, w, cap=False)
    arc(d, p0[0], p0[1], r, angle + 90, angle + 270, w, cap=False)


def prism(d, cx, cy, s, fill=255):
    """A prism seen corner-on: a diamond with a cut showing its lit face."""
    poly(d, [(cx, cy - s), (cx + s * 0.8, cy), (cx, cy + s), (cx - s * 0.8, cy)], fill)
    line(d, [(cx - s * 0.8 + s * 0.28, cy), (cx, cy - s + s * 0.35)], max(1.6, s * 0.16), fill=0)


# -- the four icons -----------------------------------------------------------------------------------

def wrecking_ball(d):
    # A prism shattering where the ball meets it (lower-right), shards flying.
    for (x, y, s, rot) in ((103, 97, 8, 20), (114, 82, 6, -15), (94, 113, 5.5, 50)):
        a = math.radians(rot)
        pts = [(x + math.cos(a + k * 2.1) * s, y + math.sin(a + k * 2.1) * s) for k in range(3)]
        poly(d, pts)
    # Motion arcs behind it: the whip that threw it.
    arc(d, 60, 60, 40, 150, 200, 5.5)
    arc(d, 60, 60, 30, 155, 195, 4.5)
    # The chain into it, three links from the top-left.
    for i, (x, y) in enumerate(((22, 18), (32, 27), (42, 36))):
        chain_link(d, x, y, 13, 7.5, 42 + (i % 2) * 0, 3.0)
    morning_star(d, 66, 66, 22, spikes=8, spike=0.5, rot=22.5)


def mass_mode(d):
    # Prisms laid behind the ball, growing toward it - it BUILDS (Mass Mode), it does not break.
    for (x, y, s) in ((18, 98, 9), (38, 86, 12), (61, 72, 15)):
        prism(d, x, y, s)
    # The ball, ahead of its trail, with a short tow line out of frame.
    morning_star(d, 88, 52, 19, spikes=8, spike=0.5, rot=0)
    line(d, [(102, 36), (116, 18)], 4.2)
    # The build mark: a plus over the newest prism.
    line(d, [(58, 36), (58, 50)], 5)
    line(d, [(51, 43), (65, 43)], 5)


def winch(d):
    # The drum, its hub cut out, chain wound on it.
    circle(d, 46, 58, 25)
    circle(d, 46, 58, 9, fill=0)
    circle(d, 46, 58, 4)
    for a in (35, 95, 155, 215, 275, 335):
        x = 46 + math.cos(math.radians(a)) * 17
        y = 58 + math.sin(math.radians(a)) * 17
        circle(d, x, y, 2.6, fill=0)
    # The chain coming off it toward the ball, lower right.
    for i, (x, y) in enumerate(((72, 78), (82, 88), (92, 98))):
        chain_link(d, x, y, 13, 7.5, 45, 3.0)
    morning_star(d, 105, 109, 8, spikes=6, spike=0.45, rot=15, hole=False)
    # Reel-in: the arrow over the drum, winding the chain IN.
    pts = arc(d, 46, 58, 36, 200, 330, 5.5)
    end = pts[-1]
    prev = pts[-2]
    arrowhead(d, (end[0] + (end[0] - prev[0]) * 2.2, end[1] + (end[1] - prev[1]) * 2.2), (end[0] - prev[0], end[1] - prev[1]), 11)


def plant(d):
    # The planted ball: the pivot at the centre.
    morning_star(d, 60, 66, 13, spikes=8, spike=0.5, rot=22.5, hole=False)
    # The orbit round it, opened where the ship rides (on the left, at 180 deg).
    arc(d, 60, 66, 42, 212, 508, 5.0)
    # The chain: pivot to ship.
    sx, sy = 60 - 42, 66
    line(d, [(60, 66), (sx, sy)], 3.6)
    # The hull on the orbit: a swept dart along the clockwise tangent (straight UP at 180 deg on a y-down
    # canvas), so it reads as swinging round the pivot.
    tx, ty = 0.0, -1.0
    nx, ny = 1.0, 0.0
    nose = (sx + tx * 15, sy + ty * 15)
    poly(d, [nose, (sx - tx * 8 + nx * 10, sy - ty * 8 + ny * 10), (sx - tx * 3, sy - ty * 3), (sx - tx * 8 - nx * 10, sy - ty * 8 - ny * 10)])


ICONS = {
    "Thresher_WreckingBall.png": wrecking_ball,
    "Thresher_MassMode.png": mass_mode,
    "Thresher_Winch.png": winch,
    "Thresher_Plant.png": plant,
}


def render(fn) -> Image.Image:
    mask = Image.new("L", (W, W), 0)
    fn(ImageDraw.Draw(mask))
    mask = mask.resize((SIZE, SIZE), Image.LANCZOS)
    img = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
    img.putalpha(mask)
    return img


def meta_guid(path):
    with open(path + ".meta", encoding="utf-8") as f:
        for line_ in f:
            if line_.startswith("guid: "):
                return line_.split()[1]
    return None


def main(argv):
    check = "--check" in argv
    errors = []
    sheet = []
    for name, fn in ICONS.items():
        path = os.path.join(OUT_DIR, name)
        if meta_guid(path) != GUIDS[name]:
            errors.append(f"{name}: its .meta guid is not {GUIDS[name]} (the row's references would break)")
            continue
        img = render(fn)
        alpha = img.getchannel("A")
        coverage = sum(alpha.histogram()[i] * i for i in range(256)) / (255.0 * SIZE * SIZE)
        if not 0.08 <= coverage <= 0.45:
            errors.append(f"{name}: glyph covers {coverage:.0%} of the canvas (want 8-45% - a bold read, not a block)")
        bbox = alpha.getbbox()
        if bbox and (bbox[0] < 1 or bbox[1] < 1 or bbox[2] > SIZE - 1 or bbox[3] > SIZE - 1):
            errors.append(f"{name}: the glyph touches the canvas edge {bbox}")
        if check:
            old = Image.open(path).convert("RGBA")
            diff = ImageChops.difference(old.getchannel("A"), alpha).getextrema()[1]
            if diff > 2:
                errors.append(f"{name}: differs from a re-render by {diff}/255 (hand edit, or re-run the tool)")
        else:
            img.save(path, optimize=True)
            print(f"wrote {os.path.relpath(path, ROOT)} ({coverage:.0%} coverage)")
        sheet.append(img)
    if "--sheet" in argv and sheet:
        out = argv[argv.index("--sheet") + 1]
        tints = [(255, 214, 64), (120, 200, 255), (130, 255, 120), (255, 120, 220)]
        board = Image.new("RGBA", (SIZE * 2 * len(sheet) + 16, SIZE * 2 + 16), (24, 34, 56, 255))
        for i, img in enumerate(sheet):
            big = img.resize((SIZE * 2, SIZE * 2), Image.LANCZOS)
            tint = Image.new("RGBA", big.size, tints[i] + (255,))
            tint.putalpha(big.getchannel("A"))
            board.alpha_composite(tint, (8 + i * SIZE * 2, 8))
        board.save(out)
    for e in errors:
        print("FAIL: " + e)
    print("author_thresher_icons: " + ("OK" if not errors else f"{len(errors)} finding(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
