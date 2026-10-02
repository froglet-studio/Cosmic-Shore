// Cost reference for the hierarchical ecology: the MICRO agent tick and the MACRO cohort tick written the way a
// Burst job would run them (float32 SoA, a counting-sort spatial hash, no allocation in the loop, one thread).
// It is NOT the simulation (that is the Python reference); it is the same work per agent / per cohort so the
// per-unit cost of a compiled port can be measured instead of guessed.
//
//   cc -O2 -march=native -o kernels kernels.c -lm && ./kernels
//
// Prints ns per agent per micro tick at several agent counts, and ns per cohort per macro tick + the flora field.
#include <math.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

static uint64_t rs = 88172645463325252ull;
static inline uint32_t xr(void) { rs ^= rs << 13; rs ^= rs >> 7; rs ^= rs << 17; return (uint32_t)rs; }
static inline float ur(void) { return (xr() >> 8) * (1.0f / 16777216.0f); }
static double now(void) { struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t); return t.tv_sec + t.tv_nsec * 1e-9; }

#define R 1200.0f
#define L 200.0f
#define VA 4
#define NV 64
#define GN 12           // regions per axis
#define HC 40.0f        // hash cell (>= flee radius)
#define HN 60           // hash cells per axis (2400/40)

typedef struct {
    int n;
    float *x, *y, *z, *vx, *vy, *vz, *hx, *hy, *hz, *E;
    uint8_t *sp;
    int *cell, *order, *start;
} Agents;

static float F[GN * GN * GN * NV], K[GN * GN * GN * NV], Nut[GN * GN * GN];

static inline int region_of(float x, float y, float z) {
    int i = (int)((x + R) / L), j = (int)((y + R) / L), k = (int)((z + R) / L);
    if (i < 0) i = 0; if (i >= GN) i = GN - 1; if (j < 0) j = 0; if (j >= GN) j = GN - 1; if (k < 0) k = 0; if (k >= GN) k = GN - 1;
    return (i * GN + j) * GN + k;
}
static inline int vox_of(float x, float y, float z, int r) {
    int i = r / (GN * GN), j = (r / GN) % GN, k = r % GN;
    float ox = -R + i * L, oy = -R + j * L, oz = -R + k * L;
    int a = (int)((x - ox) / (L / VA)), b = (int)((y - oy) / (L / VA)), c = (int)((z - oz) / (L / VA));
    a = a < 0 ? 0 : a >= VA ? VA - 1 : a; b = b < 0 ? 0 : b >= VA ? VA - 1 : b; c = c < 0 ? 0 : c >= VA ? VA - 1 : c;
    return (a * VA + b) * VA + c;
}
static inline int hcell(float x, float y, float z) {
    int i = (int)((x + R) / HC), j = (int)((y + R) / HC), k = (int)((z + R) / HC);
    i = i < 0 ? 0 : i >= HN ? HN - 1 : i; j = j < 0 ? 0 : j >= HN ? HN - 1 : j; k = k < 0 ? 0 : k >= HN ? HN - 1 : k;
    return (i * HN + j) * HN + k;
}

static void build_hash(Agents *A) {
    memset(A->start, 0, sizeof(int) * (HN * HN * HN + 1));
    for (int i = 0; i < A->n; i++) { A->cell[i] = hcell(A->x[i], A->y[i], A->z[i]); A->start[A->cell[i] + 1]++; }
    for (int c = 0; c < HN * HN * HN; c++) A->start[c + 1] += A->start[c];
    static int fill[HN * HN * HN];
    memcpy(fill, A->start, sizeof(int) * HN * HN * HN);
    for (int i = 0; i < A->n; i++) A->order[fill[A->cell[i]]++] = i;
}

// nearest agent of species `want` within radius rr of agent i (3x3x3 hash cells)
static int nearest(Agents *A, int i, uint8_t want, float rr, float *dout) {
    int c = A->cell[i], ci = c / (HN * HN), cj = (c / HN) % HN, ck = c % HN, best = -1;
    float bd = rr * rr;
    for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) for (int d = -1; d <= 1; d++) {
        int x = ci + a, y = cj + b, z = ck + d;
        if (x < 0 || y < 0 || z < 0 || x >= HN || y >= HN || z >= HN) continue;
        int cc = (x * HN + y) * HN + z;
        for (int s = A->start[cc]; s < A->start[cc + 1]; s++) {
            int j = A->order[s];
            if (A->sp[j] != want) continue;
            float dx = A->x[j] - A->x[i], dy = A->y[j] - A->y[i], dz = A->z[j] - A->z[i];
            float d2 = dx * dx + dy * dy + dz * dz;
            if (d2 < bd) { bd = d2; best = j; }
        }
    }
    *dout = sqrtf(bd);
    return best;
}

