#!/usr/bin/env python3
"""Rev 101 - Quests as a screen of its own (2026-11-13). Writes R101*.dc.html into <root>/project.

Usage: python3 r101_quests.py <root>
The kit (helmet, backdrop, sea lines, hub band) is lifted from Rev 90's Quests board so the
look matches the boards Shombith already picked from.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_BOARD = os.path.join(HERE, '..', 'board')
root = sys.argv[1]
out = os.path.join(root, 'project')

r90 = open(os.path.join(REPO_BOARD, 'R90Quests.dc.html')).read()
STYLE = r90[r90.find('<style>') + 7:r90.find('</style>')]
i = r90.find('<img src="/_blob/391619dde523174b4a9ead64e169b67a"')
BACKDROP = r90[i:r90.find('</svg>', i) + 6]
i = r90.find('<nav aria-label="Menu screens"')
HUB = r90[i:r90.find('</nav>', i) + 6]

SOFT_LINE = ('linear-gradient(90deg,rgba(230,233,255,0) 0,rgba(230,233,255,.8) 20%,rgba(230,233,255,.8) 80%,'
             'rgba(230,233,255,0) 100%)')
STYLE += (
    ".soft{clip-path:none;box-shadow:none;color:#E6E9FF;background:" + SOFT_LINE + " top/100% 1px no-repeat," + SOFT_LINE +
    " bottom/100% 1px no-repeat,linear-gradient(90deg,rgba(0,4,31,0) 0,rgba(0,4,31,.86) 20%,rgba(0,4,31,.86) 80%,rgba(0,4,31,0) 100%)}\n"
    ".softcta{clip-path:none;color:#00041F;background:linear-gradient(90deg,rgba(153,255,128,0) 0,#99FF80 20%,#99FF80 80%,rgba(153,255,128,0) 100%)}\n"
    ".softwait{clip-path:none;color:#747BAD;background:" + SOFT_LINE.replace('230,233,255,.8', '67,76,137,.9') +
    " top/100% 1px no-repeat," + SOFT_LINE.replace('230,233,255,.8', '67,76,137,.9') + " bottom/100% 1px no-repeat}\n"
    ".dia{display:inline-block;box-sizing:border-box;transform:rotate(45deg);flex:none}\n"
    ".lit{fill:none;stroke:#E6E9FF;stroke-opacity:.75;stroke-width:2;transition:stroke-dashoffset 1.1s ease}\n"
)

ART = {
    'scurry': '/_blob/e991a3576a32930173b452e9806c08d3',
    'skim': '/_blob/870fa2d022d70e4eadd5963ce81b5328',
    'joust': '/_blob/f73f095638ef9ad060a2a8bb097ea2af',
    'mael': '/_blob/360243baa0fb047a2c955d06f7f663a0',
    'hangar': '/_blob/7d5ddbe91b12c281ac5d6a4b4ad38239',
}
SEA = '/_blob/7559d7b030c6e949f7cde2d25ce5d9e7'

# The episode, from Assets/_SO_Assets/GameModeQuest (GameModeQuestList order; goals are the assets'
# Intensity3/4GoalDescription and PlaysToUnlock). "Hex Race" is shown as Skim Race, the mode's name.
Q = [
    {'n': '01', 'name': 'Scurry', 'art': ART['scurry'],
     'goals': ['Rank first in an Intensity 2 game', 'Rank first in an Intensity 3 game'],
     'tiers': ['Opens Intensity 3', 'Opens Intensity 4'],
     'opens': 'Skim Race', 'opensArt': ART['skim'], 'opensBlurb': 'A new game in the Arcade'},
    {'n': '02', 'name': 'Skim Race', 'art': ART['skim'],
     'goals': ['Rank first in an Intensity 2 race', 'Rank first in an Intensity 3 race'],
     'tiers': ['Opens Intensity 3', 'Opens Intensity 4'],
     'opens': 'Joust', 'opensArt': ART['joust'], 'opensBlurb': 'A new game in the Arcade'},
    {'n': '03', 'name': 'Joust', 'art': ART['joust'],
     'goals': ['Win a Joust match at Intensity 2', 'Win a Joust match at Intensity 3'],
     'tiers': ['Opens Intensity 3', 'Opens Intensity 4'],
     'opens': 'Maelstrom', 'opensArt': ART['mael'], 'opensBlurb': 'A new game in the Arcade'},
    {'n': '04', 'name': 'Maelstrom', 'art': ART['mael'],
     'goals': ['Play 3 games at Intensity 2', 'Play 3 games at Intensity 3'],
     'tiers': ['Opens Intensity 3', 'Opens Intensity 4'],
     'opens': 'Vessel Hangar', 'opensArt': ART['hangar'], 'opensBlurb': 'The episode’s last quest'},
    {'n': '05', 'name': 'Vessel Hangar', 'art': ART['hangar'],
     'goals': ['Claim the four game quests'],
     'tiers': ['Opens the Hangar'],
     'opens': 'The Hangar', 'opensArt': ART['hangar'], 'opensBlurb': 'Upgrade your vessels in the Hub'},
]
INIT = [{'st': 'claimed', 'done': 2}, {'st': 'claimed', 'done': 2}, {'st': 'ready', 'done': 2},
        {'st': 'locked', 'done': 0}, {'st': 'locked', 'done': 0}]

# A: stars on the chart. C: stations on the route.
PA = [(230, 870), (520, 730), (820, 800), (1060, 570), (1290, 390)]
PC = [(230, 590), (590, 520), (950, 590), (1310, 520), (1670, 590)]


def qpath(a, b, bend):
    (x0, y0), (x1, y1) = a, b
    mx, my = (x0 + x1) / 2, (y0 + y1) / 2
    dx, dy = x1 - x0, y1 - y0
    l = math.hypot(dx, dy)
    cx, cy = mx - dy / l * bend, my + dx / l * bend
    n, length, px, py = 64, 0.0, x0, y0
    for k in range(1, n + 1):
        t = k / n
        x = (1 - t) ** 2 * x0 + 2 * (1 - t) * t * cx + t * t * x1
        y = (1 - t) ** 2 * y0 + 2 * (1 - t) * t * cy + t * t * y1
        length += math.hypot(x - px, y - py)
        px, py = x, y
    return 'M%.0f %.0f Q%.0f %.0f %.0f %.0f' % (x0, y0, cx, cy, x1, y1), math.ceil(length) + 2


SEG_A = [qpath(PA[k], PA[k + 1], -90) for k in range(4)]
SEG_C = [qpath(PC[k], PC[k + 1], 40 if k % 2 == 0 else -40) for k in range(4)]

LOGIC = '''
const Q = %(Q)s;
const INIT = %(INIT)s;
const PA = %(PA)s;
const PC = %(PC)s;
const LA = %(LA)s;
const LC = %(LC)s;
class Component extends DCLogic {
constructor(p) { super(p); this.state = { s: INIT.map((x) => ({ st: x.st, done: x.done })), sel: 2 }; }
renderVals() {
  const st = this.state, s = st.s, sel = st.sel;
  const line = (i) => { const x = s[i]; return x.st === 'claimed' ? 'Claimed' : x.st === 'ready' ? 'Ready to claim'
    : x.st === 'open' ? x.done + ' of ' + Q[i].goals.length + ' goals' : 'After ' + Q[i - 1].name; };
  const ring = (i) => s[i].st === 'claimed' ? '#E6E9FF' : s[i].st === 'ready' ? '#99FF80' : s[i].st === 'open' ? '#E6E9FF' : '#434C89';
  const quests = Q.map((q, i) => { const on = i === sel, lock = s[i].st === 'locked';
    const w = on ? 340 : 250, h = on ? 200 : 146;
    return { n: q.n, name: q.name, art: q.art, line: line(i),
      ink: lock ? '#747BAD' : '#E6E9FF', lineInk: s[i].st === 'ready' ? '#99FF80' : '#747BAD',
      fill: s[i].st === 'claimed' ? '#E6E9FF' : 'rgba(0,0,0,0)', ring: ring(i),
      size: on ? '22px' : '16px', under: on ? '#00D4FF' : 'rgba(0,0,0,0)', pressed: on ? 'true' : 'false',
      pick: () => this.setState({ sel: i }),
      ax: (PA[i][0] - 130) + 'px', ay: (PA[i][1] - 16) + 'px',
      prog: Math.min(s[i].done, q.goals.length) + ' / ' + q.goals.length,
      rowBg: on ? 'linear-gradient(90deg,rgba(0,4,31,.9),rgba(0,4,31,0))' : 'rgba(0,0,0,0)',
      rowEdge: on ? 'inset 2px 0 0 #00D4FF' : 'none', rowArt: lock ? 0.25 : on ? 0.9 : 0.5,
      cw: w + 'px', ch: h + 'px', cl: (PC[i][0] - w / 2) + 'px', ct: (PC[i][1] - h - 40) + 'px',
      cop: lock ? 0.28 : on ? 1 : 0.62, cfilt: lock ? 'grayscale(1) brightness(.7)' : 'none',
      cx: (PC[i][0] - 150) + 'px', cy: (PC[i][1] - 16) + 'px' }; });
  const seg = (L, k) => { const lit = s[k + 1].st !== 'locked'; return { len: L[k], off: lit ? 0 : L[k] }; };
  const q = Q[sel], x = s[sel];
  const goals = q.goals.map((g, j) => { const ok = x.st === 'claimed' || x.st === 'ready' || j < x.done;
    const cur = x.st === 'open' && j === x.done;
    return { text: g, tier: q.tiers[j], fill: ok ? '#E6E9FF' : 'rgba(0,0,0,0)', ring: ok || cur ? '#E6E9FF' : '#434C89',
      ink: ok || cur ? '#E6E9FF' : '#747BAD', note: ok ? 'Done' : cur ? 'Next' : '', last: 'false' }; });
  const claimRow = { text: 'Claim', tier: 'Opens ' + q.opens,
    fill: x.st === 'claimed' ? '#E6E9FF' : 'rgba(0,0,0,0)', ring: x.st === 'ready' ? '#99FF80' : x.st === 'claimed' ? '#E6E9FF' : '#434C89',
    ink: x.st === 'ready' || x.st === 'claimed' ? '#E6E9FF' : '#747BAD', note: x.st === 'claimed' ? 'Claimed' : x.st === 'ready' ? 'Ready' : '' };
  const steps = goals.concat([claimRow]);
  const claimed = s.filter((y) => y.st === 'claimed').length;
  const meter = s.map((y) => ({ c: y.st === 'claimed' ? '#E6E9FF' : y.st === 'ready' ? '#99FF80' : y.st === 'open' ? '#747BAD' : '#434C89' }));
  const claim = () => { if (x.st !== 'ready') return; const n = s.map((y) => ({ st: y.st, done: y.done }));
    n[sel].st = 'claimed'; if (sel + 1 < Q.length) n[sel + 1] = sel + 1 === Q.length - 1 ? { st: 'ready', done: 1 } : { st: 'open', done: 0 };
    this.setState({ s: n }); };
  const play = () => { if (x.st !== 'open') return; const n = s.map((y) => ({ st: y.st, done: y.done }));
    n[sel].done += 1; if (n[sel].done >= q.goals.length) n[sel].st = 'ready'; this.setState({ s: n }); };
  const focus = { kicker: 'Quest ' + q.n + ' of 05', name: q.name, art: q.art, line: line(sel),
    lineInk: x.st === 'ready' ? '#99FF80' : '#747BAD',
    claim: x.st === 'ready', play: x.st === 'open', wait: x.st === 'locked' || x.st === 'claimed',
    waitText: x.st === 'claimed' ? 'Claimed' : 'Opens after ' + (sel > 0 ? Q[sel - 1].name : ''),
    playNote: x.st === 'open' ? 'Plays ' + q.name + ' at Intensity ' + (2 + x.done) : '',
    opens: q.opens, opensArt: q.opensArt, opensBlurb: q.opensBlurb,
    opensOp: x.st === 'claimed' ? 1 : 0.45, opensKicker: x.st === 'claimed' ? 'Opened' : 'Claim opens', claimAct: claim, playAct: play };
  return { quests, goals, steps, meter, focus, progress: claimed + ' of ' + Q.length + ' claimed',
    segA0: seg(LA, 0), segA1: seg(LA, 1), segA2: seg(LA, 2), segA3: seg(LA, 3),
    segC0: seg(LC, 0), segC1: seg(LC, 1), segC2: seg(LC, 2), segC3: seg(LC, 3) };
}
}
''' % {'Q': json.dumps(Q), 'INIT': json.dumps(INIT), 'PA': json.dumps(PA), 'PC': json.dumps(PC),
       'LA': json.dumps([l for _, l in SEG_A]), 'LC': json.dumps([l for _, l in SEG_C])}


def page(title, body, logic, w=1920, h=1080):
    return ('<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<title>' + title + '</title>\n'
            '<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>\n<helmet>\n'
            '<link rel="preconnect" href="https://fonts.googleapis.com">\n'
            '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">\n'
            '<link href="https://fonts.googleapis.com/css2?family=Aldrich&amp;family=Chakra+Petch:wght@500;600;700&amp;display=swap" rel="stylesheet">\n'
            '<style>' + STYLE + '</style>\n</helmet>\n'
            '<div style="width: %dpx; height: %dpx; position: relative; overflow: hidden; background: #00041F; color: #E6E9FF; '
            "font-family: 'Aldrich', 'Chakra Petch', sans-serif\">\n" % (w, h) + body + '\n</div>\n</x-dc>\n'
            '<script type="text/x-dc" data-dc-script data-props=\'{"$preview":{"width":%d,"height":%d}}\'>' % (w, h)
            + logic + '</script>\n</body>\n</html>\n')


TOPROW = ('<div class="abs" style="left:64px;right:64px;top:104px;height:48px;display:flex;align-items:center;'
          'justify-content:space-between;z-index:4"><span style="font-size:22px">Quests</span>'
          '<a class="btn sm soft" href="#" style="width:180px"><span class="kc">B</span>Back</a></div>')

CTA = ('<div style="margin-top:auto;display:flex;flex-direction:column;gap:12px">'
       '<sc-if value="{{focus.play}}" hint-placeholder-val="{{ false }}"><span style="font-size:15px;color:#747BAD;text-align:center">{{focus.playNote}}</span></sc-if>'
       '<sc-if value="{{focus.claim}}" hint-placeholder-val="{{ true }}"><button class="btn softcta" type="button" onClick="{{focus.claimAct}}" style="width:100%"><span class="kc">A</span>Claim</button></sc-if>'
       '<sc-if value="{{focus.play}}" hint-placeholder-val="{{ false }}"><button class="btn softcta" type="button" onClick="{{focus.playAct}}" style="width:100%"><span class="kc">A</span>Play quest</button></sc-if>'
       '<sc-if value="{{focus.wait}}" hint-placeholder-val="{{ false }}"><span class="btn softwait" style="width:100%">{{focus.waitText}}</span></sc-if>'
       '</div>')

EP2 = '<span class="lab" style="position:absolute;left:64px;bottom:40px;color:#434C89">Episode 2 &#160;&#183;&#160; Coming soon</span>'


def steps_column(top_gap=14):
    """The open quest's goals and claim as a vertical ladder (used by A and B)."""
    return ('<div style="position:relative;display:flex;flex-direction:column;gap:%dpx">'
            '<div style="position:absolute;left:8px;top:14px;bottom:14px;width:1px;background:#434C89"></div>'
            '<sc-for list="{{steps}}" as="g" hint-placeholder-count="3"><div style="position:relative;display:flex;gap:22px;align-items:flex-start">'
            '<span class="dia" style="width:16px;height:16px;margin:6px 1px 0;background:{{g.fill}};border:2px solid {{g.ring}};box-shadow:0 0 0 5px #00041F"></span>'
            '<span style="display:flex;flex-direction:column;gap:6px;flex:1"><span style="font-size:19px;line-height:1.35;color:{{g.ink}}">{{g.text}}</span>'
            '<span style="display:flex;justify-content:space-between"><span class="lab">{{g.tier}}</span><span class="lab" style="color:#E6E9FF">{{g.note}}</span></span></span>'
            '</div></sc-for></div>') % top_gap


