using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// A flat, JSON-serializable bag of named float parameters plus a set of enabled
    /// behavior modules. Genes that are not present fall back to the registry default
    /// when read, so adding a new gene to a module does not invalidate older genomes
    /// stored on disk.
    ///
    /// Crossover/mutation are explicit operations; the genome itself is otherwise a
    /// passive value object.
    /// </summary>
    [Serializable]
    public class TrainingGenome
    {
        // Parallel arrays so Unity's JsonUtility can round-trip the genome.
        [SerializeField] List<string> geneNames = new();
        [SerializeField] List<float> geneValues = new();
        [SerializeField] List<string> enabledModules = new();

        // Bookkeeping populated by the runner; not part of the search space.
        [SerializeField] public int GenerationBorn;
        [SerializeField] public int EvaluationCount;
        [SerializeField] public float Fitness;
        [SerializeField] public float NoveltyScore;
        [SerializeField] public string Lineage = "";

        public IReadOnlyList<string> EnabledModules => enabledModules;

        public bool IsModuleEnabled(string moduleName) => enabledModules.Contains(moduleName);

        public void SetModuleEnabled(string moduleName, bool enabled)
        {
            if (enabled)
            {
                if (!enabledModules.Contains(moduleName)) enabledModules.Add(moduleName);
            }
            else
            {
                enabledModules.Remove(moduleName);
            }
        }

        /// <summary>
        /// Reads a gene, falling back to the registry default if missing.
        /// Always clamped to the registry range so callers never see garbage.
        /// </summary>
        public float Get(string geneName)
        {
            int idx = geneNames.IndexOf(geneName);
            if (idx >= 0)
            {
                if (GeneRegistry.TryGetSpec(geneName, out var spec))
                    return spec.Clamp(geneValues[idx]);
                return geneValues[idx];
            }

            return GeneRegistry.TryGetSpec(geneName, out var fallback) ? fallback.Default : 0f;
        }

        public void Set(string geneName, float value)
        {
            if (GeneRegistry.TryGetSpec(geneName, out var spec))
                value = spec.Clamp(value);

            int idx = geneNames.IndexOf(geneName);
            if (idx >= 0)
            {
                geneValues[idx] = value;
            }
            else
            {
                geneNames.Add(geneName);
                geneValues.Add(value);
            }
        }

        public bool Has(string geneName) => geneNames.Contains(geneName);

        /// <summary>
        /// Builds a genome populated with each registered gene's default value.
        /// Modules flagged as "default enabled" in the registry are turned on.
        /// </summary>
        public static TrainingGenome FromRegistryDefaults()
        {
            var g = new TrainingGenome();
            foreach (var kv in GeneRegistry.Specs)
                g.Set(kv.Key, kv.Value.Default);
            foreach (var mod in GeneRegistry.DefaultEnabledModules)
                g.SetModuleEnabled(mod, true);
            return g;
        }

        /// <summary>
        /// Random genome over the full registered search space. Each module has a
        /// configurable probability of being active in the initial population.
        /// </summary>
        public static TrainingGenome Random(float moduleEnableProbability = 0.85f)
        {
            var g = new TrainingGenome();
            foreach (var kv in GeneRegistry.Specs)
                g.Set(kv.Key, kv.Value.RandomValue());
            foreach (var moduleName in GeneRegistry.Modules.Keys)
            {
                bool enabled = GeneRegistry.IsDefaultEnabled(moduleName)
                    || UnityEngine.Random.value < moduleEnableProbability;
                g.SetModuleEnabled(moduleName, enabled);
            }
            return g;
        }

        public TrainingGenome Clone()
        {
            var copy = new TrainingGenome
            {
                GenerationBorn = GenerationBorn,
                EvaluationCount = 0,
                Fitness = 0f,
                NoveltyScore = 0f,
                Lineage = Lineage
            };
            for (int i = 0; i < geneNames.Count; i++)
                copy.Set(geneNames[i], geneValues[i]);
            foreach (var m in enabledModules) copy.SetModuleEnabled(m, true);
            return copy;
        }

        /// <summary>
        /// Uniform per-gene crossover plus uniform per-module crossover.
        /// Lineage is recorded so we can audit which ancestors produced which fit children.
        /// </summary>
        public static TrainingGenome Crossover(TrainingGenome a, TrainingGenome b)
        {
            var child = new TrainingGenome
            {
                Lineage = $"{Snip(a.Lineage)}+{Snip(b.Lineage)}"
            };
            foreach (var kv in GeneRegistry.Specs)
            {
                float pick = UnityEngine.Random.value < 0.5f ? a.Get(kv.Key) : b.Get(kv.Key);
                child.Set(kv.Key, pick);
            }
            foreach (var moduleName in GeneRegistry.Modules.Keys)
            {
                bool pick = UnityEngine.Random.value < 0.5f
                    ? a.IsModuleEnabled(moduleName)
                    : b.IsModuleEnabled(moduleName);
                child.SetModuleEnabled(moduleName, pick);
            }
            return child;
        }

        /// <summary>
        /// Numeric jitter (Gaussian-ish) per gene plus an independent low-rate bit-flip per
        /// module enable bit. Keeping structural mutation rare is what lets the search
        /// converge instead of churning topology endlessly.
        /// </summary>
        public void Mutate(float numericRate, float numericStrength, float structuralRate)
        {
            foreach (var kv in GeneRegistry.Specs)
            {
                if (UnityEngine.Random.value > numericRate) continue;
                var spec = kv.Value;
                float current = Get(kv.Key);
                float range = (spec.Max - spec.Min) * numericStrength;
                float delta = SampleGaussian() * range;
                Set(kv.Key, current + delta);
            }

            foreach (var moduleName in GeneRegistry.Modules.Keys)
            {
                if (UnityEngine.Random.value > structuralRate) continue;
                if (GeneRegistry.IsDefaultEnabled(moduleName)) continue;
                SetModuleEnabled(moduleName, !IsModuleEnabled(moduleName));
            }
        }

        /// <summary>
        /// Box-Muller. Cheap, no allocations, good enough for parameter search.
        /// </summary>
        static float SampleGaussian()
        {
            float u1 = Mathf.Max(UnityEngine.Random.value, 1e-6f);
            float u2 = UnityEngine.Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }

        static string Snip(string s)
        {
            if (string.IsNullOrEmpty(s)) return "?";
            return s.Length <= 8 ? s : s.Substring(0, 8);
        }

        /// <summary>
        /// Compact behavior fingerprint used by the novelty archive to compare
        /// genomes without invoking another rollout. Hashes module bits and
        /// quantized parameter buckets so structurally similar genomes collide.
        /// </summary>
        public int BehaviorHash()
        {
            unchecked
            {
                int hash = 17;
                foreach (var moduleName in GeneRegistry.Modules.Keys)
                    hash = hash * 31 + (IsModuleEnabled(moduleName) ? moduleName.GetHashCode() : 0);
                foreach (var kv in GeneRegistry.Specs)
                {
                    float v = Get(kv.Key);
                    float t = (v - kv.Value.Min) / Mathf.Max(kv.Value.Max - kv.Value.Min, 1e-6f);
                    int bucket = Mathf.Clamp((int)(t * 8f), 0, 7);
                    hash = hash * 31 + (kv.Key.GetHashCode() ^ bucket);
                }
                return hash;
            }
        }

        public string Summarize()
        {
            var sb = new StringBuilder(256);
            sb.Append($"gen={GenerationBorn} eval={EvaluationCount} fit={Fitness:F2} nov={NoveltyScore:F2} | mods=");
            sb.Append(string.Join(",", enabledModules));
            return sb.ToString();
        }
    }
}