static void micro_tick(Agents *A, float dt) {
    build_hash(A);
    for (int i = 0; i < A->n; i++) {
        float sx = 0, sy = 0, sz = 0, d;
        int r = region_of(A->x[i], A->y[i], A->z[i]), v = vox_of(A->x[i], A->y[i], A->z[i], r);
        float E = A->E[i];
        if (A->sp[i] == 0) {
            float G = F[r * NV + v] + K[r * NV + v];
            float take = 0.25f * G / (G + 30.f) * (1.f - E / 32.f) * dt;
            if (take > G) take = G;
            F[r * NV + v] -= take; A->E[i] += take;          // (a job would scatter-add per voxel)
            int j = nearest(A, i, 1, 40.f, &d);
            if (j >= 0) { sx += (A->x[i] - A->x[j]) / d * 4; sy += (A->y[i] - A->y[j]) / d * 4; sz += (A->z[i] - A->z[j]) / d * 4; }
            // food gradient: 6 voxel samples
            float best = G; int bdir = -1;
            for (int k = 0; k < 6; k++) {
                float px = A->x[i] + (k == 0 ? 50 : k == 1 ? -50 : 0), py = A->y[i] + (k == 2 ? 50 : k == 3 ? -50 : 0), pz = A->z[i] + (k == 4 ? 50 : k == 5 ? -50 : 0);
                int r2 = region_of(px, py, pz), v2 = vox_of(px, py, pz, r2);
                float g2 = F[r2 * NV + v2] + K[r2 * NV + v2];
                if (g2 > best * 1.05f) { best = g2; bdir = k; }
            }
            if (bdir >= 0) { sx += (bdir == 0) - (bdir == 1); sy += (bdir == 2) - (bdir == 3); sz += (bdir == 4) - (bdir == 5); }
        } else if (E < 96.f) {
            int j = nearest(A, i, 0, 25.f, &d);
            if (j >= 0) { sx += (A->x[j] - A->x[i]) / d * 3; sy += (A->y[j] - A->y[i]) / d * 3; sz += (A->z[j] - A->z[i]) / d * 3; }
        }
        // heading wander
        A->hx[i] += (ur() - .5f) * 0.2f; A->hy[i] += (ur() - .5f) * 0.2f; A->hz[i] += (ur() - .5f) * 0.2f;
        float hn = 1.f / sqrtf(A->hx[i] * A->hx[i] + A->hy[i] * A->hy[i] + A->hz[i] * A->hz[i] + 1e-9f);
        A->hx[i] *= hn; A->hy[i] *= hn; A->hz[i] *= hn;
        sx += A->hx[i] * .6f; sy += A->hy[i] * .6f; sz += A->hz[i] * .6f;
        float sn = sqrtf(sx * sx + sy * sy + sz * sz) + 1e-9f, spd = A->sp[i] ? 20.f : 18.f;
        A->vx[i] += (sx / sn * spd - A->vx[i]) * .4f; A->vy[i] += (sy / sn * spd - A->vy[i]) * .4f; A->vz[i] += (sz / sn * spd - A->vz[i]) * .4f;
        A->x[i] += A->vx[i] * dt; A->y[i] += A->vy[i] * dt; A->z[i] += A->vz[i] * dt;
        float rr = sqrtf(A->x[i] * A->x[i] + A->y[i] * A->y[i] + A->z[i] * A->z[i]);
        if (rr > R * .985f) { float s = R * .985f / rr; A->x[i] *= s; A->y[i] *= s; A->z[i] *= s; }
        float burn = (A->sp[i] ? 0.06f : 0.04f) * dt;
        if (burn > A->E[i]) burn = A->E[i];
        A->E[i] -= burn; Nut[r] += burn;
    }
}

// ---- macro: cohorts (R regions x C slots x 2 species), the per-cohort work of one macro step ---------------
#define C 10
static inline float ndtrf(float x) { return 0.5f * erfcf(-x * 0.70710678f); }
static inline int binom(int n, float p) {      // normal approx above 30, exact Bernoulli sum below
    if (n <= 0 || p <= 0) return 0;
    if (n < 30) { int k = 0; for (int i = 0; i < n; i++) k += ur() < p; return k; }
    float m = n * p, s = sqrtf(m * (1 - p)), u1 = ur() + 1e-7f, u2 = ur();
    int k = (int)lrintf(m + s * sqrtf(-2 * logf(u1)) * cosf(6.2831853f * u2));
    return k < 0 ? 0 : k > n ? n : k;
}
typedef struct { int N[4]; float S, Q; } Cohort;

