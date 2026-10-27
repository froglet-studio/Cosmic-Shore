using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Sparrow's Space card draws the guns' live cone at a READABLE angle: the real caps (a
    /// degree or two until the blow-out) map piecewise-linearly onto one card angle per level
    /// (<see cref="SparrowHUDView.SprayCardAngle"/>). Pinned here because a wrong knot draws a
    /// plausible wrong card - a cone that reads one level off from the gun it reports.
    /// </summary>
    public class SparrowGunSprayCardAngleTests
    {
        // The shipped shape: 1.5 / 7.5 / 37.5 real, onto 8 / 19 / 30 on the card.
        static readonly float[] Card = { 8f, 19f, 30f };
        const float Sustainable = 1.5f, Blowout = 7.5f, Collapse = 37.5f;

        static float Angle(float real) => SparrowHUDView.SprayCardAngle(real, Sustainable, Blowout, Collapse, Card);

        [Test]
        public void ColdGun_IsOneStream() => Assert.AreEqual(0f, Angle(0f), 1e-4f);

        [Test]
        public void EachCap_LandsOnItsCardAngle()
        {
            Assert.AreEqual(8f, Angle(Sustainable), 1e-4f);
            Assert.AreEqual(19f, Angle(Blowout), 1e-4f);
            Assert.AreEqual(30f, Angle(Collapse), 1e-4f);
        }

        [Test]
        public void BetweenCaps_Interpolates()
        {
            Assert.AreEqual(4f, Angle(0.75f), 1e-4f);                // half way up the first ramp
            Assert.AreEqual(13.5f, Angle(4.5f), 1e-4f);              // half way up the blow-out
            Assert.AreEqual(24.5f, Angle(22.5f), 1e-4f);             // half way up the collapse
        }

        [Test]
        public void PastTheLastCap_Clamps() => Assert.AreEqual(30f, Angle(90f), 1e-4f);

        [Test]
        public void NegativeAngle_IsCold() => Assert.AreEqual(0f, Angle(-3f), 1e-4f);

        [Test]
        public void AProfileWithoutLaterStages_HoldsAtItsWidestLevel()
        {
            // No blow-out and no collapse authored: the controller passes 0 for both caps.
            Assert.AreEqual(8f, SparrowHUDView.SprayCardAngle(1.5f, 1.5f, 0f, 0f, Card), 1e-4f);
            Assert.AreEqual(8f, SparrowHUDView.SprayCardAngle(5f, 1.5f, 0f, 0f, Card), 1e-4f);
        }

        [Test]
        public void NoCardAngles_IsCold() => Assert.AreEqual(0f, SparrowHUDView.SprayCardAngle(7.5f, 1.5f, 7.5f, 37.5f, new float[0]), 1e-4f);
    }
}
