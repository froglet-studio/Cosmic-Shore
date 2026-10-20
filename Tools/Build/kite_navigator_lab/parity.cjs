#!/usr/bin/env node
// parity.cjs - holds the Crew Vessel Lab's kite core (Docs/Studios/CrewVesselLab.html: kiteMove, tailAdvance) to the
// SHIPPED C# KiteNavigatorCore, frame by frame, over scripted thumbs.
//
//   node Tools/Build/kite_navigator_lab/parity.cjs
//
// 1. Writes the scenarios (deterministic) and runs trace.sh: the shipped C#, compiled from its real path.
// 2. Opens the lab page in headless Chromium and runs the SAME scenarios through window.__lab.parityTrace, i.e. through
//    the functions the page itself flies.
// 3. Compares every frame: kite position and velocity within TOL, tail length and every closed loop equal.
// 4. Negative controls: the page re-run with a planted change (kiteAccel 12 -> 11, tailLen 1.6 -> 1.45, kiteSpeed 320 ->
//    300) MUST fail. A gate that cannot see a few-percent dial change is not a gate.
// 5. The shipped asset (KiteNavigatorConfig.asset) against the page's own numbers, key by key, with a planted control.
'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');
const { chromium } = require('playwright');

const ROOT = path.resolve(__dirname, '../../..');
const PAGE = path.join(ROOT, 'Docs/Studios/CrewVesselLab.html');
// The scenarios' kite numbers: round 3's (the shipped tail is 2.4 since 2026-10-18; the core is length-agnostic). The
// asset is held to the PAGE's own numbers below, not to these.
const P = { planeRange: 160, anchor: 0.3, kiteSpeed: 320, kiteAccel: 12, tailLen: 1.6 };
const DT = 1 / 60, TOL = 0.02, REL = 1e-4;
const ASSET = path.join(ROOT, 'Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset');
// asset field -> the lab key it came from (KiteNavigatorConfigSO's tooltips name the same keys)
const ASSET_KEYS = {
  boardRadius: 'planeRange', anchor: 'anchor', easeRate: 'easeRate', reachY: 'reachY', kiteRadius: 'kiteRadius',
  kiteSpeed: 'kiteSpeed', kiteAccel: 'kiteAccel', tailLength: 'tailLen', moteCount: 'moteCount', moteCluster: 'moteCluster',
  growth: 'growth', spaceMaxScale: 'spaceScale', spaceMaxScaleCap: 'spaceCap', massPrisms: 'massPrisms', massPrismCap: 'massCap',
  timeBoostSeconds: 'timeBoost', timeBoostCap: 'timeCap', chargePetals: 'chargePetals', chargeCap: 'chargeCap',
  stealCooldown: 'stealCooldown',
};

function readAsset() {
  const out = {};
  for (const line of fs.readFileSync(ASSET, 'utf8').split('\n')) { const m = /^  (\w+): (-?[\d.]+)\s*$/.exec(line); if (m) out[m[1]] = Number(m[2]); }
  return out;
}

function assetDrift(asset, labP) {
  const bad = [];
  for (const [k, lk] of Object.entries(ASSET_KEYS)) if (!(k in asset) || Math.abs(asset[k] - labP[lk]) > 1e-6) bad.push(`${k} ${asset[k]} vs lab ${lk} ${labP[lk]}`);
  // the lab's thumb offset is CSS px (96 per inch); the asset's is millimetres of glass
  if (Math.abs(asset.thumbOffsetMillimetres - Math.round(labP.thumbOffset / 96 * 25.4)) > 1e-6) bad.push(`thumbOffsetMillimetres ${asset.thumbOffsetMillimetres} vs lab ${labP.thumbOffset} px`);
  return bad;
}

function scenarios() {
  const A = P.anchor * P.planeRange, out = [];
  const add = (name, frames, f) => { const aims = []; for (let i = 0; i < frames; i++) aims.push(f(i * DT)); out.push({ name, dt: DT, P, aims }); };
  add('circle', 600, (t) => [Math.cos(t * 3) * 45, A + Math.sin(t * 3) * 45, 1]);
  add('figure8', 720, (t) => [Math.sin(t * 2) * 70, A + Math.sin(t * 4) * 35, 1]);
  add('tight-circle', 480, (t) => [20 + Math.cos(t * 5) * 22, A + 30 + Math.sin(t * 5) * 22, 1]);
  add('lift-and-coast', 360, (t) => [60, A + 80, t < 2 ? 1 : 0]);
  add('past-the-rim', 360, (t) => [t < 3 ? 400 : -400, A + 300, 1]);
  add('zigzag', 600, (t) => [((Math.floor(t * 2) % 2) ? 80 : -80), A - 60 + t * 20, 1]);
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
  const scn = { scenarios: scenarios() };
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'kite-parity-'));
  const file = path.join(tmp, 'scenarios.json');
  fs.writeFileSync(file, JSON.stringify(scn));
  const ref = JSON.parse(execFileSync('bash', [path.join(__dirname, 'trace.sh'), file], { encoding: 'utf8', maxBuffer: 1 << 28 }));

  const browser = await chromium.launch();
  const page = await browser.newPage();
  page.on('pageerror', (e) => { console.error('page error:', e.message); process.exitCode = 1; });
  await page.goto('file://' + PAGE);
  const run = (planted) => page.evaluate(({ scn, planted }) => scn.scenarios.map((s) => window.__lab.parityTrace(Object.assign({}, s, { P: Object.assign({}, s.P, planted) }))), { scn, planted });

  const main = compare(ref, await run({}));
  console.log(`parity: ${main.ok ? 'PASS' : 'FAIL'} - ${main.frames} frames, ${main.loops} loops closed, worst ${main.worst.toFixed(3)} x tolerance (${main.where})` + (main.structural ? `, ${main.structural} structural mismatches` : ''));
  if (!main.ok) process.exitCode = 1;
  if (main.loops < 10) { console.log('FAIL: the scenarios closed fewer than 10 loops, so the loop path is not exercised'); process.exitCode = 1; }

  for (const [label, planted] of [['kiteAccel 12 -> 11', { kiteAccel: 11 }], ['tailLen 1.6 -> 1.45', { tailLen: 1.45 }], ['kiteSpeed 320 -> 300', { kiteSpeed: 300 }]]) {
    const r = compare(ref, await run(planted));
    console.log(`control ${label}: ${r.ok ? 'NOT CAUGHT (gate is blind)' : 'caught'} (worst ${r.worst.toFixed(1)} x tolerance, ${r.structural} structural)`);
    if (r.ok) process.exitCode = 1;
  }

  const labP = await page.evaluate(() => Object.assign({}, window.__lab.P));
  const asset = readAsset(), drift = assetDrift(asset, labP);
  console.log(`asset: ${drift.length ? 'FAIL - ' + drift.join('; ') : 'PASS - ' + (Object.keys(ASSET_KEYS).length + 1) + ' fields equal the lab'}`);
  if (drift.length) process.exitCode = 1;
  const planted = assetDrift(Object.assign({}, asset, { growth: asset.growth + 0.1 }), labP);
  console.log(`control asset growth +0.1: ${planted.length ? 'caught' : 'NOT CAUGHT (gate is blind)'}`);
  if (!planted.length) process.exitCode = 1;
  await browser.close();
})();
