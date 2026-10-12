using System.Linq;
using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    /// <summary>
    /// Holds Load Time Insights' attribution rule (<c>LoadInsights.ComputeAttribution</c>, owner
    /// picked by <c>LoadInsights.Claimant</c>): every millisecond goes to exactly one span, the
    /// latest-started open span wins, and an ENVELOPE span (a hold or a fixed dwell other work runs
    /// inside) only gets the time nothing else claims, whenever the other spans started. The
    /// connecting panel's dwell and its arena-ready hold are envelopes, so a streamed lay that began
    /// before them keeps its own time; a sweep's "Top cost" column reads the result.
    /// </summary>
    [TestFixture]
    public class LoadInsightsAttributionTests
    {
        [Test]
        public void Envelopes_DoNotTakeTimeFromALayAlreadyInFlight()
        {
            // The heavy-cell shape: the dwell opens at OnClientReady (t0) and the environment lay
            // starts after it and outlives it; the hold opens when the dwell ends; the settle span
            // runs after the lay; the gate then holds its stable window.
            var report = Report(11_000f,
                Span(0, LoadInsightCategory.ScriptedDelay, "dwell", 0f, 4_000f, isWait: true, envelope: true),
                Span(1, LoadInsightCategory.Environment, "lay", 750f, 10_000f),
                Span(2, LoadInsightCategory.GameFlow, "hold", 4_000f, 11_000f, envelope: true),
                Span(3, LoadInsightCategory.Environment, "settle", 10_000f, 10_500f));

            LoadInsights.ComputeAttribution(report);

            Assert.That(report.spans[1].exclusiveMs, Is.EqualTo(9_250f).Within(0.5f),
                "the lay keeps everything from its start to its end, across both envelopes");
            Assert.That(report.spans[0].exclusiveMs, Is.EqualTo(750f).Within(0.5f),
                "the dwell keeps only its idle part, before the lay began");
            Assert.That(report.spans[3].exclusiveMs, Is.EqualTo(500f).Within(0.5f));
            Assert.That(report.spans[2].exclusiveMs, Is.EqualTo(500f).Within(0.5f),
                "the hold keeps only the gate's own tail");
            Assert.That(report.waitMs, Is.EqualTo(750f).Within(0.5f),
                "only the idle part of the dwell is a scripted wait");
            Assert.That(report.unattributedMs, Is.EqualTo(0f).Within(0.5f));
            Assert.That(Slice(report, LoadInsightCategory.Environment), Is.EqualTo(9_750f).Within(0.5f));
            Assert.That(Slice(report, LoadInsightCategory.ScriptedDelay), Is.EqualTo(750f).Within(0.5f));
        }

        [Test]
        public void ALayThatStartedBeforeTheDwell_KeepsTheDwellsTime()
        {
            // A scene-Start lay (a SegmentSpawner environment) runs before OnClientReady: the
            // dwell opens on top of it and must not book that build time as a scripted wait.
            var report = Report(8_000f,
                Span(0, LoadInsightCategory.Environment, "lay", 0f, 7_000f),
                Span(1, LoadInsightCategory.ScriptedDelay, "dwell", 1_000f, 5_000f, isWait: true, envelope: true),
                Span(2, LoadInsightCategory.GameFlow, "hold", 5_000f, 8_000f, envelope: true));

            LoadInsights.ComputeAttribution(report);

            Assert.That(report.spans[0].exclusiveMs, Is.EqualTo(7_000f).Within(0.5f));
            Assert.That(report.spans[1].exclusiveMs, Is.EqualTo(0f).Within(0.5f));
            Assert.That(report.spans[2].exclusiveMs, Is.EqualTo(1_000f).Within(0.5f));
            Assert.That(report.waitMs, Is.EqualTo(0f).Within(0.5f), "no wait while the build was running");
        }

        [Test]
        public void PlainSpans_InnerAndLaterStartedStillWin()
        {
            // The rule for ordinary spans is unchanged: nested → the inner one; overlapping async
            // flows → the later-started one.
            var report = Report(5_000f,
                Span(0, LoadInsightCategory.SceneLoad, "outer", 0f, 5_000f),
                Span(1, LoadInsightCategory.Netcode, "inner", 1_000f, 2_000f),
                Span(2, LoadInsightCategory.Fauna, "early async", 2_500f, 4_500f),
                Span(3, LoadInsightCategory.Flora, "late async", 3_000f, 4_000f));

            LoadInsights.ComputeAttribution(report);

            Assert.That(report.spans[0].exclusiveMs, Is.EqualTo(2_000f).Within(0.5f));
            Assert.That(report.spans[1].exclusiveMs, Is.EqualTo(1_000f).Within(0.5f));
            Assert.That(report.spans[2].exclusiveMs, Is.EqualTo(1_000f).Within(0.5f));
            Assert.That(report.spans[3].exclusiveMs, Is.EqualTo(1_000f).Within(0.5f));
        }

        [Test]
        public void EnvelopesAlone_LatestStartedWins_AndEveryMillisecondIsCounted()
        {
            var report = Report(7_000f,
                Span(0, LoadInsightCategory.GameFlow, "outer hold", 0f, 6_000f, envelope: true),
                Span(1, LoadInsightCategory.ScriptedDelay, "inner dwell", 1_000f, 3_000f, isWait: true, envelope: true));

            LoadInsights.ComputeAttribution(report);

            Assert.That(report.spans[0].exclusiveMs, Is.EqualTo(4_000f).Within(0.5f));
            Assert.That(report.spans[1].exclusiveMs, Is.EqualTo(2_000f).Within(0.5f));
            Assert.That(report.unattributedMs, Is.EqualTo(1_000f).Within(0.5f), "no open span after 6 s");
            float accounted = report.spans.Sum(s => s.exclusiveMs) + report.unattributedMs;
            Assert.That(accounted, Is.EqualTo(report.totalMs).Within(0.5f), "percentages must sum to 100");
        }

        static LoadInsightReport Report(float totalMs, params LoadInsightSpan[] spans) => new()
        {
            totalMs = totalMs,
            spans = spans.ToList()
        };

        static LoadInsightSpan Span(int id, LoadInsightCategory category, string label, float startMs, float endMs,
            bool isWait = false, bool envelope = false) => new()
        {
            id = id,
            label = label,
            category = (int)category,
            categoryName = LoadInsightCategories.DisplayName(category),
            startMs = startMs,
            endMs = endMs,
            durationMs = endMs - startMs,
            isWait = isWait,
            isEnvelope = envelope
        };

        static float Slice(LoadInsightReport report, LoadInsightCategory category) =>
            report.slices.FirstOrDefault(s => s.category == (int)category)?.attributedMs ?? 0f;
    }
}
