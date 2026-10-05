// ===================================================================================================================
// nca_creature.js - a trained 3D neural cellular automaton (Tools/NCA/results/lizard3d_swim) as a flyable creature.
//
// The lizard is not animated by keyframes: it is a 22x44x44x16 grid of cells, every cell running the same small
// network (64 perception features -> 128 ReLU -> 16) on its 27-voxel neighbourhood. Trained so the body swims a
// helical cycle of 8 poses, one pose per ~8.5 NCA steps, forever, and REGROWS whatever is cut away.
//
//   const c = new NcaCreature({ seed: 7, scale: 0.6, position: [0, 20, 0], element: 'space' });
//   c.grow(96);                         // optional synchronous pre-grow (a few hundred ms); otherwise it grows live
//   per frame:  c.step(dt);  c.sync();  // step() spends at most budgetMs / cellsPerFrame, then RESUMES next frame
//   c.hit(worldPoint, worldRadius);     // erase voxels -> the NCA regrows them over the next ~100-300 steps
//   const mesh = c.mesh(THREE); scene.add(mesh);   // InstancedMesh of small triangular prisms, refreshed by sync()
//
// Inference is nca3d_core.js (held equal to the PyTorch model by Tools/NCA/verify_js3d.py), made resumable: a step
// is split into (begin: alive mask + list of alive cells) -> (chunks of per-cell updates, any number per frame) ->
// (finish: post alive mask, swap). Rendering always reads the last COMPLETE state, so a half-done step never shows.
// Plain browser JS, no modules: defines window.NcaCreature (and module.exports under Node).
// Built by build_nca_creature.py from nca_creature.src.js - edit the .src.js, not the built file.
// ===================================================================================================================
(function (root) {
  'use strict';
  const NCA_WEIGHTS = /*__NCA_WEIGHTS__*/null;

  // prism palette (Tools/Ecology/flight/src/60_stakes.js ELEMENT_COLOUR)
  const ELEMENT_COLOUR = { mass: [0.98, 0.42, 0.30], charge: [1.0, 0.84, 0.25], space: [0.42, 0.62, 1.0], time: [0.62, 1.0, 0.55] };

  // ---- weights: raw weights.json object (nested arrays) or the embedded packed form ------------------------------
  function b64bytes(s) {
    if (typeof atob === 'function') { const b = atob(s), u = new Uint8Array(b.length); for (let i = 0; i < b.length; i++) u[i] = b.charCodeAt(i); return u; }
    return new Uint8Array(Buffer.from(s, 'base64'));
  }
  function f16(h) {
    const s = h & 0x8000 ? -1 : 1, e = (h >> 10) & 31, m = h & 1023;
    if (e === 0) return s * m * 5.960464477539063e-8;
    if (e === 31) return m ? NaN : s * Infinity;
    return s * (1 + m / 1024) * Math.pow(2, e - 15);
  }
  function unpack(p) {
    const out = {};
    for (const k of ['w1', 'b1', 'w2', 'b2']) {
      const t = p.tensors[k], u = b64bytes(t.data), n = t.n, a = new Float32Array(n);
      if (t.fmt === 'f16') { const v = new DataView(u.buffer, u.byteOffset, u.byteLength); for (let i = 0; i < n; i++) a[i] = f16(v.getUint16(2 * i, true)); }
      else if (t.fmt === 'i8') {          // per-row symmetric int8: rows of t.row values, scale per row
        const rows = n / t.row; for (let r = 0; r < rows; r++) for (let j = 0; j < t.row; j++) { const q = u[r * t.row + j]; a[r * t.row + j] = (q > 127 ? q - 256 : q) * t.scale[r]; }
      } else { const v = new DataView(u.buffer, u.byteOffset, u.byteLength); for (let i = 0; i < n; i++) a[i] = v.getFloat32(4 * i, true); }
      out[k] = a;
    }
    return out;
  }
  function loadWeights(w) {
    if (!w) throw new Error('NcaCreature: no weights (pass {weights} or use the built nca_creature.js)');
    const meta = { C: w.channel_n, HID: w.hidden, fireRate: w.fire_rate, D: w.D, H: w.H, W: w.W, period: w.period || 8, frames: w.frames || 8 };
    const t = w.tensors ? unpack(w) : { w1: new Float32Array(w.w1.flat()), b1: new Float32Array(w.b1), w2: new Float32Array(w.w2.flat()), b2: new Float32Array(w.b2) };
    return Object.assign(meta, t);
  }
  let cachedEmbedded = null;

  function mulberry32(a) {
    return function () { a |= 0; a = (a + 0x6D2B79F5) | 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
  }
  const now = (typeof performance !== 'undefined' && performance.now) ? () => performance.now() : () => Date.now();

  // ---- the creature --------------------------------------------------------------------------------------------
  class NcaCreature {
    /**
     * opts: seed (int, fire-mask RNG), scale (world units per voxel, default 0.5), position [x,y,z] or {x,y,z},
     * yaw (radians about world Y), element ('mass'|'charge'|'space'|'time'), tint (0..1, how far the colour leans
     * from the NCA's own rgb to the element colour, default 0.65), stepsPerSecond (target NCA step rate, default 30;
     * the swim cycle is ~68 steps), budgetMs (default 4 ms of NCA work per step() call), cellsPerFrame (hard cap on
     * cell updates per call, overrides budgetMs when set), alphaThreshold (voxel shown when alpha > this, default
     * 0.3), respawn (reseed if every cell dies, default true), weights (a weights.json object; default embedded).
     */
    constructor(opts) {
      opts = opts || {};
      const w = opts.weights ? loadWeights(opts.weights) : (cachedEmbedded || (cachedEmbedded = loadWeights(NCA_WEIGHTS)));
      Object.assign(this, { C: w.C, HID: w.HID, D: w.D, H: w.H, W: w.W, period: w.period, frames: w.frames });
      this._w = w;
      this.fireRate = opts.fireRate ?? w.fireRate;
      this.scale = opts.scale ?? 0.5;
      const p = opts.position || [0, 0, 0];
      this.position = Array.isArray(p) ? p.slice() : [p.x || 0, p.y || 0, p.z || 0];
      this.yaw = opts.yaw || 0;
      this.element = opts.element || 'space';
      this.tint = opts.tint ?? 0.65;
      this.stepsPerSecond = opts.stepsPerSecond ?? 30;
      this.budgetMs = opts.budgetMs ?? 4;
      this.cellsPerFrame = opts.cellsPerFrame ?? null;
      this.alphaThreshold = opts.alphaThreshold ?? 0.3;
      this.respawn = opts.respawn ?? true;
      this.rand = mulberry32((opts.seed ?? 1) >>> 0 || 1);

      const N = this.D * this.H * this.W, C = this.C;
      this.N = N;
      this.s = new Float32Array(N * C); this.ns = new Float32Array(N * C);
      this.pre = new Uint8Array(N); this.post = new Uint8Array(N);
      this._ta = new Float32Array(N); this._tb = new Float32Array(N);
      this._list = new Int32Array(N); this._listN = 0; this._cursor = 0; this._inStep = false;
      this._feat = new Float32Array(4 * C); this._hid = new Float32Array(this.HID);
      // [27][4] stencil (identity, sobel x, y, z), neighbour order dz, dy, dx - exactly nca3d_core.js
      const K = new Float32Array(27 * 4), sm = [1, 2, 1], df = [-1, 0, 1], e = [0, 1, 0];
      for (let z = 0, n = 0; z < 3; z++) for (let y = 0; y < 3; y++) for (let x = 0; x < 3; x++, n++) {
        K[n * 4] = e[z] * e[y] * e[x]; K[n * 4 + 1] = sm[z] * sm[y] * df[x] / 32;
        K[n * 4 + 2] = sm[z] * df[y] * sm[x] / 32; K[n * 4 + 3] = df[z] * sm[y] * sm[x] / 32;
      }
      this._K = K;
      this.steps = 0;          // completed NCA steps
      this.cellUpdates = 0;    // total per-cell network evaluations
      this.lastStepMs = 0;     // wall time spent in the last step() call
      this.respawns = 0;
      this._debt = 0;
      this._mesh = null;
      this.reset();
    }

    /** back to a single seed cell (alpha + hidden channels = 1 at the grid centre), as nca3d.make_seed */
    reset() {
      const { C, D, H, W } = this;
      this.s.fill(0); this.ns.fill(0); this._inStep = false; this._listN = 0; this._cursor = 0;
      const i = ((D >> 1) * H + (H >> 1)) * W + (W >> 1);
      for (let c = 3; c < C; c++) this.s[i * C + c] = 1;
      this._aliveMask(this.s, this.pre);
    }

    _aliveMask(st, out) {   // separable 3x3x3 max of alpha > 0.1
      const { C, D, H, W, N } = this, A = this._ta, B = this._tb;
      for (let i = 0; i < N; i++) A[i] = st[i * C + 3];
      for (let z = 0; z < D; z++) for (let y = 0; y < H; y++) { const r = (z * H + y) * W;
        for (let x = 0; x < W; x++) { let m = A[r + x]; if (x > 0 && A[r + x - 1] > m) m = A[r + x - 1]; if (x < W - 1 && A[r + x + 1] > m) m = A[r + x + 1]; B[r + x] = m; } }
      for (let z = 0; z < D; z++) for (let y = 0; y < H; y++) { const r = (z * H + y) * W;
        for (let x = 0; x < W; x++) { let m = B[r + x]; if (y > 0 && B[r - W + x] > m) m = B[r - W + x]; if (y < H - 1 && B[r + W + x] > m) m = B[r + W + x]; A[r + x] = m; } }
      const HW = H * W;
      for (let i = 0; i < N; i++) { const z = (i / HW) | 0; let m = A[i]; if (z > 0 && A[i - HW] > m) m = A[i - HW]; if (z < D - 1 && A[i + HW] > m) m = A[i + HW]; out[i] = m > 0.1 ? 1 : 0; }
    }

    _begin() {
      // pre-mask of s: equal to the post-mask of the previous step unless hit() intervened; recompute to be exact
      this._aliveMask(this.s, this.pre);
      this.ns.set(this.s);
      let n = 0; const pre = this.pre, L = this._list;
      for (let i = 0; i < this.N; i++) if (pre[i]) L[n++] = i;
      this._listN = n; this._cursor = 0; this._inStep = true;
    }

    _update(i) {   // one cell's residual update from s into ns (nca3d_core.js step(), per cell)
      const { C, H, W, D, HID } = this, F = 4 * C, s = this.s, pre = this.pre, K = this._K, feat = this._feat, hid = this._hid;
      const { w1, b1, w2, b2 } = this._w;
      const HW = H * W, z = (i / HW) | 0, y = ((i - z * HW) / W) | 0, x = i - z * HW - y * W;
      feat.fill(0);
      let n = 0;
      for (let dz = -1; dz <= 1; dz++) for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++, n++) {
        const zz = z + dz, yy = y + dy, xx = x + dx;
        if (zz < 0 || zz >= D || yy < 0 || yy >= H || xx < 0 || xx >= W) continue;
        const j = (zz * H + yy) * W + xx;
        if (!pre[j]) continue;              // outside the alive mask the state is exactly zero
        const o = j * C, k0 = K[n * 4], k1 = K[n * 4 + 1], k2 = K[n * 4 + 2], k3 = K[n * 4 + 3];
        for (let c = 0; c < C; c++) {
          const v = s[o + c]; if (v === 0) continue;
          feat[4 * c] += k0 * v; feat[4 * c + 1] += k1 * v; feat[4 * c + 2] += k2 * v; feat[4 * c + 3] += k3 * v;
        }
      }
      for (let h = 0; h < HID; h++) {
        let a = b1[h]; const o = h * F;
        for (let f = 0; f < F; f++) a += w1[o + f] * feat[f];
        hid[h] = a > 0 ? a : 0;
      }
      const ns = this.ns, base = i * C;
      for (let c = 0; c < C; c++) {
        let a = b2[c]; const o = c * HID;
        for (let h = 0; h < HID; h++) a += w2[o + h] * hid[h];
        ns[base + c] += a;
      }
      this.cellUpdates++;
    }

    _finish() {
      const C = this.C, ns = this.ns, pre = this.pre, post = this.post;
      this._aliveMask(ns, post);
      for (let i = 0; i < this.N; i++) if (!(pre[i] && post[i])) ns.fill(0, i * C, i * C + C);
      const t = this.s; this.s = ns; this.ns = t;
      this._inStep = false; this.steps++;
      if (this.respawn && this._listN === 0) { this.respawns++; this.reset(); }
    }

    /** Work on the NCA for at most budgetMs (or cellsPerFrame cell updates), resuming wherever the last call stopped.
     *  dt (seconds) feeds the step clock: no more than stepsPerSecond NCA steps run per second of dt. Returns the
     *  number of NCA steps completed in this call. dt <= 0 or omitted -> just spend the budget. */
    step(dt) {
      const t0 = now();
      if (dt > 0) this._debt = Math.min(this._debt + dt * this.stepsPerSecond, 3); else if (dt === undefined) this._debt = Math.max(this._debt, 1);
      const capCells = this.cellsPerFrame, budget = this.budgetMs;
      let done = 0, cells = 0;
      while (this._debt > 0 || this._inStep) {
        if (!this._inStep) { if (capCells == null && done > 0 && now() - t0 >= 0.5 * budget) break; this._begin(); }
        const L = this._list, rate = this.fireRate, rnd = this.rand;
        while (this._cursor < this._listN) {
          const i = L[this._cursor++];
          if (rnd() <= rate) { this._update(i); cells++; }
          if (capCells != null ? cells >= capCells : ((cells & 7) === 0 && cells && now() - t0 >= budget)) break;
        }
        if (this._cursor < this._listN) break;            // out of budget mid-step: resume next call
        this._finish(); done++; this._debt -= 1;
        if (capCells != null ? cells >= capCells : now() - t0 >= budget) break;
      }
      this.lastStepMs = now() - t0;
      return done;
    }

    /** Run n whole NCA steps synchronously (blocking; use before the creature is shown). */
    grow(n) {
      for (let k = 0; k < n; k++) {
        if (!this._inStep) { if (capCells == null && done > 0 && now() - t0 >= 0.5 * budget) break; this._begin(); }
        const L = this._list, rate = this.fireRate, rnd = this.rand;
        while (this._cursor < this._listN) { const i = L[this._cursor++]; if (rnd() <= rate) this._update(i); }
        this._finish();
      }
      return this;
    }

    /** grid (z, y, x) -> world [X, Y, Z]. The lizard lies in the grid's H-W plane -> world X-Z; grid depth -> world Y. */
    gridToWorld(z, y, x, out) {
      const s = this.scale, lx = (x + 0.5 - this.W / 2) * s, ly = (z + 0.5 - this.D / 2) * s, lz = (y + 0.5 - this.H / 2) * s;
      const c = Math.cos(this.yaw), sn = Math.sin(this.yaw);
      out = out || [0, 0, 0];
      out[0] = this.position[0] + c * lx + sn * lz; out[1] = this.position[1] + ly; out[2] = this.position[2] - sn * lx + c * lz;
      return out;
    }
    worldToGrid(p) {
      const P = Array.isArray(p) ? p : [p.x, p.y, p.z], s = this.scale;
      const dx = P[0] - this.position[0], dy = P[1] - this.position[1], dz = P[2] - this.position[2];
      const c = Math.cos(this.yaw), sn = Math.sin(this.yaw), lx = c * dx - sn * dz, lz = sn * dx + c * dz;
      return [dy / s + this.D / 2 - 0.5, lz / s + this.H / 2 - 0.5, lx / s + this.W / 2 - 0.5];   // z, y, x
    }

    /** Erase every cell within `radius` (world units) of `point` (zero all 16 channels, in the shown state and in a
     *  step in flight). The surviving cells regrow the hole. Returns the number of VISIBLE voxels removed. */
    hit(point, radius) {
      const [gz, gy, gx] = this.worldToGrid(point), r = radius / this.scale, r2 = r * r, { C, D, H, W } = this, thr = this.alphaThreshold;
      let removed = 0;
      for (let z = Math.max(0, Math.floor(gz - r)); z <= Math.min(D - 1, Math.ceil(gz + r)); z++)
        for (let y = Math.max(0, Math.floor(gy - r)); y <= Math.min(H - 1, Math.ceil(gy + r)); y++)
          for (let x = Math.max(0, Math.floor(gx - r)); x <= Math.min(W - 1, Math.ceil(gx + r)); x++) {
            if ((z - gz) ** 2 + (y - gy) ** 2 + (x - gx) ** 2 > r2) continue;
            const i = (z * H + y) * W + x;
            if (this.s[i * C + 3] > thr) removed++;
            this.s.fill(0, i * C, i * C + C); this.ns.fill(0, i * C, i * C + C);
            this.pre[i] = 0;            // a step in flight must not write into (or read from) the wound
          }
      this.hits = (this.hits || 0) + 1;
      return removed;
    }

    /** Bounding sphere in world space of the live voxels (cheap: from the grid centre and the grid extent). */
    get radius() { return 0.5 * this.scale * Math.hypot(this.W, this.H, this.D); }

    /** Count of voxels with alpha > alphaThreshold. */
    count() { const C = this.C, s = this.s, thr = this.alphaThreshold; let n = 0; for (let i = 0; i < this.N; i++) if (s[i * C + 3] > thr) n++; return n; }

    _colour(r, g, b, a, out) {
      // NCA rgb is premultiplied: un-premultiply, then lean toward the element colour while keeping the lizard's own
      // light/dark pattern (its luminance modulates the element colour) and some of its hue.
      const ia = 1 / Math.max(a, 1e-3), R = Math.min(1, Math.max(0, r * ia)), G = Math.min(1, Math.max(0, g * ia)), B = Math.min(1, Math.max(0, b * ia));
      const E = ELEMENT_COLOUR[this.element] || ELEMENT_COLOUR.space, t = this.tint;
      const lum = Math.min(1, (0.6 * R + 0.9 * G + 0.5 * B) / 0.75);     // ~1 on the lizard's bright green, lower on dark marks
      const k = 0.35 + 0.8 * lum;
      out[0] = Math.min(1, (1 - t) * R + t * E[0] * k); out[1] = Math.min(1, (1 - t) * G + t * E[1] * k); out[2] = Math.min(1, (1 - t) * B + t * E[2] * k);
      return out;
    }

    /** Live voxels: { n, pos: Float32Array(3n) world positions, rgba: Float32Array(4n) (rgb in [0,1], a = NCA alpha) }.
     *  Reuses its buffers between calls. */
    voxels() {
      const { C, D, H, W } = this, s = this.s, thr = this.alphaThreshold;
      if (!this._vox) this._vox = { n: 0, pos: new Float32Array(this.N * 3), rgba: new Float32Array(this.N * 4) };
      const v = this._vox, tmp = [0, 0, 0], col = [0, 0, 0];
      let n = 0;
      for (let z = 0; z < D; z++) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const o = ((z * H + y) * W + x) * C, a = s[o + 3];
        if (a <= thr) continue;
        this.gridToWorld(z, y, x, tmp); v.pos[3 * n] = tmp[0]; v.pos[3 * n + 1] = tmp[1]; v.pos[3 * n + 2] = tmp[2];
        this._colour(s[o], s[o + 1], s[o + 2], a, col);
        v.rgba[4 * n] = col[0]; v.rgba[4 * n + 1] = col[1]; v.rgba[4 * n + 2] = col[2]; v.rgba[4 * n + 3] = Math.min(1, a);
        n++;
      }
      v.n = n;
      return v;
    }

    /** Optional three.js helper: an InstancedMesh of small triangular prisms (pointing along the body), refreshed by
     *  sync(). capacity defaults to 4096 (the grown lizard shows ~1000 voxels). */
    mesh(THREE, opts) {
      if (this._mesh) return this._mesh;
      opts = opts || {};
      const cap = opts.capacity || 4096, sz = this.scale * (opts.voxelSize ?? 1.15);
      const geo = new THREE.CylinderGeometry(sz * 0.62, sz * 0.62, sz, 3, 1);   // a 3-sided cylinder = triangular prism
      const mat = opts.material || new THREE.MeshStandardMaterial({ roughness: 0.45, metalness: 0.1, flatShading: true });
      const m = new THREE.InstancedMesh(geo, mat, cap);
      m.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      m.setColorAt(0, new THREE.Color(1, 1, 1));
      m.frustumCulled = false; m.count = 0; m.name = 'NcaCreature';
      this._mesh = m; this._THREE = THREE; this._m4 = new THREE.Matrix4(); this._q = new THREE.Quaternion(); this._v3 = new THREE.Vector3(); this._s3 = new THREE.Vector3(); this._c = new THREE.Color();
      this._q.setFromAxisAngle(new THREE.Vector3(0, 1, 0), this.yaw);
      this.sync();
      return m;
    }

    /** Push the current live voxels into the InstancedMesh (no-op without mesh()). Voxel size follows alpha, so the
     *  regrowing edge of a wound fades in instead of popping. */
    sync() {
      const m = this._mesh; if (!m) return 0;
      const v = this.voxels(), n = Math.min(v.n, m.instanceMatrix.count), M = this._m4, P = this._v3, S = this._s3, col = this._c;
      this._q.setFromAxisAngle(this._v3.set(0, 1, 0), this.yaw);
      for (let i = 0; i < n; i++) {
        const a = v.rgba[4 * i + 3], k = 0.55 + 0.45 * Math.min(1, a);
        P.set(v.pos[3 * i], v.pos[3 * i + 1], v.pos[3 * i + 2]); S.set(k, k, k);
        M.compose(P, this._q, S); m.setMatrixAt(i, M);
        col.setRGB(v.rgba[4 * i], v.rgba[4 * i + 1], v.rgba[4 * i + 2]); m.setColorAt(i, col);
      }
      m.count = n;
      m.instanceMatrix.needsUpdate = true; if (m.instanceColor) m.instanceColor.needsUpdate = true;
      return n;
    }

    dispose() { if (this._mesh) { this._mesh.geometry.dispose(); this._mesh.material.dispose && this._mesh.material.dispose(); this._mesh = null; } }
  }
  NcaCreature.ELEMENT_COLOUR = ELEMENT_COLOUR;
  NcaCreature.loadWeights = loadWeights;
  NcaCreature.hasEmbeddedWeights = !!NCA_WEIGHTS;

  if (typeof module !== 'undefined' && module.exports) module.exports = NcaCreature;
  if (root) root.NcaCreature = NcaCreature;
})(typeof window !== 'undefined' ? window : (typeof globalThis !== 'undefined' ? globalThis : this));
