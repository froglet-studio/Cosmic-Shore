using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Dolphin's omni-crystal retirement: the crystal does not shatter, it BECOMES the Echo
    /// Obliteration cone the same hit fires. Record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_MORPH.md</c>.
    ///
    /// The window is the FLEET's (<c>Resources/CrystalMorphConfig</c>, shared with every other
    /// retirement) so a pickup reads the same length whichever hull took it. The one Dolphin-only
    /// number is how fast the crystal thins out on its way onto the cone, because only the Dolphin's
    /// target is a blast the player must keep seeing. <see cref="DolphinCrystalMorph"/> is the
    /// mechanism and <c>CrystalMorphMeshBuilder</c> the geometry.
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
        [Header("Subtlety")]
        [Tooltip("How fast the crystal's plates thin out while they stretch onto the blast cone: " +
                 "opacity = (1 - progress)^power. 2 leaves a quarter of the crystal halfway out; " +
                 "raise it for a subtler morph, lower it for a more visible one. The blast itself is " +
                 "never covered or delayed.")]
        [SerializeField, Range(0.5f, 6f)] float fadePower = DolphinCrystalMorph.DefaultFadePower;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var vessel = vesselImpactor != null ? vesselImpactor.Vessel : null;
            if (vessel == null) return;

            DolphinCrystalMorph.Begin(in data.Origin, vessel, fadePower);
        }
    }
}
