using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Dolphin's omni-crystal retirement: the crystal does not shatter, it BECOMES the Echo
    /// Obliteration cone the same hit fires. Record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_MORPH.md</c>.
    ///
    /// This asset carries no tuning on purpose. The feel is the FLEET's
    /// (<c>Resources/CrystalMorphConfig</c>, shared with the Squirrel's ring and the Scarab's forge)
    /// so a pickup reads the same length whichever hull took it; <see cref="DolphinCrystalMorph"/>
    /// is the mechanism and <c>CrystalMorphMeshBuilder</c> the geometry.
    ///
    /// It deliberately does NOT fire the blast, and must not: the sibling
    /// <see cref="VesselExplosionByCrystalEffectSO"/> fires it through the ordinary AOE spawner and
    /// the morph lands on whatever that fires (<see cref="AOEConicExplosion.Initialized"/>). One
    /// authority for the blast. It runs FIRST in <see cref="VesselImpactor.ExecuteOmniCrystalImpact"/>,
    /// so it is listening before the sibling fires.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DolphinCrystalMorphByCrystal",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/DolphinCrystalMorphByCrystalEffectSO")]
    public class DolphinCrystalMorphByCrystalEffectSO : VesselOmniCrystalRetirementSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var vessel = vesselImpactor != null ? vesselImpactor.Vessel : null;
            if (vessel == null) return;

            DolphinCrystalMorph.Begin(in data.Origin, vessel);
        }
    }
}
