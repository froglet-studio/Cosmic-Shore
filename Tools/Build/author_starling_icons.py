#!/usr/bin/env python3
"""Author the Starlings' ability-row icons.

    python3 Tools/Build/author_starling_icons.py           # render + write the PNGs and their .metas
    python3 Tools/Build/author_starling_icons.py --check   # re-render, compare to the committed PNGs and metas
    python3 Tools/Build/author_starling_icons.py --sheet F # also write a preview sheet (icons on the HUD navy)

The house icon language (author_angler_icons.py): a bold WHITE glyph on a transparent 128 px canvas, multiply-tinted at
runtime by the four-icon row, drawn at 8x and downsampled. The Starlings' four buttons each pick the hand's card of one
element (R_VesselActions/STARLING.md), so each icon is a CARD with its element's mark on it:

  Charge  a lightning bolt      Mass  a prism (hexagon)      Space  a ring round a black hole      Time  an hourglass

and the objective card (left of the omnicrystal's) draws the phase's target with no card frame: a faceted gem (Omnicrystal),
three shards (Elemental crystals), a crosshair on a hull (Opposing vessels), a wall of prism blocks (Opposing mass).

The card images (SO_Class_Starling) are rendered from the model by Tools/Build/vessel_model/render_cards.py, not here.
Deterministic guids (starling_icon_ids.py); each .meta is the house sprite meta with the guid swapped. --check compares pixels
(tolerance 2/255), not bytes.
"""
import math
import os
import re
import sys

from PIL import Image, ImageChops, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import author_thresher_icons as T  # noqa: E402  (shared primitives; importing it writes nothing)
from starling_icon_ids import guid_for  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(HERE))
OUT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "AbilityIcons", "Starling")
DONOR_META = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "Icon_Generate.png.meta")
SIZE, K = T.SIZE, T.K
circle, line, poly, arc, ring = T.circle, T.line, T.poly, T.arc, T.ring


def card(d):
    """The card every icon sits on: a rounded outline, upright, 76 x 104."""
    x0, y0, x1, y1, r, w = 26, 12, 102, 116, 12, 6.0
    line(d, [(x0 + r, y0), (x1 - r, y0)], w)
    line(d, [(x0 + r, y1), (x1 - r, y1)], w)
    line(d, [(x0, y0 + r), (x0, y1 - r)], w)
    line(d, [(x1, y0 + r), (x1, y1 - r)], w)
    arc(d, x0 + r, y0 + r, r, 180, 270, w)
    arc(d, x1 - r, y0 + r, r, 270, 360, w)
    arc(d, x1 - r, y1 - r, r, 0, 90, w)
    arc(d, x0 + r, y1 - r, r, 90, 180, w)


def charge(d):
    card(d)
    poly(d, [(72, 26), (44, 68), (62, 68), (54, 102), (86, 56), (67, 56), (78, 26)])


def mass(d):
    card(d)
    pts = [(64 + 26 * math.cos(math.radians(a)), 64 + 26 * math.sin(math.radians(a))) for a in range(30, 390, 60)]
    poly(d, pts)
    for a in (90, 210, 330):   # the facets of a prism seen end on
        line(d, [(64, 64), (64 + 26 * math.cos(math.radians(a)), 64 + 26 * math.sin(math.radians(a)))], 3.0, fill=0)


def space(d):
    card(d)
    circle(d, 64, 64, 10)
    arc(d, 64, 64, 24, 200, 470, 5.0)
    circle(d, 64 + 24 * math.cos(math.radians(110)), 64 + 24 * math.sin(math.radians(110)), 5)


def time(d):
    card(d)
    line(d, [(44, 30), (84, 30)], 5.0)
    line(d, [(44, 98), (84, 98)], 5.0)
    poly(d, [(48, 34), (80, 34), (66, 64), (62, 64)])
    poly(d, [(62, 64), (66, 64), (80, 94), (48, 94)], fill=255)
    poly(d, [(54, 40), (74, 40), (64, 58)], fill=0)   # the sand has run out of the top


