using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The SHAPE of a round's in-flight growth: how far along its swell a projectile is at a
    /// given point in its flight.
    ///
    /// Pulled out as a pure function for the same reason the growth CURVE was
    /// (<see cref="ElementalScaling.RoundGrowthFactorForLevel"/>) — the shape is a feel
    /// decision that should be provable in an edit-mode test rather than only observable at
    /// 120 u/s. <see cref="Projectile"/>'s growth pass is the only caller.
    ///
    /// Two shapes, one expression:
    /// <list type="bullet">
    /// <item><b>Swell across the whole flight</b> (<paramref name="completeAt01"/> = 1) — the
    /// full-auto tracer. The round is still growing when it arrives, so its size reports how
    /// far it has come.</item>
    /// <item><b>Swell early, then hold</b> (0 &lt; <paramref name="completeAt01"/> &lt; 1) — the
    /// skyburst missile at 0.2. The round reaches full size in the first fifth of its flight
    /// and flies the rest of it at that size, so what you are looking at from the moment it
    /// clears the hull is the thing that will arrive.</item>
    /// </list>
    /// </summary>
    public static class RoundGrowthRamp
    {
        /// <summary>
        /// The multiplier a round is at, given how far through its flight it is
        /// (<paramref name="progress01"/>, 0 at the muzzle to 1 at the end of its lifetime),
        /// the factor it swells to (<paramref name="factor"/>) and the fraction of the flight
        /// the swell takes (<paramref name="completeAt01"/>, 1 = the whole flight).
        ///
        /// Always exactly 1 at launch and exactly <paramref name="factor"/> from
        /// <paramref name="completeAt01"/> onward — it never overshoots and never falls back,
        /// so a round can only ever grow into its size and hold it.
        /// </summary>
        public static float At(float progress01, float factor, float completeAt01 = 1f)
        {
            // A non-positive window means "already there" rather than a divide by zero: the
            // round is full size on its first frame.
            float t = completeAt01 <= 0f ? 1f : Mathf.Clamp01(progress01 / completeAt01);
            return Mathf.LerpUnclamped(1f, factor, t);
        }

        /// <summary>
        /// True once the swell is finished, so the caller can stop re-writing a transform that
        /// will not change again — a missile that holds its size for 80% of its flight would
        /// otherwise dirty its hierarchy every frame for nothing.
        /// </summary>
        public static bool IsComplete(float progress01, float completeAt01 = 1f)
            => completeAt01 <= 0f || progress01 >= completeAt01;
    }
}
