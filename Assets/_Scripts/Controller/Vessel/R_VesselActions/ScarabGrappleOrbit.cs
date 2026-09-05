using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One held-drift ball grapple, as the PARAMETRIC ORBIT the hull rides around the ball
    /// (SCARAB.md §4.7). Everything a peer needs to reproduce the vessel's pose at any instant is
    /// in here, and nothing that could drift between peers: the orbit is a closed-form function
    /// of time, so the SERVER (which flings the ball) and the OWNER (which writes the vessel's
    /// pose) compute the same tangent from the same six numbers and never have to exchange a
    /// per-tick position. Time is the shared network clock, stored as a double so a long match
    /// does not erode the phase.
    /// </summary>
    public struct ScarabGrappleOrbitState
    {
        /// <summary>Orbit axis (unit). The hull circles the ball right-handed about it.</summary>
        public Vector3 Axis;
        /// <summary>Radial direction from the ball's centre to the hull at <see cref="StartTime"/> (unit).</summary>
        public Vector3 Radial0;
        /// <summary>Orbit radius: ball radius + hull radius + clearance, world units.</summary>
        public float Radius;
        /// <summary>Angular speed about <see cref="Axis"/> in radians per second (≥ 0; the sign lives in the axis).</summary>
        public float AngularSpeed;
        /// <summary>Clock time the grapple began (seconds on the shared clock).</summary>
        public double StartTime;

        public bool IsValid => Radius > 0f && Axis.sqrMagnitude > 0.5f && Radial0.sqrMagnitude > 0.5f;
    }

    /// <summary>
    /// The pure geometry of the grapple: how a contact becomes an orbit, where the hull is on it
    /// at a given time, how fast it is moving relative to the ball there, and what the ball is
    /// flung with on release. No Unity object touches this — <c>ScarabGrappleOrbitTests</c> pins
    /// the contract offline.
    /// </summary>
    public static class ScarabGrappleOrbit
    {
        const float TangentEpsilon = 1e-3f;

        /// <summary>
        /// Build the orbit from the CONTACT: the hull's position and velocity relative to the ball
        /// at the moment it stuck. The radial component of the approach is absorbed (that is the
        /// "stick"); the tangential component becomes the orbit, so a glancing contact spins fast
        /// around the ball and a dead-centre one sticks with no spin — impact location and
        /// velocity are the whole input, which is what makes the move readable and masterable.
        /// The ball's own velocity is subtracted first, so grabbing a moving ball is exactly
        /// grabbing a still one in the ball's frame: the carry falls out of the parametrisation
        /// (the orbit is always about wherever the ball IS) with no extra term.
        /// </summary>
        /// <param name="fallbackUp">A reference direction used only to pick an orbit plane when
        /// the approach is purely radial (no tangential motion) or the hull sits on the ball's
        /// centre; any non-zero vector will do.</param>
        public static ScarabGrappleOrbitState FromContact(
            Vector3 ballPosition, Vector3 ballVelocity,
            Vector3 hullPosition, Vector3 hullVelocity,
            float radius, double now, Vector3 fallbackUp)
        {
            Vector3 radial = hullPosition - ballPosition;
            if (radial.sqrMagnitude < 1e-6f)
                radial = fallbackUp.sqrMagnitude > 1e-6f ? fallbackUp : Vector3.up;
            radial.Normalize();

            Vector3 relative = hullVelocity - ballVelocity;
            Vector3 tangential = Vector3.ProjectOnPlane(relative, radial);
            float tangentSpeed = tangential.magnitude;

            Vector3 axis;
            float angularSpeed;
            if (tangentSpeed > TangentEpsilon && radius > 1e-4f)
            {
                axis = Vector3.Cross(radial, tangential / tangentSpeed).normalized;
                angularSpeed = tangentSpeed / radius;
            }
            else
            {
                axis = AnyPerpendicular(radial, fallbackUp);
                angularSpeed = 0f;
            }

            return new ScarabGrappleOrbitState
            {
                Axis = axis,
                Radial0 = radial,
                Radius = Mathf.Max(radius, 1e-4f),
                AngularSpeed = angularSpeed,
                StartTime = now,
            };
        }

        /// <summary>Unit direction from the ball's centre to the hull at <paramref name="now"/>.</summary>
        public static Vector3 RadialAt(in ScarabGrappleOrbitState s, double now)
        {
            float degrees = (float)((now - s.StartTime) * s.AngularSpeed) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(degrees, s.Axis) * s.Radial0;
        }

        /// <summary>The hull's world position at <paramref name="now"/> for a ball at
        /// <paramref name="ballPosition"/>.</summary>
        public static Vector3 PositionAt(in ScarabGrappleOrbitState s, double now, Vector3 ballPosition)
            => ballPosition + RadialAt(s, now) * s.Radius;

        /// <summary>The hull's velocity RELATIVE to the ball at <paramref name="now"/> — the orbit
        /// tangent scaled by the orbital speed. Zero for a spinless hold.</summary>
        public static Vector3 RelativeVelocityAt(in ScarabGrappleOrbitState s, double now)
        {
            if (s.AngularSpeed <= 0f) return Vector3.zero;
            Vector3 radial = RadialAt(s, now);
            return Vector3.Cross(s.Axis, radial) * (s.AngularSpeed * s.Radius);
        }

        /// <summary>The orbital speed — a constant of the orbit.</summary>
        public static float OrbitalSpeed(in ScarabGrappleOrbitState s) => s.AngularSpeed * s.Radius;

        /// <summary>
        /// What the ball is FLUNG with on release: the hull's orbital velocity at that instant,
        /// scaled by <paramref name="multiplier"/>. The ball leaves the way the hull was swinging,
        /// and faster than the hull, so the two separate cleanly instead of re-colliding — the
        /// release moment picks the direction, the entry contact picked the speed. Zero for a
        /// spinless hold: a ball grabbed dead-centre is carried, not thrown.
        /// </summary>
        public static Vector3 FlingVelocity(in ScarabGrappleOrbitState s, double now, float multiplier)
            => RelativeVelocityAt(s, now) * multiplier;

        /// <summary>The ball's spin while held: the orbit's angular velocity vector, so the ball
        /// rolls with the hull circling it (cosmetic — the ball's angular velocity carries no
        /// gameplay, but a held ball that did not turn would read as glued to nothing).</summary>
        public static Vector3 BallSpin(in ScarabGrappleOrbitState s, float fraction)
            => s.Axis * (s.AngularSpeed * fraction);

        /// <summary>
        /// The hull's orientation on the orbit: nose along the relative motion, belly to the ball
        /// (up = radial outward). A spinless hold has no tangent to face along, so the caller's
        /// fallback forward is projected onto the tangent plane instead — the hull keeps the
        /// heading it arrived with, rolled belly-down onto the ball.
        /// </summary>
        public static Quaternion PoseRotation(Vector3 radial, Vector3 relativeVelocity, Vector3 fallbackForward)
        {
            Vector3 forward = relativeVelocity;
            if (forward.sqrMagnitude < TangentEpsilon * TangentEpsilon)
                forward = Vector3.ProjectOnPlane(fallbackForward, radial);
            if (forward.sqrMagnitude < 1e-6f)
                forward = AnyPerpendicular(radial, fallbackForward);
            return Quaternion.LookRotation(forward.normalized, radial);
        }

        static Vector3 AnyPerpendicular(Vector3 unit, Vector3 hint)
        {
            Vector3 candidate = Vector3.Cross(unit, hint);
            if (candidate.sqrMagnitude < 1e-6f) candidate = Vector3.Cross(unit, Vector3.up);
            if (candidate.sqrMagnitude < 1e-6f) candidate = Vector3.Cross(unit, Vector3.right);
            return candidate.normalized;
        }
    }
}
