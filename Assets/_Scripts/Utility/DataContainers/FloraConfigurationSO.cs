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

        [Tooltip("Seconds between shield refreshes on the health prisms (the Charge gyroid " +
                 "ships shielded leaves at 1). -1 = keep prefab.")]
        public float ShieldPeriod = -1f;

        [Tooltip("Live-prism budget for assembled flora (Mass gyroid 1500, Space 800). " +
                 "-1 = keep prefab.")]
        public int MaxTotalSpawnedObjects = -1;

        [Tooltip("Planting radius as a fraction of the cell membrane radius. -1 = keep prefab.")]
        public float PlantRadiusCellFraction = -1f;
    }
}
