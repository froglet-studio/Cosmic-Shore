using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The stick a replayed vessel's pilot must have held, read back from how its recorded pose turned.
    /// A replay drives each hull by pose alone (<see cref="ReplayMatchDirector"/>), with its flight model and
    /// input off, so <c>InputStatus</c> sat idle and every hull's <c>VesselAnimation</c> played its idle pose:
    /// only a hull whose gait runs on speed (the Stoat's lope) moved. Feeding this stick back into
    /// <c>InputStatus</c> lets every hull's puppetry follow the turns it is seen making.
    ///
    /// The inverse of <c>VesselTransformer.Pitch/Yaw/Roll</c>: each applies
    /// <c>stick * (speed * RotationThrottleScaler + axisScaler) * dt</c> degrees about the hull's own right, up
    /// and forward axes, so a local turn rate divided by that full-stick rate is the stick, clamped to [-1, 1].
    /// Pure math, no scene: <c>ReplayPuppetryTests</c> holds it.
    /// </summary>
    public static class ReplayPuppetry
    {
        /// <summary>A pose step longer than this (a seek, a skip, a respawn) is not a turn.</summary>
        public const float MaxStepSeconds = 0.5f;

        /// <summary>Smoothing time constant, seconds: a recorded pose arrives at its own rate (10 Hz for a
        /// converted flight record), so the raw per-frame rate steps; this eases it like a thumb would.</summary>
        public const float EaseSeconds = 0.12f;

        /// <summary>Full-stick turn rates, degrees per second, about the hull's right (pitch), up (yaw) and
        /// forward (roll) axes.</summary>
        public static Vector3 FullStickRates(float speed, float rotationThrottleScaler, float pitchScaler,
            float yawScaler, float rollScaler, float turnMultiplier)
        {
            float fromSpeed = speed * rotationThrottleScaler;
            float m = Mathf.Max(0f, turnMultiplier);
            return new Vector3((fromSpeed + pitchScaler) * m, (fromSpeed + yawScaler) * m, (fromSpeed + rollScaler) * m);
        }

        /// <summary>The hull's local turn rate (x pitch, y yaw, z roll), degrees per second, from one pose to the
        /// next <paramref name="dt"/> seconds later. Zero for a step that is not a turn.</summary>
        public static Vector3 LocalTurnRate(Quaternion from, Quaternion to, float dt)
        {
            if (dt <= 1e-5f || dt > MaxStepSeconds) return Vector3.zero;
            var delta = Quaternion.Inverse(from) * to;
            delta.ToAngleAxis(out float angle, out var axis);
            if (float.IsNaN(axis.x) || float.IsInfinity(axis.x)) return Vector3.zero;
            if (angle > 180f) angle -= 360f;
            return axis * (angle / dt);
        }

        /// <summary>The stick (x pitch -> YSum, y yaw -> XSum, z roll -> YDiff) that turns the hull at
        /// <paramref name="rate"/> given its full-stick rates.</summary>
        public static Vector3 StickFor(Vector3 rate, Vector3 fullStick) => new(
            Axis(rate.x, fullStick.x), Axis(rate.y, fullStick.y), Axis(rate.z, fullStick.z));

        /// <summary>The throttle axis (XDiff, in [0, 1]) that holds <paramref name="speed"/>.</summary>
        public static float ThrottleFor(float speed, float minimumSpeed, float throttleScaler) =>
            throttleScaler > 1e-3f ? Mathf.Clamp01((speed - Mathf.Max(0f, minimumSpeed)) / throttleScaler) : 0f;

        /// <summary>Eases <paramref name="current"/> toward <paramref name="target"/> over <paramref name="dt"/>.</summary>
        public static Vector3 Ease(Vector3 current, Vector3 target, float dt) =>
            dt <= 0f ? current : Vector3.Lerp(current, target, 1f - Mathf.Exp(-dt / EaseSeconds));

        static float Axis(float rate, float fullStick) =>
            fullStick > 1e-3f ? Mathf.Clamp(rate / fullStick, -1f, 1f) : 0f;
    }
}
