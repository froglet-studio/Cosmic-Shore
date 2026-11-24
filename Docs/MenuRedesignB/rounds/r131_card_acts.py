#!/usr/bin/env python3
"""Rev 131 - three new symbolic card pictures beside the two Shombith kept (2026-11-24).

Usage: python3 r131_card_acts.py <root> 131
Shombith on Rev 130: "keep the S1 frames but for the Y1 I only like Y3, change Y1,2 and 4 to make them more interesting
i dont like crest playbook pictogram at all not that interesting and does not give me the impression".

Kept as they were: S1 Frame and Y3 Diorama (r130_card_pictures.py). Pictogram, Playbook and Crest were flat signs: they
said what the mode is, not what it feels like. The three new ones are about the feel of the moment the vessel scores:
  Y5 Light trail - a long exposure: the play is already written across the card in light; hover flies it live.
  Y6 Chase cam - the view from behind the hull, the world rushing at it; hover throttles up into the act.
  Y7 Hero shot - the hull huge and close on speed rays; hover lunges into an impact frame with the word.
Every card is still the Poster with the short line (built in PR #2356). Writes R<rev>Compare, R<rev>Y5..Y7, R<rev>Notes.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, REV = sys.argv[1], sys.argv[2]
_src = open(os.path.join(HERE, 'r130_card_pictures.py')).read()
_src = _src[:_src.rindex("\nT = 'Rev %s' % REV")]
sys.argv = [sys.argv[0], root, REV]
exec(_src)  # everything r130 draws: skeleton, CARDS, ROW, PATHS, card_s1, card_y3, card_now, option_board, ...

HOV = ('.{o}:hover {s},.{o}.hv {s},.{o}.fc {s}')


def hov(o, sel):
    return ','.join(HOV.format(o=o, s=s.strip()) for s in sel.split(','))


# Where each act scores, in card units: the hull's path, the projectile's path (or None), the target, the word.
ACT = {
    'goal': (PATHS['goal'][0], PATHS['goal'][1], (286, 74), '+1'),
    'skim': (PATHS['skim'][0], None, (284, 42), 'First'),
    'shoot': (PATHS['shoot'][0], PATHS['shoot'][1], (268, 76), 'Hit'),
    'plough': (PATHS['plough'][0], None, (222, 118), 'Through'),
}
DUST = ''.join('<circle cx="%d" cy="%d" r="%.1f" fill="#B8C0F0" opacity="%.2f"/>'
               % ((k * 73) % 341, (k * 41) % 200 + 10, .8 + (k % 3) * .5, .2 + (k % 4) * .1) for k in range(30))

CSS3 = '''
@keyframes run5{0%%{offset-distance:0%%}62%%,100%%{offset-distance:100%%}}
@keyframes ink5{0%%{stroke-dashoffset:520}62%%,100%%{stroke-dashoffset:0}}
@keyframes ink5b{0%%,40%%{stroke-dashoffset:300}72%%,100%%{stroke-dashoffset:0}}
@keyframes bloom{0%%,62%%{opacity:0;transform:scale(.3)}70%%{opacity:1;transform:scale(1.25)}100%%{opacity:0;transform:scale(2)}}
@keyframes word{0%%,64%%{opacity:0;transform:translateY(6px) scale(.8)}72%%{opacity:1;transform:translateY(0) scale(1.1)}90%%{opacity:1;transform:translateY(-6px) scale(1)}100%%{opacity:0;transform:translateY(-10px)}}

/* ---- Y5 Light trail: a long exposure of the play; hover flies it live ---- */
.y5 .pic{background:radial-gradient(120%% 90%% at 80%% 10%%,rgba(67,76,137,.45) 0,rgba(0,4,31,0) 60%%),#00041F}
.y5 .glow{fill:none;stroke:#B8C0F0;stroke-width:12;stroke-linecap:round;opacity:.22;filter:url(#blur5)}
.y5 .core{fill:none;stroke:#E6E9FF;stroke-width:2.2;stroke-linecap:round}
.y5 .thin{fill:none;stroke:#E6E9FF;stroke-width:1.4;stroke-linecap:round;stroke-dasharray:3 7}
.y5 .ink{stroke-dasharray:520}
.y5 .inkb{stroke-dasharray:300}
.y5 .ves{offset-distance:100%%;width:100px;height:100px}
.y5 .tg{filter:drop-shadow(0 0 6px rgba(230,233,255,.9))}
.y5 .bl,.y6 .bl,.y7 .bl{position:absolute;width:90px;height:90px;margin:-45px 0 0 -45px;border-radius:50%%;opacity:0;
  background:radial-gradient(circle,rgba(230,233,255,.95) 0,rgba(184,192,240,.4) 35%%,rgba(0,4,31,0) 70%%);z-index:3}
.y5 .wd,.y6 .wd{position:absolute;font-family:'Aldrich',sans-serif;font-size:28px;color:#E6E9FF;opacity:0;z-index:4;white-space:nowrap}
%(h5ink)s{animation:ink5 2.6s cubic-bezier(.45,0,.2,1) infinite}
%(h5inkb)s{animation:ink5b 2.6s cubic-bezier(.45,0,.2,1) infinite}
%(h5ves)s{animation:run5 2.6s cubic-bezier(.45,0,.2,1) infinite}
%(h5bl)s{animation:bloom 2.6s ease-out infinite}
%(h5wd)s{animation:word 2.6s ease-out infinite}
%(h5sc)s{animation:scatter 2.6s ease-out infinite}

/* ---- Y6 Chase cam: behind the hull, the world rushing at it; hover throttles up ---- */
.y6 .pic{background:radial-gradient(60%% 50%% at 56%% 20%%,rgba(67,76,137,.55) 0,rgba(0,4,31,0) 70%%),#00041F}
.y6 .floor{position:absolute;left:-260px;right:-260px;top:50px;height:420px;transform-origin:50%% 0;
  transform:perspective(180px) rotateX(64deg);
  background:repeating-linear-gradient(0deg,rgba(67,76,137,.9) 0 2px,rgba(0,4,31,0) 2px 34px),
             repeating-linear-gradient(90deg,rgba(34,38,69,.95) 0 2px,rgba(0,4,31,0) 2px 40px);
  -webkit-mask-image:linear-gradient(180deg,rgba(0,0,0,0) 0,#000 40%%);mask-image:linear-gradient(180deg,rgba(0,0,0,0) 0,#000 40%%)}
@keyframes rush{from{background-position:0 0,0 0}to{background-position:0 34px,0 0}}
%(h6floor)s{animation:rush .32s linear infinite}
.y6 .lines line{stroke:#B8C0F0;stroke-width:1.4;stroke-dasharray:14 220;stroke-dashoffset:0;opacity:0}
@keyframes streak{0%%{stroke-dashoffset:40;opacity:0}20%%{opacity:.8}100%%{stroke-dashoffset:-200;opacity:0}}
%(h6lines)s{animation:streak .9s linear infinite}
.y6 .tgt{position:absolute;left:192px;top:50px;width:0;height:0;transform:scale(.34);z-index:2}
@keyframes approach{0%%{transform:scale(.34)}60%%{transform:scale(1)}70%%{transform:scale(1.15)}100%%{transform:scale(.34)}}
%(h6tgt)s{animation:approach 2.6s cubic-bezier(.6,0,.6,1) infinite}
.y6 .hero{position:absolute;left:122px;top:58px;width:124px;height:124px;z-index:3;
  filter:drop-shadow(0 14px 12px rgba(0,1,10,.95));animation:bob 3s ease-in-out infinite;animation-play-state:paused}
@keyframes bob{0%%,100%%{transform:translateY(0) rotate(0)}50%%{transform:translateY(-4px) rotate(-1.5deg)}}
@keyframes bank{0%%{transform:translate(0,0) rotate(0)}30%%{transform:translate(-10px,-2px) rotate(-8deg)}55%%{transform:translate(8px,-8px) rotate(5deg) scale(.94)}
  70%%{transform:translate(0,-4px) rotate(0) scale(.96)}100%%{transform:translate(0,0)}}
%(h6hero)s{animation:bank 2.6s ease-in-out infinite}
.y6 .proj{position:absolute;left:0;top:0;z-index:2;opacity:0}
@keyframes tovp{0%%,28%%{opacity:0;transform:translate(180px,150px) scale(1)}34%%{opacity:1}60%%{opacity:1;transform:translate(192px,52px) scale(.3)}62%%,100%%{opacity:0;transform:translate(192px,52px) scale(.3)}}
%(h6proj)s{animation:tovp 2.6s cubic-bezier(.3,0,.4,1) infinite}
%(h6bl)s{animation:bloom 2.6s ease-out infinite}
%(h6wd)s{animation:word 2.6s ease-out infinite}
%(h6sc)s{animation:scatter 2.6s ease-out infinite}

/* ---- Y7 Hero shot: the hull huge on speed rays; hover lunges into the impact frame ---- */
.y7 .pic{background:repeating-conic-gradient(from 0deg at 30%% 46%%,rgba(67,76,137,.34) 0 5deg,rgba(0,4,31,0) 5deg 13deg),
  radial-gradient(70%% 70%% at 30%% 46%%,rgba(67,76,137,.5) 0,rgba(0,4,31,0) 70%%),#00041F}
.y7 .rays{position:absolute;inset:-120px;background:repeating-conic-gradient(from 3deg at 42%% 46%%,rgba(184,192,240,.16) 0 1.5deg,rgba(0,4,31,0) 1.5deg 17deg)}
@keyframes spin{to{transform:rotate(360deg)}}
.y7 .rays{animation:spin 60s linear infinite;animation-play-state:paused}
%(h7rays)s{animation-play-state:running;animation-duration:4s}
.y7 .big{position:absolute;left:-34px;top:-26px;width:236px;height:236px;z-index:2;transform:rotate(-12deg);
  filter:drop-shadow(0 18px 16px rgba(0,1,10,.95))}
@keyframes lunge{0%%{transform:rotate(-12deg) translate(0,0) scale(1)}40%%{transform:rotate(-14deg) translate(-14px,6px) scale(.96)}
  56%%{transform:rotate(-8deg) translate(46px,-16px) scale(1.08)}80%%{transform:rotate(-10deg) translate(40px,-12px) scale(1.06)}100%%{transform:rotate(-12deg) translate(0,0) scale(1)}}
%(h7big)s{animation:lunge 2.6s cubic-bezier(.5,0,.2,1) infinite}
.y7 .tg7{position:absolute;z-index:2}
.y7 .zip line{stroke:#E6E9FF;stroke-width:2;opacity:0}
@keyframes zip{0%%,40%%{opacity:0;transform:translateX(30px)}50%%{opacity:.8}62%%,100%%{opacity:0;transform:translateX(-40px)}}
%(h7zip)s{animation:zip 2.6s linear infinite}
.y7 .burst{position:absolute;left:0;top:0;width:150px;height:120px;margin:-60px 0 0 -75px;z-index:3;opacity:0;transform:scale(.3)}
@keyframes burst{0%%,52%%{opacity:0;transform:scale(.3) rotate(-8deg)}58%%{opacity:1;transform:scale(1.08) rotate(-8deg)}84%%{opacity:1;transform:scale(1) rotate(-8deg)}100%%{opacity:0;transform:scale(1.1) rotate(-8deg)}}
%(h7burst)s{animation:burst 2.6s ease-out infinite}
.y7 .burst span{position:absolute;left:50%%;top:50%%;transform:translate(-50%%,-50%%);padding:0 4px;background:#E6E9FF;font-family:'Aldrich',sans-serif;font-size:28px;line-height:1;color:#00041F;white-space:nowrap}
@keyframes shake{0%%,54%%{transform:translate(0,0)}56%%{transform:translate(-4px,3px)}58%%{transform:translate(4px,-3px)}60%%{transform:translate(-3px,-2px)}62%%,100%%{transform:translate(0,0)}}
%(h7shake)s{animation:shake 2.6s linear infinite}
%(h7sc)s{animation:scatter 2.6s ease-out infinite}

.lk .acts{filter:grayscale(1) brightness(.5)}
.lk .rays,.lk .floor,.lk .hero,.lk .big{animation:none!important}
''' % {
    'h5ink': hov('y5', '.ink'), 'h5inkb': hov('y5', '.inkb'), 'h5ves': hov('y5', '.ves'), 'h5bl': hov('y5', '.bl'),
    'h5wd': hov('y5', '.wd'), 'h5sc': hov('y5', '.sc'),
    'h6floor': hov('y6', '.floor'), 'h6lines': hov('y6', '.lines line'), 'h6tgt': hov('y6', '.tgt'),
    'h6hero': hov('y6', '.hero'), 'h6proj': hov('y6', '.proj'), 'h6bl': hov('y6', '.bl'), 'h6wd': hov('y6', '.wd'),
    'h6sc': hov('y6', '.sc'),
    'h7rays': hov('y7', '.rays'), 'h7big': hov('y7', '.big'), 'h7zip': hov('y7', '.zip line'),
    'h7burst': hov('y7', '.burst'), 'h7shake': hov('y7', '.acts'), 'h7sc': hov('y7', '.sc'),
}
STYLE += CSS3


def blocks(x0, y0, cls='sc', n=10, colour='#E6E9FF', glow=False):
    """The hedge the Rhino ploughs: n small panes in two staggered columns, each flying off its own way."""
    out = []
    for k in range(n):
        x, y = x0 + (k % 2) * 16, y0 + k * 11
        out.append('<i class="%s" style="position:absolute;left:%dpx;top:%dpx;width:14px;height:10px;border:1.5px solid %s;%s'
                   '--dx:%dpx;--dy:%dpx;--r:%ddeg"></i>'
                   % (cls, x, y, colour, 'box-shadow:0 0 6px rgba(230,233,255,.8);' if glow else '',
                      40 + (k * 7) % 30, (k - 5) * 9, (k * 47) % 180))
    return ''.join(out)


def ring(cx, cy, s=1.0, glow=False):
    f = ' filter="url(#blur5)"' if glow else ''
    return ('<ellipse cx="%d" cy="%d" rx="%g" ry="%g" fill="none" stroke="#B8C0F0" stroke-width="8" opacity=".25"%s/>'
            '<ellipse cx="%d" cy="%d" rx="%g" ry="%g" fill="none" stroke="#E6E9FF" stroke-width="3"/>'
            % (cx, cy, 20 * s, 36 * s, f, cx, cy, 20 * s, 36 * s))


def crystal(cx, cy, s=1.0):
    p = '%g,%g %g,%g %g,%g %g,%g' % (cx, cy - 18 * s, cx + 14 * s, cy, cx, cy + 18 * s, cx - 14 * s, cy)
    q = '%g,%g %g,%g %g,%g' % (cx - 14 * s, cy, cx, cy - 18 * s, cx, cy + 18 * s)
    return ('<polygon points="%s" fill="rgba(184,192,240,.25)" stroke="#E6E9FF" stroke-width="2.5"/>'
            '<polygon points="%s" fill="rgba(230,233,255,.35)"/>' % (p, q))


# ------------------------------------------------------------------ Y5 Light trail
def card_y5(m, st=''):
    d = CARDS[m]
    a, b, (tx, ty), word = ACT[d['act']]
    svg = ['<svg class="abs" width="341" height="296" style="left:0;top:0" aria-hidden="true">'
           '<defs><filter id="blur5" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="4"/></filter></defs>',
           DUST,
           '<path class="glow ink" d="%s"/><path class="core ink" d="%s"/>' % (a, a)]
    if b and d['act'] == 'goal':
        svg.append('<path class="glow inkb" d="%s" style="stroke-width:7"/><path class="core inkb" d="%s" style="stroke-width:1.6"/>' % (b, b))
    if b and d['act'] == 'shoot':
        for k, off in enumerate((-6, 0, 6)):
            svg.append('<path class="core inkb" d="M 148 %d L 240 %d" style="stroke-width:1.6;opacity:%.2f"/>' % (106 + off, 76 + off, .6 + k * .2))
    if d['act'] == 'goal':
        svg.append('<g class="tg">%s</g>' % ring(tx, ty))
        svg.append('<circle cx="%d" cy="%d" r="7" fill="#E6E9FF"/>' % (tx - 8, ty + 4))
    if d['act'] == 'skim':
        svg.append('<g class="tg">%s</g>' % crystal(tx, ty))
    svg.append('</svg>')
    extra = ''
    if d['act'] == 'shoot':
        extra = ('<img class="hl" src="%s" alt="Rival" style="position:absolute;left:236px;top:44px;width:64px;height:64px;'
                 'transform:scaleX(-1);filter:brightness(0) invert(.92) drop-shadow(0 0 6px rgba(230,233,255,.9))">' % HULL['Sparrow'])
    if d['act'] == 'plough':
        extra = blocks(214, 64, glow=True)
    acts = ('<div class="acts">%s%s<img class="ves hl" src="%s" alt="%s" style="offset-path:path(\'%s\')">'
            '<i class="bl" style="left:%dpx;top:%dpx"></i><span class="wd" style="left:%dpx;top:%dpx">%s</span></div>'
            % (''.join(svg), extra, HULL[d['hull']], d['hull'], a, tx, ty, max(tx - 70, 150), max(ty - 64, 4), word))
    return skeleton('y y5', m, st, '', acts=acts)


# ------------------------------------------------------------------ Y6 Chase cam
VP = (192, 50)


def card_y6(m, st=''):
    d = CARDS[m]
    act, word = d['act'], ACT[d['act']][3]
    import math
    lines = ''.join('<line x1="%d" y1="%d" x2="%d" y2="%d" style="animation-delay:-%.2fs"/>'
                    % (VP[0], VP[1], VP[0] + 420 * math.cos(t), VP[1] + 420 * math.sin(t), (k * .37) % .9)
                    for k, t in enumerate([math.radians(a) for a in (-170, -150, -128, -100, -70, -40, -18, 6, 24, 150, 168, 196, 210)]))
    pic = ('<div class="floor"></div>'
           '<svg class="abs lines" width="341" height="296" style="left:0;top:0" aria-hidden="true">%s</svg>' % lines)
    if act == 'goal':
        tgt = '<svg class="abs" width="120" height="160" style="left:-60px;top:-80px" aria-hidden="true">%s</svg>' % ring(60, 80, 1.1)
        proj = '<i class="proj" style="width:22px;height:22px;margin:-11px 0 0 -11px;border-radius:50%;background:#E6E9FF;box-shadow:0 0 10px rgba(230,233,255,.9)"></i>'
    elif act == 'skim':
        tgt = '<svg class="abs" width="120" height="120" style="left:-60px;top:-60px" aria-hidden="true">%s</svg>' % crystal(60, 60, 2.2)
        proj = ''
        pic += ('<svg class="abs" width="341" height="296" style="left:0;top:0" aria-hidden="true">'
                '<path d="M 180 300 L %d %d" stroke="#B8C0F0" stroke-width="6" opacity=".35"/>'
                '<path d="M 180 300 L %d %d" stroke="#E6E9FF" stroke-width="2"/></svg>' % (VP[0], VP[1], VP[0], VP[1]))
    elif act == 'shoot':
        tgt = ('<img class="hl" src="%s" alt="Rival" style="position:absolute;left:-60px;top:-50px;width:120px;height:100px;transform:scaleX(-1)">'
               % HULL['Sparrow'])
        proj = ''.join('<i class="proj" style="width:6px;height:16px;margin:-8px 0 0 %dpx;background:#E6E9FF;box-shadow:0 0 8px rgba(230,233,255,.9);'
                       'animation-delay:%.2fs"></i>' % (off, k * .1) for k, off in enumerate((-14, 0, 14)))
    else:
        tgt = '<div style="position:absolute;left:-40px;top:-60px;width:80px;height:120px">%s</div>' % ''.join(
            '<i class="sc" style="position:absolute;left:%dpx;top:%dpx;width:22px;height:14px;border:2px solid #E6E9FF;'
            'box-shadow:0 0 6px rgba(230,233,255,.6);--dx:%dpx;--dy:%dpx;--r:%ddeg"></i>'
            % ((k % 3) * 26, (k // 3) * 20, ((k % 3) - 1) * 90, ((k // 3) - 2) * 40 + 20, (k * 53) % 200) for k in range(15))
        proj = ''
    acts = ('<div class="acts"><div class="tgt">%s</div>%s'
            '<img class="hero hl" src="%s" alt="%s">'
            '<i class="bl" style="left:%dpx;top:%dpx"></i><span class="wd" style="left:%dpx;top:14px">%s</span></div>'
            % (tgt, proj, HULL[d['hull']], d['hull'], VP[0], VP[1], VP[0] + 34, word))
    return skeleton('y y6', m, st, pic, acts=acts)


# ------------------------------------------------------------------ Y7 Hero shot
BURST = '50,0 61,30 95,12 78,44 120,50 80,66 104,100 64,84 54,120 42,86 6,104 26,70 0,50 30,40 12,8 42,28'


def card_y7(m, st=''):
    d = CARDS[m]
    act, word = d['act'], {'goal': 'GOAL', 'skim': 'FIRST', 'shoot': 'HIT', 'plough': 'THROUGH'}[d['act']]
    tx, ty = 262, 70
    if act == 'goal':
        tg = ('<svg class="tg7" width="120" height="160" style="left:%dpx;top:%dpx" aria-hidden="true">%s</svg>'
              '<i class="tg7" style="left:%dpx;top:%dpx;width:16px;height:16px;border-radius:50%%;background:#E6E9FF"></i>'
              % (tx - 60, ty - 80, ring(60, 80, 1.3), tx - 70, ty + 22))
    elif act == 'skim':
        tg = '<svg class="tg7" width="120" height="120" style="left:%dpx;top:%dpx" aria-hidden="true">%s</svg>' % (tx - 60, ty - 60, crystal(60, 60, 1.8))
    elif act == 'shoot':
        tg = ('<img class="tg7 hl" src="%s" alt="Rival" style="left:%dpx;top:%dpx;width:84px;height:84px;transform:scaleX(-1)">'
              % (HULL['Sparrow'], tx - 42, ty - 42))
    else:
        tg = '<div class="tg7" style="left:%dpx;top:%dpx;width:60px;height:120px">%s</div>' % (tx - 20, ty - 56, blocks(0, 0, n=10))
    zips = ''.join('<line x1="%d" y1="%d" x2="%d" y2="%d" style="animation-delay:%.2fs"/>' % (x, y, x + 70, y, (k % 3) * .04)
                   for k, (x, y) in enumerate([(150, 40), (170, 70), (140, 100), (180, 128), (160, 150)]))
    burst = ('<div class="burst" style="left:%dpx;top:%dpx"><svg width="150" height="120" viewBox="0 0 120 120" preserveAspectRatio="none" aria-hidden="true">'
             '<polygon points="%s" fill="#E6E9FF"/></svg><span>%s</span></div>' % (tx - 10, ty + 10, BURST, word))
    acts = ('<div class="acts">%s<svg class="abs zip" width="341" height="296" style="left:0;top:0" aria-hidden="true">%s</svg>'
            '<img class="big hl" src="%s" alt="%s">%s</div>' % (tg, zips, HULL[d['hull']], d['hull'], burst))
    return skeleton('y y7', m, st, '<div class="rays"></div>', acts=acts)


NEW = [
    ('Y5', 'Light trail', card_y5, 'A long exposure: the play is already written across the card in light, the hull at the end of it. '
     'Hover erases it and flies it live, and the target blooms.'),
    ('Y6', 'Chase cam', card_y6, 'The view from behind the hull, the target far ahead. Hover throttles up: the floor rushes, the streaks '
     'fly past, the target looms and the hull scores.'),
    ('Y7', 'Hero shot', card_y7, 'The hull huge and close on speed rays, the target in its sights. Hover: it winds up, lunges, and the '
     'card shakes on an impact frame with the word.'),
]


def compare(title):
    cells = [('Now', 'The Poster as built', card_now), ('S1', 'Frame (kept)', card_s1), ('Y3', 'Diorama (kept)', card_y3)] + \
            [(k, n, f) for k, n, f, _ in NEW]
    pos = [(64 + c * 400, 172) for c in range(3)] + [(64 + c * 400, 602) for c in range(3)]
    cells_html = []
    for (x, y), (key, name, fn) in zip(pos, cells):
        cells_html.append('<div class="abs" style="left:%dpx;top:%dpx;width:341px">'
                          '<div style="display:flex;gap:12px;align-items:baseline;margin-bottom:16px"><span class="lab"%s>%s</span>'
                          '<span style="font-size:18px">%s</span></div>%s</div>'
                          % (x, y, ' style="color:#E6E9FF"' if key in ('Y5', 'Y6', 'Y7') else '', key, name, fn('Scarab Scramble')))
    legend = ('<div class="abs" style="left:1300px;top:172px;width:520px;display:flex;flex-direction:column;gap:14px">'
              '<span class="lab" style="color:#E6E9FF">Read me</span>'
              '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">Scarab Scramble at the in-game size. The top row is the Poster as '
              'built and the two you kept from Rev 130. The bottom row is three new symbolic pictures in place of Pictogram, Playbook and '
              'Crest.</span>'
              '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">Those three were signs: they said what the mode is. The new ones '
              'are about the feel of the moment the vessel scores: a long exposure of the play, the view from behind the hull, and an '
              'impact frame.</span>'
              '<span style="font-size:14px;line-height:1.5;color:#747BAD">Point at a card. Each new one has its own board with four '
              'modes and every state.</span></div>')
    body = (BACKDROP + HUB +
            '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,4,31,.95)"></div>'
            '<div class="abs" style="left:64px;top:100px;display:flex;align-items:baseline;gap:22px"><span style="font-size:28px">'
            'Symbolic, three new ways</span><span class="lab">Beside the Frame and the Diorama</span></div>'
            + ''.join(cells_html) + legend)
    return page(title, body, NOLOGIC)


def notes(title):
    def block(head, lines):
        return ('<div style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">%s</span>'
                + ''.join('<span style="font-size:15px;line-height:1.55;color:#B8C0F0">%s</span>' % l for l in lines) + '</div>') % head
    cols = [
        block('Asked (Shombith, 2026-11-24)', [
            '&#8220;keep the S1 frames but for the Y1 I only like Y3, change Y1,2 and 4 to make them more interesting i dont like '
            'crest playbook pictogram at all not that interesting and does not give me the impression&#8221;']),
        block('What changed', [
            'S1 Frame and Y3 Diorama stay as drawn in Rev 130. Pictogram, Playbook and Crest are gone.',
            'They were flat signs of the rules. The three new ones show the feel of the moment the vessel scores, each with the '
            'real hull and its own way of moving.']),
        block('The three', [
            'Y5 Light trail: a long exposure of the play at rest; hover flies it live and the target blooms.',
            'Y6 Chase cam: behind the hull, the world rushing at it; the target looms and the hull scores.',
            'Y7 Hero shot: the hull huge on speed rays; it lunges, the card shakes on an impact frame with the word.']),
        block('In the game', [
            'Each is a small UI animation per mode: a path or a scale, one hull sprite, one prop and a bloom, playing only on the '
            'hovered or focused card. Y6&#8217;s floor is one scrolling texture; Y7&#8217;s rays one rotating image.',
            'Touch keeps the rest state, as every card does.']),
    ]
    body = ('<div class="abs" style="left:64px;top:56px;display:flex;flex-direction:column;gap:10px"><span class="lab">Rev %s notes</span>'
            '<span style="font-size:36px">Symbolic, three new ways</span></div>'
            '<div class="abs" style="left:64px;right:64px;top:170px;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));column-gap:56px">'
            % REV + ''.join(cols) + '</div>')
    return page(title, body, NOLOGIC, 1920, 900)


T = 'Rev %s' % REV
boards = {'Compare': compare('%s - Compare: Scarab Scramble, the Frame and the Diorama kept, three new symbolic pictures' % T)}
for key, name, fn, line in NEW:
    boards[key] = option_board(key, name, fn, line, '%s %s - %s: the Arcade row and every state - interactive' % (T, key, name))
boards['Notes'] = notes('%s - notes: three new symbolic pictures' % T)
for k, html in boards.items():
    with open(os.path.join(out, 'R%s%s.dc.html' % (REV, k)), 'w') as f:
        f.write(html)
print('written', out, sorted(boards))
