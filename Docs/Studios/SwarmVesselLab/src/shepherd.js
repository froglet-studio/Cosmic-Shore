// ============================================================================================
// shepherd.js — round 2: THE SHEPHERD. One pilot (a Rhino hull), two sheepdogs (one phone player, one
// thumb per dog) and a flock of sheep (the swarm). Garrett's brief, 2026-10-20:
//   - dogs are repulsive and live on the pilot's XZ plane; their minimum repulsion radius is larger than a
//     big thumb's occlusion, so the ring always shows round the thumb that holds the dog;
//   - the pilot is repulsive too, and can briefly CALL the flock toward itself on a long cooldown;
//   - sheep fly in 3D like a swarm but are strongly drawn to the pilot's XZ plane;
//   - lifting a dog's thumb leaves a repulsive DECOY (one at a time; a new one replaces the old);
//   - the pilot teleports to the decoy with a trigger or by lifting its thumb;
//   - Mass = breeding rate, Time = flock speed, Space = repulsion radius of pilot, dogs and decoy,
//     Charge = share of danger sheep (0% at -5, 100% at 15);
//   - L5: Mass poops a cluster of mass, Space eats shielded prisms, Charge attacks players, Time leaves
//     a decoy where the pilot was when it teleports;
//   - the pilot has the Rhino's full speed and its fly-straight ramp.
// Same contract as the rest of the lab: shepStep never draws and never reads a device; the AI pilot and
// the AI dogs write the SAME input object a thumb, a key or a gamepad does; every run is seeded.
// ============================================================================================

// ================= (1) the shipped numbers this mode reads, and its own knobs =================
const SHEP_SHIPPED = {
  rhinoCruise: 50,      // Rhino.prefab VesselTransformer.DefaultThrottleScaler (full throttle, no boost)
  rhinoMaxBoost: 16.8,  // RhinoRampBoostAction.asset maxBoostMultiplier (50 x 16.8 = 840 u/s flying straight)
  rhinoAccel: 220,      // RhinoRampBoostAction.asset accelerationPerSecond (Time 0 multiplier x1)
  rhinoBleed: 300,      // RhinoRampBoostAction.asset bleedPerSecond (steering inside the ramp)
  rhinoReturn: 120,     // RhinoRampBoostAction.asset returnPerSecond (the coast after the ramp lets go)
  rhinoPlateau: 0.3,    // StraightLineGesture.EngageThreshold (full boost below this stick deviation)
  rhinoGrace: 1,        // RhinoRampBoostAction.asset straightnessGraceBand (boost reaches 0 here)
  rhinoTurn: 90,        // Rhino.prefab PitchScaler / YawScaler (deg/s at full stick, plus speed x 0.2)
  rhinoRts: 0.2,        // Rhino.prefab RotationThrottleScaler
  elTime: 2,            // lab: elements run -5..15 here, Garrett's Charge scale (0% at -5, 100% at 15)
  elSpace: 2,
  elMass: 2,
  elCharge: 2,
  thumbMm: 22,          // lab: a BIG thumb's occlusion, in mm (the disc of screen a thumb hides)
  thumbMargin: 1.25,    // lab: the smallest ring is this many thumb radii
  viewSpan: 1400,       // lab: world units across the screen's SHORT side (a landscape phone's height)
  sheep: 60,            // lab: flock size at the start
  sheepCap: 180,        // lab: most sheep alive at once
  sheepSpeed: 70,       // lab: sheep cruise (u/s) at Time 0; panic adds up to +80%
  planePull: 1.6,       // lab: how hard sheep are drawn to the pilot's XZ plane (1/s^2)
  dogSpeed: 520,        // lab: how fast a dog runs to its thumb (u/s)
  callTime: 2.5,        // lab: seconds the pilot's call lasts
  callCooldown: 20,     // lab: seconds before the call is ready again
  breedFood: 4,         // lab: prisms a sheep eats to have a lamb at Mass -5 (fewer as Mass rises)
  pooEvery: 6,          // lab: Mass L5, seconds between clusters
  attackRange: 200,     // lab: Charge L5, how close a rival must be for the flock to go for it
  intensity: 2,         // lab: I1..I4 for every game (rivals, shields, pen size)
  aiNoise: 0.15,        // lab: AI pilot and AI dogs sloppiness (0 expert, 1 rookie)
  round: 45,            // lab: seconds per round
};
const SHEP_SPEC = [
  ['Elements', 'elTime', -5, 15, 1, 'Time', 'Flock speed: +6% per level (floor 40%). L5: when the pilot teleports, a decoy is left where it was.'],
  ['Elements', 'elSpace', -5, 15, 1, 'Space', 'Repulsion radius of pilot, both dogs and the decoy: the thumb floor at -5, +6% of it per level. L5: sheep can eat shielded prisms.'],
  ['Elements', 'elMass', -5, 15, 1, 'Mass', 'Breeding: prisms a sheep eats per lamb fall from 4 at -5 to about 1.3 at 15. L5: the flock poops a cluster of mass every few seconds.'],
  ['Elements', 'elCharge', -5, 15, 1, 'Charge', 'Share of danger sheep: 0% at -5, 35% at 2, 75% at 10, 100% at 15. L5: sheep near a rival go for it.'],
  ['Thumb', 'thumbMm', 0, 30, 1, 'Big thumb (mm)', 'The disc of screen a big thumb hides. 0 turns the floor off (what a mouse would allow).'],
  ['Thumb', 'thumbMargin', 1, 2, 0.05, 'Ring / thumb', 'The smallest repulsion ring, in thumb radii. Above 1 the ring always peeks out round the thumb.'],
  ['Thumb', 'viewSpan', 800, 2400, 50, 'View span', 'World units across the screen\'s short side. The floor is fixed in world units from a 390 px landscape phone.'],
  ['Flock', 'sheep', 10, 150, 1, 'Starting flock', 'Sheep at the start of a round.'],
  ['Flock', 'sheepCap', 40, 300, 5, 'Flock cap', 'Most sheep alive at once.'],
  ['Flock', 'sheepSpeed', 30, 160, 1, 'Sheep speed', 'Cruise at Time 0. Fleeing adds up to +80%.'],
  ['Flock', 'planePull', 0.2, 4, 0.1, 'Pull to the plane', 'How strongly sheep are drawn to the pilot\'s XZ plane. Fly up or down and the flock follows.'],
  ['Flock', 'breedFood', 1, 10, 0.5, 'Meals per lamb', 'At Mass -5. Mass divides it by 1 + 0.1 per level above -5.'],
  ['Crew', 'dogSpeed', 150, 1200, 10, 'Dog speed', 'How fast a dog runs to its thumb. Fast reads as "under my thumb".'],
  ['Crew', 'callTime', 0.5, 6, 0.1, 'Call length (s)', 'How long the pilot pulls the flock in.'],
  ['Crew', 'callCooldown', 3, 60, 1, 'Call cooldown (s)', 'The long cooldown on the call.'],
  ['Crew', 'pooEvery', 2, 20, 0.5, 'Poop every (s)', 'Mass L5: seconds between clusters of mass.'],
  ['Crew', 'attackRange', 60, 500, 10, 'Attack range', 'Charge L5: how close a rival must be before the flock goes for it.'],
  ['Hull', 'rhinoCruise', 20, 120, 1, 'Rhino cruise', 'Shipped Rhino throttle scaler.'],
  ['Hull', 'rhinoMaxBoost', 1, 24, 0.1, 'Straight boost', 'Shipped ramp ceiling (x cruise).'],
  ['Hull', 'rhinoAccel', 20, 600, 5, 'Ramp acceleration', 'Shipped ramp wind-up (u/s per s).'],
  ['Arena', 'intensity', 1, 4, 1, 'Intensity', 'I1-I4: Charge rivals 1-4, Space shields 25-55%, Time pens shrink, rivals faster.'],
  ['Arena', 'aiNoise', 0, 1, 0.05, 'AI noise', '0 = expert AI pilot and dogs, 1 = rookies. The scorecard runs both.'],
  ['Arena', 'round', 20, 120, 5, 'Round (s)', 'Seconds per round.'],
];
const SHEP_LS = 'lab:' + LAB.name + ':shepP';
const SP = Object.assign({}, SHEP_SHIPPED, lsGet(SHEP_LS, {}));
for (const k of Object.keys(SP)) if (!(k in SHEP_SHIPPED)) delete SP[k];

const SHEP_GAMES = [
  { id: 'time', name: 'Time: pen race', el: 'time', how: 'Five pens in a loop. A pen counts when 60% of the flock stands in it at once. Score: pens x 10, plus seconds to spare.' },
  { id: 'space', name: 'Space: graze the fort', el: 'space', how: 'A ruby fort, part shielded. Sheep eat the plain prisms; Space L5 lets them eat the shielded ones. Score: enemy prisms eaten.' },
  { id: 'mass', name: 'Mass: grow the flock', el: 'mass', how: 'Pasture in patches. Every meal brings a lamb closer; Mass L5 poops clusters of your own mass. Score: lambs born plus your mass standing.' },
  { id: 'charge', name: 'Charge: wolves', el: 'charge', how: 'Rival ships hunt the pilot and eat plain sheep. A danger sheep they touch costs them a petal and slows them. Score: petals taken - lost + 3 per knockout.' },
];
const SHEP_ROLES = [
  { id: 'dogs', name: 'You: the dogs', how: 'One thumb per dog (or drag with the mouse). Lift a thumb to leave a decoy. The AI flies the pilot.' },
  { id: 'pilot', name: 'You: the pilot', how: 'Stick: WASD / arrows, a gamepad stick, or the left half of the screen. Call: E, A button or the CALL pad. Teleport: Space, right trigger, or lift the stick thumb. The AI runs the dogs.' },
  { id: 'both', name: 'You: both (hot seat)', how: 'Pilot on keyboard or gamepad, dogs on touch or mouse.' },
  { id: 'ai', name: 'Watch the AI', how: 'Both crews are AI. Use it to see the scheme work before you try it.' },
];

// ================= (2) element rules =================
const L5 = (v) => v >= 5;
const SEL = {
  speed: () => Math.max(0.4, 1 + 0.06 * SP.elTime),
  floorU: () => (SP.thumbMm / 2) * 6.3 * SP.thumbMargin * SP.viewSpan / 390,   // 6.3 CSS px per mm on a phone; 390 px = landscape phone height
  ring: () => Math.max(40, SEL.floorU() * (1 + 0.06 * (SP.elSpace + 5))),      // never below the floor; floor turned off -> 40 u stand-in
  meals: () => SP.breedFood / (1 + 0.1 * (SP.elMass + 5)),
  danger: () => clamp((SP.elCharge + 5) / 20, 0, 1),
};
const SH_R = 3200;            // arena radius (the cell): the Rhino needs room to run its 840 u/s line
const SH_RIVAL_PETALS = 10;

