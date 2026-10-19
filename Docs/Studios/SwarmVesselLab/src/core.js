// ============================================================================================
// core.js — the lab meta, the shipped numbers, the sliders, and THE MODEL (world + swarm step).
// simStep never draws and never reads a device: input arrives as one object (touch, mouse, gamepad
// and the AI commander all write the same shape), so the batch can run it headless thousands of times.
// ============================================================================================
const $ = (id) => document.getElementById(id);

// ================= (1) LAB meta + the shipped numbers =================
// A READER of the shipped numbers, never their authority. Every value that exists in the game names the
// asset or field it was copied from; everything else is a lab knob ("lab:"). The game has no swarm VESSEL
// yet, so most of this page is proposal — the Termite deck and the locust's rules are the shipped anchors.
const LAB = { name: 'Swarm Vessel Lab', branch: 'cece/inspiring-einstein-k6syi5', round: 1, doc: 'Docs/Studios/SWARM_VESSEL_LAB.md' };
const SHIPPED = {
  cruise: 70,          // TermiteCommandTransformer.commandCruiseSpeed (Time 0; 112 at Time 10)
  accel: 90,           // TermiteCommandTransformer.commandAcceleration (u/s per s)
  pherMax: 10,         // TermitePheromoneAction.asset maxPheromone
  pherStart: 5,        // TermitePheromoneAction.asset startPheromone
  pherRegen: 0.357,    // TermitePheromoneAction.asset regenPerSecond.Value (0.714 at Time 10)
  droneSpeed: 55,      // TermiteDroneTuning.speed
  forage: 70,          // TermiteDroneTuning.forageRadius
  carry: 3,            // TermiteDroneTuning.carryCapacity
  droneCap: 36,        // TermiteDeckExecutor.maxLiveDrones
  locustMarch: 135,    // Tools/Ecology/bestiary/species/locust.py (gregarious march, u/s)
  locustCrowd: 40,     // locust.py: neighbours inside 40 u set the phase
  elTime: 2,           // lab: the swarm's TIME level 0..10 (Notion fauna concepts: Time = movement speed, spawn rate)
  elSpace: 2,          // lab: SPACE level (reach, spread, sense radius)
  elMass: 2,           // lab: MASS level (body size, hit points, carry)
  elCharge: 2,         // lab: CHARGE level (aggression thresholds, damage, debuff length)
  pop: 1,              // lab: population scale on every species' base count
  aggression: 0.5,     // lab: the B5 knob (NCA backlog B5 "calm <-> centroid-rush"), read by the Mood control
  rivalSkill: 0.5,     // lab: rival vessel accuracy and fire rate
  aiNoise: 0.15,       // lab: the AI commander's sloppiness (0 = expert, 1 = rookie)
  round: 45,           // lab: seconds per minigame round
};

// ================= (2) SPEC: one row per slider =================
const SPEC = [
  ['Elements', 'elTime',   0, 10, 1, 'Time',   'Swarm speed and how fast it breeds (+6% speed and +8% birth rate per level). Hull cruise follows the shipped 70 -> 112 curve.'],
  ['Elements', 'elSpace',  0, 10, 1, 'Space',  'How far members sense and reach (+8% per level), and how wide the swarm spreads.'],
  ['Elements', 'elMass',   0, 10, 1, 'Mass',   'Member size, hit points and carry capacity (+8% per level).'],
  ['Elements', 'elCharge', 0, 10, 1, 'Charge', 'Damage, sting and drain strength, debuff length (+8% per level). Lowers the locust crowd threshold.'],
  ['Swarm', 'pop',        0.25, 3, 0.05, 'Population scale', 'Multiplies every species\' base member count. Phones stay smooth up to about 2.'],
  ['Swarm', 'aggression', 0, 1, 0.01, 'Aggression (B5 knob)', 'Calm (escort the hull, avoid threats) to centroid-rush (charge the nearest enemy mass). Read live by the Mood control.'],
  ['Swarm', 'droneSpeed', 20, 160, 1, 'Member base speed', 'Shipped Termite drone speed. Each species scales it by its own factor.'],
  ['Swarm', 'forage', 20, 240, 1, 'Forage radius', 'Shipped Termite forage radius: how far a member looks for work around its focus.'],
  ['Swarm', 'carry', 1, 8, 1, 'Carry capacity', 'Shipped Termite carry: prisms a carrier moves per trip (the lab moves them as one parcel).'],
  ['Swarm', 'locustCrowd', 15, 90, 1, 'Locust crowd radius', 'locust.py: neighbours inside this radius push a locust toward the gregarious phase.'],
  ['Swarm', 'locustMarch', 60, 220, 1, 'Locust march speed', 'locust.py: gregarious march speed.'],
  ['Economy', 'pherMax', 4, 20, 1, 'Pheromone tank', 'Shipped Termite tank. Elixir-style economies and card costs are priced in it.'],
  ['Economy', 'pherStart', 0, 20, 1, 'Starting pheromone', 'Shipped start.'],
  ['Economy', 'pherRegen', 0.1, 2, 0.01, 'Pheromone per second', 'Shipped 0.357/s at Time 0 (0.714 at Time 10).'],
  ['Economy', 'droneCap', 6, 120, 1, 'Deck member cap', 'Shipped Termite cap on live drones; the deck economy respects it.'],
  ['Hull', 'cruise', 30, 200, 1, 'Hull cruise', 'Shipped Termite commanded cruise at Time 0.'],
  ['Hull', 'accel', 20, 300, 1, 'Hull acceleration', 'Shipped Termite commanded acceleration.'],
  ['Arena', 'rivalSkill', 0, 1, 0.05, 'Rival skill', 'Rival vessels\' fire rate and aim.'],
  ['Arena', 'aiNoise', 0, 1, 0.05, 'AI commander noise', '0 = an expert commander, 1 = a rookie. The scorecard runs both.'],
  ['Arena', 'round', 20, 120, 5, 'Round length (s)', 'Seconds per minigame round.'],
];

const LS_KEY = 'lab:' + LAB.name + ':P';
const lsGet = (k, d) => { try { const v = localStorage.getItem(k); return v ? JSON.parse(v) : d; } catch (e) { return d; } };
const lsSet = (k, v) => { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) { /* private window */ } };
const P = Object.assign({}, SHIPPED, lsGet(LS_KEY, {}));
for (const k of Object.keys(P)) if (!(k in SHIPPED)) delete P[k];

// ================= seeded RNG =================
function mulberry32(a) { return () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }

// ================= geometry =================
const R = 600;            // cell membrane radius (lab units, about one game unit each)
const NUC = 90;           // nucleus radius
const TAU = Math.PI * 2;
const clamp = (v, a, b) => v < a ? a : v > b ? b : v;
const len = (x, y) => Math.sqrt(x * x + y * y);
const DOM = { NEUTRAL: 0, JADE: 1, RUBY: 2, GOLD: 3 };
const ME = DOM.JADE;

// Element multipliers (Notion "Fauna Concepts": Mass bigger blocks, Space reach/separation, Time speed,
// Charge thresholds/debuff duration). Each is 1 at level 0.
const EL = {
  spd:   () => 1 + 0.06 * P.elTime,
  birth: () => 1 + 0.08 * P.elTime,
  reach: () => 1 + 0.08 * P.elSpace,
  size:  () => 1 + 0.08 * P.elMass,
  dmg:   () => (1 + 0.08 * P.elCharge) * (W && W.mods ? W.mods.bite : 1),
};
const hullCruise = () => P.cruise * (1 + 0.6 * P.elTime / 10);   // shipped 70 -> 112 over Time 0..10

