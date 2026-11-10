#!/usr/bin/env python3
"""Rev 77 boards: every other Voyage window in the picked look, Rev 74 C3 "Place" (Frogma tab "Voyages").

Shombith, 2026-11-08: "C3 and now show mockups of all other windows".

The picked look: the menu's ground; on the left the HyperSea map column (the whole galaxy, + / - through seven tiers,
the edge-on view at its foot); on the right the coverflow of muted place close-ups. Drawn in it here:
  Crew       Ark crew: three seats and their 12 settings; a press opens that setting's strip, one press picks
  Story      the open voyage's story, one beat at a time, over the place
  Underway   a voyage that is out: the place, how far the work is, the squad flying it, Set sail dark
  Escort     the Ark escort as a card in the coverflow, its difficulty stepper
  Saga       the Saga tab: Tandava and its forms, part 2 The Severing locked
  Episodes   the Episodes tab: coming soon
  Quests     the Quests window, its own window, the five quests as place cards on the same carousel
plus the notes. The kit and data are Rev 70-74's (r70 / r71 / r74_voyages.py).

    python3 Docs/MenuRedesignB/rounds/r77_voyages.py <root> 77

Rev 75 and 76 went to other tabs while these were drawn; the round takes the next free number.
"""
import importlib.util
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
_root, _rev = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "77"
sys.argv = [sys.argv[0], _root, _rev]
_spec = importlib.util.spec_from_file_location("r74", HERE / "r74_voyages.py")
r74 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(r74)
r71, r70 = r74.r71, r74.r71.r70

REV, ROOT = int(_rev), r74.ROOT
blob, page, nav, tabrow, GROUND, squad, SAIL, TABS = r74.blob, r74.page, r74.nav, r74.tabrow, r74.GROUND, r74.squad, r74.SAIL, r74.TABS
VOYAGES, QUESTS = r74.VOYAGES, r70.QUESTS
TANDAVA = blob("ea5244253b797934bf2988bdc1260ec7")
TANDAVA_FORMS = "Great Serpent - Many-Headed Serpent - Lord of the Dance - Antlion"

# The story lines of Rev 46's voyages (the board's own content): who brought it and the water.
_r46 = (REPO / "Docs/MenuRedesignB/board/R46Cards.dc.html").read_text(encoding="utf-8")
_V46 = json.loads(re.search(r"const V = (\[.*?\]);\n", _r46, re.S).group(1))
STORY = [{"crewLine": v["crewLine"], "light": v["light"], "faction": v["faction"]} for v in _V46]

DATA = r74.DATA + "const STORY = " + json.dumps(STORY) + ";\n"
FLOW = """
  const flowOf = (items, sel, pick, step) => items.map((x, i) => { const d = i - sel, a = Math.abs(d);
    return Object.assign({}, x, { pick: () => pick(i), pressed: d === 0 ? 'true' : 'false', z: 10 - a, op: a === 0 ? 1 : a === 1 ? 0.7 : 0.35,
      x: '200px', y: '30px', edge: d === 0 ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89',
      tf: 'translateX(' + (d * (step || 250)) + 'px) rotateY(' + (-d * 40) + 'deg) scale(' + (1 - Math.min(a, 2) * 0.18) + ')' }); });
"""
START = r71.START.replace("open: -1 };", "open: -1, t: 0 };")


def stage(listname, left=1120, top=172, w=760, h=400, cw=360, ch=320):
    return (f'<div class="abs" style="left:{left}px;top:{top}px;width:{w}px;height:{h}px;perspective:1400px;overflow:hidden">'
            f'<sc-for list="{{{{{listname}}}}}" as="c" hint-placeholder-count="4">{r74.card("C3", cw, ch)}</sc-for></div>')


