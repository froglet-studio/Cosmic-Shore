#!/usr/bin/env python3
"""The Ark screen inside the real Ark, animated, in every colour set: four boards (Squads, Converter, Artifacts, Decor),
each its own room in the Ark, whose backdrops are the seamless loops of r67_ark_tabs_render.py, with the Ark screen's UI on top. Rev 61 C's style and
lighting (picked as the Ark's final design, Shombith 2026-11-06).

    python3 r67_ark_tabs_boards.py <root> <rev> <blobs.json>

blobs.json maps <scene>_<set>_<run> (scene: squads, converter, artifacts, decor; set: Nocturne, Photic, DartFrog; run:
loop, and convert for the converter) to /_blob/<id> animated WebPs. Every board has a MOCKUP colour-set switch (outside
the screen's own UI) that swaps the backdrop between the three sets. Colours follow Docs/UI_STYLE_GUIDE.md.
"""
import html
import json
import sys
from pathlib import Path

ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2])
BL = json.loads(Path(sys.argv[3]).read_text())
e = html.escape
W, H = 1920, 1080
SETS = [("Nocturne", "Nocturne"), ("Photic", "Photic"), ("DartFrog", "Dart Frog")]
NAV = [("CODEX", "/_blob/ec690d6b90d6fcdbc430b9bbaacf28a9"), ("HANGAR", "/_blob/34f5a50f1275fef42808fe9df3bf6af5"),
       ("HOME", "/_blob/a5bd4d8d1de7b494567fc3de75525e1e"), ("ARK", "/_blob/e5b050c5a676193c650e94d873ad40e2"),
       ("PORT", "/_blob/d0e850bc927559f1a499868d26f2a3de")]
LIGHT, SOFT, INACT, NEUT, NEUT_D, VDARK, BLACK = "#E6E9FF", "#747BAD", "#5C5F70", "#434C89", "#222645", "#00041F", "#00010A"
SUB = "#9AA0CC"
SEL, COMMIT, CTA, PURCHASE = "#00D4FF", "#A600FF", "#99FF80", "#FFAE00"
DOMC = {"Jade": "#21C0BD", "Ruby": "#BD1BC8", "Gold": "#FF8F11"}
DOMS = ["Jade", "Ruby", "Gold"]

