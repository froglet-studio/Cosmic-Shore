#!/usr/bin/env python3
"""Rev 94 boards, second pass: the crew and the three domains as one poster (Frogma tab "Loading Screens").

Shombith, 2026-11-12, on the first four posters (the crew, the shore, the pilot, three domains): "Delete others and
can you combine D and A and the theme switching in this scene as well, this scene needs to be very very smooth ...
the colors on D is too bright, I dont like that and also the empty dull blue. Can show some animated parts of the
cell along with the sea lines in the background."

So: A's crew and D's three slanted domain bands in one picture; the first pass's four boards are gone from the canvas
(they are in git at aa2439de9d). What changed against them:

  the bands    each is a prism of its domain as the worn colour set paints it: the FACE colour as the body (a dark
               jewel, not a bright flat), the RIM colour as a thin edge. The set changes on a 28 s loop - Nocturne,
               Dart Frog, Dusk, Ukiyo-e (Docs/VesselModels/colour_sets.json) - blending, never cutting.
  the ground   no flat blue discs: Rev 91's sea lines, and parts of a cell moving in it (a membrane breathing,
               crystals, a prism trail being laid, flora turning).
  motion       every animation eases both ways and alternates; nothing pops or restarts.

  A  The crew before the domains   five pilots in a line across the three bands, the Ark above (recommended)
  B  Three crews                   a pilot and a hull in each band, the Ark beside

    python3 Docs/MenuRedesignB/rounds/r94_posters.py <scratch root> [rev]
"""
import importlib.util
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "94"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r92", HERE / "r92_splash.py")
r92 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r92)          # sets the bar's steps to the boot status lines
r91 = r92.r91

REV = int(_rev)
INK, DIM, LINE, GROUND, DEEP, PALE = r91.INK, r91.DIM, r91.LINE, r91.GROUND, r91.DEEP, r92.PALE
b = r91.blob
LOGO, FROG = b("9ec3291104bcd9430ee361c4a7f4b879"), b("57c57263842bd92d07e7828027588b6b")
CEL = {"Squirrel": b("1468d15884d9031c5e4378ded3e0dec0"), "Sparrow": b("2314bea6ceea7e36cfae27a74f37fe46"),
       "Dolphin": b("c5b89e4807daf4a805b0039e865a1ce0")}
PILOT = {"Galleon": b("26b33660e516612a262c2f693e37ac5c"), "Needle": b("325da62cb7f1a098a367c01003c52078"),
         "Anvil": b("e14cc944b8d648a2efa28acd4dd88943"), "Sundial": b("9d71be03b91a703e90bb458a587cd7fd"),
         "Hornet": b("ad76b45e5834587f8e8abf4e0ec90066")}
ARK = r91.ARK

# (rim, face) per domain, straight from Docs/VesselModels/colour_sets.json `sets.<id>.domains.<family>.<pigment>.swatch`.
SETS = [("Nocturne", {"jade": ("#4BD5D4", "#480135"), "ruby": ("#DF5CE4", "#00384E"), "gold": ("#ECBD44", "#3F205F")}),
        ("Dart Frog", {"jade": ("#6ECDAD", "#52290C"), "ruby": ("#CE62ED", "#0A424B"), "gold": ("#FDD052", "#2A1B3D")}),
        ("Dusk", {"jade": ("#66DED7", "#3C1D08"), "ruby": ("#CB66E9", "#1E2A1E"), "gold": ("#F0D58C", "#4F3135")}),
        ("Ukiyo-e", {"jade": ("#61D5C3", "#2C6A61"), "ruby": ("#B049E5", "#5A2279"), "gold": ("#FCD269", "#94753A")})]
DOMAINS = ["jade", "ruby", "gold"]
LOOP = 28


def theme_css():
    """One loop through the sets. Each holds, then blends into the next over a fifth of its turn; the last blends
    back into the first, so the loop has no seam."""
    n, out = len(SETS), ""
    for dom in DOMAINS:
        for pig, prop, name in ((1, "background-color", f"face-{dom}"), (0, "background-color", f"rim-{dom}")):
            keys = ""
            for i, (_n, cols) in enumerate(SETS):
                s, e = 100 * i / n, 100 * (i + 1) / n - 100 / n * .22
                keys += f"{s:.2f}%,{e:.2f}%{{{prop}:{cols[dom][pig]}}}"
            keys += f"100%{{{prop}:{SETS[0][1][dom][pig]}}}"
            out += f"@keyframes {name}{{{keys}}}"
    for i in range(n):
        s, e = 100 * i / n, 100 * (i + 1) / n - 100 / n * .22
        nxt = 100 * (i + 1) / n
        if i == 0:
            out += f"@keyframes set0{{0%,{e:.2f}%{{opacity:1}}{nxt:.2f}%,{100 - 100 / n * .22:.2f}%{{opacity:0}}100%{{opacity:1}}}}"
        else:
            out += f"@keyframes set{i}{{0%,{s - 100 / n * .22:.2f}%{{opacity:0}}{s:.2f}%,{e:.2f}%{{opacity:1}}{nxt:.2f}%,100%{{opacity:0}}}}"
    return out


