#!/usr/bin/env node
// Pixel parity between two rounds of the Omni Shepard Lab at the SHIPPED settings.
//
//   git show <old-commit>:Docs/Studios/OmniShepardLab.html > /tmp/old.html
//   NODE_PATH=/usr/local/lib/node_modules_global node Tools/Build/omni_shepard_lab_parity.cjs /tmp/old.html Docs/Studios/OmniShepardLab.html \
//       [--old-set '{"period":3.5,...}'] [--new-style classic]
//
// --old-set applies knob values to the OLD page first (e.g. the recipe a designer exported, to prove
// the new shipped assets encode it); --new-style loads a library style on the NEW page first (e.g.
// "classic", to prove a style kept its look when the shipped numbers moved).
//
// Both pages are driven through window.__lab on a manual clock (camera 34 u, no orbit, seed 7,
// 800 x 600 stage) and read back with readPixels at five times across the trip. A round that
// generalises the effect must still draw the shipped look: round 3 vs round 2 measured at most
// 10 of ~85k lit pixels over 2/255. Negative control: moving any style knob (Swirl 270°) makes
// tens of thousands of pixels differ.
'use strict';
const { chromium } = require('playwright');
const argv = process.argv.slice(2), flag = (n) => { const i = argv.indexOf(n); return i < 0 ? null : argv.splice(i, 2)[1]; };
const OLD_SET = JSON.parse(flag('--old-set') || '{}'), NEW_STYLE = flag('--new-style');
(async () => {
  const b = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  async function grab(file, ts, set, style) {
    const p = await b.newPage({ viewport: { width: 1600, height: 900 } });
    const errs = []; p.on('pageerror', (e) => errs.push(e.message)); p.on('console', (m) => { if (m.type() !== 'log' && !/GPU stall/.test(m.text())) errs.push(m.text()); });
    await p.goto('file://' + require('path').resolve(file), { waitUntil: 'load' });
    const out = [];
    for (const t of ts) {
      out.push(await p.evaluate(([t, set, style]) => {
        const L = window.__lab;
        if (style) L.applyStyle(L.STYLES.find((x) => x.id === style));
        for (const [k, v] of Object.entries(set)) L.setParam(k, v);
        const cv = document.getElementById('stage'); cv.style.width = '800px'; cv.style.height = '600px'; L.manualClock = true; L.setParam('viewSpin', 0); L.setParam('viewDist', 34); L.reset(7); L.tick(t, 1);
        const c = document.getElementById('stage'), g = c.getContext('webgl'), px = new Uint8Array(c.width * c.height * 4);
        g.readPixels(0, 0, c.width, c.height, g.RGBA, g.UNSIGNED_BYTE, px); let bin = ''; for (let i = 0; i < px.length; i += 8192) bin += String.fromCharCode.apply(null, px.subarray(i, i + 8192)); return { w: c.width, h: c.height, px: btoa(bin) };
      }, [t, set, style]));
    }
    await p.close(); return { out, errs };
  }
  const ts = [0.5, 1.7, 2.95, 4.2, 7.7];
  const A = await grab(argv[0], ts, OLD_SET, null), B = await grab(argv[1], ts, {}, NEW_STYLE);
  console.log('errors r2', A.errs, 'r3', B.errs);
  ts.forEach((t, i) => {
    const a = { w: A.out[i].w, h: A.out[i].h, px: Buffer.from(A.out[i].px, 'base64') }, c = { w: B.out[i].w, h: B.out[i].h, px: Buffer.from(B.out[i].px, 'base64') }; if (a.w !== c.w || a.h !== c.h) { console.log('size differs'); return; }
    let max = 0, sum = 0, over = 0, lit = 0;
    for (let k = 0; k < a.px.length; k += 4) { let d = 0; for (let q = 0; q < 3; q++) d = Math.max(d, Math.abs(a.px[k + q] - c.px[k + q])); max = Math.max(max, d); sum += d; if (d > 2) over++; if (a.px[k] + a.px[k + 1] + a.px[k + 2] > 40) lit++; }
    console.log('t=' + t + '  max diff ' + max + '/255, mean ' + (sum / (a.px.length / 4)).toFixed(4) + ', pixels >2: ' + over + ' of ' + lit + ' lit');
  });
  await b.close();
})();
