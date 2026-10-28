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
        /// The camera's follow frame: the hull's live rotation slerped toward the drop frame by
        /// <paramref name="weight01"/>. While the hull holds still that is the hull pitched by the weight; while it
        /// turns into the drop frame at the landing, the result stays ON the drop frame at full weight, which is
        /// why the landing moves no camera.
        /// </summary>
        public static Quaternion CameraFrame(Quaternion hullNow, Quaternion dropFrame, float weight01) =>
            Quaternion.Slerp(hullNow, dropFrame, Mathf.Clamp01(weight01));
    }
}
