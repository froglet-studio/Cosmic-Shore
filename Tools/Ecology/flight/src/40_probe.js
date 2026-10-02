// ===================================================================================================================
// 40_probe.js - the shared threat + feel scorecard (common/scorecard.py Probe) plus the bestiary's FIRST-STRIKE
// telegraph (bestiary/run.py TelProbe). Same definitions, so a JS number and a Python number mean the same thing.
// The affect/emotion block of the Python probe is NOT ported (it reads the frozen probe.json; the flight page asks
// the human instead - see the rating panel).
// ===================================================================================================================
function Probe(dt) {
  this.dt = dt; this.sizes = []; this.approach = []; this.coh = []; this.jerk = [];
  this.speeds = new Float64Array(1 << 16); this.nSpeeds = 0;
  this.hist = []; this.intentOn = {}; this.leads = []; this.nHitsSeen = 0; this.pilotSpeed = 1; this.series = {};
  this.rng = new Rng(4242);
}
Probe.prototype.observe = function (arena, v) {
  const m = v.m, P = v.P, V = v.V, S = v.S, I = v.I, PL = arena.pilots;
  // TelProbe series: nearest agent's intent per pilot, every observation
  for (const p of PL) {
    let val = 0;
    if (m) { let bd = Infinity, bj = 0; for (let j = 0; j < m; j++) { const d = (P[3 * j] - p.pos[0]) ** 2 + (P[3 * j + 1] - p.pos[1]) ** 2 + (P[3 * j + 2] - p.pos[2]) ** 2; if (d < bd) { bd = d; bj = j; } } val = I ? I[bj] : 0; }
    (this.series[p.name] || (this.series[p.name] = [])).push([arena.t, val]);
  }
  if (!m) return;
  const sz = Array.from(S.subarray(0, m)).sort((a, b) => a - b); this.sizes.push(median(sz));
  if (this.nSpeeds + m > this.speeds.length) { const b = new Float64Array((this.nSpeeds + m) * 2); b.set(this.speeds.subarray(0, this.nSpeeds)); this.speeds = b; }
  for (let j = 0; j < m; j++) this.speeds[this.nSpeeds++] = Math.hypot(V[3 * j], V[3 * j + 1], V[3 * j + 2]);
  let ps = 0; for (const p of PL) ps += p.speed; this.pilotSpeed = PL.length ? ps / PL.length : 1;
  for (const p of PL) {
    let s = 0, c = 0;
    for (let j = 0; j < m; j++) {
      const dx = P[3 * j] - p.pos[0], dy = P[3 * j + 1] - p.pos[1], dz = P[3 * j + 2] - p.pos[2], d = Math.sqrt(dx * dx + dy * dy + dz * dz);
      if (d < 300) { s += ((V[3 * j] - p.vel[0]) * dx + (V[3 * j + 1] - p.vel[1]) * dy + (V[3 * j + 2] - p.vel[2]) * dz) / Math.max(d, 1e-6); c++; }
    }
    if (c) this.approach.push(s / c);
  }
  if (m >= 4) {   // coherence: up to 256 sampled agents vs their 12 nearest (self included, like numpy's argsort)
    const kk = Math.min(12, m), ns = Math.min(256, m), idx = [];
    if (m <= 256) for (let j = 0; j < m; j++) idx.push(j); else { const all = []; for (let j = 0; j < m; j++) all.push(j); const ch = this.rng.choiceNoReplace(all, 256); for (const j of ch) idx.push(j); }
    let tot = 0; const dd = new Float64Array(m), ord = new Int32Array(m);
    for (const i of idx) {
      for (let j = 0; j < m; j++) { dd[j] = (P[3 * j] - P[3 * i]) ** 2 + (P[3 * j + 1] - P[3 * i + 1]) ** 2 + (P[3 * j + 2] - P[3 * i + 2]) ** 2; ord[j] = j; }
      const o = Array.from(ord).sort((a, b) => dd[a] - dd[b]).slice(0, kk);
      let ux = 0, uy = 0, uz = 0;
      for (const j of o) { const s = Math.max(Math.hypot(V[3 * j], V[3 * j + 1], V[3 * j + 2]), 1e-6); ux += V[3 * j] / s; uy += V[3 * j + 1] / s; uz += V[3 * j + 2] / s; }
      tot += Math.hypot(ux / kk, uy / kk, uz / kk);
    }
    this.coh.push(tot / ns);
  }
  this.hist.push(Float64Array.from(P.subarray(0, 3 * m))); if (this.hist.length > 4) this.hist.shift();
  if (this.hist.length === 4 && this.hist.every(h => h.length === 3 * m)) {
    const [h0, h1, h2, h3] = this.hist; let js = 0, ss = 0;
    for (let j = 0; j < m; j++) {
      let jx = 0, sx = 0;
      for (let a = 0; a < 3; a++) { const q = h3[3 * j + a] - 3 * h2[3 * j + a] + 3 * h1[3 * j + a] - h0[3 * j + a]; jx += q * q; const st = h3[3 * j + a] - h2[3 * j + a]; sx += st * st; }
      js += Math.sqrt(jx); ss += Math.sqrt(sx);
    }
    this.jerk.push((js / m) / Math.max(ss / m, 1e-6));
  }
  // telegraph: credit a hit to an intent that was ALREADY showing (process hits first)
  for (let q = this.nHitsSeen; q < arena.log.length; q++) { const [t, name] = arena.log[q]; if (name in this.intentOn) { this.leads.push(t - this.intentOn[name]); delete this.intentOn[name]; } }
  this.nHitsSeen = arena.log.length;
  if (I) for (const p of PL) {
    let bd = Infinity, bj = 0; for (let j = 0; j < m; j++) { const d = (P[3 * j] - p.pos[0]) ** 2 + (P[3 * j + 1] - p.pos[1]) ** 2 + (P[3 * j + 2] - p.pos[2]) ** 2; if (d < bd) { bd = d; bj = j; } }
    if (I[bj] > 0.5 && !(p.name in this.intentOn)) this.intentOn[p.name] = arena.t;
    if (I[bj] < 0.2 && Math.sqrt(bd) > 200) delete this.intentOn[p.name];
  }
};
/** bestiary/run.py first_leads: per engagement (no hit on that pilot in the previous 3 s), how long the nearest
 *  agent's intent had been > 0.5 continuously right before the first strike. */
