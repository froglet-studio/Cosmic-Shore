using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A Blue team crystal is free-for-all (Skim Race's shared farm), a stamped one is domain-locked,
    /// and the impact chain must agree with <see cref="Crystal.CanBeCollected"/> — the rule the AI
    /// target sensor and the HUD objective read. They disagreed once and nobody could collect.
    /// </summary>
    public class TeamCrystalImpactorDomainTests
    {
        static readonly Domains[] Pilots = { Domains.Jade, Domains.Ruby, Domains.Gold };

        [Test]
        public void BlueCrystal_AdmitsEveryPilotDomain()
        {
            foreach (var d in Pilots)
                Assert.IsTrue(TeamCrystalImpactor.AdmitsShipDomain(Domains.Blue, d), $"Blue refused {d}");
        }

        [Test]
        public void StampedCrystal_AdmitsOwnDomain_RejectsForeign()
        {
            foreach (var crystal in Pilots)
            foreach (var ship in Pilots)
                Assert.AreEqual(crystal == ship, TeamCrystalImpactor.AdmitsShipDomain(crystal, ship),
                    $"crystal {crystal} vs ship {ship}");
        }

        [Test]
        public void ImpactorRule_MatchesCanBeCollectedRule()
        {
            var all = new[] { Domains.Jade, Domains.Ruby, Domains.Blue, Domains.Gold };
            foreach (var crystal in all)
            foreach (var ship in all)
                Assert.AreEqual(Crystal.IsCollectableBy(crystal, ship),
                    TeamCrystalImpactor.AdmitsShipDomain(crystal, ship), $"{crystal}/{ship}");
        }
    }
}