// ================= spatial hash (prisms) =================
const CELL = 24;
function hkey(cx, cy) { return (cx + 512) * 1024 + (cy + 512); }
function hashBuild(list) {
  const m = new Map();
  for (const p of list) {
    if (!p.alive) continue;
    const k = hkey(Math.floor(p.x / CELL), Math.floor(p.y / CELL));
    let b = m.get(k); if (!b) { b = []; m.set(k, b); } b.push(p);
  }
  return m;
}
function hashQuery(m, x, y, r, fn) {
  const c0 = Math.floor((x - r) / CELL), c1 = Math.floor((x + r) / CELL), d0 = Math.floor((y - r) / CELL), d1 = Math.floor((y + r) / CELL), r2 = r * r;
  for (let cx = c0; cx <= c1; cx++) for (let cy = d0; cy <= d1; cy++) {
    const b = m.get(hkey(cx, cy)); if (!b) continue;
    for (const p of b) { if (!p.alive) continue; const dx = p.x - x, dy = p.y - y; if (dx * dx + dy * dy <= r2) if (fn(p, dx * dx + dy * dy) === false) return; }
  }
}

// ================= fields (pheromone, trail, blood) on a coarse grid =================
const FG = 80, FS = (2 * R) / FG;
function fieldIdx(x, y) { const i = Math.floor((x + R) / FS), j = Math.floor((y + R) / FS); return (i < 0 || j < 0 || i >= FG || j >= FG) ? -1 : j * FG + i; }
function fieldDiffuse(f, keep, spread) {
  const out = new Float32Array(f.length);
  for (let j = 1; j < FG - 1; j++) for (let i = 1; i < FG - 1; i++) {
    const k = j * FG + i, n = (f[k - 1] + f[k + 1] + f[k - FG] + f[k + FG]) * 0.25;
    out[k] = (f[k] * (1 - spread) + n * spread) * keep;
  }
  f.set(out);
}
function fieldGrad(f, x, y) {
  const k = fieldIdx(x, y); if (k < FG || k >= f.length - FG) return [0, 0];
  return [f[k + 1] - f[k - 1], f[k + FG] - f[k - FG]];
}

// ================= the world =================
let W = null;   // the live world; the batch swaps it out and back
function newWorld(seed) {
  return {
    seed, rnd: mulberry32(seed), t: 0, prisms: [], crystals: [], rivals: [], agents: [], fauna: [], proj: [], fx: [],
    homes: [], baits: [], marks: [], webs: [], paint: [], gates: null,
    hash: null, ahash: null, pher: new Float32Array(FG * FG), trail: new Float32Array(FG * FG), blood: new Float32Array(FG * FG),
    hull: null, econ: null, meta: null, ctl: null, sp: null, scene: 'sandbox', game: null,
    stats: { eaten: 0, destroyed: 0, built: 0, delivered: 0, crystals: 0, petalsTaken: 0, petalsLost: 0, births: 0, deaths: 0, stings: 0, flashes: 0, plays: 0, merges: 0, revealed: 0, snared: 0 },
    focus: null, selection: null, mood: P.aggression, order: 0, orderAt: 0, sync: 0, syncKick: 0, leaderIdx: -1, formation: 'ring', cast: false,
    tick: 0, done: false, finishT: null, fortress: 0, drafts: 0, nextDraft: 20, nextRoulette: 15, toast: null, rouletteIdx: 0,
  };
}

function addPrism(x, y, dom, opt) {
  if (len(x, y) > R - 8) return null;
  const p = { x, y, dom, alive: true, hp: (opt && opt.hp) || 1, sh: (opt && opt.sh) || 0, carried: -1, mark: 0, built: (opt && opt.built) || 0, claims: 0, tag: (opt && opt.tag) || 0, hidden: false };
  W.prisms.push(p);
  return p;
}
function killPrism(p, cause) {
  if (!p.alive) return;
  p.alive = false;
  W.stats.destroyed++;
  if (p.dom !== ME && p.dom !== DOM.NEUTRAL && cause !== 'fauna') W.stats.hostileKills = (W.stats.hostileKills || 0) + 1;
  if (p.tag === 1) W.fortress--;
  const k = fieldIdx(p.x, p.y); if (k >= 0) W.blood[k] += 1;
  if (cause !== 'quiet') W.fx.push({ k: 'pop', x: p.x, y: p.y, t: 0, dom: p.dom });
}
function addCrystal(x, y, opt) {
  const c = { x, y, alive: true, kind: (opt && opt.kind) || 'omni', hidden: !!(opt && opt.hidden), order: (opt && opt.order) != null ? opt.order : -1, carriedBy: -1, decoy: !!(opt && opt.decoy) };
  W.crystals.push(c); return c;
}
function addRival(x, y, dom, mode) {
  const r = { x, y, vx: 0, vy: 0, dom, petals: 10, petalDebt: 0, alive: true, cd: 1 + W.rnd(), mode: mode || 'hunt', marked: 0, respawn: 0, slow: 0, stun: 0, tx: x, ty: y, retarget: 0, trailT: 0, latched: 0, heat: 0 };
  W.rivals.push(r); return r;
}
function addHome(x, y, kind) { const h = { x, y, kind, r: 16, sites: 0, nestHp: 0, spawnT: 0, stock: 0 }; W.homes.push(h); return h; }

// clump of prisms (a resource patch, a wall arc, a ring)
function clump(cx, cy, n, spread, dom, opt) {
  for (let i = 0; i < n; i++) { const a = W.rnd() * TAU, r = Math.sqrt(W.rnd()) * spread; addPrism(cx + Math.cos(a) * r, cy + Math.sin(a) * r, dom, opt); }
}
function ring(cx, cy, rad, thick, step, dom, opt) {
  for (let r = rad; r < rad + thick; r += step) {
    const n = Math.floor(TAU * r / step);
    for (let i = 0; i < n; i++) { const a = i / n * TAU; const o = Object.assign({}, opt); if (opt && opt.shieldFrac && W.rnd() < opt.shieldFrac) o.sh = 1; addPrism(cx + Math.cos(a) * r, cy + Math.sin(a) * r, dom, o); }
  }
}
function arc(cx, cy, rad, a0, a1, step, dom, opt) {
  const n = Math.max(2, Math.floor(Math.abs(a1 - a0) * rad / step));
  for (let i = 0; i <= n; i++) { const a = a0 + (a1 - a0) * i / n; addPrism(cx + Math.cos(a) * rad, cy + Math.sin(a) * rad, dom, opt); }
}
function line(x0, y0, x1, y1, step, dom, opt) {
  const L = len(x1 - x0, y1 - y0), n = Math.max(1, Math.floor(L / step));
  for (let i = 0; i <= n; i++) addPrism(x0 + (x1 - x0) * i / n, y0 + (y1 - y0) * i / n, dom, opt);
}

// ================= the hull (the vessel the swarm belongs to) =================
function makeHull(x, y) { return { x, y, vx: 0, vy: 0, hx: 1, hy: 0, petals: 10, mode: 'auto', tx: x, ty: y, trailT: 0, ramCd: 0, hp: 1, alive: true, roam: null, stick: false }; }

