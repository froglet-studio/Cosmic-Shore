#!/usr/bin/env python3
"""Rev 114 boards: the Voyages playtest round (Frogma tab "Voyages playtest", Docs/MENU_REDESIGN_B.md).

Shombith played the Milestone 1 build on 2026-11-16: "The crew screen needs some rework, suggest me some mockups in frogma
for that. The in game still has no intro, I could not see there should be a kick ass intro. The objective on the left was
too big show me 4 mockups of that on a new frogma page. There were no toasts in the game but text showed up at the top of
the ark. Still in the game I did not get a good feel. Create 4 mockups of each thing".

Four options for each of five things, lettered A-D, on a tab of their own:
  C1-C4    the crew recommends screen       IAB, ICD  the voyage intro, as storyboards (two to a tall board)
  O1-O4    a smaller objective box          TAB, TCD  the toasts (two to a tall board)
  F1-F4    the feel in flight (camera, framing, what the world does)
The notes are a sticky on the tab (NOTES below), not a board: the canvas holds at most 512 files, so the intro and
toast options share boards and the superseded Voyages rounds (Rev 100, 103, 108, 111) were taken off the canvas.

The in-flight boards are drawn over real frames of the game (Docs/Pilots/art/refs/sea_*.jpg and
Docs/MenuRedesignB/build_refs/sea_cell_outside.jpg, uploaded to the canvas as assets), never over an empty backdrop.

    python3 Docs/MenuRedesignB/rounds/r114_voyages.py <scratch root> [rev]
"""
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "114"
_spec = importlib.util.spec_from_file_location("r113", HERE / "r113_voyages.py")
r113 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev]
_spec.loader.exec_module(r113)
r100, r108 = r113.r100, r113.r108

REV = int(_rev)
ROOT = r113.ROOT
blob = r100.blob
HULL, FLOWER, LOCK, FACE, ARK = r113.HULL, r113.FLOWER, r113.LOCK, r113.FACE, r113.ARK
flower, pips, menu = r113.flower, r113.pips, r113.menu

# Real frames of the game (see the docstring for their sources).
G = {"cell": blob("0a182592352006ab83b412f19abf1294"), "trail": blob("10eb1eb07d7b050e4a362444c9f4de65"),
     "crystal": blob("228990ef977b2379d38a9fc03c210729"), "heart": blob("bc6606a29087fada268cd6bfcc0a3650"),
     "approach": blob("c3de9bac4b1983ff5288fabe0cdc776e"), "through": blob("4fe7dfb295b8f70cf9cc710afcab5994")}
MAPS = [blob("b96bc9cef23de987e4ed42294befc28a"), blob("037d66eeef857313705b96448c91f4a0"),
        blob("da47aa2e4c65d63e53711ebfe6fc4ac2"), blob("9778fd69fcae1f8cb003a9bbd8508a56")]
SECTOR = r100.TIERS[2][2]

r100.HELMET = r100.HELMET.replace("</style>", (
    ".frame{position:absolute;overflow:hidden;background:#00010A}\n"
    ".frame>img.bg{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}\n"
    ".cap2{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.14em;text-transform:uppercase;color:#747BAD}\n"
    ".note{font-size:16px;line-height:1.55;color:#B8C0F0}\n"
    ".mini{background:rgba(0,4,31,.82);box-shadow:inset 2px 0 0 #E6E9FF}\n"
    ".row{display:flex;align-items:center}\n"
    "</style>"))

LOGIC = r113.LOGIC


_CAPTURE = []


def page(title, body, h=1080):
    if _CAPTURE:
        _CAPTURE[-1].append(body)
        return ""
    html = r100.page(title, body, LOGIC)
    return html if h == 1080 else html.replace("height: 1080px", f"height: {h}px").replace('"height":1080', f'"height":{h}')


# ── shared pieces ───────────────────────────────────────────────────────────────────────────────────────────────────
def ground(bg, pos="50% 50%", dim=0.25):
    return (f'<img src="{bg}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{pos}">'
            f'<div class="abs" style="inset:0;background:rgba(0,1,10,{dim})"></div>')


def frame(x, y, w, h, bg, inner="", pos="50% 50%", dim=0.2, line=False):
    edge = "box-shadow:inset 0 0 0 1px #00D4FF;" if line else "box-shadow:inset 0 0 0 1px rgba(67,76,137,.6);"
    return (f'<div class="frame" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px">'
            f'<img class="bg" src="{bg}" alt="" style="object-position:{pos}">'
            f'<div class="abs" style="inset:0;background:rgba(0,1,10,{dim})"></div>{inner}'
            f'<div class="abs" style="inset:0;{edge}pointer-events:none"></div></div>')


def ark(x, y, w, rot=0, op=1):
    return (f'<img src="{ARK}" alt="The Ark" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{w * 0.47:.0f}px;'
            f'object-fit:contain;transform:rotate({rot}deg);opacity:{op}">')


def hull(v, x, y, w, rot=0):
    return (f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:{x}px;top:{y}px;width:{w}px;height:{w * 0.45:.0f}px;'
            f'object-fit:contain;transform:rotate({rot}deg)">')


def fauna(x, y, s=1.0):
    """A hunting fauna, lit red in the world (a lit body and the light it casts), never a ring on the screen."""
    w = 46 * s
    return (f'<span aria-hidden="true" class="abs" style="left:{x - w}px;top:{y - w}px;width:{w * 2:.0f}px;height:{w * 2:.0f}px;'
            'border-radius:50%;background:radial-gradient(circle,rgba(255,75,58,.42),rgba(255,75,58,0) 65%)"></span>'
            f'<svg class="abs" style="left:{x - 22 * s:.0f}px;top:{y - 12 * s:.0f}px" width="{44 * s:.0f}" height="{24 * s:.0f}" viewBox="0 0 44 24" aria-hidden="true">'
            '<path d="M0 12L16 2l6 6 6-6 16 10-16 10-6-6-6 6z" fill="#FF4B3A" stroke="#FF8A7C" stroke-width="1.5"></path></svg>')


def gem(x, y, s=1.0):
    return (f'<svg class="abs" style="left:{x}px;top:{y}px" width="{26 * s:.0f}" height="{36 * s:.0f}" viewBox="0 0 26 36" '
            'fill="rgba(230,233,255,.2)" stroke="#E6E9FF" stroke-width="2" aria-hidden="true"><path d="M13 1l12 17-12 17L1 18z"></path></svg>')


def title_tag(word, x=40, y=40):
    return f'<span class="abs cap2" style="left:{x}px;top:{y}px;color:#E6E9FF;z-index:9">{word}</span>'


def side(x, y, w, heading, items):
    """An annotation column: a heading, then (label, text) pairs."""
    rows = "".join(f'<div style="display:flex;flex-direction:column;gap:6px"><span class="cap2" style="color:#E6E9FF">{a}</span>'
                   f'<span class="note">{b}</span></div>' for a, b in items)
    return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:22px">'
            f'<span style="font-size:26px;line-height:1.3">{heading}</span>{rows}</div>')


def hullbar(pct, breach, w=None, h=8):
    ww = f"width:{w}px;" if w else ""
    return (f'<div class="bar" style="{ww}height:{h}px"><span style="width:{pct}%;background:#E6E9FF"></span>'
            f'<span class="breach" style="left:{breach}%"></span></div>')