def place_panel(left, top, w, h, inner):
    """A big C3 face: the place's muted close-up filling a panel, the words over its foot."""
    return (f'<div class="abs sl" style="left:{left}px;top:{top}px;width:{w}px;height:{h}px;overflow:hidden;background:#00010A;'
            f'box-shadow:inset 0 0 0 1px #434C89"><img src="{{{{voyage.map}}}}" alt="{{{{voyage.place}}}}, close" style="position:absolute;'
            f'left:-60%;top:-60%;width:220%;height:220%;object-fit:cover;object-position:{{{{voyage.pos}}}};filter:saturate(.25) brightness(.55)">'
            f'<div style="position:absolute;left:0;right:0;bottom:0;padding:0 32px 30px;display:flex;flex-direction:column;gap:10px">{inner}</div></div>')


def board(body, title, extra_vals, ret, lit="Voyages", map_col=True, tabs=None, start=None):
    lg = (DATA + "class Component extends DCLogic {\nconstructor(p) { super(p); " + (start or START) + " }\nrenderVals() {" + r71.VALS
          + r74.MAP_VALS + FLOW + extra_vals + "  return { " + r71.RET + ", mx, " + ret + " };\n}\n}")
    head = GROUND + nav(crystals=False) + tabrow(tabs or TABS, lit)
    return page(title, head + (r74.map_column() if map_col else "") + body, lg)


