using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the Urchin's CRADLE (<c>PrismCradle</c>, <c>PrismCradle.hlsl</c>,
    /// <c>HighPolyPrismMesh</c>, Docs/PRISM_ANIMATION.md §4.7.2).
    ///
    /// While an Urchin RIDES a prismscape — attached, not launched, not in free flight — the mass
    /// around it DRAPES over the hull: every vertex within <see cref="DrapeReach"/> of the hull's
    /// surface slides along its own radius toward that surface, closing over the parts of the ship
    /// the prism would have swallowed and rising to meet the parts it has not reached. The prisms
    /// close enough to be draped are swapped to a high-poly copy of the identical solid
    /// (<see cref="Subdivision"/>) so the surface BENDS instead of hinging, and the swap happens
    /// where the drape is provably zero (<see cref="ResidencyMargin"/>) so it is never visible.
    /// The pilot reads it as the mass they are grinding cradling them.
    ///
    /// The deformation itself is a GLOBAL shader uniform written once per frame — there is no
    /// per-prism animation state and no per-prism cost to pay for widening the reach. What DOES
    /// cost is the residency swap, which is bounded by <see cref="MaxResidentPrisms"/> and
    /// <see cref="Subdivision"/>. The hull's RADIUS is not here: it is a property of the vessel
    /// (measured once from its hull by <c>PrismCradleSource</c>), not of the feel.
    ///
    /// Place the asset at <c>Resources/PrismCradleConfig</c>. With no asset the defaults below
    /// apply, so the feature works out of the box.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismCradleConfig", menuName = "ScriptableObjects/Rendering/Prism Cradle Config")]
    public class PrismCradleConfigSO : ScriptableObject
    {
        [Header("Cradle")]
        [Tooltip("Master switch. Off publishes an empty bank and swaps nothing, which makes the " +
                 "shader's very first branch return the untouched vertex — prisms then cost exactly " +
                 "what they cost before this feature existed.")]
        [SerializeField] bool enabled = true;

        [Tooltip("How far beyond the hull's SURFACE the drape reaches, in world units. At this " +
                 "distance the displacement, its first derivative and the normal correction are all " +
                 "exactly zero, so there is no seam where the effect ends — it is the width of the " +
                 "lip the mass rises into, not a cutoff. Mass INSIDE the hull is always fully " +
                 "wrapped onto its surface regardless of this.")]
        [Min(0.01f)]
        [SerializeField] float drapeReach = 6f;

        [Tooltip("The silkiness. 1 is a broad, soft drape that starts rising a long way out; larger " +
                 "pulls the fabric tight against the hull and leaves a longer flat tail. Clamped at " +
                 "1 from below, where the falloff's derivative stops being finite at the far edge.")]
        [Range(1f, 6f)]
        [SerializeField] float drapeExponent = 1.5f;

        [Tooltip("The CEILING the eased strength runs to — how much of the full drape is ever " +
                 "performed. At 1 mass inside the hull lands exactly on its surface; below that it " +
                 "goes proportionally less of the way. This is the dial for \"the effect is too " +
                 "strong\" — never the reach, which decides WHICH mass is involved.")]
        [Range(0f, 1f)]
        [SerializeField] float maxStrength = 1f;

        [Header("Geometry residency")]
        [Tooltip("Quads per face axis on the high-poly prism the cradle swaps in: 16 is 3,072 " +
                 "triangles against the authored prism's 24. This is what buys the drape a surface " +
                 "to bend — the effect's two earlier rounds moved the authored triangles and read as " +
                 "facets hinging. Higher is smoother and costs vertices on the resident prisms only.")]
        [Range(2, 32)]
        [SerializeField] int subdivision = 16;

        [Tooltip("Hard ceiling on how many prisms may hold the high-poly mesh at once, per frame, " +
                 "across every riding Urchin. This is the whole performance budget of the feature: " +
                 "at the default subdivision each resident prism is ~3k triangles, so 24 of them is " +
                 "~74k — a handful of prisms, which is what makes the high-poly swap affordable at " +
                 "all. The nearest prisms win.")]
        [Min(0)]
        [SerializeField] int maxResidentPrisms = 24;

        [Tooltip("Extra world units beyond the hull radius plus the drape reach at which a prism " +
                 "becomes resident. It exists so the mesh swap happens strictly OUTSIDE the volume " +
                 "the drape can move anything, which is what makes it invisible: a prism changes " +
                 "geometry only while every one of its vertices is provably unmoved.")]
        [Min(0f)]
        [SerializeField] float residencyMargin = 2f;

        [Header("Continuity")]
        [Tooltip("Seconds the cradle takes to reach full strength after the Urchin attaches. A bare " +
                 "on/off would snap every face in the band into place on one frame — continuity of " +
                 "existence applies to a deformation as much as to mass.")]
        [Min(0f)]
        [SerializeField] float engageSeconds = 0.25f;

        [Tooltip("Seconds the cradle takes to let go after the Urchin detaches or launches. Slightly " +
                 "longer than the engage so a launch reads as the mass releasing the hull rather than " +
                 "the hull tearing free.")]
        [Min(0f)]
        [SerializeField] float releaseSeconds = 0.4f;

        public bool Enabled => enabled;

        /// <summary>How far past the hull's surface the drape reaches, world units.</summary>
        public float DrapeReach => Mathf.Max(0.01f, drapeReach);

        /// <summary>
        /// The falloff's shaping power. Floored at 1 rather than merely clamped positive: below 1
        /// the falloff's derivative diverges at the far edge, which puts a visible crease exactly
        /// where the effect is supposed to vanish without one.
        /// </summary>
        public float DrapeExponent => Mathf.Clamp(drapeExponent, 1f, 6f);

        /// <summary>
        /// The ceiling the eased strength runs to, 0..1. Clamped rather than merely floored: above
        /// 1 the map overshoots — mass slides PAST the hull's surface and out the other side — which
        /// is not a stronger cradle, it is a different (and wrong) one.
        /// </summary>
        public float MaxStrength => Mathf.Clamp01(maxStrength);

        public int Subdivision => Mathf.Clamp(subdivision, 2, 32);
        public int MaxResidentPrisms => Mathf.Max(0, maxResidentPrisms);
        public float ResidencyMargin => Mathf.Max(0f, residencyMargin);
        public float EngageSeconds => Mathf.Max(0f, engageSeconds);
        public float ReleaseSeconds => Mathf.Max(0f, releaseSeconds);

        /// <summary>
        /// The shape the shader can actually run: a positive drape reach. The shader treats
        /// anything else as "off" (its second sentinel), so an insane asset degrades to no cradle
        /// rather than to a division by zero in the falloff.
        /// </summary>
        public bool IsSane => DrapeReach > 0f && DrapeExponent >= 1f;
    }
}
