"""Draw ONE ability-lockup card the way the HUD draws it, from the style asset's own numbers.

The sheet used to show a sprite on a square plate; Garrett (2026-10-25): "your before pictures are not accurate or
exhaustive". A card is two borderless trapezoids meeting wide-edge to wide-edge across a gap (Docs/ABILITY_LOCKUP.md):
the element flower in the upper cell, the ability icon in the lower one, a slant edge in slantEdgeColor on the
sloped sides, the icon scaled into iconBoxSize, and the whole thing tinted by STATE (a joust flash, a spent ring)
but never by element. Numbers are read from Assets/Resources/AbilityLockupStyle.asset at import so a style change
moves the sheet too; the petal sprites come from Assets/Resources/ElementPetals/<element>_petal.png.

This is an offline approximation for LOOKING - the Editor is the truth - and it says so on the sheet.
"""
import math
import os
import re

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
STYLE = os.path.join(ROOT, "Assets", "Resources", "AbilityLockupStyle.asset")
PETALS = os.path.join(ROOT, "Assets", "Resources", "ElementPetals")
SS = 4   # supersample for the plate edges


def _style():
    text = open(STYLE, encoding="utf-8").read()
    def f(key, default):
        m = re.search(rf"^\s*{key}: ([-\d.]+)\s*$", text, re.M)
        return float(m.group(1)) if m else default
    def c(key, default):
        m = re.search(rf"^\s*{key}: \{{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}}", text, re.M)
        return tuple(int(round(float(v) * 255)) for v in m.groups()) if m else default
    return dict(
        plateWidth=f("plateWidth", 104), inset=f("trapezoidInset", 9), cellGap=f("cellGap", 6),
        abilityCell=f("abilityCellHeight", 88), petalCell=f("petalCellHeight", 88),
        flower=f("petalFlowerSize", 60), iconBox=f("iconBoxSize", 60),
        edgeT=f("slantEdgeThickness", 3), edgeColor=c("slantEdgeColor", (116, 123, 173, 217)),
        plateColor=c("plateColor", (0, 4, 31, 219)), chipHeight=f("chipHeight", 24),
        lockedPlate=c("lockedPlateColor", (0, 4, 31, 140)), lockedMark=c("lockedMarkColor", (92, 95, 112, 140)),
        edgeAA=f("slantEdgeAntialias", 1), gaugeFrac=f("gaugeCellFraction", 1),
        gaugeTrack=c("gaugeTrackColor", (22, 24, 33, 230)), gaugeFill=c("gaugeFillColor", (56, 130, 255, 140)),
    )


S = _style()
HUD_BG = (16, 22, 48, 255)      # the game behind the HUD, roughly


def _trapezoid(d, x0, y0, w, h, inset, wide_at_bottom, fill, edge, edge_t):
    """One cell: wide edge on the side that faces the gap."""
    if wide_at_bottom:
        pts = [(x0 + inset, y0), (x0 + w - inset, y0), (x0 + w, y0 + h), (x0, y0 + h)]
    else:
        pts = [(x0, y0), (x0 + w, y0), (x0 + w - inset, y0 + h), (x0 + inset, y0 + h)]
    d.polygon([(x * SS, y * SS) for x, y in pts], fill=fill)
    # The slant edge rides the two sloped sides only.
    for a, b in ((pts[0], pts[3]), (pts[1], pts[2])):
        d.line([(a[0] * SS, a[1] * SS), (b[0] * SS, b[1] * SS)], fill=edge, width=int(edge_t * SS))


def flower(element, size):
    """The element flower at level 0..: four petals of the element's sprite turned 90 degrees apart (an
    approximation of ElementalBarsView's radial layout - the petal ladder's fill state is not modelled)."""
    path = os.path.join(PETALS, f"{element.lower()}_petal.png")
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    if not os.path.exists(path):
        return canvas
    petal = Image.open(path).convert("RGBA")
    ph = int(size * 0.5)
    petal = petal.resize((max(1, int(petal.width * ph / petal.height)), ph), Image.LANCZOS)
    for i in range(4):
        rot = petal.rotate(-90 * i, expand=True, resample=Image.BICUBIC)
        # Each petal points outward from the centre: place its base at the centre.
        dx = (size - rot.width) // 2 + int(math.cos(math.radians(-90 + 90 * i)) * size * 0.22)
        dy = (size - rot.height) // 2 + int(math.sin(math.radians(-90 + 90 * i)) * size * 0.22)
        canvas.alpha_composite(rot, (dx, dy))
    return canvas


