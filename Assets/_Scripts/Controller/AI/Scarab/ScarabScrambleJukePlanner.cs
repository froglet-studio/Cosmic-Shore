using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The geometry behind Scarab Scramble's AI jukes — pure, so it is unit-tested
    /// (<c>ScarabScrambleJukePlannerTests</c>). Everything is in world units and seconds and
    /// assumes both bodies hold their velocity over the half-second window, which is what a
    /// committed dash is.
    /// </summary>
    public static class ScarabScrambleJukePlanner
    {
        /// <summary>
        /// How far SIDEWAYS a committed dash has carried the hull <paramref name="t"/> seconds
        /// after it fired. The dash is a <c>ShipVelocityModifier</c> whose speed follows
        /// <c>v · (cos(π s / T) / 2 + 1)</c> over its duration T (VesselTransformer
        /// .ApplyVelocityModifiers); this is that curve integrated: <c>v · (t + T/(2π) · sin(π t / T))</c>,
        /// reaching exactly <c>v · T</c> at the end. The transformer's 100 u/s channel clamp
        /// shaves the first ~0.1 s of a shipped (80 u/s) dash by a few units; the contact
        /// radius the callers pass absorbs it.
        /// </summary>
        public static float DashDisplacement(float t, float dashSpeed, float duration)
        {
            if (t <= 0f || dashSpeed <= 0f || duration <= 0f) return 0f;
            t = Mathf.Min(t, duration);
            return dashSpeed * (t + duration / (2f * Mathf.PI) * Mathf.Sin(Mathf.PI * t / duration));
        }

        /// <summary>
        /// Closest approach of two bodies moving at constant velocity, given the target's
        /// position and velocity RELATIVE to the self. Returns the time (never negative: a pair
        /// already separating is closest now) and the offset from self to target at that time.
        /// </summary>
        public static float ClosestApproach(Vector3 relPos, Vector3 relVel, out Vector3 offsetAtClosest)
        {
            float vv = relVel.sqrMagnitude;
            float t = vv > 1e-6f ? Mathf.Max(0f, -Vector3.Dot(relPos, relVel) / vv) : 0f;
            offsetAtClosest = relPos + relVel * t;
            return t;
        }

        /// <summary>
        /// Should a hull at <paramref name="selfPos"/> moving at <paramref name="selfVel"/> fire a
        /// committed dash NOW to strike a ball inside the steal window? True when the closest
        /// approach falls between <paramref name="minLeadSeconds"/> and
        /// <paramref name="windowSeconds"/>, and the dash's own sideways travel by then lands the
        /// hull within <paramref name="contactRadius"/> of the ball. That is the reading of "inside
        /// the juke-steal window" that matters: the window is only open for the dash's duration, so
        /// a dash that arrives after it closes is a bump, and one that overshoots is a miss.
        /// <paramref name="shove"/> is the offset at closest approach — the side the ball is on;
        /// zero for a dead-centre pass, which the juke resolves to the hull's right.
        /// </summary>
        public static bool ShouldStealDash(Vector3 selfPos, Vector3 selfVel, Vector3 ballPos,
                                           Vector3 ballVel, float windowSeconds, float minLeadSeconds,
                                           float dashSpeed, float contactRadius, out Vector3 shove)
        {
            float t = ClosestApproach(ballPos - selfPos, ballVel - selfVel, out Vector3 offset);
            shove = offset.sqrMagnitude > 1e-4f ? offset : Vector3.zero;
            if (t < minLeadSeconds || t > windowSeconds) return false;
            float reach = DashDisplacement(t, dashSpeed, windowSeconds);
            return Mathf.Abs(offset.magnitude - reach) <= contactRadius;
        }

        /// <summary>
        /// Should an escorting hull dash sideways toward its escort point? Only on a LONG
        /// STRAIGHT: the point is at least <paramref name="minDistance"/> ahead, within
        /// <paramref name="maxAngleDegrees"/> of the course, its sideways offset is at least
        /// <paramref name="minLateral"/> (pass one full dash, so the dash never overshoots the
        /// line it is correcting), and the escorted ball is at least
        /// <paramref name="ballClearance"/> away so the plate riding the dash cannot reach it.
        /// <paramref name="shove"/> is that sideways offset.
        /// </summary>
        public static bool ShouldEscortDash(Vector3 selfPos, Vector3 course, Vector3 aimPoint,
                                            Vector3 ballPos, float minDistance, float maxAngleDegrees,
                                            float minLateral, float ballClearance, out Vector3 shove)
        {
            shove = Vector3.zero;
            if (course.sqrMagnitude < 1e-6f) return false;
            course = course.normalized;

            Vector3 toAim = aimPoint - selfPos;
            if (toAim.sqrMagnitude < minDistance * minDistance) return false;
            if ((ballPos - selfPos).sqrMagnitude < ballClearance * ballClearance) return false;

            float along = Vector3.Dot(toAim, course);
            if (along <= 0f || Vector3.Angle(course, toAim) > maxAngleDegrees) return false;

            Vector3 lateral = toAim - course * along;
            if (lateral.magnitude < minLateral) return false;

            shove = lateral;
            return true;
        }
    }
}
