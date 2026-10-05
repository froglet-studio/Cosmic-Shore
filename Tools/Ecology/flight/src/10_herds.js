// ===================================================================================================================
// 10_herds.js - the bestiary species (bestiary/species/*.py) and flight's grazer (flight/grazer.py), ported line by
// line. Every number a species is TUNED by is read from params.json (exported from the Python by
// export_params.py); inline literals are the same as the Python's and are hash-guarded by port_manifest.json.
// numpy all-pairs blocks become a neighbour hash over live agents - the same set for every bounded radius.
// ===================================================================================================================

function mkHerd(self, arena, P, n, centre) {
  const h = P.herd;
  Herd.call(self, arena, n, centre, h.spread, h.size, h.body);
}
function inherit(C) { C.prototype = Object.create(Herd.prototype); C.prototype.constructor = C; return C; }
function C_(P, k, o) { return (o && o[k] !== undefined) ? o[k] : P.const[k]; }

// ------------------------------------------------------------------------------------------------ PACK (dread)
const Pack = inherit(function Pack(arena, P, o) {
  o = o || {}; this.name = 'pack';
  this.CRUISE = C_(P, 'CRUISE', o); this.SPRINT = C_(P, 'SPRINT', o); this.RANGE = C_(P, 'RANGE', o); this.WINDUP = C_(P, 'WINDUP', o);
  const n = o.n || P.init.n;
  mkHerd(this, arena, P, n, o.centre);
  this.aspect = 2.6;
  this.weave = new Float64Array(n); this.gait = new Float64Array(n);
  for (let i = 0; i < n; i++) this.weave[i] = this.rng.uniform(0, 6.28);
  for (let i = 0; i < n; i++) this.gait[i] = this.rng.uniform(0.92, 1.08);
  this.stamina = new Float64Array(n).fill(3); this.cool = new Float64Array(n); this.closure = new Float64Array(n);
  this.wind = new Float64Array(n);   // seconds this hunter's own telegraph has shown (fair burns: bites need WINDUP)
  this.strikes = 0; this.ablate = o.ablate || null;
  this.b = new Float64Array(3 * n); this.pred = new Float64Array(3 * n); this.same = new Uint8Array(n * n);
  this.heading = new Float64Array(3 * n);
});
Pack.prototype.act = function (arena, dt) {
  const n = this.n, A = this.alive, X = this.pos, V = this.vel;
  let any = false; for (let i = 0; i < n; i++) if (A[i]) { any = true; break; } if (!any) return;
  this.pilotVectors(arena);
  const PL = arena.pilots, off = this.off, dist = this.dist, k = this.pk, b = this.b, pred = this.pred, same = this.same;
  const SPRINT = this.SPRINT, CRUISE = this.CRUISE, RANGE = this.RANGE;
  for (let i = 0; i < n; i++) {
    const p = PL[k[i]], tau = clamp(dist[i] / SPRINT, 0, 2);
    for (let a = 0; a < 3; a++) pred[3 * i + a] = p.pos[a] + p.vel[a] * tau;
    const bx = X[3 * i] - pred[3 * i], by = X[3 * i + 1] - pred[3 * i + 1], bz = X[3 * i + 2] - pred[3 * i + 2];
    const bn = Math.max(Math.sqrt(bx * bx + by * by + bz * bz), 1e-9);
    b[3 * i] = bx / bn; b[3 * i + 1] = by / bn; b[3 * i + 2] = bz / bn;
  }
  for (let i = 0; i < n; i++) for (let j = 0; j < n; j++)
    same[i * n + j] = (k[i] === k[j] && A[j] && A[i] && dist[j] < 350 && dist[i] < RANGE) ? 1 : 0;
  let cx = 0, cy = 0, cz = 0, na = 0;
  for (let i = 0; i < n; i++) if (A[i]) { cx += X[3 * i]; cy += X[3 * i + 1]; cz += X[3 * i + 2]; na++; }
  cx /= na; cy /= na; cz /= na;
  const des = this.des, ab = this.ablate, striking = this._st || (this._st = new Uint8Array(n));
  for (let i = 0; i < n; i++) {
    const p = PL[k[i]];
    let ax = 0, ay = 0, az = 0, cnt = 1, rx = b[3 * i], ry = b[3 * i + 1], rz = b[3 * i + 2];
    for (let j = 0; j < n; j++) {
      if (!same[i * n + j]) continue;
      const cos = b[3 * i] * b[3 * j] + b[3 * i + 1] * b[3 * j + 1] + b[3 * i + 2] * b[3 * j + 2];
      const pu = Math.max(cos - 0.2, 0);
      ax -= pu * (b[3 * j] - cos * b[3 * i]); ay -= pu * (b[3 * j + 1] - cos * b[3 * i + 1]); az -= pu * (b[3 * j + 2] - cos * b[3 * i + 2]);
      cnt++; rx += b[3 * j]; ry += b[3 * j + 1]; rz += b[3 * j + 2];
    }
    const res = Math.sqrt(rx * rx + ry * ry + rz * rz) / cnt;
    let closure = clamp((1 - res) * clamp((cnt - 1) / 3, 0, 1) * 1.6, 0, 1);
    if (!(dist[i] < 350)) closure = 0;
    this.closure[i] = closure;
    const pvn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-9);
    let wx = b[3 * i] + 1.2 * ax + 0.9 * p.vel[0] / pvn, wy = b[3 * i + 1] + 1.2 * ay + 0.9 * p.vel[1] / pvn, wz = b[3 * i + 2] + 1.2 * az + 0.9 * p.vel[2] / pvn;
    let st = closure > 0.55 && this.stamina[i] > 0.3 && this.cool[i] <= 0;
    let px = pred[3 * i], py = pred[3 * i + 1], pz = pred[3 * i + 2];
    if (ab === 'chase') { px = p.pos[0]; py = p.pos[1]; pz = p.pos[2]; wx = b[3 * i]; wy = b[3 * i + 1]; wz = b[3 * i + 2]; st = dist[i] < RANGE && this.stamina[i] > 0.3 && this.cool[i] <= 0; }
    else if (ab === 'noquorum') st = dist[i] < 150 && this.stamina[i] > 0.3 && this.cool[i] <= 0;
    striking[i] = st ? 1 : 0;
    const wn = Math.max(Math.hypot(wx, wy, wz), 1e-9); wx /= wn; wy /= wn; wz /= wn;
    const ring = st ? 0 : clamp(dist[i] * 0.5, 120, 220);
    const gx = px + wx * ring - X[3 * i], gy = py + wy * ring - X[3 * i + 1], gz = pz + wz * ring - X[3 * i + 2];
    const gn = Math.max(Math.hypot(gx, gy, gz), 1e-9), sp = st ? SPRINT : CRUISE * this.gait[i];
    des[3 * i] = gx / gn * sp; des[3 * i + 1] = gy / gn * sp; des[3 * i + 2] = gz / gn * sp;
    if (dist[i] > RANGE) {   // roam with the pack
      const qx = cx - X[3 * i] + this.rng.normal(0, 40), qy = cy - X[3 * i + 1] + this.rng.normal(0, 40), qz = cz - X[3 * i + 2] + this.rng.normal(0, 40);
      const qn = Math.max(Math.hypot(qx, qy, qz), 1e-9);
      des[3 * i] = qx / qn * 40; des[3 * i + 1] = qy / qn * 40; des[3 * i + 2] = qz / qn * 40;
    }
  }
  // separation over live packmates (radius 30, x60)
  for (let i = 0; i < n; i++) {
    let sx = 0, sy = 0, sz = 0;
    for (let j = 0; j < n; j++) {
      if (j === i || !A[j]) continue;
      const dx = X[3 * j] - X[3 * i], dy = X[3 * j + 1] - X[3 * i + 1], dz = X[3 * j + 2] - X[3 * i + 2];
      const d = Math.sqrt(dx * dx + dy * dy + dz * dz); if (d >= 30) continue;
      const w = 1 - d / 30, dd = Math.max(d, 1e-6); sx -= dx / dd * w; sy -= dy / dd * w; sz -= dz / dd * w;
    }
    des[3 * i] += sx * 60; des[3 * i + 1] += sy * 60; des[3 * i + 2] += sz * 60;
  }
  for (let i = 0; i < n; i++) {
    // stalking weave on each hunter's own phase
    // side = unit(cross(desired, [0,1,0]) + 1e-6); cross(d, y) = (-dz, 0, dx)
    const sx = -des[3 * i + 2] + 1e-6, sy = 1e-6, sz = des[3 * i] + 1e-6;
    const sn = Math.max(Math.hypot(sx, sy, sz), 1e-9);
    const amp = Math.sin(1.3 * this.t + this.weave[i]) * 35 * (1 - striking[i]) * (dist[i] < RANGE ? 1 : 0);
    des[3 * i] += sx / sn * amp; des[3 * i + 1] += sy / sn * amp; des[3 * i + 2] += sz / sn * amp;
    if (this.cool[i] > 0) {   // winded: fall back and widen
      const p = PL[k[i]]; const bx = X[3 * i] - p.pos[0], by = X[3 * i + 1] - p.pos[1], bz = X[3 * i + 2] - p.pos[2];
      const bn = Math.max(Math.hypot(bx, by, bz), 1e-9);
      des[3 * i] = bx / bn * CRUISE; des[3 * i + 1] = by / bn * CRUISE; des[3 * i + 2] = bz / bn * CRUISE;
    }
    this.steerTo(V, i, des[3 * i], des[3 * i + 1], des[3 * i + 2], 260 * dt);
  }
  this.containAll();
  for (let i = 0; i < n; i++) {
    this.stamina[i] = striking[i] ? this.stamina[i] - dt : Math.min(3, this.stamina[i] + 0.5 * dt);
    this.cool[i] = Math.max(0, this.cool[i] - dt);
    this.intent[i] = this.cool[i] > 0 ? 0 : this.closure[i];
    this.wind[i] = this.intent[i] > 0.5 ? this.wind[i] + dt : this.intent[i] < 0.2 ? 0 : this.wind[i];
    // they WATCH you: heading = toward the predicted pilot while in range
    let hx, hy, hz;
    if (dist[i] < RANGE) { hx = pred[3 * i] - X[3 * i]; hy = pred[3 * i + 1] - X[3 * i + 1]; hz = pred[3 * i + 2] - X[3 * i + 2]; }
    else { hx = V[3 * i]; hy = V[3 * i + 1]; hz = V[3 * i + 2]; }
    const hn = Math.max(Math.hypot(hx, hy, hz), 1e-9);
    this.heading[3 * i] = hx / hn; this.heading[3 * i + 1] = hy / hn; this.heading[3 * i + 2] = hz / hn;
  }
  const r0 = PL[0].radius, bite = [];
  for (let i = 0; i < n; i++) if (A[i] && dist[i] < r0 + this.size[i] + 4 && this.cool[i] <= 0 && this.wind[i] >= this.WINDUP - 1e-9) bite.push(i);
  for (const i of bite) {
    arena.hit(PL[k[i]], 'bite', undefined, i); this.strikes++;
    for (let j = 0; j < n; j++) if (same[i * n + j] || j === i) this.cool[j] = 3.0;
  }
  this.hunterContacts(arena);
  this.publish(arena);
};
Pack.prototype.colour = function (i, c) {
  if (this.cool[i] > 0) { c[0] = 0.35; c[1] = 0.3; c[2] = 0.3; return c; }
  c[0] = 0.55 + 0.45 * this.closure[i]; c[1] = 0.25; c[2] = 0.2; return c;
};

