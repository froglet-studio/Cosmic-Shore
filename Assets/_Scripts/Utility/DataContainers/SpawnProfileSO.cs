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
        [Tooltip("DEPRECATED - inert. The locked no-domain-asymmetry invariant says all three " +
                 "domains seed flora, so the spawners no longer roll an excluded domain " +
                 "(CLAUDE.md ▸ Ecosystem Design Principles). Kept only so legacy assets " +
                 "deserialize; remove with the next SpawnProfile asset migration.")]
        public bool FloraExcludeLocalDomain = false;
        [Min(0f)] public float FloraSpawnVolumeCeiling = 12000f;
        [Tooltip("Wait this many seconds after the crystal spawns before flora begins spawning.")]
        [Min(0f)] public float FloraInitialDelaySeconds;
        [Tooltip("Seconds between each flora spawn (within the initial flora batch). 0 = spawn all instantly.")]
        [Min(0f)] public float FloraSpawnIntervalSeconds;

        [Header("Density - the CELL's half of the flora split")]
        [Tooltip("HOW MANY plants this cell carries, as a multiplier on every species' " +
                 "FloraConfigurationSO.InitialSpawnCount, its PopulationSize seed floor AND its " +
                 "MaxLivePopulation cap. 1 = exactly as authored.\n\n" +
                 "It scales the CAP as well as the floors on purpose (same rule as " +
                 "FaunaPopulationScale): the cap is what actually bounds a standing population, " +
                 "so a scalar that moved only the seed counts would be clamped away above ~1.5x " +
                 "and read as doing nothing. Scaling PRODUCTION is all it does - nothing is ever " +
                 "culled to meet a lowered scale (Docs/ECOSYSTEM.md §0: no imposed death).\n\n" +
                 "This exists so a cell can scale its whole forest WITHOUT forking the per-species " +
                 "assets. A SpawnProfile is referenced from CellConfigDataSO, so it already forks " +
                 "per intensity for free under CellTypeChoiceOptions.IntensityWise - which makes " +
                 "one profile per intensity the natural home for 'how much arena is there', while " +
                 "the species assets keep owning what each plant IS. Rampage authors four.\n\n" +
                 "Pairs with PhaseThresholds and does NOT replace them: this scales the seed batch " +
                 "(the fill RATE and opening density), while the cell's Frenzy volume gate is what " +
                 "actually bounds the standing population. Move one without the other and the " +
                 "forest either tops out at the wrong size or takes the whole match to get there.")]
        [Min(0f)] public float FloraPopulationScale = 1f;

        [Tooltip("HOW BIG each plant may get, as a multiplier on the live-prism budget that " +
                 "survives the variant roll and any cell override. 1 = exactly as authored.\n\n" +
                 "A MULTIPLIER rather than an absolute because the three flora families ship very " +
                 "different budgets (assembled 400, branching/phyllotactic 1000+), so no single " +
                 "number could serve them all. Seeded prisms scale as the PRODUCT of this and " +
                 "FloraPopulationScale.")]
        [Min(0f)] public float FloraPlantBudgetScale = 1f;
        // The flora regrowth pulse (FloraRegrowthPulsePeriod / FloraRegrowthPulseDuration)
        // was removed: it was a hard-coded growth oscillator faking the "breathing" the
        // food web is meant to produce. Mass is conserved - growth resumes only when an
        // active force lowers the prism count below Frenzy. See Docs/ECOSYSTEM.md §0.
        public List<FloraConfigurationSO> SupportedFloras = new();
        
        [Header("FaunaPrefab Configs")]
        [Tooltip("DEPRECATED - inert. Fauna spawn in the cell's controlling color only (locked " +
                 "no-domain-asymmetry invariant); no spawner reads this. Kept only so legacy " +
                 "assets deserialize; remove with the next SpawnProfile asset migration.")]
        public bool FaunaExcludeLocalDomain = false;
        [Min(0f)] public float InitialFaunaSpawnWaitTime = 10f;

        [Tooltip("Fauna release tier this biome STARTS at - seeds Cell.FaunaReleaseTier when the " +
                 "config is assigned, before any spawner can tick. A species seeds only while its " +
                 "FaunaConfigurationSO.ReleaseTier is at or below the cell's tier. int.MaxValue " +
                 "(the default, and what every shipped biome uses) means 'everything released from " +
                 "the first tick'. PeelTheCage authors -1: its cage starts SEALED, and the mode raises " +
                 "the tier at 25% / 50%. Authoring it here rather than having the mode set it at " +
                 "runtime is deliberate - the spawner starts on the cell's own bootstrap clock, so " +
                 "a runtime-only seal races it and loses whenever the cell wins.")]
        public int InitialFaunaReleaseTier = int.MaxValue;
        [Min(0f)] public float FaunaSpawnVolumeThreshold = 1f;

        [Tooltip("HOW MANY creatures this cell carries, as a multiplier on every species' " +
                 "FaunaConfigurationSO seed counts AND its MaxLivePopulation cap. 1 = exactly as " +
                 "authored, and every shipped biome leaves it there.\n\n" +
                 "The fauna twin of FloraPopulationScale, and it exists for the same reason: a " +
                 "SpawnProfile forks per intensity for free under CellTypeChoiceOptions.IntensityWise, " +
                 "so one profile per intensity is the natural home for 'how much wildlife is there', " +
                 "while the species assets keep owning what each creature IS. Rampage rides it 1x/2x/" +
                 "3x/4x - and its two species are the SHARED Blob assets, which is exactly why the " +
                 "scalar has to live here and not on them.\n\n" +
                 "It scales the CAP as well as the floors on purpose: MaxLivePopulation is what " +
                 "actually bounds the standing population, so a scalar that moved only the seed " +
                 "counts would be clamped away above ~1.5x and read as doing nothing. Scaling " +
                 "PRODUCTION is all this does - nothing is ever culled to meet a lowered scale " +
                 "(Docs/ECOSYSTEM.md 0: no imposed death).")]
        [Min(0f)] public float FaunaPopulationScale = 1f;
        [Tooltip("Fixed period (seconds) between fauna spawn-cycle ticks - the ecosystem heartbeat. " +
                 "Platform default is 30s; scoring modes that ride the wave clock (Brood Rush) depend on it.")]
        [Min(0f)] public float BaseFaunaSpawnTime = 30f;
        [Tooltip("OFF (default): the tick is a SEEDER - it only tops each species up to its seed floor " +
                 "(PopulationSize), staying out while the food web sustains it. ON: every tick spawns a " +
                 "full fresh wave of PopulationSize fauna (clamped by MaxLivePopulation), so each cycle " +
                 "visibly births a brood in the controlling color - used by wave-scored modes (Brood Rush). " +
                 "Population is still bounded by starvation + the per-species cap; no imposed death.")]
        public bool SeedFullWaveEveryTick = false;
        [Tooltip("Population control (prey-linked), authored in NOMINAL PRISMS: a herbivore population " +
                 "only spawns while the cell holds at least this many prisms' worth of opposing " +
                 "ENVIRONMENT VOLUME (value × 16, the nominal leaf volume - volume is the spine; fauna " +
                 "bodies don't count, they aren't edible). Predator species read it directly as N live " +
                 "herbivores. Below the floor, production pauses until prey returns; existing fauna then " +
                 "starve. 0 = always produce. See Docs/ECOSYSTEM.md.")]
        [Min(0)] public int FaunaFoodFloor = 5;
        [Tooltip("Wait this many seconds after the crystal spawns before FaunaPrefab begins spawning.")]
        [Min(0f)] public float FaunaInitialDelaySeconds;
        [Tooltip("Seconds between each population spawn (within the initial FaunaPrefab batch). 0 = spawn all instantly.")]
        [Min(0f)] public float FaunaSpawnIntervalSeconds;
        [Tooltip("HERBIVORE spawn-point ring: this many points spaced evenly on a circle " +
                 "around the cell centre (equidistant from each other and from the centre). " +
                 "The ring rotates on the SPAWN CLOCK — each BaseFaunaSpawnTime tick steps to " +
                 "the next point, whether or not that tick actually hatched anything — so a " +
                 "3-point ring walks all three points in 3 × BaseFaunaSpawnTime and repeats, and " +
                 "every herbivore species hatching on the same tick shares that point. Each " +
                 "new group therefore gets its own feeding ground, and a head start before a " +
                 "territorial predator's patch reaches it. 0 or 1 = legacy behavior (spawn on " +
                 "the densest sensed mass). Predators are unaffected (they alternate points on " +
                 "their own ring, one per spawn).")]
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

        /// <summary>
        /// This cell's take on an authored fauna population number - a seed count
        /// (<c>InitialSpawnCount</c> / <c>PopulationSize</c>) or a cap
        /// (<c>MaxLivePopulation</c>) - scaled by <see cref="FaunaPopulationScale"/>.
        ///
        /// <para>Read it through <c>Cell.ResolveFaunaPopulation</c> rather than calling it
        /// directly: every producer (both spawners, reproduction, the freestyle conveyor)
        /// already holds the cell, and routing them all through one accessor is what stops
        /// the scalar from being live in one producer and dead in the next.</para>
        ///
        /// <para><b>0 passes through unchanged</b> so "uncapped" stays uncapped, and a
        /// non-positive scale is treated as 1 so an unauthored field can never empty a biome.
        /// Rounds half UP explicitly - <c>Mathf.RoundToInt</c> is banker's rounding, which
        /// would send an authored 5 x 1.5 one way and a 7 x 1.5 the other for no stated
        /// reason (the same rule the flora scalar follows).</para>
        /// </summary>
        public int ScaleFaunaPopulation(int authored)
        {
            if (authored <= 0) return authored;
            if (FaunaPopulationScale <= 0f || Mathf.Approximately(FaunaPopulationScale, 1f))
                return authored;

            return Mathf.Max(1, Mathf.FloorToInt(authored * FaunaPopulationScale + 0.5f));
        }

        /// <summary>
        /// This profile's take on an authored flora population number - a seed count
        /// (<c>InitialSpawnCount</c> / <c>PopulationSize</c>) or the hard cap
        /// (<c>MaxLivePopulation</c>) - scaled by <see cref="FloraPopulationScale"/>.
        ///
        /// <para>Read it through <c>Cell.ResolveFloraPopulation</c> rather than calling it
        /// directly: every flora producer (both spawners, reproduction, the freestyle
        /// conveyor) already holds the cell, and routing them all through one accessor is what
        /// stops the scalar from being live in one producer and dead in the next.</para>
        ///
        /// <para><b>It scales the CAP as well as the floors on purpose</b>, exactly as the
        /// fauna scalar does: <c>MaxLivePopulation</c> is what actually bounds a standing
        /// population, so a scalar that moved only the seed counts would be clamped away above
        /// ~1.5x and read as doing nothing. 0 passes through unchanged so "uncapped" stays
        /// uncapped, and a non-positive scale is treated as 1 so an unauthored field can never
        /// empty a biome. Rounds half UP explicitly - <c>Mathf.RoundToInt</c> is banker's
        /// rounding.</para>
        /// </summary>
        public int ScaleFloraPopulation(int authored)
        {
            if (authored <= 0) return authored;
            if (FloraPopulationScale <= 0f || Mathf.Approximately(FloraPopulationScale, 1f))
                return authored;

            return Mathf.Max(1, Mathf.FloorToInt(authored * FloraPopulationScale + 0.5f));
        }
    }
}