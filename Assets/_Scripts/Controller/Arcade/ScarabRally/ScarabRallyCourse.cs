using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's ring race, stated against the SCARAB's own flight model. Every vessel number below
    /// is read off Scarab.prefab and ScarabVesselTransformer.cs (ScarabRallyCourseTests reads them
    /// back), and the walk is <see cref="SwitchbackCourse"/>, shared: what makes this course the
    /// Scarab's is the CUT.
    ///
    /// <para><b>The Scarab's speed is momentum.</b> Its throttle is an integrator, not a dial: RT pushes
    /// at <see cref="AccelerationPerSecond"/> along the nose up to <see cref="TopSpeed"/> (216 u/s,
    /// x1.5 at Time 10), and a released trigger sheds <see cref="CoastDragPerSecond"/> every second -
    /// so lifting for a corner costs a lot, and winding back up costs 2.4 s. Its turn rate is
    /// <c>speed x 0.1 + 100</c> deg/s, a <see cref="FullThrottleRadius"/> (~102 u) circle flat out.
    /// The drift (LT) decouples the course from the nose and keeps 100% of the speed; the juke
    /// (right stick to the rim) is a ~<see cref="JukeOffset"/> u sideways hop.</para>
    ///
    /// <para><b>So a level is a question about those three.</b> A hairpin is far tighter than the
    /// 102 u circle, so it is either a lift (shed speed, then wind back up) or a drift (keep it all);
    /// a mouth presented off the racing line is a juke rather than a correction.</para>
    /// <list type="number">
    /// <item><b>I1, learn the throttle:</b> wide mouths, gentle corners, long legs. Hold it down.</item>
    /// <item><b>I2, drift:</b> every second corner is a 90-150 degree hairpin.</item>
    /// <item><b>I3, the juke:</b> tighter mouths twisted further off the flow line, a hairpin every
    /// third corner, and Switchback's prism walls along the legs (<see cref="SwitchbackFeatures"/>).</item>
    /// <item><b>I4, gauntlet:</b> short legs, a hairpin every second corner, the tightest mouths and
    /// Switchback's danger prisms beside the racing line - the fast line is the risky one.</item>
    /// </list>
    /// </summary>
    public static class ScarabRallyCourse
    {
        // ── The vessel this course is cut for ────────────────────────────────

        /// <summary>`accelerationPerSecond` on ScarabVesselTransformer (its field initializer; the prefab
        /// does not override it).</summary>
        public const float AccelerationPerSecond = 90f;

        /// <summary>`coastDragPerSecond` on ScarabVesselTransformer - what a released throttle sheds.</summary>
        public const float CoastDragPerSecond = 120f;

        /// <summary>`baseTopSpeed` on ScarabVesselTransformer - the ceiling at Time 0.</summary>
        public const float TopSpeed = 216f;

        /// <summary>`PitchScaler` / `YawScaler` on Scarab.prefab.</summary>
        public const float TurnScaler = 100f;

        /// <summary>`RotationThrottleScaler` on Scarab.prefab: the turn rate grows 0.1 deg/s per u/s.</summary>
        public const float RotationThrottleScaler = 0.1f;

        /// <summary>`jukeSpeed` on Scarab.prefab's ScarabJukeController (sideways u/s).</summary>
        public const float JukeSpeed = 80f;

        /// <summary>`jukeDurationSeconds` on Scarab.prefab's ScarabJukeController.</summary>
        public const float JukeDurationSeconds = 0.5f;

        /// <summary>How far one juke moves the hull sideways: ~40 u.</summary>
        public const float JukeOffset = JukeSpeed * JukeDurationSeconds;

        /// <summary>Turn rate at <paramref name="speed"/>, deg/s - VesselTransformer's own formula.</summary>
        public static float TurnRateDegPerSec(float speed) => speed * RotationThrottleScaler + TurnScaler;

        /// <summary>The steady circle at <paramref name="speed"/>.</summary>
        public static float CornerRadiusAt(float speed) =>
            speed / (TurnRateDegPerSec(speed) * Mathf.Deg2Rad);

        /// <summary>THE number the hairpins are measured against: the circle at full throttle, ~102 u.</summary>
        public static float FullThrottleRadius => CornerRadiusAt(TopSpeed);

        /// <summary>Seconds to wind from a standstill back to the ceiling: 2.4 s - what a lift costs.</summary>
        public static float WindUpSeconds => TopSpeed / AccelerationPerSecond;

        /// <summary>The widest mouth, at intensity 1 - Switchback's, the race this one is cut from.</summary>
        public const float WidestRingRadius = 64f;

        /// <summary>The mouth radius per intensity: 64, 46, 32, 22. No step tighter than 0.72x, and the
        /// tightest is still more than half a juke across, so a juke can always put you in it.</summary>
        public static float RingRadiusForIntensity(int intensity) =>
            new[] { WidestRingRadius, 46f, 32f, 22f }[Mathf.Clamp(intensity, 1, 4) - 1];

        public static SwitchbackCourseSettings ForIntensity(int intensity)
        {
            int i = Mathf.Clamp(intensity, 1, 4);
            return new SwitchbackCourseSettings
            {
                // Never under the ~360 u an AI needs for its approach run (Switchback's floor). Every leg
                // is longer than a wind-up from rest (216^2 / (2 x 90) = 259 u), so a lift costs time and
                // never the next ring.
                MinStep = new[] { 420f, 400f, 420f, 360f }[i - 1],
                MaxStep = new[] { 620f, 620f, 620f, 520f }[i - 1],
                MaxTurnDegrees = new[] { 40f, 45f, 50f, 55f }[i - 1],
                // I3's demand is the juke: the mouths twist further off the line you arrive on.
                AxisJitterDegrees = new[] { 15f, 25f, 40f, 40f }[i - 1],
                MaxPresentDegrees = new[] { 40f, 50f, 60f, 60f }[i - 1],
                HairpinEvery = new[] { 0, 2, 3, 2 }[i - 1],
                HairpinMinDegrees = new[] { 0f, 90f, 90f, 110f }[i - 1],
                HairpinMaxDegrees = new[] { 0f, 150f, 130f, 150f }[i - 1],
                RingRadius = RingRadiusForIntensity(i),
                MinSeparation = new[] { 260f, 200f, 220f, 180f }[i - 1],
            };
        }
    }
}
