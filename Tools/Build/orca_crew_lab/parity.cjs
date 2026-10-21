#!/usr/bin/env node
// parity.cjs - holds the Crew of Four Lab's crew rules (Docs/Studios/CrewOfFourLab.html: turnBody, followFrame,
// rotateToward, steerToward, holdHorizon, shieldDirs, flyMissile, rapidStep, penLay, penFlush, penLanding, exposed as window.__lab.parity) to the SHIPPED C#
// OrcaCrewCore, step by step, over scripted inputs.
//
//   node Tools/Build/orca_crew_lab/parity.cjs
//
// 1. Runs trace.sh: the shipped C#, compiled from its real path, over the scripted scenarios (TraceDriver.cs).
// 2. Opens the lab page in headless Chromium and runs the SAME scenarios through the page's own functions. The page is
//    right-handed (three.js) and Unity left-handed, so every world vector crosses with x mirrored; hull-local values
//    (the shield's directions, the AI's stick) need no mirror.
// 3. Compares every step within TOL.
// 4. Negative controls: the page re-run with a planted change (horizonSmooth 3 -> 2.7, missileTurn 150 -> 140,
//    shieldOpenDeg 45 -> 44, rapidHeatTime 6 -> 5.7, rapidPenSpacing 5 -> 4.8) MUST fail. A gate that cannot see a few-percent dial change is not a gate.
// 5. The shipped asset (OrcaCrewConfig.asset) against the page's SHIPPED numbers, key by key, with a planted control.
'use strict';
const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const { chromium } = require('playwright');

const ROOT = path.resolve(__dirname, '../../..');
const PAGE = path.join(ROOT, 'Docs/Studios/CrewOfFourLab.html');
const ASSET = path.join(ROOT, 'Assets/_SO_Assets/VesselActions/OrcaCrew/OrcaCrewConfig.asset');
const TOL = 2e-3;
// asset field -> the lab SHIPPED key it came from (OrcaCrewConfigSO's tooltips name the same keys)
const ASSET_KEYS = {
  hullRadius: 'hullRadius', mountSmooth: 'mountSmooth', horizonSmooth: 'horizonSmooth', turretTurn: 'turretTurn',
  missileSpeed: 'missileSpeed', missileTurn: 'missileTurn', missileLife: 'missileLife', launchCooldown: 'launchCd',
  blastSeconds: 'blastTime', startMissiles: 'startMissiles', maxMissiles: 'maxMissiles',
  omniMissiles: 'omniMissiles', shieldBlocks: 'shieldBlocks', shieldMinRadius: 'shieldMinR', shieldLinger: 'shieldLinger',
  shieldSuper: 'shieldSuper', gunsOn: 'gunsOn', sniperCooldown: 'sniperCd', sniperRange: 'sniperRange', sniperConeDeg: 'sniperCone',
  sniperMinPathRadius: 'sniperMinR', fastGunRate: 'rapidRate', fastGunRoundSpeed: 'rapidSpeed', fastGunRoundSeconds: 'rapidLife',
  fastGunHeatSeconds: 'rapidHeatTime', fastGunCoolSeconds: 'rapidCoolTime', bombSpeed: 'bombSpeed', bombFuse: 'bombLife',
  fastGunPenRate: 'rapidPenRate', fastGunPenReach: 'rapidPenReach', fastGunPenSpacing: 'rapidPenSpacing', fastGunPenFill: 'rapidPenFill',
  fastGunPenGap: 'rapidPenGap', fastGunPenCap: 'rapidPenCap', sniperScopeFov: 'sniperFov', fastGunCamBack: 'rapidCamBack',
  fastGunCamUp: 'rapidCamUp', bombCamBack: 'bombCamBack', bombCamUp: 'bombCamUp', gunCamAim: 'gunCamAim',
  shieldMaxRadius: 'shieldMaxR', shieldOpenDeg: 'shieldOpenDeg', shieldCooldown: 'shieldCd', boostTank: 'boostTank',
  turretFov: 'turretFov', missileCamBack: 'missileCamBack', missileCamUp: 'missileCamUp',
};