function stepHull(dt, input) {
  const h = W.hull, ctl = W.ctl;
  let tx = h.x, ty = h.y, want = 0;
  const mode = ctl.hull;                 // 'auto' | 'stick' | 'anchor'
  if (mode === 'stick') {
    if (input.down && input.px != null) { tx = input.px; ty = input.py; want = hullCruise(); }
    else if (W.ctl.id === 'school' || W.ctl.id === 'formation' || W.ctl.id === 'leash' || W.ctl.id === 'sheepdog') { tx = h.tx; ty = h.ty; want = len(tx - h.x, ty - h.y) > 8 ? hullCruise() : 0; }
    if (input.down && input.px != null) { h.tx = input.px; h.ty = input.py; }
  } else if (mode === 'auto') {
    // The queen flies herself: roam the cell band and swing toward a crystal she may collect.
    if (!h.roam || len(h.roam.x - h.x, h.roam.y - h.y) < 14 || W.t > h.roam.until) {
      let best = null, bd = 1e9;
      for (const c of W.crystals) if (c.alive && !c.hidden && c.carriedBy < 0 && !c.decoy && (W.game !== 'time' || c.order === W.gateNext)) { const d = len(c.x - h.x, c.y - h.y); if (d < bd && (d < 320 || W.game === 'time')) { bd = d; best = c; } }
      const threat = W.game === 'charge' ? nearestRival(h.x, h.y, 220) : null;
      if (threat) { const ax = h.x - threat.x, ay = h.y - threat.y, ad = len(ax, ay) || 1; best = { x: clamp(h.x + (ax / ad - ay / ad * 0.8) * 160, -R * 0.8, R * 0.8), y: clamp(h.y + (ay / ad + ax / ad * 0.8) * 160, -R * 0.8, R * 0.8) }; }
      if (best) h.roam = { x: best.x, y: best.y, until: W.t + (threat ? 1.2 : 6) };
      else { const a = W.rnd() * TAU, r = NUC * 1.6 + W.rnd() * (R * 0.7 - NUC * 1.6); h.roam = { x: Math.cos(a) * r, y: Math.sin(a) * r, until: W.t + 9 }; }
    }
    tx = h.roam.x; ty = h.roam.y; want = hullCruise() * 0.7;
  }
  const dx = tx - h.x, dy = ty - h.y, d = len(dx, dy);
  let ax = 0, ay = 0;
  if (d > 4 && want > 0) { const s = Math.min(want, Math.sqrt(2 * P.accel * d)); ax = dx / d * s - h.vx; ay = dy / d * s - h.vy; }
  else { ax = -h.vx; ay = -h.vy; }
  const am = len(ax, ay), lim = P.accel * dt;
  if (am > lim) { ax *= lim / am; ay *= lim / am; }
  h.vx += ax; h.vy += ay;
  h.x += h.vx * dt; h.y += h.vy * dt;
  const r = len(h.x, h.y); if (r > R - 12) { h.x *= (R - 12) / r; h.y *= (R - 12) / r; h.vx *= 0.5; h.vy *= 0.5; }
  const sp = len(h.vx, h.vy); if (sp > 1) { h.hx = h.vx / sp; h.hy = h.vy / sp; }
  // The hull lays its own thin trail: conserved mass, the Mass baseline every vessel has.
  h.trailT -= dt;
  if (sp > 20 && h.trailT <= 0) { h.trailT = 0.55; addPrism(h.x - h.hx * 6, h.y - h.hy * 6, ME, { built: 2 }); }
  // A weak ram: the hull breaks one hostile prism it flies into, and collects crystals it touches.
  h.ramCd -= dt;
  if (h.ramCd <= 0) hashQuery(W.hash, h.x, h.y, 7, (p) => { if (p.dom !== ME && p.sh === 0 && p.carried < 0) { killPrism(p); h.ramCd = 0.4; return false; } });
  for (const c of W.crystals) if (c.alive && !c.hidden && c.carriedBy < 0 && len(c.x - h.x, c.y - h.y) < 10) collectCrystal(c, 'hull');
}

function collectCrystal(c, by) {
  if (c.decoy) return;
  if (W.game === 'time' && c.order >= 0 && c.order !== W.gateNext) return;
  c.alive = false; c.carriedBy = -1;
  W.stats.crystals++;
  if (W.game === 'time' && c.order >= 0) { W.gateNext++; if (W.gateNext >= W.gateCount && W.finishT == null) W.finishT = W.t; }
  econOnCrystal(c);
  W.fx.push({ k: 'ring', x: c.x, y: c.y, t: 0, r: 26, col: '#9ff7c6' });
  if (W.game !== 'time' && W.scene !== 'dark') {
    // respawn somewhere else (crystals are the cell's, not the swarm's)
    const a = W.rnd() * TAU, r = NUC * 1.4 + W.rnd() * (R * 0.75 - NUC * 1.4);
    addCrystal(Math.cos(a) * r, Math.sin(a) * r, { kind: W.rnd() < 0.5 ? 'omni' : 'el' });
  }
}

// ================= rivals (vessels of another domain) =================
function nearestRival(x, y, maxD) {
  let best = null, bd = maxD || 1e9;
  for (const r of W.rivals) { if (!r.alive) continue; const d = len(r.x - x, r.y - y); if (d < bd) { bd = d; best = r; } }
  return best;
}
function stepRivals(dt) {
  const h = W.hull;
  for (const r of W.rivals) {
    if (!r.alive) continue;
    r.stun = Math.max(0, r.stun - dt); r.slow = Math.max(0, r.slow - dt);
    r.retarget -= dt;
    if (r.retarget <= 0) {
      r.retarget = 1.2 + W.rnd();
      const lure = W.crystals.find((c) => c.alive && c.decoy && len(c.x - r.x, c.y - r.y) < 320);
      if (lure) { r.tx = lure.x; r.ty = lure.y; }
      else if (r.mode === 'hunt') { r.tx = h.x + (W.rnd() - .5) * 60; r.ty = h.y + (W.rnd() - .5) * 60; }
      else if (r.mode === 'graze') {
        // grazes our mass: pick a jade prism, else a crystal, else roam
        let best = null, bd = 1e9; for (let i = 0; i < 40; i++) { const p = W.prisms[Math.floor(W.rnd() * W.prisms.length)]; if (p && p.alive && p.dom === ME) { const d = len(p.x - r.x, p.y - r.y); if (d < bd) { bd = d; best = p; } } }
        if (best) { r.tx = best.x; r.ty = best.y; } else { const a = W.rnd() * TAU; r.tx = Math.cos(a) * R * 0.6; r.ty = Math.sin(a) * R * 0.6; }
      } else if (r.mode === 'guard') { const a = W.t * 0.4 + r.dom; r.tx = W.fortX + Math.cos(a) * 110; r.ty = W.fortY + Math.sin(a) * 110; }
      else { // decoy-crystal bait: rivals chase crystals they can see
        let best = null, bd = 400; for (const c of W.crystals) if (c.alive && !c.hidden) { const d = len(c.x - r.x, c.y - r.y); if (d < bd) { bd = d; best = c; } }
        if (best) { r.tx = best.x; r.ty = best.y; } else { const a = W.rnd() * TAU; r.tx = Math.cos(a) * R * 0.5; r.ty = Math.sin(a) * R * 0.5; }
      }
    }
    const spd = (r.stun > 0 ? 0 : (r.slow > 0 ? 45 : 95)) * (1 - Math.min(0.6, r.latched * 0.05));
    const dx = r.tx - r.x, dy = r.ty - r.y, d = len(dx, dy) || 1;
    r.vx += (dx / d * spd - r.vx) * Math.min(1, 2.5 * dt); r.vy += (dy / d * spd - r.vy) * Math.min(1, 2.5 * dt);
    r.x += r.vx * dt; r.y += r.vy * dt;
    const rr = len(r.x, r.y); if (rr > R - 12) { r.x *= (R - 12) / rr; r.y *= (R - 12) / rr; }
    // rivals lay their own trail (hostile mass the swarm can eat or carry)
    r.trailT -= dt; if (r.trailT <= 0 && spd > 0) { r.trailT = 0.6; addPrism(r.x, r.y, r.dom, { built: 3 }); }
    // and eat our mass on contact
    hashQuery(W.hash, r.x, r.y, 8, (p) => { if (p.dom === ME && p.sh === 0 && p.carried < 0) { killPrism(p); } });
    // shoot
    r.cd -= dt * (0.6 + P.rivalSkill);
    if (r.cd <= 0 && r.stun <= 0) {
      r.cd = 1.9 + W.rnd() * 0.8;
      let tx = h.x, ty = h.y;
      // decoy: a dense cloud near the hull draws fire onto a member instead (starling, bat)
      const cloud = W.sp.decoy ? decoyAgentNear(h.x, h.y, 90) : null;
      if (cloud && W.rnd() < Math.min(0.85, W.decoyDensity)) { tx = cloud.x; ty = cloud.y; }
      const dd = len(tx - r.x, ty - r.y);
      if (dd < 300) {
        const spread = (1 - P.rivalSkill) * 0.5 + 0.08;
        const a = Math.atan2(ty - r.y, tx - r.x) + (W.rnd() - 0.5) * spread;
        W.proj.push({ x: r.x, y: r.y, vx: Math.cos(a) * 220, vy: Math.sin(a) * 220, life: 1.5, dom: r.dom });
      }
    }
    // stings and drains accrue as a debt; whole petals transfer
    if (r.petalDebt >= 1) { const n = Math.floor(r.petalDebt); r.petalDebt -= n; const take = Math.min(n, r.petals); r.petals -= take; W.hull.petals += take; W.stats.petalsTaken += take; if (take) econOnPetal(take); if (take) W.fx.push({ k: 'petal', x: r.x, y: r.y, t: 0 }); }
    r.latched = 0;
  }
}
function decoyAgentNear(x, y, rad) {
  let n = 0, pick = null;
  for (const a of W.agents) if (a.alive && Math.abs(a.x - x) < rad && Math.abs(a.y - y) < rad) { n++; if (!pick || W.rnd() < 1 / n) pick = a; }
  W.decoyDensity = n / 40;
  return pick;
}
function stepProjectiles(dt) {
  const h = W.hull;
  for (const q of W.proj) {
    if (q.life <= 0) continue;
    q.x += q.vx * dt; q.y += q.vy * dt; q.life -= dt;
    // members in the way absorb the shot (shield, bridge, decoy)
    for (const a of W.agents) {
      if (!a.alive) continue;
      const dx = a.x - q.x, dy = a.y - q.y, rr = 3 + a.size;
      if (dx * dx + dy * dy < rr * rr) { q.life = 0; hurtAgent(a, 1); break; }
    }
    if (q.life <= 0) continue;
    if (len(h.x - q.x, h.y - q.y) < 9) { q.life = 0; const r = nearestRival(q.x, q.y, 1e9); if (h.petals > 0) { h.petals--; W.stats.petalsLost++; if (r) r.petals++; } W.fx.push({ k: 'hit', x: h.x, y: h.y, t: 0 }); continue; }
    hashQuery(W.hash, q.x, q.y, 5, (p) => { if (p.dom === ME && p.carried < 0) { if (p.sh) p.sh = 0; else killPrism(p); q.life = 0; return false; } });
  }
  W.proj = W.proj.filter((q) => q.life > 0);
}

