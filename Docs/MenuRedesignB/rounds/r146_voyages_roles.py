#!/usr/bin/env python3
"""Rev 146 boards: the Voyages screens redrawn for vessel ROLES and ARCHETYPES (Frogma tab "Voyages",
Docs/MENU_REDESIGN_B.md).

Shombith, 2026-11-26: the roles board is final (https://claude.ai/artifact/Duwb9xJ7XdAykxMKAnMyFe, his JSON at
/mnt/project-files/archetypes/board-2026-11-26.json). Cultivator, Destroyer and Guardian are ROLES; their nine sub-types
are ARCHETYPES (Pumper, Builder, Burster / Sapper, Specialist, Nuke / Harraser, Zoner, Enhancer). Each vessel has a
primary archetype, a secondary, sometimes a tertiary. They replace the Milestone 2 classes everywhere a class showed:
the quay's job cards and the giver's "Bring me", Your Ark's seats, and Vessels.

The picked Rev 138 screens are kept (the quay, Your Ark, Vessels E); only what the classes did is redrawn, three ways:

  A  Seat asks (recommended)  the giver asks for three archetypes, each with its reason; each seat shows the ask it fills
  B  Know the crew            the giver names the rival crew's archetypes and asks for their counters
  C  Plain words              no glyph rows: the ask as a sentence, archetypes as words, Vessels as a grouped list

The ask per voyage type and the fit rule come from the gameplay thread (2026-11-26): three seats, each one archetype; a
vessel meets a seat fully on its primary, half on a secondary or tertiary; squad fit 0-3 in halves. Difficulty is 1-9
(the four-step "Rough sea" bar of Rev 138 is replaced).

    python3 Docs/MenuRedesignB/rounds/r146_voyages_roles.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "146"
_spec = importlib.util.spec_from_file_location("r138", HERE / "r138_voyages_shell_final.py")
r138 = importlib.util.module_from_spec(_spec)
sys.argv = [sys.argv[0], _root, _rev]
_spec.loader.exec_module(r138)
r136, r135, r134, r133 = r138.r136, r138.r135, r138.r134, r138.r133

REV = int(_rev)
ROOT = r138.ROOT
INK, MUTE, SOFT, SEL = r133.INK, r133.MUTE, r133.SOFT, r133.SEL
RED2 = r133.RED2
page, window, kc, face, flower, gems = r133.page, r133.window, r133.kc, r133.face, r133.flower, r133.gems
HULL, CREW, PREVIEW, FADE, FLOOR = r133.HULL, r133.CREW, r138.PREVIEW, r138.FADE, r134.FLOOR
LINE = "rgba(67,76,137,.6)"

# ── the roles and archetypes (Shombith's board, 26 Nov) ─────────────────────────────────────────────────────────────
ROLE_GLYPH = {  # the three role glyphs: Rev 133's Cultivator sprout, Destroyer star and Guardian shield
    "Cultivator": r133.CLASS["Cultivator"], "Destroyer": r133.CLASS["Destroyer"], "Guardian": r133.CLASS["Guardian"]}
ROLES = {"Cultivator": ["Pumper", "Builder", "Burster"], "Destroyer": ["Sapper", "Specialist", "Nuke"],
         "Guardian": ["Harraser", "Zoner", "Enhancer"]}
ROLE_OF = {a: r for r, aa in ROLES.items() for a in aa}
ROLE_GOAL = {"Cultivator": "Grows your prisms", "Destroyer": "Destroys enemy prisms", "Guardian": "Protects your prisms"}
ID = {"pumper": "Pumper", "scout": "Builder", "burster": "Burster", "sapper": "Sapper", "specialist": "Specialist", "nuke": "Nuke",
      "hunters": "Harraser", "builders": "Zoner", "enhancers": "Enhancer"}            # the board's draft ids
DESC = {
    "Pumper": "Steadily makes prisms faster than anyone.", "Builder": "Makes prisms that shield fauna and standing prisms.",
    "Burster": "One big, quick jump in your team's volume.", "Sapper": "Wears enemy prisms down over time.",
    "Specialist": "Reaches the main targets and counters special prisms.", "Nuke": "Destroys many enemy prisms at once.",
    "Harraser": "Silences the enemy's attackers.", "Zoner": "Makes fields where prisms take no damage.",
    "Enhancer": "Buffs and heals your prisms under attack.",
}
_BOARD = Path("/mnt/project-files/archetypes/board-2026-11-26.json")
_DATA = json.loads(_BOARD.read_text()) if _BOARD.exists() else None
if _DATA:
    ARCH = {}
    for v in _DATA["vessels"]:
        name = v["name"].replace("&amp;", "&")
        p = [ID[k] for k, t in v["roles"].items() if t == "p"]
        s = [ID[k] for k, t in v["roles"].items() if t == "s"]
        ARCH[name] = p + s
    COUNTERS = [(ID[l["from"]], ID[l["to"]]) for l in _DATA["links"] if l["kind"] == "k_counter"]
else:  # the same data, frozen 2026-11-26, so the generator runs without the project folder
    ARCH = {"Manta": ["Nuke", "Pumper"], "Squirrel": ["Pumper", "Harraser"], "Tiger Moth": ["Pumper", "Enhancer"], "Wombat": ["Burster"],
            "Scarab": ["Burster", "Builder"], "Sparrow": ["Sapper", "Harraser", "Builder"], "Brittle Star": ["Sapper", "Harraser"],
            "Grizzly": ["Sapper", "Nuke"], "Shrike": ["Specialist", "Builder"], "Serpent": ["Builder", "Specialist"],
            "Dolphin": ["Nuke", "Harraser", "Pumper"], "Thresher": ["Nuke", "Pumper"], "Urchin": ["Enhancer", "Burster"],
            "Rhino": ["Specialist", "Sapper"], "Stoat": ["Sapper", "Nuke"], "Angler": ["Sapper", "Harraser"],
            "Goby & Pistol": ["Pumper", "Sapper"], "Wrasse & Grouper": ["Pumper", "Sapper"], "Orca": ["Specialist", "Sapper"],
            "Starling": ["Harraser", "Sapper"], "Jacks": ["Harraser", "Sapper"], "Honeyguide & Ratel": ["Sapper", "Pumper"],
            "Meerkat": ["Builder"], "Swordfish": ["Sapper", "Specialist"], "Jewel Wasp": ["Specialist", "Sapper", "Harraser"]}
    COUNTERS = [("Builder", "Sapper"), ("Sapper", "Pumper"), ("Builder", "Specialist"), ("Nuke", "Builder"), ("Specialist", "Burster"),
                ("Burster", "Nuke"), ("Sapper", "Enhancer"), ("Specialist", "Enhancer"), ("Enhancer", "Nuke"), ("Nuke", "Zoner"),
                ("Zoner", "Sapper"), ("Harraser", "Specialist")]
STARTERS = ["Squirrel", "Sparrow", "Dolphin", "Thresher", "Tiger Moth", "Swordfish"]
PIC = {"Honeyguide & Ratel": "Honeyguide", "Wrasse & Grouper": "Wrasse"}            # the hangar preview's name


def beats(a):
    return [b for x, b in COUNTERS if x == a]


def beaten_by(a):
    return [x for x, b in COUNTERS if b == a]


def primary(a):
    return [v for v, aa in ARCH.items() if aa[0] == a]


def also(a):
    return [v for v, aa in ARCH.items() if a in aa[1:]]


def meets(v, a):
    aa = ARCH.get(v, [])
    return 1.0 if aa and aa[0] == a else .5 if a in aa else 0.0


# ── the voyage and the squad (the gameplay thread's asks, 26 Nov) ──────────────────────────────────────────────────
JOBS = [  # key, name, kind, giver callsign, giver, what they are, difficulty 1-9, the ask (three archetypes, each with the giver's reason)
    ("salvage", "The Glassmere Shatter", "Salvage", "GALLEON", "Cass Morrow", "Pirate", 4,
     [("Nuke", "cracks the wreck's shell in one go"), ("Sapper", "wears down the walls the Rusted Grin leave behind"),
      ("Harraser", "silences their Specialist before it reaches the hold")]),
    ("escort", "The Tollmoor Crossing", "Escort the Ark", "WINGMAN", "Captain Theo Lark", "First responder", 6,
     [("Enhancer", ""), ("Harraser", ""), ("Nuke", "")]),
    ("bounty", "Black Tide Rising", "Bounty", "BANDIT", "Juno Rask", "Bounty hunter", 6,
     [("Harraser", ""), ("Specialist", ""), ("Sapper", "")]),
    ("core", "Ironmouth Holds", "Hold the Core", "RAMPART", "Marshal Edda Kroll", "Aristocrat", 3,
     [("Pumper", ""), ("Burster", ""), ("Sapper", "")]),
]
JOB = {j[0]: j for j in JOBS}
J = JOB["salvage"]
ASK = [a for a, _ in J[7]]
QUOTE = r133.JOB["salvage"][9]
RIVAL = ("The Rusted Grin", [("Serpent", "Builder"), ("Rhino", "Specialist"), ("Goby & Pistol", "Pumper")])
# Shombith's default squad, seated as Rev 134 seats it; each seat fills the ask its vessel meets best.
SEATS = [  # seat, who, vessel, element, pilot, mood, the ask it fills
    ("Leader", "You", "Sparrow", "Charge", "TALON", "Keen", "Sapper"),
    ("Seat 2", "Rook", "Squirrel", "Mass", "VOLT", "Steady", "Harraser"),
    ("Seat 3", "AI wingmate", "Dolphin", "Space", "OPAL", "Reluctant", "Nuke"),
]
FIT = sum(meets(s[2], s[6]) for s in SEATS)          # 2.5: Sparrow and Dolphin on their primaries, Squirrel's secondary Harraser
MOOD_SAY = r133.MOOD_SAY
MOOD_WHY = r133.MOOD_WHY


def screen(title, body):
    """One screen of a tall option board: its title and its body, placed later by tall()."""
    return title, body


def tall(title, screens):
    """An option board: its three screens stacked, 1920 x 3240 (the canvas holds 512 files, so one board per option)."""
    body = "".join(f'<div class="abs" style="left:0;top:{i * 1080}px;width:1920px;height:1080px;overflow:hidden">{b}</div>'
                   + (f'<div class="abs" style="left:0;right:0;top:{i * 1080 - 1}px;height:2px;background:#00010A;z-index:30"></div>' if i else "")
                   for i, (_, b) in enumerate(screens))
    html = page(title, body)
    assert html.count("height: 1080px") >= 1
    return html.replace("width: 1920px; height: 1080px", "width: 1920px; height: 3240px", 1).replace('"height":1080', '"height":3240')


# ── small pieces ────────────────────────────────────────────────────────────────────────────────────────────────────
def rglyph(role, s=20, col=INK, w=1.8):
    return (f'<svg width="{s}" height="{s}" viewBox="0 0 30 30" aria-hidden="true" style="flex:none"><path d="{ROLE_GLYPH[role]}" fill="none" '
            f'stroke="{col}" stroke-width="{w}" stroke-linejoin="round" stroke-linecap="round"></path></svg>')


def atag(a, size=15, col=INK, s=20):
    """An archetype as one word with its role's glyph."""
    return f'<span class="row" style="gap:8px;font-size:{size}px;color:{col};white-space:nowrap">{rglyph(ROLE_OF[a], s, col)}{a}</span>'


