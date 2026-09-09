using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab's PHASE GRAB (SCARAB.md §3.8) — the rules a ball obeys when a phasing hull
    /// catches it. Two of them are worth proving offline because they are easy to believe and
    /// easy to get wrong: the reversal is an INVOLUTION (two Scarabs must be able to rally one
    /// ball forever without it gaining or losing speed), and the pass-through window has to end
    /// with the CONTACT rather than with the clock. The third is the mirrored blast's rear-half
    /// test, which is one dot product and decides whether the pilot keeps their signature move.
    ///
    /// The plate-reversal tests that used to head this file are gone with the mechanic they
    /// pinned: a held drift no longer inverts the blast, and the blast now claims its own mirror
    /// image instead (<c>AOECylindricalExplosion.mirrorAboutStartPlane</c>) — a volume change with
    /// no pose arithmetic left to assert.
    /// </summary>
    public class ScarabPhaseReversalTests
    {
        const float Tol = 1e-3f;

        [Test]
        public void AReversedBallRetracesItsOwnPathAtItsOwnSpeed()
        {
            Vector3 v = new(30f, -12f, 44f);
            Vector3 reversed = ScarabPhaseReversal.ReversedBallVelocity(v);

            Assert.AreEqual(v.magnitude, reversed.magnitude, Tol,
                "the reversal adds no energy and takes none — it is a redirection only");
            Assert.AreEqual(-1f, Vector3.Dot(v.normalized, reversed.normalized), Tol,
                "exactly 180 degrees, not a bounce that happens to point back");
        }

        [Test]
        public void ReversalIsAnInvolution_SoTwoScarabsCanRallyOneBall()
        {
            Vector3 v = new(-7f, 2f, 19f);
            Vector3 twice = ScarabPhaseReversal.ReversedBallVelocity(
                ScarabPhaseReversal.ReversedBallVelocity(v));
            Assert.Less((twice - v).magnitude, Tol, "back on its original heading, undiminished");
        }

        [Test]
        public void ABallWithNoTrajectoryFallsThroughToTheOrdinaryStrike()
        {
            const float floor = 3f;
            Assert.IsFalse(ScarabPhaseReversal.CanReverseBall(0f, floor),
                "a resting ball has nothing to send back");
            Assert.IsFalse(ScarabPhaseReversal.CanReverseBall(2.99f, floor), "just under");
            Assert.IsTrue(ScarabPhaseReversal.CanReverseBall(floor, floor), "at the floor it reverses");
            Assert.IsTrue(ScarabPhaseReversal.CanReverseBall(200f, floor), "and at any speed above");
        }

        [Test]
        public void AZeroFloorReversesEverythingThatMovesAtAll()
        {
            // The floor is a design choice, not a numerical guard — at 0 the rule is simply
            // "always reverse", and the maths must not smuggle in a threshold of its own.
            Assert.IsTrue(ScarabPhaseReversal.CanReverseBall(0.0001f, 0f));
            Assert.IsTrue(ScarabPhaseReversal.CanReverseBall(0f, 0f));
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
            Vector3 reversed = ScarabPhaseReversal.ReversedBallVelocity(ballVel);
            const float minClear = 15f;

            Vector3 exit = ScarabPhaseReversal.ReversedExitPosition(striker, reversed, forward, minClear);

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
            Vector3 reversed = ScarabPhaseReversal.ReversedBallVelocity(ballVel);
            const float minClear = 15f;
            Vector3 ballWas = new(0f, 0f, minClear);          // sitting on the clearance shell

            Vector3 exit = ScarabPhaseReversal.ReversedExitPosition(striker, reversed, Vector3.forward, minClear);

            Assert.Less((exit - ballWas).magnitude, Tol, "put down where it already was");
        }

        [Test]
        public void ADegenerateVelocityFallsBackOnTheContactNormal()
        {
            Vector3 striker = new(5f, 5f, 5f);
            Vector3 fallback = Vector3.up;
            Vector3 exit = ScarabPhaseReversal.ReversedExitPosition(striker, Vector3.zero, fallback, 9f);
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

            Vector3 reversed = ScarabPhaseReversal.ReversedBallVelocity(ballVel);
            Assert.Less(Vector3.Dot(reversed - strikerVel, n), 0f,
                "STILL approaching after the reversal — the gate would fire a second one");

            // Which is why the grab arms a latch instead.
            const float now = 100f;
            float expiry = ScarabPhaseReversal.PassThroughExpiry(now, 0.35f);
            Assert.IsTrue(ScarabPhaseReversal.IsPassingThrough(expiry, now), "closed on the frame it fires");
            Assert.IsTrue(ScarabPhaseReversal.IsPassingThrough(expiry, now + 0.34f), "and through the transit");
            Assert.IsFalse(ScarabPhaseReversal.IsPassingThrough(expiry, now + 0.35f), "then the vessel is mass again");
            Assert.IsFalse(ScarabPhaseReversal.IsPassingThrough(expiry, now + 10f));
        }

        [Test]
        public void AZeroPassThroughLeavesTheVesselSolidImmediately()
        {
            // 0 must mean "off", not "open forever" — the window is a tuning value and a designer
            // turning it down to nothing has to get the un-phased behaviour back.
            const float now = 50f;
            float expiry = ScarabPhaseReversal.PassThroughExpiry(now, 0f);
            Assert.IsFalse(ScarabPhaseReversal.IsPassingThrough(expiry, now));
            Assert.AreEqual(now, ScarabPhaseReversal.PassThroughExpiry(now, -5f), Tol,
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
            float expiry = ScarabPhaseReversal.PassThroughExpiry(armed, 0.35f);
            const float gap = 0.08f;

            // Still overlapping (contact refreshed every frame) → still passing through.
            Assert.IsFalse(ScarabPhaseReversal.PassThroughLapsed(armed + 0.20f, expiry, armed + 0.21f, gap),
                "a live contact holds the window open");

            // Contacts stopped 0.09 s ago → the ball is out the other side, so it is over —
            // well before the cap would have ended it.
            Assert.IsTrue(ScarabPhaseReversal.PassThroughLapsed(armed + 0.05f, expiry, armed + 0.14f, gap),
                "no contact for longer than the gap IS the ball having left");

            // A one-frame hole in a multi-collider hull must not end it early.
            Assert.IsFalse(ScarabPhaseReversal.PassThroughLapsed(armed + 0.05f, expiry, armed + 0.07f, gap),
                "a glancing frame with no reported contact is not a departure");
        }

        [Test]
        public void TheCapStillEndsAPassThroughThatKeepsReportingContact()
        {
            // The one case the contact test cannot end: a pilot who parks inside the ball. The
            // cap is the backstop, so the ball can never be permanently intangible to a vessel.
            const float armed = 100f;
            float expiry = ScarabPhaseReversal.PassThroughExpiry(armed, 0.35f);
            Assert.IsTrue(ScarabPhaseReversal.PassThroughLapsed(armed + 0.36f, expiry, armed + 0.36f, 0.08f),
                "contact or no contact, the window cannot outlive its cap");
        }

        // ------------------------------------------------- the mirrored blast's rear half

        [Test]
        public void OnlyTheREARHalfOfAMirroredBlastCanEverDeliverABallToItsOwnPilot()
        {
            // THE TEST THAT KEEPS THE SIGNATURE MOVE ALIVE. A phasing pilot who punches a ball
            // AWAY must not mark it: if they did, the ball they just sent down-range would be
            // intangible to them for the whole cap, so chasing it down and grabbing it — the move
            // the whole ability exists for — would fly straight through and do nothing.
            Vector3 hull = new(10f, -4f, 7f);
            Vector3 axis = new Vector3(1f, 0f, 1f).normalized;   // the dash direction

            Assert.IsTrue(ScarabPhaseReversal.IsBehindStartPlane(hull - axis * 30f, hull, axis),
                "a ball astern is the half that gets dragged forward through the pilot");
            Assert.IsFalse(ScarabPhaseReversal.IsBehindStartPlane(hull + axis * 30f, hull, axis),
                "a ball down-range is being punched AWAY and must never be tagged");
        }

        [Test]
        public void TheStartPlaneItselfIsNotBehind()
        {
            // The boundary belongs to the forward half, which is the safe direction: a ball exactly
            // on the plane is not moving toward the pilot, so tagging it could only ever cost them
            // a grab. It also matches the un-mirrored volume, s in [0, depth], where 0 is inside.
            Vector3 hull = Vector3.zero;
            Vector3 axis = Vector3.forward;
            Assert.IsFalse(ScarabPhaseReversal.IsBehindStartPlane(hull, hull, axis),
                "the emitter's own plane is not behind it");
            Assert.IsFalse(ScarabPhaseReversal.IsBehindStartPlane(new Vector3(50f, -20f, 0f), hull, axis),
                "and neither is anything abeam of it, however far out");
        }

        [Test]
        public void TheSideTestIsTheMIRRORTest_AnOrdinaryPlateHasNoRearHalf()
        {
            // One dot product answers both questions, which is why the authored flag is never read
            // a second time. An un-mirrored cylinder's volume is s in [0, depth]; nothing it claims
            // can sit at negative s, so nothing it kicks can ever be tagged.
            Vector3 hull = new(-3f, 12f, 0.5f);
            Vector3 axis = Vector3.right;
            for (float s = 0f; s <= 54f; s += 2f)
                Assert.IsFalse(
                    ScarabPhaseReversal.IsBehindStartPlane(hull + axis * s, hull, axis),
                    $"nothing in an un-mirrored plate's own volume is behind its start plane (s={s})");
        }

    }
}
