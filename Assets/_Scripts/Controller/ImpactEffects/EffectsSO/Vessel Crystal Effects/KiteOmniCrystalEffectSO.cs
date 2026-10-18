using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An Omni crystal the Kite's hull collects widens the navigator's board for a while: the kite view zooms out, the
    /// kite may fly that much further, and the lasso grows with it (<see cref="KiteNavigator.OnOmniCrystal"/>; Garrett,
    /// 2026-10-18). Wired into the omni list of <c>KiteImpactorDataContainer</c> only (Tools/Build/author_kite_assets.py),
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
