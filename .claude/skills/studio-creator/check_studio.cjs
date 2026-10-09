#!/usr/bin/env node
// check_studio.cjs - the /studio-creator gate for one vessel studio page (Docs/Studios/VesselStudio/*.html).
//
//   node .claude/skills/studio-creator/check_studio.cjs <page.html> --hook __squirrelStudio [--out DIR] [--three FILE]
//   node .claude/skills/studio-creator/check_studio.cjs --self-test [--three FILE]
//
// A READER: it opens the page in headless Chromium, never edits it, writes screenshots into --out.
// Exit 0 = every check passed, 1 = a check failed (each one named), 2 = it could not run.
//
// Checks:
//   1. no console or page error, on a desktop (1280x860), a 400x900 window and an emulated phone held
//      sideways (844x390)
//   2. no sideways scroll at 400 px (the phone browser case)
//   3. the test hook (--hook) exists; when it has startRace() and a numeric state.t (getter or state()),
//      a started race advances it
//   4. the WebGL stage is not blank (more than one colour)
//   5. on an emulated phone at 844x390 the page is in play mode (body.play), either by itself or after a
//      real tap on "Play on phone" (#playBtn) - a tap that something covers is a failure
//
// --three FILE serves three.js from a local copy (headless machines often cannot reach the CDN).
// Fonts are stubbed so a blocked Google Fonts request is not an error.
// --self-test plants five defects into a minimal page (a page error, a 900 px element, a missing
// hook, a blank stage, a card lying over Play on phone) and requires the gate to name each one: a gate that has only ever passed is not a gate.
'use strict';
const fs = require('fs'), os = require('os'), path = require('path');

let chromium;
try { ({ chromium } = require('playwright')); }
catch (e) {
  try { ({ chromium } = require('playwright-core')); }
  catch (e2) { console.error('check_studio: cannot load playwright or playwright-core (' + e2.message + ')'); process.exit(2); }
}

function args(argv) {
  const a = { file: null, hook: null, out: null, three: null, selfTest: false };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    if (k === '--hook') a.hook = argv[++i];
    else if (k === '--out') a.out = argv[++i];
    else if (k === '--three') a.three = argv[++i];
    else if (k === '--self-test') a.selfTest = true;
    else if (!a.file) a.file = k;
    else { console.error('check_studio: unexpected argument ' + k); process.exit(2); }
  }
  return a;
}

function headlessShell() {
  // The pre-installed browsers (never `playwright install`): prefer a headless shell when one exists.
  const root = process.env.PLAYWRIGHT_BROWSERS_PATH || '/opt/pw-browsers';
  try {
    for (const d of fs.readdirSync(root)) {
      const p = path.join(root, d, 'chrome-linux', 'headless_shell');
      if (d.startsWith('chromium_headless_shell') && fs.existsSync(p)) return p;
    }
  } catch (e) { /* no browser folder: let playwright pick */ }
  return undefined;
}

