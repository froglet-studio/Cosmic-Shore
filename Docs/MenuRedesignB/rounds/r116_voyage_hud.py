#!/usr/bin/env python3
"""Rev 116 boards: the in-game voyage UI, six ways (Frogma tab "Voyages playtest", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-17, after playing the round-3 build: "the In game UI is too stupid and old like we need to work more
on that for sure. I need new mockups lets say 6 of each in game screen including toasts, the toasts should come in the
designated toast spot as it comes in many arcade games."

Six screens, one board each, six options per board (A-F). A letter is one design family across every board, so a
pick can be a whole family ("all of D") or a mix ("HUD from A, pause from D"):

  Hud      flying in a raided cell: the goal count, the Ark's hull on the left, the cell, the anchor timer
  Attack   the Ark under attack: a kaiju-sized raider on the hull, the hull under 30% and the hold leaking
  Toasts   the toast spot: one fixed place per family, every kind of toast drawn in it
  Sail     the raid countdown: the Ark sails in 5, bank what you carry
  Pause    the pause menu with the goal met: Resume, Complete and set sail, Settings, Leave voyage
  Score    the end scoreboard: the Ark docked, a row per seat, rewards, Finish and Venture further

The families:
  A Marquee   arcade top centre: the count is the score, the toast slot sits right under it (recommended)
  B Rail      one column on the left: the hull first, everything else under it; toasts slide out of the rail
  C Console   a racing game's bottom console, the route a line across the top; toasts in the lower third
  D Corners   a shooter's corners: hull top left, crystals top right; toasts a feed under the crystals
  E Edges     two arcs on the screen's edges, hull and goal; toasts are big words at top centre, no box
  F Blocks    a fighting game's top bar, segmented; toasts a slanted banner that slams in at mid left

Every word is the authored text already in the voyage assets (ToastArkUnderAttack, ObjectiveStripAnchor, ...), the
game frame behind is the same real frame of the game Rev 114 used, fauna are lit in the world (no rings or arrows on
the screen), the vessel's own HUD keeps its corner (bottom right), and nothing moves at rest.

    python3 Docs/MenuRedesignB/rounds/r116_voyage_hud.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "116"
_spec = importlib.util.spec_from_file_location("r114", HERE / "r114_voyages.py")
r114 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev]
_spec.loader.exec_module(r114)
r100, r113 = r114.r100, r114.r113

REV = int(_rev)
ROOT = r114.ROOT
G, ARK, HULL, FACE = r114.G, r114.ARK, r114.HULL, r114.FACE
flower, ark, hullimg, fauna, gem = r113.flower, r114.ark, r114.hull, r114.fauna, r114.gem

W, H, GAP, LABEL, UNDER, HEAD = 1920, 1080, 80, 64, 230, 150
BW = 3 * W + 2 * GAP
CELL_H = LABEL + H + UNDER
BH = HEAD + 2 * CELL_H + GAP

# colours (style guide): ink, muted, line, panel, call to action (Lime), threat, Ruby (no way back), focus
INK, MUTE, SOFT, LINE = "#E6E9FF", "#747BAD", "#B8C0F0", "#434C89"
PANEL = "rgba(0,4,31,.84)"
LIME, RED, RED2, RUBY, FOCUS = "#99FF80", "#FF4B3A", "#FB8E7E", "#A600FF", "#00D4FF"
CP = "font-family:'Chakra Petch',sans-serif;"

r100.HELMET = r100.HELMET.replace("</style>", (
    ".scr{position:absolute;overflow:hidden;background:#00010A}\n"
    ".big{font-family:'Chakra Petch',sans-serif;font-weight:700;letter-spacing:.06em;font-variant-numeric:tabular-nums}\n"
    ".hd{font-family:'Chakra Petch',sans-serif;font-weight:700;letter-spacing:.16em;text-transform:uppercase}\n"
    ".skew{clip-path:polygon(28px 0,100% 0,calc(100% - 28px) 100%,0 100%)}\n"
    ".tin{animation:tin .5s cubic-bezier(.2,.9,.25,1) both}\n"
    "@keyframes tin{from{opacity:0;transform:translateY(-14px) scale(.96)}to{opacity:1;transform:none}}\n"
    ".slot{position:absolute;box-shadow:inset 0 0 0 1px rgba(0,212,255,.55);background:repeating-linear-gradient(135deg,rgba(0,212,255,.05) 0 10px,rgba(0,0,0,0) 10px 20px)}\n"
    "</style>"))

# ── the voyage's state on every board (one run, the same numbers everywhere) ─────────────────────────────────────────
GOAL, BANKED, CARRY, HULLP, BREACH, LEAK = 50, 23, 7, 64, 10, 30
CELLS = ["Home", "Garland", "Scarab Reef", "Tollmoor", "Brine Gate", "The Maw"]
NOW = 2  # Scarab Reef, cell 3 of 6
SQUAD = [("Sparrow", "Ilia Silvergate", "You", 14, "Space"), ("Manta", "Tamako Deephook", "AI", 13, "Mass"),
         ("Dolphin", "Veliane Crestward", "AI", 12, "Time"), ("Serpent", "Ekora Hollowbrook", "AI", 11, "Charge")]

TOASTS = {  # kind: (headline, under, colour) - the authored ToastXxx / ToastXxxSub lines, with their tokens filled
    "threat": ("ARK UNDER ATTACK", "2 raiders near the hull. Hunt what is biting it.", RED2),
    "leak": ("THE HOLD IS LEAKING", "The hull is below 30%. Banked crystals spill one by one: guard the Ark and grab them back.", RED2),
    "event": ("ENTERING CELL 3. HOSTILE.", "More crystals here. Its fauna are hunting the Ark.", INK),
    "goal": ("50 CRYSTALS. BRING THEM HOME.", "Pause and choose Complete and set sail, or keep raiding for more.", INK),
    "banked": ("BANKED 7", "30 of 50 aboard.", INK),
    "sail": ("THE ARK SAILS IN 5", "Bank what you carry or lose it.", INK),
}


# ── icons (inline stroke SVG, never emoji) ───────────────────────────────────────────────────────────────────────────
def i_gem(s=22, c=INK):
    return (f'<svg width="{s}" height="{s * 1.3:.0f}" viewBox="0 0 20 26" fill="none" stroke="{c}" stroke-width="2" aria-hidden="true">'
            '<path d="M10 1l8 12-8 12-8-12z"></path><path d="M2 13h16" opacity=".5"></path></svg>')


def i_ark(s=34, c=INK):
    return (f'<svg width="{s}" height="{s * .5:.0f}" viewBox="0 0 40 20" fill="none" stroke="{c}" stroke-width="2" aria-hidden="true">'
            '<path d="M1 12h38l-6 7H7z"></path><path d="M10 12V6h14l5 6"></path><path d="M15 6V2"></path></svg>')


def i_warn(s=22, c=RED2):
    return (f'<svg width="{s}" height="{s}" viewBox="0 0 22 22" fill="none" stroke="{c}" stroke-width="2" aria-hidden="true">'
            '<path d="M11 2l10 18H1z"></path><path d="M11 9v5M11 16v1"></path></svg>')


def i_flag(s=22, c=INK):
    return (f'<svg width="{s}" height="{s}" viewBox="0 0 22 22" fill="none" stroke="{c}" stroke-width="2" aria-hidden="true">'
            '<path d="M4 21V2M4 3h13l-3 4 3 4H4"></path></svg>')


def i_clock(s=22, c=INK):
    return (f'<svg width="{s}" height="{s}" viewBox="0 0 22 22" fill="none" stroke="{c}" stroke-width="2" aria-hidden="true">'
            '<circle cx="11" cy="11" r="9"></circle><path d="M11 6v5l3 2"></path></svg>')


ICON = {"threat": i_warn, "leak": i_warn, "event": i_flag, "goal": lambda s=22, c=INK: i_gem(s * .8, c),
        "banked": lambda s=22, c=INK: i_gem(s * .8, c), "sail": i_clock}


def kc(k):
    return f'<span class="kc">{k}</span>'


# ── the game behind ─────────────────────────────────────────────────────────────────────────────────────────────────
VESSEL = ('<div class="abs" style="right:40px;bottom:40px;width:380px;height:130px;box-shadow:inset 0 0 0 1px rgba(67,76,137,.5);display:flex;'
          'align-items:center;justify-content:center"><span class="cap2" style="color:#747BAD">Vessel HUD, as today</span></div>')


def world(kind):
    g = r114.ground
    if kind == "raid":
        return (g(G["approach"], "50% 50%", .12) + ark(690, 330, 540) + fauna(1210, 400, .8) + fauna(560, 660, .7)
                + gem(1080, 620) + gem(1130, 560, .8) + gem(820, 700, .9) + gem(1400, 520, .7))
    if kind == "attack":
        kaiju = (fauna(1010, 420, 5.2) + '<span class="abs" style="left:780px;top:380px;width:420px;height:200px;border-radius:50%;'
                 'background:radial-gradient(ellipse,rgba(255,75,58,.30),rgba(255,75,58,0) 70%)"></span>')
        spill = "".join(gem(x, y, .7) for x, y in ((640, 600), (600, 660), (700, 700), (560, 560)))
        return g(G["through"], "50% 50%", .18) + ark(620, 360, 560, -4) + kaiju + fauna(700, 420, 1.1) + spill
    if kind == "sail":
        return g(G["cell"], "50% 50%", .14) + ark(720, 340, 500, 3) + gem(1180, 640) + gem(560, 600, .8) + fauna(1300, 360, .7)
    return ('<img src="' + G["approach"] + '" alt="" style="position:absolute;inset:-30px;width:1980px;height:1140px;object-fit:cover;filter:blur(14px)">'
            '<div class="abs" style="inset:0;background:rgba(0,1,10,.62)"></div>' + ark(700, 360, 520, 0, .55))


def dock_world():
    return ('<img src="' + G["heart"] + '" alt="" style="position:absolute;inset:-30px;width:1980px;height:1140px;object-fit:cover;filter:blur(10px)">'
            '<div class="abs" style="inset:0;background:rgba(0,1,10,.66)"></div>')


# ── small shared pieces ─────────────────────────────────────────────────────────────────────────────────────────────
def vbar(x, y, w, h, pct, col=INK, marks=True):
    """A vertical hull bar with the breach line (red) and the leak line (dashed)."""
    fill = h * pct / 100
    m = ""
    if marks:
        m = (f'<span class="abs" style="left:-6px;right:-6px;bottom:{h * BREACH / 100:.0f}px;height:2px;background:{RED}"></span>'
             f'<span class="abs" style="left:-6px;right:-6px;bottom:{h * LEAK / 100:.0f}px;height:0;border-top:2px dashed {MUTE}"></span>')
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;background:rgba(67,76,137,.4)">'
            f'<span class="abs" style="left:0;right:0;bottom:0;height:{fill:.0f}px;background:{col}"></span>{m}</div>')


def hbar(w, h, pct, col=INK, marks=True, track="rgba(67,76,137,.45)"):
    m = ""
    if marks:
        m = (f'<span class="abs" style="top:-4px;bottom:-4px;left:{BREACH}%;width:2px;background:{RED}"></span>'
             f'<span class="abs" style="top:-4px;bottom:-4px;left:{LEAK}%;width:0;border-left:2px dashed {MUTE}"></span>')
    return (f'<div style="position:relative;width:{w}px;height:{h}px;background:{track}">'
            f'<span class="abs" style="left:0;top:0;bottom:0;width:{pct}%;background:{col}"></span>{m}</div>')


def route_h(w, now=NOW, labels=False, col=INK):
    n = len(CELLS)
    out = [f'<div style="position:relative;width:{w}px;height:{40 if labels else 14}px">'
           f'<span class="abs" style="left:6px;right:6px;top:6px;height:2px;background:rgba(67,76,137,.8)"></span>'
           f'<span class="abs" style="left:6px;top:6px;height:2px;width:{(now + .45) / (n - 1) * (w - 12):.0f}px;background:{col}"></span>']
    for i, name in enumerate(CELLS):
        x = i / (n - 1) * (w - 14)
        st = (f"background:{col}" if i < now else f"background:#00041F;box-shadow:inset 0 0 0 3px {col}" if i == now
              else f"background:#00041F;box-shadow:inset 0 0 0 2px {SOFT if i == n - 1 else MUTE}")
        out.append(f'<span class="abs" style="left:{x:.0f}px;top:0;width:14px;height:14px;border-radius:50%;{st}"></span>')
        if labels:
            out.append(f'<span class="abs" style="left:{x + 7:.0f}px;top:22px;transform:translateX(-50%);font-size:13px;white-space:nowrap;'
                       f'color:{INK if i == now else MUTE}">{name}</span>')
    out.append(f'<span class="abs" style="left:{(now + .45) / (n - 1) * (w - 14) - 9:.0f}px;top:-10px">{i_ark(22, col)}</span>')
    return "".join(out) + "</div>"


def stack(x, y, inner, w=None, gap=10, align="flex-start", extra=""):
    ww = f"width:{w}px;" if w else ""
    return (f'<div class="abs" style="left:{x}px;top:{y}px;{ww}display:flex;flex-direction:column;gap:{gap}px;align-items:{align};{extra}">'
            f'{inner}</div>')


def seats_rows(w=900, size=17, faces=True):
    rows = []
    for v, p, who, n, el in SQUAD:
        rows.append(f'<div class="row" style="gap:16px;height:64px;padding:0 16px;background:rgba(0,4,31,.6);box-shadow:inset 2px 0 0 {INK if who == "You" else LINE}">'
                    + (r114.face(p, 42, 50) if faces else "")
                    + f'<img class="hull" src="{HULL[v]}" alt="{v}" style="width:90px;height:44px">'
                    f'<span style="display:flex;flex-direction:column;gap:2px;flex:1"><span style="font-size:{size}px">{p}</span>'
                    f'<span class="cap2">{v}  ·  {who}</span></span>{flower(el, 20)}'
                    f'<span class="big" style="font-size:28px;width:70px;text-align:right">{n}</span></div>')
    return f'<div style="width:{w}px;display:flex;flex-direction:column;gap:8px">' + "".join(rows) + "</div>"


REWARDS = [("Crystals banked", "50"), ("Ark hull", "58%"), ("Cells raided", "4 of 6"), ("Time", "6:42")]


def buttons_pause(direction="column", w=420, big=True, gap=12):
    h = "height:64px;font-size:20px;" if big else ""
    return (f'<div style="display:flex;flex-direction:{direction};gap:{gap}px;{"width:" + str(w) + "px;" if direction == "column" else ""}">'
            f'<button class="btn def" type="button" style="{h}justify-content:flex-start;width:100%">{kc("B")}Resume</button>'
            f'<button class="btn cta" type="button" style="{h}justify-content:flex-start;width:100%">{kc("A")}Complete and set sail</button>'
            f'<button class="btn def" type="button" style="{h}justify-content:flex-start;width:100%">Settings</button>'
            f'<button class="btn def" type="button" style="{h}justify-content:flex-start;width:100%;color:#B8C0F0">Leave voyage</button></div>')


def end_buttons(gap=16):
    return (f'<div class="row" style="gap:{gap}px">'
            f'<button class="btn def" type="button" style="height:64px;font-size:20px">{kc("B")}Finish</button>'
            f'<button class="btn cta" type="button" style="height:64px;font-size:20px">{kc("A")}Venture further  ·  goal 60</button></div>')


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# A  Marquee: the count at top centre is the score; the toast slot is the band right under it
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def a_hull_rail(pct, hot=False):
    col = RED2 if hot else INK
    return (f'<div class="abs" style="left:40px;top:300px;width:90px;display:flex;flex-direction:column;align-items:center;gap:10px">'
            f'{i_ark(40, col)}<span class="big" style="font-size:30px;color:{col}">{pct}<span style="font-size:16px">%</span></span></div>'
            + vbar(75, 400, 20, 380, pct, col) + f'<span class="abs cap2" style="left:40px;top:796px;color:{col}">Ark hull</span>')


def a_top(banked=BANKED, under=True, col=INK, timer="0:18"):
    sub = (f'<div class="row" style="gap:22px;font-size:17px;color:{SOFT}"><span>Cell 3 of 6  ·  Scarab Reef  ·  <span style="color:{RED2}">hostile</span></span>'
           f'<span class="row" style="gap:8px">{i_clock(18, SOFT)}Raid {timer}</span></div>') if under else ""
    return stack(0, 26, (f'<div class="row" style="gap:16px">{i_gem(30, col)}<span class="big" style="font-size:72px;line-height:1;color:{col}">{banked}</span>'
                         f'<span class="big" style="font-size:30px;color:{MUTE};align-self:flex-end;margin-bottom:8px">/ {GOAL}</span></div>'
                         f'{route_h(520, col=INK)}{sub}'), W, 10, "center")


def a_toast(kind, top=228):
    head, sub, col = TOASTS[kind]
    bg = "rgba(82,5,3,.9)" if col == RED2 else "rgba(0,4,31,.9)"
    return (f'<div class="abs tin" style="left:0;right:0;top:{top}px;display:flex;justify-content:center">'
            f'<div class="skew" style="display:flex;align-items:center;gap:22px;padding:14px 64px;background:{bg};box-shadow:inset 0 -3px 0 {col}">'
            f'{ICON[kind](30, col)}<span style="display:flex;flex-direction:column;gap:4px"><span class="hd" style="font-size:32px;color:{col}">{head}</span>'
            f'<span style="font-size:17px;color:{INK};max-width:640px">{sub}</span></span></div></div>')


def a_carry():
    return (f'<div class="abs" style="left:0;right:0;top:640px;display:flex;justify-content:center"><span class="row" style="gap:10px;padding:8px 16px;background:rgba(0,4,31,.7);font-size:17px">'
            f'{i_gem(16)}Carrying {CARRY}. Bank them at the Ark.</span></div>')


def a_hud():
    return world("raid") + a_top() + a_hull_rail(HULLP) + a_carry() + VESSEL


def a_attack():
    return world("attack") + a_top() + a_hull_rail(27, True) + a_toast("leak") + VESSEL


def a_toasts():
    return world("raid") + a_top() + a_hull_rail(HULLP) + a_toast("threat") + VESSEL


def a_sail():
    ring = (f'<div class="abs" style="left:810px;top:330px;width:300px;height:300px">'
            f'<svg width="300" height="300" viewBox="0 0 300 300" aria-hidden="true"><circle cx="150" cy="150" r="140" fill="rgba(0,4,31,.55)" stroke="rgba(67,76,137,.6)" stroke-width="8"></circle>'
            f'<circle cx="150" cy="150" r="140" fill="none" stroke="{INK}" stroke-width="8" stroke-dasharray="147 880" transform="rotate(-90 150 150)"></circle></svg>'
            f'<span class="abs big" style="inset:0;display:flex;align-items:center;justify-content:center;font-size:150px">5</span></div>')
    return world("sail") + a_top(timer="0:05") + a_hull_rail(HULLP) + a_toast("sail") + ring + a_carry() + VESSEL


def a_pause():
    left = stack(560, 230, (f'<span class="cap2">Paused  ·  Escort the Ark</span>'
                            f'<span class="hd" style="font-size:44px;color:{INK}">Goal met</span>'
                            f'<span style="font-size:20px;color:{SOFT};max-width:420px;line-height:1.45">50 of 50 aboard. Set sail for home now, or keep raiding for more.</span>'
                            f'<div style="height:16px"></div>{buttons_pause()}'), 440, 12)
    right = stack(1080, 230, ('<span class="cap2">This voyage</span>' + "".join(
        f'<div class="row" style="justify-content:space-between;width:300px;padding:10px 0;box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)"><span style="color:{SOFT}">{a}</span><span class="big" style="font-size:24px">{b}</span></div>'
        for a, b in REWARDS) + f'<div style="margin-top:18px">{route_h(300, 3, True)}</div>'), 300, 4)
    return world("pause") + left + right


def a_score():
    head = stack(0, 70, (f'<span class="cap2">Escort the Ark  ·  Docked at Home</span><span class="hd" style="font-size:64px">The Ark is home</span>'
                         f'<div class="row" style="gap:16px">{i_gem(34, INK)}<span class="big" style="font-size:96px;line-height:1;color:{INK}">50</span>'
                         f'<span style="font-size:22px;color:{SOFT};align-self:flex-end;margin-bottom:14px">crystals banked</span></div>'
                         f'<div class="row" style="gap:26px;font-size:19px">{flower("Space", 22)} 17 {flower("Mass", 22)} 14 {flower("Time", 22)} 11 {flower("Charge", 22)} 8</div>'), W, 14, "center")
    rows = stack(510, 470, seats_rows(900), 900)
    rew = (f'<div class="abs row" style="left:510px;top:780px;width:900px;gap:12px">' + "".join(
        f'<div style="flex:1;padding:12px 14px;background:rgba(0,4,31,.7)"><span class="cap2">{a}</span><div class="big" style="font-size:26px;margin-top:4px">{b}</div></div>'
        for a, b in (("Crew trust", "+120"), ("Pilot XP", "+340"), ("Colour shards", "+6"))) + "</div>")
    return dock_world() + ark(1360, 120, 420, 0, .5) + head + rows + rew + stack(0, 930, end_buttons(), W, 0, "center")


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# B  Rail: one column on the left, the hull first; the toast slides out of the rail
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def b_rail(pct=HULLP, hot=False, banked=BANKED, timer="0:18", timer_big=False):
    col = RED2 if hot else INK
    cells = "".join(
        f'<div class="row" style="gap:12px;font-size:15px;color:{INK if i == NOW else MUTE}">'
        f'<span style="width:10px;height:10px;border-radius:50%;{"background:" + INK if i < NOW else "box-shadow:inset 0 0 0 2px " + (INK if i == NOW else INK if i == 5 else MUTE)}"></span>'
        f'{name}{"  ·  hostile" if i == NOW else ""}</div>' for i, name in enumerate(CELLS))
    t = (f'<div class="row" style="gap:12px">{i_clock(26)}<span class="big" style="font-size:{90 if timer_big else 30}px;line-height:1">{timer}</span></div>'
         f'<span class="cap2">{"The Ark sails" if timer_big else "Raid: the Ark sails in"}</span>')
    inner = (f'<div style="padding:20px 22px;background:{"rgba(82,5,3,.85)" if hot else PANEL};box-shadow:inset 3px 0 0 {col}">'
             f'<div class="row" style="justify-content:space-between">{i_ark(46, col)}<span class="big" style="font-size:54px;line-height:1;color:{col}">{pct}%</span></div>'
             f'<div style="margin-top:12px">{hbar(276, 12, pct, col)}</div><span class="cap2" style="display:block;margin-top:10px;color:{col}">Ark hull</span></div>'
             f'<div style="padding:18px 22px;background:{PANEL};box-shadow:inset 3px 0 0 {LINE}"><span class="cap2">Steal {GOAL} crystals</span>'
             f'<div class="row" style="gap:10px;margin-top:6px">{i_gem(22)}<span class="big" style="font-size:40px">{banked}</span><span class="big" style="font-size:20px;color:{MUTE}">/ {GOAL}</span></div>'
             f'<div style="margin-top:8px">{hbar(276, 6, banked / GOAL * 100, INK, False)}</div>'
             f'<span style="display:block;margin-top:10px;font-size:15px;color:{SOFT}">Carrying {CARRY}. Bank them at the Ark.</span></div>'
             f'<div style="padding:18px 22px;background:{PANEL};box-shadow:inset 3px 0 0 {LINE};display:flex;flex-direction:column;gap:8px">{t}</div>'
             f'<div style="padding:16px 22px;background:{PANEL};box-shadow:inset 3px 0 0 {LINE};display:flex;flex-direction:column;gap:9px">{cells}</div>')
    return stack(32, 32, inner, 320, 8)


def b_toast(kind, top=32):
    head, sub, col = TOASTS[kind]
    bg = "rgba(82,5,3,.92)" if col == RED2 else "rgba(0,4,31,.92)"
    return (f'<div class="abs tin" style="left:360px;top:{top}px;width:600px;display:flex;align-items:stretch;background:{bg}">'
            f'<span style="width:62px;display:flex;align-items:center;justify-content:center;background:{col};flex:none">{ICON[kind](28, "#00041F")}</span>'
            f'<span style="display:flex;flex-direction:column;gap:4px;padding:14px 22px"><span class="hd" style="font-size:24px;color:{col}">{head}</span>'
            f'<span style="font-size:16px;color:{INK};line-height:1.4">{sub}</span></span></div>')


def b_hud():
    return world("raid") + b_rail() + VESSEL


def b_attack():
    return world("attack") + b_rail(27, True) + b_toast("leak") + VESSEL


def b_toasts():
    return world("raid") + b_rail() + b_toast("threat") + VESSEL


def b_sail():
    return world("sail") + b_rail(timer="5", timer_big=True) + b_toast("sail", 300) + VESSEL


def b_pause():
    panel = ('<div class="abs" style="left:0;top:0;width:620px;height:1080px;background:rgba(0,4,31,.95);box-shadow:inset -1px 0 0 rgba(67,76,137,.7)"></div>'
             + stack(70, 120, (f'<span class="cap2">Paused</span><span class="hd" style="font-size:40px">Escort the Ark</span>'
                               f'<span class="row" style="gap:10px;font-size:20px;color:{INK}">{i_gem(18, INK)}Goal met: 50 of 50 aboard</span>'
                               f'<div style="height:30px"></div>{buttons_pause(w=460)}'), 480, 14))
    side = stack(720, 120, ('<span class="cap2">Your squad</span>' + seats_rows(560, 16, False)
                            + f'<div style="height:20px"></div><span class="cap2">Route</span>{route_h(560, 3, True)}'), 560, 10)
    return world("pause") + panel + side


def b_score():
    left = stack(80, 90, (f'<span class="cap2">Escort the Ark  ·  docked at Home</span><span class="hd" style="font-size:56px">Voyage complete</span>'
                          f'<div style="height:10px"></div>' + seats_rows(860)
                          + f'<div style="height:24px"></div>{end_buttons()}'), 900, 12)
    right = ('<div class="abs" style="left:1100px;top:0;width:820px;height:1080px;background:rgba(0,4,31,.9)"></div>'
             + stack(1160, 90, (f'<span class="cap2">Banked</span><span class="big" style="font-size:150px;line-height:1;color:{INK}">50</span>'
                                f'<div class="row" style="gap:22px;font-size:20px">{flower("Space", 22)} 17 {flower("Mass", 22)} 14 {flower("Time", 22)} 11 {flower("Charge", 22)} 8</div>'
                                f'<div style="height:30px"></div><span class="cap2">Rewards</span>' + "".join(
                                    f'<div class="row" style="justify-content:space-between;width:640px;padding:14px 0;box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)"><span style="font-size:20px">{a}</span><span class="big" style="font-size:30px">{b}</span></div>'
                                    for a, b in (("Crew trust", "+120"), ("Pilot XP", "+340"), ("Colour shards", "+6"), ("Ark hull", "58%")))), 680, 10))
    return dock_world() + left + right


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# C  Console: a racing game's bottom console; the route is a line across the top; toasts in the lower third
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def c_top():
    return stack(0, 30, route_h(1100, labels=True), W, 0, "center")


def c_console(pct=HULLP, hot=False, banked=BANKED, timer="0:18"):
    col = RED2 if hot else INK
    arc_len = 2 * 3.1416 * 52 * .75
    gauge = (f'<div style="position:relative;width:130px;height:130px;flex:none"><svg width="130" height="130" viewBox="0 0 130 130" aria-hidden="true">'
             f'<circle cx="65" cy="65" r="52" fill="none" stroke="rgba(67,76,137,.6)" stroke-width="10" stroke-dasharray="{arc_len:.0f} 400" transform="rotate(135 65 65)"></circle>'
             f'<circle cx="65" cy="65" r="52" fill="none" stroke="{col}" stroke-width="10" stroke-dasharray="{arc_len * pct / 100:.0f} 400" transform="rotate(135 65 65)"></circle>'
             f'<line x1="65" y1="5" x2="65" y2="20" stroke="{RED}" stroke-width="3" transform="rotate({-135 + 270 * BREACH / 100} 65 65)"></line></svg>'
             f'<span class="abs" style="inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center"><span class="big" style="font-size:38px;color:{col}">{pct}</span>'
             f'<span class="cap2" style="font-size:11px;color:{col}">Ark hull</span></span></div>')
    mid = (f'<div style="display:flex;flex-direction:column;align-items:center;gap:6px;padding:0 34px">'
           f'<div class="row" style="gap:12px">{i_gem(26)}<span class="big" style="font-size:66px;line-height:1">{banked}</span><span class="big" style="font-size:26px;color:{MUTE}">/ {GOAL}</span></div>'
           f'<span style="font-size:15px;color:{SOFT}">Carrying {CARRY}  ·  bank at the Ark</span></div>')
    tim = (f'<div style="display:flex;flex-direction:column;align-items:center;gap:4px;width:130px"><span class="big" style="font-size:40px">{timer}</span>'
           f'<span class="cap2" style="font-size:11px">The Ark sails</span><span style="font-size:13px;color:{RED2}">Scarab Reef  ·  hostile</span></div>')
    return (f'<div class="abs" style="left:560px;top:880px;width:800px;height:170px;display:flex;align-items:center;justify-content:center;'
            f'background:linear-gradient(0deg,rgba(0,4,31,.92),rgba(0,4,31,.6));clip-path:polygon(60px 0,calc(100% - 60px) 0,100% 100%,0 100%)">{gauge}{mid}{tim}</div>')


def c_toast(kind, top=700):
    head, sub, col = TOASTS[kind]
    return (f'<div class="abs tin" style="left:0;right:0;top:{top}px;display:flex;flex-direction:column;align-items:center;gap:6px">'
            f'<div class="row" style="gap:16px;padding:12px 40px;background:linear-gradient(90deg,rgba(0,4,31,0),{"rgba(82,5,3,.9)" if col == RED2 else "rgba(0,4,31,.9)"} 18%,{"rgba(82,5,3,.9)" if col == RED2 else "rgba(0,4,31,.9)"} 82%,rgba(0,4,31,0))">'
            f'{ICON[kind](26, col)}<span class="hd" style="font-size:30px;color:{col}">{head}</span></div>'
            f'<span style="font-size:17px;color:{INK};text-shadow:0 1px 3px #00010A">{sub}</span></div>')


def c_hud():
    return world("raid") + c_top() + c_console() + VESSEL


def c_attack():
    return world("attack") + c_top() + c_console(27, True) + c_toast("leak") + VESSEL


def c_toasts():
    return world("raid") + c_top() + c_console() + c_toast("threat") + VESSEL


def c_sail():
    count = stack(0, 300, (f'<span class="hd" style="font-size:26px">The Ark sails in</span>'
                           f'<span class="big" style="font-size:220px;line-height:1">5</span>'), W, 0, "center")
    return world("sail") + c_top() + count + c_console(timer="0:05") + c_toast("sail", 720).replace("THE ARK SAILS IN 5", "BANK WHAT YOU CARRY").replace(
        "Bank what you carry or lose it.", "Carrying 7. Anything not aboard is lost.") + VESSEL


def c_pause():
    sheet = ('<div class="abs" style="left:0;right:0;bottom:0;height:420px;background:rgba(0,4,31,.95);box-shadow:inset 0 1px 0 rgba(67,76,137,.8)"></div>'
             + stack(120, 700, (f'<span class="cap2">Paused  ·  Escort the Ark</span><span class="hd" style="font-size:40px;color:{INK}">Goal met  ·  50 of 50</span>'
                                f'<span style="font-size:18px;color:{SOFT}">Set sail for home now, or keep raiding for more.</span>'), 700, 10)
             + f'<div class="abs" style="left:120px;top:900px">{buttons_pause("row", big=True)}</div>'
             + stack(1180, 700, ('<span class="cap2">This voyage</span>' + "".join(
                 f'<div class="row" style="justify-content:space-between;width:600px;padding:6px 0"><span style="color:{SOFT}">{a}</span><span class="big" style="font-size:22px">{b}</span></div>'
                 for a, b in REWARDS)), 600, 2))
    return world("pause") + c_top() + sheet


def c_score():
    pod = ""
    for i, (v, p, who, n, el) in enumerate(SQUAD):
        x, hgt = 330 + i * 330, 260 - i * 40
        pod += (f'<div class="abs" style="left:{x}px;top:{760 - hgt}px;width:290px;height:{hgt}px;background:rgba(0,4,31,.85);box-shadow:inset 0 3px 0 {INK if who == "You" else LINE}"></div>'
                f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:{x + 25}px;top:{760 - hgt - 130}px;width:240px;height:120px">'
                + stack(x + 20, 760 - hgt + 20, (f'<span class="big" style="font-size:44px">{n}</span><span style="font-size:17px">{p}</span><span class="cap2">{v}  ·  {who}</span>'), 250, 4))
    head = stack(0, 70, (f'<span class="cap2">Escort the Ark  ·  docked at Home</span>'
                         f'<div class="row" style="gap:18px"><span class="hd" style="font-size:52px">The Ark is home</span><span class="big" style="font-size:52px;color:{INK}">50</span>'
                         f'{i_gem(30, INK)}</div>'), W, 8, "center")
    rew = stack(0, 800, (f'<div class="row" style="gap:40px;font-size:20px">' + "".join(
        f'<span><span class="cap2">{a}</span> <span class="big" style="font-size:26px;margin-left:8px">{b}</span></span>'
        for a, b in (("Crew trust", "+120"), ("Pilot XP", "+340"), ("Colour shards", "+6"), ("Ark hull", "58%"))) + "</div>" + end_buttons()), W, 30, "center")
    return dock_world() + head + pod + rew


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# D  Corners: hull top left, crystals top right, a toast feed under the crystals
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def d_corners(pct=HULLP, hot=False, banked=BANKED, timer="0:18"):
    col = RED2 if hot else INK
    tl = stack(40, 36, (f'<div class="row" style="gap:14px">{i_ark(52, col)}<span class="big" style="font-size:64px;line-height:1;color:{col}">{pct}</span>'
                        f'<span class="big" style="font-size:24px;color:{col}">%</span></div>{hbar(360, 10, pct, col)}'
                        f'<span class="cap2" style="color:{col}">Ark hull  ·  breach at {BREACH}%</span>'), 380, 10)
    tc = stack(0, 40, (f'<div class="row" style="gap:14px;padding:8px 18px;background:rgba(0,4,31,.7)"><span class="cap2" style="color:{INK}">Cell 3 / 6</span>'
                       f'<span>Scarab Reef</span><span style="color:{RED2}">hostile</span><span class="row" style="gap:6px">{i_clock(16)}{timer}</span></div>'), W, 0, "center")
    tr = stack(1500, 36, (f'<div class="row" style="gap:12px;justify-content:flex-end;width:380px"><span class="big" style="font-size:24px;color:{MUTE};align-self:flex-end;margin-bottom:6px">{GOAL} /</span>'
                          f'<span class="big" style="font-size:64px;line-height:1">{banked}</span>{i_gem(30)}</div>'
                          f'<span style="font-size:15px;color:{SOFT};text-align:right;width:380px">Carrying {CARRY}. Bank them at the Ark.</span>'), 380, 8, "flex-end")
    return tl + tc + tr


def d_feed(kinds, top=180):
    rows = ""
    for j, kind in enumerate(kinds):
        head, sub, col = TOASTS[kind]
        rows += (f'<div class="{"tin " if j == 0 else ""}row" style="gap:14px;width:440px;padding:12px 16px;box-sizing:border-box;opacity:{1 - j * .35};'
                 f'background:{"rgba(82,5,3,.9)" if col == RED2 else "rgba(0,4,31,.88)"};box-shadow:inset -3px 0 0 {col}">'
                 f'{ICON[kind](22, col)}<span style="display:flex;flex-direction:column;gap:3px"><span class="hd" style="font-size:18px;color:{col}">{head}</span>'
                 f'<span style="font-size:14px;color:{SOFT};line-height:1.35">{sub}</span></span></div>')
    return stack(1440, top, rows, 440, 8, "flex-end")


def d_hud():
    return world("raid") + d_corners() + VESSEL


def d_attack():
    return world("attack") + d_corners(27, True) + d_feed(["leak", "threat"]) + VESSEL


def d_toasts():
    return world("raid") + d_corners() + d_feed(["threat", "banked", "event"]) + VESSEL


def d_sail():
    mid = stack(0, 170, (f'<span class="hd" style="font-size:22px">The Ark sails in</span><span class="big" style="font-size:120px;line-height:1">5</span>'), W, 0, "center")
    return world("sail") + d_corners(timer="0:05") + mid + d_feed(["sail", "banked"]) + VESSEL


def d_pause():
    left = stack(160, 190, (f'<span class="cap2">Paused</span><span class="hd" style="font-size:72px;line-height:1">Escort<br>the Ark</span>'
                            f'<div style="height:40px"></div>{buttons_pause(w=460)}'), 560, 10)
    right = ('<div class="abs" style="left:900px;top:190px;width:2px;height:700px;background:rgba(67,76,137,.7)"></div>'
             + stack(980, 190, (f'<span class="row" style="gap:12px;color:{INK}">{i_gem(22, INK)}<span class="hd" style="font-size:22px">Goal met</span></span>'
                                f'<span class="big" style="font-size:150px;line-height:1">50<span style="font-size:50px;color:{MUTE}"> / 50</span></span>'
                                + "".join(f'<div class="row" style="justify-content:space-between;width:700px;padding:12px 0;box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)">'
                                          f'<span style="font-size:20px;color:{SOFT}">{a}</span><span class="big" style="font-size:28px">{b}</span></div>' for a, b in REWARDS[1:])),
                     760, 8))
    return world("pause") + left + right


def d_score():
    cards = ""
    for i, (v, p, who, n, el) in enumerate(SQUAD):
        x = 140 + i * 420
        cards += (f'<div class="abs" style="left:{x}px;top:300px;width:380px;height:440px;background:rgba(0,4,31,.88);box-shadow:inset 0 0 0 1px {INK if who == "You" else LINE}"></div>'
                  f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:{x + 30}px;top:320px;width:320px;height:160px">'
                  + stack(x + 30, 500, (f'<span class="cap2">{v}  ·  {who}</span><span style="font-size:20px">{p}</span>'
                                       f'<div class="row" style="gap:10px;margin-top:6px">{i_gem(22)}<span class="big" style="font-size:52px">{n}</span>{flower(el, 26)}</div>'
                                       f'<span style="font-size:15px;color:{SOFT}">{["Most banked", "Most raiders driven off", "Longest escort", "Most cells raided"][i]}</span>'), 320, 6))
    head = stack(140, 80, (f'<span class="cap2">Escort the Ark  ·  docked at Home</span>'
                           f'<div class="row" style="gap:30px"><span class="hd" style="font-size:56px">The Ark is home</span>'
                           f'<span class="big" style="font-size:56px;color:{INK}">50 banked</span></div>'), 1640, 8)
    foot = (f'<div class="abs row" style="left:140px;top:800px;width:1640px;justify-content:space-between">'
            f'<div class="row" style="gap:40px">' + "".join(f'<span style="display:flex;flex-direction:column;gap:4px"><span class="cap2">{a}</span><span class="big" style="font-size:30px">{b}</span></span>'
                                                            for a, b in (("Crew trust", "+120"), ("Pilot XP", "+340"), ("Colour shards", "+6"))) + f'</div>{end_buttons()}</div>')
    return dock_world() + head + cards + foot


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# E  Edges: two arcs hug the screen's sides (hull left, goal right); toasts are big words at top centre, no box
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def e_arc(side, pct, col, label, value, sub):
    left = side == "L"
    # an arc bowed toward the centre, 620 px tall
    path = "M70 630 Q 10 320 70 10" if left else "M10 630 Q 70 320 10 10"
    length = 680
    x = 30 if left else W - 110
    fill = length * pct / 100
    marks = ""
    if left:
        for p, st in ((BREACH, f'stroke="{RED}" stroke-width="4"'), (LEAK, f'stroke="{MUTE}" stroke-width="3" stroke-dasharray="4 4"')):
            yy = 630 - 620 * p / 100
            marks += f'<line x1="22" y1="{yy:.0f}" x2="72" y2="{yy:.0f}" {st}></line>'
    svg = (f'<svg class="abs" style="left:{x}px;top:220px" width="80" height="640" viewBox="0 0 80 640" aria-hidden="true">'
           f'<path d="{path}" fill="none" stroke="rgba(67,76,137,.55)" stroke-width="10" stroke-linecap="round"></path>'
           f'<path d="{path}" fill="none" stroke="{col}" stroke-width="10" stroke-linecap="round" stroke-dasharray="{fill:.0f} {length}"></path>{marks}</svg>')
    tx = 120 if left else W - 420
    al = "flex-start" if left else "flex-end"
    txt = stack(tx, 470, (f'<span class="big" style="font-size:56px;line-height:1;color:{col}">{value}</span><span class="cap2" style="color:{col}">{label}</span>'
                          f'<span style="font-size:14px;color:{SOFT};text-align:{"left" if left else "right"};max-width:300px">{sub}</span>'), 300, 6, al)
    return svg + txt


def e_frame(pct=HULLP, hot=False, banked=BANKED, timer="0:18"):
    col = RED2 if hot else INK
    top = stack(0, 34, f'<span class="cap2" style="color:{INK}">Cell 3 of 6  ·  Scarab Reef  ·  <span style="color:{RED2}">hostile</span>  ·  the Ark sails in {timer}</span>', W, 0, "center")
    return (top + e_arc("L", pct, col, "Ark hull", f"{pct}%", f"Breach at {BREACH}%") +
            e_arc("R", banked / GOAL * 100, INK, f"of {GOAL} aboard", f"{banked}", f"Carrying {CARRY}. Bank them at the Ark."))


def e_toast(kind, top=120, size=58):
    head, sub, col = TOASTS[kind]
    return (f'<div class="abs tin" style="left:0;right:0;top:{top}px;display:flex;flex-direction:column;align-items:center;gap:10px">'
            f'<div class="row" style="gap:26px"><span style="width:120px;height:2px;background:{col}"></span>'
            f'<span class="hd" style="font-size:{size}px;letter-spacing:.2em;color:{col};text-shadow:0 2px 14px rgba(0,1,10,.9)">{head}</span>'
            f'<span style="width:120px;height:2px;background:{col}"></span></div>'
            f'<span style="font-size:19px;color:{INK};text-shadow:0 1px 6px #00010A">{sub}</span></div>')


def e_hud():
    return world("raid") + e_frame() + VESSEL


def e_attack():
    return world("attack") + e_frame(27, True) + e_toast("leak", size=50) + VESSEL


def e_toasts():
    return world("raid") + e_frame() + e_toast("threat") + VESSEL


def e_sail():
    big = stack(0, 260, f'<span class="big" style="font-size:260px;line-height:1;color:rgba(230,233,255,.92);text-shadow:0 4px 30px rgba(0,1,10,.8)">5</span>', W, 0, "center")
    return world("sail") + e_frame(timer="0:05") + e_toast("sail", size=50) + big + VESSEL


def e_pause():
    head = stack(0, 230, (f'<span class="cap2">Paused  ·  Escort the Ark</span><span class="hd" style="font-size:80px;letter-spacing:.2em">Goal met</span>'
                          f'<span style="font-size:22px;color:{SOFT}">50 of 50 aboard  ·  hull 58%  ·  cell 4 of 6  ·  6:42</span>'), W, 12, "center")
    btns = (f'<div class="abs row" style="left:0;right:0;top:620px;justify-content:center;gap:16px">'
            f'<button class="btn def" type="button" style="height:64px;font-size:20px">{kc("B")}Resume</button>'
            f'<button class="btn cta" type="button" style="height:76px;font-size:24px;padding:0 40px">{kc("A")}Complete and set sail</button>'
            f'<button class="btn def" type="button" style="height:64px;font-size:20px">Settings</button>'
            f'<button class="btn def" type="button" style="height:64px;font-size:20px;color:#B8C0F0">Leave voyage</button></div>')
    return world("pause") + head + btns + stack(0, 790, route_h(900, 3, True), W, 0, "center")


def e_score():
    head = stack(0, 60, (f'<span class="cap2">Escort the Ark</span><span class="hd" style="font-size:72px;letter-spacing:.2em">The Ark is home</span>'), W, 8, "center")
    per = [0, 9, 0, 22, 19, 0]  # crystals banked per cell, Home .. The Maw
    bars = ""
    for i, name in enumerate(CELLS[:5]):
        x = 360 + i * 250
        hgt = per[i] * 9
        bars += (f'<div class="abs" style="left:{x}px;top:{470 - hgt}px;width:120px;height:{max(hgt, 2)}px;background:{INK if hgt else LINE}"></div>'
                 f'<span class="abs big" style="left:{x}px;top:{430 - hgt}px;width:120px;text-align:center;font-size:28px">{per[i] or ""}</span>'
                 f'<span class="abs" style="left:{x - 40}px;top:486px;width:200px;text-align:center;font-size:15px;color:{SOFT}">{name}</span>')
    total = stack(1640, 260, (f'<span class="cap2">Banked</span><span class="big" style="font-size:90px;line-height:1;color:{INK}">50</span>'), 200, 4)
    seats = (f'<div class="abs row" style="left:200px;top:560px;width:1520px;gap:16px">' + "".join(
        f'<div style="flex:1;display:flex;align-items:center;gap:12px;padding:12px;background:rgba(0,4,31,.8);box-shadow:inset 0 2px 0 {INK if who == "You" else LINE}">'
        f'<img class="hull" src="{HULL[v]}" alt="{v}" style="width:110px;height:54px"><span style="display:flex;flex-direction:column;gap:2px;flex:1"><span style="font-size:16px">{p}</span>'
        f'<span class="cap2">{who}</span></span><span class="big" style="font-size:34px">{n}</span></div>' for v, p, who, n, el in SQUAD) + "</div>")
    rew = stack(0, 720, (f'<div class="row" style="gap:50px">' + "".join(
        f'<span style="display:flex;flex-direction:column;align-items:center;gap:4px"><span class="big" style="font-size:36px">{b}</span><span class="cap2">{a}</span></span>'
        for a, b in (("Crew trust", "+120"), ("Pilot XP", "+340"), ("Colour shards", "+6"), ("Ark hull", "58%"))) + "</div>" + end_buttons()), W, 40, "center")
    return dock_world() + head + bars + total + seats + rew


# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# F  Blocks: a fighting game's top bar, segmented; toasts a slanted banner that slams in at mid left
# ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def segs(n, on, w, h, col, rev=False, gap=4):
    cells = "".join(f'<span style="flex:1;height:{h}px;background:{col if (n - 1 - i if rev else i) < on else "rgba(67,76,137,.45)"};'
                    f'{"box-shadow:inset 0 0 0 2px " + RED if (not rev and i == round(n * BREACH / 100) - 1) else ""}"></span>' for i in range(n))
    return f'<div class="row" style="gap:{gap}px;width:{w}px">{cells}</div>'


def f_bar(pct=HULLP, hot=False, banked=BANKED, timer="18", big_timer=False):
    col = RED2 if hot else INK
    left = stack(40, 32, (f'<div class="row" style="gap:12px;justify-content:space-between;width:700px"><span class="row" style="gap:10px">{i_ark(40, col)}<span class="hd" style="font-size:20px;color:{col}">The Ark</span></span>'
                          f'<span class="big" style="font-size:30px;color:{col}">{pct}%</span></div>'
                          f'<div class="skew" style="padding:6px 30px;background:rgba(0,4,31,.85)">{segs(20, round(pct / 5), 640, 26, col)}</div>'), 700, 6)
    right = stack(1180, 32, (f'<div class="row" style="gap:12px;justify-content:space-between;width:700px"><span class="big" style="font-size:30px">{banked:03d} / {GOAL:03d}</span>'
                             f'<span class="row" style="gap:10px"><span class="hd" style="font-size:20px">Crystals</span>{i_gem(22)}</span></div>'
                             f'<div class="skew" style="padding:6px 30px;background:rgba(0,4,31,.85)">{segs(25, round(banked / 2), 640, 26, INK, True)}</div>'), 700, 6, "flex-end")
    t = (f'<div class="abs" style="left:{W / 2 - (110 if big_timer else 70)}px;top:24px;width:{220 if big_timer else 140}px;height:{150 if big_timer else 96}px;'
         f'background:rgba(0,4,31,.92);box-shadow:inset 0 0 0 2px {INK};display:flex;flex-direction:column;align-items:center;justify-content:center">'
         f'<span class="big" style="font-size:{110 if big_timer else 58}px;line-height:1">{timer}</span><span class="cap2" style="font-size:11px">{"Sail" if big_timer else "Raid"}</span></div>')
    under = stack(0, 140 if not big_timer else 186, f'<span class="cap2" style="color:{INK}">Cell 3  ·  Scarab Reef  ·  <span style="color:{RED2}">hostile</span>  ·  carrying {CARRY}</span>', W, 0, "center")
    return left + right + t + under


def f_toast(kind, top=330):
    head, sub, col = TOASTS[kind]
    bg = "rgba(119,5,3,.94)" if col == RED2 else "rgba(0,4,31,.94)"
    return (f'<div class="abs tin" style="left:-30px;top:{top}px;display:flex;flex-direction:column;gap:0">'
            f'<div class="skew" style="padding:14px 90px 14px 70px;background:{bg};box-shadow:inset 0 4px 0 {col}">'
            f'<span class="hd" style="font-size:46px;letter-spacing:.1em;color:{col};font-style:italic">{head}</span></div>'
            f'<div class="skew" style="margin-left:40px;padding:8px 60px 8px 50px;background:rgba(0,4,31,.85);max-width:720px">'
            f'<span style="font-size:17px;color:{INK}">{sub}</span></div></div>')


def f_hud():
    return world("raid") + f_bar() + VESSEL


def f_attack():
    return world("attack") + f_bar(27, True) + f_toast("leak") + VESSEL


def f_toasts():
    return world("raid") + f_bar() + f_toast("threat") + VESSEL


def f_sail():
    return world("sail") + f_bar(timer="5", big_timer=True) + f_toast("sail") + VESSEL


def f_pause():
    items = [("Resume", False, "B"), ("Complete and set sail", True, "A"), ("Settings", False, ""), ("Leave voyage", False, "")]
    rows = ""
    for j, (lab, on, k) in enumerate(items):
        col = "#00041F" if on else (SOFT if lab == "Leave voyage" else INK)
        rows += (f'<button class="pick row" type="button" style="gap:20px;height:78px;padding:0 30px;width:640px;box-sizing:border-box;'
                 f'{"background:" + LIME + ";color:#00041F" if on else "background:rgba(0,4,31,.85);color:" + col}">'
                 f'<span class="hd" style="font-size:30px;width:28px">{"&gt;" if on else ""}</span>'
                 f'<span class="hd" style="font-size:28px;font-style:italic">{lab}</span>'
                 f'{"<span class=kc style=margin-left:auto;border-color:currentColor;color:inherit>" + k + "</span>" if k else ""}</button>')
    head = (f'<div class="abs skew" style="left:-30px;top:150px;padding:18px 120px 18px 90px;background:{INK}">'
            f'<span class="hd" style="font-size:64px;color:#00041F;font-style:italic">Paused</span></div>')
    side = stack(1200, 330, (f'<span class="hd" style="font-size:24px;color:{INK};font-style:italic">Goal met</span>'
                             f'<span class="big" style="font-size:120px;line-height:1">050<span style="font-size:40px;color:{MUTE}"> / 050</span></span>'
                             + segs(25, 25, 560, 22, INK)
                             + f'<span style="font-size:18px;color:{SOFT};margin-top:12px">Hull 58%  ·  cell 4 of 6  ·  6:42</span>'), 600, 12)
    return world("pause") + head + stack(120, 330, rows, 640, 12) + side


def f_score():
    head = (f'<div class="abs skew" style="left:-30px;top:60px;padding:18px 140px 18px 90px;background:{INK}">'
            f'<span class="hd" style="font-size:64px;color:#00041F;font-style:italic">Results</span></div>')
    ranks = ["S", "A", "A", "B"]
    rows = ""
    for i, (v, p, who, n, el) in enumerate(SQUAD):
        rows += (f'<div class="row" style="gap:24px;width:1140px;box-sizing:border-box;height:96px;padding:0 30px;background:rgba(0,4,31,.88);box-shadow:inset 4px 0 0 {INK if who == "You" else LINE}">'
                 f'<span class="big" style="font-size:30px;width:40px;color:{MUTE}">{i + 1}</span>'
                 f'<img class="hull" src="{HULL[v]}" alt="{v}" style="width:140px;height:70px">'
                 f'<span style="display:flex;flex-direction:column;gap:4px;flex:1"><span class="hd" style="font-size:22px;font-style:italic">{p}</span><span class="cap2">{v}  ·  {who}</span></span>'
                 f'{flower(el, 26)}<span class="big" style="font-size:44px;width:90px;text-align:right">{n:03d}</span>'
                 f'<span class="hd" style="font-size:60px;width:80px;text-align:center;font-style:italic;color:{INK if ranks[i] == "S" else INK}">{ranks[i]}</span></div>')
    side = stack(1330, 230, (f'<span class="cap2">Total banked</span><span class="big" style="font-size:110px;line-height:1;color:{INK}">050</span>'
                             + "".join(f'<div class="row" style="justify-content:space-between;width:460px;padding:10px 0;box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)"><span class="hd" style="font-size:18px">{a}</span><span class="big" style="font-size:26px">{b}</span></div>'
                                       for a, b in (("Crew trust", "+120"), ("Pilot XP", "+340"), ("Colour shards", "+6"), ("Ark hull", "58%")))), 480, 8)
    return dock_world() + head + stack(120, 230, rows, 1140, 10) + side + stack(120, 720, end_buttons(), 1140, 0)


FAM = {
    "A": ("Marquee (recommended)", "Arcade top centre: the count is the score, the toast slot is the band right under it.",
          a_hud, a_attack, a_toasts, a_sail, a_pause, a_score, a_toast),
    "B": ("Rail", "One column on the left: the Ark's hull first, the goal, the timer and the route under it. Toasts slide out of the rail.",
          b_hud, b_attack, b_toasts, b_sail, b_pause, b_score, b_toast),
    "C": ("Console", "A racing game's console at the bottom centre; the route is a line across the top. Toasts in the lower third.",
          c_hud, c_attack, c_toasts, c_sail, c_pause, c_score, c_toast),
    "D": ("Corners", "A shooter's corners: hull top left, crystals top right. Toasts are a feed of up to three under the crystals.",
          d_hud, d_attack, d_toasts, d_sail, d_pause, d_score, None),
    "E": ("Edges", "Two arcs hug the screen's sides, hull left and goal right. Toasts are big words at top centre with no box.",
          e_hud, e_attack, e_toasts, e_sail, e_pause, e_score, e_toast),
    "F": ("Blocks", "A fighting game's top bar in segments, the timer boxed between. Toasts are a slanted banner that slams in at mid left.",
          f_hud, f_attack, f_toasts, f_sail, f_pause, f_score, f_toast),
}

SCREENS = {  # key: (index into FAM, board title, header sub, one line per option)
    "Hud": (2, "In flight: the goal count, the Ark's hull on the left, the cell and the anchor timer",
            "Raiding Scarab Reef, cell 3 of 6: 23 of 50 crystals banked, 7 carried, the Ark's hull 64%, the Ark sails in 0:18."),
    "Attack": (3, "The Ark under attack: a kaiju-sized raider on the hull, the hull under 30%, the hold leaking",
               "The hull at 27%: under the 30% leak line (dashed) and close to the 10% breach line (red). The hull readout turns the threat colour; the raider is lit red in the world, never ringed on the screen."),
    "Toasts": (4, "The toast spot: one fixed place per option, every kind of toast drawn in it",
               "The screen shows ARK UNDER ATTACK landing in the spot. Under each screen: the same spot holding an event, a goal and a small one. One toast at a time; a threat jumps the queue."),
    "Sail": (5, "The raid countdown: the Ark sails in 5, bank what you carry",
             "The last five seconds at anchor (ToastAnchorClosing). Carrying 7: anything not aboard when the Ark sails is lost."),
    "Pause": (6, "The pause menu with the goal met: Resume, Complete and set sail, Settings, Leave voyage",
              "50 of 50 aboard at cell 4. Complete and set sail is the call to action (Lime); Leave voyage is the no-way-back (Ruby text)."),
    "Score": (7, "The end scoreboard: the Ark docked, a row per seat, rewards, Finish and Venture further",
              "Docked at Home with 50 banked. Rewards are drawn as placeholders: there is no voyage XP or trust system yet (Docs/Voyages/CRYSTAL_RUN.md)."),
}


def toast_strip(letter):
    """Under each Toasts screen: the same spot holding three more kinds, at their real size, clipped to the strip."""
    fam = FAM[letter]
    out = []
    for j, kind in enumerate(("event", "goal", "banked")):
        x = j * 650
        if letter == "D":
            inner = d_feed([kind], 20).replace("left:1440px", "left:10px")
        elif letter == "B":
            inner = b_toast(kind, 20).replace("left:360px", "left:10px").replace("width:600px", "width:620px")
        elif letter == "A":
            inner = a_toast(kind, 30)
        elif letter == "C":
            inner = c_toast(kind, 40)
        elif letter == "E":
            inner = e_toast(kind, 40, 30)
        else:
            inner = f_toast(kind, 20).replace("font-size:46px", "font-size:30px")
        out.append(f'<div class="abs" style="left:{x}px;top:0;width:630px;height:{UNDER - 40}px;overflow:hidden;background:#00041F;'
                   f'box-shadow:inset 0 0 0 1px rgba(67,76,137,.5)"><div style="position:absolute;inset:0;transform:scale(.62);transform-origin:{"50% 0" if letter in "ACE" else "0 0"};'
                   f'width:{1016 if letter in "ACE" else 1016}px;left:{-193 if letter in "ACE" else 0}px">{inner}</div>'
                   f'<span class="abs cap2" style="right:12px;bottom:8px">{kind}</span></div>')
    return "".join(out)


def slot_mark(letter):
    """A dashed outline of the toast spot, drawn only on the Toasts board, so the spot reads as a place."""
    box = {"A": (440, 220, 1040, 120), "B": (352, 24, 616, 120), "C": (480, 690, 960, 110), "D": (1432, 172, 456, 300),
           "E": (380, 110, 1160, 120), "F": (0, 322, 1040, 150)}[letter]
    x, y, w, h = box
    return (f'<div class="slot" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px"></div>'
            f'<span class="abs cap2" style="left:{x}px;top:{y + h + 6}px;color:{FOCUS}">Toast spot</span>')


SCALE = [14, 15, 18, 20, 22, 28, 36, 44, 48]


def snap(html):
    """Font sizes up to 48 px land on the style guide's type scale; the big display numbers stay as drawn."""
    import re
    def fix(m):
        v = float(m.group(1))
        if v > 48:
            return m.group(0)
        return f"font-size:{min(SCALE, key=lambda s: abs(s - v))}px"
    return re.sub(r"font-size:(\d+(?:\.\d+)?)px", fix, html)


