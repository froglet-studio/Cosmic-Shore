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
    }
}
