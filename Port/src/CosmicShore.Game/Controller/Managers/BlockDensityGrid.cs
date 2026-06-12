using CosmicShore.Engine;
// PORT managed-array conversion (V11): the Unity Jobs/Burst/Mathematics pipeline below is
// ported as plain managed C# (precedent: "PrismAOERegistry managed-array port", VESSEL_LAYER
// plan). NativeArray<T> → managed arrays, float3/math → Vector3/Mathf,
// IJob.Schedule().Complete() → direct Execute() call, [BurstCompile]/[ReadOnly] dropped.
// Original directives:
//   using Unity.Jobs;
//   using Unity.Collections;
//   using Unity.Mathematics;
//   using Unity.Burst;
using CosmicShore.Gameplay;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Burst job that finds the densest region of a flattened count grid.
    ///
    /// Pipeline (proven by DensityPartitionBenchmark + DensityPartitionTemporalSim —
    /// see Docs/DENSITY_PARTITIONING_AUDIT.md §6/§7):
    ///   1. Separable 3D box filter (sliding window — O(N³) per pass, independent
    ///      of kernel width) of the raw counts into a smoothed float field.
    ///   2. Argmax over the smoothed field.
    ///   3. Parabolic sub-voxel interpolation around the peak, so the answer is
    ///      not quantized to the grid stride.
    ///   4. Mean-shift refinement over the RAW counts: iteratively move the answer
    ///      to the centroid of mass within the kernel radius. This is what makes
    ///      the target TRACK remaining mass as fauna consume a cluster's core —
    ///      without it the answer stays pinned to the (smoothed) cluster centre
    ///      even after the centre has been eaten hollow, and consumption stalls
    ///      (the temporal sim's "plateau at Frenzy" failure mode).
    ///
    /// Outputs: result[0] = world-space densest point;
    ///          resultMeta[0] = peak smoothed density (0 ⇒ the grid is empty).
    /// </summary>
    // PORT managed-array conversion: [BurstCompile] ... : IJob dropped; Execute() is called directly.
    public struct FindDensestRegionJob
    {
        public ushort[] values;       // flattened raw counts, length dim³
        public float[] bufA;          // scratch, length dim³
        public float[] bufB;          // scratch, length dim³
        public Vector3[] result;      // result[0] = world-space densest point
        public float[] resultMeta;    // resultMeta[0] = peak smoothed density

        public int dim;                 // grid points per axis
        public float stride;            // metres between grid points
        public Vector3 origin;          // world-space position of grid index (0,0,0)
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
            Vector3 seed = new Vector3(bx + dx, by + dy, bz + dz);

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
                int x0 = Mathf.Max(0, (int)Mathf.Floor(seed.x - msR));
                int x1 = Mathf.Min(N - 1, (int)Mathf.Ceil(seed.x + msR));
                int y0 = Mathf.Max(0, (int)Mathf.Floor(seed.y - msR));
                int y1 = Mathf.Min(N - 1, (int)Mathf.Ceil(seed.y + msR));
                int z0 = Mathf.Max(0, (int)Mathf.Floor(seed.z - msR));
                int z1 = Mathf.Min(N - 1, (int)Mathf.Ceil(seed.z + msR));

                Vector3 weighted = Vector3.zero;
                float total = 0f;
                for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    ushort v = values[x + y * N + z * N * N];
                    if (v == 0) continue;
                    Vector3 p = new Vector3(x, y, z);
                    if ((p - seed).sqrMagnitude > msR2) continue; // PORT managed-array conversion: math.distancesq(p, seed)
                    weighted += p * v;
                    total += v;
                }

                if (total < 1e-3f) break;          // no mass in reach — keep the interp answer
                Vector3 next = weighted / total;
                if ((next - seed).sqrMagnitude < 1e-6f) { seed = next; break; } // converged
                seed = next;
            }

            result[0] = origin + seed * stride;
        }

        /// <summary>(f(i-1), f(i), f(i+1)) along an axis; (b,b,b) at a boundary.</summary>
        static Vector3 SampleAxis(float[] field, int N, int xi, int yi, int zi, int axis)
        {
            int dxi = axis == 0 ? 1 : 0;
            int dyi = axis == 1 ? 1 : 0;
            int dzi = axis == 2 ? 1 : 0;
            float b = field[xi + yi * N + zi * N * N];

            int ax = xi - dxi, ay = yi - dyi, az = zi - dzi;
            int cx = xi + dxi, cy = yi + dyi, cz = zi + dzi;
            bool aIn = ax >= 0 && ax < N && ay >= 0 && ay < N && az >= 0 && az < N;
            bool cIn = cx >= 0 && cx < N && cy >= 0 && cy < N && cz >= 0 && cz < N;
            if (!aIn || !cIn) return new Vector3(b, b, b);

            float a = field[ax + ay * N + az * N * N];
            float c = field[cx + cy * N + cz * N * N];
            return new Vector3(a, b, c);
        }

        /// <summary>Continuous offset of a parabolic peak through (a,b,c) at (-1,0,+1), clamped to ±0.5.</summary>
        static float ParabolicOffset(Vector3 abc)
        {
            float a = abc.x, b = abc.y, c = abc.z;
            if (b < a || b < c) return 0f;            // peak isn't really at b
            float denom = a - 2f * b + c;
            if (Mathf.Abs(denom) < 1e-6f) return 0f;  // no curvature
            return Mathf.Clamp(0.5f * (a - c) / denom, -0.5f, 0.5f);
        }
    }

    public class BlockDensityGrid
    {
        // ------------------------------------------------------------------
        //  Physical constants — all tied to the swarm-effective consumption
        //  scale, NOT to the grid or the cell. See the audit §6/§7: the swarm
        //  (consumeRadius 40-72m + boid-separation spread) collectively covers
        //  a ~150m-radius volume, so that is the scale the algorithm samples
        //  density at, and the voxel size resolves features at half that scale.
        // ------------------------------------------------------------------

        /// <summary>
        /// Physical smoothing-kernel radius in metres — the swarm-effective
        /// consumption volume (consumeRadius + boid separation spread). The
        /// box-filter half-width in voxels is derived from this and the stride,
        /// so the smoothing scale stays physical regardless of cell size.
        /// </summary>
        public const float SmoothingRadiusMeters = 150f;

        /// <summary>
        /// Target physical voxel size in metres — half the smoothing kernel
        /// (Nyquist: voxels at half the kernel scale resolve features at the
        /// kernel scale). Grid resolution is derived per cell from this, so a
        /// 2400m Blob cell gets ~33 points/axis (~75m voxels) while a small
        /// cell gets proportionally fewer. The previous fixed 17³ resolution
        /// gave 141m voxels at Blob-cell scale — coarse enough that an entire
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
        /// indistinguishable from an exact one — but this bound turns
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
        // PORT managed-array conversion: NativeArray<ushort/float/float3> fields → managed arrays.
        protected ushort[] jobValues;   // sole count storage — written directly by Add/RemoveBlock
        protected float[] jobBufA;
        protected float[] jobBufB;
        protected Vector3[] jobResult;
        protected float[] jobResultMeta;
        protected bool jobSystemInitialized = false;

        // ---- Result cache ----
        // The answer only changes when blocks are added/removed (dirty flag), and
        // even then fauna can tolerate MinRecomputeIntervalSeconds of staleness.
        // Without this, every fauna's GetExplosionTarget call re-ran the full job
        // on identical data — at production population scale (4 fauna per 100
        // prisms ⇒ 100s of fauna) that was 100s of redundant job runs per second.
        bool dirty = true;
        bool hasCachedResult = false;
        Vector3 cachedResult;
        float cachedResultDensity;
        float lastComputeTime = float.NegativeInfinity;

        /// <summary>
        /// Peak smoothed density found by the most recent job run. 0 means the grid
        /// was empty — callers should fall back to their anchor position instead of
        /// using the returned location.
        /// </summary>
        public float LastResultDensity => cachedResultDensity;

        /// <summary>Actual grid resolution chosen for this cell (diagnostic).</summary>
        public int GridPointsPerDimensionActual => nGridPointsPerDimension;

        /// <summary>
        /// Initialize the grid to cover a cube of side <paramref name="worldDiameter"/>
        /// centered on <paramref name="cellCenter"/>.
        ///
        /// Sizing the grid to the owning cell — instead of the old hard-coded
        /// 1000m cube anchored at world origin — is the structural fix for the
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
            // PORT managed-array conversion: new NativeArray<T>(totalSize, Allocator.Persistent) → new T[totalSize].
            jobValues = new ushort[totalSize];
            jobBufA = new float[totalSize];
            jobBufB = new float[totalSize];
            jobResult = new Vector3[1];
            jobResultMeta = new float[1];
            jobSystemInitialized = true;

            dirty = true;
            hasCachedResult = false;
            cachedResultDensity = 0f;
            lastComputeTime = float.NegativeInfinity;
        }

        /// <summary>
        /// Releases the persistent NativeArrays. Plain C# class — the owning Cell
        /// must call this explicitly when discarding a grid.
        /// </summary>
        public void Dispose()
        {
            if (!jobSystemInitialized) return;
            // PORT managed-array conversion: IsCreated checks + NativeArray.Dispose() calls
            // → release the managed arrays to the GC.
            jobValues = null;
            jobBufA = null;
            jobBufB = null;
            jobResult = null;
            jobResultMeta = null;
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
            // Bounds guard — FindDensestRegion returns sub-voxel positions and
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

            // Cache policy:
            //  - clean (no block changes since last run) → exact cached answer
            //  - dirty but computed < MinRecomputeIntervalSeconds ago → cached answer
            //    (bounded staleness, invisible at fauna's 0.5-2s goal cadence)
            //  - dirty and stale → run the job
            if (hasCachedResult)
            {
                bool recentlyComputed = Time.time - lastComputeTime < MinRecomputeIntervalSeconds;
                if (!dirty || recentlyComputed)
                    return cachedResult;
            }

            var job = new FindDensestRegionJob
            {
                values = jobValues,
                bufA = jobBufA,
                bufB = jobBufB,
                result = jobResult,
                resultMeta = jobResultMeta,
                dim = nGridPointsPerDimension,
                stride = Stride,
                origin = new Vector3(origin.x, origin.y, origin.z),
                kernelHalfWidth = kernelHalfWidth,
                meanShiftRadiusVoxels = SmoothingRadiusMeters / Stride,
                meanShiftIterations = MeanShiftIterations,
            };

            job.Execute(); // PORT managed-array conversion: job.Schedule().Complete();

            Vector3 r = jobResult[0];
            cachedResult = new Vector3(r.x, r.y, r.z);
            cachedResultDensity = jobResultMeta[0];
            hasCachedResult = true;
            dirty = false;
            lastComputeTime = Time.time;
            return cachedResult;
        }

        // PORT Deviation (V11, restore when Prism ports): public virtual void AddBlock(Prism block) {}
        // Prism : MonoBehaviour and only block.transform.position is read, so the base type
        // stands in to keep the add/remove surface live until the prism cluster lands (V15).
        public virtual void AddBlock(MonoBehaviour block) {}

        // PORT Deviation (V11, restore when Prism ports): public virtual void RemoveBlock(Prism block) {}
        public virtual void RemoveBlock(MonoBehaviour block) {}
    }

    public class BlockCountDensityGrid : BlockDensityGrid
    {
        public BlockCountDensityGrid(Domains domain, Vector3 cellCenter, float worldDiameter)
        {
            Init(domain, cellCenter, worldDiameter);
        }

        // PORT Deviation (V11, restore when Prism ports): public override void AddBlock(Prism block)
        public override void AddBlock(MonoBehaviour block)
        {
            if (!jobSystemInitialized) return;
            Vector3Int idx = MapCoordinatesToGridIndices(block.transform.position);
            if (!InBounds(idx)) return;

            int flat = FlatIndex(idx);
            // Saturate instead of wrapping. (The previous byte storage wrapped at 255 —
            // production cells reach 10,000+ prisms, so a hot voxel could overflow and
            // erase its own density.)
            if (jobValues[flat] < ushort.MaxValue)
            {
                jobValues[flat]++;
                MarkDirty();
            }
        }

        // PORT Deviation (V11, restore when Prism ports): public override void RemoveBlock(Prism block)
        public override void RemoveBlock(MonoBehaviour block)
        {
            if (!jobSystemInitialized) return;
            Vector3Int idx = MapCoordinatesToGridIndices(block.transform.position);
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
