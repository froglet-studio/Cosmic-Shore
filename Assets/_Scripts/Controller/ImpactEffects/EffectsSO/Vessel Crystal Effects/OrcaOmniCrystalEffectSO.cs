using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An Omni crystal the Orca collects - by flying through it, or by one of its gunners' missiles flying through it -
    /// refills its shared magazine (<see cref="OrcaCrew.OnOmniCrystal"/>; Garrett, 2026-10-20: "omnicrystals gain
    /// missiles"). Wired into the omni list of <c>OrcaImpactorDataContainer</c> only (Tools/Build/author_orca_assets.py),
    /// which every peer runs; the crew itself acts only where it runs (OrcaCrew's "Where it runs").
    /// </summary>
    [CreateAssetMenu(fileName = "OrcaOmniCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/OrcaOmniCrystalEffectSO")]
    public sealed class OrcaOmniCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var hull = vesselImpactor ? vesselImpactor.Vessel?.Transform : null;
            if (hull && hull.TryGetComponent(out OrcaCrew crew)) crew.OnOmniCrystal();
        }
    }
}
