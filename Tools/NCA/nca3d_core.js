// 3D Neural CA — inference step over a channels-last Float32Array [D*H*W*C].
// Mirrors nca3d.CA3D exactly (verify_js3d.py holds them equal): perception
// [identity, sobel_x, sobel_y, sobel_z] (feature 4*c+k), 3D Sobel = derivative
// [-1,0,1] on one axis x smoothing [1,2,1] on the other two, /32; 3x3x3 alive mask
// before and after. One per-cell function of the 27-voxel neighbourhood — the shape
// of a compute-shader kernel.
function makeNCA3D(weights) {
  const C = weights.channel_n, HID = weights.hidden, F = 4 * C;
  const D = weights.D, H = weights.H, W = weights.W, N = D * H * W;
  const w1 = new Float32Array(weights.w1.flat()), b1 = new Float32Array(weights.b1);
  const w2 = new Float32Array(weights.w2.flat()), b2 = new Float32Array(weights.b2);
  // [27][4] stencil, neighbour order dz, dy, dx (major to minor)
  const K = new Float32Array(27 * 4);
  { const sm = [1, 2, 1], df = [-1, 0, 1], e = [0, 1, 0]; let n = 0;
    for (let z = 0; z < 3; z++) for (let y = 0; y < 3; y++) for (let x = 0; x < 3; x++, n++) {
      K[n * 4] = e[z] * e[y] * e[x];
      K[n * 4 + 1] = sm[z] * sm[y] * df[x] / 32;
      K[n * 4 + 2] = sm[z] * df[y] * sm[x] / 32;
      K[n * 4 + 3] = df[z] * sm[y] * sm[x] / 32;
    } }
  let s = new Float32Array(N * C), ns = new Float32Array(N * C);
  const pre = new Uint8Array(N), post = new Uint8Array(N), tmpA = new Float32Array(N), tmpB = new Float32Array(N);
  const feat = new Float32Array(F), hid = new Float32Array(HID);
  const I = (z, y, x) => (z * H + y) * W + x;

  function aliveMask(st, out) {   // separable 3x3x3 max of alpha > 0.1
    for (let i = 0; i < N; i++) tmpA[i] = st[i * C + 3];
    for (let z = 0; z < D; z++) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      let m = tmpA[I(z, y, x)]; if (x > 0) m = Math.max(m, tmpA[I(z, y, x - 1)]); if (x < W - 1) m = Math.max(m, tmpA[I(z, y, x + 1)]);
      tmpB[I(z, y, x)] = m; }
    for (let z = 0; z < D; z++) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      let m = tmpB[I(z, y, x)]; if (y > 0) m = Math.max(m, tmpB[I(z, y - 1, x)]); if (y < H - 1) m = Math.max(m, tmpB[I(z, y + 1, x)]);
      tmpA[I(z, y, x)] = m; }
    for (let z = 0; z < D; z++) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      let m = tmpA[I(z, y, x)]; if (z > 0) m = Math.max(m, tmpA[I(z - 1, y, x)]); if (z < D - 1) m = Math.max(m, tmpA[I(z + 1, y, x)]);
      out[I(z, y, x)] = m > 0.1 ? 1 : 0; }
  }

  function step(opts) {
    const rate = (opts && opts.fireRate) ?? weights.fire_rate, rnd = (opts && opts.random) || Math.random;
    aliveMask(s, pre);
    ns.set(s);
    let alive = 0;
    for (let z = 0; z < D; z++) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = I(z, y, x);
      if (!pre[i]) continue;
      alive++;
      if (!(rnd() <= rate)) continue;
      feat.fill(0);
      let n = 0;
      for (let dz = -1; dz <= 1; dz++) for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++, n++) {
        const zz = z + dz, yy = y + dy, xx = x + dx;
        if (zz < 0 || zz >= D || yy < 0 || yy >= H || xx < 0 || xx >= W) continue;
        const o = I(zz, yy, xx) * C, k0 = K[n * 4], k1 = K[n * 4 + 1], k2 = K[n * 4 + 2], k3 = K[n * 4 + 3];
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
      for (let c = 0; c < C; c++) {
        let a = b2[c]; const o = c * HID;
        for (let h = 0; h < HID; h++) a += w2[o + h] * hid[h];
        ns[i * C + c] += a;
      }
    }
    aliveMask(ns, post);
    for (let i = 0; i < N; i++) if (!(pre[i] && post[i])) ns.fill(0, i * C, i * C + C);
    const t = s; s = ns; ns = t;
    return alive;
  }
  return {
    C, D, H, W,
    get state() { return s; },
    seed() { s.fill(0); const i = I(D >> 1, H >> 1, W >> 1); for (let c = 3; c < C; c++) s[i * C + c] = 1; },
    eraseBall(cz, cy, cx, r) {
      for (let z = 0; z < D; z++) for (let y = 0; y < H; y++) for (let x = 0; x < W; x++)
        if ((z - cz) ** 2 + (y - cy) ** 2 + (x - cx) ** 2 < r * r) s.fill(0, I(z, y, x) * C, I(z, y, x) * C + C);
    },
    step,
  };
}
if (typeof module !== "undefined") module.exports = { makeNCA3D };
