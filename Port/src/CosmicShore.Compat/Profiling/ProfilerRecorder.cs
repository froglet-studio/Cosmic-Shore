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
    /// Original contract: records a named profiler marker or engine counter.
    ///
    /// <para><b>A marker</b> (anything a <see cref="ProfilerMarker"/> times) records through
    /// <see cref="MarkerCollector"/>: one sample per frame the marker ran, in nanoseconds of
    /// main-thread time with that frame's call count, kept in a ring of <c>capacity</c> frames.</para>
    ///
    /// <para><b>A counter</b> the engine can measure reads live: "Main Thread" frame time in ns,
    /// "GC Allocated In Frame" (the loop thread's bytes in the last frame), "GC Reserved/Used Memory",
    /// "System Used Memory". Every other counter (draw calls, batches, network) is valid but reads 0.</para>
    ///
    /// <para>The state lives in a shared buffer, so copies of this struct are one recorder, as with
    /// Unity's native handle (<c>diag</c> resets and restarts copies held in a list).</para>
    /// </summary>
    public struct ProfilerRecorder : IDisposable
    {
        readonly string _name;
        readonly MarkerSampleBuffer _buffer;

        public ProfilerRecorder(ProfilerCategory category, string statName, int capacity = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
            : this(category.Name, statName, capacity, options) { }

        public ProfilerRecorder(string categoryName, string statName, int capacity = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
        {
            _name = statName ?? "";
            int id = IsCounter(_name) ? 0 : MarkerCollector.Register(categoryName, _name, declared: false);
            _buffer = new MarkerSampleBuffer(id, capacity);
            if ((options & ProfilerRecorderOptions.StartImmediately) != 0) _buffer.Start();
        }

        /// <summary>Records the marker a <see cref="LowLevel.Unsafe.ProfilerRecorderHandle"/> names (from <c>GetAvailable</c>).</summary>
        public ProfilerRecorder(LowLevel.Unsafe.ProfilerRecorderHandle statHandle, int capacity = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
        {
            var description = LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(statHandle);
            _name = description.Name ?? "";
            _buffer = new MarkerSampleBuffer(statHandle.MarkerId, capacity);
            if ((options & ProfilerRecorderOptions.StartImmediately) != 0) _buffer.Start();
        }

        ProfilerRecorder(ProfilerMarker marker, int capacity, ProfilerRecorderOptions options)
        {
            _name = marker.Name ?? "";
            _buffer = new MarkerSampleBuffer(marker.Id, capacity);
            if ((options & ProfilerRecorderOptions.StartImmediately) != 0) _buffer.Start();
        }

        public static ProfilerRecorder StartNew(ProfilerCategory category, string statName, int capacity = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
            => new(category, statName, capacity, options | ProfilerRecorderOptions.StartImmediately);

        public static ProfilerRecorder StartNew(ProfilerMarker marker, int capacity = 1, ProfilerRecorderOptions options = ProfilerRecorderOptions.Default)
            => new(marker, capacity, options | ProfilerRecorderOptions.StartImmediately);

        static bool IsCounter(string name) => name is "Main Thread" or "CPU Main Thread Frame Time" or "CPU Total Frame Time"
            or "GC Reserved Memory" or "Total Reserved Memory" or "GC Used Memory" or "Total Used Memory"
            or "System Used Memory" or "GC Allocated In Frame"
            || name.EndsWith(" Count", StringComparison.Ordinal) || name.StartsWith("CSM ", StringComparison.Ordinal);

        bool IsMarker => _buffer != null && _buffer.MarkerId > 0;

        public bool Valid => _buffer != null;
        public bool IsRunning => _buffer?.Running ?? false;
        public bool WrappedAround => IsMarker && _buffer.WrappedAround;
        public int Capacity => IsMarker ? _buffer.Capacity : 1;
        public int Count => IsMarker ? _buffer.Count : 1;
        public ProfilerMarkerDataUnit UnitType => IsMarker ? ProfilerMarkerDataUnit.TimeNanoseconds : ProfilerMarkerDataUnit.Undefined;
        public long CurrentValue => IsMarker ? _buffer.Last.value : Read();
        public double CurrentValueAsDouble => CurrentValue;
        public long LastValue => IsMarker ? _buffer.Last.value : Read();
        public double LastValueAsDouble => LastValue;

        long Read() => _name switch
        {
            "Main Thread" or "CPU Main Thread Frame Time" or "CPU Total Frame Time" => (long)(Time.unscaledDeltaTime * 1e9),
            "GC Reserved Memory" or "Total Reserved Memory" => GC.GetGCMemoryInfo().HeapSizeBytes,
            "GC Used Memory" or "Total Used Memory" => GC.GetTotalMemory(false),
            "System Used Memory" => Environment.WorkingSet,
            "GC Allocated In Frame" => MarkerCollector.LastFrameAllocatedBytes,
            _ => 0,
        };

        public void Start() => _buffer?.Start();
        public void Stop() => _buffer?.Stop();
        public void Reset() => _buffer?.Reset();

        public ProfilerRecorderSample GetSample(int index)
        {
            if (!IsMarker) return new() { Value = Read(), Count = 1 };
            var (value, count) = _buffer.Get(index);
            return new() { Value = value, Count = count };
        }

        public void CopyTo(List<ProfilerRecorderSample> outSamples, bool reset = false)
        {
            outSamples.Clear();
            for (int i = 0; i < Count; i++) outSamples.Add(GetSample(i));
            if (reset && IsMarker) { bool running = _buffer.Running; _buffer.Reset(); if (running) _buffer.Start(); }
        }

        public void Dispose() => _buffer?.Stop();
    }
}
