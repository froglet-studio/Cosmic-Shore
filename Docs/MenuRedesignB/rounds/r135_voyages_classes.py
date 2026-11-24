#!/usr/bin/env python3
"""Rev 135 boards: the full-screen HyperSea kept on the quay board, and the vessel classes screen (Frogma tab "Voyages",
Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-24, on Rev 134: "We can build these. ... in the board in the first screen please make sure the
fullscreen of hypersea feature stays, show me the screen which differentiates vessels as per their classes and what to
choose and how effective they are in the missions. That screen show me before you start building".

  BoardFull    Rev 134's quay board with the Full screen control on its chart (the built Rev 111 feature, kept)
  FullScreen   the HyperSea full screen as built (zoom inside the map, Back from full screen, Hide), now carrying the
               four givers on the right, their pins, the lane, the events next door and the rival crew
  ClassesA     the fleet by class: seven classes x four voyages, how well each class does in each, the vessels in each
  ClassesB     this voyage's shelf (recommended): every vessel sorted Best / Good / Poor for the voyage you are on, what
               each class does there, who the AI can fly, and the one swap that lifts your squad's fit
  ClassesPick  ClassesB inside the seat picker: choosing Seat 3's hull, fit and pay change before you take it

Effectiveness is a label for voyages, never a kit change: Best is a class the giver asks for (it adds to the squad fit
and the pay bonus); Good does the work without the bonus; Poor has the least to do there.

    python3 Docs/MenuRedesignB/rounds/r135_voyages_classes.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "135"
_spec = importlib.util.spec_from_file_location("r133", HERE / "r133_voyages_m2.py")
r133 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev]
_spec.loader.exec_module(r133)
r108 = r133.r108

REV = int(_rev)
ROOT = r133.ROOT
INK, MUTE, SOFT, LIME, RED2, SEL = r133.INK, r133.MUTE, r133.SOFT, r133.LIME, r133.RED2, r133.SEL
HULL, CREW, JOB, JOBS, SEATS, CLASS = r133.HULL, r133.CREW, r133.JOB, r133.JOBS, r133.SEATS, r133.CLASS
DANGER, DOCK, ZONE, RIVAL = r133.DANGER, r133.DOCK, r133.ZONE, r133.RIVAL
glyph, cls_tag, danger, face, gems, window, page, kc = r133.glyph, r133.cls_tag, r133.danger, r133.face, r133.gems, r133.window, r133.page, r133.kc
sea_map, place, zone, lane_svg, cell, event_mark, a_pin, notes_page = (r133.sea_map, r133.place, r133.zone, r133.lane_svg, r133.cell,
                                                                        r133.event_mark, r133.a_pin, r133.notes_page)
EXPAND = r108.EXPAND

# ── the fleet by class (the Milestone 2 design's sort; Garrett's Brood Rush measurement wins where it disagrees) ──────
FLEET = {
    "Guardian": ["Serpent", "Grizzly", "Shrike"],
    "Protector": ["Scarab", "Orca", "Thresher"],
    "Destroyer": ["Sparrow", "Dolphin", "Swordfish", "Rhino"],
    "Cultivator": ["Squirrel", "Urchin", "Tiger Moth", "Goby & Pistol"],
    "Hunter": ["Manta", "Brittle Star", "Honeyguide"],
    "Shepherd": ["Angler", "Jacks", "Starling", "Jewel Wasp"],
    "Pathfinder": ["Meerkat", "Stoat", "Wrasse"],
}
AI_READY = {"Serpent", "Scarab", "Sparrow", "Dolphin", "Squirrel"}       # vessels with an escort role today (*Role.cs)
ORDER = list(FLEET)
ROLE = {
    "Guardian": "Holds a place: walls and shells around the Ark",
    "Protector": "Takes the hits meant for the Ark",
    "Destroyer": "Breaks mass, hulls and whatever is in the way",
    "Cultivator": "Grows and gathers: crystal, mass, a living wall",
    "Hunter": "Finds one target and runs it down",
    "Shepherd": "Moves fauna and crowds where it wants them",
    "Pathfinder": "Fast, reads the sea, gets there first",
}
# how well each class does in each voyage: 3 Best (the giver asks for it), 2 Good, 1 Poor
EFF = {
    "salvage": {"Cultivator": 3, "Pathfinder": 3, "Protector": 3, "Destroyer": 2, "Hunter": 2, "Shepherd": 2, "Guardian": 1},
    "escort": {"Protector": 3, "Guardian": 3, "Destroyer": 3, "Shepherd": 2, "Hunter": 2, "Cultivator": 1, "Pathfinder": 1},
    "bounty": {"Hunter": 3, "Pathfinder": 3, "Destroyer": 3, "Shepherd": 2, "Protector": 1, "Guardian": 1, "Cultivator": 1},
    "core": {"Guardian": 3, "Cultivator": 3, "Protector": 3, "Destroyer": 2, "Shepherd": 2, "Hunter": 1, "Pathfinder": 1},
}
for k, j in JOB.items():                       # the table agrees with each giver's ask in the Codex
    assert sorted(c for c, v in EFF[k].items() if v == 3) == sorted(j[7]), k
# what each class does in The Glassmere Shatter (loose crystal, a rival racing you, teeth in the water)
SALVAGE_DOES = {
    "Cultivator": "Sweeps loose crystal fastest and stows it in one pass",
    "Pathfinder": "First to the far drifts before the rival crew",
    "Protector": "Keeps the Ark whole while the hold is open",
    "Destroyer": "Breaks the rival's hulls off your crystal",
    "Hunter": "Picks off what hunts the gatherers",
    "Shepherd": "Herds the fauna away from the drifts",
    "Guardian": "Holds one spot; salvage keeps moving",
}
FITWORD = {3: "Best", 2: "Good", 1: "Poor"}
SQUAD = {s[2]: s for s in SEATS}               # Sparrow, Squirrel, Dolphin
J = JOB["salvage"]


def pips(n, s=12):
    return ('<span class="row" style="gap:3px">' + "".join(
        f'<span style="width:{s}px;height:{s}px;background:{INK if i < n else "rgba(67,76,137,.6)"}"></span>' for i in range(3)) + '</span>')


def fit_word(n, size=15):
    col = INK if n == 3 else SOFT if n == 2 else MUTE
    return f'<span class="row" style="gap:10px;font-size:{size}px;color:{col};white-space:nowrap">{pips(n)}{FITWORD[n]}</span>'


def tag(word, col=INK, line=None):
    edge = f";box-shadow:inset 0 0 0 1px {line}" if line else ""
    return f'<span class="cap" style="font-size:14px;color:{col};padding:2px 6px;white-space:nowrap{edge}">{word}</span>'


def vessel_name(v, size=14):
    ai = tag("AI", SOFT, "rgba(184,192,240,.5)") if v in AI_READY else ""
    you = f';color:{INK};box-shadow:inset 0 -1px 0 {SEL}' if v in SQUAD else f';color:{SOFT}'
    return f'<span class="row" style="gap:6px;font-size:{size}px;white-space:nowrap"><span style="padding-bottom:2px{you}">{v}</span>{ai}</span>'


def legend(x, y):
    return (f'<div class="abs row" style="left:{x}px;top:{y}px;gap:30px;font-size:14px;color:{SOFT}">'
            f'{fit_word(3, 14)}<span>the giver asks for it: adds to squad fit and pay</span>{fit_word(2, 14)}<span>does the work, no bonus</span>'
            f'{fit_word(1, 14)}<span>little to do there</span>'
            f'<span class="row" style="gap:8px">{tag("AI", SOFT, "rgba(184,192,240,.5)")}the AI can fly it in a seat</span>'
            f'<span class="row" style="gap:8px"><span style="width:26px;height:2px;background:{SEL}"></span>in your squad</span></div>')


# ── BoardFull: the quay with its Full screen control ────────────────────────────────────────────────────────────────
def board_full():
    html = r133.b_board()
    btn = (f'<button class="btn sm soft abs" type="button" style="left:1640px;top:204px;width:224px;gap:12px;z-index:4">'
           f'{EXPAND}Full screen</button>')
    html = html.replace('<span class="abs lab" style="left:20px;top:18px">On the chart</span>',
                        '<span class="abs lab" style="left:20px;top:18px">On the chart</span>', 1)
    assert "Take the job" in html
    see = (f'<button class="btn sm soft abs" type="button" style="left:900px;top:790px;width:260px;gap:12px;z-index:4">'
           f'{kc("Y")}See every class</button>')
    btn += see
    i = html.rindex("</x-dc>")
    html = html[:i] + btn + html[i:]
    return html.replace(f"<title>Rev {REV} B Board - The Quay", f"<title>Rev {REV} BoardFull - The Quay", 1)


# ── FullScreen: the HyperSea, edge to edge ──────────────────────────────────────────────────────────────────────────
TIERS = [("Galaxy", "10,000 ly"), ("Arm", "1,000 ly"), ("Sector", "200 ly"), ("Neighbourhood", "20 ly"), ("Cell", "5 ly")]


def zoom_row(x, y, on=2):
    tiers = "".join(
        f'<button class="pick" type="button" aria-pressed="{str(i == on).lower()}" style="height:52px;padding:0 16px;display:flex;flex-direction:column;'
        f'justify-content:center;gap:2px;background:none;{"box-shadow:inset 0 -2px 0 " + SEL if i == on else ""}">'
        f'<span style="font-size:15px;color:{INK if i == on else SOFT}">{n}</span><span class="num" style="font-size:14px;color:{MUTE}">{d}</span></button>'
        for i, (n, d) in enumerate(TIERS))
    pm = "".join(f'<button class="btn sm soft" type="button" aria-label="Zoom {w}" style="width:52px;height:52px;padding:0">{s}</button>'
                 for w, s in (("in", "+"), ("out", "-")))
    return (f'<div class="abs row" style="left:{x}px;top:{y}px;gap:16px;z-index:5"><div class="row" style="gap:4px;padding:0 6px;'
            f'background:rgba(0,4,31,.82);box-shadow:inset 0 0 0 1px rgba(67,76,137,.8)">{tiers}</div>{pm}</div>')


def giver_row(key, on):
    j = JOB[key]
    edge = f"box-shadow:inset 2px 0 0 {SEL};background:rgba(0,212,255,.06)" if on else "background:rgba(0,4,31,.82)"
    return (f'<button class="pick row" type="button" aria-pressed="{str(on).lower()}" style="width:400px;height:118px;gap:14px;padding:0 16px;{edge}">'
            f'{face(j[3], 64, 76, 1 if on else .7)}<span style="display:flex;flex-direction:column;gap:6px;flex:1;min-width:0">'
            f'<span class="lab" style="font-size:14px">{j[2]}  ·  {j[12]}</span><span style="font-size:18px;color:{INK if on else SOFT}">{j[1]}</span>'
            f'<span class="row" style="gap:8px">{"".join(glyph(c, 16, INK if on else SOFT) for c in j[7])}'
            f'<span style="font-size:14px;color:{MUTE};margin-left:6px">{j[4]}  ·  {DANGER[j[6]]}</span></span></span></button>')


def full_screen():
    inner = (zone(180, 560, ZONE) + zone(760, 150, "Celadon Reach") + zone(980, 1000, "Carmine Drift") + zone(1180, 300, "Aurum Banks")
             + lane_svg([(420, 640), (620, 560), (840, 490), (1060, 430)])
             + cell(620, 560, 54, "ahead") + cell(840, 490, 54, "ahead")
             + "".join(place(x, y, n, d) for x, y, n, d in r133.A_PLACES)
             + "".join(a_pin(k, k == "salvage") for k in ("salvage", "escort", "bounty", "core"))
             + event_mark(560, 740, "rival", RIVAL[0], "Also bound for Glassmere")
             + event_mark(880, 590, "wreck", "Wreck surfacing", "Next door, 40 s")
             + event_mark(300, 420, "bloom", "Bloom", "Two cells off"))
    givers = (f'<div class="abs" style="left:1480px;top:120px;display:flex;flex-direction:column;gap:10px;z-index:5">'
              f'<div class="row" style="justify-content:space-between;width:400px"><span class="lab">Work at {DOCK}</span>'
              f'<button class="btn sm soft" type="button" style="width:110px;height:36px">Hide</button></div>'
              + "".join(giver_row(k, k == "salvage") for k in ("salvage", "escort", "bounty", "core")) + '</div>')
    back = (f'<button class="btn sm soft abs" type="button" style="right:40px;top:40px;width:320px;gap:12px;z-index:6">'
            f'{kc("B")}Back from full screen</button>')
    foot = (f'<div class="glass" style="position:absolute;left:40px;top:900px;width:1840px;height:140px;box-sizing:border-box;padding:0 28px;'
            f'display:flex;align-items:center;gap:30px;z-index:5">{face(J[3], 72, 86)}'
            f'<span style="display:flex;flex-direction:column;gap:6px;width:420px"><span class="lab">{J[2]}  ·  {J[11]}</span>'
            f'<span style="font-size:22px">{J[1]}</span><span style="font-size:14px;color:{SOFT}">{DOCK} to {J[4]}  ·  {J[5]} cells  ·  3-5 min</span></span>'
            f'{danger(J[6])}<span class="row" style="gap:10px"><span class="lab">Wants</span>{"".join(glyph(c, 20) for c in J[7])}</span>'
            f'{r133.fit_meter(w=120)}<span style="margin-left:auto">{gems(r133.PAY, 20)}</span>'
            f'<button class="btn soft-cta" type="button" style="width:280px">{kc("A")}Take the job</button></div>')
    body = sea_map(0, 0, 1920, 1080, "50% 45%", .15, inner) + zoom_row(40, 40) + back + givers + foot
    return page(f"Rev {REV} FullScreen - the HyperSea full screen as built in Rev 111 (zoom inside the map, Back from full screen, Hide), "
                "carrying the givers, the lane, the events next door and the rival crew", body)


# ── ClassesA: the fleet by class, seven x four ──────────────────────────────────────────────────────────────────────
def classes_a():
    cols = [("salvage", 640), ("escort", 950), ("bounty", 1260), ("core", 1570)]
    head = "".join(
        f'<div class="abs" style="left:{x}px;top:176px;width:290px;display:flex;flex-direction:column;gap:6px;padding-bottom:10px;'
        f'{"box-shadow:inset 0 -2px 0 " + SEL if k == "salvage" else "box-shadow:inset 0 -1px 0 rgba(67,76,137,.8)"}">'
        f'<span class="lab" style="font-size:14px">{JOB[k][2]}</span><span style="font-size:18px;color:{INK if k == "salvage" else SOFT}">{JOB[k][1]}</span></div>'
        for k, x in cols)
    head += (f'<div class="abs" style="left:40px;top:176px;width:560px;display:flex;flex-direction:column;gap:6px;padding-bottom:10px;'
             f'box-shadow:inset 0 -1px 0 rgba(67,76,137,.8)"><span class="lab" style="font-size:14px">The fleet by class</span>'
             f'<span style="font-size:18px">Seven classes, four voyages</span></div>')
    rows = ""
    for i, c in enumerate(ORDER):
        y = 262 + i * 100
        mine = any(v in SQUAD for v in FLEET[c])
        rows += (f'<div class="abs" style="left:40px;top:{y}px;width:1820px;height:92px;background:{"rgba(0,4,31,.7)" if mine else "rgba(0,4,31,.45)"}"></div>'
                 f'<div class="abs" style="left:56px;top:{y + 12}px;width:540px;display:flex;flex-direction:column;gap:6px">'
                 f'<span class="row" style="gap:12px">{glyph(c, 26)}<span style="font-size:20px">{c}</span>'
                 f'<span style="font-size:14px;color:{SOFT}">{ROLE[c]}</span></span>'
                 f'<span class="row" style="gap:16px;margin-left:38px">{"".join(vessel_name(v, 14) for v in FLEET[c])}</span></div>'
                 + "".join(f'<div class="abs" style="left:{x}px;top:{y + 32}px">{fit_word(EFF[k][c], 15)}</div>' for k, x in cols))
    foot = legend(40, 990) + (f'<button class="btn sm soft abs" type="button" style="right:40px;top:980px;width:220px">{kc("B")}Back</button>')
    return page(f"Rev {REV} ClassesA - the fleet by class: how each of the seven classes does in each of the four voyages, and the vessels in it",
                window(3, head + rows + foot))


# ── ClassesB: this voyage's shelf (recommended) ─────────────────────────────────────────────────────────────────────
def tile(v, x, y, on=False, dim=False, w=200, h=84, ai_seat=False):
    art = (f'<img class="hull" src="{HULL[v]}" alt="{v}" style="width:84px;height:48px;object-fit:contain;flex:none">' if v in HULL else
           f'<span style="width:84px;height:48px;display:flex;align-items:center;justify-content:center;flex:none">{glyph(r_cls(v), 30, SOFT, 1.4)}</span>')
    edge = (f"box-shadow:inset 0 0 0 2px {SEL};background:rgba(0,212,255,.08)" if on else
            f"box-shadow:inset 0 -2px 0 {SEL};background:rgba(0,4,31,.7)" if v in SQUAD else "background:rgba(0,4,31,.5)")
    sub = ("Needs a person" if ai_seat and v not in AI_READY else "Your squad" if v in SQUAD else "")
    return (f'<button class="pick abs row" type="button" aria-pressed="{str(on).lower()}" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;gap:10px;'
            f'padding:0 10px;{edge};opacity:{.45 if dim else 1}">{art}<span style="display:flex;flex-direction:column;gap:5px;min-width:0;align-items:flex-start">'
            f'<span style="font-size:14px;white-space:nowrap">{v}</span>'
            f'{tag("AI", SOFT, "rgba(184,192,240,.5)") if v in AI_READY else ""}'
            f'{"<span style=\"font-size:14px;white-space:nowrap;color:" + MUTE + "\">" + sub + "</span>" if sub else ""}</span></button>')


def r_cls(v):
    return next(c for c, vs in FLEET.items() if v in vs)


def shelf(x0, y0, sel=None, ai_seat=False, w_tile=200, row_h=92):
    """Every class for The Glassmere Shatter, Best first, its line for this voyage and its vessels as tiles."""
    out, y = "", y0
    for n in (3, 2, 1):
        out += (f'<div class="abs row" style="left:{x0}px;top:{y}px;gap:14px">{fit_word(n, 15)}'
                f'<span style="font-size:14px;color:{MUTE}">{["", "little to do here", "does the work, no bonus", "Cass asks for these: each adds to fit and pay"][n]}</span></div>')
        y += 30
        for c in [c for c in ORDER if EFF["salvage"][c] == n]:
            out += (f'<div class="abs" style="left:{x0}px;top:{y}px;width:420px;display:flex;flex-direction:column;gap:6px;padding-top:12px">'
                    f'<span class="row" style="gap:10px">{glyph(c, 22, INK if n == 3 else SOFT)}<span style="font-size:18px;color:{INK if n == 3 else SOFT}">{c}</span></span>'
                    f'<span style="font-size:14px;color:{SOFT};line-height:1.35;margin-left:32px">{SALVAGE_DOES[c]}</span></div>')
            out += "".join(tile(v, x0 + 440 + i * (w_tile + 10), y + 4, on=(v == sel), dim=(ai_seat and v not in AI_READY), w=w_tile, ai_seat=ai_seat)
                           for i, v in enumerate(FLEET[c]))
            y += row_h
        y += 6
    return out


def voyage_tabs(x, y):
    return (f'<div class="abs row" style="left:{x}px;top:{y}px;gap:6px">' + "".join(
        f'<button class="pick" type="button" aria-pressed="{str(k == "salvage").lower()}" style="height:52px;padding:0 18px;display:flex;flex-direction:column;'
        f'justify-content:center;gap:3px;background:{"rgba(0,212,255,.06)" if k == "salvage" else "rgba(0,4,31,.5)"};'
        f'{"box-shadow:inset 0 -2px 0 " + SEL if k == "salvage" else ""}"><span class="cap" style="font-size:14px;color:{MUTE}">{JOB[k][2]}</span>'
        f'<span style="font-size:15px;color:{INK if k == "salvage" else SOFT}">{JOB[k][1]}</span></button>' for k, *_ in JOBS) + '</div>')


def squad_panel(x, y, w=460, swap=False):
    seats = [list(s) for s in SEATS]
    if swap:
        seats[2][2] = "Scarab"
    fit = sum(1 for s in seats if EFF["salvage"][r_cls(s[2])] == 3)
    bonus = {0: 0, 1: 10, 2: 20, 3: 35}[fit]
    rows = "".join(
        f'<div class="row" style="gap:12px;height:56px;padding:0 12px;background:rgba(0,4,31,.55);{"box-shadow:inset 2px 0 0 " + SEL if swap and i == 2 else ""}">'
        f'<span style="font-size:14px;color:{MUTE};width:56px" class="cap">{s[0]}</span>'
        f'<span style="font-size:15px;flex:1">{s[2]}</span>{cls_tag(r_cls(s[2]), 14, INK if EFF["salvage"][r_cls(s[2])] == 3 else SOFT, 18)}'
        f'<span style="width:84px;display:flex;justify-content:flex-end">{fit_word(EFF["salvage"][r_cls(s[2])], 14)}</span></div>'
        for i, s in enumerate(seats))
    segs = "".join(f'<span style="flex:1;height:10px;background:{INK if i < fit else "rgba(67,76,137,.6)"}"></span>' for i in range(3))
    pay = 30 + 30 * bonus // 100
    return (f'<div class="glass" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;box-sizing:border-box;padding:24px;display:flex;flex-direction:column;gap:14px">'
            f'<span class="lab">Your squad for this voyage</span>{rows}'
            f'<div class="row" style="gap:14px;margin-top:6px"><span class="row" style="gap:4px;width:100px">{segs}</span>'
            f'<span style="font-size:15px;white-space:nowrap">Squad fit {fit} of 3</span><span style="font-size:15px;color:{SOFT};white-space:nowrap">Pay +{bonus}%</span>'
            f'<span style="margin-left:auto">{gems(pay, 18)}</span></div></div>')


def suggestion(x, y, w=460):
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:12px">'
            f'<span class="lab">One swap that helps</span>'
            f'<span style="font-size:15px;line-height:1.45">Seat 3: Dolphin to <span style="color:{INK}">Scarab</span>, a Protector the AI can fly. '
            f'Squad fit 1 to 2 of 3, pay 33 to 36.</span>'
            f'<span style="font-size:14px;color:{SOFT};line-height:1.45">Urchin, Tiger Moth and Goby &amp; Pistol fit too, but they have no voyage AI yet: '
            f'a friend has to fly them.</span>'
            f'<div class="row" style="gap:12px;margin-top:6px"><button class="btn sm soft" type="button" style="width:220px">{kc("X")}Try the swap</button>'
            f'<button class="btn sm soft" type="button" style="width:220px">{kc("B")}Back to your Ark</button></div></div>')


def classes_b():
    giver = (f'<div class="abs row" style="left:40px;top:248px;gap:14px">{face(J[3], 44, 52)}'
             f'<span style="font-size:15px">{J[11]} wants</span>{"".join(cls_tag(c, 15) for c in J[7])}'
             f'<span style="font-size:14px;color:{SOFT};margin-left:10px">Loose crystal, a rival crew racing you, teeth in the water</span></div>')
    body = (voyage_tabs(40, 172) + giver + shelf(40, 318) + squad_panel(1420, 172) + suggestion(1420, 560)
            + f'<span class="abs" style="left:1420px;top:800px;width:460px;font-size:14px;color:{MUTE};line-height:1.5">A class is a label for voyages: '
              f'it never changes what a vessel does. Best means the giver asked for it.</span>')
    return page(f"Rev {REV} ClassesB - this voyage's shelf (recommended): every vessel Best, Good or Poor for The Glassmere Shatter, what each class does there, "
                "who the AI can fly, and the swap that lifts the squad's fit", window(3, body))


def classes_pick():
    dim = (f'<div class="abs" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,1,10,.8)"></div>')
    head = (f'<div class="abs" style="left:40px;top:168px;display:flex;flex-direction:column;gap:8px">'
            f'<span class="lab">Seat 3  ·  AI wingmate  ·  {J[1]}</span><span style="font-size:22px">Choose the hull for Seat 3</span></div>')
    pv = (f'<div class="glass" style="position:absolute;left:1420px;top:176px;width:460px;box-sizing:border-box;padding:24px;display:flex;flex-direction:column;gap:14px">'
          f'<span class="lab">Scarab  ·  flown by the AI</span>'
          f'<div class="row" style="gap:16px">{glyph("Protector", 44, INK, 1.4)}<span style="display:flex;flex-direction:column;gap:6px">'
          f'<span style="font-size:22px">Scarab</span>{fit_word(3, 15)}</span></div>'
          f'<span style="font-size:15px;line-height:1.45">Protector: {SALVAGE_DOES["Protector"].lower()}.</span>'
          f'<span style="font-size:14px;color:{SOFT}">Pilot stays OPAL: Reluctant on salvage, the AI plays the seat one rung down.</span></div>')
    body = (dim + head + shelf(40, 250, sel="Scarab", ai_seat=True, w_tile=200, row_h=90)
            + pv + squad_panel(1420, 500, swap=True)
            + f'<div class="abs row" style="left:1420px;top:880px;gap:12px"><button class="btn soft-cta" type="button" style="width:260px">{kc("A")}Take the seat</button>'
              f'<button class="btn sm soft" type="button" style="width:180px">{kc("B")}Back</button></div>')
    return page(f"Rev {REV} ClassesPick - the shelf in the seat picker: Seat 3's hull, sorted for this voyage; vessels without a voyage AI are dimmed "
                "for an AI seat; fit and pay change before you take it", window(3, body))


def notes():
    rows = [
        ("Asked", "Shombith, 24 Nov: \"make sure the fullscreen of hypersea feature stays, show me the screen which differentiates vessels as "
                  "per their classes and what to choose and how effective they are in the missions. That screen show me before you start building\"."),
        ("Full screen kept", "Board B keeps the built Rev 111 control: Full screen on its chart opens the HyperSea edge to edge with the zoom row "
                             "inside the map, Back from full screen (B) and Hide. In full screen the givers stand on the right, their pins and your lane on "
                             "the map, with the wreck, the bloom and The Rusted Grin marked; the picked voyage runs along the foot with Take the job."),
        ("Effectiveness", "Best: the giver asks for that class, so each seat in it adds to squad fit and the pay bonus (+10 / +20 / +35%). "
                          "Good: does the work without the bonus. Poor: little to do there. Three squares, no colour. A class is a label for "
                          "voyages and never changes a kit (LOCKED: a mode never changes the vessel)."),
        ("A  The fleet by class", "One reference table: seven classes down, four voyages across, every vessel under its class. Good for "
                                  "learning the fleet; you have to find your own voyage's column."),
        ("B  This voyage's shelf (recommended)", "Answers \"what do I choose\" for the voyage you are on: Best first, what each class does in "
                                                 "THIS voyage, your squad's fit and pay, and the one swap that lifts it. The tabs switch voyages. It "
                                                 "opens from the board (the Wants line) and from Your Ark, and the seat picker is the same shelf."),
        ("AI seats", "Five vessels have a voyage AI today (Serpent, Scarab, Sparrow, Dolphin, Squirrel). In an AI seat the others are dimmed "
                     "and say Needs a person; a friend can still fly them."),
        ("Art", "Hull renders exist for six vessels; the rest show their class glyph until /vessel-model renders them. Class sort is the M2 "
                "design's; where Garrett's Brood Rush measurement disagrees, it wins."),
        ("Rivals", "Answered on the card (24 Nov): in Milestone 2 a rival crew is three vessels, no Ark. Rival Arks stay DEPTH.md D3."),
    ]
    html = notes_page("notes: the full-screen HyperSea kept, and the vessel classes screen", rows)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


BOARDS = {"BoardFull": board_full, "FullScreen": full_screen, "ClassesA": classes_a, "ClassesB": classes_b, "ClassesPick": classes_pick}

if __name__ == "__main__":
    for key, fn in BOARDS.items():
        (ROOT / f"R{REV}{key}.dc.html").write_text(fn(), encoding="utf-8")
    (ROOT / f"R{REV}Notes.dc.html").write_text(notes(), encoding="utf-8")
    print("wrote", len(BOARDS) + 1, "boards to", ROOT)
