using CosmicShore.Data;
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pins the flora population pipeline's gating (Docs/ECOSYSTEM.md §32) - the plant-side
    /// mirror of <see cref="FaunaReproductionRulesTests"/>.
    ///
    /// <para>The property these tests exist to defend is that the model needs <b>no imposed
    /// death</b>: a plant funds a child out of growth it actually did, so a full plant (which has
    /// stopped growing) has stopped reproducing, and the population is bounded by the food web
    /// grazing plants back into growing again - never by a decay clock.</para>
    /// </summary>
    public class FloraReproductionRulesTests
    {
        const float NoCooldown = 0f;
        const float LongAgo = 9999f;

        static bool Seed(
            int growthSinceBirth = 100, int growthPerOffspring = 10,
            float secondsSinceLastBirth = LongAgo, float cooldownSeconds = NoCooldown,
            int livePrisms = 100, int prismBudget = 100, float maturityFraction = 0f,
            int livePopulation = 0, int maxPopulation = 0) =>
            FloraReproductionRules.ShouldSeed(
                growthSinceBirth, growthPerOffspring, secondsSinceLastBirth, cooldownSeconds,
                livePrisms, prismBudget, maturityFraction, livePopulation, maxPopulation);

        // ── Reproduction is opt-in ────────────────────────────────────────────

        [Test]
        public void ZeroGrowthPerOffspring_NeverSeeds()
        {
            // A species that authors no reproduction is driven by the seeder alone - which is
            // what keeps every biome that authors nothing bit-for-bit unchanged.
            Assert.IsFalse(Seed(growthPerOffspring: 0));
            Assert.IsFalse(Seed(growthPerOffspring: -1));
        }

        // ── Growth is the currency ────────────────────────────────────────────

        [Test]
        public void BelowGrowthQuota_DoesNotSeed()
        {
            Assert.IsFalse(Seed(growthSinceBirth: 9, growthPerOffspring: 10));
        }

        [Test]
        public void AtGrowthQuota_Seeds()
        {
            Assert.IsTrue(Seed(growthSinceBirth: 10, growthPerOffspring: 10));
        }

        // ── Throttles ─────────────────────────────────────────────────────────

        [Test]
        public void WithinCooldown_DoesNotSeed()
        {
            Assert.IsFalse(Seed(secondsSinceLastBirth: 4f, cooldownSeconds: 5f));
            Assert.IsTrue(Seed(secondsSinceLastBirth: 5f, cooldownSeconds: 5f));
        }

        [Test]
        public void AtOrOverCap_DoesNotSeed()
        {
            Assert.IsFalse(Seed(livePopulation: 12, maxPopulation: 12));
            Assert.IsFalse(Seed(livePopulation: 13, maxPopulation: 12));
            Assert.IsTrue(Seed(livePopulation: 11, maxPopulation: 12));
        }

        [Test]
        public void ZeroCap_IsUncapped()
        {
            Assert.IsTrue(Seed(livePopulation: 10_000, maxPopulation: 0));
        }

        // ── The maturity gate ─────────────────────────────────────────────────

        [Test]
        public void GrazedStub_WithQuotaBanked_IsHeldBackByMaturity()
        {
            // The case a pure growth quota cannot see: the plant has grown its quota across
            // several graze-and-regrow cycles but is currently a stub.
            Assert.IsFalse(Seed(livePrisms: 5, prismBudget: 27, maturityFraction: 0.5f));
            Assert.IsTrue(Seed(livePrisms: 14, prismBudget: 27, maturityFraction: 0.5f));
        }

        [Test]
        public void MaturityGate_OffByDefault()
        {
            Assert.IsTrue(Seed(livePrisms: 1, prismBudget: 27, maturityFraction: 0f));
        }

        [Test]
        public void MaturityGate_IgnoredWhenFamilyReportsNoBudget()
        {
            // PrismBudget 0 means "this flora family has no budget", not "the plant is empty".
            Assert.IsTrue(Seed(livePrisms: 0, prismBudget: 0, maturityFraction: 1f));
        }

        [Test]
        public void FullPlant_AtFullMaturityRequirement_Seeds()
        {
            // The gyroid's authored shape: a plant that completed its unit cell colonises.
            Assert.IsTrue(Seed(
                growthSinceBirth: 27, growthPerOffspring: 27,
                livePrisms: 27, prismBudget: 27, maturityFraction: 1f));
        }

        // ── The seeder is a floor, not a driver ───────────────────────────────

        [Test]
        public void SeedSpawnCount_FillsDeficitBelowFloor()
        {
            Assert.AreEqual(4, FloraReproductionRules.SeedSpawnCount(0, 4, 0));
            Assert.AreEqual(1, FloraReproductionRules.SeedSpawnCount(3, 4, 0));
        }

        [Test]
        public void SeedSpawnCount_IsZeroAtOrAboveFloor()
        {
            Assert.AreEqual(0, FloraReproductionRules.SeedSpawnCount(4, 4, 0));
            // Reproduction has carried the species past its floor - the seeder stays out.
            Assert.AreEqual(0, FloraReproductionRules.SeedSpawnCount(40, 4, 0));
        }

        [Test]
        public void SeedSpawnCount_NeverExceedsCap()
        {
            Assert.AreEqual(2, FloraReproductionRules.SeedSpawnCount(8, 20, 10));
            Assert.AreEqual(0, FloraReproductionRules.SeedSpawnCount(10, 20, 10));
            // Over cap already (a lowered scale) - seeding stops, nothing is culled.
            Assert.AreEqual(0, FloraReproductionRules.SeedSpawnCount(15, 20, 10));
        }

        // ── Opt-in switch ─────────────────────────────────────────────────────

        [Test]
        public void HasPopulationModel_IsOptIn()
        {
            Assert.IsFalse(FloraReproductionRules.HasPopulationModel(0));
            Assert.IsTrue(FloraReproductionRules.HasPopulationModel(1));
        }

        // ── THE TIME LAW ──────────────────────────────────────────────────────
        //
        // Time breeds faster, the other three a little slower. Pinned here because the law
        // lives in CODE rather than in the assets - the quota is authored per config while
        // the element is ROLLED per plant, so there is no asset a --check could defend it in.

        [Test]
        public void TimeReproducesFasterThanEveryOtherElement()
        {
            float time = FloraReproductionRules.ReproductionRateFor(Element.Time);
            Assert.Greater(time, 1f, "Time must breed faster than the fleet.");

            foreach (var other in new[] { Element.Charge, Element.Mass, Element.Space })
            {
                float rate = FloraReproductionRules.ReproductionRateFor(other);
                Assert.Less(rate, 1f, $"{other} must breed slower than the fleet.");
                Assert.Less(rate, time, $"{other} must breed slower than Time.");
            }
        }

        [Test]
        public void EveryNonTimeElementSharesOneRate()
        {
            // "Everyone else" is ONE rate, not three - the law is a Time law, not a per-element
            // ladder, and three drifting constants is how it would become one.
            Assert.AreEqual(FloraReproductionRules.ReproductionRateFor(Element.Charge),
                            FloraReproductionRules.ReproductionRateFor(Element.Mass));
            Assert.AreEqual(FloraReproductionRules.ReproductionRateFor(Element.Charge),
                            FloraReproductionRules.ReproductionRateFor(Element.Space));
        }

        [Test]
        public void ElementlessPlantKeepsTheFleetRate()
        {
            // None means "no crystal resolved yet", not "a fourth element" - it must not
            // silently inherit the penalty.
            Assert.AreEqual(1f, FloraReproductionRules.ReproductionRateFor(Element.None));
            Assert.AreEqual(1f, FloraReproductionRules.ReproductionRateFor(Element.Omni));
        }

        [Test]
        public void RateIsAppliedAsACostPerChild_SoFasterMeansCheaper()
        {
            // The quota (prisms per child) and the colony cycle (seconds per child) are the
            // same quantity in different units, which is why ONE constant drives both.
            int fleetQuota = 100;
            int timeQuota = FloraReproductionRules.ScaleGrowthQuota(
                fleetQuota, FloraReproductionRules.ReproductionRateFor(Element.Time));
            int massQuota = FloraReproductionRules.ScaleGrowthQuota(
                fleetQuota, FloraReproductionRules.ReproductionRateFor(Element.Mass));

            Assert.Less(timeQuota, fleetQuota);
            Assert.Greater(massQuota, fleetQuota);

            float fleetPeriod = 30f;
            Assert.Less(FloraReproductionRules.ScaleCostPerChild(
                fleetPeriod, FloraReproductionRules.ReproductionRateFor(Element.Time)), fleetPeriod);
            Assert.Greater(FloraReproductionRules.ScaleCostPerChild(
                fleetPeriod, FloraReproductionRules.ReproductionRateFor(Element.Mass)), fleetPeriod);
        }

        [Test]
        public void ScalingNeverTurnsReproductionOnOrOff()
        {
            // 0 is the species saying "I do not reproduce" (56 of 85 flora configs). No element
            // may scale a species into breeding...
            foreach (var element in new[] { Element.Time, Element.Charge, Element.Mass, Element.Space })
            {
                float rate = FloraReproductionRules.ReproductionRateFor(element);
                Assert.AreEqual(0, FloraReproductionRules.ScaleGrowthQuota(0, rate));
                Assert.AreEqual(0f, FloraReproductionRules.ScaleCostPerChild(0f, rate));
            }

            // ...nor out of it: a small quota scaled by the Time rate must never floor to 0,
            // which ShouldSeed would read as "does not reproduce".
            Assert.GreaterOrEqual(
                FloraReproductionRules.ScaleGrowthQuota(
                    1, FloraReproductionRules.ReproductionRateFor(Element.Time)), 1);
        }

        [Test]
        public void ScaledQuotaStillGatesSeeding()
        {
            // The scaled quota is what ShouldSeed actually spends (Flora.TryReproduce), so a
            // Time plant seeds on growth a fleet plant could not yet afford.
            int authored = 100;
            int timeQuota = FloraReproductionRules.ScaleGrowthQuota(
                authored, FloraReproductionRules.ReproductionRateFor(Element.Time));

            Assert.IsTrue(Seed(growthSinceBirth: timeQuota, growthPerOffspring: timeQuota));
            Assert.IsFalse(Seed(growthSinceBirth: timeQuota, growthPerOffspring: authored));
        }
    }
}
