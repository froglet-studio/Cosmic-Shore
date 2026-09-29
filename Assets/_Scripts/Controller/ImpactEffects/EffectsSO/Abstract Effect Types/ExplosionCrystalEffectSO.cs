
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a BLAST does to a crystal it engulfs — the missing row of the impactor matrix. Every
    /// other impactor that can reach a crystal has one (a vessel collects it, a projectile pops
    /// it); an explosion reached crystals through the same trigger and then fell out of the
    /// bottom of the switch doing nothing at all.
    ///
    /// The crystal impactor is passed rather than a <see cref="CrystalImpactData"/> because a
    /// blast needs the crystal's POSITION — it is not touching the crystal the way a vessel is,
    /// so "where the collector is" and "where the crystal is" are different places, and
    /// <c>CrystalImpactData</c> deliberately carries no position (widening that networked struct
    /// would change the wire format for the whole fleet).
    /// </summary>
    public abstract class ExplosionCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ExplosionImpactor impactor, OmniCrystalImpactor crystalImpactee);
    }
}
