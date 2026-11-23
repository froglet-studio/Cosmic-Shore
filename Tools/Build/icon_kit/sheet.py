"""The contact sheet: every icon of a hull BEFORE and AFTER, at 128 / 60 / 40 px, on a plate like the lockup's.

LOOK at it (Read the PNG) before writing anything into Assets/. The plate is the lockup's plateColor over the
HUD navy, the icon white on it, the way the row draws it. No element colour anywhere: elements have no colour
(Garrett 2026-10-24, LOCKED), and a coloured preview would teach the eye the wrong thing.
"""
import os

from PIL import Image, ImageDraw, ImageFont

import card as card_mod

NAVY = (14, 20, 44, 255)
PLATE = (0, 4, 31, 255)
ELEMENT_LABEL = (150, 156, 190)
SIZES = (128, 60, 40)


def _plate(size, element):
    pad = max(6, size // 8)
    im = Image.new("RGBA", (size + 2 * pad, size + 2 * pad), NAVY)
    d = ImageDraw.Draw(im)
    d.rectangle([2, 2, im.width - 3, im.height - 3], fill=PLATE)
    return im, pad


def tile(icon, size, element):
    im, pad = _plate(size, element)
    ic = icon.convert("RGBA").resize((size, size), Image.LANCZOS)
    im.alpha_composite(ic, (pad, pad))
    return im


def before_after(rows, out_path, title=""):
    """rows: [(name, element, before_image_or_None, after_image)]."""
    font = ImageFont.load_default()
    label_w = 150
    group_w = 100 + sum(s + 2 * max(6, s // 8) + 8 for s in SIZES[1:])
    row_h = 128 + 2 * 16 + 10
    W = label_w + 2 * group_w + 40
    H = 30 + row_h * len(rows)
    sheet = Image.new("RGBA", (W, H), NAVY)
    d = ImageDraw.Draw(sheet)
    d.text((8, 8), title[:24], fill=(230, 230, 230, 255), font=font)
    d.text((label_w, 8), "BEFORE   card as drawn / 60 / 40", fill=(180, 180, 180, 255), font=font)
    d.text((label_w + group_w + 24, 8), "AFTER   card as drawn / 60 / 40  (cards drawn offline from AbilityLockupStyle.asset)", fill=(180, 180, 180, 255), font=font)
    for i, (name, element, before, after) in enumerate(rows):
        y = 30 + i * row_h
        d.text((8, y + 60), name, fill=(230, 230, 230, 255), font=font)
        d.text((8, y + 74), element, fill=ELEMENT_LABEL + (255,), font=font)
        for g, img in enumerate((before, after)):
            x = label_w + g * (group_w + 24)
            if img is None:
                continue
            # The card as the lockup draws it (plates, flower, the icon in its 60 px box), then the raw icon.
            c = card_mod.card(img, element if element in ("Charge", "Mass", "Space", "Time") else "Charge",
                              scale=0.8)
            sheet.alpha_composite(c, (x, y + (row_h - 10 - c.height) // 2))
            x += c.width + 8
            for s in SIZES[1:]:
                t = tile(img, s, element)
                sheet.alpha_composite(t, (x, y + (row_h - 10 - t.height) // 2))
                x += t.width + 8
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    sheet.save(out_path)
    return out_path
