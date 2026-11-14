#!/usr/bin/env python3
"""Rev 112 boards: the Colour sets screen for a catalog that keeps growing.

Shombith, 2026-11-14: "Color sets we do need to change how they look, we will be making multiples of them so
scrolling or clicking through each one is not an option. [...] We need a preview to show what the color set changes to
depict the world of our game, We need a scrollable list with differentiation between free and the other type, lets use
the whole place not leave a big empty space in the middle."

Every set is the shipped catalog (Assets/_SO_Assets/Color Palettes/ColorSetCatalog.asset: name, line, pigments, vision)
and every colour is its shipped SO_ColorSet value (r51_profile_colour_sets.palette). The big world and the four
"what it repaints" tiles are drawn ONCE with colour holes, so the board stays small while all fourteen sets preview.

    A  Stage and list   list left (Free, then Premium), the world and what it repaints right (recommended)
    B  Wall             the world and its repaints across the top, a scrolling wall of tiles below
    C  Two shelves      Free left, Premium right, the world between them
    D  The real cell    the world is the screen, a docked list right, the repaints along the foot

    python3 Docs/MenuRedesignB/rounds/r112_colour_sets_catalog.py <root> 112
"""
import importlib.util
import json
import math
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2])
_argv = sys.argv
sys.argv = [sys.argv[0], sys.argv[1], str(REV)]
_spec = importlib.util.spec_from_file_location("r54", HERE / "r54_colour_sets_modern.py")
r54 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r54)
sys.argv = _argv
r51 = r54.r51

mix, kc, board = r51.mix, r51.kc, r51.board
JADE, LIME, GOLD, TEXT, SOFT = r51.JADE, r51.LIME, r51.GOLD, r51.TEXT, r51.SOFT
TICK, LOCK = r51.ICON_TICK, r51.ICON_LOCK
FREEC, FREEE = "#C9CDEA", "#5A63A0"
# Shombith picked D (2026-11-14) and asked, on D only: "bring the sea lines in the card backgrounds and don't name it
# Free but Color Blind supported (Free)". PICKED is on while D is drawn, so A, B and C stay as they were published.
PICKED = [False]
CB = "Color Blind supported (Free)"


def card_sea(w, h, alpha=.22):
    """The menu's sea lines (r51.sea_lines) inside a card, behind its content."""
    return r51.sea_lines(w=w, h=h, top=0, alpha=alpha).replace('style="position:absolute;left:0;top:0px"', 'style="position:absolute;left:0;top:0;pointer-events:none"')
DOMS = [("jade", "Jade"), ("ruby", "Ruby"), ("gold", "Gold"), ("blue", "Blue")]

# ---------------------------------------------------------------- the catalog, as shipped

ASSET = {"nocturne": "Nocturne", "photic": "Photic", "dartfrog": "DartFrog", "nacre": "Nacre",
         "blueringedoctopus": "BlueRingedOctopus", "neonnoir": "NeonNoir", "ukiyoe": "UkiyoE", "dusk": "Dusk",
         "contrast": "Contrast", "protanopia": "Protanopia", "deuteranopia": "Deuteranopia",
         "tritanopia": "Tritanopia", "achromatopsia": "Achromatopsia", "lowvision": "LowVision"}
# Free: the two sets every pilot starts with, and every set made for a vision (section 126.3: never a reward).
# Premium: the rest. Prices are placeholders for the board; ownership is a sample state (Dart Frog owned).
PRICE = {"dartfrog": 1200, "nacre": 800, "blueringedoctopus": 1200, "neonnoir": 1500, "ukiyoe": 1500, "dusk": 800}
OWNED = {"dartfrog"}


def catalog():
    text = (REPO / "Assets/_SO_Assets/Color Palettes/ColorSetCatalog.asset").read_text()
    out = []
    for block in text.split("\n  - id: ")[1:]:
        sid = block.split("\n", 1)[0].strip()
        name = re.search(r"displayName: (.+)", block).group(1).strip()
        line = re.search(r"line: (.+)", block).group(1).strip()
        if line.startswith("'"):
            line = line[1:-1].replace("''", "'")
        pig = re.findall(r"\n    - (.+)", block)
        vision = int(re.search(r"vision: (\d+)", block).group(1))
        pal = r51.palette(f"{ASSET[sid]}ColorSetSO")
        out.append({"id": sid, "name": name, "line": line, "pig": pig, "vision": vision > 0, "pal": pal,
                    "free": sid not in PRICE, "price": PRICE.get(sid, 0), "owned": sid in OWNED or sid not in PRICE})
    return out


SETS = catalog()
SET = {s["id"]: s for s in SETS}
FREE = [s for s in SETS if s["free"] and not s["vision"]]
VISION = [s for s in SETS if s["vision"]]
PREM = [s for s in SETS if not s["free"]]
START = "dartfrog"


def rgba(hexc, a):
    return f"rgba({int(hexc[1:3], 16)},{int(hexc[3:5], 16)},{int(hexc[5:7], 16)},{a})"


def colours(s):
    """Every colour the scene and the tiles read, per set: tiers t0-t2 (prism, shielded, super-shielded) of each domain
    as face, rim, lit, half, dark facets and a glow, plus the vessel, the trail, the sea wash, the crystal and the menu."""
    pal, c = s["pal"], {}
    for d, _ in DOMS:
        p, dd = pal[d], {}
        for t, k in enumerate(("", "s", "x")):
            f, r = p[k + "f"], p[k + "r"]
            dd[f"t{t}"] = {"f": f, "r": r, "lit": mix(f, r, .5), "half": mix(f, r, .2), "dark": mix(f, "#000000", .35),
                           "g": rgba(r, .6)}
        dd.update({"v": p["v"], "t": p["t"], "tg": rgba(p["t"], .28), "w": rgba(p["r"], .16)})
        c[d] = dd
    c["cta"] = pal["cta"]
    c["ctag"] = rgba(pal["cta"], .7)
    # The menu as ColorSetUITheme paints it (section 126.3), approximated: panels from the Jade face, selection the
    # Jade rim, purchase the Gold rim, the call to action the set's own.
    c["ui"] = {"panel": mix(pal["jade"]["f"], "#00041F", .78), "line": mix(pal["jade"]["f"], "#434C89", .5),
               "sel": pal["jade"]["r"], "buy": pal["gold"]["r"], "cta": pal["cta"]}
    return c


COL = {s["id"]: colours(s) for s in SETS}


def lookup(c, path):
    for k in path.split("."):
        c = c[k]
    return c


def col_static(sid):
    return lambda path: lookup(COL[sid], path)


def col_hole(path):
    return "{{p." + path + "}}"


