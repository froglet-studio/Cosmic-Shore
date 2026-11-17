from common import *


def mini():
    """A small Launch Console drawn in whichever theme its card scopes (data-th on the card)."""
    return ('<span style="position:relative;display:block;height:190px;background:var(--gd);overflow:hidden;box-shadow:inset 0 0 0 1px color-mix(in srgb,var(--ln) 70%,transparent)">'
            '<svg class="abs" style="left:0;top:0" width="470" height="190" viewBox="0 0 470 190" aria-hidden="true">'
            '<path d="M0 120 C 80 100, 160 140, 240 118 S 400 96, 470 116" fill="none" style="stroke:var(--sea1);opacity:.5"/>'
            '<path d="M0 150 C 90 170, 180 130, 270 150 S 410 170, 470 146" fill="none" style="stroke:var(--sea3);opacity:.4"/></svg>'
            '<span class="abs" style="left:0;top:0;bottom:0;width:62px;background:var(--g);box-shadow:inset -1px 0 0 var(--ln)"></span>'
            '<span class="abs" style="left:14px;top:16px;width:14px;height:14px;transform:rotate(45deg);background:var(--sel)"></span>'
            + "".join('<span class="abs" style="left:14px;top:%dpx;width:%dpx;height:4px;background:%s"></span>' % (48 + k * 18, 34 if k else 30, "var(--tx)" if k == 0 else "var(--to)") for k in range(5)) +
            '<span class="abs" style="left:12px;top:46px;width:2px;height:8px;background:var(--sel)"></span>'
            '<span class="abs" style="left:80px;top:16px;width:120px;height:8px;background:var(--tx)"></span>'
            '<span class="abs" style="left:80px;top:30px;width:180px;height:4px;background:var(--ts)"></span>'
            + "".join('<span class="abs sls" style="left:%dpx;top:52px;width:118px;height:60px;background:var(--g);box-shadow:inset 0 0 0 1px %s"></span>' % (80 + k * 128, "var(--sel)" if k == 1 else "var(--ln)") for k in range(3)) +
            '<span class="abs" style="left:96px;top:66px;width:40px;height:10px;border:1px solid var(--cta)"></span>'
            '<span class="abs" style="left:224px;top:66px;width:40px;height:10px;border:1px solid var(--dg)"></span>'
            '<span class="abs" style="left:352px;top:66px;width:40px;height:10px;border:1px solid var(--cta)"></span>'
            '<span class="abs sls" style="left:80px;top:128px;width:110px;height:30px;background:var(--ctaf)"></span>'
            '<span class="abs sls" style="left:200px;top:128px;width:90px;height:30px;background:var(--cm)"></span>'
            '<span class="abs sls" style="left:300px;top:128px;width:90px;height:30px;box-shadow:inset 0 0 0 1px var(--ts)"></span>'
            '</span>')


def seg(name, lst):
    return ('<div class="lab" style="margin-bottom:10px">' + name + '</div>'
            '<div style="display:flex;gap:8px;margin-bottom:26px"><sc-for list="{{' + lst + '}}" as="o" hint-placeholder-count="3">'
            '<button class="chip" type="button" style="flex:1;justify-content:center;height:44px" aria-pressed="{{o.pressed}}" onClick="{{o.pick}}">{{o.n}}</button></sc-for></div>')


