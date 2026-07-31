#if UNITY_EDITOR
using System;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// SkimmerSwingKinematics Tests - validates the Rhino sword's velocity model against an
    /// analytic ground truth.
    ///
    /// WHY THIS MATTERS:
    /// When the sword destroys a prism, the velocity handed to that prism is the velocity of
    /// the PART of the blade that hit it - vessel velocity plus the blade's own motion on the
    /// lever arm. Get a frame or a cross-product backwards and the sword's tip either stops
    /// reading as fast or, worse, hurls debris in the wrong direction on every swipe. The model
    /// is pure math over a differentiated pose, so it can be checked exactly here rather than
    /// eyeballed in play mode.
    ///
    /// The reference implementation below rebuilds the sword's real rig from Rhino.prefab
    /// (mount at (0, 9.38, 20.7) on the Fusilage, 20 deg raised rest pose, 90/90/65 sweep from
    /// RhinoShieldSwipeConfig) and differentiates a material point's true world position
    /// analytically. RelativeVelocity must reproduce it.
    /// </summary>
    [TestFixture]
    public class SkimmerSwingKinematicsTests
    {
        // --- the authored sword rig ---
        static readonly Vector3 MountLocalPosition = new(0f, 9.38f, 20.7f);
        static readonly Quaternion MountLocalRotation = Quaternion.AngleAxis(20f, Vector3.right);
        const float RestHalfLength = 15f;   // 0.5 * localScale.y (30)
        const float SwipeYaw = 90f, SwipeRoll = 90f, ChopPitch = 65f;

        // Central-difference step for the analytic reference. Deliberately not tiny: Unity's
        // Quaternion is float32 and recovering an angle near identity is ill-conditioned, so
        // shrinking H trades truncation error for far worse float noise. 2ms balances the two.
        const float H = 2e-3f;

        // Relative tolerance, with an absolute floor for near-stationary points. A genuine
        // frame/sign error shows up as tens of percent, so 1% is a sharp gate.
        const float RelativeTolerance = 0.01f;
        const float AbsoluteFloor = 0.5f;   // world units/sec

        static void AssertVelocity(string label, Vector3 expected, Vector3 actual)
        {
            float allowed = Mathf.Max(AbsoluteFloor, expected.magnitude * RelativeTolerance);
            Assert.That((actual - expected).magnitude, Is.LessThan(allowed),
                $"{label}: expected {expected} (|v|={expected.magnitude:0.##}) but model gave {actual}");
        }

        static Quaternion Sweep(float diff, float sum) =>
            Quaternion.AngleAxis(diff * SwipeYaw, Vector3.up)
            * Quaternion.AngleAxis(diff * SwipeRoll, Vector3.forward)
            * Quaternion.AngleAxis(0.5f * sum * ChopPitch, Vector3.right);

        /// <summary>A pose of the whole rig at time t: vessel + swipe controls + blade length.</summary>
        struct Pose
        {
            public Vector3 VesselPosition;
            public Quaternion VesselRotation;
            public float Diff, Sum, HalfLength;
        }

        /// <summary>
        /// The blade's pose in the vessel's frame, exactly as ShieldSwipeActionExecutor writes
        /// it: the sweep is applied to BOTH the mount position and the rest rotation, so the
        /// blade pivots about the Fusilage origin instead of spinning in place.
        /// </summary>
        static void BladeLocal(in Pose p, out Vector3 position, out Quaternion rotation)
        {
            var sweep = Sweep(p.Diff, p.Sum);
            position = sweep * MountLocalPosition;
            rotation = sweep * MountLocalRotation;
        }

        /// <summary>
        /// True world position of the material point that sits at rest-frame offset
        /// <paramref name="restOffset"/> along the blade. The offset scales with the blade's
        /// length, so a growing sword really does carry its points outward.
        /// </summary>
        static Vector3 WorldPoint(in Pose p, Vector3 restOffset)
        {
            BladeLocal(p, out var localPos, out var localRot);
            Vector3 scaled = restOffset * (p.HalfLength / RestHalfLength);
            return p.VesselPosition + p.VesselRotation * (localPos + localRot * scaled);
        }

        /// <summary>Ground truth: the point's instantaneous world velocity, by central difference.</summary>
        static Vector3 TrueVelocity(Func<float, Pose> trajectory, float t, Vector3 restOffset)
        {
            Vector3 a = WorldPoint(trajectory(t - H), restOffset);
            Vector3 b = WorldPoint(trajectory(t + H), restOffset);
            return (b - a) / (2f * H);
        }

        /// <summary>
        /// Build the model's Sample for time <paramref name="t"/>: state read AT t, rates
        /// central-differenced across t +/- H. This mirrors what LateUpdate stores, except the
        /// blade pose here is vessel-local by construction (the Fusilage sits at the vessel
        /// origin with identity rotation) instead of being un-mapped from world.
        /// </summary>
        static SkimmerSwingKinematics.Sample SampleAt(Func<float, Pose> trajectory, float t)
        {
            Pose prev = trajectory(t - H), now = trajectory(t), next = trajectory(t + H);
            float invDt = 1f / (2f * H);

            BladeLocal(prev, out var prevPos, out var prevRot);
            BladeLocal(now, out var nowPos, out var nowRot);
            BladeLocal(next, out var nextPos, out var nextRot);

            return new SkimmerSwingKinematics.Sample
            {
                VesselPosition = now.VesselPosition,
                VesselRotation = now.VesselRotation,
                VesselAngularVelocity = SkimmerSwingKinematics.AngularVelocity(
                    prev.VesselRotation, next.VesselRotation, invDt, 0f),

                BladeLocalPosition = nowPos,
                BladeLocalRotation = nowRot,
                BladeLinearVelocity = (nextPos - prevPos) * invDt,
                BladeAngularVelocity = SkimmerSwingKinematics.AngularVelocity(prevRot, nextRot, invDt, 0f),

                HalfLength = now.HalfLength,
                HalfLengthRate = (next.HalfLength - prev.HalfLength) * invDt,
            };
        }

        static void AssertMatchesGroundTruth(string label, Func<float, Pose> trajectory, float t, float t01)
        {
            Vector3 restOffset = Vector3.up * ((2f * t01 - 1f) * RestHalfLength);

            Vector3 expected = TrueVelocity(trajectory, t, restOffset);
            Vector3 vesselVelocity =
                (trajectory(t + H).VesselPosition - trajectory(t - H).VesselPosition) / (2f * H);
            Vector3 actual = vesselVelocity + SkimmerSwingKinematics.RelativeVelocity(
                SampleAt(trajectory, t), WorldPoint(trajectory(t), restOffset), Vector3.up, true, true);

            AssertVelocity($"{label} @ t={t:0.###} t01={t01:0.##}", expected, actual);
        }

        // --- trajectories: vessel cruising forward at 35 u/s, sword doing various things ---

        static Pose Cruise(float t) => new()
        {
            VesselPosition = new Vector3(0f, 0f, 35f) * t,
            VesselRotation = Quaternion.identity,
            Diff = 0f, Sum = 0f, HalfLength = RestHalfLength,
        };

        static Pose Swipe(float t) => new()
        {
            VesselPosition = new Vector3(0f, 0f, 35f) * t,
            VesselRotation = Quaternion.identity,
            Diff = Mathf.Sin(2f * Mathf.PI * t / 0.72f),
            Sum = Mathf.Sin(2f * Mathf.PI * t / 0.72f) * Mathf.Sin(2f * Mathf.PI * t / 0.72f),
            HalfLength = RestHalfLength,
        };

        static Pose SwipeAndTurn(float t)
        {
            var p = Swipe(t);
            p.VesselRotation = Quaternion.AngleAxis(120f * t, Vector3.up) * Quaternion.AngleAxis(70f * t, Vector3.forward);
            return p;
        }

        static Pose Growing(float t)
        {
            var p = Cruise(t);
            p.HalfLength = RestHalfLength + 45f * t;
            return p;
        }

        static Pose Everything(float t)
        {
            var p = SwipeAndTurn(t);
            p.HalfLength = RestHalfLength + 45f * t;
            return p;
        }

        static readonly float[] SampleTimes = { 0.05f, 0.12f, 0.2f, 0.31f };
        static readonly float[] AlongBlade = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        #region Composition matches ground truth

        [Test]
        public void RelativeVelocity_PureSwipe_MatchesAnalyticGroundTruth()
        {
            foreach (var t in SampleTimes)
            foreach (var t01 in AlongBlade)
                AssertMatchesGroundTruth("swipe", Swipe, t, t01);
        }

        [Test]
        public void RelativeVelocity_SwipeWhileVesselTurns_MatchesAnalyticGroundTruth()
        {
            foreach (var t in SampleTimes)
            foreach (var t01 in AlongBlade)
                AssertMatchesGroundTruth("swipe+turn", SwipeAndTurn, t, t01);
        }

        [Test]
        public void RelativeVelocity_BladeGrowing_MatchesAnalyticGroundTruth()
        {
            foreach (var t in SampleTimes)
            foreach (var t01 in AlongBlade)
                AssertMatchesGroundTruth("growth", Growing, t, t01);
        }

        [Test]
        public void RelativeVelocity_SwipeTurnAndGrowth_MatchesAnalyticGroundTruth()
        {
            foreach (var t in SampleTimes)
            foreach (var t01 in AlongBlade)
                AssertMatchesGroundTruth("everything", Everything, t, t01);
        }

        #endregion

        #region The behaviours the feature exists for

        [Test]
        public void RelativeVelocity_IdleSword_IsZero()
        {
            // No swipe, no growth: the model must add nothing, so impacts collapse to exactly
            // the pre-model behaviour (vessel Course * Speed) for a sword just being carried.
            var sample = SampleAt(Cruise, 0.2f);
            Vector3 tip = WorldPoint(Cruise(0.2f), Vector3.up * RestHalfLength);
            Vector3 relative = SkimmerSwingKinematics.RelativeVelocity(sample, tip, Vector3.up, true, true);

            Assert.That(relative.magnitude, Is.LessThan(AbsoluteFloor));
        }

        [Test]
        public void RelativeVelocity_MidSwipe_TipIsFasterThanBladeMidpoint()
        {
            // The whole point of the feature: farther out on the lever arm = faster.
            var sample = SampleAt(Swipe, 0.12f);
            var pose = Swipe(0.12f);

            float Speed(float t01)
            {
                Vector3 offset = Vector3.up * ((2f * t01 - 1f) * RestHalfLength);
                return SkimmerSwingKinematics
                    .RelativeVelocity(sample, WorldPoint(pose, offset), Vector3.up, true, true).magnitude;
            }

            Assert.That(Speed(1f), Is.GreaterThan(Speed(0.5f)),
                "the sword tip must swing faster than its midpoint");
            Assert.That(Speed(1f), Is.GreaterThan(150f),
                "a full swipe should carry the tip far above the vessel's own ~35 u/s");
        }

        [Test]
        public void RelativeVelocity_VesselRotationTerm_CanBeDisabled()
        {
            var sample = SampleAt(SwipeAndTurn, 0.12f);
            Vector3 tip = WorldPoint(SwipeAndTurn(0.12f), Vector3.up * RestHalfLength);

            Vector3 with = SkimmerSwingKinematics.RelativeVelocity(sample, tip, Vector3.up, true, true);
            Vector3 without = SkimmerSwingKinematics.RelativeVelocity(sample, tip, Vector3.up, false, true);

            Assert.AreNotEqual(with, without, "the vessel-rotation term must actually contribute");
            Assert.That((with - without - Vector3.Cross(sample.VesselAngularVelocity, tip - sample.VesselPosition)).magnitude,
                Is.LessThan(AbsoluteFloor), "the difference must be exactly omega_vessel x r");
        }

        [Test]
        public void RelativeVelocity_ElongationTerm_PushesTheTipOutward()
        {
            var sample = SampleAt(Growing, 0.2f);
            var pose = Growing(0.2f);
            Vector3 tip = WorldPoint(pose, Vector3.up * RestHalfLength);

            Vector3 with = SkimmerSwingKinematics.RelativeVelocity(sample, tip, Vector3.up, true, true);
            Vector3 without = SkimmerSwingKinematics.RelativeVelocity(sample, tip, Vector3.up, true, false);

            BladeLocal(pose, out _, out var localRot);
            Vector3 axis = pose.VesselRotation * (localRot * Vector3.up);

            Assert.That(Vector3.Dot(with, axis), Is.GreaterThan(Vector3.Dot(without, axis)),
                "a lengthening blade must drive its tip along the blade axis");
        }

        #endregion

        #region AngularVelocity

        [Test]
        public void AngularVelocity_ConstantSpin_RecoversTheRate()
        {
            const float degreesPerSecond = 240f;
            const float dt = 1f / 60f;
            var from = Quaternion.identity;
            var to = Quaternion.AngleAxis(degreesPerSecond * dt, Vector3.up);

            Vector3 omega = SkimmerSwingKinematics.AngularVelocity(from, to, 1f / dt, 0f);

            Assert.That(omega.normalized.y, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(omega.magnitude, Is.EqualTo(degreesPerSecond * Mathf.Deg2Rad).Within(1e-3f));
        }

        [Test]
        public void AngularVelocity_TakesTheShortWayAround()
        {
            // A -10 deg step must read as -10 deg/frame, not +350.
            const float dt = 1f / 60f;
            var from = Quaternion.identity;
            var to = Quaternion.AngleAxis(-10f, Vector3.up);

            Vector3 omega = SkimmerSwingKinematics.AngularVelocity(from, to, 1f / dt, 0f);

            Assert.That(omega.magnitude * Mathf.Rad2Deg * dt, Is.EqualTo(10f).Within(1e-2f));
            Assert.That(Vector3.Dot(omega, Vector3.up), Is.LessThan(0f), "the spin must read as negative yaw");
        }

        [Test]
        public void AngularVelocity_ClampsARunawayFrame()
        {
            // A hitch that resolves as a huge delta must not become an absurd tip velocity.
            Vector3 omega = SkimmerSwingKinematics.AngularVelocity(
                Quaternion.identity, Quaternion.AngleAxis(170f, Vector3.up), 1f / 0.001f, 3600f);

            Assert.That(omega.magnitude, Is.EqualTo(3600f * Mathf.Deg2Rad).Within(1e-2f));
        }

        [Test]
        public void AngularVelocity_NoRotation_IsZero()
        {
            Vector3 omega = SkimmerSwingKinematics.AngularVelocity(
                Quaternion.identity, Quaternion.identity, 60f, 3600f);

            Assert.That(omega.magnitude, Is.LessThan(1e-5f));
        }

        #endregion
    }
}
#endif
