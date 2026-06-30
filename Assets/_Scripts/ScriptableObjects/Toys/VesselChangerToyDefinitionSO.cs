using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Toy that cycles the local player's vessel class on each pass — fly through it to
    /// hop to the next ship. Reuses the existing networked swap pipeline
    /// (<see cref="MenuServerPlayerVesselInitializer.RequestSwap"/>), so the change
    /// replicates to all clients exactly like the vessel-selection panel does.
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_VesselChanger", menuName = "ScriptableObjects/Toys/Vessel Changer Toy")]
    public class VesselChangerToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Vessel Changer")]
        [SerializeField, Tooltip("Vessel classes the toy cycles through, in order. Leave empty for the full playable set.")]
        VesselClassType[] vesselCycle;

        public override Toy CreateToy(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor, DisplayName);
            var toy = go.AddComponent<VesselChangerToy>();
            if (vesselCycle is { Length: > 0 }) toy.SetCycle(vesselCycle);
            toy.Initialize(this, context, placement);
            return toy;
        }
    }
}
