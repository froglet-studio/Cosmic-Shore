#!/usr/bin/env python3
"""Rev 71 boards: six new Voyage windows on the menu's own ground, and the Quests window (Frogma tab "Voyages").

Shombith, 2026-11-08, on Rev 70: "Dont like any one of them too bland thats. Do something new, delete all these give
me 6 new, do not use the ark bg use the normal transparent bg like hangar and ecology for example only ark gets that
treatment."

Every board stands on the menu's ground: the menu cell under a Deep Sea window and the slow sea lines, as the Hangar
and the Codex do (only the Ark wears its room). Each option is a different idea, not a re-skin:
  A Dial         the course through a round lens, the voyages as bearings on its ring
  B Departures   a departures board, the open voyage as a boarding pass with the squad on its stub
  C Coverflow    the voyages as tilted cards on a carousel, the picked one faced front
  D Hex board    the HyperSea's own cells: the Ark in the middle hex, each voyage a hex holding its course
  E Journey      the route as a band of map across the window, its edge-on profile under it
  F Crew's hand  the crew deals the voyages as a hand of cards; the picked card rises
Every option carries the two fixes of Rev 70: a squad hull opens the vessel strip on its seat (one press picks), and
picking another squad never changes the voyage on screen. The kit, the art and the data are Rev 70's
(Docs/MenuRedesignB/rounds/r70_voyages.py).

    python3 Docs/MenuRedesignB/rounds/r71_voyages.py <root> 71
"""
import importlib.util
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "71"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r70", HERE / "r70_voyages.py")
r70 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r70)

ROOT, REV = r70.ROOT, int(_rev)
blob, page, nav, tabrow, SEA_LINES, LOCK = r70.blob, r70.page, r70.nav, r70.tabrow, r70.SEA_LINES, r70.LOCK
HULL, SQUADS, DOMC, QUESTS = r70.HULL, r70.SQUADS, r70.DOMC, r70.QUESTS

MENU_CELL = blob("391619dde523174b4a9ead64e169b67a")   # the menu cell under every window (Rev 54's Showroom ground)
EDGES = ["cd62f3bbf1d64b1e7f1f4739209aaa74", "25c96499cc959b94655e2a5a3362228e",
         "58696b3b52c011054a8b024f94aff2e1", "e8de257ad245f412b429c6d58363dc57"]   # Rev 46's edge-on views
VOYAGES = [dict(v, edge=blob(e)) for v, e in zip(r70.VOYAGES, EDGES)]
TABS = [("Voyages", False), ("Saga", False), ("Episodes", True)]

GROUND = ('<img src="' + MENU_CELL + '" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover">'
          '<div class="abs" style="left:0;top:84px;width:1920px;height:996px;background:radial-gradient(ellipse 70% 55% at 50% 28%,'
          'rgba(117,143,251,.14),rgba(117,143,251,0) 70%),linear-gradient(180deg,rgba(34,38,69,.9) 0%,rgba(0,4,31,.92) 48%,'
          'rgba(0,1,10,.95) 100%)"></div>' + SEA_LINES)

DATA = ("const V = " + json.dumps(VOYAGES) + ";\nconst SQ = " + json.dumps(SQUADS) + ";\nconst HULL = " + json.dumps(HULL)
        + ";\nconst DOMC = " + json.dumps(DOMC) + ";\nconst HULLS = " + json.dumps(list(HULL)) + ";\n")

