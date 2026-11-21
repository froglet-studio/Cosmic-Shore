// The LAB's own runtime (Tools/Ecology/flight/creatures/nca_creature.src.js on the research branch, JS backend) run on
// the research weights.json - the reference NcaVoxelCore must reproduce (Tools/Build/nca_creature_harness/run.sh).
//
//   node lab_reference.js <nca_creature.js> <weights.json> <out.json> [wasm|js]
//
// Backend `wasm` (the default the lab ships: nca_kernel.c, f32 SIMD - needs the BUILT nca_creature.js, which embeds the
// kernel) or `js` (the batched f64 path; nca_creature.src.js is enough).
// Seed 7, grown one step at a time: count + whole-state sum every 10 steps, the full state at steps 1, 50 and 200;
// then a radius-7 cut through the body (no debris, so the fire-mask RNG is untouched) and 100 more steps.
'use strict';
const fs = require('fs');
const N = require(process.argv[2]);
const w = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const backend = process.argv[5] || 'wasm';
const c = new N({ seed: 7, weights: w, wasm: backend === 'wasm', scale: 0.5 });
if (c.backend !== (backend === 'wasm' ? 'wasm-simd' : 'js')) throw new Error(`asked for ${backend}, runtime gave ${c.backend}`);
const sum = () => { let s = 0; for (let i = 0; i < c.s.length; i++) s += c.s[i]; return s; };
const dump = () => Buffer.from(new Float32Array(c.s).buffer).toString('base64');
const out = { backend: c.backend, seed: 7, D: c.D, H: c.H, W: c.W, C: c.C, track: [], states: {} };
for (let s = 1; s <= 300; s++) {
  if (s === 201) {
    // a cut through the body's centre of mass, as a vessel flying through it would make
    const K = c._keep, HW = c.H * c.W; let gz = 0, gy = 0, gx = 0, n = 0;
    for (let k = 0; k < c._keepN; k++) { const i = K[k]; if (c.s[i * c.C + 3] > 0.3) { const z = (i / HW) | 0, y = ((i - z * HW) / c.W) | 0, x = i - z * HW - y * c.W; gz += z; gy += y; gx += x; n++; } }
    gz = Math.round(gz / n); gy = Math.round(gy / n); gx = Math.round(gx / n);
    const p = c.gridToWorld(gz, gy, gx);
    const g = c.worldToGrid(p);
    const removed = c.hit(p, 7 * c.scale, { maxDebris: 0 });
    out.hit = { gz: g[0], gy: g[1], gx: g[2], r: 7, removed, countAfter: c.count() };
  }
  c.grow(1);
  if (s % 10 === 0 || s === 1) out.track.push([s, c.count(), sum()]);
  if (s === 1 || s === 50 || s === 200 || s === 300) out.states[s] = dump();
}
fs.writeFileSync(process.argv[4], JSON.stringify(out));
console.log(`lab reference (${c.backend}): ${out.track.length} checkpoints, hit removed ${out.hit.removed}, count@300 ${c.count()}`);
