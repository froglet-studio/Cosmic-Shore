using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Riposte AI's plan (<see cref="RiposteDuel"/>, RIPOSTE.md section 3): the three legs follow each other the way
    /// the doc says, and every leg's end is a point a Swordfish can fly a straight line to.
    /// </summary>
    public class RiposteDuelTests
    {
        const RiposteDuel.Phase Charge = RiposteDuel.Phase.Charge;
        const RiposteDuel.Phase Extend = RiposteDuel.Phase.Extend;
        const RiposteDuel.Phase Reap = RiposteDuel.Phase.Reap;

        static RiposteDuel.Phase Next(RiposteDuel.Phase phase, float fuel, float below = 0.06f, float until = 0.15f,
                                      bool hasRival = true, bool passed = false, bool arrived = false,
                                      bool timedOut = false) =>
            RiposteDuel.Next(phase, fuel, below, until, hasRival, passed, arrived, timedOut);

        [Test]
        public void ACharge_GoesOn_UntilItPassesItsRival()
        {
            Assert.AreEqual(Charge, Next(Charge, 0.5f));
            Assert.AreEqual(Extend, Next(Charge, 0.5f, passed: true));
        }

        [Test]
        public void ACharge_ThatNeverCloses_RunsOutInsteadOfCircling()
        {
            Assert.AreEqual(Extend, Next(Charge, 0.5f, timedOut: true));
        }

        [Test]
        public void AnEmptyTank_NeverAbandonsAChargeInFlight()
        {
            // Fuel is checked between legs: breaking off a charge to cut sticks hands the rival the faster hull.
            Assert.AreEqual(Charge, Next(Charge, 0f));
        }

        [Test]
        public void AfterTheRunOut_ALowTankGoesCutting_AndAFullOneChargesAgain()
        {
            Assert.AreEqual(Extend, Next(Extend, 0f));
            Assert.AreEqual(Reap, Next(Extend, 0.01f, arrived: true));
            Assert.AreEqual(Charge, Next(Extend, 0.2f, arrived: true));
            Assert.AreEqual(Reap, Next(Extend, 0.01f, timedOut: true));
        }

        [Test]
        public void Cutting_LastsUntilTheTankItWanted_ThenCharges()
        {
            Assert.AreEqual(Reap, Next(Reap, 0.1f));
            Assert.AreEqual(Reap, Next(Reap, 0.1f, arrived: true), "a line that ended short is followed by another");
            Assert.AreEqual(Charge, Next(Reap, 0.15f));
        }

        [Test]
        public void APilotThatNeverCutsOnPurpose_NeverEntersReap()
        {
            // Easy: reapBelow 0. Every transition from every leg, at an empty tank.
            foreach (var phase in new[] { Charge, Extend })
                foreach (bool flag in new[] { false, true })
                    Assert.AreNotEqual(Reap, RiposteDuel.Next(phase, 0f, 0f, 0f, flag, flag, flag, flag),
                                       $"{phase} with every flag {flag}");
            Assert.AreEqual(Charge, RiposteDuel.Next(Reap, 0f, 0f, 0f, true, false, false, false),
                            "and one that finds itself cutting leaves at once");
        }

        [Test]
        public void WithNobodyInRange_APilotThatCuts_GoesCutting()
        {
            Assert.AreEqual(Reap, Next(Charge, 0.1f, hasRival: false));
            Assert.AreEqual(Charge, Next(Charge, 0.2f, hasRival: false), "a tank already full waits at the centre");
        }

        [Test]
        public void Passed_IsARivalBehindTheNose_InsideTheRadius()
        {
            var self = Vector3.zero;
            var fwd = Vector3.forward;
            Assert.IsFalse(RiposteDuel.Passed(self, fwd, 400f, new Vector3(0, 0, 100f), 50f, 240f), "ahead: still charging");
            Assert.IsTrue(RiposteDuel.Passed(self, fwd, 400f, new Vector3(0, 0, -100f), 50f, 240f), "behind and near");
            Assert.IsFalse(RiposteDuel.Passed(self, fwd, 400f, new Vector3(0, 0, -300f), 50f, 240f),
                           "behind but far: turn back in rather than run further out");
        }

        [Test]
        public void Passed_BreaksOffAKnot_WhenThisHullIsNotTheFaster()
        {
            var self = Vector3.zero;
            var rival = new Vector3(0, 0, 40f);   // ahead, inside a third of the radius
            Assert.IsTrue(RiposteDuel.Passed(self, Vector3.forward, 30f, rival, 30f, 240f), "equal speed cannot strike");
            Assert.IsFalse(RiposteDuel.Passed(self, Vector3.forward, 300f, rival, 30f, 240f), "the faster hull stays in");
        }

        [Test]
        public void ChargePoint_LiesPastTheAim_OnTheLineFromThePilot()
        {
            var self = new Vector3(10f, 0f, 0f);
            var aim = new Vector3(10f, 0f, 200f);
            var point = RiposteDuel.ChargePoint(self, aim, 350f);
            Assert.AreEqual(550f, point.z, 1e-3f);
            Assert.AreEqual(10f, point.x, 1e-3f);
        }

        [Test]
        public void ExtendPoint_IsStraightAhead_AndStaysInsideTheArena()
        {
            var centre = new Vector3(5f, -3f, 2f);
            var inside = RiposteDuel.ExtendPoint(centre, Vector3.right, 300f, centre, 540f);
            Assert.AreEqual(300f, Vector3.Distance(inside, centre), 1e-2f);

            var start = centre + Vector3.right * 500f;
            var clamped = RiposteDuel.ExtendPoint(start, Vector3.right, 300f, centre, 540f);
            Assert.AreEqual(540f, Vector3.Distance(clamped, centre), 1e-2f);
        }

        [Test]
        public void ReapPoint_IsOnTheHeapsSurface_AcrossTheCentreFromThePilot()
        {
            var centre = new Vector3(100f, 50f, -20f);
            const float radius = 470f, spread = 35f;
            var self = centre + new Vector3(-300f, 40f, 120f);
            for (int i = 0; i <= 10; i++)
                for (int j = 0; j <= 10; j++)
                {
                    var point = RiposteDuel.ReapPoint(self, centre, radius, i / 10f, j / 10f, spread);
                    Assert.AreEqual(radius, Vector3.Distance(point, centre), 0.05f, "on the surface");
                    float off = Vector3.Angle(centre - self, point - centre);
                    Assert.LessOrEqual(off, spread + 0.05f, "within the spread of the line through the centre");
                    // The line from the pilot to the point crosses the heap: it comes within the spread's chord of the centre.
                    Assert.Greater(Vector3.Distance(self, point), radius, "a real crossing, not a touch");
                }
        }

        [Test]
        public void ReapPoint_FromTheCentre_StillAnswers()
        {
            var point = RiposteDuel.ReapPoint(Vector3.zero, Vector3.zero, 470f, 0.5f, 0.5f, 35f);
            Assert.AreEqual(470f, point.magnitude, 0.05f);
        }

        [Test]
        public void ClampToBall_LeavesAnInsidePointAlone()
        {
            var p = new Vector3(10f, 20f, 30f);
            Assert.AreEqual(p, RiposteDuel.ClampToBall(p, Vector3.zero, 100f));
            Assert.AreEqual(100f, RiposteDuel.ClampToBall(p * 10f, Vector3.zero, 100f).magnitude, 1e-2f);
        }
    }
}
