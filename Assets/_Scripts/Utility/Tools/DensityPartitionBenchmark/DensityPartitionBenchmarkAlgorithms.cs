using System.Collections.Generic;
using System.Diagnostics;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.Tools.DensityPartitionBenchmark
{
    /// <summary>
    /// Result of one (algorithm, query) pair.
    /// </summary>
    public struct BenchmarkResult
    {
        /// <summary>World-space answer the algorithm picked as "the densest point".</summary>
        public Vector3 Location;

        /// <summary>The density value at that location, as the algorithm itself measured it (count or mass-weighted).</summary>
        public float Density;

        /// <summary>True if the algorithm declared "no data" (empty input). Used to avoid bogus error metrics.</summary>
        public bool Empty;

        /// <summary>Wall-clock time for this single query, in milliseconds.</summary>
        public double ElapsedMs;
    }

    /// <summary>
    /// Parameter knobs for the unified Search algorithm. Every benchmark variant
    /// is one combination of these options. Keeping the implementation in a single
    /// method ensures the variants differ only along the dimensions named here -
    /// no hidden divergence between, say, the smoothing pass in GridSmoothed vs.
    /// MassHistogramSmoothed.
    /// </summary>
    public struct SearchOptions
    {
        /// <summary>Voxel count per axis (cubic grid). 17 matches production BlockDensityGrid.</summary>
        public int gridSize;

        /// <summary>If true, sum prism Volume; if false, count 1 per prism.</summary>
        public bool massWeighted;

        /// <summary>If true, apply a separable box filter before argmax.</summary>
        public bool smoothed;

        /// <summary>
        /// Physical radius (m) of the smoothing kernel. The box-filter half-width
        /// in voxels is round(smoothingRadiusM / stride), so finer grids get
        /// wider kernels and the effective smoothing scale stays constant.
        /// </summary>
        public float smoothingRadiusM;

        /// <summary>
        /// If true, refine the argmax voxel via parabolic interpolation through
        /// the peak and its 6 axis-aligned neighbors. Drops accuracy floor from
        /// grid-stride quantization to sub-voxel precision.
        /// </summary>
        public bool subVoxelInterp;

        /// <summary>
        /// If true, after argmax+interp, do a final O(N) pass: compute the
        /// (mass-weighted, if massWeighted=true) centroid of all prisms within
        /// smoothingRadiusM of the interp answer. Grounds the answer in the
        /// actual prism positions instead of voxel centers - drops error toward
        /// the analytical centroid floor at the cost of one extra prism scan.
        /// </summary>
        public bool centroidRefine;

        /// <summary>
        /// Number of iterations of centroid refinement when centroidRefine=true.
        /// >1 = mean-shift: re-take the centroid with the previous answer as the
        /// new seed. Converges to a local fixed point of the kernel. Typically
        /// converges in 3-5 iterations. Ignored when centroidRefine=false.
        /// Default 1 = single-pass centroid (the iteration-2 behavior).
        /// </summary>
        public int centroidIterations;

        /// <summary>
        /// When > 0, the grid is built with this fixed half-extent (origin at
        /// -fixedHalfExtentM) instead of adapting to the scenario's worldHalfExtent.
        /// Models the *actual shipped* BlockDensityGrid, which is hard-coded to a
        /// 1000m cube (±500m) regardless of the cell's real size. Prisms outside
        /// the fixed grid are silently dropped - exactly the production behavior.
        /// With a 1200m-radius cell, this drops ~86% of the cell volume's prisms.
        /// 0 = adaptive grid sized to the data (what every other variant does).
        /// </summary>
        public float fixedHalfExtentM;

        /// <summary>
        /// When > 0, gridSize is IGNORED and the resolution is derived from the
        /// world extent so each voxel is ~this many metres on a side, clamped to
        /// [9, 33] points per axis. Mirrors production BlockDensityGrid's
        /// TargetVoxelSizeMeters-driven adaptive resolution: voxel size is a
        /// physical constant, not a fraction of cell size.
        /// </summary>
        public float targetVoxelSizeM;

        /// <summary>
        /// If true, refine via mean-shift over the RAW VOXEL HISTOGRAM (count-
        /// weighted voxel centers within smoothingRadiusM), iterated
        /// centroidIterations times. This is what production FindDensestRegionJob
        /// does - the grid stores only counts, so prism-position-based centroid
        /// refinement (centroidRefine) is not available to it. Mutually exclusive
        /// with centroidRefine; if both are set, voxelMeanShift wins.
        /// </summary>
        public bool voxelMeanShift;
    }

    /// <summary>
    /// Algorithms are pure functions over the (prism list, query) pair. Each returns
    /// a BenchmarkResult timed with Stopwatch - single-shot, so noise is non-trivial;
    /// the runner does a warm-up pass to reduce JIT bias.
    ///
    /// Conventions:
    ///   excludeDomain = null   → "all-domain" query (densest of everything)
    ///   excludeDomain = X      → "anti-X" query    (densest of everything that's not X)
    /// </summary>
    public static class DensityPartitionBenchmarkAlgorithms
    {
        // Production grid constants (BlockDensityGrid defaults). Variant constructors
        // use these for the production-matching baselines.
        public const float ProductionStride = 60f;
        public const int ProductionGridCells = 17;
        public const float ProductionExtent = 500f;

        // Default kernel radius = the swarm-effective consumption volume. A 4-fauna
        // swarm at consumeRadius=40m, goalOrbitRadius=60m, plus boid separation
        // spread covers roughly a 150m-radius region around the goal point. The
        // algorithm only has to land within this of enemy mass - the swarm sweeps
        // the rest. (Was 90m through iteration 3, before the hierarchical
        // fauna→swarm→population analysis re-scoped it.)
        public const float DefaultSmoothingRadiusM = 150f;

        // ==================================================================
        //  Variant constructors - named for the report.
        // ==================================================================

        /// <summary>
        /// The LITERAL shipped algorithm: 17³ count grid hard-fixed at ±500m,
        /// argmax, no smoothing/interp. Prisms outside ±500m are dropped exactly
        /// as BlockCountDensityGrid.AddBlock's bounds check drops them. This is
        /// the only variant that reproduces the production grid-undersizing bug -
        /// every other variant adapts its grid to the data.
        /// </summary>
        public static SearchOptions ProductionFixedGrid17() => new SearchOptions
        {
            gridSize = ProductionGridCells,
            massWeighted = false,
            smoothed = false,
            smoothingRadiusM = 0f,
            subVoxelInterp = false,
            fixedHalfExtentM = ProductionExtent, // 500m - the shipped constant
        };

        public static SearchOptions GridArgmax17() => new SearchOptions
        {
            gridSize = ProductionGridCells,
            massWeighted = false,
            smoothed = false,
            smoothingRadiusM = 0f,
            subVoxelInterp = false,
        };

        public static SearchOptions GridSmoothed17() => new SearchOptions
        {
            gridSize = ProductionGridCells,
            massWeighted = false,
            smoothed = true,
            smoothingRadiusM = DefaultSmoothingRadiusM,
            subVoxelInterp = false,
        };

        public static SearchOptions GridSmoothedInterp17() => new SearchOptions
        {
            gridSize = ProductionGridCells,
            massWeighted = false,
            smoothed = true,
            smoothingRadiusM = DefaultSmoothingRadiusM,
            subVoxelInterp = true,
        };

        public static SearchOptions GridMassSmoothedInterp17() => new SearchOptions
        {
            gridSize = ProductionGridCells,
            massWeighted = true,
            smoothed = true,
            smoothingRadiusM = DefaultSmoothingRadiusM,
            subVoxelInterp = true,
        };

        public static SearchOptions GridMassSmoothedInterp32() => new SearchOptions
        {
            gridSize = 32,
            massWeighted = true,
            smoothed = true,
            smoothingRadiusM = DefaultSmoothingRadiusM,
            subVoxelInterp = true,
        };

        public static SearchOptions GridMassSmoothedInterpCentroid17() => new SearchOptions
        {
            gridSize = ProductionGridCells,
            massWeighted = true,
            smoothed = true,
            smoothingRadiusM = DefaultSmoothingRadiusM,
            subVoxelInterp = true,
            centroidRefine = true,
            centroidIterations = 1,
        };

        public static SearchOptions GridMassSmoothedInterpCentroid32() => new SearchOptions
        {
            gridSize = 32,
            massWeighted = true,
            smoothed = true,
            smoothingRadiusM = DefaultSmoothingRadiusM,
            subVoxelInterp = true,
            centroidRefine = true,
            centroidIterations = 1,
        };

        public static SearchOptions GridMassSmoothedInterpMeanShift17() => new SearchOptions
        {
            gridSize = ProductionGridCells,
            massWeighted = true,
            smoothed = true,
            smoothingRadiusM = DefaultSmoothingRadiusM,
            subVoxelInterp = true,
            centroidRefine = true,
            centroidIterations = 5,
        };

        public static SearchOptions GridMassSmoothedInterpMeanShift32() => new SearchOptions
        {
            gridSize = 32,
            massWeighted = true,
            smoothed = true,
            smoothingRadiusM = DefaultSmoothingRadiusM,
            subVoxelInterp = true,
            centroidRefine = true,
            centroidIterations = 5,
        };

        /// <summary>
        /// The EXACT Phase-2 production pipeline (BlockDensityGrid after the
        /// audit's §7.3 fix): adaptive resolution targeting 75m voxels (clamped
        /// 9..33 points/axis), count-weighted, box smoothing at 150m, sub-voxel
        /// interp, then 5 iterations of mean-shift over the RAW VOXEL HISTOGRAM
        /// (not prism positions - the production grid only stores counts).
        /// Run this row to know what production will actually answer.
        /// </summary>
        public static SearchOptions ProductionV2() => new SearchOptions
        {
            gridSize = 0, // derived from targetVoxelSizeM
            targetVoxelSizeM = 75f,
            massWeighted = false,
            smoothed = true,
            smoothingRadiusM = DefaultSmoothingRadiusM,
            subVoxelInterp = true,
            voxelMeanShift = true,
            centroidIterations = 5,
        };

        // ==================================================================
        //  Unified Search() - implements every variant.
        // ==================================================================

        public static BenchmarkResult Search(
            IReadOnlyList<BenchmarkPrism> prisms,
            Domains? excludeDomain,
            float worldHalfExtent,
            SearchOptions opt)
        {
            var sw = Stopwatch.StartNew();
            if (prisms == null || prisms.Count == 0)
                return new BenchmarkResult { Empty = true, ElapsedMs = sw.Elapsed.TotalMilliseconds };

            // fixedHalfExtentM > 0 models the shipped BlockDensityGrid, which is
            // hard-coded to a 1000m cube (±500m) regardless of cell size. Prisms
            // outside that box fall out of TryMapToIndex's bounds check and are
            // silently dropped - exactly the production behavior.
            float effectiveHalfExtent = opt.fixedHalfExtentM > 0f ? opt.fixedHalfExtentM : worldHalfExtent;

            // targetVoxelSizeM > 0 derives the resolution from the extent so voxel
            // size is a physical constant - production BlockDensityGrid's adaptive
            // resolution. Otherwise the explicit gridSize is used.
            int N = opt.targetVoxelSizeM > 0f
                ? Mathf.Clamp(Mathf.CeilToInt(effectiveHalfExtent * 2f / opt.targetVoxelSizeM) + 1, 9, 33)
                : opt.gridSize;

            float stride = effectiveHalfExtent * 2f / N;
            float origin = -effectiveHalfExtent;
            int len = N * N * N;

            // Phase 1: bin (pooled allocation - see RentArray/ReturnArray below)
            float[] hist = RentArray(len);
            try
            {
                for (int i = 0; i < prisms.Count; i++)
                {
                    var p = prisms[i];
                    if (excludeDomain.HasValue && p.Domain == excludeDomain.Value) continue;
                    int xi, yi, zi;
                    if (!TryMapToIndex(p.Position, origin, stride, N, out xi, out yi, out zi)) continue;
                    hist[xi + yi * N + zi * N * N] += opt.massWeighted ? p.Volume : 1f;
                }

                // Phase 2: optional smoothing (separable box filter, pooled scratch)
                float[] field = hist;
                float[] scratch = null;
                if (opt.smoothed)
                {
                    int kernelHalfWidth = Mathf.Max(1, Mathf.RoundToInt(opt.smoothingRadiusM / stride));
                    scratch = RentArray(len);
                    field = SeparableBoxFilter3D(hist, scratch, N, kernelHalfWidth);
                }

                try
                {
                    // Phase 3: argmax
                    float best = -1f;
                    int bestX = 0, bestY = 0, bestZ = 0;
                    for (int z = 0; z < N; z++)
                    for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float v = field[x + y * N + z * N * N];
                        if (v > best) { best = v; bestX = x; bestY = y; bestZ = z; }
                    }

                    if (best <= 0f)
                        return new BenchmarkResult { Empty = true, ElapsedMs = sw.Elapsed.TotalMilliseconds };

                    // Phase 4: optional sub-voxel parabolic interpolation
                    float dx = 0f, dy = 0f, dz = 0f;
                    if (opt.subVoxelInterp)
                    {
                        dx = ParabolicOffset(SampleAxis(field, N, bestX, bestY, bestZ, axis: 0));
                        dy = ParabolicOffset(SampleAxis(field, N, bestX, bestY, bestZ, axis: 1));
                        dz = ParabolicOffset(SampleAxis(field, N, bestX, bestY, bestZ, axis: 2));
                    }

                    Vector3 loc = new Vector3(
                        origin + (bestX + dx) * stride,
                        origin + (bestY + dy) * stride,
                        origin + (bestZ + dz) * stride);

                    // Phase 5a: voxel-weighted mean-shift - the PRODUCTION refinement.
                    // Iteratively moves the answer to the count-weighted centroid of
                    // RAW HISTOGRAM voxels within the kernel radius. The production
                    // grid stores only counts (no prism positions), so this is the
                    // refinement FindDensestRegionJob can actually do. Takes priority
                    // over centroidRefine when both are set.
                    if (opt.voxelMeanShift)
                    {
                        int iters = Mathf.Max(1, opt.centroidIterations);
                        float msR = opt.smoothingRadiusM / stride; // voxel units
                        float msR2 = msR * msR;
                        Vector3 seed = new Vector3(bestX + dx, bestY + dy, bestZ + dz);
                        for (int it = 0; it < iters; it++)
                        {
                            int x0 = Mathf.Max(0, Mathf.FloorToInt(seed.x - msR));
                            int x1 = Mathf.Min(N - 1, Mathf.CeilToInt(seed.x + msR));
                            int y0 = Mathf.Max(0, Mathf.FloorToInt(seed.y - msR));
                            int y1 = Mathf.Min(N - 1, Mathf.CeilToInt(seed.y + msR));
                            int z0 = Mathf.Max(0, Mathf.FloorToInt(seed.z - msR));
                            int z1 = Mathf.Min(N - 1, Mathf.CeilToInt(seed.z + msR));

                            Vector3 weighted = Vector3.zero;
                            float total = 0f;
                            for (int z = z0; z <= z1; z++)
                            for (int y = y0; y <= y1; y++)
                            for (int x = x0; x <= x1; x++)
                            {
                                float v = hist[x + y * N + z * N * N]; // RAW counts, not smoothed
                                if (v <= 0f) continue;
                                Vector3 p = new Vector3(x, y, z);
                                if ((p - seed).sqrMagnitude > msR2) continue;
                                weighted += p * v;
                                total += v;
                            }

                            if (total < 1e-3f) break;
                            Vector3 next = weighted / total;
                            if ((next - seed).sqrMagnitude < 1e-6f) { seed = next; break; }
                            seed = next;
                        }
                        loc = new Vector3(
                            origin + seed.x * stride,
                            origin + seed.y * stride,
                            origin + seed.z * stride);
                    }
                    // Phase 5b: prism-position centroid refinement - the IDEAL
                    // refinement (requires prism positions, which production's grid
                    // does not store). Kept as the accuracy ceiling to compare
                    // voxelMeanShift against.
                    else if (opt.centroidRefine)
                    {
                        int iters = Mathf.Max(1, opt.centroidIterations);
                        for (int it = 0; it < iters; it++)
                        {
                            Vector3 prev = loc;
                            loc = CentroidWithinRadius(prisms, excludeDomain, loc, opt.smoothingRadiusM, opt.massWeighted, fallback: loc);
                            if ((loc - prev).sqrMagnitude < 0.25f) break; // 0.5m converged
                        }
                    }

                    sw.Stop();
                    return new BenchmarkResult
                    {
                        Location = loc,
                        Density = best,
                        Empty = false,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    };
                }
                finally
                {
                    if (scratch != null) ReturnArray(scratch);
                }
            }
            finally
            {
                ReturnArray(hist);
            }
        }

        /// <summary>
        /// Weighted centroid of prisms within radius of `seed`. Box kernel matches
        /// the GroundTruth + ScoreLocation conventions, so the answer is consistent
        /// with how mass% is rescored. Returns `fallback` when no prisms qualify.
        /// </summary>
        static Vector3 CentroidWithinRadius(
            IReadOnlyList<BenchmarkPrism> prisms,
            Domains? excludeDomain,
            Vector3 seed,
            float radius,
            bool massWeighted,
            Vector3 fallback)
        {
            Vector3 acc = Vector3.zero;
            float wsum = 0f;
            for (int i = 0; i < prisms.Count; i++)
            {
                var p = prisms[i];
                if (excludeDomain.HasValue && p.Domain == excludeDomain.Value) continue;
                Vector3 diff = p.Position - seed;
                if (Mathf.Abs(diff.x) > radius) continue;
                if (Mathf.Abs(diff.y) > radius) continue;
                if (Mathf.Abs(diff.z) > radius) continue;
                float w = massWeighted ? p.Volume : 1f;
                acc += p.Position * w;
                wsum += w;
            }
            return wsum > 0f ? acc / wsum : fallback;
        }

        // ==================================================================
        //  Ground truth - histogram + smoothing at finer resolution.
        //
        //  Was: brute-force scan over ~35^3 candidates × N prisms = O(N × C)
        //  Now: bin once, smooth, argmax. O(N + 3·G^3) with G=64.
        //  ~100× speedup at N=2000.
        //
        //  Conceptually: as G → ∞ and kernel → matched, this converges to the
        //  brute-force answer. At G=64 (stride 15.6m) and a 12-voxel half-kernel
        //  the box approximates a 90m sphere closely enough for "where is the peak"
        //  purposes.
        // ==================================================================

        public static BenchmarkResult GroundTruth(
            IReadOnlyList<BenchmarkPrism> prisms,
            Domains? excludeDomain,
            float worldHalfExtent,
            float smoothingRadius,
            bool massWeighted)
        {
            var opt = new SearchOptions
            {
                gridSize = 64,
                massWeighted = massWeighted,
                smoothed = true,
                smoothingRadiusM = smoothingRadius,
                subVoxelInterp = true,
            };
            return Search(prisms, excludeDomain, worldHalfExtent, opt);
        }

        // ==================================================================
        //  Helpers
        // ==================================================================

        static bool TryMapToIndex(Vector3 pos, float origin, float stride, int N, out int x, out int y, out int z)
        {
            x = Mathf.RoundToInt((pos.x - origin) / stride);
            y = Mathf.RoundToInt((pos.y - origin) / stride);
            z = Mathf.RoundToInt((pos.z - origin) / stride);
            return x >= 0 && x < N && y >= 0 && y < N && z >= 0 && z < N;
        }

        /// <summary>
        /// Pull 3 samples along the named axis through (xi, yi, zi). Returns
        /// (a, b, c) = (f(idx-1), f(idx), f(idx+1)) for parabolic fit. Returns
        /// (b, b, b) if at boundary (no fit possible).
        /// </summary>
        static Vector3 SampleAxis(float[] field, int N, int xi, int yi, int zi, int axis)
        {
            int dx = axis == 0 ? 1 : 0;
            int dy = axis == 1 ? 1 : 0;
            int dz = axis == 2 ? 1 : 0;
            int bx = xi, by = yi, bz = zi;
            int b = field[bx + by * N + bz * N * N] != 0f ? 0 : 0; // suppress unused warning if any
            float B = field[bx + by * N + bz * N * N];

            int ax = bx - dx, ay = by - dy, az = bz - dz;
            int cx = bx + dx, cy = by + dy, cz = bz + dz;

            bool aIn = ax >= 0 && ax < N && ay >= 0 && ay < N && az >= 0 && az < N;
            bool cIn = cx >= 0 && cx < N && cy >= 0 && cy < N && cz >= 0 && cz < N;
            if (!aIn || !cIn) return new Vector3(B, B, B);

            float A = field[ax + ay * N + az * N * N];
            float C = field[cx + cy * N + cz * N * N];
            return new Vector3(A, B, C);
        }

        /// <summary>
        /// Continuous offset of a parabolic peak fit through (a,b,c) at indices
        /// (-1, 0, +1). Returns 0 when the fit is degenerate (no curvature) or
        /// the peak isn't really at b. Clamped to [-0.5, 0.5] - beyond that the
        /// peak should have been at a neighboring voxel anyway.
        /// </summary>
        static float ParabolicOffset(Vector3 abc)
        {
            float a = abc.x, b = abc.y, c = abc.z;
            // Peak must actually be at b
            if (b < a || b < c) return 0f;
            float denom = a - 2f * b + c;
            if (Mathf.Abs(denom) < 1e-6f) return 0f;
            float off = 0.5f * (a - c) / denom;
            return Mathf.Clamp(off, -0.5f, 0.5f);
        }

        /// <summary>
        /// Separable 3D box filter using a sliding window. O(N³) per pass × 3 passes,
        /// independent of kernel width - works as well for 3³ kernels as for 25³ ones.
        /// Edge voxels use a partial-window sum (no zero-padding shrink).
        ///
        /// `scratch` is a caller-provided buffer of the same length as `input`; it's
        /// overwritten during the X/Y passes and ends up holding intermediate state.
        /// Return value is one of {input, scratch} depending on pass parity; both
        /// buffers' final contents are valid for the caller to read or recycle.
        /// </summary>
        // ──────────────────────────────────────────────────────────────────
        // Array pool - keyed by length, lockless single-threaded LIFO.
        //
        // Iteration 1 allocated ~130 MB of float[] across one full run (5
        // algorithms × 36 queries × up to 3 arrays per Search). The resulting
        // GC pauses caused 5-7ms outliers in the 32³ rows. The pool reuses
        // arrays across queries - typical hot run hits 4 distinct lengths
        // (17³=4913, 32³=32768, 64³=262144, plus an occasional resize).
        // ──────────────────────────────────────────────────────────────────

        static readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.Stack<float[]>> _pool
            = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.Stack<float[]>>();

        static float[] RentArray(int length)
        {
            System.Collections.Generic.Stack<float[]> stack;
            if (_pool.TryGetValue(length, out stack) && stack.Count > 0)
            {
                var arr = stack.Pop();
                System.Array.Clear(arr, 0, length);
                return arr;
            }
            return new float[length];
        }

        static void ReturnArray(float[] arr)
        {
            if (arr == null) return;
            System.Collections.Generic.Stack<float[]> stack;
            if (!_pool.TryGetValue(arr.Length, out stack))
            {
                stack = new System.Collections.Generic.Stack<float[]>(4);
                _pool[arr.Length] = stack;
            }
            stack.Push(arr);
        }

        static float[] SeparableBoxFilter3D(float[] input, float[] scratch, int N, int kernelHalfWidth)
        {
            // We mutate `input` in-place across alternating passes - the caller's
            // expectation is "field" returned, "hist" untouched after this returns.
            // To honor that, we use the input as the "a" buffer (reading from it,
            // overwriting it on the second pass) and scratch as the "b" buffer.
            // Since this function returns into a finally that disposes both, we
            // can clobber `input` freely.
            float[] a = input;
            float[] b = scratch;

            // X pass: a -> b
            for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
            {
                int rowOff = y * N + z * N * N;
                float sum = 0f;
                // Initialize window: indices in [-K..K] clipped to [0..N-1]
                for (int k = 0; k <= kernelHalfWidth && k < N; k++) sum += a[rowOff + k];
                b[rowOff + 0] = sum;
                for (int x = 1; x < N; x++)
                {
                    int addIdx = x + kernelHalfWidth;
                    int rmIdx = x - kernelHalfWidth - 1;
                    if (addIdx < N) sum += a[rowOff + addIdx];
                    if (rmIdx >= 0) sum -= a[rowOff + rmIdx];
                    b[rowOff + x] = sum;
                }
            }
            // Y pass: b -> a
            for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                int colOff = x + z * N * N;
                float sum = 0f;
                for (int k = 0; k <= kernelHalfWidth && k < N; k++) sum += b[colOff + k * N];
                a[colOff + 0 * N] = sum;
                for (int y = 1; y < N; y++)
                {
                    int addIdx = y + kernelHalfWidth;
                    int rmIdx = y - kernelHalfWidth - 1;
                    if (addIdx < N) sum += b[colOff + addIdx * N];
                    if (rmIdx >= 0) sum -= b[colOff + rmIdx * N];
                    a[colOff + y * N] = sum;
                }
            }
            // Z pass: a -> b
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int depthOff = x + y * N;
                float sum = 0f;
                for (int k = 0; k <= kernelHalfWidth && k < N; k++) sum += a[depthOff + k * N * N];
                b[depthOff + 0 * N * N] = sum;
                for (int z = 1; z < N; z++)
                {
                    int addIdx = z + kernelHalfWidth;
                    int rmIdx = z - kernelHalfWidth - 1;
                    if (addIdx < N) sum += a[depthOff + addIdx * N * N];
                    if (rmIdx >= 0) sum -= a[depthOff + rmIdx * N * N];
                    b[depthOff + z * N * N] = sum;
                }
            }
            return b;
        }
    }
}
