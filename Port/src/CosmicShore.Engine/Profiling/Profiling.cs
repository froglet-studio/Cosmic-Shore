using System;

namespace CosmicShore.Engine.Profiling
{
    // First-party stand-ins for the Unity.Profiling surface the codebase instruments
    // with. No-ops until the benchmark tool gets a collector in this engine; call
    // sites port verbatim (using-swap only).

    public readonly struct ProfilerCategory
    {
        public readonly string Name;
        ProfilerCategory(string name) { Name = name; }
        public static ProfilerCategory Network => new("Network");
        public static ProfilerCategory Scripts => new("Scripts");
        public static ProfilerCategory Render => new("Render");
    }

    public enum ProfilerMarkerDataUnit { Undefined = 0, TimeNanoseconds = 1, Bytes = 2, Count = 3, Percent = 4 }

    [Flags]
    public enum ProfilerCounterOptions { None = 0, FlushOnEndOfFrame = 1, ResetToZeroOnFlush = 2 }

    public readonly struct ProfilerMarker
    {
        public readonly string Name;
        public ProfilerMarker(string name) { Name = name; }
        public ProfilerMarker(ProfilerCategory category, string name) { Name = name; }

        public readonly struct AutoScope : IDisposable { public void Dispose() { } }
        public AutoScope Auto() => default;
        public void Begin() { }
        public void End() { }
    }

    // Class (not struct, unlike the original): the original wrote through native
    // memory from readonly fields; a managed struct can't, a class can.
    public sealed class ProfilerCounterValue<T> where T : struct
    {
        public T Value;
        public ProfilerCounterValue(ProfilerCategory category, string name, ProfilerMarkerDataUnit unit,
            ProfilerCounterOptions options = ProfilerCounterOptions.None) { Value = default; }
    }
}