// ------------------------------------------------------------------------------------------------ THIEF (mischief)
const Thief = inherit(function Thief(arena, P, o) {
  o = o || {}; this.name = 'thief';
  for (const kk of ['FREE_V', 'LADEN_V', 'SCOUT', 'WARM', 'SPOT', 'THIEF_OWNER']) this[kk] = C_(P, kk, o);
  const n = o.n || P.init.n;
  mkHerd(this, arena, P, n, o.centre);
  const env = envPrisms(arena, o);
  const e = env[this.rng.int(env.length)];
  this.nest = [arena.mpos[3 * e], arena.mpos[3 * e + 1], arena.mpos[3 * e + 2]];
  for (let i = 0; i < n; i++) for (let a = 0; a < 3; a++) this.pos[3 * i + a] = this.nest[a] + this.rng.normal(0, 20);
  this.claim = new Int32Array(n).fill(-1); this.carry = new Int32Array(n).fill(-1);
  this.stolenBy = new Map(); this.steals = 0; this.stolenVol = 0; this.recaptured = 0; this.hoard = 0;
  this.ablate = o.ablate || null;
});
Thief.prototype.act = function (arena, dt) {
  const n = this.n, A = this.alive, X = this.pos, V = this.vel, des = this.des;
  this.pilotVectors(arena);
  const off = this.off, dist = this.dist, k = this.pk, PL = arena.pilots;
  const warm = this.ablate === 'cold' ? 1e9 : this.WARM;
  // the warm wake: live, pilot-owned trail prisms laid within `warm` s (mborn is the arena's laid-time ledger)
  // laid_t: when THIS species first saw each prism (its own clock, as the Python does)
  if (!this.laidT || this.laidT.length < arena.n) { const b = new Float64Array(Math.max(arena.n * 2, 1024)); if (this.laidT) b.set(this.laidT.subarray(0, this.nSeen)); this.laidT = b; }
  for (let i = this.nSeen || 0; i < arena.n; i++) this.laidT[i] = this.t;
  this.nSeen = arena.n;
  const LT = this.laidT;
  const trail = this._trail || (this._trail = []); trail.length = 0;
  for (let i = arena.n - 1; i >= 0; i--) {
    if (this.t - LT[i] > warm + 30 && warm < 1e9) break;          // seen long ago: nothing older can be warm (index order = time order)
    if (arena.malive[i] && arena.mowner[i] >= 0 && this.t - LT[i] <= warm) trail.push(i);
  }
  const claimed = this._claimed || (this._claimed = new Set()); claimed.clear();
  for (let i = 0; i < n; i++) { if (this.claim[i] >= 0) claimed.add(this.claim[i]); if (this.carry[i] >= 0) claimed.add(this.carry[i]); }
  this.buildNbr(16);
  for (let i = 0; i < n; i++) {
    des[3 * i] = 0; des[3 * i + 1] = 0; des[3 * i + 2] = 0;
    if (!A[i]) continue;
    if (this.carry[i] >= 0) {
      const tx = this.nest[0] - X[3 * i], ty = this.nest[1] - X[3 * i + 1], tz = this.nest[2] - X[3 * i + 2], tn = Math.max(Math.hypot(tx, ty, tz), 1e-9);
      des[3 * i] = tx / tn * this.LADEN_V; des[3 * i + 1] = ty / tn * this.LADEN_V; des[3 * i + 2] = tz / tn * this.LADEN_V;
      if (tn < 12) {
        const j = this.carry[i]; let ux = this.rng.normal(), uy = this.rng.normal(), uz = this.rng.normal(); const un = Math.max(Math.hypot(ux, uy, uz), 1e-9);
        const rr = 8 + 1.5 * Math.cbrt(this.hoard + 1);
        arena.mpos[3 * j] = this.nest[0] + ux / un * rr; arena.mpos[3 * j + 1] = this.nest[1] + uy / un * rr; arena.mpos[3 * j + 2] = this.nest[2] + uz / un * rr;
        arena.mdirty[j] = 1;
        this.carry[i] = -1; this.hoard++;
      }
      continue;
    }
    let c = this.claim[i];
    if (c < 0 || !arena.malive[c] || arena.mowner[c] < 0 || this.t - LT[c] > warm + 1.5) {
      this.claim[i] = -1;
      let best = -1;
      for (const j of trail) {
        if (claimed.has(j)) continue;
        const d = Math.hypot(arena.mpos[3 * j] - X[3 * i], arena.mpos[3 * j + 1] - X[3 * i + 1], arena.mpos[3 * j + 2] - X[3 * i + 2]);
        if (d < this.SCOUT && j > best) best = j;                 // freshest = highest index (laid last)
      }
      if (best >= 0) { this.claim[i] = best; claimed.add(best); }
    }
    c = this.claim[i];
    if (c >= 0) {
      const tx = arena.mpos[3 * c] - X[3 * i], ty = arena.mpos[3 * c + 1] - X[3 * i + 1], tz = arena.mpos[3 * c + 2] - X[3 * i + 2], tn = Math.max(Math.hypot(tx, ty, tz), 1e-9);
      des[3 * i] = tx / tn * this.FREE_V; des[3 * i + 1] = ty / tn * this.FREE_V; des[3 * i + 2] = tz / tn * this.FREE_V;
      if (tn < 5) {
        const ow = arena.mowner[c];
        arena.mowner[c] = this.THIEF_OWNER; arena.mdirty[c] = 1; this.carry[i] = c; this.claim[i] = -1;
        this.stolenBy.set(c, ow); this.steals++; this.stolenVol += arena.mvol[c];
        arena.hit(PL[ow] || PL[0], 'steal', arena.mvol[c]);
      }
    } else if (dist[i] < this.SPOT && warm < 1e9) {   // a ship in sight: fall in behind it (gulls behind a trawler)
      const p = PL[k[i]], vn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-9);
      const tx = p.pos[0] - p.vel[0] / vn * 70 - X[3 * i], ty = p.pos[1] - p.vel[1] / vn * 70 - X[3 * i + 1], tz = p.pos[2] - p.vel[2] / vn * 70 - X[3 * i + 2];
      const tn = Math.max(Math.hypot(tx, ty, tz), 1e-9);
      des[3 * i] = tx / tn * this.FREE_V; des[3 * i + 1] = ty / tn * this.FREE_V; des[3 * i + 2] = tz / tn * this.FREE_V;
    } else {
      const tx = this.nest[0] - X[3 * i] + this.rng.normal(0, 30), ty = this.nest[1] - X[3 * i + 1] + this.rng.normal(0, 30), tz = this.nest[2] - X[3 * i + 2] + this.rng.normal(0, 30);
      const tn = Math.max(Math.hypot(tx, ty, tz), 1e-9);
      des[3 * i] = tx / tn * 30; des[3 * i + 1] = ty / tn * 30; des[3 * i + 2] = tz / tn * 30;
    }
  }
  for (let i = 0; i < n; i++) {
    const p = PL[k[i]], pvn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-9), on = Math.max(dist[i], 1e-9);
    const pointing = (p.vel[0] * -off[3 * i] + p.vel[1] * -off[3 * i + 1] + p.vel[2] * -off[3 * i + 2]) / pvn / on > 0.85;
    if (A[i] && this.carry[i] < 0 && dist[i] < 120 && pointing && this.ablate !== 'bold') {
      // side = unit(cross(pv, up) + 1e-6); cross(v,[0,1,0]) = (-vz, 0, vx)
      let sx = -p.vel[2] + 1e-6, sy = 1e-6, sz = p.vel[0] + 1e-6; const sn = Math.max(Math.hypot(sx, sy, sz), 1e-9); sx /= sn; sy /= sn; sz /= sn;
      const sg = Math.sign(sx * -off[3 * i] + sy * -off[3 * i + 1] + sz * -off[3 * i + 2]);
      des[3 * i] = sx * sg * this.FREE_V; des[3 * i + 1] = sy * sg * this.FREE_V; des[3 * i + 2] = sz * sg * this.FREE_V;
    }
    if (A[i]) this.sepInto(i, 8, des, 40);
    this.steerTo(V, i, des[3 * i], des[3 * i + 1], des[3 * i + 2], (this.carry[i] >= 0 ? 200 : 500) * dt);
  }
  this.containAll();
  for (let i = 0; i < n; i++) if (A[i] && this.carry[i] >= 0) {     // the carried prism rides in the grip
    const j = this.carry[i], vn = Math.max(Math.hypot(V[3 * i], V[3 * i + 1], V[3 * i + 2]), 1e-9);
    for (let a = 0; a < 3; a++) arena.mpos[3 * j + a] = X[3 * i + a] + V[3 * i + a] * dt - V[3 * i + a] / vn * 3;
    arena.mdirty[j] = 1;
  }
  for (let i = 0; i < n; i++) {
    const near = dist[i] < 300;
    this.intent[i] = (A[i] && this.claim[i] >= 0 && near) ? 1 : (A[i] && this.claim[i] < 0 && this.carry[i] < 0 && near) ? 0.7 : this.carry[i] >= 0 ? 0.3 : 0;
  }
  for (const p of PL) {                                       // a hunter knocks a thief down: recapture
    if (p.policy !== 'hunter' && !p.rams) continue;
    for (let i = 0; i < n; i++) {
      if (!A[i]) continue;
      if (Math.hypot(X[3 * i] - p.pos[0], X[3 * i + 1] - p.pos[1], X[3 * i + 2] - p.pos[2]) >= p.radius + this.size[i] + 4) continue;
      const j = this.carry[i];
      if (j >= 0) { arena.mowner[j] = this.stolenBy.has(j) ? this.stolenBy.get(j) : 0; arena.mdirty[j] = 1; this.recaptured++; this.carry[i] = -1; }
      this.kill(i, arena);
    }
  }
  this.publish(arena);
};
Thief.prototype.colour = function (i, c) {
  if (this.carry[i] >= 0) { c[0] = 1.0; c[1] = 0.85; c[2] = 0.3; }
  else if (this.claim[i] >= 0) { c[0] = 0.85; c[1] = 0.85; c[2] = 1.0; }
  else { c[0] = 0.42; c[1] = 0.42; c[2] = 0.62; }
  return c;
};

