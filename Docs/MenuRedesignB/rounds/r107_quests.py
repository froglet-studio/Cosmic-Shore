#!/usr/bin/env python3
"""Quests, second round (2026-11-14): B and C mixed, a reward section (a colour set per quest), no "Episode 1".

Usage: python3 r107_quests.py <root> 107
Shares Rev 101's kit and quest data: the part of r101_quests.py above its boards is run here.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, REV = sys.argv[1], sys.argv[2]
_src = open(os.path.join(HERE, 'r101_quests.py')).read()
_src = _src[:_src.index('# ---------- A: the star chart')]
sys.argv = [sys.argv[0], root]
exec(_src)  # STYLE, BACKDROP, HUB, ART, SEA, Q, INIT, page, TOPROW, CTA, qpath, SOFT_LINE, out

# One colour set per quest, from Docs/VesselModels/colour_sets.json (rim swatch, face swatch per domain). The
# assignment is a proposal; Nocturne (the default) and the accessibility sets stay free for everyone.
_cs = os.path.join(HERE, '..', '..', 'VesselModels', 'colour_sets.json')
if not os.path.exists(_cs):
    _cs = '/home/user/Cosmic-Shore/Docs/VesselModels/colour_sets.json'
SETS = json.load(open(_cs))['sets']
BLURB = {
    'ukiyo_e': 'Edo woodblock prints: each body is its edge printed deeper.',
    'dusk': 'The hush after sunset: soft edges over dusky bodies.',
    'neon_noir': 'Neon tubes over wet asphalt.',
    'blue_ringed_octopus': 'Sand skin, electric-blue rings, black outlines.',
    'nacre': 'The inside of a shell: pastel bodies under clearer edges.',
}
REWARD = ['ukiyo_e', 'dusk', 'neon_noir', 'blue_ringed_octopus', 'nacre']
DOM = ['Fire', 'Gold', 'Lime', 'Jade', 'Blue', 'Ruby']
for q, key in zip(Q, REWARD):
    s = SETS[key]
    q['reward'] = s['name']
    q['rewardBlurb'] = BLURB[key]
    q['sw'] = [[s['domains'][d.lower()]['rim']['swatch'], s['domains'][d.lower()]['face']['swatch']] for d in DOM]

# D: the route across the top. E: the route down the left.
PD = [(230, 470), (590, 410), (950, 470), (1310, 410), (1670, 470)]
PE = [(104, 250), (104, 410), (104, 570), (104, 730), (104, 890)]
SEG_D = [qpath(PD[k], PD[k + 1], 30 if k % 2 == 0 else -30) for k in range(4)]
SEG_E = [('M104 %d L104 %d' % (PE[k][1] + 14, PE[k + 1][1] - 14), PE[k + 1][1] - PE[k][1] - 26) for k in range(4)]

LOGIC = '''
const Q = %(Q)s;
const INIT = %(INIT)s;
const DOM = %(DOM)s;
const PD = %(PD)s;
const PE = %(PE)s;
const LD = %(LD)s;
const LE = %(LE)s;
class Component extends DCLogic {
constructor(p) { super(p); this.state = { s: INIT.map((x) => ({ st: x.st, done: x.done })), sel: 2 }; }
renderVals() {
  const st = this.state, s = st.s, sel = st.sel;
  const line = (i) => { const x = s[i]; return x.st === 'claimed' ? 'Claimed' : x.st === 'ready' ? 'Ready to claim'
    : x.st === 'open' ? x.done + ' of ' + Q[i].goals.length + ' goals' : 'After ' + Q[i - 1].name; };
  const ring = (i) => s[i].st === 'claimed' ? '#E6E9FF' : s[i].st === 'ready' ? '#99FF80' : s[i].st === 'open' ? '#E6E9FF' : '#434C89';
  const quests = Q.map((q, i) => { const on = i === sel, lock = s[i].st === 'locked';
    const w = on ? 280 : 200, h = on ? 158 : 112;
    return { n: q.n, name: q.name, art: q.art, line: line(i), reward: q.reward,
      ink: lock ? '#747BAD' : '#E6E9FF', lineInk: s[i].st === 'ready' ? '#99FF80' : '#747BAD',
      fill: s[i].st === 'claimed' ? '#E6E9FF' : 'rgba(0,0,0,0)', ring: ring(i),
      size: on ? '22px' : '16px', under: on ? '#00D4FF' : 'rgba(0,0,0,0)', pressed: on ? 'true' : 'false',
      pick: () => this.setState({ sel: i }),
      cw: w + 'px', ch: h + 'px', cl: (PD[i][0] - w / 2) + 'px', ct: (PD[i][1] - h - 30) + 'px',
      cop: lock ? 0.28 : on ? 1 : 0.6, cfilt: lock ? 'grayscale(1) brightness(.7)' : 'none',
      cx: (PD[i][0] - 140) + 'px', cy: (PD[i][1] - 16) + 'px',
      ey: (PE[i][1] - 16) + 'px', eop: lock ? 0.3 : on ? 1 : 0.6, efilt: lock ? 'grayscale(1) brightness(.7)' : 'none',
      ebg: on ? 'linear-gradient(90deg,rgba(0,4,31,.92),rgba(0,4,31,0))' : 'rgba(0,0,0,0)' }; });
  const seg = (L, k) => { const lit = s[k + 1].st !== 'locked'; return { len: L[k], off: lit ? 0 : L[k] }; };
  const q = Q[sel], x = s[sel];
  const goals = q.goals.map((g, j) => { const ok = x.st === 'claimed' || x.st === 'ready' || j < x.done;
    const cur = x.st === 'open' && j === x.done;
    return { text: g, tier: q.tiers[j], fill: ok ? '#E6E9FF' : 'rgba(0,0,0,0)', ring: ok || cur ? '#E6E9FF' : '#434C89',
      ink: ok || cur ? '#E6E9FF' : '#747BAD', note: ok ? 'Done' : cur ? 'Next' : '' }; });
  const claimRow = { text: 'Claim', tier: 'Unlocks ' + q.reward,
    fill: x.st === 'claimed' ? '#E6E9FF' : 'rgba(0,0,0,0)', ring: x.st === 'ready' ? '#99FF80' : x.st === 'claimed' ? '#E6E9FF' : '#434C89',
    ink: x.st === 'ready' || x.st === 'claimed' ? '#E6E9FF' : '#747BAD', note: x.st === 'claimed' ? 'Claimed' : x.st === 'ready' ? 'Ready' : '' };
  const steps = goals.concat([claimRow]);
  const claimed = s.filter((y) => y.st === 'claimed').length;
  const meter = s.map((y) => ({ c: y.st === 'claimed' ? '#E6E9FF' : y.st === 'ready' ? '#99FF80' : y.st === 'open' ? '#747BAD' : '#434C89' }));
  const yours = x.st === 'claimed';
  const sw = q.sw.map((p, j) => ({ rim: p[0], face: p[1], lab: DOM[j] }));
  const claim = () => { if (x.st !== 'ready') return; const n = s.map((y) => ({ st: y.st, done: y.done }));
    n[sel].st = 'claimed'; if (sel + 1 < Q.length) n[sel + 1] = sel + 1 === Q.length - 1 ? { st: 'ready', done: 1 } : { st: 'open', done: 0 };
    this.setState({ s: n }); };
  const play = () => { if (x.st !== 'open') return; const n = s.map((y) => ({ st: y.st, done: y.done }));
    n[sel].done += 1; if (n[sel].done >= q.goals.length) n[sel].st = 'ready'; this.setState({ s: n }); };
  const focus = { kicker: 'Quest ' + q.n + ' of 05', name: q.name, art: q.art, line: line(sel),
    lineInk: x.st === 'ready' ? '#99FF80' : '#747BAD',
    claim: x.st === 'ready', play: x.st === 'open', wait: x.st === 'locked' || x.st === 'claimed',
    waitText: yours ? 'Claimed' : 'Opens after ' + (sel > 0 ? Q[sel - 1].name : ''),
    playNote: x.st === 'open' ? 'Plays ' + q.name + ' at Intensity ' + (2 + x.done) : '',
    opens: q.opens, opensArt: q.opensArt, opensKicker: yours ? 'Opened' : 'Claim also opens',
    reward: q.reward, rewardBlurb: q.rewardBlurb, rewardKicker: yours ? 'Your colour set' : 'Reward',
    rewardState: yours ? 'Yours. Wear it from the Profile.' : x.st === 'ready' ? 'Claim to unlock it' : 'Unlocked by this quest',
    rewardInk: yours ? '#E6E9FF' : x.st === 'ready' ? '#99FF80' : '#747BAD',
    swOp: yours || x.st === 'ready' ? 1 : 0.55, claimAct: claim, playAct: play };
  return { quests, steps, meter, sw, focus, progress: claimed + ' of ' + Q.length + ' claimed',
    segD0: seg(LD, 0), segD1: seg(LD, 1), segD2: seg(LD, 2), segD3: seg(LD, 3),
    segE0: seg(LE, 0), segE1: seg(LE, 1), segE2: seg(LE, 2), segE3: seg(LE, 3) };
}
}
''' % {'Q': json.dumps(Q), 'INIT': json.dumps(INIT), 'DOM': json.dumps(DOM), 'PD': json.dumps(PD), 'PE': json.dumps(PE),
       'LD': json.dumps([l for _, l in SEG_D]), 'LE': json.dumps([l for _, l in SEG_E])}

DASH = '<path d="%s" fill="none" stroke="#434C89" stroke-width="1.5" stroke-dasharray="3 9"></path>'
LIT = '<path class="lit" d="%s" style="stroke-dasharray:{{seg%s%d.len}};stroke-dashoffset:{{seg%s%d.off}}"></path>'
METER = ('<div style="display:flex;gap:6px;width:%dpx"><sc-for list="{{meter}}" as="m" hint-placeholder-count="5">'
         '<span style="flex:1;height:3px;background:{{m.c}}"></span></sc-for></div>')


def ladder(gap=20, size=19):
    return ('<div style="position:relative;display:flex;flex-direction:column;gap:%dpx">'
            '<div style="position:absolute;left:8px;top:14px;bottom:14px;width:1px;background:#434C89"></div>'
            '<sc-for list="{{steps}}" as="g" hint-placeholder-count="3"><div style="position:relative;display:flex;gap:22px;align-items:flex-start">'
            '<span class="dia" style="width:16px;height:16px;margin:6px 1px 0;background:{{g.fill}};border:2px solid {{g.ring}};box-shadow:0 0 0 5px #00041F"></span>'
            '<span style="display:flex;flex-direction:column;gap:6px;flex:1"><span style="font-size:%dpx;line-height:1.35;color:{{g.ink}}">{{g.text}}</span>'
            '<span style="display:flex;justify-content:space-between"><span class="lab">{{g.tier}}</span><span class="lab" style="color:#E6E9FF">{{g.note}}</span></span></span>'
            '</div></sc-for></div>') % (gap, size)


def checkpoints():
    return ('<div style="position:relative;display:flex;gap:28px">'
            '<div style="position:absolute;left:8px;right:60px;top:9px;height:1px;background:#434C89"></div>'
            '<sc-for list="{{steps}}" as="g" hint-placeholder-count="3"><div style="position:relative;flex:1;display:flex;flex-direction:column;gap:12px">'
            '<span class="dia" style="width:16px;height:16px;margin-left:1px;background:{{g.fill}};border:2px solid {{g.ring}};box-shadow:0 0 0 6px #00041F"></span>'
            '<span class="lab">{{g.tier}}</span><span style="font-size:18px;line-height:1.4;color:{{g.ink}}">{{g.text}}</span>'
            '<span class="lab" style="color:#E6E9FF">{{g.note}}</span></div></sc-for></div>')


def prisms(size, cols_style, gap):
    """The set's six domains as prisms: the body is the face colour, the edge the rim colour."""
    return ('<div style="%s;opacity:{{focus.swOp}}"><sc-for list="{{sw}}" as="p" hint-placeholder-count="6">'
            '<div style="display:flex;flex-direction:column;align-items:center;gap:%dpx">'
            '<span style="height:%dpx;display:flex;align-items:center"><span class="dia" style="width:%dpx;height:%dpx;background:{{p.face}};border:3px solid {{p.rim}}"></span></span>'
            '<span class="lab" style="font-size:12px">{{p.lab}}</span></div></sc-for></div>') % (cols_style, gap, int(size * 1.45), size, size)


