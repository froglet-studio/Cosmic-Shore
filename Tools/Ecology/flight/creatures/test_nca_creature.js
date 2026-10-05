#!/usr/bin/env node
// test_nca_creature.js - headless checks for nca_creature.js (the trained 3D NCA lizard as a flyable creature).
//   node test_nca_creature.js            -> prints a summary, writes results/nca_creature.json, exits 1 on failure
// Checks: growth from a single seed cell; keeps animating (swim cycle: frame-to-frame change > 0, for 600+ steps);
// hit() erases voxels and the body regrows to >= 90 % of its pre-hit count; step(dt) with a budget never stalls a frame
// and resumes mid-step; voxels() colours lean to the element palette; the body frame (yaw/pitch/roll) round-trips;
// swim() is phase-locked to the NCA tail beat and its speed follows the step rate; cuts shed debris; JS adapts its rate.
'use strict';
const fs = require('fs'), path = require('path');
const NcaCreature = require('./nca_creature.js');

const out = { module: 'nca_creature.js', source: 'Tools/NCA/results/lizard3d_swim', checks: {} };
const fail = [];
const ok = (name, cond, info) => { out.checks[name] = Object.assign({ pass: !!cond }, info); if (!cond) fail.push(name); console.log((cond ? 'PASS ' : 'FAIL ') + name + ' ' + JSON.stringify(info)); };

// ---- 1. growth from seed ---------------------------------------------------------------------------------------
const c = new NcaCreature({ seed: 7, scale: 0.5, position: [0, 20, 0], element: 'space' });
out.grid = [c.D, c.H, c.W, c.C]; out.embeddedWeights = NcaCreature.hasEmbeddedWeights;
const growth = [];
const tg = Date.now();
for (let s = 0; s <= 200; s++) { if (s % 8 === 0) growth.push([s, c.count()]); if (s < 200) c.grow(1); }
const growMs = (Date.now() - tg) / 200;
const grown = c.count();
ok('growth_from_seed', growth[0][1] <= 1 && grown > 500, { alive_at_step: growth.filter((g, i) => i % 3 === 0 || i === growth.length - 1), grown_voxels_at_200: grown, ms_per_full_step: +growMs.toFixed(2) });

// ---- 2. keeps animating: swim cycle, frame-to-frame changes and the pose period ---------------------------------
function snap(cr) { const v = cr.voxels(), a = new Float32Array(cr.N); const C = cr.C; for (let i = 0; i < cr.N; i++) a[i] = Math.min(1, Math.max(0, cr.s[i * C + 3])); return { a, n: v.n }; }
function diff(p, q) { let d = 0, ch = 0; for (let i = 0; i < p.a.length; i++) { const e = Math.abs(p.a[i] - q.a[i]); d += e; if ((p.a[i] > 0.3) !== (q.a[i] > 0.3)) ch++; } return { l1: d, flips: ch }; }
let prev = snap(c); const f2f = [], counts = [], snaps = [];
for (let s = 0; s < 640; s++) {
  c.grow(1); const cur = snap(c), d = diff(prev, cur); f2f.push(d.flips); counts.push(cur.n); prev = cur;
  if (s >= 512) snaps.push(cur.a);
}
// pose period: autocorrelation lag (of alpha volume) that best matches, between 20 and 100 steps
let bestLag = 0, bestErr = Infinity;
for (let lag = 20; lag <= 100; lag++) { let e = 0, m = 0; for (let k = 0; k + lag < snaps.length; k += 4) { const A = snaps[k], B = snaps[k + lag]; for (let i = 0; i < A.length; i++) e += Math.abs(A[i] - B[i]); m++; } e /= m; if (e < bestErr) { bestErr = e; bestLag = lag; } }
const minFlips = Math.min(...f2f.slice(-200)), meanFlips = f2f.slice(-200).reduce((a, b) => a + b, 0) / 200;
const late = counts.slice(-200), lateMin = Math.min(...late), lateMax = Math.max(...late);
ok('keeps_animating', meanFlips > 1 && lateMin > 0.5 * grown, { steps: 640, mean_voxel_flips_per_step_last200: +meanFlips.toFixed(1), min_flips_last200: minFlips, alive_range_last200: [lateMin, lateMax], cycle_period_steps: bestLag, expected_cycle_steps: '8 poses x ~8.5 = ~68' });

