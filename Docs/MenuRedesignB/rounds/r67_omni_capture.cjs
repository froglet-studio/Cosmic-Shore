// Garrett's omni combine, captured from the Omni Combine Lab itself (Docs/Studios/OmniCombineLab.html, the source the
// game's OmniCombinePlayer is a line-for-line port of): its four crystals idling, then the whole combine, stepped on
// the lab's own manual clock at 10 fps and saved with a transparent background so it can be laid over the Ark.
//   node r67_omni_capture.cjs <lab.html> <out_dir> [w h pitch]   (pitch: the lab's 0.35; 0.12 from inside the Ark)
//   VD=<omni radii> sets the lab's camera distance (7 by default; Rev 104 used 10 so no crystal leaves the frame)
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const [, , lab, out, W = '960', H = '540', PITCH_ARG = '0.35'] = process.argv;
(async () => {
  fs.mkdirSync(path.join(out, 'idle'), { recursive: true });
  fs.mkdirSync(path.join(out, 'combine'), { recursive: true });
  const b = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-webgl', '--ignore-gpu-blocklist'] });
  const p = await b.newPage({ viewport: { width: +W, height: +H } });
  await p.addInitScript(() => {
    // clear to transparent instead of the lab's stage colour
    const cc = WebGLRenderingContext.prototype.clearColor;
    WebGLRenderingContext.prototype.clearColor = function () { return cc.call(this, 0, 0, 0, 0); };
  });
  await p.goto('file://' + path.resolve(lab), { waitUntil: 'load' });
  await p.addStyleTag({ content: `html,body,#app,#stageWrap{background:transparent!important}
    header,#dock,#bottom,#scrub,#hud{display:none!important}
    #app{display:block!important;height:100vh!important}
    #stageWrap{position:fixed!important;inset:0!important}
    #stage{width:100vw!important;height:100vh!important}` });
  await p.waitForTimeout(800);
  await p.evaluate((vd) => { const L = window.__lab; L.manualClock = true; L.setParam && L.setParam('viewSpin', 0); L.setParam && L.setParam('viewDist', vd); }, +(process.env.VD || 7));
  const shot = async (file) => {
    const el = await p.$('#stage');
    await el.screenshot({ path: file, omitBackground: true });
  };
  const YAW = 0.6, PITCH = +PITCH_ARG, SPIN = 6 * Math.PI / 180;
  // idle: the four crystals each doing their effect, before the first sets off (the lab's lead is 2.5 s)
  await p.evaluate(() => window.__lab.reset());
  for (let f = 0; f < 25; f++) {
    await p.evaluate(({ yaw, pitch, f }) => { const L = window.__lab; L.setCamera(yaw, pitch); L.tick(f === 0 ? 0 : 0.1, 1); }, { yaw: YAW, pitch: PITCH, f });
    await shot(path.join(out, 'idle', `f_${String(f).padStart(4, '0')}.png`));
  }
  // the combine: from rest to the omni awake and held (13.7 s), the lab's camera turning slowly as it does
  await p.evaluate(() => window.__lab.reset());
  const L = await p.evaluate(() => window.__lab.morphEnds(window.__lab.P) + window.__lab.P.hold);
  const n = Math.round(L * 10);
  for (let f = 0; f < n; f++) {
    await p.evaluate(({ yaw, pitch, f }) => { const L = window.__lab; L.setCamera(yaw, pitch); L.tick(f === 0 ? 0 : 0.1, 1); },
      { yaw: YAW + SPIN * f * 0.1, pitch: PITCH, f });
    await shot(path.join(out, 'combine', `f_${String(f).padStart(4, '0')}.png`));
  }
  console.log('idle 25, combine', n);
  await b.close();
})();
