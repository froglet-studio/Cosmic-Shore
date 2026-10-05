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
// Speed (grown lizard, ~3.5k cells in the alive mask, ~1.6k fire per step): the step is sparse (only alive-mask cells
// are visited; bit-identical to the dense step) and the per-cell network runs in a 3.6 KB WebAssembly SIMD kernel
// (nca_kernel.c, embedded; 4 cells per weight load): ~2.6 ms/step vs 33.5 ms for the old dense JS step, so a 4 ms
// frame budget gives ~90 steps/s. Without WebAssembly SIMD (or with {wasm: false}) a batched JS path runs (~19 ms).
// c.backend says which ('wasm-simd' | 'js').
// Plain browser JS, no modules: defines window.NcaCreature (and module.exports under Node).
// Built by build_nca_creature.py from nca_creature.src.js - edit the .src.js, not the built file.
// ===================================================================================================================
(function (root) {
  'use strict';
  const NCA_WEIGHTS = /*__NCA_WEIGHTS__*/null;
  const NCA_WASM = /*__NCA_WASM__*/null;     // { b64, heapBase }: nca_kernel.c compiled to WebAssembly SIMD

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
     * 0.3), respawn (reseed if every cell dies, default true), weights (a weights.json object; default embedded), wasm (default true: use the SIMD kernel when available).
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
      this.post = new Uint8Array(N); this._postList = new Int32Array(N); this._postN = 0;
      this._list = new Int32Array(N); this._listN = 0; this._cursor = 0; this._inStep = false;
      // sparse bookkeeping: _keep = cells that may be non-zero in s (sorted), _keepOld = the same for ns (stale)
      this._keep = new Int32Array(N); this._keepN = 0; this._keepOld = new Int32Array(N); this._keepOldN = 0;
      this._qn = 0;
      this._feat = new Float32Array(16 * C); this._hid = new Float32Array(4 * this.HID);
      // [27][4] stencil (identity, sobel x, y, z), neighbour order dz, dy, dx - exactly nca3d_core.js
      const K = new Float32Array(27 * 4), sm = [1, 2, 1], df = [-1, 0, 1], e = [0, 1, 0];
      for (let z = 0, n = 0; z < 3; z++) for (let y = 0; y < 3; y++) for (let x = 0; x < 3; x++, n++) {
        K[n * 4] = e[z] * e[y] * e[x]; K[n * 4 + 1] = sm[z] * sm[y] * df[x] / 32;
        K[n * 4 + 2] = sm[z] * df[y] * sm[x] / 32; K[n * 4 + 3] = df[z] * sm[y] * sm[x] / 32;
      }
      this._K = K;
      // backend: WebAssembly SIMD kernel when available (opts.wasm !== false), else the batched JS path
      this._wasm = (opts.wasm ?? true) && C === 16 && this.HID === 128 ? NcaCreature._wasmModule() : null;
      if (this._wasm) this._initWasm(); else {
        this.s = new Float32Array(N * C); this.ns = new Float32Array(N * C); this.pre = new Uint8Array(N);
        this._q = new Int32Array(4); this._qcap = 4;
      }
      this.backend = this._wasm ? 'wasm-simd' : 'js';
      this.steps = 0;          // completed NCA steps
      this.cellUpdates = 0;    // total per-cell network evaluations
      this.lastStepMs = 0;     // wall time spent in the last step() call
      this.respawns = 0;
      this._debt = 0;
      this._mesh = null;
      this.reset();
    }

    _initWasm() {
      // one WebAssembly.Memory per creature: weights re-laid-out for the SIMD kernel, cell queue, pre mask, s, ns
      const { C, HID, N } = this, F = 4 * C, w = this._w, mod = this._wasm;
      let off = (mod.heapBase + 15) & ~15;
      const take = (bytes) => { const p = off; off = (off + bytes + 15) & ~15; return p; };
      const pw1 = take(4 * F * HID), pb1 = take(4 * HID), pw2 = take(4 * HID * C), pb2 = take(4 * C), pK = take(4 * 108), pd = take(12),
        pq = take(4 * N), ppre = take(N), ps = take(4 * N * C), pns = take(4 * N * C);
      const mem = new WebAssembly.Memory({ initial: Math.ceil(off / 65536) + 1 });
      const inst = new WebAssembly.Instance(mod.module, { env: { memory: mem } }), buf = mem.buffer;
      const w1t = new Float32Array(buf, pw1, F * HID), w2t = new Float32Array(buf, pw2, HID * C);
      for (let h = 0; h < HID; h++) for (let c = 0; c < C; c++) for (let k = 0; k < 4; k++) w1t[(k * C + c) * HID + h] = w.w1[h * F + 4 * c + k];
      for (let c = 0; c < C; c++) for (let h = 0; h < HID; h++) w2t[h * C + c] = w.w2[c * HID + h];
      new Float32Array(buf, pb1, HID).set(w.b1); new Float32Array(buf, pb2, C).set(w.b2);
      new Float32Array(buf, pK, 108).set(this._K); new Int32Array(buf, pd, 3).set([this.D, this.H, this.W]);
      this._mem = mem; this._kern = inst.exports.nca_cells;
      Object.assign(this, { _w1tPtr: pw1, _b1Ptr: pb1, _w2tPtr: pw2, _b2Ptr: pb2, _KPtr: pK, _dimsPtr: pd, _qPtr: pq, _prePtr: ppre, _sPtr: ps, _nsPtr: pns });
      this._q = new Int32Array(buf, pq, N); this._qcap = 32;
      this.pre = new Uint8Array(buf, ppre, N); this.s = new Float32Array(buf, ps, N * C); this.ns = new Float32Array(buf, pns, N * C);
    }

    /** back to a single seed cell (alpha + hidden channels = 1 at the grid centre), as nca3d.make_seed */
    /** back to a single seed cell (alpha + hidden channels = 1 at the grid centre), as nca3d.make_seed */
    reset() {
      const { C, D, H, W } = this;
      this.s.fill(0); this.ns.fill(0); this.pre.fill(0); this.post.fill(0);
      this._inStep = false; this._listN = 0; this._cursor = 0; this._postN = 0; this._qn = 0; this._keepOldN = 0;
      const i = ((D >> 1) * H + (H >> 1)) * W + (W >> 1);
      for (let c = 3; c < C; c++) this.s[i * C + c] = 1;
      this._keep[0] = i; this._keepN = 1;
    }

    // Mark the 3x3x3 dilation of {cells of `list` with alpha > 0.1} in `mask`, appending newly marked cells to `out`.
    // Equals the dense separable 3x3x3 max-pool of nca3d_core.js because every cell outside `list` is exactly zero.
    _dilate(st, list, n, mask, out) {
      const { C, D, H, W } = this, HW = H * W;
      let m = 0;
      for (let k = 0; k < n; k++) {
        const i = list[k]; if (!(st[i * C + 3] > 0.1)) continue;
        const z = (i / HW) | 0, y = ((i - z * HW) / W) | 0, x = i - z * HW - y * W;
        const z0 = z > 0 ? z - 1 : 0, z1 = z < D - 1 ? z + 1 : z, y0 = y > 0 ? y - 1 : 0, y1 = y < H - 1 ? y + 1 : y, x0 = x > 0 ? x - 1 : 0, x1 = x < W - 1 ? x + 1 : x;
        for (let zz = z0; zz <= z1; zz++) for (let yy = y0; yy <= y1; yy++) { const r = (zz * H + yy) * W;
          for (let xx = x0; xx <= x1; xx++) { const j = r + xx; if (!mask[j]) { mask[j] = 1; out[m++] = j; } } }
      }
      return m;
    }

    _begin() {
      // Sparse active set: only cells in the alive mask (3x3x3 dilation of alpha > 0.1, ~3.5k of 42.6k for the grown
      // lizard) are touched. Every other cell is exactly zero before and after the step (alive masking), so this is
      // the dense step of nca3d_core.js bit for bit, without the dense mask passes and full-grid copies.
      const C = this.C, s = this.s, ns = this.ns, pre = this.pre, L = this._list;
      for (let k = 0; k < this._listN; k++) pre[L[k]] = 0;
      const n = this._dilate(s, this._keep, this._keepN, pre, L);
      L.subarray(0, n).sort();                               // row-major order: same fire-mask RNG sequence as dense
      const KO = this._keepOld;                              // ns still holds the state of two steps ago: clear it
      for (let k = 0, m = this._keepOldN; k < m; k++) { const o = KO[k] * C; for (let c = 0; c < C; c++) ns[o + c] = 0; }
      for (let k = 0; k < n; k++) { const o = L[k] * C; for (let c = 0; c < C; c++) ns[o + c] = s[o + c]; }
      this._keepOldN = 0;
      this._listN = n; this._cursor = 0; this._inStep = true;
    }

    // Cell updates are queued and evaluated 4 at a time: every weight loaded once feeds 4 accumulators (register
    // blocking), which is ~4x less memory traffic than one cell at a time. Cells in a step are independent (they read
    // s and write only their own ns row), so batching changes nothing but speed; per-cell accumulation order is the
    // same as nca3d_core.js, so results are bitwise identical to the unbatched step.
    _push(i) { this._q[this._qn++] = i; if (this._qn === this._qcap) this._flush(); }

    _flush() {
      const nb = this._qn; if (nb === 0) return;
      this._qn = 0;
      if (this._wasm) { this._kern(this._sPtr, this._nsPtr, this._prePtr, this._qPtr, nb, this._w1tPtr, this._b1Ptr, this._w2tPtr, this._b2Ptr, this._KPtr, this._dimsPtr); this.cellUpdates += nb; return; }
      const { C, H, W, D, HID } = this, F = 4 * C, s = this.s, pre = this.pre, K = this._K, X = this._feat, Hd = this._hid, q = this._q;
      const { w1, b1, w2, b2 } = this._w, HW = H * W;
      X.fill(0);
      for (let b = 0; b < nb; b++) {           // perception: X[f*4 + b], f = 4*channel + {id, sx, sy, sz}
        const i = q[b], z = (i / HW) | 0, y = ((i - z * HW) / W) | 0, x = i - z * HW - y * W;
        let n = 0;
        for (let dz = -1; dz <= 1; dz++) for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++, n++) {
          const zz = z + dz, yy = y + dy, xx = x + dx;
          if (zz < 0 || zz >= D || yy < 0 || yy >= H || xx < 0 || xx >= W) continue;
          const j = (zz * H + yy) * W + xx;
          if (!pre[j]) continue;              // outside the alive mask the state is exactly zero
          const o = j * C, k0 = K[n * 4], k1 = K[n * 4 + 1], k2 = K[n * 4 + 2], k3 = K[n * 4 + 3];
          for (let c = 0, p = b; c < C; c++, p += 16) {
            const v = s[o + c]; if (v === 0) continue;
            X[p] += k0 * v; X[p + 4] += k1 * v; X[p + 8] += k2 * v; X[p + 12] += k3 * v;
          }
        }
      }
      for (let h = 0; h < HID; h++) {           // layer 1 (64 -> 128, ReLU), 4 cells per weight load
        const bh = b1[h], o = h * F; let a0 = bh, a1 = bh, a2 = bh, a3 = bh;
        for (let f = 0, p = 0; f < F; f++, p += 4) { const w = w1[o + f]; a0 += w * X[p]; a1 += w * X[p + 1]; a2 += w * X[p + 2]; a3 += w * X[p + 3]; }
        const r = 4 * h; Hd[r] = a0 > 0 ? a0 : 0; Hd[r + 1] = a1 > 0 ? a1 : 0; Hd[r + 2] = a2 > 0 ? a2 : 0; Hd[r + 3] = a3 > 0 ? a3 : 0;
      }
      const ns = this.ns, o0 = q[0] * C, o1 = q[1] * C, o2 = q[2] * C, o3 = q[3] * C;
      for (let c = 0; c < C; c++) {             // layer 2 (128 -> 16), residual add into ns
        const bc = b2[c], o = c * HID; let a0 = bc, a1 = bc, a2 = bc, a3 = bc;
        for (let h = 0, p = 0; h < HID; h++, p += 4) { const w = w2[o + h]; a0 += w * Hd[p]; a1 += w * Hd[p + 1]; a2 += w * Hd[p + 2]; a3 += w * Hd[p + 3]; }
        ns[o0 + c] += a0; if (nb > 1) ns[o1 + c] += a1; if (nb > 2) ns[o2 + c] += a2; if (nb > 3) ns[o3 + c] += a3;
      }
      this.cellUpdates += nb;
    }

    _finish() {
      const C = this.C, ns = this.ns, pre = this.pre, post = this.post, L = this._list, n = this._listN, PL = this._postList;
      for (let k = 0; k < this._postN; k++) post[PL[k]] = 0;
      this._postN = this._dilate(ns, L, n, post, PL);          // ns is non-zero only on L
      const KN = this._keepOld; let m = 0;                    // survivors (pre & post) = the new state's keep list
      for (let k = 0; k < n; k++) {
        const i = L[k];
        if (pre[i] && post[i]) KN[m++] = i; else { const o = i * C; for (let c = 0; c < C; c++) ns[o + c] = 0; }
      }
      this._keepOld = this._keep; this._keepOldN = this._keepN; this._keep = KN; this._keepN = m;
      const t = this.s; this.s = ns; this.ns = t;
      if (this._wasm) { const p = this._sPtr; this._sPtr = this._nsPtr; this._nsPtr = p; }
      this._inStep = false; this.steps++;
      if (this.respawn && n === 0) { this.respawns++; this.reset(); }
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
        if (!this._inStep) { if (capCells == null && done > 0 && now() - t0 >= budget) break; this._begin(); }
        const L = this._list, rate = this.fireRate, rnd = this.rand;
        while (this._cursor < this._listN) {
          const i = L[this._cursor++];
          if (rnd() <= rate) { this._push(i); cells++; }
          if (capCells != null ? cells >= capCells : ((cells & 7) === 0 && cells && now() - t0 >= budget)) break;
        }
        this._flush();
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
        if (!this._inStep) this._begin();
        const L = this._list, rate = this.fireRate, rnd = this.rand;
        while (this._cursor < this._listN) { const i = L[this._cursor++]; if (rnd() <= rate) this._push(i); }
        this._flush();
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
    count() { const C = this.C, s = this.s, thr = this.alphaThreshold, K = this._keep; let n = 0; for (let k = 0; k < this._keepN; k++) if (s[K[k] * C + 3] > thr) n++; return n; }

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
      let n = 0; const K = this._keep, HW = H * W;
      for (let k = 0; k < this._keepN; k++) {              // keep list is sorted: same order as a z, y, x scan
        const i = K[k], o = i * C, a = s[o + 3];
        const z = (i / HW) | 0, y = ((i - z * HW) / W) | 0, x = i - z * HW - y * W;
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
  let wasmCache;   // undefined: not tried yet; null: unavailable (no WebAssembly, no SIMD, or not embedded)
  NcaCreature._wasmModule = function () {
    if (wasmCache !== undefined) return wasmCache;
    wasmCache = null;
    try {
      if (NCA_WASM && typeof WebAssembly === 'object') {
        const bytes = b64bytes(NCA_WASM.b64);
        if (WebAssembly.validate(bytes)) wasmCache = { module: new WebAssembly.Module(bytes), heapBase: NCA_WASM.heapBase };
      }
    } catch (e) { wasmCache = null; }
    return wasmCache;
  };
  NcaCreature.ELEMENT_COLOUR = ELEMENT_COLOUR;
  NcaCreature.loadWeights = loadWeights;
  NcaCreature.hasEmbeddedWeights = !!NCA_WEIGHTS;

  if (typeof module !== 'undefined' && module.exports) module.exports = NcaCreature;
  if (root) root.NcaCreature = NcaCreature;
})(typeof window !== 'undefined' ? window : (typeof globalThis !== 'undefined' ? globalThis : this));
