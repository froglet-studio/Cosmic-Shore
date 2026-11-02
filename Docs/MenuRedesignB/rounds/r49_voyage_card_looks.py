#!/usr/bin/env python3
"""Rev 49 boards: option I's voyage card (Rev 44 I, Formation, picked), four more inviting looks, just the card.

Shombith, 2026-11-02: "Lets go I, but make the card background more inviting, its a lot of texts and etc, give me
mockups of just the cards maybe 4."

Each board is the card at its real size (760 x 868) and keeps what the I card does: the voyage pager, the squad
formation with the squad presets beside it (a preset or D-pad up / down swaps the formation), the voyage, Set sail,
Ark crew and Story. What changes is the ground and how much is written: the summary moves to Story, the numbers are
three, the waypoints show the two ends lit, and the art carries the place.

    python3 Docs/MenuRedesignB/rounds/r49_voyage_card_looks.py <root> 49
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2])
sys.argv = [sys.argv[0], sys.argv[1], "44"]
for name in ("r43", "r44"):
    spec = importlib.util.spec_from_file_location(name, HERE / f"{name}_voyage_window.py")
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
import r43  # noqa: E402
import r44  # noqa: E402
from r43 import LIGHT, INACT, NEUT, NEUT_D, NEUT_L, SUB, VDARK, ROUTE, ARK_AMBER, kc, DPAD_L, DPAD_R, GLOW_CSS  # noqa: E402

W, H = 760, 868
SECTOR = r43.MAP_TIERS[2][3]          # the Sector render: Lantern Reach's reefs and clouds
COURSE = r43.MAP                      # the Region render: the course from Tumult to Lantern Reach, lit
SECTOR_PIN = (1012, 576)              # Lantern Reach on the Sector render (px of 2196 x 1358)
COURSE_PIN = (660, 157)
ARKS = r44.ARKS
SEATS_BY_SQUAD = [
    [["Leader", "You", "Sparrow", "Shrapnel", "Charge", "Extra Bay"], ["Rogue One", "Halcyon", "Dolphin", "Riptide", "Space", "Seed Pod"],
     ["Rogue Two", "AI wingmate", "Squirrel", "Trace", "Time", "No artifact"]],
    [["Leader", "Kestrel", "Rhino", "Tomahawk", "Mass", "Long Edge"], ["Rogue One", "You", "Manta", "Reaper", "Charge", "Tailwind"],
     ["Rogue Two", "AI wingmate", "Serpent", "Scales", "Space", "No artifact"]],
]
for _arks, _seats in zip(ARKS, SEATS_BY_SQUAD):
    _arks["seats"] = [dict(zip(("role", "who", "vessel", "pilot", "element", "art"), x), hull=r43.HULL[x[2]],
                           none=x[5] == "No artifact") for x in _seats]

UP = '<svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="M1.5 6.5L5 3l3.5 3.5"></path></svg>'
DOWN = '<svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="M1.5 3.5L5 7l3.5-3.5"></path></svg>'

LOGIC = """const ARKS = %s;
class Component extends DCLogic {
constructor(p){ super(p); this.state = { ark: 0 }; }
renderVals() {
const k = this.state.ark, A = ARKS[k];
return { ak: { name: A.name, seats: A.seats, idx: k + 1, count: ARKS.length, h0: A.hulls[0], h1: A.hulls[1], h2: A.hulls[2], a0: A.alts[0], a1: A.alts[1], a2: A.alts[2],
  prev: () => this.setState({ ark: (k + ARKS.length - 1) %% ARKS.length }), next: () => this.setState({ ark: (k + 1) %% ARKS.length }),
  list: ARKS.map((a, i) => ({ name: a.name, line: a.line, h0: a.hulls[0], h1: a.hulls[1], h2: a.hulls[2], on: i === k, off: i !== k,
    pressed: i === k ? 'true' : 'false', fill: i === k ? 'rgba(230,233,255,.12)' : 'rgba(0,1,10,.35)', edge: i === k ? '#E6E9FF' : 'rgba(116,123,173,.45)',
    pick: () => this.setState({ ark: i }) })) } };
}
}"""


def art(src, pin, scale, w, h, cx, cy, extra=""):
    """An <img> of a render, scaled, placed so `pin` lands at (cx, cy) of a w x h box."""
    iw, ih = 2196 * scale, 1358 * scale
    left, top = cx - pin[0] * scale, cy - pin[1] * scale
    return (f'<img src="{src}" alt="" style="position:absolute;left:{left:.0f}px;top:{top:.0f}px;width:{iw:.0f}px;height:{ih:.0f}px;'
            f'max-width:none;{extra}">')


def pager(dark=True):
    tabs = "".join(
        f'<button class="pick" type="button" style="position:relative;display:flex;align-items:baseline;gap:6px;white-space:nowrap;padding:6px 0">'
        f'<span class="num" style="font-size:12px;color:{LIGHT if n == "2" else "rgba(230,233,255,.45)"}">{n}</span>'
        f'<span style="font-size:15px;color:{LIGHT if n == "2" else "rgba(230,233,255,.6)"}">{p}</span>'
        + (f'<span class="abs" style="left:0;right:0;bottom:-7px;height:2px;background:{ROUTE}"></span>' if n == "2" else "") + '</button>'
        for n, p, *_ in r43.PAGER)
    key = lambda svg, lab: f'<button class="pick" type="button" aria-label="{lab}"><span class="kc" style="height:26px;border-color:rgba(230,233,255,.35)">{svg}</span></button>'
    return (f'<div style="display:flex;align-items:center;gap:12px;padding-bottom:8px;border-bottom:1px solid rgba(230,233,255,.14)">'
            f'{key(DPAD_L, "Previous voyage")}<div style="flex:1;display:flex;justify-content:space-between;gap:10px">{tabs}</div>{key(DPAD_R, "Next voyage")}</div>')


def presets(compact=False):
    rows = ('<sc-for list="{{ak.list}}" as="q" hint-placeholder-count="2">'
            '<button class="pick sls" type="button" onClick="{{q.pick}}" aria-pressed="{{q.pressed}}" '
            'style="display:flex;flex-direction:column;gap:5px;padding:8px 12px;background:{{q.fill}};box-shadow:inset 0 0 0 1px {{q.edge}}">'
            '<span style="font-size:15px">{{q.name}}</span>'
            + ('' if compact else '<span style="display:flex;gap:4px"><img src="{{q.h0}}" alt="" style="width:50px;height:18px;object-fit:contain">'
               '<img src="{{q.h1}}" alt="" style="width:50px;height:18px;object-fit:contain"><img src="{{q.h2}}" alt="" style="width:50px;height:18px;object-fit:contain"></span>')
            + '</button></sc-for>')
    return (f'<div style="display:flex;flex-direction:column;gap:8px;width:200px;flex:none">'
            f'<div style="display:flex;justify-content:space-between;align-items:center"><span class="lab" style="font-size:11px;color:{LIGHT}">Squad presets</span>'
            f'<span style="display:flex;gap:4px"><button class="pick" type="button" aria-label="Previous squad" onClick="{{{{ak.prev}}}}"><span class="kc">{UP}</span></button>'
            f'<button class="pick" type="button" aria-label="Next squad" onClick="{{{{ak.next}}}}"><span class="kc">{DOWN}</span></button></span></div>'
            f'{rows}<button class="btn gho sls" type="button" style="height:36px;font-size:12px;letter-spacing:.1em;gap:8px;box-shadow:inset 0 0 0 1px rgba(230,233,255,.35)">'
            f'{kc("X")}<span>Configure the squad</span></button></div>')


def squad_name():
    return (f'<div style="display:flex;flex-direction:column;gap:2px"><span class="lab" style="font-size:11px;color:rgba(230,233,255,.7)">Your squad</span>'
            f'<span style="font-size:24px">{{{{ak.name}}}}</span></div>')


def numbers(size=30, gap=30):
    return (f'<div style="display:flex;gap:{gap}px;align-items:flex-end">'
            + "".join(f'<span style="display:flex;flex-direction:column;gap:3px"><span class="num" style="font-size:{size}px;line-height:1">{v}</span>'
                      f'<span class="lab" style="font-size:10px;color:rgba(230,233,255,.7)">{k}</span></span>' for k, v in (("Course", "580 ly"), ("Sail", "29 min"), ("Intensity", "I2")))
            + '</div>')


def ends():
    """The waypoints at their quietest: the Ark lit at Tumult, the sweep, Lantern Reach pulsing."""
    return (f'<div style="position:relative;display:flex;justify-content:space-between;align-items:flex-start">'
            f'<span class="abs" style="left:7px;right:7px;top:6px;height:2px;background:linear-gradient(90deg,{ROUTE},rgba(200,255,90,.25))"></span>'
            f'<span class="abs" style="left:7px;right:7px;top:2px;height:10px"><span class="wpb" style="top:0"></span></span>'
            f'<div style="position:relative;display:flex;flex-direction:column;gap:8px"><span class="wpn" style="width:14px;height:14px;background:{ROUTE};animation:none;box-shadow:0 0 12px 3px rgba(200,255,90,.6)"></span>'
            f'<span style="font-size:13px">Tumult</span><span class="cap" style="font-size:10px;color:{ARK_AMBER}">Your Ark</span></div>'
            f'<span class="cap num" style="align-self:center;font-size:11px;color:rgba(230,233,255,.7);padding:3px 8px;background:rgba(0,1,10,.55)">7 waypoints - 29 min</span>'
            f'<div style="position:relative;display:flex;flex-direction:column;align-items:flex-end;gap:8px">'
            f'<span class="wpr" style="right:-4px;top:-4px;width:22px;height:22px"></span><span class="wpn" style="width:14px;height:14px;animation-delay:2.9s"></span>'
            f'<span style="font-size:13px">Lantern Reach</span><span class="cap" style="font-size:10px;color:{ROUTE}">Destination</span></div></div>')


CUR = {"key": "A"}   # which look is being drawn; the level buttons link its three boards (Play follows them)


def link(level):
    return f'R{REV}{CUR["key"]}{["", "", "Crew", "Story"][level]}.dc.html'


def buttons():
    return (f'<div style="display:flex;gap:10px"><button class="btn cta sl" type="button" style="flex:2">{kc("A")}<span>Set sail</span></button>'
            f'<a class="btn sl" href="{link(2)}" style="flex:1;text-decoration:none;background:rgba(43,50,102,.85);color:{LIGHT}">{kc("X")}<span>Ark crew</span></a>'
            f'<a class="btn sl" href="{link(3)}" style="flex:1;text-decoration:none;background:transparent;color:#C9CDEA;box-shadow:inset 0 0 0 1px rgba(230,233,255,.35)">{kc("Y")}<span>Story</span></a></div>')


TITLE = "Open the vaults at Lantern Reach"
LINE = "Vaults are lit at Lantern Reach."


LAST = {}


def card_root(inner, ground):
    LAST["ground"] = ground
    return (f'<section aria-label="Voyage card" class="sl" style="position:absolute;left:0;top:0;width:{W}px;height:{H}px;overflow:hidden;background:#060A26;'
            f'box-shadow:inset 0 0 0 1px #2B3266">{ground}<div style="position:absolute;inset:0;padding:22px 26px 24px;display:flex;flex-direction:column;gap:14px">{inner}</div></section>')


def look_A():
    """Destination glow: the Sector render around Lantern Reach, soft and blurred, fills the card; the squad stands in it."""
    ground = (art(SECTOR, SECTOR_PIN, 0.62, W, H, 380, 230, "filter:blur(5px) saturate(1.15);opacity:.95;transform:scale(1.04)")
              + '<div class="abs" style="inset:0;background:linear-gradient(180deg,rgba(6,10,38,.55) 0%,rgba(6,10,38,.15) 30%,rgba(6,10,38,.55) 58%,rgba(6,10,38,.96) 82%)"></div>')
    inner = (f'{pager()}<div style="display:flex;flex-direction:column;gap:4px;margin-top:4px"><span class="lab" style="color:rgba(230,233,255,.75)">Voyage 2 - Hunt - found by Citadel</span>'
             f'<span style="font-size:34px;line-height:1.08">{TITLE}</span><span style="font-size:16px;color:#D6DAF5">{LINE}</span></div>'
             f'<div style="display:flex;gap:16px;align-items:flex-end">{"<div style=\"flex:1;display:flex;flex-direction:column;gap:6px\">" + squad_name() + r44.formation_live(.72) + "</div>"}{presets()}</div>'
             f'<div style="flex:1"></div>{numbers()}{ends()}{buttons()}')
    return card_root(inner, ground)


def look_B():
    """Hangar light: a deep gradient, the sea lines, and the squad lit on a floor of light, large; the voyage is a strip."""
    sea = ('<svg class="abs" style="left:0;top:0" width="760" height="868" aria-hidden="true">'
           + "".join(f'<path d="M0 {y} C 190 {y - 18}, 380 {y + 18}, 570 {y} S 760 {y - 10}, 760 {y}" fill="none" stroke="#7F98FF" stroke-opacity="{o}" stroke-width="1"></path>'
                     for y, o in ((120, .14), (190, .1), (260, .16), (330, .1), (400, .12), (620, .08), (690, .1)))
           + '</svg>')
    ground = ('<div class="abs" style="inset:0;background:radial-gradient(ellipse 80% 55% at 42% 40%,#1B2A7A 0%,#0B1250 45%,#050928 80%)"></div>' + sea
              + '<div class="abs" style="left:40px;top:330px;width:420px;height:70px;border-radius:50%;background:radial-gradient(ellipse at center,rgba(150,180,255,.38),rgba(150,180,255,0) 70%)"></div>'
              + '<div class="abs" style="left:0;right:0;bottom:0;height:330px;background:linear-gradient(180deg,rgba(5,9,40,0),rgba(3,6,28,.96) 30%)"></div>')
    inner = (f'{pager()}<div style="display:flex;gap:16px;align-items:flex-start;margin-top:6px">'
             f'<div style="flex:1;display:flex;flex-direction:column;gap:8px">{squad_name()}{r44.formation_live(.86)}</div>{presets()}</div>'
             f'<div style="flex:1"></div><div style="display:flex;flex-direction:column;gap:6px"><span class="lab" style="color:rgba(230,233,255,.75)">Voyage 2 - Hunt - found by Citadel</span>'
             f'<span style="font-size:30px;line-height:1.1">{TITLE}</span></div>{numbers(28)}{ends()}{buttons()}')
    return card_root(inner, ground)


def look_C():
    """Route poster: the real course, Tumult to Lantern Reach, lit on the HyperSea map, dimmed behind the whole card."""
    ground = (art(COURSE, (1085, 668), 0.5, W, H, 400, 360, "opacity:.9")
              + '<div class="abs" style="inset:0;background:radial-gradient(ellipse 70% 60% at 50% 42%,rgba(6,10,38,.25),rgba(6,10,38,.85) 80%)"></div>'
              + '<div class="abs" style="left:0;right:0;bottom:0;height:420px;background:linear-gradient(180deg,rgba(6,10,38,0),rgba(6,10,38,.97) 38%)"></div>')
    inner = (f'{pager()}<div style="display:flex;justify-content:space-between;align-items:flex-start;margin-top:4px">'
             f'<span class="cap" style="font-size:12px;padding:5px 10px;background:rgba(0,1,10,.6);color:{ROUTE}">Your course - 580 ly</span>'
             f'<span class="cap" style="font-size:12px;padding:5px 10px;background:rgba(0,1,10,.6);color:#D6DAF5">Found by Citadel</span></div>'
             f'<div style="flex:1"></div><div style="display:flex;flex-direction:column;gap:4px"><span class="lab" style="color:rgba(230,233,255,.75)">Voyage 2 - Hunt</span>'
             f'<span style="font-size:34px;line-height:1.08">{TITLE}</span></div>{numbers(26, 26)}'
             f'<div style="display:flex;gap:16px;align-items:flex-end"><div style="flex:1;display:flex;flex-direction:column;gap:6px">{squad_name()}{r44.formation_live(.62)}</div>{presets(compact=True)}</div>'
             f'{buttons()}')
    return card_root(inner, ground)


def look_D():
    """Split art: the destination's reefs as a picture across the top; the squad sits on the seam; the rest is calm."""
    ground = ('<div class="abs" style="left:0;top:0;width:760px;height:380px;overflow:hidden">'
              + art(SECTOR, SECTOR_PIN, 0.9, 760, 380, 300, 170, "filter:blur(2.5px) saturate(1.2);transform:scale(1.03)")
              + '<div class="abs" style="inset:0;background:linear-gradient(180deg,rgba(6,10,38,.45) 0%,rgba(6,10,38,0) 25%,rgba(6,10,38,0) 60%,#060A26 100%)"></div></div>')
    inner = (f'{pager()}<div style="display:flex;flex-direction:column;gap:4px;margin-top:4px;text-shadow:0 2px 12px rgba(0,1,10,.8)">'
             f'<span class="lab" style="color:#E6E9FF">Voyage 2 - Hunt - Lantern Reach</span><span style="font-size:36px;line-height:1.05">{TITLE}</span></div>'
             f'<div style="flex:1"></div><div style="display:flex;gap:16px;align-items:flex-end">'
             f'<div style="flex:1;display:flex;flex-direction:column;gap:6px">{squad_name()}{r44.formation_live(.74)}</div>{presets()}</div>'
             f'<span style="font-size:15px;color:#C9CDEA">{LINE} Found by Oriel Vance, Citadel.</span>{numbers(28)}{ends()}{buttons()}')
    return card_root(inner, ground)