// ================= (3) the world =================
let SW = null;
function shepWorld(seed, game, opt) {
  const w = {
    seed, rnd: mulberry32(seed * 7919 + 13), t: 0, game, opt: opt || {},
    pilot: { x: -500, y: 0, z: 0, yaw: Math.PI / 2, pitch: 0, v: SP.rhinoCruise, petals: 10, hitCd: 0, callT: 0, callCd: 0, teleCd: 0, off: !!(opt && opt.noPilot) },
    dogs: [{ x: -300, y: 0, z: 260, down: false, id: 0, off: !!(opt && opt.noDogs) }, { x: -300, y: 0, z: -260, down: false, id: 1, off: !!(opt && opt.noDogs) }],
    decoy: null, sheep: [], prisms: [], rivals: [], pens: [], penNext: 0, fx: [], prev: { tele: false, call: false, d0: false, d1: false },
    stats: { eaten: 0, enemyEaten: 0, born: 0, lost: 0, pooped: 0, petalsTaken: 0, petalsLost: 0, ko: 0, teleports: 0, calls: 0, decoys: 0, dangerHits: 0 },
    pooT: SP.pooEvery, regrowT: 0, finishT: null, done: false, hash: null, phash: null, phashDirty: true, ai: { noiseT: 0, nx: 0, nz: 0, liftT: 4, lift: 0, pnx: 0, pnz: 0 },
  };
  SW = w;
  const I = SP.intensity;
  for (let i = 0; i < SP.sheep; i++) { const a = w.rnd() * TAU, r = Math.sqrt(w.rnd()) * 260; addSheep(Math.cos(a) * r, (w.rnd() - 0.5) * 60, Math.sin(a) * r); }
  w.stats.born = 0;
  // pasture: patches of neutral prisms near the plane, everywhere (food for every game)
  const patches = game === 'mass' ? 9 : 5;
  for (let p = 0; p < patches; p++) {
    const a = w.rnd() * TAU, r = (game === 'mass' ? 300 : 500) + w.rnd() * (game === 'mass' ? 900 : 1500), cx = Math.cos(a) * r, cz = Math.sin(a) * r;
    for (let i = 0; i < (game === 'mass' ? 34 : 18); i++) { const b = w.rnd() * TAU, rr = Math.sqrt(w.rnd()) * 150; addShPrism(cx + Math.cos(b) * rr, (w.rnd() - 0.5) * 70, cz + Math.sin(b) * rr, 'pasture'); }
  }
  if (game === 'space') {
    // the fort: three shells of ruby prisms round a core, part shielded
    w.fort = { x: 1300, z: 300 };
    const shield = 0.15 + 0.1 * I;
    for (let s = 0; s < 3; s++) {
      const rad = 90 + s * 70, n = Math.floor(TAU * rad / 34);
      for (let i = 0; i < n; i++) for (const yy of [-30, 0, 30]) { const a = i / n * TAU + s * 0.3; addShPrism(w.fort.x + Math.cos(a) * rad, yy, w.fort.z + Math.sin(a) * rad, 'enemy', w.rnd() < shield + (2 - s) * 0.12); }
    }
    for (let i = 0; i < 50; i++) { const a = w.rnd() * TAU, r = Math.sqrt(w.rnd()) * 80; addShPrism(w.fort.x + Math.cos(a) * r, (w.rnd() - 0.5) * 60, w.fort.z + Math.sin(a) * r, 'enemy', w.rnd() < shield + 0.25); }
  }
  if (game === 'time') {
    const pr = 360 - 25 * I;
    for (let i = 0; i < 5; i++) { const a = -Math.PI / 2 + i / 5 * TAU * 0.9 + 0.5; w.pens.push({ x: 900 + Math.cos(a) * 900, z: Math.sin(a) * 900, r: pr }); }
  }
  if (game === 'charge') {
    const n = (opt && opt.rivals != null) ? opt.rivals : I;
    for (let i = 0; i < n; i++) { const a = 0.6 + i / Math.max(1, n) * TAU; w.rivals.push({ x: Math.cos(a) * 1500, y: 0, z: Math.sin(a) * 1500, vx: 0, vz: 0, petals: SH_RIVAL_PETALS, debt: 0, slow: 0, hitCd: 0, out: 0, tx: 0, tz: 0, re: 0 }); }
  }
  return w;
}
function addSheep(x, y, z) {
  if (SW.sheep.length >= SP.sheepCap) return null;
  const s = { x, y, z, vx: 0, vy: 0, vz: 0, fed: 0, eatCd: SW.rnd(), roll: SW.rnd(), ph: SW.rnd() * TAU, tgt: null, panic: 0 };
  SW.sheep.push(s); SW.stats.born++;
  return s;
}
function addShPrism(x, y, z, kind, shielded) {
  if (Math.hypot(x, z) > SH_R - 40) return null;
  const p = { x, y, z, kind, sh: !!shielded, alive: true, claim: 0 };
  SW.prisms.push(p); SW.phashDirty = true; return p;
}
const isDanger = (s) => s.roll < SEL.danger();
const edibleFor = (p) => p.alive && p.kind !== 'jade' && (!p.sh || L5(SP.elSpace));

// xz grid hash (y ignored in the key; distance checks are 3D)
const SH_CELL = 90;
function shKey(x, z) { return ((Math.floor(x / SH_CELL) + 2048) << 12) | (Math.floor(z / SH_CELL) + 2048); }
function shHash(list) { const m = new Map(); for (const o of list) { if (o.alive === false) continue; const k = shKey(o.x, o.z); let b = m.get(k); if (!b) m.set(k, b = []); b.push(o); } return m; }
function shQuery(m, x, z, r, fn) {
  const c0 = Math.floor((x - r) / SH_CELL), c1 = Math.floor((x + r) / SH_CELL), d0 = Math.floor((z - r) / SH_CELL), d1 = Math.floor((z + r) / SH_CELL);
  for (let i = c0; i <= c1; i++) for (let j = d0; j <= d1; j++) { const b = m.get(((i + 2048) << 12) | (j + 2048)); if (b) for (const o of b) fn(o); }
}
function flockStats() {
  const S = SW.sheep; let cx = 0, cy = 0, cz = 0;
  for (const s of S) { cx += s.x; cy += s.y; cz += s.z; }
  const n = Math.max(1, S.length); cx /= n; cy /= n; cz /= n;
  let r2 = 0, far = null, fd = -1;
  for (const s of S) { const d = (s.x - cx) ** 2 + (s.z - cz) ** 2; r2 += d; if (d > fd) { fd = d; far = s; } }
  return { x: cx, y: cy, z: cz, r: Math.sqrt(r2 / n), far, n: S.length };
}

// ================= (4) one step =================
// input: { pilot: { sx, sy, call, tele }, dogs: [{ down, x, z }, { down, x, z }] }  (held states; edges are found here)
function shepStep(dt, input) {
  const w = SW; if (w.done) return;
  w.t += dt;
  const inp = input || {};
  const pi = inp.pilot || {}, di = inp.dogs || [];
  stepPilot(dt, pi);
  const planeY = w.pilot.y;
  // ---- dogs: run to the thumb; a lift leaves a decoy where the dog stands ----
  for (let k = 0; k < 2; k++) {
    const d = w.dogs[k], t = di[k] || {};
    d.y = planeY;
    if (d.off) continue;
    const was = w.prev['d' + k];
    if (t.down) {
      const dx = t.x - d.x, dz = t.z - d.z, L = Math.hypot(dx, dz), step = SP.dogSpeed * dt;
      if (L > step) { d.x += dx / L * step; d.z += dz / L * step; } else { d.x = t.x; d.z = t.z; }
    } else if (was) { dropDecoy(d.x, d.z, 'dog'); }
    d.down = !!t.down; w.prev['d' + k] = d.down;
  }
  if (w.decoy) w.decoy.y = planeY;
  stepSheep(dt, planeY);
  stepRivalsSh(dt);
  // ---- Mass L5: the flock poops a cluster of its own mass ----
  if (L5(SP.elMass)) {
    w.pooT -= dt;
    if (w.pooT <= 0 && w.sheep.length) {
      w.pooT = SP.pooEvery;
      const s = w.sheep[Math.floor(w.rnd() * w.sheep.length)];
      for (let i = 0; i < 7; i++) { const a = w.rnd() * TAU, r = w.rnd() * 22; if (addShPrism(s.x + Math.cos(a) * r, s.y + (w.rnd() - 0.5) * 16, s.z + Math.sin(a) * r, 'jade')) w.stats.pooped++; }
      w.fx.push({ k: 'poo', x: s.x, y: s.y, z: s.z, t: 0 });
    }
  }
  // ---- pasture regrows slowly in the Mass game ----
  if (w.game === 'mass') { w.regrowT -= dt; if (w.regrowT <= 0) { w.regrowT = 0.4; const a = w.rnd() * TAU, r = 400 + w.rnd() * 1700; addShPrism(Math.cos(a) * r, (w.rnd() - 0.5) * 70, Math.sin(a) * r, 'pasture'); } }
  // ---- pens ----
  if (w.game === 'time' && w.penNext < w.pens.length) {
    const p = w.pens[w.penNext]; let inside = 0;
    for (const s of w.sheep) if ((s.x - p.x) ** 2 + (s.z - p.z) ** 2 < p.r * p.r) inside++;
    p.fill = w.sheep.length ? inside / w.sheep.length : 0;
    if (w.sheep.length && p.fill >= 0.6) { w.penNext++; w.fx.push({ k: 'pen', x: p.x, y: planeY, z: p.z, t: 0, r: p.r }); if (w.penNext >= w.pens.length) w.finishT = w.t; }
  }
  for (const f of w.fx) f.t += dt;
  w.fx = w.fx.filter((f) => f.t < 1.2);
  if (w.t >= SP.round || (w.game === 'time' && w.finishT != null)) w.done = true;
}

function dropDecoy(x, z, by) {
  SW.decoy = { x, y: SW.pilot.y, z, by, t: SW.t };
  SW.stats.decoys++;
  SW.fx.push({ k: 'decoy', x, y: SW.pilot.y, z, t: 0 });
}

