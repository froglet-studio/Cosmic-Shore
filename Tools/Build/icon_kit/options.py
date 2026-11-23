"""An OPTIONS sheet: the old icon first, then each candidate, every one on the card as the HUD draws it.

Garrett, 2026-10-25: "show the old when comparing." A candidate is judged against what ships, at the size it ships,
so the first column is always the BEFORE (the sprite on the card today, or the one a new icon replaces), then the
candidates in the order given; each as the card at rest (and in a state tint when the card has one), the raw icon
at 60 and 40, and at 128. Gate results are printed under each label, with the exemptions the spec would carry.

    from options import sheet
    sheet("Squirrel Time card - Skimming", before=[("shipped: the Sparrow's boost icon", <PIL image>), ...],
          options=[("A  ...", <mask or layers>), ...], element="Time", states=[("rest", (255,255,255,255))],
          exempt=("gap",), out="Docs/Icons/sheets/squirrel_skimming_options.png")
"""
import os
import tempfile

from PIL import Image, ImageDraw, ImageFont

import canvas as C
import card
import verify

NAVY = (14, 20, 44, 255)
PLATE = (0, 4, 31, 255)


def _tile(im, size, pad=8):
    t = Image.new("RGBA", (size + 2 * pad, size + 2 * pad), PLATE)
    t.alpha_composite(im.resize((size, size), Image.LANCZOS), (pad, pad))
    return t


def _cell(sheet, d, x, y, label, im, element, states, font, verdict):
    d.text((x, y), label[:78], fill=(230, 230, 230, 255), font=font)
    if verdict is not None:
        d.text((x, y + 12), verdict, fill=(150, 200, 150, 255) if verdict.endswith("ok") else (230, 150, 150, 255), font=font)
    xx, yy = x, y + 26
    for _name, tint in states:
        c = card.card(im, element, tint=tint)
        sheet.alpha_composite(c, (xx, yy))
        xx += c.width + 8
    for s in (60, 40):
        t = _tile(im, s)
        sheet.alpha_composite(t, (xx, yy + 60))
        xx += t.width + 8
    t = _tile(im, 128)
    sheet.alpha_composite(t, (xx, yy + 20))
    return xx + t.width + 16


def sheet(title, before, options, element, out, states=(("rest", (255, 255, 255, 255)),), exempt=(), cols=2):
    """before: [(label, PIL image)], options: [(label, mask-or-layers)]. Writes `out` and returns its path."""
    font = ImageFont.load_default()
    cells = [("BEFORE  " + lbl, im, None) for lbl, im in before]
    for lbl, m in options:
        im = C.render(m)
        p = tempfile.mktemp(suffix=".png")
        im.save(p)
        fails = [f for f in verify.judge(verify.measure(p)) if f not in exempt]
        os.unlink(p)
        verdict = ("gates" + (f" ({', '.join(exempt)} exempt)" if exempt else "") + ": " + ("ok" if not fails else "FAIL " + ",".join(fails)))
        cells.append((lbl, im, verdict))
    cell_w = 505 if len(states) == 1 else 505 + 130 * (len(states) - 1)
    rows = (len(cells) + cols - 1) // cols
    img = Image.new("RGBA", (10 + cols * cell_w, 40 + rows * 250), NAVY)
    d = ImageDraw.Draw(img)
    d.text((10, 8), title + "   (cards drawn offline from AbilityLockupStyle.asset - the Editor is the truth)", fill=(230, 230, 230, 255), font=font)
    for i, (lbl, im, verdict) in enumerate(cells):
        x = 10 + (i % cols) * cell_w
        y = 30 + (i // cols) * 250
        _cell(img, d, x, y, lbl, im, element, states, font, verdict)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out)
    return out