# ---------- A: the star chart ----------
def board_a():
    segs_under = ''.join('<path d="%s" fill="none" stroke="#434C89" stroke-width="1.5" stroke-dasharray="3 9"></path>' % d for d, _ in SEG_A)
    segs_lit = ''.join('<path class="lit" d="%s" style="stroke-dasharray:{{segA%d.len}};stroke-dashoffset:{{segA%d.off}}"></path>' % (d, k, k)
                       for k, (d, _) in enumerate(SEG_A))
    body = (BACKDROP +
            '<img src="' + SEA + '" alt="The HyperSea" style="position:absolute;left:0;top:84px;width:1440px;height:996px;object-fit:cover;object-position:50% 55%;opacity:.42;filter:saturate(1.1) brightness(.85);'
            '-webkit-mask-image:radial-gradient(ellipse 72% 70% at 45% 55%,#000 40%,transparent 100%);mask-image:radial-gradient(ellipse 72% 70% at 45% 55%,#000 40%,transparent 100%)">'
            '<svg aria-hidden="true" width="1440" height="1080" style="position:absolute;left:0;top:0">' + segs_under + segs_lit + '</svg>'
            + HUB + TOPROW +
            '<header class="abs" style="left:64px;top:186px;display:flex;flex-direction:column;gap:10px">'
            '<span class="lab">Episode 1</span><span style="font-size:44px;line-height:1.1">The Hypersea</span>'
            '<span class="num" style="font-size:18px;color:#E6E9FF">{{progress}}</span></header>'
            '<nav aria-label="Episode 1\'s quests"><sc-for list="{{quests}}" as="r" hint-placeholder-count="5">'
            '<button class="pick" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}" style="position:absolute;left:{{r.ax}};top:{{r.ay}};width:260px;display:flex;flex-direction:column;align-items:center;gap:10px">'
            '<span style="height:32px;display:flex;align-items:center"><span class="dia" style="width:{{r.size}};height:{{r.size}};background:{{r.fill}};border:2px solid {{r.ring}}"></span></span>'
            '<span style="font-size:20px;color:{{r.ink}};padding-bottom:6px;border-bottom:2px solid {{r.under}}">{{r.name}}</span>'
            '<span class="num" style="font-size:14px;color:{{r.lineInk}}">{{r.line}}</span></button></sc-for></nav>'
            + EP2 +
            '<div class="abs" aria-hidden="true" style="right:0;top:84px;width:640px;height:996px;background:linear-gradient(90deg,rgba(0,4,31,0) 0,rgba(0,4,31,.9) 34%,rgba(0,4,31,.94) 100%)"></div>'
            '<section aria-label="The picked quest" class="abs" style="left:1440px;top:186px;width:416px;height:850px;display:flex;flex-direction:column">'
            '<span class="lab">{{focus.kicker}}</span>'
            '<img src="{{focus.art}}" alt="{{focus.name}}" style="margin-top:16px;width:416px;height:220px;object-fit:cover;-webkit-mask-image:linear-gradient(180deg,#000 60%,transparent);mask-image:linear-gradient(180deg,#000 60%,transparent)">'
            '<span style="font-size:40px;line-height:1.1;margin-top:6px">{{focus.name}}</span>'
            '<span class="num" style="font-size:16px;margin-top:10px;color:{{focus.lineInk}}">{{focus.line}}</span>'
            '<span class="lab" style="margin-top:34px;margin-bottom:18px">Goals</span>'
            + steps_column(22) +
            '<div style="display:flex;gap:16px;align-items:center;margin-top:30px;opacity:{{focus.opensOp}}">'
            '<img src="{{focus.opensArt}}" alt="" style="width:112px;height:63px;object-fit:cover;-webkit-mask-image:linear-gradient(90deg,#000 70%,transparent);mask-image:linear-gradient(90deg,#000 70%,transparent)">'
            '<span style="display:flex;flex-direction:column;gap:4px"><span class="lab">{{focus.opensKicker}}</span><span style="font-size:20px">{{focus.opens}}</span>'
            '<span style="font-size:14px;color:#747BAD">{{focus.opensBlurb}}</span></span></div>'
            + CTA + '</section>')
    return page('Rev 101 A - Quests as a star chart: five quests as stars on the HyperSea, joined by the current that opens them - interactive', body, LOGIC)


