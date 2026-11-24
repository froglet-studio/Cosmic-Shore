#!/usr/bin/env python3
"""Frogma round: the Ark's Artifacts tab, where you touch the artifacts in the room itself (Shombith, 2026-11-24).

Shombith on the built Rev 105 B (the medallion rail along the floor): "Artifacts again doesnt make sense I cannot click
in the image itself that should be what I should be able to do. Rework on this and give me different design
variations."

Every option is the full Ark screen on its Artifacts tab over the built backdrop (Rev 86's room, the clip the game
plays) with the same content and the same three levels as Rev 105 (the hoard, one artifact, feeding it). The rail of
medallions is gone in all four: the four artifacts on their pedestals ARE the buttons (a hit area over each one in the
clip, its pedestal and halo lit on hover or pad focus), and the dark prisms in the alcoves are the slots still to find.
Only what happens when you touch one differs:

  A  lit where it stands: its pedestal lights, the rest of the room dims, the words hang in the air beside it;
  B  step up to it: the view walks in to its pedestal, the words and its four tracks beside it;
  C  written on the floor: the words lie on the deck in front of its pedestal, lit like the floor rings;
  D  the column shows it: a beam ties it to the column of light, whose upper half plays what it does (Rev 105 B's
     lens, which Shombith picked, moved into the room).

Content, numbers and the feed rule come from r105_artifacts.py (the same four artifacts, 10 crystals of each, a feed
of 5). Positions are measured on the clip's first frame (artifacts_Nocturne_loop.mp4, 1920 x 1080).

    python3 Docs/MenuRedesignB/rounds/r140_artifacts_in_room.py <root> [--round N]
"""
import argparse
import copy
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import r105_artifacts as r105  # noqa: E402

# Measured on the clip (1920 x 1080): each artifact's centre and size, its pedestal's top ring.
SPOTS = [
    {"cx": 530, "top": 482, "w": 120, "px": 530, "py": 695, "rx": 58, "ry": 11, "hx": 530, "hy": 588},   # Javelin
    {"cx": 770, "top": 505, "w": 210, "px": 762, "py": 672, "rx": 46, "ry": 10, "hx": 770, "hy": 562},   # Hydra
    {"cx": 1034, "top": 482, "w": 110, "px": 1038, "py": 675, "rx": 56, "ry": 10, "hx": 1030, "hy": 562},  # Aftershock
    {"cx": 1204, "top": 492, "w": 200, "px": 1205, "py": 712, "rx": 66, "ry": 12, "hx": 1202, "hy": 578},  # Aegis
]
# The dark prisms in the alcoves: the artifacts still to find.
SLOTS = [{"x": 345, "y": 578}, {"x": 620, "y": 560}, {"x": 1328, "y": 555}, {"x": 1448, "y": 585}]
COLUMN_X = 820

EXTRA_CSS = """
.spot{position:absolute;background:none;border:0;padding:0;cursor:pointer;z-index:3}
.spot .halo{position:absolute;left:50%;width:320px;height:320px;margin:-160px 0 0 -160px;border-radius:50%;background:radial-gradient(circle,rgba(0,212,255,.28) 0%,rgba(0,212,255,.08) 45%,rgba(0,212,255,0) 70%);opacity:0;transition:opacity 260ms ease;pointer-events:none;mix-blend-mode:screen}
.spot .ring{position:absolute;overflow:visible;opacity:0;transition:opacity 260ms ease;pointer-events:none}
.spot .tag{position:absolute;left:50%;transform:translateX(-50%);white-space:nowrap;opacity:0;transition:opacity 200ms ease;pointer-events:none}
.spot:hover .halo,.spot:focus-visible .halo{opacity:.6}
.spot:hover .ring,.spot:focus-visible .ring{opacity:.75}
.spot:hover .tag,.spot:focus-visible .tag{opacity:1}
.spot.on .halo{opacity:1}.spot.on .ring{opacity:1}.spot.on .tag{opacity:0}
.spot.dim{opacity:.85}
.spot:focus-visible{outline:none}
.slot{position:absolute;width:70px;height:90px;margin:-45px 0 0 -35px;background:none;border:0;padding:0;cursor:pointer;z-index:3}
.slot span{position:absolute;inset:0;border-radius:50%;box-shadow:inset 0 0 0 1px rgba(116,123,173,.0);display:flex;align-items:center;justify-content:center;font-family:'Chakra Petch',sans-serif;font-size:22px;color:rgba(184,192,240,0);transition:all 220ms ease}
.slot:hover span,.slot:focus-visible span{box-shadow:inset 0 0 0 1px rgba(184,192,240,.55);color:rgba(184,192,240,.9)}
.slot:focus-visible{outline:none}
.world{position:absolute;left:0;top:0;width:1920px;height:1080px;transform-origin:0 0;transition:transform 900ms cubic-bezier(.22,.8,.25,1)}
@keyframes beam{from{stroke-dashoffset:40}to{stroke-dashoffset:0}}
@keyframes up{0%{transform:translateY(0);opacity:0}20%{opacity:1}100%{transform:translateY(-120px);opacity:0}}
"""

HELMET = r105.HELMET.replace("</style>", EXTRA_CSS + "</style>")


def chrome():
    """r105's band and tab row, without its backdrop image: the image moves into the world layer (B walks into it)."""
    c = r105.chrome()
    start = c.index("<img class=\"bg\"")
    end = c.index(">", start) + 1
    return c[end:]


