using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Everything that shapes a <see cref="NestedGyroidFlora"/>: the stack of nested gyroid sheets, the fibers that
    /// stitch them, how fast it grows and how its prisms are shaded. One asset per deployment; per the config-separation
    /// rule no numbers live on the prefab. The growth rule itself is <see cref="NestedGyroidBuilder"/>, a pure function of
    /// <see cref="ToSettings"/>, so two plants with one config grow one lattice (built once, cached).
    ///
    /// <para>The defaults are the measured ones (Docs/ECOSYSTEM.md §58, Tools/Build/nested_gyroid_harness): 2,011 prisms,
    /// one component, every bond inside the Urchin's reach, zero overlaps, ~0.1 s of build spread over frames.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "NestedGyroidConfig", menuName = "ScriptableObjects/Flora/Nested Gyroid Config")]
    public class NestedGyroidConfigSO : ScriptableObject
    {
        [Header("Field: G(p) = sin x cos y + sin y cos z + sin z cos x")]
        [Tooltip("World size of one gyroid period (2π in field units). This is the scale dial: the layer gap, the " +
                 "curvature and the plate size all grow with it. At 240 the thinnest layer is 8.8 units deep, which " +
                 "keeps an Urchin riding one sheet (hover 2) clear of the next.")]
        [Min(10f)] public float CellSize = 240f;

        [Tooltip("Gyroid periods per side of the bounding cube. Prism count goes as its CUBE, so 2 needs a budget near " +
                 "8x the default - otherwise the build coarsens the tiling until it fits.")]
        [Range(1, 4)] public int CellsPerSide = 1;

        [Tooltip("N - the number of nested sheets G = t_i, t_i evenly spaced on [-tMax, +tMax]. Odd N puts a sheet " +
                 "through the heart (G(0) = 0); even N puts the heart between the two central sheets.")]
        [Range(2, 15)] public int SheetCount = 7;

        [Tooltip("Outermost level |t|. Clamped below 1.40: the only critical values of G are ±√2 and ±1.5, so inside " +
                 "|t| < √2 the gradient never vanishes, the sheets never touch, and every gradient line crosses each " +
                 "sheet exactly once.")]
        [Range(0.05f, NestedGyroidSettings.TMaxCeiling)] public float TMax = 1.2f;

        [Tooltip("How many sheets the plant grows, counted outward from the heart (ring by ring). Below SheetCount the " +
                 "plant stops early and its outermost GROWN sheets are its skin; fibers stop there too.")]
        [Range(1, 15)] public int MaxSheetCount = 7;

        [Header("Sheets (the skin)")]
        [Tooltip("Poisson-disk spacing of the plates on each sheet, in world units. Every plate's footprint is a share " +
                 "of this, and the Urchin's reach scales with the plate - so the tiling stays ridable at any spacing.")]
        [Min(1f)] public float SheetPoissonSpacing = 20f;

        [Tooltip("Plate length (local y, along the combed tangent) as a share of the spacing, before the fit.")]
        [Range(0.05f, 1.5f)] public float PlateLengthOfSpacing = 0.8f;

        [Tooltip("Plate width (local x) as a share of the spacing, before the fit.")]
        [Range(0.05f, 1.5f)] public float PlateWidthOfSpacing = 0.45f;

        [Tooltip("Plate thickness (local z, the sheet normal) as a share of the LOCAL layer gap Δt/|∇G| - so the " +
                 "layers read visibly thicker near the saddles, where |∇G| is smallest. Capped at 3/4 of the plate's width.")]
        [Range(0.02f, 0.6f)] public float PlateThicknessOfGap = 0.22f;

        [Tooltip("Helicoidal plywood: each sheet's plate axis is turned this far about ∇G relative to the sheet inside it.")]
        [Range(-90f, 90f)] public float PlywoodTwistDegrees = 25f;

        [Header("Fibers (the warp)")]
        [Tooltip("Poisson spacing of the fiber seeds on the t = 0 sheet. Each seed is traced along ∇G/|∇G| out to the " +
                 "outermost sheets on both sides; its crossings are FORCED into each sheet's tiling, so a fiber always " +
                 "lands on a plate. Fibers are the stack's cross-sheet bonds: without them it is separate shells.")]
        [Min(1f)] public float FiberSeedSpacing = 60f;

        [Tooltip("Target spacing of the struts along a fiber, inside each layer gap.")]
        [Min(0.5f)] public float FiberPrismSpacing = 8f;

        [Tooltip("Strut cross-section (local x and y).")]
        [Min(0.1f)] public float FiberThickness = 1.6f;

        [Tooltip("Strut length as a share of its slot along the fiber.")]
        [Range(0.1f, 0.95f)] public float FiberFill = 0.8f;

        [Header("Clearance")]
        [Tooltip("Gap every prism keeps from every other (the fit shrinks to it; a strut keeps it from the plates it " +
                 "joins). Spindles are exempt - a limb may pass through a plate.")]
        [Min(0f)] public float Clearance = 0.5f;

        [Tooltip("No plate within this distance of the heart: the crystal's seat.")]
        [Min(0f)] public float HeartClearance = 8f;

        [Header("Growth")]
        [Tooltip("Growth front rate, prisms per second. Ring by ring from the t = 0 sheet: inner sheets first, fibers " +
                 "extending as each new sheet appears, every prism hanging off one already standing.")]
        [Min(0.1f)] public float GrowthRate = 40f;

        [Tooltip("Hard ceiling on the plant's prisms. Over it the tiling COARSENS (sheets and fibers together) until the " +
                 "stack fits; only if that fails is the growth order cut - and a cut prefix is still one connected plant.")]
        [Min(16)] public int PrismBudget = 2600;

        [Tooltip("Milliseconds of lattice build per frame while a plant is first planted. The build is cached per config, " +
                 "so only the first plant of a config pays it.")]
        [Range(0.25f, 8f)] public float BuildSliceMilliseconds = 2f;

        [Header("Look (a shade of the DOMAIN colour - the hue is always the team's)")]
        public NestedGyroidColorMode ColorMode = NestedGyroidColorMode.GradedByLevel;

        [Tooltip("GradedByLevel: brightness gain on the -tMax sheet (x) and the +tMax sheet (y), linear between.")]
        public Vector2 GradedGain = new Vector2(0.55f, 1.25f);

        [Tooltip("AlternatingSheets: brightness gain on every odd sheet (even sheets keep 1).")]
        [Range(0f, 2f)] public float AlternateSheetGain = 0.6f;

        [Tooltip("Fibers: how far their domain colour is lerped toward white - the third, distinct treatment.")]
        [Range(0f, 1f)] public float FiberWhiten = 0.45f;

        [Header("Rider model (the Urchin's ride kernel - read, not tuned)")]
        [Tooltip("BlockscapeFollower.groundSearchRadiusScale. A prism's reach is max(1, largest extent) x this + hover; " +
                 "the build links neighbours only inside 90% of it and refuses to ship a bond beyond. Keep equal to " +
                 "the Urchin prefab's value.")]
        [Min(0.1f)] public float RiderGroundSearchScale = 2.5f;

        [Tooltip("BlockscapeFollower.hoverHeight. Keep equal to the Urchin prefab's value.")]
        [Min(0f)] public float RiderHoverHeight = 2f;

        [Tooltip("Seed for the Poisson thinning (deterministic: one seed, one lattice, on every machine).")]
        public int Seed = 1;

        /// <summary>The growth rule's inputs. <paramref name="lengthScale"/> is a uniform similarity
        /// (FloraVariantTuning.LatticeScale): every LENGTH scales, the rider model does not.</summary>
        public NestedGyroidSettings ToSettings(float lengthScale = 1f, int budgetOverride = -1)
        {
            float k = lengthScale > 0f ? lengthScale : 1f;
            return new NestedGyroidSettings
            {
                CellSize = CellSize * k,
                CellsPerSide = CellsPerSide,
                SheetCount = SheetCount,
                TMax = TMax,
                MaxSheets = MaxSheetCount,
                SheetPoissonSpacing = SheetPoissonSpacing * k,
                FiberSeedSpacing = FiberSeedSpacing * k,
                FiberPrismSpacing = FiberPrismSpacing * k,
                PlywoodTwistDegrees = PlywoodTwistDegrees,
                PrismBudget = budgetOverride >= 0 ? budgetOverride : PrismBudget,
                PlateLengthOfSpacing = PlateLengthOfSpacing,
                PlateWidthOfSpacing = PlateWidthOfSpacing,
                PlateThicknessOfGap = PlateThicknessOfGap,
                FiberThickness = FiberThickness * k,
                FiberFill = FiberFill,
                Clearance = Clearance * k,
                HeartClearance = HeartClearance * k,
                RiderGroundSearchScale = RiderGroundSearchScale,
                RiderHoverHeight = RiderHoverHeight,
                Seed = Seed,
            }.Sanitized();
        }
    }
}
