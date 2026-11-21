#!/usr/bin/env python3
"""Author the Brittle Star ability row's icon sprites.

    python3 Tools/Build/author_brittlestar_icons.py           # render + write the four PNGs and their .metas
    python3 Tools/Build/author_brittlestar_icons.py --check   # re-render, compare to the committed PNGs and metas
    python3 Tools/Build/author_brittlestar_icons.py --sheet F # also write a preview sheet (tinted, on the HUD navy)

The house icon language (Assets/_Graphics/Icons/AbilityIcons/Manta/, the Thresher's art pass this copies): a bold WHITE
glyph on a transparent 128 px canvas, multiply-tinted at runtime by the four-icon row, read at a glance. Drawn at 8x and
downsampled (Lanczos), so every edge is anti-aliased. Each names the ACT, in the class document's own words
(R_VesselActions/BRITTLE_STAR.md):

  Charge  Ring Detonation   the pentagon support ring blowing apart, its five sides thrown outward
  Mass    Creation Mode     the pentagon of blocks the trail becomes, a second fainter pentagon laid behind it
  Space   Destruction Mode  the five turrets' streams, fired together
  Time    Speed Mode        the star with every thruster lit, speed lines streaming behind

The drawing helpers are imported from author_thresher_icons.py (one icon language, one set of primitives). Deterministic
guids; each .meta is the house sprite meta (Icon_Generate.png.meta) with the guid swapped. --check decodes and compares
pixels (tolerance 2/255) rather than bytes, so a different zlib build cannot fail it.
"""
import hashlib
import math
import os
import re
import sys

from PIL import Image, ImageChops, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import author_thresher_icons as T  # noqa: E402  (shared primitives; importing it writes nothing)

ROOT = os.path.dirname(os.path.dirname(HERE))
OUT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "AbilityIcons", "BrittleStar")
DONOR_META = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "Icon_Generate.png.meta")
SIZE, K = T.SIZE, T.K
P, circle, line, poly, arc = T.P, T.circle, T.line, T.poly, T.arc


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/BrittleStarIcons/{name}".encode()).hexdigest()


def pentagon(cx, cy, r, rot=-90.0):
    return [(cx + r * math.cos(math.radians(rot + i * 72)), cy + r * math.sin(math.radians(rot + i * 72))) for i in range(5)]


def block(d, cx, cy, s, angle, fill=255):
    """A trail block seen face-on: a rounded-ish square turned to `angle` degrees."""
    a = math.radians(angle)
    ux, uy = math.cos(a) * s, math.sin(a) * s
    vx, vy = -uy * 0.7, ux * 0.7
    poly(d, [(cx - ux - vx, cy - uy - vy), (cx + ux - vx, cy + uy - vy), (cx + ux + vx, cy + uy + vy), (cx - ux + vx, cy - uy + vy)], fill)


# -- the four icons --------------------------------------------------------------------------------------

def ring_detonation(d):
    # The five sides of the ring, each thrown outward from where it was, with a blast at the centre.
    pts = pentagon(64, 66, 34)
    for i in range(5):
        a, b = pts[i], pts[(i + 1) % 5]
        mx, my = (a[0] + b[0]) / 2 - 64, (a[1] + b[1]) / 2 - 66
        m = math.hypot(mx, my)
        push = 9.0
        ox, oy = mx / m * push, my / m * push
        line(d, [(a[0] + ox + (b[0] - a[0]) * 0.12, a[1] + oy + (b[1] - a[1]) * 0.12),
                 (b[0] + ox - (b[0] - a[0]) * 0.12, b[1] + oy - (b[1] - a[1]) * 0.12)], 8.5)
        # A speed tick behind each flying side.
        line(d, [(64 + mx * 0.55, 66 + my * 0.55), (64 + mx * 0.78, 66 + my * 0.78)], 4.0)
    # The blast: a burst star.
    burst = []
    for i in range(10):
        r = 16 if i % 2 == 0 else 7
        a = math.radians(-90 + i * 36)
        burst.append((64 + r * math.cos(a), 66 + r * math.sin(a)))
    poly(d, burst)


