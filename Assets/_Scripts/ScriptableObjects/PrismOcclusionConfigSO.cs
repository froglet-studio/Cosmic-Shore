using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the camera↔vessel prism occlusion corridor
    /// (<c>PrismOcclusionCorridor</c>, <c>PrismOcclusionCorridor.hlsl</c>,
    /// Docs/PRISM_ANIMATION.md §5 C1).
    ///
    /// Prisms inside the capsule swept from the player's camera to the player's vessel
    /// dissolve so the ship is never hidden by its own trail or by the environment.
    /// Everything here is a GLOBAL shader uniform written once per frame — there is no
    /// per-prism state to tune, and no per-prism cost to pay for widening the corridor.
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

        [Tooltip("World-space radius at which the corridor is fully opaque again. A prism farther " +
                 "than this from the camera→vessel segment is never touched. Keep it close to " +
                 "innerRadius: the band between them is the only mass ever in transition, and a " +
                 "SHORT band is what makes the world snap back to opaque as you move off.")]
        [Min(0f)]
        [SerializeField] float outerRadius = 13f;

        [Tooltip("Radius of the FULLY CLEAR core — inside it the fade sits at coreAlpha (0 by " +
                 "default, i.e. gone). Make it comfortably wider than the ship so the vessel is " +
                 "never inside the gradient itself. Between inner and outer the fade eases back to " +
                 "opaque on a C2-continuous quintic.")]
        [Min(0f)]
        [SerializeField] float innerRadius = 9f;

        [Tooltip("Alpha at the corridor core. 0 (the default) tapers fully to nothing, so no " +
                 "dithered ghost survives anywhere the ship can be. A small positive value leaves a " +
                 "faint speckle instead, if reading 'there is mass here' ever matters more than " +
                 "reading the ship.")]
        [Range(0f, 1f)]
        [SerializeField] float coreAlpha = 0f;

        public bool Enabled => enabled;
        public float OuterRadius => outerRadius;

        /// <summary>Clamped so a mis-authored asset can never invert the feather.</summary>
        public float InnerRadius => Mathf.Min(innerRadius, outerRadius);

        public float CoreAlpha => coreAlpha;

        /// <summary>
        /// The packed <c>_PrismOcclusionParams</c> uniform: (outerRadius, innerRadius, coreAlpha).
        /// A non-positive x is the shader's "corridor off" sentinel.
        /// </summary>
        public Vector4 PackedParams => enabled
            ? new Vector4(outerRadius, InnerRadius, coreAlpha, 0f)
            : Vector4.zero;
    }
}
