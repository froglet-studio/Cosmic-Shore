#!/usr/bin/env python3
"""Rev 83 boards: the seat editor of the picked B5, redrawn in the game's own look (Frogma tab "Voyages",
Docs/MENU_REDESIGN_B.md section 164).

Shombith, 2026-11-09, on Rev 82: "Bro it looks soooo bad the selection UI, make it modern sleek and with our cosmic
shore UI, what even is this, mockups for this now". Rev 82's editor was a black box of outlined squares. These three
are drawn from the style guide (Docs/UI_STYLE_GUIDE.md sections 4-7: the corner sliver, filled plate cards that say
their state, Jade for the thing you picked, popups on #00010A with a light 1-unit border and their buttons straddling
the foot) and from the screen Shombith picked as modern, Rev 54 A's Showroom (the choice as the hero, its name large,
a slim strip of options with the picked one marked by a Jade line and a check). All three are playable in the B5
window and do what Rev 82 did (one press picks, the card behind changes at once, the pilot swap, the seats you may
not change).

  S1  Showroom   the current choice is the hero - its render large, its name in big type, its own description - and
                 the options are a slim strip along the foot (recommended)
  S2  Hangar bay the choice stands large on a lit podium, the options a sleek list beside it
  S3  Seat sheet no overlay: the seat's own card opens into a sheet with all four choices as rows, one press each

The hull lines are the game's own (SO_Class_<hull>.asset Description). The artifacts are the game's
(Assets/_SO_Assets/Pilots/Artifacts). The four pilots are the canvas's portraits and carry no invented biography.

    python3 Docs/MenuRedesignB/rounds/r83_voyages.py <scratch root> [rev]
"""
import copy
import importlib.util
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "83"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r82", HERE / "r82_voyages.py")
r82 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r82)
r81, r80, r71 = r82.r81, r82.r80, r82.r71

REV, ROOT, page, nav, GROUND = int(_rev), r82.ROOT, r80.page, r80.nav, r80.GROUND
HULL, PILOT = r71.HULL, r80.PILOT
PX, PW, MAP_W = r81.PX, r81.PW, r81.MAP_W
JADE = "#21C0BD"

DESC = {   # SO_Class_<hull>.asset Description, as the game ships it
    "Sparrow": "The classic arcade flyer. Use your Pulsefire Cannons and Skyburst Rockets to destroy enemy blocks, flora, fauna, structures, everything...",
    "Dolphin": "Thread the narrowest needles at the highest speeds to build max charge, then drift sideways into a crystal to release one of the biggest blasts in the Hypersea.",
    "Squirrel": "Get in your flow and stay there. Hard decisions are for other classes. You’re here to bounce around and ride your speed tubes, leaving a wake of creation and destruction.",
    "Rhino": "A plough. Your hull shoves every prism it hits aside, and a shoved prism that hits another breaks both. Pull a trigger to throw the furrow that way, and run flat out to wind up the ramp.",
    "Manta": "Soar at high speed through open spaces to create the biggest blocks in the Hypersea, or skim your opponents trail to steal theirs.",
    "Serpent": "Navigate with just one thumb and place seeds that will steal ALL local blocks into a wall that can be sheilded. Use walls to disrupt enemy volume and navigation.",
}
ELEMENTS = ["Charge", "Mass", "Space", "Time"]
ARTIFACTS = ["No artifact", "Aegis", "Aftershock", "Hydra", "Javelin"]   # Assets/_SO_Assets/Pilots/Artifacts

SQUADS = copy.deepcopy(r80.SQUADS)
for squad, arts in zip(SQUADS, (["Aegis", "Hydra", "No artifact"], ["Javelin", "Aftershock", "No artifact"])):
    for seat, a in zip(squad["seats"], arts):
        seat["a"] = a

DATA = ("const V = " + json.dumps(r71.VOYAGES) + ";\nconst SQ = " + json.dumps(SQUADS) + ";\nconst HULL = " + json.dumps(HULL)
        + ";\nconst PILOT = " + json.dumps(PILOT) + ";\nconst DESC = " + json.dumps(DESC) + ";\nconst ELEMENTS = " + json.dumps(ELEMENTS)
        + ";\nconst ARTIFACTS = " + json.dumps(ARTIFACTS) + ";\n")

