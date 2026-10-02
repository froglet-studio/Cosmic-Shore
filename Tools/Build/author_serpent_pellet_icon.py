#!/usr/bin/env python3
"""Derive the Serpent's fuel-pellet ability icon from its original HUD art.

The Serpent's Time ability (solid fuel pellets) shipped with a four-pellet readout in
`Assets/_Graphics/Design Assests/HUD UI/Serpent Fuel/`: `Base.png` - four dark pellet SLOTS drawn
over a translucent decagon plate - and `Line_1..4.png`, one lit pellet each, laid over the slots
as the tank fills. The ability lockup (`Docs/ABILITY_LOCKUP.md`) draws every ability on its own
trapezoid plate and retired the fleet's decagon backdrops, so the slots are wanted WITHOUT the
decagon. This script keys it out:

    Serpent Fuel/PelletSlots.png   (the Time card's icon; Line_1..4 ride on top of it)

The decagon is one flat colour at HALF alpha - (0, 2, 10, 127) - while the pellets are OPAQUE, so
the plate is removed by remapping alpha 127..255 onto 0..255. That takes the plate to exactly 0,
leaves every pellet at exactly 255, and scales the antialiased pellet edges (which blend pellet
over plate) proportionally. The canvas is kept at the source's 274 x 274 so the Line overlays
authored against Base.png's frame still land on their slots.

    python3 Tools/Build/author_serpent_pellet_icon.py           # (re)derive
    python3 Tools/Build/author_serpent_pellet_icon.py --check   # fail if it drifted

Deterministic: pure-Python PNG decode/encode, zlib at a fixed level. Never hand-edit the output.
"""
import os
import re
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(ROOT, "Assets", "_Graphics", "Design Assests", "HUD UI", "Serpent Fuel")
SOURCE = os.path.join(ART, "Base.png")
OUTPUT = os.path.join(ART, "PelletSlots.png")
OUTPUT_GUID = "7c3e91a5d24b4f6e9a0b8d15c6e2f4a8"
PLATE_ALPHA = 127
PLATE_SHADE_MAX = 20          # the plate's own colour is (0, 2, 10); its shading stays this dark
PELLET_OUTLINE_ALPHA = 200    # a pellet's black outline is opaque; plate shading never is
PELLET_RADIUS = 124           # px from centre: every pellet inside, the rim stroke outside

assert os.path.isdir(os.path.join(ROOT, "Assets")), f"ROOT resolved to {ROOT}, which has no Assets/"


def decode_png(data):
    """RGBA8, non-interlaced - the only shape the source art uses. Anything else fails loudly."""
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "not a PNG"
    pos, idat, width = 8, b"", None
    while pos < len(data):
        length, ctype = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if ctype == b"IHDR":
            width, height, depth, colour, _, _, interlace = struct.unpack(">IIBBBBB", body)
            assert (depth, colour, interlace) == (8, 6, 0), f"unsupported PNG layout {depth}/{colour}/{interlace}"
        elif ctype == b"IDAT":
            idat += body
        pos += 12 + length
    raw = zlib.decompress(idat)
    stride = width * 4
    rows, prev = [], bytearray(stride)
    for y in range(height):
        f = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for i in range(stride):
            a = line[i - 4] if i >= 4 else 0
            b = prev[i]
            c = prev[i - 4] if i >= 4 else 0
            if f == 1: line[i] = (line[i] + a) & 255
            elif f == 2: line[i] = (line[i] + b) & 255
            elif f == 3: line[i] = (line[i] + ((a + b) >> 1)) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    return width, height, rows


def encode_png(width, height, rows):
    raw = b"".join(b"\x00" + bytes(r) for r in rows)

    def chunk(tag, body):
        return struct.pack(">I", len(body)) + tag + body + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF)

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))


def key_out_plate(rows):
    """Remove the plate, keep the pellets.

    Three things belong to the plate and all three are removed: its flat half-alpha fill, the
    faint dark shading painted into it (near-black pixels just above half alpha), and the pale
    STROKE round its rim, which sits outside every pellet. Measured off the source: the pellets
    reach at most ~112 px from the centre and the rim sits at ~135, so a 124 px radius separates
    them with room on both sides. What is left is remapped 127..255 -> 0..255 so the pellets'
    antialiased edges fade to nothing instead of to a dark halo.
    """
    span = 255 - PLATE_ALPHA
    size = len(rows)
    centre = (size - 1) / 2.0
    out = []
    for y, line in enumerate(rows):
        o = bytearray(line)
        for i in range(3, len(o), 4):
            x = i // 4
            r, g, b, a = o[i - 3], o[i - 2], o[i - 1], o[i]
            outside = (x - centre) ** 2 + (y - centre) ** 2 > PELLET_RADIUS ** 2
            plate_shade = max(r, g, b) <= PLATE_SHADE_MAX and a < PELLET_OUTLINE_ALPHA
            if outside or plate_shade or a <= PLATE_ALPHA:
                o[i - 3] = o[i - 2] = o[i - 1] = o[i] = 0
            else:
                o[i] = min(255, round((a - PLATE_ALPHA) * 255 / span))
        out.append(o)
    return out


def meta_for(guid):
    """The source's own import settings, re-keyed - same sprite mode, PPU and filtering."""
    with open(SOURCE + ".meta", encoding="utf-8") as fh:
        text = fh.read()
    text = re.sub(r"^guid: [0-9a-f]{32}$", f"guid: {guid}", text, count=1, flags=re.M)
    return re.sub(r"spriteID: [0-9a-f]{32}", "spriteID: " + guid[::-1], text)


def main(argv):
    check = "--check" in argv
    w, h, rows = decode_png(open(SOURCE, "rb").read())
    keyed = key_out_plate(rows)

    # The claim the file makes: no plate pixel survives, and no pellet pixel was touched.
    for src, dst in zip(rows, keyed):
        for i in range(3, len(src), 4):
            assert not (src[i] == PLATE_ALPHA and dst[i] != 0), "plate pixel survived"
            assert not (src[i] == 255 and dst[i] != 255), "opaque pellet pixel lost alpha"

    png = encode_png(w, h, keyed)
    meta = meta_for(OUTPUT_GUID)
    have = open(OUTPUT, "rb").read() if os.path.exists(OUTPUT) else None
    have_meta = open(OUTPUT + ".meta", encoding="utf-8").read() if os.path.exists(OUTPUT + ".meta") else None
    stale = have != png or have_meta != meta

    if check:
        if stale:
            print("STALE: PelletSlots.png - re-run Tools/Build/author_serpent_pellet_icon.py")
            return 1
        print("OK: PelletSlots.png matches Base.png with the decagon keyed out.")
        return 0

    if stale:
        with open(OUTPUT, "wb") as fh: fh.write(png)
        with open(OUTPUT + ".meta", "w", encoding="utf-8", newline="\n") as fh: fh.write(meta)
        print("wrote PelletSlots.png")
    else:
        print("up to date")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
