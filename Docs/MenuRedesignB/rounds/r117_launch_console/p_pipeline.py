from common import *

W, GAP = 370, 48


def lanes():
    out = []
    for i in range(4):
        x = i * (W + GAP)
        out.append(
            '<section class="pn sl rise d%d" style="left:%dpx;top:0;width:%dpx;height:620px;box-shadow:{{L%d.ring}}" aria-label="{{L%d.name}}">' % (i + 1, x, W, i, i)
            + '<div style="display:flex;align-items:center;justify-content:space-between"><span class="lab">{{L%d.order}}</span><span class="st {{L%d.cls}}"><i></i>{{L%d.state}}</span></div>' % (i, i, i)
            + '<div class="h2" style="margin:14px 0 4px">{{L%d.name}}</div>' % i
            + '<div class="mono soft sm">{{L%d.branch}}</div>' % i
            + '<div class="hr" style="margin:18px 0"></div>'
            + '<sc-if value="{{L%d.live}}" hint-placeholder-val="{{ true }}">' % i
            + '<div style="display:flex;align-items:baseline;gap:10px"><span class="num" style="font-size:44px">{{L%d.vessels}}</span><span class="lab">vessels</span></div>' % i
            + '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:6px;margin:14px 0 6px">'
            + '<sc-for list="{{L%d.hulls}}" as="h" hint-placeholder-count="6"><div class="hullbox sls" style="height:64px"><img class="hull" src="{{h.src}}" alt="{{h.n}}"></div></sc-for></div>' % i
            + '<div class="sm off" style="height:20px">{{L%d.more}}</div>' % i
            + '<div class="lab" style="margin:18px 0 10px">Feeds</div>'
            + '<sc-for list="{{L%d.feeds}}" as="f" hint-placeholder-count="3"><div class="sm" style="display:flex;align-items:center;gap:10px;height:30px"><span style="width:6px;height:6px;transform:rotate(45deg);background:var(--ts)"></span>{{f.t}}</div></sc-for>' % i
            + '<div class="abs sm soft" style="left:28px;right:28px;bottom:24px;display:flex;justify-content:space-between"><span>{{L%d.head}}</span><span class="num">{{L%d.when}}</span></div>' % (i, i)
            + '</sc-if>'
            + '<sc-if value="{{L%d.missing}}" hint-placeholder-val="{{ false }}">' % i
            + '<div style="height:330px;display:flex;flex-direction:column;justify-content:center;gap:16px;box-shadow:inset 0 0 0 1px var(--ln);padding:28px;box-sizing:border-box;background:repeating-linear-gradient(135deg,transparent 0 14px,color-mix(in srgb,var(--ln) 25%,transparent) 14px 15px)">'
            + '<div class="h3">Not created yet</div><div class="sm soft" style="line-height:1.5">Release opens with Beta Phase 2: beta keys, one at a time, and the activation data they bring.</div></div>'
            + '<div class="lab" style="margin:22px 0 10px">Will feed</div>'
            + '<sc-for list="{{L%d.feeds}}" as="f" hint-placeholder-count="2"><div class="sm soft" style="display:flex;align-items:center;gap:10px;height:30px"><span style="width:6px;height:6px;transform:rotate(45deg);box-shadow:inset 0 0 0 1px var(--ts)"></span>{{f.t}}</div></sc-for>' % i
            + '<button class="btn def s abs" type="button" style="left:28px;bottom:24px">Create the Release branch</button>'
            + '</sc-if></section>')
        if i < 3:
            gx = x + W
            dots = "".join('<span class="abs flow" style="left:0;top:3px;width:6px;height:6px;background:var(--sel);transform:rotate(45deg);animation:flow 2.4s %.1fs linear infinite"></span>' % (k * .8) for k in range(3))
            out.append(
                '<div class="abs" style="left:%dpx;top:96px;width:%dpx;height:12px;overflow:hidden" aria-hidden="true"><div class="abs" style="left:0;right:0;top:6px;height:1px;background:var(--ln)"></div>%s</div>' % (gx, GAP, dots)
                + '<button class="pick abs lift" style="left:%dpx;top:150px;width:40px;height:56px;display:flex;align-items:center;justify-content:center;color:var(--cm);box-shadow:inset 0 0 0 1px {{P%d.ring}};background:var(--g);clip-path:polygon(50%% 0,100%% 22%%,100%% 78%%,50%% 100%%,0 78%%,0 22%%)" aria-label="{{P%d.label}}" title="{{P%d.label}}" aria-pressed="{{P%d.pressed}}" onClick="{{P%d.pick}}">%s</button>'
                % (gx + 4, i, i, i, i, i, ico(I["arrow"], 18, 2)))
    return "".join(out)


