/* swarm_loss.js - the phase-1 shape yardstick in plain JS (no torch), for tests and the demo's readout.
 *
 * A line-by-line restatement of Tools/NCA/swarm_nca.py swarm_loss() with the default LossCfg: the debiased entropic
 * Sinkhorn divergence between the swarm's live tadpoles and the best (animation frame, slot -> domain assignment) of a
 * body plan, over a cost that adds heart position (Huber, pos_scale 2), element (60), domain region (25), prism
 * half-extents (3), tier (6), facing (1.5) and spindle (1). It is the "sink" number swarm_eval.py / sort_eval.py
 * print as own-plan loss; the phase-1 pass bar is loss < 8 and strictly closest to the wanted plan.
 *
 *   const L = require('./swarm_loss.js');
 *   L.loss(x, plan) -> { sink, frame, perm }      x = { p:[n][3], elem, dom, h:[n][3], tier, f:[n][3], sp:[n][2] }
 *   L.scoreAll(x, SwarmPlans.plans) -> { mass: 3.1, space: 41.2, ... }
 */
(function (root) {
  'use strict';
  const LC = { pos_scale: 2.0, w_elem: 60, w_dom: 25, w_h: 3, w_tier: 6, w_face: 1.5, w_sp: 1, eps: 0.05 };

  function perms(k) {               // itertools.permutations(range(3), k)
    const out = [];
    const rec = (cur) => {
      if (cur.length === k) { out.push(cur.slice()); return; }
      for (let i = 0; i < 3; i++) if (!cur.includes(i)) { cur.push(i); rec(cur); cur.pop(); }
    };
    rec([]); return out;
  }
  const LOSS_PERMS = { 1: perms(1), 2: perms(2), 3: perms(3) };

  const poscost = d2 => d2 < 16 ? d2 : 8 * Math.sqrt(Math.max(d2, 1e-9)) - 16;

  function centred(p) {
    const n = p.length, c = [0, 0, 0];
    for (const q of p) { c[0] += q[0]; c[1] += q[1]; c[2] += q[2]; }
    c[0] /= n; c[1] /= n; c[2] /= n;
    return p.map(q => [q[0] - c[0], q[1] - c[1], q[2] - c[2]]);
  }

  // A: {pc, elem, dom, h, tierOH, f, sp} ; B likewise ; domB: function j -> domain id of B's unit j
  function costMatrix(A, B, domB) {
    const n = A.pc.length, m = B.pc.length, C = new Float64Array(n * m), s2 = 2 * LC.pos_scale * LC.pos_scale;
    for (let i = 0; i < n; i++) {
      const pa = A.pc[i], ha = A.h[i], fa = A.f[i], ta = A.tierOH[i], sa = A.sp[i], ea = A.elem[i], da = A.dom[i];
      for (let j = 0; j < m; j++) {
        const pb = B.pc[j], hb = B.h[j], fb = B.f[j], tb = B.tierOH[j], sb = B.sp[j];
        const dx = pa[0] - pb[0], dy = pa[1] - pb[1], dz = pa[2] - pb[2];
        let c = poscost((dx * dx + dy * dy + dz * dz) / s2);
        if (ea !== B.elem[j]) c += LC.w_elem;
        if (da !== domB(j)) c += LC.w_dom;
        const h0 = ha[0] - hb[0], h1 = ha[1] - hb[1], h2 = ha[2] - hb[2];
        c += LC.w_h * (h0 * h0 + h1 * h1 + h2 * h2);
        const t0 = ta[0] - tb[0], t1 = ta[1] - tb[1], t2 = ta[2] - tb[2];
        c += LC.w_tier * (t0 * t0 + t1 * t1 + t2 * t2);
        c += LC.w_face * (1 - (fa[0] * fb[0] + fa[1] * fb[1] + fa[2] * fb[2]));
        const s0 = sa[0] - sb[0], s1 = sa[1] - sb[1];
        c += LC.w_sp * (s0 * s0 + s1 * s1);
        C[i * m + j] = c;
      }
    }
    return C;
  }

  // entropic OT value with eps-scaling (swarm_nca.sinkhorn_ot); uniform marginals
  function sinkhorn(C, n, m, eps, itersPer) {
    itersPer = itersPer || 3;
    const la = Math.log(1 / n), lb = Math.log(1 / m);
    const f = new Float64Array(n), g = new Float64Array(m);
    let cmax = -Infinity; for (let k = 0; k < C.length; k++) if (C[k] > cmax) cmax = C[k];
    let e = Math.max(cmax, eps);
    const rowLse = (gg, ee, out) => {        // out_i = -ee * LSE_j(lb + (gg_j - C_ij)/ee)
      for (let i = 0; i < n; i++) {
        let mx = -Infinity; const o = i * m;
        for (let j = 0; j < m; j++) { const v = (gg[j] - C[o + j]) / ee; if (v > mx) mx = v; }
        let s = 0; for (let j = 0; j < m; j++) s += Math.exp((gg[j] - C[o + j]) / ee - mx);
        out[i] = -ee * (lb + mx + Math.log(s));
      }
    };
    const colLse = (ff, ee, out) => {
      const mx = new Float64Array(m).fill(-Infinity), s = new Float64Array(m);
      for (let i = 0; i < n; i++) { const o = i * m; for (let j = 0; j < m; j++) { const v = (ff[i] - C[o + j]) / ee; if (v > mx[j]) mx[j] = v; } }
      for (let i = 0; i < n; i++) { const o = i * m; for (let j = 0; j < m; j++) s[j] += Math.exp((ff[i] - C[o + j]) / ee - mx[j]); }
      for (let j = 0; j < m; j++) out[j] = -ee * (la + mx[j] + Math.log(s[j]));
    };
    for (let guard = 0; guard < 200; guard++) {
      for (let it = 0; it < itersPer; it++) { rowLse(g, e, f); colLse(f, e, g); }
      if (e <= eps) break;
      e = Math.max(e * 0.5, eps);
    }
    const fd = new Float64Array(n), gd = new Float64Array(m);
    rowLse(g, eps, fd); colLse(f, eps, gd);
    let v = 0; for (let i = 0; i < n; i++) v += fd[i] / n; for (let j = 0; j < m; j++) v += gd[j] / m;
    return v;
  }

  const OH = t => [t === 0 ? 1 : 0, t === 1 ? 1 : 0, t === 2 ? 1 : 0];

  function prepX(x) {
    return { pc: centred(x.p), elem: x.elem, dom: x.dom, h: x.h, f: x.f, sp: x.sp,
             tierOH: x.tier.map((t, i) => x.elem[i] === 0 ? OH(t) : [1, 0, 0]) };
  }

  const _cache = new WeakMap();
  function planFrames(plan) {
    if (_cache.has(plan)) return _cache.get(plan);
    const n = plan.n, fr = [];
    for (let k = 0; k < plan.frames; k++) {
      const pc = [], f = [];
      for (let i = 0; i < n; i++) {
        const o = (k * n + i) * 3;
        pc.push([plan.P[o], plan.P[o + 1], plan.P[o + 2]]); f.push([plan.F[o], plan.F[o + 1], plan.F[o + 2]]);
      }
      const h = [], sp = [];
      for (let i = 0; i < n; i++) { h.push(plan.H.slice(3 * i, 3 * i + 3)); sp.push(plan.SP.slice(2 * i, 2 * i + 2)); }
      fr.push({ pc: centred(pc), f, h, sp, elem: plan.elem, dom: plan.slot, slot: plan.slot,
                tierOH: plan.tier.map(t => OH(t)), self: null });
    }
    _cache.set(plan, fr);
    return fr;
  }

  /** Debiased Sinkhorn divergence of swarm x to plan (swarm_nca._divergence, L.rel_elem = 0). */
  function loss(x, plan, opt) {
    opt = opt || {};
    if (!x.p.length) return { sink: 100, frame: 0, perm: [0] };
    const X = prepX(x), n = X.pc.length, frames = planFrames(plan), m = plan.n;
    const P = LOSS_PERMS[plan.slots];
    const fset = opt.frames || frames.map((_, k) => k);
    let best = null;
    for (const k of fset) {
      const t = frames[k];
      for (const perm of P) {
        const C = costMatrix(X, t, j => perm[t.slot[j]]);
        const v = sinkhorn(C, n, m, Math.max(LC.eps, 0.5), 2);
        if (!best || v < best.v) best = { v, k, perm, C };
      }
    }
    const t = frames[best.k];
    const oab = sinkhorn(best.C, n, m, LC.eps, 3);
    const oaa = sinkhorn(costMatrix(X, X, i => X.dom[i]), n, n, LC.eps, 3);
    if (t.self === null) t.self = sinkhorn(costMatrix(t, t, j => t.slot[j]), m, m, LC.eps, 3);
    return { sink: oab - 0.5 * oaa - 0.5 * t.self, frame: best.k, perm: best.perm };
  }

  function scoreAll(x, plans, opt) {
    const out = {};
    for (const k of ['mass', 'space', 'charge', 'time']) out[k] = Math.round(loss(x, plans[k], opt).sink * 100) / 100;
    return out;
  }

  /** The plan's own frame k as an x (for the self-check: loss(planX(plan, 0), plan) must be ~0). */
  function planX(plan, k) {
    const t = planFrames(plan)[k || 0];
    return { p: t.pc, elem: plan.elem.slice(), dom: plan.slot.slice(), h: t.h, tier: plan.tier.slice(), f: t.f, sp: t.sp };
  }

  const API = { loss, scoreAll, planX, sinkhorn, LC };
  if (typeof module !== 'undefined' && module.exports) module.exports = API;
  if (root) root.SwarmLoss = API;
})(typeof window !== 'undefined' ? window : (typeof globalThis !== 'undefined' ? globalThis : this));