# ------------------------------------------------------------------------------------------------ the other two parts
# Shombith, 2026-11-02: "Show me the 3 parts of the cards." Under each look: level 2 (Ark crew, the 12 settings, which
# follow the squad picked) and level 3 (Story, optional), on that look's own ground, darkened for reading.

def crumbs(level):
    names = ["1 Voyage", "2 Ark crew", "3 Story"]
    return (f'<div style="display:flex;align-items:center;gap:14px;padding-bottom:10px;border-bottom:1px solid rgba(230,233,255,.14)">'
            f'<a href="{link(1)}" style="display:flex;align-items:center;gap:8px;text-decoration:none">{kc("B")}<span class="cap" style="font-size:13px;color:rgba(230,233,255,.7)">Voyage</span></a>'
            f'<span style="flex:1"></span>'
            + "".join(f'<a href="{link(i + 1)}" class="cap" style="font-size:12px;text-decoration:none;color:{LIGHT if i + 1 == level else "rgba(230,233,255,.5)"}">{n}</a>' for i, n in enumerate(names))
            + '</div>')


def seat_col():
    setting = lambda lab, key: (f'<button class="pick" type="button" style="display:flex;align-items:center;gap:6px;height:46px;padding:0 10px;'
                                f'background:rgba(0,1,10,.45);box-shadow:inset 0 0 0 1px rgba(116,123,173,.4)"><span style="display:flex;flex-direction:column;gap:2px;flex:1;min-width:0">'
                                f'<span class="lab" style="font-size:9px;color:rgba(230,233,255,.6)">{lab}</span><span class="ell" style="font-size:15px">{{{{s.{key}}}}}</span></span>'
                                f'<span style="color:rgba(230,233,255,.5)">{r43.CHEV}</span></button>')
    return ('<sc-for list="{{ak.seats}}" as="s" hint-placeholder-count="3"><div style="flex:1;min-width:0;display:flex;flex-direction:column;gap:6px">'
            '<span class="lab" style="font-size:10px;color:rgba(230,233,255,.7)">{{s.role}}</span><span style="font-size:18px">{{s.who}}</span>'
            '<img src="{{s.hull}}" alt="{{s.vessel}}" style="width:100%;height:110px;object-fit:contain;margin:6px 0">'
            + setting("Vessel", "vessel") + setting("Pilot", "pilot") + setting("Element", "element") + setting("Artifact", "art")
            + '</div></sc-for>')


