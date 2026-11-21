#!/usr/bin/env python3
"""
Author the TWO-TRIGGER control glyph - the chip a card wears when its ability is driven by
holding BOTH triggers at once (InputEvents.BothSticksAction, HintBinding.PadBothTriggers).

PLACEHOLDER ART. The glyph is the two single-trigger chips the ControlGlyphSet already wears
(LT = PS/L1.png, RT = XBOX/R1.png, and their "Active" held twins) laid side by side, so the
two-trigger chip cannot disagree with the single-trigger chips on any other card. When real
art lands, replace the PNGs and retire this script - the guids are what the glyph set keys on.

The composite is pixel-exact (no resampling): left source, a transparent gap, right source,
both vertically centred on the taller of the two.

Usage:
    python3 Tools/Build/author_two_trigger_glyph.py            # write the PNGs + .meta files
    python3 Tools/Build/author_two_trigger_glyph.py --check    # verify, non-zero on drift
"""

import argparse
import hashlib
import os
import re
import struct
import sys
import zlib

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BUTTONS = os.path.join(REPO, "Assets", "_Graphics", "Design Assests", "HUD UI", "Buttons")
OUT_DIR = os.path.join(REPO, "Assets", "_Graphics", "UI", "ControlIcons")
GAP = 4

# name -> (left source, right source)
GLYPHS = {
    "Glyph_BothTriggers-PLACEHOLDER": (os.path.join(BUTTONS, "PS", "L1.png"),
                                       os.path.join(BUTTONS, "XBOX", "R1.png")),
    "Glyph_BothTriggers_Held-PLACEHOLDER": (os.path.join(BUTTONS, "PS", "L1 Active.png"),
                                            os.path.join(BUTTONS, "XBOX", "R1 Active.png")),
}

# The sprite import settings are the source chips' own, so the composite imports identically.
META_TEMPLATE = os.path.join(BUTTONS, "PS", "L1.png.meta")


def guid_for(name):
    """Deterministic guid, stable across runs and machines (asset-surgery skill section 3)."""
    return hashlib.md5(f"CosmicShore/ControlGlyphs/{name}".encode()).hexdigest()


def read_png(path):
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", path
    i, idat, w = 8, b"", None
    while i < len(data):
        n = struct.unpack(">I", data[i:i + 4])[0]
        tag, body = data[i + 4:i + 8], data[i + 8:i + 8 + n]
        if tag == b"IHDR":
            w, h, depth, colour, _, _, interlace = struct.unpack(">IIBBBBB", body)
            assert depth == 8 and colour == 6 and interlace == 0, f"{path}: need 8-bit RGBA, non-interlaced"
        elif tag == b"IDAT":
            idat += body
        i += 12 + n
    raw, stride, bpp = zlib.decompress(idat), w * 4, 4
    rows, prev, pos = [], bytearray(stride), 0
    for _ in range(h):
        f, line = raw[pos], bytearray(raw[pos + 1:pos + 1 + stride])
        pos += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if f == 1:
                line[x] = (line[x] + a) & 255
            elif f == 2:
                line[x] = (line[x] + b) & 255
            elif f == 3:
                line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(bytes(line))
        prev = line
    return w, h, rows


def encode_png(w, h, rows):
    def chunk(tag, body):
        return struct.pack(">I", len(body)) + tag + body + struct.pack(">I", zlib.crc32(tag + body))
    raw = b"".join(b"\x00" + r for r in rows)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def composite(left_path, right_path):
    lw, lh, lrows = read_png(left_path)
    rw, rh, rrows = read_png(right_path)
    w, h = lw + GAP + rw, max(lh, rh)
    blank = lambda n: b"\x00" * (n * 4)
    lo, ro = (h - lh) // 2, (h - rh) // 2
    rows = []
    for y in range(h):
        left = lrows[y - lo] if 0 <= y - lo < lh else blank(lw)
        right = rrows[y - ro] if 0 <= y - ro < rh else blank(rw)
        rows.append(left + blank(GAP) + right)
    return encode_png(w, h, rows)


def meta_for(name):
    text = open(META_TEMPLATE, encoding="utf-8").read()
    text, n = re.subn(r"^guid: [0-9a-f]{32}$", f"guid: {guid_for(name)}", text, count=1, flags=re.M)
    assert n == 1, "template meta has no guid line"
    # A fresh sprite id per asset, deterministic like the guid.
    sprite_id = hashlib.md5(f"CosmicShore/ControlGlyphs/{name}/sprite".encode()).hexdigest()
    text, n = re.subn(r"^(\s*spriteID: )[0-9a-f]{32}$", lambda m: m.group(1) + sprite_id, text, count=1, flags=re.M)
    assert n == 1, "template meta has no spriteID line"
    return text


def build():
    return {name: (composite(l, r), meta_for(name)) for name, (l, r) in GLYPHS.items()}


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[1])
    ap.add_argument("--check", action="store_true", help="verify on-disk output, non-zero on drift")
    args = ap.parse_args()

    built = build()
    if args.check:
        drift = []
        for name, (png, meta) in built.items():
            for ext, want in ((".png", png), (".png.meta", meta.encode())):
                path = os.path.join(OUT_DIR, name + ext)
                got = open(path, "rb").read() if os.path.exists(path) else b""
                if got != want:
                    drift.append(name + ext)
        print(("FAIL: %d file(s) differ - re-run without --check: %s" % (len(drift), ", ".join(drift)))
              if drift else "OK: %d glyph(s) match" % len(built))
        return 1 if drift else 0

    for name, (png, meta) in built.items():
        with open(os.path.join(OUT_DIR, name + ".png"), "wb") as f:
            f.write(png)
        with open(os.path.join(OUT_DIR, name + ".png.meta"), "w", newline="\n") as f:
            f.write(meta)
        print("wrote", name, guid_for(name))
    return 0


if __name__ == "__main__":
    sys.exit(main())