def main():
    cards = ('<section class="abs rise d1" style="left:0;top:0;width:1040px;height:620px" aria-label="Themes">'
             '<div style="display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px">'
             '<sc-for list="{{themes}}" as="t" hint-placeholder-count="4">'
             '<button class="pick lift sl" data-th="{{t.id}}" style="position:relative;height:298px;padding:16px 18px;box-sizing:border-box;background:var(--g);color:var(--tx);box-shadow:{{t.ring}}" aria-pressed="{{t.pressed}}" onClick="{{t.pick}}">'
             '<span style="display:flex;align-items:baseline;gap:12px;margin-bottom:12px"><span class="h3" style="flex:1">{{t.n}}</span><span class="sm" style="color:var(--ts)">{{t.kind}}</span>'
             '<sc-if value="{{t.on}}" hint-placeholder-val="{{ false }}"><span class="st go"><i></i>On</span></sc-if></span>'
             + mini() +
             '<span class="sm" style="display:block;margin-top:12px;color:var(--ts)">{{t.note}}</span></button></sc-for></div></section>')
    motion = ('<section class="pn sl rise d3" style="left:0;top:644px;width:1040px;height:292px" aria-label="Motion preview">'
              '<div class="lab">How it moves, at {{mo.n}}</div>'
              '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:28px;margin-top:22px">'
              '<div><div style="position:relative;height:110px;overflow:hidden;box-shadow:inset 0 0 0 1px var(--ln)">'
              '<span class="abs sls demo-rise" style="left:20px;top:22px;width:150px;height:28px;background:var(--s)"></span>'
              '<span class="abs sls demo-rise" style="left:20px;top:60px;width:110px;height:28px;background:var(--s);animation-delay:.25s"></span></div>'
              '<div class="sm" style="margin-top:12px">Panels rise in</div><div class="sm soft" style="margin-top:4px">{{mo.a}}</div></div>'
              '<div><div style="position:relative;height:110px;display:flex;align-items:center;justify-content:center;box-shadow:inset 0 0 0 1px var(--ln)">'
              '<span class="btn cta s glow">Start a release</span></div>'
              '<div class="sm" style="margin-top:12px">The call to action breathes</div><div class="sm soft" style="margin-top:4px">{{mo.b}}</div></div>'
              '<div><div style="position:relative;height:110px;overflow:hidden;box-shadow:inset 0 0 0 1px var(--ln)">'
              '<div class="seawrap" style="height:110px"><svg width="3840" height="110" viewBox="0 0 3840 110" aria-hidden="true">'
              '<path d="M0 50 C 240 20, 480 80, 960 50 S 1680 20, 1920 50 S 2640 80, 3840 50" style="stroke:var(--sea1);opacity:.7"/>'
              '<path d="M0 78 C 320 98, 640 58, 960 78 S 1600 98, 1920 78 S 2880 58, 3840 78" style="stroke:var(--sea3);opacity:.6"/></svg></div></div>'
              '<div class="sm" style="margin-top:12px">The sea drifts behind</div><div class="sm soft" style="margin-top:4px">{{mo.c}}</div></div>'
              '</div></section>')
    panel = ('<aside class="pn sl rise d2" style="left:1064px;top:0;width:560px;height:936px" aria-label="Theme settings">'
             '<div class="lab">Theme</div><div class="h2" style="margin:10px 0 26px">{{cur.n}}</div>'
             + seg("Motion", "motion") + seg("Density", "density") + seg("The sea behind", "sea") + seg("The cell behind", "img") +
             '<div class="lab" style="margin-bottom:12px">What each colour means here</div>'
             '<sc-for list="{{means}}" as="m" hint-placeholder-count="4"><div style="display:flex;align-items:center;gap:14px;height:40px">'
             '<span class="sls" style="width:44px;height:22px;background:{{m.col}}"></span><span class="sm" style="width:130px">{{m.n}}</span><span class="sm soft">{{m.what}}</span></div></sc-for>'
             '<button class="pick" style="display:flex;align-items:center;gap:14px;margin-top:22px" aria-pressed="{{wear.pressed}}" onClick="{{wear.pick}}">'
             '<span style="position:relative;width:46px;height:24px;background:{{wear.bg}};box-shadow:inset 0 0 0 1px var(--ts);transition:background-color .2s">'
             '<span class="abs" style="top:4px;left:{{wear.x}};width:16px;height:16px;background:var(--tx);transition:left .2s cubic-bezier(.33,1,.68,1)"></span></span>'
             '<span class="sm">Wear the colour set I picked in the game</span></button>'
             '<div class="abs" style="left:28px;right:28px;bottom:28px;display:flex;gap:12px;align-items:center">'
             '<button class="btn cta" type="button" onClick="{{save.go}}">Save theme</button><span class="sm" style="color:var(--cta)">{{save.msg}}</span></div></aside>')
    return cards + motion + panel


