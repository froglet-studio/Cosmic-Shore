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

        // The four droppable elements — Element also has None and Omni, which are NOT valid
        // lifeform powerup elements.
        static readonly Element[] Elemental = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        public static Element RandomElement() => Elemental[Random.Range(0, Elemental.Length)];

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
