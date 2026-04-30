using System;
using System.Collections.Generic;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Aggregated statistics computed from a collection of FrameSnapshots.
    /// </summary>
    [Serializable]
    public class BenchmarkStatistics
    {
        public int totalFrames;
        public float durationSeconds;

        // Frame time (ms)
        public float avgFrameTimeMs;
        public float minFrameTimeMs;
        public float maxFrameTimeMs;
        public float medianFrameTimeMs;
        public float p95FrameTimeMs;
        public float p99FrameTimeMs;
        public float stdDevFrameTimeMs;

        // FPS
        public float avgFps;
        public float minFps;
        public float maxFps;
        public float medianFps;
        public float p5Fps; // 5th percentile = worst 5% of FPS
        public float p1Fps; // 1st percentile = worst 1% of FPS

        // Rendering averages
        public float avgDrawCalls;
        public float avgBatches;
        public float avgSetPassCalls;
        public float avgTriangles;
        public float avgVertices;

        // Memory
        public long peakAllocatedMemory;
        public long avgAllocatedMemory;
        public long totalGcAllocated;

        // Physics
        public float avgActiveRigidbodies;

        public static BenchmarkStatistics Compute(List<FrameSnapshot> snapshots, float durationSec)
        {
            if (snapshots == null || snapshots.Count == 0)
                return new BenchmarkStatistics();

            var stats = new BenchmarkStatistics
            {
                totalFrames = snapshots.Count,
                durationSeconds = durationSec
            };

            var frameTimes = new float[snapshots.Count];
            var fpsValues = new float[snapshots.Count];

            float sumFrameTime = 0;
            float sumFps = 0;
            float sumDrawCalls = 0;
            float sumBatches = 0;
            float sumSetPass = 0;
            float sumTris = 0;
            float sumVerts = 0;
            long sumAllocMem = 0;
            long sumGc = 0;
            float sumRigidbodies = 0;

            stats.minFrameTimeMs = float.MaxValue;
            stats.maxFrameTimeMs = float.MinValue;
            stats.minFps = float.MaxValue;
            stats.maxFps = float.MinValue;
            stats.peakAllocatedMemory = 0;

            for (int i = 0; i < snapshots.Count; i++)
            {
                var s = snapshots[i];
                frameTimes[i] = s.deltaTimeMs;
                fpsValues[i] = s.fps;

                sumFrameTime += s.deltaTimeMs;
                sumFps += s.fps;
                sumDrawCalls += s.drawCalls;
                sumBatches += s.batches;
                sumSetPass += s.setPassCalls;
                sumTris += s.triangles;
                sumVerts += s.vertices;
                sumAllocMem += s.totalAllocatedMemory;
                sumGc += s.gcAllocatedPerFrame;
                sumRigidbodies += s.activeRigidbodies;

                if (s.deltaTimeMs < stats.minFrameTimeMs) stats.minFrameTimeMs = s.deltaTimeMs;
                if (s.deltaTimeMs > stats.maxFrameTimeMs) stats.maxFrameTimeMs = s.deltaTimeMs;
                if (s.fps < stats.minFps) stats.minFps = s.fps;
                if (s.fps > stats.maxFps) stats.maxFps = s.fps;
                if (s.totalAllocatedMemory > stats.peakAllocatedMemory)
                    stats.peakAllocatedMemory = s.totalAllocatedMemory;
            }

            int n = snapshots.Count;
            stats.avgFrameTimeMs = sumFrameTime / n;
            stats.avgFps = sumFps / n;
            stats.avgDrawCalls = sumDrawCalls / n;
            stats.avgBatches = sumBatches / n;
            stats.avgSetPassCalls = sumSetPass / n;
            stats.avgTriangles = sumTris / n;
            stats.avgVertices = sumVerts / n;
            stats.avgAllocatedMemory = sumAllocMem / n;
            stats.totalGcAllocated = sumGc;
            stats.avgActiveRigidbodies = sumRigidbodies / n;

            // Standard deviation of frame time
            float sumSqDiff = 0;
            for (int i = 0; i < n; i++)
            {
                float diff = frameTimes[i] - stats.avgFrameTimeMs;
                sumSqDiff += diff * diff;
            }
            stats.stdDevFrameTimeMs = (float)Math.Sqrt(sumSqDiff / n);

            // Sort for percentiles
            Array.Sort(frameTimes);
            Array.Sort(fpsValues);

            stats.medianFrameTimeMs = Percentile(frameTimes, 0.50f);
            stats.p95FrameTimeMs = Percentile(frameTimes, 0.95f);
            stats.p99FrameTimeMs = Percentile(frameTimes, 0.99f);

            stats.medianFps = Percentile(fpsValues, 0.50f);
            stats.p5Fps = Percentile(fpsValues, 0.05f);
            stats.p1Fps = Percentile(fpsValues, 0.01f);

            return stats;
        }

        static float Percentile(float[] sorted, float p)
        {
            if (sorted.Length == 0) return 0;
            float index = p * (sorted.Length - 1);
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper) return sorted[lower];
            float frac = index - lower;
            return sorted[lower] * (1f - frac) + sorted[upper] * frac;
        }
    }
}
