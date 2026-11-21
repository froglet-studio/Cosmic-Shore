using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    /// <summary>
    /// The pure statistics behind <c>diag</c>'s per-system timings (<see cref="MarkerBudget"/>).
    /// The recorder half needs a running player and is not covered here.
    /// </summary>
    public class MarkerBudgetTests
    {
        // ── marker list ────────────────────────────────────────────────────

        [Test]
        public void ResolveMarkers_KeepsDefaultsInOrder_ThenAppendsExtras()
        {
            var list = MarkerBudget.ResolveMarkers(new[] { "Custom.A", "Custom.B" });

            Assert.AreEqual(MarkerBudget.DefaultMarkers.Length + 2, list.Count);
            for (int i = 0; i < MarkerBudget.DefaultMarkers.Length; i++)
                Assert.AreEqual(MarkerBudget.DefaultMarkers[i], list[i]);
            Assert.AreEqual("Custom.A", list[list.Count - 2]);
            Assert.AreEqual("Custom.B", list[list.Count - 1]);
        }

        [Test]
        public void ResolveMarkers_DropsDuplicatesAndBlanks_CaseSensitively()
        {
            var list = MarkerBudget.ResolveMarkers(new[] { "PlayerLoop", " ", null, "playerloop", "X", "X" });

            Assert.AreEqual(1, list.FindAll(m => m == "PlayerLoop").Count);
            Assert.Contains("playerloop", list, "Profiler marker names are case-sensitive, so this is a different marker");
            Assert.AreEqual(1, list.FindAll(m => m == "X").Count);
            Assert.IsFalse(list.Contains(" ") || list.Contains(null));
        }

        [Test]
        public void ResolveMarkers_NoExtras_IsTheDefaults()
        {
            CollectionAssert.AreEqual(MarkerBudget.DefaultMarkers, MarkerBudget.ResolveMarkers(null));
        }

        [Test]
        public void DefaultMarkers_HaveNoDuplicates()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in MarkerBudget.DefaultMarkers)
                Assert.IsTrue(seen.Add(m), $"'{m}' is listed twice");
        }

        [TestCase("m=A,B", new[] { "A", "B" })]
        [TestCase("M= A , ,B ", new[] { "A", "B" })]
        [TestCase("m=Fauna.BodySync", new[] { "Fauna.BodySync" })]
        [TestCase("m=", new string[0])]
        public void TryParseExtraMarkers_ReadsCommaSeparatedNames(string arg, string[] expected)
        {
            Assert.IsTrue(MarkerBudget.TryParseExtraMarkers(arg, out var names));
            CollectionAssert.AreEqual(expected, names);
        }

        [TestCase("S5_Wildlife")]
        [TestCase("15")]
        [TestCase("")]
        [TestCase(null)]
        public void TryParseExtraMarkers_LeavesLabelsAndDurationsAlone(string arg)
        {
            Assert.IsFalse(MarkerBudget.TryParseExtraMarkers(arg, out _));
        }

        // ── percentiles ────────────────────────────────────────────────────

        [Test]
        public void Percentile_MatchesTheRuleDiagUsesForItsP99()
        {
            // diag computes p99 as sorted[Mathf.RoundToInt(0.99 * (n - 1))]; Mathf.RoundToInt is
            // (int)Math.Round(x), i.e. half to EVEN. Every percentile must use that one rule.
            foreach (float p in new[] { 0.5f, 0.95f, 0.99f })
                for (int n = 1; n <= 60; n++)
                {
                    var sorted = new List<float>();
                    for (int i = 0; i < n; i++) sorted.Add(i);
                    int expected = (int)Math.Round(p * (n - 1));
                    Assert.AreEqual(expected, MarkerBudget.Percentile(sorted, p), $"p={p} n={n}");
                }
        }

        [Test]
        public void Percentile_EmptyIsZero_AndPIsClamped()
        {
            Assert.AreEqual(0f, MarkerBudget.Percentile(new List<float>(), 0.5f));
            Assert.AreEqual(0f, MarkerBudget.Percentile(null, 0.5f));
            var sorted = new List<float> { 1f, 2f, 3f };
            Assert.AreEqual(1f, MarkerBudget.Percentile(sorted, -1f));
            Assert.AreEqual(3f, MarkerBudget.Percentile(sorted, 2f));
        }

        // ── one marker's row ───────────────────────────────────────────────

        [Test]
        public void Summarize_AveragesOverTheWholeRun_NotOverTheFramesItRanIn()
        {
            // Ran on 2 frames of 4 at 10 ms: its per-frame cost to the run is 5 ms, and it was
            // present half the time. Averaging over its own frames would say 10 ms and hide that.
            var stat = MarkerBudget.Summarize("M", "Scripts", true,
                new List<float> { 10f, 0f, 10f, 0f }, new List<float> { 2f, 0f, 2f, 0f }, 4);

            Assert.AreEqual(5f, stat.avgMs, 1e-5f);
            Assert.AreEqual(50f, stat.presentPct, 1e-4f);
            Assert.AreEqual(1f, stat.avgCalls, 1e-5f);
            Assert.AreEqual(10f, stat.maxMs, 1e-5f);
            Assert.AreEqual(4, stat.runFrames);
            Assert.AreEqual(4, stat.framesWithData);
        }

        [Test]
        public void Summarize_FramesTheRecorderNeverSaw_CountAsZero()
        {
            // A recorder holds data for 2 frames of a 4-frame run.
            var stat = MarkerBudget.Summarize("M", "Scripts", true,
                new List<float> { 8f, 4f }, new List<float> { 1f, 1f }, 4);

            Assert.AreEqual(3f, stat.avgMs, 1e-5f);
            Assert.AreEqual(50f, stat.presentPct, 1e-4f);
            Assert.AreEqual(0.5f, stat.avgCalls, 1e-5f);
            Assert.AreEqual(2, stat.framesWithData);
            Assert.AreEqual(4, stat.runFrames);
            // sorted [0, 0, 4, 8]: p50 = sorted[round(1.5)] = sorted[2] (half to even) = 4
            Assert.AreEqual(4f, stat.p50Ms, 1e-5f);
            Assert.AreEqual(8f, stat.p95Ms, 1e-5f);
        }

        [Test]
        public void Summarize_MoreSamplesThanRunFrames_UsesTheSamples()
        {
            // A recorder can see a frame at either edge that the run's own counter did not.
            var stat = MarkerBudget.Summarize("M", "", true,
                new List<float> { 2f, 2f, 2f }, null, 2);

            Assert.AreEqual(3, stat.runFrames);
            Assert.AreEqual(2f, stat.avgMs, 1e-5f);
            Assert.AreEqual(0f, stat.avgCalls, 1e-5f);
        }

        [Test]
        public void Summarize_NegativeValuesAreClampedToZero()
        {
            var stat = MarkerBudget.Summarize("M", "", true, new List<float> { -3f, 3f }, null, 2);
            Assert.AreEqual(1.5f, stat.avgMs, 1e-5f);
            Assert.AreEqual(50f, stat.presentPct, 1e-4f);
        }

        [Test]
        public void Summarize_NotFound_IsAZeroRowThatSaysSo()
        {
            var stat = MarkerBudget.Summarize("Gone.Marker", null, false, null, null, 900);

            Assert.IsFalse(stat.found);
            Assert.AreEqual("", stat.category);
            Assert.AreEqual(0f, stat.avgMs);
            Assert.AreEqual(0f, stat.maxMs);
            Assert.AreEqual(900, stat.runFrames);
        }

        [Test]
        public void Summarize_EmptyRun_IsAllZeros()
        {
            var stat = MarkerBudget.Summarize("M", "", true, new List<float>(), null, 0);
            Assert.AreEqual(0, stat.runFrames);
            Assert.AreEqual(0f, stat.avgMs);
            Assert.AreEqual(0f, stat.p95Ms);
        }

        [Test]
        public void Summarize_CarriesTruncation()
        {
            Assert.IsTrue(MarkerBudget.Summarize("M", "", true, new List<float> { 1f }, null, 1, truncated: true).truncated);
        }

        // ── ordering ───────────────────────────────────────────────────────

        [Test]
        public void SortForReport_FoundFirst_BiggestAverageFirst_TiesByName()
        {
            var stats = new List<MarkerBudget.MarkerStat>
            {
                new MarkerBudget.MarkerStat { name = "Missing", found = false, avgMs = 99f },
                new MarkerBudget.MarkerStat { name = "B", found = true, avgMs = 2f },
                new MarkerBudget.MarkerStat { name = "A", found = true, avgMs = 2f },
                new MarkerBudget.MarkerStat { name = "Big", found = true, avgMs = 7f },
            };

            MarkerBudget.SortForReport(stats);

            CollectionAssert.AreEqual(new[] { "Big", "A", "B", "Missing" },
                stats.ConvertAll(s => s.name));
        }
    }
}