// The element channels: an ElementalFloat (Min at rest, Max at level 10) holds the lab's 1 + k x level / 15, the line
// through 1 and 1 + 2k/3 at level 10. Each is [asset field, lab base key (null = a pure multiplier), lab k key].
const ELEMENTAL = [['boostRefill', 'boostRefill', 'kTime'], ['shieldSeconds', 'shieldTime', 'kMass'],
  ['blastRadius', 'blastR', 'kSpace'], ['missileScale', null, 'kCharge'], ['bombBlastRadius', 'bombBlastR', 'kSpace']];
// numbers the asset states in the game's own units: [asset field, its lab key, asset value -> lab value]
const DERIVED = [['sniperStripPerElement', 'sniperPetals', (v) => v * 40]];   // 0.1 of each of four elements = 4 petals

function csTrace() {
  const out = execFileSync('bash', [path.join(__dirname, 'trace.sh')], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'], env: Object.assign({}, process.env, { ORCA_RAPID: '30,6,5', ORCA_PEN_SPACING: '5' }) });
  return JSON.parse(out.trim().split('\n').pop());
}

// The page side: the same script as TraceDriver.cs, through window.__lab.parity, with x mirrored on the way in and out.
function pageTrace(par) {
  const F = window.__lab.parity, DT = 1 / 60, D2R = Math.PI / 180;
  const V = (x, y, z) => ({ x, y, z }), M = (v) => V(-v.x, v.y, v.z), A = (v) => [-v.x, v.y, v.z];
  const norm = (v) => { const l = Math.hypot(v.x, v.y, v.z) || 1; return V(v.x / l, v.y / l, v.z / l); };
  const add = (a, b) => V(a.x + b.x, a.y + b.y, a.z + b.z), scale = (a, s) => V(a.x * s, a.y * s, a.z * s), dot = (a, b) => a.x * b.x + a.y * b.y + a.z * b.z;
  const out = { turret: [], missile: [], steer: [], shield: [], rapid: [], pen: [], landing: [] };
  // A. the turrets and the pod
  const hull = { f: V(0, 0, 1), u: V(0, 1, 0) }, pod = { f: V(0, 0, 1), u: V(0, 1, 0) };
  const person = { aimW: M(norm(V(0.3, 0.1, 1))), upW: V(0, 1, 0) }, ai = { aimW: V(0, 0, 1), upW: V(0, 1, 0) };
  for (let i = 0; i < 600; i++) {
    F.turnBody(hull, 1.2 * Math.sin(i * 0.031) * DT, 0.8 * Math.cos(i * 0.023) * DT, 1.5 * Math.sin(i * 0.017) * DT);
    F.followFrame(pod, hull, DT, 0.35);
    const az = 0.4 * Math.sin(i * 0.05) * DT, el = 0.3 * Math.cos(i * 0.043) * DT;
    if (az || el) { const tmp = { f: person.aimW, u: person.upW }; F.turnBody(tmp, az, el, 0); person.aimW = tmp.f; person.upW = tmp.u; }
    const sway = F.holdHorizon(person, hull.u, DT, par.horizon);
    const want = M(norm(V(Math.sin(i * 0.02), 0.3 * Math.cos(i * 0.017), Math.cos(i * 0.02))));
    ai.aimW = F.rotateToward(ai.aimW, want, 160 * D2R * DT);
    out.turret.push([A(person.aimW), A(person.upW), sway, A(pod.f), A(pod.u), A(ai.aimW)]);
  }
  // B. the missile (OrcaMissile.Launch, then flyMissile)
  const aim = M(norm(V(0.2, 0.1, 1))), podUp = V(0, 1, 0);
  const u0 = add(podUp, scale(aim, -dot(podUp, aim)));
  const m = { pos: add(M(V(2, 5, -1)), scale(aim, 3)), f: aim, u: norm(u0), t: 0, stick: null };
  for (let i = 0; i < 300; i++) {
    m.stick = { yaw: Math.sin(i * 0.04), pitch: 0.5 * Math.cos(i * 0.03), roll: 0.2 * Math.sin(i * 0.07) };
    F.flyMissile(m, 180, par.missileTurn * D2R * DT, DT);
    out.missile.push([A(m.pos), A(m.f), A(m.u)]);
  }
  // C. the AI's stick (hull-local results: no mirror)
  for (let i = 0; i < 60; i++) {
    const f = { f: V(0, 0, 1), u: V(0, 1, 0) };
    F.turnBody(f, i * 0.37, Math.sin(i * 0.5) * 0.9, i * 0.21);
    const d = M(norm(V(Math.cos(i * 1.3), Math.sin(i * 0.7), Math.sin(i * 1.3 + 0.4))));
    const st = F.steerToward(f, d, 0, null);
    out.steer.push([st.yaw, st.pitch, st.roll]);
  }
  // D. the shield (hull-local)
  out.shield = F.shieldDirs(144, par.openDeg).map((d) => [d.x, d.y, d.z]);
  // E. the fast gun's rate and heat over a scripted trigger (held 7 s, off 1 s, held 9 s, off 6 s, held 2 s), at the page's
  // own P (rapidRate 30, rapidHeatTime 6, rapidCoolTime 5; the planted control moves rapidHeatTime)
  const P = window.__lab.P, saved = P.rapidHeatTime; P.rapidHeatTime = par.heat;
  const t = { heat: 0, hot: false, fireT: 0 };
  for (let i = 0; i < 25 * 60; i++) {
    const sec = i * DT, hold = sec < 7 || (sec >= 8 && sec < 17) || sec >= 23;
    const n = F.rapidStep(t, hold, DT);
    out.rapid.push([n, t.heat, t.hot ? 1 : 0]);
  }
  P.rapidHeatTime = saved;
  // F. the fast gun's pen: the same scripted landings as TraceDriver.cs (positions and directions mirrored in and out)
  const cross = (a, b) => V(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
  const st = { last: null, pend: null, seen: -1e9 };
  for (let i = 0; i < 400; i++) {
    const now = Math.fround(Math.fround(i * 0.1) + (i >= 150 ? 1 : 0)), sw = i * 1.5 + Math.floor(i / 37) * 25;
    const at = V(120 * Math.sin(sw * 0.02), 40 * Math.cos(sw * 0.031), 120 + 30 * Math.sin(sw * 0.017));
    const aim = norm(at), across = norm(add(cross(V(0, 1, 0), aim), V(0, 1, 0)));   // Unity-frame inputs, mirrored below
    const row = [];
    const dot = F.penFlush(st, now, 0.35);
    row.push(dot ? 1 : 0, dot ? A(dot.pos) : [0, 0, 0], dot ? A(dot.f) : [0, 0, 0]);
    const marks = F.penLay(st, M(at), M(aim), M(across), now, par.penSpacing, 0.35, 2);
    row.push(marks.length); for (const q of marks) row.push(A(q.pos), A(q.f));
    out.pen.push(row);
  }
  for (let k = 0; k <= 20; k++) { const l = F.penLanding(50 + k * 20, 4.5, 120); out.landing.push([l.d, l.t]); }
  return out;
}

function flat(x, acc = []) { if (Array.isArray(x)) for (const y of x) flat(y, acc); else acc.push(x); return acc; }
function compare(cs, pg) {
  const bad = [];
  for (const k of ['turret', 'missile', 'steer', 'shield', 'rapid', 'pen', 'landing']) {
    if (cs[k].length !== pg[k].length) { bad.push(`${k}: ${cs[k].length} steps in C#, ${pg[k].length} on the page`); continue; }
    for (let i = 0; i < cs[k].length; i++) {
      const a = flat(cs[k][i]), b = flat(pg[k][i]);
      let worst = 0; for (let j = 0; j < a.length; j++) worst = Math.max(worst, Math.abs(a[j] - b[j]));
      if (!(worst <= TOL)) { bad.push(`${k} step ${i}: off by ${worst.toExponential(2)}`); break; }
    }
  }
  return bad;
}

function readAsset() {
  const out = {};
  if (!fs.existsSync(ASSET)) return null;
  let block = null;
  for (const line of fs.readFileSync(ASSET, 'utf8').split('\n')) {
    const m = /^  (\w+): (-?[\d.]+)\s*$/.exec(line); if (m) { out[m[1]] = Number(m[2]); block = null; continue; }
    const b = /^  (\w+):\s*$/.exec(line); if (b) { block = b[1]; continue; }
    const n = /^    (\w+): (-?[\d.]+)\s*$/.exec(line); if (n && block) out[block + '.' + n[1]] = Number(n[2]);
  }
  return out;
}
function assetDiff(asset, shipped) {
  const bad = [];
  for (const [field, key] of Object.entries(ASSET_KEYS)) {
    if (!(field in asset)) { bad.push(`${field} missing from the asset`); continue; }
    if (Math.abs(asset[field] - shipped[key]) > 1e-6) bad.push(`${field} ${asset[field]} != lab ${key} ${shipped[key]}`);
  }
  for (const [field, baseKey, kKey] of ELEMENTAL) {
    const base = baseKey ? shipped[baseKey] : 1, full = base * (1 + 2 * shipped[kKey] / 3);
    if (!(field + '.Min' in asset)) { bad.push(`${field} (ElementalFloat) missing from the asset`); continue; }
    if (Math.abs(asset[field + '.Min'] - base) > 1e-4) bad.push(`${field}.Min ${asset[field + '.Min']} != lab ${baseKey || '1'} ${base}`);
    if (Math.abs(asset[field + '.Max'] - full) > 1e-4) bad.push(`${field}.Max ${asset[field + '.Max']} != lab ${baseKey || '1'} x (1 + 2 ${kKey} / 3) = ${full.toFixed(5)}`);
    if (asset[field + '.Enabled'] !== 1) bad.push(`${field} is not enabled (the element would change nothing)`);
  }
  for (const [field, key, toLab] of DERIVED) {
    if (!(field in asset)) { bad.push(`${field} missing from the asset`); continue; }
    if (Math.abs(toLab(asset[field]) - shipped[key]) > 1e-6) bad.push(`${field} ${asset[field]} -> ${toLab(asset[field])} != lab ${key} ${shipped[key]}`);
  }
  return bad;
}

(async () => {
  let failed = false;
  const cs = csTrace();
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const errors = []; page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('file://' + PAGE); await page.waitForTimeout(600);
  const run = (par) => page.evaluate(pageTrace, par);
  const base = { horizon: 3, missileTurn: 150, openDeg: 45, heat: 6, penSpacing: 5 };
  const diff = compare(cs, await run(base));
  if (diff.length) { failed = true; console.log('FAIL core vs page:\n  ' + diff.join('\n  ')); }
  else console.log(`PASS core vs page: ${cs.turret.length} turret steps, ${cs.missile.length} missile steps, ${cs.steer.length} AI sticks, ${cs.shield.length} shield directions, ${cs.rapid.length} fast-gun steps, ${cs.pen.length} pen landings, ${cs.landing.length} pen reaches within ${TOL}`);
  for (const [name, plant] of [['horizonSmooth 3 -> 2.7', { horizon: 2.7 }], ['missileTurn 150 -> 140', { missileTurn: 140 }], ['shieldOpenDeg 45 -> 44', { openDeg: 44 }], ['rapidHeatTime 6 -> 5.7', { heat: 5.7 }], ['rapidPenSpacing 5 -> 4.8', { penSpacing: 4.8 }]]) {
    const d = compare(cs, await run(Object.assign({}, base, plant)));
    if (d.length) console.log(`PASS planted control caught (${name}): ${d[0]}`);
    else { failed = true; console.log(`FAIL planted control NOT caught (${name})`); }
  }
  const shipped = await page.evaluate(() => window.__lab.SHIPPED);
  const asset = readAsset();
  if (!asset) { failed = true; console.log('FAIL no asset at ' + path.relative(ROOT, ASSET)); }
  else {
    const d = assetDiff(asset, shipped);
    if (d.length) { failed = true; console.log('FAIL asset vs page:\n  ' + d.join('\n  ')); }
    else console.log(`PASS asset vs page: ${Object.keys(ASSET_KEYS).length} fields and ${ELEMENTAL.length} element channels equal the lab's SHIPPED`);
    const planted = Object.assign({}, asset, { missileSpeed: asset.missileSpeed + 5 });
    if (assetDiff(planted, shipped).length) console.log('PASS planted asset control caught (missileSpeed +5)');
    else { failed = true; console.log('FAIL planted asset control NOT caught'); }
    const plantedK = Object.assign({}, asset, { 'blastRadius.Max': asset['blastRadius.Max'] * 1.05 });
    if (assetDiff(plantedK, shipped).length) console.log('PASS planted element control caught (blastRadius.Max +5%)');
    else { failed = true; console.log('FAIL planted element control NOT caught'); }
  }
  if (errors.length) { failed = true; console.log('FAIL page errors: ' + errors.join('; ')); }
  await browser.close();
  console.log(failed ? 'FAIL' : 'PASS');
  process.exit(failed ? 1 : 0);
})();