def opens_row():
    return ('<div style="display:flex;gap:14px;align-items:center">'
            '<img src="{{focus.opensArt}}" alt="" style="width:96px;height:54px;object-fit:cover;-webkit-mask-image:linear-gradient(90deg,#000 70%,transparent);mask-image:linear-gradient(90deg,#000 70%,transparent)">'
            '<span style="display:flex;flex-direction:column;gap:4px"><span class="lab">{{focus.opensKicker}}</span><span style="font-size:18px">{{focus.opens}}</span></span></div>')


def reward_block(prism_html, name_size=30):
    return ('<span class="lab">{{focus.rewardKicker}}</span>'
            '<span style="font-size:%dpx;line-height:1.1;margin-top:10px">{{focus.reward}}</span>'
            '<span class="cap" style="font-size:14px;color:#747BAD;margin-top:6px">Colour set</span>'
            '<div style="margin-top:22px">%s</div>'
            '<span style="font-size:16px;line-height:1.5;color:#C9CDEB;margin-top:20px">{{focus.rewardBlurb}}</span>'
            '<span style="font-size:15px;margin-top:10px;color:{{focus.rewardInk}}">{{focus.rewardState}}</span>') % (name_size, prism_html)


# ---------- D: the route on top, the quest and its reward below ----------
def board_d():
    under = ''.join(DASH % d for d, _ in SEG_D)
    lit = ''.join(LIT % (d, 'D', k, 'D', k) for k, (d, _) in enumerate(SEG_D))
    body = (BACKDROP + '<svg aria-hidden="true" width="1920" height="1080" style="position:absolute;left:0;top:0">'
            + (DASH % 'M0 480 Q110 480 230 470') + (DASH % 'M1670 470 Q1790 460 1920 460') + under + lit + '</svg>'
            + HUB + TOPROW.replace('<span style="font-size:22px">Quests</span>',
                                   '<span style="display:flex;align-items:center;gap:28px"><span style="font-size:22px">Quests</span>'
                                   '<span class="num" style="font-size:16px;color:#E6E9FF">{{progress}}</span>' + (METER % 260) + '</span>') +
            '<nav aria-label="Quests"><sc-for list="{{quests}}" as="r" hint-placeholder-count="5">'
            '<img src="{{r.art}}" alt="" style="position:absolute;left:{{r.cl}};top:{{r.ct}};width:{{r.cw}};height:{{r.ch}};object-fit:cover;opacity:{{r.cop}};filter:{{r.cfilt}};'
            '-webkit-mask-image:radial-gradient(ellipse 50% 50% at 50% 50%,#000 55%,transparent 100%);mask-image:radial-gradient(ellipse 50% 50% at 50% 50%,#000 55%,transparent 100%);transition:all .35s ease">'
            '<button class="pick" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}" style="position:absolute;left:{{r.cx}};top:{{r.cy}};width:280px;display:flex;flex-direction:column;align-items:center;gap:8px">'
            '<span style="height:32px;display:flex;align-items:center"><span class="dia" style="width:{{r.size}};height:{{r.size}};background:{{r.fill}};border:2px solid {{r.ring}};box-shadow:0 0 0 6px #00041F"></span></span>'
            '<span style="font-size:20px;color:{{r.ink}};padding-bottom:6px;border-bottom:2px solid {{r.under}}">{{r.name}}</span>'
            '<span class="num" style="font-size:14px;color:{{r.lineInk}}">{{r.line}}</span></button></sc-for></nav>'
            '<div class="abs" aria-hidden="true" style="left:0;right:0;bottom:0;height:500px;background:linear-gradient(180deg,rgba(0,4,31,0) 0,rgba(0,4,31,.93) 22%,rgba(0,1,10,.97) 100%)"></div>'
            '<section aria-label="The picked quest" class="abs" style="left:64px;top:640px;width:560px;height:400px;display:flex;flex-direction:column">'
            '<span class="lab">{{focus.kicker}}</span><span style="font-size:40px;line-height:1.1;margin-top:8px">{{focus.name}}</span>'
            '<span class="num" style="font-size:16px;margin-top:8px;margin-bottom:26px;color:{{focus.lineInk}}">{{focus.line}}</span>'
            + ladder(18, 18) + '</section>'
            '<section aria-label="The reward" class="abs" style="left:700px;top:640px;width:640px;height:400px;display:flex;flex-direction:column">'
            + reward_block(prisms(40, 'display:grid;grid-template-columns:repeat(6,minmax(0,1fr));column-gap:12px', 10)) + '</section>'
            '<section aria-label="Claim" class="abs" style="left:1420px;top:640px;width:436px;height:400px;display:flex;flex-direction:column">'
            + opens_row() + CTA + '</section>')
    return page('Rev %s D - Quests: the route across the top, the picked quest, its colour set reward and Claim below - interactive' % REV, body, LOGIC)