async function check(browser, file, hook, out, three) {
  const fails = [];
  const url = 'file://' + path.resolve(file);
  fs.mkdirSync(out, { recursive: true });
  // Phones are emulated as phones (touch, mobile, an Android UA): a studio that detects its platform
  // shows the touch controls only to a phone, so a desktop browser shrunk to 844 px would hide them.
  const PHONE = { hasTouch: true, isMobile: true, userAgent: 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Mobile Safari/537.36' };
  async function open(viewport, label, phone) {
    const ctx = await browser.newContext(Object.assign({ viewport }, phone ? PHONE : {}));
    if (three) await ctx.route('**/three.min.js', (r) => r.fulfill({ path: three, contentType: 'application/javascript' }));
    await ctx.route('https://fonts.googleapis.com/**', (r) => r.fulfill({ body: '', contentType: 'text/css' }));
    const page = await ctx.newPage(), errs = [];
    page.on('console', (m) => { if (m.type() === 'error') errs.push(m.text()); });
    page.on('pageerror', (e) => errs.push(String(e && e.message || e)));
    await page.goto(url, { waitUntil: 'load' });
    await page.waitForTimeout(1500);
    return { ctx, page, errs, label };
  }
  const done = (o) => { for (const e of o.errs) fails.push(o.label + ': console/page error: ' + e); return o.ctx.close(); };

  // desktop: hook, race, stage
  const d = await open({ width: 1280, height: 860 }, 'desktop');
  if (hook) {
    const has = await d.page.evaluate((h) => !!window[h], hook);
    if (!has) fails.push('desktop: test hook window.' + hook + ' is missing');
    else {
      const r = await d.page.evaluate(async (h) => {
        const s = window[h];
        if (typeof s.startRace !== 'function') return { skipped: true };
        const st = () => { const v = typeof s.state === 'function' ? s.state() : s.state; return v && typeof v.t === 'number' ? v.t : null; };
        const t0 = st(); s.startRace();
        await new Promise((res) => setTimeout(res, 2000));
        return { t0, t1: st() };
      }, hook);
      if (!r.skipped && r.t1 !== null && !(r.t1 > (r.t0 || 0))) fails.push('desktop: startRace() did not advance state.t (' + JSON.stringify(r) + ')');
    }
  }
  // Read the stage from a real screenshot: a WebGL canvas without preserveDrawingBuffer reads back
  // empty through drawImage, so asking the canvas itself would call every three.js stage blank.
  let blank = 'no canvas';
  const canvasEl = await d.page.$('canvas');
  if (canvasEl) {
    const png = (await canvasEl.screenshot()).toString('base64');
    blank = await d.page.evaluate(async (b64) => {
      const img = new Image(); img.src = 'data:image/png;base64,' + b64; await img.decode();
      const g = document.createElement('canvas'); g.width = 64; g.height = 40;
      const x = g.getContext('2d'); x.drawImage(img, 0, 0, 64, 40);
      const px = x.getImageData(0, 0, 64, 40).data, seen = new Set();
      for (let i = 0; i < px.length; i += 4) seen.add(px[i] + ',' + px[i + 1] + ',' + px[i + 2]);
      return seen.size > 1 ? null : 'one colour';
    }, png);
  }
  if (blank) fails.push('desktop: the stage is blank (' + blank + ')');
  await d.page.screenshot({ path: path.join(out, 'desktop.png') });
  await done(d);

  // 400 px wide: no sideways scroll. Measured WITHOUT mobile emulation on purpose: an emulated phone
  // zooms out to fit wide content, which hides exactly the overflow this check exists to find.
  const p = await open({ width: 400, height: 900 }, 'phone 400');
  const wide = await p.page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  if (wide > 0) fails.push('phone 400: the page scrolls sideways by ' + wide + ' px');
  await p.page.screenshot({ path: path.join(out, 'phone_portrait.png'), fullPage: true });
  await done(p);

  // phone held sideways: Play on phone
  const l = await open({ width: 844, height: 390 }, 'phone 844x390', true);
  if (await l.page.evaluate(() => document.body.classList.contains('play'))) {
    // The page knew it was on a phone and went straight to touch play (the Stoat does): nothing to tap.
    await l.page.screenshot({ path: path.join(out, 'phone_play.png') });
  } else if (await l.page.$('#playBtn')) {
    // A real tap (mouse click events still fire on an emulated touch page), not a DOM click: a start card lying over the button is exactly the bug to catch.
    let tapped = true;
    try { await l.page.click('#playBtn', { timeout: 3000 }); }
    catch (e) {
      tapped = false;
      const why = /intercepts pointer events/.test(e.message) ? (e.message.match(/<[^>]+>[^\n]*intercepts pointer events/) || ['something covers it'])[0]
        : /not visible/.test(e.message) ? 'the button is not visible at this size' : e.message.split('\n')[0];
      fails.push('phone 844x390: Play on phone cannot be tapped (' + why + ')');
    }
    if (tapped) {
      await l.page.waitForTimeout(600);
      if (!(await l.page.evaluate(() => document.body.classList.contains('play')))) fails.push('phone 844x390: Play on phone did not enter play mode');
    }
    await l.page.screenshot({ path: path.join(out, 'phone_play.png') });
  } else fails.push('phone 844x390: no #playBtn (every studio has Play on phone)');
  await done(l);
  return fails;
}

const PLAIN = (body, script) => '<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width">' +
  '<style>body{margin:0;background:#111}canvas{width:300px;height:200px;display:block}body.play canvas{width:100%}</style>' +
  '<canvas id="c" width="300" height="200"></canvas><button id="playBtn">Play</button>' + body +
  '<script>const x=document.getElementById("c").getContext("2d");x.fillStyle="#222";x.fillRect(0,0,300,200);x.fillStyle="#3e8";x.fillRect(20,20,80,40);' +
  'document.getElementById("playBtn").onclick=()=>document.body.classList.add("play");' +
  'let t=0;setInterval(()=>t+=0.1,100);' + script + '</script>';

async function selfTest(browser, three) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'check_studio_')), hookJs = 'window.__s={get state(){return{t}},startRace(){}};';
  const cases = [
    ['clean page', PLAIN('', hookJs), null],
    ['page error', PLAIN('', hookJs + 'null.boom;'), /console\/page error/],
    ['900 px element', PLAIN('<div style="width:900px;height:4px"></div>', hookJs), /scrolls sideways/],
    ['missing hook', PLAIN('', ''), /test hook window.__s is missing/],
    ['blank stage', PLAIN('', hookJs + 'x.fillStyle="#222";x.fillRect(0,0,300,200);'), /stage is blank/],
    ['card over the button', PLAIN('<div style="position:fixed;inset:0;background:#0008"></div>', hookJs), /cannot be tapped/],
  ];
  let bad = 0;
  for (const [name, html, want] of cases) {
    const f = path.join(dir, name.replace(/\W+/g, '_') + '.html'); fs.writeFileSync(f, html);
    const fails = await check(browser, f, '__s', path.join(dir, 'out'), three);
    const ok = want ? fails.some((m) => want.test(m)) : fails.length === 0;
    console.log((ok ? 'ok   ' : 'FAIL ') + name + (want ? ' -> named' : ' -> passes') + (ok ? '' : ': ' + JSON.stringify(fails)));
    if (!ok) bad++;
  }
  return bad;
}

(async () => {
  const a = args(process.argv.slice(2));
  if (!a.selfTest && (!a.file || !a.hook)) { console.error('usage: check_studio.cjs <page.html> --hook __name [--out DIR] [--three FILE] | --self-test'); process.exit(2); }
  const browser = await chromium.launch({ executablePath: headlessShell(), args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  try {
    if (a.selfTest) { const bad = await selfTest(browser, a.three); process.exitCode = bad ? 1 : 0; return; }
    const out = a.out || fs.mkdtempSync(path.join(os.tmpdir(), 'studio_'));
    const fails = await check(browser, a.file, a.hook, out, a.three);
    for (const f of fails) console.log('FAIL ' + f);
    console.log(fails.length ? fails.length + ' check(s) failed' : 'all checks passed', '- screenshots in', out);
    process.exitCode = fails.length ? 1 : 0;
  } finally { await browser.close(); }
})().catch((e) => { console.error('check_studio: could not run: ' + e.message); process.exit(2); });
