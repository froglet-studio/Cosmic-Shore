using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure arithmetic of the Meerkat's kit (Garrett's design, approved by Yash 2026-10-28; <c>R_VesselActions/MEERKAT.md</c>
    /// §9), ported line for line from the Vessel Studio's Meerkat page (<c>Docs/Studios/VesselStudio/meerkat.html</c>, the
    /// "Garrett's kit" functions <c>kitOverrides</c>, <c>perpBasis</c>, <c>kitPortal</c>, <c>kitAddSentinel</c>,
    /// <c>kitZaps</c>, <c>kitTryRide</c>). No scene state: <c>MeerkatSolitonMathTests</c> holds it to the page's own numbers.
    /// The soliton's surface area and its swallow and fire are round 23's (<see cref="SolitonTunnelShapeMath.SurfaceAreaThroat"/>,
    /// <see cref="SolitonTunnelShapeMath.PullVelocity"/>, <see cref="MeerkatSwallow"/>), not this class's.
    /// </summary>
    public static class MeerkatSolitonMath
    {
        /// <summary>How many prisms an exit portal sets down (Garrett: a regular hexagon).</summary>
        public const int PortalCount = 6;

        /// <summary>A sentinel stands this far back from its tunnel's way in (the page's <c>wh.A - 3 t0</c>).</summary>
        public const float SentinelStandBack = 3f;

        /// <summary>An empty sentinel that found nothing in its guard looks again after this long (the page's 0.2 s idle).</summary>
        public const float ZapIdleSeconds = 0.2f;

        /// <summary>A ridden soliton is not re-entered by the same rider for this long (the page's 1.5 s).</summary>
        public const float RideAgainSeconds = 1.5f;

        // ------------------------------------------------------------------ Time: the ride

        /// <summary>The page's <c>kitOverrides</c>: the throat's ride multiplier × the rush (Time's ElementalFloat).</summary>
        public static float RideMid(float rideMidMultiplier, float rush) => rideMidMultiplier * Mathf.Max(0.05f, rush);

        /// <summary>...and Garrett's Time L5, Boost out: every soliton is left at <paramref name="boostOut"/> × the exit multiplier.</summary>
        public static float RideExit(float rideExitMultiplier, bool boostOutUnlocked, float boostOut) =>
            boostOutUnlocked ? rideExitMultiplier * Mathf.Max(1f, boostOut) : rideExitMultiplier;

        /// <summary>
        /// The page's <c>kitTryRide</c>: whether a hull at <paramref name="hull"/> facing <paramref name="forward"/> flies into a
        /// standing soliton's way in at <paramref name="a"/> (heading <paramref name="t0"/>, mouth radius
        /// <paramref name="mouth"/>): from 1 u before the mouth to 10 u past it, inside the mouth, nose along the tunnel.
        /// <paramref name="along"/> is how far past the way in it is.
        /// </summary>
        public static bool CanEnter(Vector3 hull, Vector3 forward, Vector3 a, Vector3 t0, float mouth, out float along)
        {
            var d = hull - a;
            along = Vector3.Dot(d, t0);
            if (along < -1f || along > 10f) return false;
            if ((d - t0 * along).magnitude > mouth) return false;
            return Vector3.Dot(forward, t0) >= 0.3f;
        }

        // ------------------------------------------------------------------ Charge: the sentinels

        /// <summary>The guard: a sphere of <paramref name="guardShare"/> (Garrett: 1.5) × the opening radius round the way in.</summary>
        public static float GuardRadius(float guardShare, float mouthRadius) => Mathf.Max(0f, guardShare) * mouthRadius;

        /// <summary>Where a sentinel stands: just back from its tunnel's way in.</summary>
        public static Vector3 SentinelPosition(Vector3 a, Vector3 t0) => a - t0 * SentinelStandBack;

        /// <summary>The pool: <paramref name="pool"/> sentinels, <paramref name="poolL5"/> at Charge L5 (Garrett: 3, 6).</summary>
        public static int PoolSize(bool sixSentinels, int pool, int poolL5) => Mathf.Max(1, sixSentinels ? poolL5 : pool);

        /// <summary>
        /// The page's <c>kitAddSentinel</c> pool rule, on ids in the order they were placed: with the pool used up, the OLDEST
        /// retires (and its soliton with it) until there is room, then <paramref name="id"/> joins. The retired ids, oldest
        /// first, go to <paramref name="retired"/>.
        /// </summary>
        public static void AddToPool(List<int> placed, int id, int poolSize, List<int> retired)
        {
            poolSize = Mathf.Max(1, poolSize);
            while (placed.Count >= poolSize)
            {
                retired.Add(placed[0]);
                placed.RemoveAt(0);
            }
            placed.Add(id);
        }

        /// <summary>
        /// The page's <c>kitZaps</c> clock: an empty sentinel's AI owes <paramref name="rate"/> zaps a second, never more than
        /// one in reserve (the page caps the owed count below 2, so a hitch never discharges as a burst).
        /// </summary>
        public static float OweZaps(float owed, float dt, float rate) => Mathf.Min(1.999f, owed + dt * Mathf.Max(0f, rate));

        /// <summary>The petals one zap knocks out, at a Charge normalized level (the page's <c>zapPetals</c>, 0.1 → 0.25).</summary>
        public static float ZapPetals(float atL0, float atL10, float normalizedLevel) =>
            Mathf.Max(0f, atL0 + (atL10 - atL0) * Mathf.Clamp01(normalizedLevel));

        // ------------------------------------------------------------------ Mass: the exit portal

        /// <summary>
        /// The page's <c>perpBasis</c>: two unit vectors square to <paramref name="t"/>, u = up × t (right × t when t is
        /// nearly up) and w = t × u.
        /// </summary>
        public static void PerpBasis(Vector3 t, out Vector3 u, out Vector3 w)
        {
            var seed = Mathf.Abs(t.y) > 0.9f ? Vector3.right : Vector3.up;
            u = Vector3.Cross(seed, t).normalized;
            w = Vector3.Cross(t, u).normalized;
        }

        /// <summary>
        /// The page's <c>kitPortal</c>: prism <paramref name="k"/> (0..5) of the exit portal - on a regular hexagon square to
        /// the exit heading <paramref name="tangent"/>, its corners <paramref name="share"/> (1.5) × the exit radius
        /// <paramref name="exitRadius"/> out, the prism at its edge's midpoint (the apothem) and laid along the edge, so the six
        /// close into one outline. <paramref name="along"/> is the edge's direction, <paramref name="outward"/> the way out.
        /// </summary>
        public static Vector3 PortalPoint(Vector3 exit, Vector3 tangent, float exitRadius, float share, int k,
            out Vector3 along, out Vector3 outward)
        {
            PerpBasis(tangent, out var u, out var w);
            float r = Mathf.Max(0f, share) * exitRadius, apothem = r * Mathf.Cos(Mathf.PI / 6f);
            float a = (k + 0.5f) / PortalCount * 2f * Mathf.PI, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            outward = u * ca + w * sa;
            along = u * -sa + w * ca;
            return exit + u * (ca * apothem) + w * (sa * apothem);
        }
    }
}
