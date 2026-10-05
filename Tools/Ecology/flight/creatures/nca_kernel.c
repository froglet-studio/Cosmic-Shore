// nca_kernel.c - WebAssembly SIMD kernel for nca_creature.js (one batch of 3D-NCA cell updates).
//   clang --target=wasm32 -O3 -msimd128 -nostdlib -Wl,--no-entry -Wl,--import-memory -Wl,--export=nca_cells \
//         -Wl,--export=__heap_base -o nca_kernel.wasm nca_kernel.c        (build_nca_creature.py does this)
// Fixed shape: C = 16 channels, HID = 128 hidden, perception = 4 x 16 (identity, sobel x, y, z) on a 3x3x3 stencil.
// Weights are pre-laid-out by nca_creature.js: w1t[f'][h] with f' = k*16 + c (k = stencil kind, c = channel),
// w2t[h][c]. Accumulation is f32 (the JS path accumulates in f64): differences are ~1e-6, far below fp16 weight noise.
#include <wasm_simd128.h>

#define C 16
#define HID 128
#define F 64

typedef struct { int D, H, W; } Dims;

static inline v128_t ld(const float *p) { return wasm_v128_load(p); }
static inline void st(float *p, v128_t v) { wasm_v128_store(p, v); }
static inline v128_t fma4(v128_t a, v128_t b, v128_t c) { return wasm_f32x4_add(a, wasm_f32x4_mul(b, c)); }

// perception for one cell -> X[64] in k*16+c layout
static void perceive(const float *s, const unsigned char *pre, const float *K, int i, int D, int H, int W, float *X) {
  const int HW = H * W, z = i / HW, y = (i - z * HW) / W, x = i - z * HW - y * W;
  v128_t a[16];
  for (int m = 0; m < 16; m++) a[m] = wasm_f32x4_splat(0.f);
  int n = 0;
  for (int dz = -1; dz <= 1; dz++) for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++, n++) {
    const int zz = z + dz, yy = y + dy, xx = x + dx;
    if (zz < 0 || zz >= D || yy < 0 || yy >= H || xx < 0 || xx >= W) continue;
    const int j = (zz * H + yy) * W + xx;
    if (!pre[j]) continue;
    const float *r = s + j * C;
    const v128_t v0 = ld(r), v1 = ld(r + 4), v2 = ld(r + 8), v3 = ld(r + 12);
    for (int k = 0; k < 4; k++) {
      const float kk = K[n * 4 + k];
      if (kk == 0.f) continue;
      const v128_t ks = wasm_f32x4_splat(kk);
      a[k * 4] = fma4(a[k * 4], ks, v0); a[k * 4 + 1] = fma4(a[k * 4 + 1], ks, v1);
      a[k * 4 + 2] = fma4(a[k * 4 + 2], ks, v2); a[k * 4 + 3] = fma4(a[k * 4 + 3], ks, v3);
    }
  }
  for (int m = 0; m < 16; m++) st(X + 4 * m, a[m]);
}

// Update nq cells listed in q: ns[i] += MLP(perceive(s, i)). dims = {D, H, W}.
void nca_cells(const float *s, float *ns, const unsigned char *pre, const int *q, int nq,
               const float *w1t, const float *b1, const float *w2t, const float *b2, const float *K, const int *dims) {
  const int D = dims[0], H = dims[1], W = dims[2];
  float X[4][F] __attribute__((aligned(16)));
  float Hd[4][HID] __attribute__((aligned(16)));
  for (int g = 0; g < nq; g += 4) {
    const int nb = nq - g < 4 ? nq - g : 4;
    for (int b = 0; b < 4; b++) {
      if (b < nb) perceive(s, pre, K, q[g + b], D, H, W, X[b]);
      else for (int f = 0; f < F; f++) X[b][f] = 0.f;
    }
    for (int h = 0; h < HID; h += 8) {
      const v128_t bA = ld(b1 + h), bB = ld(b1 + h + 4);
      v128_t a0 = bA, a1 = bB, c0 = bA, c1 = bB, d0 = bA, d1 = bB, e0 = bA, e1 = bB;
      const float *w = w1t + h;
      for (int f = 0; f < F; f++, w += HID) {
        const v128_t wa = ld(w), wb = ld(w + 4);
        v128_t x = wasm_f32x4_splat(X[0][f]); a0 = fma4(a0, wa, x); a1 = fma4(a1, wb, x);
        x = wasm_f32x4_splat(X[1][f]); c0 = fma4(c0, wa, x); c1 = fma4(c1, wb, x);
        x = wasm_f32x4_splat(X[2][f]); d0 = fma4(d0, wa, x); d1 = fma4(d1, wb, x);
        x = wasm_f32x4_splat(X[3][f]); e0 = fma4(e0, wa, x); e1 = fma4(e1, wb, x);
      }
      const v128_t zr = wasm_f32x4_splat(0.f);
      st(Hd[0] + h, wasm_f32x4_max(a0, zr)); st(Hd[0] + h + 4, wasm_f32x4_max(a1, zr));
      st(Hd[1] + h, wasm_f32x4_max(c0, zr)); st(Hd[1] + h + 4, wasm_f32x4_max(c1, zr));
      st(Hd[2] + h, wasm_f32x4_max(d0, zr)); st(Hd[2] + h + 4, wasm_f32x4_max(d1, zr));
      st(Hd[3] + h, wasm_f32x4_max(e0, zr)); st(Hd[3] + h + 4, wasm_f32x4_max(e1, zr));
    }
    for (int b = 0; b < nb; b++) {
      v128_t o0 = ld(b2), o1 = ld(b2 + 4), o2 = ld(b2 + 8), o3 = ld(b2 + 12);
      const float *hd = Hd[b];
      for (int h = 0; h < HID; h++) {
        const float hv = hd[h];
        if (hv == 0.f) continue;           // ~60 % of hidden units are off after ReLU
        const v128_t x = wasm_f32x4_splat(hv); const float *w = w2t + h * C;
        o0 = fma4(o0, ld(w), x); o1 = fma4(o1, ld(w + 4), x); o2 = fma4(o2, ld(w + 8), x); o3 = fma4(o3, ld(w + 12), x);
      }
      float *r = ns + q[g + b] * C;
      st(r, wasm_f32x4_add(ld(r), o0)); st(r + 4, wasm_f32x4_add(ld(r + 4), o1));
      st(r + 8, wasm_f32x4_add(ld(r + 8), o2)); st(r + 12, wasm_f32x4_add(ld(r + 12), o3));
    }
  }
}
