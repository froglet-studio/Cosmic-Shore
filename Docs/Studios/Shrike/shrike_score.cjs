#!/usr/bin/env node
// shrike_score.cjs - the Shrike Studio's scorecard, headless: the same runScorecard / runStyles the page's
// "Score AI levels" and "Score styles" buttons run, on five seeds, for both minigames. Prints JSON.
//
//   export NODE_PATH=<a folder with node_modules/playwright>      (here: /usr/local/lib/node_modules_global)
//   node Docs/Studios/Shrike/shrike_score.cjs [--three <local three.min.js>] [--games thread,den]
//
// --three serves three.js r128 from a local file when the CDN is blocked (the page loads it from cdnjs).
'use strict';
const path = require('path');
const { chromium } = require('playwright');
const argv = process.argv.slice(2), opt = (k) => { const i = argv.indexOf(k); return i >= 0 ? argv[i + 1] : null; };
const page = path.resolve(__dirname, '../VesselStudio/shrike.html'), three = opt('--three'), games = (opt('--games') || 'thread,den').split(',');
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  const p = await (await browser.newContext({ viewport: { width: 1600, height: 900 } })).newPage();
  const errors = []; p.on('pageerror', (e) => errors.push(e.message));
  if (three) await p.route('**/three.min.js', (r) => r.fulfill({ path: path.resolve(three), contentType: 'text/javascript' }));
  await p.route('https://fonts.googleapis.com/**', (r) => r.fulfill({ body: '', contentType: 'text/css' }));
  await p.goto('file://' + page, { waitUntil: 'load' }); await p.waitForTimeout(500);
  const out = await p.evaluate(async (games) => {
    const S = window.__shrikeStudio, seeds = [11, 22, 33, 44, 55], r = (x) => x == null ? null : Math.round(x * 10) / 10, res = {};
    for (const g of games) {
      const lv = await new Promise((done) => S.runScorecard(g, seeds, done)), st = await new Promise((done) => S.runStyles(g, seeds, done));
      for (const I in lv) for (const k in lv[I]) lv[I][k] = r(lv[I][k]);
      for (const n in st) { st[n].finish = r(st[n].finish); st[n].acc = Math.round(st[n].acc * 100); }
      res[g] = { levels: lv, styles: st };
    }
    return res;
  }, games);
  if (errors.length) { console.error('page errors:\n' + errors.join('\n')); process.exitCode = 1; }
  console.log(JSON.stringify(out, null, 1));
  await browser.close();
})().catch((e) => { console.error(e); process.exit(1); });
