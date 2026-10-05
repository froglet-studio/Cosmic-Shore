// siege_eval.js - what the SIEGE (src/70_siege.js) costs a pilot, and whether it is fair.
//
//   node Tools/Ecology/flight/siege_eval.js [--minutes 3] [--seeds 6] [--K '{"T_HOLD":1.5}'] [--ablate nosync] [--quiet]
//
// The siege runs in the bestiary world (scattered flora, trails on) against the three scripted pilots, modelled on
// stakes_eval.js: WANDER (careless), EVADER (skilled: turns away from the nearest member) and HUNTER (aggressive:
// rams), plus BREAKER, a gap-reader that is not one of the bestiary's pilots: once a shell is up it steers for the
// direction (from the shell centre) farthest from every member - the counterplay the design names, flown perfectly.
// Every danger contact goes through Stakes (tuned rule) with TeleTrack's lead; the Probe gives the feel axes and the
// first-strike telegraph. Per encounter = per GATHER the siege started. Writes results/siege_eval.json.
'use strict';
const fs = require('fs'), path = require('path');
const E = require('./sim.js');
const arg = (k, d) => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : d; };
const MIN = +arg('minutes', 3), SEEDS = +arg('seeds', 6), DT = 1 / 30, K = JSON.parse(arg('K', '{}')), ABL = arg('ablate', null);
const QUIET = process.argv.includes('--quiet');
const POLS = ['wander', 'evader', 'hunter', 'breaker'];
const DIRS = (() => { const n = 96, out = [], ga = Math.PI * (3 - Math.sqrt(5)); for (let i = 0; i < n; i++) { const y = 1 - 2 * (i + 0.5) / n, r = Math.sqrt(1 - y * y); out.push([Math.cos(ga * i) * r, y, Math.sin(ga * i) * r]); } return out; })();

/** the gap-reader: during a shell, aim along the open direction (largest angular clearance from members) out of C. */
function breakerSteer(ar, sp, p, dt) {
  const ph = sp.phase; let gx, gy, gz;
  if (ph === 'gather' || ph === 'close' || ph === 'hold') {
    const C = sp.C; let best = -2, bd = null;
    const U = []; for (let i = 0; i < sp.n; i++) if (sp.alive[i]) { const x = sp.pos[3 * i] - C[0], y = sp.pos[3 * i + 1] - C[1], z = sp.pos[3 * i + 2] - C[2], q = Math.max(Math.hypot(x, y, z), 1e-9); U.push(x / q, y / q, z / q); }
    const vn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-9);
    for (const d of DIRS) {
      let mx = -1; for (let j = 0; j < U.length; j += 3) { const c = d[0] * U[j] + d[1] * U[j + 1] + d[2] * U[j + 2]; if (c > mx) mx = c; }
      const score = -mx + 0.05 * (d[0] * p.vel[0] + d[1] * p.vel[1] + d[2] * p.vel[2]) / vn;   // widest gap (ties: ahead)
      if (score > best) { best = score; bd = d; }
    }
    gx = C[0] + bd[0] * 1000; gy = C[1] + bd[1] * 1000; gz = C[2] + bd[2] * 1000;
  } else {   // otherwise wander like the careless pilot
    if (Math.hypot(p.goal[0] - p.pos[0], p.goal[1] - p.pos[1], p.goal[2] - p.pos[2]) < 60) ar.ball(0.2 * ar.R, 0.9 * ar.R, p.goal);
    gx = p.goal[0]; gy = p.goal[1]; gz = p.goal[2];
  }
  const wx = gx - p.pos[0], wy = gy - p.pos[1], wz = gz - p.pos[2], n = Math.max(Math.hypot(wx, wy, wz), 1e-9);
  const vn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-9), vx = p.vel[0] / vn, vy = p.vel[1] / vn, vz = p.vel[2] / vn;
  const ang = Math.acos(E.clamp(vx * wx / n + vy * wy / n + vz * wz / n, -1, 1)), kk = Math.min(1, p.turn * dt / Math.max(ang, 1e-6));
  let nx = vx + (wx / n - vx) * kk, ny = vy + (wy / n - vy) * kk, nz = vz + (wz / n - vz) * kk; const nn = Math.max(Math.hypot(nx, ny, nz), 1e-9);
  p.vel[0] = nx / nn * p.speed; p.vel[1] = ny / nn * p.speed; p.vel[2] = nz / nn * p.speed;
}

