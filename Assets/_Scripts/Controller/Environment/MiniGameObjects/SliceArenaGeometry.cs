namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The envelope every <c>GameModes.Cleave</c> arena is built to — <b>one per INTENSITY</b>.
    ///
    /// Cleave's four intensities are four DIFFERENT PLACES rather than four sizes of one — angled
    /// panes, wavy roads, the nested cage, twisted ribbons. Each arena class is used by exactly
    /// ONE intensity, so each can carry its own scale as a compile-time constant; what this file
    /// owns is the TABLE, plus the two derived numbers every intensity-blind consumer needs.
    ///
    /// <b>Three dials per rung, and they do different things.</b>
    ///   • <see cref="LengthScaleI1"/>… — the SIMILARITY. Multiplies every authored LENGTH that
    ///     describes WHERE the arena is (radii, shell gaps, band radii, wave amplitudes, the
    ///     across-grain spacing between one rib and the next) and divides its noise FREQUENCY, so
    ///     the PLACE is a uniform k x transform of the geometry designed at
    ///     <see cref="AuthoredRadius"/>.
    ///   • <see cref="PrismScaleI1"/>… — how big ONE PRISM is, and the along-grain step that keeps
    ///     a run of them continuous. <b>It is 2 on every rung</b>: a Cleave prism is the same small
    ///     size in every Cleave arena, whatever size the place is. See below.
    ///   • <see cref="GapScaleI1"/>… — the SPACING. Multiplies the ACROSS-grain step ALONE, so a
    ///     pane's ribs sit G times further apart while each rib stays a continuous bar. That is
    ///     the one dial that DOES move a count: rib count falls by G.
    ///
    /// They are separate because they answer different questions. The similarity asks "how big is
    /// this place"; the prism scale asks "what is it MADE of"; the gap asks "how much of it is
    /// mass". Intensities 1 and 2 are both 6 x the authored radius — vast open places you cross
    /// rather than dense objects you peel. Only the Panes spends the gap dial: it is a set of RIBS,
    /// and thinning ribs is what turns a slab into a place.
    ///
    /// <b>PRISM SIZE IS NOT PART OF THE SIMILARITY, and that is a design rule rather than a
    /// factoring convenience.</b> It used to be: at <see cref="LengthScaleI1"/> = 6 a pane's plank
    /// was 20 x 20 x 102 world units and a mullion 31 x 31 x 132, and the arena read as LOW POLY —
    /// a handful of enormous slabs. <b>Destroying many small prisms is the fun; destroying one big
    /// one is not</b>, and a field of small prisms reads as high-tech where the same mass in fewer
    /// pieces reads as cheap geometry. So the prism scale is pinned at 2 on all four rungs and the
    /// two 6 x arenas keep their SPACE while their mass is re-cut into roughly three times as many
    /// pieces, each a third the size. Rungs 3 and 4 were already at 2 and are byte-for-byte
    /// unchanged, which is what makes ONE number honest across the whole ladder.
    ///
    /// The cost is stated rather than hidden: <b>a count that is a ratio of two lengths moves when
    /// the two lengths stop sharing a scale.</b> The along-grain step carries the PRISM scale (a run
    /// of prisms must stay continuous) while an arena's across-grain layout carries the SIMILARITY,
    /// so the 6 x rungs' counts rise by roughly <c>LengthScale / PrismScale</c> = 3 — the Panes
    /// measured 5,107 -> 15,380. That is still under the collider ceiling the mode has already
    /// shipped, and it forces the destruction target up with it
    /// (<c>EndConditionOverridesSO.cleavePrismTargetByIntensity</c>, 400 -> 1,200) — a target is a
    /// fraction of the arena, so re-cutting the arena into more pieces re-prices it. It is the same
    /// match: 1,200 of the new prisms is a THIRD the volume 400 of the old ones were.
    ///
    /// One effect worth carrying beyond this mode: <b>shrinking the prisms bought back the volume
    /// ladder's float32 resolution.</b> <c>Cell.liveVolumeTotal</c> is a float32 running total and
    /// its resolution is the ulp at the value it holds; at 6 x the Panes' baseline was 345M (ulp 32)
    /// so a 4.5-volume Rhino trail prism could not move it AT ALL. At 38.5M the ulp is 4 and the
    /// ladder moves again. <c>Tools/Build/cleave_budget.py</c>'s check 7 measures it per rung; the
    /// Swell is still over the line (ulp 8) and still gated on the cell growing nothing.
    ///
    /// <b>A gap scale above 1 is only defined for an arena whose across-grain density is a STEP,
    /// and only wanted where the surface is a set of BARS rather than a road.</b> The Panes samples
    /// ribs at a spacing, so tripling it is one constant. The CAGE has no such step at all — its
    /// density is a rib/hoop COUNT on a sphere — so a gap scale authored for it would be silently
    /// INERT, and <c>SpawnableRibcage.AssertNoGapScale</c> says so loudly instead. The Swell and
    /// the Twistbands both HAVE a step and both carry the dial authored at 1, for one reason: each
    /// lays a continuous plated deck a blade is held against, so opening its lanes up is not
    /// "sparser", it is a lattice the sword rattles through — a different arena, not a lighter one.
    ///
    /// <b>Three systems are sized against the arena and only one of them knows the intensity.</b>
    ///   • <c>CleaveController</c> parks its AI stations at that intensity's
    ///     <see cref="OuterRadiusFor"/> x <see cref="AiStationStandoff"/>. It reads
    ///     <c>GameDataSO.SelectedIntensity</c>, which is set server-side before the scene loads, so
    ///     it is exact. <c>AIPilot</c> has no arrive-and-stop behaviour, so a station INSIDE the
    ///     mass is a point the AI orbits from within forever — the "the AI just stays inside"
    ///     defect, twice. The standoff must stay > 1 (asserted in <c>SliceArenaGeometryTests</c>).
    ///   • The scene's <c>ServerPlayerVesselInitializer.spawnRingRadiusFloorByIntensity</c> puts the
    ///     players outside all of it. That list exists BECAUSE the ring is one serialized scene
    ///     value and this mode now needs four — one ring for a 2,160-radius arena and the same ring
    ///     for a 720 one would either spawn a pilot inside the big arenas or park them 3,000 units
    ///     from a speck.
    ///   • The cell membrane contains the lot, and it is per-intensity for the same reason: each
    ///     intensity already has its own <c>CellConfigDataSO</c>, so intensities 1 and 2 point at a
    ///     resized <c>CleaveMembrane.prefab</c> (3,600) while 3 and 4 keep the standard 1,200.
    ///
    /// So the ordering <c>OuterRadius &lt; OuterRadius x AiStationStandoff &lt; spawn ring &lt;
    /// membrane</c> is the real invariant, and it must hold <b>per intensity</b>.
    /// <c>Tools/Build/cleave_budget.py</c> asserts it on the prism's far CORNER for each rung.
    ///
    /// <b>The arena is bigger than a nucleus on every rung, and that is a design requirement.</b>
    /// The mode first shipped at <see cref="AuthoredRadius"/> = 360, which is SMALLER than a
    /// standard nucleus (~392 world radius), so four hand-built arenas read as one small ball parked
    /// in the middle of an otherwise empty cell. The cage and the twistbands are now twice that; the
    /// panes and the swell are six times it, spanning 4,320 units — over three times the standard
    /// membrane's own diameter, which is why they carry their own.
    ///
    /// <b>Scaling an arena is a SIMILARITY, not a re-author</b>, and three properties are why:
    ///
    ///   • <b>Prism counts do not move with <see cref="LengthScaleI1"/></b>, so the collider budget
    ///     is untouched by the envelope; only the gap dial changes a count, and it only ever
    ///     lowers one.
    ///   • <b>The scale is an exact power of two where it can be</b>, so scaled constants stay
    ///     bit-exact and no <c>floor</c> boundary or noise sample can flip. At 6 (2 x 3) that no
    ///     longer holds exactly, so the 6 x rungs are re-MEASURED rather than assumed.
    ///   • <b>Prism SIZE is free in colliders</b> — only COUNT costs one — which is why the prism
    ///     dial can be spent without a budget conversation, and why spending it DOWNWARD is not
    ///     free: a third the size at the same along-grain density is three times the count.
    ///     All four arenas opt into <c>CellEnvironmentSpawnableBase.AdmitsAuthoredPrismScale</c>,
    ///     which routes their lay through <c>Prism.AdmitTargetScale</c> — the documented call for
    ///     anything that STATES a size. It is opt-in rather than global because
    ///     <c>SpawnablePrism.prefab</c> is shared by ~30 spawnables and admitting a size that is
    ///     currently clamped is a behaviour change for whichever of them is relying on the clamp.
    ///     <b>It was REQUIRED while prism size rode the envelope</b> — three of the 6 x rungs'
    ///     lengths cleared that prefab's <c>maxScale</c> of 100, which clamps PER AXIS inside the
    ///     setter with no log and no return value — and is now a standing guard, every authored
    ///     size being comfortably inside the window. That is worth noticing rather than tidying
    ///     away: <i>a shared prefab's scale ceiling was the only thing in the project saying the
    ///     prisms had grown absurd, and it said it silently.</i>
    /// </summary>
    public static class SliceArenaGeometry
    {
        /// <summary>The radius the four arenas' spacings, prism sizes and noise frequencies were
        /// hand-tuned against. It is NOT any arena's size — it is the denominator of every
        /// length scale below, kept so the authored literals in each generator stay the readable
        /// numbers they were designed as.</summary>
        public const float AuthoredRadius = 360f;

        /// <summary>Uniform authored-units -> world-units multiplier, intensity 1 (The Panes).</summary>
        public const float LengthScaleI1 = 6f;
        /// <summary>Uniform authored-units -> world-units multiplier, intensity 2 (The Swell).</summary>
        public const float LengthScaleI2 = 6f;
        /// <summary>Uniform authored-units -> world-units multiplier, intensity 3 (The Cage).</summary>
        public const float LengthScaleI3 = 2f;
        /// <summary>Uniform authored-units -> world-units multiplier, intensity 4 (The Twistbands).</summary>
        public const float LengthScaleI4 = 2f;

        /// <summary>Authored-units -> world-units for a PRISM'S OWN DIMENSIONS and for the
        /// along-grain step that keeps a run of them continuous. Deliberately the SAME on every
        /// rung — see the class summary. Rungs 3 and 4 also run <see cref="LengthScaleI3"/> = 2, so
        /// for them this changes nothing at all.</summary>
        public const float PrismScaleI1 = 2f;
        /// <inheritdoc cref="PrismScaleI1"/>
        public const float PrismScaleI2 = 2f;
        /// <inheritdoc cref="PrismScaleI1"/>
        public const float PrismScaleI3 = 2f;
        /// <inheritdoc cref="PrismScaleI1"/>
        public const float PrismScaleI4 = 2f;

        /// <summary>Extra ACROSS-grain spacing, intensity 1. Multiplies the rib step alone.</summary>
        public const float GapScaleI1 = 3f;
        /// <summary>Intensity 2's deck is a plate lattice sized to its ribbon — see the class summary.</summary>
        public const float GapScaleI2 = 1f;
        /// <summary>Intensity 3's density is a rib/hoop COUNT, not a step — see the class summary.</summary>
        public const float GapScaleI3 = 1f;
        /// <summary>Intensity 4's deck is a plate lattice sized to its ribbon — see the class summary.</summary>
        public const float GapScaleI4 = 1f;

        /// <summary>Nothing any Cleave arena lays may sit outside this radius from the cell centre.</summary>
        public const float OuterRadiusI1 = AuthoredRadius * LengthScaleI1;   // 2160
        /// <inheritdoc cref="OuterRadiusI1"/>
        public const float OuterRadiusI2 = AuthoredRadius * LengthScaleI2;   // 2160
        /// <inheritdoc cref="OuterRadiusI1"/>
        public const float OuterRadiusI3 = AuthoredRadius * LengthScaleI3;   // 720
        /// <inheritdoc cref="OuterRadiusI1"/>
        public const float OuterRadiusI4 = AuthoredRadius * LengthScaleI4;   // 720

        /// <summary>Multiple of an intensity's own outer radius at which an AI parks between
        /// strikes. MUST stay above 1 — see the class summary.</summary>
        public const float AiStationStandoff = 1.3f;

        /// <summary>The widest rung, for anything that must bound EVERY intensity at once.</summary>
        public const float MaxOuterRadius = OuterRadiusI1;

        /// <summary>Intensity (1-based, clamped) -> that arena's length scale.</summary>
        public static float LengthScaleFor(int intensity) => intensity switch
        {
            <= 1 => LengthScaleI1,
            2 => LengthScaleI2,
            3 => LengthScaleI3,
            _ => LengthScaleI4,
        };

        /// <summary>Intensity (1-based, clamped) -> that arena's prism scale.</summary>
        public static float PrismScaleFor(int intensity) => intensity switch
        {
            <= 1 => PrismScaleI1,
            2 => PrismScaleI2,
            3 => PrismScaleI3,
            _ => PrismScaleI4,
        };

        /// <summary>Intensity (1-based, clamped) -> that arena's across-grain gap scale.</summary>
        public static float GapScaleFor(int intensity) => intensity switch
        {
            <= 1 => GapScaleI1,
            2 => GapScaleI2,
            3 => GapScaleI3,
            _ => GapScaleI4,
        };

        /// <summary>Intensity (1-based, clamped) -> the radius nothing in that arena may exceed.</summary>
        public static float OuterRadiusFor(int intensity) => intensity switch
        {
            <= 1 => OuterRadiusI1,
            2 => OuterRadiusI2,
            3 => OuterRadiusI3,
            _ => OuterRadiusI4,
        };

        /// <summary>Where <c>CleaveController</c> parks an AI between strikes, for this intensity.</summary>
        public static float AiStationRadiusFor(int intensity) =>
            OuterRadiusFor(intensity) * AiStationStandoff;
    }
}