# Rev 82's logic, with the seat being edited marked in Jade, and the editor's hero: the current choice of the open tab.
LOGIC = r82.LOGIC.replace(r82.DATA, DATA)
LOGIC = LOGIC.replace("edge: st.open === i ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89'",
                      "edge: st.open === i ? 'inset 0 0 0 2px " + JADE + "' : 'inset 0 0 0 1px #434C89'")
LOGIC = LOGIC.replace("const tile = (on) => on ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89';",
                      "const tile = (on) => on ? 'inset 0 3px 0 " + JADE + "' : 'none';")
assert "inset 0 3px 0 " + JADE in LOGIC
HERO = """
  if (ed.open) {
    const s = seats[st.open], kinds = [Object.keys(HULL), Object.keys(PILOT), ELEMENTS, ARTIFACTS], cur = [s.v, s.p, s.e, s.a][st.tab];
    const list = kinds[st.tab], at = list.indexOf(cur), pickAt = (k) => [ed.hulls, ed.pilots, ed.els, ed.arts][st.tab][k].pick;
    ed.hero = { name: cur, kind: ['Vessel', 'Pilot', 'Element', 'Artifact'][st.tab], count: (at + 1) + ' of ' + list.length,
      img: st.tab === 0 ? HULL[cur] : st.tab === 1 ? PILOT[cur] : '', isHull: st.tab === 0, isFace: st.tab === 1, isWord: st.tab > 1,
      line: st.tab === 0 ? DESC[cur] : st.tab === 1 ? 'Flies the ' + s.v + ' for ' + q.name : st.tab === 2 ? 'The element this seat flies with' : (cur === 'No artifact' ? 'This seat carries no artifact' : 'Carried by the ' + s.v),
      prev: pickAt((at - 1 + list.length) % list.length), next: pickAt((at + 1) % list.length) };
    [ed.hulls, ed.pilots, ed.els, ed.arts].forEach((xs) => xs.forEach((x) => { x.mark = x.pressed === 'true' ? '\\u2713' : ''; }));
    ed.sheet = [['Vessel', ed.hulls, 0], ['Pilot', ed.pilots, 1], ['Element', ed.els, 2], ['Artifact', ed.arts, 3]].map((r) => ({ name: r[0], items: r[1], isImg: r[2] < 2, isFace: r[2] === 1 }));
  }
  return { voyage, vlist, tabs, presets, cards, ed };"""
LOGIC = LOGIC.replace("\n  return { voyage, vlist, tabs, presets, cards, ed };", HERO)
LOGIC = LOGIC.replace("let ed = { open: false,", "let ed = { hero: { name: '', kind: '', count: '', img: '', isHull: false, isFace: false, isWord: false, line: '', prev: () => {}, next: () => {} }, sheet: [], open: false,")
assert "ed.hero = " in LOGIC and "sheet: [], open: false" in LOGIC


def panel(cards_html):
    return f"""<section class="abs" style="left:{PX}px;top:244px;width:{PW}px;height:796px;display:flex;flex-direction:column">
<span style="font-size:28px;line-height:1.25">{{{{voyage.title}}}}</span>
<span style="font-size:18px;line-height:1.5;color:#E6E9FF;margin-top:12px">{{{{voyage.line}}}}</span>
<div style="margin-top:22px;display:grid;grid-template-columns:repeat(3,1fr);gap:16px"><div style="display:flex;flex-direction:column;gap:6px"><span class="lab">Distance</span><span class="num" style="font-size:28px">{{{{voyage.ly}}}}</span></div><div style="display:flex;flex-direction:column;gap:6px"><span class="lab">Sail</span><span class="num" style="font-size:28px">{{{{voyage.min}}}}</span></div><div style="display:flex;flex-direction:column;gap:6px"><span class="lab">Intensity</span><span class="num" style="font-size:28px">{{{{voyage.i}}}}</span></div></div>
<div style="display:flex;align-items:center;gap:10px;margin-top:auto"><span class="lab">Squad</span><sc-for list="{{{{presets}}}}" as="p" hint-placeholder-count="2"><button class="pick sls" type="button" aria-pressed="{{{{p.pressed}}}}" onClick="{{{{p.pick}}}}" style="padding:10px 16px;font-size:15px;color:{{{{p.col}}}};box-shadow:{{{{p.edge}}}}">{{{{p.name}}}}</button></sc-for></div>
{cards_html}
<a class="btn cta" href="#" style="margin-top:18px;width:100%"><span class="kc">A</span>Set sail</a>
</section>"""


