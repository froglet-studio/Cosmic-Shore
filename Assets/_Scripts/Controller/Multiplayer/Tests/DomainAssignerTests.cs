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
        }

        [Test]
        public void GetDomainsByGameModes_ReturnsUniqueDomains()
        {
            var assigned = new HashSet<Domains>();

            // There are 4 valid domains (Jade, Ruby, Blue, Gold) after excluding None and Unassigned.
            for (int i = 0; i < 4; i++)
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
        public void GetDomainsByGameModes_FourPlayers_AllGetUniqueDomains()
        {
            DomainAssigner.Initialize();

            var assigned = new HashSet<Domains>();
            for (int i = 0; i < 4; i++)
            {
                var domain = DomainAssigner.GetDomainsByGameModes(GameModes.HexRace);
                Assert.AreNotEqual(Domains.Unassigned, domain,
                    $"Player {i + 1} of 4 got Unassigned — pool exhausted too early.");
                Assert.IsTrue(assigned.Add(domain),
                    $"Player {i + 1} got duplicate domain {domain}.");
            }

            Assert.AreEqual(4, assigned.Count, "4-player game should use all 4 domains.");
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
    }
}
