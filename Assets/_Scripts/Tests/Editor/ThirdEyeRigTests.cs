#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="ThirdEyeRig"/> - the in-game Third Eye's camera math. These pin the properties a
    /// playtest would notice: Frame pilot's one still pose is world-up and behind the hull's heading, the
    /// rig has NO pose that follows a hull (a hull is watched through the game's own camera, whoever flies
    /// it), a segment half behind the camera is clipped rather than drawn backwards, and an off-screen
    /// pilot's marker lands on the edge you would turn to.
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
        public void FramePose_IsWorldUp_BehindTheHeading_AndFramesAHullPointingStraightUp()
        {
            var level = ThirdEyeRig.FramePose(Vector3.zero, Quaternion.identity, 90f, 35f);
            Assert.AreEqual(-90f, level.Position.z, Eps);
            Assert.AreEqual(35f, level.Position.y, Eps);
            Assert.Greater(Vector3.Dot(level.Rotation * Vector3.up, Vector3.up), 0.9f);
            Assert.Greater(Vector3.Dot(level.Rotation * Vector3.forward, -level.Position.normalized), 0.999f, "it looks at the hull");

            // A rolled, yawed hull is still framed world-up from behind its flattened heading.
            var banked = ThirdEyeRig.FramePose(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 60f), 90f, 35f);
            Assert.AreEqual(-80f, banked.Position.x, Eps);
            Assert.AreEqual(35f, banked.Position.y, Eps);

            var straightUp = Quaternion.LookRotation(Vector3.up, Vector3.back);
            var climbing = ThirdEyeRig.FramePose(Vector3.zero, straightUp, 90f, 35f);
            Assert.AreEqual(-90f, climbing.Position.z, Eps, "no horizontal heading: framed from world back");
        }

        /// <summary>
        /// The one camera rule (Yash, 2026-11-22): the AI Diagnose panel has no camera that follows a hull. Its Chase
        /// (30 behind), Follow and Orbit poses watched an AI-flown hull from nearer than the player's own camera. A
        /// pose that takes smoothing state by ref, or one of those names coming back, is a follow camera coming back.
        /// </summary>
        [Test]
        public void TheRigHasNoPoseThatFollowsAHull()
        {
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
            foreach (var method in typeof(ThirdEyeRig).GetMethods(flags))
            {
                CollectionAssert.DoesNotContain(new[] { "Chase", "Follow", "Orbit" }, method.Name,
                    "a hull is watched through the game's own camera; Free fly is the only extra camera");
                if (method.ReturnType != typeof(ThirdEyeRig.Pose)) continue;
                foreach (var parameter in method.GetParameters())
                    Assert.IsFalse(parameter.ParameterType.IsByRef,
                        $"{method.Name} carries state between frames ({parameter.Name}): that is a camera following something");
            }
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
        public void YawPitch_ReadsARotationsYawAndPitch()
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
