#!/usr/bin/env node
// verify_lab.cjs — the /labmaker contract gate for a single-file browser lab.
//
//   node .claude/skills/labmaker/verify_lab.cjs <lab.html> [--out <dir>] [--self-test]
//
// A READER: it opens the page in headless Chromium and writes screenshots + a JSON report into
// --out (default: a scratch dir under $TMPDIR). It never edits the lab. Exit 0 = every check
// passed, 1 = a check failed (each failure is named), 2 = it could not run (bad args, no browser).
//
// What it checks (the contract in .claude/skills/labmaker/SKILL.md §3):
//   1. loads with NO console errors / page errors, at desktop 1600×900 and an emulated phone
//   2. desktop: the layout fits the window — measured as scrollHeight vs clientHeight, so it also
//      catches content CLIPPED by body{overflow:hidden}, which no scrollbar would show; phone: no
//      horizontal scroll
//   3. window.__lab exposes the test surface: SHIPPED, SPEC, P, tick, reset, score, runBatch, state
//   4. every SPEC key exists in SHIPPED and every shipped value lies inside its slider range
//   5. the manual clock advances the model (state().t moves under tick, not under wall time)
//   6. runBatch is deterministic: two calls give byte-identical rows
//   7. the stage is not blank after ticking (the canvas has more than one colour)
//
// --self-test plants four defects into a copy of the template (a console error, a SHIPPED
// value outside its range, a hook that throws, a non-deterministic batch) and requires the gate
// to name each one —
// a gate that has only ever passed is not a gate.
'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');

let chromium, devices;
try { ({ chromium, devices } = require('playwright')); }
catch (e) { console.error('verify_lab: cannot load playwright (' + e.message + ')'); process.exit(2); }

function parseArgs(argv) {
  const a = { file: null, out: null, selfTest: false };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--out') a.out = argv[++i];
    else if (argv[i] === '--self-test') a.selfTest = true;
    else if (!a.file) a.file = argv[i];
    else { console.error('verify_lab: unexpected argument ' + argv[i]); process.exit(2); }
  }
  return a;
}

async function verify(file, outDir, browser) {
  const failures = [], notes = [];
  const url = 'file://' + path.resolve(file);
  fs.mkdirSync(outDir, { recursive: true });

  async function open(ctxOpts, label) {
    const ctx = await browser.newContext(ctxOpts);
    const page = await ctx.newPage();
    const errors = [];
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
    page.on('pageerror', (e) => errors.push(String(e && e.message || e)));
    await page.goto(url, { waitUntil: 'load' });
    await page.waitForTimeout(400);
    for (const e of errors) failures.push(label + ': console/page error: ' + e);
    return { ctx, page, errors };
  }

  // ---- desktop ----
  const d = await open({ viewport: { width: 1600, height: 900 } }, 'desktop');
  const hasLab = await d.page.evaluate(() => !!window.__lab);
  if (!hasLab) failures.push('window.__lab is missing — the test surface is the contract (SKILL.md §3.2)');
  else {
    const r = await d.page.evaluate(() => {
      const L = window.__lab, out = { missing: [], spec: [], scroll: null, clock: null, det: null, blank: null, threw: [] };
      // A hook that throws is a named failure, never a crash of the verifier.
      const call = (name, fn) => { try { return fn(); } catch (e) { out.threw.push(name + ' threw: ' + (e && e.message || e)); return undefined; } };
      for (const k of ['SHIPPED', 'SPEC', 'P', 'tick', 'reset', 'score', 'runBatch', 'state']) if (L[k] == null) out.missing.push(k);
      if (L.SPEC && L.SHIPPED) for (const row of L.SPEC) {
        const [, key, lo, hi] = row;
        if (!(key in L.SHIPPED)) out.spec.push(key + ' is a slider but not in SHIPPED');
        else if (L.SHIPPED[key] < lo || L.SHIPPED[key] > hi) out.spec.push(key + ' shipped ' + L.SHIPPED[key] + ' outside its range ' + lo + '…' + hi);
      }
      const se = document.scrollingElement || document.documentElement;
      out.scroll = { h: se.scrollHeight - se.clientHeight, w: se.scrollWidth - se.clientWidth };
      if (typeof L.tick === 'function' && typeof L.state === 'function') {
        call('manualClock', () => { L.manualClock = true; });
        call('reset(7)', () => L.reset(7));
        const t0 = call('state()', () => L.state().t); call('tick(1/60, 120)', () => L.tick(1 / 60, 120)); const t1 = call('state()', () => L.state().t);
        if (t0 !== undefined && t1 !== undefined) out.clock = { t0, t1 };
      }
      if (typeof L.runBatch === 'function') {
        const a = call('runBatch()', () => JSON.stringify(L.runBatch()));
        const b = a === undefined ? undefined : call('runBatch()', () => JSON.stringify(L.runBatch()));
        if (a !== undefined && b !== undefined) out.det = [a, b];
      }
      const c = document.querySelector('canvas');
      if (c) {
        try {
          const g = c.getContext('2d');
          if (g) {
            const px = g.getImageData(0, 0, c.width, c.height).data, seen = new Set();
            for (let i = 0; i < px.length && seen.size < 3; i += 4 * 97) seen.add(px[i] + ',' + px[i + 1] + ',' + px[i + 2]);
            out.blank = seen.size < 2;
          } else out.blank = 'webgl';
        } catch (e) { out.blank = 'unreadable'; }
      }
      return out;
    });
    for (const k of r.missing) failures.push('window.__lab.' + k + ' is missing');
    for (const t of r.threw) failures.push('hook: ' + t);
    for (const s of r.spec) failures.push('SPEC/SHIPPED: ' + s);
    if (r.scroll.h > 1 || r.scroll.w > 1) failures.push('desktop: the layout overflows the window by ' + r.scroll.w + '×' + r.scroll.h + ' px (it scrolls, or with overflow:hidden it is CLIPPED) — the stage and docks must fit');
    if (r.clock && !(r.clock.t1 > r.clock.t0)) failures.push('manual clock: state().t did not advance under tick (' + r.clock.t0 + ' → ' + r.clock.t1 + ')');
    if (r.det && r.det[0] !== r.det[1]) failures.push('runBatch is not deterministic:\n    ' + r.det[0] + '\n    ' + r.det[1]);
    if (r.blank === true) failures.push('stage: the canvas is a single colour after 120 ticks');
    if (r.blank === 'webgl') notes.push('stage is WebGL; blank check skipped — read the screenshot');
    if (r.det) notes.push('batch: ' + r.det[0]);
  }
  await d.page.screenshot({ path: path.join(outDir, 'desktop.png') });
  await d.ctx.close();

  // ---- phone ----
  const p = await open(Object.assign({}, devices['iPhone 13']), 'phone');
  const ps = await p.page.evaluate(() => { const se = document.scrollingElement || document.documentElement; return { w: se.scrollWidth - se.clientWidth, device: window.__lab && window.__lab.PLATFORM && window.__lab.PLATFORM.device }; });
  if (ps.w > 1) failures.push('phone: horizontal scroll of ' + ps.w + ' px at 390 px wide');
  if (ps.device && ps.device !== 'phone') failures.push('phone: PLATFORM.device reads "' + ps.device + '" on an emulated iPhone 13');
  await p.page.screenshot({ path: path.join(outDir, 'phone.png'), fullPage: true });
  await p.ctx.close();

  const report = { file: path.resolve(file), ok: failures.length === 0, failures, notes, screenshots: ['desktop.png', 'phone.png'] };
  fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify(report, null, 2));
  return report;
}