// ------------------------------------------------------------------------------------------------ LOCUST (cute -> storm)
const Locust = inherit(function Locust(arena, P, o) {
  o = o || {}; this.name = 'locust';
  for (const kk of ['CAP', 'CHEW', 'BREED', 'FULL']) this[kk] = C_(P, kk, o);
  const CAP = o.cap || this.CAP, n0 = o.n || P.init.n;
  mkHerd(this, arena, P, CAP, o.centre);
  const env = envPrisms(arena, o);
  const c = this.rng.choiceNoReplace(env, 3);
  for (let i = 0; i < CAP; i++) { const cc = c[i % 3]; for (let a = 0; a < 3; a++) this.pos[3 * i + a] = arena.mpos[3 * cc + a] + this.rng.normal(0, 90); }
  for (let i = 0; i < CAP; i++) { if (i < n0) this.gut[i] = 30; else { this.alive[i] = 0; this.body[i] = 0; } }
  this.g = new Float64Array(CAP);
  this.chew = new Float64Array(CAP); this.breed = new Float64Array(CAP);
  for (let i = 0; i < CAP; i++) this.chew[i] = this.rng.uniform(0, 3);
  for (let i = 0; i < CAP; i++) this.breed[i] = this.rng.uniform(4, 8);
  this.age = new Float64Array(CAP).fill(10); this.bitecd = new Float64Array(CAP); this.food = new Int32Array(CAP).fill(-1);
  this.tick = 0; this.births = 0; this.trailEaten = 0; this.ablate = o.ablate || null;
  this.nn = new Float64Array(CAP); this.al = new Float64Array(3 * CAP); this.co = new Float64Array(3 * CAP); this.sp = new Float64Array(3 * CAP);
});
Locust.prototype.act = function (arena, dt) {
  const n = this.n, A = this.alive, X = this.pos, V = this.vel;
  let m = 0; for (let i = 0; i < n; i++) m += A[i]; if (!m) return;
  this.buildNbr(60);
  const g = this.g, nn = this.nn, al = this.al, co = this.co, sp = this.sp, des = this.des;
  for (let i = 0; i < n; i++) {
    if (!A[i]) continue;
    const c = this.nbr.query(X[3 * i], X[3 * i + 1], X[3 * i + 2], 60), b = this.nbr.buf;
    let n40 = 0, n60 = 0, ax = 0, ay = 0, az = 0, cx = 0, cy = 0, cz = 0, sx = 0, sy = 0, sz = 0;
    for (let s = 0; s < c; s++) {
      const j = b[s]; if (j === i) continue;
      const dx = X[3 * j] - X[3 * i], dy = X[3 * j + 1] - X[3 * i + 1], dz = X[3 * j + 2] - X[3 * i + 2];
      const d = Math.sqrt(dx * dx + dy * dy + dz * dz);
      if (d < 40) n40++;
      if (d < 60) { n60++; ax += V[3 * j]; ay += V[3 * j + 1]; az += V[3 * j + 2]; cx += dx; cy += dy; cz += dz; }
      if (d < 28) { const w = 1 - d / 28, dd = Math.max(d, 1e-6); sx -= dx / dd * w; sy -= dy / dd * w; sz -= dz / dd * w; }
    }
    const cm = Math.max(n60, 1);
    nn[i] = n40; al[3 * i] = ax / cm; al[3 * i + 1] = ay / cm; al[3 * i + 2] = az / cm;
    co[3 * i] = cx / cm; co[3 * i + 1] = cy / cm; co[3 * i + 2] = cz / cm; sp[3 * i] = sx; sp[3 * i + 1] = sy; sp[3 * i + 2] = sz;
    const hunger = clamp(1 - this.gut[i] / 40, 0, 1);
    const target = 1 / (1 + Math.exp(-(n40 - (9 - 3 * hunger)) * 0.9));
    g[i] = g[i] + (target - g[i]) * (dt / 4);
    if (this.ablate === 'solitary') g[i] = 0; else if (this.ablate === 'gregarious') g[i] = 1;
  }
  // food: re-search on this agent's turn (1 in 4) or when it has none; gregarious ones take trail too
  this.tick++;
  for (let i = 0; i < n; i++) {
    if (!A[i]) continue;
    if (!((i + this.tick) % 4 === 0 || this.food[i] < 0)) continue;
    const c = arena.massNear(X[3 * i], X[3 * i + 1], X[3 * i + 2], 120);
    let best = -1, bd = Infinity;
    for (let s = 0; s < c; s++) {
      const j = arena.qbuf[s]; if (g[i] < 0.5 && arena.mowner[j] >= 0) continue;
      const d = (arena.mpos[3 * j] - X[3 * i]) ** 2 + (arena.mpos[3 * j + 1] - X[3 * i + 1]) ** 2 + (arena.mpos[3 * j + 2] - X[3 * i + 2]) ** 2;
      if (d < bd) { bd = d; best = j; }
    }
    this.food[i] = best;
  }
  this.pilotVectors(arena);
  const off = this.off, dist = this.dist, k = this.pk, PL = arena.pilots;
  for (let i = 0; i < n; i++) {
    if (!A[i]) continue;
    const fj = this.food[i]; let fx = 0, fy = 0, fz = 0;
    const has = fj >= 0 && arena.malive[fj];
    if (has) {
      const dx = arena.mpos[3 * fj] - X[3 * i], dy = arena.mpos[3 * fj + 1] - X[3 * i + 1], dz = arena.mpos[3 * fj + 2] - X[3 * i + 2], dn = Math.max(Math.hypot(dx, dy, dz), 1e-9);
      fx = dx / dn; fy = dy / dn; fz = dz / dn;
      if (dn < 7 && this.chew[i] <= 0) {
        const v = arena.consume(fj); this.gut[i] += v; this.chew[i] = this.CHEW;
        if (v > 0 && arena.mowner[fj] >= 0) this.trailEaten += v;
      }
    }
    const on = Math.max(dist[i], 1e-9), tx = off[3 * i] / on, ty = off[3 * i + 1] / on, tz = off[3 * i + 2] / on;
    const shy = dist[i] < 90 && g[i] < 0.5 ? 1 : 0, swarm = dist[i] < 250 && g[i] >= 0.5 ? 1 : 0;
    let ux = this.rng.normal() + 0.6 * fx, uy = this.rng.normal() + 0.6 * fy, uz = this.rng.normal() + 0.6 * fz;
    let un = Math.max(Math.hypot(ux, uy, uz), 1e-9);
    const solx = ux / un * 25 + sp[3 * i] * 40 - tx * shy * 70, soly = uy / un * 25 + sp[3 * i + 1] * 40 - ty * shy * 70, solz = uz / un * 25 + sp[3 * i + 2] * 40 - tz * shy * 70;
    ux = al[3 * i] / 135 * 1.5 + co[3 * i] * 0.02 + 0.8 * fx + 1.6 * tx * swarm;
    uy = al[3 * i + 1] / 135 * 1.5 + co[3 * i + 1] * 0.02 + 0.8 * fy + 1.6 * ty * swarm;
    uz = al[3 * i + 2] / 135 * 1.5 + co[3 * i + 2] * 0.02 + 0.8 * fz + 1.6 * tz * swarm;
    un = Math.max(Math.hypot(ux, uy, uz), 1e-9);
    const grx = ux / un * 135 + sp[3 * i] * 30, gry = uy / un * 135 + sp[3 * i + 1] * 30, grz = uz / un * 135 + sp[3 * i + 2] * 30;
    const gi = g[i];
    this.steerTo(V, i, solx * (1 - gi) + grx * gi, soly * (1 - gi) + gry * gi, solz * (1 - gi) + grz * gi, (80 + 300 * gi) * dt);
    this.intent[i] = gi * clamp(1.6 - dist[i] / 400, 0, 1);
  }
  this.containAll();
  for (let i = 0; i < n; i++) { this.bitecd[i] = Math.max(0, this.bitecd[i] - dt); this.chew[i] = Math.max(0, this.chew[i] - dt); }
  for (let i = 0; i < n; i++) {
    if (!A[i] || !(dist[i] < 250 && g[i] >= 0.5) || !(dist[i] < 10)) continue;
    if (this.bitecd[i] <= 0) { arena.hit(PL[k[i]], 'bite', 0.05, i); this.bitecd[i] = 2.0; }
  }
  // breed: a full gut buds a newborn whose body is paid from the gut
  for (let i = 0; i < n; i++) { this.breed[i] = Math.max(0, this.breed[i] - dt); this.age[i] += dt; }
  const free = this._free || (this._free = []); free.length = 0;
  for (let i = 0; i < n; i++) if (!A[i]) free.push(i);
  let fp = 0;
  const parents = this._par || (this._par = []); parents.length = 0;
  for (let i = 0; i < n; i++) if (A[i] && this.gut[i] >= this.FULL && this.breed[i] <= 0) parents.push(i);
  for (const i of parents) {
    if (fp >= free.length) break;
    const j = free[fp++];
    A[j] = 1; this.body[j] = 6; this.gut[i] -= 6; this.gut[j] = 0;
    for (let a = 0; a < 3; a++) { X[3 * j + a] = X[3 * i + a] + this.rng.normal(0, 3); V[3 * j + a] = V[3 * i + a]; }
    g[j] = g[i]; this.age[j] = 0; this.breed[i] = this.BREED; this.breed[j] = this.BREED; this.chew[j] = 3; this.births++;
    if (arena.onBirth) arena.onBirth(this, j);
  }
  for (let i = 0; i < n; i++) this.size[i] = 2 * clamp(this.age[i] / 2, 0.05, 1) * (1 + 0.5 * g[i]);
  this.hunterContacts(arena);
  this.publish(arena);
};
Locust.prototype.colour = function (i, c) { const g = this.g[i]; c[0] = 0.45 * (1 - g) + 1.0 * g; c[1] = 0.95 * (1 - g) + 0.8 * g; c[2] = 0.4 * (1 - g) + 0.1 * g; return c; };
Locust.prototype.gregFrac = function () { let a = 0, s = 0; for (let i = 0; i < this.n; i++) if (this.alive[i]) { a++; s += this.g[i] > 0.5 ? 1 : 0; } return a ? s / a : 0; };