# ── Crew ──────────────────────────────────────────────────────────────────────────────────────────────────────────────
def crew_board():
    head = """<div class="abs" style="left:1120px;top:180px;width:760px;display:flex;flex-direction:column;gap:12px">
<span class="lab">{{pinned}}</span>
<div style="display:flex;align-items:center;gap:12px"><span style="font-size:28px;margin-right:8px">{{crewName}}</span><sc-for list="{{domains}}" as="d" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{d.pressed}}" onClick="{{d.pick}}" style="padding:10px 16px;background:rgba(0,4,31,.7);box-shadow:{{d.edge}};font-family:'Chakra Petch';font-weight:600;font-size:14px;letter-spacing:.12em;text-transform:uppercase;color:{{d.col}}">{{d.name}}</button></sc-for></div>
</div>"""
    seats = """<div class="abs" style="left:1120px;top:300px;width:760px;display:flex;flex-direction:column;gap:12px">
<sc-for list="{{seats}}" as="s" hint-placeholder-count="3"><div class="sl" style="position:relative;height:212px;box-sizing:border-box;padding:16px 20px;background:rgba(0,1,10,.78);box-shadow:{{s.edge}};font-family:'Chakra Petch';font-weight:600;font-size:14px;letter-spacing:.14em;text-transform:uppercase;color:#747BAD">{{s.head}}<img class="hull" src="{{s.hull}}" alt="{{s.v}}" style="position:absolute;left:12px;top:48px;width:230px;height:150px"><div style="position:absolute;left:260px;right:20px;top:20px;display:grid;grid-template-columns:1fr 1fr;gap:10px"><sc-for list="{{s.settings}}" as="g" hint-placeholder-count="4"><button class="pick set sls" type="button" data-l="{{g.label}}" onClick="{{g.press}}" style="height:80px;box-sizing:border-box;padding:12px 16px;background:rgba(0,4,31,.7);box-shadow:{{g.edge}};font-family:'Aldrich';font-size:18px;letter-spacing:0;text-transform:none;color:{{g.col}}">{{g.value}}</button></sc-for></div></div></sc-for>
</div>
<sc-if value="{{cstrip.open}}" hint-placeholder-val="{{ false }}"><div class="abs glass sl" style="left:40px;top:596px;width:1060px;height:210px;box-sizing:border-box;padding:16px 20px;z-index:8;box-shadow:inset 0 0 0 1px #00D4FF;font-family:'Chakra Petch';font-weight:600;font-size:14px;letter-spacing:.14em;text-transform:uppercase;color:#E6E9FF">{{cstrip.caption}}<div style="display:flex;gap:10px;margin-top:12px"><sc-for list="{{cstrip.items}}" as="t" hint-placeholder-count="6"><button class="pick sls" type="button" aria-label="{{t.name}}" aria-pressed="{{t.pressed}}" onClick="{{t.pick}}" style="flex:1;height:140px;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:8px;box-shadow:{{t.edge}};font-family:'Aldrich';font-size:15px;letter-spacing:0;text-transform:none;color:#E6E9FF"><img class="hull" src="{{t.img}}" alt="" style="width:{{t.w}};height:{{t.h}}">{{t.label}}</button></sc-for></div></div></sc-if>
<a class="btn cta abs" href="#" style="left:1120px;top:990px;width:760px"><span class="kc">Y</span>Save squad</a>"""
    ev = ("  const PILOT = " + json.dumps(r70.PILOT) + ", ELEMENT = " + json.dumps(r70.ELEMENT) + ", ARTS = " + json.dumps(r70.ARTIFACTS) + """;
  const LABELS = ['Vessel', 'Pilot', 'Element', 'Artifact'], KEYS = ['v', 'p', 'e', 'a'];
  const o = st.cs, dom = st.dom || SQ[st.sq].dom;
  const setSeat = (i, k, val) => this.setState({ seats: seats.map((s, j) => j === i ? Object.assign({}, s, { [k]: val }) : s), cs: null });
  const crewSeats = seats.map((s, i) => ({ v: s.v, hull: HULL[s.v], head: '0' + (i + 1) + ' ' + s.role + ' - ' + s.who,
    edge: o && o.seat === i ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px rgba(116,123,173,.35)',
    settings: KEYS.map((k, j) => { const on = o && o.seat === i && o.set === j; return { label: LABELS[j], value: s[k],
      col: s[k] === 'No artifact' ? '#747BAD' : '#E6E9FF', edge: on ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px rgba(116,123,173,.35)',
      press: () => this.setState({ cs: on ? null : { seat: i, set: j } }) }; }) }));
  let cstrip = { open: false, caption: '', items: [] };
  if (o) {
    const s = seats[o.seat], k = KEYS[o.set];
    let items;
    if (o.set === 0) items = HULLS.map((h) => ({ name: h, img: HULL[h], w: '150px', h: '100px', label: '' }));
    else if (o.set === 1) items = Object.keys(PILOT).map((n) => ({ name: n, img: PILOT[n], w: '72px', h: '92px', label: n }));
    else if (o.set === 2) items = Object.keys(ELEMENT).map((n) => ({ name: n, img: ELEMENT[n], w: '56px', h: '56px', label: n }));
    else items = ARTS.map((n) => ({ name: n, img: '', w: '0px', h: '0px', label: n }));
    cstrip = { open: true, caption: s.role + ' - ' + LABELS[o.set] + ': one press picks', items: items.map((t) => Object.assign(t, {
      pressed: s[k] === t.name ? 'true' : 'false', edge: s[k] === t.name ? 'inset 0 0 0 2px #00D4FF' : 'none', pick: () => setSeat(o.seat, k, t.name) })) };
  }
  const domains = ['Jade', 'Ruby', 'Gold'].map((d) => ({ name: d, pressed: d === dom ? 'true' : 'false', col: d === dom ? '#E6E9FF' : '#747BAD',
    edge: 'inset 3px 0 0 ' + DOMC[d] + (d === dom ? ', inset 0 0 0 1px #00D4FF' : ', inset 0 0 0 1px rgba(116,123,173,.45)'), pick: () => this.setState({ dom: d }) }));
  const pinned = 'Voyage ' + v.n + ' - ' + v.title + ' - stays as it is', crewName = SQ[st.sq].name;
""")
    return board(head + seats, f"Rev {REV} Crew - Ark crew in the picked look: three seats and their 12 settings; a press opens that setting's strip, one press picks - interactive",
                 ev, "seats: crewSeats, cstrip, domains, pinned, crewName")


