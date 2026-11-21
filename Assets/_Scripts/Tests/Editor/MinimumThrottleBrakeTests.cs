using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The rule under test: on a two-thumb flier that authors no speed floor, bringing the
    /// throttle to its minimum has to END in a stop. The exponential tracking the fleet has
    /// always used cannot do that on its own — it approaches zero and never arrives — so
    /// <see cref="MinimumThrottleBrake"/> owns the last stretch.
    ///
    /// Mirrors VesselTransformer.StepTowardTarget's composition exactly (lerp, then brake), so
    /// these are statements about the shipped path rather than about a re-derivation of it.
    /// </summary>
    [TestFixture]
    public class MinimumThrottleBrakeTests
    {
        const float Lerp = 1.5f;      // VesselTransformer.LERP_AMOUNT
        const float Dt = 1f / 60f;
        const float DolphinCruise = 68f;   // Dolphin.prefab DefaultThrottleScaler
        // The Rhino authored 10 here until this branch zeroed it so it could be stopped;
        // the hulls that still author a floor are the one-thumb ones (Sparrow, Serpent,
        // Falcon, Shrike, Termite), whose target can never be zero anyway.
        const float AuthoredFloor = 10f;   // Sparrow.prefab DefaultMinimumSpeed

        static float Step(float current, float target, float throttleScaler, float brakeSeconds)
        {
            float stepped = Mathf.Lerp(current, target, Lerp * Dt);
            return MinimumThrottleBrake.Apply(
                stepped, current, target,
                MinimumThrottleBrake.RateFor(throttleScaler, brakeSeconds), Dt);
        }

        /// <summary>Run the step until the speed stops changing, and report how long that took.
        /// Returns -1 when it never settles, which IS the legacy exponential's answer.</summary>
        static float SecondsToStop(float from, float target, float throttleScaler, float brakeSeconds,
                                   float maxSeconds = 30f)
        {
            float s = from;
            int frames = Mathf.CeilToInt(maxSeconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                s = Step(s, target, throttleScaler, brakeSeconds);
                if (s <= 0f) return (i + 1) * Dt;
            }
            return -1f;
        }

        [Test]
        public void ExponentialAlone_NeverReachesZero()
        {
            // The defect, stated as a test: with the brake off, a zero target is an asymptote.
            Assert.AreEqual(-1f, SecondsToStop(DolphinCruise, 0f, DolphinCruise, brakeSeconds: 0f),
                "Without the brake the exponential tail must be shown never to land — if this "
                + "starts passing, the premise of MinimumThrottleBrake has changed.");
        }

        [Test]
        public void TheVisibleFall_IsBitIdenticalToTheLegacyExponential()
        {
            // THE DESIGN, not the consequence. Every other test here asserts that a stop
            // HAPPENS, which a brake composed the wrong way also satisfies: subtracting the
            // constant rate from the already-stepped value applies both every frame and runs
            // ~40% under the legacy curve half a second in, i.e. a different deceleration feel on
            // every affected hull — including the Squirrel, which was the reference for CORRECT
            // behaviour when this work was asked for. The whole of the fall the pilot can see
            // must be the curve that shipped, to the bit.
            foreach (float scaler in new[] { 60f, 68f, 180f })   // Squirrel, Dolphin, Manta
            {
                float crossover = MinimumThrottleBrake.RateFor(scaler, MinimumThrottleBrake.DefaultBrakeSeconds) / Lerp;
                Assert.Less(crossover, scaler,
                    "the crossover must sit below cruise, or the brake owns the visible fall");

                for (float v = scaler; v > crossover; v -= scaler / 32f)
                {
                    Assert.AreEqual(
                        Step(v, 0f, scaler, brakeSeconds: 0f),
                        Step(v, 0f, scaler, MinimumThrottleBrake.DefaultBrakeSeconds),
                        0f,
                        $"above the crossover ({crossover:F1} u/s at scaler {scaler}) the exponential "
                        + $"must win outright; it did not at {v:F1} u/s.");
                }
            }
        }

        [Test]
        public void BelowTheCrossover_TheConstantRateTakesOver()
        {
            // The other half of the same statement: the brake must actually be doing something
            // down there, or the composition above is just the legacy exponential with extra steps.
            const float scaler = 60f;
            float crossover = MinimumThrottleBrake.RateFor(scaler, MinimumThrottleBrake.DefaultBrakeSeconds) / Lerp;
            float v = crossover * 0.5f;
            Assert.Less(Step(v, 0f, scaler, MinimumThrottleBrake.DefaultBrakeSeconds),
                        Step(v, 0f, scaler, brakeSeconds: 0f),
                        "below the crossover the constant rate must be the stronger of the two");
        }

        [Test]
        public void ZeroTarget_ReachesExactlyZero_FromCruise()
        {
            float t = SecondsToStop(DolphinCruise, 0f, DolphinCruise, MinimumThrottleBrake.DefaultBrakeSeconds);
            Assert.Greater(t, 0f, "minimum throttle must end in a stop");
            Assert.Less(t, 1.5f,
                "and it must land promptly enough to read as a stop. Measured 1.40 s at "
                + "DefaultBrakeSeconds 2 - if this fails, the composition or the rate moved.");
        }

        [Test]
        public void ZeroTarget_ReachesExactlyZero_FromFullBoost()
        {
            // A boosted Dolphin peaks near 347 u/s; that is the longest fall in the fleet's
            // two-thumb roster and the one the pilot actually complained about.
            float t = SecondsToStop(347f, 0f, DolphinCruise, MinimumThrottleBrake.DefaultBrakeSeconds);
            Assert.Greater(t, 0f);
            Assert.Less(t, 3f,
                "a full-boost stop must still be a stop, not a coast. Measured 2.47 s from 347 u/s.");
        }

        [Test]
        public void NonZeroTarget_IsUntouched()
        {
            // The Rhino authors a 10 u/s floor, so its minimum-throttle target is 10, not 0 —
            // and HeadlongCircuit's whole corner model is derived from that floor. The brake
            // must not be able to reach it.
            float withBrake = Step(DolphinCruise, AuthoredFloor, 50f, MinimumThrottleBrake.DefaultBrakeSeconds);
            float without = Step(DolphinCruise, AuthoredFloor, 50f, brakeSeconds: 0f);
            Assert.AreEqual(without, withBrake, 1e-6f);
        }

        [Test]
        public void DeceleratingTowardALowerCruise_IsUntouched()
        {
            float withBrake = Step(DolphinCruise, 40f, DolphinCruise, MinimumThrottleBrake.DefaultBrakeSeconds);
            float without = Step(DolphinCruise, 40f, DolphinCruise, brakeSeconds: 0f);
            Assert.AreEqual(without, withBrake, 1e-6f);
        }

        [Test]
        public void AcceleratingFromAStop_IsUntouched()
        {
            // Recoverability: throttling back up from a dead stop must be the ordinary lerp.
            float withBrake = Step(0f, DolphinCruise, DolphinCruise, MinimumThrottleBrake.DefaultBrakeSeconds);
            float without = Step(0f, DolphinCruise, DolphinCruise, brakeSeconds: 0f);
            Assert.AreEqual(without, withBrake, 1e-6f);
            Assert.Greater(withBrake, 0f);
        }

        [Test]
        public void NegativeCurrent_StandsDown()
        {
            // The vector model hands the nose COMPONENT here, which is negative while the
            // velocity points behind the nose. Braking that would add forward thrust.
            float stepped = Mathf.Lerp(-20f, 0f, Lerp * Dt);
            Assert.AreEqual(stepped,
                MinimumThrottleBrake.Apply(stepped, -20f, 0f, MinimumThrottleBrake.RateFor(68f, 2f), Dt),
                1e-6f);
        }

        [Test]
        public void UnseededThrottleScaler_YieldsNoBrake()
        {
            // ThrottleScaler is 0 until ResetTransformer seeds it from DefaultThrottleScaler.
            Assert.AreEqual(0f, MinimumThrottleBrake.RateFor(0f, 2f));
            Assert.AreEqual(0f, MinimumThrottleBrake.RateFor(68f, 0f));
        }

        [Test]
        public void RateIsOneCruisePerBrakeSecond()
        {
            Assert.AreEqual(34f, MinimumThrottleBrake.RateFor(DolphinCruise, 2f), 1e-4f);
            Assert.AreEqual(90f, MinimumThrottleBrake.RateFor(180f, 2f), 1e-4f);  // Manta
        }

        [Test]
        public void FasterHullsStopInAComparableTime()
        {
            // The rate is the vessel's OWN cruise, so a 180 u/s Manta does not coast three times
            // as long as a 68 u/s Dolphin.
            float dolphin = SecondsToStop(DolphinCruise, 0f, DolphinCruise, MinimumThrottleBrake.DefaultBrakeSeconds);
            float manta = SecondsToStop(180f, 0f, 180f, MinimumThrottleBrake.DefaultBrakeSeconds);
            Assert.AreEqual(dolphin, manta, 0.05f);
        }
    }
}
