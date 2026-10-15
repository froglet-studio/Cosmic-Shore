using System;
using System.Collections.Generic;

// Unity.Profiling.LowLevel(.Unsafe) are mapped to these namespaces by the Live source sync.
namespace CosmicShore.Engine.Profiling.LowLevel
{
    public enum ProfilerMarkerDataType : byte
    {
        InstrumentationBlock = 0, Int32 = 2, UInt32 = 3, Int64 = 4, UInt64 = 5, Float = 6, Double = 7,
        String16 = 9, Blob8 = 11, GfxResourceId = 12,
    }

    [Flags]
    public enum MarkerFlags : ushort
    {
        Default = 0, Script = 1 << 1, ScriptInvoke = 1 << 5, ScriptDeepProfiler = 1 << 6, AvailabilityEditor = 1 << 2,
        AvailabilityNonDevelopment = 1 << 3, Warning = 1 << 4, Counter = 1 << 7, SampleGPU = 1 << 8,
    }
}

namespace CosmicShore.Engine.Profiling.LowLevel.Unsafe
{
    /// <summary>
    /// Original contract: one entry per profiler marker or counter the running player knows,
    /// enumerated with <see cref="GetAvailable"/> and named by <see cref="GetDescription"/>, and
    /// recordable with <c>new ProfilerRecorder(handle, ...)</c>. Here: one entry per marker a
    /// <see cref="ProfilerMarker"/> has declared so far (<see cref="MarkerCollector"/>), all in
    /// nanoseconds. Engine counters are not enumerated; a recorder names them directly.
    /// </summary>
    public readonly struct ProfilerRecorderHandle
    {
        readonly ulong _handle;
        ProfilerRecorderHandle(ulong handle) { _handle = handle; }

        public bool Valid => _handle != 0;
        internal int MarkerId => (int)_handle;

        public static void GetAvailable(List<ProfilerRecorderHandle> outRecorderHandleList)
        {
            outRecorderHandleList.Clear();
            var ids = new List<int>();
            MarkerCollector.GetDeclared(ids);
            foreach (int id in ids) outRecorderHandleList.Add(new ProfilerRecorderHandle((ulong)id));
        }

        public static ProfilerRecorderDescription GetDescription(ProfilerRecorderHandle handle)
        {
            string name = MarkerCollector.NameOf(handle.MarkerId);
            return name == null ? default : new ProfilerRecorderDescription(
                new ProfilerCategory(MarkerCollector.CategoryOf(handle.MarkerId)), name, ProfilerMarkerDataUnit.TimeNanoseconds);
        }
    }

    public readonly struct ProfilerRecorderDescription
    {
        internal ProfilerRecorderDescription(ProfilerCategory category, string name, ProfilerMarkerDataUnit unit)
        {
            Category = category;
            Name = name;
            UnitType = unit;
            DataType = ProfilerMarkerDataType.Int64;
            Flags = MarkerFlags.Script;
        }

        public ProfilerCategory Category { get; }
        public MarkerFlags Flags { get; }
        public ProfilerMarkerDataType DataType { get; }
        public ProfilerMarkerDataUnit UnitType { get; }
        public string Name { get; }
    }
}