Probe.prototype.firstLeads = function (arena) {
  const GAP = 3.0, out = [];
  for (const name in this.series) {
    const ser = this.series[name], hits = arena.log.filter(h => h[1] === name).map(h => h[0]).sort((a, b) => a - b);
    let last = -1e9;
    for (const th of hits) {
      if (th - last > GAP) {
        let j = -1; for (let q = 0; q < ser.length; q++) if (ser[q][0] < th - 1e-6) j = q; else break;
        let lead = 0; while (j >= 0 && ser[j][1] > 0.5) { lead = th - ser[j][0]; j--; }
        out.push(lead);
      }
      last = th;
    }
  }
  return out;
};
Probe.prototype.feel = function () {
  const sp = Array.from(this.speeds.subarray(0, this.nSpeeds)).sort((a, b) => a - b);
  const med = sp.length ? median(sp) : 0;
  const r = (x, d) => Math.round(x * 10 ** d) / 10 ** d;
  return {
    size: r(this.sizes.length ? median(this.sizes.slice().sort((a, b) => a - b)) : 0, 2),
    speed_rel: r(med / Math.max(this.pilotSpeed, 1e-6), 3),
    approach: r(this.approach.length ? mean(this.approach) : 0, 2),
    coherence: r(this.coh.length ? mean(this.coh) : 0, 3),
    burstiness: r((sp.length ? percentile(sp, 95) : 0) / Math.max(med, 1e-6), 2),
    jerk_rel: r(this.jerk.length ? mean(this.jerk) : 0, 3),
  };
};
function mean(a) { let s = 0; for (const x of a) s += x; return a.length ? s / a.length : 0; }
function median(sorted) { const n = sorted.length; if (!n) return 0; return n % 2 ? sorted[(n - 1) / 2] : 0.5 * (sorted[n / 2 - 1] + sorted[n / 2]); }
function percentile(sorted, q) { const n = sorted.length; if (!n) return 0; const pos = (n - 1) * q / 100, lo = Math.floor(pos), hi = Math.ceil(pos); return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo); }
