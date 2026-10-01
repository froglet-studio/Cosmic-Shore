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
      const fr = cfg.animate ? Math.floor(this.clock / cfg.period) % nf : 0;
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

  const API = { LiveSwarm, EvoSwarm, PlanFields, Grid, makeRng, prismH, rawPrism, KINDS, MAJOR, PLAN_OF, DEF_CFG, DEF_WORLD, largestRemainder };
  if (typeof module !== 'undefined' && module.exports) module.exports = API; else root.SwarmLive = API;

  // ------------------------------------------------------------------ node entry ---
  if (typeof require !== 'undefined' && typeof module !== 'undefined' && require.main === module) {
    const fs = require('fs'), argv = process.argv.slice(2), cmd = argv[0];
    const targets = JSON.parse(fs.readFileSync(argv[1] || 'results/live/targets.json', 'utf8'));
    if (cmd === 'fidelity') {
      // node swarm_live.js fidelity targets.json out.json SEEDS STEPS [evo_rule.json|-] [switch]
      const out = argv[2] || 'runs/live_js_states.json', seeds = +(argv[3] || 8), steps = +(argv[4] || 240);
      const evo = argv[5] && argv[5] !== '-' ? JSON.parse(fs.readFileSync(argv[5], 'utf8')) : null, sw_ = argv[6] === 'switch';
      const SWITCH_TO = { mass: 2, space: 0, charge: 3, time: 1 };
      const pf = new PlanFields(targets, DEF_CFG), states = [];
      for (const k of KINDS) for (let s = 0; s < seeds; s++) {
        const sw = evo ? new EvoSwarm(targets, evo, { seed: 1000 + s }) : new LiveSwarm(targets, { seed: 1000 + s, planFields: pf });
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
    } else if (cmd === 'bench') {
      const pf = new PlanFields(targets, DEF_CFG), evo = argv[2] ? JSON.parse(fs.readFileSync(argv[2], 'utf8')) : null;
      for (const k of KINDS) {
        const sw = evo ? new EvoSwarm(targets, evo, { seed: 3 }) : new LiveSwarm(targets, { seed: 3, planFields: pf, capacity: 400 }); sw.seedPlan(k, 16);
        for (let t = 0; t < 240; t++) sw.step();
        const t0 = process.hrtime.bigint(); for (let t = 0; t < 200; t++) sw.step();
        const ms = Number(process.hrtime.bigint() - t0) / 1e6 / 200;
        console.log(k, 'n', sw.census().n, ms.toFixed(2), 'ms/step', (1000 / ms).toFixed(0), 'steps/s');
      }
    }
  }
})(typeof window !== 'undefined' ? window : this);