def met(x, s=18):
    """How fully a vessel meets an ask: a full ring (primary), a half ring (secondary or tertiary), an empty one."""
    if x >= 1:
        inner = f'<circle cx="10" cy="10" r="5" fill="{INK}"></circle>'
    elif x > 0:
        inner = f'<path d="M10 5 A5 5 0 0 0 10 15 Z" fill="{INK}"></path>'
    else:
        inner = ""
    return (f'<svg width="{s}" height="{s}" viewBox="0 0 20 20" aria-hidden="true" style="flex:none"><circle cx="10" cy="10" r="8" fill="none" '
            f'stroke="{INK if x > 0 else MUTE}" stroke-width="1.6"></circle>{inner}</svg>')


def met_word(x):
    return "Meets it" if x >= 1 else "Half: not its first" if x > 0 else "Does not meet it"


def difficulty(n, w=120):
    segs = "".join(f'<span style="flex:1;height:8px;background:{INK if i < n else "rgba(67,76,137,.6)"}"></span>' for i in range(9))
    return (f'<span class="row" style="gap:10px;font-size:14px;color:{SOFT}"><span class="row" style="gap:2px;width:{w}px">{segs}</span>'
            f'Difficulty {n}</span>')


def fit_meter(fit=FIT, w=180, frm=None):
    segs = ""
    for i in range(3):
        part = max(0.0, min(1.0, fit - i))
        segs += (f'<span style="flex:1;height:10px;background:linear-gradient(90deg,{INK} {part * 100:.0f}%,rgba(67,76,137,.6) {part * 100:.0f}%)"></span>')
    f = lambda x: f"{x:g}"
    words = f"Squad fit {f(frm)} to {f(fit)} of 3" if frm is not None and frm != fit else f"Squad fit {f(fit)} of 3"
    return (f'<span class="row" style="gap:14px"><span class="row" style="gap:4px;width:{w}px">{segs}</span>'
            f'<span style="font-size:15px;color:{INK};white-space:nowrap">{words}</span></span>')


