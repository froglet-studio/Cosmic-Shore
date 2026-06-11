using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    [TestFixture]
    public class MetricDeltaTests
    {
        #region Verdict Classification

        [Test]
        public void Constructor_HigherIsWorse_IncreaseIsRegression()
        {
            var delta = new MetricDelta("Frame Time", 16f, 20f, higherIsWorse: true);

            Assert.AreEqual(MetricDelta.Verdict.Regressed, delta.verdict);
            Assert.AreEqual(4f, delta.absoluteDelta, 0.001f);
            Assert.Greater(delta.percentDelta, 0f);
        }

        [Test]
        public void Constructor_HigherIsWorse_DecreaseIsImprovement()
        {
            var delta = new MetricDelta("Frame Time", 20f, 16f, higherIsWorse: true);

            Assert.AreEqual(MetricDelta.Verdict.Improved, delta.verdict);
            Assert.AreEqual(-4f, delta.absoluteDelta, 0.001f);
            Assert.Less(delta.percentDelta, 0f);
        }

        [Test]
        public void Constructor_HigherIsBetter_IncreaseIsImprovement()
        {
            var delta = new MetricDelta("FPS", 30f, 60f, higherIsWorse: false);

            Assert.AreEqual(MetricDelta.Verdict.Improved, delta.verdict);
            Assert.AreEqual(30f, delta.absoluteDelta, 0.001f);
        }

        [Test]
        public void Constructor_HigherIsBetter_DecreaseIsRegression()
        {
            var delta = new MetricDelta("FPS", 60f, 30f, higherIsWorse: false);

            Assert.AreEqual(MetricDelta.Verdict.Regressed, delta.verdict);
            Assert.AreEqual(-30f, delta.absoluteDelta, 0.001f);
        }

        #endregion

        #region Neutral Threshold

        [Test]
        public void Constructor_WithinNeutralThreshold_IsNeutral()
        {
            // 1% change with 2% threshold
            var delta = new MetricDelta("Frame Time", 100f, 101f, higherIsWorse: true, neutralThresholdPercent: 2f);

            Assert.AreEqual(MetricDelta.Verdict.Neutral, delta.verdict);
        }

        [Test]
        public void Constructor_ExactlyAtThreshold_IsNeutral()
        {
            // 2% change with 2% threshold
            var delta = new MetricDelta("Frame Time", 100f, 102f, higherIsWorse: true, neutralThresholdPercent: 2f);

            Assert.AreEqual(MetricDelta.Verdict.Neutral, delta.verdict);
        }

        [Test]
        public void Constructor_BeyondThreshold_IsNotNeutral()
        {
            // 3% change with 2% threshold
            var delta = new MetricDelta("Frame Time", 100f, 103f, higherIsWorse: true, neutralThresholdPercent: 2f);

            Assert.AreEqual(MetricDelta.Verdict.Regressed, delta.verdict);
        }

        [Test]
        public void Constructor_CustomThreshold_RespectedCorrectly()
        {
            // 4% change with 5% threshold => neutral
            var delta = new MetricDelta("Frame Time", 100f, 104f, higherIsWorse: true, neutralThresholdPercent: 5f);

            Assert.AreEqual(MetricDelta.Verdict.Neutral, delta.verdict);
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Constructor_ZeroBaseline_PercentDeltaIsZero()
        {
            var delta = new MetricDelta("Metric", 0f, 10f, higherIsWorse: true);

            Assert.AreEqual(0f, delta.percentDelta, 0.001f);
        }

        [Test]
        public void Constructor_IdenticalValues_IsNeutral()
        {
            var delta = new MetricDelta("Metric", 50f, 50f, higherIsWorse: true);

            Assert.AreEqual(MetricDelta.Verdict.Neutral, delta.verdict);
            Assert.AreEqual(0f, delta.absoluteDelta, 0.001f);
            Assert.AreEqual(0f, delta.percentDelta, 0.001f);
        }

        [Test]
        public void Constructor_PercentDelta_CalculatedCorrectly()
        {
            var delta = new MetricDelta("Frame Time", 20f, 25f, higherIsWorse: true);

            // (25-20)/|20| * 100 = 25%
            Assert.AreEqual(25f, delta.percentDelta, 0.01f);
        }

        #endregion
    }
}