# ---------- E: the route down the left as the list, the quest in the middle, the reward on the right ----------
def board_e():
    under = ''.join(DASH % d for d, _ in SEG_E)
    lit = ''.join(LIT % (d, 'E', k, 'E', k) for k, (d, _) in enumerate(SEG_E))
    body = (BACKDROP + '<svg aria-hidden="true" width="400" height="1080" style="position:absolute;left:0;top:0">' + under + lit + '</svg>'
            + HUB + TOPROW.replace('<span style="font-size:22px">Quests</span>',
                                   '<span style="display:flex;align-items:center;gap:28px"><span style="font-size:22px">Quests</span>'
                                   '<span class="num" style="font-size:16px;color:#E6E9FF">{{progress}}</span>' + (METER % 260) + '</span>') +
            '<nav aria-label="Quests"><sc-for list="{{quests}}" as="r" hint-placeholder-count="5">'
            '<button class="pick" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}" style="position:absolute;left:84px;top:{{r.ey}};width:420px;height:120px;display:flex;align-items:flex-start;gap:26px;background:{{r.ebg}}">'
            '<span style="height:32px;width:40px;display:flex;align-items:center;justify-content:center"><span class="dia" style="width:{{r.size}};height:{{r.size}};background:{{r.fill}};border:2px solid {{r.ring}};box-shadow:0 0 0 6px #00041F"></span></span>'
            '<span style="display:flex;flex-direction:column;gap:6px;padding-top:4px;flex:1"><span style="font-size:20px;color:{{r.ink}};align-self:flex-start;padding-bottom:5px;border-bottom:2px solid {{r.under}}">{{r.name}}</span>'
            '<span class="num" style="font-size:14px;color:{{r.lineInk}}">{{r.line}}</span>'
            '<span class="lab" style="font-size:12px">{{r.reward}}</span></span>'
            '<img src="{{r.art}}" alt="" style="width:150px;height:84px;object-fit:cover;opacity:{{r.eop}};filter:{{r.efilt}};-webkit-mask-image:radial-gradient(ellipse 50% 50% at 50% 50%,#000 50%,transparent 100%);mask-image:radial-gradient(ellipse 50% 50% at 50% 50%,#000 50%,transparent 100%)">'
            '</button></sc-for></nav>'
            '<section aria-label="The picked quest" class="abs" style="left:580px;top:186px;width:760px;height:850px;display:flex;flex-direction:column">'
            '<div style="position:relative;width:760px;height:380px">'
            '<img src="{{focus.art}}" alt="{{focus.name}}" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;-webkit-mask-image:linear-gradient(180deg,#000 45%,transparent 100%);mask-image:linear-gradient(180deg,#000 45%,transparent 100%)">'
            '<span style="position:absolute;left:0;bottom:4px;display:flex;flex-direction:column;gap:8px"><span class="lab" style="color:#E6E9FF">{{focus.kicker}}</span>'
            '<span style="font-size:48px;line-height:1.05">{{focus.name}}</span></span></div>'
            '<span class="num" style="font-size:16px;margin-top:14px;color:{{focus.lineInk}}">{{focus.line}}</span>'
            '<span class="lab" style="margin-top:46px;margin-bottom:22px">The way to claim it</span>'
            + checkpoints() +
            '<div style="margin-top:auto">' + opens_row() + '</div></section>'
            '<section aria-label="The reward" class="abs" style="left:1420px;top:186px;width:436px;height:850px;display:flex;flex-direction:column">'
            + reward_block(prisms(60, 'display:grid;grid-template-columns:repeat(3,minmax(0,1fr));row-gap:22px;column-gap:16px', 12), 36)
            + CTA + '</section>')
    return page('Rev %s E - Quests: the route down the left as the list, the quest and its goals in the middle, its colour set reward and Claim on the right - interactive' % REV, body, LOGIC)


