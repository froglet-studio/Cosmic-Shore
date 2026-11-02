using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The omni-crystal retirement of every hull whose omni ability is a CREATURE (the Angler, the Jacks): the crystal
    /// does not shatter, it folds into the heart of the creature the same pickup is born as. Record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/FAUNA_BIRTH_CRYSTAL_MORPH.md</c>.
    ///
    /// Carries no tuning: the feel is the fleet's (<c>Resources/CrystalMorphConfig</c>). It does NOT spawn the creature
    /// and must not: the sibling effect in the vessel's crystal list spawns it and announces it through
    /// <see cref="CrystalFaunaBirth"/>. It runs FIRST, so it is listening before the creature exists.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FaunaBirthCrystalMorphByCrystal",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/FaunaBirthCrystalMorphByCrystalEffectSO")]
    public class FaunaBirthCrystalMorphByCrystalEffectSO : VesselOmniCrystalRetirementSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var vessel = vesselImpactor != null ? vesselImpactor.Vessel : null;
            var status = vessel?.VesselStatus;
            if (status == null) return;
            FaunaBirthCrystalMorph.Begin(in data.Origin, status, status.VesselType.ToString());
        }
    }
}