WORLD = (
    '<div class="world" style="transform:translate({{world.x}}px,{{world.y}}px) scale({{world.s}})">'
    f'<img class="bg" src="{r105.BACKDROP}" alt="The Artifacts room: four artifacts float over pedestals round a column '
    'of light, dark alcoves down both walls, a porthole onto the cell">'
    # the spotlight: the room dims round the picked artifact
    '<div class="abs" style="inset:0;pointer-events:none;transition:opacity 600ms ease;opacity:{{spot.op}};background:'
    'radial-gradient(ellipse 300px 360px at {{spot.x}}px {{spot.y}}px,rgba(0,1,10,0) 0%,rgba(0,1,10,0) 45%,rgba(0,1,10,.62) 100%)"></div>'
    '<sc-for list="{{slots}}" as="q" hint-placeholder-count="4"><button class="slot" type="button" onClick="{{q.pick}}" '
    'aria-label="An empty alcove: an artifact still to find" style="left:{{q.x}}px;top:{{q.y}}px"><span>?</span></button></sc-for>'
    '<sc-for list="{{spots}}" as="s" hint-placeholder-count="4"><button class="spot {{s.cls}}" type="button" '
    'onClick="{{s.pick}}" aria-label="{{s.aria}}" aria-pressed="{{s.pressed}}" '
    'style="left:{{s.left}}px;top:{{s.top}}px;width:{{s.w}}px;height:{{s.h}}px">'
    '<span class="halo" style="top:{{s.haloY}}px"></span>'
    '<svg class="ring" width="{{s.svgW}}" height="{{s.svgH}}" style="left:{{s.svgX}}px;top:{{s.svgY}}px" aria-hidden="true">'
    '<ellipse cx="{{s.ex}}" cy="{{s.ey}}" rx="{{s.rx}}" ry="{{s.ry}}" fill="none" stroke="#00D4FF" stroke-width="3" '
    'style="filter:drop-shadow(0 0 6px rgba(0,212,255,.9))"></ellipse>'
    '<ellipse cx="{{s.ex}}" cy="{{s.ey}}" rx="{{s.rx2}}" ry="{{s.ry2}}" fill="none" stroke="rgba(0,212,255,.45)" stroke-width="1.5"></ellipse></svg>'
    '<span class="tag cap sh" style="top:-34px;font-size:16px;color:#E6E9FF">{{s.kind}}<span style="color:#8A8FAE;'
    'font-family:Aldrich,sans-serif;letter-spacing:0;text-transform:none;font-size:14px">{{s.sub}}</span></span>'
    '</button></sc-for>'
    '{{WORLD_EXTRA}}'
    '</div>'
)

REST = ('<sc-if value="{{isRest}}" hint-placeholder-val="{{ true }}"><span class="abs sh" style="left:0;right:0;top:860px;'
        'text-align:center;font-size:20px;color:#B8C0F0;animation:rise 420ms ease both;pointer-events:none;z-index:4">'
        'Touch an artifact to see what it does.</span>'
        '<span class="abs sh lab" style="left:0;right:0;top:896px;text-align:center;pointer-events:none;z-index:4">'
        '4 found - the dark alcoves hold the ones still to find</span></sc-if>')

GIVE = r105.GIVE
FEED_IT = '<button class="btn sl ghost" type="button" onClick="{{toFeed}}"><span class="kc">A</span>Feed it</button>'


def tracks(width):
    """The four tracks as rows: the attuned one live, the three rolled ones fixed."""
    return f"""
<div style="width:{width}px;display:flex;flex-direction:column;gap:12px">
  <span class="lab sh">Its four tracks</span>
  <sc-for list="{{{{trk}}}}" as="t" hint-placeholder-count="4"><div style="display:flex;flex-direction:column;gap:6px;opacity:{{{{t.op}}}}">
    <div style="display:flex;align-items:center;gap:10px"><img src="{{{{t.petal}}}}" alt="" style="width:22px;height:22px">
      <span class="sh" style="font-size:15px;color:#B8C0F0;flex:1">{{{{t.label}}}}</span>
      <span class="sh num" style="font-size:16px;color:{{{{t.col}}}}">{{{{t.val}}}}</span></div>
    <div style="position:relative;height:3px;background:rgba(116,123,173,.3)"><div class="abs" style="left:0;top:0;bottom:0;width:{{{{t.w}}}};background:{{{{t.col}}}};transition:width 700ms ease"></div></div>
  </div></sc-for>
</div>"""


# ---------------- A: lit where it stands ----------------
BODY_A = """
<sc-if value="{{showCur}}" hint-placeholder-val="{{ false }}">
  <svg class="abs" style="left:0;top:0;z-index:3;pointer-events:none" width="1920" height="1080" aria-hidden="true">
    <path d="{{lead.d}}" fill="none" stroke="rgba(0,212,255,.75)" stroke-width="1.5"></path>
    <circle cx="{{lead.x}}" cy="{{lead.y}}" r="4" fill="#00D4FF"></circle>
  </svg>
  <div class="abs" style="left:{{side.x}}px;top:220px;width:{{side.w}}px;display:flex;flex-direction:column;gap:24px;z-index:4">""" + r105.details(460) + """
    <sc-if value="{{isArt}}" hint-placeholder-val="{{ true }}"><div style="display:flex;gap:12px;flex-wrap:wrap;animation:rise 420ms ease both">""" + FEED_IT + GIVE + """</div>""" + r105.fixed_rows() + """</sc-if>
    <sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}"><div style="display:flex;flex-direction:column;gap:22px;animation:rise 420ms ease both">""" + r105.meter(460) + r105.crystals(40) + """</div></sc-if>
  </div>
</sc-if>
"""