CSS = """@keyframes breathe{0%{transform:scale(1)}100%{transform:scale(1.035)}}
@keyframes bob{0%{transform:translateY(-8px)}100%{transform:translateY(10px)}}
@keyframes flow{0%{stroke-dashoffset:0}100%{stroke-dashoffset:-270}}
@keyframes pulse{0%{opacity:.3}100%{opacity:.95}}
@keyframes bloom{0%{transform:scale(.86) rotate(0deg)}100%{transform:scale(1.06) rotate(22deg)}}
.burst{transform-box:fill-box;transform-origin:center}
""" + f".th{{animation-duration:{LOOP}s;animation-timing-function:ease-in-out;animation-iteration-count:infinite}}\n" + theme_css()


def img(src, alt, x, y, w, h, extra=""):
    return f'<img src="{src}" alt="{alt}" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{h}px;object-fit:contain;{extra}">'


def marks(x, y, w, fx, fy, fw=240):
    return img(LOGO, "Cosmic Shore", x, y, w, round(w * 359 / 910)) + img(FROG, "Froglet Games", fx, fy, fw, round(fw * 322 / 900))


def band(i, x, w=400, top=-60, h=1000, alpha=.9):
    """One domain as its prism: the set's face colour as the body, its rim colour as the two long edges."""
    dom = DOMAINS[i]
    face, rim = SETS[0][1][dom][1], SETS[0][1][dom][0]
    edge = f"position:absolute;top:0;width:3px;height:100%;background-color:{rim};animation-name:rim-{dom}"
    return (f'<div aria-hidden="true" style="position:absolute;left:{x}px;top:{top}px;width:{w}px;height:{h}px;transform:skewX(-14deg)">'
            f'<div class="th" style="position:absolute;inset:0;opacity:{alpha};background-color:{face};animation-name:face-{dom}"></div>'
            f'<div class="th" style="{edge};left:0"></div><div class="th" style="{edge};right:0"></div></div>')


def cell_parts(seed, membrane, crystals, trail_d, bursts):
    """Parts of a cell moving in the sea: a membrane breathing, crystals, a prism trail being laid, flora turning."""
    rng = random.Random(seed)
    mx, my, mr = membrane
    out = (f'<g style="transform-box:fill-box;transform-origin:center;animation:breathe 11s ease-in-out infinite alternate">'
           f'<circle cx="{mx}" cy="{my}" r="{mr}" fill="url(#cellG)" stroke="rgba(185,201,250,.5)" stroke-width="2"></circle>'
           f'<ellipse cx="{mx - mr * .32:.0f}" cy="{my - mr * .42:.0f}" rx="{mr * .4:.0f}" ry="{mr * .2:.0f}" transform="rotate(-32 {mx - mr * .32:.0f} {my - mr * .42:.0f})" fill="rgba(230,233,255,.06)"></ellipse></g>')
    for i, (x, y) in enumerate(crystals):
        out += (f'<rect x="{x - 8}" y="{y - 8}" width="16" height="16" transform="rotate(45 {x} {y})" fill="{INK}" '
                f'style="animation:pulse {3 + i * .9:.1f}s ease-in-out infinite alternate"></rect>')
    out += f'<path d="{trail_d}" stroke="{PALE}" stroke-width="10" stroke-dasharray="20 7" opacity=".55" style="animation:flow 4.5s linear infinite"></path>'
    for x, y, r, n, dur in bursts:
        out += r92.burst(x, y, r, PALE, n, dur, rng).replace('opacity=".8"', 'opacity=".5"')
    return r92.svg(out)


def themes(x, y, row=True):
    """Which colour set is worn: the four names, the worn one lit. A readout, not a control."""
    flex = "display:flex;gap:28px" if row else "display:flex;flex-direction:column;gap:12px"
    out = f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;flex-direction:column;gap:12px"><span class="lab">Colour set</span><div style="{flex}">'
    for i, (n, _c) in enumerate(SETS):
        out += (f'<span style="position:relative;font-size:18px;color:{DIM};white-space:nowrap">{n}'
                f'<span class="th" aria-hidden="true" style="position:absolute;left:0;top:0;color:{INK};opacity:{1 if i == 0 else 0};animation-name:set{i}">{n}</span></span>')
    return out + "</div></div>"


def foot(y=860):
    return f'<div class="abs" aria-hidden="true" style="left:0;top:{y}px;width:1920px;height:{1080 - y}px;background:{DEEP}"></div>' + r91.bar()


