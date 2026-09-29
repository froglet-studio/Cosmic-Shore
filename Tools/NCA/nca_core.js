// Growing Neural CA — inference step, channels-last Float32Array [H*W*C].
// Mirrors growing_nca.CAModel.forward exactly (verify_js.py holds them equal).
// Also the reference for an eventual C#/HLSL port: nothing here is JS-specific.
function makeNCA(weights, H, W) {
  const C = weights.channel_n, HID = weights.hidden, F = 3 * C;
  const w1 = new Float32Array(weights.w1.flat()), b1 = new Float32Array(weights.b1);
  const w2 = new Float32Array(weights.w2.flat()), b2 = new Float32Array(weights.b2);
  const N = H * W;
  let x = new Float32Array(N * C), nx = new Float32Array(N * C);
  const pre = new Uint8Array(N), post = new Uint8Array(N);
  const feat = new Float32Array(F), hid = new Float32Array(HID);

  function aliveMask(s, out) {
    for (let y = 0; y < H; y++) for (let xx = 0; xx < W; xx++) {
      let m = -Infinity;
      for (let dy = -1; dy <= 1; dy++) { const yy = y + dy; if (yy < 0 || yy >= H) continue;
        for (let dx = -1; dx <= 1; dx++) { const x2 = xx + dx; if (x2 < 0 || x2 >= W) continue;
          const a = s[(yy * W + x2) * C + 3]; if (a > m) m = a; } }
      out[y * W + xx] = m > 0.1 ? 1 : 0;
    }
  }
  const at = (s, y, xx, c) => (y < 0 || y >= H || xx < 0 || xx >= W) ? 0 : s[(y * W + xx) * C + c];

  function step(opts) {
    const rate = opts.fireRate ?? weights.fire_rate, ang = opts.angle || 0;
    const ca = Math.cos(ang), sa = Math.sin(ang), rnd = opts.random || Math.random;
    aliveMask(x, pre);
    nx.set(x);
    let alive = 0;
    for (let y = 0; y < H; y++) for (let xx = 0; xx < W; xx++) {
      const i = y * W + xx;
      if (!pre[i]) continue;
      alive++;
      if (!(rnd() <= rate)) continue;
      for (let c = 0; c < C; c++) {
        const tl = at(x, y - 1, xx - 1, c), t = at(x, y - 1, xx, c), tr = at(x, y - 1, xx + 1, c);
        const l = at(x, y, xx - 1, c), r = at(x, y, xx + 1, c);
        const bl = at(x, y + 1, xx - 1, c), b = at(x, y + 1, xx, c), br = at(x, y + 1, xx + 1, c);
        const sx = ((tr + 2 * r + br) - (tl + 2 * l + bl)) / 8, sy = ((bl + 2 * b + br) - (tl + 2 * t + tr)) / 8;
        feat[3 * c] = x[i * C + c];
        feat[3 * c + 1] = ca * sx - sa * sy;
        feat[3 * c + 2] = sa * sx + ca * sy;
      }
      for (let h = 0; h < HID; h++) {
        let s = b1[h]; const o = h * F;
        for (let f = 0; f < F; f++) s += w1[o + f] * feat[f];
        hid[h] = s > 0 ? s : 0;
      }
      for (let c = 0; c < C; c++) {
        let s = b2[c]; const o = c * HID;
        for (let h = 0; h < HID; h++) s += w2[o + h] * hid[h];
        nx[i * C + c] += s;
      }
    }
    aliveMask(nx, post);
    for (let i = 0; i < N; i++) if (!(pre[i] && post[i])) nx.fill(0, i * C, i * C + C);
    const t = x; x = nx; nx = t;
    return alive;
  }
  return {
    C, H, W,
    get state() { return x; },
    seed(sy = H >> 1, sx = W >> 1, clear = true) {
      if (clear) x.fill(0);
      for (let c = 3; c < C; c++) x[(sy * W + sx) * C + c] = 1;
    },
    erase(cy, cx, r) {
      for (let y = Math.max(0, Math.floor(cy - r)); y <= Math.min(H - 1, Math.ceil(cy + r)); y++)
        for (let xx = Math.max(0, Math.floor(cx - r)); xx <= Math.min(W - 1, Math.ceil(cx + r)); xx++)
          if ((y - cy) ** 2 + (xx - cx) ** 2 < r * r) x.fill(0, (y * W + xx) * C, (y * W + xx + 1) * C);
    },
    step,
  };
}
if (typeof module !== "undefined") module.exports = { makeNCA };