# The values every option shares: the voyage, the squad (presets, formation, the vessel strip), its own picks.
VALS = """
  const st = this.state, v = V[st.v], seats = st.seats;
  const copy = (q) => q.seats.map((s) => Object.assign({}, s));
  const voyage = { n: v.n, title: v.title, line: v.line, place: v.place, placeLine: v.placeLine, map: v.map, pos: v.pos,
    edge: v.edge, ly: v.ly, min: v.min, intensity: 'Intensity ' + v.i, crew: v.crew,
    kicker: 'Voyage ' + v.n + ' of ' + V.length + ' - brought by ' + v.crew, from: 'From Tumult',
    course: 'Tumult to ' + v.place + ' - ' + v.wps + ' waypoints' };
  const presets = SQ.map((s, i) => ({ name: s.name, pressed: i === st.sq ? 'true' : 'false', col: i === st.sq ? '#E6E9FF' : '#747BAD',
    edge: 'inset 3px 0 0 ' + DOMC[s.dom] + (i === st.sq ? ', inset 0 0 0 1px #00D4FF' : ', inset 0 0 0 1px rgba(116,123,173,.45)'),
    pick: () => this.setState({ sq: i, seats: copy(SQ[i]), open: -1 }) }));
  const formation = seats.map((s, i) => ({ hull: HULL[s.v], alt: s.role + ': ' + s.v + ', press to change',
    edge: st.open === i ? 'inset 0 0 0 2px #00D4FF' : 'none', pressed: st.open === i ? 'true' : 'false',
    pick: () => this.setState({ open: st.open === i ? -1 : i }) }));
  const crewLine = seats.map((s) => (s.who === 'AI wingmate' ? 'AI' : s.who) + ' - ' + s.v).join('     ');
  let strip = { open: false, caption: '', items: [] };
  if (st.open >= 0) {
    const s = seats[st.open];
    strip = { open: true, caption: s.role + ' - vessel: one press picks',
      items: HULLS.map((h) => ({ name: h, img: HULL[h], pressed: s.v === h ? 'true' : 'false',
        edge: s.v === h ? 'inset 0 0 0 2px #00D4FF' : 'none',
        pick: () => this.setState({ seats: seats.map((x, j) => j === st.open ? Object.assign({}, x, { v: h }) : x), open: -1 }) })) };
  }
  const pickV = (i) => () => this.setState({ v: i, open: -1 });
"""
START = "this.state = { v: 1, sq: 0, seats: SQ[0].seats.map((s) => Object.assign({}, s)), open: -1 };"
RET = "voyage, presets, formation, crewLine, strip"


def logic(extra_vals="", extra_ret=""):
    return DATA + ("class Component extends DCLogic {\nconstructor(p) { super(p); " + START + " }\nrenderVals() {"
                   + VALS + extra_vals + "  return { " + RET + (", " + extra_ret if extra_ret else "") + " };\n}\n}")


def squad(x, y, w, hull_h=96, strip_x=None, strip_y=None, strip_w=None):
    """The squad: the presets, the three hulls (a press opens the vessel strip on that seat) and who flies what."""
    sx, sy, sw = strip_x if strip_x is not None else x, strip_y if strip_y is not None else y - 236, strip_w or w
    return f"""<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:12px">
<div style="display:flex;gap:10px"><sc-for list="{{{{presets}}}}" as="p" hint-placeholder-count="2"><button class="pick sls" type="button" aria-pressed="{{{{p.pressed}}}}" onClick="{{{{p.pick}}}}" style="padding:10px 16px;background:rgba(0,4,31,.7);box-shadow:{{{{p.edge}}}};font-family:'Chakra Petch';font-weight:600;font-size:14px;letter-spacing:.12em;text-transform:uppercase;color:{{{{p.col}}}}">{{{{p.name}}}}</button></sc-for></div>
<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:10px"><sc-for list="{{{{formation}}}}" as="f" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{{{f.pressed}}}}" aria-label="{{{{f.alt}}}}" onClick="{{{{f.pick}}}}" style="height:{hull_h}px;display:flex;align-items:center;justify-content:center;box-shadow:{{{{f.edge}}}}"><img class="hull" src="{{{{f.hull}}}}" alt="{{{{f.alt}}}}" style="width:100%;height:{hull_h - 8}px"></button></sc-for></div>
<span style="font-size:15px;color:#E6E9FF;white-space:pre">{{{{crewLine}}}}</span>
</div>
<sc-if value="{{{{strip.open}}}}" hint-placeholder-val="{{{{ false }}}}"><div class="abs glass sl" style="left:{sx}px;top:{sy}px;width:{sw}px;height:220px;box-sizing:border-box;padding:16px 20px;z-index:8;box-shadow:inset 0 0 0 1px #00D4FF;font-family:'Chakra Petch';font-weight:600;font-size:14px;letter-spacing:.14em;text-transform:uppercase;color:#E6E9FF">{{{{strip.caption}}}}<div style="display:flex;gap:10px;margin-top:12px"><sc-for list="{{{{strip.items}}}}" as="t" hint-placeholder-count="6"><button class="pick sls" type="button" aria-label="{{{{t.name}}}}" aria-pressed="{{{{t.pressed}}}}" onClick="{{{{t.pick}}}}" style="flex:1;height:150px;display:flex;align-items:center;justify-content:center;box-shadow:{{{{t.edge}}}}"><img class="hull" src="{{{{t.img}}}}" alt="{{{{t.name}}}}" style="width:100%;height:110px"></button></sc-for></div></div></sc-if>"""


SAIL = '<a class="btn commit abs" href="#" style="left:{x}px;top:{y}px;width:{w}px"><span class="kc">A</span>Set sail</a>'


