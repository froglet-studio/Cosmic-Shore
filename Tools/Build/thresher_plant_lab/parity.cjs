#!/usr/bin/env node
// parity.cjs — holds the Thresher Plant Lab's JS solver to the SHIPPED C# ThresherChainSolver, frame by frame.
//
//   node Tools/Build/thresher_plant_lab/parity.cjs [--ref <trace.json>]
//
// 1. Runs trace.sh (the shipped solver, compiled from its real path) over scenarios.json -> reference trace,
//    unless --ref names one already written.
// 2. Opens Docs/Studios/ThresherPlantLab.html in headless Chromium and runs the SAME scenarios through the
//    page's own solver (window.__lab.parityTrace), so the gate measures exactly the code the page flies.
// 3. Compares every frame: mode equal, ship and ball positions within TOL. Exit 1 on any mismatch.
// 4. Negative controls: the page re-run with a planted change (skid 7 -> 7.5; yank 0.5 -> 0.55; hookTurnRate
//    300 -> 290; and on the analog path elRigid 30 -> 28, anStopDecel 450 -> 420, anAnchor 2 -> 2.2) MUST fail
//    the same comparison - a gate that cannot see a 7% dial change is not a gate.
'use strict';
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const { chromium } = require('playwright');

const ROOT = path.resolve(__dirname, '../../..');
const PAGE = path.join(ROOT, 'Docs/Studios/ThresherPlantLab.html');
const SPEC = JSON.parse(fs.readFileSync(path.join(__dirname, 'scenarios.json'), 'utf8'));
const SCEN = SPEC.scenarios, DIALS = SPEC.analogDials;
// The C# side is float32 and the page float64, so rounding accumulates with distance flown: the gap grows
// smoothly (never steps) to ~0.06 u after ~600 u of a spun-up launch. Tolerance = 0.02 u + 2e-4 x the ship's distance from the origin on that frame.
// A 7% change to one dial (the negative controls) misses by 3 u or more.
const TOL = 0.02, REL = 2e-4;

function compare(ref, got) {
  let worst = 0, where = '', modeMiss = 0, frames = 0;
  for (const sc of SCEN) {
    const a = ref[sc.name], b = got[sc.name];
    if (!a || !b || a.length !== b.length) return { ok: false, worst: Infinity, where: sc.name + ' frame count', frames };
    for (let i = 0; i < a.length; i++) {
      frames++;
      if (a[i][0] !== b[i][0]) { modeMiss++; if (worst < Infinity) where = `${sc.name} frame ${i} mode ${a[i][0]} vs ${b[i][0]}`; worst = Infinity; continue; }
      const scale = Math.hypot(a[i][1], a[i][2], a[i][3]);
      for (let k = 1; k <= 8; k++) {
        const d = Math.abs(a[i][k] - b[i][k]) / (TOL + REL * scale);
        if (d > worst) { worst = d; where = `${sc.name} frame ${i} col ${k} (|diff| ${Math.abs(a[i][k] - b[i][k]).toExponential(2)} u)`; }
      }
    }
  }
  return { ok: worst <= 1 && modeMiss === 0, worst, where, frames, modeMiss };
}

