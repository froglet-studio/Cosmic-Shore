using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for the SUPER-SHIELD DEFLECTION JIGGLE — the wobble a super-shielded prism
    /// runs when it is HIT but not destroyed (<c>Prism.AbsorbSuperShieldHit</c> →
    /// <c>PrismSuperShieldJiggle.Stamp</c> → <c>PrismJiggleClock</c> in
    /// <c>PrismClockAnimation.hlsl</c>). Docs/PRISM_ANIMATION.md §5 C14.
    ///
    /// Super-shielded mass is fully invulnerable, so before this every hit on it was
    /// visually silent — sparks fired, the mass did not move, and a deflection read as a
    /// miss. This is the deflection made legible, and it changes NOTHING about gameplay:
    /// the prism stays invulnerable, its collider, volume, spatial registration, domain and
    /// state flags are untouched, and only photons move.
    ///
    /// It is a clock-material animation in the strict sense (Docs/PRISM_ANIMATION.md §1):
    /// ONE stamp of initial conditions at the hit, the GPU runs the whole wobble from the
    /// shader clock, and ONE scheduled clear at the analytically-known end. Everything here
    /// is stamped per hit, which is why it lives in a config asset rather than in per-material
    /// shader constants — the wobble's FEEL is designer-owned, its curve shape is not.
    ///
    /// Place the asset at <c>Resources/PrismSuperShieldJiggleConfig</c>. With no asset the
    /// defaults below apply, so the effect works out of the box.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismSuperShieldJiggleConfig",
                     menuName = "ScriptableObjects/Rendering/Prism Super-Shield Jiggle Config")]
    public class PrismSuperShieldJiggleConfigSO : ScriptableObject
    {
        [Header("Deflection")]
        [Tooltip("Master switch. Off stamps nothing, so a super-shielded hit costs exactly what " +
                 "it cost before this feature existed (the early-return, and nothing else).")]
        [SerializeField] bool enabled = true;

        [Tooltip("How long one deflection wobbles before it has settled completely. The shader's " +
                 "envelope reaches EXACTLY zero here, so the scheduled stamp-clear is invisible.")]
        [Min(0.05f)]
        [SerializeField] float duration = 0.55f;

        [Header("Amplitude (peak face tilt, degrees)")]
        [Tooltip("Peak tilt for the WEAKEST hit that still registers. Applied to each face about " +
                 "the prism's own origin, so the super-shield stella's outer spikes wag far while " +
                 "the core barely moves — a few degrees is a lot of motion at the tips.")]
        [Range(0f, 30f)]
        [SerializeField] float minTiltDegrees = 2.5f;

        [Tooltip("Peak tilt at (and above) referenceImpactSpeed. Hits harder than that do not " +
                 "wobble harder — a ceiling, so a shotgun blast cannot turn a track lining inside out.")]
        [Range(0f, 30f)]
        [SerializeField] float maxTiltDegrees = 6.5f;

        [Tooltip("Impact speed (world units/second) at which the tilt reaches maxTiltDegrees. " +
                 "Hits arrive from bullets, hulls, swords and blast waves at wildly different " +
                 "magnitudes, so the mapping is normalised rather than absolute.")]
        [Min(1f)]
        [SerializeField] float referenceImpactSpeed = 120f;

        [Header("Wobble character")]
        [Tooltip("PRECESSION: how fast each face's rotation axis sweeps around that face's own " +
                 "normal, in degrees/second. This is the direction the face tips toward, revolving.")]
        [Min(0f)]
        [SerializeField] float precessionDegreesPerSecond = 1260f;

        [Tooltip("NUTATION: how fast the axis' cone half-angle breathes, in degrees/second. The " +
                 "face alternates between an in-plane twist (axis on the normal) and a maximum tip " +
                 "(axis in the face plane) at this rate. Deliberately NOT a whole-number multiple " +
                 "of the precession rate, so the wobble never repeats inside one deflection.")]
        [Min(0f)]
        [SerializeField] float nutationDegreesPerSecond = 1776f;

        [Header("Spam control")]
        [Tooltip("Minimum seconds between two stamps on the SAME prism. A swept piercing " +
                 "projectile re-dispatches the same prism every frame it overlaps, a drone swarm " +
                 "re-queues its meal every behaviour tick, and N concurrent blasts each hit " +
                 "independently — without this, a prism under fire re-stamps dozens of times per " +
                 "second and never gets past the envelope's opening frame, so it reads as frozen " +
                 "mid-wobble instead of wobbling. 0 re-stamps on every hit.")]
        [Min(0f)]
        [SerializeField] float minSecondsBetweenStamps = 0.12f;

        public bool Enabled => enabled;
        public float Duration => Mathf.Max(0.05f, duration);
        public float MinSecondsBetweenStamps => Mathf.Max(0f, minSecondsBetweenStamps);

        /// <summary>Peak tilt in RADIANS for an impact of the given world speed, clamped into
        /// [minTilt, maxTilt]. A non-positive or unknown speed takes the minimum — a deflection
        /// always reads, even when the hit site could not describe its own magnitude.</summary>
        public float PeakTiltRadians(float impactSpeed)
        {
            float lo = Mathf.Min(minTiltDegrees, maxTiltDegrees);
            float hi = Mathf.Max(minTiltDegrees, maxTiltDegrees);
            float t = impactSpeed > 0f ? Mathf.Clamp01(impactSpeed / Mathf.Max(1f, referenceImpactSpeed)) : 0f;
            return Mathf.Lerp(lo, hi, t) * Mathf.Deg2Rad;
        }

        /// <summary>
        /// The packed <c>_JiggleParams</c> per-instance value for one deflection:
        /// (peak tilt radians, precession rad/s, nutation rad/s).
        /// </summary>
        public Vector3 PackParams(float impactSpeed) => new Vector3(
            PeakTiltRadians(impactSpeed),
            precessionDegreesPerSecond * Mathf.Deg2Rad,
            nutationDegreesPerSecond * Mathf.Deg2Rad);

        /// <summary>Worst-case tilt, for sizing the culling envelope at stamp time. Bounds are
        /// gameplay-free, so they are sized from the CEILING rather than from this hit.</summary>
        public float MaxTiltRadians => Mathf.Max(minTiltDegrees, maxTiltDegrees) * Mathf.Deg2Rad;
    }
}
