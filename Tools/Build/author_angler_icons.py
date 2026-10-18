#!/usr/bin/env python3
"""Author the Angler's ability-row icons and its card images.

    python3 Tools/Build/author_angler_icons.py           # render + write the PNGs and their .metas
    python3 Tools/Build/author_angler_icons.py --check   # re-render, compare to the committed PNGs and metas
    python3 Tools/Build/author_angler_icons.py --sheet F # also write a preview sheet (icons tinted on the HUD navy + cards)

The house icon language (author_brittlestar_icons.py, after the Thresher's art pass): a bold WHITE glyph on a transparent
128 px canvas, multiply-tinted at runtime by the four-icon row, drawn at 8x and downsampled. Each names the ACT, in the
Angler Command Lab's words (Docs/Studios/ANGLER_COMMAND_LAB.md, round 2 - the Sling painter):

  Charge  Command   the Sling painter: the beam's cone over a row of painted prisms, the ball thrown down it
  Mass    Leash     the school: five small fish on a spiral round the hull
  Space   Lure      the esca: a bulb hanging from a curved stalk, its light rays out
  Time    Tame      a fish held in the light, the taming arc three-quarters full round it

The element order is the PROPOSAL in R_VesselActions/ANGLER.md (no ElementalAbilityMaps/Angler.asset is authored until
the designer approves a mapping), so an icon names its act, never its element.

The CARD (SO_Class_Angler.IconActive / IconInactive, 256 px) WAS a flat drawing (until 2026-10-18; the card is now rendered
from the Angler's model by Tools/Build/vessel_model/render_cards.py and this tool keeps only its .metas) in the house card look the Thresher's
renderer set (gold body, a green accent, a navy outline; the inactive fade is R.inactive below): an
anglerfish in profile with its lit esca. The Angler wears the Stoat's procedural hull for now (ANGLER.md §5), so there
is no hull of its own to render; when it has one, render the card from it the way render_brittlestar_card_icon.py does.

Deterministic guids; each .meta is the house sprite meta with the guid swapped. --check decodes and compares pixels
(tolerance 2/255) rather than bytes, so a different zlib build cannot fail it.
"""
import math
import os
import re
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import author_thresher_icons as T  # noqa: E402  (shared primitives; importing it writes nothing)


class R:
    """The card look's inactive fade and sprite meta, as render_thresher_card_icon.py defined them (that renderer was
    retired 2026-10-18 when the Thresher's card moved to its model; these two lines of the house look stay here)."""

    DONOR_META = os.path.join(os.path.dirname(os.path.dirname(HERE)), "Assets", "_Graphics", "CardImages", "Squirrel.png.meta")

    @staticmethod
    def inactive(img):
        out = img.copy()
        grey = out[..., :3] @ np.array([0.299, 0.587, 0.114])
        out[..., :3] = 0.5 * out[..., :3] + 0.5 * grey[..., None]
        out[..., :3] = out[..., :3] * 0.7 + 0.3
        out[..., 3] *= 0.85
        return out

    @staticmethod
    def sprite_meta(guid):
        donor = open(R.DONOR_META, encoding="utf-8").read()
        out = re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {guid}", donor, count=1, flags=re.M)
        assert out.count(guid) == 1, "donor meta did not take the guid"
        return out


ROOT = os.path.dirname(os.path.dirname(HERE))
OUT_DIR = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "AbilityIcons", "Angler")
CARD_DIR = os.path.join(ROOT, "Assets", "_Graphics", "CardImages")
DONOR_META = os.path.join(ROOT, "Assets", "_Graphics", "Icons", "Icon_Generate.png.meta")
SIZE, K = T.SIZE, T.K
P, circle, line, poly, arc = T.P, T.circle, T.line, T.poly, T.arc
from angler_icon_ids import CARD_ACTIVE, CARD_INACTIVE, guid_for  # noqa: E402  (the ids, shared with the asset generator)


def fish(d, cx, cy, s, facing=1.0, fill=255):
    """A small fish in profile: a lens body and a forked tail, `s` its half length."""
    pts = [(cx + facing * s * math.cos(math.radians(a)), cy + s * 0.42 * math.sin(math.radians(a))) for a in range(0, 360, 12)]
    poly(d, pts, fill)
    tx = cx - facing * s
    poly(d, [(tx + facing * s * 0.15, cy), (tx - facing * s * 0.55, cy - s * 0.5), (tx - facing * s * 0.4, cy), (tx - facing * s * 0.55, cy + s * 0.5)], fill)


# -- the four icons ----------------------------------------------------------------------------------------------
# The element map was approved on 2026-10-16 (R_VesselActions/ANGLER.md §7): Charge = Erase / Lock-on, Mass = Lure,
# Tame & Grow, Space = Paint, Time = Dash to the School. The FILE names are round 1's proposal's and are kept, because
# the HUD's sprite references hold their guids (guid_for(name)); each file now draws its slot's approved ability.

