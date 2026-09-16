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
    }
}