// ---- 3. hit() and regrowth --------------------------------------------------------------------------------------
// a big bite: centred halfway between the body centre and the farthest voxel (a chunk of tail/torso), radius 7 voxels
function avgCount(cr, n) { let t = 0; for (let k = 0; k < n; k++) { cr.grow(1); t += cr.count(); } return t / n; }
const before = avgCount(c, 68);                  // average over one swim cycle (count varies with the pose)
const v = c.voxels(); let far = 0, fd = -1;
for (let i = 0; i < v.n; i++) { const d = Math.hypot(v.pos[3 * i] - c.position[0], v.pos[3 * i + 2] - c.position[2]); if (d > fd) { fd = d; far = i; } }
const hp = [0.5 * (v.pos[3 * far] + c.position[0]), v.pos[3 * far + 1], 0.5 * (v.pos[3 * far + 2] + c.position[2])];
const removed = c.hit(hp, 7 * c.scale);
const afterHit = c.count();
const rec = [[0, afterHit]], win = []; let recStep = null;
for (let s = 1; s <= 600; s++) {
  c.grow(1); const n = c.count(); if (s % 10 === 0) rec.push([s, n]);
  win.push(n); if (win.length > 17) win.shift();         // 17-step window (1/4 swim cycle) so one lucky pose does not count
  const wm = win.reduce((x, y) => x + y, 0) / win.length;
  if (recStep === null && win.length === 17 && wm >= 0.9 * before) recStep = s;
}
const finalAvg = avgCount(c, 68);
ok('hit_and_regrow', removed > 0.15 * before && recStep !== null && finalAvg >= 0.9 * before,
  { pre_hit_avg_voxels: Math.round(before), hit_radius_voxels: 7, removed, after_hit: afterHit, removed_pct: +(100 * (before - afterHit) / before).toFixed(1),
    steps_to_90pct_windowed: recStep, recovery_pct_after_600_steps: +(100 * finalAvg / before).toFixed(1), curve: rec.filter((_, i) => i % 3 === 0) });

// ---- 4. budgeted, resumable step(dt) ----------------------------------------------------------------------------
const b = new NcaCreature({ seed: 3, scale: 0.5, element: 'mass', budgetMs: 4 });
b.grow(96);
for (let w = 0; w < 30; w++) b.step(1 / 60);   // JIT warm-up
const frameMs = []; let stepsDone = 0, midStep = 0;
for (let f = 0; f < 300; f++) { stepsDone += b.step(1 / 60); frameMs.push(b.lastStepMs); if (b._inStep) midStep++; }
frameMs.sort((x, y) => x - y);
const p50 = frameMs[150], p95 = frameMs[285], max = frameMs[299];
const capd = new NcaCreature({ seed: 3, element: 'time', cellsPerFrame: 2000 }); capd.grow(96);
const capMs = []; let capSteps = 0; for (let f = 0; f < 120; f++) { capSteps += capd.step(1 / 60); capMs.push(capd.lastStepMs); }
// p99 rather than max: on a loaded shared CPU the OS can preempt any 50 us kernel call for 10+ ms
const p99 = frameMs[296];
// resumability: the kernel is now faster than the 4 ms budget, so check mid-step resume with a 0.5 ms budget and that
// a step split over many frames gives exactly the state of an unsplit grow() with the same seed
const rs = new NcaCreature({ seed: 5, budgetMs: 0.5, stepsPerSecond: 1e6 }), rg = new NcaCreature({ seed: 5 });
rs.grow(96); rg.grow(96);
let rsMid = 0; while (rs.steps < 106) { rs.step(1 / 60); if (rs._inStep) rsMid++; }
rg.grow(10); let rsDiff = 0; for (let i = 0; i < rs.s.length; i++) rsDiff = Math.max(rsDiff, Math.abs(rs.s[i] - rg.s[i]));
ok('budgeted_step', p95 <= 4 + 2 && p99 < 4 * 4 && stepsDone > 0 && rsMid > 0 && rsDiff === 0,
  { budgetMs: 4, frames: 300, nca_steps_done: stepsDone, nca_steps_per_sec_at_60fps: +(stepsDone / 5).toFixed(1), frames_ending_mid_step: midStep, resume_check_0p5ms_budget: { frames_ending_mid_step: rsMid, steps: 10, max_abs_diff_vs_unsplit: rsDiff },
    frame_ms_p50: +p50.toFixed(2), frame_ms_p95: +p95.toFixed(2), frame_ms_p99: +p99.toFixed(2), frame_ms_max: +max.toFixed(2),
    cellsPerFrame_2000: { frames: 120, nca_steps: capSteps, frame_ms_mean: +(capMs.reduce((a, x) => a + x, 0) / 120).toFixed(2) } });

