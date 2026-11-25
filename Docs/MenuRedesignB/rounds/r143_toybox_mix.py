#!/usr/bin/env python3
"""Rev 143 - the Toy Box mix (2026-11-24): Rev 142 D's Toy Box with Rev 142 A's Configure, on the menu's gradient and sea lines.

Usage: python3 r143_toybox_mix.py <root> 143
Shombith on Rev 142: he likes the Toy Box screen, not the Configure that opens from it ("put the A UI for the inside
views"), and "it's too blue ... There is no gradient behind. There's no sea lines." Rev 142 laid a flat 95 % navy
window over the backdrop, which hid the backdrop's gradient and sea lines; this round draws them OVER the window and
makes the cards dark glass instead of solid navy. Everything else is Rev 142's code (r142_toybox.py), reused as is.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, REV = sys.argv[1], sys.argv[2]
_src = open(os.path.join(HERE, 'r142_toybox.py')).read()
_src = _src[:_src.index('# ================================================================== boards')]
exec(_src)  # everything of Rev 142 but its board list and writer

# The window: the menu's own gradient and sea lines drawn over a near-black ground, so they show (Rev 142 hid them).
_bg = BACKDROP[BACKDROP.find('<div'):]
WINDOW = ('<div class="abs" aria-hidden="true" style="left:0;top:84px;width:1920px;height:996px;background:rgba(0,1,10,.9)"></div>'
          + _bg + STRIP)
# The cards: dark glass, a soft light from the top, the sea lines; no solid navy.
NEW_BG = ('background:radial-gradient(ellipse 90% 55% at 50% 0%,rgba(230,233,255,.07),rgba(230,233,255,0) 70%),'
          'repeating-linear-gradient(172deg,rgba(230,233,255,.035) 0 1px,rgba(0,0,0,0) 1px 16px),'
          'linear-gradient(180deg,rgba(18,20,38,.72) 0%,rgba(0,1,10,.82) 100%);backdrop-filter:blur(6px)')
BASE_CSS = BASE_CSS.replace(CARD_BG, NEW_BG)
# The variation tiles: the same dark glass, not solid navy.
BASE_CSS = BASE_CSS.replace('text-align:left;background:#00041F;', 'text-align:left;background:rgba(0,1,10,.55);').replace('.tb-tile:hover{background:#0B1240}', '.tb-tile:hover{background:rgba(230,233,255,.06)}')


def e_from():
    px, py, pw, ph = A_PV
    return [[round(x + D_CARD[0] / 2 - (px + pw / 2)), round(y + D_CARD[1] / 2 - (py + ph / 2)), round(D_CARD[0] / pw, 3)] for x, y in D_POS]


OPT_E = {'from': e_from(), 'cards': [{'x': x, 'y': y} for x, y in D_POS]}

import json
import re
os.makedirs(out, exist_ok=True)
PLANNED = json.load(open(os.path.join(out, 'canvas.json')))['boards']
files = {'R%sE.dc.html' % REV: board('x', D_CSS + A_CSS, d_box(), a_cfg(), a_dive(), a_world(), OPT_E)}
for name, html in files.items():
    html = re.sub(r'<title>.*?</title>', lambda m: '<title>' + PLANNED[name]['title'].replace('&', '&amp;') + '</title>', html, count=1)
    open(os.path.join(out, name), 'w').write(html)
    print('wrote', name, len(html))
