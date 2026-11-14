#!/usr/bin/env python3
"""Rev 87 boards: the Voyages tab drawn new around the HyperSea map and the edge-on view (Frogma tab "Voyages",
Docs/MENU_REDESIGN_B.md section 168).

Shombith, 2026-11-12, on the built Rev 81 B5 + Rev 83 S2 (section 165): "this has gone through a lot of updates but
it either does not update inside unity due to some issues or I do not like the design of it. Only thing the hypersea
map and the bottom edge on view should stay apart from that ... delete the old voyages mockups and create new".

So every board keeps the course map over its edge-on view exactly as Rev 81 drew them, and redraws the rest: the tabs
(four, as the game has), the voyages, the open voyage's words and numbers, the squad, changing a seat, and Set sail.
There is no separate seat editor screen in any of them: a seat changes where it stands.

  A  Slide     small voyage cards, the picked one slides open; the right column is only the squad, as rows
  B  Wide sea  the whole course wide on the left over its edge view; one panel on the right holds the rest
  C  Ticket    a quiet list, and the open voyage as one ticket: where, the numbers, the crew, Set sail as its stub
  Notes

    python3 Docs/MenuRedesignB/rounds/r87_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "87"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r82", HERE / "r82_voyages.py")
r82 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r82)
r80, r79 = r82.r80, r82.r81.r80.r79

REV, ROOT, page, nav, GROUND = int(_rev), r82.ROOT, r82.page, r82.nav, r82.GROUND
course, edge = r80.course, r80.edge

JADE, INK, DIM, LINE = "#00D4FF", "#E6E9FF", "#747BAD", "#434C89"
PLATE = "rgba(0,4,31,.86)"
CHEV_L = ('<svg width="12" height="16" viewBox="0 0 12 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">'
          '<path d="M9 2L3 8l6 6"></path></svg>')
CHEV_R = ('<svg width="12" height="16" viewBox="0 0 12 16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">'
          '<path d="M3 2l6 6-6 6"></path></svg>')

LOGIC = r82.DATA + """class Component extends DCLogic {
constructor(p) { super(p); this.state = { v: 1, sq: 0, books: SQ.map((q) => q.seats.map((s) => Object.assign({}, s))), open: 0, tab: 0 }; }
renderVals() {
  const st = this.state, v = V[st.v], q = SQ[st.sq], seats = st.books[st.sq];
  const who = (s) => s.who === 'AI wingmate' ? 'AI' : s.who;
  // The Ark's rules: your own seat, and the AI seats of a squad you made; in a squad you joined, only yours.
  const canEdit = (i) => q.mine ? (seats[i].who === 'You' || seats[i].who === 'AI wingmate') : seats[i].who === 'You';
  const set = (i, patch) => {
    const books = st.books.map((b) => b.map((s) => Object.assign({}, s)));
    const mine = books[st.sq];
    // A pilot flies one seat: the seat that had them takes this seat's pilot (the swap).
    if (patch.p) { const j = mine.findIndex((s, k) => k !== i && s.p === patch.p); if (j >= 0) mine[j].p = mine[i].p; }
    Object.assign(mine[i], patch);
    this.setState({ books });
  };
  const step = (list, cur, d) => list[(list.indexOf(cur) + d + list.length) % list.length];
  const voyage = { n: '0' + v.n, title: v.title, line: v.line, place: v.place, ly: v.ly, min: v.min, i: String(v.i),
    map: v.map, pos: v.pos, edge: v.edge, crew: 'Brought by ' + v.crew, kicker: 'Voyage 0' + v.n + ' - brought by ' + v.crew };
  const vlist = V.map((x, i) => ({ n: '0' + x.n, place: x.place, title: x.title, line: x.line, map: x.map, pos: x.pos,
    crew: 'Brought by ' + x.crew, ly: x.ly, min: x.min, i: String(x.i), meta: x.min + ' - Intensity ' + x.i,
    on: i === st.v, off: i !== st.v, grow: i === st.v ? '1 1 0' : '0 0 auto', pressed: i === st.v ? 'true' : 'false',
    col: i === st.v ? '#E6E9FF' : '#747BAD', bar: i === st.v ? '#00D4FF' : 'rgba(0,0,0,0)',
    bg: i === st.v ? 'rgba(0,4,31,.86)' : 'rgba(0,4,31,0)', pick: () => this.setState({ v: i }) }));
  const tabs = ['Voyages', 'Saga', 'Quests', 'Episodes'].map((t, i) => ({ name: t, col: i === 0 ? '#E6E9FF' : '#747BAD',
    line: i === 0 ? '#E6E9FF' : 'rgba(0,0,0,0)', pressed: i === 0 ? 'true' : 'false', soon: i === 3, pick: () => {} }));
  const presets = SQ.map((s, i) => ({ name: s.mine ? s.name : s.name + ' - ' + s.owner + "'s", pressed: i === st.sq ? 'true' : 'false',
    col: i === st.sq ? '#E6E9FF' : '#747BAD', line: i === st.sq ? '#00D4FF' : 'rgba(0,0,0,0)',
    pick: () => this.setState({ sq: i, open: -1 }) }));
  const tile = (on) => on ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89';
  const seatList = seats.map((s, i) => { const ok = canEdit(i), open = st.open === i;
    return { face: PILOT[s.p], hull: HULL[s.v], name: s.v, who: who(s), pilot: s.p, line1: who(s) + ' - ' + s.p,
      line2: ok ? s.e + ' - ' + s.a : (s.who + "'s seat"), short: who(s) + ' - ' + s.p, kit: s.e + ' - ' + s.a, open: open,
      edge: open ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89', bar: open ? '#00D4FF' : 'rgba(0,0,0,0)',
      op: ok ? 1 : 0.55, pressed: open ? 'true' : 'false',
      alt: s.role + ', ' + s.who + ': ' + s.v + (ok ? ', press to change' : ', not yours to change'),
      pick: ok ? () => this.setState({ open: open ? -1 : i, tab: 0 }) : () => {},
      steps: [
        { label: 'Vessel', value: s.v, prev: () => set(i, { v: step(Object.keys(HULL), s.v, -1) }), next: () => set(i, { v: step(Object.keys(HULL), s.v, 1) }) },
        { label: 'Pilot', value: s.p, prev: () => set(i, { p: step(Object.keys(PILOT), s.p, -1) }), next: () => set(i, { p: step(Object.keys(PILOT), s.p, 1) }) },
        { label: 'Element', value: s.e, prev: () => set(i, { e: step(ELEMENTS, s.e, -1) }), next: () => set(i, { e: step(ELEMENTS, s.e, 1) }) },
        { label: 'Artifact', value: s.a, prev: () => set(i, { a: step(ARTIFACTS, s.a, -1) }), next: () => set(i, { a: step(ARTIFACTS, s.a, 1) }) } ] }; });
  // B's tray: the open seat's four choices as tabs, each a row of tiles; one press picks.
  let tray = { open: false, title: '', tabs: [], tiles: [], pics: false, words: false };
  if (st.open >= 0) {
    const i = st.open, s = seats[i];
    const lists = [
      Object.keys(HULL).map((h) => ({ name: h, img: HULL[h], pressed: s.v === h ? 'true' : 'false', edge: tile(s.v === h), pick: () => set(i, { v: h }) })),
      Object.keys(PILOT).map((p) => ({ name: p, img: PILOT[p], pressed: s.p === p ? 'true' : 'false', edge: tile(s.p === p), pick: () => set(i, { p: p }) })),
      ELEMENTS.map((e) => ({ name: e, img: '', pressed: s.e === e ? 'true' : 'false', edge: tile(s.e === e), pick: () => set(i, { e: e }) })),
      ARTIFACTS.map((a) => ({ name: a, img: '', pressed: s.a === a ? 'true' : 'false', edge: tile(s.a === a), pick: () => set(i, { a: a }) })) ];
    tray = { open: true, title: s.role + ' - ' + who(s), pics: st.tab < 2, words: st.tab >= 2, tiles: lists[st.tab],
      tabs: ['Vessel', 'Pilot', 'Element', 'Artifact'].map((t, k) => ({ name: t, pressed: st.tab === k ? 'true' : 'false',
        col: st.tab === k ? '#E6E9FF' : '#747BAD', line: st.tab === k ? '#E6E9FF' : 'rgba(0,0,0,0)', pick: () => this.setState({ tab: k }) })) };
  }
  return { voyage, vlist, tabs, presets, seatList, tray };
}
}"""


# ── Shared pieces ─────────────────────────────────────────────────────────────────────────────────────────────────────

def tabs(x=64, y=178):
    """The window's four tabs as plain capitals, the shown one light and underlined; Episodes says Soon."""
    return (f'<div class="abs" style="left:{x}px;top:{y}px;display:flex;gap:40px;align-items:flex-end">'
            '<sc-for list="{{tabs}}" as="t" hint-placeholder-count="4"><button class="pick cap" type="button" aria-pressed="{{t.pressed}}" '
            'onClick="{{t.pick}}" style="font-size:22px;color:{{t.col}};padding-bottom:8px;border-bottom:2px solid {{t.line}}">{{t.name}}'
            '<sc-if value="{{t.soon}}" hint-placeholder-val="{{ false }}"><span style="font-size:14px;margin-left:10px">Soon</span></sc-if>'
            '</button></sc-for></div>')


