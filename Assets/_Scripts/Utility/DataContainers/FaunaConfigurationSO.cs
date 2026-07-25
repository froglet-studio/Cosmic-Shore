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

        [Header("Deployment behavior")]
        [Tooltip("0-1: pulls this species' roaming goal toward the cell centre so it spends " +
                 "more time on the central canopy (the gyroids around the nucleus). Herbivores " +
                 "only — predators hunt prey, not places. Leave 0 (default) for deployments " +
                 "that must range far from centre (e.g. the Skim Race track-cleanup swarm).")]
        [Range(0f, 1f)] public float CenterFocusBias = 0f;

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

        [Tooltip("Per-variant expression applied on top of the base prefab at AssignLineage - " +
                 "everything that used to force a prefab variant per element (behavior numbers, " +
                 "body scale/material, audio loop). Leave Enabled off to keep the prefab as " +
                 "authored. Inventory source: the Mass/Space/Time tadpole prefab diff " +
                 "(Docs/ECOSYSTEM.md §3).")]
        public FaunaVariantTuning Variant = new();
    }

    /// <summary>
    /// The data that used to differ between per-element prefab VARIANTS of the same species,
    /// hoisted into config so ONE base prefab serves all of them. Sentinels keep the prefab's
    /// authored value: floats -1 (scale 0), object refs null, Forager = KeepPrefab. Captured
    /// from the real Mass/Space/Time tadpole diff: body scale (0.4/0.7/0.4), spindle material,
    /// starvation (90/30/30), cohesion radius (50/20/20), behavior tick (1.5/3/3), graze radius
    /// (45/15/15), goal weight (3/0.3/0.3), speed band (10-15/10-15/15-20), forager (on/off/off),
    /// FMOD loop (Mass Tadpole / none / Time Tadpole) + attenuation (0-200 / - / -).
    /// </summary>
    [System.Serializable]
    public class FaunaVariantTuning
    {
        public enum TriState { KeepPrefab = 0, On = 1, Off = 2 }

        [Tooltip("Master switch - off means this block changes nothing (legacy prefab-variant path).")]
        public bool Enabled = false;

        [Header("Body")]
        [Tooltip("Uniform root scale for this variant (the level-1 base the level curve grows " +
                 "from). 0 = keep the prefab's authored scale.")]
        [Min(0f)] public float BaseBodyScale = 0f;
        [Tooltip("Target scale of each body HealthPrism - the per-element PRISM shape (the " +
                 "Mass/Time tadpoles author 0.8 x 0.8 x 7 long tail prisms; Space keeps the " +
                 "spindle default). Zero = keep the prefab's authored prism scale.")]
        public Vector3 BodyPrismScale = Vector3.zero;
        [Tooltip("Material applied to the body's Spindle renderers (the per-element body look). " +
                 "None = keep the prefab's authored material.")]
        public Material BodyMaterial;

        [Header("Survival / feeding")]
        [Tooltip("Seconds without feeding before starvation. -1 = keep prefab.")]
        public float StarvationSeconds = -1f;
        [Tooltip("Forager creatures hunt the cell's densest sensed mass and can starve; " +
                 "KeepPrefab leaves the authored flag.")]
        public TriState Forager = TriState.KeepPrefab;

        [Header("Boid flocking (applies to Boid-based species; -1 = keep prefab)")]
        public float CohesionRadius = -1f;
        public float BehaviorUpdateRate = -1f;
        public float TrailBlockInteractionRadius = -1f;
        public float GoalWeight = -1f;
        public float MinSpeed = -1f;
        public float MaxSpeed = -1f;

        [Header("Audio")]
        [Tooltip("When on, the variant's loop event (below) replaces the prefab emitter's - an " +
                 "empty reference silences the loop (the Space tadpole ships silent).")]
        public bool OverrideAudio = false;
        public FMODUnity.EventReference AudioLoopEvent;
        [Tooltip("FMOD attenuation override distances; -1 leaves the emitter's authored values.")]
        public float AudioMinDistance = -1f;
        public float AudioMaxDistance = -1f;
    }
}
