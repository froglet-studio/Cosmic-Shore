#!/usr/bin/env python3
"""Rev 82 board: the picked B5, playable - how the squad is chosen and changed (Frogma tab "Voyages",
Docs/MENU_REDESIGN_B.md section 163).

Shombith, 2026-11-09: "B5 delete others and show me how the squad selection and changing works, create a live
playable mockup". One board, the whole Voyage window as Rev 81 B5 drew it, with the squad live:

  - the squads are chips over the cards; a press switches the squad, and its three seats with it;
  - a press on a seat you may change opens the seat editor over the course: Vessel, Pilot, Element, Artifact;
  - one press on a choice picks it, and the card behind changes at once; Done (or B) closes the editor;
  - a pilot flies one seat: picking one who flies another seat swaps them (the Ark's rule, SavedSquad.KeepCardsOwn);
  - a seat you may not change shows whose it is: a friend's seat in your squad, every seat but yours in a squad you
    joined (the Ark's rule, SquadsPage.CanEdit);
  - the voyages on the left still pick the voyage; Set sail stays the one Lime.

    python3 Docs/MenuRedesignB/rounds/r82_voyages.py <scratch root> [rev]
"""
import importlib.util
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "82"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r81", HERE / "r81_voyages.py")
r81 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r81)
r80, r71 = r81.r80, r81.r80.r71

REV, ROOT, page, nav, GROUND = int(_rev), r81.ROOT, r80.page, r80.nav, r80.GROUND
HULL, PILOT, SQUADS = r71.HULL, r80.PILOT, r80.SQUADS
ELEMENTS = ["Charge", "Mass", "Space", "Time"]
ARTIFACTS = ["No artifact", "Extra Bay", "Seed Pod", "Tailwind", "Long Edge"]
PX, PW, MAP_W = r81.PX, r81.PW, r81.MAP_W

DATA = ("const V = " + json.dumps(r71.VOYAGES) + ";\nconst SQ = " + json.dumps(SQUADS) + ";\nconst HULL = " + json.dumps(HULL)
        + ";\nconst PILOT = " + json.dumps(PILOT) + ";\nconst ELEMENTS = " + json.dumps(ELEMENTS)
        + ";\nconst ARTIFACTS = " + json.dumps(ARTIFACTS) + ";\n")

LOGIC = DATA + """class Component extends DCLogic {
constructor(p) { super(p); this.state = { v: 1, sq: 0, books: SQ.map((q) => q.seats.map((s) => Object.assign({}, s))), open: -1, tab: 0 }; }
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
  const voyage = { title: v.title, line: v.line, ly: v.ly, min: v.min, i: String(v.i), placeLine: v.placeLine, map: v.map, pos: v.pos, edge: v.edge };
  const vlist = V.map((x, i) => ({ n: '0' + x.n, place: x.place, map: x.map, pos: x.pos, crew: 'Brought by ' + x.crew,
    meta: x.ly + ' \\u00B7 ' + x.min + ' \\u00B7 Intensity ' + x.i, pressed: i === st.v ? 'true' : 'false',
    col: i === st.v ? '#E6E9FF' : '#747BAD', bar: i === st.v ? '#00D4FF' : 'rgba(0,0,0,0)', bg: i === st.v ? 'rgba(0,4,31,.86)' : 'rgba(0,4,31,0)',
    pick: () => this.setState({ v: i }) }));
  const tabs = ['Voyages', 'Saga', 'Episodes'].map((t, i) => ({ name: t, col: i === 0 ? '#E6E9FF' : '#747BAD',
    line: i === 0 ? '#E6E9FF' : 'rgba(0,0,0,0)', pressed: i === 0 ? 'true' : 'false', pick: () => {} }));
  const presets = SQ.map((s, i) => ({ name: s.mine ? s.name : s.name + ' - ' + s.owner + "'s", pressed: i === st.sq ? 'true' : 'false',
    col: i === st.sq ? '#E6E9FF' : '#747BAD', edge: i === st.sq ? 'inset 0 -2px 0 #00D4FF' : 'inset 0 0 0 1px #434C89',
    pick: () => this.setState({ sq: i, open: -1 }) }));
  const cards = seats.map((s, i) => { const ok = canEdit(i);
    return { face: PILOT[s.p], hull: HULL[s.v], name: s.v, line1: who(s) + ' - ' + s.p, line2: ok ? s.e + ' - ' + s.a : (s.who + "'s seat"),
      edge: st.open === i ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89', op: ok ? 1 : 0.55, pressed: st.open === i ? 'true' : 'false',
      alt: s.role + ', ' + s.who + ': ' + s.v + (ok ? ', press to change' : ', not yours to change'),
      pick: ok ? () => this.setState({ open: st.open === i ? -1 : i, tab: 0 }) : () => {} }; });
  let ed = { open: false, title: '', isV: false, isP: false, isE: false, isA: false, tabs: [], hulls: [], pilots: [], els: [], arts: [], done: () => {} };
  if (st.open >= 0) {
    const i = st.open, s = seats[i];
    const tile = (on) => on ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89';
    ed = { open: true, title: s.role + ' - ' + who(s) + ' - ' + q.name, isV: st.tab === 0, isP: st.tab === 1, isE: st.tab === 2, isA: st.tab === 3,
      tabs: ['Vessel', 'Pilot', 'Element', 'Artifact'].map((t, k) => ({ name: t, pressed: st.tab === k ? 'true' : 'false',
        col: st.tab === k ? '#E6E9FF' : '#747BAD', line: st.tab === k ? '#E6E9FF' : 'rgba(0,0,0,0)', pick: () => this.setState({ tab: k }) })),
      hulls: Object.keys(HULL).map((h) => ({ name: h, img: HULL[h], pressed: s.v === h ? 'true' : 'false', edge: tile(s.v === h), pick: () => set(i, { v: h }) })),
      pilots: Object.keys(PILOT).map((p) => { const j = seats.findIndex((x) => x.p === p);
        return { name: p, img: PILOT[p], pressed: s.p === p ? 'true' : 'false', edge: tile(s.p === p),
          note: j >= 0 && j !== i ? 'Flies ' + seats[j].v + ' - swaps' : (s.p === p ? 'Flying' : ''), pick: () => set(i, { p: p }) }; }),
      els: ELEMENTS.map((e) => ({ name: e, pressed: s.e === e ? 'true' : 'false', edge: tile(s.e === e), pick: () => set(i, { e: e }) })),
      arts: ARTIFACTS.map((a) => ({ name: a, pressed: s.a === a ? 'true' : 'false', edge: tile(s.a === a), pick: () => set(i, { a: a }) })),
      done: () => this.setState({ open: -1 }) };
  }
  return { voyage, vlist, tabs, presets, cards, ed };
}
}"""