def squads(style=""):
    """Which squad sails: its name as a word, the picked one under a Jade line."""
    return (f'<div style="display:flex;align-items:flex-end;gap:26px;{style}"><span class="lab" style="padding-bottom:8px">Squad</span>'
            '<sc-for list="{{presets}}" as="p" hint-placeholder-count="2"><button class="pick" type="button" aria-pressed="{{p.pressed}}" '
            'onClick="{{p.pick}}" style="font-size:18px;color:{{p.col}};padding-bottom:6px;border-bottom:2px solid {{p.line}}">{{p.name}}</button>'
            '</sc-for></div>')


def numbers(size=28, gap=16):
    cell = lambda lab, val: (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab">{lab}</span>'
                             f'<span class="num" style="font-size:{size}px">{val}</span></div>')
    return (f'<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:{gap}px">' + cell("Distance", "{{voyage.ly}}")
            + cell("Sail", "{{voyage.min}}") + cell("Intensity", "{{voyage.i}}") + '</div>')


def steppers(cols=2):
    """Changing a seat where it stands: Vessel, Pilot, Element, Artifact, each one line with a step either way."""
    arrow = lambda fn, lab, svg: (f'<button class="pick" type="button" aria-label="{lab} {{{{k.label}}}}" onClick="{{{{k.{fn}}}}}" '
                                  f'style="width:44px;height:44px;display:flex;align-items:center;justify-content:center;color:{INK}">{svg}</button>')
    return (f'<div style="display:grid;grid-template-columns:repeat({cols},1fr);gap:10px 14px;padding:4px 18px 16px">'
            '<sc-for list="{{s.steps}}" as="k" hint-placeholder-count="4"><div style="display:flex;flex-direction:column;gap:2px">'
            '<span class="lab">{{k.label}}</span><div class="sls" style="height:44px;display:flex;align-items:center;justify-content:space-between;'
            f'background:#00010A;box-shadow:inset 0 0 0 1px {LINE}">' + arrow("prev", "Previous", CHEV_L)
            + '<span style="font-size:18px">{{k.value}}</span>' + arrow("next", "Next", CHEV_R) + '</div></div></sc-for></div>')


