using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Everything that shapes a <see cref="NestedGyroidFlora"/>: the stack of nested gyroid sheets, the fibers that
    /// stitch them, how fast it grows and how it reads through its thickness. One asset per deployment; per the
    /// config-separation rule no numbers live on the prefab. The growth rule itself is <see cref="NestedGyroidBuilder"/>,
    /// a pure function of <see cref="ToSettings"/>, so two plants with one config and one element grow one lattice
    /// (built once, cached).
    ///
    /// <para><b>The base sheet is the GYROID FLORA's own tiling</b> (<see cref="NestedGyroidTemplate"/>, measured off its
    /// bond table: 576 sites per period, its octagon rings, its frames), and the element's prism is the gyroid flora's
    /// leaf for that element (its config's <c>Variant.LeafSize</c> / <c>LatticeScale</c>). So the subdivision and the
    /// per-element proportions are the original plant's, not this config's; what this config says is how big a
    /// period is, how many sheets nest, and how the stack is stitched and shaded (Docs/ECOSYSTEM.md §58.7).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "NestedGyroidConfig", menuName = "ScriptableObjects/Flora/Nested Gyroid Config")]
    public class NestedGyroidConfigSO : ScriptableObject
    {
        [Header("Field: G(p) = sin x cos y + sin y cos z + sin z cos x")]
        [Tooltip("World size of one gyroid period at lattice scale 1 - the gyroid flora's own period is 120 " +
                 "(NestedGyroidTemplate.Period), so 240 grows it at twice the original plant's size. The element's " +
                 "leaf scales WITH it (CellSize / 120), so the template's proportions hold at any size; an element's " +
                 "own LatticeScale (Space 3.19) then widens the lattice only, exactly as on the gyroid flora. At 240 " +
                 "the thinnest layer is ~9 units deep.")]
        [Min(10f)] public float CellSize = 240f;

        [Tooltip("Gyroid periods per side of the bounding cube. 576 template sites per period per sheet, so the prism " +
                 "count goes as the CUBE of this - 2 needs a budget near 8x the default.")]
        [Range(1, 4)] public int CellsPerSide = 1;

        [Tooltip("N - the number of nested sheets G = t_i, t_i evenly spaced on [-tMax, +tMax]. Odd N puts a sheet " +
                 "through the heart (G(0) = 0); even N puts the heart between the two central sheets. Every sheet " +
                 "is the same template, carried out along the gradient lines.")]
        [Range(2, 15)] public int SheetCount = 7;

        [Tooltip("Outermost level |t|. Clamped below 1.40: the only critical values of G are ±√2 and ±1.5, so inside " +
                 "|t| < √2 the gradient never vanishes, the sheets never touch, and every gradient line crosses each " +
                 "sheet exactly once - which is what lets one template site have exactly one image per sheet.")]
        [Range(0.05f, NestedGyroidSettings.TMaxCeiling)] public float TMax = 1.2f;

        [Tooltip("How many sheets the plant grows, counted outward from the heart (ring by ring). Below SheetCount the " +
                 "plant stops early and its outermost GROWN sheets are its skin; fibers stop there too.")]
        [Range(1, 15)] public int MaxSheetCount = 7;

        [Header("Sheets (the skin)")]
        [Tooltip("A plate is never thicker than this share of its local layer gap Δt/|∇G|. Below the cap a plate's " +
                 "thickness is the element's leaf thickness scaled by its layer's gap against the t = 0 layer's, so " +
                 "the outer layers and the saddles read visibly thicker.")]
        [Range(0.02f, 0.9f)] public float PlateThicknessOfGap = 0.45f;

        [Tooltip("Helicoidal plywood: each sheet's plates turned this far about ∇G relative to the sheet inside it. " +
                 "0 by default - the template's plates follow its own bonds, and turning them crosses each plate over " +
                 "its loop neighbours (measured at 25°: 52-66 plates per element lost to the fit, against 0-10 at 0°).")]
        [Range(-90f, 90f)] public float PlywoodTwistDegrees = 0f;

        [Header("Fibers (the warp)")]
        [Tooltip("Poisson spacing, over the template's sites, of the gradient lines that carry STRUTS. Every site's " +
                 "line through the stack is what places its plate on each sheet, so a strut always runs plate to " +
                 "plate; these are the lines drawn. Fibers are the stack's cross-sheet bonds: without them it is " +
                 "separate shells.")]
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
        [Tooltip("Growth front rate, prisms per second, before the element's tempo law (Time grows fastest - " +
                 "Flora.ResolveGrowPeriod). Ring by ring from the t = 0 sheet: inner sheets first, fibers extending " +
                 "as each new sheet appears, every prism hanging off one already standing.")]
        [Min(0.1f)] public float GrowthRate = 40f;

        [Tooltip("Hard ceiling on the plant's prisms. The tiling is the template's and is never coarsened, so over it " +
                 "the growth order is CUT (a cut prefix is still one connected plant, but its outer ring is " +
                 "incomplete - and the build warns). The default stack is ~4,250 at one period per side.")]
        [Min(16)] public int PrismBudget = 4600;

        [Tooltip("Milliseconds of lattice build per frame while a plant is first planted. The build is cached per config, " +
                 "so only the first plant of a config pays it.")]
        [Range(0.25f, 8f)] public float BuildSliceMilliseconds = 2f;

        [Header("Look - the domain's own prism states, never a lit one")]
        [Tooltip("How the stack reads through its thickness. The template's octagon rings are DANGER prisms on every " +
                 "sheet, exactly as on the gyroid flora, and the fiber struts are danger prisms; this chooses the " +
                 "darkening grade on top. A shade only ever DARKENS (gain <= 1): a lighter or whitened prism reads as a " +
                 "state the game does not have.")]
        public NestedGyroidColorMode ColorMode = NestedGyroidColorMode.GradedByLevel;

        [Tooltip("GradedByLevel: brightness gain on the -tMax sheet; the +tMax sheet is unshaded (1), linear between.")]
        [Range(0f, 1f)] public float GradedInnerGain = 0.55f;

        [Tooltip("AlternatingSheets: brightness gain on every odd sheet (even sheets are unshaded).")]
        [Range(0f, 1f)] public float AlternateSheetGain = 0.6f;

        [Header("Rider model (the Urchin's ride kernel - read, not tuned)")]
        [Tooltip("BlockscapeFollower.groundSearchRadiusScale. A prism's reach is max(1, largest extent) x this + hover; " +
                 "the build links neighbours only inside 90% of it and refuses to ship a bond beyond. Keep equal to " +
                 "the Urchin prefab's value.")]
        [Min(0.1f)] public float RiderGroundSearchScale = 2.5f;

        [Tooltip("BlockscapeFollower.hoverHeight. Keep equal to the Urchin prefab's value.")]
        [Min(0f)] public float RiderHoverHeight = 2f;

        [Tooltip("Seed for the Poisson choice of which gradient lines carry struts (deterministic: one seed, one " +
                 "lattice, on every machine).")]
        public int Seed = 1;

        /// <summary>The leaf the gyroid flora's own Time element grows (its config's Variant.LeafSize) - what a plant
        /// with no element block (a hand-placed test plant) wears.</summary>
        public static readonly Vector3 TemplateTimeLeaf = new Vector3(9f, 3.4f, 1.5f);

        /// <summary>
        /// The growth rule's inputs for a plant whose ELEMENT leaf is <paramref name="elementLeaf"/> (the gyroid
        /// flora's LeafSize for that element, at the template's native period) and whose element lattice scale is
        /// <paramref name="latticeScale"/> (FloraVariantTuning.LatticeScale - Space 3.19). The leaf scales with
        /// <see cref="CellSize"/> only, the lattice with both: the gyroid flora's own convention, so every element
        /// keeps the original plant's proportions. The rider model never scales.
        /// </summary>
        public NestedGyroidSettings ToSettings(Vector3 elementLeaf, float latticeScale = 1f, int budgetOverride = -1)
        {
            float k = latticeScale > 0f ? latticeScale : 1f;
            float size = CellSize / NestedGyroidTemplate.Period;
            var leaf = elementLeaf.sqrMagnitude > 0f ? elementLeaf : TemplateTimeLeaf;
            return new NestedGyroidSettings
            {
                CellSize = CellSize * k,
                CellsPerSide = CellsPerSide,
                SheetCount = SheetCount,
                TMax = TMax,
                MaxSheets = MaxSheetCount,
                Leaf = new System.Numerics.Vector3(leaf.x * size, leaf.y * size, leaf.z * size),
                FiberSeedSpacing = FiberSeedSpacing * k,
                FiberPrismSpacing = FiberPrismSpacing * k,
                PlywoodTwistDegrees = PlywoodTwistDegrees,
                PrismBudget = budgetOverride >= 0 ? budgetOverride : PrismBudget,
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