# ── Story ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
def story_board():
    inner = ('<span class="lab">{{beat.label}}</span><span style="font-size:28px;line-height:1.3">{{beat.line}}</span>'
             '<span style="font-size:15px;color:#E6E9FF">{{beat.by}}</span>')
    body = ("""<div class="abs" style="left:1120px;top:180px;width:760px;display:flex;flex-direction:column;gap:10px"><span class="lab">{{voyage.kicker}}</span><span style="font-size:36px;line-height:1.1">{{voyage.title}}</span></div>"""
            + place_panel(1120, 300, 760, 560, inner)
            + """<div class="abs" style="left:1120px;top:884px;width:760px;display:flex;align-items:center;justify-content:space-between">
<button class="btn sm def" type="button" onClick="{{beat.prev}}" aria-label="The beat before">&lt;</button>
<svg aria-hidden="true" width="120" height="10"><rect x="0" y="3" width="{{beat.d0}}" height="4" fill="{{beat.f0}}"></rect><rect x="34" y="3" width="{{beat.d1}}" height="4" fill="{{beat.f1}}"></rect><rect x="68" y="3" width="{{beat.d2}}" height="4" fill="{{beat.f2}}"></rect><rect x="102" y="3" width="{{beat.d3}}" height="4" fill="{{beat.f3}}"></rect></svg>
<button class="btn sm def" type="button" onClick="{{beat.next}}" aria-label="The next beat">&gt;</button>
</div>""" + SAIL.format(x=1120, y=990, w=760))
    ev = """  const S = STORY[st.v], b = st.b || 0;
  const beats = [{ label: 'The news', line: v.line, by: 'From Tumult' }, { label: 'The place', line: v.placeLine, by: v.place },
    { label: 'Who brought it', line: S.crewLine, by: v.crew + ' - ' + S.faction }, { label: 'The water', line: S.light, by: v.ly + ' on the way' }];
  const beat = Object.assign({}, beats[b], { prev: () => this.setState({ b: (b + 3) % 4 }), next: () => this.setState({ b: (b + 1) % 4 }) });
  beats.forEach((x, i) => { beat['d' + i] = i === b ? 30 : 12; beat['f' + i] = i === b ? '#E6E9FF' : '#434C89'; });
"""
    return board(body, f"Rev {REV} Story - the open voyage's story in the picked look: one beat at a time over the place - interactive", ev, "beat")


# ── Under way ─────────────────────────────────────────────────────────────────────────────────────────────────────────
def underway_board():
    inner = '<span style="font-size:48px;line-height:1">{{voyage.place}}</span>'
    body = (place_panel(1120, 180, 760, 420, inner)
            + """<div class="abs" style="left:1120px;top:624px;width:760px;display:flex;flex-direction:column;gap:12px">
<span class="cap" style="font-size:22px;color:#E6E9FF">{{under.head}}</span>
<span class="cap num" style="font-size:15px;color:#E6E9FF">{{under.progress}}</span>
<div style="height:8px;background:#222645"><div style="height:8px;width:{{under.fill}};background:#E6E9FF"></div></div>
</div>
<div class="abs" style="left:1120px;top:790px;width:760px;display:flex;flex-direction:column;gap:10px">
<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:10px"><sc-for list="{{formation}}" as="f" hint-placeholder-count="3"><img class="hull" src="{{f.hull}}" alt="{{f.alt}}" style="width:100%;height:110px"></sc-for></div>
<span style="font-size:15px;color:#E6E9FF;white-space:pre">{{crewLine}}</span></div>
<span class="btn abs" style="left:1120px;top:990px;width:760px;background:rgba(0,4,31,.6);color:#747BAD;box-shadow:inset 0 0 0 1px #434C89;cursor:default">{{under.sail}}</span>""")
    ev = """  const under = { head: 'Under way to ' + v.place, progress: 'Stowed 1 of 3', fill: '33%', sail: 'Set sail - after this voyage' };
"""
    return board(body, f"Rev {REV} Underway - a voyage that is out, in the picked look: the place, how far the work is, the squad flying it, Set sail dark - interactive",
                 ev, "under", start=START.replace("t: 0 };", "t: 3 };"))


