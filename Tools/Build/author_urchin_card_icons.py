#!/usr/bin/env python3
"""Give the Urchin's class asset card icons that EXIST.

`SO_Class_Urchin.IconActive` / `IconInactive` pointed at two sprite guids that no `.meta` in the
tree owns - the art was deleted at some point and the class asset was never re-pointed. A
`UnityEngine.UI.Image` whose sprite is missing draws a SOLID WHITE QUAD in its tint, so the
Arena carousel showed a white square and called it the Urchin (the same failure shape as the
`Pip.prefab` frame in CLAUDE.md's anti-patterns: the only evidence is the rendered frame).

The Urchin's real card art is `Assets/_Graphics/CardImages/Urchin_Square.png` (the class's
`CardSilohoutteActive` already points at it - the same file the Dolphin uses for ITS
`IconActive`, `Dolphin_Square.png`). It has no inactive sibling, so this script derives one the
way the Scarab's is derived - the card family's greyed look - and re-points the class asset:

    Assets/_Graphics/CardImages/Urchin_Inactive.png   (IconInactive, derived from Urchin_Square)
    SO_Class_Urchin.IconActive   -> Urchin_Square.png
    SO_Class_Urchin.IconInactive -> Urchin_Inactive.png

    python3 Tools/Build/author_urchin_card_icons.py           # (re)derive + re-point
    python3 Tools/Build/author_urchin_card_icons.py --check   # fail if either drifted

Deterministic: pure-Python PNG decode/encode, zlib at a fixed level. Re-run it after the source
art changes; never hand-edit the inactive PNG.
"""
import os
import re
import struct
import sys
import zlib

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from render_scarab_card_icons import greyed, encode_png, meta_for  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CARD_DIR = os.path.join(ROOT, "Assets", "_Graphics", "CardImages")
SOURCE = os.path.join(CARD_DIR, "Urchin_Square.png")
INACTIVE = os.path.join(CARD_DIR, "Urchin_Inactive.png")
INACTIVE_GUID = "4d7b2e9a1c6f4b3e8a5d0c2f7e9b1a63"
CLASS_ASSET = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes", "SO_Class_Urchin.asset")


def guid_of(png_path):
    with open(png_path + ".meta", encoding="utf-8") as fh:
        return re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M).group(1)


def decode_png(data):
    """8-bit RGBA, non-interlaced, any filter - which is what every card PNG is."""
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "not a PNG"
    i, idat, ihdr = 8, bytearray(), None
    while i < len(data):
        length = struct.unpack(">I", data[i:i + 4])[0]
        kind, body = data[i + 4:i + 8], data[i + 8:i + 8 + length]
        if kind == b"IHDR":
            ihdr = struct.unpack(">IIBBBBB", body)
        elif kind == b"IDAT":
            idat += body
        i += 12 + length
    w, h, depth, ctype, _, _, interlace = ihdr
    if (depth, ctype, interlace) != (8, 6, 0):
        raise SystemExit(f"{SOURCE}: expected 8-bit RGBA non-interlaced, got depth {depth} type {ctype} interlace {interlace}")
    raw, bpp, stride = zlib.decompress(bytes(idat)), 4, w * 4
    rows, prev, pos = [], bytearray(stride), 0
    for _ in range(h):
        ftype, line = raw[pos], bytearray(raw[pos + 1:pos + 1 + stride])
        pos += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if ftype == 1:
                line[x] = (line[x] + a) & 255
            elif ftype == 2:
                line[x] = (line[x] + b) & 255
            elif ftype == 3:
                line[x] = (line[x] + ((a + b) >> 1)) & 255
            elif ftype == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pred = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pred) & 255
        rows.append([(line[x] / 255.0, line[x + 1] / 255.0, line[x + 2] / 255.0, line[x + 3] / 255.0)
                     for x in range(0, stride, 4)])
        prev = line
    if w != h:
        raise SystemExit(f"{SOURCE}: card icons are square, got {w}x{h}")
    return rows


def want_class_asset(text, active_guid, inactive_guid):
    text = re.sub(r"^(  IconActive: \{fileID: 21300000, guid: )[0-9a-f]{32}", r"\g<1>" + active_guid,
                  text, count=1, flags=re.M)
    text = re.sub(r"^(  IconInactive: \{fileID: 21300000, guid: )[0-9a-f]{32}", r"\g<1>" + inactive_guid,
                  text, count=1, flags=re.M)
    return text


def main(argv):
    check = "--check" in argv
    pixels = decode_png(open(SOURCE, "rb").read())
    want_png = encode_png(greyed(pixels))
    want_meta = meta_for(INACTIVE_GUID)
    with open(CLASS_ASSET, encoding="utf-8") as fh:
        have_asset = fh.read()
    want_asset = want_class_asset(have_asset, guid_of(SOURCE), INACTIVE_GUID)

    stale = []
    for path, want, mode in ((INACTIVE, want_png, "wb"), (INACTIVE + ".meta", want_meta, "w"),
                             (CLASS_ASSET, want_asset, "w")):
        have = None
        if os.path.exists(path):
            have = open(path, "rb").read() if mode == "wb" else open(path, encoding="utf-8").read()
        if have != want:
            stale.append(os.path.relpath(path, ROOT))
            if not check:
                if mode == "wb":
                    with open(path, "wb") as fh:
                        fh.write(want)
                else:
                    with open(path, "w", encoding="utf-8", newline="\n") as fh:
                        fh.write(want)
    if check:
        if stale:
            print("STALE: " + ", ".join(stale) + " - re-run Tools/Build/author_urchin_card_icons.py")
            return 1
        print("OK: Urchin card icons resolve and the inactive variant matches the source art.")
        return 0
    print(("wrote " + ", ".join(stale)) if stale else "up to date")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
