// ===================================================================================================================
// 70_siege.js - the SIEGE (terrifying): a JS-original species, no Python reference (so it is excluded from the
// fidelity gate via JS_ORIGINAL; it is scored by siege_eval.js instead). The design target the lead named
// (DISCOVERIES "the lead's verdict on the emotion viewer"): "being surrounded by them and having them all dive in at
// once". Shape = the converge wave: emotion archetype TerSwarm, the bestiary pack's ring-and-strike (closure quorum),
// the locust's dense phase.
//
// One swarm, one rhythm, a phase machine shared by every member:
//   ROAM    - a loose cloud stalks you at ~STALK u. Harmless, dim.
//   GATHER  - you came within DETECT: the cloud streams round you onto a SHELL of radius R0 centred on a point C that
//             trails you (it moves slower than you). Members flow ALONG the shell surface to their slots, so the side
//             nearest the cloud fills first and the far side stays OPEN: the gaps are the slots nobody holds yet.
//             Intent 0.15..0.45 (no bites, members swerve round you).
//   CLOSE   - fill reached the quorum (or GATHER timed out): the shell shrinks R0 -> R1 and C nearly stops. Intent
//             0.55 -> 0.8 (the glow: the telegraph starts here). Any member within LUNGE of you lunges and bites (the
//             wall is solid); the unfilled gaps are not. Fly out through a gap -> ESCAPED.
//   HOLD    - the shell sits at R1 for T_HOLD, intent 0.8 -> 1 (pulsing), gaps still filling.
//   DIVE    - every member at once sprints at your predicted position. Contact = a bite (a danger contact through
//             arena.hit, attributed by the caller's ar._src; the stakes burn petals). Each member bites at most once.
//   SCATTER - burst outward, then ROAM again until the cooldown ends. A repeating rhythm, ~15-20 s a cycle.
// Escape (you are outside the shell by ESC_MARGIN during GATHER/CLOSE/HOLD) also ends in SCATTER, with no bite.
// Dead members (rammed) regrow in ROAM by eating a loose prism next to the cloud, so mass is conserved.
// ===================================================================================================================
const SIEGE_DEFAULTS = {
  N: 150, SIZE: 3.4, BODY: 4,
  STALK: 430, DETECT: 640, CRUISE: 120, SPRINT: 270, DIVE: 330,
  R0: 300, R1: 130, ESC_MARGIN: 45, LUNGE: 35, SEAT_COS: 0.97, CORONA: 1.35,
  QUORUM: 0.5, T_GATHER: 3.0, T_GATHER_MAX: 5.0, P_GATHER: 0.6, P_CLOSE: 0.8, T_CLOSE: 2.6, T_HOLD: 1.3, T_DIVE: 1.6, T_SCATTER: 1.6, T_COOL: 10.0, BITE_GAP: 0.25, T_COOL_ESC: 6.0,
  FOLLOW_GATHER: 100, LEAD: 0.6, FOLLOW_CLOSE: 25, REGROW_EVERY: 1.0,
};
const SIEGE_PHASES = ['roam', 'gather', 'close', 'hold', 'dive', 'scatter'];

function Siege(arena, P, o) {
  o = o || {}; this.name = 'siege';
  const K = this.K = Object.assign({}, SIEGE_DEFAULTS, (P && P.const) || {}, o.K || {});
  const n = o.n || K.N;
  const centre = o.centre || arena.ball(0.35 * arena.R, 0.7 * arena.R);
  Herd.call(this, arena, n, centre, 70, K.SIZE, K.BODY);
  this.aspect = 1.6;
  this.heading = new Float64Array(3 * n);
  this.slot = new Float64Array(3 * n);      // unit slot direction on the shell for each member
  this.seated = new Uint8Array(n); this.bit = new Uint8Array(n);
  this.phase = 'roam'; this.tp = 0; this.axis = [0, 0, 1]; this.prog = 0; this.cool = 2.0; this.C = [0, 0, 0]; this.Rs = K.R0; this.fill = 0;
  this.encounters = 0; this.escapes = 0; this.dives = 0; this.diveBites = 0; this.wallBites = 0; this.strikes = 0; this.breaches = 0; this._lastBite = -1e9;
  this.events = [];                          // [t, what] - 'gather', 'close', 'hold', 'dive', 'escape', 'scatter'
  this._regrow = 0; this.ablate = o.ablate || null;
  this.buildFib();
}
Siege.prototype = Object.create(Herd.prototype); Siege.prototype.constructor = Siege;