def frame(body, title, extra_vals="", extra_ret="", tabs=TABS, lit="Voyages"):
    return page(title, GROUND + nav() + tabrow(tabs, lit) + body, logic(extra_vals, extra_ret))


def stats(size=28, gap=34):
    return (f'<div class="num" style="display:flex;gap:{gap}px;font-size:{size}px"><span>{{{{voyage.ly}}}}</span>'
            '<span>{{voyage.min}}</span><span>{{voyage.intensity}}</span></div>')


# ── A: Dial ───────────────────────────────────────────────────────────────────────────────────────────────────────────
def a_board():
    cx, cy, r = 400, 400, 372
    ticks = "".join(
        f'<line x1="{cx + (r + 6) * math.cos(t):.1f}" y1="{cy + (r + 6) * math.sin(t):.1f}" '
        f'x2="{cx + (r + (18 if k % 6 == 0 else 11)) * math.cos(t):.1f}" y2="{cy + (r + (18 if k % 6 == 0 else 11)) * math.sin(t):.1f}" '
        f'stroke="#747BAD" stroke-width="{2 if k % 6 == 0 else 1}"></line>'
        for k in range(72) for t in [k * math.pi / 36])
    dial = f"""<svg aria-hidden="true" width="820" height="820" viewBox="-10 -10 820 820" style="position:absolute;left:150px;top:196px">
<defs><clipPath id="r71lens"><circle cx="{cx}" cy="{cy}" r="{r}"></circle></clipPath>
<radialGradient id="r71vig"><stop offset="0.62" stop-color="#00041F" stop-opacity="0"></stop><stop offset="1" stop-color="#00041F" stop-opacity="0.85"></stop></radialGradient></defs>
<image href="{{{{voyage.map}}}}" x="{cx - r}" y="{cy - r}" width="{2 * r}" height="{2 * r}" preserveAspectRatio="xMidYMid slice" clip-path="url(#r71lens)"></image>
<circle cx="{cx}" cy="{cy}" r="{r}" fill="url(#r71vig)"></circle>
<circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="#434C89" stroke-width="2"></circle>
<circle cx="{cx}" cy="{cy}" r="{r + 30}" fill="none" stroke="#434C89" stroke-width="1"></circle>
{ticks}
<path d="M{cx} {cy - 14}L{cx + 10} {cy}L{cx} {cy + 14}L{cx - 10} {cy}Z" fill="#E6E9FF"></path>
</svg>"""
    marks = []
    for i, deg in enumerate((-142, -96, -50, -4)):
        t = math.radians(deg)
        x = 150 + 10 + cx + (r + 30) * math.cos(t)
        y = 196 + 10 + cy + (r + 30) * math.sin(t)
        marks.append(f'<button class="pick sls abs" type="button" aria-pressed="{{{{bear.p{i}}}}}" onClick="{{{{bear.k{i}}}}}" '
                     f'style="left:{x - 34:.0f}px;top:{y - 22:.0f}px;width:68px;height:44px;display:flex;align-items:center;justify-content:center;'
                     f'background:{{{{bear.b{i}}}}};box-shadow:{{{{bear.e{i}}}}};font-family:\'Chakra Petch\';font-weight:700;font-size:18px;'
                     f'color:{{{{bear.c{i}}}}}">0{i + 1}</button>')
    right = """<div class="abs" style="left:1080px;top:200px;width:800px;display:flex;flex-direction:column;gap:16px">
<span class="lab">{{voyage.kicker}}</span>
<span style="font-size:48px;line-height:1.05">{{voyage.title}}</span>
<span style="font-size:18px;line-height:1.45;color:#747BAD;max-width:700px">{{voyage.line}}</span>
""" + stats() + "</div>"
    body = dial + "".join(marks) + right + squad(1080, 560, 800, 130, 1080, 324, 800) + SAIL.format(x=1080, y=990, w=800)
    ev = """  const bear = {};
  V.forEach((x, i) => { const on = i === st.v; bear['p' + i] = on ? 'true' : 'false'; bear['k' + i] = pickV(i);
    bear['b' + i] = on ? '#00D4FF' : 'rgba(0,4,31,.85)'; bear['c' + i] = on ? '#00041F' : '#E6E9FF';
    bear['e' + i] = on ? 'none' : 'inset 0 0 0 1px #747BAD'; });
"""
    return frame(body, f"Rev {REV} A - Dial: the course through a round lens, the voyages as bearings on its ring - interactive", ev, "bear")


