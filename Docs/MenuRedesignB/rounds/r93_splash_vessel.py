#!/usr/bin/env python3
"""Rev 93 boards: the boot splash as today's art with a real vessel (Frogma tab "Loading Screens").

Shombith, 2026-11-12, after rejecting Rev 92: "I like the idea of real arena and also improve today's art replace it
with like a real vessel. Can you show me mockups for both?" and, on the offline arena renders, "Dont use these
pictures they are too bad, I will change them later too". So this round is the second direction only: today's splash
picture (a neon vessel laying a trail of prisms through a starfield, the logo top left) rebuilt with a REAL hull.

The hulls are the shipped FBX meshes (Squirrel, Sparrow, Dolphin) projected and drawn in the art's own style: blue
faces, glowing cyan creases. The prisms are laid behind the vessel one by one on the loop. The real-arena direction
waits for arena pictures he accepts.

  A  Squirrel      today's composition, the real Squirrel laying a Ruby trail (recommended)
  B  The squad     three hulls in formation, a trail each in the three domain colours
  C  The fleet     one hull at a time in the same place, taking turns

    python3 Docs/MenuRedesignB/rounds/r93_splash_vessel.py <scratch root> [rev]
"""
import importlib.util
import math
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "93"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r92", HERE / "r92_splash.py")
r92 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r92)          # sets the bar's steps to the boot status lines
r91 = r92.r91

REV = int(_rev)
JADE, RUBY, GOLD, INK = r92.JADE, r92.RUBY, r92.GOLD, r91.INK
RGB = {JADE: "33,192,189", RUBY: "189,27,200", GOLD: "255,155,17"}
VESSEL = {"Squirrel": r91.blob("5ea764a00032fb32c2ec6079465c122c"), "Sparrow": r91.blob("a0c39880ccb847030a30a36177924314"),
          "Dolphin": r91.blob("24be7f6e1dd079e05cf6ade520d628b9")}

CSS = """@keyframes lay{0%,6%{opacity:0;transform:scale(.6)}12%,88%{opacity:1;transform:scale(1)}96%,100%{opacity:0;transform:scale(1)}}
@keyframes fly{0%{transform:translate(-14px,10px)}100%{transform:translate(16px,-12px)}}
@keyframes twinkle{0%{opacity:.25}100%{opacity:.9}}"""


def stars(seed, n=54):
    rng = random.Random(seed)
    out = ""
    for i in range(n):
        x, y, s = rng.randint(20, 1900), rng.randint(20, 860), rng.choice([2, 2, 3, 4])
        out += (f'<rect x="{x}" y="{y}" width="{s * 2}" height="{s}" fill="rgba(185,201,250,.7)" '
                f'style="animation:twinkle {rng.uniform(2, 5):.1f}s ease-in-out {-rng.uniform(0, 5):.1f}s infinite alternate"></rect>')
    return f'<svg aria-hidden="true" viewBox="0 0 1920 1080" style="position:absolute;left:0;top:0;width:1920px;height:1080px">{out}</svg>'


def trail(x0, y0, x1, y1, col, n=5, w=120, l0=250, l1=380, loop=8.0):
    """Prisms laid from (x0,y0) toward the vessel at (x1,y1): slabs across the flight path, the newest nearest the hull."""
    ang = math.degrees(math.atan2(y1 - y0, x1 - x0))
    out = ""
    for i in range(n):
        t = i / max(n - 1, 1)
        cx, cy, ln = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, l0 + (l1 - l0) * t
        out += (f'<div aria-hidden="true" style="position:absolute;left:{cx - w / 2:.0f}px;top:{cy - ln / 2:.0f}px;width:{w}px;height:{ln:.0f}px;transform:rotate({ang:.1f}deg)">'
                f'<div style="width:100%;height:100%;box-sizing:border-box;border:4px solid {col};background:rgba({RGB[col]},.24);opacity:0;'
                f'animation:lay {loop}s ease-out {-(loop - i * loop * .09):.2f}s infinite"></div></div>')
    return out


def hull(name, cx, cy, size, tilt=14, dur=7):
    return (f'<div style="position:absolute;left:{cx - size // 2}px;top:{cy - size // 2}px;width:{size}px;height:{size}px;animation:fly {dur}s ease-in-out infinite alternate">'
            f'<img src="{VESSEL[name]}" alt="The {name}" style="width:100%;height:100%;object-fit:contain;transform:rotate({tilt}deg)"></div>')