def pic(v, w, h, fade=True, op=1.0):
    k = PIC.get(v, v)
    if k in PREVIEW:
        return (f'<img src="/_blob/{PREVIEW[k]}" alt="{v}" style="width:{w}px;height:{h}px;object-fit:cover;opacity:{op};'
                f'{FADE if fade else ""}">')
    return (f'<span style="width:{w}px;height:{h}px;display:flex;align-items:center;justify-content:center;opacity:{op}">'
            f'{rglyph(ROLE_OF[ARCH[v][0]], min(w, h) // 2, SOFT, 1.1)}</span>')


def header(right=""):
    """Your Ark's voyage line (Rev 134), with difficulty 1-9 and whatever the option puts on the right."""
    return (f'<div class="abs row" style="left:40px;right:40px;top:176px;justify-content:space-between">'
            f'<span class="row" style="gap:16px">{face(J[3], 44, 52)}<span style="display:flex;flex-direction:column;gap:6px">'
            f'<span style="font-size:18px">{J[1]}</span><span class="row" style="gap:16px;font-size:14px;color:{SOFT}">{difficulty(J[6])}'
            f'<span>3 cells  ·  for {J[4]}</span></span></span></span>{right}</div>')


def asked_right(style="tags"):
    if style == "words":
        return f'<span style="font-size:15px;color:{SOFT}">Asks for a <span style="color:{INK}">{ASK[0]}</span>, a <span style="color:{INK}">{ASK[1]}</span> and a <span style="color:{INK}">{ASK[2]}</span></span>'
    return f'<span class="row" style="gap:20px"><span class="lab">Asks for</span>{"".join(atag(a, 15) for a in ASK)}</span>'


# ── the quay (Rev 138 Board, kept) ──────────────────────────────────────────────────────────────────────────────────
def job_card(i, key, on, marks):
    j = JOB[key]
    x = 40 + i * 240
    edge = f"inset 0 -2px 0 {SEL}" if on else f"inset 0 0 0 1px {LINE}"
    return (f'<button class="pick abs" type="button" aria-pressed="{str(on).lower()}" style="left:{x}px;top:190px;width:230px;height:420px;overflow:hidden;'
            f'background:#00010A;box-shadow:{edge}">'
            f'<img src="{CREW[j[3]][3]}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:50% 20%;opacity:{1 if on else .55}">'
            f'<div class="abs" style="left:0;right:0;bottom:0;height:210px;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.95) 45%)"></div>'
            f'<div class="abs" style="left:18px;right:18px;bottom:18px;display:flex;flex-direction:column;gap:8px;pointer-events:none">'
            f'<span class="lab" style="color:{INK if on else SOFT}">{j[2]}</span><span style="font-size:18px;line-height:1.3">{j[1]}</span>'
            f'{marks(j, on)}</div></button>')


def crystal_run_card(x, marks_empty=""):
    return (f'<button class="pick abs" type="button" aria-pressed="false" style="left:{x}px;top:190px;width:230px;height:420px;overflow:hidden;'
            f'background:#00010A;box-shadow:inset 0 0 0 1px {LINE}">'
            f'<img src="{r133.ARK}" alt="" style="position:absolute;left:-60px;top:110px;width:360px;height:170px;object-fit:contain;opacity:.6">'
            f'<div class="abs" style="left:0;right:0;bottom:0;height:190px;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.95) 50%)"></div>'
            f'<div class="abs" style="left:18px;right:18px;bottom:18px;display:flex;flex-direction:column;gap:8px;pointer-events:none">'
            f'<span class="lab" style="color:{SOFT}">Raid</span><span style="font-size:18px;line-height:1.3">Crystal Run to Tollmoor</span>'
            f'<span style="font-size:14px;color:{MUTE}">Asks for no one</span></div></button>')


def chart():
    return r133.sea_map(1260, 190, 620, 420, "60% 45%", .3,
                        r133.lane_svg([(60, 330), (220, 260), (380, 200), (540, 140)], w=620, h=420)
                        + r133.place(60, 330, r133.DOCK, True, 14) + r133.place(540, 140, "Glassmere", False, 14)
                        + r133.cell(220, 260, 46, "ahead") + r133.cell(380, 200, 46, "ahead")
                        + '<span class="abs lab" style="left:20px;top:18px">On the chart</span>'
                        + f'<span class="abs" style="left:20px;bottom:18px;font-size:14px;color:{SOFT}">3 cells  ·  3-5 min  ·  Difficulty {J[6]}</span>') + \
        (f'<button class="btn sm soft abs" type="button" style="left:1640px;top:204px;width:224px;gap:12px;z-index:4">{r135.EXPAND}Full screen</button>')


def crew_says():
    return (f'<div class="abs" style="left:1260px;top:640px;width:620px;display:flex;flex-direction:column;gap:12px">'
            f'<span class="lab">Your crew says</span>'
            + "".join(f'<div class="row" style="gap:12px">{face(cs, 36, 44)}<span style="font-size:15px;flex:1;color:{INK}">"{MOOD_SAY[m]}"</span>{r133.mood_mark(m)}</div>'
                      for _, _, _, _, cs, m, _ in SEATS) + '</div>')


