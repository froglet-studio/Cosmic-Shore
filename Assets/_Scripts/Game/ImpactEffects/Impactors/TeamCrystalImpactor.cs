using CosmicShore.Models.Enums;
using CosmicShore.Game.Environment.FlowField;
namespace CosmicShore.Game.ImpactEffects.Impactors
{
    public class TeamCrystalImpactor : OmniCrystalImpactor
    {
        protected override bool IsDomainMatching(Domains domain) => 
            Crystal.ownDomain == domain;
    }
}