# ---------------------------------------------------------------- drawing: prisms, vessels, crystals

def prism_g(x, y, w, rot, C, d, t, glow=True, alpha=1.0, blur=0.0):
    """A long bipyramid lit from the upper left, as r54 draws it, in SVG units at (x, y) centre."""
    s = w / 40
    k = f"{d}.t{t}"
    flt = f"filter:drop-shadow(0 0 {6 * s:.1f}px {C(k + '.g')})" if glow else ""
    if blur > .2:
        flt += f" blur({blur:.1f}px)" if flt else f"filter:blur({blur:.1f}px)"
    return (f'<g transform="translate({x:.0f} {y:.0f}) rotate({rot:.0f}) scale({s:.3f}) translate(-20 -46)" '
            f'opacity="{alpha:.2f}" style="{flt}">'
            f'<polygon points="20,0 2,38 20,44" fill="{C(k + ".lit")}"></polygon><polygon points="20,0 38,38 20,44" fill="{C(k + ".half")}"></polygon>'
            f'<polygon points="2,38 20,44 20,92" fill="{C(k + ".f")}"></polygon><polygon points="38,38 20,44 20,92" fill="{C(k + ".dark")}"></polygon>'
            f'<polygon points="20,0 38,38 20,92 2,38" fill="none" stroke="{C(k + ".r")}" stroke-width="1.4" stroke-linejoin="round"></polygon></g>')


def vessel_g(x, y, sc, rot, C, d):
    return (f'<g transform="translate({x:.0f} {y:.0f}) rotate({rot:.0f}) scale({sc:.2f})" style="filter:drop-shadow(0 0 6px {C(d + ".v")})">'
            f'<polygon points="22,0 -16,-11 -9,0 -16,11" fill="{C(d + ".v")}"></polygon>'
            f'<polygon points="22,0 -9,0 -16,11" fill="#000" opacity=".25"></polygon></g>')


def crystal_g(x, y, sc, C):
    return (f'<g transform="translate({x:.0f} {y:.0f}) scale({sc:.2f})" style="filter:drop-shadow(0 0 10px {C("ctag")})">'
            f'<polygon points="-16,0 -8,-14 8,-14 16,0 8,14 -8,14" fill="{C("cta")}" opacity=".92"></polygon>'
            '<polygon points="-16,0 -8,-14 8,-14 16,0" fill="#fff" opacity=".28"></polygon></g>')


# The in-game vessel icons (each SO_Class_<Hull>.IconActive, Assets/_Graphics/CardImages/), uploaded to the canvas.
# Drawn nose to the upper right; NOSE is that heading in degrees. One hull per domain for the picture.
ICON = {"squirrel": "/_blob/276a4fd2d8162fe352348a4f0cd3bf95", "sparrow": "/_blob/ee550d11c9903c5ab823d1516a3c5252",
        "dolphin": "/_blob/1d56c689ae5fbc5419ce6e2a385fcc11", "manta": "/_blob/72b56ad6a6e25b7c4cb2cc3712fcb200"}
HULL = {"jade": "squirrel", "ruby": "sparrow", "gold": "dolphin", "blue": "manta"}
NOSE = -40
_uid = [0]


def tint_defs(C, u):
    """One filter per domain: the icon multiplied by the set's vessel colour (its dark hull stays dark, its highlights
    take the colour), over a soft glow of the same colour."""
    out = []
    for d, _ in DOMS:
        out.append(f'<filter id="tn-{d}-{u}" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">'
                   f'<feFlood flood-color="{C(d + ".v")}"></feFlood><feComposite in2="SourceAlpha" operator="in" result="c"></feComposite>'
                   '<feBlend in="SourceGraphic" in2="c" mode="multiply" result="m"></feBlend>'
                   '<feGaussianBlur in="c" stdDeviation="4" result="g"></feGaussianBlur>'
                   '<feComponentTransfer in="g" result="g2"><feFuncA type="linear" slope=".55"></feFuncA></feComponentTransfer>'
                   '<feMerge><feMergeNode in="g2"></feMergeNode><feMergeNode in="m"></feMergeNode></feMerge></filter>')
    return "<defs>" + "".join(out) + "</defs>"


def _bez(p, t):
    (x0, y0), (x1, y1), (x2, y2), (x3, y3) = p
    m = 1 - t
    x = m ** 3 * x0 + 3 * m * m * t * x1 + 3 * m * t * t * x2 + t ** 3 * x3
    y = m ** 3 * y0 + 3 * m * m * t * y1 + 3 * m * t * t * y2 + t ** 3 * y3
    dx = 3 * m * m * (x1 - x0) + 6 * m * t * (x2 - x1) + 3 * t * t * (x3 - x2)
    dy = 3 * m * m * (y1 - y0) + 6 * m * t * (y2 - y1) + 3 * t * t * (y3 - y2)
    return x, y, math.degrees(math.atan2(dy, dx))


def trail_g(pts, C, d, u, icon, pw, gap, alpha=1.0, glow=True):
    """A vessel's trail as the game lays it: a line of prisms standing across the flight path (rim over face in the
    set's colours, oldest faintest), and the hull's own icon at its head, nose along the path."""
    samples = [_bez(pts, i / 400) for i in range(401)]
    out, run, last = [], 0.0, samples[0]
    marks = []
    for sx, sy, a in samples[1:]:
        run += math.hypot(sx - last[0], sy - last[1])
        last = (sx, sy, a)
        if run >= gap:
            marks.append((sx, sy, a))
            run = 0.0
    marks = marks[:-2]
    n = max(1, len(marks))
    for k, (x, y, a) in enumerate(marks):
        out.append(prism_g(x, y, pw, a, C, d, 0, glow=glow, alpha=alpha * (.35 + .65 * k / n)))
    hx, hy, ha = samples[-1]
    sz = icon
    out.append(f'<g transform="translate({hx:.0f} {hy:.0f}) rotate({ha - NOSE:.0f})" opacity="{alpha:.2f}">'
               f'<image href="{ICON[HULL[d]]}" x="{-sz / 2:.0f}" y="{-sz / 2:.0f}" width="{sz:.0f}" height="{sz:.0f}" filter="url(#tn-{d}-{u})"></image></g>')
    return "".join(out)


CENTRES = {"jade": (.2, .44), "ruby": (.8, .36), "gold": (.52, .26), "blue": (.44, .72)}