function stepPilot(dt, pi) {
  const w = SW, p = w.pilot;
  if (p.off) { p.y = 0; return; }
  const sx = clamp(pi.sx || 0, -1, 1), sy = clamp(pi.sy || 0, -1, 1);
  // the Rhino's ramp: a slope, not a switch (RampBoostActionSO): full boost below the plateau, none at the grace band
  const dev = Math.max(Math.abs(sx), Math.abs(sy));
  const straight = 1 - clamp((dev - SP.rhinoPlateau) / Math.max(1e-4, SP.rhinoGrace - SP.rhinoPlateau), 0, 1);
  const target = SP.rhinoCruise * (1 + (SP.rhinoMaxBoost - 1) * straight);
  if (p.v < target) p.v = Math.min(target, p.v + SP.rhinoAccel * dt);
  else p.v = Math.max(target, p.v - (straight > 0 ? SP.rhinoBleed : SP.rhinoReturn) * dt);
  const turn = (p.v * SP.rhinoRts + SP.rhinoTurn) * Math.PI / 180;
  p.yaw += sx * turn * dt;
  p.pitch = clamp(p.pitch + sy * turn * dt, -1.2, 1.2);
  p.pitch *= 1 - Math.min(1, dt * 0.8 * (1 - Math.abs(sy)));          // lab: the nose settles level when the stick lets go
  const cp = Math.cos(p.pitch);
  p.x += Math.sin(p.yaw) * cp * p.v * dt; p.z += Math.cos(p.yaw) * cp * p.v * dt; p.y += Math.sin(p.pitch) * p.v * dt;
  p.y = clamp(p.y, -900, 900);
  // the cell wall turns the hull back in (the model's, not an input)
  const r = Math.hypot(p.x, p.z);
  if (r > SH_R) { const inward = Math.atan2(-p.x, -p.z); let e = inward - p.yaw; e = Math.atan2(Math.sin(e), Math.cos(e)); p.yaw += clamp(e, -turn * dt * 1.5, turn * dt * 1.5); }
  // call (edge, long cooldown)
  p.callT = Math.max(0, p.callT - dt); p.callCd = Math.max(0, p.callCd - dt); p.teleCd = Math.max(0, p.teleCd - dt); p.hitCd = Math.max(0, p.hitCd - dt);
  if (pi.call && !w.prev.call && p.callCd <= 0) { p.callT = SP.callTime; p.callCd = SP.callCooldown; w.stats.calls++; w.fx.push({ k: 'call', x: p.x, y: p.y, z: p.z, t: 0 }); }
  w.prev.call = !!pi.call;
  // teleport to the decoy (edge). Time L5 leaves a decoy where the pilot was.
  if (pi.tele && !w.prev.tele && w.decoy && p.teleCd <= 0) {
    const ox = p.x, oz = p.z, d = w.decoy;
    w.fx.push({ k: 'tele', x: ox, y: p.y, z: oz, t: 0, x2: d.x, z2: d.z });
    p.x = d.x; p.z = d.z; p.teleCd = 0.6; w.stats.teleports++;
    w.decoy = null;
    if (L5(SP.elTime)) dropDecoy(ox, oz, 'pilot');
  }
  w.prev.tele = !!pi.tele;
}

function repellers() {
  const w = SW, R = SEL.ring(), out = [];
  if (!w.pilot.off && w.pilot.callT <= 0) out.push({ x: w.pilot.x, z: w.pilot.z, r: R });
  for (const d of w.dogs) if (!d.off) out.push({ x: d.x, z: d.z, r: R });
  if (w.decoy) out.push({ x: w.decoy.x, z: w.decoy.z, r: R });
  return out;
}

function stepSheep(dt, planeY) {
  const w = SW, S = w.sheep;
  if (w.phashDirty) { w.phash = shHash(w.prisms); w.phashDirty = false; }
  const hash = shHash(S), reps = repellers(), vmax0 = SP.sheepSpeed * SEL.speed(), p = w.pilot;
  const calling = !p.off && p.callT > 0;
  const atk = L5(SP.elCharge) && w.rivals.length;
  const born = [];
  for (const s of S) {
    let sx = 0, sy = 0, sz = 0, ax = 0, az = 0, cx = 0, cy = 0, cz = 0, n = 0;
    shQuery(hash, s.x, s.z, 160, (o) => {
      if (o === s) return;
      const dx = s.x - o.x, dy = s.y - o.y, dz = s.z - o.z, d2 = dx * dx + dy * dy + dz * dz;
      if (d2 > 160 * 160) return;
      const d = Math.sqrt(d2) || 1;
      if (d < 58) { const k = (58 - d) / 58 / d; sx += dx * k; sy += dy * k; sz += dz * k; }
      ax += o.vx; az += o.vz; cx += o.x; cy += o.y; cz += o.z; n++;
    });
    let gx = sx * 1.6, gz = sz * 1.6;
    if (n) { gx += ((ax / n) / vmax0) * 0.5 + (cx / n - s.x) / 160 * 0.6; gz += ((az / n) / vmax0) * 0.5 + (cz / n - s.z) / 160 * 0.6; }
    // the herders: a cylinder of repulsion round each (horizontal distance, so the plane reads from above)
    let panic = 0;
    for (const r of reps) {
      const dx = s.x - r.x, dz = s.z - r.z, d = Math.hypot(dx, dz);
      if (d < r.r) { const k = 1 - d / r.r; gx += dx / (d || 1) * 3.2 * k; gz += dz / (d || 1) * 3.2 * k; panic = Math.max(panic, k); }
    }
    if (calling) { const dx = p.x - s.x, dz = p.z - s.z, d = Math.hypot(dx, dz) || 1; if (d > 30) { gx += dx / d * 2.6; gz += dz / d * 2.6; } panic = Math.max(panic, 0.5); }
    // Charge L5: a rival in range draws the flock in
    if (atk) { let best = null, bd = SP.attackRange; for (const r of w.rivals) if (r.out <= 0) { const d = Math.hypot(r.x - s.x, r.z - s.z); if (d < bd) { bd = d; best = r; } } if (best) { gx += (best.x - s.x) / bd * 2.2; gz += (best.z - s.z) / bd * 2.2; panic = Math.max(panic, 0.6); } }
    // graze: an unafraid sheep wanders to the nearest edible prism it can sense
    s.eatCd -= dt;
    if (panic < 0.5) {
      if (!s.tgt || !s.tgt.alive || !edibleFor(s.tgt)) {
        s.tgt = null; let bd = 120 * 120;
        if (s.eatCd <= 0) shQuery(w.phash, s.x, s.z, 120, (q) => { if (!edibleFor(q)) return; const d = (q.x - s.x) ** 2 + (q.z - s.z) ** 2; if (d < bd && Math.abs(q.y - s.y) < 90) { bd = d; s.tgt = q; } });
      }
      if (s.tgt) { const dx = s.tgt.x - s.x, dz = s.tgt.z - s.z, d = Math.hypot(dx, dz) || 1; gx += dx / d * 0.9; gz += dz / d * 0.9; }
    } else s.tgt = null;
    // wander (seeded per sheep, smooth)
    s.ph += dt * (0.6 + s.roll * 0.8);
    gx += Math.cos(s.ph * 1.7 + s.roll * 9) * 0.25; gz += Math.sin(s.ph * 1.3 + s.roll * 5) * 0.25;
    // the wall
    const rr = Math.hypot(s.x, s.z); if (rr > SH_R - 200) { gx -= s.x / rr * 2; gz -= s.z / rr * 2; }
    const gm = Math.hypot(gx, gz), vmax = vmax0 * (1 + 0.8 * panic);
    const dvx = gm > 0.02 ? gx / gm * vmax * Math.min(1, gm) : 0, dvz = gm > 0.02 ? gz / gm * vmax * Math.min(1, gm) : 0;
    const ag = Math.min(1, 3 * dt);
    s.vx += (dvx - s.vx) * ag; s.vz += (dvz - s.vz) * ag;
    // 3D: separation and a little vertical wander, all pulled hard toward the pilot's plane
    s.vy += ((planeY - s.y) * SP.planePull - s.vy * 1.8 + sy * 40 + Math.sin(s.ph * 2.1 + s.roll * 3) * 18) * dt;
    s.x += s.vx * dt; s.y += s.vy * dt; s.z += s.vz * dt;
    s.panic = panic;
    // eat on contact
    if (s.tgt && s.eatCd <= 0) {
      const q = s.tgt;
      if ((q.x - s.x) ** 2 + (q.y - s.y) ** 2 + (q.z - s.z) ** 2 < 22 * 22) {
        q.alive = false; w.phashDirty = true; s.tgt = null; s.eatCd = 0.9; s.fed++; w.stats.eaten++;
        if (q.kind === 'enemy') w.stats.enemyEaten++;
        if (s.fed >= SEL.meals()) { s.fed -= SEL.meals(); born.push(s); }
      }
    }
  }
  for (const s of born) { const l = addSheep(s.x + (w.rnd() - 0.5) * 20, s.y, s.z + (w.rnd() - 0.5) * 20); if (l) w.fx.push({ k: 'lamb', x: l.x, y: l.y, z: l.z, t: 0 }); }
  if (w.phashDirty) w.prisms = w.prisms.filter((q) => q.alive);
}

function stepRivalsSh(dt) {
  const w = SW; if (!w.rivals.length) return;
  const p = w.pilot, spd0 = 110 + 25 * SP.intensity;
  for (const r of w.rivals) {
    if (r.out > 0) { r.out -= dt; if (r.out <= 0) { r.petals = SH_RIVAL_PETALS; const a = w.rnd() * TAU; r.x = Math.cos(a) * 1800; r.z = Math.sin(a) * 1800; } continue; }
    r.slow = Math.max(0, r.slow - dt); r.hitCd = Math.max(0, r.hitCd - dt); r.eatCd = Math.max(0, (r.eatCd || 0) - dt); r.re -= dt;
    // hunt: the pilot when it is near, else the nearest plain sheep (wolves eat the flock)
    if (r.re <= 0) {
      r.re = 0.5;
      const dp = p.off ? 1e9 : Math.hypot(p.x - r.x, p.z - r.z);
      let best = null, bd = 1e9; for (const s of w.sheep) { const d = Math.hypot(s.x - r.x, s.z - r.z) + (isDanger(s) ? 400 : 0); if (d < bd) { bd = d; best = s; } }
      if (dp < 700 || !best) { r.tx = p.x; r.tz = p.z; } else { r.tx = best.x; r.tz = best.z; }
    }
    const spd = spd0 * (r.slow > 0 ? 0.35 : 1), dx = r.tx - r.x, dz = r.tz - r.z, d = Math.hypot(dx, dz) || 1;
    r.vx += (dx / d * spd - r.vx) * Math.min(1, 2 * dt); r.vz += (dz / d * spd - r.vz) * Math.min(1, 2 * dt);
    r.x += r.vx * dt; r.z += r.vz * dt; r.y += (p.y - r.y) * Math.min(1, dt);
    // ram the pilot
    if (!p.off && p.hitCd <= 0 && Math.hypot(p.x - r.x, p.z - r.z) < 26 && Math.abs(p.y - r.y) < 40) { p.hitCd = 1.5; if (p.petals > 0) { p.petals--; r.petals++; w.stats.petalsLost++; } w.fx.push({ k: 'hit', x: p.x, y: p.y, z: p.z, t: 0 }); }
    // touch sheep: danger sheep sting (a petal and a slow) and are spent; plain sheep get eaten (Charge L5: they nip first)
    for (let i = w.sheep.length - 1; i >= 0 && r.eatCd <= 0; i--) {
      const s = w.sheep[i];
      if (Math.abs(s.x - r.x) > 22 || Math.abs(s.z - r.z) > 22 || Math.abs(s.y - r.y) > 40) continue;
      if (isDanger(s)) { r.debt += 1; r.slow = 1.5; w.stats.dangerHits++; }
      else if (L5(SP.elCharge)) r.debt += 0.34;
      r.eatCd = 1.2; w.sheep.splice(i, 1); w.stats.lost++; w.fx.push({ k: isDanger(s) ? 'sting' : 'eaten', x: s.x, y: s.y, z: s.z, t: 0 });
    }
    if (r.debt >= 1) { const n = Math.floor(r.debt); r.debt -= n; const take = Math.min(n, r.petals); r.petals -= take; p.petals += take; w.stats.petalsTaken += take; if (r.petals <= 0) { r.out = 5; w.stats.ko++; w.fx.push({ k: 'ko', x: r.x, y: r.y, z: r.z, t: 0 }); } }
  }
}

