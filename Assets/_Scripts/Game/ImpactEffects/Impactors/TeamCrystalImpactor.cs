using CosmicShore.Models.Enums;
using CosmicShore.Game.Environment;
namespace CosmicShore.Game.ImpactEffects
{
    public class TeamCrystalImpactor : OmniCrystalImpactor
    {
        protected override bool IsDomainMatching(Domains domain) => 
            Crystal.ownDomain == domain;
    }
}