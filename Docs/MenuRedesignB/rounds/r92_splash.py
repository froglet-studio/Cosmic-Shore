#!/usr/bin/env python3
"""Rev 92 boards: the boot splash and sign-in scene (Frogma tab "Loading Screens", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-12, on Rev 91 and a screenshot of today's splash: "This can be a good bootstrap splash and sign in
scene but we need something better than the image I shared with you. It cannot be texts, it needs to be froglet logo,
game name and something cool to show off the environment, create separate mockups for that."

Every board carries the two logos (Froglet Games, Cosmic Shore), Rev 91's moving sea and Rev 91's bar with the boot
status lines as its four steps, and no other words. They differ in how the environment is shown:

  A  The cell     one cell with three domain trails being laid around it, crystals inside (recommended)
  B  The gates    ring gates flying past the game's logo, two trails running out of the distance
  C  The arenas   three windows onto real arenas (the card-art renders at their own size), taking turns
  D  The garden   flora blooming around the logos

A, B and D are drawn in SVG and CSS as stand-ins for the live world; C is the game's own renders.

    python3 Docs/MenuRedesignB/rounds/r92_splash.py <scratch root> [rev]
"""
import importlib.util
import math
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "92"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r91", HERE / "r91_loading.py")
r91 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r91)

REV = int(_rev)
INK, DIM, LINE, GROUND, DEEP = r91.INK, r91.DIM, r91.LINE, r91.GROUND, r91.DEEP
JADE, RUBY, GOLD, PALE = "#21C0BD", "#BD1BC8", "#FF9B11", "#B9C9FA"
# The boot status channel's own lines, in the order they are raised (BootStatusBroadcaster, the auth controller).
r91.STEPS[:] = [("Connecting", 0, 22), ("Signing in", 18, 64), ("Loading profile", 52, 86), ("Creating session", 70, 100)]

LOGO, FROG = r91.blob("0fed24d7a640632e522d64fa6a1a9ebf"), r91.blob("76287c6c64c5ff9dc19b05448085b21c")
ENV = {n: r91.blob(i) for n, i in [("Waystation", "ad72df09cf292f6f7191d1601e216807"), ("Tapestry", "4f3d304f39cdf528a306832dccf14ad1"),
                                   ("Harvest", "685dcc7ce250db42e96011ff638a1fa3"), ("Glasshouse", "4645571a997eff39168b7328dc3a0c76"),
                                   ("Bloomrush", "09965967edb1aeb45d9db45603a1ccc9"), ("Kelpwood", "bbcbf1068f9f21fbdb0c8f46672388b7")]}

CSS = """@keyframes flow{0%{stroke-dashoffset:0}100%{stroke-dashoffset:-270}}
@keyframes pulse{0%{opacity:.35}100%{opacity:1}}
@keyframes ring{0%{transform:scale(.04);opacity:0}14%{opacity:.9}60%{opacity:.5}82%,100%{transform:scale(2.2);opacity:0}}
@keyframes pan{0%{transform:scale(1) translate(0,0)}100%{transform:scale(1.14) translate(-2%,1.5%)}}
@keyframes swap{0%,44%{opacity:0}56%,94%{opacity:1}100%{opacity:0}}
@keyframes bloom{0%{transform:scale(.8) rotate(0deg)}100%{transform:scale(1.08) rotate(24deg)}}
@keyframes drift{0%{transform:translate(-14px,10px)}100%{transform:translate(16px,-12px)}}
.burst{transform-box:fill-box;transform-origin:center}"""


def logos(cx, cy, w, frog_x, frog_y, frog_w=300, plate=True):
    """The game's logo centred on (cx, cy) and Froglet's, smaller. Images, not type."""
    h, fh = round(w * 391 / 942), round(frog_w * 447 / 900)
    back = (r91.glow(cx - w, cy - h, 2 * w, 2 * h, .0).replace("rgba(230,233,255,0.0)", "rgba(0,1,10,.78)").replace("rgba(230,233,255,0)", "rgba(0,1,10,0)")
            if plate else "")
    return (back + f'<img src="{LOGO}" alt="Cosmic Shore" style="position:absolute;left:{cx - w // 2}px;top:{cy - h // 2}px;width:{w}px;height:{h}px;object-fit:contain">'
            f'<img src="{FROG}" alt="Froglet Games" style="position:absolute;left:{frog_x}px;top:{frog_y}px;width:{frog_w}px;height:{fh}px;object-fit:contain">')