# ---------- B: the quest log ----------
def board_b():
    body = (BACKDROP + HUB + TOPROW +
            '<section aria-label="Episode 1" class="abs" style="left:64px;top:186px;width:470px;display:flex;flex-direction:column">'
            '<span class="lab">Episode 1</span><span style="font-size:36px;line-height:1.1;margin-top:10px">The Hypersea</span>'
            '<div style="display:flex;gap:6px;margin-top:20px"><sc-for list="{{meter}}" as="m" hint-placeholder-count="5"><span style="flex:1;height:3px;background:{{m.c}}"></span></sc-for></div>'
            '<span class="num" style="font-size:16px;color:#E6E9FF;margin-top:12px">{{progress}}</span>'
            '<nav aria-label="Episode 1\'s quests" style="display:flex;flex-direction:column;margin-top:30px;border-top:1px solid rgba(67,76,137,.6)">'
            '<sc-for list="{{quests}}" as="r" hint-placeholder-count="5"><button class="pick" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}" '
            'style="position:relative;height:104px;display:flex;align-items:center;gap:20px;padding:0 20px;border-bottom:1px solid rgba(67,76,137,.6);background:{{r.rowBg}};box-shadow:{{r.rowEdge}}">'
            '<span class="dia" style="width:14px;height:14px;background:{{r.fill}};border:2px solid {{r.ring}}"></span>'
            '<span style="display:flex;flex-direction:column;gap:8px;flex:1"><span class="lab">Quest {{r.n}}</span><span style="font-size:22px;color:{{r.ink}}">{{r.name}}</span></span>'
            '<span class="num" style="font-size:14px;color:{{r.lineInk}};text-align:right;width:130px">{{r.line}}</span></button></sc-for></nav></section>'
            + EP2 +
            '<section aria-label="The picked quest" class="abs" style="left:610px;top:186px;width:740px;height:850px;display:flex;flex-direction:column">'
            '<div style="position:relative;width:740px;height:360px">'
            '<img src="{{focus.art}}" alt="{{focus.name}}" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover;-webkit-mask-image:linear-gradient(180deg,#000 45%,transparent 100%);mask-image:linear-gradient(180deg,#000 45%,transparent 100%)">'
            '<span style="position:absolute;left:0;bottom:4px;display:flex;flex-direction:column;gap:8px"><span class="lab" style="color:#E6E9FF">{{focus.kicker}}</span>'
            '<span style="font-size:48px;line-height:1.05">{{focus.name}}</span></span></div>'
            '<span class="num" style="font-size:16px;margin-top:14px;color:{{focus.lineInk}}">{{focus.line}}</span>'
            '<span class="lab" style="margin-top:38px;margin-bottom:20px">The way to claim it</span>'
            + steps_column(28) + '</section>'
            '<section aria-label="What the claim opens" class="abs" style="left:1420px;top:186px;width:436px;height:850px;display:flex;flex-direction:column">'
            '<span class="lab">{{focus.opensKicker}}</span>'
            '<img src="{{focus.opensArt}}" alt="{{focus.opens}}" style="margin-top:16px;width:436px;height:245px;object-fit:cover;opacity:{{focus.opensOp}};-webkit-mask-image:linear-gradient(180deg,#000 60%,transparent);mask-image:linear-gradient(180deg,#000 60%,transparent)">'
            '<span style="font-size:30px;margin-top:8px">{{focus.opens}}</span>'
            '<span style="font-size:16px;line-height:1.5;color:#747BAD;margin-top:8px">{{focus.opensBlurb}}</span>'
            + CTA + '</section>')
    return page('Rev 101 B - Quests as a quest log: a calm list, the open quest\'s goals as a ladder, what the claim opens - interactive', body, LOGIC)


