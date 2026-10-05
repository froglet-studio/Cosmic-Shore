// ===================================================================================================================
// 50_worlds.js - (1) the three FIDELITY worlds, each a JS mirror of the Python harness its species was scored in,
// plus a headless runner returning exactly the statistics flight/fidelity_py.py records; (2) the FLIGHT world the
// page flies: one cell, every species at once (or one alone), the player as the only pilot.
// ===================================================================================================================
const SPECIES = { pack: Pack, thief: Thief, locust: Locust, lurker: Lurker, stampede: Stampede, leviathan: Leviathan,
  mobber: Mobber, grazer: Grazer, fortress: Fortress, snaptrap: SnapTrap, siege: Siege };
const WORLD = { pack: 'bestiary', thief: 'bestiary', locust: 'bestiary', lurker: 'bestiary', stampede: 'bestiary',
  leviathan: 'bestiary', mobber: 'bestiary', grazer: 'bestiary', fortress: 'builders', snaptrap: 'flora', siege: 'bestiary' };
/** JS-original species: no Python reference, so the fidelity gate (fidelity_js.js) skips them; siege_eval.js scores them. */
const JS_ORIGINAL = { siege: true };
const POLICIES = { bestiary: ['wander', 'evader', 'hunter'], builders: ['wander', 'evader', 'hunter'], flora: ['wander', 'reader', 'cutter'] };

function makePilot(policy) {
  if (policy === 'wander') return Pilot.wanderer();
  if (policy === 'evader') return Pilot.evader();
  if (policy === 'hunter') return Pilot.hunter();
  if (policy === 'reader') return new Pilot('reader', 120, 'reader');
  if (policy === 'cutter') return new Pilot('cutter', 140, 'cutter');
  throw new Error('policy ' + policy);
}
/** run one species x pilot x seed in its own fidelity world; dt = sim step; observeEvery = probe every k steps. */
function runOne(PARAMS, key, policy, seed, minutes, dt, observeEvery, overrides) {
  const P = PARAMS.species[key], world = WORLD[key], o = Object.assign({ seed }, overrides || {});
  let ar, sp;
  if (world === 'bestiary') {
    ar = new Arena(seed, { world: 'bestiary' }); ar.scatterMass(3000); ar.enableTrails(15, 10);
    sp = new SPECIES[key](ar, P, o); ar.addPilot(makePilot(policy));
  } else if (world === 'builders') {
    ar = new Arena(seed, { world: 'builders' }); ar.scatterMass(P.extra.harness_mass);
    const p = makePilot(policy); p.trail_every = P.extra.TRAIL_EVERY; ar.addPilot(p);
    sp = new SPECIES[key](ar, P, o);
  } else {
    ar = new Arena(seed, { world: 'flora' });
    ar.GROVE_C = P.extra.GROVE_C; ar.GROVE_R = P.extra.GROVE_R; ar.SLOW_S = P.extra.SLOW_S; ar.SLOW_STRENGTH = P.extra.SLOW_STRENGTH;
    ar.groveMass(1600, 20);
    sp = new SPECIES[key](ar, P, o); ar.species = sp; ar.addPilot(makePilot(policy));
  }
  const pr = new Probe(dt * observeEvery), steps = Math.round(minutes * 60 / dt), view = {};
  const led0 = ar.liveVolume() + sp.ledger();
  let drift = 0; const t0 = now();
  for (let s = 0; s < steps; s++) {
    ar._src = sp.key || sp.name; sp.step(ar, dt);
    if (world === 'builders') sp.ram(ar);
    if (world === 'flora' && policy === 'cutter') { const p = ar.pilots[0]; sp.cut(ar, p, p.prev, p.pos); }
    ar.step(dt);
    if ((s + 1) % observeEvery === 0) pr.observe(ar, sp.view(view));
    if (s % 25 === 0 || s === steps - 1)   // the bestiary's ledger: live + held + actively removed - start - trail laid
      drift = Math.max(drift, Math.abs(ar.liveVolume() + sp.ledger() + ar.destroyed + (sp.cutVolume || 0) - led0 - ar.trail_laid));
  }
  const ms = (now() - t0) / steps;
  const kinds = {}; for (const h of ar.log) kinds[h[2]] = (kinds[h[2]] || 0) + 1;
  const leads = pr.leads.map(x => +x.toFixed(2)), fl = pr.firstLeads(ar).map(x => +x.toFixed(2));
  const sorted = pr.leads.slice().sort((a, b) => a - b);
  return { species: key, policy, seed, minutes, hits_per_min: ar.log.length / minutes, kinds,
    crystals_per_min: sp.crystals / minutes, telegraph_s: sorted.length ? +median(sorted).toFixed(2) : null,
    leads, first_leads: fl, feel: pr.feel(), ms_per_step: +ms.toFixed(3), conservation: drift, dt };
}
function now() { return (typeof performance !== 'undefined' ? performance.now() : Date.now()); }

