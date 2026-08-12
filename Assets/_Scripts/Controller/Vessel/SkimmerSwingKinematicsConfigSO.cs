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

        [Tooltip("Relative speeds below this read as exactly zero, so a sword that is not being swung imparts precisely the vessel's own velocity - the same thing the hull imparts - instead of a slightly hotter one. Sized to swallow sampling residue, not real motion: a swipe runs 200-500 u/s.")]
        [SerializeField] float restDeadbandSpeed = 1.5f;

        [Header("Terms")]
        [Tooltip("Include the vessel's own rotation carrying the blade around (omega_vessel x r). Real motion: a hard turn genuinely sweeps a 35-unit sword. Off = only the blade's motion relative to the hull counts.")]
        [SerializeField] bool includeVesselRotation = true;

        [Tooltip("Include the radial velocity from the blade lengthening/shortening. Physically real, but OFF by default: the Rhino's blade length is driven by a resource meter (ShieldSkimmerScaleDriver grows at 30 and shrinks at 10 world-units/sec, and its tick loop decays the shield every second), so the blade is almost never static. At the tip that is a permanent +15/-5 u/s on top of a ~35 u/s cruise - the sword would read as hotter than the hull for a reason that has nothing to do with swordsmanship. Turn on only if a shield extension should shove.")]
        [SerializeField] bool includeElongation;

        [Tooltip("Subtract the blade ORIGIN's growth-driven translation before differentiating. The " +
                 "Rhino's sword is hilt-anchored (ShieldSwipeActionExecutor), so its centre slides " +
                 "along the blade axis as the energy meter lengthens it - motion the sampler would " +
                 "otherwise read as a genuine swing at up to the crystal burst's 600 u/s. Leaving it " +
                 "ON keeps the same rule includeElongation encodes: growth is not a strike. Turn it " +
                 "off only for a blade whose transform does not move when it grows.")]
        [SerializeField] bool compensateGrowthTranslation = true;

        public float SmoothingSeconds => smoothingSeconds;
        public float MaxSampleDeltaSeconds => maxSampleDeltaSeconds;
        public float MaxAngularSpeedDegrees => maxAngularSpeedDegrees;
        public float RestDeadbandSpeed => restDeadbandSpeed;
        public bool IncludeVesselRotation => includeVesselRotation;
        public bool IncludeElongation => includeElongation;
        public bool CompensateGrowthTranslation => compensateGrowthTranslation;
    }
}
