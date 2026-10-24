"""The contact sheet: every icon of a hull BEFORE and AFTER, at 128 / 60 / 40 px, on a plate like the lockup's.

LOOK at it (Read the PNG) before writing anything into Assets/. The plate is the lockup's plateColor over the
HUD navy with the element's colour washed behind the icon, the way the Rev 3 cards wear it; the colours here
are a preview, not the style asset (AbilityLockupStyle.asset is the truth in the Editor).
"""
import os

from PIL import Image, ImageDraw, ImageFont

NAVY = (14, 20, 44, 255)
PLATE = (0, 4, 31, 255)
ELEMENT = {"Charge": (255, 200, 60), "Mass": (70, 130, 255), "Space": (160, 95, 255), "Time": (50, 215, 205)}
SIZES = (128, 60, 40)


def _plate(size, element):
    pad = max(6, size // 8)
    im = Image.new("RGBA", (size + 2 * pad, size + 2 * pad), NAVY)
    d = ImageDraw.Draw(im)
    d.rectangle([2, 2, im.width - 3, im.height - 3], fill=PLATE)
    c = ELEMENT.get(element, (120, 120, 120))
    blob = Image.new("RGBA", im.size, (0, 0, 0, 0))
    bd = ImageDraw.Draw(blob)
    r = int(size * 0.42)
    bd.ellipse([im.width // 2 - r - size // 10, im.height // 2 - r + size // 8, im.width // 2 + r - size // 10, im.height // 2 + r + size // 8],
               fill=c + (70,))
    im.alpha_composite(blob)
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
    group_w = sum(s + 2 * max(6, s // 8) + 8 for s in SIZES)
    row_h = 128 + 2 * 16 + 10
    W = label_w + 2 * group_w + 40
    H = 30 + row_h * len(rows)
    sheet = Image.new("RGBA", (W, H), NAVY)
    d = ImageDraw.Draw(sheet)
    d.text((8, 8), title[:24], fill=(230, 230, 230, 255), font=font)
    d.text((label_w, 8), "BEFORE   128 / 60 / 40", fill=(180, 180, 180, 255), font=font)
    d.text((label_w + group_w + 24, 8), "AFTER   128 / 60 / 40", fill=(180, 180, 180, 255), font=font)
    for i, (name, element, before, after) in enumerate(rows):
        y = 30 + i * row_h
        d.text((8, y + 60), name, fill=(230, 230, 230, 255), font=font)
        d.text((8, y + 74), element, fill=ELEMENT.get(element, (160, 160, 160)) + (255,), font=font)
        for g, img in enumerate((before, after)):
            x = label_w + g * (group_w + 24)
            if img is None:
                continue
            for s in SIZES:
                t = tile(img, s, element)
                sheet.alpha_composite(t, (x, y + (row_h - 10 - t.height) // 2))
                x += t.width + 8
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    sheet.save(out_path)
    return out_path
