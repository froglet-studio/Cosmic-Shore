using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An elemental crystal that only its OWN team can skim up: the elemental twin of <see cref="TeamCrystalImpactor"/>
    /// (the omni one). <see cref="ElementalCrystalImpactor"/> never asks who is skimming, so a crystal a mode tints to a
    /// team (<see cref="Crystal.ApplyDomainPreview"/>) is still anyone's pickup; this one refuses a skimmer whose
    /// vessel's domain <see cref="Crystal.CanBeCollected"/> rejects. A crystal left neutral
    /// (<see cref="Domains.Blue"/>) stays everyone's, exactly as the base.
    ///
    /// <para>Opt-in: a mode that lays team crystals adds THIS component instead of the base one when it mints them
    /// (Shatterplane's I3 field, <c>ShatterplaneController.Mint</c>). Nothing else in the game changes, so every elemental
    /// crystal that already ships keeps the base's no-team rule.</para>
    /// </summary>
    public class TeamElementalCrystalImpactor : ElementalCrystalImpactor
    {
        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (impactee is SkimmerImpactor skimmer && Crystal && !Crystal.CanBeCollected(DomainOf(skimmer))) return;
            base.AcceptImpactee(impactee);
        }

        /// <summary>The skimming vessel's domain, or <see cref="Domains.Blue"/> for a skimmer with no vessel (which a
        /// team crystal then refuses, as it refuses any other team).</summary>
        static Domains DomainOf(SkimmerImpactor skimmer)
        {
            var status = skimmer.Skimmer ? skimmer.Skimmer.VesselStatus : null;
            return status != null ? status.Domain : Domains.Blue;
        }
    }
}
