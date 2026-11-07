#!/usr/bin/env python3
"""The SHEET: a new portrait beside the painted set, at every size the game shows a portrait, with the gates.

LOOK at it (the Read tool on the PNG) before anything is written under Assets/. Per candidate, one row:

    BEFORE | AFTER 220x280 | 110x140 | the Hangar's pilot card (the portrait at 44x56, the name, the line) | the three
    nearest painted references at 220x280 (nearest by measurement: width, coverage, value, saturation, accent)

on the Deep Sea theme's plate (Docs/UI/froglet-tokens.json: surface #222645 on ground #00041F), gate verdicts
printed under the AFTER cell, exemptions named. BEFORE is what the callsign shows today at HEAD (author.py reads it
with `git show`, the icon kit's lesson 8), or "no art" when the Hangar row is empty.

    python3 Tools/Build/pilot_art/sheet.py --refs      # the audit sheet: all 32 references with their gate verdicts
                                                       # -> Docs/Pilots/art/sheets/references.png
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import measure  # noqa: E402
import verify  # noqa: E402

ROOT = measure.ROOT
SHEETS = "Docs/Pilots/art/sheets"
GROUND = (0x00, 0x04, 0x1F, 255)
PLATE = (0x22, 0x26, 0x45, 255)
RAISED = (0x43, 0x4C, 0x89, 255)
TEXT = (0xE6, 0xE9, 0xFF, 255)
SOFT = (0x74, 0x7B, 0xAD, 255)
LIME = (0x99, 0xFF, 0x80, 255)
DANGER = (0xFF, 0x4B, 0x3A, 255)
GOLD = (0xFF, 0xAE, 0x00, 255)
FONT_PATH = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FONT_BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
NEAREST_KEYS = ("width", "coverage", "lum_p50", "sat", "accent")


def font(size=13, bold=False):
    try:
        return ImageFont.truetype(FONT_BOLD if bold else FONT_PATH, size)
    except OSError:
        return ImageFont.load_default()


def fit(img, w, h):
    """The image scaled to fit w x h keeping its aspect (the Hangar Image is preserveAspect)."""
    s = min(w / img.width, h / img.height)
    size = (max(1, int(round(img.width * s))), max(1, int(round(img.height * s))))
    return img.resize(size, Image.LANCZOS if s < 1 else Image.NEAREST)


def cell(img, w, h, label="", sub=None, plate=PLATE, pad=8):
    """A plate of w x h with the image centred, a label under it (sub: a list of (text, colour) lines)."""
    f = font(12)
    lines = [(label, TEXT)] + list(sub or [])
    lh = 15
    out = Image.new("RGBA", (w + 2 * pad, h + 2 * pad + lh * len(lines) + 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(out)
    d.rectangle((pad, pad, pad + w - 1, pad + h - 1), fill=plate)
    if img is not None:
        im = fit(img.convert("RGBA"), w, h)
        out.alpha_composite(im, (pad + (w - im.width) // 2, pad + (h - im.height) // 2))
    y = pad + h + 4
    for text, colour in lines:
        if text:
            d.text((pad, y), text, fill=colour, font=f)
        y += lh
    return out


def hangar_card(portrait, name, line, state=""):
    """The Hangar's pilot card as author_menu_hangar.py lays it out: portrait at (10, 10) in 44x56, the name at
    x 66 (H3), the line under it (BodySmall), the state at the right (ControlActive)."""
    w, h = 440, 76
    out = Image.new("RGBA", (w, h), PLATE)
    d = ImageDraw.Draw(out)
    d.rectangle((0, h - 1, w, h), fill=RAISED)
    if portrait is not None:
        im = fit(portrait.convert("RGBA"), 44, 56)
        out.alpha_composite(im, (10 + (44 - im.width) // 2, 10 + (56 - im.height) // 2))
    d.text((66, 12), name, fill=TEXT, font=font(18, bold=True))
    d.text((66, 42), line[:60], fill=SOFT, font=font(12))
    if state:
        d.text((w - 16 - d.textlength(state, font=font(11)), 14), state, fill=(0x00, 0xD4, 0xFF, 255), font=font(11))
    return out


def envelope_portraits():
    path = os.path.join(ROOT, measure.ENVELOPE)
    if not os.path.exists(path):
        return {}, {}
    data = json.load(open(path, encoding="utf-8"))
    return data.get("portraits", {}), data.get("envelope", {})


def nearest(m, k=3):
    """The k references nearest to metrics m, by standardized distance over NEAREST_KEYS."""
    portraits, env = envelope_portraits()
    if not portraits:
        return []
    scored = []
    for name, pm in portraits.items():
        d = 0.0
        for key in NEAREST_KEYS:
            span = max(1e-6, env[key]["max"] - env[key]["min"])
            d += ((m[key] - pm[key]) / span) ** 2
        scored.append((d, name))
    return [n for _, n in sorted(scored)[:k]]


def reference_image(name):
    path = os.path.join(ROOT, measure.REFERENCE_DIR, name + ".png")
    return Image.open(path).convert("RGBA") if os.path.exists(path) else None


def verdict_lines(rows):
    out = []
    fails = [(n, r) for n, p, r in rows if not p]
    if not fails:
        out.append(("gates: all pass", LIME))
    for n, r in fails:
        out.append((f"{n}: exempt ({r})" if r else f"{n}: FAIL", GOLD if r else DANGER))
    return out


def candidate_row(c, width):
    """c: dict(name, sprite, before, person, callsign, line, rows (verify rows), metrics, nearest (names) ...)."""
    f = font(14, bold=True)
    title = f"{c['person']}  ({c['callsign']})  -  {c.get('heritage', '')}  -  {c.get('faction', '')}"
    pieces = [
        cell(c.get("before"), 220, 280, "BEFORE (at HEAD)" if c.get("before") is not None else "BEFORE: no art",
             [("the callsign's portrait today", SOFT)]),
        cell(c["sprite"], 220, 280, f"AFTER  Portrait_{c['name']}.png", verdict_lines(c["rows"])),
        cell(c["sprite"].resize((110, 140), Image.LANCZOS), 110, 140, "110x140"),
        cell(hangar_card(c["sprite"], c["person"], c["line"], c.get("state", "")), 440, 76, "the Hangar's pilot card (44x56)"),
    ]
    for n in c.get("nearest", []):
        ref = reference_image(n)
        pieces.append(cell(ref, 220, 280, n.replace("_Placeholder", ""), [("nearest painted reference", SOFT)]))
    h = max(p.height for p in pieces) + 30
    row = Image.new("RGBA", (width, h), GROUND)
    ImageDraw.Draw(row).text((12, 6), title, fill=TEXT, font=f)
    x = 4
    for p in pieces:
        row.alpha_composite(p, (x, 26))
        x += p.width + 6
    return row


def sheet(candidates, out, title="Pilot portraits"):
    width = 1900
    rows = [candidate_row(c, width) for c in candidates]
    head = 48
    legend = ("BEFORE at HEAD | AFTER 220x280 with the gates (verify.py --rules) | 110x140 | the Hangar card at 44x56 | "
              "the three nearest painted references. Deep Sea plate #222645 on #00041F.")
    img = Image.new("RGBA", (width, head + sum(r.height for r in rows) + 8), GROUND)
    d = ImageDraw.Draw(img)
    d.text((12, 8), title, fill=TEXT, font=font(20, bold=True))
    d.text((12, 32), legend, fill=SOFT, font=font(12))
    y = head
    for r in rows:
        img.alpha_composite(r, (0, y))
        y += r.height
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.convert("RGB").save(out)
    return out


def references_sheet(out=None):
    """Every reference at 220x280 and 44x56 with its gate verdicts: the audit a round starts from."""
    out = out or os.path.join(ROOT, SHEETS, "references.png")
    cols = 8
    cells = []
    for p in measure.references():
        img = Image.open(p).convert("RGBA")
        ok, rows, m = verify.verify(p)
        name = os.path.basename(p).replace("_Placeholder", "").replace(".png", "")
        sub = verdict_lines(rows) + [(f"w {m['width']:.2f} cov {m['coverage']:.2f} lum {m['lum_p50']:.2f} sat {m['sat']:.2f}", SOFT)]
        c = cell(img, 220, 280, name, sub)
        thumb = fit(img, 44, 56)
        c.alpha_composite(thumb, (c.width - 8 - 44, 8))
        cells.append(c)
    cw = max(c.width for c in cells)
    ch = max(c.height for c in cells)
    rows_n = (len(cells) + cols - 1) // cols
    img = Image.new("RGBA", (cols * cw + 8, 40 + rows_n * ch), GROUND)
    d = ImageDraw.Draw(img)
    d.text((12, 8), "The painted set, measured (verify.py): the envelope every new portrait is framed to and gated against. "
                    "Top right of each cell: the Hangar's 44x56.", fill=TEXT, font=font(14, bold=True))
    for i, c in enumerate(cells):
        img.alpha_composite(c, (4 + (i % cols) * cw, 40 + (i // cols) * ch))
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.convert("RGB").save(out)
    return out


def main(argv):
    if "--refs" in argv:
        print("wrote", references_sheet())
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
