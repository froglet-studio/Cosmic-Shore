// bake_previews.cjs - the hub's live card previews as PNG thumbnails for the native homes (/vessel-studio D34).
//
// The web hub (Docs/Studios/VesselStudio/index.html) draws each studio card's preview live on a 2D canvas. Amoebius's
// VESSEL STUDIO page (ImGui) and Unity's studio home show the same card, so they show this picture of it: the hub's own
// canvas, rendered headless on a fixed clock (the same frame every run), saved as previews/<studio id>.png next to
// studios.json ("preview"). previews/previews.json stamps the hash of the preview code it was baked from, and
// parity_gate.py fails when the hub's preview code changes without a re-bake.
//
//   export NODE_PATH=<a folder with node_modules/playwright-core>
//   node .claude/skills/vessel-studio/bake_previews.cjs [--root <checkout>] [--seconds 1.2]
'use strict';
const fs = require('fs'), path = require('path'), { execFileSync } = require('child_process');
const { chromium } = require('playwright-core');

const argv = process.argv.slice(2);
const opt = (k, d) => { const i = argv.indexOf(k); return i >= 0 ? argv[i + 1] : d; };
const root = path.resolve(opt('--root', '.'));
const seconds = Number(opt('--seconds', '1.2'));
const dir = path.join(root, 'Docs/Studios/VesselStudio');

function headless() {
  const base = process.env.PLAYWRIGHT_BROWSERS_PATH;
  if (base) for (const d of fs.readdirSync(base)) {
    for (const exe of ['chrome-linux/headless_shell', 'chrome-headless-shell-linux64/chrome-headless-shell', 'chrome-linux64/chrome'])
      if (fs.existsSync(path.join(base, d, exe))) return path.join(base, d, exe);
  }
  return undefined;   // playwright's own
}

(async () => {
  const cat = JSON.parse(fs.readFileSync(path.join(dir, 'studios.json'), 'utf8'));
  const browser = await chromium.launch({ executablePath: headless() });
  try {
    const page = await browser.newPage({ viewport: { width: 1200, height: 900 } });
    await page.route(/^https?:\/\//, (r) => r.fulfill({ body: '', contentType: /css/.test(r.request().url()) ? 'text/css' : 'text/plain' }));
    // a fixed clock: the page's animation frames run only when we step them, so every bake draws the same frame
    await page.addInitScript(() => {
      let now = 0; const q = [];
      performance.now = () => now;
      window.requestAnimationFrame = (cb) => { q.push(cb); return q.length; };
      window.__stepFrames = (n, ms) => { for (let i = 0; i < n; i++) { now += ms; const run = q.splice(0); for (const cb of run) cb(now); } };
      window.matchMedia = ((mm) => (qry) => (/reduced-motion/.test(qry) ? { matches: false, addListener() {}, addEventListener() {} } : mm.call(window, qry)))(window.matchMedia);
    });
    await page.goto('file://' + path.join(dir, cat.hub || 'index.html'), { waitUntil: 'load' });
    const frames = Math.round(seconds * 60);
    await page.evaluate((n) => window.__stepFrames(n, 1000 / 60), frames);
    const shots = await page.evaluate(() => [...document.querySelectorAll('a.bay')].map((a) => {
      const c = a.querySelector('canvas'); return { file: a.getAttribute('href'), png: c ? c.toDataURL('image/png') : null };
    }));
    fs.mkdirSync(path.join(dir, 'previews'), { recursive: true });
    for (const s of cat.studios) {
      const shot = shots.find((x) => x.file === s.file);
      if (!shot || !shot.png) { console.error('no hub card with a canvas for ' + s.id); process.exitCode = 1; continue; }
      const out = path.join(dir, s.preview || ('previews/' + s.id + '.png'));
      fs.writeFileSync(out, Buffer.from(shot.png.split(',')[1], 'base64'));
      console.log('baked ' + path.relative(root, out));
    }
    const hash = execFileSync('python3', [path.join(__dirname, 'studio_cards.py'), '--hash', root], { encoding: 'utf8' }).trim();
    fs.writeFileSync(path.join(dir, 'previews', 'previews.json'), JSON.stringify({ source: hash, seconds, size: '640x300' }, null, 2) + '\n');
  } finally { await browser.close(); }
})();