static void macro_tick(Cohort *co, int nreg, float dt) {
    for (int r = 0; r < nreg; r++) {
        // flora field: logistic + nutrient, 64 voxels
        float lim = Nut[r] / (Nut[r] + 4000.f), mean = 0;
        for (int v = 0; v < NV; v++) mean += F[r * NV + v];
        mean /= NV;
        float tot = 0;
        for (int v = 0; v < NV; v++) {
            float f = F[r * NV + v], room = 1.f - (f + K[r * NV + v]) / 120.f;
            float g = 0.006f * (f + 0.02f * mean) * (room > 0 ? room : 0) * lim * dt;
            F[r * NV + v] += g; tot += g;
        }
        Nut[r] -= tot;
        for (int s = 0; s < 2; s++) {
            Cohort *c = co + ((size_t)r * 2 + s) * C;
            float eff = 0;
            for (int k = 0; k < C; k++) { int n = c[k].N[0] + c[k].N[1] + c[k].N[2] + c[k].N[3]; if (n) eff += n * (1 - c[k].S / n / 32.f); }
            float fpr = eff > 0 ? 0.1f : 0;
            for (int k = 0; k < C; k++) {
                int n = c[k].N[0] + c[k].N[1] + c[k].N[2] + c[k].N[3];
                if (!n) continue;
                float m = c[k].S / n, var = c[k].Q / n - m * m; if (var < 1e-6f) var = 1e-6f;
                float gain = fpr * (1 - m / 32.f) * dt - 0.04f * dt;
                c[k].Q += 2 * gain * c[k].S + n * gain * gain; c[k].S += n * gain;
                float sd = sqrtf(var), a = (20.f - m) / sd;
                float pb = 1 - ndtrf(a), pd = ndtrf(-m / sd);
                int kb = binom(n, pb), kd = binom(n - kb, pd);
                // tail moments (the closed forms cost an exp + an erfc)
                float lam = expf(-0.5f * a * a) * 0.39894f / (pb + 1e-30f);
                c[k].S -= kb * (m + sd * lam); c[k].N[0] -= kd > c[k].N[0] ? c[k].N[0] : kd;
                // hops: 6 neighbours, one binomial each
                for (int d = 0; d < 6; d++) { int h = binom(n, 0.005f * dt); c[k].N[1] -= h > c[k].N[1] ? c[k].N[1] : h; }
            }
        }
    }
}

int main(void) {
    for (int i = 0; i < GN * GN * GN * NV; i++) F[i] = 60.f * ur();
    for (int i = 0; i < GN * GN * GN; i++) Nut[i] = 3000.f;
    int sizes[] = {1000, 2000, 5000, 10000, 20000};
    printf("MICRO tick (agents clustered within 450 u of a pilot, 10%% predators)\n");
    for (int t = 0; t < 5; t++) {
        int n = sizes[t];
        Agents A = {n};
        float **fs[] = {&A.x, &A.y, &A.z, &A.vx, &A.vy, &A.vz, &A.hx, &A.hy, &A.hz, &A.E};
        for (int f = 0; f < 10; f++) *fs[f] = calloc(n, sizeof(float));
        A.sp = calloc(n, 1); A.cell = calloc(n, sizeof(int)); A.order = calloc(n, sizeof(int)); A.start = calloc(HN * HN * HN + 1, sizeof(int));
        for (int i = 0; i < n; i++) {
            float u = ur(), v = ur(), w = cbrtf(ur()) * 450.f, th = 6.2831853f * u, ph = acosf(2 * v - 1);
            A.x[i] = w * sinf(ph) * cosf(th) + 300; A.y[i] = w * sinf(ph) * sinf(th); A.z[i] = w * cosf(ph);
            A.sp[i] = (i % 10) == 0; A.E[i] = A.sp[i] ? 60 : 12; A.hx[i] = 1;
        }
        for (int w = 0; w < 5; w++) micro_tick(&A, 0.1f);
        int reps = 200; double t0 = now();
        for (int w = 0; w < reps; w++) micro_tick(&A, 0.1f);
        double dtm = (now() - t0) / reps;
        printf("  agents %6d  %.3f ms/tick  %.1f ns/agent  -> at 10 Hz: %.3f ms per second of game (%.4f ms per 60fps frame)\n",
               n, dtm * 1e3, dtm * 1e9 / n, dtm * 1e4, dtm * 1e4 / 60);
    }
    int nreg = 912;
    Cohort *co = calloc((size_t)nreg * 2 * C, sizeof(Cohort));
    for (int i = 0; i < nreg * 2 * C; i++) { if (ur() < 0.6f) { for (int e = 0; e < 4; e++) co[i].N[e] = 3; co[i].S = 12 * 12; co[i].Q = 12 * 12 * 12 * 1.02f; } }
    for (int w = 0; w < 3; w++) macro_tick(co, nreg, 1.0f);
    int reps = 100; double t0 = now();
    for (int w = 0; w < reps; w++) macro_tick(co, nreg, 1.0f);
    double dtm = (now() - t0) / reps;
    printf("MACRO tick (912 regions x 10 cohorts x 2 species + 58k flora voxels): %.3f ms per macro step (1 Hz)"
           " = %.1f ns per cohort slot; amortised %.4f ms per 60 fps frame\n", dtm * 1e3, dtm * 1e9 / (nreg * 2 * C), dtm * 1e3 / 60);
    return 0;
}