// ---- 5. colours by element --------------------------------------------------------------------------------------
const cols = {};
for (const el of ['mass', 'charge', 'space', 'time']) {
  b.element = el; const vv = b.voxels(); const m = [0, 0, 0];
  for (let i = 0; i < vv.n; i++) for (let k = 0; k < 3; k++) m[k] += vv.rgba[4 * i + k] / vv.n;
  cols[el] = m.map(x => +x.toFixed(3));
}
const dom = (m, E) => { const a = m.indexOf(Math.max(...m)), e = E.indexOf(Math.max(...E)); return a === e; };
ok('element_colours', ['mass', 'space', 'time'].every(el => dom(cols[el], NcaCreature.ELEMENT_COLOUR[el])), { mean_rgb: cols });

// ---- 6. speed: sparse active set + WebAssembly SIMD kernel vs the JS backend (and the old dense step) ------------
// wasm accumulates in f32 (JS in f64), so the backends agree to ~1e-5, far below the fp16 weight quantisation.
// The JS backend is the dense nca3d_core.js step bit for bit (only cells in the alive mask are visited).
const DENSE_BASELINE_MS = 33.5;      // the dense JS step this replaced (f9491d22), grown lizard, same machine class
const pw = new NcaCreature({ seed: 11 }), pj = new NcaCreature({ seed: 11, wasm: false });
let pmax = 0, pcnt = 0;
for (let s = 0; s < 300; s++) {
  pw.grow(1); pj.grow(1);
  for (let i = 0; i < pw.s.length; i++) { const d = Math.abs(pw.s[i] - pj.s[i]); if (d > pmax) pmax = d; }
  pcnt = Math.max(pcnt, Math.abs(pw.count() - pj.count()));
}
const msPer = (cr, n) => { const t = process.hrtime.bigint(); cr.grow(n); return Number(process.hrtime.bigint() - t) / 1e6 / n; };
msPer(pw, 20); msPer(pj, 5);
const wasmMs = msPer(pw, 100), jsMs = msPer(pj, 30);
const fast = new NcaCreature({ seed: 3, budgetMs: 4, stepsPerSecond: 1e6 }); fast.grow(96);
for (let w = 0; w < 30; w++) fast.step(1 / 60);
let fastSteps = 0; for (let f = 0; f < 120; f++) fastSteps += fast.step(1 / 60);
const stepsPerSecAt4ms = fastSteps / 2, sps = Math.min(stepsPerSecAt4ms, b.stepsPerSecond);
ok('speed_and_parity', pw.backend === 'wasm-simd' ? (pmax < 1e-3 && pcnt <= 2 && wasmMs * 5 <= DENSE_BASELINE_MS) : true,
  { backend: pw.backend, wasm_vs_js_max_abs_diff_300_steps: +pmax.toExponential(2), wasm_vs_js_max_count_diff: pcnt,
    ms_per_step: { dense_baseline: DENSE_BASELINE_MS, js_sparse: +jsMs.toFixed(2), wasm_simd: +wasmMs.toFixed(2) },
    speedup_vs_dense: +(DENSE_BASELINE_MS / wasmMs).toFixed(1),
    nca_steps_per_sec_at_4ms_per_60fps_frame_uncapped: stepsPerSecAt4ms, swim_cycle_s_at_that_rate: +(68 / stepsPerSecAt4ms).toFixed(2),
    regrow_90pct_s_at_default_30_steps_per_s: recStep && +(recStep / sps).toFixed(1),
    regrow_90pct_s_uncapped: recStep && +(recStep / stepsPerSecAt4ms).toFixed(1) });

