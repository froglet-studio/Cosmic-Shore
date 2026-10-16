using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for the Rhino's MASS ability, <b>Afterimage</b> (<see cref="RhinoSwordAfterimage"/>):
    /// for a few seconds after the blade LEAVES the energized state, it leaves prisms behind it in
    /// its own shape - afterimages of the sword - and while an omni-crystal surge holds the blade
    /// energized it lays them the whole time. MASS scales their VOLUME; the Mass level-5 upgrade
    /// lays them as DANGER prisms. See <c>R_VesselActions/RHINO_SWORD_AFTERIMAGE.md</c>.
    ///
    /// <para>Shared by every Rhino and stateless: per-vessel state lives on the component.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RhinoSwordAfterimageConfig",
        menuName = "ScriptableObjects/Vessel Actions/Rhino Sword Afterimage Config")]
    public sealed class RhinoSwordAfterimageConfigSO : ScriptableObject
    {
        [Header("Window")]
        [Tooltip("Seconds the blade keeps laying afterimages after it LEAVES the energized state. " +
                 "An omni-crystal surge lays them for its whole length instead (the window then " +
                 "starts again when the surge's energize ends).")]
        [SerializeField, Min(0f)] float windowSeconds = 5f;

        [Tooltip("Shortest gap between two afterimages, seconds - the cadence ceiling.")]
        [SerializeField, Min(0.02f)] float layIntervalSeconds = 0.3f;

        [Tooltip("The blade TIP must have travelled this far (world units) since the last afterimage " +
                 "before another is laid, so a still blade does not stack copies of itself in place.")]
        [SerializeField, Min(0f)] float minTipTravel = 6f;

        [Tooltip("Most afterimages one window may lay - the mass budget per energize.")]
        [SerializeField, Min(1)] int maxAfterimagesPerWindow = 16;

        [Header("Shape (each afterimage is the blade, tiled into segments)")]
        [Tooltip("Longest segment, world units. The blade is cut into the fewest segments no longer " +
                 "than this, capped at Max Segments.")]
        [SerializeField, Min(0.5f)] float segmentLength = 10f;

        [Tooltip("Most segments one afterimage is cut into.")]
        [SerializeField, Min(1)] int maxSegments = 6;

        [Tooltip("Fraction of the blade's hilt-to-tip length an afterimage covers, centred on the blade.")]
        [SerializeField, Range(0.1f, 1f)] float lengthCoverage = 0.9f;

        [Tooltip("Segment width across the blade's broad face, world units, at the resting Mass level.")]
        [SerializeField, Min(0.1f)] float width = 2f;

        [Tooltip("Segment thickness through the blade, world units, at the resting Mass level.")]
        [SerializeField, Min(0.1f)] float thickness = 0.8f;

        [Tooltip("Seconds a fresh afterimage's collider stays off. Every other pilot meets it after " +
                 "this; the Rhino itself never cuts its own afterimages, and its hull ignores its own " +
                 "fresh mass for the fleet-wide self-trail grace (SelfTrailContactConfigSO).")]
        [SerializeField, Min(0f)] float colliderDelaySeconds = 0.1f;

        /// <summary>MASS -> afterimage VOLUME: x1 at rest, x2.5 at level 10, floored at x0.25. Cube-rooted
        /// into every axis, so volume - the conserved quantity - scales linearly with the level (the
        /// VesselPrismController.trailVolume rule). Read at the REPLICATED level: the mass is laid on
        /// every peer and every peer must lay the same mass.</summary>
        [Header("Elements")]
        [SerializeField] ElementalFloat massVolumeMultiplier =
            ElementalFloat.Multiplier(1f, 2.5f, Element.Mass, 0.25f);

        [Tooltip("Element whose level-5 upgrade lays the afterimages as DANGER prisms. Gated on the " +
                 "replicated unlock bit (IsUpgradeActive), snapshotted per afterimage.")]
        [SerializeField] Element dangerUpgradeElement = Element.Mass;

        public float WindowSeconds => windowSeconds;
        public float LayIntervalSeconds => layIntervalSeconds;
        public float MinTipTravel => minTipTravel;
        public int MaxAfterimagesPerWindow => maxAfterimagesPerWindow;
        public float SegmentLength => segmentLength;
        public int MaxSegments => maxSegments;
        public float LengthCoverage => lengthCoverage;
        public float Width => width;
        public float Thickness => thickness;
        public float ColliderDelaySeconds => colliderDelaySeconds;
        public Element DangerUpgradeElement => dangerUpgradeElement;

        public float MassVolumeMultiplier(IVesselStatus status) => massVolumeMultiplier.EvaluateReplicated(status);

        /// <summary>MASS level-5, Danger Afterimages: lay them as danger prisms? Through the replicated
        /// unlock bit, so every peer lays the same kind of mass.</summary>
        public bool DangerActive(IVesselStatus status)
        {
            if (dangerUpgradeElement == Element.None) return false;
            var abilities = status?.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(dangerUpgradeElement);
        }

        /// <summary>
        /// How many segments a blade of <paramref name="coveredLength"/> is cut into, and how long each
        /// is. Pure, so the tiling is testable without a vessel.
        /// </summary>
        public static int SegmentCount(float coveredLength, float segmentLength, int maxSegments) =>
            Mathf.Clamp(Mathf.CeilToInt(coveredLength / Mathf.Max(0.01f, segmentLength)), 1, Mathf.Max(1, maxSegments));
    }
}