# ── Escort ────────────────────────────────────────────────────────────────────────────────────────────────────────────
def escort_board():
    body = (stage("eflow")
            + """<div class="abs" style="left:1120px;top:588px;width:760px;display:flex;align-items:center;justify-content:space-between">
<span class="lab">{{esc.kicker}}</span>
<sc-if value="{{esc.isEscort}}" hint-placeholder-val="{{ true }}"><div style="display:flex;align-items:center;gap:14px"><button class="btn sm def" type="button" onClick="{{esc.down}}" aria-label="Easier">-</button><span class="num" style="font-size:22px">{{esc.diff}}</span><button class="btn sm def" type="button" onClick="{{esc.up}}" aria-label="Harder">+</button></div></sc-if>
</div>""" + squad(1120, 700, 760, 92, 1120, 468, 760) + SAIL.format(x=1120, y=990, w=760))
    ev = """  const d = st.d || 2, e = st.e || 0;
  const items = [{ title: 'Escort the Ark - difficulty ' + d, place: 'Escort the Ark', map: V[1].map, pos: '70% 92%' }]
    .concat(V.slice(0, 3).map((x) => ({ title: x.title, place: x.place, map: x.map, pos: x.pos })));
  const eflow = flowOf(items, e, (i) => this.setState({ e: i, open: -1 }));
  const esc = { isEscort: e === 0, diff: 'Difficulty ' + d, down: () => this.setState({ d: Math.max(1, d - 1) }), up: () => this.setState({ d: Math.min(4, d + 1) }),
    kicker: e === 0 ? 'Ark escort - Tumult to Tollmoor' : 'Voyage ' + V[e - 1].n + ' - ' + V[e - 1].min };
"""
    return board(body, f"Rev {REV} Escort - the Ark escort as a card in the picked coverflow, its difficulty stepper under it - interactive", ev, "eflow, esc")


# ── Saga ──────────────────────────────────────────────────────────────────────────────────────────────────────────────
def saga_board():
    left = f"""<figure class="abs sl" style="left:40px;top:172px;width:1060px;height:868px;margin:0;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89">
<img src="{TANDAVA}" alt="Tandava" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;filter:saturate(.35) brightness(.6)">
<div style="position:absolute;left:0;right:0;bottom:0;padding:0 36px 34px;display:flex;flex-direction:column;gap:10px"><span class="lab">Saga - part {{{{saga.part}}}} of 2</span><span style="font-size:48px;line-height:1">Tandava</span><span style="font-size:18px;color:#E6E9FF">{TANDAVA_FORMS}</span></div>
</figure>"""
    body = (left + stage("sflow")
            + """<div class="abs" style="left:1120px;top:588px;width:760px"><span style="font-size:18px;line-height:1.45;color:#E6E9FF">{{saga.line}}</span></div>"""
            + squad(1120, 700, 760, 92, 1120, 468, 760)
            + '<sc-if value="{{saga.open}}" hint-placeholder-val="{{ true }}">' + SAIL.format(x=1120, y=990, w=760) + '</sc-if>'
              '<sc-if value="{{saga.locked}}" hint-placeholder-val="{{ false }}"><span class="btn abs" style="left:1120px;top:990px;width:760px;background:rgba(0,4,31,.6);color:#747BAD;box-shadow:inset 0 0 0 1px #434C89">After Tandava</span></sc-if>')
    ev = ("  const TANDAVA = " + json.dumps(TANDAVA) + """;
  const k = st.k || 0;
  const sflow = flowOf([{ title: 'Tandava', place: '01 Tandava', map: TANDAVA, pos: '50% 40%' }, { title: 'The Severing', place: '02 The Severing', map: TANDAVA, pos: '50% 80%' }],
    k, (i) => this.setState({ k: i }), 300);
  const saga = { part: k + 1, open: k === 0, locked: k === 1, line: k === 0 ? 'Two parts. The second opens when the first is done.' : 'The Severing opens when Tandava is done.' };
""")
    return board(body, f"Rev {REV} Saga - the Saga tab in the picked look: Tandava and its forms, part 2 The Severing locked - interactive", ev, "sflow, saga", lit="Saga", map_col=False)


