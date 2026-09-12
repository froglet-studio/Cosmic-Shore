using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// STING's vessel half: the Manta's skimmer grazing another VESSEL charges the bay a tick
    /// and — when the bay holds a bomb, the target is an enemy, and the Manta carries the
    /// authored speed margin — plants a silent bomb on them. Authored in the Manta's SKIMMER
    /// container's vessel-skimmer array (the joust surface,
    /// <see cref="VesselExplosionBySkimmerEffectSO"/>'s slot), so "graze to plant" rides the
    /// exact contact the platform already calls a joust.
    ///
    /// The dispatcher hands the OTHER vessel as <paramref name="impactor"/> and this Manta's
    /// skimmer as <paramref name="impactee"/> — the same argument order every vessel-skimmer
    /// effect lives with. All eligibility (one bomb per target, domain, speed, simulation
    /// authority) lives in the bay, so this effect stays a pure router.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MantaStingPlantBombVesselEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/MantaStingPlantBombVesselEffectSO")]
    public class MantaStingPlantBombVesselEffectSO : VesselSkimmerEffectsSO
    {
        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            var mantaStatus = impactee?.Skimmer?.VesselStatus;
            var targetStatus = impactor?.Vessel?.VesselStatus;
            if (mantaStatus == null || targetStatus == null) return;
            if (!MantaStingActionExecutor.TryGetFor(mantaStatus, out var bay)) return;

            bay.AddVesselSkimCharge(targetStatus);
            bay.TryPlantOnVessel(targetStatus);
        }
    }
}
