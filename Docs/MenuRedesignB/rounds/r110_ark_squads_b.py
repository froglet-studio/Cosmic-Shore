#!/usr/bin/env python3
"""Rev 110 - the Squads tab's option B (the holo table, picked by Shombith 2026-11-13: "B is good") with every setting,
and the check of real vessel models over the hangar video (Docs/MENU_REDESIGN_B.md section 200).

Shombith, 2026-11-13: "Can you show me all the settings and on changing the vessel will the model behind the image change
since this is a video so should be put actual vessel models inside and check?"

    R110B       every setting of B on one interactive board: a dashed "Mockup - every setting" row jumps to each state
                (at rest, your seat, the vessel / element / pilot / artifact lists, an offline mate and Open the seat,
                who flies an open seat, the squad dial and a joined squad, the light, Make, Join a friend)
    R110Notes   the vessel-swap check: today's clip (the hulls baked in), the empty-pad hangar, the live model layer, the
                composite; rendered by r67_ark_tabs_render.py (ARK_LINE, ARK_PASS=vessels)

    python3 Docs/MenuRedesignB/rounds/r110_ark_squads_b.py <root>
"""
import importlib.util
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("r106", os.path.join(HERE, "r106_ark_squads.py"))
B6 = importlib.util.module_from_spec(_spec)
sys.argv, _argv = [sys.argv[0]], sys.argv          # r106 reads argv[1] as its root; keep it from ours
_spec.loader.exec_module(B6)
sys.argv = _argv
ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
OUT = os.path.join(ROOT, "project")
SWAP = "/_blob/00d6d67b8e8c484472ef059f3a564131"     # the swap-check sheet (r110 renders), uploaded to the canvas

BOOK = B6.BOOK + [{"name": "New squad", "dom": "Jade", "mine": True, "owner": "You", "fresh": True, "seats": [
    {"who": "You", "v": "Sparrow", "e": "Charge", "p": "VOLT", "a": "Javelin", "on": True, "me": True},
    {"who": "Open seat", "v": "Dolphin", "e": "Space", "p": "ZODIAC", "a": "None", "on": False, "open": True},
    {"who": "Open seat", "v": "Serpent", "e": "Mass", "p": "ANVIL", "a": "None", "on": False, "open": True}]}]
FRIENDS = [{"owner": "Kestrel", "squad": "Breakers", "dom": "Ruby", "open": "1 open seat", "sq": 1},
           {"owner": "Wren", "squad": "Tidebreak", "dom": "Gold", "open": "2 open seats", "sq": -1},
           {"owner": "Halcyon", "squad": "Lantern", "dom": "Jade", "open": "1 open seat", "sq": -1}]
STATES = ["At rest", "Your seat", "Vessel", "Element", "Pilot", "Artifact", "Offline mate", "Open seat", "Who flies it",
          "Joined squad", "Light", "Make", "Join a friend"]

S = B6
CHEV, CHEV_L, CHEV_R, PLUS, LOCK = S.CHEV, S.CHEV_L, S.CHEV_R, S.PLUS, S.LOCK