Siege.prototype.buildFib = function () {
  const n = this.n, F = this.fib = new Float64Array(3 * n), ga = Math.PI * (3 - Math.sqrt(5));
  for (let i = 0; i < n; i++) {
    const y = 1 - 2 * (i + 0.5) / n, r = Math.sqrt(1 - y * y), th = ga * i;
    F[3 * i] = Math.cos(th) * r; F[3 * i + 1] = y; F[3 * i + 2] = Math.sin(th) * r;
  }
};
/** slot assignment at GATHER: near side of the shell (toward the cloud) to the members nearest it, banded by polar
 *  angle round the cloud->C axis and ordered by azimuth inside a band, so paths do not cross the middle. */
Siege.prototype.assignSlots = function () {
  const n = this.n, X = this.pos, A = this.alive, C = this.C, F = this.fib;
  let ax = 0, ay = 0, az = 0, m = 0;
  for (let i = 0; i < n; i++) if (A[i]) { ax += X[3 * i] - C[0]; ay += X[3 * i + 1] - C[1]; az += X[3 * i + 2] - C[2]; m++; }
  const an = Math.max(Math.hypot(ax, ay, az), 1e-9); ax /= an; ay /= an; az /= an;
  this.axis = [ax, ay, az];
  // a perpendicular frame for azimuth
  let ux = 0, uy = 1, uz = 0; if (Math.abs(ay) > 0.9) { ux = 1; uy = 0; }
  let bx = uy * az - uz * ay, by = uz * ax - ux * az, bz = ux * ay - uy * ax; const bn = Math.hypot(bx, by, bz); bx /= bn; by /= bn; bz /= bn;
  const cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
  const key = (x, y, z) => { const d = x * ax + y * ay + z * az; return [d, Math.atan2(x * cx + y * cy + z * cz, x * bx + y * by + z * bz)]; };
  const live = []; for (let i = 0; i < n; i++) if (A[i]) live.push(i);
  // slots: the m Fibonacci directions with the best spread are simply the first m of a fresh m-point lattice
  const S = [];
  const ga = Math.PI * (3 - Math.sqrt(5));
  for (let s = 0; s < live.length; s++) {
    const y = 1 - 2 * (s + 0.5) / live.length, r = Math.sqrt(1 - y * y), th = ga * s;
    const dx = Math.cos(th) * r, dy = y, dz = Math.sin(th) * r; const [d, az2] = key(dx, dy, dz); S.push([d, az2, dx, dy, dz]);
  }
  const M = live.map(i => { const dx = X[3 * i] - C[0], dy = X[3 * i + 1] - C[1], dz = X[3 * i + 2] - C[2], dn = Math.max(Math.hypot(dx, dy, dz), 1e-9); const [d, az2] = key(dx / dn, dy / dn, dz / dn); return [d - Math.hypot(dx, dy, dz) / 2000, az2, i]; });
  S.sort((p, q) => q[0] - p[0]); M.sort((p, q) => q[0] - p[0]);
  const B = 8, per = Math.ceil(live.length / B);
  for (let b0 = 0; b0 < live.length; b0 += per) {
    const ss = S.slice(b0, b0 + per).sort((p, q) => p[1] - q[1]), mm = M.slice(b0, b0 + per).sort((p, q) => p[1] - q[1]);
    for (let k = 0; k < ss.length; k++) { const i = mm[k][2]; this.slot[3 * i] = ss[k][2]; this.slot[3 * i + 1] = ss[k][3]; this.slot[3 * i + 2] = ss[k][4]; }
  }
};
Siege.prototype.go = function (phase, t) { this.phase = phase; this.tp = 0; this.events.push([+t.toFixed(2), phase]); };
Siege.prototype.phaseIntent = function () {
  const K = this.K, tp = this.tp;
  switch (this.phase) {
    case 'gather': return 0.15 + 0.3 * this.fill;
    case 'close': return 0.55 + 0.25 * Math.min(1, tp / K.T_CLOSE);
    case 'hold': return Math.min(1, 0.8 + 0.2 * tp / K.T_HOLD) * (0.9 + 0.1 * Math.sin(tp * 18));
    case 'dive': return 1;
    default: return 0;
  }
};
Siege.prototype.act = function (arena, dt) {
  const K = this.K, n = this.n, A = this.alive, X = this.pos, V = this.vel, des = this.des, p = arena.pilots[0];
  if (!p) return;
  let live = 0, gx = 0, gy = 0, gz = 0;
  for (let i = 0; i < n; i++) if (A[i]) { live++; gx += X[3 * i]; gy += X[3 * i + 1]; gz += X[3 * i + 2]; }
  if (!live) return;
  gx /= live; gy /= live; gz /= live;
  this.tp += dt; this.cool -= dt;
  const C = this.C, t = arena.t;
  const pd = Math.hypot(p.pos[0] - gx, p.pos[1] - gy, p.pos[2] - gz);
  const dC = () => Math.hypot(p.pos[0] - C[0], p.pos[1] - C[1], p.pos[2] - C[2]);
  // ---------------------------------------------------------------- the phase machine
  const ph = this.phase;
  if (ph === 'roam') {
    if (this.cool <= 0 && pd < K.DETECT && live >= 12) {
      C[0] = p.pos[0]; C[1] = p.pos[1]; C[2] = p.pos[2]; this.Rs = K.R0; this.fill = 0; this.bit.fill(0);
      this.assignSlots(); this.encounters++; this.go('gather', t);
    }
  } else if (ph === 'gather' || ph === 'close' || ph === 'hold') {
    const follow = ph === 'gather' ? K.FOLLOW_GATHER : ph === 'close' ? K.FOLLOW_CLOSE : 0;
    const ox = p.pos[0] - C[0], oy = p.pos[1] - C[1], oz = p.pos[2] - C[2], on = Math.hypot(ox, oy, oz);
    if (on > 1e-6) { const s = Math.min(on, follow * dt) / on; C[0] += ox * s; C[1] += oy * s; C[2] += oz * s; }
    // the shell must fit in the cell: its centre stays inside the membrane by most of its radius
    const cr = Math.hypot(C[0], C[1], C[2]), cmax = 0.92 * this.R - 0.6 * this.Rs;
    if (cr > cmax) { const s = cmax / cr; C[0] *= s; C[1] *= s; C[2] *= s; }
    if (ph === 'close') this.Rs = K.R0 + (K.R1 - K.R0) * smooth01(this.tp / K.T_CLOSE);
    if (dC() > Math.max(this.Rs, this.rWall || 0) + K.ESC_MARGIN) { this.escapes++; this.go('scatter', t); this.events[this.events.length - 1][1] = 'escape'; this.cool = K.T_COOL_ESC; }
    else if (ph === 'gather' && ((this.tp >= K.T_GATHER && this.fill >= K.QUORUM) || this.tp > K.T_GATHER_MAX)) this.go('close', t);
    else if (ph === 'close' && this.tp >= K.T_CLOSE) this.go('hold', t);
    else if (ph === 'hold' && this.tp >= K.T_HOLD) { this.dives++; this.go('dive', t); }
  } else if (ph === 'dive') {
    if (this.tp >= K.T_DIVE) { this.go('scatter', t); this.cool = K.T_COOL; }
  } else if (ph === 'scatter') {
    if (this.tp >= K.T_SCATTER) this.go('roam', t);
  }
  const phase = this.phase, it = this.phaseIntent(), shell = phase === 'gather' || phase === 'close' || phase === 'hold';
  // the IRIS: the shell fills from the cloud's side (axis) outward; the far cap stays open and shrinks to nothing at
  // the end of HOLD. prog = filled share of the sphere's area; front = cos of the filled cap's polar angle.
  const prog = phase === 'gather' ? K.P_GATHER * Math.min(1, this.tp / K.T_GATHER)
    : phase === 'close' ? K.P_GATHER + (K.P_CLOSE - K.P_GATHER) * Math.min(1, this.tp / K.T_CLOSE)
    : phase === 'hold' ? K.P_CLOSE + (1 - K.P_CLOSE) * Math.min(1, this.tp / K.T_HOLD) : 1;
  this.prog = prog;
  const front = 1 - 2 * prog, ax = this.axis[0], ay = this.axis[1], az = this.axis[2];
  const sinF = Math.sqrt(Math.max(0, 1 - front * front));
  // ---------------------------------------------------------------- desired velocities
  const tau = 0.35, prx = p.pos[0] + p.vel[0] * tau, pry = p.pos[1] + p.vel[1] * tau, prz = p.pos[2] + p.vel[2] * tau;
  let seated = 0, rw = 0, nw = 0;
  for (let i = 0; i < n; i++) {
    if (!A[i]) continue;
    const xi = X[3 * i], yi = X[3 * i + 1], zi = X[3 * i + 2];
    const dx = p.pos[0] - xi, dy = p.pos[1] - yi, dz = p.pos[2] - zi, d = Math.max(Math.hypot(dx, dy, dz), 1e-9);
    let wx, wy, wz, sp;
    if (shell) {
      // flow along the shell toward the slot: step <= ~35 deg round the surface per target, never through the middle
      let ux = xi - C[0], uy = yi - C[1], uz = zi - C[2]; const un = Math.max(Math.hypot(ux, uy, uz), 1e-9); ux /= un; uy /= un; uz /= un;
      let sx = this.slot[3 * i], sy = this.slot[3 * i + 1], sz = this.slot[3 * i + 2];
      const ca = sx * ax + sy * ay + sz * az;
      const held = ca < front && this.ablate !== 'noiris';
      if (held) {   // not released yet: hover just OUTSIDE the iris rim at the same azimuth (a corona, not a wall)
        let px = sx - ca * ax, py = sy - ca * ay, pz = sz - ca * az; const pn = Math.hypot(px, py, pz);
        if (pn > 1e-6) { px /= pn; py /= pn; pz /= pn; sx = ax * front + px * sinF; sy = ay * front + py * sinF; sz = az * front + pz * sinF; }
      }
      const c = ux * sx + uy * sy + uz * sz;
      let tx = sx, ty = sy, tz = sz;
      if (c < 0.82) {
        let qx = sx - c * ux, qy = sy - c * uy, qz = sz - c * uz; let qn = Math.hypot(qx, qy, qz);
        if (qn < 1e-6) { qx = -uz; qy = 0; qz = ux; qn = Math.max(Math.hypot(qx, qy, qz), 1e-9); }
        tx = ux + 0.7 * qx / qn; ty = uy + 0.7 * qy / qn; tz = uz + 0.7 * qz / qn;
        const tn = Math.hypot(tx, ty, tz); tx /= tn; ty /= tn; tz /= tn;
      }
      const R = held ? this.Rs * K.CORONA : this.Rs;
      const ex = C[0] + tx * R - xi, ey = C[1] + ty * R - yi, ez = C[2] + tz * R - zi, en = Math.max(Math.hypot(ex, ey, ez), 1e-9);
      // in the wall = at its place ON THE SPHERE (angle only: the radius lags while the shell shrinks)
      this.seated[i] = !held && c > K.SEAT_COS ? 1 : 0; seated += this.seated[i];
      if (this.seated[i]) { rw += un; nw++; }
      sp = Math.min(K.SPRINT, 40 + 3.0 * en);
      wx = ex / en * sp; wy = ey / en * sp; wz = ez / en * sp;
      if (phase === 'gather' && d < 70) { const k = (70 - d) / 70 * K.SPRINT; wx -= dx / d * k; wy -= dy / d * k; wz -= dz / d * k; }   // swerve, no bite
      // the wall is solid: once the glow is up (>= 0.4 s into CLOSE) a member within LUNGE lunges at you
      if (phase !== 'gather' && !(phase === 'close' && this.tp < 0.4) && d < K.LUNGE && !this.bit[i] && this.seated[i]) { wx = dx / d * K.DIVE; wy = dy / d * K.DIVE; wz = dz / d * K.DIVE; }
    } else if (phase === 'dive') {
      if (this.bit[i]) { wx = -dx / d * K.SPRINT; wy = -dy / d * K.SPRINT; wz = -dz / d * K.SPRINT; }
      else {
        const qx = prx - xi, qy = pry - yi, qz = prz - zi, qn = Math.max(Math.hypot(qx, qy, qz), 1e-9);
        const dv = this.ablate === 'nosync' ? K.DIVE * (0.5 + (i % 7) / 6) : K.DIVE;
        wx = qx / qn * dv; wy = qy / qn * dv; wz = qz / qn * dv;
      }
    } else if (phase === 'scatter') {
      wx = -dx / d * K.SPRINT; wy = -dy / d * K.SPRINT; wz = -dz / d * K.SPRINT;
    } else {   // roam: stalk at STALK from the pilot, loose cloud, slow drift
      // the cloud works round AHEAD of your line (the pack's fan-ahead), so the shell forms across your path and its
      // open cap - the way out - is behind you or to the side, never simply straight on
      const pv = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-9);
      let rx = gx - p.pos[0], ry = gy - p.pos[1], rz = gz - p.pos[2], rn = Math.max(Math.hypot(rx, ry, rz), 1e-9);
      rx = rx / rn + K.LEAD * p.vel[0] / pv; ry = ry / rn + K.LEAD * p.vel[1] / pv; rz = rz / rn + K.LEAD * p.vel[2] / pv; rn = Math.max(Math.hypot(rx, ry, rz), 1e-9);
      const qx = p.pos[0] + rx / rn * K.STALK, qy = p.pos[1] + ry / rn * K.STALK, qz = p.pos[2] + rz / rn * K.STALK;
      const ax = qx - gx + (gx - xi) * 0.8 + this.rng.normal(0, 30), ay = qy - gy + (gy - yi) * 0.8 + this.rng.normal(0, 30), az = qz - gz + (gz - zi) * 0.8 + this.rng.normal(0, 30);
      const an = Math.max(Math.hypot(ax, ay, az), 1e-9); sp = Math.min(K.CRUISE * 1.6, 25 + an * 0.6);
      wx = ax / an * sp; wy = ay / an * sp; wz = az / an * sp;
    }
    des[3 * i] = wx; des[3 * i + 1] = wy; des[3 * i + 2] = wz;
  }
  if (shell) { this.fill = seated / live; this.rWall = nw ? rw / nw : this.Rs; }
  // separation (radius 14) through the neighbour hash; strong enough that the shell reads as a lattice with holes
  this.buildNbr(16);
  for (let i = 0; i < n; i++) if (A[i]) this.sepInto(i, 14, des, phase === 'dive' ? 20 : 90);
  const acc = phase === 'dive' ? 900 : 520;
  for (let i = 0; i < n; i++) if (A[i]) this.steerTo(V, i, des[3 * i], des[3 * i + 1], des[3 * i + 2], acc * dt);
  this.containAll();
  // heading: everyone watches you from GATHER on; otherwise faces its motion
  for (let i = 0; i < n; i++) {
    let hx, hy, hz;
    if (phase !== 'roam' && phase !== 'scatter') { hx = p.pos[0] - X[3 * i]; hy = p.pos[1] - X[3 * i + 1]; hz = p.pos[2] - X[3 * i + 2]; }
    else { hx = V[3 * i]; hy = V[3 * i + 1]; hz = V[3 * i + 2]; }
    const hn = Math.max(Math.hypot(hx, hy, hz), 1e-9);
    this.heading[3 * i] = hx / hn; this.heading[3 * i + 1] = hy / hn; this.heading[3 * i + 2] = hz / hn;
    this.intent[i] = A[i] ? (this.bit[i] ? Math.min(it, 0.3) : it) : 0;
  }
  // ---------------------------------------------------------------- bites (danger contacts) and rams
  // the wall is a web: brush within LUNGE of a wall member once the glow is up and EVERY member dives at once
  if (((phase === 'close' && this.tp >= 0.4) || phase === 'hold') && this.ablate !== 'nobreach') {
    for (let i = 0; i < n; i++) {
      if (!A[i] || !this.seated[i]) continue;
      if (Math.hypot(X[3 * i] - p.pos[0], X[3 * i + 1] - p.pos[1], X[3 * i + 2] - p.pos[2]) < K.LUNGE) {
        this.dives++; this.breaches++; this.go('dive', arena.t); this.events[this.events.length - 1][1] = 'breach'; break;
      }
    }
  }
  if (this.phase === 'dive' || (phase === 'close' && this.tp >= 0.4) || phase === 'hold') {
    const r0 = p.radius;
    for (let i = 0; i < n; i++) {
      if (!A[i] || this.bit[i]) continue;
      const d = Math.hypot(X[3 * i] - p.pos[0], X[3 * i + 1] - p.pos[1], X[3 * i + 2] - p.pos[2]);
      if (d < r0 + this.size[i] + 4) {
        this.bit[i] = 1;
        // one danger contact per BITE_GAP: the members arriving in the same instant are one bite, not fifty
        if (t - this._lastBite < K.BITE_GAP) continue;
        this._lastBite = t; this.strikes++;
        if (this.phase === 'dive') this.diveBites++; else this.wallBites++;
        arena.hit(p, 'bite');

      }
    }
  }
  // committed members (CLOSE..SCATTER) cannot be rammed: a dive must never be a crystal fountain
  const vuln = this._vuln || (this._vuln = new Uint8Array(n)), committed = this.phase !== 'roam' && this.phase !== 'gather';   // incl. SCATTER: they burst out past you
  for (let i = 0; i < n; i++) vuln[i] = committed ? 0 : 1;
  this.hunterContacts(arena, 4, vuln);
  if (phase === 'roam') this.regrow(arena, dt, gx, gy, gz);
  // what a pilot reads as the threat: the WALL (seated members) while a shell stands - the gaps are where it is not;
  // the whole swarm otherwise
  let wall = 0; if (shell) for (let i = 0; i < n; i++) wall += A[i] && this.seated[i];
  // a ROAMING cloud is not yet a threat (intent 0, it never bites), so it publishes none
  const none = this._none || (this._none = new Uint8Array(n));
  this.publish(arena, phase === 'roam' ? none : shell && wall >= 6 ? this.seated : null);
};
/** a rammed member regrows in ROAM by eating one loose prism near the cloud (conserved: its body IS that volume). */
Siege.prototype.regrow = function (arena, dt, gx, gy, gz) {
  this._regrow += dt; if (this._regrow < this.K.REGROW_EVERY) return; this._regrow = 0;
  let j = -1; for (let i = 0; i < this.n; i++) if (!this.alive[i]) { j = i; break; }
  if (j < 0) return;
  const c = arena.massNear(gx, gy, gz, 160);
  for (let s = 0; s < c; s++) {
    const q = arena.qbuf[s]; if (arena.mtrail[q] || arena.mowner[q] >= 0 || arena.mshield[q]) continue;
    const x = arena.mpos[3 * q], y = arena.mpos[3 * q + 1], z = arena.mpos[3 * q + 2], v = arena.consume(q);
    if (v <= 0) continue;
    arena.eaten -= v;   // not grazing: the volume moves into the body (ledger), not out of the world
    this.alive[j] = 1; this.body[j] = v; this.gut[j] = 0; this.bit[j] = 0;
    this.pos[3 * j] = x; this.pos[3 * j + 1] = y; this.pos[3 * j + 2] = z; this.vel[3 * j] = this.vel[3 * j + 1] = this.vel[3 * j + 2] = 0;
    if (arena.onBirth) arena.onBirth(this, j);
    return;
  }
};
Siege.prototype.colour = function (i, c) {
  const it = this.intent[i], ph = this.phase;
  if (ph === 'roam' || ph === 'scatter' || this.bit[i]) { c[0] = 0.32; c[1] = 0.22; c[2] = 0.42; return c; }
  if (ph === 'gather') { c[0] = 0.45 + 0.3 * it; c[1] = 0.18; c[2] = 0.62; return c; }
  // CLOSE/HOLD/DIVE: violet -> magenta -> white-hot
  c[0] = Math.min(1, 0.7 + 0.3 * it); c[1] = 0.15 + 0.55 * Math.max(0, it - 0.75) * 4 * 0.25; c[2] = 0.75 - 0.35 * it; return c;
};
/** the encounter summary siege_eval.js and the page read. */
Siege.prototype.report = function () {
  return { encounters: this.encounters, escapes: this.escapes, dives: this.dives, dive_bites: this.diveBites, wall_bites: this.wallBites, breaches: this.breaches,
    phase: this.phase, fill: +this.fill.toFixed(2), shell_r: +this.Rs.toFixed(0) };
};
function smooth01(x) { x = clamp(x, 0, 1); return x * x * (3 - 2 * x); }