function run(policy, seed, key) {
  key = key || 'siege';
  const ar = new E.Arena(seed, { world: 'bestiary' }); ar.scatterMass(3000); ar.enableTrails(15, 10);
  const PS = require('./params.json').species;
  const sp = new E.SPECIES[key](ar, PS[key], { seed, K, ablate: ABL }); sp.key = key;
  const pl = ar.addPilot(policy === 'breaker' ? Object.assign(new E.Pilot('player', 140, 'breaker'), { turn: 2.0 }) : E.makePilot(policy));
  const deep = new E.Stakes({ rule: 'tuned', hostile: true, start: 1000 });   // never empties: the true cost per encounter
  const st = new E.Stakes({ rule: 'tuned', hostile: true }), tt = new E.TeleTrack(), pr = new E.Probe(DT * 3), view = {};
  const leads = []; let cryN = 0;
  ar.onHit = (p, kind, amt, src, who, standing) => { if (p !== pl) return; const L = tt.lead(ar.t, src || key, who, standing); const ev = deep.contact(ar.t, kind, src || key, L >= 0.25); st.contact(ar.t, kind, src || key, L >= 0.25); if (ev && ev.total) leads.push(L); };
  ar.onKill = () => { cryN++; };
  const led0 = ar.liveVolume() + sp.ledger(); let drift = 0;
  const steps = Math.round(MIN * 60 / DT);
  for (let s = 0; s < steps; s++) {
    ar._src = key; sp.step(ar, DT);
    if (policy === 'breaker') breakerSteer(ar, sp, pl, DT);
    ar.step(DT);
    tt.observe(ar.t, pl, [sp]);
    if ((s + 1) % 3 === 0) pr.observe(ar, sp.view(view));
    if (s % 30 === 0 || s === steps - 1) drift = Math.max(drift, Math.abs(ar.liveVolume() + sp.ledger() + ar.destroyed - led0 - ar.trail_laid));
  }
  const S = deep.summary(MIN), S5 = st.summary(MIN), rep = sp.report ? sp.report() : {};
  return { policy, seed, burned: S.burned, contacts: S.contacts, tele_share: S.telegraphed_share, end: S5.end, stripped_at: S5.stripped_at,
    encounters: rep.encounters ?? null, escapes: rep.escapes ?? null, dives: rep.dives ?? null, dive_bites: rep.dive_bites ?? null, wall_bites: rep.wall_bites ?? null, breaches: rep.breaches ?? null,
    kills: sp.kills, leads, first_leads: pr.firstLeads(ar), feel: pr.feel(), conservation: +drift.toFixed(6), events: sp.events ? sp.events.slice(0, 12) : [] };
}

