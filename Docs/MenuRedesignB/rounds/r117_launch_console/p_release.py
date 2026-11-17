from common import *


def stepper():
    return ('<section class="pn sl rise d1" style="left:0;top:0;width:1624px;height:96px;padding:0 28px;display:flex;align-items:center;gap:8px" aria-label="Release steps">'
            '<sc-for list="{{rsteps}}" as="s" hint-placeholder-count="5">'
            '<button class="pick" style="position:relative;flex:1;height:96px;display:flex;align-items:center;gap:14px;padding:0 8px" aria-current="{{s.cur}}" onClick="{{s.pick}}">'
            '<span class="num" style="width:34px;height:34px;display:inline-flex;align-items:center;justify-content:center;font-size:15px;color:{{s.ncol}};background:{{s.nbg}};box-shadow:inset 0 0 0 1.5px {{s.ring}};clip-path:polygon(7px 0,100% 0,100% calc(100% - 7px),calc(100% - 7px) 100%,0 100%,0 7px)">'
            '<sc-if value="{{s.done}}" hint-placeholder-val="{{ false }}">' + ico(I["check"], 18, 2.4) + '</sc-if><sc-if value="{{s.todo}}" hint-placeholder-val="{{ true }}">{{s.k}}</sc-if></span>'
            '<span><span class="cap" style="display:block;font-size:15px;color:{{s.tcol}}">{{s.n}}</span><span class="sm" style="display:block;font-size:14px;color:var(--to)">{{s.sub}}</span></span>'
            '<span class="abs" style="left:8px;right:8px;bottom:0;height:2px;background:{{s.bar}};transition:background-color .3s"></span></button></sc-for></section>')


def what():
    return (
        '<sc-if value="{{p.what}}" hint-placeholder-val="{{ true }}"><div class="fade">'
        '<div class="lab">Release from</div>'
        '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px;margin:12px 0 26px">'
        '<sc-for list="{{branches}}" as="b" hint-placeholder-count="3"><button class="pick lift sls" style="height:112px;padding:16px 18px;box-sizing:border-box;background:{{b.bg}};box-shadow:{{b.ring}};opacity:{{b.op}}" aria-pressed="{{b.pressed}}" onClick="{{b.pick}}">'
        '<span style="display:flex;justify-content:space-between;align-items:center"><span class="h3" style="color:var(--tx)">{{b.n}}</span><span class="st {{b.cls}}"><i></i>{{b.s}}</span></span>'
        '<span class="sm" style="display:block;margin-top:10px;color:var(--ts)">{{b.sub}}</span></button></sc-for></div>'
        '<div style="display:flex;gap:32px;align-items:flex-start">'
        '<div style="width:260px"><label class="lab" for="ver" style="display:block">Version</label>'
        '<input id="ver" class="num" value="{{v.version}}" onInput="{{v.setVersion}}" style="margin-top:12px;width:100%;box-sizing:border-box;height:56px;padding:0 16px;background:color-mix(in srgb,var(--gd) 80%,transparent);border:0;box-shadow:inset 0 0 0 1px var(--ts);color:var(--tx);font-family:Aldrich,sans-serif;font-size:22px;outline-color:var(--sel)"></div>'
        '<div style="flex:1"><div class="lab">Platforms</div>'
        '<div style="display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin-top:12px">'
        '<sc-for list="{{rplats}}" as="q" hint-placeholder-count="4"><button class="pick sls lift" style="height:56px;display:flex;align-items:center;gap:10px;padding:0 14px;box-sizing:border-box;background:{{q.bg}};box-shadow:{{q.ring}};color:{{q.col}}" aria-pressed="{{q.pressed}}" onClick="{{q.pick}}">'
        + icon_switch('q', ['phone', 'monitor', 'globe'], 18) + '<span class="cap" style="font-size:14px">{{q.n}}</span></button></sc-for></div></div></div>'
        '<div class="lab" style="margin:26px 0 12px">Vessels in this release ({{v.vcount}} of 6)</div>'
        '<div style="display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:10px">'
        '<sc-for list="{{vessels}}" as="h" hint-placeholder-count="6"><button class="pick lift" aria-pressed="{{h.pressed}}" onClick="{{h.pick}}" style="opacity:{{h.op}}">'
        '<span class="hullbox sls" style="display:block;height:92px;box-shadow:{{h.ring}}"><img class="hull" src="{{h.src}}" alt=""></span>'
        '<span class="sm" style="display:block;margin-top:8px;color:var(--tx)">{{h.n}}</span></button></sc-for></div>'
        '</div></sc-if>')


