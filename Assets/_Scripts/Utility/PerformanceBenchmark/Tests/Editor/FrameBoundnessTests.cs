using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    [TestFixture]
    public class FrameBoundnessTests
    {
        #region Classify

        [Test]
        public void Classify_NoData_ReturnsUnknown()
        {
            Assert.AreEqual(FrameBoundness.Unknown, FrameBoundness.Classify(0f, 0f));
        }

        [Test]
        public void Classify_GpuDominant_ReturnsGpuBound()
        {
            Assert.AreEqual(FrameBoundness.GpuBound, FrameBoundness.Classify(10f, 12f));
        }

        [Test]
        public void Classify_CpuDominant_ReturnsCpuBound()
        {
            Assert.AreEqual(FrameBoundness.CpuBound, FrameBoundness.Classify(12f, 10f));
        }

        [Test]
        public void Classify_WithinDominanceRatio_ReturnsBalanced()
        {
            Assert.AreEqual(FrameBoundness.Balanced, FrameBoundness.Classify(10f, 10.5f));
            Assert.AreEqual(FrameBoundness.Balanced, FrameBoundness.Classify(10.5f, 10f));
        }

        [Test]
        public void Classify_OnlyCpuMeasured_ReturnsCpuBound()
        {
            // GPU timing unsupported on the platform (0) but CPU was measured.
            Assert.AreEqual(FrameBoundness.CpuBound, FrameBoundness.Classify(8f, 0f));
        }

        [Test]
        public void Classify_OnlyGpuMeasured_ReturnsGpuBound()
        {
            Assert.AreEqual(FrameBoundness.GpuBound, FrameBoundness.Classify(0f, 8f));
        }

        #endregion

        #region BusyCpuMs

        [Test]
        public void BusyCpu_SubtractsPresentWaitFromMainThread()
        {
            // Main thread 16 ms but 10 ms of that was waiting for present → 6 ms of work,
            // which beats the 4 ms render thread.
            Assert.AreEqual(6f, FrameBoundness.BusyCpuMs(16.6f, 16f, 10f, 4f), 0.001f);
        }

        [Test]
        public void BusyCpu_RenderThreadDominates_WhenBusierThanMainThread()
        {
            Assert.AreEqual(5f, FrameBoundness.BusyCpuMs(16.6f, 12f, 10f, 5f), 0.001f);
        }

        [Test]
        public void BusyCpu_FallsBackToTotal_WhenThreadTimesUnavailable()
        {
            Assert.AreEqual(16.6f, FrameBoundness.BusyCpuMs(16.6f, 0f, 0f, 0f), 0.001f);
        }

        #endregion

        // --- GPU plausibility -------------------------------------------------
        // Live regression: gpuFrameTime came back as 77,028,560,000 ms on a frame whose
        // GPU was idle, and Classify called it GPU-bound with total confidence.

        [Test]
        public void SanitizeGpuMs_PassesPlausibleReadings()
        {
            Assert.AreEqual(0f, FrameBoundness.SanitizeGpuMs(0f), 0.001f);
            Assert.AreEqual(2.8f, FrameBoundness.SanitizeGpuMs(2.8f), 0.001f);
            Assert.AreEqual(250f, FrameBoundness.SanitizeGpuMs(250f), 0.001f,
                "a terrible but real frame must survive - the filter rejects non-timings, not bad news");
        }

        [Test]
        public void SanitizeGpuMs_RejectsGarbage()
        {
            Assert.AreEqual(0f, FrameBoundness.SanitizeGpuMs(77_028_560_000f), 0.001f);
            Assert.AreEqual(0f, FrameBoundness.SanitizeGpuMs(float.NaN), 0.001f);
            Assert.AreEqual(0f, FrameBoundness.SanitizeGpuMs(float.PositiveInfinity), 0.001f);
            Assert.AreEqual(0f, FrameBoundness.SanitizeGpuMs(-1f), 0.001f);
        }

        [Test]
        public void Classify_DoesNotCallGarbageGpuTimeGpuBound()
        {
            // The exact shipped reading: busy CPU 24.5 ms, GPU garbage.
            Assert.AreEqual(FrameBoundness.CpuBound,
                FrameBoundness.Classify(24.5f, 77_028_560_000f),
                "a GPU reading that is not a timing must not decide the verdict");

            // Negative control: the same CPU time against a genuinely larger GPU time
            // still reads GPU-bound, so the filter has not simply disabled the verdict.
            Assert.AreEqual(FrameBoundness.GpuBound, FrameBoundness.Classify(24.5f, 40f));
        }

        // --- Cap detection ----------------------------------------------------
        // Live regression: 8.4 ms frames at 120 FPS holding 3.2 ms CPU and 0.4 ms GPU —
        // 57% of every frame idle — reported as "CPU-bound", because IsAtCap asks the
        // platform for the refresh rate and the EDITOR does not report one.

        [Test]
        public void IsLimitedByPresent_CatchesTheVsyncCappedEditorFrame()
        {
            Assert.IsTrue(FrameBoundness.IsLimitedByPresent(8.4f, 3.2f, 0.4f, out float idle));
            Assert.AreEqual(5.2f, idle, 0.001f);
        }

        [Test]
        public void IsLimitedByPresent_LeavesGenuinelyBoundFramesAlone()
        {
            // NEGATIVE CONTROLS: work fills the frame, so nothing is waiting.
            Assert.IsFalse(FrameBoundness.IsLimitedByPresent(16.7f, 16.2f, 4f, out _), "CPU-bound");
            Assert.IsFalse(FrameBoundness.IsLimitedByPresent(16.7f, 4f, 16.2f, out _), "GPU-bound");
        }

        [Test]
        public void IsLimitedByPresent_NeedsBothBars()
        {
            // 1.4 ms slack: over neither. Ordinary jitter must not read as a cap.
            Assert.IsFalse(FrameBoundness.IsLimitedByPresent(8.4f, 7.0f, 0.4f, out _));
            // 10 ms slack but only 16.7% of a 60 ms frame: over the floor, under the ratio.
            Assert.IsFalse(FrameBoundness.IsLimitedByPresent(60f, 50f, 1f, out _));
            // 2.1 ms AND 26%: both cleared.
            Assert.IsTrue(FrameBoundness.IsLimitedByPresent(8.0f, 5.9f, 0.1f, out _));
        }

        [Test]
        public void IsLimitedByPresent_SaysNothingWithoutData()
        {
            // No timing at all must not read as "capped" — an absent sensor is not evidence.
            Assert.IsFalse(FrameBoundness.IsLimitedByPresent(8.4f, 0f, 0f, out _), "no work data");
            Assert.IsFalse(FrameBoundness.IsLimitedByPresent(0f, 3f, 1f, out _), "no frame time");
            // A garbage GPU reading must not be able to fake "work fills the frame" either.
            Assert.IsTrue(FrameBoundness.IsLimitedByPresent(8.4f, 3.2f, 77_028_560_000f, out _));
        }

        [Test]
        public void IsFrameCapConfigured_SeparatesARealCapFromUnattributedSlack()
        {
            // Screenshot 2: vsync 1 -> a cap really is set, so "Capped" is the honest label.
            Assert.IsTrue(FrameBoundness.IsFrameCapConfigured(vSyncCount: 1, targetFrameRate: 120));
            Assert.IsTrue(FrameBoundness.IsFrameCapConfigured(vSyncCount: 0, targetFrameRate: 60));
            Assert.IsTrue(FrameBoundness.IsFrameCapConfigured(vSyncCount: 1, targetFrameRate: -1));

            // Screenshot 3: `fps uncap` applied, yet 2.8 ms of slack remained. Nothing is
            // capping it — that is editor overhead the frame clock carries and
            // FrameTimingManager does not attribute, and calling it "Capped" names a cause
            // that is not there.
            Assert.IsFalse(FrameBoundness.IsFrameCapConfigured(vSyncCount: 0, targetFrameRate: -1));
            Assert.IsFalse(FrameBoundness.IsFrameCapConfigured(vSyncCount: 0, targetFrameRate: 0));

            // The two predicates are independent: slack can exist with no cap, and a cap can
            // exist on a frame with no slack (work happens to fill it).
            Assert.IsTrue(FrameBoundness.IsLimitedByPresent(7.5f, 4.7f, 2.3f, out _));
            Assert.IsFalse(FrameBoundness.IsFrameCapConfigured(0, -1));
        }

        [Test]
        public void IsIdleConsistentWithCap_RejectsTheStalledEditorFrame()
        {
            // THE CASE THIS EXISTS FOR (measured 2026-09-18): a 90.9 ms frame carrying 4.8 ms
            // of main-thread work and 2.2 ms of GPU, with the cap reading `vsync 1 · target
            // 120`. Vsync-1 on 120 Hz is an 8.3 ms FLOOR and cannot produce a 90.9 ms frame,
            // so 86.1 ms was idle for a reason that is NOT the cap. A configured cap is
            // evidence a cap could bind, never that it did.
            Assert.IsFalse(FrameBoundness.IsIdleConsistentWithCap(frameMs: 90.9f, capFps: 120f),
                "90.9 ms is 11x a 120 Hz budget — not the cap");

            // A frame actually sitting at a 120 Hz cap, and one merely missing it slightly,
            // both still read as capped — the label must survive ordinary overshoot.
            Assert.IsTrue(FrameBoundness.IsIdleConsistentWithCap(8.4f, 120f));
            Assert.IsTrue(FrameBoundness.IsIdleConsistentWithCap(12.0f, 120f));
            // 60 Hz: 16.7 ms budget, so 30 ms is within tolerance and 40 ms is not.
            Assert.IsTrue(FrameBoundness.IsIdleConsistentWithCap(30f, 60f));
            Assert.IsFalse(FrameBoundness.IsIdleConsistentWithCap(40f, 60f));

            // NEGATIVE CONTROL for the editor case §1.5 was written for: vsync is ON but the
            // platform cannot NAME the rate (refreshRateRatio reads ~0 in the editor), so
            // there is no budget to compare against and the cap stays plausible. Returning
            // false here would silently undo the label §1.5 earned.
            Assert.IsTrue(FrameBoundness.IsIdleConsistentWithCap(8.4f, capFps: -1f),
                "unnameable cap must keep the capped label");
            Assert.IsTrue(FrameBoundness.IsIdleConsistentWithCap(90.9f, capFps: 0f));
        }

        [Test]
        public void ClassifyFrameLimit_ReportAndOverlayCannotDisagree()
        {
            // THE BUG (measured 2026-09-20): an EMPTY scene — zero prism entities — saved
            // `boundVerdict: "CPU-bound"` on a frame that was 76.6% idle and pinned to the
            // 120 Hz vsync budget to within 0.01 ms. BuildReport called bare Classify, which
            // knows only CPU vs GPU; the live row walked the cap ladder. The report was the
            // artifact somebody reads a week later.
            var limit = FrameBoundness.ClassifyFrameLimit(
                frameMs: 8.340f, measuredFps: 119.90f, busyCpuMs: 1.949f, gpuMs: 1.395f,
                capConfigured: true, namedCapFps: -1f, out float idleMs);
            Assert.AreEqual(FrameBoundness.FrameLimit.CappedByIdle, limit);
            Assert.AreEqual(6.391f, idleMs, 0.01f);
            // …and the old call still answers the wrong thing, which is why it was replaced.
            Assert.AreEqual(FrameBoundness.CpuBound, FrameBoundness.Classify(1.949f, 1.395f));

            // Both Test C arms, 61% idle at the cap.
            Assert.AreEqual(FrameBoundness.FrameLimit.CappedByIdle,
                FrameBoundness.ClassifyFrameLimit(8.388f, 119.21f, 3.232f, 1.464f, true, -1f, out _));
            Assert.AreEqual(FrameBoundness.FrameLimit.CappedByIdle,
                FrameBoundness.ClassifyFrameLimit(8.395f, 119.12f, 3.248f, 1.540f, true, -1f, out _));

            // NEGATIVE CONTROLS — a verdict that calls everything "capped" is worthless.
            // pathOff: genuinely CPU-bound at 13% idle, under both bars.
            Assert.AreEqual(FrameBoundness.FrameLimit.Cpu,
                FrameBoundness.ClassifyFrameLimit(25.859f, 38.67f, 22.471f, 4.719f, true, -1f, out _));
            // The 90.9 ms frame under a NAMEABLE 120 Hz cap an 8.3 ms floor cannot produce.
            Assert.AreEqual(FrameBoundness.FrameLimit.Stalled,
                FrameBoundness.ClassifyFrameLimit(90.9f, 11.0f, 4.8f, 2.2f, true, 120f, out _));
            // `fps uncap` applied — editor residue, not a cap.
            Assert.AreEqual(FrameBoundness.FrameLimit.IdleNoCap,
                FrameBoundness.ClassifyFrameLimit(8.2f, 122f, 4.8f, 2.1f, false, -1f, out _));
            // A cap the platform CAN name.
            Assert.AreEqual(FrameBoundness.FrameLimit.AtNamedCap,
                FrameBoundness.ClassifyFrameLimit(8.4f, 119f, 3.0f, 1.0f, true, 120f, out _));
            // No timing data at all must stay silent rather than guess.
            Assert.AreEqual(FrameBoundness.FrameLimit.Unknown,
                FrameBoundness.ClassifyFrameLimit(8.4f, 119f, 0f, 0f, true, -1f, out _));
            // A GPU-bound frame whose work fills it.
            Assert.AreEqual(FrameBoundness.FrameLimit.Gpu,
                FrameBoundness.ClassifyFrameLimit(5.0f, 200f, 1.0f, 4.5f, false, -1f, out _));
        }

        [Test]
        public void DescribeFrameLimit_SaysWhenAVerdictIsNotAMeasurement()
        {
            // A saved report is read without the context that produced it, so a capped or
            // stalled frame has to disqualify its own frame time in the text.
            StringAssert.Contains("NOT a measurement",
                FrameBoundness.DescribeFrameLimit(FrameBoundness.FrameLimit.CappedByIdle, 6.4f, -1f));
            StringAssert.Contains("NOT a measurement",
                FrameBoundness.DescribeFrameLimit(FrameBoundness.FrameLimit.AtNamedCap, 0f, 120f));
            StringAssert.Contains("NOT a measurement",
                FrameBoundness.DescribeFrameLimit(FrameBoundness.FrameLimit.Stalled, 86.1f, 120f));
            // A real processor verdict must NOT carry the disclaimer.
            Assert.AreEqual(FrameBoundness.CpuBound,
                FrameBoundness.DescribeFrameLimit(FrameBoundness.FrameLimit.Cpu, 0f, -1f));
            Assert.AreEqual(FrameBoundness.GpuBound,
                FrameBoundness.DescribeFrameLimit(FrameBoundness.FrameLimit.Gpu, 0f, -1f));
        }
    }
}
