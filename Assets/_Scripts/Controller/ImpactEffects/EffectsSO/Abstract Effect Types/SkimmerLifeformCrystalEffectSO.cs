namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Effect run when a SKIMMER sweeps a LIVING lifeform's embedded crystal (its heart) — the
    /// skimmer sibling of <see cref="VesselLifeformCrystalEffectSO"/> and
    /// <see cref="ExplosionLifeformCrystalEffectSO"/>.
    ///
    /// <para><b>This arm did not exist before the Butterfly.</b> <c>SkimmerImpactor</c>'s crystal
    /// case returns outright on <c>crystal.IsEmbedded</c>, and correctly so: an embedded heart is
    /// not skim-COLLECTABLE, and without that gate every skimmer crystal effect in the fleet (the
    /// Rhino sword's crystal burst) would fire on it repeatedly, since a heart's collider is never
    /// disabled by a collection. So the fix is a separate LIST rather than a widened one — a
    /// heart reaching a skimmer is a different event from a pickup reaching it, and the two want
    /// different effects.</para>
    ///
    /// <para>Every other vessel's skimmer container leaves this array empty, so the new arm is a
    /// provable no-op across the fleet: an empty list is what <c>DoesEffectExist</c> already
    /// returns false for.</para>
    ///
    /// <para>Receives the live <see cref="Crystal"/> (not <c>CrystalImpactData</c>) because the
    /// embedded interaction needs the owning lifeform, and it never routes through the networked
    /// collect chain — the ecosystem simulation is local.</para>
    /// </summary>
    public abstract class SkimmerLifeformCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(SkimmerImpactor impactor, Crystal embeddedCrystal);
    }
}
