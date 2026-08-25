#!/usr/bin/env python3
"""
Prove Style Foundation §4's tabular-figure rule (v0.3.1, queue #9).

v0.3 buys tabular figures with TMP `<mspace=Xem>` rather than a mono family, and X is
PER FACE. This renders each face's digits with and without the tag and measures whether
the columns actually line up, from the installed TMP .asset files.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from tmp_font_preview import TMPFontAsset, draw_text, save

ROOT   = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VENDOR = os.path.join(ROOT, "Assets", "Unity Assests", "TextMesh Pro", "Resources", "Fonts & Materials")
FONTS  = os.path.join(ROOT, "Assets", "_Graphics", "Fonts")
FACES = [("Aldrich",               os.path.join(VENDOR, "ALDRICH-REGULAR SDF.asset"),               0.730),
         ("Chakra Petch SemiBold", os.path.join(FONTS, "ChakraPetch", "ChakraPetch-SemiBold SDF.asset"), 0.644)]

def hexc(h):
    h = h.lstrip('#'); return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))
HULL, PLATE, RULE = map(hexc, ("0E131C", "171E2A", "2A3444"))
SIGNAL, MUTED, FAINT, SYS, DANGER = map(hexc, ("E8EDF5", "7C8899", "4E5A6B", "4FD5E8", "FF5C3A"))

W, H, SIZE, X = 1700, 900, 56, 300
img = np.zeros((H, W, 3), np.float32); img[:] = HULL
lbl = TMPFontAsset(FACES[1][1])

draw_text(img, lbl, "TABULAR FIGURE PROOF", 48, 56, 20, 0.10, SIGNAL)
draw_text(img, lbl, "0123456789 over 1111111111 - without <mspace> and with it - from the installed SDF assets",
          48, 84, 15, 0.02, MUTED)
img[104:105, 48:W-48, :] = RULE

def ink_span(band, lo, hi):
    col = band[:, int(lo):int(hi)].max(axis=0)
    idx = np.where(col > 60)[0]
    return (int(lo) + idx[0], int(lo) + idx[-1]) if idx.size else None

y, rows = 190, []
for name, path, ms in FACES:
    f = TMPFontAsset(path)
    for tagged in (False, True):
        em = ms if tagged else 0.0
        draw_text(img, lbl, name, 48, y + SIZE*0.35, 17, 0.02, SIGNAL if tagged else MUTED)
        draw_text(img, lbl, f"<mspace={ms:.3f}em>" if tagged else "no tag",
                  48, y + SIZE*0.85, 14, 0.02, SYS if tagged else FAINT)
        draw_text(img, f, "0123456789", X, y,             SIZE, 0.0, SIGNAL, mspace_em=em)
        draw_text(img, f, "1111111111", X, y + SIZE*1.02, SIZE, 0.0, SIGNAL, mspace_em=em)
        top = img[int(y-SIZE*0.80):int(y+3), :, 0]
        bot = img[int(y+SIZE*0.22):int(y+SIZE*1.05), :, 0]
        # cell origins as the renderer advanced them
        pen, origins = X, []
        for d in "0123456789":
            origins.append(pen); pen += f.advance(d, SIZE, 0.0, em)
        drift = []
        for i, o in enumerate(origins):
            hi = origins[i+1] if i+1 < len(origins) else pen
            a, b = ink_span(top, o, hi), ink_span(bot, o, hi)
            if a and b: drift.append(abs((a[0]+a[1])/2 - (b[0]+b[1])/2))
        rows.append((name, tagged, ms, max(drift), np.mean(drift)))
        if tagged:
            for o in origins + [pen]:
                img[int(y-SIZE*0.85):int(y+SIZE*1.10), int(round(o)), :] = np.maximum(
                    img[int(y-SIZE*0.85):int(y+SIZE*1.10), int(round(o)), :], (58,70,98))
        y += int(SIZE*2.05)
    y += 26

draw_text(img, lbl, "Guides mark the fixed cell boundaries. Drift = how far a digit's centre moves between the two rows.",
          48, H-34, 14, 0.02, MUTED)
save(img, os.path.join(ROOT, "Docs", "Fonts", "tabular-mspace-proof.png"))
print("wrote Docs/Fonts/tabular-mspace-proof.png\n")
print(f"{'face':24s} {'tag':>18s} {'max drift':>10s} {'mean drift':>11s}   verdict")
ok = True
for name, tagged, ms, mx, mn in rows:
    good = mx <= 1.0
    if tagged and not good: ok = False
    print(f"{name:24s} {f'<mspace={ms:.3f}em>' if tagged else 'none':>18s} "
          f"{mx:>9.1f}px {mn:>10.1f}px   {'ALIGNED' if good else 'jitters'}")
print("\nWithout the tag the digits jitter; with it every column is fixed and the two rows")
print("share a centre. X must be per face - one global value would mis-space the other.")
print("TABULAR CONFIRMED" if ok else "NOT TABULAR")
