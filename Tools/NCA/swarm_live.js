/* swarm_live.js - a LIVE port of the grid-morphogen swarm (hgrid "oracle": hgrid_core.py + hgrid_boid.py
 * FieldBoid / OracleField, config results/hgrid/oracle/summary.json meta.cfg) that runs in real time in the
 * browser and in node.
 *
 * One swarm, one Float32Array per state channel. Each step:
 *   1. the grid (16^3 cells of 6 voxels) snaps to the swarm's centroid; the plan is the MAJORITY element's
 *   2. the plan's rasterised target field D (wanted density per element x slot + look attributes + flow) is
 *      a constant per (plan, animation frame) relative to the grid, so it is built once and cached
 *   3. tadpoles splat their class (element x domain) into the grid; deficit = wanted - actual
 *   4. each tadpole climbs its own class deficit (x3) + the all-class deficit (x1) + the plan's flow (x4),
 *      with persistence 0.6, noise, element top speed, designed collision and the cell membrane
 *   5. prism / tier / facing / spindle ease toward the field's attributes
 *   6. eggs hatch over two steps; a class the swarm holds 15% too much of, sitting where it is not wanted,
 *      starves (withers to a crystal); a class with room lays eggs toward its deficit (domain breeds true)
 *
 * Exactly the Python step, operation for operation (fidelity: live_export.py score). Only the random
 * numbers differ. Usage:
 *   const L = require('./swarm_live.js'); const sw = new L.LiveSwarm(targets, {capacity: 280});
 *   sw.seedPlan('mass', rng) | sw.seedRandom({n: 24, bias: -1}); sw.step(); sw.units(); sw.killBall(c, r)
 * node:  node swarm_live.js fidelity results/live/targets.json out.json [seeds]   (grow every plan, export)
 *        node swarm_live.js bench results/live/targets.json
 */