# ── B: Departures ─────────────────────────────────────────────────────────────────────────────────────────────────────
def b_board():
    board = """<span class="abs lab" style="left:40px;top:184px">Departures from Tumult</span>
<div class="abs" style="left:40px;top:220px;width:1000px;display:flex;flex-direction:column;gap:14px">
<sc-for list="{{rows}}" as="r" hint-placeholder-count="4"><button class="pick sls" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}" style="height:150px;box-sizing:border-box;padding:24px 28px;display:flex;flex-direction:column;justify-content:space-between;background:{{r.bg}};box-shadow:{{r.edge}};font-size:28px;color:#E6E9FF">{{r.title}}<span class="cap num" style="display:block;font-size:18px;letter-spacing:.2em;color:{{r.subCol}}">{{r.sub}}</span></button></sc-for>
</div>"""
    ticket = """<div class="abs" style="left:1100px;top:196px;width:780px;height:860px;box-sizing:border-box;padding:36px 40px;background:rgba(0,4,31,.94);clip-path:polygon(16px 0,100% 0,100% 520px,calc(100% - 18px) 538px,100% 556px,100% calc(100% - 16px),calc(100% - 16px) 100%,0 100%,0 556px,18px 538px,0 520px,0 16px);display:flex;flex-direction:column;gap:14px">
<span class="lab">{{voyage.kicker}}</span>
<span class="cap" style="font-size:18px;color:#747BAD">{{voyage.from}}</span>
<span style="font-size:48px;line-height:1.05">{{voyage.place}}</span>
""" + stats(28, 30) + """
<img src="{{voyage.map}}" alt="The course on the HyperSea map" style="width:100%;height:190px;object-fit:cover;object-position:{{voyage.pos}};filter:brightness(.85)">
<div style="position:absolute;left:40px;right:40px;top:538px;border-top:2px dashed #434C89"></div>
</div>"""
    body = board + ticket + squad(1140, 756, 700, 80, 1100, 500, 780) + SAIL.format(x=1140, y=976, w=700)
    ev = """  const rows = V.map((x, i) => { const on = i === st.v; return { title: x.title, pressed: on ? 'true' : 'false', pick: pickV(i),
    sub: x.place + '   ' + x.ly + '   ' + x.min + (on ? '   Boarding' : ''), subCol: on ? '#00D4FF' : '#747BAD',
    bg: on ? 'rgba(34,38,69,.94)' : 'rgba(0,4,31,.82)', edge: on ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px rgba(116,123,173,.35)' }; });
"""
    return frame(body, f"Rev {REV} B - Departures: the voyages on a departures board, the open voyage a boarding pass with your squad on its stub - interactive", ev, "rows")


# ── C: Coverflow ──────────────────────────────────────────────────────────────────────────────────────────────────────
def c_board():
    stage = """<div class="abs" style="left:0;top:190px;width:1920px;height:520px;perspective:1600px">
<sc-for list="{{flow}}" as="c" hint-placeholder-count="4"><button class="pick sl" type="button" aria-pressed="{{c.pressed}}" aria-label="{{c.title}}" onClick="{{c.pick}}" style="position:absolute;left:660px;top:30px;width:600px;height:420px;overflow:hidden;background:#00041F;box-shadow:{{c.edge}};transform:{{c.tf}};opacity:{{c.op}};z-index:{{c.z}};transition:transform 320ms cubic-bezier(.22,1,.36,1),opacity 320ms"><img src="{{c.map}}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{{c.pos}};filter:brightness(.85)"><span style="position:absolute;left:0;right:0;bottom:0;padding:60px 26px 22px;background:linear-gradient(180deg,rgba(0,4,31,0),rgba(0,4,31,.92));font-size:22px;line-height:1.25;color:#E6E9FF">{{c.title}}</span></button></sc-for>
</div>
<span class="kc abs" style="left:520px;top:432px">LB</span><span class="kc abs" style="left:1372px;top:432px">RB</span>
<div class="abs" style="left:360px;top:720px;width:1200px;display:flex;flex-direction:column;align-items:center;gap:12px;text-align:center">
<span class="lab">{{voyage.kicker}}</span>
<span style="font-size:20px;line-height:1.45;color:#E6E9FF">{{voyage.line}}</span>
""" + stats(28, 40) + "</div>"
    body = stage + squad(40, 850, 620, 90, 40, 610, 1000) + SAIL.format(x=1480, y=990, w=400)
    ev = """  const flow = V.map((x, i) => { const d = i - st.v, a = Math.abs(d); return { title: x.title, map: x.map, pos: x.pos, pick: pickV(i),
    pressed: d === 0 ? 'true' : 'false', z: 10 - a, op: a === 0 ? 1 : a === 1 ? 0.75 : 0.4,
    tf: 'translateX(' + (d * 470) + 'px) rotateY(' + (-d * 38) + 'deg) scale(' + (1 - Math.min(a, 2) * 0.16) + ')',
    edge: d === 0 ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89' }; });
"""
    return frame(body, f"Rev {REV} C - Coverflow: the voyages as tilted cards on a carousel, the picked one faced front, LB / RB turn it - interactive", ev, "flow")


