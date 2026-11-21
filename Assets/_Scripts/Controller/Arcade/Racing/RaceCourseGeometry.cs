using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One gate of a generated race course: where the ring is, which way it faces, and how wide
    /// its mouth is. The mouth radius is BOTH the drawn ring and the crossing test's lateral
    /// bound - a switch's ring IS its trigger volume, drawn at its own radius
    /// (Docs/ToySystem/ARCHITECTURE.md, "The switch"), so the two can never drift.
    ///
    /// <para>Shared by every gate race rather than owned by one: Switchback flies an open chain
    /// of these and Headlong flies a closed circuit of them, and a gate is the same object in
    /// both. Was <c>SwitchbackGate</c> until Headlong needed the second course shape.</para>
    /// </summary>
    public readonly struct RaceGate
    {
        public readonly Vector3 Position;
        public readonly Vector3 Axis;      // unit; the direction the course flows through the mouth
        public readonly float Radius;

        public RaceGate(Vector3 position, Vector3 axis, float radius)
        {
            Position = position;
            Axis = axis;
            Radius = radius;
        }
    }

    /// <summary>
    /// The pure geometry and randomness every generated race course is built out of. No
    /// <c>UnityEngine.Random</c> (global state that deterministic systems seed - see
    /// Docs/ECOSYSTEM.md and the SkimRace track), no <c>System.Random</c> (its sequence is a
    /// property of the implementation rather than of the seed - the trap
    /// Docs/WEEKLY_CHALLENGE.md records), no <c>Time</c>, no scene access. Every function here
    /// is a pure function of its arguments, so a course generator built on it is unit-testable
    /// offline and reproduces exactly on any runtime.
    ///
    /// <para>Extracted from <c>SwitchbackCourse</c>'s private helpers when Headlong arrived and
    /// wanted the same deflection, turn-clamping and RNG against a different course SHAPE.
    /// A second copy would have been the semantic duplicate the ship protocol scans for, and
    /// the two would have drifted at the first tuning pass.</para>
    /// </summary>
    public static class RaceCourseGeometry
    {
        /// <summary>
        /// Deterministic 32-bit xorshift. Specified arithmetic on unsigned ints, so it is
        /// identical on every runtime.
        /// </summary>
        public struct Rng
        {
            uint _s;

            public Rng(int seed)
            {
                // 0 is the xorshift fixed point: it would emit nothing but zeros forever.
                uint s = unchecked((uint)seed);
                _s = s != 0u ? s : 0x9E3779B9u;
            }

            public uint NextUInt()
            {
                uint x = _s;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _s = x;
                return x;
            }

            /// <summary>Uniform in [0,1).</summary>
            public float Unit() => NextUInt() / 4294967296f;

            public float Range(float a, float b) => a + (b - a) * Unit();
        }

        public static Vector3 SafeNormalize(Vector3 v, Vector3 fallback) =>
            v.sqrMagnitude > 1e-10f ? v.normalized : fallback;

        /// <summary>Unsigned angle in degrees between two vectors.</summary>
        public static float Angle(Vector3 a, Vector3 b) =>
            Mathf.Acos(Mathf.Clamp(Vector3.Dot(SafeNormalize(a, Vector3.forward),
                                               SafeNormalize(b, Vector3.forward)), -1f, 1f)) * Mathf.Rad2Deg;

        /// <summary>Any unit vector perpendicular to <paramref name="v"/>, chosen deterministically.</summary>
        public static Vector3 Perpendicular(Vector3 v)
        {
            Vector3 a = Mathf.Abs(v.x) < 0.9f ? Vector3.right : Vector3.up;
            return SafeNormalize(Vector3.Cross(v, a), Vector3.up);
        }

        /// <summary>Rodrigues rotation of <paramref name="v"/> about the unit <paramref name="axis"/>.</summary>
        public static Vector3 RotateAbout(Vector3 v, Vector3 axis, float radians)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return v * c + Vector3.Cross(axis, v) * s + axis * (Vector3.Dot(axis, v) * (1f - c));
        }

        /// <summary>
        /// Rotate <paramref name="v"/> by a random angle up to <paramref name="maxDegrees"/>
        /// about a random perpendicular axis - a uniform draw on the CONE around v.
        ///
        /// <para>The angle is drawn as <c>max * sqrt(u)</c> rather than <c>max * u</c>: a cone's
        /// area grows with the angle, so a linear draw crowds every deflection near zero and the
        /// course comes out nearly straight. This is the same shape as the fauna-band fix in
        /// Docs/ECOSYSTEM.md - a uniform draw in a radial coordinate is not a uniform
        /// dispersal.</para>
        /// </summary>
        public static Vector3 Deflect(ref Rng rng, Vector3 v, float maxDegrees)
        {
            if (maxDegrees <= 0f) return v;

            Vector3 u = Perpendicular(v);
            Vector3 w = Vector3.Cross(v, u);
            float phi = rng.Range(0f, 2f * Mathf.PI);
            Vector3 spin = SafeNormalize(u * Mathf.Cos(phi) + w * Mathf.Sin(phi), u);
            float angle = maxDegrees * Mathf.Deg2Rad * Mathf.Sqrt(rng.Unit());
            return SafeNormalize(RotateAbout(v, spin, angle), v);
        }

        /// <summary>
        /// <paramref name="want"/> when it is already within <paramref name="maxDegrees"/> of
        /// <paramref name="prev"/>, else the direction exactly that far from
        /// <paramref name="prev"/> in want's plane. This is what makes a turn cap structural:
        /// every heading a walk accepts has passed through here or through
        /// <see cref="Deflect"/>, and neither can exceed it.
        /// </summary>
        public static Vector3 ClampTurn(Vector3 prev, Vector3 want, float maxDegrees)
        {
            float angle = Angle(prev, want);
            if (angle <= maxDegrees) return want;

            Vector3 axis = Vector3.Cross(prev, want);
            axis = axis.sqrMagnitude < 1e-10f ? Perpendicular(prev) : axis.normalized;
            return SafeNormalize(RotateAbout(prev, axis, maxDegrees * Mathf.Deg2Rad), prev);
        }

        /// <summary>
        /// The largest turn radius that FITS a corner: the vessel arrives along
        /// <c>p - previous</c>, leaves along <c>next - p</c>, and may spend at most half of the
        /// shorter leg as tangent length on each side (half, so two adjacent corners can never
        /// claim the same stretch of leg).
        ///
        /// <para>For a turn of <c>theta</c> with tangent length <c>T</c> the inscribed circle has
        /// radius <c>T / tan(theta/2)</c>, so a vessel whose minimum turn radius is <c>R</c> can
        /// hold the racing line through this corner exactly when <c>R &lt;= </c> this value. It
        /// is the tangent-length form rather than Switchback's <c>leg &gt; 2R sin(turn)</c> chord
        /// test because a closed circuit has a corner at EVERY vertex - there is no straight
        /// entry or exit to absorb the error.</para>
        ///
        /// <para>Returns <see cref="float.PositiveInfinity"/> for a straight-through vertex,
        /// which is correct: any radius fits.</para>
        /// </summary>
        public static float CornerRadius(Vector3 previous, Vector3 p, Vector3 next)
        {
            Vector3 inbound = p - previous;
            Vector3 outbound = next - p;
            float turn = Angle(inbound, outbound);
            if (turn < 1e-4f) return float.PositiveInfinity;

            float tangent = Mathf.Min(inbound.magnitude, outbound.magnitude) * 0.5f;
            return tangent / Mathf.Tan(turn * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>
        /// The tightest circle a vessel can fly at <paramref name="speed"/> given a turn rate of
        /// <c>speed * rotationThrottleScaler + turnScaler</c> degrees per second - the exact
        /// quantity <c>VesselTransformer.MinTurnRadius</c> reports, restated here so a course
        /// generator can be tested without a vessel.
        ///
        /// <para>Note what the <paramref name="rotationThrottleScaler"/> term does to the limit:
        /// at 0 the radius grows linearly and without bound, and at r &gt; 0 it CONVERGES to
        /// <c>180 / (pi * r)</c>. That asymptote is what a course can be sized against.</para>
        /// </summary>
        public static float MinTurnRadius(float speed, float rotationThrottleScaler, float turnScaler)
        {
            float omega = speed * rotationThrottleScaler + turnScaler;
            return omega <= 1e-4f ? float.PositiveInfinity : speed / (omega * Mathf.Deg2Rad);
        }
    }
}
