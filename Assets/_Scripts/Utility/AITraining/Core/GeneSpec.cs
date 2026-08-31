using System;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Declares a single named gene the training framework can search over.
    /// Behavior modules and fitness components register their genes through GeneRegistry
    /// so the genome stays extensible without changing core types.
    /// </summary>
    [Serializable]
    public struct GeneSpec : IEquatable<GeneSpec>
    {
        public string Name;
        public float Min;
        public float Max;
        public float Default;

        public GeneSpec(string name, float min, float max, float defaultValue)
        {
            Name = name;
            Min = min;
            Max = max;
            Default = Mathf.Clamp(defaultValue, min, max);
        }

        public float Clamp(float value) => Mathf.Clamp(value, Min, Max);

        public float RandomValue() => UnityEngine.Random.Range(Min, Max);

        public bool Equals(GeneSpec other) => Name == other.Name;

        public override bool Equals(object obj) => obj is GeneSpec other && Equals(other);

        public override int GetHashCode() => Name?.GetHashCode() ?? 0;

        public override string ToString() => $"{Name}=[{Min}..{Max}]({Default})";
    }
}