def sail(style):
    return f'<a class="btn cta" href="#" style="{style}"><span class="kc">A</span>Set sail</a>'


def sea(x=620, w=660):
    """The two things that stay: the course on the HyperSea, and the same course seen edge on under it."""
    return course(x, 244, w, 470, muted=True, plain=True) + "\n" + edge(x, 734, w, 306, muted=True, plain=True)


def window(body, title):
    return page(title, GROUND + nav(crystals=False) + r79.header() + tabs() + body, LOGIC)


# ── A: Slide ──────────────────────────────────────────────────────────────────────────────────────────────────────────

def a_board():
    # Small cards on the course's own water; the picked one is open and carries the voyage's words and numbers.
    cards = f"""<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:244px;width:540px;height:796px;display:flex;flex-direction:column;gap:10px">
<sc-for list="{{{{vlist}}}}" as="r" hint-placeholder-count="4"><div style="flex:{{{{r.grow}}}};display:flex;flex-direction:column;min-height:0">
<sc-if value="{{{{r.off}}}}" hint-placeholder-val="{{{{ true }}}}"><button class="pick sls" type="button" aria-pressed="false" onClick="{{{{r.pick}}}}" style="position:relative;height:96px;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px {LINE}">
<img src="{{{{r.map}}}}" alt="" style="position:absolute;right:0;top:0;width:300px;height:100%;object-fit:cover;object-position:{{{{r.pos}}}};opacity:.85">
<span style="position:absolute;inset:0;background:linear-gradient(90deg,#00010A 40%,rgba(0,1,10,0))"></span>
<span style="position:absolute;left:22px;top:22px;font-size:22px;color:{INK}">{{{{r.n}}}}  {{{{r.place}}}}</span>
<span class="num" style="position:absolute;left:22px;top:56px;font-size:15px;color:{DIM}">{{{{r.meta}}}}</span></button></sc-if>
<sc-if value="{{{{r.on}}}}" hint-placeholder-val="{{{{ false }}}}"><section class="sl" aria-label="{{{{r.title}}}}" style="flex:1;box-sizing:border-box;padding:30px 30px 30px 33px;display:flex;flex-direction:column;background:{PLATE};box-shadow:inset 3px 0 0 {JADE}">
<span class="lab">{{{{r.n}}}} - {{{{r.crew}}}}</span>
<span style="font-size:36px;line-height:1.15;margin-top:14px">{{{{r.place}}}}</span>
<span style="font-size:22px;line-height:1.3;margin-top:10px">{{{{r.title}}}}</span>
<span style="font-size:18px;line-height:1.5;color:{INK};margin-top:16px">{{{{r.line}}}}</span>
<div style="margin-top:auto">{numbers()}</div></section></sc-if></div>
</sc-for></nav>"""
    # The right column is only who sails: a row per seat, and a pressed row opens its four choices under it.
    rows = f"""<section aria-label="The squad" class="abs" style="left:1320px;top:244px;width:560px;height:796px;display:flex;flex-direction:column">
{squads()}
<div style="display:flex;flex-direction:column;gap:12px;margin-top:20px">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><div class="sl" style="opacity:{{{{s.op}}}};background:{PLATE};box-shadow:{{{{s.edge}}}}">
<button class="pick" type="button" aria-pressed="{{{{s.pressed}}}}" aria-label="{{{{s.alt}}}}" onClick="{{{{s.pick}}}}" style="width:100%;height:158px;display:grid;grid-template-columns:120px 1fr 200px;gap:16px;align-items:center;padding:0 18px;box-sizing:border-box">
<img src="{{{{s.face}}}}" alt="" style="width:120px;height:150px;align-self:end;object-fit:contain;object-position:50% 100%">
<span style="display:flex;flex-direction:column;gap:6px"><span style="font-size:22px">{{{{s.name}}}}</span><span style="font-size:15px;color:{INK}">{{{{s.line1}}}}</span><span style="font-size:15px;color:{DIM}">{{{{s.line2}}}}</span></span>
<img class="hull" src="{{{{s.hull}}}}" alt="" style="width:200px;height:110px"></button>
<sc-if value="{{{{s.open}}}}" hint-placeholder-val="{{{{ false }}}}">{steppers()}</sc-if>
</div></sc-for></div>
{sail("margin-top:auto;width:100%")}
</section>"""
    return window(cards + sea() + rows,
                  f"Rev {REV} A - Slide: small voyage cards, the picked one slides open; the squad as rows that open where they stand (recommended) - interactive")