# ---------- C: the route ----------
def board_c():
    segs_under = ''.join('<path d="%s" fill="none" stroke="#434C89" stroke-width="1.5" stroke-dasharray="3 9"></path>' % d for d, _ in SEG_C)
    segs_lit = ''.join('<path class="lit" d="%s" style="stroke-dasharray:{{segC%d.len}};stroke-dashoffset:{{segC%d.off}}"></path>' % (d, k, k)
                       for k, (d, _) in enumerate(SEG_C))
    body = (BACKDROP + '<svg aria-hidden="true" width="1920" height="1080" style="position:absolute;left:0;top:0">'
            '<path d="M0 600 Q110 600 230 590" fill="none" stroke="#434C89" stroke-width="1.5" stroke-dasharray="3 9"></path>'
            '<path d="M1670 590 Q1790 580 1920 580" fill="none" stroke="#434C89" stroke-width="1.5" stroke-dasharray="3 9"></path>'
            + segs_under + segs_lit + '</svg>' + HUB + TOPROW +
            '<header class="abs" style="left:0;right:0;top:170px;display:flex;flex-direction:column;align-items:center;gap:8px">'
            '<span class="lab">Episode 1</span><span style="font-size:40px;line-height:1.1">The Hypersea</span>'
            '<span class="num" style="font-size:17px">{{progress}}</span></header>'
            '<nav aria-label="Episode 1\'s quests"><sc-for list="{{quests}}" as="r" hint-placeholder-count="5">'
            '<img src="{{r.art}}" alt="" style="position:absolute;left:{{r.cl}};top:{{r.ct}};width:{{r.cw}};height:{{r.ch}};object-fit:cover;opacity:{{r.cop}};filter:{{r.cfilt}};'
            '-webkit-mask-image:radial-gradient(ellipse 50% 50% at 50% 50%,#000 55%,transparent 100%);mask-image:radial-gradient(ellipse 50% 50% at 50% 50%,#000 55%,transparent 100%);transition:all .35s ease">'
            '<button class="pick" type="button" aria-pressed="{{r.pressed}}" onClick="{{r.pick}}" style="position:absolute;left:{{r.cx}};top:{{r.cy}};width:300px;display:flex;flex-direction:column;align-items:center;gap:10px">'
            '<span style="height:32px;display:flex;align-items:center"><span class="dia" style="width:{{r.size}};height:{{r.size}};background:{{r.fill}};border:2px solid {{r.ring}};box-shadow:0 0 0 6px #00041F"></span></span>'
            '<span style="font-size:20px;color:{{r.ink}};padding-bottom:6px;border-bottom:2px solid {{r.under}}">{{r.name}}</span>'
            '<span class="num" style="font-size:14px;color:{{r.lineInk}}">{{r.line}}</span></button></sc-for></nav>'
            '<div class="abs" aria-hidden="true" style="left:0;right:0;bottom:0;height:330px;background:linear-gradient(180deg,rgba(0,4,31,0) 0,rgba(0,4,31,.92) 38%,rgba(0,1,10,.96) 100%)"></div>'
            '<section aria-label="The picked quest" class="abs" style="left:64px;right:64px;top:800px;height:240px;display:flex;gap:56px;align-items:flex-start">'
            '<div style="width:340px;display:flex;flex-direction:column;gap:8px"><span class="lab">{{focus.kicker}}</span>'
            '<span style="font-size:36px;line-height:1.1">{{focus.name}}</span>'
            '<span class="num" style="font-size:16px;color:{{focus.lineInk}}">{{focus.line}}</span></div>'
            '<div style="flex:1;position:relative;display:flex;gap:28px;padding-top:4px">'
            '<div style="position:absolute;left:8px;right:60px;top:13px;height:1px;background:#434C89"></div>'
            '<sc-for list="{{steps}}" as="g" hint-placeholder-count="3"><div style="position:relative;flex:1;display:flex;flex-direction:column;gap:12px">'
            '<span class="dia" style="width:16px;height:16px;margin-left:1px;background:{{g.fill}};border:2px solid {{g.ring}};box-shadow:0 0 0 6px #00041F"></span>'
            '<span class="lab">{{g.tier}}</span><span style="font-size:18px;line-height:1.4;color:{{g.ink}}">{{g.text}}</span>'
            '<span class="lab" style="color:#E6E9FF">{{g.note}}</span></div></sc-for></div>'
            '<div style="width:360px;height:200px;display:flex;flex-direction:column">' + CTA + '</div></section>')
    return page('Rev 101 C - Quests as a route: the five quests as stations along one course, the open one\'s goals as checkpoints - interactive', body, LOGIC)


