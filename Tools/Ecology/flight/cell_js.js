// The JS half of the WHOLE-CELL gate: the page's own FlightWorld 'cell' (sim.js), with a scripted pilot in place of
// the player, headless under Node, booking the same ledger as cell_py.py (who eats / destroys / steals / hauls whose
// mass, populations, who hits the pilot). cell_fidelity.py compares the two.
//
//   node Tools/Ecology/flight/cell_js.js                          # both pilots, 6 seeds x 3 min, dt 0.1
//   node Tools/Ecology/flight/cell_js.js --drop locust            # the cell without the locusts (an ablation)
//   node Tools/Ecology/flight/cell_js.js --pop scored             # scored populations
//   node Tools/Ecology/flight/cell_js.js --break grazer.SENSE=20 --out results/cell_js_neg.json
'use strict';
const fs = require('fs'), path = require('path');
const E = require('./sim.js');
const HERE = __dirname;
const PARAMS = JSON.parse(fs.readFileSync(path.join(HERE, 'params.json')));
const args = process.argv.slice(2);
const opt = { seeds: [7, 23, 41, 101, 202, 303], minutes: 3, dt: 0.1, policies: ['wander', 'hunter'], drop: [], pop: 'cell', out: null, brk: [] };
const many = (i) => { const r = []; while (args[i + 1] && !args[i + 1].startsWith('--')) r.push(args[++i]); return [r, i]; };
for (let i = 0; i < args.length; i++) {
  const a = args[i]; let r;
  if (a === '--seeds') { [r, i] = many(i); opt.seeds = r.map(Number); }
  else if (a === '--policies') { [r, i] = many(i); opt.policies = r; }
  else if (a === '--drop') { [r, i] = many(i); opt.drop = r; }
  else if (a === '--minutes') opt.minutes = +args[++i];
  else if (a === '--dt30') opt.dt = 1 / 30;
  else if (a === '--pop') opt.pop = args[++i];
  else if (a === '--out') opt.out = args[++i];
  else if (a === '--break') opt.brk.push(args[++i]);
  else throw new Error('unknown arg ' + a);
}
const P = JSON.parse(JSON.stringify(PARAMS));
for (const b of opt.brk) { const [lhs, v] = b.split('='); const [sp, k] = lhs.split('.'); P.species[sp].const[k] = +v; }
const SAMPLE_S = 1.0;

function aliveCount(sp) { let n = 0; const a = sp.alive; if (!a) return 0; for (let i = 0; i < a.length; i++) if (a[i]) n++; return n; }

function one(policy, seed) {
  const W = new E.FlightWorld(P, 'cell', seed, 1, { pilot: policy, drop: opt.drop, pop: opt.pop });
  const ar = W.arena;
  // the ledger: book every mass event to the species whose step made it (ar._src) and the prism's creator
  const creator = new Array(ar.n).fill('env');
  const book = { eat: {}, destroy: {}, steal: {}, move: {} };
  const who = (i) => (i < creator.length && creator[i]) || 'env';
  const tally = (kind, i, v) => { if (!(v > 0)) return; const d = book[kind][ar._src] || (book[kind][ar._src] = {}); const w = who(i); d[w] = (d[w] || 0) + v; };
  const lay = ar.layMass.bind(ar);
  ar.layMass = function (x, y, z, vol, elem, dom, danger, trail, shielded, owner) {
    const i = lay(x, y, z, vol, elem, dom, danger, trail, shielded, owner);
    creator[i] = (owner !== undefined && owner >= 0) ? 'wake' : ar._src; return i;
  };
  const con = ar.consume.bind(ar), des = ar.destroy.bind(ar), stl = ar.steal.bind(ar), mov = ar.moveMass.bind(ar);
  ar.consume = function (i) { const v = con(i); tally('eat', i, v); return v; };
  ar.destroy = function (i) { const v = des(i); tally('destroy', i, v); return v; };
  ar.steal = function (i, dom) { const v = stl(i, dom); tally('steal', i, v); return v; };
  ar.moveMass = function (i, x, y, z) { mov(i, x, y, z); tally('move', i, ar.mvol[i]); };
  const keys = W.species.map(s => s.key);
  const pops = {}; for (const k of keys) pops[k] = [];
  const crys0 = {}; for (const s of W.species) crys0[s.key] = s.crystals || 0;
  const steps = Math.round(opt.minutes * 60 / opt.dt), every = Math.max(1, Math.round(SAMPLE_S / opt.dt));
  const t0 = Date.now();
  for (let s = 0; s < steps; s++) {
    for (const sp of W.species) { ar._src = sp.key; sp.step(ar, opt.dt); if (sp.ram) sp.ram(ar); }
    ar._src = 'arena'; ar.step(opt.dt);
    if (s % every === 0) for (const sp of W.species) pops[sp.key].push(aliveCount(sp));
  }
  const ms = (Date.now() - t0) / steps, m = opt.minutes;
  const per = (d) => { const o = {}; for (const a in d) { o[a] = {}; for (const b in d[a]) o[a][b] = +(d[a][b] / m).toFixed(2); } return o; };
  const hitsBy = {}; for (const h of ar.log) hitsBy[h[4]] = (hitsBy[h[4]] || 0) + 1;
  for (const k in hitsBy) hitsBy[k] = +(hitsBy[k] / m).toFixed(3);
  const o = (f) => { const r = {}; for (const k of keys) r[k] = f(k); return r; };
  const mean = (a) => a.reduce((s, x) => s + x, 0) / a.length;
  const au = W.audit();
  return { policy, seed, minutes: m, drop: opt.drop.slice().sort(), pop: opt.pop, species: keys,
    hits_by: hitsBy, hits_per_min: +(ar.log.length / m).toFixed(3),
    eat: per(book.eat), destroy: per(book.destroy), steal: per(book.steal), move: per(book.move),
    pop_mean: o(k => +mean(pops[k]).toFixed(2)), pop_end: o(k => pops[k][pops[k].length - 1]), pop_start: o(k => pops[k][0]),
    crystals_per_min: o(k => +(((W.species.find(s => s.key === k).crystals || 0) - crys0[k]) / m).toFixed(3)),
    conservation: +au.residual.toFixed(6), ms_per_step: +ms.toFixed(2), dt: opt.dt };
}

const runs = []; const T0 = Date.now();
for (const pol of opt.policies) for (const s of opt.seeds) {
  const r = one(pol, s); runs.push(r);
  console.log(`${pol.padEnd(7)} seed ${String(s).padStart(4)}  hits/min ${r.hits_per_min.toFixed(2).padStart(6)}  ${r.ms_per_step.toFixed(1).padStart(6)} ms/step  residual ${r.conservation}  (${((Date.now() - T0) / 1000).toFixed(0)} s)`);
}
const tag = (opt.drop.length ? '_minus_' + opt.drop.slice().sort().join('_') : '') + (opt.pop === 'scored' ? '_scored' : '') + (opt.dt < 0.05 ? '_dt30' : '');
const out = opt.out ? path.resolve(HERE, opt.out) : path.join(HERE, 'results', `cell_js${tag}.json`);
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify({ dt: opt.dt, minutes: opt.minutes, drop: opt.drop.slice().sort(), pop: opt.pop, broken: opt.brk, runs }, null, 1));
console.log('wrote', out);