def world(w, h, C, n=40, seed=11, dof=True, trail=True, crystal=True, stars=True):
    """The set's world: a deep sea washed by its rims, prisms of the four domains in depth, a vessel's trail and a
    free crystal. Seeded, so every set shows the same world and only the colours change."""
    r = r54._rand(seed)
    sc = min(w, h) / 864
    out = [f'<svg width="{w}" height="{h}" viewBox="0 0 {w} {h}" aria-hidden="true" style="display:block">'
           '<defs><radialGradient id="sea" cx="58%" cy="42%" r="75%"><stop offset="0" stop-color="#0A1450"></stop>'
           '<stop offset=".45" stop-color="#040A30"></stop><stop offset="1" stop-color="#00010A"></stop></radialGradient></defs>'
           f'<rect width="{w}" height="{h}" fill="url(#sea)"></rect>']
    for d, (cx, cy) in CENTRES.items():
        out.append(f'<ellipse cx="{cx * w:.0f}" cy="{cy * h:.0f}" rx="{w * .32:.0f}" ry="{h * .34:.0f}" fill="{C(d + ".w")}" style="filter:blur({40 * sc:.0f}px)"></ellipse>')
    if stars:
        out.append("".join(f'<circle cx="{r() * w:.0f}" cy="{r() * h:.0f}" r="{.5 + r() * 1.1:.1f}" fill="#E6E9FF" opacity="{.15 + r() * .4:.2f}"></circle>'
                           for _ in range(int(60 * w * h / (1840 * 864)) + 8)))
    items = []
    for i in range(n):
        d = DOMS[i % 4][0]
        cx, cy = CENTRES[d]
        z = r()
        x = (cx + (r() - .5) * .44) * w
        y = (cy + (r() - .5) * .4) * h
        t = 0 if r() < .62 else (1 if r() < .7 else 2)
        size = sc * (12 + z * z * 44)
        items.append((z, x, y, size, d, t, (r() - .5) * 70))
    items.sort()
    for z, x, y, size, d, t, rot in items:
        out.append(prism_g(x, y, size, rot, C, d, t, glow=z > .35, alpha=.45 + z * .55, blur=(1 - z) * 2.4 if dof else 0))
    if trail:
        _uid[0] += 1
        u = _uid[0]
        out.append(tint_defs(C, u))
        lanes = [("ruby", ((w * .02, h * .2), (w * .14, h * .16), (w * .22, h * .1), (w * .32, h * .12)), .62),
                 ("gold", ((w * 1.02, h * .62), (w * .94, h * .58), (w * .9, h * .48), (w * .84, h * .44)), .72),
                 ("jade", ((-20, h * .86), (w * .25, h * .8), (w * .42, h * .52), (w * .66, h * .32)), 1.0)]
        for d, pts, a in lanes:
            out.append(trail_g(pts, C, d, u, 120 * sc * a, 15 * sc * a, 26 * sc * a, alpha=a))
    if crystal:
        out.append(crystal_g(w * .86, h * .17, sc * 1.4, C))
    out.append("</svg>")
    return "".join(out)


# ---------------------------------------------------------------- what the set repaints: four tiles, one per surface

