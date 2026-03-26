using UnityEngine;

namespace CosmicShore.Game.Fauna
{
    /// <summary>
    /// Defines a variant species of Locust with its own config and prefab overrides.
    /// Multiple variants can be registered in a LocustSwarmManager to introduce
    /// visual and behavioral diversity within a single swarm.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LocustVariant",
        menuName = "ScriptableObjects/Fauna/Locust Variant")]
    public class LocustVariantSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name for this locust variant.")]
        public string VariantName = "Common Locust";

        [Header("Prefab")]
        [Tooltip("Locust prefab for this variant. If null, the swarm manager's default prefab is used.")]
        public Locust LocustPrefab;

        [Header("Behavior Config")]
        [Tooltip("Config overrides for this variant. If null, the swarm manager's default config is used.")]
        public LocustConfigSO Config;

        [Header("Variant Tuning")]
        [Tooltip("Speed multiplier relative to the base config. 1 = normal.")]
        [Range(0.5f, 2f)] public float SpeedMultiplier = 1f;

        [Tooltip("Consumption rate multiplier relative to the base config. 1 = normal.")]
        [Range(0.5f, 3f)] public float ConsumptionMultiplier = 1f;

        [Tooltip("Size multiplier for the locust's embedded health prism. 1 = normal boid size.")]
        [Range(0.5f, 2f)] public float SizeMultiplier = 1f;

        [Tooltip("Relative probability of this variant being chosen during spawning.")]
        [Min(0.01f)] public float SpawnWeight = 1f;
    }
}