async function selfTest(browser, outRoot) {
  const tpl = path.join(__dirname, 'template', 'lab.html');
  const src = fs.readFileSync(tpl, 'utf8');
  const plants = [
    { name: 'console error', expect: /console\/page error/, edit: (s) => s.replace("'use strict';", "'use strict'; console.error('planted');") },
    { name: 'shipped out of range', expect: /outside its range/, edit: (s) => s.replace('damping: 0.4,', 'damping: 9,') },
    { name: 'hook throws', expect: /hook: runBatch\(\) threw/, edit: (s) => s.replace("opts = opts || {};", "opts = opts || {}; if (!opts.qs) throw new TypeError('planted: runBatch needs qs');") },
    { name: 'nondeterministic batch', expect: /not deterministic/, edit: (s) => s.replace("rows.push({ variant: va.name, caughtPct:", "rows.push({ jitter: Math.random(), variant: va.name, caughtPct:") },
  ];
  let ok = true;
  const clean = await verify(tpl, path.join(outRoot, 'clean'), browser);
  console.log((clean.ok ? 'PASS' : 'FAIL') + '  clean template' + (clean.ok ? '' : '\n  ' + clean.failures.join('\n  ')));
  ok = ok && clean.ok;
  for (const pl of plants) {
    const edited = pl.edit(src);
    if (edited === src) { console.log('FAIL  plant "' + pl.name + '" did not apply — the template moved; update the self-test'); ok = false; continue; }
    const f = path.join(outRoot, pl.name.replace(/\W+/g, '_') + '.html');
    fs.writeFileSync(f, edited);
    const r = await verify(f, path.join(outRoot, pl.name.replace(/\W+/g, '_')), browser);
    const caught = !r.ok && r.failures.some((x) => pl.expect.test(x));
    console.log((caught ? 'PASS' : 'FAIL') + '  planted ' + pl.name + (caught ? ' → named' : ' → NOT caught'));
    ok = ok && caught;
  }
  return ok;
}

(async () => {
  const a = parseArgs(process.argv.slice(2));
  if (!a.selfTest && !a.file) { console.error('usage: verify_lab.cjs <lab.html> [--out dir] | --self-test'); process.exit(2); }
  if (a.file && !fs.existsSync(a.file)) { console.error('verify_lab: no such file ' + a.file); process.exit(2); }
  const out = a.out || fs.mkdtempSync(path.join(os.tmpdir(), 'verify_lab-'));
  let browser;
  try { browser = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] }); }
  catch (e) { console.error('verify_lab: cannot launch chromium (' + e.message.split('\n')[0] + ')'); process.exit(2); }
  try {
    if (a.selfTest) { const ok = await selfTest(browser, out); console.log((ok ? 'self-test OK' : 'self-test FAILED') + ' — artifacts in ' + out); process.exitCode = ok ? 0 : 1; return; }
    const r = await verify(a.file, out, browser);
    for (const n of r.notes) console.log('note  ' + n);
    if (r.ok) console.log('PASS  ' + a.file + ' — screenshots + report in ' + out);
    else { console.log('FAIL  ' + a.file); for (const f of r.failures) console.log('  - ' + f); console.log('report in ' + out); }
    process.exitCode = r.ok ? 0 : 1;
  } finally { await browser.close(); }
})();
