from common import *

# The six stages along an arc (x, y inside the hero's track box 1040 x 240).
NODES = [(60 + i * 184, round(170 - 92 * math.sin(math.pi * i / 5))) for i in range(6)]


def track_svg():
    d = "M %d %d" % NODES[0]
    for (x0, y0), (x1, y1) in zip(NODES, NODES[1:]):
        cx = (x0 + x1) / 2
        d += " C %.0f %d, %.0f %d, %d %d" % (cx, y0, cx, y1, x1, y1)
    return ('<svg class="abs" style="left:0;top:0" width="1040" height="240" viewBox="0 0 1040 240" aria-hidden="true">'
            '<path d="' + d + '" fill="none" style="stroke:var(--ln)" stroke-width="2" stroke-dasharray="4 8"/>'
            '<path class="draw" d="' + d + '" fill="none" style="stroke:var(--tx)" stroke-width="2" pathLength="2400" '
            'stroke-dasharray="2400" stroke-dashoffset="2400"/></svg>')


def main():
    gates = ""
    for g in range(4):
        (x0, y0), (x1, y1) = NODES[g], NODES[g + 1]
        gx, gy = (x0 + x1) / 2, (y0 + y1) / 2
        gates += ('<div class="abs fade" style="left:%dpx;top:%dpx;width:14px;height:14px;transform:rotate(45deg);'
                  'background:var(--g);box-shadow:inset 0 0 0 1.5px var(--ts);animation-delay:%.1fs"></div>'
                  '<div class="abs lab fade" style="left:%dpx;top:%dpx;font-size:14px;animation-delay:%.1fs">Gate %d</div>'
                  % (gx - 7, gy - 7, 1 + g * .3, gx - 24, gy - 34, 1 + g * .3, g + 1))
    hero = (
        '<section class="pn sl rise d1" style="left:0;top:0;width:1624px;height:300px" aria-label="Launch stages">'
        '<div class="lab">{{sel.lab}}</div>'
        '<h2 class="num" style="font-size:48px;line-height:1.05;margin:10px 0 12px;font-weight:400">{{sel.n}}</h2>'
        '<div class="sm soft" style="width:480px;line-height:1.5">{{sel.vessels}}</div>'
        '<div class="hr" style="width:480px;margin:18px 0"></div>'
        '<div style="display:flex;gap:28px;width:500px"><div style="flex:1"><div class="lab" style="margin-bottom:6px">Access</div><div class="sm">{{sel.access}}</div></div>'
        '<div style="flex:1"><div class="lab" style="margin-bottom:6px">To move on</div><div class="sm">{{sel.gate}}</div></div></div>'
        '<div class="abs" style="left:560px;top:30px;width:1040px;height:240px">' + track_svg() + gates +
        '<sc-for list="{{stages}}" as="s" hint-placeholder-count="6">'
        '<button class="pick abs" style="left:{{s.x}};top:{{s.y}};width:150px;margin-left:-75px;text-align:center" aria-pressed="{{s.pressed}}" onClick="{{s.pick}}">'
        '<span style="display:block;position:relative;width:22px;height:22px;margin:0 auto 10px">'
        '<sc-if value="{{s.here}}" hint-placeholder-val="{{ false }}"><span class="abs pulse" style="inset:0;transform-origin:50% 50%;box-shadow:0 0 0 2px var(--sel);transform:rotate(45deg)"></span></sc-if>'
        '<span class="abs" style="inset:3px;transform:rotate(45deg);background:{{s.fill}};box-shadow:{{s.ring}};transition:background-color .3s,box-shadow .3s"></span></span>'
        '<span class="cap" style="display:block;font-size:14px;color:{{s.col}}">{{s.n}}</span>'
        '<span class="sm" style="display:block;font-size:14px;color:var(--to);margin-top:2px">{{s.note}}</span></button></sc-for>'
        '</div></section>')

    plat = (
        '<section class="pn sl rise d2" style="left:0;top:324px;width:520px;height:300px" aria-label="Gate 1">'
        '<div class="lab">Gate 1, to Beta Phase 2</div>'
        '<div class="h3" style="margin:12px 0 18px;line-height:1.35">Co-op players come back, week after week, to the same experience</div>'
        '<div style="display:flex;align-items:baseline;gap:12px"><span class="num" style="font-size:44px">42%</span><span class="sm soft">return the next week to play co-op</span></div>'
        '<div style="position:relative;height:10px;margin:16px 0 8px;background:color-mix(in srgb,var(--ln) 60%,transparent)">'
        '<div style="position:absolute;left:0;top:0;bottom:0;width:42%;background:var(--data);animation:grow 1.6s .6s cubic-bezier(.22,1,.36,1) both;transform-origin:0 50%"></div>'
        '<div style="position:absolute;left:60%;top:-6px;bottom:-6px;border-left:2px dashed var(--ts)"></div></div>'
        '<div style="display:flex;justify-content:space-between" class="sm soft"><span>Week 3 of Beta Phase 1</span><span>Target not set yet</span></div>'
        '<button class="btn def s" type="button" style="margin-top:22px">Set the Gate 1 target</button></section>'

        '<section class="pn sl rise d3" style="left:544px;top:324px;width:1080px;height:300px" aria-label="Platforms">'
        '<div style="display:flex;align-items:center;justify-content:space-between"><div class="lab">Platforms in this release</div>'
        '<div class="sm soft">{{picked}} of 6 picked</div></div>'
        '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;margin-top:16px">'
        '<sc-for list="{{plats}}" as="p" hint-placeholder-count="6">'
        '<button class="pick lift sls" style="position:relative;height:104px;padding:14px 16px;box-sizing:border-box;background:{{p.bg}};box-shadow:{{p.ring}}" aria-pressed="{{p.pressed}}" onClick="{{p.pick}}">'
        '<span style="display:flex;align-items:center;gap:10px"><span class="soft" style="display:inline-flex">' + icon_switch('p', ['monitor','phone','globe','cloud','server']) + '</span><span class="cap" style="font-size:14px;white-space:nowrap;color:var(--tx)">{{p.n}}</span>'
        '<span style="flex:1"></span><span class="st {{p.cls}}"><i></i>{{p.state}}</span></span>'
        '<span class="sm" style="display:block;margin-top:12px;color:var(--tx)">{{p.why}}</span>'
        '<span class="sm" style="display:block;margin-top:4px;color:var(--to)">{{p.build}}</span></button></sc-for></div></section>')

    low = (
        '<section class="pn sl rise d4" style="left:0;top:648px;width:520px;height:288px" aria-label="Next steps">'
        '<div class="lab">Next steps for the 0.9.5 alpha</div>'
        '<div style="margin-top:12px">'
        '<sc-for list="{{steps}}" as="s" hint-placeholder-count="4">'
        '<button class="pick" style="display:flex;align-items:center;gap:16px;width:100%;height:50px;border-bottom:1px solid color-mix(in srgb,var(--ln) 60%,transparent)" aria-pressed="{{s.pressed}}" onClick="{{s.pick}}">'
        '<span style="width:22px;height:22px;display:inline-flex;align-items:center;justify-content:center;box-shadow:inset 0 0 0 1.5px {{s.box}};color:var(--cta)">'
        '<sc-if value="{{s.done}}" hint-placeholder-val="{{ false }}">' + ico(I["check"], 16, 2.4) + '</sc-if></span>'
        '<span class="mono soft" style="width:44px;font-size:14px">{{s.t}}</span>'
        '<span class="sm" style="flex:1;color:{{s.col}};text-decoration:{{s.strike}}">{{s.n}}</span></button></sc-for></div></section>'

        '<section class="pn sl rise d5" style="left:544px;top:648px;width:560px;height:288px" aria-label="Activity">'
        '<div class="lab">Since you last looked</div><div style="margin-top:10px">'
        '<sc-for list="{{feed}}" as="f" hint-placeholder-count="5">'
        '<div class="rise" style="display:flex;align-items:center;gap:14px;height:44px;animation-delay:{{f.d}}">'
        '<span class="soft" style="display:inline-flex">' + icon_switch('f', ['merge','drive','check','people','bug']) + '</span>'
        '<span class="sm" style="flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:{{f.col}}">{{f.t}}</span>'
        '<span class="sm off num">{{f.ago}}</span></div></sc-for></div></section>'

        '<section class="pn sl rise d6" style="left:1128px;top:648px;width:496px;height:288px;background:color-mix(in srgb,var(--s) 55%,var(--g))" aria-label="Start a release">'
        '<div class="lab">Ready when you are</div>'
        '<div class="h2" style="margin:12px 0 10px">Start a release</div>'
        '<div class="sm soft" style="line-height:1.5">Development to Master, then the Alpha build to Friends, Family and Tadpoles on {{picked}} platforms.</div>'
        '<div style="display:flex;gap:12px;margin-top:28px"><a class="btn cta glow" href="R117Release.dc.html">' + ico(I["Release"], 20, 2) + 'Start a release</a>'
        '<a class="btn def" href="R117Pipeline.dc.html">Pipeline</a></div></section>')
    return hero + plat + low


