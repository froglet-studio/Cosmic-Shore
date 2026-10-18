#if UNITY_EDITOR
using CosmicShore.Editor.AI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="ThirdEyeRig"/> - the Third Eye window's camera math. These pin the properties a
    /// playtest would notice: the Chase camera sits behind and above the hull in the hull's own frame
    /// and catches up without lag at speed, Follow stays world-up, a segment half behind the camera is
    /// clipped rather than drawn backwards, and an off-screen pilot's marker lands on the edge you
    /// would turn to.
    /// </summary>
    public class ThirdEyeRigTests
    {
        const float Eps = 1e-3f;

        [Test]
        public void Damp_ZeroRateIsRigid_PositiveRateIsAFraction()
        {
            Assert.AreEqual(1f, ThirdEyeRig.Damp(0f, 0.016f));
            float k = ThirdEyeRig.Damp(8f, 0.016f);
            Assert.Greater(k, 0f);
            Assert.Less(k, 1f);
            Assert.AreEqual(0f, ThirdEyeRig.Damp(8f, 0f), Eps);
        }

        [Test]
        public void Chase_Snap_SitsBehindAndAboveInTheHullFrame()
        {
            var hullRot = Quaternion.Euler(0f, 90f, 30f);
            Vector3 hullPos = new(100f, 20f, -50f);
            Vector3 offset = Vector3.zero, up = Vector3.up;

            var pose = ThirdEyeRig.Chase(hullPos, hullRot, ref offset, ref up, 30f, 8f, 40f, 8f, 0.016f, true);

            Vector3 local = Quaternion.Inverse(hullRot) * (pose.Position - hullPos);
            Assert.AreEqual(-30f, local.z, Eps);
            Assert.AreEqual(8f, local.y, Eps);
            Assert.AreEqual(0f, local.x, Eps);
            Assert.Greater(Vector3.Dot(pose.Rotation * Vector3.forward, hullRot * Vector3.forward), 0.9f,
                "the chase camera looks the way the hull flies");
            Assert.Greater(Vector3.Dot(pose.Rotation * Vector3.up, hullRot * Vector3.up), 0.9f, "it rolls with the hull");
        }

        [Test]
        public void Chase_SmoothsTheOffsetNotThePosition_SoSpeedNeverAddsLag()
        {
            var rot = Quaternion.identity;
            Vector3 offset = Vector3.zero, up = Vector3.up;
            ThirdEyeRig.Chase(Vector3.zero, rot, ref offset, ref up, 30f, 8f, 40f, 4f, 0.016f, true);

            // The hull jumps 500 u forward in one frame (a 5x boost at 30 fps): the camera keeps its offset.
            var pose = ThirdEyeRig.Chase(new Vector3(0f, 0f, 500f), rot, ref offset, ref up, 30f, 8f, 40f, 4f, 0.033f, false);
            Assert.AreEqual(470f, pose.Position.z, Eps);
        }

        [Test]
        public void Follow_IsWorldUp_AndKeepsItsHeadingWhenTheHullPointsStraightUp()
        {
            Vector3 offset = Vector3.zero, heading = Vector3.zero;
            var level = ThirdEyeRig.Follow(Vector3.zero, Quaternion.identity, ref offset, ref heading, 90f, 35f, 3f, 0.016f, true);
            Assert.AreEqual(-90f, level.Position.z, Eps);
            Assert.AreEqual(35f, level.Position.y, Eps);
            Assert.Greater(Vector3.Dot(level.Rotation * Vector3.up, Vector3.up), 0.9f);

            var straightUp = Quaternion.LookRotation(Vector3.up, Vector3.back);
            var climbing = ThirdEyeRig.Follow(Vector3.zero, straightUp, ref offset, ref heading, 90f, 35f, 3f, 0.016f, true);
            Assert.AreEqual(-90f, climbing.Position.z, Eps, "the last horizontal heading is kept");
        }

        [Test]
        public void FreeFly_MovesAlongItsOwnForward_AndClampsDiagonals()
        {
            var pose = ThirdEyeRig.FreeFly(Vector3.zero, 90f, 0f, Vector3.forward, 10f, 1f);
            Assert.AreEqual(10f, pose.Position.x, Eps);
            var diagonal = ThirdEyeRig.FreeFly(Vector3.zero, 0f, 0f, new Vector3(1f, 0f, 1f), 10f, 1f);
            Assert.AreEqual(10f, diagonal.Position.magnitude, Eps);
        }

        [Test]
        public void Orbit_SitsAtTheDistance_LookingAtTheHull()
        {
            Vector3 hull = new(5f, 6f, 7f);
            var pose = ThirdEyeRig.Orbit(hull, 45f, 20f, 80f);
            Assert.AreEqual(80f, Vector3.Distance(pose.Position, hull), Eps);
            Assert.Greater(Vector3.Dot(pose.Rotation * Vector3.forward, (hull - pose.Position).normalized), 0.999f);
        }

        [Test]
        public void YawPitch_RoundTripsAnOrbit()
        {
            ThirdEyeRig.YawPitch(Quaternion.Euler(-30f, 120f, 0f), out float yaw, out float pitch);
            Assert.AreEqual(120f, yaw, Eps);
            Assert.AreEqual(-30f, pitch, Eps);
        }

        [Test]
        public void ClipToFront_KeepsTheFrontPart_AndRejectsAWhollyHiddenSegment()
        {
            Vector3 a = new(0f, 0f, 10f), b = new(0f, 0f, -10f);
            Assert.IsTrue(ThirdEyeRig.ClipToFront(a, 10f, b, -10f, 1f, out var ca, out var cb));
            Assert.AreEqual(a, ca);
            Assert.AreEqual(1f, cb.z, Eps, "cut at the near plane");

            Assert.IsFalse(ThirdEyeRig.ClipToFront(a, -1f, b, -5f, 1f, out _, out _));
            Assert.IsTrue(ThirdEyeRig.ClipToFront(a, 3f, b, 4f, 1f, out ca, out cb));
            Assert.AreEqual(a, ca);
            Assert.AreEqual(b, cb);
        }

        [Test]
        public void EdgePoint_OnScreenStays_OffScreenPins_BehindMirrors()
        {
            Assert.AreEqual(new Vector2(0.3f, 0.6f), ThirdEyeRig.EdgePoint(new Vector3(0.3f, 0.6f, 5f), 0.04f));

            Vector2 right = ThirdEyeRig.EdgePoint(new Vector3(3f, 0.5f, 5f), 0.04f);
            Assert.AreEqual(0.96f, right.x, Eps);
            Assert.AreEqual(0.5f, right.y, Eps);

            // Behind the camera and to its right on the viewport: you turn LEFT to see it.
            Vector2 behind = ThirdEyeRig.EdgePoint(new Vector3(0.9f, 0.5f, -5f), 0.04f);
            Assert.AreEqual(0.04f, behind.x, Eps);
        }

        [Test]
        public void LookRotationSafe_NeverWarnsOnDegenerateInput()
        {
            var fallback = Quaternion.Euler(0f, 33f, 0f);
            Assert.AreEqual(fallback, ThirdEyeRig.LookRotationSafe(Vector3.zero, Vector3.up, fallback));
            var straightUp = ThirdEyeRig.LookRotationSafe(Vector3.up, Vector3.up, fallback);
            Assert.Greater(Vector3.Dot(straightUp * Vector3.forward, Vector3.up), 0.999f);
        }
    }
}
#endif
