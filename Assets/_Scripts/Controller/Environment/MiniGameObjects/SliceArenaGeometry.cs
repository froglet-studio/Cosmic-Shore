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
    /// <see cref="OuterRadius"/> would break the first two without failing anything.
    ///
    /// <b>The arena is bigger than a nucleus, and that is a design requirement rather than a
    /// coincidence.</b> The mode first shipped at <see cref="AuthoredRadius"/> = 360, which is
    /// SMALLER than a standard nucleus (<c>Nucleus.prefab</c> at scale 400 is ~392 world radius) -
    /// so four hand-built arenas read as one small ball parked in the middle of an otherwise empty
    /// cell, and a boosted Rhino crossed the whole thing in 0.6 s. <see cref="OuterRadius"/> is now
    /// twice that: the play space spans 1440 units, 1.8x a nucleus RADIUS and 60% of the membrane's
    /// own diameter, so the arena IS the cell rather than an ornament inside it.
    ///
    /// <b>Scaling it is a SIMILARITY, not a re-author.</b> Every arena multiplies its authored
    /// lengths - steps, prism dimensions, band radii, wave amplitudes - by
    /// <see cref="LengthScale"/>, and divides its noise FREQUENCIES by it, so the whole family is
    /// one uniform k x transform of the geometry that was designed and reviewed at
    /// <see cref="AuthoredRadius"/>. Three consequences are the reason it is done this way:
    ///
    ///   • <b>Prism counts do not move</b>, so the collider budget is untouched. Every count in
    ///     these generators is a RATIO (<c>floor(radius / step)</c>, <c>round(arc / step)</c>), and
    ///     both halves scale together.
    ///   • <b>The scale is an exact power of two</b>, so each scaled constant is bit-exact and no
    ///     <c>floor</c> boundary or noise sample can flip. The harness proves it: the measured
    ///     counts are IDENTICAL to the pre-scale measurement and every volume is exactly 8x.
    ///   • <b>Prism SIZE is free in colliders</b> - only COUNT costs one - so growing the prisms
    ///     with the spacing is what keeps a rib reading as a continuous bar instead of a dotted
    ///     line at twice the spacing. (The environment lay path writes <c>Prism.TargetScale</c>
    ///     directly, which clamps per axis inside the setter; <c>SpawnablePrism.prefab</c> - the
    ///     prefab every Cleave arena lays through - authors <c>maxScale</c> 100, so the scaled
    ///     dimensions are admitted. See CLAUDE.md on that silent clamp.)
    ///
    /// The one thing that is NOT a pure scale is the spawn ring, because the MEMBRANE did not
    /// scale: 576 x 2 would put players 48 units off the membrane wall, so the scene authors 1050
    /// (station 936 -> ring 1050 -> membrane 1200). <c>Tools/Build/cleave_budget.py</c> owns that
    /// number and asserts the ordering.
    ///
    /// Nothing offline carries its own copy of these numbers.
    /// <c>Tools/Build/cleave_arena_harness</c> COMPILES this file and writes the constants into
    /// <c>cleave_arena_measurements.json</c> alongside the arena counts it measured with them, and
    /// <c>Tools/Build/cleave_budget.py</c> reads the envelope from there - so the model and the
    /// shipped arenas agree by construction rather than by anyone remembering to update both.
    /// </summary>
    public static class SliceArenaGeometry
    {
        /// <summary>Nothing any Cleave arena lays may sit outside this radius from the cell centre.</summary>
        public const float OuterRadius = 720f;

        /// <summary>The radius the four arenas' spacings, prism sizes and noise frequencies were
        /// hand-tuned against. It is NOT the arena's size - <see cref="OuterRadius"/> is - it is the
        /// denominator of <see cref="LengthScale"/>, kept so the authored literals in each
        /// generator stay the readable numbers they were designed as.</summary>
        public const float AuthoredRadius = 360f;

        /// <summary>Uniform multiplier from authored units to world units. Every LENGTH in a Cleave
        /// arena is multiplied by it and every noise FREQUENCY divided by it; counts, angles and
        /// fractions-of-the-radius are left alone. Keep it an exact power of two and the geometry
        /// stays bit-identical up to scale - see the class summary.</summary>
        public const float LengthScale = OuterRadius / AuthoredRadius;

        /// <summary>Multiple of <see cref="OuterRadius"/> at which an AI parks between strikes.
        /// MUST stay above 1 - see the class summary.</summary>
        public const float AiStationStandoff = 1.3f;
    }
}
