// stakes_eval.js - what the petal stakes (src/60_stakes.js) do to a pilot, per species, per rule.
//
//   node Tools/Ecology/flight/stakes_eval.js [--minutes 3] [--seeds 6]
//
// Each species runs in its own fidelity world (the scored Python harness, mirrored in 50_worlds.js) against the three
// scripted pilots: WANDER (careless: flies its own route, reads nothing), EVADER (skilled: turns away from what
// telegraphs) and HUNTER (aggressive: rams for kills, so it meets the crystals). Every danger contact goes through
// Stakes.contact with the telegraph lead from TeleTrack; every kill drops a crystal that pays a petal if the pilot
// passes within 16 u while it lasts (60 s). The pilots do not steer for crystals, so `gained` is a floor.
// Writes results/stakes_eval.json and prints a table. Questions it answers:
//   1. shipped (5 petals x 4) vs tuned (1 x 4): petals burned per minute, time to strip a 20-petal pilot;
//   2. does skill matter: careless vs skilled burn ratio (the bar: a skilled pilot loses clearly less);
//   3. are burns fair: telegraphed share of burned petals (the bar: most of them were read in advance).
'use strict';
const fs = require('fs'), path = require('path');
const E = require('./sim.js');
const P = JSON.parse(fs.readFileSync(path.join(__dirname, 'params.json'), 'utf8'));
const arg = (k, d) => { const i = process.argv.indexOf('--' + k); return i > 0 ? +process.argv[i + 1] : d; };
const MIN = arg('minutes', 3), SEEDS = arg('seeds', 6), DT = 1 / 30;
const POL = { bestiary: ['wander', 'evader', 'hunter'], builders: ['wander', 'evader', 'hunter'], flora: ['wander', 'reader', 'cutter'] };

function world(key, policy, seed) {
  const Pk = P.species[key], w = E.WORLD[key], o = { seed };
  let ar, sp;
  if (w === 'bestiary') { ar = new E.Arena(seed, { world: 'bestiary' }); ar.scatterMass(3000); ar.enableTrails(15, 10); sp = new E.SPECIES[key](ar, Pk, o); ar.addPilot(E.makePilot(policy)); }
  else if (w === 'builders') { ar = new E.Arena(seed, { world: 'builders' }); ar.scatterMass(Pk.extra.harness_mass); const p = E.makePilot(policy); p.trail_every = Pk.extra.TRAIL_EVERY; ar.addPilot(p); sp = new E.SPECIES[key](ar, Pk, o); }
  else { ar = new E.Arena(seed, { world: 'flora' }); Object.assign(ar, { GROVE_C: Pk.extra.GROVE_C, GROVE_R: Pk.extra.GROVE_R, SLOW_S: Pk.extra.SLOW_S, SLOW_STRENGTH: Pk.extra.SLOW_STRENGTH }); ar.groveMass(1600, 20); sp = new E.SPECIES[key](ar, Pk, o); ar.species = sp; ar.addPilot(E.makePilot(policy)); }
  sp.key = key;
  return { ar, sp, w };
}

function run(key, policy, seed, rule) {
  const { ar, sp, w } = world(key, policy, seed), pl = ar.pilots[0];
  const st = new E.Stakes({ rule, hostile: true }), tt = new E.TeleTrack(), cry = [];
  ar.onHit = (p, kind, amt, src, who, standing) => { if (p === pl) st.contact(ar.t, kind, src || key, tt.lead(ar.t, src || key, who, standing) >= 0.25); };
  ar.onKill = (s, i) => { const X = s.pos || s.h; if (X) cry.push([X[3 * i], X[3 * i + 1], X[3 * i + 2], ar.t, E.SPECIES_ELEMENT[key]]); };
  const steps = Math.round(MIN * 60 / DT);
  for (let s = 0; s < steps; s++) {
    ar._src = key; sp.step(ar, DT);
    if (w === 'builders') sp.ram(ar);
    if (w === 'flora' && policy === 'cutter') sp.cut(ar, pl, pl.prev, pl.pos);
    ar.step(DT);
    tt.observe(ar.t, pl, [sp]);
    for (let c = cry.length - 1; c >= 0; c--) {
      const q = cry[c];
      if (ar.t - q[3] > 60) { cry.splice(c, 1); continue; }
      if ((q[0] - pl.pos[0]) ** 2 + (q[1] - pl.pos[1]) ** 2 + (q[2] - pl.pos[2]) ** 2 < 256) { st.collect(q[4], ar.t); cry.splice(c, 1); }
    }
  }
  return st.summary(MIN);
}

