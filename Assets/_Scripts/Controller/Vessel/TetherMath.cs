using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Tether's physics as PURE FUNCTIONS of state: no Unity objects, no
    /// <c>Time.deltaTime</c>, no side effects. Everything the vessel does to its own momentum is
    /// answered here, so <c>TetherMathTests</c> (and <c>Tools/Build/tether_harness</c>, which runs
    /// those tests offline against this exact file) can fly it without an editor.
    ///
    /// Two kinds of line share it:
    /// <list type="bullet">
    /// <item><b>Auto-tethers</b> — short ELASTIC lines to anchors the vessel plants for itself,
    /// alternating left and right of the nose. A spring that pulls only past its rest length,
    /// with damping that resists stretching (never closing). Released when the hull passes
    /// abeam of the anchor.</item>
    /// <item><b>The long tether</b> — one RIGID line to a distant prism, on a held trigger. A
    /// max-distance constraint: speed is kept on hook (redirected onto the tangent), reeling
    /// conserves angular momentum, and a release past a half turn pays a boost.</item>
    /// </list>
    ///
    /// <b>The search plane.</b> Both kinds only look for anchors near the plane spanned by the
    /// hull's FORWARD and RIGHT axes (normal = the hull's up). Rolling the hull tilts that plane,
    /// which is how a pilot chooses where the lines go. Every function that needs it takes the
    /// three axes explicitly rather than a rotation, so this file needs no Quaternion.
    /// </summary>
    public static class TetherMath
    {
        /// <summary>Below this a length, speed or direction is numerically meaningless.</summary>
        public const float Epsilon = 1e-4f;

        /// <summary>The swing a release must clear to earn its boost — a half turn.</summary>
        public const float HalfTurnRadians = Mathf.PI;

        const float TwoPi = Mathf.PI * 2f;

        // ------------------------------------------------------------------ search plane

        /// <summary>
        /// Angle, in degrees, between <paramref name="toTarget"/> and the search plane (the plane
        /// through the hull's forward and right axes, whose normal is <paramref name="up"/>).
        /// Unsigned: a target 10° above the plane and one 10° below it are equally far off it.
        /// </summary>
        public static float PlaneElevationDegrees(Vector3 toTarget, Vector3 up)
        {
            float len = toTarget.magnitude;
            if (len < Epsilon) return 0f;
            float s = Mathf.Clamp(Mathf.Abs(Vector3.Dot(toTarget / len, up)), 0f, 1f);
            return Mathf.Asin(s) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// How far the hook window has moved outward at <paramref name="speed"/>:
        /// <c>1 + growth × max(0, speed/cruise − 1)</c>. At cruise and below it is exactly 1.
        ///
        /// It exists so arcs stay READABLE as speed rises. The spin of a swing is <c>v / r</c>, so
        /// a window fixed in distance would turn every fast hook into a blur; moving the window
        /// out with speed keeps the swing rate near what the pilot learned at cruise.
        /// </summary>
        public static float WindowScale(float speed, float cruise, float growth)
        {
            if (cruise < Epsilon) return 1f;
            return 1f + Mathf.Max(0f, growth) * Mathf.Max(0f, speed / cruise - 1f);
        }

        /// <summary>The long tether's hook window and how candidates in it are ranked.</summary>
        public struct HookWindow
        {
            /// <summary>Closer than this is skipped (already scaled by <see cref="WindowScale"/>).</summary>
            public float MinDistance;
            /// <summary>Further than this is out of reach (already scaled).</summary>
            public float MaxDistance;
            /// <summary>How far off the search plane a candidate may sit, degrees.</summary>
            public float PlaneToleranceDegrees;
            /// <summary>Where in [min, max] the distance score peaks, 0..1.</summary>
            public float IdealDistance01;
            /// <summary>Weight on being out to the side (1 = abeam).</summary>
            public float SideWeight;
            /// <summary>Weight on being ahead (1 = dead ahead).</summary>
            public float AheadWeight;
            /// <summary>Weight on sitting near the ideal distance.</summary>
            public float DistanceWeight;
            /// <summary>Weight on sitting close to the search plane.</summary>
            public float PlaneWeight;
        }

        /// <summary>
        /// Score a hook candidate on one side of the search plane. False (and no score) when it
        /// is ineligible: out of the distance window, BEHIND the hull, on the other side, or too
        /// far off the plane. Higher is better; eligible scores are comparable across candidates
        /// on the same frame only.
        /// </summary>
        /// <param name="side">+1 for the right trigger, −1 for the left.</param>
        public static bool TryScoreHook(
            Vector3 hull, Vector3 forward, Vector3 right, Vector3 up, int side,
            Vector3 candidate, in HookWindow w, out float score)
        {
            score = 0f;
            Vector3 d = candidate - hull;
            float dist = d.magnitude;
            if (dist < w.MinDistance || dist > w.MaxDistance || dist < Epsilon) return false;

            Vector3 dir = d / dist;
            float ahead = Vector3.Dot(dir, forward);
            if (ahead < 0f) return false;                                  // behind you

            float lateral = Vector3.Dot(dir, right) * (side >= 0 ? 1f : -1f);
            if (lateral <= 0f) return false;                               // the other trigger's side

            float elevation = PlaneElevationDegrees(d, up);
            if (elevation > w.PlaneToleranceDegrees) return false;         // off the search plane

            float span = Mathf.Max(Epsilon, w.MaxDistance - w.MinDistance);
            float t = Mathf.Clamp01((dist - w.MinDistance) / span);
            float ideal = Mathf.Clamp01(w.IdealDistance01);
            float distanceScore = 1f - Mathf.Abs(t - ideal) / Mathf.Max(Mathf.Max(ideal, 1f - ideal), Epsilon);
            float planeScore = w.PlaneToleranceDegrees > Epsilon
                ? 1f - elevation / w.PlaneToleranceDegrees
                : 1f;

            score = w.SideWeight * lateral
                  + w.AheadWeight * ahead
                  + w.DistanceWeight * distanceScore
                  + w.PlaneWeight * planeScore;
            return true;
        }

        // ------------------------------------------------------------------ long tether

        /// <summary>
        /// The velocity a hull flies away with the instant a rigid line goes taut: the SAME
        /// speed, redirected onto the tangent of the circle about <paramref name="anchor"/>.
        /// Hooking never costs speed — that is the rule, and <c>TetherMathTests</c> holds it.
        ///
        /// When the hull is flying straight at (or away from) the anchor there is no tangent to
        /// keep; <paramref name="fallbackForward"/> projected off the line chooses one.
        /// </summary>
        public static Vector3 HookVelocity(Vector3 hull, Vector3 velocity, Vector3 anchor, Vector3 fallbackForward)
        {
            float speed = velocity.magnitude;
            Vector3 r = hull - anchor;
            float len = r.magnitude;
            if (len < Epsilon || speed < Epsilon) return velocity;

            Vector3 radial = r / len;
            Vector3 tangent = velocity - radial * Vector3.Dot(velocity, radial);
            if (tangent.sqrMagnitude < Epsilon * Epsilon)
            {
                tangent = fallbackForward - radial * Vector3.Dot(fallbackForward, radial);
                if (tangent.sqrMagnitude < Epsilon * Epsilon) return velocity;
            }
            return tangent.normalized * speed;
        }

        /// <summary>
        /// How hard a line yanks when it goes taut, 0..1: the share of the hull's velocity that
        /// pointed ALONG the line and is about to be turned onto the tangent. A hook taken dead
        /// abeam is 0 (nothing to redirect); one taken flying straight at the anchor is 1. Drives
        /// the snap's shake, haptic and beam flash, so the feedback is as big as the redirect.
        /// </summary>
        public static float HookYank01(Vector3 hull, Vector3 velocity, Vector3 anchor)
        {
            Vector3 r = hull - anchor;
            float len = r.magnitude, speed = velocity.magnitude;
            if (len < Epsilon || speed < Epsilon) return 0f;
            return Mathf.Clamp01(Mathf.Abs(Vector3.Dot(velocity / speed, r / len)));
        }

        /// <summary>Orbital rate about the anchor, radians per second.</summary>
        public static float SpinRate(float tangentialSpeed, float length)
            => length > Epsilon ? tangentialSpeed / length : 0f;

        /// <summary>
        /// The shortest line the spin limit allows: angular momentum <c>h = v·L</c> is conserved
        /// while reeling, and the spin at <c>L'</c> is <c>h / L'²</c>, so the reel must stop at
        /// <c>L' = √(v·L / ω_max)</c>. This is why one swing pays a bounded multiple of the hook
        /// speed (<c>√(L·ω_max / v)</c>) rather than reeling straight to the speed cap.
        /// </summary>
        public static float SpinLimitedLength(float length, float speed, float maxSpinRadPerSec)
            => maxSpinRadPerSec > Epsilon ? Mathf.Sqrt(Mathf.Max(0f, speed * length) / maxSpinRadPerSec) : 0f;

        /// <summary>
        /// One frame of winch on the long tether's length.
        ///
        /// <paramref name="reelInput"/> is the pilot's pitch command, −1..1: positive reels in at
        /// <paramref name="reelRate"/> on top of the constant <paramref name="autoReel"/>, negative
        /// lets rope out at <paramref name="reelRate"/> (less the auto reel). A shortening line
        /// stops at the highest of <paramref name="minLength"/>, the spin limit
        /// (<see cref="SpinLimitedLength"/>) and the speed cap; a lengthening one at
        /// <paramref name="maxLength"/>.
        ///
        /// <b>A floor STOPS a reel, it never pays a line out.</b> The spin floor rises with speed,
        /// which the reel itself raises, so the obvious <c>Max(floor, L − step)</c> lengthens a line
        /// sitting below the floor of the moment and chatters between reel and pay-out at a rate
        /// that changes with the frame rate (the Gibbon prototype hit exactly this). A line may sit
        /// below the current floor; it got there legitimately.
        /// </summary>
        public static float ReelLength(
            float length, float speed, float reelInput, float reelRate, float autoReel,
            float minLength, float maxLength, float maxSpinRadPerSec, float maxSpeed, float dt)
        {
            float input = Mathf.Clamp(reelInput, -1f, 1f);
            float ratePerSecond = input >= 0f
                ? -(Mathf.Max(0f, autoReel) + input * Mathf.Max(0f, reelRate))
                : -input * Mathf.Max(0f, reelRate) - Mathf.Max(0f, autoReel);
            float next = length + ratePerSecond * dt;

            if (next < length)
            {
                float floor = Mathf.Max(minLength, SpinLimitedLength(length, speed, maxSpinRadPerSec));
                if (maxSpeed > Epsilon) floor = Mathf.Max(floor, speed * length / maxSpeed);
                next = Mathf.Max(next, Mathf.Min(length, floor));
            }
            else if (next > length)
            {
                next = Mathf.Min(next, Mathf.Max(length, maxLength));
            }
            return next;
        }

        /// <summary>
        /// Tangential speed after the line changes length, with angular momentum <c>v·L</c>
        /// conserved — reeling in spins you up, letting out slows you. No pump formula: the
        /// rope's force is radial, so it exerts no torque about the anchor.
        /// </summary>
        public static float ConserveAngularMomentum(float speed, float oldLength, float newLength)
            => newLength > Epsilon ? speed * oldLength / newLength : speed;

        /// <summary>
        /// The velocity to fly THIS frame on a taut rigid line: the hull's speed, kept exactly,
        /// along the tangent rotated toward the anchor by HALF the arc it sweeps this frame. A
        /// straight step along that chord lands on the circle, so the swing is integrated by the
        /// flight model's ordinary <c>position += velocity·dt</c> with no radial drift worth the
        /// name; <see cref="ConstrainToRope"/> removes the residue afterwards.
        ///
        /// Any radial component in <paramref name="velocity"/> is discarded: outward is what the
        /// rope forbids (the max-distance constraint removing outward radial velocity), and inward
        /// is the reel's job, which <see cref="ConstrainToRope"/> carries out on position.
        /// </summary>
        public static Vector3 ArcVelocity(Vector3 hull, Vector3 velocity, Vector3 anchor, float length, float dt)
        {
            float speed = velocity.magnitude;
            Vector3 r = hull - anchor;
            float len = r.magnitude;
            if (len < Epsilon || speed < Epsilon) return velocity;

            Vector3 radial = r / len;
            Vector3 tangent = velocity - radial * Vector3.Dot(velocity, radial);
            if (tangent.sqrMagnitude < Epsilon * Epsilon) return velocity;
            tangent = tangent.normalized;

            float half = 0.5f * speed * dt / Mathf.Max(length, Epsilon);
            return (tangent * Mathf.Cos(half) - radial * Mathf.Sin(half)) * speed;
        }

        /// <summary>The hull's position put back on the rope: <paramref name="length"/> from the
        /// anchor along the current line. Rigid both ways while hooked — outward is the
        /// constraint, inward is the reel.</summary>
        public static Vector3 ConstrainToRope(Vector3 hull, Vector3 anchor, float length)
        {
            Vector3 r = hull - anchor;
            float len = r.magnitude;
            return len < Epsilon ? hull : anchor + r * (length / len);
        }

        /// <summary>Unsigned angle between two lines from the anchor, radians.</summary>
        public static float SweptAngle(Vector3 fromRelative, Vector3 toRelative)
        {
            float cross = Vector3.Cross(fromRelative, toRelative).magnitude;
            float dot = Vector3.Dot(fromRelative, toRelative);
            return Mathf.Atan2(cross, dot);   // atan2(0, 0) is 0: a degenerate line sweeps nothing
        }

        /// <summary>Swing progress toward the half turn, 0..1 (the swept-arc indicator).</summary>
        public static float HalfTurnProgress01(float sweptRadians) => Mathf.Clamp01(sweptRadians / HalfTurnRadians);

        /// <summary>Spin in revolutions per second — the unit <c>maxSpin</c> is authored in.</summary>
        public static float RevolutionsPerSecond(float radiansPerSecond) => radiansPerSecond / TwoPi;

        /// <summary>
        /// Exit speed when the long tether is let go. Below a half turn the speed is simply kept;
        /// past it, a boost of <paramref name="releaseBoost"/> that SHRINKS as the speed nears the
        /// cap (<c>× (1 − (v/cap)²)</c>), so chaining swings levels off instead of slamming into
        /// the ceiling. Never above <paramref name="maxSpeed"/>.
        /// </summary>
        public static float ReleaseSpeed(float speed, float sweptRadians, float releaseBoost, float maxSpeed)
        {
            float capped = maxSpeed > Epsilon ? Mathf.Min(speed, maxSpeed) : speed;
            if (sweptRadians < HalfTurnRadians || releaseBoost <= 0f) return capped;
            float headroom = maxSpeed > Epsilon ? 1f - (speed / maxSpeed) * (speed / maxSpeed) : 1f;
            float boosted = speed * (1f + releaseBoost * Mathf.Clamp01(headroom));
            return maxSpeed > Epsilon ? Mathf.Min(boosted, maxSpeed) : boosted;
        }

        /// <summary>
        /// Multiplier on the flight model's ordinary overspeed decay: <paramref name="glideFactor"/>
        /// for <paramref name="glideSeconds"/> after a release, 1 otherwise. A quick re-hook inside
        /// that window carries the speed the last swing earned.
        /// </summary>
        public static float OverspeedDecayScale(float secondsSinceRelease, float glideSeconds, float glideFactor)
            => secondsSinceRelease >= 0f && secondsSinceRelease < glideSeconds ? Mathf.Clamp01(glideFactor) : 1f;

        // ------------------------------------------------------------------ auto-tethers

        /// <summary>
        /// Where the next auto-anchor goes: <paramref name="leadSeconds"/> × speed ahead, rotated
        /// <paramref name="angleDegrees"/> off the nose WITHIN the search plane, to
        /// <paramref name="side"/> (+1 right, −1 left).
        /// </summary>
        public static Vector3 AnchorPoint(
            Vector3 hull, Vector3 forward, Vector3 right, float speed,
            float leadSeconds, float angleDegrees, int side)
        {
            float a = angleDegrees * Mathf.Deg2Rad;
            Vector3 dir = forward * Mathf.Cos(a) + right * ((side >= 0 ? 1f : -1f) * Mathf.Sin(a));
            return hull + dir * (Mathf.Max(0f, speed) * Mathf.Max(0f, leadSeconds));
        }

        /// <summary>The hull has drawn level with (or passed) the anchor along its own nose —
        /// the moment an auto-tether lets go.</summary>
        public static bool PassedAbeam(Vector3 hull, Vector3 forward, Vector3 anchor)
            => Vector3.Dot(anchor - hull, forward) <= 0f;

        /// <summary>
        /// One elastic auto-tether's velocity change this frame (already × dt).
        ///
        /// <b>Spring</b>: <c>stiffness × stretch</c> toward the anchor, ONLY past the rest length —
        /// slack is silent, which is what makes it a tether and not a rubber band.
        ///
        /// <b>Damping resists STRETCHING only.</b> The hull is flying toward an anchor planted ahead
        /// of it, so the line shortens all the time; a symmetric damper would brake that closing
        /// speed, which is the vessel's own forward flight. Damping the opening rate alone stops
        /// the spring ringing without taxing cruise. Applied as the exact solution of
        /// <c>dv/dt = −c·v</c>, so a hitch frame cannot overshoot.
        /// </summary>
        /// <param name="tension01">stretch / (initial − rest), 0..1 — the beam's brightness.</param>
        public static Vector3 ElasticPull(
            Vector3 hull, Vector3 velocity, Vector3 anchor, float rest, float span,
            float stiffness, float damping, float dt, out float tension01)
        {
            tension01 = 0f;
            Vector3 d = anchor - hull;
            float len = d.magnitude;
            if (len < Epsilon) return Vector3.zero;

            float stretch = len - rest;
            if (stretch <= 0f) return Vector3.zero;

            Vector3 dir = d / len;
            tension01 = span > Epsilon ? Mathf.Clamp01(stretch / span) : 1f;
            Vector3 dv = dir * (Mathf.Max(0f, stiffness) * stretch * dt);

            float closing = Vector3.Dot(velocity, dir);
            if (closing < 0f)
                dv += dir * (-closing * (1f - Mathf.Exp(-Mathf.Max(0f, damping) * dt)));
            return dv;
        }

        /// <summary>
        /// Limit a velocity change so it cannot raise speed past <paramref name="cap"/>. It bounds
        /// GAIN only — a hull already faster than the cap (out of a swing, say) keeps its speed and
        /// the change keeps its full effect on direction. This is how the auto-tethers hold the
        /// vessel a little above cruise instead of accelerating it forever.
        /// </summary>
        public static Vector3 LimitSpeedGain(Vector3 velocity, Vector3 deltaV, float cap)
        {
            Vector3 next = velocity + deltaV;
            float before = velocity.magnitude, after = next.magnitude;
            if (after <= cap || after <= before || after < Epsilon) return deltaV;
            return next * (Mathf.Max(before, cap) / after) - velocity;
        }

        // ------------------------------------------------------------------ cutting

        /// <summary>Parameter of the point on segment a→b closest to <paramref name="p"/>, 0..1.</summary>
        public static float ClosestT(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            return lenSq < Epsilon * Epsilon ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
        }

        /// <summary>
        /// Velocity of a point on a tether, the cut's contact velocity. A tether is a rigid segment
        /// pivoting on its anchor, so a point <paramref name="t01"/> of the way from the anchor (0)
        /// to the hull (1) moves at that fraction of the hull's velocity — the blade is fastest at
        /// the hilt, the opposite of a sword. A moving anchor (a drifting crystal) adds its own
        /// share at the far end.
        /// </summary>
        public static Vector3 RopePointVelocity(Vector3 hullVelocity, Vector3 anchorVelocity, float t01)
        {
            float t = Mathf.Clamp01(t01);
            return anchorVelocity * (1f - t) + hullVelocity * t;
        }
    }
}
