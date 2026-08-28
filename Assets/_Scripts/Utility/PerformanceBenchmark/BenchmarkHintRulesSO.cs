using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Customizable set of <see cref="HintRule"/>s that the <see cref="BenchmarkAnalysis"/> engine
    /// evaluates against a run. Tune thresholds, advice text, severity, and add profiler-marker rules
    /// here. When no asset is supplied (or its list is empty), the engine falls back to
    /// <see cref="BenchmarkAnalysis.DefaultRules"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BenchmarkHintRules",
        menuName = "ScriptableObjects/Tools/Benchmark Hint Rules",
        order = 101)]
    public class BenchmarkHintRulesSO : ScriptableObject
    {
        [Tooltip("Rules evaluated against each run. Leave empty to use the built-in defaults.")]
        [SerializeField] private List<HintRule> rules = new();

        public IReadOnlyList<HintRule> Rules => rules;

        /// <summary>Returns the configured rules, or the built-in defaults when none are authored.</summary>
        public IList<HintRule> Resolve() =>
            rules != null && rules.Count > 0 ? rules : BenchmarkAnalysis.DefaultRules();

        [ContextMenu("Reset To Defaults")]
        private void ResetToDefaults()
        {
            rules = new List<HintRule>(BenchmarkAnalysis.DefaultRules());
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
