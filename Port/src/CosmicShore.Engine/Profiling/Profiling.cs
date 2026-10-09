using System;

namespace CosmicShore.Engine.Profiling
{
    // First-party stand-ins for the Unity.Profiling surface the codebase instruments
    // with; call sites port verbatim (using-swap only). Markers record into
    // MarkerCollector (main loop thread, inclusive time, calls, allocated bytes).

    public readonly struct ProfilerCategory
    {
        public readonly string Name;
        ProfilerCategory(string name) { Name = name; }
        public static ProfilerCategory Network => new("Network");
        public static ProfilerCategory Scripts => new("Scripts");
        public static ProfilerCategory Render => new("Render");
        public static ProfilerCategory Memory => new("Memory");
        public static ProfilerCategory Physics => new("Physics");
        public static ProfilerCategory Internal => new("Internal");
        public static ProfilerCategory Ai => new("Ai");
        public static ProfilerCategory Animation => new("Animation");
        public static ProfilerCategory Audio => new("Audio");
        public static ProfilerCategory Gui => new("Gui");
        public static ProfilerCategory Input => new("Input");
        public static ProfilerCategory Lighting => new("Lighting");
        public static ProfilerCategory Loading => new("Loading");
        public static ProfilerCategory Particles => new("Particles");
        public static ProfilerCategory Video => new("Video");
        public static ProfilerCategory Vr => new("Vr");
        public static ProfilerCategory FileIO => new("FileIO");
        public ProfilerCategory(string name, ushort color = 0) : this(name) { }
        public override string ToString() => Name;
    }

    public enum ProfilerMarkerDataUnit { Undefined = 0, TimeNanoseconds = 1, Bytes = 2, Count = 3, Percent = 4 }

    [Flags]
    public enum ProfilerCounterOptions { None = 0, FlushOnEndOfFrame = 1, ResetToZeroOnFlush = 2 }

    public readonly struct ProfilerMarker
    {
        public readonly string Name;
        /// <summary>This engine's collector id for the marker (0 for a default marker).</summary>
        public readonly int Id;
        public ProfilerMarker(string name) : this(ProfilerCategory.Scripts, name) { }
        public ProfilerMarker(ProfilerCategory category, string name)
        {
            Name = name;
            Id = MarkerCollector.Register(category.Name, name, declared: true);
        }

        public readonly struct AutoScope : IDisposable
        {
            readonly int _id;
            internal AutoScope(int id) { _id = id; }
            public void Dispose() => MarkerCollector.End(_id);
        }

        public AutoScope Auto() { MarkerCollector.Begin(Id); return new AutoScope(Id); }
        public void Begin() => MarkerCollector.Begin(Id);
        public void Begin(Object contextUnityObject) => MarkerCollector.Begin(Id);
        public void End() => MarkerCollector.End(Id);
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
