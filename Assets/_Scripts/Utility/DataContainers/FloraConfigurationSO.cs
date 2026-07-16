using CosmicShore.Data;
using CosmicShore.Gameplay;
using Unity.Entities.UI;
using UnityEngine;

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
