using UnityEngine;

namespace CosmicShore.Editor.AI
{
    /// <summary>
    /// The Third Eye's camera math, pure: no scene, no camera, no editor state, so every pose is a
    /// function of its inputs and <c>ThirdEyeRigTests</c> pins it. The rules are the Vessel Studio's
    /// (/vessel-studio D16 and §7): the Chase camera smooths its OFFSET from the hull, never its
    /// position, so a 5x boost cannot leave the camera behind.
    /// </summary>
    public static class ThirdEyeRig
    {
        public struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;

            public Pose(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }
        }

        /// <summary>Frame-rate independent catch-up fraction for an exponential follow. 0 = rigid.</summary>
        public static float Damp(float ratePerSecond, float dt) =>
            ratePerSecond <= 0f ? 1f : 1f - Mathf.Exp(-ratePerSecond * Mathf.Max(0f, dt));

        /// <summary>
        /// A look rotation that never warns: a zero direction keeps <paramref name="fallback"/>, and an up
        /// parallel to the direction is swapped for one that is not.
        /// </summary>
        public static Quaternion LookRotationSafe(Vector3 direction, Vector3 up, Quaternion fallback)
        {
            if (direction.sqrMagnitude < 1e-8f) return fallback;
            if (up.sqrMagnitude < 1e-8f) up = Vector3.up;
            if (Vector3.Cross(direction, up).sqrMagnitude < 1e-8f)
                up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.forward)) < 0.9f ? Vector3.forward : Vector3.right;
            return Quaternion.LookRotation(direction, up);
        }

        /// <summary>
        /// Close behind the hull, in the hull's own frame, rolling with it. <paramref name="offset"/> and
        /// <paramref name="up"/> carry the smoothing between frames; <paramref name="snap"/> jumps them to
        /// the target (a new pilot, a new camera) instead of swooping.
        /// </summary>
        public static Pose Chase(Vector3 hullPosition, Quaternion hullRotation, ref Vector3 offset, ref Vector3 up,
                                 float distance, float height, float lookAhead, float smoothing, float dt, bool snap)
        {
            Vector3 desiredOffset = hullRotation * new Vector3(0f, height, -distance);
            Vector3 desiredUp = hullRotation * Vector3.up;
            float k = snap ? 1f : Damp(smoothing, dt);
            offset = Vector3.Lerp(offset, desiredOffset, k);
            up = Vector3.Slerp(up.sqrMagnitude > 1e-8f ? up : desiredUp, desiredUp, k);

            Vector3 position = hullPosition + offset;
            Vector3 look = hullPosition + hullRotation * Vector3.forward * lookAhead;
            return new Pose(position, LookRotationSafe(look - position, up, hullRotation));
        }

        /// <summary>
        /// Wider and world-up: behind the hull's heading flattened onto the horizontal, looking at the
        /// hull. A hull flying straight up or down keeps the last horizontal heading in
        /// <paramref name="heading"/>, so the camera does not spin.
        /// </summary>
        public static Pose Follow(Vector3 hullPosition, Quaternion hullRotation, ref Vector3 offset, ref Vector3 heading,
                                  float distance, float height, float smoothing, float dt, bool snap)
        {
            Vector3 flat = Vector3.ProjectOnPlane(hullRotation * Vector3.forward, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f) heading = flat.normalized;
            else if (heading.sqrMagnitude < 1e-8f) heading = Vector3.forward;

            Vector3 desiredOffset = -heading * distance + Vector3.up * height;
            offset = Vector3.Lerp(offset, desiredOffset, snap ? 1f : Damp(smoothing, dt));

            Vector3 position = hullPosition + offset;
            return new Pose(position, LookRotationSafe(hullPosition - position, Vector3.up, Quaternion.identity));
        }

        /// <summary>Detached flight: yaw / pitch in degrees, <paramref name="move"/> in camera space (x right, y up, z forward).</summary>
        public static Pose FreeFly(Vector3 position, float yawDegrees, float pitchDegrees, Vector3 move, float speed, float dt)
        {
            Quaternion rotation = Quaternion.Euler(Mathf.Clamp(pitchDegrees, -89f, 89f), yawDegrees, 0f);
            Vector3 step = move.sqrMagnitude > 1f ? move.normalized : move;
            return new Pose(position + rotation * step * (speed * Mathf.Max(0f, dt)), rotation);
        }

        /// <summary>Round the hull at <paramref name="distance"/>, looking at it.</summary>
        public static Pose Orbit(Vector3 hullPosition, float yawDegrees, float pitchDegrees, float distance)
        {
            Quaternion rotation = Quaternion.Euler(Mathf.Clamp(pitchDegrees, -89f, 89f), yawDegrees, 0f);
            return new Pose(hullPosition - rotation * Vector3.forward * distance, rotation);
        }

        /// <summary>A rotation's yaw and pitch in degrees, pitch in [-180, 180).</summary>
        public static void YawPitch(Quaternion rotation, out float yawDegrees, out float pitchDegrees)
        {
            Vector3 e = rotation.eulerAngles;
            yawDegrees = e.y;
            pitchDegrees = Mathf.Repeat(e.x + 180f, 360f) - 180f;
        }

        /// <summary>
        /// Clip a segment to the part in front of the camera. <paramref name="depthA"/> and
        /// <paramref name="depthB"/> are each end's distance in front of the camera (a viewport point's z).
        /// False when the whole segment is behind <paramref name="near"/>.
        /// </summary>
        public static bool ClipToFront(Vector3 a, float depthA, Vector3 b, float depthB, float near,
                                       out Vector3 clippedA, out Vector3 clippedB)
        {
            clippedA = a;
            clippedB = b;
            bool aIn = depthA >= near, bIn = depthB >= near;
            if (aIn && bIn) return true;
            if (!aIn && !bIn) return false;

            float t = (near - depthA) / (depthB - depthA);
            Vector3 cut = Vector3.Lerp(a, b, t);
            if (aIn) clippedB = cut;
            else clippedA = cut;
            return true;
        }

        /// <summary>
        /// Where to pin a marker for a point that may be off screen, in viewport space (0..1, y up).
        /// A point behind the camera is mirrored first, so the marker sits on the side you would turn to.
        /// </summary>
        public static Vector2 EdgePoint(Vector3 viewport, float inset)
        {
            Vector2 p = new(viewport.x, viewport.y);
            if (viewport.z < 0f) p = Vector2.one - p;

            Vector2 c = new(0.5f, 0.5f);
            Vector2 d = p - c;
            float half = 0.5f - inset;
            float m = Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
            if (viewport.z >= 0f && m <= half) return p;
            if (m < 1e-6f) return new Vector2(0.5f, inset);
            return c + d * (half / m);
        }
    }
}