# ---------------- B: step up to it ----------------
BODY_B = """
<div class="abs" style="inset:0;pointer-events:none;z-index:2;transition:opacity 700ms ease;opacity:{{vig}};background:linear-gradient(90deg,rgba(0,1,10,0) 55%,rgba(0,1,10,.86) 64%,rgba(0,1,10,.9) 100%),radial-gradient(ellipse 70% 80% at 32% 52%,rgba(0,1,10,0) 40%,rgba(0,1,10,.75) 100%)"></div>
<sc-if value="{{showCur}}" hint-placeholder-val="{{ false }}">
  <div class="abs" style="left:1240px;top:200px;width:600px;display:flex;flex-direction:column;gap:24px;z-index:4">""" + r105.details(600) + """
    <sc-if value="{{isArt}}" hint-placeholder-val="{{ true }}"><div style="display:flex;gap:12px;animation:rise 420ms ease both">""" + FEED_IT + GIVE + """</div>""" + tracks(520) + """</sc-if>
    <sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}"><div style="display:flex;flex-direction:column;gap:22px;animation:rise 420ms ease both">""" + r105.meter(520) + r105.crystals(42) + """</div></sc-if>
  </div>
  <div class="abs sh" style="left:80px;bottom:96px;display:flex;gap:28px;z-index:4">
    <button class="pick" type="button" onClick="{{prevArt}}" style="display:flex;gap:10px;align-items:center;color:#B8C0F0;font-size:16px"><span class="kc">&lt;</span>{{prevName}}</button>
    <button class="pick" type="button" onClick="{{nextArt}}" style="display:flex;gap:10px;align-items:center;color:#B8C0F0;font-size:16px">{{nextName}}<span class="kc">&gt;</span></button>
  </div>
</sc-if>
"""

# ---------------- C: written on the floor ----------------
BODY_C = """
<sc-if value="{{showCur}}" hint-placeholder-val="{{ false }}">
  <div class="abs" style="left:{{floor.x}}px;top:{{floor.top}}px;width:960px;height:520px;transform-origin:50% 100%;transform:perspective(1100px) rotateX(38deg);z-index:3;pointer-events:none;animation:rise 500ms ease both">
    <div class="abs" style="inset:0;border-radius:50%/42%;box-shadow:inset 0 0 0 3px rgba(0,212,255,.75),0 0 30px rgba(0,212,255,.35);background:radial-gradient(ellipse at 50% 50%,rgba(0,212,255,.10),rgba(0,4,31,.55) 70%)"></div>
    <div class="abs" style="left:110px;right:110px;top:40px;bottom:40px;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:16px;text-align:center">
      <span class="cap sh" style="font-size:24px;color:#8A8FAE">{{cur.kind}} - {{cur.serial}}</span>
      <span class="sh" style="font-size:52px;line-height:1.1;color:#E6E9FF">{{cur.name}}</span>
      <sc-if value="{{isArt}}" hint-placeholder-val="{{ true }}">
        <span class="sh" style="font-size:27px;line-height:1.35;color:#B8C0F0">{{cur.desc}}</span>
        <div style="display:flex;align-items:center;gap:14px"><img src="{{cur.petal}}" alt="" style="width:40px;height:40px"><span class="sh" style="font-size:28px;color:#E6E9FF">{{cur.el}} - {{cur.aspect}}</span><span class="sh num" style="font-size:28px;color:#00D4FF">{{cur.val}}</span></div>
        <span class="sh" style="font-size:22px;color:#8A8FAE">{{cur.held}}</span>
      </sc-if>
      <sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}">
        <span class="sh num" style="font-size:34px;color:#E6E9FF">Level {{cur.lvl}}<span style="color:#99FF80">{{cur.lvlNext}}</span></span>
        <div style="position:relative;width:520px;height:8px;background:rgba(116,123,173,.35)"><div class="abs" style="left:0;top:0;bottom:0;width:{{cur.nextW}};background:rgba(153,255,128,.5)"></div><div class="abs" style="left:0;top:0;bottom:0;width:{{cur.lvlW}};background:#00D4FF;transition:width 700ms ease"></div><div class="abs" style="left:50%;top:-8px;bottom:-8px;width:3px;background:{{cur.twistMark}}"></div></div>
        <span class="sh" style="font-size:22px;color:{{cur.twistCol}}">At 5: {{cur.twist}}</span>
        <span class="sh num" style="font-size:22px;color:#8A8FAE">{{cur.balLine}}</span>
      </sc-if>
    </div>
  </div>
  <div class="abs" style="left:0;right:0;top:1000px;display:flex;justify-content:center;gap:14px;z-index:5">
    <sc-if value="{{isArt}}" hint-placeholder-val="{{ true }}">""" + FEED_IT + GIVE + """</sc-if>
    <sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}"><button class="btn sl {{feedCls}}" type="button" onClick="{{feed}}"><span class="kc">A</span>Feed {{batch}} {{cur.el}}</button><button class="btn sl {{omniCls}}" type="button" onClick="{{feedOmni}}"><span class="kc">Y</span>Feed {{batch}} Omni</button></sc-if>
  </div>
  <sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}"><div class="abs" style="left:{{cur.ringX}}px;top:{{cur.ringY}}px;display:flex;gap:4px;z-index:4;pointer-events:none">
    <sc-for list="{{cry}}" as="k" hint-placeholder-count="10"><img src="{{k.src}}" alt="" style="width:22px;height:22px;object-fit:contain;opacity:{{k.op}};animation:{{k.anim}}"></sc-for></div></sc-if>
</sc-if>
"""

