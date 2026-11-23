#!/usr/bin/env python3
"""Rev 130 - the arcade card's picture, eight more ways, with the short line (2026-11-23).

Usage: python3 r130_card_pictures.py <root> 130
Shombith on the built Poster card: "the description is too big there should be a short description. For the arcade game
cards bg - I need 8 more variations 4 of them with screenshots and the directions we are going with all the variations
of hover, locked etc and 4 with symbolic approach where on hover the vessel does something for eg the scarab scores a
goal".

Every card is the Poster (Rev 128 E, built in PR #2345): the name in Aldrich over a navy fade, the element tag and the
star, the hull, and the panel that slides up on hover or pad focus - now holding the SHORT line
(Tools/Build/card_short_lines.json, built in the same branch). Only the picture and what it does on hover change.

  S1-S4 use real frames of the game (Docs/Pilots/art/refs: frames from the studio site and one flown in the Toybox).
  They are not yet one frame per mode: a pick means capturing one per mode (the look sheet, /unity-look).
  Y1-Y4 are symbolic: the mode's play drawn as a sign, and on hover the hull does the thing that scores.

Writes R<rev>Compare, R<rev>S1..S4, R<rev>Y1..Y4 and R<rev>Notes into <root>/project.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, REV = sys.argv[1], sys.argv[2]
_src = open(os.path.join(HERE, 'r128_game_cards.py')).read()
_src = _src[:_src.index("T = 'Rev %s' % REV")]
sys.argv = [sys.argv[0], root, REV]
exec(_src)  # STYLE (with the cards' CSS), STAR, LOCK, shape, HULL, blob, page, BACKDROP, HUB, NOLOGIC, out, W, H

SHORT = json.load(open(os.path.join(HERE, '..', '..', '..', 'Tools', 'Build', 'card_short_lines.json')))['lines']
SHAPE['Mass'] = '12,2.5 21.5,20.5 2.5,20.5'
HULL['Scarab'] = blob('17f5b385c9d29ecd62531ae716ec35f6')
# The four cards in the row, as their SO_ArcadeGame authors them; the short line from card_short_lines.json.
CARDS = {
    'Skim Race': {'asset': 'ArcadeGameSkimRace', 'hull': 'Squirrel', 'el': 'Time', 'pl': '1 - 12', 'act': 'skim',
                  'art': blob('ca2666bff475ae6f282abb3614e3c1ee'), 'frame': blob('14f032df7a080228d778c912a82fb628')},
    'Scarab Scramble': {'asset': 'ArcadeGameScarabScramble', 'hull': 'Scarab', 'el': 'Mass', 'pl': '1 - 9', 'act': 'goal',
                        'art': blob('ae2764d2d1f0e1d8b4ba954a4b54eb6e'), 'frame': blob('6bf62bd87cbcad958a4e4dadd5f8b207')},
    'Dog Fight': {'asset': 'ArcadeGameDogFight', 'hull': 'Sparrow', 'el': 'Charge', 'pl': '2 - 9', 'act': 'shoot',
                  'art': blob('d9a6022ba95ff2a8882f7aebb9380f2c'), 'frame': blob('6df4474fc1dfb17232df79858552d3b4')},
    'Headlong': {'asset': 'ArcadeGameHeadlong', 'hull': 'Rhino', 'el': 'Time', 'pl': '2 - 9', 'act': 'plough',
                 'art': blob('5d08b235a136f9e205973efec979b209'), 'frame': blob('90f2cd458ea26f18d40bd02550398ec7')},
}
ROW = ['Skim Race', 'Scarab Scramble', 'Dog Fight', 'Headlong']
REEL = [blob('5d5bfb5e289985f6a4fbfced8fb090f7'), blob('75ae446668e6746c62472dd162e7285c')]

CSS2 = '''
/* ---- the Poster skeleton every option shares (Rev 128 E as built, PR #2345) ---- */
.pk .pic{position:absolute;inset:0;overflow:hidden}
.pk .shade{position:absolute;left:0;right:0;bottom:0;height:200px;z-index:2;background:linear-gradient(180deg,rgba(0,4,31,0) 0,rgba(0,4,31,.86) 58%,#00041F 100%)}
.pk .title{position:absolute;left:20px;bottom:20px;width:229px;z-index:3;font-family:'Aldrich',sans-serif;font-size:36px;line-height:1;color:#E6E9FF;transition:transform .2s ease}
.pk .tag{position:absolute;left:12px;top:10px;z-index:3}
.pk .hp{position:absolute;right:12px;bottom:14px;z-index:3;width:60px;height:60px;transition:opacity .2s}
.pk .open{position:absolute;left:0;right:0;bottom:0;height:0;z-index:4;overflow:hidden;background:rgba(0,4,31,.96);transition:height .2s ease}
.pk .open .sl{position:absolute;left:20px;right:20px;top:16px;font-family:'Aldrich',sans-serif;font-size:15px;line-height:1.45;color:#E6E9FF}
.pk .open .pl{position:absolute;left:20px;bottom:14px}
.pk:hover .open,.pk.hv .open,.pk.fc .open{height:84px}
.pk:hover .title,.pk.hv .title,.pk.fc .title{transform:translateY(-84px)}
.pk:hover .hp,.pk.hv .hp,.pk.fc .hp{opacity:0}
.lk .title{color:#747BAD}
.lk .pic{filter:grayscale(1) brightness(.45)}
.lk .open{display:none}
.lk .acts *{animation:none!important}

/* ---- S1 Frame: a real frame of the game, full bleed; hover pushes in ---- */
.s1 .ph{position:absolute;inset:-8px;background-size:cover;background-position:center;transition:transform .6s cubic-bezier(.22,1,.36,1)}
.s1 .ph{animation:drift 40s ease-in-out infinite alternate}
.s1:hover .ph,.s1.hv .ph,.s1.fc .ph{transform:scale(1.08)}

/* ---- S2 Night grade: the frame graded to the deep sea at rest; hover lights it in full colour ---- */
.s2 .ph{position:absolute;inset:-8px;background-size:cover;background-position:center;filter:grayscale(1) brightness(.85) contrast(1.1);transition:filter .45s ease}
.s2 .tint{position:absolute;inset:0;background:#434C89;mix-blend-mode:color;transition:opacity .45s ease}
.s2:hover .ph,.s2.hv .ph,.s2.fc .ph{filter:none}
.s2:hover .tint,.s2.hv .tint,.s2.fc .tint{opacity:0}

/* ---- S3 Reel: three frames; hover plays them as a slide show, the ticks show which ---- */
.s3 .ph{position:absolute;inset:-8px;background-size:cover;background-position:center;opacity:0}
.s3 .ph.f0{opacity:1}
.s3:hover .ph,.s3.hv .ph,.s3.fc .ph{animation:reel 4.5s linear infinite}
.s3:hover .ph.f1,.s3.hv .ph.f1,.s3.fc .ph.f1{animation-delay:1.5s}
.s3:hover .ph.f2,.s3.hv .ph.f2,.s3.fc .ph.f2{animation-delay:3s}
@keyframes reel{0%{opacity:0;transform:scale(1)}6%{opacity:1}33%{opacity:1}40%{opacity:0;transform:scale(1.05)}100%{opacity:0}}
.s3 .ticks{position:absolute;right:56px;top:20px;z-index:3;display:flex;gap:4px;opacity:0;transition:opacity .2s}
.s3 .ticks i{display:block;width:14px;height:2px;background:#434C89;position:relative;overflow:hidden}
.s3 .ticks i::after{content:"";position:absolute;inset:0;background:#E6E9FF;transform:translateX(-100%)}
.s3:hover .ticks,.s3.hv .ticks,.s3.fc .ticks{opacity:1}
.s3:hover .ticks i::after,.s3.hv .ticks i::after,.s3.fc .ticks i::after{animation:tick 4.5s linear infinite}
.s3 .ticks i:nth-child(2)::after{animation-delay:1.5s!important}
.s3 .ticks i:nth-child(3)::after{animation-delay:3s!important}
@keyframes tick{0%{transform:translateX(-100%)}33%{transform:translateX(0)}34%{transform:translateX(100%)}100%{transform:translateX(100%)}}

/* ---- S4 Window: the frame in a window, the hull in front of it, the short line always on the card ---- */
.s4 .pic{inset:0 0 auto 0;height:182px;clip-path:polygon(0 0,100% 0,100% 168px,calc(100% - 14px) 182px,0 182px)}
.s4 .ph{position:absolute;inset:-10px;background-size:cover;background-position:center;transition:transform 1.2s cubic-bezier(.22,1,.36,1)}
.s4:hover .ph,.s4.hv .ph,.s4.fc .ph{transform:scale(1.1) translate(-10px,4px)}
.s4 .shade{display:none}
.s4 .rim{position:absolute;left:0;right:0;top:182px;height:1px;background:#434C89;z-index:2}
.s4 .hp{right:auto;left:14px;bottom:auto;top:130px;width:96px;height:96px;filter:drop-shadow(0 10px 12px rgba(0,1,10,.9));transition:transform .3s cubic-bezier(.22,1,.36,1)}
.s4:hover .hp,.s4.hv .hp,.s4.fc .hp{opacity:1;transform:translate(6px,-8px) rotate(-6deg)}
.s4 .title{left:122px;bottom:auto;top:196px;width:205px;font-size:22px;white-space:nowrap}
.s4 .open{display:none}
.s4 .sl2{position:absolute;left:122px;right:16px;top:226px;font-family:'Aldrich',sans-serif;font-size:15px;line-height:1.4;color:#B8C0F0;z-index:3}
.s4:hover .title,.s4.hv .title,.s4.fc .title{transform:none}
.s4 .pl2{position:absolute;left:20px;bottom:12px;z-index:3}

/* ---- the symbolic cards: a sign of the play on the deep sea, and the hull acting it out on hover ---- */
.y .pic{background:#00041F}
.y .acts{position:absolute;inset:0}
.y .ves{position:absolute;left:0;top:0;width:76px;height:76px;offset-rotate:0deg;offset-distance:0%;z-index:2}
.y .sil{filter:brightness(0) invert(.92)}
.y .hp{display:none}
.y .go{animation-duration:2.6s;animation-iteration-count:infinite;animation-timing-function:cubic-bezier(.45,0,.2,1);animation-play-state:paused}
.y:hover .go,.y.hv .go,.y.fc .go{animation-play-state:running}
.y .pop{position:absolute;font-family:'Aldrich',sans-serif;font-size:22px;color:#E6E9FF;opacity:0;z-index:3}
@keyframes run{0%,8%{offset-distance:0%}62%,100%{offset-distance:100%}}
@keyframes ball{0%,40%{offset-distance:0%;opacity:1}72%{offset-distance:100%;opacity:1}80%,100%{offset-distance:100%;opacity:0}}
@keyframes flash{0%,70%{opacity:.35}74%{opacity:1}100%{opacity:.35}}
@keyframes pop{0%,70%{opacity:0;transform:translateY(0)}76%{opacity:1}100%{opacity:0;transform:translateY(-22px)}}
@keyframes spark{0%,55%{opacity:0;transform:scale(.4)}62%{opacity:1;transform:scale(1.4)}80%,100%{opacity:0;transform:scale(1.8)}}
@keyframes shot{0%,20%{offset-distance:0%;opacity:0}24%{opacity:1}52%{offset-distance:100%;opacity:1}54%,100%{offset-distance:100%;opacity:0}}
@keyframes hit{0%,50%{opacity:.9}54%{opacity:.2}58%{opacity:.9}62%{opacity:.2}66%,100%{opacity:.9}}
@keyframes scatter{0%,46%{transform:translate(0,0) rotate(0);opacity:1}70%{opacity:1}100%{transform:translate(var(--dx),var(--dy)) rotate(var(--r));opacity:0}}
@keyframes draw{0%{stroke-dashoffset:400}60%,100%{stroke-dashoffset:0}}

/* Y1 Pictogram: the sign big and flat, the hull a white silhouette */
.y1 .sign{stroke:#E6E9FF;fill:none;stroke-width:3}
.y1 .soft{stroke:#434C89;fill:none;stroke-width:2}

/* Y2 Playbook: a chalk diagram on a grid; hover draws the line and the hull runs it */
.y2 .pic{background:linear-gradient(#222645 1px,transparent 1px) 0 0/24px 24px,linear-gradient(90deg,#222645 1px,transparent 1px) 0 0/24px 24px,#00041F}
.y2 .route{stroke:#B8C0F0;stroke-width:2;fill:none;stroke-dasharray:6 6}
.y2 .ink{stroke:#E6E9FF;stroke-width:2.5;fill:none;stroke-dasharray:400;stroke-dashoffset:400}
.y2:hover .ink,.y2.hv .ink,.y2.fc .ink{animation:draw 2.6s ease-out infinite}
.y2 .mk{font-family:'Chakra Petch',sans-serif;font-size:14px;fill:#747BAD;letter-spacing:.12em}

/* Y3 Diorama: layers at three depths; hover tilts them apart while the hull acts */
.y3 .back,.y3 .mid,.y3 .front{position:absolute;inset:0;transition:transform .35s cubic-bezier(.22,1,.36,1)}
.y3:hover .back,.y3.hv .back,.y3.fc .back{transform:translate(6px,-3px) scale(1.02)}
.y3:hover .mid,.y3.hv .mid,.y3.fc .mid{transform:translate(-4px,2px)}
.y3:hover .front,.y3.hv .front,.y3.fc .front{transform:translate(-12px,6px) scale(1.06)}
.y3 .rimarc{stroke:#434C89;fill:none;stroke-width:1.2}
.y3 .ves{width:84px;height:84px;filter:drop-shadow(0 10px 10px rgba(0,1,10,.9))}

/* Y4 Crest: the play inside a badge; hover sends the hull round it and the badge lights */
.y4 .crest{stroke:#434C89;fill:#00041F;stroke-width:2}
.y4 .crest2{stroke:#E6E9FF;fill:none;stroke-width:2;opacity:.35}
.y4:hover .crest2,.y4.hv .crest2,.y4.fc .crest2{animation:flash 2.6s infinite}
'''
STYLE += CSS2


def pilots(m):
    return '<span class="capt">%s pilots</span>' % CARDS[m]['pl']


def skeleton(o, m, st, pic, acts='', s4=False):
    d = CARDS[m]
    words = m.split(' ')
    title = '<br>'.join(words) if (len(words) > 1 and not s4) else m
    fs = '' if not s4 else ''
    tag = '<div class="tag">%s</div>' % shape(d['el'], 20)
    if s4:
        extra = ('<div class="rim"></div><div class="sl2">%s</div><div class="pl2">%s</div>' % (SHORT[d['asset']], pilots(m)))
        opener = ''
    else:
        extra = ''
        opener = '<div class="open"><div class="sl">%s</div><div class="pl">%s</div></div>' % (SHORT[d['asset']], pilots(m))
    inner = ('<div class="gc"><div class="pic">%s</div>%s<div class="shade"></div>%s%s'
             '<img class="hp hl" src="%s" alt="%s"><div class="title"%s>%s</div>%s%s%s</div><div class="edge"></div>'
             % (pic, acts, tag, STAR, HULL[d['hull']], d['hull'], fs, title, opener, extra, LOCK))
    return wrap('pk ' + o, inner, st)


# ------------------------------------------------------------------ S: real frames
def card_s1(m, st=''):
    return skeleton('s1', m, st, '<div class="ph" style="background-image:url(%s)"></div>' % CARDS[m]['frame'])


def card_s2(m, st=''):
    return skeleton('s2', m, st, '<div class="ph" style="background-image:url(%s)"></div><div class="tint"></div>' % CARDS[m]['frame'])


def card_s3(m, st=''):
    frames = [CARDS[m]['frame']] + REEL
    pic = ''.join('<div class="ph f%d" style="background-image:url(%s)"></div>' % (k, f) for k, f in enumerate(frames))
    return skeleton('s3', m, st, pic, acts='<div class="ticks"><i></i><i></i><i></i></div>')


def card_s4(m, st=''):
    return skeleton('s4', m, st, '<div class="ph" style="background-image:url(%s)"></div>' % CARDS[m]['frame'], s4=True)


# ------------------------------------------------------------------ Y: the sign of the play
# Each act is drawn in card units (341 x 296). The hull runs an offset-path; the prop reacts on the same 2.6 s beat.
PATHS = {
    'goal': ('M 44 162 C 80 150 110 124 140 120', 'M 150 118 C 190 108 240 90 276 76'),
    'skim': ('M 36 170 C 80 150 110 100 160 90 C 210 80 240 64 268 52', None),
    'shoot': ('M 44 160 C 80 140 100 122 120 114', 'M 148 106 L 240 76'),
    'plough': ('M 40 118 L 300 118', None),
}


def prop(act, style):
    """The mode's sign: what the hull acts on. Plain shapes in the token colours."""
    sign = 'sign' if style == 'y1' else ('route' if style == 'y2' else 'rimarc')
    if act == 'goal':
        return ('<svg class="abs" width="341" height="296" style="left:0;top:0" aria-hidden="true">'
                '<ellipse cx="286" cy="74" rx="20" ry="36" fill="none" stroke="#E6E9FF" stroke-width="3" class="go" style="animation-name:flash"/>'
                '<ellipse cx="286" cy="74" rx="28" ry="46" fill="none" stroke="#434C89" stroke-width="1.5"/></svg>'
                '<i class="go" style="position:absolute;left:0;top:0;width:18px;height:18px;border-radius:50%%;background:#E6E9FF;'
                'offset-path:path(\'%s\');offset-rotate:0deg;animation-name:ball"></i>'
                '<span class="pop go" style="left:264px;top:8px;animation-name:pop">+1</span>' % PATHS['goal'][1])
    if act == 'skim':
        dashes = ''.join('<line x1="%d" y1="%d" x2="%d" y2="%d"/>' % (x, y, x + 8, y - 3)
                         for x, y in [(52, 162), (74, 152), (94, 136), (112, 116), (134, 100), (160, 92), (186, 86), (212, 78), (236, 66)])
        return ('<svg class="abs" width="341" height="296" style="left:0;top:0" aria-hidden="true">'
                '<g stroke="#B8C0F0" stroke-width="3">%s</g>'
                '<polygon points="284,24 298,42 284,60 270,42" fill="none" stroke="#E6E9FF" stroke-width="3"/></svg>'
                '<span class="go" style="position:absolute;left:262px;top:20px;width:44px;height:44px;border-radius:50%%;border:2px solid #E6E9FF;animation-name:spark;opacity:0"></span>'
                '<span class="pop go" style="left:200px;top:8px;animation-name:pop">First</span>' % dashes)
    if act == 'shoot':
        return ('<img class="go sil" src="%s" alt="Rival" style="position:absolute;left:236px;top:44px;width:64px;height:64px;transform:scaleX(-1);animation-name:hit">'
                '%s<span class="pop go" style="left:280px;top:14px;animation-name:pop">+1</span>'
                % (HULL['Sparrow'], ''.join('<i class="go" style="position:absolute;left:0;top:0;width:6px;height:6px;background:#E6E9FF;'
                                             'offset-path:path(\'%s\');animation-name:shot;animation-delay:%.2fs"></i>' % (PATHS['shoot'][1], k * .12)
                                             for k in range(3))))
    if act == 'plough':
        blocks = []
        for k in range(10):
            x, y = 214 + (k % 2) * 16, 64 + k * 11
            blocks.append('<i class="go" style="position:absolute;left:%dpx;top:%dpx;width:14px;height:10px;border:1.5px solid #E6E9FF;'
                          '--dx:%dpx;--dy:%dpx;--r:%ddeg;animation-name:scatter"></i>' % (x, y, 40 + (k * 7) % 30, (k - 5) * 9, (k * 47) % 180))
        return ''.join(blocks) + '<span class="pop go" style="left:236px;top:20px;animation-name:pop">Through</span>'
    return ''