// ------------------------------------------------------------------------------------------------ LURKER (eerie)
const Lurker = inherit(function Lurker(arena, P, o) {
  o = o || {}; this.name = 'lurker';
  for (const kk of ['GAPE', 'REACH_T', 'LUNGE', 'SENSE']) this[kk] = C_(P, kk, o);
  const n = o.n || P.init.n;
  mkHerd(this, arena, P, n, o.centre);
  const env = envPrisms(arena, o);
  const pick = this.rng.choiceNoReplace(env, n);
  for (let i = 0; i < n; i++) for (let a = 0; a < 3; a++) this.pos[3 * i + a] = arena.mpos[3 * pick[i] + a] + this.rng.normal(0, 8);
  this.gape = new Float64Array(n); this.lunge = new Float64Array(n); this.spent = new Float64Array(n);
  this.seat = new Int32Array(n).fill(-1); this.prevD = new Float64Array(n).fill(Infinity);
  this.heading = new Float64Array(3 * n);
  for (let i = 0; i < n; i++) { const u = vunit(this.rng.normal(), this.rng.normal(), this.rng.normal(), [0, 0, 0]); this.heading.set(u, 3 * i); }
  this.snaps = 0; this.hits = 0; this.ablate = o.ablate || null;
});
Lurker.prototype.act = function (arena, dt) {
  const n = this.n, A = this.alive, X = this.pos, V = this.vel, des = this.des;
  this.pilotVectors(arena);
  const off = this.off, dist = this.dist, k = this.pk, PL = arena.pilots;
  const go = this._go || (this._go = new Uint8Array(n));
  const gt = this.ablate === 'nogape' ? 0.05 : this.GAPE;
  for (let i = 0; i < n; i++) {
    const closing = dist[i] < this.prevD[i] - 1e-3; this.prevD[i] = dist[i];
    const idle = A[i] && this.lunge[i] <= 0 && this.spent[i] <= 0;
    const trig = idle && ((dist[i] < this.SENSE && closing) || dist[i] < 80);
    this.gape[i] = (trig || (idle && this.gape[i] > 0 && dist[i] < 230)) ? this.gape[i] + dt / gt : Math.max(0, this.gape[i] - 2 * dt);
    go[i] = idle && this.gape[i] >= 1 ? 1 : 0;
    if (go[i]) {
      const p = PL[k[i]], s = clamp(dist[i] / this.LUNGE, 0, 0.5);
      const ax = off[3 * i] + p.vel[0] * s, ay = off[3 * i + 1] + p.vel[1] * s, az = off[3 * i + 2] + p.vel[2] * s, an = Math.max(Math.hypot(ax, ay, az), 1e-9);
      V[3 * i] = ax / an * this.LUNGE; V[3 * i + 1] = ay / an * this.LUNGE; V[3 * i + 2] = az / an * this.LUNGE;
    }
    const lungePrev = this.lunge[i];
    this.lunge[i] = go[i] ? this.REACH_T : Math.max(0, this.lunge[i] - dt);
    if (go[i]) { this.snaps++; this.gape[i] = 0; }
    const ending = this.lunge[i] <= 0 && this.lunge[i] + dt > 0 && !go[i] && Math.hypot(V[3 * i], V[3 * i + 1], V[3 * i + 2]) > 200;
    this.spent[i] = ending ? 3.0 : Math.max(0, this.spent[i] - dt);
    void lungePrev;
  }
  for (let i = 0; i < n; i++) {
    des[3 * i] = 0; des[3 * i + 1] = 0; des[3 * i + 2] = 0;
    const settle = A[i] && this.lunge[i] <= 0 && this.gape[i] <= 0;
    if (settle) {
      let s = this.seat[i];
      if (s < 0 || !arena.malive[s]) {
        const c = arena.massNear(X[3 * i], X[3 * i + 1], X[3 * i + 2], 400);
        let bestFar = -1, bdFar = Infinity, bestAny = -1, bdAny = Infinity;
        for (let q = 0; q < c; q++) {
          const j = arena.qbuf[q]; if (arena.mowner[j] >= 0) continue;
          let dmin = 1e9;
          for (let o = 0; o < n; o++) if (o !== i && A[o]) {
            const d = Math.hypot(arena.mpos[3 * j] - X[3 * o], arena.mpos[3 * j + 1] - X[3 * o + 1], arena.mpos[3 * j + 2] - X[3 * o + 2]); if (d < dmin) dmin = d;
          }
          const dme = Math.hypot(arena.mpos[3 * j] - X[3 * i], arena.mpos[3 * j + 1] - X[3 * i + 1], arena.mpos[3 * j + 2] - X[3 * i + 2]);
          if (dme < bdAny) { bdAny = dme; bestAny = j; }
          if (dmin > 60 && dme < bdFar) { bdFar = dme; bestFar = j; }
        }
        if (bestAny >= 0) this.seat[i] = bestFar >= 0 ? bestFar : bestAny;
        s = this.seat[i];
      }
      if (s >= 0) {
        const tx = arena.mpos[3 * s] - X[3 * i], ty = arena.mpos[3 * s + 1] - X[3 * i + 1], tz = arena.mpos[3 * s + 2] - X[3 * i + 2];
        const dd = Math.hypot(tx, ty, tz);
        if (dd > 6) { const sp = Math.min(20, dd * 2) / Math.max(dd, 1e-9); des[3 * i] = tx * sp; des[3 * i + 1] = ty * sp; des[3 * i + 2] = tz * sp; }
      }
      // creep while unwatched; freeze when looked at
      const p = PL[k[i]], pvn = Math.max(Math.hypot(p.vel[0], p.vel[1], p.vel[2]), 1e-9), on = Math.max(dist[i], 1e-9);
      const look = (p.vel[0] * -off[3 * i] + p.vel[1] * -off[3 * i + 1] + p.vel[2] * -off[3 * i + 2]) / pvn / on > Math.cos(50 * Math.PI / 180);
      const dorm = dist[i] < 600 && dist[i] > 120 && this.ablate !== 'nocreep';
      if (dorm && !look) {
        const ax = p.pos[0] + p.vel[0] * 3 - X[3 * i], ay = p.pos[1] + p.vel[1] * 3 - X[3 * i + 1], az = p.pos[2] + p.vel[2] * 3 - X[3 * i + 2], an = Math.max(Math.hypot(ax, ay, az), 1e-9);
        des[3 * i] = ax / an * 35; des[3 * i + 1] = ay / an * 35; des[3 * i + 2] = az / an * 35; this.seat[i] = -1;
      } else if (dorm && look) { des[3 * i] = 0; des[3 * i + 1] = 0; des[3 * i + 2] = 0; V[3 * i] = 0; V[3 * i + 1] = 0; V[3 * i + 2] = 0; }
    }
    if (!(this.lunge[i] > 0)) this.steerTo(V, i, des[3 * i], des[3 * i + 1], des[3 * i + 2], 120 * dt);
  }
  this.containAll();
  for (let i = 0; i < n; i++) {
    const gp = clamp(this.gape[i], 0, 1);
    this.size[i] = (gp > 0 ? 5.6 + 3.4 * gp : 4.0) + 3.0 * (this.lunge[i] > 0 ? 1 : 0);
    this.intent[i] = this.lunge[i] > 0 ? 1 : gp > 0.02 ? 0.6 + 0.4 * gp : 0;
  }
  const r0 = PL[0].radius;
  for (let i = 0; i < n; i++) if (A[i] && this.lunge[i] > 0 && dist[i] < r0 + this.size[i] + 4) {
    arena.hit(PL[k[i]], 'bite', 0.3, i); this.hits++; this.lunge[i] = 0; this.spent[i] = 3;
    V[3 * i] *= 0.1; V[3 * i + 1] *= 0.1; V[3 * i + 2] *= 0.1;
  }
  this.hunterContacts(arena);
  const tm = this._thr || (this._thr = new Uint8Array(n));
  for (let i = 0; i < n; i++) tm[i] = (this.gape[i] > 0.05 || this.lunge[i] > 0 || this.spent[i] > 0) ? 1 : 0;
  this.publish(arena, tm);
};
Lurker.prototype.colour = function (i, c) {
  if (this.lunge[i] > 0) { c[0] = 1; c[1] = 0.1; c[2] = 0.1; return c; }
  if (this.spent[i] > 0) { c[0] = 0.3; c[1] = 0.35; c[2] = 0.3; return c; }
  const g = this.gape[i] > 0.02 ? clamp(0.5 + 0.5 * this.gape[i], 0, 1) : 0;
  c[0] = 0.75 * (1 - g) + 0.5 * g; c[1] = 1.0 * (1 - g) + 0.05 * g; c[2] = 0.45 * (1 - g) + 0.1 * g; return c;
};

