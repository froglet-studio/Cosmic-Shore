using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The four elemental crystal prefabs (Charge / Mass / Space / Time), and the one size every
    /// elemental crystal is (<see cref="CrystalWorldScale"/>). Every lifeform is
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

        /// <summary>
        /// ONE SIZE FOR EVERY ELEMENTAL CRYSTAL: the world scale of a Charge, Mass, Space or Time
        /// crystal wherever it appears - a lifeform's heart, a petal knocked out of a hull, a
        /// mode's arena scatter, a toy's pickup. Every one of them is worth exactly one petal
        /// (<see cref="CosmicShore.Gameplay.SkimmerAdjustElementLevelByCrystalEffectSO.LevelGainPerCrystal"/>),
        /// so there is nothing for a size to say, and every crystal says the same thing.
        ///
        /// <para><b>Taken from the Elemental Converter</b> (the omni combine on the Ark screen,
        /// <see cref="CosmicShore.Gameplay.OmniCombinePlayer"/>), whose crystals are the right
        /// size for an elemental crystal (Garrett, 2026-10-26). There each crystal is a share of
        /// the omni, and the Charge crystal - the reference the game's four models are equalized
        /// to - is 0.503 of the omni's half-extent (<c>OmniCombineData.json</c>: 0.808 against
        /// 1.606). The omni's size is fixed: <c>Crystal.prefab</c> at root 10, the omni 37 of
        /// the 39 crystal managers spawn, whose body is 0.72 u of half-extent per unit of root
        /// scale against the Charge body's 1.02. So 0.503 x 10 x 0.72 / 1.02 = 3.55.
        /// <c>ElementalCrystalSizeTests</c> measures all three meshes and holds this to the
        /// converter's ratio.</para>
        ///
        /// <para>This is the ROOT's world scale. The four prefabs' model children still carry
        /// their per-element corrections (Mass 1.38, Space 1.34, Time 1.42), so one root scale
        /// renders one apparent size for every element. The prefabs' own root scale is this
        /// value, so a crystal instantiated and left alone is already the right size.</para>
        /// </summary>
        public const float CrystalWorldScale = 3.55f;

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
