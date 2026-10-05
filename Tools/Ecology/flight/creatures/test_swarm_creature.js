/* test_swarm_creature.js - the swarm creature against the phase-1 yardstick.
 *
 *   node Tools/Ecology/flight/creatures/test_swarm_creature.js   -> creatures/results/swarm_creature.json
 *
 * 1. own-plan shape loss per element after settling while swimming (swarm_loss.js, all 8 frames; tier-1 bar < 8),
 *    plus the loss to the other three plans (must be strictly worst-matched by none: own plan is the closest).
 * 2. morph time between every pair of plans: seconds from setElement until own-plan loss < 8 (and its settled value).
 * 3. hit recovery: knock a third of the body loose (cracking crystals), time until loss is back under 8 and within
 *    +1 of the pre-hit loss; molts that the recovery cost.
 * 4. steering: the body swims to a goal as one piece.
 * 5. per-frame ms at n = 192, 256, 1024 for frac 8 (shipped) and frac 1 (every tadpole every step).
 * 6. composition: a mixed vector picks the majority plan and the swarm's census matches the vector.
 * Exit code 1 if a bar fails.
 */
'use strict';
const fs = require('fs');
const path = require('path');
const SC = require('./swarm_creature.js');
const PLANS = require('./swarm_plans.js');
const L = require('./swarm_loss.js');

const K = ['mass', 'space', 'charge', 'time'];
const DT = 1 / 60;
const run = (c, sec) => { for (let k = 0, m = Math.round(sec / DT); k < m; k++) c.step(DT); };
const near = c => { const F = 8, k = Math.round(c.phase) % F; return [(k + F - 1) % F, k, (k + 1) % F]; };
const lossNow = (c, kind, all) => L.loss(c.lossState(), PLANS[kind], all ? {} : { frames: near(c) }).sink;
const r2 = x => Math.round(x * 100) / 100;
const out = { bar: 8, n: 192, own: {}, morph: {}, hit: {}, steer: {}, perf: {}, composition: {} };
let ok = true;

// 1. own-plan loss, swimming
for (const e of K) {
  const c = new SC({ n: 192, element: e, seed: 11 });
  c.steer([200, 0, 0], 6);
  run(c, 3);
  const all = L.scoreAll(c.lossState(), PLANS);
  const own = all[e], closest = K.every(k => k === e || all[k] > own);
  out.own[e] = { plan: PLANS[e].name, loss: own, others: all, closest, pass: own < 8 && closest };
  ok = ok && out.own[e].pass;
  console.log('own', e, PLANS[e].name, own, closest ? 'closest' : 'NOT CLOSEST');
}

// 2. morphs
const times = [];
for (const a of K) for (const b of K) {
  if (a === b) continue;
  const c = new SC({ n: 192, element: a, seed: 7 });
  c.steer([200, 0, 0], 6);
  run(c, 1.5);
  c.setElement(b);
  let t = 0, done = null;
  while (t < 10) {
    run(c, 0.1); t += 0.1;
    if (lossNow(c, b) < 8) { done = r2(t); break; }
  }
  run(c, 2);
  const settled = r2(lossNow(c, b, true));
  out.morph[a + '->' + b] = { from: PLANS[a].name, to: PLANS[b].name, seconds: done, settled, molts: c.stats.molts };
  if (done === null || settled >= 8) ok = false; else times.push(done);
  console.log('morph', a, '->', b, done, 's settled', settled, 'molts', c.stats.molts);
}
out.morphSummary = { mean: r2(times.reduce((x, y) => x + y, 0) / times.length), max: Math.max(...times) };

// 3. hit recovery
for (const e of K) {
  const c = new SC({ n: 192, element: e, seed: 3 });
  run(c, 2);
  const before = lossNow(c, e);
  const b = c.bounds();
  const n0 = c.stats.molts;
  const knocked = c.hit(b.centre, b.radius * 0.45);
  let t = 0, peak = 0, back = null;
  while (t < 10) {
    run(c, 0.1); t += 0.1;
    const l = lossNow(c, e); peak = Math.max(peak, l);
    if (l < 8 && l < before + 1) { back = r2(t); break; }
  }
  out.hit[e] = { knocked, before: r2(before), peak: r2(peak), recoverSeconds: back, moltsBack: c.stats.molts - n0 };
  if (back === null) ok = false;
  console.log('hit', e, 'knocked', knocked, 'before', r2(before), 'peak', r2(peak), 'back in', back, 's');
}

// 4. steering
{
  const c = new SC({ n: 192, element: 'mass', seed: 2, scale: 0.4, position: [0, 0, 0] });
  const goal = [-60, 10, 40];
  c.steer(goal, 8);
  run(c, 20);
  const d = Math.hypot(...c.position.map((v, a) => v - goal[a]));
  const ls = r2(lossNow(c, 'mass'));
  out.steer = { goal, finalDistance: r2(d), lossWhileSwimming: ls, pass: d < 3 && ls < 8 };
  ok = ok && out.steer.pass;
  console.log('steer dist', r2(d), 'loss', ls);
}

// 5. perf
for (const n of [192, 256, 1024]) for (const frac of [8, 1]) {
  const c = new SC({ n, element: 'mass', seed: 1, frac });
  c.steer([500, 0, 0], 6);
  run(c, 1);
  c.setElement('space'); run(c, 1); c.setElement('mass');        // includes a re-deal mid-run
  const frames = 600, t0 = process.hrtime.bigint();
  for (let k = 0; k < frames; k++) { c.step(DT); c.units(); }
  const ms = Number(process.hrtime.bigint() - t0) / 1e6 / frames;
  out.perf['n' + n + '_frac' + frac] = { msPerFrame: Math.round(ms * 1000) / 1000, includes: 'step + units()' };
  const t1 = process.hrtime.bigint(); c.setElement('charge'); const dealMs = Number(process.hrtime.bigint() - t1) / 1e6;
  out.perf['n' + n + '_frac' + frac].redealMs = r2(dealMs);
  console.log('perf n', n, 'frac', frac, (Math.round(ms * 1000) / 1000) + ' ms/frame, re-deal', r2(dealMs), 'ms');
}
if (out.perf.n1024_frac8) out.perf.n1024_loss_mass = r2(lossNow((() => { const c = new SC({ n: 1024, element: 'mass', seed: 1 }); run(c, 2); return c; })(), 'mass'));

// 6. composition
{
  const c = new SC({ n: 192, element: 'mass', seed: 4 });
  run(c, 1);
  const want = [0.15, 0.2, 0.5, 0.15];
  c.setComposition(want);
  run(c, 4);
  const comp = c.composition();
  const err = Math.max(...comp.have.map((h, i) => Math.abs(h - want[i])));
  out.composition = { want, have: comp.have.map(r2), plan: comp.plan, name: comp.name, maxError: r2(err),
                      lossToPlan: r2(lossNow(c, comp.plan, true)), pass: comp.plan === 'space' && err < 0.02 };
  ok = ok && out.composition.pass;
  console.log('composition', comp.plan, comp.name, 'have', comp.have.map(r2).join(','), 'loss', out.composition.lossToPlan);
}

out.pass = ok;
const dir = path.join(__dirname, 'results');
fs.mkdirSync(dir, { recursive: true });
fs.writeFileSync(path.join(dir, 'swarm_creature.json'), JSON.stringify(out, null, 1));
console.log(ok ? 'PASS' : 'FAIL', '->', path.join(dir, 'swarm_creature.json'));
process.exit(ok ? 0 : 1);
