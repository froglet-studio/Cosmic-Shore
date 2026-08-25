#!/usr/bin/env python3
"""
Render the Style Foundation §4 type scale at the 1920x1080 reference, from the
GENERATED TMP font assets. Verifies T5's "type-scale test scene screenshot"
criterion outside the editor: every row at its specified family, weight, size
and tracking, on the §2 surface/text ramp.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from tmp_font_preview import TMPFontAsset, draw_text, measure, save

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONTS = os.path.join(ROOT, "Assets", "_Graphics", "Fonts")
WNAME = {300: "Light", 400: "Regular", 500: "Medium", 600: "SemiBold", 700: "Bold"}
FOLDER = {"Chakra Petch": "ChakraPetch", "Space Grotesk": "SpaceGrotesk",
          "JetBrains Mono": "JetBrainsMono"}
_cache = {}


def font(family, weight):
    key = (family, weight)
    if key not in _cache:
        stem = FOLDER[family]
        _cache[key] = TMPFontAsset(
            os.path.join(FONTS, stem, f"{stem}-{WNAME[weight]} SDF.asset"))
    return _cache[key]


def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


VOID, HULL, PLATE, RULE, RULE_HI = map(hexc, ("07090F", "0E131C", "171E2A", "2A3444", "3D4A5E"))
SIGNAL, BODY, MUTED, FAINT = map(hexc, ("E8EDF5", "B9C4D2", "7C8899", "4E5A6B"))
SYS = hexc("4FD5E8")

# role, family, weight, size, tracking(em), sample
SCALE = [
    ("Display", "Chakra Petch",  600, 48, +0.01, "Victory"),
    ("H1",      "Chakra Petch",  600, 32,  0.00, "Screen headers"),
    ("H2",      "Chakra Petch",  500, 24,  0.00, "Panel headers, modal titles"),
    ("H3",      "Chakra Petch",  500, 18,  0.00, "Card titles, tab labels"),
    ("Body",    "Space Grotesk", 400, 16,  0.00, "Descriptions and dialogue set at the body size."),
    ("BodySm",  "Space Grotesk", 400, 14,  0.00, "Secondary copy and hints sit one step down."),
    ("Label",   "JetBrains Mono",500, 12, +0.10, "FIELD LABEL / EYEBROW / STATUS"),
    ("DataLg",  "JetBrains Mono",700, 44, -0.01, "1 284 730"),
    ("Data",    "JetBrains Mono",500, 20,  0.00, "0123456789  ·  42 / 120"),
    ("DataSm",  "JetBrains Mono",400, 13, +0.04, "0123456789  rank 07"),
]
SYMBOLS = "× · — – ‑ … ← → ↑ ↓ ✕ + −"

W, H = 1920, 1080
img = np.zeros((H, W, 3), np.float32); img[:] = HULL

lab   = font("JetBrains Mono", 500)
labr  = font("JetBrains Mono", 400)
head  = font("Chakra Petch", 600)
bodyf = font("Space Grotesk", 400)

# ---- header
draw_text(img, head,  "Type scale", 64, 84, 34, 0.0, SIGNAL)
draw_text(img, bodyf, "Style Foundation §4 · rendered at the 1920×1080 reference "
                      "from the generated TMP SDF assets", 64, 116, 16, 0.0, MUTED)
img[140:141, 64:W-64, :] = RULE_HI

# ---- column headings
COLX, SAMPX = 64, 580
for x, t in ((COLX, "ROLE"), (COLX + 118, "FAMILY"), (COLX + 268, "WT"),
             (COLX + 318, "SIZE"), (COLX + 382, "TRACK"), (SAMPX, "SAMPLE")):
    draw_text(img, lab, t, x, 176, 12, 0.10, FAINT)
img[190:191, 64:W-64, :] = RULE

y = 240
for i, (role, fam, wt, size, trk, sample) in enumerate(SCALE):
    f = font(fam, wt)
    band_h = max(size * 1.45, 42)
    if i % 2 == 1:                                   # zebra on `plate`
        img[int(y - band_h * 0.72):int(y + band_h * 0.28), 56:W-56, :] = PLATE
    draw_text(img, lab,  role.upper(),        COLX,        y, 13, 0.06, BODY)
    draw_text(img, labr, fam,                 COLX + 118,  y, 13, 0.02, MUTED)
    draw_text(img, labr, str(wt),             COLX + 268,  y, 13, 0.02, MUTED)
    draw_text(img, labr, str(size),           COLX + 318,  y, 13, 0.02, MUTED)
    draw_text(img, labr, f"{trk:+.2f}em" if trk else "0", COLX + 382, y, 13, 0.02,
              SYS if trk else FAINT)
    draw_text(img, f,    sample,              SAMPX,       y, size, trk, SIGNAL)
    y += band_h + 12

# ---- symbol coverage
img[int(y)-24:int(y)-23, 64:W-64, :] = RULE
draw_text(img, lab, "REQUIRED SYMBOL SET", COLX, y + 14, 12, 0.10, FAINT)
yy = y + 52
for fam, wt in (("Chakra Petch", 400), ("Space Grotesk", 400), ("JetBrains Mono", 400)):
    f = font(fam, wt)
    draw_text(img, labr, fam, COLX, yy, 13, 0.02, MUTED)
    pen, absent = SAMPX, []
    for ch in SYMBOLS:
        if ch == ' ':
            pen += 11; continue
        slot = max(f.advance(ch, 22), f.advance(' ', 22), 14)
        if f.has(ch):
            draw_text(img, f, ch, pen, yy, 22, 0.0, SIGNAL)
        else:                                        # box the slot the font cannot fill
            absent.append(f"U+{ord(ch):04X}")
            x0, x1 = int(pen) - 2, int(pen + slot) + 2
            y0, y1 = int(yy) - 20, int(yy) + 5
            for a, b in ((y0, y0 + 1), (y1, y1 + 1)):
                img[a:b, x0:x1, :] = hexc("FF5C3A")
            for a, b in ((x0, x0 + 1), (x1, x1 + 1)):
                img[y0:y1, a:b, :] = hexc("FF5C3A")
        pen += slot + 14
    if absent:
        draw_text(img, labr, "absent: " + " ".join(absent), pen + 24, yy, 13, 0.02,
                  hexc("FF5C3A"))
    yy += 32

draw_text(img, bodyf,
          "Boxed slots are glyphs the family does not contain; they resolve through the fallback chain "
          "(Space Grotesk → Chakra Petch → Liberation Sans → dynamic overflow).",
          COLX, H - 34, 14, 0.0, MUTED)
out = os.path.join(ROOT, "Docs", "Fonts", "type-scale-1920x1080.png")
save(img, out)
print("wrote", os.path.relpath(out, ROOT))
for role, fam, wt, size, trk, sample in SCALE:
    f = font(fam, wt)
    miss = [c for c in sample if c != ' ' and not f.has(c)]
    print(f"  {role:8s} {fam:15s} {wt} {size:>2}px trk{trk:+.2f}  "
          f"advance={measure(f, sample, size, trk):7.1f}px  missing={miss or '-'}")
