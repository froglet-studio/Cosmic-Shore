#!/usr/bin/env python3
"""
Render Style Foundation §4 (v0.3.1) at the 1920x1080 PC reference.

Reads the GENERATED/installed TMP .asset files — atlas pixels, glyph rects, face
metrics — and reproduces TMP_SDF.shader's alpha rule, so what it verifies is the
shipped assets rather than the source TTFs.

v0.3 §0-C cancelled Space Grotesk and JetBrains Mono: the scale is Aldrich for
headings and body, Chakra Petch SemiBold for buttons (always caps), and the Data
roles are Aldrich under TMP <mspace> rather than a mono family.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from tmp_font_preview import TMPFontAsset, draw_text, save

ROOT   = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
FONTS  = os.path.join(ROOT, "Assets", "_Graphics", "Fonts")
VENDOR = os.path.join(ROOT, "Assets", "Unity Assests", "TextMesh Pro", "Resources",
                      "Fonts & Materials")
# Aldrich is still vendored; moving it into project space is T6's job, not T5's.
PATHS = {
    "Aldrich":               os.path.join(VENDOR, "ALDRICH-REGULAR SDF.asset"),
    "Chakra Petch SemiBold": os.path.join(FONTS, "ChakraPetch", "ChakraPetch-SemiBold SDF.asset"),
    "Chakra Petch":          os.path.join(FONTS, "ChakraPetch", "ChakraPetch-Regular SDF.asset"),
}
_cache = {}
def font(name):
    if name not in _cache:
        _cache[name] = TMPFontAsset(PATHS[name])
    return _cache[name]

def hexc(h):
    h = h.lstrip('#'); return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))

HULL, PLATE, RULE, RULE_HI = map(hexc, ("0E131C", "171E2A", "2A3444", "3D4A5E"))
SIGNAL, BODY, MUTED, FAINT = map(hexc, ("E8EDF5", "B9C4D2", "7C8899", "4E5A6B"))
SYS, ATTN = hexc("4FD5E8"), hexc("A67CFF")

# Measured from hmtx / unitsPerEm — the X in <mspace=Xem>, per face (v0.3.1, queue #9).
MSPACE = {"Aldrich": 0.730, "Chakra Petch SemiBold": 0.644}

# role, family, mobile@800, pc@1920, caps, mspace, spec-authored(†), sample
SCALE = [
    ("Display",      "Aldrich",               None, 48, False, False, True,  "Victory"),
    ("H1",           "Aldrich",               24,   36, False, False, False, "Screen headers"),
    ("H2",           "Aldrich",               20,   28, False, False, False, "Panel headers, modal titles"),
    ("H3",           "Aldrich",               16,   22, False, False, False, "Card titles, tab labels"),
    ("Body",         "Aldrich",               16,   18, False, False, False, "Descriptions and dialogue set at the body size."),
    ("Body small",   "Aldrich",               None, 15, False, False, True,  "Secondary copy and hints sit one step down."),
    ("Button",       "Chakra Petch SemiBold", 16,   18, True,  False, False, "START MATCH"),
    ("Button small", "Chakra Petch SemiBold", 12,   14, True,  False, False, "CANCEL"),
    ("Data (large)", "Aldrich",               None, 44, False, True,  True,  "1 284 730"),
    ("Data",         "Aldrich",               None, 20, False, True,  True,  "0123456789   42 / 120"),
    ("Data (small)", "Aldrich",               None, 15, False, True,  True,  "0123456789   rank 07"),
]

W, H = 1920, 1080
img = np.zeros((H, W, 3), np.float32); img[:] = HULL
ald, cps = font("Aldrich"), font("Chakra Petch SemiBold")

draw_text(img, ald, "Type scale", 64, 84, 34, 0.0, SIGNAL)
draw_text(img, ald, "Style Foundation sec.4 (v0.3.1) - PC @1920 - rendered from the installed TMP SDF assets",
          64, 114, 15, 0.0, MUTED)
img[136:137, 64:W-64, :] = RULE_HI

COLX, SAMPX = 64, 660
for x, t in ((COLX, "ROLE"), (COLX+150, "FAMILY"), (COLX+360, "@800"),
             (COLX+430, "@1920"), (COLX+510, "MSPACE"), (SAMPX, "SAMPLE")):
    draw_text(img, cps, t, x, 172, 12, 0.10, FAINT)
img[186:187, 64:W-64, :] = RULE

y = 214
for i, (role, fam, m800, pc, caps, ms, authored, sample) in enumerate(SCALE):
    f = font(fam)
    band = max(pc * 1.26, 38)
    if i % 2 == 1:
        img[int(y-band*0.70):int(y+band*0.30), 56:W-56, :] = PLATE
    draw_text(img, cps, role.upper() + (" *" if authored else ""), COLX, y, 13, 0.06, BODY)
    draw_text(img, ald, fam, COLX+150, y, 13, 0.0, MUTED)
    draw_text(img, ald, str(m800) if m800 else "-", COLX+360, y, 13, 0.0,
              MUTED if m800 else FAINT)
    draw_text(img, ald, str(pc), COLX+430, y, 13, 0.0, SIGNAL)
    draw_text(img, ald, f"{MSPACE[fam]:.3f}em" if ms else "-", COLX+510, y, 13, 0.0,
              SYS if ms else FAINT)
    draw_text(img, f, sample, SAMPX, y, pc, 0.0, SIGNAL,
              mspace_em=MSPACE[fam] if ms else 0.0)
    y += band + 11

img[int(y)-18:int(y)-17, 64:W-64, :] = RULE
draw_text(img, cps, "* SPEC-AUTHORED - NOT ON THE SOURCE TYPOGRAPHY PAGE", COLX, y+16, 12, 0.10, ATTN)
draw_text(img, ald,
          "Those five rows carry no guide backing and are open to revision in a way the transcribed six are not.",
          COLX, y+42, 14, 0.0, MUTED)
draw_text(img, ald,
          "Buttons are always caps. Data roles are Aldrich under <mspace>, not a mono family - v0.3 sec.0-C cancelled Space Grotesk and JetBrains Mono.",
          COLX, y+66, 14, 0.0, MUTED)
draw_text(img, cps, "ALDRICH CHARSET GAP - FOR T6", COLX, y+100, 12, 0.10, hexc("FF5C3A"))
_ald_lat1 = sum(1 for c in range(0xA0, 0x100) if c in ald.chars)
draw_text(img, ald,
          f"The installed Aldrich asset covers ASCII 95/95 but Latin-1 Supplement only {_ald_lat1}/96, and none of - x . <- -> arrows.",
          COLX, y+126, 14, 0.0, MUTED)
draw_text(img, ald,
          "Under v0.3 Aldrich is the primary face, so an em-dash or an accent in UI copy falls through to Liberation Sans and",
          COLX, y+148, 14, 0.0, MUTED)
draw_text(img, ald,
          "changes typeface mid-sentence. Nothing vanishes (Liberation covers it) but the face shifts. Regenerating Aldrich at",
          COLX, y+170, 14, 0.0, MUTED)
draw_text(img, ald,
          "90/9/1024 with the project charset closes it - the generator on this branch already does exactly that. T6 owns it.",
          COLX, y+192, 14, 0.0, MUTED)

out = os.path.join(ROOT, "Docs", "Fonts", "type-scale-1920x1080.png")
save(img, out)
print("wrote", os.path.relpath(out, ROOT))
for role, fam, m800, pc, caps, ms, authored, sample in SCALE:
    f = font(fam)
    miss = [c for c in sample if c != ' ' and not f.has(c)]
    print(f"  {role:13s}{'†' if authored else ' '} {fam:22s} @1920 {pc:>2}px "
          f"{'mspace ' + format(MSPACE[fam], '.3f') + 'em' if ms else 'proportional':>18s}"
          f"  missing={miss or '-'}")
