from common import *

DAYS = [0, 1, 3, 7, 14, 21, 28]
SERIES = {"2211": [100, 58, 44, 39, 34, 31, 29], "2190": [100, 51, 38, 31, 27, 24, 22]}
CW, CH = 700, 220


def pts(vals):
    return " ".join("%.0f,%.0f" % (40 + d / 28 * (CW - 60), 10 + (100 - v) / 100 * (CH - 30)) for d, v in zip(DAYS, vals))


def spark(vals, w=150, h=40):
    lo, hi = min(vals), max(vals)
    p = " ".join("%.0f,%.0f" % (i / (len(vals) - 1) * w, h - 4 - (v - lo) / (hi - lo or 1) * (h - 8)) for i, v in enumerate(vals))
    return ('<svg width="%d" height="%d" viewBox="0 0 %d %d" aria-hidden="true"><polyline class="draw" points="%s" fill="none" style="stroke:var(--data)" stroke-width="1.6" pathLength="2400"/></svg>' % (w, h, w, h, p))


TILES = [("Sessions, 7 days", "1,284", [120, 140, 132, 170, 185, 176, 210, 151]),
         ("Median session", "14 min", [11, 12, 12, 13, 12, 14, 14, 14]),
         ("Crash-free sessions", "99.2%", [97.8, 98.1, 98.6, 98.4, 99.0, 99.1, 99.3, 99.2]),
         ("Played in co-op", "37%", [22, 25, 27, 30, 29, 33, 35, 37])]


