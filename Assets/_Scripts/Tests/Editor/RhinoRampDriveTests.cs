using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's ramp drive (<see cref="RampBoostAIPolicySO"/>): brake on the ramp to a corner speed sized by the
    /// corner, and press the ramp only where the leg pays the wind-up back. Pure maths - no vessel.
    /// </summary>
    public class RhinoRampDriveTests
    {
        RampBoostAIPolicySO _policy;

        [SetUp]
        public void SetUp() => _policy = ScriptableObject.CreateInstance<RampBoostAIPolicySO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_policy);

        static AIBoostContext Context(float distance, float speed, Vector3 nextLeg = default) =>
            new(Vector3.forward * distance, Vector3.forward, Vector3.forward, speed, 0f, false, false, false, nextLeg);

        [Test]
        public void CornerSpeed_UnknownCornerIsANinetyDegreeOne()
        {
            Assert.AreEqual(_policy.CornerSpeed, _policy.CornerSpeedFor(-1f), 1e-3f);
            Assert.AreEqual(_policy.CornerSpeed, _policy.CornerSpeedFor(90f), 1e-3f);
        }

        [Test]
        public void CornerSpeed_GentleBendsAllowMore_HairpinsFloor()
        {
            Assert.AreEqual(_policy.CornerSpeed / Mathf.Tan(22.5f * Mathf.Deg2Rad), _policy.CornerSpeedFor(45f), 1e-2f);
            Assert.Greater(_policy.CornerSpeedFor(30f), _policy.CornerSpeedFor(90f));
            Assert.AreEqual(_policy.MinCornerSpeed, _policy.CornerSpeedFor(175f), 1e-3f);
        }

        [Test]
        public void BrakeDistance_IsTheRampsOwnBleed()
        {
            // 840 -> 360 u/s at the Rhino's 300 u/s² bleed: (840² - 360²) / 600 = 960 u.
            Assert.AreEqual(960f, RampBoostAIPolicySO.BrakeDistance(840f, 360f, 300f), 1e-2f);
            Assert.AreEqual(0f, RampBoostAIPolicySO.BrakeDistance(300f, 360f, 300f));
        }

        [Test]
        public void PeakSpeed_ClimbsAndBrakesWithinTheLeg()
        {
            float v = RampBoostAIPolicySO.PeakSpeed(100f, 360f, 220f, 300f, 3000f);
            float used = (v * v - 100f * 100f) / (2f * 220f) + (v * v - 360f * 360f) / (2f * 300f);
            Assert.AreEqual(3000f, used, 0.5f, "the climb plus the brake spend exactly the leg");
            Assert.AreEqual(400f, RampBoostAIPolicySO.PeakSpeed(400f, 360f, 220f, 300f, 0f), 1e-3f,
                "no leg: never slower than now");
            Assert.AreEqual(Mathf.Sqrt(100f * 100f + 2f * 220f * 50f), RampBoostAIPolicySO.PeakSpeed(100f, 360f, 220f, 300f, 50f), 1e-2f,
                "a leg too short to pass the exit speed climbs the whole way");
        }

        [Test]
        public void PaysBack_LongLegYes_ShortLegNo()
        {
            Assert.IsTrue(_policy.PaysBack(Context(4000f, 100f), 220f, 300f));
            Assert.IsFalse(_policy.PaysBack(Context(120f, 100f), 220f, 300f), "no room left past the brake margin");
        }

        [Test]
        public void BrakePoint_FarNo_InsideTheBleedYes()
        {
            Assert.IsFalse(_policy.BrakePoint(Context(5000f, 840f), 300f));
            // 840 -> 600 u/s (a 90-degree corner) bleeds over 576 u; 800 u less the 120 u margin and 0.25 s lead is 470.
            Assert.IsTrue(_policy.BrakePoint(Context(800f, 840f), 300f), "576 u of bleed does not fit in 470");
            Assert.IsFalse(_policy.BrakePoint(Context(800f, 200f), 300f), "already under the corner speed");
        }

        [Test]
        public void BrakePoint_AGentleBendBrakesLater()
        {
            // The same 700 u at 840 u/s: a 90° corner brakes now, a 30° bend (corner speed 600 / tan 15° = 2239) does not.
            var right = Context(700f, 840f, Vector3.right * 500f);
            var gentle = Context(700f, 840f, Quaternion.AngleAxis(30f, Vector3.up) * Vector3.forward * 500f);
            Assert.AreEqual(90f, right.CornerDegrees, 1e-2f);
            Assert.AreEqual(30f, gentle.CornerDegrees, 1e-2f);
            Assert.IsTrue(_policy.BrakePoint(right, 300f));
            Assert.IsFalse(_policy.BrakePoint(gentle, 300f));
        }

        [Test]
        public void CornerDegrees_UnknownWithoutANextLeg()
        {
            Assert.AreEqual(-1f, Context(500f, 300f).CornerDegrees);
        }

        [Test]
        public void GovernThrottle_LiftsAboveTheCornerSpeed_ReturnsUnder_NeverPastTheFloor()
        {
            Assert.Less(RampBoostAIPolicySO.GovernThrottle(1f, 800f, 360f, 4f, 0.1f, 0.4f), 1f);
            Assert.AreEqual(1f, RampBoostAIPolicySO.GovernThrottle(0.8f, 200f, 360f, 4f, 0.1f, 0.4f), 1e-3f);
            Assert.AreEqual(0.4f, RampBoostAIPolicySO.GovernThrottle(0.45f, 800f, 360f, 4f, 0.1f, 0.4f), 1e-3f);
        }
    }
}
