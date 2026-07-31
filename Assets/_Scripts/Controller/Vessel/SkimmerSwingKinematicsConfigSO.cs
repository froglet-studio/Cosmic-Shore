using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shared tuning for <see cref="SkimmerSwingKinematics"/> - the rigid-body velocity
    /// model of a swinging skimmer (the Rhino's sword). Everything here is numerical
    /// feel/stability; the blade's structure (which local axis is its length, which
    /// transform is the pivot) is authored per-prefab on the component itself, and the
    /// gameplay dial that decides how much of the swing reaches a prism lives on the
    /// impact effect SO next to its inertia.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SkimmerSwingKinematicsConfig",
        menuName = "ScriptableObjects/Vessel Actions/SkimmerSwingKinematicsConfigSO")]
    public class SkimmerSwingKinematicsConfigSO : ScriptableObject
    {
        [Header("Sampling")]
        [Tooltip("Time constant for smoothing the differentiated rates. The pose is written every frame by the swipe executor, so this only filters frame-to-frame dt jitter - keep it tiny or the tip lags the swing it is supposed to describe.")]
        [SerializeField] float smoothingSeconds = 0.03f;

        [Tooltip("Frames longer than this are treated as a hitch and skipped instead of differentiated - a 250ms stall would otherwise read as a near-static sword and wipe the swing.")]
        [SerializeField] float maxSampleDeltaSeconds = 0.1f;

        [Header("Clamps (numerical safety, not gameplay tuning)")]
        [Tooltip("Ceiling on the blade's angular speed relative to the vessel. Guards against a single bad frame turning into an absurd tip velocity. 0 = unclamped.")]
        [SerializeField] float maxAngularSpeedDegrees = 3600f;

        [Header("Terms")]
        [Tooltip("Include the vessel's own rotation carrying the blade around (omega_vessel x r). Real motion: a hard turn genuinely sweeps a 35-unit sword. Off = only the blade's motion relative to the hull counts.")]
        [SerializeField] bool includeVesselRotation = true;

        [Tooltip("Include the radial velocity from the blade lengthening/shortening (the shield growth driver). A growing sword really does drive its tip outward.")]
        [SerializeField] bool includeElongation = true;

        public float SmoothingSeconds => smoothingSeconds;
        public float MaxSampleDeltaSeconds => maxSampleDeltaSeconds;
        public float MaxAngularSpeedDegrees => maxAngularSpeedDegrees;
        public bool IncludeVesselRotation => includeVesselRotation;
        public bool IncludeElongation => includeElongation;
    }
}