// ------------------------------------------------------------------------------------------------ STAMPEDE (majestic)
const Stampede = inherit(function Stampede(arena, P, o) {
  o = o || {}; this.name = 'stampede';
  for (const kk of ['SENSE', 'BULL_R', 'HEAD_DOWN', 'SCENT', 'WINDUP']) this[kk] = C_(P, kk, o);
  const n = o.n || P.init.n, herds = o.herds || P.init.herds;
  mkHerd(this, arena, P, n, o.centre);
  const env = envPrisms(arena, o);
  const c = this.rng.choiceNoReplace(env, herds);
  for (let i = 0; i < n; i++) for (let a = 0; a < 3; a++) this.pos[3 * i + a] = arena.mpos[3 * c[i % herds] + a] + this.rng.normal(0, 50);
  this.bull = new Uint8Array(n); for (let i = 0; i < n; i++) this.bull[i] = i % 4 === 0 ? 1 : 0;
  if (o.ablate === 'nobulls') this.bull.fill(0);
  for (let i = 0; i < n; i++) this.size[i] = this.bull[i] ? 14 : 10;
  this.a = new Float64Array(n); this.f = new Float64Array(3 * n);
  for (let i = 0; i < n; i++) this.f.set(vunit(this.rng.normal(), this.rng.normal(), this.rng.normal(), [0, 0, 0]), 3 * i);
  this.chew = new Float64Array(n); for (let i = 0; i < n; i++) this.chew[i] = this.rng.uniform(0, 6);
  this.prep = new Float64Array(n); this.charge = new Float64Array(n); this.rest = new Float64Array(n); this.cd = new Float64Array(n);
  this.wind = new Float64Array(n);   // seconds this animal's own telegraph has shown (fair burns: tramples need WINDUP)
  this.charges = 0; this.trailEaten = 0; this.ablate = o.ablate || null;
  this.a2 = new Float64Array(n); this.f2 = new Float64Array(3 * n); this.cen = new Float64Array(3 * n); this.alv = new Float64Array(3 * n);
  this.lure = new Float64Array(3 * n);
});
Stampede.prototype.act = function (arena, dt) {
  const n = this.n, A = this.alive, X = this.pos, V = this.vel, des = this.des;
  this.pilotVectors(arena);
  const off = this.off, dist = this.dist, k = this.pk, PL = arena.pilots;
  this.buildNbr(90);
  const a = this.a, a2 = this.a2, f = this.f, f2 = this.f2, cen = this.cen, alv = this.alv;
  let msz = 0; for (let i = 0; i < n; i++) msz += this.size[i]; msz /= n;
  const sepR = 2.2 * msz;
  // alarm: sight, then contagion (max over herdmates within 70 u, of the OLD alarm)
  for (let i = 0; i < n; i++) {
    const saw = A[i] && dist[i] < this.SENSE;
    let loud = 0;
    const c = this.nbr.query(X[3 * i], X[3 * i + 1], X[3 * i + 2], 70), b = this.nbr.buf;
    for (let s = 0; s < c; s++) {
      const j = b[s]; if (j === i) continue;
      if (Math.hypot(X[3 * j] - X[3 * i], X[3 * j + 1] - X[3 * i + 1], X[3 * j + 2] - X[3 * i + 2]) < 70 && a[j] > loud) loud = a[j];
    }
    const target = Math.max(0.9 * loud, 0);
    a2[i] = saw ? 1 : (target > a[i] ? a[i] + (target - a[i]) * Math.min(1, 3 * dt) : Math.max(0, a[i] - 0.12 * dt));
  }
  a.set(a2);
  for (let i = 0; i < n; i++) {
    let ix = 1e-9, iy = 1e-9, iz = 1e-9, hx = 0, hy = 0, hz = 0, hn = 0, lx = 0, ly = 0, lz = 0;
    const c = this.nbr.query(X[3 * i], X[3 * i + 1], X[3 * i + 2], 90), b = this.nbr.buf;
    for (let s = 0; s < c; s++) {
      const j = b[s]; if (j === i) continue;
      const dx = X[3 * j] - X[3 * i], dy = X[3 * j + 1] - X[3 * i + 1], dz = X[3 * j + 2] - X[3 * i + 2], d = Math.sqrt(dx * dx + dy * dy + dz * dz);
      if (d < 70) { ix += a[j] * f[3 * j]; iy += a[j] * f[3 * j + 1]; iz += a[j] * f[3 * j + 2]; }
      if (d < 90) { hx += dx; hy += dy; hz += dz; hn++; lx += V[3 * j]; ly += V[3 * j + 1]; lz += V[3 * j + 2]; }
    }
    const hm = Math.max(hn, 1);
    cen[3 * i] = hx / hm; cen[3 * i + 1] = hy / hm; cen[3 * i + 2] = hz / hm;
    alv[3 * i] = lx / hm; alv[3 * i + 1] = ly / hm; alv[3 * i + 2] = lz / hm;
    const inn = Math.max(Math.hypot(ix, iy, iz), 1e-9); ix /= inn; iy /= inn; iz /= inn;
    const saw = A[i] && dist[i] < this.SENSE;
    if (saw) {
      const on = Math.max(dist[i], 1e-9);
      let awx, awy, awz;
      if (this.ablate === 'selfish') { awx = -off[3 * i] / on; awy = -off[3 * i + 1] / on; awz = -off[3 * i + 2] / on; }
      else { const qx = cen[3 * i] - off[3 * i], qy = cen[3 * i + 1] - off[3 * i + 1], qz = cen[3 * i + 2] - off[3 * i + 2], qn = Math.max(Math.hypot(qx, qy, qz), 1e-9); awx = qx / qn; awy = qy / qn; awz = qz / qn; }
      const ux = 0.35 * -off[3 * i] / on + awx, uy = 0.35 * -off[3 * i + 1] / on + awy, uz = 0.35 * -off[3 * i + 2] / on + awz, un = Math.max(Math.hypot(ux, uy, uz), 1e-9);
      f2[3 * i] = ux / un; f2[3 * i + 1] = uy / un; f2[3 * i + 2] = uz / un;
    } else {
      const s = Math.min(1, 2 * dt);
      const ux = f[3 * i] + (ix - f[3 * i]) * s, uy = f[3 * i + 1] + (iy - f[3 * i + 1]) * s, uz = f[3 * i + 2] + (iz - f[3 * i + 2]) * s, un = Math.max(Math.hypot(ux, uy, uz), 1e-9);
      f2[3 * i] = ux / un; f2[3 * i + 1] = uy / un; f2[3 * i + 2] = uz / un;
    }
  }
  f.set(f2);
  // trail scent: walk UP the trail toward the newest trail prism within SCENT (of the last 400 live trail prisms)
  const own = this._own || (this._own = new Int32Array(400)); let no = 0;
  for (let j = arena.n - 1; j >= 0 && no < 400; j--) if (arena.malive[j] && arena.mowner[j] >= 0) own[no++] = j;  // newest first
  const lure = this.lure; lure.fill(0);
  if (no) for (let i = 0; i < n; i++) {
    for (let q = 0; q < no; q++) {
      const j = own[q], dx = arena.mpos[3 * j] - X[3 * i], dy = arena.mpos[3 * j + 1] - X[3 * i + 1], dz = arena.mpos[3 * j + 2] - X[3 * i + 2];
      const d = Math.sqrt(dx * dx + dy * dy + dz * dz);
      if (d < this.SCENT) { const dn = Math.max(d, 1e-9); lure[3 * i] = dx / dn; lure[3 * i + 1] = dy / dn; lure[3 * i + 2] = dz / dn; break; }
    }
  }
  const th = 0.05 * this.t;
  let drx = Math.cos(th), dry = 0.3 * Math.sin(0.7 * th), drz = Math.sin(th); const drn = Math.hypot(drx, dry, drz); drx /= drn; dry /= drn; drz /= drn;
  for (let i = 0; i < n; i++) {
    let gx = this.rng.normal() * 0.6 + cen[3 * i] * 0.02 + 0.3 * drx + 1.2 * lure[3 * i];
    let gy = this.rng.normal() * 0.6 + cen[3 * i + 1] * 0.02 + 0.3 * dry + 1.2 * lure[3 * i + 1];
    let gz = this.rng.normal() * 0.6 + cen[3 * i + 2] * 0.02 + 0.3 * drz + 1.2 * lure[3 * i + 2];
    let gn = Math.max(Math.hypot(gx, gy, gz), 1e-9); gx = gx / gn * 30; gy = gy / gn * 30; gz = gz / gn * 30;
    const avn = Math.max(Math.hypot(alv[3 * i], alv[3 * i + 1], alv[3 * i + 2]), 1e-9);
    let bx = f[3 * i] + alv[3 * i] / avn * 0.8, by = f[3 * i + 1] + alv[3 * i + 1] / avn * 0.8, bz = f[3 * i + 2] + alv[3 * i + 2] / avn * 0.8;
    const bn = Math.max(Math.hypot(bx, by, bz), 1e-9); bx = bx / bn * 125; by = by / bn * 125; bz = bz / bn * 125;
    des[3 * i] = gx * (1 - a[i]) + bx * a[i]; des[3 * i + 1] = gy * (1 - a[i]) + by * a[i]; des[3 * i + 2] = gz * (1 - a[i]) + bz * a[i];
  }
  for (let i = 0; i < n; i++) if (A[i] || true) this.sepInto(i, sepR, des, 50);
  // bulls: head down, then charge
  const ch = this._ch || (this._ch = new Uint8Array(n));
  for (let i = 0; i < n; i++) {
    this.rest[i] = Math.max(0, this.rest[i] - dt);
    const cand = A[i] && this.bull[i] && dist[i] < this.BULL_R && this.charge[i] <= 0 && this.rest[i] <= 0;
    this.prep[i] = cand ? this.prep[i] + dt : (this.charge[i] > 0 ? this.prep[i] : 0);
    const go = cand && this.prep[i] >= this.HEAD_DOWN;
    const was = this.charge[i] > 0;
    this.charge[i] = go ? 1.5 : Math.max(0, this.charge[i] - dt);
    if (go) { this.charges++; this.prep[i] = 0; }
    if (was && this.charge[i] <= 0) this.rest[i] = 4.0;
    const on = Math.max(dist[i], 1e-9);
    if (cand) { des[3 * i] = off[3 * i] / on * 20; des[3 * i + 1] = off[3 * i + 1] / on * 20; des[3 * i + 2] = off[3 * i + 2] / on * 20; }
    ch[i] = this.charge[i] > 0 ? 1 : 0;
    if (ch[i]) {
      const p = PL[k[i]], s = clamp(dist[i] / 170, 0, 1.2);
      const lx = off[3 * i] + p.vel[0] * s, ly = off[3 * i + 1] + p.vel[1] * s, lz = off[3 * i + 2] + p.vel[2] * s, ln = Math.max(Math.hypot(lx, ly, lz), 1e-9);
      des[3 * i] = lx / ln * 170; des[3 * i + 1] = ly / ln * 170; des[3 * i + 2] = lz / ln * 170;
    }
    this.steerTo(V, i, des[3 * i], des[3 * i + 1], des[3 * i + 2], (ch[i] ? 400 : 60 + 200 * a[i]) * dt);
  }
  this.containAll();
  const spd = this._spd || (this._spd = new Float64Array(n));
  for (let i = 0; i < n; i++) {
    spd[i] = Math.hypot(V[3 * i], V[3 * i + 1], V[3 * i + 2]);
    const vn = Math.max(spd[i], 1e-9), on = Math.max(dist[i], 1e-9);
    const tow = (V[3 * i] * off[3 * i] + V[3 * i + 1] * off[3 * i + 1] + V[3 * i + 2] * off[3 * i + 2]) / vn / on;
    let it = this.bull[i] ? clamp(this.prep[i] / this.HEAD_DOWN, 0, 1) + ch[i] : 0;
    this.intent[i] = Math.max(it, a[i] * clamp(2 * tow, 0, 1) * (spd[i] > 60 ? 1 : 0));
  }
  // the herd PARTS around a bull lowering its head
  const hot = this._hot || (this._hot = []); hot.length = 0;
  for (let i = 0; i < n; i++) if (A[i] && this.bull[i] && (this.prep[i] > 0 || ch[i])) hot.push(i);
  if (hot.length) {
    const it0 = Float64Array.from(this.intent);
    for (let i = 0; i < n; i++) {
      if (this.bull[i]) continue;
      let bsel = -1;
      for (const j of hot) { if (j === i) continue; if (Math.hypot(X[3 * j] - X[3 * i], X[3 * j + 1] - X[3 * i + 1], X[3 * j + 2] - X[3 * i + 2]) < 70) { bsel = j; break; } }
      if (bsel < 0) continue;
      const on = Math.max(dist[bsel], 1e-9), lx = off[3 * bsel] / on, ly = off[3 * bsel + 1] / on, lz = off[3 * bsel + 2] / on;
      let sx = X[3 * i] - X[3 * bsel], sy = X[3 * i + 1] - X[3 * bsel + 1], sz = X[3 * i + 2] - X[3 * bsel + 2];
      const pr = sx * lx + sy * ly + sz * lz; sx -= pr * lx; sy -= pr * ly; sz -= pr * lz;
      const sn = Math.max(Math.hypot(sx, sy, sz), 1e-9);
      V[3 * i] += sx / sn * 60 * dt * 10; V[3 * i + 1] += sy / sn * 60 * dt * 10; V[3 * i + 2] += sz / sn * 60 * dt * 10;
      this.intent[i] = Math.max(it0[i], it0[bsel]);
    }
  }
  for (let i = 0; i < n; i++) { this.intent[i] = clamp(this.intent[i], 0, 1); this.wind[i] = this.intent[i] > 0.5 ? this.wind[i] + dt : this.intent[i] < 0.2 ? 0 : this.wind[i]; this.chew[i] = Math.max(0, this.chew[i] - dt); this.cd[i] = Math.max(0, this.cd[i] - dt); }
  // trample mass in the path, graze otherwise
  for (let i = 0; i < n; i++) {
    if (!A[i] || !(spd[i] > 60 || this.chew[i] <= 0)) continue;
    const c = arena.massNear(X[3 * i], X[3 * i + 1], X[3 * i + 2], this.size[i] + 3);
    const m = spd[i] <= 60 ? Math.min(c, 1) : c;
    if (spd[i] <= 60 && m) this.chew[i] = 6.0;
    for (let q = 0; q < m; q++) {
      const j = arena.qbuf[q], v = arena.consume(j); this.gut[i] += v;
      if (arena.mowner[j] >= 0) this.trailEaten += v;
    }
  }
  // trample / gore a pilot (a body running INTO you)
  for (let i = 0; i < n; i++) {
    if (!A[i] || !(dist[i] < this.size[i] + 8) || this.cd[i] > 0 || !(this.wind[i] >= this.WINDUP - 1e-9)) continue;
    const on = Math.max(dist[i], 1e-9);
    const closing = (V[3 * i] * off[3 * i] + V[3 * i + 1] * off[3 * i + 1] + V[3 * i + 2] * off[3 * i + 2]) / on > 0.3 * Math.max(spd[i], 1e-6);
    if ((spd[i] > 60 && closing) || ch[i]) { arena.hit(PL[k[i]], 'bite', this.bull[i] ? 0.2 : 0.1, i); this.cd[i] = 2.0; }
  }
  // a calf alone is prey
  const alone = this._alone || (this._alone = new Uint8Array(n));
  for (let i = 0; i < n; i++) {
    let md = Infinity;
    const c = this.nbr.query(X[3 * i], X[3 * i + 1], X[3 * i + 2], 45), b = this.nbr.buf;
    for (let s = 0; s < c; s++) { const j = b[s]; if (j === i) continue; const d = Math.hypot(X[3 * j] - X[3 * i], X[3 * j + 1] - X[3 * i + 1], X[3 * j + 2] - X[3 * i + 2]); if (d < md) md = d; }
    alone[i] = md > 45 ? 1 : 0;
  }
  this.hunterContacts(arena, 4, alone);
  this.publish(arena);
};
Stampede.prototype.colour = function (i, c) {
  if (this.charge[i] > 0) { c[0] = 1; c[1] = 0.15; c[2] = 0.1; return c; }
  const a = this.a[i], b = this.bull[i];
  const r = b ? 0.85 : 0.7, g = b ? 0.7 : 0.75, bl = b ? 0.5 : 0.9;
  c[0] = r * (1 - 0.5 * a) + 0.5 * a; c[1] = g * (1 - 0.5 * a) + 0.15 * a; c[2] = bl * (1 - 0.5 * a) + 0.075 * a; return c;
};

