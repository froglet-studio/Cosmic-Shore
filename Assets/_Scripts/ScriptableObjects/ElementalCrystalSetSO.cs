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

        [Header("Heart sizing — the DEFAULT only; a species authors its own")]
        [Tooltip("World scale a lifeform heart renders at when its species has authored no size " +
                 "of its own. Every shipped lifeform DOES author one, per element, in its " +
                 "variant tuning (FaunaVariantTuning/FloraVariantTuning.HeartWorldScale) and " +
                 "sized to that lifeform's body — so this is the floor under a config nobody " +
                 "has sized yet, and under the runtime-provisioned misconfiguration path. " +
                 "3 is the historical flora value, which sits mid-band.")]
        [Min(0.01f)] [SerializeField] float defaultHeartWorldScale = 3f;

        /// <summary>
        /// The world scale a heart renders at when its species authors none. There is no level
        /// curve: a lifeform's heart size is a property of the lifeform (Docs/ECOSYSTEM.md §39.2).
        /// </summary>
        public float DefaultHeartWorldScale => defaultHeartWorldScale;

        /// <summary>
        /// The largest heart world scale that still pays its full collect reward.
        ///
        /// <para>The reward is <c>min(worldScale × levelPerUnitScale, maxLevelGainPerCrystal)</c>
        /// (<see cref="CosmicShore.Gameplay.SkimmerAdjustElementLevelByCrystalEffectSO"/>), so at
        /// the shipped 0.1 / 0.5 it saturates at exactly 5.0 world scale. Heart size is now
        /// AUTHORED PER LIFEFORM and the reward follows it — a bigger creature's heart is worth
        /// more — which only works while the whole authored band stays under this ceiling: past
        /// it, two visibly different hearts pay the same, i.e. a size the player can see and a
        /// reward they cannot.</para>
        ///
        /// <para>The 4% margin is deliberate headroom, not slack:
        /// <c>Tools/Build/author_lifeform_heart_sizes.py</c> fails the build if any authored
        /// heart exceeds it. Do NOT answer an overshoot by retuning
        /// <c>levelPerUnitScale</c> — that constant is shared with every non-lifeform elemental
        /// crystal (the Wanderway conveyor, Dog Fight's arena scatter). Compress the size
        /// mapping instead.</para>
        /// </summary>
        public const float MaxSafeHeartWorldScale = 4.8f;

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
