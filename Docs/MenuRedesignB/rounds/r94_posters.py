#!/usr/bin/env python3
"""Rev 94 boards: the boot splash as a poster (Frogma tab "Loading Screens").

Shombith, 2026-11-12, on Rev 93: "Okay can we move away from this neon design, create some good poster based
mockups 4".

No glow anywhere: flat colour fields, the roster's painted pilots, the real hulls drawn in three flat tones with an
ink line, and both logos as flat marks. Each board is one poster with Rev 91's bar on a dark foot.

  A  The crew        an ensemble poster: five pilots in a line before the Ark, on two flat discs (recommended)
  B  The shore       a travel poster: a low sun-coloured cell on the horizon, a flat sea, the Squirrel crossing it
  C  The pilot       a character one-sheet: one pilot on a gold disc, the hull's shadow behind; four take turns
  D  Three domains   three slanted bands in Jade, Ruby and Gold, a hull in each

    python3 Docs/MenuRedesignB/rounds/r94_posters.py <scratch root> [rev]
"""
import importlib.util
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
INK, DIM, LINE, GROUND, DEEP = r91.INK, r91.DIM, r91.LINE, r91.GROUND, r91.DEEP
JADE, RUBY, GOLD = r92.JADE, r92.RUBY, r92.GOLD
SURFACE, B44, B52, GOLD92, GOLD84 = "#222645", "#302BD5", "#404EEC", "#FEE297", "#FFBC5D"
b = r91.blob
LOGO, FROG = b("9ec3291104bcd9430ee361c4a7f4b879"), b("57c57263842bd92d07e7828027588b6b")
CEL = {"Squirrel": b("1468d15884d9031c5e4378ded3e0dec0"), "Sparrow": b("2314bea6ceea7e36cfae27a74f37fe46"),
       "Dolphin": b("c5b89e4807daf4a805b0039e865a1ce0")}
SHADOW = b("417c94d7b0d798ef93a2c4078caf9de5")
PILOT = {"Galleon": b("26b33660e516612a262c2f693e37ac5c"), "Needle": b("325da62cb7f1a098a367c01003c52078"),
         "Anvil": b("e14cc944b8d648a2efa28acd4dd88943"), "Sundial": b("9d71be03b91a703e90bb458a587cd7fd"),
         "Hornet": b("ad76b45e5834587f8e8abf4e0ec90066")}
ARK = r91.ARK

CSS = """@keyframes breathe{0%{transform:scale(1)}100%{transform:scale(1.04)}}
@keyframes slide{0%{transform:translateX(0)}100%{transform:translateX(-1920px)}}
@keyframes bob{0%{transform:translateY(-8px)}100%{transform:translateY(10px)}}
@keyframes cross{0%{transform:translate(-30px,14px)}100%{transform:translate(40px,-18px)}}"""


def img(src, alt, x, y, w, h, extra=""):
    return f'<img src="{src}" alt="{alt}" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{h}px;object-fit:contain;{extra}">'


def marks(x, y, w, fx, fy, fw=240):
    """Both logos as flat marks."""
    return img(LOGO, "Cosmic Shore", x, y, w, round(w * 359 / 910)) + img(FROG, "Froglet Games", fx, fy, fw, round(fw * 322 / 900))


def field(col, x=0, y=0, w=1920, h=1080):
    return f'<div class="abs" aria-hidden="true" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;background:{col}"></div>'


def disc(cx, cy, r, col, breathe=0):
    anim = f";animation:breathe {breathe}s ease-in-out infinite alternate" if breathe else ""
    return (f'<div class="abs" aria-hidden="true" style="left:{cx - r}px;top:{cy - r}px;width:{2 * r}px;height:{2 * r}px;border-radius:50%;background:{col}{anim}"></div>')


def foot(y=860):
    """The poster's dark foot, where the bar sits."""
    return field(DEEP, 0, y, 1920, 1080 - y) + r91.bar()


# ── A The crew ────────────────────────────────────────────────────────────────────────────