// ================= neutral fauna (grazers that eat any mass outside the nucleus) =================
function stepFauna(dt) {
  for (const f of W.fauna) {
    if (!f.alive) continue;
    f.t -= dt;
    if (f.t <= 0 || !f.tgt || !f.tgt.alive) {
      f.t = 2; f.tgt = null; let bd = 140;
      hashQuery(W.hash, f.x, f.y, 140, (p, d2) => { const d = Math.sqrt(d2); if (p.carried < 0 && p.sh === 0 && len(p.x, p.y) > NUC && d < bd) { bd = d; f.tgt = p; } });
      if (!f.tgt) { const a = W.rnd() * TAU; f.wx = f.x + Math.cos(a) * 80; f.wy = f.y + Math.sin(a) * 80; }
    }
    const tx = f.tgt ? f.tgt.x : f.wx, ty = f.tgt ? f.tgt.y : f.wy, dx = tx - f.x, dy = ty - f.y, d = len(dx, dy) || 1;
    f.x += dx / d * 30 * dt; f.y += dy / d * 30 * dt;
    if (f.tgt && d < 4) { f.chew -= dt; if (f.chew <= 0) { killPrism(f.tgt, 'fauna'); f.chew = 1.5; f.tgt = null; } }
  }
}

// ================= the swarm: members, genes and verbs =================
// A member is one body. Its SPECIES gives it genes (how it moves) and one VERB (what it does to the world).
// The CONTROL scheme only ever writes a focus point, an order, a mood or a selection — it never moves a
// member directly. That separation is the whole point of the lab: a swarm vessel is a combination of
// (species verb) x (control) x (economy) x (meta), and each axis can be swapped while the others hold.
function addAgent(x, y, opt) {
  const sp = W.sp, s = (opt && opt.species) ? SPECIES_BY_ID[opt.species] : sp;
  if (W.agents.filter((a) => a.alive).length >= agentCap()) return null;
  const a = {
    x, y, vx: 0, vy: 0, alive: true, sp: s.id, verb: (opt && opt.verb) || s.verb, hp: s.hp * EL.size(), size: s.size * EL.size() * ((opt && opt.big) || 1),
    state: 'idle', tgt: null, carry: null, cd: W.rnd(), phase: W.rnd() * TAU, g: 0, age: 0, sq: (opt && opt.sq) || 0, sel: true, link: -1,
    caste: (opt && opt.caste) || 'worker', born: W.t, gene: (opt && opt.gene) ? Object.assign({}, opt.gene) : { spd: 1, sense: 1, bite: 1 }, kills: 0, big: (opt && opt.big) || 1,
  };
  a.hp *= W.mods.hp / ((opt && opt.costHp) || 1);
  W.agents.push(a); W.stats.births++;
  W.fx.push({ k: 'birth', x, y, t: 0 });
  return a;
}
function agentCap() { return Math.round(Math.min(220, (W.econ && W.econ.cap) || P.droneCap) ); }
function liveAgents() { return W.agents.filter((a) => a.alive); }
function hurtAgent(a, dmg) {
  a.hp -= dmg;
  if (a.hp <= 0 && a.alive) {
    a.alive = false; W.stats.deaths++;
    // a body is conserved mass: a dead member leaves one neutral prism where it fell (the §26 skeleton, in miniature)
    if (W.sp.leavesBody) addPrism(a.x, a.y, DOM.NEUTRAL, { built: 0 });
    if (a.carry && a.carry.kind === 'crystal') { a.carry.c.carriedBy = -1; a.carry.c.x = a.x; a.carry.c.y = a.y; }
    W.fx.push({ k: 'die', x: a.x, y: a.y, t: 0 });
  }
}
function memberSpeed(a) {
  const s = SPECIES_BY_ID[a.sp];
  let v = P.droneSpeed * s.speedK * EL.spd() * a.gene.spd * W.mods.spd / Math.sqrt(a.big);
  if (s.id === 'locust') v = P.droneSpeed * 0.55 + (P.locustMarch - P.droneSpeed * 0.55) * a.g;
  if (W.buff.frenzy > 0) v *= 1.45;
  if (a.verb === 'frenzy') { const k = fieldIdx(a.x, a.y); v *= 1 + Math.min(1.5, (k >= 0 ? W.blood[k] : 0) * 0.4); }
  return v;
}
const senseR = (a) => P.forage * EL.reach() * a.gene.sense * W.mods.sense * SPECIES_BY_ID[a.sp].senseK;

// the focus a member is pulled toward: the control's focus point, its squad's focus, or the hull
function focusFor(a) {
  if (W.ctl.id === 'split') return a.sq === 1 ? (W.focusB || W.hull) : (W.focus || W.hull);
  if (W.ctl.id === 'select' && !a.sel) return a.homeFocus || W.hull;
  if (W.ctl.id === 'leader' && W.leaderIdx >= 0 && W.agents[W.leaderIdx] && W.agents[W.leaderIdx].alive && W.agents[W.leaderIdx] !== a) return W.agents[W.leaderIdx];
  if (W.order === 1) return W.hull;                                    // Gather
  if (W.order === 3 && W.homes.length) return W.homes[0];              // Defend
  if (W.bait && W.bait.t > 0) return W.bait;
  return W.focus || W.hull;
}

