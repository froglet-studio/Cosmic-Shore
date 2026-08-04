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

        [Tooltip("World-space radius of the camera→vessel capsule. A prism farther than this from " +
                 "the segment is never touched. The retired ClearPrisms capsule used 20.")]
        [Min(0f)]
        [SerializeField] float outerRadius = 18f;

        [Tooltip("Inside this radius the fade is at its floor. Between inner and outer the fade " +
                 "smoothsteps back to fully opaque, so the corridor has a soft edge instead of a " +
                 "hard cylinder.")]
        [Min(0f)]
        [SerializeField] float innerRadius = 5f;

        [Tooltip("Alpha at the corridor core. 0 removes the prism completely; a small value leaves " +
                 "a faint dithered ghost so the player can still read that mass is there.")]
        [Range(0f, 1f)]
        [SerializeField] float coreAlpha = 0.05f;

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
