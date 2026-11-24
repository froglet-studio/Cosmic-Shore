#!/usr/bin/env python3
"""Rev 119 boards: four new directions for the in-game voyage HUD, none of them the marquee (Frogma tab "Voyages playtest").

Shombith, 2026-11-20, on Rev 118: "Why are the texts so big. I do not want big texts what the hell", then "Yes I want
different mockups not just marquee, I didnt like marquee and how it looks in game".

So: the text is back at the game's own sizes (labels 15-18, toast lines 24 / 17, numbers 30-40 on the 1920 x 1080
frame), and the objective leaves the top centre. Four families, four screens each, so a pick can be a whole family or
a mix. Every family is also different from Rev 116's B-F (rail, console, corners, edges, blocks):

  A Tracker   an open-world quest tracker on the right: the objective as three short lines with their numbers;
              toasts a slim line in the lower third
  B On the Ark the Ark carries its own numbers: a lit plate over the hull holds the crystals aboard and the hull, in
              the world (LOCKED rule: a world fact shown in the world); the screen keeps one small chip top left for
              when the Ark is off screen; toasts a feed under the chip
  C Reticle   two thin arcs around the aim point: the hull on the left, the crystals on the right, small figures
              beside them; the route and the clock a hairline at the top; toasts a word line over the aim point
  D Card      one small card top left, everything in it (crystals, hull bar, route, clock); toasts slide out under it

  Raid    raiding a hostile cell
  Banked  the banked toast
  Attack  the Ark under attack
  Sail    the raid countdown

The game frame, the Ark, the fauna, the icons and the numbers are Rev 116's (r116_voyage_hud.py as a library).

    python3 Docs/MenuRedesignB/rounds/r119_voyage_hud_directions.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "119"
_spec = importlib.util.spec_from_file_location("r116", HERE / "r116_voyage_hud.py")
r116 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev, "lib"]
_spec.loader.exec_module(r116)
r114, r113, r100 = r116.r114, r116.r113, r116.r100

REV = int(_rev)
ROOT = r116.ROOT
W, H = 1920, 1080
INK, MUTE, SOFT, LINE, PANEL = r116.INK, r116.MUTE, r116.SOFT, r116.LINE, r116.PANEL
LIME, RED, RED2, FOCUS = r116.LIME, r116.RED, r116.RED2, r116.FOCUS
GOAL, BANKED, CARRY, HULLP = r116.GOAL, r116.BANKED, r116.CARRY, r116.HULLP
ark, fauna, gem, world = r116.ark, r116.fauna, r116.gem, r116.world
i_gem, i_ark, i_warn, i_clock = r116.i_gem, r116.i_ark, r116.i_warn, r116.i_clock
route_h, hbar, stack = r116.route_h, r116.hbar, r116.stack
VESSEL = r116.VESSEL

# The game's own sizes (canvas units on the 1920 x 1080 frame).
LABEL, BODY, NUM, TOAST, TOASTSUB = 15, 18, 34, 24, 17
SHADOW = "text-shadow:0 1px 3px rgba(0,1,10,.9);"

# What each screen shows, the same numbers on every family.
STATE = {
    "Raid": dict(banked=BANKED, carry=CARRY, hull=HULLP, hot=False, clock="0:18", toast=None),
    "Banked": dict(banked=30, carry=0, hull=HULLP, hot=False, clock="0:18",
                   toast=("Banked 7", "30 of 50 aboard. Space 3, Time 2, Mass 2.", "good")),
    "Attack": dict(banked=BANKED, carry=CARRY, hull=41, hot=True, clock="0:18",
                   toast=("Ark under attack", "3 raiders near the hull. Hunt what is biting it.", "threat")),
    "Sail": dict(banked=BANKED, carry=CARRY, hull=HULLP, hot=False, clock="0:05",
                 toast=("The Ark sails in 5", "Bank the +7 or lose it.", "event")),
}
MOOD = {"event": INK, "threat": RED2, "good": LIME}


def scene(key):
    """The game behind, with the in-world attacker marks on Attack (ArkAttackerMarks, already built)."""
    if key == "Attack":
        return world("attack") + diamond(1010, 330, 2.2) + diamond(700, 380, 1) + edge_arrow(600, 1)
    if key == "Sail":
        return world("sail")
    return world("raid")


def diamond(x, y, s=1.0):
    w, h = 34 * s, 52 * s
    return (f'<svg class="abs" style="left:{x - w / 2:.0f}px;top:{y - h:.0f}px" width="{w:.0f}" height="{h:.0f}" viewBox="0 0 34 52" aria-hidden="true">'
            '<path d="M17 52L0 20 17 0z" fill="#FF4B3A"></path><path d="M17 52L34 20 17 0z" fill="#B8231A"></path>'
            '<path d="M0 20h34" stroke="#FF8A7C" stroke-width="1.5"></path></svg>')


def edge_arrow(y=600, pips=1):
    x = W - 60
    p = "".join(f'<span style="width:10px;height:10px;background:{RED};transform:rotate(45deg)"></span>' for _ in range(pips))
    return (f'<svg class="abs" style="left:{x - 30}px;top:{y - 30}px" width="60" height="60" viewBox="0 0 80 80" aria-hidden="true">'
            '<path d="M10 28h34V12l30 28-30 28V52H10z" fill="#FF4B3A"></path></svg>'
            f'<div class="abs row" style="left:{x - 20}px;top:{y + 36}px;gap:8px">{p}</div>')


def hull_col(st):
    return RED2 if st["hot"] else INK


# ── A Tracker ───────────────────────────────────────────────────────────────────────────────────────────────────────
def a_line(icon, text, figure, col=INK, extra=""):
    return (f'<div class="row" style="gap:12px;align-items:center;{extra}">'
            f'<span style="width:22px;display:flex;justify-content:center">{icon}</span>'
            f'<span style="flex:1;font-size:{BODY}px;color:{col};{SHADOW}">{text}</span>'
            f'<span class="big" style="font-size:22px;color:{col};{SHADOW}">{figure}</span></div>')


def a_hud(key):
    st = STATE[key]
    hc = hull_col(st)
    carry = f' <span style="color:{LIME}">+{st["carry"]}</span>' if st["carry"] else ""
    lines = (a_line(i_gem(16), "Steal crystals", f'{st["banked"]} / {GOAL}{carry}')
             + a_line(i_ark(22, hc), "Under attack" if st["hot"] else "Keep the Ark above 10%", f'{st["hull"]}%', hc)
             + f'<div style="margin-left:34px">{hbar(296, 4, st["hull"], hc)}</div>'
             + a_line(i_clock(16, LIME if key == "Sail" else INK), "The Ark sails", st["clock"], LIME if key == "Sail" else INK))
    head = (f'<div class="row" style="gap:10px;padding-bottom:6px;box-shadow:inset 0 -1px 0 {LINE}">'
            f'<span class="hd" style="font-size:{LABEL}px;color:{SOFT}">Crystal Run</span>'
            f'<span style="flex:1"></span><span style="font-size:{LABEL}px;color:{MUTE}">Scarab Reef, 3 of 6</span></div>')
    hud = stack(W - 40 - 380, 250, head + lines, 380, 12,
                extra=f"padding:14px 16px;background:linear-gradient(270deg,rgba(0,4,31,.72),rgba(0,4,31,0));box-shadow:inset -2px 0 0 {hc if st['hot'] else LINE}")
    return hud + (a_toast(*st["toast"]) if st["toast"] else "")


def a_toast(head, sub, mood):
    col = MOOD[mood]
    return (f'<div class="abs tin" style="left:0;right:0;top:720px;display:flex;justify-content:center">'
            f'<div style="display:flex;flex-direction:column;align-items:center;gap:4px;padding:10px 60px;'
            f'background:linear-gradient(90deg,rgba(0,4,31,0),rgba(0,4,31,.8) 20%,rgba(0,4,31,.8) 80%,rgba(0,4,31,0))">'
            f'<span class="hd" style="font-size:{TOAST}px;color:{col}">{head}</span>'
            f'<span style="font-size:{TOASTSUB}px;color:{INK}">{sub}</span></div></div>')


# ── B On the Ark ────────────────────────────────────────────────────────────────────────────────────────────────────
ARK_AT = {"Raid": (960, 330), "Banked": (960, 330), "Attack": (760, 380), "Sail": (970, 340)}


def b_plate(key):
    """A lit plate in the world over the Ark: faceted, tilted, with the crystals aboard and the hull as a ring of
    segments. Drawn as a lit 3D object (a game object on the Ark, not screen UI)."""
    st = STATE[key]
    cx, top = ARK_AT[key]
    hc = RED if st["hot"] else "#9FE8FF"
    segs = 16
    lit = round(segs * st["hull"] / 100)
    seg = "".join(
        f'<span style="width:9px;height:14px;background:{hc if i < lit else "rgba(67,76,137,.55)"};'
        f'{"box-shadow:0 0 8px " + hc if i < lit else ""}"></span>' for i in range(segs))
    glow = "rgba(255,75,58,.35)" if st["hot"] else "rgba(0,212,255,.25)"
    return (f'<div class="abs" style="left:{cx - 170}px;top:{top - 120}px;width:340px;perspective:600px">'
            f'<div style="transform:rotateX(28deg);transform-origin:50% 100%;display:flex;flex-direction:column;align-items:center;gap:8px;'
            f'padding:12px 60px 14px;background:linear-gradient(180deg,rgba(40,70,140,.55),rgba(0,20,60,.65));'
            f'clip-path:polygon(14% 0,86% 0,100% 50%,86% 100%,14% 100%,0 50%);box-shadow:0 0 30px {glow}">'
            f'<div class="row" style="gap:10px;align-items:center">{i_gem(18, "#9FE8FF")}'
            f'<span class="big" style="font-size:{NUM}px;line-height:1;color:#E6F7FF;text-shadow:0 0 10px rgba(0,212,255,.8)">{st["banked"]}</span>'
            f'<span class="big" style="font-size:20px;color:#9FB4E6">/ {GOAL}</span></div>'
            f'<div class="row" style="gap:3px">{seg}</div></div></div>'
            f'<span class="abs" style="left:{cx - 1}px;top:{top - 8}px;width:2px;height:30px;background:linear-gradient({hc},rgba(0,0,0,0))"></span>')


def b_chip(key):
    st = STATE[key]
    hc = hull_col(st)
    carry = f'<span class="big" style="font-size:18px;color:{LIME}">+{st["carry"]}</span>' if st["carry"] else ""
    return (f'<div class="abs row" style="left:40px;top:36px;gap:16px;padding:8px 16px;background:{PANEL};box-shadow:inset 2px 0 0 {hc}">'
            f'<span class="row" style="gap:8px">{i_gem(14)}<span class="big" style="font-size:20px">{st["banked"]}/{GOAL}</span>{carry}</span>'
            f'<span class="row" style="gap:8px">{i_ark(22, hc)}<span class="big" style="font-size:20px;color:{hc}">{st["hull"]}%</span></span>'
            f'<span class="row" style="gap:8px">{i_clock(14, LIME if key == "Sail" else INK)}<span class="big" style="font-size:20px">{st["clock"]}</span></span></div>')


def b_toast(head, sub, mood):
    col = MOOD[mood]
    return (f'<div class="abs tin" style="left:40px;top:96px;display:flex;flex-direction:column;gap:3px;padding:8px 16px 10px;'
            f'max-width:520px;background:linear-gradient(90deg,rgba(0,4,31,.85),rgba(0,4,31,0));box-shadow:inset 2px 0 0 {col}">'
            f'<span class="hd" style="font-size:20px;color:{col}">{head}</span>'
            f'<span style="font-size:{TOASTSUB - 1}px;color:{INK}">{sub}</span></div>')


def b_hud(key):
    st = STATE[key]
    return b_plate(key) + b_chip(key) + (b_toast(*st["toast"]) if st["toast"] else "")


# ── C Reticle ───────────────────────────────────────────────────────────────────────────────────────────────────────
def c_hud(key):
    st = STATE[key]
    hc = RED2 if st["hot"] else INK
    cx, cy = W // 2, H // 2 + 40
    # The arcs: drawn as stroked halves; pct fills from the bottom.
    svg = (f'<svg class="abs" style="left:{cx - 100}px;top:{cy - 100}px" width="200" height="200" viewBox="0 0 200 200" aria-hidden="true">'
           f'{arc_path("left", st["hull"], hc)}{arc_path("right", 100 * st["banked"] / GOAL, INK)}'
           f'<path d="M96 100h8M100 96v8" stroke="{SOFT}" stroke-width="1.5"></path></svg>')
    carry = f' <span style="color:{LIME}">+{st["carry"]}</span>' if st["carry"] else ""
    figs = (f'<span class="abs big" style="left:{cx - 100 - 70}px;top:{cy - 12}px;width:60px;text-align:right;font-size:18px;color:{hc};{SHADOW}">{st["hull"]}%</span>'
            f'<span class="abs big" style="left:{cx + 110}px;top:{cy - 12}px;font-size:18px;{SHADOW}">{st["banked"]}/{GOAL}{carry}</span>')
    top = (f'<div class="abs row" style="left:0;right:0;top:28px;justify-content:center;gap:20px;font-size:{LABEL}px;color:{SOFT};{SHADOW}">'
           f'{route_h(260)}<span>Scarab Reef</span>'
           f'<span class="row" style="gap:6px;color:{LIME if key == "Sail" else SOFT}">{i_clock(14, LIME if key == "Sail" else SOFT)}{st["clock"]}</span></div>')
    out = svg + figs + top
    if st["toast"]:
        head, sub, mood = st["toast"]
        col = MOOD[mood]
        out += (f'<div class="abs tin" style="left:0;right:0;top:{cy - 190}px;display:flex;flex-direction:column;align-items:center;gap:2px;{SHADOW}">'
                f'<span class="hd" style="font-size:{TOAST}px;color:{col}">{head}</span>'
                f'<span style="font-size:{TOASTSUB}px;color:{INK}">{sub}</span></div>')
    return out


def arc_path(side, pct, col):
    """Half circle of radius 88 around (100,100), left or right, filled from the bottom up by pct."""
    import math
    def pt(a):
        return 100 + 88 * math.cos(a), 100 + 88 * math.sin(a)
    # Angles in SVG space (y down): bottom is 90 deg, top is 270 deg (-90). Leave a 30 deg gap top and bottom.
    if side == "left":
        a0, a1 = math.radians(120), math.radians(240)  # bottom-left to top-left, through 180
        sweep = 1
    else:
        a0, a1 = math.radians(60), math.radians(-60)   # bottom-right to top-right, through 0
        sweep = 0
    af = a0 + (a1 - a0) * pct / 100
    x0, y0 = pt(a0); x1, y1 = pt(a1); xf, yf = pt(af)
    track = f'<path d="M{x0:.1f} {y0:.1f} A88 88 0 0 {sweep} {x1:.1f} {y1:.1f}" fill="none" stroke="rgba(67,76,137,.6)" stroke-width="4"></path>'
    fill = f'<path d="M{x0:.1f} {y0:.1f} A88 88 0 0 {sweep} {xf:.1f} {yf:.1f}" fill="none" stroke="{col}" stroke-width="4"></path>'
    return track + fill


# ── D Card ──────────────────────────────────────────────────────────────────────────────────────────────────────────
def d_card(key):
    st = STATE[key]
    hc = hull_col(st)
    carry = f'<span class="big" style="font-size:18px;color:{LIME};margin-left:8px">+{st["carry"]}</span>' if st["carry"] else ""
    clk = LIME if key == "Sail" else INK
    return stack(40, 36, (
        f'<div class="row" style="gap:10px;align-items:baseline">{i_gem(16)}'
        f'<span class="big" style="font-size:{NUM}px;line-height:1">{st["banked"]}</span>'
        f'<span class="big" style="font-size:18px;color:{MUTE}">/ {GOAL}</span>{carry}<span style="flex:1"></span>'
        f'<span class="row" style="gap:6px">{i_clock(14, clk)}<span class="big" style="font-size:18px;color:{clk}">{st["clock"]}</span></span></div>'
        f'<div class="row" style="gap:10px;align-items:center">{i_ark(22, hc)}{hbar(200, 6, st["hull"], hc)}'
        f'<span class="big" style="font-size:16px;color:{hc}">{st["hull"]}%</span></div>'
        + (f'<span class="hd" style="font-size:13px;color:{RED2}">Under attack</span>' if st["hot"] else "")
        + f'<div class="row" style="gap:10px;align-items:center">{route_h(200)}'
        f'<span style="font-size:{LABEL - 1}px;color:{SOFT}">Scarab Reef</span></div>'), 320, 10,
        extra=f"padding:12px 16px;background:{PANEL};box-shadow:inset 0 2px 0 {hc if st['hot'] else LINE}")


def d_toast(head, sub, mood, top):
    col = MOOD[mood]
    return (f'<div class="abs tin" style="left:40px;top:{top}px;width:320px;display:flex;flex-direction:column;gap:3px;padding:10px 16px 12px;'
            f'background:rgba(0,4,31,.78);box-shadow:inset 3px 0 0 {col}">'
            f'<span class="hd" style="font-size:19px;color:{col}">{head}</span>'
            f'<span style="font-size:{TOASTSUB - 1}px;color:{INK};line-height:1.3">{sub}</span></div>')


def d_hud(key):
    st = STATE[key]
    top = 36 + (182 if st["hot"] else 158)
    return d_card(key) + (d_toast(*st["toast"], top) if st["toast"] else "")


FAMILIES = {
    "A": ("Tracker", "an open-world quest tracker on the right; toasts a slim line in the lower third", a_hud),
    "B": ("On the Ark", "the Ark carries its numbers on a lit plate in the world; one chip top left; toasts a feed under it", b_hud),
    "C": ("Reticle", "two thin arcs around the aim point, hull left and crystals right; the route a hairline on top", c_hud),
    "D": ("Card", "one small card top left with everything in it; toasts slide out under it", d_hud),
}
SCREENS = {"Raid": "raiding a hostile cell", "Banked": "the banked toast", "Attack": "the Ark under attack",
           "Sail": "the raid countdown"}


def board(fam, key):
    name, _, fn = FAMILIES[fam]
    return r100.page(f"Rev {REV} {fam}{key} - {name}: {SCREENS[key]}", scene(key) + fn(key) + VESSEL, r113.LOGIC)


def s_notes():
    rows = [
        ("Normal text again", f"Labels {LABEL}, body {BODY}, toast lines {TOAST} / {TOASTSUB}, numbers 18-{NUM} on the 1920 x 1080 frame: the game's own sizes. Rev 118's sized-up type is dropped."),
        ("No marquee", "The objective leaves the top centre in every family. Each is also unlike Rev 116's rail, console, corners, edges and blocks."),
        ("A Tracker", "The objective reads as a sentence with its number, like an open-world quest tracker: Steal crystals 23 / 50 +7, Keep the Ark above 10% 64%, The Ark sails 0:18. Toasts sit in the lower third."),
        ("B On the Ark", "The Ark wears its numbers: a lit plate over the hull (crystals aboard, the hull as lit segments) that turns red when hit. A world fact shown in the world. A small chip top left carries the same numbers for when the Ark is off screen."),
        ("C Reticle", "Hull and crystals as two thin arcs around where you aim, so your eyes never leave the centre. The route and clock are a hairline at the top. Toasts are words over the aim point, no box."),
        ("D Card", "Everything in one small card top left (crystals, carried, clock, hull bar, route), toasts slide out under it. The quietest of the four."),
        ("Threats stay in the world", "Red diamonds and the edge arrow are ArkAttackerMarks, already built. The vessel HUD keeps its corner."),
        ("Pick", "A whole family, or a mix (say the Ark plate from B with the card from D). The pick gets the full sixteen screens next."),
    ]
    return (f'<span class="abs hd" style="left:60px;top:50px;font-size:36px">Rev {REV} notes</span>'
            + stack(60, 130, "".join(
                f'<div style="display:flex;gap:30px;width:1800px"><span class="hd" style="font-size:20px;width:380px;flex:none;color:{INK}">{a}</span>'
                f'<span style="font-size:19px;color:{SOFT};line-height:1.45">{b}</span></div>' for a, b in rows), 1800, 22))


def notes():
    html = r100.page(f"Rev {REV} - notes: four directions without the marquee, at normal text sizes", s_notes(), r113.LOGIC)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


if __name__ == "__main__":
    n = 0
    for fam in FAMILIES:
        for key in SCREENS:
            (ROOT / f"R{REV}{fam}{key}.dc.html").write_text(board(fam, key), encoding="utf-8")
            n += 1
    (ROOT / f"R{REV}Notes.dc.html").write_text(notes(), encoding="utf-8")
    print("wrote", n + 1, "boards to", ROOT)
