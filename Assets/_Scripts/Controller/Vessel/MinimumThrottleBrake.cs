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
    /// is 0 (<c>DefaultMinimumSpeed</c> 0 — Squirrel, Dolphin, Manta, Urchin, and the Rhino since
    /// this branch zeroed its floor) therefore targets a genuine 0 and then tails off toward it
    /// forever, so the pilot who holds the scissor gets a
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
    /// for no speed and this vessel authors no floor". Two whole classes of vessel are therefore
    /// untouched STRUCTURALLY rather than by tuning: every ONE-THUMB hull, because
    /// <c>SingleStickVesselTransformer.ComputeThrottleTarget</c> is
    /// <c>ThrottleScaler * boost + MinimumSpeed</c> with no throttle axis in it and so can never
    /// be zero; and the Scarab, which overrides <c>ComputeNoseAcceleration</c> wholesale and
    /// never reaches this step at all (it already brakes to a real stop through its own
    /// <c>coastDragPerSecond</c>). A vessel decelerating toward a lower-but-nonzero cruise is
    /// untouched too — decelerating to 40 still feels the way it always did.
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
        /// the top. Composed with the exponential per <see cref="Apply"/> — the exponential wins
        /// outright above <c>rate / LERP_AMOUNT</c>, so this owns only the tail — 2 s of cruise
        /// stops a Dolphin in <b>1.40 s</b> from cruise and <b>2.47 s</b> from a full boosted
        /// 347 u/s, and a Squirrel and a Manta in 1.40 s each from their very different cruises.
        /// Deliberately NOT fast enough to read as a wall: the whole of the fall the pilot can see
        /// is bit-identical to what shipped.
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
        /// <param name="current">The speed at the top of the frame. Zero means there is nothing to
        /// brake. A NEGATIVE value is the mirror case and is handled only when
        /// <paramref name="symmetric"/> is set: the vector model routinely hands a negative nose
        /// component here when the velocity points behind the nose, and braking that toward zero
        /// would change how every drifting hull recovers — so the mirror is opt-in, taken today
        /// only by a transformer whose throttle can actually COMMAND reverse.</param>
        /// <param name="target">The commanded cruise target. ANY non-zero target — a vessel with a
        /// floor, a pilot still asking for speed, or a pilot asking for reverse — stands the brake
        /// down. (Before reverse existed this read <c>target &gt; 0f</c>, which is the same test
        /// while no shipped transformer can produce a negative target: <c>XDiff</c> is in [0, 1],
        /// the scalers are positive and <c>CurrentBoostAmount</c> is at least 1. The reverse-capable
        /// axis is the first thing that can, and it must not be clamped to a stop at zero.)</param>
        /// <param name="symmetric">Brake a NEGATIVE <paramref name="current"/> up to zero as well.
        /// Off for every hull that cannot command reverse, which makes this a provable no-op for
        /// them — see the <paramref name="current"/> note.</param>
        public static float Apply(float stepped, float current, float target, float ratePerSecond,
                                  float dt, bool symmetric = false)
        {
            if (target != 0f || ratePerSecond <= 0f || dt <= 0f) return stepped;

            if (current < 0f)
            {
                // The mirror of the branch below: a reversing vessel released to centre is asking
                // for a STOP exactly as a forward one is, and the exponential tails off toward zero
                // from below just as hopelessly. MIN with zero so the brake can never push the
                // vessel forward past the stop it was asked for.
                return symmetric
                    ? Mathf.Min(0f, Mathf.Max(stepped, current + ratePerSecond * dt))
                    : stepped;
            }

            if (current == 0f) return stepped;

            // MIN of the two candidate speeds, never the SUM. Subtracting the rate from the
            // already-stepped value applies BOTH every frame, which is 40% below the legacy curve
            // half a second into a Squirrel's stop - a different deceleration feel on every
            // affected hull, not just an end on the old one. Taking the lower of "what the
            // exponential reached" and "what a constant rate reached" makes the exponential win
            // outright while it is the stronger of the two (above ratePerSecond / LERP_AMOUNT,
            // i.e. 20 u/s on a Squirrel), so the whole of the fall the pilot can see is
            // bit-identical to what shipped, and the constant rate owns only the tail.
            return Mathf.Max(0f, Mathf.Min(stepped, current - ratePerSecond * dt));
        }
    }
}
