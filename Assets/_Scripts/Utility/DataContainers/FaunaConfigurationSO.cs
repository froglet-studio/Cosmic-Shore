using CosmicShore.Data;
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
        [Tooltip("SEED FLOOR for this species: the periodic spawner only tops the live population " +
                 "back up to this count (bootstrap + extinction recovery). Above the floor the " +
                 "population is driven by REPRODUCTION and bounded by starvation/predation - the " +
                 "food web, not the spawner. See Docs/ECOSYSTEM.md §6.")]
        [Min(1)] public int PopulationSize = 4;
        public float SpawnProbability;

        [Header("Reproduction (the population driver - see Docs/ECOSYSTEM.md §6)")]
        [Tooltip("Feeds (prism consumes; kills for predators) an individual needs per birth. " +
                 "0 = this species does not reproduce; the seed floor is then its only source. " +
                 "Lower = prey converts to population faster (steeper Lotka-Volterra upswing).")]
        [Min(0)] public int FeedsPerOffspring = 0;
        [Tooltip("Offspring spawned per birth.")]
        [Min(1)] public int OffspringPerBirth = 1;
        [Tooltip("Minimum seconds between births per individual. Throttles bursts when an " +
                 "individual is gorging on a dense buildup.")]
        [Min(0f)] public float ReproductionCooldownSeconds = 10f;
        [Tooltip("Hard per-cell cap on this species' live population. A PERFORMANCE backstop, " +
                 "not the primary control (starvation is) - size it to what the frame budget " +
                 "tolerates, not to where you want the population to sit. 0 = uncapped.")]
        [Min(0)] public int MaxLivePopulation = 0;

        [Header("Elemental contract (element x level - one base prefab, 20 data-defined variants)")]
        [Tooltip("The element this species config spawns as. None = keep the prefab-authored " +
                 "crystal element (the legacy per-element prefab-variant path). Setting it lets " +
                 "ONE base prefab serve all four element variants: the heart crystal is " +
                 "provisioned from ElementalCrystalSet at lineage assignment.")]
        public Element Element = Element.None;

        [Tooltip("Level (1..5) this species spawns at. Level scales body + crystal below; it can " +
                 "rise in-world (e.g. an own-domain Crystal Joust levels the creature up).")]
        [Range(1, 5)] public int InitialLevel = 1;

        [Tooltip("Uniform body scale multiplier per level above 1 (level 5 = this^4).")]
        [Min(1f)] public float BodyScalePerLevel = 1.15f;

        [Tooltip("Crystal WORLD scale multiplier per level above 1 - a higher-level lifeform " +
                 "drops a bigger elemental powerup on death (mass rewarded, still conserved).")]
        [Min(1f)] public float CrystalScalePerLevel = 1.2f;

        [Tooltip("Seconds a level-up growth animates over (continuity - a level-up blooms, " +
                 "never pops). Spawn-time seeding applies instantly (it spawns AT size).")]
        [Min(0.05f)] public float LevelGrowSeconds = 1f;
    }
}