def main():
    tiers = ('<section class="pn sl rise d1" style="left:0;top:0;width:1624px;height:196px" aria-label="Who is playing">'
             '<div class="lab">Who is playing</div>'
             '<div class="abs" style="left:120px;right:120px;top:104px;height:1px;background:var(--ln)"></div>'
             '<div style="display:grid;grid-template-columns:repeat(5,minmax(0,1fr));margin-top:20px">'
             '<sc-for list="{{tiers}}" as="t" hint-placeholder-count="5"><div style="position:relative;text-align:center;opacity:{{t.op}}">'
             '<span style="display:block;width:20px;height:20px;margin:22px auto 16px;transform:rotate(45deg);background:{{t.fill}};box-shadow:inset 0 0 0 1.5px {{t.ring}}"></span>'
             '<span class="cap" style="display:block;font-size:14px;color:{{t.col}}">{{t.n}}</span>'
             '<span class="sm" style="display:block;margin-top:4px;color:var(--ts)">{{t.who}}</span>'
             '<span class="sm num" style="display:block;margin-top:2px;color:var(--to)">{{t.count}}</span></div></sc-for></div></section>')
    gate = ('<section class="pn sl rise d2" style="left:0;top:220px;width:400px;height:344px" aria-label="Gate">'
            '<div style="display:flex;gap:6px"><sc-for list="{{gates}}" as="g" hint-placeholder-count="4"><button class="chip" type="button" style="height:32px;padding:0 10px;font-size:14px" aria-pressed="{{g.pressed}}" onClick="{{g.pick}}">{{g.n}}</button></sc-for></div>'
            '<div style="position:relative;width:170px;height:170px;margin:22px auto 0">'
            '<svg class="abs" width="170" height="170" viewBox="0 0 170 170" aria-hidden="true"><circle cx="85" cy="85" r="74" fill="none" style="stroke:color-mix(in srgb,var(--ln) 70%,transparent)" stroke-width="10"/>'
            '<circle cx="85" cy="85" r="74" fill="none" style="stroke:var(--data);stroke-dashoffset:{{gm.off}};transition:stroke-dashoffset 1.2s cubic-bezier(.22,1,.36,1)" stroke-width="10" pathLength="100" stroke-dasharray="100" transform="rotate(-90 85 85)"/></svg>'
            '<div class="abs" style="inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center"><span class="num" style="font-size:44px">{{gm.v}}</span><span class="lab" style="font-size:14px">{{gm.unit}}</span></div></div>'
            '<div class="sm" style="text-align:center;margin-top:16px;line-height:1.45">{{gm.what}}</div>'
            '<div class="sm" style="text-align:center;margin-top:6px;color:var(--to)">{{gm.target}}</div></section>')
    grid = "".join('<line x1="40" x2="%d" y1="%.0f" y2="%.0f" style="stroke:color-mix(in srgb,var(--ln) 45%%,transparent)"/><text x="30" y="%.0f" text-anchor="end" style="fill:var(--to);font-size:14px;font-family:Chakra Petch">%d%%</text>'
                   % (CW - 20, 10 + (100 - v) / 100 * (CH - 30), 10 + (100 - v) / 100 * (CH - 30), 14 + (100 - v) / 100 * (CH - 30), v) for v in (0, 25, 50, 75, 100))
    xl = "".join('<text x="%.0f" y="%d" text-anchor="middle" style="fill:var(--to);font-size:14px;font-family:Chakra Petch">D%d</text>' % (40 + d / 28 * (CW - 60), CH + 8, d) for d in DAYS)
    chart = ('<section class="pn sl rise d3" style="left:424px;top:220px;width:760px;height:344px" aria-label="Retention">'
             '<div style="display:flex;align-items:center;gap:10px"><span class="lab" style="flex:1">Players still playing, days after first launch</span>'
             '<sc-for list="{{series}}" as="s" hint-placeholder-count="2"><button class="chip" type="button" aria-pressed="{{s.pressed}}" onClick="{{s.pick}}"><span style="width:14px;height:2px;background:{{s.col}}"></span>{{s.n}}</button></sc-for></div>'
             '<svg style="display:block;margin-top:18px" width="%d" height="%d" viewBox="0 0 %d %d" role="img" aria-label="Retention curves for builds 2211 and 2190">' % (CW, CH + 14, CW, CH + 14)
             + grid + xl +
             '<polyline class="draw" points="' + pts(SERIES["2190"]) + '" fill="none" style="stroke:var(--ts);opacity:{{s1.op}};transition:opacity .4s" stroke-width="2" stroke-dasharray="6 5" pathLength="2400"/>'
             '<polyline class="draw" points="' + pts(SERIES["2211"]) + '" fill="none" style="stroke:var(--data);opacity:{{s0.op}};transition:opacity .4s" stroke-width="2.5" pathLength="2400"/>'
             + "".join('<circle cx="%s" cy="%s" r="3.5" style="fill:var(--data);opacity:{{s0.op}}"/>' % tuple(p.split(",")) for p in pts(SERIES["2211"]).split())
             + '</svg></section>')
    tiles = ('<section class="pn sl rise d4" style="left:1208px;top:220px;width:416px;height:344px" aria-label="Instrumented data">'
             '<div class="lab">From the builds themselves</div>'
             '<div style="display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px 20px;margin-top:16px">'
             + "".join('<div><div class="sm soft" style="font-size:14px">%s</div><div class="num" style="font-size:28px;margin:4px 0 6px">%s</div>%s</div>' % (n, v, spark(s)) for n, v, s in TILES)
             + '</div></section>')
    inbox = ('<section class="pn sl rise d5" style="left:0;top:588px;width:1624px;height:348px" aria-label="Feedback">'
             '<div style="display:flex;align-items:center;gap:10px"><span class="lab" style="flex:1">Feedback</span>'
             '<sc-for list="{{srcs}}" as="f" hint-placeholder-count="4"><button class="chip" type="button" aria-pressed="{{f.pressed}}" onClick="{{f.pick}}">{{f.n}}</button></sc-for></div>'
             '<div style="margin-top:12px;height:262px;overflow:hidden">'
             '<sc-for list="{{fb}}" as="r" hint-placeholder-count="5">'
             '<div class="rise" style="border-bottom:1px solid color-mix(in srgb,var(--ln) 50%,transparent);animation-delay:{{r.d}}">'
             '<button class="pick" style="display:flex;align-items:center;gap:16px;width:100%;height:52px" aria-expanded="{{r.open}}" onClick="{{r.pick}}">'
             '<span class="soft" style="display:inline-flex">' + icon_switch('r', ['people', 'bug', 'spark'], 18) + '</span>'
             '<span class="lab" style="width:150px;font-size:14px">{{r.src}}</span>'
             '<span class="sm" style="width:110px;color:var(--ts)">{{r.vessel}}</span>'
             '<span class="sm" style="flex:1;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:var(--tx)">{{r.t}}</span>'
             '<span class="sm off" style="width:120px;text-align:right">{{r.who}}, {{r.ago}}</span></button>'
             '<sc-if value="{{r.isOpen}}" hint-placeholder-val="{{ false }}"><div class="fade" style="display:flex;gap:24px;align-items:center;padding:0 0 16px 50px">'
             '<span class="sm" style="flex:1;line-height:1.5;color:var(--ts)">{{r.more}}</span>'
             '<button class="btn def s" type="button">Suggest a ticket</button><button class="btn def s" type="button">Reply in game</button></div></sc-if>'
             '</div></sc-for></div></section>')
    return tiers + gate + chart + tiles + inbox