(function (root) {
  'use strict';
  const KINDS = ['mass', 'space', 'charge', 'time'];
  const MAJOR = { mass: 1, space: 2, charge: 0, time: 3 };           // plan -> its major element
  const PLAN_OF = [2, 0, 1, 3];                                       // element -> plan index (Charge->charge, Mass->mass...)
  const PERM3 = [[0, 1, 2], [0, 2, 1], [1, 0, 2], [1, 2, 0], [2, 0, 1], [2, 1, 0]];
  const NCLS = 12, FIELD_C = 47, FLOW_C = 12, DC = FIELD_C + FLOW_C;
  // state rows per tadpole (as swarm_nca channels): A | FAC 3 | PR 3 | TI 3 | SP 2 | DIE ; VEL is hidden 12-14
  const DEF_CFG = {
    G: 16, cell: 6.0, quant: 1, k_class: 3.0, k_total: 1.0, k_home: 0.15, persist: 0.6, noise: 0.05,
    p_lay: 0.1, p_cross: 0.25, hatch_steps: 2, ease: 0.3, starve: 1, starve_rate: 0.04, starve_tol: 0.15,
    starve_local: 0.0, hyst: 0.0, animate: 1, flow: 1, k_flow: 4.0, period: 8,
  };
  const DEF_WORLD = { R: 8.0, r0: 2.4, rep: 0.4, vmax: [0.8, 0.8, 0.8, 2.0], r_bud: 2.6, membrane: 80.0, capacity: 280, DIE_AT: 1.0 };

  // ------------------------------------------------------------------ random numbers ---
  function mulberry32(a) {
    return function () {
      a |= 0; a = a + 0x6D2B79F5 | 0;
      let t = Math.imul(a ^ a >>> 15, 1 | a);
      t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
      return ((t ^ t >>> 14) >>> 0) / 4294967296;
    };
  }
  function makeRng(seed) {
    const u = mulberry32(seed >>> 0);
    let spare = null;
    u.normal = () => {
      if (spare !== null) { const s = spare; spare = null; return s; }
      let a = 0, b = 0; while (a < 1e-12) a = u(); b = u();
      const r = Math.sqrt(-2 * Math.log(a)); spare = r * Math.sin(2 * Math.PI * b); return r * Math.cos(2 * Math.PI * b);
    };
    return u;
  }

  // ------------------------------------------------------------------ element identity ---
  const sig = x => 1 / (1 + Math.exp(-x));
  const clamp = (x, lo, hi) => x < lo ? lo : x > hi ? hi : x;
  /** swarm_nca.prism_h: raw [3] -> half-extents inside each element's range */
  function prismH(r0, r1, r2, e, out) {
    const s0 = sig(r0), s1 = sig(r1), s2 = sig(r2);
    if (e === 0) { out[0] = 0.3 + 1.9 * s0; out[1] = 0.3 + 1.9 * s1; out[2] = 0.3 + 1.9 * s2; }
    else if (e === 1) {
      const a = 0.5 + 1.3 * s0, b = 0.5 + 1.3 * s1, c = 0.5 + 1.3 * s2, m = 1.6 * Math.min(a, b, c);
      out[0] = Math.min(a, m); out[1] = Math.min(b, m); out[2] = Math.min(c, m);
    } else if (e === 2) {
      const L = 0.9 + 2.1 * s0, cm = clamp(L / 4, 0.25, 0.42);
      out[0] = L; out[1] = 0.25 + (cm - 0.25) * s1; out[2] = 0.25 + (cm - 0.25) * s2;
    } else {
      const L = 0.45 + 0.75 * s0, cm = clamp(L / 1.5, 0.3, 0.6);
      out[0] = L; out[1] = 0.3 + (cm - 0.3) * s1; out[2] = 0.3 + (cm - 0.3) * s2;
    }
    return out;
  }
  const logit = (x, lo, hi) => { const s = clamp((x - lo) / Math.max(hi - lo, 1e-6), 0.02, 0.98); return Math.log(s / (1 - s)); };
  /** hgrid_core.raw_prism (inverse of prism_h, up to the Mass ratio clamp) */
  function rawPrism(h, e) {
    if (e === 0) return h.map(x => logit(x, 0.3, 2.2));
    if (e === 1) return h.map(x => logit(x, 0.5, 1.8));
    const L = h[0];
    const [lo, hi, c0, cdiv, clo, chi] = e === 2 ? [0.9, 3.0, 0.25, 4, 0.25, 0.42] : [0.45, 1.2, 0.3, 1.5, 0.3, 0.6];
    const cmax = clamp(L / cdiv, clo, chi);
    const r = [logit(L, lo, hi)];
    for (let k = 1; k < 3; k++) { const s1 = clamp((h[k] - c0) / Math.max(cmax - c0, 1e-3), 0.02, 0.98); r.push(Math.log(s1 / (1 - s1))); }
    return r;
  }

  // ------------------------------------------------------------------ grid geometry ---
  class Grid {
    constructor(G, cell) { this.G = G; this.cell = cell; this.G3 = G * G * G; this.cx = 0; this.cy = 0; this.cz = 0;
      this.tmp = new Float32Array(this.G3); }
    setCentre(x, y, z) { this.cx = x; this.cy = y; this.cz = z; }
    /** trilinear corners at world (x,y,z): fills idx[8] (flat, -1 outside) and w[8] */
    corners(x, y, z, idx, w) {
      const G = this.G, h = (G - 1) / 2;
      const ux = (x - this.cx) / this.cell + h, uy = (y - this.cy) / this.cell + h, uz = (z - this.cz) / this.cell + h;
      const ix = Math.floor(ux), iy = Math.floor(uy), iz = Math.floor(uz), fx = ux - ix, fy = uy - iy, fz = uz - iz;
      let n = 0;
      for (let dx = 0; dx < 2; dx++) for (let dy = 0; dy < 2; dy++) for (let dz = 0; dz < 2; dz++, n++) {
        const a = ix + dx, b = iy + dy, c = iz + dz;
        w[n] = (dx ? fx : 1 - fx) * (dy ? fy : 1 - fy) * (dz ? fz : 1 - fz);
        idx[n] = (a >= 0 && a < G && b >= 0 && b < G && c >= 0 && c < G) ? (a * G + b) * G + c : -1;
      }
    }
    /** separable [1,2,1]/4 blur with zero padding, in place on channels [c0, c1) of f */
    blur(f, c0, c1) {
      const G = this.G, G3 = this.G3, t = this.tmp, st = [G * G, G, 1];
      for (let c = c0; c < c1; c++) {
        const o = c * G3;
        for (let d = 0; d < 3; d++) {
          const s = st[d];
          for (let i = 0; i < G3; i++) {
            const coord = d === 0 ? (i / (G * G)) | 0 : d === 1 ? ((i / G) | 0) % G : i % G;
            const v = f[o + i];
            t[i] = 0.5 * v + (coord > 0 ? 0.25 * f[o + i - s] : 0) + (coord < G - 1 ? 0.25 * f[o + i + s] : 0);
          }
          f.set(t, o);
        }
      }
    }
  }

  // ------------------------------------------------------------------ plan fields ---
  /** hgrid_core.unit_values + PlanFields: target units -> [DC] values per unit */
  function unitValues(fr, i, nextP, period) {
    const v = new Float32Array(DC), e = fr.e[i], sl = fr.sl[i];
    v[e * 3 + sl] = 1;
    const rp = rawPrism([fr.h[3 * i], fr.h[3 * i + 1], fr.h[3 * i + 2]], e);
    for (let k = 0; k < 3; k++) v[12 + e * 3 + k] = rp[k];
    if (e === 0) for (let k = 0; k < 3; k++) v[24 + k] = Math.log((fr.ti[i] === k ? 0.9 : 0) + 0.1 / 3);
    for (let k = 0; k < 3; k++) v[27 + e * 3 + k] = fr.f[3 * i + k];
    const a = clamp(fr.sp[2 * i] / 0.6, 0.02, 0.98), b = clamp(fr.sp[2 * i + 1] / 0.5, -0.98, 0.98);
    v[39 + e * 2] = Math.log(a / (1 - a)); v[39 + e * 2 + 1] = 0.5 * Math.log((1 + b) / (1 - b));
    for (let c = 0; c < 3; c++) v[FIELD_C + e * 3 + c] = (nextP[3 * i + c] - fr.p[3 * i + c]) / period;
    return v;
  }

  class PlanFields {
    constructor(targets, cfg) {
      this.targets = targets; this.cfg = cfg; this.cache = {};
      this.grid = new Grid(cfg.G, cfg.cell);           // centred at 0: a field relative to its grid does not depend on where the grid is
      this.idx = new Int32Array(8); this.w = new Float32Array(8);
    }
    get(k, f) {
      const key = k + ':' + f;
      if (this.cache[key]) return this.cache[key];
      const T = this.targets[k], nf = T.frames.length, fr = T.frames[f], nx = T.frames[(f + 1) % nf];
      const g = this.grid, G3 = g.G3, D = new Float32Array(DC * G3), n = fr.e.length;
      for (let i = 0; i < n; i++) {
        const v = unitValues(fr, i, nx.p, this.cfg.period);
        g.corners(fr.p[3 * i], fr.p[3 * i + 1], fr.p[3 * i + 2], this.idx, this.w);
        for (let c = 0; c < 8; c++) {
          const j = this.idx[c]; if (j < 0) continue; const w = this.w[c];
          for (let ch = 0; ch < DC; ch++) if (v[ch] !== 0) D[ch * G3 + j] += w * v[ch];
        }
      }
      g.blur(D, 0, DC);
      // wanted headcount per (element, slot): the field's integral
      const want = new Float32Array(NCLS);
      for (let ch = 0; ch < NCLS; ch++) { let s = 0; for (let j = 0; j < G3; j++) s += D[ch * G3 + j]; want[ch] = s; }
      return (this.cache[key] = { D, want });
    }
  }

  // ------------------------------------------------------------------ the swarm ---
  class LiveSwarm {
    constructor(targets, opts) {
      opts = opts || {};
      this.targets = targets;
      this.cfg = Object.assign({}, DEF_CFG, opts.cfg || {});
      this.world = Object.assign({}, DEF_WORLD, opts.world || {});
      if (opts.capacity) this.world.capacity = opts.capacity;
      this.rng = opts.rng || makeRng(opts.seed || 1);
      const N = this.N = this.world.capacity;
      this.pos = new Float32Array(N * 3); this.prev = new Float32Array(N * 3);
      this.vel = new Float32Array(N * 3);
      this.SC = opts.stride || 13; this.DIEI = this.SC === 13 ? 12 : 31;
      this.S = new Float32Array(N * this.SC);            // A | FAC 3 | PR 3 | TI 3 | SP 2 | (hidden) | DIE
      this.elem = new Int8Array(N); this.dom = new Int8Array(N);
      this.active = new Uint8Array(N); this.hatched = new Uint8Array(N); this.age = new Int16Array(N);
      this.born = new Float64Array(N);                   // step a slot was (re)filled (renderer: bloom)
      this.clock = 0; this.gplan = -1; this.dmap = [0, 1, 2]; this.deaths = 0; this.switches = [];
      this.pf = opts.planFields || new PlanFields(targets, this.cfg);
      const G = this.cfg.G; this.grid = new Grid(G, this.cfg.cell); this.G3 = G * G * G;
      this.Dd = new Float32Array(NCLS * this.G3);
      this.Afld = new Float32Array(NCLS * this.G3);
      this.defs = new Float32Array(14 * this.G3);
      this.gdef = new Float32Array(13 * 3 * this.G3);
      this.idx = new Int32Array(8); this.w = new Float32Array(8);
      this.samp = new Float32Array(N * 14); this.sgrad = new Float32Array(N * 39);
      this.events = [];                                  // {type:'death'|'kill'|'birth'|'switch', ...} drained by the renderer
    }

    // ---------------------------------------------------------- seeding ---
    clear() {
      this.active.fill(0); this.hatched.fill(0); this.S.fill(0); this.vel.fill(0); this.age.fill(0);
      this.clock = 0; this.gplan = -1; this.dmap = [0, 1, 2]; this.deaths = 0; this.switches = []; this.events = [];
    }
    place(i, x, y, z, e, d, hatched) {
      const p = this.pos; p[3 * i] = x; p[3 * i + 1] = y; p[3 * i + 2] = z;
      this.prev.set([x, y, z], 3 * i);
      this.S.fill(0, this.SC * i, this.SC * i + this.SC); this.vel.fill(0, 3 * i, 3 * i + 3);
      this.elem[i] = e; this.dom[i] = d; this.active[i] = 1; this.hatched[i] = hatched ? 1 : 0; this.age[i] = 0;
      if (hatched) this.S[this.SC * i] = 1;
      this.born[i] = this.clock;
    }
    /** swarm_nca.seed_swarm: n hatched tadpoles at the plan's element and domain mix, randn*2 */
    seedPlan(kind, n) {
      n = n || 16; this.clear();
      const T = this.targets[kind];
      const em = largestRemainder(T.mix, n, [0, 1, 2, 3].filter(e => T.mix[e] > 0));
      const sm = T.slot_mix.filter(c => c > 0), dm = largestRemainder(sm, n, sm.map((_, i) => i));
      const es = shuffle(rep(em), this.rng), ds = shuffle(rep(dm), this.rng);
      for (let i = 0; i < n; i++) this.place(i, 2 * this.rng.normal(), 2 * this.rng.normal(), 2 * this.rng.normal(), es[i], ds[i], true);
    }
    /** a RANDOM seed: n tadpoles uniform in a ball, random element mix (optionally biased), random domains.
     *  bias: -1 random, else an element index that is the clear majority (55-75%). */
    seedRandom(o) {
      o = o || {}; const n = o.n || 24, r = o.radius || 12, rng = this.rng; this.clear();
      const gam2 = () => -Math.log(rng() + 1e-9) - Math.log(rng() + 1e-9);   // Gamma(2): a Dirichlet(2,..) mix, rarely a near-zero share
      let we = [0, 1, 2, 3].map(gam2);
      if (o.bias != null && o.bias >= 0) {
        const s = we.reduce((a, b) => a + b, 0) - we[o.bias], share = 0.55 + 0.2 * rng();
        we = we.map((v, e) => e === o.bias ? share : (1 - share) * v / s);
      }
      const nd = o.domains || 3, wd = Array.from({ length: nd }, gam2);
      const em = largestRemainder(we, n, []), dm = largestRemainder(wd, n, []);
      const es = shuffle(rep(em), rng), ds = shuffle(rep(dm), rng);
      for (let i = 0; i < n; i++) {
        let x, y, z; do { x = 2 * rng() - 1; y = 2 * rng() - 1; z = 2 * rng() - 1; } while (x * x + y * y + z * z > 1);
        this.place(i, r * x, r * y, r * z, es[i], ds[i], true);
      }
      return { elements: em, domains: dm };
    }

    // ---------------------------------------------------------- reading ---
    census() {
      const el = [0, 0, 0, 0], dm = [0, 0, 0]; let n = 0, eggs = 0;
      for (let i = 0; i < this.N; i++) if (this.active[i]) { if (this.hatched[i]) { el[this.elem[i]]++; dm[this.dom[i]]++; n++; } else eggs++; }
      let maj = 0; for (let e = 1; e < 4; e++) if (el[e] > el[maj]) maj = e;
      return { n, eggs, elements: el, domains: dm, majority: n ? maj : -1, plan: this.gplan < 0 ? null : KINDS[this.gplan], deaths: this.deaths };
    }
    /** decoded unit i (as swarm_nca.decode / the rollout packer): p, elem, dom, h, tier, f, sp */
    unit(i, out) {
      const S = this.S, o = this.SC * i, e = this.elem[i];
      const fx = S[o + 1], fy = S[o + 2], fz = S[o + 3], fl = Math.sqrt(fx * fx + fy * fy + fz * fz + 0.04);
      out.h = prismH(S[o + 4], S[o + 5], S[o + 6], e, out.h || [0, 0, 0]);
      let tier = 0; if (e === 0) { tier = S[o + 7] >= S[o + 8] ? (S[o + 7] >= S[o + 9] ? 0 : 2) : (S[o + 8] >= S[o + 9] ? 1 : 2); }
      out.tier = tier; out.f = [fx / fl, fy / fl, fz / fl];
      out.sp = [0.6 * sig(S[o + 10]), 0.5 * Math.tanh(S[o + 11])];
      out.elem = e; out.dom = this.dom[i]; out.alpha = S[o];
      return out;
    }
    /** state export for the Python scorer (live_export.py swarm_from_states) */
    exportState() {
      const st = { pos: [], s: [], elem: [], dom: [], active: [], hatched: [] };
      for (let i = 0; i < this.N; i++) {
        if (!this.active[i]) continue;
        for (let k = 0; k < 3; k++) st.pos.push(+this.pos[3 * i + k].toFixed(4));
        for (let k = 0; k < 12; k++) st.s.push(+this.S[this.SC * i + k].toFixed(5));
        st.s.push(+this.S[this.SC * i + this.DIEI].toFixed(5));
        st.elem.push(this.elem[i]); st.dom.push(this.dom[i]); st.active.push(1); st.hatched.push(this.hatched[i]);
      }
      return st;
    }

    // ---------------------------------------------------------- interaction ---
    kill(i, why) {
      if (!this.active[i]) return false;
      const wasLive = this.hatched[i];
      this.events.push({ type: why || 'kill', i, p: [this.pos[3 * i], this.pos[3 * i + 1], this.pos[3 * i + 2]], elem: this.elem[i], dom: this.dom[i], egg: !wasLive, step: this.clock });
      this.active[i] = 0; this.hatched[i] = 0; this.S.fill(0, this.SC * i, this.SC * i + this.SC); this.vel.fill(0, 3 * i, 3 * i + 3); this.age[i] = 0;
      return true;
    }
    /** every tadpole (and egg) within r of c; elemFilter -1 = all */
    killBall(c, r, elemFilter) {
      let k = 0; const r2 = r * r;
      for (let i = 0; i < this.N; i++) {
        if (!this.active[i] || (elemFilter != null && elemFilter >= 0 && this.elem[i] !== elemFilter)) continue;
        const dx = this.pos[3 * i] - c[0], dy = this.pos[3 * i + 1] - c[1], dz = this.pos[3 * i + 2] - c[2];
        if (dx * dx + dy * dy + dz * dz <= r2 && this.kill(i, 'kill')) k++;
      }
      return k;
    }
    /** every tadpole within r of the ray o + t d (d unit), t >= 0 */
    killRay(o, d, r, elemFilter) {
      let k = 0; const r2 = r * r;
      for (let i = 0; i < this.N; i++) {
        if (!this.active[i] || (elemFilter != null && elemFilter >= 0 && this.elem[i] !== elemFilter)) continue;
        const px = this.pos[3 * i] - o[0], py = this.pos[3 * i + 1] - o[1], pz = this.pos[3 * i + 2] - o[2];
        const t = Math.max(0, px * d[0] + py * d[1] + pz * d[2]);
        const qx = px - t * d[0], qy = py - t * d[1], qz = pz - t * d[2];
        if (qx * qx + qy * qy + qz * qz <= r2 && this.kill(i, 'kill')) k++;
      }
      return k;
    }

    /** swarm_nca.lose_majority, mode "excess": the majority loses just enough (+ a random extra) that
     *  element `to` leads it by one. Returns the new majority's plan, or null when no switch is possible. */
    loseMajority(to, keepMin) {
      keepMin = keepMin == null ? 6 : keepMin;
      const c = this.census().elements, n = c.reduce((a, b) => a + b, 0);
      let maj = 0; for (let e = 1; e < 4; e++) if (c[e] > c[maj]) maj = e;
      if (to === maj || c[to] < 2) return null;
      const extra = Math.floor(this.rng() * Math.max(1, Math.floor(c[to] / 3) + 1));
      const excess = c[maj] - c[to] + 1 + extra;
      if (n - excess < keepMin || excess <= 0 || excess > c[maj]) return null;
      const idx = []; for (let i = 0; i < this.N; i++) if (this.active[i] && this.hatched[i] && this.elem[i] === maj) idx.push(i);
      shuffle(idx, this.rng).slice(0, excess).forEach(i => this.kill(i, 'kill'));
      const c2 = this.census(); return c2.majority >= 0 ? KINDS[PLAN_OF[c2.majority]] : null;
    }

    // ---------------------------------------------------------- the step ---
    decidePlan(cnt, dcnt) {
      let n = cnt[0] + cnt[1] + cnt[2] + cnt[3]; if (n === 0) return;
      let maj = 0; for (let e = 1; e < 4; e++) if (cnt[e] > cnt[maj]) maj = e;
      const cur = this.gplan; let nw = cur;
      if (cur < 0) nw = PLAN_OF[maj];
      else { const ce = MAJOR[KINDS[cur]]; if (cnt[maj] > cnt[ce] + this.cfg.hyst * n) nw = PLAN_OF[maj]; }
      if (nw !== cur) {
        this.gplan = nw;
        const T = this.targets[KINDS[nw]], sh = [0, 1, 2].map(s => T.slot_mix[s] || 0), ss = sh[0] + sh[1] + sh[2];
        let best = -1, arg = PERM3[0];
        for (const p of PERM3) { let v = 0; for (let s = 0; s < 3; s++) v += Math.min(dcnt[p[s]], sh[s] / ss * n); if (v > best + 1e-6) { best = v; arg = p; } }
        this.dmap = arg.slice();
        if (cur >= 0) { this.switches.push({ step: this.clock, from: KINDS[cur], to: KINDS[nw] }); this.events.push({ type: 'switch', from: KINDS[cur], to: KINDS[nw], step: this.clock }); }
      }
    }

    step() {
      const cfg = this.cfg, W = this.world, N = this.N, G3 = this.G3, g = this.grid, rng = this.rng;
      const pos = this.pos, S = this.S, vel = this.vel, elem = this.elem, dom = this.dom;
      this.prev.set(pos);
      // live = active & hatched (before the step)
      const live = new Uint8Array(N); let nl = 0, mx = 0, my = 0, mz = 0;
      const cnt = [0, 0, 0, 0], dcnt = [0, 0, 0];
      for (let i = 0; i < N; i++) if (this.active[i] && this.hatched[i]) {
        live[i] = 1; nl++; mx += pos[3 * i]; my += pos[3 * i + 1]; mz += pos[3 * i + 2]; cnt[elem[i]]++; dcnt[dom[i]]++;
      }
      const nn = Math.max(nl, 1); let cx = mx / nn, cy = my / nn, cz = mz / nn;
      if (cfg.quant) { cx = roundEven(cx / cfg.cell) * cfg.cell; cy = roundEven(cy / cfg.cell) * cfg.cell; cz = roundEven(cz / cfg.cell) * cfg.cell; }
      g.setCentre(cx, cy, cz);
      if (this.clock === 0) this.gplan = -1;
      this.decidePlan(cnt, dcnt);
      if (this.gplan < 0) { this.clock++; this.hatchOnly(); return; }
      const kind = KINDS[this.gplan], nf = this.targets[kind].frames.length;
      const fr = cfg.animate ? Math.floor(this.clock / this.periodOf(kind)) % nf : 0;
      const { D, want: wantSlot } = this.pf.get(kind, fr);
      // Dd: wanted density per (element, DOMAIN) through the slot->domain map
      const Dd = this.Dd, inv = [0, 0, 0];
      for (let s = 0; s < 3; s++) inv[this.dmap[s]] = s;
      const wanted = new Float32Array(NCLS);
      for (let e = 0; e < 4; e++) for (let d = 0; d < 3; d++) {
        Dd.set(D.subarray((e * 3 + inv[d]) * G3, (e * 3 + inv[d] + 1) * G3), (e * 3 + d) * G3);
        wanted[e * 3 + d] = wantSlot[e * 3 + inv[d]];
      }
      // actual class density
      const A = this.Afld; A.fill(0);
      const idx = this.idx, w = this.w;
      for (let i = 0; i < N; i++) if (live[i]) {
        g.corners(pos[3 * i], pos[3 * i + 1], pos[3 * i + 2], idx, w);
        const o = (elem[i] * 3 + dom[i]) * G3;
        for (let c = 0; c < 8; c++) if (idx[c] >= 0) A[o + idx[c]] += w[c];
      }
      g.blur(A, 0, NCLS);
      // deficits: 12 class | total deficit | total wanted
      const defs = this.defs;
      for (let j = 0; j < G3; j++) {
        let dt = 0, at = 0;
        for (let c = 0; c < NCLS; c++) { const a = Dd[c * G3 + j], b = A[c * G3 + j]; defs[c * G3 + j] = a - b; dt += a; at += b; }
        defs[12 * G3 + j] = dt - at; defs[13 * G3 + j] = dt;
      }
      this.gradient(defs, 13, this.gdef);
      // sample deficits + gradients at the OLD positions
      const samp = this.samp, sg = this.sgrad, gdef = this.gdef;
      const vnew = new Float32Array(N * 3);
      for (let i = 0; i < N; i++) {
        if (!this.active[i]) continue;
        g.corners(pos[3 * i], pos[3 * i + 1], pos[3 * i + 2], idx, w);
        const cl = elem[i] * 3 + dom[i];
        // only the channels the rule reads: own deficit, total wanted, own & total gradients (+ every class
        // gradient is needed for the egg direction: sample all 13 x 3 like the Python)
        let dOwn = 0, wantTot = 0;
        for (let c = 0; c < 8; c++) { const j = idx[c]; if (j < 0) continue; dOwn += w[c] * defs[cl * G3 + j]; wantTot += w[c] * defs[13 * G3 + j]; }
        samp[14 * i + cl] = dOwn; samp[14 * i + 12] = dOwn; samp[14 * i + 13] = wantTot;
        for (let q = 0; q < 39; q++) { let s = 0; for (let c = 0; c < 8; c++) { const j = idx[c]; if (j >= 0) s += w[c] * gdef[q * G3 + j]; } sg[39 * i + q] = s; }
        if (!live[i]) continue;
        const niche = wanted[cl] > 0.5 ? 1 : 0;
        let vx = cfg.k_class * sg[39 * i + cl * 3] * niche + cfg.k_total * sg[39 * i + 36];
        let vy = cfg.k_class * sg[39 * i + cl * 3 + 1] * niche + cfg.k_total * sg[39 * i + 37];
        let vz = cfg.k_class * sg[39 * i + cl * 3 + 2] * niche + cfg.k_total * sg[39 * i + 38];
        if (cfg.flow && cfg.k_flow) {
          const e = elem[i]; let fx = 0, fy = 0, fz = 0, de = 0;
          for (let c = 0; c < 8; c++) {
            const j = idx[c]; if (j < 0) continue; const ww = w[c];
            fx += ww * D[(FIELD_C + e * 3) * G3 + j]; fy += ww * D[(FIELD_C + e * 3 + 1) * G3 + j]; fz += ww * D[(FIELD_C + e * 3 + 2) * G3 + j];
            de += ww * (D[(e * 3) * G3 + j] + D[(e * 3 + 1) * G3 + j] + D[(e * 3 + 2) * G3 + j]);
          }
          const dd = Math.max(de, 0.2);
          vx += cfg.k_flow * fx / dd; vy += cfg.k_flow * fy / dd; vz += cfg.k_flow * fz / dd;
        }
        if (wantTot < 0.05) {
          const hx = cx - pos[3 * i], hy = cy - pos[3 * i + 1], hz = cz - pos[3 * i + 2], hl = Math.max(Math.hypot(hx, hy, hz), 1);
          vx += cfg.k_home * hx / hl; vy += cfg.k_home * hy / hl; vz += cfg.k_home * hz / hl;
        }
        if (cfg.noise) { vx += cfg.noise * rng.normal(); vy += cfg.noise * rng.normal(); vz += cfg.noise * rng.normal(); }
        vnew[3 * i] = vx; vnew[3 * i + 1] = vy; vnew[3 * i + 2] = vz;
      }
      // velocity persistence + top speed; move the live ones
      const P = new Float32Array(pos);
      for (let i = 0; i < N; i++) {
        if (!live[i]) { vel[3 * i] = vel[3 * i + 1] = vel[3 * i + 2] = 0; continue; }
        let ux = cfg.persist * vel[3 * i] + (1 - cfg.persist) * vnew[3 * i];
        let uy = cfg.persist * vel[3 * i + 1] + (1 - cfg.persist) * vnew[3 * i + 1];
        let uz = cfg.persist * vel[3 * i + 2] + (1 - cfg.persist) * vnew[3 * i + 2];
        const sp = Math.hypot(ux, uy, uz), vm = W.vmax[elem[i]], f = Math.min(vm / Math.max(sp, 1e-6), 1);
        ux *= f; uy *= f; uz *= f;
        vel[3 * i] = ux; vel[3 * i + 1] = uy; vel[3 * i + 2] = uz;
        P[3 * i] += ux; P[3 * i + 1] += uy; P[3 * i + 2] += uz;
      }
      // collision: pairs within R at the OLD positions (every active slot, eggs too), pushed at the NEW ones
      this.collide(P);
      for (let i = 0; i < N; i++) {
        const x = P[3 * i], y = P[3 * i + 1], z = P[3 * i + 2], rad = Math.max(Math.hypot(x, y, z), 1e-6), ex = Math.max(rad - W.membrane, 0);
        if (ex > 0) { const k = 0.5 * ex / rad; P[3 * i] = x - k * x; P[3 * i + 1] = y - k * y; P[3 * i + 2] = z - k * z; }
      }
      pos.set(P);
      // look: ease toward the field's attributes at the NEW position
      const row = new Float32Array(DC);
      for (let i = 0; i < N; i++) {
        if (!live[i]) continue;
        g.corners(pos[3 * i], pos[3 * i + 1], pos[3 * i + 2], idx, w);
        row.fill(0);
        for (let c = 0; c < 8; c++) { const j = idx[c]; if (j < 0) continue; const ww = w[c]; for (let ch = 0; ch < FIELD_C; ch++) row[ch] += ww * D[ch * G3 + j]; }
        const e = elem[i], de0 = row[e * 3] + row[e * 3 + 1] + row[e * 3 + 2], dens = Math.max(de0, 1e-3), has = de0 > 1e-3 ? 1 : 0;
        const dc = Math.max(row[0] + row[1] + row[2], 1e-3), o = 13 * i, ea = cfg.ease;
        const tgt = [row[12 + e * 3] / dens * has, row[13 + e * 3] / dens * has, row[14 + e * 3] / dens * has,
          row[24] / dc, row[25] / dc, row[26] / dc,
          5 * row[27 + e * 3], 5 * row[28 + e * 3], 5 * row[29 + e * 3],
          row[39 + e * 2] / dens * has, row[40 + e * 2] / dens * has];
        // state order PR(4-6) TI(7-9) FAC(1-3) SP(10-11)
        const map = [4, 5, 6, 7, 8, 9, 1, 2, 3, 10, 11];
        for (let k = 0; k < 11; k++) S[o + map[k]] += ea * (tgt[k] - S[o + map[k]]);
      }
      // hatching
      for (let i = 0; i < N; i++) {
        if (this.active[i] && !this.hatched[i]) {
          this.age[i]++;
          S[13 * i] = Math.min(this.age[i] / cfg.hatch_steps, 1) * 0.1;
          if (this.age[i] >= cfg.hatch_steps) { this.hatched[i] = 1; this.age[i] = 0; }
        } else if (this.active[i]) { S[13 * i] = 1; this.age[i] = 0; }
      }
      // starvation: a surplus class (whole swarm) where its class is not wanted
      const have = new Float32Array(NCLS);
      for (let i = 0; i < N; i++) if (live[i]) have[elem[i] * 3 + dom[i]]++;
      if (cfg.starve) {
        const surplus = have.map((h, c) => h > (1 + cfg.starve_tol) * wanted[c] + 1.0 ? 1 : 0);
        for (let i = 0; i < N; i++) {
          const cl = elem[i] * 3 + dom[i], o = 13 * i + 12;
          const sur = live[i] && surplus[cl] && samp[14 * i + 12] < cfg.starve_local;
          S[o] = sur ? S[o] + cfg.starve_rate : Math.max(S[o] - cfg.starve_rate, 0);
          if (live[i] && S[o] > W.DIE_AT) { this.deaths++; this.kill(i, 'death'); }
        }
      }
      this.clock++;
      this.lay(live, have, wanted);
    }

    periodOf(kind) { return this.cfg.period; }

    hatchOnly() {
      for (let i = 0; i < this.N; i++) if (this.active[i] && !this.hatched[i]) {
        this.age[i]++; this.S[13 * i] = Math.min(this.age[i] / this.cfg.hatch_steps, 1) * 0.1;
        if (this.age[i] >= this.cfg.hatch_steps) { this.hatched[i] = 1; this.age[i] = 0; this.S[13 * i] = 1; }
      }
    }

    gradient(f, K, out) {
      const G = this.cfg.G, G3 = this.G3, inv = 1 / (2 * this.cfg.cell), st = [G * G, G, 1];
      for (let k = 0; k < K; k++) {
        const o = k * G3;
        for (let d = 0; d < 3; d++) {
          const s = st[d], q = (k * 3 + d) * G3;
          for (let i = 0; i < G3; i++) {
            const coord = d === 0 ? (i / (G * G)) | 0 : d === 1 ? ((i / G) | 0) % G : i % G;
            const a = coord < G - 1 ? f[o + i + s] : f[o + i], b = coord > 0 ? f[o + i - s] : f[o + i];
            out[q + i] = (a - b) * inv;
          }
        }
      }
    }

    collide(P) {
      const W = this.world, N = this.N, pos = this.pos, R2 = W.R * W.R, k = W.rep * W.r0 * 0.5;
      const act = []; for (let i = 0; i < N; i++) if (this.active[i]) act.push(i);
      const dP = new Float32Array(N * 3);
      // uniform hash grid of cell R on the old positions
      const cs = W.R, map = new Map();
      for (const i of act) {
        const key = Math.floor(pos[3 * i] / cs) + ',' + Math.floor(pos[3 * i + 1] / cs) + ',' + Math.floor(pos[3 * i + 2] / cs);
        let b = map.get(key); if (!b) map.set(key, b = []); b.push(i);
      }
      for (const i of act) {
        const bx = Math.floor(pos[3 * i] / cs), by = Math.floor(pos[3 * i + 1] / cs), bz = Math.floor(pos[3 * i + 2] / cs);
        for (let a = -1; a <= 1; a++) for (let b = -1; b <= 1; b++) for (let c = -1; c <= 1; c++) {
          const bucket = map.get((bx + a) + ',' + (by + b) + ',' + (bz + c)); if (!bucket) continue;
          for (const j of bucket) {
            if (j === i) continue;
            const ox = pos[3 * j] - pos[3 * i], oy = pos[3 * j + 1] - pos[3 * i + 1], oz = pos[3 * j + 2] - pos[3 * i + 2];
            if (ox * ox + oy * oy + oz * oz >= R2) continue;
            const dx = P[3 * j] - P[3 * i], dy = P[3 * j + 1] - P[3 * i + 1], dz = P[3 * j + 2] - P[3 * i + 2];
            const r = Math.sqrt(Math.max(dx * dx + dy * dy + dz * dz, 1e-8)), ov = Math.max(W.r0 - r, 0) / W.r0;
            if (ov <= 0) continue;
            const f = -k * ov / r; dP[3 * i] += f * dx; dP[3 * i + 1] += f * dy; dP[3 * i + 2] += f * dz;
          }
        }
      }
      for (let i = 0; i < 3 * N; i++) P[i] += dP[i];
    }

    lay(parentsLive, have, want) {
      const cfg = this.cfg, W = this.world, N = this.N, rng = this.rng, elem = this.elem, dom = this.dom;
      const rel = new Float32Array(NCLS);
      for (let c = 0; c < NCLS; c++) rel[c] = clamp((want[c] - have[c]) / Math.max(have[c], 1), 0, 1);
      const bestRel = [0, 0, 0], bestE = [0, 0, 0];
      for (let d = 0; d < 3; d++) { let b = 0; for (let e = 1; e < 4; e++) if (rel[e * 3 + d] > rel[b * 3 + d]) b = e; bestRel[d] = rel[b * 3 + d]; bestE[d] = b; }
      const par = [], childE = new Int8Array(N);
      for (let i = 0; i < N; i++) {
        const ok0 = parentsLive[i] && this.active[i];
        const own = rel[elem[i] * 3 + dom[i]], alt = bestRel[dom[i]];
        const p = cfg.p_lay * Math.max(own, cfg.p_cross * alt);
        const u = rng(), coin = rng() < cfg.p_cross;                    // the Python draws both for every slot
        if (!(ok0 && u < p)) continue;
        childE[i] = (own <= 0 || (coin && alt > own)) ? bestE[dom[i]] : elem[i];
        par.push(i);
      }
      if (!par.length) return;
      const free = []; for (let i = 0; i < N; i++) if (!this.active[i]) free.push(i);
      const k = Math.min(par.length, free.length); if (!k) return;
      shuffle(par, rng);
      for (let q = 0; q < k; q++) {
        const i = par[q], s = free[q], e = childE[i], d = dom[i], gi = 39 * i + (e * 3 + d) * 3;
        let gx = this.sgrad[gi], gy = this.sgrad[gi + 1], gz = this.sgrad[gi + 2];
        const gl = Math.max(Math.hypot(gx, gy, gz), 1e-6); gx /= gl; gy /= gl; gz /= gl;
        let nx = rng.normal(), ny = rng.normal(), nz = rng.normal(); const nl = Math.max(Math.hypot(nx, ny, nz), 1e-12);
        let dx = gx + 0.7 * nx / nl, dy = gy + 0.7 * ny / nl, dz = gz + 0.7 * nz / nl; const dl = Math.max(Math.hypot(dx, dy, dz), 1e-6);
        const p = this.pos;
        this.place(s, p[3 * i] + W.r_bud * dx / dl, p[3 * i + 1] + W.r_bud * dy / dl, p[3 * i + 2] + W.r_bud * dz / dl, e, d, false);
        this.prev[3 * s] = p[3 * i]; this.prev[3 * s + 1] = p[3 * i + 1]; this.prev[3 * s + 2] = p[3 * i + 2];   // an egg slides out of its parent
        this.events.push({ type: 'birth', i: s, parent: i, elem: e, dom: d, step: this.clock });
      }
    }
  }

  // ------------------------------------------------------------------ the evolved rule ---
  /* A port of evo_model.EvoRule (results/evo): the trained G2 rule (swarm_nca.SwarmRule, a 232-192-192-35
   * MLP every tadpole runs on what it senses within R) wrapped by a CMA-ES genome: a lay homeostat (a
   * parent lays more when its element is short of the share the CURRENT majority's plan wants), egg
   * choice (a share of eggs take the element the swarm is shortest of; domain breeds true) and an output
   * gain (already folded into w3/b3 by live_export.py). Full 32-channel state per tadpole. */
  function f32(w) {
    if (typeof Buffer !== 'undefined') { const b = Buffer.from(w.b64, 'base64'); return new Float32Array(b.buffer.slice(b.byteOffset, b.byteOffset + b.length)); }
    const bin = atob(w.b64), u = new Uint8Array(bin.length); for (let i = 0; i < bin.length; i++) u[i] = bin.charCodeAt(i);
    return new Float32Array(u.buffer);
  }
  class EvoSwarm extends LiveSwarm {
    constructor(targets, rule, opts) {
      opts = Object.assign({}, opts || {}, { stride: 32, world: Object.assign({}, rule.world, (opts || {}).world || {}) });
      if (!opts.capacity) opts.capacity = rule.world.capacity;
      super(targets, opts);
      this.rule = rule;
      if (!rule._w) rule._w = { w1: f32(rule.weights.w1), b1: f32(rule.weights.b1), w2: f32(rule.weights.w2), b2: f32(rule.weights.b2), w3: f32(rule.weights.w3), b3: f32(rule.weights.b3) };
      this.H = rule.hidden; this.X = 37; this.F = 37 * 3 + 37 * 3 + 3 + 2 + 5;
      this.locked = -1;
    }
    clear() { super.clear(); this.locked = -1; }
    shares() {
      const c = [0, 0, 0, 0]; let n = 0;
      for (let i = 0; i < this.N; i++) if (this.active[i] && this.hatched[i]) { c[this.elem[i]]++; n++; }
      return { c, n };
    }
    edges() {                                         // all active pairs within R at the current positions
      const W = this.world, N = this.N, pos = this.pos, R2 = W.R * W.R, cs = W.R, map = new Map(), gi = [], gj = [];
      const act = []; for (let i = 0; i < N; i++) if (this.active[i]) act.push(i);
      for (const i of act) { const key = Math.floor(pos[3 * i] / cs) + ',' + Math.floor(pos[3 * i + 1] / cs) + ',' + Math.floor(pos[3 * i + 2] / cs);
        let b = map.get(key); if (!b) map.set(key, b = []); b.push(i); }
      for (const i of act) {
        const bx = Math.floor(pos[3 * i] / cs), by = Math.floor(pos[3 * i + 1] / cs), bz = Math.floor(pos[3 * i + 2] / cs);
        for (let a = -1; a <= 1; a++) for (let b = -1; b <= 1; b++) for (let c = -1; c <= 1; c++) {
          const bucket = map.get((bx + a) + ',' + (by + b) + ',' + (bz + c)); if (!bucket) continue;
          for (const j of bucket) { if (j === i) continue;
            const dx = pos[3 * j] - pos[3 * i], dy = pos[3 * j + 1] - pos[3 * i + 1], dz = pos[3 * j + 2] - pos[3 * i + 2];
            if (dx * dx + dy * dy + dz * dz < R2) { gi.push(i); gj.push(j); } }
        }
      }
      return { gi, gj };
    }
    step() {
      const W = this.world, N = this.N, rule = this.rule, wt = rule._w, rng = this.rng, SC = 32, X = 37, H = this.H, F = this.F;
      const pos = this.pos, S = this.S, elem = this.elem, dom = this.dom;
      this.prev.set(pos);
      if (this.clock === 0) this.locked = -1;
      // the majority the homeostats steer by
      const sh = this.shares();
      let top = 0; for (let e = 1; e < 4; e++) if (sh.c[e] > sh.c[top]) top = e;
      const before = this.locked;
      if (rule.lock.on) { const cur = Math.max(this.locked, 0); if (this.locked < 0 || sh.c[top] - sh.c[cur] > rule.lock.margin * sh.n) this.locked = top; }
      else this.locked = top;
      if (sh.n && before >= 0 && this.locked !== before && PLAN_OF[this.locked] !== PLAN_OF[before]) {
        this.switches.push({ step: this.clock, from: KINDS[PLAN_OF[before]], to: KINDS[PLAN_OF[this.locked]] });
        this.events.push({ type: 'switch', from: KINDS[PLAN_OF[before]], to: KINDS[PLAN_OF[this.locked]], step: this.clock });
      }
      this.gplan = sh.n ? PLAN_OF[this.locked] : -1;
      const { gi, gj } = this.edges(), E = gi.length;
      // x = state | one-hot element | hatched
      const x = new Float32Array(N * X);
      for (let i = 0; i < N; i++) { x.set(S.subarray(SC * i, SC * i + SC), X * i); x[X * i + 32 + elem[i]] = 1; x[X * i + 36] = this.hatched[i]; }
      const fire = new Uint8Array(N); for (let i = 0; i < N; i++) fire[i] = this.active[i] && rng() <= rule.fire_rate ? 1 : 0;
      // perceive (only for fired tadpoles; the Python drops edges whose centre did not fire)
      const feat = new Float32Array(N * F), rs = new Float32Array(N), ro = new Float32Array(N), gs = new Float32Array(N);
      const R = W.R, R2 = R * R;
      const MS = X, MO = 2 * X, GR = 3 * X, GS = 6 * X, RH = 6 * X + 3;
      for (let k = 0; k < E; k++) {
        const i = gi[k], j = gj[k]; if (!fire[i]) continue;
        const dx = pos[3 * j] - pos[3 * i], dy = pos[3 * j + 1] - pos[3 * i + 1], dz = pos[3 * j + 2] - pos[3 * i + 2];
        const q = Math.max(1 - (dx * dx + dy * dy + dz * dz) / R2, 0), w = q * q * q, g = q * q, same = dom[i] === dom[j] ? 1 : 0;
        const o = F * i, xj = X * j, xi = X * i, u = [dx / R, dy / R, dz / R];
        if (same) { rs[i] += w; for (let c = 0; c < X; c++) feat[o + MS + c] += w * x[xj + c]; }
        else { ro[i] += w; for (let c = 0; c < X; c++) feat[o + MO + c] += w * x[xj + c]; }
        gs[i] += g;
        for (let d = 0; d < 3; d++) { const gu = g * u[d], b = o + GR + d * X; for (let c = 0; c < X; c++) feat[b + c] += gu * (x[xj + c] - x[xi + c]);
          feat[o + GS + d] += gu * (same - 1); }
      }
      const glob = [Math.max(sh.n, 1) / 100, ...sh.c.map(v => v / Math.max(sh.n, 1))];
      const h1 = new Float32Array(H), h2 = new Float32Array(H), out = new Float32Array(35), ds = new Float32Array(N * SC), v = new Float32Array(N * 3);
      for (let i = 0; i < N; i++) {
        if (!fire[i]) continue;
        const o = F * i;
        for (let c = 0; c < X; c++) feat[o + c] = x[X * i + c];
        for (let c = 0; c < X; c++) { feat[o + MS + c] /= 1 + rs[i]; feat[o + MO + c] /= 1 + ro[i]; }
        for (let c = 0; c < 3 * X; c++) feat[o + GR + c] /= 1 + gs[i];
        for (let d = 0; d < 3; d++) feat[o + GS + d] /= 1 + gs[i];
        feat[o + RH] = (rs[i] + ro[i]) / W.rho0; feat[o + RH + 1] = rs[i] / W.rho0;
        for (let c = 0; c < 5; c++) feat[o + RH + 2 + c] = glob[c];
        // MLP: h1 = relu(W1 f + b1); h2 = relu(W2 h1 + b2) + h1; out = W3 h2 + b3
        for (let r = 0; r < H; r++) { let a = wt.b1[r]; const wr = r * F; for (let c = 0; c < F; c++) a += wt.w1[wr + c] * feat[o + c]; h1[r] = a > 0 ? a : 0; }
        for (let r = 0; r < H; r++) { let a = wt.b2[r]; const wr = r * H; for (let c = 0; c < H; c++) a += wt.w2[wr + c] * h1[c]; h2[r] = (a > 0 ? a : 0) + h1[r]; }
        for (let r = 0; r < 35; r++) { let a = wt.b3[r]; const wr = r * H; for (let c = 0; c < H; c++) a += wt.w3[wr + c] * h2[c]; out[r] = a; }
        for (let c = 0; c < SC; c++) ds[SC * i + c] = out[c];
        const vm = W.vmax[elem[i]]; v[3 * i] = vm * Math.tanh(out[32]); v[3 * i + 1] = vm * Math.tanh(out[33]); v[3 * i + 2] = vm * Math.tanh(out[34]);
      }
      for (let k = 0; k < N * SC; k++) S[k] = Math.max(-1e3, Math.min(1e3, S[k] + ds[k]));
      const P = new Float32Array(pos); for (let k = 0; k < 3 * N; k++) P[k] += v[k];
      // collision (same pairs, new positions) + membrane
      const kk = W.rep * W.r0 * 0.5, dP = new Float32Array(3 * N);
      for (let k = 0; k < E; k++) {
        const i = gi[k], j = gj[k];
        const dx = P[3 * j] - P[3 * i], dy = P[3 * j + 1] - P[3 * i + 1], dz = P[3 * j + 2] - P[3 * i + 2];
        const r = Math.sqrt(Math.max(dx * dx + dy * dy + dz * dz, 1e-8)), ov = Math.max(W.r0 - r, 0) / W.r0;
        if (ov > 0) { const f = -kk * ov / r; dP[3 * i] += f * dx; dP[3 * i + 1] += f * dy; dP[3 * i + 2] += f * dz; }
      }
      for (let i = 0; i < N; i++) {
        let x0 = P[3 * i] + dP[3 * i], y0 = P[3 * i + 1] + dP[3 * i + 1], z0 = P[3 * i + 2] + dP[3 * i + 2];
        const rad = Math.max(Math.hypot(x0, y0, z0), 1e-6), ex = Math.max(rad - W.membrane, 0);
        if (ex > 0) { const f = 0.5 * ex / rad; x0 -= f * x0; y0 -= f * y0; z0 -= f * z0; }
        pos[3 * i] = x0; pos[3 * i + 1] = y0; pos[3 * i + 2] = z0;
      }
      for (let i = 0; i < N; i++) { this.vel[3 * i] = v[3 * i]; this.vel[3 * i + 1] = v[3 * i + 1]; this.vel[3 * i + 2] = v[3 * i + 2]; }
      // hatch / death / egg loss
      const act = this.active, hat = this.hatched;
      const hat2 = new Uint8Array(N), alive = new Uint8Array(N), near = new Uint8Array(N);
      for (let i = 0; i < N; i++) { hat2[i] = hat[i] || (act[i] && S[SC * i] > 0.1) ? 1 : 0; const died = act[i] && hat[i] && S[SC * i + 31] > W.DIE_AT; alive[i] = hat2[i] && !died ? 1 : 0; near[i] = alive[i]; }
      for (let k = 0; k < E; k++) if (alive[gj[k]]) near[gi[k]] = 1;
      for (let i = 0; i < N; i++) {
        if (!act[i]) continue;
        const died = hat[i] && S[SC * i + 31] > W.DIE_AT;
        const age = this.age[i] + (!hat2[i] ? 1 : 0);
        const goneEgg = !hat2[i] && (!near[i] || age > W.egg_life);
        if (died) { this.deaths++; this.kill(i, 'death'); continue; }
        if (goneEgg) { this.kill(i, 'egg'); continue; }
        hat[i] = hat2[i]; this.age[i] = hat2[i] ? 0 : age;
      }
      this.clock++;
      this.lay(gi, gj);
    }
    lay(gi, gj) {
      const W = this.world, N = this.N, rule = this.rule, rng = this.rng, SC = 32, pos = this.pos, elem = this.elem;
      const sh = this.shares(), share = sh.c.map(v => v / Math.max(sh.n, 1)), want = rule.D[Math.max(this.locked, 0)];
      const deficit = want.map((w, e) => w - share[e]);
      const sigm = z => 1 / (1 + Math.exp(-z)), pmax = 1 / Math.max(W.p_bud, 1e-3);
      const probs = (() => { const z = deficit.map(d => rule.egg.beta * d / 0.1), m = Math.max(...z), ex = z.map(a => Math.exp(a - m)), s = ex.reduce((a, b) => a + b, 0); return ex.map(a => a / s); })();
      const pchoose = rule.egg.on ? rule.egg.p : 0;
      const ncl = new Float32Array(N), cen = new Float32Array(3 * N), r2 = W.r_lay * W.r_lay;
      for (let k = 0; k < gi.length; k++) {
        const i = gi[k], j = gj[k], dx = pos[3 * j] - pos[3 * i], dy = pos[3 * j + 1] - pos[3 * i + 1], dz = pos[3 * j + 2] - pos[3 * i + 2];
        if (dx * dx + dy * dy + dz * dz < r2) { ncl[i]++; cen[3 * i] += dx; cen[3 * i + 1] += dy; cen[3 * i + 2] += dz; }
      }
      const par = [];
      for (let i = 0; i < N; i++) {
        const u = rng();
        if (!(this.hatched[i] && this.active[i] && ncl[i] < W.k_bud)) continue;
        const q = W.learned_lay ? sigm(W.lay_gain * this.S[SC * i + 30] + W.lay_bias) : 1;
        const mult = rule.lay.on ? Math.min(sigm(rule.lay.k * deficit[elem[i]] / 0.1 + rule.lay.b) / sigm(rule.lay.b), pmax) : 1;
        if (u <= W.p_bud * q * mult) par.push(i);
      }
      if (!par.length) return;
      const free = []; for (let i = 0; i < N; i++) if (!this.active[i]) free.push(i);
      const k = Math.min(par.length, free.length); if (!k) return;
      shuffle(par, rng);
      for (let q = 0; q < k; q++) {
        const i = par[q], s = free[q];
        let ax = -cen[3 * i], ay = -cen[3 * i + 1], az = -cen[3 * i + 2]; const al = Math.max(Math.hypot(ax, ay, az), 1e-6); ax /= al; ay /= al; az /= al;
        const nx = rng.normal(), ny = rng.normal(), nz = rng.normal(), nl = Math.max(Math.hypot(nx, ny, nz), 1e-6);
        let dx = ax + 0.6 * nx / nl, dy = ay + 0.6 * ny / nl, dz = az + 0.6 * nz / nl; const dl = Math.max(Math.hypot(dx, dy, dz), 1e-6);
        let e = elem[i];
        if (pchoose > 0 && rng() < pchoose) { let u = rng(), c = 0; while (c < 3 && u > probs[c]) { u -= probs[c]; c++; } e = c; }
        if (rng() < W.p_mut) e = (e + 1 + Math.floor(rng() * 3)) % 4;
        this.place(s, pos[3 * i] + W.r_bud * dx / dl, pos[3 * i + 1] + W.r_bud * dy / dl, pos[3 * i + 2] + W.r_bud * dz / dl, e, this.dom[i], false);
        this.prev[3 * s] = pos[3 * i]; this.prev[3 * s + 1] = pos[3 * i + 1]; this.prev[3 * s + 2] = pos[3 * i + 2];
        this.events.push({ type: 'birth', i: s, parent: i, elem: e, dom: this.dom[i], step: this.clock });
      }
    }
  }

  // ------------------------------------------------------------------ hgrid2: the grid morphogen, round 2 ---
  /* A port of hgrid2_model.Boid2 at results/hgrid2/params.json (= hgrid2_eval.BEST). Everything the round-1
   * grid does (LiveSwarm.step above: plan, coarse class-deficit climb, laying, looks), plus:
   *   * a plan, once committed, holds `lock` steps; a plan with fewer than 3 slots maps them onto domains 0..k-1
   *   * staggered starvation: per-slot hunger rates in [0.5, 1.5] x starve_rate, a class never loses more than
   *     its excess over the plan, an element never below the plan's count of it (misfits wither one by one)
   *   * the FINE layer: per class, a Gaussian bump field of the plan's units minus the live tadpoles of that
   *     class (sigma = 1.2 x the plan's own unit spacing); each tadpole climbs its own class's field, gain
   *     scaled by how full the body is, plus feed-forward (a share of its nearby same-class units' motion)
   *   * migrants: a tadpole where its class is barely wanted heads for the best site where its class is missing */
  const HG2_CFG = {
    k_class: 10.0, k_flow: 0.0, starve_tol: 0.0, k_fine: 2.0, k_fine_tot: 0.0, sigma_rel: 1.2, sigma: 2.5, settle: 1,
    periods: { time: 16 }, interp: 1, stagger: 1, k_mig: 1.0, mig_th: 0.3, mig_L: 40.0, elem_floor: 1, small_slack: 2.0,
    starve_slack: 0.0, lock: 60, dmap_low: 1, k_ff: 1.5,
  };
  class Hgrid2Swarm extends LiveSwarm {
    constructor(targets, opts) {
      opts = Object.assign({}, opts || {});
      opts.cfg = Object.assign({}, HG2_CFG, opts.cfg || {});
      super(targets, opts);
      this.chg = -1e9;
      const u = mulberry32(1234); this.U = new Float32Array(this.N); for (let i = 0; i < this.N; i++) this.U[i] = 0.5 + u();
      this.nn = {};
      for (const k of KINDS) {                       // the plan's own mean nearest-neighbour spacing (frame 0)
        const p = targets[k].frames[0].p, n = p.length / 3; let s = 0;
        for (let i = 0; i < n; i++) { let b = 1e18; for (let j = 0; j < n; j++) if (j !== i) { const d = (p[3 * i] - p[3 * j]) ** 2 + (p[3 * i + 1] - p[3 * j + 1]) ** 2 + (p[3 * i + 2] - p[3 * j + 2]) ** 2; if (d < b) b = d; } s += Math.sqrt(b); }
        this.nn[k] = s / n;
      }
      this.migrants = 0; this.starved = 0;
    }
    clear() { super.clear(); this.chg = -1e9; }
    periodOf(kind) { const p = this.cfg.periods && this.cfg.periods[kind]; return p || this.cfg.period; }
    sigOf(kind) { return this.cfg.sigma_rel ? this.cfg.sigma_rel * this.nn[kind] : this.cfg.sigma; }
    counts() {
      const cnt = [0, 0, 0, 0], dcnt = [0, 0, 0];
      for (let i = 0; i < this.N; i++) if (this.active[i] && this.hatched[i]) { cnt[this.elem[i]]++; dcnt[this.dom[i]]++; }
      return { cnt, dcnt };
    }
    /** hgrid_boid.decide_plan with the real hysteresis, then Boid2.decide's commit lock and low_dmap */
    decide() {
      const cfg = this.cfg;
      if (this.clock === 0) this.chg = -1e9;
      const { cnt, dcnt } = this.counts(), n = cnt[0] + cnt[1] + cnt[2] + cnt[3];
      const oldP = this.gplan, oldD = this.dmap.slice();
      if (n > 0) {
        let maj = 0; for (let e = 1; e < 4; e++) if (cnt[e] > cnt[maj]) maj = e;
        const cur = this.gplan; let nw = cur;
        if (cur < 0) nw = PLAN_OF[maj];
        else { const ce = MAJOR[KINDS[cur]]; if (cnt[maj] > cnt[ce] + cfg.hyst * n) nw = PLAN_OF[maj]; }
        if (nw !== cur) { this.gplan = nw; this.dmap = domainMap(dcnt, this.targets[KINDS[nw]].slot_mix); }
      }
      if (oldP >= 0 && this.gplan !== oldP) {
        if (cfg.lock && this.clock - this.chg < cfg.lock) { this.gplan = oldP; this.dmap = oldD; }
        else {
          this.chg = this.clock;
          this.switches.push({ step: this.clock, from: KINDS[oldP], to: KINDS[this.gplan] });
          this.events.push({ type: 'switch', from: KINDS[oldP], to: KINDS[this.gplan], step: this.clock });
        }
      }
      if (cfg.dmap_low) this.lowDmap(dcnt);
    }
    lowDmap(dcnt) {
      if (this.gplan < 0) return;
      const sm0 = this.targets[KINDS[this.gplan]].slot_mix, k = sm0.filter(c => c > 0).length;
      if (k >= 3) return;
      const have = new Set(this.dmap.slice(0, k)); let ok = have.size === k; for (let d = 0; d < k; d++) if (!have.has(d)) ok = false;
      if (ok) return;
      const sm = sm0.slice(0, k);
      const dd = [...Array(k).keys()].sort((a, b) => dcnt[b] - dcnt[a]);        // stable, as Python's sorted
      const order = [...Array(k).keys()].sort((a, b) => sm[b] - sm[a]);
      let p = [0, 0, 0];
      order.forEach((s_, q) => { p[s_] = dd[q]; });
      if (k === 2) p[2] = [0, 1, 2].find(d => !p.slice(0, 2).includes(d));
      if (k === 1) p = [0, 1, 2];
      this.dmap = p;
    }
    step() {
      const cfg = this.cfg, N = this.N;
      this.decide();
      const pos0 = new Float32Array(this.pos), live0 = new Uint8Array(N);
      for (let i = 0; i < N; i++) live0[i] = this.active[i] && this.hatched[i] ? 1 : 0;
      const h0 = cfg.hyst, st0 = cfg.starve;
      cfg.hyst = 1e9; if (cfg.stagger) cfg.starve = 0;
      try { super.step(); } finally { cfg.hyst = h0; cfg.starve = st0; }
      if (cfg.stagger && cfg.starve) this.starveStaggered();
      if (!cfg.k_fine && !cfg.k_fine_tot) return;
      if (this.gplan < 0) return;
      const live = new Uint8Array(N); let nl = 0, mx = 0, my = 0, mz = 0;
      for (let i = 0; i < N; i++) if (live0[i] && this.active[i] && this.hatched[i]) { live[i] = 1; nl++; mx += pos0[3 * i]; my += pos0[3 * i + 1]; mz += pos0[3 * i + 2]; }
      if (nl < 2) return;
      let cx = mx / nl, cy = my / nl, cz = mz / nl;
      if (cfg.quant) { cx = roundEven(cx / cfg.cell) * cfg.cell; cy = roundEven(cy / cfg.cell) * cfg.cell; cz = roundEven(cz / cfg.cell) * cfg.cell; }
      const tg = this.fineTargets(cx, cy, cz);
      this.fineDisp(tg, live, nl);
      if (cfg.k_mig) this.migrate(tg, live);
    }
    /** Boid2.fine_targets: the plan's units (interpolated between frames) placed at the grid centre, their class, motion */
    fineTargets(cx, cy, cz) {
      const kind = KINDS[this.gplan], T = this.targets[kind], frs = T.frames, nf = frs.length, per = this.periodOf(kind);
      const c = this.clock, f = this.cfg.animate ? Math.floor(c / per) % nf : 0, a = this.cfg.animate ? (c % per) / per : 0;
      const p0 = frs[f].p, p1 = frs[(f + 1) % nf].p, M = p0.length / 3, p = new Float32Array(3 * M), vel = new Float32Array(3 * M), cls = new Int8Array(M);
      const ctr = [cx, cy, cz];
      for (let t = 0; t < M; t++) {
        for (let k = 0; k < 3; k++) {
          const q0 = p0[3 * t + k], q1 = p1[3 * t + k];
          vel[3 * t + k] = (q1 - q0) / per;
          p[3 * t + k] = (this.cfg.interp ? q0 + a * (q1 - q0) : q0) + ctr[k];
        }
        cls[t] = frs[f].e[t] * 3 + this.dmap[frs[f].sl[t]];
      }
      const byCls = Array.from({ length: NCLS }, () => []);
      for (let t = 0; t < M; t++) byCls[cls[t]].push(t);
      return { p, vel, cls, M, byCls, sig: this.sigOf(kind) };
    }
    /** Boid2.fine_disp: each live tadpole climbs (own class's wanted bumps - own class's live bumps) */
    fineDisp(tg, live, nl) {
      const cfg = this.cfg, N = this.N, pos = this.pos, elem = this.elem, dom = this.dom, W = this.world;
      const s2 = 2 * tg.sig * tg.sig, isg2 = 1 / (tg.sig * tg.sig);
      const mine = Array.from({ length: NCLS }, () => []);
      for (let i = 0; i < N; i++) if (live[i]) mine[elem[i] * 3 + dom[i]].push(i);
      let k = cfg.k_fine; if (cfg.settle) k *= Math.max(0, Math.min(1, nl / tg.M));
      const disp = new Float32Array(3 * N);
      for (let c = 0; c < NCLS; c++) {
        const ts = tg.byCls[c], xs = mine[c];
        for (const i of xs) {
          const x = pos[3 * i], y = pos[3 * i + 1], z = pos[3 * i + 2];
          let gx = 0, gy = 0, gz = 0, wsum = 0, fx = 0, fy = 0, fz = 0;
          for (const t of ts) {
            const dx = x - tg.p[3 * t], dy = y - tg.p[3 * t + 1], dz = z - tg.p[3 * t + 2], w = Math.exp(-(dx * dx + dy * dy + dz * dz) / s2);
            gx -= w * dx; gy -= w * dy; gz -= w * dz; wsum += w; fx += w * tg.vel[3 * t]; fy += w * tg.vel[3 * t + 1]; fz += w * tg.vel[3 * t + 2];
          }
          for (const j of xs) {
            const dx = x - pos[3 * j], dy = y - pos[3 * j + 1], dz = z - pos[3 * j + 2], w = Math.exp(-(dx * dx + dy * dy + dz * dz) / s2);
            gx += w * dx; gy += w * dy; gz += w * dz;                                  // - g_have
          }
          let ux = k * gx * isg2, uy = k * gy * isg2, uz = k * gz * isg2;
          if (cfg.k_ff) { const ws = Math.max(wsum, 0.3); ux += cfg.k_ff * fx / ws; uy += cfg.k_ff * fy / ws; uz += cfg.k_ff * fz / ws; }
          const dn = Math.hypot(ux, uy, uz), vm = W.vmax[elem[i]], f = Math.min(vm / Math.max(dn, 1e-6), 1);
          disp[3 * i] = ux * f; disp[3 * i + 1] = uy * f; disp[3 * i + 2] = uz * f;
        }
      }
      for (let i = 0; i < N; i++) if (live[i]) { pos[3 * i] += disp[3 * i]; pos[3 * i + 1] += disp[3 * i + 1]; pos[3 * i + 2] += disp[3 * i + 2]; }
    }
    /** Boid2.migrate: a tadpole where its class is barely wanted heads for the neediest site of its class */
    migrate(tg, live) {
      const cfg = this.cfg, N = this.N, pos = this.pos, elem = this.elem, dom = this.dom, W = this.world, s2 = 2 * tg.sig * tg.sig;
      const mine = Array.from({ length: NCLS }, () => []);
      for (let i = 0; i < N; i++) if (live[i]) mine[elem[i] * 3 + dom[i]].push(i);
      const x0 = new Float32Array(pos);
      let nm = 0;
      for (let c = 0; c < NCLS; c++) {
        const ts = tg.byCls[c], xs = mine[c]; if (!xs.length) continue;
        // own_want per tadpole, have per site (Gxt over the same class)
        const G = new Float32Array(xs.length * ts.length), own = new Float32Array(xs.length), have = new Float32Array(ts.length);
        let any = false;
        xs.forEach((i, a) => {
          for (let b = 0; b < ts.length; b++) { const t = ts[b];
            const dx = x0[3 * i] - tg.p[3 * t], dy = x0[3 * i + 1] - tg.p[3 * t + 1], dz = x0[3 * i + 2] - tg.p[3 * t + 2];
            const w = Math.exp(-(dx * dx + dy * dy + dz * dz) / s2); G[a * ts.length + b] = w; own[a] += w; have[b] += w; }
          if (own[a] < cfg.mig_th) any = true;
        });
        if (!any || !ts.length) continue;
        const want = new Float32Array(ts.length);
        for (let b = 0; b < ts.length; b++) { const t = ts[b]; let s = 0;
          for (let b2 = 0; b2 < ts.length; b2++) { const u = ts[b2]; const dx = tg.p[3 * t] - tg.p[3 * u], dy = tg.p[3 * t + 1] - tg.p[3 * u + 1], dz = tg.p[3 * t + 2] - tg.p[3 * u + 2]; s += Math.exp(-(dx * dx + dy * dy + dz * dz) / s2); }
          want[b] = s; }
        xs.forEach((i, a) => {
          if (!(own[a] < cfg.mig_th)) return;
          let best = -1, bs = -Infinity;
          for (let b = 0; b < ts.length; b++) { const t = ts[b];
            const dist = Math.hypot(tg.p[3 * t] - x0[3 * i], tg.p[3 * t + 1] - x0[3 * i + 1], tg.p[3 * t + 2] - x0[3 * i + 2]);
            const sc = want[b] - have[b] - dist / cfg.mig_L; if (sc > bs) { bs = sc; best = b; } }
          const t = ts[best];
          const dx = tg.p[3 * t] - x0[3 * i], dy = tg.p[3 * t + 1] - x0[3 * i + 1], dz = tg.p[3 * t + 2] - x0[3 * i + 2], dl = Math.hypot(dx, dy, dz);
          const sl = cfg.k_mig * W.vmax[elem[i]];
          if (dl < sl) { pos[3 * i] = x0[3 * i] + dx; pos[3 * i + 1] = x0[3 * i + 1] + dy; pos[3 * i + 2] = x0[3 * i + 2] + dz; }
          else { const f = sl / Math.max(dl, 1e-6); pos[3 * i] = x0[3 * i] + f * dx; pos[3 * i + 1] = x0[3 * i + 1] + f * dy; pos[3 * i + 2] = x0[3 * i + 2] + f * dz; }
          nm++;
        });
      }
      this.migrants = nm;
    }
    /** Boid2.starve_staggered */
    starveStaggered() {
      const cfg = this.cfg, N = this.N, S = this.S, elem = this.elem, dom = this.dom;
      if (this.gplan < 0) return;
      const have = new Float32Array(NCLS), want = new Float32Array(NCLS);
      for (let i = 0; i < N; i++) if (this.active[i] && this.hatched[i]) have[elem[i] * 3 + dom[i]]++;
      const fr = this.targets[KINDS[this.gplan]].frames[0];
      for (let t = 0; t < fr.e.length; t++) want[fr.e[t] * 3 + this.dmap[fr.sl[t]]]++;
      const slack = new Float32Array(NCLS).fill(cfg.starve_slack);
      if (cfg.small_slack >= 0) for (let e = 0; e < 4; e++) if (want[e * 3] + want[e * 3 + 1] + want[e * 3 + 2] <= 2) for (let d = 0; d < 3; d++) slack[e * 3 + d] = cfg.small_slack;
      const excess = new Float32Array(NCLS);
      for (let c = 0; c < NCLS; c++) excess[c] = Math.max(have[c] - Math.floor((1 + cfg.starve_tol) * want[c]) - slack[c], 0);
      if (cfg.elem_floor) for (let e = 0; e < 4; e++) {
        const he = have[e * 3] + have[e * 3 + 1] + have[e * 3 + 2], we = want[e * 3] + want[e * 3 + 1] + want[e * 3 + 2];
        const eexc = Math.max(he - we - slack[e * 3], 0), tot = Math.max(excess[e * 3] + excess[e * 3 + 1] + excess[e * 3 + 2], 1e-6), r = Math.min(eexc / tot, 1);
        for (let d = 0; d < 3; d++) excess[e * 3 + d] = Math.floor(excess[e * 3 + d] * r + 1e-4);
      }
      const cand = [];
      for (let i = 0; i < N; i++) {
        const o = 13 * i + 12, live = this.active[i] && this.hatched[i], cl = elem[i] * 3 + dom[i];
        S[o] = live && excess[cl] > 0 ? S[o] + cfg.starve_rate * this.U[i] : Math.max(S[o] - cfg.starve_rate, 0);
        if (live && S[o] > 1.0) cand.push(i);
      }
      cand.sort((a, b) => S[13 * b + 12] - S[13 * a + 12]);
      const quota = excess.slice();
      for (const i of cand) {
        const c = elem[i] * 3 + dom[i];
        if (quota[c] >= 1) { quota[c]--; this.deaths++; this.starved++; this.kill(i, 'death'); }
        else S[13 * i + 12] = 1.0;
      }
    }
  }
  function domainMap(dcnt, slotMix) {
    const sh = [0, 1, 2].map(s => slotMix[s] || 0), ss = sh[0] + sh[1] + sh[2], n = dcnt[0] + dcnt[1] + dcnt[2];
    let best = -1, arg = PERM3[0];
    for (const p of PERM3) { let v = 0; for (let s = 0; s < 3; s++) v += Math.min(dcnt[p[s]], sh[s] / ss * n); if (v > best + 1e-6) { best = v; arg = p; } }
    return arg.slice();
  }

  // ------------------------------------------------------------------ sort: emergent cell sorting ---
  /* A port of sort_model.SortSwarm at results/sort/params.json. No grid, no network, no assigned places:
   *   * a tadpole's TYPE = (element, region); a region is the body part its DOMAIN plays in the current plan
   *     (the domain -> region map is re-picked from the census every role_every steps, sticky)
   *   * positional information: per type a mixture of Gaussian wells in body coordinates (origin = the swarm's
   *     centroid); a newborn COMMITS to the well its type under-occupies most and climbs it
   *   * neighbours: collision, differential adhesion (unlike types push apart harder than like), Potts swaps
   *   * composition: a parent lays while its class is short of the plan (or lays its domain's most-needed
   *     element); total headcount is capped at the plan's; a SURPLUS tadpole MOLTS into a deficit element of
   *     its own domain, and may transfer to the neediest region. Nothing dies on a clock or of hunger.
   * The wells are PlanCode's (k-means with its own fixed seed), exported once by live_export.py sort. */
  const H_ROLE = 12, H_EST = 13, H_VEL = 16, H_AGE = 19, H_FATE = 20, H_FKEY = 21;
  function sortPerms(k) { return k === 1 ? [[0]] : k === 2 ? [[0, 1], [1, 0]] : PERM3; }
  class SortSwarm extends LiveSwarm {
    constructor(targets, code, opts) {
      opts = Object.assign({}, opts || {}, { stride: 32 });
      super(targets, opts);
      this.code = code; this.scfg = Object.assign({}, code.cfg, (opts || {}).scfg || {});
      if (!code._prep) {
        for (const k of KINDS) {
          const c = code.codes[k]; c.keys = [];
          for (const key of Object.keys(c.wells)) {
            const [e, s] = key.split(',').map(Number), w = c.wells[key];
            w.e = e; w.s = s; w.K = w.w.length; w.logw = w.w.map(Math.log); c.keys.push(w);
          }
        }
        code._prep = true;
      }
      this.mem = null; this.molts = 0; this.transfers = 0;
    }
    clear() { super.clear(); this.mem = null; }
    reset() {
      const c = [0, 0, 0, 0]; for (let i = 0; i < this.N; i++) if (this.active[i] && this.hatched[i]) c[this.elem[i]]++;
      let mx = 0; for (let e = 1; e < 4; e++) if (c[e] > c[mx]) mx = e;
      this.mem = { plan: KINDS[PLAN_OF[mx]], cand: KINDS[PLAN_OF[mx]], cand_n: 0, perm: null, t: 0 };
    }
    pickPerm(code, idx, old) {
      const cen = [0, 1, 2, 3].map(() => [0, 0, 0]);
      for (const i of idx) cen[this.elem[i]][this.dom[i]]++;
      let tot = 0; cen.forEach(r => r.forEach(v => tot += v)); tot = Math.max(1, tot);
      const scale = tot / Math.max(1, code.n);
      let best = null, arg = null;
      for (const perm of sortPerms(code.nslots)) {
        let cost = 0;
        for (let e = 0; e < 4; e++) for (let s = 0; s < code.nslots; s++) cost += Math.abs(cen[e][perm[s]] - code.counts[e][s] * scale);
        if (old && old.length === perm.length && old.every((v, q) => v === perm[q])) cost -= 2.0;
        if (best === null || cost < best) { best = cost; arg = perm; }
      }
      return arg.slice();
    }
    effRole(i, roleOfDom) {
      let r = roleOfDom[this.dom[i]];
      const pid = KINDS.indexOf(this.mem.plan), h = Math.trunc(this.S[32 * i + H_ROLE]) - 1;
      if (h >= 0 && Math.floor(h / 4) === pid) r = h % 4;
      return r;
    }
    census44(roleOfDom, eggs) {
      const cen = [0, 1, 2, 3].map(() => [0, 0, 0, 0]);
      for (let i = 0; i < this.N; i++) if (this.active[i] && (eggs || this.hatched[i])) { const r = this.effRole(i, roleOfDom); cen[this.elem[i]][r < 0 ? 3 : r]++; }
      return cen;
    }
    poisson(lam) { const L = Math.exp(-lam); let k = 0, p = 1; do { k++; p *= this.rng(); } while (p > L); return k - 1; }
    permutation(n) { const a = [...Array(n).keys()]; return shuffle(a, this.rng); }
    step() {
      this.prev.set(this.pos);
      if (this.clock === 0 || !this.mem) this.reset();
      this.sortStep();
      this.clock++;
      this.gplan = KINDS.indexOf(this.mem.plan);
    }
    sortStep() {
      const cfg = this.scfg, m = this.mem, rng = this.rng, N = this.N, S = this.S, pos = this.pos, elem = this.elem, dom = this.dom;
      let idx = []; for (let i = 0; i < N; i++) if (this.active[i] && this.hatched[i]) idx.push(i);
      if (!idx.length) return;
      // --- plan from element ratios, with dwell hysteresis
      const counts = [0, 0, 0, 0]; for (const i of idx) counts[elem[i]]++;
      const cur = MAJOR[m.plan]; let top = 0; for (let e = 1; e < 4; e++) if (counts[e] > counts[top]) top = e;
      const maj = counts[cur] === counts[top] ? cur : top;
      let contested = false;
      if (maj !== cur) {
        const cand = KINDS[PLAN_OF[maj]];
        m.cand_n = m.cand === cand ? m.cand_n + 1 : 1; m.cand = cand;
        if (m.cand_n >= cfg.dwell) {
          this.switches.push({ step: this.clock, from: m.plan, to: cand }); this.events.push({ type: 'switch', from: m.plan, to: cand, step: this.clock });
          m.plan = cand; m.perm = null; m.cand_n = 0;
        } else contested = true;
      } else { m.cand = m.plan; m.cand_n = 0; }
      const code = this.code.codes[m.plan];
      if (m.perm === null || m.t % cfg.role_every === 0) m.perm = this.pickPerm(code, idx, m.perm);
      const perm = m.perm, roleOfDom = [-1, -1, -1];
      perm.forEach((d, s) => { roleOfDom[d] = s; });
      m.t++;
      // --- hatching
      for (let i = 0; i < N; i++) if (this.active[i] && !this.hatched[i]) { S[32 * i + H_AGE] += 1; if (S[32 * i + H_AGE] >= cfg.hatch_steps) this.hatched[i] = 1; }
      idx = []; for (let i = 0; i < N; i++) if (this.active[i] && this.hatched[i]) idx.push(i);
      const n = idx.length, role = new Int8Array(n);
      for (let a = 0; a < n; a++) role[a] = this.effRole(idx[a], roleOfDom);
      // --- centre and body coords
      let c0 = [0, 0, 0]; for (const i of idx) for (let k = 0; k < 3; k++) c0[k] += pos[3 * i + k]; c0 = c0.map(v => v / n);
      const xb = new Float64Array(3 * n); for (let a = 0; a < n; a++) for (let k = 0; k < 3; k++) xb[3 * a + k] = pos[3 * idx[a] + k] - c0[k];
      // --- own-type chemotaxis toward one's fated well
      const G = new Float64Array(3 * n), MU = Float64Array.from(xb), INV = new Float64Array(9 * n), hasWell = new Uint8Array(n);
      const pid = KINDS.indexOf(m.plan);
      for (const w of code.keys) {
        const e = w.e, s = w.s, sel = []; for (let a = 0; a < n; a++) if (elem[idx[a]] === e && role[a] === s) sel.push(a);
        if (!sel.length) continue;
        const key = 1 + pid * 16 + e * 4 + s;
        const stale = sel.filter(a => { const j = idx[a]; return S[32 * j + H_FATE] < 1 || S[32 * j + H_FKEY] !== key; });
        if (stale.length) {
          const occ = new Float64Array(w.K);
          for (const a of sel) { const j = idx[a]; if (!(S[32 * j + H_FATE] < 1 || S[32 * j + H_FKEY] !== key)) occ[Math.trunc(S[32 * j + H_FATE] - 1)]++; }
          const need = w.w.map((v, q) => v * sel.length - occ[q]);
          for (const a of stale) {
            const j = idx[a]; let f = 0, bv = -Infinity;
            for (let q = 0; q < w.K; q++) { const v = need[q] + 1e-3 * rng(); if (v > bv) { bv = v; f = q; } }
            S[32 * j + H_FATE] = f + 1; S[32 * j + H_FKEY] = key; need[f] -= 1;
          }
        }
        for (const a of sel) {
          const k = Math.trunc(S[32 * idx[a] + H_FATE] - 1), mu = w.mu, inv = w.inv;
          const d0 = xb[3 * a] - mu[3 * k], d1 = xb[3 * a + 1] - mu[3 * k + 1], d2 = xb[3 * a + 2] - mu[3 * k + 2], o = 9 * k;
          G[3 * a] = inv[o] * d0 + inv[o + 1] * d1 + inv[o + 2] * d2;
          G[3 * a + 1] = inv[o + 3] * d0 + inv[o + 4] * d1 + inv[o + 5] * d2;
          G[3 * a + 2] = inv[o + 6] * d0 + inv[o + 7] * d1 + inv[o + 8] * d2;
          for (let q = 0; q < 3; q++) MU[3 * a + q] = mu[3 * k + q];
          for (let q = 0; q < 9; q++) INV[9 * a + q] = inv[o + q];
          hasWell[a] = 1;
        }
      }
      // orphans (no region, or a class the plan has no wells for): climb the plan's whole body (best type)
      for (let a = 0; a < n; a++) {
        if (hasWell[a] || code.wells[elem[idx[a]] + ',' + role[a]]) continue;
        let Eb = Infinity, gb = [0, 0, 0];
        for (const w of code.keys) { const r = mixGrad(w, xb[3 * a], xb[3 * a + 1], xb[3 * a + 2]); if (r.E < Eb) { Eb = r.E; gb = r.g; } }
        G[3 * a] = gb[0]; G[3 * a + 1] = gb[1]; G[3 * a + 2] = gb[2];
      }
      const want = new Float64Array(3 * n);
      for (let a = 0; a < n; a++) {
        const sx = -cfg.k_well * G[3 * a], sy = -cfg.k_well * G[3 * a + 1], sz = -cfg.k_well * G[3 * a + 2], nr = Math.hypot(sx, sy, sz);
        const f = Math.min(1, cfg.well_clip / Math.max(nr, 1e-9));
        want[3 * a] = sx * f; want[3 * a + 1] = sy * f; want[3 * a + 2] = sz * f;
      }
      // --- neighbours: collision, adhesion, swaps (dense, as the Python)
      const P = new Float64Array(3 * n); for (let a = 0; a < n; a++) for (let k = 0; k < 3; k++) P[3 * a + k] = pos[3 * idx[a] + k];
      const fcol = new Float64Array(3 * n), fadh = new Float64Array(3 * n), fsw = new Float64Array(3 * n);
      const el = new Int8Array(n); for (let a = 0; a < n; a++) el[a] = elem[idx[a]];
      const R2s = cfg.r_swap * cfg.r_swap, R2a = cfg.R_adh * cfg.R_adh, R2c = cfg.r0 * cfg.r0;
      const eAt = (a, y0, y1, y2) => { const d0 = y0 - MU[3 * a], d1 = y1 - MU[3 * a + 1], d2 = y2 - MU[3 * a + 2], o = 9 * a;
        return 0.5 * (d0 * (INV[o] * d0 + INV[o + 1] * d1 + INV[o + 2] * d2) + d1 * (INV[o + 3] * d0 + INV[o + 4] * d1 + INV[o + 5] * d2) + d2 * (INV[o + 6] * d0 + INV[o + 7] * d1 + INV[o + 8] * d2)); };
      for (let a = 0; a < n; a++) for (let b = a + 1; b < n; b++) {
        const dx = P[3 * b] - P[3 * a], dy = P[3 * b + 1] - P[3 * a + 1], dz = P[3 * b + 2] - P[3 * a + 2], q = dx * dx + dy * dy + dz * dz;
        if (q >= R2a && q >= R2s && q >= R2c) continue;
        const d = Math.sqrt(q);
        if (d < cfg.r0) { const r = (cfg.r0 - d) / d; fcol[3 * a] -= cfg.k_rep * r * dx; fcol[3 * a + 1] -= cfg.k_rep * r * dy; fcol[3 * a + 2] -= cfg.k_rep * r * dz;
          fcol[3 * b] += cfg.k_rep * r * dx; fcol[3 * b + 1] += cfg.k_rep * r * dy; fcol[3 * b + 2] += cfg.k_rep * r * dz; }
        if (d < cfg.R_adh && d > cfg.r0 * 0.9) {
          const st = el[a] === el[b] && role[a] === role[b], se = el[a] === el[b], sr = role[a] === role[b];
          const A = (st ? cfg.a_same : se ? cfg.a_elem : sr ? cfg.a_role : cfg.a_other) / d;
          fadh[3 * a] += A * dx; fadh[3 * a + 1] += A * dy; fadh[3 * a + 2] += A * dz;
          fadh[3 * b] -= A * dx; fadh[3 * b + 1] -= A * dy; fadh[3 * b + 2] -= A * dz;
        }
        if (cfg.swap > 0 && d < cfg.r_swap) {
          const gain = (eAt(a, xb[3 * a], xb[3 * a + 1], xb[3 * a + 2]) + eAt(b, xb[3 * b], xb[3 * b + 1], xb[3 * b + 2]))
            - (eAt(a, xb[3 * b], xb[3 * b + 1], xb[3 * b + 2]) + eAt(b, xb[3 * a], xb[3 * a + 1], xb[3 * a + 2]));
          if (gain > cfg.swap_margin) {
            const h = cfg.swap * 0.5;
            fsw[3 * a] += h * dx; fsw[3 * a + 1] += h * dy; fsw[3 * a + 2] += h * dz;
            fsw[3 * b] -= h * dx; fsw[3 * b + 1] -= h * dy; fsw[3 * b + 2] -= h * dz;
            const r = Math.max(cfg.r0 - d, 0) / d * cfg.k_rep;
            fcol[3 * a] += r * dx; fcol[3 * a + 1] += r * dy; fcol[3 * a + 2] += r * dz;
            fcol[3 * b] -= r * dx; fcol[3 * b + 1] -= r * dy; fcol[3 * b + 2] -= r * dz;
          }
        }
      }
      for (let a = 0; a < n; a++) {
        const i = idx[a], o = 32 * i + H_VEL;
        let vx = cfg.inertia * S[o] + (1 - cfg.inertia) * (want[3 * a] + fcol[3 * a] + fadh[3 * a] + fsw[3 * a]);
        let vy = cfg.inertia * S[o + 1] + (1 - cfg.inertia) * (want[3 * a + 1] + fcol[3 * a + 1] + fadh[3 * a + 1] + fsw[3 * a + 1]);
        let vz = cfg.inertia * S[o + 2] + (1 - cfg.inertia) * (want[3 * a + 2] + fcol[3 * a + 2] + fadh[3 * a + 2] + fsw[3 * a + 2]);
        const sp = Math.hypot(vx, vy, vz), f = Math.min(1, cfg.vmax[el[a]] / Math.max(sp, 1e-9));
        vx *= f; vy *= f; vz *= f;
        pos[3 * i] += vx; pos[3 * i + 1] += vy; pos[3 * i + 2] += vz;
        S[o] = vx; S[o + 1] = vy; S[o + 2] = vz;
        this.vel[3 * i] = vx; this.vel[3 * i + 1] = vy; this.vel[3 * i + 2] = vz;
      }
      // --- look: the type's code state
      const keep = new Float32Array(H_FKEY - H_ROLE + 1);
      for (let a = 0; a < n; a++) {
        const e = el[a], r = role[a], j = idx[a];
        let w = code.wells[e + ',' + r], st = w ? w.state : null;
        if (!st) for (let s2 = 0; s2 < 3 && !st; s2++) { const w2 = code.wells[e + ',' + s2]; if (w2) st = w2.state; }
        if (!st) st = this.code.fallback[e];
        const o = 32 * j;
        keep.set(S.subarray(o + H_ROLE, o + H_FKEY + 1));
        for (let q = 0; q < 32; q++) S[o + q] = st[q];
        S.set(keep, o + H_ROLE);
      }
      // --- composition
      if (!contested) { this.sortLay(code, roleOfDom); if (cfg.molt) this.sortMolt(code, roleOfDom); }
    }
    sortLay(code, roleOfDom) {
      const cfg = this.scfg, rng = this.rng, N = this.N, S = this.S, elem = this.elem, dom = this.dom, pos = this.pos;
      const cen = this.census44(roleOfDom, true), want = cen.map((_, e) => [0, 1, 2].map(s => Math.ceil(code.counts[e][s] * cfg.over)).concat([0]));
      const deficit = want.map((r, e) => r.map((v, s) => v - cen[e][s]));
      let tot = 0; for (let e = 0; e < 4; e++) for (let s = 0; s < 3; s++) tot += Math.max(deficit[e][s], 0);
      if (tot <= 0) return;
      const idx = [], free = []; let nact = 0;
      for (let i = 0; i < N; i++) { if (this.active[i]) { nact++; if (this.hatched[i]) idx.push(i); } else free.push(i); }
      const room = Math.ceil(code.n * cfg.over) - nact;
      const fill = want.map((r, e) => r.map((v, s) => v > 0 ? cen[e][s] / Math.max(v, 1) : 9.0));
      const nlay = Math.min(cfg.lay_max, free.length, room, this.poisson(Math.max(cfg.lay_rate * idx.length, 0.2)));
      if (nlay <= 0) return;
      const ec = [0, 0, 0, 0]; for (let i = 0; i < N; i++) if (this.active[i]) ec[elem[i]]++;
      const maj = MAJOR[this.mem.plan];
      let laid = 0;
      for (const q of this.permutation(idx.length)) {
        const i = idx[q];
        if (laid >= nlay || !free.length) break;
        const r = roleOfDom[dom[i]]; if (r < 0) continue;
        const e = elem[i];
        let low = 0; for (let x = 1; x < 4; x++) if (fill[x][r] < fill[low][r]) low = x;
        let ce;
        if (deficit[e][r] > 0 && fill[e][r] <= fill[low][r] + cfg.fill_tol) ce = e;
        else if (rng() < cfg.p_cross && deficit[low][r] > 0) ce = low;
        else continue;
        if (ce !== maj && ec[ce] + 1 >= ec[maj]) { if (rng() < cfg.p_cross && deficit[maj][r] > 0) ce = maj; else continue; }
        ec[ce]++;
        const j = free.shift();
        let dx = rng.normal(), dy = rng.normal(), dz = rng.normal(); const dl = Math.max(Math.hypot(dx, dy, dz), 1e-6);
        const est = S.slice(32 * i + H_EST, 32 * i + H_EST + 3);
        this.place(j, pos[3 * i] + cfg.r_bud * dx / dl, pos[3 * i + 1] + cfg.r_bud * dy / dl, pos[3 * i + 2] + cfg.r_bud * dz / dl, ce, dom[i], false);
        S[32 * j] = 0.2; S.set(est, 32 * j + H_EST);
        this.prev[3 * j] = pos[3 * i]; this.prev[3 * j + 1] = pos[3 * i + 1]; this.prev[3 * j + 2] = pos[3 * i + 2];
        this.events.push({ type: 'birth', i: j, parent: i, elem: ce, dom: dom[i], step: this.clock });
        deficit[ce][r]--; cen[ce][r]++; fill[ce][r] = cen[ce][r] / Math.max(want[ce][r], 1);
        laid++;
      }
    }
    sortMolt(code, roleOfDom) {
      const cfg = this.scfg, rng = this.rng, N = this.N, S = this.S, elem = this.elem;
      const cen = this.census44(roleOfDom, false), want = [0, 1, 2, 3].map(e => [...code.counts[e].slice(0, 3), 0]);
      const idx = []; for (let i = 0; i < N; i++) if (this.active[i] && this.hatched[i]) idx.push(i);
      const roles = idx.map(i => this.effRole(i, roleOfDom)), pid = KINDS.indexOf(this.mem.plan), maj = MAJOR[code.kind || this.mem.plan];
      for (const q of this.permutation(idx.length)) {
        const i = idx[q], r = roles[q];
        if (rng() > cfg.molt_rate) continue;
        const e = elem[i], rc = r < 0 ? 3 : r;
        if (cen[e][rc] <= want[e][rc]) continue;
        const def = want.map((row, x) => row.map((v, s) => v - cen[x][s]));
        let r2;
        if (r >= 0 && Math.max(def[0][r], def[1][r], def[2][r], def[3][r]) > 0) r2 = r;
        else {
          let bv = -Infinity, bs = -1; for (let x = 0; x < 4; x++) for (let s = 0; s < 3; s++) if (def[x][s] > bv) { bv = def[x][s]; bs = s; }
          if (cfg.transfer && bv > 0) { r2 = bs; S[32 * i + H_ROLE] = 1 + 4 * pid + r2; this.transfers++; } else continue;
        }
        const fl = [0, 1, 2, 3].map(x => def[x][r2] <= 0 ? 9.0 : want[x][r2] > 0 ? cen[x][r2] / Math.max(want[x][r2], 1) : 9.0);
        let ne = 0; for (let x = 1; x < 4; x++) if (fl[x] < fl[ne]) ne = x;
        const ec = [0, 0, 0, 0]; for (const j of idx) ec[elem[j]]++;
        if (ne !== maj && ec[ne] + 1 >= ec[maj]) continue;
        elem[i] = ne; cen[e][rc]--; cen[ne][r2]++;
        this.molts++; this.events.push({ type: 'molt', i, from: e, to: ne, step: this.clock });
      }
    }
  }
  /** PlanCode.energy_grad for one point: -log mixture density of a type's wells and its gradient */
  function mixGrad(w, x, y, z) {
    const K = w.K, lp = new Float64Array(K), Md = new Float64Array(3 * K);
    let mx = -Infinity;
    for (let k = 0; k < K; k++) {
      const d0 = x - w.mu[3 * k], d1 = y - w.mu[3 * k + 1], d2 = z - w.mu[3 * k + 2], o = 9 * k, iv = w.inv;
      const m0 = iv[o] * d0 + iv[o + 1] * d1 + iv[o + 2] * d2, m1 = iv[o + 3] * d0 + iv[o + 4] * d1 + iv[o + 5] * d2, m2 = iv[o + 6] * d0 + iv[o + 7] * d1 + iv[o + 8] * d2;
      Md[3 * k] = m0; Md[3 * k + 1] = m1; Md[3 * k + 2] = m2;
      lp[k] = w.logw[k] - 0.5 * (d0 * m0 + d1 * m1 + d2 * m2) - 0.5 * w.ld[k]; if (lp[k] > mx) mx = lp[k];
    }
    let Z = 0; const r = new Float64Array(K); for (let k = 0; k < K; k++) { r[k] = Math.exp(lp[k] - mx); Z += r[k]; }
    const g = [0, 0, 0]; for (let k = 0; k < K; k++) { const rk = r[k] / Z; g[0] += rk * Md[3 * k]; g[1] += rk * Md[3 * k + 1]; g[2] += rk * Md[3 * k + 2]; }
    return { E: -(mx + Math.log(Z)), g };
  }

  // ------------------------------------------------------------------ helpers ---
  function roundEven(x) { const r = Math.round(x); return (Math.abs(x % 1) === 0.5 && r % 2 !== 0) ? r - 1 : r; }
  function rep(counts) { const o = []; counts.forEach((c, i) => { for (let k = 0; k < c; k++) o.push(i); }); return o; }
  function shuffle(a, rng) { for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(rng() * (i + 1)); const t = a[i]; a[i] = a[j]; a[j] = t; } return a; }
  function largestRemainder(weights, n, need) {
    const s = weights.reduce((a, b) => a + b, 0) || 1, w = weights.map(x => x / s);
    const base = w.map(x => Math.floor(x * n)), rem = w.map((x, i) => x * n - base[i]);
    for (const i of need || []) if (base[i] === 0) base[i] = 1;
    let tot = base.reduce((a, b) => a + b, 0);
    while (tot < n) { let i = 0; for (let j = 1; j < rem.length; j++) if (rem[j] > rem[i]) i = j; base[i]++; rem[i] = -1; tot++; }
    while (tot > n) { let i = 0; for (let j = 1; j < base.length; j++) if (base[j] > base[i]) i = j; base[i]--; tot--; }
    return base;
  }

  const API = { LiveSwarm, EvoSwarm, Hgrid2Swarm, SortSwarm, HG2_CFG, PlanFields, Grid, makeRng, prismH, rawPrism, KINDS, MAJOR, PLAN_OF, DEF_CFG, DEF_WORLD, largestRemainder };
  if (typeof module !== 'undefined' && module.exports) module.exports = API; else root.SwarmLive = API;

  // ------------------------------------------------------------------ node entry ---
  if (typeof require !== 'undefined' && typeof module !== 'undefined' && require.main === module) {
    const fs = require('fs'), argv = process.argv.slice(2), cmd = argv[0];
    const targets = JSON.parse(fs.readFileSync(argv[1] || 'results/live/targets.json', 'utf8'));
    // MODEL: '-' grid | evo_rule.json | hgrid2 | hgrid2:12 (G) | sort (results/live2/sort_code.json) | sort:path
    const makeModel = (spec, seed, pfs) => {
      if (!spec || spec === '-') return new LiveSwarm(targets, { seed, planFields: pfs.grid || (pfs.grid = new PlanFields(targets, DEF_CFG)) });
      if (spec.startsWith('hgrid2')) { const G = +(spec.split(':')[1] || 16), cfg = Object.assign({}, DEF_CFG, HG2_CFG, { G });
        return new Hgrid2Swarm(targets, { seed, cfg, planFields: pfs['h' + G] || (pfs['h' + G] = new PlanFields(targets, cfg)) }); }
      if (spec.startsWith('sort')) { const p = spec.split(':')[1] || 'results/live2/sort_code.json';
        const code = pfs.sort || (pfs.sort = JSON.parse(fs.readFileSync(p, 'utf8'))); return new SortSwarm(targets, code, { seed }); }
      const evo = pfs.evo || (pfs.evo = JSON.parse(fs.readFileSync(spec, 'utf8'))); return new EvoSwarm(targets, evo, { seed });
    };
    if (cmd === 'fidelity') {
      // node swarm_live.js fidelity targets.json out.json SEEDS STEPS [evo_rule.json|-] [switch]
      const out = argv[2] || 'runs/live_js_states.json', seeds = +(argv[3] || 8), steps = +(argv[4] || 240);
      const spec = argv[5] || '-', sw_ = argv[6] === 'switch';
      const SWITCH_TO = { mass: 2, space: 0, charge: 3, time: 1 };
      const pfs = {}, states = [];
      for (const k of (process.env.LIVE_KINDS || KINDS.join(',')).split(',')) for (let s = 0; s < seeds; s++) {
        const sw = makeModel(spec, 1000 + s, pfs);
        sw.seedPlan(k, 16);
        const t0 = Date.now(); for (let t = 0; t < steps; t++) sw.step();
        let c = sw.census();
        console.log(k, s, 'n', c.n, 'mix', c.elements.join('/'), 'plan', c.plan, (Date.now() - t0) + 'ms');
        states.push(Object.assign({ kind: k, seed: s, step: steps, tag: 'own', want: k }, sw.exportState()));
        if (sw_) {
          const want = sw.loseMajority(SWITCH_TO[k]);
          for (let t = 0; t < steps; t++) sw.step();
          c = sw.census();
          console.log('  switch ->', want, 'n', c.n, 'mix', c.elements.join('/'), 'plan', c.plan);
          if (want) states.push(Object.assign({ kind: k, seed: s, step: 2 * steps, tag: 'switch', want }, sw.exportState()));
        }
      }
      fs.writeFileSync(out, JSON.stringify({ states }));
    } else if (cmd === 'predation') {
      // How hard is it to make a grown body change species by GRAZING it? Every step a predator eats
      // `rate` random tadpoles of the current majority element (fractional rates are a per-step chance).
      // Reports the steps until the plan switches (cap 600) and how many it had to eat.
      // node swarm_live.js predation targets.json [evo_rule.json|-] SEEDS
      const spec = argv[2] || '-', seeds = +(argv[3] || 4);
      const pfs = {}, out = {};
      for (const k of KINDS) for (const rate of (process.env.LIVE_RATES || '0.25,0.5,1,2,4').split(',').map(Number)) {
        const rows = [];
        for (let s = 0; s < seeds; s++) {
          const ov = process.env.LIVE_CFG ? JSON.parse(process.env.LIVE_CFG) : {};
          const sw = makeModel(spec, 2000 + s, pfs); Object.assign(sw.scfg || sw.cfg, ov);
          sw.seedPlan(k, 16); for (let t = 0; t < 240; t++) sw.step();
          const plan0 = sw.census().plan, n0 = sw.census().n; let eaten = 0, t = 0;
          for (; t < 600; t++) {
            const c = sw.census(); if (c.plan !== plan0 || c.n === 0) break;
            let q = Math.floor(rate) + (sw.rng() < rate % 1 ? 1 : 0);
            const ids = []; for (let i = 0; i < sw.N; i++) if (sw.active[i] && sw.hatched[i] && sw.elem[i] === c.majority) ids.push(i);
            shuffle(ids, sw.rng).slice(0, q).forEach(i => { sw.kill(i, 'kill'); eaten++; });
            sw.step(); sw.events.length = 0;
          }
          const c = sw.census();
          rows.push({ steps: t, eaten, n0, switched: c.plan !== plan0, to: c.plan, n: c.n });
        }
        (out[k] = out[k] || {})[rate] = rows;
        const sw_ = rows.filter(r => r.switched);
        console.log(k, 'rate', rate, 'switched', sw_.length + '/' + rows.length, 'steps', rows.map(r => r.steps).join(','), 'eaten', rows.map(r => r.eaten).join(','), 'to', rows.map(r => r.to).join(','));
      }
      fs.writeFileSync(argv[4] || 'runs/live_predation.json', JSON.stringify(out));
    } else if (cmd === 'bench') {
      // node swarm_live.js bench targets.json [MODEL]
      const pfs = {}, spec = argv[2] || '-';
      for (const k of KINDS) {
        const sw = makeModel(spec, 3, pfs); sw.seedPlan(k, 16);
        for (let t = 0; t < 240; t++) sw.step();
        const t0 = process.hrtime.bigint(); for (let t = 0; t < 200; t++) sw.step();
        const ms = Number(process.hrtime.bigint() - t0) / 1e6 / 200;
        console.log(k, 'n', sw.census().n, ms.toFixed(2), 'ms/step', (1000 / ms).toFixed(0), 'steps/s');
      }
    }
  }
})(typeof window !== 'undefined' ? window : this);