BODY = (S.BACKDROP + S.nav() + S.TABROW +
        # the mockup's state row (not part of the game's UI)
        '<div class="abs" style="left:40px;top:160px;right:40px;display:flex;flex-wrap:wrap;align-items:center;gap:6px;'
        'padding:6px 10px;border:1px dashed rgba(154,160,204,.5);z-index:9"><span class="lab" style="font-size:11px;margin-right:6px">'
        'Mockup - every setting</span><sc-for list="{{states}}" as="m" hint-placeholder-count="13"><button class="pick sls" type="button" '
        'aria-pressed="{{m.pressed}}" onClick="{{m.pick}}" style="padding:5px 10px;background:rgba(0,1,10,.7);box-shadow:inset 0 0 0 1px {{m.edge}}">'
        '<span class="cap" style="font-size:12px;color:{{m.col}}">{{m.name}}</span></button></sc-for></div>'
        # the bridge
        '<div class="abs" style="left:-140px;bottom:-60px;width:420px;height:300px;border-radius:50%;'
        'background:radial-gradient(closest-side,rgba(255,174,0,.22),rgba(255,174,0,0));pointer-events:none"></div>'
        '<div class="abs" style="right:-140px;bottom:-60px;width:420px;height:300px;border-radius:50%;'
        'background:radial-gradient(closest-side,rgba(255,174,0,.22),rgba(255,174,0,0));pointer-events:none"></div>'
        '<div class="abs lat" style="left:330px;top:676px;width:1260px;height:520px;border-radius:50%;'
        'background-color:{{domeFill}};box-shadow:inset 0 30px 80px -20px {{lightGlow}},0 -10px 80px -30px {{lightGlow}};'
        'border:1px solid {{lightEdge}};z-index:2"></div>'
        '<div class="abs" style="left:0;right:0;top:944px;height:136px;z-index:3;background:linear-gradient(180deg,'
        '#1B1710 0,#0B0A08 40%,#05060A 100%);border-top:3px solid #8A6A3A;box-shadow:0 -1px 0 0 #D2A25A,0 -18px 40px -10px rgba(255,174,0,.18)"></div>'
        # the plates
        '<sc-for list="{{plates}}" as="t" hint-placeholder-count="3"><button class="pick abs sls lat" type="button" onClick="{{t.pick}}" '
        'aria-pressed="{{t.pressed}}" aria-label="{{t.aria}}" style="left:{{t.x}}px;top:{{t.y}}px;width:300px;height:82px;box-sizing:border-box;'
        'padding:12px 14px;z-index:4;display:flex;align-items:center;gap:12px;background-color:{{t.fill}};box-shadow:inset 0 0 0 1px {{t.edge}};'
        'transform:perspective(900px) rotateX(16deg);transition:top 300ms ease-out;opacity:{{t.op}}">'
        '<sc-if value="{{t.hasAva}}" hint-placeholder-val="{{ true }}"><img class="ava sls" src="{{t.avatar}}" alt="" style="width:56px;height:56px;filter:{{t.filter}}"></sc-if>'
        '<sc-if value="{{t.noAva}}" hint-placeholder-val="{{ false }}"><span class="sls" style="width:56px;height:56px;box-shadow:inset 0 0 0 1px {{t.edge}};'
        'display:flex;align-items:center;justify-content:center;color:{{t.edge}}">' + PLUS + '</span></sc-if>'
        '<span style="display:flex;flex-direction:column;gap:4px;min-width:0"><span class="ell" style="font-size:22px">{{t.who}}</span>'
        '<span class="ell" style="font-size:14px;color:#9AA0CC">{{t.line}}</span></span>'
        '<span class="abs" style="right:12px;top:12px;width:7px;height:7px;transform:rotate(45deg);background:{{t.dot}}"></span></button></sc-for>'
        # what the table projects: a seat, a list, or the friends' open seats
        '<sc-if value="{{projOn}}" hint-placeholder-val="{{ true }}"><div class="abs" style="left:{{beamX}}px;top:{{beamTop}}px;width:300px;'
        'height:{{beamH}}px;z-index:3;pointer-events:none;background:linear-gradient(0deg,{{lightGlow}},rgba(0,0,0,0));'
        'clip-path:polygon(0 100%,100% 100%,90% 0,10% 0);opacity:.6"></div>'
        '<aside aria-label="{{projAria}}" class="abs rise lat sl" style="left:{{projX}}px;top:{{projY}}px;width:420px;box-sizing:border-box;'
        'padding:16px 18px;z-index:5;background-color:rgba(0,1,10,.6);box-shadow:inset 0 0 0 1px {{lightEdge}},'
        'inset 0 -80px 120px -60px {{lightGlow}};display:flex;flex-direction:column;gap:4px">'
        '<span class="lab" style="padding:2px 6px 8px">{{projTitle}}</span>'
        '<sc-if value="{{showTiles}}" hint-placeholder-val="{{ true }}"><sc-for list="{{tiles}}" as="k" hint-placeholder-count="4">'
        '<button class="pick" type="button" onClick="{{k.pick}}" style="display:flex;align-items:center;gap:14px;height:44px;'
        'padding:0 6px;border-bottom:1px solid rgba(116,123,173,.22)"><span class="lab" style="font-size:11px;width:96px">{{k.label}}</span>'
        '<span style="flex:1;font-size:19px;color:{{k.col}}">{{k.value}}</span><span style="color:#747BAD;display:flex;opacity:{{k.chev}}">' + CHEV +
        '</span></button></sc-for></sc-if>'
        '<sc-if value="{{showChoices}}" hint-placeholder-val="{{ false }}"><div class="fade" style="display:flex;flex-wrap:wrap;gap:6px">'
        '<sc-for list="{{choices}}" as="c" hint-placeholder-count="5"><button class="pick sls" type="button" onClick="{{c.pick}}" '
        'style="display:flex;align-items:center;gap:8px;padding:{{c.pad}};background:{{c.fill}};box-shadow:inset 0 0 0 1px {{c.edge}}">'
        '<sc-if value="{{c.hasAva}}" hint-placeholder-val="{{ false }}"><img class="ava sls" src="{{c.avatar}}" alt="" style="width:34px;height:34px"></sc-if>'
        '<span style="font-size:16px;color:{{c.col}}">{{c.name}}</span></button></sc-for></div>'
        '<span style="font-size:13px;color:#9AA0CC;padding:8px 6px 0">{{choiceHint}}</span></sc-if>'
        '<sc-if value="{{showFriends}}" hint-placeholder-val="{{ false }}"><sc-for list="{{friends}}" as="f" hint-placeholder-count="3">'
        '<div style="display:flex;align-items:center;gap:12px;height:56px;padding:0 6px;border-bottom:1px solid rgba(116,123,173,.22)">'
        '<span style="width:9px;height:9px;transform:rotate(45deg);background:{{f.dot}}"></span><span style="flex:1;display:flex;flex-direction:column;gap:2px">'
        '<span style="font-size:18px">{{f.squad}}</span><span class="lab" style="font-size:10px">{{f.line}}</span></span>'
        '<button class="btn sm def sls" type="button" onClick="{{f.pick}}"><span>Join</span></button></div></sc-for></sc-if>'
        '<sc-if value="{{hasAction}}" hint-placeholder-val="{{ true }}"><span style="display:flex;padding-top:10px">'
        '<button class="{{actionClass}}" type="button" onClick="{{action}}"><span class="kc">X</span><span>{{actionLabel}}</span>'
        '</button></span></sc-if><sc-if value="{{readOnly}}" hint-placeholder-val="{{ false }}"><span style="font-size:13px;color:#9AA0CC;'
        'padding:10px 6px 0">{{readOnlyLine}}</span></sc-if></aside></sc-if>'
        # the rail
        '<div class="abs" style="left:40px;top:972px;z-index:6;display:flex;align-items:center;gap:14px">'
        '<button class="pick" type="button" onClick="{{prevSquad}}" aria-label="Previous squad" style="color:#C9CDEA;display:flex;padding:10px">'
        + CHEV_L + '</button><span style="display:flex;flex-direction:column;gap:3px;min-width:200px">'
        '<span style="font-size:26px;line-height:1">{{squadName}}</span><span class="lab" style="font-size:11px">{{whose}}</span></span>'
        '<button class="pick" type="button" onClick="{{nextSquad}}" aria-label="Next squad" style="color:#C9CDEA;display:flex;padding:10px">'
        + CHEV_R + '</button><span style="width:1px;height:36px;background:rgba(210,162,90,.35);margin:0 8px"></span>'
        '<button class="btn sm def sls" type="button" onClick="{{make}}">' + PLUS + '<span>Make</span></button>'
        '<button class="btn sm def sls" type="button" onClick="{{join}}"><span>Join</span></button></div>'
        '<div class="abs" style="left:50%;top:984px;transform:translateX(-50%);z-index:6;display:flex;align-items:center;gap:22px">'
        '<sc-for list="{{domains}}" as="d" hint-placeholder-count="3"><button class="pick" type="button" aria-pressed="{{d.pressed}}" '
        'aria-label="{{d.aria}}" onClick="{{d.pick}}" style="display:flex;align-items:center;gap:8px;opacity:{{d.op}}">'
        '<span style="width:14px;height:14px;transform:rotate(45deg);background:{{d.dot}};box-shadow:0 0 {{d.glow}} {{d.dot}}"></span>'
        '<span class="cap" style="font-size:13px;color:{{d.col}}">{{d.name}}</span></button></sc-for></div>'
        + S.TOAST.replace("top:170px", "top:214px") + S.FOOT.replace("bottom:36px", "bottom:44px"))

