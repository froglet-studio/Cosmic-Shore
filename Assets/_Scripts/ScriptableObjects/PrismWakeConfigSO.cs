using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the WAKE (<c>PrismWake</c>, <c>PrismWake.hlsl</c>, <c>HighPolyPrismMesh</c>,
    /// Docs/PRISM_ANIMATION.md §4.7.3).
    ///
    /// A CARRIER travelling fast enough drags a travelling ripple through the mass around its
    /// recent path. The prisms in that volume are swapped to a high-poly copy of the identical
    /// solid (<see cref="Subdivision"/>) so the surface RIPPLES instead of hinging, and the swap
    /// happens where the wake is provably zero (<see cref="ResidencyMargin"/>) so it is never seen.
    ///
    /// The deformation itself is a GLOBAL shader uniform written once per frame — there is no
    /// per-prism animation state and no per-prism cost to pay for widening the reach. What DOES
    /// cost is the residency swap, bounded by <see cref="MaxResidentPrisms"/> and
    /// <see cref="Subdivision"/>, and that budget is SHARED across every live wake — which is the
    /// arithmetic behind the design rule that the wake belongs to a FEW carriers (the Sparrow's
    /// skyburst missile and the Scarab's ball) rather than to every vessel.
    ///
    /// The carrier's RADIUS is not here: each carrier answers for its own, live, through
    /// <c>IPrismWakeCarrier</c>, and the wake's reach and train length are expressed as MULTIPLES
    /// of it — so a bigger thing leaves a bigger wake with nothing authored per carrier, the same
    /// object-sized principle the occlusion corridor is built on, and a skyburst's wake grows with
    /// it as MASS swells the round in flight.
    ///
    /// Place the asset at <c>Resources/PrismWakeConfig</c>. With no asset the defaults below
    /// apply, so the feature works out of the box.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismWakeConfig", menuName = "ScriptableObjects/Rendering/Prism Wake Config")]
    public class PrismWakeConfigSO : ScriptableObject
    {
        /// <summary>
        /// The largest value of |t·K'(t)| over the radial falloff family K(t) = (1−S(t))^e for
        /// e ∈ [1, 6], attained at e = 1, t = 2/3. It is the only thing besides the amplitude that
        /// enters the no-fold bound (<see cref="NeverFolds"/>), and it is a property of the
        /// falloff's SHAPE, so it cannot drift when the reach, the wavelength or the carrier radius
        /// are retuned. Re-derive it only if <c>PrismWakeRadial</c> changes family.
        /// </summary>
        public const float MaxRadialFalloffSlope = 0.889f;

        /// <summary>
        /// The amplitude above which the map can fold (b ≤ 0): 1 / (1 + <see cref="MaxRadialFalloffSlope"/>).
        /// <see cref="amplitude"/>'s inspector range stops meaningfully short of it on purpose —
        /// the margin is what lets the falloff's exponent be retuned without re-deriving the bound.
        /// </summary>
        public const float FoldingAmplitude = 1f / (1f + MaxRadialFalloffSlope);

        [Header("Wake")]
        [Tooltip("Master switch. Off publishes an empty bank and swaps nothing, which makes the " +
                 "shader's very first branch return the untouched vertex — prisms then cost exactly " +
                 "what they cost before this feature existed.")]
        [SerializeField] bool enabled = true;

        [Tooltip("How hard the mass is pushed away from (and pulled back toward) the carrier's path, " +
                 "as a dimensionless fraction of a vertex's own distance from that path. It is a " +
                 "STRAIN rather than a distance, which is what makes the path itself a fixed point " +
                 "and the map singularity-free. The range stops short of 0.529, where the map could " +
                 "begin to fold the prism inside out — see PrismWake.hlsl's NO FOLD note.")]
        [Range(0f, 0.45f)]
        [SerializeField] float amplitude = 0.25f;

        [Tooltip("How tightly the ripple hugs the carrier's path. 1 spreads it evenly out to the " +
                 "reach; larger keeps it close to the path with a longer flat tail. Clamped at 1 " +
                 "from below, where the falloff's derivative stops being finite at the outer edge " +
                 "and the wake gets a visible rim.")]
        [Range(1f, 6f)]
        [SerializeField] float radialExponent = 1.5f;

        [Tooltip("How far OUT from the carrier's path the ripple reaches, in multiples of the CARRIER'S " +
                 "own live radius — so a bigger thing leaves a bigger wake with nothing authored " +
                 "per carrier, and a skyburst's grows with it as MASS swells the round. At " +
                 "this distance the displacement, its first derivative and the normal correction " +
                 "are all exactly zero, so there is no seam where the wake ends.")]
        [Min(0.25f)]
        [SerializeField] float reachHullRadii = 3f;

        [Tooltip("How far BEHIND the carrier the wave train runs, in multiples of its radius. " +
                 "The train's envelope is zero at the carrier's own plane and again at this distance, " +
                 "value and slope both — those two planes are swept through mass at speed, and a " +
                 "kink at either would read as an invisible wall passing.")]
        [Min(0.25f)]
        [SerializeField] float trainHullRadii = 6f;

        [Tooltip("How many full crests fit in one train. This is the wake's WAVELENGTH, expressed " +
                 "so that it scales with the carrier: more waves is a finer ripple. Below about 1 the " +
                 "train holds less than one crest and reads as a single bulge rather than a wake.")]
        [Range(0.5f, 8f)]
        [SerializeField] float wavesPerTrain = 2.5f;

        [Tooltip("How fast the crests travel backward, as a multiple of the carrier's own speed. At " +
                 "exactly 1 a crest sits STILL in the world and the ship flies out from under it, " +
                 "which is what a boat's wake does; above 1 the crests stream backward as well, " +
                 "which reads as more energetic. It is never below 1 — a crest that lags the ship " +
                 "is being dragged along, which reads as an aura rather than a wake.")]
        [Range(1f, 3f)]
        [SerializeField] float phaseTravel = 1.35f;

        [Header("Speed window")]
        [Tooltip("The speed at which a wake starts to appear, world units per second. ABSOLUTE and " +
                 "carrier-independent, exactly like the speed tunnel's: the same speed on a missile " +
                 "and on a ball leaves the same wake, so a player learns the cue once, and nothing " +
                 "is normalised against a carrier's own top speed.\n\n" +
                 "AUTHOR IT FROM MEASURED SPEEDS. Its first value was 150, chosen without measuring " +
                 "anything, against a Squirrel that cruises at 54 and tops out at 300 — so on the " +
                 "hull the mode actually flew, the window never opened and the effect was reported " +
                 "as \"too subtle\" rather than as absent. A window that never opens is " +
                 "indistinguishable on screen from an effect that is too weak.")]
        [Min(0f)]
        [SerializeField] float engageSpeed = 150f;

        [Tooltip("The speed at which the wake reaches full strength, world units per second. Must " +
                 "exceed the engage speed; between the two the strength ramps linearly.")]
        [Min(1f)]
        [SerializeField] float fullSpeed = 400f;

        [Header("Geometry residency")]
        [Tooltip("Quads per face axis on the high-poly prism the wake swaps in: 12 is 1,728 " +
                 "triangles against the authored prism's 24. This is what buys the ripple a surface " +
                 "to bend — a deformation is only as smooth as the surface it moves. It is lower " +
                 "than the cradle's 16 because a wake's wavelength spans several prisms, where a " +
                 "drape's whole curvature sits inside one.")]
        [Range(2, 32)]
        [SerializeField] int subdivision = 12;

        [Tooltip("Hard ceiling on how many prisms may hold the high-poly mesh at once, per frame, " +
                 "across every wake in the match. This is the whole performance budget of the " +
                 "feature: at the default subdivision each resident prism is ~1.7k triangles, so 96 " +
                 "is ~166k. The budget is SHARED and SPLIT EVENLY — four wakes live at once get a " +
                 "quarter of it each, so each is coarser, not absent.\n\n" +
                 "It is also the reason a wake is granted to a FEW carriers rather than to every " +
                 "vessel: split far enough, every wake is the authored 24-triangle prism again and " +
                 "the effect is gone from all of them at once.")]
        [Min(0)]
        [SerializeField] int maxResidentPrisms = 48;

        [Tooltip("Extra world units beyond the wake's own volume at which a prism becomes resident. " +
                 "It exists so the mesh swap happens strictly OUTSIDE the volume the ripple can " +
                 "move anything, which is what makes it invisible. Set it above a typical prism's " +
                 "half-length: the spatial index keys prisms by their CENTRE, so a prism longer than " +
                 "twice this can swap while one end is already inside the wake.")]
        [Min(0f)]
        [SerializeField] float residencyMargin = 12f;

        [Header("Continuity")]
        [Tooltip("Seconds the wake takes to reach the strength the speed window asks for. The " +
                 "window is already smooth in speed, so this is the guard against speed JUMPING — " +
                 "a launch, a strike, a pool reissue — putting a full wake on screen in one frame.")]
        [Min(0f)]
        [SerializeField] float engageSeconds = 0.35f;

        [Tooltip("Seconds the wake takes to fade after the carrier drops below the engage speed, " +
                 "stops, or is retired. Longer than the engage, so a ball settling or a round " +
                 "finishing its flight reads as the water settling rather than the wake being " +
                 "switched off (continuity of existence).")]
        [Min(0f)]
        [SerializeField] float releaseSeconds = 0.9f;

        public bool Enabled => enabled;

        /// <summary>
        /// The radial strain's ceiling, dimensionless. Clamped to the inspector range rather than
        /// merely floored: past <see cref="FoldingAmplitude"/> the map can turn a prism inside out,
        /// which is not a stronger wake — it is a broken one.
        /// </summary>
        public float Amplitude => Mathf.Clamp(amplitude, 0f, 0.45f);

        /// <summary>
        /// The radial falloff's shaping power. Floored at 1 rather than merely clamped positive:
        /// below 1 the falloff's derivative diverges at the outer edge, which puts a visible rim
        /// exactly where the wake is supposed to vanish without one.
        /// </summary>
        public float RadialExponent => Mathf.Clamp(radialExponent, 1f, 6f);

        public float ReachHullRadii => Mathf.Max(0.25f, reachHullRadii);
        public float TrainHullRadii => Mathf.Max(0.25f, trainHullRadii);
        public float WavesPerTrain => Mathf.Clamp(wavesPerTrain, 0.5f, 8f);
        public float PhaseTravel => Mathf.Clamp(phaseTravel, 1f, 3f);

        public float EngageSpeed => Mathf.Max(0f, engageSpeed);

        /// <summary>
        /// The speed at which the wake is full. Held strictly above <see cref="EngageSpeed"/> so
        /// <see cref="StrengthForSpeed"/>'s divisor can never be zero — an asset with the two the
        /// same would otherwise make the ramp a step, which is the one thing the window exists to
        /// avoid.
        /// </summary>
        public float FullSpeed => Mathf.Max(EngageSpeed + 1f, fullSpeed);

        public int Subdivision => Mathf.Clamp(subdivision, 2, 32);
        public int MaxResidentPrisms => Mathf.Max(0, maxResidentPrisms);
        public float ResidencyMargin => Mathf.Max(0f, residencyMargin);
        public float EngageSeconds => Mathf.Max(0f, engageSeconds);
        public float ReleaseSeconds => Mathf.Max(0f, releaseSeconds);

        /// <summary>
        /// The speed window as a 0..1 ramp. The ONE place a speed becomes a wake strength, so the
        /// publisher, the source and the tests cannot disagree about where a wake starts.
        /// </summary>
        public float StrengthForSpeed(float speed) =>
            Mathf.Clamp01((speed - EngageSpeed) / (FullSpeed - EngageSpeed));

        /// <summary>
        /// The shape the shader can actually run: a positive amplitude, a falloff it can
        /// differentiate at both ends, and a volume with extent. The shader treats anything else as
        /// "off" (its second sentinel), so an insane asset degrades to no wake rather than to a
        /// division by zero.
        /// </summary>
        public bool IsSane =>
            Amplitude > 0f &&
            RadialExponent >= 1f &&
            ReachHullRadii > 0f &&
            TrainHullRadii > 0f &&
            FullSpeed > EngageSpeed;

        /// <summary>
        /// The no-fold guarantee, as ONE predicate both an edit-mode test and the offline harness
        /// call. The map's two stretch terms are c = 1 + E and b = (1 + E) + r·∂E/∂r, and both are
        /// bounded by the amplitude alone — no carrier radius, no reach, no wavelength — so this
        /// cannot be invalidated by retuning any of them.
        /// </summary>
        public bool NeverFolds => Amplitude * (1f + MaxRadialFalloffSlope) < 1f;
    }
}
