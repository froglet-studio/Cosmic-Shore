using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The microscene conveyor toy — fly through it to start a belt of shuffled, randomized
    /// microscenes (prism arrangements, elemental-crystal pickups, flora/fauna released into the
    /// cell) blooming in ahead of your flight path. When the pool is populated, the belt recycles
    /// the scene farthest behind into a fresh arrangement ahead — transport of the same conserved
    /// mass (suction out → bloom in), never destruction, with shuffle-bag recipe + domain +
    /// rotation variation so the loop doesn't read as a loop. Toy-faithful: no score, no end
    /// condition, no timers — the belt advances only with the player's own motion.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_Conveyor", menuName = "ScriptableObjects/Toys/Conveyor Toy")]
    public class ConveyorToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Conveyor — content")]
        [SerializeField, Tooltip("Prism prefab laid in the scene arrangements (a plain environment prism, " +
                                 "e.g. SpawnablePrism). Environment-owned conserved mass: fauna can graze it, " +
                                 "abilities can break it, the belt only transports it.")]
        Prism prismPrefab;

        [SerializeField, Tooltip("Optional crystal-side collection effects granted when the vessel skims a " +
                                 "scene's elemental crystal (e.g. SkimmerAdjustElementLevelByCrystalEffect). " +
                                 "The skimmer's own authored crystal effects fire regardless.")]
        SkimmerCrystalEffectSO[] crystalCollectionEffects;

        [SerializeField, Min(0), Tooltip("Most elemental-crystal pickups a single scene can hold.")]
        int maxCrystalsPerScene = 3;

        [SerializeField, Tooltip("Include the living recipes (Meadow flora / Menagerie fauna, released into " +
                                 "the host cell as ordinary citizens). Ignored gracefully when the scene has " +
                                 "no live Cell — those scenes then carry prisms + crystals only.")]
        bool lifeformScenes = true;

        [Header("Conveyor — belt")]
        [SerializeField, Min(2), Tooltip("Scenes in the pool. The belt creates this many, then recycles — " +
                                         "this bounds the toy's total mass and collider footprint " +
                                         "(poolSize × prismBudget prism colliders). Keep a few above " +
                                         "Ahead Target Scenes so passed scenes have slack before reclaim.")]
        int poolSize = 10;

        [SerializeField, Min(6), Tooltip("Prisms per scene. Every recipe is fitted to exactly this count so " +
                                         "recycled scenes can re-pose the same prism stock into any arrangement.")]
        int prismBudgetPerScene = 42;

        [SerializeField, Min(20f), Tooltip("Lateral radius of one scene, world units.")]
        float sceneRadius = 55f;

        [SerializeField, Min(50f), Tooltip("Minimum spacing between consecutive scene anchors. At speed the " +
                                           "effective spacing stretches to speed × Min Scene Interval.")]
        float sceneSpacing = 220f;

        [SerializeField, Min(30f), Tooltip("Minimum distance ahead of the vessel for the first scene on " +
                                           "activation (stretches with speed so it isn't instantly passed).")]
        float firstSceneDistance = 170f;

        [SerializeField, Range(3, 10), Tooltip("The field of structures the belt keeps ahead of you at all " +
                                               "times, in scenes. Lookahead distance = this × effective spacing.")]
        int aheadTargetScenes = 7;

        [SerializeField, Min(0.5f), Tooltip("Seconds of flight between scenes at speed — the faster you fly, " +
                                            "the wider scenes space out so the stream stays readable. " +
                                            "Effective spacing = max(Scene Spacing, speed × this).")]
        float minSceneIntervalSeconds = 2f;

        [SerializeField, Min(100f), Tooltip("Minimum distance behind you before a passed scene clears " +
                                            "(suctions up for the belt head). Stretches with speed.")]
        float recycleBehindDistance = 250f;

        [SerializeField, Min(0.2f), Tooltip("Seconds for each half of the recycle transport (suction out, " +
                                            "bloom back in) — the visible continuity-law transition. Also bounds " +
                                            "belt throughput: a full recycle holds its slot for 2× this.")]
        float transitionSeconds = 1.2f;

        [SerializeField, Range(0f, 1f), Tooltip("How strongly the belt bends toward the player's current " +
                                                "course when extending (0 = straight line, 1 = shadow the player).")]
        float courseFollow = 0.6f;

        [SerializeField, Tooltip("Deterministic seed for recipes/variation. 0 = fresh ride every session.")]
        int seed;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<ConveyorToy>();
            toy.Configure(BuildConfig());
            toy.Initialize(this, context, placement);
        }

        ConveyorConfig BuildConfig() => new()
        {
            PrismPrefab = prismPrefab,
            CrystalEffects = crystalCollectionEffects,
            PoolSize = poolSize,
            PrismBudget = prismBudgetPerScene,
            SceneRadius = sceneRadius,
            SceneSpacing = sceneSpacing,
            FirstSceneDistance = firstSceneDistance,
            AheadTargetScenes = aheadTargetScenes,
            MinSceneIntervalSeconds = minSceneIntervalSeconds,
            RecycleBehindDistance = recycleBehindDistance,
            TransitionSeconds = transitionSeconds,
            CourseFollow = courseFollow,
            MaxCrystalsPerScene = maxCrystalsPerScene,
            LifeformScenes = lifeformScenes,
            Seed = seed,
        };

        /// <summary>Wires a prism prefab on a runtime-synthesised definition (the zero-config default toybox).</summary>
        internal void SetRuntimePrismPrefab(Prism prefab) => prismPrefab = prefab;
    }
}
