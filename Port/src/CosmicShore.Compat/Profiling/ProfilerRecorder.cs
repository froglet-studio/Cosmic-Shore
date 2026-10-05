using System;
using System.Collections.Generic;

// Unity.Profiling is mapped to this namespace by the Live source sync.
namespace CosmicShore.Engine.Profiling
{
    [Flags]
    public enum ProfilerRecorderOptions
    {
        None = 0, StartImmediately = 1, KeepAliveDuringDomainReload = 2, CollectOnlyOnCurrentThread = 4,
        WrapAroundWhenCapacityReached = 8, SumAllSamplesInFrame = 16, GpuRecorder = 64, Default = StartImmediately | SumAllSamplesInFrame,
    }

    public struct ProfilerRecorderSample { public long Value { get; set; } public long Count { get; set; } }

    /// <summary>
    /// Original contract: reads a named engine counter. The port answers the counters it
    /// can measure ("Main Thread" frame time in ns, "GC Reserved/Used Memory" in bytes,
    /// "System Used Memory"); every other counter is valid but reads 0.
    /// </summary>
    public struct ProfilerRecorder : IDisposable
    {
        readonly string _name;
        bool _running;

        public ProfilerRecorder(ProfilerCategory category, string statName, int capacity = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
        { _name = statName; _running = (options & ProfilerRecorderOptions.StartImmediately) != 0; }

        public ProfilerRecorder(string categoryName, string statName, int capacity = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
        { _name = statName; _running = (options & ProfilerRecorderOptions.StartImmediately) != 0; }

        public static ProfilerRecorder StartNew(ProfilerCategory category, string statName, int capacity = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
            => new(category, statName, capacity, options | ProfilerRecorderOptions.StartImmediately);

        public static ProfilerRecorder StartNew(ProfilerMarker marker, int capacity = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
            => new("Scripts", "marker", capacity, options | ProfilerRecorderOptions.StartImmediately);

        public bool Valid => _name != null;
        public bool IsRunning => _running;
        public bool WrappedAround => false;
        public int Capacity => 1;
        public int Count => 1;
        public ProfilerMarkerDataUnit UnitType => ProfilerMarkerDataUnit.Undefined;
        public long CurrentValue => Read();
        public double CurrentValueAsDouble => Read();
        public long LastValue => Read();
        public double LastValueAsDouble => Read();

        long Read() => _name switch
        {
            "Main Thread" or "CPU Main Thread Frame Time" or "CPU Total Frame Time" => (long)(Time.unscaledDeltaTime * 1e9),
            "GC Reserved Memory" or "Total Reserved Memory" => GC.GetGCMemoryInfo().HeapSizeBytes,
            "GC Used Memory" or "Total Used Memory" => GC.GetTotalMemory(false),
            "System Used Memory" => Environment.WorkingSet,
            "GC Allocated In Frame" => 0,
            _ => 0,
        };

        public void Start() => _running = true;
        public void Stop() => _running = false;
        public void Reset() { }
        public ProfilerRecorderSample GetSample(int index) => new() { Value = Read(), Count = 1 };
        public void CopyTo(List<ProfilerRecorderSample> outSamples, bool reset = false) { outSamples.Clear(); outSamples.Add(GetSample(0)); }
        public void Dispose() => _running = false;
    }
}
