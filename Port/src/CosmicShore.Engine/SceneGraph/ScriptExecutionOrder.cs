using System;
using System.Collections.Concurrent;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Unity's Script Execution Order settings: the per-script <c>executionOrder</c> a project
    /// stores in each script's <c>.meta</c> (MonoImporter). The content layer registers them before
    /// any scene loads. A registered value takes precedence over <see cref="DefaultExecutionOrderAttribute"/>
    /// (Unity's manual: the Script Execution Order settings override the attribute). The order
    /// applies to Awake/OnEnable during a scene load, to Start, and to every per-frame phase.
    /// </summary>
    public static class ScriptExecutionOrder
    {
        static readonly ConcurrentDictionary<Type, int> s_overrides = new();

        /// <summary>Registers <paramref name="type"/>'s order (0 removes it) and refreshes its cached lifecycle.</summary>
        public static void Set(Type type, int order)
        {
            if (type == null) return;
            if (order == 0) s_overrides.TryRemove(type, out _);
            else s_overrides[type] = order;
            LifecycleHooks.Invalidate(type);
        }

        public static bool TryGet(Type type, out int order) => s_overrides.TryGetValue(type, out order);

        /// <summary>Test seam: forget every registered order.</summary>
        public static void Clear()
        {
            foreach (var t in s_overrides.Keys) LifecycleHooks.Invalidate(t);
            s_overrides.Clear();
        }
    }
}