HEAD = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{title}</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{{margin:0;background:#00010A}}
.lab{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.16em;text-transform:uppercase;color:#8C93C7;text-shadow:0 0 8px rgba(0,1,10,.95),0 1px 2px rgba(0,1,10,.95)}}
.cap{{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}}
.num{{font-variant-numeric:tabular-nums}}
.abs{{position:absolute}}
.sl{{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}}
.sls{{clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}}
.kc{{display:inline-flex;align-items:center;justify-content:center;min-width:26px;height:22px;padding:0 6px;box-sizing:border-box;border:1px solid #434C89;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:12px;color:#C9CDEA}}
.btn{{box-sizing:border-box;display:flex;align-items:center;justify-content:center;gap:10px;height:48px;padding:0 20px;white-space:nowrap;border:0;cursor:pointer;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:18px;letter-spacing:.1em;text-transform:uppercase}}
.btn.sm{{height:40px;padding:0 14px;font-size:14px}}
.commit{{background:#A600FF;color:#E6E9FF}}
.cta{{background:#99FF80;color:#00010A}}
.def{{background:rgba(0,4,31,.55);color:#E6E9FF;box-shadow:inset 0 0 0 1px #747BAD}}
.off{{background:rgba(0,4,31,.4);color:#5C5F70;box-shadow:inset 0 0 0 1px #222645}}
.btn .kc{{border-color:currentColor;color:inherit;opacity:.85}}
.btn:focus-visible,.pick:focus-visible{{outline:2px solid #00D4FF;outline-offset:2px}}
.pick{{background:none;border:0;padding:0;cursor:pointer;color:inherit;font:inherit;text-align:left}}
.glass{{background:rgba(0,4,31,.62);backdrop-filter:blur(14px);-webkit-backdrop-filter:blur(14px)}}
.bg{{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;transition:opacity 900ms ease-in-out}}
.ell{{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}}
</style>
</helmet>
"""
TAIL = """</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
{logic}</script>
</body>
</html>
"""
LOCK = ('<svg width="12" height="14" viewBox="0 0 14 16" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true">'
        '<path d="M3 7V5a4 4 0 018 0v2"></path><path d="M1.5 7h11v8h-11z"></path></svg>')
PLUS = ('<svg width="14" height="14" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">'
        '<path d="M7 1v12M1 7h12"></path></svg>')


def kc(t):
    return f'<span class="kc">{e(t)}</span>'


def nav():
    items = []
    for name, icon in NAV:
        on = name == "ARK"
        mark = '<span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:#E6E9FF"></span>' if on else ""
        items.append(f'<span style="position:relative;display:flex;align-items:center;gap:10px;font-family:\'Chakra Petch\';font-weight:600;'
                     f'font-size:17px;letter-spacing:.12em;color:{LIGHT if on else SOFT}"><img src="{icon}" alt="" '
                     f'style="width:26px;height:26px;object-fit:contain;opacity:{1 if on else .7}">{name}{mark}</span>')
    return (f'<nav aria-label="Menu screens" class="abs" style="left:0;right:0;top:0;height:84px;background:rgba(0,1,10,.78);'
            f'border-bottom:1px solid {NEUT_D};display:flex;align-items:center;justify-content:center;gap:48px;z-index:5">'
            f'{kc("LT")}{"".join(items)}{kc("RT")}</nav>'
            f'<div class="abs num" style="right:40px;top:30px;z-index:6;display:flex;align-items:center;gap:8px;font-size:20px;color:{PURCHASE}">'
            f'<span style="width:14px;height:18px;background:{PURCHASE};clip-path:polygon(50% 0,100% 35%,50% 100%,0 35%)"></span>1,240</div>')


def toast():
    return ('<sc-if value="{{hasToast}}" hint-placeholder-val="{{ false }}"><div class="abs glass sls" role="status" '
            'style="right:40px;top:172px;width:440px;padding:14px 18px 14px 22px;z-index:9;display:flex;flex-direction:column;gap:4px">'
            f'<span class="abs" style="left:0;top:12px;bottom:12px;width:3px;background:{LIGHT}"></span>'
            '<span class="lab" style="font-size:12px">Squads</span><span style="font-size:16px;line-height:1.4">{{toast}}</span></div></sc-if>')


def head(size=40):
    return (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab">{{{{kicker}}}}</span>'
            f'<span style="display:flex;align-items:center;gap:14px"><span style="width:12px;height:12px;transform:rotate(45deg);background:{{{{dc}}}}"></span>'
            f'<span style="font-size:{size}px;line-height:1.05">{{{{name}}}}</span></span>'
            f'<span style="font-size:15px;color:{SUB}">{{{{line}}}}</span></div>')


def squads_row():
    return ('<div style="display:flex;align-items:center;gap:8px"><span class="lab" style="font-size:12px;margin-right:6px">Squads</span>'
            '<sc-for list="{{squads}}" as="s" hint-placeholder-count="2"><button class="pick sls glass" type="button" onClick="{{s.pick}}" '
            'style="display:flex;align-items:center;gap:8px;padding:8px 14px;box-shadow:inset 0 0 0 1px {{s.edge}}">'
            '<span style="width:8px;height:8px;transform:rotate(45deg);background:{{s.dot}}"></span>'
            '<span class="cap" style="font-size:14px;color:{{s.col}}">{{s.name}}</span></button></sc-for>'
            f'<button class="pick sls glass" type="button" onClick="{{{{make}}}}" style="display:flex;align-items:center;gap:6px;padding:8px 14px;'
            f'box-shadow:inset 0 0 0 1px rgba(116,123,173,.45);color:{SUB}">{PLUS}<span class="cap" style="font-size:14px">Make</span></button>'
            f'<button class="pick sls glass" type="button" onClick="{{{{join}}}}" style="padding:8px 14px;box-shadow:inset 0 0 0 1px rgba(116,123,173,.45)">'
            f'<span class="cap" style="font-size:14px;color:{SUB}">Join a friend</span></button></div>')


def domain_chips():
    return ('<div style="display:flex;align-items:center;gap:8px"><span class="lab" style="font-size:12px;margin-right:6px">Domain</span>'
            '<sc-for list="{{domains}}" as="d" hint-placeholder-count="3"><button class="pick sls glass" type="button" aria-pressed="{{d.pressed}}" '
            'onClick="{{d.pick}}" style="display:flex;align-items:center;gap:8px;padding:8px 14px;box-shadow:inset 0 0 0 1px {{d.edge}}">'
            '<span style="width:9px;height:9px;transform:rotate(45deg);background:{{d.dot}}"></span>'
            '<span class="cap" style="font-size:14px;color:{{d.col}}">{{d.name}}</span></button></sc-for></div>')



BOOK = [
    {"name": "Skim Wing", "dom": "Jade", "mine": True, "line": "Yours - three in the squad, one offline",
     "seats": [{"seat": "Leader", "who": "You", "v": "Sparrow", "e": "Charge", "p": "Shrapnel", "a": "Javelin", "on": True, "me": True},
               {"seat": "Rogue One", "who": "Halcyon", "v": "Dolphin", "e": "Space", "p": "Riptide", "a": "Hydra", "on": True},
               {"seat": "Rogue Two", "who": "Mothwing", "v": "Serpent", "e": "Mass", "p": "Scales", "a": "Aegis", "on": False}]},
    {"name": "Breakers", "dom": "Ruby", "mine": False, "owner": "Kestrel", "line": "Kestrel's squad - joined",
     "seats": [{"seat": "Leader", "who": "Kestrel", "v": "Rhino", "e": "Mass", "p": "Tomahawk", "a": "Aftershock", "on": True},
               {"seat": "Rogue One", "who": "You", "v": "Manta", "e": "Charge", "p": "Reaper", "a": "Javelin", "on": True, "me": True},
               {"seat": "Rogue Two", "who": "Tern", "v": "Serpent", "e": "Space", "p": "Scales", "a": "Hydra", "on": False}]},
]
ARTS = [("Javelin", "Throws the payload down your heading. It cracks every rival super-shield along its line and bursts where it stops."),
        ("Hydra", "Splits on release into heads that fan out across your heading, each carrying part of the payload, and each cracks where it lands."),
        ("Aftershock", "Lands down your heading and cracks, then cracks again in the same place a few seconds later, catching whatever was rebuilt."),
        ("Aegis", "Release the payload beside your Ark and it super-shields the Ark's hull for a while, guarding everything aboard.")]
ELEMS = [("Space", 7), ("Charge", 4), ("Mass", 5), ("Time", 3)]


TABS = [("Converter", "converter"), ("Squads", "squads"), ("Artifacts", "artifacts"), ("Decor", "decor")]


def logic():
    bg = {sc_: {s: {"loop": BL[f"{sc_}_{s}_loop"], "convert": BL.get(f"{sc_}_{s}_convert", "")} for s, _ in SETS} for _, sc_ in TABS}
    return (f"const BG = {json.dumps(bg)};\nconst TABS = {json.dumps(TABS)};\nconst SETS = {json.dumps(SETS)};\nconst DOMC = {json.dumps(DOMC)};\n"
            f"const BOOK = {json.dumps(BOOK)};\nconst ARTS = {json.dumps(ARTS)};\nconst ELEMS = {json.dumps(ELEMS)};\n" + r"""
class Component extends DCLogic {
 constructor(props) { super(props); this.state = { tab: 1, set: 0, sq: 0, doms: BOOK.map((b) => b.dom), sel: 0, toast: '', running: false, run: 0,
   counts: ELEMS.map((x) => x[1]), omni: 0, art: 1, open: {} }; this.t = null; this.c = null; }
 componentWillUnmount() { if (this.t) clearTimeout(this.t); if (this.c) clearTimeout(this.c); }
 say(x) { if (this.t) clearTimeout(this.t); this.setState({ toast: x }); this.t = setTimeout(() => this.setState({ toast: '' }), 2800); }
 renderVals() {
  const st = this.state, q = BOOK[st.sq], dom = st.doms[st.sq], dc = DOMC[dom];
  const layers = [];
  const B = BG[TABS[st.tab][1]];
  SETS.forEach((s, i) => {
   layers.push({ src: B[s[0]].loop, op: i === st.set && !st.running ? 1 : 0, alt: i === st.set && !st.running ? 'Inside the Ark, live, ' + s[1] : '' });
   if (B[s[0]].convert) layers.push({ src: i === st.set && st.running ? B[s[0]].convert + '?run=' + st.run : B[s[0]].convert,
     op: i === st.set && st.running ? 1 : 0, alt: i === st.set && st.running ? 'Converting: the crystals fly into the reactor core' : '' });
  });
  const sets = SETS.map((s, i) => ({ name: s[1], on: i === st.set, edge: i === st.set ? '#00D4FF' : 'rgba(116,123,173,.45)',
   col: i === st.set ? '#E6E9FF' : '#9AA0CC', pressed: i === st.set ? 'true' : 'false', pick: () => this.setState({ set: i }) }));
  const seats = q.seats.map((s, i) => {
   const on = i === st.sel, isOpen = !!st.open[st.sq + ':' + i];
   return { num: '0' + (i + 1), seat: s.seat, who: isOpen ? 'Open seat' : s.who, v: isOpen ? '-' : s.v, e: isOpen ? '-' : s.e,
    p: isOpen ? '-' : s.p, a: isOpen ? '-' : s.a, status: isOpen ? 'Open - anyone in the squad can take it' : (s.on ? 'Online' : 'Offline - vessel powered down'),
    dot: isOpen ? '#5C5F70' : (s.on ? '#E6E9FF' : '#5C5F70'), dim: s.on || isOpen ? '1' : '.62',
    ring: on ? '#00D4FF' : 'rgba(116,123,173,.45)', ringW: on ? '2px' : '1px', mark: on ? '#00D4FF' : 'transparent',
    me: !!s.me && !isOpen, offline: !s.on && !s.me && !isOpen && q.mine, aria: s.seat + ': ' + (isOpen ? 'open' : s.who),
    pick: () => this.setState({ sel: i }),
    change: () => this.say('Pick a vessel - your pad in the hangar updates to it.'),
    openSeat: () => { const o = Object.assign({}, st.open); o[st.sq + ':' + i] = true; this.setState({ open: o, sel: i });
      this.say(s.who + ' is offline: the seat is open. The pad stands empty until someone takes it.'); } };
  });
  const squads = BOOK.map((b, i) => ({ name: b.name, dot: DOMC[st.doms[i]], on: i === st.sq,
   edge: i === st.sq ? '#00D4FF' : 'rgba(116,123,173,.45)', col: i === st.sq ? '#E6E9FF' : '#9AA0CC',
   pick: () => this.setState({ sq: i, sel: 0, toast: '' }) }));
  const domains = ['Jade', 'Ruby', 'Gold'].map((k) => { const on = k === dom; return { name: k, dot: DOMC[k],
   edge: on ? '#00D4FF' : 'rgba(116,123,173,.45)', col: on ? '#E6E9FF' : '#9AA0CC', pressed: on ? 'true' : 'false',
   pick: () => { if (on) return; if (!q.mine) { this.say('Only ' + q.owner + ' changes the squad\'s domain.'); return; }
    const d = st.doms.slice(); d[st.sq] = k; this.setState({ doms: d }); this.say('The hangar\'s trim and the pads relight ' + k + '.'); } }; });
  const elems = ELEMS.map((x, i) => ({ name: x[0], n: String(st.counts[i]), low: st.counts[i] === 0 }));
  const canConvert = st.counts.every((n) => n > 0) && !st.running;
  const arts = ARTS.map((a, i) => ({ name: a[0], on: i === st.art, ring: i === st.art ? '#00D4FF' : 'rgba(116,123,173,.35)',
   col: i === st.art ? '#E6E9FF' : '#9AA0CC', pick: () => this.setState({ art: i }) }));
  const go = (k) => { if (st.running) return; this.setState({ tab: (k + 4) % 4, toast: '' }); };
  const tabs = TABS.map((x, i) => ({ name: x[0], on: i === st.tab, col: i === st.tab ? '#E6E9FF' : (x[0] === 'Decor' ? '#5C5F70' : '#747BAD'),
   mark: i === st.tab ? '#E6E9FF' : 'transparent', locked: x[0] === 'Decor', pick: () => go(i) }));
  return { tabs, prevTab: () => go(st.tab - 1), nextTab: () => go(st.tab + 1),
   isConverter: st.tab === 0, isSquads: st.tab === 1, isArtifacts: st.tab === 2, isDecor: st.tab === 3,
   layers, sets, seats, squads, domains, dom, dc, name: q.name, line: q.line,
   kicker: q.mine ? 'Your squad' : q.owner + '\'s squad', toast: st.toast, hasToast: st.toast !== '',
   elems, omni: String(st.omni), running: st.running, idle: !st.running, convLabel: st.running ? 'Converting...' : 'Convert',
   convClass: canConvert ? 'btn commit sl' : 'btn off sl',
   convert: () => { if (st.running) return; if (!st.counts.every((n) => n > 0)) { this.say('You need one of each element.'); return; }
    this.setState({ running: true, run: st.run + 1 });
    if (this.c) clearTimeout(this.c);
    this.c = setTimeout(() => { const c = this.state.counts.map((n) => n - 1);
     this.setState({ running: false, counts: c, omni: this.state.omni + 1 }); this.say('One Omni crystal made.'); }, 4900); },
   arts, artName: ARTS[st.art][0], artText: ARTS[st.art][1], artHeld: st.art === 1 ? 'Equipped by Halcyon' : (st.art === 0 ? 'Equipped by you' : 'In the cabinet'),
   edit: () => this.say('Edit opens the seat picker.'), make: () => this.say('Make squad starts a new one.'),
   join: () => this.say('Join a friend\'s squad.'), mission: () => this.say('Mission is locked: finish Episode 1 first.'),
   arena: () => this.say('Taking ' + q.name + ' into the Arena.'), equip: () => this.say(ARTS[st.art][0] + ' goes to the seat you pick.'),
   locked: () => this.say('Not open yet.') };
 }
}
""")


def board(title, body):
    root = (f'<div style="width: {W}px; height: {H}px; position: relative; overflow: hidden; background: {VDARK}; color: {LIGHT}; '
            f"font-family: 'Aldrich', 'Chakra Petch', sans-serif\">\n{body}\n</div>\n")
    return HEAD.format(title=e(title)) + root + TAIL.format(w=W, h=H, logic=LOGIC)


def backdrop():
    return ('<sc-for list="{{layers}}" as="l" hint-placeholder-count="3"><img class="bg" src="{{l.src}}" alt="{{l.alt}}" '
            'style="opacity:{{l.op}}"></sc-for>'
            '<div class="abs" style="inset:0;background:linear-gradient(180deg,rgba(0,1,10,.72) 0%,rgba(0,1,10,0) 22%,'
            'rgba(0,1,10,0) 70%,rgba(0,1,10,.7) 100%);pointer-events:none"></div>')


def tabrow():
    tab = ('<sc-for list="{{tabs}}" as="t" hint-placeholder-count="4"><button class="pick" type="button" onClick="{{t.pick}}" '
           'style="position:relative;display:flex;align-items:center;gap:8px;color:{{t.col}}">'
           f'<sc-if value="{{{{t.locked}}}}" hint-placeholder-val="{{{{ false }}}}">{LOCK}</sc-if>'
           '<span class="cap" style="font-size:16px;color:{{t.col}}">{{t.name}}</span>'
           '<span class="abs" style="left:0;right:0;bottom:-15px;height:2px;background:{{t.mark}}"></span></button></sc-for>')
    return (f'<div class="abs" style="left:40px;right:40px;top:96px;height:52px;display:flex;align-items:center;gap:30px;'
            f'border-bottom:1px solid rgba(116,123,173,.35);z-index:4">'
            f'<button class="pick" type="button" onClick="{{{{prevTab}}}}" aria-label="Previous tab">{kc("LB")}</button>{tab}'
            f'<button class="pick" type="button" onClick="{{{{nextTab}}}}" aria-label="Next tab">{kc("RB")}</button></div>')


def set_switch():
    """The mockup's own control (not the game's UI): which colour set the backdrop is rendered in."""
    return ('<div class="abs" style="left:40px;top:160px;display:flex;align-items:center;gap:8px;padding:6px 10px;'
            'border:1px dashed rgba(154,160,204,.5);z-index:6"><span class="lab" style="font-size:11px">Mockup - colour set</span>'
            '<sc-for list="{{sets}}" as="s" hint-placeholder-count="3"><button class="pick sls glass" type="button" aria-pressed="{{s.pressed}}" '
            'onClick="{{s.pick}}" style="padding:6px 12px;box-shadow:inset 0 0 0 1px {{s.edge}}">'
            '<span class="cap" style="font-size:13px;color:{{s.col}}">{{s.name}}</span></button></sc-for></div>')


def frame(body):
    return body


def seat_card(width=470):
    def r(k, v):
        return (f'<span style="display:flex;justify-content:space-between;align-items:baseline;gap:12px;padding:5px 0;'
                f'border-top:1px solid rgba(116,123,173,.25)"><span class="lab" style="font-size:11px">{k}</span>'
                f'<span class="ell" style="font-size:14px">{v}</span></span>').replace("padding:5px 0", "padding:3px 0")
    return (f'<div role="group" aria-label="{{{{s.aria}}}}" class="sl glass" style="position:relative;width:{width}px;box-sizing:border-box;'
            f'padding:11px 14px 10px 18px;display:flex;flex-direction:column;gap:3px;opacity:{{{{s.dim}}}};box-shadow:inset 0 0 0 {{{{s.ringW}}}} {{{{s.ring}}}}">'
            f'<span class="abs" style="left:0;top:14px;bottom:14px;width:3px;background:{{{{dc}}}}"></span>'
            f'<button class="pick" type="button" onClick="{{{{s.pick}}}}" style="display:flex;flex-direction:column;gap:5px;width:100%">'
            f'<span style="display:flex;justify-content:space-between;width:100%"><span class="lab num" style="font-size:12px">{{{{s.num}}}} {{{{s.seat}}}}</span>'
            f'<span style="width:8px;height:8px;transform:rotate(45deg);background:{{{{s.mark}}}}"></span></span>'
            f'<span class="ell" style="font-size:19px;max-width:100%">{{{{s.who}}}}</span>'
            f'<span style="display:flex;align-items:center;gap:8px"><span style="width:7px;height:7px;border-radius:50%;background:{{{{s.dot}}}}"></span>'
            f'<span style="font-size:13px;color:{SUB}">{{{{s.status}}}}</span></span></button>'
            f'<span style="display:flex;flex-direction:column;margin-top:2px">{r("Vessel", "{{s.v}}")}{r("Element", "{{s.e}}")}{r("Pilot", "{{s.p}}")}{r("Artifact", "{{s.a}}")}</span>'
            f'<sc-if value="{{{{s.me}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="display:flex;gap:8px;margin-top:4px">'
            f'<button class="btn sm def sls" type="button" onClick="{{{{s.change}}}}">{kc("Y")}<span>Change vessel</span></button></span></sc-if>'
            f'<sc-if value="{{{{s.offline}}}}" hint-placeholder-val="{{{{ false }}}}"><span style="display:flex;gap:8px;margin-top:6px">'
            f'<button class="btn sm cta sls" type="button" onClick="{{{{s.openSeat}}}}">{kc("Y")}<span>Open the seat</span></button></span></sc-if>'
            f'</div>')


def bottom(right, left=""):
    return (f'<div class="abs" style="left:40px;right:40px;top:1004px;display:flex;justify-content:space-between;align-items:center">'
            f'<div style="display:flex;gap:10px">{left}</div>{right}</div>')


def take_into():
    return (f'<div style="display:flex;align-items:center;gap:10px"><span class="lab" style="font-size:12px">Take this squad into</span>'
            f'<button class="btn off sl" type="button" onClick="{{{{mission}}}}">{LOCK}<span>Mission</span></button>'
            f'<button class="btn commit sl" type="button" onClick="{{{{arena}}}}">{kc("A")}<span>Arena</span></button></div>')


def screen_squads():
    cards = ('<div class="abs" style="right:40px;top:262px;width:470px;display:flex;flex-direction:column;gap:8px">'
             '<sc-for list="{{seats}}" as="s" hint-placeholder-count="3">' + seat_card() + '</sc-for></div>')
    body = (f'<div class="abs" style="right:40px;top:172px;width:470px">{head(34)}</div>'
            f'<div class="abs" style="left:40px;top:214px;display:flex;flex-direction:column;gap:10px">{squads_row()}{domain_chips()}</div>'
            + bottom(take_into(), f'<button class="btn sm def sls" type="button" onClick="{{{{edit}}}}">{kc("X")}<span>Edit squad</span></button>'))
    return frame(body + cards)


def screen_converter():
    rows_ = ('<sc-for list="{{elems}}" as="x" hint-placeholder-count="4"><span style="display:flex;justify-content:space-between;'
             'align-items:baseline;padding:9px 0;border-top:1px solid rgba(116,123,173,.25)"><span class="cap" style="font-size:15px">{{x.name}}</span>'
             '<span class="num" style="font-size:22px">{{x.n}}</span></span></sc-for>')
    panel = (f'<div class="abs glass sl" style="left:40px;top:300px;width:420px;box-sizing:border-box;padding:20px 22px;display:flex;flex-direction:column;gap:10px">'
             f'<span class="lab">Elemental converter</span><span style="font-size:30px">One of each makes an Omni</span>'
             f'<span style="font-size:15px;line-height:1.45;color:{SUB}">Your crystals wait in their cradles round the reactor. Convert '
             f'sends one of each into the core, in order, and the Omni forms there.</span><span style="display:flex;flex-direction:column;margin-top:6px">{rows_}</span>'
             f'<span style="display:flex;justify-content:space-between;align-items:baseline;padding:10px 0 0;border-top:1px solid rgba(116,123,173,.5)">'
             f'<span class="cap" style="font-size:15px">Omni made</span><span class="num" style="font-size:26px">{{{{omni}}}}</span></span></div>')
    conv = (f'<button class="{{{{convClass}}}}" type="button" onClick="{{{{convert}}}}" style="min-width:260px">{kc("A")}<span>{{{{convLabel}}}}</span></button>')
    return frame(panel + bottom(conv))


def screen_artifacts():
    lst = ('<sc-for list="{{arts}}" as="a" hint-placeholder-count="4"><button class="pick sls" type="button" onClick="{{a.pick}}" '
           'style="display:flex;align-items:center;gap:12px;padding:12px 14px;box-shadow:inset 0 0 0 1px {{a.ring}};background:rgba(0,4,31,.5)">'
           '<span class="cap" style="font-size:16px;color:{{a.col}}">{{a.name}}</span></button></sc-for>')
    panel = (f'<div class="abs glass sl" style="left:40px;top:300px;width:460px;box-sizing:border-box;padding:20px 22px;display:flex;flex-direction:column;gap:10px">'
             f'<span class="lab">Artifacts - 4 of 12 found</span><div style="display:flex;flex-direction:column;gap:8px">{lst}</div>'
             f'<span style="font-size:14px;color:{SUB}">Eight cells wait for the artifacts still to be found.</span></div>')
    detail = (f'<div class="abs glass sl" style="right:40px;top:640px;width:520px;box-sizing:border-box;padding:18px 22px;display:flex;flex-direction:column;gap:8px">'
              f'<span class="lab">{{{{artHeld}}}}</span><span style="font-size:30px">{{{{artName}}}}</span>'
              f'<span style="font-size:16px;line-height:1.5;color:#C9CDEA">{{{{artText}}}}</span>'
              f'<span style="display:flex;gap:8px;margin-top:6px"><button class="btn sm def sls" type="button" onClick="{{{{equip}}}}">{kc("A")}<span>Give to a seat</span></button></span></div>')
    return frame(panel + detail + bottom(""))


def screen_decor():
    tiles = "".join(f'<div class="sls" style="display:flex;align-items:center;gap:12px;padding:14px 16px;background:rgba(0,4,31,.55);'
                    f'box-shadow:inset 0 0 0 1px {NEUT_D};color:{INACT}">{LOCK}<span class="cap" style="font-size:16px">{t}</span></div>'
                    for t in ("Banner", "Outer hull", "Inner hull"))
    panel = (f'<div class="abs glass sl" style="left:40px;top:300px;width:440px;box-sizing:border-box;padding:20px 22px;display:flex;flex-direction:column;gap:12px">'
             f'<span class="lab">Decor - locked</span><span style="font-size:30px">Not open yet</span>'
             f'<span style="font-size:15px;line-height:1.45;color:{SUB}">The Ark&#39;s banner and its outer and inner hull skins. The bay stays sealed for now.</span>'
             f'<div style="display:flex;flex-direction:column;gap:8px;margin-top:4px">{tiles}</div></div>')
    return frame(panel + bottom(""))


SCREENS = [("isConverter", screen_converter), ("isSquads", screen_squads), ("isArtifacts", screen_artifacts), ("isDecor", screen_decor)]


def main():
    global LOGIC
    LOGIC = logic()
    bodies = "".join(f'<sc-if value="{{{{{flag}}}}}" hint-placeholder-val="{{{{ {"true" if flag == "isSquads" else "false"} }}}}">{fn()}</sc-if>'
                     for flag, fn in SCREENS)
    body = backdrop() + nav() + tabrow() + set_switch() + bodies + toast()
    out = board(f"Rev {REV} - the Ark screen inside the Ark, live", body)
    (ROOT / f"R{REV}Ark.dc.html").write_text(out, encoding="utf-8")
    print(f"R{REV}Ark.dc.html", len(out))


if __name__ == "__main__":
    main()