def svg(inner):
    return (f'<svg aria-hidden="true" viewBox="0 0 1920 1080" fill="none" style="position:absolute;left:0;top:0;width:1920px;height:1080px">'
            f'<defs><radialGradient id="cellG" cx="0.38" cy="0.32" r="0.75"><stop offset="0" stop-color="rgba(185,201,250,.40)"></stop>'
            f'<stop offset="0.55" stop-color="rgba(117,143,251,.16)"></stop><stop offset="1" stop-color="rgba(48,43,213,.12)"></stop></radialGradient></defs>{inner}</svg>')


def trail(rx, ry, rot, col, speed, front):
    d = f"M {rx} 0 A {rx} {ry} 0 0 1 {-rx} 0" if front else f"M {-rx} 0 A {rx} {ry} 0 0 1 {rx} 0"
    return (f'<g transform="rotate({rot})"><path d="{d}" stroke="{col}" stroke-width="{13 if front else 9}" stroke-dasharray="20 7" '
            f'opacity="{1 if front else .4}" style="animation:flow {speed}s linear infinite"></path></g>')


# ── A The cell ────────────────────────────────────────────────────────────────────────────

def board_a():
    orbits = [(470, 120, -18, JADE, 3.2), (420, 176, 30, RUBY, 4.1), (430, 104, 58, GOLD, 5.0)]
    crystals = "".join(f'<rect x="{x - 9}" y="{y - 9}" width="18" height="18" transform="rotate(45 {x} {y})" fill="{INK}" '
                       f'style="animation:pulse {2 + i * .7}s ease-in-out infinite alternate"></rect>'
                       for i, (x, y) in enumerate([(-90, -70), (110, 30), (-20, 140)]))
    cell = (f'<circle cx="930" cy="230" r="74" fill="url(#cellG)" stroke="rgba(185,201,250,.30)" stroke-width="1" opacity=".6"></circle>'
            f'<g style="animation:drift 14s ease-in-out infinite alternate"><g transform="translate(1350 452)">'
            + "".join(trail(rx, ry, rot, c, s, False) for rx, ry, rot, c, s in orbits)
            + f'<circle r="300" fill="url(#cellG)" stroke="rgba(185,201,250,.55)" stroke-width="2"></circle>'
            + f'<ellipse cx="-96" cy="-128" rx="120" ry="64" transform="rotate(-32 -96 -128)" fill="rgba(230,233,255,.07)"></ellipse>'
            + crystals + "".join(trail(rx, ry, rot, c, s, True) for rx, ry, rot, c, s in orbits) + "</g></g>")
    body = r91.sea() + svg(cell) + logos(446, 470, 700, 96, 64, plate=False) + r91.bar()
    return r91.page(f"R{REV}A.dc.html", body, CSS)


# ── B The gates ───────────────────────────────────────────────────────────────────────────

def board_b():
    rings = "".join(f'<div aria-hidden="true" style="position:absolute;left:510px;top:160px;width:900px;height:620px;box-sizing:border-box;border:3px solid {PALE};'
                    f'border-radius:50%;opacity:0;animation:ring 7s cubic-bezier(.5,0,.9,.45) {-i * .875:.3f}s infinite"></div>' for i in range(8))
    trails = (f'<path d="M 960 470 C 800 500 380 560 -40 760" stroke="{JADE}" stroke-width="12" stroke-dasharray="20 7" style="animation:flow 1.6s linear infinite"></path>'
              f'<path d="M 960 470 C 1100 430 1500 300 1960 60" stroke="{RUBY}" stroke-width="12" stroke-dasharray="20 7" style="animation:flow 2.1s linear infinite"></path>')
    body = r91.sea() + svg(trails) + rings + logos(960, 470, 640, 810, 40) + r91.bar()
    return r91.page(f"R{REV}B.dc.html", body, CSS)


# ── C The arenas ──────────────────────────────────────────────────────────────────────────

def board_c():
    pairs = [("Waystation", "Glasshouse"), ("Tapestry", "Bloomrush"), ("Harvest", "Kelpwood")]
    wins = ""
    for i, (a, b) in enumerate(pairs):
        x, w, h = 96 + i * 592, 544, 361
        img = "position:absolute;left:0;top:0;width:100%;height:100%;object-fit:cover"
        wins += (f'<figure style="position:absolute;left:{x}px;top:452px;width:{w}px;height:{h}px;margin:0">'
                 f'<div class="abs sl" aria-hidden="true" style="inset:0;background:{LINE}"></div>'
                 f'<div class="abs sl" style="inset:1px;overflow:hidden;background:{DEEP}">'
                 f'<img src="{ENV[a]}" alt="The {a} arena" style="{img};animation:pan 16s ease-in-out infinite alternate">'
                 f'<img src="{ENV[b]}" alt="The {b} arena" style="{img};opacity:0;animation:swap 16s linear {-i * 5.3:.1f}s infinite"></div></figure>')
    body = r91.sea() + logos(960, 290, 560, 810, 24, plate=False) + wins + r91.bar()
    return r91.page(f"R{REV}C.dc.html", body, CSS)