# ---------------- D: the column shows it ----------------
COLUMN = """
<div class="abs" style="left:{{colLeft}}px;top:158px;width:500px;height:330px;z-index:3;pointer-events:none;animation:lensOpen 620ms cubic-bezier(.22,.8,.25,1) both;transform-origin:50% 100%">
  <div class="abs" style="inset:0 0 0 0;border-radius:50%/34px;background:linear-gradient(90deg,rgba(0,212,255,.05),rgba(0,212,255,.16) 30%,rgba(230,233,255,.10) 50%,rgba(0,212,255,.16) 70%,rgba(0,212,255,.05))"></div>
  <div class="abs" style="left:0;right:0;top:0;height:68px;border-radius:50%;box-shadow:inset 0 0 0 2px rgba(0,212,255,.75)"></div>
  <div class="abs" style="left:0;right:0;bottom:0;height:68px;border-radius:50%;box-shadow:inset 0 0 0 2px rgba(0,212,255,.9),0 0 24px rgba(0,212,255,.45)"></div>
  <svg class="abs" style="left:30px;top:20px" width="440" height="300" viewBox="0 120 640 470" preserveAspectRatio="xMidYMid meet" aria-hidden="true">
    <sc-for list="{{holo.prisms}}" as="p" hint-placeholder-count="40"><rect x="{{p.x}}" y="{{p.y}}" width="22" height="12" fill="rgba(166,0,255,.35)" stroke="{{p.c}}" stroke-width="1.4" transform="rotate({{p.r}} {{p.cx}} {{p.cy}})"></rect></sc-for>
    <sc-for list="{{holo.paths}}" as="p" hint-placeholder-count="4"><path d="{{p.d}}" fill="none" stroke="#00D4FF" stroke-width="4" stroke-linecap="round" stroke-dasharray="300" style="animation:fly 2.4s ease-out {{p.delay}} infinite"></path></sc-for>
    <sc-for list="{{holo.bursts}}" as="b" hint-placeholder-count="4"><circle cx="{{b.x}}" cy="{{b.y}}" r="0" fill="rgba(0,212,255,.18)" stroke="#E6E9FF" stroke-width="2" style="animation:burst 2.4s ease-out {{b.delay}} infinite"></circle></sc-for>
    <path d="M296 600 L320 556 L344 600 Z" fill="rgba(230,233,255,.15)" stroke="#E6E9FF" stroke-width="2"></path>
  </svg>
</div>
<svg class="abs" style="left:0;top:0;z-index:3;pointer-events:none" width="1920" height="1080" aria-hidden="true">
  <path d="{{beam.d}}" fill="none" stroke="#00D4FF" stroke-width="2.5" stroke-dasharray="10 10" style="animation:beam 700ms linear infinite;filter:drop-shadow(0 0 5px rgba(0,212,255,.9))"></path>
</svg>"""

BODY_D = """
<sc-if value="{{showCur}}" hint-placeholder-val="{{ false }}">""" + COLUMN + """
  <div class="abs" style="left:80px;top:220px;width:420px;display:flex;flex-direction:column;gap:24px;z-index:4">""" + r105.details(420) + """
    <sc-if value="{{isArt}}" hint-placeholder-val="{{ true }}"><div style="display:flex;gap:12px;flex-wrap:wrap;animation:rise 420ms ease both">""" + FEED_IT + GIVE + """</div></sc-if>
    <sc-if value="{{isFeed}}" hint-placeholder-val="{{ false }}"><div style="display:flex;flex-direction:column;gap:22px;animation:rise 420ms ease both">""" + r105.meter(420) + r105.crystals(36) + """</div></sc-if>
  </div>
  <div class="abs" style="left:1400px;top:230px;z-index:4;animation:rise 420ms ease both">""" + tracks(440) + """</div>
</sc-if>
"""

