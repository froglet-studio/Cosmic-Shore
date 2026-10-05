// ===================================================================================================================
// 30_snaptrap.js - the SNAP TRAP clump (flora/snaptrap.py, run with its SEARCHED parameters exactly as
// flora/sandbox.py overlays them) on a PlantBody prism store (flora/harness.py). Each trap: a heart -> a stalk ->
// two lobes whose rims carry DANGER teeth. OPEN -> PRIMING (the lobes gape wider and glow: the telegraph) ->
// ARMED -> CLOSING (a pilot still between the lobes is SNAPPED) -> SHUT (digest/reset) -> OPEN. Traps turn to face
// traffic (an EMA of where vessels passed) and bud new traps only from mass their roots absorbed.
// ===================================================================================================================
const OPEN = 0, PRIMING = 1, ARMED = 2, CLOSING = 3, SHUT = 4;

function PlantBody(cap) {
  this.cap = 0; this.n = 0; this._alloc(cap || 4096); this.burnCd = new Map(); this.laid = 0; this.laidVol = 0;
}
PlantBody.prototype._alloc = function (cap) {
  const g = (A, T, k) => { const b = new T(cap * (k || 1)); if (A) b.set(A); return b; };
  this.pos = g(this.pos, Float64Array, 3); this.half = g(this.half, Float64Array); this.vol = g(this.vol, Float64Array);
  this.danger = g(this.danger, Uint8Array); const o = new Int32Array(cap).fill(-1); if (this.owner) o.set(this.owner); this.owner = o;
  this.alive = g(this.alive, Uint8Array); this.hot = g(this.hot, Float64Array); this.shield = g(this.shield, Uint8Array);
  this.cap = cap;
};
PlantBody.prototype.lay = function (x, y, z, half, vol, owner, danger, shield) {
  if (this.n >= this.cap) this._alloc(this.cap * 2);
  const i = this.n++;
  this.pos[3 * i] = x; this.pos[3 * i + 1] = y; this.pos[3 * i + 2] = z; this.half[i] = half; this.vol[i] = vol;
  this.owner[i] = owner; this.danger[i] = danger ? 1 : 0; this.alive[i] = 1; this.hot[i] = 0; this.shield[i] = shield ? 1 : 0;
  this.laid++; this.laidVol += vol; return i;
};
PlantBody.prototype.take = function (j) {
  if (!this.alive[j]) return 0;
  if (this.shield[j]) { this.shield[j] = 0; return 0; }
  this.alive[j] = 0; return this.vol[j];
};
PlantBody.prototype.totalVolume = function () { let s = 0; for (let i = 0; i < this.n; i++) if (this.alive[i]) s += this.vol[i]; return s; };
/** live prisms whose bounding sphere (radius half) a pilot segment a->b of hull radius touches. */
PlantBody.prototype.contacts = function (a, b, radius, out) {
  out.length = 0;
  const abx = b[0] - a[0], aby = b[1] - a[1], abz = b[2] - a[2], L2 = abx * abx + aby * aby + abz * abz;
  for (let i = 0; i < this.n; i++) {
    if (!this.alive[i]) continue;
    const px = this.pos[3 * i] - a[0], py = this.pos[3 * i + 1] - a[1], pz = this.pos[3 * i + 2] - a[2];
    const t = L2 < 1e-12 ? 0 : clamp((px * abx + py * aby + pz * abz) / L2, 0, 1);
    const dx = px - t * abx, dy = py - t * aby, dz = pz - t * abz;
    if (Math.sqrt(dx * dx + dy * dy + dz * dz) < this.half[i] + radius) out.push(i);
  }
  return out;
};
function segPointDist(a, b, x, y, z) {
  const abx = b[0] - a[0], aby = b[1] - a[1], abz = b[2] - a[2], L2 = abx * abx + aby * aby + abz * abz;
  const px = x - a[0], py = y - a[1], pz = z - a[2];
  const t = L2 < 1e-12 ? 0 : clamp((px * abx + py * aby + pz * abz) / L2, 0, 1);
  return Math.hypot(px - t * abx, py - t * aby, pz - t * abz);
}

