using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    public class TeamCrystalImpactor : OmniCrystalImpactor
    {
        protected override bool IsDomainMatching(Domains domain) => 
            Crystal.ownDomain == domain;
    }
}