CARDS = ('<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:14px">'
         '<sc-for list="{{cards}}" as="s" hint-placeholder-count="3"><button class="pick sl" type="button" aria-pressed="{{s.pressed}}" '
         'aria-label="{{s.alt}}" onClick="{{s.pick}}" style="height:330px;display:flex;flex-direction:column;align-items:center;'
         'justify-content:flex-end;gap:6px;padding:0 8px 16px;opacity:{{s.op}};background:linear-gradient(180deg,rgba(34,38,69,.35),'
         'rgba(34,38,69,.92));box-shadow:{{s.edge}}"><img src="{{s.face}}" alt="" style="width:100%;height:150px;object-fit:contain;'
         'object-position:50% 100%"><img class="hull" src="{{s.hull}}" alt="" style="width:100%;height:82px">'
         '<span style="font-size:18px">{{s.name}}</span><span style="font-size:15px;line-height:1.5;color:#E6E9FF;text-align:center">'
         '{{s.line1}}<br>{{s.line2}}</span></button></sc-for></div>')

TABS = ('<div style="display:flex;gap:32px"><sc-for list="{{ed.tabs}}" as="k" hint-placeholder-count="4"><button class="pick cap" type="button" '
        'aria-pressed="{{k.pressed}}" onClick="{{k.pick}}" style="font-size:18px;color:{{k.col}};padding-bottom:10px;'
        'border-bottom:2px solid {{k.line}}">{{k.name}}</button></sc-for></div>')

DONE = ('<button class="btn def abs" type="button" onClick="{{ed.done}}" style="{pos}"><span class="kc">B</span>Done</button>')

# The strip of options for the open tab (S1): small sliver tiles on the plate, the picked one with a Jade top line and a check.
STRIP_TILE = ('<button class="pick sls" type="button" aria-label="{{{{t.name}}}}" aria-pressed="{{{{t.pressed}}}}" onClick="{{{{t.pick}}}}" '
              'style="position:relative;height:{h}px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:8px;'
              'padding-bottom:12px;background:rgba(34,38,69,.72);box-shadow:{{{{t.edge}}}}">{inner}<span class="cap" style="font-size:14px;'
              'color:#E6E9FF">{{{{t.name}}}} <span style="color:' + JADE + '">{{{{t.mark}}}}</span></span></button>')


def strip(h):
    img_h = STRIP_TILE.format(h=h, inner='<img class="hull" src="{{t.img}}" alt="" style="width:84%;height:' + str(h - 54) + 'px">')
    img_p = STRIP_TILE.format(h=h, inner='<img src="{{t.img}}" alt="" style="width:100%;height:' + str(h - 44) + 'px;object-fit:contain;object-position:50% 100%">')
    word = STRIP_TILE.format(h=h, inner='')
    return ('<sc-if value="{{ed.isV}}" hint-placeholder-val="{{ true }}"><div style="display:grid;grid-template-columns:repeat(6,1fr);gap:10px">'
            '<sc-for list="{{ed.hulls}}" as="t" hint-placeholder-count="6">' + img_h + '</sc-for></div></sc-if>'
            '<sc-if value="{{ed.isP}}" hint-placeholder-val="{{ false }}"><div style="display:grid;grid-template-columns:repeat(4,1fr);gap:10px">'
            '<sc-for list="{{ed.pilots}}" as="t" hint-placeholder-count="4">' + img_p + '</sc-for></div></sc-if>'
            '<sc-if value="{{ed.isE}}" hint-placeholder-val="{{ false }}"><div style="display:grid;grid-template-columns:repeat(4,1fr);gap:10px">'
            '<sc-for list="{{ed.els}}" as="t" hint-placeholder-count="4">' + word.replace(f"height:{h}px", "height:72px") + '</sc-for></div></sc-if>'
            '<sc-if value="{{ed.isA}}" hint-placeholder-val="{{ false }}"><div style="display:grid;grid-template-columns:repeat(5,1fr);gap:10px">'
            '<sc-for list="{{ed.arts}}" as="t" hint-placeholder-count="5">' + word.replace(f"height:{h}px", "height:72px") + '</sc-for></div></sc-if>')


