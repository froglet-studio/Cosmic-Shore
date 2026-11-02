using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Honeyguide's omni-crystal retirement: the crystal does not shatter, it breaks into the heart of the hive comb the
    /// same pickup lays ("the omnicrystal becomes the hive", <see cref="HoneyguideCrew"/>). Record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/HONEYGUIDE_CRYSTAL_MORPH.md</c>.
    ///
    /// Carries no tuning: the feel is the fleet's (<c>Resources/CrystalMorphConfig</c>). It does NOT open the hive and must
    /// not: the sibling <see cref="HoneyguideOmniCrystalEffectSO"/> does, and the morph lands on the comb that lays
    /// (<see cref="HoneyguideCrew.CombLaid"/>). It runs FIRST, so it is listening before the comb exists.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HoneyguideCrystalMorphByCrystal",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/HoneyguideCrystalMorphByCrystalEffectSO")]
    public class HoneyguideCrystalMorphByCrystalEffectSO : VesselOmniCrystalRetirementSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor != null ? vesselImpactor.Vessel?.VesselStatus : null;
            if (status == null) return;
            HoneyguideCrystalMorph.Begin(in data.Origin, status);
        }
    }
}
