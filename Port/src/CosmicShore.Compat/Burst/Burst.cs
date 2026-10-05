using System;

namespace Unity.Burst
{
    // Burst is a compiler, not a runtime: in the port every [BurstCompile] job and function simply
    // runs as ordinary managed C#. These attributes and enums exist so annotated code compiles
    // unchanged; they carry their authored values but nothing reads them.

    public enum FloatMode { Default = 0, Strict = 1, Deterministic = 2, Fast = 3 }

    public enum FloatPrecision { Standard = 0, High = 1, Medium = 2, Low = 3 }

    public enum OptimizeFor { Default = 0, Performance = 1, Size = 2, FastCompilation = 3, Balanced = 4 }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Assembly)]
    public sealed class BurstCompileAttribute : Attribute
    {
        public BurstCompileAttribute() { }
        public BurstCompileAttribute(FloatPrecision floatPrecision, FloatMode floatMode)
        {
            FloatPrecision = floatPrecision;
            FloatMode = floatMode;
        }

        public FloatMode FloatMode { get; set; }
        public FloatPrecision FloatPrecision { get; set; }
        public bool CompileSynchronously { get; set; }
        public bool Debug { get; set; }
        public bool DisableSafetyChecks { get; set; }
        public bool DisableDirectCall { get; set; }
        public OptimizeFor OptimizeFor { get; set; }
        public string[] Options { get; set; }
    }

    /// <summary>Marks a method whose body Burst strips (managed-only code). Runs normally here.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class BurstDiscardAttribute : Attribute { }

    /// <summary>Aliasing hint for Burst's optimizer; no effect here.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Struct | AttributeTargets.Method)]
    public sealed class NoAliasAttribute : Attribute { }

    /// <summary>Burst compiler status. Never enabled in the port.</summary>
    public static class BurstCompiler
    {
        public static bool IsEnabled => false;
        public static readonly BurstCompilerOptions Options = new BurstCompilerOptions();
    }

    public sealed class BurstCompilerOptions
    {
        public bool EnableBurstCompilation { get; set; }
        public bool EnableBurstCompileSynchronously { get; set; }
        public bool EnableBurstSafetyChecks { get; set; }
        public bool IsEnabled => false;
    }

    /// <summary>
    /// A static value shared between managed and Burst code, keyed by (context type, value type).
    /// Here it is a plain per-key managed box; <see cref="Data"/> returns a reference into it so
    /// writes through <c>ref</c> are visible to every reader, as in Unity.
    /// </summary>
    public readonly struct SharedStatic<T> where T : struct
    {
        readonly Box _box;

        SharedStatic(Box box) { _box = box; }

        public ref T Data => ref _box.Value;

        public static SharedStatic<T> GetOrCreate<TContext>(uint alignment = 0) => new SharedStatic<T>(Store<TContext>.Box);
        public static SharedStatic<T> GetOrCreate<TContext, TSubContext>(uint alignment = 0) => new SharedStatic<T>(Store<(TContext, TSubContext)>.Box);
        public static SharedStatic<T> GetOrCreate(Type contextType, uint alignment = 0) => new SharedStatic<T>(TypeStore.Get(contextType));
        public static SharedStatic<T> GetOrCreate(Type contextType, Type subContextType, uint alignment = 0) => new SharedStatic<T>(TypeStore.Get((contextType, subContextType)));

        sealed class Box { public T Value; }

        static class Store<TKey> { public static readonly Box Box = new Box(); }

        static class TypeStore
        {
            static readonly System.Collections.Concurrent.ConcurrentDictionary<object, Box> Boxes = new();
            public static Box Get(object key) => Boxes.GetOrAdd(key, _ => new Box());
        }
    }
}
