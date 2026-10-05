// ===================================================================================================================
// 00_core.js - the shared arena, ported from Tools/Ecology/common/arena.py (+ the three harness worlds the species
// were scored in: bestiary/run.py BArena, builders/harness.py, flora/harness.py FloraArena).
// Typed arrays, a hashed spatial grid, no per-step allocation on the hot paths. Mass is CONSERVED: nothing here
// removes a prism on a clock; consume/destroy are the only removals and both are ledgered (audit()).
// ===================================================================================================================
'use strict';

// ---------------------------------------------------------------- rng (mulberry32 + cached Box-Muller)
function Rng(seed) { this.s = (seed * 2654435761) >>> 0 || 1; this._spare = 0; this._has = false; }
Rng.prototype.random = function () {
  let t = (this.s = (this.s + 0x6D2B79F5) | 0);
  t = Math.imul(t ^ (t >>> 15), 1 | t);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
};
Rng.prototype.normal = function (mu, sd) {
  if (mu === undefined) mu = 0; if (sd === undefined) sd = 1;
  if (this._has) { this._has = false; return mu + sd * this._spare; }
  let u = 0, v = 0; while (u === 0) u = this.random(); v = this.random();
  const r = Math.sqrt(-2 * Math.log(u)), th = 6.283185307179586 * v;
  this._spare = r * Math.sin(th); this._has = true;
  return mu + sd * r * Math.cos(th);
};
Rng.prototype.uniform = function (a, b) { return a + (b - a) * this.random(); };
Rng.prototype.int = function (n) { return Math.floor(this.random() * n); };
/** k distinct picks from an int array (partial Fisher-Yates on a copy). */
Rng.prototype.choiceNoReplace = function (arr, k) {
  const a = Array.from(arr); const out = [];
  for (let i = 0; i < k && i < a.length; i++) { const j = i + this.int(a.length - i); const t = a[i]; a[i] = a[j]; a[j] = t; out.push(a[i]); }
  return out;
};

const TAU = 6.283185307179586;
function clamp(x, a, b) { return x < a ? a : x > b ? b : x; }

// ---------------------------------------------------------------- hashed spatial grid (counting sort, dedup'd query)
// A dense grid over a 2.4 km cell at 40 u is 226k buckets per rebuild; a 2^bits hash table keeps rebuilds O(n + 2^bits)
// and a collision only ever adds candidates (callers filter by exact distance), never loses one.
function SpatialHash(bits) {
  this.size = 1 << bits; this.mask = this.size - 1;
  this.start = new Int32Array(this.size + 1); this.fill = new Int32Array(this.size);
  this.items = new Int32Array(1024); this.keys = new Int32Array(1024);
  this.h = 40; this.n = 0; this.buf = new Int32Array(1024);
  this.mark = new Int32Array(this.size); this.gen = 0;   // generation-stamped bucket marks: O(1) dedup, no allocation
}
SpatialHash.prototype.key = function (ix, iy, iz) {
  return ((Math.imul(ix, 73856093) ^ Math.imul(iy, 19349663) ^ Math.imul(iz, 83492791)) >>> 0) & this.mask;
};
/** pos: Float64Array (n*3); ok: optional Uint8Array mask (1 = include). */
SpatialHash.prototype.build = function (pos, n, ok, h) {
  this.h = h; const inv = 1 / h;
  if (this.keys.length < n) { this.keys = new Int32Array(n * 2); this.items = new Int32Array(n * 2); }
  const st = this.start; st.fill(0);
  let m = 0;
  for (let i = 0; i < n; i++) {
    if (ok && !ok[i]) { this.keys[i] = -1; continue; }
    const k = this.key(Math.floor(pos[3 * i] * inv), Math.floor(pos[3 * i + 1] * inv), Math.floor(pos[3 * i + 2] * inv));
    this.keys[i] = k; st[k + 1]++; m++;
  }
  for (let b = 0; b < this.size; b++) st[b + 1] += st[b];
  this.fill.set(st.subarray(0, this.size));
  for (let i = 0; i < n; i++) { const k = this.keys[i]; if (k >= 0) this.items[this.fill[k]++] = i; }
  this.n = m;
};
/** Candidates within the cube around (x,y,z) of half-size r. Returns the count; indices in this.buf. */
SpatialHash.prototype.query = function (x, y, z, r) {
  const inv = 1 / this.h;
  const x0 = Math.floor((x - r) * inv), x1 = Math.floor((x + r) * inv);
  const y0 = Math.floor((y - r) * inv), y1 = Math.floor((y + r) * inv);
  const z0 = Math.floor((z - r) * inv), z1 = Math.floor((z + r) * inv);
  let c = 0;
  if (++this.gen === 0x7fffffff) { this.mark.fill(0); this.gen = 1; }
  const g = this.gen, mk = this.mark;
  for (let ix = x0; ix <= x1; ix++) for (let iy = y0; iy <= y1; iy++) for (let iz = z0; iz <= z1; iz++) {
    const k = this.key(ix, iy, iz);
    if (mk[k] === g) continue;          // two cells hashing to one bucket: visit the bucket once
    mk[k] = g;
    const s0 = this.start[k], s1 = this.start[k + 1];
    if (c + (s1 - s0) > this.buf.length) { const nb = new Int32Array((c + s1 - s0) * 2); nb.set(this.buf.subarray(0, c)); this.buf = nb; }
    for (let s = s0; s < s1; s++) this.buf[c++] = this.items[s];
  }
  return c;
};