# ── B: Wide sea ───────────────────────────────────────────────────────────────────────────────────────────────────────

def b_board():
    # The whole course picture, uncropped, with its edge view under it at the same width.
    wide = course(64, 244, 960, 594, muted=True, plain=True) + "\n" + edge(64, 850, 960, 190, muted=True, plain=True)
    picks = f"""<nav aria-label="Voyages on offer" style="display:grid;grid-template-columns:repeat(2,1fr);gap:10px">
<sc-for list="{{{{vlist}}}}" as="r" hint-placeholder-count="4"><button class="pick sls" type="button" aria-pressed="{{{{r.pressed}}}}" onClick="{{{{r.pick}}}}" style="height:68px;display:flex;align-items:center;justify-content:space-between;padding:0 20px;background:{{{{r.bg}}}};box-shadow:inset 3px 0 0 {{{{r.bar}}}}, inset 0 0 0 1px {LINE}">
<span style="font-size:22px;color:{{{{r.col}}}}">{{{{r.n}}}}  {{{{r.place}}}}</span><span class="num" style="font-size:15px;color:{DIM}">{{{{r.min}}}}</span></button></sc-for></nav>"""
    tile_pic = ('<button class="pick sls" type="button" aria-label="{{t.name}}" aria-pressed="{{t.pressed}}" onClick="{{t.pick}}" '
                'style="flex:1;height:104px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:2px;padding-bottom:8px;'
                'background:#00010A;box-shadow:{{t.edge}}"><img class="hull" src="{{t.img}}" alt="" style="width:88%;height:66px">'
                f'<span style="font-size:14px;color:{INK}">{{{{t.name}}}}</span></button>')
    tile_word = ('<button class="pick sls" type="button" aria-pressed="{{t.pressed}}" onClick="{{t.pick}}" '
                 f'style="flex:1;height:104px;display:flex;align-items:center;justify-content:center;text-align:center;padding:0 8px;font-size:18px;color:{INK};'
                 'background:#00010A;box-shadow:{{t.edge}}">{{t.name}}</button>')
    panel = f"""<section aria-label="The open voyage" class="abs" style="left:1064px;top:244px;width:792px;height:796px;display:flex;flex-direction:column">
{picks}
<span class="lab" style="margin-top:26px">{{{{voyage.kicker}}}}</span>
<span style="font-size:28px;line-height:1.25;margin-top:8px">{{{{voyage.title}}}}</span>
<div style="margin-top:18px">{numbers()}</div>
{squads("margin-top:24px")}
<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:12px">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><button class="pick sl" type="button" aria-pressed="{{{{s.pressed}}}}" aria-label="{{{{s.alt}}}}" onClick="{{{{s.pick}}}}" style="height:172px;opacity:{{{{s.op}}}};display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:4px;padding-bottom:12px;background:{PLATE};box-shadow:{{{{s.edge}}}}">
<img class="hull" src="{{{{s.hull}}}}" alt="" style="width:88%;height:84px">
<span style="font-size:18px">{{{{s.name}}}}</span><span style="font-size:15px;color:{INK}">{{{{s.short}}}}</span><span style="font-size:15px;color:{DIM}">{{{{s.line2}}}}</span></button></sc-for></div>
<sc-if value="{{{{tray.open}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="margin-top:12px;display:flex;flex-direction:column;gap:8px">
<div style="display:flex;gap:26px;align-items:flex-end"><span class="lab" style="padding-bottom:8px;color:{INK}">{{{{tray.title}}}}</span><sc-for list="{{{{tray.tabs}}}}" as="k" hint-placeholder-count="4"><button class="pick cap" type="button" aria-pressed="{{{{k.pressed}}}}" onClick="{{{{k.pick}}}}" style="font-size:14px;color:{{{{k.col}}}};padding-bottom:6px;border-bottom:2px solid {{{{k.line}}}}">{{{{k.name}}}}</button></sc-for></div>
<sc-if value="{{{{tray.pics}}}}" hint-placeholder-val="{{{{ true }}}}"><div style="display:flex;gap:8px"><sc-for list="{{{{tray.tiles}}}}" as="t" hint-placeholder-count="6">{tile_pic}</sc-for></div></sc-if>
<sc-if value="{{{{tray.words}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="display:flex;gap:8px"><sc-for list="{{{{tray.tiles}}}}" as="t" hint-placeholder-count="4">{tile_word}</sc-for></div></sc-if>
</div></sc-if>
{sail("margin-top:auto;width:100%")}
</section>"""
    return window(wide + panel,
                  f"Rev {REV} B - Wide sea: the whole course wide over its edge view; one panel with the voyages, the squad in formation and a tray under a pressed seat - interactive")