def tile_svg(kind, C):
    """A 300 x 170 picture of one surface the set repaints, drawn with the colour lookups in C."""
    o = ['<svg viewBox="0 0 300 170" width="100%" height="100%" preserveAspectRatio="xMidYMid meet" aria-hidden="true" style="display:block">']
    if kind == "prisms":
        for i, (d, _) in enumerate(DOMS):
            for t in range(3):
                o.append(prism_g(40 + i * 72 + t * 14, 33 + t * 52, 21, -12, C, d, t, glow=True))
    elif kind == "vessels":
        _uid[0] += 1
        u = _uid[0]
        o.append(tint_defs(C, u))
        for i, (d, _) in enumerate(DOMS):
            cx, cy = 75 + (i % 2) * 150, 44 + (i // 2) * 84
            o.append(trail_g(((cx - 74, cy + 34), (cx - 40, cy + 30), (cx - 6, cy + 4), (cx + 34, cy - 14)), C, d, u, 78, 13, 15, glow=True))
    elif kind == "crystals":
        o.append(crystal_g(150, 80, 2.6, C) + crystal_g(64, 128, 1.1, C) + crystal_g(240, 40, 1.2, C) + crystal_g(250, 134, .8, C))
    elif kind == "menus":
        o.append(f'<rect x="8" y="10" width="284" height="150" fill="{C("ui.panel")}" stroke="{C("ui.line")}" stroke-width="1.2"></rect>'
                 f'<rect x="26" y="28" width="118" height="40" fill="none" stroke="{C("ui.sel")}" stroke-width="2"></rect>'
                 f'<rect x="34" y="40" width="70" height="6" fill="#E6E9FF" opacity=".75"></rect><rect x="34" y="52" width="48" height="5" fill="#E6E9FF" opacity=".35"></rect>'
                 f'<rect x="156" y="28" width="118" height="40" fill="none" stroke="{C("ui.line")}" stroke-width="1.2"></rect>'
                 f'<rect x="164" y="40" width="64" height="6" fill="#E6E9FF" opacity=".5"></rect><rect x="164" y="52" width="40" height="5" fill="#E6E9FF" opacity=".25"></rect>'
                 f'<polygon points="34,98 150,98 150,132 142,140 26,140 26,106" fill="{C("ui.cta")}"></polygon>'
                 f'<rect x="58" y="116" width="62" height="6" fill="#00010A"></rect>'
                 f'<polygon points="168,98 274,98 274,132 266,140 160,140 160,106" fill="{C("ui.buy")}"></polygon>'
                 f'<polygon points="182,119 188,111 196,111 202,119 196,127 188,127" fill="#00010A"></polygon><rect x="210" y="116" width="44" height="6" fill="#00010A"></rect>')
    o.append("</svg>")
    return "".join(o)


TILES = [("prisms", "Prisms", "four domains, three shields"), ("vessels", "Vessels and trails", "every team"),
         ("crystals", "Crystals", "what you collect"), ("menus", "Menus", "panels, picks, buttons")]


def tile(kind, label, sub, w, h, C=col_hole):
    return (f'<div class="sls" style="position:relative;width:{w}px;height:{h}px;background:rgba(6,10,38,.94);box-shadow:inset 0 0 0 1px #2B3266;'
            'display:flex;flex-direction:column;box-sizing:border-box;padding:14px 16px 10px">'
            + (card_sea(w, h) if PICKED[0] else "")
            + f'<div style="{"position:relative;" if PICKED[0] else ""}display:flex;justify-content:space-between;align-items:baseline;gap:10px"><span class="tagl" style="color:#C9CDEA;white-space:nowrap">{label}</span>'
            + (f'<span style="font-size:12px;color:{SOFT};white-space:nowrap">{sub}</span>' if w >= 280 else "") + '</div>'
            f'<div style="{"position:relative;" if PICKED[0] else ""}flex:1;min-height:0;margin-top:8px">{tile_svg(kind, C)}</div></div>')


def tiles_row(w, h, gap=16, cols=4):
    tw = (w - gap * (cols - 1)) // cols
    rows = (len(TILES) + cols - 1) // cols
    th = (h - gap * (rows - 1)) // rows
    return (f'<div style="display:grid;grid-template-columns:repeat({cols}, {tw}px);gap:{gap}px">'
            + "".join(tile(k, l, s, tw, th) for k, l, s in TILES) + "</div>")


# ---------------------------------------------------------------- the list: Free and Premium

CRYSTAL = ('<svg width="14" height="12" viewBox="0 0 14 12" aria-hidden="true"><polygon points="0,6 3.5,0 10.5,0 14,6 10.5,12 3.5,12" '
           'fill="currentColor"></polygon></svg>')
EYE = ('<svg width="16" height="12" viewBox="0 0 16 12" fill="none" stroke="currentColor" stroke-width="1.4" aria-hidden="true">'
       '<path d="M1 6c2-3.4 4.5-5 7-5s5 1.6 7 5c-2 3.4-4.5 5-7 5S3 9.4 1 6z"></path><circle cx="8" cy="6" r="2"></circle></svg>')


def swatches(s, size=12):
    return ('<span style="display:flex;gap:4px">' + "".join(
        f'<span style="display:flex"><span style="width:{size}px;height:{size}px;background:{s["pal"][d]["r"]}"></span>'
        f'<span style="width:{size}px;height:{size}px;background:{s["pal"][d]["f"]};box-shadow:inset 0 0 0 1px #434C89"></span></span>'
        for d, _ in DOMS) + "</span>")


def mini_world(sid, w, h):
    """A set's signature at thumbnail size: its sea washed by the four rims, one large prism per domain (the middle two
    shielded, so the tiers read too) and its crystal. Big enough that fourteen thumbnails tell fourteen sets apart."""
    C = col_static(sid)
    pw = h * .34
    o = [f'<svg width="{w}" height="{h}" viewBox="0 0 {w} {h}" aria-hidden="true" style="display:block">'
         f'<rect width="{w}" height="{h}" fill="#040A30"></rect>']
    for i, (d, _) in enumerate(DOMS):
        o.append(f'<ellipse cx="{(i + .5) * w / 4:.0f}" cy="{h * .5:.0f}" rx="{w * .22:.0f}" ry="{h * .6:.0f}" fill="{C(d + ".w")}" style="filter:blur({h * .12:.0f}px)"></ellipse>')
    for i, (d, _) in enumerate(DOMS):
        o.append(prism_g((i + .5) * w / 4.6 + w * .01, h * .5 + (i % 2) * h * .06 - h * .03, pw, -14 + i * 9, C, d, [0, 1, 0, 2][i], glow=True))
    o.append(crystal_g(w * .92, h * .2, h / 170, C) + "</svg>")
    return "".join(o)


def thumb(s, w, h):
    return (f'<div style="width:{w}px;height:{h}px;overflow:hidden;flex:none;box-shadow:inset 0 0 0 1px #2B3266">'
            + mini_world(s["id"], w, h) + "</div>")


def badge(s):
    """The one place a row says its type: Free in lime words, Premium as a gold price or a gold Owned."""
    i = s["id"]
    if s["free"] and PICKED[0]:
        return (f'<span style="color:{FREEC};display:inline-flex">{EYE}</span>' if s["vision"] else "")
    if s["free"]:
        return f'<span class="tagl" style="color:{FREEC};font-size:11px">Free</span>'
    return (f'<sc-if value="{{{{own.{i}}}}}" hint-placeholder-val="{{{{ {"true" if s["owned"] else "false"} }}}}">'
            f'<span class="tagl" style="color:{GOLD};font-size:11px">Owned</span></sc-if>'
            f'<sc-if value="{{{{buyable.{i}}}}}" hint-placeholder-val="{{{{ {"false" if s["owned"] else "true"} }}}}">'
            f'<span class="num" style="display:inline-flex;align-items:center;gap:6px;color:{GOLD};font-family:\'Chakra Petch\';font-weight:600;font-size:15px">'
            f'{CRYSTAL}{s["price"]:,}</span></sc-if>')


def row(s, w, tw=112, th=64):
    """One set in the list: its world small, its name, its four rim-over-face pairs, its type and price."""
    i = s["id"]
    edge = FREEE if s["free"] else GOLD
    return (f'<div style="position:relative;width:{w}px;flex:none">'
            f'<button class="pick" type="button" onClick="{{{{pick.{i}}}}}" aria-label="{s["name"]}" style="position:absolute;inset:0;z-index:2"></button>'
            f'<sc-if value="{{{{is.{i}}}}}" hint-placeholder-val="{{{{ {"true" if i == START else "false"} }}}}">'
            f'<span class="abs" style="inset:0;background:rgba(0,212,255,.07);box-shadow:inset 0 0 0 1px {JADE}"></span></sc-if>'
            + (f'<div class="abs" style="inset:4px 0;overflow:hidden;background:rgba(10,18,60,.55)">{card_sea(w, 76, .3)}</div>' if PICKED[0] else "")
            + f'<div style="position:relative;display:flex;align-items:center;gap:16px;padding:10px 16px 10px 14px;pointer-events:none">'
            f'<span style="width:2px;height:{th}px;background:{edge};opacity:{.9 if not s["free"] else .55};flex:none"></span>'
            + thumb(s, tw, th) +
            f'<div style="flex:1;min-width:0"><div style="display:flex;align-items:center;gap:10px"><span style="font-size:20px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">{s["name"]}</span>'
            f'<sc-if value="{{{{eqIs.{i}}}}}" hint-placeholder-val="{{{{ {"true" if i == "nocturne" else "false"} }}}}"><span style="color:{TEXT};display:inline-flex">{TICK}</span></sc-if></div>'
            f'<div style="margin-top:9px">{swatches(s, 11)}</div></div>'
            f'<div style="flex:none;text-align:right">{badge(s)}</div></div></div>')


def group_head(title, count, note, colour, icon=""):
    return (f'<div style="display:flex;align-items:baseline;gap:12px;padding:18px 16px 8px;flex:none">'
            f'<span style="display:inline-flex;align-items:center;gap:8px;color:{colour}">{icon}<span class="tagl" style="font-size:13px">{title}</span></span>'
            f'<span class="num" style="font-size:13px;color:{SOFT}">{count}</span>'
            f'<span style="margin-left:auto;font-size:12px;color:{SOFT}">{note}</span></div>')


def list_body(w, which=("free", "vision", "prem"), filt=False):
    parts = []
    if "free" in which:
        if PICKED[0]:
            g = (f'<sc-if value="{{{{f.all}}}}" hint-placeholder-val="{{{{ true }}}}">' + group_head("Yours from the start", len(FREE), "", FREEC)
                 + "".join(row(s, w) for s in FREE) + "</sc-if>")
        else:
            g = group_head("Free", len(FREE) + len(VISION), "yours from the start", FREEC)
            g += "".join(row(s, w) for s in FREE)
        g += (group_head(CB, len(VISION), "", "#C9CDEA", EYE) if PICKED[0]
              else group_head("Colour vision", len(VISION), "always free", "#C9CDEA", EYE))
        g += "".join(row(s, w) for s in VISION)
        parts.append(f'<sc-if value="{{{{show.free}}}}" hint-placeholder-val="{{{{ true }}}}">{g}</sc-if>' if filt else g)
    if "prem" in which:
        g = group_head("Premium", len(PREM), "unlock with crystals", GOLD, CRYSTAL)
        g += "".join(row(s, w) for s in PREM)
        parts.append(f'<sc-if value="{{{{show.premium}}}}" hint-placeholder-val="{{{{ true }}}}">{g}</sc-if>' if filt else g)
    return "".join(parts)


def scroller(x, y, w, h, inner, top=""):
    return (f'<section aria-label="Colour sets" class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px">'
            f'<div class="abs sl panel" style="inset:0"></div>'
            f'<div class="abs" style="inset:0;display:flex;flex-direction:column">{top}'
            f'<div class="scroll" style="flex:1;min-height:0;overflow-y:auto;display:flex;flex-direction:column;padding-bottom:12px">{inner}</div>'
            f'<div class="abs" style="left:1px;right:12px;bottom:1px;height:40px;background:linear-gradient(180deg,rgba(8,12,40,0),rgba(8,12,40,.95));pointer-events:none"></div>'
            '</div></section>')


def filters():
    chips = "".join(
        f'<button class="tabw cap" type="button" onClick="{{{{setF.{k}}}}}" style="position:relative;font-size:{13 if PICKED[0] else 14}px;{"white-space:nowrap;" if PICKED[0] else ""}color:{TEXT}">{n}'
        f'<span class="num" style="color:{SOFT};margin-left:6px">{c}</span>'
        f'<sc-if value="{{{{f.{k}}}}}" hint-placeholder-val="{{{{ {"true" if k == "all" else "false"} }}}}"><span class="abs" style="left:0;right:0;bottom:-8px;height:2px;background:{JADE}"></span></sc-if></button>'
        for k, n, c in (("all", "All", len(SETS)), ("free", CB if PICKED[0] else "Free", len(VISION) if PICKED[0] else len(FREE) + len(VISION)), ("premium", "Premium", len(PREM))))
    return f'<div style="display:flex;gap:{20 if PICKED[0] else 26}px;padding:20px 18px 12px;border-bottom:1px solid #222645;flex:none">{chips}</div>'


# ---------------------------------------------------------------- the stage: the picked set's world, name and action

def action():
    return ('<div style="display:flex;align-items:center;gap:14px;justify-content:flex-end">'
            '<sc-if value="{{canApply}}" hint-placeholder-val="{{ false }}"><button class="btn cta" type="button" onClick="{{apply}}">' + kc("A") + 'Apply</button></sc-if>'
            '<sc-if value="{{canBuy}}" hint-placeholder-val="{{ true }}"><button class="btn buy" type="button" onClick="{{buy}}">' + kc("A")
            + f'Unlock <span class="num" style="display:inline-flex;align-items:center;gap:6px">{CRYSTAL}' + '{{price}}</span></button></sc-if>'
            f'<sc-if value="{{{{isEq}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="cap" style="display:inline-flex;gap:10px;align-items:center;font-size:15px;color:{LIME}">'
            f'{TICK}<span style="color:{TEXT}">Equipped</span></span></sc-if></div>')


def type_line():
    free_word = "Yours from the start" if PICKED[0] else "Free"
    vision_word = CB if PICKED[0] else "Colour vision"
    return (f'<span style="display:inline-flex;gap:14px;align-items:center">'
            f'<sc-if value="{{{{selFree}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="tagl" style="color:{FREEC}">{free_word}</span></sc-if>'
            f'<sc-if value="{{{{selPremium}}}}" hint-placeholder-val="{{{{ true }}}}"><span class="tagl" style="color:{GOLD}">Premium</span></sc-if>'
            f'<sc-if value="{{{{selVision}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="tagl" style="display:inline-flex;gap:6px;align-items:center;color:#C9CDEA">{EYE}{vision_word}</span></sc-if>'
            '</span>')


def pigments(size=15):
    return ('<div style="display:flex;flex-wrap:wrap;gap:8px 26px">' + "".join(
        f'<span style="display:inline-flex;align-items:center;gap:9px"><span style="display:flex"><span style="width:14px;height:14px;background:{{{{p.{d}.t0.r}}}}"></span>'
        f'<span style="width:14px;height:14px;background:{{{{p.{d}.t0.f}}}};box-shadow:inset 0 0 0 1px #434C89"></span></span>'
        f'<span style="font-size:{size}px;color:#C9CDEA;white-space:nowrap">{{{{pig.{d}}}}}</span></span>' for d, _ in DOMS) + "</div>")


def stage_words(name_size=72):
    return (f'<div>{type_line()}<div style="font-size:{name_size}px;line-height:1;margin-top:10px">' + '{{selName}}</div>'
            '<div style="font-size:17px;color:#C9CDEA;margin-top:12px;max-width:820px">{{selLine}}</div>'
            f'<div style="margin-top:16px">{pigments()}</div></div>')


def world_frame(x, y, w, h, words_at="bottom", name_size=72, right_pad=48, extra=""):
    shade = ('<div class="ghost" style="background:linear-gradient(180deg,rgba(0,1,10,.5) 0%,rgba(0,1,10,0) 20%,rgba(0,1,10,0) 45%,rgba(0,1,10,.92) 88%)"></div>')
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px">'
            f'<div class="abs sl" style="inset:0;overflow:hidden">{world(w, h, col_hole, n=46)}{shade}</div>'
            f'<div class="abs" style="left:40px;top:28px;display:flex;gap:18px;align-items:center"><span class="tagl" style="color:#C9CDEA">Wearing ' + '{{eqName}}</span></div>'
            f'<div class="abs" style="right:{right_pad}px;top:26px;font-size:14px;color:{SOFT}">Only you see your colour set</div>'
            f'<div class="abs" style="left:40px;right:{right_pad}px;bottom:34px;display:flex;align-items:flex-end;justify-content:space-between;gap:30px">'
            f'{stage_words(name_size)}<div style="flex:none">{action()}</div></div>{extra}</div>')


