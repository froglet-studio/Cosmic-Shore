using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the Urchin's CRADLE (<c>PrismCradle</c>, <c>PrismCradle.hlsl</c>,
    /// Docs/PRISM_ANIMATION.md §4.7.2).
    ///
    /// While an Urchin RIDES a prismscape — attached, not launched, not in free flight — every
    /// prism face inside <see cref="OuterRange"/> of the hull swings so that its normal points
    /// at the hull's centre and its centroid sits ON the hull's surface, by an amount that
    /// ramps from nothing at <see cref="OuterRange"/> to everything at <see cref="InnerRange"/>.
    /// The pilot reads it as the mass they are grinding cradling them.
    ///
    /// Everything here is a GLOBAL shader uniform written once per frame — there is no
    /// per-prism state to tune and no per-prism cost to pay for widening the band. The hull's
    /// RADIUS is not here: it is a property of the vessel (measured once from its hull, or
    /// authored on <c>PrismCradleSource</c>), not of the feel.
    ///
    /// Place the asset at <c>Resources/PrismCradleConfig</c>. With no asset the defaults below
    /// apply, so the feature works out of the box.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismCradleConfig", menuName = "ScriptableObjects/Rendering/Prism Cradle Config")]
    public class PrismCradleConfigSO : ScriptableObject
    {
        [Header("Cradle")]
        [Tooltip("Master switch. Off publishes an empty bank, which makes the shader's very first " +
                 "branch return the untouched vertex — prisms then cost exactly what they cost before " +
                 "this feature existed.")]
        [SerializeField] bool enabled = true;

        [Tooltip("Distance from the hull's centre to a prism FACE's centroid, in world units, at " +
                 "which the cradle is exactly zero. Faces farther than this are untouched.")]
        [Min(0f)]
        [SerializeField] float outerRange = 15f;

        [Tooltip("Distance at (and inside) which the cradle is at full strength: the face's centroid " +
                 "sits on the hull's surface and its normal points at the hull's centre. Between " +
                 "this and the outer range the face does the same thing by a smoothly smaller " +
                 "amount — that gradient is what lets one face hand off to the next at a seam. " +
                 "Clamped below the outer range.")]
        [Min(0f)]
        [SerializeField] float innerRange = 10f;

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
        public float OuterRange => Mathf.Max(0f, outerRange);
        public float InnerRange => Mathf.Clamp(innerRange, 0f, OuterRange);
        public float EngageSeconds => Mathf.Max(0f, engageSeconds);
        public float ReleaseSeconds => Mathf.Max(0f, releaseSeconds);

        /// <summary>
        /// The band the shader can actually run: a positive outer range strictly wider than the
        /// inner one. The shader treats anything else as "off" (its second sentinel), so an insane
        /// asset degrades to no cradle rather than to a division by zero in the smoothstep.
        /// </summary>
        public bool IsSane => OuterRange > 0f && OuterRange > InnerRange;
    }
}
