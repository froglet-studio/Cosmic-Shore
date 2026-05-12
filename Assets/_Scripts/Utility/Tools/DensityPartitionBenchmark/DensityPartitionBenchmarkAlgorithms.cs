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
    /// Algorithms are pure functions over the (prism list, query) pair. Each returns a
    /// BenchmarkResult. The runner times them with Stopwatch — single-shot, so noise
    /// is non-trivial; the report includes a "warm" pass to reduce JIT startup bias.
    ///
    /// Conventions:
    ///   excludeDomain = null   → "all-domain" query (densest of everything)
    ///   excludeDomain = X      → "anti-X" query    (densest of everything that's not X)
    ///
    /// Grid bounds default to ±worldHalfExtent. Stride matches Cell's production
    /// BlockCountDensityGrid (60m, 17 cells per axis), so GridArgmax is exactly the
    /// algorithm that runs in production today.
    /// </summary>
    public static class DensityPartitionBenchmarkAlgorithms
    {
        // ------------------------------------------------------------------
        // Production-equivalent constants. Match BlockDensityGrid defaults
        // so GridArgmax results are directly comparable to runtime behavior.
        // ------------------------------------------------------------------
        public const float ProductionStride = 60f;
        public const int ProductionGridCells = 17;          // 1000 / 60 + 1
        public const float ProductionExtent = 500f;         // half of totalLength=1000

        // Recommended-system histogram resolution. See audit §5 q3.
        public const int RecommendedGridCells = 32;

        // ==================================================================
        //  0. Ground truth — brute force, slow but correct.
        // ==================================================================

        /// <summary>
        /// Ground truth: the answer every algorithm is graded against. Iterates over
        /// candidate centers spaced at ~Stride/3, counting (or volume-weighting) the
        /// prisms within smoothingRadius. Pick the candidate with the highest score.
        ///
        /// O(N × C) where C is the candidate count. Slow — but the benchmark only
        /// runs it once per scenario per query, so total time is bounded.
        /// </summary>
        public static BenchmarkResult GroundTruth(
            IReadOnlyList<BenchmarkPrism> prisms,
            Domains? excludeDomain,
            float worldHalfExtent,
            float smoothingRadius,
            bool massWeighted)
        {
            var sw = Stopwatch.StartNew();
            if (prisms == null || prisms.Count == 0)
                return new BenchmarkResult { Empty = true, ElapsedMs = sw.Elapsed.TotalMilliseconds };

            // Candidate grid: ProductionStride/2 spacing for a finer search than the
            // production grid (so production can score nonzero error) but coarse enough
            // that a 6-scenario report completes in seconds in pure C#. Tunable by
            // editing this constant if the report needs sub-30m precision.
            float candStride = ProductionStride / 2f;
            int candPerAxis = Mathf.Max(8, Mathf.CeilToInt(worldHalfExtent * 2 / candStride));
            float candOrigin = -worldHalfExtent;

            float bestScore = -1f;
            Vector3 bestPos = Vector3.zero;
            float r2 = smoothingRadius * smoothingRadius;

            for (int xi = 0; xi <= candPerAxis; xi++)
            for (int yi = 0; yi <= candPerAxis; yi++)
            for (int zi = 0; zi <= candPerAxis; zi++)
            {
                Vector3 c = new Vector3(
                    candOrigin + xi * candStride,
                    candOrigin + yi * candStride,
                    candOrigin + zi * candStride);

                float score = 0f;
                for (int i = 0; i < prisms.Count; i++)
                {
                    var p = prisms[i];
                    if (excludeDomain.HasValue && p.Domain == excludeDomain.Value) continue;
                    if ((p.Position - c).sqrMagnitude > r2) continue;
                    score += massWeighted ? p.Volume : 1f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPos = c;
                }
            }

            sw.Stop();
            return new BenchmarkResult
            {
                Location = bestPos,
                Density = Mathf.Max(0f, bestScore),
                Empty = bestScore <= 0f,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        // ==================================================================
        //  1. GridArgmax — current production behavior.
        //
        //  Mirrors Cell.countGrids[D].FindDensestRegion() exactly:
        //    - 17^3 byte grid, stride=60, anchored at -500.
        //    - For an anti-D query, increment every prism in domain != D into
        //      the grid by 1.
        //    - argmax over voxels, return the voxel center.
        //
        //  The "wrong" half of the per-Cell bookkeeping (only count blocks
        //  "not in D") is built into the loop, so this is byte-for-byte the
        //  algorithm a fauna at Cell.GetExplosionTarget(D) sees today.
        // ==================================================================

        public static BenchmarkResult GridArgmax(
            IReadOnlyList<BenchmarkPrism> prisms,
            Domains? excludeDomain,
            float worldHalfExtent)
        {
            var sw = Stopwatch.StartNew();
            if (prisms == null || prisms.Count == 0)
                return new BenchmarkResult { Empty = true, ElapsedMs = sw.Elapsed.TotalMilliseconds };

            int N = ProductionGridCells;
            float stride = ProductionStride;
            float origin = -worldHalfExtent;
            int[] counts = new int[N * N * N];

            // Fill grid (count-only, matching BlockCountDensityGrid).
            for (int i = 0; i < prisms.Count; i++)
            {
                var p = prisms[i];
                if (excludeDomain.HasValue && p.Domain == excludeDomain.Value) continue;
                Vector3Int idx = MapToIndex(p.Position, origin, stride);
                if (!InBounds(idx, N)) continue;
                counts[Flatten(idx, N)] += 1;
            }

            int best = -1;
            int bestIdx = 0;
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] > best) { best = counts[i]; bestIdx = i; }

            Vector3 loc = best > 0 ? Unflatten(bestIdx, N, origin, stride) : Vector3.zero;
            sw.Stop();
            return new BenchmarkResult
            {
                Location = loc,
                Density = Mathf.Max(0, best),
                Empty = best <= 0,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        // ==================================================================
        //  2. GridSmoothed — current + 3x3x3 box smoothing.
        //
        //  This is what the sibling branch (claude/density-partitioning-sync-6OXTO,
        //  per the task brief) presumably implements as its kernel smoothing pass.
        //  Same 17^3 byte grid → box-filter → argmax.
        // ==================================================================

        public static BenchmarkResult GridSmoothed(
            IReadOnlyList<BenchmarkPrism> prisms,
            Domains? excludeDomain,
            float worldHalfExtent)
        {
            var sw = Stopwatch.StartNew();
            if (prisms == null || prisms.Count == 0)
                return new BenchmarkResult { Empty = true, ElapsedMs = sw.Elapsed.TotalMilliseconds };

            int N = ProductionGridCells;
            float stride = ProductionStride;
            float origin = -worldHalfExtent;
            int[] counts = new int[N * N * N];

            for (int i = 0; i < prisms.Count; i++)
            {
                var p = prisms[i];
                if (excludeDomain.HasValue && p.Domain == excludeDomain.Value) continue;
                Vector3Int idx = MapToIndex(p.Position, origin, stride);
                if (!InBounds(idx, N)) continue;
                counts[Flatten(idx, N)] += 1;
            }

            // 3x3x3 box filter into a parallel buffer.
            int[] smoothed = new int[counts.Length];
            for (int x = 0; x < N; x++)
            for (int y = 0; y < N; y++)
            for (int z = 0; z < N; z++)
            {
                int sum = 0;
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int xi = x + dx, yi = y + dy, zi = z + dz;
                    if (xi < 0 || xi >= N || yi < 0 || yi >= N || zi < 0 || zi >= N) continue;
                    sum += counts[xi + yi * N + zi * N * N];
                }
                smoothed[x + y * N + z * N * N] = sum;
            }

            int best = -1;
            int bestIdx = 0;
            for (int i = 0; i < smoothed.Length; i++)
                if (smoothed[i] > best) { best = smoothed[i]; bestIdx = i; }

            Vector3 loc = best > 0 ? Unflatten(bestIdx, N, origin, stride) : Vector3.zero;
            sw.Stop();
            return new BenchmarkResult
            {
                Location = loc,
                Density = Mathf.Max(0, best),
                Empty = best <= 0,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        // ==================================================================
        //  3. GridCentroid — production grid, count-weighted centroid.
        //
        //  Baseline alternative reduction for the same grid storage. Stable to
        //  cell quantization but fails on multi-cluster (returns the empty
        //  middle). Included so the report makes the "centroid is wrong here"
        //  failure mode visible.
        // ==================================================================

        public static BenchmarkResult GridCentroid(
            IReadOnlyList<BenchmarkPrism> prisms,
            Domains? excludeDomain,
            float worldHalfExtent)
        {
            var sw = Stopwatch.StartNew();
            if (prisms == null || prisms.Count == 0)
                return new BenchmarkResult { Empty = true, ElapsedMs = sw.Elapsed.TotalMilliseconds };

            int N = ProductionGridCells;
            float stride = ProductionStride;
            float origin = -worldHalfExtent;
            int[] counts = new int[N * N * N];

            for (int i = 0; i < prisms.Count; i++)
            {
                var p = prisms[i];
                if (excludeDomain.HasValue && p.Domain == excludeDomain.Value) continue;
                Vector3Int idx = MapToIndex(p.Position, origin, stride);
                if (!InBounds(idx, N)) continue;
                counts[Flatten(idx, N)] += 1;
            }

            Vector3 acc = Vector3.zero;
            int total = 0;
            for (int x = 0; x < N; x++)
            for (int y = 0; y < N; y++)
            for (int z = 0; z < N; z++)
            {
                int c = counts[x + y * N + z * N * N];
                if (c <= 0) continue;
                acc += new Vector3(origin + x * stride, origin + y * stride, origin + z * stride) * c;
                total += c;
            }

            sw.Stop();
            return new BenchmarkResult
            {
                Location = total > 0 ? acc / total : Vector3.zero,
                Density = total,
                Empty = total <= 0,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        // ==================================================================
        //  4. MassHistogramArgmax — recommended (§3.7 in the audit).
        //
        //  Volume-weighted histogram at the recommended 32^3 resolution.
        //  Conceptually identical to a Burst job over PrismAOERegistry's
        //  NativeArrays — runs in pure C# here for the Edit-Mode benchmark.
        //  Each prism contributes its Volume (not 1) to the histogram bin,
        //  fixing the §2.3.2 count-vs-mass issue.
        // ==================================================================

        public static BenchmarkResult MassHistogramArgmax(
            IReadOnlyList<BenchmarkPrism> prisms,
            Domains? excludeDomain,
            float worldHalfExtent)
        {
            var sw = Stopwatch.StartNew();
            if (prisms == null || prisms.Count == 0)
                return new BenchmarkResult { Empty = true, ElapsedMs = sw.Elapsed.TotalMilliseconds };

            int N = RecommendedGridCells;
            float stride = (worldHalfExtent * 2) / N;
            float origin = -worldHalfExtent;
            float[] mass = new float[N * N * N];

            for (int i = 0; i < prisms.Count; i++)
            {
                var p = prisms[i];
                if (excludeDomain.HasValue && p.Domain == excludeDomain.Value) continue;
                Vector3Int idx = MapToIndex(p.Position, origin, stride);
                if (!InBounds(idx, N)) continue;
                mass[Flatten(idx, N)] += p.Volume;
            }

            float best = -1f;
            int bestIdx = 0;
            for (int i = 0; i < mass.Length; i++)
                if (mass[i] > best) { best = mass[i]; bestIdx = i; }

            Vector3 loc = best > 0 ? Unflatten(bestIdx, N, origin, stride) : Vector3.zero;
            sw.Stop();
            return new BenchmarkResult
            {
                Location = loc,
                Density = Mathf.Max(0f, best),
                Empty = best <= 0,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        // ==================================================================
        //  5. MassHistogramSmoothed — recommended + 3x3x3 smoothing.
        //
        //  The audit's preferred default reduction. 32^3 mass histogram, box
        //  filter, argmax. This is the algorithm the production system should
        //  delegate to once the migration in §4.4 lands.
        // ==================================================================

        public static BenchmarkResult MassHistogramSmoothed(
            IReadOnlyList<BenchmarkPrism> prisms,
            Domains? excludeDomain,
            float worldHalfExtent)
        {
            var sw = Stopwatch.StartNew();
            if (prisms == null || prisms.Count == 0)
                return new BenchmarkResult { Empty = true, ElapsedMs = sw.Elapsed.TotalMilliseconds };

            int N = RecommendedGridCells;
            float stride = (worldHalfExtent * 2) / N;
            float origin = -worldHalfExtent;
            float[] mass = new float[N * N * N];

            for (int i = 0; i < prisms.Count; i++)
            {
                var p = prisms[i];
                if (excludeDomain.HasValue && p.Domain == excludeDomain.Value) continue;
                Vector3Int idx = MapToIndex(p.Position, origin, stride);
                if (!InBounds(idx, N)) continue;
                mass[Flatten(idx, N)] += p.Volume;
            }

            float[] smoothed = new float[mass.Length];
            for (int x = 0; x < N; x++)
            for (int y = 0; y < N; y++)
            for (int z = 0; z < N; z++)
            {
                float sum = 0f;
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int xi = x + dx, yi = y + dy, zi = z + dz;
                    if (xi < 0 || xi >= N || yi < 0 || yi >= N || zi < 0 || zi >= N) continue;
                    sum += mass[xi + yi * N + zi * N * N];
                }
                smoothed[x + y * N + z * N * N] = sum;
            }

            float best = -1f;
            int bestIdx = 0;
            for (int i = 0; i < smoothed.Length; i++)
                if (smoothed[i] > best) { best = smoothed[i]; bestIdx = i; }

            Vector3 loc = best > 0 ? Unflatten(bestIdx, N, origin, stride) : Vector3.zero;
            sw.Stop();
            return new BenchmarkResult
            {
                Location = loc,
                Density = Mathf.Max(0f, best),
                Empty = best <= 0,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        // ==================================================================
        //  Helpers
        // ==================================================================

        static Vector3Int MapToIndex(Vector3 pos, float origin, float stride)
        {
            Vector3 translated = pos - new Vector3(origin, origin, origin);
            return new Vector3Int(
                Mathf.RoundToInt(translated.x / stride),
                Mathf.RoundToInt(translated.y / stride),
                Mathf.RoundToInt(translated.z / stride));
        }

        static bool InBounds(Vector3Int idx, int N) =>
            idx.x >= 0 && idx.x < N && idx.y >= 0 && idx.y < N && idx.z >= 0 && idx.z < N;

        static int Flatten(Vector3Int idx, int N) => idx.x + idx.y * N + idx.z * N * N;

        static Vector3 Unflatten(int flat, int N, float origin, float stride)
        {
            int x = flat % N;
            int y = (flat / N) % N;
            int z = flat / (N * N);
            return new Vector3(origin + x * stride, origin + y * stride, origin + z * stride);
        }
    }
}