LOGIC = r"""
const ARTS = __ARTS__;
const SPOTS = __SPOTS__;
const SLOTS = __SLOTS__;
const PET = __PET__;
const CRY = __CRY__;
const BATCH = 5;
const ELS = ['Mass', 'Space', 'Time', 'Charge'];
const OPTION = '__OPTION__';
const COLX = __COLX__;
const TABS = ['Converter', 'Squads', 'Pilots', 'Artifacts', 'Decor'];
const lvlOf = (a, inv) => a.base + (a.ceil - a.base) * inv / (inv + a.half);
const valOf = (t, L) => t[1] + (t[2] - t[1]) * L / 10;
const INTS = ['heads', 'shocks', 're-raises', 'cracks before it stops'];
const fmt = (t, v) => (INTS.indexOf(t[0]) >= 0 ? String(Math.round(v)) : t[0].indexOf('hull') >= 0 ? Math.round(v * 100) + '%' : (Math.abs(t[2] - t[1]) >= 50 ? String(Math.round(v)) : v.toFixed(1)));
const f1 = (x) => x.toFixed(1);
class Component extends DCLogic {
 constructor(props) { super(props); this.state = { lvl: 0, sel: 1, inv: ARTS.map((a) => a.inv),
   bal: { Mass: 10, Space: 10, Time: 10, Charge: 10, Omni: 10 }, fed: 0, toast: '', lastOmni: false }; this.t = null; }
 componentWillUnmount() { if (this.t) clearTimeout(this.t); }
 say(x) { if (this.t) clearTimeout(this.t); this.setState({ toast: x }); this.t = setTimeout(() => this.setState({ toast: '' }), 3200); }
 doFeed(omni) {
  const st = this.state, a = ARTS[st.sel], k = omni ? 'Omni' : a.el;
  if (st.bal[k] < BATCH) { this.say('Not enough ' + k + ' crystals: ' + st.bal[k] + ' held, a feed takes ' + BATCH + '. The Converter makes more.'); return; }
  const inv = st.inv.slice(); inv[st.sel] += BATCH; const bal = Object.assign({}, st.bal); bal[k] -= BATCH;
  this.setState({ inv, bal, fed: st.fed + 1, lastOmni: !!omni }); }
 renderVals() {
  const st = this.state, a = ARTS[st.sel], inv = st.inv[st.sel], sp = SPOTS[st.sel];
  const L = lvlOf(a, inv), Ln = lvlOf(a, inv + BATCH), t = a.trk[a.el];
  const open = st.lvl >= 1;
  const pick = (i) => () => { if (i === st.sel && open) return; this.setState({ sel: i, lvl: 1, toast: '' }); };
  // the world: B walks in to the picked pedestal; the others keep the room still
  const ZS = 1.9, TX = 560, TY = 600;
  const world = OPTION === 'B' && open ? { s: ZS, x: Math.round(TX - ZS * sp.hx), y: Math.round(TY - ZS * sp.hy) } : { s: 1, x: 0, y: 0 };
  const spots = ARTS.map((g, i) => { const s = SPOTS[i], on = open && i === st.sel;
   const bottom = s.py + s.ry + 24, h = bottom - s.top, left = s.cx - s.w / 2;
   const svgW = s.rx * 2 + 60, svgH = s.ry * 2 + 60;
   return { kind: g.kind, sub: g.holder ? (g.holder === 'You' ? '  yours' : '  ' + g.holder + "'s") : '  in the hoard',
    pick: pick(i), aria: g.kind + ': ' + g.name, pressed: on ? 'true' : 'false', cls: on ? 'on' : (open ? 'dim' : ''),
    left: Math.round(left), top: s.top, w: s.w, h: Math.round(h), haloY: s.hy - s.top,
    svgW, svgH, svgX: Math.round(s.px - left - svgW / 2), svgY: Math.round(s.py - s.top - svgH / 2), ex: svgW / 2, ey: svgH / 2,
    rx: s.rx, ry: s.ry, rx2: s.rx + 14, ry2: s.ry + 5 }; });
  const slots = SLOTS.map((q) => ({ x: q.x, y: q.y, pick: () => this.say('An empty alcove. It waits for an artifact you have still to find.') }));
  const spot = { x: sp.hx, y: sp.hy + 40, op: open && (OPTION === 'A' || OPTION === 'C' || OPTION === 'D') ? 1 : 0 };
  const held = a.holder ? (a.holder === 'You' ? 'Carried by you, in the ' + a.seat + ' seat' : 'Carried by ' + a.holder + ', in the ' + a.seat + ' seat') : 'In your hoard - no seat carries it';
  const fixed = ELS.filter((e) => e !== a.el).map((e) => ({ petal: PET[e], label: e + ' - ' + a.trk[e][0], val: fmt(a.trk[e], valOf(a.trk[e], a.fixed[e])) }));
  const cur = { kind: a.kind, serial: a.serial, name: a.name, desc: a.desc, lore: a.lore, el: a.el, petal: PET[a.el], aspect: t[0],
   val: fmt(t, valOf(t, L)), held, fixed, lvl: f1(L), lvlNext: st.lvl === 2 ? '  > ' + f1(Ln) : '', lvlW: (L * 10) + '%',
   nextW: st.lvl === 2 ? (Ln * 10) + '%' : '0%', ceilX: (a.ceil * 10) + '%', twist: t[3],
   twistCol: L >= 5 ? '#E6E9FF' : '#747BAD', twistMark: L >= 5 ? '#99FF80' : '#747BAD',
   valLine: 'Next feed: ' + fmt(t, valOf(t, L)) + ' > ' + fmt(t, valOf(t, Ln)) + ' ' + t[0] + '. ' + inv + ' crystals fed so far.',
   balLine: a.el + ' crystals ' + st.bal[a.el] + ' - Omni ' + st.bal.Omni + ' - a feed takes ' + BATCH,
   giveLabel: a.holder ? 'Give to another seat' : 'Give to a seat',
   ringX: Math.round(sp.px - 128), ringY: Math.round(sp.py + sp.ry + 8) };
  const trk = ELS.map((e) => { const lv = e === a.el ? L : a.fixed[e];
   return { petal: PET[e], label: e + ' - ' + a.trk[e][0], val: fmt(a.trk[e], valOf(a.trk[e], lv)), w: (lv * 10) + '%',
    col: e === a.el ? '#00D4FF' : 'rgba(230,233,255,.6)', op: e === a.el ? 1 : 0.7 }; });
  const cry = [];
  for (let i = 0; i < 10; i++) { const left = st.bal[a.el] > i;
   const justFed = st.fed > 0 && !st.lastOmni && i >= st.bal[a.el] && i < st.bal[a.el] + BATCH;
   const anim = justFed ? ((OPTION === 'C' ? 'up' : (st.fed % 2 ? 'feedA' : 'feedB')) + ' 700ms ease-in ' + ((i - st.bal[a.el]) * 70) + 'ms both') : 'none';
   cry.push({ src: CRY[a.el], op: left ? 1 : (justFed ? 1 : 0.12), anim }); }
  // A: words beside it, on the side with room; a leader from the artifact
  const right = st.sel <= 1;
  const side = right ? { x: 1340, w: 500 } : { x: 80, w: 420 };
  const lx = right ? 1320 : 520, ly = 300;
  const lead = { d: 'M' + sp.hx + ' ' + (sp.hy - 60) + ' L' + sp.hx + ' ' + ly + ' L' + lx + ' ' + ly, x: sp.hx, y: sp.hy - 60 };
  // C: the plate on the deck in front of its pedestal
  const floor = { x: Math.max(20, Math.min(940, sp.px - 480)), top: 500 };
  // D: the beam from the artifact up into the column; the column's hologram
  const beam = { d: 'M' + sp.hx + ' ' + (sp.hy - 40) + ' Q' + Math.round((sp.hx + COLX) / 2) + ' ' + (sp.hy - 40) + ' ' + COLX + ' 488' };
  const rnd = (n) => { const x = Math.sin(n * 91.7 + st.sel * 13.1) * 43758.5; return x - Math.floor(x); };
  const prisms = []; for (let i = 0; i < 40; i++) { const x = 60 + rnd(i) * 500, y = 150 + rnd(i + 40) * 400;
   prisms.push({ x: Math.round(x), y: Math.round(y), cx: Math.round(x + 11), cy: Math.round(y + 6), r: Math.round(rnd(i + 9) * 180), c: rnd(i + 3) > 0.5 ? 'rgba(166,0,255,.8)' : 'rgba(255,174,0,.7)' }); }
  const paths = [], bursts = [];
  if (a.kind === 'Hydra') { const n = Math.round(valOf(a.trk.Charge, L)); for (let i = 0; i < n; i++) { const ang = (-90 - 50 + 100 * i / Math.max(1, n - 1)) * Math.PI / 180;
    const ex = 320 + Math.cos(ang) * 330, ey = 560 + Math.sin(ang) * 330; paths.push({ d: 'M320 556 Q' + Math.round(320 + Math.cos(ang) * 120) + ' ' + Math.round(470 + Math.sin(ang) * 60) + ' ' + Math.round(ex) + ' ' + Math.round(ey), delay: (i * 0.08) + 's' });
    bursts.push({ x: Math.round(ex), y: Math.round(ey), delay: (i * 0.08) + 's' }); } }
  else if (a.kind === 'Javelin') { paths.push({ d: 'M320 556 L320 160', delay: '0s' }); [430, 330, 230].forEach((y, i) => bursts.push({ x: 320, y, delay: (0.2 * i) + 's' })); bursts.push({ x: 320, y: 160, delay: '0.6s' }); }
  else if (a.kind === 'Aftershock') { paths.push({ d: 'M320 556 Q320 380 320 300', delay: '0s' }); bursts.push({ x: 320, y: 300, delay: '0s' }); bursts.push({ x: 320, y: 300, delay: '0.9s' }); bursts.push({ x: 320, y: 300, delay: '1.4s' }); }
  else { paths.push({ d: 'M180 520 A160 160 0 0 1 460 520', delay: '0s' }); paths.push({ d: 'M150 540 A190 190 0 0 1 490 540', delay: '0.3s' }); bursts.push({ x: 320, y: 360, delay: '0.5s' }); }
  const holo = { prisms, paths, bursts };
  const tabs = TABS.map((n) => ({ name: n, col: n === 'Artifacts' ? '#E6E9FF' : (n === 'Decor' ? '#5C5F70' : '#747BAD'), mark: n === 'Artifacts' ? '#E6E9FF' : 'transparent',
   pick: () => this.say(n === 'Artifacts' ? '' : 'Only the Artifacts tab is drawn in this round.') }));
  const names = ['Hoard', a.kind, 'Feed'];
  const trail = names.slice(0, st.lvl + 1).map((n, i) => ({ name: n, sep: i ? '/  ' : '', col: i === st.lvl ? '#E6E9FF' : '#747BAD' }));
  const hints = st.lvl === 0 ? [{ k: 'D-pad', t: 'Move' }, { k: 'A', t: 'Look' }] : (st.lvl === 1 ? [{ k: 'D-pad', t: 'Next artifact' }, { k: 'A', t: 'Feed' }, { k: 'X', t: 'Give' }, { k: 'B', t: 'Back' }] : [{ k: 'A', t: 'Feed ' + a.el }, { k: 'Y', t: 'Feed Omni' }, { k: 'B', t: 'Back' }]);
  const step = (d) => () => this.setState({ sel: (st.sel + d + ARTS.length) % ARTS.length, lvl: Math.max(1, st.lvl), toast: '' });
  const n = ARTS.length;
  return { tabs, trail, spots, slots, spot, world, cur, trk, cry, holo, hints, side, lead, floor, beam, colLeft: COLX - 250,
   prevName: ARTS[(st.sel - 1 + n) % n].kind, nextName: ARTS[(st.sel + 1) % n].kind, vig: open ? 1 : 0,
   showBack: st.lvl > 0, isRest: st.lvl === 0, isArt: st.lvl === 1, isFeed: st.lvl === 2, showCur: open,
   dim: 0, batch: String(BATCH),
   feedCls: st.bal[a.el] >= BATCH ? 'cta' : 'off', omniCls: st.bal.Omni >= BATCH ? 'ghost' : 'off',
   toast: st.toast, hasToast: st.toast !== '', say: { tab: () => this.say('LB / RB change tab; only Artifacts is drawn here.') },
   toFeed: () => this.setState({ lvl: 2, toast: '' }), feed: () => this.doFeed(false), feedOmni: () => this.doFeed(true),
   back: () => this.setState({ lvl: Math.max(0, st.lvl - 1), toast: '' }),
   prevArt: step(-1), nextArt: step(1),
   give: () => this.say('The Squads tab opens and asks which seat carries the ' + a.kind + '.') };
 }
}
"""