def checks():
    return (
        '<sc-if value="{{p.checks}}" hint-placeholder-val="{{ false }}"><div class="fade">'
        '<div style="display:flex;align-items:baseline;gap:16px"><span class="lab">Checked just now</span><span class="sm soft">{{c.sum}}</span></div>'
        '<div style="margin-top:14px">'
        '<sc-for list="{{c.rows}}" as="k" hint-placeholder-count="7">'
        '<div style="display:flex;align-items:center;gap:18px;height:68px;border-bottom:1px solid color-mix(in srgb,var(--ln) 55%,transparent)">'
        '<span style="position:relative;width:28px;height:28px;flex:none">'
        '<span class="abs hideat" style="inset:6px;border:2px solid var(--sel);border-right-color:transparent;border-radius:50%;animation-delay:0s,{{k.d}}"></span>'
        '<span class="abs showat" style="inset:0;display:flex;align-items:center;justify-content:center;color:{{k.col}};box-shadow:inset 0 0 0 1.5px {{k.col}};animation-delay:{{k.d}}">'
        '<sc-if value="{{k.ok}}" hint-placeholder-val="{{ true }}">' + ico(I["check"], 16, 2.4) + '</sc-if>'
        '<sc-if value="{{k.bad}}" hint-placeholder-val="{{ false }}">' + ico(I["x"], 14, 2.4) + '</sc-if>'
        '<sc-if value="{{k.wn}}" hint-placeholder-val="{{ false }}">' + ico('<path d="M6 12h12"/>', 14, 2.4) + '</sc-if></span></span>'
        '<span style="flex:1"><span style="display:block;font-size:18px">{{k.n}}</span><span class="sm" style="display:block;margin-top:2px;color:{{k.dcol}}">{{k.detail}}</span></span>'
        '<span class="showat" style="animation-delay:{{k.d}}"><span class="st {{k.cls}}"><i></i>{{k.s}}</span></span>'
        '<sc-if value="{{k.fix}}" hint-placeholder-val="{{ false }}"><button class="btn def s showat" type="button" style="animation-delay:{{k.d}}" onClick="{{k.doFix}}">{{k.fixLabel}}</button></sc-if>'
        '</div></sc-for></div></div></sc-if>')


def notes():
    return (
        '<sc-if value="{{p.notes}}" hint-placeholder-val="{{ false }}"><div class="fade" style="display:flex;gap:28px">'
        '<div style="flex:1.3"><label class="lab" for="pn">For players, in the game and on the store</label>'
        '<textarea id="pn" style="display:block;margin-top:12px;width:100%;height:470px;box-sizing:border-box;padding:18px;resize:none;background:color-mix(in srgb,var(--gd) 80%,transparent);border:0;box-shadow:inset 0 0 0 1px var(--ts);color:var(--tx);font-family:Aldrich,sans-serif;font-size:18px;line-height:1.6;outline-color:var(--sel)">'
        'Cosmic Shore 0.9.5\n\nNew\n- Voyages: the in-flight HUD shows your goal, the Ark\'s hull and the anchor timer.\n- Serpent: four intensities of its Time game, from first flight to very hard.\n\nBetter\n- The Sparrow keeps its missile lock while boosting.\n- Squad invites survive a rejoin.\n\nThank you, Tadpoles. Tell us what you think in the game\'s feedback button.</textarea></div>'
        '<div style="flex:1"><div class="lab">For the team, from the merged pull requests</div>'
        '<div style="margin-top:12px">'
        '<sc-for list="{{prs}}" as="r" hint-placeholder-count="6"><div class="rise" style="display:flex;gap:14px;align-items:center;height:56px;border-bottom:1px solid color-mix(in srgb,var(--ln) 55%,transparent);animation-delay:{{r.d}}">'
        '<span class="mono soft" style="width:58px;font-size:14px">{{r.n}}</span><span class="sm" style="flex:1">{{r.t}}</span><span class="sm off">{{r.who}}</span></div></sc-for></div>'
        '<button class="btn def s" type="button" style="margin-top:22px">Write them again from the PRs</button></div></div></sc-if>')


