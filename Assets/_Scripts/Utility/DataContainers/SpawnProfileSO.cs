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
        [Tooltip("Fixed period (seconds) between fauna spawn-cycle ticks — the ecosystem heartbeat. " +
                 "Platform default is 30s; scoring modes that ride the wave clock (Brood Rush) depend on it.")]
        [Min(0f)] public float BaseFaunaSpawnTime = 30f;
        [Tooltip("OFF (default): the tick is a SEEDER — it only tops each species up to its seed floor " +
                 "(PopulationSize), staying out while the food web sustains it. ON: every tick spawns a " +
                 "full fresh wave of PopulationSize fauna (clamped by MaxLivePopulation), so each cycle " +
                 "visibly births a brood in the controlling color — used by wave-scored modes (Brood Rush). " +
                 "Population is still bounded by starvation + the per-species cap; no imposed death.")]
        public bool SeedFullWaveEveryTick = false;
        [Tooltip("Population control (prey-linked), authored in NOMINAL PRISMS: a herbivore population " +
                 "only spawns while the cell holds at least this many prisms' worth of opposing " +
                 "ENVIRONMENT VOLUME (value × 16, the nominal leaf volume — volume is the spine; fauna " +
                 "bodies don't count, they aren't edible). Predator species read it directly as N live " +
                 "herbivores. Below the floor, production pauses until prey returns; existing fauna then " +
                 "starve. 0 = always produce. See Docs/ECOSYSTEM.md.")]
        [Min(0)] public int FaunaFoodFloor = 5;
        [Tooltip("Wait this many seconds after the crystal spawns before FaunaPrefab begins spawning.")]
        [Min(0f)] public float FaunaInitialDelaySeconds;
        [Tooltip("Seconds between each population spawn (within the initial FaunaPrefab batch). 0 = spawn all instantly.")]
        [Min(0f)] public float FaunaSpawnIntervalSeconds;
        [Tooltip("HERBIVORE spawn-point ring: successive herbivore waves rotate between this " +
                 "many points spaced evenly on a circle around the cell centre (equidistant " +
                 "from each other and from the centre), so each new group gets its own feeding " +
                 "ground — and a head start before a territorial predator's patch reaches it. " +
                 "0 or 1 = legacy behavior (spawn on the densest sensed mass). Predators are " +
                 "unaffected (they spawn on the densest mass as before).")]
        [Min(0)] public int HerbivoreSpawnPointCount = 0;
        [Tooltip("Radius of the herbivore spawn-point ring (world units from the cell centre).")]
        [Min(0f)] public float HerbivoreSpawnRadius = 400f;
        [Tooltip("PREDATOR spawn-point ring, orthogonal to the herbivore ring: points spaced " +
                 "evenly on a VERTICAL circle (the herbivore ring is equatorial/XZ), starting " +
                 "at +Y — so 2 points sit exactly on the poles. Solitary predators also spawn " +
                 "at most ONE per spawn interval while the ring is active, alternating points. " +
                 "0 = legacy behavior (spawn on the densest sensed mass, no per-interval cap).")]
        [Min(0)] public int PredatorSpawnPointCount = 0;
        [Tooltip("Radius of the predator spawn-point ring (world units from the cell centre).")]
        [Min(0f)] public float PredatorSpawnRadius = 600f;
        public List<FaunaConfigurationSO> SupportedFaunas = new();
        
        public FloraConfigurationSO GetRandomFlora() => SupportedFloras[0];
        public FaunaConfigurationSO GetRandomFauna() => SupportedFaunas[0];
    }
}