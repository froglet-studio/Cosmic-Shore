#!/usr/bin/env python3
"""Headless screenshots of nca_demo.html: grown lizard, right after a bite, and after regrowth.
   python3 shoot_nca_demo.py [--three /path/three.min.js]   (routes the jsDelivr three r128 URL to a local copy)"""
import argparse, os, pathlib
from playwright.sync_api import sync_playwright
HERE = pathlib.Path(__file__).resolve().parent
ap = argparse.ArgumentParser(); ap.add_argument('--three', default='/home/claude/scratch/three.min.js')
ap.add_argument('--chrome', default='/opt/pw-browsers/chromium-1194/chrome-linux/chrome'); a = ap.parse_args()
(HERE / 'shots').mkdir(exist_ok=True)
with sync_playwright() as p:
    b = p.chromium.launch(executable_path=a.chrome if os.path.exists(a.chrome) else None, args=['--use-gl=swiftshader', '--enable-webgl', '--ignore-gpu-blocklist'])
    pg = b.new_page(viewport={'width': 1100, 'height': 700})
    pg.route('https://cdn.jsdelivr.net/npm/three@0.128.0/build/three.min.js', lambda r: r.fulfill(path=a.three, content_type='application/javascript'))
    errs = []; pg.on('pageerror', lambda e: errs.append(str(e)))
    for el in ('space', 'mass'):
        pg.goto((HERE / 'nca_demo.html').as_uri() + f'?element={el}&grow=120')
        pg.wait_for_function('window.creature && window.creature.steps > 125', timeout=60000)
        pg.click('#spin'); pg.wait_for_timeout(300)
        pg.screenshot(path=str(HERE / 'shots' / f'nca_demo_{el}.png'))
        if el == 'space':
            info = pg.evaluate('''() => { const c = window.creature, v = c.voxels(); let far = 0, fd = -1;
              for (let i = 0; i < v.n; i++) { const d = Math.hypot(v.pos[3*i], v.pos[3*i+2]); if (d > fd) { fd = d; far = i; } }
              const p = [0.5*v.pos[3*far], v.pos[3*far+1], 0.5*v.pos[3*far+2]]; const before = c.count(); const rem = c.hit(p, 3.5); c.sync();
              return { before, removed: rem, steps: c.steps }; }''')
            pg.wait_for_timeout(50); pg.screenshot(path=str(HERE / 'shots' / 'nca_demo_bitten.png'))
            s0 = info['steps']; pg.wait_for_function(f'window.creature.steps > {s0 + 60}', timeout=120000)
            info['after'] = pg.evaluate('window.creature.count()')
            pg.screenshot(path=str(HERE / 'shots' / 'nca_demo_regrown.png'))
            print('bite', info)
    print('stats:', pg.inner_text('#stats')); print('page errors:', errs[:3])
    b.close()