# ── A The crew before the domains ─────────────────────────────────────────────────────────

def board_a():
    line = [("Hornet", 430), ("Needle", 500), ("Galleon", 590), ("Sundial", 500), ("Anvil", 430)]
    xs = [470, 715, 960, 1205, 1450]
    crew = ""
    for i in (0, 4, 1, 3, 2):                       # the outer pilots first, the middle one in front
        n, h = line[i]
        w = round(h * 440 / 560)
        crew += img(PILOT[n], n, xs[i] - w // 2, 862 - h, w, h)
    parts = cell_parts(1, (1800, 380, 300), [(1690, 300), (1770, 470), (150, 520), (250, 660)],
                       "M -40 760 C 160 700 300 560 380 300", [(210, 330, 110, 26, 9)])
    body = (r91.sea() + parts + "".join(band(i, 360 + i * 400) for i in range(3))
            + img(ARK, "The Ark", 600, 96, 720, 337, "animation:bob 10s ease-in-out infinite alternate")
            + crew + marks(96, 64, 300, 96, 200, 180) + themes(1650, 620, row=False) + foot())
    return r91.page(f"R{REV}A.dc.html", body, CSS)


# ── B Three crews ─────────────────────────────────────────────────────────────────────────

def board_b():
    seats = [("Needle", "Dolphin", 44, 300, 7), ("Galleon", "Squirrel", 14, 340, 9), ("Anvil", "Sparrow", 10, 320, 8)]
    crews = ""
    for i, (p, hname, tilt, s, dur) in enumerate(seats):
        cx = 920 + i * 400 - 60                    # the band's middle at the pilots' height
        crews += img(CEL[hname], f"The {hname}", cx + 70 - s // 2, 70, s, s, f"transform:rotate({tilt}deg);animation:bob {dur}s ease-in-out infinite alternate")
        crews += img(PILOT[p], p, cx - 173, 862 - 440, 346, 440)
    parts = cell_parts(2, (310, 600, 280), [(150, 520), (430, 700), (560, 420)],
                       "M -40 840 C 180 830 500 790 700 600", [(600, 250, 80, 22, 10)])
    body = (r91.sea() + parts + "".join(band(i, 700 + i * 400) for i in range(3)) + crews
            + img(ARK, "The Ark", 60, 470, 520, 244, "animation:bob 10s ease-in-out infinite alternate")
            + marks(96, 64, 400, 96, 240, 200) + themes(96, 770) + foot())
    return r91.page(f"R{REV}B.dc.html", body, CSS)


def board_notes():
    cols = [
        ("Asked", ["Shombith, 2026-11-12: \"Delete others and can you combine D and A and the theme switching in this scene as well, this scene needs to be very very smooth ... the colors on D is too bright ... and also the empty dull blue. Can show some animated parts of the cell along with the sea lines in the background.\""]),
        ("What changed", ["The crew (A) and the three domain bands (D) are one poster. The first four boards are off the canvas.",
                          "A band is its domain's prism: the colour set's face colour as the body, its rim colour as a thin edge. No bright flats.",
                          "No blue discs. Behind: the sea lines, and parts of a cell moving (a membrane, crystals, a trail being laid, flora)."]),
        ("The colour set changes", ["Nocturne, Dart Frog, Dusk and Ukiyo-e in turn, on a 28 second loop. Each holds, then blends into the next; the loop has no seam.",
                                    "The colours are the sets' own rim and face swatches. The names top right are a readout of which is worn.",
                                    "In the game the splash would wear the player's set, and blend when it changes."]),
        ("The two", ["A The crew before the domains (recommended): five pilots across the three bands, the Ark above.",
                     "B Three crews: a pilot and a hull in each band, the Ark beside.",
                     "Smooth: every motion eases both ways and alternates; nothing pops or restarts."]),
    ]
    body = (f'<div class="abs" aria-hidden="true" style="inset:0;background:{GROUND}"></div>'
            f'<header class="abs" style="left:96px;top:64px;display:flex;flex-direction:column;gap:6px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:36px">The crew and the three domains, one poster</span></header>'
            '<div class="abs" style="left:96px;top:200px;width:1728px;display:grid;grid-template-columns:repeat(4,1fr);gap:48px">')
    for head, items in cols:
        body += (f'<section style="display:flex;flex-direction:column;gap:16px;padding-top:16px;box-shadow:inset 0 1px 0 {LINE}"><span style="font-size:22px">{head}</span>'
                 + "".join(f'<span style="font-size:18px;line-height:1.5;color:{INK}">{t}</span>' for t in items) + "</section>")
    body += "</div>"
    return r91.page(f"R{REV}Notes.dc.html", body, h=900, loop=False)


if __name__ == "__main__":
    for fn in (board_a, board_b, board_notes):
        print("wrote", fn())