TOAST = ('<sc-if value="{{toastOn}}" hint-placeholder-val="{{ false }}"><div role="status" class="abs sls" style="right:40px;top:96px;width:440px;background:#00010A;'
         'box-shadow:inset 0 0 0 1px #434C89;display:flex;z-index:9;animation:tstIn 320ms cubic-bezier(.22,1,.36,1) both">'
         f'<span style="width:3px;background:{LIME}"></span><div style="padding:13px 18px;flex:1"><div class="cap" style="font-size:14px">' + '{{toastHead}}</div>'
         '<div style="font-size:14px;color:#C9CDEA;margin-top:3px">{{toastLine}}</div></div>'
         '<button class="pick" type="button" onClick="{{dismiss}}" aria-label="Close" style="padding:0 14px;color:#747BAD">'
         '<svg width="12" height="12" viewBox="0 0 12 12" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M1 1l10 10M11 1L1 11"></path></svg></button></div></sc-if>')

EXTRA_CSS = """
.buy{background:#FFAE00;color:#00010A}
.scroll{scrollbar-width:thin;scrollbar-color:#434C89 transparent}
.scroll::-webkit-scrollbar{width:6px}
.scroll::-webkit-scrollbar-thumb{background:#434C89}
.wallcard{position:relative}
"""
HELMET = r54.HELMET.replace("</style>", EXTRA_CSS + "</style>")


