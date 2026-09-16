using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The terminal approach to a ZERO cruise target — what makes "throttle at minimum" mean the
    /// vessel actually stops.
    ///
    /// The fleet's throttle tracking is an exponential lerp toward
    /// <c>VesselTransformer.ComputeThrottleTarget()</c>, which is the right shape everywhere
    /// except at the bottom: an exponential NEVER ARRIVES. A two-thumb flier whose throttle floor
    /// is 0 (<c>DefaultMinimumSpeed</c> 0 — Squirrel, Dolphin, Manta, Urchin) therefore targets a
    /// genuine 0 and then tails off toward it forever, so the pilot who holds the scissor gets a
    /// crawl that keeps shrinking rather than a stop. From a boosted Dolphin's 347 u/s that tail
    /// is several seconds and a couple of hundred units long, which is what "it doesn't come to a
    /// stop" actually looks like from the seat.
    ///
    /// This is the same answer the fleet already reached twice for the same reason — the Scarab's
    /// <c>coastDragPerSecond</c> and the Urchin's <c>detachSpeedDecayRate</c> are both constant
    /// rates chosen, in their own words, over "an exponential tail that never quite lands". This
    /// puts it in the SHARED step so every vessel gets it instead of the two that grew their own.
    ///
    /// It applies ONLY when the commanded target is zero, which is exactly "the pilot is asking
    /// for no speed and this vessel authors no floor". A vessel with a non-zero
    /// <c>MinimumSpeed</c> (the Rhino's 10) still targets that floor and is untouched, as is any
    /// vessel decelerating toward a lower-but-nonzero cruise — decelerating to 40 still feels the
    /// way it always did.
    /// </summary>
    public static class MinimumThrottleBrake
    {
        /// <summary>
        /// How long the brake takes to remove one full unboosted cruise's worth of speed.
        /// Expressed in the vessel's OWN cruise rather than as an absolute u/s so a 68 u/s Dolphin
        /// and a 180 u/s Manta stop in a comparable TIME instead of the fast hull coasting three
        /// times as far; see <see cref="RateFor"/>.
        ///
        /// 2 s is calibrated against the one shipped vessel that already brakes to a real stop:
        /// the Scarab sheds its 216 u/s ceiling at <c>coastDragPerSecond</c> 120, i.e. ~1.8 s from
        /// the top. Composed with the exponential — which still removes far more per frame at
        /// speed, so this only owns the tail — 2 s of cruise lands a Dolphin in ~0.9 s from cruise
        /// and ~1.9 s from a full boosted 347 u/s. Deliberately NOT fast enough to read as a wall:
        /// the exponential's shape is unchanged for the whole of the fall the pilot can see.
        /// </summary>
        public const float DefaultBrakeSeconds = 2f;

        /// <summary>The brake's strength in u/s², derived from the vessel's own full-throttle
        /// cruise. 0 (no brake, legacy exponential tail only) whenever either input is
        /// non-positive — including a transformer whose <c>ThrottleScaler</c> has not been
        /// seeded from its Default yet.</summary>
        public static float RateFor(float throttleScaler, float brakeSeconds)
            => throttleScaler > 0f && brakeSeconds > 0f ? throttleScaler / brakeSeconds : 0f;

        /// <summary>
        /// Land <paramref name="stepped"/> — the value the ordinary exponential step already
        /// produced this frame — on a real zero when the target is zero.
        ///
        /// Takes whichever of the two is further along, so the exponential still owns the fast
        /// early fall (it removes far more per frame at speed) and the constant rate owns only the
        /// last stretch, where the exponential has given up. The result is the same deceleration
        /// curve the fleet has always had, with an end on it.
        /// </summary>
        /// <param name="stepped">This frame's exponentially-stepped speed.</param>
        /// <param name="current">The speed at the top of the frame. A non-positive value means
        /// there is nothing to brake (the vector model can hand a negative nose component here
        /// when the velocity points behind the nose), and the brake stands down.</param>
        /// <param name="target">The commanded cruise target. Anything above zero is a vessel with
        /// a floor or a pilot still asking for speed, and the brake stands down.</param>
        public static float Apply(float stepped, float current, float target, float ratePerSecond, float dt)
        {
            if (target > 0f || current <= 0f || ratePerSecond <= 0f || dt <= 0f) return stepped;
            if (stepped <= 0f) return 0f;
            return Mathf.Max(0f, stepped - ratePerSecond * dt);
        }
    }
}