def audience():
    return (
        '<sc-if value="{{p.aud}}" hint-placeholder-val="{{ false }}"><div class="fade">'
        '<div class="lab">Who gets it</div>'
        '<div style="display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:12px;margin-top:12px">'
        '<sc-for list="{{rtiers}}" as="t" hint-placeholder-count="5"><button class="pick sls" style="position:relative;height:200px;padding:18px;box-sizing:border-box;background:{{t.bg}};box-shadow:{{t.ring}};opacity:{{t.op}};cursor:{{t.cur}}" aria-pressed="{{t.pressed}}" aria-disabled="{{t.locked}}" onClick="{{t.pick}}">'
        '<span class="lab" style="display:block;font-size:14px">{{t.k}}</span><span class="h3" style="display:block;margin-top:10px;color:var(--tx)">{{t.n}}</span>'
        '<span class="sm" style="display:block;margin-top:8px;color:var(--ts);line-height:1.45">{{t.who}}</span>'
        '<span class="abs sm" style="left:18px;bottom:16px;display:flex;align-items:center;gap:8px;color:{{t.fcol}}">'
        '<sc-if value="{{t.isLocked}}" hint-placeholder-val="{{ false }}">' + ico(I["lock"], 16) + '</sc-if>{{t.foot}}</span></button></sc-for></div>'
        '<div style="display:flex;gap:48px;margin-top:34px">'
        '<div><div class="lab">Testers</div><div style="display:flex;align-items:baseline;gap:10px;margin-top:10px"><span class="num" style="font-size:44px">38</span><span class="sm soft">Friends, Family and Tadpoles on the alpha list</span></div></div>'
        '<div><div class="lab">Android rollout</div><div style="display:flex;gap:8px;margin-top:16px">'
        '<sc-for list="{{rollout}}" as="r" hint-placeholder-count="3"><button class="chip" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}">{{r.n}}</button></sc-for></div></div>'
        '<div><div class="lab">Feedback goes to</div><div class="sm" style="margin-top:16px;line-height:1.6">The in-game feedback button, then Signals</div></div>'
        '</div></div></sc-if>')


