from common import *


def main():
    filters = ('<div class="abs rise d1" style="left:0;top:0;width:1040px;height:44px;display:flex;align-items:center;gap:10px">'
               '<sc-for list="{{filters}}" as="f" hint-placeholder-count="4"><button class="chip" type="button" aria-pressed="{{f.pressed}}" onClick="{{f.pick}}">{{f.n}}</button></sc-for>'
               '<span style="flex:1"></span><span class="sm soft">Built from the build profiles, uploaded where each platform tests</span></div>')
    matrix = (
        '<section class="pn sl rise d2" style="left:0;top:64px;width:1040px;height:560px" aria-label="Builds by platform and branch">'
        '<div style="display:grid;grid-template-columns:180px repeat(3,minmax(0,1fr));gap:12px;align-items:center">'
        '<span></span><span class="lab">Development</span><span class="lab">Master</span><span class="lab">Release</span></div>'
        '<sc-for list="{{rows}}" as="r" hint-placeholder-count="4">'
        '<div class="rise" style="display:grid;grid-template-columns:180px repeat(3,minmax(0,1fr));gap:12px;margin-top:14px;animation-delay:{{r.d}}">'
        '<div style="display:flex;align-items:center;gap:12px"><span class="soft" style="display:inline-flex">' + icon_switch('r', ['phone', 'monitor', 'globe'], 20) + '</span>'
        '<span class="cap" style="font-size:15px">{{r.n}}</span></div>'
        '<sc-for list="{{r.cells}}" as="c" hint-placeholder-count="3">'
        '<button class="pick lift sls" style="position:relative;height:104px;padding:14px 16px;box-sizing:border-box;background:{{c.bg}};box-shadow:{{c.ring}};overflow:hidden" aria-pressed="{{c.pressed}}" aria-label="{{c.label}}" onClick="{{c.pick}}">'
        '<span style="display:flex;align-items:center;justify-content:space-between"><span class="mono" style="font-size:15px;color:{{c.vcol}}">{{c.v}}</span><span class="st {{c.cls}}"><i></i>{{c.s}}</span></span>'
        '<span class="sm" style="display:block;margin-top:10px;color:var(--ts)">{{c.sub}}</span>'
        '<sc-if value="{{c.running}}" hint-placeholder-val="{{ false }}"><span class="abs" style="left:16px;right:16px;bottom:14px;height:4px;background:color-mix(in srgb,var(--ln) 60%,transparent)">'
        '<span class="abs bar" style="left:0;top:0;bottom:0;width:62%;background:var(--sel)"></span></span></sc-if></button>'
        '</sc-for></div></sc-for></section>')
    sizes = (
        '<section class="pn sl rise d3" style="left:0;top:648px;width:1040px;height:288px" aria-label="Build size">'
        '<div style="display:flex;align-items:baseline;justify-content:space-between"><span class="lab">{{sz.title}}</span><span class="sm soft">Store limit {{sz.limit}}</span></div>'
        '<div style="position:relative;height:170px;margin-top:22px;display:flex;align-items:flex-end;gap:14px">'
        '<div class="abs" style="left:0;right:0;top:{{sz.limitY}};border-top:1px dashed var(--dg)"></div>'
        '<sc-for list="{{sz.bars}}" as="b" hint-placeholder-count="10"><div style="flex:1;display:flex;flex-direction:column;align-items:center;gap:6px">'
        '<span class="sm num" style="font-size:14px;color:var(--ts)">{{b.v}}</span>'
        '<span class="grow" style="display:block;width:44px;height:{{b.h}};background:{{b.col}};animation-delay:{{b.d}}"></span></div></sc-for></div>'
        '<div style="display:flex;justify-content:space-between;margin-top:10px" class="sm off"><span>2190</span><span>Build</span><span>2211</span></div></section>')
    detail = (
        '<section class="pn sl rise d4" style="left:1064px;top:0;width:560px;height:936px" aria-label="{{b.label}}">'
        '<div class="lab">{{b.branch}}</div>'
        '<div style="display:flex;align-items:center;gap:14px;margin:10px 0 6px"><span class="h2" style="flex:1">{{b.platform}}</span><span class="st {{b.cls}}"><i></i>{{b.s}}</span></div>'
        '<div class="mono soft">{{b.v}}  ·  {{b.commit}}</div>'
        '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;margin:22px 0">'
        '<div><div class="lab" style="font-size:14px">Size</div><div class="num" style="font-size:22px;margin-top:4px">{{b.size}}</div></div>'
        '<div><div class="lab" style="font-size:14px">Time</div><div class="num" style="font-size:22px;margin-top:4px">{{b.time}}</div></div>'
        '<div><div class="lab" style="font-size:14px">Profile</div><div style="font-size:15px;margin-top:8px">{{b.profile}}</div></div></div>'
        '<div class="hr"></div>'
        '<div class="lab" style="margin:20px 0 8px">Steps</div>'
        '<div style="position:relative">'
        '<div class="abs" style="left:13px;top:14px;bottom:14px;width:1px;background:var(--ln)"></div>'
        '<sc-for list="{{b.steps}}" as="s" hint-placeholder-count="7">'
        '<div style="position:relative;display:flex;align-items:center;gap:18px;height:44px">'
        '<span style="position:relative;width:28px;height:28px;display:inline-flex;align-items:center;justify-content:center;background:var(--g);box-shadow:inset 0 0 0 1.5px {{s.ring}};color:{{s.col}}">'
        '<sc-if value="{{s.done}}" hint-placeholder-val="{{ true }}">' + ico(I["check"], 16, 2.4) + '</sc-if>'
        '<sc-if value="{{s.now}}" hint-placeholder-val="{{ false }}"><span class="spin" style="width:14px;height:14px;box-sizing:border-box;border:2px solid var(--sel);border-right-color:transparent;border-radius:50%"></span></sc-if>'
        '<sc-if value="{{s.fail}}" hint-placeholder-val="{{ false }}">' + ico(I["x"], 14, 2.4) + '</sc-if></span>'
        '<span class="sm" style="flex:1;color:{{s.tcol}}">{{s.n}}</span><span class="sm num off">{{s.t}}</span></div></sc-for></div>'
        '<sc-if value="{{b.failed}}" hint-placeholder-val="{{ false }}"><div class="sm" style="margin-top:10px;padding:12px 14px;box-shadow:inset 0 0 0 1px var(--dg);color:var(--dg);line-height:1.45">{{b.why}}</div></sc-if>'
        '<div class="hr" style="margin:20px 0"></div>'
        '<div class="lab" style="margin-bottom:8px">Goes to</div>'
        '<sc-for list="{{b.dest}}" as="d" hint-placeholder-count="2"><div style="display:flex;align-items:center;gap:12px;height:42px">'
        '<span class="soft" style="display:inline-flex">' + icon_switch('d', ['drive', 'phone', 'monitor', 'globe', 'cloud'], 18) + '</span>'
        '<span class="sm" style="flex:1">{{d.n}}</span><span class="sm" style="color:{{d.col}}">{{d.s}}</span></div></sc-for>'
        '<div class="abs" style="left:28px;right:28px;bottom:28px;display:flex;gap:12px">'
        '<button class="btn {{b.ctaCls}}" type="button" onClick="{{b.build}}">{{b.cta}}</button>'
        '<button class="btn def" type="button">' + ico(I["link"], 18) + 'Copy link</button></div>'
        '</section>')
    return filters + matrix + sizes + detail


