using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pilot's hands: turns "rotate the ship like THIS" into the three stick axes a human would
    /// push, for the flight model <see cref="VesselTransformer"/> actually runs.
    ///
    /// <para><b>The plant has a lag the old autopilot never modelled.</b> The stick does not turn the
    /// ship. It turns a COMMANDED rotation (<c>accumulatedRotation</c>) about the ship's own axes, at
    /// up to the Pitch/Yaw/Roll scaler in degrees per second, and the transform then chases the
    /// commanded rotation with <c>Slerp(transform, commanded, FollowRate * dt)</c> — so the ship
    /// turns at <c>k</c> times the rotation between the two (<c>k</c> = FollowRate, 1.5 / s), about
    /// that rotation's own axis. The ship's turn rate is therefore a first-order lag of the stick
    /// with a 0.67 s time constant, and holding a turn at rate <c>ω</c> means holding the commanded
    /// rotation <c>|ω| / k</c> AHEAD of the ship: 51° for the 77°/s a Skim Race bend asks at full
    /// boost. A pilot that steers the stick at its target (<c>AIPilot</c>'s cubic law) steers with
    /// the ship's turn 0.67 s out of date, which is invisible at cruise and is exactly the overshoot
    /// that puts a hull into a ribbon at speed.</para>
    ///
    /// <para><b>So this servoes the COMMANDED ROTATION, not the stick.</b> For a desired ship rate
    /// <c>ω</c> the commanded rotation must be <c>C* = exp(ω / k) · T</c> (T = the transform); the
    /// stick feeds forward how fast C* itself moves (<c>ω + ω' / k</c>) and closes the rotation from
    /// C to C* at <see cref="SkimRacerProfile.ServoGain"/>. The composition is the EXACT quaternion
    /// one: at a 50° lag a difference of rotation vectors is a different rotation, and the first
    /// version of this controller, which subtracted them, wound its own lag up past 70° in a
    /// twisting bend and flew the hull into the plates.</para>
    ///
    /// <para>The axes are the TRANSFORM's (the engine rotates the commanded rotation about
    /// <c>transform.right / up / forward</c>, not about its own), so the decomposition below uses them
    /// too. A positive rotation vector along <c>right</c> is nose-down — the same sign
    /// <c>YSum &gt; 0</c> produces.</para>
    ///
    /// Pure. Compiled and run by Tools/Build/squirrel_ai_harness.
    /// </summary>
    public static class SkimFlightController
    {
        /// <summary>Axis times angle (radians) of <paramref name="q"/>, along the shorter arc.</summary>
        public static Vector3 RotationVector(Quaternion q)
        {
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            var v = new Vector3(q.x, q.y, q.z);
            float s = v.magnitude;
            if (s < 1e-7f) return v * 2f;
            float angle = 2f * Mathf.Atan2(s, q.w);
            return v * (angle / s);
        }

        /// <summary>The rotation vector (axis times radians) that turns unit direction
        /// <paramref name="from"/> onto <paramref name="to"/>. Antiparallel directions turn about
        /// <paramref name="fallbackAxis"/>.</summary>
        public static Vector3 TurnVector(Vector3 from, Vector3 to, Vector3 fallbackAxis)
        {
            Vector3 axis = Vector3.Cross(from, to);
            float sin = axis.magnitude;
            float cos = Vector3.Dot(from, to);
            if (sin < 1e-6f)
                return cos > 0f ? Vector3.zero : fallbackAxis.normalized * Mathf.PI;
            return axis * (Mathf.Atan2(sin, cos) / sin);
        }

        /// <summary>The rotation whose axis and angle (radians) are those of <paramref name="v"/>.</summary>
        public static Quaternion FromRotationVector(Vector3 v)
        {
            float angle = v.magnitude;
            if (angle < 1e-7f) return Quaternion.identity;
            return Quaternion.AngleAxis(angle * Mathf.Rad2Deg, v / angle);
        }

        /// <summary>
        /// Stick axes that make the ship's TRANSFORM rotate at <paramref name="omega"/> (world,
        /// radians per second), changing at <paramref name="omegaRate"/>. Pitch and yaw are scaled
        /// together when either saturates, so a saturated command still turns the nose the way it was
        /// asked to; roll saturates on its own.
        /// </summary>
        /// <param name="servoGain">How fast (1/s) the commanded rotation is pulled onto the one that
        /// produces <paramref name="omega"/>. 0 is pure feed-forward.</param>
        public static void Solve(in SkimRacerSensors s, Vector3 omega, Vector3 omegaRate, float servoGain,
                                 out float xSum, out float ySum, out float yDiff)
        {
            float k = Mathf.Max(0.05f, s.FollowRate);
            Quaternion target = FromRotationVector(omega / k) * s.Rotation;
            Vector3 err = RotationVector(target * Quaternion.Inverse(s.CommandedRotation));
            Vector3 u = omega + omegaRate / k + err * servoGain;

            Vector3 right = s.Rotation * Vector3.right;
            Vector3 up = s.Rotation * Vector3.up;
            Vector3 fwd = s.Rotation * Vector3.forward;

            float pitch = Mathf.Max(1f, s.PitchRateDegrees) * Mathf.Deg2Rad;
            float yaw = Mathf.Max(1f, s.YawRateDegrees) * Mathf.Deg2Rad;
            float roll = Mathf.Max(1f, s.RollRateDegrees) * Mathf.Deg2Rad;

            ySum = Vector3.Dot(u, right) / pitch;
            xSum = Vector3.Dot(u, up) / yaw;
            yDiff = Vector3.Dot(u, fwd) / roll;

            float m = Mathf.Max(Mathf.Abs(xSum), Mathf.Abs(ySum));
            if (m > 1f) { xSum /= m; ySum /= m; }
            yDiff = Mathf.Clamp(yDiff, -1f, 1f);
        }
    }
}