def vessel(m, style):
    d = CARDS[m]
    path = PATHS[d['act']][0]
    cls = 'ves go' + (' sil' if style == 'y1' else '')
    return ('<img class="%s hl" src="%s" alt="%s" style="offset-path:path(\'%s\');animation-name:run">'
            % (cls, HULL[d['hull']], d['hull'], path))


def card_y1(m, st=''):
    acts = '<div class="acts">%s%s</div>' % (prop(CARDS[m]['act'], 'y1'), vessel(m, 'y1'))
    return skeleton('y y1', m, st, '', acts=acts)


def card_y2(m, st=''):
    d = CARDS[m]
    a, b = PATHS[d['act']]
    route = ('<svg class="abs" width="341" height="296" style="left:0;top:0" aria-hidden="true">'
             '<path class="route" d="%s"/>%s<path class="ink" d="%s"/>'
             '<text class="mk" x="26" y="%d">1</text><text class="mk" x="144" y="%d">2</text>%s</svg>'
             % (a, ('<path class="route" d="%s"/>' % b) if b else '', a, 186 if d['act'] != 'plough' else 108,
                104 if d['act'] != 'plough' else 108, '<text class="mk" x="300" y="22">3</text>' if b else ''))
    acts = '<div class="acts">%s%s%s</div>' % (route, prop(d['act'], 'y2'), vessel(m, 'y2'))
    return skeleton('y y2', m, st, '', acts=acts)


