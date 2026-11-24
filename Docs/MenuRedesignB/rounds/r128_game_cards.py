#!/usr/bin/env python3
"""Rev 128 - Game cards, six ways (2026-11-23): Skim Race's arcade card beside the card we ship.

Usage: python3 r128_game_cards.py <root> 128
Writes R127Compare, A-F and Notes into <root>/project. The kit (helmet, the menu backdrop, the sea lines, the hub
band, page()) is Rev 101's, lifted as Rev 107 and Rev 121 do. Every image is a /_blob/ id in the canvas: the four
cards' shipped CardBackground renders (Assets/_Graphics/ARCADE/CardBackgrounds) and the hulls' colourful card renders
(Assets/_Graphics/Hangar/Cards), uploaded for this round.

Every hover in these boards is CSS: a 3 x 3 grid of invisible cells over a card (`i.hz`) picks the tilt and the
parallax, so the boards need no pointer handlers and nothing runs while a card rests except the option's one loop.
"""
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


ART = {'Skim Race': blob('351e7c818e70e534751fcb047b6ebd73'), 'Dog Fight': blob('db869644af7c79b38440e3f3940dd9a7'),
       'Headlong': blob('bbc3d5564020b01bad4c0da0511f5207'), 'Joust': blob('d3e33649e573457382d31f4f1ee39bcb')}
HULL = {'Squirrel': blob('0b59d109e643a6db63d1197fa68bec82'), 'Sparrow': blob('5d6e6caf494b25329c5384d72185255e'),
        'Rhino': blob('45aa01c84539d8c2a79fe4b9adee898b')}
# The element shapes (section 100.1): Time the diamond, Charge the pentagon. White; elements have no colour.
SHAPE = {'Time': '12,1.5 20,12 12,22.5 4,12', 'Charge': '12,2 21.5,9 18,20.5 6,20.5 2.5,9'}
# Each card as its SO_ArcadeGame authors it (DisplayName, Vessels[0], Description, Min/MaxPlayersAllowed).
MODES = {
    'Skim Race': {'hull': 'Squirrel', 'el': 'Time', 'pl': '1-12',
                  'desc': 'Find the fastest path to the crystal by skimming trails for a speed boost.'},
    'Dog Fight': {'hull': 'Sparrow', 'el': 'Charge', 'pl': '2-9',
                  'desc': 'Sparrows only, in the wreck of a world that already lost. Guns score one, rockets score fifty.'},
    'Headlong': {'hull': 'Rhino', 'el': 'Time', 'pl': '2-9',
                 'desc': 'Rhinos only, a stampede. Hold the throttle dead straight and the ramp winds you up to fifteen times cruise.'},
    'Joust': {'hull': 'Squirrel', 'el': 'Charge', 'pl': '2-12', 'desc': 'Joust joust joust'},
}
GRID = ['Skim Race', 'Dog Fight', 'Headlong', 'Joust']

# The race line through Skim Race's own render, in card units (the 768 x 510 art cover-fitted to 341 x 200:
# x * 0.444, y * 0.444 - 13). It follows the render's dotted trail down the left, through the pink ring at the
# foot, up through the gold and the small cyan ring, to a crystal at the top right.
SKIM_LINE = ('M 58 -12 C 44 30 44 70 56 100 C 72 140 130 182 190 196 C 214 201 216 180 210 160 '
             'C 207 135 206 115 208 98 C 210 70 220 48 231 35 C 240 26 252 30 262 46')
# A neighbour card has no traced line yet (the /cardart generator would write one per mode); a calm S stands in.
GENERIC_LINE = 'M -10 150 C 60 120 90 60 170 80 C 230 96 262 70 300 40'

W, H, BAND = 341, 296, 200
TILT = 7  # degrees at a corner cell