def squad_tabs():
    return ('<div style="display:flex;align-items:center;gap:8px"><span class="lab" style="font-size:11px;margin-right:4px">Squad</span>'
            '<sc-for list="{{ak.list}}" as="q" hint-placeholder-count="2"><button class="pick sls" type="button" onClick="{{q.pick}}" aria-pressed="{{q.pressed}}" '
            'style="padding:6px 12px;background:{{q.fill}};box-shadow:inset 0 0 0 1px {{q.edge}}"><span style="font-size:14px">{{q.name}}</span></button></sc-for>'
            f'<span style="flex:1"></span><span style="display:flex;gap:4px"><button class="pick" type="button" aria-label="Previous squad" onClick="{{{{ak.prev}}}}"><span class="kc">{UP}</span></button>'
            f'<button class="pick" type="button" aria-label="Next squad" onClick="{{{{ak.next}}}}"><span class="kc">{DOWN}</span></button></span></div>')


def level_crew(ground):
    inner = (f'{crumbs(2)}<div style="display:flex;flex-direction:column;gap:4px"><span class="lab" style="color:rgba(230,233,255,.75)">Ark crew - three seats, four settings each</span>'
             f'<span style="font-size:30px">{{{{ak.name}}}}</span><span style="font-size:14px;color:#C9CDEA">Loaded from your squad. Changes are for this voyage until you save them.</span></div>'
             f'{squad_tabs()}<div style="display:flex;gap:12px">{seat_col()}</div><div style="flex:1"></div>'
             f'<div style="display:flex;gap:10px"><button class="btn cta sl" type="button" style="flex:2">{kc("A")}<span>Set sail</span></button>'
             f'<button class="btn sl" type="button" style="flex:1.2;background:rgba(43,50,102,.85);color:{LIGHT}">{kc("X")}<span>Save squad</span></button>'
             f'<a class="btn sl" href="{link(1)}" style="flex:1;text-decoration:none;background:transparent;color:#C9CDEA;box-shadow:inset 0 0 0 1px rgba(230,233,255,.35)">{kc("B")}<span>Voyage</span></a></div>')
    return card_root(inner, ground + '<div class="abs" style="inset:0;background:rgba(6,10,38,.42)"></div>')


