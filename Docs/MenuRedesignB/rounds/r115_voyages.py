#!/usr/bin/env python3
"""Rev 115 boards: the crew selection screen, five more ways (Frogma tab "Voyages playtest", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-16, after Rev 114 C2 (pilot first) was built: "I want you to rework on the crew screen as well 5
mockups for the crew selection screen". In the same message: "Shouldnt the ark park at each cell and give us time to
steal the cells crystals. Its also like a single directional motion for the ark, its nothing interesting." So every
option shows the voyage's route as the cells the Ark stops at, and what each stop holds.

  AB  A Briefing room: your crew around the route table, the job in the middle (recommended)
      B Dossier: one pilot at a time, full height, a film strip of the rest
  CD  C Route first: the voyage drawn as its stops, the pilot who asked in the corner
      D Contracts: a fanned hand of job cards, the picked one raised
  E   E Comms: incoming calls from your crew, the picked one opened

The canvas holds at most 512 files of its own: the five options share three boards, and the unpicked Rev 114 crew
options (C1, C3, C4) came off the canvas for room (copies in Docs/MenuRedesignB/board/).

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

REV = int(_rev)
ROOT = r114.ROOT
G, MAPS, ARK, FACE, LOCK, FLOWER = r114.G, r114.MAPS, r114.ARK, r114.FACE, r114.LOCK, r114.FLOWER
page, menu, scrim, pips, face, flower = r114.page, r114.menu, r114.scrim, r114.pips, r114.face, r114.flower
open_btn, lock_line, close_x, ark, frame, pair = r114.open_btn, r114.lock_line, r114.close_x, r114.ark, r114.frame, r114.pair

# The jobs your crew brings. (pilot, faction, job, line in their words, difficulty, open, picture, stops)
# A stop is (name, state, crystals): the cells the Ark parks at on the way.
ESCORT_STOPS = [("Garland", "Calm", 4), ("Scarab Reef", "Hostile", 7), ("Boneyard", "Calm", 3), ("The Swarm", "Hostile", 6), ("The Maw", "End", 0)]
RUN_STOPS = [("Cell 1", "Calm", 4), ("Cell 2", "Hostile", 7), ("Cell 3", "Calm", 3), ("Tollmoor", "Hostile", 6)]
JOBS = [
    ("Veliane Crestward", "Wardens of the Calm", "Escort the Ark to The Maw", "Get the Ark to The Maw. Keep its hull whole; we have nothing behind it.", 2, True, G["cell"], ESCORT_STOPS),
    ("Tamako Deephook", "Order of the Last Lure", "Crystal Run before Tollmoor", "Twenty crystals before Tollmoor. The quiet cells are easy; the angry ones pay.", 3, True, G["crystal"], RUN_STOPS),
    ("Ilia Silvergate", "Hunters", "Patrol Saltmarch", "Something is eating the Saltmarch shoals. Find it.", 3, False, MAPS[0], []),
    ("Ekora Hollowbrook", "Pirates", "Raid the hoard at Magpie Hollow", "Magpie Hollow keeps more than it needs.", 5, False, MAPS[2], []),
    ("Quivera Pearlspark", "Merchants", "Carry a dispatch to Aurum Harbour", "Sealed, and nobody opens it. Not even you.", 2, False, MAPS[3], []),
    ("Mathalo Kelpbloom", "Hunters", "Patrol Verdigris", "Verdigris has gone too quiet.", 3, False, MAPS[1], []),
]


def first(p):
    return p.split()[0]


def crystals_line(stops):
    total = sum(c for _, _, c in stops)
    return f'{total} crystals across {len([s for s in stops if s[1] != "End"])} stops'


def stops_row(stops, w=900, big=False):
    """The route as stops the Ark parks at: a dot per cell, its name, calm or hostile, and the crystals it holds."""
    if not stops:
        return ""
    n = len(stops)
    out = [f'<div style="position:relative;width:{w}px;height:{110 if big else 92}px">'
           f'<div style="position:absolute;left:12px;right:12px;top:11px;height:2px;background:rgba(67,76,137,.8)"></div>']
    for i, (name, state, c) in enumerate(stops):
        x = i / (n - 1) * (w - 24)
        end = state == "End"
        dot = ("background:#00041F;box-shadow:inset 0 0 0 2px #99FF80" if end else
               "background:#00041F;box-shadow:inset 0 0 0 2px #FF8A7C" if state == "Hostile" else
               "background:#00041F;box-shadow:inset 0 0 0 2px #E6E9FF")
        col = "#99FF80" if end else "#FF8A7C" if state == "Hostile" else "#B8C0F0"
        al = "left" if i == 0 else "right" if i == n - 1 else "center"
        off = 0 if i == 0 else 128 if i == n - 1 else 64
        out.append(f'<div style="position:absolute;left:{x - off + 12:.0f}px;top:0;width:{140 if i in (0, n - 1) else 128}px;text-align:{al}">'
                   f'<div style="width:24px;height:24px;border-radius:50%;{dot};margin:{"0" if i == 0 else "0 0 0 auto" if i == n - 1 else "0 auto"}"></div>'
                   f'<div style="font-size:{16 if big else 14}px;margin-top:8px;white-space:nowrap">{name}</div>'
                   f'<div class="cap2" style="font-size:11px;margin-top:4px;color:{col}">{"Destination" if end else state}</div>'
                   + ("" if end else f'<div class="num" style="font-size:13px;margin-top:4px;color:#E6E9FF">{c} crystals</div>') + '</div>')
    out.append("</div>")
    return "".join(out)


def park_note():
    return ('<span style="font-size:14px;color:#747BAD">The Ark parks at each cell while you work it, then sails on.</span>')


def rewards(stops):
    return f'<span class="row" style="gap:10px;font-size:15px;color:#B8C0F0">Pays in crystals  ·  {crystals_line(stops)}</span>'


def diff(d, w=100):
    return f'<span class="row" style="gap:10px;font-size:14px;color:#747BAD">{pips(d, w)}<span class="num">{d}</span></span>'


def heading(t, sub, x=40, y=34):
    return (f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;flex-direction:column;gap:8px"><span style="font-size:28px">{t}</span>'
            f'<span style="font-size:15px;color:#747BAD">{sub}</span></div>')


def dialog(body, x=80, y=30, w=1760, h=940, label="Your crew"):
    return (f'<div role="dialog" aria-label="{label}" class="glass modal" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{h}px;overflow:hidden">'
            + body + close_x(w - 84, 30) + '</div>')


# ── A: Briefing room ────────────────────────────────────────────────────────────────────────────────────────────────
def opt_a():
    pick = JOBS[0]
    seats = [(90, 170), (90, 370), (1420, 170), (1420, 370), (90, 570), (1420, 570)]

    def seat(i, j, x, y):
        on, live = i == 0, j[5]
        edge = "box-shadow:inset 0 0 0 2px #00D4FF" if on else "box-shadow:inset 0 0 0 1px rgba(67,76,137,.6)"
        return (f'<div class="abs" style="left:{x}px;top:{y}px;width:250px;display:flex;gap:14px;align-items:center;padding:10px;background:rgba(0,4,31,.85);{edge};{"" if live else "opacity:.6"}">'
                f'{face(j[0], 64, 76)}<span style="display:flex;flex-direction:column;gap:5px;min-width:0"><span class="cap2">{j[1].split()[0]}</span>'
                f'<span style="font-size:17px">{first(j[0])}</span><span class="ell" style="font-size:13px;color:{"#B8C0F0" if live else "#FFAE00"}">{"Has a job" if live else "Locked"}</span></span></div>')
    table = ('<div class="abs" style="left:400px;top:220px;width:960px;height:480px;border-radius:50%;'
             'background:radial-gradient(ellipse at 50% 50%,rgba(117,143,251,.16),rgba(0,4,31,.0) 70%);box-shadow:inset 0 0 0 1px rgba(67,76,137,.6)"></div>')
    center = (f'<div class="abs" style="left:480px;top:290px;width:800px;display:flex;flex-direction:column;align-items:center;gap:14px;text-align:center">'
              f'<span class="cap2" style="color:#E6E9FF">{pick[0]} asks</span>'
              f'<span style="font-size:28px;line-height:1.25">"{pick[3]}"</span>'
              f'<span class="row" style="gap:18px">{diff(pick[4])}{rewards(pick[7])}</span>'
              f'<div style="margin-top:10px">{stops_row(pick[7], 760)}</div>{park_note()}</div>')
    foot = (f'<div class="abs row" style="left:0;right:0;bottom:30px;justify-content:center;gap:18px">{open_btn(280, "Take the job")}'
            '<button class="btn sm soft" type="button" style="width:300px"><span class="kc">Y</span>Tamako\'s Crystal Run instead</button></div>')
    body = (heading("The briefing room", "Your crew around the route table. Pick a pilot to hear their job.")
            + table + "".join(seat(i, j, x, y) for i, (j, (x, y)) in enumerate(zip(JOBS, seats))) + center + foot)
    return page(f"Rev {REV} A - Briefing room: your crew around the route table, the job in the middle (recommended)", menu(1, scrim(dialog(body))))


# ── B: Dossier ──────────────────────────────────────────────────────────────────────────────────────────────────────
def opt_b():
    j = JOBS[1]
    hero = (f'<div class="frame" style="left:0;top:0;width:920px;height:940px"><img class="bg" src="{j[6]}" alt="">'
            '<div class="abs" style="inset:0;background:linear-gradient(90deg,rgba(0,4,31,0) 50%,rgba(0,4,31,.95) 100%)"></div>'
            f'<img src="{FACE[j[0]]}" alt="" style="position:absolute;left:120px;top:120px;width:520px;height:620px;object-fit:cover;object-position:50% 25%">'
            '<div class="abs" style="left:120px;top:740px;width:520px;height:2px;background:#E6E9FF"></div></div>')
    info = (f'<div class="abs" style="left:980px;top:110px;width:700px;display:flex;flex-direction:column;gap:18px">'
            f'<span class="cap2">{j[1]}  ·  2 of 6</span><span style="font-size:44px">{j[0]}</span>'
            f'<span style="font-size:26px;line-height:1.3;color:#E6E9FF">"{j[3]}"</span>'
            f'<span class="cap2" style="color:#E6E9FF;margin-top:10px">{j[2]}</span>'
            f'<span class="row" style="gap:18px">{diff(j[4])}{rewards(j[7])}</span>'
            f'<div style="margin-top:6px">{stops_row(j[7], 680)}</div>{park_note()}'
            f'<div class="row" style="gap:18px;margin-top:16px">{open_btn(260, "Take the job")}</div></div>')
    arrows = ''.join(
        f'<button class="pick abs" type="button" aria-label="{lab}" style="left:{x}px;top:440px;width:56px;height:56px;display:flex;align-items:center;justify-content:center;'
        f'color:#E6E9FF;box-shadow:inset 0 0 0 1px #434C89;background:rgba(0,4,31,.7)"><svg width="18" height="18" viewBox="0 0 18 18" fill="none" stroke="currentColor" '
        f'stroke-width="2" aria-hidden="true"><path d="{d}"></path></svg></button>' for lab, x, d in (("Previous pilot", 30, "M12 2L5 9l7 7"), ("Next pilot", 840, "M6 2l7 7-7 7")))
    strip = ('<div class="abs row" style="left:980px;bottom:40px;gap:12px">'
             + "".join(f'<div style="position:relative;width:96px;height:112px;{"box-shadow:inset 0 0 0 2px #00D4FF" if i == 1 else ""};{"" if x[5] else "opacity:.5"}">'
                       f'<img src="{FACE[x[0]]}" alt="" style="position:absolute;inset:{3 if i == 1 else 0}px;width:calc(100% - {6 if i == 1 else 0}px);height:calc(100% - {6 if i == 1 else 0}px);object-fit:cover;object-position:50% 25%"></div>'
                       for i, x in enumerate(JOBS)) + '</div>')
    body = hero + arrows + info + strip
    return page(f"Rev {REV} B - Dossier: one pilot at a time, full height, the job beside them, a film strip of the rest", menu(1, scrim(dialog(body))))


# ── C: Route first ──────────────────────────────────────────────────────────────────────────────────────────────────
def opt_c():
    j = JOBS[0]
    tabs = "".join(f'<button class="pick" type="button" style="height:56px;padding:0 24px;{"box-shadow:inset 0 -2px 0 #00D4FF;color:#E6E9FF" if i == 0 else "color:#747BAD"}">'
                   f'<span class="cap2" style="color:inherit">{x[2]}</span></button>' for i, x in enumerate(JOBS[:2]))
    route = (f'<div class="frame" style="left:40px;top:180px;width:1680px;height:500px"><img class="bg" src="{G["cell"]}" alt="">'
             '<div class="abs" style="inset:0;background:rgba(0,1,10,.55)"></div>'
             + ark(120, 90, 260)
             + '<svg class="abs" style="left:0;top:0" width="1680" height="500" viewBox="0 0 1680 500" fill="none" aria-hidden="true">'
               '<path d="M250 200 C 450 120, 520 380, 700 300 S 1000 120, 1150 260 S 1400 380, 1560 220" stroke="#E6E9FF" stroke-width="2" stroke-dasharray="6 8"></path></svg>'
             + "".join(f'<div class="abs" style="left:{x - 60}px;top:{y - 14}px;width:120px;display:flex;flex-direction:column;align-items:center;gap:6px">'
                       f'<span style="width:28px;height:28px;border-radius:50%;background:#00041F;box-shadow:inset 0 0 0 2px {c}"></span>'
                       f'<span style="font-size:15px;white-space:nowrap">{n}</span><span class="cap2" style="font-size:11px;color:{c}">{s}</span>'
                       + (f'<span class="num" style="font-size:13px">{k} crystals</span>' if s != "Destination" else "") + '</div>'
                       for (n, s, k, c), (x, y) in zip([("Garland", "Calm", 4, "#E6E9FF"), ("Scarab Reef", "Hostile", 7, "#FF8A7C"), ("Boneyard", "Calm", 3, "#E6E9FF"),
                                                         ("The Swarm", "Hostile", 6, "#FF8A7C"), ("The Maw", "Destination", 0, "#99FF80")],
                                                        [(250, 200), (700, 300), (1150, 260), (1380, 330), (1560, 220)]))
             + '<span class="abs cap2" style="left:24px;bottom:20px">The Ark parks at each cell while you work it, then sails on to the next.</span></div>')
    asker = (f'<div class="abs row" style="left:40px;top:710px;width:820px;gap:18px;align-items:flex-start">{face(j[0], 84, 100)}'
             f'<span style="display:flex;flex-direction:column;gap:8px"><span class="cap2">{j[0]}, {j[1]}, asks</span>'
             f'<span style="font-size:22px;line-height:1.35">"{j[3]}"</span><span class="row" style="gap:18px">{diff(j[4])}{rewards(j[7])}</span></span></div>')
    act = (f'<div class="abs" style="left:1440px;top:740px">{open_btn(280, "Take the job")}</div>')
    locked = ('<div class="abs row" style="left:40px;right:40px;bottom:30px;gap:14px;padding-top:16px;box-shadow:inset 0 1px 0 rgba(67,76,137,.6)">'
              f'{lock_line("After Milestone 1")}'
              + "".join(f'<span class="row" style="gap:8px;opacity:.6">{face(x[0], 28, 32)}<span style="font-size:13px;color:#B8C0F0">{x[2]}</span></span>' for x in JOBS[2:]) + '</div>')
    body = (heading("Pick a voyage", "") + f'<nav aria-label="Open voyages" class="abs row" style="left:40px;top:100px;gap:8px">{tabs}</nav>'
            + route + asker + act + locked)
    return page(f"Rev {REV} C - Route first: the voyage drawn as the cells the Ark parks at, the pilot who asked in the corner", menu(1, scrim(dialog(body))))


# ── D: Contracts ────────────────────────────────────────────────────────────────────────────────────────────────────
def opt_d():
    def card(j, x, rot, lift, on):
        edge = "box-shadow:inset 0 0 0 2px #00D4FF" if on else "box-shadow:inset 0 0 0 1px rgba(67,76,137,.7)"
        return (f'<div class="abs" style="left:{x}px;top:{300 - lift}px;width:300px;height:440px;transform:rotate({rot}deg);transform-origin:50% 120%;'
                f'background:#00041F;{edge};{"" if j[5] else "opacity:.7"}">'
                f'<div class="frame" style="left:1px;top:1px;width:298px;height:170px"><img class="bg" src="{j[6]}" alt=""></div>'
                f'<img src="{FACE[j[0]]}" alt="" style="position:absolute;left:20px;top:130px;width:72px;height:86px;object-fit:cover;object-position:50% 25%;box-shadow:0 0 0 3px #00041F">'
                f'<div class="abs" style="left:20px;right:20px;top:228px;display:flex;flex-direction:column;gap:8px"><span class="cap2">{j[1]}</span>'
                f'<span style="font-size:19px;line-height:1.3">{j[2]}</span>{diff(j[4], 80)}</div>'
                f'<div class="abs" style="left:20px;right:20px;bottom:18px">{open_btn(200, "Take it") if on else ("" if j[5] else lock_line("Locked"))}</div></div>')
    xs = [(160, -12, 10), (420, -6, 30), (700, 0, 90), (980, 6, 30), (1240, 12, 10)]
    order = [JOBS[2], JOBS[1], JOBS[0], JOBS[3], JOBS[4]]
    cards = "".join(card(j, x, r, l, j is JOBS[0]) for j, (x, r, l) in zip(order, xs))
    j = JOBS[0]
    detail = (f'<div class="abs" style="left:40px;right:40px;top:780px;display:flex;justify-content:space-between;align-items:flex-start">'
              f'<span style="display:flex;flex-direction:column;gap:8px;width:640px"><span class="cap2">{j[0]} asks</span><span style="font-size:20px;line-height:1.35">"{j[3]}"</span></span>'
              f'<span style="display:flex;flex-direction:column;gap:8px">{stops_row(j[7], 820)}</span></div>')
    body = heading("Contracts", "Your crew's jobs as a hand of cards. The raised one is picked; two are open now.") + cards + detail
    return page(f"Rev {REV} D - Contracts: a fanned hand of job cards from your crew, the picked one raised", menu(1, scrim(dialog(body))))


# ── E: Comms ────────────────────────────────────────────────────────────────────────────────────────────────────────
def opt_e():
    def call(i, j, on):
        edge = "box-shadow:inset 2px 0 0 #00D4FF;background:rgba(0,212,255,.06)" if on else "box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)"
        bars = "".join(f'<span style="width:3px;height:{h}px;background:{"#E6E9FF" if j[5] else "#434C89"}"></span>' for h in (8, 14, 22, 12, 18, 9, 16))
        return (f'<div class="row" style="gap:16px;height:100px;padding:0 20px;{edge};{"" if j[5] else "opacity:.6"}">'
                f'{face(j[0], 56, 66)}<span style="display:flex;flex-direction:column;gap:6px;min-width:0;flex:1"><span class="cap2">{j[0]}  ·  {j[1]}</span>'
                f'<span class="ell" style="font-size:16px;color:#B8C0F0">"{j[3]}"</span></span>'
                f'<span class="row" style="gap:3px;align-items:center">{bars}</span>{"" if j[5] else f"<span style=color:#FFAE00>{LOCK}</span>"}</div>')
    inbox = ('<div class="abs" style="left:40px;top:130px;width:720px;display:flex;flex-direction:column">'
             '<span class="cap2" style="padding:0 20px 10px">Incoming  ·  6</span>' + "".join(call(i, j, i == 0) for i, j in enumerate(JOBS)) + '</div>')
    j = JOBS[0]
    opened = (f'<div class="abs" style="left:820px;top:130px;width:900px;display:flex;flex-direction:column;gap:20px">'
              f'<div class="frame" style="position:relative;width:900px;height:300px"><img class="bg" src="{j[6]}" alt="">{ark(300, 60, 320)}'
              f'<div class="abs row" style="left:20px;bottom:18px;gap:12px;padding:8px 12px;background:rgba(0,4,31,.85)">{face(j[0], 40, 48)}'
              f'<span style="font-size:14px">{j[0]}, on the radio</span></div></div>'
              f'<span style="font-size:28px;line-height:1.25">"{j[3]}"</span>'
              f'<span class="cap2" style="color:#E6E9FF">{j[2]}</span><span class="row" style="gap:18px">{diff(j[4])}{rewards(j[7])}</span>'
              f'{stops_row(j[7], 900)}{park_note()}'
              f'<div class="row" style="gap:18px">{open_btn(260, "Answer: take the job")}'
              '<button class="btn sm soft" type="button" style="width:200px"><span class="kc">B</span>Not now</button></div></div>')
    body = heading("Comms", "Your crew calls when they have work. Open a call to hear it.") + inbox + opened
    return page(f"Rev {REV} E - Comms: your crew's calls as an inbox, the picked call opened with its route", menu(1, scrim(dialog(body))))


BOARDS = {f"R{REV}AB.dc.html": lambda: pair(opt_a, opt_b, f"Rev {REV} AB - Crew A: Briefing room (recommended), and Crew B: Dossier"),
          f"R{REV}CD.dc.html": lambda: pair(opt_c, opt_d, f"Rev {REV} CD - Crew C: Route first, and Crew D: Contracts"),
          f"R{REV}E.dc.html": opt_e}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
