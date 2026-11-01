using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The omni-crystal retirement for every hull whose omni-crystal ability is a plain spherical
    /// blast (the Butterfly's bloom, the Rhino's slow blast, the Manta's Kabloom self-blast): the
    /// crystal does not shatter, it BECOMES the sphere. Record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/SPHERE_BLAST_CRYSTAL_MORPH.md</c>.
    ///
    /// One asset serves every such hull. It carries no tuning on purpose: the feel is the FLEET's
    /// (<c>Resources/CrystalMorphConfig</c>) and the shape is the blast's own.
    ///
    /// It deliberately does NOT fire the blast, and must not: the sibling effect in the hull's
    /// <c>VesselCrystalEffects</c> fires it and the morph lands on whatever that fires
    /// (<see cref="AOEExplosion.BlastInitialized"/>). It runs FIRST in
    /// <see cref="VesselImpactor.ExecuteOmniCrystalImpact"/>, so it is listening before the blast.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SphereBlastCrystalMorphByCrystal",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/SphereBlastCrystalMorphByCrystalEffectSO")]
    public class SphereBlastCrystalMorphByCrystalEffectSO : VesselOmniCrystalRetirementSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var vessel = vesselImpactor != null ? vesselImpactor.Vessel : null;
            if (vessel == null || vessel.VesselStatus == null) return;

            SphereBlastCrystalMorph.Begin(in data.Origin, vessel, vessel.VesselStatus.VesselType.ToString());
        }
    }
}
