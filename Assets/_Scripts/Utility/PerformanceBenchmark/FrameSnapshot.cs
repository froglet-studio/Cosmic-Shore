using System;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// A single frame's worth of performance data captured during a benchmark run.
    /// All times are in milliseconds.
    /// </summary>
    [Serializable]
    public struct FrameSnapshot
    {
        public int frameIndex;
        public float deltaTimeMs;
        public float fps;

        // Rendering
        public int drawCalls;
        public int batches;
        public int setPassCalls;
        public int triangles;
        public int vertices;

        // Memory (bytes)
        public long totalAllocatedMemory;
        public long totalReservedMemory;
        public long gcAllocatedPerFrame;

        // Physics
        public int activeRigidbodies;
    }
}
