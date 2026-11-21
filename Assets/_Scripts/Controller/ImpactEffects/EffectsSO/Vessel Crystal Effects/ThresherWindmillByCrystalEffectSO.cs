using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's omni-crystal effect: the wrecking ball windmills round the ship — red-hot,
    /// across its line of flight — for <c>ThresherConfigSO.windmillSeconds</c>
    /// (<see cref="ThresherExecutor.StartWindmill"/>, <c>THRESHER.md</c> § Windmill).
    ///
    /// Wired ONLY into <c>ThresherImpactorDataContainer.vesselCrystalEffects</c>, the list
    /// <see cref="VesselImpactor.ExecuteOmniCrystalImpact"/> runs for an OMNI crystal; elemental
    /// crystals dispatch to the per-element lists and never reach it, so collecting Charge, Mass,
    /// Space or Time still only levels the element. That dispatch runs on EVERY peer (the owner
    /// routes it through <c>NetworkVesselImpactor</c>), and every peer simulates the ball, so every
    /// peer starts the same windmill from the same pickup.
    /// </summary>
    [CreateAssetMenu(fileName = "ThresherWindmillByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/ThresherWindmillByCrystalEffectSO")]
    public sealed class ThresherWindmillByCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (!vesselImpactor) return;
            // The executor lives on the hull's ShipActions child; this effect is on no other
            // vessel's container, so a miss here is a Thresher prefab without its executor.
            var thresher = vesselImpactor.GetComponentInChildren<ThresherExecutor>(true);
            if (!thresher)
            {
                CSDebug.LogWarning($"[ThresherWindmill] '{vesselImpactor.name}' collected an omni crystal but has no " +
                                 "ThresherExecutor - is this effect wired into a non-Thresher container?", vesselImpactor);
                return;
            }
            thresher.StartWindmill();
        }
    }
}