# ---------- Compare: three ways to show a colour set as the reward ----------
def compare():
    s = SETS['neon_noir']
    sw = [(s['domains'][d.lower()]['rim']['swatch'], s['domains'][d.lower()]['face']['swatch'], d) for d in DOM]

    def dia(face, rim, size, bw=3):
        return '<span class="dia" style="width:%dpx;height:%dpx;background:%s;border:%dpx solid %s"></span>' % (size, size, face, bw, rim)

    def labelled(face, rim, d, size):
        return ('<div style="display:flex;flex-direction:column;align-items:center;gap:10px"><span style="height:%dpx;display:flex;align-items:center">%s</span>'
                '<span class="lab" style="font-size:12px">%s</span></div>') % (int(size * 1.45), dia(face, rim, size), d)

    row = '<div style="display:grid;grid-template-columns:repeat(6,minmax(0,1fr));column-gap:12px">' + ''.join(labelled(f, r, d, 40) for r, f, d in sw) + '</div>'
    grid = '<div style="display:grid;grid-template-columns:repeat(3,minmax(0,1fr));row-gap:22px;column-gap:16px">' + ''.join(labelled(f, r, d, 60) for r, f, d in sw) + '</div>'
    bands = '<div style="display:flex;flex-direction:column;gap:8px">' + ''.join(
        '<div style="display:flex;align-items:center;gap:14px"><span class="lab" style="font-size:12px;width:44px">%s</span>'
        '<span style="flex:1;height:22px;background:linear-gradient(90deg,%s 0,%s 70%%,rgba(0,0,0,0) 100%%);box-shadow:inset 0 2px 0 %s"></span></div>' % (d, f, f, r)
        for r, f, d in sw) + '</div>'
    head = ('<span class="lab">Reward</span><span style="font-size:30px;line-height:1.1;margin-top:10px">Neon Noir</span>'
            '<span class="cap" style="font-size:14px;color:#747BAD;margin-top:6px">Colour set</span>')
    tail = ('<span style="font-size:16px;line-height:1.5;color:#C9CDEB;margin-top:20px">Neon tubes over wet asphalt.</span>'
            '<span style="font-size:15px;margin-top:10px;color:#99FF80">Claim to unlock it</span>')
    cols = [('Six prisms in a row (D)', 'Each domain as the prism it paints: body in the face colour, edge in the rim colour. Fits a wide, short space.', row),
            ('Six prisms, three by two (E)', 'The same prisms larger, for a tall column. Easiest to read on a phone.', grid),
            ('Six bands (not used)', 'Each domain as a stripe: the face colour with the rim along its top edge. Reads as a palette, less as the game.', bands)]
    body = ('<div class="abs" style="left:64px;top:52px;display:flex;flex-direction:column;gap:10px"><span class="lab">Rev %s compare</span>'
            '<span style="font-size:32px">The reward: a colour set, drawn three ways</span>'
            '<span style="font-size:16px;color:#747BAD">Joust\'s reward, Neon Noir, ready to claim. Swatches are the set\'s own rim and face colours.</span></div>'
            '<div class="abs" style="left:64px;right:64px;top:210px;display:grid;grid-template-columns:repeat(3,minmax(0,1fr));column-gap:96px">') % REV
    for h, sub, art in cols:
        body += ('<div style="display:flex;flex-direction:column"><span style="font-size:22px">%s</span>'
                 '<span style="font-size:14px;line-height:1.5;color:#747BAD;margin:10px 0 40px;height:42px">%s</span>'
                 '<div style="display:flex;flex-direction:column">%s<div style="margin-top:22px">%s</div>%s</div></div>') % (h, sub, head, art, tail)
    body += '</div>'
    return page('Rev %s Compare - the colour set reward drawn three ways' % REV, body,
                '\nclass Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n')


