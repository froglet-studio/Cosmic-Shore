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
    /// Pipeline (proven by DensityPartitionBenchmark — GridSmoothedInterp17 row,
    /// ~28m median error at cell scale vs ~100m for raw argmax):
    ///   1. Separable 3D box filter (sliding window — O(N³) per pass, independent
    ///      of kernel width) of the raw counts into a smoothed float field.
    ///   2. Argmax over the smoothed field.
    ///   3. Parabolic sub-voxel interpolation around the peak, so the answer is
    ///      not quantized to the (now coarse, cell-sized) grid stride.
    ///
    /// Output is a world-space position written to result[0].
    /// </summary>
    [BurstCompile]
    public struct FindDensestRegionJob : IJob
    {
        [ReadOnly] public NativeArray<byte> values; // flattened raw counts, length dim³
        public NativeArray<float> bufA;             // scratch, length dim³
        public NativeArray<float> bufB;             // scratch, length dim³
        public NativeArray<float3> result;          // result[0] = world-space densest point

        public int dim;                 // grid points per axis
        public float stride;            // metres between grid points
        public float3 origin;           // world-space position of grid index (0,0,0)
        public int kernelHalfWidth;     // box-filter half-width in voxels (>= 1)

        public void Execute()
        {
            int N = dim;
            int K = kernelHalfWidth < 1 ? 1 : kernelHalfWidth;

            // --- X pass: values(byte) -> bufA(float), sliding-window sum ---
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

            // --- Sub-voxel parabolic interpolation around the peak ---
            float dx = ParabolicOffset(SampleAxis(bufA, N, bx, by, bz, 0));
            float dy = ParabolicOffset(SampleAxis(bufA, N, bx, by, bz, 1));
            float dz = ParabolicOffset(SampleAxis(bufA, N, bx, by, bz, 2));

            result[0] = origin + new float3(
                (bx + dx) * stride,
                (by + dy) * stride,
                (bz + dz) * stride);
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
        /// <summary>
        /// Fixed grid resolution: 17 points per axis. DensityPartitionBenchmark
        /// (GridSmoothedInterp17) proved 17³ + box smoothing + sub-voxel interp
        /// lands within ~28m of ground truth even at a 1200m-radius cell scale —
        /// well inside the ~150m swarm consumption radius. The stride is derived
        /// from the cell size, so this stays at 17³ regardless of cell size.
        /// </summary>
        public const int GridPointsPerDimension = 17;

        /// <summary>
        /// Physical smoothing-kernel radius in metres — the swarm-effective
        /// consumption volume (consumeRadius 40m + goalOrbitRadius 60m + boid
        /// separation spread). The box-filter half-width in voxels is derived
        /// from this and the stride, so the smoothing scale stays physical
        /// regardless of cell size.
        /// </summary>
        public const float SmoothingRadiusMeters = 150f;

        public float Stride;
        public float totalLength;
        public Vector3 origin;
        public Domains Domain;
        public byte[,,] values;

        protected int nGridPointsPerDimension;
        protected int kernelHalfWidth;
        protected NativeArray<byte> jobValues;
        protected NativeArray<float> jobBufA;
        protected NativeArray<float> jobBufB;
        protected NativeArray<float3> jobResult;
        protected bool jobSystemInitialized = false;

        /// <summary>
        /// Initialize the grid to cover a cube of side <paramref name="worldDiameter"/>
        /// centered on <paramref name="cellCenter"/>.
        ///
        /// Sizing the grid to the owning cell — instead of the old hard-coded
        /// 1000m cube anchored at world origin — is the structural fix for the
        /// production grid being blind to ~86% of a 1200m-radius cell's volume.
        /// See Docs/DENSITY_PARTITIONING_AUDIT.md.
        /// </summary>
        public void Init(Domains domain, Vector3 cellCenter, float worldDiameter)
        {
            Domain = domain;
            totalLength = Mathf.Max(1f, worldDiameter);
            nGridPointsPerDimension = GridPointsPerDimension;
            Stride = totalLength / (nGridPointsPerDimension - 1);
            origin = cellCenter - new Vector3(totalLength / 2f, totalLength / 2f, totalLength / 2f);
            kernelHalfWidth = Mathf.Max(1, Mathf.RoundToInt(SmoothingRadiusMeters / Stride));

            int totalSize = nGridPointsPerDimension * nGridPointsPerDimension * nGridPointsPerDimension;
            jobValues = new NativeArray<byte>(totalSize, Allocator.Persistent);
            jobBufA = new NativeArray<float>(totalSize, Allocator.Persistent);
            jobBufB = new NativeArray<float>(totalSize, Allocator.Persistent);
            jobResult = new NativeArray<float3>(1, Allocator.Persistent);
            jobSystemInitialized = true;
        }

        /// <summary>
        /// Releases the persistent NativeArrays. Plain C# class — the owning Cell
        /// must call this explicitly when discarding a grid. (Pre-existing code
        /// never called the old OnDestroy, so grids leaked; callers that want to
        /// stop leaking should route through here.)
        /// </summary>
        public void Dispose()
        {
            if (!jobSystemInitialized) return;
            if (jobValues.IsCreated) jobValues.Dispose();
            if (jobBufA.IsCreated) jobBufA.Dispose();
            if (jobBufB.IsCreated) jobBufB.Dispose();
            if (jobResult.IsCreated) jobResult.Dispose();
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

        public int GetDensityAtPosition(Vector3 coords)
        {
            Vector3Int idx = MapCoordinatesToGridIndices(coords);
            // Bounds guard — FindDensestRegion now returns sub-voxel positions and
            // arbitrary callers may pass world points outside the grid. Out-of-grid
            // is density 0 (the caller's "fall back to anchor" path).
            if (idx.x < 0 || idx.x >= nGridPointsPerDimension ||
                idx.y < 0 || idx.y >= nGridPointsPerDimension ||
                idx.z < 0 || idx.z >= nGridPointsPerDimension)
                return 0;
            return this.values[idx.x, idx.y, idx.z];
        }

        protected void UpdateJobValues()
        {
            // Convert 3D array to flat array for the job system.
            for (int x = 0; x < nGridPointsPerDimension; x++)
            for (int y = 0; y < nGridPointsPerDimension; y++)
            for (int z = 0; z < nGridPointsPerDimension; z++)
            {
                int index = x + y * nGridPointsPerDimension + z * nGridPointsPerDimension * nGridPointsPerDimension;
                jobValues[index] = values[x, y, z];
            }
        }

        public Vector3 FindDensestRegion()
        {
            if (!jobSystemInitialized)
                return origin + Vector3.one * (totalLength / 2f); // grid center fallback

            UpdateJobValues();

            var job = new FindDensestRegionJob
            {
                values = jobValues,
                bufA = jobBufA,
                bufB = jobBufB,
                result = jobResult,
                dim = nGridPointsPerDimension,
                stride = Stride,
                origin = new float3(origin.x, origin.y, origin.z),
                kernelHalfWidth = kernelHalfWidth,
            };

            job.Schedule().Complete();

            float3 r = jobResult[0];
            return new Vector3(r.x, r.y, r.z);
        }

        public virtual void AddBlock(Prism block) {}

        public virtual void RemoveBlock(Prism block) {}
    }

    public class BlockCountDensityGrid : BlockDensityGrid
    {
        public BlockCountDensityGrid(Domains domain, Vector3 cellCenter, float worldDiameter)
        {
            base.Init(domain, cellCenter, worldDiameter);
            values = new byte[nGridPointsPerDimension, nGridPointsPerDimension, nGridPointsPerDimension];
        }

        public override void AddBlock(Prism block)
        {
            Vector3Int idx = MapCoordinatesToGridIndices(block.transform.position);
            if (idx.x >= 0 && idx.x < nGridPointsPerDimension &&
                idx.y >= 0 && idx.y < nGridPointsPerDimension &&
                idx.z >= 0 && idx.z < nGridPointsPerDimension)
                this.values[idx.x, idx.y, idx.z] += 1;
        }

        public override void RemoveBlock(Prism block)
        {
            Vector3Int idx = MapCoordinatesToGridIndices(block.transform.position);
            if (idx.x >= 0 && idx.x < nGridPointsPerDimension &&
                idx.y >= 0 && idx.y < nGridPointsPerDimension &&
                idx.z >= 0 && idx.z < nGridPointsPerDimension)
                this.values[idx.x, idx.y, idx.z] -= 1;
        }
    }

    public class BlockVolumeDensityGrid : BlockDensityGrid {}
}
