using System;

namespace CosmicShore.Engine.Scripting
{
    /// <summary>Keeps the annotated member through code stripping (inert marker for now).</summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    public sealed class PreserveAttribute : Attribute { }

    /// <summary>Marks a member the code stripper must keep when its declaring type is kept (inert marker).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Delegate, Inherited = false)]
    public sealed class RequiredMemberAttribute : Attribute { }

    /// <summary>Unity's garbage-collector mode toggle surface (the port runs the .NET GC).</summary>
    public static class GarbageCollector
    {
        public enum Mode { Disabled = 0, Enabled = 1, Manual = 2 }
        public static Mode GCMode { get; set; } = Mode.Enabled;
        public static bool isIncremental => false;
        public static ulong incrementalTimeSliceNanoseconds { get; set; }
        public static bool CollectIncremental(ulong nanoseconds = 0) { System.GC.Collect(); return true; }
    }
}