# ------------------------------------------------------------------ styles
CSS = '''
.gw{position:relative;width:341px;height:296px;perspective:900px}
.gw .hz{position:absolute;z-index:6;width:33.34%;height:33.34%;display:block}
.tilt{position:absolute;inset:0;transition:transform .22s cubic-bezier(.33,1,.68,1);transform-style:preserve-3d}
.gc{position:absolute;inset:0;overflow:hidden;background:#00041F;clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px)}
.edge{position:absolute;inset:0;pointer-events:none;z-index:5;clip-path:polygon(14px 0,100% 0,100% calc(100% - 14px),calc(100% - 14px) 100%,0 100%,0 14px);
 background:linear-gradient(#434C89,#434C89) top/100% 1px no-repeat,linear-gradient(#434C89,#434C89) bottom/100% 1px no-repeat,
 linear-gradient(#434C89,#434C89) left/1px 100% no-repeat,linear-gradient(#434C89,#434C89) right/1px 100% no-repeat;transition:filter .2s}
.edge::before,.edge::after{content:"";position:absolute;width:20px;height:1px;background:#434C89;transform-origin:0 0}
.edge::before{left:0;top:14px;transform:rotate(-45deg)}
.edge::after{right:0;bottom:14px;transform:rotate(-45deg);transform-origin:100% 100%}
.band{position:absolute;left:0;top:0;width:341px;height:200px;overflow:hidden}
.art{position:absolute;left:-9px;top:-6px;width:359px;height:212px;background-size:cover;background-position:center}
.drift{animation:drift 40s ease-in-out infinite alternate}
@keyframes drift{0%{transform:translate(0,0)}50%{transform:translate(-5px,3px)}100%{transform:translate(4px,-2px)}}
.seam{position:absolute;left:0;right:0;top:200px;height:1px;background:#222645}
.body{position:absolute;left:0;right:0;top:201px;bottom:0;background:#00041F}
.nm{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:22px;letter-spacing:.04em;color:#E6E9FF;white-space:nowrap}
.hl{object-fit:contain;filter:drop-shadow(0 0 1px rgba(230,233,255,.55))}
.star{position:absolute;right:12px;top:12px;width:32px;height:32px;border-radius:50%;background:rgba(0,1,10,.72);display:flex;align-items:center;justify-content:center;z-index:4}
.shp polygon{fill:none;stroke:#E6E9FF;stroke-width:1.6}
.ring{position:absolute;left:-5px;top:-5px;pointer-events:none;opacity:0;overflow:visible}
.ring polygon{fill:none;stroke:#00D4FF;stroke-width:2}
.fc .ring{opacity:1}
.lockv{position:absolute;inset:0;display:none;align-items:center;justify-content:center;z-index:4;background:rgba(0,1,10,.35)}
.lk .lockv{display:flex}
.lk .art,.lk .lay{filter:grayscale(1) brightness(.45)}
.lk .hl{filter:grayscale(1) brightness(.5)}
.lk .nm{color:#747BAD}
.lk .hz{display:none}
.capt{font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.16em;text-transform:uppercase;color:#747BAD}
.zoom{transform:scale(1.22);transform-origin:0 0}

/* ---- Before: the shipped card (Rev 18 A, 73; hover is 101.1 Sheen) ---- */
.o0 .sheen{position:absolute;top:0;bottom:0;left:-60%;width:45%;z-index:3;pointer-events:none;opacity:0;
 background:linear-gradient(100deg,rgba(230,233,255,0) 0,rgba(230,233,255,.16) 50%,rgba(230,233,255,0) 100%)}
.o0:hover .sheen,.o0.hv .sheen,.o0.fc .sheen{animation:sheen .52s cubic-bezier(.33,1,.68,1) both}
@keyframes sheen{0%{left:-60%;opacity:1}100%{left:115%;opacity:1}}
.o0:hover .body,.o0.hv .body,.o0.fc .body{background:#222645}

/* ---- A Depth: a window into the race, the picture full bleed, a glass strip for the name ---- */
.o1 .band{height:296px}
.o1 .art{height:316px;top:-10px;left:-14px;width:369px;mix-blend-mode:lighten}
.o1 .stars{position:absolute;inset:-24px;background:
 radial-gradient(1px 1px at 20px 30px,#E6E9FF 50%,transparent 51%) 0 0/90px 70px,
 radial-gradient(1.4px 1.4px at 60px 10px,#B8C0F0 50%,transparent 51%) 0 0/130px 110px,
 radial-gradient(1px 1px at 45px 55px,#747BAD 50%,transparent 51%) 0 0/60px 80px;opacity:.8}
.o1 .lay{position:absolute;inset:0;transition:transform .32s cubic-bezier(.22,1,.36,1)}
.o1 .mote{position:absolute;width:3px;height:3px;border-radius:50%;background:#E6E9FF;opacity:0;animation:mote 9s linear infinite}
@keyframes mote{0%{transform:translate(0,0);opacity:0}15%{opacity:.8}85%{opacity:.6}100%{transform:translate(-40px,-70px);opacity:0}}
.o1 .fly{position:absolute;left:22px;top:150px;width:140px;height:47px;filter:drop-shadow(0 8px 10px rgba(0,1,10,.8)) drop-shadow(0 0 1px rgba(230,233,255,.6));
 animation:glide 7s ease-in-out infinite alternate;transition:transform .32s cubic-bezier(.22,1,.36,1)}
@keyframes glide{from{margin:0 0 0 0}to{margin:-6px 0 0 -10px}}
.o1:hover .fly,.o1.hv .fly,.o1.fc .fly{transform:translate(14px,-10px) scale(1.12)}
.o1 .glass{position:absolute;left:0;right:0;bottom:0;height:68px;z-index:3;background:rgba(0,4,31,.62);backdrop-filter:blur(10px);-webkit-backdrop-filter:blur(10px);
 box-shadow:inset 0 1px 0 rgba(230,233,255,.14)}
.o1:hover .edge,.o1.hv .edge,.o1.fc .edge{filter:brightness(2.4)}
.o1 .glare{position:absolute;inset:0;z-index:4;pointer-events:none;opacity:0;transition:opacity .25s;
 background:radial-gradient(circle at 30% 20%,rgba(230,233,255,.16),rgba(230,233,255,0) 55%)}
.o1:hover .glare,.o1.hv .glare,.o1.fc .glare{opacity:1}

/* ---- B Racing line ---- */
.o2 .art{filter:brightness(.78) saturate(.9);transition:filter .3s}
.o2:hover .art,.o2.hv .art,.o2.fc .art{filter:brightness(.62) saturate(.8)}
.o2 .route{position:absolute;left:0;top:0;width:341px;height:200px;overflow:visible}
.o2 .base{fill:none;stroke:#E6E9FF;stroke-opacity:.22;stroke-dasharray:2 5;stroke-width:1.5;transition:stroke-opacity .4s}
.o2:hover .base,.o2.hv .base,.o2.fc .base{stroke-opacity:.55;stroke-dasharray:2 5}
.o2 .comet{fill:none;stroke:#E6E9FF;stroke-width:3;stroke-linecap:round;stroke-dasharray:9 120;stroke-dashoffset:129;
 filter:drop-shadow(0 0 3px rgba(230,233,255,.9));animation:comet 4.2s cubic-bezier(.45,0,.4,1) infinite}
@keyframes comet{0%{stroke-dashoffset:129}62%{stroke-dashoffset:9}63%,100%{stroke-dashoffset:-1;stroke-opacity:0}}
.o2:hover .comet,.o2.hv .comet,.o2.fc .comet{animation:none;opacity:0}
.o2 .cry{transform-box:fill-box;transform-origin:center;animation:cry 4.2s linear infinite}
@keyframes cry{0%,60%{transform:scale(1);opacity:.75}66%{transform:scale(1.9);opacity:1}80%,100%{transform:scale(1);opacity:.75}}
.o2 .pass{fill:none;stroke:#E6E9FF;stroke-width:1.4;opacity:0;transform-box:fill-box;transform-origin:center;animation:pass 4.2s linear infinite}
@keyframes pass{0%,100%{opacity:0;transform:scale(.8)}}
.o2 .p1{animation-name:pass1}.o2 .p2{animation-name:pass2}.o2 .p3{animation-name:pass3}
@keyframes pass1{0%,30%{opacity:0;transform:scale(.8)}34%{opacity:.9;transform:scale(1.15)}44%,100%{opacity:0;transform:scale(1.4)}}
@keyframes pass2{0%,44%{opacity:0;transform:scale(.8)}47%{opacity:.9;transform:scale(1.15)}56%,100%{opacity:0;transform:scale(1.4)}}
@keyframes pass3{0%,51%{opacity:0;transform:scale(.8)}54%{opacity:.9;transform:scale(1.15)}62%,100%{opacity:0;transform:scale(1.4)}}
.o2 .flyer{position:absolute;left:0;top:0;width:54px;height:18px;opacity:0;offset-rotate:auto 180deg;z-index:3;pointer-events:none}
.o2:hover .flyer,.o2.hv .flyer,.o2.fc .flyer{animation:fly 1.7s cubic-bezier(.4,0,.2,1) both}
@keyframes fly{0%{offset-distance:0%;opacity:0}6%{opacity:1}92%{opacity:1}100%{offset-distance:100%;opacity:0}}
.o2.hv .flyer{animation:none;opacity:1;offset-distance:58%}
.o2 .lit{fill:none;stroke:#E6E9FF;stroke-opacity:.85;stroke-width:1.6;stroke-linecap:round;stroke-dasharray:130;stroke-dashoffset:130;filter:drop-shadow(0 0 2px rgba(230,233,255,.8))}
.o2:hover .lit,.o2.fc .lit{animation:litin 1.7s cubic-bezier(.4,0,.2,1) both}
.o2.hv .lit{stroke-dashoffset:55}
@keyframes litin{to{stroke-dashoffset:0}}
.o2 .rule{position:absolute;left:84px;top:262px;height:1px;width:0;background:#E6E9FF;transition:width .3s cubic-bezier(.33,1,.68,1)}
.o2:hover .rule,.o2.hv .rule,.o2.fc .rule{width:150px}

/* ---- C Hero hull ---- */
.o3 .gc{top:28px}
.o3 .edge{top:28px}
.o3 .band{height:172px}
.o3 .art{height:190px;transition:transform .5s cubic-bezier(.22,1,.36,1)}
.o3:hover .art,.o3.hv .art,.o3.fc .art{transform:scale(1.05)}
.o3 .seam{top:172px}.o3 .body{top:173px}
.o3 .hero{position:absolute;right:-26px;top:-4px;width:270px;height:90px;z-index:7;pointer-events:none;
 filter:drop-shadow(0 10px 12px rgba(0,1,10,.75)) drop-shadow(0 0 1px rgba(230,233,255,.6));animation:bob 6s ease-in-out infinite alternate;
 transition:transform .45s cubic-bezier(.34,1.56,.64,1)}
@keyframes bob{from{margin-top:0}to{margin-top:5px}}
.o3:hover .hero,.o3.hv .hero,.o3.fc .hero{transform:translate(-16px,-8px) rotate(-5deg) scale(1.06)}
.o3 .streak{position:absolute;height:1.5px;right:150px;width:0;z-index:6;background:linear-gradient(270deg,rgba(230,233,255,.9),rgba(230,233,255,0));transition:width .45s cubic-bezier(.22,1,.36,1)}
.o3:hover .streak,.o3.hv .streak,.o3.fc .streak{width:130px}
.o3 .streak.s2{transition-delay:.05s}.o3 .streak.s3{transition-delay:.1s}

/* ---- D Silver foil: a collectible, the picture set in a foil frame ---- */
.o4 .gc{background:linear-gradient(135deg,#B8C0F0 0,#434C89 22%,#E6E9FF 40%,#747BAD 58%,#222645 76%,#B8C0F0 100%)}
.o4 .inner{position:absolute;inset:6px;background:#00041F;overflow:hidden;clip-path:polygon(10px 0,100% 0,100% calc(100% - 10px),calc(100% - 10px) 100%,0 100%,0 10px)}
.o4 .band{left:0;top:0;width:329px;height:184px}
.o4 .art{left:-10px;top:-8px;width:349px;height:202px}
.o4 .seam{top:184px;background:#747BAD}
.o4 .body{top:185px;background:linear-gradient(160deg,#222645 0,#00041F 70%)}
.o4 .foil{position:absolute;inset:0;z-index:2;pointer-events:none;mix-blend-mode:screen;opacity:.35;
 background:repeating-linear-gradient(115deg,rgba(230,233,255,0) 0,rgba(230,233,255,.0) 40px,rgba(230,233,255,.45) 52px,rgba(184,192,240,.12) 64px,rgba(230,233,255,0) 90px);
 background-size:400% 400%;animation:foil 16s linear infinite alternate;transition:opacity .3s}
@keyframes foil{from{background-position:0% 0%}to{background-position:100% 100%}}
.o4 .gc::after{content:"";position:absolute;inset:0;pointer-events:none;mix-blend-mode:screen;opacity:.6;
 background:linear-gradient(115deg,rgba(230,233,255,0) 30%,rgba(230,233,255,.9) 45%,rgba(230,233,255,0) 60%);background-size:300% 300%;animation:foil 9s ease-in-out infinite alternate}
.o4:hover .foil,.o4.hv .foil,.o4.fc .foil{opacity:.7}
.o4 .spec{position:absolute;inset:0;z-index:3;pointer-events:none;opacity:0;transition:opacity .25s,background-position .3s cubic-bezier(.22,1,.36,1);
 background:linear-gradient(115deg,rgba(230,233,255,0) 35%,rgba(230,233,255,.38) 50%,rgba(230,233,255,0) 65%);background-size:250% 250%;background-position:50% 50%}
.o4:hover .spec,.o4.hv .spec,.o4.fc .spec{opacity:1}
.o4 .edge{display:none}
.o4 .num{position:absolute;left:10px;top:10px;z-index:4;font-family:'Chakra Petch',sans-serif;font-weight:600;font-size:14px;letter-spacing:.18em;color:#00041F;
 padding:3px 8px;background:linear-gradient(90deg,#E6E9FF,#B8C0F0)}
.o4 .hl{top:208px!important}
.o4 .nm{top:220px!important}
.o4 .shpw{top:222px!important}

/* ---- E Poster ---- */
.o5 .art{height:316px;top:-10px}
.o5 .band{height:296px}
.o5 .shade{position:absolute;left:0;right:0;bottom:0;height:200px;z-index:2;background:linear-gradient(180deg,rgba(0,4,31,0) 0,rgba(0,4,31,.86) 58%,#00041F 100%)}
.o5 .title{position:absolute;left:20px;bottom:22px;z-index:3;font-family:'Chakra Petch',sans-serif;font-weight:700;font-size:44px;line-height:.92;letter-spacing:.02em;
 text-transform:uppercase;color:#E6E9FF;transition:transform .32s cubic-bezier(.22,1,.36,1)}
.o5 .open{position:absolute;left:0;right:0;bottom:0;height:0;z-index:4;overflow:hidden;background:rgba(0,4,31,.96);
 box-shadow:inset 0 1px 0 #434C89;transition:height .32s cubic-bezier(.22,1,.36,1)}
.o5:hover .open,.o5.hv .open,.o5.fc .open{height:112px}
.o5:hover .title,.o5.hv .title,.o5.fc .title{transform:translateY(-112px)}
.o5 .tag{position:absolute;left:20px;top:16px;z-index:3;display:flex;gap:10px;align-items:center}
.o5 .hp{position:absolute;right:8px;bottom:62px;z-index:3;width:120px;height:40px;transition:opacity .2s}
.o5:hover .hp,.o5.hv .hp,.o5.fc .hp{opacity:0}
.o5 .bars{display:flex;gap:4px;align-items:flex-end}
.o5 .bars i{display:block;width:6px;background:#434C89}
.o5 .bars i.on{background:#E6E9FF}

/* ---- F Live loop: a frame of the game at rest, the game running on hover ---- */
.o6 .live{position:absolute;inset:0;background:radial-gradient(ellipse 90% 80% at 50% 18%,#222645 0,#00041F 45%,#00041F 80%);overflow:hidden}
.o6 .cellrim{position:absolute;left:-70px;right:-70px;top:-170px;height:250px;border-radius:50%;
 background:radial-gradient(ellipse at 50% 100%,rgba(116,123,173,.35),rgba(116,123,173,0) 60%);box-shadow:inset 0 -2px 0 rgba(184,192,240,.55),0 0 40px rgba(116,123,173,.3)}
.o6 .pz{position:absolute;left:0;top:0;width:16px;height:5px;background:#21C0BD;opacity:0;offset-rotate:auto;
 animation:pz 1.8s cubic-bezier(.55,0,1,.6) infinite paused}
.o6 .pz.r{background:#BD1BC8}.o6 .pz.g{background:#FF9B11}
@keyframes pz{0%{offset-distance:0%;transform:scale(.15);opacity:0}12%{opacity:.95}100%{offset-distance:100%;transform:scale(2.6);opacity:1}}
.o6 .spk{position:absolute;left:0;top:0;width:2px;height:2px;border-radius:1px;background:#E6E9FF;opacity:0;offset-rotate:auto;
 animation:spk 1.2s cubic-bezier(.55,0,1,.6) infinite paused}
@keyframes spk{0%{offset-distance:0%;opacity:0;transform:scale(1,1)}30%{opacity:.8}100%{offset-distance:100%;opacity:.9;transform:scale(9,1)}}
.o6 .me{position:absolute;left:118px;top:128px;width:105px;height:35px;filter:drop-shadow(0 6px 8px rgba(0,1,10,.9));animation:sway 2.2s ease-in-out infinite alternate paused}
@keyframes sway{from{transform:translate(-22px,2px) rotate(4deg)}to{transform:translate(22px,-2px) rotate(-4deg)}}
.o6 .boost{position:absolute;left:150px;top:150px;width:50px;height:50px;border-radius:50%;background:radial-gradient(circle,rgba(230,233,255,.55),rgba(230,233,255,0) 70%);animation:sway 2.2s ease-in-out infinite alternate paused}
.o6 .cr{position:absolute;left:166px;top:52px;width:10px;height:10px;transform:rotate(45deg);border:1.5px solid #E6E9FF;background:rgba(230,233,255,.25);opacity:.8;animation:cr 3.2s linear infinite paused;animation-delay:-1.1s}
@keyframes cr{0%{transform:translate(0,0) rotate(45deg) scale(.4);opacity:0}10%{opacity:1}55%{transform:translate(0,74px) rotate(45deg) scale(2.2);opacity:1}60%{transform:translate(0,78px) rotate(45deg) scale(4);opacity:0}100%{opacity:0}}
.o6 .plus{position:absolute;left:190px;top:104px;font-family:'Chakra Petch',sans-serif;font-weight:700;font-size:15px;color:#E6E9FF;opacity:0;animation:plus 3.2s linear infinite paused;animation-delay:-1.1s}
@keyframes plus{0%,56%{opacity:0;transform:translateY(0)}62%{opacity:1}85%{opacity:0;transform:translateY(-22px)}100%{opacity:0}}
.o6:hover .pz,.o6.hv .pz,.o6.fc .pz,.o6:hover .spk,.o6.hv .spk,.o6.fc .spk,.o6:hover .me,.o6.hv .me,.o6.fc .me,
.o6:hover .boost,.o6.hv .boost,.o6.fc .boost,.o6:hover .cr,.o6.hv .cr,.o6.fc .cr,.o6:hover .plus,.o6.hv .plus,.o6.fc .plus{animation-play-state:running}
.o6 .rec{position:absolute;left:14px;top:14px;z-index:4;display:flex;gap:6px;align-items:center;opacity:.0;transition:opacity .3s}
.o6:hover .rec,.o6.hv .rec,.o6.fc .rec{opacity:1}
.lk .live{filter:grayscale(1) brightness(.45)}
'''