def hero(x_img, w_img, h_img, name_size=48):
    return (f'<sc-if value="{{{{ed.hero.isHull}}}}" hint-placeholder-val="{{{{ true }}}}"><img class="hull abs" src="{{{{ed.hero.img}}}}" alt="" '
            f'style="{x_img};width:{w_img}px;height:{h_img}px"></sc-if>'
            f'<sc-if value="{{{{ed.hero.isFace}}}}" hint-placeholder-val="{{{{ false }}}}"><img class="abs" src="{{{{ed.hero.img}}}}" alt="" '
            f'style="{x_img};width:{w_img}px;height:{h_img}px;object-fit:contain;object-position:50% 100%"></sc-if>')


def window(body, title):
    return page(title, GROUND + nav(crystals=False) + r82.r81.r80.r79.header() + body, LOGIC)


# ── S1: Showroom ──────────────────────────────────────────────────────────────────────────────────────────────────────

def s1_board():
    ed = f"""<sc-if value="{{{{ed.open}}}}" hint-placeholder-val="{{{{ false }}}}"><div class="abs" style="left:40px;top:236px;width:1240px;height:804px;z-index:9">
<div class="abs sl" style="inset:0;background:radial-gradient(ellipse 60% 55% at 62% 42%,rgba(117,143,251,.22),rgba(117,143,251,0) 70%),#00010A;box-shadow:inset 0 0 0 1px #E6E9FF"></div>
<div class="abs" style="left:40px;right:40px;top:32px;display:flex;align-items:flex-end;justify-content:space-between"><span class="lab" style="color:#E6E9FF">{{{{ed.title}}}}</span>{TABS}</div>
{hero("right:60px;top:110px", 660, 400)}
<div class="abs" style="left:40px;top:150px;width:520px;display:flex;flex-direction:column;gap:14px"><span class="lab">{{{{ed.hero.kind}}}}  {{{{ed.hero.count}}}}</span><span style="font-size:48px;line-height:1.05">{{{{ed.hero.name}}}}</span><span style="font-size:18px;line-height:1.55;color:#E6E9FF">{{{{ed.hero.line}}}}</span></div>
<div class="abs" style="left:40px;right:40px;bottom:64px">{strip(150)}</div>
{DONE.replace("{pos}", "right:40px;bottom:-24px")}
</div></sc-if>"""
    return window(r81.left() + panel(CARDS) + ed,
                  f"Rev {REV} S1 - Showroom: the choice as the hero, its render and name large, the options a slim strip along the foot (recommended) - interactive")


# ── S2: Hangar bay ────────────────────────────────────────────────────────────────────────────────────────────────────

