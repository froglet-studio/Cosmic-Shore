using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Utility
{
    [CreateAssetMenu(
        fileName = "FaunaConfiguration",
        menuName = "ScriptableObjects/DataContainers/" + nameof(FaunaConfigurationSO))]
    public class FaunaConfigurationSO : ScriptableObject
    {
        public Fauna FaunaPrefab;
        public int InitialSpawnCount;
        [Tooltip("Fauna instantiated per population burst (fixed swarm size). Prism count drives their " +
                 "aggression/behavior, not this count. See Docs/ECOSYSTEM.md.")]
        [Min(1)] public int PopulationSize = 4;
        public float SpawnProbability;
    }
}