#if UNITY_EDITOR
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// HapticEnvelopeFollower Tests - Validates the DSP core of the continuous
    /// haptic bed.
    ///
    /// WHY THIS MATTERS:
    /// The follower converts live FMOD bus loudness into actuator level every
    /// frame. Wrong attack/release constants make the bed lag or stutter, a
    /// broken gate leaves the vibration motor running on silence (battery
    /// drain), and hysteresis bugs make the bed flutter at the threshold.
    /// </summary>
    [TestFixture]
    public class HapticEnvelopeFollowerTests
    {
        static HapticFollowerSettings Settings() => HapticFollowerSettings.Default;

        [Test]
        public void Step_SilentInput_StaysAtZero()
        {
            var follower = new HapticEnvelopeFollower();
            float output = 0f;
            for (int i = 0; i < 100; i++)
                output = follower.Step(0f, 0.016f, Settings());

            Assert.AreEqual(0f, output);
            Assert.IsFalse(follower.GateOpen);
        }

        [Test]
        public void Step_LoudInput_OpensGateAndRises()
        {
            var follower = new HapticEnvelopeFollower();
            float output = 0f;
            for (int i = 0; i < 30; i++)
                output = follower.Step(1f, 0.016f, Settings());

            Assert.IsTrue(follower.GateOpen);
            Assert.Greater(output, 0.5f);
        }

        [Test]
        public void Step_AttackIsFasterThanRelease()
        {
            var settings = Settings();
            var follower = new HapticEnvelopeFollower();

            follower.Step(1f, 0.05f, settings);
            float afterAttack = follower.SmoothedLevel;

            // Drive to full, then release for the same wall time.
            for (int i = 0; i < 200; i++) follower.Step(1f, 0.016f, settings);
            follower.Step(0f, 0.05f, settings);
            float dropAmount = 1f - follower.SmoothedLevel;

            Assert.Greater(afterAttack, dropAmount,
                "one attack step should gain more level than one equal release step loses");
        }

        [Test]
        public void Step_GateHasHysteresis()
        {
            var settings = Settings();
            var follower = new HapticEnvelopeFollower();

            // Drive smoothed level to just above GateOpen, then feed a level
            // between GateClose and GateOpen — the gate must STAY open.
            for (int i = 0; i < 500; i++)
                follower.Step(settings.GateOpen * 1.5f, 0.016f, settings);
            Assert.IsTrue(follower.GateOpen);

            float between = (settings.GateOpen + settings.GateClose) * 0.5f;
            follower.Step(between, 0.016f, settings);
            Assert.IsTrue(follower.GateOpen, "gate must not close between GateClose and GateOpen");

            // Below GateClose it must eventually close.
            for (int i = 0; i < 500; i++)
                follower.Step(0f, 0.016f, settings);
            Assert.IsFalse(follower.GateOpen);
        }

        [Test]
        public void Step_OutputRespectsMaxLevelCeiling()
        {
            var settings = Settings();
            settings.MaxLevel = 0.6f;
            var follower = new HapticEnvelopeFollower();

            float output = 0f;
            for (int i = 0; i < 500; i++)
                output = follower.Step(1f, 0.016f, settings);

            Assert.LessOrEqual(output, 0.6f + 1e-4f);
        }

        [Test]
        public void Step_GammaBoostsQuietDetail()
        {
            var settings = Settings();
            settings.Gamma = 0.5f;
            settings.GateOpen = 0.01f;
            settings.GateClose = 0.005f;
            settings.MaxLevel = 1f;
            var follower = new HapticEnvelopeFollower();

            float output = 0f;
            for (int i = 0; i < 1000; i++)
                output = follower.Step(0.25f, 0.016f, settings);

            // sqrt(0.25) = 0.5 — the curve lifts quiet input above linear.
            Assert.Greater(output, 0.4f);
        }

        [Test]
        public void Reset_ClosesGateAndZeroesLevel()
        {
            var follower = new HapticEnvelopeFollower();
            for (int i = 0; i < 50; i++) follower.Step(1f, 0.016f, Settings());

            follower.Reset();

            Assert.AreEqual(0f, follower.SmoothedLevel);
            Assert.IsFalse(follower.GateOpen);
        }

        [Test]
        public void SlewTowards_IsRateLimited()
        {
            float result = HapticEnvelopeFollower.SlewTowards(0f, 1f, maxDeltaPerSecond: 2f, deltaSeconds: 0.1f);
            Assert.AreEqual(0.2f, result, 1e-5f);
        }

        [Test]
        public void NormalizeFrequency_MapsRangeLogarithmically()
        {
            Assert.AreEqual(0f, HapticEnvelopeFollower.NormalizeFrequency(65f, 65f, 3500f), 1e-3f);
            Assert.AreEqual(1f, HapticEnvelopeFollower.NormalizeFrequency(3500f, 65f, 3500f), 1e-3f);

            float mid = HapticEnvelopeFollower.NormalizeFrequency(480f, 65f, 3500f);
            Assert.That(mid, Is.InRange(0.45f, 0.55f), "geometric mid (~477 Hz) should land near 0.5");
        }

        [Test]
        public void NormalizeFrequency_NoSignal_ReturnsNeutralMid()
        {
            Assert.AreEqual(0.5f, HapticEnvelopeFollower.NormalizeFrequency(0f, 65f, 3500f));
        }
    }
}
#endif
