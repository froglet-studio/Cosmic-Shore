using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Edit-mode coverage for the full-auto accuracy-decay cone
    /// (<see cref="GunSpreadMath"/>). Lives under an Editor/ folder per CLAUDE.md — a test
    /// anywhere else compiles into the player and breaks the Windows build at the IL2CPP
    /// linker, which neither the compile tier nor the edit-mode suite can see.
    /// </summary>
    public class GunSpreadMathTests
    {
        const float Onset = 0.12f;
        const float Growth = 3.2f;
        const float Max = 4f;

        static float Angle(float held) => GunSpreadMath.HalfAngleDegrees(held, Onset, Growth, Max);

        // ------------------------------------------------------------------ the ramp

        [Test]
        public void HalfAngle_IsZero_ThroughTheOnsetWindow()
        {
            // The whole point of the onset grace: a tapped burst is PERFECTLY accurate.
            Assert.AreEqual(0f, Angle(0f));
            Assert.AreEqual(0f, Angle(Onset * 0.5f));
            Assert.AreEqual(0f, Angle(Onset));
        }

        [Test]
        public void HalfAngle_GrowsLinearly_AfterTheOnsetWindow()
        {
            Assert.AreEqual(Growth * 0.5f, Angle(Onset + 0.5f), 1e-4f);
            Assert.AreEqual(Growth * 1.0f, Angle(Onset + 1.0f), 1e-4f);
        }

        [Test]
        public void HalfAngle_SaturatesAtTheCap_AndNeverExceedsIt()
        {
            float timeToCap = Onset + Max / Growth;
            Assert.AreEqual(Max, Angle(timeToCap), 1e-4f);
            Assert.AreEqual(Max, Angle(timeToCap + 5f), 1e-4f);
            Assert.AreEqual(Max, Angle(timeToCap + 3600f), 1e-4f);
        }

        [Test]
        public void HalfAngle_IsZero_WhenTheProfileOptsOut()
        {
            // A zero cap (or zero growth) is the sanctioned "this gun has no spread" opt-out.
            Assert.AreEqual(0f, GunSpreadMath.HalfAngleDegrees(10f, Onset, Growth, 0f));
            Assert.AreEqual(0f, GunSpreadMath.HalfAngleDegrees(10f, Onset, 0f, Max));
        }

        [Test]
        public void HalfAngle_TreatsNegativeOnsetAsZero()
        {
            Assert.AreEqual(Growth, GunSpreadMath.HalfAngleDegrees(1f, -5f, Growth, Max), 1e-4f);
        }

        // ------------------------------------------------------------------ the cone

        [Test]
        public void Perturb_ReturnsTheAimExactly_WhenTheConeIsClosed()
        {
            var forward = new Vector3(1f, 2f, 3f).normalized;
            var shot = GunSpreadMath.Perturb(forward, 0f, 0.5f, 12345);
            Assert.That(Vector3.Angle(forward, shot), Is.LessThan(1e-3f));
        }

        [Test]
        public void Perturb_AlwaysReturnsAUnitVector()
        {
            for (uint i = 0; i < 500; i++)
            {
                var shot = GunSpreadMath.Perturb(Vector3.forward, 4f, 0.5f, i);
                Assert.AreEqual(1f, shot.magnitude, 1e-4f, $"serial {i}");
            }
        }

        [Test]
        public void Perturb_NeverLeavesTheCone()
        {
            // The cap is a hard promise: no round may land outside the authored half-angle.
            const float half = 4f;
            var forward = new Vector3(-0.3f, 0.8f, 0.5f).normalized;

            for (uint i = 0; i < 5000; i++)
            {
                var shot = GunSpreadMath.Perturb(forward, half, 0.5f, i);
                Assert.That(Vector3.Angle(forward, shot), Is.LessThanOrEqualTo(half + 1e-3f),
                    $"serial {i} escaped the cone");
            }
        }

        [Test]
        public void Perturb_StaysInsideTheCone_WhenAimingStraightUp()
        {
            // The basis helper swaps near the poles; a degenerate cross product there would
            // produce NaNs or a collapsed cone.
            foreach (var axis in new[] { Vector3.up, Vector3.down })
            {
                for (uint i = 0; i < 500; i++)
                {
                    var shot = GunSpreadMath.Perturb(axis, 4f, 0.5f, i);
                    Assert.IsFalse(float.IsNaN(shot.x) || float.IsNaN(shot.y) || float.IsNaN(shot.z));
                    Assert.That(Vector3.Angle(axis, shot), Is.LessThanOrEqualTo(4f + 1e-3f));
                }
            }
        }

        [Test]
        public void Perturb_IsDeterministicPerSerial()
        {
            // Two peers that agree on the shot count must agree on where the shot went —
            // this is what keeps locally-spawned turret prisms in the same places.
            var a = GunSpreadMath.Perturb(Vector3.forward, 4f, 0.5f, 987654);
            var b = GunSpreadMath.Perturb(Vector3.forward, 4f, 0.5f, 987654);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void Perturb_ScattersConsecutiveSerials()
        {
            // Consecutive rounds must not walk a smooth path — that would draw a line, not a
            // stochastic cone. Neighbouring serials should differ by an appreciable angle.
            float meanStep = 0f;
            const int n = 400;
            for (uint i = 0; i < n; i++)
            {
                var a = GunSpreadMath.Perturb(Vector3.forward, 4f, 0.5f, i);
                var b = GunSpreadMath.Perturb(Vector3.forward, 4f, 0.5f, i + 1);
                meanStep += Vector3.Angle(a, b);
            }
            meanStep /= n;

            // Uniform-disc sampling over a 4° cone gives a mean neighbour separation well
            // above a degree; anything near zero means the hash has failed to decorrelate.
            Assert.That(meanStep, Is.GreaterThan(1f));
        }

        [Test]
        public void Perturb_FillsTheConeEvenly_AtTheUniformDiscBias()
        {
            // bias 0.5 samples uniformly over the cone's DISC, which is what makes the whole
            // danger zone saturate rather than piling every round in the middle. Uniform over
            // a disc puts half the rounds outside r/√2 ≈ 0.707 of the radius.
            const float half = 4f;
            int outerHalf = 0;
            const int n = 20000;

            for (uint i = 0; i < n; i++)
            {
                var shot = GunSpreadMath.Perturb(Vector3.forward, half, 0.5f, i);
                if (Vector3.Angle(Vector3.forward, shot) > half * 0.7071f) outerHalf++;
            }

            Assert.That(outerHalf / (float)n, Is.EqualTo(0.5f).Within(0.03f));
        }

        [Test]
        public void Perturb_ConcentratesOnTheCore_AtHigherBias()
        {
            // bias 1.0 is the "dense core, thin halo" authoring option: the thing you are
            // aiming at still soaks most of the fire.
            const float half = 4f;
            const int n = 20000;
            int uniformInner = 0, biasedInner = 0;

            for (uint i = 0; i < n; i++)
            {
                if (Vector3.Angle(Vector3.forward, GunSpreadMath.Perturb(Vector3.forward, half, 0.5f, i)) < half * 0.5f)
                    uniformInner++;
                if (Vector3.Angle(Vector3.forward, GunSpreadMath.Perturb(Vector3.forward, half, 1.0f, i)) < half * 0.5f)
                    biasedInner++;
            }

            Assert.That(biasedInner, Is.GreaterThan(uniformInner),
                "a higher bias must pull rounds toward the middle of the cone");
        }

        [Test]
        public void Perturb_RollIsUnbiased_AroundTheAimAxis()
        {
            // No preferred direction: the cone must be a cone, not a fan.
            var sum = Vector3.zero;
            const int n = 20000;
            for (uint i = 0; i < n; i++)
                sum += GunSpreadMath.Perturb(Vector3.forward, 4f, 0.5f, i);

            var mean = sum / n;
            Assert.That(Mathf.Abs(mean.x), Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(mean.y), Is.LessThan(0.01f));
        }

        // ------------------------------------------------------------------ the deflection

        [Test]
        public void DeflectionOf_CarriesTheMuzzleForwardOntoTheShot()
        {
            var from = new Vector3(0.2f, -0.4f, 1f).normalized;
            var to = GunSpreadMath.Perturb(from, 4f, 0.5f, 42);

            var rotated = GunSpreadMath.DeflectionOf(from, to) * from;
            Assert.That(Vector3.Angle(rotated, to), Is.LessThan(1e-2f));
        }

        [Test]
        public void DeflectionOf_PreservesRoll_AboutTheAimAxis()
        {
            // Composing the deflection onto the muzzle pose must not re-reference roll to world
            // up the way LookRotation would — a turret prism's long axis IS the shot, so a roll
            // change is visible as a twist.
            var muzzle = Quaternion.Euler(15f, -40f, 63f);
            var forward = muzzle * Vector3.forward;
            var aim = GunSpreadMath.Perturb(forward, 4f, 0.5f, 7);

            var shotRotation = GunSpreadMath.DeflectionOf(forward, aim) * muzzle;

            // The shot points down the deflected aim …
            Assert.That(Vector3.Angle(shotRotation * Vector3.forward, aim), Is.LessThan(1e-2f));
            // … and its up vector has moved by no more than the deflection itself.
            Assert.That(Vector3.Angle(shotRotation * Vector3.up, muzzle * Vector3.up),
                Is.LessThanOrEqualTo(4f + 1e-2f));
        }
    }
}
