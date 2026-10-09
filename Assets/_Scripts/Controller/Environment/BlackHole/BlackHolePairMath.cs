using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure maths of a BLACK–WHITE HOLE PAIR (Docs/BLACK_HOLE.md §11): how the two close on each
    /// other and annihilate, and where a body that fell into the black hole comes out of the white one.
    /// No scene, no time — every method is a function of its arguments, so
    /// <c>BlackHolePhysicsTests</c> hold it directly and the registry only applies it.
    ///
    /// <para><b>The life.</b> A pair is born a half-gap <c>s0</c> either side of its midpoint. While it
    /// is HELD (the Stoat's trigger: the pilot orbiting the black hole) it stands still. Once let go
    /// the two FALL TOWARD EACH OTHER, accelerating from rest to the closing speed over the ramp and
    /// closing at it after: <c>s(t) = s0 − v·t²/(2τ)</c> for <c>t ≤ τ</c>, <c>s0 − v·τ/2 − v·(t − τ)</c>
    /// after. Where their horizons touch they ANNIHILATE: both ease out through their warp weight and
    /// are gone. (The first version drifted them apart and back on a parabola; the playtest asked for
    /// a pair that only ever closes — 2026-10-09.)</para>
    ///
    /// <para><b>The tunnel.</b> A body that crosses the black horizon at <c>P</c> (relative to its
    /// centre) moving with velocity <c>v</c> comes out of the white hole at <c>−P</c> (relative to
    /// its centre) with the SAME velocity: a point reflection. Entering means <c>v·P &lt; 0</c>, so
    /// at <c>−P</c> the same <c>v</c> points outward — the body leaves the way it came in, and the
    /// white hole's repulsion (the black hole's pull, reversed) carries it off; its drawn
    /// spaghettification relaxes with distance, the capture movie run backwards. Prisms and vessels
    /// alike (<c>BlackHoleGravityField</c>, <c>BlackHoleVesselPull</c>).</para>
    /// </summary>
    public static class BlackHolePairMath
    {
        /// <summary>How far outside the white horizon an emitted body is placed, as a fraction of it.</summary>
        public const float EmitRadiusFraction = 1.05f;

        /// <summary>
        /// Half-gap <paramref name="t"/> seconds after the pair was let go: <paramref name="halfGap0"/>
        /// closed by a fall that accelerates from rest to <paramref name="closeSpeed"/> over
        /// <paramref name="rampSeconds"/> and holds it after. Never below zero.
        /// </summary>
        public static float ClosingHalfGap(float halfGap0, float closeSpeed, float rampSeconds, float t)
        {
            if (t <= 0f || closeSpeed <= 0f) return halfGap0;
            float closed = rampSeconds > 0f && t < rampSeconds
                ? 0.5f * closeSpeed * t * t / rampSeconds
                : 0.5f * closeSpeed * Mathf.Max(0f, rampSeconds) + closeSpeed * (t - Mathf.Max(0f, rampSeconds));
            return Mathf.Max(0f, halfGap0 - closed);
        }

        /// <summary>True once the two horizons touch (each hole within its own horizon of the midpoint).</summary>
        public static bool HaveMet(float halfGap, float horizonRadius) => halfGap <= Mathf.Max(0f, horizonRadius);

        /// <summary>Seconds from the let-go until the horizons touch (infinite when nothing closes them).</summary>
        public static float SecondsToMeet(float halfGap0, float horizonRadius, float closeSpeed, float rampSeconds)
        {
            float gap = halfGap0 - Mathf.Max(0f, horizonRadius);
            if (gap <= 0f) return 0f;
            if (closeSpeed <= 0f) return float.PositiveInfinity;
            float ramp = Mathf.Max(0f, rampSeconds);
            float rampDistance = 0.5f * closeSpeed * ramp;
            return gap <= rampDistance && ramp > 0f
                ? Mathf.Sqrt(2f * gap * ramp / closeSpeed)
                : ramp + (gap - rampDistance) / closeSpeed;
        }

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
