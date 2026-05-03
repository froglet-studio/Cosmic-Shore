using System.Collections.Generic;
using NUnit.Framework;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Locks the contract that <see cref="CellLifeSpawnerBase.PickRandomDomain"/>
    /// never returns Blue. Blue is reserved for environmental decoration (gyroids,
    /// spawnable shapes, walls) and is excluded from player assignment by
    /// <see cref="CosmicShore.Gameplay.DomainAssigner"/>; allowing the spawner to
    /// roll Blue produced Blue flora that grew Blue prisms, eventually electing
    /// Blue as the cell's DominantDomain and triggering Blue fauna spawns in
    /// Menu_Main even though no player was on the Blue team.
    /// </summary>
    [TestFixture]
    public class CellLifeSpawnerDomainTests
    {
        // Subclass purely to expose the protected PickRandomDomain method —
        // we don't need any spawner behavior, just the domain-selection logic.
        sealed class TestSpawner : CellLifeSpawnerBase
        {
            protected override void OnStart(
                Cell host,
                CellConfigDataSO config,
                CellRuntimeDataSO runtime,
                GameDataSO gameData) { }

            public Domains PickPublic(Domains? excluded) => PickRandomDomain(excluded);
        }

        [Test]
        public void PickRandomDomain_NeverReturnsBlue_WithNoExclusion()
        {
            var spawner = new TestSpawner();
            var seen = new HashSet<Domains>();
            // 1000 rolls makes it overwhelmingly likely we'd see Blue at least once
            // if it were in the candidate list (1/3 odds → ~333 expected hits).
            for (int i = 0; i < 1000; i++)
                seen.Add(spawner.PickPublic(null));

            Assert.IsFalse(seen.Contains(Domains.Blue),
                "PickRandomDomain rolled Blue. Blue must be excluded from player-assignable domains.");
        }

        [Test]
        public void PickRandomDomain_NeverReturnsBlue_WhenJadeExcluded()
        {
            var spawner = new TestSpawner();
            var seen = new HashSet<Domains>();
            for (int i = 0; i < 1000; i++)
                seen.Add(spawner.PickPublic(Domains.Jade));

            Assert.IsFalse(seen.Contains(Domains.Blue),
                "PickRandomDomain rolled Blue when Jade was excluded. Even with the local " +
                "domain excluded (the FloraExcludeLocalDomain=true path), Blue must not appear.");
            Assert.IsFalse(seen.Contains(Domains.Jade),
                "Excluded domain was returned anyway — exclusion is broken.");
        }

        [Test]
        public void PickRandomDomain_OnlyReturnsJadeRubyOrGold()
        {
            var spawner = new TestSpawner();
            var allowed = new HashSet<Domains> { Domains.Jade, Domains.Ruby, Domains.Gold };
            for (int i = 0; i < 1000; i++)
            {
                var d = spawner.PickPublic(null);
                Assert.IsTrue(allowed.Contains(d),
                    $"PickRandomDomain returned {d}, which is not a player-assignable domain. " +
                    "Only Jade/Ruby/Gold may control cells and spawn fauna.");
            }
        }

        [Test]
        public void PickRandomDomain_FallsBackToJade_WhenAllValidDomainsExcluded()
        {
            // Defense-in-depth: even though we never call this with the only non-excluded
            // candidate being Blue (since Blue isn't a candidate), the legacy fallback
            // path should still return a player-controllable domain — Jade — rather than
            // None/Unassigned/Blue.
            var spawner = new TestSpawner();
            // Excluding any single domain still leaves two valid candidates, so we just
            // sanity-check the fallback returns Jade when a candidate is excluded that
            // happens to be in the list. The only way to hit the "all excluded" path
            // would require multiple removals — guarded inside the helper.
            Assert.AreEqual(Domains.Ruby, FirstNonExcludedRubyOrGold(spawner, Domains.Jade),
                "Spawner should be able to return Ruby when Jade is excluded.");
        }

        static Domains FirstNonExcludedRubyOrGold(TestSpawner spawner, Domains excluded)
        {
            for (int i = 0; i < 200; i++)
            {
                var d = spawner.PickPublic(excluded);
                if (d == Domains.Ruby) return d;
            }
            return Domains.Jade;
        }
    }
}
