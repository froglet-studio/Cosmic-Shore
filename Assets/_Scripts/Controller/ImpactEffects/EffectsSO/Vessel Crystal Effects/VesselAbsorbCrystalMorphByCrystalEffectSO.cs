using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The omni-crystal retirement of every hull whose omni ability puts nothing in the world to land on (the Brittle Star,
    /// Sparrow, Stoat, Orca and Wrasse): the crystal does not shatter, it folds into the vessel that took it. A design
    /// placeholder until each hull is given a target of its own. Record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/VESSEL_ABSORB_CRYSTAL_MORPH.md</c>.
    ///
    /// Carries no tuning: the feel is the fleet's (<c>Resources/CrystalMorphConfig</c>). The hull's own omni effect runs
    /// after it, unchanged.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselAbsorbCrystalMorphByCrystal",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselAbsorbCrystalMorphByCrystalEffectSO")]
    public class VesselAbsorbCrystalMorphByCrystalEffectSO : VesselOmniCrystalRetirementSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor != null ? vesselImpactor.Vessel?.VesselStatus : null;
            if (status == null) return;
            VesselAbsorbCrystalMorph.Begin(in data.Origin, status, status.VesselType.ToString());
        }
    }
}
