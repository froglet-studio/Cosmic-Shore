using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Angler's omni-crystal effect: the crystal MORPHS into a random creature that joins the school (the designer,
    /// 2026-10-16; <c>R_VesselActions/ANGLER.md</c> §1; the Angler Command Lab's <c>omniMorph</c>), born where the crystal was
    /// collected (<see cref="AnglerCommandExecutor.OmniMorph"/>). The asset keeps its first name (round 2 was a mass tame).
    ///
    /// Wired ONLY into <c>AnglerImpactorDataContainer.vesselCrystalEffects</c>, the list
    /// <see cref="VesselImpactor.ExecuteOmniCrystalImpact"/> runs for an OMNI crystal (elemental crystals dispatch to
    /// the per-element lists and never reach it). That dispatch runs on every peer; a replicated species is born, and any
    /// creature leashed, only where the ecology is simulated.
    /// </summary>
    [CreateAssetMenu(fileName = "AnglerTameByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/AnglerTameByCrystalEffectSO")]
    public sealed class AnglerTameByCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (!vesselImpactor) return;
            // The executor lives on the hull's ShipActions child; this effect is on no other vessel's container, so a
            // miss here is an Angler prefab without its command executor.
            var angler = vesselImpactor.GetComponentInChildren<AnglerCommandExecutor>(true);
            if (!angler)
            {
                CSDebug.LogWarning($"[AnglerOmniTame] '{vesselImpactor.name}' collected an omni crystal but has no " +
                                   "AnglerCommandExecutor - is this effect wired into a non-Angler container?", vesselImpactor);
                return;
            }
            Vector3 at = data.Origin.Valid ? data.Origin.Pose.position : vesselImpactor.transform.position;
            angler.OmniMorph(at);
        }
    }
}