def card_y3(m, st=''):
    d = CARDS[m]
    back = ('<div class="back"><svg class="abs" width="341" height="296" style="left:0;top:0" aria-hidden="true">'
            '<circle class="rimarc" cx="170" cy="560" r="470"/><circle class="rimarc" cx="170" cy="560" r="430" opacity=".5"/>'
            '%s</svg></div>' % ''.join('<circle cx="%d" cy="%d" r="%.1f" fill="#B8C0F0" opacity="%.2f"/>'
                                       % ((k * 73) % 341, (k * 41) % 200 + 10, .8 + (k % 3) * .5, .25 + (k % 4) * .12) for k in range(36)))
    acts = ('%s<div class="mid"><div class="acts">%s</div></div><div class="front"><div class="acts">%s</div></div>'
            % (back, prop(d['act'], 'y3'), vessel(m, 'y3')))
    return skeleton('y y3', m, st, '', acts=acts)


def card_y4(m, st=''):
    d = CARDS[m]
    crest = ('<svg class="abs" width="341" height="296" style="left:0;top:0" aria-hidden="true">'
             '<polygon class="crest" points="170,10 300,50 300,150 170,196 40,150 40,50"/>'
             '<polygon class="crest2" points="170,20 290,56 290,144 170,186 50,144 50,56"/></svg>')
    acts = '<div class="acts">%s%s%s</div>' % (crest, prop(d['act'], 'y4'), vessel(m, 'y4'))
    return skeleton('y y4', m, st, '', acts=acts)


