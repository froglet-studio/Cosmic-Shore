using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>Where a capture camera goes and what it looks at. One solve, every concept.</summary>
    public readonly struct ScreenshotShot
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float FieldOfView;
        public readonly float Distance;

        public ScreenshotShot(Vector3 position, Quaternion rotation, float fieldOfView, float distance)
        {
            Position = position;
            Rotation = rotation;
            FieldOfView = fieldOfView;
            Distance = distance;
        }
    }

    /// <summary>
    /// The camera-placement math for <see cref="ScreenshotDirector"/>, kept PURE and separate from
    /// it on purpose: a shot is a function of (concept, subject pose, subject velocity, a random
    /// stream), so it can be unit-tested in edit mode over thousands of rolls without a camera, a
    /// scene, or play mode — which is the only way to know that no concept in the library can aim a
    /// camera at the inside of the ship or produce a NaN rotation.
    /// </summary>
    public static class ScreenshotFraming
    {
        /// <summary>
        /// Closest a capture camera may sit to the subject's ORIGIN. The real floor is applied by
        /// the director, which knows the hull's measured radius; this is the floor that holds even
        /// when nothing knows how big the ship is.
        /// </summary>
        public const float MinimumDistance = 1.5f;

        /// <summary>
        /// How far ahead of the subject the aim may be thrown, as a fraction of the camera's
        /// distance from it. At 0.4 the subject sits at most ~22° off the optical axis — near the
        /// edge of frame on a long lens, which is the composition, and never outside it.
        /// </summary>
        public const float MaxLeadFraction = 0.4f;

        /// <summary>
        /// Solves one shot. <paramref name="rng"/> is the only source of variation, so passing a
        /// seeded stream reproduces a photograph exactly.
        /// </summary>
        /// <param name="subjectPosition">What the shot is of.</param>
        /// <param name="subjectForward">The hull's facing — the fallback basis when it is not moving.</param>
        /// <param name="course">Direction of travel. May be zero; the hull's facing stands in.</param>
        /// <param name="speed">World units per second, for the aim lead.</param>
        /// <param name="minimumDistance">Hull-aware floor; the camera is never placed inside the ship.</param>
        public static ScreenshotShot Solve(
            ScreenshotConcept concept,
            Vector3 subjectPosition,
            Vector3 subjectForward,
            Vector3 course,
            float speed,
            System.Random rng,
            float minimumDistance = MinimumDistance)
        {
            // ── the basis ────────────────────────────────────────────────────
            // A vessel pointing straight up has no heading to measure azimuth from, so every
            // candidate basis is flattened and the next one is tried: course, then facing, then
            // world north. Without this a vertical climb collapses the basis to zero and the whole
            // solve degenerates — LookRotation of a zero vector is an identity rotation pointing at
            // nothing, which renders as a shot of empty space rather than as an error.
            Vector3 heading = Flatten(course);
            if (heading == Vector3.zero) heading = Flatten(subjectForward);
            if (heading == Vector3.zero) heading = Vector3.forward;

            Vector3 basis = concept.worldAligned ? Vector3.forward : heading;

            // ── the offset ───────────────────────────────────────────────────
            float azimuth = Sample(concept.azimuthDegrees, rng);
            float elevation = Mathf.Clamp(Sample(concept.elevationDegrees, rng), -89f, 89f);
            float distance = Mathf.Max(Sample(concept.distance, rng), Mathf.Max(minimumDistance, MinimumDistance));

            Vector3 direction = Quaternion.AngleAxis(azimuth, Vector3.up) * basis;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            // Degenerate only if `direction` is vertical, which it cannot be: it was built by
            // yawing a flattened basis about world up, so it stays in the horizontal plane.
            if (right.sqrMagnitude > 1e-6f)
                direction = Quaternion.AngleAxis(-elevation, right.normalized) * direction;

            Vector3 position = subjectPosition + direction.normalized * distance;

            // ── the aim ──────────────────────────────────────────────────────
            // Lead room: aiming ahead of a moving subject pushes it toward the back of frame and
            // leaves the space it is flying into, which is what makes a still photograph of a
            // moving object read as moving.
            //
            // The lead is CLAMPED to a fraction of the shot distance, and that clamp is
            // load-bearing rather than defensive: a lead expressed in seconds of travel is a
            // WORLD distance, so the same 0.35s that frames a 110u static tracking shot beautifully
            // is 105u of lead on a vessel doing 300 u/s — from the 8u Low Chase camera, which puts
            // the ship completely outside the frame and photographs empty space. Lead room is a
            // property of the FRAME, so it has to be measured in frames.
            Vector3 travel = course.sqrMagnitude < 1e-6f ? Vector3.zero : course.normalized;
            float lead = Mathf.Min(speed * Sample(concept.aimLeadSeconds, rng), distance * MaxLeadFraction);
            Vector3 aimPoint = subjectPosition + travel * lead;

            Vector3 toSubject = aimPoint - position;
            if (toSubject.sqrMagnitude < 1e-6f) toSubject = -direction; // never LookRotation a zero vector

            Quaternion rotation = Quaternion.LookRotation(toSubject.normalized, Vector3.up);
            rotation *= Quaternion.Euler(
                Sample(concept.framingPitchDegrees, rng),
                0f,
                Sample(concept.rollDegrees, rng));

            float fov = Mathf.Clamp(Sample(concept.fieldOfView, rng), 5f, 170f);

            return new ScreenshotShot(position, rotation, fov, distance);
        }

        /// <summary>
        /// A value inside a range expressed as a <see cref="Vector2"/>. Order-insensitive, because
        /// an inspector range typed as (210, 150) is a range, not a mistake worth punishing.
        /// </summary>
        public static float Sample(Vector2 range, System.Random rng)
        {
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            return Mathf.Approximately(min, max) ? min : Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        /// <summary>The vector with its vertical component removed, or zero if nothing survives.</summary>
        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.zero : v.normalized;
        }
    }
}