# ── C: Ticket ─────────────────────────────────────────────────────────────────────────────────────────────────────────

def c_board():
    rows = f"""<nav aria-label="Voyages on offer" class="abs" style="left:40px;top:244px;width:540px;display:flex;flex-direction:column;gap:6px">
<sc-for list="{{{{vlist}}}}" as="r" hint-placeholder-count="4"><button class="pick" type="button" aria-pressed="{{{{r.pressed}}}}" onClick="{{{{r.pick}}}}" style="height:150px;box-sizing:border-box;padding:0 24px;display:grid;grid-template-columns:64px 1fr;align-items:center;background:{{{{r.bg}}}};box-shadow:inset 3px 0 0 {{{{r.bar}}}}">
<span class="num" style="font-size:28px;color:{DIM}">{{{{r.n}}}}</span>
<span style="display:flex;flex-direction:column;gap:8px"><span style="font-size:28px;color:{{{{r.col}}}}">{{{{r.place}}}}</span><span class="num" style="font-size:15px;color:{DIM}">{{{{r.meta}}}}</span></span></button></sc-for></nav>"""
    rule = f'<div style="height:0;border-top:1px dashed {LINE};margin:22px 0"></div>'
    ticket = f"""<section aria-label="The open voyage" class="abs" style="left:1320px;top:244px;width:560px;height:796px">
<div class="abs sl" style="inset:0;background:{PLATE};box-shadow:inset 0 0 0 1px {LINE}"></div>
<div class="abs" style="left:30px;right:30px;top:28px;bottom:84px;display:flex;flex-direction:column">
<span class="lab">{{{{voyage.kicker}}}}</span>
<span style="font-size:48px;line-height:1.1;margin-top:12px">{{{{voyage.place}}}}</span>
<span style="font-size:18px;line-height:1.5;color:{INK};margin-top:10px">{{{{voyage.title}}}}</span>
{rule}{numbers(size=28)}{rule}
{squads()}
<div style="display:flex;flex-direction:column;margin-top:8px">
<sc-for list="{{{{seatList}}}}" as="s" hint-placeholder-count="3"><div style="opacity:{{{{s.op}}}};box-shadow:inset 3px 0 0 {{{{s.bar}}}}">
<button class="pick" type="button" aria-pressed="{{{{s.pressed}}}}" aria-label="{{{{s.alt}}}}" onClick="{{{{s.pick}}}}" style="width:100%;height:76px;display:grid;grid-template-columns:150px 1fr 170px;gap:16px;align-items:center;padding:0 12px;box-sizing:border-box">
<img class="hull" src="{{{{s.hull}}}}" alt="" style="width:150px;height:62px">
<span style="display:flex;flex-direction:column;gap:4px"><span style="font-size:18px">{{{{s.name}}}}</span><span style="font-size:15px;color:{DIM}">{{{{s.line1}}}}</span></span>
<span style="font-size:15px;color:{DIM};text-align:right">{{{{s.line2}}}}</span></button>
<sc-if value="{{{{s.open}}}}" hint-placeholder-val="{{{{ false }}}}">{steppers()}</sc-if>
</div></sc-for></div>
</div>
{sail("position:absolute;left:0;right:0;bottom:0;width:100%;height:64px")}
</section>"""
    return window(rows + sea() + ticket,
                  f"Rev {REV} C - Ticket: a quiet list, and the open voyage as one ticket - where, the numbers, the crew, Set sail as its stub - interactive")


