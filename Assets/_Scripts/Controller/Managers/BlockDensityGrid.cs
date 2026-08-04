using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using CosmicShore.Gameplay;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Burst job that finds the densest region of a flattened count grid.
    ///
    /// Pipeline (proven by DensityPartitionBenchmark + DensityPartitionTemporalSim -
    /// see Docs/DENSITY_PARTITIONING_AUDIT.md §6/§7):
    ///   1. Separable 3D box filter (sliding window - O(N³) per pass, independent
    ///      of kernel width) of the raw counts into a smoothed float field.
    ///   2. Argmax over the smoothed field.
    ///   3. Parabolic sub-voxel interpolation around the peak, so the answer is
    ///      not quantized to the grid stride.
    ///   4. Mean-shift refinement over the RAW counts: iteratively move the answer
    ///      to the centroid of mass within the kernel radius. This is what makes
    ///      the target TRACK remaining mass as fauna consume a cluster's core -
    ///      without it the answer stays pinned to the (smoothed) cluster centre
    ///      even after the centre has been eaten hollow, and consumption stalls
    ///      (the temporal sim's "plateau at Frenzy" failure mode).
    ///
    /// Outputs: result[0] = world-space densest point;
    ///          resultMeta[0] = peak smoothed density (0 ⇒ the grid is empty).
    /// </summary>
    [BurstCompile]
    public struct FindDensestRegionJob : IJob
    {
        [ReadOnly] public NativeArray<ushort> values; // flattened raw counts, length dim³
        public NativeArray<float> bufA;               // scratch, length dim³
        public NativeArray<float> bufB;               // scratch, length dim³
        public NativeArray<float3> result;            // result[0] = world-space densest point
        public NativeArray<float> resultMeta;         // resultMeta[0] = peak smoothed density

        public int dim;                 // grid points per axis
        public float stride;            // metres between grid points
        public float3 origin;           // world-space position of grid index (0,0,0)
        public int kernelHalfWidth;     // box-filter half-width in voxels (>= 1)
        public float meanShiftRadiusVoxels; // mean-shift kernel radius in voxel units
        public int meanShiftIterations;     // 0 disables the refinement pass

        public void Execute()
        {
            int N = dim;
            int K = kernelHalfWidth < 1 ? 1 : kernelHalfWidth;

            // --- X pass: values(ushort) -> bufA(float), sliding-window sum ---
            for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
            {
                int rowOff = y * N + z * N * N;
                float sum = 0f;
                for (int k = 0; k <= K && k < N; k++) sum += values[rowOff + k];
                bufA[rowOff] = sum;
                for (int x = 1; x < N; x++)
                {
                    int addIdx = x + K;
                    int rmIdx = x - K - 1;
                    if (addIdx < N) sum += values[rowOff + addIdx];
                    if (rmIdx >= 0) sum -= values[rowOff + rmIdx];
                    bufA[rowOff + x] = sum;
                }
            }

            // --- Y pass: bufA -> bufB ---
            for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                int colOff = x + z * N * N;
                float sum = 0f;
                for (int k = 0; k <= K && k < N; k++) sum += bufA[colOff + k * N];
                bufB[colOff] = sum;
                for (int y = 1; y < N; y++)
                {
                    int addIdx = y + K;
                    int rmIdx = y - K - 1;
                    if (addIdx < N) sum += bufA[colOff + addIdx * N];
                    if (rmIdx >= 0) sum -= bufA[colOff + rmIdx * N];
                    bufB[colOff + y * N] = sum;
                }
            }

            // --- Z pass: bufB -> bufA (smoothed field ends up in bufA) ---
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int depthOff = x + y * N;
                float sum = 0f;
                for (int k = 0; k <= K && k < N; k++) sum += bufB[depthOff + k * N * N];
                bufA[depthOff] = sum;
                for (int z = 1; z < N; z++)
                {
                    int addIdx = z + K;
                    int rmIdx = z - K - 1;
                    if (addIdx < N) sum += bufB[depthOff + addIdx * N * N];
                    if (rmIdx >= 0) sum -= bufB[depthOff + rmIdx * N * N];
                    bufA[depthOff + z * N * N] = sum;
                }
            }

            // --- Argmax over the smoothed field ---
            float best = -1f;
            int bx = 0, by = 0, bz = 0;
            for (int z = 0; z < N; z++)
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float v = bufA[x + y * N + z * N * N];
                if (v > best) { best = v; bx = x; by = y; bz = z; }
            }
            resultMeta[0] = best;

            // --- Sub-voxel parabolic interpolation around the peak ---
            float dx = ParabolicOffset(SampleAxis(bufA, N, bx, by, bz, 0));
            float dy = ParabolicOffset(SampleAxis(bufA, N, bx, by, bz, 1));
            float dz = ParabolicOffset(SampleAxis(bufA, N, bx, by, bz, 2));
            float3 seed = new float3(bx + dx, by + dy, bz + dz);

            // --- Mean-shift refinement over RAW counts ---
            // Each iteration moves the answer to the count-weighted centroid of the
            // voxels within meanShiftRadiusVoxels. Converges to the local mode of
            // the raw density field. As consumption empties the voxels around the
            // smoothed peak, the centroid (and therefore the answer) walks outward
            // to wherever the surviving mass actually is.
            float msR = meanShiftRadiusVoxels;
            float msR2 = msR * msR;
            for (int iter = 0; iter < meanShiftIterations; iter++)
            {
                int x0 = math.max(0, (int)math.floor(seed.x - msR));
                int x1 = math.min(N - 1, (int)math.ceil(seed.x + msR));
                int y0 = math.max(0, (int)math.floor(seed.y - msR));
                int y1 = math.min(N - 1, (int)math.ceil(seed.y + msR));
                int z0 = math.max(0, (int)math.floor(seed.z - msR));
                int z1 = math.min(N - 1, (int)math.ceil(seed.z + msR));

                float3 weighted = float3.zero;
                float total = 0f;
                for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    ushort v = values[x + y * N + z * N * N];
                    if (v == 0) continue;
                    float3 p = new float3(x, y, z);
                    if (math.distancesq(p, seed) > msR2) continue;
                    weighted += p * v;
                    total += v;
                }

                if (total < 1e-3f) break;          // no mass in reach - keep the interp answer
                float3 next = weighted / total;
                if (math.distancesq(next, seed) < 1e-6f) { seed = next; break; } // converged
                seed = next;
            }

            result[0] = origin + seed * stride;
        }

        /// <summary>(f(i-1), f(i), f(i+1)) along an axis; (b,b,b) at a boundary.</summary>
        static float3 SampleAxis(NativeArray<float> field, int N, int xi, int yi, int zi, int axis)
        {
            int dxi = axis == 0 ? 1 : 0;
            int dyi = axis == 1 ? 1 : 0;
            int dzi = axis == 2 ? 1 : 0;
            float b = field[xi + yi * N + zi * N * N];

            int ax = xi - dxi, ay = yi - dyi, az = zi - dzi;
            int cx = xi + dxi, cy = yi + dyi, cz = zi + dzi;
            bool aIn = ax >= 0 && ax < N && ay >= 0 && ay < N && az >= 0 && az < N;
            bool cIn = cx >= 0 && cx < N && cy >= 0 && cy < N && cz >= 0 && cz < N;
            if (!aIn || !cIn) return new float3(b, b, b);

            float a = field[ax + ay * N + az * N * N];
            float c = field[cx + cy * N + cz * N * N];
            return new float3(a, b, c);
        }

        /// <summary>Continuous offset of a parabolic peak through (a,b,c) at (-1,0,+1), clamped to ±0.5.</summary>
        static float ParabolicOffset(float3 abc)
        {
            float a = abc.x, b = abc.y, c = abc.z;
            if (b < a || b < c) return 0f;            // peak isn't really at b
            float denom = a - 2f * b + c;
            if (math.abs(denom) < 1e-6f) return 0f;   // no curvature
            return math.clamp(0.5f * (a - c) / denom, -0.5f, 0.5f);
        }
    }

    public class BlockDensityGrid
    {
        // ------------------------------------------------------------------
        //  Physical constants - all tied to the swarm-effective consumption
        //  scale, NOT to the grid or the cell. See the audit §6/§7: the swarm
        //  (consumeRadius 40-72m + boid-separation spread) collectively covers
        //  a ~150m-radius volume, so that is the scale the algorithm samples
        //  density at, and the voxel size resolves features at half that scale.
        // ------------------------------------------------------------------

        /// <summary>
        /// Physical smoothing-kernel radius in metres - the swarm-effective
        /// consumption volume (consumeRadius + boid separation spread). The
        /// box-filter half-width in voxels is derived from this and the stride,
        /// so the smoothing scale stays physical regardless of cell size.
        /// </summary>
        public const float SmoothingRadiusMeters = 150f;

        /// <summary>
        /// Target physical voxel size in metres - half the smoothing kernel
        /// (Nyquist: voxels at half the kernel scale resolve features at the
        /// kernel scale). Grid resolution is derived per cell from this, so a
        /// 2400m Blob cell gets ~33 points/axis (~75m voxels) while a small
        /// cell gets proportionally fewer. The previous fixed 17³ resolution
        /// gave 141m voxels at Blob-cell scale - coarse enough that an entire
        /// flora cluster fit in one voxel, so the argmax could not shift as
        /// fauna depleted the cluster's core (temporal sim: "MID plateaus").
        /// </summary>
        public const float TargetVoxelSizeMeters = 75f;

        /// <summary>Resolution floor for very small cells.</summary>
        public const int MinGridPointsPerDimension = 9;

        /// <summary>
        /// Resolution ceiling for memory safety. 33³ = 35,937 voxels:
        /// ushort counts (72KB) + two float scratch buffers (288KB) ≈ 360KB
        /// per grid, ~1.4MB per cell (4 grids).
        /// </summary>
        public const int MaxGridPointsPerDimension = 33;

        /// <summary>Mean-shift refinement iterations inside the Burst job.</summary>
        public const int MeanShiftIterations = 5;

        /// <summary>
        /// Minimum seconds between job runs while the grid is changing. Fauna
        /// re-query their goal every 0.5-2s, so a 0.25s-stale answer is
        /// indistinguishable from an exact one - but this bound turns
        /// "every fauna's query runs the job" (100s of redundant runs/sec at
        /// production population scale) into "at most 4 runs/sec per grid".
        /// </summary>
        public const float MinRecomputeIntervalSeconds = 0.25f;

        public float Stride;
        public float totalLength;
        public Vector3 origin;
        public Domains Domain;

        protected int nGridPointsPerDimension;
        protected int kernelHalfWidth;
        protected NativeArray<ushort> jobValues;   // sole count storage - written directly by Add/RemoveBlock
        protected NativeArray<ushort> jobValuesSnapshot; // job-read copy - lets Add/RemoveBlock keep writing jobValues while a job is in flight
        protected NativeArray<float> jobBufA;
        protected NativeArray<float> jobBufB;
        protected NativeArray<float3> jobResult;
        protected NativeArray<float> jobResultMeta;
        protected bool jobSystemInitialized = false;

        // ---- Async job state ----
        // The job used to run Schedule().Complete() inline - an ~O(dim³) stall on
        // the main thread every time the cache went stale (the visible fauna-goal
        // frame spike). It now schedules against the snapshot and the NEXT query
        // harvests the finished result: callers always get the cached answer
        // instantly, at most one caller-interval staler than before - inside the
        // staleness tolerance the cache policy already declares.
        JobHandle pendingJob;
        bool jobInFlight;

        // ---- Result cache ----
        // The answer only changes when blocks are added/removed (dirty flag), and
        // even then fauna can tolerate MinRecomputeIntervalSeconds of staleness.
        // Without this, every fauna's GetExplosionTarget call re-ran the full job
        // on identical data - at production population scale (4 fauna per 100
        // prisms ⇒ 100s of fauna) that was 100s of redundant job runs per second.
        bool dirty = true;
        bool hasCachedResult = false;
        Vector3 cachedResult;
        float cachedResultDensity;
        float lastComputeTime = float.NegativeInfinity;

        /// <summary>
        /// Peak smoothed density found by the most recent job run. 0 means the grid
        /// was empty - callers should fall back to their anchor position instead of
        /// using the returned location.
        /// </summary>
        public float LastResultDensity => cachedResultDensity;

        /// <summary>Actual grid resolution chosen for this cell (diagnostic).</summary>
        public int GridPointsPerDimensionActual => nGridPointsPerDimension;

        /// <summary>
        /// Initialize the grid to cover a cube of side <paramref name="worldDiameter"/>
        /// centered on <paramref name="cellCenter"/>.
        ///
        /// Sizing the grid to the owning cell - instead of the old hard-coded
        /// 1000m cube anchored at world origin - is the structural fix for the
        /// production grid being blind to ~86% of a 1200m-radius cell's volume.
        /// Resolution is derived from TargetVoxelSizeMeters so the voxel size is
        /// a physical constant rather than a fraction of the cell size.
        /// See Docs/DENSITY_PARTITIONING_AUDIT.md.
        /// </summary>
        public void Init(Domains domain, Vector3 cellCenter, float worldDiameter)
        {
            Domain = domain;
            totalLength = Mathf.Max(1f, worldDiameter);
            nGridPointsPerDimension = Mathf.Clamp(
                Mathf.CeilToInt(totalLength / TargetVoxelSizeMeters) + 1,
                MinGridPointsPerDimension, MaxGridPointsPerDimension);
            Stride = totalLength / (nGridPointsPerDimension - 1);
            origin = cellCenter - new Vector3(totalLength / 2f, totalLength / 2f, totalLength / 2f);
            kernelHalfWidth = Mathf.Max(1, Mathf.RoundToInt(SmoothingRadiusMeters / Stride));

            int totalSize = nGridPointsPerDimension * nGridPointsPerDimension * nGridPointsPerDimension;
            jobValues = new NativeArray<ushort>(totalSize, Allocator.Persistent);
            jobValuesSnapshot = new NativeArray<ushort>(totalSize, Allocator.Persistent);
            jobBufA = new NativeArray<float>(totalSize, Allocator.Persistent);
            jobBufB = new NativeArray<float>(totalSize, Allocator.Persistent);
            jobResult = new NativeArray<float3>(1, Allocator.Persistent);
            jobResultMeta = new NativeArray<float>(1, Allocator.Persistent);
            jobSystemInitialized = true;

            dirty = true;
            hasCachedResult = false;
            cachedResultDensity = 0f;
            lastComputeTime = float.NegativeInfinity;
            jobInFlight = false;
        }

        /// <summary>
        /// Releases the persistent NativeArrays. Plain C# class - the owning Cell
        /// must call this explicitly when discarding a grid.
        /// </summary>
        public void Dispose()
        {
            if (!jobSystemInitialized) return;
            // An in-flight job reads the snapshot/scratch buffers - finish it
            // before freeing them (Complete on a done job is a no-op).
            if (jobInFlight)
            {
                pendingJob.Complete();
                jobInFlight = false;
            }
            if (jobValues.IsCreated) jobValues.Dispose();
            if (jobValuesSnapshot.IsCreated) jobValuesSnapshot.Dispose();
            if (jobBufA.IsCreated) jobBufA.Dispose();
            if (jobBufB.IsCreated) jobBufB.Dispose();
            if (jobResult.IsCreated) jobResult.Dispose();
            if (jobResultMeta.IsCreated) jobResultMeta.Dispose();
            jobSystemInitialized = false;
        }

        public Vector3Int MapCoordinatesToGridIndices(Vector3 coords)
        {
            Vector3 translatedCoords = coords - this.origin;
            Vector3 unroundedIndices = translatedCoords / this.Stride;
            Vector3Int indices = Vector3Int.RoundToInt(unroundedIndices);
            return indices;
        }

        public Vector3 MapGridIndicesToCoordinates(Vector3Int indices)
        {
            Vector3 untranslatedCoords = (Vector3)indices * this.Stride;
            Vector3 coords = untranslatedCoords + this.origin;
            return coords;
        }

        protected bool InBounds(Vector3Int idx) =>
            idx.x >= 0 && idx.x < nGridPointsPerDimension &&
            idx.y >= 0 && idx.y < nGridPointsPerDimension &&
            idx.z >= 0 && idx.z < nGridPointsPerDimension;

        protected int FlatIndex(Vector3Int idx) =>
            idx.x + idx.y * nGridPointsPerDimension + idx.z * nGridPointsPerDimension * nGridPointsPerDimension;

        public int GetDensityAtPosition(Vector3 coords)
        {
            if (!jobSystemInitialized) return 0;
            Vector3Int idx = MapCoordinatesToGridIndices(coords);
            // Bounds guard - FindDensestRegion returns sub-voxel positions and
            // arbitrary callers may pass world points outside the grid. Out-of-grid
            // is density 0 (the caller's "fall back to anchor" path).
            if (!InBounds(idx)) return 0;
            return jobValues[FlatIndex(idx)];
        }

        /// <summary>Marks the cached result stale. Called by Add/RemoveBlock.</summary>
        protected void MarkDirty() => dirty = true;

        public Vector3 FindDensestRegion()
        {
            if (!jobSystemInitialized)
                return origin + Vector3.one * (totalLength / 2f); // grid center fallback

            // Harvest a finished in-flight job (non-blocking: Complete() on a done
            // handle only satisfies the safety system before we read the results).
            if (jobInFlight && pendingJob.IsCompleted)
            {
                pendingJob.Complete();
                jobInFlight = false;
                float3 r = jobResult[0];
                cachedResult = new Vector3(r.x, r.y, r.z);
                cachedResultDensity = jobResultMeta[0];
                hasCachedResult = true;
                lastComputeTime = Time.time;
            }

            // Cache/schedule policy:
            //  - clean (no block changes since last harvest) → exact cached answer
            //  - dirty but harvested < MinRecomputeIntervalSeconds ago → cached
            //    answer (bounded staleness, invisible at fauna's 0.5-2s goal cadence)
            //  - dirty and stale → schedule the job on a snapshot of the counts and
            //    keep returning the cached answer; a later query harvests it. The
            //    snapshot is what lets Add/RemoveBlock keep writing the live counts
            //    while the job runs on worker threads.
            bool recentlyComputed = Time.time - lastComputeTime < MinRecomputeIntervalSeconds;
            if (!jobInFlight && dirty && !recentlyComputed)
            {
                NativeArray<ushort>.Copy(jobValues, jobValuesSnapshot);
                var job = new FindDensestRegionJob
                {
                    values = jobValuesSnapshot,
                    bufA = jobBufA,
                    bufB = jobBufB,
                    result = jobResult,
                    resultMeta = jobResultMeta,
                    dim = nGridPointsPerDimension,
                    stride = Stride,
                    origin = new float3(origin.x, origin.y, origin.z),
                    kernelHalfWidth = kernelHalfWidth,
                    meanShiftRadiusVoxels = SmoothingRadiusMeters / Stride,
                    meanShiftIterations = MeanShiftIterations,
                };
                pendingJob = job.Schedule();
                jobInFlight = true;
                // Blocks added/removed AFTER this snapshot re-mark dirty and are
                // picked up by the next schedule.
                dirty = false;
            }

            if (hasCachedResult)
                return cachedResult;

            // Cold start: first query since Init, nothing harvested yet. Report the
            // grid-centre with LastResultDensity still 0 - callers treat density 0
            // as "grid empty" and fall back to their anchor (crystal), exactly as
            // they do for a genuinely empty grid. The first harvest replaces this
            // within one caller interval.
            return origin + Vector3.one * (totalLength / 2f);
        }

        // These take the prism's world POSITION rather than the prism, because that is
        // all a density grid ever wanted and reading it here charged the caller a
        // managed→engine interop per grid: Cell.AddBlock / Cell.RemoveBlock each fan
        // out to three grids, so a prism paid 3 transform.position reads on creation
        // and 3 more on death for one value that cannot change in between. The caller
        // now reads it once and hands it down.
        public virtual void AddBlockAt(Vector3 position) {}

        public virtual void RemoveBlockAt(Vector3 position) {}
    }

    public class BlockCountDensityGrid : BlockDensityGrid
    {
        public BlockCountDensityGrid(Domains domain, Vector3 cellCenter, float worldDiameter)
        {
            Init(domain, cellCenter, worldDiameter);
        }

        public override void AddBlockAt(Vector3 position)
        {
            if (!jobSystemInitialized) return;
            Vector3Int idx = MapCoordinatesToGridIndices(position);
            if (!InBounds(idx)) return;

            int flat = FlatIndex(idx);
            // Saturate instead of wrapping. (The previous byte storage wrapped at 255 -
            // production cells reach 10,000+ prisms, so a hot voxel could overflow and
            // erase its own density.)
            if (jobValues[flat] < ushort.MaxValue)
            {
                jobValues[flat]++;
                MarkDirty();
            }
        }

        public override void RemoveBlockAt(Vector3 position)
        {
            if (!jobSystemInitialized) return;
            Vector3Int idx = MapCoordinatesToGridIndices(position);
            if (!InBounds(idx)) return;

            int flat = FlatIndex(idx);
            // Underflow guard: a remove for a block that was never added at this voxel
            // (prism moved between Add and Remove, or pre-fix stale data) must not wrap.
            if (jobValues[flat] > 0)
            {
                jobValues[flat]--;
                MarkDirty();
            }
        }
    }

    public class BlockVolumeDensityGrid : BlockDensityGrid {}
}
