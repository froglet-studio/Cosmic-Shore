using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselChangeResourceByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselChangeResourceByPrismEffectSO")]
    public class VesselChangeResourceByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private int energyResourceIndex; 

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            var rs = vesselImpactor?.Vessel?.VesselStatus?.ResourceSystem;
            if (rs == null) return;

            if (energyResourceIndex < 0 || energyResourceIndex >= rs.Resources.Count) return;

            var res = rs.Resources[energyResourceIndex];
            var halfAmount = res.CurrentAmount * 0.5f;

            rs.SetResourceAmount(energyResourceIndex, halfAmount);
        }
    }
}