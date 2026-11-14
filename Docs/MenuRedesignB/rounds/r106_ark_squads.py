#!/usr/bin/env python3
"""Rev 106 - the Ark's Squads tab in levels (Docs/MENU_REDESIGN_B.md, the Rev 106 section).

Shombith, 2026-11-13: the UI cards on top of each Ark screen go; the Squads screen is the worst ("selecting through so
many and the text is noisy, it can be broken down in levels"); the style follows the pilot scenes that show captains
inside the Ark (Jugger and Scales: a dark bridge lit by its display, the display a dome of light with depth, a brass
rail and warm practical lamps). The hangar backdrops stay (the Frogma renders, section 190).

Writes five boards into <root>/project/ (the round's scratch root from `frogma.py plan`):
    R106A        Levels: tags over the vessels, a seat opens, one choice at a time (recommended)
    R106B        The holo table: the squad on a bridge console, its seat projected up
    R106C        Seat by seat: one seat in type over the hangar, no panels
    R106Compare  one seat at rest and open, in the build and in A, B and C
    R106Notes    what was asked, the cues, the words on screen

    python3 Docs/MenuRedesignB/rounds/r106_ark_squads.py <root>
"""
import json
import os
import sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
OUT = os.path.join(ROOT, "project")

# The canvas's own art (reused ids, never re-uploaded).
NAV = {"CODEX": "ec690d6b90d6fcdbc430b9bbaacf28a9", "HANGAR": "34f5a50f1275fef42808fe9df3bf6af5",
       "HOME": "a5bd4d8d1de7b494567fc3de75525e1e", "ARK": "e5b050c5a676193c650e94d873ad40e2",
       "PORT": "d0e850bc927559f1a499868d26f2a3de"}
HANGAR = {"Jade": "/_blob/c2a9b2634cf0ccae357e84f5a3ff5917", "Ruby": "/_blob/cd2a783f5d168b8a448e948e3d67122a",
          "Gold": "/_blob/48075ef29368f88768c579631ceb3f3d"}           # Rev 67's Nocturne hangar, per domain
CLOSE = {"VOLT": "32a9eed26dda493c7334d13cddaf435e", "ZODIAC": "30c5723ab68af43282bfca424757ff31",
         "ANVIL": "f4c3e0f26d1f48e7d121156e212e4136", "TALON": "77e722ebf367c3b4093b2ae2778762e4",
         "HORNET": "5bb7e9dc77ccbb917e79991a86168b17", "NEEDLE": "17190fc5544dfc08266eab625c0aa6a2",
         "IVY": "d750680bcbd6bed6958c9fa98632d348", "OPAL": "02126cf45624881e04e68ab3443b261b"}  # Rev 99's close-ups
REF = {"Jugger": "/_blob/4f7de477b02276aa415c32781f1a821a", "Scales": "/_blob/d3c0dc1db64080be767b648fdd7596e2"}
DOMC = {"Jade": "#21C0BD", "Ruby": "#BD1BC8", "Gold": "#FF8F11"}

BOOK = [
    {"name": "Skim Wing", "dom": "Jade", "mine": True, "owner": "You", "seats": [
        {"who": "You", "v": "Sparrow", "e": "Charge", "p": "VOLT", "a": "Javelin", "on": True, "me": True},
        {"who": "Halcyon", "v": "Dolphin", "e": "Space", "p": "ZODIAC", "a": "Hydra", "on": True},
        {"who": "Mothwing", "v": "Serpent", "e": "Mass", "p": "ANVIL", "a": "Aegis", "on": False}]},
    {"name": "Breakers", "dom": "Ruby", "mine": False, "owner": "Kestrel", "seats": [
        {"who": "Kestrel", "v": "Rhino", "e": "Mass", "p": "TALON", "a": "Aftershock", "on": True},
        {"who": "You", "v": "Manta", "e": "Charge", "p": "HORNET", "a": "Javelin", "on": True, "me": True},
        {"who": "Tern", "v": "Serpent", "e": "Space", "p": "NEEDLE", "a": "Hydra", "on": False}]},
]
CHOICES = {"v": ["Sparrow", "Dolphin", "Serpent", "Manta", "Rhino", "Squirrel", "Urchin"],
           "e": ["Charge", "Mass", "Space", "Time"],
           "p": ["VOLT", "ZODIAC", "ANVIL", "TALON", "HORNET", "NEEDLE", "IVY", "OPAL"],
           "a": ["Javelin", "Hydra", "Aftershock", "Aegis", "None"]}
# Where each seat's vessel stands in the hangar render (1920 x 1080), seat 0 the centre pad (Rev 67's line-up).
PADS = [{"x": 690, "y": 545}, {"x": 250, "y": 535}, {"x": 1120, "y": 470}]

# Icons: inline stroke SVG, 20 px, currentColor.
ICON = {
    "v": '<path d="M10 2l7 14-7-4-7 4z"></path>',
    "e": '<path d="M10 2l7 8-7 8-7-8z"></path><path d="M10 6v8"></path>',
    "p": '<circle cx="10" cy="7" r="4"></circle><path d="M3 18c1-4 4-6 7-6s6 2 7 6"></path>',
    "a": '<path d="M10 2l7 4v8l-7 4-7-4V6z"></path><path d="M10 6v8M6 8l8 4"></path>',
}


def svg(key, size=20, extra=""):
    return (f'<svg width="{size}" height="{size}" viewBox="0 0 20 20" fill="none" stroke="currentColor" '
            f'stroke-width="1.6" aria-hidden="true"{extra}>{ICON[key]}</svg>')


LOCK = ('<svg width="12" height="14" viewBox="0 0 14 16" fill="none" stroke="currentColor" stroke-width="1.6" '
        'aria-hidden="true"><path d="M3 7V5a4 4 0 018 0v2"></path><path d="M1.5 7h11v8h-11z"></path></svg>')
CHEV = ('<svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.6" '
        'aria-hidden="true"><path d="M4 2l4 4-4 4"></path></svg>')
CHEV_L = ('<svg width="14" height="14" viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.8" '
          'aria-hidden="true"><path d="M8 2L4 6l4 4"></path></svg>')
CHEV_R = ('<svg width="14" height="14" viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.8" '
          'aria-hidden="true"><path d="M4 2l4 4-4 4"></path></svg>')
DOWN = ('<svg width="12" height="12" viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="1.6" '
        'aria-hidden="true"><path d="M2 4l4 4 4-4"></path></svg>')
PLUS = ('<svg width="14" height="14" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.8" '
        'aria-hidden="true"><path d="M7 1v12M1 7h12"></path></svg>')

HELMET = """<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:#00010A}
.lab{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.16em;text-transform:uppercase;color:#8C93C7;text-shadow:0 0 8px rgba(0,1,10,.95),0 1px 2px rgba(0,1,10,.95)}
.cap{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}
.num{font-variant-numeric:tabular-nums}
.abs{position:absolute}
.sl{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}
.sls{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}
.kc{display:inline-flex;align-items:center;justify-content:center;min-width:26px;height:22px;padding:0 6px;box-sizing:border-box;border:1px solid #434C89;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:12px;color:#C9CDEA}
.btn{box-sizing:border-box;display:flex;align-items:center;justify-content:center;gap:10px;height:48px;padding:0 20px;white-space:nowrap;border:0;cursor:pointer;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:18px;letter-spacing:.1em;text-transform:uppercase}
.btn.sm{height:40px;padding:0 14px;font-size:14px}
.commit{background:#A600FF;color:#E6E9FF}
.cta{background:#99FF80;color:#00010A}
.def{background:rgba(0,4,31,.55);color:#E6E9FF;box-shadow:inset 0 0 0 1px #747BAD}
.off{background:rgba(0,4,31,.4);color:#5C5F70;box-shadow:inset 0 0 0 1px #222645}
.btn .kc{border-color:currentColor;color:inherit;opacity:.85}
.btn:focus-visible,.pick:focus-visible{outline:2px solid #00D4FF;outline-offset:2px}
.pick{background:none;border:0;padding:0;cursor:pointer;color:inherit;font:inherit;text-align:left}
.bg{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;transition:opacity 900ms ease-in-out}
.ell{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.lat{background-image:repeating-linear-gradient(60deg,rgba(230,233,255,.05) 0 1px,transparent 1px 24px),repeating-linear-gradient(-60deg,rgba(230,233,255,.05) 0 1px,transparent 1px 24px)}
.ava{object-fit:cover;display:block}
.rise{animation:csRise 360ms cubic-bezier(.2,.8,.2,1) both}
@keyframes csRise{from{opacity:0;transform:translateY(14px)}to{opacity:1;transform:translateY(0)}}
.fade{animation:csFade 300ms ease-out both}
@keyframes csFade{from{opacity:0}to{opacity:1}}
</style>
</helmet>"""


