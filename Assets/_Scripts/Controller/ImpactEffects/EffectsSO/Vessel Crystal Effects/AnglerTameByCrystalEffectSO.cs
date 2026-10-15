using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Angler's omni-crystal effect — the MASS TAME (<c>R_VesselActions/ANGLER.md</c> §1; the Angler Command Lab's
    /// <c>omniTame</c>): every wild herbivore within the config's <c>omniTameRadius</c> of the hull joins the school at
    /// once, nearest first, up to the leash capacity (<see cref="AnglerCommandExecutor.OmniTame"/>).
    ///
    /// Wired ONLY into <c>AnglerImpactorDataContainer.vesselCrystalEffects</c>, the list
    /// <see cref="VesselImpactor.ExecuteOmniCrystalImpact"/> runs for an OMNI crystal (elemental crystals dispatch to
    /// the per-element lists and never reach it). That dispatch runs on every peer; a creature can be leashed only where
    /// it is simulated, so the taming lands on the creatures' authority alone.
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
            angler.OmniTame();
        }
    }
}
