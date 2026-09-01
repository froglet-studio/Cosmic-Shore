#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for <see cref="FaunaReproductionRules"/> - the pure gating behind the
    /// prey-linked population pipeline (Docs/ECOSYSTEM.md §6): reproduction converts
    /// feeds into offspring (the population driver), and the periodic spawner only
    /// seeds a species back up to its floor (bootstrap + extinction recovery).
    /// These rules ARE the Lotka–Volterra coupling; pin them.
    /// </summary>
    [TestFixture]
    public class FaunaReproductionRulesTests
    {
        // ---- ShouldBirth: reproduction disabled ----

        [Test]
        public void ShouldBirth_ZeroFeedsPerOffspring_NeverBirths()
        {
            // FeedsPerOffspring <= 0 = species does not reproduce, regardless of feeds.
            Assert.IsFalse(FaunaReproductionRules.ShouldBirth(100, 0, 999f, 0f, 0, 0));
        }

        [Test]
        public void ShouldBirth_NegativeFeedsPerOffspring_NeverBirths()
        {
            Assert.IsFalse(FaunaReproductionRules.ShouldBirth(100, -1, 999f, 0f, 0, 0));
        }

        // ---- ShouldBirth: feed threshold ----

        [Test]
        public void ShouldBirth_BelowFeedThreshold_False()
        {
            Assert.IsFalse(FaunaReproductionRules.ShouldBirth(9, 10, 999f, 0f, 5, 0));
        }

        [Test]
        public void ShouldBirth_AtFeedThreshold_True()
        {
            Assert.IsTrue(FaunaReproductionRules.ShouldBirth(10, 10, 999f, 0f, 5, 0));
        }

        [Test]
        public void ShouldBirth_AboveFeedThreshold_True()
        {
            // Feeds carry over while the cooldown blocks - exceeding the threshold is fine.
            Assert.IsTrue(FaunaReproductionRules.ShouldBirth(25, 10, 999f, 0f, 5, 0));
        }

        // ---- ShouldBirth: cooldown ----

        [Test]
        public void ShouldBirth_WithinCooldown_False()
        {
            Assert.IsFalse(FaunaReproductionRules.ShouldBirth(10, 10, 5.9f, 6f, 5, 0));
        }

        [Test]
        public void ShouldBirth_AtCooldown_True()
        {
            // Block is strict (< cooldown), so exactly cooldown passes.
            Assert.IsTrue(FaunaReproductionRules.ShouldBirth(10, 10, 6f, 6f, 5, 0));
        }

        // ---- ShouldBirth: population cap (performance backstop) ----

        [Test]
        public void ShouldBirth_AtCap_False()
        {
            Assert.IsFalse(FaunaReproductionRules.ShouldBirth(10, 10, 999f, 0f, 60, 60));
        }

        [Test]
        public void ShouldBirth_AboveCap_False()
        {
            // Seeding + a same-frame birth can momentarily exceed the cap; births stay blocked.
            Assert.IsFalse(FaunaReproductionRules.ShouldBirth(10, 10, 999f, 0f, 61, 60));
        }

        [Test]
        public void ShouldBirth_JustBelowCap_True()
        {
            Assert.IsTrue(FaunaReproductionRules.ShouldBirth(10, 10, 999f, 0f, 59, 60));
        }

        [Test]
        public void ShouldBirth_ZeroCap_Uncapped()
        {
            Assert.IsTrue(FaunaReproductionRules.ShouldBirth(10, 10, 999f, 0f, 10000, 0));
        }

        // ---- SeedSpawnCount: deficit-below-floor semantics ----

        [Test]
        public void SeedSpawnCount_EmptyCell_SpawnsFullFloor()
        {
            Assert.AreEqual(25, FaunaReproductionRules.SeedSpawnCount(0, 25, 0));
        }

        [Test]
        public void SeedSpawnCount_PartialPopulation_SpawnsDeficit()
        {
            Assert.AreEqual(7, FaunaReproductionRules.SeedSpawnCount(18, 25, 0));
        }

        [Test]
        public void SeedSpawnCount_AtFloor_SpawnsNothing()
        {
            Assert.AreEqual(0, FaunaReproductionRules.SeedSpawnCount(25, 25, 0));
        }

        [Test]
        public void SeedSpawnCount_AboveFloor_SpawnsNothing()
        {
            // Reproduction has grown the population past the floor - the seeder stays out.
            Assert.AreEqual(0, FaunaReproductionRules.SeedSpawnCount(48, 25, 0));
        }

        [Test]
        public void SeedSpawnCount_CapClampsDeficit()
        {
            // Floor 25, cap 20, live 18: only 2 fit under the cap, not the full 7 deficit.
            Assert.AreEqual(2, FaunaReproductionRules.SeedSpawnCount(18, 25, 20));
        }

        [Test]
        public void SeedSpawnCount_LiveAboveCap_SpawnsNothing()
        {
            Assert.AreEqual(0, FaunaReproductionRules.SeedSpawnCount(30, 25, 20));
        }

        [Test]
        public void SeedSpawnCount_ZeroCap_NoClamp()
        {
            Assert.AreEqual(25, FaunaReproductionRules.SeedSpawnCount(0, 25, 0));
        }

        // ---- WaveSpawnCount: full-wave-every-tick semantics (Brood Rush) ----

        [Test]
        public void WaveSpawnCount_EmptyCell_SpawnsFullWave()
        {
            Assert.AreEqual(6, FaunaReproductionRules.WaveSpawnCount(0, 6, 24));
        }

        [Test]
        public void WaveSpawnCount_PopulatedCell_StillSpawnsFullWave()
        {
            // Unlike the seeder, a live population at/above the floor does NOT
            // suppress the wave - every tick births a fresh brood.
            Assert.AreEqual(6, FaunaReproductionRules.WaveSpawnCount(12, 6, 24));
        }

        [Test]
        public void WaveSpawnCount_CapClampsWave()
        {
            // Cap 24, live 20: only 4 of the 6 fit.
            Assert.AreEqual(4, FaunaReproductionRules.WaveSpawnCount(20, 6, 24));
        }

        [Test]
        public void WaveSpawnCount_AtCap_SpawnsNothing()
        {
            Assert.AreEqual(0, FaunaReproductionRules.WaveSpawnCount(24, 6, 24));
        }

        [Test]
        public void WaveSpawnCount_ZeroCap_NoClamp()
        {
            Assert.AreEqual(6, FaunaReproductionRules.WaveSpawnCount(999, 6, 0));
        }
    }
}
#endif
