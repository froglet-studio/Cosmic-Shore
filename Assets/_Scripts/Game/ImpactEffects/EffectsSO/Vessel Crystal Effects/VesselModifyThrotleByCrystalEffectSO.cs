using UnityEngine;
using CosmicShore.Game.Environment.FlowField;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselCrystalEffects
{
    [CreateAssetMenu(fileName = "VesselModifyThrotleByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselModifyThrotleByCrystalEffectSO")]
    public class VesselModifyThrotleByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;
        
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            vesselImpactor.Vessel.VesselStatus.VesselTransformer.ModifyThrottle(data.SpeedBuffAmount, _speedModifierDuration);
        }
    }
}