def launch():
    rows = ('<sc-for list="{{L.rows}}" as="r" hint-placeholder-count="4"><div style="display:flex;align-items:center;gap:16px;height:58px">'
            '<span class="cap" style="width:150px;font-size:14px">{{r.n}}</span>'
            '<span style="position:relative;flex:1;height:6px;background:color-mix(in srgb,var(--ln) 55%,transparent)"><span class="abs fill" style="left:0;top:0;bottom:0;width:100%;background:var(--sel);animation-duration:{{r.dur}}"></span></span>'
            '<span class="sm showat" style="width:170px;color:var(--cta);display:flex;align-items:center;gap:8px;animation-delay:{{r.dur}}">' + ico(I["check"], 16, 2.4) + '{{r.done}}</span>'
            '<button class="pick sm" style="color:var(--dg)" type="button">{{r.back}}</button></div></sc-for>')
    return (
        '<sc-if value="{{p.go}}" hint-placeholder-val="{{ false }}"><div class="fade">'
        '<sc-if value="{{L.ready}}" hint-placeholder-val="{{ true }}">'
        '<div style="display:flex;gap:40px;align-items:center;height:560px">'
        '<div style="position:relative;width:420px;height:420px;flex:none">'
        '<svg class="abs" width="420" height="420" viewBox="0 0 420 420" aria-hidden="true"><circle cx="210" cy="210" r="196" fill="none" style="stroke:var(--ln)" stroke-dasharray="2 10"/>'
        '<circle cx="210" cy="210" r="150" fill="none" style="stroke:var(--ln)"/><circle cx="210" cy="210" r="104" fill="none" style="stroke:var(--ln)" stroke-dasharray="1 6"/></svg>'
        '<div class="abs orbit" style="left:0;top:0;width:420px;height:420px"><span class="abs" style="left:203px;top:53px;width:14px;height:14px;transform:rotate(45deg);background:var(--cm)"></span></div>'
        '<div class="abs" style="left:110px;top:150px;width:200px;text-align:center"><div class="lab">Ready to release</div><div class="num" style="font-size:48px;margin-top:8px">{{v.version}}</div><div class="sm soft" style="margin-top:6px">{{L.line}}</div></div></div>'
        '<div style="flex:1"><div class="h2">Release {{v.version}} to {{L.aud}}</div>'
        '<div class="sm soft" style="margin-top:14px;line-height:1.6;max-width:560px">{{L.what}} You can halt a rollout or roll back each platform afterwards, but testers who already have the build keep it.</div>'
        '<div style="display:flex;gap:14px;margin-top:34px"><button class="btn commit" type="button" style="height:64px;padding:0 34px;font-size:20px" onClick="{{L.go}}">' + ico(I["Release"], 22, 2) + 'Release {{v.version}}</button></div>'
        '<div class="sm off" style="margin-top:14px">Every step of the runbook is recorded on the console.</div></div></div></sc-if>'
        '<sc-if value="{{L.live}}" hint-placeholder-val="{{ false }}">'
        '<div style="display:flex;gap:40px;align-items:flex-start">'
        '<div style="position:relative;width:360px;height:360px;flex:none">'
        '<svg class="abs" width="360" height="360" viewBox="0 0 360 360" aria-hidden="true"><circle cx="180" cy="180" r="160" fill="none" style="stroke:var(--ln)"/>'
        '<circle class="sweep" cx="180" cy="180" r="160" fill="none" style="stroke:var(--sel)" stroke-width="3" pathLength="1000" transform="rotate(-90 180 180)"/>'
        '<circle cx="180" cy="180" r="120" fill="none" style="stroke:var(--ln)" stroke-dasharray="1 6"/></svg>'
        '<div class="abs" style="left:0;top:0;width:360px;height:360px"><span class="abs pulse" style="left:166px;top:166px;width:28px;height:28px;box-shadow:0 0 0 2px var(--sel);transform:rotate(45deg)"></span>'
        '<span class="abs" style="left:172px;top:172px;width:16px;height:16px;transform:rotate(45deg);background:var(--sel)"></span></div>'
        '<div class="abs showat" style="left:0;right:0;top:220px;text-align:center;animation-delay:7s"><div class="lab" style="color:var(--cta)">Live</div></div></div>'
        '<div style="flex:1"><div class="lab">Rolling out {{v.version}}</div><div class="h2" style="margin:10px 0 20px">On its way to {{L.aud}}</div>' + rows +
        '<div class="showat sm" style="margin-top:24px;padding:16px 18px;box-shadow:inset 0 0 0 1px var(--cta);line-height:1.5;animation-delay:7s">'
        'Released. 38 testers have it. Feedback and instrumented data start showing on Signals within the hour.</div>'
        '<div style="display:flex;gap:12px;margin-top:20px"><a class="btn def s showat" href="R117Signals.dc.html" style="animation-delay:7s">Watch Signals</a>'
        '<button class="btn def s" type="button" onClick="{{L.reset}}">Start over</button></div></div></div></sc-if>'
        '</div></sc-if>')


def summary():
    return ('<aside class="pn sl rise d3" style="left:1204px;top:120px;width:420px;height:816px" aria-label="This release">'
            '<div class="lab">This release</div>'
            '<div class="num" style="font-size:44px;margin:10px 0 2px">{{v.version}}</div>'
            '<div class="sm soft">{{sm.route}}</div><div class="hr" style="margin:22px 0"></div>'
            '<div class="lab" style="margin-bottom:8px">Platforms</div>'
            '<sc-for list="{{sm.plats}}" as="q" hint-placeholder-count="4"><div style="display:flex;align-items:center;gap:10px;height:40px">'
            '<span class="sm" style="flex:1;color:{{q.col}};text-decoration:{{q.strike}}">{{q.n}}</span><span class="st {{q.cls}}"><i></i>{{q.s}}</span></div></sc-for>'
            '<div class="hr" style="margin:18px 0"></div>'
            '<div style="display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px">'
            '<div><div class="lab" style="font-size:14px">Vessels</div><div style="font-size:22px;margin-top:6px">{{v.vcount}}</div></div>'
            '<div><div class="lab" style="font-size:14px">Commits</div><div style="font-size:22px;margin-top:6px">61</div></div>'
            '<div><div class="lab" style="font-size:14px">Audience</div><div class="sm" style="margin-top:6px">{{sm.aud}}</div></div>'
            '<div><div class="lab" style="font-size:14px">Checks</div><div class="sm" style="margin-top:6px;color:{{sm.ccol}}">{{sm.checks}}</div></div></div>'
            '<div class="abs" style="left:28px;right:28px;bottom:28px">'
            '<div class="lab" style="margin-bottom:10px">If something breaks</div>'
            '<div class="sm soft" style="line-height:1.55">Each platform keeps its own way back: halt the Android rollout, pause TestFlight, set the previous Steam build live, point the Toybox at the last build.</div></div></aside>')