def quay(title, marks, ask_block, see_word="See every archetype"):
    cards = "".join(job_card(i, k, k == "salvage", marks) for i, k in enumerate(("salvage", "escort", "bounty", "core")))
    head = f'<span class="abs" style="left:40px;top:164px;font-size:14px;color:{SOFT}">Docked at {r133.DOCK}, {r133.ZONE}. Five jobs on the quay.</span>'
    see = (f'<button class="btn sm soft abs" type="button" style="left:1260px;top:880px;width:360px;gap:12px;z-index:4">{kc("Y")}{see_word}</button>')
    foot = (f'<div class="abs row" style="left:40px;right:40px;top:990px;justify-content:space-between">'
            f'{fit_meter()}<span class="row" style="gap:24px">{gems(36, 20)}'
            f'<button class="btn soft-cta" type="button" style="width:300px">{kc("A")}Take the job</button></span></div>')
    return screen(title, window(1, head + cards + crystal_run_card(1000) + chart() + ask_block + see + crew_says() + foot))


def giver_line():
    return (f'<span class="lab">{J[4]}  ·  {J[5]}</span>'
            f'<span style="font-size:22px;line-height:1.45;max-width:1140px">"{QUOTE}"</span>')


# ── Your Ark (Rev 134 seats, kept) ──────────────────────────────────────────────────────────────────────────────────
def seat(i, x, under, over=""):
    """Rev 134's seat: the hull standing on the floor, the vessel's line, the pilot. `under` is what each option puts
    beside the vessel's name; `over` sits above the hull."""
    role, who, v, el, cs, m, fills = SEATS[i]
    st = {"You": "You", "Rook": "Ready", "AI wingmate": "AI"}[who]
    stc = {"You": SEL, "Ready": INK, "AI": MUTE}[st]
    foot = (f'<button class="btn sm soft" type="button" style="width:100%">{kc("X")}Change your seat</button>' if who == "You" else
            f'<span style="font-size:14px;color:{MUTE}">Rook picks their own seat.</span>' if who == "Rook" else
            '<span class="row" style="gap:10px"><button class="btn sm soft chipq" type="button" style="flex:1">Invite a friend</button>'
            '<button class="btn sm soft chipq" type="button" style="flex:1">Change wingmate</button></span>')
    return (f'<div class="abs" style="left:{x}px;top:250px;width:520px;height:700px">'
            f'<span aria-hidden="true" class="abs" style="left:40px;right:40px;top:230px;height:60px;border-radius:50%;'
            f'background:radial-gradient(ellipse,rgba(150,172,252,.22),rgba(150,172,252,0) 70%)"></span>'
            f'<img class="hull" src="{HULL[v]}" alt="{v}" style="position:absolute;left:40px;width:440px;top:40px;height:230px;object-fit:contain">'
            f'<div class="abs" style="left:0;right:0;top:0;display:flex;justify-content:space-between"><span class="cap2">{role}  ·  {who}</span>'
            f'<span class="cap2" style="color:{stc}">{st}</span></div>{over}'
            f'<div class="abs row" style="left:0;right:0;top:300px;gap:14px"><span style="font-size:24px">{v}</span>'
            f'{under(v, fills)}<span style="margin-left:auto">{flower(el, 30)}</span></div>'
            f'<div class="abs" style="left:0;right:0;top:350px;height:1px;background:{SEL if who == "You" else LINE}"></div>'
            f'<div class="abs row" style="left:0;right:0;top:370px;gap:16px;align-items:flex-start">{face(cs, 84, 100)}'
            f'<span style="display:flex;flex-direction:column;gap:8px;flex:1"><span style="font-size:18px">{CREW[cs][0]}</span>'
            f'<span style="font-size:16px;line-height:1.4">"{MOOD_SAY[m]}"</span></span></div>'
            f'<div class="abs row" style="left:0;right:0;top:490px;gap:28px">{r133.mood_tag(m)}</div>'
            f'<span class="abs" style="left:0;top:524px;font-size:14px;color:{SOFT}">{MOOD_WHY[m]}</span>'
            f'<div class="abs" style="left:0;right:0;top:580px">{foot}</div></div>')


def sail_bar():
    return (f'<div class="abs row" style="left:40px;right:40px;top:990px;justify-content:space-between">{fit_meter()}'
            f'<span class="row" style="gap:24px"><span style="font-size:16px;color:#B8C0F0">Pay 36</span>'
            f'<button class="btn soft-cta" type="button" style="width:300px">{kc("A")}Set sail</button></span></div>')


def ark(title, under, over=lambda i: "", right=None, extra=""):
    body = header(right if right is not None else asked_right()) + FLOOR + "".join(seat(i, 120 + i * 580, under, over(i)) for i in range(3)) + sail_bar() + extra
    return screen(title, window(3, body))


# ── Vessels (Vessels E, kept: the floor, hulls standing on it, one CTA) ─────────────────────────────────────────────
SEAT_V, SEAT_ASK = "Squirrel", "Harraser"          # Vessels opened from Seat 2 (Squirrel, which meets Harraser only half)
PICKED = "Starling"


def standing(v, x, w, top=250, on=False, line="", h=None):
    h = h or w * 9 // 16
    return (f'<button class="pick abs" type="button" aria-pressed="{str(on).lower()}" style="left:{x}px;top:{top}px;width:{w}px;height:{h + 170}px;background:none">'
            f'<span class="abs" style="left:0;top:0">{pic(v, w, h)}</span>'
            f'<span class="abs" style="left:0;right:0;top:{h + 20}px;font-size:22px;text-align:left">{v}</span>'
            f'<span class="abs" style="left:0;right:0;top:{h + 62}px;height:{2 if on else 1}px;background:{SEL if on else LINE}"></span>'
            f'<span class="abs" style="left:0;right:0;top:{h + 80}px;font-size:15px;color:{SOFT};text-align:left;line-height:1.4">{line}</span></button>')


def small(v, x, y, w=200, on=False, now=False):
    tag = f'<span style="font-size:13px;color:{SEL}">In this seat now</span>' if now else ""
    return (f'<button class="pick abs" type="button" aria-pressed="{str(on).lower()}" style="left:{x}px;top:{y}px;width:{w}px;height:{w * 9 // 16 + 60}px;background:none;'
            f'box-shadow:inset 0 0 0 1px {SEL if now else "transparent"}">'
            f'<span class="abs" style="left:0;top:0">{pic(v, w, w * 9 // 16, op=.85)}</span>'
            f'<span class="abs row" style="left:10px;right:10px;top:{w * 9 // 16 + 8}px;justify-content:space-between">'
            f'<span style="font-size:16px">{v}</span>{met(meets(v, SEAT_ASK), 16)}</span>'
            f'<span class="abs" style="left:10px;top:{w * 9 // 16 + 34}px">{tag}</span></button>')