def card_now(m, st=''):
    """The Poster as built (PR #2345): the inside-the-cell render, drifting."""
    return skeleton('s1', m, st, '<div class="ph" style="background-image:url(%s)"></div>' % CARDS[m]['art'])


OPTS = [
    ('S1', 'Frame', card_s1, 'A real frame of the game fills the card and drifts. Hover pushes in, and the panel brings the short line.'),
    ('S2', 'Night grade', card_s2, 'The frame is graded to the deep sea at rest, so a grid of cards reads as one calm wall. Hover lights it in full colour.'),
    ('S3', 'Reel', card_s3, 'Three frames of the mode. Hover plays them as a slow slide show, with ticks for which one is up.'),
    ('S4', 'Window', card_s4, 'The frame in a window, the hull standing in front of it, the short line always on the card. No panel: hover pans the frame and lifts the hull.'),
    ('Y1', 'Pictogram', card_y1, 'The play as a flat white sign and the hull as a silhouette. Hover: the hull does the thing that scores.'),
    ('Y2', 'Playbook', card_y2, 'A chalk play on a grid, numbered. Hover draws the line and the hull runs it.'),
    ('Y3', 'Diorama', card_y3, 'The cell rim and dust behind, the prop in the middle, the hull in front. Hover parts the layers while the hull acts.'),
    ('Y4', 'Crest', card_y4, 'The play inside a badge. Hover: the hull acts it out and the badge lights.'),
]
ACTS = {'Skim Race': 'skims the trail to the crystal', 'Scarab Scramble': 'bats the ball through the ring',
        'Dog Fight': 'fires on a rival', 'Headlong': 'ploughs through the hedge'}