def s2_board():
    row = ('<button class="pick sls" type="button" aria-label="{{{{t.name}}}}" aria-pressed="{{{{t.pressed}}}}" onClick="{{{{t.pick}}}}" '
           'style="height:{h}px;display:grid;grid-template-columns:{c};align-items:center;gap:16px;padding:0 18px;'
           'background:rgba(34,38,69,.72);box-shadow:{{{{t.edge}}}}">{inner}<span style="font-size:18px;color:#E6E9FF;text-align:left">'
           '{{{{t.name}}}}</span><span class="cap" style="font-size:14px;color:' + JADE + '">{{{{t.mark}}}}</span></button>')
    lists = ('<sc-if value="{{ed.isV}}" hint-placeholder-val="{{ true }}"><div style="display:flex;flex-direction:column;gap:8px">'
             '<sc-for list="{{ed.hulls}}" as="t" hint-placeholder-count="6">'
             + row.format(h=84, c="120px 1fr 24px", inner='<img class="hull" src="{{t.img}}" alt="" style="width:120px;height:62px">') + '</sc-for></div></sc-if>'
             '<sc-if value="{{ed.isP}}" hint-placeholder-val="{{ false }}"><div style="display:flex;flex-direction:column;gap:8px">'
             '<sc-for list="{{ed.pilots}}" as="t" hint-placeholder-count="4">'
             + row.format(h=104, c="72px 1fr 24px", inner='<img src="{{t.img}}" alt="" style="width:72px;height:92px;object-fit:cover;object-position:50% 16%">') + '</sc-for></div></sc-if>'
             '<sc-if value="{{ed.isE}}" hint-placeholder-val="{{ false }}"><div style="display:flex;flex-direction:column;gap:8px">'
             '<sc-for list="{{ed.els}}" as="t" hint-placeholder-count="4">' + row.format(h=72, c="1fr 24px", inner='') + '</sc-for></div></sc-if>'
             '<sc-if value="{{ed.isA}}" hint-placeholder-val="{{ false }}"><div style="display:flex;flex-direction:column;gap:8px">'
             '<sc-for list="{{ed.arts}}" as="t" hint-placeholder-count="5">' + row.format(h=72, c="1fr 24px", inner='') + '</sc-for></div></sc-if>')
    ed = f"""<sc-if value="{{{{ed.open}}}}" hint-placeholder-val="{{{{ false }}}}"><div class="abs" style="left:40px;top:236px;width:1240px;height:804px;z-index:9">
<div class="abs sl" style="inset:0;background:radial-gradient(ellipse 44% 16% at 36% 78%,rgba(33,192,189,.18),rgba(33,192,189,0) 70%),radial-gradient(ellipse 50% 50% at 36% 46%,rgba(117,143,251,.16),rgba(117,143,251,0) 70%),#00010A;box-shadow:inset 0 0 0 1px #E6E9FF"></div>
<div class="abs" style="left:40px;top:32px;width:760px;display:flex;flex-direction:column;gap:22px"><span class="lab" style="color:#E6E9FF">{{{{ed.title}}}}</span>{TABS}</div>
{hero("left:80px;top:170px", 660, 440)}
<div class="abs" style="left:40px;bottom:56px;width:760px;display:flex;flex-direction:column;gap:10px"><span style="font-size:36px">{{{{ed.hero.name}}}}</span><span style="font-size:18px;line-height:1.5;color:#E6E9FF">{{{{ed.hero.line}}}}</span></div>
<div class="abs" style="left:840px;top:32px;width:360px;bottom:56px;overflow:hidden">{lists}</div>
{DONE.replace("{pos}", "right:40px;bottom:-24px")}
</div></sc-if>"""
    return window(r81.left() + panel(CARDS) + ed,
                  f"Rev {REV} S2 - Hangar bay: the choice large on a lit podium, the options a sleek list beside it - interactive")


# ── S3: Seat sheet ────────────────────────────────────────────────────────────────────────────────────────────────────

