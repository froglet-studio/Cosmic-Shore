using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An Omni crystal the Wrasse's hull collects drops a cluster of each element's motes on the navigator's board, so a
    /// crew short of crystals can make up for it (<see cref="KiteNavigator.OnOmniCrystal"/>; Garrett, 2026-10-18 round 4e,
    /// replacing round 4's zoomed-out board). Wired into the omni list of <c>WrasseImpactorDataContainer</c> only (Tools/Build/author_wrasse_assets.py),
    /// which every peer runs; the seat itself acts only on the local pilot's hull.
    /// </summary>
    [CreateAssetMenu(fileName = "KiteOmniCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/KiteOmniCrystalEffectSO")]
    public sealed class KiteOmniCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var hull = vesselImpactor ? vesselImpactor.Vessel?.Transform : null;
            if (hull && hull.TryGetComponent(out KiteNavigator seat)) seat.OnOmniCrystal();
        }
    }
}
