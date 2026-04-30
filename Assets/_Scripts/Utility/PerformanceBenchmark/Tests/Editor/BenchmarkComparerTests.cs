using System.Collections.Generic;
using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    [TestFixture]
    public class BenchmarkComparerTests
    {
        #region Helpers

        static BenchmarkReport CreateReport(float avgFps, float avgFrameTimeMs, float avgDrawCalls = 100f)
        {
            var report = new BenchmarkReport
            {
                label = "test",
                gitBranch = "main",
                gitCommitHash = "abc123",
                timestamp = "2026-01-01",
                sceneName = "TestScene",
                snapshots = new List<FrameSnapshot>(),
                statistics = new BenchmarkStatistics
                {
                    totalFrames = 100,
                    durationSeconds = 10f,
                    avgFps = avgFps,
                    minFps = avgFps * 0.8f,
                    maxFps = avgFps * 1.2f,
                    medianFps = avgFps,
                    p5Fps = avgFps * 0.85f,
                    p1Fps = avgFps * 0.8f,
                    avgFrameTimeMs = avgFrameTimeMs,
                    minFrameTimeMs = avgFrameTimeMs * 0.8f,
                    maxFrameTimeMs = avgFrameTimeMs * 1.5f,
                    medianFrameTimeMs = avgFrameTimeMs,
                    p95FrameTimeMs = avgFrameTimeMs * 1.3f,
                    p99FrameTimeMs = avgFrameTimeMs * 1.4f,
                    stdDevFrameTimeMs = 2f,
                    avgDrawCalls = avgDrawCalls,
                    avgBatches = avgDrawCalls * 0.5f,
                    avgSetPassCalls = 10f,
                    avgTriangles = 50000f,
                    peakAllocatedMemory = 100_000_000,
                    avgAllocatedMemory = 80_000_000,
                    totalGcAllocated = 5_000_000
                }
            };
            return report;
        }

        #endregion

        #region Compare

        [Test]
        public void Compare_IdenticalReports_AllNeutral()
        {
            var baseline = CreateReport(60f, 16.67f);
            var current = CreateReport(60f, 16.67f);

            var result = BenchmarkComparer.Compare(baseline, current);

            Assert.AreEqual(0, result.regressions);
            Assert.AreEqual(0, result.improvements);
            Assert.AreEqual(result.deltas.Length, result.neutral);
        }

        [Test]
        public void Compare_BetterFps_DetectsImprovement()
        {
            var baseline = CreateReport(30f, 33.33f);
            var current = CreateReport(60f, 16.67f);

            var result = BenchmarkComparer.Compare(baseline, current);

            Assert.Greater(result.improvements, 0);
        }

        [Test]
        public void Compare_WorseFps_DetectsRegression()
        {
            var baseline = CreateReport(60f, 16.67f);
            var current = CreateReport(30f, 33.33f);

            var result = BenchmarkComparer.Compare(baseline, current);

            Assert.Greater(result.regressions, 0);
        }

        [Test]
        public void Compare_DeltaCount_MatchesAllTrackedMetrics()
        {
            var baseline = CreateReport(60f, 16.67f);
            var current = CreateReport(60f, 16.67f);

            var result = BenchmarkComparer.Compare(baseline, current);

            // Should have deltas for: 5 FPS + 5 frame time + 4 rendering + 3 memory = 17
            Assert.AreEqual(17, result.deltas.Length);
        }

        [Test]
        public void Compare_CustomThreshold_AffectsVerdicts()
        {
            // 5% improvement in FPS would be neutral at 10% threshold
            var baseline = CreateReport(60f, 16.67f);
            var current = CreateReport(63f, 15.87f);

            var strictResult = BenchmarkComparer.Compare(baseline, current, neutralThresholdPercent: 1f);
            var lenientResult = BenchmarkComparer.Compare(baseline, current, neutralThresholdPercent: 10f);

            Assert.GreaterOrEqual(lenientResult.neutral, strictResult.neutral);
        }

        [Test]
        public void Compare_SumOfVerdicts_EqualsDeltaCount()
        {
            var baseline = CreateReport(60f, 16.67f);
            var current = CreateReport(55f, 18.18f, avgDrawCalls: 120f);

            var result = BenchmarkComparer.Compare(baseline, current);

            Assert.AreEqual(result.deltas.Length, result.improvements + result.regressions + result.neutral);
        }

        #endregion

        #region FormatAsText

        [Test]
        public void FormatAsText_ReturnsNonEmptyString()
        {
            var baseline = CreateReport(60f, 16.67f);
            var current = CreateReport(55f, 18.18f);
            var result = BenchmarkComparer.Compare(baseline, current);

            string text = BenchmarkComparer.FormatAsText(result);

            Assert.IsNotNull(text);
            Assert.IsNotEmpty(text);
        }

        [Test]
        public void FormatAsText_ContainsHeader()
        {
            var baseline = CreateReport(60f, 16.67f);
            var current = CreateReport(55f, 18.18f);
            var result = BenchmarkComparer.Compare(baseline, current);

            string text = BenchmarkComparer.FormatAsText(result);

            Assert.IsTrue(text.Contains("PERFORMANCE BENCHMARK COMPARISON"));
        }

        [Test]
        public void FormatAsText_ContainsSummaryLine()
        {
            var baseline = CreateReport(60f, 16.67f);
            var current = CreateReport(55f, 18.18f);
            var result = BenchmarkComparer.Compare(baseline, current);

            string text = BenchmarkComparer.FormatAsText(result);

            Assert.IsTrue(text.Contains("Summary:"));
            Assert.IsTrue(text.Contains("improved"));
            Assert.IsTrue(text.Contains("regressed"));
            Assert.IsTrue(text.Contains("unchanged"));
        }

        #endregion
    }
}