EXTRA = (".bar{animation:bar 9s cubic-bezier(.4,0,.2,1) infinite;transform-origin:0 50%}"
         "@keyframes bar{0%{width:8%}85%{width:96%}100%{width:96%}}"
         ".grow{animation:growy 1s cubic-bezier(.22,1,.36,1) both;transform-origin:50% 100%}"
         "@keyframes growy{from{transform:scaleY(0)}to{transform:scaleY(1)}}")

LOGIC = r"""
const BR = ['Development', 'Master', 'Release'];
const ROWS = [
  { n: 'Android', icon: 'phone', group: 'Mobile', cells: [
    { v: '0.9.4 (2211)', cls: 'go', s: 'Passed', sub: 'Drive link, 3 h ago', size: '612 MB', time: '24 min', commit: '4be1c90' },
    { v: '0.9.3 (2190)', cls: 'go', s: 'Passed', sub: 'Play internal, 23 d ago', size: '598 MB', time: '23 min', commit: '1d07a3e' }, null ] },
  { n: 'iOS', icon: 'phone', group: 'Mobile', cells: [
    { v: '0.9.4 (2212)', cls: 'run', s: 'Building', sub: 'IL2CPP, about 11 min left', size: 'Not yet', time: '19 min', commit: '4be1c90', at: 4 },
    { v: '0.9.3 (2209)', cls: 'nogo', s: 'Failed', sub: 'Signing, 1 d ago', size: 'Not built', time: '31 min', commit: '1d07a3e', at: 5, why: 'Signing failed: export compliance is not answered in App Store Connect, so TestFlight refuses the upload.' }, null ] },
  { n: 'Windows', icon: 'monitor', group: 'Desktop', cells: [
    { v: '0.9.4 (2211)', cls: 'go', s: 'Passed', sub: 'Drive link, 3 h ago', size: '1.42 GB', time: '18 min', commit: '4be1c90' },
    { v: '0.9.3 (2190)', cls: 'risk', s: '2 warnings', sub: 'Steam Playtest, 23 d ago', size: '1.39 GB', time: '17 min', commit: '1d07a3e' }, null ] },
  { n: 'Web Toybox', icon: 'globe', group: 'Web', cells: [
    { v: '0.9.4 (2211)', cls: 'go', s: 'Passed', sub: 'Staged, 2 h ago', size: '88 MB', time: '12 min', commit: '4be1c90' },
    { v: '0.9.3 (2190)', cls: 'go', s: 'Live', sub: 'froglet.games/play', size: '86 MB', time: '12 min', commit: '1d07a3e' }, null ] } ];
const STEPS = { Android: ['Queued', 'Checkout', 'Import', 'Compile', 'IL2CPP', 'Sign the bundle', 'Upload'], iOS: ['Queued', 'Checkout', 'Import', 'Compile', 'IL2CPP and Xcode', 'Sign', 'Upload to TestFlight'],
  Windows: ['Queued', 'Checkout', 'Import', 'Compile', 'IL2CPP', 'Package', 'Upload'], 'Web Toybox': ['Queued', 'Checkout', 'Import', 'Compile', 'WebGL', 'Compress', 'Upload to R2'] };
const STEPT = ['0:12', '0:40', '6:05', '3:22', '9:48', '1:10', '2:31'];
const DEST = { Android: [['drive', 'Google Drive share link'], ['phone', 'Play Console internal track']], iOS: [['phone', 'TestFlight']],
  Windows: [['drive', 'Google Drive share link'], ['monitor', 'Steam Playtest branch']], 'Web Toybox': [['cloud', 'R2 staging'], ['globe', 'froglet.games/play']] };
const SIZE = { Android: [[560, 590, 588, 601, 596, 598, 603, 607, 610, 612], 650, 'MB', '650 MB'], iOS: [[702, 715, 716, 720, 731, 733, 740, 744, 0, 0], 800, 'MB', '800 MB'],
  Windows: [[1.31, 1.33, 1.33, 1.35, 1.36, 1.39, 1.39, 1.4, 1.41, 1.42], 1.6, 'GB', 'none, budget 1.6 GB'], 'Web Toybox': [[80, 82, 83, 83, 85, 86, 86, 87, 88, 88], 100, 'MB', 'budget 100 MB'] };
const FILTERS = ['All', 'Mobile', 'Desktop', 'Web'];
class Component extends DCLogic {
  constructor(p) { super(p); this.state = Object.assign({}, UI0, { r: 1, c: 0, f: 'All', started: {} }); }
  renderVals() {
    const st = this.state;
    const cellOf = (ri, ci) => { const c = ROWS[ri].cells[ci]; const k = ri + ':' + ci;
      if (c && st.started[k]) return Object.assign({}, c, { cls: 'run', s: 'Building', sub: 'Queued, then checkout', at: 1, why: '' });
      return c; };
    const rows = ROWS.filter((r) => st.f === 'All' || r.group === st.f).map((r) => { const ri = ROWS.indexOf(r);
      return Object.assign(icf(r.icon), { n: r.n, d: (0.1 + ri * 0.08).toFixed(2) + 's', cells: BR.map((b, ci) => { const c = cellOf(ri, ci), sel = st.r === ri && st.c === ci;
        return c ? { v: c.v, cls: c.cls, s: c.s, sub: c.sub, running: c.cls === 'run', pressed: on(sel), label: r.n + ' on ' + b, vcol: 'var(--tx)',
            bg: sel ? 'color-mix(in srgb,var(--sel) 10%,var(--g))' : 'color-mix(in srgb,var(--s) 45%,transparent)',
            ring: sel ? 'inset 0 0 0 1px var(--sel)' : 'inset 0 0 0 1px color-mix(in srgb,var(--ln) 60%,transparent)', pick: () => this.setState({ r: ri, c: ci }) }
          : { v: 'No branch yet', cls: 'none', s: 'None', sub: 'Opens in Beta Phase 2', running: false, pressed: on(sel), label: r.n + ' on Release', vcol: 'var(--to)',
            bg: 'transparent', ring: 'inset 0 0 0 1px color-mix(in srgb,var(--ln) 40%,transparent)', pick: () => this.setState({ r: ri, c: ci }) }; }) }); });
    const R = ROWS[st.r], C = cellOf(st.r, st.c);
    const at = !C ? -1 : (C.cls === 'run' || C.cls === 'nogo') ? C.at : 99;
    const b = C ? { platform: R.n, branch: BR[st.c], v: C.v, commit: C.commit, cls: C.cls, s: C.s, size: C.size, time: C.time, profile: R.n === 'Web Toybox' ? 'WebGL release' : R.n + ' release',
      label: R.n + ' build on ' + BR[st.c], failed: C.cls === 'nogo', why: C.why || '',
      steps: STEPS[R.n].map((n, k) => { const done = k < at, now = C.cls === 'run' && k === at, fail = C.cls === 'nogo' && k === at;
        return { n: n, done: done, now: now, fail: fail, t: done ? STEPT[k] : (now ? 'now' : ''), ring: fail ? 'var(--dg)' : (now ? 'var(--sel)' : (done ? 'var(--cta)' : 'var(--ln)')),
          col: fail ? 'var(--dg)' : 'var(--cta)', tcol: done || now || fail ? 'var(--tx)' : 'var(--to)' }; }),
      dest: DEST[R.n].map((d) => Object.assign(icf(d[0]), { n: d[1], s: C.cls === 'go' ? 'Uploaded' : 'Waiting', col: C.cls === 'go' ? 'var(--cta)' : 'var(--to)' })),
      cta: C.cls === 'run' ? 'Building' : 'Build again', ctaCls: C.cls === 'run' ? 'def' : 'cta', build: () => { const s = Object.assign({}, st.started); s[st.r + ':' + st.c] = true; this.setState({ started: s }); } }
      : { platform: R.n, branch: 'Release', v: 'No Release branch yet', commit: '', cls: 'none', s: 'None', size: '', time: '', profile: '', label: R.n + ' on Release', failed: false, why: '',
        steps: [], dest: [], cta: 'Build', ctaCls: 'def', build: () => {} };
    const S = SIZE[R.n], vals = S[0].filter((x) => x > 0);
    const sz = { title: R.n + ' size, last ' + vals.length + ' builds', limit: S[3], limitY: '0px',
      bars: vals.map((x, k) => ({ v: String(x), h: Math.round(150 * x / S[1]) + 'px', d: (0.3 + k * 0.05).toFixed(2) + 's',
        col: k === vals.length - 1 ? 'var(--data)' : 'color-mix(in srgb,var(--data) 45%,transparent)' })) };
    sz.limitY = '20px';
    return { ui: uiVals(this), rows: rows, b: b, sz: sz,
      filters: FILTERS.map((n) => ({ n: n, pressed: on(st.f === n), pick: () => this.setState({ f: n }) })) };
  }
}
"""


def build():
    body = shell("Builds", "Builds", "Every platform on every branch, and where each build goes to be tested", main())
    return board("Rev 117 Builds - every platform on every branch", body, LOGIC, EXTRA)
