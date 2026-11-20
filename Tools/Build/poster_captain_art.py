#!/usr/bin/env python3
"""The welcome poster's captain pictures, one per card, drawn to the card's own shape.

The busts (Docs/Pilots/art/busts/) are framed tight: ears, horns and hair touch the picture's edges, so any crop into
the poster's tall leaning cards cut them. This draws each card's picture at the card's exact aspect: the bust is
scaled so its top sits under the card's top with room to spare and so the part that must show (KEEP: the head, the
horns) fits across the lean, centred where the lean puts the card's middle at head height. Behind it is the same
bust heavily its own upper third stretched, blurred and dimmed (a soft field of the colours behind its head, with no shapes to read), and the bust's top and
sides fade into it. The bottom is the card's foot and is not faded.

author_start_flow.py imports these files byte for byte. Run after changing CAPTAINS or the card sizes; --check fails
on a missing picture, and a picture is redrawn when deleted or with --force.

    python3 Tools/Build/poster_captain_art.py [--check] [--force]
"""
import argparse
import io
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BUSTS = "Docs/Pilots/art/busts"
OUT = "Docs/MenuRedesignB/build_refs/captains"
SCALE = 2                 # picture pixels per poster pixel
HEAD_ROOM = 0.07          # at least this share of the card's height above the bust's top
FEATHER = 0.07            # the bust's sides fade over this share of its size
TOP_FEATHER = 0.16        # and its top over this share
BACK_BLUR, BACK_DIM = 0.06, 0.7   # the field behind: blur radius as a share of the card's width, brightness
# The part of each bust (left, right, as shares of its width) that must stay inside the card. Horns and ears that
# reach out are listed here; the default is the face.
KEEP = {"ANVIL": (0.12, 0.86), "Spare06": (0.26, 0.70), "Maestro": (0.28, 0.80)}
KEEP_DEFAULT = (0.28, 0.72)


def build(name, h, w, slant):
    box = w + round(h * slant)                     # the card's rect; the lean takes h * slant off each row
    W, H, lean = box * SCALE, h * SCALE, h * slant * SCALE
    bust = Image.open(os.path.join(REPO, BUSTS, f"{name}.jpg")).convert("RGB")
    left, right = KEEP.get(name, KEEP_DEFAULT)
    size = min(H * (1 - HEAD_ROOM), w * SCALE * 0.94 / (right - left))
    size = int(size)
    top = H - size
    head_row = top + size * 0.18                   # where the horns and ears are
    middle = (w * SCALE) / 2 + lean * (1 - head_row / H)    # the card's middle on that row
    x = int(round(middle - size * (left + right) / 2))

    # The field: the bust blurred to colour alone, covering the card.
    # The bust's own upper third (the sky and street behind the head), stretched over the card, so the field
    # continues the colours the bust's top fades from.
    upper = bust.crop((0, 0, bust.width, bust.height // 3))
    field = upper.resize((max(W, x + size) - min(0, x), H), Image.LANCZOS).crop((-min(0, x), 0, -min(0, x) + W, H))
    field = ImageEnhance.Brightness(field.filter(ImageFilter.GaussianBlur(W * BACK_BLUR))).enhance(BACK_DIM)

    b = bust.resize((size, size), Image.LANCZOS)
    f = int(size * FEATHER)
    sides, top_fade = Image.new("L", (size, size), 255), Image.new("L", (size, size), 255)
    ds, dt = ImageDraw.Draw(sides), ImageDraw.Draw(top_fade)
    for i in range(f):
        alpha = int(255 * (i + 1) / (f + 1))
        ds.line([(i, 0), (i, size)], fill=alpha)
        ds.line([(size - 1 - i, 0), (size - 1 - i, size)], fill=alpha)
    t = int(size * TOP_FEATHER)
    for i in range(t):
        dt.line([(0, i), (size, i)], fill=int(255 * ((i + 1) / (t + 1)) ** 1.5))
    mask = ImageChops.multiply(sides, top_fade)
    field.paste(b, (x, top), mask)
    buf = io.BytesIO()
    field.save(buf, "JPEG", quality=92, optimize=True)
    return buf.getvalue()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()
    sys.path.insert(0, os.path.dirname(__file__))
    from author_start_flow import CAPTAINS, SLANT
    missing = []
    for name, h, w in CAPTAINS:
        path = os.path.join(REPO, OUT, f"{name}.jpg")
        if os.path.exists(path) and not a.force:
            continue
        missing.append(path)
        if not a.check:
            os.makedirs(os.path.dirname(path), exist_ok=True)
            open(path, "wb").write(build(name, h, w, SLANT))
            print("wrote", os.path.relpath(path, REPO))
    if a.check and missing:
        sys.exit("missing captain pictures (run poster_captain_art.py): " + ", ".join(missing))
    print("OK: every captain has its poster picture")


if __name__ == "__main__":
    main()