# ── Episodes ──────────────────────────────────────────────────────────────────────────────────────────────────────────
def episodes_board():
    galaxy = r74.TIERS[0][2]
    body = f"""<div class="abs sl" style="left:460px;top:220px;width:1000px;height:600px;overflow:hidden;background:#00010A;box-shadow:inset 0 0 0 1px #434C89">
<img src="{galaxy}" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;filter:saturate(.2) brightness(.5)">
<div style="position:absolute;left:0;right:0;bottom:0;padding:0 40px 36px;display:flex;flex-direction:column;gap:12px"><span style="display:flex;align-items:center;gap:14px;font-size:48px;line-height:1">{r70.LOCK.replace('width="14" height="16"', 'width="30" height="34"')}Episode 2</span><span class="cap" style="font-size:22px;color:#E6E9FF">Coming soon</span><span style="font-size:18px;color:#E6E9FF">New episodes arrive here. Voyages and the Saga are open now.</span></div>
</div>
<a class="btn def abs" href="R{REV}Saga.dc.html" style="left:760px;top:868px;width:400px"><span class="kc">LB</span>Back to the Saga</a>"""
    return board(body, f"Rev {REV} Episodes - the Episodes tab in the picked look: coming soon - interactive", "", "voyage", lit="Episodes", map_col=False)


# ── Quests ────────────────────────────────────────────────────────────────────────────────────────────────────────────
def quests_board():
    head = """<div class="abs" style="left:40px;top:186px;right:40px;display:flex;align-items:flex-end;justify-content:space-between">
<span style="font-size:48px;line-height:1">The Hypersea</span>
<div style="display:flex;align-items:center;gap:18px"><span class="num" style="font-size:20px">{{progress}}</span><div style="width:320px;height:8px;background:#222645"><div style="height:8px;width:{{fill}};background:#E6E9FF"></div></div></div>
</div>"""
    body = (head + stage("qflow", left=40, top=270, w=1840, h=470, cw=420, ch=400).replace("left:{{c.x}}", "left:{{c.x}}")
            + """<div class="abs glass sl" style="left:40px;top:800px;width:1840px;height:180px;box-sizing:border-box;padding:28px 36px;display:flex;align-items:center;justify-content:space-between">
<div style="display:flex;flex-direction:column;gap:10px"><span style="font-size:28px">{{focus.title}}</span><span style="font-size:18px;color:#E6E9FF">{{focus.goal}}</span></div>
<sc-if value="{{focus.claim}}" hint-placeholder-val="{{ false }}"><button class="btn cta" type="button" onClick="{{focus.act}}"><span class="kc">A</span>Claim</button></sc-if>
<sc-if value="{{focus.play}}" hint-placeholder-val="{{ true }}"><button class="btn cta" type="button"><span class="kc">A</span>Play quest</button></sc-if>
<sc-if value="{{focus.wait}}" hint-placeholder-val="{{ false }}"><span class="cap" style="font-size:18px;color:#E6E9FF">{{focus.waitLine}}</span></sc-if>
</div>""")
    lg = ("const Q = " + json.dumps(QUESTS) + """;
class Component extends DCLogic {
constructor(p) { super(p); this.state = { states: Q.map((q) => q.state), sel: 2 }; }
renderVals() {
  const st = this.state, s = st.states;
  const line = (x, i) => x === 'claimed' ? 'Claimed' : x === 'done' ? 'Done - claim it' : x === 'open' ? 'Next to play' : 'After ' + Q[i - 1].name;
  const qflow = Q.map((q, i) => { const d = i - st.sel, a = Math.abs(d);
    return { title: q.name, place: q.n + '  ' + q.name, map: q.art, pos: '50% 50%', pick: () => this.setState({ sel: i }), pressed: d === 0 ? 'true' : 'false',
      z: 10 - a, op: s[i] === 'locked' ? (a === 0 ? 0.6 : 0.3) : a === 0 ? 1 : a === 1 ? 0.75 : 0.45, x: '710px', y: '20px',
      edge: d === 0 ? 'inset 0 0 0 2px #00D4FF' : 'inset 0 0 0 1px #434C89',
      tf: 'translateX(' + (d * 330) + 'px) rotateY(' + (-d * 34) + 'deg) scale(' + (1 - Math.min(a, 2) * 0.16) + ')' }; });
  const k = s[st.sel], cur = Q[st.sel], claimed = s.filter((x) => x === 'claimed').length;
  const act = () => { if (k === 'done') { const n = s.slice(); n[st.sel] = 'claimed'; this.setState({ states: n }); } };
  const focus = { title: cur.n + '  ' + cur.name + ' - ' + line(k, st.sel), goal: cur.goal, claim: k === 'done', play: k === 'open', wait: k === 'locked' || k === 'claimed',
    waitLine: k === 'claimed' ? 'Claimed' : 'Unlocks ' + line(k, st.sel).toLowerCase(), act };
  return { qflow, focus, progress: claimed + ' of ' + Q.length + ' claimed', fill: Math.round(100 * claimed / Q.length) + '%' };
}
}""")
    tabs = [("Episode 1", False), ("Episode 2", True)]
    return page(f"Rev {REV} Quests - the Quests window, its own window, in the picked look: the five quests as place cards on the carousel - interactive",
                GROUND + nav() + tabrow(tabs, "Episode 1") + body, lg)


