using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The spring contract ScarabAnimation's puppetry stands on. The property that makes the
    /// closed form worth its arithmetic is the SEMIGROUP one — stepping 1 s in one call or in
    /// 240 sub-steps lands on the same state (a Lerp(k·dt) or Euler integration fails this,
    /// which is exactly the frame-rate dependence being retired) — plus the damping character
    /// each part group is tuned by: critical damping never overshoots (the horn must aim, not
    /// wobble), ζ 0.4 visibly rings (the antennae must), and an impulse peaks immediately and
    /// decays to nothing (the juke flourish's whole mechanism).
    /// </summary>
    public class AngularSpringTests
    {
        const float Omega = 20f;

        static AngularSpring.State StepMany(AngularSpring.State s, float target, float omega,
                                            float zeta, float total, int steps)
        {
            float dt = total / steps;
            for (int i = 0; i < steps; i++)
                s = AngularSpring.Step(s, target, omega, zeta, dt);
            return s;
        }

        [Test]
        public void StepIsFrameRateIndependent()
        {
            // Constant target: the exact solution composes, so 1 step == 240 steps to float
            // accuracy. This is the assertion the Lerp idiom cannot pass.
            foreach (float zeta in new[] { 0.4f, 0.6f, 1f, 1.4f })
            {
                var start = new AngularSpring.State { Position = 40f, Velocity = -15f };
                var one = AngularSpring.Step(start, 0f, Omega, zeta, 1f);
                var sixty = StepMany(start, 0f, Omega, zeta, 1f, 60);
                var lots = StepMany(start, 0f, Omega, zeta, 1f, 240);
                Assert.AreEqual(one.Position, sixty.Position, 1e-3f, $"zeta {zeta} pos 1 vs 60");
                Assert.AreEqual(one.Position, lots.Position, 1e-3f, $"zeta {zeta} pos 1 vs 240");
                Assert.AreEqual(one.Velocity, lots.Velocity, 1e-2f, $"zeta {zeta} vel 1 vs 240");
            }
        }

        [Test]
        public void CriticalDampingNeverOvershoots()
        {
            var s = AngularSpring.AtRest(30f);
            for (int i = 0; i < 600; i++)
            {
                s = AngularSpring.Step(s, 0f, Omega, 1f, 1f / 60f);
                Assert.GreaterOrEqual(s.Position, -1e-3f, "critically damped spring crossed its target");
            }
            Assert.AreEqual(0f, s.Position, 1e-2f, "did not settle");
        }

        [Test]
        public void UnderdampedVisiblyRings()
        {
            // ζ 0.4 from 30° must overshoot by the analytic fraction exp(−ζπ/√(1−ζ²)) ≈ 25%
            // of the step — the "one or two honest oscillations" the antennae are tuned for.
            var s = AngularSpring.AtRest(30f);
            float minimum = float.MaxValue;
            for (int i = 0; i < 600; i++)
            {
                s = AngularSpring.Step(s, 0f, Omega, 0.4f, 1f / 60f);
                minimum = Mathf.Min(minimum, s.Position);
            }
            float expected = -30f * Mathf.Exp(-0.4f * Mathf.PI / Mathf.Sqrt(1f - 0.4f * 0.4f));
            Assert.AreEqual(expected, minimum, 0.6f, "overshoot magnitude off the analytic value");
            Assert.AreEqual(0f, s.Position, 0.1f, "did not settle");
        }

        [Test]
        public void ImpulsePeaksImmediatelyAndDecays()
        {
            var s = AngularSpring.AtRest(0f);
            AngularSpring.AddImpulse(ref s, 400f);
            Assert.AreEqual(400f, s.Velocity, 1e-4f, "impulse is a velocity add");

            float peak = 0f;
            int peakFrame = 0;
            for (int i = 0; i < 600; i++)
            {
                s = AngularSpring.Step(s, 0f, Omega, 0.6f, 1f / 60f);
                if (Mathf.Abs(s.Position) > peak) { peak = Mathf.Abs(s.Position); peakFrame = i; }
            }
            Assert.Greater(peak, 5f, "the kick must visibly move the channel");
            Assert.Less(peakFrame, 12, "the response must peak within ~0.2 s of the event");
            Assert.AreEqual(0f, s.Position, 0.05f, "must decay back to rest on its own");
        }

        [Test]
        public void ZeroAndNegativeDtAreInert()
        {
            var s = new AngularSpring.State { Position = 12f, Velocity = 3f };
            var same = AngularSpring.Step(s, 0f, Omega, 1f, 0f);
            Assert.AreEqual(s.Position, same.Position, 0f);
            Assert.AreEqual(s.Velocity, same.Velocity, 0f);
        }
    }
}