JS = """
const BOOK = %(book)s;
const CHOICES = %(choices)s;
const CLOSE = %(close)s;
const HANGAR = %(hangar)s;
const DOMC = %(domc)s;
const FRIENDS = %(friends)s;
const STATES = %(states)s;
const TABS = ["Converter", "Squads", "Pilots", "Artifacts", "Decor"];
const KEYS = [["v", "Vessel"], ["e", "Element"], ["p", "Pilot"], ["a", "Artifact"]];
const ORDER = [1, 0, 2], XS = [490, 810, 1130];
function hexA(h, a) { const n = parseInt(h.slice(1), 16); return 'rgba(' + (n >> 16) + ',' + ((n >> 8) & 255) + ',' + (n & 255) + ',' + a + ')'; }

class Component extends DCLogic {
 constructor(props) { super(props); this.state = { sq: 0, doms: BOOK.map((b) => b.dom), seat: -1, picking: '', made: false,
   joining: false, edits: {}, opened: {}, filled: {}, mode: 0, toast: '' }; this.t = null; }
 componentWillUnmount() { if (this.t) clearTimeout(this.t); }
 say(x) { if (this.t) clearTimeout(this.t); this.setState({ toast: x }); this.t = setTimeout(() => this.setState({ toast: '' }), 2600); }
 squads() { return this.state.made ? [0, 1, 2] : [0, 1]; }
 seatData(i) {
  const st = this.state, q = BOOK[st.sq], s = q.seats[i], k = st.sq + ':' + i, e = st.edits[k] || {}, fill = st.filled[k];
  const open = (!!st.opened[k] || !!s.open) && !fill;
  const who = fill ? fill : (open ? 'Open seat' : s.who), on = fill === 'AI wingmate' ? true : (open ? false : s.on);
  return { i, who, on, me: !!s.me, open, mine: q.mine, wait: fill === 'Open for a friend',
   v: e.v || s.v, e: e.e || s.e, p: e.p || s.p, a: e.a || s.a,
   state: open ? 'Open' : (fill === 'AI wingmate' ? 'AI wingmate' : (fill ? 'Waiting for a friend' : (on ? 'Online' : 'Offline'))),
   avatar: '/_blob/' + CLOSE[e.p || s.p] };
 }
 canEdit(d) { return BOOK[this.state.sq].mine || d.me; }
 choose(i, key, val) { const k = this.state.sq + ':' + i, e = Object.assign({}, this.state.edits);
  e[k] = Object.assign({}, e[k] || {}); e[k][key] = val; this.setState({ edits: e, picking: '' }); }
 jump(n) {
  const base = { sq: 0, seat: -1, picking: '', joining: false, made: this.state.made, opened: {}, filled: {}, mode: n, toast: '' };
  const S = [{}, { seat: 0 }, { seat: 0, picking: 'v' }, { seat: 0, picking: 'e' }, { seat: 0, picking: 'p' }, { seat: 0, picking: 'a' },
   { seat: 2 }, { seat: 2, opened: { '0:2': true } }, { seat: 2, opened: { '0:2': true }, picking: 'w' }, { sq: 1, seat: 2 },
   { seat: -1 }, { sq: 2, made: true, seat: 1 }, { joining: true }][n];
  this.setState(Object.assign(base, S));
  if (n === 10) this.say('The squad\\'s light: pick Jade, Ruby or Gold on the rail. The hangar and the table relight.');
 }
 renderVals() {
  const st = this.state, q = BOOK[st.sq], dom = st.doms[st.sq], dc = DOMC[dom], open = st.seat >= 0, d = this.seatData(open ? st.seat : 0);
  const states = STATES.map((n, i) => ({ name: n, pressed: i === st.mode ? 'true' : 'false', edge: i === st.mode ? '#00D4FF' : 'rgba(116,123,173,.45)',
   col: i === st.mode ? '#E6E9FF' : '#9AA0CC', pick: () => this.jump(i) }));
  const tabs = TABS.map((x, i) => ({ name: x, col: i === 1 ? '#E6E9FF' : (x === 'Decor' ? '#5C5F70' : '#747BAD'),
   mark: i === 1 ? '#E6E9FF' : 'transparent', locked: x === 'Decor', pick: () => this.say(x + ' is its own thread of this round.') }));
  const plates = ORDER.map((i, col) => { const s = this.seatData(i), on = i === st.seat, live = s.on && !s.open;
   return { x: XS[col], y: on ? 744 : 770, who: s.who, avatar: s.avatar, hasAva: !s.open, noAva: s.open,
    line: s.open ? (q.mine ? 'Pick who flies it' : 'Anyone in the squad can take it') : (s.wait ? 'Waiting for a friend' : s.v + ' - ' + s.e),
    aria: 'Seat: ' + s.who, pressed: on ? 'true' : 'false', op: live || on ? 1 : 0.72,
    fill: on ? hexA(dc, 0.26) : hexA(dc, 0.08), edge: on ? dc : hexA(dc, live ? 0.45 : 0.25),
    dot: live ? dc : '#5C5F70', filter: live ? 'none' : 'grayscale(1) brightness(.6)',
    pick: () => this.setState({ seat: on ? -1 : i, picking: '', joining: false }) }; });
  const col = Math.max(0, ORDER.indexOf(st.seat)), px = XS[col], pk = st.picking;
  const tiles = KEYS.map((k) => ({ label: k[1], value: d[k[0]], col: d.wait ? '#9AA0CC' : '#E6E9FF', chev: this.canEdit(d) ? 1 : 0,
   pick: () => { if (!this.canEdit(d)) { this.say('Only ' + q.owner + ' changes this seat.'); return; } this.setState({ picking: k[0] }); } }));
  if (d.wait || d.who === 'AI wingmate') tiles.unshift({ label: 'Who flies it', value: d.who, col: '#E6E9FF', chev: 1, pick: () => this.setState({ picking: 'w' }) });
  let choices = [], choiceHint = '';
  if (pk === 'w') { choices = ['AI wingmate', 'Open for a friend'].map((n) => ({ name: n, pad: '10px 16px', col: '#E6E9FF', edge: hexA(dc, 0.6),
    fill: 'rgba(0,4,31,.4)', hasAva: false, pick: () => { const f = Object.assign({}, st.filled); f[st.sq + ':' + st.seat] = n;
     this.setState({ filled: f, picking: '' }); } }));
    choiceHint = 'An AI wingmate flies the seat now; a friend who joins takes it from them.'; }
  else if (pk) { const cur = d[pk]; choices = CHOICES[pk].map((n) => ({ name: n, pad: pk === 'p' ? '4px 12px 4px 4px' : '9px 14px',
    col: n === cur ? '#E6E9FF' : '#9AA0CC', edge: n === cur ? dc : 'rgba(116,123,173,.35)', fill: n === cur ? hexA(dc, 0.18) : 'rgba(0,4,31,.4)',
    hasAva: pk === 'p', avatar: '/_blob/' + CLOSE[n], pick: () => this.choose(st.seat, pk, n) }));
    choiceHint = { v: 'Your pad in the hangar changes to the vessel you pick.', e: "The vessel's element: which of its four abilities it leads with.",
     p: "The pilot's callsign biases how an AI flies this seat.", a: 'Carried into the match; None leaves the slot empty.' }[pk]; }
  const offlineFriend = !d.on && !d.me && !d.open && !d.wait && d.mine;
  const friends = FRIENDS.map((f) => ({ squad: f.squad, line: f.owner + "'s squad - " + f.open, dot: DOMC[f.dom],
   pick: () => { this.setState({ joining: false, sq: f.sq >= 0 ? f.sq : st.sq, seat: -1 }); this.say('You joined ' + f.squad + '. Its leader sets the seats.'); } }));
  const projOn = (open && !(d.open && !q.mine)) || st.joining;
  const pickName = pk === 'w' ? 'Who flies it' : (pk ? KEYS.find((k) => k[0] === pk)[1] : '');
  const domains = ['Jade', 'Ruby', 'Gold'].map((k) => { const on = k === dom; return { name: k, dot: DOMC[k], col: on ? '#E6E9FF' : '#9AA0CC',
   pressed: on ? 'true' : 'false', op: q.mine || on ? 1 : 0.35, glow: on ? '14px' : '0', aria: 'Light the squad ' + k,
   pick: () => { if (!q.mine) { this.say('Only ' + q.owner + " changes the squad's light."); return; } const dd = st.doms.slice(); dd[st.sq] = k; this.setState({ doms: dd }); } }; });
  const list = this.squads(), at = list.indexOf(st.sq);
  const go = (k) => this.setState({ sq: list[(at + k + list.length) %% list.length], seat: -1, picking: '', joining: false });
  return { tabs, states, plates, domains, tiles, choices, choiceHint, friends, dc,
   bg: HANGAR[dom], bgAlt: 'Inside the Ark: the squad lined up in the hangar, lit ' + dom,
   lightEdge: hexA(dc, 0.6), lightGlow: hexA(dc, 0.35), domeFill: hexA(dc, 0.07),
   squadName: q.name, whose: q.fresh ? 'New - yours' : (q.mine ? 'Your squad' : q.owner + "'s squad - joined"),
   projOn, projAria: st.joining ? 'Join a friend' : 'Seat',
   projTitle: st.joining ? "Friends' squads with an open seat" : (pk ? 'Pick - ' + pickName : d.who + ' - ' + d.state),
   showTiles: !st.joining && !pk, showChoices: !st.joining && !!pk, showFriends: st.joining,
   projX: st.joining ? 750 : px - 60, projY: st.joining ? 420 : (pk ? 300 : 440), beamX: st.joining ? 810 : px, beamTop: pk ? 560 : 640, beamH: pk ? 200 : 120,
   hasAction: !st.joining && !pk && (d.me || offlineFriend || (d.open && q.mine)),
   actionLabel: d.me ? 'Change vessel' : (d.open ? 'Who flies it' : 'Open the seat'),
   actionClass: d.me ? 'btn sm def sls' : (d.open ? 'btn sm def sls' : 'btn sm cta sls'),
   action: () => { if (d.me) this.setState({ picking: 'v' }); else if (d.open) this.setState({ picking: 'w' });
    else { const o = Object.assign({}, st.opened); o[st.sq + ':' + st.seat] = true; this.setState({ opened: o });
     this.say(d.who + ' is offline. The seat is open; the pad stands empty until someone takes it.'); } },
   readOnly: !q.mine && !st.joining && open, readOnlyLine: q.owner + ' leads this squad and sets its seats; you change only your own.',
   toast: st.toast, hasToast: st.toast !== '',
   prevSquad: () => go(-1), nextSquad: () => go(1),
   make: () => { this.setState({ made: true, sq: 2, seat: 1, picking: '', joining: false, mode: 11 }); this.say('A new squad: you, and two open seats.'); },
   join: () => this.setState({ joining: !st.joining, seat: -1, picking: '', mode: 12 }),
   mission: () => this.say('Missions open later.'), arena: () => this.say('Taking ' + q.name + ' into the Arena.') };
 }
}
""" % dict(book=json.dumps(BOOK), choices=json.dumps(S.CHOICES), close=json.dumps(S.CLOSE), hangar=json.dumps(S.HANGAR),
           domc=json.dumps(S.DOMC), friends=json.dumps(FRIENDS), states=json.dumps(STATES))