// ---------------------------------------------------------------- pilot (common/arena.py Pilot)
function Pilot(policy, speed, name) {
  this.policy = policy; this.speed = speed; this.name = name || policy;
  this.pos = [0, 0, 0]; this.vel = [0, 0, 0]; this.goal = [0, 0, 0]; this.prev = [0, 0, 0];
  this.turn = 2.0; this.radius = 6.0; this.domain = 1; this.trail_every = 0; this.trail_vol = 6.0; this._trail_t = 0;
  this.rams = false;                 // the flight player rams (kills on contact) like the scripted hunter does
  this.hitsN = 0;
}
Pilot.wanderer = () => new Pilot('wander', 120, 'wanderer');
Pilot.hunter = () => new Pilot('hunter', 160, 'hunter');
Pilot.evader = () => new Pilot('evader', 140, 'evader');

// ---------------------------------------------------------------- arena
function Arena(seed, opt) {
  opt = opt || {};
  this.rng = new Rng(seed); this.R = opt.R || 1200; this.nucleus = 200; this.t = 0;
  this.world = opt.world || 'bestiary';
  this.cap = 0; this.n = 0; this._alloc(8192);
  this.grid = new SpatialHash(14); this.gridH = 40; this.qbuf = new Int32Array(1024);
  this.pilots = [];
  this.targets = new Float64Array(3 * 64); this.nTargets = 0;
  this.threats = new Float64Array(3 * 64); this.nThreats = 0;
  this.log = [];
  this.eaten = 0; this.destroyed = 0; this.stolen = 0; this.steals = 0; this.moves = 0;
  this.laid = 0; this.scattered = 0; this.trail_laid = 0; this.created = 0;
  this.trail_spacing = 0; this.trail_vol = 10; this._trailAcc = [];
  this.species = null;        // flora world: the species under test (cutter targets, reader hazards)
  // flora world bookkeeping (flora/harness.py FloraArena)
  this.fl = { trail_gap: 24, trail_vol: 6, acc: {}, slow_t: {}, goal_t: {}, boost_left: {}, boost_cool: {}, base: {} };
  this.onHit = null;          // flight page hook (flash, sound)
  this.slowAllHits = this.world === 'flora';
}
Arena.prototype._alloc = function (cap) {
  const grow = (A, T, k) => { const b = new T(cap * (k || 1)); if (A) b.set(A.subarray(0, Math.min(A.length, cap * (k || 1)))); return b; };
  this.mpos = grow(this.mpos, Float64Array, 3); this.mvol = grow(this.mvol, Float64Array);
  this.melem = grow(this.melem, Int8Array); this.malive = grow(this.malive, Uint8Array);
  this.mshield = grow(this.mshield, Uint8Array); this.mdom = grow(this.mdom, Int8Array);
  this.mdanger = grow(this.mdanger, Uint8Array); this.mtrail = grow(this.mtrail, Uint8Array);
  this.mowner = grow(this.mowner, Int16Array); this.mborn = grow(this.mborn, Float64Array);
  this.mstruct = grow(this.mstruct, Uint8Array);   // builders: registered structure (arena.struct_owner)
  this.mdirty = grow(this.mdirty, Uint8Array);     // render hint: moved / restyled since the page last looked
  this.cap = cap;
};
Arena.prototype._newMass = function () {
  if (this.n >= this.cap) this._alloc(this.cap * 2);
  return this.n++;
};
/** a point volume-uniform in the shell [lo, hi] (common/arena.py _ball). */
Arena.prototype.ball = function (lo, hi, out, o) {
  out = out || [0, 0, 0]; o = o || 0;
  let x = this.rng.normal(), y = this.rng.normal(), z = this.rng.normal();
  const d = Math.sqrt(x * x + y * y + z * z) || 1;
  const u = this.rng.random();
  const r = Math.cbrt(lo * lo * lo + u * (hi * hi * hi - lo * lo * lo));
  out[o] = x / d * r; out[o + 1] = y / d * r; out[o + 2] = z / d * r;
  return out;
};
Arena.prototype.scatterMass = function (n, rlo, rhi, vlo, vhi, clumps) {
  rlo = rlo === undefined ? 0.3 : rlo; rhi = rhi === undefined ? 0.9 : rhi;
  vlo = vlo || 8; vhi = vhi || 40; clumps = clumps || 24;
  const C = new Float64Array(clumps * 3);
  for (let c = 0; c < clumps; c++) this.ball(rlo * this.R, rhi * this.R, C, 3 * c);
  for (let k = 0; k < n; k++) {
    const w = this.rng.int(clumps), i = this._newMass();
    this.mpos[3 * i] = C[3 * w] + this.rng.normal(0, 25); this.mpos[3 * i + 1] = C[3 * w + 1] + this.rng.normal(0, 25);
    this.mpos[3 * i + 2] = C[3 * w + 2] + this.rng.normal(0, 25);
    this.mvol[i] = this.rng.uniform(vlo, vhi); this.melem[i] = w % 4; this.malive[i] = 1;
    this.mshield[i] = 0; this.mdom[i] = 0; this.mdanger[i] = 0; this.mtrail[i] = 0; this.mowner[i] = -1;
    this.mborn[i] = this.t; this.mstruct[i] = 0; this.mdirty[i] = 1;
    this.scattered += this.mvol[i];
  }
  this.rebuildGrid();
};
Arena.prototype.layMass = function (x, y, z, vol, elem, dom, danger, trail, shielded, owner) {
  const i = this._newMass();
  this.mpos[3 * i] = x; this.mpos[3 * i + 1] = y; this.mpos[3 * i + 2] = z;
  this.mvol[i] = vol; this.melem[i] = elem || 0; this.malive[i] = 1; this.mshield[i] = shielded ? 1 : 0;
  this.mdom[i] = dom || 0; this.mdanger[i] = danger ? 1 : 0; this.mtrail[i] = trail ? 1 : 0;
  this.mowner[i] = owner === undefined ? -1 : owner; this.mborn[i] = this.t; this.mstruct[i] = 0; this.mdirty[i] = 1;
  this.laid += vol;
  return i;
};
Arena.prototype.steal = function (i, dom) {
  if (!this.malive[i] || this.mshield[i] || this.mdom[i] === dom) return 0;
  this.mdom[i] = dom; this.stolen += this.mvol[i]; this.steals++; this.mdirty[i] = 1;
  return this.mvol[i];
};
Arena.prototype.moveMass = function (i, x, y, z) {
  this.mpos[3 * i] = x; this.mpos[3 * i + 1] = y; this.mpos[3 * i + 2] = z; this.moves++; this.mdirty[i] = 1;
};
Arena.prototype.destroy = function (i) {
  if (!this.malive[i] || this.mshield[i]) return 0;
  this.malive[i] = 0; this.destroyed += this.mvol[i]; this.mdirty[i] = 1; if (this.onRemove) this.onRemove(i, 'destroy'); return this.mvol[i];
};
Arena.prototype.consume = function (i) {
  if (!this.malive[i] || this.mshield[i]) return 0;
  this.malive[i] = 0; this.eaten += this.mvol[i]; this.mdirty[i] = 1; if (this.onRemove) this.onRemove(i, 'eat'); return this.mvol[i];
};
Arena.prototype.liveVolume = function () {
  let s = 0; for (let i = 0; i < this.n; i++) if (this.malive[i]) s += this.mvol[i]; return s;
};
/** conservation residual: everything ever created - (live + eaten + destroyed). 0 = conserved. */
Arena.prototype.audit = function () {
  return (this.scattered + this.laid) - (this.liveVolume() + this.eaten + this.destroyed);
};
Arena.prototype.rebuildGrid = function () { this.grid.build(this.mpos, this.n, this.malive, this.gridH); };
/** live prisms within r of (x,y,z): count returned, indices in this.qbuf (valid until the next massNear). */
Arena.prototype.massNear = function (x, y, z, r) {
  const c = this.grid.query(x, y, z, r), b = this.grid.buf, r2 = r * r;
  if (this.qbuf.length < c) this.qbuf = new Int32Array(c * 2);
  let m = 0;
  for (let s = 0; s < c; s++) {
    const i = b[s]; if (i >= this.n || !this.malive[i]) continue;
    const dx = this.mpos[3 * i] - x, dy = this.mpos[3 * i + 1] - y, dz = this.mpos[3 * i + 2] - z;
    if (dx * dx + dy * dy + dz * dz <= r2) this.qbuf[m++] = i;
  }
  return m;
};
Arena.prototype.setTargets = function (pos, n, ok) { this.nTargets = this._pub('targets', pos, n, ok); };
Arena.prototype.setThreats = function (pos, n, ok) { this.nThreats = this._pub('threats', pos, n, ok); };
Arena.prototype._pub = function (key, pos, n, ok) {
  let m = 0; for (let i = 0; i < n; i++) if (!ok || ok[i]) m++;
  if (this[key].length < 3 * m) this[key] = new Float64Array(3 * m * 2);
  const A = this[key]; let w = 0;
  for (let i = 0; i < n; i++) if (!ok || ok[i]) { A[3 * w] = pos[3 * i]; A[3 * w + 1] = pos[3 * i + 1]; A[3 * w + 2] = pos[3 * i + 2]; w++; }
  return m;
};
Arena.prototype.addPilot = function (p) {
  this.ball(0.5 * this.R, 0.8 * this.R, p.pos);
  let x = this.rng.normal(), y = this.rng.normal(), z = this.rng.normal(); const d = Math.hypot(x, y, z) || 1;
  p.vel = [x / d * p.speed, y / d * p.speed, z / d * p.speed];
  this.ball(0.2 * this.R, 0.9 * this.R, p.goal);
  if (this.world === 'flora') {
    this.grove(0, p.pos); this.grove(0, p.goal);
    this.fl.boost_left[p.name] = 0; this.fl.boost_cool[p.name] = 0; this.fl.base[p.name] = p.speed; this.fl.goal_t[p.name] = 0;
  }
  p.prev = p.pos.slice();
  this._trailAcc.push(0);
  this.pilots.push(p);
  return p;
};
/** who = the striking agent's index in its species (instrumentation only: TeleTrack reads that agent's own intent);
 *  standing = the contact was a standing danger prism (always shown hot), not a strike. Neither changes behaviour. */