def board_a():
    line = [("Hornet", 430), ("Needle", 500), ("Galleon", 590), ("Sundial", 500), ("Anvil", 430)]
    xs = [470, 715, 960, 1205, 1450]
    crew = ""
    for i in (0, 4, 1, 3, 2):                       # the outer pilots first, the middle one in front
        n, h = line[i]
        w = round(h * 440 / 560)
        crew += img(PILOT[n], n, xs[i] - w // 2, 862 - h, w, h)
    body = (field(GROUND) + disc(960, 520, 560, B44, 12) + disc(960, 520, 400, B52, 9)
            + img(ARK, "The Ark", 580, 150, 760, 356, "animation:bob 9s ease-in-out infinite alternate")
            + crew + marks(96, 64, 400, 1584, 72) + foot())
    return r91.page(f"R{REV}A.dc.html", body, CSS)


# ── B The shore ───────────────────────────────────────────────────────────────────────────

def wave(y, amp, period, col, dur):
    half = period // 2
    d = f"M0 {y} q {half // 2} {-amp} {half} 0" + f" t {half} 0" * (3840 // half - 1) + " V 1080 H 0 Z"
    return (f'<svg aria-hidden="true" viewBox="0 0 3840 1080" style="position:absolute;left:0;top:0;width:3840px;height:1080px;animation:slide {dur}s linear infinite">'
            f'<path d="{d}" fill="{col}"></path></svg>')


def board_b():
    sky = field(GROUND, 0, 0, 1920, 240) + field(SURFACE, 0, 240, 1920, 150) + field(LINE, 0, 390, 1920, 120) + field(GOLD84, 0, 510, 1920, 120)
    slabs = "".join(f'<div aria-hidden="true" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{h}px;background:{INK};transform:rotate(-28deg)"></div>'
                    for x, y, w, h in [(1000, 440, 46, 150), (880, 500, 38, 124), (776, 552, 30, 100), (690, 596, 24, 78)])
    body = (sky + disc(1300, 620, 310, GOLD92, 10)
            + f'<div style="position:absolute;left:0;top:0;width:1920px;height:1080px;animation:cross 14s ease-in-out infinite alternate">{slabs}'
            + img(SHADOW, "The Squirrel", 1000, 150, 560, 560, "transform:rotate(14deg)") + "</div>"
            + wave(640, 26, 960, B44, 60) + wave(700, 22, 640, SURFACE, 44) + wave(770, 18, 960, GROUND, 30)
            + marks(96, 72, 600, 96, 330, 250) + foot())
    return r91.page(f"R{REV}B.dc.html", body, CSS)


# ── C The pilot ───────────────────────────────────────────────────────────────────────────

def board_c():
    names = ["Galleon", "Needle", "Sundial", "Hornet"]
    css, _ = r91.cycle(names, "pp")
    pilots = r91.stack([f'<img src="{PILOT[n]}" alt="{n}" style="width:528px;height:672px;object-fit:contain">' for n in names],
                       "pp", "left:0;top:0;width:528px;height:672px")
    calls = r91.stack([f'<span style="font-size:48px;line-height:1.1;text-transform:uppercase">{n}</span>' for n in names],
                      "pp", "left:0;top:0;width:520px;height:56px")
    body = (field(SURFACE) + disc(1250, 470, 430, GOLD84, 11)
            + img(SHADOW, "", 780, 30, 660, 660, "transform:rotate(14deg);opacity:.9;animation:bob 10s ease-in-out infinite alternate")
            + f'<div class="abs" style="left:1010px;top:190px;width:528px;height:672px">{pilots}</div>'
            + marks(96, 72, 520, 96, 300, 240)
            + f'<span class="lab abs" style="left:96px;top:560px;color:{INK}">Pilot</span>'
            + f'<div class="abs" style="left:96px;top:590px;width:520px;height:56px">{calls}</div>' + foot())
    return r91.page(f"R{REV}C.dc.html", body, CSS + css)


# ── D Three domains ───────────────────────────────────────────────────────────────────────

def board_d():
    bands = "".join(f'<div aria-hidden="true" style="position:absolute;left:{760 + i * 400}px;top:-40px;width:400px;height:1000px;background:{c};transform:skewX(-14deg)"></div>'
                    for i, c in enumerate([JADE, RUBY, GOLD]))
    hulls = "".join(img(CEL[n], f"The {n}", x, y, s, s, f"transform:rotate({t}deg);animation:bob {d}s ease-in-out infinite alternate")
                    for n, x, y, s, t, d in [("Dolphin", 800, 240, 330, 44, 7), ("Squirrel", 1175, 250, 370, 14, 9), ("Sparrow", 1570, 260, 340, 10, 8)])
    body = field(GROUND) + bands + hulls + marks(96, 330, 560, 96, 600, 240) + foot()
    return r91.page(f"R{REV}D.dc.html", body, CSS)


def board_notes():
    cols = [
        ("Asked", ["Shombith, 2026-11-12, on Rev 93: \"Okay can we move away from this neon design, create some good poster based mockups 4\"."]),
        ("Shared by all four", ["No glow. Flat colour fields, the painted pilots, the real hulls in three flat tones with an ink line.",
                                "Both logos as flat marks, cut from the neon ones. A real flat logo would need drawing.",
                                "Rev 91's bar on a dark foot. Motion is slow: a disc breathing, a hull drifting, the sea sliding."]),
        ("The four", ["A The crew (recommended): five pilots in a line before the Ark.",
                      "B The shore: a travel poster, the Squirrel crossing a low cell over a flat sea.",
                      "C The pilot: a character one-sheet; four pilots take turns.",
                      "D Three domains: Jade, Ruby and Gold bands with a hull in each."]),
        ("To know", ["The pilots are the 220 x 280 portraits enlarged; a poster this size wants them painted larger.",
                     "Gold here is a sun and a disc, not a purchase: a poster uses the families as paint. Say if that is wrong for the splash.",
                     "The hulls are each model's rest pose."]),
    ]
    body = (field(GROUND, 0, 0, 1920, 900)
            + f'<header class="abs" style="left:96px;top:64px;display:flex;flex-direction:column;gap:6px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:36px">The boot splash as a poster</span></header>'
            '<div class="abs" style="left:96px;top:200px;width:1728px;display:grid;grid-template-columns:repeat(4,1fr);gap:48px">')
    for head, items in cols:
        body += (f'<section style="display:flex;flex-direction:column;gap:16px;padding-top:16px;box-shadow:inset 0 1px 0 {LINE}"><span style="font-size:22px">{head}</span>'
                 + "".join(f'<span style="font-size:18px;line-height:1.5;color:{INK}">{t}</span>' for t in items) + "</section>")
    body += "</div>"
    return r91.page(f"R{REV}Notes.dc.html", body, h=900, loop=False)


if __name__ == "__main__":
    for fn in (board_a, board_b, board_c, board_d, board_notes):
        print("wrote", fn())
