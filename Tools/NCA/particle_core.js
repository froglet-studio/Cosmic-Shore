// Collision automaton — inference step for particle_nca.ParticleNCA (verify_particle_js.py
// holds the two equal). Particles in fixed slots: pos [cap*d], s [cap*C], active [cap].
// Per step: pair list within R -> perception (own, neighbour mean, state gradient, crowding)
// -> MLP on alive + firing particles -> state delta + velocity -> designed collision push ->
// alive test before/after -> designed budding. O(N^2) pair search: fine at a few hundred
// particles; the engine port would use the prism spatial index instead.
function makeParticleNCA(weights) {
  const W = weights.world, d = W.dim, C = weights.channel_n, HID = weights.hidden;
  const F = C * (2 + d) + 1, OUT = C + d, cap = W.capacity;
  const w1 = new Float32Array(weights.w1.flat()), b1 = new Float32Array(weights.b1);
  const w2 = new Float32Array(weights.w2.flat()), b2 = new Float32Array(weights.b2);
  const pos = new Float32Array(cap * d), s = new Float32Array(cap * C), act = new Uint8Array(cap);
  const npos = new Float32Array(cap * d), ns = new Float32Array(cap * C);
  const pre = new Uint8Array(cap), post = new Uint8Array(cap), cnt = new Float32Array(cap);
  const feat = new Float32Array(F), hid = new Float32Array(HID), out = new Float32Array(OUT);
  const cen = new Float32Array(cap * d);
  let ei = new Int32Array(0), ej = new Int32Array(0);
  const R2 = W.R * W.R;

  function edges() {
    const a = [], b = [];
    for (let i = 0; i < cap; i++) {
      if (!act[i]) continue;
      for (let j = 0; j < cap; j++) {
        if (j === i || !act[j]) continue;
        let r2 = 0; for (let k = 0; k < d; k++) { const t = pos[j * d + k] - pos[i * d + k]; r2 += t * t; }
        if (r2 < R2) { a.push(i); b.push(j); }
      }
    }
    ei = Int32Array.from(a); ej = Int32Array.from(b);
  }
  const amax = new Float32Array(cap);   // float scratch: out is a Uint8Array and would truncate alpha
  function aliveMask(st, out) {
    for (let i = 0; i < cap; i++) amax[i] = st[i * C + 3];
    for (let e = 0; e < ei.length; e++) { const v = st[ej[e] * C + 3]; if (v > amax[ei[e]]) amax[ei[e]] = v; }
    for (let i = 0; i < cap; i++) out[i] = act[i] && amax[i] > 0.1 ? 1 : 0;
  }

  function step(opts) {
    opts = opts || {};
    const rate = opts.fireRate ?? weights.fire_rate, rnd = opts.random || Math.random, bud = opts.bud !== false;
    edges();
    aliveMask(s, pre);
    // perception accumulators, per particle
    const rho = new Float32Array(cap), gs = new Float32Array(cap);
    const mean = new Float32Array(cap * C), grad = new Float32Array(cap * d * C);
    const mom = new Float32Array(cap * d * d);   // corrected perception: sum g u u^T
    cnt.fill(0); cen.fill(0); mom.fill(0);
    for (let e = 0; e < ei.length; e++) {
      const i = ei[e], j = ej[e];
      let r2 = 0; const dx = [0, 0, 0];
      for (let k = 0; k < d; k++) { dx[k] = pos[j * d + k] - pos[i * d + k]; r2 += dx[k] * dx[k]; }
      const q = Math.max(0, 1 - r2 / R2), w = q * q * q, g = q * q;
      rho[i] += w; gs[i] += g; cnt[i] += 1;
      for (let k = 0; k < d; k++) cen[i * d + k] += dx[k];
      if (W.corrected) for (let a = 0; a < d; a++) for (let b = 0; b < d; b++) mom[(i * d + a) * d + b] += g * (dx[a] / W.R) * (dx[b] / W.R);
      for (let c = 0; c < C; c++) {
        mean[i * C + c] += w * s[j * C + c];
        const diff = s[j * C + c] - s[i * C + c];
        for (let k = 0; k < d; k++) grad[(i * d + k) * C + c] += g * (dx[k] / W.R) * diff;
      }
    }
    const gsol = new Float32Array(d * C), A = new Float64Array(d * (d + C));
    function solveMoment(i) {
      const m = d + C;
      for (let a = 0; a < d; a++) {
        for (let b = 0; b < d; b++) A[a * m + b] = mom[(i * d + a) * d + b] + (a === b ? W.reg : 0);
        for (let c = 0; c < C; c++) A[a * m + d + c] = grad[(i * d + a) * C + c];
      }
      for (let col = 0; col < d; col++) {
        let piv = col;
        for (let r = col + 1; r < d; r++) if (Math.abs(A[r * m + col]) > Math.abs(A[piv * m + col])) piv = r;
        if (piv !== col) for (let q = 0; q < m; q++) { const tmp = A[col * m + q]; A[col * m + q] = A[piv * m + q]; A[piv * m + q] = tmp; }
        for (let r = 0; r < d; r++) {
          if (r === col) continue;
          const fct = A[r * m + col] / A[col * m + col];
          for (let q = col; q < m; q++) A[r * m + q] -= fct * A[col * m + q];
        }
      }
      for (let k = 0; k < d; k++) for (let c = 0; c < C; c++) gsol[k * C + c] = A[k * m + d + c] / A[k * m + k];
    }
    ns.set(s); npos.set(pos);
    for (let i = 0; i < cap; i++) {
      if (!pre[i]) continue;
      if (!(rnd() <= rate)) continue;
      let f = 0;
      for (let c = 0; c < C; c++) feat[f++] = s[i * C + c];
      if (W.corrected) {
        for (let c = 0; c < C; c++) feat[f++] = mean[i * C + c] / (rho[i] + 1e-3);
        solveMoment(i);
        for (let c = 0; c < C; c++) for (let k = 0; k < d; k++) feat[f++] = gsol[k * C + c];
      } else {
        for (let c = 0; c < C; c++) feat[f++] = mean[i * C + c] / (1 + rho[i]);
        for (let c = 0; c < C; c++) for (let k = 0; k < d; k++) feat[f++] = grad[(i * d + k) * C + c] / (1 + gs[i]);
      }
      feat[f++] = rho[i] / W.rho0;
      for (let h = 0; h < HID; h++) {
        let a = b1[h]; const o = h * F;
        for (let q = 0; q < F; q++) a += w1[o + q] * feat[q];
        hid[h] = a > 0 ? a : 0;
      }
      for (let o = 0; o < OUT; o++) {
        let a = b2[o]; const off = o * HID;
        for (let h = 0; h < HID; h++) a += w2[off + h] * hid[h];
        out[o] = a;
      }
      for (let c = 0; c < C; c++) ns[i * C + c] += out[c];
      for (let k = 0; k < d; k++) npos[i * d + k] += W.vmax * Math.tanh(out[C + k]);
    }
    // designed collision: push apart pairs closer than r0 (positions after the learned move)
    const push = new Float32Array(cap * d);
    for (let e = 0; e < ei.length; e++) {
      const i = ei[e], j = ej[e]; let r2 = 0; const dx = [0, 0, 0];
      for (let k = 0; k < d; k++) { dx[k] = npos[j * d + k] - npos[i * d + k]; r2 += dx[k] * dx[k]; }
      const r = Math.sqrt(Math.max(r2, 1e-8)), ov = Math.max(0, W.r0 - r) / W.r0;
      for (let k = 0; k < d; k++) push[i * d + k] -= W.rep * W.r0 * 0.5 * ov * dx[k] / r;
    }
    for (let i = 0; i < cap; i++) if (act[i]) for (let k = 0; k < d; k++) npos[i * d + k] += push[i * d + k];
    aliveMask(ns, post);
    for (let i = 0; i < cap; i++) {
      const keep = pre[i] && post[i];
      if (!keep) ns.fill(0, i * C, i * C + C);
      act[i] = keep ? 1 : 0;
    }
    s.set(ns); pos.set(npos);
    if (bud) budStep(rnd);
  }

  function budStep(rnd) {   // designed growth (see particle_nca.ParticleNCA.bud)
    const parents = [];
    for (let i = 0; i < cap; i++)
      if (act[i] && s[i * C + 3] > 0.1 && cnt[i] < W.k_bud && rnd() <= 0.5) parents.push(i);
    let free = 0;
    for (const p of parents) {
      while (free < cap && act[free]) free++;
      if (free >= cap) break;
      const away = [], n = [];
      let an = 0, nn = 0;
      for (let k = 0; k < d; k++) { away.push(-cen[p * d + k]); an += away[k] ** 2; }
      for (let k = 0; k < d; k++) { const g = gauss(rnd); n.push(g); nn += g * g; }
      an = Math.sqrt(an) || 1e-6; nn = Math.sqrt(nn) || 1e-6;
      let dir = away.map((v, k) => v / an + 0.6 * n[k] / nn), dn = Math.hypot(...dir) || 1e-6;
      for (let k = 0; k < d; k++) pos[free * d + k] = pos[p * d + k] + W.r_bud * dir[k] / dn;
      s.fill(0, free * C, free * C + C);
      act[free] = 1;
      free++;
    }
  }
  function gauss(rnd) { return Math.sqrt(-2 * Math.log(Math.max(1e-9, rnd()))) * Math.cos(2 * Math.PI * rnd()); }

  return {
    d, C, cap, pos, s, act, step,
    seed(centre) {
      pos.fill(0); s.fill(0); act.fill(0);
      for (let k = 0; k < d; k++) pos[k] = centre[k];
      for (let c = 3; c < C; c++) s[c] = 1;
      act[0] = 1;
    },
    count() { let n = 0; for (let i = 0; i < cap; i++) n += act[i]; return n; },
    eraseBall(centre, r) {
      for (let i = 0; i < cap; i++) {
        if (!act[i]) continue;
        let r2 = 0; for (let k = 0; k < d; k++) r2 += (pos[i * d + k] - centre[k]) ** 2;
        if (r2 < r * r) { act[i] = 0; s.fill(0, i * C, i * C + C); }
      }
    },
  };
}
if (typeof module !== "undefined") module.exports = { makeParticleNCA };
