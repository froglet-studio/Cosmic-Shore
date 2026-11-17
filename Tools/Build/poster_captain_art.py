#!/usr/bin/env python3
"""The welcome poster's captain pictures: each 1024 px bust with headroom painted above it.

The busts (Docs/Pilots/art/busts/) are framed tight, so a head or ear touches the picture's top edge and the poster's
leaning card cut it. This puts the bust at the foot of a taller canvas (the cards' own aspect, about 4:5) and fills the
room above with the bust's own top rows mirrored, blurred and darkened, so the whole character sits inside the card.
author_start_flow.py imports these files byte for byte. Run after changing CAPTAINS; it writes only missing pictures (delete one to
repaint it), and --check fails on a missing one.

    python3 Tools/Build/poster_captain_art.py [--check]
"""
import argparse
import io
import os
import sys

from PIL import Image, ImageEnhance, ImageFilter

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BUSTS = "Docs/Pilots/art/busts"
OUT = "Docs/MenuRedesignB/build_refs/captains"
SIZE = 1024
HEAD_ROOM = 256          # 1024 x 1280 (0.8, the cards' aspect)
BLEND = 96               # rows over which the painted room fades into the bust
BLUR, DIM = 48, 0.5


def build(name):
    bust = Image.open(os.path.join(REPO, BUSTS, f"{name}.jpg")).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS)
    top = bust.crop((0, 0, SIZE, HEAD_ROOM + BLEND)).transpose(Image.FLIP_TOP_BOTTOM)
    room = ImageEnhance.Brightness(top.filter(ImageFilter.GaussianBlur(BLUR))).enhance(DIM)
    out = Image.new("RGB", (SIZE, SIZE + HEAD_ROOM))
    out.paste(room, (0, 0))
    # Fade the bust's top rows in over the room, so there is no seam.
    mask = Image.linear_gradient("L").resize((SIZE, BLEND))
    out.paste(bust.crop((0, 0, SIZE, BLEND)), (0, HEAD_ROOM), mask)
    out.paste(bust.crop((0, BLEND, SIZE, SIZE)), (0, HEAD_ROOM + BLEND))
    buf = io.BytesIO()
    out.save(buf, "JPEG", quality=90, optimize=True)
    return buf.getvalue()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    sys.path.insert(0, os.path.dirname(__file__))
    from author_start_flow import CAPTAINS
    stale = []
    for name, _, _ in CAPTAINS:
        path = os.path.join(REPO, OUT, f"{name}.jpg")
        if not os.path.exists(path):
            stale.append(path)
            if not a.check:
                os.makedirs(os.path.dirname(path), exist_ok=True)
                open(path, "wb").write(build(name))
                print("wrote", os.path.relpath(path, REPO))
    if a.check and stale:
        sys.exit("missing captain pictures (run poster_captain_art.py): " + ", ".join(stale))
    print("OK: every captain has its poster picture")


if __name__ == "__main__":
    main()