def vbar(frm, to, cta):
    return (f'<div class="abs row" style="left:40px;right:40px;top:990px;gap:24px">{fit_meter(to, 180, frm)}'
            f'<button class="btn sm soft" type="button" style="width:180px;margin-left:auto">{kc("B")}Back</button>'
            f'<button class="btn soft-cta" type="button" style="width:380px">{kc("A")}{cta}</button></div>')


def seat_line(words):
    return f'<span class="abs" style="left:40px;top:240px;font-size:15px;color:{SOFT}">{words}</span>'


NEW_FIT = FIT - meets(SEAT_V, SEAT_ASK) + meets(PICKED, SEAT_ASK)      # 2.5 to 3


def arch_names(v, lead=INK, rest=SOFT, sep="  ·  "):
    aa = ARCH[v]
    return sep.join(f'<span style="color:{lead if i == 0 else rest}">{a}</span>' for i, a in enumerate(aa))


# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# A  Seat asks: three archetypes, each with its reason; each seat shows the ask it fills
# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def a_cardmarks(j, on):
    col = INK if on else SOFT
    return (f'<span style="display:flex;flex-direction:column;gap:6px">'
            + "".join(f'<span class="row" style="gap:8px;font-size:14px;color:{col}">{rglyph(ROLE_OF[a], 16, col)}{a}</span>' for a, _ in j[7])
            + '</span>')


def a_quay():
    fill = {s[6]: s for s in SEATS}
    rows = ""
    for a, why in J[7]:
        s = fill[a]
        x = meets(s[2], a)
        rows += (f'<div class="row" style="gap:0;height:58px;box-shadow:inset 0 -1px 0 {LINE}">'
                 f'<span style="width:200px">{atag(a, 18, INK, 22)}</span>'
                 f'<span style="flex:1;font-size:16px;color:{SOFT}">{why}</span>'
                 f'<span class="row" style="gap:10px;width:220px;justify-content:flex-end;font-size:15px">{met(x)}'
                 f'<span style="color:{INK if x >= 1 else SOFT}">{s[2]}{"" if x >= 1 else ", half"}</span></span></div>')
    block = (f'<div class="abs" style="left:40px;top:636px;width:1180px;display:flex;flex-direction:column;gap:12px">{giver_line()}'
             f'<div style="display:flex;flex-direction:column;margin-top:4px"><div class="row" style="height:28px;justify-content:space-between">'
             f'<span class="lab">Bring me</span><span class="lab">Your squad</span></div>{rows}</div></div>')
    return quay(f"Rev {REV} A Quay - Seat asks (recommended): the giver asks for three archetypes, each with a reason; your squad answers beside it",
                a_cardmarks, block.replace("top:636px", "top:626px"))


def a_under(v, fills):
    x = meets(v, fills)
    return f'<span style="font-size:15px;color:{SOFT};white-space:nowrap">{arch_names(v)}</span>'


def a_over(i):
    _, _, v, _, _, _, fills = SEATS[i]
    x = meets(v, fills)
    return (f'<div class="abs row" style="left:0;top:34px;gap:10px;padding:6px 12px;background:rgba(0,4,31,.7);'
            f'box-shadow:inset 0 0 0 1px {INK if x >= 1 else LINE}">{met(x)}<span style="font-size:15px">Fills {fills}</span>'
            f'<span style="font-size:14px;color:{SOFT}">{"" if x >= 1 else "half: its second"}</span></div>')


def a_ark():
    return ark(f"Rev {REV} A Ark - Seat asks: each seat shows the ask it fills (a full or half ring); the vessel's archetypes beside its name",
               a_under, a_over)


def a_tabs(on, y=880):
    out = f'<div class="abs row" style="left:40px;right:40px;top:{y}px;justify-content:center;gap:28px">'
    for r, aa in ROLES.items():
        out += f'<span class="row" style="gap:4px;padding:0 10px;box-shadow:inset 0 -1px 0 {LINE}">{rglyph(r, 20, SOFT, 1.6)}'
        for a in aa:
            lit = a == on
            n = len(primary(a))
            col = INK if lit else (SOFT if a in ASK else MUTE)
            out += (f'<button class="pick" type="button" aria-pressed="{str(lit).lower()}" style="height:52px;padding:0 12px;background:none;'
                    f'font-size:15px;color:{col};{"box-shadow:inset 0 -2px 0 " + SEL if lit else ""}">{a}{"" if n else " (none yet)"}</button>')
        out += '</span>'
    return out + '</div>'


