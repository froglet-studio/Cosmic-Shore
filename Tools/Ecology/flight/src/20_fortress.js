// ===================================================================================================================
// 20_fortress.js - the FORTRESS builder colony (builders/fortress.py on builders/nest.py NestWeavers on
// builders/core.py Colony): workers forage loose + trail prisms, STEAL them on pickup (they change hands, never
// leave), carry them home and deposit them on a lattice by a LOCAL rule (a Q-template shell x cement); a cut wall
// releases alarm that pulls laden menders to the wound and a gap rule closes it. A pilot inside the alarm radius
// raises the colony's alarm: below 0.6 the workers screen the core (the telegraph), above it they strike.
// The lattice is kept at `half` sites each way (default 12 = 96 u): the Q template is < 0.05 past 52 u, so no rule
// can read or write beyond it; Python's half=40 only adds empty memory.
// ===================================================================================================================

const N26 = (function () { const o = []; for (let x = -1; x <= 1; x++) for (let y = -1; y <= 1; y++) for (let z = -1; z <= 1; z++) if (x || y || z) o.push([x, y, z]); return o; })();
const N26_3 = N26.filter((_, i) => i % 3 === 0);

function Fortress(arena, P, o) {
  o = o || {}; this.name = 'fortress';
  const I = P.init, Hd = P.herd, seed = o.seed === undefined ? 0 : o.seed;
  this.n = o.n || I.n; this.Rc = I.Rc; this.w = I.w; this.kc = Hd.k_cement; this.nuc = Hd.nucleate;
  this.alarmR = I.alarm; this.gpb = I.growth_per_brood; this.gapGain = I.gap_gain; this.alarmGain = I.alarm_gain;
  this.mend = o.mend || I.mend; this.dom = Hd.dom; this.speed = Hd.speed; this.sense = Hd.sense; this.frac = Hd.frac;
  this.s = Hd.s; this.half = o.half || 12;
  this.rng = new Rng(seed + 991);
  const ar = new Rng(seed + 5);
  let anchor = o.anchor;
  if (!anchor) { const d = vunit(ar.normal(), ar.normal(), ar.normal(), [0, 0, 0]); anchor = [d[0] * 450, d[1] * 450, d[2] * 450]; }
  this.anchor = anchor.slice();
  const n = this.n;
  this.pos = new Float64Array(3 * n); this.vel = new Float64Array(3 * n);
  for (let k = 0; k < n; k++) for (let a = 0; a < 3; a++) this.pos[3 * k + a] = anchor[a] + this.rng.normal(0, 20);
  this.size = new Float64Array(n).fill(3); this.intent = new Float64Array(n); this.alive = new Uint8Array(n).fill(1);
  this.carry = new Int32Array(n).fill(-1); this.goal = new Int32Array(n).fill(-1);
  this.wander = new Float64Array(3 * n); for (let k = 0; k < 3 * n; k++) this.wander[k] = this.rng.normal();
  this.dirs = new Float64Array(3 * n);
  for (let k = 0; k < n; k++) { const d = vunit(ar.normal(), ar.normal(), ar.normal(), [0, 0, 0]); this.dirs.set(d, 3 * k); }
  this.tick = 0; this.kills = 0; this.crystals = 0; this.placed = 0; this.placedTrail = 0; this.pickups = 0; this.carryMoves = 0;
  this.claimed = new Set(); this.taken = new Set();
  // lattice
  const m = this.m = 2 * this.half + 1, M3 = m * m * m;
  this.occ = new Int32Array(M3).fill(-1); this.blocked = new Uint8Array(M3);
  this.cement = new Float32Array(M3); this.alarm = new Float32Array(M3); this.Q = new Float32Array(M3);
  this._tmp = new Float32Array(M3);
  for (let i = 0; i < m; i++) for (let j = 0; j < m; j++) for (let kk = 0; kk < m; kk++) {
    const r = Math.hypot(i - this.half, j - this.half, kk - this.half) * this.s, id = (i * m + j) * m + kk;
    this.Q[id] = Math.exp(-(((r - this.Rc) / this.w) ** 2));
    if (r < this.Rc * 0.55) this.blocked[id] = 1;
  }
  this.sites = new Map();          // mass index -> site id
  this.store = 0; this.raided = 0; this.breaches = 0; this.cuts = 0; this.repairs = 0; this.repairTrail = 0;
  this.openBreach = new Set(); this.alarmLevel = 0; this.cool = new Float64Array(n);
  this.queries = 0;
}
Fortress.prototype.siteOf = function (x, y, z) {
  const h = this.half, s = this.s, A = this.anchor;
  return [Math.round((x - A[0]) / s) + h, Math.round((y - A[1]) / s) + h, Math.round((z - A[2]) / s) + h];
};
Fortress.prototype.inside = function (q) { const m = this.m; return q[0] >= 0 && q[0] < m && q[1] >= 0 && q[1] < m && q[2] >= 0 && q[2] < m; };
Fortress.prototype.id = function (q) { return (q[0] * this.m + q[1]) * this.m + q[2]; };
Fortress.prototype.sitePos = function (q, out) { const h = this.half, s = this.s, A = this.anchor; out[0] = A[0] + (q[0] - h) * s; out[1] = A[1] + (q[1] - h) * s; out[2] = A[2] + (q[2] - h) * s; return out; };
Fortress.prototype.free = function (q) { return this.inside(q) && this.occ[this.id(q)] < 0 && !this.blocked[this.id(q)]; };
Fortress.prototype.count = function (q) {
  let c = 0;
  for (const o of N26) { const r = [q[0] + o[0], q[1] + o[1], q[2] + o[2]]; if (this.inside(r) && this.occ[this.id(r)] >= 0) c++; }
  return c;
};
Fortress.prototype.depositScore = function (q) {
  const id = this.id(q), qq = this.Q[id];
  if (qq < 0.05) return 0;
  const c = this.cement[id], nb = this.count(q);
  let p = nb > 0 ? qq * (0.05 + c * c / (c * c + this.kc * this.kc)) : qq * this.nuc;
  if (p <= 0) return p;
  if (this.mend === 'gap' || this.mend === 'both') p *= 1 + this.gapGain * Math.max(0, nb - 4) / 6;
  return Math.min(1, p);
};
Fortress.prototype.home = function (k, out) {
  if (this.mend === 'alarm' || this.mend === 'both') {
    const site = this.siteOf(this.pos[3 * k], this.pos[3 * k + 1], this.pos[3 * k + 2]);
    if (this.inside(site) && this.alarm[this.id(site)] > 0.02) {
      let best = null, bv = this.alarm[this.id(site)];
      for (const o of N26_3) { const q = [site[0] + o[0], site[1] + o[1], site[2] + o[2]]; if (this.inside(q) && this.alarm[this.id(q)] > bv) { best = q; bv = this.alarm[this.id(q)]; } }
      return this.sitePos(best || site, out);
    }
  }
  // a laden worker walks to "its" drifting bearing on the shell
  const d = vunit(this.dirs[3 * k] + this.rng.normal(0, 0.05), this.dirs[3 * k + 1] + this.rng.normal(0, 0.05), this.dirs[3 * k + 2] + this.rng.normal(0, 0.05), [0, 0, 0]);
  this.dirs.set(d, 3 * k);
  out[0] = this.anchor[0] + d[0] * this.Rc; out[1] = this.anchor[1] + d[1] * this.Rc; out[2] = this.anchor[2] + d[2] * this.Rc; return out;
};
Fortress.prototype.steerK = function (k, tx, ty, tz, dt, speed) {
  const dx = tx - this.pos[3 * k], dy = ty - this.pos[3 * k + 1], dz = tz - this.pos[3 * k + 2], n = Math.sqrt(dx * dx + dy * dy + dz * dz);
  const sp = speed === undefined ? this.speed : speed, v = Math.min(sp, n / Math.max(dt, 1e-6)) / Math.max(n, 1e-6);
  this.vel[3 * k] = 0.7 * this.vel[3 * k] + 0.3 * dx * v; this.vel[3 * k + 1] = 0.7 * this.vel[3 * k + 1] + 0.3 * dy * v; this.vel[3 * k + 2] = 0.7 * this.vel[3 * k + 2] + 0.3 * dz * v;
};
Fortress.prototype.stealable = function (arena, i) {
  return arena.malive[i] && !arena.mshield[i] && !this.taken.has(i) && !this.claimed.has(i) && !arena.mstruct[i];
};
Fortress.prototype.forageTarget = function (arena, k) {
  this.queries++;
  const c = arena.massNear(this.pos[3 * k], this.pos[3 * k + 1], this.pos[3 * k + 2], this.sense);
  let best = -1, bd = 1e18;
  for (let s = 0; s < c; s++) {
    const i = arena.qbuf[s]; if (!this.stealable(arena, i)) continue;
    const d = ((arena.mpos[3 * i] - this.pos[3 * k]) ** 2 + (arena.mpos[3 * i + 1] - this.pos[3 * k + 1]) ** 2 + (arena.mpos[3 * i + 2] - this.pos[3 * k + 2]) ** 2) * (arena.mtrail[i] ? 0.25 : 1);
    if (d < bd) { best = i; bd = d; }
  }
  return best;
};
const _pick8 = new Int32Array(26);
Fortress.prototype.tryDeposit = function (arena, k) {
  const here = this.siteOf(this.pos[3 * k], this.pos[3 * k + 1], this.pos[3 * k + 2]);
  for (let i = 0; i < 26; i++) _pick8[i] = i;
  for (let i = 0; i < 8; i++) { const j = i + this.rng.int(26 - i); const t = _pick8[i]; _pick8[i] = _pick8[j]; _pick8[j] = t; }
  let best = null, bp = 0;
  for (let s = 0; s < 8; s++) {
    const o = N26[_pick8[s]], q = [here[0] + o[0], here[1] + o[1], here[2] + o[2]];
    if (!this.free(q)) continue;
    const p = this.depositScore(q); if (p > bp) { best = q; bp = p; }
  }
  if (best && this.rng.random() < bp) {
    const i = this.carry[k], P = this.sitePos(best, [0, 0, 0]);
    arena.moveMass(i, P[0], P[1], P[2]);
    const id = this.id(best); this.occ[id] = i; this.sites.set(i, id);
    this.carry[k] = -1; this.placed++; arena.mstruct[i] = 1; if (arena.mtrail[i]) this.placedTrail++;
    this.cement[id] += 1; if (this.placed % this.gpb === 0) this.store++;
    if (this.openBreach.has(id)) { this.openBreach.delete(id); this.repairs++; this.repairTrail += arena.mtrail[i]; }
    return true;
  }
  return false;
};
Fortress.prototype.onRammed = function (arena, k) {
  this.alive[k] = 0; this.kills++; this.crystals++;
  if (this.carry[k] >= 0) { this.taken.delete(this.carry[k]); this.carry[k] = -1; }
  if (this.goal[k] >= 0) { this.claimed.delete(this.goal[k]); this.goal[k] = -1; }
  if (arena.onKill) arena.onKill(this, k);
};
Fortress.prototype.blur = function (a, keep, w, decay) {
  // a' = decay * (keep*a + w/6 * sum of the 6 face neighbours), zero boundary
  const m = this.m, t = this._tmp, mm = m * m;
  for (let i = 0; i < m; i++) for (let j = 0; j < m; j++) for (let k = 0; k < m; k++) {
    const id = (i * m + j) * m + k; let s = 0;
    if (i > 0) s += a[id - mm]; if (i < m - 1) s += a[id + mm];
    if (j > 0) s += a[id - m]; if (j < m - 1) s += a[id + m];
    if (k > 0) s += a[id - 1]; if (k < m - 1) s += a[id + 1];
    t[id] = decay * (keep * a[id] + w / 6 * s);
  }
  a.set(t);
};
Fortress.prototype.step = function (arena, dt) {
  this.tick++;
  const R = arena.R, n = this.n, X = this.pos, tgt = [0, 0, 0];
  for (let k = 0; k < n; k++) {
    if (!this.alive[k]) continue;
    const refresh = (k + this.tick) % this.frac === 0;
    if (this.carry[k] >= 0) {
      if (!arena.malive[this.carry[k]]) { this.taken.delete(this.carry[k]); this.carry[k] = -1; continue; }
      this.home(k, tgt); this.steerK(k, tgt[0], tgt[1], tgt[2], dt); this.tryDeposit(arena, k);
    } else {
      let g = this.goal[k];
      if (g >= 0 && (!arena.malive[g] || this.taken.has(g))) { this.claimed.delete(g); this.goal[k] = g = -1; }
      if (g < 0 && refresh) { g = this.forageTarget(arena, k); if (g >= 0) { this.goal[k] = g; this.claimed.add(g); } }
      if (g >= 0) {
        this.steerK(k, arena.mpos[3 * g], arena.mpos[3 * g + 1], arena.mpos[3 * g + 2], dt);
        if (Math.hypot(arena.mpos[3 * g] - X[3 * k], arena.mpos[3 * g + 1] - X[3 * k + 1], arena.mpos[3 * g + 2] - X[3 * k + 2]) < 6) {
          arena.steal(g, this.dom); this.claimed.delete(g); this.goal[k] = -1;
          if (arena.mdom[g] === this.dom) { this.carry[k] = g; this.taken.add(g); this.pickups++; }
        }
      } else {
        if (refresh) {
          const w = vunit(this.wander[3 * k] + this.rng.normal(0, 0.5), this.wander[3 * k + 1] + this.rng.normal(0, 0.5), this.wander[3 * k + 2] + this.rng.normal(0, 0.5), [0, 0, 0]);
          this.wander.set(w, 3 * k);
        }
        this.steerK(k, X[3 * k] + this.wander[3 * k] * 50, X[3 * k + 1] + this.wander[3 * k + 1] * 50, X[3 * k + 2] + this.wander[3 * k + 2] * 50, dt, this.speed * 0.6);
      }
    }
  }
  this.behave(arena, dt);
  for (let k = 0; k < n; k++) {
    for (let a = 0; a < 3; a++) X[3 * k + a] += this.vel[3 * k + a] * dt;
    const r = Math.hypot(X[3 * k], X[3 * k + 1], X[3 * k + 2]);
    if (r > R * 0.95) { const s = R * 0.95 / r; X[3 * k] *= s; X[3 * k + 1] *= s; X[3 * k + 2] *= s; }
  }
  for (let k = 0; k < n; k++) {      // carried prisms ride their carrier (one write each, only when it moved)
    if (!this.alive[k] || this.carry[k] < 0) continue;
    const c = this.carry[k], tx = X[3 * k], ty = X[3 * k + 1] - 4, tz = X[3 * k + 2];
    if ((arena.mpos[3 * c] - tx) ** 2 + (arena.mpos[3 * c + 1] - ty) ** 2 + (arena.mpos[3 * c + 2] - tz) ** 2 > 0.25) { arena.moveMass(c, tx, ty, tz); this.carryMoves++; }
  }
};
Fortress.prototype.behave = function (arena, dt) {
  // breaches: a structure prism destroyed OR stolen back (changed hands) frees its site
  const dead = [];
  for (const [i, id] of this.sites) if (!arena.malive[i] || arena.mdom[i] !== this.dom) dead.push(i);
  for (const i of dead) {
    const id = this.sites.get(i); this.sites.delete(i); this.occ[id] = -1; this.taken.delete(i); arena.mstruct[i] = 0;
    this.breaches++; this.cuts++; this.openBreach.add(id);
    if (this.mend === 'alarm' || this.mend === 'both') this.alarm[id] += this.alarmGain;
  }
  if (this.tick % 5 === 0) this.blur(this.cement, 0.8, 0.2, 0.97);
  // nest weavers publish: a raider flies at the core; the evader flees every 4th worker
  arena.setTargets(this.anchor, 1, null);
  const thr = this._thr || (this._thr = new Uint8Array(this.n)); let w = 0;
  for (let k = 0; k < this.n; k++) { thr[k] = 0; if (this.alive[k]) { if (w % 4 === 0) thr[k] = 1; w++; } }
  arena.setThreats(this.pos, this.n, thr);
  this.defend(arena, dt);
  for (const p of arena.pilots) {
    const dn = Math.hypot(p.pos[0] - this.anchor[0], p.pos[1] - this.anchor[1], p.pos[2] - this.anchor[2]);
    if ((p.policy === 'hunter' || p.policy === 'cutter' || p.rams) && dn < 12 && this.store > 0) { this.crystals += this.store; this.raided += this.store; this.store = 0; }
  }
  if (this.tick % 3 === 0) { let mx = 0; for (let i = 0; i < this.alarm.length; i++) if (this.alarm[i] > mx) mx = this.alarm[i]; if (mx > 1e-3) this.blur(this.alarm, 0.5, 0.5, 0.95); }
};
/** builders/core.py Colony.defend: alarm accumulates while an intruder is inside; below strike_at the workers form
 *  a GUARD SCREEN between core and pilot (the telegraph), at/above it they strike and sting on contact. */