def full_board(title, body, logic, w=1920, h=1080):
    return board(title, body, logic, w, h, PROPS).replace(r51.HELMET, HELMET)


# ---------------------------------------------------------------- A  Stage and list (recommended)

def option_a():
    Y, H = 176, 872
    lst = scroller(40, Y, 540, H, list_body(540))
    stage = world_frame(600, Y, 1280, 600)
    under = f'<div class="abs" style="left:600px;top:{Y + 616}px">{tiles_row(1280, H - 616)}</div>'
    return r54.chrome() + lst + stage + under + TOAST


# ---------------------------------------------------------------- B  Wall

def wall_card(s, w, th):
    i = s["id"]
    edge = FREEE if s["free"] else GOLD
    return (f'<div class="wallcard" style="width:{w}px">'
            f'<button class="pick" type="button" onClick="{{{{pick.{i}}}}}" aria-label="{s["name"]}" style="position:absolute;inset:0;z-index:2"></button>'
            f'<div style="position:relative;pointer-events:none">'
            f'<div class="sls" style="overflow:hidden;height:{th}px">{mini_world(i, w, th)}</div>'
            f'<span class="abs" style="left:0;right:0;bottom:0;height:2px;background:{edge};opacity:{.55 if s["free"] else .9}"></span>'
            f'<sc-if value="{{{{is.{i}}}}}" hint-placeholder-val="{{{{ {"true" if i == START else "false"} }}}}"><span class="abs sls" style="inset:0;box-shadow:inset 0 0 0 2px {JADE}"></span></sc-if></div>'
            f'<div style="display:flex;align-items:center;gap:10px;margin-top:9px;pointer-events:none">{('<span style="color:#C9CDEA;display:inline-flex">' + EYE + '</span>') if s["vision"] else ""}<span style="font-size:16px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;flex:1">{s["name"]}</span>'
            f'<sc-if value="{{{{eqIs.{i}}}}}" hint-placeholder-val="{{{{ {"true" if i == "nocturne" else "false"} }}}}"><span style="color:{TEXT};display:inline-flex">{TICK}</span></sc-if>'
            f'{badge(s)}</div></div>')


def shelf(title, sets, w, cols, note, colour, icon="", key=None):
    gap = 18
    cw = (w - gap * (cols - 1)) // cols
    cards = "".join(wall_card(s, cw, 104) for s in sets)
    body = (group_head(title, len(sets), note, colour, icon).replace("padding:18px 16px 8px", "padding:14px 0 10px")
            + f'<div style="display:grid;grid-template-columns:repeat({cols}, {cw}px);gap:18px {gap}px">{cards}</div>')
    return f'<sc-if value="{{{{show.{key}}}}}" hint-placeholder-val="{{{{ true }}}}">{body}</sc-if>' if key else body


def option_b():
    Y = 176
    stage = world_frame(40, Y, 1180, 470, name_size=60)
    side = f'<div class="abs" style="left:1240px;top:{Y}px">{tiles_row(640, 470, gap=16, cols=2)}</div>'
    WY = Y + 490
    WH = 1048 - WY
    inner = (shelf("Free", FREE + VISION, 1800, 8, "yours from the start; the eye marks a set made for colour vision", FREEC, key="free")
             + shelf("Premium", PREM, 1800, 8, "unlock with crystals", GOLD, CRYSTAL, key="premium"))
    wall = (f'<section aria-label="Colour sets" class="abs" style="left:40px;top:{WY}px;width:1840px;height:{WH}px">'
            '<div class="abs sl panel" style="inset:0"></div>'
            '<div class="abs" style="inset:0;display:flex;flex-direction:column">'
            + filters().replace("padding:20px 18px 12px", "padding:16px 20px 10px")
            + f'<div class="scroll" style="flex:1;min-height:0;overflow-y:auto;padding:0 20px 18px">{inner}</div></div></section>')
    return r54.chrome() + stage + side + wall + TOAST


# ---------------------------------------------------------------- C  Two shelves

def option_c():
    Y, H = 176, 872
    left = scroller(40, Y, 420, H, list_body(420, ("free",)).replace("width:112px;height:64px", "width:96px;height:56px"))
    right = scroller(1460, Y, 420, H, list_body(420, ("prem",)))
    stage = world_frame(480, Y, 960, 592, name_size=58, right_pad=32)
    under = f'<div class="abs" style="left:480px;top:{Y + 608}px">{tiles_row(960, H - 608, gap=12)}</div>'
    return r54.chrome() + left + right + stage + under + TOAST


# ---------------------------------------------------------------- D  The real cell

def option_d():
    PICKED[0] = True
    try:
        return _option_d()
    finally:
        PICKED[0] = False


