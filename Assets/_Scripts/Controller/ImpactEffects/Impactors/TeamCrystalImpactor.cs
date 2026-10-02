using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A domain-locked omni crystal: collectable only by its own domain — EXCEPT when it wears
    /// <see cref="Domains.Blue"/>, the "no team" sentinel, which means free-for-all exactly as it does
    /// on <see cref="Crystal.CanBeCollected"/>. Skim Race spawns its <c>BigCrystalVariant</c> (which
    /// carries this impactor) as Blue for a shared farm; a strict <c>ownDomain == domain</c> test
    /// refused every vessel there while the AI and HUD, reading <c>CanBeCollected</c>, still aimed at
    /// them. A domain-stamped team crystal (the Dolphin's Claimed Seed) is unaffected.
    /// </summary>
    public class TeamCrystalImpactor : OmniCrystalImpactor
    {
        protected override bool IsDomainMatching(Domains domain) => AdmitsShipDomain(Crystal.ownDomain, domain);

        /// <summary>Pure form of <see cref="IsDomainMatching"/>, for tests.</summary>
        public static bool AdmitsShipDomain(Domains crystalDomain, Domains shipDomain) =>
            Crystal.IsCollectableBy(crystalDomain, shipDomain);
    }
}