# ---- the objective card (the core card left of the omnicrystal's): what the swarm is going for in this phase, no card frame

def obj_omni(d):
    """Omnicrystal: a big faceted gem."""
    poly(d, [(64, 14), (102, 52), (64, 114), (26, 52)])
    for a, b in (((26, 52), (102, 52)), ((48, 52), (64, 114)), ((80, 52), (64, 114)), ((48, 52), (64, 14)), ((80, 52), (64, 14))):
        line(d, [a, b], 3.5, fill=0)


def obj_crystals(d):
    """Elemental crystals: a cluster of three shards."""
    poly(d, [(64, 16), (80, 48), (72, 104), (56, 104), (48, 48)])
    poly(d, [(34, 46), (46, 66), (42, 106), (30, 106), (24, 66)])
    poly(d, [(94, 46), (104, 66), (98, 106), (86, 106), (82, 66)])


def obj_vessels(d):
    """Opposing vessels: a crosshair on a hull's arrowhead."""
    ring(d, 64, 64, 44, 6.0)
    for a, b in (((64, 8), (64, 30)), ((64, 98), (64, 120)), ((8, 64), (30, 64)), ((98, 64), (120, 64))):
        line(d, [a, b], 6.0)
    poly(d, [(64, 40), (84, 84), (64, 74), (44, 84)])


def obj_mass(d):
    """Opposing mass: a wall of prism blocks."""
    for (x, y) in ((28, 80), (64, 80), (100, 80), (46, 48), (82, 48), (64, 16)):
        poly(d, [(x - 16, y), (x + 16, y), (x + 16, y + 28), (x - 16, y + 28)])
        line(d, [(x - 16, y + 9), (x + 16, y + 9)], 3.0, fill=0)


DRAW = dict(zip(("Starling_Charge.png", "Starling_Mass.png", "Starling_Space.png", "Starling_Time.png",
                 "Starling_ObjOmni.png", "Starling_ObjCrystals.png", "Starling_ObjVessels.png", "Starling_ObjMass.png"),
                (charge, mass, space, time, obj_omni, obj_crystals, obj_vessels, obj_mass)))


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


def outputs():
    return {os.path.join(OUT_DIR, name): (render(fn), sprite_meta(guid_for(name))) for name, fn in DRAW.items()}


def main(argv):
    files = outputs()
    if "--check" in argv:
        bad = []
        for path, (img, meta) in files.items():
            name = os.path.relpath(path, ROOT)
            if not os.path.exists(path):
                bad.append(f"{name} missing")
                continue
            diff = ImageChops.difference(Image.open(path).convert("RGBA"), img).getextrema()
            if max(hi for _, hi in diff) > 2:
                bad.append(f"{name} differs from its render - re-run without --check")
            if not os.path.exists(path + ".meta") or open(path + ".meta", encoding="utf-8").read() != meta:
                bad.append(f"{name}.meta missing or drifted")
        if not os.path.exists(OUT_DIR + ".meta") or open(OUT_DIR + ".meta", encoding="utf-8").read() != folder_meta():
            bad.append("icon folder .meta missing or drifted")
        for b in bad:
            print("FAIL:", b)
        print("author_starling_icons: " + ("OK" if not bad else f"{len(bad)} finding(s)"))
        return 0 if not bad else 1
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_DIR + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(folder_meta())
    for path, (img, meta) in files.items():
        img.save(path, optimize=False, compress_level=9)
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(meta)
        print(f"wrote {os.path.relpath(path, ROOT)}")
    if "--sheet" in argv:
        icons = [img for img, _ in files.values()]
        sheet = Image.new("RGBA", (SIZE * len(icons), SIZE), (20, 30, 60, 255))
        for i, img in enumerate(icons):
            sheet.alpha_composite(img, (i * SIZE, 0))
        sheet.save(argv[argv.index("--sheet") + 1])
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
