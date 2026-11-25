#!/usr/bin/env python3
"""Rev 145 - Toy configure's variation panel, three ways, on Rev 144 G (2026-11-24).

Usage: python3 r145_toy_variations.py <root> 145
Shombith picked Rev 144 G (D's Toy Box, A's Configure, the 2D toy glyphs with depth) and pointed at the variation
tiles: "for this layout for the cards I want you to change that. Make this cool too." Three answers for the panel on
the right of Configure, everything else Rev 144 G:
  H Shelf     big tiles, the hull or creature standing in a spotlight, the name at the foot; hover lifts it
  I Slide     a list whose picked row slides open into a big card with the render (Rev 117's "slide open")
  J Pedestal  the pick stands large on a lit pedestal at the top; the variations are a row of round chips under it
And Today's activity big again over the grid shrunk to 0.84 ("I liked the top place where the todays card use to
show up big"): H the banner, I the clock ring and a turning crystal, J the live strip where the shark swims across.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, REV = sys.argv[1], sys.argv[2]
_src = open(os.path.join(HERE, 'r144_toybox_glyphs.py')).read()
_src = _src[:_src.index('import json\nimport re')]
sys.argv = [sys.argv[0], root, REV]
exec(_src)
for t, i in zip(TOYS, DEEP):
    t['art'] = blob(i)

IMG = ('<sc-if value="{{r.hasImg}}" hint-placeholder-val="{{ true }}"><img class="%s" src="{{r.img}}" alt=""></sc-if>'
       '<sc-if value="{{r.hasSw}}" hint-placeholder-val="{{ false }}"><span class="%s-sw dia" style="background:{{r.sw}}"></span></sc-if>')
SUB = '<span class="lab ell" style="font-size:12px;color:{{r.dc}}">{{r.d}}</span>'

COMMON = '''
.vx{box-sizing:border-box;position:relative;border:0;cursor:pointer;color:#E6E9FF;font-family:'Aldrich',sans-serif;text-align:left;overflow:hidden;
 background:radial-gradient(ellipse 80% 60% at 50% 40%,rgba(230,233,255,.06),rgba(230,233,255,0) 70%),linear-gradient(180deg,rgba(18,20,38,.7),rgba(0,1,10,.8));
 box-shadow:inset 0 0 0 1px #222645;transition:box-shadow .25s,transform .3s cubic-bezier(.2,.8,.2,1)}
.vx:focus-visible{outline:2px solid #00D4FF;outline-offset:2px}
.vx .sweep{position:absolute;top:0;bottom:0;width:70px;left:-90px;background:linear-gradient(90deg,rgba(230,233,255,0),rgba(230,233,255,.12),rgba(230,233,255,0));transform:skewX(-14deg);pointer-events:none}
.vx:hover .sweep{animation:aSweep .9s ease}
.vx.on{box-shadow:inset 0 0 0 2px #E6E9FF}
'''

# ---------------- H Shelf
H_CSS = COMMON + '''
.h-t{height:210px;animation:tbRise .45s calc(var(--dl)*1ms) cubic-bezier(.2,.8,.2,1) both}
.h-t:hover{transform:translateY(-4px)}
.h-img{position:absolute;left:50%;top:30px;width:250px;height:110px;margin-left:-125px;object-fit:contain;transition:transform .45s cubic-bezier(.2,.8,.2,1);filter:drop-shadow(0 14px 14px rgba(0,1,10,.7))}
.h-t:hover .h-img,.h-t.on .h-img{transform:translateY(-6px) scale(1.12)}
.h-floor{position:absolute;left:18%;right:18%;top:140px;height:10px;border-radius:50%;background:radial-gradient(ellipse,rgba(230,233,255,.22),rgba(230,233,255,0) 70%)}
.h-t.on .h-floor{background:radial-gradient(ellipse,rgba(230,233,255,.5),rgba(230,233,255,0) 70%)}
.h-sw-sw{position:absolute;left:50%;top:60px;width:40px;height:40px;margin-left:-20px}
.h-words{position:absolute;left:18px;right:18px;bottom:14px;display:flex;justify-content:space-between;align-items:baseline}
'''


def h_tiles(*a, **k):
    return ('<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px">'
            '<sc-for list="{{rows}}" as="r" hint-placeholder-count="6">'
            '<button class="vx h-t {{r.cls}}" type="button" onClick="{{r.pick}}" style="--dl:{{r.dl}}">'
            '<span class="h-floor"></span>' + IMG % ('h-img', 'h') +
            '<span class="h-words"><span class="ell" style="font-size:18px">{{r.n}}</span>' + SUB + '</span>'
            '<span class="sweep"></span></button></sc-for></div>')


# ---------------- I Slide
I_CSS = COMMON + '''
.i-t{height:58px;display:flex;align-items:center;gap:16px;padding:0 18px;animation:tbRise .4s calc(var(--dl)*1ms) cubic-bezier(.2,.8,.2,1) both}
.i-t:hover{transform:translateX(6px)}
.i-ico{width:64px;height:36px;object-fit:contain;flex:none}
.i-ico-sw,.i-big-sw{width:18px;height:18px;margin:0 23px;flex:none}
.i-t.on{height:196px;align-items:flex-end;padding:0 22px 18px;animation:iOpen .55s cubic-bezier(.3,.9,.3,1) both}
@keyframes iOpen{from{height:58px}to{height:196px}}
.i-t.on .i-ico{position:absolute;left:50%;top:18px;width:420px;height:120px;margin-left:-210px;animation:iFly .6s .1s cubic-bezier(.2,.8,.2,1) both;filter:drop-shadow(0 16px 16px rgba(0,1,10,.7))}
@keyframes iFly{from{transform:translateX(-120px) scale(.4);opacity:0}to{transform:none;opacity:1}}
.i-t.on .i-name{font-size:28px}
.i-t.on .i-ico-sw{position:absolute;left:50%;top:50px;width:56px;height:56px;margin-left:-28px}
'''


def i_tiles(*a, **k):
    return ('<div style="display:flex;flex-direction:column;gap:8px">'
            '<sc-for list="{{rows}}" as="r" hint-placeholder-count="6">'
            '<button class="vx i-t {{r.cls}}" type="button" onClick="{{r.pick}}" style="--dl:{{r.dl}}">' + IMG % ('i-ico', 'i-ico') +
            '<span style="display:flex;flex-direction:column;gap:4px;min-width:0;position:relative"><span class="ell i-name" style="font-size:18px">{{r.n}}</span>' + SUB + '</span>'
            '<span class="sweep"></span></button></sc-for></div>')


# ---------------- J Pedestal
J_CSS = COMMON + '''
.j-stage{position:relative;height:300px;overflow:hidden;box-shadow:inset 0 0 0 1px #222645;background:radial-gradient(ellipse 70% 55% at 50% 72%,rgba(230,233,255,.1),rgba(230,233,255,0) 70%),linear-gradient(180deg,rgba(18,20,38,.6),rgba(0,1,10,.85))}
.j-ped{position:absolute;left:50%;top:226px;width:360px;height:44px;margin-left:-180px;border-radius:50%;box-shadow:0 0 0 1px rgba(230,233,255,.35),inset 0 0 30px rgba(230,233,255,.12);animation:jPed .6s ease both}
@keyframes jPed{from{transform:scaleX(.3);opacity:0}to{transform:none;opacity:1}}
.j-hero{position:absolute;left:50%;top:40px;width:520px;height:180px;margin-left:-260px;object-fit:contain;filter:drop-shadow(0 26px 20px rgba(0,1,10,.8));animation:jDrop .7s cubic-bezier(.3,1.3,.5,1) both}
@keyframes jDrop{from{transform:translateY(-60px) scale(.7);opacity:0}to{transform:none;opacity:1}}
.j-name{position:absolute;left:24px;top:18px;font-size:30px}
.j-chips{display:flex;gap:14px;justify-content:space-between}
.j-c{width:90px;height:116px;display:flex;flex-direction:column;align-items:center;gap:8px;padding-top:10px;background:none;box-shadow:none}
.j-c .j-o{position:relative;width:72px;height:72px;border-radius:50%;display:flex;align-items:center;justify-content:center;box-shadow:inset 0 0 0 1px #434C89;background:rgba(0,1,10,.6);transition:transform .3s cubic-bezier(.2,.8,.2,1),box-shadow .3s}
.j-c:hover .j-o{transform:translateY(-6px)}
.j-c.on{box-shadow:none}
.j-c.on .j-o{box-shadow:inset 0 0 0 2px #E6E9FF,0 0 0 6px rgba(230,233,255,.08);transform:translateY(-6px)}
.j-ico{width:56px;height:40px;object-fit:contain}
.j-ico-sw{width:22px;height:22px}
.j-c .ell{max-width:90px;text-align:center}
'''


def j_tiles(*a, **k):
    hero = ('<img class="j-hero" src="{{pv.img}}" alt="">')
    return ('<div class="j-stage"><span class="j-ped"></span>'
            '<sc-if value="{{pvA}}" hint-placeholder-val="{{ true }}">' + hero + '</sc-if>'
            '<sc-if value="{{pvB}}" hint-placeholder-val="{{ false }}">' + hero + '</sc-if>'
            '<span class="j-name">{{pv.name}}</span></div>'
            '<div class="j-chips"><sc-for list="{{rows}}" as="r" hint-placeholder-count="6">'
            '<button class="vx j-c {{r.cls}}" type="button" onClick="{{r.pick}}" style="animation:tbRise .4s {{r.dl}}ms both">'
            '<span class="j-o">' + IMG % ('j-ico', 'j-ico') + '</span>'
            '<span class="ell" style="font-size:15px">{{r.n}}</span></button></sc-for></div>')



# ---------------- Today's activity, big again (Shombith: "I liked the top place where the todays card use to show up
# big, can you do some variation here as well"). The seven cards shrink to 0.84 under it; one Today per board.
SHARK = blob('90c8aebc77d4efba0d2bd00eae7e0046')
SPAWN3D = blob('89188e129eefb17e5d8ed19c55627126')
K, TX, TY = 0.84, 153.6, 221.8
_d_box = d_box


def d_box():
    b = _d_box()
    b = b[:b.index('<div class="abs tb-fade"')]
    return ('<div class="abs" style="left:0;top:0;width:1920px;height:1080px;transform:translate(%spx,%spx) scale(%s);transform-origin:0 0">%s</div>'
            % (TX, TY, K, b))


OPT_E['from'] = [[round(K * (x + D_CARD[0] / 2) + TX - (A_PV[0] + A_PV[2] / 2)), round(K * (y + D_CARD[1] / 2) + TY - (A_PV[1] + A_PV[3] / 2)),
                  round(K * D_CARD[0] / A_PV[2], 3)] for x, y in D_POS]

TITLE = ('<div class="abs" style="left:64px;top:160px;display:flex;align-items:baseline;gap:22px;z-index:4">'
         '<span style="font-size:28px">Toy Box</span><span class="lab">7 toys</span></div>')
CTA = '<button class="btn cta" type="button" style="width:240px"><span class="kc">A</span>Try it &#160;+10</button>'
T_CSS = """
.td{position:absolute;left:64px;top:216px;width:1792px;height:178px;overflow:hidden;box-shadow:inset 0 0 0 1px #434C89;animation:tbRise .6s both}
.td .bgw{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;object-position:50% 60%;opacity:.5}
.td .shade{position:absolute;inset:0;background:linear-gradient(90deg,rgba(0,1,10,.92) 0%,rgba(0,1,10,.7) 45%,rgba(0,1,10,.2) 75%,rgba(0,1,10,.75) 100%)}
.td .sw8{position:absolute;top:0;bottom:0;width:140px;left:-200px;background:linear-gradient(90deg,rgba(230,233,255,0),rgba(230,233,255,.1),rgba(230,233,255,0));transform:skewX(-14deg);animation:tdSweep 7s 1s ease-in-out infinite}
@keyframes tdSweep{0%{left:-200px}25%{left:1900px}100%{left:1900px}}
.td .lens{position:absolute;left:28px;top:23px;width:132px;height:132px;border-radius:50%;overflow:hidden;box-shadow:inset 0 0 0 1px #747BAD;background:rgba(0,1,10,.7)}
.td .lens img{position:absolute;inset:-10px;width:152px;height:152px;object-fit:contain}
.td .eb{position:absolute;left:192px;top:34px}
.td .hl{position:absolute;left:192px;top:62px;font-size:38px}
.td .sub{position:absolute;left:192px;top:118px;display:flex;gap:22px;align-items:center}
.td .go{position:absolute;right:28px;top:61px}
.ring{position:absolute;left:20px;top:15px;width:148px;height:148px;transform:rotate(-90deg)}
.ring circle{fill:none;stroke-width:3}
.ring .t{stroke:#222645}.ring .f{stroke:#E6E9FF;stroke-dasharray:452;stroke-dashoffset:452;animation:ringIn 1.4s .3s cubic-bezier(.2,.8,.2,1) forwards}
@keyframes ringIn{to{stroke-dashoffset:136}}
.rew{position:absolute;right:300px;top:40px;display:flex;flex-direction:column;align-items:center;gap:10px}
.rew .dia{width:46px;height:46px;box-shadow:inset 0 0 0 2px #E6E9FF;background:rgba(230,233,255,.08);animation:rewSpin 6s linear infinite}
@keyframes rewSpin{from{transform:rotate(45deg)}to{transform:rotate(405deg)}}
.swim{position:absolute;top:10px;left:0;width:200px;height:160px;object-fit:contain;animation:swim 14s linear infinite;filter:drop-shadow(0 12px 12px rgba(0,1,10,.7))}
@keyframes swim{0%{transform:translateX(1900px) translateY(0)}25%{transform:translateX(1400px) translateY(-14px)}50%{transform:translateX(900px) translateY(8px)}75%{transform:translateX(400px) translateY(-10px)}100%{transform:translateX(-260px) translateY(0)}}
.chip{position:absolute;left:28px;top:23px;width:132px;height:132px;display:flex;align-items:center;justify-content:center;box-shadow:inset 0 0 0 1px #747BAD;background:rgba(0,1,10,.7)}
.chip img{width:116px;height:116px;object-fit:contain}
"""
TXT = ('<span class="lab eb">Today&#8217;s activity &#160;&#183;&#160; Spawn Matrix</span>'
       '<span class="hl">Spawn a Shark</span>')
TODAY = {
    'H': ('<div class="td"><img class="bgw" src="%s" alt=""><div class="shade"></div><span class="sw8"></span>'
          '<span class="lens"><img src="%s" alt=""></span>' % (WORLD, SHARK) + TXT +
          '<span class="sub"><span class="lab" style="color:#E6E9FF">Ends in 7h 12m</span><span class="lab">+10 crystals</span></span>'
          '<span class="go">' + CTA + '</span></div>'),
    'I': ('<div class="td"><img class="bgw" src="%s" alt=""><div class="shade"></div>'
          '<svg class="ring" viewBox="0 0 148 148" aria-hidden="true"><circle class="t" cx="74" cy="74" r="72"></circle><circle class="f" cx="74" cy="74" r="72"></circle></svg>'
          '<span class="lens"><img src="%s" alt=""></span>' % (WORLD, SHARK) + TXT +
          '<span class="sub"><span class="lab" style="color:#E6E9FF">7h 12m left today</span></span>'
          '<span class="rew"><span class="dia"></span><span style="font-size:22px">+10</span></span>'
          '<span class="go">' + CTA + '</span></div>'),
    'J': ('<div class="td"><img class="bgw" src="%s" alt="" style="opacity:.35"><img class="swim" src="%s" alt="">'
          '<div class="shade" style="background:linear-gradient(90deg,rgba(0,1,10,.9) 0%%,rgba(0,1,10,.55) 40%%,rgba(0,1,10,0) 70%%,rgba(0,1,10,.6) 100%%)"></div>'
          '<span class="chip"><img src="%s" alt=""></span>' % (WORLD, SHARK, SPAWN3D) + TXT +
          '<span class="sub"><span class="lab" style="color:#E6E9FF">Ends in 7h 12m</span><span class="lab">+10 crystals</span></span>'
          '<span class="go">' + CTA + '</span></div>'),
}

import json
import re
os.makedirs(out, exist_ok=True)
PLANNED = json.load(open(os.path.join(out, 'canvas.json')))['boards']
files = {}
for key, fn, css in (('H', h_tiles, H_CSS), ('I', i_tiles, I_CSS), ('J', j_tiles, J_CSS)):
    tiles = fn
    HEAD = TITLE + TODAY[key]
    files['R%s%s.dc.html' % (REV, key)] = board('x', D_CSS + A_CSS + G_CSS + T_CSS + css, d_box(), a_cfg(), a_dive(), a_world(), OPT_E)
for name, html in files.items():
    html = re.sub(r'<title>.*?</title>', lambda m: '<title>' + PLANNED[name]['title'].replace('&', '&amp;') + '</title>', html, count=1)
    open(os.path.join(out, name), 'w').write(html)
    print('wrote', name, len(html))