def _option_d():
    full = (f'<div class="abs" style="left:0;top:84px;width:1920px;height:996px;overflow:hidden">{world(1920, 996, col_hole, n=60)}'
            '<div class="ghost" style="background:linear-gradient(90deg,rgba(0,1,10,.0) 0%,rgba(0,1,10,0) 50%,rgba(0,1,10,.55) 72%),'
            'linear-gradient(180deg,rgba(0,1,10,.6) 0%,rgba(0,1,10,0) 16%,rgba(0,1,10,0) 44%,rgba(0,1,10,.9) 90%)"></div></div>')
    head = r54.chrome().split(r51.topnav())[1]
    lst = scroller(1360, 176, 520, 872, list_body(520, filt=True), top=filters())
    words = (f'<div class="abs" style="left:40px;top:548px;display:flex;gap:18px"><span class="tagl" style="color:#C9CDEA">Wearing ' + '{{eqName}}</span>'
             f'<span style="font-size:14px;color:{SOFT}">Only you see your colour set</span></div>'
             f'<div class="abs" style="left:40px;width:1290px;top:592px;display:flex;align-items:flex-end;justify-content:space-between;gap:24px">'
             f'{stage_words(80)}<div style="flex:none">{action()}</div></div>')
    under = f'<div class="abs" style="left:40px;top:858px">{tiles_row(1300, 190)}</div>'
    return r51.topnav() + full + head + lst + words + under + TOAST


# ---------------------------------------------------------------- logic

def logic():
    P = {sid: COL[sid] for sid in COL}
    S = {s["id"]: {"name": s["name"], "line": s["line"], "free": s["free"], "vision": s["vision"], "price": s["price"],
                   "pig": {d: (s["pig"][k] if k < len(s["pig"]) else "") for k, (d, _) in enumerate(DOMS)}} for s in SETS}
    return f"""const P = {json.dumps(P, separators=(',', ':'))};
const S = {json.dumps(S, separators=(',', ':'))};
const IDS = Object.keys(S);
class Component extends DCLogic {{
constructor(p){{ super(p); this.state = {{ sel: this.props.start ?? '{START}', eq: 'nocturne', owned: {json.dumps(sorted(OWNED))}, toast: '', filter: 'all' }}; }}
renderVals() {{
const st = this.state;
const s = S[st.sel];
const owns = (id) => S[id].free || st.owned.indexOf(id) >= 0;
const is = {{}}, eqIs = {{}}, own = {{}}, buyable = {{}}, pick = {{}};
IDS.forEach(id => {{
  is[id] = st.sel === id; eqIs[id] = st.eq === id;
  own[id] = !S[id].free && owns(id); buyable[id] = !owns(id);
  pick[id] = () => this.setState({{ sel: id, toast: '' }});
}});
const mine = owns(st.sel);
return {{
  p: P[st.sel], pig: s.pig, is, eqIs, own, buyable, pick,
  selName: s.name, selLine: s.line, selFree: s.free && !s.vision, selVision: s.vision, selPremium: !s.free,
  eqName: S[st.eq].name, price: s.price.toLocaleString('en-US'),
  canApply: mine && st.sel !== st.eq, canBuy: !mine, isEq: st.sel === st.eq,
  toastOn: st.toast !== '',
  toastHead: st.toast === 'buy' ? 'Colour set unlocked' : 'Colour set applied',
  toastLine: st.toast === 'buy' ? s.name + ' is yours. Press A to wear it.' : S[st.eq].name + ' is on every prism, vessel, trail and menu.',
  f: {{ all: st.filter === 'all', free: st.filter === 'free', premium: st.filter === 'premium' }},
  setF: {{ all: () => this.setState({{ filter: 'all' }}), free: () => this.setState({{ filter: 'free' }}), premium: () => this.setState({{ filter: 'premium' }}) }},
  show: {{ free: st.filter !== 'premium', premium: st.filter !== 'free' }},
  apply: () => this.setState({{ eq: st.sel, toast: 'apply' }}),
  buy: () => this.setState({{ owned: st.owned.concat([st.sel]), toast: 'buy' }}),
  dismiss: () => this.setState({{ toast: '' }})
}};
}}
}}"""


PROPS = {"start": {"editor": "enum", "options": [s["id"] for s in SETS], "default": START, "section": "State"}}


# ---------------------------------------------------------------- compare and notes

def mini(kind):
    """A layout diagram of one option at 1/4.4 scale: where the list, the world and the tiles sit."""
    W, H = 420, 236
    def box(x, y, w, h, fill, label="", col="#C9CDEA"):
        return (f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" stroke="#434C89" stroke-width="1"></rect>'
                + (f'<text x="{x + 8}" y="{y + 16}" font-size="11" fill="{col}" font-family="Chakra Petch" letter-spacing="1.5">{label}</text>' if label else ""))
    world_f, list_f, tile_f = "#1A2470", "#0B1036", "#111845"
    o = [f'<svg width="{W}" height="{H}" viewBox="0 0 {W} {H}" aria-hidden="true"><rect width="{W}" height="{H}" fill="#00041F"></rect>'
         f'<rect width="{W}" height="18" fill="#00010A"></rect>']
    if kind == "A":
        o += [box(8, 38, 118, 190, list_f, "LIST"), box(134, 38, 278, 130, world_f, "WORLD"), box(134, 174, 278, 54, tile_f, "REPAINTS")]
    elif kind == "B":
        o += [box(8, 38, 256, 104, world_f, "WORLD"), box(270, 38, 142, 104, tile_f, "REPAINTS"), box(8, 148, 404, 80, list_f, "WALL OF SETS")]
    elif kind == "C":
        o += [box(8, 38, 92, 190, list_f, "FREE", FREEC), box(320, 38, 92, 190, list_f, "PREMIUM", GOLD), box(106, 38, 208, 128, world_f, "WORLD"),
              box(106, 172, 208, 56, tile_f, "REPAINTS")]
    else:
        o += [box(0, 18, W, H - 18, world_f, ""), box(296, 38, 116, 190, list_f, "LIST"), box(8, 186, 282, 42, tile_f, "REPAINTS"),
              '<text x="12" y="150" font-size="11" fill="#C9CDEA" font-family="Chakra Petch" letter-spacing="1.5">THE WORLD IS THE SCREEN</text>']
    o.append("</svg>")
    return "".join(o)