EXTRA = "@keyframes grow{from{transform:scaleX(0)}to{transform:scaleX(1)}}"

LOGIC = r"""
const STAGES = [
  { n: 'Beta Phase 1', note: 'Now', vessels: 'Bleeding Edge 21 vessels, Development 6, Master 6. Prototype feedback in Unity.', access: 'Alpha access: Friends, Family and Tadpoles', gate: 'Gate 1: co-op players return week after week' },
  { n: 'Beta Phase 2', note: 'After Gate 1', vessels: 'Vessel counts for each branch are not set yet.', access: 'Pre-release access, then beta keys one at a time', gate: 'Gate 2: new players play again after a week' },
  { n: 'Beta Phase 3', note: 'After Gate 2', vessels: 'Vessel counts for each branch are not set yet.', access: 'Alpha for Tadpoles, beta keys one at a time', gate: 'Gate 3: surveyed players would give five stars' },
  { n: 'Early access', note: 'After Gate 3', vessels: 'Release sells early access.', access: '$15 early access', gate: 'Gate 4: feature complete, only content after' },
  { n: 'Full release', note: 'After Gate 4', vessels: 'Release goes free to play.', access: 'Free to play', gate: 'Activation, retention and referral data' },
  { n: 'Game 2', note: 'Beyond', vessels: 'Everlasting content, bug fixes and balance updates.', access: 'Everyone', gate: 'Game 2 design' } ];
const XY = """ + str([list(n) for n in NODES]) + r""";
const PLATS = [
  { n: 'Windows / Steam', icon: 'monitor', cls: 'risk', state: 'At risk', why: '2 blockers in progress', build: '0.9.4 (2211), 3 h ago' },
  { n: 'iOS', icon: 'phone', cls: 'nogo', state: 'No-go', why: 'TestFlight build fails at signing', build: '0.9.4 (2209), 1 d ago' },
  { n: 'Android', icon: 'phone', cls: 'go', state: 'Go', why: 'No open blockers', build: '0.9.4 (2211), 3 h ago' },
  { n: 'Web Toybox', icon: 'globe', cls: 'go', state: 'Go', why: 'Staged and smoke-tested', build: '0.9.4 (2211), 2 h ago' },
  { n: 'Live ops', icon: 'cloud', cls: 'risk', state: 'At risk', why: 'Production deploy is 9 days old', build: 'Remote config r41' },
  { n: 'Backend', icon: 'server', cls: 'go', state: 'Go', why: 'Kill switch tested', build: 'Cloud Code 1.7.2' } ];
const STEPS = [['T-7', 'Freeze Master for the 0.9.5 alpha'], ['T-3', 'Smoke test the four platforms'], ['T-1', 'Write the notes for Tadpoles'], ['T-0', 'Release to Friends, Family and Tadpoles']];
const FEED = [['merge', 'Merged #2156 into Bleeding Edge', '2 h'], ['drive', 'Android 0.9.4 (2211) is on Drive', '3 h'], ['check', 'Guard is green on Bleeding Edge', '3 h'],
  ['people', '4 new notes from Tadpoles', '5 h'], ['bug', 'iOS TestFlight build failed at signing', '1 d']];
class Component extends DCLogic {
  constructor(p) { super(p); this.state = Object.assign({}, UI0, { stage: 0, plats: [true, false, true, true, false, false], done: [true, false, false, false] }); }
  renderVals() {
    const st = this.state, S = STAGES[st.stage];
    const stages = STAGES.map((s, i) => ({ n: s.n, note: s.note, x: XY[i][0] + 'px', y: (XY[i][1] - 11) + 'px', here: i === 0,
      pressed: on(i === st.stage), col: i === st.stage ? 'var(--tx)' : 'var(--ts)',
      fill: i === 0 ? 'var(--sel)' : (i === st.stage ? 'var(--tx)' : 'var(--g)'),
      ring: i === st.stage ? '0 0 0 2px var(--tx)' : 'inset 0 0 0 1.5px var(--ts)',
      pick: () => this.setState({ stage: i }) }));
    const plats = PLATS.map((p, i) => Object.assign(icf(p.icon), { n: p.n, cls: p.cls, state: p.state, why: p.why, build: p.build, pressed: on(st.plats[i]),
      bg: st.plats[i] ? 'color-mix(in srgb,var(--sel) 10%,var(--g))' : 'color-mix(in srgb,var(--s) 45%,transparent)',
      ring: st.plats[i] ? 'inset 0 0 0 1px var(--sel)' : 'inset 0 0 0 1px color-mix(in srgb,var(--ln) 60%,transparent)',
      pick: () => { const a = st.plats.slice(); a[i] = !a[i]; this.setState({ plats: a }); } }));
    const steps = STEPS.map((s, i) => ({ t: s[0], n: s[1], done: st.done[i], pressed: on(st.done[i]),
      box: st.done[i] ? 'var(--cta)' : 'var(--ts)', col: st.done[i] ? 'var(--to)' : 'var(--tx)', strike: st.done[i] ? 'line-through' : 'none',
      pick: () => { const a = st.done.slice(); a[i] = !a[i]; this.setState({ done: a }); } }));
    const feed = FEED.map((f, i) => Object.assign(icf(f[0]), { t: f[1], ago: f[2], d: (0.5 + i * 0.12).toFixed(2) + 's', col: f[0] === 'bug' ? 'var(--dg)' : 'var(--tx)' }));
    return { ui: uiVals(this), stages: stages, plats: plats, steps: steps, feed: feed,
      picked: String(st.plats.filter(Boolean).length),
      sel: { n: S.n, vessels: S.vessels, access: S.access, gate: S.gate, lab: st.stage === 0 ? 'You are here' : (st.stage < 5 ? 'Ahead' : 'Beyond the launch') } };
  }
}
"""


def build():
    body = shell("Mission", "Mission", "Where the launch stands, and the one thing to do next", main())
    return board("Rev 117 Mission - the Launch Console home", body, LOGIC, EXTRA)