def paint(d):
    # Space - the beam's cone from the nose (left) over five painted prisms; the ball (three dots) flying down its axis.
    nose = (14, 64)
    poly(d, [nose, (114, 30), (114, 98)], 70)                       # the beam, faint
    line(d, [nose, (114, 30)], 4.0)
    line(d, [nose, (114, 98)], 4.0)
    for i, (x, y) in enumerate(((78, 48), (92, 58), (104, 70), (90, 80), (102, 44))):
        poly(d, [(x - 5, y - 5), (x + 5, y - 5), (x + 5, y + 5), (x - 5, y + 5)])   # painted prisms
    for x in (36, 48, 60):
        circle(d, x, 64, 5.5)                                       # the ball, thrown


def lock_on(d):
    # Charge - the eraser's narrow beam ending in a lock ring round a hull, two fish closing on it.
    nose = (12, 96)
    line(d, [nose, (78, 50)], 4.0)                                  # the beam
    arc(d, 88, 44, 22, 0, 360, 5.0)                                 # the lock ring
    for a in (45, 135, 225, 315):                                   # its four ticks
        x, y = math.cos(math.radians(a)), math.sin(math.radians(a))
        line(d, [(88 + x * 16, 44 + y * 16), (88 + x * 28, 44 + y * 28)], 4.0)
    poly(d, [(88, 34), (98, 50), (78, 50)])                         # the hunted hull
    fish(d, 40, 30, 11, facing=1)
    fish(d, 104, 100, 11, facing=-1)


def lure_grow(d):
    # Mass - the esca's bulb and rays above a sprouting plant: the lure that tames and grows.
    circle(d, 80, 34, 12)
    for a in range(-90, 271, 45):
        r0, r1 = 18, 26
        line(d, [(80 + r0 * math.cos(math.radians(a)), 34 + r0 * math.sin(math.radians(a))),
                 (80 + r1 * math.cos(math.radians(a)), 34 + r1 * math.sin(math.radians(a)))], 4.0)
    line(d, [(18, 30), (40, 18), (68, 28)], 5.0)                    # the stalk
    line(d, [(46, 116), (46, 74)], 5.0)                             # the plant's stem
    poly(d, [(46, 86), (24, 72), (20, 92)])                         # leaf
    poly(d, [(46, 78), (68, 62), (72, 84)])                         # leaf


def dash(d):
    # Time - the hull (left) streaking three-quarters of the way to its school's centre (right).
    for i, (x, y) in enumerate(((96, 46), (108, 64), (96, 82), (114, 84), (112, 44))):
        fish(d, x, y, 8, facing=-1)
    poly(d, [(32, 64), (12, 52), (17, 64), (12, 76)])               # the hull, nose toward its school
    for k, w in ((0, 5.0), (1, 3.0), (2, 2.0)):                     # speed lines
        y = 56 + k * 8
        line(d, [(38, y), (66 - k * 6, y)], w)
    poly(d, [(70, 52), (84, 64), (70, 76)])                         # the arrowhead, at the 75% mark


def seed(d):
    # The omni card: a seed with its first shoot - the plant an omni crystal leaves, planted on the next lure release.
    pts = [(64 + 22 * math.cos(math.radians(a)), 82 + 30 * math.sin(math.radians(a))) for a in range(0, 360, 10)]
    poly(d, pts)                                                    # the seed
    line(d, [(64, 54), (64, 30)], 5.0)                              # the shoot
    poly(d, [(64, 38), (40, 22), (38, 40)])                         # leaf
    poly(d, [(64, 32), (88, 16), (90, 34)])                         # leaf


ICONS = {
    "Angler_Seed.png": seed,           # the omni card: a flora seed (round 4)
    "Angler_Command.png": lock_on,     # Charge: Erase / Lock-on
    "Angler_Leash.png": lure_grow,     # Mass: Lure, Tame & Grow
    "Angler_Lure.png": paint,          # Space: Paint
    "Angler_Tame.png": dash,           # Time: Dash to the School
}


def render(draw_fn):
    big = Image.new("L", (SIZE * K, SIZE * K), 0)
    draw_fn(ImageDraw.Draw(big))
    alpha = big.resize((SIZE, SIZE), Image.LANCZOS)
    img = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
    img.putalpha(alpha)
    return img


# -- the card --------------------------------------------------------------------------------------------------

CARD = 256
CK = 4
GOLD, GOLD_DARK, GREEN, NAVY = (255, 208, 68), (196, 150, 40), (157, 255, 46), (20, 30, 60)