# ── D The garden ──────────────────────────────────────────────────────────────────────────

def burst(x, y, r, col, n, dur, rng):
    d = ""
    for k in range(n):
        a = 2 * math.pi * k / n + rng.uniform(-.05, .05)
        r1 = r * rng.uniform(.72, 1)
        d += f"M {x + math.cos(a) * r * .2:.0f} {y + math.sin(a) * r * .2:.0f} L {x + math.cos(a) * r1:.0f} {y + math.sin(a) * r1:.0f} "
    return (f'<g class="burst" style="animation:bloom {dur}s ease-in-out infinite alternate"><path d="{d}" stroke="{col}" stroke-width="2" opacity=".8"></path>'
            f'<circle cx="{x}" cy="{y}" r="{r * .2:.0f}" fill="rgba(0,4,31,.8)" stroke="{col}" stroke-width="2"></circle></g>')


def board_d():
    rng = random.Random(92)
    spots = [(250, 300, 170, JADE, 34, 6), (520, 720, 120, PALE, 28, 8), (1660, 290, 190, RUBY, 36, 7), (1440, 730, 130, PALE, 28, 9),
             (170, 660, 96, GOLD, 22, 5), (1790, 640, 104, PALE, 24, 6.5), (700, 150, 64, PALE, 18, 7.5)]
    body = (r91.sea() + svg("".join(burst(x, y, r, c, n, dur, rng) for x, y, r, c, n, dur in spots))
            + logos(960, 470, 640, 810, 150) + r91.bar())
    return r91.page(f"R{REV}D.dc.html", body, CSS)


# ── Notes ─────────────────────────────────────────────────────────────────────────────────

def board_notes():
    cols = [
        ("Asked", ["Shombith, 2026-11-12: \"we need something better than the image I shared with you. It cannot be texts, it needs to be froglet logo, game name and something cool to show off the environment, create separate mockups for that.\""]),
        ("Shared by all four", ["The two logos as images: Cosmic Shore, and Froglet Games smaller.",
                                "Rev 91's moving sea and Rev 91's bar. Its four steps are the boot's own status lines.",
                                "No other words. The sign-in, pilot name, consent and connection-lost windows sit over it as today."]),
        ("How each shows the environment", ["A The cell (recommended): one cell, three domain trails laid around it, crystals inside.",
                                            "B The gates: ring gates fly past the logo; two trails run out of the distance.",
                                            "C The arenas: three windows onto real arenas, taking turns. The only one made of the game's own pictures.",
                                            "D The garden: flora blooming around the logos."]),
        ("Against the build", ["Today: one still picture under a dim, the logo in it, LOADING over a small bar and one status line.",
                               "New: the environment moves. In the game A, B and D would be the live world behind the logos, not a drawing.",
                               "The bar is the same one as the loading screen (Rev 91 F), with a real percentage."]),
    ]
    body = (f'<div class="abs" aria-hidden="true" style="inset:0;background:linear-gradient(180deg,{GROUND} 0,{DEEP} 100%)"></div>'
            f'<header class="abs" style="left:96px;top:64px;display:flex;flex-direction:column;gap:6px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:36px">The boot splash and sign-in scene</span></header>'
            '<div class="abs" style="left:96px;top:200px;width:1728px;display:grid;grid-template-columns:repeat(4,1fr);gap:48px">')
    for head, items in cols:
        body += (f'<section style="display:flex;flex-direction:column;gap:16px;padding-top:16px;box-shadow:inset 0 1px 0 {LINE}"><span style="font-size:22px">{head}</span>'
                 + "".join(f'<span style="font-size:18px;line-height:1.5;color:{INK}">{t}</span>' for t in items) + "</section>")
    body += "</div>"
    return r91.page(f"R{REV}Notes.dc.html", body, h=900, loop=False)


if __name__ == "__main__":
    for fn in (board_a, board_b, board_c, board_d, board_notes):
        print("wrote", fn())
