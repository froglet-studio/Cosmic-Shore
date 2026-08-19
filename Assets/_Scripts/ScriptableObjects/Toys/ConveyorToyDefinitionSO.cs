using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The microscene conveyor toy - fly through it to start a belt of shuffled, randomized
    /// microscenes (prism arrangements, elemental-crystal pickups, flora/fauna released into the
    /// cell) blooming in ahead of your flight path. When the pool is populated, the belt recycles
    /// the scene farthest behind into a fresh arrangement ahead - transport of the same conserved
    /// mass (suction out → bloom in), never destruction, with shuffle-bag recipe + domain +
    /// rotation variation so the loop doesn't read as a loop. Toy-faithful: no score, no end
    /// condition, no timers - the belt advances only with the player's own motion.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_Conveyor", menuName = "ScriptableObjects/Toys/Conveyor Toy")]
    public class ConveyorToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Conveyor - content")]
        [SerializeField, Tooltip("Prism prefab laid in the scene arrangements (a plain environment prism, " +
                                 "e.g. SpawnablePrism). Environment-owned conserved mass: fauna can graze it, " +
                                 "abilities can break it, the belt only transports it.")]
        Prism prismPrefab;

        [SerializeField, Tooltip("Optional crystal-side collection effects granted when the vessel skims a " +
                                 "scene's elemental crystal (e.g. SkimmerAdjustElementLevelByCrystalEffect). " +
                                 "The skimmer's own authored crystal effects fire regardless.")]
        SkimmerCrystalEffectSO[] crystalCollectionEffects;

        [SerializeField, Tooltip("Omni crystal prefab - the body-collected jackpot (fuel + speed buff, " +
                                 "any domain), the richer/rarer reward in the mix. Assets/_Prefabs/" +
                                 "Environment/Crystal.prefab. Optional: unwired → omni slots fall back to " +
                                 "elemental pickups.")]
        Crystal omniCrystalPrefab;

        [SerializeField, Min(0), Tooltip("Most crystal pickups a single scene can hold.")]
        int maxCrystalsPerScene = 6;

        [SerializeField, Tooltip("Theming palette - structural domain schemes over the full playable " +
                                 "triad (per-structure rainbows, gradients, pinwheels, stripes, mirrors, " +
                                 "neutral-Blue veins), prism-kind schemes (danger gates/tips, armoured " +
                                 "shield frames, keystone landmarks - shield counts capped for the " +
                                 "collider budget), scale moods (uniform / long-axis stretch / taper), " +
                                 "and the elemental/omni crystal mix. Painted per structure, never " +
                                 "per-prism confetti.")]
        MicroscenePalette palette = new();

        [SerializeField, Tooltip("Include the living recipes (Meadow flora / Menagerie fauna, released into " +
                                 "the host cell as ordinary citizens). Ignored gracefully when the scene has " +
                                 "no live Cell - those scenes then carry prisms + crystals only.")]
        bool lifeformScenes = true;

        [Header("Conveyor - belt")]
        [SerializeField, Min(2), Tooltip("Scenes in the pool. THE BELT'S WHOLE CONSERVED STOCK IS " +
                                         "poolSize × prismBudgetPerScene — it is built once, up front, " +
                                         "behind a load veil, and transported forever after (the belt " +
                                         "never instantiates again). 20 × 1500 = 30,000 prisms, the same " +
                                         "order as an authored cell environment. Keep a few above Ahead " +
                                         "Target Scenes so passed scenes have slack before reclaim.")]
        int poolSize = 20;

        [SerializeField, Min(6), Tooltip("Prisms per scene. Every recipe is fitted to exactly this count so " +
                                         "recycled scenes can re-pose the same prism stock into any " +
                                         "arrangement. At or above MicroscenePatterns.GrandBudgetThreshold " +
                                         "the monument-scale assemblies (Cathedral, World Tree, Orrery, " +
                                         "Sunken City, Leviathan, Geode Vault, Aurora Veil, Hypersphere) " +
                                         "join the shuffle; below it the belt stays on the classic recipes.")]
        int prismBudgetPerScene = 1500;

        [SerializeField, Min(20f), Tooltip("Lateral radius of one scene, world units. The classic recipes " +
                                           "are authored at 80 and scaled bodily to this; the grand " +
                                           "assemblies use it as their own basis. A scene runs ~2.2 × this " +
                                           "along the flight axis, so Scene Spacing must exceed that.")]
        float sceneRadius = 180f;

        [SerializeField, Min(50f), Tooltip("Minimum spacing between consecutive scene anchors. Must exceed " +
                                           "~2.2 × Scene Radius or grand assemblies interpenetrate. At speed " +
                                           "the effective spacing stretches to speed × Min Scene Interval.")]
        float sceneSpacing = 520f;

        [SerializeField, Min(30f), Tooltip("Minimum distance ahead of the vessel for the first scene on " +
                                           "activation (stretches with speed so it isn't instantly passed).")]
        float firstSceneDistance = 460f;

        [SerializeField, Range(3, 10), Tooltip("The field of structures the belt keeps ahead of you at all " +
                                               "times, in scenes. Lookahead distance = this × effective spacing.")]
        int aheadTargetScenes = 5;

        [SerializeField, Min(0.5f), Tooltip("Seconds of flight between scenes at speed - the faster you fly, " +
                                            "the wider scenes space out so the stream stays readable. " +
                                            "Effective spacing = max(Scene Spacing, speed × this).")]
        float minSceneIntervalSeconds = 2f;

        [SerializeField, Min(100f), Tooltip("Minimum distance behind you before a passed scene clears " +
                                            "(suctions up for the belt head). Stretches with speed.")]
        float recycleBehindDistance = 520f;

        [SerializeField, Min(0.2f), Tooltip("Seconds for each half of the recycle transport (suction out, " +
                                            "bloom back in) - the visible continuity-law transition. Also bounds " +
                                            "belt throughput: a full recycle holds its slot for 2× this.")]
        float transitionSeconds = 1.2f;

        [SerializeField, Range(20f, 80f),
         Tooltip("How sharp a turn re-lays the ribbon straight ahead, in degrees. Scenes farther " +
                 "off your live heading than this stop counting as 'ahead', so the belt keeps a " +
                 "connected ribbon that BENDS with gentler turns but BREAKS and re-lays directly in " +
                 "front of you once you turn past this angle. Lower = the ribbon snaps to your new " +
                 "heading sooner; higher = it follows longer curves before re-laying.")]
        float turnBreakDegrees = 55f;

        [SerializeField, Tooltip("Deterministic seed for recipes/variation. 0 = fresh ride every session.")]
        int seed;

        [Header("Conveyor - the run (Wanderway as its own mode)")]
        [SerializeField, Tooltip("Starting a wander hands the host cell its BARE CANVAS config - the " +
                                 "one that grows nothing (no EnvironmentPrefab AND no flora or fauna " +
                                 "in its SpawnProfile, e.g. Barren) - through Cell.RequestCellSwap, " +
                                 "the same explicit, player-initiated world change the Cell Selector " +
                                 "performs. The wander is the thing you look at, not an authored world " +
                                 "you fly through. Requires the cell's config list to include such an " +
                                 "entry; with none it falls back to the cheapest environment-free " +
                                 "config, and with none of those it warns and leaves the world alone.")]
        bool revertCellOnStart = true;

        [SerializeField, Min(1), Tooltip("Length of the ROLLING tether, in prisms. The trail follows " +
                                         "you as a ribbon of exactly this many: as you lay at the head, " +
                                         "the oldest prism withers and recycles back into the pool, so " +
                                         "the wander is endless at fixed memory. The return station rides " +
                                         "the tail, so the way home is always this far behind you. " +
                                         "This is a per-ribbon LENGTH: a vessel that lays a double " +
                                         "trail holds 2× the prisms for the same visible tether. " +
                                         "AUTHORIZED EXCEPTION to mass conservation, scoped to a live " +
                                         "WanderwayRun - see Docs/ECOSYSTEM.md §0.")]
        int tetherPrisms = 100;

        [SerializeField, Min(4f), Tooltip("Body radius of the return station at the end of the tether. " +
                                          "It has to read at wander distances, so keep it well above the " +
                                          "toybox toys' scale.")]
        float returnStationRadius = 22f;

        [SerializeField, Tooltip("Accent for the return station - deliberately distinct from the toy's own " +
                                 "accent so the way home never reads as another wander station.")]
        Color returnStationColor = new(1f, 0.78f, 0.25f, 1f);

        [Header("Conveyor - visibility guards")]
        [SerializeField, Min(0f),
         Tooltip("Hard floor on how close a scene may bloom in, world units from the vessel. The " +
                 "belt already targets far ahead; this guarantees a structure never materialises in " +
                 "the player's face even under degenerate geometry. Keep at or below First Scene " +
                 "Distance so it never fights normal near-fill placement.")]
        float minPlacementDistance = 380f;

        [SerializeField, Min(0f),
         Tooltip("Extra world-unit margin added to Scene Radius when deciding a scene is fully off " +
                 "screen before it may be recycled (suctioned away). A scene is only reclaimed once " +
                 "this padded sphere lies wholly outside the camera view, so the player never watches " +
                 "a scene vanish. Larger = more buffer against turning mid-transition; the belt just " +
                 "waits a touch longer for scenes to leave view.")]
        float offscreenMargin = 80f;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<ConveyorToy>();
            toy.Configure(BuildConfig());
            toy.Initialize(this, context, placement);
        }

        ConveyorConfig BuildConfig() => new()
        {
            DisplayName = DisplayName,
            PrismPrefab = prismPrefab,
            OmniCrystalPrefab = omniCrystalPrefab,
            CrystalEffects = crystalCollectionEffects,
            Palette = palette ?? new MicroscenePalette(),
            PoolSize = poolSize,
            PrismBudget = prismBudgetPerScene,
            SceneRadius = sceneRadius,
            SceneSpacing = sceneSpacing,
            FirstSceneDistance = firstSceneDistance,
            AheadTargetScenes = aheadTargetScenes,
            MinSceneIntervalSeconds = minSceneIntervalSeconds,
            RecycleBehindDistance = recycleBehindDistance,
            TransitionSeconds = transitionSeconds,
            TurnBreakDegrees = turnBreakDegrees,
            MaxCrystalsPerScene = maxCrystalsPerScene,
            LifeformScenes = lifeformScenes,
            Seed = seed,
            MinPlacementDistance = minPlacementDistance,
            OffscreenMargin = offscreenMargin,
            RevertCellOnStart = revertCellOnStart,
            TetherPrisms = tetherPrisms,
            ReturnStationRadius = returnStationRadius,
            ReturnStationColor = returnStationColor,
        };

        /// <summary>Wires a prism prefab on a runtime-synthesised definition (the zero-config default toybox).</summary>
        internal void SetRuntimePrismPrefab(Prism prefab) => prismPrefab = prefab;
    }
}