def option_board(key, name, fn, line, title):
    strip_mode = 'Scarab Scramble' if key.startswith('Y') else 'Skim Race'
    grid = ''.join('<div class="abs" style="left:%dpx;top:212px">%s</div>' % (64 + k * 381, fn(m)) for k, m in enumerate(ROW))
    states = [('Rest', ''), ('Hover (held)', 'hv'), ('Pad focus', 'fc'), ('Locked', 'lk')]
    strip = ''.join('<div class="abs" style="left:%dpx;top:610px"><span class="capt" style="display:block;margin-bottom:16px">%s</span>%s</div>'
                    % (64 + k * 381, lab, fn(strip_mode, st)) for k, (lab, st) in enumerate(states))
    what = ('Real frames of the game; a pick means one frame captured per mode.' if key.startswith('S') else
            'On hover: ' + '; '.join('%s %s' % (CARDS[m]['hull'], ACTS[m]) for m in ROW) + '.')
    side = ('<div class="abs" style="left:1600px;top:212px;width:256px;display:flex;flex-direction:column;gap:14px">'
            '<span class="lab" style="color:#E6E9FF">%s &#183; %s</span>'
            '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">%s</span>'
            '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">%s</span>'
            '<span style="font-size:14px;line-height:1.5;color:#747BAD">Point at any card above. The row below holds %s in each state.</span></div>'
            % (key, name, line, what, strip_mode))
    body = (BACKDROP + HUB +
            '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,4,31,.95)"></div>'
            '<div class="abs" style="left:64px;top:108px;display:flex;align-items:baseline;gap:22px;z-index:4">'
            '<span style="font-size:28px">Arcade</span><span class="lab">All games</span></div>'
            + grid +
            '<div class="abs" style="left:64px;right:64px;top:572px;height:1px;background:#222645"></div>'
            '<span class="lab abs" style="left:64px;top:584px">%s, every state</span>' % strip_mode
            + strip + side)
    return page(title, body, NOLOGIC)