# ── D: Hex board ──────────────────────────────────────────────────────────────────────────────────────────────────────
def hexpath(cx, cy, r):
    pts = [(cx + r * math.cos(math.radians(60 * k)), cy + r * math.sin(math.radians(60 * k))) for k in range(6)]
    return "M" + "L".join(f"{x:.1f} {y:.1f}" for x, y in pts) + "Z"


def d_board():
    W, H, R = 1200, 880, 46
    grid = []
    for row in range(-1, int(H / (R * 1.732)) + 2):
        for col in range(-1, int(W / (R * 1.5)) + 2):
            x = col * R * 1.5
            y = row * R * 1.732 + (R * 0.866 if col % 2 else 0)
            grid.append(hexpath(x, y, R - 2))
    ark = (600, 440)
    spots = [(330, 230), (880, 220), (300, 660), (900, 670)]
    big = 150
    clips = "".join(f'<clipPath id="r71h{i}"><path d="{hexpath(x, y, big)}"></path></clipPath>' for i, (x, y) in enumerate(spots))
    hexes = "".join(
        f'<image href="{VOYAGES[i]["map"]}" x="{x - big}" y="{y - big}" width="{2 * big}" height="{2 * big}" preserveAspectRatio="xMidYMid slice" clip-path="url(#r71h{i})" opacity="{{{{hx.o{i}}}}}"></image>'
        f'<path d="{hexpath(x, y, big)}" fill="none" stroke="{{{{hx.s{i}}}}}" stroke-width="{{{{hx.w{i}}}}}"></path>'
        for i, (x, y) in enumerate(spots))
    svg = f"""<svg aria-hidden="true" width="{W}" height="{H}" style="position:absolute;left:40px;top:180px">
<defs>{clips}</defs>
<path d="{''.join(grid)}" fill="none" stroke="#222645" stroke-width="1.5"></path>
<line x1="{ark[0]}" y1="{ark[1]}" x2="{{{{hx.tx}}}}" y2="{{{{hx.ty}}}}" stroke="#E6E9FF" stroke-width="3" stroke-dasharray="10 8"></line>
{hexes}
<path d="{hexpath(ark[0], ark[1], 64)}" fill="#222645" stroke="#747BAD" stroke-width="2"></path>
<path d="M{ark[0]} {ark[1] - 18}L{ark[0] + 13} {ark[1]}L{ark[0]} {ark[1] + 18}L{ark[0] - 13} {ark[1]}Z" fill="#E6E9FF"></path>
</svg>"""
    labels = "".join(
        f'<button class="pick abs" type="button" aria-pressed="{{{{hx.p{i}}}}}" aria-label="{VOYAGES[i]["title"]}" onClick="{{{{hx.k{i}}}}}" '
        f'style="left:{40 + x - big}px;top:{180 + y - big}px;width:{2 * big}px;height:{2 * big}px"></button>'
        f'<span class="abs" style="left:{40 + x - 170}px;top:{180 + y + big + 8}px;width:340px;text-align:center;font-size:18px;line-height:1.3;color:{{{{hx.c{i}}}}}">{VOYAGES[i]["place"]} - {VOYAGES[i]["min"]}</span>'
        for i, (x, y) in enumerate(spots))
    ark_label = f'<span class="abs cap" style="left:{40 + ark[0] - 60}px;top:{180 + ark[1] + 72}px;width:120px;text-align:center;font-size:14px;color:#E6E9FF">The Ark</span>'
    right = """<div class="abs glass sl" style="left:1280px;top:190px;width:600px;height:400px;box-sizing:border-box;padding:30px;display:flex;flex-direction:column;gap:14px">
<span class="lab">{{voyage.kicker}}</span>
<span style="font-size:36px;line-height:1.1">{{voyage.title}}</span>
<span style="font-size:18px;line-height:1.45;color:#747BAD">{{voyage.line}}</span>
""" + stats(28, 30) + "</div>"
    body = svg + labels + ark_label + right + squad(1280, 640, 600, 96, 1280, 404, 600) + SAIL.format(x=1280, y=990, w=600)
    ev = "  const SPOTS = " + json.dumps(spots) + """;
  const hx = { tx: SPOTS[st.v][0], ty: SPOTS[st.v][1] };
  V.forEach((x, i) => { const on = i === st.v; hx['p' + i] = on ? 'true' : 'false'; hx['k' + i] = pickV(i);
    hx['o' + i] = on ? 1 : 0.55; hx['s' + i] = on ? '#00D4FF' : '#434C89'; hx['w' + i] = on ? 5 : 2; hx['c' + i] = on ? '#E6E9FF' : '#747BAD'; });
"""
    return frame(body, f"Rev {REV} D - Hex board: the HyperSea's own cells, the Ark in the middle hex, each voyage a hex holding its course (recommended) - interactive", ev, "hx")


