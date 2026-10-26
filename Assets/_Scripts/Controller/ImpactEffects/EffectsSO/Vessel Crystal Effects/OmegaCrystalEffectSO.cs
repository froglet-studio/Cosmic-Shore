using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A crystal an Omega crew's hull collects fills the omega meter (<see cref="OmegaCrew.OnCrystal"/>: the page's
    /// meterPerCrystal, half of it in the omega form). Wired into the crystal list of <c>OmegaImpactorDataContainer</c>
    /// (Tools/Build/author_omega_assets.py); the crew itself acts only where it runs (OmegaCrew's "Where it runs").
    /// </summary>
    [CreateAssetMenu(fileName = "OmegaCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/OmegaCrystalEffectSO")]
    public sealed class OmegaCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var hull = vesselImpactor ? vesselImpactor.Vessel?.Transform : null;
            if (hull && hull.TryGetComponent(out OmegaCrew crew)) crew.OnCrystal();
        }
    }
}