function shepScore() {
  const w = SW, s = w.stats;
  if (w.game === 'time') return w.penNext * 10 + (w.finishT != null ? Math.max(0, Math.round(SP.round - w.finishT)) : 0);
  if (w.game === 'space') return s.enemyEaten;
  if (w.game === 'mass') return s.born + w.prisms.filter((q) => q.kind === 'jade').length;
  return s.petalsTaken - s.petalsLost + 3 * s.ko;
}

// ================= (5) the AI crews: inputs only =================
function shTarget() {
  const w = SW, F = flockStats();
  if (w.game === 'time') { const p = w.pens[Math.min(w.penNext, w.pens.length - 1)]; return { x: p.x, z: p.z }; }
  // the nearest edible prism cluster to the flock (the fort, a patch, or a rival in Charge)
  if (w.game === 'charge') { let best = null, bd = 1e9; for (const r of w.rivals) if (r.out <= 0) { const d = Math.hypot(r.x - F.x, r.z - F.z); if (d < bd) { bd = d; best = r; } } if (best) return { x: best.x, z: best.z }; }
  if (w.game === 'space' && w.fort) {
    let any = false; for (const q of w.prisms) if (q.kind === 'enemy' && edibleFor(q)) { any = true; break; }
    if (any) return { x: w.fort.x, z: w.fort.z };
  }
  let best = null, bd = 1e9;
  for (const q of w.prisms) { if (!edibleFor(q)) continue; const d = Math.hypot(q.x - F.x, q.z - F.z); if (d > 150 && d < bd) { bd = d; best = q; } }
  return best ? { x: best.x, z: best.z } : { x: F.x, z: F.z };
}
// The point behind the flock (seen from the target) — Strombom's driving position.
function drivePoint(F, T, back) { let ux = T.x - F.x, uz = T.z - F.z; const L = Math.hypot(ux, uz) || 1; ux /= L; uz /= L; return { x: F.x - ux * back, z: F.z - uz * back, ux, uz }; }

function shAI(role) {
  const w = SW, F = flockStats(), T = shTarget(), R = SEL.ring(), nz = SP.aiNoise, out = { pilot: { sx: 0, sy: 0, call: false, tele: false }, dogs: [{ down: true, x: w.dogs[0].x, z: w.dogs[0].z }, { down: true, x: w.dogs[1].x, z: w.dogs[1].z }] };
  const adt = Math.max(0, w.t - (w.ai.lastT || 0)); w.ai.lastT = w.t;
  w.ai.noiseT -= adt;
  if (w.ai.noiseT <= 0) { w.ai.noiseT = 0.5; w.ai.nx = (w.rnd() - 0.5) * 2 * nz * 260; w.ai.nz = (w.rnd() - 0.5) * 2 * nz * 260; w.ai.pnx = (w.rnd() - 0.5) * nz; w.ai.pnz = (w.rnd() - 0.5) * nz; }
  if (role === 'dogs' || role === 'all') {
    // dog 0 collects the straggler; dog 1 drives from behind, on a flank
    const drive = drivePoint(F, T, F.r + R * 0.55);
    const far = F.far, spread = F.r;
    let a;
    if (far && spread > 150) { const ox = far.x - F.x, oz = far.z - F.z, L = Math.hypot(ox, oz) || 1; a = { x: far.x + ox / L * R * 0.55, z: far.z + oz / L * R * 0.55 }; }
    else a = { x: drive.x - drive.uz * R * 0.7, z: drive.z + drive.ux * R * 0.7 };
    const b = { x: drive.x + drive.uz * R * 0.45, z: drive.z - drive.ux * R * 0.45 };
    const route = (d, pt) => {   // go round the flock, not through it
      const vx = pt.x - d.x, vz = pt.z - d.z, L = Math.hypot(vx, vz) || 1, tx = F.x - d.x, tz = F.z - d.z, along = clamp((tx * vx + tz * vz) / (L * L), 0, 1);
      const qx = d.x + vx * along - F.x, qz = d.z + vz * along - F.z, q = Math.hypot(qx, qz);
      if (along > 0 && along < 1 && q < F.r + R * 0.6) { const s = q > 1 ? 1 / q : 1; return { x: F.x + qx * s * (F.r + R * 0.9), z: F.z + qz * s * (F.r + R * 0.9) }; }
      return pt;
    };
    const pa = route(w.dogs[0], a), pb = route(w.dogs[1], b);
    out.dogs[0] = { down: true, x: pa.x + w.ai.nx, z: pa.z + w.ai.nz };
    out.dogs[1] = { down: true, x: pb.x - w.ai.nz, z: pb.z + w.ai.nx };
    // every few seconds the driving dog lifts for a moment: a decoy behind the flock holds that side
    w.ai.liftT -= adt;
    if (w.ai.liftT <= 0) { w.ai.lift = 0.15; w.ai.liftT = 7 + nz * 4; }
    if (w.ai.lift > 0) { w.ai.lift -= adt; out.dogs[1].down = false; }
  }
  if (role === 'pilot' || role === 'all') {
    const p = w.pilot;
    // the pilot holds the flank opposite the driving dog, so three herders close a U round the flock
    const dp = drivePoint(F, T, F.r + R * 0.35);
    let goal = { x: dp.x - dp.uz * (F.r + R * 0.6), z: dp.z + dp.ux * (F.r + R * 0.6) };
    // Charge: keep away from the wolves first
    for (const r of w.rivals) if (r.out <= 0 && Math.hypot(r.x - p.x, r.z - p.z) < 260) { goal = { x: p.x + (p.x - r.x) * 3, z: p.z + (p.z - r.z) * 3 }; }
    const dx = goal.x - p.x, dz = goal.z - p.z, dist = Math.hypot(dx, dz);
    let want = Math.atan2(dx, dz);
    if (dist < 160) want = p.yaw + 0.9;            // there: circle on station
    let e = want - p.yaw; e = Math.atan2(Math.sin(e), Math.cos(e));
    out.pilot.sx = clamp(e * 1.6 + w.ai.pnx, -1, 1);
    out.pilot.sy = clamp(-p.y * 0.004 - p.pitch * 1.5 + w.ai.pnz, -1, 1);
    // call when the flock has scattered; teleport when the decoy sits much closer to where the pilot wants to be
    out.pilot.call = F.r > 200 && p.callCd <= 0 && Math.hypot(F.x - p.x, F.z - p.z) < 900;
    if (w.decoy && Math.hypot(w.decoy.x - goal.x, w.decoy.z - goal.z) < dist - 500) out.pilot.tele = !w.prev.tele;
  }
  return out;
}

// ================= (6) the headless scorecard =================
const SHEP_ROWS = [
  { id: 'pilotOnly', name: 'Pilot only (no dogs)', opt: { noDogs: true } },
  { id: 'dogsOnly', name: 'Dogs only (no pilot)', opt: { noPilot: true } },
  { id: 'crew', name: 'Shepherd + 2 dogs (yours)', opt: {} },
  { id: 'smallthumb', name: 'Small thumb (14 mm floor)', set: { thumbMm: 14 } },
  { id: 'rookie', name: 'Rookie crew (noise 0.6)', set: { aiNoise: 0.6 } },
  { id: 'time5', name: 'Time 5 (swap decoy)', set: { elTime: 5 } },
  { id: 'space5', name: 'Space 5 (eat shields)', set: { elSpace: 5 } },
  { id: 'mass5', name: 'Mass 5 (poop mass)', set: { elMass: 5 } },
  { id: 'charge5', name: 'Charge 5 (attack)', set: { elCharge: 5 } },
  { id: 'charge15', name: 'Charge 15 (all danger)', set: { elCharge: 15 } },
];
function shepRun(game, seed, opt, secs) {
  const keep = SW, round = SP.round; SP.round = secs || 30;
  shepWorld(seed, game, opt);
  const dt = 1 / 20;
  while (!SW.done) shepStep(dt, shAI('all'));
  const sc = shepScore(), st = Object.assign({ sheep: SW.sheep.length }, SW.stats);
  SP.round = round; SW = keep;
  return { score: sc, stats: st };
}
function shepBatch(opts) {
  const o = opts || {}, seeds = o.seeds || [1], secs = o.secs || 30, rows = o.rows || SHEP_ROWS, res = [];
  for (const row of rows) {
    const saved = {}; for (const k in (row.set || {})) { saved[k] = SP[k]; SP[k] = row.set[k]; }
    const r = { id: row.id, name: row.name };
    for (const g of SHEP_GAMES) { let s = 0; for (const sd of seeds) s += shepRun(g.id, sd, row.opt, secs).score; r[g.id] = Math.round(s / seeds.length * 10) / 10; }
    for (const k in saved) SP[k] = saved[k];
    res.push(r);
  }
  return res;
}

// ================= (7) the page: overlay, camera, drawing, touch =================
let shepOn = false;
const shepState = { game: lsGet('lab:' + LAB.name + ':shepGame', 'space'), role: lsGet('lab:' + LAB.name + ':shepRole', PLATFORM.device === 'phone' ? 'dogs' : 'ai'), seed: 1, paused: false, manual: false, sheet: false, showThumb: true, batch: null };
const shepRoot = el('div'); shepRoot.id = 'shep'; shepRoot.hidden = true;
shepRoot.innerHTML =
  '<canvas id="shepCanvas" aria-label="Shepherd: the flock, the pilot and two sheepdogs on the pilot\'s plane"></canvas>' +
  '<div id="shepTop">' +
  '<button class="sbtn" id="shepBack" title="Back to the lab">&#8592; Lab</button>' +
  '<b class="stitle">Shepherd</b>' +
  '<select id="shepGame" aria-label="Game"></select>' +
  '<select id="shepRole" aria-label="Who you play"></select>' +
  '<button class="sbtn" id="shepRestart">Restart</button>' +
  '<button class="sbtn" id="shepPause">Pause</button>' +
  '<button class="sbtn" id="shepMore" aria-expanded="false">Settings</button>' +
  '</div>' +
  '<div id="shepHud"></div>' +
  '<div id="shepToast"></div>' +
  '<div id="shepPads"><button class="spad" id="shepCall">CALL</button></div>' +
  '<div id="shepEnd" hidden></div>' +
  '<aside id="shepSheet" hidden></aside>';
