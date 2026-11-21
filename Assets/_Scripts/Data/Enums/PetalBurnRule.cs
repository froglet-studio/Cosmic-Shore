namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.

    /// <summary>
    /// HOW BIG a danger-prism contact bites - the stakes switch (Docs/ELEMENTAL_ECONOMY.md §4.1).
    /// Authored per CELL on <c>CellConfigDataSO.PetalBurnRule</c> and read by
    /// <c>VesselElementalDebuffByDangerPrismEffectSO</c> through <see cref="PetalBurnRules"/>, so
    /// one cell can play the measured alternative while every other mode keeps what shipped.
    /// The size applies to BOTH branches of that effect: the hostile burn (permanent) and the
    /// own-domain temporary debuff (decays) - the two are the same number by design.
    /// </summary>
    public enum PetalBurnRule
    {
        /// <summary>The shipped rule: the effect asset's <c>debuffMagnitude</c> (-0.5 normalized
        /// = FIVE petals per element, 20 per contact). The Living Ecology lab measured it
        /// stripping a careless pilot in 5-37 s.</summary>
        Shipped = 0,

        /// <summary>The tuned rule: the effect asset's <c>tunedDebuffMagnitude</c> (-0.1 = ONE
        /// petal per element, 4 per contact). Measured at 0.22 petals/min for a skilled pilot and
        /// 3.25 for a careless one.</summary>
        Tuned = 1,
    }

    /// <summary>
    /// The resolver: which authored magnitude a rule selects. Pure (no Unity) so the elemental
    /// transfer harness runs the game's own code. An unknown value falls back to
    /// <see cref="PetalBurnRule.Shipped"/>, so a cell that authors nothing plays what shipped.
    /// </summary>
    public static class PetalBurnRules
    {
        public static float Magnitude(PetalBurnRule rule, float shippedMagnitude, float tunedMagnitude)
            => rule == PetalBurnRule.Tuned ? tunedMagnitude : shippedMagnitude;
    }
}