def notes():
    def block(head, lines):
        return ('<div style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">%s</span>' % head
                + ''.join('<span style="font-size:17px;line-height:1.55;color:#C9CDEB">%s</span>' % l for l in lines) + '</div>')
    pairs = ''.join('<span style="font-size:17px;line-height:1.55;color:#C9CDEB">%s: %s</span>' % (q['name'], q['reward']) for q in Q)
    cols = [
        block('Asked (Shombith, 2026-11-14, on Rev 101)', [
            '&#8220;Maybe mix up B and C but also there should be a section showing the reward. We are thinking of color sets to be rewards for each quests. And dont name it Episode 1, its just quest.&#8221;']),
        block('The two mixes', [
            '<b>D Route on top (recommended).</b> C’s course across the top with each quest’s art; under it B’s parts side by side: the quest and its goals, the reward, and Claim. Everything on one screen without scrolling, and the reward sits in the middle where the eye lands.',
            '<b>E Route as the list.</b> B’s list turned into C’s course, running down the left; the quest large in the middle with its goals as checkpoints; the reward its own tall column beside Claim.']),
        '<div style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">The rewards (a proposal)</span>' + pairs +
        '<span style="font-size:17px;line-height:1.55;color:#C9CDEB">The sets and their swatches are the game’s own (colour_sets.json). Nocturne, the default, and the accessibility sets are not used: they stay free for everyone. Claimed sets would be worn from the Profile’s colour sets.</span></div>',
        block('What changed from Rev 101', [
            'No episode: the header is Quests and “2 of 5 claimed” with its meter. The Episode 2 line is gone.',
            'Claim’s step now reads “Unlocks &lt;set&gt;”; the next quest it opens is a small line by the button.',
            'Same as before: real quests and goals, Claim works, Play quest counts a goal on the board, and the one motion is the course drawing on to the next quest.']),
    ]
    body = ('<div class="abs" style="left:64px;top:56px;display:flex;flex-direction:column;gap:10px"><span class="lab">Rev %s notes</span>'
            '<span style="font-size:32px">Quests: B and C mixed, with the reward</span></div>'
            '<div class="abs" style="left:64px;right:64px;top:170px;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));column-gap:56px">' % REV
            + ''.join(cols) + '</div>')
    return page('Rev %s notes - Quests, B and C mixed, with the reward' % REV, body,
                '\nclass Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n', 1920, 900)


for name, fn in [('D', board_d), ('E', board_e), ('Compare', compare), ('Notes', notes)]:
    with open(os.path.join(out, 'R%s%s.dc.html' % (REV, name)), 'w') as f:
        f.write(fn())
print('written', out)