def a_vessels():
    firsts = primary(SEAT_ASK)
    w, gap = 520, 80
    x0 = 120
    lines = {v: f'{arch_names(v)}<br>Meets the seat fully. {"The AI can fly it." if v in r135.AI_READY else ""}' for v in firsts}
    floor = "".join(standing(v, x0 + i * (w + gap), w, 260, v == PICKED, lines[v]) for i, v in enumerate(firsts))
    sec = also(SEAT_ASK)
    shelf = (f'<span class="abs lab" style="left:1340px;top:262px">Harraser second or third</span>'
             + "".join(small(v, 1340 + (i % 2) * 280, 300 + (i // 2) * 180, 260, False, v == SEAT_V) for i, v in enumerate(sec)))
    info = (f'<div class="abs row" style="left:120px;top:812px;gap:24px;font-size:15px">'
            f'{atag(SEAT_ASK, 15)}<span style="color:{SOFT}">{DESC[SEAT_ASK]}</span>'
            f'<span style="color:{SOFT}">Counters {", ".join(beats(SEAT_ASK)) or "nothing"}.</span>'
            f'<span style="color:{SOFT}">Countered by {", ".join(beaten_by(SEAT_ASK)) or "nothing"}.</span></div>')
    body = (header(asked_right()) + seat_line("Seat 2  ·  Rook  ·  fills Harraser, half (Squirrel's second)") + FLOOR + floor + shelf + info
            + a_tabs(SEAT_ASK) + vbar(FIT, NEW_FIT, f"Take {PICKED}"))
    return screen(f"Rev {REV} A Vessels - Seat asks: opens on the seat's ask; vessels that lead with it stand on the floor, the ones that have it second beside them",
                window(3, body))


# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# B  Know the crew: the giver names who you will meet and asks for their counters
# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def b_cardmarks(j, on):
    col = INK if on else SOFT
    return f'<span class="row" style="gap:8px">{"".join(rglyph(ROLE_OF[a], 18, col) for a, _ in j[7])}</span>'


def counter_of(a):
    return {"Builder": "Nuke", "Specialist": "Harraser", "Pumper": "Sapper"}[a]


def b_quay():
    rows = ""
    for v, a in RIVAL[1]:
        c = counter_of(a)
        mine = next(s for s in SEATS if s[6] == c)
        x = meets(mine[2], c)
        rows += (f'<div class="row" style="gap:18px;height:68px;box-shadow:inset 0 -1px 0 {LINE}">'
                 f'<span class="row" style="gap:14px;width:360px">{pic(v, 96, 54, False, .9)}<span style="display:flex;flex-direction:column;gap:4px">'
                 f'<span style="font-size:16px">{v}</span>{atag(a, 14, SOFT, 16)}</span></span>'
                 f'<svg width="80" height="20" aria-hidden="true"><path d="M78 10 H6 M14 3 L6 10 L14 17" fill="none" stroke="{SOFT}" stroke-width="1.6"></path></svg>'
                 f'<span style="width:230px">{atag(c, 18, INK, 22)}</span>'
                 f'<span class="row" style="gap:10px;flex:1;justify-content:flex-end;font-size:15px">{met(x)}'
                 f'<span style="color:{INK if x >= 1 else SOFT}">{mine[2]}{"" if x >= 1 else ", half"}</span></span></div>')
    block = (f'<div class="abs" style="left:40px;top:626px;width:1180px;display:flex;flex-direction:column;gap:10px">'
             f'<span class="lab">{J[4]}  ·  {J[5]}</span>'
             f'<span style="font-size:20px;line-height:1.45">"The Rusted Grin are after the same wreck. Bring what beats them."</span>'
             f'<div class="row" style="height:26px;gap:0"><span class="lab" style="width:458px">They fly</span><span class="lab" style="width:330px">Bring</span>'
             f'<span class="lab" style="flex:1;text-align:right">Your squad</span></div>{rows}</div>')
    return quay(f"Rev {REV} B Quay - Know the crew: the giver names the rival crew's archetypes and asks for what counters each",
                b_cardmarks, block)


def b_under(v, fills):
    return atag(ARCH[v][0], 15, INK)


def b_over(i):
    _, _, v, _, _, _, fills = SEATS[i]
    them = next(rv for rv, a in RIVAL[1] if counter_of(a) == fills)
    x = meets(v, fills)
    return (f'<div class="abs" style="left:0;top:34px;display:flex;flex-direction:column;gap:4px;padding:8px 12px;background:rgba(0,4,31,.7)">'
            f'<span class="row" style="gap:10px;font-size:15px">{met(x)}{fills}{"" if x >= 1 else " (half)"} beats their {them}</span></div>')


def b_ark():
    right = (f'<span class="row" style="gap:16px"><span class="lab">They fly</span>'
             + "".join(f'<span class="row" style="gap:8px">{pic(v, 64, 36, False)}<span style="font-size:14px;color:{SOFT}">{a}</span></span>'
                       for v, a in RIVAL[1]) + '</span>')
    return ark(f"Rev {REV} B Ark - Know the crew: each seat names the rival it answers; your vessel shows its first archetype only",
               b_under, b_over, right)


WHEEL = ["Pumper", "Builder", "Burster", "Sapper", "Specialist", "Nuke", "Harraser", "Zoner", "Enhancer"]


def wheel(cx, cy, r, on, lit):
    import math
    pos = {}
    for i, a in enumerate(WHEEL):
        t = -math.pi / 2 + i * 2 * math.pi / 9 + (math.pi / 9) * (i // 3) * 0
        pos[a] = (cx + r * math.cos(t), cy + r * math.sin(t))
    lines = ""
    for x, y in COUNTERS:
        if on not in (x, y):
            continue
        (x1, y1), (x2, y2) = pos[x], pos[y]
        dx, dy = x2 - x1, y2 - y1
        d = math.hypot(dx, dy)
        x2, y2 = x2 - dx / d * 34, y2 - dy / d * 34
        x1, y1 = x1 + dx / d * 34, y1 + dy / d * 34
        col = INK if x == on else RED2
        lines += f'<path d="M{x1:.0f},{y1:.0f} L{x2:.0f},{y2:.0f}" stroke="{col}" stroke-width="1.6" fill="none" marker-end="url(#wa{"i" if x == on else "r"})"></path>'
    svg = (f'<svg class="abs" style="left:0;top:0" width="1920" height="1080" aria-hidden="true"><defs>'
           f'<marker id="wai" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M0,0 L10,5 L0,10 z" fill="{INK}"></path></marker>'
           f'<marker id="war" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M0,0 L10,5 L0,10 z" fill="{RED2}"></path></marker>'
           f'</defs><circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="{LINE}" stroke-width="1"></circle>{lines}</svg>')
    nodes = ""
    for a, (x, y) in pos.items():
        is_on = a == on
        col = INK if (is_on or a in lit) else MUTE
        ring = f"box-shadow:inset 0 0 0 {2 if is_on else 1}px {SEL if is_on else (INK if a in lit else LINE)}"
        nodes += (f'<button class="pick abs" type="button" aria-pressed="{str(is_on).lower()}" style="left:{x - 30:.0f}px;top:{y - 30:.0f}px;width:60px;height:60px;'
                  f'border-radius:50%;background:#00041F;{ring};display:flex;align-items:center;justify-content:center">{rglyph(ROLE_OF[a], 24, col, 1.6)}</button>'
                  f'<span class="abs" style="left:{x:.0f}px;top:{y + 36:.0f}px;transform:translateX(-50%);font-size:14px;color:{col};white-space:nowrap">{a}</span>')
    return svg + nodes


def b_vessels():
    on = SEAT_ASK
    firsts = primary(on)
    w, gap = 440, 70
    lines = {v: f'{arch_names(v)}<br>Beats their Rhino (Specialist).' for v in firsts}
    floor = "".join(standing(v, 80 + i * (w + gap), w, 280, v == PICKED, lines[v]) for i, v in enumerate(firsts))
    sec = also(on)
    shelf = (f'<span class="abs lab" style="left:80px;top:720px">Harraser second or third</span>'
             f'<span class="abs" style="left:80px;top:752px;font-size:15px;color:{SOFT}">{"  ·  ".join(sec)}</span>')
    side = (f'<div class="abs" style="left:1180px;top:262px;width:700px;height:620px;box-shadow:inset 1px 0 0 {LINE}"></div>'
            + wheel(1530, 560, 230, on, [b for b in beats(on)] + beaten_by(on))
            + f'<div class="abs" style="left:1220px;top:268px;display:flex;flex-direction:column;gap:6px">'
              f'{atag(on, 18, INK, 22)}<span style="font-size:14px;color:{SOFT};max-width:420px;line-height:1.4">{DESC[on]}</span></div>'
            + f'<div class="abs" style="left:1220px;top:850px;display:flex;gap:24px;font-size:14px;color:{SOFT}">'
              f'<span class="row" style="gap:8px"><span style="width:24px;height:2px;background:{INK}"></span>counters</span>'
              f'<span class="row" style="gap:8px"><span style="width:24px;height:2px;background:{RED2}"></span>countered by</span>'
              f'<span>Press an archetype to put its vessels on the floor</span></div>')
    body = (header(asked_right()) + seat_line("Seat 2  ·  Rook  ·  answers their Rhino (Specialist) with a Harraser") + FLOOR + floor + shelf + side
            + vbar(FIT, NEW_FIT, f"Take {PICKED}"))
    return screen(f"Rev {REV} B Vessels - Know the crew: the counter wheel picks the archetype; its vessels stand on the floor",
                window(3, body))


# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
# C  Plain words: the ask as a sentence, archetypes as words, Vessels as a grouped list
# ════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
def c_cardmarks(j, on):
    col = INK if on else SOFT
    return f'<span style="font-size:14px;color:{col};line-height:1.4">{", ".join(a for a, _ in j[7])}</span>'


def c_quay():
    block = (f'<div class="abs" style="left:40px;top:636px;width:1180px;display:flex;flex-direction:column;gap:14px">{giver_line()}'
             f'<span style="font-size:20px;line-height:1.5;margin-top:6px">Bring me a <span style="color:{INK};box-shadow:inset 0 -1px 0 {INK}">Nuke</span>, '
             f'a <span style="box-shadow:inset 0 -1px 0 {INK}">Sapper</span> and a <span style="box-shadow:inset 0 -1px 0 {INK}">Harraser</span>.</span>'
             f'<span style="font-size:15px;color:{SOFT}">Dolphin is a Nuke, Sparrow is a Sapper. Squirrel is a Harraser only second; a Starling or Jacks leads with it.</span></div>')
    return quay(f"Rev {REV} C Quay - Plain words: the ask is one sentence and the crew's fit one line under it; the cards list the archetypes",
                c_cardmarks, block)


def c_under(v, fills):
    return ""


def c_over(i):
    _, _, v, _, _, _, fills = SEATS[i]
    x = meets(v, fills)
    aa = ARCH[v]
    return (f'<div class="abs" style="left:0;right:0;top:640px;display:flex;flex-direction:column;gap:6px">'
            f'<span style="font-size:15px">{"The " + fills if x >= 1 else "Half the " + fills} for this voyage</span>'
            f'<span style="font-size:14px;color:{SOFT}">{aa[0]} first{", then " + ", ".join(aa[1:]) if aa[1:] else ""}</span></div>')


def c_ark():
    html = ark(f"Rev {REV} C Ark - Plain words: under each seat, the ask it fills in words and the vessel's archetypes in order",
               c_under, c_over, asked_right("words"))
    return html


def c_list(x, y, on):
    out = f'<div class="abs" style="left:{x}px;top:{y}px;width:340px;display:flex;flex-direction:column;gap:2px">'
    for r, aa in ROLES.items():
        out += (f'<span class="row" style="gap:10px;height:44px;margin-top:10px">{rglyph(r, 18, SOFT, 1.6)}<span class="lab">{r}</span>'
                f'<span style="font-size:13px;color:{MUTE}">{ROLE_GOAL[r]}</span></span>')
        for a in aa:
            lit = a == on
            n = len(primary(a))
            col = INK if lit else (INK if a in ASK else SOFT) if n else MUTE
            out += (f'<button class="pick row" type="button" aria-pressed="{str(lit).lower()}" style="height:44px;padding:0 14px;background:{"rgba(117,143,251,.08)" if lit else "none"};'
                    f'justify-content:space-between;{"box-shadow:inset 2px 0 0 " + SEL if lit else ""}"><span style="font-size:16px;color:{col}">{a}'
                    f'{"  ·  asked" if a in ASK else ""}</span><span style="font-size:14px;color:{MUTE}">{n or "none yet"}</span></button>')
    return out + '</div>'


def c_vessels():
    on = SEAT_ASK
    firsts = primary(on)
    w, gap = 560, 80
    lines = {v: f'{arch_names(v)}' for v in firsts}
    floor = "".join(standing(v, 480 + i * (w + gap), w, 270, v == PICKED, lines[v]) for i, v in enumerate(firsts))
    info = (f'<div class="abs" style="left:480px;top:740px;width:1200px;display:flex;flex-direction:column;gap:8px">'
            f'<span style="font-size:16px">{on}  ·  {DESC[on]}</span>'
            f'<span style="font-size:15px;color:{SOFT}">Counters {", ".join(beats(on))}. Also has it second: {", ".join(also(on))}.</span></div>')
    body = (header(asked_right("words")) + seat_line("Seat 2  ·  Rook  ·  half the Harraser (Squirrel has it second)") + FLOOR
            + c_list(40, 250, on) + floor + info + vbar(FIT, NEW_FIT, f"Take {PICKED}"))
    return screen(f"Rev {REV} C Vessels - Plain words: the nine archetypes as a list under their roles, the picked one's vessels on the floor",
                window(3, body))


# ── Compare and notes ───────────────────────────────────────────────────────────────────────────────────────────────
def compare():
    cols = [("A  Seat asks (recommended)", [
                ("On the quay", "Three archetypes, each with the giver's reason, and which of your vessels meets it (full or half ring)."),
                ("On the job card", "The three archetypes, one under the other."),
                ("On Your Ark", "Above each hull: the ask it fills. Beside the name: its archetypes, first one bright."),
                ("In Vessels", "Opens on the seat's ask. Vessels that lead with it on the floor; those with it second beside. Nine tabs under three roles."),
                ("Counters", "One line under the floor: what the archetype counters and what counters it.")]),
            ("B  Know the crew", [
                ("On the quay", "The rival crew's three vessels and their archetypes; beside each, the archetype that beats it."),
                ("On the job card", "Three role glyphs, as today."),
                ("On Your Ark", "Above each hull: which rival it answers. Beside the name: its first archetype only."),
                ("In Vessels", "A counter wheel of the nine archetypes picks what stands on the floor; arrows for what it beats and what beats it."),
                ("Counters", "The point of the screens: every ask is a counter.")]),
            ("C  Plain words", [
                ("On the quay", "One sentence: Bring me a Nuke, a Sapper and a Harraser. One line on how your squad fits."),
                ("On the job card", "The archetypes as words."),
                ("On Your Ark", "Under each pilot: the ask it fills in words, then the vessel's archetypes in order."),
                ("In Vessels", "A list of the nine under their roles, with how many vessels lead with each; the picked one's vessels on the floor."),
                ("Counters", "A sentence under the floor.")])]
    out = ""
    for i, (head, rows) in enumerate(cols):
        x = 60 + i * 610
        out += (f'<div class="abs" style="left:{x}px;top:150px;width:570px;display:flex;flex-direction:column;gap:20px">'
                f'<span class="hd" style="font-size:22px">{head}</span>'
                + "".join(f'<div style="display:flex;flex-direction:column;gap:6px;padding-bottom:14px;box-shadow:inset 0 -1px 0 {LINE}">'
                          f'<span class="lab">{a}</span><span style="font-size:16px;line-height:1.45;color:{SOFT}">{b}</span></div>' for a, b in rows)
                + '</div>')
    key = (f'<div class="abs" style="left:60px;top:840px;width:1800px;display:flex;gap:60px">'
           + "".join(f'<div style="display:flex;flex-direction:column;gap:10px"><span class="row" style="gap:10px">{rglyph(r, 24)}'
                     f'<span style="font-size:18px">{r}</span><span style="font-size:14px;color:{SOFT}">{ROLE_GOAL[r]}</span></span>'
                     f'<span style="font-size:15px;color:{SOFT}">{"  ·  ".join(aa)}</span></div>' for r, aa in ROLES.items())
           + f'<div style="display:flex;flex-direction:column;gap:10px"><span style="font-size:18px">Meets an ask</span>'
             f'<span class="row" style="gap:18px;font-size:15px;color:{SOFT}"><span class="row" style="gap:8px">{met(1)}first</span>'
             f'<span class="row" style="gap:8px">{met(.5)}second or third</span><span class="row" style="gap:8px">{met(0)}no</span></span></div></div>')
    return page(f"Rev {REV} Compare - where the roles show, three ways",
                f'<span class="abs hd" style="left:60px;top:60px;font-size:32px">Roles on the Voyages screens, three ways</span>' + out + key)


def notes():
    no_lead = [a for r in ROLES.values() for a in r if not primary(a)]
    rows = [
        ("Asked", "Redesign the Voyages UI wherever roles touch it: Vessels, the seat picker, the class tags and the givers' "
                  "recommendations. The roles board is final (Cultivator, Destroyer, Guardian; nine archetypes)."),
        ("Kept", "The picked Rev 138 screens: the quay with five jobs (the Crystal Run as it is), Your Ark's seats, Vessels E's floor. "
                 "Only what the seven classes did is redrawn. Difficulty is 1-9 (Rev 138's four-step sea bar is gone)."),
        ("The ask", "From the gameplay thread: three seats, each one archetype. Salvage asks Nuke, Sapper, Harraser; Escort Enhancer, "
                    "Harraser, Nuke; Bounty Harraser, Specialist, Sapper; Hold the Core Pumper, Burster, Sapper. A vessel meets a seat "
                    "fully on its first archetype, half on its second or third. Squad fit is 0 to 3 in halves (Sparrow, Squirrel, "
                    f"Dolphin: {FIT:g} of 3). The Crystal Run asks for no one."),
        ("Recommended: A", "The ask stays three plain words with a reason each, and every screen answers the same question: which seat "
                           "fills which ask, fully or half. B is the richest but puts a rival crew on every job, which only Bounty and "
                           "the rival voyages have. C is the quietest but hides the half fit."),
        ("Glyphs", "Only the three role glyphs (the sprout, the star and the shield of Rev 133). Archetypes are words; nine more "
                   "glyphs would be noise."),
        ("Open", f"No vessel leads with {', '.join(no_lead)}. No starter leads with a Guardian archetype. "
                 "Starters (Squirrel, Sparrow, Dolphin, Thresher, Tiger Moth, Swordfish) carry no mark here: say if they should. "
                 "Tiger Moth still has no hangar preview."),
    ]
    html = r133.notes_page("notes: roles on the Voyages screens", rows)
    return html.replace("height: 1080px", "height: 900px").replace('"height":1080', '"height":900')


BOARDS = {
    "A": lambda: tall(f"Rev {REV} A - Seat asks (recommended): the giver asks for three archetypes with a reason each; each seat shows the ask it fills; "
                      "Vessels opens on that ask. Quay, Your Ark, Vessels", [a_quay(), a_ark(), a_vessels()]),
    "B": lambda: tall(f"Rev {REV} B - Know the crew: the giver names the rival crew's archetypes and asks for their counters; Vessels picks by a counter wheel. "
                      "Quay, Your Ark, Vessels", [b_quay(), b_ark(), b_vessels()]),
    "C": lambda: tall(f"Rev {REV} C - Plain words: the ask as one sentence, archetypes as words, Vessels as a grouped list. Quay, Your Ark, Vessels",
                      [c_quay(), c_ark(), c_vessels()]),
}

if __name__ == "__main__":
    for key, fn in BOARDS.items():
        (ROOT / f"R{REV}{key}.dc.html").write_text(fn(), encoding="utf-8")
    if "--with-notes" in sys.argv:        # the canvas had 4 free files on 2026-11-26: notes and compare stay in the doc
        (ROOT / f"R{REV}Compare.dc.html").write_text(compare(), encoding="utf-8")
        (ROOT / f"R{REV}Notes.dc.html").write_text(notes(), encoding="utf-8")
    print("wrote", len(BOARDS), "boards to", ROOT)
