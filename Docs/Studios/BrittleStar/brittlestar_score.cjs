#!/usr/bin/env node
// brittlestar_score.cjs - the Brittle Star studio's scorecard, headless (a READER: it opens the page, writes nothing).
//
//   export NODE_PATH=/opt/node-tools/node_modules        # playwright-core; pre-installed browsers, never `playwright install`
//   curl -sSfL -o /tmp/three.min.js https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js
//   node Docs/Studios/BrittleStar/brittlestar_score.cjs Docs/Studios/VesselStudio/brittlestar.html /tmp/three.min.js
//
// Prints, for Demolition Derby and Fauna Frenzy: Easy / Medium / Hard / Hard-no-ring / Hard-no-modes per intensity
// (median of seeds 11/22/33, seconds) and the style table at Hard I3. It calls the page's own test hook
// (window.__brittleStarStudio.runScorecard / runStyles), so it measures exactly what the Scorecard tab shows.
// Results of 2026-10-14: Docs/Studios/BRITTLE_STAR.md section 5.1.
const fs = require('fs'), path = require('path');
let chromium; try { ({ chromium } = require('playwright')); } catch (e) { ({ chromium } = require('playwright-core')); }
const [page_, three] = process.argv.slice(2);
(async () => {
  const exe = fs.readdirSync('/opt/pw-browsers').filter(d => d.startsWith('chromium_headless_shell')).map(d => path.join('/opt/pw-browsers', d, 'chrome-linux/headless_shell'))[0];
  const b = await chromium.launch({ executablePath: exe, args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  const ctx = await b.newContext({ viewport: { width: 1200, height: 800 } });
  await ctx.route('**/three.min.js', r => r.fulfill({ path: three, contentType: 'application/javascript' }));
  const p = await ctx.newPage(); await p.goto('file://' + path.resolve(page_)); await p.waitForTimeout(800);
  const r = await p.evaluate(async () => {
    const s = window.__brittleStarStudio, out = {};
    for (const g of ['derby', 'frenzy']) {
      out[g] = await new Promise((res) => s.runScorecard(g, [11, 22, 33], res));
      out[g + 'Styles'] = await new Promise((res) => s.runStyles(g, [11, 22, 33], res));
    }
    return out;
  });
  const f = (x) => x == null ? 'DNF' : x.toFixed(1);
  for (const g of ['derby', 'frenzy']) {
    console.log('== ' + g + ': I | Easy Medium Hard NoRing NoModes');
    for (const I of [1, 2, 3, 4]) { const row = r[g][I]; console.log('I' + I, ['Easy', 'Medium', 'Hard', 'NoRing', 'NoModes'].map(k => f(row[k])).join(' | ')); }
    console.log('-- styles (Hard, I3): time destroyed rings speed creation destruction overdrive');
    for (const [k, v] of Object.entries(r[g + 'Styles'])) console.log(k, f(v.finish), v.destroyed, v.rings, ...['speed', 'creation', 'destruction', 'overdrive'].map(m => Math.round(v.share[m] * 100) + '%'));
  }
  await b.close();
})();