def card_active():
    """An anglerfish in profile, facing right: a deep jawed body, a fin, a stalk arching over the head to a lit esca."""
    W = CARD * CK
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    Q = lambda x, y: (x * CK, y * CK)

    def body_pts(scale):
        cx, cy = 118, 150
        pts = []
        for a in range(0, 360, 6):
            t = math.radians(a)
            rx, ry = 78 * scale, (52 if math.sin(t) < 0 else 60) * scale
            pts.append(Q(cx + rx * math.cos(t), cy + ry * math.sin(t)))
        return pts

    d.polygon(body_pts(1.06), fill=NAVY + (255,))                       # the outline
    d.polygon([Q(30, 120), Q(58, 150), Q(30, 186)], fill=NAVY + (255,))  # tail outline
    d.polygon(body_pts(1.0), fill=GOLD + (255,))
    d.polygon([Q(36, 128), Q(60, 150), Q(36, 178)], fill=GOLD_DARK + (255,))   # tail
    d.polygon([Q(120, 98), Q(150, 72), Q(158, 104)], fill=GOLD_DARK + (255,))  # dorsal fin
    # the jaw: a dark wedge with teeth
    d.polygon([Q(150, 150), Q(198, 132), Q(200, 176)], fill=NAVY + (255,))
    for x in range(160, 198, 8):
        d.polygon([Q(x, 141 + (x - 160) * -0.35), Q(x + 4, 152), Q(x + 8, 139 + (x - 160) * -0.35)], fill=(250, 245, 230, 255))
    d.ellipse([Q(150, 120), Q(166, 136)], fill=NAVY + (255,))            # the eye
    d.ellipse([Q(155, 124), Q(160, 129)], fill=(250, 245, 230, 255))
    # the stalk and the esca, lit
    stalk = [Q(138 + 70 * t, 104 - 70 * math.sin(t * math.pi * 0.8)) for t in [i / 20 for i in range(21)]]
    d.line(stalk, fill=NAVY + (255,), width=9 * CK, joint="curve")
    d.line(stalk, fill=GOLD_DARK + (255,), width=5 * CK, joint="curve")
    ex, ey = stalk[-1][0] / CK, stalk[-1][1] / CK
    for r, a in ((34, 40), (26, 80), (19, 140)):
        d.ellipse([Q(ex - r, ey - r), Q(ex + r, ey + r)], fill=GREEN + (a,))
    d.ellipse([Q(ex - 12, ey - 12), Q(ex + 12, ey + 12)], fill=NAVY + (255,))
    d.ellipse([Q(ex - 9, ey - 9), Q(ex + 9, ey + 9)], fill=GREEN + (255,))
    small = img.resize((CARD, CARD), Image.LANCZOS)
    return np.asarray(small).astype(np.float64) / 255.0


def to_image(arr):
    return Image.fromarray(np.clip(np.round(arr * 255), 0, 255).astype(np.uint8), "RGBA")


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
    """Every file this tool owns: path -> (image, meta text). Since 2026-10-18 the Angler has its own model and its
    CARD images (Angler.png / Angler_Inactive.png) are rendered from it by Tools/Build/vessel_model/render_cards.py,
    so this tool owns only the card .metas (their guids, which SO_Class_Angler names); card_active() below is the
    flat drawing it used to write, kept for the record and no longer written."""
    return {os.path.join(OUT_DIR, name): (render(fn), sprite_meta(guid_for(name))) for name, fn in ICONS.items()}


def card_metas():
    """The two card .metas this tool still owns: path -> meta text."""
    return {os.path.join(CARD_DIR, name) + ".meta": R.sprite_meta(guid_for(name)) for name in (CARD_ACTIVE, CARD_INACTIVE)}


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
        for meta_path, meta in card_metas().items():
            if not os.path.exists(meta_path) or open(meta_path, encoding="utf-8").read() != meta:
                bad.append(f"{os.path.relpath(meta_path, ROOT)} missing or drifted")
        if not os.path.exists(OUT_DIR + ".meta") or open(OUT_DIR + ".meta", encoding="utf-8").read() != folder_meta():
            bad.append("icon folder .meta missing or drifted")
        for b in bad:
            print("FAIL:", b)
        print("author_angler_icons: " + ("OK" if not bad else f"{len(bad)} finding(s)"))
        return 0 if not bad else 1
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(OUT_DIR + ".meta", "w", encoding="utf-8", newline="\n") as f:
        f.write(folder_meta())
    for meta_path, meta in card_metas().items():
        with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(meta)
    for path, (img, meta) in files.items():
        img.save(path, optimize=False, compress_level=9)
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(meta)
        print(f"wrote {os.path.relpath(path, ROOT)}")
    if "--sheet" in argv:
        icons = [img for p, (img, _) in files.items() if p.startswith(OUT_DIR)]
        cards = [img for p, (img, _) in files.items() if p.startswith(CARD_DIR)]
        sheet = Image.new("RGBA", (SIZE * len(icons) + CARD * len(cards), CARD), (20, 30, 60, 255))
        for i, img in enumerate(icons):
            sheet.alpha_composite(img, (i * SIZE, (CARD - SIZE) // 2))
        for i, img in enumerate(cards):
            sheet.alpha_composite(img, (SIZE * len(icons) + i * CARD, 0))
        sheet.save(argv[argv.index("--sheet") + 1])
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
