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

        // CPU vs GPU frame time (ms) from FrameTimingManager. 0 when frame-timing
        // stats are unavailable on the platform / not yet warmed up.
        public float cpuFrameTimeMs;
        public float gpuFrameTimeMs;

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

        // Netcode (NGO) - 0 when uninstrumented / non-networked. netcodeTimeMs is the summed
        // self time of the CSM.Net.* markers this frame.
        public float netcodeTimeMs;
        public int rpcsSent;
        public int netVarsDirty;
        public long netBytesSent;

        // Game load - gameplay object counts so frame cost can be read against
        // the active workload. 0 when the source manager/data is unavailable.
        public int activePrisms;
        public int activeExplosions;
        public int activeImplosions;
        public int activeVessels;
        public int activePlayers;
    }
}
