using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    [TestFixture]
    public class BenchmarkAnalysisTests
    {
        static BenchmarkReport ReportWith(BenchmarkStatistics stats, List<SpikeEntry> spikes = null)
        {
            return new BenchmarkReport { statistics = stats, spikes = spikes ?? new List<SpikeEntry>() };
        }

        // ── Grade ───────────────────────────────────────

        [Test]
        public void Grade_ZeroFrames_ReportsNoData()
        {
            string grade = BenchmarkGrade.Evaluate(new BenchmarkStatistics(), out string explanation);
            Assert.AreEqual("-", grade);
            Assert.AreEqual("No data captured", explanation);
        }

        [Test]
        public void Grade_SmoothRun_IsA()
        {
            var stats = new BenchmarkStatistics { totalFrames = 100, avgFps = 60, p99FrameTimeMs = 18, stdDevFrameTimeMs = 2 };
            Assert.AreEqual("A", BenchmarkGrade.Evaluate(stats));
        }

        // ── Score ───────────────────────────────────────

        [Test]
        public void Score_ZeroFrames_IsZero()
        {
            Assert.AreEqual(0, BenchmarkAnalysis.ComputeScore(new BenchmarkStatistics()));
        }

        [Test]
        public void Score_IsClampedToRange()
        {
            var great = new BenchmarkStatistics { totalFrames = 100, avgFps = 120, p99FrameTimeMs = 8, stdDevFrameTimeMs = 1 };
            var awful = new BenchmarkStatistics { totalFrames = 100, avgFps = 5, p99FrameTimeMs = 200, stdDevFrameTimeMs = 80, totalGcAllocated = 100_000_000 };

            int g = BenchmarkAnalysis.ComputeScore(great);
            int a = BenchmarkAnalysis.ComputeScore(awful);

            Assert.GreaterOrEqual(g, 0);
            Assert.LessOrEqual(g, 100);
            Assert.GreaterOrEqual(a, 0);
            Assert.LessOrEqual(a, 100);
            Assert.Greater(g, a);
        }

        // ── Hint engine (default rules) ─────────────────

        [Test]
        public void Analyze_SteepMemorySlope_FiresLeakBlocker()
        {
            var stats = new BenchmarkStatistics
            {
                totalFrames = 100,
                avgFps = 60,
                memorySlopeBytesPerFrame = 20 * 1024 // 20 KB/frame, well over the 8 KB default
            };

            var result = BenchmarkAnalysis.Analyze(ReportWith(stats), null);

            Assert.IsTrue(result.isBlocked, "a steep memory slope should flag the run as blocked");
            Assert.IsTrue(result.hints.Any(h => h.id == "memory-leak" && h.severity == HintSeverity.Blocker));
            // Actionable: the leak hint must carry fix advice.
            var leak = result.hints.First(h => h.id == "memory-leak");
            Assert.IsNotEmpty(leak.fixAdvice);
        }

        [Test]
        public void Analyze_HighGcPerFrame_FiresGcPressure()
        {
            var stats = new BenchmarkStatistics
            {
                totalFrames = 10,
                avgFps = 60,
                totalGcAllocated = 10L * 8 * 1024 // ≈ 8 KB/frame, over the 4 KB default
            };

            var result = BenchmarkAnalysis.Analyze(ReportWith(stats), null);

            Assert.IsTrue(result.hints.Any(h => h.id == "gc-pressure"));
        }

        [Test]
        public void Analyze_SpikeMarkerMatch_NamesTheMarker()
        {
            var stats = new BenchmarkStatistics { totalFrames = 50, avgFps = 55 };
            var spikes = new List<SpikeEntry>
            {
                new SpikeEntry
                {
                    frameIndex = 3, frameTimeMs = 80,
                    topMarkers = new List<MarkerSample> { new MarkerSample { name = "GC.Collect", ms = 40f } }
                }
            };

            var result = BenchmarkAnalysis.Analyze(ReportWith(stats, spikes), null);

            var hint = result.hints.FirstOrDefault(h => h.id == "gc-collect-spike");
            Assert.IsNotNull(hint, "a GC.Collect spike marker should trigger the matching rule");
            Assert.AreEqual("GC.Collect", hint.matchedMarker);
        }

        [Test]
        public void Analyze_HighNetcodeShare_FiresHint()
        {
            var stats = new BenchmarkStatistics
            {
                totalFrames = 100, avgFps = 60, avgFrameTimeMs = 16.6f,
                avgNetcodeTimeMs = 5f, netcodeSharePercent = 30f
            };

            var result = BenchmarkAnalysis.Analyze(ReportWith(stats), null);

            Assert.IsTrue(result.hints.Any(h => h.id == "netcode-share"));
        }

        [Test]
        public void Analyze_CleanRun_NoBlockers()
        {
            var stats = new BenchmarkStatistics
            {
                totalFrames = 100, avgFps = 60, p99FrameTimeMs = 18, stdDevFrameTimeMs = 2,
                avgDrawCalls = 200, memorySlopeBytesPerFrame = 0, totalGcAllocated = 0
            };

            var result = BenchmarkAnalysis.Analyze(ReportWith(stats), null);

            Assert.IsFalse(result.isBlocked);
        }
    }
}
