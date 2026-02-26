using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselPrismEffects
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