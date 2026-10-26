using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The AI Butterfly's fold in Waystation (<see cref="WaystationController"/>): the fold lands along the heading it
    /// leaves on, so the aim it may leave on shrinks with distance, and it lets go short of the ring's plane so the hull
    /// flies through the mouth (2026-10-17 audit: at a flat 12 degrees a 420 u fold landed 87 u off a 40-56 u mouth).
    /// </summary>
    public class WaystationFoldTests
    {
        [Test]
        public void AimLimit_LandsInsideHalfTheMouth()
        {
            foreach (float d in new[] { 420f, 900f, 1800f })
            {
                float limit = WaystationController.FoldAimLimitDegrees(d, 40f, 0.5f, 12f);
                float offCentre = d * Mathf.Sin(limit * Mathf.Deg2Rad);
                Assert.LessOrEqual(offCentre, 20f + 1e-3f, $"at {d} u the fold lands {offCentre:F1} u off a 40 u ring's centre");
            }
        }

        [Test]
        public void AimLimit_NegativeControl_TheFlatTwelveDegreesMissedTheRing()
        {
            Assert.Greater(420f * Mathf.Sin(12f * Mathf.Deg2Rad), 40f, "the old cone landed outside the mouth");
            Assert.AreEqual(12f, WaystationController.FoldAimLimitDegrees(30f, 40f, 0.5f, 12f), 1e-3f,
                "close in, the authored cone is the tighter one");
        }

        [Test]
        public void Hold_LandsShortOfThePlane_AndNeverNegative()
        {
            float hold = WaystationController.FoldHoldSeconds(900f, 0f, 30f, 900f);
            Assert.AreEqual((900f - 30f) / 900f, hold, 1e-4f);
            Assert.Less(WaystationController.FoldHoldSeconds(900f, 2f, 30f, 900f) * 900f, 900f, "lands before the plane");
            Assert.AreEqual(0f, WaystationController.FoldHoldSeconds(10f, 0f, 30f, 900f));
        }
    }
}
