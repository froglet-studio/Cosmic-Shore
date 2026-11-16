#!/usr/bin/env python3
"""Rev 113 boards: Voyages Milestone 1, one voyage end to end (Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-16: "Lets make one voyage good ... Lets keep all other voyages from crew locked for now." His list:
the lobby-or-squad confusion ("I want lobby approach to be the main task. Now when I select a vessel it doesnt really get
applied in the voyage I selected"), the crew-recommends screen as "a popup from that window and categorization between
hunters, pirates etc", an in-game HUD ("which cell is coming what the ark is doing we should use both the toast and the
objective box"), and "at the end of the voyage there should be a scoreboard with rewards and another button called venture
into the voage or finish". Then: "It can be something steal these many elemental crystals during the voyage and few cells
attack you few dont" (Crystal Run), and "Please use frogma to draw under the voyages track".

The numbers follow the Milestone 1 game plan (Claude Docs, "Voyages Milestone 1: game plan"): Escort the Ark is five
worlds ending at The Maw, breach line 25% at difficulty 2; Crystal Run is 20 crystals in 4 cells, 2 hostile, breach 40%.

  Compare  the toasts across one voyage, each beside the moment that raises it
  Lobby    your Ark, lobby only; a saved squad only fills the open AI seats; "You sail as" beside Set sail (recommended)
  Picker   the seat popup on Vessel: one press saves the seat for this voyage and says so
  Crew     crew recommends as a popup: Escort the Ark and Crystal Run open, the rest by faction, locked
  Hud      Escort the Ark in flight: the objective box and a threat toast
  Run      Crystal Run in flight: the crystal counter leads; calm and hostile cells on the route
  Score    the end: the Ark's hull, a row per seat, rewards, Finish and Venture further
  Notes

    python3 Docs/MenuRedesignB/rounds/r113_voyages.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "113"
_spec = importlib.util.spec_from_file_location("r108", HERE / "r108_voyages.py")
r108 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, "108"]
_spec.loader.exec_module(r108)
r100 = r108.r100

REV = int(_rev)
ROOT = r100.ROOT
blob = r100.blob
HULL, FLOWER, LOCK = r100.HULL, r100.FLOWER, r100.LOCK
FACE = {p[1]: p[4] for p in r108.PILOTS}
ARK = blob("f2a0a3e9c284a960c539d417b46cd1eb")
FOAM = blob("1837bd8d6221b1584ff604aecf5d8ae1")
LOGIC = "class Component extends DCLogic {\nrenderVals() { return {}; }\n}\n"

r100.HELMET = r100.HELMET.replace("</style>", (
    ".toast{position:absolute;left:50%;transform:translateX(-50%);display:flex;flex-direction:column;align-items:center;gap:6px;padding:14px 140px;white-space:nowrap}\n"
    ".toast b{font-family:'Chakra Petch',sans-serif;font-weight:700;font-size:30px;letter-spacing:.18em}\n"
    ".toast i{font-style:normal;font-size:19px;color:#E6E9FF}\n"
    ".t-event{background:linear-gradient(90deg,rgba(0,4,31,0),rgba(0,4,31,.9) 22%,rgba(0,4,31,.9) 78%,rgba(0,4,31,0))}\n"
    ".t-event b{color:#E6E9FF}\n"
    ".t-threat{background:linear-gradient(90deg,rgba(80,10,20,0),rgba(80,10,20,.88) 22%,rgba(80,10,20,.88) 78%,rgba(80,10,20,0))}\n"
    ".t-threat b{color:#FF8A7C}\n"
    ".t-good{background:linear-gradient(90deg,rgba(0,4,31,0),rgba(0,4,31,.9) 22%,rgba(0,4,31,.9) 78%,rgba(0,4,31,0));box-shadow:inset 0 -2px 0 rgba(153,255,128,.0)}\n"
    ".t-good b{color:#99FF80}\n"
    ".obj{position:absolute;left:40px;top:40px;width:600px;padding:22px 26px;box-sizing:border-box;background:rgba(0,4,31,.84);box-shadow:inset 2px 0 0 #E6E9FF}\n"
    ".bar{position:relative;height:12px;background:rgba(67,76,137,.45)}\n"
    ".bar>span{position:absolute;left:0;top:0;bottom:0}\n"
    ".breach{position:absolute;top:-5px;bottom:-5px;width:2px;background:#FF4B3A}\n"
    ".tag{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.14em;text-transform:uppercase}\n"
    "</style>"))


def page(title, body):
    return r100.page(title, body, LOGIC)


def flower(el, size=22):
    return f'<img src="{FLOWER[el]}" alt="{el}" style="width:{size}px;height:{size}px;vertical-align:-5px">'


def pips(n, w=124):
    return f'<span class="pips" style="width:{w}px"><span style="width:{n / 9 * 100:.1f}%"></span></span>'


def menu(step, body):
    return r108.SEA + r100.r70.nav(crystals=False) + r100.header(step) + body


# ── Lobby: one page, your seat first ────────────────────────────────────────────────────────────────────────────────
def seat(x, role, who, v, pilot, el, art, badge, foot, cls="glass"):
    return (f'<div class="{cls}" style="position:absolute;left:{x}px;top:300px;width:590px;height:540px">'
            f'<div style="position:absolute;left:28px;right:28px;top:26px;display:flex;align-items:baseline;justify-content:space-between">'
            f'<span style="display:flex;flex-direction:column;gap:6px"><span class="lab">{role}</span><span style="font-size:22px">{who}</span></span>{badge}</div>'
            f'<span aria-hidden="true" style="position:absolute;left:6%;right:6%;top:120px;height:210px;background:radial-gradient(ellipse 50% 50% at 50% 50%,rgba(150,172,252,.22),rgba(150,172,252,0))"></span>'
            f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:8%;width:84%;top:100px;height:220px">'
            f'<div style="position:absolute;left:28px;right:28px;top:350px;display:flex;align-items:center;gap:18px">'
            f'<img src="{FACE[pilot]}" alt="" style="width:72px;height:86px;object-fit:cover;object-position:50% 28%;border-radius:4px">'
            f'<span style="display:flex;flex-direction:column;gap:8px;min-width:0;flex:1"><span style="font-size:22px">{v}</span>'
            f'<span class="ell" style="font-size:15px;color:#747BAD">{pilot}  ·  {art}</span></span>'
            f'<span style="display:flex;flex-direction:column;align-items:center;gap:6px">{flower(el, 32)}<span style="font-size:14px;color:#747BAD">{el}</span></span></div>'
            f'<div style="position:absolute;left:28px;right:28px;bottom:26px;height:48px;display:flex;align-items:center;gap:8px">{foot}</div></div>')


def chip(word, col="#E6E9FF"):
    return (f'<span class="cap" style="font-size:14px;color:{col};display:flex;align-items:center;gap:8px;white-space:nowrap">'
            f'<span style="width:8px;height:8px;border-radius:50%;background:{col}"></span>{word}</span>')


def quick(words):
    return "".join(f'<button class="btn sm soft chipq" type="button" style="flex:1;height:40px;padding:0 8px;font-size:14px">{w}</button>' for w in words)


VOYAGE_CHIP = ('<div class="abs" style="left:1180px;top:176px;width:700px;height:72px;display:flex;align-items:center;gap:18px;z-index:3">'
               f'<img src="{ARK}" alt="The Ark" style="width:110px;height:60px;object-fit:contain">'
               '<span style="display:flex;flex-direction:column;gap:8px;min-width:0"><span class="ell" style="font-size:18px">Escort the Ark to The Maw</span>'
               f'<span style="display:flex;align-items:center;gap:12px;font-size:14px;color:#747BAD"><span>Ark escort  ·  5 worlds</span>{pips(2)}<span class="num">2</span></span></span></div>')


def lobby_body(extra=""):
    head = ('<div class="abs" style="left:40px;top:186px;display:flex;flex-direction:column;gap:12px;z-index:3">'
            '<span class="lab" style="color:#E6E9FF">Your lobby</span>'
            '<span style="font-size:15px;color:#747BAD">Pick your seat. Friends who join pick their own. Empty seats fly with AI wingmates.</span></div>')
    seats = (seat(40, "Leader", "You", "Manta", "Tamako Deephook", "Space", "No artifact", chip("Flies this voyage"),
                  quick(["Vessel", "Pilot", "Element", "Artifact"]))
             + seat(665, "Seat 2  ·  in your lobby", "Rook", "Dolphin", "Ilia Silvergate", "Charge", "No artifact", chip("Ready"),
                    '<span style="font-size:15px;color:#747BAD">Rook picks their own seat; you see it here as they choose.</span>')
             + seat(1290, "Seat 3  ·  open", "AI wingmate", "Sparrow", "Mathalo Kelpbloom", "Time", "No artifact", chip("AI", "#747BAD"),
                    '<button class="btn sm soft chipq" type="button" style="flex:1;height:40px;font-size:14px">Invite a friend</button>'
                    '<button class="btn sm soft chipq" type="button" style="flex:1;height:40px;font-size:14px">Change wingmate</button>'))
    foot = ('<div class="abs" style="left:40px;top:878px;z-index:3"><button class="btn soft chipq" type="button" style="width:520px"><span class="kc">Y</span>Fill the AI seats from a saved squad</button></div>'
            '<div class="abs" style="left:860px;top:878px;height:56px;width:360px;display:flex;align-items:center;justify-content:flex-end;gap:10px;font-size:16px;color:#747BAD;z-index:3">'
            f'You sail as <span style="color:#E6E9FF">Manta  ·  Space</span>{flower("Space", 20)}</div>'
            '<div class="abs" style="left:1240px;top:878px;display:flex;gap:20px;z-index:3"><button class="btn soft" type="button" style="width:200px"><span class="kc">B</span>Back</button>'
            '<button class="btn soft-cta" type="button" style="width:420px"><span class="kc">A</span>Set sail</button></div>')
    return menu(3, head + VOYAGE_CHIP + seats + foot + extra)


def lobby_board():
    return page(f"Rev {REV} Lobby - Your Ark, lobby only: what you pick is what you fly, read back beside Set sail (recommended)", lobby_body())


# ── Picker: the seat popup on Vessel ────────────────────────────────────────────────────────────────────────────────
def picker_board():
    tabs = [("Vessel", "Manta", True), ("Pilot", "Tamako Deephook", False), ("Element", "Space", False), ("Artifact", "No artifact", False)]
    tabrow = "".join(f'<span style="display:flex;flex-direction:column;gap:8px;padding-bottom:10px;box-shadow:inset 0 -2px 0 {"#00D4FF" if on else "rgba(0,0,0,0)"}">'
                     f'<span class="cap" style="font-size:15px;color:{"#E6E9FF" if on else "#747BAD"}">{n}</span><span style="font-size:14px;color:#747BAD">{now}</span></span>'
                     for n, now, on in tabs)
    vessels = [("Manta", "Glider: wide wings, long skims", True, True), ("Rhino", "Ram: breaks shields, lays heavy walls", True, False),
               ("Dolphin", "Skimmer: rides the trail, jumps it", False, False), ("Sparrow", "Gunship: cannons and a turret stance", False, False),
               ("Squirrel", "Racer: drifts and boosts", False, False), ("Serpent", "Hunter: coils, strikes from range", False, False)]
    grid = "".join(
        f'<button class="tile pick" type="button" aria-pressed="{"true" if on else "false"}" style="position:relative;height:170px;background:rgba(0,4,31,.7);'
        f'box-shadow:{"inset 0 0 0 2px #00D4FF" if on else "inset 0 0 0 1px rgba(67,76,137,.8)"};display:flex;flex-direction:column;align-items:center;justify-content:center;gap:10px">'
        + (f'<span class="cap" style="position:absolute;left:8px;top:8px;font-size:14px;padding:2px 8px;background:rgba(0,1,10,.85);color:#E6E9FF;box-shadow:inset 0 -2px 0 #00D4FF">For this voyage</span>' if fit else "")
        + f'<img class="hull" src="{HULL[n]}" alt="" style="width:70%;height:80px"><span style="font-size:18px">{n}</span></button>'
        for n, _, fit, on in vessels)
    prev = ('<aside aria-label="Your pick" style="width:400px;flex:none;position:relative;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px rgba(67,76,137,.6)">'
            f'<img class="hull ship" src="{HULL["Manta"]}" alt="" style="position:absolute;left:6%;width:88%;top:70px;height:220px">'
            '<div style="position:absolute;inset:0;background:linear-gradient(180deg,rgba(0,1,10,0) 40%,rgba(0,1,10,.95) 75%)"></div>'
            '<div style="position:absolute;left:28px;right:28px;bottom:28px;display:flex;flex-direction:column;gap:12px">'
            '<span class="lab" style="color:#E6E9FF">For this voyage</span><span style="font-size:28px;line-height:1.2">Manta</span>'
            '<span style="font-size:15px;line-height:1.5;color:#B8C0F0">Wide and steady. Holds space beside the Ark and turns fauna away from its hull.</span></div></aside>')
    saved = ('<div style="display:flex;align-items:center;gap:14px;height:56px;padding:0 20px;background:rgba(0,4,31,.8);box-shadow:inset 2px 0 0 #E6E9FF">'
             '<svg width="18" height="14" viewBox="0 0 18 14" fill="none" stroke="#E6E9FF" stroke-width="2" aria-hidden="true"><path d="M1 7l5 5L17 1"></path></svg>'
             '<span style="font-size:17px">Manta is your vessel for Escort the Ark. Your seat card behind has changed.</span></div>')
    close = ('<button class="pick" type="button" aria-label="Close" style="width:44px;height:44px;display:flex;align-items:center;justify-content:center;color:#E6E9FF;box-shadow:inset 0 0 0 1px #434C89">'
             '<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M2 2l12 12M14 2L2 14"></path></svg></button>')
    pop = ('<div class="abs" style="left:0;top:84px;width:1920px;height:996px;z-index:8;background:rgba(0,1,10,.72);backdrop-filter:blur(6px);-webkit-backdrop-filter:blur(6px)">'
           '<div role="dialog" aria-label="Change your seat" class="glass modal" style="position:absolute;left:200px;top:70px;width:1520px;height:830px">'
           '<div style="position:absolute;left:40px;right:40px;top:30px;display:flex;align-items:flex-start;justify-content:space-between">'
           f'<div style="display:flex;flex-direction:column;gap:18px"><span style="font-size:28px">Change your seat</span><div style="display:flex;gap:40px">{tabrow}</div></div>{close}</div>'
           '<div style="position:absolute;left:40px;right:40px;top:180px;bottom:100px;display:flex;gap:32px">'
           f'<div style="flex:1;min-width:0;display:flex;flex-direction:column;gap:22px"><div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px">{grid}</div>{saved}</div>{prev}</div>'
           '<div style="position:absolute;left:40px;right:40px;bottom:30px;display:flex;align-items:center;justify-content:space-between">'
           '<span style="font-size:15px;color:#747BAD">One press saves it to your seat for this voyage, the same seat Set sail reads.</span>'
           '<button class="btn soft-cta" type="button" style="width:300px"><span class="kc">A</span>Done</button></div></div></div>')
    return page(f"Rev {REV} Picker - The seat popup on Vessel: one press saves your seat for this voyage, and says so", lobby_body(pop))


# ── Crew recommends: a popup, by faction ────────────────────────────────────────────────────────────────────────────
CREW_JOBS = [("Hunters", [("Ilia Silvergate", "Patrol Saltmarch", 3), ("Mathalo Kelpbloom", "Patrol Verdigris", 3)]),
             ("Pirates", [("Ekora Hollowbrook", "Raid the hoard at Magpie Hollow", 5)]),
             ("Merchants", [("Quivera Pearlspark", "Carry a sealed dispatch to Aurum Harbour", 2), ("Karenth Amberweaver", "Carry sealed cargo to Tollmoor", 4)]),
             ("Thieves", [("Tamako Deephook", "Open the vaults at Lantern Reach", 4)])]


def open_card(img, kind, title, meta):
    return (f'<div class="glass" style="position:relative;height:176px;box-shadow:inset 0 0 0 1px #00D4FF">'
            f'<div style="position:absolute;left:24px;top:22px;width:150px;height:80px;display:flex;align-items:center;justify-content:center">{img}</div>'
            f'<div style="position:absolute;left:196px;right:24px;top:22px;display:flex;flex-direction:column;gap:8px"><span class="lab">{kind}</span>'
            f'<span style="font-size:23px;line-height:1.25">{title}</span></div>'
            f'<div style="position:absolute;left:24px;bottom:24px;display:flex;align-items:center;gap:12px;font-size:14px;color:#747BAD">{meta}</div>'
            '<button class="btn sm soft-cta" type="button" style="position:absolute;right:20px;bottom:16px;width:220px"><span class="kc">A</span>Open</button></div>')


CRYSTALS = ('<svg width="130" height="70" viewBox="0 0 130 70" fill="none" stroke="#E6E9FF" stroke-width="2" aria-hidden="true">'
            '<path d="M24 35L37 10l13 25-13 25z"></path><path d="M58 30l9-16 9 16-9 16z"></path><path d="M86 40l11-22 11 22-11 22z"></path></svg>')


def crew_board():
    rail = [("All", 8, True)] + [(f, len(j), False) for f, j in CREW_JOBS]
    rail_html = "".join(f'<div style="display:flex;justify-content:space-between;align-items:center;height:54px;padding:0 18px;'
                        f'{"box-shadow:inset 2px 0 0 #00D4FF;background:rgba(0,212,255,.06);color:#E6E9FF" if on else "color:#747BAD"}">'
                        f'<span class="cap" style="font-size:15px">{n}</span><span class="num" style="font-size:15px">{c}</span></div>' for n, c, on in rail)
    rail_html += '<p style="margin:18px 0 0;padding:0 18px;font-size:14px;line-height:1.5;color:#5C5F70">Factions with no work from your crew are left out.</p>'

    def job(p, j, d):
        return (f'<div style="position:relative;height:100px;display:flex;align-items:center;gap:16px;padding:0 18px;background:rgba(0,4,31,.7);'
                f'box-shadow:inset 0 0 0 1px rgba(67,76,137,.6);opacity:.72">'
                f'<img src="{FACE[p]}" alt="" style="width:58px;height:70px;object-fit:cover;object-position:50% 28%;border-radius:4px">'
                f'<span style="display:flex;flex-direction:column;gap:6px;min-width:0;flex:1"><span class="lab" style="font-size:13px">{p} recommends</span>'
                f'<span class="ell" style="font-size:18px">{j}</span><span style="display:flex;align-items:center;gap:10px;font-size:14px;color:#747BAD">{pips(d, 90)}<span class="num">{d}</span></span></span>'
                f'<span style="color:#FFAE00">{LOCK}</span></div>')
    groups = "".join(f'<div style="display:flex;flex-direction:column;gap:10px"><span class="lab" style="font-size:13px">{f}</span>'
                     f'<div style="display:grid;grid-template-columns:1fr 1fr;gap:14px">{"".join(job(*x) for x in jobs)}</div></div>'
                     for f, jobs in CREW_JOBS[:3])
    body = (f'<div class="abs" style="left:1520px;top:176px;z-index:3"><button class="btn soft chipq" type="button" style="width:360px">Crew recommends  ·  8</button></div>'
            '<div class="abs" style="left:0;top:84px;width:1920px;height:996px;z-index:8;background:rgba(0,1,10,.72);backdrop-filter:blur(6px);-webkit-backdrop-filter:blur(6px)">'
            '<div role="dialog" aria-label="Your crew recommends" class="glass modal" style="position:absolute;left:200px;top:40px;width:1520px;height:900px">'
            '<div style="position:absolute;left:40px;right:40px;top:30px;display:flex;align-items:flex-start;justify-content:space-between">'
            '<div style="display:flex;flex-direction:column;gap:10px"><span style="font-size:28px">Your crew recommends</span>'
            '<span style="font-size:15px;color:#747BAD">Each pilot on your Ark has work they would like you to take.</span></div>'
            '<button class="pick" type="button" aria-label="Close" style="width:44px;height:44px;display:flex;align-items:center;justify-content:center;color:#E6E9FF;box-shadow:inset 0 0 0 1px #434C89">'
            '<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M2 2l12 12M14 2L2 14"></path></svg></button></div>'
            f'<nav aria-label="Factions" style="position:absolute;left:40px;top:130px;width:280px">{rail_html}</nav>'
            '<div style="position:absolute;left:360px;right:40px;top:130px;display:flex;flex-direction:column;gap:22px">'
            '<span class="lab" style="color:#E6E9FF">Open now</span>'
            '<div style="display:grid;grid-template-columns:1fr 1fr;gap:18px;margin-top:-10px">'
            + open_card(f'<img src="{ARK}" alt="The Ark" style="width:150px;height:80px;object-fit:contain">', "Ark escort", "Escort the Ark to The Maw", pips(2, 100) + '<span class="num">2</span><span>5 worlds</span>')
            + open_card(CRYSTALS, "Crew voyage  ·  any pilot", "Crystal Run: steal 20 crystals before Tollmoor", pips(3, 100) + '<span class="num">3</span><span>4 cells, 2 hostile</span>')
            + '</div>'
            f'<div style="display:flex;justify-content:space-between;align-items:center"><span class="lab">Crew voyages</span>'
            f'<span style="display:flex;align-items:center;gap:8px;font-size:15px;color:#FFAE00">{LOCK}Locked for Milestone 1</span></div>'
            f'{groups}<span class="lab" style="font-size:13px;color:#5C5F70">Thieves  ·  1 more below</span></div>'
            '</div></div>')
    return page(f"Rev {REV} Crew - Crew recommends as a popup: Escort the Ark and Crystal Run open, the rest by faction, locked", menu(1, body))


# ── In flight ───────────────────────────────────────────────────────────────────────────────────────────────────────
def flight_ground(tint):
    return (f'<img src="{FOAM}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:.32">'
            f'<div class="abs" style="inset:0;background:radial-gradient(ellipse 60% 55% at 52% 55%,{tint},rgba(0,4,31,.55) 60%,rgba(0,1,10,.92) 100%)"></div>')


def route(stops, done_pct):
    n = len(stops)
    out = [f'<div style="position:relative;height:{86 if any(len(s) > 2 for s in stops) else 66}px;margin-top:18px">'
           '<div style="position:absolute;left:10px;right:10px;top:9px;height:2px;background:rgba(67,76,137,.8)"></div>'
           f'<div style="position:absolute;left:10px;width:{done_pct}%;top:9px;height:2px;background:#E6E9FF"></div>']
    for i, s in enumerate(stops):
        name, state = s[0], s[1]
        tag = s[2] if len(s) > 2 else ""
        x = i / (n - 1) * 100
        last = i == n - 1
        dot = {"done": "background:#E6E9FF", "now": "background:#E6E9FF;box-shadow:0 0 0 5px rgba(230,233,255,.22)",
               "end": "background:#00041F;box-shadow:inset 0 0 0 2px #99FF80"}.get(state, "background:#00041F;box-shadow:inset 0 0 0 2px #747BAD")
        col = {"now": "#E6E9FF", "done": "#B8C0F0", "end": "#99FF80"}.get(state, "#747BAD")
        w, off = 112, (92 if last else 56 if i else 0)
        align = "right" if last else "center" if i else "left"
        m = "0 0 0 auto" if last else "0 auto" if i else "0"
        tagh = (f'<div class="tag" style="font-size:11px;margin-top:2px;color:{"#FF8A7C" if tag == "Hostile" else "#747BAD"}">{tag}</div>' if tag else "")
        out.append(f'<div style="position:absolute;left:calc({x:.2f}% - {off}px);top:0;width:{w}px;text-align:{align}">'
                   f'<div style="width:20px;height:20px;border-radius:50%;{dot};margin:{m}"></div>'
                   f'<div style="font-size:13px;margin-top:6px;white-space:nowrap;color:{col}">{name}</div>{tagh}</div>')
    out.append("</div>")
    return "".join(out)


def hull_bar(pct, breach):
    return (f'<div style="display:flex;justify-content:space-between;margin-top:18px"><span class="lab">Ark hull</span><span class="num" style="font-size:16px">{pct}%</span></div>'
            f'<div class="bar" style="margin-top:8px"><span style="width:{pct}%;background:#E6E9FF"></span><span class="breach" style="left:{breach}%"></span></div>')


def marker(x, y):
    return (f'<div class="abs" style="left:{x - 34}px;top:{y - 34}px;width:68px;height:68px;border-radius:50%;box-shadow:inset 0 0 0 3px #FF4B3A;opacity:.95">'
            '<svg width="68" height="68" viewBox="-34 -34 68 68" aria-hidden="true"><path d="M-14 0L0-10 14 0 0 10z" fill="#5a0e18" stroke="#FF8A7C" stroke-width="2"></path></svg></div>')


VESSEL_HUD = ('<div class="abs" style="right:40px;bottom:40px;width:420px;height:150px;box-shadow:inset 0 0 0 1px rgba(67,76,137,.6);display:flex;'
              'align-items:center;justify-content:center"><span class="lab" style="color:#5C5F70">Vessel HUD, as today</span></div>')


def hud_board():
    box = ('<div class="obj"><span class="lab" style="color:#E6E9FF">Escort the Ark</span>'
           '<div style="font-size:23px;line-height:1.3;margin-top:6px">Keep the Ark\'s hull above 25% to The Maw\'s core</div>'
           + route([("Garland", "done"), ("Scarab Reef", "now"), ("Boneyard", "next"), ("The Swarm", "next"), ("The Maw", "end")], 25)
           + hull_bar(78, 25)
           + '<div style="font-size:16px;color:#B8C0F0;margin-top:12px">The Ark is crossing Scarab Reef (hostile), 0:48 to The Boneyard</div>'
           '<div style="display:flex;gap:10px;margin-top:14px;padding-top:14px;box-shadow:inset 0 1px 0 rgba(67,76,137,.6);font-size:19px;color:#FF8A7C">'
           '<span>&#9656;</span>Break off the 3 fauna on the Ark\'s left side</div></div>')
    world = (f'<img src="{ARK}" alt="The Ark" style="position:absolute;left:720px;top:430px;width:560px;height:262px;object-fit:contain">'
             + marker(740, 500) + marker(700, 610) + marker(800, 660)
             + f'<img class="hull" src="{HULL["Manta"]}" alt="Your Manta" style="position:absolute;left:880px;top:780px;width:200px;height:90px">'
             '<div class="abs" style="left:28px;top:820px;display:flex;align-items:center;gap:12px;color:#FF8A7C">'
             '<svg width="34" height="40" viewBox="0 0 34 40" aria-hidden="true"><path d="M0 20L34 0v40z" fill="#FF4B3A"></path></svg><span class="tag">2 more</span></div>'
             '<span class="abs lab" style="left:1470px;top:300px;color:#B8C0F0">The Boneyard</span>')
    toast = '<div class="toast t-threat" style="top:150px;left:1180px"><b>FAUNA ARE HUNTING THE ARK</b><i>Kill what bites the Ark. Red markers show where.</i></div>'
    return page(f"Rev {REV} Hud - Escort the Ark HUD: the objective box (goal, five worlds, hull and breach line, your job) and a threat toast",
                flight_ground("rgba(117,143,251,.18)") + world + box + toast + VESSEL_HUD)


def run_board():
    split = "".join(f'<span style="display:flex;align-items:center;gap:8px">{flower(el, 20)}<span class="num">{el} {n}</span></span>'
                    for el, n in (("Space", 5), ("Time", 4), ("Charge", 2), ("Mass", 1)))
    box = ('<div class="obj"><span class="lab" style="color:#E6E9FF">Crystal Run</span>'
           '<div style="font-size:23px;line-height:1.3;margin-top:6px">Steal 20 crystals before the Ark reaches Tollmoor</div>'
           '<div style="display:flex;justify-content:space-between;align-items:baseline;margin-top:18px"><span class="lab">Crystals</span>'
           '<span class="num" style="font-size:38px">12<span style="font-size:20px;color:#747BAD"> / 20</span></span></div>'
           '<div class="bar" style="margin-top:8px;height:14px"><span style="width:60%;background:#E6E9FF"></span></div>'
           f'<div style="display:flex;gap:22px;margin-top:12px;font-size:15px;color:#B8C0F0">{split}</div>'
           + route([("Cell 1", "done", "Calm"), ("Cell 2", "now", "Hostile"), ("Cell 3", "next", "Calm"), ("Tollmoor", "end", "Hostile")], 33)
           + hull_bar(70, 40)
           + '<div style="display:flex;gap:10px;margin-top:14px;padding-top:14px;box-shadow:inset 0 1px 0 rgba(67,76,137,.6);font-size:19px;color:#FF8A7C">'
           '<span>&#9656;</span>A hostile cell: rich in crystals, but its fauna go for the Ark</div></div>')
    gems = "".join(f'<svg class="abs" style="left:{x}px;top:{y}px" width="26" height="36" viewBox="0 0 26 36" fill="rgba(230,233,255,.12)" stroke="#E6E9FF" stroke-width="2" aria-hidden="true"><path d="M13 1l12 17-12 17L1 18z"></path></svg>'
                   for x, y in ((1180, 300), (1260, 370), (1340, 280), (1420, 430), (1240, 520), (1500, 340), (1540, 500), (1400, 600), (1620, 410), (1130, 430)))
    world = (f'<img src="{ARK}" alt="The Ark" style="position:absolute;left:520px;top:520px;width:480px;height:225px;object-fit:contain">'
             + marker(560, 580) + marker(610, 700) + gems
             + f'<img class="hull" src="{HULL["Sparrow"]}" alt="Your Sparrow" style="position:absolute;left:1270px;top:440px;width:170px;height:80px">')
    toast = '<div class="toast t-event" style="top:150px;left:1180px"><b>ENTERING CELL 2. HOSTILE.</b><i>More crystals here. Its fauna are hunting the Ark.</i></div>'
    return page(f"Rev {REV} Run - Crystal Run HUD: the crystal counter leads, calm and hostile cells on the route",
                flight_ground("rgba(166,0,255,.14)") + world + box + toast + VESSEL_HUD)


# ── The toasts, side by side ────────────────────────────────────────────────────────────────────────────────────────
TOASTS = [("t-event", "ESCORT THE ARK TO THE MAW", "Five worlds. Keep its hull above 25%.", "The voyage starts"),
          ("t-event", "ENTERING SCARAB REEF. HOSTILE.", "Kill what bites the Ark", "A new world or cell"),
          ("t-threat", "FAUNA ARE HUNTING THE ARK", "Red markers show where", "A hostile cell begins"),
          ("t-threat", "ARK UNDER ATTACK", "", "The hull drops fast"),
          ("t-threat", "BREACH CLOSE", "The Ark is within 10% of the breach line", "Within 10% of the line"),
          ("t-good", "20 CRYSTALS. BRING THEM HOME.", "", "Crystal Run goal reached"),
          ("t-event", "FINAL CELL: THE DESTINATION", "", "The last cell")]


def compare_board():
    rows = "".join(f'<span class="abs lab" style="left:60px;top:{176 + i * 126}px;width:360px">{w}</span>'
                   f'<div class="toast {k}" style="top:{150 + i * 126}px;left:1140px;padding:10px 120px"><b style="font-size:26px">{b}</b>'
                   + (f'<i style="font-size:17px">{s}</i>' if s else "") + "</div>"
                   for i, (k, b, s, w) in enumerate(TOASTS))
    head = ('<div class="abs" style="left:60px;top:48px;display:flex;flex-direction:column;gap:10px">'
            '<span style="font-size:28px">The toasts across one voyage</span>'
            '<span style="font-size:15px;color:#747BAD">A few seconds each, top centre, one at a time. The objective box holds what stays true; a toast marks what just changed. No toast per crystal.</span></div>')
    return page(f"Rev {REV} - the toasts across one voyage, each beside the moment that raises it", flight_ground("rgba(117,143,251,.10)") + head + rows)


# ── The end ─────────────────────────────────────────────────────────────────────────────────────────────────────────
def score_board():
    rows = [("You", "Manta  ·  Tamako", 14, "1,240", 22, "3,450", True), ("Rook", "Dolphin  ·  Ilia", 9, "860", 31, "2,910", False),
            ("AI wingmate", "Sparrow  ·  Mathalo", 6, "410", 12, "1,520", False)]
    th = "".join(f'<span class="lab" style="text-align:{"left" if i == 0 else "right"}">{h}</span>' for i, h in enumerate(("Seat", "Kills", "Hull saved", "Crystals", "Score")))
    tr = "".join(f'<div style="display:grid;grid-template-columns:2.4fr 1fr 1fr 1fr 1fr;align-items:center;height:64px;padding:0 16px;box-shadow:inset 0 1px 0 rgba(67,76,137,.5);'
                 f'{"background:rgba(230,233,255,.05)" if me else ""}"><span style="font-size:20px">{w} <span style="font-size:15px;color:#747BAD">  ·  {v}</span></span>'
                 + "".join(f'<span class="num" style="text-align:right;font-size:20px">{c}</span>' for c in (k, h, cr, sc)) + "</div>"
                 for w, v, k, h, cr, sc, me in rows)
    left = ('<div class="glass" style="position:absolute;left:120px;top:300px;width:1080px;height:470px;padding:30px 32px;box-sizing:border-box">'
            '<div style="display:flex;justify-content:space-between;align-items:baseline"><span class="lab">Your Ark</span>'
            '<span style="display:flex;align-items:baseline;gap:14px"><span class="lab">Hull at the core</span><span class="num" style="font-size:32px">62%</span></span></div>'
            '<div class="bar" style="margin:14px 0 28px"><span style="width:62%;background:#E6E9FF"></span><span class="breach" style="left:25%"></span></div>'
            f'<div style="display:grid;grid-template-columns:2.4fr 1fr 1fr 1fr 1fr;padding:0 16px 12px">{th}</div>{tr}'
            '<div style="display:flex;align-items:center;gap:14px;margin-top:24px;font-size:16px;color:#B8C0F0">'
            '<span class="cap" style="font-size:13px;padding:4px 10px;box-shadow:inset 0 0 0 1px #E6E9FF;color:#E6E9FF">Best escort</span>You kept the most fauna off the Ark.</div></div>')
    right = ('<div class="glass" style="position:absolute;left:1230px;top:300px;width:570px;height:470px;padding:30px 32px;box-sizing:border-box;display:flex;flex-direction:column;gap:26px">'
             '<span class="lab">Rewards</span>'
             '<div><div style="display:flex;justify-content:space-between;font-size:21px"><span>Crystals</span><span class="num" style="color:#FFAE00">+ 60</span></div>'
             '<div style="font-size:14px;color:#747BAD;margin-top:4px">Scaled by difficulty 2</div></div>'
             '<div><div style="display:flex;justify-content:space-between;font-size:21px"><span>Experience</span><span class="num">+ 1,200</span></div>'
             '<div class="bar" style="margin-top:10px"><span style="width:72%;background:#E6E9FF"></span></div>'
             '<div style="font-size:14px;color:#747BAD;margin-top:6px">Ensign  ·  720 to Lieutenant</div></div>'
             '<div style="display:flex;justify-content:space-between;font-size:21px"><span>Tamako Deephook\'s trust</span><span class="num">+ 1</span></div></div>')
    head = ('<div class="abs" style="left:0;width:1920px;top:110px;text-align:center;display:flex;flex-direction:column;gap:10px;align-items:center">'
            '<span class="lab" style="color:#99FF80">Voyage complete</span><span style="font-size:56px">The Ark reached The Maw\'s core</span>'
            '<span style="font-size:17px;color:#747BAD">Escort the Ark  ·  difficulty 2  ·  5 of 5 worlds  ·  9:14</span></div>')
    foot = ('<div class="abs" style="left:120px;top:820px;display:flex;align-items:center;gap:24px"><button class="btn soft" type="button" style="width:360px;height:64px"><span class="kc">B</span>Finish</button>'
            '<span style="font-size:16px;color:#747BAD">Bank the rewards and return to Voyages</span></div>'
            '<div class="abs" style="left:1000px;top:820px;width:800px;display:flex;align-items:center;justify-content:flex-end;gap:24px">'
            '<span style="font-size:16px;color:#747BAD">The same escort, difficulty 3</span>'
            '<button class="btn soft-cta" type="button" style="width:380px;height:64px"><span class="kc">A</span>Venture further</button></div>')
    dim = '<div class="abs" style="inset:0;background:rgba(0,1,10,.62)"></div>'
    return page(f"Rev {REV} Score - The end: the Ark's hull, a row per seat, rewards, Finish and Venture further",
                flight_ground("rgba(117,143,251,.12)") + f'<img src="{ARK}" alt="" style="position:absolute;left:660px;top:420px;width:600px;height:280px;object-fit:contain;opacity:.5">'
                + dim + head + left + right + foot)


# ── Notes ───────────────────────────────────────────────────────────────────────────────────────────────────────────
NOTES = [
    ("Lobby (recommended)", "The lobby is the only page: you, then friends who join, then AI wingmates. No Squad 1 / Squad 2 tabs. "
     "A saved squad is one line (Y) that fills the open AI seats and never your seat or a friend's. \"Flies this voyage\" on your card and "
     "\"You sail as Manta · Space\" by Set sail are read from the record the voyage spawns from, so the bug where a pick did not apply shows here."),
    ("Picker", "One press on a vessel saves it to your seat for this voyage (not to a squad preset), and the line under the grid says so. "
     "Vessels that suit an escort carry \"For this voyage\"; every vessel stays pickable."),
    ("Crew", "The six stacked recommendation cards over the map become one button, \"Crew recommends · 8\", that opens this popup. "
     "Escort the Ark and Crystal Run are open (Crystal Run is offered whoever is seated, so it sits above the factions). "
     "Every other crew voyage shows its pilot, job and difficulty under its faction, locked for Milestone 1. Factions shown are examples."),
    ("Hud", "The objective box holds what is true now: the goal in the voyage's own words, the five worlds as stops, the hull with the "
     "breach line for the chosen difficulty, the Ark's state and time to the next world, and one action line. Fauna on the Ark carry a red "
     "marker, and an edge arrow when off screen."),
    ("Run", "The crystal counter leads (12 / 20), counted from the start of the leg, with the split by element under it (any element counts). "
     "Each cell on the route is marked calm or hostile before you reach it, so the player chooses: farm the hostile cell or stay with the Ark."),
    ("Toasts (Compare)", "A few seconds each, top centre, one at a time; colour by kind (an event, a threat, a goal). Wording follows the game plan."),
    ("Score", "The Ark's hull at the core against the breach line, a row per seat, then rewards: the escort pays crystals scaled by difficulty. "
     "Finish (B) banks and returns to Voyages; Venture further (A) runs the same escort one difficulty up. Crystal Run uses the same board "
     "with crystals per element per seat; a lost Ark shows it as failed with Try again."),
]


def notes_board():
    items = "".join(f'<div style="display:flex;flex-direction:column;gap:8px"><span class="lab" style="color:#E6E9FF">{h}</span>'
                    f'<p style="margin:0;font-size:16px;line-height:1.55;color:#B8C0F0">{t}</p></div>' for h, t in NOTES)
    body = ('<div style="position:absolute;inset:0;background:#00041F"></div>'
            '<div style="position:absolute;left:60px;right:60px;top:50px;display:flex;flex-direction:column;gap:12px">'
            f'<span style="font-size:30px">Rev {REV} notes: Voyages Milestone 1</span>'
            '<span style="font-size:15px;color:#747BAD">Escort the Ark and Crystal Run made good end to end. Numbers from the Milestone 1 game plan. Awaiting a pick per board.</span></div>'
            f'<div style="position:absolute;left:60px;right:60px;top:170px;display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:30px 60px">{items}</div>')
    html = page(f"Rev {REV} - notes: what each board fixes from Shombith's Milestone 1 list", body)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


BOARDS = {f"R{REV}Compare.dc.html": compare_board, f"R{REV}Lobby.dc.html": lobby_board, f"R{REV}Picker.dc.html": picker_board,
          f"R{REV}Crew.dc.html": crew_board, f"R{REV}Hud.dc.html": hud_board, f"R{REV}Run.dc.html": run_board,
          f"R{REV}Score.dc.html": score_board, f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
