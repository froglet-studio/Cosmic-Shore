using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "SkimmerSetShieldByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Crystal/SkimmerSetShieldByCrystalEffectSO")]
    public class SkimmerSetShieldByCrystalEffectSO : SkimmerCrystalEffectSO
    {
        [SerializeField] private int shieldIndex = 0;

        public override void Execute(SkimmerImpactor impactor, CrystalImpactor crystalImpactee)
        {
            var rs = impactor?.Skimmer?.VesselStatus?.ResourceSystem;
            if (!rs) return;
            rs.SetResourceAmount(shieldIndex, 1f);
        }
    }
}