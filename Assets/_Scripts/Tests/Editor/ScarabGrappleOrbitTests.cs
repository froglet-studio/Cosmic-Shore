using System.Linq;
using System.Reflection;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The held-drift grapple's orbit contract (SCARAB.md §4.7). The orbit is a pure function of
    /// six numbers and the clock, and that purity is load-bearing: the SERVER flings the ball and
    /// the OWNER writes the hull's pose from the same state without exchanging a position, so
    /// anything that made the two disagree — a phase that did not start at the contact, a
    /// tangent that did not match the entry motion, a fling that did not follow the swing —
    /// would surface as a throw going somewhere other than where the pilot saw the hull going.
    /// </summary>
    public class ScarabGrappleOrbitTests
    {
        const float Tol = 1e-3f;
        static readonly Vector3 Ball = new(40f, -12f, 300f);
        const float Radius = 12f;

        static void AssertVector(Vector3 expected, Vector3 actual, float tol = Tol, string what = "")
            => Assert.Less((expected - actual).magnitude, tol,
                $"{what} expected {expected} got {actual}");

        static ScarabGrappleOrbitState Glancing(Vector3 ballVelocity, double t0 = 100.0)
        {
            // Hull sits +x of the ball, arriving with 30 u/s toward the ball (radial, absorbed)
            // and 60 u/s along +z (tangential, becomes the orbit), on top of the ball's motion.
            Vector3 hullPos = Ball + Vector3.right * Radius;
            Vector3 hullVel = ballVelocity + new Vector3(-30f, 0f, 60f);
            return ScarabGrappleOrbit.FromContact(Ball, ballVelocity, hullPos, hullVel, Radius, t0, Vector3.up);
        }

        [Test]
        public void HeadOnContact_SticksWithNoSpin()
        {
            Vector3 hullPos = Ball + Vector3.right * Radius;
            var s = ScarabGrappleOrbit.FromContact(Ball, Vector3.zero, hullPos, Vector3.left * 80f,
                                                   Radius, 5.0, Vector3.up);
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual(0f, s.AngularSpeed, Tol);
            AssertVector(Vector3.right, s.Radial0, what: "radial");
            AssertVector(Vector3.zero, ScarabGrappleOrbit.RelativeVelocityAt(s, 9.0), what: "relative velocity");
            AssertVector(Vector3.zero, ScarabGrappleOrbit.FlingVelocity(s, 9.0, 1.6f), what: "fling");
            // A spinless hold still has a well-formed orbit plane, so the pose has an up.
            Assert.AreEqual(1f, s.Axis.magnitude, Tol);
            Assert.AreEqual(0f, Vector3.Dot(s.Axis, s.Radial0), Tol);
        }

        [Test]
        public void GlancingContact_OrbitSpeedIsTheTangentialSpeed()
        {
            var s = Glancing(Vector3.zero);
            Assert.AreEqual(60f / Radius, s.AngularSpeed, Tol, "angular speed = |v_tan| / R");
            Assert.AreEqual(60f, ScarabGrappleOrbit.OrbitalSpeed(s), Tol);
            // Axis is perpendicular to both the radial and the tangent, and right-handed so the
            // hull circles from +x toward +z: axis = x × z = −y.
            AssertVector(Vector3.down, s.Axis, what: "axis");
        }

        [Test]
        public void MotionIsContinuousAtTheContact()
        {
            // At t0 the hull is exactly where it stuck and moving exactly with the tangential
            // component it arrived with — no snap in position, no snap in velocity.
            var s = Glancing(Vector3.zero, t0: 100.0);
            AssertVector(Ball + Vector3.right * Radius, ScarabGrappleOrbit.PositionAt(s, 100.0, Ball), what: "position");
            AssertVector(Vector3.forward * 60f, ScarabGrappleOrbit.RelativeVelocityAt(s, 100.0), what: "velocity");
        }

        [Test]
        public void QuarterTurnLater_TheHullIsWhereTheTangentPointed()
        {
            var s = Glancing(Vector3.zero, t0: 0.0);
            double quarter = (Mathf.PI / 2f) / s.AngularSpeed;
            AssertVector(Vector3.forward, ScarabGrappleOrbit.RadialAt(s, quarter), what: "radial after a quarter turn");
            AssertVector(Vector3.left * 60f, ScarabGrappleOrbit.RelativeVelocityAt(s, quarter), what: "tangent after a quarter turn");
            // Radius is exact at every phase.
            for (int i = 0; i <= 8; i++)
            {
                double t = quarter * i * 0.5;
                Assert.AreEqual(1f, ScarabGrappleOrbit.RadialAt(s, t).magnitude, Tol);
                Assert.AreEqual(Radius, (ScarabGrappleOrbit.PositionAt(s, t, Ball) - Ball).magnitude, Tol);
            }
        }

        [Test]
        public void TangentIsAlwaysPerpendicularToTheRadial()
        {
            var s = Glancing(Vector3.zero, t0: 3.0);
            for (int i = 0; i < 12; i++)
            {
                double t = 3.0 + i * 0.137;
                Vector3 radial = ScarabGrappleOrbit.RadialAt(s, t);
                Vector3 tangent = ScarabGrappleOrbit.RelativeVelocityAt(s, t);
                Assert.AreEqual(0f, Vector3.Dot(radial, tangent), Tol);
                Assert.AreEqual(60f, tangent.magnitude, Tol);
            }
        }

        [Test]
        public void AMovingBallIsCarried_TheOrbitIsInTheBallsFrame()
        {
            // Same hull motion RELATIVE to the ball, whether the ball is still or flying: the
            // ball's velocity never enters the orbit. The carry is the parametrisation itself —
            // the hull is placed relative to wherever the ball is.
            var still = Glancing(Vector3.zero, t0: 0.0);
            var moving = Glancing(new Vector3(20f, -5f, 150f), t0: 0.0);
            Assert.AreEqual(still.AngularSpeed, moving.AngularSpeed, Tol);
            AssertVector(still.Axis, moving.Axis, what: "axis");
            AssertVector(still.Radial0, moving.Radial0, what: "radial");

            Vector3 ballLater = Ball + new Vector3(20f, -5f, 150f) * 0.5f;
            Vector3 hullLater = ScarabGrappleOrbit.PositionAt(moving, 0.5, ballLater);
            Assert.AreEqual(Radius, (hullLater - ballLater).magnitude, Tol);
        }

        [Test]
        public void ReleaseFlingsAlongTheSwingAtThatMoment()
        {
            var s = Glancing(Vector3.zero, t0: 0.0);
            double t = 0.31;
            Vector3 tangent = ScarabGrappleOrbit.RelativeVelocityAt(s, t);
            Vector3 fling = ScarabGrappleOrbit.FlingVelocity(s, t, 1.6f);
            AssertVector(tangent * 1.6f, fling, what: "fling");
            Assert.AreEqual(96f, fling.magnitude, Tol, "orbital speed × multiplier");
            // A later release throws somewhere else: the release MOMENT is the aim.
            Vector3 flingLater = ScarabGrappleOrbit.FlingVelocity(s, t + 0.2, 1.6f);
            Assert.Greater(Vector3.Angle(fling, flingLater), 30f);
        }

        [Test]
        public void HeldBallSpinFollowsTheOrbit()
        {
            var s = Glancing(Vector3.zero);
            AssertVector(s.Axis * s.AngularSpeed, ScarabGrappleOrbit.BallSpin(s, 1f), what: "spin");
            AssertVector(Vector3.zero, ScarabGrappleOrbit.BallSpin(s, 0f), what: "spin off");
        }

        [Test]
        public void PoseFacesTheSwingWithTheBellyToTheBall()
        {
            var s = Glancing(Vector3.zero, t0: 0.0);
            Vector3 radial = ScarabGrappleOrbit.RadialAt(s, 0.0);
            Vector3 tangent = ScarabGrappleOrbit.RelativeVelocityAt(s, 0.0);
            var rot = ScarabGrappleOrbit.PoseRotation(radial, tangent, Vector3.forward);
            AssertVector(Vector3.forward, rot * Vector3.forward, what: "nose along the tangent");
            AssertVector(Vector3.right, rot * Vector3.up, what: "up = radial (belly to the ball)");

            // Spinless: keeps the heading it arrived with, projected onto the tangent plane.
            var rest = ScarabGrappleOrbit.PoseRotation(Vector3.right, Vector3.zero, new Vector3(1f, 0f, 1f).normalized);
            AssertVector(Vector3.forward, rest * Vector3.forward, what: "spinless nose");
            AssertVector(Vector3.right, rest * Vector3.up, what: "spinless up");
        }

        /// <summary>
        /// THE DTO GUARD. <c>ScarabBallGrapple.GrappleState</c> hand-writes a
        /// <c>NetworkSerialize</c> that walks this struct field by field, and a field added here
        /// but not added there compiles, reads correctly on the server, and arrives at every other
        /// peer as <c>default</c> — the exact silent-by-omission failure CLAUDE.md records for
        /// <c>NetworkExplodeParams</c> (a flag that worked in a solo editor test and did nothing
        /// across the wire). This pins the struct's shape so the next field fails HERE, BY NAME,
        /// with the file to edit in the message.
        /// </summary>
        [Test]
        public void EveryOrbitFieldIsCarriedByTheGrappleDTO()
        {
            var serialized = new[] { "Axis", "Radial0", "Radius", "AngularSpeed", "StartTime" };
            var actual = typeof(ScarabGrappleOrbitState)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .ToArray();

            foreach (var name in actual)
                Assert.IsTrue(System.Array.IndexOf(serialized, name) >= 0,
                    $"ScarabGrappleOrbitState.{name} is not serialized by " +
                    "ScarabBallGrapple.GrappleState.NetworkSerialize — add it there (and to this " +
                    "list). Until you do, every peer but the server reads it as default, which is " +
                    "a grapple whose orbit silently differs across the wire.");

            foreach (var name in serialized)
                Assert.IsTrue(System.Array.IndexOf(actual, name) >= 0,
                    $"GrappleState.NetworkSerialize claims to carry ScarabGrappleOrbitState.{name}, " +
                    "which no longer exists — remove it from the serializer and from this list.");
        }

        [Test]
        public void DegenerateContactAtTheCentreStillYieldsAnOrbit()
        {
            var s = ScarabGrappleOrbit.FromContact(Ball, Vector3.zero, Ball, Vector3.forward * 50f,
                                                   Radius, 0.0, Vector3.up);
            Assert.IsTrue(s.IsValid);
            Assert.AreEqual(1f, s.Radial0.magnitude, Tol);
            Assert.AreEqual(Radius, (ScarabGrappleOrbit.PositionAt(s, 0.0, Ball) - Ball).magnitude, Tol);
        }
    }
}
