using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// A container that pairs a behavior with a selection weight.
    /// You could add fields like cooldown, name, or required states here as well.
    /// </summary>
    [System.Serializable]
    public struct FaunaBehaviorOption
    {
        public FaunaBehavior behavior;
        [Tooltip("Higher weight = higher chance of picking this behavior.")]
        public float weight;
    }
}