def models_body():
    p = lambda t, size=17, col="#C9CDEA": f'<p style="margin:0;font-size:{size}px;line-height:1.5;color:{col}">{t}</p>'
    return ('<div class="abs" style="left:64px;top:48px;display:flex;flex-direction:column;gap:6px"><span class="lab">Rev 110 - the vessels in the hangar</span>'
            '<span style="font-size:34px">Change a vessel and the hangar changes with it: real models over an empty-pad video</span></div>'
            f'<img class="abs" src="{SWAP}" alt="Four renders: today\'s clip with the vessels baked in; the hangar with empty pads; the live layer of the squad\'s real models with a Rhino in your seat; the Rhino rendered into the room in one pass" '
            'style="left:64px;top:150px;width:1180px;height:708px;object-fit:contain;background:#00010A">'
            '<div class="abs" style="left:1290px;top:150px;width:570px;display:flex;flex-direction:column;gap:12px">'
            '<span class="lab">Asked - Shombith, 2026-11-13</span>'
            + p('"On changing the vessel will the model behind the image change since this is a video so should be put actual vessel models inside and check?"', 18, "#E6E9FF")
            + '<span class="lab" style="margin-top:10px">Today: no</span>'
            + p("Each Squads clip (one per colour set and domain, section 190) has the Dolphin, the Sparrow and the Serpent rendered into it (1). Changing a vessel changes the words, never the picture.")
            + '<span class="lab" style="margin-top:10px">The fix, checked</span>'
            + p("Render the same hangar with the pads empty (2) and draw the squad's real models on the pads live, from the same camera (3). Laid over the video (as in the game) it matches the Rhino rendered into the room in one pass (4) to within 2 of 255 per pixel on average.")
            + p("In the game: the empty-pad clips replace the nine Squads clips; a camera at the render's camera draws each seat's vessel prefab on its pad into a transparent layer over the video, rebuilt only when a seat's vessel changes; offline hulls powered down, open pads empty.")
            + p("Still to match in Unity: the hulls' light (the room's lamps and pad glow as real lights) and a little haze over them. Renders here use stand-in pictures on the wall boards and windows.", 15, "#9AA0CC")
            + '</div>')


BOARDS = {
    "R110B": ("Rev 110 B - The holo table with every setting: the seat, its four lists, an open seat, the squads, the light, Make, Join - interactive", BODY, JS, 1080),
    "R110Notes": ("Rev 110 - the vessels in the hangar: real models over an empty-pad video, checked", models_body(), S.STATIC_JS, 900),
}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for key, (title, body, js, h) in BOARDS.items():
        with open(os.path.join(OUT, key + ".dc.html"), "w") as f:
            f.write(S.board(title, body, js, h=h))
        print("wrote", key)