def dots(n, now, w=150, end_lime=True):
    out = [f'<span style="position:relative;display:inline-block;width:{w}px;height:12px">'
           f'<span style="position:absolute;left:4px;right:4px;top:5px;height:2px;background:rgba(67,76,137,.8)"></span>'
           f'<span style="position:absolute;left:4px;width:{now / (n - 1) * (w - 8):.0f}px;top:5px;height:2px;background:#E6E9FF"></span>']
    for i in range(n):
        x = i / (n - 1) * (w - 12)
        st = ("background:#E6E9FF" if i <= now else
              "background:#00041F;box-shadow:inset 0 0 0 2px #99FF80" if (i == n - 1 and end_lime) else
              "background:#00041F;box-shadow:inset 0 0 0 2px #747BAD")
        out.append(f'<span style="position:absolute;left:{x:.0f}px;top:0;width:12px;height:12px;border-radius:50%;{st}"></span>')
    return "".join(out) + "</span>"


VESSEL_HUD = ('<div class="abs" style="right:40px;bottom:40px;width:380px;height:130px;box-shadow:inset 0 0 0 1px rgba(67,76,137,.5);display:flex;'
              'align-items:center;justify-content:center"><span class="cap2" style="color:#5C5F70">Vessel HUD, as today</span></div>')


def size_tag(x, y, words):
    return f'<span class="abs cap2" style="left:{x}px;top:{y}px;color:#00D4FF">{words}</span>'


# ── C: the crew recommends screen ───────────────────────────────────────────────────────────────────────────────────
JOBS = [  # (faction, pilot, job, difficulty, place, map)
    ("Hunters", "Ilia Silvergate", "Patrol Saltmarch", 3, "Saltmarch", 0),
    ("Hunters", "Mathalo Kelpbloom", "Patrol Verdigris", 3, "Verdigris", 1),
    ("Pirates", "Ekora Hollowbrook", "Raid the hoard at Magpie Hollow", 5, "Magpie Hollow", 2),
    ("Merchants", "Quivera Pearlspark", "Carry a sealed dispatch to Aurum Harbour", 2, "Aurum Harbour", 3),
    ("Merchants", "Karenth Amberweaver", "Carry sealed cargo to Tollmoor", 4, "Tollmoor", 0),
    ("Thieves", "Tamako Deephook", "Open the vaults at Lantern Reach", 4, "Lantern Reach", 1)]
FACTIONS = [("All", 8), ("Open now", 2), ("Hunters", 2), ("Pirates", 1), ("Merchants", 2), ("Thieves", 1)]
CRYSTALS = r113.CRYSTALS


def face(p, w=56, h=66):
    return f'<img src="{FACE[p]}" alt="" style="width:{w}px;height:{h}px;object-fit:cover;object-position:50% 28%;border-radius:4px;flex:none">'


def lock_line(words="Opens after Milestone 1"):
    return f'<span class="row" style="gap:8px;font-size:14px;color:#FFAE00;white-space:nowrap">{LOCK}{words}</span>'


def open_btn(w=180, label="Open"):
    return f'<button class="btn sm soft-cta" type="button" style="width:{w}px;flex:none"><span class="kc">A</span>{label}</button>'


def close_x(x, y):
    return (f'<button class="pick abs" type="button" aria-label="Close" style="left:{x}px;top:{y}px;width:44px;height:44px;display:flex;align-items:center;'
            'justify-content:center;color:#E6E9FF;box-shadow:inset 0 0 0 1px #434C89">'
            '<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M2 2l12 12M14 2L2 14"></path></svg></button>')


def scrim(inner):
    return ('<div class="abs" style="left:0;top:84px;width:1920px;height:996px;z-index:8;background:rgba(0,1,10,.72)">' + inner + '</div>')


def crew_a():  # drawer over the HyperSea, each job pinned on the map
    pins = [(380, 360, 1, True), (620, 520, 2, True), (300, 700, 3, False), (760, 300, 4, False), (900, 640, 5, False),
            (560, 760, 6, False), (1040, 460, 7, False), (180, 520, 8, False)]
    pin_html = "".join(
        f'<div class="abs" style="left:{x - 18}px;top:{y - 18}px;width:36px;height:36px;border-radius:50%;display:flex;align-items:center;justify-content:center;'
        f'font-size:15px;{"background:#00041F;box-shadow:inset 0 0 0 2px #00D4FF;color:#E6E9FF" if i == 1 else ("background:#00041F;box-shadow:inset 0 0 0 2px #E6E9FF" if live else "background:rgba(0,4,31,.8);box-shadow:inset 0 0 0 1px #747BAD;color:#747BAD")}">{i}</div>'
        for x, y, i, live in pins)
    mapv = (f'<div class="frame" style="left:40px;top:180px;width:1120px;height:860px">'
            f'<img class="bg" src="{SECTOR}" alt="The sector">'
            '<div class="abs" style="inset:0;background:rgba(0,1,10,.35)"></div>'
            f'{pin_html}<span class="abs cap2" style="left:24px;bottom:20px">The sector around your Ark. Each number is a job on the list.</span></div>')

    def row(i, img, kind, title, meta, live, on=False):
        edge = "box-shadow:inset 2px 0 0 #00D4FF;background:rgba(0,212,255,.06)" if on else "box-shadow:inset 0 -1px 0 rgba(67,76,137,.5)"
        tail = open_btn(150) if live else lock_line("Locked")
        return (f'<div class="row" style="gap:16px;height:92px;padding:0 18px;{edge};{"" if live else "opacity:.7"}">'
                f'<span class="num" style="width:22px;color:#747BAD">{i}</span>{img}'
                f'<span style="display:flex;flex-direction:column;gap:6px;min-width:0;flex:1"><span class="cap2">{kind}</span>'
                f'<span class="ell" style="font-size:18px">{title}</span><span class="row" style="gap:10px;font-size:13px;color:#747BAD">{meta}</span></span>{tail}</div>')
    ark_img = f'<img src="{ARK}" alt="" style="width:80px;height:44px;object-fit:contain;flex:none">'
    rows = (row(1, ark_img, "Ark escort", "Escort the Ark to The Maw", pips(2, 80) + '<span class="num">2</span><span>5 worlds</span>', True, True)
            + row(2, '<span style="width:80px;display:flex;justify-content:center;flex:none;transform:scale(.6)">' + CRYSTALS + '</span>', "Any pilot", "Crystal Run before Tollmoor",
                  pips(3, 80) + '<span class="num">3</span><span>4 cells, 2 hostile</span>', True))
    groups = ""
    n = 3
    for fac in ("Hunters", "Pirates", "Merchants", "Thieves"):
        js = [j for j in JOBS if j[0] == fac]
        groups += f'<div class="cap2" style="padding:16px 18px 6px">{fac}  ·  {len(js)}</div>'
        for j in js:
            groups += row(n, face(j[1], 44, 52), j[1], j[2], pips(j[3], 80) + f'<span class="num">{j[3]}</span><span>{j[4]}</span>', False)
            n += 1
    drawer = ('<div class="glass" style="position:absolute;left:1200px;top:180px;width:680px;height:860px;overflow:hidden">'
              '<div style="position:absolute;left:28px;right:28px;top:24px;display:flex;justify-content:space-between;align-items:flex-start">'
              '<span style="display:flex;flex-direction:column;gap:8px"><span style="font-size:26px">Your crew recommends</span>'
              '<span style="font-size:14px;color:#747BAD">Two are open now. The rest open after Milestone 1.</span></span></div>'
              '<div style="position:absolute;left:28px;right:28px;top:110px;display:flex;gap:8px;flex-wrap:wrap">'
              + "".join(f'<button class="btn sm soft chipq" type="button" style="height:36px;padding:0 14px;font-size:13px;{"box-shadow:inset 0 0 0 1px #00D4FF" if f == "All" else ""}">{f} {c}</button>' for f, c in FACTIONS)
              + '</div><div style="position:absolute;left:10px;right:10px;top:214px;bottom:0;overflow:hidden">'
              '<div class="cap2" style="padding:10px 18px 6px;color:#E6E9FF">Open now</div>' + rows + groups + '</div></div>')
    body = mapv + drawer + close_x(1824, 196)
    return page(f"Rev {REV} C1 - Crew A: a drawer over the HyperSea; each job pinned on the map, the list numbered to match (recommended)", menu(1, body))