def compare_board():
    opts = [("A", "Stage and list", "List left, the world and its four repaints right. Every pixel is the set or the list.", "recommended"),
            ("B", "Wall", "The world on top; every set a tile below. Best once there are 30 or more sets.", ""),
            ("C", "Two shelves", "Free left, Premium right, the world between. The type is where a set sits.", ""),
            ("D", "The real cell", "The world is the screen, as the built Showroom; a docked list on the right.", "")]
    cards = "".join(
        f'<div><div class="sl" style="overflow:hidden">{mini(k)}</div><div style="display:flex;gap:12px;align-items:baseline;margin-top:12px">'
        f'<span style="font-size:28px">{k}</span><span style="font-size:20px">{n}</span>'
        + (f'<span class="tagl" style="color:{TEXT};margin-left:auto">{rec}</span>' if rec else "")
        + f'</div><div style="font-size:14px;color:#C9CDEA;margin-top:6px;line-height:1.45">{d}</div></div>' for k, n, d, rec in opts)
    rows = (f'<div style="display:flex;flex-direction:column;gap:0">'
            + row(SET["photic"], 560).replace("{{is.photic}}", "{{no}}").replace("{{eqIs.photic}}", "{{no}}")
            + row(SET["protanopia"], 560).replace("{{is.protanopia}}", "{{no}}").replace("{{eqIs.protanopia}}", "{{no}}")
            + row(SET["dartfrog"], 560).replace("{{is.dartfrog}}", "{{no}}").replace("{{eqIs.dartfrog}}", "{{no}}")
            + row(SET["neonnoir"], 560).replace("{{is.neonnoir}}", "{{no}}").replace("{{eqIs.neonnoir}}", "{{no}}") + '</div>')
    rows = re.sub(r'onClick="\{\{pick\.\w+\}\}"', "", rows)
    rows = rows.replace("{{own.dartfrog}}", "{{yes}}").replace("{{buyable.dartfrog}}", "{{no}}") \
               .replace("{{own.neonnoir}}", "{{no}}").replace("{{buyable.neonnoir}}", "{{yes}}")
    legend = [("Free", FREEC, "A plain word, a quiet edge. No price: the set is yours."),
              ("Colour vision", "#C9CDEA", "Inside Free, under its own heading: never a reward (section 126.3)."),
              ("Premium, owned", GOLD, "Gold edge, the word Owned. Apply as any free set."),
              ("Premium, to unlock", GOLD, "Gold edge, the crystal and its price. The button turns gold: Unlock.")]
    leg = "".join(f'<div style="display:flex;gap:14px;align-items:baseline;margin-top:14px"><span class="tagl" style="color:{c};width:170px;flex:none">{n}</span>'
                  f'<span style="font-size:14px;color:#C9CDEA;line-height:1.45">{d}</span></div>' for n, c, d in legend)
    tl = "".join(f'<div style="width:280px">{tile(k, l, s, 280, 220, col_static("dartfrog"))}</div>' for k, l, s in TILES)
    body = (f'<div style="padding:40px 56px"><span class="lab">Rev {REV} - compare</span>'
            '<div style="font-size:34px;margin:6px 0 24px">Four layouts, one list, one world</div>'
            f'<div style="display:grid;grid-template-columns:repeat(4, minmax(0, 1fr));gap:28px">{cards}</div>'
            '<div style="display:flex;gap:48px;margin-top:40px">'
            f'<div><span class="lab">Free and Premium in a row</span><div class="sl panel" style="margin-top:14px;padding:6px 0">{rows}</div></div>'
            f'<div style="flex:1;min-width:0"><span class="lab">What each mark says</span>{leg}'
            f'<div class="lab" style="margin-top:30px">What a set repaints (Dart Frog)</div>'
            f'<div style="display:grid;grid-template-columns:repeat(4, 280px);gap:14px;margin-top:14px">{tl}</div></div></div></div>')
    lg = "class Component extends DCLogic {\nrenderVals() {\nreturn { yes: true, no: false };\n}\n}"
    return board(title("Compare"), body, lg).replace(r51.HELMET, HELMET)


def notes_board():
    cols = [
        ("Asked", [
            "\"we will be making multiples of them so scrolling or clicking through each one is not an option\": the built Showroom's < > rail shows three of fourteen and steps one at a time.",
            "\"a preview to show what the color set changes to depict the world of our game\": the world, plus four tiles for what it repaints (prisms in three shields, vessels and trails, crystals, menus).",
            "\"a scrollable list with differentiation between free and the other type\" and \"use the whole place\": every option fills the screen; nothing sits in an empty middle."]),
        ("The four", [
            "A Stage and list (recommended): the list holds eight sets in view and scrolls; the world and its tiles take the rest. Every set one press away, the preview as big as the screen allows.",
            "B Wall: twelve tiles in view, each a little world; scales furthest when the catalog passes thirty.",
            "C Two shelves: the type is a place, not a mark. Costs width: the world is the smallest of the four.",
            "D The real cell: closest to what is built (the world is the menu cell itself); the list docks right with All / Free / Premium."]),
        ("Free and Premium", [
            "Free: Nocturne, Photic, and the six colour-vision sets under their own heading (always free, section 126.3). A plain word and a quiet edge: Lime stays the one call to action (Apply), so Gold alone says Premium.",
            "Premium: the other six. Gold edge; Owned, or the crystal and its price; the button turns gold (Unlock), the style guide's purchase button.",
            "Prices and Dart Frog owned are sample values. How a Premium set is earned (crystals, a quest as in Rev 107, a store) is not decided."]),
        ("In Unity, and decisions", [
            "The world in A, B and C is a framed view of the menu cell (as the Showroom does); the four tiles are small renders, or drawn UI from the set's colours.",
            "The menus tile is an approximation of ColorSetUITheme; the build paints it from the set.",
            "1. Which option. 2. The word for the other type: Premium (drawn), Earned or Store. 3. Do tiles animate (a prism shielding, a trail drawn) or stay still."]),
    ]
    html = "".join(f'<div style="flex:1;min-width:0"><span class="lab" style="font-size:14px">{t}</span>'
                   + "".join(f'<p style="font-size:15px;line-height:1.5;color:#C9CDEA;margin:12px 0 0">{i}</p>' for i in items) + '</div>'
                   for t, items in cols)
    body = (f'<div style="padding:40px 56px"><span class="lab">Rev {REV} - notes</span><div style="font-size:34px;margin:6px 0 22px">Colour sets for a growing catalog</div>'
            f'<div style="display:flex;gap:40px">{html}</div></div>')
    return board(title("Notes"), body, w=1920, h=900).replace(r51.HELMET, HELMET)


def title(key):
    canvas = json.loads((ROOT / "canvas.json").read_text())
    return (canvas["boards"].get(f"R{REV}{key}.dc.html") or {}).get("title") or f"Rev {REV} {key}"


def main():
    out = {
        "Compare": compare_board(),
        "A": full_board(title("A"), option_a(), logic()),
        "B": full_board(title("B"), option_b(), logic()),
        "C": full_board(title("C"), option_c(), logic()),
        "D": full_board(title("D"), option_d(), logic()),
        "Notes": notes_board(),
    }
    for k, html in out.items():
        (ROOT / f"R{REV}{k}.dc.html").write_text(html)
        print(f"wrote R{REV}{k}.dc.html ({len(html) // 1024} KB)")


if __name__ == "__main__":
    main()