def main():
    body = ('<section class="pn sl rise d2" style="left:0;top:120px;width:1180px;height:816px" aria-label="{{p.title}}">'
            '<div class="h2" style="margin-bottom:22px">{{p.title}}</div>'
            + what() + checks() + notes() + audience() + launch() +
            '<div class="abs" style="left:28px;right:28px;bottom:28px;display:flex;gap:12px;align-items:center">'
            '<sc-if value="{{p.canBack}}" hint-placeholder-val="{{ false }}"><button class="btn def" type="button" onClick="{{p.back}}">' + ico(I["back"], 18) + 'Back</button></sc-if>'
            '<span style="flex:1"></span><span class="sm" style="color:{{p.hcol}}">{{p.hint}}</span>'
            '<sc-if value="{{p.canNext}}" hint-placeholder-val="{{ true }}"><button class="btn cta" type="button" onClick="{{p.next}}">{{p.nextLabel}}' + ico(I["arrow"], 18, 2) + '</button></sc-if></div></section>')
    return stepper() + body + summary()


EXTRA = (".hideat{animation:spin 1.1s linear infinite,hide .01s forwards}"
         "@keyframes hide{to{opacity:0;visibility:hidden}}"
         ".showat{opacity:0;animation:show .35s cubic-bezier(.34,1.56,.64,1) forwards}"
         "@keyframes show{from{opacity:0;transform:scale(.7)}to{opacity:1;transform:none}}"
         ".fill{transform-origin:0 50%;animation:fillx 4s cubic-bezier(.4,0,.2,1) both}"
         "@keyframes fillx{from{transform:scaleX(0)}to{transform:scaleX(1)}}"
         ".orbit{animation:spin 18s linear infinite}"
         ".sweep{stroke-dasharray:1000;stroke-dashoffset:1000;animation:sweep 7s cubic-bezier(.4,0,.2,1) forwards}"
         "@keyframes sweep{to{stroke-dashoffset:0}}"
         "[data-mo=off] .showat{opacity:1}")

