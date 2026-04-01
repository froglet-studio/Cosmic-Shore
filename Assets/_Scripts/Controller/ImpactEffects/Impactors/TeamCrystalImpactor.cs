using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    public class TeamCrystalImpactor : OmniCrystalImpactor
    {
        protected override bool IsDomainMatching(Domains domain) =>
            Crystal.ownDomain == domain;

        protected override void ExecuteEffect(VesselImpactor vesselImpactee)
        {
            var shipStatus = vesselImpactee.Vessel.VesselStatus;

            if (!Crystal.CanBeCollected(shipStatus.Domain))
                return;

            base.ExecuteEffect(vesselImpactee);
        }
    }
}
