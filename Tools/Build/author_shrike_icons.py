#!/usr/bin/env python3
"""Author the Shrike ability row's icon sprites.

    python3 Tools/Build/author_shrike_icons.py           # render + write the four PNGs and their .metas
    python3 Tools/Build/author_shrike_icons.py --check   # re-render, compare to the committed PNGs and metas
    python3 Tools/Build/author_shrike_icons.py --sheet F # also write a preview sheet (on the HUD navy)

The house icon language (the Brittle Star's tool, author_brittlestar_icons.py, which this copies): a bold WHITE glyph on
a transparent 128 px canvas, multiply-tinted at runtime by the four-icon row. Drawn at 8x and downsampled (Lanczos).
Each names the ACT, in the class document's words (R_VesselActions/SHRIKE.md):

  Charge  Void Lance       the hull's beam piercing a block, the block's halves thrown apart
  Mass    Nest             a cup of blocks round the hull, the nose's way out open
  Space   Targeting Array  a lock reticle with a missile curving in on it
  Time    Void Sling       the tether to an anchor block, the hull rushing along it

Deterministic guids; each .meta is the house sprite meta (Icon_Generate.png.meta) with the guid swapped. --check decodes
and compares pixels (tolerance 2/255) rather than bytes.
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
OUT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "AbilityIcons", "Shrike")
DONOR_META = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "Icon_Generate.png.meta")
SIZE, K = T.SIZE, T.K
P, circle, line, poly, arc, ring, arrowhead = T.P, T.circle, T.line, T.poly, T.arc, T.ring, T.arrowhead


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/ShrikeIcons/{name}".encode()).hexdigest()


def hull(d, cx, cy, s, angle=0.0):
    """The Shrike seen from above, nose along `angle` degrees: a long dart, its wings drawn swept BACK (the hull's are
    swept forward, which at 128 px reads as fletching - an arrow pointing the wrong way)."""
    a = math.radians(angle)
    ux, uy = math.cos(a), math.sin(a)
    nx, ny = -uy, ux
    pt = lambda f, w: (cx + ux * f * s + nx * w * s, cy + uy * f * s + ny * w * s)
    poly(d, [pt(1.0, 0), pt(-0.5, 0.2), pt(-0.65, 0), pt(-0.5, -0.2)])
    poly(d, [pt(0.25, 0.1), pt(-0.35, 0.7), pt(-0.55, 0.7), pt(-0.25, 0.12)])
    poly(d, [pt(0.25, -0.1), pt(-0.35, -0.7), pt(-0.55, -0.7), pt(-0.25, -0.12)])


def block(d, cx, cy, s, angle, fill=255):
    a = math.radians(angle)
    ux, uy = math.cos(a) * s, math.sin(a) * s
    vx, vy = -uy, ux
    poly(d, [(cx - ux - vx, cy - uy - vy), (cx + ux - vx, cy + uy - vy), (cx + ux + vx, cy + uy + vy), (cx - ux + vx, cy - uy + vy)], fill)


# -- the four icons --------------------------------------------------------------------------------------

def void_lance(d):
    # The hull (left) and its beam straight through a block, the block's two halves thrown off the line.
    hull(d, 24, 64, 20)
    line(d, [(44, 64), (122, 64)], 7.0)
    block(d, 88, 42, 9, 18)
    block(d, 96, 88, 9, -24)
    for (x, y) in ((76, 52), (104, 76)):
        circle(d, x, y, 3.2)


def nest(d):
    # A cup of blocks round the hull, open to the right (the nose's way out), the turret bud on top.
    for i in range(9):
        a = math.radians(70 + i * 27.5)
        block(d, 60 + 40 * math.cos(a), 66 + 40 * math.sin(a), 6.5, math.degrees(a))
    hull(d, 60, 66, 22)
    poly(d, [(46, 14), (54, 6), (62, 14), (54, 22)])


def targeting_array(d):
    # A lock reticle (right) and one missile curving up and in on it.
    ring(d, 86, 50, 24, 6.0)
    for (x0, y0, x1, y1) in ((86, 18, 86, 32), (86, 68, 86, 82), (54, 50, 68, 50), (104, 50, 118, 50)):
        line(d, [(x0, y0), (x1, y1)], 5.0)
    circle(d, 86, 50, 4.5)
    pts = arc(d, 74, 116, 58, 196, 262, 5.5)
    arrowhead(d, (pts[-1][0] + 8, pts[-1][1] - 3), (1.0, -0.4), 14)


def void_sling(d):
    # The tether from the hull (left) to an anchor block (right); the hull rushing along it, speed lines behind.
    block(d, 106, 64, 12, 0)
    for x in (54, 68, 82):
        line(d, [(x, 64), (x + 8, 64)], 4.0)
    hull(d, 36, 64, 22)
    for (y, x0, x1) in ((44, 4, 22), (84, 4, 22), (64, 2, 12)):
        line(d, [(x0, y), (x1, y)], 4.0)


ICONS = {
    "Shrike_VoidLance.png": void_lance,             # Charge
    "Shrike_Nest.png": nest,                        # Mass
    "Shrike_TargetingArray.png": targeting_array,   # Space
    "Shrike_VoidSling.png": void_sling,             # Time
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
        print("author_shrike_icons: " + ("OK" if not bad else f"{len(bad)} finding(s)"))
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