// ------------------------------------------------------------------------------------------------ LEVIATHAN (majestic)
const Leviathan = inherit(function Leviathan(arena, P, o) {
  o = o || {}; this.name = 'leviathan';
  for (const kk of ['N', 'LEN', 'RAD', 'ASSEMBLE_N', 'DISSOLVE_N']) this[kk] = C_(P, kk, o);
  const N = this.N;
  mkHerd(this, arena, P, N, o.centre);
  this.slots = new Float64Array(3 * N);
  for (let i = 0; i < N; i++) {
    const t = i + 0.5, x = 1 - 2 * t / N, phi = t * Math.PI * (3 - Math.sqrt(5));
    const r = this.RAD * Math.sqrt(clamp(1 - x * x, 0, 1)) * (x < -0.4 ? 0.55 + 0.45 * (x + 1) / 0.6 : 1);
    this.slots[3 * i] = x * this.LEN / 2; this.slots[3 * i + 1] = r * Math.cos(phi); this.slots[3 * i + 2] = r * Math.sin(phi);
  }
  this.slot = new Int32Array(N).fill(-1); this.assembled = false;
  this.centre = [0, 0, 0]; let c = 0;
  for (let i = 0; i < N; i++) for (let a = 0; a < 3; a++) this.centre[a] += this.pos[3 * i + a] / N;
  this.head = vunit(this.rng.normal(), this.rng.normal(), this.rng.normal(), [0, 0, 0]);
  this.amp = 0; this.burncd = 0; this.assemblies = 0; this.dissolves = 0; this.eaten = 0;
  this.gulpPrep = 0; this.gulp = 0; this.gulpRest = 0; this.gulps = 0; this.ablate = o.ablate || null;
  this.F = new Float64Array(9); void c;
});
Leviathan.prototype.frame = function () {
  const f = this.head, F = this.F;
  let sx = -f[2] + 1e-6, sy = 1e-6, sz = f[0] + 1e-6; const sn = Math.max(Math.hypot(sx, sy, sz), 1e-9); sx /= sn; sy /= sn; sz /= sn;
  const ux = sy * f[2] - sz * f[1], uy = sz * f[0] - sx * f[2], uz = sx * f[1] - sy * f[0];
  F[0] = f[0]; F[1] = f[1]; F[2] = f[2]; F[3] = sx; F[4] = sy; F[5] = sz; F[6] = ux; F[7] = uy; F[8] = uz; return F;
};
Leviathan.prototype.act = function (arena, dt) {
  const N = this.N, A = this.alive, X = this.pos, V = this.vel, PL = arena.pilots;
  this.pilotVectors(arena);
  const dist = this.dist, k = this.pk;
  let cnt = 0, cx = 0, cy = 0, cz = 0;
  for (let i = 0; i < N; i++) if (A[i]) { cnt++; cx += X[3 * i]; cy += X[3 * i + 1]; cz += X[3 * i + 2]; }
  if (!cnt) return;
  cx /= cnt; cy /= cnt; cz /= cnt;
  let tight = 0; for (let i = 0; i < N; i++) if (A[i] && Math.hypot(X[3 * i] - cx, X[3 * i + 1] - cy, X[3 * i + 2] - cz) < 260) tight++;
  if (!this.assembled && tight >= this.ASSEMBLE_N && this.ablate !== 'noassemble') {
    this.assembled = true; this.assemblies++;
    this.centre = [cx, cy, cz];
    let vx = 0, vy = 0, vz = 0; for (let i = 0; i < N; i++) if (A[i]) { vx += V[3 * i]; vy += V[3 * i + 1]; vz += V[3 * i + 2]; }
    vx /= cnt; vy /= cnt; vz /= cnt;
    if (Math.hypot(vx, vy, vz) > 1) this.head = vunit(vx, vy, vz, [0, 0, 0]);
    const F = this.frame(), S = this.slots;
    const W = new Float64Array(3 * N);
    for (let j = 0; j < N; j++) for (let a = 0; a < 3; a++) W[3 * j + a] = this.centre[a] + S[3 * j] * F[a] + S[3 * j + 1] * F[3 + a] + S[3 * j + 2] * F[6 + a];
    const order = [];
    for (let i = 0; i < N; i++) if (A[i]) order.push(i);
    const key = i => -((X[3 * i] - cx) * this.head[0] + (X[3 * i + 1] - cy) * this.head[1] + (X[3 * i + 2] - cz) * this.head[2]);
    order.sort((a, b) => key(a) - key(b) || a - b);
    const free = new Uint8Array(N).fill(1);
    for (const i of order) {
      let bj = -1, bd = Infinity;
      for (let j = 0; j < N; j++) if (free[j]) { const d = (W[3 * j] - X[3 * i]) ** 2 + (W[3 * j + 1] - X[3 * i + 1]) ** 2 + (W[3 * j + 2] - X[3 * i + 2]) ** 2; if (d < bd) { bd = d; bj = j; } }
      this.slot[i] = bj; free[bj] = 0;
    }
  } else if (this.assembled && cnt < this.DISSOLVE_N) { this.assembled = false; this.dissolves++; this.slot.fill(-1); }
  if (this.assembled) {
    const C = this.centre, H = this.head;
    let bd = Infinity, bp = 0;
    for (let q = 0; q < PL.length; q++) { const d = Math.hypot(PL[q].pos[0] - C[0], PL[q].pos[1] - C[1], PL[q].pos[2] - C[2]); if (d < bd) { bd = d; bp = q; } }
    let want = H.slice();
    if (bd < 700) { const u = vunit(PL[bp].pos[0] - C[0], PL[bp].pos[1] - C[1], PL[bp].pos[2] - C[2], [0, 0, 0]); want = vunit(want[0] + 0.8 * u[0], want[1] + 0.8 * u[1], want[2] + 0.8 * u[2], [0, 0, 0]); }
    const mc = arena.massNear(C[0] + H[0] * 200, C[1] + H[1] * 200, C[2] + H[2] * 200, 250);
    if (mc) {
      let mx = 0, my = 0, mz = 0; for (let s = 0; s < mc; s++) { const j = arena.qbuf[s]; mx += arena.mpos[3 * j]; my += arena.mpos[3 * j + 1]; mz += arena.mpos[3 * j + 2]; }
      const u = vunit(mx / mc - C[0], my / mc - C[1], mz / mc - C[2], [0, 0, 0]);
      want = vunit(want[0] + 0.5 * u[0], want[1] + 0.5 * u[1], want[2] + 0.5 * u[2], [0, 0, 0]);
    }
    // trail scent: the newest of the last 300 live trail prisms within 600 u
    let seen = 0, pick = -1;
    for (let j = arena.n - 1; j >= 0 && seen < 300; j--) {
      if (!(arena.malive[j] && arena.mowner[j] >= 0)) continue; seen++;
      if (pick < 0 && Math.hypot(arena.mpos[3 * j] - C[0], arena.mpos[3 * j + 1] - C[1], arena.mpos[3 * j + 2] - C[2]) < 600) pick = j;
    }
    if (pick >= 0) { const u = vunit(arena.mpos[3 * pick] - C[0], arena.mpos[3 * pick + 1] - C[1], arena.mpos[3 * pick + 2] - C[2], [0, 0, 0]); want = vunit(want[0] + 0.9 * u[0], want[1] + 0.9 * u[1], want[2] + 0.9 * u[2], [0, 0, 0]); }
    const cr = Math.hypot(C[0], C[1], C[2]), wall = clamp(cr / arena.R - 0.7, 0, 1) * 5 / arena.R;
    want = vunit(want[0] - C[0] * wall, want[1] - C[1] * wall, want[2] - C[2] * wall, [0, 0, 0]);
    const ang = Math.acos(clamp(H[0] * want[0] + H[1] * want[1] + H[2] * want[2], -1, 1));
    if (ang > 1e-4) { const s = Math.min(1, 0.35 * dt / ang); this.head = vunit(H[0] + (want[0] - H[0]) * s, H[1] + (want[1] - H[1]) * s, H[2] + (want[2] - H[2]) * s, [0, 0, 0]); }
    const Hh = this.head, half = this.LEN * 0.5;
    let mouth = [C[0] + Hh[0] * half, C[1] + Hh[1] * half, C[2] + Hh[2] * half];
    let ahead = false;
    for (const p of PL) { const dx = p.pos[0] - mouth[0], dy = p.pos[1] - mouth[1], dz = p.pos[2] - mouth[2], dn = Math.hypot(dx, dy, dz); if (dn < 220 && (dx * Hh[0] + dy * Hh[1] + dz * Hh[2]) / Math.max(dn, 1e-9) > 0.7) ahead = true; }
    this.gulpRest = Math.max(0, this.gulpRest - dt);
    if (this.gulp > 0) { this.gulp -= dt; if (this.gulp <= 0) this.gulpRest = 4; }
    else if (ahead && this.gulpRest <= 0 && this.ablate !== 'nogulp') { this.gulpPrep += dt; if (this.gulpPrep >= 1.2) { this.gulp = 1.6; this.gulpPrep = 0; this.gulps++; } }
    else this.gulpPrep = Math.max(0, this.gulpPrep - 2 * dt);
    const spd = this.gulp > 0 ? 115 : 35;
    for (let a = 0; a < 3; a++) C[a] += Hh[a] * spd * dt;
    const F = this.frame();
    const tail = [C[0] - Hh[0] * this.LEN * 0.4, C[1] - Hh[1] * this.LEN * 0.4, C[2] - Hh[2] * this.LEN * 0.4];
    let nearTail = false; for (const p of PL) if (Math.hypot(p.pos[0] - tail[0], p.pos[1] - tail[1], p.pos[2] - tail[2]) < 170) nearTail = true;
    this.amp = nearTail ? Math.min(1, this.amp + dt * 0.8) : Math.max(0, this.amp - dt * 0.4);
    const jawOpen = Math.min(1, this.gulpPrep / 1.2 + (this.gulp > 0 ? 1 : 0));
    const g = this.gulp <= 0 ? Math.min(1, this.gulpPrep / 0.6) : 1;
    for (let i = 0; i < N; i++) {
      const sl = this.slot[i] >= 0 ? this.slot[i] : 0;
      let sx = this.slots[3 * sl], sy = this.slots[3 * sl + 1], sz = this.slots[3 * sl + 2];
      const xb = sx / half, w = Math.pow(clamp(-xb, 0, 1), 1.5);
      sy += (12 + 70 * this.amp) * w * Math.sin(2.2 * this.t - 3 * xb);
      const jaw = clamp(xb - 0.6, 0, 1) / 0.4 * jawOpen;
      sy *= 1 + 1.2 * jaw; sz *= 1 + 1.2 * jaw;
      let dx = 0, dy = 0, dz = 0;
      if (this.slot[i] >= 0) {
        for (let a = 0; a < 3; a++) {
          const goal = C[a] + sx * F[a] + sy * F[3 + a] + sz * F[6 + a];
          if (a === 0) dx = (goal - X[3 * i]) * 3; else if (a === 1) dy = (goal - X[3 * i + 1]) * 3; else dz = (goal - X[3 * i + 2]) * 3;
        }
        dx += Hh[0] * spd; dy += Hh[1] * spd; dz += Hh[2] * spd;
        this.steerTo(V, i, dx, dy, dz, 600 * dt);
        const vn = Math.max(Math.hypot(V[3 * i], V[3 * i + 1], V[3 * i + 2]), 1e-6), s = Math.min(1, (120 + spd) / vn);
        V[3 * i] *= s; V[3 * i + 1] *= s; V[3 * i + 2] *= s;
      }
      const xbw = this.slot[i] >= 0 ? xb : 0;
      let it = clamp(1.6 - dist[i] / 150, 0, 1) * A[i];
      if (xbw < -0.3 && dist[i] < 250) it = Math.max(it, this.amp);
      if (xbw > 0.3 && dist[i] < 400) it = Math.max(it, g);
      this.intent[i] = it;
    }
    mouth = [C[0] + Hh[0] * half, C[1] + Hh[1] * half, C[2] + Hh[2] * half];
    const ec = arena.massNear(mouth[0], mouth[1], mouth[2], 35);
    let first = -1; for (let i = 0; i < N; i++) if (A[i]) { first = i; break; }
    for (let s = 0; s < ec; s++) { const j = arena.qbuf[s], v = arena.consume(j); if (v > 0) { this.gut[first] += v; this.eaten += v; } }
    if (this.gulp > 0) for (const p of PL) if (Math.hypot(p.pos[0] - mouth[0], p.pos[1] - mouth[1], p.pos[2] - mouth[2]) < 45 + p.radius && this.burncd <= 0) { arena.hit(p, 'bite', 0.4); this.burncd = 1; }
  } else {
    this.buildNbr(120);
    const des = this.des;
    for (let i = 0; i < N; i++) {
      const c = this.nbr.query(X[3 * i], X[3 * i + 1], X[3 * i + 2], 120), b = this.nbr.buf;
      let hx = 0, hy = 0, hz = 0, hn = 0;
      for (let s = 0; s < c; s++) { const j = b[s]; if (j === i) continue; const dx = X[3 * j] - X[3 * i], dy = X[3 * j + 1] - X[3 * i + 1], dz = X[3 * j + 2] - X[3 * i + 2]; if (dx * dx + dy * dy + dz * dz < 14400) { hx += dx; hy += dy; hz += dz; hn++; } }
      const hm = Math.max(hn, 1);
      const gl = vunit(cx - X[3 * i], cy - X[3 * i + 1], cz - X[3 * i + 2], [0, 0, 0]);
      const u = vunit(hx / hm * 0.03 + gl[0] * 0.6 + this.rng.normal(0, 0.3), hy / hm * 0.03 + gl[1] * 0.6 + this.rng.normal(0, 0.3), hz / hm * 0.03 + gl[2] * 0.6 + this.rng.normal(0, 0.3), [0, 0, 0]);
      des[3 * i] = u[0] * 45; des[3 * i + 1] = u[1] * 45; des[3 * i + 2] = u[2] * 45;
      this.sepInto(i, 14, des, 30);
      this.steerTo(V, i, des[3 * i], des[3 * i + 1], des[3 * i + 2], 90 * dt);
      this.intent[i] = 0;
    }
  }
  this.containAll();
  this.burncd = Math.max(0, this.burncd - dt);
  if (this.assembled && this.burncd <= 0) {
    for (let i = 0; i < N; i++) if (A[i] && this.slot[i] >= 0 && dist[i] < PL[0].radius + this.size[i] + 2) { arena.hit(PL[k[i]], 'burn', 0.2, i); this.burncd = 0.5; break; }
  }
  this.hunterContacts(arena);
  this.publish(arena);
};
Leviathan.prototype.colour = function (i, c) {
  if (!this.assembled) { c[0] = 0.35; c[1] = 0.55; c[2] = 0.95; return c; }
  const it = clamp(this.intent[i], 0, 1);
  c[0] = Math.min(1, 0.35 * 0.6 + 0.4 + 0.6 * it); c[1] = 0.55 * 0.6 + 0.35 * 0.4; c[2] = 0.95 * 0.6 + 0.2 * 0.4; return c;
};

