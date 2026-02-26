using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
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