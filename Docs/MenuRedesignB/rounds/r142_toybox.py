#!/usr/bin/env python3
"""Rev 142 - The Toy Box redrawn with motion, four ways (2026-11-24).

Usage: python3 r142_toybox.py <root> <rev>
Writes R<rev>Compare, A-D and Notes into <root>/project. The kit (helmet, the menu backdrop, the sea lines, the hub
band, page()) is Rev 101's, lifted as Rev 128 does. Every image is a /_blob/ id already in the canvas: the seven
toys' codex portraits (Item 8's Toy Box board), the hull card renders (Rev 128), the hull, fauna, paint and element
icons (Item 8's Toy configure board) and the in-game cell frame the menu backdrop uses.

Each option board is the whole flow, playable: the Toy Box window -> open a toy -> pick a variation (the switch
animation) -> its verb (the spawn animation in the preview) -> NAVIGATE (the dive into the preview) -> the world,
where your vessel arrives. Animations are CSS keyframes that play when a block mounts (sc-if), so nothing runs at
rest but a toy's slow bob. Parity blocks (`pvA` / `pvB`) remount the preview on every switch so its animation
replays.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, REV = sys.argv[1], sys.argv[2]
_src = open(os.path.join(HERE, 'r101_quests.py')).read()
_src = _src[:_src.index('# ---------- A: the star chart')]
sys.argv = [sys.argv[0], root]
exec(_src)  # STYLE, BACKDROP, HUB, page, SOFT_LINE, out


def blob(i):
    return '/_blob/' + i


WORLD = blob('391619dde523174b4a9ead64e169b67a')  # the in-game cell frame under the menu backdrop
SQUIRREL = blob('0b59d109e643a6db63d1197fa68bec82')

TOYS = [
    {'key': 'vessel', 'name': 'Vessel Changer', 'group': 'Pilot', 'verb': 'Switch', 'art': blob('e3021fea3ed132a9c31d1d339fc70cc9'),
     'desc': 'Swap your ship.', 'hint': 'Fly through the ring to swap your ship.',
     'rows': [{'n': 'Squirrel', 'img': SQUIRREL, 'hull': True, 'd': 'flying'},
              {'n': 'Sparrow', 'img': blob('5d6e6caf494b25329c5384d72185255e'), 'hull': True},
              {'n': 'Rhino', 'img': blob('45aa01c84539d8c2a79fe4b9adee898b'), 'hull': True},
              {'n': 'Manta', 'img': blob('8c982fb3b1311931aa946a7f5337d47b')},
              {'n': 'Dolphin', 'img': blob('0fa7978ec882f85c851f541bb20b17bc')},
              {'n': 'Serpent', 'img': blob('d60af9f6ab71865788f13a01fc914c77')}]},
    {'key': 'spawn', 'name': 'Spawn Matrix', 'group': 'Creation', 'verb': 'Spawn', 'art': blob('fcc11a46f5aa7d95ca55a153db5aef22'),
     'desc': 'The release bench. Fauna, flora and vessels, spawned live into this cell.', 'hint': 'Fly through to open the release bench.',
     'rows': [{'n': 'Shark', 'img': blob('90c8aebc77d4efba0d2bd00eae7e0046')},
              {'n': 'Tadpole', 'img': blob('bc37dda08a80192fb15efee2999cf53f')},
              {'n': 'Brittlestar', 'img': blob('d3c8549684f8f24d88ec6e7fbdd33b21')},
              {'n': 'QuadFish', 'img': blob('cfc46e3475b3a9596cfef8f1dfaac33a')},
              {'n': 'Clawfish', 'img': blob('06fbcfe5439d34f47ebdd1593be1cd53')},
              {'n': 'Worm Colony', 'img': blob('2a3fef619c37450f9ea4470cdf38d31e')}]},
    {'key': 'paint', 'name': 'Connect the Dots', 'group': 'Creation', 'verb': 'Start', 'art': blob('860998acebc7a0176136953b669b2185'),
     'desc': 'Connect the dots to paint 3D masterpieces with your trail.', 'hint': 'Fly through to start painting.',
     'rows': [{'n': 'Lotus', 'img': blob('9dc72e6d2c1798a01c201ca59e9bc40d'), 'd': '64% painted'},
              {'n': 'Nautilus', 'img': blob('434a1e1057192fbb2d0da0853ff58e83')},
              {'n': 'Peacock', 'img': blob('a57eab7cb50f0a628dee6084bc9269af')},
              {'n': 'Saturn', 'img': blob('4e33286940f8ea7fbfb0946141b1682b')},
              {'n': 'Star', 'img': blob('6a519ef1730d403b932b7d7666cf201f')},
              {'n': 'Starry Night', 'img': blob('5df35d699fd8c04565101801438f732a'), 'd': 'masterpiece'}]},
    {'key': 'cell', 'name': 'Cell Selector', 'group': 'World', 'verb': 'Switch', 'art': blob('abe556c558151a94c49027df61f45aa0'),
     'desc': 'The freestyle world picker, and the freestyle reset.', 'hint': 'Fly through to pick a world.',
     'rows': [{'n': 'Lattice', 'd': 'you are here'}, {'n': 'Yggdra'}, {'n': 'Daedala'}, {'n': 'Orrery'}, {'n': 'Zephyr'}, {'n': 'Caldera'}]},
    {'key': 'domain', 'name': 'Domain Changer', 'group': 'Pilot', 'verb': 'Switch', 'art': blob('7c6cae013251a3d627c8c8f043d2f4bb'),
     'desc': 'Change your team colour.', 'hint': 'Fly through to change your team colour.',
     'rows': [{'n': 'Jade', 'sw': '#00D4FF', 'd': 'current'}, {'n': 'Ruby', 'sw': '#A600FF'}, {'n': 'Gold', 'sw': '#FFAE00'}]},
    {'key': 'charge', 'name': 'Element Charger', 'group': 'Pilot', 'verb': 'Charge', 'art': blob('7850440a6efe08dd09a6f63c7810dd29'),
     'desc': "Charge your vessel's elements.", 'hint': "Fly through to charge your vessel's elements.",
     'rows': [{'n': 'Charge', 'img': blob('146f0db34a0fb1fd240e6ec4b6ee3be3')}, {'n': 'Mass', 'img': blob('6cbe6dea086cb1497b611aa43d345e92')},
              {'n': 'Space', 'img': blob('d1fa841ca6b542c08c18c935d9a28670')}, {'n': 'Time', 'img': blob('c39bb9b2681dac53359d12c921a7bfc3')}]},
    {'key': 'wander', 'name': 'Wander', 'group': 'World', 'verb': 'Leave', 'art': blob('46b6ec3d6fd2a32ef887fc0f5522fd49'),
     'desc': 'Leave the cell and go wandering, with an Ark or without one.', 'hint': 'Fly through to leave the cell.',
     'rows': [{'n': 'With the Ark'}, {'n': 'Alone'}]},
]

# ------------------------------------------------------------------ shared kit
CARD_BG = ('background:radial-gradient(ellipse 80% 60% at 50% 30%,rgba(117,143,251,.16),rgba(117,143,251,0) 70%),'
           'repeating-linear-gradient(172deg,rgba(116,123,173,.07) 0 1px,rgba(0,0,0,0) 1px 16px),'
           'linear-gradient(180deg,#0B1240 0%,#00041F 100%)')

BASE_CSS = '''
.tb-card{position:absolute;box-sizing:border-box;overflow:hidden;cursor:pointer;color:#E6E9FF;font-family:'Aldrich',sans-serif;border:0;padding:0;text-align:left;%(bg)s;box-shadow:inset 0 0 0 1px #222645}
.tb-card:focus-visible{outline:2px solid #00D4FF;outline-offset:3px}
.tb-bob{animation:tbBob 8s ease-in-out infinite}
@keyframes tbBob{0%%,100%%{transform:translateY(0)}50%%{transform:translateY(-6px)}}
@keyframes tbRise{from{opacity:0;transform:translateY(26px)}to{opacity:1;transform:none}}
@keyframes tbFade{from{opacity:0}to{opacity:1}}
@keyframes tbOut{from{opacity:1}to{opacity:0}}
.tb-rise{animation:tbRise .55s cubic-bezier(.2,.8,.2,1) both}
.tb-fade{animation:tbFade .5s ease both}
.tb-tile{box-sizing:border-box;position:relative;display:flex;align-items:center;gap:14px;border:0;cursor:pointer;color:#E6E9FF;font-family:'Aldrich',sans-serif;text-align:left;background:#00041F;box-shadow:inset 0 0 0 1px #222645;transition:box-shadow .2s,background .2s,transform .2s}
.tb-tile:hover{background:#0B1240}
.tb-tile.on{background:#222645;box-shadow:inset 0 0 0 2px #E6E9FF}
.tb-tile:focus-visible{outline:2px solid #00D4FF;outline-offset:2px}
.tb-sw{width:28px;height:28px;flex:none}
.tb-world{position:absolute;inset:0;width:100%%;height:100%%;object-fit:cover}
.tb-hud{font-family:'Chakra Petch',sans-serif;font-weight:600;letter-spacing:.14em;text-transform:uppercase}
.tb-toast{position:absolute;left:50%%;bottom:120px;transform:translateX(-50%%);padding:14px 28px;font-size:17px;z-index:30;animation:tbRise .4s ease both}
.tb-ctl{position:absolute;z-index:40;display:flex;gap:10px;align-items:center}
''' % {'bg': CARD_BG}

STRIP = ('<div class="abs" style="left:0;right:0;top:96px;height:40px;display:flex;align-items:center;justify-content:center;gap:34px;z-index:5">'
         '<span class="kc">LT</span>'
         + ''.join('<span class="cap" style="font-size:16px;position:relative;color:%s">%s%s</span>'
                   % ('#E6E9FF' if n == 'Toy Box' else '#747BAD', n.upper(),
                      '<span class="abs" style="left:0;right:0;bottom:-8px;height:2px;background:#E6E9FF"></span>' if n == 'Toy Box' else '')
                   for n in ['Toy Box', 'Arcade', 'Arena', 'Mission'])
         + '<span class="kc">RT</span></div>')

WINDOW = ('<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,4,31,.95)"></div>'
          + STRIP)

HEAD = ('<div class="abs" style="left:64px;right:64px;top:160px;height:48px;display:flex;align-items:center;justify-content:space-between;z-index:4">'
        '<span style="display:flex;align-items:baseline;gap:22px"><span style="font-size:28px">Toy Box</span><span class="lab">7 toys</span></span>'
        '<span style="display:flex;align-items:center;gap:18px"><span class="lab">Today</span>'
        '<span style="font-size:16px;color:#B8C0F0">Spawn a Shark in the Spawn Matrix</span>'
        '<span class="lab" style="color:#E6E9FF">Ends in 7h 12m</span>'
        '<span class="btn sm soft" style="height:36px;font-size:13px">Try it +10</span></span></div>')

CFG_HEAD = ('<div class="abs tb-rise" style="left:64px;right:64px;top:160px;height:48px;display:flex;align-items:center;justify-content:space-between;z-index:4">'
            '<span style="display:flex;align-items:baseline;gap:22px"><span class="lab">Toy Box &#160;/&#160; {{cur.group}}</span>'
            '<span style="font-size:28px">{{cur.name}}</span></span>'
            '<button class="btn sm soft" type="button" onClick="{{back}}" style="width:180px"><span class="kc">B</span>Back</button></div>')

TOAST = ('<sc-if value="{{toast}}" hint-placeholder-val="{{ false }}"><div class="tb-toast glass" style="box-shadow:inset 0 0 0 1px #434C89">{{toastText}}</div></sc-if>')

# What the preview shows for the picked variation: a hull render wide, an icon square, a domain swatch, or the toy.
PV_INNER = ('<sc-if value="{{pv.isHull}}" hint-placeholder-val="{{ true }}"><img src="{{pv.img}}" alt="" class="hull" style="width:%(hw)dpx;height:%(hh)dpx"></sc-if>'
            '<sc-if value="{{pv.isIcon}}" hint-placeholder-val="{{ false }}"><img src="{{pv.img}}" alt="" style="width:%(iw)dpx;height:%(iw)dpx;object-fit:contain;background:radial-gradient(circle,rgba(0,4,31,.85) 30%%,rgba(0,4,31,0) 68%%);transform:scale(1.35)"></sc-if>'
            '<sc-if value="{{pv.isSwatch}}" hint-placeholder-val="{{ false }}"><span style="position:relative;display:block;width:%(iw)dpx;height:%(iw)dpx">'
            '<img src="{{cur.art}}" alt="" style="position:absolute;inset:0;width:100%%;height:100%%;object-fit:contain">'
            '<span style="position:absolute;left:50%%;top:50%%;width:34%%;height:34%%;transform:translate(-50%%,-50%%) rotate(45deg);background:{{pv.sw}}"></span></span></sc-if>'
            '<sc-if value="{{pv.isToy}}" hint-placeholder-val="{{ false }}"><img src="{{cur.art}}" alt="" style="width:%(iw)dpx;height:%(iw)dpx;object-fit:contain"></sc-if>')


def pv_inner(hw=560, hh=187, iw=300):
    return PV_INNER % {'hw': hw, 'hh': hh, 'iw': iw}


def tiles(cols, tile_h=72, gap=12, extra_cls='', icon=44):
    """The variation tiles, as Toy configure draws them (icon or swatch, name, second line)."""
    return ('<div style="display:grid;grid-template-columns:repeat(%d,1fr);gap:%dpx">' % (cols, gap)
            + '<sc-for list="{{rows}}" as="r" hint-placeholder-count="6">'
            '<button class="tb-tile {{r.cls}} %s" type="button" onClick="{{r.pick}}" style="height:%dpx;padding:0 16px;animation:tbRise .45s {{r.dl}}ms cubic-bezier(.2,.8,.2,1) both">'
            '<sc-if value="{{r.hasImg}}" hint-placeholder-val="{{ true }}"><img src="{{r.img}}" alt="" style="width:%dpx;height:%dpx;object-fit:contain;flex:none"></sc-if>'
            '<sc-if value="{{r.hasSw}}" hint-placeholder-val="{{ false }}"><span class="tb-sw dia" style="background:{{r.sw}};width:20px;height:20px;margin:0 4px"></span></sc-if>'
            '<span style="display:flex;flex-direction:column;gap:4px;min-width:0"><span class="ell" style="font-size:17px">{{r.n}}</span>'
            '<span class="lab ell" style="font-size:12px;color:{{r.dc}}">{{r.d}}</span></span></button></sc-for></div>'
            % (extra_cls, tile_h, icon, icon))


def actions(width=None):
    w = 'width:%dpx;' % width if width else ''
    return ('<div style="display:flex;gap:16px;%s">' % w
            + '<button class="btn cta" type="button" onClick="{{commit}}" style="flex:1"><span class="kc">A</span>{{cur.verb}}</button>'
            '<button class="btn def" type="button" onClick="{{navigate}}" style="flex:1"><span class="kc">Y</span>Navigate</button></div>')


REPLAY = ('<div class="tb-ctl" style="left:24px;bottom:20px"><button class="btn sm soft" type="button" onClick="{{replay}}" style="height:36px;font-size:12px">Board: start again</button>'
          '<span style="font-size:13px;color:#747BAD">{{stageNote}}</span></div>')


def world_hud(extra=''):
    return ('<div class="abs tb-hud" style="left:64px;top:56px;font-size:15px;color:#E6E9FF;animation:tbRise .6s 1.6s both;z-index:20">{{cur.name}} &#160;&#183;&#160; 120 m</div>'
            '<div class="abs" style="left:0;right:0;bottom:96px;text-align:center;font-size:20px;color:#E6E9FF;text-shadow:0 2px 10px #00010A;animation:tbRise .6s 1.9s both;z-index:20">{{cur.hint}}</div>'
            '<div class="abs" style="right:64px;top:48px;z-index:20;animation:tbRise .6s 1.6s both"><button class="btn sm soft" type="button" onClick="{{back}}"><span class="kc">B</span>Toy Box</button></div>'
            + extra)


LOGIC = r'''
const TOYS = __TOYS__;
const OPT = __OPT__;
class Component extends DCLogic {
  constructor(props) {
    super(props);
    this.state = { stage: 'box', toy: 0, sel: 0, k: 0, sk: 0, spawned: false, front: 0, toast: '' };
    this.timers = [];
  }
  componentWillUnmount() { this.timers.forEach((t) => clearTimeout(t)); }
  later(ms, f) { this.timers.push(setTimeout(f, ms)); }
  say(text) { this.setState({ toast: text }); this.later(2400, () => this.setState({ toast: '' })); }
  open(i) { this.setState({ stage: 'cfg', toy: i, sel: 0, k: this.state.k + 1, spawned: false, front: i }); }
  turn(d) { this.setState({ front: this.state.front + d }); }
  face(i) {
    const n = TOYS.length, f = this.state.front, cur = ((f % n) + n) % n;
    let d = i - cur; if (d > n / 2) d -= n; if (d < -n / 2) d += n;
    if (d === 0) this.open(i); else this.setState({ front: f + d });
  }
  renderVals() {
    const st = this.state, n = TOYS.length, t = TOYS[st.toy], row = t.rows[st.sel] || {};
    const fi = ((st.front % n) + n) % n;
    const toys = TOYS.map((x, i) => {
      const o = (OPT.cards || [])[i] || {};
      return { name: x.name, group: x.group, art: x.art, desc: x.desc, verb: x.verb, count: x.rows.length + ' ways',
               i: i, d: 80 + i * 70, x: o.x || 0, y: o.y || 0, rot: Math.round(i * 360 / n * 100) / 100,
               cls: i === fi ? 'front' : '', open: () => (OPT.orbit ? this.face(i) : this.open(i)) };
    });
    const rows = t.rows.map((r, i) => ({
      n: r.n, d: r.d || '', dc: r.d ? '#99FF80' : '#747BAD', img: r.img || '', hasImg: !!r.img, sw: r.sw || '', hasSw: !!r.sw,
      cls: i === st.sel ? 'on' : '', dl: 60 + i * 50, dx: OPT.dialPos ? OPT.dialPos[i][0] : 0, dy: OPT.dialPos ? OPT.dialPos[i][1] : 0,
      pick: () => { if (i !== st.sel) this.setState({ sel: i, k: st.k + 1, spawned: false }); }
    }));
    const pv = { img: row.img || t.art, sw: row.sw || '', name: row.n || t.name,
                 isHull: !!row.hull, isIcon: !!row.img && !row.hull, isSwatch: !!row.sw, isToy: !row.img && !row.sw };
    const from = OPT.from[st.toy] || [0, 0, 1];
    const ride = t.key === 'vessel' && st.spawned && row.img ? row.img : OPT.squirrel;
    const notes = { box: 'Point at a toy, then open it.', cfg: 'Pick a variation, press ' + t.verb + ', or Navigate to fly there.',
                    dive: 'Navigate: into the preview...', world: 'You are at the station.' };
    return {
      isBox: st.stage === 'box', isCfg: st.stage === 'cfg', isDive: st.stage === 'dive', isWorld: st.stage === 'world',
      toys, rows, pv, pvA: st.k % 2 === 0, pvB: st.k % 2 === 1, spA: st.spawned && st.sk % 2 === 0, spB: st.spawned && st.sk % 2 === 1,
      cur: { name: t.name, group: t.group, art: t.art, desc: t.desc, verb: t.verb, hint: t.hint, pick: row.n || '' },
      fx: from[0], fy: from[1], fs: from[2], ring: -st.front * 360 / n,
      frontName: TOYS[fi].name, frontDesc: TOYS[fi].desc, frontVerb: TOYS[fi].verb, frontCount: TOYS[fi].rows.length + ' ways',
      openFront: () => this.open(fi), prev: () => this.turn(-1), next: () => this.turn(1),
      ride, rideHull: ride === OPT.squirrel || /(5d6e6caf|45aa01c8)/.test(ride),
      commit: () => { this.setState({ spawned: true, sk: st.sk + 1 }); this.say(t.verb + ': ' + (row.n || t.name) + '.'); },
      navigate: () => { this.setState({ stage: 'dive' }); this.later(OPT.diveMs, () => this.setState({ stage: 'world' })); },
      back: () => this.setState({ stage: st.stage === 'world' ? 'box' : 'box', spawned: false }),
      replay: () => this.setState({ stage: 'box', spawned: false, toast: '' }),
      stageNote: notes[st.stage] || '', toast: !!st.toast, toastText: st.toast
    };
  }
}
'''


def logic(opt):
    opt = dict(opt)
    opt.setdefault('squirrel', SQUIRREL)
    opt.setdefault('diveMs', 1700)
    return LOGIC.replace('__TOYS__', json.dumps(TOYS)).replace('__OPT__', json.dumps(opt))


def board(title, css, box, cfg, dive, world, opt):
    body = ('<style>' + css + '</style>' if False else '') + (
        BACKDROP + HUB +
        '<sc-if value="{{isBox}}" hint-placeholder-val="{{ true }}">' + WINDOW + HEAD + box + '</sc-if>'
        '<sc-if value="{{isCfg}}" hint-placeholder-val="{{ false }}">' + WINDOW + CFG_HEAD + cfg + '</sc-if>'
        '<sc-if value="{{isDive}}" hint-placeholder-val="{{ false }}">' + dive + '</sc-if>'
        '<sc-if value="{{isWorld}}" hint-placeholder-val="{{ false }}"><div class="abs" style="inset:0;z-index:12;overflow:hidden;background:#00010A">' + world + '</div></sc-if>'
        + TOAST + REPLAY)
    return page_css(title, body, logic(opt), css)


def page_css(title, body, lg, css, h=1080):
    html = page(title, body, lg, 1920, h)
    return html.replace('</style>\n</helmet>', BASE_CSS + css + '</style>\n</helmet>', 1)


# ================================================================== A  Portal
# Tall cards dealt in a row; the toy floats in the top two thirds; the name and its line at the foot; the verb slides
# up on hover. Open: the card grows into the preview. Navigate: the preview takes the screen and you fly through the
# toy's ring into the world; your vessel streaks in from the vanishing point.
A_W, A_H, A_GAP, A_TOP = 230, 420, 24, 300
A_LEFT = (1920 - (7 * A_W + 6 * A_GAP)) // 2
A_PV = (64, 236, 1120, 680)  # the preview rect in Configure


def a_from():
    px, py, pw, ph = A_PV
    out = []
    for i in range(7):
        cx, cy = A_LEFT + i * (A_W + A_GAP) + A_W / 2, A_TOP + A_H / 2
        out.append([round(cx - (px + pw / 2)), round(cy - (py + ph / 2)), round(A_W / pw, 3)])
    return out


A_CSS = '''
.a-card{width:230px;height:420px;transform-origin:50% 100%;animation:aDeal .7s calc(var(--d)*1ms) cubic-bezier(.2,.8,.2,1) both;transition:transform .35s cubic-bezier(.2,.8,.2,1),box-shadow .35s}
@keyframes aDeal{from{opacity:0;transform:translateY(80px) rotateX(35deg) scale(.9)}to{opacity:1;transform:none}}
.a-wrap{position:absolute;perspective:1100px}
.a-card:hover,.a-card:focus-visible{transform:translateY(-14px) rotateX(6deg);box-shadow:inset 0 0 0 1px #E6E9FF,0 30px 60px rgba(0,1,10,.7)}
.a-obj{position:absolute;left:15px;top:44px;width:200px;height:200px;object-fit:contain;transition:transform .5s cubic-bezier(.2,.8,.2,1)}
.a-card:hover .a-obj{transform:scale(1.1) rotate(-6deg)}
.a-sweep{position:absolute;top:0;bottom:0;width:60px;left:-80px;background:linear-gradient(90deg,rgba(230,233,255,0),rgba(230,233,255,.14),rgba(230,233,255,0));transform:skewX(-14deg)}
.a-card:hover .a-sweep{animation:aSweep .9s ease}
@keyframes aSweep{from{left:-80px}to{left:280px}}
.a-verb{position:absolute;left:20px;right:20px;bottom:20px;height:40px;opacity:0;transform:translateY(16px);transition:all .3s cubic-bezier(.2,.8,.2,1)}
.a-card:hover .a-verb,.a-card:focus-visible .a-verb{opacity:1;transform:none}
.a-words{position:absolute;left:20px;right:20px;bottom:20px;transition:transform .3s cubic-bezier(.2,.8,.2,1)}
.a-card:hover .a-words,.a-card:focus-visible .a-words{transform:translateY(-54px)}
.a-pv{position:absolute;overflow:hidden;animation:aOpen .75s cubic-bezier(.2,.8,.2,1) both;box-shadow:inset 0 0 0 1px #434C89}
@keyframes aOpen{from{transform:translate(calc(var(--fx)*1px),calc(var(--fy)*1px)) scale(var(--fs),.62);opacity:.4}to{transform:none;opacity:1}}
.a-in{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;animation:aStrip .6s cubic-bezier(.2,.8,.2,1) both}
@keyframes aStrip{from{transform:translateX(160px);opacity:0;filter:blur(8px)}to{transform:none;opacity:1;filter:none}}
.a-flash{position:absolute;inset:0;pointer-events:none;background:linear-gradient(100deg,rgba(230,233,255,0) 30%,rgba(230,233,255,.22) 50%,rgba(230,233,255,0) 70%);animation:aFlash .7s ease both}
@keyframes aFlash{from{transform:translateX(-100%)}to{transform:translateX(100%)}}
.a-ring{position:absolute;left:50%;top:50%;width:200px;height:200px;margin:-100px 0 0 -100px;border-radius:50%;box-shadow:0 0 0 3px #E6E9FF;animation:aRing .9s cubic-bezier(.2,.8,.2,1) both;pointer-events:none}
.a-ring.r2{animation-delay:.12s}
@keyframes aRing{from{transform:scale(.1);opacity:1}to{transform:scale(3.6);opacity:0}}
.a-pop{animation:aPop .7s .1s cubic-bezier(.3,1.6,.5,1) both}
@keyframes aPop{from{transform:scale(0) rotate(-20deg)}to{transform:none}}
.a-side{animation:aSide .7s .25s cubic-bezier(.2,.8,.2,1) both}
@keyframes aSide{from{opacity:0;transform:translateX(70px)}to{opacity:1;transform:none}}
.a-dive{position:absolute;inset:0;z-index:12;overflow:hidden;background:#00010A;animation:aGrow .9s cubic-bezier(.6,0,.2,1) both}
@keyframes aGrow{from{clip-path:inset(236px 736px 164px 64px)}to{clip-path:inset(0 0 0 0)}}
.a-dive .tb-world{animation:aPush 1.7s cubic-bezier(.5,0,.2,1) both}
@keyframes aPush{from{transform:scale(1)}to{transform:scale(1.9);filter:blur(3px)}}
.a-portal{position:absolute;left:50%;top:50%;width:420px;height:420px;margin:-210px 0 0 -210px;object-fit:contain;animation:aThrough 1.7s cubic-bezier(.6,0,.4,1) both}
@keyframes aThrough{0%{transform:scale(.7);opacity:1}70%{opacity:1}100%{transform:scale(9);opacity:0}}
.a-wv{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;animation:aLand 1.2s cubic-bezier(.2,.8,.2,1) both}
@keyframes aLand{from{transform:scale(1.25)}to{transform:none}}
.a-ship{position:absolute;left:50%;top:58%;width:960px;height:320px;margin:-160px 0 0 -480px;animation:aStreak 1.3s .25s cubic-bezier(.15,.9,.25,1) both}
@keyframes aStreak{0%{transform:translateY(-140px) scale(.05);opacity:0}15%{opacity:1}100%{transform:none;opacity:1}}
.a-trail{position:absolute;left:50%;top:0;width:6px;height:58%;margin-left:-3px;background:linear-gradient(180deg,rgba(230,233,255,0),rgba(230,233,255,.8));transform-origin:50% 100%;animation:aTrail 1.1s .25s ease-out both}
@keyframes aTrail{0%{transform:scaleY(0);opacity:1}60%{transform:scaleY(1);opacity:.9}100%{transform:scaleY(1);opacity:0}}
.a-shock{position:absolute;left:50%;top:58%;width:300px;height:300px;margin:-150px 0 0 -150px;border-radius:50%;box-shadow:0 0 0 2px #E6E9FF;animation:aRing 1s 1.35s cubic-bezier(.2,.8,.2,1) both}
'''


def a_box():
    cards = ('<sc-for list="{{toys}}" as="t" hint-placeholder-count="7">'
             '<div class="a-wrap" style="left:{{t.x}}px;top:%dpx;width:%dpx;height:%dpx">'
             '<button class="tb-card a-card" type="button" onClick="{{t.open}}" style="--d:{{t.d}}">'
             '<span class="lab abs" style="left:20px;top:18px">{{t.group}}</span>'
             '<span class="lab abs" style="right:20px;top:18px;color:#434C89">{{t.count}}</span>'
             '<img class="a-obj tb-bob" src="{{t.art}}" alt="">'
             '<span class="a-sweep"></span>'
             '<span class="a-words"><span style="display:block;font-size:24px;line-height:1.15">{{t.name}}</span>'
             '<span style="display:block;margin-top:8px;font-size:14px;line-height:1.45;color:#B8C0F0;height:40px;overflow:hidden">{{t.desc}}</span></span>'
             '<span class="a-verb btn sm cta" style="height:40px"><span class="kc">A</span>Open</span>'
             '</button></div></sc-for>') % (A_TOP, A_W, A_H)
    foot = ('<div class="abs tb-fade" style="left:64px;right:64px;top:800px;display:flex;justify-content:space-between;align-items:center">'
            '<span style="font-size:15px;color:#747BAD">Point at a toy: it rises and its verb slides up. Open it: the card grows into its live preview.</span>'
            '<span style="display:flex;gap:10px;align-items:center"><span class="kc">A</span><span class="lab">Open</span></span></div>')
    return cards + foot


def a_cfg():
    px, py, pw, ph = A_PV
    pvb = ('<div class="a-in">%s</div><div class="a-flash"></div>' % pv_inner(780, 260, 380))
    pv = ('<div class="a-pv" style="left:%dpx;top:%dpx;width:%dpx;height:%dpx;--fx:{{fx}};--fy:{{fy}};--fs:{{fs}}">'
          '<img class="tb-world" src="%s" alt="" style="opacity:.42">'
          '<div class="abs" style="inset:0;background:radial-gradient(ellipse 60%% 60%% at 50%% 50%%,rgba(0,4,31,0),rgba(0,4,31,.75))"></div>'
          '<sc-if value="{{pvA}}" hint-placeholder-val="{{ true }}">%s</sc-if>'
          '<sc-if value="{{pvB}}" hint-placeholder-val="{{ false }}">%s</sc-if>'
          '<sc-if value="{{spA}}" hint-placeholder-val="{{ false }}"><span class="a-ring"></span><span class="a-ring r2"></span></sc-if>'
          '<sc-if value="{{spB}}" hint-placeholder-val="{{ false }}"><span class="a-ring"></span><span class="a-ring r2"></span></sc-if>'
          '<span class="lab abs" style="left:24px;top:20px;color:#E6E9FF">Live preview</span>'
          '<span class="abs" style="left:24px;bottom:20px;font-size:22px">{{pv.name}}</span></div>') % (px, py, pw, ph, WORLD, pvb, pvb)
    side = ('<div class="abs a-side" style="left:1228px;top:236px;width:628px;height:680px;display:flex;flex-direction:column;gap:22px">'
            '<span style="font-size:16px;line-height:1.55;color:#B8C0F0">{{cur.desc}}</span>'
            '<span class="lab">Variations</span>' + tiles(2) +
            '<div style="margin-top:auto">' + actions() + '</div></div>')
    return pv + side


def a_dive():
    return ('<div class="a-dive"><img class="tb-world" src="%s" alt=""><img class="a-portal" src="{{cur.art}}" alt=""></div>' % WORLD)


def a_world():
    return ('<img class="a-wv" src="%s" alt=""><div class="abs" style="inset:0;background:radial-gradient(ellipse 55%% 50%% at 50%% 56%%,rgba(0,1,10,.0),rgba(0,1,10,.55))"></div><span class="a-trail"></span>'
            '<img class="a-ship hull" src="{{ride}}" alt=""><span class="a-shock"></span>' % WORLD) + world_hud()


# ================================================================== B  Orbit
# The seven toys stand on a ring around you; LT / RT turns it; the card facing you is the only one with words. Its
# name runs up the card's spine. Open: the card flips round and its back is Configure. Switch: the preview turns like
# a cube. The verb: prisms fly in and build the thing. Navigate: warp streaks, and your vessel is built from prisms.
B_CSS = '''
.b-stage{position:absolute;left:0;right:0;top:250px;height:470px;perspective:2000px;perspective-origin:50% 30%}
.b-ring{position:absolute;left:50%;top:40px;width:0;height:0;transform-style:preserve-3d;transition:transform .8s cubic-bezier(.3,.9,.3,1)}
.b-card{width:380px;height:300px;left:-190px;top:0;transform-style:preserve-3d;backface-visibility:hidden;opacity:.55;transition:opacity .6s,box-shadow .3s}
.b-card.front{opacity:1;box-shadow:inset 0 0 0 1px #E6E9FF}
.b-card:hover{opacity:.9}
.b-spine{position:absolute;left:0;top:0;bottom:0;width:64px;display:flex;align-items:center;justify-content:center;box-shadow:inset -1px 0 0 #222645}
.b-spine span{writing-mode:vertical-rl;transform:rotate(180deg);font-size:24px;white-space:nowrap}
.b-obj{position:absolute;left:96px;top:30px;width:240px;height:240px;object-fit:contain}
.b-card.front .b-obj{animation:bSpin 14s linear infinite}
@keyframes bSpin{from{transform:rotateY(0)}to{transform:rotateY(360deg)}}
.b-info{animation:tbRise .5s .15s cubic-bezier(.2,.8,.2,1) both}
.b-flip{position:absolute;left:64px;right:64px;top:236px;height:760px;transform-style:preserve-3d;animation:bFlip .8s cubic-bezier(.3,.9,.3,1) both;transform-origin:50% 40%}
@keyframes bFlip{from{transform:perspective(2000px) rotateY(-100deg) scale(.4);opacity:0}45%{opacity:1}to{transform:none;opacity:1}}
.b-pv{position:absolute;left:280px;top:0;width:1232px;height:520px;overflow:hidden;box-shadow:inset 0 0 0 1px #434C89;perspective:1400px}
.b-cube{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;transform-origin:50% 50% -300px;animation:bCube .7s cubic-bezier(.3,.9,.3,1) both}
@keyframes bCube{from{transform:rotateY(80deg);opacity:0}to{transform:none;opacity:1}}
.b-px{position:absolute;left:50%;top:50%;width:18px;height:18px;margin:-9px;background:#E6E9FF;animation:bFly .8s cubic-bezier(.5,0,.2,1) both}
@keyframes bFly{from{transform:translate(calc(var(--x)*1px),calc(var(--y)*1px)) rotate(180deg);opacity:0}30%{opacity:1}85%{transform:translate(0,0) scale(.6);opacity:1}to{transform:scale(0);opacity:0}}
.b-built{animation:bBuilt .5s .7s cubic-bezier(.3,1.4,.5,1) both}
@keyframes bBuilt{from{opacity:0;transform:scale(.6)}to{opacity:1;transform:none}}
.b-warp{position:absolute;inset:0;z-index:12;overflow:hidden;background:#00010A}
.b-warp .tb-world{animation:bZoom 1.7s cubic-bezier(.6,0,.2,1) both}
@keyframes bZoom{from{transform:scale(.62) translateY(-60px);opacity:.6}to{transform:scale(1.6);opacity:1;filter:blur(2px)}}
.b-streak{position:absolute;left:50%;top:50%;height:2px;width:520px;transform-origin:0 50%;background:linear-gradient(90deg,rgba(230,233,255,0),#E6E9FF);animation:bStreak 1.1s calc(var(--d)*1ms) cubic-bezier(.6,0,.4,1) both}
@keyframes bStreak{from{transform:rotate(calc(var(--a)*1deg)) translateX(40px) scaleX(.1);opacity:0}40%{opacity:1}to{transform:rotate(calc(var(--a)*1deg)) translateX(1300px) scaleX(1.4);opacity:0}}
.b-wv{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;animation:aLand 1.2s cubic-bezier(.2,.8,.2,1) both}
.b-ship{position:absolute;left:50%;top:56%;width:960px;height:320px;margin:-160px 0 0 -480px;animation:bBuilt .6s 1s cubic-bezier(.3,1.4,.5,1) both}
.b-wpx{position:absolute;left:50%;top:56%;width:26px;height:26px;margin:-13px;background:#E6E9FF;animation:bFly 1.1s calc(var(--d)*1ms) cubic-bezier(.5,0,.2,1) both}
'''

B_BITS = [(-620, -300), (-480, 260), (-300, -380), (-120, 340), (60, -360), (220, 320), (420, -280), (600, 240), (-640, 40), (640, -40), (-360, 120), (380, -120)]


def b_box():
    ring = ('<div class="b-stage"><div class="b-ring" style="transform:translateZ(-600px) rotateY({{ring}}deg)">'
            '<sc-for list="{{toys}}" as="t" hint-placeholder-count="7">'
            '<button class="tb-card b-card {{t.cls}}" type="button" onClick="{{t.open}}" style="transform:rotateY({{t.rot}}deg) translateZ(600px)">'
            '<span class="b-spine"><span>{{t.name}}</span></span>'
            '<img class="b-obj" src="{{t.art}}" alt="">'
            '<span class="lab abs" style="right:18px;top:16px">{{t.group}}</span></button>'
            '</sc-for></div></div>')
    info = ('<div class="abs" style="left:0;right:0;top:760px;display:flex;flex-direction:column;align-items:center;gap:14px">'
            '<div class="abs" style="left:610px;top:-6px;display:flex;gap:22px;align-items:center;width:700px;justify-content:space-between">'
            '<button class="btn sm soft" type="button" onClick="{{prev}}" style="width:72px"><span class="kc">LT</span></button>'
            '<span></span><button class="btn sm soft" type="button" onClick="{{next}}" style="width:72px"><span class="kc">RT</span></button></div>'
            '<span style="font-size:34px">{{frontName}}</span>'
            '<span style="font-size:16px;color:#B8C0F0;max-width:620px;text-align:center;line-height:1.5">{{frontDesc}}</span>'
            '<span class="lab">{{frontCount}}</span>'
            '<button class="btn cta" type="button" onClick="{{openFront}}" style="width:260px;margin-top:6px"><span class="kc">A</span>Open</button></div>')
    return ring + info


def b_cfg():
    bits = ''.join('<span class="b-px" style="--x:%d;--y:%d;animation-delay:%dms"></span>' % (x, y, k * 30) for k, (x, y) in enumerate(B_BITS))
    face = '<div class="b-cube">%s</div>' % pv_inner(780, 260, 360)
    spawn = '<div class="abs" style="inset:0">%s<div class="abs b-built" style="inset:0;display:flex;align-items:center;justify-content:center"><span class="a-ring"></span></div></div>' % bits
    return ('<div class="b-flip" style="--fx:{{fx}};--fy:{{fy}}">'
            '<div class="b-pv"><img class="tb-world" src="%s" alt="" style="opacity:.42">'
            '<div class="abs" style="inset:0;background:radial-gradient(ellipse 60%% 60%% at 50%% 50%%,rgba(0,4,31,0),rgba(0,4,31,.8))"></div>'
            '<sc-if value="{{pvA}}" hint-placeholder-val="{{ true }}">%s</sc-if><sc-if value="{{pvB}}" hint-placeholder-val="{{ false }}">%s</sc-if>'
            '<sc-if value="{{spA}}" hint-placeholder-val="{{ false }}">%s</sc-if><sc-if value="{{spB}}" hint-placeholder-val="{{ false }}">%s</sc-if>'
            '<span class="lab abs" style="left:24px;top:20px;color:#E6E9FF">Live preview</span>'
            '<span class="abs" style="left:24px;bottom:20px;font-size:22px">{{pv.name}}</span></div>'
            '<div class="abs" style="left:0;top:0;width:250px;display:flex;flex-direction:column;gap:16px">'
            '<span class="lab">About</span><span style="font-size:16px;line-height:1.55;color:#B8C0F0">{{cur.desc}}</span></div>'
            '<div class="abs" style="left:280px;top:556px;width:1232px">' % (WORLD, face, face, spawn, spawn)
            + tiles(6, tile_h=110, icon=48, extra_cls='b-tile') +
            '</div><div class="abs" style="left:280px;top:690px;width:1232px;display:flex;justify-content:center">' + actions(620) + '</div></div>')


def b_dive():
    streaks = ''.join('<span class="b-streak" style="--a:%d;--d:%d"></span>' % (a, (a * 7) % 400) for a in range(0, 360, 15))
    return '<div class="b-warp"><img class="tb-world" src="%s" alt="">%s</div>' % (WORLD, streaks)


def b_world():
    bits = ''.join('<span class="b-wpx" style="--x:%d;--y:%d;--d:%d"></span>' % (x * 1.4, y * 1.2, k * 40) for k, (x, y) in enumerate(B_BITS))
    return ('<img class="b-wv" src="%s" alt=""><div class="abs" style="inset:0;background:radial-gradient(ellipse 55%% 50%% at 50%% 56%%,rgba(0,1,10,.0),rgba(0,1,10,.55))"></div>%s<img class="b-ship hull" src="{{ride}}" alt="">' % (WORLD, bits)) + world_hud()


# ================================================================== C  Slide open
# Seven narrow strips side by side, each with its name up the strip; the one you point at slides open wide and shows
# the toy, its line and its first variations. Open: the strip widens into the window. Switch: a diagonal wipe. The
# verb: five petals of light bloom and the thing drops in. Navigate: the window splits into slats that slide away.
C_CSS = '''
.c-row{position:absolute;left:64px;right:64px;top:236px;height:640px;display:flex;gap:12px}
.c-strip{position:relative;flex:0 0 150px;height:640px;transition:flex .6s cubic-bezier(.3,.9,.3,1),box-shadow .3s;animation:cIn .6s calc(var(--d)*1ms) cubic-bezier(.2,.8,.2,1) both}
@keyframes cIn{from{opacity:0;transform:translateY(-40px) scaleY(.8)}to{opacity:1;transform:none}}
.c-row:not(:hover) .c-strip:first-child,.c-strip:hover,.c-strip:focus-visible{flex:1 1 640px;box-shadow:inset 0 0 0 1px #E6E9FF}
.c-name{position:absolute;left:0;bottom:28px;width:150px;display:flex;justify-content:center}
.c-name span{writing-mode:vertical-rl;transform:rotate(180deg);font-size:26px;white-space:nowrap}
.c-thumb{position:absolute;left:25px;top:28px;width:100px;height:100px;object-fit:contain;transition:all .6s cubic-bezier(.3,.9,.3,1)}
.c-open{position:absolute;left:170px;top:28px;right:32px;bottom:28px;opacity:0;transform:translateX(30px);transition:opacity .35s,transform .5s;display:flex;flex-direction:column;gap:14px;pointer-events:none}
.c-row:not(:hover) .c-strip:first-child .c-open,.c-strip:hover .c-open,.c-strip:focus-visible .c-open{opacity:1;transform:none;transition-delay:.2s}
.c-row:not(:hover) .c-strip:first-child .c-thumb,.c-strip:hover .c-thumb,.c-strip:focus-visible .c-thumb{left:170px;top:150px;width:300px;height:300px}
.c-panel{position:absolute;left:64px;right:64px;top:236px;height:760px;animation:cWide .7s cubic-bezier(.3,.9,.3,1) both}
@keyframes cWide{from{clip-path:inset(0 calc(var(--fx)*1px) 120px calc(var(--fy)*1px))}to{clip-path:inset(0 0 0 0)}}
.c-pv{position:absolute;left:0;top:0;width:1180px;height:760px;overflow:hidden;box-shadow:inset 0 0 0 1px #434C89}
.c-wipe{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;animation:cWipe .65s cubic-bezier(.6,0,.2,1) both}
@keyframes cWipe{from{clip-path:polygon(0 0,0 0,-30% 100%,-30% 100%)}to{clip-path:polygon(0 0,130% 0,100% 100%,-30% 100%)}}
.c-petal{position:absolute;left:50%;top:50%;width:70px;height:190px;margin:-190px 0 0 -35px;transform-origin:50% 100%;background:linear-gradient(180deg,rgba(230,233,255,0),rgba(230,233,255,.55));clip-path:polygon(50% 0,100% 45%,50% 100%,0 45%);animation:cBloom 1s cubic-bezier(.2,.8,.2,1) both}
@keyframes cBloom{0%{transform:rotate(calc(var(--a)*1deg)) scaleY(0);opacity:0}40%{transform:rotate(calc(var(--a)*1deg + 20deg)) scaleY(1);opacity:1}100%{transform:rotate(calc(var(--a)*1deg + 50deg)) scaleY(1.3);opacity:0}}
.c-drop{animation:cDrop .7s .35s cubic-bezier(.3,1.3,.5,1) both}
@keyframes cDrop{from{transform:translateY(-260px) scale(.7);opacity:0}to{transform:none;opacity:1}}
.c-shut{position:absolute;inset:0;z-index:12;overflow:hidden;background:#00010A}
.c-shut .tb-world{animation:cSettle 1.7s cubic-bezier(.2,.8,.2,1) both}
@keyframes cSettle{from{transform:scale(1.35)}to{transform:scale(1.05)}}
.c-slat{position:absolute;left:0;width:1920px;height:136px;background:rgba(0,4,31,.97);box-shadow:inset 0 -1px 0 #222645;animation:cSlat .9s calc(var(--d)*1ms) cubic-bezier(.6,0,.2,1) both}
@keyframes cSlat{from{transform:none}to{transform:translateX(calc(var(--s)*2000px))}}
.c-wv{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;transform:scale(1.05)}
.c-ship{position:absolute;left:50%;top:56%;width:960px;height:320px;margin:-160px 0 0 -480px;animation:cDrop 1s .2s cubic-bezier(.3,1.25,.5,1) both}
.c-wtrail{position:absolute;left:50%;top:0;width:4px;height:56%;margin-left:-2px;background:linear-gradient(180deg,rgba(230,233,255,0),#E6E9FF);transform-origin:50% 0;animation:aTrail 1.1s .1s ease-out both}
'''


def c_from():
    # clip insets for the open (right, left) from each strip's place in the 1792 px row (the open strip is 640 + 6 x 150)
    out = []
    for i in range(7):
        left = i * 162
        width = 640 if i == 0 else 150
        out.append([1792 - left - width, left, 1])
    return out


def c_box():
    row = ('<div class="c-row"><sc-for list="{{toys}}" as="t" hint-placeholder-count="7">'
           '<button class="tb-card c-strip" type="button" onClick="{{t.open}}" style="--d:{{t.d}}">'
           '<img class="c-thumb tb-bob" src="{{t.art}}" alt="">'
           '<span class="c-name"><span>{{t.name}}</span></span>'
           '<span class="c-open"><span class="lab">{{t.group}} &#160;&#183;&#160; {{t.count}}</span>'
           '<span style="font-size:36px;line-height:1.1">{{t.name}}</span>'
           '<span style="font-size:16px;line-height:1.5;color:#B8C0F0;max-width:420px">{{t.desc}}</span>'
           '<span style="margin-top:auto;align-self:flex-end;display:flex;gap:10px;align-items:center"><span class="kc">A</span><span class="lab" style="color:#E6E9FF">Open</span></span></span>'
           '</button></sc-for></div>')
    foot = ('<div class="abs tb-fade" style="left:64px;top:900px;font-size:15px;color:#747BAD">'
            'Point along the row: each toy slides open. Open one: it widens into the window.</div>')
    return row + foot


def c_cfg():
    face = '<div class="c-wipe">%s</div>' % pv_inner(840, 280, 400)
    petals = ''.join('<span class="c-petal" style="--a:%d"></span>' % a for a in range(0, 360, 72))
    spawn = '<div class="abs" style="inset:0">%s</div>' % petals
    return ('<div class="c-panel" style="--fx:{{fx}};--fy:{{fy}}">'
            '<div class="c-pv"><img class="tb-world" src="%s" alt="" style="opacity:.42">'
            '<div class="abs" style="inset:0;background:radial-gradient(ellipse 60%% 60%% at 50%% 50%%,rgba(0,4,31,0),rgba(0,4,31,.8))"></div>'
            '<sc-if value="{{pvA}}" hint-placeholder-val="{{ true }}">%s</sc-if><sc-if value="{{pvB}}" hint-placeholder-val="{{ false }}">%s</sc-if>'
            '<sc-if value="{{spA}}" hint-placeholder-val="{{ false }}">%s</sc-if><sc-if value="{{spB}}" hint-placeholder-val="{{ false }}">%s</sc-if>'
            '<span class="lab abs" style="left:24px;top:20px;color:#E6E9FF">Live preview</span>'
            '<span class="abs" style="left:24px;bottom:20px;font-size:22px">{{pv.name}}</span></div>'
            '<div class="abs tb-rise" style="left:1212px;top:0;right:0;bottom:0;display:flex;flex-direction:column;gap:18px;animation-delay:.3s">'
            '<span style="font-size:16px;line-height:1.55;color:#B8C0F0">{{cur.desc}}</span><span class="lab">Variations</span>'
            % (WORLD, face, face, spawn, spawn)
            + tiles(1, tile_h=70, gap=10) + '<div style="margin-top:auto">' + actions() + '</div></div></div>')


def c_dive():
    slats = ''.join('<span class="c-slat" style="top:%dpx;--s:%d;--d:%d"></span>' % (k * 136, 1 if k % 2 else -1, 80 + k * 70) for k in range(8))
    return '<div class="c-shut"><img class="tb-world" src="%s" alt="">%s</div>' % (WORLD, slats)


def c_world():
    petals = ''.join('<span class="c-petal" style="--a:%d;top:56%%;animation-delay:1s"></span>' % a for a in range(0, 360, 72))
    return ('<img class="c-wv" src="%s" alt=""><div class="abs" style="inset:0;background:radial-gradient(ellipse 55%% 50%% at 50%% 56%%,rgba(0,1,10,.0),rgba(0,1,10,.55))"></div><span class="c-wtrail"></span><img class="c-ship hull" src="{{ride}}" alt="">%s' % (WORLD, petals)) + world_hud()


# ================================================================== D  Lens (recommended)
# Each card holds a round lens onto the cell with the toy floating in it; the name sits under the lens, centred. Open:
# the lens irises open into Configure's big round preview, with the variations on a dial around it. Switch: the lens
# blinks shut and opens on the new pick. The verb: rings ripple out of it. Navigate: the lens grows past the screen,
# rings rush past, and your vessel rises out of the centre point on a ripple.
D_CARD = (300, 360)
D_POS = [(130, 236), (470, 236), (810, 236), (1150, 236), (300, 616), (640, 616), (980, 616)]
D_POS = [(x + 170, y) for x, y in D_POS]
D_LENS = (520, 610, 300)  # Configure's lens centre and radius


def d_from():
    out = []
    for x, y in D_POS:
        cx, cy = x + D_CARD[0] / 2, y + 30 + 100
        out.append([round(cx), round(cy), 100])
    return out


D_DIAL = []
for k in range(6):
    import math
    a = math.radians(-62 + k * 25)
    D_DIAL.append([round(D_LENS[0] + math.cos(a) * 420), round(D_LENS[1] + math.sin(a) * 420)])

D_CSS = '''
.d-card{width:300px;height:360px;animation:dIn .6s calc(var(--d)*1ms) cubic-bezier(.2,.8,.2,1) both;transition:transform .35s cubic-bezier(.2,.8,.2,1),box-shadow .35s}
@keyframes dIn{from{opacity:0;transform:scale(.85)}to{opacity:1;transform:none}}
.d-card:hover,.d-card:focus-visible{transform:translateY(-8px);box-shadow:inset 0 0 0 1px #E6E9FF}
.d-lens{position:absolute;left:50px;top:30px;width:200px;height:200px;border-radius:50%;overflow:hidden;box-shadow:inset 0 0 0 1px #434C89;transition:transform .5s cubic-bezier(.2,.8,.2,1)}
.d-lens .tb-world{opacity:.22;transition:transform 1.2s ease}
.d-card:hover .d-lens{transform:scale(1.08)}
.d-card:hover .d-lens .tb-world{transform:scale(1.4)}
.d-lens .d-obj{position:absolute;left:20px;top:20px;width:160px;height:160px;object-fit:contain;transition:transform .8s cubic-bezier(.2,.8,.2,1)}
.d-card:hover .d-obj{transform:rotate(25deg) scale(1.1)}
.d-rip{position:absolute;left:50px;top:30px;width:200px;height:200px;border-radius:50%;box-shadow:0 0 0 1px #E6E9FF;opacity:0;pointer-events:none}
.d-card:hover .d-rip{animation:dRip 1.4s ease-out infinite}
@keyframes dRip{from{transform:scale(1);opacity:.8}to{transform:scale(1.35);opacity:0}}
.d-iris{position:absolute;left:0;top:0;width:1920px;height:1080px;animation:dIris .8s cubic-bezier(.3,.9,.3,1) both}
@keyframes dIris{from{clip-path:circle(calc(var(--fs)*1px) at calc(var(--fx)*1px) calc(var(--fy)*1px))}to{clip-path:circle(1400px at 520px 610px)}}
.d-big{position:absolute;border-radius:50%;overflow:hidden;box-shadow:inset 0 0 0 1px #434C89}
.d-blink{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;animation:dBlink .6s cubic-bezier(.6,0,.2,1) both}
@keyframes dBlink{from{clip-path:circle(0 at 50% 50%)}to{clip-path:circle(75% at 50% 50%)}}
.d-wave{position:absolute;left:50%;top:50%;width:200px;height:200px;margin:-100px;border-radius:50%;box-shadow:0 0 0 2px #E6E9FF;animation:dWave 1.2s cubic-bezier(.2,.8,.2,1) both;pointer-events:none}
@keyframes dWave{from{transform:scale(.2);opacity:1}to{transform:scale(3.2);opacity:0}}
.d-dial{position:absolute;width:210px;margin:-36px 0 0 0;animation:tbRise .45s cubic-bezier(.2,.8,.2,1) both}
.d-dive{position:absolute;inset:0;z-index:12;overflow:hidden;background:#00010A;animation:dGrow 1.1s cubic-bezier(.6,0,.2,1) both}
@keyframes dGrow{from{clip-path:circle(300px at 520px 610px)}to{clip-path:circle(1300px at 960px 540px)}}
.d-dive .tb-world{animation:dIn2 1.7s cubic-bezier(.5,0,.2,1) both}
@keyframes dIn2{from{transform:scale(1.7) translate(-120px,40px)}to{transform:scale(1.05)}}
.d-tun{position:absolute;left:50%;top:50%;width:160px;height:160px;margin:-80px;border-radius:50%;box-shadow:0 0 0 2px rgba(230,233,255,.7);animation:dTun 1.3s calc(var(--d)*1ms) cubic-bezier(.6,0,.6,1) both}
@keyframes dTun{from{transform:scale(.1);opacity:0}30%{opacity:1}to{transform:scale(14);opacity:0}}
.d-wv{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;transform:scale(1.05)}
.d-ship{position:absolute;left:50%;top:54%;width:960px;height:320px;margin:-160px 0 0 -480px;animation:dRise 1.1s .5s cubic-bezier(.2,1.2,.4,1) both}
@keyframes dRise{from{transform:scale(0);opacity:0;filter:brightness(3)}60%{opacity:1}to{transform:none;opacity:1;filter:none}}
.d-dot{position:absolute;left:50%;top:54%;width:14px;height:14px;margin:-7px;border-radius:50%;background:#E6E9FF;animation:dDot .6s both}
@keyframes dDot{0%{transform:scale(0)}60%{transform:scale(1.4)}100%{transform:scale(0)}}
.d-ww{position:absolute;left:50%;top:54%;width:240px;height:240px;margin:-120px;border-radius:50%;box-shadow:0 0 0 2px #E6E9FF;animation:dWave 1.4s calc(var(--d)*1ms) cubic-bezier(.2,.8,.2,1) both}
'''


def d_box():
    cards = ('<sc-for list="{{toys}}" as="t" hint-placeholder-count="7">'
             '<button class="tb-card d-card" type="button" onClick="{{t.open}}" style="left:{{t.x}}px;top:{{t.y}}px;--d:{{t.d}}">'
             '<span class="d-lens"><img class="tb-world" src="%s" alt=""><img class="d-obj tb-bob" src="{{t.art}}" alt=""></span>'
             '<span class="d-rip"></span>'
             '<span class="abs" style="left:0;right:0;top:254px;text-align:center;font-size:24px">{{t.name}}</span>'
             '<span class="lab abs" style="left:0;right:0;top:292px;text-align:center">{{t.group}} &#160;&#183;&#160; {{t.count}}</span>'
             '</button></sc-for>') % WORLD
    foot = ('<div class="abs tb-fade" style="right:64px;top:1010px;font-size:15px;color:#747BAD">'
            'Point at a toy: its lens widens and ripples. Open it: the lens irises open into the preview.</div>')
    return cards + foot


def d_cfg():
    cx, cy, r = D_LENS
    face = '<div class="d-blink">%s</div>' % pv_inner(560, 187, 320)
    waves = '<span class="d-wave"></span><span class="d-wave" style="animation-delay:.15s"></span><span class="d-wave" style="animation-delay:.3s"></span>'
    lens = ('<div class="d-big" style="left:%dpx;top:%dpx;width:%dpx;height:%dpx">'
            '<img class="tb-world" src="%s" alt="" style="opacity:.6">'
            '<div class="abs" style="inset:0;background:radial-gradient(circle at 50%% 50%%,rgba(0,4,31,0) 40%%,rgba(0,4,31,.85))"></div>'
            '<sc-if value="{{pvA}}" hint-placeholder-val="{{ true }}">%s</sc-if><sc-if value="{{pvB}}" hint-placeholder-val="{{ false }}">%s</sc-if>'
            '<sc-if value="{{spA}}" hint-placeholder-val="{{ false }}">%s</sc-if><sc-if value="{{spB}}" hint-placeholder-val="{{ false }}">%s</sc-if>'
            '</div>') % (cx - r, cy - r, 2 * r, 2 * r, WORLD, face, face, waves, waves)
    caption = ('<span class="lab abs" style="left:%dpx;top:%dpx;width:%dpx;text-align:center;color:#E6E9FF">Live preview</span>'
               '<span class="abs" style="left:%dpx;top:%dpx;width:%dpx;text-align:center;font-size:24px">{{pv.name}}</span>'
               % (cx - r, cy + r + 18, 2 * r, cx - r, cy + r + 44, 2 * r))
    dial = ('<sc-for list="{{rows}}" as="r" hint-placeholder-count="6">'
            '<button class="tb-tile d-dial {{r.cls}}" type="button" onClick="{{r.pick}}" style="left:{{r.dx}}px;top:{{r.dy}}px;height:72px;padding:0 14px;animation-delay:{{r.dl}}ms">'
            '<sc-if value="{{r.hasImg}}" hint-placeholder-val="{{ true }}"><img src="{{r.img}}" alt="" style="width:44px;height:44px;object-fit:contain;flex:none"></sc-if>'
            '<sc-if value="{{r.hasSw}}" hint-placeholder-val="{{ false }}"><span class="dia" style="background:{{r.sw}};width:20px;height:20px;margin:0 4px"></span></sc-if>'
            '<span style="display:flex;flex-direction:column;gap:4px;min-width:0"><span class="ell" style="font-size:17px">{{r.n}}</span>'
            '<span class="lab ell" style="font-size:12px;color:{{r.dc}}">{{r.d}}</span></span></button></sc-for>')
    arc = ('<svg class="abs" width="1920" height="1080" style="left:0;top:0;pointer-events:none"><circle cx="%d" cy="%d" r="420" fill="none" stroke="#222645" stroke-width="1" stroke-dasharray="2 10"></circle></svg>' % (cx, cy))
    side = ('<div class="abs tb-rise" style="left:1300px;top:420px;width:556px;display:flex;flex-direction:column;gap:22px;animation-delay:.35s">'
            '<span class="lab">About</span><span style="font-size:17px;line-height:1.55;color:#B8C0F0">{{cur.desc}}</span>'
            '<span class="lab" style="margin-top:20px">Picked</span><span style="font-size:30px">{{pv.name}}</span>'
            '<div style="margin-top:24px">' + actions() + '</div></div>')
    return ('<div class="d-iris" style="--fx:{{fx}};--fy:{{fy}};--fs:{{fs}}">' + arc + lens + caption + dial + '</div>' + side)


def d_dive():
    tun = ''.join('<span class="d-tun" style="--d:%d"></span>' % (k * 120) for k in range(7))
    return '<div class="d-dive"><img class="tb-world" src="%s" alt="">%s</div>' % (WORLD, tun)


def d_world():
    waves = ''.join('<span class="d-ww" style="--d:%d"></span>' % d for d in (550, 750, 950))
    return ('<img class="d-wv" src="%s" alt=""><div class="abs" style="inset:0;background:radial-gradient(ellipse 55%% 50%% at 50%% 56%%,rgba(0,1,10,.0),rgba(0,1,10,.55))"></div><span class="d-dot"></span>%s<img class="d-ship hull" src="{{ride}}" alt="">' % (WORLD, waves)) + world_hud()


# ================================================================== boards
OPTS = [
    ('A', 'Portal', A_CSS, a_box, a_cfg, a_dive, a_world,
     {'from': a_from(), 'cards': [{'x': A_LEFT + i * (A_W + A_GAP)} for i in range(7)]},
     'Tall cards dealt in a row; the verb slides up on hover; the card grows into the preview; you fly through the toy and your vessel streaks in'),
    ('B', 'Orbit', B_CSS, b_box, b_cfg, b_dive, b_world,
     {'from': [[0, 0, 1]] * 7, 'orbit': True},
     'The toys on a ring around you, LT / RT turns it; the card flips to Configure; the preview turns like a cube; warp streaks and a vessel built from prisms'),
    ('C', 'Slide open', C_CSS, c_box, c_cfg, c_dive, c_world,
     {'from': c_from()},
     'Seven strips that slide open as you point; the strip widens into the window; a diagonal wipe; petals bloom; the window splits into slats and your vessel drops in'),
    ('D', 'Lens', D_CSS, d_box, d_cfg, d_dive, d_world,
     {'from': d_from(), 'cards': [{'x': x, 'y': y} for x, y in D_POS], 'dialPos': D_DIAL, 'diveMs': 1600},
     'A round lens on every card; it irises open into a round preview with the variations on a dial; the lens blinks to switch; you dive through it and rise out of a ripple (recommended)'),
]


def option(letter, name, css, box, cfg, dive, world, opt, line):
    title = 'Rev %s %s - %s: %s - interactive' % (REV, letter, name, line)
    return board(title, css, box(), cfg(), dive(), world(), opt)


def compare_board():
    title = 'Rev %s Compare - the toy card four ways, the same toy (Spawn Matrix), at rest and pointed at' % REV
    t = TOYS[1]
    cells = []
    xs = [64, 520, 976, 1432]
    for k, (letter, name, *_r) in enumerate(OPTS):
        x = xs[k]
        if letter == 'A':
            card = ('<div class="a-wrap" style="left:0;top:0;width:230px;height:420px"><div class="tb-card a-card %s" style="--d:0;%s">'
                    '<span class="lab abs" style="left:20px;top:18px">Creation</span><img class="a-obj" src="%s" alt="">'
                    '<span class="a-words" style="%s"><span style="display:block;font-size:24px">Spawn Matrix</span>'
                    '<span style="display:block;margin-top:8px;font-size:14px;line-height:1.45;color:#B8C0F0">%s</span></span>%s</div></div>')
            rest = card % ('', '', t['art'], '', 'The release bench.', '')
            held = card % ('', 'transform:translateY(-14px) rotateX(6deg);box-shadow:inset 0 0 0 1px #E6E9FF', t['art'], 'transform:translateY(-54px)', 'The release bench.',
                           '<span class="btn sm cta abs" style="left:20px;right:20px;bottom:20px;height:40px"><span class="kc">A</span>Open</span>')
            pair = '<div class="abs" style="left:0;top:0">%s</div><div class="abs" style="left:200px;top:0;transform:scale(.0001)"></div>' % rest
            pair = ('<div class="abs" style="left:0;top:40px;transform:scale(.78);transform-origin:0 0">%s</div>'
                    '<div class="abs" style="left:198px;top:40px;transform:scale(.78);transform-origin:0 0">%s</div>') % (rest, held)
        elif letter == 'B':
            c = ('<div class="tb-card" style="position:relative;width:380px;height:300px;%s"><span class="b-spine"><span>Spawn Matrix</span></span>'
                 '<img class="b-obj" src="%s" alt=""><span class="lab abs" style="right:18px;top:16px">Creation</span></div>')
            pair = ('<div class="abs" style="left:0;top:40px;transform:perspective(1200px) rotateY(28deg) scale(.6);transform-origin:0 0;opacity:.55">%s</div>'
                    '<div class="abs" style="left:110px;top:150px;transform:scale(.78);transform-origin:0 0">%s</div>') % (
                c % ('', t['art']), c % ('box-shadow:inset 0 0 0 1px #E6E9FF', t['art']))
        elif letter == 'C':
            closed = ('<div class="tb-card" style="position:relative;width:150px;height:520px"><img class="c-thumb" src="%s" alt="">'
                      '<span class="c-name"><span>Spawn Matrix</span></span></div>') % t['art']
            opened = ('<div class="tb-card" style="position:relative;width:520px;height:520px;box-shadow:inset 0 0 0 1px #E6E9FF">'
                      '<img class="c-thumb" src="%s" alt="" style="left:170px;top:150px;width:260px;height:260px">'
                      '<span class="c-name"><span>Spawn Matrix</span></span>'
                      '<span class="abs" style="left:170px;top:28px;right:24px;display:flex;flex-direction:column;gap:12px">'
                      '<span class="lab">Creation &#160;&#183;&#160; 6 ways</span><span style="font-size:30px">Spawn Matrix</span>'
                      '<span style="font-size:15px;color:#B8C0F0;line-height:1.5">The release bench.</span></span></div>') % t['art']
            pair = ('<div class="abs" style="left:0;top:40px;transform:scale(.6);transform-origin:0 0;display:flex;gap:10px">%s%s</div>' % (closed, opened))
        else:
            c = ('<div class="tb-card" style="position:relative;width:300px;height:360px;%s"><span class="d-lens" style="%s"><img class="tb-world" src="%s" alt="">'
                 '<img class="d-obj" src="%s" alt=""></span>%s'
                 '<span class="abs" style="left:0;right:0;top:254px;text-align:center;font-size:24px">Spawn Matrix</span>'
                 '<span class="lab abs" style="left:0;right:0;top:292px;text-align:center">Creation &#160;&#183;&#160; 6 ways</span></div>')
            rest = c % ('', '', WORLD, t['art'], '')
            held = c % ('box-shadow:inset 0 0 0 1px #E6E9FF', 'transform:scale(1.08)', WORLD, t['art'],
                        '<span class="abs" style="left:38px;top:18px;width:224px;height:224px;border-radius:50%;box-shadow:0 0 0 1px rgba(230,233,255,.5)"></span>')
            pair = ('<div class="abs" style="left:0;top:40px;transform:scale(.72);transform-origin:0 0">%s</div>'
                    '<div class="abs" style="left:0;top:330px;transform:scale(.72);transform-origin:0 0">%s</div>') % (rest, held)
        cells.append('<div class="abs" style="left:%dpx;top:210px;width:424px;height:640px"><div style="display:flex;gap:12px;align-items:baseline">'
                     '<span class="lab" style="color:#E6E9FF">%s</span><span style="font-size:20px">%s</span></div>%s</div>' % (x, letter, name, pair))
    moves = [
        ('Open', 'the card grows into the preview', 'the card flips; its back is Configure', 'the strip widens into the window', 'the lens irises open'),
        ('Switch', 'film strip with a light sweep', 'the preview turns like a cube', 'a diagonal wipe', 'the lens blinks shut and open'),
        ('Spawn', 'two rings and a pop', 'prisms fly in and build it', 'five petals bloom, it drops in', 'three ripples'),
        ('Navigate', 'through the toy&#8217;s ring', 'warp streaks', 'the window splits into slats', 'the lens grows past you, a tunnel of rings'),
        ('Arrive', 'streaks in from the horizon', 'built from prisms', 'drops in on a trail', 'rises out of a point on a ripple'),
    ]
    table = '<div class="abs" style="left:64px;top:870px;right:64px;display:grid;grid-template-columns:140px repeat(4,1fr);row-gap:10px;column-gap:24px">'
    for row in moves:
        table += '<span class="lab" style="color:#E6E9FF">%s</span>' % row[0] + ''.join('<span style="font-size:15px;color:#B8C0F0">%s</span>' % c for c in row[1:])
    table += '</div>'
    body = (BACKDROP + HUB + WINDOW +
            '<div class="abs" style="left:64px;top:156px;display:flex;align-items:baseline;gap:22px"><span style="font-size:28px">The toy card, four ways</span>'
            '<span class="lab">Spawn Matrix at rest and pointed at; each option board plays the whole flow</span></div>'
            + ''.join(cells) + table)
    return page_css(title, body, NOLOGIC, A_CSS + B_CSS + C_CSS + D_CSS)


NOLOGIC = '\nclass Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n'


def notes_board():
    title = 'Rev %s Notes - what was asked, what each option does, and what it costs in Unity' % REV

    def block(head, lines):
        return ('<div style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">%s</span>' % head
                + ''.join('<span style="font-size:15px;line-height:1.55;color:#B8C0F0">%s</span>' % l for l in lines) + '</div>')
    cols = [
        block('Asked (Shombith, 2026-11-24)', [
            '&#8220;I want to redesign the toybox UI in a more innovative way, spawning or switching or navigating should have animations and the whole cards (bg is good) orientation inside each main card and the preview screen with a navigate animation which takes you inside the preview screen and spawns you on a cool way. Create 4 different UI variations for this.&#8221;',
            'Kept: the card background (navy, the sea lines, the toy&#8217;s codex portrait), the seven toys, their words, Toy configure&#8217;s variations and its two actions (the toy&#8217;s verb, Navigate).',
            'Changed in every option: how a card lays out its words and portrait, and five moments that are cuts today: open, switch, the verb, Navigate, and arriving in the world.',
            'Every option board is playable: open a toy, pick a variation, press its verb, then Navigate. &#8220;Board: start again&#8221; (bottom left) goes back to the Toy Box.']),
        block('The four', [
            '<b>A Portal.</b> Tall cards dealt in a row. Pointing lifts a card and slides the verb up. Open grows the card into the preview on the left (the Configure layout picked in Rev 33 / 43). Navigate: you fly through the toy&#8217;s ring and your vessel streaks in from the horizon.',
            '<b>B Orbit.</b> The toys stand on a ring around you; LT / RT turns it, and only the card facing you has words. Open flips the card. The preview turns like a cube; the verb builds the thing from prisms; Navigate is a warp and your vessel is built from prisms.',
            '<b>C Slide open.</b> Seven strips, names up their spines; the one you point at slides open (Rev 117&#8217;s &#8220;cards that slide open&#8221;). The strip widens into the window, a diagonal wipe switches, petals bloom on the verb, and the window splits into slats as you leave.',
            '<b>D Lens (recommended).</b> Each card is a lens onto the cell with the toy inside. Open irises the lens into a big round preview with the variations on a dial; switching blinks the lens; Navigate grows the lens past the screen and you rise out of a ripple. It is the only one where &#8220;inside the preview&#8221; is literal: the same circle carries you from card to world, and the ripple matches the avatar ring picked in Rev 127.']),
        block('What each costs in Unity', [
            'All four are UI tweens (DOTween / the kit&#8217;s KitMotion) on RectTransforms, CanvasGroups and one mask; nothing runs at rest but the portrait bob already shipped (Card_PortraitBob.mat).',
            '<b>A</b> one scale + position tween from the card&#8217;s rect to the preview; the dive scales the preview RawImage to full screen while the camera pushes toward the station ring.',
            '<b>B</b> seven cards on a 3D ring (one rotation tween; world-space or a perspective canvas). The prism bursts are a pooled UI particle set. The most work of the four.',
            '<b>C</b> a HorizontalLayoutGroup with preferred-width tweens; the slats are eight Images; the petals are one sprite turned five times.',
            '<b>D</b> one circular mask whose radius is tweened (a SoftMask or a cutout shader on the preview RawImage); the dial is a fixed set of six anchors; the ripples are one sprite.',
            'Navigate in every option is the UI handing over to the camera: the preview camera (ToyPreviewCamera) blends into the gameplay camera at the station, and the vessel&#8217;s arrival is the spawn effect at that point. The board fakes both with the cell frame.',
            'Built only after a pick, through /frogma-to-unity on Toy configure and the Toy Box generators (author_menu_toybox.py, author_menu_toyconfigure.py).'])]
    body = (BACKDROP + HUB + WINDOW +
            '<div class="abs" style="left:64px;top:156px"><span style="font-size:28px">Notes</span></div>'
            '<div class="abs" style="left:64px;top:220px;right:64px;display:grid;grid-template-columns:1fr 1.15fr 1.15fr;gap:56px">'
            + ''.join(cols) + '</div>')
    return page_css(title, body, NOLOGIC, '', 900)


os.makedirs(out, exist_ok=True)
_idx = os.path.join(out, 'canvas.json')
PLANNED = json.load(open(_idx))['boards'] if os.path.exists(_idx) else {}
files = {('R%s%s.dc.html' % (REV, o[0])): option(*o) for o in OPTS}
files['R%sCompare.dc.html' % REV] = compare_board()
files['R%sNotes.dc.html' % REV] = notes_board()
import re as _re
for name, html in files.items():
    if name in PLANNED:  # the title the plan put in the index is the board's title
        html = _re.sub(r'<title>.*?</title>', lambda m: '<title>' + PLANNED[name]['title'].replace('&', '&amp;') + '</title>', html, count=1)
    open(os.path.join(out, name), 'w').write(html)
    print('wrote', name, len(html))
