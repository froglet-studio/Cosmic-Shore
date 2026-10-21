#!/usr/bin/env python3
"""Author Marlin and Jacks' ability-row icons (Garrett, 2026-10-21: "lets give it some models and get it ready to ship";
the row wore the Rhino's sword placeholders).

    python3 Tools/Build/author_marlin_icons.py           # render + write the four PNGs (and a .meta for a new one)
    python3 Tools/Build/author_marlin_icons.py --check   # re-render, compare to the committed PNGs
    python3 Tools/Build/author_marlin_icons.py --sheet F # also write a preview sheet (tinted, on the HUD navy)

The house icon language (author_thresher_icons.py, whose drawing helpers this reuses): a bold WHITE glyph on a
transparent 128 px canvas, multiply-tinted at runtime by the four-icon row. Each names what the element does to the
HERD (R_VesselActions/MARLIN_JACKS.md §2; the cards' titles in Resources/ElementalAbilityMaps/Marlin.asset):

  Charge  Danger Share  a fish with a jagged bolt through it: the herd's danger creatures that sting rivals
  Mass    Breeding      a big fish and two young beside it: the herd's birth rate
  Space   Herding Ring  a jack's dotted ring with the herd pushed out round it
  Time    Herd Speed    a fish with speed lines behind it

Marlin.prefab points its HUD's four icon Images at these (author_marlin_assets.py, ICON_SPRITES). The guids are fixed
here; a missing .meta is cloned from the Thresher's with the guid swapped.
"""
import math
import os
import re
import sys

from PIL import Image, ImageChops

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import author_thresher_icons as T  # noqa: E402  (the house drawing helpers and the 8x render)
from author_thresher_icons import arc, circle, line, poly  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "AbilityIcons", "Marlin")
DONOR_META = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "AbilityIcons", "Thresher", "Thresher_Plant.png.meta")
SIZE = T.SIZE
GUIDS = {
    "Marlin_DangerShare.png": "c3a91e5d7b2f4c68a0e4d1b9f6c2a873",
    "Marlin_Breeding.png": "5e8b2d6a1f9c4b37a2d0e7c4b1f9a645",
    "Marlin_HerdingRing.png": "9f1c4a7e3b6d4e82b5a0c8d2e7f1b396",
    "Marlin_HerdSpeed.png": "2b7d5f9a4c1e4a63b8f2d0e6a9c5b718",
}


def fish(d, cx, cy, length, height, facing=1, fill=255, eye=True):
    """A fish seen from the side, nose toward `facing` (+1 right, -1 left): a lens body and a forked tail."""
    n = 28
    half = length / 2
    body = []
    for i in range(n + 1):
        t = i / n
        x = -half + length * t
        y = math.sin(math.pi * t) ** 0.8 * height / 2
        body.append((cx + facing * x, cy - y))
    for i in range(n, -1, -1):
        t = i / n
        x = -half + length * t
        y = math.sin(math.pi * t) ** 0.8 * height / 2
        body.append((cx + facing * x, cy + y))
    poly(d, body, fill)
    tx = cx - facing * half
    poly(d, [(tx + facing * length * 0.06, cy), (tx - facing * length * 0.24, cy - height * 0.62),
             (tx - facing * length * 0.18, cy), (tx - facing * length * 0.24, cy + height * 0.62)], fill)
    if eye:
        circle(d, cx + facing * half * 0.62, cy - height * 0.10, max(1.6, height * 0.09), fill=0)


def danger_share(d):
    fish(d, 66, 66, 68, 40, facing=1)
    # a jagged bolt across the body, cut out of it and standing proud above and below
    bolt = [(76, 20), (60, 58), (74, 60), (56, 108), (90, 52), (74, 50), (90, 20)]
    poly(d, [(x + 3, y) for x, y in bolt], 0)
    poly(d, bolt)


def breeding(d):
    fish(d, 60, 52, 66, 36, facing=1)
    fish(d, 84, 96, 34, 18, facing=1)
    fish(d, 44, 102, 28, 15, facing=1)


def herding_ring(d):
    cx, cy, r = 64, 64, 26
    # the ring: dots round the jack, as the jacks' view draws it
    for i in range(16):
        a = math.radians(i * 360 / 16)
        circle(d, cx + math.cos(a) * r, cy + math.sin(a) * r, 3.2)
    # the jack at its centre, nose up
    poly(d, [(cx, cy - 11), (cx + 7, cy + 7), (cx, cy + 3), (cx - 7, cy + 7)])
    # the herd pushed out round it
    for ang in (205, 320, 90):
        a = math.radians(ang)
        fx, fy = cx + math.cos(a) * (r + 22), cy + math.sin(a) * (r + 22)
        fish(d, fx, fy, 26, 13, facing=1 if math.cos(a) >= 0 else -1, eye=False)


def herd_speed(d):
    fish(d, 78, 64, 62, 36, facing=1)
    for y, x0, x1 in ((46, 16, 34), (64, 12, 30), (82, 16, 34)):
        line(d, [(x0, y), (x1, y)], 6)


ICONS = {
    "Marlin_DangerShare.png": danger_share,
    "Marlin_Breeding.png": breeding,
    "Marlin_HerdingRing.png": herding_ring,
    "Marlin_HerdSpeed.png": herd_speed,
}


def meta_guid(path):
    if not os.path.exists(path + ".meta"):
        return None
    m = re.search(r"^guid: ([0-9a-f]{32})$", open(path + ".meta", encoding="utf-8").read(), re.M)
    return m.group(1) if m else None


def main(argv):
    check = "--check" in argv
    errors, sheet = [], []
    os.makedirs(OUT_DIR, exist_ok=True)
    if not check and not os.path.exists(OUT_DIR + ".meta"):
        with open(OUT_DIR + ".meta", "w", encoding="utf-8") as f:
            f.write("fileFormatVersion: 2\nguid: 7d4a1c9e2b5f4836a0c3e8d1f6b2a947\nfolderAsset: yes\nDefaultImporter:\n"
                    "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    for name, fn in ICONS.items():
        path = os.path.join(OUT_DIR, name)
        guid = meta_guid(path)
        if guid is None and not check:
            donor = open(DONOR_META, encoding="utf-8").read()
            with open(path + ".meta", "w", encoding="utf-8") as f:
                f.write(re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + GUIDS[name], donor, count=1, flags=re.M))
            guid = GUIDS[name]
        if guid != GUIDS[name]:
            errors.append(f"{name}: its .meta guid is not {GUIDS[name]} (Marlin.prefab's HUD names it)")
            continue
        img = T.render(fn)
        alpha = img.getchannel("A")
        coverage = sum(alpha.histogram()[i] * i for i in range(256)) / (255.0 * SIZE * SIZE)
        if not 0.08 <= coverage <= 0.45:
            errors.append(f"{name}: glyph covers {coverage:.0%} of the canvas (want 8-45% - a bold read, not a block)")
        bbox = alpha.getbbox()
        if bbox and (bbox[0] < 1 or bbox[1] < 1 or bbox[2] > SIZE - 1 or bbox[3] > SIZE - 1):
            errors.append(f"{name}: the glyph touches the canvas edge {bbox}")
        if check:
            if not os.path.exists(path):
                errors.append(f"{name}: missing (run the tool)")
                continue
            diff = ImageChops.difference(Image.open(path).convert("RGBA").getchannel("A"), alpha).getextrema()[1]
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
    print("author_marlin_icons: " + ("OK" if not errors else f"{len(errors)} finding(s)"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