function summarise(key) {
  const out = { species: key, minutes: MIN, seeds: SEEDS, K, ablate: ABL, rows: {} };
  for (const pol of (key === 'siege' ? POLS : ['wander', 'evader', 'hunter'])) {
    const R = []; for (let s = 0; s < SEEDS; s++) R.push(run(pol, 100 + s, key));
    const sum = f => R.reduce((a, r) => a + (f(r) || 0), 0), med = a => a.length ? +E.median(a.slice().sort((x, y) => x - y)).toFixed(2) : null;
    const enc = sum(r => r.encounters), burned = sum(r => r.burned), tele = sum(r => (r.tele_share || 0) * r.burned);
    const feelKeys = Object.keys(R[0].feel), feel = {}; for (const k of feelKeys) feel[k] = +(sum(r => r.feel[k]) / R.length).toFixed(3);
    out.rows[pol] = { burned_per_min: +(burned / R.length / MIN).toFixed(2), burned_per_encounter: enc ? +(burned / enc).toFixed(2) : null,
      encounters_per_min: +(enc / R.length / MIN).toFixed(2), escape_share: enc ? +(sum(r => r.escapes) / enc).toFixed(2) : null,
      dive_bites_per_dive: sum(r => r.dives) ? +(sum(r => r.dive_bites) / sum(r => r.dives)).toFixed(2) : null, wall_bites_per_min: +(sum(r => r.wall_bites) / R.length / MIN).toFixed(2), breach_share: enc ? +(sum(r => r.breaches) / enc).toFixed(2) : null, dive_share: enc ? +(sum(r => r.dives) / enc).toFixed(2) : null,
      telegraphed_share: burned ? +(tele / burned).toFixed(2) : null, lead_s_median: med([].concat(...R.map(r => r.leads))),
      first_lead_s_median: med([].concat(...R.map(r => r.first_leads))), end_petals: +(sum(r => r.end) / R.length).toFixed(1),
      stripped_runs: R.filter(r => r.stripped_at !== null).length, kills_per_min: +(sum(r => r.kills) / R.length / MIN).toFixed(2),
      conservation_max: Math.max(...R.map(r => r.conservation)), feel };
  }
  const w = out.rows.wander, e = out.rows.evader, b = out.rows.breaker;
  out.headline = { careless_per_enc: w.burned_per_encounter, skilled_per_enc: e.burned_per_encounter, breaker_per_enc: b ? b.burned_per_encounter : null,
    skilled_over_careless: w.burned_per_min ? +(e.burned_per_min / w.burned_per_min).toFixed(2) : null,
    breaker_over_careless: b && w.burned_per_min ? +(b.burned_per_min / w.burned_per_min).toFixed(2) : null };
  return out;
}

if (require.main === module) {
  const t0 = Date.now(), res = summarise(arg('species', 'siege'));
  res.wall_s = +((Date.now() - t0) / 1000).toFixed(0);
  if (!QUIET) { fs.mkdirSync(path.join(__dirname, 'results'), { recursive: true }); fs.writeFileSync(path.join(__dirname, 'results', (res.species === 'siege' ? 'siege_eval' : 'siege_eval_' + res.species) + (ABL ? '_' + ABL : '') + '.json'), JSON.stringify(res, null, 1)); }
  const pad = (s, n) => String(s ?? '-').padEnd(n);
  console.log(pad('pilot', 9) + pad('burn/min', 9) + pad('burn/enc', 9) + pad('enc/min', 8) + pad('escape', 7) + pad('bite/dv', 8) + pad('breach', 7) + pad('dived', 6) + pad('tele', 6) + pad('lead', 6) + pad('1stlead', 8) + pad('end', 5) + pad('strip', 6) + 'kill/m');
  for (const [p, r] of Object.entries(res.rows)) console.log(pad(p, 9) + pad(r.burned_per_min, 9) + pad(r.burned_per_encounter, 9) + pad(r.encounters_per_min, 8) + pad(r.escape_share, 7) + pad(r.dive_bites_per_dive, 8) + pad(r.breach_share, 7) + pad(r.dive_share, 6) + pad(r.telegraphed_share, 6) + pad(r.lead_s_median, 6) + pad(r.first_lead_s_median, 8) + pad(r.end_petals, 5) + pad(r.stripped_runs + '/' + SEEDS, 6) + r.kills_per_min);
  console.log('feel(wander)', JSON.stringify(res.rows.wander.feel), 'cons', res.rows.wander.conservation_max);
  console.log(JSON.stringify(res.headline), 'wall', res.wall_s + 's');
}
module.exports = { run, summarise, breakerSteer };
