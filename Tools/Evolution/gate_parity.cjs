#!/usr/bin/env node
// The parity gate (Docs/EVOLUTION.md §7): Tools/Evolution/sim.js must reproduce the C# harness's golden trajectories
// (Tools/Evolution/results/golden.json, written by `bash Tools/Build/evolution_harness/run.sh golden`) EXACTLY -
// every number of every census row, the RNG's first draws, eight mutation samples, a phenotype and a packed genome.
// It then plants three defects in the JS (a flipped multiplier branch, a shifted RNG, a lost fround) and requires
// each to be caught, so a gate that cannot bite never reports a pass.
//
//   node Tools/Evolution/gate_parity.cjs            # exit 0 = the page flies the model the harness measured
'use strict';
const fs = require('fs');
const path = require('path');
const Evo = require(path.join(__dirname, 'sim.js'));
const golden = JSON.parse(fs.readFileSync(path.join(__dirname, 'results', 'golden.json'), 'utf8'));
const f32 = Math.fround;

let fails = 0;
function check(ok, what) { console.log(`  [${ok ? 'ok' : 'FAIL'}] ${what}`); if (!ok) fails++; }

function paramsFromGolden(gp) {
  const over = Object.assign({}, gp);
  over.Evolution = Object.assign({}, gp.Evolution);
  return Evo.params(over);
}

function runCase(c, EvoImpl) {
  const p = (EvoImpl || Evo).params(Object.assign({}, c.params, { Evolution: Object.assign({}, c.params.Evolution) }));
  const arena = new (EvoImpl || Evo).Arena(p, c.seed);
  const rows = [];
  for (let s = 0; s < c.steps; s += c.every) { arena.advance(c.every); rows.push(arena.census()); }
  return rows;
}

function firstDiff(a, b) {
  if (a.length !== b.length) return `row count ${a.length} vs ${b.length}`;
  for (let i = 0; i < a.length; i++) {
    for (let k = 0; k < a[i].length; k++) {
      if (a[i][k] !== b[i][k]) return `row ${i} col ${k}: js ${a[i][k]} vs golden ${b[i][k]}`;
    }
  }
  return null;
}

console.log(`parity: ${golden.cases.length} golden cases`);
for (const c of golden.cases) {
  // the RNG
  const rng = new Evo.GenomeRng(c.seed);
  const first8 = []; for (let i = 0; i < 8; i++) first8.push(rng.nextUnit());
  check(first8.every((v, i) => v === c.rngFirst8[i]), `${c.name}: GenomeRng's first 8 uniforms match`);

  // mutation samples + phenotype + pack, straight from the core
  const p = paramsFromGolden(c.params);
  const r2 = new Evo.GenomeRng(c.seed);
  let g = Evo.genome(0.25, -0.5, 0.75, 0);
  let mutOk = true;
  for (let i = 0; i < c.mutations.length; i++) {
    g = Evo.mutate(g, p.Evolution, r2);
    for (let l = 0; l < 4; l++) if (g[l] !== f32(c.mutations[i][l])) mutOk = false;
  }
  check(mutOk, `${c.name}: 8 mutation samples match (float32)`);
  const ph = Evo.express(g, p.Evolution);
  const phArr = [ph.pace, ph.reach, ph.upkeep, ph.fecundity, ph.provision, ph.cohesion];
  check(phArr.every((v, i) => v === f32(c.phenotype[i])), `${c.name}: the phenotype matches`);
  check(Evo.pack(g) === c.packed, `${c.name}: the packed genome matches (${c.packed})`);

  // the trajectory
  const rows = runCase(c);
  const d = firstDiff(rows, c.rows);
  check(d === null, `${c.name}: ${c.steps} steps, ${rows.length} census rows identical${d ? ' - ' + d : ''}`);
}

// ---- negative controls: three planted defects, each must be caught by the trajectory diff ----
const base = golden.cases[0];
function withPlanted(mutator) {
  // re-load a fresh copy of the module, then patch it
  delete require.cache[require.resolve(path.join(__dirname, 'sim.js'))];
  const E = require(path.join(__dirname, 'sim.js'));
  mutator(E);
  return E;
}
{
  // (1) a lost fround on the mutation step: the model drifts off the float32 lattice the C# lives on
  const E = withPlanted(E => {
    const orig = E.mutate;
    E.mutate = function (parent, s, rng) { const g = orig(parent, s, rng); return g.map(v => v + 1e-9); };
  });
  // the Arena class captured `mutate` lexically, so patch through a subclass that re-expresses births
  const rowsGolden = base.rows;
  const p = E.params(Object.assign({}, base.params, { Evolution: Object.assign({}, base.params.Evolution) }));
  const arena = new E.Arena(p, base.seed);
  // plant inside the arena: nudge every living genome once at the start
  for (const a of arena.h) a.g = a.g.map(v => v + 1e-9);
  const rows = [];
  for (let s = 0; s < base.steps; s += base.every) { arena.advance(base.every); rows.push(arena.census()); }
  check(firstDiff(rows, rowsGolden) !== null, 'negative control bites: a 1e-9 nudge off the float32 lattice is caught');
}
{
  // (2) a shifted RNG: one extra draw at the start
  const p = Evo.params(Object.assign({}, base.params, { Evolution: Object.assign({}, base.params.Evolution) }));
  const arena = new Evo.Arena(p, base.seed);
  arena.rng.nextUnit();
  const rows = [];
  for (let s = 0; s < base.steps; s += base.every) { arena.advance(base.every); rows.push(arena.census()); }
  check(firstDiff(rows, base.rows) !== null, 'negative control bites: one extra RNG draw is caught');
}
{
  // (3) a flipped expression: the upkeep cost of pace removed. Planted in the FAMINE case: in the cap-bound Blob
  // balance nobody starves inside 20 minutes, so upkeep is inert there and a defect in it would be invisible -
  // which the gate's first draft found out, and is why this control runs where the stomach decides.
  const famine = golden.cases.find(c => c.name.startsWith('sparse')) || base;
  const p = Evo.params(Object.assign({}, famine.params, { Evolution: Object.assign({}, famine.params.Evolution) }));
  p.Evolution.TempoUpkeepRange = 1;
  const arena = new Evo.Arena(p, famine.seed);
  const rows = [];
  for (let s = 0; s < famine.steps; s += famine.every) { arena.advance(famine.every); rows.push(arena.census()); }
  check(firstDiff(rows, famine.rows) !== null, `negative control bites: dropping the cost of pace is caught (${famine.name})`);
}

console.log(fails === 0 ? 'parity gate: OK' : `parity gate: ${fails} FAILURE(S)`);
process.exit(fails === 0 ? 0 : 1);
