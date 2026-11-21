using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The one rule that seats an AI on a domain (<see cref="ServerPlayerVesselInitializerWithAI.PlacedOrBalancedDomain"/>):
    /// a domain the host placed wins, anything else is balanced. Both dealers of AI seats use it (the match's
    /// spawner and the Maelstrom's hub); the Maelstrom once had only the balanced half, so two AI the host put on
    /// Ruby came out as one Ruby and one Gold (2026-11-19).
    /// </summary>
    public class PlacedAIDomainTests
    {
        static Dictionary<Domains, int> Counts(int jade, int ruby, int gold) => new()
        {
            { Domains.Jade, jade }, { Domains.Ruby, ruby }, { Domains.Gold, gold },
        };

        [Test]
        public void TwoHumansOnJade_BothAIPlacedOnRuby_BothSitOnRuby()
        {
            var humans = Counts(2, 0, 0);
            var totals = Counts(2, 0, 0);
            var placed = new List<Domains> { Domains.Ruby, Domains.Ruby };

            var first = ServerPlayerVesselInitializerWithAI.PlacedOrBalancedDomain(0, placed, totals, humans);
            var second = ServerPlayerVesselInitializerWithAI.PlacedOrBalancedDomain(1, placed, totals, humans);

            Assert.AreEqual(Domains.Ruby, first);
            Assert.AreEqual(Domains.Ruby, second, "the second AI was balanced onto Gold instead of the placed Ruby");
            Assert.AreEqual(2, totals[Domains.Ruby]);
            Assert.AreEqual(0, totals[Domains.Gold]);
        }

        [Test]
        public void NothingPlaced_IsTheBalancedPick()
        {
            var humans = Counts(2, 0, 0);
            var totals = Counts(2, 0, 0);

            var first = ServerPlayerVesselInitializerWithAI.PlacedOrBalancedDomain(0, null, totals, humans);
            var second = ServerPlayerVesselInitializerWithAI.PlacedOrBalancedDomain(1, new List<Domains>(), totals, humans);

            Assert.AreEqual(Domains.Ruby, first);
            Assert.AreEqual(Domains.Gold, second);
        }

        [Test]
        public void BlueAndPastTheEnd_MeanNotPlaced()
        {
            var humans = Counts(1, 0, 0);
            var totals = Counts(1, 0, 0);
            var placed = new List<Domains> { Domains.Blue };

            Assert.AreEqual(Domains.Ruby, ServerPlayerVesselInitializerWithAI.PlacedOrBalancedDomain(0, placed, totals, humans));
            Assert.AreEqual(Domains.Gold, ServerPlayerVesselInitializerWithAI.PlacedOrBalancedDomain(1, placed, totals, humans));
        }

        [Test]
        public void APlacedDomainOutsideTheActiveSet_IsKept_AndCountsStayWhole()
        {
            // Two active domains; the host places an AI on Gold all the same (widening the match).
            var humans = new Dictionary<Domains, int> { { Domains.Jade, 1 }, { Domains.Ruby, 0 } };
            var totals = new Dictionary<Domains, int>(humans);
            var placed = new List<Domains> { Domains.Gold };

            var domain = ServerPlayerVesselInitializerWithAI.PlacedOrBalancedDomain(0, placed, totals, humans);

            Assert.AreEqual(Domains.Gold, domain);
            Assert.IsFalse(totals.ContainsKey(Domains.Gold), "a placed domain must not add a half-known key to the counts");
        }
    }
}
