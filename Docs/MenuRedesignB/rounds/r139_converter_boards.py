#!/usr/bin/env python3
"""Rev 139: the Elemental Converter redrawn (Shombith, 2026-11-24: "No convert animation happening even the UI is very
weird and does not look good at all. Completely need to rework this. The BG is fine but not the UI").

Writes R139A, R139B, R139C (interactive, over the pier stills the shipped converter clips are made of), R139Compare
(the convert moment on one timeline) and R139Notes into <root>/project/. The kit (fonts, nav band, tabs, the balances
switch) is Rev 104 B's, so only the converter's own overlay differs.

    python3 Docs/MenuRedesignB/rounds/r139_converter_boards.py <root>
"""
import sys
from pathlib import Path

ROOT = Path(sys.argv[1] if len(sys.argv) > 1 else ".") / "project"

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
.btn{{box-sizing:border-box;display:flex;align-items:center;justify-content:center;gap:12px;height:58px;padding:0 34px;white-space:nowrap;border:0;cursor:pointer;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:21px;letter-spacing:.12em;text-transform:uppercase}}
.cta{{background:#99FF80;color:#00010A}}
.off{{background:rgba(0,4,31,.7);color:#5C5F70;box-shadow:inset 0 0 0 1px #222645;cursor:default}}
.btn .kc{{border-color:currentColor;color:inherit;opacity:.85}}
.btn:focus-visible,.pick:focus-visible,.pad:focus-visible{{outline:2px solid #00D4FF;outline-offset:2px}}
.pick{{background:none;border:0;padding:0;cursor:pointer;color:inherit;font:inherit;text-align:left}}
.glass{{background:rgba(0,4,31,.62);backdrop-filter:blur(14px);-webkit-backdrop-filter:blur(14px)}}
.bg{{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;transition:opacity 260ms ease-out}}
.tick{{transition:color 260ms ease-out,opacity 260ms ease-out}}
@keyframes csRise{{0%{{opacity:0;transform:translateY(8px)}}25%{{opacity:1;transform:none}}100%{{opacity:0;transform:translateY(-30px)}}}}
@keyframes csPop{{0%{{transform:scale(.4);opacity:0}}60%{{transform:scale(1.12);opacity:1}}100%{{transform:scale(1);opacity:1}}}}
@keyframes csDraw{{from{{stroke-dashoffset:1}}to{{stroke-dashoffset:0}}}}
@keyframes csRing{{0%{{transform:scale(.7);opacity:.95}}100%{{transform:scale(1.9);opacity:0}}}}
@keyframes csFuse{{0%{{transform:translate(0,0) scale(1) rotate(0);opacity:1}}100%{{transform:translate(var(--dx),var(--dy)) scale(.2) rotate(200deg);opacity:0}}}}
@keyframes csBack{{from{{opacity:0;transform:scale(.6)}}to{{opacity:1;transform:none}}}}
@keyframes csFlash{{0%{{opacity:0}}30%{{opacity:1}}100%{{opacity:0}}}}
{extra_css}
</style>
</helmet>
<div style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
<sc-for list="{{{{layers}}}}" as="l" hint-placeholder-count="3"><img class="bg" src="{{{{l.src}}}}" alt="{{{{l.alt}}}}" style="opacity:{{{{l.op}}}}"></sc-for>
<div class="abs" style="inset:0;background:linear-gradient(180deg,rgba(0,1,10,.72) 0%,rgba(0,1,10,0) 22%,rgba(0,1,10,0) 66%,rgba(0,1,10,.78) 100%);pointer-events:none"></div>
"""

ICONS = {"CODEX": "ec690d6b90d6fcdbc430b9bbaacf28a9", "HANGAR": "34f5a50f1275fef42808fe9df3bf6af5",
         "HOME": "a5bd4d8d1de7b494567fc3de75525e1e", "ARK": "e5b050c5a676193c650e94d873ad40e2",
         "PORT": "d0e850bc927559f1a499868d26f2a3de"}


def nav():
    items = []
    for name, blob in ICONS.items():
        on = name == "ARK"
        bar = '<span class="abs" style="left:0;right:0;bottom:-10px;height:2px;background:#E6E9FF"></span>' if on else ""
        items.append(f'<span style="position:relative;display:flex;align-items:center;gap:10px;font-family:\'Chakra Petch\';'
                     f'font-weight:600;font-size:17px;letter-spacing:.12em;color:{"#E6E9FF" if on else "#747BAD"}">'
                     f'<img src="/_blob/{blob}" alt="" style="width:26px;height:26px;object-fit:contain;opacity:{1 if on else 0.7}">{name}{bar}</span>')
    tabs = []
    for t in ("Converter", "Squads", "Pilots", "Artifacts", "Decor"):
        on = t == "Converter"
        col = "#E6E9FF" if on else ("#5C5F70" if t == "Decor" else "#747BAD")
        lock = ('<svg width="12" height="14" viewBox="0 0 14 16" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true">'
                '<path d="M3 7V5a4 4 0 018 0v2"></path><path d="M1.5 7h11v8h-11z"></path></svg>') if t == "Decor" else ""
        tabs.append(f'<span style="position:relative;display:flex;align-items:center;gap:8px;color:{col}">{lock}'
                    f'<span class="cap" style="font-size:16px">{t}</span><span class="abs" style="left:0;right:0;bottom:-15px;'
                    f'height:2px;background:{"#E6E9FF" if on else "transparent"}"></span></span>')
    return ('<nav aria-label="Menu screens" class="abs" style="left:0;right:0;top:0;height:84px;background:rgba(0,1,10,.78);'
            'border-bottom:1px solid #222645;display:flex;align-items:center;justify-content:center;gap:48px;z-index:5">'
            '<span class="kc">LT</span>' + "".join(items) + '<span class="kc">RT</span></nav>'
            '<div class="abs" style="left:40px;right:40px;top:96px;height:52px;display:flex;align-items:center;gap:30px;'
            'border-bottom:1px solid rgba(116,123,173,.35);z-index:4"><span class="kc">LB</span>' + "".join(tabs) +
            '<span class="kc">RB</span></div>'
            '<div class="abs" style="left:40px;top:164px;display:flex;align-items:center;gap:8px;padding:6px 10px;'
            'border:1px dashed rgba(154,160,204,.5);z-index:6"><span class="lab" style="font-size:11px">Mockup - balances</span>'
            '<sc-for list="{{presets}}" as="p" hint-placeholder-count="3"><button class="pick sls glass" type="button" '
            'aria-pressed="{{p.pressed}}" onClick="{{p.pick}}" style="padding:6px 12px;box-shadow:inset 0 0 0 1px {{p.edge}}">'
            '<span class="cap" style="font-size:13px;color:{{p.col}}">{{p.name}}</span></button></sc-for></div>\n')


TAIL = """</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{"$preview":{"width":1920,"height":1080},"start":{"preset":0}}'>
const ORDER = ["Space", "Charge", "Mass", "Time"];
const ART = {"Space": "/_blob/552b1a2acbcb63737e1174c398089c9c", "Charge": "/_blob/9b5b9502defc2ffb3ddfc86af8f2b581", "Mass": "/_blob/f1c37884ec4eaede9f7a20f81af70273", "Time": "/_blob/5f2b5586aa94e21d23984dedc2c4f93a"};
const OMNI_ART = "/_blob/7850440a6efe08dd09a6f63c7810dd29";
const BG = {"idle": "/_blob/7ac8cae4fa1672907baa9fb0e73dd2e1", "busy": "/_blob/c0a80065bda4df0778cad35299132c7e", "done": "/_blob/9a3144de6c2328cb7ae1a795c05bf9e0"};
const ALT = {"idle": "The pier out of the Ark, the four crystals idling over it in the cell", "busy": "The crystals flying in to the pier's ring", "done": "The omni crystal formed over the pier's ring"};
const PRESETS = [["Starting 10 each", [10, 10, 10, 10]], ["Short of Time", [6, 9, 4, 0]], ["Later: none yet", [0, 0, 0, 0]]];
const STEP = 650;
const fmt = (n) => String(n).replace(/\\B(?=(\\d{3})+(?!\\d))/g, ',');
__CONSTS__
class Component extends DCLogic {
  constructor(props) {
    super(props);
    const s = Object.assign({ preset: 0 }, props.start || {});
    const p = Math.max(0, Math.min(PRESETS.length - 1, s.preset | 0));
    this.state = { preset: p, bal: PRESETS[p][1].slice(), omni: 3385, phase: 'idle', step: -1, gained: false, runs: 0 };
    this.t = null;
  }
  componentWillUnmount() { if (this.t) clearInterval(this.t); }
  stop() { if (this.t) { clearInterval(this.t); this.t = null; } }
  pickPreset(i) { this.stop(); this.setState({ preset: i, bal: PRESETS[i][1].slice(), phase: 'idle', step: -1, gained: false }); }
  spend(i) { const bal = this.state.bal.slice(); bal[i] -= 1; return bal; }
  run() {
    const st = this.state;
    if (st.phase === 'busy' || st.bal.some((b) => b < 1)) return;
    this.stop();
    // The press is answered on the same frame: the first crystal leaves now (the clip is trimmed to start moving).
    this.setState({ phase: 'busy', step: 0, bal: this.spend(0), gained: false, runs: st.runs + 1 });
    this.t = setInterval(() => {
      const n = this.state.step + 1;
      if (n >= ORDER.length) {
        this.stop();
        this.setState({ phase: 'done', step: -1, omni: this.state.omni + 1, gained: true });
      } else this.setState({ step: n, bal: this.spend(n) });
    }, STEP);
  }
  base() {
    const st = this.state;
    const busy = st.phase === 'busy', done = st.phase === 'done';
    const shortIdx = st.bal.findIndex((b) => b < 1);
    const can = !busy && shortIdx < 0;
    const empty = st.bal.every((b) => b < 1);
    let line = 'One of each makes one omni crystal', lineCol = '#8C93C7';
    if (busy) line = 'Converting';
    else if (done && can) line = 'One omni crystal made';
    else if (empty) { line = 'No elemental crystals yet. Matches earn them.'; lineCol = '#C9CDEA'; }
    else if (shortIdx >= 0) { line = 'Short of ' + ORDER[shortIdx] + ': one more converts'; lineCol = '#C9CDEA'; }
    const ph = busy ? 'busy' : (done ? 'done' : 'idle');
    return { st, busy, done, can, shortIdx, line, lineCol,
      layers: ['idle', 'busy', 'done'].map((k) => ({ src: BG[k], op: k === ph ? 1 : 0, alt: k === ph ? ALT[k] : '' })),
      presets: PRESETS.map((p, i) => ({ name: p[0], pressed: i === st.preset ? 'true' : 'false',
        edge: i === st.preset ? '#00D4FF' : 'rgba(116,123,173,.45)', col: i === st.preset ? '#E6E9FF' : '#9AA0CC', pick: () => this.pickPreset(i) })),
      omniText: fmt(st.omni), omniAria: 'Omni crystals: ' + st.omni, omniCol: done ? '#99FF80' : '#E6E9FF',
      omniGained: st.gained,
      convLabel: busy ? 'Converting' : (done && can ? 'Convert again' : 'Convert'),
      convClass: can ? 'btn cta sl' : 'btn off sl', convOff: can ? 'false' : 'true',
      convert: () => this.run() };
  }
  elem(i) {
    const st = this.state, busy = st.phase === 'busy';
    const name = ORDER[i], n = st.bal[i], active = busy && i === st.step, gone = busy && i <= st.step, zero = n < 1;
    return { name, art: ART[name], n: String(n), aria: name + ' crystals: ' + n, active, gone,
      col: active ? '#99FF80' : (zero ? '#5C5F70' : '#E6E9FF'), nameCol: zero && !busy ? '#C9CDEA' : '#8C93C7',
      artOp: zero ? 0.35 : 1, spending: active };
  }
__RENDER__
}
</script>
</body>
</html>
"""


def board(title, extra_css, body, consts, render):
    return (HEAD.format(title=title, extra_css=extra_css) + nav() + body +
            TAIL.replace("__CONSTS__", consts).replace("__RENDER__", render))


# Where the pier's landing ring sits in the 1920 x 1080 stills (measured on converter_Nocturne_loop.mp4).
PAD_X, PAD_Y, PAD_RX, PAD_RY = 960, 603, 142, 20

# ---------------------------------------------------------------- A: light paths
A_BODY = """<svg aria-hidden="true" class="abs" width="1920" height="1080" viewBox="0 0 1920 1080" style="left:0;top:0;pointer-events:none;z-index:3" fill="none">
<sc-for list="{{paths}}" as="p" hint-placeholder-count="4"><path d="{{p.d}}" stroke="rgba(184,192,240,.26)" stroke-width="1.5" stroke-dasharray="3 9" stroke-linecap="round"></path><path d="{{p.d}}" pathLength="1" stroke="#99FF80" stroke-width="2.5" stroke-linecap="round" stroke-dasharray="1" stroke-dashoffset="{{p.off}}" style="opacity:{{p.op}};animation:{{p.anim}}"></path></sc-for>
<sc-if value="{{flash}}" hint-placeholder-val="{{ false }}"><ellipse cx="960" cy="603" rx="142" ry="20" stroke="#99FF80" stroke-width="3" style="transform-box:fill-box;transform-origin:center;animation:csRing 900ms ease-out both"></ellipse></sc-if>
</svg>
<sc-if value="{{omniGained}}" hint-placeholder-val="{{ false }}"><span class="abs cap" style="left:860px;width:200px;top:520px;text-align:center;font-size:24px;color:#99FF80;z-index:4;animation:csRise 1400ms ease-out both">+1 omni</span></sc-if>
<div class="abs" style="left:0;right:0;top:902px;text-align:center;z-index:4"><span class="lab" style="font-size:14px;color:{{lineCol}}">{{line}}</span></div>
<sc-for list="{{elems}}" as="e" hint-placeholder-count="4"><div role="group" aria-label="{{e.aria}}" class="abs" style="left:{{e.left}};top:944px;width:170px;display:flex;align-items:center;gap:12px;z-index:4">
<img src="{{e.art}}" alt="" style="width:44px;height:44px;object-fit:contain;opacity:{{e.artOp}};transition:opacity 260ms ease-out">
<div style="display:flex;flex-direction:column;gap:2px;position:relative"><span class="num tick" style="font-size:36px;line-height:1;color:{{e.col}}">{{e.n}}</span><span class="lab" style="font-size:12px;color:{{e.nameCol}}">{{e.name}}</span>
<sc-if value="{{e.spending}}" hint-placeholder-val="{{ false }}"><span class="abs cap" style="left:0;top:-22px;font-size:18px;color:#99FF80;animation:csRise 700ms ease-out both">-1</span></sc-if></div>
</div></sc-for>
<div class="abs" style="left:1610px;top:944px;width:1px;height:56px;background:rgba(116,123,173,.45);z-index:4"></div>
<div role="group" aria-label="{{omniAria}}" class="abs" style="left:1640px;top:944px;width:220px;display:flex;align-items:center;gap:12px;z-index:4">
<img src="/_blob/7850440a6efe08dd09a6f63c7810dd29" alt="" style="width:48px;height:48px;object-fit:contain">
<div style="display:flex;flex-direction:column;gap:2px"><span class="num tick" style="font-size:36px;line-height:1;color:{{omniCol}}">{{omniText}}</span><span class="lab" style="font-size:12px">Omni</span></div>
</div>
<div class="abs" style="left:0;right:0;top:944px;display:flex;justify-content:center;z-index:5"><button class="{{convClass}}" type="button" onClick="{{convert}}" aria-disabled="{{convOff}}" style="min-width:280px"><span class="kc">A</span><span>{{convLabel}}</span></button></div>
"""
A_CONSTS = "const LEFT = [330, 560, 1190, 1420];\nconst ANCHOR = [352, 582, 1212, 1442];"
A_RENDER = """  renderVals() {
    const b = this.base();
    const elems = ORDER.map((_, i) => Object.assign(this.elem(i), { left: LEFT[i] + 'px' }));
    const paths = ORDER.map((_, i) => {
      const x = ANCHOR[i], e = elems[i];
      const d = 'M' + x + ' 936 C' + x + ' 760 ' + (960 + (x - 960) * 0.25) + ' 680 960 610';
      return { d, off: e.gone ? 0 : 1, op: e.gone ? 0.85 : 0, anim: e.active ? 'csDraw 600ms ease-out both' : 'none' };
    });
    return Object.assign(b, { elems, paths, flash: b.done && b.st.gained });
  }"""

# ---------------------------------------------------------------- B: glass orb
B_CSS = (".orb{border-radius:50%;background:radial-gradient(circle at 36% 28%,rgba(230,233,255,.26) 0,rgba(230,233,255,.05) 22%,"
         "rgba(0,4,31,.55) 52%,rgba(0,1,10,.9) 100%);box-shadow:inset 0 0 0 1.5px rgba(184,192,240,.5),inset 0 -24px 48px rgba(184,192,240,.10),"
         "0 18px 50px rgba(0,1,10,.7)}")
B_BODY = """<div aria-hidden="true" class="abs" style="left:740px;top:990px;width:440px;height:40px;border-radius:50%;background:radial-gradient(ellipse at center,rgba(184,192,240,.22),rgba(184,192,240,0) 70%);z-index:3"></div>
<div class="abs orb" style="left:850px;top:790px;width:220px;height:220px;z-index:4;overflow:hidden">
<svg aria-hidden="true" width="220" height="220" viewBox="0 0 300 300" class="abs" style="left:0;top:0" fill="none"><ellipse cx="150" cy="160" rx="104" ry="34" stroke="rgba(184,192,240,.30)" stroke-dasharray="3 7"></ellipse><ellipse cx="150" cy="160" rx="62" ry="20" stroke="rgba(184,192,240,.18)" stroke-dasharray="2 6"></ellipse></svg>
<sc-if value="{{flash}}" hint-placeholder-val="{{ false }}"><div class="abs" style="inset:0;border-radius:50%;background:radial-gradient(circle,rgba(153,255,128,.45),rgba(153,255,128,0) 62%);animation:csFlash 900ms ease-out both"></div></sc-if>
<sc-for list="{{elems}}" as="e" hint-placeholder-count="4"><img src="{{e.art}}" alt="" class="abs" style="left:{{e.ix}};top:{{e.iy}};width:44px;height:44px;object-fit:contain;opacity:{{e.artOp}};--dx:{{e.dx}};--dy:{{e.dy}};animation:{{e.anim}}"></sc-for>
<sc-if value="{{omniShown}}" hint-placeholder-val="{{ false }}"><img src="/_blob/7850440a6efe08dd09a6f63c7810dd29" alt="" class="abs" style="left:74px;top:74px;width:72px;height:72px;object-fit:contain;animation:csPop 520ms cubic-bezier(.34,1.56,.64,1) both"></sc-if>
</div>
<svg aria-hidden="true" class="abs" width="1920" height="1080" viewBox="0 0 1920 1080" style="left:0;top:0;pointer-events:none;z-index:3" fill="none">
<sc-for list="{{leads}}" as="l" hint-placeholder-count="4"><path d="{{l.d}}" stroke="{{l.c}}" stroke-width="1" style="transition:stroke 260ms ease-out"></path></sc-for>
</svg>
<sc-for list="{{elems}}" as="e" hint-placeholder-count="4"><div role="group" aria-label="{{e.aria}}" class="abs" style="left:{{e.left}};top:{{e.top}};width:150px;display:flex;flex-direction:column;align-items:{{e.align}};gap:2px;z-index:4">
<span class="num tick" style="font-size:38px;line-height:1;color:{{e.col}}">{{e.n}}</span><span class="lab" style="font-size:12px;color:{{e.nameCol}}">{{e.name}}</span>
<sc-if value="{{e.spending}}" hint-placeholder-val="{{ false }}"><span class="abs cap" style="top:-24px;font-size:18px;color:#99FF80;animation:csRise 700ms ease-out both">-1</span></sc-if>
</div></sc-for>
<div role="group" aria-label="{{omniAria}}" class="abs" style="left:860px;top:744px;width:200px;display:flex;align-items:baseline;justify-content:center;gap:10px;z-index:4"><span class="lab" style="font-size:12px">Omni</span><span class="num tick" style="font-size:28px;color:{{omniCol}}">{{omniText}}</span>
<sc-if value="{{omniGained}}" hint-placeholder-val="{{ false }}"><span class="abs cap" style="right:-10px;top:-22px;font-size:18px;color:#99FF80;animation:csRise 1200ms ease-out both">+1</span></sc-if></div>
<div class="abs" style="left:0;right:0;top:1048px;text-align:center;z-index:4"><span class="lab" style="font-size:13px;color:{{lineCol}}">{{line}}</span></div>
<div class="abs" style="left:0;right:0;top:982px;display:flex;justify-content:center;z-index:5"><button class="{{convClass}}" type="button" onClick="{{convert}}" aria-disabled="{{convOff}}" style="min-width:260px"><span class="kc">A</span><span>{{convLabel}}</span></button></div>
"""
B_CONSTS = ("const INNER = [[18, 93], [58, 38], [118, 38], [158, 93]];\n"
            "const LABEL = [[680, 900, 'flex-end'], [680, 820, 'flex-end'], [1090, 820, 'flex-start'], [1090, 900, 'flex-start']];")
B_RENDER = """  renderVals() {
    const b = this.base();
    const elems = ORDER.map((_, i) => {
      const e = this.elem(i), c = INNER[i], l = LABEL[i];
      const fused = b.busy && i < b.st.step;
      return Object.assign(e, { ix: c[0] + 'px', iy: c[1] + 'px', dx: (88 - c[0]) + 'px', dy: (88 - c[1]) + 'px',
        anim: e.active ? 'csFuse 620ms cubic-bezier(.55,0,.8,.4) both' : (fused ? 'csFuse 1ms linear both' : (b.done ? 'csBack 500ms 700ms ease-out both' : 'none')),
        left: l[0] + 'px', top: l[1] + 'px', align: l[2] });
    });
    const leads = ORDER.map((_, i) => {
      const c = INNER[i], l = LABEL[i], e = elems[i];
      const ox = 850 + c[0] + 22, oy = 790 + c[1] + 22, lx = i < 2 ? 836 : 1084, ly = l[1] + 20;
      return { d: 'M' + lx + ' ' + ly + ' L' + ox + ' ' + oy,
        c: e.gone ? '#99FF80' : 'rgba(184,192,240,.32)' };
    });
    return Object.assign(b, { elems, leads, flash: b.done && b.st.gained, omniShown: b.done && b.st.gained });
  }"""

# ---------------------------------------------------------------- C: the pad is the button
C_CSS = (".pad{position:absolute;background:none;border:0;padding:0;cursor:pointer;color:inherit}"
         ".pad .ring{transition:stroke 200ms ease-out,stroke-width 200ms ease-out}"
         ".pad:hover .ring.on,.pad:focus-visible .ring.on{stroke:#C9FFB9;stroke-width:4}"
         ".pad[aria-disabled=true]{cursor:default}")
C_BODY = """<button class="pad" type="button" onClick="{{convert}}" aria-disabled="{{convOff}}" aria-label="{{padAria}}" style="left:770px;top:520px;width:380px;height:130px;z-index:5">
<svg aria-hidden="true" width="380" height="130" viewBox="0 0 380 130" class="abs" style="left:0;top:0" fill="none">
<ellipse class="ring {{ringClass}}" cx="190" cy="83" rx="150" ry="24" stroke="{{ringCol}}" stroke-width="2.5"></ellipse>
<ellipse cx="190" cy="83" rx="118" ry="17" stroke="{{ringCol}}" stroke-opacity=".45" stroke-width="1" stroke-dasharray="3 7"></ellipse>
<sc-for list="{{pulses}}" as="p" hint-placeholder-count="1"><ellipse cx="190" cy="83" rx="150" ry="24" stroke="#99FF80" stroke-width="3" style="transform-box:fill-box;transform-origin:center;animation:csRing 800ms ease-out both"></ellipse></sc-for>
</svg>
<span class="abs" style="left:0;right:0;top:0;display:flex;justify-content:center;align-items:center;gap:10px"><span class="kc" style="border-color:{{ringCol}};color:{{ringCol}};background:rgba(0,1,10,.55)">A</span><span class="cap" style="font-size:22px;color:{{padTextCol}};text-shadow:0 0 10px rgba(0,1,10,.95),0 1px 2px rgba(0,1,10,.95)">{{convLabel}}</span></span>
</button>
<sc-if value="{{omniGained}}" hint-placeholder-val="{{ false }}"><span class="abs cap" style="left:860px;width:200px;top:470px;text-align:center;font-size:24px;color:#99FF80;z-index:4;animation:csRise 1400ms ease-out both">+1 omni</span></sc-if>
<div class="abs" style="left:0;right:0;top:902px;text-align:center;z-index:4"><span class="lab" style="font-size:14px;color:{{lineCol}}">{{line}}</span></div>
<div class="abs" style="left:0;right:0;top:944px;display:flex;justify-content:center;align-items:center;gap:56px;z-index:4">
<sc-for list="{{elems}}" as="e" hint-placeholder-count="4"><div role="group" aria-label="{{e.aria}}" style="position:relative;display:flex;align-items:center;gap:12px">
<img src="{{e.art}}" alt="" style="width:44px;height:44px;object-fit:contain;opacity:{{e.artOp}};transition:opacity 260ms ease-out">
<div style="display:flex;flex-direction:column;gap:2px;position:relative"><span class="num tick" style="font-size:36px;line-height:1;color:{{e.col}}">{{e.n}}</span><span class="lab" style="font-size:12px;color:{{e.nameCol}}">{{e.name}}</span>
<sc-if value="{{e.spending}}" hint-placeholder-val="{{ false }}"><span class="abs cap" style="left:0;top:-22px;font-size:18px;color:#99FF80;animation:csRise 700ms ease-out both">-1</span></sc-if></div>
</div></sc-for>
<span style="width:1px;height:56px;background:rgba(116,123,173,.45)"></span>
<div role="group" aria-label="{{omniAria}}" style="display:flex;align-items:center;gap:12px"><img src="/_blob/7850440a6efe08dd09a6f63c7810dd29" alt="" style="width:48px;height:48px;object-fit:contain">
<div style="display:flex;flex-direction:column;gap:2px"><span class="num tick" style="font-size:36px;line-height:1;color:{{omniCol}}">{{omniText}}</span><span class="lab" style="font-size:12px">Omni</span></div></div>
</div>
"""
C_CONSTS = ""
C_RENDER = """  renderVals() {
    const b = this.base();
    const elems = ORDER.map((_, i) => this.elem(i));
    const ringCol = b.can ? '#99FF80' : (b.busy ? '#99FF80' : '#5C5F70');
    // One pulse per crystal landing, keyed by the step so each restarts its ring.
    const pulses = b.busy ? [{ k: b.st.step }] : (b.done && b.st.gained ? [{ k: 9 }] : []);
    return Object.assign(b, { elems, ringCol, ringClass: b.can ? 'on' : 'off', pulses,
      padTextCol: b.can || b.busy ? '#E6E9FF' : '#8C93C7',
      padAria: b.can ? 'Convert: press the pier\\'s ring' : (b.busy ? 'Converting' : b.line) });
  }"""

# ---------------------------------------------------------------- Compare: the convert moment on one timeline
CMP = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Rev 139 - the convert moment of A, B and C side by side, frame by frame</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:#00010A}
.lab{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:13px;letter-spacing:.16em;text-transform:uppercase;color:#8C93C7}
.cap{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}
.abs{position:absolute}
</style>
</helmet>
<div style="width: 1920px; height: 1080px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif">
<div class="abs" style="left:80px;top:64px;right:80px"><div class="cap" style="font-size:30px">The convert moment, A, B and C on one clock</div>
<div style="margin-top:12px;font-size:18px;line-height:1.5;color:#C9CDEA;max-width:1500px">Pressing CONVERT today plays the shipped clip from its first frame, and the crystals in it only start to move 4.7 seconds later: for almost five seconds the press looks like it did nothing. All three options start the clip where the crystals leave, and the overlay answers on the press itself.</div></div>
<div class="abs" style="left:420px;right:80px;top:250px;height:30px;display:flex;justify-content:space-between"><sc-for list="{{ticks}}" as="t" hint-placeholder-count="8"><span class="lab" style="width:20px;text-align:center">{{t.s}}</span></sc-for></div>
<sc-for list="{{rows}}" as="r" hint-placeholder-count="5"><div class="abs" style="left:80px;right:80px;top:{{r.top}};height:120px;border-top:1px solid #222645">
<div class="abs" style="left:0;top:22px;width:320px"><div class="cap" style="font-size:19px;color:{{r.col}}">{{r.name}}</div><div style="margin-top:6px;font-size:14px;line-height:1.4;color:#8C93C7">{{r.sub}}</div></div>
<div class="abs" style="left:350px;right:0;top:56px;height:2px;background:#222645"></div>
<sc-for list="{{r.spans}}" as="s" hint-placeholder-count="3"><div class="abs" style="left:{{s.left}};width:{{s.w}};top:46px;height:22px;background:{{s.bg}};box-shadow:inset 0 0 0 1px {{s.edge}}"></div><span class="abs" style="left:{{s.left}};top:76px;font-size:13px;line-height:1.3;color:#C9CDEA;width:{{s.tw}}">{{s.t}}</span></sc-for>
</div></sc-for>
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{"$preview":{"width":1920,"height":1080}}'>
const X0 = 350, PX = 2.05;   // 0 s at 350 px into the row, 205 px per second (the row is 1410 px wide: 0 to 6.8 s)
const at = (s) => Math.round(X0 + s * PX * 100) + 'px';
const w = (a, b) => Math.round((b - a) * PX * 100) + 'px';
const SPAN = (a, b, t, hot) => ({ left: at(a), w: w(a, b), tw: Math.max(150, Math.round((b - a) * PX * 100)) + 'px', t,
  bg: hot ? 'rgba(153,255,128,.28)' : 'rgba(116,123,173,.18)', edge: hot ? '#99FF80' : '#434C89' });
const ROWS = [
  ['Today', 'the clip from frame 0, the dais waits on it', '#C9CDEA', [SPAN(0, 4.7, 'Nothing moves. CONVERT reads Converting.', false), SPAN(4.7, 6.8, 'the crystals fly in; the omni forms at 10.7 s', true)]],
  ['A  Light paths', 'the counts at the foot, a path per crystal', '#E6E9FF', [SPAN(0, 0.6, 'Space -1, its path draws to the pad', true), SPAN(0.65, 2.6, 'Charge, Mass, Time each -1, path by path', true), SPAN(2.6, 3.5, 'ring flash, +1 omni', true)]],
  ['B  Glass orb', 'a small glass sphere at the foot', '#E6E9FF', [SPAN(0, 0.6, 'Space spirals into the orb', true), SPAN(0.65, 2.6, 'the other three follow', true), SPAN(2.6, 3.5, 'the omni pops inside, +1', true)]],
  ['C  The pad is the button', 'press the pier\\'s ring itself', '#E6E9FF', [SPAN(0, 0.6, 'the ring pulses, Space -1', true), SPAN(0.65, 2.6, 'a pulse as each crystal lands', true), SPAN(2.6, 3.5, '+1 omni rises off the pad', true)]],
  ['The clip, every option', 'converter_Nocturne_convert, trimmed', '#99FF80', [SPAN(0, 6.0, 'starts at its 4.7 s mark: the crystals leave on the press, the omni forms about 6 s later', true)]],
];
class Component extends DCLogic {
  renderVals() {
    return {
      ticks: [0, 1, 2, 3, 4, 5, 6].map((s) => ({ s: s + ' s' })),
      rows: ROWS.map((r, i) => ({ name: r[0], sub: r[1], col: r[2], top: (300 + i * 150) + 'px', spans: r[3] })),
    };
  }
}
</script>
</body>
</html>
"""

NOTES = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Rev 139 - notes: why nothing seemed to happen on CONVERT, what each option changes in the build</title>
<script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">
<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">
<style>
body{margin:0;background:#00010A}
.cap{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.12em;text-transform:uppercase}
p{margin:0 0 14px 0}
</style>
</helmet>
<div style="width: 1920px; height: 900px; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; font-family: 'Aldrich', 'Chakra Petch', sans-serif; padding: 64px 80px; box-sizing: border-box; font-size: 19px; line-height: 1.5">
<div class="cap" style="font-size:30px;margin-bottom:24px">Rev 139 notes</div>
<div style="display:flex;gap:80px">
<div style="flex:1;color:#C9CDEA">
<p><span class="cap" style="color:#E6E9FF">Asked</span> (Shombith, 2026-11-24): "No convert animation happening even the UI is very weird and does not look good at all. Completely need to rework this. The BG is fine but not the UI." The pier and its clips stay; the dais of Rev 104 B goes.</p>
<p><span class="cap" style="color:#E6E9FF">Why nothing seemed to happen.</span> CONVERT plays converter_Nocturne_convert from frame 0, and in that clip the four crystals idle for the first 4.7 seconds before they fly. The dais's rays and counts step on the overlay's own clock, so the screen said Converting while the picture stood still. Every option trims the clip to start where the crystals leave, and answers the press on the same frame.</p>
<p><span class="cap" style="color:#E6E9FF">Why the dais read as weird.</span> Its three big ellipses cut across the deck below the crystals, the counts sat on the rings far from the crystals they count, and the rays pointed at an omni drawn on the floor, not at the pad where the real omni forms.</p>
</div>
<div style="flex:1;color:#C9CDEA">
<p><span class="cap" style="color:#E6E9FF">A, Light paths.</span> One quiet line at the foot, CONVERT in it. On a press each count drops and its dotted path draws up to the pad, like the routes on the holo table in Scales's scene; the ring flashes when the omni forms.</p>
<p><span class="cap" style="color:#E6E9FF">B, Glass orb.</span> The converter as an object: a small glass sphere at the foot, after the orb in Flux's cockpit. The four sit inside it and spiral into one omni; counts hang off it on hairlines. The most to build (a sphere shader or a rendered sprite).</p>
<p><span class="cap" style="color:#E6E9FF">C, The pad is the button (recommended).</span> The pier's own landing ring is CONVERT: press the thing in the picture, the same thing you asked for on the Artifacts tab. The counts are one line at the foot and nothing else is drawn. The ring pulses as each crystal lands. On a pad, A presses it as before.</p>
<p><span class="cap" style="color:#E6E9FF">Colour.</span> CONVERT is drawn Lime, the call to action; the build has it gold (Purchase). Say if gold stays.</p>
</div>
</div>
</div>
</x-dc>
<script type="text/x-dc" data-dc-script data-props='{"$preview":{"width":1920,"height":900}}'>
class Component extends DCLogic {
  renderVals() { return {}; }
}
</script>
</body>
</html>
"""

T = {
    "A": "Rev 139 A - Light paths: the five readings in one quiet line at the foot; on CONVERT each crystal&#x27;s path lights up to the pad - interactive",
    "B": "Rev 139 B - Glass orb: the converter as a small glass sphere at the foot, the four inside fuse into the omni - interactive",
    "C": "Rev 139 C - The pad is the button: press the pier&#x27;s own ring; the counts are one line at the foot (recommended) - interactive",
}

if __name__ == "__main__":
    ROOT.mkdir(parents=True, exist_ok=True)
    (ROOT / "R139A.dc.html").write_text(board(T["A"], "", A_BODY, A_CONSTS, A_RENDER))
    (ROOT / "R139B.dc.html").write_text(board(T["B"], B_CSS, B_BODY, B_CONSTS, B_RENDER))
    (ROOT / "R139C.dc.html").write_text(board(T["C"], C_CSS, C_BODY, C_CONSTS, C_RENDER))
    (ROOT / "R139Compare.dc.html").write_text(CMP)
    (ROOT / "R139Notes.dc.html").write_text(NOTES)
    print("wrote", ROOT)
