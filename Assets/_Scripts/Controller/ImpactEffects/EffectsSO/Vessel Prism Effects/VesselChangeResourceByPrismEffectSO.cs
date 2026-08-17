using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "VesselChangeResourceByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselChangeResourceByPrismEffectSO")]
    public class VesselChangeResourceByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private int energyResourceIndex;

        [Tooltip("Fraction of the banked resource the vessel KEEPS on a prism ram. 0.5 = loses half.")]
        [SerializeField, Range(0f, 1f)] private float retainedFraction = 0.5f;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            var rs = vesselImpactor?.Vessel?.VesselStatus?.ResourceSystem;
            if (rs == null) return;

            if (energyResourceIndex < 0 || energyResourceIndex >= rs.Resources.Count) return;

            var res = rs.Resources[energyResourceIndex];

            rs.SetResourceAmount(energyResourceIndex, res.CurrentAmount * retainedFraction);
        }
    }
}