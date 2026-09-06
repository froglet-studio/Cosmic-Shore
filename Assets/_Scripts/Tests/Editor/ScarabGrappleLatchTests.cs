using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab grapple's attach/release contract (SCARAB.md §4.7). These pin the ONE defect
    /// this type exists to make impossible: an edge-driven attach against a level-driven release,
    /// which strands a hull off its orbit while the server still holds the ball.
    /// </summary>
    public class ScarabGrappleLatchTests
    {
        // ------------------------------------------------------------- attach is a level

        [Test]
        public void FollowingIsAPureLevel_SoALostFrameHealsItself()
        {
            // The frame the ball reference goes missing: detach.
            Assert.IsFalse(ScarabGrappleLatch.ShouldFollow(true, true, ballUsable: false, hasTransformer: true, alreadyReleased: false),
                "a missing ball must detach");
            // The very next frame, once it resolves again: re-attach, with no state edge to wait on.
            Assert.IsTrue(ScarabGrappleLatch.ShouldFollow(true, true, ballUsable: true, hasTransformer: true, alreadyReleased: false),
                "the same level must re-attach — this is what an edge-driven attach could not do");
        }

        [Test]
        public void EveryInputCanIndependentlyDetach()
        {
            Assert.IsFalse(ScarabGrappleLatch.ShouldFollow(false, true, true, true, false), "hold released");
            Assert.IsFalse(ScarabGrappleLatch.ShouldFollow(true, false, true, true, false), "no live grapple");
            Assert.IsFalse(ScarabGrappleLatch.ShouldFollow(true, true, false, true, false), "ball unusable");
            Assert.IsFalse(ScarabGrappleLatch.ShouldFollow(true, true, true, false, false), "no transformer");
            Assert.IsTrue(ScarabGrappleLatch.ShouldFollow(true, true, true, true, false), "all five hold");
        }

        [Test]
        public void AFlutteredHoldDoesNotRejoinTheGrappleItJustReleased()
        {
            // Every level is satisfied again — the pilot re-buried the trigger — but they already
            // let THIS grapple go, and the server is a tick away from flinging it. Re-attaching
            // here would blend the camera off the ball and straight back onto it.
            Assert.IsFalse(ScarabGrappleLatch.ShouldFollow(true, true, true, true, alreadyReleased: true),
                "letting go is a decision, not a level");
        }

        // ------------------------------------------------------------- release is monotonic

        [Test]
        public void TheCounterAdvancesOnlyOnTheFallingEdge()
        {
            Assert.AreEqual(0, (int)ScarabGrappleLatch.AdvanceReleaseSeq(0, wasArmed: false, armed: false), "idle");
            Assert.AreEqual(0, (int)ScarabGrappleLatch.AdvanceReleaseSeq(0, wasArmed: false, armed: true), "rising edge");
            Assert.AreEqual(0, (int)ScarabGrappleLatch.AdvanceReleaseSeq(0, wasArmed: true, armed: true), "still held");
            Assert.AreEqual(1, (int)ScarabGrappleLatch.AdvanceReleaseSeq(0, wasArmed: true, armed: false), "falling edge");
        }

        [Test]
        public void HoldingTheDriftForManyFramesCountsNoReleases()
        {
            uint seq = 0;
            bool was = false;
            for (int i = 0; i < 240; i++)
            {
                seq = ScarabGrappleLatch.AdvanceReleaseSeq(seq, was, true);
                was = true;
            }
            Assert.AreEqual(0, (int)seq, "a sustained hold is not a stream of releases");
        }

        [Test]
        public void SubTickFlutterStillReachesTheServer()
        {
            // THE BUG THIS FILE EXISTS FOR. The hold drops for a single owner frame and returns
            // before the next network tick, so the server samples `armed == true` both times and
            // the level tells it nothing happened. The counter does not care that it was never
            // sampled in between: it only ever counts up.
            uint seqAtGrab = 7;
            uint seq = seqAtGrab;

            seq = ScarabGrappleLatch.AdvanceReleaseSeq(seq, wasArmed: true, armed: false);  // frame n
            seq = ScarabGrappleLatch.AdvanceReleaseSeq(seq, wasArmed: false, armed: true);  // frame n+1

            Assert.IsTrue(ScarabGrappleLatch.ServerShouldRelease(armed: true, seqAtGrab, seq),
                "a hold that flickered off and back inside one tick is still a release");
        }

        [Test]
        public void AHeldGrappleWithNoReleaseIsNeverEnded()
        {
            Assert.IsFalse(ScarabGrappleLatch.ServerShouldRelease(armed: true, seqAtGrab: 7, seqNow: 7),
                "still held, nothing counted");
        }

        [Test]
        public void ADroppedHoldEndsTheGrappleEvenIfTheCounterNeverArrives()
        {
            // The disconnect case: the owner is gone, so no counter update is coming, but their
            // owner-write bool has stopped being true.
            Assert.IsTrue(ScarabGrappleLatch.ServerShouldRelease(armed: false, seqAtGrab: 3, seqNow: 3),
                "the level alone must still be able to end a grapple");
        }

        [Test]
        public void TheCounterIsComparedForCHANGE_SoAWrapStillReleases()
        {
            uint seqAtGrab = uint.MaxValue;
            uint seq = ScarabGrappleLatch.AdvanceReleaseSeq(seqAtGrab, wasArmed: true, armed: false);

            Assert.AreEqual(0, (int)seq, "the counter wraps rather than throwing");
            Assert.IsTrue(ScarabGrappleLatch.ServerShouldRelease(armed: true, seqAtGrab, seq),
                "a wrapped counter is still a change — this is why the test is != and not >");
        }
    }
}
