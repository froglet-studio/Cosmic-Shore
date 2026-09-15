using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The vessel changer - <b>one toy that opens into the hangar</b>. Fly the station and a
    /// matrix of mini ship models blooms out ahead (every vessel in the collection except the one
    /// you are flying); fly a ship and you swap into it, and the matrix closes behind you.
    ///
    /// Reuses the existing networked swap pipeline
    /// (<see cref="MenuServerPlayerVesselInitializer.RequestSwap"/>), so the change replicates to
    /// all clients exactly like the vessel-selection panel does.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_VesselChanger", menuName = "ScriptableObjects/Toys/Vessel Changer Toy")]
    public class VesselChangerToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Vessel Changer")]
        [SerializeField, Tooltip("Vessel classes offered in the matrix (each a mini model). Leave empty for a " +
                                 "curated default set. The vessel you're currently flying is always excluded.")]
        VesselClassType[] vesselCollection;

        [Header("Layout")]
        [SerializeField, Min(10f), Tooltip("Spacing between ships in the matrix.")]
        float stationSpacing = 60f;

        [SerializeField, Min(0.5f), Tooltip("How far out the matrix blooms, in multiples of Station Spacing, " +
                                            "measured from the toy along the outward radial (away from the cell " +
                                            "centre). You fly AT the toy and keep going, so this is the gap you " +
                                            "cross before the choices. Twice the other toys' factor because the " +
                                            "ships are deliberately NOT scaled up - the spacing stays, so the " +
                                            "distance has to carry the approach on its own.")]
        float matrixDistanceFactor = 6f;

        public VesselClassType[] VesselCollection => vesselCollection;
        public float StationSpacing => stationSpacing;
        public float MatrixDistanceFactor => matrixDistanceFactor;

        /// <summary>The hull you fly. It changes VESSEL and nothing else - the world is exactly where you
        /// left it, and you are a different ship in it.</summary>
        public override ToyCategory Category => ToyCategory.Pilot;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<VesselChangerToy>();
            toy.Configure(this);
            toy.Initialize(this, context, placement);
        }
    }
}
