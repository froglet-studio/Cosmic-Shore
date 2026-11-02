#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>Host-side cap and rate limit on client prism reports (score integrity).</summary>
    [TestFixture]
    public class HitReportGuardTests
    {
        [Test]
        public void ClampVolume_CapsOnlyAbsurdValues()
        {
            Assert.AreEqual(5f, HitReportGuard.ClampVolume(5f));
            Assert.AreEqual(HitReportGuard.MaxReportedVolume, HitReportGuard.ClampVolume(1e9f));
        }

        [Test]
        public void Burst_UpToCapacity_ThenThrottles()
        {
            var g = new HitReportGuard();
            for (int i = 0; i < (int)HitReportGuard.BurstCapacity; i++)
                Assert.IsTrue(g.TryConsume(10.0), $"report {i} should pass");
            Assert.IsFalse(g.TryConsume(10.0));
            Assert.AreEqual(1, g.Throttled);
        }

        [Test]
        public void Refills_OverTime()
        {
            var g = new HitReportGuard();
            for (int i = 0; i < (int)HitReportGuard.BurstCapacity; i++) g.TryConsume(0.0);
            Assert.IsFalse(g.TryConsume(0.0));
            Assert.IsTrue(g.TryConsume(1.0)); // a second later, RefillPerSecond tokens are back
        }

        [Test]
        public void SustainedHonestRate_NeverThrottled()
        {
            var g = new HitReportGuard();
            double t = 0;
            for (int i = 0; i < 3000; i++, t += 1.0 / 120) // 120 reports/s for 25 s
                Assert.IsTrue(g.TryConsume(t));
        }
    }
}
#endif