def crew_b():  # pilot first
    crew = [("Veliane Crestward", "Wardens", "Escort the Ark", True), ("Tamako Deephook", "Thieves", "Lantern Reach vaults", False),
            ("Ilia Silvergate", "Hunters", "Patrol Saltmarch", False), ("Karenth Amberweaver", "Merchants", "Cargo to Tollmoor", False),
            ("Ekora Hollowbrook", "Pirates", "Magpie Hollow hoard", False), ("Quivera Pearlspark", "Merchants", "Dispatch to Aurum", False)]
    cards = "".join(
        f'<div style="position:relative;display:flex;flex-direction:column;gap:10px;{"" if i in (0, 2) else "opacity:.62"}">'
        f'<div style="position:relative;width:100%;height:250px;{"box-shadow:inset 0 0 0 2px #00D4FF" if i == 0 else ""}">'
        f'<img src="{FACE[p]}" alt="" style="position:absolute;inset:{3 if i == 0 else 0}px;width:calc(100% - {6 if i == 0 else 0}px);height:calc(100% - {6 if i == 0 else 0}px);object-fit:cover;object-position:50% 25%">'
        f'{"" if live or i == 2 else f"<span class=abs style=right:10px;top:10px;color:#FFAE00>{LOCK}</span>"}</div>'
        f'<span class="cap2">{fac}</span><span style="font-size:17px">{p.split()[0]}</span>'
        f'<span class="ell" style="font-size:14px;color:#747BAD">{job}</span></div>'
        for i, (p, fac, job, live) in enumerate(crew))
    strip = f'<div style="position:absolute;left:40px;right:40px;top:120px;display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:22px">{cards}</div>'
    detail = ('<div style="position:absolute;left:40px;right:40px;top:560px;height:330px;display:grid;grid-template-columns:620px 1fr;gap:40px">'
              f'<div class="frame" style="position:relative;width:620px;height:330px"><img class="bg" src="{G["cell"]}" alt="">'
              f'{ark(150, 90, 320)}</div>'
              '<div style="display:flex;flex-direction:column;gap:16px">'
              '<span class="cap2" style="color:#E6E9FF">Veliane Crestward, Wardens of the Calm, asks</span>'
              '<span style="font-size:34px;line-height:1.2">"Get the Ark to The Maw. Keep its hull whole; we have nothing behind it."</span>'
              f'<span class="row" style="gap:14px;font-size:15px;color:#747BAD">{pips(2, 110)}<span class="num">2</span><span>5 worlds</span><span>·</span><span>Rewards: crystals, XP, Veliane\'s trust</span></span>'
              f'<div class="row" style="gap:18px;margin-top:auto">{open_btn(260, "Take the job")}'
              '<button class="btn sm soft" type="button" style="width:220px"><span class="kc">Y</span>Crystal Run instead</button></div></div></div>')
    pop = ('<div role="dialog" aria-label="Your crew recommends" class="glass modal" style="position:absolute;left:120px;top:40px;width:1680px;height:920px">'
           '<div style="position:absolute;left:40px;top:34px;display:flex;flex-direction:column;gap:8px"><span style="font-size:28px">Your crew, and the work they bring</span>'
           '<span style="font-size:15px;color:#747BAD">Pick a pilot to hear their job. Veliane\'s and the Crystal Run are open; the others open after Milestone 1.</span></div>'
           + close_x(1596, 34) + strip + detail + '</div>')
    return page(f"Rev {REV} C2 - Crew B: pilot first; your crew's faces across the top, the picked pilot's job told in their words", menu(1, scrim(pop)))


def crew_c():  # the job board, poster columns
    def poster(img, title, who, d, live, on=False):
        edge = "box-shadow:inset 0 0 0 1px #00D4FF" if on else "box-shadow:inset 0 0 0 1px rgba(67,76,137,.6)"
        return (f'<div style="position:relative;height:300px;background:rgba(0,4,31,.8);{edge};{"" if live else "opacity:.6"}">'
                f'<div class="frame" style="left:1px;top:1px;width:calc(100% - 2px);height:150px">{img}</div>'
                f'<div style="position:absolute;left:16px;right:16px;top:166px;display:flex;flex-direction:column;gap:8px">'
                f'<span style="font-size:17px;line-height:1.3">{title}</span><span class="row" style="gap:10px;font-size:13px;color:#747BAD">{who}</span>'
                f'<span class="row" style="gap:10px;font-size:13px;color:#747BAD">{pips(d, 80)}<span class="num">{d}</span></span></div>'
                f'<div style="position:absolute;left:16px;right:16px;bottom:14px">{open_btn(170) if live else lock_line("Locked")}</div></div>')
    col_open = ('<div style="display:flex;flex-direction:column;gap:16px"><span class="cap2" style="color:#E6E9FF">Open now  ·  2</span>'
                + poster(f'<img class="bg" src="{G["cell"]}" alt="">' + ark(40, 30, 200), "Escort the Ark to The Maw", "Ark escort", 2, True, True)
                + poster(f'<img class="bg" src="{G["crystal"]}" alt="">', "Crystal Run before Tollmoor", "Any pilot", 3, True) + '</div>')
    cols = col_open
    for fac in ("Hunters", "Pirates", "Merchants", "Thieves"):
        js = [j for j in JOBS if j[0] == fac]
        cols += (f'<div style="display:flex;flex-direction:column;gap:16px"><span class="cap2">{fac}  ·  {len(js)}</span>'
                 + "".join(poster(f'<img class="bg" src="{MAPS[j[5]]}" alt="">', j[2], face(j[1], 22, 26) + j[1], j[3], False) for j in js) + '</div>')
    pop = ('<div role="dialog" aria-label="Job board" class="glass modal" style="position:absolute;left:60px;top:30px;width:1800px;height:940px">'
           '<div style="position:absolute;left:40px;top:30px;display:flex;flex-direction:column;gap:8px"><span style="font-size:28px">Job board</span>'
           '<span style="font-size:15px;color:#747BAD">What your crew has heard of, by who is paying. One column per faction.</span></div>'
           + close_x(1716, 30) +
           f'<div style="position:absolute;left:40px;right:40px;top:130px;display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:26px">{cols}</div></div>')
    return page(f"Rev {REV} C3 - Crew C: the job board; one poster per voyage in faction columns, the two open ones first", menu(1, scrim(pop)))