def board():
    cards = ('<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-top:14px">'
             '<sc-for list="{{cards}}" as="s" hint-placeholder-count="3"><button class="pick sl" type="button" aria-pressed="{{s.pressed}}" '
             'aria-label="{{s.alt}}" onClick="{{s.pick}}" style="height:330px;display:flex;flex-direction:column;align-items:center;'
             'justify-content:flex-end;gap:6px;padding:0 8px 16px;opacity:{{s.op}};background:linear-gradient(180deg,rgba(0,4,31,.2),'
             'rgba(0,4,31,.92));box-shadow:{{s.edge}}"><img src="{{s.face}}" alt="" style="width:100%;height:150px;object-fit:contain;'
             'object-position:50% 100%"><img class="hull" src="{{s.hull}}" alt="" style="width:100%;height:82px">'
             '<span style="font-size:18px">{{s.name}}</span><span style="font-size:15px;line-height:1.5;color:#E6E9FF;text-align:center">'
             '{{s.line1}}<br>{{s.line2}}</span></button></sc-for></div>')
    presets = ('<div style="display:flex;align-items:center;gap:10px;margin-top:auto"><span class="lab">Squad</span>'
               '<sc-for list="{{presets}}" as="p" hint-placeholder-count="2"><button class="pick sls" type="button" aria-pressed="{{p.pressed}}" '
               'onClick="{{p.pick}}" style="padding:10px 16px;font-size:15px;color:{{p.col}};box-shadow:{{p.edge}}">{{p.name}}</button></sc-for></div>')
    panel = f"""<section class="abs" style="left:{PX}px;top:244px;width:{PW}px;height:796px;display:flex;flex-direction:column">
<span style="font-size:28px;line-height:1.25">{{{{voyage.title}}}}</span>
<span style="font-size:18px;line-height:1.5;color:#E6E9FF;margin-top:12px">{{{{voyage.line}}}}</span>
<div style="margin-top:22px;display:grid;grid-template-columns:repeat(3,1fr);gap:16px"><div style="display:flex;flex-direction:column;gap:6px"><span class="lab">Distance</span><span class="num" style="font-size:28px">{{{{voyage.ly}}}}</span></div><div style="display:flex;flex-direction:column;gap:6px"><span class="lab">Sail</span><span class="num" style="font-size:28px">{{{{voyage.min}}}}</span></div><div style="display:flex;flex-direction:column;gap:6px"><span class="lab">Intensity</span><span class="num" style="font-size:28px">{{{{voyage.i}}}}</span></div></div>
{presets}
{cards}
<a class="btn cta" href="#" style="margin-top:18px;width:100%"><span class="kc">A</span>Set sail</a>
</section>"""
    tile_v = ('<button class="pick sls" type="button" aria-label="{{t.name}}" aria-pressed="{{t.pressed}}" onClick="{{t.pick}}" '
              'style="height:170px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:6px;padding-bottom:12px;'
              'box-shadow:{{t.edge}}"><img class="hull" src="{{t.img}}" alt="" style="width:86%;height:110px">'
              '<span class="cap" style="font-size:14px;color:#E6E9FF">{{t.name}}</span></button>')
    tile_p = ('<button class="pick sls" type="button" aria-label="{{t.name}}" aria-pressed="{{t.pressed}}" onClick="{{t.pick}}" '
              'style="height:360px;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:6px;padding-bottom:14px;'
              'box-shadow:{{t.edge}}"><img src="{{t.img}}" alt="" style="width:100%;height:260px;object-fit:contain;object-position:50% 100%">'
              '<span class="cap" style="font-size:14px;color:#E6E9FF">{{t.name}}</span><span style="font-size:14px;color:#747BAD;text-align:center;min-height:20px">{{t.note}}</span></button>')
    tile_w = ('<button class="pick sls" type="button" aria-label="{{{{t.name}}}}" aria-pressed="{{{{t.pressed}}}}" onClick="{{{{t.pick}}}}" '
              'style="height:{h}px;display:flex;align-items:center;padding:0 24px;font-size:22px;color:#E6E9FF;box-shadow:{{{{t.edge}}}}">'
              '{{{{t.name}}}}</button>')
    editor = f"""<sc-if value="{{{{ed.open}}}}" hint-placeholder-val="{{{{ false }}}}"><div class="abs sl" style="left:620px;top:244px;width:{MAP_W}px;height:796px;box-sizing:border-box;padding:24px 26px;z-index:9;background:#00010A;box-shadow:inset 0 0 0 1px #00D4FF;display:flex;flex-direction:column;gap:18px">
<div style="display:flex;align-items:center;justify-content:space-between"><span class="lab" style="color:#E6E9FF">{{{{ed.title}}}}</span><button class="btn sm def" type="button" onClick="{{{{ed.done}}}}"><span class="kc">B</span>Done</button></div>
<div style="display:flex;gap:28px"><sc-for list="{{{{ed.tabs}}}}" as="k" hint-placeholder-count="4"><button class="pick cap" type="button" aria-pressed="{{{{k.pressed}}}}" onClick="{{{{k.pick}}}}" style="font-size:18px;color:{{{{k.col}}}};padding-bottom:8px;border-bottom:2px solid {{{{k.line}}}}">{{{{k.name}}}}</button></sc-for></div>
<sc-if value="{{{{ed.isV}}}}" hint-placeholder-val="{{{{ true }}}}"><div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px"><sc-for list="{{{{ed.hulls}}}}" as="t" hint-placeholder-count="6">{tile_v}</sc-for></div></sc-if>
<sc-if value="{{{{ed.isP}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="display:grid;grid-template-columns:repeat(4,1fr);gap:12px"><sc-for list="{{{{ed.pilots}}}}" as="t" hint-placeholder-count="4">{tile_p}</sc-for></div></sc-if>
<sc-if value="{{{{ed.isE}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="display:grid;grid-template-columns:repeat(2,1fr);gap:12px"><sc-for list="{{{{ed.els}}}}" as="t" hint-placeholder-count="4">{tile_w.format(h=120)}</sc-for></div></sc-if>
<sc-if value="{{{{ed.isA}}}}" hint-placeholder-val="{{{{ false }}}}"><div style="display:flex;flex-direction:column;gap:10px"><sc-for list="{{{{ed.arts}}}}" as="t" hint-placeholder-count="5">{tile_w.format(h=72)}</sc-for></div></sc-if>
<span style="margin-top:auto;font-size:15px;color:#747BAD">One press picks, and the card changes at once. A pilot flies one seat: picking one who flies another seat swaps them.</span>
</div></sc-if>"""
    body = r81.left() + panel + editor
    return page(f"Rev {REV} - B5 playable: choosing the squad and changing its seats - interactive",
                GROUND + nav(crystals=False) + r81.r80.r79.header() + body, LOGIC)


BOARDS = {f"R{REV}Squad.dc.html": board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
