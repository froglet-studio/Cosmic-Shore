using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Stoat's omni-crystal effect — OVERCLOCK (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1e, the doc's proposal O1):
    /// for <see cref="seconds"/> the pathfinder's warp boost holds at full whether or not the poles warp the path, and
    /// the line stays lime (<see cref="StoatPathfinderExecutor.Overclock"/>). The Time race's fantasy as a pickup.
    ///
    /// Wired ONLY into <c>StoatImpactorDataContainer.vesselCrystalEffects</c>, the list
    /// <see cref="VesselImpactor.ExecuteOmniCrystalImpact"/> runs for an OMNI crystal (elemental crystals dispatch to
    /// the per-element lists and never reach it). That dispatch runs on every peer; the boost itself acts only where
    /// the hull is simulated, which is where the pathfinder writes the flight clock.
    /// </summary>
    [CreateAssetMenu(fileName = "StoatOverclockByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/StoatOverclockByCrystalEffectSO")]
    public sealed class StoatOverclockByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Tooltip("Seconds the pathfinder's boost holds at full after an omni crystal.")]
        [SerializeField, Range(0.5f, 30f)] float seconds = 6f;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (!vesselImpactor) return;
            // The executor lives on the hull's ShipActions child; this effect is on no other vessel's container, so a
            // miss here is a Stoat prefab without its pathfinder.
            var pathfinder = vesselImpactor.GetComponentInChildren<StoatPathfinderExecutor>(true);
            if (!pathfinder)
            {
                CSDebug.LogWarning($"[StoatOverclock] '{vesselImpactor.name}' collected an omni crystal but has no " +
                                   "StoatPathfinderExecutor - is this effect wired into a non-Stoat container?", vesselImpactor);
                return;
            }
            pathfinder.Overclock(seconds);
        }
    }
}
