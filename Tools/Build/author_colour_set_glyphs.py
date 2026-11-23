#!/usr/bin/env python3
"""
Draw the white UI glyphs the Profile's Colour sets page (Rev 112 D, Docs/MENU_REDESIGN_B.md section 187) tints at run
time from the set shown, so a list of any number of sets shows each set's real prisms:

    Assets/_Graphics/UI/Kit/kit_prism_face.png     a prism's silhouette (the long bipyramid the boards draw), tinted the face
    Assets/_Graphics/UI/Kit/kit_prism_lit.png      its upper-left facet, tinted the face lifted toward the rim
    Assets/_Graphics/UI/Kit/kit_prism_rim.png      its outline, tinted the rim
    Assets/_Graphics/UI/Chrome/chrome_eye.png      the eye that marks a Color Blind supported set

White and alpha only: every colour comes from the worn or browsed SO_ColorSet (CLAUDE.md, the colour-set rule). The
pixels are deterministic, so --check compares them byte for byte; the guids are fixed (md5 of the name), so a re-run
never breaks a reference.

    python3 Tools/Build/author_colour_set_glyphs.py            # write
    python3 Tools/Build/author_colour_set_glyphs.py --check    # fail if a glyph or its meta drifted
"""
import argparse
import hashlib
import io
import os
import sys

from PIL import Image, ImageDraw

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
KIT = "Assets/_Graphics/UI/Kit"
CHROME = "Assets/_Graphics/UI/Chrome"
SS = 4   # supersampling

# The prism in the boards' 40 x 92 units (r54_colour_sets_modern.prism): tip, the girdle at y 38 (centre 44), foot.
TIP, LEFT, MID, RIGHT, FOOT = (20, 0), (2, 38), (20, 44), (38, 38), (20, 92)
PRISM_W, PRISM_H = 80, 184          # px: 2 per unit


def _canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (255, 255, 255, 0))


def _done(im, w, h):
    return im.resize((w, h), Image.LANCZOS)


def _pts(points, pad, scale):
    return [(pad + x * scale, pad + y * scale) for x, y in points]


def prism(kind):
    pad = 4                                          # px, so the outline's stroke is not clipped
    w, h = PRISM_W + 2 * pad, PRISM_H + 2 * pad
    im = _canvas(w, h)
    d = ImageDraw.Draw(im)
    sc = SS * PRISM_W / 40
    p = SS * pad
    outline = _pts((TIP, RIGHT, FOOT, LEFT), p, sc)
    if kind == "face":
        d.polygon(outline, fill=(255, 255, 255, 255))
    elif kind == "lit":
        d.polygon(_pts((TIP, LEFT, MID), p, sc), fill=(255, 255, 255, 255))
    else:
        d.line(outline + [outline[0]], fill=(255, 255, 255, 255), width=int(3 * SS), joint="curve")
        d.line(_pts((LEFT, MID, RIGHT), p, sc), fill=(255, 255, 255, 110), width=int(1.5 * SS), joint="curve")
    return _done(im, w, h)


def eye():
    w, h = 64, 48
    im = _canvas(w, h)
    d = ImageDraw.Draw(im)
    s = SS
    # The almond: two arcs from corner to corner, a 3 px stroke, and the pupil.
    d.arc((4 * s, 6 * s, 60 * s, 70 * s), 214, 326, fill=(255, 255, 255, 255), width=3 * s)
    d.arc((4 * s, -22 * s, 60 * s, 42 * s), 34, 146, fill=(255, 255, 255, 255), width=3 * s)
    d.ellipse((23 * s, 15 * s, 41 * s, 33 * s), outline=(255, 255, 255, 255), width=3 * s)
    return _done(im, w, h)


GLYPHS = {
    f"{KIT}/kit_prism_face.png": lambda: prism("face"),
    f"{KIT}/kit_prism_lit.png": lambda: prism("lit"),
    f"{KIT}/kit_prism_rim.png": lambda: prism("rim"),
    f"{CHROME}/chrome_eye.png": eye,
}


def png_bytes(im):
    buf = io.BytesIO()
    im.save(buf, format="PNG", optimize=False)
    return buf.getvalue()


def guid_for(path):
    return hashlib.md5(f"CosmicShore/UIGlyph/{os.path.basename(path)}".encode()).hexdigest()


def meta_text(path):
    """The kit's own sprite import settings (kit_hex_fill.png.meta / a chrome glyph's), with this glyph's guid."""
    donor = f"{KIT}/kit_hex_fill.png.meta" if path.startswith(KIT) else f"{CHROME}/chrome_crystal.png.meta"
    with open(os.path.join(REPO, donor), encoding="utf-8") as f:
        text = f.read()
    lines = text.splitlines(keepends=True)
    out = []
    for line in lines:
        if line.startswith("guid: "):
            line = f"guid: {guid_for(path)}\n"
        out.append(line)
    return "".join(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    drift = []
    for path, make in GLYPHS.items():
        want = {path: png_bytes(make()), path + ".meta": meta_text(path).encode()}
        for p, data in want.items():
            full = os.path.join(REPO, p)
            have = open(full, "rb").read() if os.path.exists(full) else None
            if have == data:
                continue
            if a.check:
                drift.append(p)
            else:
                with open(full, "wb") as f:
                    f.write(data)
                print(f"wrote {p}")
    if drift:
        sys.exit("author_colour_set_glyphs --check: drifted: " + ", ".join(drift))
    print(f"author_colour_set_glyphs{' --check' if a.check else ''}: ok ({len(GLYPHS)} glyphs)")


if __name__ == "__main__":
    main()