// ---- 7. body frame: world <-> grid round trip under yaw + pitch + roll; heading() agrees with yawFor() -------------
{
  const b = new NcaCreature({ seed: 3, scale: 2, position: [10, -5, 7] });
  let worst = 0;
  for (let k = 0; k < 40; k++) {
    b.yaw = 6 * Math.random() - 3; b.pitch = Math.random() - 0.5; b.roll = 1.4 * Math.random() - 0.7;
    const g = [22 * Math.random(), 44 * Math.random(), 44 * Math.random()], w = b.gridToWorld(g[0] - 0.5, g[1] - 0.5, g[2] - 0.5), r = b.worldToGrid(w);
    worst = Math.max(worst, Math.abs(r[0] - g[0] + 0.5), Math.abs(r[1] - g[1] + 0.5), Math.abs(r[2] - g[2] + 0.5));
  }
  b.pitch = b.roll = 0; b.yaw = b.yawFor(0.6, -0.8); const h = b.heading();
  ok('body_frame', worst < 1e-6 && Math.abs(h[0] - 0.6) < 1e-6 && Math.abs(h[2] + 0.8) < 1e-6, { roundtrip_max_err_voxels: +worst.toExponential(2), heading: h.map(v => +v.toFixed(4)) });
}

// ---- 8. swim(): phase-locked to the NCA's tail beat, speed follows the achieved step rate, turns to the goal ---------
{
  const run = (sps) => {
    const L = new NcaCreature({ seed: 9, scale: 4, element: 'mass', stepsPerSecond: sps, budgetMs: 50 }); L.grow(110);
    const goal = [3000, 0, 1500], bends = [], sp = [];
    for (let f = 0; f < 600; f++) { L.step(1 / 60); L.swim(1 / 60, goal, { cruise: 22 }); if (f > 240) { bends.push(L.bend); sp.push(L.speed); } }
    const mb = bends.reduce((a, b) => a + b, 0) / bends.length; let cross = 0; for (let k = 1; k < bends.length; k++) if ((bends[k] - mb) * (bends[k - 1] - mb) < 0) cross++;
    const h = L.heading(), want = Math.atan2(1500 - L.position[2], 3000 - L.position[0]), err = Math.abs(Math.atan2(Math.sin(Math.atan2(h[2], h[0]) - want), Math.cos(Math.atan2(h[2], h[0]) - want)));
    return { speed: sp.reduce((a, b) => a + b, 0) / sp.length, speedMin: Math.min(...sp), speedMax: Math.max(...sp), bendAmp: Math.max(...bends) - Math.min(...bends), crossings: cross, headingErrDeg: err * 180 / Math.PI, steps: L.steps };
  };
  const slow = run(10), fast = run(30);
  ok('swim_phase_locked', fast.speed > 2 * slow.speed && fast.bendAmp > 1 && fast.crossings >= 4 && fast.speedMax > 1.1 * fast.speedMin && fast.headingErrDeg < 25,
    { at_10_steps_per_s: Object.fromEntries(Object.entries(slow).map(([k, v]) => [k, +v.toFixed(2)])), at_30_steps_per_s: Object.fromEntries(Object.entries(fast).map(([k, v]) => [k, +v.toFixed(2)])) });
}

// ---- 9. a cut sheds debris and leaves a glowing wound, both short-lived; the JS fallback lowers its step rate --------
{
  const L = new NcaCreature({ seed: 5, scale: 4 }); L.grow(100);
  const v = L.voxels(), j = 3 * (v.n >> 1), removed = L.hit([v.pos[j], v.pos[j + 1], v.pos[j + 2]], 22, { velocity: [100, 0, 0] });
  const d0 = L.debris.length, w0 = L.wounds.length; for (let f = 0; f < 360; f++) L.step(1 / 60);
  const J = new NcaCreature({ seed: 5, scale: 4, wasm: false, stepsPerSecond: 20, budgetMs: 4 }); J.grow(100);
  for (let f = 0; f < 120; f++) J.step(1 / 60);
  ok('cut_debris_and_adaptive_rate', removed > 0 && d0 > 0 && w0 === 1 && L.debris.length === 0 && L.wounds.length === 0 && J.backend === 'js' && J.rate < J.stepsPerSecond,
    { removed, debris_after_cut: d0, wounds: w0, debris_after_6s: L.debris.length, wounds_after_6s: L.wounds.length, js_ms_per_step: +J.msPerStep.toFixed(1), js_rate: +J.rate.toFixed(1) });
}

out.pass = fail.length === 0; out.failed = fail;
fs.mkdirSync(path.join(__dirname, 'results'), { recursive: true });
fs.writeFileSync(path.join(__dirname, 'results', 'nca_creature.json'), JSON.stringify(out, null, 1));
console.log(out.pass ? 'ALL PASS' : 'FAILED: ' + fail.join(', '));
process.exit(out.pass ? 0 : 1);
