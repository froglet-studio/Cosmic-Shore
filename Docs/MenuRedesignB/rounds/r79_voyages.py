#!/usr/bin/env python3
"""Rev 79 boards: the Voyage window, four new directions (Frogma tab "Voyages", Docs/MENU_REDESIGN_B.md section 160).

Shombith, 2026-11-09, after the built Rev 74 C3: "These are still old UI's ... remove it create new ideas completely",
"The quality is very very bad", then "Revert all, redesign window". So every board redraws the WHOLE window - its tabs,
the HyperSea map and the edge view, the voyages, and picking the squad's vessels - not only the right column, and none
of them is a variation of Rev 70, 71, 74 or 77.

  A  Chart      the map is the window: the voyages are pins on it, the picked one docks on the right
  B  Logbook    the voyages as a list, the course large in the middle, the squad as three seats
  C  Formation  the squad's three hulls are the hero; the destinations on a ribbon above them
  D  Route      the picked voyage as a journey line across the window, rising and falling through the disk
  Compare       picking a vessel, the four ways side by side (the part the build broke)
  Notes

Every board is the real window inside the menu: the nav band, the hub row (Toy Box, Arcade, Arena, Voyage), the
title and Back, on the menu's ground, with the canvas's own art (the galaxy-tier map, the edge-on renders, the hull
renders). One Lime per screen: Set sail.

    python3 Docs/MenuRedesignB/rounds/r79_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "79"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r74", HERE / "r74_voyages.py")
r74 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r74)
r71 = r74.r71

REV, ROOT = int(_rev), r71.ROOT
blob, page, nav, GROUND, LOCK = r71.blob, r71.page, r71.nav, r71.GROUND, r71.LOCK
VOYAGES, HULL, TIERS = r71.VOYAGES, r71.HULL, r74.TIERS
GALAXY = TIERS[0][2]

# Where each voyage's destination sits on the galaxy-tier map in A (px inside the map frame), and the Ark at Tumult.
PINS = [(930, 470), (610, 360), (1040, 640), (470, 610)]
HOME = (652, 435)   # the map's own Tumult marker
# The course's height through the disk in D, per stop (ly above the midplane; the edge-on renders' own reading).
HEIGHTS = [[0, 40, 98], [0, -60, 20, 70, 110, 60, 30], [0, -40, -120], [0, 80, 150]]

DATA = (r71.DATA + "const PINS = " + json.dumps(PINS) + ";\nconst HOME = " + json.dumps(HOME)
        + ";\nconst HEIGHTS = " + json.dumps(HEIGHTS) + ";\n")

# The values every direction shares: r71's voyage, squad and vessel strip, plus the voyages as a list and the tabs.
VALS = r71.VALS + """
  const vlist = V.map((x, i) => ({ n: '0' + x.n, place: x.place, title: x.title, meta: x.ly + ' \u00B7 ' + x.min + ' \u00B7 Intensity ' + x.i,
    pressed: i === st.v ? 'true' : 'false', on: i === st.v, col: i === st.v ? '#E6E9FF' : '#747BAD',
    bar: i === st.v ? '#00D4FF' : 'rgba(0,0,0,0)', bg: i === st.v ? 'rgba(0,4,31,.86)' : 'rgba(0,4,31,0)',
    pick: pickV(i) }));
  const tabs = ['Voyages', 'Saga', 'Episodes'].map((t, i) => ({ name: t, locked: i === 2,
    col: (st.tab || 0) === i ? '#E6E9FF' : '#747BAD', line: (st.tab || 0) === i ? '#E6E9FF' : 'rgba(0,0,0,0)',
    pressed: (st.tab || 0) === i ? 'true' : 'false', pick: () => { if (i !== 2) this.setState({ tab: i }); } }));
  const seatNames = seats.map((s, i) => ({ hull: HULL[s.v], name: s.v, who: s.who === 'AI wingmate' ? 'AI wingmate' : s.who,
    role: s.role, pilot: s.p, el: s.e, pick: formation[i].pick, pressed: formation[i].pressed,
    edge: st.open === i ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89' }));
  const squadName = q => q;
  const sq = SQ[st.sq];
  const squad = { name: sq.name, next: () => this.setState({ sq: (st.sq + 1) % SQ.length, seats: SQ[(st.sq + 1) % SQ.length].seats.map((s) => Object.assign({}, s)), open: -1 }),
    seat: st.open >= 0 ? seats[st.open].role : '', close: () => this.setState({ open: -1 }) };
"""
RET = "voyage, presets, formation, crewLine, strip, vlist, tabs, seatNames, squad"


def logic(extra_vals="", extra_ret=""):
    return DATA + ("class Component extends DCLogic {\nconstructor(p) { super(p); " + r71.START + " }\nrenderVals() {"
                   + VALS + extra_vals + "  return { " + RET + (", " + extra_ret if extra_ret else "") + " };\n}\n}")


# ── The window's own frame: the menu's nav band, then the hub row, the title and Back ─────────────────────────────

HUB = ("TOY BOX", "ARCADE", "ARENA", "VOYAGE")


def header():
    items = "".join(
        f'<span class="cap" style="font-size:18px;color:{"#E6E9FF" if h == "VOYAGE" else "#747BAD"};padding-bottom:6px;'
        f'border-bottom:2px solid {"#E6E9FF" if h == "VOYAGE" else "rgba(0,0,0,0)"}">{h}</span>' for h in HUB)
    return ('<div class="abs" style="left:64px;right:64px;top:96px;height:60px;display:flex;align-items:center;'
            'justify-content:space-between;z-index:4"><span style="font-size:36px;width:360px">Voyage</span>'
            f'<div style="display:flex;align-items:center;gap:30px"><span class="kc">LB</span>{items}<span class="kc">RB</span></div>'
            '<div style="width:360px;display:flex;justify-content:flex-end"><button class="btn sm def" type="button">'
            '<span class="kc">B</span>Back</button></div></div>')


def window(body, title, extra_vals="", extra_ret=""):
    return page(title, GROUND + nav(crystals=False) + header() + body, logic(extra_vals, extra_ret))


SAIL = ('<a class="btn cta abs" href="#" style="left:{x}px;top:{y}px;width:{w}px"><span class="kc">A</span>Set sail</a>')


def hull_grid(x, y, w, cols, tile_h, z=9, caption=True, close=True):
    """The vessel picker: every hull at once in a grid, each a render with its name under it; one press picks."""
    cap = ('<div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:14px">'
           '<span class="lab" style="color:#E6E9FF">{{squad.seat}} - pick a vessel</span>'
           + ('<button class="btn sm def" type="button" onClick="{{squad.close}}"><span class="kc">B</span>Close</button>' if close else '')
           + '</div>') if caption else ''
    return (f'<sc-if value="{{{{strip.open}}}}" hint-placeholder-val="{{{{ false }}}}"><div class="abs sl" style="left:{x}px;top:{y}px;'
            f'width:{w}px;box-sizing:border-box;padding:20px 22px 22px;z-index:{z};background:#00010A;'
            f'box-shadow:inset 0 0 0 1px #00D4FF">{cap}<div style="display:grid;grid-template-columns:repeat({cols},1fr);gap:12px">'
            '<sc-for list="{{strip.items}}" as="t" hint-placeholder-count="6"><button class="pick sls" type="button" '
            'aria-label="{{t.name}}" aria-pressed="{{t.pressed}}" onClick="{{t.pick}}" style="height:'
            f'{tile_h}px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:6px;padding-bottom:10px;'
            'box-shadow:{{t.edge}}"><img class="hull" src="{{t.img}}" alt="" style="width:86%;height:'
            f'{tile_h - 46}px"><span class="cap" style="font-size:14px;color:#E6E9FF">{{{{t.name}}}}</span></button>'
            '</sc-for></div></div></sc-if>')


# ── A: Chart ──────────────────────────────────────────────────────────────────────────────────────────────────────────

def a_board():
    vals = """
  const pins = V.map((x, i) => ({ place: x.place, pick: pickV(i), pressed: i === st.v ? 'true' : 'false',
    x: (PINS[i][0] - 8) + 'px', y: (PINS[i][1] - 8) + 'px', lx: (PINS[i][0] + 16) + 'px', ly: (PINS[i][1] - 15) + 'px',
    col: i === st.v ? '#E6E9FF' : '#747BAD', edge: i === st.v ? 'inset 0 0 0 1px #00D4FF' : 'inset 0 0 0 1px rgba(67,76,137,.8)',
    dot: i === st.v ? '#00D4FF' : '#E6E9FF' }));
  const route = 'M' + HOME[0] + ' ' + HOME[1] + ' Q ' + ((HOME[0] + PINS[st.v][0]) / 2) + ' ' + (Math.min(HOME[1], PINS[st.v][1]) - 90) + ' ' + PINS[st.v][0] + ' ' + PINS[st.v][1];
"""
    body = f"""<figure class="abs sl" style="left:40px;top:172px;width:1840px;height:868px;margin:0;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89">
<img src="{GALAXY}" alt="The HyperSea, the whole galaxy, with the voyages on offer" style="position:absolute;left:0;top:0;width:1300px;height:868px;object-fit:cover;object-position:50% 50%;opacity:.86">
<svg aria-hidden="true" width="1300" height="868" style="position:absolute;left:0;top:0"><path d="{{{{route}}}}" fill="none" stroke="#E6E9FF" stroke-width="2.5" stroke-dasharray="8 7"></path><circle cx="{HOME[0]}" cy="{HOME[1]}" r="9" fill="#FFAE00"></circle></svg>
<sc-for list="{{{{pins}}}}" as="p" hint-placeholder-count="4"><button class="pick" type="button" aria-label="{{{{p.place}}}}" aria-pressed="{{{{p.pressed}}}}" onClick="{{{{p.pick}}}}" style="position:absolute;left:{{{{p.x}}}};top:{{{{p.y}}}};width:16px;height:16px;transform:rotate(45deg);background:{{{{p.dot}}}}"></button><button class="pick sls" type="button" aria-hidden="true" tabindex="-1" onClick="{{{{p.pick}}}}" style="position:absolute;left:{{{{p.lx}}}};top:{{{{p.ly}}}};padding:6px 12px;background:rgba(0,1,10,.84);box-shadow:{{{{p.edge}}}};font-family:'Chakra Petch';font-weight:600;font-size:15px;color:{{{{p.col}}}}">{{{{p.place}}}}</button></sc-for>
<div class="abs" style="left:24px;top:24px;display:flex;gap:6px"><sc-for list="{{{{tabs}}}}" as="t" hint-placeholder-count="3"><button class="pick sls cap" type="button" aria-pressed="{{{{t.pressed}}}}" onClick="{{{{t.pick}}}}" style="padding:10px 18px;background:rgba(0,1,10,.84);box-shadow:inset 0 -2px 0 {{{{t.line}}}};font-size:15px;color:{{{{t.col}}}}">{{{{t.name}}}}</button></sc-for></div>
<div class="abs sls" style="left:24px;bottom:24px;width:1252px;height:132px;overflow:hidden;background:rgba(0,1,10,.84);box-shadow:inset 0 0 0 1px #434C89"><img src="{{{{voyage.edge}}}}" alt="The course seen edge on, through the disk" style="position:absolute;left:0;top:0;width:100%;height:100%;object-fit:cover;object-position:40% 60%;opacity:.9"></div>
<aside class="abs" style="left:1300px;top:0;width:540px;height:868px;box-sizing:border-box;padding:36px 36px 32px;background:rgba(0,4,31,.94);box-shadow:inset 1px 0 0 #434C89;display:flex;flex-direction:column">
<span class="lab">{{{{voyage.kicker}}}}</span>
<span style="font-size:28px;line-height:1.25;margin-top:12px">{{{{voyage.title}}}}</span>
<span style="font-size:18px;line-height:1.5;color:#E6E9FF;margin-top:14px">{{{{voyage.line}}}}</span>
<div class="num" style="display:flex;gap:30px;font-size:28px;margin-top:26px"><span>{{{{voyage.ly}}}}</span><span>{{{{voyage.min}}}}</span><span>{{{{voyage.intensity}}}}</span></div>
<span style="font-size:15px;line-height:1.5;color:#747BAD;margin-top:22px">{{{{voyage.placeLine}}}}</span>
<button class="pick lab" type="button" onClick="{{{{squad.next}}}}" style="margin-top:auto;color:#E6E9FF;text-align:left">Squad - {{{{squad.name}}}}  &gt;</button>
<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:14px"><sc-for list="{{{{seatNames}}}}" as="s" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{{{s.pressed}}}}" onClick="{{{{s.pick}}}}" style="height:150px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:6px;padding-bottom:12px;box-shadow:{{{{s.edge}}}}"><img class="hull" src="{{{{s.hull}}}}" alt="{{{{s.role}}}}, {{{{s.who}}}}: {{{{s.name}}}}, press to change" style="width:90%;height:92px"><span style="font-size:15px;color:#E6E9FF">{{{{s.name}}}}</span></button></sc-for></div>
<a class="btn cta" href="#" style="margin-top:24px;width:100%"><span class="kc">A</span>Set sail</a>
</aside>
{hull_grid(1316, 332, 508, 3, 160)}
</figure>"""
    return window(body, f"Rev {REV} A - Chart: the map is the window; voyages are pins on it and the picked one docks on the right (recommended) - interactive",
                  vals, "pins, route")


# ── B: Logbook ────────────────────────────────────────────────────────────────────────────────────────────────────────

def tab_text(x, y, size=22, gap=40):
    return (f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;gap:{gap}px;align-items:flex-end">'
            '<sc-for list="{{tabs}}" as="t" hint-placeholder-count="3"><button class="pick cap" type="button" aria-pressed="{{t.pressed}}" '
            f'onClick="{{{{t.pick}}}}" style="font-size:{size}px;color:{{{{t.col}}}};padding-bottom:8px;border-bottom:2px solid {{{{t.line}}}}">'
            '{{t.name}}</button></sc-for></div>')


def b_board():
    body = f"""{tab_text(64, 178)}
<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:244px;width:520px;display:flex;flex-direction:column;gap:6px"><sc-for list="{{{{vlist}}}}" as="r" hint-placeholder-count="4"><button class="pick sls" type="button" aria-pressed="{{{{r.pressed}}}}" onClick="{{{{r.pick}}}}" style="height:190px;box-sizing:border-box;padding:0 28px;display:flex;flex-direction:column;justify-content:center;gap:10px;background:{{{{r.bg}}}};box-shadow:inset 3px 0 0 {{{{r.bar}}}}"><span style="font-size:22px;color:{{{{r.col}}}}">{{{{r.n}}}}  {{{{r.place}}}}</span><span class="num" style="font-size:15px;color:#747BAD">{{{{r.meta}}}}</span></button></sc-for></nav>
<figure class="abs sl" style="left:600px;top:244px;width:760px;height:500px;margin:0;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89"><img src="{{{{voyage.map}}}}" alt="The picked voyage's course on the HyperSea" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{{{{voyage.pos}}}}"></figure>
<figure class="abs sl" style="left:600px;top:764px;width:760px;height:276px;margin:0;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89"><img src="{{{{voyage.edge}}}}" alt="The course seen edge on, through the disk" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:40% 60%"></figure>
<section class="abs" style="left:1400px;top:244px;width:480px;height:796px;display:flex;flex-direction:column">
<span class="lab">{{{{voyage.kicker}}}}</span>
<span style="font-size:28px;line-height:1.25;margin-top:12px">{{{{voyage.title}}}}</span>
<div class="num" style="display:flex;gap:26px;font-size:22px;margin-top:20px"><span>{{{{voyage.ly}}}}</span><span>{{{{voyage.min}}}}</span><span>{{{{voyage.intensity}}}}</span></div>
<span style="font-size:18px;line-height:1.5;color:#E6E9FF;margin-top:18px">{{{{voyage.line}}}}</span>
<button class="pick lab" type="button" onClick="{{{{squad.next}}}}" style="margin-top:auto;color:#E6E9FF;text-align:left">Squad - {{{{squad.name}}}}  &gt;</button>
<div style="display:flex;flex-direction:column;gap:10px;margin-top:14px"><sc-for list="{{{{seatNames}}}}" as="s" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{{{s.pressed}}}}" onClick="{{{{s.pick}}}}" style="height:104px;display:flex;align-items:center;gap:20px;padding:0 20px;box-shadow:{{{{s.edge}}}}"><img class="hull" src="{{{{s.hull}}}}" alt="{{{{s.role}}}}: {{{{s.name}}}}, press to change" style="width:150px;height:84px"><span style="font-size:18px;color:#E6E9FF">{{{{s.name}}}}<br><span style="font-size:15px;color:#747BAD">{{{{s.who}}}} - {{{{s.el}}}}</span></span></button></sc-for></div>
<a class="btn cta" href="#" style="margin-top:24px;width:100%"><span class="kc">A</span>Set sail</a>
</section>
{hull_grid(600, 244, 760, 3, 176)}"""
    return window(body, f"Rev {REV} B - Logbook: a list of voyages, the course large in the middle, the squad as three seats on the right - interactive")


# ── C: Formation ──────────────────────────────────────────────────────────────────────────────────────────────────────

def c_board():
    body = f"""{tab_text(64, 178, 18, 34)}
<nav aria-label="Destinations" class="abs" style="left:40px;right:40px;top:236px;height:92px;display:grid;grid-template-columns:repeat(4,1fr);gap:12px"><sc-for list="{{{{vlist}}}}" as="r" hint-placeholder-count="4"><button class="pick sls" type="button" aria-pressed="{{{{r.pressed}}}}" onClick="{{{{r.pick}}}}" style="box-sizing:border-box;padding:0 22px;display:flex;flex-direction:column;justify-content:center;gap:6px;background:{{{{r.bg}}}};box-shadow:inset 0 -2px 0 {{{{r.bar}}}}"><span style="font-size:22px;color:{{{{r.col}}}}">{{{{r.place}}}}</span><span class="num" style="font-size:15px;color:#747BAD">{{{{r.meta}}}}</span></button></sc-for></nav>
<figure class="abs" style="left:64px;top:368px;width:360px;height:360px;margin:0;border-radius:50%;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89"><img src="{{{{voyage.map}}}}" alt="The picked voyage's course on the HyperSea" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{{{{voyage.pos}}}}"></figure>
<div class="abs" style="left:64px;top:756px;width:420px;display:flex;flex-direction:column;gap:12px"><span style="font-size:28px;line-height:1.25">{{{{voyage.title}}}}</span><span style="font-size:18px;line-height:1.5;color:#E6E9FF">{{{{voyage.line}}}}</span></div>
<button class="pick lab abs" type="button" onClick="{{{{squad.next}}}}" style="left:560px;top:372px;color:#E6E9FF">Squad - {{{{squad.name}}}}  &gt;</button>
<div class="abs" style="left:560px;top:410px;width:1320px;height:420px;display:grid;grid-template-columns:repeat(3,1fr);gap:24px"><sc-for list="{{{{seatNames}}}}" as="s" hint-placeholder-count="3"><button class="pick" type="button" aria-pressed="{{{{s.pressed}}}}" onClick="{{{{s.pick}}}}" style="position:relative;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;padding-bottom:20px;gap:10px;box-shadow:{{{{s.edge}}}};background:radial-gradient(ellipse 60% 20% at 50% 74%,rgba(117,143,251,.28),rgba(117,143,251,0))"><img class="hull" src="{{{{s.hull}}}}" alt="{{{{s.role}}}}: {{{{s.name}}}}, press to change" style="width:96%;height:280px"><span style="font-size:22px">{{{{s.name}}}}</span><span class="lab">{{{{s.role}}}} - {{{{s.who}}}}</span></button></sc-for></div>
{hull_grid(560, 410, 1320, 6, 200, z=9)}
<div class="abs sls" style="left:560px;top:870px;width:860px;height:170px;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89"><img src="{{{{voyage.edge}}}}" alt="The course seen edge on, through the disk" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:40% 60%"><span class="abs lab" style="left:16px;top:12px;color:#E6E9FF">Edge on - {{{{voyage.ly}}}}  {{{{voyage.min}}}}  {{{{voyage.intensity}}}}</span></div>
{SAIL.format(x=1460, y=984, w=420)}"""
    return window(body, f"Rev {REV} C - Formation: the squad's three hulls are the hero; destinations on a ribbon above them - interactive")


# ── D: Route ──────────────────────────────────────────────────────────────────────────────────────────────────────────

def d_board():
    vals = """
  const H = HEIGHTS[st.v], n = H.length, x0 = 120, x1 = 1680, mid = 300;
  const stops = H.map((h, i) => ({ x: x0 + (x1 - x0) * i / (n - 1), y: mid - h * 1.1 }));
  const line = stops.map((p) => p.x.toFixed(0) + ',' + p.y.toFixed(0)).join(' ');
  const marks = stops.map((p, i) => ({ x: (p.x - 9).toFixed(0) + 'px', y: (p.y - 9).toFixed(0) + 'px', end: i === 0 || i === n - 1,
    size: (i === 0 || i === n - 1) ? '18px' : '12px', fill: i === 0 ? '#FFAE00' : '#E6E9FF' }));
  const ends = { from: 'Tumult - midplane', to: v.place + ' - ' + H[n - 1] + ' ly ' + (H[n - 1] >= 0 ? 'above' : 'below'),
    tx: (x1 - 300) + 'px', ty: (stops[n - 1].y + 22).toFixed(0) + 'px', fy: (stops[0].y - 40).toFixed(0) + 'px' };
"""
    body = f"""{tab_text(64, 178, 18, 34)}
<section class="abs sl" style="left:40px;top:236px;width:1840px;height:520px;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89">
<img src="{{{{voyage.map}}}}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:{{{{voyage.pos}}}};opacity:.22">
<span class="abs lab" style="left:36px;top:30px;color:#E6E9FF">{{{{voyage.kicker}}}}</span>
<span class="abs" style="left:36px;top:62px;width:1200px;font-size:36px;line-height:1.2">{{{{voyage.title}}}}</span>
<svg aria-hidden="true" width="1840" height="520" style="position:absolute;left:0;top:0"><line x1="80" y1="300" x2="1760" y2="300" stroke="#434C89" stroke-width="1.5" stroke-dasharray="4 8"></line><polyline points="{{{{line}}}}" fill="none" stroke="#E6E9FF" stroke-width="3" stroke-linejoin="round"></polyline></svg>
<sc-for list="{{{{marks}}}}" as="m" hint-placeholder-count="3"><span style="position:absolute;left:{{{{m.x}}}};top:{{{{m.y}}}};width:{{{{m.size}}}};height:{{{{m.size}}}};transform:rotate(45deg);background:{{{{m.fill}}}}"></span></sc-for>
<span class="abs cap" style="left:96px;top:{{{{ends.fy}}}};font-size:15px;color:#FFAE00">{{{{ends.from}}}}</span>
<span class="abs cap" style="left:{{{{ends.tx}}}};top:{{{{ends.ty}}}};width:340px;text-align:right;font-size:15px;color:#E6E9FF">{{{{ends.to}}}}</span>
<div class="abs num" style="left:36px;bottom:28px;display:flex;gap:30px;font-size:28px"><span>{{{{voyage.ly}}}}</span><span>{{{{voyage.min}}}}</span><span>{{{{voyage.intensity}}}}</span></div>
<span class="abs lab" style="right:36px;bottom:34px">The line is the course edge on: up and down through the disk</span>
</section>
<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:788px;width:1040px;height:252px;display:grid;grid-template-columns:repeat(2,1fr);gap:12px"><sc-for list="{{{{vlist}}}}" as="r" hint-placeholder-count="4"><button class="pick sls" type="button" aria-pressed="{{{{r.pressed}}}}" onClick="{{{{r.pick}}}}" style="box-sizing:border-box;padding:0 24px;display:flex;flex-direction:column;justify-content:center;gap:6px;background:{{{{r.bg}}}};box-shadow:inset 3px 0 0 {{{{r.bar}}}}"><span style="font-size:22px;color:{{{{r.col}}}}">{{{{r.n}}}}  {{{{r.place}}}}</span><span class="num" style="font-size:15px;color:#747BAD">{{{{r.meta}}}}</span></button></sc-for></nav>
<button class="pick lab abs" type="button" onClick="{{{{squad.next}}}}" style="left:1120px;top:792px;color:#E6E9FF">Squad - {{{{squad.name}}}}  &gt;</button>
<div class="abs" style="left:1120px;top:828px;width:760px;height:130px;display:grid;grid-template-columns:repeat(3,1fr);gap:12px"><sc-for list="{{{{seatNames}}}}" as="s" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{{{s.pressed}}}}" onClick="{{{{s.pick}}}}" style="display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:4px;padding-bottom:8px;box-shadow:{{{{s.edge}}}}"><img class="hull" src="{{{{s.hull}}}}" alt="{{{{s.role}}}}: {{{{s.name}}}}, press to change" style="width:86%;height:84px"><span style="font-size:15px;color:#E6E9FF">{{{{s.name}}}}</span></button></sc-for></div>
{hull_grid(1120, 418, 760, 3, 160)}
{SAIL.format(x=1120, y=984, w=760)}"""
    return window(body, f"Rev {REV} D - Route: the picked voyage as a journey line across the window, rising and falling through the disk - interactive",
                  vals, "line, marks, ends")


# ── Compare: picking a vessel, four ways ──────────────────────────────────────────────────────────────────────────────

def compare_board():
    names = list(HULL)

    def tiles(cols, size, label=True):
        return "".join(
            f'<div style="display:flex;flex-direction:column;align-items:center;gap:6px"><img class="hull" src="{HULL[n]}" alt="" '
            f'style="width:{size}px;height:{int(size * 0.62)}px"><span class="cap" style="font-size:14px;color:{"#E6E9FF" if i else "#00D4FF"}">{n}</span></div>'
            for i, n in enumerate(names))

    def cell(x, y, key, words, inner):
        return (f'<section class="abs" style="left:{x}px;top:{y}px;width:880px;height:440px">'
                f'<span class="abs" style="left:0;top:2px;width:880px;font-size:18px;color:#E6E9FF">{key}  {words}</span>{inner}</section>')

    a = ('<div class="abs sl" style="left:0;top:50px;width:540px;height:280px;box-sizing:border-box;padding:24px;'
         'background:rgba(0,1,10,.95);box-shadow:inset 0 0 0 1px #00D4FF;display:grid;grid-template-columns:repeat(3,1fr);'
         f'gap:16px;align-content:start">{tiles(3, 140)}</div>')
    b = ('<div class="abs sl" style="left:0;top:50px;width:860px;height:330px;box-sizing:border-box;padding:28px;'
         'background:rgba(0,1,10,.95);box-shadow:inset 0 0 0 1px #00D4FF;display:grid;grid-template-columns:repeat(3,1fr);'
         f'gap:18px;align-content:start">{tiles(3, 180)}</div>')
    c = ('<div class="abs sl" style="left:0;top:50px;width:880px;height:170px;box-sizing:border-box;padding:24px;'
         'background:rgba(0,1,10,.95);box-shadow:inset 0 0 0 1px #00D4FF;display:grid;grid-template-columns:repeat(6,1fr);'
         f'gap:10px;align-items:end">{tiles(6, 120)}</div>')
    d = ('<div class="abs sl" style="left:0;top:50px;width:760px;height:310px;box-sizing:border-box;padding:24px;'
         'background:rgba(0,1,10,.95);box-shadow:inset 0 0 0 1px #00D4FF;display:grid;grid-template-columns:repeat(3,1fr);'
         f'gap:16px;align-content:start">{tiles(3, 160)}</div>')
    body = (cell(60, 80, "A", "Chart: a grid over the dock, the map still in view.", a)
            + cell(980, 80, "B", "Logbook: the grid takes the course panel while it is open.", b)
            + cell(60, 600, "C", "Formation: one row under the three hulls, every hull in view at once.", c)
            + cell(980, 600, "D", "Route: a grid above the squad, over the route band.", d))
    body = (r71.GROUND.replace('top:84px;width:1920px;height:996px', 'top:0;width:1920px;height:1080px') + body
            + '<span class="abs lab" style="left:60px;top:28px">Picking a vessel - a press on a hull opens every hull, one press picks</span>')
    return page(f"Rev {REV} - Picking a vessel, four ways: one press from the squad to any hull", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}")


# ── Notes ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

def notes_board():
    rows = [
        ("Asked", "\"These are still old UI's ... remove it create new ideas completely\", \"The quality is very very bad\", "
                  "\"Revert all, redesign window\" (Shombith, 2026-11-09). The build is reverted; these are new directions for the whole window."),
        ("What every board redraws", "The window's tabs (Voyages, Saga, Episodes), the HyperSea map and the edge view, the voyages "
                  "and picking vessels. Same content in all four: four voyages, the Skim Wing squad, one Set sail."),
        ("A Chart (recommended)", "The map is the window: the voyages are pins with their names, the course from the Ark a dashed "
                  "line, the edge view a band at the foot. The picked voyage docks on the right with the squad and Set sail. The map "
                  "stays in view while you pick, which is what Garrett built the window around."),
        ("B Logbook", "Application-like: a list you scan, the course large in the middle, the edge view under it, three seats."),
        ("C Formation", "The squad is the hero, like the Hangar; destinations are a ribbon; the map is a small round scope."),
        ("D Route", "The voyage as a journey: the line is the edge-on course, so the map and the edge view become one picture."),
        ("Picking a vessel", "In all four, a press on a hull opens every hull at once in a grid, one press picks. Six hull renders "
                  "are on the canvas; the game's grid would scroll for more."),
        ("Not in this round", "The pinned voyage while the crew or domain changes stays a proposal to Garrett (section 147)."),
    ]
    items = "".join(f'<div style="display:grid;grid-template-columns:320px 1fr;gap:28px"><span class="cap" style="font-size:18px">{k}</span>'
                    f'<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{v}</span></div>' for k, v in rows)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:26px">'
            f'<span style="font-size:36px">Rev {REV} - the Voyage window, four new directions</span>{items}</div>')
    return page(f"Rev {REV} - notes: what each direction is, what it redraws, the recommendation, what it would change in the game",
                body, "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}Compare.dc.html": compare_board, f"R{REV}A.dc.html": a_board, f"R{REV}B.dc.html": b_board,
          f"R{REV}C.dc.html": c_board, f"R{REV}D.dc.html": d_board, f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
