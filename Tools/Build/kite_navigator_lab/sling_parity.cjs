#!/usr/bin/env node
// sling_parity.cjs - holds the Wrasse Lasso Lab's SLING (Docs/Studios/WrasseLassoLab.html: stepKite 'sling', ropeForce,
// rimBounce, advanceTail) to the SHIPPED C# KiteNavigatorCore.Sling + AdvanceTail, frame by frame, over scripted thumbs.
//
//   node Tools/Build/kite_navigator_lab/sling_parity.cjs
//
// 1. Writes the scenarios and runs trace.sh: the shipped C#, compiled from its real path.
// 2. Runs the SAME scenarios through window.__lab.slingTrace, i.e. through the functions the page itself flies.
// 3. Compares every frame: position and velocity within tolerance, tail length and every closed loop equal.
// 4. Planted controls (rope 42 -> 40, stiffness 260 -> 240, drag 0.35 -> 0.45) MUST fail.
// 5. The shipped asset's sling fields against the page's SHIPPED numbers, with a planted control.
// The page's board is centred on the origin, so the C# runs with anchor 0 (the core is otherwise translation-free).
'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');
const { chromium } = require('playwright');

const ROOT = path.resolve(__dirname, '../../..');
const PAGE = path.join(ROOT, 'Docs/Studios/WrasseLassoLab.html');
const ASSET = path.join(ROOT, 'Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset');
const DT = 1 / 60, TOL = 0.05, REL = 2e-4;
// asset field -> the lab key it came from (KiteNavigatorConfigSO's tooltips name the same keys)
const ASSET_KEYS = { slingRope: 'slRope', slingStiffness: 'slStiff', slingDamping: 'slDamp', slingDrag: 'slDrag', slingFreeDrag: 'slFreeDrag', slingMaxSpeed: 'slMaxSpeed', tailLength: 'tailLength', boardRadius: 'boardRadius' };

function readAsset() {
  const out = {};
  for (const line of fs.readFileSync(ASSET, 'utf8').split('\n')) { const m = /^  (\w+): (-?[\d.]+)\s*$/.exec(line); if (m) out[m[1]] = Number(m[2]); }
  return out;
}
function assetDrift(asset, labP) {
  const bad = [];
  for (const [k, lk] of Object.entries(ASSET_KEYS)) if (!(k in asset) || Math.abs(asset[k] - labP[lk]) > 1e-6) bad.push(`${k} ${asset[k]} vs lab ${lk} ${labP[lk]}`);
  if (asset.pointerControl !== 1) bad.push(`pointerControl ${asset.pointerControl} (1 = Sling, the designer's pick)`);
  return bad;
}

function scenarios(labP) {
  const P = { planeRange: labP.boardRadius, anchor: 0, kiteSpeed: 320, kiteAccel: 12, tailLen: labP.tailLength,
    slRope: labP.slRope, slStiff: labP.slStiff, slDamp: labP.slDamp, slDrag: labP.slDrag, slFreeDrag: labP.slFreeDrag, slMaxSpeed: labP.slMaxSpeed };
  const out = [];
  const add = (name, frames, f) => { const aims = []; for (let i = 0; i < frames; i++) aims.push(f(i * DT)); out.push({ name, control: 'sling', dt: DT, P, aims }); };
  // a small hand circle leading the kite: the whirl
  add('whirl', 600, (t) => [Math.cos(t * 7) * 10, Math.sin(t * 7) * 10, 1]);
  add('whirl-drifting', 720, (t) => [Math.cos(t * 6.5) * 12 + t * 8, Math.sin(t * 6.5) * 12 - t * 6, 1]);
  add('tow', 480, (t) => [-100 + t * 50, 60 * Math.sin(t), 1]);
  add('let-go', 480, (t) => [Math.cos(t * 7) * 10, Math.sin(t * 7) * 10, t < 4 ? 1 : 0]);
  add('yank-to-rim', 360, (t) => [t < 2 ? 140 : -140, 0, (Math.floor(t * 2) % 3) ? 1 : 0]);
  add('taps', 600, (t) => [Math.cos(t) * 80, Math.sin(t * 1.3) * 80, (Math.floor(t * 3) % 2)]);
  return out;
}

