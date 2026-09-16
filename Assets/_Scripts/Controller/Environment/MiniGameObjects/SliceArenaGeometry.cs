namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The envelope every <c>GameModes.Cleave</c> arena is built to — <b>one per INTENSITY</b>.
    ///
    /// Cleave's four intensities are four DIFFERENT PLACES rather than four sizes of one — angled
    /// panes, wave sheets, the nested cage, twisted ribbons. Each arena class is used by exactly
    /// ONE intensity, so each can carry its own scale as a compile-time constant; what this file
    /// owns is the TABLE, plus the two derived numbers every intensity-blind consumer needs.
    ///
    /// <b>Two dials per rung, and they do different things.</b>
    ///   • <see cref="LengthScaleI1"/>… — the SIMILARITY. Multiplies every authored LENGTH in that
    ///     arena (steps, prism dimensions, shell gaps, band radii, wave amplitudes) and divides its
    ///     noise FREQUENCY, so the whole arena is a uniform k x transform of the geometry that was
    ///     designed and reviewed at <see cref="AuthoredRadius"/>. Prism COUNTS do not move: every
    ///     count in these generators is a ratio of two lengths that both carry it.
    ///   • <see cref="GapScaleI1"/>… — the SPACING. Multiplies the ACROSS-grain step ALONE, so a
    ///     pane's ribs (or a sheet's) sit G times further apart while each rib stays a continuous
    ///     bar. That is the one dial that DOES move a count: rib count falls by G.
    ///
    /// The two are separate because they answer different questions. The similarity asks "how big
    /// is this place"; the gap asks "how much of it is mass". Intensities 1 and 2 turn both up —
    /// 6 x the authored radius at 3 x the rib spacing — so they read as vast and open, which is
    /// also why their destruction target is a quarter of the others'
    /// (<c>EndConditionOverridesSO.cleavePrismTargetByIntensity</c>).
    ///
    /// <b>A gap scale above 1 is only defined for an arena whose across-grain density is a STEP.</b>
    /// The Panes and the Swell sample ribs at a spacing, so tripling it is one constant. The CAGE
    /// has no such step at all — its density is a rib/hoop COUNT on a sphere — so a gap scale
    /// authored for it would be silently INERT, and <c>SpawnableRibcage.AssertNoGapScale</c> says so
    /// loudly instead. The Twistbands DOES have a step and carries the dial, authored at 1 because
    /// its deck is a continuous plated road rather than a set of ribs; raising it there opens the
    /// road into a lattice, which is a different arena, not a sparser one.
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
    ///   • <b>Prism SIZE is free in colliders</b> — only COUNT costs one — so growing the prisms
    ///     with the spacing is what keeps a rib reading as a continuous bar. At 6 x, three of the
    ///     Panes' authored lengths exceed <c>PrismScaleAnimator</c>'s serialized <c>maxScale</c> of
    ///     100, which clamps PER AXIS inside the setter with no log and no return value. The Cleave
    ///     arenas therefore opt into <c>CellEnvironmentSpawnableBase.AdmitsAuthoredPrismScale</c>,
    ///     which routes their lay through <c>Prism.AdmitTargetScale</c> — the documented call for
    ///     anything that STATES a size. It is opt-in rather than global because
    ///     <c>SpawnablePrism.prefab</c> is shared by ~30 spawnables and admitting a size that is
    ///     currently clamped is a behaviour change for whichever of them is relying on the clamp.
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

        /// <summary>Extra ACROSS-grain spacing, intensity 1. Multiplies the rib step alone.</summary>
        public const float GapScaleI1 = 3f;
        /// <summary>Extra ACROSS-grain spacing, intensity 2. Multiplies the rib step alone.</summary>
        public const float GapScaleI2 = 3f;
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
