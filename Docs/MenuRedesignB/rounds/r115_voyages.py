#!/usr/bin/env python3
"""Rev 115 boards: your Ark and the seat picker, five ways (Frogma tab "Voyages playtest", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-16: "rework on the crew screen as well 5 mockups for the crew selection screen", then: "My bad delete
all these by crew I meant the ark screen where I select vessels and the popup to select pilots, elements etc." So the
first Rev 115 (five crew-recommends screens) came off the canvas, and this round redraws page 3, Your Ark (the lobby
where you pick your seat), and the popup that sets a seat's vessel, pilot, element and artifact. Built today: Rev 113
Lobby and Picker (sections 209, 210.1 and the app-shell PRs).

Each board is one option, two screens tall: the Ark page on top, its picker open below.
  A  Hangar line-up: the seats as vessels standing in a row; the picker slides in from the right with a live preview (recommended)
  B  Your seat first: your vessel large, wingmates in a column; the picker is a bottom sheet of one row at a time
  C  Pilot cards: each seat a tall card led by its pilot's portrait; the picker is a loadout in columns
  D  Formation: the seats around the Ark from above; the picker walks vessel, pilot, element, artifact one step at a time
  E  Garage list: the seats as rows; the picker is a vessel-by-element grid, one press sets both

The canvas holds at most 512 files of its own: Rev 114's unpicked options (Intro C/D, Objective B-D, Feel B-D) came off
for room (copies kept in Docs/MenuRedesignB/board/).

    python3 Docs/MenuRedesignB/rounds/r115_voyages.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "115"
_spec = importlib.util.spec_from_file_location("r114", HERE / "r114_voyages.py")
r114 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev]
_spec.loader.exec_module(r114)
r113, r108 = r114.r113, r114.r108

REV = int(_rev)
ROOT = r114.ROOT
HULL, FACE, FLOWER, ARK, G = r114.HULL, r114.FACE, r114.FLOWER, r114.ARK, r114.G
page, menu, pips, face, flower, close_x, pair = r114.page, r114.menu, r114.pips, r114.face, r114.flower, r114.close_x, r114.pair
_CAPTURE = r114._CAPTURE

VESSELS = {v[0]: v[2] for v in r108.VESSELS}          # name -> one line
ELEMENTS = ["Charge", "Mass", "Space", "Time"]
SEATS = [  # (role, who, vessel, pilot, element, artifact, state)
    ("Leader", "You", "Manta", "Tamako Deephook", "Space", "No artifact", "You"),
    ("Seat 2", "Rook", "Dolphin", "Ilia Silvergate", "Charge", "Tailwind", "Ready"),
    ("Seat 3", "AI wingmate", "Sparrow", "Mathalo Kelpbloom", "Time", "No artifact", "AI"),
]
PILOTS = ["Tamako Deephook", "Ilia Silvergate", "Mathalo Kelpbloom", "Karenth Amberweaver", "Quivera Pearlspark", "Veliane Crestward"]
STATE_COL = {"You": "#00D4FF", "Ready": "#99FF80", "AI": "#747BAD", "Choosing": "#FFAE00"}


def tall(top_fn, bottom_fn, title):
    """One option: the Ark page above, the same page with its picker open below (1920 x 2200)."""
    _CAPTURE.append([])
    top_fn(), bottom_fn()
    a, b = _CAPTURE.pop()
    body = (f'<div style="position:absolute;left:0;top:0;width:1920px;height:1080px;overflow:hidden">{a}</div>'
            f'<div style="position:absolute;left:0;top:1120px;width:1920px;height:1080px;overflow:hidden">{b}</div>')
    return page(title, body, 2200)


def ark_page(body, title="ark"):
    return page(title, menu(3, body))


def state_chip(st):
    c = STATE_COL[st]
    return (f'<span class="cap2 row" style="gap:8px;color:{c}"><span style="width:8px;height:8px;border-radius:50%;background:{c}"></span>'
            f'{ {"You": "You", "Ready": "Ready", "AI": "AI wingmate", "Choosing": "Choosing"}[st] }</span>')


def voyage_line(x=40, y=180):
    return (f'<div class="abs row" style="left:{x}px;top:{y}px;gap:16px"><img src="{ARK}" alt="" style="width:90px;height:48px;object-fit:contain">'
            '<span style="display:flex;flex-direction:column;gap:6px"><span style="font-size:18px">Escort the Ark to The Maw</span>'
            f'<span class="row" style="gap:10px;font-size:14px;color:#747BAD">Ark escort  ·  5 worlds  {pips(2, 90)}<span class="num">2</span></span></span></div>')


def sail_bar(sail_as="Manta  ·  Space", y=990):
    return (f'<div class="abs row" style="left:40px;right:40px;top:{y}px;justify-content:space-between">'
            '<button class="btn sm soft" type="button" style="width:420px"><span class="kc">Y</span>Fill the AI seats from a saved squad</button>'
            f'<span class="row" style="gap:24px"><span style="font-size:16px;color:#B8C0F0">You sail as {sail_as}</span>'
            '<button class="btn soft-cta" type="button" style="width:300px"><span class="kc">A</span>Set sail</button></span></div>')


def scrim(inner, side=False):
    return ('<div class="abs" style="left:0;top:84px;width:1920px;height:996px;z-index:8;background:rgba(0,1,10,'
            + (".45" if side else ".72") + ')">' + inner + '</div>')


def saved_line(words):
    return ('<span class="row" style="gap:12px;font-size:16px"><svg width="18" height="14" viewBox="0 0 18 14" fill="none" stroke="#E6E9FF" '
            f'stroke-width="2" aria-hidden="true"><path d="M1 7l5 5L17 1"></path></svg>{words}</span>')


def vessel_tile(v, on, w=200, h=150):
    edge = "inset 0 0 0 1px #00D4FF" if on else "inset 0 0 0 1px rgba(67,76,137,.6)"
    return (f'<button class="pick" type="button" aria-pressed="{str(on).lower()}" style="width:{w}px;height:{h}px;background:rgba(0,4,31,.7);box-shadow:{edge};'
            f'display:flex;flex-direction:column;align-items:center;justify-content:center;gap:8px">'
            f'<img class="hull" src="{HULL[v]}" alt="" style="width:80%;height:{h - 60}px;object-fit:contain"><span style="font-size:16px">{v}</span></button>')


def pilot_row(p, on, w=360):
    edge = "box-shadow:inset 2px 0 0 #00D4FF;background:rgba(0,212,255,.06)" if on else ""
    return (f'<button class="pick row" type="button" aria-pressed="{str(on).lower()}" style="width:{w}px;height:76px;gap:14px;padding:0 14px;{edge};text-align:left">'
            f'{face(p, 48, 58)}<span style="display:flex;flex-direction:column;gap:4px"><span style="font-size:16px">{p}</span>'
            f'<span style="font-size:13px;color:#747BAD">Sees what the fauna are heading for</span></span></button>')


def element_pick(el, on, size=56):
    edge = "inset 0 0 0 1px #00D4FF" if on else "inset 0 0 0 1px rgba(67,76,137,.6)"
    return (f'<button class="pick" type="button" aria-pressed="{str(on).lower()}" style="width:{size + 64}px;height:{size + 60}px;box-shadow:{edge};'
            f'display:flex;flex-direction:column;align-items:center;justify-content:center;gap:8px">{flower(el, size)}<span style="font-size:14px">{el}</span></button>')


# ── A: Hangar line-up ───────────────────────────────────────────────────────────────────────────────────────────────
def a_seats(pick_open=False):
    out = []
    for i, (role, who, v, p, el, art, st) in enumerate(SEATS):
        x = 120 + i * 580
        on = i == 0
        out.append(
            f'<div class="abs" style="left:{x}px;top:300px;width:520px;height:620px">'
            f'<span aria-hidden="true" class="abs" style="left:40px;right:40px;top:300px;height:60px;border-radius:50%;background:radial-gradient(ellipse,rgba(150,172,252,.22),rgba(150,172,252,0) 70%)"></span>'
            f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:20px;width:480px;top:70px;height:260px;object-fit:contain">'
            f'<div class="abs" style="left:0;right:0;top:0;display:flex;justify-content:space-between"><span class="cap2">{role}</span>{state_chip(st)}</div>'
            f'<div class="abs" style="left:0;right:0;top:390px;height:1px;background:{"#00D4FF" if on else "rgba(67,76,137,.6)"}"></div>'
            f'<div class="abs row" style="left:0;right:0;top:410px;gap:16px">{face(p, 60, 72)}<span style="display:flex;flex-direction:column;gap:6px;flex:1">'
            f'<span style="font-size:24px">{v}</span><span style="font-size:14px;color:#747BAD">{p}  ·  {art}</span></span>'
            f'<span style="display:flex;flex-direction:column;align-items:center;gap:4px">{flower(el, 34)}<span style="font-size:13px;color:#747BAD">{el}</span></span></div>'
            f'<div class="abs" style="left:0;right:0;top:520px">'
            + ('<button class="btn sm soft" type="button" style="width:100%"><span class="kc">X</span>Change your seat</button>' if i == 0 else
               '<span style="font-size:14px;color:#747BAD">Rook picks their own seat.</span>' if st == "Ready" else
               '<span class="row" style="gap:10px"><button class="btn sm soft chipq" type="button" style="flex:1">Invite a friend</button>'
               '<button class="btn sm soft chipq" type="button" style="flex:1">Change wingmate</button></span>')
            + '</div></div>')
    floor = ('<div class="abs" style="left:0;right:0;top:640px;height:300px;background:linear-gradient(180deg,rgba(117,143,251,0),rgba(117,143,251,.06));'
             'box-shadow:inset 0 1px 0 rgba(67,76,137,.4)"></div>')
    return floor + "".join(out)


def opt_a_page():
    return ark_page(voyage_line() + a_seats() + sail_bar(), "a")


def opt_a_pick():
    tabs = "".join(f'<span class="cap2" style="padding-bottom:10px;{"box-shadow:inset 0 -2px 0 #00D4FF;color:#E6E9FF" if t == "Vessel" else ""}">{t}</span>'
                   for t in ("Vessel", "Pilot", "Element", "Artifact"))
    grid = "".join(vessel_tile(v, v == "Manta", 200, 150) for v in HULL)
    panel = ('<div role="dialog" aria-label="Change your seat" class="abs" style="left:1100px;top:0;width:820px;height:996px;background:#00041F;box-shadow:inset 1px 0 0 #434C89">'
             '<div class="abs" style="left:40px;top:34px;font-size:26px">Change your seat</div>' + close_x(736, 30)
             + f'<div class="abs row" style="left:40px;top:96px;gap:36px">{tabs}</div>'
             f'<div class="abs" style="left:40px;top:160px;display:grid;grid-template-columns:repeat(3,200px);gap:16px">{grid}</div>'
             f'<div class="abs" style="left:40px;right:40px;top:520px;display:flex;flex-direction:column;gap:10px">'
             f'<span style="font-size:22px">Manta</span><span style="font-size:15px;line-height:1.5;color:#B8C0F0">{VESSELS["Manta"]}. Wide and steady; holds space beside the Ark.</span></div>'
             f'<div class="abs" style="left:40px;right:40px;top:640px;display:flex;flex-direction:column;gap:14px"><span class="cap2">Your seat now</span>'
             f'<span class="row" style="gap:16px">{face("Tamako Deephook", 44, 52)}<span style="font-size:16px">Manta  ·  Tamako  ·  Space  ·  No artifact</span>{flower("Space", 26)}</span></div>'
             f'<div class="abs" style="left:40px;right:40px;bottom:40px;display:flex;justify-content:space-between;align-items:center">'
             f'{saved_line("Saved to your seat for this voyage.")}<button class="btn soft-cta" type="button" style="width:220px"><span class="kc">A</span>Done</button></div></div>')
    return ark_page(voyage_line() + a_seats() + sail_bar() + scrim(panel, side=True), "a2")


def opt_a():
    return tall(opt_a_page, opt_a_pick, f"Rev {REV} A - Hangar line-up: your seats as vessels standing in a row; the picker slides in from the right and your seat changes behind it (recommended)")


# ── B: Your seat first ──────────────────────────────────────────────────────────────────────────────────────────────
def b_body(sheet=False):
    role, who, v, p, el, art, st = SEATS[0]
    hero = (f'<span aria-hidden="true" class="abs" style="left:120px;top:420px;width:1000px;height:180px;border-radius:50%;background:radial-gradient(ellipse,rgba(150,172,252,.2),rgba(150,172,252,0) 70%)"></span>'
            f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:100px;top:330px;width:1040px;height:380px;object-fit:contain">'
            f'<div class="abs" style="left:40px;top:260px;display:flex;flex-direction:column;gap:8px"><span class="cap2">Your seat  ·  leader</span>'
            f'<span style="font-size:40px">{v}</span></div>')
    chips = (f'<div class="abs row" style="left:40px;top:720px;gap:14px">'
             + "".join(f'<button class="btn sm soft chipq" type="button" style="height:64px;padding:0 20px;{"box-shadow:inset 0 0 0 1px #00D4FF" if (sheet and k == "Pilot") else ""}">'
                       f'<span class="row" style="gap:12px">{ico}<span style="display:flex;flex-direction:column;align-items:flex-start;gap:3px"><span class="cap2" style="font-size:11px">{k}</span>'
                       f'<span style="font-size:15px">{val}</span></span></span></button>'
                       for k, val, ico in (("Vessel", v, ""), ("Pilot", p, face(p, 36, 42)), ("Element", el, flower(el, 30)), ("Artifact", art, "")))
             + '</div>')
    wing = ('<div class="abs" style="left:1340px;top:260px;width:540px;display:flex;flex-direction:column;gap:16px"><span class="cap2">Your squad</span>'
            + "".join(f'<div class="row" style="gap:16px;height:110px;padding:0 16px;box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)">'
                      f'<img class="hull" src="{HULL[s[2]]}" alt="" style="width:130px;height:70px;object-fit:contain">'
                      f'<span style="display:flex;flex-direction:column;gap:6px;flex:1"><span style="font-size:18px">{s[1]}</span>'
                      f'<span style="font-size:13px;color:#747BAD">{s[2]}  ·  {s[3].split()[0]}  ·  {s[4]}</span>{state_chip(s[6])}</span>{flower(s[4], 28)}</div>'
                      for s in SEATS[1:])
            + '<div class="row" style="gap:10px"><button class="btn sm soft chipq" type="button" style="flex:1">Invite a friend</button>'
            '<button class="btn sm soft chipq" type="button" style="flex:1">Change wingmate</button></div></div>')
    out = voyage_line() + hero + chips + wing + sail_bar()
    if sheet:
        row = "".join(f'<button class="pick" type="button" aria-pressed="{str(i == 0).lower()}" style="width:230px;height:250px;flex:none;position:relative;'
                      f'box-shadow:{"inset 0 0 0 2px #00D4FF" if i == 0 else "inset 0 0 0 1px rgba(67,76,137,.6)"}">'
                      f'<img src="{FACE[x]}" alt="" style="position:absolute;inset:{3 if i == 0 else 1}px;width:calc(100% - {6 if i == 0 else 2}px);height:190px;object-fit:cover;object-position:50% 25%">'
                      f'<span class="abs" style="left:12px;right:12px;bottom:14px;font-size:15px;text-align:left">{x}</span></button>' for i, x in enumerate(PILOTS))
        out += ('<div class="abs" style="left:0;top:84px;width:1920px;height:996px;z-index:8;background:rgba(0,1,10,.4)">'
                '<div role="dialog" aria-label="Pick your pilot" class="abs" style="left:0;right:0;bottom:0;height:470px;background:#00041F;box-shadow:inset 0 1px 0 #434C89">'
                '<div class="abs row" style="left:40px;right:40px;top:30px;justify-content:space-between"><span class="row" style="gap:30px"><span style="font-size:26px">Pilot</span>'
                '<span class="cap2">LB Vessel  ·  Element RB</span></span>' + '</div>'
                f'<div class="abs row" style="left:40px;right:40px;top:96px;gap:18px;overflow:hidden">{row}</div>'
                f'<div class="abs row" style="left:40px;right:40px;bottom:30px;justify-content:space-between">{saved_line("Tamako flies your Manta this voyage.")}'
                '<button class="btn soft-cta" type="button" style="width:220px"><span class="kc">A</span>Done</button></div></div></div>')
    return out


def opt_b():
    return tall(lambda: ark_page(b_body(), "b"), lambda: ark_page(b_body(True), "b2"),
                f"Rev {REV} B - Your seat first: your vessel large with its four picks under it; the picker is a bottom sheet, one row at a time")


# ── C: Pilot cards ──────────────────────────────────────────────────────────────────────────────────────────────────
def c_cards():
    out = []
    for i, (role, who, v, p, el, art, st) in enumerate(SEATS):
        x = 200 + i * 520
        edge = "box-shadow:inset 0 0 0 2px #00D4FF" if i == 0 else "box-shadow:inset 0 0 0 1px rgba(67,76,137,.6)"
        out.append(f'<div class="abs" style="left:{x}px;top:260px;width:480px;height:680px;background:#00041F;{edge};overflow:hidden">'
                   f'<img src="{FACE[p]}" alt="" style="position:absolute;left:2px;top:2px;width:476px;height:420px;object-fit:cover;object-position:50% 22%">'
                   '<div class="abs" style="left:0;right:0;top:280px;height:150px;background:linear-gradient(180deg,rgba(0,4,31,0),#00041F)"></div>'
                   f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:150px;top:330px;width:320px;height:150px;object-fit:contain">'
                   f'<div class="abs" style="left:24px;right:24px;top:20px;display:flex;justify-content:space-between"><span class="cap2" style="color:#E6E9FF">{role}</span>{state_chip(st)}</div>'
                   f'<div class="abs" style="left:24px;right:24px;top:500px;display:flex;flex-direction:column;gap:8px"><span style="font-size:24px">{p.split()[0]}</span>'
                   f'<span class="row" style="gap:12px;font-size:15px;color:#B8C0F0">{v}  ·  {el} {flower(el, 22)}  ·  {art}</span>'
                   f'<span style="font-size:14px;color:#747BAD">{who}</span></div>'
                   f'<div class="abs" style="left:24px;right:24px;bottom:22px">'
                   + ('<button class="btn sm soft" type="button" style="width:100%"><span class="kc">X</span>Loadout</button>' if i == 0 else "") + '</div></div>')
    return "".join(out)


def c_loadout():
    col = lambda t, inner, w: (f'<div style="display:flex;flex-direction:column;gap:12px;width:{w}px"><span class="cap2">{t}</span>{inner}</div>')
    vessels = "".join(f'<button class="pick row" type="button" aria-pressed="{str(v == "Manta").lower()}" style="height:70px;gap:12px;padding:0 12px;'
                      f'{"box-shadow:inset 2px 0 0 #00D4FF;background:rgba(0,212,255,.06)" if v == "Manta" else ""}"><img class="hull" src="{HULL[v]}" alt="" style="width:90px;height:50px;object-fit:contain">'
                      f'<span style="font-size:16px">{v}</span></button>' for v in HULL)
    pilots = "".join(pilot_row(p, p == "Tamako Deephook", 400) for p in PILOTS)
    els = '<div style="display:grid;grid-template-columns:1fr 1fr;gap:12px">' + "".join(element_pick(e, e == "Space", 48) for e in ELEMENTS) + '</div>'
    arts = "".join(f'<button class="pick" type="button" style="height:52px;padding:0 12px;text-align:left;{"box-shadow:inset 2px 0 0 #00D4FF" if a == "No artifact" else ""}">'
                   f'<span style="font-size:15px">{a}</span> <span style="font-size:13px;color:#747BAD">{d}</span></button>' for a, d in r108.ARTIFACTS)
    preview = (f'<div style="position:relative;width:380px;height:720px;background:#00010A;box-shadow:inset 0 0 0 1px rgba(67,76,137,.6);overflow:hidden">'
               f'<img src="{FACE["Tamako Deephook"]}" alt="" style="position:absolute;left:1px;top:1px;width:378px;height:360px;object-fit:cover;object-position:50% 22%">'
               f'<img class="hull" src="{HULL["Manta"]}" alt="" style="position:absolute;left:30px;top:330px;width:320px;height:160px;object-fit:contain">'
               '<div class="abs" style="left:24px;right:24px;bottom:24px;display:flex;flex-direction:column;gap:10px"><span class="cap2" style="color:#E6E9FF">You sail as</span>'
               f'<span style="font-size:22px">Manta  ·  Tamako</span><span class="row" style="gap:10px;font-size:15px;color:#B8C0F0">{flower("Space", 22)}Space  ·  No artifact</span></div></div>')
    dlg = ('<div role="dialog" aria-label="Loadout" class="abs" style="left:60px;top:30px;width:1800px;height:930px;background:#00041F;box-shadow:inset 0 0 0 1px #434C89">'
           '<div class="abs" style="left:40px;top:30px;font-size:28px">Loadout</div>' + close_x(1716, 30)
           + '<div class="abs row" style="left:40px;top:110px;gap:36px;align-items:flex-start">'
           + col("Vessel", vessels, 260) + col("Pilot", pilots, 400) + col("Element", els, 260) + col("Artifact", arts, 280) + preview + '</div>'
           f'<div class="abs row" style="left:40px;right:40px;bottom:30px;justify-content:space-between">{saved_line("Every column saves to your seat for this voyage.")}'
           '<button class="btn soft-cta" type="button" style="width:220px"><span class="kc">A</span>Done</button></div></div>')
    return scrim(dlg)


def opt_c():
    return tall(lambda: ark_page(voyage_line() + c_cards() + sail_bar("Manta  ·  Tamako  ·  Space"), "c"),
                lambda: ark_page(voyage_line() + c_cards() + sail_bar("Manta  ·  Tamako  ·  Space") + c_loadout(), "c2"),
                f"Rev {REV} C - Pilot cards: each seat a tall card led by its pilot, the vessel beneath; the picker is a loadout in four columns with what you sail as")


# ── D: Formation ────────────────────────────────────────────────────────────────────────────────────────────────────
def d_body():
    out = [f'<div class="frame" style="left:40px;top:260px;width:1840px;height:700px"><img class="bg" src="{G["cell"]}" alt="">'
           '<div class="abs" style="inset:0;background:rgba(0,1,10,.55)"></div>' + r114.ark(670, 210, 500) + '</div>']
    slots = [(470, 520, 0), (1130, 420, 1), (1150, 720, 2)]
    for x, y, i in slots:
        role, who, v, p, el, art, st = SEATS[i]
        out.append(f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:{x}px;top:{y}px;width:220px;height:100px;object-fit:contain">'
                   f'<div class="abs" style="left:{x + (230 if i else -310)}px;top:{y + 10}px;width:300px;display:flex;flex-direction:column;gap:6px;'
                   f'padding:12px 14px;background:rgba(0,4,31,.85);box-shadow:inset 2px 0 0 {"#00D4FF" if i == 0 else "#434C89"}">'
                   f'<span class="row" style="justify-content:space-between"><span style="font-size:17px">{who}</span>{state_chip(st)}</span>'
                   f'<span class="row" style="gap:8px;font-size:13px;color:#B8C0F0">{face(p, 26, 30)}{v}  ·  {p.split()[0]}  ·  {el}{flower(el, 18)}</span></div>')
    out.append('<span class="abs cap2" style="left:64px;top:920px">Where each seat flies around the Ark. Left flank, right flank, rear guard.</span>')
    return voyage_line() + "".join(out) + sail_bar()


def d_step():
    steps = "".join(f'<span class="row" style="gap:10px;color:{c}"><span class="num" style="width:28px;height:28px;border-radius:50%;display:flex;align-items:center;justify-content:center;'
                    f'box-shadow:inset 0 0 0 {"2px #00D4FF" if n == 2 else "1px #434C89"}">{n}</span><span class="cap2" style="color:inherit">{t}</span>'
                    f'<span style="font-size:13px;color:#747BAD">{val}</span></span>'
                    for n, t, val, c in ((1, "Vessel", "Manta", "#B8C0F0"), (2, "Pilot", "", "#E6E9FF"), (3, "Element", "", "#747BAD"), (4, "Artifact", "", "#747BAD")))
    big = "".join(f'<button class="pick" type="button" aria-pressed="{str(i == 0).lower()}" style="width:300px;height:400px;position:relative;flex:none;'
                  f'box-shadow:{"inset 0 0 0 2px #00D4FF" if i == 0 else "inset 0 0 0 1px rgba(67,76,137,.6)"};{"" if i < 3 else "opacity:.5"}">'
                  f'<img src="{FACE[x]}" alt="" style="position:absolute;inset:{3 if i == 0 else 1}px;width:calc(100% - {6 if i == 0 else 2}px);height:300px;object-fit:cover;object-position:50% 22%">'
                  f'<span class="abs" style="left:16px;right:16px;bottom:44px;font-size:17px;text-align:left">{x}</span>'
                  f'<span class="abs" style="left:16px;right:16px;bottom:18px;font-size:13px;color:#747BAD;text-align:left">Sees the fauna\'s target</span></button>'
                  for i, x in enumerate(PILOTS[:4]))
    dlg = ('<div role="dialog" aria-label="Your seat, step 2 of 4" class="abs" style="left:260px;top:60px;width:1400px;height:860px;background:#00041F;box-shadow:inset 0 0 0 1px #434C89">'
           '<div class="abs" style="left:40px;top:30px;font-size:28px">Who flies your Manta?</div>' + close_x(1316, 30)
           + f'<div class="abs row" style="left:40px;top:96px;gap:40px">{steps}</div>'
           f'<div class="abs row" style="left:40px;right:40px;top:170px;gap:24px">{big}</div>'
           '<div class="abs row" style="left:40px;right:40px;bottom:30px;justify-content:space-between">'
           '<button class="btn sm soft" type="button" style="width:200px"><span class="kc">B</span>Back to vessel</button>'
           '<button class="btn soft-cta" type="button" style="width:300px"><span class="kc">A</span>Next: element</button></div></div>')
    return scrim(dlg)


def opt_d():
    return tall(lambda: ark_page(d_body(), "d"), lambda: ark_page(d_body() + d_step(), "d2"),
                f"Rev {REV} D - Formation: the seats where they fly around the Ark; the picker walks vessel, pilot, element and artifact one step at a time")


# ── E: Garage list ──────────────────────────────────────────────────────────────────────────────────────────────────
def e_body():
    head = ('<div class="abs row" style="left:40px;right:40px;top:300px;height:40px;gap:0;box-shadow:inset 0 -1px 0 rgba(67,76,137,.6)">'
            + "".join(f'<span class="cap2" style="width:{w}px">{t}</span>' for t, w in (("Seat", 240), ("Vessel", 420), ("Pilot", 420), ("Element", 240), ("Artifact", 260), ("", 260))) + '</div>')
    rows = ""
    for i, (role, who, v, p, el, art, st) in enumerate(SEATS):
        edge = "box-shadow:inset 2px 0 0 #00D4FF,inset 0 -1px 0 rgba(67,76,137,.5);background:rgba(0,212,255,.05)" if i == 0 else "box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)"
        rows += (f'<div class="row" style="height:150px;{edge}">'
                 f'<span style="width:240px;display:flex;flex-direction:column;gap:8px;padding-left:16px"><span style="font-size:20px">{who}</span>{state_chip(st)}</span>'
                 f'<span class="row" style="width:420px;gap:16px"><img class="hull" src="{HULL[v]}" alt="" style="width:200px;height:100px;object-fit:contain"><span style="font-size:18px">{v}</span></span>'
                 f'<span class="row" style="width:420px;gap:14px">{face(p, 56, 66)}<span style="font-size:17px">{p}</span></span>'
                 f'<span class="row" style="width:240px;gap:10px">{flower(el, 34)}<span style="font-size:17px">{el}</span></span>'
                 f'<span style="width:260px;font-size:16px;color:#B8C0F0">{art}</span>'
                 + (f'<button class="btn sm soft" type="button" style="width:220px"><span class="kc">X</span>Change</button>' if i == 0 else
                    '<span style="width:220px;font-size:14px;color:#747BAD">' + ("Picks their own" if st == "Ready" else "Change wingmate") + '</span>') + '</div>')
    return voyage_line() + head + f'<div class="abs" style="left:40px;right:40px;top:340px">{rows}</div>' + sail_bar()


def e_grid():
    head = "".join(f'<span class="row" style="width:180px;justify-content:center;gap:8px">{flower(e, 26)}<span class="cap2">{e}</span></span>' for e in ELEMENTS)
    rows = ""
    for v in HULL:
        cells = "".join(f'<button class="pick" type="button" aria-pressed="{str(v == "Manta" and e == "Space").lower()}" style="width:180px;height:76px;'
                        f'box-shadow:{"inset 0 0 0 2px #00D4FF" if (v == "Manta" and e == "Space") else "inset 0 0 0 1px rgba(67,76,137,.45)"};'
                        f'display:flex;align-items:center;justify-content:center"><img class="hull" src="{HULL[v]}" alt="{v} {e}" style="width:110px;height:56px;object-fit:contain"></button>'
                        for e in ELEMENTS)
        rows += f'<div class="row" style="gap:10px"><span style="width:140px;font-size:17px">{v}</span>{cells}</div>'
    pil = "".join(pilot_row(p, p == "Tamako Deephook", 420) for p in PILOTS[:5])
    dlg = ('<div role="dialog" aria-label="Change your seat" class="abs" style="left:60px;top:30px;width:1800px;height:930px;background:#00041F;box-shadow:inset 0 0 0 1px #434C89">'
           '<div class="abs" style="left:40px;top:30px;display:flex;flex-direction:column;gap:8px"><span style="font-size:28px">Change your seat</span>'
           '<span style="font-size:15px;color:#747BAD">One press picks a vessel and its element together.</span></div>' + close_x(1716, 30)
           + f'<div class="abs" style="left:40px;top:130px;display:flex;flex-direction:column;gap:10px"><div class="row" style="gap:10px"><span style="width:140px"></span>{head}</div>{rows}</div>'
           f'<div class="abs" style="left:1000px;top:130px;display:flex;flex-direction:column;gap:6px"><span class="cap2" style="margin-bottom:6px">Pilot</span>{pil}</div>'
           f'<div class="abs row" style="left:40px;right:40px;bottom:30px;justify-content:space-between">{saved_line("Manta in Space, flown by Tamako. Saved to your seat for this voyage.")}'
           '<button class="btn soft-cta" type="button" style="width:220px"><span class="kc">A</span>Done</button></div></div>')
    return scrim(dlg)


def opt_e():
    return tall(lambda: ark_page(e_body(), "e"), lambda: ark_page(e_body() + e_grid(), "e2"),
                f"Rev {REV} E - Garage list: the seats as rows you read across; the picker is a vessel-by-element grid, one press sets both")


BOARDS = {f"R{REV}A.dc.html": opt_a, f"R{REV}B.dc.html": opt_b, f"R{REV}C.dc.html": opt_c, f"R{REV}D.dc.html": opt_d, f"R{REV}E.dc.html": opt_e}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
