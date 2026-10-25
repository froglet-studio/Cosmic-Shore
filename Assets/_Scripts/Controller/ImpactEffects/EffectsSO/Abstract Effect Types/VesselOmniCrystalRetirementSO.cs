namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vessel's BESPOKE omni-crystal retirement — the animation that plays instead of the shared
    /// husk spray when THIS hull collects an omni crystal. One per vessel, part of the vessel
    /// package, authored in <see cref="VesselImpactorDataContainerSO.OmniCrystalRetirement"/>.
    ///
    /// It is its own type rather than one more entry in <c>VesselCrystalEffects</c> because it is
    /// the one crystal effect the CRYSTAL has to see: <see cref="OmniCrystalImpactor"/> asks the
    /// collecting vessel whether it retires the crystal itself and, when it does, sends the
    /// explode with <c>ExplodeParams.SuppressHusk</c> — the pickup sound and the impact latch stay,
    /// only the spray is the vessel's job now. A vessel that authors nothing keeps the spray.
    ///
    /// Replication is inherited, not re-derived: this runs from
    /// <see cref="VesselImpactor.ExecuteOmniCrystalImpact"/>, which the owner routes through
    /// <c>NetworkVesselImpactor</c> to EVERY peer, and the suppression rides the explode payload
    /// the crystal manager already broadcasts. Both halves read the same container asset, so they
    /// cannot disagree about which hull retires its own crystals.
    /// </summary>
    public abstract class VesselOmniCrystalRetirementSO : VesselCrystalEffectSO
    {
    }
}
