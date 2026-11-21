using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Process-wide registry of every gene the training framework can mutate.
    /// Behavior modules and fitness components register their genes once at boot;
    /// the genome and population layers iterate the registry, so adding a new
    /// behavior means dropping in a new module — no central edit needed.
    ///
    /// Modules are registered into named groups so the population can do
    /// structural mutation (turning an entire module on or off) without losing
    /// the parameter values inside it.
    /// </summary>
    public static class GeneRegistry
    {
        static readonly Dictionary<string, GeneSpec> s_Specs = new();
        static readonly Dictionary<string, List<string>> s_Modules = new();
        static readonly HashSet<string> s_DefaultEnabledModules = new();

        public static IReadOnlyDictionary<string, GeneSpec> Specs => s_Specs;
        public static IReadOnlyDictionary<string, List<string>> Modules => s_Modules;
        public static IEnumerable<string> DefaultEnabledModules => s_DefaultEnabledModules;

        /// <summary>
        /// O(1) membership check. Exposed as a method rather than a property because
        /// IReadOnlyCollection.Contains routes through MemoryExtensions for strings,
        /// which requires an explicit StringComparison and fails to type-check.
        /// </summary>
        public static bool IsDefaultEnabled(string moduleName) => s_DefaultEnabledModules.Contains(moduleName);

        public static void Register(string moduleName, GeneSpec spec, bool defaultEnabled = true)
        {
            if (string.IsNullOrEmpty(spec.Name))
            {
                Debug.LogError("[GeneRegistry] Cannot register gene with empty name.");
                return;
            }

            if (s_Specs.TryGetValue(spec.Name, out var existing))
            {
                if (existing.Min != spec.Min || existing.Max != spec.Max)
                {
                    Debug.LogWarning(
                        $"[GeneRegistry] Gene '{spec.Name}' already registered with " +
                        $"range [{existing.Min}, {existing.Max}]; new range " +
                        $"[{spec.Min}, {spec.Max}] ignored.");
                }
                return;
            }

            s_Specs[spec.Name] = spec;

            if (string.IsNullOrEmpty(moduleName)) moduleName = "Core";
            if (!s_Modules.TryGetValue(moduleName, out var list))
            {
                list = new List<string>();
                s_Modules[moduleName] = list;
            }
            list.Add(spec.Name);

            if (defaultEnabled) s_DefaultEnabledModules.Add(moduleName);
        }

        public static bool TryGetSpec(string geneName, out GeneSpec spec) =>
            s_Specs.TryGetValue(geneName, out spec);

        /// <summary>
        /// Wipes all registrations. Tests use this; normal play does not.
        /// </summary>
        public static void Clear()
        {
            s_Specs.Clear();
            s_Modules.Clear();
            s_DefaultEnabledModules.Clear();
        }
    }
}