# ── E: Journey ────────────────────────────────────────────────────────────────────────────────────────────────────────
def e_board():
    head = """<div class="abs" style="left:40px;top:184px;width:1300px;display:flex;flex-direction:column;gap:12px">
<span class="lab">{{voyage.kicker}}</span>
<span style="font-size:48px;line-height:1.05">{{voyage.title}}</span>
""" + stats(28, 34) + "</div>"
    band = """<div class="abs sl" style="left:40px;top:396px;width:1840px;height:270px;overflow:hidden;box-shadow:inset 0 0 0 1px #434C89"><img src="{{voyage.map}}" alt="The course on the HyperSea map" style="width:100%;height:100%;object-fit:cover;object-position:{{voyage.pos}}"></div>
<img src="{{voyage.edge}}" alt="The course seen edge on: how it rises and falls through the disk" style="position:absolute;left:40px;top:680px;width:1840px;height:150px;object-fit:cover">"""
    tabs = """<div class="abs" style="left:40px;top:858px;width:1200px;display:grid;grid-template-columns:repeat(4,1fr);gap:14px">
<sc-for list="{{legs}}" as="r" hint-placeholder-count="4"><button class="pick sls" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}" style="height:180px;box-sizing:border-box;padding:18px 20px 0;display:flex;flex-direction:column;justify-content:space-between;background:rgba(0,4,31,.82);box-shadow:{{r.edge}};font-size:18px;line-height:1.3;color:#E6E9FF">{{r.title}}<span style="display:block;padding-bottom:16px;font-size:15px;color:#747BAD">{{r.sub}}</span></button></sc-for>
</div>"""
    body = (head + band + tabs + '<a class="btn commit abs" href="#" style="left:1480px;top:200px;width:400px"><span class="kc">A</span>Set sail</a>'
            + squad(1280, 858, 600, 86, 1280, 620, 600))
    ev = """  const legs = V.map((x, i) => ({ title: x.title, sub: x.place + ' - ' + x.min, pressed: i === st.v ? 'true' : 'false', pick: pickV(i),
    edge: i === st.v ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px rgba(116,123,173,.45)' }));
"""
    return frame(body, f"Rev {REV} E - Journey: the route as a band of map across the window, its edge-on profile under it - interactive", ev, "legs")


# ── F: Crew's hand ────────────────────────────────────────────────────────────────────────────────────────────────────
def f_board():
    head = """<div class="abs" style="left:40px;top:190px;width:1100px;display:flex;flex-direction:column;gap:14px">
<span class="lab">{{voyage.kicker}}</span>
<span style="font-size:48px;line-height:1.05">{{voyage.title}}</span>
<span style="font-size:18px;line-height:1.45;color:#747BAD;max-width:900px">{{voyage.line}}</span>
""" + stats(28, 34) + "</div>"
    hand = """<div class="abs" style="left:0;top:560px;width:1240px;height:520px">
<sc-for list="{{hand}}" as="c" hint-placeholder-count="4"><button class="pick sl" type="button" aria-pressed="{{c.pressed}}" aria-label="{{c.title}}" onClick="{{c.pick}}" style="position:absolute;left:{{c.x}};top:80px;width:280px;height:400px;overflow:hidden;background:#00041F;box-shadow:{{c.edge}};transform-origin:50% 140%;transform:{{c.tf}};z-index:{{c.z}};transition:transform 320ms cubic-bezier(.22,1,.36,1)"><img src="{{c.map}}" alt="" style="width:100%;height:250px;object-fit:cover;object-position:{{c.pos}};display:block"><span style="display:block;padding:16px 18px 0;font-size:20px;line-height:1.3;color:#E6E9FF">{{c.title}}</span></button></sc-for>
</div>"""
    body = head + hand + squad(1300, 620, 580, 100, 1300, 380, 580) + SAIL.format(x=1300, y=990, w=580)
    ev = """  const hand = V.map((x, i) => { const on = i === st.v, k = i - 1.5;
    return { title: x.title, map: x.map, pos: x.pos, pick: pickV(i), pressed: on ? 'true' : 'false', x: (470 + k * 230) + 'px',
      z: on ? 10 : 4 - Math.abs(k), edge: on ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89',
      tf: 'rotate(' + (k * 7) + 'deg)' + (on ? ' translateY(-90px) scale(1.05)' : '') }; });
"""
    return frame(body, f"Rev {REV} F - Crew's hand: your crew deals the voyages as a hand of cards; the picked card rises - interactive", ev, "hand")