OPTIONS = {
    "A": ("A - Lit where it stands: touch it, its pedestal lights, the words hang beside it", BODY_A, ""),
    "B": ("B - Step up to it: touch it and the view walks in to its pedestal", BODY_B, ""),
    "C": ("C - Written on the floor: the words lie on the deck in front of it", BODY_C, ""),
    "D": ("D - The column shows it: touch it and the light column plays what it does (recommended)", BODY_D, ""),
}


def arts():
    a = copy.deepcopy(r105.ARTS)
    for g, s in zip(a, SPOTS):
        g["ax"], g["ay"] = s["hx"], s["hy"]
    return a


def board(rnd, key, title, body, pet):
    data = {"__ARTS__": json.dumps(arts()), "__SPOTS__": json.dumps(SPOTS), "__SLOTS__": json.dumps(SLOTS),
            "__PET__": json.dumps(pet), "__CRY__": json.dumps(r105.CRYSTAL), "__OPTION__": key, "__COLX__": str(COLUMN_X)}
    logic = LOGIC
    for k, v in data.items():
        logic = logic.replace(k, v)
    root = ('<div style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00041F; '
            'color: #E6E9FF; font-family: \'Aldrich\', \'Chakra Petch\', sans-serif">')
    world = WORLD.replace("{{WORLD_EXTRA}}", "")
    return (f'<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<title>Rev {rnd} {key} - {title.split(" - ", 1)[1]} - interactive</title>\n'
            '<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>\n' + HELMET + "\n" + root + world + chrome() + r105.BACK
            + REST + body + r105.FOOT +
            '\n</div>\n</x-dc>\n<script type="text/x-dc" data-dc-script data-props=\'{"$preview":{"width":1920,"height":1080}}\'>\n'
            + logic + '</script>\n</body>\n</html>\n')