function compare(refAll, gotAll) {
  let worst = 0, where = '', structural = 0, frames = 0, loops = 0;
  for (let s = 0; s < refAll.length; s++) {
    const ref = refAll[s], got = gotAll[s];
    for (let f = 0; f < ref.length; f++) {
      const a = ref[f], b = got[f]; frames++;
      if (a.loop) loops++;
      const tol = TOL + REL * Math.hypot(a.x, a.z) * f;
      const err = Math.max(Math.abs(a.x - b.x), Math.abs(a.z - b.z), Math.abs(a.vx - b.vx) * DT, Math.abs(a.vz - b.vz) * DT);
      if (err / tol > worst) { worst = err / tol; where = `scenario ${s} frame ${f}`; }
      if (a.tail !== b.tail || a.loop !== b.loop) { if (!structural) where = `scenario ${s} frame ${f}: tail ${a.tail}/${b.tail} loop ${a.loop}/${b.loop}`; structural++; }
    }
  }
  return { ok: worst <= 1 && structural === 0, worst, where, structural, frames, loops };
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  page.on('pageerror', (e) => { console.error('page error:', e.message); process.exitCode = 1; });
  await page.goto('file://' + PAGE);
  const labP = await page.evaluate(() => Object.assign({}, window.__lab.SHIPPED));
  const scn = { scenarios: scenarios(labP) };
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'sling-parity-'));
  const file = path.join(tmp, 'scenarios.json');
  fs.writeFileSync(file, JSON.stringify(scn));
  const ref = JSON.parse(execFileSync('bash', [path.join(__dirname, 'trace.sh'), file], { encoding: 'utf8', maxBuffer: 1 << 28 }));
  const toLab = (P) => ({ boardRadius: P.planeRange, tailLength: P.tailLen, slRope: P.slRope, slStiff: P.slStiff, slDamp: P.slDamp, slDrag: P.slDrag, slFreeDrag: P.slFreeDrag, slMaxSpeed: P.slMaxSpeed, labSnap: 0 });
  const run = (planted) => page.evaluate(({ scn, planted }) => scn.scenarios.map((s) => window.__lab.slingTrace(Object.assign({}, s, { P: Object.assign({}, s.labP, planted) }))), { scn: { scenarios: scn.scenarios.map((s) => Object.assign({}, s, { labP: toLab(s.P) })) }, planted });

  const main = compare(ref, await run({}));
  console.log(`sling parity: ${main.ok ? 'PASS' : 'FAIL'} - ${main.frames} frames, ${main.loops} loops closed, worst ${main.worst.toFixed(3)} x tolerance (${main.where})` + (main.structural ? `, ${main.structural} structural mismatches` : ''));
  if (!main.ok) process.exitCode = 1;
  if (main.loops < 10) { console.log('FAIL: the scenarios closed fewer than 10 loops, so the loop path is not exercised'); process.exitCode = 1; }

  for (const [label, planted] of [['slRope 42 -> 40', { slRope: 40 }], ['slStiff 260 -> 240', { slStiff: 240 }], ['slDrag 0.35 -> 0.45', { slDrag: 0.45 }]]) {
    const r = compare(ref, await run(planted));
    console.log(`control ${label}: ${r.ok ? 'NOT CAUGHT (gate is blind)' : 'caught'} (worst ${r.worst.toFixed(1)} x tolerance, ${r.structural} structural)`);
    if (r.ok) process.exitCode = 1;
  }

  const asset = readAsset(), drift = assetDrift(asset, labP);
  console.log(`asset: ${drift.length ? 'FAIL - ' + drift.join('; ') : 'PASS - ' + (Object.keys(ASSET_KEYS).length + 1) + ' fields equal the lab'}`);
  if (drift.length) process.exitCode = 1;
  const planted = assetDrift(Object.assign({}, asset, { slingRope: asset.slingRope + 1 }), labP);
  console.log(`control asset slingRope +1: ${planted.length ? 'caught' : 'NOT CAUGHT (gate is blind)'}`);
  if (!planted.length) process.exitCode = 1;
  await browser.close();
})();
