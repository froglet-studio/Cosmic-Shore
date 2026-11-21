using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Scarab Scramble's AI juke geometry (SCARABSCRAMBLE.md "AI"). The property the steal test
    /// guards is the one a naive "dash when close" rule gets wrong: the steal window is only open
    /// for the dash's own duration, so a dash must be fired when the CONTACT lands inside it and
    /// the dash's sideways travel closes the miss - early is a bump, late is a bump, too far is a
    /// miss. The numbers below are the shipped Scarab.prefab juke (JukeSpeed 80 u/s,
    /// JukeDurationSeconds 0.5), which the controller reads live off ScarabJukeController and
    /// passes into the planner; the planner itself takes them as arguments.
    /// </summary>
    public class ScarabScrambleJukePlannerTests
    {
        const float Speed = 80f;   // ScarabJukeController.JukeSpeed on Scarab.prefab
        const float Window = 0.5f; // ScarabJukeController.JukeDurationSeconds on Scarab.prefab
        const float MinLead = 0.1f;
        const float Contact = 10f;

        [Test]
        public void DashDisplacement_IsZeroAtFire_AndOneFullDashAtTheEnd()
        {
            Assert.AreEqual(0f, ScarabScrambleJukePlanner.DashDisplacement(0f, Speed, Window), 1e-5f);
            Assert.AreEqual(Speed * Window,
                ScarabScrambleJukePlanner.DashDisplacement(Window, Speed, Window), 1e-3f);
            // Clamped past the end: the modifier is gone.
            Assert.AreEqual(Speed * Window,
                ScarabScrambleJukePlanner.DashDisplacement(2f, Speed, Window), 1e-3f);
        }

        [Test]
        public void DashDisplacement_IsMonotone_AndFrontLoaded()
        {
            float prev = 0f;
            for (int i = 1; i <= 50; i++)
            {
                float d = ScarabScrambleJukePlanner.DashDisplacement(Window * i / 50f, Speed, Window);
                Assert.Greater(d, prev);
                prev = d;
            }
            // The curve starts at 1.5x speed and ends at 0.5x, so the first half covers more.
            float half = ScarabScrambleJukePlanner.DashDisplacement(Window * 0.5f, Speed, Window);
            Assert.Greater(half, Speed * Window * 0.5f);
        }

        [Test]
        public void ClosestApproach_IsNow_ForASeparatingPair()
        {
            float t = ScarabScrambleJukePlanner.ClosestApproach(
                new Vector3(0f, 0f, -50f), new Vector3(0f, 0f, -10f), out Vector3 offset);
            Assert.AreEqual(0f, t);
            Assert.AreEqual(50f, offset.magnitude, 1e-4f);
        }

        [Test]
        public void ClosestApproach_FindsTheMissOfAPassingBall()
        {
            // Hull at origin flying +Z at 200; stationary ball 60 ahead, 30 to the right.
            float t = ScarabScrambleJukePlanner.ClosestApproach(
                new Vector3(30f, 0f, 60f), new Vector3(0f, 0f, -200f), out Vector3 offset);
            Assert.AreEqual(0.3f, t, 1e-4f);
            Assert.AreEqual(30f, offset.x, 1e-3f);
            Assert.AreEqual(0f, offset.z, 1e-3f);
        }

        [Test]
        public void Steal_Fires_WhenTheDashLandsTheContactInsideTheWindow()
        {
            const float t = 0.3f;
            float lateral = ScarabScrambleJukePlanner.DashDisplacement(t, Speed, Window);
            bool fire = ScarabScrambleJukePlanner.ShouldStealDash(
                Vector3.zero, new Vector3(0f, 0f, 200f), new Vector3(lateral, 0f, 200f * t),
                Vector3.zero, Window, MinLead, Speed, Contact, out Vector3 shove);
            Assert.IsTrue(fire);
            Assert.Greater(shove.x, 0f, "dash toward the side the ball is on");
        }

        [Test]
        public void Steal_Holds_WhenTheContactIsBeyondTheWindow()
        {
            // Closest approach 1 s out: a dash now would close its window before contact.
            bool fire = ScarabScrambleJukePlanner.ShouldStealDash(
                Vector3.zero, new Vector3(0f, 0f, 200f), new Vector3(Speed * Window, 0f, 200f),
                Vector3.zero, Window, MinLead, Speed, Contact, out _);
            Assert.IsFalse(fire);
        }

        [Test]
        public void Steal_Holds_WhenTheContactIsSoonerThanTheMinimumLead()
        {
            bool fire = ScarabScrambleJukePlanner.ShouldStealDash(
                Vector3.zero, new Vector3(0f, 0f, 200f), new Vector3(0f, 0f, 200f * 0.05f),
                Vector3.zero, Window, MinLead, Speed, Contact, out _);
            Assert.IsFalse(fire);
        }

        [Test]
        public void Steal_Holds_WhenTheDashWouldOvershootADeadAheadBall()
        {
            // Dead ahead at 0.4 s: no sideways miss, but the dash would carry the hull ~37 units
            // off the line - well past a 10-unit contact. Fly into it; dash later or not at all.
            bool fire = ScarabScrambleJukePlanner.ShouldStealDash(
                Vector3.zero, new Vector3(0f, 0f, 200f), new Vector3(0f, 0f, 80f),
                Vector3.zero, Window, MinLead, Speed, Contact, out Vector3 shove);
            Assert.IsFalse(fire);
            Assert.AreEqual(Vector3.zero, shove);
        }

        [Test]
        public void Steal_Holds_WhenTheBallIsOutOfReachSideways()
        {
            bool fire = ScarabScrambleJukePlanner.ShouldStealDash(
                Vector3.zero, new Vector3(0f, 0f, 200f), new Vector3(150f, 0f, 60f),
                Vector3.zero, Window, MinLead, Speed, Contact, out _);
            Assert.IsFalse(fire);
        }

        [Test]
        public void Steal_LeadsAMovingBall()
        {
            // The ball crosses the hull's path: at t = 0.25 it is level with the hull, and the
            // dash's travel by then (~27) matches its sideways offset.
            const float t = 0.25f;
            float reach = ScarabScrambleJukePlanner.DashDisplacement(t, Speed, Window);
            Vector3 ballVel = new(-40f, 0f, 0f);
            Vector3 ballPos = new Vector3(reach, 0f, 200f * t) - ballVel * t;
            bool fire = ScarabScrambleJukePlanner.ShouldStealDash(
                Vector3.zero, new Vector3(0f, 0f, 200f), ballPos, ballVel,
                Window, MinLead, Speed, Contact, out _);
            Assert.IsTrue(fire);
        }

        static readonly Vector3 Course = Vector3.forward;
        const float FullDash = Speed * Window; // 40
        const float MinDistance = 150f;
        const float MaxAngle = 30f;
        const float Clearance = 70f;

        static bool Escort(Vector3 aim, Vector3 ball, out Vector3 shove) =>
            ScarabScrambleJukePlanner.ShouldEscortDash(Vector3.zero, Course, aim, ball,
                MinDistance, MaxAngle, FullDash, Clearance, out shove);

        [Test]
        public void Escort_Fires_OnALongStraightWithAFullDashOfOffset()
        {
            Assert.IsTrue(Escort(new Vector3(-60f, 0f, 200f), new Vector3(-80f, 0f, 260f), out Vector3 shove));
            Assert.AreEqual(0f, Vector3.Dot(shove, Course), 1e-3f, "the shove is sideways only");
            Assert.Less(shove.x, 0f, "toward the escort point's side");
        }

        [Test]
        public void Escort_Holds_WhenTheEscortPointIsNear()
        {
            Assert.IsFalse(Escort(new Vector3(-50f, 0f, 100f), new Vector3(-60f, 0f, 160f), out _));
        }

        [Test]
        public void Escort_Holds_MidTurn()
        {
            // 45 degrees off the nose: the pilot is turning, not on a straight.
            Assert.IsFalse(Escort(new Vector3(-150f, 0f, 150f), new Vector3(-180f, 0f, 200f), out _));
        }

        [Test]
        public void Escort_Holds_WhenTheOffsetIsLessThanOneDash()
        {
            // Lined up to within 20 units: a 40-unit dash would overshoot the line.
            Assert.IsFalse(Escort(new Vector3(20f, 0f, 200f), new Vector3(25f, 0f, 260f), out _));
        }

        [Test]
        public void Escort_Holds_WhenTheOwnBallIsInsideThePlate()
        {
            // Escort point far ahead, but the ball is beside the hull (the AI is between it and
            // the hoop): the plate would throw our own ball off its line.
            Assert.IsFalse(Escort(new Vector3(-60f, 0f, 200f), new Vector3(-30f, 0f, 20f), out _));
        }

        [Test]
        public void Escort_Holds_WhenThePointIsBehind()
        {
            Assert.IsFalse(Escort(new Vector3(-60f, 0f, -200f), new Vector3(-80f, 0f, -260f), out _));
        }
    }
}