def nav():
    items = []
    for name in ("CODEX", "HANGAR", "HOME", "ARK", "PORT"):
        on = name == "ARK"
        mark = '<span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:#E6E9FF"></span>' if on else ""
        items.append(f'<span style="position:relative;display:flex;align-items:center;gap:10px;font-family:\'Chakra Petch\';'
                     f'font-weight:600;font-size:17px;letter-spacing:.12em;color:{"#E6E9FF" if on else "#747BAD"}">'
                     f'<img src="/_blob/{NAV[name]}" alt="" style="width:26px;height:26px;object-fit:contain;opacity:{1 if on else 0.7}">'
                     f'{name}{mark}</span>')
    return ('<nav aria-label="Menu screens" class="abs" style="left:0;right:0;top:0;height:84px;background:rgba(0,1,10,.78);'
            'border-bottom:1px solid #222645;display:flex;align-items:center;justify-content:center;gap:48px;z-index:5">'
            '<span class="kc">LT</span>' + "".join(items) + '<span class="kc">RT</span></nav>'
            '<div class="abs num" style="right:40px;top:30px;z-index:6;display:flex;align-items:center;gap:8px;font-size:20px;color:#FFAE00">'
            '<span style="width:14px;height:18px;background:#FFAE00;clip-path:polygon(50% 0,100% 35%,50% 100%,0 35%)"></span>1,240</div>')


TABROW = ('<div class="abs" style="left:40px;right:40px;top:96px;height:52px;display:flex;align-items:center;gap:30px;'
          'border-bottom:1px solid rgba(116,123,173,.25);z-index:4"><span class="kc">LB</span>'
          '<sc-for list="{{tabs}}" as="t" hint-placeholder-count="5"><button class="pick" type="button" onClick="{{t.pick}}" '
          'style="position:relative;display:flex;align-items:center;gap:8px;color:{{t.col}}"><sc-if value="{{t.locked}}" '
          'hint-placeholder-val="{{ false }}">' + LOCK + '</sc-if><span class="cap" style="font-size:16px;color:{{t.col}}">{{t.name}}</span>'
          '<span class="abs" style="left:0;right:0;bottom:-15px;height:2px;background:{{t.mark}}"></span></button></sc-for>'
          '<span class="kc">RB</span></div>')

BACKDROP = ('<img class="bg" src="{{bg}}" alt="{{bgAlt}}">'
            '<div class="abs" style="inset:0;background:linear-gradient(180deg,rgba(0,1,10,.72) 0%,rgba(0,1,10,0) 20%,'
            'rgba(0,1,10,0) 72%,rgba(0,1,10,.78) 100%);pointer-events:none"></div>')

TOAST = ('<sc-if value="{{hasToast}}" hint-placeholder-val="{{ false }}"><div class="abs fade" role="status" '
         'style="left:50%;top:170px;transform:translateX(-50%);z-index:9;padding:10px 18px;background:rgba(0,1,10,.88);'
         'box-shadow:inset 0 0 0 1px {{lightEdge}};font-size:15px;white-space:nowrap">{{toast}}</div></sc-if>')

FOOT = ('<div class="abs" style="right:40px;bottom:36px;display:flex;align-items:center;gap:10px;z-index:6">'
        '<button class="btn off sl" type="button" onClick="{{mission}}" aria-label="Mission, locked">' + LOCK +
        '<span>Mission</span></button><button class="btn commit sl" type="button" onClick="{{arena}}">'
        '<span class="kc">A</span><span>Arena</span></button></div>')

# The squad list (Y): squads, the domain (leader only), make, join. Shared by A and C.
SQUAD_LIST = ('<sc-if value="{{listOpen}}" hint-placeholder-val="{{ false }}"><div class="abs rise lat sl" '
              'style="left:40px;top:220px;width:360px;box-sizing:border-box;padding:16px 18px;z-index:8;'
              'background-color:rgba(0,1,10,.9);box-shadow:inset 0 0 0 1px {{lightEdge}};display:flex;flex-direction:column;gap:6px">'
              '<sc-for list="{{squadRows}}" as="s" hint-placeholder-count="2"><button class="pick" type="button" onClick="{{s.pick}}" '
              'style="position:relative;display:flex;align-items:center;gap:12px;height:46px;padding:0 12px;'
              'background:{{s.fill}}"><span class="abs" style="left:0;top:8px;bottom:8px;width:2px;background:{{s.mark}}"></span>'
              '<span style="width:10px;height:10px;transform:rotate(45deg);background:{{s.dot}}"></span>'
              '<span style="flex:1;font-size:18px">{{s.name}}</span><span class="lab" style="font-size:11px">{{s.whose}}</span></button></sc-for>'
              '<sc-if value="{{isMine}}" hint-placeholder-val="{{ true }}"><div style="display:flex;align-items:center;gap:14px;'
              'padding:10px 12px 4px;border-top:1px solid rgba(116,123,173,.25);margin-top:4px"><span class="lab" style="font-size:11px">Light</span>'
              '<sc-for list="{{domains}}" as="d" hint-placeholder-count="3"><button class="pick" type="button" aria-pressed="{{d.pressed}}" '
              'onClick="{{d.pick}}" style="display:flex;align-items:center;gap:6px;padding:4px 0;border-bottom:2px solid {{d.line}}">'
              '<span style="width:9px;height:9px;transform:rotate(45deg);background:{{d.dot}}"></span><span class="cap" '
              'style="font-size:13px;color:{{d.col}}">{{d.name}}</span></button></sc-for></div></sc-if>'
              '<div style="display:flex;gap:8px;padding-top:8px;border-top:1px solid rgba(116,123,173,.25);margin-top:6px">'
              '<button class="btn sm def sls" type="button" onClick="{{make}}">' + PLUS + '<span>Make</span></button>'
              '<button class="btn sm def sls" type="button" onClick="{{join}}"><span>Join a friend</span></button></div></div></sc-if>')

SQUAD_SWITCH = ('<button class="pick abs" type="button" onClick="{{toggleList}}" aria-expanded="{{listAria}}" '
                'style="left:40px;top:166px;display:flex;align-items:center;gap:12px;z-index:7">'
                '<span style="width:12px;height:12px;transform:rotate(45deg);background:{{dc}}"></span>'
                '<span style="font-size:30px;line-height:1;text-shadow:0 2px 10px rgba(0,1,10,.9)">{{squadName}}</span>'
                '<span style="color:#9AA0CC;display:flex">' + DOWN + '</span><span class="kc">Y</span></button>')