document.body.appendChild(shepRoot);
const shepCss = document.createElement('style');
shepCss.textContent = `
  #shep { position: fixed; inset: 0; z-index: 50; background: var(--stage); color: #e6efec; touch-action: none; user-select: none; -webkit-user-select: none; overflow: hidden; }
  #shepCanvas { position: absolute; inset: 0; width: 100%; height: 100%; display: block; }
  #shepTop { position: absolute; left: 0; right: 0; top: 0; display: flex; gap: 6px; align-items: center; flex-wrap: wrap; pointer-events: none;
             padding: calc(6px + env(safe-area-inset-top, 0px)) max(12px, env(safe-area-inset-right, 0px)) 6px max(12px, env(safe-area-inset-left, 0px)); }
  #shepTop > * { pointer-events: auto; }
  #shepTop .stitle { font: 700 15px var(--f-display); letter-spacing: .03em; margin: 0 4px; }
  #shepTop select { width: auto; max-width: 46vw; background: rgba(10,20,22,.85); color: #e6efec; border: 1px solid #34514d; border-radius: 6px; padding: 4px 6px; font: 12px var(--f-ui); }
  .sbtn { background: rgba(10,20,22,.85); color: #e6efec; border: 1px solid #34514d; border-radius: 6px; padding: 4px 10px; cursor: pointer; font: 600 12px var(--f-display); min-height: 30px; }
  .sbtn:hover, .sbtn:focus-visible { border-color: #f0a043; }
  #shepHud { position: absolute; left: max(12px, env(safe-area-inset-left, 0px)); top: calc(46px + env(safe-area-inset-top, 0px)); font: 12px/1.45 var(--f-mono); color: #d6e4e0; pointer-events: none; white-space: pre; text-shadow: 0 1px 2px #000; }
  #shepHud .big { font: 700 18px var(--f-display); color: #fff; }
  #shepToast { position: absolute; left: 50%; bottom: calc(18px + env(safe-area-inset-bottom, 0px)); transform: translateX(-50%); background: rgba(8,16,18,.88); color: #ffe2b8; border: 1px solid #f0a043; border-radius: 6px; padding: 5px 12px; font: 12px var(--f-mono); pointer-events: none; opacity: 0; transition: opacity .25s; max-width: calc(100% - 32px); text-align: center; }
  #shepToast.show { opacity: 1; }
  #shepPads { position: absolute; right: max(18px, env(safe-area-inset-right, 0px)); bottom: calc(22px + env(safe-area-inset-bottom, 0px)); pointer-events: none; }
  .spad { pointer-events: auto; width: 84px; height: 84px; border-radius: 50%; border: 2px solid #f0a043; background: rgba(240,160,67,.16); color: #ffd9a6; font: 700 14px var(--f-display); letter-spacing: .06em; cursor: pointer; }
  .spad[disabled] { opacity: .45; }
  #shepEnd { position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%); background: rgba(10,18,20,.95); border: 1px solid #f0a043; border-radius: 10px; padding: 14px 16px; width: min(420px, calc(100% - 32px)); font: 13px/1.5 var(--f-ui); }
  #shepEnd h3 { font: 700 16px var(--f-display); margin: 0 0 6px; }
  #shepEnd .row2 { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 10px; }
  #shepSheet { position: absolute; right: 0; top: 0; bottom: 0; width: min(380px, 100%); background: var(--panel); color: var(--ink); overflow: auto; border-left: 1px solid var(--line);
               padding: calc(10px + env(safe-area-inset-top, 0px)) max(16px, env(safe-area-inset-right, 0px)) calc(16px + env(safe-area-inset-bottom, 0px)) 16px; user-select: text; -webkit-user-select: text; touch-action: pan-y; }
  #shepSheet .head { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
  #shepSheet h2 { font: 700 15px var(--f-display); margin: 0; }
  #shepSheet .el5 { font: 10px var(--f-mono); padding: 1px 6px; border-radius: 99px; background: color-mix(in srgb, var(--accent) 22%, transparent); margin-left: 6px; }
  #shepSheet textarea { min-height: 54px; }
  #shepSheet td.best { color: var(--ok); font-weight: 700; }
  @media (prefers-reduced-motion: reduce) { #shepToast { transition: none; } }
`;
document.head.appendChild(shepCss);
const sc = $('shepCanvas'), sg = sc.getContext('2d');
for (const g of SHEP_GAMES) { const o = el('option', '', g.name); o.value = g.id; $('shepGame').appendChild(o); }
for (const r of SHEP_ROLES) { const o = el('option', '', r.name); o.value = r.id; $('shepRole').appendChild(o); }
if (!SHEP_GAMES.some((g) => g.id === shepState.game)) shepState.game = 'space';
if (!SHEP_ROLES.some((r) => r.id === shepState.role)) shepState.role = 'ai';
$('shepGame').value = shepState.game; $('shepRole').value = shepState.role;

// ---- camera: tilted, looking down onto the pilot's plane, following the flock ----
const cam = { tx: 0, tz: 0, pitch: 0.95, dist: 2600, f: 1, cx: 0, cy: 0, short: 390, k: 1 };
function shepResize() { const r = sc.getBoundingClientRect(), dpr = Math.min(2, devicePixelRatio || 1); const w = Math.max(1, Math.round(r.width * dpr)), h = Math.max(1, Math.round(r.height * dpr)); if (sc.width !== w || sc.height !== h) { sc.width = w; sc.height = h; } cam.k = sc.width / Math.max(1, r.width); }
function camUpdate(dt) {
  const F = flockStats(), p = SW.pilot;
  const w = shepState.role === 'pilot' && !p.off ? 0.45 : 0.15;
  const gx = F.n ? F.x * (1 - w) + p.x * w : p.x, gz = F.n ? F.z * (1 - w) + p.z * w : p.z;
  const a = dt > 0 ? Math.min(1, dt * 1.6) : 1;
  cam.tx += (gx - cam.tx) * a; cam.tz += (gz - cam.tz) * a;
  cam.cx = sc.width / 2; cam.cy = sc.height * 0.54; cam.short = Math.min(sc.width, sc.height);
  cam.f = cam.short * cam.dist / SP.viewSpan;
}
function camBasis() {
  const cp = Math.cos(cam.pitch), sp = Math.sin(cam.pitch), py = SW.pilot.y;
  const pos = { x: cam.tx, y: py + cam.dist * sp, z: cam.tz - cam.dist * cp };
  return { pos, f: { x: 0, y: -sp, z: cp }, u: { x: 0, y: cp, z: sp }, r: { x: 1, y: 0, z: 0 } };
}
let CB = null;
function proj(x, y, z) {
  const b = CB, dx = x - b.pos.x, dy = y - b.pos.y, dz = z - b.pos.z;
  const depth = dx * b.f.x + dy * b.f.y + dz * b.f.z; if (depth < 50) return null;
  const rx = dx * b.r.x + dy * b.r.y + dz * b.r.z, uy = dx * b.u.x + dy * b.u.y + dz * b.u.z;
  return { x: cam.cx - rx * cam.f / depth, y: cam.cy - uy * cam.f / depth, s: cam.f / depth, d: depth };
}
// screen point -> the pilot's plane (where the dogs live). Mirrored x: the camera looks along +z.
function toPlane(clientX, clientY) {
  const r = sc.getBoundingClientRect(), px = (clientX - r.left) * cam.k, py = (clientY - r.top) * cam.k, b = CB || camBasis();
  const a = -(px - cam.cx) / cam.f, c = -(py - cam.cy) / cam.f;
  const dir = { x: b.f.x + a * b.r.x + c * b.u.x, y: b.f.y + a * b.r.y + c * b.u.y, z: b.f.z + a * b.r.z + c * b.u.z };
  if (dir.y > -1e-3) return null;
  const t = (SW.pilot.y - b.pos.y) / dir.y;
  return { x: b.pos.x + dir.x * t, z: b.pos.z + dir.z * t };
}

