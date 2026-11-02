using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Shrike's omni-crystal retirement: the crystal does not shatter, it folds into the nest flora the same pickup
    /// grows where it was (<see cref="ShrikeExecutor.CollectCrystal"/>). Record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/SHRIKE_CRYSTAL_MORPH.md</c>.
    ///
    /// Carries no tuning: the feel is the fleet's (<c>Resources/CrystalMorphConfig</c>). It does NOT grow the flora and
    /// must not: the sibling <see cref="ShrikeCrystalEffectSO"/> grows it and the morph lands on what that grows
    /// (<see cref="ShrikeExecutor.FloraGrownFromCrystal"/>). It runs FIRST, so it is listening before the flora exists.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ShrikeCrystalMorphByCrystal",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/ShrikeCrystalMorphByCrystalEffectSO")]
    public class ShrikeCrystalMorphByCrystalEffectSO : VesselOmniCrystalRetirementSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor != null && vesselImpactor.Vessel != null ? vesselImpactor.Vessel.VesselStatus : null;
            if (status == null) return;
            ShrikeCrystalMorph.Begin(in data.Origin, status);
        }
    }
}
