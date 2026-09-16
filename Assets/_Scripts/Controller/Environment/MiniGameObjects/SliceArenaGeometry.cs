namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The one envelope every <c>GameModes.Cleave</c> arena is built to.
    ///
    /// Cleave's four intensities are four DIFFERENT PLACES rather than four sizes of one - angled
    /// panes, wave sheets, the nested cage, twisted ribbons - and the only thing they are required
    /// to share is how much room they take up. That is not tidiness: three separate systems are
    /// sized against the arena's outer radius and none of them can be told which intensity is
    /// running.
    ///
    ///   • <c>CleaveController</c> parks its AI stations at <see cref="OuterRadius"/> x
    ///     <see cref="AiStationStandoff"/>. <c>AIPilot</c> has no arrive-and-stop behaviour, so a
    ///     station INSIDE the mass is a point the AI orbits from within forever - the "the AI just
    ///     stays inside" defect, twice. The standoff must stay > 1 (asserted in
    ///     <c>SliceArenaGeometryTests</c>).
    ///   • The scene's <c>ServerPlayerVesselInitializer.spawnRingRadiusFloor</c> puts the players
    ///     outside all of it, far enough back to see the whole arena and line up a charge.
    ///   • The cell membrane contains the lot.
    ///
    /// So the ordering <c>OuterRadius &lt; OuterRadius x AiStationStandoff &lt; spawn ring &lt;
    /// membrane</c> is the real invariant, and an arena that quietly grew past
    /// <see cref="OuterRadius"/> would break the first two without failing anything. Build to it,
    /// and if a future arena genuinely needs more room, move this number and re-derive the ring
    /// rather than special-casing one intensity.
    ///
    /// Nothing offline carries its own copy of these two numbers.
    /// <c>Tools/Build/cleave_arena_harness</c> COMPILES this file and writes the constants into
    /// <c>cleave_arena_measurements.json</c> alongside the arena counts it measured with them, and
    /// <c>Tools/Build/cleave_budget.py</c> reads the envelope from there - so the model and the
    /// shipped arenas agree by construction rather than by anyone remembering to update both.
    /// </summary>
    public static class SliceArenaGeometry
    {
        /// <summary>Nothing any Cleave arena lays may sit outside this radius from the cell centre.</summary>
        public const float OuterRadius = 360f;

        /// <summary>Multiple of <see cref="OuterRadius"/> at which an AI parks between strikes.
        /// MUST stay above 1 - see the class summary.</summary>
        public const float AiStationStandoff = 1.3f;
    }
}
