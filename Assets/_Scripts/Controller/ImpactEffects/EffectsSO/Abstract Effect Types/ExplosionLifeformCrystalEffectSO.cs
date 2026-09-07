namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a BLAST does to a LIVING lifeform's embedded crystal — its heart. The explosion-side
    /// twin of <see cref="VesselLifeformCrystalEffectSO"/> (the vessel's joust surface), and a
    /// different row of the impactor matrix from <see cref="ExplosionCrystalEffectSO"/>, which
    /// takes a free-floating OMNI crystal a blast can spend.
    ///
    /// The live <see cref="Crystal"/> is passed rather than a <c>CrystalImpactData</c> for the
    /// same reason the vessel-side effect takes one: the interaction is with the OWNING LIFEFORM
    /// (<see cref="Crystal.EmbeddedIn"/>), not with a pickup, and it never routes through the
    /// networked collect chain — the ecosystem simulation is local.
    /// </summary>
    public abstract class ExplosionLifeformCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(ExplosionImpactor impactor, Crystal embeddedCrystal);
    }
}
