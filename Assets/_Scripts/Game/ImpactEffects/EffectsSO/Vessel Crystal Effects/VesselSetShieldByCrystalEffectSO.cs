using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselSetShieldByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Crystal/VesselSetShieldByCrystalEffectSO")]
    public class VesselSetShieldByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField] private int shieldIndex = 0;

        public override void Execute(VesselImpactor impactor, CrystalImpactData data)
        {
            var rs = impactor?.Vessel?.VesselStatus?.ResourceSystem;
            if (!rs) return;
            rs.SetResourceAmount(shieldIndex, 1f);
        }
    }
}