def creation_mode(d):
    # The pentagon of blocks the trail becomes (each block tangent to the ring, so the five read as a pentagon), and
    # the one laid before it, smaller and fainter, behind.
    for (cx, cy, r, s, fill) in ((30, 94, 13, 4.2, 140), (74, 56, 29, 8.0, 255)):
        for i, (x, y) in enumerate(pentagon(cx, cy, r)):
            block(d, x, y, s, -90 + i * 72 + 90, fill)
    line(d, [(42, 84), (52, 76)], 3.5, fill=140)


def destruction_mode(d):
    # Five turrets on the pentagon's points (left), all five streams firing right.
    pts = pentagon(30, 64, 22, rot=180)
    for (x, y) in pts:
        circle(d, x, y, 6.5)
        for k, (x0, x1) in enumerate(((x + 12, x + 32), (x + 42, x + 62), (x + 72, x + 90))):
            if x1 > 124:
                x1 = 124
            if x0 < x1:
                line(d, [(x0, y), (x1, y)], 5.5 - k * 0.6)
    line(d, [pts[0], pts[1], pts[2], pts[3], pts[4], pts[0]], 3.0)


def speed_mode(d):
    # The star nose-right, every thruster lit (flames to the left), speed lines streaming behind.
    cx, cy = 80, 64
    pts = pentagon(cx, cy, 22, rot=0)
    line(d, pts + [pts[0]], 6.0)
    circle(d, cx, cy, 8.5)
    for (x, y) in pts:
        line(d, [(cx, cy), (x, y)], 3.5)
        # The flame: a long tapered teardrop streaming back (left).
        poly(d, [(x - 4, y - 4.5), (x - 4, y + 4.5), (x - 26, y)])
    for (y, x0, x1) in ((30, 14, 46), (98, 14, 46), (64, 6, 28)):
        line(d, [(x0, y), (x1, y)], 4.5)


ICONS = {
    "BrittleStar_RingDetonation.png": ring_detonation,   # Charge
    "BrittleStar_CreationMode.png": creation_mode,       # Mass
    "BrittleStar_DestructionMode.png": destruction_mode, # Space
    "BrittleStar_SpeedMode.png": speed_mode,             # Time
}


def render(draw_fn):
    big = Image.new("L", (SIZE * K, SIZE * K), 0)
    draw_fn(ImageDraw.Draw(big))
    alpha = big.resize((SIZE, SIZE), Image.LANCZOS)
    img = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
    img.putalpha(alpha)
    return img


def sprite_meta(guid: str) -> str:
    donor = open(DONOR_META, encoding="utf-8").read()
    out = re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {guid}", donor, count=1, flags=re.M)
    assert out.count(guid) == 1, "donor meta did not take the guid"
    return out


def folder_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {guid_for('folder')}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n")


def main(argv):
    check = "--check" in argv
    images = {name: render(fn) for name, fn in ICONS.items()}
    if check:
        bad = []
        for name, img in images.items():
            path = os.path.join(OUT_DIR, name)
            if not os.path.exists(path):
                bad.append(f"{name} missing")
                continue
            diff = ImageChops.difference(Image.open(path).convert("RGBA"), img).getextrema()
            if max(hi for _, hi in diff) > 2:
                bad.append(f"{name} differs from its render - re-run without --check")
            if open(path + ".meta", encoding="utf-8").read() != sprite_meta(guid_for(name)):
                bad.append(f"{name}.meta drifted")
        if not os.path.exists(OUT_DIR + ".meta") or open(OUT_DIR + ".meta", encoding="utf-8").read() != folder_meta():
            bad.append("folder .meta missing or drifted")
        for b in bad:
            print("FAIL:", b)
        print("author_brittlestar_icons: " + ("OK" if not bad else f"{len(bad)} finding(s)"))
        return 0 if not bad else 1
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_DIR + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(folder_meta())
    for name, img in images.items():
        path = os.path.join(OUT_DIR, name)
        img.save(path, optimize=False, compress_level=9)
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(sprite_meta(guid_for(name)))
        print(f"wrote {os.path.relpath(path, ROOT)} (guid {guid_for(name)})")
    if "--sheet" in argv:
        sheet = Image.new("RGBA", (SIZE * len(images), SIZE), (20, 30, 60, 255))
        for i, img in enumerate(images.values()):
            sheet.alpha_composite(img, (i * SIZE, 0))
        sheet.save(argv[argv.index("--sheet") + 1])
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
