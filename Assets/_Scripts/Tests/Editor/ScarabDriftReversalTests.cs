using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab's REVERSE modifier (SCARAB.md §3.8). The claim worth proving offline is the
    /// blast's: that inverting the plate is a SPAWN TRANSFORM which sweeps the identical cylinder
    /// the other way, because that is what lets the reversal exist with no second code path — and
    /// "identical volume" is exactly the kind of geometric assertion that is easy to believe and
    /// easy to get wrong by one length.
    /// </summary>
    public class ScarabDriftReversalTests
    {
        const float Tol = 1e-3f;

        // ------------------------------------------------------------------ the plate

        [Test]
        public void TheReversedPlateSweepsTheIDENTICALCylinder()
        {
            Vector3 at = new(12f, -4f, 30f);
            Vector3 dir = new Vector3(0.4f, 0.5f, -0.77f).normalized;
            const float length = 54f;

            var pose = ScarabDriftReversal.ReversedSweep(at, dir, length);

            // The forward sweep covers [at, at + dir*L]. The reversed one must cover the same
            // segment, traversed from the far end back.
            Vector3 forwardStart = at, forwardEnd = at + dir * length;
            Vector3 reverseStart = pose.Position, reverseEnd = pose.Position + pose.Forward * length;

            Assert.Less((reverseStart - forwardEnd).magnitude, Tol, "reversed starts where forward ended");
            Assert.Less((reverseEnd - forwardStart).magnitude, Tol, "and ends where forward started");
        }

        [Test]
        public void TheReversedPlateThrowsMassTheOtherWay()
        {
            Vector3 dir = Vector3.forward;
            var pose = ScarabDriftReversal.ReversedSweep(Vector3.zero, dir, 20f);

            // The plate's law is "everything it claims leaves along the sweep", so the debris
            // direction IS the pose's forward. That is the whole mechanism.
            Assert.AreEqual(-1f, Vector3.Dot(pose.Forward, dir), Tol, "exactly opposed");
            Assert.AreEqual(1f, pose.Forward.magnitude, Tol, "still a unit sweep direction");
        }

        [Test]
        public void ReversingTwiceIsTheOriginalSweep()
        {
            Vector3 at = new(-3f, 8f, 1f);
            Vector3 dir = new Vector3(-0.6f, 0.1f, 0.79f).normalized;
            const float length = 33f;

            var once = ScarabDriftReversal.ReversedSweep(at, dir, length);
            var twice = ScarabDriftReversal.ReversedSweep(once.Position, once.Forward, length);

            Assert.Less((twice.Position - at).magnitude, Tol, "back to the hull");
            Assert.Less((twice.Forward - dir).magnitude, Tol, "and back to the dash direction");
        }

        [Test]
        public void AZeroLengthPlateHasNothingToReverseAndDoesNotMove()
        {
            Vector3 at = new(5f, 5f, 5f);
            var pose = ScarabDriftReversal.ReversedSweep(at, Vector3.right, 0f);
            Assert.Less((pose.Position - at).magnitude, Tol,
                "the caller declines to reverse at zero length; the maths must not invent an offset");
        }

        // ------------------------------------------------------------------ the ball

        [Test]
        public void AReversedBallRetracesItsOwnPathAtItsOwnSpeed()
        {
            Vector3 v = new(30f, -12f, 44f);
            Vector3 reversed = ScarabDriftReversal.ReversedBallVelocity(v);

            Assert.AreEqual(v.magnitude, reversed.magnitude, Tol,
                "the reversal adds no energy and takes none — it is a redirection only");
            Assert.AreEqual(-1f, Vector3.Dot(v.normalized, reversed.normalized), Tol,
                "exactly 180 degrees, not a bounce that happens to point back");
        }

        [Test]
        public void ReversalIsAnInvolution_SoTwoScarabsCanRallyOneBall()
        {
            Vector3 v = new(-7f, 2f, 19f);
            Vector3 twice = ScarabDriftReversal.ReversedBallVelocity(
                ScarabDriftReversal.ReversedBallVelocity(v));
            Assert.Less((twice - v).magnitude, Tol, "back on its original heading, undiminished");
        }

        [Test]
        public void ABallWithNoTrajectoryFallsThroughToTheOrdinaryStrike()
        {
            const float floor = 3f;
            Assert.IsFalse(ScarabDriftReversal.CanReverseBall(0f, floor),
                "a resting ball has nothing to send back");
            Assert.IsFalse(ScarabDriftReversal.CanReverseBall(2.99f, floor), "just under");
            Assert.IsTrue(ScarabDriftReversal.CanReverseBall(floor, floor), "at the floor it reverses");
            Assert.IsTrue(ScarabDriftReversal.CanReverseBall(200f, floor), "and at any speed above");
        }

        [Test]
        public void AZeroFloorReversesEverythingThatMovesAtAll()
        {
            // The floor is a design choice, not a numerical guard — at 0 the rule is simply
            // "always reverse", and the maths must not smuggle in a threshold of its own.
            Assert.IsTrue(ScarabDriftReversal.CanReverseBall(0.0001f, 0f));
            Assert.IsTrue(ScarabDriftReversal.CanReverseBall(0f, 0f));
        }

        // ------------------------------------------------------- the fling (release + latch)

        [Test]
        public void TheChaseDownFlingPutsTheBallBEHINDTheStriker()
        {
            // The signature case: the Scarab runs a ball down from behind. The ball is ahead of
            // the hull, moving away; reversing it sends it back along a heading that runs straight
            // THROUGH the ship. The release has to put it out the other side, or the fling is
            // impossible however the velocity rule is written.
            Vector3 striker = new(0f, 0f, 0f);
            Vector3 forward = Vector3.forward;
            Vector3 ballVel = forward * 40f;                 // fleeing along +z
            Vector3 reversed = ScarabDriftReversal.ReversedBallVelocity(ballVel);
            const float minClear = 15f;

            Vector3 exit = ScarabDriftReversal.ReversedExitPosition(striker, reversed, forward, minClear);

            Assert.Less(Vector3.Dot(exit - striker, forward), 0f,
                "released behind the striker, not in front of it");
            Assert.AreEqual(minClear, (exit - striker).magnitude, Tol,
                "and exactly clear of it — the same guarantee the depenetration makes");
            Assert.Greater(Vector3.Dot(exit - striker, reversed.normalized), 0f,
                "on the side it is now travelling toward, so it separates from here on");
        }

        [Test]
        public void TheHeadOnCaseReleasesWhereTheBallAlreadyWas()
        {
            // Ball coming at the nose: the reversal sends it back out the front, so the release
            // is very nearly a no-op. Worth pinning — the fling must not TELEPORT a ball that had
            // no reason to move.
            Vector3 striker = Vector3.zero;
            Vector3 ballVel = Vector3.back * 30f;             // incoming along -z
            Vector3 reversed = ScarabDriftReversal.ReversedBallVelocity(ballVel);
            const float minClear = 15f;
            Vector3 ballWas = new(0f, 0f, minClear);          // sitting on the clearance shell

            Vector3 exit = ScarabDriftReversal.ReversedExitPosition(striker, reversed, Vector3.forward, minClear);

            Assert.Less((exit - ballWas).magnitude, Tol, "put down where it already was");
        }

        [Test]
        public void ADegenerateVelocityFallsBackOnTheContactNormal()
        {
            Vector3 striker = new(5f, 5f, 5f);
            Vector3 fallback = Vector3.up;
            Vector3 exit = ScarabDriftReversal.ReversedExitPosition(striker, Vector3.zero, fallback, 9f);
            Assert.Less((exit - (striker + fallback * 9f)).magnitude, Tol,
                "never NaN, never the striker's own position");
        }

        [Test]
        public void ThePassThroughWindowIsWhatStopsTheInvolutionCancellingItself()
        {
            // The gate the shipped code USED to rely on — "only respond when the ball is moving
            // INTO the vessel" — cannot do this job, and this is the arithmetic that proves it:
            // a striker closing faster than the ball travels still sees the REVERSED ball as
            // approaching, so it strikes again and the two reversals cancel to a plain bounce.
            Vector3 n = Vector3.forward;                       // contact normal, hull → ball
            Vector3 ballVel = Vector3.forward * 20f;           // fleeing
            Vector3 strikerVel = Vector3.forward * 60f;        // chasing, three times faster
            Assert.Less(Vector3.Dot(ballVel - strikerVel, n), 0f, "approaching: the first strike fires");

            Vector3 reversed = ScarabDriftReversal.ReversedBallVelocity(ballVel);
            Assert.Less(Vector3.Dot(reversed - strikerVel, n), 0f,
                "STILL approaching after the reversal — the gate would fire a second one");

            // Which is why the grab arms a latch instead.
            const float now = 100f;
            float expiry = ScarabDriftReversal.PassThroughExpiry(now, 0.35f);
            Assert.IsTrue(ScarabDriftReversal.IsPassingThrough(expiry, now), "closed on the frame it fires");
            Assert.IsTrue(ScarabDriftReversal.IsPassingThrough(expiry, now + 0.34f), "and through the transit");
            Assert.IsFalse(ScarabDriftReversal.IsPassingThrough(expiry, now + 0.35f), "then the vessel is mass again");
            Assert.IsFalse(ScarabDriftReversal.IsPassingThrough(expiry, now + 10f));
        }

        [Test]
        public void AZeroPassThroughLeavesTheVesselSolidImmediately()
        {
            // 0 must mean "off", not "open forever" — the window is a tuning value and a designer
            // turning it down to nothing has to get the un-phased behaviour back.
            const float now = 50f;
            float expiry = ScarabDriftReversal.PassThroughExpiry(now, 0f);
            Assert.IsFalse(ScarabDriftReversal.IsPassingThrough(expiry, now));
            Assert.AreEqual(now, ScarabDriftReversal.PassThroughExpiry(now, -5f), Tol,
                "a negative window is clamped, never a window in the past that reads as open");
        }

        [Test]
        public void ThePassThroughEndsWithTheCONTACT_NotWithTheClock()
        {
            // The window covers one grab's overlapping frames. A pilot who turns around and comes
            // back is entitled to a fresh reversal the moment they arrive — a window still open
            // after its own contact stopped makes the next ram do NOTHING, which reads as the
            // ability cutting out rather than as a rule.
            const float armed = 100f;
            float expiry = ScarabDriftReversal.PassThroughExpiry(armed, 0.35f);
            const float gap = 0.08f;

            // Still overlapping (contact refreshed every frame) → still passing through.
            Assert.IsFalse(ScarabDriftReversal.PassThroughLapsed(armed + 0.20f, expiry, armed + 0.21f, gap),
                "a live contact holds the window open");

            // Contacts stopped 0.09 s ago → the ball is out the other side, so it is over —
            // well before the cap would have ended it.
            Assert.IsTrue(ScarabDriftReversal.PassThroughLapsed(armed + 0.05f, expiry, armed + 0.14f, gap),
                "no contact for longer than the gap IS the ball having left");

            // A one-frame hole in a multi-collider hull must not end it early.
            Assert.IsFalse(ScarabDriftReversal.PassThroughLapsed(armed + 0.05f, expiry, armed + 0.07f, gap),
                "a glancing frame with no reported contact is not a departure");
        }

        [Test]
        public void TheCapStillEndsAPassThroughThatKeepsReportingContact()
        {
            // The one case the contact test cannot end: a pilot who parks inside the ball. The
            // cap is the backstop, so the ball can never be permanently intangible to a vessel.
            const float armed = 100f;
            float expiry = ScarabDriftReversal.PassThroughExpiry(armed, 0.35f);
            Assert.IsTrue(ScarabDriftReversal.PassThroughLapsed(armed + 0.36f, expiry, armed + 0.36f, 0.08f),
                "contact or no contact, the window cannot outlive its cap");
        }

        // ------------------------------------------------------- the hold is a LATCH

        const float Engage = 0.9f;
        const float Release = 0.6f;

        [Test]
        public void TheHoldEngagesOnlyWhenTheTriggerIsGenuinelyBuried()
        {
            Assert.IsFalse(ScarabDriftReversal.LatchDriftHold(false, 0.0f, Engage, Release));
            Assert.IsFalse(ScarabDriftReversal.LatchDriftHold(false, 0.61f, Engage, Release),
                "past the RELEASE point is not past the ENGAGE point");
            Assert.IsFalse(ScarabDriftReversal.LatchDriftHold(false, 0.89f, Engage, Release));
            Assert.IsTrue(ScarabDriftReversal.LatchDriftHold(false, Engage, Engage, Release));
        }

        [Test]
        public void AWobbleOnABuriedTriggerDoesNotDropTheReversal()
        {
            // THE REPORTED DEFECT. A physical trigger held to its stop reads ~1.0 and dips a few
            // percent under a thumb that is also working a stick; the bare comparison this
            // replaced dropped the modifier on every dip, which is what "the reversed impact was
            // not consistent" was.
            bool held = ScarabDriftReversal.LatchDriftHold(false, 1f, Engage, Release);
            foreach (float dip in new[] { 0.97f, 0.88f, 0.93f, 0.79f, 0.99f })
            {
                held = ScarabDriftReversal.LatchDriftHold(held, dip, Engage, Release);
                Assert.IsTrue(held, $"a dip to {dip} is a wobble, not a release");
            }
        }

        [Test]
        public void LettingTheTriggerUpReleasesIt()
        {
            bool held = ScarabDriftReversal.LatchDriftHold(false, 1f, Engage, Release);
            held = ScarabDriftReversal.LatchDriftHold(held, 0.59f, Engage, Release);
            Assert.IsFalse(held, "below the release point the pilot has let go");

            // And re-engaging costs the full travel again — the band is not a one-way door.
            held = ScarabDriftReversal.LatchDriftHold(held, 0.85f, Engage, Release);
            Assert.IsFalse(held, "coming back up must reach ENGAGE, not RELEASE");
            held = ScarabDriftReversal.LatchDriftHold(held, 0.95f, Engage, Release);
            Assert.IsTrue(held);
        }

        [Test]
        public void NoAuthoredBandCanLeaveTheModifierStuckOn()
        {
            // The one failure mode worse than the flicker this replaced: a reversal that will not
            // let go. It is impossible by construction rather than by a guard — releasing tests
            // the SAME depth reading against a threshold, so letting the trigger all the way up
            // releases whatever the two numbers are, including a band authored backwards.
            foreach (float engage in new[] { 0.5f, 0.9f, 1f })
            foreach (float release in new[] { 0.1f, 0.6f, 0.9f, 1f })
                Assert.IsFalse(ScarabDriftReversal.LatchDriftHold(true, 0f, engage, release),
                    $"a released trigger must release the modifier (engage {engage}, release {release})");

            // At equal thresholds the latch degenerates to the bare comparison it replaced, which
            // is the honest reading of "no band" — not a special case in the code.
            Assert.IsTrue(ScarabDriftReversal.LatchDriftHold(true, 0.95f, 0.9f, 0.9f));
            Assert.IsFalse(ScarabDriftReversal.LatchDriftHold(true, 0.85f, 0.9f, 0.9f),
                "no band means no memory");
        }
    }
}