# ── Quests ────────────────────────────────────────────────────────────────────────────────────────────────────────────
def quests_board():
    pts = [(260, 560), (600, 400), (960, 520), (1320, 380), (1660, 500)]
    path = "M" + " ".join(f"{x} {y}" if k == 0 else f"C{(pts[k - 1][0] + x) / 2} {pts[k - 1][1]} {(pts[k - 1][0] + x) / 2} {y} {x} {y}"
                          for k, (x, y) in enumerate(pts))
    trail = (f'<svg aria-hidden="true" width="1920" height="1080" style="position:absolute;left:0;top:0">'
             f'<path d="{path}" fill="none" stroke="#434C89" stroke-width="3" stroke-dasharray="2 12" stroke-linecap="round"></path>'
             f'<path d="{path}" fill="none" stroke="#E6E9FF" stroke-width="3" pathLength="100" stroke-dasharray="{{{{lit}}}} 100"></path></svg>')
    meds = "".join(
        f'<button class="pick oct abs" type="button" aria-pressed="{{{{q.p{i}}}}}" aria-label="{QUESTS[i]["name"]}" onClick="{{{{q.k{i}}}}}" '
        f'style="left:{x - 110}px;top:{y - 110}px;width:220px;height:220px;padding:0;overflow:hidden;background:{{{{q.ring{i}}}}};clip-path:polygon(29% 0,71% 0,100% 29%,100% 71%,71% 100%,29% 100%,0 71%,0 29%)">'
        f'<img src="{QUESTS[i]["art"]}" alt="" style="position:absolute;inset:6px;width:208px;height:208px;object-fit:cover;opacity:{{{{q.o{i}}}}};clip-path:polygon(29% 0,71% 0,100% 29%,100% 71%,71% 100%,29% 100%,0 71%,0 29%)"></button>'
        f'<span class="abs" style="left:{x - 150}px;top:{y + 122}px;width:300px;text-align:center;font-size:20px;color:{{{{q.c{i}}}}}">{QUESTS[i]["n"]}  {QUESTS[i]["name"]}</span>'
        f'<span class="abs cap" style="left:{x - 150}px;top:{y + 156}px;width:300px;text-align:center;font-size:14px;color:{{{{q.c{i}}}}}">{{{{q.l{i}}}}}</span>'
        for i, (x, y) in enumerate(pts))
    head = """<div class="abs" style="left:40px;top:186px;right:40px;display:flex;align-items:flex-end;justify-content:space-between">
<span style="font-size:48px;line-height:1">The Hypersea</span>
<span class="num" style="font-size:22px">{{progress}}</span>
</div>"""
    focus = """<div class="abs glass sl" style="left:40px;top:860px;width:1840px;height:170px;box-sizing:border-box;padding:28px 36px;display:flex;align-items:center;justify-content:space-between">
<div style="display:flex;flex-direction:column;gap:10px"><span style="font-size:28px">{{focus.title}}</span><span style="font-size:18px;color:#747BAD">{{focus.goal}}</span></div>
<sc-if value="{{focus.claim}}" hint-placeholder-val="{{ false }}"><button class="btn cta" type="button" onClick="{{focus.act}}"><span class="kc">A</span>Claim</button></sc-if>
<sc-if value="{{focus.play}}" hint-placeholder-val="{{ true }}"><button class="btn cta" type="button"><span class="kc">A</span>Play quest</button></sc-if>
<sc-if value="{{focus.wait}}" hint-placeholder-val="{{ false }}"><span class="cap" style="font-size:18px;color:#747BAD">{{focus.waitLine}}</span></sc-if>
</div>"""
    body = GROUND + nav() + tabrow([("Episode 1", False), ("Episode 2", True)], "Episode 1") + head + trail + meds + focus
    lg = "const Q = " + json.dumps(QUESTS) + """;
class Component extends DCLogic {
constructor(p) { super(p); this.state = { states: Q.map((q) => q.state), sel: 2 }; }
renderVals() {
  const st = this.state, s = st.states;
  const claimed = s.filter((x) => x === 'claimed').length;
  const line = (x, i) => x === 'claimed' ? 'Claimed' : x === 'done' ? 'Done - claim it' : x === 'open' ? 'Next to play' : 'After ' + Q[i - 1].name;
  const q = {};
  Q.forEach((x, i) => { const on = i === st.sel;
    q['p' + i] = on ? 'true' : 'false'; q['k' + i] = () => this.setState({ sel: i });
    q['ring' + i] = on ? '#00D4FF' : s[i] === 'locked' ? '#222645' : '#747BAD';
    q['o' + i] = s[i] === 'locked' ? 0.35 : 1; q['c' + i] = s[i] === 'locked' ? '#747BAD' : '#E6E9FF'; q['l' + i] = line(s[i], i); });
  const k = s[st.sel], cur = Q[st.sel];
  const act = () => { if (k === 'done') { const n = s.slice(); n[st.sel] = 'claimed'; this.setState({ states: n }); } };
  const focus = { title: cur.n + '  ' + cur.name, goal: cur.goal, claim: k === 'done', play: k === 'open', wait: k === 'locked' || k === 'claimed',
    waitLine: k === 'claimed' ? 'Claimed' : 'Unlocks ' + line(k, st.sel).toLowerCase(), act };
  const reached = s.filter((x) => x !== 'locked').length;
  return { q, focus, progress: claimed + ' of ' + Q.length + ' claimed', lit: Math.round(100 * Math.max(0, reached - 1) / (Q.length - 1)) };
}
}"""
    return page(f"Rev {REV} Quests - Quests, its own window on the menu's ground: Episode 1's five quests on one trail, the next one lit - interactive", body, lg)