def s3_board():
    chip = ('<button class="pick sls" type="button" aria-label="{{{{t.name}}}}" aria-pressed="{{{{t.pressed}}}}" onClick="{{{{t.pick}}}}" '
            'style="min-width:0;height:{h}px;display:flex;flex-direction:column;align-items:center;justify-content:{j};gap:4px;padding:0 4px {pb}px;'
            'background:rgba(34,38,69,.72);box-shadow:{{{{t.edge}}}}">{inner}<span style="max-width:100%;font-size:15px;color:#E6E9FF;white-space:nowrap">{{{{t.name}}}}</span></button>')
    row = lambda name, lst, cols, h, inner: (
        f'<div style="display:flex;flex-direction:column;gap:8px"><span class="lab">{name}</span>'
        f'<div style="display:grid;grid-template-columns:repeat({cols},1fr);gap:8px"><sc-for list="{{{{{lst}}}}}" as="t" hint-placeholder-count="{cols}">'
        + chip.format(h=h, inner=inner, j='flex-end' if inner else 'center', pb=8 if inner else 0) + '</sc-for></div></div>')
    sheet = ('<div style="display:flex;flex-direction:column;gap:18px;margin-top:18px">'
             + row("Vessel", "ed.hulls", 6, 108, '<img class="hull" src="{{t.img}}" alt="" style="width:92%;height:58px">')
             + row("Pilot", "ed.pilots", 4, 150, '<img src="{{t.img}}" alt="" style="width:100%;height:114px;object-fit:contain;object-position:50% 100%">')
             + row("Element", "ed.els", 4, 56, '')
             + row("Artifact", "ed.arts", 5, 56, '') + '</div>')
    # The seat being changed takes the panel: its pilot and hull large at the head, then all four choices at once.
    ed = f"""<sc-if value="{{{{ed.open}}}}" hint-placeholder-val="{{{{ false }}}}"><div class="abs" style="left:{PX - 16}px;top:236px;width:{PW + 32}px;height:804px;z-index:9">
<div class="abs sl" style="inset:0;background:linear-gradient(180deg,#222645,#00010A 46%);box-shadow:inset 0 0 0 2px {JADE}"></div>
<div class="abs" style="left:28px;right:28px;top:24px;bottom:56px;display:flex;flex-direction:column">
<div style="display:grid;grid-template-columns:150px 1fr;gap:18px;align-items:end"><img src="{{{{ed.hero.img}}}}" alt="" style="width:150px;height:150px;object-fit:contain;object-position:50% 100%"><div style="display:flex;flex-direction:column;gap:8px"><span class="lab" style="color:#E6E9FF">{{{{ed.title}}}}</span><span style="font-size:36px;line-height:1.05">{{{{ed.hero.name}}}}</span></div></div>
{sheet}
</div>
{DONE.replace("{pos}", "right:28px;bottom:-24px")}
</div></sc-if>"""
    return page(f"Rev {REV} S3 - Seat sheet: the seat's card opens into a sheet with all four choices as rows - interactive",
                GROUND + nav(crystals=False) + r82.r81.r80.r79.header() + r81.left() + panel(CARDS) + ed,
                LOGIC.replace("const s = seats[st.open], kinds", "const s = seats[st.open], kinds").replace(
                    "img: st.tab === 0 ? HULL[cur] : st.tab === 1 ? PILOT[cur] : ''", "img: st.tab === 1 ? PILOT[cur] : PILOT[s.p]").replace(
                    "name: cur, kind:", "name: s.v + ' - ' + s.p, kind:"))


def compare_board():
    """Rev 82's editor as it was, opened on the first seat, so the three can be read against it."""
    html = r82.board().replace("open: -1, tab: 0 }", "open: 0, tab: 0 }")
    assert "open: 0, tab: 0 }" in html
    return re.sub(r"<title>[^<]*</title>", f"<title>Rev {REV} - Rev 82 Squad: the outlined-square editor this replaces</title>", html, count=1)


def notes_board():
    rows = [
        ("Asked", "\"Bro it looks soooo bad the selection UI, make it modern sleek and with our cosmic shore UI, what even is this, mockups for this now\" (Shombith, 2026-11-09, on Rev 82)."),
        ("Drawn from", "The style guide: the corner sliver, filled plate tiles, Jade for the picked thing with a check, the popup on #00010A with a 1-unit light border and its button straddling the foot. And Rev 54 A, the Showroom picked as modern: the choice as the hero, a slim strip of options."),
        ("S1 Showroom (recommended)", "The current choice is the hero: the hull render large with its own description, or the pilot full length. The options are a strip along the foot; one press picks, and the hero and the seat card change at once."),
        ("S2 Hangar bay", "The choice stands large on a lit podium with its name and line; the options are a list of sleek rows beside it, each with its render."),
        ("S3 Seat sheet", "No tabs: the seat's own card opens into a sheet with Vessel, Pilot, Element and Artifact as four rows at once. Four presses change everything."),
        ("Kept from Rev 82", "One press picks; the pilot swap; the seats you may not change; the squad chips; the window as B5 draws it."),
        ("Real content", "The hull lines are the game's (SO_Class_<hull> Description; the Serpent's 'sheilded' is the game's own typo). The artifacts are the game's: Aegis, Aftershock, Hydra, Javelin."),
    ]
    items = "".join(f'<div style="display:grid;grid-template-columns:340px 1fr;gap:28px"><span class="cap" style="font-size:18px">{k}</span>'
                    f'<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{v}</span></div>' for k, v in rows)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:24px">'
            f'<span style="font-size:36px">Rev {REV} - the seat editor in the game\'s own look</span>{items}</div>')
    return page(f"Rev {REV} - What was wrong with Rev 82's editor, what the three draw from, the recommendation", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}Compare.dc.html": compare_board, f"R{REV}S1.dc.html": s1_board, f"R{REV}S2.dc.html": s2_board, f"R{REV}S3.dc.html": s3_board, f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