STORY = [("Who brought it", "Oriel Vance, CITADEL, Hunter", "Reads a vault's shell like a face. Has opened more than she has kept."),
         ("The place", "Lantern Reach, a vault field", "Super-shielded shells drift in a lit shoal, each with an artifact inside."),
         ("The news", "Heard at Celadon Harbour", "A trader swears the shells at Lantern Reach are lit again."),
         ("The water", "Coreward lane, 70 ly below the midplane", "Light from here takes 580 years to reach Lantern Reach. You take 29 minutes. The thickest water is 2.7 g/cm3, as heavy as iron.")]


def level_story(ground):
    blocks = "".join(f'<div style="display:flex;flex-direction:column;gap:5px;padding:14px 0;border-top:1px solid rgba(230,233,255,.14)">'
                     f'<span class="lab" style="font-size:11px;color:rgba(230,233,255,.7)">{k}</span><span style="font-size:18px">{a}</span>'
                     f'<span style="font-size:15px;line-height:1.5;color:#D6DAF5">{b}</span></div>' for k, a, b in STORY)
    inner = (f'{crumbs(3)}<div style="display:flex;flex-direction:column;gap:4px"><div style="display:flex;gap:10px;align-items:center">'
             f'<span class="lab" style="color:rgba(230,233,255,.75)">Story</span><span class="cap" style="font-size:11px;padding:2px 8px;box-shadow:inset 0 0 0 1px rgba(230,233,255,.35);color:rgba(230,233,255,.7)">Optional</span></div>'
             f'<span style="font-size:30px;line-height:1.1">{TITLE}</span>'
             f'<span style="font-size:16px;color:#D6DAF5">Vaults are lit at Lantern Reach. Crack one with a payload, carry its artifact back to the Ark and stow it.</span></div>'
             f'{blocks}<div style="flex:1"></div><div style="display:flex;gap:10px"><button class="btn cta sl" type="button" style="flex:2">{kc("A")}<span>Set sail</span></button>'
             f'<a class="btn sl" href="{link(1)}" style="flex:1;text-decoration:none;background:transparent;color:#C9CDEA;box-shadow:inset 0 0 0 1px rgba(230,233,255,.35)">{kc("B")}<span>Voyage</span></a></div>')
    return card_root(inner, ground + '<div class="abs" style="inset:0;background:rgba(6,10,38,.52)"></div>')


