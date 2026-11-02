#!/usr/bin/env python3
"""Rev 47 boards: the Ark's Squads tab reworked - the vessels in their REAL domain colours (the ship material's domain
slot, not a tint over the whole card), the domain change as the nanobot swap or the Meerkat's wormhole warp, and the Ark
itself as a turning 3D model inside the screen. Six options A-F, plus Compare and Notes. Writes into <root>/project/.

    python3 Docs/MenuRedesignB/rounds/r47_ark_squads.py <root> 47 r47_blobs.json

blobs.json maps a render's name (Sparrow_Jade, ark_Ruby, before_Dolphin_Gold, ...) to its /_blob/<id> in the canvas's
asset store; without it the boards carry stand-in ids (for the offline gate only). The renders come from the vessels'
FBX models by r47_render.py: the palette's ship colours (vessel_colors.py), lit; see the Notes board.
The boards are on the Froglet Design Board's Menu Redesign C tab (Docs/MENU_REDESIGN_B.md, Rev 47, section 114)."""
import hashlib
import math
import html
import json
import sys
from pathlib import Path

ROOT = Path(sys.argv[1]) / "project"
REV = int(sys.argv[2])
BLOBS = json.loads(Path(sys.argv[3]).read_text()) if len(sys.argv) > 3 else {}
W, H = 1920, 1080
e = html.escape


def blob(name):
    if name in BLOBS:
        return BLOBS[name]
    return "/_blob/" + hashlib.md5(name.encode()).hexdigest()


NAV = [("CODEX", "/_blob/ec690d6b90d6fcdbc430b9bbaacf28a9"), ("HANGAR", "/_blob/34f5a50f1275fef42808fe9df3bf6af5"),
       ("HOME", "/_blob/a5bd4d8d1de7b494567fc3de75525e1e"), ("ARK", "/_blob/e5b050c5a676193c650e94d873ad40e2"),
       ("PORT", "/_blob/d0e850bc927559f1a499868d26f2a3de")]
PILOT_ART = {"Shrapnel": "/_blob/70fab52e055a8baf2eca47881dc75789", "Riptide": "/_blob/10e39715b356ad76fe64dcc5234b824f",
             "Trace": "/_blob/b2c216ffe8fce1e71dc01d6af16ef5bf", "Tomahawk": "/_blob/d396f4042ff5e564acc49e446c1f625b",
             "Reaper": "/_blob/9cfbb306f11d2d9f5b916f2a8f37aa1b", "Scales": "/_blob/97d8ca1b82cba9b7a6e19dd7de4590f4"}
HULLS = ["Sparrow", "Dolphin", "Squirrel", "Rhino", "Manta", "Serpent"]
DOMS = ["Jade", "Ruby", "Gold"]
# Style foundation v0.3.3: the domains' UI colours (Docs/STYLE_FOUNDATION.md) - the chips, the edit line, the bots
DOM = {"Jade": ("#00D4FF", "#80EAFF"), "Ruby": ("#A600FF", "#D280FF"), "Gold": ("#FFAE00", "#FFD780")}
LIGHT, INACT, CTA, NEUT_L, NEUT, NEUT_D, VDARK, BLACK, SUB = ("#E6E9FF", "#5C5F70", "#99FF80", "#747BAD", "#434C89",
                                                             "#222645", "#00041F", "#00010A", "#9AA0CC")

BOOK = [
    {"name": "Skim Wing", "dom": "Jade", "mine": True, "line": "Yours - every seat filled",
     "slots": [["Leader", "You", "Sparrow", "Shrapnel", "Charge", "Extra Bay"],
               ["Rogue One", "Halcyon", "Dolphin", "Riptide", "Space", "Seed Pod"],
               ["Rogue Two", "AI wingmate", "Squirrel", "Trace", "Time", "No artifact"]]},
    {"name": "Breakers", "dom": "Ruby", "mine": False, "owner": "Kestrel", "line": "Kestrel's squad - joined",
     "slots": [["Leader", "Kestrel", "Rhino", "Tomahawk", "Mass", "Long Edge"],
               ["Rogue One", "You", "Manta", "Reaper", "Charge", "Tailwind"],
               ["Rogue Two", "AI wingmate", "Serpent", "Scales", "Space", "No artifact"]]},
]

