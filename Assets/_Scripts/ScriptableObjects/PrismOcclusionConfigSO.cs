using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the camera↔vessel prism occlusion corridor
    /// (<c>PrismOcclusionCorridor</c>, <c>PrismOcclusionCorridor.hlsl</c>,
    /// Docs/PRISM_ANIMATION.md §5 C1).
    ///
    /// Prisms inside the BARE CONE from the player's camera to the player's vessel dissolve
    /// so the ship is never hidden by its own trail or by the environment. The cone is a
    /// point at the lens, reaches these radii only at the hull, and ends flat at the
    /// vessel's plane — so the cleared region is a constant angular size (the ship's
    /// silhouette) at every depth, and nothing level with or behind the ship is touched.
    /// Everything here is a GLOBAL shader uniform written once per frame — there is no
    /// per-prism state to tune, and no per-prism cost to pay for widening the corridor.
    ///
    /// The radii are NOT authored in world units: they are multiples of the vessel's own
    /// circumscribing radius, measured from its hull at bind time
    /// (<c>PrismOcclusionCorridor.MeasureCircumscribedRadius</c>). The corridor is therefore
    /// ship-sized by construction — a new vessel of any size gets a correctly-scaled corridor
    /// with nothing to author, which is the same "no per-vessel wiring" property the rest of
    /// the platform law rests on.
    ///
    /// Place the asset at <c>Resources/PrismOcclusionConfig</c>. With no asset the
    /// defaults below apply, so the feature works out of the box.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismOcclusionConfig", menuName = "ScriptableObjects/Rendering/Prism Occlusion Config")]
    public class PrismOcclusionConfigSO : ScriptableObject
    {
        [Header("Corridor")]
        [Tooltip("Master switch. Off publishes a zero radius, which makes the shader's very first " +
                 "branch return the untouched alpha — prisms then cost exactly what they cost before " +
                 "this feature existed.")]
        [SerializeField] bool enabled = true;

        [Tooltip("Outer edge of the gradient AT THE VESSEL, as a multiple of the VESSEL'S OWN " +
                 "circumscribing radius (the sphere that encloses its hull). 1 puts the fully-opaque " +
                 "edge exactly on that circle. The radius tapers to zero at the camera, so this is " +
                 "the widest the cone ever gets — the corridor is ship-sized, so a big hull opens a " +
                 "big corridor and a small one a small corridor, with no per-vessel authoring.")]
        [Min(0f)]
        [SerializeField] float outerRadiusScale = 1f;

        [Tooltip("Inner edge of the gradient — the fully-clear core — as a multiple of the same " +
                 "circumscribing radius, tapering with the outer cone. Deliberately much narrower " +
                 "than the outer edge: at 0.25 three quarters of the cone's cross-section is " +
                 "gradient, so the dissolve reads as a soft column with a small solid-clear centre " +
                 "rather than a hard hole with a rim. 0 makes the whole cone a gradient, clear only " +
                 "on the axis itself.")]
        [Range(0f, 1f)]
        [SerializeField] float innerRadiusScale = 0.25f;

        [Tooltip("Alpha at the corridor core. 0 (the default) tapers fully to nothing, so no " +
                 "dithered ghost survives anywhere the ship can be. A small positive value leaves a " +
                 "faint speckle instead, if reading 'there is mass here' ever matters more than " +
                 "reading the ship.")]
        [Range(0f, 1f)]
        [SerializeField] float coreAlpha = 0f;

        [Header("Sanity band (world units)")]
        [Tooltip("World-space radius used when a vessel's hull cannot be measured at all (no mesh " +
                 "renderers). Never expected — PrismOcclusionDiagnostics screams if it is used — " +
                 "but the corridor is a platform law, so it degrades to a sane corridor rather " +
                 "than silently switching off.")]
        [Min(0f)]
        [SerializeField] float fallbackVesselRadius = 6f;

        [Tooltip("Measured hull radii are clamped into [min, max] and the clamp screams once. " +
                 "Since the corridor is now sized ENTIRELY by the measurement, a bad one is not " +
                 "cosmetic: too small and the ship is hidden anyway, too large and the world " +
                 "dissolves in front of you. The clamp turns either into a visible, named error.")]
        [Min(0f)]
        [SerializeField] float minVesselRadius = 2f;

        [Min(0f)]
        [SerializeField] float maxVesselRadius = 80f;

        public bool Enabled => enabled;
        public float OuterRadiusScale => outerRadiusScale;

        /// <summary>Clamped so a mis-authored asset can never invert the feather.</summary>
        public float InnerRadiusScale => Mathf.Min(innerRadiusScale, outerRadiusScale);

        public float CoreAlpha => coreAlpha;
        public float FallbackVesselRadius => fallbackVesselRadius;
        public float MinVesselRadius => minVesselRadius;
        public float MaxVesselRadius => Mathf.Max(maxVesselRadius, minVesselRadius);

        /// <summary>
        /// The packed <c>_PrismOcclusionParams</c> uniform for a vessel whose circumscribing
        /// radius is <paramref name="vesselRadius"/>: (outerRadius, innerRadius, coreAlpha),
        /// all in world units. A non-positive x is the shader's "corridor off" sentinel.
        /// </summary>
        public Vector4 PackParams(float vesselRadius)
        {
            if (!enabled || vesselRadius <= 0f)
                return Vector4.zero;
            return new Vector4(vesselRadius * outerRadiusScale,
                               vesselRadius * InnerRadiusScale,
                               coreAlpha, 0f);
        }
    }
}
