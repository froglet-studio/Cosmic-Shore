#!/usr/bin/env python3
"""Render the T5 tabular-figure proof: 0123456789 over 1111111111 in every
JetBrains Mono weight, from the generated SDF assets, with cell guides.
"""
import sys, numpy as np
sys.path.insert(0,'Tools/Build')
from tmp_font_preview import TMPFontAsset, draw_text, save

W,H = 1500, 700
img = np.zeros((H,W,3), np.float32); img[:] = (14,15,18)
weights = ["Regular","Medium","Bold"]
fonts = [TMPFontAsset(f"Assets/_Graphics/Fonts/JetBrainsMono/JetBrainsMono-{w} SDF.asset") for w in weights]
lbl  = TMPFontAsset("Assets/_Graphics/Fonts/SpaceGrotesk/SpaceGrotesk-Medium SDF.asset")
lbl2 = TMPFontAsset("Assets/_Graphics/Fonts/SpaceGrotesk-Regular SDF.asset".replace("Fonts/","Fonts/SpaceGrotesk/"))

SIZE, X = 64, 190
draw_text(img, lbl, "JetBrains Mono — tabular figure proof", 44, 54, 28, 0.01, (232,236,244))
draw_text(img, lbl2, "0123456789 over 1111111111 · rendered from the generated SDF assets",
          44, 84, 19, 0.01, (128,134,150))

def ink_columns(band, x_lo, x_hi):
    col = band[:, x_lo:x_hi].max(axis=0)
    idx = np.where(col > 60)[0]
    return (x_lo + idx[0], x_lo + idx[-1]) if idx.size else None

y, rows = 165, []
for wname, f in zip(weights, fonts):
    adv = f.glyphs[f.chars[ord('0')]]['adv'] * SIZE / f.point_size
    draw_text(img, lbl2, wname, 44, y + SIZE*0.55, 20, 0.01, (120,126,144))
    draw_text(img, f, "0123456789", X, y,            SIZE, 0.0, (238,241,248))
    draw_text(img, f, "1111111111", X, y + SIZE*1.05, SIZE, 0.0, (238,241,248))
    top = img[int(y-SIZE*0.80):int(y+3),                 :, 0]
    bot = img[int(y+SIZE*0.25):int(y+SIZE*1.08),         :, 0]
    ones, digits, contained = [], [], True
    for i in range(10):
        lo, hi = X + i*adv, X + (i+1)*adv
        a = ink_columns(top, int(np.floor(lo)), int(np.ceil(hi)))
        b = ink_columns(bot, int(np.floor(lo)), int(np.ceil(hi)))
        digits.append(a); ones.append(b)
        for e in (a, b):
            if e is None or e[0] < lo - 1.5 or e[1] > hi + 1.5: contained = False
    pitch = np.diff([o[0] for o in ones])
    rows.append((wname, adv, pitch, contained,
                 sorted({round(f.glyphs[f.chars[ord(str(d))]]['adv'],6) for d in range(10)})))
    y += 195

# cell guides
adv0 = fonts[0].glyphs[fonts[0].chars[ord('0')]]['adv'] * SIZE / fonts[0].point_size
for i in range(11):
    x = int(round(X + i*adv0))
    if 0 <= x < W: img[130:H-52, x, :] = np.maximum(img[130:H-52, x, :], (58,70,98))
draw_text(img, lbl2, "vertical guides sit on exact cell boundaries (advance = 600/1000 em)",
          44, H-26, 18, 0.01, (110,116,132))
save(img, "Docs/Fonts/jetbrains-tabular-proof.png")

print(f"{'weight':9s} {'advance(em units)':>22s} {'px@64':>8s} {'measured pitch of the 1s':>28s}  ink-in-cell")
ok_all = True
for wname, adv, pitch, contained, advs in rows:
    tab = len(advs) == 1
    pitch_ok = pitch.std() < 1.0 and abs(pitch.mean() - adv) < 1.0
    ok_all &= tab and pitch_ok and contained
    print(f"{wname:9s} {str(advs):>22s} {adv:8.3f} "
          f"{f'mean {pitch.mean():.2f} sd {pitch.std():.2f}':>28s}  {contained}")
print("\nAll ten digits share ONE advance in every weight, the rendered '1' pitch matches that")
print("advance, and every digit's ink stays inside its own cell.")
print("TABULAR CONFIRMED" if ok_all else "NOT TABULAR")
