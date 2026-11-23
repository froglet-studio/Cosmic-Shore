#!/usr/bin/env python3
"""Rev 120 boards: every in-game voyage screen in Rev 119 B, "On the Ark" (Frogma tab "Voyages playtest").

Shombith, 2026-11-20, on Rev 119: "I like B with the HUD above the ark, thats cool".

B: the Ark carries its own numbers. A lit plate over the hull, in the world (a world fact shown in the world), holds the
crystals aboard, the hull as lit segments and one state line (carried, the sail countdown, under attack, leaking). It
turns red when the Ark is hit and lime when the goal is met. A small chip top left carries the same numbers for when the
Ark is off screen, and the toasts are a feed under the chip. The game's own text sizes throughout (no Rev 118 sizes).

Sixteen screens, in the order a run meets them, plus notes:
  Intro SetOff Raid Pickup Banked Attack Leak Sail Kaiju Stray Goal Pause Home Escort Score Lost

    python3 Docs/MenuRedesignB/rounds/r120_voyage_hud_ark.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "120"
_spec = importlib.util.spec_from_file_location("r116", HERE / "r116_voyage_hud.py")
r116 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev, "lib"]
_spec.loader.exec_module(r116)
r114, r113, r100 = r116.r114, r116.r113, r116.r100

REV = int(_rev)
ROOT = r116.ROOT
W, H = 1920, 1080
INK, MUTE, SOFT, LINE, PANEL = r116.INK, r116.MUTE, r116.SOFT, r116.LINE, r116.PANEL
LIME, RED, RED2 = r116.LIME, r116.RED, r116.RED2
GOAL, BANKED, CARRY = r116.GOAL, r116.BANKED, r116.CARRY
ark, fauna, gem, flower, world, dock_world = r116.ark, r116.fauna, r116.gem, r116.flower, r116.world, r116.dock_world
i_gem, i_ark, i_clock, kc = r116.i_gem, r116.i_ark, r116.i_clock, r116.kc
route_h, stack, seats_rows = r116.route_h, r116.stack, r116.seats_rows
VESSEL = r116.VESSEL

HOLO, HOLO_INK, HOLO_SOFT = "#9FE8FF", "#E6F7FF", "#9FB4E6"
TOASTSUB = 16
MOOD = {"event": INK, "threat": RED2, "good": LIME}


# ── the pieces ──────────────────────────────────────────────────────────────────────────────────────────────────────
def plate(cx, top, count, hull, mood="calm", line="", goal=f"/ {GOAL}", figure_icon="gem", s=1.0):
    """The lit plate over the Ark, in the world: faceted, tilted toward the camera, scaled with distance (s).
    mood: calm (sea blue), hot (red: under attack, leaking), good (lime: goal met)."""
    col = {"calm": HOLO, "hot": RED, "good": LIME}[mood]
    glow = {"calm": "rgba(0,212,255,.25)", "hot": "rgba(255,75,58,.38)", "good": "rgba(153,255,128,.3)"}[mood]
    bg = ("linear-gradient(180deg,rgba(120,20,20,.6),rgba(50,0,10,.7))" if mood == "hot"
          else "linear-gradient(180deg,rgba(40,70,140,.55),rgba(0,20,60,.65))")
    segs = 16
    lit = round(segs * hull / 100)
    seg = "".join(f'<span style="width:9px;height:14px;background:{col if i < lit else "rgba(67,76,137,.55)"};'
                  f'{"box-shadow:0 0 8px " + col if i < lit else ""}"></span>' for i in range(segs))
    icon = i_gem(18, col) if figure_icon == "gem" else i_ark(30, col)
    num_col = LIME if mood == "good" else HOLO_INK
    ln = f'<span style="font-size:15px;color:{RED2 if mood == "hot" else LIME if mood == "good" else HOLO_SOFT};letter-spacing:.08em;white-space:nowrap">{line}</span>' if line else ""
    return (f'<div class="abs" style="left:{cx - 170}px;top:{top - 150}px;width:340px;height:150px;perspective:600px;'
            f'transform:scale({s});transform-origin:50% 100%;display:flex;align-items:flex-end;justify-content:center">'
            f'<div style="transform:rotateX(28deg);transform-origin:50% 100%;display:flex;flex-direction:column;align-items:center;gap:7px;'
            f'padding:12px 60px 14px;background:{bg};clip-path:polygon(14% 0,86% 0,100% 50%,86% 100%,14% 100%,0 50%);box-shadow:0 0 30px {glow}">'
            f'<div class="row" style="gap:10px;align-items:center">{icon}'
            f'<span class="big" style="font-size:34px;line-height:1;color:{num_col};text-shadow:0 0 10px {glow}">{count}</span>'
            f'<span class="big" style="font-size:20px;color:{HOLO_SOFT}">{goal}</span></div>'
            f'<div class="row" style="gap:3px">{seg}</div>{ln}</div>'
            f'<span class="abs" style="left:169px;bottom:-26px;width:2px;height:26px;background:linear-gradient({col},rgba(0,0,0,0))"></span></div>')


def chip(banked=BANKED, carry=CARRY, hull=64, mood="calm", clock="0:18", lime_clock=False, extra="", ring=False):
    """Top left: the same numbers as the plate, for when the Ark is off screen."""
    hc = RED2 if mood == "hot" else INK
    cc = LIME if mood == "good" else INK
    c = f'<span class="big" style="font-size:18px;color:{LIME}">+{carry}</span>' if carry else ""
    clk = (f'<span class="row" style="gap:8px">{i_clock(14, LIME if lime_clock else INK)}'
           f'<span class="big" style="font-size:20px;color:{LIME if lime_clock else INK}">{clock}</span></span>') if clock else ""
    edge = f"box-shadow:inset 2px 0 0 {hc}" + (f",0 0 0 1px {INK}" if ring else "")
    return (f'<div class="abs row" style="left:40px;top:36px;gap:16px;padding:8px 16px;background:{PANEL};{edge}">'
            f'<span class="row" style="gap:8px">{i_gem(14, cc)}<span class="big" style="font-size:20px;color:{cc}">{banked}/{GOAL}</span>{c}</span>'
            f'<span class="row" style="gap:8px">{i_ark(22, hc)}<span class="big" style="font-size:20px;color:{hc}">{hull}%</span></span>'
            f'{clk}{extra}</div>')


def toast(head, sub="", mood="event", top=96):
    col = MOOD[mood]
    s = f'<span style="font-size:{TOASTSUB}px;color:{INK};line-height:1.35">{sub}</span>' if sub else ""
    return (f'<div class="abs tin" style="left:40px;top:{top}px;display:flex;flex-direction:column;gap:3px;padding:8px 16px 10px;'
            f'max-width:520px;background:linear-gradient(90deg,rgba(0,4,31,.85),rgba(0,4,31,0));box-shadow:inset 2px 0 0 {col}">'
            f'<span class="hd" style="font-size:20px;color:{col}">{head}</span>{s}</div>')


def diamond(x, y, s=1.0):
    w, h = 34 * s, 52 * s
    return (f'<svg class="abs" style="left:{x - w / 2:.0f}px;top:{y - h:.0f}px" width="{w:.0f}" height="{h:.0f}" viewBox="0 0 34 52" aria-hidden="true">'
            '<path d="M17 52L0 20 17 0z" fill="#FF4B3A"></path><path d="M17 52L34 20 17 0z" fill="#B8231A"></path>'
            '<path d="M0 20h34" stroke="#FF8A7C" stroke-width="1.5"></path></svg>')


def edge_arrow(side="right", y=600, pips=1):
    x = W - 60 if side == "right" else 60
    rot = 0 if side == "right" else 180
    p = "".join(f'<span style="width:10px;height:10px;background:{RED};transform:rotate(45deg)"></span>' for _ in range(pips))
    return (f'<svg class="abs" style="left:{x - 30}px;top:{y - 30}px;transform:rotate({rot}deg)" width="60" height="60" viewBox="0 0 80 80" aria-hidden="true">'
            '<path d="M10 28h34V12l30 28-30 28V52H10z" fill="#FF4B3A"></path></svg>'
            f'<div class="abs row" style="left:{x - 20}px;top:{y + 36}px;gap:8px">{p}</div>')


def letterbox():
    return ('<div class="abs" style="left:0;right:0;top:0;height:120px;background:#000"></div>'
            '<div class="abs" style="left:0;right:0;bottom:0;height:120px;background:#000"></div>')


def choice(key, title, line, cta=False):
    return (f'<div style="display:flex;flex-direction:column;gap:8px;width:300px">'
            f'<button class="btn {"cta" if cta else "def"}" type="button" style="height:60px;font-size:20px;width:100%">{kc(key)}{title}</button>'
            f'<span style="font-size:15px;color:{SOFT};text-align:center;line-height:1.35">{line}</span></div>')


# ── the screens ─────────────────────────────────────────────────────────────────────────────────────────────────────
RAID_ARK = (960, 330)


def s_intro():
    return (world("raid") + letterbox()
            + stack(0, 400, (f'<span class="hd" style="font-size:20px;color:{SOFT}">Crystal Run to The Maw</span>'
                             f'<span class="hd" style="font-size:56px;line-height:1">Steal {GOAL} crystals</span>'
                             f'<span style="font-size:20px;color:{INK}">Before the Ark reaches The Maw. Keep its hull above 10%.</span>'), W, 14, "center")
            + f'<div class="abs row" style="right:60px;bottom:150px;gap:10px;font-size:16px;color:{SOFT}">{kc("A")}Hold to skip</div>')


def s_setoff():
    return (world("raid") + plate(*RAID_ARK, 0, 100, line="Garland, 1 of 6") + chip(0, 0, 100, clock="")
            + toast(f"Steal {GOAL} crystals", "Before the Ark reaches The Maw. Keep its hull above 10%.") + VESSEL)


def s_raid():
    return world("raid") + plate(*RAID_ARK, BANKED, 64, line=f"+{CARRY} to bank") + chip() + VESSEL


def s_pickup():
    return (world("raid") + plate(*RAID_ARK, BANKED, 64, line="+8 to bank") + chip(carry=8)
            + toast("+1 Space", "Carrying 8. Bank them at the Ark.", "good") + VESSEL)


def s_banked():
    return (world("raid") + plate(*RAID_ARK, 30, 64, line="Banked 7") + chip(30, 0)
            + toast("Banked 7", "30 of 50 aboard. Space 3, Time 2, Mass 2.", "good") + VESSEL)


def s_attack():
    return (world("attack") + diamond(1010, 330, 2.2) + diamond(700, 380, 1) + edge_arrow("right", 600, 1)
            + plate(760, 380, BANKED, 41, "hot", "Under attack") + chip(hull=41, mood="hot")
            + toast("Ark under attack", "3 raiders near the hull. Hunt what is biting it.", "threat") + VESSEL)


def s_leak():
    return (world("attack") + diamond(1010, 330, 2.2) + plate(760, 380, 21, 27, "hot", "Leaking: guard the hold")
            + chip(21, CARRY, 27, "hot")
            + toast("The hold is leaking", "The hull is below 30%. Banked crystals spill one by one: guard the Ark and grab them back.", "threat")
            + VESSEL)


def s_sail():
    return (world("sail") + plate(970, 340, BANKED, 64, line=f"Sails in 5. Bank the +{CARRY}") + chip(clock="0:05", lime_clock=True)
            + toast("The Ark sails in 5", "Bank what you carry or lose it.") + VESSEL)


def s_kaiju():
    return (r114.ground(r114.G["through"], "50% 50%", .2) + ark(520, 420, 520, -3) + fauna(1250, 520, 6) + diamond(1250, 430, 2.6)
            + plate(780, 420, 30, 58, line="Bound for Tollmoor") + chip(30, 0, 58, clock="")
            + toast("Kaiju in open water", "A giant waits between the cells for the Ark's hull. Bring it down.", "threat") + VESSEL)


def s_stray():
    return (r114.ground(r114.G["trail"], "50% 50%", .22) + ark(1500, 520, 220, 0, .8)
            + plate(1610, 520, 26, 64, line="Return: 3", s=.62) + chip(26, 4, ring=True)
            + toast("Return to the Ark: 3", "Turn back or lose the 4 crystals you carry.", "threat") + VESSEL)


def s_goal():
    return (world("raid") + plate(*RAID_ARK, GOAL, 58, "good", "Goal met. Bring them home.") + chip(GOAL, 0, 58, "good", "0:12")
            + toast(f"{GOAL} crystals. Bring them home.", "Pause and choose Complete and set sail, or keep raiding for more.", "good") + VESSEL)


def s_pause():
    return r116.a_pause().replace("Escort the Ark", "Crystal Run")


def s_home():
    return (r114.ground(r114.G["trail"], "50% 50%", .18) + ark(700, 380, 520) + fauna(1300, 460, 1.2)
            + plate(960, 380, GOAL, 58, line="Aboard. Docking banks it all", goal="aboard")
            + chip(GOAL, 0, 58, clock="")
            + toast("Escort the Ark home", "Docking at Home banks everything aboard.") + VESSEL)


def s_escort():
    return (world("attack") + diamond(1010, 330, 2.2) + edge_arrow("left", 560, 2)
            + plate(760, 380, "52%", 52, "hot", "Assault: 4 of 6 near", goal="hull", figure_icon="ark")
            + chip(0, 0, 52, "hot", clock="")
            .replace(f'<span class="row" style="gap:8px">{i_gem(14, INK)}<span class="big" style="font-size:20px;color:{INK}">0/{GOAL}</span></span>', "")
            + toast("Assault on the Ark", "Drive the assault off: 4 / 6 attackers near the Ark.", "threat") + VESSEL)


def s_score():
    head = stack(0, 70, (f'<span class="cap2">Crystal Run  ·  The Maw  ·  4 cells  ·  6:42</span>'
                         f'<span class="hd" style="font-size:52px">Voyage complete</span>'
                         f'<div class="row" style="gap:16px">{i_gem(30, INK)}<span class="big" style="font-size:80px;line-height:1">{GOAL}</span>'
                         f'<span style="font-size:20px;color:{SOFT};align-self:flex-end;margin-bottom:12px">crystals stolen</span></div>'
                         f'<div class="row" style="gap:24px;font-size:18px">{flower("Space", 20)} 17 {flower("Mass", 20)} 14 {flower("Time", 20)} 11 {flower("Charge", 20)} 8</div>'), W, 12, "center")
    rows = stack(510, 450, seats_rows(900), 900)
    buttons = (f'<div class="abs row" style="left:0;right:0;top:820px;justify-content:center;gap:24px;align-items:flex-start">'
               + choice("X", "Finish here", "Bank it all now and end the voyage")
               + choice("B", "Sail home", "Escort the Ark home for 2x the crystals")
               + choice("A", "Venture further", "Next run: 60 crystals, 7 cells", True) + "</div>")
    return dock_world() + ark(1360, 120, 420, 0, .5) + head + rows + buttons


def s_lost():
    head = stack(0, 330, (f'<span class="hd" style="font-size:20px;color:{RED2}">The Ark is lost</span>'
                          f'<span class="hd" style="font-size:48px;line-height:1">Everything aboard went down with it</span>'
                          f'<span style="font-size:17px;color:{SOFT}">Crystal Run to The Maw  ·  3 cells  ·  5:10  ·  31 crystals lost</span>'), W, 14, "center")
    btn = (f'<div class="abs row" style="left:0;right:0;top:580px;justify-content:center">'
           + choice("A", "Back to Voyages", "Return to the Voyages page", True) + "</div>")
    return world("pause") + head + btn


def s_notes():
    rows = [
        ("The Ark wears its numbers", "A lit plate over the hull, in the world: crystals aboard, the hull as 16 lit segments, and one state line (+7 to bank, Sails in 5, Under attack, Leaking, Goal met). It is a game object riding the Ark, so it scales with distance and hides behind things like the Ark does."),
        ("Three moods", "Sea blue at rest, red while the Ark is hit or leaking, lime when the goal is met. All three come from the colour set (domain-free: the Ark's own colours)."),
        ("The chip, top left", "The same numbers in one line (crystals, carried, hull, clock), for when the Ark is behind you or far away. On Stray it gets an outline."),
        ("Toasts", "A feed under the chip, 20 / 16, sliding in and out. The centre of the screen stays clear for flying."),
        ("Escort and the way home", "On an escort the plate's number is the hull (it is the score); on the way home it is what is aboard."),
        ("Unchanged", "Pause and the end board keep the shared windows at normal sizes. Threat marks stay ArkAttackerMarks in the world. The vessel HUD keeps its corner."),
        ("To build", "VoyageHud's objective box becomes the chip and the toast feed; a world-space plate parented to the Ark (billboarded, lit like the Ark, colour-set colours) shows the rest. Rev 118's parked code is replaced."),
    ]
    return (f'<span class="abs hd" style="left:60px;top:50px;font-size:36px">Rev {REV} notes</span>'
            + stack(60, 130, "".join(
                f'<div style="display:flex;gap:30px;width:1800px"><span class="hd" style="font-size:20px;width:380px;flex:none;color:{INK}">{a}</span>'
                f'<span style="font-size:19px;color:{SOFT};line-height:1.45">{b}</span></div>' for a, b in rows), 1800, 22))


SCREENS = {
    "Intro": ("The intro: course title and objective, no HUD", s_intro),
    "SetOff": ("Setting off: the plate lights, the goal toast", s_setoff),
    "Raid": ("Raiding a hostile cell: the plate over the Ark, the chip", s_raid),
    "Pickup": ("A crystal picked up: +1 toast, the plate's to-bank line", s_pickup),
    "Banked": ("Banked at the Ark: the plate counts up, the split in the toast", s_banked),
    "Attack": ("The Ark under attack: the plate turns red", s_attack),
    "Leak": ("The hold leaking: hull under 30%", s_leak),
    "Sail": ("The raid countdown on the plate", s_sail),
    "Kaiju": ("Between cells: a kaiju in open water", s_kaiju),
    "Stray": ("Too far from the Ark: the plate small and far, the chip outlined", s_stray),
    "Goal": ("Goal met: the plate turns lime", s_goal),
    "Pause": ("Pause, goal met: Complete and set sail", s_pause),
    "Home": ("Sailing home: what is aboard", s_home),
    "Escort": ("Escort the Ark: the hull is the score", s_escort),
    "Score": ("The end board: Finish here, Sail home, Venture further", s_score),
    "Lost": ("The Ark lost", s_lost),
}


def board(key):
    title, fn = SCREENS[key]
    return r100.page(f"Rev {REV} {key} - {title}", fn(), r113.LOGIC)


def notes():
    html = r100.page(f"Rev {REV} - notes: B On the Ark on every screen", s_notes(), r113.LOGIC)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


if __name__ == "__main__":
    for key in SCREENS:
        (ROOT / f"R{REV}{key}.dc.html").write_text(board(key), encoding="utf-8")
    (ROOT / f"R{REV}Notes.dc.html").write_text(notes(), encoding="utf-8")
    print("wrote", len(SCREENS) + 1, "boards to", ROOT)
