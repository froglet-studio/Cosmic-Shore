namespace CosmicShore.Game
{
    public class TeamCrystalImpactor : OmniCrystalImpactor
    {
        protected override bool IsDomainMatching(Domains domain) => 
            Crystal.ownDomain == domain;
    }
}