def compare(title):
    cells = [('Now', 'The Poster as built', card_now)] + [(k, n, f) for k, n, f, _ in OPTS]
    pos = [(64 + c * 362, 172) for c in range(5)] + [(64 + c * 362, 602) for c in range(4)]
    out_cells = []
    for (x, y), (key, name, fn) in zip(pos, cells):
        out_cells.append('<div class="abs" style="left:%dpx;top:%dpx;width:341px">'
                         '<div style="display:flex;gap:12px;align-items:baseline;margin-bottom:16px"><span class="lab"%s>%s</span>'
                         '<span style="font-size:18px">%s</span></div>%s</div>'
                         % (x, y, ' style="color:#E6E9FF"' if key == 'Now' else '', key, name, fn('Scarab Scramble')))
    legend = ('<div class="abs" style="left:1512px;top:602px;width:344px;display:flex;flex-direction:column;gap:14px">'
              '<span class="lab" style="color:#E6E9FF">Read me</span>'
              '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">Scarab Scramble, nine ways at the in-game size. The top row is the '
              'Poster as built, then four made from real frames of the game (S). The bottom row is symbolic (Y): point at one and the '
              'Scarab bats its ball through the ring.</span>'
              '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">Every card now opens on its short line, not the long description.</span>'
              '<span style="font-size:14px;line-height:1.5;color:#747BAD">Each option has its own board with four modes and every state.</span></div>')
    body = (BACKDROP + HUB +
            '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,4,31,.95)"></div>'
            '<div class="abs" style="left:64px;top:100px;display:flex;align-items:baseline;gap:22px"><span style="font-size:28px">The card&#8217;s picture, eight more ways</span>'
            '<span class="lab">Real frames, and signs where the vessel scores</span></div>'
            + ''.join(out_cells) + legend)
    return page(title, body, NOLOGIC)


