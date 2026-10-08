using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure maths of a BLACK–WHITE HOLE PAIR (Docs/BLACK_HOLE.md §11): how the two drift apart
    /// and back together, and where a body that fell into the black hole comes out of the white one.
    /// No scene, no time — every method is a function of its arguments, so
    /// <c>BlackHolePhysicsTests</c> hold it directly and the registry only applies it.
    ///
    /// <para><b>The drift.</b> The pair is born at a half-gap <c>s0</c> either side of its midpoint,
    /// each hole moving outward along the pair's axis at the drift speed. They decelerate
    /// uniformly, stop at half the lifetime, fall back, and meet again at <c>s0</c> at the end of
    /// it — a parabola in the half-gap: <c>s(t) = s0 + v·t − (v/T)·t²</c>. That is the moment they
    /// ANNIHILATE: both ease out through their warp weight and are gone.</para>
    ///
    /// <para><b>The tunnel.</b> A body that crosses the black horizon at <c>P</c> (relative to its
    /// centre) moving with velocity <c>v</c> comes out of the white hole at <c>−P</c> (relative to
    /// its centre) with the SAME velocity: a point reflection. Entering means <c>v·P &lt; 0</c>, so
    /// at <c>−P</c> the same <c>v</c> points outward — the body leaves the way it came in, and the
    /// white hole's repulsion (the black hole's pull, reversed) carries it off; its drawn
    /// spaghettification relaxes with distance, the capture movie run backwards.</para>
    /// </summary>
    public static class BlackHolePairMath
    {
        /// <summary>How far outside the white horizon an emitted body is placed, as a fraction of it.</summary>
        public const float EmitRadiusFraction = 1.05f;

        /// <summary>
        /// Half-gap between the pair's holes <paramref name="t"/> seconds after birth: out from
        /// <paramref name="halfGap0"/> at <paramref name="driftSpeed"/>, decelerating to a stop at
        /// <paramref name="lifetime"/>/2, back to <paramref name="halfGap0"/> at <paramref name="lifetime"/>.
        /// Clamped to the birth gap after the lifetime (the pair has annihilated by then).
        /// </summary>
        public static float HalfGap(float halfGap0, float driftSpeed, float lifetime, float t)
        {
            if (lifetime <= 0f || t <= 0f) return halfGap0;
            if (t >= lifetime) return halfGap0;
            return halfGap0 + driftSpeed * t - (driftSpeed / lifetime) * t * t;
        }

        /// <summary>The widest the pair gets, at half its lifetime.</summary>
        public static float MaxHalfGap(float halfGap0, float driftSpeed, float lifetime) =>
            halfGap0 + 0.25f * driftSpeed * lifetime;

        /// <summary>True once the pair's lifetime is spent: the holes have met again and annihilate.</summary>
        public static bool IsSpent(float lifetime, float t) => t >= lifetime;

        /// <summary>
        /// The two holes' positions: the black hole <paramref name="halfGap"/> along −axis from the
        /// midpoint, the white hole the same along +axis.
        /// </summary>
        public static void Positions(Vector3 midpoint, Vector3 axis, float halfGap, out Vector3 black, out Vector3 white)
        {
            var a = axis.sqrMagnitude > 1e-8f ? axis.normalized : Vector3.right;
            black = midpoint - a * halfGap;
            white = midpoint + a * halfGap;
        }

        /// <summary>
        /// Where a body captured at <paramref name="capturePosition"/> by the black hole at
        /// <paramref name="blackCentre"/> emerges from the white hole at <paramref name="whiteCentre"/>:
        /// the point reflection of its entry, held at least <see cref="EmitRadiusFraction"/> ×
        /// <paramref name="whiteHorizon"/> from the white centre so it is born outside the horizon.
        /// A capture dead on the centre (no direction) emerges along <paramref name="fallbackDirection"/>.
        /// </summary>
        public static Vector3 ExitPosition(Vector3 capturePosition, Vector3 blackCentre, Vector3 whiteCentre,
            float whiteHorizon, Vector3 fallbackDirection)
        {
            var entry = capturePosition - blackCentre;
            var outward = -entry;
            float r = outward.magnitude;
            Vector3 dir;
            if (r > 1e-5f) dir = outward / r;
            else dir = fallbackDirection.sqrMagnitude > 1e-8f ? fallbackDirection.normalized : Vector3.up;
            float radius = Mathf.Max(r, EmitRadiusFraction * Mathf.Max(whiteHorizon, 1e-3f));
            return whiteCentre + dir * radius;
        }
    }
}
