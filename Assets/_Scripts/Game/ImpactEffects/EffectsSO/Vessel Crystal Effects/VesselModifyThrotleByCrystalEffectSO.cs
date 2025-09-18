using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselModifyThrotleByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselModifyThrotleByCrystalEffectSO")]
    public class VesselModifyThrotleByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactor crystalImpactee)
        {
            vesselImpactor.Vessel.VesselStatus.VesselTransformer.ModifyThrottle(crystalImpactee.Crystal.crystalProperties.speedBuffAmount, _speedModifierDuration);
        }
    }
}