def notes_board():
    blocks = [
        ("Asked", "Shombith, 2026-11-08, on Rev 70: too bland; something new; six new options; the menu's own ground, as the Hangar and the Codex have, not the Ark's room (only the Ark wears that)."),
        ("Six ideas", "A Dial, a round lens on the course with bearings on its ring. B Departures, a board and a boarding pass. C Coverflow, tilted cards. D Hex board, the HyperSea's cells with the Ark in the middle. E Journey, a band of map and the edge-on profile. F Crew's hand, the voyages dealt as cards."),
        ("Recommended: D", "It is the HyperSea itself: the cells are the game's 20 ly hexes, the Ark sits in the one it is in, and a voyage is a hex you can see the way to. It reads as a game board, not a list. B is the clearest to read; F carries the story that the crew brings the voyages."),
        ("In every option", "A squad hull opens the vessel strip on its seat and one press picks (P-ui-21, the configure card's strip). Picking another squad never changes the voyage on screen. Set sail is Ruby, the selection the selection blue, Lime unused."),
        ("The course stays put", "As in Rev 70: the board is dealt from the seated pilots and the home domain (Docs/Voyages/DEPTH.md section 4). Pinning the voyage on screen is Garrett's rule to change; a proposal to him before any build."),
        ("Quests", "Its own window, opened from Home like the Profile, on the same ground: Episode 1's five quests as medallions on one trail, lit as far as you have reached, one Lime button (Claim or Play quest)."),
    ]
    cells = "".join(f'<div style="display:flex;flex-direction:column;gap:10px"><span class="lab">{h}</span>'
                    f'<span style="font-size:18px;line-height:1.5">{t}</span></div>' for h, t in blocks)
    body = (f'<span class="abs" style="left:56px;top:44px;font-size:36px">Rev {REV} - notes</span>'
            f'<div class="abs" style="left:56px;top:120px;width:1808px;display:grid;grid-template-columns:1fr 1fr;gap:34px 64px">{cells}</div>')
    return page(f"Rev {REV} - notes: six new Voyage windows on the menu's ground, what each is, the recommendation, the two fixes they all carry, and the Quests window",
                body, "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}A.dc.html": a_board, f"R{REV}B.dc.html": b_board, f"R{REV}C.dc.html": c_board,
          f"R{REV}D.dc.html": d_board, f"R{REV}E.dc.html": e_board, f"R{REV}F.dc.html": f_board,
          f"R{REV}Quests.dc.html": quests_board, f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
