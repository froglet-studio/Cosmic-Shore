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
        [Tooltip("DEPRECATED — inert. The locked no-domain-asymmetry invariant says all three " +
                 "domains seed flora, so the spawners no longer roll an excluded domain " +
                 "(CLAUDE.md ▸ Ecosystem Design Principles). Kept only so legacy assets " +
                 "deserialize; remove with the next SpawnProfile asset migration.")]
        public bool FloraExcludeLocalDomain = false;
        [Min(0f)] public float FloraSpawnVolumeCeiling = 12000f;
        [Tooltip("Wait this many seconds after the crystal spawns before flora begins spawning.")]
        [Min(0f)] public float FloraInitialDelaySeconds;
        [Tooltip("Seconds between each flora spawn (within the initial flora batch). 0 = spawn all instantly.")]
        [Min(0f)] public float FloraSpawnIntervalSeconds;
        // The flora regrowth pulse (FloraRegrowthPulsePeriod / FloraRegrowthPulseDuration)
        // was removed: it was a hard-coded growth oscillator faking the "breathing" the
        // food web is meant to produce. Mass is conserved — growth resumes only when an
        // active force lowers the prism count below Frenzy. See Docs/ECOSYSTEM.md §0.
        public List<FloraConfigurationSO> SupportedFloras = new();
        
        [Header("FaunaPrefab Configs")]
        [Tooltip("DEPRECATED — inert. Fauna spawn in the cell's controlling color only (locked " +
                 "no-domain-asymmetry invariant); no spawner reads this. Kept only so legacy " +
                 "assets deserialize; remove with the next SpawnProfile asset migration.")]
        public bool FaunaExcludeLocalDomain = false;
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