(async () => {
  const argv = process.argv.slice(2);
  let refPath = argv[0] === '--ref' ? argv[1] : null;
  if (!refPath) {
    refPath = path.join(require('os').tmpdir(), 'thresher_plant_ref.json');
    execFileSync('bash', [path.join(__dirname, 'trace.sh'), refPath], { stdio: ['ignore', 'ignore', 'inherit'] });
  }
  const ref = JSON.parse(fs.readFileSync(refPath, 'utf8'));
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('file://' + PAGE);
  await page.waitForFunction(() => window.__lab && window.__lab.parityTrace);
  const run = (over) => page.evaluate(([s, o, a]) => window.__lab.parityTrace(s, o, a), [SCEN, over, DIALS]);

  let failed = 0;
  const main = compare(ref, await run(null));
  console.log(`${main.ok ? 'PASS' : 'FAIL'} digital + analog paths (${SCEN.length} scenarios): ${main.frames} frames, worst diff ${main.worst.toFixed(2)} x tolerance at ${main.where}`);
  if (!main.ok) failed++;
  for (const [label, over] of [['skid 7 -> 7.5', { skid: 7.5 }], ['yank 0.5 -> 0.55', { yank: 0.55 }], ['hookTurnRate 300 -> 290', { hookTurnRate: 290 }],
                              ['analog: elRigid 30 -> 28', { elRigid: 28 }], ['analog: anStopDecel 450 -> 420', { anStopDecel: 420 }], ['analog: anAnchor 2 -> 2.2', { anAnchor: 2.2 }]]) {
    const c = compare(ref, await run(over));
    const caught = !c.ok;
    console.log(`${caught ? 'PASS' : 'FAIL'} negative control (${label}) ${caught ? 'is caught' : 'was NOT caught'}: worst ${c.worst === Infinity ? 'mode mismatch' : c.worst.toFixed(0) + ' x tolerance'}`);
    if (!caught) failed++;
  }
  // 5. The ASSET is the record: ThresherConfig.asset's analog dials (game units) must equal the parity dials
  //    above AND the lab's "Analog triggers" preset, so the page, the gate and the game cannot drift apart.
  const asset = fs.readFileSync(path.join(ROOT, 'Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset'), 'utf8');
  const field = (key) => { const m = asset.match(new RegExp('^\\s+' + key + ': ([-0-9.eE]+)\\s*$', 'm')); return m ? +m[1] : NaN; };
  const k = field('gameCruise') / field('sandboxCruise');
  const fromAsset = {
    anScheme: field('analogTriggers'), anBrake: field('analogBrake'), anBrakeCurve: field('analogBrakeCurve'),
    anAnchor: field('analogAnchorCurve'), anStopDecel: field('analogStopDecel') * k, elSlack: field('chainSlackRate'),
    elRigid: field('chainRigidRate'), elDamp: field('chainDamping'), elCurve: field('rigidityCurve'), elSlew: field('rigiditySlew'),
    hkSeconds: field('analogHookSeconds'), spEase: field('spinEase'), yank: field('yank'),
    anFull: field('fullPull'), anRelease: field('letGoBelow'), hlPost: field('hullFollowsPlantedVelocity'),
    rlSpinDown: field('releaseSpinDownSeconds'), rlSpinMaxDeg: field('releaseSpinDownMaxDegrees'),
    cmCarry: field('spectateCarrySeconds'), cmAngAccel: field('cameraMaxFollowAngularAcceleration'),
  };
  const preset = await page.evaluate(() => Object.assign({}, window.__lab.SHIPPED, window.__lab.VARIANTS.find((v) => v.name === 'Analog triggers').v));
  const near = (a, b) => Math.abs(a - b) <= 1e-3 * Math.max(1, Math.abs(a), Math.abs(b));
  const off = [];
  for (const [key, val] of Object.entries(fromAsset)) {
    if (!near(val, preset[key])) off.push(`${key}: asset ${val} vs lab preset ${preset[key]}`);
    if (key in DIALS && !near(val, DIALS[key])) off.push(`${key}: asset ${val} vs scenarios.json ${DIALS[key]}`);
  }
  console.log(off.length ? 'FAIL ThresherConfig.asset differs: ' + off.join('; ') : `PASS ThresherConfig.asset's ${Object.keys(fromAsset).length} analog dials equal the lab preset and the parity dials`);
  if (off.length) failed++;
  if (errors.length) { console.log('FAIL page errors: ' + errors.join(' | ')); failed++; }
  await browser.close();
  process.exit(failed ? 1 : 0);
})().catch((e) => { console.error(e); process.exit(2); });
