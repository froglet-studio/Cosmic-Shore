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
    /// recordable with <c>new ProfilerRecorder(handle, ...)</c>. This engine has no marker
    /// collector yet (see <c>Profiling.cs</c>), so it knows no markers: <see cref="GetAvailable"/>
    /// returns an empty list and a caller that looks a marker up by name finds nothing, which is
    /// what <c>diag</c> reports (<c>found: false</c>) instead of a number the engine never measured.
    /// </summary>
    public readonly struct ProfilerRecorderHandle
    {
        readonly ulong _handle;
        ProfilerRecorderHandle(ulong handle) { _handle = handle; }

        public bool Valid => _handle != 0;

        public static void GetAvailable(List<ProfilerRecorderHandle> outRecorderHandleList) => outRecorderHandleList.Clear();

        public static ProfilerRecorderDescription GetDescription(ProfilerRecorderHandle handle) => default;
    }

    public readonly struct ProfilerRecorderDescription
    {
        public ProfilerCategory Category { get; }
        public MarkerFlags Flags { get; }
        public ProfilerMarkerDataType DataType { get; }
        public ProfilerMarkerDataUnit UnitType { get; }
        public string Name { get; }
    }
}