def crew_d():  # faction tabs, hero row
    tabs = "".join(f'<button class="pick" type="button" style="height:56px;padding:0 22px;font-size:15px;{"box-shadow:inset 0 -2px 0 #00D4FF;color:#E6E9FF" if f == "Hunters" else "color:#747BAD"}">'
                   f'<span class="cap2" style="color:inherit">{f}</span> <span class="num">{c}</span></button>' for f, c in FACTIONS if f != "Open now")

    def hero(bg, kind, title, meta, extra=""):
        return (f'<div class="frame" style="position:relative;height:300px;box-shadow:inset 0 0 0 1px rgba(67,76,137,.6)"><img class="bg" src="{bg}" alt="">'
                '<div class="abs" style="inset:0;background:linear-gradient(90deg,rgba(0,4,31,.92) 0%,rgba(0,4,31,.6) 50%,rgba(0,4,31,0) 100%)"></div>'
                f'{extra}<div class="abs" style="left:32px;top:30px;width:460px;display:flex;flex-direction:column;gap:12px"><span class="cap2">{kind}</span>'
                f'<span style="font-size:30px;line-height:1.2">{title}</span><span class="row" style="gap:12px;font-size:14px;color:#747BAD">{meta}</span></div>'
                f'<div class="abs" style="left:32px;bottom:28px">{open_btn(240)}</div></div>')
    heroes = ('<div style="position:absolute;left:40px;right:40px;top:200px;display:grid;grid-template-columns:1fr 1fr;gap:26px">'
              + hero(G["cell"], "Open now  ·  Ark escort", "Escort the Ark to The Maw", pips(2, 100) + '<span class="num">2</span><span>5 worlds</span>', ark(470, 70, 300))
              + hero(G["crystal"], "Open now  ·  any pilot", "Crystal Run before Tollmoor", pips(3, 100) + '<span class="num">3</span><span>4 cells, 2 hostile</span>') + '</div>')

    def tile(j):
        return (f'<div class="row" style="gap:16px;height:120px;padding:0 20px;background:rgba(0,4,31,.7);box-shadow:inset 0 0 0 1px rgba(67,76,137,.6);opacity:.75">'
                f'{face(j[1], 64, 76)}<span style="display:flex;flex-direction:column;gap:6px;min-width:0;flex:1"><span class="cap2">{j[1]}</span>'
                f'<span class="ell" style="font-size:18px">{j[2]}</span><span class="row" style="gap:10px;font-size:13px;color:#747BAD">{pips(j[3], 80)}<span class="num">{j[3]}</span><span>{j[4]}</span></span></span>'
                f'<span style="color:#FFAE00">{LOCK}</span></div>')
    hunters = [j for j in JOBS if j[0] == "Hunters"] + [("Hunters", "Wyvendo Waveline", "Clear the nest at Ember Shoal", 6, "Ember Shoal", 2)]
    grid = (f'<div style="position:absolute;left:40px;right:40px;top:560px;display:flex;justify-content:space-between;align-items:center">'
            f'<span class="cap2">Hunters  ·  what they would pay for</span>{lock_line("Locked for Milestone 1")}</div>'
            f'<div style="position:absolute;left:40px;right:40px;top:600px;display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px">{"".join(tile(j) for j in hunters)}</div>')
    pop = ('<div role="dialog" aria-label="Your crew recommends" class="glass modal" style="position:absolute;left:100px;top:40px;width:1720px;height:900px">'
           '<div style="position:absolute;left:40px;top:30px;font-size:28px">Your crew recommends</div>' + close_x(1636, 30)
           + f'<nav aria-label="Factions" class="row" style="position:absolute;left:40px;right:40px;top:100px;gap:8px;box-shadow:inset 0 -1px 0 rgba(67,76,137,.6)">{tabs}</nav>'
           + heroes + grid + '</div>')
    return page(f"Rev {REV} C4 - Crew D: faction tabs across the top; the two open voyages as a hero row that stays above every tab", menu(1, scrim(pop)))