Arena.prototype.hit = function (p, kind, amount, who, standing) {
  this.log.push([this.t, p.name, kind, amount === undefined ? 1 : amount, this._src]); p.hitsN++;
  if (this.slowAllHits || kind === 'burn' || kind === 'snap') this.fl.slow_t[p.name] = this.t;   // a danger contact slows
  if (this.onHit) this.onHit(p, kind, amount, this._src, who, standing);
};
/** flora/harness.py speed_factor: a burn stops you (1.5 x clamped) and you recover linearly over 3 s. */
Arena.prototype.speedFactor = function (p) {
  const t0 = this.fl.slow_t[p.name]; if (t0 === undefined) return 1;
  const x = (this.t - t0) / (this.SLOW_S || 3.0); return x >= 1 ? 1 : Math.max(0.1, 1 - (this.SLOW_STRENGTH || 1.5) * (1 - x));
};
Arena.prototype.enableTrails = function (spacing, vol) { this.trail_spacing = spacing; this.trail_vol = vol; return this; };

const _g = [0, 0, 0];
Arena.prototype.step = function (dt) {
  const flora = this.world === 'flora';
  for (let k = 0; k < this.pilots.length; k++) {
    const p = this.pilots[k];
    p.prev[0] = p.pos[0]; p.prev[1] = p.pos[1]; p.prev[2] = p.pos[2];
    if (flora) {
      const f = this.fl;
      f.boost_left[p.name] = Math.max(0, f.boost_left[p.name] - dt); f.boost_cool[p.name] = Math.max(0, f.boost_cool[p.name] - dt);
      if (p.policy !== 'reader') p.speed = f.base[p.name];
      p.speed *= this.speedFactor(p);
    }
    if (p.policy !== 'player') {
      this.pilotGoal(p, _g);
      const wx = _g[0] - p.pos[0], wy = _g[1] - p.pos[1], wz = _g[2] - p.pos[2];
      const n = Math.sqrt(wx * wx + wy * wy + wz * wz);
      if (n > 1e-6) {
        const vn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-6);
        const vx = p.vel[0] / vn, vy = p.vel[1] / vn, vz = p.vel[2] / vn, ux = wx / n, uy = wy / n, uz = wz / n;
        const ang = Math.acos(clamp(vx * ux + vy * uy + vz * uz, -1, 1));
        const kk = Math.min(1, p.turn * dt / Math.max(ang, 1e-6));
        let nx = vx + (ux - vx) * kk, ny = vy + (uy - vy) * kk, nz = vz + (uz - vz) * kk;
        const nn = Math.max(Math.hypot(nx, ny, nz), 1e-6);
        p.vel[0] = nx / nn * p.speed; p.vel[1] = ny / nn * p.speed; p.vel[2] = nz / nn * p.speed;
      }
    }
    p.pos[0] += p.vel[0] * dt; p.pos[1] += p.vel[1] * dt; p.pos[2] += p.vel[2] * dt;
    const r = Math.hypot(p.pos[0], p.pos[1], p.pos[2]);
    if (r > this.R * 0.97) { const s = this.R * 0.97 / r; p.pos[0] *= s; p.pos[1] *= s; p.pos[2] *= s; }
    const vn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-6);
    if (this.trail_spacing) {              // bestiary: a prism every trail_spacing u, owned by pilot k
      let acc = this._trailAcc[k] + p.speed * dt;
      while (acc >= this.trail_spacing) {
        acc -= this.trail_spacing;
        const b = (p.radius + 4 + acc) / vn;
        this.layMass(p.pos[0] - p.vel[0] * b, p.pos[1] - p.vel[1] * b, p.pos[2] - p.vel[2] * b, this.trail_vol,
                     k % 4, this.trailDom || 0, false, !!this.trailFlag, false, k);
        this.trail_laid += this.trail_vol;
      }
      this._trailAcc[k] = acc;
    }
    if (p.trail_every > 0) {               // builders: a domain trail prism every trail_every s
      p._trail_t += dt;
      while (p._trail_t >= p.trail_every) {
        p._trail_t -= p.trail_every;
        const b = p.radius * 2 / vn;
        this.layMass(p.pos[0] - p.vel[0] * b, p.pos[1] - p.vel[1] * b, p.pos[2] - p.vel[2] * b, p.trail_vol, 0, p.domain, false, true);
        this.trail_laid += p.trail_vol;
      }
    }
    if (flora) {                           // flora: a 6-vol prism 8 u behind every 24 u flown
      const dx = p.pos[0] - p.prev[0], dy = p.pos[1] - p.prev[1], dz = p.pos[2] - p.prev[2];
      const L = Math.sqrt(dx * dx + dy * dy + dz * dz);
      const f = this.fl; f.acc[p.name] = (f.acc[p.name] || 0) + L;
      if (f.acc[p.name] >= f.trail_gap) {
        f.acc[p.name] = 0; const s = 8 / Math.max(L, 1e-6);
        this.layMass(p.prev[0] - dx * s, p.prev[1] - dy * s, p.prev[2] - dz * s, f.trail_vol, 0);
        this.created += f.trail_vol; this.trail_laid += f.trail_vol;
      }
    }
  }
  this.rebuildGrid();
  this.t += dt;
};
Arena.prototype.pilotGoal = function (p, out) {
  if (this.world === 'flora') return this.floraGoal(p, out);
  if (p.policy === 'hunter' && this.nTargets) return this._nearestOf(this.targets, this.nTargets, p.pos, out);
  if (p.policy === 'evader' && this.nThreats) {
    this._nearestOf(this.threats, this.nThreats, p.pos, out);
    let ax = p.pos[0] - out[0], ay = p.pos[1] - out[1], az = p.pos[2] - out[2];
    // SWERVE (DISCOVERIES "Lurker counterplay"): a threat AHEAD (away points behind) is fled sideways first - aiming
    // straight back makes the shared lerp turn crawl (~0.2 rad/s near 180 deg), so the pilot flew into the ambush
    const vn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-6), vx = p.vel[0] / vn, vy = p.vel[1] / vn, vz = p.vel[2] / vn;
    const dot = ax * vx + ay * vy + az * vz;
    if (dot < 0) {
      ax -= dot * vx; ay -= dot * vy; az -= dot * vz;
      if (Math.hypot(ax, ay, az) < 1e-3 * Math.abs(dot)) { ax = -vz; ay = 0; az = vx; if (Math.hypot(ax, az) < 1e-6) { ax = 1; az = 0; } }
    }
    const n = Math.max(Math.hypot(ax, ay, az), 1e-6);
    out[0] = p.pos[0] + ax / n * 300; out[1] = p.pos[1] + ay / n * 300; out[2] = p.pos[2] + az / n * 300; return out;
  }
  if (Math.hypot(p.goal[0] - p.pos[0], p.goal[1] - p.pos[1], p.goal[2] - p.pos[2]) < 60) this.ball(0.2 * this.R, 0.9 * this.R, p.goal);
  out[0] = p.goal[0]; out[1] = p.goal[1]; out[2] = p.goal[2]; return out;
};
Arena.prototype._nearestOf = function (A, n, q, out) {
  let best = 0, bd = Infinity;
  for (let i = 0; i < n; i++) { const d = (A[3 * i] - q[0]) ** 2 + (A[3 * i + 1] - q[1]) ** 2 + (A[3 * i + 2] - q[2]) ** 2; if (d < bd) { bd = d; best = i; } }
  out[0] = A[3 * best]; out[1] = A[3 * best + 1]; out[2] = A[3 * best + 2]; return out;
};