def tilt_css(o, layers=()):
    """The 3 x 3 cell grid: the cell under the pointer tilts the card toward it; each (class, px) layer shifts with it."""
    rules = []
    for r in range(3):
        for c in range(3):
            k = r * 3 + c + 1
            rx, ry = (1 - r) * TILT, (c - 1) * TILT
            sel = '.%s>i.hz:nth-of-type(%d):hover~.tilt' % (o, k)
            rules.append('%s{transform:rotateX(%ddeg) rotateY(%ddeg)}' % (sel, rx, ry))
            for cls, px in layers:
                rules.append('%s .%s{transform:translate(%dpx,%dpx)}' % (sel, cls, (c - 1) * px, (r - 1) * px))
            if o == 'o4':
                rules.append('%s .spec{background-position:%d%% %d%%}' % (sel, 100 - c * 50, 100 - r * 50))
    # The forced "hover" and pad-focus states hold the top-right cell's tilt.
    for st in ('hv', 'fc'):
        rules.append('.%s.%s>.tilt{transform:rotateX(%ddeg) rotateY(%ddeg)}' % (o, st, TILT // 2, TILT // 2))
        for cls, px in layers:
            rules.append('.%s.%s .%s{transform:translate(%dpx,%dpx)}' % (o, st, cls, px // 2, -px // 2))
    return '\n'.join(rules)


CSS += tilt_css('o1', [('far', -4), ('mid', 5), ('near', 12)])
CSS += '\n' + tilt_css('o4')
STYLE += CSS

CELLS = ''.join('<i class="hz" aria-hidden="true" style="left:%.2f%%;top:%.2f%%"></i>' % (c * 33.33, r * 33.33)
                for r in range(3) for c in range(3))
STAR = ('<span class="star" aria-label="Favourite"><svg width="16" height="16" viewBox="0 0 24 24" aria-hidden="true" fill="none" '
        'stroke="#E6E9FF" stroke-width="1.8" stroke-linejoin="round"><path d="M12 2.8l2.8 5.9 6.4.8-4.7 4.4 1.2 6.4L12 17.2l-5.7 3.1 1.2-6.4-4.7-4.4 6.4-.8z"/></svg></span>')
LOCK = ('<span class="lockv" aria-label="Locked"><svg width="34" height="34" viewBox="0 0 24 24" fill="none" stroke="#E6E9FF" stroke-width="1.6" '
        'aria-hidden="true"><rect x="5" y="11" width="14" height="10"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/></svg></span>')


def shape(el, size=24):
    return ('<svg class="shp" aria-label="%s" width="%d" height="%d" viewBox="0 0 24 24"><polygon points="%s"></polygon></svg>'
            % (el, size, size, SHAPE[el]))


def wrap(o, inner, state='', tilt=False, extra_outside=''):
    cells = CELLS if tilt else ''
    top = 28 if o == 'o3' else 0
    w, h = W + 10, H + 10 - top
    ring = ('<svg class="ring" width="%d" height="%d" style="top:%dpx" aria-hidden="true"><polygon points="16,1 %d,1 %d,%d %d,%d 1,%d 1,16"></polygon></svg>'
            % (w, h, top - 5, w - 1, w - 1, h - 16, w - 16, h - 1, h - 1))
    return ('<div class="gw %s %s">%s<div class="tilt">%s</div>%s%s</div>'
            % (o, state, cells, inner, extra_outside, ring))


def body_row(m, hull_w=60, name_left=84, shp=True):
    d = MODES[m]
    return ('<div class="body"></div><div class="seam"></div>'
            '<img class="hl" src="%s" alt="%s" style="position:absolute;left:10px;top:221px;width:%dpx;height:54px">'
            '<div class="nm" style="position:absolute;left:%dpx;top:234px">%s</div>'
            '%s' % (HULL[d['hull']], d['hull'], hull_w, name_left, m,
                    ('<div style="position:absolute;right:16px;top:236px">%s</div>' % shape(d['el'])) if shp else ''))


# ------------------------------------------------------------------ the seven cards
def card_before(m, st=''):
    inner = ('<div class="gc"><div class="band"><div class="art drift" style="background-image:url(%s)"></div></div>%s%s<div class="sheen"></div>%s</div><div class="edge"></div>'
             % (ART[m], STAR, body_row(m), LOCK))
    return wrap('o0', inner, st)


def card_a(m, st=''):
    d = MODES[m]
    motes = ''.join('<i class="mote" style="left:%dpx;top:%dpx;animation-delay:-%.1fs"></i>' % (x, y, dl)
                    for x, y, dl in [(60, 170, 0), (250, 150, 3), (300, 190, 6.2), (140, 120, 4.4), (210, 60, 7.5)])
    inner = ('<div class="gc"><div class="band">'
             '<div class="lay far"><div class="stars"></div></div>'
             '<div class="lay mid"><div class="art drift" style="background-image:url(%s)"></div></div>'
             '<div class="lay near">%s<img class="fly hl" src="%s" alt="%s"></div><div class="glare"></div></div>%s'
             '<div class="glass"><div class="nm" style="position:absolute;left:18px;top:20px">%s</div>'
             '<div style="position:absolute;right:16px;top:22px">%s</div></div>%s</div><div class="edge"></div>'
             % (ART[m], motes, HULL[d['hull']], d['hull'], STAR, m, shape(d['el']), LOCK))
    return wrap('o1', inner, st, tilt=True)


def card_b(m, st=''):
    line = SKIM_LINE if m == 'Skim Race' else GENERIC_LINE
    rings = ''
    if m == 'Skim Race':
        rings = ('<ellipse class="pass p1" cx="210" cy="190" rx="17" ry="11"></ellipse>'
                 '<ellipse class="pass p2" cx="208" cy="98" rx="12" ry="8"></ellipse>'
                 '<ellipse class="pass p3" cx="231" cy="35" rx="7" ry="10"></ellipse>')
    end = (262, 46) if m == 'Skim Race' else (300, 40)
    svg = ('<svg class="route" viewBox="0 0 341 200" aria-hidden="true">'
           '<path class="base" d="%s" pathLength="120"></path>'
           '<path class="lit" d="%s" pathLength="130"></path>'
           '<path class="comet" d="%s" pathLength="120"></path>%s'
           '<rect class="cry" x="%d" y="%d" width="9" height="9" fill="none" stroke="#E6E9FF" stroke-width="1.5" transform="rotate(45 %d %d)"></rect>'
           '</svg>' % (line, line, line, rings, end[0] - 4, end[1] - 4, end[0], end[1]))
    flyer = ('<img class="flyer" src="%s" alt="" style="offset-path:path(\'%s\')">' % (HULL[MODES[m]['hull']], line))
    inner = ('<div class="gc"><div class="band"><div class="art drift" style="background-image:url(%s)"></div>%s%s</div>%s%s<div class="rule"></div>%s</div><div class="edge"></div>'
             % (ART[m], svg, flyer, STAR, body_row(m), LOCK))
    return wrap('o2', inner, st)


def card_c(m, st=''):
    d = MODES[m]
    inner = ('<div class="gc"><div class="band"><div class="art" style="background-image:url(%s)"></div></div>%s'
             '<div class="body"></div><div class="seam"></div>'
             '<div class="nm" style="position:absolute;left:18px;top:196px;font-size:22px">%s</div>'
             '<div class="capt" style="position:absolute;left:18px;top:232px;display:flex;gap:12px;align-items:center">%s<span>%s pilots</span></div>'
             '%s</div><div class="edge"></div>' % (ART[m], STAR.replace('right:12px', 'left:12px;right:auto'), m, shape(d['el'], 18), d['pl'], LOCK))
    outside = ('<i class="streak" style="top:34px"></i><i class="streak s2" style="top:46px"></i><i class="streak s3" style="top:58px"></i>'
               '<img class="hero hl" src="%s" alt="%s">' % (HULL[d['hull']], d['hull']))
    if st == 'lk':
        outside = outside.replace('class="hero hl"', 'class="hero hl" style="filter:grayscale(1) brightness(.45)"')
    return wrap('o3', inner, st, extra_outside=outside)


def card_d(m, st=''):
    d = MODES[m]
    inner = ('<div class="gc"><div class="inner"><div class="band"><div class="art drift" style="background-image:url(%s)"></div>'
             '<div class="foil"></div></div>%s'
             '<div class="body"></div><div class="seam"></div>'
             '<img class="hl" src="%s" alt="%s" style="position:absolute;left:10px;top:221px;width:60px;height:54px">'
             '<div class="nm" style="position:absolute;left:84px;top:234px">%s</div>'
             '<div class="shpw" style="position:absolute;right:16px;top:236px">%s</div>'
             '<span class="num">No. 033</span>%s</div><div class="spec"></div></div>'
             % (ART[m], STAR, HULL[d['hull']], d['hull'], m, shape(d['el']), LOCK))
    return wrap('o4', inner, st, tilt=True)


def card_e(m, st=''):
    d = MODES[m]
    words = m.split(' ')
    title = '<br>'.join(words) if len(words) > 1 else m
    fs = 36 if max(len(w) for w in words) > 6 else 44
    bars = ''.join('<i class="%s" style="height:%dpx"></i>' % ('on' if k < 2 else '', 6 + k * 4) for k in range(4))
    inner = ('<div class="gc"><div class="band"><div class="art drift" style="background-image:url(%s)"></div></div>'
             '<div class="shade"></div>%s'
             '<div class="tag">%s</div>'
             '<img class="hp hl" src="%s" alt="%s">'
             '<div class="title" style="font-size:%dpx">%s</div>'
             '<div class="open"><div style="position:absolute;left:20px;right:20px;top:16px;font-size:15px;line-height:1.45;color:#B8C0F0;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden">%s</div>'
             '<div class="capt" style="position:absolute;left:20px;right:20px;bottom:14px;display:flex;justify-content:space-between;align-items:center">'
             '<span>%s pilots</span><span style="display:flex;gap:10px;align-items:center">Intensity <span class="bars">%s</span></span></div></div>'
             '%s</div><div class="edge"></div>'
             % (ART[m], STAR, shape(d['el'], 20), HULL[d['hull']], d['hull'], fs, title, d['desc'], d['pl'], bars, LOCK))
    return wrap('o5', inner, st)


# F's trails run from the vanishing point under the cell's rim to the foot of the band (card units).
F_TRAILS = [('', 'M 172 44 C 172 90 168 150 166 230'), ('r', 'M 176 44 C 196 90 250 150 330 214'),
            ('g', 'M 166 44 C 150 90 92 150 10 214')]
F_SPARKS = ['M 170 44 L -20 60', 'M 170 44 L 360 70', 'M 170 44 L -10 140', 'M 170 44 L 350 160', 'M 170 44 L 60 -10', 'M 170 44 L 290 -10']


def card_f(m, st=''):
    d = MODES[m]
    pz = ''.join('<i class="pz %s" style="offset-path:path(\'%s\');animation-delay:-%.2fs"></i>' % (c, path, k * 0.3)
                 for c, path in F_TRAILS for k in range(6))
    spk = ''.join('<i class="spk" style="offset-path:path(\'%s\');animation-delay:-%.2fs"></i>' % (path, k * 0.37)
                  for k, path in enumerate(F_SPARKS))
    live = ('<div class="live"><div class="cellrim"></div>%s%s'
            '<i class="cr"></i><span class="plus">+1</span><i class="boost"></i><img class="me hl" src="%s" alt="%s"></div>' % (spk, pz, HULL[d['hull']], d['hull']))
    inner = ('<div class="gc"><div class="band">%s'
             '<span class="rec capt" style="color:#E6E9FF"><svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true"><polygon points="1,1 9,5 1,9" fill="#E6E9FF"></polygon></svg>Preview</span></div>'
             '%s%s%s</div><div class="edge"></div>' % (live, STAR, body_row(m), LOCK))
    return wrap('o6', inner, st)


OPTS = [
    ('Before', 'The card we ship', card_before, 'Rest: the render drifts 5 px over 40 s. Hover: one sheen sweeps, the body lifts 8 %.'),
    ('A', 'Depth', card_a, 'Rest: the picture full bleed in three planes, the Squirrel in it, the name on glass. Hover: the card tilts to the pointer, the planes part.'),
    ('B', 'Racing line (recommended)', card_b, 'Rest: a light runs the course through the rings to the crystal. Hover: the Squirrel flies it once.'),
    ('C', 'Hero hull', card_c, 'Rest: the Squirrel rides above the frame. Hover: it surges forward, speed lines behind.'),
    ('D', 'Silver foil', card_d, 'Rest: the picture in a silver foil frame that shimmers. Hover: the card tilts, the light follows the pointer.'),
    ('E', 'Poster', card_e, 'Rest: the render full bleed under a big title. Hover: the card slides open: what it is, pilots, intensity.'),
    ('F', 'Live loop', card_f, 'Rest: one frame of the game. Hover: it plays, the Squirrel skimming the trails to a crystal.'),
]

HEAD = ('<div class="abs" style="left:64px;top:108px;display:flex;align-items:baseline;gap:22px;z-index:4">'
        '<span style="font-size:28px">Arcade</span><span class="lab">All games</span></div>')


def option_board(letter, name, fn, line, title):
    grid = ''.join('<div class="abs" style="left:%dpx;top:212px">%s</div>' % (64 + k * 381, fn(m)) for k, m in enumerate(GRID))
    states = [('Rest', ''), ('Hover (held)', 'hv'), ('Pad focus', 'fc'), ('Locked', 'lk')]
    strip = ''.join('<div class="abs" style="left:%dpx;top:%dpx"><span class="capt" style="display:block;margin-bottom:16px">%s</span>%s</div>'
                    % (64 + k * 381, 610, lab, fn('Skim Race', st)) for k, (lab, st) in enumerate(states))
    side = ('<div class="abs" style="left:1600px;top:212px;width:256px;display:flex;flex-direction:column;gap:14px">'
            '<span class="lab" style="color:#E6E9FF">%s &#183; %s</span>'
            '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">%s</span>'
            '<span style="font-size:14px;line-height:1.5;color:#747BAD">Point at any card above. The row below holds Skim Race in each state.</span></div>'
            % (letter, name, line))
    body = (BACKDROP + HUB +
            '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,4,31,.95)"></div>'
            + HEAD + grid +
            '<div class="abs" style="left:64px;right:64px;top:572px;height:1px;background:#222645"></div>'
            '<span class="lab abs" style="left:64px;top:584px">Skim Race, every state</span>'
            + strip + side)
    return page(title, body, NOLOGIC)


NOLOGIC = '\nclass Component extends DCLogic {\nrenderVals() {\nreturn {};\n}\n}\n'


def compare(title):
    cells = []
    pos = [(64, 190), (514, 190), (964, 190), (1414, 190), (64, 640), (514, 640), (964, 640)]
    for (x, y), (letter, name, fn, line) in zip(pos, OPTS):
        mark = 'color:#E6E9FF' if letter == 'Before' else ''
        cells.append('<div class="abs" style="left:%dpx;top:%dpx;width:416px">'
                     '<div style="display:flex;gap:12px;align-items:baseline;margin-bottom:6px"><span class="lab" style="%s">%s</span>'
                     '<span style="font-size:18px">%s</span></div>'
                     '<div style="height:384px;position:relative;margin-top:%dpx"><div class="zoom">%s</div></div></div>'
                     % (x, y, mark, letter, name, 30 if letter == 'C' else 14, fn('Skim Race')))
    legend = ('<div class="abs" style="left:1414px;top:668px;width:442px;display:flex;flex-direction:column;gap:14px">'
              '<span class="lab" style="color:#E6E9FF">Read me</span>'
              '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">One mode, Skim Race, seven ways at 1.22 x the in-game size. '
              'Point at each card: what you see is the hover, and on a controller the same plays when the card takes focus.</span>'
              '<span style="font-size:15px;line-height:1.55;color:#B8C0F0">Each card keeps what the shipped card says: the name, the hull, '
              'the element shape and the favourite star. No new words except where the card opens (E).</span>'
              '<span style="font-size:14px;line-height:1.5;color:#747BAD">Each option has its own board with the Arcade row and every state.</span></div>')
    body = (BACKDROP + HUB +
            '<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,4,31,.95)"></div>'
            '<div class="abs" style="left:64px;top:108px;display:flex;align-items:baseline;gap:22px"><span style="font-size:28px">Skim Race</span>'
            '<span class="lab">The card we ship beside six new ones</span></div>'
            + ''.join(cells) + legend)
    return page(title, body, NOLOGIC)


def notes(title):
    def block(head, lines):
        return ('<div style="display:flex;flex-direction:column;gap:14px"><span class="lab" style="color:#E6E9FF">%s</span>' % head
                + ''.join('<span style="font-size:15px;line-height:1.55;color:#B8C0F0">%s</span>' % l for l in lines) + '</div>')
    cols = [
        block('Asked (Shombith, 2026-11-23)', [
            '&#8220;Redesign the arcade/ arena game cards. Currently they are still images ... They need to get way way better in terms of lets say appearance, motion, hover features etc. They need to lively yet not affect performance.&#8221;',
            'One mode, Skim Race, drawn seven ways: the card we ship (Rev 18 A, section 73, hover 101.1 Sheen) and six new ones, same size (341 x 296), same words.',
            'Today the card is one still render (the /cardart offline render of the mode&#8217;s arena) that drifts 5 px over 40 s.']),
        block('What each costs in Unity', [
            '<b>A Depth.</b> Three images in the card&#8217;s mask; a tilt and three offsets set on pointer enter / move, eased. Nothing per frame at rest but the drift and five motes (one shader).',
            '<b>B Racing line.</b> One line mesh on the band with a scrolling material (the comet is a UV offset, like CardDrift). The hull tween plays once on hover. The line per mode comes from the course /cardart already renders.',
            '<b>C Hero hull.</b> The hull image moves outside the mask; a bob on the existing PortraitBob shader; one tween on hover.',
            '<b>D Silver foil.</b> One overlay material on the band (a scrolling gradient), a tilt like A. The cheapest of the moving ones.',
            '<b>E Poster.</b> One height tween on hover. Nothing moves at rest but the drift.',
            '<b>F Live loop.</b> A 3 s flipbook (24 frames in one atlas per mode) played only on the hovered or focused card; every other card shows its first frame. Costs memory per mode, not frame time.']),
        block('Rules every option keeps', [
            'Hover is white; the pad&#8217;s focus is the sky-blue ring, and focus plays the same as hover, so a controller sees the motion too.',
            'Touch gets no hover: on a phone the card is the rest state, and a press opens Configure as today.',
            'Locked cards stand still and answer nothing.',
            'Elements have no colour: the shape stays white. The only colour is the render itself (and the trail prisms in F, as the game draws them).',
            'At most one loop per card at rest; F has none.',
            'The words are each card&#8217;s own (SO_ArcadeGame: name, Description, players). Joust&#8217;s Description is still the placeholder &#8220;Joust joust joust&#8221;, which E would show.']),
        block('Recommendation: B, Racing line', [
            'It teaches the mode at a glance (skim the trail, through the rings, to the crystal), so the card says what the game is without a word.',
            'It is lively at rest at almost no cost (one scrolling line per card), and the Squirrel flying it on hover is the payoff.',
            'It grows to every mode: each card&#8217;s course comes from the generator /cardart renders, so the line is data, not hand art.',
            'Mix and match is open: B&#8217;s line works with C&#8217;s hull or E&#8217;s slide-open body.']),
    ]
    body = ('<div class="abs" style="left:64px;top:56px;display:flex;flex-direction:column;gap:10px"><span class="lab">Rev %s notes</span>'
            '<span style="font-size:36px">Game cards, six ways</span></div>'
            '<div class="abs" style="left:64px;right:64px;top:170px;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));column-gap:56px">'
            % REV + ''.join(cols) + '</div>')
    return page(title, body, NOLOGIC, 1920, 900)


T = 'Rev %s' % REV
boards = {
    'Compare': compare('%s - Compare: the shipped card and the six new ones side by side, hover each' % T),
    'Notes': notes('%s - notes: what each one moves, what it costs in Unity, the recommendation' % T),
}
TITLES = {
    'A': 'Depth: the picture in three planes that tilt with the pointer',
    'B': 'Racing line: a light runs the course through the rings; on hover the Squirrel flies it (recommended)',
    'C': 'Hero hull: the Squirrel breaks out of the frame and surges on hover',
    'D': 'Silver foil: a collectible card, foil that catches the light as it tilts',
    'E': 'Poster: the picture full bleed under a big title; the card slides open on hover',
    'F': 'Live loop: a still at rest, the game playing in the card on hover',
}
for letter, name, fn, line in OPTS[1:]:
    boards[letter] = option_board(letter, name, fn, line, '%s %s - %s - interactive' % (T, letter, TITLES[letter]))
for k, html in boards.items():
    with open(os.path.join(out, 'R%s%s.dc.html' % (REV, k)), 'w') as f:
        f.write(html)
print('written', out, sorted(boards))