EXTRA = (".demo-rise{animation:demorise 2.6s cubic-bezier(.22,1,.36,1) infinite}"
         "@keyframes demorise{0%{opacity:0;transform:translateY(18px)}30%,85%{opacity:1;transform:none}100%{opacity:0}}"
         "[data-mo=calm] .demo-rise{animation-duration:5s}")

LOGIC = r"""
const KINDS = { deep: ['The default', 'The game\'s own Deep Sea: navy ground, the sea, the cell.'], shore: ['Light', 'Shore, for daylight, printing and sharing a screen.'],
  nocturne: ['Colour set', 'Garrett\'s live palette. Plum ground, lanterns on the rims.'], dartfrog: ['Colour set', 'Metallic bronze, from the Dart Frog set players unlock.'] };
const MO = { full: ['Full', 'Each panel lifts in once, in order', 'Slowly, while it can still be pressed', 'One slow drift, about two minutes a lap'],
  calm: ['Calm', 'Each panel lifts in once', 'Still, with its glow held', 'Very slow, seven minutes a lap'],
  off: ['Off', 'Panels are simply there', 'Still', 'Still lines'] };
class Component extends DCLogic {
  constructor(p) { super(p); this.state = Object.assign({}, UI0, { wear: false, saved: false }); }
  renderVals() {
    const st = this.state;
    const opts = (key, list) => list.map(([id, n]) => ({ n: n, pressed: on(st[key] === id), pick: () => this.setState({ [key]: id, saved: false }) }));
    const M = MO[st.mo];
    return { ui: uiVals(this),
      themes: THEMES.map(([id, n]) => ({ id: id, n: n, kind: KINDS[id][0], note: KINDS[id][1], on: st.th === id, pressed: on(st.th === id),
        ring: st.th === id ? 'inset 0 0 0 2px var(--sel),0 20px 60px rgba(0,0,0,.35)' : 'inset 0 0 0 1px var(--ln)', pick: () => this.setState({ th: id, saved: false, wear: false }) })),
      cur: { n: THEMES.filter((t) => t[0] === st.th)[0][1] },
      motion: opts('mo', [['full', 'Full'], ['calm', 'Calm'], ['off', 'Off']]),
      density: opts('dn', [['comfort', 'Comfortable'], ['compact', 'Compact']]),
      sea: opts('sea', [['on', 'On'], ['off', 'Off']]),
      img: opts('img', [['on', 'On'], ['off', 'Off']]),
      mo: { n: M[0], a: M[1], b: M[2], c: M[3] },
      means: [['var(--ctaf)', 'Call to action', 'The one thing to do now'], ['var(--sel)', 'Selection', 'What you picked, and focus'],
        ['var(--cm)', 'No way back', 'Promote, Release'], ['var(--dg)', 'Danger', 'No-go, a failed build, roll back']].map((m) => ({ col: m[0], n: m[1], what: m[2] })),
      wear: { pressed: on(st.wear), bg: st.wear ? 'var(--sel)' : 'transparent', x: st.wear ? '26px' : '4px', pick: () => this.setState({ wear: !st.wear, th: !st.wear ? 'nocturne' : st.th }) },
      save: { msg: st.saved ? 'Saved for you on every page' : '', go: () => this.setState({ saved: true }) } };
  }
}
"""


def build():
    body = shell("Theme", "Theme", "How the console looks and moves, for you", main())
    return board("Rev 117 Theme - the console's themes, motion and density", body, LOGIC, EXTRA)
