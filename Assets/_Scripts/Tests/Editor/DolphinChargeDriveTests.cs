using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Dolphin's charge drive (<see cref="ChargeBoostAIPolicySO"/>): drift again at the discharge's PEAK (the drift
    /// freezes that speed), and spend on a straight only when a discharge can lift the speed. Pure maths - no vessel.
    /// </summary>
    public class DolphinChargeDriveTests
    {
        ChargeBoostAIPolicySO _policy;

        [SetUp]
        public void SetUp() => _policy = ScriptableObject.CreateInstance<ChargeBoostAIPolicySO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_policy);

        [Test]
        public void DischargeCeiling_IsCruiseTimesTheSquaredMultiplier()
        {
            // DOLPHIN_ENERGY_ECONOMY.md §2: 68 × 2.259² = 347 off a full meter; an empty meter adds nothing.
            Assert.AreEqual(68f * 2.259f * 2.259f, ChargeBoostAIPolicySO.DischargeCeiling(68f, 2.259f, 1f), 1e-2f);
            Assert.AreEqual(68f, ChargeBoostAIPolicySO.DischargeCeiling(68f, 2.259f, 0f), 1e-3f);
        }

        [Test]
        public void AtPeak_OnlyOnceTheSpeedMeetsTheFallingTarget()
        {
            Assert.IsFalse(_policy.AtPeak(0.05f, 300f, 300f), "the target rises on the release itself: too early to read a peak");
            Assert.IsFalse(_policy.AtPeak(0.6f, 200f, 320f), "still climbing toward the target");
            Assert.IsTrue(_policy.AtPeak(0.6f, 298f, 300f), "met the target: the top of the discharge");
        }

        [Test]
        public void PulseWorthIt_SlowSlideYes_SlideAtTheTopNo()
        {
            Assert.IsTrue(_policy.PulseWorthIt(150f, 68f, 2.259f, 0.95f));
            Assert.IsFalse(_policy.PulseWorthIt(340f, 68f, 2.259f, 1f), "already sliding near 347: only the engage sound");
        }
    }
}