# ── I: the intro, as storyboards ────────────────────────────────────────────────────────────────────────────────────
def board6(title, sub, shots):
    """Six shots, three by two, each a 16:9 frame and its caption (time, camera, words on screen)."""
    W, H, GX, GY = 580, 326, 40, 110
    out = [f'<div class="abs" style="left:40px;top:34px;display:flex;flex-direction:column;gap:8px"><span style="font-size:28px">{title}</span>'
           f'<span style="font-size:15px;color:#747BAD">{sub}</span></div>']
    for i, (bg, pos, inner, t, cam, words) in enumerate(shots):
        x, y = 40 + (i % 3) * (W + GX), 130 + (i // 3) * (H + GY + 96)
        out.append(frame(x, y, W, H, bg, '<div class="abs" style="left:0;right:0;top:0;height:30px;background:#00010A"></div>'
                         '<div class="abs" style="left:0;right:0;bottom:0;height:30px;background:#00010A"></div>' + inner, pos, 0.18))
        out.append(f'<div class="abs" style="left:{x}px;top:{y + H + 14}px;width:{W}px;display:flex;flex-direction:column;gap:6px">'
                   f'<span class="row" style="gap:14px"><span class="num" style="font-size:15px;color:#00D4FF">{i + 1}</span><span class="cap2" style="color:#E6E9FF">{t}</span></span>'
                   f'<span style="font-size:15px;line-height:1.45;color:#B8C0F0">{cam}</span>'
                   f'<span style="font-size:14px;color:#747BAD">{words}</span></div>')
    return "".join(out)


def big_title(word, sub="", y=120, size=40):
    return (f'<div class="abs" style="left:0;right:0;top:{y}px;display:flex;flex-direction:column;align-items:center;gap:8px">'
            f'<span style="font-family:\'Chakra Petch\',sans-serif;font-weight:700;font-size:{size}px;letter-spacing:.2em">{word}</span>'
            f'<span style="font-size:13px;color:#B8C0F0">{sub}</span></div>')


def namecard(x, y, v, p, el):
    return (f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;align-items:center;gap:10px;padding:8px 12px;background:rgba(0,4,31,.82);box-shadow:inset 2px 0 0 #E6E9FF">'
            f'{face(p, 30, 36)}<span style="display:flex;flex-direction:column;gap:3px"><span style="font-size:13px">{v}</span>'
            f'<span style="font-size:11px;color:#B8C0F0">{p.split()[0]}  ·  {el}</span></span>{flower(el, 18)}</div>')


def skip_hint():
    return '<span class="abs cap2" style="right:16px;bottom:40px;font-size:10px;color:#747BAD">Hold B to skip</span>'


def intro_a():
    shots = [
        (G["cell"], "50% 50%", ark(170, 120, 240), "0:00 - 0:02", "Wide on the home cell. The Ark eases out of the membrane; low music, one horn.", "No words. Letterbox bars on."),
        (G["through"], "30% 50%", ark(-60, 40, 700), "0:02 - 0:04", "Slow push along the Ark's hull, engines lighting one by one.", "THE ARK  ·  hull 100%"),
        (G["approach"], "40% 50%", namecard(60, 230, "Manta", "Tamako Deephook", "Space"), "0:04 - 0:05", "Your vessel drops in beside the Ark. Hard cut on the beat.", "Your name card: vessel, pilot, element"),
        (G["trail"], "60% 50%", hull("Dolphin", 160, 120, 120, -4) + hull("Sparrow", 330, 170, 110, 6) + namecard(30, 236, "Dolphin", "Ilia Silvergate", "Charge") + namecard(310, 236, "Sparrow", "Mathalo Kelpbloom", "Time"), "0:05 - 0:06", "Wingmates take their places either side, each with a card.", "One card a seat, half a second each"),
        (G["cell"], "50% 50%", big_title("ESCORT THE ARK", "Keep its hull above 25% to The Maw's core", 110, 34) + '<div class="abs" style="left:150px;top:210px">' + dots(5, 0, 280) + '</div>', "0:06 - 0:08", "Title over the sea; the five worlds draw in as a line, The Maw last.", "The goal line is the objective's own words"),
        (G["approach"], "50% 50%", '<span class="abs" style="left:0;right:0;top:120px;text-align:center;font-family:\'Chakra Petch\',sans-serif;font-weight:700;font-size:48px;letter-spacing:.2em">GO</span>' + skip_hint(), "0:08 - 0:09", "The camera swings in behind you; bars slide away; control is yours.", "GO, then the HUD fades up"),
    ]
    body = ground(G["cell"], dim=.94) + board6("Intro A: Departure", "Nine seconds. The Ark, your squad by name, the goal, the route, go. Skippable after the first run.", shots)
    return page(f"Rev {REV} I1 - Intro A: Departure; the Ark undocks, your squad forms up by name, the goal and route, go (recommended)", body)


def intro_b():
    shots = [
        (SECTOR, "50% 50%", '<span class="abs cap2" style="left:20px;top:44px;color:#E6E9FF">HyperSea  ·  sector</span>', "0:00 - 0:02", "Start on the HyperSea table you just picked from; the route glows on it.", "Same table as the Voyages page"),
        (r100.TIERS[1][2], "50% 50%", "", "0:02 - 0:03", "Dive: sector to neighbourhood, one tier a beat.", "Tier names flick past"),
        (G["cell"], "50% 50%", ark(220, 150, 140), "0:03 - 0:05", "Through the cell wall; the Ark small in the middle of it.", "Escort the Ark  ·  5 worlds"),
        (G["through"], "40% 50%", ark(40, 60, 480), "0:05 - 0:06", "Down to the Ark; the camera settles over its back.", ""),
        (G["approach"], "50% 50%", '<div class="mini abs" style="left:30px;top:50px;padding:12px 16px;width:300px"><span class="cap2" style="color:#E6E9FF">Escort the Ark</span><div style="font-size:14px;margin-top:6px">Keep its hull above 25% to The Maw\'s core</div></div>', "0:06 - 0:07", "The objective writes itself into the corner where it lives during play.", "The box lands where it stays"),
        (G["approach"], "50% 50%", skip_hint(), "0:07 - 0:08", "Into your seat. Go.", "GO"),
    ]
    body = ground(G["cell"], dim=.94) + board6("Intro B: From the map", "Eight seconds. Ties the voyage to the page you chose it on. Calm, no fauna.", shots)
    return page(f"Rev {REV} I2 - Intro B: From the map; the HyperSea table dives down to the Ark and hands you the seat", body)


def intro_c():
    maw = '<span class="abs" style="left:190px;top:80px;width:200px;height:200px;border-radius:50%;background:radial-gradient(circle,rgba(166,0,255,.5),rgba(0,1,10,0) 70%)"></span>'
    shots = [
        (G["heart"], "50% 50%", maw + '<span class="abs cap2" style="left:20px;bottom:44px;color:#E6E9FF">THE MAW</span>', "0:00 - 0:02", "Cold open on The Maw's core: dark, a slow heartbeat in the sound.", "THE MAW, small, bottom left"),
        (G["crystal"], "50% 50%", fauna(200, 160, 1.6) + fauna(330, 210, 1.2) + fauna(420, 130, 1), "0:02 - 0:03", "Its fauna turn toward the camera, lit red.", "No words"),
        (G["cell"], "50% 50%", ark(170, 110, 240), "0:03 - 0:04", "Hard cut to the Ark, far away and quiet.", "THE ARK  ·  hull 100%"),
        (G["trail"], "50% 50%", '<span class="abs" style="left:40px;bottom:40px;display:flex;gap:10px;align-items:center;padding:8px 12px;background:rgba(0,4,31,.82)">' + face("Veliane Crestward", 34, 40) + '<span style="font-size:13px">"They will come for the hull. Do not let them."</span></span>', "0:04 - 0:06", "Veliane, on the radio, over your squad flying in.", "One line from the pilot who gave the job"),
        (G["approach"], "50% 50%", big_title("ESCORT THE ARK", "", 120, 38), "0:06 - 0:07", "Title slam on a hit of the drums.", "ESCORT THE ARK"),
        (G["approach"], "50% 50%", skip_hint(), "0:07 - 0:08", "Camera behind you. Go.", "GO"),
    ]
    body = ground(G["heart"], dim=.94) + board6("Intro C: Threat first", "Eight seconds. Shows what you are up against before what you are protecting.", shots)
    return page(f"Rev {REV} I3 - Intro C: Threat first; The Maw and its fauna, then the Ark, a pilot's line, the title", body)


def intro_d():
    bay = '<div class="abs" style="left:0;top:30px;right:0;bottom:30px;background:linear-gradient(180deg,#1a1d33,#00010A 40%,#00010A 60%,#1a1d33)"></div>'
    shots = [
        (G["through"], "50% 50%", bay + '<span class="abs" style="left:240px;top:120px;width:100px;height:86px;background:radial-gradient(ellipse,rgba(230,233,255,.6),rgba(230,233,255,0) 70%)"></span>' + hull("Manta", 200, 200, 180), "0:00 - 0:01", "Inside the Ark's bay, behind your vessel; the door opens to the sea.", "One continuous camera, no cuts"),
        (G["approach"], "30% 50%", ark(260, 20, 420, 4), "0:01 - 0:02", "You shoot out; the Ark falls away behind.", ""),
        (G["trail"], "50% 50%", hull("Manta", 220, 140, 150) + hull("Dolphin", 80, 100, 100) + hull("Sparrow", 400, 110, 100), "0:02 - 0:03", "Wingmates launch either side; the camera pulls back to take in all three.", ""),
        (G["cell"], "50% 50%", ark(200, 160, 180) + '<div class="mini abs" style="left:30px;top:50px;padding:10px 14px"><span class="cap2" style="color:#E6E9FF">Escort the Ark</span></div>', "0:03 - 0:04", "Swing round to see the Ark following; the objective fades in.", "Objective box"),
        (G["cell"], "50% 50%", big_title("THE ARK SAILS", "5 worlds  ·  The Maw", 120, 30), "0:04 - 0:05", "One line over the sea.", ""),
        (G["approach"], "50% 50%", "", "0:05", "Control is yours without a cut.", "Never skippable: it is the launch"),
    ]
    body = ground(G["through"], dim=.94) + board6("Intro D: One take", "Five seconds. Fast enough to never skip; the launch is the intro.", shots)
    return page(f"Rev {REV} I4 - Intro D: One take; out of the Ark's bay into formation in five seconds, no cuts", body)


# ── O: a smaller objective box ──────────────────────────────────────────────────────────────────────────────────────
def flight(bg="approach", pos="50% 50%", dim=0.15):
    return ground(G[bg], pos, dim) + ark(700, 300, 520) + fauna(780, 360) + fauna(960, 470, .8)


def obj_board(key, title, desc, box, notes, extra=""):
    panel = '<div class="abs" style="left:1400px;top:0;width:520px;height:1080px;background:#00041F;box-shadow:inset 1px 0 0 rgba(67,76,137,.6)"></div>'
    body = flight() + box + extra + panel + side(1440, 60, 440, desc, notes)
    return page(f"Rev {REV} {key} - {title}", body)


def obj_a():
    box = ('<div class="mini abs" style="left:40px;top:40px;width:420px;padding:14px 18px;box-sizing:border-box">'
           '<div class="row" style="justify-content:space-between"><span class="cap2" style="color:#E6E9FF">Escort the Ark</span><span class="num" style="font-size:15px">78%</span></div>'
           f'<div style="margin-top:8px">{hullbar(78, 25)}</div>'
           f'<div class="row" style="justify-content:space-between;margin-top:10px;font-size:14px;color:#B8C0F0"><span>Scarab Reef  ·  hostile</span>{dots(5, 1, 110)}</div>'
           '<div style="margin-top:8px;font-size:15px;color:#FF8A7C">Break off 3 fauna on the Ark</div></div>')
    run = ('<div class="mini abs" style="left:40px;top:250px;width:420px;padding:14px 18px;box-sizing:border-box">'
           '<div class="row" style="justify-content:space-between"><span class="cap2" style="color:#E6E9FF">Crystal Run</span><span class="num" style="font-size:22px">12<span style="font-size:14px;color:#747BAD"> / 20</span></span></div>'
           f'<div style="margin-top:8px">{hullbar(70, 40)}</div>'
           f'<div class="row" style="gap:14px;margin-top:10px;font-size:13px;color:#B8C0F0">{flower("Space", 16)}5 {flower("Time", 16)}4 {flower("Charge", 16)}2 {flower("Mass", 16)}1<span style="margin-left:auto">Cell 2  ·  hostile</span></div></div>')
    return obj_board("O1", "Objective A: a strip, 420 px wide; the goal's name, the hull and breach line, the world and the job", "A: a strip",
                     box + run + size_tag(40, 220, "420 x 120  ·  was 600 x 330"),
                     [("What stays", "The hull bar and its breach line, the world you are in, five dots for the route, one job line."),
                      ("What goes", "The goal sentence (the intro and the first toast say it), the time to the next world, the route names."),
                      ("Crystal Run", "The count takes the hull's place on the top line; the four flowers sit under the bar.")])


def obj_b():
    rib = ('<div class="abs" style="left:610px;top:28px;width:700px;display:flex;flex-direction:column;align-items:center;gap:8px">'
           '<div class="row" style="gap:16px;width:100%">'
           '<span class="cap2" style="color:#E6E9FF">Garland</span>'
           '<div style="position:relative;flex:1;height:10px;background:rgba(67,76,137,.45)">'
           '<span style="position:absolute;left:0;top:0;bottom:0;width:28%;background:#E6E9FF"></span>'
           + "".join(f'<span style="position:absolute;left:{p}%;top:-5px;width:2px;height:20px;background:#747BAD"></span>' for p in (25, 50, 75))
           + '<span style="position:absolute;left:28%;top:-9px;width:14px;height:28px;margin-left:-7px;background:#00041F;box-shadow:inset 0 0 0 2px #E6E9FF"></span></div>'
           '<span class="cap2" style="color:#99FF80">The Maw</span></div>'
           '<div class="row" style="gap:18px;font-size:17px;color:#B8C0F0"><span>Ark 78%</span><span style="color:#FF4B3A">breach 25%</span><span>Scarab Reef  ·  0:48</span></div></div>')
    return obj_board("O2", "Objective B: a ribbon at top centre; the Ark's journey is the bar, the hull a number under it", "B: a ribbon",
                     rib + size_tag(610, 110, "700 x 60  ·  nothing on the left"),
                     [("Reads as", "Where the Ark is between home and The Maw. One glance up, like a race's lap bar."),
                      ("The hull", "A number under the ribbon; it turns red and grows when within 10% of the breach line."),
                      ("Job lines", "Move to the toasts. The left side of the screen is the game's again.")])


def obj_c():
    g = ('<div class="abs" style="left:40px;top:40px;width:260px;height:150px;display:flex;gap:16px;align-items:center">'
         '<div style="position:relative;width:110px;height:110px;flex:none">'
         '<svg width="110" height="110" viewBox="0 0 110 110" aria-hidden="true"><circle cx="55" cy="55" r="48" fill="rgba(0,4,31,.82)" stroke="rgba(67,76,137,.6)" stroke-width="6"></circle>'
         '<circle cx="55" cy="55" r="48" fill="none" stroke="#E6E9FF" stroke-width="6" stroke-dasharray="235 302" transform="rotate(-90 55 55)"></circle>'
         '<line x1="55" y1="1" x2="55" y2="13" stroke="#FF4B3A" stroke-width="3" transform="rotate(90 55 55)"></line></svg>'
         '<span class="abs num" style="inset:0;display:flex;align-items:center;justify-content:center;font-size:30px">78</span></div>'
         '<div style="display:flex;flex-direction:column;gap:8px"><span class="cap2" style="color:#E6E9FF">Ark hull</span>'
         f'<span style="font-size:14px;color:#B8C0F0">World 2 of 5</span>{dots(5, 1, 110)}</div></div>')
    held = ('<div class="mini abs" style="left:40px;top:250px;width:420px;padding:14px 18px;box-sizing:border-box;opacity:.92">'
            '<span class="cap2">Hold View for the full objective</span><div style="font-size:15px;margin-top:8px">Keep the Ark\'s hull above 25% to The Maw\'s core</div>'
            f'<div style="margin-top:10px">{dots(5, 1, 380)}</div></div>')
    return obj_board("O3", "Objective C: a corner gauge, 260 px; the hull first, the rest held on a button", "C: a gauge",
                     g + held + size_tag(40, 210, "260 x 150  ·  the box below shows only while held"),
                     [("Reads as", "One number that matters most: the Ark's hull, with the breach tick on the ring."),
                      ("On demand", "Holding View opens the full goal and route; letting go folds it."),
                      ("Crystal Run", "The ring counts crystals instead (12 of 20), the hull a small bar under it.")])


def obj_d():
    open_ = ('<div class="mini abs" style="left:40px;top:40px;width:440px;padding:16px 20px;box-sizing:border-box">'
             '<span class="cap2" style="color:#E6E9FF">World 2 of 5  ·  Scarab Reef  ·  hostile</span>'
             '<div style="font-size:18px;margin-top:8px;line-height:1.35">Keep the Ark\'s hull above 25%. Its fauna will go for the hull.</div>'
             f'<div style="margin-top:12px">{hullbar(78, 25)}</div><div style="margin-top:12px">{dots(5, 1, 400)}</div></div>')
    folded = ('<div class="mini abs" style="left:40px;top:300px;height:44px;padding:0 18px;display:flex;align-items:center;gap:14px;box-sizing:border-box">'
              f'<span class="cap2" style="color:#E6E9FF">Ark</span><span class="num">78%</span>{hullbar(78, 25, 120, 6)}'
              '<span style="font-size:14px;color:#B8C0F0">Scarab Reef  ·  0:48</span></div>')
    return obj_board("O4", "Objective D: opens for four seconds when something changes, then folds to one line (recommended)", "D: opens, then folds",
                     open_ + folded + size_tag(40, 250, "Open: 440 x 170, four seconds") + size_tag(40, 360, "Folded: 380 x 44, the rest of the time"),
                     [("Opens on", "A new world, the hull within 10% of the breach line, a goal reached. Never for a crystal."),
                      ("Folded", "Ark, hull, a short bar with the breach tick, the world and time to the next. One line, top left."),
                      ("Why", "The box is only big when it has news, so nothing large sits on the screen while you fly.")],
                     '<svg class="abs" style="left:250px;top:212px" width="20" height="80" viewBox="0 0 20 80" aria-hidden="true"><path d="M10 0v70M3 62l7 10 7-10" fill="none" stroke="#747BAD" stroke-width="2"></path></svg>')


# ── T: toasts ───────────────────────────────────────────────────────────────────────────────────────────────────────
KINDS = [("event", "ENTERING SCARAB REEF", "World 2 of 5. Hostile.", "#E6E9FF", "rgba(0,4,31,.88)"),
         ("threat", "FAUNA ON THE ARK", "Red ones are biting the hull", "#FF8A7C", "rgba(80,10,20,.86)"),
         ("goal", "20 CRYSTALS. BRING THEM HOME.", "", "#99FF80", "rgba(0,4,31,.88)")]


def toast_board(key, title, desc, live, row_fn, notes):
    rows = "".join(f'<div style="display:flex;flex-direction:column;gap:10px"><span class="cap2">{k}</span>{row_fn(k, a, b, c, bg)}</div>'
                   for k, a, b, c, bg in KINDS)
    strip = (f'<div class="abs" style="left:40px;top:640px;width:1340px;height:400px;background:#00041F;box-shadow:inset 0 0 0 1px rgba(67,76,137,.5);padding:26px 30px;box-sizing:border-box;'
             f'display:grid;grid-template-columns:1fr;gap:18px">{rows}</div>')
    body = (frame(40, 40, 1340, 560, G["approach"], live, "50% 45%", 0.12)
            + strip + side(1440, 60, 440, desc, notes))
    return page(f"Rev {REV} {key} - {title}", body)


def toasts_a():
    def t(k, a, b, c, bg):
        return (f'<div style="align-self:center;display:flex;flex-direction:column;align-items:center;gap:4px;padding:10px 90px;'
                f'background:linear-gradient(90deg,rgba(0,0,0,0),{bg} 22%,{bg} 78%,rgba(0,0,0,0))">'
                f'<b style="font-family:\'Chakra Petch\',sans-serif;font-size:22px;letter-spacing:.16em;color:{c}">{a}</b>'
                f'{f"<i style=font-style:normal;font-size:15px>{b}</i>" if b else ""}</div>')
    live = (ark(420, 200, 440) + fauna(520, 260) + '<div class="abs" style="left:0;right:0;top:56px;display:flex;justify-content:center">' + t(*KINDS[1][:2], KINDS[1][2], KINDS[1][3], KINDS[1][4]) + '</div>')
    return toast_board("T1", "Toasts A: the Rev 113 band, a third smaller, held high above the Ark", "A: the band, smaller", live, t,
                       [("Size", "22 px words on an 80 px band (was 30 px on 110). Sits at the top tenth of the screen, never over the Ark."),
                        ("Timing", "Three seconds, one at a time; a threat jumps the queue."),
                        ("The playtest", "The words floated over the Ark because the banner was drawn in the world. Here it is screen space.")])


def toasts_b():
    who = {"event": "Veliane Crestward", "threat": "Ilia Silvergate", "goal": "Tamako Deephook"}

    def t(k, a, b, c, bg):
        return (f'<div class="row" style="gap:14px;padding:10px 18px 10px 10px;background:{bg};box-shadow:inset 2px 0 0 {c};width:560px">'
                f'{face(who[k], 52, 62)}<span style="display:flex;flex-direction:column;gap:4px"><span class="cap2">{who[k].split()[0]}</span>'
                f'<span style="font-size:18px;color:{c}">{a.capitalize()}</span>{f"<span style=font-size:14px;color:#B8C0F0>{b}</span>" if b else ""}</span></div>')
    live = ark(420, 160, 440) + fauna(520, 220) + '<div class="abs" style="left:40px;bottom:40px">' + t(*KINDS[1]) + '</div>'
    return toast_board("T2", "Toasts B: the crew speaks; each toast is a pilot on the radio, face and words, lower left", "B: the crew speaks", live, t,
                       [("Reads as", "Your crew talking to you. The pilot who gave the job calls the big moments; wingmates call threats."),
                        ("Sound", "Each pilot has an empty radio-chirp slot; a voice line can come later."),
                        ("Where", "Lower left, above nothing the game needs. Four seconds each.")])


def toasts_c():
    def t(k, a, b, c, bg):
        return (f'<div class="row" style="gap:12px;height:44px;padding:0 16px;background:{bg};box-shadow:inset 2px 0 0 {c};width:420px;box-sizing:border-box">'
                f'<span style="font-size:15px;color:{c};white-space:nowrap">{a.capitalize()}</span>'
                f'{f"<span class=ell style=font-size:13px;color:#747BAD>{b}</span>" if b else ""}</div>')
    obj = ('<div class="mini abs" style="left:30px;top:30px;height:40px;padding:0 16px;display:flex;align-items:center;gap:12px">'
           f'<span class="cap2" style="color:#E6E9FF">Ark</span><span class="num">78%</span>{hullbar(78, 25, 100, 6)}</div>')
    live = (ark(500, 180, 420) + fauna(600, 240) + obj
            + f'<div class="abs" style="left:30px;top:80px;display:flex;flex-direction:column;gap:6px">{t(*KINDS[1])}<div style="opacity:.55">{t(*KINDS[0])}</div></div>')
    return toast_board("T3", "Toasts C: a quiet feed under the objective; the newest on top, two at most", "C: a feed", live, t,
                       [("Reads as", "A log you can glance at. Nothing crosses the middle of the screen."),
                        ("Timing", "Five seconds each; the older one fades as the new one lands."),
                        ("Risk", "Quiet enough to miss. Good for small news, weak for big moments.")])


def toasts_d():
    def t(k, a, b, c, bg):
        if k == "event":
            return (f'<div style="align-self:center;display:flex;flex-direction:column;align-items:center;gap:6px">'
                    f'<span class="cap2">World 2 of 5</span><b style="font-family:\'Chakra Petch\',sans-serif;font-size:38px;letter-spacing:.24em">SCARAB REEF</b>'
                    f'<span style="font-size:14px;color:#FF8A7C">Hostile</span></div>')
        return t_small(a, b, c, bg)

    def t_small(a, b, c, bg):
        return (f'<div style="align-self:center;display:flex;align-items:center;gap:12px;padding:8px 22px;background:{bg}">'
                f'<b style="font-family:\'Chakra Petch\',sans-serif;font-size:17px;letter-spacing:.14em;color:{c}">{a}</b>'
                f'{f"<span style=font-size:13px;color:#B8C0F0>{b}</span>" if b else ""}</div>')
    live = (ark(420, 230, 440) + fauna(520, 290)
            + '<div class="abs" style="left:0;right:0;top:70px;display:flex;flex-direction:column;align-items:center;gap:6px">'
            + '<span class="cap2">World 2 of 5</span><b style="font-family:\'Chakra Petch\',sans-serif;font-size:44px;letter-spacing:.24em">SCARAB REEF</b>'
            + '<span style="font-size:15px;color:#FF8A7C">Hostile</span></div>')
    return toast_board("T4", "Toasts D: two sizes; a new world, a breach or the goal takes the screen, everything else is a small line (recommended)", "D: two sizes", live, t,
                       [("Big", "A new world, BREACH CLOSE, the goal reached, the last cell. Centre top, two seconds in, out with the music sting."),
                        ("Small", "Fauna on the Ark, a wingmate down, a crystal milestone. One line, top centre, three seconds."),
                        ("Why", "Big moments should feel big. That is most of the 'feel' the playtest missed.")])


# ── F: the feel in flight ───────────────────────────────────────────────────────────────────────────────────────────
def feel_board(key, title, desc, scene, notes):
    body = (frame(40, 40, 1340, 754, scene[0], scene[1], scene[2], 0.1)
            + side(1440, 60, 440, desc, notes[:3])
            + '<div class="abs" style="left:40px;top:830px;width:1340px;display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:30px">'
            + "".join(f'<div style="display:flex;flex-direction:column;gap:8px"><span class="cap2" style="color:#E6E9FF">{a}</span><span class="note">{b}</span></div>' for a, b in notes[3:])
            + '</div>')
    return page(f"Rev {REV} {key} - {title}", body)


def feel_a():
    inner = (ark(560, 150, 560) + fauna(700, 230) + fauna(880, 320, .8) + '<div class="abs" style="left:0;top:0;bottom:0;width:120px;background:linear-gradient(90deg,rgba(0,1,10,.5),rgba(0,1,10,0))"></div>')
    return feel_board("F1", "Feel A: Chase; the camera close behind you, the Ark the landmark ahead and to one side",
                      "A: Chase", (G["approach"], inner, "50% 50%"),
                      [("Camera", "Close behind your vessel, low. The Ark always ahead, a third of the frame."),
                       ("Speed", "Streaks in the dust when you boost; the field of view opens a little."),
                       ("Fauna", "Lit red bodies on the Ark's hull; their light falls on the plates they bite."),
                       ("Good for", "Flying feels fast and yours."), ("Weak for", "You lose the Ark when you turn to chase."),
                       ("Changes", "Camera offset per vessel; the dust streaks; red fauna light.")])


def feel_b():
    inner = (ark(400, 230, 560) + fauna(470, 260) + fauna(520, 400, .9) + fauna(900, 330, .9)
             + hull("Manta", 300, 520, 150, -10) + hull("Dolphin", 960, 200, 110, 8) + hull("Sparrow", 1000, 520, 110, -6)
             + '<span class="abs" style="left:620px;top:470px;width:120px;height:60px;border-radius:50%;background:radial-gradient(ellipse,rgba(255,75,58,.35),rgba(255,75,58,0) 70%)"></span>')
    return feel_board("F2", "Feel B: Guardian; the camera pulled back, the Ark at the centre, you and the threats read at a glance (recommended)",
                      "B: Guardian", (G["cell"], inner, "50% 50%"),
                      [("Camera", "Pulled back and a little above; the Ark sits in the middle third, you orbit it."),
                       ("Threats", "Fauna lit red, the Ark's hull plates flare where they bite; you see the fight before the HUD tells you."),
                       ("The Ark", "Turns its lights to amber, then red, as the hull falls: the hull bar, told in the world."),
                       ("Good for", "An escort: what you protect is always in the shot."), ("Weak for", "Less speed in the frame."),
                       ("Changes", "An escort camera rig; the Ark's hull lights tied to its hull; red fauna light.")])


def feel_c():
    bars = ('<div class="abs" style="left:0;right:0;top:0;height:70px;background:#00010A"></div>'
            '<div class="abs" style="left:0;right:0;bottom:0;height:70px;background:#00010A"></div>')
    inner = (bars + ark(300, 170, 760, -6) + fauna(420, 250, 1.4) + fauna(560, 360, 1.2) + fauna(760, 300)
             + '<span class="abs" style="left:0;right:0;bottom:100px;text-align:center;font-family:\'Chakra Petch\',sans-serif;font-weight:700;font-size:34px;letter-spacing:.2em;color:#FF8A7C">BREACH CLOSE</span>')
    return feel_board("F3", "Feel C: Cinematic beats; for two seconds at the big moments the camera takes over, then hands back",
                      "C: Cinematic beats", (G["through"], inner, "50% 50%"),
                      [("Beats", "A new world, the breach line close, the last cell, the Ark's arrival. Never more than one a minute."),
                       ("Camera", "Letterbox in, a slow move on the Ark, time slowed to half; back to your seat in two seconds."),
                       ("Control", "You keep steering through it; the beat can be turned off in settings."),
                       ("Good for", "The voyage has a story you can see."), ("Weak for", "Taking the camera can annoy in a fight."),
                       ("Changes", "A beat director on the voyage run; the same Cinemachine rig as the intro.")])


def feel_d():
    inner = (ark(620, 160, 520)
             + '<span class="abs" style="left:560px;top:300px;width:140px;height:140px;border-radius:50%;background:radial-gradient(circle,rgba(255,174,0,.55),rgba(255,75,58,.2) 40%,rgba(0,0,0,0) 70%)"></span>'
             + "".join(f'<span class="abs" style="left:{630 + dx}px;top:{370 + dy}px;width:10px;height:3px;background:#FFAE00;transform:rotate({r}deg)"></span>'
                       for dx, dy, r in ((-40, -30, 30), (30, -40, -40), (50, 10, 10), (-50, 20, -20), (10, 50, 70), (-10, -55, 90)))
             + gem(300, 200, 1.2) + gem(250, 280) + '<div class="mini abs" style="left:30px;top:30px;height:40px;padding:0 16px;display:flex;align-items:center;gap:12px">'
             '<span class="cap2" style="color:#E6E9FF">Crystals</span><span class="num">13 / 20</span><span style="font-size:13px;color:#99FF80">+1</span></div>')
    return feel_board("F4", "Feel D: Punch; every hit, kill and crystal answers at once in the world, the sound and the controller",
                      "D: Punch", (G["approach"], inner, "50% 50%"),
                      [("Kills", "A fauna bursts into lit shards; a short hit-stop (60 ms); a rumble; an empty sound slot for the kill."),
                       ("Crystals", "The crystal flies to you and the counter ticks with a soft chime slot; every fifth gets a bigger one."),
                       ("The Ark", "Each bite shakes the Ark's plates and flashes them; the camera nudges when it is badly hit."),
                       ("Good for", "Every action feels answered."), ("Weak for", "Busy if every effect is loud; needs tuning on device."),
                       ("Changes", "Hit-stop and rumble on the voyage run; the kill and crystal sound slots; shard bursts.")])


# ── notes (a sticky on the tab, written into the index by the round's publish) ───────────────────────────────────────────────────────────────────────────────────────────────────────────
NOTES = [
    ("What Shombith saw", "No intro. The objective box too big on the left. No toasts: the words showed over the Ark instead. The crew screen needs rework. The game did not feel good yet."),
    ("Crew (C1-C4)", "A keeps the HyperSea in view and numbers each job on the map (recommended). B leads with your pilots' faces. C is a poster board by faction. D puts factions in tabs."),
    ("Intro (I1-I4)", "A is the Ark, your squad by name, the goal and the route in nine seconds (recommended: it is his list). B dives from the map. C opens on the threat. D is a five-second launch."),
    ("Objective (O1-O4)", "A is a 420 px strip. B a ribbon at top centre. C a 260 px hull gauge with the rest held on a button. D opens on news and folds to one line (recommended)."),
    ("Toasts (T1-T4)", "A is the picked band, smaller. B is the crew on the radio. C a quiet feed. D two sizes: big moments take the screen (recommended). All are screen space; the playtest's words were drawn in the world."),
    ("Feel (F1-F4)", "A chase camera. B guardian camera with the Ark central and its lights showing its hull (recommended). C two-second cinematic beats. D punch: hit-stop, shards, sound slots. B and D combine."),
    ("Rules kept", "Every word an authored field; colours from the set; sounds empty slots; fauna shown lit in the world, never as rings; no glows in the UI; a game mode never changes the vessel kit."),
    ("Next", "Pick one per row (or mix). The picks become prompts for the in-game and app-shell threads."),
]


def pair(a, b, title):
    """Two options on one tall board (the canvas holds at most 512 files): A above, B under it."""
    _CAPTURE.append([])
    a(), b()
    top, bottom = _CAPTURE.pop()
    body = (f'<div style="position:absolute;left:0;top:0;width:1920px;height:1080px;overflow:hidden">{top}</div>'
            f'<div style="position:absolute;left:0;top:1120px;width:1920px;height:1080px;overflow:hidden">{bottom}</div>')
    return page(title, body, 2200)


BOARDS = {f"R{REV}C1.dc.html": crew_a, f"R{REV}C2.dc.html": crew_b, f"R{REV}C3.dc.html": crew_c, f"R{REV}C4.dc.html": crew_d,
          f"R{REV}IAB.dc.html": lambda: pair(intro_a, intro_b, f"Rev {REV} IAB - Intro A: Departure (recommended), and Intro B: From the map"),
          f"R{REV}ICD.dc.html": lambda: pair(intro_c, intro_d, f"Rev {REV} ICD - Intro C: Threat first, and Intro D: One take"),
          f"R{REV}O1.dc.html": obj_a, f"R{REV}O2.dc.html": obj_b, f"R{REV}O3.dc.html": obj_c, f"R{REV}O4.dc.html": obj_d,
          f"R{REV}TAB.dc.html": lambda: pair(toasts_a, toasts_b, f"Rev {REV} TAB - Toasts A: the band, smaller, and Toasts B: the crew speaks"),
          f"R{REV}TCD.dc.html": lambda: pair(toasts_c, toasts_d, f"Rev {REV} TCD - Toasts C: a feed, and Toasts D: two sizes (recommended)"),
          f"R{REV}F1.dc.html": feel_a, f"R{REV}F2.dc.html": feel_b, f"R{REV}F3.dc.html": feel_c, f"R{REV}F4.dc.html": feel_d}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
