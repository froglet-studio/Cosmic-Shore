namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vessel's BESPOKE omni-crystal retirement — the animation that plays instead of the
    /// shared husk spray when THIS hull collects an omni crystal. One per vessel; part of the
    /// vessel package, authored on that vessel's <see cref="VesselImpactorDataContainerSO"/>.
    ///
    /// It exists as its own type, rather than as one more entry in
    /// <c>VesselCrystalEffects</c>, because it is the one crystal effect that has to be
    /// visible to the CRYSTAL: <see cref="OmniCrystalImpactor"/> asks the collecting vessel
    /// whether it retires the crystal itself, and skips <see cref="Crystal.Explode"/> when it
    /// does. A vessel that authors nothing here keeps the shared husk spray, so the slot is
    /// opt-in per hull and the fleet migrates one vessel at a time.
    ///
    /// Replication comes for free and must not be re-derived: this runs from
    /// <see cref="VesselImpactor.ExecuteOmniCrystalImpact"/>, which the owner routes through
    /// <c>NetworkVesselImpactor.ExecuteCrystalImpact_ServerRpc</c> and back out to EVERY peer,
    /// so a bespoke retirement plays on every machine exactly like the husk it replaces. The
    /// suppression half is server-only and needs no RPC of its own — the server is the only
    /// machine that reaches <c>Crystal.Explode</c>'s broadcast in the first place.
    /// </summary>
    public abstract class VesselOmniCrystalRetirementSO : VesselCrystalEffectSO
    {
    }
}
