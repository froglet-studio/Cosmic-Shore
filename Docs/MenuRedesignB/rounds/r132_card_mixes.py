#!/usr/bin/env python3
"""Rev 132 - four cards that mix a real frame of the game with the vessel acting (2026-11-24).

Usage: python3 r132_card_mixes.py <root> 132
Shombith on Rev 131: "Create me some more variations mixing screenshots and the diagetic ideas, 4 more seperate".

Each mix lays a real game frame (S1's) under one of the acting pictures, so the card reads as the place at rest and the
vessel scores in it on hover:
  M1 Frame + Light trail - the frame dimmed, the play written over it in light; hover flies it and the frame pushes in.
  M2 Frame + Hero shot   - the frame motion-blurred behind the huge hull and its rays; hover lunges into the burst.
  M3 Frame + Diorama     - the frame as the diorama's back plane; hover parts the planes while the vessel acts.
  M4 Frame + Chase cam   - the frame is the view ahead; hover rushes into it while the target looms and the hull scores.
Built on r131_card_acts.py (and through it r130's skeleton): each mix is that card with the frame put in its picture.
Writes R<rev>Compare, R<rev>M1..M4 and R<rev>Notes.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, REV = sys.argv[1], sys.argv[2]
_src = open(os.path.join(HERE, 'r131_card_acts.py')).read()
_src = _src[:_src.rindex("\nT = 'Rev %s' % REV")]
sys.argv = [sys.argv[0], root, REV]
exec(_src)  # r131 and r130: skeleton, CARDS, card_s1, card_y3, card_y5, card_y6, card_y7, option_board, page, ...

# The option board treats a mix like an acting card (Scarab Scramble in the state row, the acts in the side text).
_ob = open(os.path.join(HERE, 'r130_card_pictures.py')).read()
exec(_ob[_ob.index('def option_board'):_ob.index('\ndef compare')].replace("key.startswith('Y')", "key[0] in 'YM'"))

CSS4 = '''
/* ---- the mixes: a real frame of the game in the picture, the acting card on top ---- */
.mx .pic{background:#00041F}
.mx .ph{position:absolute;inset:-10px;background-size:cover;background-position:center;transition:transform .8s cubic-bezier(.22,1,.36,1),filter .5s}
.mx .dim{position:absolute;inset:0}

/* M1: dimmed and cooled so the light reads; hover pushes in */
.m1 .ph{filter:saturate(.7) brightness(.62)}
.m1 .dim{background:linear-gradient(180deg,rgba(0,4,31,.25) 0,rgba(0,4,31,.55) 100%%)}
%(m1ph)s{transform:scale(1.08);filter:saturate(1) brightness(.8)}

/* M2: the frame streaks behind the hero; the rays thin out over it */
.m2 .ph{filter:blur(1.5px) brightness(.72);transform:scale(1.06)}
.m2 .dim{background:radial-gradient(70%% 70%% at 30%% 46%%,rgba(0,4,31,.1) 0,rgba(0,4,31,.6) 100%%)}
.m2 .rays{opacity:.55}
@keyframes zoomblur{0%%,40%%{transform:scale(1.06);filter:blur(1.5px) brightness(.72)}56%%{transform:scale(1.22);filter:blur(4px) brightness(1)}100%%{transform:scale(1.06);filter:blur(1.5px) brightness(.72)}}
%(m2ph)s{animation:zoomblur 2.6s cubic-bezier(.5,0,.2,1) infinite}

/* M3: the frame is the diorama's back plane */
.m3 .ph{filter:brightness(.7)}
.m3 .dim{background:linear-gradient(180deg,rgba(0,4,31,.15) 0,rgba(0,4,31,.5) 100%%)}
%(m3ph)s{transform:translate(8px,-4px) scale(1.03)}

/* M4: the frame is the view ahead; the floor under the hull fades it out */
.m4 .ph{filter:brightness(.75);transform-origin:56%% 18%%}
.m4 .dim{background:linear-gradient(180deg,rgba(0,4,31,0) 0,rgba(0,4,31,.35) 30%%,rgba(0,4,31,.85) 62%%)}
.m4 .floor{opacity:.75}
@keyframes rushin{0%%{transform:scale(1)}60%%{transform:scale(1.45)}70%%{transform:scale(1.55);filter:brightness(1.05)}100%%{transform:scale(1)}}
%(m4ph)s{animation:rushin 2.6s cubic-bezier(.6,0,.6,1) infinite}
''' % {'m1ph': hov('m1', '.ph'), 'm2ph': hov('m2', '.ph'), 'm3ph': hov('m3', '.ph'), 'm4ph': hov('m4', '.ph')}
STYLE += CSS4


def mix(fn, base, key):
    """`fn`'s card with the mode's frame laid at the back of its picture."""
    def card(m, st=''):
        html = fn(m, st)
        layer = '<div class="ph" style="background-image:url(%s)"></div><div class="dim"></div>' % CARDS[m]['frame']
        assert html.count('<div class="pic">') == 1 and ('gw pk y %s ' % base) in html, base
        return (html.replace('<div class="pic">', '<div class="pic">' + layer, 1)
                    .replace('gw pk y %s ' % base, 'gw pk y %s mx %s ' % (base, key), 1))
    return card


MIX = [
    ('M1', 'Frame + Light trail', mix(card_y5, 'y5', 'm1'),
     'A real frame of the game, dimmed, with the play written over it in light and the hull at its end. Hover flies it live, '
     'the frame pushes in and the target blooms.'),
    ('M2', 'Frame + Hero shot', mix(card_y7, 'y7', 'm2'),
     'The frame streaks behind the huge hull and its speed rays. Hover: the hull lunges, the frame zoom-blurs, and the card '
     'shakes on the impact burst.'),
    ('M3', 'Frame + Diorama', mix(card_y3, 'y3', 'm3'),
     'The frame is the diorama&#8217;s back plane, the prop in the middle, the hull in front. Hover parts the planes while the '
     'vessel acts.'),
    ('M4', 'Frame + Chase cam', mix(card_y6, 'y6', 'm4'),
     'The frame is the view ahead of the hull. Hover rushes into it: the picture zooms, the target looms and the hull scores.'),
]


def compare(title):
    cells = [('S1', 'Frame (kept)', card_s1), ('Y3', 'Diorama (kept)', card_y3), ('Y7', 'Hero shot', card_y7)] + \
            [(k, n, f) for k, n, f, _ in MIX]
    pos = [(64 + c * 400, 172) for c in range(3)] + [(64 + c * 400, 602) for c in range(4)]
    cells_html = []
    for (x, y), (key, name, fn) in zip(pos, cells):
        cells_html.append('<div class="abs" style="left:%dpx;top:%dpx;width:341px">'
                          '<div style="display:flex;gap:12px;align-items:baseline;margin-bottom:16px"><span class="lab"%s>%s</span>'
                          '<span style="font-size:18px">%s</span></div>%s</div>'
                          % (x, y, ' style="color:#E6E9FF"' if key.startswith('M') else '', key, name, fn('Scarab Scramble')))
    legend = ('<div class="abs" style="left:1300px;top:172px;width:520px;display:flex;flex-direction:column;gap:14px">'
              '<span class="lab" style="color:#E6E9FF">Read me</span>'
              '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">Scarab Scramble at the in-game size. The top row is what the '
              'mixes are made from: the Frame, the Diorama and the Hero shot. The bottom row is four mixes: a real frame of the game '
              'at the back of the picture, and the vessel acting in front of it.</span>'
              '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">At rest the card shows the place; on hover the vessel '
              'scores in it. The Light trail and the Chase cam are mixed too.</span>'
              '<span style="font-size:14px;line-height:1.5;color:#747BAD">Point at a card. Each mix has its own board with four '
              'modes and every state.</span></div>')
    body = (BACKDROP + HUB +
            '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,4,31,.95)"></div>'
            '<div class="abs" style="left:64px;top:100px;display:flex;align-items:baseline;gap:22px"><span style="font-size:28px">'
            'Real frames with the vessel acting, four mixes</span><span class="lab">The place at rest, the score on hover</span></div>'
            + ''.join(cells_html) + legend)
    return page(title, body, NOLOGIC)


def notes(title):
    def block(head, lines):
        return ('<div style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">%s</span>'
                + ''.join('<span style="font-size:15px;line-height:1.55;color:#B8C0F0">%s</span>' % l for l in lines) + '</div>') % head
    cols = [
        block('Asked (Shombith, 2026-11-24)', [
            '&#8220;Create me some more variations mixing screenshots and the diagetic ideas, 4 more seperate&#8221;']),
        block('The four', [
            'M1 Frame + Light trail, M2 Frame + Hero shot, M3 Frame + Diorama, M4 Frame + Chase cam.',
            'Each is the acting card from Rev 130 or 131 with a real frame of the game at the back of its picture, graded so the '
            'vessel and its act stay the brightest thing on the card.']),
        block('The frames', [
            'They are the same game frames S1 uses, real but not yet one per mode. A pick means capturing a frame per mode at '
            'intensity 2 (the look sheet, /unity-look), framed so its centre is where the act happens.']),
        block('In the game', [
            'One frame image per mode under the acting layer: a path or a scale, one hull sprite, one prop and a bloom. The frame '
            'moves by a transform only (push in, zoom, parallax), so hover costs no more than the acting card alone.',
            'Touch keeps the rest state, as every card does.']),
    ]
    body = ('<div class="abs" style="left:64px;top:56px;display:flex;flex-direction:column;gap:10px"><span class="lab">Rev %s notes</span>'
            '<span style="font-size:36px">Real frames with the vessel acting, four mixes</span></div>'
            '<div class="abs" style="left:64px;right:64px;top:170px;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));column-gap:56px">'
            % REV + ''.join(cols) + '</div>')
    return page(title, body, NOLOGIC, 1920, 900)


T = 'Rev %s' % REV
boards = {'Compare': compare('%s - Compare: Scarab Scramble, the Frame, the Diorama and the Hero shot, and four mixes' % T)}
for key, name, fn, line in MIX:
    boards[key] = option_board(key, name, fn, line, '%s %s - %s: the Arcade row and every state - interactive' % (T, key, name))
boards['Notes'] = notes('%s - notes: real frames with the vessel acting' % T)
for k, html in boards.items():
    with open(os.path.join(out, 'R%s%s.dc.html' % (REV, k)), 'w') as f:
        f.write(html)
print('written', out, sorted(boards))
