#!/usr/bin/env python3
"""Rev 144 - the Toy Box mix with the 2D toy glyphs (2026-11-24).

Usage: python3 r144_toybox_glyphs.py <root> 144
Shombith on Rev 143: "I want the 2D icons they had which garrett added not these 3D icons", and "If you can make those
2D icons with a different variation to give them a 3D look you can try". The glyphs are Tools/Build/icon_kit/
toy_portraits.py's seven drawings (Docs/CODEX.md section 4, sheet Docs/Icons/sheets/toybox.png), rendered at 512 px.
F shows them flat as drawn. G shows the same glyph baked with depth (an 18-step extrusion down-right in the slate
shades, a top-left lit bevel on the white face, a soft contact shadow) and a perspective tilt on hover; the bake is
`depth()` below, a one-off image pass a Unity build would ship as a second sprite per toy. Everything else is Rev 143.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
root, REV = sys.argv[1], sys.argv[2]
_src = open(os.path.join(HERE, 'r143_toybox_mix.py')).read()
_src = _src[:_src.index('import json\nimport re')]
sys.argv = [sys.argv[0], root, REV]
exec(_src)

FLAT = ['c0f8c947fa3e2cc07a8bc527032da2c9', 'dafb232810f250b1fe78d334e09eff21', 'b7a872eae51fa85f051b8d9d5134db30',
        '1447dc1b35c56671ca0cc82533ec3b98', 'bf67bcb788f989d4fb9c6d71a65e4388', '28ab6a6997fc5abeccae38e9496c9a80',
        '9b78c5a8d070fb6fa36ee6d26748e7d5']   # vessel, spawn, paint, cell, domain, charge, wander (TOYS order)
DEEP = ['ab2c648891979025a4e867484e7df135', '89188e129eefb17e5d8ed19c55627126', 'c731377d67c2c4bfc3c0974672ab2f27',
        'e2d8fdd7b8c21c71c09e01e8d2ce5bd8', 'e3a894818fbcc6e4d7f92f6c0a7965b0', '094d90c3a98e20bc7f5e8f4472ddcf4f',
        '4ad39e06a90bb0da3bd5a9e779fab2b1']

F_CSS = '.d-card:hover .d-obj{transform:scale(1.08)}\n.d-lens .d-obj{left:30px;top:30px;width:140px;height:140px}\n'
G_CSS = ('.d-lens{perspective:500px}\n.d-lens .d-obj{left:22px;top:22px;width:156px;height:156px}\n'
         '.d-card:hover .d-obj{transform:rotateX(14deg) rotateY(-20deg) scale(1.1)}\n'
         '.a-in img{filter:drop-shadow(0 24px 30px rgba(0,1,10,.6))}\n')

import json
import re
os.makedirs(out, exist_ok=True)
PLANNED = json.load(open(os.path.join(out, 'canvas.json')))['boards']
files = {}
for key, ids, css in (('F', FLAT, F_CSS), ('G', DEEP, G_CSS)):
    for t, i in zip(TOYS, ids):
        t['art'] = blob(i)
    files['R%s%s.dc.html' % (REV, key)] = board('x', D_CSS + A_CSS + css, d_box(), a_cfg(), a_dive(), a_world(), OPT_E)
for name, html in files.items():
    html = re.sub(r'<title>.*?</title>', lambda m: '<title>' + PLANNED[name]['title'].replace('&', '&amp;') + '</title>', html, count=1)
    open(os.path.join(out, name), 'w').write(html)
    print('wrote', name, len(html))