# ── Notes ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

def notes_board():
    rows = [
        ("Asked", "\"Only thing the hypersea map and the bottom edge on view should stay apart from that ... delete the old voyages "
                  "mockups and create new\" (Shombith, 2026-11-12, on the built Rev 81 B5 and Rev 83 S2)."),
        ("Kept, in all three", "The course on the HyperSea and the same course seen edge on under it, as Rev 81 drew them."),
        ("New, in all three", "Four tabs as the game has them (Voyages, Saga, Quests, Episodes). No seat editor screen: a seat "
                              "changes where it stands, and the map is never covered. One Lime: Set sail. Jade marks the pick."),
        ("A Slide (recommended)", "Small voyage cards on their own water; the picked one slides open with its words and numbers. "
                                  "The right column is only the squad: a row per seat, and a pressed row opens Vessel, Pilot, "
                                  "Element and Artifact as four steppers."),
        ("B Wide sea", "The whole course picture, uncropped, takes the left of the window with its edge view under it. One panel on the "
                       "right: the four voyages, the open one's numbers, the squad's three hulls in formation, and a tray of tiles under a pressed seat."),
        ("C Ticket", "The quietest: a list of places with no pictures, and the open voyage as one ticket. The crew are three "
                     "lines; a pressed line opens the same steppers; Set sail is the ticket's stub."),
        ("For the build", "Each is made of pieces the menu kit already has (a plate, a text tab, a stepper, a card), so it can be "
                          "authored as prefabs rather than built in code at runtime, which is what kept the last build from updating."),
    ]
    items = "".join(f'<div style="display:grid;grid-template-columns:340px 1fr;gap:28px"><span class="cap" style="font-size:18px">{k}</span>'
                    f'<span style="font-size:18px;line-height:1.5;color:#E6E9FF">{v}</span></div>' for k, v in rows)
    body = (f'<div class="abs" style="left:80px;top:64px;width:1760px;display:flex;flex-direction:column;gap:26px">'
            f'<span style="font-size:36px">Rev {REV} - the Voyages tab, drawn new around the map and the edge view</span>{items}</div>')
    return page(f"Rev {REV} - notes: what stays, what is new in all three, the three options, the recommendation", body,
                "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}A.dc.html": a_board, f"R{REV}B.dc.html": b_board, f"R{REV}C.dc.html": c_board,
          f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