Fortress.prototype.defend = function (arena, dt) {
  const strikeAt = 0.6, rise = 0.6, fall = 0.3, cooldown = 1.5, A = this.anchor, alarmR = this.alarmR, guardR = alarmR * 0.5;
  for (let k = 0; k < this.n; k++) { this.cool[k] -= dt; this.intent[k] = 0; }
  let inside = false, p = null, pd = Infinity, pAll = null, pdAll = Infinity;
  for (const q of arena.pilots) {
    const d = Math.hypot(q.pos[0] - A[0], q.pos[1] - A[1], q.pos[2] - A[2]);
    if (d < alarmR) { inside = true; if (d < pd) { pd = d; p = q; } }
    if (d < pdAll) { pdAll = d; pAll = q; }
  }
  this.alarmLevel = inside ? Math.min(1, this.alarmLevel + rise * dt) : Math.max(0, this.alarmLevel - fall * dt);
  if (this.alarmLevel <= 0) return;
  p = p || pAll; if (!p) return;
  const to = vunit(p.pos[0] - A[0], p.pos[1] - A[1], p.pos[2] - A[2], [0, 0, 0]);
  for (let k = 0; k < this.n; k++) {
    if (!this.alive[k] || this.carry[k] >= 0) continue;
    const dh = Math.hypot(this.pos[3 * k] - A[0], this.pos[3 * k + 1] - A[1], this.pos[3 * k + 2] - A[2]);
    if (!(dh < alarmR * 2.2)) continue;
    if (this.alarmLevel < strikeAt) {
      const wx = this.wander[3 * k], wy = this.wander[3 * k + 1], wz = this.wander[3 * k + 2], pr = wx * to[0] + wy * to[1] + wz * to[2];
      const ox = wx - to[0] * pr, oy = wy - to[1] * pr, oz = wz - to[2] * pr;
      this.steerK(k, A[0] + to[0] * guardR + ox * guardR * 0.6, A[1] + to[1] * guardR + oy * guardR * 0.6, A[2] + to[2] * guardR + oz * guardR * 0.6, dt, this.speed * 1.4);
      this.intent[k] = 0.25 + 0.5 * this.alarmLevel / strikeAt;
    } else {
      this.steerK(k, p.pos[0] + p.vel[0] * 0.25, p.pos[1] + p.vel[1] * 0.25, p.pos[2] + p.vel[2] * 0.25, dt, this.speed * 1.8);
      this.intent[k] = 1;
      const d = Math.hypot(this.pos[3 * k] - p.pos[0], this.pos[3 * k + 1] - p.pos[1], this.pos[3 * k + 2] - p.pos[2]);
      if (d < p.radius + 4 && this.cool[k] <= 0) { arena.hit(p, 'sting', undefined, k); this.cool[k] = cooldown; }
    }
  }
};
/** builders/harness.py ram(): a ramming pilot destroys unshielded structure prisms and kills workers it touches. */
Fortress.prototype.ram = function (arena) {
  for (const p of arena.pilots) {
    if (p.policy !== 'hunter' && p.policy !== 'cutter' && !p.rams) continue;
    const c = arena.massNear(p.pos[0], p.pos[1], p.pos[2], p.radius + 4);
    for (let s = 0; s < c; s++) { const i = arena.qbuf[s]; if (arena.malive[i] && arena.mstruct[i]) arena.destroy(i); }
    for (let k = 0; k < this.n; k++) {
      if (!this.alive[k]) continue;
      if (Math.hypot(this.pos[3 * k] - p.pos[0], this.pos[3 * k + 1] - p.pos[1], this.pos[3 * k + 2] - p.pos[2]) < p.radius + this.size[k] + 1) this.onRammed(arena, k);
    }
  }
};
Fortress.prototype.ledger = function () { return 0; };   // workers hold no body mass; structure + carried stay arena mass
Fortress.prototype.liveCount = function () { let c = 0; for (let i = 0; i < this.n; i++) c += this.alive[i]; return c; };
/** probe view: Colony exposes ALL workers (dead included), like the Python's agent_pos. */
Fortress.prototype.view = function (v) {
  v = v || {}; v.P = this.pos; v.V = this.vel; v.S = this.size; v.I = this.intent; v.m = this.n; v.kills = this.kills; v.crystals = this.crystals; return v;
};
Fortress.prototype.colour = function (k, c) {
  if (this.intent[k] >= 1) { c[0] = 1; c[1] = 0.25; c[2] = 0.3; return c; }
  if (this.intent[k] > 0) { c[0] = 1; c[1] = 0.55; c[2] = 0.35; return c; }
  if (this.carry[k] >= 0) { c[0] = 1; c[1] = 1; c[2] = 0.6; return c; }
  c[0] = 0.95; c[1] = 0.35; c[2] = 0.45; return c;
};
