using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the Rhino sword's SLICE death (<c>PrismSlice</c>, <c>PrismSlice.hlsl</c>,
    /// Docs/PRISM_ANIMATION.md §4.10, R_VesselActions/RHINO_ENERGY_SWORD.md § "The slice").
    ///
    /// A prism the blade destroys is CUT along the plane the blade swept through it: the two
    /// halves hold together for a beat, part along the cut and open like a book, each showing a
    /// hot cut face that cools to the domain's bright colour, and then each dissolves away from
    /// the cut. Each half is a pure render entity drawing the shared <c>HighPolyPrismMesh</c>, so
    /// this is the high-poly prism's second consumer after the Urchin's cradle.
    ///
    /// Everything the CPU needs to STAMP and RETIRE a slice lives here (life, the motion time
    /// constants, the distances and angles); the purely visual dials (colours, glow, noise) live
    /// on <see cref="Material"/>. The two motion time constants and the dissolve window are pushed
    /// onto a runtime clone of that material, so this asset is the one source of truth for the
    /// clock and the shared material asset is never written at runtime.
    ///
    /// Place the asset at <c>Resources/PrismSliceConfig</c>. With no asset, or no material, the
    /// slice is off and the blade's kills fall back to the ordinary explosion — never to nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismSliceConfig", menuName = "ScriptableObjects/Rendering/Prism Slice Config")]
    public class PrismSliceConfigSO : ScriptableObject
    {
        [Header("Slice")]
        [Tooltip("Master switch. Off, every blade kill is the ordinary prism explosion.")]
        [SerializeField] bool enabled = true;

        [Tooltip("The PrismSlice.shader material the halves draw with. Visual dials (colours, glow, " +
                 "noise) are authored on it; the clock constants below are written onto a runtime " +
                 "clone, never onto this asset.")]
        [SerializeField] Material material;

        [Header("Budget")]
        [Tooltip("Quads per face axis on the high-poly prism each half draws: 10 is 1,200 triangles " +
                 "against the authored prism's 24. It is what makes the cut face a real surface and " +
                 "bounds the rim's chamfer to one grid cell (a tenth of a face at 10). Quadratic in " +
                 "triangles.")]
        [Range(2, 32)]
        [SerializeField] int subdivision = 10;

        [Tooltip("Hard ceiling on slices alive at once. Each is TWO halves, so the triangle bill is " +
                 "maxLiveSlices x 2 x 12 x subdivision^2 (48 x 2 x 1,200 = 115,200 at the defaults). " +
                 "A kill past the ceiling falls back to the ordinary explosion, so a blade sweeping a " +
                 "dense rib still reads — it just stops being sliced until the budget frees.")]
        [Min(0)]
        [SerializeField] int maxLiveSlices = 48;

        [Header("Clock")]
        [Tooltip("Seconds from the cut to retirement. The dissolve completes before this (see the " +
                 "end margin), so retirement is never visible.")]
        [Min(0.1f)]
        [SerializeField] float lifeSeconds = 1.15f;

        [Tooltip("Time constant of the halves PARTING along the cut (1 - e^(-t/tau)). Short: the " +
                 "wedge of the blade shoves them apart and they settle.")]
        [Min(0.005f)]
        [SerializeField] float separateSeconds = 0.09f;

        [Tooltip("Time constant of the halves OPENING like a book about the trailing edge. Longer " +
                 "than the parting, so the cut reads as clean first and falls open second.")]
        [Min(0.005f)]
        [SerializeField] float openSeconds = 0.3f;

        [Tooltip("Drag time constant on the drift the swing hands both halves. Total drift distance " +
                 "is drift speed x this, so the halves are carried along the blade's path and coast " +
                 "to a stop instead of flying off.")]
        [Min(0.005f)]
        [SerializeField] float driftDragSeconds = 0.35f;

        [Tooltip("When the dissolve starts, as a fraction of the life. Before it the halves are whole " +
                 "and the cut face is on show.")]
        [Range(0f, 0.9f)]
        [SerializeField] float dissolveStart = 0.3f;

        [Tooltip("How much of the life is left AFTER the dissolve finishes, as a fraction. Keeps the " +
                 "retirement strictly after the last fragment is gone.")]
        [Range(0.01f, 0.5f)]
        [SerializeField] float dissolveEndMargin = 0.08f;

        [Header("Motion")]
        [Tooltip("How far each half parts, as a fraction of the prism's half-thickness ACROSS the cut.")]
        [Min(0f)]
        [SerializeField] float separationFraction = 0.55f;

        [Tooltip("Floor on how far each half parts, world units — a thin prism still visibly opens.")]
        [Min(0f)]
        [SerializeField] float minSeparation = 0.25f;

        [Tooltip("Opening angle (degrees) for a slow graze.")]
        [Range(0f, 90f)]
        [SerializeField] float openAngleMin = 8f;

        [Tooltip("Opening angle (degrees) for a full-speed swing. Capped at 90: past that the " +
                 "hinge construction stops guaranteeing the halves cannot pass through each other.")]
        [Range(0f, 90f)]
        [SerializeField] float openAngleMax = 32f;

        [Tooltip("Contact speed at which the opening reaches its maximum, world units/sec.")]
        [Min(0.01f)]
        [SerializeField] float openAngleFullSpeed = 220f;

        [Tooltip("Fraction of the impact velocity the halves drift with — the blade carrying the " +
                 "pieces along its path.")]
        [Min(0f)]
        [SerializeField] float driftFraction = 0.2f;

        [Tooltip("Ceiling on the drift speed, world units/sec.")]
        [Min(0f)]
        [SerializeField] float maxDriftSpeed = 35f;

        [Header("The cut")]
        [Tooltip("How far from the prism's centre the cut may land, as a fraction of its half-" +
                 "thickness across the cut. The plane is the blade's own; this only stops a grazing " +
                 "tip from shaving a sliver off a corner, which reads as a chip rather than a slice.")]
        [Range(0f, 0.95f)]
        [SerializeField] float maxCutOffsetFraction = 0.55f;

        public bool Enabled => enabled;
        public Material Material => material;
        public int Subdivision => Mathf.Clamp(subdivision, 2, 32);
        public int MaxLiveSlices => Mathf.Max(0, maxLiveSlices);
        public float LifeSeconds => Mathf.Max(0.1f, lifeSeconds);
        public float SeparateSeconds => Mathf.Max(0.005f, separateSeconds);
        public float OpenSeconds => Mathf.Max(0.005f, openSeconds);
        public float DriftDragSeconds => Mathf.Max(0.005f, driftDragSeconds);
        public float DissolveStart => Mathf.Clamp(dissolveStart, 0f, 0.9f);
        public float DissolveEndMargin => Mathf.Clamp(dissolveEndMargin, 0.01f, 0.5f);
        public float SeparationFraction => Mathf.Max(0f, separationFraction);
        public float MinSeparation => Mathf.Max(0f, minSeparation);
        public float OpenAngleMinRadians => Mathf.Clamp(openAngleMin, 0f, 90f) * Mathf.Deg2Rad;
        public float OpenAngleMaxRadians => Mathf.Clamp(openAngleMax, 0f, 90f) * Mathf.Deg2Rad;
        public float OpenAngleFullSpeed => Mathf.Max(0.01f, openAngleFullSpeed);
        public float DriftFraction => Mathf.Max(0f, driftFraction);
        public float MaxDriftSpeed => Mathf.Max(0f, maxDriftSpeed);
        public float MaxCutOffsetFraction => Mathf.Clamp(maxCutOffsetFraction, 0f, 0.95f);

        /// <summary>Triangles drawn if every slot of the budget is in use.</summary>
        public long WorstCaseTriangles => (long)MaxLiveSlices * 2L * 12L * Subdivision * Subdivision;

        /// <summary>
        /// The shape the shader and the retirement can actually run: a dissolve window that ends
        /// strictly before retirement, and an opening that stays inside the 90° the hinge proof
        /// covers. One predicate, read by the edit-mode test and by <c>PrismSlice</c> itself.
        /// </summary>
        public bool IsSane =>
            DissolveStart < 1f - DissolveEndMargin &&
            OpenAngleMinRadians <= OpenAngleMaxRadians &&
            OpenAngleMaxRadians <= Mathf.PI * 0.5f + 1e-5f &&
            LifeSeconds > 0f;
    }
}