def ground(seed):
    return r91.sea() + stars(seed)


def logos():
    return r92.logos(446, 250, 640, 96, 440, frog_w=260, plate=False)


def board_a():
    body = (ground(1) + trail(540, 820, 1150, 470, RUBY) + hull("Squirrel", 1390, 400, 660) + logos() + r91.bar())
    return r91.page(f"R{REV}A.dc.html", body, CSS)


def board_b():
    body = (ground(2)
            + trail(880, 330, 1200, 210, JADE, n=4, w=70, l0=120, l1=170) + hull("Dolphin", 1380, 200, 320, tilt=44, dur=6)
            + trail(640, 820, 1130, 540, RUBY, n=5, w=110, l0=220, l1=320) + hull("Squirrel", 1350, 470, 560)
            + trail(1180, 840, 1500, 720, GOLD, n=4, w=80, l0=140, l1=200) + hull("Sparrow", 1700, 660, 400, tilt=10, dur=8)
            + logos() + r91.bar())
    return r91.page(f"R{REV}B.dc.html", body, CSS)


def board_c():
    names = ["Squirrel", "Sparrow", "Dolphin"]
    css, _ = r91.cycle(names, "hv")
    tilts = {"Squirrel": 14, "Sparrow": 10, "Dolphin": 44}
    turns = r91.stack([f'<img src="{VESSEL[n]}" alt="The {n}" style="width:660px;height:660px;object-fit:contain;transform:rotate({tilts[n]}deg)">' for n in names],
                      "hv", "left:0;top:0;width:660px;height:660px")
    body = (ground(3) + trail(540, 820, 1150, 470, RUBY)
            + f'<div style="position:absolute;left:1050px;top:60px;width:720px;height:720px;animation:fly 7s ease-in-out infinite alternate">{turns}</div>'
            + logos() + r91.bar())
    return r91.page(f"R{REV}C.dc.html", body, CSS + css)


def board_notes():
    cols = [
        ("Asked", ["Shombith, 2026-11-12, on Rev 92: \"I dont like the direction already\". Then: \"I like the idea of real arena and also improve today's art replace it with like a real vessel. Can you show me mockups for both?\""]),
        ("This round", ["Today's splash picture rebuilt: a starfield over the sea, a neon vessel laying a trail of prisms, the logo top left, Froglet's logo under it.",
                        "The vessel is real: the shipped hull mesh, drawn in the art's own blue faces and glowing cyan creases.",
                        "The prisms are laid one by one behind it. The bar is Rev 91's, with the boot status lines."]),
        ("The three", ["A Squirrel (recommended): today's composition with the real Squirrel.",
                       "B The squad: Dolphin, Squirrel and Sparrow in formation, trails in Jade, Ruby and Gold.",
                       "C The fleet: Squirrel, Sparrow and Dolphin take turns in the same place."]),
        ("Not here", ["The real-arena direction: the offline arena pictures were turned down (\"they are too bad, I will change them later\"), so it waits for arena pictures from the editor.",
                      "Today's art has a prism structure on the left; these boards leave it out."]),
    ]
    body = (f'<div class="abs" aria-hidden="true" style="inset:0;background:linear-gradient(180deg,{r91.GROUND} 0,{r91.DEEP} 100%)"></div>'
            f'<header class="abs" style="left:96px;top:64px;display:flex;flex-direction:column;gap:6px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:36px">The boot splash with a real vessel</span></header>'
            '<div class="abs" style="left:96px;top:200px;width:1728px;display:grid;grid-template-columns:repeat(4,1fr);gap:48px">')
    for head, items in cols:
        body += (f'<section style="display:flex;flex-direction:column;gap:16px;padding-top:16px;box-shadow:inset 0 1px 0 {r91.LINE}"><span style="font-size:22px">{head}</span>'
                 + "".join(f'<span style="font-size:18px;line-height:1.5;color:{INK}">{t}</span>' for t in items) + "</section>")
    body += "</div>"
    return r91.page(f"R{REV}Notes.dc.html", body, h=900, loop=False)


if __name__ == "__main__":
    for fn in (board_a, board_b, board_c, board_notes):
        print("wrote", fn())
