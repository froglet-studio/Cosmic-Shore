// parity_probe.cjs - what each URL shows, for parity_gate.py --render (/vessel-studio D33).
//   node .claude/skills/vessel-studio/parity_probe.cjs URL [URL ...]   -> JSON { url: inventory } on stdout
// The inventory is what must be the same on every surface: visible tabs, buttons, selects, section titles and the Sync
// panel, plus any page error. Status lines (askState, reqState, logStatus, decNote) say what is enabled and may differ.
// Needs playwright (or playwright-core) and a Chrome: CHROME=/path/to/chrome. Exits 3 when either is missing.
// A GPU-less box renders with SwiftShader; NOGL=1 starts Chrome with WebGL off (the page must still load every tab).
let chromium;
try { ({ chromium } = require('playwright')); } catch (e) { try { ({ chromium } = require('playwright-core')); } catch (e2) { console.error('playwright is not installed'); process.exit(3); } }
const fs = require('fs');
const exe = process.env.CHROME || ['/opt/google/chrome/chrome', '/usr/bin/google-chrome', '/usr/bin/chromium'].find((p) => fs.existsSync(p));
(async () => {
  const args = process.env.NOGL ? ['--disable-webgl', '--disable-3d-apis'] : ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'];
  let b;
  try { b = await chromium.launch(Object.assign({ args }, exe ? { executablePath: exe } : {})); } catch (e) { console.error('no Chrome: ' + e.message.split('\n')[0]); process.exit(3); }
  const out = {};
  for (const url of process.argv.slice(2)) {
    const pg = await b.newPage({ viewport: { width: 1600, height: 960 } });
    const pageErrors = [];
    pg.on('pageerror', (e) => pageErrors.push(e.message.slice(0, 160)));
    try { await pg.goto(url, { waitUntil: 'networkidle', timeout: 45000 }); } catch (e) { pageErrors.push('load: ' + e.message.slice(0, 100)); }
    await pg.waitForTimeout(2500);
    out[url] = await pg.evaluate(() => {
      const vis = (el) => { const r = el.getBoundingClientRect(); const s = getComputedStyle(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none'; };
      const txt = (el) => (el.textContent || el.value || '').replace(/\s+/g, ' ').trim().slice(0, 40);
      const uniq = (a) => [...new Set(a.filter(Boolean))].sort();
      const id = (i) => { const e = document.getElementById(i); return e ? txt(e) : null; };
      return {
        tabs: uniq([...document.querySelectorAll('[role=tab], .tab, .tabs button')].filter(vis).map(txt)),
        buttons: uniq([...document.querySelectorAll('button, summary')].filter(vis).map(txt)),
        selects: uniq([...document.querySelectorAll('select')].filter(vis).map((s) => s.id || s.name || txt(s))),
        sections: uniq([...document.querySelectorAll('h1, h2, h3, legend')].filter(vis).map(txt)),
        syncPanel: !!document.getElementById('vs-sync'),
        noWebGL: !!document.querySelector('.vs-nogl'),
        status: { askState: id('askState'), reqState: id('reqState'), logStatus: id('logStatus') },
      };
    });
    out[url].pageErrors = pageErrors;
    if (process.env.SHOTS) await pg.screenshot({ path: process.env.SHOTS + '/' + Object.keys(out).length + '.png' });
    await pg.close();
  }
  await b.close();
  process.stdout.write(JSON.stringify(out, null, 1));
})();