# ---------- Compare ----------
def compare():
    def mark(fill, ring, size=16):
        return '<span class="dia" style="width:%dpx;height:%dpx;background:%s;border:2px solid %s"></span>' % (size, size, fill, ring)

    def ladder(rows):
        h = '<div style="display:flex;flex-direction:column;gap:18px">'
        for txt, tier, f, r, ink, note in rows:
            h += ('<div style="display:flex;gap:18px;align-items:flex-start"><span style="margin-top:5px">' + mark(f, r, 14) + '</span>'
                  '<span style="flex:1;display:flex;flex-direction:column;gap:5px"><span style="font-size:17px;color:%s">%s</span>'
                  '<span style="display:flex;justify-content:space-between"><span class="lab">%s</span><span class="lab" style="color:#E6E9FF">%s</span></span></span></div>') % (ink, txt, tier, note)
        return h + '</div>'

    W, Wd, D = '#E6E9FF', '#434C89', 'rgba(0,0,0,0)'
    ready = [('Win a Joust match at Intensity 2', 'Opens Intensity 3', W, W, W, 'Done'),
             ('Win a Joust match at Intensity 3', 'Opens Intensity 4', W, W, W, 'Done'),
             ('Claim', 'Opens Maelstrom', D, '#99FF80', W, 'Ready')]
    opened = [('Play 3 games at Intensity 2', 'Opens Intensity 3', W, W, W, 'Done'),
              ('Play 3 games at Intensity 3', 'Opens Intensity 4', D, W, W, 'Next'),
              ('Claim', 'Opens Vessel Hangar', D, Wd, '#747BAD', '')]
    btn_claim = '<span class="btn softcta" style="width:100%"><span class="kc">A</span>Claim</span>'
    btn_play = '<span class="btn softcta" style="width:100%"><span class="kc">A</span>Play quest</span>'

    def col_a(name, art, line, lineInk, rows, btn, opens=None):
        img = ('<img src="%s" alt="" style="width:100%%;height:96px;object-fit:cover;-webkit-mask-image:linear-gradient(180deg,#000 55%%,transparent);mask-image:linear-gradient(180deg,#000 55%%,transparent)">' % art) if art else ''
        op = ''
        if opens:
            op = ('<div style="display:flex;gap:14px;align-items:center"><img src="%s" alt="" style="width:96px;height:54px;object-fit:cover">'
                  '<span style="display:flex;flex-direction:column;gap:3px"><span class="lab" style="font-size:12px">Claim opens</span><span style="font-size:17px">%s</span></span></div>') % opens
        return ('<div style="display:flex;flex-direction:column;gap:12px">%s'
                '<span style="font-size:28px">%s</span><span style="font-size:15px;color:%s">%s</span>%s%s%s</div>') % (img, name, lineInk, line, ladder(rows), op, btn)

    def col_c(name, line, lineInk, rows, btn):
        h = '<div style="display:flex;flex-direction:column;gap:14px"><span style="font-size:28px">%s</span><span style="font-size:15px;color:%s">%s</span>' % (name, lineInk, line)
        h += '<div style="position:relative;display:flex;gap:14px"><div style="position:absolute;left:8px;right:40px;top:9px;height:1px;background:#434C89"></div>'
        for txt, tier, f, r, ink, note in rows:
            h += ('<div style="position:relative;flex:1;display:flex;flex-direction:column;gap:8px">' + mark(f, r, 14).replace('"></span>', ';box-shadow:0 0 0 5px #00041F"></span>') +
                  '<span class="lab" style="font-size:12px">%s</span><span style="font-size:15px;line-height:1.35;color:%s">%s</span><span class="lab" style="font-size:12px;color:#E6E9FF">%s</span></div>') % (tier, ink, txt, note)
        return h + '</div>' + btn + '</div>'

    cols = [
        ('A  Star chart', 'The picked star\'s column: art, goals as a ladder, what it opens, the button.',
         col_a('Joust', ART['joust'], 'Ready to claim', '#99FF80', ready, btn_claim),
         col_a('Maelstrom', None, '1 of 2 goals', '#747BAD', opened, btn_play)),
        ('B  Quest log', 'The same ladder, larger, in the middle; what the claim opens gets its own column.',
         col_a('Joust', None, 'Ready to claim', '#99FF80', ready, btn_claim, (ART['mael'], 'Maelstrom')),
         col_a('Maelstrom', None, '1 of 2 goals', '#747BAD', opened, btn_play, (ART['hangar'], 'Vessel Hangar'))),
        ('C  Route', 'The goals run left to right under the course, as checkpoints.',
         col_c('Joust', 'Ready to claim', '#99FF80', ready, btn_claim),
         col_c('Maelstrom', '1 of 2 goals', '#747BAD', opened, btn_play)),
    ]
    body = ('<div class="abs" style="left:64px;top:52px;display:flex;flex-direction:column;gap:10px"><span class="lab">Rev 101 compare</span>'
            '<span style="font-size:32px">One quest\'s goals and its claim, as each option draws them</span>'
            '<span style="font-size:16px;color:#747BAD">Top: Joust, both goals done, ready to claim. Bottom: Maelstrom, one goal of two done.</span></div>'
            '<div class="abs" style="left:64px;right:64px;top:176px;display:grid;grid-template-columns:repeat(3,minmax(0,1fr));column-gap:72px">')
    for head, sub, top, bot in cols:
        body += ('<div style="display:flex;flex-direction:column;gap:22px"><span style="font-size:22px">%s</span>'
                 '<span style="font-size:14px;line-height:1.5;color:#747BAD;height:42px">%s</span>%s'
                 '<div style="height:1px;background:rgba(67,76,137,.6)"></div>%s</div>') % (head, sub, top, bot)
    body += '</div>'
    return page('Rev 101 Compare - one quest\'s goals and its claim in each option', body,
                '\nclass Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n')