// ---------------------------------------------------------------- flora world (flora/harness.py, n_crystals = 0)
Arena.prototype.grove = function (margin, out, o) {
  const C = this.GROVE_C || [0, 0, 600], GR = this.GROVE_R || 450;
  this.ball(0, GR - (margin || 0), out, o); o = o || 0;
  out[o] += C[0]; out[o + 1] += C[1]; out[o + 2] += C[2]; return out;
};
Arena.prototype.groveMass = function (n, clumps, vlo, vhi, spread) {
  clumps = clumps || 20; vlo = vlo || 8; vhi = vhi || 40; spread = spread || 25;
  const C = new Float64Array(clumps * 3);
  for (let c = 0; c < clumps; c++) this.grove(40, C, 3 * c);
  for (let k = 0; k < n; k++) {
    const w = this.rng.int(clumps), i = this._newMass();
    for (let a = 0; a < 3; a++) this.mpos[3 * i + a] = C[3 * w + a] + this.rng.normal(0, spread);
    this.mvol[i] = this.rng.uniform(vlo, vhi); this.melem[i] = w % 4; this.malive[i] = 1; this.mowner[i] = -1;
    this.mborn[i] = this.t; this.mdirty[i] = 1;
    this.scattered += this.mvol[i];
  }
  this.rebuildGrid();
};
Arena.prototype.floraGoal = function (p, out) {
  const f = this.fl;
  if (p.policy === 'cutter' && this.species) {
    const n = this.species.cutTargets(this._ct || (this._ct = { a: new Float64Array(3 * 512), n: 0 }));
    if (n) return this._nearestOf(this._ct.a, n, p.pos, out);
  }
  if (Math.hypot(p.goal[0] - p.pos[0], p.goal[1] - p.pos[1], p.goal[2] - p.pos[2]) < 60) { this.grove(0, p.goal); f.goal_t[p.name] = this.t; }
  else if (this.t - (f.goal_t[p.name] || 0) > 15) { this.grove(0, p.goal); f.goal_t[p.name] = this.t; }
  if (p.policy === 'reader' && this.species) return this.readerGoal(p, out);
  out[0] = p.goal[0]; out[1] = p.goal[1]; out[2] = p.goal[2]; return out;
};
// 26 Fibonacci directions (flora/harness.py _fib)
const FIB26 = (function () {
  const D = []; for (let i = 0; i < 26; i++) {
    const t = i + 0.5, phi = Math.acos(1 - 2 * t / 26), th = Math.PI * (1 + Math.sqrt(5)) * t;
    D.push([Math.cos(th) * Math.sin(phi), Math.sin(th) * Math.sin(phi), Math.cos(phi)]);
  } return D;
})();
/** the READER pilot: context steering over 26 directions scored by goal alignment minus VISIBLE danger. */
Arena.prototype.readerGoal = function (p, out) {
  const f = this.fl, base = f.base[p.name];
  const H = this.species.hazards();   // {pos Float64Array, rad, w, n, soft:{pos,n}}
  const goal = p.goal;
  let wx = goal[0] - p.pos[0], wy = goal[1] - p.pos[1], wz = goal[2] - p.pos[2]; const wn = Math.max(Math.hypot(wx, wy, wz), 1e-6);
  wx /= wn; wy /= wn; wz /= wn;
  const vn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-6), vx = p.vel[0] / vn, vy = p.vel[1] / vn, vz = p.vel[2] / vn;
  let speed = base * (f.boost_left[p.name] > 0 ? 1.7 : 1.0);
  const idx = [];
  for (let h = 0; h < H.n; h++) if (Math.hypot(H.pos[3 * h] - p.pos[0], H.pos[3 * h + 1] - p.pos[1], H.pos[3 * h + 2] - p.pos[2]) < 260) idx.push(h);
  if (!idx.length) { p.speed = speed * this.speedFactor(p); out[0] = goal[0]; out[1] = goal[1]; out[2] = goal[2]; return out; }
  const dirs = [[wx, wy, wz], [vx, vy, vz]].concat(FIB26);
  const look = [40, 90, 150, 220], lw = [1.0, 0.7, 0.4, 0.2];
  let best = 0, bs = -Infinity, bestDanger = 0;
  for (let c = 0; c < dirs.length; c++) {
    const D = dirs[c]; let danger = 0;
    for (let l = 0; l < 4; l++) {
      const qx = p.pos[0] + D[0] * look[l], qy = p.pos[1] + D[1] * look[l], qz = p.pos[2] + D[2] * look[l];
      let mx = 0;
      for (const h of idx) {
        const dd = Math.hypot(qx - H.pos[3 * h], qy - H.pos[3 * h + 1], qz - H.pos[3 * h + 2]);
        const v = clamp(1 - (dd - H.rad[h]) / 25, 0, 1) * H.w[h]; if (v > mx) mx = v;
      }
      danger += mx * lw[l];
    }
    const score = (D[0] * wx + D[1] * wy + D[2] * wz) + 0.6 * (D[0] * vx + D[1] * vy + D[2] * vz) - 2.5 * danger;
    if (score > bs) { bs = score; best = c; bestDanger = danger; }
  }
  if (bestDanger > 0.6 && f.boost_cool[p.name] <= 0 && f.boost_left[p.name] <= 0) { f.boost_left[p.name] = 1.0; f.boost_cool[p.name] = 4.0; }
  p.speed = speed * this.speedFactor(p);
  out[0] = p.pos[0] + dirs[best][0] * 200; out[1] = p.pos[1] + dirs[best][1] * 200; out[2] = p.pos[2] + dirs[best][2] * 200;
  return out;
};

