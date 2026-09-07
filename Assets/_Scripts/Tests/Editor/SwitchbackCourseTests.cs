using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Switchback course contract. Every gate is randomly placed and randomly oriented, and
    /// the whole design rests on four things being true anyway - the course fits the cell, no
    /// corner is sharper than a Dolphin can fly, no gate stands edge-on to the line you arrive
    /// on, and no two mouths are close enough to be confused. Those are asserted here across a
    /// 400-seed sweep of every shipped intensity rather than eyeballed in the editor, because a
    /// generated course is exactly the kind of thing that is fine on the seed you looked at.
    /// </summary>
    public class SwitchbackCourseTests
    {
        // The Skim Race cell, measured: Nucleus.prefab at scale 400 is 391.9u, and
        // CapsuleMembrane authors radius 1200. The controller derives the same shell.
        const float Inner = 480f;
        const float Outer = 1080f;
        const int Gates = 20;
        const int Seeds = 400;

        // Dolphin at BOOST: 347 u/s over a speed-independent 110 deg/s. The state in which a
        // racer is least able to correct, so the one a course has to be flyable in.
        const float BoostTurnRadius = 180.7f;

        static SwitchbackCourseSettings Settings(int intensity)
        {
            var s = SwitchbackCourseSettings.ForIntensity(intensity);
            s.GateCount = Gates;
            s.InnerRadius = Inner;
            s.OuterRadius = Outer;
            s.FirstGateDirection = Vector3.up;
            s.FirstGateDistance = 620f;
            return s;
        }

        static List<SwitchbackGate> Course(int intensity, int seed) =>
            SwitchbackCourse.Generate(seed, Settings(intensity));

        // ── The walk always terminates ───────────────────────────────────

        [Test]
        public void EverySeedProducesAFullCourse([Values(1, 2, 3, 4)] int intensity)
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var course = Course(intensity, seed);
                Assert.IsNotNull(course, $"intensity {intensity} seed {seed} produced no course.");
                Assert.AreEqual(Gates, course.Count,
                    $"intensity {intensity} seed {seed} produced {course.Count} gates.");
            }
        }

        // ── It fits the cell ─────────────────────────────────────────────

        [Test]
        public void EveryGateSitsInsideTheCourseShell([Values(1, 2, 3, 4)] int intensity)
        {
            for (int seed = 1; seed <= Seeds; seed++)
                foreach (var gate in Course(intensity, seed))
                {
                    float r = gate.Position.magnitude;
                    Assert.GreaterOrEqual(r, Inner - 0.01f,
                        $"intensity {intensity} seed {seed}: a gate at {r:F1} is inside the nucleus.");
                    Assert.LessOrEqual(r, Outer + 0.01f,
                        $"intensity {intensity} seed {seed}: a gate at {r:F1} is outside the membrane.");
                }
        }

        [Test]
        public void GateOneSitsOnTheSpawnFormationPole([Values(1, 2, 3, 4)] int intensity)
        {
            // The fairness rule: pilots spawn on an EQUATORIAL ring, so a first gate on that
            // ring's axis is exactly equidistant from all of them. Anywhere else and whoever
            // spawned nearest starts the race ahead.
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var first = Course(intensity, seed)[0].Position;
                Assert.AreEqual(0f, first.x, 0.01f, $"seed {seed}: gate 1 is off the pole in x.");
                Assert.AreEqual(0f, first.z, 0.01f, $"seed {seed}: gate 1 is off the pole in z.");
                Assert.Greater(first.y, 0f, $"seed {seed}: gate 1 is on the wrong pole.");
            }
        }

        // ── It is flyable ────────────────────────────────────────────────

        [Test]
        public void NoCornerExceedsTheTurnCap([Values(1, 2, 3, 4)] int intensity)
        {
            float cap = SwitchbackCourseSettings.ForIntensity(intensity).MaxTurnDegrees;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, seed);
                for (int i = 1; i < c.Count - 1; i++)
                {
                    Vector3 inbound = (c[i].Position - c[i - 1].Position).normalized;
                    Vector3 outbound = (c[i + 1].Position - c[i].Position).normalized;
                    float turn = SwitchbackCourse.Angle(inbound, outbound);
                    Assert.LessOrEqual(turn, cap + 0.05f,
                        $"intensity {intensity} seed {seed} gate {i}: {turn:F1} deg corner exceeds the {cap} cap.");
                }
            }
        }

        [Test]
        public void NoGateStandsEdgeOnToTheLineYouArriveOn([Values(1, 2, 3, 4)] int intensity)
        {
            // A gate whose axis is perpendicular to the flight line is a slot, not a gate. The
            // jitter budget is what makes this hold: a corner spends half its turn on the
            // presentation, and the jitter can only spend what is left.
            float cap = SwitchbackCourseSettings.ForIntensity(intensity).MaxPresentDegrees;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, seed);
                for (int i = 1; i < c.Count; i++)
                {
                    Vector3 arrive = (c[i].Position - c[i - 1].Position).normalized;
                    float present = SwitchbackCourse.Angle(arrive, c[i].Axis);
                    if (present > 90f) present = 180f - present;     // a ring is threadable both ways
                    Assert.LessOrEqual(present, cap + 0.05f,
                        $"intensity {intensity} seed {seed} gate {i}: presents {present:F1} deg off the arriving line.");
                }
            }
        }

        [Test]
        public void EveryCornerClearsTheDolphinsTurningCircleAtBoost([Values(1, 2, 3, 4)] int intensity)
        {
            // Dubins: pure pursuit cannot reach a target inside a circle of radius R tangent to
            // its velocity, so a leg shorter than 2R*sin(turn) is a corner nobody can make -
            // human or AI - and no amount of turning harder fixes it.
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, seed);
                for (int i = 1; i < c.Count - 1; i++)
                {
                    Vector3 inbound = (c[i].Position - c[i - 1].Position).normalized;
                    Vector3 outbound = (c[i + 1].Position - c[i].Position).normalized;
                    float turn = SwitchbackCourse.Angle(inbound, outbound) * Mathf.Deg2Rad;
                    float leg = (c[i + 1].Position - c[i].Position).magnitude;
                    float needed = 2f * BoostTurnRadius * Mathf.Sin(turn);
                    Assert.Greater(leg, needed,
                        $"intensity {intensity} seed {seed} gate {i}: leg {leg:F0} is inside the " +
                        $"boosted turning circle ({needed:F0} needed).");
                }
            }
        }

        // ── The gates cannot be confused ─────────────────────────────────

        [Test]
        public void NoTwoMouthsComeWithinARingDiameter([Values(1, 2, 3, 4)] int intensity)
        {
            float ring = SwitchbackCourseSettings.ForIntensity(intensity).RingRadius;
            float floor = 2f * ring;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var c = Course(intensity, seed);
                for (int i = 0; i < c.Count; i++)
                    for (int j = i + 1; j < c.Count; j++)
                    {
                        float d = (c[i].Position - c[j].Position).magnitude;
                        Assert.Greater(d, floor,
                            $"intensity {intensity} seed {seed}: gates {i} and {j} are {d:F0} apart, " +
                            $"closer than the {floor:F0} their mouths span.");
                    }
            }
        }

        // ── It is reproducible ───────────────────────────────────────────

        [Test]
        public void TheSameSeedAlwaysProducesTheSameCourse()
        {
            var a = Course(3, 987654);
            var b = Course(3, 987654);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position, $"gate {i} position drifted between runs.");
                Assert.AreEqual(a[i].Axis, b[i].Axis, $"gate {i} axis drifted between runs.");
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentCourses()
        {
            var a = Course(3, 11);
            var b = Course(3, 12);
            Assert.AreNotEqual(a[1].Position, b[1].Position, "two seeds produced the same course.");
        }

        // ── The shipped ladder is a ladder ───────────────────────────────

        [Test]
        public void IntensityTightensTheCourseMonotonically()
        {
            // Intensity is the COURSE, not the arena: mouths narrow, corners sharpen, gates
            // twist further off the line. If any of these ever stops moving in one direction,
            // the ladder has stopped meaning anything.
            for (int i = 1; i < 4; i++)
            {
                var lo = SwitchbackCourseSettings.ForIntensity(i);
                var hi = SwitchbackCourseSettings.ForIntensity(i + 1);
                Assert.Less(hi.RingRadius, lo.RingRadius, $"ring radius did not tighten from {i} to {i + 1}.");
                Assert.Greater(hi.MaxTurnDegrees, lo.MaxTurnDegrees, $"corners did not sharpen from {i} to {i + 1}.");
                Assert.Greater(hi.AxisJitterDegrees, lo.AxisJitterDegrees, $"gates did not twist further from {i} to {i + 1}.");
                Assert.Less(hi.MinStep, lo.MinStep, $"legs did not shorten from {i} to {i + 1}.");
            }
        }

        [Test]
        public void EveryLegClearsTheAiApproachRun()
        {
            // AIPilot breaks off and re-attacks when it cannot turn onto its objective, and its
            // escape leg is sized off the approach run. A leg shorter than the run at cruise
            // (68 u/s x 2.5s = 170u, floored by the 2R-c reachability distance of ~360u at boost)
            // would have an AI peeling away between consecutive gates for the whole race.
            const float AiApproachRunFloor = 360f;
            for (int i = 1; i <= 4; i++)
                Assert.GreaterOrEqual(SwitchbackCourseSettings.ForIntensity(i).MinStep, AiApproachRunFloor,
                    $"intensity {i}'s shortest leg is inside the AI's approach run.");
        }
        // ── The mouth ladder ─────────────────────────────────────────────

        /// <summary>
        /// The tightest mouth is sized off the SHIP, not off a number somebody liked. Intensity 4
        /// is "barely bigger than a Dolphin", so it must clear the measured hull and must not
        /// clear it by much - a mouth that has quietly grown back to several ship-widths is the
        /// failure this catches, and it does not throw, it just stops being intensity 4.
        /// </summary>
        [Test]
        public void TightestMouthIsBarelyBiggerThanTheShip()
        {
            float r4 = SwitchbackCourseSettings.ForIntensity(4).RingRadius;
            float hull = SwitchbackCourseSettings.DolphinHullRadius;

            Assert.Greater(r4, hull, "intensity 4's mouth is smaller than the ship - unflyable.");
            Assert.LessOrEqual(r4, hull * 2f,
                $"intensity 4's mouth is {r4 / hull:F2} ship-radii; 'barely bigger' is under 2.");
        }

        [Test]
        public void WidestMouthIsUnchanged()
        {
            // Play-tested and explicitly kept - only the tightening below it was asked for.
            Assert.AreEqual(72f, SwitchbackCourseSettings.ForIntensity(1).RingRadius, 0.001f);
        }

        [Test]
        public void MouthLadderIsGeometric()
        {
            // Equal ratios means every step is the same increment of difficulty. Asserted rather
            // than trusted because the ladder is computed, and a linear ramp between the same two
            // ends would pass every other test here while feeling like three flat levels and a
            // cliff.
            float r1 = SwitchbackCourseSettings.ForIntensity(1).RingRadius;
            float r2 = SwitchbackCourseSettings.ForIntensity(2).RingRadius;
            float r3 = SwitchbackCourseSettings.ForIntensity(3).RingRadius;
            float r4 = SwitchbackCourseSettings.ForIntensity(4).RingRadius;

            Assert.AreEqual(r2 / r1, r3 / r2, 1e-4f);
            Assert.AreEqual(r3 / r2, r4 / r3, 1e-4f);
        }

        [Test]
        public void EveryMouthStillClearsTheShip([Values(1, 2, 3, 4)] int intensity)
        {
            Assert.Greater(SwitchbackCourseSettings.ForIntensity(intensity).RingRadius,
                           SwitchbackCourseSettings.DolphinHullRadius,
                           $"intensity {intensity} cannot be flown through.");
        }

    }
}