# ---------- Notes ----------
def notes():
    def block(head, lines):
        return ('<div style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">%s</span>' % head
                + ''.join('<span style="font-size:17px;line-height:1.55;color:#C9CDEB">%s</span>' % l for l in lines) + '</div>')
    cols = [
        block('Asked (Shombith, 2026-11-13)', [
            '&#8220;Remove the quest from inside the voyages. Quest should be a seperate screen with its own interactive UI not inside this itself.&#8221;',
            'So Quests leaves the Voyage window: no Quests tab there any more. The Voyage window keeps Voyages, with Saga and Episodes locked (their own thread).']),
        block('Where it opens from', [
            'Its own window over Home, like the Profile, with Back to Home. Home’s current-quest card opens it.',
            'Open question: should Quests also get a place on the top band (beside Home or Ark)? Not drawn; the band is shared by every screen.']),
        block('The three options', [
            '<b>A Star chart (recommended).</b> The HyperSea itself is the screen: Episode 1 is The Hypersea, so its quests are stars on it, and claiming one draws the current on to the next star. The most of its own; one press per star; the HyperSea theme without a second map.',
            '<b>B Quest log.</b> The calmest: a list, the goals large in the middle, what you win on its own column. Easiest to build, least its own.',
            '<b>C Route.</b> The episode as one course across the screen, the goals as checkpoints under it. Reads as a journey; less room for the goals.']),
        block('What is real, what is on the board', [
            'Real: the five quests in order, each one’s two goals and the step each opens (the GameModeQuest assets), the states (QuestFrontier: locked, open, ready, claimed), and claim opening the next.',
            'On the board only: Play quest counts a goal done on each press, so the whole chain can be clicked through. In the game it opens the Arcade at that intensity.',
            'The one motion: on Claim the line to the next quest draws in (A and C). Nothing else moves at rest. Ready is Lime (it is the thing to press); the picked quest is the one Jade line.',
            'The quest named HEX RACE in its asset is shown as Skim Race.']),
    ]
    body = ('<div class="abs" style="left:64px;top:56px;display:flex;flex-direction:column;gap:10px"><span class="lab">Rev 101 notes</span>'
            '<span style="font-size:32px">Quests as a screen of its own</span></div>'
            '<div class="abs" style="left:64px;right:64px;top:170px;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));column-gap:56px">'
            + ''.join(cols) + '</div>')
    return page('Rev 101 notes - Quests as a screen of its own', body,
                '\nclass Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n', 1920, 900)


for name, fn in [('R101A', board_a), ('R101B', board_b), ('R101C', board_c), ('R101Compare', compare), ('R101Notes', notes)]:
    with open(os.path.join(out, name + '.dc.html'), 'w') as f:
        f.write(fn())
print('written', out)
