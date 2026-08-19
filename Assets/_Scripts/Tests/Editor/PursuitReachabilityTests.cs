#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The gate for the AI's orbit break (<see cref="PursuitReachability"/>, <see cref="OrbitDetector"/>).
    ///
    /// These are PROPERTY tests rather than value tests, because the thing being asserted is that a
    /// piece of algebra is equivalent to a geometric definition — and the failure mode of getting
    /// that wrong is not a crash or a wrong number, it is an AI that quietly resumes orbiting its
    /// objective forever, which no other test in this project would notice.
    ///
    /// The end-to-end case at the bottom is the one that matters most: it flies the shipped
    /// predicates through a pursuit loop and asserts that a configuration pure pursuit provably
    /// cannot solve is solved with the break-off in.
    /// </summary>
    public class PursuitReachabilityTests
    {
        // Deterministic: a property test that fails only on some seeds is a property test nobody
        // will trust or act on.
        const int Seed = 20260819;

        /// <summary>
        /// The DIRECT geometric statement, deliberately written the long way and independent of the
        /// shipped algebra: the target is inside the turning circle when it is within R of that
        /// circle's centre. <see cref="PursuitReachability.IsInsideTurningCircle"/> is the same
        /// claim with the R² cancelled, and this is what proves the cancellation.
        /// </summary>
        static bool ReferenceInsideCircle(Vector3 toTarget, Vector3 heading, float radius)
        {
            Vector3 forward = heading.normalized;
            Vector3 perpendicular = toTarget - forward * Vector3.Dot(toTarget, forward);
            if (perpendicular.sqrMagnitude <= 1e-12f) return false;
            Vector3 centre = perpendicular.normalized * radius;
            return (toTarget - centre).magnitude < radius;
        }

        [Test]
        public void TurningCircleTest_IsExactlyTheCircleItClaimsToBe()
        {
            var rng = new System.Random(Seed);
            float Range(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            Vector3 Vec(float s) => new Vector3(Range(-s, s), Range(-s, s), Range(-s, s));

            int inside = 0, disagreements = 0;
            const int cases = 20000;
            for (int i = 0; i < cases; i++)
            {
                Vector3 toTarget = Vec(300f), heading = Vec(1f);
                float radius = Range(1f, 300f);

                bool shipped = PursuitReachability.IsInsideTurningCircle(toTarget, heading, radius);
                if (shipped) inside++;
                if (shipped != ReferenceInsideCircle(toTarget, heading, radius)) disagreements++;
            }

            Assert.Greater(inside, cases / 10,
                "Almost nothing tested as unreachable — the case distribution has drifted and this " +
                "test is no longer exercising the branch it exists to check.");
            Assert.AreEqual(0, disagreements,
                $"{disagreements} of {cases} cases disagree with the direct circle definition. The " +
                "shipped test is |d|² < 2R·|d⊥|, which is |target − C| < R with the R² cancelled; a " +
                "disagreement means the cancellation is wrong and the AI's whole reachability model " +
                "is describing some other shape.");
        }

        [Test]
        public void BeyondTwoRadii_NothingIsEverUnreachable()
        {
            // This is the property the break-off's exit condition rests on: sin θ ≤ 1, so
            // |d| < 2R·sin θ cannot hold once |d| exceeds 2R. If it can, "fly out to 2R" stops
            // being a guarantee and the AI can leave a break-off still trapped.
            var rng = new System.Random(Seed + 1);
            float Range(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            Vector3 Vec(float s) => new Vector3(Range(-s, s), Range(-s, s), Range(-s, s));

            for (int i = 0; i < 20000; i++)
            {
                float radius = Range(1f, 300f);
                Vector3 direction = Vec(1f);
                if (direction.sqrMagnitude < 0.25f) continue;

                float separation = PursuitReachability.GuaranteedReachableSeparation(radius);
                Vector3 toTarget = direction.normalized * (separation * Range(1.001f, 6f));

                Assert.IsFalse(PursuitReachability.IsInsideTurningCircle(toTarget, Vec(1f), radius),
                    $"A target {toTarget.magnitude:F2} units away (past 2R = {separation:F2}) reported " +
                    "as inside the turning circle. The break-off's exit condition assumes this cannot " +
                    "happen.");
            }
        }

        [Test]
        public void MinTurnRadius_MatchesSpeedOverAngularRate()
        {
            // A Dolphin: 110°/s authored on the prefab. Expected values are computed with
            // Mathf.Deg2Rad, NOT with exact π/180 — the float32 constant differs in the 8th digit
            // and a value hand-computed from π misses by 0.01 at these magnitudes.
            Assert.AreEqual(80f / (110f * Mathf.Deg2Rad),
                            PursuitReachability.MinTurnRadius(80f, 110f), 1e-3f);
            Assert.AreEqual(41.67f, PursuitReachability.MinTurnRadius(80f, 110f), 0.01f,
                "A Dolphin at cruise turns inside about 42 units.");
            Assert.AreEqual(156.26f, PursuitReachability.MinTurnRadius(300f, 110f), 0.01f,
                "The radius scales with SPEED — that is why it cannot be an authored constant. " +
                "The same vessel needs 3.75x the room at 300 u/s that it needs at 80.");

            Assert.AreEqual(0f, PursuitReachability.MinTurnRadius(0f, 110f),
                "A stationary vessel can turn in place, so nothing is unreachable.");
            Assert.IsTrue(float.IsInfinity(PursuitReachability.MinTurnRadius(80f, 0f)),
                "A vessel that cannot turn has an infinite turning circle.");
        }

        [Test]
        public void EscapeDirection_AlwaysLeadsAwayFromTheTarget()
        {
            var rng = new System.Random(Seed + 2);
            float Range(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            Vector3 Vec(float s) => new Vector3(Range(-s, s), Range(-s, s), Range(-s, s));

            for (int i = 0; i < 5000; i++)
            {
                Vector3 toTarget = Vec(300f), heading = Vec(1f);
                if (toTarget.sqrMagnitude < 1f || heading.sqrMagnitude < 0.25f) continue;

                Vector3 escape = PursuitReachability.EscapeDirection(toTarget, heading, 0.35f);

                Assert.AreEqual(1f, escape.magnitude, 1e-3f, "Escape direction must be a unit vector.");
                Assert.GreaterOrEqual(
                    Vector3.Dot(escape, -toTarget.normalized),
                    Vector3.Dot(heading.normalized, -toTarget.normalized) - 1e-4f,
                    "Biasing the heading away from the target must never point it MORE toward the " +
                    "target than simply holding course would have.");
            }
        }

        [Test]
        public void OrbitDetector_FiresOnAStalledOrbitAndStaysQuietWhileClosing()
        {
            var stalled = new OrbitDetector();
            stalled.Reset();
            bool fired = false;
            for (int i = 0; i < 4000 && !fired; i++)
            {
                float a = i * 0.02f;
                fired = stalled.Tick(new Vector3(100f * Mathf.Cos(a), 0f, 100f * Mathf.Sin(a)),
                                     540f, 0.9f, 1.6f);
            }
            Assert.IsTrue(fired, "A pursuer circling at constant range is the definition of the orbit " +
                                 "this detector exists to notice.");

            // A genuinely closing spiral must stay quiet for the whole of its approach. The loop
            // stops when it arrives — past that the range is constant, which IS an orbit and should
            // fire, and an earlier version of this test ran on and reported that as a false positive.
            var closing = new OrbitDetector();
            closing.Reset();
            for (int i = 0; i < 4000; i++)
            {
                float a = i * 0.02f, r = 400f - i * 0.4f;
                if (r <= 8f) break;
                Assert.IsFalse(
                    closing.Tick(new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a)), 540f, 0.9f, 1.6f),
                    $"Reported an orbit at frame {i} while the range was still falling ({r:F1}u). The " +
                    "progress gate must compare against the range at the START of the accumulation " +
                    "window — a running minimum tracks the approach down and can never register " +
                    "progress at all.");
            }
        }

        [Test]
        public void OrbitDetector_ForgetsTheOldObjectiveWhenTheTargetIsReplaced()
        {
            var detector = new OrbitDetector();
            detector.Reset();
            for (int i = 0; i < 200; i++)
            {
                float a = i * 0.02f;
                detector.Tick(new Vector3(100f * Mathf.Cos(a), 0f, 100f * Mathf.Sin(a)), 540f, 0.9f, 1.6f);
            }
            Assert.Greater(detector.SweptDegrees, 0f, "Precondition: some sweep has accumulated.");

            detector.Tick(new Vector3(0f, 0f, 5000f), 540f, 0.9f, 1.6f);
            Assert.AreEqual(0f, detector.SweptDegrees,
                "A crystal being collected and another selected teleports the bearing and the range. " +
                "The sweep accumulated around the old objective says nothing about the new one, and " +
                "carrying it over would send the AI on a break-off it never earned.");
        }

        // ---------------------------------------------------------------------------------------
        // End to end
        // ---------------------------------------------------------------------------------------

        /// <summary>One frame of the shipped AIPilot rule, reduced to the parts that steer.</summary>
        static bool Pursue(Vector3 target, float speed, float turnRateDeg, bool breakOrbits, out float seconds)
        {
            const float dt = 1f / 60f, arrivalRadius = 8f, timeout = 40f;
            const float awayBias = 0.35f, exitMargin = 1.15f, exitHysteresis = 0.75f;
            const float minExtend = 0.6f, maxExtend = 4f;

            Vector3 position = Vector3.zero, heading = Vector3.forward;
            float radius = PursuitReachability.MinTurnRadius(speed, turnRateDeg);
            float exitSeparation = PursuitReachability.GuaranteedReachableSeparation(radius) * exitMargin;

            bool extending = false;
            float extendElapsed = 0f;
            var detector = new OrbitDetector();
            detector.Reset();

            for (int step = 0; step < (int)(timeout / dt); step++)
            {
                Vector3 toTarget = target - position;
                float distance = toTarget.magnitude;
                if (distance < arrivalRadius) { seconds = step * dt; return true; }

                Vector3 steer = toTarget.normalized;
                if (breakOrbits)
                {
                    if (extending)
                    {
                        extendElapsed += dt;
                        bool clear = extendElapsed >= minExtend &&
                                     !PursuitReachability.IsInsideTurningCircle(
                                         toTarget, heading, radius * exitHysteresis);
                        if (distance > exitSeparation || clear || extendElapsed >= maxExtend)
                        {
                            extending = false;
                            extendElapsed = 0f;
                            detector.Reset();
                        }
                    }
                    else if (PursuitReachability.IsInsideTurningCircle(toTarget, heading, radius) ||
                             detector.Tick(toTarget, 540f, 0.9f, 1.6f))
                    {
                        extending = true;
                        extendElapsed = 0f;
                        detector.Reset();
                    }

                    if (extending)
                        steer = PursuitReachability.EscapeDirection(toTarget, heading, awayBias);
                }

                heading = Vector3.RotateTowards(heading, steer, turnRateDeg * Mathf.Deg2Rad * dt, 0f).normalized;
                position += heading * (speed * dt);
            }

            seconds = timeout;
            return false;
        }

        [Test]
        public void PurePursuit_Orbits_AndTheBreakOffSolvesIt()
        {
            const float speed = 80f, turnRate = 110f;   // a Dolphin at cruise
            float radius = PursuitReachability.MinTurnRadius(speed, turnRate);

            // The worst case there is: the objective sitting exactly at the centre of the circle the
            // vessel is about to fly. No turn can ever reach it.
            var target = new Vector3(radius, 0f, 0f);

            Assert.IsFalse(Pursue(target, speed, turnRate, breakOrbits: false, out _),
                "Precondition: pure pursuit must fail here. If it now succeeds, the vessel model in " +
                "this test has drifted from the one AIPilot flies and the rest of this assertion is " +
                "meaningless.");

            Assert.IsTrue(Pursue(target, speed, turnRate, breakOrbits: true, out float seconds),
                "The break-off did not reach an objective that pure pursuit provably cannot. This is " +
                "the entire point of PursuitReachability.");
            Assert.Less(seconds, 10f, $"Reached it, but took {seconds:F2}s — far longer than the ~3.6s " +
                                      "the manoeuvre measures at, which suggests it is thrashing in " +
                                      "and out of the break-off rather than flying one.");
        }

        [Test]
        public void TheBreakOff_ReachesObjectivesPurePursuitCannot_AcrossARandomField()
        {
            const float speed = 80f, turnRate = 110f;
            float radius = PursuitReachability.MinTurnRadius(speed, turnRate);
            var rng = new System.Random(Seed + 3);
            float Range(float a, float b) => a + (b - a) * (float)rng.NextDouble();

            int pureHits = 0, brokenHits = 0;
            const int trials = 120;
            for (int i = 0; i < trials; i++)
            {
                float polar = Range(0f, Mathf.PI), azimuth = Range(0f, 2f * Mathf.PI);
                float distance = Range(0.15f * radius, 5f * radius);
                var target = new Vector3(distance * Mathf.Sin(polar) * Mathf.Cos(azimuth),
                                         distance * Mathf.Sin(polar) * Mathf.Sin(azimuth),
                                         distance * Mathf.Cos(polar));

                if (Pursue(target, speed, turnRate, false, out _)) pureHits++;
                if (Pursue(target, speed, turnRate, true, out _)) brokenHits++;
            }

            Assert.AreEqual(trials, brokenHits,
                $"The break-off reached only {brokenHits}/{trials} objectives. It should reach all of " +
                "them: past 2R of separation every objective is reachable, and the break-off's exit " +
                "condition is exactly that separation.");
            Assert.Greater(brokenHits, pureHits,
                $"Pure pursuit reached {pureHits}/{trials} and the break-off {brokenHits}/{trials}. " +
                "If they are equal, the field is no longer sampling the region where bounded turn " +
                "radius bites and this test has stopped measuring anything.");
        }
    }
}
#endif
