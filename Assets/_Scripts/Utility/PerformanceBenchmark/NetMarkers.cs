using Unity.Profiling;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Profiler markers + counters for Unity Netcode for GameObjects (NGO) hot paths. Netcode
    /// cost is otherwise hidden inside generic "Scripts" markers; instrumenting it is the tool's
    /// unique value over the stock Profiler.
    ///
    /// Markers wrap NGO hot paths (serialize/deserialize, RPC dispatch, spawn/despawn, per-tick).
    /// Counters track per-frame RPC and NetworkVariable-dirty volume; they reset each frame
    /// (ResetToZeroOnFlush) so the benchmark collector reads a per-frame value.
    ///
    /// Placement of these markers lives in the gameplay/netcode code (see callers), not in the
    /// tool. Reading them lives in <see cref="PerformanceBenchmarkRunner"/>.
    /// </summary>
    public static class NetMarkers
    {
        public static readonly ProfilerMarker Tick = new(ProfilerCategory.Network, "CSM.Net.Tick");
        public static readonly ProfilerMarker Serialize = new(ProfilerCategory.Network, "CSM.Net.Serialize");
        public static readonly ProfilerMarker Deserialize = new(ProfilerCategory.Network, "CSM.Net.Deserialize");
        public static readonly ProfilerMarker SpawnDespawn = new(ProfilerCategory.Network, "CSM.Net.SpawnDespawn");
        public static readonly ProfilerMarker RpcDispatch = new(ProfilerCategory.Network, "CSM.Net.RpcDispatch");

        public static readonly ProfilerCounterValue<int> RpcsSent =
            new(ProfilerCategory.Network, "CSM RPCs Sent", ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        public static readonly ProfilerCounterValue<int> NetVarsDirty =
            new(ProfilerCategory.Network, "CSM NetVars Dirty", ProfilerMarkerDataUnit.Count,
                ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        public static readonly ProfilerCounterValue<long> BytesSent =
            new(ProfilerCategory.Network, "CSM Bytes Sent", ProfilerMarkerDataUnit.Bytes,
                ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush);

        /// <summary>Bump the per-frame RPC-sent counter. Call where an RPC is invoked.</summary>
        public static void CountRpc(int n = 1) => RpcsSent.Value += n;

        /// <summary>Bump the per-frame NetworkVariable-dirty counter. Call where a NetworkVariable value is set.</summary>
        public static void CountNetVarDirty(int n = 1) => NetVarsDirty.Value += n;

        /// <summary>Add to the per-frame bytes-sent counter (if a payload size is known).</summary>
        public static void AddBytesSent(long bytes) => BytesSent.Value += bytes;
    }
}
