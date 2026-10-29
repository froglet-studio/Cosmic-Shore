using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Tiger Moth's drop, as pure functions of time (design: <c>R_VesselActions/TIGER_MOTH.md</c>). No Unity
    /// object, so the edit-mode tests run it directly and the executor only applies what it returns.
    ///
    /// <para><b>The geometry in one line.</b> The hull falls along its own DOWN without turning, and lands pitched
    /// exactly 90 degrees nose-down, so the line it fell on IS its new forward and it flies straight on. The chase
    /// camera rides that same pitched frame from the press, so the drop reads as flying forward and the landing
    /// needs no camera move at all: the hull turns INTO the frame the camera is already in. The turn is fixed at 90
    /// rather than tunable, because any other angle lands the nose off the line the hull is travelling.</para>
    ///
    /// <para>Unity is left-handed: a positive rotation about the hull's local +x (its right) pitches the nose
    /// DOWN, carrying forward onto -up and up onto forward.</para>
    /// </summary>
    public static class TigerMothDropMath
    {
        /// <summary>The landing pitch, in degrees. Fixed: see the type's remarks.</summary>
        public const float TurnDegrees = 90f;

        /// <summary>SmoothStep(0, 1, x) with x clamped: the one ease the drop uses for speed, camera and turn.</summary>
        public static float Ease01(float x)
        {
            float t = Mathf.Clamp01(x);
            return t * t * (3f - 2f * t);
        }

        /// <summary>The hull's frame pitched nose-down by <paramref name="fraction01"/> of the landing turn.</summary>
        public static Quaternion Pitched(Quaternion hull, float fraction01) =>
            hull * Quaternion.AngleAxis(TurnDegrees * Mathf.Clamp01(fraction01), Vector3.right);

        /// <summary>The frame the drop ends in, and the one the camera rides for the whole drop: forward along the
        /// hull's down at the press, up along its forward at the press.</summary>
        public static Quaternion DropFrame(Quaternion hullAtPress) => Pitched(hullAtPress, 1f);

        /// <summary>The direction of travel for the whole drop: the hull's own down at the press.</summary>
        public static Vector3 DropDirection(Quaternion hullAtPress) => hullAtPress * Vector3.down;

        /// <summary>The fall speed <paramref name="seconds"/> into the drop: eased in from rest over
        /// <paramref name="easeSeconds"/> (0 = full speed at once).</summary>
        public static float DropSpeed(float seconds, float dropSpeed, float easeSeconds) =>
            dropSpeed * (easeSeconds > 0f ? Ease01(seconds / easeSeconds) : 1f);

        /// <summary>How far the camera has swung up over the hull, 0..1, <paramref name="seconds"/> after the press.</summary>
        public static float CameraWeight(float seconds, float enterSeconds) =>
            enterSeconds > 0f ? Ease01(seconds / enterSeconds) : 1f;

        /// <summary>The landing at <paramref name="seconds"/> after the drop ends: the hull's pitch (fraction of
        /// the turn) and its speed along the drop line, from the fall speed it let go at down to the speed it was
        /// cruising at before the press. Both finish together at <paramref name="landSeconds"/>.</summary>
        public static (float turn01, float speed) Landing(float seconds, float landSeconds, float fromSpeed, float cruiseSpeed)
        {
            float k = landSeconds > 0f ? Mathf.Clamp01(seconds / landSeconds) : 1f;
            float e = Ease01(k);
            return (e, Mathf.Lerp(fromSpeed, cruiseSpeed, e));
        }

        /// <summary>
        /// The recharge a drop of <paramref name="distance"/> pays, before Time's multiplier: in proportion up to the
        /// medium distance, the full recharge there, and no more however far past it (Garrett, 2026-10-29: "anything
        /// after medium distance is free").
        /// </summary>
        public static float Cooldown(float distance, float fullDistance, float fullSeconds) =>
            fullSeconds * (fullDistance > 0f ? Mathf.Clamp01(Mathf.Max(0f, distance) / fullDistance) : 1f);

        /// <summary>True when a drop paid the FULL recharge: the one case that leaves a wormhole pair.</summary>
        public static bool PaysFullCooldown(float distance, float fullDistance) => distance >= fullDistance;

        /// <summary>
        /// Where to point the NOSE so a PURE PITCH UP brings the BELLY onto <paramref name="toTarget"/>: the hull's up, made
        /// square to the target. A pitch of 90 degrees carries the old forward onto the new down, so a hull whose nose is
        /// on the target and then pitches to this point has its belly on the target, off by exactly the nose's error at
        /// the start. Captured ONCE at that start and held: a point re-derived every frame drifts out of the pitch plane,
        /// the autopilot answers with yaw, and its yaw drags roll (AIPilot banks into a yaw), which leaves the belly tens
        /// of degrees off (measured offline, 2026-10-29). Waystation's AI.
        /// </summary>
        public static Vector3 BellyAimNose(Quaternion hull, Vector3 toTarget)
        {
            Vector3 up = hull * Vector3.up;
            if (toTarget.sqrMagnitude < 1e-6f) return up;
            Vector3 d = toTarget.normalized;
            Vector3 square = up - Vector3.Dot(up, d) * d;
            return square.sqrMagnitude > 1e-6f ? square.normalized : hull * Vector3.forward;
        }

        /// <summary>
        /// Seconds to hold the drop so it covers <paramref name="distance"/> along its line, landing included: the ease in
        /// covers half its time at full speed, and the landing covers its time at the mean of the fall and the cruise
        /// (both SmoothStep, whose mean is a half). Exact for a hold at least the ease long; never negative.
        /// </summary>
        public static float HoldSecondsFor(float distance, float dropSpeed, float easeSeconds, float landSeconds, float cruiseSpeed)
        {
            if (dropSpeed <= 0f) return 0f;
            float landing = landSeconds * (dropSpeed + cruiseSpeed) * 0.5f;
            return Mathf.Max(0f, (distance - landing) / dropSpeed + easeSeconds * 0.5f);
        }

        /// <summary>Degrees between the hull's belly and <paramref name="toTarget"/>: 0 = a drop would fall straight at it.</summary>
        public static float BellyAimError(Quaternion hull, Vector3 toTarget) => Vector3.Angle(hull * Vector3.down, toTarget);

        /// <summary>
        /// The camera's follow frame: the hull's live rotation slerped toward the drop frame by
        /// <paramref name="weight01"/>. While the hull holds still that is the hull pitched by the weight; while it
        /// turns into the drop frame at the landing, the result stays ON the drop frame at full weight, which is
        /// why the landing moves no camera.
        /// </summary>
        public static Quaternion CameraFrame(Quaternion hullNow, Quaternion dropFrame, float weight01) =>
            Quaternion.Slerp(hullNow, dropFrame, Mathf.Clamp01(weight01));
    }
}
