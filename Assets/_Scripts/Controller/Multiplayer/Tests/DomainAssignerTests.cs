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
            // After Initialize, the first call should return a valid domain — never
            // the Blue sentinel (Blue is the "no team / unassigned / neutral" sentinel
            // and is excluded from the assignment pool).
            var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            Assert.AreNotEqual(Domains.Blue, domain);
        }

        [Test]
        public void GetDomainsByGameModes_ReturnsUniqueDomains()
        {
            var assigned = new HashSet<Domains>();

            // There are 4 valid domains (Jade, Ruby, Gold, Amethyst) after excluding Blue.
            for (int i = 0; i < 4; i++)
            {
                var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);
                Assert.IsTrue(assigned.Add(domain),
                    $"Domain {domain} was assigned twice. Assigned so far: {string.Join(", ", assigned)}");
            }
        }

        [Test]
        public void GetDomainsByGameModes_EmptyPool_ReturnsBlue()
        {
            // Exhaust the pool
            for (int i = 0; i < 10; i++)
                DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            Assert.AreEqual(Domains.Blue, domain);
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

            Assert.AreNotEqual(Domains.Blue, domain);
        }

        [Test]
        public void GetDomainsByGameModes_NeverReturnsBlueWhilePoolHasItems()
        {
            DomainAssigner.Initialize();

            for (int i = 0; i < 4; i++)
            {
                var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);
                Assert.AreNotEqual(Domains.Blue, domain,
                    "Blue is the no-team sentinel and must be excluded from the assignment pool.");
            }
        }

        [Test]
        public void GetDomainsByGameModes_WithoutInitialize_ReturnsBlue()
        {
            // Clear the pool manually by exhausting it without re-init
            for (int i = 0; i < 10; i++)
                DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            // Don't call Initialize — simulate the missing-init bug
            var domain = DomainAssigner.GetDomainsByGameModes(GameModes.MultiplayerFreestyle);

            Assert.AreEqual(Domains.Blue, domain,
                "Without Initialize(), exhausted pool should return Blue (the sentinel).");
        }
    }
}