def board(key):
    idx, title, sub = SCREENS[key]
    body = (f'<span class="abs hd" style="left:0;top:10px;font-size:44px">{title}</span>'
            f'<span class="abs" style="left:0;top:78px;font-size:22px;color:{SOFT};max-width:5200px">{sub}</span>')
    for n, (letter, fam) in enumerate(FAM.items()):
        col, row = n % 3, n // 3
        x, y = col * (W + GAP), HEAD + row * (CELL_H + GAP)
        name, line = fam[0], fam[1]
        scr = fam[idx]()
        if key == "Toasts":
            scr += slot_mark(letter)
        body += (f'<div class="abs row" style="left:{x}px;top:{y}px;width:{W}px;height:{LABEL - 10}px;gap:18px">'
                 f'<span class="hd" style="font-size:34px">{letter}</span><span class="hd" style="font-size:24px">{name}</span>'
                 f'<span style="font-size:17px;color:{SOFT}" class="ell">{line}</span></div>'
                 f'<div class="scr" style="left:{x}px;top:{y + LABEL}px;width:{W}px;height:{H}px">{scr}</div>')
        if key == "Toasts":
            body += f'<div class="abs" style="left:{x}px;top:{y + LABEL + H + 20}px;width:{W}px;height:{UNDER - 20}px">{toast_strip(letter)}</div>'
    html = snap(r100.page(f"Rev {REV} {key} - {title}", body, r113.LOGIC))
    return (html.replace("width: 1920px; height: 1080px", f"width: {BW}px; height: {BH}px")
            .replace('"width":1920,"height":1080', f'"width":{BW},"height":{BH}'))


BOARDS = {f"R{REV}{k}.dc.html": (lambda k=k: board(k)) for k in SCREENS}

if __name__ == "__main__":
    print("board size", BW, "x", BH)
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