LOGIC = r"""
const TIERS = [['Alpha access', 'Friends, Family, Tadpoles', '38 testers'], ['Pre-release', 'Wider friends', 'After Gate 1'], ['Beta access', 'Keys, one at a time', 'After Gate 1'],
  ['Early access', '$15 on the stores', 'After Gate 3'], ['Free to play', 'Everyone', 'After Gate 4']];
const GATES = [{ n: 'Gate 1', v: '42%', unit: 'come back', p: 42, what: 'Co-op players who played again the next week, with the same build', target: 'Target not set yet' },
  { n: 'Gate 2', v: '-', unit: 'not measured', p: 0, what: 'New players who play again after one week', target: 'Measured from Beta Phase 2' },
  { n: 'Gate 3', v: '-', unit: 'not measured', p: 0, what: 'Surveyed players who would give five stars', target: 'Measured from Beta Phase 3' },
  { n: 'Gate 4', v: '-', unit: 'not measured', p: 0, what: 'Feature complete: only content is added after', target: 'A decision, not a number' }];
const FB = [['people', 'Friends and Family', 'Sparrow', 'The missile lock finally holds when I boost. Way better.', 'Mira', '2 h', 'Said in the game\'s feedback button after a Skim Race on 0.9.4 (2211).'],
  ['bug', 'Bug testing', 'Serpent', 'Fell through the floor of the I3 course after a shed', 'Caleb', '4 h', 'Sent from Unity with the console errors; it is on the Bug Ledger with a repro.'],
  ['people', 'Tadpoles', 'Dolphin', 'Did not know what the second ability does until the third game', 'Arjun', '5 h', 'Third note this week on the same ability. The ability row may need its teaching icon.'],
  ['spark', 'Prototype feedback', 'Manta', 'The new turn feels heavy at I4, light at I1. Is that on purpose?', 'Yash', '1 d', 'From a Bleeding Edge build in Unity.'],
  ['people', 'Tadpoles', 'Squirrel', 'Played co-op with my brother three nights in a row', 'Nia', '1 d', 'Counted toward Gate 1: same build, co-op, three sessions in a week.'],
  ['bug', 'Bug testing', 'Rhino', 'Squad invite lost after rejoin', 'Garrett', '2 d', 'Fixed in #2137, in the 0.9.5 release.']];
const SRC = ['All', 'Friends and Family', 'Tadpoles', 'Bug testing', 'Prototype feedback'];
class Component extends DCLogic {
  constructor(p) { super(p); this.state = Object.assign({}, UI0, { gate: 0, s: [true, true], src: 'All', fo: 0 }); }
  renderVals() {
    const st = this.state, G = GATES[st.gate];
    const list = FB.map((f, i) => [f, i]).filter(([f]) => st.src === 'All' || f[1] === st.src);
    return { ui: uiVals(this),
      tiers: TIERS.map((t, i) => ({ n: t[0], who: t[1], count: t[2], op: i === 0 ? 1 : .55, fill: i === 0 ? 'var(--sel)' : 'var(--g)', ring: i === 0 ? 'var(--sel)' : 'var(--ts)', col: i === 0 ? 'var(--tx)' : 'var(--ts)' })),
      gates: GATES.map((g, i) => ({ n: g.n, pressed: on(st.gate === i), pick: () => this.setState({ gate: i }) })),
      gm: { v: G.v, unit: G.unit, what: G.what, target: G.target, off: String(100 - G.p) },
      series: [['Build 2211', 'var(--data)'], ['Build 2190', 'var(--ts)']].map((s, i) => ({ n: s[0], col: s[1], pressed: on(st.s[i]), pick: () => { const a = st.s.slice(); a[i] = !a[i]; this.setState({ s: a }); } })),
      s0: { op: st.s[0] ? 1 : 0 }, s1: { op: st.s[1] ? .9 : 0 },
      srcs: SRC.map((n) => ({ n: n, pressed: on(st.src === n), pick: () => this.setState({ src: n, fo: -1 }) })),
      fb: list.slice(0, st.fo >= 0 ? 4 : 5).map(([f, i], k) => Object.assign(icf(f[0]), { src: f[1], vessel: f[2], t: f[3], who: f[4], ago: f[5], more: f[6], d: (0.4 + k * 0.08).toFixed(2) + 's',
        isOpen: st.fo === i, open: on(st.fo === i), pick: () => this.setState({ fo: st.fo === i ? -1 : i }) })) };
  }
}
"""


def build():
    body = shell("Signals", "Signals", "Who is playing, what they say, and how close the next gate is", main())
    return board("Rev 117 Signals - feedback, data and the gate", body, LOGIC)
