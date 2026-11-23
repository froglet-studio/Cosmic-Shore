#if UNITY_EDITOR
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>RTT thresholds used by Player.NetRttMs, the debug overlay and the poor-connection badge.</summary>
    [TestFixture]
    public class ConnectionQualityTests
    {
        [TestCase(0, ConnectionQualityLevel.Unknown)]
        [TestCase(-5, ConnectionQualityLevel.Unknown)]
        [TestCase(1, ConnectionQualityLevel.Good)]
        [TestCase(ConnectionQuality.GoodMaxMs, ConnectionQualityLevel.Good)]
        [TestCase(ConnectionQuality.GoodMaxMs + 1, ConnectionQualityLevel.Fair)]
        [TestCase(ConnectionQuality.PoorAboveMs, ConnectionQualityLevel.Fair)]
        [TestCase(ConnectionQuality.PoorAboveMs + 1, ConnectionQualityLevel.Poor)]
        public void Classify_UsesThresholds(int ms, ConnectionQualityLevel expected) =>
            Assert.AreEqual(expected, ConnectionQuality.Classify(ms));

        [Test]
        public void ToWire_ClampsToUShort()
        {
            Assert.AreEqual(ushort.MaxValue, ConnectionQuality.ToWire(1_000_000UL));
            Assert.AreEqual((ushort)150, ConnectionQuality.ToWire(150UL));
        }

        [Test]
        public void IsBad_OnlyAbovePoorThreshold()
        {
            Assert.IsFalse(ConnectionQuality.IsBad(ConnectionQuality.PoorAboveMs));
            Assert.IsTrue(ConnectionQuality.IsBad(ConnectionQuality.PoorAboveMs + 1));
        }
    }
}
#endif
