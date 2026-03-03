using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Gameplay
{
    [TestFixture]
    public class DomainAssignerTests
    {
        [SetUp]
        public void SetUp()
        {
            DomainAssigner.Initialize();
        }

        [Test]
        public void Initialize_PopulatesDomainPool()
        {
            // After Initialize, the first call should return a valid domain.
            var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            Assert.AreNotEqual(Domains.None, domain);
            Assert.AreNotEqual(Domains.Unassigned, domain);
            Assert.AreNotEqual(Domains.Blue, domain);
        }

        [Test]
        public void GetDomainsByGameModes_ReturnsUniqueDomains()
        {
            var assigned = new HashSet<Domains>();

            // There are 3 valid domains (Jade, Ruby, Gold) after excluding None, Unassigned, Blue.
            for (int i = 0; i < 3; i++)
            {
                var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);
                Assert.IsTrue(assigned.Add(domain),
                    $"Domain {domain} was assigned twice. Assigned so far: {string.Join(", ", assigned)}");
            }
        }

        [Test]
        public void GetDomainsByGameModes_EmptyPool_ReturnsUnassigned()
        {
            // Exhaust the pool
            for (int i = 0; i < 10; i++)
                DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            Assert.AreEqual(Domains.Unassigned, domain);
        }

        [Test]
        public void GetDomainsByGameModes_CoOpMode_AlwaysReturnsJade()
        {
            var domain = DomainAssigner.GetDomainsByGameModes(GameModes.Multiplayer2v2CoOpVsAI);

            Assert.AreEqual(Domains.Jade, domain);
        }

        [Test]
        public void GetDomainsByGameModes_WildlifeBlitz_AlwaysReturnsJade()
        {
            var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerWildlifeBlitzGame);

            Assert.AreEqual(Domains.Jade, domain);
        }

        [Test]
        public void Initialize_ResetsPool_AfterExhaustion()
        {
            // Exhaust the pool
            for (int i = 0; i < 10; i++)
                DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            // Re-initialize
            DomainAssigner.Initialize();

            var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            Assert.AreNotEqual(Domains.Unassigned, domain);
        }

        [Test]
        public void GetDomainsByGameModes_NeverReturnsBlue()
        {
            DomainAssigner.Initialize();

            for (int i = 0; i < 3; i++)
            {
                var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);
                Assert.AreNotEqual(Domains.Blue, domain,
                    "Blue domain should be excluded from the assignment pool.");
            }
        }

        [Test]
        public void GetDomainsByGameModes_WithoutInitialize_ReturnsUnassigned()
        {
            // Clear the pool manually by exhausting it without re-init
            for (int i = 0; i < 10; i++)
                DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            // Don't call Initialize — simulate the missing-init bug
            var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            Assert.AreEqual(Domains.Unassigned, domain,
                "Without Initialize(), exhausted pool should return Unassigned.");
        }

        #region GetBalancedAIDomains tests

        [Test]
        public void GetBalancedAIDomains_ZeroAI_ReturnsEmptyList()
        {
            var humanDomains = new List<Domains> { Domains.Jade };
            var result = DomainAssigner.GetBalancedAIDomains(humanDomains, 0);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetBalancedAIDomains_NoHumans_DistributesEvenly()
        {
            var humanDomains = new List<Domains>();
            var result = DomainAssigner.GetBalancedAIDomains(humanDomains, 6);

            Assert.AreEqual(6, result.Count);

            int jade = result.FindAll(d => d == Domains.Jade).Count;
            int ruby = result.FindAll(d => d == Domains.Ruby).Count;
            int gold = result.FindAll(d => d == Domains.Gold).Count;

            Assert.AreEqual(2, jade, "Expected 2 Jade AI");
            Assert.AreEqual(2, ruby, "Expected 2 Ruby AI");
            Assert.AreEqual(2, gold, "Expected 2 Gold AI");
        }

        [Test]
        public void GetBalancedAIDomains_TwoJadeHumans_BalancesRubyAndGold()
        {
            var humanDomains = new List<Domains> { Domains.Jade, Domains.Jade };
            var result = DomainAssigner.GetBalancedAIDomains(humanDomains, 4);

            Assert.AreEqual(4, result.Count);

            int jade = result.FindAll(d => d == Domains.Jade).Count;
            int ruby = result.FindAll(d => d == Domains.Ruby).Count;
            int gold = result.FindAll(d => d == Domains.Gold).Count;

            // 2 humans on Jade. 4 AI should fill Ruby and Gold first (2 each),
            // then Jade would only get AI if Ruby/Gold catch up.
            Assert.AreEqual(2, ruby, "Expected 2 Ruby AI");
            Assert.AreEqual(2, gold, "Expected 2 Gold AI");
            Assert.AreEqual(0, jade, "Expected 0 Jade AI (already has 2 humans)");
        }

        [Test]
        public void GetBalancedAIDomains_OnePerTeam_BalancesEvenly()
        {
            var humanDomains = new List<Domains> { Domains.Jade, Domains.Ruby, Domains.Gold };
            var result = DomainAssigner.GetBalancedAIDomains(humanDomains, 3);

            Assert.AreEqual(3, result.Count);

            int jade = result.FindAll(d => d == Domains.Jade).Count;
            int ruby = result.FindAll(d => d == Domains.Ruby).Count;
            int gold = result.FindAll(d => d == Domains.Gold).Count;

            Assert.AreEqual(1, jade, "Expected 1 Jade AI");
            Assert.AreEqual(1, ruby, "Expected 1 Ruby AI");
            Assert.AreEqual(1, gold, "Expected 1 Gold AI");
        }

        [Test]
        public void GetBalancedAIDomains_OneHuman_ElevenAI_Balanced()
        {
            var humanDomains = new List<Domains> { Domains.Gold };
            var result = DomainAssigner.GetBalancedAIDomains(humanDomains, 11);

            Assert.AreEqual(11, result.Count);

            int jade = result.FindAll(d => d == Domains.Jade).Count;
            int ruby = result.FindAll(d => d == Domains.Ruby).Count;
            int gold = result.FindAll(d => d == Domains.Gold).Count;

            // 1 human on Gold. AI fills Jade and Ruby first, then Gold.
            // Total per team should be 4/4/4 = 12 total (1 human + 11 AI).
            Assert.AreEqual(4, jade, "Expected 4 Jade AI");
            Assert.AreEqual(4, ruby, "Expected 4 Ruby AI");
            Assert.AreEqual(3, gold, "Expected 3 Gold AI (1 human + 3 AI = 4 total)");
        }

        [Test]
        public void GetBalancedAIDomains_InvalidHumanDomain_Ignored()
        {
            // Unassigned domain from a human is ignored in team counts
            var humanDomains = new List<Domains> { Domains.Unassigned, Domains.Jade };
            var result = DomainAssigner.GetBalancedAIDomains(humanDomains, 3);

            Assert.AreEqual(3, result.Count);

            // Jade has 1 human, Ruby/Gold have 0. AI fills Ruby and Gold first.
            int ruby = result.FindAll(d => d == Domains.Ruby).Count;
            int gold = result.FindAll(d => d == Domains.Gold).Count;

            Assert.IsTrue(ruby >= 1, "Ruby should get at least 1 AI");
            Assert.IsTrue(gold >= 1, "Gold should get at least 1 AI");
        }

        [Test]
        public void GetBalancedAIDomains_NegativeAICount_ReturnsEmpty()
        {
            var humanDomains = new List<Domains> { Domains.Jade };
            var result = DomainAssigner.GetBalancedAIDomains(humanDomains, -1);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void IsPlayableDomain_ValidDomains()
        {
            Assert.IsTrue(DomainAssigner.IsPlayableDomain(Domains.Jade));
            Assert.IsTrue(DomainAssigner.IsPlayableDomain(Domains.Ruby));
            Assert.IsTrue(DomainAssigner.IsPlayableDomain(Domains.Gold));
            Assert.IsFalse(DomainAssigner.IsPlayableDomain(Domains.None));
            Assert.IsFalse(DomainAssigner.IsPlayableDomain(Domains.Unassigned));
            Assert.IsFalse(DomainAssigner.IsPlayableDomain(Domains.Blue));
        }

        #endregion
    }
}