def _gauge(im, x0, y0, w, h, inset, fill01):
    """The fleet's one gauge as the lockup draws it (Docs/ABILITY_LOCKUP.md 'The gauge'): a dim track inset inside the
    ability plate's slant band by PlateEdgeReach, and a linear fill rising from its base, both masked to the trapezoid.
    Drawn on its own layer so the style's alphas blend over the plate."""
    reach = S["edgeT"] + S["edgeAA"]
    gh = h * S["gaugeFrac"]
    top = y0 + h - gh
    # the plate is wide at the TOP; its sides slope in by `inset` over `h`
    def side(y):
        t = (y - y0) / h
        return x0 + inset * t + reach, x0 + w - inset * t - reach
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    l0, r0 = side(top + reach); l1, r1 = side(y0 + h - reach)
    d.polygon([(l0 * SS, (top + reach) * SS), (r0 * SS, (top + reach) * SS), (r1 * SS, (y0 + h - reach) * SS), (l1 * SS, (y0 + h - reach) * SS)], fill=S["gaugeTrack"])
    im.alpha_composite(layer)
    if fill01 > 0:
        fy = (y0 + h - reach) - (gh - 2 * reach) * min(1.0, fill01)
        layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        lf, rf = side(fy)
        d.polygon([(lf * SS, fy * SS), (rf * SS, fy * SS), (r1 * SS, (y0 + h - reach) * SS), (l1 * SS, (y0 + h - reach) * SS)], fill=S["gaugeFill"])
        im.alpha_composite(layer)


def card(icon, element="Charge", tint=(255, 255, 255, 255), locked=False, scale=1.0, chip=None, gauge=None):
    """The card at HUD scale 1 (104 x 182 plus the chip), as an RGBA image; `scale` enlarges for the sheet.
    `icon` is a PIL image (any size: it is fitted into iconBoxSize like the lockup does); `tint` multiplies it
    (the joust flash, a gauge state); `locked` draws the quiet plate with no icon; `gauge` (0..1) draws the card's
    plate gauge at that fill, behind the icon, as a card with an adopted meter has it."""
    W = int(S["plateWidth"])
    H = int(S["petalCell"] + S["cellGap"] + S["abilityCell"])
    pad = 10
    total_h = H + 2 * pad + (int(S["chipHeight"]) + 6 if chip else 0)
    im = Image.new("RGBA", ((W + 2 * pad) * SS, total_h * SS), HUD_BG)
    d = ImageDraw.Draw(im)
    plate = S["lockedPlate"] if locked else S["plateColor"]
    edge = S["lockedMark"] if locked else S["edgeColor"]
    # Upper cell (flower): wide edge at the BOTTOM, facing the gap. Lower cell: wide edge at the TOP.
    _trapezoid(d, pad, pad, W, S["petalCell"], S["inset"], True, plate, edge, S["edgeT"])
    _trapezoid(d, pad, pad + S["petalCell"] + S["cellGap"], W, S["abilityCell"], S["inset"], False, plate, edge, S["edgeT"])
    if gauge is not None and not locked:
        _gauge(im, pad, pad + S["petalCell"] + S["cellGap"], W, S["abilityCell"], S["inset"], float(gauge))
    im = im.resize((W + 2 * pad, total_h), Image.LANCZOS)
    fl = flower(element, int(S["flower"]))
    im.alpha_composite(fl, (pad + (W - fl.width) // 2, pad + (int(S["petalCell"]) - fl.height) // 2))
    if icon is not None and not locked:
        box = int(S["iconBox"])
        ic = icon.convert("RGBA")
        k = box / max(ic.width, ic.height)
        ic = ic.resize((max(1, int(ic.width * k)), max(1, int(ic.height * k))), Image.LANCZOS)
        if tint != (255, 255, 255, 255):
            r, g, b, a = ic.split()
            ic = Image.merge("RGBA", (r.point(lambda v: v * tint[0] // 255), g.point(lambda v: v * tint[1] // 255),
                                      b.point(lambda v: v * tint[2] // 255), a))
        cy = pad + int(S["petalCell"] + S["cellGap"]) + (int(S["abilityCell"]) - ic.height) // 2
        im.alpha_composite(ic, (pad + (W - ic.width) // 2, cy))
    if chip:
        from PIL import ImageFont
        font = ImageFont.load_default()
        d = ImageDraw.Draw(im)
        ch = int(S["chipHeight"])
        y = pad + H + 4
        d.rectangle([pad + W // 2 - 18, y, pad + W // 2 + 18, y + ch - 4], fill=(230, 232, 245, 255))
        d.text((pad + W // 2 - 4 * len(chip), y + 4), chip, fill=(20, 22, 40, 255), font=font)
    if scale != 1.0:
        im = im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)
    return im