// ---- input: thumbs, mouse, keys, gamepad -> ONE input object ----
const shIn = { pilot: { sx: 0, sy: 0, call: false, tele: false }, dogs: [{ down: false, x: 0, z: 0 }, { down: false, x: 0, z: 0 }] };
const touches = new Map();      // pointerId -> { kind: 'dog'|'stick', dog, x0, y0, x, y }
const keys = new Set();
function humanDogs() { return shepState.role === 'dogs' || shepState.role === 'both'; }
function humanPilot() { return shepState.role === 'pilot' || shepState.role === 'both'; }
sc.addEventListener('pointerdown', (ev) => {
  if (!SW) return;
  if (PLATFORM.device === 'phone' && document.fullscreenElement == null && shepRoot.requestFullscreen) shepRoot.requestFullscreen().catch(() => {});
  try { sc.setPointerCapture(ev.pointerId); } catch (e) { /* older engines */ }
  const r = sc.getBoundingClientRect();
  const stickSide = humanPilot() && !humanDogs() && (ev.clientX - r.left) < r.width * 0.5;
  if (stickSide || (shepState.role === 'pilot' && ev.pointerType !== 'mouse')) { touches.set(ev.pointerId, { kind: 'stick', x0: ev.clientX, y0: ev.clientY, x: ev.clientX, y: ev.clientY }); return; }
  if (!humanDogs()) return;
  // claim the nearest dog that no thumb holds (by screen distance)
  let best = -1, bd = 1e9;
  for (let k = 0; k < 2; k++) {
    if ([...touches.values()].some((t) => t.kind === 'dog' && t.dog === k)) continue;
    const d = SW.dogs[k], q = proj(d.x, d.y, d.z); if (!q) { if (best < 0) best = k; continue; }
    const dd = Math.hypot(q.x / cam.k + r.left - ev.clientX, q.y / cam.k + r.top - ev.clientY); if (dd < bd) { bd = dd; best = k; }
  }
  if (best < 0) return;
  touches.set(ev.pointerId, { kind: 'dog', dog: best, x: ev.clientX, y: ev.clientY });
  aimDog(best, ev.clientX, ev.clientY);
});
sc.addEventListener('pointermove', (ev) => {
  const t = touches.get(ev.pointerId); if (!t) return;
  t.x = ev.clientX; t.y = ev.clientY;
  if (t.kind === 'dog') aimDog(t.dog, ev.clientX, ev.clientY);
});
function aimDog(k, x, y) { const p = toPlane(x, y); if (p) { shIn.dogs[k].down = true; shIn.dogs[k].x = p.x; shIn.dogs[k].z = p.z; } }
function endTouch(ev) {
  const t = touches.get(ev.pointerId); if (!t) return;
  touches.delete(ev.pointerId);
  if (t.kind === 'dog') shIn.dogs[t.dog].down = false;          // the lift: the step drops the decoy
  if (t.kind === 'stick') { shIn.pilot.tele = true; teleLatch = 2; }   // lifting the stick thumb teleports
}
let teleLatch = 0;
sc.addEventListener('pointerup', endTouch); sc.addEventListener('pointercancel', endTouch);
$('shepCall').addEventListener('pointerdown', (ev) => { ev.preventDefault(); callLatch = 3; });
let callLatch = 0;
addEventListener('keydown', (ev) => {
  if (!shepOn) return;
  if (ev.target && /INPUT|TEXTAREA|SELECT/.test(ev.target.tagName)) return;
  const k = ev.key.toLowerCase(); keys.add(k);
  if (k === ' ' || k.startsWith('arrow')) ev.preventDefault();
  if (k === 'escape') { if (shepState.sheet) toggleSheet(false); else shepClose(); }
  if (k === 'p') shepPause();
  if (k === 'r') shepReset();
});
addEventListener('keyup', (ev) => keys.delete(ev.key.toLowerCase()));
addEventListener('blur', () => keys.clear());
function readPad() {
  try { const pads = navigator.getGamepads ? navigator.getGamepads() : []; for (const g of pads) if (g && g.connected) return g; } catch (e) { /* the viewer may block the Gamepad API */ }
  return null;
}
function gatherHuman() {
  // pilot: keys + gamepad + the touch stick write the same three numbers
  let sx = 0, sy = 0, call = false, tele = false;
  if (keys.has('a') || keys.has('arrowleft')) sx -= 1; if (keys.has('d') || keys.has('arrowright')) sx += 1;
  if (keys.has('w') || keys.has('arrowup')) sy -= 1; if (keys.has('s') || keys.has('arrowdown')) sy += 1;
  if (keys.has('e')) call = true; if (keys.has(' ')) tele = true;
  const g = readPad();
  if (g) {
    const ax = g.axes[0] || 0, ay = g.axes[1] || 0;
    if (Math.abs(ax) > 0.12) sx = ax; if (Math.abs(ay) > 0.12) sy = ay;     // stick down = nose up (flight convention)
    if (g.buttons[0] && g.buttons[0].pressed) call = true; if (g.buttons[4] && g.buttons[4].pressed) call = true;
    if (g.buttons[7] && g.buttons[7].value > 0.6) tele = true;
  }
  for (const t of touches.values()) if (t.kind === 'stick') { sx = clamp((t.x - t.x0) / 60, -1, 1); sy = clamp((t.y - t.y0) / 60, -1, 1); }
  if (callLatch > 0) { call = true; callLatch--; }
  if (teleLatch > 0) { tele = true; teleLatch--; }
  shIn.pilot.sx = sx; shIn.pilot.sy = sy; shIn.pilot.call = call; shIn.pilot.tele = tele;
}
function liveInputFor() {
  const role = shepState.role, ai = shAI(role === 'ai' ? 'all' : role === 'dogs' ? 'pilot' : role === 'pilot' ? 'dogs' : 'none');
  gatherHuman();
  return { pilot: humanPilot() ? shIn.pilot : ai.pilot, dogs: humanDogs() ? shIn.dogs : ai.dogs };
}