// ---------------------------------------------------------------- the bestiary's Herd base (bestiary/core.py)
function Herd(arena, n, centre, spread, size, body) {
  this.n = n; this.rng = arena.rng; this.R = arena.R;
  if (!centre) centre = arena.ball(0.35 * arena.R, 0.7 * arena.R);
  if (spread === undefined) spread = 60;
  this.pos = new Float64Array(3 * n); this.vel = new Float64Array(3 * n);
  for (let i = 0; i < n; i++) for (let a = 0; a < 3; a++) { this.pos[3 * i + a] = centre[a] + this.rng.normal(0, spread); this.vel[3 * i + a] = this.rng.normal(0, 5); }
  this.size = new Float64Array(n).fill(size === undefined ? 3 : size);
  this.alive = new Uint8Array(n).fill(1);
  this.intent = new Float64Array(n);
  this.body = new Float64Array(n).fill(body === undefined ? 6 : body);
  this.gut = new Float64Array(n);
  this.kills = 0; this.crystals = 0; this.deaths = 0; this.carried = 0; this.t = 0;
  this.aspect = 1; this.heading = null;
  this.off = new Float64Array(3 * n); this.dist = new Float64Array(n); this.pk = new Int32Array(n);
  this.des = new Float64Array(3 * n);
  this.nbr = new SpatialHash(10);
}
Herd.prototype.ledger = function () { let s = this.carried; for (let i = 0; i < this.n; i++) if (this.alive[i]) s += this.body[i] + this.gut[i]; return s; };
Herd.prototype.liveCount = function () { let c = 0; for (let i = 0; i < this.n; i++) c += this.alive[i]; return c; };
Herd.prototype.kill = function (i, arena, byPilot) {
  if (!this.alive[i]) return;
  this.alive[i] = 0; this.deaths++; if (byPilot !== false) this.kills++; this.crystals++;
  const v = this.body[i] + this.gut[i];
  if (v > 0) arena.layMass(this.pos[3 * i], this.pos[3 * i + 1], this.pos[3 * i + 2], v, i % 4);   // skeleton: mass stays
  this.body[i] = 0; this.gut[i] = 0;
  if (arena.onKill) arena.onKill(this, i);
};
/** offset to / distance to / index of the NEAREST pilot, for every agent (dead ones too, like numpy). */
Herd.prototype.pilotVectors = function (arena) {
  const P = arena.pilots, n = this.n;
  for (let i = 0; i < n; i++) {
    let bd = Infinity, bk = 0, bx = 0, by = 0, bz = 0;
    for (let k = 0; k < P.length; k++) {
      const dx = P[k].pos[0] - this.pos[3 * i], dy = P[k].pos[1] - this.pos[3 * i + 1], dz = P[k].pos[2] - this.pos[3 * i + 2];
      const d = Math.sqrt(dx * dx + dy * dy + dz * dz); if (d < bd) { bd = d; bk = k; bx = dx; by = dy; bz = dz; }
    }
    this.off[3 * i] = bx; this.off[3 * i + 1] = by; this.off[3 * i + 2] = bz; this.dist[i] = bd; this.pk[i] = bk;
  }
};
Herd.prototype.hunterContacts = function (arena, extra, canKill) {
  if (extra === undefined) extra = 4;
  for (const p of arena.pilots) {
    if (p.policy !== 'hunter' && !p.rams) continue;
    for (let i = 0; i < this.n; i++) {
      if (!this.alive[i] || (canKill && !canKill[i])) continue;
      const d = Math.hypot(this.pos[3 * i] - p.pos[0], this.pos[3 * i + 1] - p.pos[1], this.pos[3 * i + 2] - p.pos[2]);
      if (d < p.radius + this.size[i] + extra) this.kill(i, arena);
    }
  }
};
Herd.prototype.publish = function (arena, threatMask, targetMask) {
  const n = this.n, a = this.alive;
  const tm = this._tm || (this._tm = new Uint8Array(n)), th = this._th || (this._th = new Uint8Array(n));
  for (let i = 0; i < n; i++) { tm[i] = a[i] && (!targetMask || targetMask[i]) ? 1 : 0; th[i] = a[i] && (!threatMask || threatMask[i]) ? 1 : 0; }
  arena.setTargets(this.pos, n, tm); arena.setThreats(this.pos, n, th);
};
Herd.prototype.step = function (arena, dt) {
  this.t += dt; this.act(arena, dt);
  for (let i = 0; i < this.n; i++) if (this.alive[i]) for (let a = 0; a < 3; a++) this.pos[3 * i + a] += this.vel[3 * i + a] * dt;
};
/** rebuild the neighbour hash over LIVE agents (numpy's all-pairs with dead columns masked out). */
Herd.prototype.buildNbr = function (h) { this.nbr.build(this.pos, this.n, this.alive, h); };
/** steer: vel_i += clamp_len(desired_i - vel_i, accel*dt) (bestiary/core.py steer). */
Herd.prototype.steerTo = function (V, i, dx, dy, dz, adt) {
  let ex = dx - V[3 * i], ey = dy - V[3 * i + 1], ez = dz - V[3 * i + 2];
  const n = Math.sqrt(ex * ex + ey * ey + ez * ez), s = Math.min(1, adt / Math.max(n, 1e-9));
  V[3 * i] += ex * s; V[3 * i + 1] += ey * s; V[3 * i + 2] += ez * s;
};
/** soft membrane (bestiary/core.py contain). */
Herd.prototype.containAll = function () {
  const R = this.R, k = 0.92;
  for (let i = 0; i < this.n; i++) {
    const x = this.pos[3 * i], y = this.pos[3 * i + 1], z = this.pos[3 * i + 2];
    const r = Math.sqrt(x * x + y * y + z * z), over = Math.max(0, (r - k * R) / (0.06 * R));
    if (over > 0) { const s = over * 80 / Math.max(r, 1e-6); this.vel[3 * i] -= x * s; this.vel[3 * i + 1] -= y * s; this.vel[3 * i + 2] -= z * s; }
  }
};
/** separation push for agent i over live neighbours within radius (w = 1 - d/r), accumulated into out[3*i..]. */
Herd.prototype.sepInto = function (i, radius, out, scale) {
  const c = this.nbr.query(this.pos[3 * i], this.pos[3 * i + 1], this.pos[3 * i + 2], radius), b = this.nbr.buf;
  let sx = 0, sy = 0, sz = 0;
  for (let s = 0; s < c; s++) {
    const j = b[s]; if (j === i) continue;
    const dx = this.pos[3 * j] - this.pos[3 * i], dy = this.pos[3 * j + 1] - this.pos[3 * i + 1], dz = this.pos[3 * j + 2] - this.pos[3 * i + 2];
    const d = Math.sqrt(dx * dx + dy * dy + dz * dz); if (d >= radius) continue;
    const w = 1 - d / radius, dd = Math.max(d, 1e-6);
    sx -= dx / dd * w; sy -= dy / dd * w; sz -= dz / dd * w;
  }
  out[3 * i] += sx * scale; out[3 * i + 1] += sy * scale; out[3 * i + 2] += sz * scale;
};
/** the probe's view (bestiary ScoreView): live agents only, compacted. */
Herd.prototype.view = function (v) {
  v = v || {}; let m = 0; for (let i = 0; i < this.n; i++) m += this.alive[i];
  if (!v.P || v.P.length < 3 * m) { v.P = new Float64Array(3 * Math.max(m, 1)); v.V = new Float64Array(3 * Math.max(m, 1)); v.S = new Float64Array(Math.max(m, 1)); v.I = new Float64Array(Math.max(m, 1)); }
  let w = 0;
  for (let i = 0; i < this.n; i++) if (this.alive[i]) {
    for (let a = 0; a < 3; a++) { v.P[3 * w + a] = this.pos[3 * i + a]; v.V[3 * w + a] = this.vel[3 * i + a]; }
    v.S[w] = this.size[i]; v.I[w] = this.intent[i]; w++;
  }
  v.m = m; v.kills = this.kills; v.crystals = this.crystals; return v;
};

/** environment prisms a species may seat on. With o.near (flight page placement only, never in the fidelity
 *  worlds) the list is restricted to the `o.nearK` prisms nearest that point - WHERE a species starts, never HOW it
 *  behaves. */
function envPrisms(arena, o) {
  const env = []; for (let i = 0; i < arena.n; i++) if (arena.mowner[i] < 0 && arena.malive[i]) env.push(i);
  if (!o || !o.near) return env;
  const q = o.near, d = i => (arena.mpos[3 * i] - q[0]) ** 2 + (arena.mpos[3 * i + 1] - q[1]) ** 2 + (arena.mpos[3 * i + 2] - q[2]) ** 2;
  env.sort((a, b) => d(a) - d(b)); return env.slice(0, o.nearK || 160);
}
// small vector helpers on [x,y,z] arrays (cold paths only)
function vunit(x, y, z, out) { const n = Math.max(Math.sqrt(x * x + y * y + z * z), 1e-9); out[0] = x / n; out[1] = y / n; out[2] = z / n; return out; }