// ------------------------------------------------------------------------------------------------ the flight world
/** One cell for the page: scattered flora mass, the player as the only pilot, and the chosen species.
 *  mode: 'cell' = every species at once (vibrancy), or a species key (judge one at a time). */
function FlightWorld(PARAMS, mode, seed, scale) {
  this.P = PARAMS; this.mode = mode; scale = scale || 1;
  const ar = this.arena = new Arena(seed || 7, { world: 'flight' });
  ar.scatterMass(mode === 'cell' || mode === 'showcase' ? 4200 : 3000);
  ar.enableTrails(15, 10); ar.trailDom = 1; ar.trailFlag = 1;   // the player's wake: conserved mass, theirs to lose
  const pl = this.player = new Pilot('player', 120, 'you'); pl.rams = true; pl.turn = 0;
  ar.addPilot(pl);
  pl.pos = [0, 0, -0.62 * ar.R]; pl.vel = [0, 0, 120];
  this.species = [];
  const add = (key, o) => { const sp = new SPECIES[key](ar, PARAMS.species[key], Object.assign({ seed: seed || 7 }, o || {})); sp.key = key; this.species.push(sp); return sp; };
  const R = ar.R;
  if (mode === 'cell' || mode === 'showcase') {
    // near the player's start, the rest spread round the cell; positions only - every rule is the species' own
    add('grazer', { n: Math.round(900 * scale), cap: Math.round(1600 * scale), clusters: 10 });
    add('locust', { cap: Math.round(360 * scale) });
    add('stampede');
    add('mobber');
    add('thief');
    add('pack');
    add('lurker');
    add('leviathan');
    add('fortress', { anchor: [0.38 * R, 0.1 * R, -0.25 * R] });
    add('snaptrap', { placeClump: (a, C, o) => { a.ball(0.3 * R, 0.75 * R, C, o); } });
    // the showcase: the whole cell plus the siege, starting on the far side so the first minute is the calm cell
    // first siege after ~40 s, then one every ~40 s, so the cell gets to be a cell between them
    if (mode === 'showcase') { const sg = add('siege', { centre: [0.25 * R, 0.15 * R, 0.45 * R], K: { T_COOL: 30, T_COOL_ESC: 25 } }); sg.cool = 40; }
  } else if (mode === 'snaptrap') {
    add('snaptrap', { placeClump: (a, C, o) => { a.ball(0.15 * R, 0.55 * R, C, o); C[o + 2] -= 0.25 * R; } });
  } else if (mode === 'siege') {
    // the siege stalks in from ahead-left; its rules (and every number) are its own (src/70_siege.js)
    add('siege', { centre: [-0.2 * R, 0.05 * R, -0.35 * R] });
  } else if (mode === 'fortress') {
    add('fortress', { anchor: [0, 0.05 * R, -0.3 * R] });
  } else {
    add(mode, { centre: [0, 0, -0.3 * R] });
  }
  ar.species = this.species.find(s => s.key === 'snaptrap') || null;
  this.start = ar.liveVolume() + this.speciesHeld();
  this.view = {};
}
FlightWorld.prototype.speciesHeld = function () { let s = 0; for (const sp of this.species) s += sp.ledger ? sp.ledger() : 0; return s; };
FlightWorld.prototype.step = function (dt) {
  const ar = this.arena;
  for (const sp of this.species) { ar._src = sp.key || sp.name; sp.step(ar, dt); if (sp.ram) sp.ram(ar); }
  ar.step(dt);
};
/** the conservation ledger the debug overlay shows: live + held + actively removed = start + the player's wake. */
FlightWorld.prototype.audit = function () {
  const ar = this.arena; let held = 0, cut = 0;
  for (const sp of this.species) { held += sp.ledger(); cut += sp.cutVolume || 0; }
  const live = ar.liveVolume();
  return { start: this.start, wake: ar.trail_laid, live, held, eaten_by_fauna: ar.eaten, destroyed: ar.destroyed, cut,
    stolen: ar.stolen, residual: live + held + ar.destroyed + cut - this.start - ar.trail_laid, prisms: ar.n };
};