# --------------------------------------------------------------------------------------------------------------------
# The logic shared by the three options: the squad book, the open seats, the picks, the toast.
LOGIC_BASE = """
const BOOK = %(book)s;
const CHOICES = %(choices)s;
const CLOSE = %(close)s;
const HANGAR = %(hangar)s;
const DOMC = %(domc)s;
const PADS = %(pads)s;
const TABS = ["Converter", "Squads", "Pilots", "Artifacts", "Decor"];
const KEYS = [["v", "Vessel"], ["e", "Element"], ["p", "Pilot"], ["a", "Artifact"]];
function hexA(h, a) { const n = parseInt(h.slice(1), 16); return 'rgba(' + (n >> 16) + ',' + ((n >> 8) & 255) + ',' + (n & 255) + ',' + a + ')'; }

class Component extends DCLogic {
 constructor(props) { super(props); this.state = { sq: 0, doms: BOOK.map((b) => b.dom), seat: %(seat0)s, picking: '', list: false,
   edits: {}, opened: {}, toast: '' }; this.t = null; }
 componentWillUnmount() { if (this.t) clearTimeout(this.t); }
 say(x) { if (this.t) clearTimeout(this.t); this.setState({ toast: x }); this.t = setTimeout(() => this.setState({ toast: '' }), 2600); }
 seatData(i) {
  const st = this.state, q = BOOK[st.sq], s = q.seats[i], k = st.sq + ':' + i, e = st.edits[k] || {};
  const open = !!st.opened[k];
  return { i, who: open ? 'Open seat' : s.who, on: s.on, me: !!s.me, open, mine: q.mine,
   v: e.v || s.v, e: e.e || s.e, p: e.p || s.p, a: e.a || s.a,
   state: open ? 'Open' : (s.on ? 'Online' : 'Offline'),
   avatar: '/_blob/' + CLOSE[e.p || s.p] };
 }
 canEdit(d) { const q = BOOK[this.state.sq]; return q.mine || d.me; }
 choose(i, key, val) { const k = this.state.sq + ':' + i, e = Object.assign({}, this.state.edits);
  e[k] = Object.assign({}, e[k] || {}); e[k][key] = val; this.setState({ edits: e, picking: '' }); }
 openSeat(i) { const k = this.state.sq + ':' + i, o = Object.assign({}, this.state.opened); o[k] = true; this.setState({ opened: o, picking: '' });
  this.say(BOOK[this.state.sq].seats[i].who + ' is offline. The seat is open; the pad stands empty until someone takes it.'); }
 common() {
  const st = this.state, q = BOOK[st.sq], dom = st.doms[st.sq], dc = DOMC[dom];
  const tabs = TABS.map((x, i) => ({ name: x, col: i === 1 ? '#E6E9FF' : (x === 'Decor' ? '#5C5F70' : '#747BAD'),
   mark: i === 1 ? '#E6E9FF' : 'transparent', locked: x === 'Decor', pick: () => this.say(i === 1 ? 'You are on Squads.' : x + ' is another thread of this round.') }));
  const squadRows = BOOK.map((b, i) => ({ name: b.name, dot: DOMC[st.doms[i]], whose: b.mine ? 'Yours' : b.owner + "'s",
   fill: i === st.sq ? hexA(DOMC[st.doms[i]], 0.14) : 'transparent', mark: i === st.sq ? DOMC[st.doms[i]] : 'transparent',
   pick: () => this.setState({ sq: i, list: false, picking: '', seat: %(seat0)s }) }));
  const domains = ['Jade', 'Ruby', 'Gold'].map((k) => ({ name: k, dot: DOMC[k], line: k === dom ? DOMC[k] : 'transparent',
   col: k === dom ? '#E6E9FF' : '#9AA0CC', pressed: k === dom ? 'true' : 'false',
   pick: () => { const d = st.doms.slice(); d[st.sq] = k; this.setState({ doms: d }); } }));
  return { tabs, dc, dom, squadName: q.name, isMine: q.mine, bg: HANGAR[dom], bgAlt: 'Inside the Ark: the squad lined up in the hangar, lit ' + dom,
   lightEdge: hexA(dc, 0.6), lightFill: hexA(dc, 0.10), lightGlow: hexA(dc, 0.35), lightLine: hexA(dc, 0.85),
   toast: st.toast, hasToast: st.toast !== '', listOpen: st.list, listAria: st.list ? 'true' : 'false', squadRows, domains,
   toggleList: () => this.setState({ list: !st.list }),
   make: () => this.say('A new squad: you and two empty pads.'), join: () => this.say("Your friends' open seats."),
   mission: () => this.say('Missions open later.'), arena: () => this.say('Taking ' + q.name + ' into the Arena.') };
 }
"""


def logic(cls_body, seat0="-1"):
    base = LOGIC_BASE % dict(book=json.dumps(BOOK), choices=json.dumps(CHOICES), close=json.dumps(CLOSE),
                             hangar=json.dumps(HANGAR), domc=json.dumps(DOMC), pads=json.dumps(PADS), seat0=seat0)
    return base + cls_body


def board(title, body, script, w=1920, h=1080):
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{title}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
{HELMET}
<div style="width: {w}px; height: {h}px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
{body}
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
{script}
</script>
</body>
</html>
"""


# ===================================================================================================== A - levels
A_BODY = (BACKDROP + nav() + TABROW + SQUAD_SWITCH +
          # Level 1: a tag of light over each vessel, its line down to the pad.
          '<sc-for list="{{tags}}" as="t" hint-placeholder-count="3"><div class="abs" style="left:{{t.x}}px;top:{{t.y}}px;'
          'transform:translate(-50%,0);display:flex;flex-direction:column;align-items:center;opacity:{{t.op}};z-index:3">'
          '<button class="pick sls" type="button" onClick="{{t.pick}}" aria-pressed="{{t.pressed}}" aria-label="{{t.aria}}" '
          'style="display:flex;align-items:center;gap:10px;padding:6px 14px 6px 6px;background:{{t.fill}};'
          'box-shadow:inset 0 0 0 1px {{t.edge}}"><img class="ava sls" src="{{t.avatar}}" alt="" style="width:40px;height:40px;'
          'filter:{{t.filter}}"><span style="display:flex;flex-direction:column;gap:2px"><span style="font-size:18px;'
          'white-space:nowrap">{{t.who}}</span><span style="display:flex;align-items:center;gap:6px"><span style="width:6px;'
          'height:6px;transform:rotate(45deg);background:{{t.dot}}"></span><span class="lab" style="font-size:10px">{{t.state}}'
          '</span></span></span></button><span style="width:1px;height:{{t.lineH}}px;background:linear-gradient(180deg,{{t.line}},'
          'rgba(0,0,0,0))"></span><span style="width:7px;height:7px;margin-top:-3px;transform:rotate(45deg);'
          'background:{{t.ring}}"></span></div></sc-for>'
          # Level 2: the seat, opened on the right, as light.
          '<sc-if value="{{seatOpen}}" hint-placeholder-val="{{ true }}"><aside aria-label="Seat" class="abs rise lat sl" '
          'style="right:40px;top:176px;width:470px;box-sizing:border-box;padding:22px 24px;z-index:4;'
          'background-color:rgba(0,1,10,.72);box-shadow:inset 0 0 0 1px {{lightEdge}},inset 0 -120px 140px -80px {{lightGlow}};'
          'display:flex;flex-direction:column;gap:16px">'
          '<div style="display:flex;gap:16px;align-items:center"><img class="ava sl" src="{{s.avatar}}" alt="{{s.p}}" '
          'style="width:96px;height:96px"><span style="display:flex;flex-direction:column;gap:6px;flex:1;min-width:0">'
          '<span class="ell" style="font-size:34px;line-height:1">{{s.who}}</span><span style="display:flex;align-items:center;gap:8px">'
          '<span style="width:7px;height:7px;transform:rotate(45deg);background:{{sDot}}"></span><span class="lab" '
          'style="font-size:12px">{{s.state}}</span></span></span><button class="pick" type="button" onClick="{{close}}" '
          'aria-label="Close the seat"><span class="kc">B</span></button></div>'
          # the four tiles, or the one choice being made
          '<sc-if value="{{notPicking}}" hint-placeholder-val="{{ true }}"><div style="display:flex;flex-direction:column;gap:8px">'
          '<sc-for list="{{tiles}}" as="k" hint-placeholder-count="4"><button class="pick sls" type="button" onClick="{{k.pick}}" '
          'style="display:flex;align-items:center;gap:16px;height:68px;padding:0 18px;background:rgba(0,4,31,.5);'
          'box-shadow:inset 0 0 0 1px rgba(116,123,173,.35)"><span style="color:{{lightLine}};display:flex">{{k.icon}}</span>'
          '<span class="lab" style="font-size:11px;width:84px">{{k.label}}</span><span style="flex:1;font-size:22px">{{k.value}}</span>'
          '<span style="color:#747BAD;display:flex;opacity:{{k.chev}}">' + CHEV + '</span></button></sc-for></div></sc-if>'
          '<sc-if value="{{isPicking}}" hint-placeholder-val="{{ false }}"><div class="fade" style="display:flex;flex-direction:column;gap:4px">'
          '<span class="lab" style="padding-bottom:6px">{{pickTitle}}</span><sc-for list="{{choices}}" as="c" hint-placeholder-count="5">'
          '<button class="pick" type="button" onClick="{{c.pick}}" style="position:relative;display:flex;align-items:center;'
          'height:46px;padding:0 18px;background:{{c.fill}}"><span class="abs" style="left:0;top:9px;bottom:9px;width:2px;'
          'background:{{c.mark}}"></span><span style="font-size:19px;color:{{c.col}}">{{c.name}}</span></button></sc-for></div></sc-if>'
          '<sc-if value="{{hasAction}}" hint-placeholder-val="{{ true }}"><button class="{{actionClass}}" type="button" '
          'onClick="{{action}}"><span class="kc">X</span><span>{{actionLabel}}</span></button></sc-if></aside></sc-if>'
          + SQUAD_LIST + TOAST + FOOT +
          '<div class="abs" style="left:40px;bottom:44px;display:flex;align-items:center;gap:18px;z-index:5">'
          '<span style="display:flex;align-items:center;gap:8px"><span class="kc">A</span><span class="lab" style="font-size:11px">{{hintA}}</span></span>'
          '<sc-if value="{{seatOpen}}" hint-placeholder-val="{{ true }}"><span style="display:flex;align-items:center;gap:8px">'
          '<span class="kc">B</span><span class="lab" style="font-size:11px">Back</span></span></sc-if></div>')

TAG_JS = """
 tagsFor(c) {
  const st = this.state, q = BOOK[st.sq];
  return q.seats.map((_, i) => { const d = this.seatData(i), on = i === st.seat, live = d.on && !d.open, pad = PADS[i];
   return { x: pad.x, y: pad.y - 170, lineH: 120, who: d.who, state: d.state, avatar: d.avatar, aria: 'Seat: ' + d.who,
    op: st.seat >= 0 && !on ? 0.55 : 1, pressed: on ? 'true' : 'false',
    fill: on ? hexA(c.dc, 0.22) : 'rgba(0,1,10,.62)', edge: on ? c.dc : (live ? hexA(c.dc, 0.5) : 'rgba(116,123,173,.35)'),
    dot: d.open ? '#5C5F70' : (live ? c.dc : '#5C5F70'), line: live ? hexA(c.dc, 0.8) : 'rgba(116,123,173,.4)',
    ring: live ? hexA(c.dc, 0.6) : 'rgba(116,123,173,.25)', filter: live ? 'none' : 'grayscale(1) brightness(.6)',
    pick: () => this.setState({ seat: on ? -1 : i, picking: '', list: false }) }; });
 }