// work targets — every verb asks the world the same kind of question with its own filter
function findPrism(a, rad, pred) {
  let best = null, bd = rad;
  hashQuery(W.hash, a.x, a.y, rad, (p, d2) => { const d = Math.sqrt(d2); if (d < bd && pred(p)) { bd = d; best = p; } });
  return best;
}
const hostile = (p) => p.dom !== ME && p.carried < 0 && !p.hidden;
const edible = (p) => hostile(p) && p.sh === 0 && len(p.x, p.y) > NUC * 0.6;
function findCrystal(a, rad) {
  let best = null, bd = rad;
  for (const c of W.crystals) {
    if (!c.alive || c.carriedBy >= 0 || (c.hidden && !c.revealed) || c.decoy) continue;
    if (W.game === 'time' && c.order >= 0 && c.order !== W.gateNext) continue;
    const d = len(c.x - a.x, c.y - a.y); if (d < bd) { bd = d; best = c; }
  }
  return best;
}
function buildSite(a) {
  // where carried mass is laid: next to the swarm's newest construction near the focus, so a mound GROWS
  const f = W.homes.length ? W.homes[W.homes.length - 1] : (W.focus || W.hull);
  const ang = W.rnd() * TAU, r = 10 + Math.sqrt(W.rnd()) * (18 + 2.2 * Math.sqrt(W.stats.built + 1));
  return { x: f.x + Math.cos(ang) * r, y: f.y + Math.sin(ang) * r };
}

