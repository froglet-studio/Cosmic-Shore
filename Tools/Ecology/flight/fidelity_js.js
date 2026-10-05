// The JS half of the flight fidelity gate: run the SAME species code the page flies (sim.js) headless under Node,
// in the same three worlds as fidelity_py.py, against the same scripted pilots, and record the same statistics.
//
//   node Tools/Ecology/flight/fidelity_js.js                 # all species, dt 0.1 (the Python's step), 8 seeds
//   node Tools/Ecology/flight/fidelity_js.js --dt30          # the page's 30 Hz step, probe sampled at 10 Hz
//   node Tools/Ecology/flight/fidelity_js.js pack --break thief.WARM=1e9 --out results/neg.json
'use strict';
const fs = require('fs'), path = require('path');
const E = require('./sim.js');
const HERE = __dirname;
const PARAMS = JSON.parse(fs.readFileSync(path.join(HERE, 'params.json')));
const args = process.argv.slice(2);
const opt = { keys: [], seeds: [7, 23, 41, 101, 202, 303, 404, 505], minutes: 1.5, dt: 0.1, every: 1, out: null, brk: [] };
for (let i = 0; i < args.length; i++) {
  const a = args[i];
  if (a === '--dt30') { opt.dt = 1 / 30; opt.every = 3; }
  else if (a === '--seeds') { opt.seeds = []; while (args[i + 1] && /^\d+$/.test(args[i + 1])) opt.seeds.push(+args[++i]); }
  else if (a === '--minutes') opt.minutes = +args[++i];
  else if (a === '--out') opt.out = args[++i];
  else if (a === '--break') opt.brk.push(args[++i]);
  else opt.keys.push(a);
}
// --break species.CONST=value : a deliberately broken parameter (the gate's negative control)
const P = JSON.parse(JSON.stringify(PARAMS));
for (const b of opt.brk) { const [lhs, v] = b.split('='); const [sp, k] = lhs.split('.'); P.species[sp].const[k] = +v; }
const keys = opt.keys.length ? opt.keys : Object.keys(E.WORLD).filter(k => !(E.JS_ORIGINAL || {})[k]);   // JS-original species have no Python twin
const runs = [];
const t0 = Date.now();
for (const k of keys) for (const pol of E.POLICIES[E.WORLD[k]]) for (const s of opt.seeds) {
  runs.push(E.runOne(P, k, pol, s, opt.minutes, opt.dt, opt.every));
}
for (const k of keys) for (const pol of E.POLICIES[E.WORLD[k]]) {
  const rr = runs.filter(r => r.species === k && r.policy === pol);
  const m = f => (rr.reduce((s, r) => s + f(r), 0) / rr.length);
  console.log(`${k.padEnd(10)} ${pol.padEnd(7)} hits/min ${m(r => r.hits_per_min).toFixed(2).padStart(7)}  crystals/min ${m(r => r.crystals_per_min).toFixed(2).padStart(6)}  ms/step ${m(r => r.ms_per_step).toFixed(3)}  max drift ${Math.max(...rr.map(r => r.conservation)).toExponential(1)}`);
}
const out = opt.out ? path.resolve(HERE, opt.out) : path.join(HERE, 'results', opt.dt < 0.05 ? 'fidelity_js_dt30.json' : 'fidelity_js.json');
let old = { runs: [] }; if (fs.existsSync(out) && !opt.brk.length) old = JSON.parse(fs.readFileSync(out));
const keep = old.runs.filter(r => !keys.includes(r.species));
fs.writeFileSync(out, JSON.stringify({ dt: opt.dt, minutes: opt.minutes, seeds: opt.seeds, broken: opt.brk, runs: keep.concat(runs) }));
console.log(`${((Date.now() - t0) / 1000).toFixed(0)}s -> ${path.relative(HERE, out)}`);
