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
    }
}