const keys = Object.keys(E.SPECIES), out = { minutes: MIN, seeds: SEEDS, dt: DT, rows: [] };
const t0 = Date.now();
for (const key of keys) for (const policy of POL[E.WORLD[key]]) for (const rule of ['shipped', 'tuned']) {
  const R = []; for (let s = 0; s < SEEDS; s++) R.push(run(key, policy, 100 + s, rule));
  const m = f => R.reduce((a, r) => a + f(r), 0) / R.length;
  const strip = R.filter(r => r.stripped_at !== null).map(r => r.stripped_at);
  const burned = R.reduce((a, r) => a + r.burned, 0), tele = R.reduce((a, r) => a + (r.telegraphed_share || 0) * r.burned, 0);
  out.rows.push({ species: key, policy, rule, burned_per_min: +m(r => r.burned_per_min).toFixed(2), gained_per_min: +(m(r => r.gained) / MIN).toFixed(2),
    contacts_per_min: +(m(r => r.contacts) / MIN).toFixed(2), end_petals: +m(r => r.end).toFixed(1),
    stripped_runs: strip.length, strip_s_median: strip.length ? +E.median(strip.slice().sort((a, b) => a - b)).toFixed(0) : null,
    telegraphed_share: burned ? +(tele / burned).toFixed(2) : null });
}
out.wall_s = (Date.now() - t0) / 1000;
// headline per rule: careless (wander) vs skilled (evader / reader) and fairness, pooled over species
const pool = (rule, pols) => { const r = out.rows.filter(x => x.rule === rule && pols.includes(x.policy)); return +(r.reduce((a, x) => a + x.burned_per_min, 0) / r.length).toFixed(2); };
out.headline = {};
for (const rule of ['shipped', 'tuned']) {
  const rows = out.rows.filter(x => x.rule === rule), runs = rows.length * SEEDS;
  out.headline[rule] = { careless_burn_per_min: pool(rule, ['wander']), skilled_burn_per_min: pool(rule, ['evader', 'reader']), aggressive_burn_per_min: pool(rule, ['hunter', 'cutter']),
    stripped_share: +(rows.reduce((a, x) => a + x.stripped_runs, 0) / runs).toFixed(2),
    telegraphed_share: (() => { const r = rows.filter(x => x.telegraphed_share !== null); const w = r.reduce((a, x) => a + x.burned_per_min, 0); return w ? +(r.reduce((a, x) => a + x.telegraphed_share * x.burned_per_min, 0) / w).toFixed(2) : null; })() };
}
fs.mkdirSync(path.join(__dirname, 'results'), { recursive: true });
fs.writeFileSync(path.join(__dirname, 'results', 'stakes_eval.json'), JSON.stringify(out, null, 1));
const pad = (s, n) => String(s).padEnd(n);
console.log(pad('species', 11) + pad('pilot', 8) + pad('rule', 8) + pad('burn/min', 9) + pad('gain/min', 9) + pad('end', 6) + pad('stripped', 9) + pad('strip_s', 8) + 'read');
for (const r of out.rows) console.log(pad(r.species, 11) + pad(r.policy, 8) + pad(r.rule, 8) + pad(r.burned_per_min, 9) + pad(r.gained_per_min, 9) + pad(r.end_petals, 6) + pad(r.stripped_runs + '/' + SEEDS, 9) + pad(r.strip_s_median ?? '-', 8) + (r.telegraphed_share ?? '-'));
console.log(JSON.stringify(out.headline), 'wall', out.wall_s.toFixed(0) + 's');
