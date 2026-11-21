using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An Omni crystal the Honeyguide collects becomes a hive, and the Ratel goes into hive mode (Garrett, 2026-10-31: "the
    /// omnicrystal transforms into a hive ... the ratel enters hivemode after the honeyguide gets an omnicrystal";
    /// <see cref="HoneyguideCrew.OnOmniCrystal"/>). Wired into the omni list of <c>HoneyguideImpactorDataContainer</c> only
    /// (Tools/Build/author_honeyguide_assets.py), which every peer runs; the crew acts only on the machine that runs the hull's
    /// Dynamo.
    /// </summary>
    [CreateAssetMenu(fileName = "HoneyguideOmniCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/HoneyguideOmniCrystalEffectSO")]
    public sealed class HoneyguideOmniCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var hull = vesselImpactor ? vesselImpactor.Vessel?.Transform : null;
            if (!hull || !hull.TryGetComponent(out HoneyguideCrew crew)) return;
            crew.OnOmniCrystal(data.Origin.Valid ? data.Origin.Position : hull.position + hull.forward * 30f);
        }
    }
}
