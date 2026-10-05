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
      // body frame (lizard3d_swim, measured over the 8 trained poses in frames.npy): the body's long axis in the grid's
      // (W, H) = local (x, z) plane points head-first along `forward`; its centroid sits `offset` voxels off the grid
      // centre, so position is the body's centre and yaw/pitch/roll turn it about its own axes.
      const fw = opts.forward || [-0.6148, -0.7887], fn = Math.hypot(fw[0], fw[1]) || 1;
      this.forward = [fw[0] / fn, fw[1] / fn];
      this.offset = opts.offset || [-1.39, -0.72];
      this.pitch = opts.pitch || 0;           // nose up (radians, about the body's side axis)
      this.roll = opts.roll || 0;             // bank (radians, about the body's long axis)
      this.smooth = opts.smooth ?? true;      // render interpolates between the last two complete states
      this.surface = opts.surface ?? true;    // render only voxels with an exposed face
      this.scales = opts.scales ?? true;      // prisms lie along the skin (normal = -grad alpha), pointing tail-ward
      this.maxMsPerSecond = opts.maxMsPerSecond ?? 150;   // adaptive rate: at most this much NCA work per second
      this.minStepsPerSecond = opts.minStepsPerSecond ?? 4;
      this.msPerStep = 0;                      // EMA of wall ms per completed step (feeds the adaptive rate)
      this.rate = this.stepsPerSecond;         // the step rate actually targeted (stepsPerSecond, lowered when slow)
      this.achievedRate = 0;                   // EMA of completed steps per second of dt
      this.debris = []; this.wounds = []; this.flash = 0;
      this.bend = 0; this.bendPrev = 0; this.stroke = 0;   // tail-vs-head lateral offset (voxels) and its per-step change

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
      // render snapshots of the last two complete states: rgb (un-premultiplied NCA colour) + alpha per cell
      this._rc = new Float32Array(N * 4); this._rp = new Float32Array(N * 4);
      this._rcL = new Int32Array(N); this._rcN = 0; this._rpL = new Int32Array(N); this._rpN = 0;
      this._since = 0; this._interval = 1 / Math.max(1, this.stepsPerSecond); this._sinceFinish = 0; this._accMs = 0;
      this._strokeAvg = 0;
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
      if (this._rc) { this._rc.fill(0); this._rp.fill(0); this._rcN = this._rpN = 0; this.bend = this.bendPrev = this.stroke = 0; }
    }

    // Snapshot the state that just completed for rendering: the previous snapshot becomes `_rp`, the new one `_rc`.
    // Also measures the swim: `bend` = mean lateral offset of the tail third minus that of the head third (voxels,
    // across the body axis); its change per step (`stroke`) is the tail beat that drives swim().
    _snap() {
      const { C, H, W } = this, s = this.s, HW = H * W, K = this._keep, n = this._keepN, thr = this.alphaThreshold;
      const old = this._rp, oL = this._rpL; for (let k = 0; k < this._rpN; k++) { const o = 4 * oL[k]; old[o] = old[o + 1] = old[o + 2] = old[o + 3] = 0; }
      this._rp = this._rc; this._rpL = this._rcL; this._rpN = this._rcN; this._rc = old; this._rcL = oL;
      const R = this._rc, L = this._rcL, f0 = this.forward[0], f1 = this.forward[1], ox = W / 2 - 0.5 + this.offset[0], oz = H / 2 - 0.5 + this.offset[1];
      let m = 0, vh = 0, nh = 0, vt = 0, nt = 0;
      for (let k = 0; k < n; k++) {
        const i = K[k], o = i * C, a = s[o + 3]; if (!(a > 0.05)) continue;
        const ia = 1 / Math.max(a, 1e-3), q = 4 * i;
        R[q] = s[o] * ia; R[q + 1] = s[o + 1] * ia; R[q + 2] = s[o + 2] * ia; R[q + 3] = a; L[m++] = i;
        if (a > thr) {
          const z = (i / HW) | 0, y = ((i - z * HW) / W) | 0, x = i - z * HW - y * W, lx = x - ox, lz = y - oz;
          const u = lx * f0 + lz * f1, v = -f1 * lx + f0 * lz;
          if (u > 5) { vh += v; nh++; } else if (u < -5) { vt += v; nt++; }
        }
      }
      this._rcN = m;
      this.bendPrev = this.bend; this.bend = nh && nt ? vt / nt - vh / nh : 0;
      this.stroke = this.bend - this.bendPrev;
    }

    // after a synchronous grow/reset: no interpolation from a stale snapshot
    _settle() { this._snap(); this._rp.set(this._rc); this._rpL.set(this._rcL.subarray(0, this._rcN)); this._rpN = this._rcN; this.stroke = 0; this._since = this._interval; }

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
      if (!this._growing) this._snap();
    }

    /** Work on the NCA for at most budgetMs (or cellsPerFrame cell updates), resuming wherever the last call stopped.
     *  dt (seconds) feeds the step clock: no more than stepsPerSecond NCA steps run per second of dt. Returns the
     *  number of NCA steps completed in this call. dt <= 0 or omitted -> just spend the budget. */
    step(dt) {
      const t0 = now();
      // adaptive rate: a slow backend (JS fallback ~19 ms/step) steps less often instead of eating every frame
      this.rate = this.msPerStep > 0 ? Math.max(Math.min(this.stepsPerSecond, this.minStepsPerSecond), Math.min(this.stepsPerSecond, this.maxMsPerSecond / this.msPerStep)) : this.stepsPerSecond;
      if (dt > 0) this._debt = Math.min(this._debt + dt * this.rate, 3); else if (dt === undefined) this._debt = Math.max(this._debt, 1);
      if (dt > 0) this._age(dt);
      const capCells = this.cellsPerFrame, budget = this.budgetMs * (dt > 0 ? Math.min(2, Math.max(1, dt * 60)) : 1);   // slow frames may spend up to 2x
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
      this._accMs += this.lastStepMs;
      if (done > 0) { const per = this._accMs / done; this.msPerStep = this.msPerStep ? this.msPerStep + (per - this.msPerStep) * 0.2 : per; this._accMs = 0; }
      const ddt = dt > 0 ? dt : 0;
      this._sinceFinish += ddt;
      if (done > 0) {                                         // interpolation clock: time since the newest snapshot
        this._interval += (this._sinceFinish / done - this._interval) * 0.25; this._sinceFinish = 0; this._since = 0;
      } else this._since += ddt;
      if (ddt > 0) { const k = 1 - Math.exp(-ddt * 1.5); this.achievedRate += (done / ddt - this.achievedRate) * k; }
      return done;
    }

    // ages debris, wounds and the hit flash (sim seconds)
    _age(dt) {
      const D = this.debris, drag = Math.exp(-dt * 1.6);
      for (let k = D.length - 1; k >= 0; k--) {
        const d = D[k]; d.life -= dt; if (d.life <= 0) { D[k] = D[D.length - 1]; D.pop(); continue; }
        for (let a = 0; a < 3; a++) { d.v[a] *= drag; d.p[a] += d.v[a] * dt; }
        d.ang += d.spin * dt;
      }
      for (let k = this.wounds.length - 1; k >= 0; k--) { const w = this.wounds[k]; w.age += dt; if (w.age > w.life) this.wounds.splice(k, 1); }
      this.flash = Math.max(0, this.flash - dt * 3);
    }

    /** Interpolation weight of the newest complete state (0 = previous, 1 = newest). */
    get blend() { return this.smooth ? Math.min(1, this._since / Math.max(1e-3, this._interval)) : 1; }

    /** Run n whole NCA steps synchronously (blocking; use before the creature is shown). */
    grow(n) {
      this._growing = true;
      for (let k = 0; k < n; k++) {
        if (!this._inStep) this._begin();
        const L = this._list, rate = this.fireRate, rnd = this.rand;
        while (this._cursor < this._listN) { const i = L[this._cursor++]; if (rnd() <= rate) this._push(i); }
        this._flush();
        this._finish();
      }
      this._growing = false; this._settle();
      return this;
    }

    // local (x, y, z) -> world rotation, row-major 3x3: Ry(yaw) * [pitch about the body's side axis, then roll about
    // its long axis]. With pitch = roll = 0 it is exactly the old yaw-only rotation.
    _rot() {
      const y = this.yaw, p = this.pitch, r = this.roll, c = this._rotKey;
      if (c && c[0] === y && c[1] === p && c[2] === r) return this._R;
      const F = [this.forward[0], 0, this.forward[1]], U = [0, 1, 0], S = [-this.forward[1], 0, this.forward[0]];
      const cp = Math.cos(p), sp = Math.sin(p), cr = Math.cos(r), sr = Math.sin(r);
      const F1 = [], U1 = [], S1 = [];
      for (let i = 0; i < 3; i++) { F1[i] = cp * F[i] + sp * U[i]; const u = -sp * F[i] + cp * U[i]; U1[i] = cr * u + sr * S[i]; S1[i] = -sr * u + cr * S[i]; }
      const cy = Math.cos(y), sy = Math.sin(y), R = this._R || (this._R = new Float64Array(9));
      for (let j = 0; j < 3; j++) {               // column j of Rl = F1 F^T + U1 U^T + S1 S^T, then Ry
        const l0 = F1[0] * F[j] + U1[0] * U[j] + S1[0] * S[j], l1 = F1[1] * F[j] + U1[1] * U[j] + S1[1] * S[j], l2 = F1[2] * F[j] + U1[2] * U[j] + S1[2] * S[j];
        R[j] = cy * l0 + sy * l2; R[3 + j] = l1; R[6 + j] = -sy * l0 + cy * l2;
      }
      this._rotKey = [y, p, r];
      return R;
    }

    /** grid (z, y, x) -> world [X, Y, Z]. The lizard lies in the grid's H-W plane -> world X-Z; grid depth -> world Y. */
    gridToWorld(z, y, x, out) {
      const s = this.scale, lx = (x + 0.5 - this.W / 2 - this.offset[0]) * s, ly = (z + 0.5 - this.D / 2) * s, lz = (y + 0.5 - this.H / 2 - this.offset[1]) * s;
      const R = this._rot(), P = this.position;
      out = out || [0, 0, 0];
      out[0] = P[0] + R[0] * lx + R[1] * ly + R[2] * lz; out[1] = P[1] + R[3] * lx + R[4] * ly + R[5] * lz; out[2] = P[2] + R[6] * lx + R[7] * ly + R[8] * lz;
      return out;
    }
    worldToGrid(p) {
      const P = Array.isArray(p) ? p : [p.x, p.y, p.z], s = this.scale, R = this._rot();
      const dx = P[0] - this.position[0], dy = P[1] - this.position[1], dz = P[2] - this.position[2];
      const lx = R[0] * dx + R[3] * dy + R[6] * dz, ly = R[1] * dx + R[4] * dy + R[7] * dz, lz = R[2] * dx + R[5] * dy + R[8] * dz;
      return [ly / s + this.D / 2 - 0.5, lz / s + this.H / 2 - 0.5 + this.offset[1], lx / s + this.W / 2 - 0.5 + this.offset[0]];   // z, y, x
    }

    /** World-space unit vector the head points along. */
    heading(out) { const R = this._rot(), f0 = this.forward[0], f1 = this.forward[1]; out = out || [0, 0, 0];
      out[0] = R[0] * f0 + R[2] * f1; out[1] = R[3] * f0 + R[5] * f1; out[2] = R[6] * f0 + R[8] * f1; return out; }
    /** yaw that points the head along the horizontal world direction (dx, dz). */
    yawFor(dx, dz) { return Math.atan2(this.forward[1], this.forward[0]) - Math.atan2(dz, dx); }

    /** Swim toward `goal` (world [x,y,z]) with locomotion phase-locked to the NCA's own swim: forward speed follows
     *  the step rate actually achieved (cruise u/s at refRate steps/s, so a slow or boosted NCA swims slower or faster)
     *  and surges with each tail beat (|stroke|); the head recoils against the tail's bend (yaw wiggle); it banks into
     *  turns and pitches toward the goal. opts: cruise (default 22), refRate (20), turnRate (rad/s, 0.6), wiggle (rad
     *  per voxel of bend, 0.09), bank (0.9), maxPitch (0.45). Moves position and sets yaw, pitch and roll. */
    swim(dt, goal, opts) {
      if (!(dt > 0)) return this;
      opts = opts || {};
      const cruise = opts.cruise ?? 22, ref = opts.refRate ?? 20, turnRate = opts.turnRate ?? 0.6, wig = opts.wiggle ?? 0.09, bankK = opts.bank ?? 0.9, maxP = opts.maxPitch ?? 0.45;
      if (this._hY === undefined) { this._hY = -this.yaw + Math.atan2(this.forward[1], this.forward[0]); this._hP = 0; this._turn = 0; this._wig = 0; this._spd = 0; this._bendAvg = this.bend; }
      // travel heading (azimuth _hY about world Y in atan2(z, x) terms, elevation _hP)
      if (goal) {
        const P = this.position, dx = goal[0] - P[0], dy = goal[1] - P[1], dz = goal[2] - P[2], hd = Math.hypot(dx, dz);
        let d = Math.atan2(dz, dx) - this._hY; d = Math.atan2(Math.sin(d), Math.cos(d));
        const want = Math.max(-turnRate, Math.min(turnRate, d * 1.5));
        this._turn += (want - this._turn) * (1 - Math.exp(-dt * 2.5));
        const pw = Math.max(-maxP, Math.min(maxP, Math.atan2(dy, Math.max(1, hd))));
        this._hP += (pw - this._hP) * (1 - Math.exp(-dt * 0.8));
      } else this._turn *= Math.exp(-dt * 2);
      this._hY += this._turn * dt;
      // tail beat -> thrust: |stroke| relative to its running mean; speed scales with the achieved NCA step rate
      const st = Math.abs(this.stroke); this._strokeAvg += (st - this._strokeAvg) * (1 - Math.exp(-dt * 0.3));
      const surge = this._strokeAvg > 1e-3 ? Math.min(2, st / this._strokeAvg) : 1;
      const rateK = Math.min(2.5, (this.achievedRate || this.rate) / ref);
      const spdW = cruise * rateK * (0.72 + 0.28 * surge);
      this._spd += (spdW - this._spd) * (1 - Math.exp(-dt * 3));
      const ch = Math.cos(this._hP), v = this._spd;
      this.position[0] += Math.cos(this._hY) * ch * v * dt; this.position[1] += Math.sin(this._hP) * v * dt; this.position[2] += Math.sin(this._hY) * ch * v * dt;
      // head recoils against the tail (bend interpolated between the last two snapshots, minus its slow mean)
      const b = this.bendPrev + (this.bend - this.bendPrev) * this.blend;
      this._bendAvg += (b - this._bendAvg) * (1 - Math.exp(-dt * 0.25));
      this._wig += (wig * (b - this._bendAvg) - this._wig) * (1 - Math.exp(-dt * 6));
      this.yaw = this.yawFor(Math.cos(this._hY), Math.sin(this._hY)) + this._wig;
      this.pitch = this._hP;
      this.roll += (Math.max(-0.7, Math.min(0.7, this._turn * bankK)) - this.roll) * (1 - Math.exp(-dt * 3));
      this.speed = v;
      return this;
    }

    /** Erase every cell within `radius` (world units) of `point` (zero all 16 channels, in the shown state and in a
     *  step in flight). The surviving cells regrow the hole. Returns the number of VISIBLE voxels removed. */
    hit(point, radius, opts) {
      const [gz, gy, gx] = this.worldToGrid(point), r = radius / this.scale, r2 = r * r, { C, D, H, W } = this, thr = this.alphaThreshold;
      const P = Array.isArray(point) ? point : [point.x, point.y, point.z], kick = (opts && opts.velocity) || null, maxDebris = (opts && opts.maxDebris) ?? 220;
      const rc = this._rc, rp = this._rp, wp = [0, 0, 0];
      let removed = 0;
      for (let z = Math.max(0, Math.floor(gz - r)); z <= Math.min(D - 1, Math.ceil(gz + r)); z++)
        for (let y = Math.max(0, Math.floor(gy - r)); y <= Math.min(H - 1, Math.ceil(gy + r)); y++)
          for (let x = Math.max(0, Math.floor(gx - r)); x <= Math.min(W - 1, Math.ceil(gx + r)); x++) {
            if ((z - gz) ** 2 + (y - gy) ** 2 + (x - gx) ** 2 > r2) continue;
            const i = (z * H + y) * W + x;
            if (this.s[i * C + 3] > thr) {
              removed++;
              if (this.debris.length < maxDebris) {      // a cut sheds the voxels it took as short-lived prisms
                this.gridToWorld(z, y, x, wp); const ex = wp[0] - P[0], ey = wp[1] - P[1], ez = wp[2] - P[2], en = Math.hypot(ex, ey, ez) || 1, sp = this.scale * (6 + 14 * this.rand());
                const v = [ex / en * sp, ey / en * sp + this.scale * 3, ez / en * sp];
                if (kick) for (let a = 0; a < 3; a++) v[a] += kick[a] * 0.35;
                const q = 4 * i, c = [rc[q], rc[q + 1], rc[q + 2]];
                this.debris.push({ p: wp.slice(), v, c, life: 0.9 + 0.9 * this.rand(), max: 1.8, ang: 6.28 * this.rand(), spin: (this.rand() - 0.5) * 14, ax: [this.rand() - 0.5, this.rand() - 0.5, this.rand() - 0.5] });
              }
            }
            this.s.fill(0, i * C, i * C + C); this.ns.fill(0, i * C, i * C + C);
            rc[4 * i + 3] = 0; rp[4 * i + 3] = 0;
            this.pre[i] = 0;            // a step in flight must not write into (or read from) the wound
          }
      this.hits = (this.hits || 0) + 1;
      if (removed > 0) { this.wounds.push({ g: [gz, gy, gx], r, age: 0, life: 5 }); if (this.wounds.length > 6) this.wounds.shift(); this.flash = 1; }
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

    // render colour: mostly the element colour, modulated by the NCA's own light/dark pattern (lizard markings), with
    // a little of its hue; a touch richer than _colour (which voxels() keeps for API compatibility).
    _skin(R, G, B, out) {
      R = R < 0 ? 0 : R > 1 ? 1 : R; G = G < 0 ? 0 : G > 1 ? 1 : G; B = B < 0 ? 0 : B > 1 ? 1 : B;
      // the trained lizard's luminance spans ~0.7 (dark dorsal marks) .. ~1.7 (pale belly / bright spots): stretch it
      if (this._skinEl !== this.element) { this._skinEl = this.element; this._skinE = NcaCreature.saturate(ELEMENT_COLOUR[this.element] || ELEMENT_COLOUR.space, 1.7); }
      const E = this._skinE, t = Math.min(1, this.tint + 0.2);
      let rel = ((0.6 * R + 0.9 * G + 0.5 * B) / 0.75 - 0.72) / 0.95; rel = rel < 0 ? 0 : rel > 1 ? 1 : rel;
      const k = 0.16 + 0.7 * rel, j = (1 - t) * (0.3 + 0.5 * rel);
      out[0] = j * R + t * E[0] * k; out[1] = j * G + t * E[1] * k; out[2] = j * B + t * E[2] * k;
      return out;
    }

    /** Optional three.js helper: an InstancedMesh of triangular prisms, refreshed by sync(). Each live SURFACE voxel
     *  becomes a prism lying along the skin (its ridge along the outward normal, its length pointing tail-ward), sized
     *  by alpha interpolated between the last two complete NCA states, so the body swims smoothly between steps and
     *  reads as overlapping scales. The material adds a soft rim light and a per-instance glow (fresh wounds, the
     *  regrowing tissue, a hit flash, debris). opts: capacity (instances incl. debris, default 4096), voxelSize
     *  (default 1.15), material (custom material: no glow hook). */
    mesh(THREE, opts) {
      if (this._mesh) return this._mesh;
      opts = opts || {};
      const cap = opts.capacity || 4096;
      this._vsz = opts.voxelSize ?? 1.15;
      const geo = new THREE.CylinderGeometry(0.5, 0.5, 1, 3, 1);   // a 3-sided cylinder = triangular prism, ridge at +Z
      const glow = new THREE.InstancedBufferAttribute(new Float32Array(cap), 1); glow.setUsage(THREE.DynamicDrawUsage);
      geo.setAttribute('aGlow', glow);
      let mat = opts.material;
      if (!mat) {
        mat = new THREE.MeshStandardMaterial({ roughness: 0.38, metalness: 0.15, flatShading: true });
        mat.onBeforeCompile = (sh) => {
          sh.vertexShader = 'attribute float aGlow;\nvarying float vGlow;\n' + sh.vertexShader.replace('#include <begin_vertex>', '#include <begin_vertex>\n  vGlow = aGlow;');
          sh.fragmentShader = 'varying float vGlow;\n' + sh.fragmentShader.replace('#include <emissivemap_fragment>', [
            '#include <emissivemap_fragment>',
            '  float ncaRim = pow(1.0 - abs(dot(normal, normalize(vViewPosition))), 2.5);',
            '  totalEmissiveRadiance += diffuseColor.rgb * (0.07 + 0.35 * ncaRim + 1.5 * vGlow) + vec3(1.0, 0.93, 0.78) * 0.5 * vGlow * vGlow;'].join('\n'));
        };
        mat.customProgramCacheKey = () => 'nca-skin';
      }
      const m = new THREE.InstancedMesh(geo, mat, cap);
      m.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
      m.setColorAt(0, new THREE.Color(1, 1, 1));
      m.instanceColor.setUsage && m.instanceColor.setUsage(THREE.DynamicDrawUsage);
      m.frustumCulled = false; m.count = 0; m.name = 'NcaCreature';
      this._mesh = m; this._THREE = THREE; this._glow = glow; this._cap = cap;
      this.sync();
      return m;
    }

    /** Push the live voxels (and debris) into the InstancedMesh (no-op without mesh()). Returns the instance count. */
    sync() {
      const m = this._mesh; if (!m) return 0;
      const { D, H, W } = this, HW = H * W, R = this._rot(), sc = this.scale * this._vsz, thr = this.alphaThreshold, show = Math.min(0.12, thr);
      const f = this.blend, g = 1 - f, rc = this._rc, rp = this._rp, M = m.instanceMatrix.array, CO = m.instanceColor.array, GL = this._glow.array;
      const cap = this._cap, surf = this.surface, scales = this.scales, P = this.position, ws = this.scale;
      const ox = this.W / 2 - 0.5 + this.offset[0], oz = this.H / 2 - 0.5 + this.offset[1], oy = this.D / 2 - 0.5;
      const hd = this.heading(this._hd || (this._hd = [0, 0, 0])), wounds = this.wounds, flash = this.flash * 0.35, col = this._col || (this._col = [0, 0, 0]);
      const A = (j) => rp[4 * j + 3] * g + rc[4 * j + 3] * f;
      let n = 0;
      const lists = [this._rcL, this._rpL], counts = [this._rcN, this._rpN];
      for (let li = 0; li < 2; li++) {
        const L = lists[li], cnt = counts[li];
        for (let k = 0; k < cnt && n < cap; k++) {
          const i = L[k], q = 4 * i;
          if (li === 1 && rc[q + 3] > 0) continue;                 // already drawn from the newest list
          const a = rp[q + 3] * g + rc[q + 3] * f; if (a < show) continue;
          const z = (i / HW) | 0, y = ((i - z * HW) / W) | 0, x = i - z * HW - y * W;
          const ax0 = x > 0 ? A(i - 1) : 0, ax1 = x < W - 1 ? A(i + 1) : 0, ay0 = y > 0 ? A(i - W) : 0, ay1 = y < H - 1 ? A(i + W) : 0, az0 = z > 0 ? A(i - HW) : 0, az1 = z < D - 1 ? A(i + HW) : 0;
          if (surf && ax0 > thr && ax1 > thr && ay0 > thr && ay1 > thr && az0 > thr && az1 > thr) continue;   // fully interior
          // world position of the cell
          const lx = (x - ox) * ws, ly = (z - oy) * ws, lz = (y - oz) * ws;
          const px = P[0] + R[0] * lx + R[1] * ly + R[2] * lz, py = P[1] + R[3] * lx + R[4] * ly + R[5] * lz, pz = P[2] + R[6] * lx + R[7] * ly + R[8] * lz;
          const kk = Math.min(1, Math.max(0, (a - show) / (0.55 - show))), sz = sc * kk * kk * (3 - 2 * kk) * (0.75 + 0.25 * Math.min(1, a));
          let Xx, Xy, Xz, Yx, Yy, Yz, Zx, Zy, Zz;
          if (scales) {
            // outward normal = -grad(alpha), local (x, depth, y) -> world
            let nx = ax0 - ax1, ny = az0 - az1, nz = ay0 - ay1, nn = Math.sqrt(nx * nx + ny * ny + nz * nz);
            if (nn < 1e-4) { nx = 0; ny = 1; nz = 0; nn = 1; }
            nx /= nn; ny /= nn; nz /= nn;
            Zx = R[0] * nx + R[1] * ny + R[2] * nz; Zy = R[3] * nx + R[4] * ny + R[5] * nz; Zz = R[6] * nx + R[7] * ny + R[8] * nz;
            // length axis: tail-ward body axis, made tangent to the skin
            let tx = -hd[0], ty = -hd[1], tz = -hd[2]; const dn = tx * Zx + ty * Zy + tz * Zz; tx -= dn * Zx; ty -= dn * Zy; tz -= dn * Zz;
            let tn = Math.sqrt(tx * tx + ty * ty + tz * tz);
            if (tn < 0.2) { tx = R[1] - Zx * (R[1] * Zx + R[4] * Zy + R[7] * Zz); ty = R[4] - Zy * (R[1] * Zx + R[4] * Zy + R[7] * Zz); tz = R[7] - Zz * (R[1] * Zx + R[4] * Zy + R[7] * Zz); tn = Math.sqrt(tx * tx + ty * ty + tz * tz) || 1; }
            // the prism's cap faces out (Y = normal) and its triangle points tail-ward (Z = t): a skin of triangle
            // scales; X = Y x Z keeps the basis right-handed
            const ux = Zx, uy = Zy, uz = Zz; Zx = tx / tn; Zy = ty / tn; Zz = tz / tn; Yx = ux; Yy = uy; Yz = uz;
            Xx = Yy * Zz - Yz * Zy; Xy = Yz * Zx - Yx * Zz; Xz = Yx * Zy - Yy * Zx;
            const sx = sz * 1.35, sy = sz * 0.6, s3 = sz * 1.55;
            Xx *= sx; Xy *= sx; Xz *= sx; Yx *= sy; Yy *= sy; Yz *= sy; Zx *= s3; Zy *= s3; Zz *= s3;
          } else {
            const sr = sz * 1.24;
            Xx = R[0] * sr; Xy = R[3] * sr; Xz = R[6] * sr; Yx = R[1] * sz; Yy = R[4] * sz; Yz = R[7] * sz; Zx = R[2] * sr; Zy = R[5] * sr; Zz = R[8] * sr;
          }
          const o = 16 * n;
          M[o] = Xx; M[o + 1] = Xy; M[o + 2] = Xz; M[o + 3] = 0; M[o + 4] = Yx; M[o + 5] = Yy; M[o + 6] = Yz; M[o + 7] = 0;
          M[o + 8] = Zx; M[o + 9] = Zy; M[o + 10] = Zz; M[o + 11] = 0; M[o + 12] = px; M[o + 13] = py; M[o + 14] = pz; M[o + 15] = 1;
          // colour: interpolate the NCA rgb (from whichever snapshot has the cell), then skin it
          const wp = rp[q + 3] > 0 ? (rc[q + 3] > 0 ? g : 1) : 0, wc = 1 - wp;
          this._skin(rp[q] * wp + rc[q] * wc, rp[q + 1] * wp + rc[q + 1] * wc, rp[q + 2] * wp + rc[q + 2] * wc, col);
          CO[3 * n] = col[0]; CO[3 * n + 1] = col[1]; CO[3 * n + 2] = col[2];
          let gl = flash;
          for (let w = 0; w < wounds.length; w++) {          // the wound's rim and the tissue regrowing inside it glow
            const W0 = wounds[w], d = Math.sqrt((z - W0.g[0]) ** 2 + (y - W0.g[1]) ** 2 + (x - W0.g[2]) ** 2), rim = W0.r + 2.5;
            if (d < rim) { const fade = 1 - W0.age / W0.life; gl += fade * fade * (d < W0.r ? 0.9 : 0.9 * (rim - d) / 2.5); }
          }
          GL[n] = gl > 1.2 ? 1.2 : gl;
          n++;
        }
      }
      // debris: shrinking, tumbling, glowing prisms
      const D2 = this.debris;
      for (let k = 0; k < D2.length && n < cap; k++) {
        const d = D2[k], lf = d.life / d.max, sz = sc * 1.1 * Math.min(1, lf * 1.8);
        let ux = d.ax[0], uy = d.ax[1], uz = d.ax[2]; const un = Math.sqrt(ux * ux + uy * uy + uz * uz) || 1; ux /= un; uy /= un; uz /= un;
        const c = Math.cos(d.ang), s = Math.sin(d.ang), t = 1 - c, o = 16 * n;     // Rodrigues rotation about ax
        M[o] = (t * ux * ux + c) * sz; M[o + 1] = (t * ux * uy + s * uz) * sz; M[o + 2] = (t * ux * uz - s * uy) * sz; M[o + 3] = 0;
        M[o + 4] = (t * ux * uy - s * uz) * sz; M[o + 5] = (t * uy * uy + c) * sz; M[o + 6] = (t * uy * uz + s * ux) * sz; M[o + 7] = 0;
        M[o + 8] = (t * ux * uz + s * uy) * sz; M[o + 9] = (t * uy * uz - s * ux) * sz; M[o + 10] = (t * uz * uz + c) * sz; M[o + 11] = 0;
        M[o + 12] = d.p[0]; M[o + 13] = d.p[1]; M[o + 14] = d.p[2]; M[o + 15] = 1;
        this._skin(d.c[0], d.c[1], d.c[2], col); CO[3 * n] = col[0]; CO[3 * n + 1] = col[1]; CO[3 * n + 2] = col[2];
        GL[n] = 0.35 + 0.85 * lf;
        n++;
      }
      m.count = n;
      m.instanceMatrix.needsUpdate = true; m.instanceColor.needsUpdate = true; this._glow.needsUpdate = true;
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
  /** push a colour away from its grey (max channel kept): f = 1 unchanged, > 1 more saturated */
  NcaCreature.saturate = function (c, f) { const m = Math.max(c[0], c[1], c[2]); return c.map(v => Math.max(0, m + (v - m) * f)); };
  NcaCreature.loadWeights = loadWeights;
  NcaCreature.hasEmbeddedWeights = !!NCA_WEIGHTS;

  if (typeof module !== 'undefined' && module.exports) module.exports = NcaCreature;
  if (root) root.NcaCreature = NcaCreature;
})(typeof window !== 'undefined' ? window : (typeof globalThis !== 'undefined' ? globalThis : this));