// The verb table. Each entry returns the member's GOAL (a point) and may act on arrival.
// Keep each one small: a verb is the species' one sentence.
const VERBS = {
  graze(a, dt) {                         // eat hostile mass outside the nucleus (locust, tadpole, the fleet's herbivores)
    if (!a.tgt || !a.tgt.alive || !edible(a.tgt)) a.tgt = findPrism(a, senseR(a), edible);
    if (!a.tgt) return null;
    if (len(a.tgt.x - a.x, a.tgt.y - a.y) < 4 + a.size) { a.cd -= dt * EL.dmg() * a.gene.bite * a.big * (SPECIES_BY_ID[a.sp].id === 'locust' ? 0.35 + 1.4 * a.g : 1); if (a.cd <= 0) { killPrism(a.tgt); W.stats.eaten++; econOnEat(a); a.tgt = null; a.cd = 0.9; } }
    return a.tgt;
  },
  gnaw(a, dt) {                          // rats: chew THROUGH shields; prefer the armoured prism the fleet cannot eat
    if (!a.tgt || !a.tgt.alive || !hostile(a.tgt)) a.tgt = findPrism(a, senseR(a), (p) => hostile(p) && p.sh > 0) || findPrism(a, senseR(a), hostile);
    if (!a.tgt) return null;
    if (len(a.tgt.x - a.x, a.tgt.y - a.y) < 4 + a.size) { a.cd -= dt * EL.dmg() * a.big; if (a.cd <= 0) { if (a.tgt.sh) { a.tgt.sh = 0; W.fx.push({ k: 'shed', x: a.tgt.x, y: a.tgt.y, t: 0 }); } else { killPrism(a.tgt); W.stats.eaten++; econOnEat(a); a.tgt = null; } a.cd = 0.7; } }
    return a.tgt;
  },
  build(a) {                             // termites: carry hostile or neutral mass home and lay it as OUR mass — creation by conversion
    if (a.carry) {
      if (!a.site) a.site = buildSite(a);
      if (len(a.site.x - a.x, a.site.y - a.y) < 5) { for (let i = 0; i < a.carry.n; i++) { const s = i ? buildSite(a) : a.site; const p = addPrism(s.x, s.y, ME, { built: 1, tag: W.game === 'mass' ? 0 : 0 }); if (p) W.stats.built++; } a.carry = null; a.site = null; W.stats.delivered++; }
      return a.site;
    }
    if (!a.tgt || !a.tgt.alive || a.tgt.carried >= 0 || !(a.tgt.dom !== ME && a.tgt.sh === 0)) a.tgt = findPrism(a, senseR(a), (p) => p.dom !== ME && p.sh === 0 && p.carried < 0 && !p.hidden && p.claims < 1);
    if (!a.tgt) return null;
    a.tgt.claims = 1;
    if (len(a.tgt.x - a.x, a.tgt.y - a.y) < 4 + a.size) {
      // take up to `carry` prisms from around the target as one parcel (conserved: they leave and come back as ours)
      let n = 0; const cap = Math.max(1, Math.round(P.carry * EL.size() * 0.5));
      hashQuery(W.hash, a.x, a.y, 9, (p) => { if (n < cap && p.dom !== ME && p.sh === 0 && p.carried < 0) { p.alive = false; n++; } });
      a.carry = { kind: 'mass', n: Math.max(1, n) }; a.tgt = null;
    }
    return a.tgt;
  },
  claim(a, dt) {                         // bees: pollinate — dwell on a neutral flora prism and it turns ours, in place
    if (!a.tgt || !a.tgt.alive || a.tgt.dom === ME) a.tgt = findPrism(a, senseR(a), (p) => p.dom === DOM.NEUTRAL && p.carried < 0 && !p.hidden) || findPrism(a, senseR(a) * 0.6, (p) => hostile(p) && p.sh === 0);
    if (!a.tgt) return null;
    if (len(a.tgt.x - a.x, a.tgt.y - a.y) < 4 + a.size) { a.cd -= dt * EL.dmg(); if (a.cd <= 0) { a.tgt.dom = ME; a.tgt.built = 1; W.stats.built++; W.fx.push({ k: 'claim', x: a.tgt.x, y: a.tgt.y, t: 0 }); a.tgt = null; a.cd = 1.2; } }
    return a.tgt;
  },
  fetch(a) {                             // pigeons, crows: grab a crystal and bring it to the hull (or straight to the gate)
    if (a.carry && a.carry.kind === 'crystal') {
      const c = a.carry.c; c.x = a.x; c.y = a.y;
      if (len(W.hull.x - a.x, W.hull.y - a.y) < 10 || (W.game === 'time' && c.order >= 0)) { a.carry = null; c.carriedBy = -1; collectCrystal(c, 'member'); }
      return W.hull;
    }
    if (!a.tgt || !a.tgt.alive || a.tgt.carriedBy >= 0) a.tgt = findCrystal(a, senseR(a) * 2.2);
    if (!a.tgt) return VERBS.graze(a, 0);
    if (len(a.tgt.x - a.x, a.tgt.y - a.y) < 5) { a.tgt.carriedBy = W.agents.indexOf(a); a.carry = { kind: 'crystal', c: a.tgt }; a.tgt = null; }
    return a.tgt;
  },
  trail(a, dt) {                         // ants: stigmergy — carry food home and LAY pheromone, so the colony converges on the best patch
    const k = fieldIdx(a.x, a.y);
    if (a.carry) {
      if (k >= 0) W.trail[k] = Math.min(6, W.trail[k] + 1.6 * dt * 4);
      const home = W.homes[0] || W.hull;
      if (len(home.x - a.x, home.y - a.y) < 12) { a.carry = null; W.stats.delivered++; econOnEat(a); const p = addPrism(home.x + (W.rnd() - .5) * 30, home.y + (W.rnd() - .5) * 30, ME, { built: 1 }); if (p) W.stats.built++; }
      return home;
    }
    if (a.tgt && a.tgt.alive && edible(a.tgt)) {
      if (len(a.tgt.x - a.x, a.tgt.y - a.y) < 4 + a.size) { killPrism(a.tgt, 'quiet'); W.stats.eaten++; a.carry = { kind: 'food' }; a.tgt = null; }
      return a.tgt;
    }
    a.tgt = findPrism(a, senseR(a) * 0.45, edible);
    if (a.tgt) return a.tgt;
    // no food in sight: climb the trail gradient (with noise), which is how the trail recruits
    const g = fieldGrad(W.trail, a.x, a.y), gm = len(g[0], g[1]);
    if (gm > 0.01) return { x: a.x + g[0] / gm * 30, y: a.y + g[1] / gm * 30 };
    return null;
  },
  sting(a, dt) {                         // wasps (and bees, as a second verb): attack a rival; bees die stinging, wasps retreat and return
    const s = SPECIES_BY_ID[a.sp];
    if (W.mood < 0.25 && a.state !== 'sting') return null;
    const r = (a.tgt && a.tgt.petals != null && a.tgt.alive) ? a.tgt : nearestRival(a.x, a.y, senseR(a) * (1 + W.mood * 2.5));
    if (!r) { a.tgt = null; return null; }
    a.tgt = r; a.state = 'sting';
    if (a.cd > 0) { a.cd -= dt; if (a.cd > 0.6) { const away = len(a.x - r.x, a.y - r.y) || 1; return { x: a.x + (a.x - r.x) / away * 40, y: a.y + (a.y - r.y) / away * 40 }; } }
    if (len(r.x - a.x, r.y - a.y) < 8 + a.size) {
      r.petalDebt += 0.12 * EL.dmg() * a.big; r.slow = Math.max(r.slow, 0.8 * EL.dmg()); W.stats.stings++;
      W.fx.push({ k: 'sting', x: r.x, y: r.y, t: 0 });
      if (s.suicideSting) hurtAgent(a, 99); else a.cd = 1.4;
    }
    return r;
  },
  drain(a, dt) {                         // mosquitoes: latch on and drink — the rival slows under the load and leaks petals
    const r = (a.tgt && a.tgt.petals != null && a.tgt.alive) ? a.tgt : nearestRival(a.x, a.y, senseR(a) * 2);
    if (!r) { a.tgt = null; a.state = 'idle'; return VERBS.graze(a, dt); }
    a.tgt = r;
    if (len(r.x - a.x, r.y - a.y) < 9 + a.size) {
      a.state = 'latch'; r.latched++; r.petalDebt += 0.018 * EL.dmg() * dt * 10;
      a.x = r.x + Math.cos(a.phase) * 7; a.y = r.y + Math.sin(a.phase) * 7; a.vx = r.vx; a.vy = r.vy; a.phase += dt * 3;
      if (W.rnd() < dt * 0.05) { a.state = 'idle'; a.tgt = null; hurtAgent(a, 0.5); }   // swatted
      return null;
    }
    a.state = 'idle';
    return r;
  },
  murmur(a) {                            // starlings: no work at all — the cloud IS the ability. Dense near the hull, it draws fire (decoy)
    const f = focusFor(a), t = W.t * (1.2 + W.mood * 2) + a.phase;
    const rad = 26 + 20 * Math.sin(t * 0.7 + a.phase * 3) + 40 * W.mood;
    return { x: f.x + Math.cos(t) * rad, y: f.y + Math.sin(t * 1.3) * rad };
  },
  ping(a, dt) {                          // bats: echolocate — every ping reveals hidden crystals and hidden mass nearby
    a.cd -= dt;
    if (a.cd <= 0) {
      a.cd = 1.6 + W.rnd() * 0.8; const rr = senseR(a) * 1.6;
      W.fx.push({ k: 'ping', x: a.x, y: a.y, t: 0, r: rr });
      for (const c of W.crystals) if (c.alive && c.hidden && !c.revealed && len(c.x - a.x, c.y - a.y) < rr) { c.revealed = true; W.stats.revealed++; }
      hashQuery(W.hash, a.x, a.y, rr, (p) => { if (p.hidden) { p.hidden = false; W.stats.revealed++; } });
    }
    const c = findCrystal(a, senseR(a) * 2.2);
    if (c) return VERBS.fetch(a);
    if (a.carry) return VERBS.fetch(a);
    const t = W.t * 0.9 + a.phase, f = focusFor(a);
    return { x: f.x + Math.cos(t) * 140, y: f.y + Math.sin(t * 0.8) * 140 };
  },
  flash(a, dt) {                         // fireflies: coupled oscillators (Kuramoto). In sync, one flash stuns every rival in range
    return VERBS.graze(a, dt);           // the phase coupling + burst live in stepSync, because they are a property of the SWARM
  },
  steal(a, dt) {                         // crows: mob a rival and pick its pocket (petals), or snatch a crystal
    const r = nearestRival(a.x, a.y, senseR(a) * (1 + W.mood * 2));
    if (r && (W.mood > 0.4 || r.marked > 0)) {
      a.tgt = r;
      if (len(r.x - a.x, r.y - a.y) < 9 + a.size) { a.cd -= dt; if (a.cd <= 0) { r.petalDebt += 0.2 * EL.dmg(); r.marked = 6; a.cd = 2.2; W.fx.push({ k: 'steal', x: r.x, y: r.y, t: 0 }); } }
      return r;
    }
    return VERBS.fetch(a);
  },
  frenzy(a, dt) {                        // piranhas: damage calls more damage — blood in the water multiplies speed AND bite
    if (!a.tgt || !a.tgt.alive || !hostile(a.tgt)) {
      // smell blood first: the gradient of recent kills
      const g = fieldGrad(W.blood, a.x, a.y), gm = len(g[0], g[1]);
      a.tgt = findPrism(a, senseR(a), (p) => hostile(p) && p.sh === 0);
      if (!a.tgt && gm > 0.02) return { x: a.x + g[0] / gm * 30, y: a.y + g[1] / gm * 30 };
    }
    if (!a.tgt) return null;
    const k = fieldIdx(a.x, a.y), blood = k >= 0 ? W.blood[k] : 0;
    if (len(a.tgt.x - a.x, a.tgt.y - a.y) < 4 + a.size) { a.cd -= dt * EL.dmg() * (1 + blood * 1.2); if (a.cd <= 0) { killPrism(a.tgt); W.stats.eaten++; econOnEat(a); a.tgt = null; a.cd = 0.8; } }
    return a.tgt;
  },
  network(a, dt) {                       // slime mold: explore, lay trail, and where the trail runs thick between two meals it hardens into a road of OUR mass
    const k = fieldIdx(a.x, a.y);
    if (k >= 0) { W.trail[k] = Math.min(8, W.trail[k] + dt * 1.2); if (W.trail[k] > 6.5 && W.rnd() < dt * 0.6) { const p = addPrism(a.x, a.y, ME, { built: 1 }); if (p) { W.stats.built++; W.trail[k] = 3; } } }
    const food = findPrism(a, senseR(a) * 0.8, edible);
    if (food) { if (len(food.x - a.x, food.y - a.y) < 5) { killPrism(food, 'quiet'); W.stats.eaten++; econOnEat(a); } return food; }
    // Physarum sensing: steer toward the stronger of three samples ahead
    const h = Math.atan2(a.vy, a.vx) || a.phase, s = 22, best = [-1, 0];
    for (const off of [-0.6, 0, 0.6]) { const kk = fieldIdx(a.x + Math.cos(h + off) * s, a.y + Math.sin(h + off) * s); const v = kk >= 0 ? W.trail[kk] : -1; if (v > best[0]) best[0] = v, best[1] = off; }
    const hh = h + best[1] + (W.rnd() - 0.5) * 0.8;
    return { x: a.x + Math.cos(hh) * 30, y: a.y + Math.sin(hh) * 30 };
  },
  web(a, dt) {                           // spiders: string webs between nearby anchors; a rival crossing a strand is SNARED
    a.cd -= dt;
    if (a.cd <= 0 && W.webs.length < 26) {
      a.cd = 3 + W.rnd() * 2;
      const anchor = findPrism(a, 60, (p) => p.dom === ME && !p.hidden);
      if (anchor) { const ang = W.rnd() * TAU, L = 40 + W.rnd() * 50 * EL.reach(); W.webs.push({ x0: anchor.x, y0: anchor.y, x1: anchor.x + Math.cos(ang) * L, y1: anchor.y + Math.sin(ang) * L, hp: 3 }); }
      else { const ang = W.rnd() * TAU, L = 40 + W.rnd() * 40; W.webs.push({ x0: a.x, y0: a.y, x1: a.x + Math.cos(ang) * L, y1: a.y + Math.sin(ang) * L, hp: 2 }); }
    }
    return null;
  },
  link(a) {                              // siphonophores: a chain of bodies, each following the one ahead — a living wall that eats shots
    const i = W.chain.indexOf(a);
    if (i <= 0) return null;                    // the head steers by the ordinary pull
    const ahead = W.chain[i - 1]; const dx = a.x - ahead.x, dy = a.y - ahead.y, d = len(dx, dy) || 1, gap = 9 * a.size / 3;
    return { x: ahead.x + dx / d * gap, y: ahead.y + dy / d * gap, tight: true };
  },
  plasma(a, dt) {                        // the Notion School: six fish bound by plasma. They hold a ring; the LINES between them cut
    const live = W.chain, i = live.indexOf(a), n = live.length; if (i < 0 || n < 2) return null;
    const f = focusFor(a), t = W.t * (0.6 + W.mood * 1.2), rad = 30 + 26 * EL.reach() + 40 * (W.spread || 0);
    return { x: f.x + Math.cos(t + i / n * TAU) * rad, y: f.y + Math.sin(t + i / n * TAU) * rad, tight: true };
  },
  merge(a, dt) {                         // tadpoles: graze, and when five crowd together they METAMORPHOSE into a frog (stepMerge)
    if (a.big > 1) {                     // a frog: a tongue that snaps crystals and prisms from range
      a.cd -= dt;
      const c = findCrystal(a, 90 * EL.reach());
      if (c && a.cd <= 0) { W.fx.push({ k: 'tongue', x: a.x, y: a.y, x1: c.x, y1: c.y, t: 0 }); collectCrystal(c, 'frog'); a.cd = 1.2; return null; }
      const p = findPrism(a, 70 * EL.reach(), edible);
      if (p && a.cd <= 0) { W.fx.push({ k: 'tongue', x: a.x, y: a.y, x1: p.x, y1: p.y, t: 0 }); killPrism(p); W.stats.eaten++; econOnEat(a); a.cd = 0.5; }
      return p;
    }
    return VERBS.graze(a, dt);
  },
  march(a, dt) { return VERBS.graze(a, dt); },
};

