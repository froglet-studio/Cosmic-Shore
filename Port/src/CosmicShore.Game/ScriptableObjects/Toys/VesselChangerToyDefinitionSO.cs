using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Engine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Toy that cycles the local player's vessel class on each pass — fly through it to
    /// hop to the next ship. Reuses the existing networked swap pipeline
    /// (<c>MenuServerPlayerVesselInitializer.RequestSwap</c>), so the change
    /// replicates to all clients exactly like the vessel-selection panel does.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_VesselChanger", menuName = "ScriptableObjects/Toys/Vessel Changer Toy")]
    public class VesselChangerToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Vessel Changer")]
        [SerializeField, Tooltip("Vessel classes offered as toys (each a mini model). Leave empty for a curated " +
                                 "default set. The vessel you're currently flying is always excluded.")]
        VesselClassType[] vesselCollection;

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var host = new GameObject($"ToySet_{Id}");
            host.transform.SetParent(parent, false);
            var set = host.AddComponent<VesselChangerToySet>();
            if (vesselCollection is { Length: > 0 }) set.SetCollection(vesselCollection);
            set.Initialize(this, context, placement, placement.BodyRadius, placement.TriggerRadius);
        }
    }
}
