using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rhino energy-sword vs crystal effect (RHINO_ENERGY_SWORD.md). When the sword collects an
    /// elemental crystal it spends ALL stored energy bursting the blade in all three dimensions,
    /// scaled by the energy at the moment of the hit. The 3D burst, the blade flash, the local
    /// camera shake, and the energy drain are owned by the sword state
    /// (<see cref="IRhinoSwordState.TriggerCrystalBurst"/>); this effect only kicks it off.
    ///
    /// It deliberately spawns NO explosion. The sword capsule overlaps the hull, so every elemental
    /// crystal the Rhino flies through reaches this effect — an AOE here detonated on every petal
    /// pickup, which was never the intent.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoSwordCrystalBurstEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Crystal/RhinoSwordCrystalBurstEffectSO")]
    public sealed class RhinoSwordCrystalBurstEffectSO : SkimmerCrystalEffectSO
    {
        public override void Execute(SkimmerImpactor impactor, CrystalImpactor impactee)
        {
            if (impactor == null || impactor.Skimmer == null || impactee == null) return;

            // 3D size burst + flash + shake scaled by stored energy, then consume ALL of it.
            impactor.Skimmer.SwordState?.TriggerCrystalBurst();
        }
    }
}