function SnapTrap(arena, P, o) {
  o = o || {}; this.name = 'snaptrap';
  const p = this.p = Object.assign({}, P.extra.params, o.params || {});
  this.L = P.extra.layout; this.K = this.L.length;
  this.rng = arena.rng; this.body = new PlantBody(4096);
  const cap = this.cap = o.cap || 512, K = this.K;
  this.h = new Float64Array(3 * cap); this.a = new Float64Array(3 * cap); this.state = new Int8Array(cap);
  this.timer = new Float64Array(cap); this.theta = new Float64Array(cap); this.itn = new Float64Array(cap);
  this.grow = new Float64Array(cap); this.alive = new Uint8Array(cap); this.slots = new Int32Array(cap * K).fill(-1);
  this.ema = new Float64Array(3 * cap); this.absorbT = new Float64Array(cap); this.caught = new Uint8Array(cap); this.fired = new Float64Array(cap);
  this.n = 0; this.cutVolume = 0; this.crystals = 0; this.deaths = 0; this.snaps = 0; this.kills = 0;
  this.cost = 3 * p.prism_vol + 16 * p.prism_vol + 8 * p.tooth_vol;
  this.reserve = (o.n || p.n_traps) * this.cost;
  const clumps = o.clumps || p.clumps, C = new Float64Array(3 * clumps);
  for (let c = 0; c < clumps; c++) { if (o.placeClump) o.placeClump(arena, C, 3 * c); else arena.grove(60, C, 3 * c); }
  const nt = o.n || p.n_traps;
  for (let i = 0; i < nt; i++) {
    const c = i % clumps;
    this.sprout(C[3 * c] + this.rng.normal(0, p.clump_r), C[3 * c + 1] + this.rng.normal(0, p.clump_r), C[3 * c + 2] + this.rng.normal(0, p.clump_r), true);
  }
  this.pose();
  this._ct = []; this._fr = [0, 0, 0, 0, 0, 0];
}
SnapTrap.prototype.sprout = function (x, y, z, grown) {
  if (this.n >= this.cap || this.reserve < this.cost) return -1;
  const i = this.n++, d = vunit(this.rng.normal(), this.rng.normal(), this.rng.normal(), [0, 0, 0]);
  this.a.set(d, 3 * i); this.h[3 * i] = x; this.h[3 * i + 1] = y; this.h[3 * i + 2] = z;
  this.state[i] = OPEN; this.theta[i] = this.p.gape * Math.PI / 180; this.alive[i] = 1;
  this.grow[i] = grown ? 1 : 0; this.absorbT[i] = this.rng.uniform(0, this.p.absorb_every);
  this.layUpto(i, grown ? 1 : 0);
  return i;
};
SnapTrap.prototype.layUpto = function (i, g) {
  const K = this.K, p = this.p;
  for (let k = 0; k < K; k++) {
    if (this.slots[i * K + k] >= 0 || k / K > g) continue;
    const dg = this.L[k][3], vol = dg ? p.tooth_vol : p.prism_vol;
    if (this.reserve < vol) return;
    this.reserve -= vol;
    this.slots[i * K + k] = this.body.lay(this.h[3 * i], this.h[3 * i + 1], this.h[3 * i + 2], dg ? 3.5 : 4.0, vol, i, dg, false);
  }
};
/** frame (n, b) perpendicular to axis a (flora/snaptrap.py _frame). */
SnapTrap.prototype.frame = function (i, F) {
  const ax = this.a[3 * i], ay = this.a[3 * i + 1], az = this.a[3 * i + 2];
  const ux = Math.abs(ay) < 0.9 ? 0 : 1, uy = Math.abs(ay) < 0.9 ? 1 : 0, uz = 0;
  let nx = ay * uz - az * uy, ny = az * ux - ax * uz, nz = ax * uy - ay * ux; const nn = Math.hypot(nx, ny, nz); nx /= nn; ny /= nn; nz /= nn;
  F[0] = nx; F[1] = ny; F[2] = nz; F[3] = ay * nz - az * ny; F[4] = az * nx - ax * nz; F[5] = ax * ny - ay * nx; return F;
};
SnapTrap.prototype.pose = function () {
  const K = this.K, F = this._fr || (this._fr = [0, 0, 0, 0, 0, 0]), B = this.body.pos;
  for (let i = 0; i < this.n; i++) {
    let any = false; for (let k = 0; k < K; k++) if (this.slots[i * K + k] >= 0) { any = true; break; } if (!any) continue;
    this.frame(i, F);
    const ax = this.a[3 * i], ay = this.a[3 * i + 1], az = this.a[3 * i + 2], hx = this.h[3 * i], hy = this.h[3 * i + 1], hz = this.h[3 * i + 2];
    const mx = hx + ax * 22, my = hy + ay * 22, mz = hz + az * 22, th = this.theta[i], ct = Math.cos(th), st = Math.sin(th);
    for (let k = 0; k < K; k++) {
      const j = this.slots[i * K + k]; if (j < 0) continue;
      const kind = this.L[k][0], r = this.L[k][1], s = this.L[k][2];
      if (kind === 0) { B[3 * j] = hx + ax * r; B[3 * j + 1] = hy + ay * r; B[3 * j + 2] = hz + az * r; }
      else {
        const ux = ct * ax + kind * st * F[0], uy = ct * ay + kind * st * F[1], uz = ct * az + kind * st * F[2];
        B[3 * j] = mx + ux * r + F[3] * s; B[3 * j + 1] = my + uy * r + F[4] * s; B[3 * j + 2] = mz + uz * r + F[5] * s;
      }
    }
  }
};
SnapTrap.prototype.step = function (arena, dt) {
  const p = this.p, N = this.n, K = this.K, PL = arena.pilots, rng = this.rng;
  const live = []; for (let i = 0; i < N; i++) if (this.alive[i]) live.push(i);
  for (const i of live) {                                   // roots: absorb food, bud new traps
    if (this.grow[i] < 1) { this.grow[i] = Math.min(1, this.grow[i] + dt / 6); this.layUpto(i, this.grow[i]); continue; }
    this.absorbT[i] -= dt;
    if (this.absorbT[i] <= 0) {
      this.absorbT[i] = p.absorb_every * rng.uniform(0.8, 1.2);
      const c = arena.massNear(this.h[3 * i], this.h[3 * i + 1], this.h[3 * i + 2], p.root);
      if (c) {
        let best = -1, bd = Infinity;
        for (let s = 0; s < c; s++) { const j = arena.qbuf[s]; const d = (arena.mpos[3 * j] - this.h[3 * i]) ** 2 + (arena.mpos[3 * j + 1] - this.h[3 * i + 1]) ** 2 + (arena.mpos[3 * j + 2] - this.h[3 * i + 2]) ** 2; if (d < bd) { bd = d; best = j; } }
        this.reserve += arena.consume(best);
      }
      this.layUpto(i, 1);
    }
  }
  if (this.reserve >= this.cost && live.length) {
    const par = live[rng.int(live.length)];
    let d = vunit(rng.normal(), rng.normal(), rng.normal(), [0, 0, 0]);
    d = vunit(d[0] + p.bud_bias * 3 * this.ema[3 * par], d[1] + p.bud_bias * 3 * this.ema[3 * par + 1], d[2] + p.bud_bias * 3 * this.ema[3 * par + 2], [0, 0, 0]);
    const r = rng.uniform(45, 80);
    this.sprout(this.h[3 * par] + d[0] * r, this.h[3 * par + 1] + d[1] * r, this.h[3 * par + 2] + d[2] * r, false);
  }
  const gape = p.gape * Math.PI / 180, wide = (p.gape + p.dgape) * Math.PI / 180, shut = 3 * Math.PI / 180;
  const F = [0, 0, 0, 0, 0, 0];
  for (const i of live) {
    if (this.grow[i] < 1) continue;
    this.frame(i, F);
    const ax = this.a[3 * i], ay = this.a[3 * i + 1], az = this.a[3 * i + 2];
    const mx = this.h[3 * i] + ax * 22, my = this.h[3 * i + 1] + ay * 22, mz = this.h[3 * i + 2] + az * 22;
    let near = false, inMouth = false;
    for (const pi of PL) {
      const dx = pi.pos[0] - mx, dy = pi.pos[1] - my, dz = pi.pos[2] - mz, dist = Math.sqrt(dx * dx + dy * dy + dz * dz);
      if (dist < p.sense) {
        near = true; const dn = Math.max(dist, 1e-6);
        for (let a = 0; a < 3; a++) this.ema[3 * i + a] = 0.97 * this.ema[3 * i + a] + 0.03 * [dx, dy, dz][a] / dn;
      }
      const z = dx * ax + dy * ay + dz * az, y = dx * F[0] + dy * F[1] + dz * F[2], w = dx * F[3] + dy * F[4] + dz * F[5];
      if (z > 0 && z < p.mouth_len && Math.abs(w) < p.mouth_w && Math.abs(y) < z * Math.tan(this.theta[i]) + 6) inMouth = true;
    }
    let st = this.state[i], tm = this.timer[i] + dt, th = this.theta[i], it = this.itn[i];
    if (st === OPEN && near) { st = PRIMING; tm = 0; }
    const pr = st === PRIMING;
    if (pr) { it = Math.min(1, it + dt / p.t_prime); th = gape + (wide - gape) * it; }
    if (pr && !near) { it = Math.max(0, it - 2 * dt / p.t_prime); if (it <= 0) st = OPEN; }
    if (pr && it >= 1 && near) st = ARMED;
    const ar = st === ARMED;
    if (ar && !near) st = PRIMING;
    let intact = 0, tot = 0; for (let k = 3; k < K; k++) { tot++; if (this.slots[i * K + k] >= 0) intact++; }
    const fire = (p.fire_on === 'mouth' ? (ar && inMouth) : (ar && near)) && intact / tot >= 0.5;
    if (fire) { st = CLOSING; tm = 0; this.fired[i]++; }
    const cl = st === CLOSING, thPrev = th;
    if (cl) { th = Math.max(shut, wide - (wide - shut) * tm / p.t_close); it = 1; }
    const done = cl && tm >= p.t_close;
    if (cl) for (const pi of PL) {                                       // the closing sweep / a pilot still inside when shut
      const dx = pi.pos[0] - mx, dy = pi.pos[1] - my, dz = pi.pos[2] - mz;
      const z = dx * ax + dy * ay + dz * az, y = Math.abs(dx * F[0] + dy * F[1] + dz * F[2]), w = Math.abs(dx * F[3] + dy * F[4] + dz * F[5]);
      const inslab = z > 0 && z < p.mouth_len + 6 && w < p.mouth_w + 6;
      const swept = inslab && y < z * Math.tan(thPrev) + 6 && y > z * Math.tan(th) - 6;
      const caught = done && inslab && y < z * Math.tan(th) + 8;
      if ((swept || caught) && !this.caught[i]) { this.caught[i] = 1; this.snaps++; arena.hit(pi, 'snap', undefined, i); }
    }
    if (done) { st = SHUT; tm = 0; }
    if (st === SHUT) {
      const reset = this.caught[i] ? p.t_digest : p.t_reset;
      it = Math.max(0, it - dt / 0.5);
      const reopen = tm > reset;
      th = reopen ? Math.min(gape, th + dt * (gape - shut) / 1.5) : shut;
      if (reopen && th >= gape - 1e-6) { st = OPEN; this.caught[i] = 0; }
    }
    this.state[i] = st; this.timer[i] = tm; this.theta[i] = th; this.itn[i] = it;
  }
  for (const i of live) {                                   // heliotropism toward traffic
    if (this.grow[i] < 1) continue;
    const ex = this.ema[3 * i], ey = this.ema[3 * i + 1], ez = this.ema[3 * i + 2], ne = Math.hypot(ex, ey, ez);
    if (ne < 0.05 || this.state[i] === CLOSING) continue;
    const tx = ex / ne, ty = ey / ne, tz = ez / ne, cx = this.a[3 * i], cy = this.a[3 * i + 1], cz = this.a[3 * i + 2];
    const ang = Math.acos(clamp(cx * tx + cy * ty + cz * tz, -1, 1)), kk = Math.min(1, p.turn_deg * Math.PI / 180 * dt / Math.max(ang, 1e-6));
    this.a.set(vunit(cx + (tx - cx) * kk, cy + (ty - cy) * kk, cz + (tz - cz) * kk, [0, 0, 0]), 3 * i);
  }
  this.pose();
  const B = this.body;
  for (let j = 0; j < B.n; j++) {
    if (!B.alive[j]) continue;
    let hot = B.danger[j] ? 0.6 : 0; const o = B.owner[j];
    if (o >= 0) hot = Math.max(hot, this.itn[o] * this.alive[o]);
    B.hot[j] = hot;
  }
  const c = this._ct;
  for (let q = 0; q < PL.length; q++) {                      // rim teeth burn on contact
    const pi = PL[q]; B.contacts(pi.prev, pi.pos, 6.0, c);
    for (const j of c) {
      if (!B.danger[j]) continue;
      const key = j * 16 + q;
      if ((B.burnCd.get(key) || -1e9) > arena.t) continue;
      B.burnCd.set(key, arena.t + 1.0);
      const o = B.owner[j]; if (o >= 0 && this.caught[o]) continue;
      arena.hit(pi, 'burn', undefined, o, true);
    }
  }
  if (p.ram) for (const pi of PL) {                          // a pilot flying through PLAIN plant prisms breaks them
    B.contacts(pi.prev, pi.pos, pi.radius, c);
    for (const j of c) { if (B.danger[j]) continue; const v = B.take(j); if (!v) continue; this.cutVolume += v; this.clearSlot(j); }
  }
};
SnapTrap.prototype.clearSlot = function (j) {
  const o = this.body.owner[j]; if (o < 0) return; const K = this.K;
  for (let k = 0; k < K; k++) if (this.slots[o * K + k] === j) this.slots[o * K + k] = -1;
};
SnapTrap.prototype.heartPos = function (i, out) {
  if (!this.p.heart_in_jaws) { out[0] = this.h[3 * i]; out[1] = this.h[3 * i + 1]; out[2] = this.h[3 * i + 2]; return out; }
  out[0] = this.h[3 * i] + this.a[3 * i] * 30; out[1] = this.h[3 * i + 1] + this.a[3 * i + 1] * 30; out[2] = this.h[3 * i + 2] + this.a[3 * i + 2] * 30; return out;
};
SnapTrap.prototype.cutTargets = function (buf) {
  let m = 0; const t = [0, 0, 0];
  for (let i = 0; i < this.n; i++) if (this.alive[i] && this.grow[i] >= 1) {
    if (buf.a.length < 3 * (m + 1)) { const b = new Float64Array(buf.a.length * 2); b.set(buf.a); buf.a = b; }
    this.heartPos(i, t); buf.a[3 * m] = t[0]; buf.a[3 * m + 1] = t[1]; buf.a[3 * m + 2] = t[2]; m++;
  }
  buf.n = m; return m;
};
/** the cutter's ability: break plain plant prisms along its path, collect hearts within 12 u of it. */
SnapTrap.prototype.cut = function (arena, pilot, a, b) {
  const c = this.body.contacts(a, b, 14.0, this._ct);
  for (const j of c) { if (this.body.danger[j]) continue; const v = this.body.take(j); if (!v) continue; this.cutVolume += v; this.clearSlot(j); }
  this.collectHearts(arena, a, b, 12.0);
};
SnapTrap.prototype.collectHearts = function (arena, a, b, r) {
  const t = [0, 0, 0];
  for (let i = 0; i < this.n; i++) if (this.alive[i]) { this.heartPos(i, t); if (segPointDist(a, b, t[0], t[1], t[2]) < r) this.die(arena, i); }
};
SnapTrap.prototype.die = function (arena, i) {
  this.alive[i] = 0; this.crystals++; this.deaths++; this.kills++; this.itn[i] = 0;
  for (let k = 0; k < this.K; k++) { const j = this.slots[i * this.K + k]; if (j >= 0) { this.body.danger[j] = 0; this.body.owner[j] = -1; } }
  if (arena.onKill) arena.onKill(this, i);
};
/** what the plant TELEGRAPHS (glowing mouths + teeth) - the reader pilot steers on this and nothing else. */
SnapTrap.prototype.hazards = function () {
  const H = this._hz || (this._hz = { pos: new Float64Array(3 * 4096), rad: new Float64Array(4096), w: new Float64Array(4096), n: 0 });
  const L = this.p.mouth_len, lip = L * Math.tan((this.p.gape + this.p.dgape) * Math.PI / 180), mr = Math.max(L * 0.7, lip);
  let m = 0;
  const grow = need => { if (H.rad.length < need) { const nb = need * 2; const p2 = new Float64Array(3 * nb); p2.set(H.pos); H.pos = p2; const r2 = new Float64Array(nb); r2.set(H.rad); H.rad = r2; const w2 = new Float64Array(nb); w2.set(H.w); H.w = w2; } };
  for (let i = 0; i < this.n; i++) if (this.alive[i] && this.grow[i] >= 1 && this.itn[i] > 0.25) {
    grow(m + 1);
    for (let a = 0; a < 3; a++) H.pos[3 * m + a] = this.h[3 * i + a] + this.a[3 * i + a] * (22 + L * 0.6);
    H.rad[m] = mr; H.w[m] = this.itn[i]; m++;
  }
  const B = this.body;
  for (let j = 0; j < B.n; j++) if (B.alive[j] && B.danger[j]) { grow(m + 1); for (let a = 0; a < 3; a++) H.pos[3 * m + a] = B.pos[3 * j + a]; H.rad[m] = 8; H.w[m] = 0.5; m++; }
  H.n = m; return H;
};
SnapTrap.prototype.massTotal = function () { return this.reserve + this.body.totalVolume(); };
SnapTrap.prototype.ledger = function () { return this.massTotal(); };
SnapTrap.prototype.liveCount = function () { let c = 0; for (let i = 0; i < this.n; i++) c += this.alive[i]; return c; };
SnapTrap.prototype.view = function (v) {
  v = v || {}; let m = 0; for (let i = 0; i < this.n; i++) m += this.alive[i];
  if (!v.P || v.P.length < 3 * m) { v.P = new Float64Array(3 * Math.max(m, 1)); v.V = new Float64Array(3 * Math.max(m, 1)); v.S = new Float64Array(Math.max(m, 1)); v.I = new Float64Array(Math.max(m, 1)); }
  let w = 0;
  for (let i = 0; i < this.n; i++) if (this.alive[i]) { for (let a = 0; a < 3; a++) { v.P[3 * w + a] = this.h[3 * i + a]; v.V[3 * w + a] = 0; } v.S[w] = 30; v.I[w] = this.itn[i]; w++; }
  v.m = m; v.kills = this.crystals; v.crystals = this.crystals; return v;
};
