using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Jewel Wasp's omni-crystal effect: the crystal hatches a random PREDATOR where it was collected and the wasp
    /// possesses it at once (Garrett, 2026-11-14: "for the omni make a random predator"; <c>R_VesselActions/JEWEL_WASP.md</c>
    /// §6). The pool is <see cref="JewelWaspConfigSO.OmniPredators"/>; the birth is <see cref="JewelWaspExecutor.HatchPredator"/>.
    ///
    /// <para>Wired ONLY into <c>JewelWaspImpactorDataContainer.vesselCrystalEffects</c>, the list
    /// <see cref="VesselImpactor.ExecuteOmniCrystalImpact"/> runs for an OMNI crystal. That dispatch runs on every peer; the
    /// predator is born, and possessed, only where the ecology is simulated and the hull is decided.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "JewelWaspHatchByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/JewelWaspHatchByCrystalEffectSO")]
    public sealed class JewelWaspHatchByCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (!vesselImpactor) return;
            var wasp = vesselImpactor.GetComponentInChildren<JewelWaspExecutor>(true);
            if (!wasp)
            {
                CSDebug.LogWarning($"[JewelWaspOmni] '{vesselImpactor.name}' collected an omni crystal but has no " +
                                   "JewelWaspExecutor - is this effect wired into a non-Jewel-Wasp container?", vesselImpactor);
                return;
            }
            Vector3 at = data.Origin.Valid ? data.Origin.Pose.position : vesselImpactor.transform.position;
            if (!wasp.HatchPredator(at))
                CrystalFaunaBirth.Decline(vesselImpactor.Vessel?.VesselStatus,
                                          "no predator could hatch here (no cell, an empty pool, or this peer does not simulate the ecology)");
        }
    }
}