// ------------------------------------------------------------------------------------------------ MOBBER (playful)
const Mobber = inherit(function Mobber(arena, P, o) {
  o = o || {}; this.name = 'mobber';
  for (const kk of ['MAXV', 'DIVE_V', 'PULL']) this[kk] = C_(P, kk, o);
  const n = o.n || P.init.n, roosts = o.roosts || P.init.roosts;
  mkHerd(this, arena, P, n, o.centre);
  const env = envPrisms(arena, o);
  const c = this.rng.choiceNoReplace(env, roosts);
  this.roost = new Float64Array(3 * roosts); for (let r = 0; r < roosts; r++) for (let a = 0; a < 3; a++) this.roost[3 * r + a] = arena.mpos[3 * c[r] + a];
  this.home = new Int32Array(n); for (let i = 0; i < n; i++) this.home[i] = i % roosts;
  for (let i = 0; i < n; i++) for (let a = 0; a < 3; a++) this.pos[3 * i + a] = this.roost[3 * this.home[i] + a] + this.rng.normal(0, 25);
  this.m = new Float64Array(n); this.clock = new Float64Array(n); for (let i = 0; i < n; i++) this.clock[i] = this.rng.uniform(0, 2.5);
  this.pull = new Float64Array(n); this.dive = new Float64Array(n); this.pecks = 0; this.ablate = o.ablate || null;
  this.m2 = new Float64Array(n);
});
Mobber.prototype.act = function (arena, dt) {
  const n = this.n, A = this.alive, X = this.pos, V = this.vel, des = this.des, PL = arena.pilots;
  this.pilotVectors(arena);
  const off = this.off, dist = this.dist, k = this.pk;
  this.buildNbr(120);
  for (let i = 0; i < n; i++) {
    const p = PL[k[i]], ps = Math.hypot(p.vel[0], p.vel[1], p.vel[2]);
    const h = this.home[i], nr = Math.hypot(p.pos[0] - this.roost[3 * h], p.pos[1] - this.roost[3 * h + 1], p.pos[2] - this.roost[3 * h + 2]) < 260;
    let provoke = dist[i] < 300 && (ps < 100 || nr);
    if (this.ablate === 'speedblind') provoke = dist[i] < 300;
    let loud = 0;
    const c = this.nbr.query(X[3 * i], X[3 * i + 1], X[3 * i + 2], 120), b = this.nbr.buf;
    for (let s = 0; s < c; s++) { const j = b[s]; if (j === i) continue; if (Math.hypot(X[3 * j] - X[3 * i], X[3 * j + 1] - X[3 * i + 1], X[3 * j + 2] - X[3 * i + 2]) < 120 && this.m[j] > loud) loud = this.m[j]; }
    const tgt = provoke ? 1 : 0.9 * loud * (dist[i] < 400 ? 1 : 0);
    this.m2[i] = clamp(tgt > this.m[i] ? this.m[i] + (tgt - this.m[i]) * Math.min(1, 2 * dt) : this.m[i] - 0.25 * dt, 0, 1);
  }
  this.m.set(this.m2);
  for (let i = 0; i < n; i++) {
    const p = PL[k[i]], pv = p.vel, ps = Math.hypot(pv[0], pv[1], pv[2]);
    const mob = A[i] && this.m[i] > 0.4;
    const r = Math.max(dist[i], 1e-6), on = Math.max(dist[i], 1e-9);
    const ox = off[3 * i], oy = off[3 * i + 1], oz = off[3 * i + 2];
    if (mob) {
      const rs = clamp(r - 30, -40, 80) * 2;
      // tang = unit(cross(off,[0,1,0]) + cross(off,[1,0,0])*0.3): cross(o,y)=(-oz,0,ox), cross(o,x)=(0,oz,-oy)
      const tx = -oz, ty = 0.3 * oz, tz = ox - 0.3 * oy, tn = Math.max(Math.hypot(tx, ty, tz), 1e-9);
      des[3 * i] = pv[0] + ox / on * rs + tx / tn * 60; des[3 * i + 1] = pv[1] + oy / on * rs + ty / tn * 60; des[3 * i + 2] = pv[2] + oz / on * rs + tz / tn * 60;
    } else {
      const h = this.home[i];
      const u = vunit(this.roost[3 * h] - X[3 * i] + this.rng.normal(0, 15), this.roost[3 * h + 1] - X[3 * i + 1] + this.rng.normal(0, 15), this.roost[3 * h + 2] - X[3 * i + 2] + this.rng.normal(0, 15), [0, 0, 0]);
      des[3 * i] = u[0] * 30; des[3 * i + 1] = u[1] * 30; des[3 * i + 2] = u[2] * 30;
    }
    this.sepInto(i, 6, des, 40);
    if (mob) this.clock[i] -= dt;
    const startpull = this.ablate !== 'nodive' && mob && this.clock[i] <= 0 && this.pull[i] <= 0 && this.dive[i] <= 0 && dist[i] < 80;
    const was = this.pull[i] > 0;
    this.pull[i] = startpull ? this.PULL : Math.max(0, this.pull[i] - dt);
    const godive = was && this.pull[i] <= 0 && mob;
    this.dive[i] = godive ? 0.6 : Math.max(0, this.dive[i] - dt);
    if (startpull) this.clock[i] = 2.5 + this.PULL;
    if (this.pull[i] > 0) { const u = vunit(-ox, -oy + 1, -oz, [0, 0, 0]); des[3 * i] = pv[0] + u[0] * 50; des[3 * i + 1] = pv[1] + u[1] * 50; des[3 * i + 2] = pv[2] + u[2] * 50; }
    if (this.dive[i] > 0) { des[3 * i] = pv[0] + ox / on * this.DIVE_V; des[3 * i + 1] = pv[1] + oy / on * this.DIVE_V; des[3 * i + 2] = pv[2] + oz / on * this.DIVE_V; }
    const pvn = Math.max(ps, 1e-9);
    const pointing = (pv[0] * -ox + pv[1] * -oy + pv[2] * -oz) / pvn / on > 0.9 && dist[i] < 60;
    if (!this._pt) this._pt = new Uint8Array(n);
    this._pt[i] = pointing ? 0 : 1;      // can_kill = ~pointing
    if (pointing) { let sx = -pv[2] + 1e-6, sy = 1e-6, sz = pv[0] + 1e-6; const sn = Math.max(Math.hypot(sx, sy, sz), 1e-9); des[3 * i] += sx / sn * 140; des[3 * i + 1] += sy / sn * 140; des[3 * i + 2] += sz / sn * 140; }
    const vmax = this.dive[i] > 0 ? this.DIVE_V + ps : this.MAXV;
    this.steerTo(V, i, des[3 * i], des[3 * i + 1], des[3 * i + 2], 500 * dt);
    const sp = Math.max(Math.hypot(V[3 * i], V[3 * i + 1], V[3 * i + 2]), 1e-6), s = Math.min(1, vmax / sp);
    V[3 * i] *= s; V[3 * i + 1] *= s; V[3 * i + 2] *= s;
    const swirl = mob && dist[i] < 60;
    this.intent[i] = this.dive[i] > 0 ? 1 : this.pull[i] > 0 ? 0.6 + 0.4 * (1 - this.pull[i] / this.PULL) : swirl ? 0.55 : 0.3 * (mob ? 1 : 0);
  }
  this.containAll();
  for (let i = 0; i < n; i++) if (A[i] && this.dive[i] > 0 && dist[i] < PL[0].radius + 6) {
    const p = PL[k[i]], on = Math.max(dist[i], 1e-9);
    arena.hit(p, 'drain', 0.02, i); this.pecks++; this.dive[i] = 0;
    V[3 * i] = p.vel[0] - off[3 * i] / on * 60; V[3 * i + 1] = p.vel[1] - off[3 * i + 1] / on * 60; V[3 * i + 2] = p.vel[2] - off[3 * i + 2] / on * 60;
  }
  this.hunterContacts(arena, 1.0, this._pt);
  const tm = this._thr || (this._thr = new Uint8Array(n));
  for (let i = 0; i < n; i++) tm[i] = A[i] && this.m[i] > 0.4 ? 1 : 0;
  this.publish(arena, tm);
};
Mobber.prototype.colour = function (i, c) {
  if (this.dive[i] > 0) { c[0] = 1; c[1] = 0.5; c[2] = 0.2; return c; }
  if (this.pull[i] > 0) { c[0] = 1; c[1] = 1; c[2] = 0.6; return c; }
  const m = this.m[i]; c[0] = 0.6 * (1 - 0.5 * m) + 0.5 * m; c[1] = 0.85 * (1 - 0.5 * m) + 0.3 * m; c[2] = 1.0 * (1 - 0.5 * m) + 0.45 * m; return c;
};