LOGIC = r"""
const H = """ + str(HULL).replace("'", '"') + r""";
const SIX = ['Sparrow', 'Dolphin', 'Squirrel', 'Rhino', 'Manta', 'Serpent'];
const STEPS = [['What', 'Branch, platforms, vessels'], ['Checks', 'Gates run live'], ['Notes', 'For players and the team'], ['Audience', 'Who gets it'], ['Launch', 'Release and watch']];
const TITLES = ['What are you releasing?', 'Is it safe to release?', 'What changed?', 'Who gets it?', 'Launch'];
const BRANCHES = [{ n: 'Development', cls: 'go', s: 'Green', sub: 'Test builds for the team, on Drive' }, { n: 'Master', cls: 'go', s: 'Green', sub: 'The alpha, for Friends, Family and Tadpoles' }, { n: 'Release', cls: 'none', s: 'Missing', sub: 'Opens in Beta Phase 2', locked: true }];
const PLATS = [{ k: 'android', n: 'Android', icon: 'phone' }, { k: 'ios', n: 'iOS', icon: 'phone' }, { k: 'windows', n: 'Windows', icon: 'monitor' }, { k: 'web', n: 'Web Toybox', icon: 'globe' }];
const TIERS = [{ k: 'Alpha access', n: 'Friends, Family, Tadpoles', who: 'The alpha list, invited by hand', foot: 'Open now' },
  { k: 'Pre-release', n: 'Wider friends', who: 'Pre-release access from Master', foot: 'Unlocks at Gate 1', locked: true },
  { k: 'Beta access', n: 'Beta keys', who: 'Limited keys, one at a time', foot: 'Unlocks at Gate 1', locked: true },
  { k: 'Early access', n: '$15', who: 'Paid early access on the stores', foot: 'Unlocks at Gate 3', locked: true },
  { k: 'Full release', n: 'Free to play', who: 'Everyone', foot: 'Unlocks at Gate 4', locked: true }];
const PRS = [['#2156', 'Voyages playtest: in-game HUD', 'Shombith'], ['#2148', 'Sparrow keeps missile lock on boost', 'Caleb'], ['#2141', 'Serpent Time ladder I1 to I4', 'Yash'],
  ['#2137', 'Squad invite survives rejoin', 'Garrett'], ['#2130', 'Colour sets list in Profile', 'Shombith'], ['#2122', 'Bug Ledger fixes from playtests', 'Caleb']];
const BACK = { android: 'Halt rollout', ios: 'Pause TestFlight', windows: 'Previous build', web: 'Roll back' };
class Component extends DCLogic {
  constructor(p) { super(p); this.state = Object.assign({}, UI0, { step: 0, br: 1, version: '0.9.5', pl: { android: true, ios: true, windows: true, web: true },
    ves: [true, true, true, true, true, true], tier: 0, roll: 0, launched: false, runId: 0 }); }
  renderVals() {
    const st = this.state, step = st.step;
    const picked = PLATS.filter((q) => st.pl[q.k]);
    const iosBad = !!st.pl.ios;
    const steps = STEPS.map((s, i) => ({ n: s[0], sub: s[1], k: String(i + 1), done: i < step, todo: i >= step, cur: i === step ? 'step' : 'false',
      ring: i === step ? 'var(--sel)' : (i < step ? 'var(--cta)' : 'var(--ln)'), ncol: i < step ? 'var(--cta)' : 'var(--tx)', nbg: i === step ? 'color-mix(in srgb,var(--sel) 16%,transparent)' : 'transparent',
      tcol: i === step ? 'var(--tx)' : 'var(--ts)', bar: i === step ? 'var(--sel)' : 'transparent', pick: () => { if (!st.launched) this.setState({ step: i }); } }));
    const togglePl = (k) => { const o = Object.assign({}, st.pl); o[k] = !o[k]; this.setState({ pl: o }); };
    const rowsC = [
      { n: 'Guard on ' + BRANCHES[st.br].n, detail: 'Every commit checked, last 3 h ago', ok: true },
      { n: 'Unity compile and EditMode tests', detail: '412 tests passed on the reference compile', ok: true },
      { n: 'QA blockers', detail: 'None open for the alpha', ok: true },
      { n: 'Hand test list', detail: '3 of 5 done; the voyage raid and the squad rejoin are left', warn: true },
      { n: 'Live ops flags', detail: 'Production config is 9 days old; nothing in this release needs a new flag', ok: true },
      { n: 'iOS signing', detail: iosBad ? 'Export compliance is not answered, so TestFlight refuses the build' : 'iOS is not in this release', bad: iosBad, ok: !iosBad,
        fix: iosBad, fixLabel: 'Leave iOS out', doFix: () => togglePl('ios') },
      { n: 'Rollback ready', detail: 'The previous build is kept on every platform', ok: true } ];
    const c = { rows: rowsC.map((r, i) => ({ n: r.n, detail: r.detail, ok: !r.bad && !r.warn, bad: !!r.bad, wn: !!r.warn, fix: !!r.fix, fixLabel: r.fixLabel || '', doFix: r.doFix || (() => {}),
      d: (0.5 + i * 0.45).toFixed(2) + 's', cls: r.bad ? 'nogo' : (r.warn ? 'risk' : 'go'), s: r.bad ? 'Blocks' : (r.warn ? 'Not done' : 'Passed'),
      col: r.bad ? 'var(--dg)' : (r.warn ? 'var(--dg)' : 'var(--cta)'), dcol: r.bad ? 'var(--dg)' : 'var(--ts)' })) };
    const blocks = rowsC.filter((r) => r.bad).length, warns = rowsC.filter((r) => r.warn).length;
    c.sum = blocks ? blocks + ' blocks the release, ' + warns + ' to finish' : 'Nothing blocks the release; ' + warns + ' to finish';
    const T = TIERS[st.tier];
    const p = { what: step === 0, checks: step === 1, notes: step === 2, aud: step === 3, go: step === 4, title: TITLES[step],
      canBack: step > 0 && !st.launched, canNext: step < 4, nextLabel: step === 3 ? 'Review' : 'Next',
      hint: step === 1 && blocks ? 'Leave iOS out, or fix its signing, to go on' : '', hcol: 'var(--dg)',
      back: () => this.setState({ step: step - 1 }), next: () => { if (!(step === 1 && blocks)) this.setState({ step: step + 1 }); } };
    const L = { ready: !st.launched, live: st.launched, aud: T.n, line: picked.length + ' platforms, ' + st.ves.filter(Boolean).length + ' vessels',
      what: 'This sends ' + BRANCHES[st.br].n + ' to ' + picked.map((q) => q.n).join(', ') + '.',
      go: () => this.setState({ launched: true }), reset: () => this.setState({ launched: false, step: 0 }),
      rows: picked.map((q, i) => ({ n: q.n, dur: (3 + i * 1.2).toFixed(1) + 's', done: q.k === 'android' ? 'Live for 10%' : (q.k === 'web' ? 'Live on /play' : 'Live'), back: BACK[q.k] })) };
    return { ui: uiVals(this), rsteps: steps, p: p, c: c, L: L,
      v: { version: st.version, vcount: String(st.ves.filter(Boolean).length), setVersion: (e) => this.setState({ version: e.target.value }) },
      branches: BRANCHES.map((b, i) => ({ n: b.n, cls: b.cls, s: b.s, sub: b.sub, pressed: on(st.br === i), op: b.locked ? .55 : 1,
        bg: st.br === i ? 'color-mix(in srgb,var(--sel) 10%,var(--g))' : 'color-mix(in srgb,var(--s) 45%,transparent)',
        ring: st.br === i ? 'inset 0 0 0 1px var(--sel)' : 'inset 0 0 0 1px color-mix(in srgb,var(--ln) 60%,transparent)', pick: () => { if (!b.locked) this.setState({ br: i }); } })),
      rplats: PLATS.map((q) => Object.assign(icf(q.icon), { n: q.n, pressed: on(st.pl[q.k]), col: st.pl[q.k] ? 'var(--tx)' : 'var(--to)',
        bg: st.pl[q.k] ? 'color-mix(in srgb,var(--sel) 10%,var(--g))' : 'transparent', ring: st.pl[q.k] ? 'inset 0 0 0 1px var(--sel)' : 'inset 0 0 0 1px color-mix(in srgb,var(--ln) 60%,transparent)',
        pick: () => togglePl(q.k) })),
      vessels: SIX.map((n, i) => ({ n: n, src: H[n], pressed: on(st.ves[i]), op: st.ves[i] ? 1 : .4, ring: st.ves[i] ? 'inset 0 0 0 1px var(--sel)' : 'none',
        pick: () => { const a = st.ves.slice(); a[i] = !a[i]; this.setState({ ves: a }); } })),
      prs: PRS.map((r, i) => ({ n: r[0], t: r[1], who: r[2], d: (0.1 + i * 0.07).toFixed(2) + 's' })),
      rtiers: TIERS.map((t, i) => ({ k: t.k, n: t.n, who: t.who, foot: t.foot, isLocked: !!t.locked, locked: t.locked ? 'true' : 'false', pressed: on(st.tier === i),
        op: t.locked ? .5 : 1, cur: t.locked ? 'not-allowed' : 'pointer', fcol: t.locked ? 'var(--to)' : 'var(--cta)',
        bg: st.tier === i ? 'color-mix(in srgb,var(--sel) 10%,var(--g))' : 'color-mix(in srgb,var(--s) 40%,transparent)',
        ring: st.tier === i ? 'inset 0 0 0 1px var(--sel)' : 'inset 0 0 0 1px color-mix(in srgb,var(--ln) 55%,transparent)', pick: () => { if (!t.locked) this.setState({ tier: i }); } })),
      rollout: ['10%', '50%', '100%'].map((n, i) => ({ n: n, pressed: on(st.roll === i), pick: () => this.setState({ roll: i }) })),
      sm: { route: BRANCHES[st.br].n + ' to ' + T.n, aud: T.n, checks: blocks ? blocks + ' blocks' : (warns ? warns + ' to finish' : 'All passed'), ccol: blocks ? 'var(--dg)' : 'var(--tx)',
        plats: PLATS.map((q) => ({ n: q.n, col: st.pl[q.k] ? 'var(--tx)' : 'var(--to)', strike: st.pl[q.k] ? 'none' : 'line-through',
          cls: !st.pl[q.k] ? 'none' : (q.k === 'ios' ? 'nogo' : (q.k === 'windows' ? 'risk' : 'go')), s: !st.pl[q.k] ? 'Left out' : (q.k === 'ios' ? 'No-go' : (q.k === 'windows' ? 'At risk' : 'Go')) })) } };
  }
}
"""


def build():
    body = shell("Release", "Release", "Perform a release, any time: five steps from a branch to the people playing it", main())
    return board("Rev 117 Release - perform a release in five steps", body, LOGIC, EXTRA)