// ---- swarm-level behaviours (a property of the whole population, not of one member) ----
function stepSync(dt) {
  // Kuramoto: every firefly nudges its phase toward its neighbours'. Order parameter r in [0,1] is the HUD's sync.
  const live = W.agents.filter((a) => a.alive && a.verb === 'flash'); if (!live.length) { W.sync = 0; return; }
  let cx = 0, cy = 0; for (const a of live) { cx += Math.cos(a.phase); cy += Math.sin(a.phase); }
  const K = 1.6 * (1 + W.syncKick), psi = Math.atan2(cy, cx), r = len(cx, cy) / live.length;
  for (const a of live) { a.phase += dt * (2.4 + (a.gene.spd - 1) * 2 + K * r * Math.sin(psi - a.phase)); if (a.phase > TAU) { a.phase -= TAU; } }
  W.syncKick = Math.max(0, W.syncKick - dt * 0.5);
  W.sync = r;
  W.flashCd -= dt;
  if (r > 0.82 && W.flashCd <= 0 && Math.cos(psi) > 0.95) {
    W.flashCd = 2.5; W.stats.flashes++;
    let mx = 0, my = 0; for (const a of live) { mx += a.x; my += a.y; } mx /= live.length; my /= live.length;
    const rad = 70 + 14 * Math.sqrt(live.length) * EL.reach();
    W.fx.push({ k: 'flash', x: mx, y: my, t: 0, r: rad });
    for (const rv of W.rivals) if (rv.alive && len(rv.x - mx, rv.y - my) < rad) { rv.stun = Math.max(rv.stun, 1.2 * EL.dmg()); rv.petalDebt += 0.25 * EL.dmg(); }
  }
}
function stepLocustPhase(dt) {
  // locust.py: crowding pushes the gregarious factor g up, solitude lets it relax. Charge lowers the threshold.
  const thr = Math.max(2, 6 - P.elCharge * 0.35);
  for (const a of W.agents) {
    if (!a.alive || a.sp !== 'locust') continue;
    let n = 0; hashQuery(W.ahash, a.x, a.y, P.locustCrowd, () => { n++; });
    const want = n - 1 >= thr ? 1 : 0;
    a.g += (want - a.g) * Math.min(1, dt * (want ? 0.8 : 0.25));
  }
}
function stepMerge(dt) {
  // tadpoles: five within 14 u fuse into one frog (the frog keeps every hit point the five had; mass is conserved)
  W.mergeT -= dt; if (W.mergeT > 0) return; W.mergeT = 0.5;
  const need = 5;
  for (const a of W.agents) {
    if (!a.alive || a.big > 1 || a.verb !== 'merge') continue;
    const pals = []; hashQuery(W.ahash, a.x, a.y, 14, (b) => { if (b !== a && b.alive && b.big === 1 && b.verb === 'merge') pals.push(b); });
    if (pals.length >= need - 1) {
      let hp = a.hp; for (const b of pals.slice(0, need - 1)) { b.alive = false; hp += b.hp; }
      a.big = 3; a.size *= 2.2; a.hp = hp; W.stats.merges++;
      W.fx.push({ k: 'ring', x: a.x, y: a.y, t: 0, r: 30, col: '#7fe08a' }); toast('Metamorphosis: five tadpoles became a frog');
      return;
    }
  }
}
function stepWebs() {
  for (const w of W.webs) {
    if (w.hp <= 0) continue;
    for (const r of W.rivals) {
      if (!r.alive || r.stun > 0.2) continue;
      if (segDist(r.x, r.y, w.x0, w.y0, w.x1, w.y1) < 6) { r.stun = 1.6 * EL.dmg(); w.hp--; W.stats.snared++; W.fx.push({ k: 'snare', x: r.x, y: r.y, t: 0 }); }
    }
  }
  W.webs = W.webs.filter((w) => w.hp > 0);
}
function segDist(px, py, x0, y0, x1, y1) {
  const dx = x1 - x0, dy = y1 - y0, L2 = dx * dx + dy * dy || 1, t = clamp(((px - x0) * dx + (py - y0) * dy) / L2, 0, 1);
  return len(px - (x0 + dx * t), py - (y0 + dy * t));
}
function stepPlasma(dt) {
  // the lines between consecutive school members cut hostile mass and burn rivals that cross them
  const live = W.chain; if (live.length < 2 || live[0].verb !== 'plasma') return;
  for (let i = 0; i < live.length; i++) {
    const a = live[i], b = live[(i + 1) % live.length];
    if (live.length === 2 && i === 1) break;
    const L = len(b.x - a.x, b.y - a.y); if (L > 160 * EL.reach()) continue;     // the plasma snaps if the fish drift too far apart
    const mx = (a.x + b.x) / 2, my = (a.y + b.y) / 2;
    hashQuery(W.hash, mx, my, L / 2 + 4, (p) => { if (hostile(p) && p.sh === 0 && segDist(p.x, p.y, a.x, a.y, b.x, b.y) < 3.5) { killPrism(p); W.stats.eaten++; } });
    for (const r of W.rivals) if (r.alive && segDist(r.x, r.y, a.x, a.y, b.x, b.y) < 5) { r.petalDebt += 0.9 * dt * EL.dmg(); r.slow = 0.5; }
  }
}
function rebuildChain() {
  const v = W.sp.verb;
  W.chain = (v === 'link' || v === 'plasma') ? W.agents.filter((a) => a.alive && a.verb === v) : [];
}