def drawer():
    return (
        '<sc-if value="{{dr.on}}" hint-placeholder-val="{{ false }}">'
        '<section class="pn raised sl slidein" style="left:0;top:436px;width:1624px;height:500px;z-index:5;box-shadow:inset 0 0 0 1px var(--cm),0 -30px 80px rgba(0,0,0,.45)" aria-label="{{dr.title}}">'
        '<div style="display:flex;align-items:center;gap:16px"><span class="lab" style="color:var(--cm)">Promote</span><span class="h2" style="flex:1">{{dr.title}}</span>'
        '<button class="pick" style="width:40px;height:40px;display:inline-flex;align-items:center;justify-content:center;color:var(--ts)" aria-label="Close" onClick="{{dr.close}}">' + ico(I["x"], 20) + '</button></div>'
        '<div class="sm soft" style="margin-top:6px">{{dr.sub}}</div>'
        '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:32px;margin-top:24px">'
        '<div><div class="lab" style="margin-bottom:8px">What moves ({{dr.count}} commits)</div>'
        '<sc-for list="{{dr.commits}}" as="c" hint-placeholder-count="5"><div class="rise" style="display:flex;gap:12px;align-items:center;height:40px;border-bottom:1px solid color-mix(in srgb,var(--ln) 50%,transparent);animation-delay:{{c.d}}">'
        '<span class="mono soft" style="font-size:14px;width:64px">{{c.sha}}</span><span class="sm" style="flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">{{c.t}}</span><span class="sm off">{{c.who}}</span></div></sc-for></div>'
        '<div><div class="lab" style="margin-bottom:8px">Vessels</div>'
        '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:8px">'
        '<sc-for list="{{dr.vessels}}" as="v" hint-placeholder-count="6"><div><div class="hullbox sls" style="height:70px"><img class="hull" src="{{v.src}}" alt="{{v.n}}"></div>'
        '<div class="sm" style="margin-top:6px">{{v.n}}</div><div class="sm" style="font-size:14px;color:{{v.col}}">{{v.what}}</div></div></sc-for></div></div>'
        '<div><div class="lab" style="margin-bottom:8px">Checks</div>'
        '<sc-for list="{{dr.checks}}" as="k" hint-placeholder-count="4"><div style="display:flex;align-items:center;gap:12px;height:44px;border-bottom:1px solid color-mix(in srgb,var(--ln) 50%,transparent)">'
        '<span class="sm" style="flex:1">{{k.t}}</span><span class="st {{k.cls}}"><i></i>{{k.s}}</span></div></sc-for>'
        '<div style="display:flex;gap:12px;margin-top:28px"><button class="btn commit" type="button" onClick="{{dr.go}}">{{dr.cta}}</button><button class="btn def" type="button">Open the PR</button></div>'
        '<div class="sm" style="margin-top:12px;color:var(--cta);height:20px">{{dr.done}}</div></div>'
        '</div></section></sc-if>')


def schedule():
    return ('<section class="pn sl rise d6" style="left:0;top:644px;width:1624px;height:292px" aria-label="Schedule">'
            '<div class="lab">On a schedule</div>'
            '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:32px;margin-top:20px">'
            '<div><div class="h3">Promote a test build</div><div class="sm soft" style="margin-top:8px;line-height:1.5">Every third Wednesday, Development goes to the Android and Windows build branches.</div>'
            '<div class="sm" style="margin-top:14px;display:flex;gap:10px;align-items:center"><span class="soft">' + ico(I["clock"], 18) + '</span>Next on Wed 25 Nov</div></div>'
            '<div><div class="h3">Tag an internal build</div><div class="sm soft" style="margin-top:8px;line-height:1.5">Every Friday, Bleeding Edge is tagged so testers can name the build they played.</div>'
            '<div class="sm" style="margin-top:14px;display:flex;gap:10px;align-items:center"><span class="soft">' + ico(I["clock"], 18) + '</span>Next on Fri 20 Nov</div></div>'
            '<div><div class="h3">Guard Bleeding Edge</div><div class="sm soft" style="margin-top:8px;line-height:1.5">Every commit is checked and repaired. A red trunk stops promotion until it is green.</div>'
            '<div class="sm" style="margin-top:14px;display:flex;gap:10px;align-items:center"><span style="color:var(--cta)">' + ico(I["check"], 18, 2) + '</span>Green, 3 h ago</div></div>'
            '</div></section>')


EXTRA = "@keyframes flow{from{transform:translateX(-10px) rotate(45deg);opacity:0}20%{opacity:1}80%{opacity:1}to{transform:translateX(52px) rotate(45deg);opacity:0}}"

