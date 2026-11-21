using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The vessel changer - <b>the hangar</b>, in the Toy Box: every vessel in the collection, the
    /// one you fly flagged; pick a ship and you swap into it.
    ///
    /// Reuses the existing networked swap pipeline
    /// (<see cref="MenuServerPlayerVesselInitializer.RequestSwap"/>), so the change replicates to
    /// all clients exactly like the vessel-selection panel does.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_VesselChanger", menuName = "ScriptableObjects/Toys/Vessel Changer Toy")]
    public class VesselChangerToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Vessel Changer")]
        [SerializeField, Tooltip("Vessel classes offered in the Toy Box. Leave empty for a " +
                                 "curated default set. The vessel you're currently flying is listed, flagged.")]
        VesselClassType[] vesselCollection;

        [SerializeField, Tooltip("Where each hull's picture comes from on the Toy Box's vessel list - " +
                                 "the class assets' card art. Leave empty and the rows are names only.")]
        SO_VesselList vesselArt;

        public VesselClassType[] VesselCollection => vesselCollection;
        public SO_VesselList VesselArt => vesselArt;

        /// <summary>The hull you fly. It changes VESSEL and nothing else - the world is exactly where you
        /// left it, and you are a different ship in it.</summary>
        public override ToyCategory Category => ToyCategory.Pilot;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateToyRoot(Id, parent, placement);
            var toy = go.AddComponent<VesselChangerToy>();
            toy.Configure(this);
            toy.Initialize(this, context, placement);
        }
    }
}