def notes(title):
    def block(head, lines):
        return ('<div style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">%s</span>'
                + ''.join('<span style="font-size:15px;line-height:1.55;color:#B8C0F0">%s</span>' % l for l in lines) + '</div>') % head
    cols = [
        block('Asked (Shombith, 2026-11-23)', [
            '&#8220;the description is too big there should be a short description.&#8221;',
            '&#8220;I need 8 more variations 4 of them with screenshots and the directions we are going with all the variations of hover, locked etc and 4 with symbolic approach where on hover the vessel does something for eg the scarab scores a goal&#8221;']),
        block('The short line', [
            'Built, not only drawn: every arcade card has a short line now (SO_Game.ShortDescription, 86 lines in Tools/Build/card_short_lines.json, under 48 characters each).',
            'The Poster&#8217;s panel shows it instead of the long description; the panel is 84 tall, was 112.']),
        block('S, real frames', [
            'S1 Frame, S2 Night grade, S3 Reel, S4 Window. The frames are the game&#8217;s own (Docs/Pilots/art/refs), one per card here, but not yet one per mode.',
            'A pick means capturing a frame per mode at intensity 2 (the look sheet, /unity-look), not a hand-made picture.']),
        block('Y, the sign of the play', [
            'Y1 Pictogram, Y2 Playbook, Y3 Diorama, Y4 Crest. On hover the hull does what scores: the Scarab bats its ball through the ring, the Squirrel skims to the crystal, the Sparrow fires, the Rhino ploughs through.',
            'In the game each is a small UI animation per mode (a prop, a path, one hull sprite), playing only while the card is hovered or focused.']),
    ]
    body = ('<div class="abs" style="left:64px;top:56px;display:flex;flex-direction:column;gap:10px"><span class="lab">Rev %s notes</span>'
            '<span style="font-size:36px">The card&#8217;s picture, eight more ways, and the short line</span></div>'
            '<div class="abs" style="left:64px;right:64px;top:170px;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));column-gap:56px">'
            % REV + ''.join(cols) + '</div>')
    return page(title, body, NOLOGIC, 1920, 900)


T = 'Rev %s' % REV
boards = {'Compare': compare('%s - Compare: Scarab Scramble, the Poster as built and eight more pictures' % T)}
for key, name, fn, line in OPTS:
    boards[key] = option_board(key, name, fn, line, '%s %s - %s: the Arcade row and every state - interactive' % (T, key, name))
boards['Notes'] = notes('%s - notes: the short line, real frames, signs of the play' % T)
for k, html in boards.items():
    with open(os.path.join(out, 'R%s%s.dc.html' % (REV, k)), 'w') as f:
        f.write(html)
print('written', out, sorted(boards))