def notes_board():
    blocks = [
        ("Picked", "Rev 74 C3, Place (Shombith, 2026-11-08): \"C3 and now show mockups of all other windows\"."),
        ("The look, everywhere", "The menu's ground; the HyperSea map column on the left (the whole galaxy, + / - through seven tiers, the edge-on view at its foot) wherever a voyage is on screen; the muted place close-up as the card face; the carousel for every choice of one among many."),
        ("The windows", "Crew: three seats and their 12 settings, a press opens that setting's strip (hulls, pilots' portraits, the Codex's element shapes, artifacts), one press picks, Save squad keeps it. Story: one beat at a time over the place. Underway: the voyage that is out. Escort: the Ark escort in the carousel with its difficulty. Saga: Tandava, part 2 locked. Episodes: coming soon."),
        ("Quests", "Its own window, opened from Home like the Profile: Episode 1's five quests on the same carousel, muted art, the claimed count, one Lime button (Claim or Play quest)."),
        ("Still a proposal", "The voyage that stays put while the crew or domain changes is Garrett's voyage rules to change (section 147). Underway's words (Stowed 1 of 3) are the game's own progress lines."),
        ("Build", "After the pick: the Voyage window's generator (author_voyage_window.py, Garrett's) and VoyageOpenCard, with the vessel strip of the configure card (Yash's ArenaLaunchPanel)."),
    ]
    cells = "".join(f'<div style="display:flex;flex-direction:column;gap:10px"><span class="lab">{h}</span>'
                    f'<span style="font-size:18px;line-height:1.5">{t}</span></div>' for h, t in blocks)
    body = (f'<span class="abs" style="left:56px;top:44px;font-size:36px">Rev {REV} - notes</span>'
            f'<div class="abs" style="left:56px;top:120px;width:1808px;display:grid;grid-template-columns:1fr 1fr;gap:34px 64px">{cells}</div>')
    return page(f"Rev {REV} - notes: every other Voyage window in the picked look (C3), the Quests window, what is still a proposal",
                body, "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}", h=900)


BOARDS = {f"R{REV}Crew.dc.html": crew_board, f"R{REV}Story.dc.html": story_board, f"R{REV}Underway.dc.html": underway_board,
          f"R{REV}Escort.dc.html": escort_board, f"R{REV}Saga.dc.html": saga_board, f"R{REV}Episodes.dc.html": episodes_board,
          f"R{REV}Quests.dc.html": quests_board, f"R{REV}Notes.dc.html": notes_board}

if __name__ == "__main__":
    for name, fn in BOARDS.items():
        (ROOT / name).write_text(fn(), encoding="utf-8")
        print("wrote", ROOT / name)
