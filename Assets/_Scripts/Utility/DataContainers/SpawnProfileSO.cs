using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    [CreateAssetMenu(
        fileName = "SpawnProfile",
        menuName = "ScriptableObjects/DataContainers/" + nameof(SpawnProfileSO))]
    public class SpawnProfileSO : ScriptableObject
    {
        [Header("Flora Configs")]
        [Tooltip("If ON, spawned FaunaPrefab populations will never use the local player's domain.")]
        public bool FloraExcludeLocalDomain = true;
        [Min(0f)] public float FloraSpawnVolumeCeiling = 12000f;
        [Tooltip("Wait this many seconds after the crystal spawns before flora begins spawning.")]
        [Min(0f)] public float FloraInitialDelaySeconds;
        [Tooltip("Seconds between each flora spawn (within the initial flora batch). 0 = spawn all instantly.")]
        [Min(0f)] public float FloraSpawnIntervalSeconds;
        [Tooltip("Flora regrowth pulse: once a cell fills past Frozen, existing flora resume growing " +
                 "for FloraRegrowthPulseDuration seconds out of every FloraRegrowthPulsePeriod seconds " +
                 "(capped at Rabid) so the canopy keeps breathing instead of freezing solid. " +
                 "<= 0 falls back to a sensible default. See Docs/ECOSYSTEM.md.")]
        [Min(0f)] public float FloraRegrowthPulsePeriod = 15f;
        [Min(0f)] public float FloraRegrowthPulseDuration = 4f;
        public List<FloraConfigurationSO> SupportedFloras = new();
        
        [Header("FaunaPrefab Configs")]
        [Tooltip("If ON, spawned FaunaPrefab populations will never use the local player's domain.")]
        public bool FaunaExcludeLocalDomain = true;
        [Min(0f)] public float InitialFaunaSpawnWaitTime = 10f;
        [Min(0f)] public float FaunaSpawnVolumeThreshold = 1f;
        [Min(0f)] public float BaseFaunaSpawnTime = 60f;
        [Tooltip("Population control (prey-linked): a population only spawns while the cell holds at least " +
                 "this many prisms NOT of the controlling color (prey). Below it, production pauses until " +
                 "prey returns; existing fauna then starve. 0 = always produce. See Docs/ECOSYSTEM.md.")]
        [Min(0)] public int FaunaFoodFloor = 5;
        [Tooltip("Wait this many seconds after the crystal spawns before FaunaPrefab begins spawning.")]
        [Min(0f)] public float FaunaInitialDelaySeconds;
        [Tooltip("Seconds between each population spawn (within the initial FaunaPrefab batch). 0 = spawn all instantly.")]
        [Min(0f)] public float FaunaSpawnIntervalSeconds;
        public List<FaunaConfigurationSO> SupportedFaunas = new();
        
        public FloraConfigurationSO GetRandomFlora() => SupportedFloras[0];
        public FaunaConfigurationSO GetRandomFauna() => SupportedFaunas[0];
    }
}