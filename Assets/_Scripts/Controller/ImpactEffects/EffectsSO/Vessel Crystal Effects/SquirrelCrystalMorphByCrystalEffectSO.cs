using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Squirrel's omni-crystal retirement: the crystal does not shatter, it BECOMES the eight
    /// shielded prisms of the boost ring the same hit lays. Record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md</c>.
    ///
    /// This asset carries no tuning on purpose. The feel is the FLEET's
    /// (<c>Resources/CrystalMorphConfig</c>, shared with the Scarab's forge) so a pickup reads the
    /// same length whichever hull took it; <see cref="SquirrelCrystalMorph"/> is the mechanism and
    /// <c>CrystalMorphMeshBuilder</c> the geometry.
    ///
    /// It deliberately does NOT lay the ring, and must not: the sibling
    /// <see cref="VesselExplosionByCrystalEffectSO"/> lays it through the ordinary AOE spawner and
    /// the morph lands on whatever that lays (<see cref="BoostRingBuilder.RingLaid"/>). One authority
    /// for the ring. It runs FIRST in <see cref="VesselImpactor.ExecuteOmniCrystalImpact"/>, so it
    /// is listening before the sibling lays.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SquirrelCrystalMorphByCrystal",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/SquirrelCrystalMorphByCrystalEffectSO")]
    public class SquirrelCrystalMorphByCrystalEffectSO : VesselOmniCrystalRetirementSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor != null && vesselImpactor.Vessel != null
                ? vesselImpactor.Vessel.VesselStatus
                : null;
            if (status == null) return;

            SquirrelCrystalMorph.Begin(in data.Origin, status.Domain);
        }
    }
}
