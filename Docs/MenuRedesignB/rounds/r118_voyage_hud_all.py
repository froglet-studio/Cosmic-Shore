#!/usr/bin/env python3
"""Rev 118 boards: every in-game voyage screen, one HUD for every device (Frogma tab "Voyages playtest").

Shombith, 2026-11-20: "HUD should be same accross all devices, I was asking you to draw in game mockups for all in game
HUD." Then: "Use frogma".

One family, Rev 116 A (Marquee, picked 2026-11-17 and built), sized up so it reads on every screen: the game shows its
1920 x 1080 layout everywhere, about 0.40 of its size on a phone (Docs/UI_STYLE_GUIDE.md section 8, D6), so no word
here is under 28 units. Sixteen screens, one board each, in the order a run meets them, plus a notes board:

  Intro   the intro reel's title shot: no HUD
  SetOff  setting off: the goal toast; the Ark's hull top left
  Raid    raiding a hostile cell: the count, what you carry, the route, the clock; the quiet hint in the toast spot
  Pickup  a crystal picked up: the running +1 toast
  Banked  banked at the Ark: the toast with what was banked by element (the split left the marquee)
  Attack  the Ark under attack: the hull turns red with UNDER ATTACK; raiders marked in the world (ArkAttackerMarks)
  Leak    the hold leaking: hull under 30%
  Sail    the raid countdown: the ring and what is at stake under it
  Kaiju   between cells: a kaiju in open water
  Stray   too far from the Ark: the recall countdown
  Goal    goal met
  Pause   pause, goal met: Complete and set sail
  Home    sailing home: what is aboard and at stake
  Escort  Escort the Ark: the hull is the score, an assault on
  Score   the end board with the goal met: Finish here, Sail home, Venture further
  Lost    the Ark lost

Every word is an authored voyage line (Docs/Voyages/voyage_slice.json) with its tokens filled; three are new in the
parked build (ObjectiveCountCarried, ObjectiveRailUnderAttack, ObjectiveRingCaption). Pictures, the Ark, the fauna and
the shared pieces come from the Rev 113-116 generators.

    python3 Docs/MenuRedesignB/rounds/r118_voyage_hud_all.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "118"
_spec = importlib.util.spec_from_file_location("r116", HERE / "r116_voyage_hud.py")
r116 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev, "lib"]
_spec.loader.exec_module(r116)
r114, r113, r100 = r116.r114, r116.r113, r116.r100

REV = int(_rev)
ROOT = r116.ROOT
W, H = 1920, 1080
INK, MUTE, SOFT, LINE = r116.INK, r116.MUTE, r116.SOFT, r116.LINE
LIME, RED, RED2, FOCUS = r116.LIME, r116.RED, r116.RED2, r116.FOCUS
GOAL, CARRY = r116.GOAL, r116.CARRY
ark, fauna, gem, flower, world, dock_world = r116.ark, r116.fauna, r116.gem, r116.flower, r116.world, r116.dock_world
i_gem, i_ark, i_warn, i_flag, i_clock, kc = r116.i_gem, r116.i_ark, r116.i_warn, r116.i_flag, r116.i_clock, r116.kc
route_h, hbar, stack, seats_rows = r116.route_h, r116.hbar, r116.stack, r116.seats_rows

# The sizes (canvas units on the 1920 x 1080 frame; on a phone, x 0.40): the floor is 28.
COUNT, GOALSZ, CARRYSZ, SUB, TOAST, TOASTSUB, FLOOR = 88, 52, 36, 28, 44, 28, 28
HULL_W, TOP, SPOT = 440, 30, 236  # the hull block's width, the marquee's top, where the toast spot starts

VESSEL = ('<div class="abs" style="right:40px;bottom:40px;width:380px;height:130px;box-shadow:inset 0 0 0 1px rgba(67,76,137,.5);display:flex;'
          f'align-items:center;justify-content:center"><span style="font-size:{FLOOR}px;color:{MUTE}">Vessel HUD, as today</span></div>')
PANEL, HOT = "rgba(0,4,31,.62)", "rgba(82,5,3,.78)"


# ── the pieces ──────────────────────────────────────────────────────────────────────────────────────────────────────
def hull_block(pct, hot=False, alarm=False):
    """The Ark at the top left: caption and figure on one line, a wide bar with the breach (red) and leak (dashed) lines,
    and UNDER ATTACK while it is hit. Red while the hull loses plates or the hold leaks."""
    col = RED2 if hot else INK
    bg = HOT if hot else PANEL
    edge = f"box-shadow:inset 0 0 0 2px {RED};" if hot else ""
    al = f'<span class="hd" style="font-size:{FLOOR}px;color:{RED2}">Under attack</span>' if alarm else ""
    return (f'<div class="abs" style="left:40px;top:{TOP + 4}px;width:{HULL_W}px;padding:14px 20px 18px;background:{bg};{edge}'
            f'display:flex;flex-direction:column;gap:10px">'
            f'<div class="row" style="justify-content:space-between;align-items:flex-end"><span class="row" style="gap:12px">{i_ark(40, col)}'
            f'<span class="hd" style="font-size:{FLOOR}px;color:{col}">Ark hull</span></span>'
            f'<span class="big" style="font-size:52px;line-height:1;color:{col}">{pct}%</span></div>'
            f'{hbar(HULL_W - 40, 22, pct, col)}{al}</div>')


def marquee(banked=23, carry=CARRY, cell=3, place="Scarab Reef", hostile=True, clock="0:18", now=2, count_col=INK,
            figure=None, figure_sub=None):
    """The score at top centre: the count, the goal small beside it, what you carry beside that; then one row: the
    route, the place and the raid clock. `figure` replaces the count (an escort's hull, what is aboard on the way home)."""
    if figure is None:
        top = (f'{i_gem(34, count_col)}<span class="big" style="font-size:{COUNT}px;line-height:1;color:{count_col}">{banked}</span>'
               f'<span class="big" style="font-size:{GOALSZ}px;color:{MUTE};align-self:flex-end;margin-bottom:6px">/ {GOAL}</span>'
               + (f'<span class="big" style="font-size:{CARRYSZ}px;color:{INK};align-self:center;margin-left:14px">+{carry}</span>' if carry else ""))
    else:
        top = figure
    state = "" if hostile is None else (f' - <span style="color:{RED2}">hostile</span>' if hostile else f' - <span style="color:{SOFT}">calm</span>')
    where = f'<span>{cell}/6 &nbsp;{place}{state}</span>' if place else ""
    clk = f'<span class="row" style="gap:10px">{i_clock(28, INK)}{clock}</span>' if clock else ""
    under = figure_sub or f'<div class="row" style="gap:26px;font-size:{SUB}px;color:{INK};white-space:nowrap">{route_h(300, now=now)}{where}{clk}</div>'
    return (f'<div class="abs" style="left:0;right:0;top:{TOP}px;display:flex;justify-content:center">'
            f'<div style="display:flex;flex-direction:column;align-items:center;gap:8px;padding:8px 40px 16px;background:{PANEL};min-width:560px">'
            f'<div class="row" style="gap:16px">{top}</div>{under}</div></div>')


def toast(head, sub="", mood="event", icon=None):
    """The one toast spot, under the marquee: a slanted band, the line 44, the line under it 28, a foot line in the mood
    colour (event white, threat red, good lime)."""
    col = {"event": INK, "threat": RED2, "good": LIME}[mood]
    bg = "rgba(82,5,3,.9)" if mood == "threat" else "rgba(0,4,31,.9)"
    ic = (icon or {"event": i_flag, "threat": i_warn, "good": i_gem}[mood])(36, col)
    s = f'<span style="font-size:{TOASTSUB}px;color:{INK};max-width:880px;line-height:1.3">{sub}</span>' if sub else ""
    return (f'<div class="abs tin" style="left:0;right:0;top:{SPOT}px;display:flex;justify-content:center">'
            f'<div class="skew" style="display:flex;align-items:center;gap:24px;padding:14px 70px 16px;max-width:1040px;background:{bg};box-shadow:inset 0 -4px 0 {col}">'
            f'{ic}<span style="display:flex;flex-direction:column;gap:6px"><span class="hd" style="font-size:{TOAST}px;color:{col}">{head}</span>{s}</span></div></div>')


def hint(text, threat=False):
    """The toast spot's quiet line when no toast is up: the next thing to do, dimmed."""
    col = RED2 if threat else SOFT
    return (f'<div class="abs" style="left:0;right:0;top:{SPOT}px;display:flex;justify-content:center">'
            f'<span style="padding:10px 40px 12px;background:rgba(0,4,31,.5);font-size:{SUB}px;color:{col}">{text}</span></div>')


def ring(n, caption):
    return (f'<div class="abs" style="left:830px;top:380px;width:260px;height:260px">'
            f'<svg width="260" height="260" viewBox="0 0 260 260" aria-hidden="true"><circle cx="130" cy="130" r="120" fill="rgba(0,4,31,.55)" stroke="rgba(67,76,137,.6)" stroke-width="9"></circle>'
            f'<circle cx="130" cy="130" r="120" fill="none" stroke="{INK}" stroke-width="9" stroke-dasharray="{754 * n / 5:.0f} 760" transform="rotate(-90 130 130)"></circle></svg>'
            f'<span class="abs big" style="inset:0;display:flex;align-items:center;justify-content:center;font-size:130px">{n}</span>'
            f'<span class="abs" style="left:-320px;right:-320px;top:276px;text-align:center;font-size:36px;color:{INK}">{caption}</span></div>')


def diamond(x, y, s=1.0):
    """ArkAttackerMarks' lit red diamond over a creature on the Ark (a 3D mark in the world, drawn as lit facets)."""
    w, h = 34 * s, 52 * s
    return (f'<svg class="abs" style="left:{x - w / 2:.0f}px;top:{y - h:.0f}px" width="{w:.0f}" height="{h:.0f}" viewBox="0 0 34 52" aria-hidden="true">'
            '<path d="M17 52L0 20 17 0z" fill="#FF4B3A"></path><path d="M17 52L34 20 17 0z" fill="#B8231A"></path>'
            '<path d="M0 20h34" stroke="#FF8A7C" stroke-width="1.5"></path></svg>')


def edge_arrow(side="left", y=560, pips=2):
    """ArkAttackerMarks' edge arrow, in the world just in front of the camera: a lit red arrow at the screen's edge
    toward attackers off screen, a pip per attacker."""
    x = 70 if side == "left" else W - 70
    rot = 180 if side == "left" else 0
    p = "".join(f'<span style="width:12px;height:12px;background:{RED};transform:rotate(45deg)"></span>' for _ in range(pips))
    return (f'<svg class="abs" style="left:{x - 40}px;top:{y - 40}px;transform:rotate({rot}deg)" width="80" height="80" viewBox="0 0 80 80" aria-hidden="true">'
            '<path d="M10 28h34V12l30 28-30 28V52H10z" fill="#FF4B3A"></path><path d="M44 12l30 28H10" fill="none" stroke="#FF8A7C" stroke-width="2"></path></svg>'
            f'<div class="abs row" style="left:{x - 30}px;top:{y + 50}px;gap:10px">{p}</div>')


def letterbox():
    return ('<div class="abs" style="left:0;right:0;top:0;height:120px;background:#000"></div>'
            '<div class="abs" style="left:0;right:0;bottom:0;height:120px;background:#000"></div>')


def tag(text):
    """The board's own label (not game UI): what this screen is, top right, in the focus colour."""
    return f'<span class="abs cap2" style="right:40px;top:{H - 40}px;color:{FOCUS};font-size:14px">{text}</span>'


# ── the screens ─────────────────────────────────────────────────────────────────────────────────────────────────────
def s_intro():
    return (world("raid") + letterbox()
            + stack(0, 380, (f'<span class="hd" style="font-size:36px;color:{SOFT}">Crystal Run to The Maw</span>'
                             f'<span class="hd" style="font-size:96px;line-height:1">Steal {GOAL} crystals</span>'
                             f'<span style="font-size:36px;color:{INK}">Before the Ark reaches The Maw. Keep its hull above 10%.</span>'), W, 18, "center")
            + f'<div class="abs row" style="right:60px;bottom:150px;gap:12px;font-size:{FLOOR}px;color:{SOFT}">{kc("A")}Hold to skip</div>')


def s_setoff():
    return (world("raid") + marquee(0, 0, 1, "Garland", False, "", now=0) + hull_block(100)
            + toast(f"Steal {GOAL} crystals", "Before the Ark reaches The Maw. Keep its hull above 10%.") + VESSEL)


def s_raid():
    return world("raid") + marquee() + hull_block(64) + hint(f"Carrying {CARRY}. Bank them at the Ark.") + VESSEL


def s_pickup():
    return (world("raid") + marquee(carry=8) + hull_block(64)
            + toast("+1 Space", "Carrying 8. Bank them at the Ark.", "good") + VESSEL)


def s_banked():
    split = f'30 of {GOAL} aboard. &nbsp; Space 3 &nbsp; Time 2 &nbsp; Mass 2'
    return world("raid") + marquee(30, 0) + hull_block(64) + toast(f"Banked {CARRY}", split, "good") + VESSEL


def s_attack():
    return (world("attack") + diamond(1010, 330, 2.2) + diamond(700, 380, 1) + edge_arrow("right", 600, 1)
            + marquee() + hull_block(41, True, True)
            + toast("Ark under attack", "3 raiders near the hull. Hunt what is biting it.", "threat") + VESSEL)


def s_leak():
    return (world("attack") + diamond(1010, 330, 2.2) + marquee(21) + hull_block(27, True, True)
            + toast("The hold is leaking", "The hull is below 30%. Banked crystals spill one by one: guard the Ark and grab them back.", "threat")
            + VESSEL)


def s_sail():
    return (world("sail") + marquee(clock="0:05") + hull_block(64) + toast("The Ark sails in 5", "Bank what you carry or lose it.")
            + ring(5, f"Bank the +{CARRY} or lose it") + VESSEL)


def s_kaiju():
    kaiju = fauna(1250, 520, 6) + diamond(1250, 430, 2.6)
    return (r114.ground(r114.G["through"], "50% 50%", .2) + ark(520, 420, 520, -3) + kaiju
            + marquee(30, 0, 4, "Ark bound for Tollmoor", None, "", now=3)
            + hull_block(58) + toast("Kaiju in open water", "A giant waits between the cells for the Ark's hull. Bring it down.", "threat")
            + VESSEL)


def s_stray():
    return (r114.ground(r114.G["trail"], "50% 50%", .22) + ark(1500, 520, 220, 0, .8) + marquee(26, 4) + hull_block(64)
            + toast("Return to the Ark: 3", "Turn back or lose the 4 crystals you carry.", "threat") + VESSEL)


def s_goal():
    return (world("raid") + marquee(GOAL, 0, 4, "Tollmoor", True, "0:12", now=3, count_col=LIME) + hull_block(58)
            + toast(f"{GOAL} crystals. Bring them home.", "Pause and choose Complete and set sail, or keep raiding for more.", "good") + VESSEL)


def s_pause():
    big = "height:72px;font-size:28px;justify-content:flex-start;width:100%;"
    buttons = (f'<div style="display:flex;flex-direction:column;gap:14px;width:520px">'
               f'<button class="btn def" type="button" style="{big}">{kc("B")}Resume</button>'
               f'<button class="btn cta" type="button" style="{big}">{kc("A")}Complete and set sail</button>'
               f'<button class="btn def" type="button" style="{big}">Settings</button>'
               f'<button class="btn def" type="button" style="{big}color:{SOFT}">Leave voyage</button></div>')
    left = stack(420, 210, (f'<span class="hd" style="font-size:{FLOOR}px;color:{SOFT}">Paused - Crystal Run</span>'
                            f'<span class="hd" style="font-size:72px;line-height:1">Goal met</span>'
                            f'<span style="font-size:{FLOOR}px;color:{SOFT};max-width:540px;line-height:1.4">{GOAL} of {GOAL} aboard. Set sail for home now, or keep raiding for more.</span>'
                            f'<div style="height:12px"></div>{buttons}'), 560, 14)
    stats = (("Crystals aboard", f"{GOAL}"), ("Ark hull", "58%"), ("Cell", "4 of 6"), ("Time", "6:42"))
    right = stack(1080, 210, (f'<span class="hd" style="font-size:{FLOOR}px;color:{SOFT}">This voyage</span>' + "".join(
        f'<div class="row" style="justify-content:space-between;width:420px;padding:12px 0;box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)">'
        f'<span style="font-size:{FLOOR}px;color:{SOFT}">{a}</span><span class="big" style="font-size:36px">{b}</span></div>'
        for a, b in stats) + f'<div style="margin-top:22px">{route_h(420, 3)}</div>'), 420, 4)
    return world("pause") + left + right


def s_home():
    fig = (f'{i_gem(34)}<span class="big" style="font-size:{COUNT}px;line-height:1">{GOAL}</span>'
           f'<span style="font-size:36px;color:{SOFT};align-self:flex-end;margin-bottom:8px">aboard, at stake</span>')
    sub = (f'<div class="row" style="gap:26px;font-size:{SUB}px;white-space:nowrap">{route_h(300, now=4)}'
           f'<span>Docking at Home banks it all</span></div>')
    return (r114.ground(r114.G["trail"], "50% 50%", .18) + ark(700, 380, 520) + fauna(1300, 460, 1.2) + marquee(figure=fig, figure_sub=sub)
            + hull_block(58) + hint("Escort the Ark home. Docking banks everything.") + VESSEL)


def s_escort():
    fig = (f'{i_ark(56)}<span class="big" style="font-size:{COUNT}px;line-height:1;color:{RED2}">52%</span>'
           f'<span style="font-size:36px;color:{SOFT};align-self:flex-end;margin-bottom:8px">Ark hull</span>')
    sub = (f'<div style="display:flex;flex-direction:column;align-items:center;gap:12px">{hbar(520, 22, 52, RED2)}'
           f'<div class="row" style="gap:26px;font-size:{SUB}px;white-space:nowrap">{route_h(300, now=2)}'
           f'<span>3/5 &nbsp;Scarab Reef - <span style="color:{RED2}">hostile</span></span></div></div>')
    return (world("attack") + diamond(1010, 330, 2.2) + edge_arrow("left", 560, 2)
            + marquee(figure=fig, figure_sub=sub)
            + toast("Assault on the Ark", "Drive the assault off: 4 / 6 attackers near the Ark", "threat") + VESSEL)


def end_choice(key, title, line, cta=False):
    cls = "cta" if cta else "def"
    return (f'<div style="display:flex;flex-direction:column;gap:10px;width:400px">'
            f'<button class="btn {cls}" type="button" style="height:72px;font-size:28px;width:100%">{kc(key)}{title}</button>'
            f'<span style="font-size:{FLOOR}px;color:{SOFT};text-align:center;line-height:1.3">{line}</span></div>')


def s_score():
    head = stack(0, 60, (f'<span class="hd" style="font-size:36px;color:{SOFT}">Voyage complete</span>'
                         f'<span class="hd" style="font-size:72px;line-height:1">{GOAL} of {GOAL} crystals stolen</span>'
                         f'<div class="row" style="gap:28px;font-size:36px">{flower("Space", 30)} 17 {flower("Mass", 30)} 14 {flower("Time", 30)} 11 {flower("Charge", 30)} 8</div>'
                         f'<span style="font-size:{FLOOR}px;color:{SOFT}">Crystal Run to The Maw - 4 cells - 6:42</span>'), W, 14, "center")
    rows = stack(460, 380, seats_rows(1000, 28).replace("height:64px", "height:92px")
                 .replace('<span class="cap2">', f'<span class="cap2" style="font-size:{FLOOR}px">'), 1000)
    buttons = (f'<div class="abs row" style="left:0;right:0;top:820px;justify-content:center;gap:30px;align-items:flex-start">'
               + end_choice("X", "Finish here", "Bank it all now and end the voyage")
               + end_choice("B", "Sail home", "Escort the Ark home for 2x the crystals")
               + end_choice("A", "Venture further", "Next run: 60 crystals, 7 cells", True) + "</div>")
    return dock_world() + head + rows + buttons


def s_lost():
    head = stack(0, 300, (f'<span class="hd" style="font-size:36px;color:{RED2}">The Ark is lost</span>'
                          f'<span class="hd" style="font-size:72px;line-height:1">Everything aboard went down with it</span>'
                          f'<span style="font-size:{FLOOR}px;color:{SOFT}">Crystal Run to The Maw - 3 cells - 5:10 - 31 crystals lost</span>'), W, 18, "center")
    btn = (f'<div class="abs row" style="left:0;right:0;top:640px;justify-content:center">'
           + end_choice("A", "Back to Voyages", "Return to the Voyages page", True) + "</div>")
    return world("pause") + head + btn


def s_notes():
    rows = [
        ("One HUD everywhere", "The game shows this 1920 x 1080 layout on every device; a phone shows it at about 0.40. So no word on the voyage HUD is under 28 units (about 11 pt on a phone). The count is 88 (was 54), toasts 44 / 28 (were 28 / 19)."),
        ("The Ark moves to the top left", "A wide bar with the breach (red) and leak (dashed) lines, ARK HULL and its figure over it; red with UNDER ATTACK while hit. The tall rail at mid left sat where a left thumb sweeps."),
        ("What you carry sits beside the count", "+7 next to 23 / 50. The mid-screen Carrying chip goes."),
        ("The toast spot is never empty", "With no toast up it holds the next thing to do, dimmed (Raid, Home). The flower split leaves the marquee and rides the BANKED toast, when it changes."),
        ("The countdown says what is at stake", "Bank the +7 or lose it, under the ring."),
        ("Escort and the way home", "An escort's score is its hull, so the marquee carries the hull and its bar and the top-left block is not drawn. On the way home the marquee is what is aboard and at stake."),
        ("Threats stay in the world", "The red diamonds, red lights and edge arrow with pips are ArkAttackerMarks, already built: 3D in the world, no 2D arrows (LOCKED rule)."),
        ("Not built", "Pause is the shared pause window (drawn bigger here). Crew trust, pilot XP and shards stay off the end board until those systems exist. The sized-up HUD is parked on a branch until this round is picked."),
    ]
    body = (f'<span class="abs hd" style="left:60px;top:50px;font-size:44px">Rev {REV} notes</span>'
            + stack(60, 140, "".join(
                f'<div style="display:flex;gap:30px;width:1800px"><span class="hd" style="font-size:22px;width:440px;flex:none;color:{INK}">{a}</span>'
                f'<span style="font-size:20px;color:{SOFT};line-height:1.45">{b}</span></div>' for a, b in rows), 1800, 22))
    return body


SCREENS = {
    "Intro": ("The intro: course title and objective, no HUD", s_intro),
    "SetOff": ("Setting off: the goal toast, the Ark's hull top left", s_setoff),
    "Raid": ("Raiding a hostile cell: count, carried, route, clock, the quiet hint", s_raid),
    "Pickup": ("A crystal picked up: the running +1 toast", s_pickup),
    "Banked": ("Banked at the Ark: the toast with what was banked by element", s_banked),
    "Attack": ("The Ark under attack: hull red, UNDER ATTACK, raiders marked in the world", s_attack),
    "Leak": ("The hold leaking: hull under 30%", s_leak),
    "Sail": ("The raid countdown: the ring and what is at stake", s_sail),
    "Kaiju": ("Between cells: a kaiju in open water", s_kaiju),
    "Stray": ("Too far from the Ark: the recall countdown", s_stray),
    "Goal": ("Goal met: the call to set sail", s_goal),
    "Pause": ("Pause, goal met: Complete and set sail", s_pause),
    "Home": ("Sailing home: what is aboard and at stake", s_home),
    "Escort": ("Escort the Ark: the hull is the score, an assault on", s_escort),
    "Score": ("The end board, goal met: Finish here, Sail home, Venture further", s_score),
    "Lost": ("The Ark lost", s_lost),
}


def board(key):
    title, fn = SCREENS[key]
    return r100.page(f"Rev {REV} {key} - {title}", fn(), r113.LOGIC)


def notes():
    html = r100.page(f"Rev {REV} - notes: what changed from the build, the type floor, what is not built", s_notes(), r113.LOGIC)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


if __name__ == "__main__":
    for key in SCREENS:
        (ROOT / f"R{REV}{key}.dc.html").write_text(board(key), encoding="utf-8")
    (ROOT / f"R{REV}Notes.dc.html").write_text(notes(), encoding="utf-8")
    print("wrote", len(SCREENS) + 1, "boards to", ROOT)