LOGIC = r"""
const H = """ + str(HULL).replace("'", '"') + r""";
const SIX = ['Sparrow', 'Dolphin', 'Squirrel', 'Rhino', 'Manta', 'Serpent'];
const LANES = [
  { order: '1  Prototype', name: 'Bleeding Edge', branch: 'bleeding-edge', cls: 'go', state: 'Green', vessels: '21', more: 'and 15 more in prototype', head: '760008b, Merge #2156', when: '2 h ago',
    feeds: ['Drive builds', 'Prototype feedback in Unity', 'Bug testing', 'Instrumented data'] },
  { order: '2  Test', name: 'Development', branch: 'development', cls: 'go', state: 'Green', vessels: '6', more: '', head: '4be1c90, promoted', when: '9 days ago',
    feeds: ['Drive builds', 'Bug testing', 'Instrumented data'] },
  { order: '3  Alpha', name: 'Master', branch: 'master', cls: 'risk', state: 'Behind', vessels: '6', more: '', head: '1d07a3e, promoted', when: '23 days ago',
    feeds: ['Alpha access', 'Friends, Family and Tadpoles feedback', 'Instrumented data'] },
  { order: '4  Beta', name: 'Release', branch: 'release', cls: 'none', state: 'Missing', missing: true, vessels: '', more: '', head: '', when: '',
    feeds: ['Beta access with limited keys', 'Activation, retention and referral data'] } ];
const PROMO = [
  { from: 'Bleeding Edge', to: 'Development', count: 142, sub: 'Brings the last nine days of work into the test branch. The 15 prototype vessels stay behind.' },
  { from: 'Development', to: 'Master', count: 61, sub: 'Puts the six vessels in front of Friends, Family and Tadpoles as the 0.9.5 alpha.' },
  { from: 'Master', to: 'Release', count: 0, sub: 'Release does not exist yet. It is created when Gate 1 passes.' } ];
const COMMITS = [['760008b', 'Voyages playtest: the in-game HUD', 'Shombith'], ['a91c3f2', 'Sparrow: missile lock holds on boost', 'Caleb'], ['5e02d1b', 'Serpent Time ladder, I1 to I4', 'Yash'],
  ['c3f88a0', 'Fix the squad invite on rejoin', 'Garrett'], ['0be7d41', 'Launch stages in the console', 'Shombith']];
const VWHAT = [['Tuned', 'var(--sel)'], ['No change', 'var(--to)'], ['New ability art', 'var(--sel)'], ['No change', 'var(--to)'], ['AI retrained', 'var(--sel)'], ['No change', 'var(--to)']];
class Component extends DCLogic {
  constructor(p) { super(p); this.state = Object.assign({}, UI0, { open: -1, promoted: -1 }); }
  renderVals() {
    const st = this.state, v = { ui: uiVals(this) };
    LANES.forEach((l, i) => {
      v['L' + i] = { order: l.order, name: l.name, branch: l.branch, cls: (st.promoted === i - 1 && i > 0) ? 'run' : l.cls,
        state: (st.promoted === i - 1 && i > 0) ? 'Promoting' : l.state, vessels: l.vessels, more: l.more, head: l.head, when: l.when,
        live: !l.missing, missing: !!l.missing, feeds: l.feeds.map((t) => ({ t: t })),
        hulls: SIX.map((n) => ({ n: n, src: H[n] })),
        ring: (st.open === i || st.open === i - 1) && st.open >= 0 ? 'inset 0 0 0 1px var(--sel)' : 'inset 0 0 0 1px color-mix(in srgb,var(--ln) 80%,transparent)' };
    });
    PROMO.forEach((p, i) => { v['P' + i] = { label: 'Promote ' + p.from + ' to ' + p.to, pressed: on(st.open === i),
      ring: st.open === i ? 'var(--cm)' : 'var(--ln)', pick: () => this.setState({ open: st.open === i ? -1 : i }) }; });
    const P = PROMO[Math.max(0, st.open)];
    v.dr = { on: st.open >= 0, title: P.from + ' to ' + P.to, sub: P.sub, count: String(P.count),
      commits: (P.count ? COMMITS : []).map((c, k) => ({ sha: c[0], t: c[1], who: c[2], d: (0.15 + k * 0.08).toFixed(2) + 's' })),
      vessels: SIX.map((n, k) => ({ n: n, src: H[n], what: VWHAT[k][0], col: VWHAT[k][1] })),
      checks: st.open === 2 ? [{ t: 'Gate 1 passed', s: 'Not yet', cls: 'nogo' }] :
        [{ t: 'Guard on ' + P.from, s: 'Green', cls: 'go' }, { t: 'Unity compile and EditMode', s: 'Passed', cls: 'go' }, { t: 'QA blockers', s: 'None open', cls: 'go' }, { t: 'Hand test list', s: '2 of 5 left', cls: 'risk' }],
      cta: st.open === 2 ? 'Waiting on Gate 1' : 'Promote to ' + P.to,
      done: st.promoted === st.open ? 'Promotion started. The guard checks it in about 20 minutes.' : '',
      close: () => this.setState({ open: -1 }), go: () => { if (st.open < 2) this.setState({ promoted: st.open }); } };
    return v;
  }
}
"""


def build():
    body = shell("Pipeline", "Pipeline", "Bleeding Edge to Development to Master to Release, and what each branch feeds", lanes() + schedule() + drawer())
    return board("Rev 117 Pipeline - the four branches", body, LOGIC, EXTRA)
