#!/usr/bin/env python3
"""Rev 46 boards: the Voyage window with small voyage cards that slide open.

Shombith, 2026-11-02, after the Rev 43-44 edits: "Rather than big cards make smaller cards like arena maybe and they
slide open when we click them or select them in the map. With this design move ahead."

One component draws every board; each state board starts it in another state. The HyperSea map explorer (Garrett's
seven tiers, a1922d78 / #1717) is on the left; the four voyages the crew found are small arena-style cards on the right.
A card, or a voyage's pin on the map at the Sector tier, slides the voyage open over the cards: level 1 the voyage
(with Your Ark), level 2 its Ark crew (the 12 settings), level 3 its story. B slides it shut.

    python3 Docs/MenuRedesignB/rounds/r46_voyage_cards.py <root> 46

Every word and number on the cards is the Voyage Board's (its four voyages) and the slice's (voyage_slice.json).
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
from r43 import (LIGHT, INACT, NEUT, NEUT_D, NEUT_L, SUB, CTA, VDARK, BLACK, ROUTE, ARK_AMBER, DOM, HULL, PRESETS, SEATS,  # noqa: E402
                 MAP_TIERS, TUMULT_IN_GALAXY, MAP_PX, MAP_ROUTE, kc, nav, setting, CHEV, LOCK, DPAD_L, DPAD_R, GLOW_CSS, SEA)

SECTOR = MAP_TIERS[2][3]
POS = "40% 45%"
MX, MY, MW, MH, EH = 40, 172, 1060, 640, 216          # the map and the edge view
PX, PY, PW, PH = 1120, 172, 760, 868                   # the voyage column

# The four voyages, as the Voyage Board lists them (its own words), with their course renders.
VOYAGES = [
    {"n": "1", "title": "Carry sealed news to Tollmoor", "kind": "Port", "place": "Tollmoor", "min": "27 min", "ly": "540 ly", "i": "I1",
     "pays": "2 to 4 omni", "odds": "94%", "cs": "VOLT", "crew": "Merrin Tal", "faction": "Merchant",
     "crewLine": "Prices news by its voltage. Fresh news sparks.",
     "summary": "Reach Tollmoor with the Ark standing. The news aboard will change what Tollmoor knows.",
     "placeLine": "A toll port on the edge of Tumult's bubble. The dock crews hear everything first.",
     "light": "News by light would take 540 years to reach Tollmoor. Your Ark carries it in 27 min.",
     "lane": "Rimward, against the spin lane, cruising 10 ly below the midplane. It crosses 4 lanes on the chart.",
     "thick": "0.4 g/cm3, a froth, lighter than water", "backed": "ZODIAC charts the fastest lane",
     "sea": [["Spray", 18, "#8e9cb4"], ["Froth", 9, "#2e8a9e"]],
     "map": "/_blob/9778fd69fcae1f8cb003a9bbd8508a56", "edge": "/_blob/cd62f3bbf1d64b1e7f1f4739209aaa74",
     "edgeCap": "Tollmoor 98 ly above", "pin": (1372, 903)},
    {"n": "2", "title": "Open the vaults at Lantern Reach", "kind": "Vault field", "place": "Lantern Reach", "min": "29 min", "ly": "580 ly", "i": "I2",
     "pays": "1 find", "odds": "78%", "cs": "CITADEL", "crew": "Oriel Vance", "faction": "Hunter",
     "crewLine": "Reads a vault's shell like a face. Has opened more than she has kept.",
     "summary": "Vaults are lit at Lantern Reach. Crack one with a payload, carry its artifact back to the Ark and stow it.",
     "placeLine": "Super-shielded shells drift in a lit shoal, each with an artifact inside.",
     "light": "Light from here takes 580 years to reach Lantern Reach. You take 29 min.",
     "lane": "Coreward lane, cruising 70 ly below the midplane. It crosses no other lane on the chart.",
     "thick": "2.7 g/cm3, as heavy as iron", "backed": "",
     "sea": [["Spray", 9, "#8e9cb4"], ["Froth", 1, "#2e8a9e"], ["Water", 19, "#2869b8"]],
     "map": r43.MAP, "edge": r43.EDGE, "edgeCap": "Lantern Reach 34 ly below", "pin": (1012, 576)},
    {"n": "3", "title": "Raid the thieves' cache at Gallowglass", "kind": "Hoard", "place": "Gallowglass", "min": "47 min", "ly": "940 ly", "i": "I2",
     "pays": "1 find", "odds": "90%", "cs": "GALLEON", "crew": "Cass Morrow", "faction": "Pirate",
     "crewLine": "Knows every Ark by its silhouette, and every wreck by its smell.",
     "summary": "The thieves' hold in the reefs toward the centre. Take what they took, and stow it before they notice.",
     "placeLine": "The thieves' hold in the reefs toward the centre.",
     "light": "Light from here takes 940 years to reach Gallowglass. You take 47 min.",
     "lane": "Coreward, against the spin lane, cruising 50 ly below the midplane. It crosses 2 lanes on the chart.",
     "thick": "30 g/cm3, heavier than osmium", "backed": "TALON, ZODIAC and WINGMAN back it",
     "sea": [["Spray", 9, "#8e9cb4"], ["Froth", 3, "#2e8a9e"], ["Water", 25, "#2869b8"], ["Thick", 10, "#6a44b0"]],
     "map": "/_blob/037d66eeef857313705b96448c91f4a0", "edge": "/_blob/58696b3b52c011054a8b024f94aff2e1",
     "edgeCap": "Gallowglass on the midplane", "pin": (1412, 382)},
    {"n": "4", "title": "Hunt the creature in The Maw", "kind": "Lair", "place": "The Maw", "min": "49 min", "ly": "980 ly", "i": "I3",
     "pays": "2 finds", "odds": "72%", "cs": "TALON", "crew": "Brisk Amadou", "faction": "Hunter",
     "crewLine": "Tracks predators for a living, and sells what they guard.",
     "summary": "One great creature in a closed cell, fat on thick water. Bring it down and carry off what it guards.",
     "placeLine": "One great creature in a closed cell, fat on thick water.",
     "light": "Light from here takes 980 years to reach The Maw. You take 49 min.",
     "lane": "Coreward lane, cruising 70 ly below the midplane. It crosses no other lane on the chart.",
     "thick": "40 g/cm3, heavier than osmium", "backed": "ZODIAC and WINGMAN back it",
     "sea": [["Spray", 7, "#8e9cb4"], ["Froth", 2, "#2e8a9e"], ["Water", 27, "#2869b8"], ["Thick", 13, "#6a44b0"]],
     "map": "/_blob/da47aa2e4c65d63e53711ebfe6fc4ac2", "edge": "/_blob/e8de257ad245f412b429c6d58363dc57",
     "edgeCap": "The Maw 163 ly above", "pin": (1078, 362)},
]
LANTERN_WPS = ["Tumult", "Elyel Light", "Brinon", "Rosum", "Belira Reach", "Duneth Gate", "Lantern Reach"]


def cover(w, h, pos):
    fx, fy = (float(v.strip("%")) / 100 for v in pos.split())
    s = max(w / MAP_PX[0], h / MAP_PX[1])
    return s, (w - MAP_PX[0] * s) * fx, (h - MAP_PX[1] * s) * fy


def data():
    s, ox, oy = cover(MW, MH, POS)
    cards = []
    for v in VOYAGES:
        px, py = v["pin"]
        # the card's art: the Sector render around the voyage's destination
        k = 0.42
        aw, ah = MAP_PX[0] * k, MAP_PX[1] * k
        art = f"width:{aw:.0f}px;height:{ah:.0f}px;left:{-(px * k - 185):.0f}px;top:{-(py * k - 62):.0f}px"
        wps = LANTERN_WPS if v["n"] == "2" else ["Tumult", v["place"]]
        cards.append({**{k2: v[k2] for k2 in v if k2 not in ("pin",)}, "art": art, "wps": wps,
                      "pinX": round(ox + px * s), "pinY": round(oy + py * s)})
    return cards


LOGIC = r"""const START = %(start)s;
const TIERS = %(tiers)s;
const TUMULT = %(tumult)s;
const V = %(voyages)s;
const PRESETS = %(presets)s;
class Component extends DCLogic {
constructor(p){ super(p); const s = (p && p.start) || START; this.state = { t: s.t, open: s.open, level: s.level, preset: 0 }; }
go(t) { this.setState({ t: Math.max(0, Math.min(TIERS.length - 1, t)) }); }
openCard(i, fromMap) { this.setState({ open: i, level: 1, t: fromMap ? 2 : 3 }); }
renderVals() {
const st = this.state, t = st.t, o = st.open, isOpen = o !== null && o !== undefined, v = isOpen ? V[o] : V[1];
const tiers = TIERS.map((x, i) => i === 3 ? [x[0], x[1], x[2], v.map, x[4], x[5]] : x);
const T = tiers[t], box = T[5];
const layers = tiers.map((x, i) => ({ src: x[3], alt: 'HyperSea map, ' + x[0] + ' tier', pos: x[4], op: i === t ? 1 : 0, sc: i < t ? 1.8 : (i > t ? 0.55 : 1) }));
const ladder = tiers.map((x, i) => ({ name: x[0], on: i === t, off: i !== t, pick: () => this.go(i) })).reverse();
const mmBox = box ? 'left:' + (TUMULT[0] - box / 2) + '%%;top:' + (TUMULT[1] - box * 0.8) + '%%;width:' + box + '%%;height:' + (box * 1.6) + '%%' : 'display:none';
const pins = V.map((c, i) => ({ n: c.n, place: c.place, style: 'left:' + (c.pinX - 22) + 'px;top:' + (c.pinY - 22) + 'px',
  ring: isOpen && o === i ? '0 0 0 2px #C8FF5A, 0 0 18px 4px rgba(200,255,90,.55)' : '0 0 0 1.5px rgba(230,233,255,.55)', pick: () => this.openCard(i, true) }));
const cards = V.map((c, i) => ({ n: c.n, title: c.title, kind: c.kind, min: c.min, ly: c.ly, i: c.i, pays: c.pays, cs: c.cs, art: c.art,
  edge: isOpen && o === i ? '#E6E9FF' : '#2B3266', pick: () => this.openCard(i, false) }));
const n = v.wps.length;
const wps = v.wps.map((w, k) => ({ name: w, first: k === 0, mid: k > 0 && k < n - 1, last: k === n - 1, delay: (2.9 * k / Math.max(1, n - 1)).toFixed(2) + 's' }));
const total = v.sea.reduce((a, b) => a + b[1], 0);
const sea = v.sea.map((x) => ({ flex: x[1], color: x[2] }));
const seaText = v.sea.map((x) => x[1] + ' ' + x[0].toLowerCase()).join(' - ') + ' - ' + total + ' cells';
const presets = PRESETS.map((p, k) => ({ name: p[0], dom: p[1], line: p[2], c: p[3], cl: p[4], h0: p[5][0], h1: p[5][1], h2: p[5][2],
  on: k === st.preset, off: k !== st.preset, pick: () => this.setState({ preset: k }) }));
const level = st.level;
return {
  mx: { layers, ladder, tier: T[0].toUpperCase(), what: T[1], scale: T[2], course: t === 3 && isOpen && o === 1, inspect: t >= 4, sector: t === 2,
        mm: t > 0, mmBox, pins, zoomIn: () => this.go(t + 1), zoomOut: () => this.go(t - 1),
        diveArk: () => this.go(6), diveCourse: () => this.go(3), diveAll: () => this.go(2),
        edge: v.edge, edgeCap: 'Edge on, true scale - disk 1,000 ly - ' + v.edgeCap },
  vc: { cards, gridOp: isOpen ? 0.18 : 1, panelX: isOpen ? 'translateX(0)' : 'translateX(800px)', isOpen,
        l1: level === 1, l2: level === 2, l3: level === 3,
        c1: level === 1 ? '#E6E9FF' : '#747BAD', c2: level === 2 ? '#E6E9FF' : '#747BAD', c3: level === 3 ? '#E6E9FF' : '#747BAD',
        close: () => this.setState({ open: null, t: 2 }),
        prev: () => this.openCard((o + 3) %% 4, false), next: () => this.openCard((o + 1) %% 4, false),
        toCrew: () => this.setState({ level: 2 }), toStory: () => this.setState({ level: 3 }), toVoyage: () => this.setState({ level: 1 }) },
  v: { n: v.n, title: v.title, kind: v.kind, place: v.place, min: v.min, ly: v.ly, i: v.i, pays: v.pays, odds: v.odds, cs: v.cs, crew: v.crew,
       faction: v.faction, crewLine: v.crewLine, summary: v.summary, placeLine: v.placeLine, light: v.light, lane: v.lane, thick: v.thick,
       backed: v.backed, hasBacked: !!v.backed, wps, sea, seaText, presets }
};
}
}"""


def logic(start):
    tiers = [[a, b, c, d, (e or POS), f] for a, b, c, d, e, f in MAP_TIERS]
    presets = [[p[0], p[1], p[2], DOM[p[1]][0], DOM[p[1]][1], [HULL[h] for h in p[3]]] for p in PRESETS]
    return LOGIC % {"start": json.dumps(start), "tiers": json.dumps(tiers), "tumult": json.dumps(list(TUMULT_IN_GALAXY)),
                    "voyages": json.dumps(data()), "presets": json.dumps(presets)}


PLATE = "background:rgba(0,1,10,.84)"


def explorer():
    m = r43.map_explorer(MX, MY, MW, MH, EH, POS)
    # the edge view follows the open voyage
    m = m.replace(f'<img src="{r43.EDGE}" alt="Edge-on view at true scale: the course from Tumult, 30 ly above the midplane, down the core lane to Lantern Reach, 34 ly below"',
                  '<img src="{{mx.edge}}" alt="Edge-on view at true scale of the open course"')
    m = m.replace("Edge on, true scale - disk 1,000 ly - Lantern Reach 34 ly below</span>", "{{mx.edgeCap}}</span>")
    # the voyages' pins at the Sector tier: a press slides that voyage open
    pins = ('<sc-if value="{{mx.sector}}" hint-placeholder-val="{{ true }}"><sc-for list="{{mx.pins}}" as="p" hint-placeholder-count="4">'
            '<button class="pick" type="button" aria-label="Open the voyage to {{p.place}}" onClick="{{p.pick}}" '
            'style="position:absolute;{{p.style}};width:44px;height:44px;border-radius:50%;box-shadow:{{p.ring}};transition:box-shadow .2s"></button>'
            '</sc-for></sc-if>')
    m = m.replace("{{mx.sector}}\" hint-placeholder-val=\"{{ false }}\">", "{{mx.sector}}\" hint-placeholder-val=\"{{ false }}\">", 1)
    m = m.replace("</sc-if><div class=\"abs\" style=\"right:14px;top:14px;", "</sc-if>" + pins + "<div class=\"abs\" style=\"right:14px;top:14px;", 1)
    assert pins in m
    m = m.replace("All four voyages from Tumult - tap a pin to open its card", "Your crew's four voyages - press a pin to open it")
    return m


def cards_grid():
    card = ('<sc-for list="{{vc.cards}}" as="c" hint-placeholder-count="4">'
            '<button class="pick sl" type="button" onClick="{{c.pick}}" aria-label="Voyage {{c.n}}: {{c.title}}" '
            'style="position:relative;display:flex;flex-direction:column;height:300px;overflow:hidden;background:#080C2E;box-shadow:inset 0 0 0 1px {{c.edge}}">'
            '<span style="position:relative;display:block;height:132px;overflow:hidden;flex:none">'
            f'<img src="{SECTOR}" alt="" style="position:absolute;{{{{c.art}}}};max-width:none">'
            '<span class="abs" style="left:0;right:0;bottom:0;height:70px;background:linear-gradient(180deg,rgba(8,12,46,0),#080C2E)"></span>'
            f'<span class="abs sls num" style="left:14px;top:12px;width:30px;height:30px;display:grid;place-items:center;background:{LIGHT};color:{VDARK};font-size:15px">{{{{c.n}}}}</span>'
            f'<span class="abs cap" style="right:12px;top:14px;font-size:11px;padding:3px 8px;{PLATE};color:#C9CDEA">{{{{c.kind}}}}</span></span>'
            '<span style="display:flex;flex-direction:column;gap:8px;padding:12px 16px 14px;flex:1">'
            '<span style="font-size:18px;line-height:1.2">{{c.title}}</span>'
            '<span style="display:flex;gap:14px;align-items:baseline"><span class="num" style="font-size:20px">{{c.min}}</span>'
            f'<span class="cap num" style="font-size:12px;color:{SUB}">{{{{c.ly}}}} - {{{{c.i}}}} - {{{{c.pays}}}}</span></span>'
            '<span style="flex:1"></span>'
            f'<span style="display:flex;justify-content:space-between;align-items:center;border-top:1px solid {NEUT_D};padding-top:10px">'
            f'<span class="cap" style="font-size:11px;color:{NEUT_L}">Found by {{{{c.cs}}}}</span>'
            f'<span class="cap" style="font-size:11px;color:{LIGHT}">Open</span></span></span></button></sc-for>')
    return (f'<section aria-label="Voyages your crew found" style="position:absolute;left:{PX}px;top:{PY}px;width:{PW}px;height:{PH}px;'
            f'display:flex;flex-direction:column;gap:14px;opacity:{{{{vc.gridOp}}}};transition:opacity .32s">'
            f'<div style="display:flex;justify-content:space-between;align-items:baseline"><span class="lab" style="color:{LIGHT}">Voyages your crew found</span>'
            f'<span class="lab num">4</span></div>'
            f'<div style="display:grid;grid-template-columns:repeat(2, minmax(0, 1fr));gap:14px">{card}</div>'
            f'<div style="display:flex;align-items:center;gap:10px;padding-top:4px">{kc("A")}<span class="cap" style="font-size:12px;color:{SUB}">Open</span>'
            f'<span class="kc">{DPAD_L}</span><span class="kc">{DPAD_R}</span><span class="cap" style="font-size:12px;color:{SUB}">Move</span>'
            f'<span class="cap" style="font-size:12px;color:{NEUT_L}">- or press a voyage\'s pin on the HyperSea map</span></div></section>')


def route():
    node = ('<sc-for list="{{v.wps}}" as="w" hint-placeholder-count="5"><div style="position:relative;display:flex;flex-direction:column;align-items:center;gap:9px;flex:1;min-width:0">'
            '<sc-if value="{{w.last}}" hint-placeholder-val="{{ false }}"><span class="wpr" style="left:50%;top:6px;width:22px;height:22px;margin:-11px 0 0 -11px"></span></sc-if>'
            '<span style="height:13px;display:grid;place-items:center">'
            f'<sc-if value="{{{{w.first}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="wpn" style="width:13px;height:13px;background:{ROUTE};animation:none;box-shadow:0 0 12px 3px rgba(200,255,90,.6)"></span></sc-if>'
            '<sc-if value="{{w.mid}}" hint-placeholder-val="{{ true }}"><span class="wpn" style="width:9px;height:9px;animation-delay:{{w.delay}}"></span></sc-if>'
            '<sc-if value="{{w.last}}" hint-placeholder-val="{{ false }}"><span class="wpn" style="width:13px;height:13px;animation-delay:{{w.delay}}"></span></sc-if></span>'
            f'<span class="ell" style="max-width:100%;font-size:12px;color:#C9CDEA">{{{{w.name}}}}</span>'
            f'<sc-if value="{{{{w.first}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="cap" style="font-size:10px;color:{ARK_AMBER}">Your Ark</span></sc-if>'
            f'<sc-if value="{{{{w.last}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="cap" style="font-size:10px;color:{ROUTE}">Destination</span></sc-if></div></sc-for>')
    return (f'<div style="display:flex;flex-direction:column;gap:10px"><div style="display:flex;justify-content:space-between"><span class="lab" style="font-size:11px">Waypoints</span>'
            f'<span class="cap num" style="font-size:11px;color:{SUB}">{{{{v.min}}}} by Ark</span></div><div style="position:relative;display:flex">'
            f'<span class="abs" style="left:8%;right:8%;top:5px;height:2px;background:linear-gradient(90deg,{ROUTE},rgba(200,255,90,.22))"></span>'
            f'<span class="abs" style="left:8%;right:8%;top:1px;height:10px"><span class="wpb" style="top:0"></span></span>{node}</div></div>')


def presets():
    row = ('<sc-for list="{{v.presets}}" as="p" hint-placeholder-count="2"><button class="pick sls" type="button" onClick="{{p.pick}}" '
           'style="position:relative;display:flex;align-items:center;gap:12px;padding:8px 14px 8px 18px;background:rgba(8,12,40,.6);box-shadow:inset 0 0 0 1px #222645">'
           '<sc-if value="{{p.on}}" hint-placeholder-val="{{ false }}"><span class="abs" style="left:0;top:0;bottom:0;width:100%;box-shadow:inset 0 0 0 1px {{p.c}};background:rgba(14,22,70,.6)"></span>'
           '<span class="abs" style="left:0;top:10px;bottom:10px;width:3px;background:{{p.c}}"></span></sc-if>'
           '<span style="position:relative;display:flex;flex-direction:column;gap:3px;min-width:150px"><span style="font-size:17px">{{p.name}}</span>'
           f'<span style="display:flex;align-items:center;gap:6px"><span style="width:7px;height:7px;transform:rotate(45deg);background:{{{{p.c}}}}"></span>'
           f'<span class="cap" style="font-size:11px;color:{{{{p.cl}}}}">{{{{p.dom}}}}</span><span style="font-size:12px;color:{SUB}">{{{{p.line}}}}</span></span></span>'
           '<span style="flex:1"></span><span style="position:relative;display:flex;gap:6px">'
           '<img src="{{p.h0}}" alt="" style="width:80px;height:30px;object-fit:contain"><img src="{{p.h1}}" alt="" style="width:80px;height:30px;object-fit:contain">'
           '<img src="{{p.h2}}" alt="" style="width:80px;height:30px;object-fit:contain"></span></button></sc-for>')
    return (f'<div style="display:flex;flex-direction:column;gap:8px"><div style="display:flex;justify-content:space-between"><span class="lab" style="font-size:11px;color:{LIGHT}">Your Ark</span>'
            f'<span class="lab" style="font-size:11px">Choose one, or configure</span></div>{row}'
            f'<button class="btn gho sls" type="button" onClick="{{{{vc.toCrew}}}}" style="height:40px">{kc("X")}<span>Configure</span></button></div>')


def panel():
    head = (f'<div style="display:flex;align-items:center;gap:12px;padding-bottom:10px;border-bottom:1px solid {NEUT_D}">'
            f'<button class="pick" type="button" onClick="{{{{vc.close}}}}" style="display:flex;align-items:center;gap:8px">{kc("B")}'
            f'<span class="cap" style="font-size:13px;color:{NEUT_L}">Voyages</span></button><span style="flex:1"></span>'
            f'<button class="pick" type="button" aria-label="Previous voyage" onClick="{{{{vc.prev}}}}"><span class="kc" style="height:26px">{DPAD_L}</span></button>'
            f'<span class="cap num" style="font-size:13px;color:{SUB}">Voyage {{{{v.n}}}} of 4</span>'
            f'<button class="pick" type="button" aria-label="Next voyage" onClick="{{{{vc.next}}}}"><span class="kc" style="height:26px">{DPAD_R}</span></button></div>')
    crumbs = (f'<div style="display:flex;gap:18px">'
              + "".join(f'<button class="pick" type="button" onClick="{{{{vc.{h}}}}}"><span class="cap" style="font-size:12px;color:{{{{vc.{c}}}}}">{t}</span></button>'
                        for h, c, t in (("toVoyage", "c1", "1 Voyage"), ("toCrew", "c2", "2 Ark crew"), ("toStory", "c3", "3 Story")))
              + '</div>')
    l1 = (f'<sc-if value="{{{{vc.l1}}}}" hint-placeholder-val="{{{{ true }}}}"><div style="display:flex;flex-direction:column;gap:14px;flex:1">'
          f'<span class="lab">{{{{v.kind}}}} - found by {{{{v.cs}}}}</span><span style="font-size:30px;line-height:1.1">{{{{v.title}}}}</span>'
          f'<span style="font-size:16px;line-height:1.45;color:#C9CDEA">{{{{v.summary}}}}</span>'
          f'<div style="display:flex;align-items:flex-end;gap:26px"><span style="display:flex;flex-direction:column;gap:2px"><span class="num" style="font-size:26px;line-height:1">{{{{v.ly}}}}</span><span class="lab" style="font-size:10px">Course</span></span>'
          f'<span style="display:flex;flex-direction:column;gap:2px"><span class="num" style="font-size:26px;line-height:1">{{{{v.min}}}}</span><span class="lab" style="font-size:10px">Sail</span></span>'
          f'<span style="display:flex;flex-direction:column;gap:2px"><span class="num" style="font-size:26px;line-height:1">{{{{v.i}}}}</span><span class="lab" style="font-size:10px">Intensity</span></span>'
          f'<span class="cap num" style="font-size:12px;color:{SUB};padding-bottom:2px">Pays {{{{v.pays}}}} - odds {{{{v.odds}}}}</span></div>'
          f'<div style="display:flex;flex-direction:column;gap:6px"><div style="display:flex;justify-content:space-between"><span class="lab" style="font-size:11px">Course, cell by cell</span>'
          f'<span class="cap num" style="font-size:11px;color:{SUB}">{{{{v.seaText}}}}</span></div><div style="display:flex;gap:2px">'
          '<sc-for list="{{v.sea}}" as="s" hint-placeholder-count="3"><span style="flex:{{s.flex}};height:8px;background:{{s.color}}"></span></sc-for></div></div>'
          f'{route()}{presets()}<div style="flex:1"></div>'
          f'<div style="display:flex;gap:10px"><button class="btn cta sl" type="button" style="flex:1.4">{kc("A")}<span>Set sail</span></button>'
          f'<button class="btn sec sl" type="button" onClick="{{{{vc.toCrew}}}}" style="flex:1">{kc("X")}<span>Ark crew</span></button>'
          f'<button class="btn gho sl" type="button" onClick="{{{{vc.toStory}}}}" style="flex:1">{kc("Y")}<span>Story</span></button></div></div></sc-if>')
    seats = "".join(r43.seat_card(i, 220, dense=True) for i in range(3))
    l2 = (f'<sc-if value="{{{{vc.l2}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="display:flex;flex-direction:column;gap:14px;flex:1">'
          f'{r43.crew_head()}{r43.preset_tabs()}<div style="display:flex;gap:10px">{seats}</div><div style="flex:1"></div>'
          f'<div style="display:flex;gap:10px"><button class="btn cta sl" type="button" style="flex:1.4">{kc("A")}<span>Set sail</span></button>'
          f'<button class="btn sec sl" type="button" style="flex:1">{kc("X")}<span>Save as Ark</span></button>'
          f'<button class="btn gho sl" type="button" onClick="{{{{vc.toVoyage}}}}" style="flex:1">{kc("B")}<span>Voyage</span></button></div></div></sc-if>')
    story = "".join(f'<div class="rule" style="display:flex;flex-direction:column;gap:6px;padding:14px 0"><span class="lab" style="font-size:11px">{k}</span>'
                    f'<span style="font-size:15px;line-height:1.5;color:#C9CDEA">{v}</span></div>'
                    for k, v in (("Who brought it", "{{v.crew}}, {{v.cs}}, {{v.faction}}: {{v.crewLine}}"), ("The place", "{{v.place}}: {{v.placeLine}}"),
                                 ("The water", "{{v.light}} The thickest water is {{v.thick}}."), ("The lane", "{{v.lane}}")))
    l3 = (f'<sc-if value="{{{{vc.l3}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="display:flex;flex-direction:column;gap:8px;flex:1">'
          f'<div style="display:flex;gap:10px;align-items:center"><span class="lab">Story</span><span class="cap" style="font-size:11px;padding:2px 8px;box-shadow:inset 0 0 0 1px {NEUT};color:{NEUT_L}">Optional</span></div>'
          f'<span style="font-size:28px;line-height:1.1">{{{{v.title}}}}</span>{story}'
          f'<sc-if value="{{{{v.hasBacked}}}}" hint-placeholder-val="{{{{ false }}}}"><span class="cap" style="font-size:12px;color:{SUB}">Backed by: {{{{v.backed}}}}</span></sc-if>'
          f'<div style="flex:1"></div><div style="display:flex;gap:10px"><button class="btn cta sl" type="button" style="flex:1.4">{kc("A")}<span>Set sail</span></button>'
          f'<button class="btn gho sl" type="button" onClick="{{{{vc.toVoyage}}}}" style="flex:1">{kc("B")}<span>Voyage</span></button></div></div></sc-if>')
    return (f'<div style="position:absolute;left:{PX - 20}px;top:{PY}px;width:{PW + 40}px;height:{PH}px;overflow:hidden;pointer-events:none">'
            f'<section aria-label="The open voyage" class="sl" style="position:absolute;left:20px;top:0;width:{PW}px;height:{PH}px;box-sizing:border-box;padding:20px 26px 24px;'
            f'background:rgba(6,10,38,.97);box-shadow:inset 0 0 0 1px #2B3266;display:flex;flex-direction:column;gap:14px;pointer-events:auto;'
            f'transform:{{{{vc.panelX}}}};transition:transform .32s cubic-bezier(.22,1,.36,1)">{head}{crumbs}{l1}{l2}{l3}</section></div>')


def board(title, start):
    body = "\n".join([nav(), r44.tabrow("G", "Voyages"), explorer(), cards_grid(), panel()])
    page = r44.board(title, body)
    page = r43.with_glow(page)
    page = page.replace(r43.EMPTY_LOGIC, logic(start), 1)
    return page


STATES = [
    ("Prototype", "the prototype: press a card or a pin, zoom the map, step the levels", {"t": 2, "open": None, "level": 1}),
    ("Cards", "the voyages as four small cards, the HyperSea map at Sector with their pins", {"t": 2, "open": None, "level": 1}),
    ("Open", "a card slid open: Lantern Reach at level 1, the map on its course", {"t": 3, "open": 1, "level": 1}),
    ("FromMap", "opened from the map: The Maw's pin pressed at Sector, its card slid open", {"t": 2, "open": 3, "level": 1}),
    ("Crew", "the open voyage at level 2, Ark crew: the 12 settings", {"t": 3, "open": 1, "level": 2}),
    ("Story", "the open voyage at level 3, its story (optional): The Maw", {"t": 3, "open": 3, "level": 3}),
]


def notes():
    def para(t):
        return f'<p style="margin:0;font-size:17px;line-height:1.5;color:#C9CDEA">{t}</p>'
    left = "".join([
        '<span class="lab">Asked (Shombith)</span>',
        para('"Rather than big cards make smaller cards like arena maybe and they slide open when we click them or select them in the map. With this design move ahead."'),
        '<span class="lab" style="margin-top:10px">What it is</span>',
        para("The four voyages your crew found are small cards, two by two, like the Arena's game cards: the Sector render around the destination as art, "
             "the number, the kind, the title, the sail time, the course, the intensity, the pay, and who found it. Press a card (A, or a click) and the voyage "
             "slides open over the cards, 0.32 s, with the map moving to its course. Press a voyage's pin on the HyperSea map at Sector and the same card slides "
             "open while the map keeps the pin ringed. B slides it shut; D-pad left / right steps to the next voyage without closing."),
        para("Inside the open voyage the three levels stay: the voyage (with Your Ark), Ark crew (the 12 settings), Story (optional). "
             "The window keeps its four tabs on LB / RB and the sea lines; the map keeps Garrett's seven tiers."),
    ])
    right = "".join([
        '<span class="lab">Built from</span>',
        para("Every voyage's words and numbers are the Voyage Board's (its four voyages: the crew line, the course cell by cell, odds, the place, the lane) and "
             "the slice's (job summaries). Each card opens onto its own course render and edge profile. The lit waypoints on the map are drawn for Lantern Reach; "
             "the others light Tumult and their destination."),
        '<span class="lab" style="margin-top:10px">What the build changes</span>',
        para("Garrett's window (author_voyage_window.py): SAGA / EPISODES buttons become the four tabs; the three-across grid of job cards becomes two by two "
             "small cards on the right of the map; the open card (VoyageJobCard.Overlay) becomes the slide-in voyage with its three levels; "
             "a voyage pin on the map (VoyageMapGraphic) opens the same card. The map and the edge view stay his."),
    ])
    return (f'<div class="abs" style="left:60px;top:44px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:30px">Small voyage cards that slide open, from the card or the map</span></div>'
            f'<div class="abs pane" style="left:40px;top:130px;width:880px;padding:20px;box-sizing:border-box;display:flex;flex-direction:column;gap:12px">{left}</div>'
            f'<div class="abs pane" style="left:960px;top:130px;width:920px;padding:20px;box-sizing:border-box;display:flex;flex-direction:column;gap:12px">{right}</div>')


def main():
    files = {}
    for key, what, start in STATES:
        page = board(f"Rev {REV} {key} - {what}", start)
        files[f"R{REV}{key}.dc.html"] = page
    files[f"R{REV}Notes.dc.html"] = r44.board(f"Rev {REV} - notes", notes())
    for n, s in files.items():
        (ROOT / n).write_text(s, encoding="utf-8")
        print(f"{n:28} {len(s):>7}")


if __name__ == "__main__":
    main()