# ---------------- Compare: Hydra touched, four ways, as stills ----------------
def compare(rnd):
    S = 0.39  # 1920 -> 749
    hy = SPOTS[1]

    def ring(s, col="#00D4FF"):
        return (f'<ellipse cx="{s["px"]}" cy="{s["py"]}" rx="{s["rx"]}" ry="{s["ry"]}" fill="none" stroke="{col}" stroke-width="4" '
                'style="filter:drop-shadow(0 0 8px rgba(0,212,255,.9))"></ellipse>')

    def words(x, y, w, align="left", size=1.0):
        return (f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;text-align:{align};display:flex;flex-direction:column;gap:14px">'
                f'<span class="lab sh" style="font-size:{int(18*size)}px">Hydra - 9F3C-01D2</span>'
                f'<span class="sh" style="font-size:{int(44*size)}px;line-height:1.1">Weeping Hydra of the Lantern Reach</span>'
                f'<span class="sh" style="font-size:{int(24*size)}px;line-height:1.4;color:#B8C0F0">Splits on release into heads that '
                'fan out across your heading.</span></div>')

    spot = ('<div class="abs" style="inset:0;background:radial-gradient(ellipse 300px 360px at 770px 602px,rgba(0,1,10,0) 0%,'
            'rgba(0,1,10,0) 45%,rgba(0,1,10,.62) 100%)"></div>')
    bg = f'<img class="bg" src="{r105.BACKDROP}" alt="">'
    z = 1.9
    zx, zy = round(560 - z * hy["hx"]), round(600 - z * hy["hy"])
    scenes = {
        "A": ("Lit where it stands", "Its pedestal lights and the room dims round it; the words hang in the open air beside it.",
              bg + spot + f'<svg class="abs" style="left:0;top:0" width="1920" height="1080">{ring(hy)}'
              f'<path d="M770 502 L770 300 L1320 300" fill="none" stroke="rgba(0,212,255,.8)" stroke-width="3"></path></svg>'
              + words(1340, 220, 520)),
        "B": ("Step up to it", "The view walks in to its pedestal; the words and its four tracks stand beside it. Back walks out.",
              f'<div class="abs" style="left:0;top:0;width:1920px;height:1080px;transform-origin:0 0;transform:translate({zx}px,{zy}px) scale({z})">'
              + bg + f'<svg class="abs" style="left:0;top:0" width="1920" height="1080">{ring(hy)}</svg></div>'
              '<div class="abs" style="inset:0;background:linear-gradient(90deg,rgba(0,1,10,0) 55%,rgba(0,1,10,.86) 64%,rgba(0,1,10,.9) 100%),radial-gradient(ellipse 70% 80% at 32% 52%,rgba(0,1,10,0) 40%,rgba(0,1,10,.75) 100%)"></div>'
              + words(1240, 220, 600)),
        "C": ("Written on the floor", "The words lie on the deck in front of its pedestal, lit like the floor's own rings.",
              bg + spot + f'<svg class="abs" style="left:0;top:0" width="1920" height="1080">{ring(hy)}</svg>'
              f'<div class="abs" style="left:{max(20, hy["px"] - 480)}px;top:500px;width:960px;height:520px;transform-origin:50% 100%;'
              'transform:perspective(1100px) rotateX(38deg);border-radius:50%/42%;box-shadow:inset 0 0 0 4px rgba(0,212,255,.8);'
              'background:radial-gradient(ellipse,rgba(0,212,255,.12),rgba(0,4,31,.55) 70%)">'
              + words(130, 70, 700, "center", 1.25) + '</div>'),
        "D": ("The column shows it (recommended)", "A beam ties it to the column of light; the column's upper half plays what it does.",
              bg + spot + f'<svg class="abs" style="left:0;top:0" width="1920" height="1080">{ring(hy)}'
              f'<path d="M770 522 Q795 522 {COLUMN_X} 488" fill="none" stroke="#00D4FF" stroke-width="4" stroke-dasharray="12 10"></path></svg>'
              f'<div class="abs" style="left:{COLUMN_X - 250}px;top:158px;width:500px;height:330px;border-radius:50%/34px;'
              'background:linear-gradient(90deg,rgba(0,212,255,.06),rgba(0,212,255,.2) 30%,rgba(230,233,255,.12) 50%,rgba(0,212,255,.2) 70%,rgba(0,212,255,.06));'
              'box-shadow:inset 0 0 0 3px rgba(0,212,255,.75)">'
              '<svg class="abs" style="left:30px;top:20px" width="440" height="300" viewBox="0 120 640 470">'
              + "".join(f'<path d="M320 556 Q{320 + c * 120:.0f} {470 + s * 60:.0f} {320 + c * 330:.0f} {560 + s * 330:.0f}" fill="none" stroke="#00D4FF" stroke-width="5"></path>'
                        f'<circle cx="{320 + c * 330:.0f}" cy="{560 + s * 330:.0f}" r="20" fill="rgba(0,212,255,.2)" stroke="#E6E9FF" stroke-width="3"></circle>'
                        for c, s in [(-0.643, -0.766), (0, -1), (0.643, -0.766)]) + '</svg></div>'
              + words(80, 220, 440)),
    }
    cells = []
    for i, (key, (name, line, inner)) in enumerate(scenes.items()):
        x, y = 120 + (i % 2) * 900, 128 + (i // 2) * 474
        cells.append(
            f'<div class="abs" style="left:{x}px;top:{y}px;width:749px">'
            f'<div style="position:relative;width:749px;height:421px;overflow:hidden;box-shadow:0 0 0 1px #222645">'
            f'<div class="abs" style="left:0;top:0;width:1920px;height:1080px;transform-origin:0 0;transform:scale({S})">{inner}</div></div>'
            f'<div style="display:flex;flex-direction:column;gap:4px;margin-top:8px"><span class="cap" style="font-size:17px">{key} - {name}</span>'
            f'<span style="font-size:15px;color:#8A8FAE">{line}</span></div></div>')
    return (f'<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<title>Rev {rnd} - Compare: what happens when you '
            'touch Hydra, in each option</title>\n<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>\n' + HELMET +
            '\n<div style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00041F; color: '
            '#E6E9FF; font-family: \'Aldrich\', \'Chakra Petch\', sans-serif">'
            f'<span class="abs cap" style="left:80px;top:48px;font-size:28px">Rev {rnd} - touch Hydra in the room, four ways</span>'
            '<span class="abs" style="left:80px;top:90px;font-size:16px;color:#8A8FAE">In every option the four artifacts on '
            'their pedestals are the buttons; no rail, no cards. Stills of the moment after the touch.</span>'
            + "".join(cells) + '</div>\n</x-dc>\n'
            '<script type="text/x-dc" data-dc-script data-props=\'{"$preview":{"width":1920,"height":1080}}\'>\n'
            'class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n</script>\n</body>\n</html>\n')


NOTES = [
    ("Asked", "Shombith, 2026-11-24, on the built Rev 105 B: \"Artifacts again doesnt make sense I cannot click in the image "
              "itself that should be what I should be able to do. Rework on this and give me different design variations.\""),
    ("What goes", "The rail of round medallions along the floor and its \"?\" circles. Nothing on the hoard level but the "
                  "room and one hint line: the four artifacts on their pedestals are the buttons, and the dark prisms in the "
                  "alcoves are the slots still to find (touch one: \"It waits for an artifact you have still to find\")."),
    ("Touching one", "Hover or pad focus lights its pedestal ring and a halo round it, with its name above it. A touch opens "
                     "it. D-pad moves between the four, A opens, B steps back a level (section 205's back still holds). On a "
                     "phone there is no hover: the first touch opens it."),
    ("What stays", "Rev 105's three levels (the hoard, one artifact, feeding it), its words, the four tracks, the feed of 5 "
                   "with 10 crystals of each, Give to a seat, the trail in the tab row. Only where the words live changes."),
    ("A - Lit where it stands", "The quietest: the room stays still, dims round the picked one, the words hang on the side "
                                "with room, tied to it by a line. Shows nothing of what it does."),
    ("B - Step up to it", "The clearest on a phone: the picked one fills the left half. The clip is 1920 wide, so a 1.9x walk "
                          "in is soft: it needs a sharper clip per pedestal or the live room."),
    ("C - Written on the floor", "The most of the room: the words are light on the deck, like the floor rings already there. "
                                 "Long descriptions get hard to read at that angle; the buttons stay upright under it."),
    ("D - The column shows it (recommended)", "Keeps what Shombith picked in Rev 105 (B: a hologram that SHOWS what it does, "
                                              "Hydra's heads fanning out as many as its level gives) and puts it in the room: "
                                              "the column of light is the lens. The words stay left, the tracks right; the "
                                              "artifacts below stay touchable to switch."),
    ("For the build", "All four sit on the built backdrop clip (ArkBackdrop): each artifact is an invisible button over its "
                      "place in the clip, with a lit ring and halo image on top (the positions are fixed in the clip). B needs "
                      "the RawImage scaled and sharper clips. D's column is Rev 201's ArtifactLens re-shaped to a cylinder. "
                      "The rail (ArtifactMedallion) goes. No new data."),
]


def notes(rnd):
    rows = "".join(
        f'<div style="display:grid;grid-template-columns:330px 1fr;gap:28px;padding:13px 0;border-top:1px solid #222645">'
        f'<span class="cap" style="font-size:15px;color:#8A8FAE">{h}</span><span style="font-size:17px;line-height:1.45">'
        f'{r105.esc(b)}</span></div>' for h, b in NOTES)
    return (f'<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<title>Rev {rnd} - Notes: what changes, what '
            'stays, what each costs to build</title>\n<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>\n' + HELMET +
            '\n<div style="width: 1920px; height: 900px; position: relative; overflow: hidden; background: #00041F; color: '
            '#E6E9FF; font-family: \'Aldrich\', \'Chakra Petch\', sans-serif">'
            f'<span class="abs cap" style="left:80px;top:48px;font-size:28px">Rev {rnd} - touch the artifacts in the room</span>'
            f'<div class="abs" style="left:80px;top:100px;width:1760px">{rows}</div></div>\n</x-dc>\n'
            '<script type="text/x-dc" data-dc-script data-props=\'{"$preview":{"width":1920,"height":900}}\'>\n'
            'class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n</script>\n</body>\n</html>\n')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("root")
    ap.add_argument("--round", type=int, default=140)
    args = ap.parse_args()
    out = Path(args.root) / "project"
    out.mkdir(parents=True, exist_ok=True)
    pet = r105.petals()
    for key, (title, body, _) in OPTIONS.items():
        (out / f"R{args.round}{key}.dc.html").write_text(board(args.round, key, title, body, pet))
    (out / f"R{args.round}Compare.dc.html").write_text(compare(args.round))
    (out / f"R{args.round}Notes.dc.html").write_text(notes(args.round))
    print("wrote", ", ".join(f"R{args.round}{k}" for k in [*OPTIONS, "Compare", "Notes"]))


if __name__ == "__main__":
    main()