"""

A_JS = logic(TAG_JS + """
 renderVals() {
  const st = this.state, c = this.common(), open = st.seat >= 0, d = open ? this.seatData(st.seat) : this.seatData(0);
  const ICON = %(icons)s;
  const tiles = KEYS.map((k) => ({ label: k[1], value: d.open ? '-' : d[k[0]], icon: ICON[k[0]], chev: this.canEdit(d) && !d.open ? 1 : 0,
   pick: () => { if (d.open) return; if (!this.canEdit(d)) { this.say('Only ' + BOOK[st.sq].owner + ' changes this seat.'); return; }
    this.setState({ picking: k[0] }); } }));
  const pk = st.picking, cur = pk ? d[pk] : '';
  const choices = pk ? CHOICES[pk].map((n) => ({ name: n, col: n === cur ? '#E6E9FF' : '#9AA0CC', mark: n === cur ? c.dc : 'transparent',
   fill: n === cur ? hexA(c.dc, 0.12) : 'transparent', pick: () => this.choose(st.seat, pk, n) })) : [];
  const offlineFriend = !d.on && !d.me && !d.open && d.mine;
  return Object.assign(c, { tags: this.tagsFor(c), seatOpen: open, s: d, sDot: d.open ? '#5C5F70' : (d.on ? c.dc : '#5C5F70'),
   tiles, isPicking: !!pk, notPicking: !pk, pickTitle: pk ? 'Pick a ' + KEYS.find((k) => k[0] === pk)[1].toLowerCase() : '', choices,
   hasAction: (d.me || offlineFriend) && !pk, actionLabel: d.me ? 'Change vessel' : 'Open the seat', actionClass: d.me ? 'btn sm def sls' : 'btn sm cta sls',
   action: () => (d.me ? this.setState({ picking: 'v' }) : this.openSeat(st.seat)),
   close: () => this.setState(pk ? { picking: '' } : { seat: -1 }), hintA: open ? 'Pick' : 'Open a seat' });
 }
}
""" % dict(icons=json.dumps({k: svg(k) for k in ICON})))

# The icons go through a hole as markup? No: holes are text. Draw the icons with sc-if per key instead.
ICON_SWITCH = "".join(f'<sc-if value="{{{{k.is_{k}}}}}" hint-placeholder-val="{{{{ false }}}}">{svg(k)}</sc-if>' for k in ICON)
A_BODY = A_BODY.replace("{{k.icon}}", ICON_SWITCH)
A_JS = A_JS.replace("icon: ICON[k[0]],", "is_v: k[0] === 'v', is_e: k[0] === 'e', is_p: k[0] === 'p', is_a: k[0] === 'a',")
A_JS = A_JS.replace("  const ICON = %s;\n" % json.dumps({k: svg(k) for k in ICON}), "")

# ===================================================================================================== B - holo table
B_BODY = (BACKDROP + nav() + TABROW +
          # the bridge: warm lamps at the edges, the console's front band, the dome of light on it
          '<div class="abs" style="left:-140px;bottom:-60px;width:420px;height:300px;border-radius:50%;'
          'background:radial-gradient(closest-side,rgba(255,174,0,.22),rgba(255,174,0,0));pointer-events:none"></div>'
          '<div class="abs" style="right:-140px;bottom:-60px;width:420px;height:300px;border-radius:50%;'
          'background:radial-gradient(closest-side,rgba(255,174,0,.22),rgba(255,174,0,0));pointer-events:none"></div>'
          '<div class="abs lat" style="left:330px;top:676px;width:1260px;height:520px;border-radius:50%;'
          'background-color:{{domeFill}};box-shadow:inset 0 30px 80px -20px {{lightGlow}},0 -10px 80px -30px {{lightGlow}};'
          'border:1px solid {{lightEdge}};z-index:2"></div>'
          '<div class="abs" style="left:0;right:0;top:944px;height:136px;z-index:3;background:linear-gradient(180deg,'
          '#1B1710 0,#0B0A08 40%,#05060A 100%);border-top:3px solid #8A6A3A;box-shadow:0 -1px 0 0 #D2A25A,0 -18px 40px -10px rgba(255,174,0,.18)"></div>'
          # the three seats standing on the dome, left to right as their pads are
          '<sc-for list="{{plates}}" as="t" hint-placeholder-count="3"><button class="pick abs sls lat" type="button" onClick="{{t.pick}}" '
          'aria-pressed="{{t.pressed}}" aria-label="{{t.aria}}" style="left:{{t.x}}px;top:{{t.y}}px;width:300px;box-sizing:border-box;'
          'padding:12px 14px;z-index:4;display:flex;align-items:center;gap:12px;background-color:{{t.fill}};box-shadow:inset 0 0 0 1px {{t.edge}};'
          'transform:perspective(900px) rotateX(16deg);transition:top 300ms ease-out;opacity:{{t.op}}">'
          '<img class="ava sls" src="{{t.avatar}}" alt="" style="width:56px;height:56px;filter:{{t.filter}}">'
          '<span style="display:flex;flex-direction:column;gap:4px;min-width:0"><span class="ell" style="font-size:22px">{{t.who}}</span>'
          '<span class="ell" style="font-size:14px;color:#9AA0CC">{{t.line}}</span></span>'
          '<span class="abs" style="right:12px;top:12px;width:7px;height:7px;transform:rotate(45deg);background:{{t.dot}}"></span></button></sc-for>'
          # the picked seat projected up off its plate
          '<sc-if value="{{seatOpen}}" hint-placeholder-val="{{ true }}"><div class="abs" style="left:{{beamX}}px;top:{{beamTop}}px;width:300px;'
          'height:{{beamH}}px;z-index:3;pointer-events:none;background:linear-gradient(0deg,{{lightGlow}},rgba(0,0,0,0));'
          'clip-path:polygon(0 100%,100% 100%,90% 0,10% 0);opacity:.6"></div>'
          '<aside aria-label="Seat" class="abs rise lat sl" style="left:{{projX}}px;top:{{projY}}px;width:420px;box-sizing:border-box;'
          'padding:16px 18px;z-index:5;background-color:rgba(0,1,10,.55);box-shadow:inset 0 0 0 1px {{lightEdge}},'
          'inset 0 -80px 120px -60px {{lightGlow}};display:flex;flex-direction:column;gap:4px">'
          '<sc-if value="{{notPicking}}" hint-placeholder-val="{{ true }}"><sc-for list="{{tiles}}" as="k" hint-placeholder-count="4">'
          '<button class="pick" type="button" onClick="{{k.pick}}" style="display:flex;align-items:center;gap:14px;height:44px;'
          'padding:0 6px;border-bottom:1px solid rgba(116,123,173,.22)"><span class="lab" style="font-size:11px;width:84px">{{k.label}}</span>'
          '<span style="flex:1;font-size:19px">{{k.value}}</span><span style="color:#747BAD;display:flex;opacity:{{k.chev}}">' + CHEV +
          '</span></button></sc-for></sc-if>'
          '<sc-if value="{{isPicking}}" hint-placeholder-val="{{ false }}"><span class="lab" style="padding:2px 6px 8px">{{pickTitle}}</span>'
          '<div class="fade" style="display:flex;flex-wrap:wrap;gap:6px"><sc-for list="{{choices}}" as="c" hint-placeholder-count="5">'
          '<button class="pick sls" type="button" onClick="{{c.pick}}" style="padding:9px 14px;background:{{c.fill}};'
          'box-shadow:inset 0 0 0 1px {{c.edge}}"><span style="font-size:16px;color:{{c.col}}">{{c.name}}</span></button></sc-for></div></sc-if>'
          '<sc-if value="{{hasAction}}" hint-placeholder-val="{{ true }}"><span style="display:flex;padding-top:10px">'
          '<button class="{{actionClass}}" type="button" onClick="{{action}}"><span class="kc">X</span><span>{{actionLabel}}</span>'
          '</button></span></sc-if></aside></sc-if>'
          # the rail: the squad dial on the left, the light in the middle, Arena on the right
          '<div class="abs" style="left:40px;top:972px;z-index:6;display:flex;align-items:center;gap:14px">'
          '<button class="pick" type="button" onClick="{{prevSquad}}" aria-label="Previous squad" style="color:#C9CDEA;display:flex;'
          'padding:10px">' + CHEV_L + '</button><span style="display:flex;flex-direction:column;gap:3px;min-width:200px">'
          '<span style="font-size:26px;line-height:1">{{squadName}}</span><span class="lab" style="font-size:11px">{{whose}}</span></span>'
          '<button class="pick" type="button" onClick="{{nextSquad}}" aria-label="Next squad" style="color:#C9CDEA;display:flex;'
          'padding:10px">' + CHEV_R + '</button><span style="width:1px;height:36px;background:rgba(210,162,90,.35);margin:0 8px"></span>'
          '<button class="btn sm def sls" type="button" onClick="{{make}}">' + PLUS + '<span>Make</span></button>'
          '<button class="btn sm def sls" type="button" onClick="{{join}}"><span>Join</span></button></div>'
          '<div class="abs" style="left:50%;top:984px;transform:translateX(-50%);z-index:6;display:flex;align-items:center;gap:22px">'
          '<sc-for list="{{domains}}" as="d" hint-placeholder-count="3"><button class="pick" type="button" aria-pressed="{{d.pressed}}" '
          'aria-label="{{d.aria}}" onClick="{{d.pick}}" style="display:flex;align-items:center;gap:8px;opacity:{{d.op}}">'
          '<span style="width:14px;height:14px;transform:rotate(45deg);background:{{d.dot}};box-shadow:0 0 {{d.glow}} {{d.dot}}"></span>'
          '<span class="cap" style="font-size:13px;color:{{d.col}}">{{d.name}}</span></button></sc-for></div>'
          + TOAST + FOOT.replace("bottom:36px", "bottom:44px"))

B_JS = logic("""
 renderVals() {
  const st = this.state, c = this.common(), q = BOOK[st.sq], open = st.seat >= 0, d = open ? this.seatData(st.seat) : this.seatData(0);
  const ORDER = [1, 0, 2], XS = [490, 810, 1130];
  const plates = ORDER.map((i, col) => { const s = this.seatData(i), on = i === st.seat, live = s.on && !s.open;
   return { x: XS[col], y: on ? 744 : 770, who: s.who, line: s.open ? 'Anyone in the squad can take it' : s.v + ' - ' + s.e,
    avatar: s.avatar, aria: 'Seat: ' + s.who, pressed: on ? 'true' : 'false', op: live || on ? 1 : 0.7,
    fill: on ? hexA(c.dc, 0.26) : hexA(c.dc, 0.08), edge: on ? c.dc : hexA(c.dc, live ? 0.45 : 0.2),
    dot: live ? c.dc : '#5C5F70', filter: live ? 'none' : 'grayscale(1) brightness(.6)',
    pick: () => this.setState({ seat: on ? -1 : i, picking: '' }) }; });
  const col = Math.max(0, ORDER.indexOf(st.seat)), px = XS[col];
  const tiles = KEYS.map((k) => ({ label: k[1], value: d.open ? '-' : d[k[0]], chev: this.canEdit(d) && !d.open ? 1 : 0,
   pick: () => { if (d.open) return; if (!this.canEdit(d)) { this.say('Only ' + q.owner + ' changes this seat.'); return; }
    this.setState({ picking: k[0] }); } }));
  const pk = st.picking, cur = pk ? d[pk] : '';
  const choices = pk ? CHOICES[pk].map((n) => ({ name: n, col: n === cur ? '#E6E9FF' : '#9AA0CC', edge: n === cur ? c.dc : 'rgba(116,123,173,.35)',
   fill: n === cur ? hexA(c.dc, 0.18) : 'rgba(0,4,31,.4)', pick: () => this.choose(st.seat, pk, n) })) : [];
  const offlineFriend = !d.on && !d.me && !d.open && d.mine;
  const domains = c.domains.map((x) => Object.assign(x, { op: q.mine || x.pressed === 'true' ? 1 : 0.35,
   glow: x.pressed === 'true' ? '14px' : '0', aria: 'Light the squad ' + x.name,
   pick: q.mine ? x.pick : () => this.say('Only ' + q.owner + " changes the squad's light.") }));
  const go = (k) => this.setState({ sq: (st.sq + k + BOOK.length) % BOOK.length, seat: -1, picking: '' });
  return Object.assign(c, { plates, seatOpen: open, domains, whose: q.mine ? 'Your squad' : q.owner + "'s squad",
   domeFill: hexA(c.dc, 0.07), projX: px - 60, projY: pk ? 300 : 470, beamX: px, beamTop: pk ? 560 : 640, beamH: pk ? 200 : 120,
   tiles, isPicking: !!pk, notPicking: !pk, pickTitle: pk ? 'Pick a ' + KEYS.find((k) => k[0] === pk)[1].toLowerCase() : '', choices,
   hasAction: (d.me || offlineFriend) && !pk, actionLabel: d.me ? 'Change vessel' : 'Open the seat', actionClass: d.me ? 'btn sm def sls' : 'btn sm cta sls',
   action: () => (d.me ? this.setState({ picking: 'v' }) : this.openSeat(st.seat)),
   prevSquad: () => go(-1), nextSquad: () => go(1) });
 }
}
""", seat0="0")

# ===================================================================================================== C - seat by seat
C_BODY = (BACKDROP + nav() + TABROW + SQUAD_SWITCH +
          '<div class="abs" style="inset:0;background:linear-gradient(270deg,rgba(0,1,10,.9) 0,rgba(0,1,10,.7) 24%,rgba(0,1,10,0) 44%);'
          'pointer-events:none;z-index:1"></div>'
          # the focus: four corner ticks around the seat's vessel, moving from pad to pad
          '<div class="abs" style="left:{{fx}}px;top:{{fy}}px;width:360px;height:200px;transition:left 420ms cubic-bezier(.2,.8,.2,1),'
          'top 420ms cubic-bezier(.2,.8,.2,1);z-index:2;pointer-events:none">'
          '<span class="abs" style="left:0;top:0;width:28px;height:28px;border-left:2px solid {{dc}};border-top:2px solid {{dc}}"></span>'
          '<span class="abs" style="right:0;top:0;width:28px;height:28px;border-right:2px solid {{dc}};border-top:2px solid {{dc}}"></span>'
          '<span class="abs" style="left:0;bottom:0;width:28px;height:28px;border-left:2px solid {{dc}};border-bottom:2px solid {{dc}}"></span>'
          '<span class="abs" style="right:0;bottom:0;width:28px;height:28px;border-right:2px solid {{dc}};border-bottom:2px solid {{dc}}"></span></div>'
          # the seat, in type, no panel
          '<section aria-label="Seat" class="abs" style="left:1330px;top:200px;width:540px;z-index:3;display:flex;flex-direction:column;gap:0">'
          '<span class="lab num">Seat {{seatNo}} of 3</span>'
          '<span class="ell" style="font-size:72px;line-height:1.05;margin-top:8px;text-shadow:0 2px 18px rgba(0,1,10,.9)">{{s.who}}</span>'
          '<span style="display:flex;align-items:center;gap:10px;margin-top:6px"><span style="width:8px;height:8px;transform:rotate(45deg);'
          'background:{{sDot}}"></span><span class="lab">{{s.state}}</span></span>'
          '<div style="display:flex;flex-direction:column;margin-top:34px">'
          '<sc-for list="{{lines}}" as="k" hint-placeholder-count="4"><div style="position:relative;display:flex;flex-direction:column;'
          'gap:4px;padding:12px 0 12px 18px"><span class="abs" style="left:0;top:14px;bottom:14px;width:2px;background:{{k.mark}}"></span>'
          '<span class="lab" style="font-size:11px">{{k.label}}</span>'
          '<sc-if value="{{k.idle}}" hint-placeholder-val="{{ true }}"><button class="pick" type="button" onClick="{{k.pick}}" '
          'style="font-size:32px;line-height:1.1;text-shadow:0 2px 12px rgba(0,1,10,.9)">{{k.value}}</button></sc-if>'
          '<sc-if value="{{k.live}}" hint-placeholder-val="{{ false }}"><span style="display:flex;align-items:center;gap:18px">'
          '<button class="pick" type="button" onClick="{{k.prev}}" aria-label="Previous" style="display:flex;color:{{dc}};padding:6px">' + CHEV_L +
          '</button><span style="font-size:32px;line-height:1.1;min-width:200px">{{k.value}}</span><button class="pick" type="button" '
          'onClick="{{k.next}}" aria-label="Next" style="display:flex;color:{{dc}};padding:6px">' + CHEV_R + '</button>'
          '<button class="pick" type="button" onClick="{{k.done}}"><span class="kc">A</span></button></span></sc-if></div></sc-for></div>'
          '<sc-if value="{{hasAction}}" hint-placeholder-val="{{ true }}"><button class="pick" type="button" onClick="{{action}}" '
          'style="display:flex;align-items:center;gap:10px;margin-top:22px;color:{{actionCol}}"><span class="kc">X</span>'
          '<span class="cap" style="font-size:16px">{{actionLabel}}</span></button></sc-if>'
          '<div style="display:flex;align-items:center;gap:14px;margin-top:40px"><button class="pick" type="button" onClick="{{prevSeat}}" '
          'aria-label="Previous seat" style="display:flex;color:#C9CDEA;padding:6px">' + CHEV_L + '</button>'
          '<sc-for list="{{pips}}" as="p" hint-placeholder-count="3"><button class="pick" type="button" onClick="{{p.pick}}" '
          'aria-label="{{p.aria}}" style="width:10px;height:10px;transform:rotate(45deg);background:{{p.fill}};box-shadow:inset 0 0 0 1px {{p.edge}}">'
          '</button></sc-for><button class="pick" type="button" onClick="{{nextSeat}}" aria-label="Next seat" style="display:flex;'
          'color:#C9CDEA;padding:6px">' + CHEV_R + '</button><span class="kc">D-pad</span></div></section>'
          + SQUAD_LIST + TOAST + FOOT)

C_JS = logic("""
 renderVals() {
  const st = this.state, c = this.common(), q = BOOK[st.sq], i = st.seat, d = this.seatData(i), pad = PADS[i];
  const pk = st.picking;
  const lines = KEYS.map((k) => { const live = st.picking === k[0], list = CHOICES[k[0]], at = list.indexOf(d[k[0]]);
   const step = (n) => this.choose(i, k[0], list[(at + n + list.length) % list.length]);
   return { label: k[1], value: d.open ? '-' : d[k[0]], live, idle: !live, mark: live ? c.dc : 'transparent',
    pick: () => { if (d.open) return; if (!this.canEdit(d)) { this.say('Only ' + q.owner + ' changes this seat.'); return; }
     this.setState({ picking: k[0] }); },
    prev: () => { step(-1); this.setState({ picking: k[0] }); }, next: () => { step(1); this.setState({ picking: k[0] }); },
    done: () => this.setState({ picking: '' }) }; });
  const offlineFriend = !d.on && !d.me && !d.open && d.mine;
  const go = (n) => this.setState({ seat: (i + n + 3) % 3, picking: '' });
  const pips = [0, 1, 2].map((n) => ({ fill: n === i ? c.dc : 'transparent', edge: n === i ? c.dc : '#747BAD', aria: 'Seat ' + (n + 1),
   pick: () => this.setState({ seat: n, picking: '' }) }));
  return Object.assign(c, { s: d, seatNo: String(i + 1), sDot: d.open ? '#5C5F70' : (d.on ? c.dc : '#5C5F70'), lines,
   fx: pad.x - 180, fy: pad.y - 120, pips, prevSeat: () => go(-1), nextSeat: () => go(1),
   hasAction: (d.me || offlineFriend) && !pk, actionLabel: d.me ? 'Change vessel' : 'Open the seat', actionCol: d.me ? '#E6E9FF' : '#99FF80',
   action: () => (d.me ? this.setState({ picking: 'v' }) : this.openSeat(i)) });
 }
}
""", seat0="0")

# ===================================================================================================== Compare (static)
def cmp_cell(inner, w=420, h=250):
    return (f'<div style="position:relative;width:{w}px;height:{h}px;overflow:hidden;background:#00041F;'
            f'box-shadow:inset 0 0 0 1px #222645"><img src="{HANGAR["Jade"]}" alt="" style="position:absolute;inset:0;'
            f'width:100%;height:100%;object-fit:cover;object-position:{{pos}};opacity:.85">{inner}</div>')


def build_card():
    rows = "".join(f'<span style="display:flex;justify-content:space-between;padding:2px 0;border-top:1px solid rgba(116,123,173,.25)">'
                   f'<span class="lab" style="font-size:9px">{a}</span><span style="font-size:11px">{b}</span></span>'
                   for a, b in (("Vessel", "Dolphin"), ("Element", "Space"), ("Pilot", "Riptide"), ("Artifact", "Hydra")))
    return ('<div class="abs sl" style="right:14px;top:14px;width:250px;box-sizing:border-box;padding:8px 10px;'
            'background:rgba(0,4,31,.75);display:flex;flex-direction:column;gap:2px;box-shadow:inset 0 0 0 1px rgba(116,123,173,.45)">'
            '<span class="lab" style="font-size:9px">02 Rogue One</span><span style="font-size:14px">Halcyon</span>'
            '<span style="font-size:10px;color:#9AA0CC">Online</span>' + rows + '</div>')


def tag(x, y, who, state, ava, on=True, sel=False):
    edge = "#21C0BD" if sel else ("rgba(33,192,189,.5)" if on else "rgba(116,123,173,.35)")
    fill = "rgba(33,192,189,.22)" if sel else "rgba(0,1,10,.62)"
    filt = "none" if on else "grayscale(1) brightness(.6)"
    return (f'<div class="abs" style="left:{x}px;top:{y}px;transform:translateX(-50%);display:flex;flex-direction:column;align-items:center">'
            f'<span class="sls" style="display:flex;align-items:center;gap:6px;padding:3px 8px 3px 3px;background:{fill};box-shadow:inset 0 0 0 1px {edge}">'
            f'<img src="/_blob/{ava}" alt="" class="sls" style="width:24px;height:24px;object-fit:cover;filter:{filt}">'
            f'<span style="display:flex;flex-direction:column"><span style="font-size:11px">{who}</span><span class="lab" style="font-size:7px">{state}</span></span></span>'
            f'<span style="width:1px;height:46px;background:linear-gradient(180deg,{edge},rgba(0,0,0,0))"></span></div>')


def cmp_body():
    cols = [("Built today (R67)", "about 80 words at rest: the squads row, the domain row, the header, three cards of five lines, the foot"),
            ("A - Levels", "about 10 words at rest: the squad's name, three names and whether they are here, Mission, Arena"),
            ("B - Holo table", "about 20 words at rest: three names with vessel and element, the squad dial, Make, Join, the light, Arena"),
            ("C - Seat by seat", "about 20 words at rest: one seat's name and four lines, the squad's name, Arena")]
    rest = [
        build_card() + '<div class="abs" style="left:10px;top:10px;display:flex;gap:4px"><span class="sls" style="padding:3px 6px;'
        'background:rgba(0,4,31,.7);box-shadow:inset 0 0 0 1px #00D4FF;font-size:9px">SKIM WING</span><span class="sls" style="padding:3px 6px;'
        'background:rgba(0,4,31,.7);font-size:9px;color:#9AA0CC">BREAKERS</span><span class="sls" style="padding:3px 6px;'
        'background:rgba(0,4,31,.7);font-size:9px;color:#9AA0CC">+ MAKE</span></div><div class="abs" style="left:10px;top:34px;display:flex;'
        'gap:4px"><span class="sls" style="padding:3px 6px;background:rgba(0,4,31,.7);font-size:9px">JADE</span><span class="sls" '
        'style="padding:3px 6px;background:rgba(0,4,31,.7);font-size:9px;color:#9AA0CC">RUBY</span><span class="sls" style="padding:3px 6px;'
        'background:rgba(0,4,31,.7);font-size:9px;color:#9AA0CC">GOLD</span></div>',
        tag(56, 80, "Halcyon", "Online", CLOSE["ZODIAC"]) + tag(150, 86, "You", "Online", CLOSE["VOLT"]) +
        tag(240, 50, "Mothwing", "Offline", CLOSE["ANVIL"], on=False) +
        '<span class="abs" style="left:12px;top:10px;font-size:15px">Skim Wing</span>',
        '<div class="abs lat" style="left:30px;top:150px;width:360px;height:160px;border-radius:50%;background-color:rgba(33,192,189,.08);'
        'border:1px solid rgba(33,192,189,.6)"></div><div class="abs" style="left:0;right:0;top:220px;height:30px;background:#0B0A08;'
        'border-top:2px solid #8A6A3A"></div>' + "".join(
            f'<span class="abs sls" style="left:{x}px;top:{y}px;width:118px;padding:5px 6px;box-sizing:border-box;font-size:11px;'
            f'background:rgba(33,192,189,{a});box-shadow:inset 0 0 0 1px rgba(33,192,189,.6);transform:perspective(500px) rotateX(16deg)">'
            f'{w}<br><span style="font-size:9px;color:#9AA0CC">{v}</span></span>'
            for x, y, w, v, a in ((24, 176, "Halcyon", "Dolphin - Space", ".08"), (151, 168, "You", "Sparrow - Charge", ".26"),
                                  (278, 176, "Mothwing", "Serpent - Mass", ".05"))),
        '<div class="abs" style="inset:0;background:linear-gradient(270deg,rgba(0,1,10,.9),rgba(0,1,10,0) 60%)"></div>'
        '<span class="abs" style="left:110px;top:120px;width:120px;height:70px;box-shadow:inset 0 0 0 2px rgba(33,192,189,.0)"></span>'
        '<span class="abs" style="left:100px;top:110px;width:16px;height:16px;border-left:2px solid #21C0BD;border-top:2px solid #21C0BD"></span>'
        '<span class="abs" style="left:214px;top:174px;width:16px;height:16px;border-right:2px solid #21C0BD;border-bottom:2px solid #21C0BD"></span>'
        '<div class="abs" style="right:14px;top:22px;width:150px;display:flex;flex-direction:column;gap:3px"><span class="lab" style="font-size:8px">'
        'Seat 1 of 3</span><span style="font-size:24px">You</span><span class="lab" style="font-size:7px;margin-top:6px">Vessel</span>'
        '<span style="font-size:13px">Sparrow</span><span class="lab" style="font-size:7px">Element</span><span style="font-size:13px">Charge</span>'
        '<span class="lab" style="font-size:7px">Pilot</span><span style="font-size:13px">VOLT</span><span class="lab" style="font-size:7px">'
        'Artifact</span><span style="font-size:13px">Javelin</span></div>',
    ]
    opened = [
        build_card() + '<div class="abs" style="left:10px;bottom:10px;width:250px;padding:8px;background:rgba(0,4,31,.85);font-size:10px;'
        'color:#9AA0CC;box-shadow:inset 0 0 0 1px #222645">EDIT SQUAD opens the picker: vessel, pilot, element, who flies it and the '
        'artifact, all at once, for the card picked</div>',
        tag(56, 80, "Halcyon", "Online", CLOSE["ZODIAC"], sel=True) +
        '<div class="abs lat sl" style="right:10px;top:10px;width:190px;bottom:10px;box-sizing:border-box;padding:10px;'
        'background-color:rgba(0,1,10,.75);box-shadow:inset 0 0 0 1px rgba(33,192,189,.6);display:flex;flex-direction:column;gap:5px">'
        f'<span style="display:flex;gap:8px;align-items:center"><img src="/_blob/{CLOSE["ZODIAC"]}" alt="" class="sls" style="width:36px;height:36px;object-fit:cover">'
        '<span style="font-size:15px">Halcyon</span></span>' + "".join(
            f'<span class="sls" style="display:flex;justify-content:space-between;padding:6px 8px;background:rgba(0,4,31,.5);'
            f'box-shadow:inset 0 0 0 1px rgba(116,123,173,.35)"><span class="lab" style="font-size:8px">{a}</span>'
            f'<span style="font-size:12px">{b}</span></span>'
            for a, b in (("Vessel", "Dolphin"), ("Element", "Space"), ("Pilot", "ZODIAC"), ("Artifact", "Hydra"))) + '</div>',
        '<div class="abs" style="left:0;right:0;top:220px;height:30px;background:#0B0A08;border-top:2px solid #8A6A3A"></div>'
        '<div class="abs" style="left:140px;top:120px;width:140px;height:60px;background:linear-gradient(0deg,rgba(33,192,189,.35),'
        'rgba(0,0,0,0));clip-path:polygon(0 100%,100% 100%,90% 0,10% 0)"></div><div class="abs lat sl" style="left:110px;top:20px;width:200px;'
        'box-sizing:border-box;padding:8px;background-color:rgba(0,1,10,.55);box-shadow:inset 0 0 0 1px rgba(33,192,189,.6);'
        'display:flex;flex-direction:column;gap:2px">' + "".join(
            f'<span style="display:flex;justify-content:space-between;padding:3px 2px;border-bottom:1px solid rgba(116,123,173,.22)">'
            f'<span class="lab" style="font-size:8px">{a}</span><span style="font-size:11px">{b}</span></span>'
            for a, b in (("Vessel", "Sparrow"), ("Element", "Charge"), ("Pilot", "VOLT"), ("Artifact", "Javelin"))) + '</div>'
        '<span class="abs sls" style="left:151px;top:160px;width:118px;padding:5px 6px;box-sizing:border-box;font-size:11px;'
        'background:rgba(33,192,189,.26);box-shadow:inset 0 0 0 1px #21C0BD">You</span>',
        '<div class="abs" style="inset:0;background:linear-gradient(270deg,rgba(0,1,10,.9),rgba(0,1,10,0) 60%)"></div>'
        '<div class="abs" style="right:14px;top:22px;width:170px;display:flex;flex-direction:column;gap:3px"><span class="lab" style="font-size:8px">'
        'Seat 1 of 3</span><span style="font-size:24px">You</span><span class="lab" style="font-size:7px;margin-top:6px">Vessel</span>'
        '<span style="display:flex;align-items:center;gap:8px;font-size:13px;padding-left:6px;border-left:2px solid #21C0BD">'
        '<span style="color:#21C0BD">&lsaquo;</span>Dolphin<span style="color:#21C0BD">&rsaquo;</span></span>'
        '<span class="lab" style="font-size:7px">Element</span><span style="font-size:13px">Charge</span></div>',
    ]
    pos = ["70% 40%", "15% 40%", "35% 55%", "40% 45%"]
    head = "".join(f'<div style="width:420px;display:flex;flex-direction:column;gap:6px"><span style="font-size:24px">{t}</span>'
                   f'<span style="font-size:14px;line-height:1.4;color:#9AA0CC">{s}</span></div>' for t, s in cols)
    r1 = "".join(cmp_cell(rest[i]).replace("{pos}", pos[i]) for i in range(4))
    r2 = "".join(cmp_cell(opened[i]).replace("{pos}", pos[i]) for i in range(4))
    presses = ("3 presses: Edit squad, the card, the vessel - every other choice on screen meanwhile",
               "3 presses: the tag, Vessel, the vessel - one choice on screen at a time",
               "3 presses: the plate, Vessel, the vessel - the choice projected over its plate",
               "2-3 presses: D-pad to the seat, Vessel, left / right - the value changes in place")
    r3 = "".join(f'<span style="width:420px;font-size:14px;line-height:1.45;color:#C9CDEA">{p}</span>' for p in presses)
    return ('<div class="abs" style="left:64px;top:48px;display:flex;flex-direction:column;gap:6px"><span class="lab">Rev 106 - compare</span>'
            '<span style="font-size:34px">One seat, at rest and opened, in the build and in A, B and C</span></div>'
            '<div class="abs" style="left:64px;top:150px;display:flex;flex-direction:column;gap:18px">'
            f'<div style="display:flex;gap:40px">{head}</div>'
            '<span class="lab">At rest - what the Squads tab shows before anything is pressed</span>'
            f'<div style="display:flex;gap:40px">{r1}</div>'
            '<span class="lab">A seat opened</span>'
            f'<div style="display:flex;gap:40px">{r2}</div>'
            '<span class="lab">To change your vessel</span>'
            f'<div style="display:flex;gap:40px">{r3}</div></div>')


# ===================================================================================================== Notes (static)
def notes_body():
    def para(t, size=17, col="#C9CDEA"):
        return f'<p style="margin:0;font-size:{size}px;line-height:1.5;color:{col}">{t}</p>'
    left = (
        '<span class="lab">Asked - Shombith, 2026-11-13</span>'
        + para('"The UI cards on top of each screen. I do not like them at all ... the worst is the ark UI itself, selecting '
               'through so many and the text is noisy, it can be broken down in levels if we want." The hangar backdrops '
               'stay ("that\'s fire"). The style: the pilot scenes that show captains inside the Ark.', 18, "#E6E9FF")
        + '<span class="lab" style="margin-top:18px">Taken from the captains\' bridges</span>'
        + para('1. The room is dark and lit by its display. Nothing on these boards is a solid card: every panel is light, '
               'the squad\'s own domain colour, over a faint lattice, so the hangar shows through.')
        + para('2. The display has depth. Jugger and Scales look into a dome, not at a sheet. B puts the squad on that dome; '
               'A and C keep the light in the room, anchored to the vessels it is about.')
        + para('3. Warm practical lamps and a brass rail frame the cold light (B). A and C leave the warm light to the hangar\'s own render.')
        + para('4. The squad\'s domain IS the light: Jade lights the tags, the dome and the hangar Jade; switch to Ruby and all of it turns Ruby.')
        + '<span class="lab" style="margin-top:18px">Levels (all three)</span>'
        + para('Level 1, the hangar: who is in the squad and Arena. Level 2, one seat: vessel, element, pilot, artifact. '
               'Level 3, one choice: the list for that one thing. The squads, Make, Join and the domain move behind the squad\'s '
               'name (Y). Nothing the build does is dropped: four squads, make, join, the light, Change vessel, Open the seat, '
               'a joined squad read-only, Mission locked, Arena.')
        + '<span class="lab" style="margin-top:18px">For the build</span>'
        + para('SquadsPage keeps its data and actions; author_menu_ark.py lays out the tags (positions per pad, from the render), '
               'the seat panel and the list. The colours come from the worn set\'s domain colours. The Breakers hangar is still '
               'Skim Wing\'s vessels lit Ruby, as in section 190.'))
    right = (
        f'<div style="display:flex;gap:16px"><img src="{REF["Jugger"]}" alt="Jugger on the bridge, a dome of live play under his hand" '
        'style="width:400px;height:225px;object-fit:cover"><img src="' + REF["Scales"] + '" alt="Scales on a bridge, the cell in a dome '
        'of light, a brass rail" style="width:400px;height:225px;object-fit:cover"></div>'
        '<span class="lab" style="font-size:11px">Jugger and Scales, Docs/Pilots/art/scenes</span>'
        '<span class="lab" style="margin-top:22px">The options</span>'
        + para('<b>A - Levels (recommended).</b> At rest only a tag of light over each vessel: the pilot\'s face, the name, here or not. '
               'Press a tag and that seat opens on the right; press a line and only that list shows. About ten words at rest.')
        + para('<b>B - The holo table.</b> The bridge console in front of the hangar: the three seats stand on a dome of light, '
               'the picked one projects its loadout up. The squad dial, Make and Join and the light sit on the brass rail.')
        + para('<b>C - Seat by seat.</b> No panel at all: one seat in large type over the dark side of the hangar, corner brackets '
               'round its vessel, D-pad to the next seat, a value changes in place.')
        + '<span class="lab" style="margin-top:18px">Why A</span>'
        + para('It answers both words: the levels cut the text to the names, and the light sits on the vessels it describes, so '
               'the hangar render stays the screen. B is the most like the bridges but covers the floor of the render with a '
               'console; its light-on-a-dome look can dress A\'s seat panel if wanted. C is the calmest, but shows one seat at a '
               'time, so who is offline is read from the brackets only.')
)
    return ('<div class="abs" style="left:64px;top:48px;display:flex;flex-direction:column;gap:6px"><span class="lab">Rev 106 - notes</span>'
            '<span style="font-size:34px">The Squads tab in levels, lit like the captains\' bridges</span></div>'
            f'<div class="abs" style="left:64px;top:150px;width:860px;display:flex;flex-direction:column;gap:10px">{left}</div>'
            f'<div class="abs" style="left:1000px;top:150px;width:840px;display:flex;flex-direction:column;gap:10px">{right}</div>')


STATIC_JS = "class Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}"

BOARDS = {
    "R106A": ("Rev 106 A - Levels: tags over the vessels, a seat opens, one choice at a time (recommended) - interactive", A_BODY, A_JS, 1080),
    "R106B": ("Rev 106 B - The holo table: the squad on a bridge console, its seat projected up - interactive", B_BODY, B_JS, 1080),
    "R106C": ("Rev 106 C - Seat by seat: one seat in type over the hangar, no panels - interactive", C_BODY, C_JS, 1080),
    "R106Compare": ("Rev 106 - one seat at rest and open, in the build and in A, B and C", cmp_body(), STATIC_JS, 1080),
    "R106Notes": ("Rev 106 - notes: what was asked, the cues from the captains' bridges, words on screen", notes_body(), STATIC_JS, 900),
}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for key, (title, body, js, h) in BOARDS.items():
        with open(os.path.join(OUT, key + ".dc.html"), "w") as f:
            f.write(board(title, body, js, h=h))
        print("wrote", key)