function stepAgents(dt) {
  const sp = W.sp, gen = sp.genes;
  W.ahash = hashBuild(W.agents);
  rebuildChain();
  if (sp.id === 'locust' || W.agents.some((a) => a.sp === 'locust')) stepLocustPhase(dt);
  const aggr = W.mood;
  for (const a of W.agents) {
    if (!a.alive) continue;
    a.age += dt;
    const f = focusFor(a);
    // (1) the verb's goal, unless an order overrides it
    let goal = null, tight = false;
    if (W.order === 1) goal = null;                                              // Gather: everyone home to the hull
    else if (W.order === 2) { const r = nearestRival(a.x, a.y, 600); goal = r ? r : null; if (r && len(r.x - a.x, r.y - a.y) < 9) { r.petalDebt += 0.05 * dt * 10 * EL.dmg(); } }
    else if (W.cast && W.castT > 0 && !a.carry && a.state !== 'latch' && len(W.castPt.x - a.x, W.castPt.y - a.y) > 30) goal = W.castPt;   // fly to the landing, then work
    else {
      const verb = VERBS[a.verb] || VERBS.graze;
      // calm swarms keep to the focus and only work close to it; a rushing swarm works anywhere (B5 knob)
      goal = verb(a, dt);
      if (goal && goal.tight) tight = true;
      if (goal && !tight && !a.carry && a.state !== 'latch' && a.state !== 'sting') {
        const leash = (sp.leash * EL.reach()) * (0.6 + aggr * 2.4);
        if (len(goal.x - f.x, goal.y - f.y) > leash && W.ctl.id !== 'paint' && a.verb !== 'network' && a.verb !== 'trail') { goal = null; a.tgt = null; }
      }
    }
    if (a.state === 'latch') continue;
    // (2) the desired velocity
    let dx = 0, dy = 0;
    const spd = memberSpeed(a);
    if (goal) { const gx = goal.x - a.x, gy = goal.y - a.y, gd = len(gx, gy) || 1; const s = tight ? Math.min(spd * 1.6, gd * 4) : spd; dx += gx / gd * s; dy += gy / gd * s; }
    else {
      // no work: orbit the focus inside the species' leash, with a little wander
      const fx = f.x - a.x, fy = f.y - a.y, fd = len(fx, fy) || 1, home = sp.idleRing * (W.formation === 'blob' ? 0.6 : 1);
      const pull = (fd - home) / Math.max(20, home);
      dx += fx / fd * spd * clamp(pull, -0.6, 1); dy += fy / fd * spd * clamp(pull, -0.6, 1);
      dx += -fy / fd * spd * 0.35 * gen.orbit; dy += fx / fd * spd * 0.35 * gen.orbit;
      if (W.formation && W.ctl.id === 'formation') { const slot = formationSlot(a); dx = (slot.x - a.x) * 3; dy = (slot.y - a.y) * 3; }
    }
    // (3) the pheromone brush: the painted field is a road every member climbs
    if (W.ctl.id === 'paint' && !a.carry) { const g = fieldGrad(W.pher, a.x, a.y), gm = len(g[0], g[1]); if (gm > 0.004) { dx += g[0] / gm * spd * 0.9; dy += g[1] / gm * spd * 0.9; } }
    // (4) the sheepdog: members flee the hull, so you steer the herd by where you put your ship
    if (W.ctl.id === 'sheepdog') { const hx = a.x - W.hull.x, hy = a.y - W.hull.y, hd = len(hx, hy) || 1; if (hd < 110) { const k = (110 - hd) / 110; dx += hx / hd * spd * 2.4 * k; dy += hy / hd * spd * 2.4 * k; } }
    // (5) boids: separation always; alignment + cohesion by gene (a locust's alignment grows with its phase)
    let sx = 0, sy = 0, ax = 0, ay = 0, cx = 0, cy = 0, n = 0;
    const ali = gen.ali + (a.sp === 'locust' ? a.g * 1.4 : 0);
    hashQuery(W.ahash, a.x, a.y, 22, (b, d2) => {
      if (b === a) return; n++;
      const d = Math.sqrt(d2) || 0.01, minD = (a.size + b.size) * 1.4;
      if (d < minD) { sx += (a.x - b.x) / d * (minD - d) / minD; sy += (a.y - b.y) / d * (minD - d) / minD; }
      ax += b.vx; ay += b.vy; cx += b.x; cy += b.y;
    });
    if (n) {
      dx += sx * spd * 1.6 * gen.sep; dy += sy * spd * 1.6 * gen.sep;
      const am = len(ax, ay) || 1; dx += ax / am * spd * 0.5 * ali; dy += ay / am * spd * 0.5 * ali;
      cx /= n; cy /= n; const cm = len(cx - a.x, cy - a.y) || 1; dx += (cx - a.x) / cm * spd * 0.25 * gen.coh; dy += (cy - a.y) / cm * spd * 0.25 * gen.coh;
    }
    dx += (W.rnd() - 0.5) * spd * gen.wander; dy += (W.rnd() - 0.5) * spd * gen.wander;
    // (6) integrate with the species' agility, cap at speed
    const dm = len(dx, dy), cap = spd * (tight ? 1.6 : 1.15);
    if (dm > cap) { dx *= cap / dm; dy *= cap / dm; }
    const k = Math.min(1, dt * sp.agility);
    a.vx += (dx - a.vx) * k; a.vy += (dy - a.vy) * k;
    a.x += a.vx * dt; a.y += a.vy * dt;
    const rr = len(a.x, a.y); if (rr > R - 6) { a.x *= (R - 6) / rr; a.y *= (R - 6) / rr; a.vx *= -0.3; a.vy *= -0.3; }
  }
  if (W.agents.some((a) => a.alive && a.verb === 'flash')) stepSync(dt);
  if (W.agents.some((a) => a.alive && a.verb === 'merge')) stepMerge(dt);
  stepWebs(); stepPlasma(dt);
  if (W.agents.length > 600) W.agents = W.agents.filter((a) => a.alive || a.carry);
}
function formationSlot(a) {
  const live = W.agents.filter((b) => b.alive), i = live.indexOf(a), n = live.length, h = W.hull, hx = h.hx, hy = h.hy;
  if (W.formation === 'line') { const off = (i - n / 2) * 7; return { x: h.x - hx * 30 - hy * off, y: h.y - hy * 30 + hx * off }; }
  if (W.formation === 'wedge') { const row = Math.floor(Math.sqrt(i)), col = i - row * row - row; return { x: h.x + hx * (24 - row * 10) - hy * col * 8, y: h.y + hy * (24 - row * 10) + hx * col * 8 }; }
  if (W.formation === 'blob') { const ang = i * 2.39996, r = 5 * Math.sqrt(i); return { x: h.x + Math.cos(ang) * r, y: h.y + Math.sin(ang) * r }; }
  const ang = i / n * TAU + W.t * 0.3, r = 36 + n * 0.6; return { x: h.x + Math.cos(ang) * r, y: h.y + Math.sin(ang) * r };
}