LOOKS = [("A", "Destination glow: the destination's region, soft and blurred, fills the card; the squad stands in it", look_A),
         ("B", "Hangar light: your squad lit large on a floor of light, the voyage a strip under it", look_B),
         ("C", "Route poster: the real course on the HyperSea map, dimmed behind the card", look_C),
         ("D", "Split art: the destination's reefs across the top, the squad on the seam", look_D)]


def page(title, body):
    p = r43.HEAD.format(title=r43.e(title)) + (f'<div style="width: {W}px; height: {H}px; position: relative; overflow: hidden; background: #00041F; color: {LIGHT}; '
                                              f"font-family: 'Aldrich', 'Chakra Petch', sans-serif\">\n{body}\n</div>\n") + r43.TAIL.format(w=W, h=H)
    p = p.replace("</style>", GLOW_CSS + "</style>", 1)
    return p.replace(r43.EMPTY_LOGIC, LOGIC % json.dumps(ARKS), 1)


def notes():
    def para(t):
        return f'<p style="margin:0;font-size:15px;line-height:1.5;color:#C9CDEA">{t}</p>'
    body = "".join([
        f'<span class="lab">Rev {REV} - notes</span><span style="font-size:24px">The I card, four more inviting looks</span>',
        '<span class="lab" style="margin-top:8px">Asked (Shombith)</span>',
        para('"Lets go I, but make the card background more inviting, its a lot of texts and etc, give me mockups of just the cards maybe 4."'),
        '<span class="lab" style="margin-top:8px">Shared by all four</span>',
        para("What the I card does stays: the voyage pager, the squad's formation with the presets beside it (a preset or D-pad up / down swaps it), "
             "Set sail, Ark crew, Story. Less is written: the summary is one line (the rest is in Story), three numbers, and the waypoints show only the "
             "two ends, lit, with the count between. The art is the Voyage Board's own renders of where you are going."),
        '<span class="lab" style="margin-top:8px">Recommendation: A</span>',
        para("The destination's colour fills the card without fighting the text, and the squad reads as standing in the place it is about to sail to. "
             "C shows the most (the real course) but repeats the map beside it; B is the calmest; D is the most like a game card."),
    ])
    return (f'<div style="position:absolute;inset:0;padding:30px;display:flex;flex-direction:column;gap:10px;background:#060A26;box-shadow:inset 0 0 0 1px #2B3266">{body}</div>')


def main():
    files = {}
    for key, what, fn in LOOKS:
        CUR["key"] = key
        files[f"R{REV}{key}.dc.html"] = page(f"Rev {REV} {key} - {what}", fn())
        ground = LAST["ground"]
        files[f"R{REV}{key}Crew.dc.html"] = page(f"Rev {REV} {key} - level 2, Ark crew", level_crew(ground))
        files[f"R{REV}{key}Story.dc.html"] = page(f"Rev {REV} {key} - level 3, Story", level_story(ground))
    nb = r43.HEAD.format(title=f"Rev {REV} - notes") + (f'<div style="width: {W}px; height: {H}px; position: relative; overflow: hidden; background: #00041F; color: {LIGHT}; '
                                                        f"font-family: 'Aldrich', 'Chakra Petch', sans-serif\">\n{notes()}\n</div>\n") + r43.TAIL.format(w=W, h=H)
    files[f"R{REV}Notes.dc.html"] = nb
    for n, s in files.items():
        (ROOT / n).write_text(s, encoding="utf-8")
        print(f"{n:22} {len(s):>7}")


if __name__ == "__main__":
    main()
