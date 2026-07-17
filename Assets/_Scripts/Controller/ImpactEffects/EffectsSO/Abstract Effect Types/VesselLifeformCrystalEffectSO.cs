namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Effect run when a vessel impacts a LIVING lifeform's embedded elemental crystal (its
    /// heart) - the joust surface. Receives the live Crystal (not CrystalImpactData) because
    /// the embedded interaction needs the owning Fauna, and it never routes through the
    /// networked collect chain (the ecosystem simulation is local).
    /// </summary>
    public abstract class VesselLifeformCrystalEffectSO : ImpactEffectSO
    {
        public abstract void Execute(VesselImpactor vesselImpactor, Crystal embeddedCrystal);
    }
}