// ---- drawing ----
const SHC = { wool: '#e9efe8', woolDim: '#9fb0ab', danger: '#ff5a3c', dog: '#f2c84b', pilot: '#1fb8b0', decoy: '#f0a043', ruby: '#c2368e', pasture: '#7fa86a', jade: '#2fd6c8', grid: 'rgba(120,170,160,.10)', ring: 'rgba(242,200,75,.55)' };
function planeRing(x, z, r, col, lw, dash) {
  const y = SW.pilot.y; sg.beginPath(); let first = true;
  for (let i = 0; i <= 40; i++) { const a = i / 40 * TAU, q = proj(x + Math.cos(a) * r, y, z + Math.sin(a) * r); if (!q) { first = true; continue; } if (first) sg.moveTo(q.x, q.y); else sg.lineTo(q.x, q.y); first = false; }
  sg.strokeStyle = col; sg.lineWidth = lw * cam.k; sg.setLineDash(dash ? dash.map((v) => v * cam.k) : []); sg.stroke(); sg.setLineDash([]);
}
function shepRender() {
  const w = SW; if (!w) return;
  shepResize(); CB = camBasis();
  sg.fillStyle = '#061014'; sg.fillRect(0, 0, sc.width, sc.height);
  const py = w.pilot.y;
  // the plane: a faint grid round the camera target
  sg.lineWidth = 1 * cam.k; sg.strokeStyle = SHC.grid;
  const g0x = Math.floor((cam.tx - 2400) / 200) * 200, g0z = Math.floor((cam.tz - 1200) / 200) * 200;
  for (let x = g0x; x <= cam.tx + 2400; x += 200) { const a = proj(x, py, g0z), b = proj(x, py, cam.tz + 2600); if (a && b) { sg.beginPath(); sg.moveTo(a.x, a.y); sg.lineTo(b.x, b.y); sg.stroke(); } }
  for (let z = g0z; z <= cam.tz + 2600; z += 200) { const a = proj(g0x, py, z), b = proj(cam.tx + 2400, py, z); if (a && b) { sg.beginPath(); sg.moveTo(a.x, a.y); sg.lineTo(b.x, b.y); sg.stroke(); } }
  planeRing(0, 0, SH_R, 'rgba(194,54,142,.35)', 2, [10, 8]);
  // pens
  w.pens.forEach((p, i) => { const cur = i === w.penNext, done = i < w.penNext; planeRing(p.x, p.z, p.r, done ? 'rgba(47,214,200,.35)' : cur ? 'rgba(159,247,198,.95)' : 'rgba(159,247,198,.3)', cur ? 3 : 1.5, done ? null : [6, 6]); const q = proj(p.x, py, p.z); if (q) { sg.fillStyle = cur ? '#9ff7c6' : 'rgba(159,247,198,.5)'; sg.font = '600 ' + 13 * cam.k + 'px "Chakra Petch", system-ui'; sg.textAlign = 'center'; sg.fillText(done ? '✓' : (i + 1) + (cur && p.fill ? ' · ' + Math.round(p.fill * 100) + '%' : ''), q.x, q.y + 4 * cam.k); } });
  // everything with depth, far to near
  const items = [];
  for (const q of w.prisms) items.push({ o: q, t: 'p' });
  for (const s of w.sheep) items.push({ o: s, t: 's' });
  for (const r of w.rivals) if (r.out <= 0) items.push({ o: r, t: 'r' });
  for (const P0 of items) { const q = proj(P0.o.x, P0.o.y, P0.o.z); P0.q = q; }
  items.sort((a, b) => (b.q ? b.q.d : 0) - (a.q ? a.q.d : 0));
  const R = SEL.ring();
  for (const it of items) {
    const q = it.q; if (!q) continue;
    if (q.x < -50 || q.y < -50 || q.x > sc.width + 50 || q.y > sc.height + 50) continue;
    const o = it.o;
    if (it.t === 'p') {
      const s = Math.max(1.5 * cam.k, 9 * q.s);
      sg.fillStyle = o.kind === 'pasture' ? SHC.pasture : o.kind === 'jade' ? SHC.jade : SHC.ruby;
      sg.fillRect(q.x - s / 2, q.y - s / 2, s, s);
      if (o.sh) { sg.strokeStyle = '#f4f1ff'; sg.lineWidth = 1.2 * cam.k; sg.strokeRect(q.x - s / 2 - 1.5 * cam.k, q.y - s / 2 - 1.5 * cam.k, s + 3 * cam.k, s + 3 * cam.k); }
    } else if (it.t === 's') {
      const r = Math.max(2.5 * cam.k, 13 * q.s), sh = proj(o.x, py, o.z);
      if (sh && Math.abs(o.y - py) > 12) { sg.strokeStyle = 'rgba(200,220,215,.18)'; sg.lineWidth = 1 * cam.k; sg.beginPath(); sg.moveTo(sh.x, sh.y); sg.lineTo(q.x, q.y); sg.stroke(); sg.fillStyle = 'rgba(0,0,0,.35)'; sg.beginPath(); sg.ellipse(sh.x, sh.y, r * 0.8, r * 0.35, 0, 0, TAU); sg.fill(); }
      sg.fillStyle = o.panic > 0.3 ? SHC.woolDim : SHC.wool; sg.beginPath(); sg.arc(q.x, q.y, r, 0, TAU); sg.fill();
      if (isDanger(o)) { sg.strokeStyle = SHC.danger; sg.lineWidth = Math.max(1.2 * cam.k, r * 0.38); sg.stroke(); }
      const hd = Math.hypot(o.vx, o.vz) || 1, hq = proj(o.x + o.vx / hd * 10, o.y, o.z + o.vz / hd * 10);
      if (hq) { sg.fillStyle = '#2b3433'; sg.beginPath(); sg.arc(q.x + (hq.x - q.x) * 0.9, q.y + (hq.y - q.y) * 0.9, r * 0.45, 0, TAU); sg.fill(); }
    } else {
      const r = Math.max(5 * cam.k, 20 * q.s), vm = Math.hypot(o.vx, o.vz) || 1, hq = proj(o.x + o.vx / vm * 40, o.y, o.z + o.vz / vm * 40), a = hq ? Math.atan2(hq.y - q.y, hq.x - q.x) : 0;
      sg.save(); sg.translate(q.x, q.y); sg.rotate(a); sg.fillStyle = o.slow > 0 ? '#7a2a58' : SHC.ruby; sg.beginPath(); sg.moveTo(r, 0); sg.lineTo(-r * 0.7, r * 0.6); sg.lineTo(-r * 0.4, 0); sg.lineTo(-r * 0.7, -r * 0.6); sg.closePath(); sg.fill(); sg.restore();
      sg.fillStyle = '#ffd0ec'; sg.font = 11 * cam.k + 'px "IBM Plex Mono", monospace'; sg.textAlign = 'center'; sg.fillText(o.petals + '✿', q.x, q.y - r - 4 * cam.k);
    }
  }
  // the decoy
  if (w.decoy) { const d = w.decoy, q = proj(d.x, py, d.z); planeRing(d.x, d.z, R, 'rgba(240,160,67,.55)', 1.5, [4, 6]); if (q) { const s = 11 * cam.k; sg.strokeStyle = SHC.decoy; sg.lineWidth = 2.5 * cam.k; sg.beginPath(); sg.moveTo(q.x, q.y - s); sg.lineTo(q.x + s, q.y); sg.lineTo(q.x, q.y + s); sg.lineTo(q.x - s, q.y); sg.closePath(); sg.stroke(); } }
  // dogs: the ring is the floor-respecting repulsion radius; the thumb disc is what a big thumb hides
  for (const d of w.dogs) {
    if (d.off) continue;
    planeRing(d.x, d.z, R, d.down ? SHC.ring : 'rgba(242,200,75,.3)', d.down ? 2.5 : 1.5, d.down ? null : [3, 5]);
    const q = proj(d.x, py, d.z); if (!q) continue;
    if (shepState.showThumb && humanDogs() && d.down) { const tr = (SP.thumbMm / 2) * 6.3 * cam.k; sg.fillStyle = 'rgba(255,255,255,.07)'; sg.beginPath(); sg.arc(q.x, q.y, tr, 0, TAU); sg.fill(); }
    sg.fillStyle = SHC.dog; sg.beginPath(); sg.arc(q.x, q.y, 9 * cam.k, 0, TAU); sg.fill();
    sg.fillStyle = '#1b1206'; sg.font = '700 ' + 11 * cam.k + 'px "Chakra Petch", system-ui'; sg.textAlign = 'center'; sg.fillText(d.id ? 'B' : 'A', q.x, q.y + 4 * cam.k);
    const t = humanDogs() ? shIn.dogs[d.id] : null;
    if (t && t.down) { const tq = proj(t.x, py, t.z); if (tq) { sg.strokeStyle = 'rgba(242,200,75,.6)'; sg.lineWidth = 1 * cam.k; sg.beginPath(); sg.moveTo(tq.x - 6 * cam.k, tq.y); sg.lineTo(tq.x + 6 * cam.k, tq.y); sg.moveTo(tq.x, tq.y - 6 * cam.k); sg.lineTo(tq.x, tq.y + 6 * cam.k); sg.stroke(); } }
  }
  // the pilot (the Rhino), its ring, its call
  const p = w.pilot;
  if (!p.off) {
    if (p.callT > 0) planeRing(p.x, p.z, R * (0.6 + 0.4 * ((w.t * 2) % 1)), 'rgba(159,247,198,.9)', 3, null);
    else planeRing(p.x, p.z, R, 'rgba(47,214,200,.5)', 2, [8, 6]);
    const q = proj(p.x, p.y, p.z), h = proj(p.x + Math.sin(p.yaw) * 40, p.y, p.z + Math.cos(p.yaw) * 40);
    if (q && h) {
      const a = Math.atan2(h.y - q.y, h.x - q.x), r = 15 * cam.k;
      sg.save(); sg.translate(q.x, q.y); sg.rotate(a); sg.fillStyle = SHC.pilot; sg.beginPath(); sg.moveTo(r * 1.3, 0); sg.lineTo(-r, r * 0.75); sg.lineTo(-r * 0.5, 0); sg.lineTo(-r, -r * 0.75); sg.closePath(); sg.fill();
      if (p.v > SP.rhinoCruise * 2) { sg.strokeStyle = 'rgba(47,214,200,.5)'; sg.lineWidth = 2 * cam.k; sg.beginPath(); sg.moveTo(-r, 0); sg.lineTo(-r - Math.min(80, p.v / 10) * cam.k, 0); sg.stroke(); }
      sg.restore();
    }
    if (q && (q.x < 0 || q.y < 0 || q.x > sc.width || q.y > sc.height)) edgeArrow(q.x, q.y, SHC.pilot);
    else if (!q) edgeArrow(sc.width / 2, sc.height * 2, SHC.pilot);
  }
  // effects
  for (const f of w.fx) {
    const q = proj(f.x, f.y, f.z); if (!q) continue; const k = f.t / 1.2;
    sg.globalAlpha = 1 - k;
    if (f.k === 'tele' && f.x2 != null) { const q2 = proj(f.x2, py, f.z2); if (q2) { sg.strokeStyle = SHC.pilot; sg.lineWidth = 2 * cam.k; sg.setLineDash([3 * cam.k, 5 * cam.k]); sg.beginPath(); sg.moveTo(q.x, q.y); sg.lineTo(q2.x, q2.y); sg.stroke(); sg.setLineDash([]); } }
    else { sg.strokeStyle = { sting: SHC.danger, eaten: '#ff9aa6', lamb: '#ffffff', poo: SHC.jade, decoy: SHC.decoy, call: '#9ff7c6', pen: '#9ff7c6', hit: '#ff5a3c', ko: '#ffd0ec' }[f.k] || '#fff'; sg.lineWidth = 2 * cam.k; sg.beginPath(); sg.arc(q.x, q.y, (6 + 40 * k) * cam.k * (f.k === 'pen' || f.k === 'call' ? 3 : 1), 0, TAU); sg.stroke(); }
    sg.globalAlpha = 1;
  }
  // the touch stick
  for (const t of touches.values()) if (t.kind === 'stick') { const r = sc.getBoundingClientRect(), x0 = (t.x0 - r.left) * cam.k, y0 = (t.y0 - r.top) * cam.k; sg.strokeStyle = 'rgba(47,214,200,.6)'; sg.lineWidth = 2 * cam.k; sg.beginPath(); sg.arc(x0, y0, 60 * cam.k, 0, TAU); sg.stroke(); sg.fillStyle = 'rgba(47,214,200,.5)'; sg.beginPath(); sg.arc((t.x - r.left) * cam.k, (t.y - r.top) * cam.k, 22 * cam.k, 0, TAU); sg.fill(); }
}
function edgeArrow(x, y, col) {
  const cx = sc.width / 2, cy = sc.height / 2, a = Math.atan2(y - cy, x - cx), m = 30 * cam.k;
  const ex = clamp(cx + Math.cos(a) * 1e4, m, sc.width - m), ey = clamp(cy + Math.sin(a) * 1e4, m + 40 * cam.k, sc.height - m);
  sg.save(); sg.translate(ex, ey); sg.rotate(a); sg.fillStyle = col; sg.beginPath(); sg.moveTo(12 * cam.k, 0); sg.lineTo(-8 * cam.k, 8 * cam.k); sg.lineTo(-8 * cam.k, -8 * cam.k); sg.closePath(); sg.fill(); sg.restore();
}
let shHudT = 0;
function shepHud() {
  const w = SW, p = w.pilot, s = w.stats, g = SHEP_GAMES.find((x) => x.id === w.game);
  const badges = [['Time', SP.elTime], ['Space', SP.elSpace], ['Mass', SP.elMass], ['Charge', SP.elCharge]].filter((e) => L5(e[1])).map((e) => e[0] + ' L5').join(' · ');
  const extra = w.game === 'time' ? 'pens ' + w.penNext + '/5' : w.game === 'space' ? 'enemy eaten ' + s.enemyEaten : w.game === 'mass' ? 'lambs ' + s.born + ' · your mass ' + w.prisms.filter((q) => q.kind === 'jade').length : 'petals +' + s.petalsTaken + ' -' + s.petalsLost + ' · KO ' + s.ko;
  $('shepHud').innerHTML = '<span class="big">' + shepScore() + '</span>  ' + g.name + '  ·  ' + Math.max(0, Math.ceil(SP.round - w.t)) + ' s\n' +
    extra + '\nflock ' + w.sheep.length + ' · danger ' + Math.round(SEL.danger() * 100) + '% · ring ' + Math.round(SEL.ring()) + ' u\n' +
    'pilot ' + Math.round(p.v) + ' u/s · ' + p.petals + '✿ · call ' + (p.callT > 0 ? 'ON' : p.callCd > 0 ? Math.ceil(p.callCd) + ' s' : 'ready') + ' · decoy ' + (w.decoy ? 'set' : 'none') + (badges ? '\n' + badges : '');
  const callBtn = $('shepCall'); callBtn.disabled = p.callCd > 0; callBtn.textContent = p.callCd > 0 ? Math.ceil(p.callCd) + ' s' : 'CALL';
  $('shepPads').hidden = !(shepState.role === 'pilot' || (shepState.role === 'both' && PLATFORM.device === 'phone'));
}
let shToastT = 0;
function shToast(msg) { const t = $('shepToast'); t.textContent = msg; t.classList.add('show'); clearTimeout(shToastT); shToastT = setTimeout(() => t.classList.remove('show'), 3800); }

// ---- loop ----
let shLast = performance.now(), shAcc = 0;
function shepFrame(now) {
  if (!shepOn) return;
  const dt = Math.min(0.05, Math.max(0, (now - shLast) / 1000)); shLast = now;
  if (!shepState.manual) {
    shepResize();
    if (!shepState.paused && !SW.done) {
      shAcc += dt;
      while (shAcc >= 1 / 60) { shAcc -= 1 / 60; shepStep(1 / 60, liveInputFor()); }
      if (SW.done) shepEndCard();
    }
    camUpdate(dt); shepRender();
    shHudT -= dt; if (shHudT <= 0) { shHudT = 0.2; shepHud(); }
  }
  requestAnimationFrame(shepFrame);
}
function shepReset(seed) {
  if (seed != null) shepState.seed = seed;
  shepWorld(shepState.seed, shepState.game);
  for (const d of shIn.dogs) d.down = false; touches.clear();
  $('shepEnd').hidden = true;
  camUpdate(0); shepHud();
}
function shepPause() { shepState.paused = !shepState.paused; $('shepPause').textContent = shepState.paused ? 'Resume' : 'Pause'; }
function shepOpen() {
  shepOn = true; shepRoot.hidden = false; shepResize(); shepReset();
  shToast(SHEP_ROLES.find((r) => r.id === shepState.role).how);
  shLast = performance.now(); requestAnimationFrame(shepFrame);
}
function shepClose() { shepOn = false; shepRoot.hidden = true; toggleSheet(false); if (document.fullscreenElement && document.exitFullscreen) document.exitFullscreen().catch(() => {}); }
function shepEndCard() {
  const box = $('shepEnd'), w = SW;
  box.innerHTML = '';
  box.appendChild(el('h3', '', SHEP_GAMES.find((g) => g.id === w.game).name + ': ' + shepScore()));
  box.appendChild(el('div', '', 'Flock ' + w.sheep.length + ' (born ' + w.stats.born + ', lost ' + w.stats.lost + ') · decoys ' + w.stats.decoys + ' · teleports ' + w.stats.teleports + ' · calls ' + w.stats.calls));
  const row = el('div', 'row2');
  const b1 = el('button', 'sbtn', 'Play again'); b1.onclick = () => shepReset(shepState.seed + 1);
  const b2 = el('button', 'sbtn', 'Keep this direction'); b2.onclick = async () => { const where = await shepRecord('keep', ''); shToast(where === 'shared' ? 'Recorded in the shared log.' : 'Recorded in this browser.'); };
  const b3 = el('button', 'sbtn', 'Settings + notes'); b3.onclick = () => toggleSheet(true);
  row.append(b1, b2, b3); box.appendChild(row); box.hidden = false;
}
function shepDiff() { const o = {}; for (const k of Object.keys(SHEP_SHIPPED)) if (Math.abs(SP[k] - SHEP_SHIPPED[k]) > 1e-9) o[k] = SP[k]; return o; }
async function shepRecord(choice, note) {
  const settings = { game: SW ? SW.game : shepState.game, role: shepState.role, seed: shepState.seed, changed: shepDiff(), score: SW ? shepScore() : null };
  return record('shepherd:' + settings.game, choice, (note ? note + ' ' : '') + JSON.stringify(settings));
}

