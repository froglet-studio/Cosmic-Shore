using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The four elemental crystal prefabs (Charge / Mass / Space / Time). Every lifeform is
    /// guaranteed to drop an elemental-crystal powerup on death; the authored per-prefab crystal
    /// is the normal source, and this set is the fallback the runtime guard
    /// (<see cref="CosmicShore.Gameplay.LifeFormCrystal"/>) provisions from when a lifeform is
    /// misconfigured with no elemental crystal. Loaded from Resources/ElementalCrystalSet.
    /// </summary>
    [CreateAssetMenu(fileName = "ElementalCrystalSet", menuName = "ScriptableObjects/" + nameof(ElementalCrystalSetSO))]
    public class ElementalCrystalSetSO : ScriptableObject
    {
        public const string ResourcePath = "ElementalCrystalSet";

        [SerializeField] Crystal charge;
        [SerializeField] Crystal mass;
        [SerializeField] Crystal space;
        [SerializeField] Crystal time;

        [Tooltip("Collection effects wired onto runtime-provisioned crystals (the standalone " +
                 "prefabs above author no collection components). Used when the provisioning " +
                 "lifeform has no authored effects to inherit.")]
        [SerializeField] SkimmerCrystalEffectSO[] collectionEffects;

        /// <summary>Default skim-collection effects for runtime-provisioned crystals.</summary>
        public SkimmerCrystalEffectSO[] CollectionEffects => collectionEffects;

        [Header("Heart sizing — ONE curve for every species and every element")]
        [Tooltip("World scale a LEVEL 1 lifeform heart renders at. This is the single " +
                 "authority: every lifeform's heart is resized to this curve when it becomes a " +
                 "heart, so a tadpole's crystal, a shark's and a gyroid's are the same size at " +
                 "the same level. Species prefabs authored anything from 0.7 to 4 world scale, " +
                 "which made an identical kill worth 4x more on one species than another (the " +
                 "collect reward and the domain fauna buff both read the heart's world scale).")]
        [Min(0.01f)] [SerializeField] float levelOneWorldScale = 3.5f;

        [Tooltip("World scale multiplier per level above 1. Deliberately GENTLE: level is now " +
                 "EARNED (flora level on reproduction, fauna after a significant amount of " +
                 "feeding), so the band is a legibility cue rather than a jackpot. Keep " +
                 "levelOneWorldScale x this^4 under the collect effect's " +
                 "maxLevelGainPerCrystal / levelPerUnitScale (5.0 world scale at the shipped " +
                 "0.5 / 0.1), or a level-5 heart is clipped by the cap and levelling stops " +
                 "paying at the top of the band.")]
        [Min(1f)] [SerializeField] float worldScalePerLevel = 1.05f;

        /// <summary>
        /// The world scale a lifeform heart of <paramref name="level"/> renders at — the one
        /// function every heart's size passes through (<see cref="LifeFormCrystal.ApplyLevelSize"/>).
        /// </summary>
        public float WorldScaleForLevel(int level) => levelOneWorldScale *
            Mathf.Pow(worldScalePerLevel, Mathf.Clamp(level, 1, Fauna.MaxLifeformLevel) - 1);

        // The four droppable elements - Element also has None and Omni, which are NOT valid
        // lifeform powerup elements.
        static readonly Element[] Elemental = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        public static Element RandomElement() => Elemental[Random.Range(0, Elemental.Length)];

        /// <summary>
        /// A random droppable element from a CALLER-SUPPLIED stream. Same four elements as
        /// <see cref="RandomElement"/>, but seeded by the caller so a layout can be reproduced
        /// exactly - which is what lets a mode scatter pickups identically on every peer without
        /// replicating them (Dog Fight's arena crystals).
        /// </summary>
        public static Element RandomElementFrom(System.Random rng) =>
            Elemental[rng.Next(Elemental.Length)];

        public Crystal GetPrefab(Element element) => element switch
        {
            Element.Charge => charge,
            Element.Mass => mass,
            Element.Space => space,
            Element.Time => time,
            _ => null,
        };

        static ElementalCrystalSetSO _cached;

        /// <summary>Loads (and caches) the project's elemental crystal set from Resources.</summary>
        public static ElementalCrystalSetSO Load()
        {
            if (_cached) return _cached;
            _cached = Resources.Load<ElementalCrystalSetSO>(ResourcePath);
            return _cached;
        }
    }
}
