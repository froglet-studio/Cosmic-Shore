using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The gamepad's steering smoother (<see cref="CascadeSmoother"/>, tuned by
    /// <see cref="GamepadFlightConfigSO"/>): a step in the stick reaches the hull smoothly, without
    /// overshoot, in about the authored delay at any frame rate, and a released stick lands on centre.
    /// </summary>
    [TestFixture]
    public class CascadeSmootherTests
    {
        const int Stages = 4;
        const float Delay = 0.12f;

        static float TimeTo90PercentOfAStep(float dt)
        {
            var smoother = new CascadeSmoother(1);
            smoother.ResetToZero();
            var v = new float[1];
            for (float t = dt; t < 2f; t += dt)
            {
                v[0] = 1f;
                smoother.Step(v, dt, Stages, Delay);
                if (v[0] >= 0.9f) return t;
            }
            return float.PositiveInfinity;
        }

        [Test]
        public void ZeroDelay_PassesTheStickStraightThrough()
        {
            var smoother = new CascadeSmoother(2);
            var v = new[] { 0.3f, -0.7f };
            smoother.Step(v, 1f / 60f, Stages, 0f);
            Assert.That(v[0], Is.EqualTo(0.3f));
            Assert.That(v[1], Is.EqualTo(-0.7f));
        }

        [Test]
        public void AStep_ReachesNinetyPercentInAboutTwoTenthsOfASecond()
        {
            // Four 30 ms stages: ~0.2 s to 90%, where the old one-stage 0.67 s nose lag took ~1.5 s.
            Assert.That(TimeTo90PercentOfAStep(1f / 60f), Is.InRange(0.17f, 0.23f));
        }

        [Test]
        public void TheResponseIsTheSameAtAnyFrameRate()
        {
            float at30 = TimeTo90PercentOfAStep(1f / 30f);
            float at144 = TimeTo90PercentOfAStep(1f / 144f);
            // Within one 30 fps frame of each other: the substeps, not the frame rate, set the curve.
            Assert.That(Mathf.Abs(at30 - at144), Is.LessThan(1f / 30f + 1e-4f));
        }

        [Test]
        public void AStep_NeverOvershootsAndStartsGently()
        {
            var smoother = new CascadeSmoother(1);
            smoother.ResetToZero();
            var v = new float[1];
            float previous = 0f, firstRise = -1f;
            for (int i = 0; i < 120; i++)
            {
                v[0] = 1f;
                smoother.Step(v, 1f / 60f, Stages, Delay);
                Assert.That(v[0], Is.LessThanOrEqualTo(1f));
                Assert.That(v[0], Is.GreaterThanOrEqualTo(previous));
                float rise = v[0] - previous;
                if (firstRise < 0f) firstRise = rise;
                previous = v[0];
            }
            // A one-stage lag takes its biggest step on the first frame; four stages start nearly still.
            Assert.That(firstRise, Is.LessThan(0.05f));
        }

        [Test]
        public void AReleasedStick_LandsExactlyOnCentre()
        {
            var smoother = new CascadeSmoother(1);
            var v = new[] { 1f };
            smoother.Step(v, 1f / 60f, Stages, Delay);
            for (int i = 0; i < 600; i++)
            {
                v[0] = 0f;
                smoother.Step(v, 1f / 60f, Stages, Delay);
            }
            Assert.That(v[0], Is.EqualTo(0f));
        }

        [Test]
        public void ARampTrailsByTheAuthoredDelay()
        {
            // Mean delay of the chain = the authored total delay: a steadily moving input is followed
            // that far behind once the chain has settled, less the frame the held input leads by.
            var smoother = new CascadeSmoother(1);
            smoother.ResetToZero();
            var v = new float[1];
            const float dt = 1f / 120f;
            float t = 0f;
            for (int i = 0; i < 240; i++)
            {
                t += dt;
                v[0] = t;
                smoother.Step(v, dt, Stages, Delay);
            }
            Assert.That(t - v[0], Is.EqualTo(Delay - dt).Within(0.005f));
        }

        [Test]
        public void ShippedDefaults_AreTheDocumentedOnes()
        {
            var config = ScriptableObject.CreateInstance<GamepadFlightConfigSO>();
            Assert.That(config.SmoothingStages, Is.EqualTo(6));
            Assert.That(config.SmoothingDelaySeconds, Is.EqualTo(0.35f));
            Object.DestroyImmediate(config);
        }
    }
}