// ---- the settings sheet ----
function toggleSheet(open) {
  shepState.sheet = open == null ? !shepState.sheet : open;
  $('shepSheet').hidden = !shepState.sheet; $('shepMore').setAttribute('aria-expanded', String(shepState.sheet));
  if (shepState.sheet) buildSheet();
}
function buildSheet() {
  const box = $('shepSheet'); box.replaceChildren();
  const head = el('div', 'head'); head.appendChild(el('h2', '', 'Shepherd settings')); const x = el('button', 'btn', 'Close'); x.onclick = () => toggleSheet(false); head.appendChild(x); box.appendChild(head);
  const g = SHEP_GAMES.find((v) => v.id === shepState.game), r = SHEP_ROLES.find((v) => v.id === shepState.role);
  box.appendChild(el('p', 'hint', g.how)); box.appendChild(el('p', 'hint', r.how));
  const nd = Object.keys(shepDiff()).length; box.appendChild(el('p', nd ? 'status-warn' : 'hint', nd ? nd + ' setting(s) differ from shipped / round-2 defaults (underlined).' : 'All settings at their shipped / round-2 values.'));
  let grp = '';
  for (const [group, key, lo, hi, step, label, tip] of SHEP_SPEC) {
    if (group !== grp) { grp = group; box.appendChild(el('h2', 'sec', group)); }
    const row = el('div', 'row'), lab = el('label'), out = el('output'), inp = document.createElement('input');
    lab.appendChild(el('span', '', label)); if (group === 'Elements' && L5(SP[key])) lab.appendChild(el('span', 'el5', 'L5'));
    inp.type = 'range'; inp.min = lo; inp.max = hi; inp.step = step; inp.value = SP[key]; inp.id = 'shep-' + key; inp.setAttribute('aria-label', label);
    const show = () => { out.textContent = fmt(SP[key], step); out.classList.toggle('changed', Math.abs(SP[key] - SHEP_SHIPPED[key]) > 1e-9); out.title = 'shipped ' + SHEP_SHIPPED[key] + ' · yours ' + SP[key]; };
    inp.oninput = () => { SP[key] = +inp.value; lsSet(SHEP_LS, shepDiff()); show(); if (key === 'sheep' || key === 'intensity') shepReset(); if (group === 'Elements') { lab.querySelector('.el5') && lab.querySelector('.el5').remove(); if (L5(SP[key])) lab.appendChild(el('span', 'el5', 'L5')); } };
    show(); row.append(lab, out, inp, el('div', 'tip', tip)); box.appendChild(row);
  }
  const tb = el('label', 'flex'); const cb = document.createElement('input'); cb.type = 'checkbox'; cb.id = 'shepThumb'; cb.checked = shepState.showThumb; cb.onchange = () => { shepState.showThumb = cb.checked; }; tb.append(cb, el('span', '', 'Show the big-thumb disc under a held dog'));
  box.appendChild(tb);
  const rs = el('button', 'btn', 'Back to shipped / round-2 values'); rs.onclick = () => { Object.assign(SP, SHEP_SHIPPED); lsSet(SHEP_LS, {}); buildSheet(); shepReset(); }; box.appendChild(el('p')).appendChild(rs);
  // scorecard
  box.appendChild(el('h2', 'sec', 'Scorecard (AI crews, 30 s, seeds 1-2)'));
  box.appendChild(el('p', 'hint', 'Each row plays all four games with the AI pilot and AI dogs. Pilot only and Dogs only are the baselines; your current settings are the starting point for every row.'));
  const run = el('button', 'btn primary', 'Run scorecard'); const tw = el('div', 'tblWrap');
  run.onclick = () => {
    run.disabled = true; shepState.batch = []; let i = 0;
    (function next() {   // one row per task, so the page stays alive on a phone
      if (i >= SHEP_ROWS.length) { run.disabled = false; run.textContent = 'Run again'; return; }
      run.textContent = 'Running ' + (i + 1) + '/' + SHEP_ROWS.length + '...';
      shepState.batch.push(shepBatch({ seeds: [1, 2], rows: [SHEP_ROWS[i++]] })[0]); drawBatch(tw); setTimeout(next, 0);
    })();
  };
  box.appendChild(run); box.appendChild(tw); if (shepState.batch) drawBatch(tw);
  // decisions
  box.appendChild(el('h2', 'sec', 'Your call'));
  const ta = document.createElement('textarea'); ta.id = 'shepNote'; ta.placeholder = 'What felt right or wrong? (saved with the game, role and settings)'; box.appendChild(ta);
  const row = el('div', 'flex');
  for (const [c, lbl] of [['keep', 'Keep'], ['change', 'Keep, with changes'], ['cut', 'Cut']]) { const b = el('button', 'btn', lbl); b.onclick = async () => { const where = await shepRecord(c, ta.value); ta.value = ''; shToast(where === 'shared' ? 'Recorded in the shared log.' : 'Recorded in this browser.'); }; row.appendChild(b); }
  box.appendChild(row);
  box.appendChild(el('p', 'hint', db ? 'Shared log: Claude reads it next round.' : 'Kept in this browser until the shared log connects.'));
  // not modelled
  box.appendChild(el('h2', 'sec', 'What this does NOT model'));
  const ul = el('ul');
  for (const t of [
    'One screen for both crews. In the game the pilot and the dog player have their own screens and cameras; here the camera follows the flock.',
    'The thumb floor uses a nominal phone (6.3 CSS px per mm, 390 px tall in landscape). Real phones differ by about 15%.',
    'The pilot flies the Rhino\'s shipped speed and straight-line ramp, but not its sword, shield swipe or trail. Time does not speed its ramp here (Time is the flock\'s).',
    'Repulsion is a vertical cylinder round each herder, so it reads from above; sheep far above the plane still flee.',
    'Sheep are not prisms with colliders. 180 of them would need the drone path (vessel agents, never lifeforms).',
    'Rivals are scripted wolves, not players. No networking, so no latency between the dog player and the pilot.',
    'The AI crew is a heuristic (Strombom-style drive and collect). Treat a gap under ~10% as noise.',
  ]) ul.appendChild(el('li', 'hint', t));
  box.appendChild(ul);
}
function drawBatch(tw) {
  const t = el('table'), cols = SHEP_GAMES.map((g) => g.id), best = {};
  for (const c of cols) best[c] = Math.max(...shepState.batch.map((r) => r[c]));
  const hr = el('tr'); hr.appendChild(el('th', '', 'Build')); for (const g of SHEP_GAMES) hr.appendChild(el('th', '', g.el[0].toUpperCase() + g.el.slice(1))); t.appendChild(hr);
  for (const r of shepState.batch) { const tr = el('tr'); tr.appendChild(el('td', '', r.name)); for (const c of cols) { const td = el('td', r[c] === best[c] && r[c] > 0 ? 'best' : '', String(r[c])); tr.appendChild(td); } t.appendChild(tr); }
  tw.replaceChildren(t);
}

$('shepBack').onclick = shepClose;
$('shepRestart').onclick = () => shepReset();
$('shepPause').onclick = shepPause;
$('shepMore').onclick = () => toggleSheet();
$('shepGame').onchange = () => { shepState.game = $('shepGame').value; lsSet('lab:' + LAB.name + ':shepGame', shepState.game); shepReset(); if (shepState.sheet) buildSheet(); shToast(SHEP_GAMES.find((g) => g.id === shepState.game).how); };
$('shepRole').onchange = () => { shepState.role = $('shepRole').value; lsSet('lab:' + LAB.name + ':shepRole', shepState.role); for (const d of shIn.dogs) d.down = false; touches.clear(); if (shepState.sheet) buildSheet(); shToast(SHEP_ROLES.find((r) => r.id === shepState.role).how); };
addEventListener('resize', () => { if (shepOn) shepResize(); });

// the lab header gets the door in; #shepherd on the link opens straight into it
{
  const b = el('button', 'btn primary', 'Shepherd'); b.id = 'shepOpen'; b.title = 'Round 2: the shepherd, two sheepdogs and a flock';
  $('platform').before(b); b.onclick = shepOpen;
  if (location.hash === '#shepherd') setTimeout(shepOpen, 0);
}
shepWorld(1, shepState.game);

// the test surface
window.__lab.shep = {
  SHIPPED: SHEP_SHIPPED, SPEC: SHEP_SPEC, P: SP, GAMES: SHEP_GAMES, ROWS: SHEP_ROWS,
  open: shepOpen, close: shepClose,
  set manualClock(v) { shepState.manual = !!v; }, get manualClock() { return shepState.manual; },
  reset: (seed, game) => { if (game) { shepState.game = game; $('shepGame').value = game; } shepReset(seed); },
  tick(dt, n) { n = n || 1; for (let i = 0; i < n; i++) shepStep(dt || 1 / 60, liveInputFor()); camUpdate(0); shepRender(); },
  input: (o) => { if (o.pilot) Object.assign(shIn.pilot, o.pilot); if (o.dogs) o.dogs.forEach((d, k) => Object.assign(shIn.dogs[k], d)); },
  run: shepRun, runBatch: shepBatch, score: () => shepScore(),
  state: () => ({ t: SW.t, game: SW.game, sheep: SW.sheep.length, score: shepScore(), ring: SEL.ring(), floor: SEL.floorU(), danger: SEL.danger(), decoy: SW.decoy ? { x: SW.decoy.x, z: SW.decoy.z, by: SW.decoy.by } : null, pilot: { x: SW.pilot.x, y: SW.pilot.y, z: SW.pilot.z, v: SW.pilot.v, callT: SW.pilot.callT, callCd: SW.pilot.callCd }, dogs: SW.dogs.map((d) => ({ x: d.x, z: d.z, down: d.down })), stats: Object.assign({}, SW.stats), done: SW.done }),
};
