// siege_trace.js - the lab siege's own trajectories, for the game port's step-for-step match (substrate_harness group
// siege). Run by siege_fixture.py with the lab's flight/sim.js; prints JSON on stdout.
//
//   node siege_trace.js <dir holding sim.js + params.json>
//
// Each scenario runs the lab exactly as siege_eval.js does (bestiary world, scattered mass, trails, one pilot) until
// the step at which the siege starts a GATHER, snapshots the whole siege there, then records every step until the
// encounter's SCATTER ends: the pilot as the siege saw it (start of step), the phase events, and the members' positions
// every EVERY steps. From the snapshot on the lab draws no random numbers (only ROAM does), so the game's port,
// given the snapshot and the pilot track, must reproduce the encounter.
'use strict';
const path = require('path');
const E = require(path.join(process.argv[2], 'sim.js'));
const DT = 1 / 30, EVERY = 10;
const r3 = x => Math.round(x * 1000) / 1000, r2 = x => Math.round(x * 100) / 100;

function breakerSteer(sp, p, dt, DIRS) {
  // siege_eval.js breakerSteer, shell branch only (the snapshot starts at GATHER)
  const C = sp.C; let best = -2, bd = null;
  const U = []; for (let i = 0; i < sp.n; i++) if (sp.alive[i]) { const x = sp.pos[3 * i] - C[0], y = sp.pos[3 * i + 1] - C[1], z = sp.pos[3 * i + 2] - C[2], q = Math.max(Math.hypot(x, y, z), 1e-9); U.push(x / q, y / q, z / q); }
  const vn0 = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-9);
  for (const d of DIRS) {
    let mx = -1; for (let j = 0; j < U.length; j += 3) { const c = d[0] * U[j] + d[1] * U[j + 1] + d[2] * U[j + 2]; if (c > mx) mx = c; }
    const score = -mx + 0.05 * (d[0] * p.vel[0] + d[1] * p.vel[1] + d[2] * p.vel[2]) / vn0;
    if (score > best) { best = score; bd = d; }
  }
  const gx = C[0] + bd[0] * 1000, gy = C[1] + bd[1] * 1000, gz = C[2] + bd[2] * 1000;
  const wx = gx - p.pos[0], wy = gy - p.pos[1], wz = gz - p.pos[2], n = Math.max(Math.hypot(wx, wy, wz), 1e-9);
  const vn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-9), vx = p.vel[0] / vn, vy = p.vel[1] / vn, vz = p.vel[2] / vn;
  const ang = Math.acos(E.clamp(vx * wx / n + vy * wy / n + vz * wz / n, -1, 1)), kk = Math.min(1, p.turn * dt / Math.max(ang, 1e-6));
  let nx = vx + (wx / n - vx) * kk, ny = vy + (wy / n - vy) * kk, nz = vz + (wz / n - vz) * kk; const nn = Math.max(Math.hypot(nx, ny, nz), 1e-9);
  p.vel[0] = nx / nn * p.speed; p.vel[1] = ny / nn * p.speed; p.vel[2] = nz / nn * p.speed;
}

function scenario(policy, seed) {
  const DIRS = (() => { const n = 96, out = [], ga = Math.PI * (3 - Math.sqrt(5)); for (let i = 0; i < n; i++) { const y = 1 - 2 * (i + 0.5) / n, r = Math.sqrt(1 - y * y); out.push([Math.cos(ga * i) * r, y, Math.sin(ga * i) * r]); } return out; })();
  const ar = new E.Arena(seed, { world: 'bestiary' }); ar.scatterMass(3000); ar.enableTrails(15, 10);
  const PS = require(path.join(process.argv[2], 'params.json')).species;
  const sp = new E.SPECIES.siege(ar, PS.siege, { seed });
  const pl = ar.addPilot(policy === 'breaker' ? Object.assign(new E.Pilot('player', 140, 'breaker'), { turn: 2.0 })
    : policy === 'still' ? new E.Pilot('player', 0, 'still') : E.makePilot(policy));
  const K = sp.K, n = sp.n;
  let snap = null, steps = [], frames = [], done = false;
  for (let s = 0; s < 30 * 120 && !done; s++) {
    if (!snap && sp.phase === 'roam') {
      let live = 0, gx = 0, gy = 0, gz = 0;
      for (let i = 0; i < n; i++) if (sp.alive[i]) { live++; gx += sp.pos[3 * i]; gy += sp.pos[3 * i + 1]; gz += sp.pos[3 * i + 2]; }
      gx /= live; gy /= live; gz /= live;
      const pd = Math.hypot(pl.pos[0] - gx, pl.pos[1] - gy, pl.pos[2] - gz);
      if (sp.cool - DT <= 0 && pd < K.DETECT && live >= 12) {
        snap = { t: ar.t, R: ar.R, size: sp.size[0], pilot_radius: pl.radius, n, phase: sp.phase, tp: sp.tp, cool: sp.cool,
          C: sp.C.slice(), axis: sp.axis.slice(), Rs: sp.Rs, fill: sp.fill, rWall: sp.rWall || 0, last_bite: sp._lastBite,
          alive: Array.from(sp.alive), pos: Array.from(sp.pos), vel: Array.from(sp.vel) };
      }
    }
    if (snap) steps.push({ t: ar.t, p: pl.pos.slice(), v: pl.vel.slice() });
    const ev0 = sp.events.length;
    ar._src = 'siege'; sp.step(ar, DT);
    if (snap) {
      const st = steps[steps.length - 1];
      st.phase = sp.phase; st.ev = sp.events.slice(ev0).map(e => e[1]); st.fill = r3(sp.fill); st.Rs = r3(sp.Rs);
      st.bites = sp.strikes; st.bit = sp.bit.reduce((a, b) => a + b, 0); st.seated = sp.seated.reduce((a, b) => a + b, 0);
      if ((steps.length - 1) % EVERY === 0) frames.push({ k: steps.length - 1, pos: Array.from(sp.pos).map(r2), C: sp.C.map(r3) });
      if (sp.phase === 'roam' && steps.length > 1) done = true;
    }
    if (snap && policy === 'breaker' && (sp.phase === 'gather' || sp.phase === 'close' || sp.phase === 'hold')) breakerSteer(sp, pl, DT, DIRS);
    ar.step(DT);
  }
  return { policy, seed, K, snap, steps, frames, every: EVERY, dt: DT };
}

// the clock path (GATHER -> CLOSE -> HOLD -> DIVE, no breach): a pilot hovering where the shell forms
const out = { scenarios: [scenario('wander', 100), scenario('evader', 101), scenario('breaker', 102), scenario('hunter', 103), scenario('still', 104)] };
process.stdout.write(JSON.stringify(out));