OPTS = {
    "A": ("Escort", "nano", "The Ark centre stage, its three escorts flying round it; the seats along the bottom"),
    "B": ("Hangar", "warp", "The Ark large on the right; the three seats stacked on the left as they leave its hangar"),
    "C": ("The deck, kept", "nano", "The built deck of three cards unchanged; the Ark turns above it"),
    "D": ("Bays", "warp", "The Ark over a row of three launch bays, one escort in each"),
    "E": ("Dossier", "nano", "The Ark on the left half; the squad as three rows on the right"),
    "F": ("Cinema", "warp", "The Ark fills the screen; the squad floats over it in thin panels"),
}
FX_NAME = {"nano": "nanobot swap", "warp": "Meerkat warp"}

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
.lab{{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.18em;text-transform:uppercase;color:#8C93C7}}
.cap{{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}}
.num{{font-variant-numeric:tabular-nums}}
.abs{{position:absolute}}
.sl{{clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}}
.sls{{clip-path:polygon(8px 0,100% 0,100% calc(100% - 8px),calc(100% - 8px) 100%,0 100%,0 8px)}}
.ell{{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}}
.kc{{display:inline-flex;align-items:center;justify-content:center;min-width:26px;height:22px;padding:0 6px;box-sizing:border-box;border:1px solid #434C89;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:12px;color:#C9CDEA}}
.btn{{box-sizing:border-box;display:flex;align-items:center;justify-content:center;gap:10px;height:48px;padding:0 18px;white-space:nowrap;border:0;cursor:pointer;font-family:'Chakra Petch',sans-serif;font-weight:700;font-size:15px;letter-spacing:.14em;text-transform:uppercase}}
.cta{{background:#99FF80;color:#00010A}}
.sec{{background:#2B3266;color:#E6E9FF}}
.gho{{background:transparent;color:#C9CDEA;box-shadow:inset 0 0 0 1px #434C89}}
.btn .kc{{border-color:currentColor;color:inherit;opacity:.8}}
.btn:focus-visible,.tab:focus-visible,.pick:focus-visible{{outline:2px solid #3DB2FF;outline-offset:2px}}
.tab,.pick{{background:none;border:0;padding:0;cursor:pointer;color:inherit;font:inherit;text-align:left}}
.panel{{background:#080C28;box-shadow:inset 0 0 0 1px #2B3266}}
.glass{{background:rgba(0,4,31,.72);box-shadow:inset 0 0 0 1px #2B3266}}
.hullbox{{position:relative}}
.hullbox>img{{position:absolute;inset:0;width:100%;height:100%;object-fit:contain}}
.bot{{position:absolute;transform:rotate(45deg);opacity:0;pointer-events:none}}
.ring{{position:absolute;left:50%;top:50%;border-radius:50%;border:2px solid;opacity:0;pointer-events:none;transform:translate(-50%,-50%) scale(0)}}
.throat{{position:absolute;left:50%;top:50%;border-radius:50%;opacity:0;pointer-events:none;transform:translate(-50%,-50%);background:radial-gradient(circle,#00010A 0%,rgba(0,1,10,.85) 38%,rgba(0,1,10,0) 70%)}}
@keyframes r45BotA{{0%{{opacity:0;transform:translate(0,0) rotate(45deg) scale(.4)}}22%{{opacity:1;transform:translate(0,0) rotate(45deg) scale(1)}}55%{{opacity:1;transform:translate(var(--dx),var(--dy)) rotate(225deg) scale(1.3)}}85%{{opacity:1;transform:translate(0,0) rotate(405deg) scale(1)}}100%{{opacity:0;transform:translate(0,0) rotate(405deg) scale(.4)}}}}
@keyframes r45BotB{{0%{{opacity:0;transform:translate(0,0) rotate(45deg) scale(.4)}}22%{{opacity:1;transform:translate(0,0) rotate(45deg) scale(1)}}55%{{opacity:1;transform:translate(var(--dx),var(--dy)) rotate(225deg) scale(1.3)}}85%{{opacity:1;transform:translate(0,0) rotate(405deg) scale(1)}}100%{{opacity:0;transform:translate(0,0) rotate(405deg) scale(.4)}}}}
@keyframes r45OutA{{0%{{opacity:1;filter:blur(0)}}30%{{opacity:0;filter:blur(6px)}}100%{{opacity:0;filter:blur(6px)}}}}
@keyframes r45OutB{{0%{{opacity:1;filter:blur(0)}}30%{{opacity:0;filter:blur(6px)}}100%{{opacity:0;filter:blur(6px)}}}}
@keyframes r45InA{{0%,62%{{opacity:0;filter:blur(6px)}}100%{{opacity:1;filter:blur(0)}}}}
@keyframes r45InB{{0%,62%{{opacity:0;filter:blur(6px)}}100%{{opacity:1;filter:blur(0)}}}}
@keyframes r45SinkA{{0%{{opacity:1;transform:scale(1) rotate(0) skewX(0);filter:blur(0)}}45%{{opacity:0;transform:scale(.06) rotate(170deg) skewX(22deg);filter:blur(5px)}}100%{{opacity:0;transform:scale(.06) rotate(170deg);filter:blur(5px)}}}}
@keyframes r45SinkB{{0%{{opacity:1;transform:scale(1) rotate(0) skewX(0);filter:blur(0)}}45%{{opacity:0;transform:scale(.06) rotate(170deg) skewX(22deg);filter:blur(5px)}}100%{{opacity:0;transform:scale(.06) rotate(170deg);filter:blur(5px)}}}}
@keyframes r45RiseA{{0%,52%{{opacity:0;transform:scale(.06) rotate(-170deg) skewX(-22deg);filter:blur(5px)}}100%{{opacity:1;transform:scale(1) rotate(0) skewX(0);filter:blur(0)}}}}
@keyframes r45RiseB{{0%,52%{{opacity:0;transform:scale(.06) rotate(-170deg) skewX(-22deg);filter:blur(5px)}}100%{{opacity:1;transform:scale(1) rotate(0) skewX(0);filter:blur(0)}}}}
@keyframes r45RingA{{0%{{opacity:0;transform:translate(-50%,-50%) scale(0)}}20%{{opacity:1;transform:translate(-50%,-50%) scale(1)}}72%{{opacity:1;transform:translate(-50%,-50%) scale(1.04)}}100%{{opacity:0;transform:translate(-50%,-50%) scale(1.7)}}}}
@keyframes r45RingB{{0%{{opacity:0;transform:translate(-50%,-50%) scale(0)}}20%{{opacity:1;transform:translate(-50%,-50%) scale(1)}}72%{{opacity:1;transform:translate(-50%,-50%) scale(1.04)}}100%{{opacity:0;transform:translate(-50%,-50%) scale(1.7)}}}}
@keyframes r45ThroatA{{0%{{opacity:0}}22%,70%{{opacity:1}}100%{{opacity:0}}}}
@keyframes r45ThroatB{{0%{{opacity:0}}22%,70%{{opacity:1}}100%{{opacity:0}}}}
@keyframes r13Sea{{from{{transform:translateX(0)}}to{{transform:translateX(-480px)}}}}
@keyframes r45Shake{{0%,100%{{transform:translateX(0)}}20%{{transform:translateX(-6px)}}40%{{transform:translateX(6px)}}60%{{transform:translateX(-4px)}}80%{{transform:translateX(4px)}}}}
</style>
</helmet>
"""

LOCK = ('<svg width="12" height="14" viewBox="0 0 14 16" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true">'
        '<path d="M3 7V5a4 4 0 018 0v2"></path><path d="M1.5 7h11v8h-11z"></path></svg>')


def kc(t):
    return f'<span class="kc">{e(t)}</span>'


# ------------------------------------------------------------------------------------------------ the shared logic

def logic(fx):
    hull = {h: {d: blob(f"{h}_{d}") for d in DOMS} for h in HULLS}
    ark = {d: blob(f"ark_{d}") for d in DOMS}
    consts = (f"const FX = {json.dumps(fx)};\n"
              f"const DOMS = {json.dumps({k: list(v) for k, v in DOM.items()})};\n"
              f"const HULL = {json.dumps(hull)};\n"
              f"const ARK = {json.dumps(ark)};\n"
              f"const PILOT = {json.dumps(PILOT_ART)};\n"
              f"const BOOK = {json.dumps(BOOK)};\n")
    return consts + r"""
const BOTS = (() => { let r = 7; const rnd = () => (r = (r * 16807) % 2147483647) / 2147483647; const out = [];
 for (let k = 0; k < 34; k++) { const a = rnd() * Math.PI * 2, rr = Math.sqrt(rnd());
  out.push([(50 + Math.cos(a) * rr * 40).toFixed(1), (50 + Math.sin(a) * rr * 30).toFixed(1), 3 + rnd() * 5, Math.cos(a) * (40 + rnd() * 90), Math.sin(a) * 70 - 30 - rnd() * 40, rnd() > 0.25, Math.round(rnd() * 160)]); }
 return out; })();
const SPIN = 1200;
class Component extends DCLogic {
 constructor(props) {
  super(props);
  this.state = { sel: 0, doms: BOOK.map((b) => b.dom), prev: null, flip: 0, toast: '', shake: '' };
  this.t = null;
 }
 componentWillUnmount() { if (this.t) clearTimeout(this.t); }
 say(x) { if (this.t) clearTimeout(this.t); this.setState({ toast: x, shake: 'r45Shake 360ms ease-in-out both' }); this.t = setTimeout(() => this.setState({ toast: '', shake: '' }), 2400); }
 swap(at, scale, c, playing, ab) {
  // one swapping picture: the old domain's render going, the new one coming, and the effect between them
  const out = { animPrev: 'none', animCur: 'none', bots: [], ringAnim: 'none', throatAnim: 'none', ringColor: c };
  if (!playing) return out;
  if (FX === 'nano') {
   out.animPrev = 'r45Out' + ab + ' ' + SPIN + 'ms ease-in-out ' + at + 'ms both';
   out.animCur = 'r45In' + ab + ' ' + SPIN + 'ms ease-in-out ' + at + 'ms both';
   out.bots = BOTS.map((b) => ({ x: b[0], y: b[1], size: (b[2] * scale).toFixed(1), dx: (b[3] * scale).toFixed(0), dy: (b[4] * scale).toFixed(0),
    c: b[5] ? c : '#E6E9FF', anim: 'r45Bot' + ab + ' ' + SPIN + 'ms cubic-bezier(.45,0,.2,1) ' + (at + b[6]) + 'ms both' }));
  } else {
   out.animPrev = 'r45Sink' + ab + ' ' + SPIN + 'ms cubic-bezier(.5,0,.75,0) ' + at + 'ms both';
   out.animCur = 'r45Rise' + ab + ' ' + SPIN + 'ms cubic-bezier(.25,1,.5,1) ' + at + 'ms both';
   out.ringAnim = 'r45Ring' + ab + ' ' + SPIN + 'ms ease-out ' + at + 'ms both';
   out.throatAnim = 'r45Throat' + ab + ' ' + SPIN + 'ms ease-in-out ' + at + 'ms both';
  }
  return out;
 }
 renderVals() {
  const st = this.state, q = BOOK[st.sel], dom = st.doms[st.sel], c = DOMS[dom][0], cl = DOMS[dom][1];
  const playing = st.prev !== null, was = playing ? st.prev : dom, ab = st.flip % 2 ? 'A' : 'B';
  const ark = Object.assign({ src: ARK[dom], prev: ARK[was], showPrev: playing, alt: 'The Ark in ' + dom + ', turning' }, this.swap(0, 2.2, c, playing, ab));
  const seats = q.slots.map((m, i) => Object.assign({
   seat: m[0], num: '0' + (i + 1), who: m[1], vessel: m[2], pilot: m[3], e: m[4], art: m[5],
   artColor: m[5] === 'No artifact' ? '#5C5F70' : '#E6E9FF', pilotArt: PILOT[m[3]],
   hull: HULL[m[2]][dom], hullPrev: HULL[m[2]][was], showPrev: playing,
   alt: m[2] + ' in ' + dom, edge: i === 0 ? c : '#2B3266', mark: i === 0 ? c : 'transparent',
   pick: () => this.setState({ toast: '' })
  }, this.swap(320 + i * 140, 1, c, playing, ab)));
  const rows = BOOK.map((b, i) => {
   const on = i === st.sel, d = st.doms[i];
   return { name: b.name, dom: d, domColor: DOMS[d][1], line: b.line, edge: on ? DOMS[d][0] : '#2B3266', mark: on ? DOMS[d][0] : 'transparent',
    fill: on ? '#0E1646' : '#080C28', pick: () => this.setState({ sel: i, prev: null, toast: '' }) };
  });
  const domains = Object.keys(DOMS).map((k) => {
   const on = k === dom;
   return { name: k, color: DOMS[k][0], text: on ? '#E6E9FF' : '#9AA0CC', edge: on ? DOMS[k][0] : '#2B3266', fill: on ? '#0E1646' : 'transparent',
    pressed: on ? 'true' : 'false',
    pick: () => {
     if (on) return;
     if (!q.mine) { this.say('Only ' + q.owner + ' changes the squad\'s domain.'); return; }
     const doms = st.doms.slice(); doms[st.sel] = k;
     this.setState({ doms, prev: dom, flip: st.flip + 1, toast: '' });
    } };
  });
  return {
   ark, seats, rows, domains, dom, domColor: c, domLight: cl,
   name: q.name, line: q.line, kicker: q.mine ? 'Your squad' : q.owner + '\'s squad',
   domainNote: q.mine ? 'Press a domain: the Ark changes first, then its escorts' : 'Only ' + q.owner + ' changes the domain',
   toast: st.toast, hasToast: st.toast !== '', shake: st.shake,
   make: () => this.say('Make squad opens the picker (Rev 31).'),
   join: () => this.say('Join a friend\'s squad (Rev 31).'),
   edit: () => this.say('Edit reopens the picker (Rev 31).'),
   mission: () => this.say('Mission is locked: finish the FTUE first.'),
   arena: () => this.say('Take into Arena: the launch panel opens on ' + dom + '.'),
   tabDecor: () => this.say('Not open yet.'),
   replay: () => { if (st.prev === null) { const k = dom; this.setState({ prev: k === 'Jade' ? 'Ruby' : 'Jade', flip: st.flip + 1 }); } else this.setState({ prev: st.prev, flip: st.flip + 1 }); }
  };
 }
}
"""


TAIL = """</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
{logic}</script>
</body>
</html>
"""

STATIC_TAIL = """</x-dc>
<script type="text/x-dc" data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
class Component extends DCLogic {{
renderVals() {{
return {{}};
}}
}}
</script>
</body>
</html>
"""


def board(title, body, fx=None, w=W, h=H):
    root = (f'<div style="width: {w}px; height: {h}px; position: relative; overflow: hidden; background: {VDARK}; color: {LIGHT}; '
            f"font-family: 'Aldrich', 'Chakra Petch', sans-serif\">\n{body}\n</div>\n")
    tail = TAIL.format(w=w, h=h, logic=logic(fx)) if fx else STATIC_TAIL.format(w=w, h=h)
    return HEAD.format(title=e(title)) + root + tail


# ------------------------------------------------------------------------------------------------ pieces

def sea():
    """The menu's ground with its sea lines, taken verbatim from the built deck's board (Rev 31), so every Ark board stands
    on the same deep blue and the same three slow layers of lines."""
    src = (Path(__file__).resolve().parents[1] / "board" / "R31DeckMade.dc.html").read_text(encoding="utf-8")
    i = src.index('<div style="position:absolute;left:0;top:84px;width:1920px;height:996px;overflow:hidden">')
    return src[i:src.index("<nav", i)].strip()


def nav():
    items = []
    for name, icon in NAV:
        on = name == "ARK"
        mark = '<span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:#E6E9FF"></span>' if on else ""
        items.append(f'<span style="position:relative;display:flex;align-items:center;gap:10px;font-family:\'Chakra Petch\';font-weight:600;'
                     f'font-size:17px;letter-spacing:.12em;color:{LIGHT if on else NEUT_L}"><img src="{icon}" alt="" '
                     f'style="width:26px;height:26px;object-fit:contain;opacity:{1 if on else .7}">{name}{mark}</span>')
    return (f'<nav aria-label="Menu screens" class="abs" style="left:0;right:0;top:0;height:84px;background:rgba(0,1,10,.85);'
            f'border-bottom:1px solid {NEUT_D};display:flex;align-items:center;justify-content:center;gap:48px;z-index:5">'
            f'{kc("LT")}{"".join(items)}{kc("RT")}</nav>'
            f'<div class="abs num" style="right:40px;top:30px;z-index:6;display:flex;align-items:center;gap:8px;font-size:20px;color:#FFAE00">'
            f'<span style="width:14px;height:18px;background:#FFAE00;clip-path:polygon(50% 0,100% 35%,50% 100%,0 35%)"></span>1,240</div>')


def tabrow(top=96, left=40):
    def lab(t, on):
        return f'<span class="cap" style="font-size:16px;color:{LIGHT if on else NEUT_L}">{t}</span>'
    tabs = (f'<span>{lab("Converter", False)}</span>'
            f'<span style="position:relative">{lab("Squads", True)}<span class="abs" style="left:0;right:0;bottom:-15px;height:2px;background:{LIGHT}"></span></span>'
            f'<span>{lab("Artifacts", False)}</span>'
            f'<button class="pick" type="button" onClick="{{{{tabDecor}}}}" style="display:flex;align-items:center;gap:8px;color:{INACT}">{LOCK}'
            f'<span class="cap" style="font-size:16px;color:{INACT}">Decor</span></button>')
    return (f'<div class="abs" style="left:{left}px;right:40px;top:{top}px;height:52px;display:flex;align-items:center;gap:30px;'
            f'border-bottom:1px solid {NEUT_D};z-index:4">{kc("LB")}{tabs}{kc("RB")}</div>')


def toast():
    return ('<sc-if value="{{hasToast}}" hint-placeholder-val="{{ false }}"><div class="abs sls" role="status" style="left:50%;top:170px;'
            f'transform:translateX(-50%);padding:10px 18px;background:#0E1646;box-shadow:inset 0 0 0 1px {NEUT};z-index:9;font-size:16px">'
            '{{toast}}</div></sc-if>')


def swap_layers(src_cur, src_prev, prefix, alt, scale_ring=0.9):
    """A picture that swaps domain: the old render going, the new one coming, and the effect (bots or the wormhole)."""
    return (f'<sc-if value="{{{{{prefix}.showPrev}}}}" hint-placeholder-val="{{{{ false }}}}"><img src="{{{{{src_prev}}}}}" alt="" '
            f'style="animation:{{{{{prefix}.animPrev}}}}"></sc-if>'
            f'<span class="throat" style="width:{int(scale_ring * 100)}%;aspect-ratio:1;animation:{{{{{prefix}.throatAnim}}}}"></span>'
            f'<img src="{{{{{src_cur}}}}}" alt="{{{{{prefix}.alt}}}}" style="animation:{{{{{prefix}.animCur}}}}">'
            f'<span class="ring" style="width:{int(scale_ring * 80)}%;aspect-ratio:1;border-color:{{{{{prefix}.ringColor}}}};animation:{{{{{prefix}.ringAnim}}}}"></span>'
            f'<span class="ring" style="width:{int(scale_ring * 60)}%;aspect-ratio:1;border-width:1px;border-color:{{{{{prefix}.ringColor}}}};animation:{{{{{prefix}.ringAnim}}}}"></span>'
            f'<sc-for list="{{{{{prefix}.bots}}}}" as="b" hint-placeholder-count="0"><span class="bot" style="left:{{{{b.x}}}}%;top:{{{{b.y}}}}%;'
            'width:{{b.size}}px;height:{{b.size}}px;background:{{b.c}};--dx:{{b.dx}}px;--dy:{{b.dy}}px;animation:{{b.anim}}"></span></sc-for>')


def ark_view(x, y, w, h, caption=True):
    cap = (f'<div class="abs" style="left:0;bottom:-6px;display:flex;align-items:center;gap:10px">'
           f'<span style="width:9px;height:9px;transform:rotate(45deg);background:{{{{domColor}}}}"></span>'
           f'<span class="lab" style="font-size:12px;color:{SUB}">The Ark - {{{{dom}}}}</span></div>') if caption else ""
    return (f'<figure class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h}px;margin:0">'
            f'<div class="hullbox" style="width:100%;height:100%">{swap_layers("ark.src", "ark.prev", "ark", "", 0.4)}</div>{cap}</figure>')


def hull_view(w, h, extra=""):
    return f'<span class="hullbox" style="display:block;width:{w}px;height:{h}px;{extra}">{swap_layers("s.hull", "s.hullPrev", "s", "")}</span>'


def domain_chips(label=True, size=15):
    lab = f'<span class="lab" style="font-size:12px">Domain</span>' if label else ""
    return (f'<div style="display:flex;align-items:center;gap:10px;animation:{{{{shake}}}}">{lab}'
            '<sc-for list="{{domains}}" as="d" hint-placeholder-count="3"><button class="pick sls" type="button" aria-pressed="{{d.pressed}}" '
            'onClick="{{d.pick}}" style="display:flex;align-items:center;gap:8px;padding:8px 14px;background:{{d.fill}};box-shadow:inset 0 0 0 1px {{d.edge}}">'
            '<span style="width:9px;height:9px;transform:rotate(45deg);background:{{d.color}}"></span>'
            f'<span class="cap" style="font-size:{size}px;color:{{{{d.text}}}}">{{{{d.name}}}}</span></button></sc-for></div>')


def squad_list(x, y, w):
    return (f'<aside aria-label="Your squads" class="abs" style="left:{x}px;top:{y}px;width:{w}px;display:flex;flex-direction:column;gap:10px">'
            f'<div style="display:flex;justify-content:space-between"><span class="lab" style="font-size:12px">Your squads</span>'
            f'<span class="lab num" style="font-size:12px">2 of 4</span></div>'
            '<sc-for list="{{rows}}" as="r" hint-placeholder-count="2"><button class="pick sls" type="button" onClick="{{r.pick}}" '
            'style="position:relative;display:flex;flex-direction:column;gap:4px;padding:12px 14px 12px 18px;background:{{r.fill}};box-shadow:inset 0 0 0 1px {{r.edge}}">'
            '<span class="abs" style="left:0;top:10px;bottom:10px;width:3px;background:{{r.mark}}"></span>'
            '<span style="display:flex;justify-content:space-between;align-items:center"><span style="font-size:18px">{{r.name}}</span>'
            '<span class="cap" style="font-size:12px;color:{{r.domColor}}">{{r.dom}}</span></span>'
            f'<span style="font-size:13px;color:{SUB}">{{{{r.line}}}}</span></button></sc-for>'
            f'<div class="sls" style="padding:14px;box-shadow:inset 0 0 0 1px {NEUT_D};color:{INACT};font-size:14px">Empty slot</div>'
            f'<div class="sls" style="padding:14px;box-shadow:inset 0 0 0 1px {NEUT_D};color:{INACT};font-size:14px">Empty slot</div>'
            f'<button class="btn sec sl" type="button" onClick="{{{{make}}}}" style="margin-top:8px">{kc("X")}<span>Make squad</span></button>'
            f'<button class="btn gho sl" type="button" onClick="{{{{join}}}}">{kc("Y")}<span>Join a friend\'s squad</span></button></aside>')


def squad_switch():
    """The squads as one row of chips (B, E, F): no list column."""
    return ('<div style="display:flex;align-items:center;gap:8px"><span class="lab" style="font-size:12px;margin-right:4px">Squads</span>'
            '<sc-for list="{{rows}}" as="r" hint-placeholder-count="2"><button class="pick sls" type="button" onClick="{{r.pick}}" '
            'style="display:flex;align-items:center;gap:8px;padding:7px 12px;background:{{r.fill}};box-shadow:inset 0 0 0 1px {{r.edge}}">'
            '<span style="width:8px;height:8px;transform:rotate(45deg);background:{{r.domColor}}"></span>'
            '<span class="cap" style="font-size:13px">{{r.name}}</span></button></sc-for>'
            f'<button class="pick sls" type="button" onClick="{{{{make}}}}" style="padding:7px 12px;box-shadow:inset 0 0 0 1px {NEUT_D}">'
            f'<span class="cap" style="font-size:13px;color:{NEUT_L}">+ Make</span></button>'
            f'<button class="pick sls" type="button" onClick="{{{{join}}}}" style="padding:7px 12px;box-shadow:inset 0 0 0 1px {NEUT_D}">'
            f'<span class="cap" style="font-size:13px;color:{NEUT_L}">Join</span></button></div>')


def head(size=40):
    return (f'<div style="display:flex;flex-direction:column;gap:6px"><span class="lab">{{{{kicker}}}}</span>'
            f'<span style="font-size:{size}px;line-height:1.05">{{{{name}}}}</span>'
            f'<span style="font-size:15px;color:{SUB}">{{{{line}}}} - {{{{domainNote}}}}</span></div>')


def take_into(gap=10):
    return (f'<div style="display:flex;align-items:center;gap:{gap}px"><span class="lab" style="font-size:12px">Take this squad into</span>'
            f'<button class="btn gho sl" type="button" onClick="{{{{mission}}}}" style="color:{INACT}">{LOCK}<span>Mission</span></button>'
            f'<button class="btn cta sl" type="button" onClick="{{{{arena}}}}">{kc("A")}<span>Arena</span></button></div>')


def edit_btn():
    return f'<button class="btn gho sl" type="button" onClick="{{{{edit}}}}" style="height:44px">{kc("X")}<span>Edit</span></button>'


def rows4(dense=False):
    fs = 14 if dense else 15
    def r(k, v, col=LIGHT):
        return (f'<span style="display:flex;justify-content:space-between;gap:10px;padding:{4 if dense else 6}px 0;border-top:1px solid {NEUT_D}">'
                f'<span class="lab" style="font-size:10px">{k}</span><span class="ell" style="font-size:{fs}px;color:{col}">{v}</span></span>')
    return (r("Vessel", "{{s.vessel}}") + r("Element", "{{s.e}}") + r("Pilot", "{{s.pilot}}")
            + r("Artifact", "{{s.art}}", "{{s.artColor}}"))


def seat_head(size=19):
    return (f'<span style="display:flex;flex-direction:column;gap:2px"><span class="lab num" style="font-size:11px">{{{{s.num}}}} {{{{s.seat}}}}</span>'
            f'<span style="font-size:{size}px">{{{{s.who}}}}</span></span>')


def seats_for(inner, gap=16, direction="row", style=""):
    return (f'<section aria-label="Squad" style="display:flex;flex-direction:{direction};gap:{gap}px;{style}">'
            f'<sc-for list="{{{{seats}}}}" as="s" hint-placeholder-count="3">{inner}</sc-for></section>')


# ------------------------------------------------------------------------------------------------ the six options

def screen_A():
    # Escort: the Ark centre stage, the three escorts round it, the seats along the bottom
    escort_pos = [(1500, 240, 360, 220), (420, 470, 340, 210), (1520, 520, 340, 200)]
    blocks = []
    for i, (x, y, w, h) in enumerate(escort_pos):
        blocks.append(
            f'<div class="abs" style="left:{x}px;top:{y}px;width:{w}px;height:{h + 40}px;display:flex;flex-direction:column;align-items:center;gap:6px">'
            f'<sc-for list="{{{{seats{i}}}}}" as="s" hint-placeholder-count="1">{hull_view(w, h)}'
            f'<span class="cap" style="font-size:12px;color:{SUB}">{{{{s.seat}}}} - {{{{s.who}}}}</span></sc-for></div>')
    card = (f'<button class="pick sl" type="button" onClick="{{{{s.pick}}}}" style="position:relative;flex:1;padding:14px 18px 12px 22px;background:#080C28;'
            f'box-shadow:inset 0 0 0 1px {{{{s.edge}}}};display:flex;gap:16px;align-items:stretch">'
            f'<span class="abs" style="left:0;top:14px;bottom:14px;width:3px;background:{{{{s.mark}}}}"></span>'
            f'<img src="{{{{s.pilotArt}}}}" alt="{{{{s.pilot}}}}" style="width:84px;height:124px;object-fit:contain;object-position:center bottom">'
            f'<span style="flex:1;display:flex;flex-direction:column;gap:4px">{seat_head()}<span style="height:4px"></span>{rows4(True)}</span></button>')
    body = (sea() + nav() + tabrow() + squad_list(40, 176, 330)
            + f'<div class="abs" style="left:420px;top:172px;width:900px">{head()}</div>'
            + f'<div class="abs" style="right:40px;top:180px">{domain_chips()}</div>'
            + ark_view(700, 250, 820, 508)
            + "".join(blocks)
            + seats_for(card, 16, "row", "position:absolute;left:420px;right:40px;top:786px")
            + f'<div class="abs" style="left:420px;right:40px;top:1000px;display:flex;justify-content:space-between;align-items:center">'
            f'{edit_btn()}{take_into()}</div>' + toast())
    return body


def screen_B():
    # Hangar: the seats stacked on the left, the Ark large on the right
    card = (f'<button class="pick sl" type="button" onClick="{{{{s.pick}}}}" style="position:relative;display:flex;align-items:center;gap:18px;'
            f'padding:12px 18px 12px 22px;height:214px;box-sizing:border-box;background:#080C28;box-shadow:inset 0 0 0 1px {{{{s.edge}}}}">'
            f'<span class="abs" style="left:0;top:14px;bottom:14px;width:3px;background:{{{{s.mark}}}}"></span>'
            f'{hull_view(250, 160)}<span style="flex:1;display:flex;flex-direction:column;gap:4px">{seat_head()}{rows4(True)}</span>'
            f'<img src="{{{{s.pilotArt}}}}" alt="{{{{s.pilot}}}}" style="width:70px;height:120px;object-fit:contain;object-position:center bottom"></button>')
    body = (sea() + nav() + tabrow()
            + f'<div class="abs" style="left:40px;top:170px;width:760px;display:flex;flex-direction:column;gap:14px">{squad_switch()}{head(34)}</div>'
            + seats_for(card, 12, "column", "position:absolute;left:40px;top:330px;width:760px")
            + f'<div class="abs" style="left:840px;top:200px;width:1040px;height:1px;background:{NEUT_D}"></div>'
            + ark_view(860, 230, 1020, 632)
            + f'<div class="abs" style="left:860px;top:890px;width:1020px;display:flex;justify-content:center">{domain_chips()}</div>'
            + f'<div class="abs" style="left:860px;right:40px;top:990px;display:flex;justify-content:space-between;align-items:center">'
            f'{edit_btn()}{take_into()}</div>' + toast())
    return body


def screen_C():
    # The deck, kept: the built three-card deck, the Ark turning above it
    pos = [(0, 950, -18, 0), (1, 600, 0, -6), (2, 1300, 0, 6)]
    cards = []
    for idx, left, dy, rot in pos:
        cards.append(
            f'<div class="abs" style="left:{left}px;top:{622 + dy}px;width:330px;transform:rotate({rot}deg);z-index:{3 if idx == 0 else 2}">'
            f'<sc-for list="{{{{seats{idx}}}}}" as="s" hint-placeholder-count="1"><button class="pick sl" type="button" onClick="{{{{s.pick}}}}" '
            f'style="position:relative;width:330px;padding:14px;box-sizing:border-box;background:#080C28;box-shadow:inset 0 0 0 1px {{{{s.edge}}}};'
            f'display:flex;flex-direction:column;gap:6px">'
            f'<span style="display:flex;justify-content:space-between"><span class="lab" style="font-size:11px">{{{{s.seat}}}}</span>'
            f'<span class="lab num" style="font-size:11px">{{{{s.num}}}} / 03</span></span>'
            f'{hull_view(300, 130)}<span style="font-size:19px">{{{{s.who}}}}</span>{rows4(True)}</button>'
            f'<img src="{{{{s.pilotArt}}}}" alt="{{{{s.pilot}}}}" class="abs" style="left:-46px;top:170px;width:80px;height:150px;object-fit:contain;object-position:center bottom">'
            f'</sc-for></div>')
    body = (sea() + nav() + tabrow() + squad_list(40, 176, 330)
            + f'<div class="abs" style="left:420px;top:172px;width:350px">{head(36)}</div>'
            + f'<div class="abs" style="right:40px;top:180px">{domain_chips()}</div>'
            + ark_view(790, 150, 700, 434)
            + "".join(cards)
            + f'<div class="abs" style="left:420px;right:40px;top:1000px;display:flex;justify-content:space-between;align-items:center">'
            f'{edit_btn()}{take_into()}</div>' + toast())
    return body


def screen_D():
    # Bays: the Ark over three launch bays, an escort in each
    bay = (f'<button class="pick" type="button" onClick="{{{{s.pick}}}}" style="position:relative;flex:1;height:340px;padding:0 18px 12px;'
           f'box-sizing:border-box;display:flex;flex-direction:column;gap:6px;background:linear-gradient(180deg,rgba(8,12,40,0) 0%,#080C28 46%);'
           f'border-left:1px solid {NEUT_D};border-right:1px solid {NEUT_D}">'
           f'<span class="abs" style="left:0;right:0;bottom:0;height:2px;background:{{{{s.mark}}}}"></span>'
           f'<span class="abs" style="left:18px;right:18px;top:108px;height:1px;background:{NEUT}"></span>'
           f'<span style="display:flex;justify-content:center">{hull_view(260, 110)}</span>'
           f'<span style="display:flex;align-items:flex-end;gap:12px"><img src="{{{{s.pilotArt}}}}" alt="{{{{s.pilot}}}}" '
           f'style="width:44px;height:64px;object-fit:contain;object-position:center bottom">{seat_head(18)}</span>{rows4(True)}</button>')
    body = (sea() + nav() + tabrow() + squad_list(40, 176, 330)
            + f'<div class="abs" style="left:420px;top:172px;width:520px">{head(36)}</div>'
            + f'<div class="abs" style="right:40px;top:180px">{domain_chips()}</div>'
            + ark_view(880, 176, 700, 434, caption=False)
            + f'<div class="abs" style="left:420px;right:40px;top:622px;display:flex;align-items:center;gap:12px">'
            f'<span class="lab" style="font-size:11px;color:{SUB}">The Ark\'s bays</span><span style="flex:1;height:1px;background:{NEUT_D}"></span></div>'
            + seats_for(bay, 0, "row", "position:absolute;left:420px;right:40px;top:640px")
            + f'<div class="abs" style="left:420px;right:40px;top:1008px;display:flex;justify-content:space-between;align-items:center">'
            f'{edit_btn()}{take_into()}</div>' + toast())
    return body


def screen_E():
    # Dossier: the Ark on the left half, the squad as three rows on the right
    row = (f'<button class="pick sl" type="button" onClick="{{{{s.pick}}}}" style="position:relative;display:grid;'
           f'grid-template-columns:200px 1fr 1fr;gap:18px;align-items:center;padding:12px 18px 12px 22px;height:190px;box-sizing:border-box;'
           f'background:#080C28;box-shadow:inset 0 0 0 1px {{{{s.edge}}}}">'
           f'<span class="abs" style="left:0;top:14px;bottom:14px;width:3px;background:{{{{s.mark}}}}"></span>'
           f'{hull_view(200, 130)}'
           f'<span style="display:flex;gap:12px;align-items:flex-end"><img src="{{{{s.pilotArt}}}}" alt="{{{{s.pilot}}}}" '
           f'style="width:64px;height:110px;object-fit:contain;object-position:center bottom">{seat_head(20)}</span>'
           f'<span style="display:flex;flex-direction:column">{rows4(True)}</span></button>')
    body = (sea() + nav() + tabrow()
            + f'<div class="abs" style="left:40px;top:176px;width:960px;height:860px;background:radial-gradient(ellipse 60% 50% at 50% 50%,'
            f'rgba(61,140,255,.10),rgba(61,140,255,0) 70%)"></div>'
            + ark_view(40, 250, 960, 595)
            + f'<div class="abs" style="left:40px;top:900px;width:960px;display:flex;justify-content:center">{domain_chips()}</div>'
            + f'<div class="abs" style="left:1040px;top:172px;width:840px;display:flex;flex-direction:column;gap:14px">{squad_switch()}{head(34)}</div>'
            + seats_for(row, 12, "column", "position:absolute;left:1040px;top:330px;width:840px")
            + f'<div class="abs" style="left:1040px;right:40px;top:1000px;display:flex;justify-content:space-between;align-items:center">'
            f'{edit_btn()}{take_into()}</div>' + toast())
    return body


def screen_F():
    # Cinema: the Ark fills the screen, the squad floats over it
    card = (f'<button class="pick sl glass" type="button" onClick="{{{{s.pick}}}}" style="position:relative;flex:1;display:flex;gap:14px;align-items:center;'
            f'padding:10px 16px 10px 20px;height:170px;box-sizing:border-box;box-shadow:inset 0 0 0 1px {{{{s.edge}}}}">'
            f'<span class="abs" style="left:0;top:14px;bottom:14px;width:3px;background:{{{{s.mark}}}}"></span>'
            f'{hull_view(190, 120)}<span style="flex:1;display:flex;flex-direction:column;gap:3px">{seat_head(18)}{rows4(True)}</span></button>')
    body = (sea() + nav()
            + ark_view(110, 120, 1700, 1054, caption=False)
            + '<div class="abs" style="left:0;right:0;bottom:0;height:420px;background:linear-gradient(180deg,rgba(0,4,31,0) 0%,rgba(0,4,31,.86) 62%)"></div>'
            + '<div class="abs" style="left:0;right:0;top:84px;height:230px;background:linear-gradient(180deg,rgba(0,4,31,.86) 0%,rgba(0,4,31,0) 100%)"></div>'
            + tabrow()
            + f'<div class="abs" style="left:40px;top:172px;width:900px;display:flex;flex-direction:column;gap:14px">{squad_switch()}{head(40)}</div>'
            + f'<div class="abs glass sls" style="right:40px;top:176px;padding:10px 12px">{domain_chips()}</div>'
            + f'<div class="abs" style="right:40px;top:246px;display:flex;align-items:center;gap:8px"><span style="width:9px;height:9px;transform:rotate(45deg);'
            f'background:{{{{domColor}}}}"></span><span class="lab" style="font-size:12px;color:{SUB}">The Ark - {{{{dom}}}}</span></div>'
            + seats_for(card, 14, "row", "position:absolute;left:40px;right:40px;top:808px")
            + f'<div class="abs" style="left:40px;right:40px;top:1000px;display:flex;justify-content:space-between;align-items:center">'
            f'{edit_btn()}{take_into()}</div>' + toast())
    return body


SCREENS = {"A": screen_A, "B": screen_B, "C": screen_C, "D": screen_D, "E": screen_E, "F": screen_F}


# ------------------------------------------------------------------------------------------------ compare and notes

def compare():
    hulls = ["Sparrow", "Dolphin", "Squirrel"]
    # 1. the colour fix: today's tint against the ship colours, per hull and domain
    head_row = "".join(f'<span class="cap" style="font-size:13px;color:{DOM[d][1]};text-align:center">{d}</span>' for d in DOMS)
    rows = []
    for hname in hulls:
        cells = []
        for d in DOMS:
            cells.append(
                f'<div style="display:grid;grid-template-columns:1fr 1fr;gap:6px">'
                f'<figure style="margin:0;background:#080C28;box-shadow:inset 0 0 0 1px {NEUT_D};height:120px;position:relative">'
                f'<img src="{blob(f"before_{hname}_{d}")}" alt="{hname} in {d}, built today: the card tinted 45 percent toward the domain" '
                f'style="position:absolute;inset:6px;width:calc(100% - 12px);height:calc(100% - 12px);object-fit:contain"></figure>'
                f'<figure style="margin:0;background:#080C28;box-shadow:inset 0 0 0 1px {DOM[d][0]};height:120px;position:relative">'
                f'<img src="{blob(f"{hname}_{d}")}" alt="{hname} in {d}: the ship colours" '
                f'style="position:absolute;inset:6px;width:calc(100% - 12px);height:calc(100% - 12px);object-fit:contain"></figure></div>')
        rows.append(f'<span style="font-size:18px;align-self:center">{hname}</span>' + "".join(cells))
    sub = "".join(f'<div style="display:grid;grid-template-columns:1fr 1fr;gap:6px"><span class="lab" style="font-size:10px;text-align:center">Built today</span>'
                  f'<span class="lab" style="font-size:10px;text-align:center;color:{LIGHT}">Ship colours</span></div>' for _ in DOMS)
    colour = (f'<div class="abs" style="left:60px;top:150px;width:1000px;display:flex;flex-direction:column;gap:10px">'
              f'<span class="lab" style="color:{LIGHT}">1 - The vessel\'s domain colour</span>'
              f'<span style="font-size:15px;line-height:1.45;color:{SUB}">Today the card render (the silver class livery) is multiplied 45 percent toward '
              f'the palette\'s TrailHighlightColor (teal 13FFF2, magenta FF00F9, amber FFA700), so the whole hull takes a cast, and Jade reads teal and Ruby pink - '
              f'neither is the colour on the ship. The fix draws each domain\'s own render: '
              f'gunmetal body, the domain on the accent plates only (the palette\'s ship colours), ember engines - the ship the player flies, lit.</span>'
              f'<div style="display:grid;grid-template-columns:110px repeat(3, minmax(0, 1fr));gap:10px 14px;margin-top:6px">'
              f'<span></span>{head_row}<span></span>{sub}{"".join(rows)}</div></div>')
    # 2. the two transitions, frame by frame (each frame drawn as it stands, no paused animation)
    def frames(kind):
        out = []
        steps = [("0.00 s", 0), ("0.25 s", 1), ("0.55 s", 2), ("0.80 s", 3), ("1.20 s", 4)]
        for t, k in steps:
            src = blob("Sparrow_Jade") if k < 2 else blob("Sparrow_Ruby")
            if kind == "nano":
                op = [1, .25, 0, .4, 1][k]
                bl = [0, 4, 6, 3, 0][k]
                bots = ""
                if 0 < k < 4:
                    spread = [0, .2, 1, .45, 0][k]
                    for b in range(18):
                        a = b * 2.39996
                        rr = (0.35 + (b % 5) * 0.13)
                        bx = 50 + math.cos(a) * rr * 40 * (1 + spread * 1.4)
                        by = 50 + math.sin(a) * rr * 30 * (1 + spread * 1.4) - spread * 8
                        col = DOM["Ruby"][0] if b % 4 else LIGHT
                        bots += (f'<span class="abs" style="left:{bx:.1f}%;top:{by:.1f}%;width:6px;height:6px;transform:rotate({45 + spread * 180:.0f}deg);'
                                 f'background:{col}"></span>')
                img = f'<img src="{src}" alt="" style="position:absolute;inset:8px;width:calc(100% - 16px);height:calc(100% - 16px);object-fit:contain;opacity:{op};filter:blur({bl}px)">'
                out.append((t, img + bots))
            else:
                sc = [1, .35, .06, .45, 1][k]
                rot = [0, 90, 170, -80, 0][k]
                op = [1, .7, 0, .7, 1][k]
                ring = [0, 1, 1.02, 1.04, 0][k]
                ringop = [0, 1, 1, .8, 0][k]
                img = (f'<span class="abs" style="left:50%;top:50%;width:70%;aspect-ratio:1;border-radius:50%;transform:translate(-50%,-50%);'
                       f'background:radial-gradient(circle,#00010A 0%,rgba(0,1,10,.85) 38%,rgba(0,1,10,0) 70%);opacity:{ringop}"></span>'
                       f'<img src="{src}" alt="" style="position:absolute;inset:8px;width:calc(100% - 16px);height:calc(100% - 16px);object-fit:contain;'
                       f'opacity:{op};transform:scale({sc}) rotate({rot}deg);filter:blur({0 if k in (0, 4) else 3}px)">'
                       f'<span class="abs" style="left:50%;top:50%;width:58%;aspect-ratio:1;border-radius:50%;border:2px solid {DOM["Ruby"][0]};'
                       f'transform:translate(-50%,-50%) scale({ring});opacity:{ringop}"></span>')
                out.append((t, img))
        return "".join(f'<div style="display:flex;flex-direction:column;gap:6px;align-items:center"><figure style="margin:0;position:relative;width:128px;height:96px;'
                       f'background:#080C28;box-shadow:inset 0 0 0 1px {NEUT_D};overflow:hidden">{inner}</figure>'
                       f'<span class="cap num" style="font-size:11px;color:{NEUT_L}">{t}</span></div>' for t, inner in out)
    trans = (f'<div class="abs" style="left:1140px;top:150px;width:720px;display:flex;flex-direction:column;gap:14px">'
             f'<span class="lab" style="color:{LIGHT}">2 - The domain change, Jade to Ruby</span>'
             f'<span style="font-size:15px;line-height:1.45;color:{SUB}">Both run 1.2 s per picture on unscaled time: the Ark first, then each escort 0.14 s apart. '
             f'Only the leader can start it.</span>'
             f'<span style="font-size:20px;margin-top:8px">Nanobot swap - A, C, E</span>'
             f'<span style="font-size:14px;line-height:1.4;color:{SUB}">The hull comes apart into 34 prism-bots in the new domain (a quarter stay white), they swirl out and rebuild it. '
             f'The rebuilt hull is the new domain\'s render, not a tint.</span>'
             f'<div style="display:flex;gap:10px">{frames("nano")}</div>'
             f'<span style="font-size:20px;margin-top:14px">Meerkat warp - B, D, F</span>'
             f'<span style="font-size:14px;line-height:1.4;color:{SUB}">The Meerkat\'s wormhole: a mouth opens on the hull with an Einstein ring in the new domain, '
             f'the hull is drawn into the throat, twisting, and comes back out the other side in its new colours.</span>'
             f'<div style="display:flex;gap:10px">{frames("warp")}</div></div>')
    title = (f'<div class="abs" style="left:60px;top:44px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - compare</span>'
             f'<span style="font-size:30px">The colour fix, and the two domain changes</span></div>')
    return title + colour + trans


def notes():
    def para(t):
        return f'<p style="margin:0;font-size:17px;line-height:1.5;color:#C9CDEA">{t}</p>'
    left = "".join([
        '<span class="lab">Asked (Shombith)</span>',
        para('"Rework the Ark squad screen. The domain colors are wrong on the vessel, please check that as we change, and we do not want that animation: '
             'we can either have a nanobot animation or the warping animation on the meerkat. We also need to show the 3D animated model of the Ark inside the squad screen."'),
        '<span class="lab" style="margin-top:10px">What was wrong with the colours</span>',
        para("Built (section 95): SquadNanobotSwap tints the whole hull Image 45 percent toward GetDomainSignalColor, which is the palette's TrailHighlightColor "
             "pushed to full brightness: teal 13FFF2, magenta FF00F9, amber FFA700. The picture under it is the Hangar card, in the silver class livery "
             "(a card claims no team), so body, glass and engines all take the cast. Jade comes out teal and Ruby pink, while the ship itself wears "
             "azure and violet plates (ShipColor) on a gunmetal body. Three Jade / Ruby / Gold sets disagree: the ship's plates, the trail highlight, and the style "
             "foundation's 00D4FF / A600FF / FFAE00."),
        para("Here every hull and the Ark are rendered from their FBX models in each domain with the game's own colours (body, glass and engine from "
             "vessel_colors.py; the domain slot from the palette's ShipColor1 / ShipColor2), lit for a showroom: a key, a fill and a rim light on the "
             "gunmetal, the domain plates glowing. A domain change swaps one correct picture for another. The chips, the edit line and the bots use the style "
             "foundation's Jade 00D4FF, Ruby A600FF, Gold FFAE00. The ground is the menu's deep blue with its sea lines."),
        '<span class="lab" style="margin-top:10px">The Ark</span>',
        para("ArkReverse.fbx, Garrett's Ark (the Arkway's and every voyage's), rendered as a slow orbit (36 frames, a 6 s loop). "
             "In the game it would be the model itself on a RenderTexture, the way the converter tab draws its 3D picture, with its turn takes playing. "
             "The 108 hull prisms the game lays in its sockets are not part of the model and are not drawn here."),
    ])
    right = "".join([
        '<span class="lab">The six</span>',
        para("A Escort: the Ark centre stage, the three escorts round it, the seats along the bottom - nanobots. "
             "B Hangar: seats stacked left, the Ark large right - warp. C The deck, kept: the built deck unchanged, the Ark above it - nanobots. "
             "D Bays: the Ark over three launch bays - warp. E Dossier: the Ark left half, three seat rows right - nanobots. "
             "F Cinema: the Ark fills the screen, the squad floats over it - warp."),
        para("Every board is interactive: press Jade / Ruby / Gold on Skim Wing to play the change; Breakers is Kestrel's, so its chips refuse. "
             "Press the squad rows to switch squads."),
        '<span class="lab" style="margin-top:10px">Recommendation: A, Escort, with the nanobots</span>',
        para("It is the only one where the Ark reads as what it is - a big ship its three escorts protect - while the seats keep the four rows the deck has. "
             "The nanobots are already built (SquadNanobotSwap) and need only the colour fix; the warp is new UI work (a lens ring and a throat over each picture). "
             "C costs the least to build if the deck should stay."),
        '<span class="lab" style="margin-top:10px">Differs from the build</span>',
        para("The hull picture per domain replaces the tint (a render per hull and domain, or the model on a RenderTexture); the Ark's 3D picture joins the tab; "
             "the domain chips stay where the leader is; Make squad, Join, Edit and Take into (Mission, Arena) are unchanged from section 95. The build waits on a pick."),
    ])
    return (f'<div class="abs" style="left:60px;top:44px;display:flex;flex-direction:column;gap:4px"><span class="lab">Rev {REV} - notes</span>'
            f'<span style="font-size:30px">The Ark\'s Squads tab: real domain colours, the Ark in 3D, six ways</span></div>'
            f'<div class="abs" style="left:60px;top:140px;width:860px;display:flex;flex-direction:column;gap:12px">{left}</div>'
            f'<div class="abs" style="left:1000px;top:140px;width:860px;display:flex;flex-direction:column;gap:12px">{right}</div>')


def per_seat_lists(src):
    """A and C place each seat on its own: give them seats0..2 (one-item lists) from the logic's seats."""
    return src.replace("return {\n   ark, seats,", "return {\n   ark, seats, seats0: seats.slice(0, 1), seats1: seats.slice(1, 2), seats2: seats.slice(2, 3),")


def main():
    files = {f"R{REV}Compare.dc.html": board(f"Rev {REV} - compare", compare())}
    for k, (name, fx, line) in OPTS.items():
        s = board(f"Rev {REV} {k} - {name} ({FX_NAME[fx]})", SCREENS[k](), fx)
        files[f"R{REV}{k}.dc.html"] = per_seat_lists(s)
    files[f"R{REV}Notes.dc.html"] = board(f"Rev {REV} - notes", notes(), h=900)
    for n, s in files.items():
        (ROOT / n).write_text(s, encoding="utf-8")
        print(f"{n:24} {len(s):>7}")


if __name__ == "__main__":
    main()
