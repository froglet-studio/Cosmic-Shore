using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipBoostByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/VesselBoostByCrystalEffectSo")]
    public class VesselBoostByCrystalEffectSo : VesselCrystalEffectSO
    {
        [SerializeField]
        float _speedModifierDuration;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactor crystalImpactee)
        {
            vesselImpactor.Ship.ShipStatus.ShipTransformer.ModifyThrottle(crystalImpactee.Crystal.crystalProperties.speedBuffAmount, _speedModifierDuration);
        }
    }
}