// ------------------------------------------------------------------------------------------------ GRAZER (cute base)
const Grazer = inherit(function Grazer(arena, P, o) {
  o = o || {}; this.name = 'grazer';
  for (const kk of ['CAP', 'SPEED', 'FLEE_V', 'SENSE', 'COMFORT', 'CHEW', 'FULL', 'BREED']) this[kk] = C_(P, kk, o);
  const cap = o.cap || this.CAP, n0 = o.n || P.init.n;
  mkHerd(this, arena, P, cap, o.centre);
  const env = envPrisms(arena, o);
  const nc = o.clusters || 4, c = this.rng.choiceNoReplace(env, nc);
  for (let i = 0; i < cap; i++) for (let a = 0; a < 3; a++) this.pos[3 * i + a] = arena.mpos[3 * c[i % nc] + a] + this.rng.normal(0, 40);
  for (let i = 0; i < cap; i++) { if (i >= n0) { this.alive[i] = 0; this.body[i] = 0; } else this.gut[i] = 10; }
  this.fear = new Float64Array(cap); this.chew = new Float64Array(cap); this.breed = new Float64Array(cap);
  for (let i = 0; i < cap; i++) this.chew[i] = this.rng.uniform(0, this.CHEW);
  for (let i = 0; i < cap; i++) this.breed[i] = this.rng.uniform(2, this.BREED);
  this.food = new Int32Array(cap).fill(-1); this.tick = 0; this.births = 0; this.prevD = new Float64Array(cap).fill(Infinity);
  this.ablate = o.ablate || null;
});
Grazer.prototype.act = function (arena, dt) {
  const n = this.n, A = this.alive, X = this.pos, V = this.vel, PL = arena.pilots;
  let m = 0; for (let i = 0; i < n; i++) m += A[i]; if (!m) return;
  this.buildNbr(30);
  this.tick++;
  for (let i = 0; i < n; i++) {
    if (!A[i] || !((i + this.tick) % 4 === 0 || this.food[i] < 0)) continue;
    const c = arena.massNear(X[3 * i], X[3 * i + 1], X[3 * i + 2], 120);
    let best = -1, bd = Infinity;
    for (let s = 0; s < c; s++) { const j = arena.qbuf[s]; if (arena.mowner[j] >= 0) continue; const d = (arena.mpos[3 * j] - X[3 * i]) ** 2 + (arena.mpos[3 * j + 1] - X[3 * i + 1]) ** 2 + (arena.mpos[3 * j + 2] - X[3 * i + 2]) ** 2; if (d < bd) { bd = d; best = j; } }
    this.food[i] = best;
  }
  this.pilotVectors(arena);
  const off = this.off, dist = this.dist, k = this.pk;
  const V0 = this._v0 || (this._v0 = new Float64Array(3 * n)); V0.set(V);   // neighbours read the step's OLD velocities
  for (let i = 0; i < n; i++) {
    const closing = dist[i] < this.prevD[i] - 1e-3; this.prevD[i] = dist[i];
    if (!A[i]) continue;
    const c = this.nbr.query(X[3 * i], X[3 * i + 1], X[3 * i + 2], 30), b = this.nbr.buf;
    let ax = 0, ay = 0, az = 0, cx = 0, cy = 0, cz = 0, cn = 0, sx = 0, sy = 0, sz = 0;
    for (let s = 0; s < c; s++) {
      const j = b[s]; if (j === i) continue;
      const dx = X[3 * j] - X[3 * i], dy = X[3 * j + 1] - X[3 * i + 1], dz = X[3 * j + 2] - X[3 * i + 2], d = Math.sqrt(dx * dx + dy * dy + dz * dz);
      if (d < 30) { ax += V0[3 * j]; ay += V0[3 * j + 1]; az += V0[3 * j + 2]; cx += dx; cy += dy; cz += dz; cn++; }
      if (d < 9) { const w = 1 - d / 9, dd = Math.max(d, 1e-6); sx -= dx / dd * w; sy -= dy / dd * w; sz -= dz / dd * w; }
    }
    const cm = Math.max(cn, 1); ax /= cm; ay /= cm; az /= cm; cx /= cm; cy /= cm; cz /= cm;
    const fj = this.food[i]; let fx = 0, fy = 0, fz = 0;
    if (fj >= 0 && arena.malive[fj]) {
      const dx = arena.mpos[3 * fj] - X[3 * i], dy = arena.mpos[3 * fj + 1] - X[3 * i + 1], dz = arena.mpos[3 * fj + 2] - X[3 * i + 2], dn = Math.max(Math.hypot(dx, dy, dz), 1e-9);
      fx = dx / dn; fy = dy / dn; fz = dz / dn;
      if (dn < 7 && this.chew[i] <= 0) { this.gut[i] += arena.consume(fj); this.chew[i] = this.CHEW; }
    }
    const p = PL[k[i]], ps = Math.hypot(p.vel[0], p.vel[1], p.vel[2]);
    const rush = dist[i] < 60 && closing && ps > 90;
    let fear = rush ? 1 : Math.max(0, this.fear[i] - 0.6 * dt);
    if (this.ablate === 'nofear') fear = 0;
    this.fear[i] = fear;
    const curious = dist[i] < this.SENSE && fear < 0.3 ? 1 : 0, on = Math.max(dist[i], 1e-9);
    const rr = clamp(dist[i] - this.COMFORT, -40, 60) / 60;
    const an = Math.max(Math.hypot(ax + 1e-9, ay + 1e-9, az + 1e-9), 1e-9);
    let dx = (ax + 1e-9) / an * 0.5 + cx * 0.02 + fx + off[3 * i] / on * rr * 1.2 * curious + this.rng.normal(0, 0.35);
    let dy = (ay + 1e-9) / an * 0.5 + cy * 0.02 + fy + off[3 * i + 1] / on * rr * 1.2 * curious + this.rng.normal(0, 0.35);
    let dz = (az + 1e-9) / an * 0.5 + cz * 0.02 + fz + off[3 * i + 2] / on * rr * 1.2 * curious + this.rng.normal(0, 0.35);
    const dn = Math.max(Math.hypot(dx, dy, dz), 1e-9);
    dx = dx / dn * this.SPEED + sx * 40; dy = dy / dn * this.SPEED + sy * 40; dz = dz / dn * this.SPEED + sz * 40;
    if (fear > 0.3) { dx = -off[3 * i] / on * this.FLEE_V + sx * 40; dy = -off[3 * i + 1] / on * this.FLEE_V + sy * 40; dz = -off[3 * i + 2] / on * this.FLEE_V + sz * 40; }
    this.steerTo(V, i, dx, dy, dz, (60 + 200 * fear) * dt);
    this.intent[i] = 0;
  }
  this.containAll();
  for (let i = 0; i < n; i++) { this.chew[i] = Math.max(0, this.chew[i] - dt); this.breed[i] = Math.max(0, this.breed[i] - dt); }
  let fp = 0; const free = this._free || (this._free = []); free.length = 0;
  for (let i = 0; i < n; i++) if (!A[i]) free.push(i);
  const par = this._par || (this._par = []); par.length = 0;
  for (let i = 0; i < n; i++) if (A[i] && this.gut[i] >= this.FULL && this.breed[i] <= 0) par.push(i);
  for (const i of par) {
    if (fp >= free.length) break;
    const j = free[fp++]; A[j] = 1; this.body[j] = 4; this.gut[i] -= 4; this.gut[j] = 0;
    for (let a = 0; a < 3; a++) { X[3 * j + a] = X[3 * i + a] + this.rng.normal(0, 3); V[3 * j + a] = V[3 * i + a]; }
    this.fear[j] = 0; this.breed[i] = this.BREED; this.breed[j] = this.BREED; this.births++;
    if (arena.onBirth) arena.onBirth(this, j);
  }
  this.hunterContacts(arena);
  const none = this._none || (this._none = new Uint8Array(n));
  this.publish(arena, none);
};
Grazer.prototype.colour = function (i, c) { const f = this.fear[i]; c[0] = 0.45 * (1 - 0.4 * f) + 0.4 * f; c[1] = 0.95 * (1 - 0.4 * f) + 0.4 * f; c[2] = 1.0; return c; };
