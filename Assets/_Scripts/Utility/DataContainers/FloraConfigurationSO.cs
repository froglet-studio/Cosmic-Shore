using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using Unity.Entities.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CosmicShore.Utility
{
    [CreateAssetMenu(
        fileName = "Flora Configuration",
        menuName = "ScriptableObjects/DataContainers/" + nameof(FloraConfigurationSO))]
    public class FloraConfigurationSO : ScriptableObject
    {
        public Flora FloraPrefab;
        [MinMax(0f, 1f)]
        public float SpawnProbability;
        public int InitialSpawnCount;
        public bool OverrideDefaultPlantPeriod;
        public int NewPlantPeriod = int.MaxValue;

        [Header("Population (the flora twin of the fauna pipeline - Docs/ECOSYSTEM.md §32)")]
        [Tooltip("SEED FLOOR for this species: the periodic spawner only tops the live plant " +
                 "count back up to this (bootstrap + extinction recovery). Above the floor the " +
                 "population is driven by the plants' own REPRODUCTION and bounded by grazing - " +
                 "the food web, not the spawner.\n\n" +
                 "0 (the default) keeps the LEGACY behaviour: the spawner plants one every plant " +
                 "period forever, bounded only by the cell's Frenzy gate. Authoring it is how a " +
                 "species opts into the population model.")]
        [Min(0)] public int PopulationSize = 0;

        [Tooltip("Hard per-cell cap on this species' live PLANT count. A PERFORMANCE backstop - " +
                 "each plant is a lifeform, so it carries a heart crystal whose collider is " +
                 "always on (Docs/ECOSYSTEM.md §21.6). Size it to the collider budget, not to " +
                 "where you want the forest to sit; grazing is the real control. 0 = uncapped.\n\n" +
                 "Scaled together with the seed floor by SpawnProfileSO.FloraPopulationScale - a " +
                 "scalar that moved only the floor would be clamped away by the cap and read as " +
                 "doing nothing.")]
        [Min(0)] public int MaxLivePopulation = 0;

        [Header("Reproduction - a plant funds its own offspring out of GROWTH")]
        [Tooltip("Prisms this plant must GROW per offspring. Growth is a plant's feeding: it " +
                 "converts the space it managed to grow into into population, exactly as a " +
                 "creature converts prey. 0 = this species does not reproduce; the seed floor is " +
                 "then its only source.\n\n" +
                 "Set it at (or near) the plant's own live-prism budget and the species reads as " +
                 "a colonizer: a plant completes itself, seeds one neighbour, and only funds " +
                 "another after grazing forces it to regrow. Lower converts growth to population " +
                 "faster.")]
        [Min(0)] public int GrowthPerOffspring = 0;

        [Tooltip("Offspring seeded per birth.")]
        [Min(1)] public int OffspringPerBirth = 1;

        [Tooltip("Minimum seconds between births per plant. Throttles a burst when a heavily " +
                 "grazed plant regrows quickly.")]
        [Min(0f)] public float ReproductionCooldownSeconds = 5f;

        [Tooltip("Fraction of its OWN live-prism budget a plant must currently hold before it " +
                 "may seed. Guards the case a pure growth quota cannot see: a plant grazed to a " +
                 "stub has still ACCUMULATED its quota across several regrow cycles, and a " +
                 "near-dead plant seeding a child reads wrong. 0 (default) = no maturity gate.")]
        [Range(0f, 1f)] public float MaturityFraction = 0f;

        [Tooltip("How far from its parent an offspring is planted, in world units, for species " +
                 "whose growth rule has no opinion about where the next plant goes.\n\n" +
                 "A LATTICE species ignores this: AssembledFlora hands the daughter the exact " +
                 "bond site its own growth would have filled next, which is what makes many " +
                 "small plants add up to one continuous gyroid (Docs/ECOSYSTEM.md §32.2).")]
        [Min(0f)] public float OffspringSpread = 60f;

        [Tooltip("Ground this species prefers when the cell's authored environment prepared " +
                 "planting sites (a garden's beds, trellis feet, hanging baskets, pool rim). " +
                 "A preference, not a requirement: a garden with none of the preferred kinds " +
                 "falls back to any prepared site, and a cell with no prepared ground at all " +
                 "disperses the flora across the membrane exactly as before. Leave as Any " +
                 "for a species with no opinion.")]
        public FloraSiteKind PreferredSites = FloraSiteKind.Any;

        [Header("Elemental contract (element as data - one base prefab, variants from config)")]
        [Tooltip("The element this flora config spawns as. None = keep the prefab-authored " +
                 "crystal element (the legacy per-element prefab-variant path, e.g. the four " +
                 "GyroidFlora prefabs). Setting it provisions the crystal from " +
                 "ElementalCrystalSet at spawn, before Initialize.")]
        public Element Element = Element.None;

        [Tooltip("Per-variant expression applied on top of the base prefab at spawn - the " +
                 "fields that differ between the authored Charge/Mass/Space/Time GyroidFlora " +
                 "prefabs. Leave Enabled off to keep the prefab as authored.")]
        public FloraVariantTuning Variant = new();

        [Tooltip("Level (1..5) this flora spawns at - scales the leaf prisms and the crystal " +
                 "below (level 5 always carries, and drops, the largest crystal).")]
        [Range(1, 5)] public int InitialLevel = 1;

        [Tooltip("Leaf prism scale multiplier per level above 1.")]
        [Min(1f)] public float LeafScalePerLevel = 1.15f;

        [Tooltip("Crystal scale multiplier per level above 1 - the death-drop powerup grows " +
                 "with level (mass rewarded, still conserved).")]
        [Min(1f)] public float CrystalScalePerLevel = 1.2f;

        [Header("Variant spread - one config spans the element x level matrix")]
        [Tooltip("Roll the ELEMENT per spawn instead of planting this config's single element. " +
                 "The element's identity (leaf prism shape, growth tempo, shield cadence, prism " +
                 "budget) comes from the palette below, so a rolled element is expressed as " +
                 "authored - not just a recoloured crystal. Empty palette = roll the element " +
                 "alone and keep this config's own Variant tuning.")]
        public bool SpreadElements = false;

        [Tooltip("Per-element sibling configs that define each element's identity (normally the " +
                 "four canonical assets in _SO_Assets/Lifeforms for this species). Only Element " +
                 "and Variant are read from them - planting counts, periods and probability stay " +
                 "on THIS config, so the cell keeps its own density tuning.")]
        public List<FloraConfigurationSO> ElementPalette = new();

        [Tooltip("Spawn across a band of LEVELS instead of always InitialLevel. Level is a pure " +
                 "scale curve (leaves + dropped crystal), so this costs no extra colliders.")]
        public LifeformLevelSpread Levels = new();

        [Header("Cell-level overrides - applied AFTER the rolled variant, so they survive SpreadElements")]
        [Tooltip("OUTER edge of the planting band this cell wants for this species, as a fraction " +
                 "of the membrane radius, overriding whatever the rolled variant carries. -1 = off.\n\n" +
                 "This exists because planting radius is a CELL fact, not an element fact - " +
                 "'Space coral grows 20x1x1 needles' is an identity, 'coral fills this arena from " +
                 "the nucleus out to 0.95 R' is a layout decision - yet both live in the same " +
                 "Variant block, and with SpreadElements on the palette sibling's whole Variant " +
                 "replaces this config's. Without these fields a cell cannot use the canonical " +
                 "per-element assets AND choose where they grow.")]
        [Min(-1f)] public float PlantRadiusCellFractionMaxOverride = -1f;

        [Tooltip("INNER edge of the planting band, as a fraction of the membrane radius. 0 " +
                 "collapses the band to a single shell at the outer fraction. -1 = off. The " +
                 "runtime clamps it outside the cell's nucleus regardless, so authoring 0 here " +
                 "means 'from the nucleus outward', not 'from the cell centre'.")]
        [Min(-1f)] public float PlantRadiusCellFractionMinOverride = -1f;

        [Tooltip("HOW BIG one plant of this species may get in this cell (live-prism budget), " +
                 "overriding the rolled variant. -1 = off. The cell-level half of the same split: " +
                 "an arena sizes its plants to its own phase ladder and collider budget, and it " +
                 "must be able to do that without forking the species' element assets.")]
        [Min(-1)] public int MaxTotalSpawnedObjectsOverride = -1;

        /// <summary>
        /// The cell-level override block, expressed as an ordinary <see cref="FloraVariantTuning"/>
        /// so it applies through the one existing path (<see cref="Flora.ApplyVariantTuning"/>) with
        /// its established "sentinel = keep what you have" semantics - no second application
        /// mechanism, and it reaches every flora family for free. Applied AFTER the rolled variant,
        /// so it wins over both the palette sibling and the prefab.
        /// </summary>
        public bool TryBuildCellOverrideTuning(out FloraVariantTuning tuning)
        {
            tuning = null;
            if (PlantRadiusCellFractionMaxOverride < 0f &&
                PlantRadiusCellFractionMinOverride < 0f &&
                MaxTotalSpawnedObjectsOverride < 0)
                return false;

            tuning = new FloraVariantTuning
            {
                Enabled = true,
                LeafSize = Vector3.zero,          // sentinel: keep
                GrowPeriod = -1f,                 // sentinel: keep
                ShieldPeriod = -1f,               // sentinel: keep
                WitherRingInterval = -1f,         // sentinel: keep
                MaxTotalSpawnedObjects = MaxTotalSpawnedObjectsOverride,
                MaxTotalSpawnedObjectsScale = -1f,   // sentinel: keep (the SpawnProfile writes it)
                PlantRadiusCellFraction = PlantRadiusCellFractionMaxOverride,
                PlantRadiusCellFractionMin = PlantRadiusCellFractionMinOverride,
            };
            return true;
        }

        /// <summary>
        /// The cell-level override block a plant should be spawned with, with the owning cell's
        /// <see cref="SpawnProfileSO.FloraPlantBudgetScale"/> folded in. Returns false when there
        /// is nothing to apply.
        ///
        /// The density scalar rides the SAME application path as the per-species overrides
        /// (<see cref="Flora.ApplyVariantTuning"/>, sentinel = keep) rather than getting a second
        /// mechanism of its own - so it reaches every flora family for free and cannot drift from
        /// the overrides it composes with.
        /// </summary>
        public bool TryBuildCellOverrideTuning(float plantBudgetScale, out FloraVariantTuning tuning)
        {
            bool scales = plantBudgetScale > 0f && !Mathf.Approximately(plantBudgetScale, 1f);
            bool hasOverrides = TryBuildCellOverrideTuning(out tuning);

            if (!scales) return hasOverrides;

            tuning ??= new FloraVariantTuning
            {
                Enabled = true,
                LeafSize = Vector3.zero,
                GrowPeriod = -1f,
                ShieldPeriod = -1f,
                WitherRingInterval = -1f,
                MaxTotalSpawnedObjects = -1,
                PlantRadiusCellFraction = -1f,
                PlantRadiusCellFractionMin = -1f,
            };
            tuning.MaxTotalSpawnedObjectsScale = plantBudgetScale;
            return true;
        }

        /// <summary>
        /// What a single plant of this species is: element + the variant block expressing it +
        /// the level it seeds at. Pass <paramref name="inherit"/> to keep an existing lineage's
        /// identity (a re-plant of the same flora) instead of rolling a fresh one.
        /// </summary>
        public LifeformVariantPick<FloraVariantTuning> RollVariant(
            LifeformVariantPick<FloraVariantTuning>? inherit = null)
        {
            if (inherit.HasValue) return inherit.Value;

            var element = Element;
            var tuning = Variant;

            if (SpreadElements)
            {
                var sibling = RollPaletteSibling();
                if (sibling)
                {
                    element = sibling.Element;
                    tuning = sibling.Variant;
                }
                else
                {
                    // No palette authored: still spread the element, expressed with this
                    // config's own Variant tuning.
                    element = CosmicShore.ScriptableObjects.ElementalCrystalSetSO.RandomElement();
                }
            }

            return new LifeformVariantPick<FloraVariantTuning>(element, tuning, Levels.Roll(InitialLevel));
        }

        FloraConfigurationSO RollPaletteSibling()
        {
            if (ElementPalette is not { Count: > 0 }) return null;

            // Uniform over the authored palette - richness comes from what a biome authors into
            // it, not from a weight table nobody tunes.
            for (int attempt = 0; attempt < ElementPalette.Count; attempt++)
            {
                var candidate = ElementPalette[Random.Range(0, ElementPalette.Count)];
                if (candidate && candidate.Element != CosmicShore.Data.Element.None) return candidate;
            }
            return null;
        }
    }

    /// <summary>
    /// The data that differs between per-element prefab VARIANTS of the same flora species,
    /// hoisted into config so ONE base prefab serves all of them. Sentinels keep the prefab's
    /// authored value: floats/ints -1, vectors zero. Captured from the real
    /// Charge/Mass/Space/Time GyroidFlora diff: leaf PRISM size (9x3.4x1.5 / 7x4.5x3.5 /
    /// 20x1x1 / 9x3.4x1.5), grow period (0.5 / 0.3 / 0.8 / 0.15), shield period
    /// (1 / 0 / 0 / 0), live-prism budget (1000 / 1500 / 800 / 1000), plant radius fraction.
    /// </summary>
    [System.Serializable]
    public class FloraVariantTuning
    {
        [Tooltip("Master switch - off means this block changes nothing (legacy prefab-variant path).")]
        public bool Enabled = false;

        [Tooltip("Per-leaf PRISM target scale - the per-element leaf shape (the Space gyroid " +
                 "grows 20x1x1 needles, Mass 7x4.5x3.5 slabs). Zero = keep the prefab's size.")]
        public Vector3 LeafSize = Vector3.zero;

        [Tooltip("Seconds between growth steps - the element's tempo (Time gyroid: 0.15, " +
                 "Space: 0.8). -1 = keep prefab.")]
        public float GrowPeriod = -1f;

        [Tooltip("LATTICE species only: scales this element's whole lattice - every distance " +
                 "between prisms, and so between their spindles - while keeping the plant's " +
                 "TOPOLOGY and PRISM COUNT identical to its elemental peers. Only the spacings " +
                 "change, so a longer prism gains room without the plant becoming a different " +
                 "plant. The Space element uses it to be skeletal: a long thin strut on a lattice " +
                 "widened to match. -1 = keep prefab. See Docs/ECOSYSTEM.md 33.7.")]
        public float LatticeScale = -1f;

        [Tooltip("Seconds between shield refreshes on the health prisms (the Charge gyroid " +
                 "ships shielded leaves at 1). -1 = keep prefab.")]
        public float ShieldPeriod = -1f;

        [Tooltip("Seconds between spindle rings on a JOUSTED death, where the structure unravels " +
                 "from the heart outward instead of detonating (Docs/ECOSYSTEM.md §26). A denser " +
                 "plant wants a shorter ring so the whole wither still reads at flight speed. " +
                 "-1 = keep prefab.")]
        public float WitherRingInterval = -1f;

        [Tooltip("Live-prism budget - the cap on how many prisms ONE plant of this species may " +
                 "hold at once (consumption frees budget, so a grazed plant regrows toward it). " +
                 "Honoured by every flora family: assembled (Mass gyroid 1500, Space 800), " +
                 "branching and phyllotactic. -1 = keep prefab.")]
        public int MaxTotalSpawnedObjects = -1;

        [Tooltip("Multiplier applied to the live-prism budget AFTER MaxTotalSpawnedObjects, so it " +
                 "scales whatever budget survived - the prefab's, the element's, or the cell's " +
                 "absolute override. Written by the cell's SpawnProfileSO.FloraPlantBudgetScale, " +
                 "never authored per species. -1 (the sentinel) = keep; 0 is treated as keep too, " +
                 "because a nested serialized class can zero-initialize and 'budget 0' must never " +
                 "be something an absent key can mean.")]
        public float MaxTotalSpawnedObjectsScale = -1f;

        [Tooltip("Growth decisions per grow tick (AssembledFlora.itemsPerGrow) - how many " +
                 "children a plant may decide in one tick. A pacing guard on the prefab; " +
                 "exposed here so a cell that WANTS a different pace can author it " +
                 "without re-pacing every other biome's plants. -1 = keep prefab.")]
        public int ItemsPerGrow = -1;

        [Tooltip("Random skips per grow tick (AssembledFlora.randomItems) - stochastic " +
                 "raggedness in the growth front. 0 = deterministic, every branch advances " +
                 "every tick. -1 = keep prefab.")]
        public int RandomItems = -1;

        [Tooltip("Seconds the youngest prism must have been alive before a LATTICE plant may " +
                 "reproduce - 'fully grown' includes the grow-in BLOOM, which takes seconds, " +
                 "while the frontier-idle test alone settles in fractions of one. This is what " +
                 "makes a colony read as fully-formed plants begetting fully-formed plants " +
                 "instead of generations cascading mid-bloom. -1 = the code default (4s).")]
        public float MaturationSeconds = -1f;

        [Tooltip("Grow-order instantiations executed per frame (AssembledFlora." +
                 "maxSpawnsPerFrame) - the frame-cost throttle on the decided-order drain. " +
                 "-1 = keep prefab.")]
        public int MaxSpawnsPerFrame = -1;

        [Tooltip("OUTER edge of the planting band, as a fraction of the cell membrane radius. " +
                 "-1 = keep prefab.")]
        public float PlantRadiusCellFraction = -1f;

        [Tooltip("INNER edge of the planting band, as a fraction of the cell membrane radius. " +
                 "0 collapses the band to a single shell at the outer fraction (legacy). " +
                 "-1 = keep prefab.")]
        public float PlantRadiusCellFractionMin = -1f;
    }
}
