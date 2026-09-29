using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The feel of an elemental crystal CAPTURE - the beat between "a skimmer touched it" and
    /// "it is gone into the hull". One asset for the whole game (Resources/CrystalCaptureConfig),
    /// per the project's config-separation rule: a per-prefab duration is exactly how the old
    /// capture drifted to 1s on some lifeforms and 3s on others.
    ///
    /// The capture runs in three phases, and the phase boundaries are what make it read as a
    /// grab rather than a drag:
    ///
    ///   1. SNATCH  - the crystal pops up in scale and kicks AWAY from the vessel. Anticipation:
    ///                the recoil is what sells the pull that follows.
    ///   2. SUCTION - it homes on the vessel's LIVE position, accelerating (never linear), swinging
    ///                in on an arc, spinning up, shrinking, and flaring brighter as it closes.
    ///   3. ABSORB  - it collapses into the hull and dissolves out (continuity of existence - a
    ///                crystal never simply stops existing), and the element's spent-crystal husk
    ///                sprays into the vessel's wake as the payoff.
    ///
    /// Total is well under a second by design. The reward (the element level) already landed at
    /// contact; this is the flourish that tells the pilot it landed, and a flourish that outlasts
    /// its own payoff reads as lag.
    /// </summary>
    [CreateAssetMenu(fileName = "CrystalCaptureConfig", menuName = "ScriptableObjects/" + nameof(CrystalCaptureConfigSO))]
    public class CrystalCaptureConfigSO : ScriptableObject
    {
        public const string ResourcePath = "CrystalCaptureConfig";

        /// <summary>Which beat of the capture a given elapsed time falls in.</summary>
        public enum Phase { Snatch = 0, Suction = 1, Absorb = 2, Done = 3 }

        [Header("Timing (seconds)")]
        [Tooltip("The grab: crystal pops in scale and recoils away from the vessel.")]
        [SerializeField, Min(0f)] float snatchDuration = 0.08f;

        [Tooltip("The flight: crystal homes on the vessel, accelerating the whole way.")]
        [SerializeField, Min(0.01f)] float suctionDuration = 0.26f;

        [Tooltip("The landing: crystal collapses into the hull and dissolves out.")]
        [SerializeField, Min(0f)] float absorbDuration = 0.1f;

        [Header("Snatch - the grab")]
        [Tooltip("Peak scale multiplier at the top of the recoil.")]
        [SerializeField, Min(1f)] float snatchScale = 1.5f;

        [Tooltip("How far the crystal kicks AWAY from the vessel, in crystal radii. " +
                 "Sized off the crystal so a grown flora heart and a tiny drop recoil alike.")]
        [SerializeField, Min(0f)] float snatchRecoilRadii = 0.9f;

        [Header("Suction - the flight")]
        [Tooltip("Acceleration exponent for the flight. 1 = a linear drag (the old look), " +
                 "higher = the crystal hangs, then snaps in.")]
        [SerializeField, Min(1f)] float suctionAcceleration = 2.6f;

        [Tooltip("Lateral swing at the middle of the flight, in crystal radii. 0 = a straight line.")]
        [SerializeField, Min(0f)] float suctionArcRadii = 1.6f;

        [Tooltip("Scale multiplier the crystal has shrunk to by the time it reaches the hull.")]
        [SerializeField, Range(0.05f, 1.5f)] float suctionEndScale = 0.55f;

        [Tooltip("Full rotations the crystal tumbles through over snatch + suction.")]
        [SerializeField, Min(0f)] float spinRevolutions = 2.25f;

        [Header("Flare")]
        [Tooltip("Brightness multiplier the crystal's colours reach at the hull. The element's HUE " +
                 "is preserved - this is a brightness flare, never a wash toward white " +
                 "(Docs/PALETTE.md: scaling a colour pair changes brightness, not contrast).")]
        [SerializeField, Min(1f)] float flareGain = 3f;

        [Header("Payoff")]
        [Tooltip("Spray the element's spent-crystal husk into the vessel's wake on arrival. " +
                 "This is the same burst an omni crystal pickup plays, and it carries the " +
                 "CrystalCollect SFX with it.")]
        [SerializeField] bool spawnHuskBurst = true;

        public float SnatchDuration => snatchDuration;
        public float SuctionDuration => suctionDuration;
        public float AbsorbDuration => absorbDuration;
        public float SnatchScale => snatchScale;
        public float SnatchRecoilRadii => snatchRecoilRadii;
        public float SuctionAcceleration => suctionAcceleration;
        public float SuctionArcRadii => suctionArcRadii;
        public float SuctionEndScale => suctionEndScale;
        public float SpinRevolutions => spinRevolutions;
        public float FlareGain => flareGain;
        public bool SpawnHuskBurst => spawnHuskBurst;

        /// <summary>Total wall-clock length of a capture.</summary>
        public float TotalDuration => snatchDuration + suctionDuration + absorbDuration;

        /// <summary>
        /// Resolves an elapsed capture time into its phase and that phase's normalized progress.
        /// Pure, so the sequence can be validated without play mode.
        /// </summary>
        public Phase ResolvePhase(float elapsed, out float phaseProgress01)
        {
            if (elapsed < snatchDuration)
            {
                phaseProgress01 = snatchDuration > 0f ? Mathf.Clamp01(elapsed / snatchDuration) : 1f;
                return Phase.Snatch;
            }

            elapsed -= snatchDuration;
            if (elapsed < suctionDuration)
            {
                phaseProgress01 = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, suctionDuration));
                return Phase.Suction;
            }

            elapsed -= suctionDuration;
            if (elapsed < absorbDuration)
            {
                phaseProgress01 = absorbDuration > 0f ? Mathf.Clamp01(elapsed / absorbDuration) : 1f;
                return Phase.Absorb;
            }

            phaseProgress01 = 1f;
            return Phase.Done;
        }

        /// <summary>
        /// Scale multiplier at a point in the capture: 1 → <see cref="SnatchScale"/> over the
        /// snatch (easing out, so the pop lands hard), down to <see cref="SuctionEndScale"/> over
        /// the flight, then to 0 through the absorb.
        /// </summary>
        public float ScaleMultiplier(Phase phase, float u) => phase switch
        {
            Phase.Snatch => Mathf.LerpUnclamped(1f, snatchScale, EaseOut(u)),
            Phase.Suction => Mathf.LerpUnclamped(snatchScale, suctionEndScale, EaseInOut(u)),
            Phase.Absorb => Mathf.LerpUnclamped(suctionEndScale, 0f, EaseIn(u)),
            _ => 0f,
        };

        /// <summary>
        /// How far along the vessel-ward flight the crystal is, 0 at the recoil anchor and 1 at
        /// the hull. Accelerating by construction - a linear ramp against a moving vessel is what
        /// made the old capture read as the crystal being dragged rather than pulled.
        /// </summary>
        public float FlightProgress01(float u) => Mathf.Pow(Mathf.Clamp01(u), suctionAcceleration);

        /// <summary>How far along the recoil the crystal is, 0 at the pickup point, 1 at the anchor.</summary>
        public static float SnatchProgress01(float u) => EaseInOut(u);

        /// <summary>Lateral arc offset over the flight, peaking at the midpoint and closing to 0.</summary>
        public static float ArcOffset01(float u) => Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);

        /// <summary>Cumulative spin, in degrees, at a point in the capture.</summary>
        public float SpinDegrees(Phase phase, float u)
        {
            float total = spinRevolutions * 360f;
            return phase switch
            {
                Phase.Snatch => total * 0.15f * EaseOut(u),
                Phase.Suction => total * Mathf.LerpUnclamped(0.15f, 1f, EaseIn(u)),
                _ => total,
            };
        }

        /// <summary>Brightness multiplier for the crystal's colours at a point in the capture.</summary>
        public float FlareMultiplier(Phase phase, float u) => phase switch
        {
            Phase.Snatch => Mathf.LerpUnclamped(1f, 1f + (flareGain - 1f) * 0.35f, EaseOut(u)),
            Phase.Suction => Mathf.LerpUnclamped(1f + (flareGain - 1f) * 0.35f, flareGain, EaseIn(u)),
            Phase.Absorb => flareGain,
            _ => 1f,
        };

        /// <summary>Opacity at a point in the capture - only the absorb dissolves it out.</summary>
        public static float Opacity(Phase phase, float u) =>
            phase == Phase.Absorb ? 1f - EaseIn(u) : phase == Phase.Done ? 0f : 1f;

        static float EaseIn(float u) { u = Mathf.Clamp01(u); return u * u; }
        static float EaseOut(float u) { u = Mathf.Clamp01(u); return 1f - (1f - u) * (1f - u); }
        static float EaseInOut(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }

        static CrystalCaptureConfigSO _cached;

        /// <summary>
        /// Loads (and caches) the project's capture config. Falls back to a defaults instance so a
        /// crystal captured in a scene with no asset still plays the effect rather than hanging at
        /// full scale forever - this runs on every lifeform death, so it must never be a hard fail.
        /// </summary>
        public static CrystalCaptureConfigSO Load()
        {
            if (_cached) return _cached;

            _cached = Resources.Load<CrystalCaptureConfigSO>(ResourcePath);
            if (!_cached)
            {
                // Loud, but once - the defaults ARE the shipped values, so a missing asset changes
                // nothing about how a capture looks. What it does mean is that nobody can TUNE it,
                // which is invisible until someone edits the asset and sees no effect.
                CSDebug.LogWarning($"[Scurry] Resources/{ResourcePath} is missing - captures " +
                    "will run on built-in defaults and the asset's values will not apply. Restore it, " +
                    $"or create one via Assets > Create > ScriptableObjects > {nameof(CrystalCaptureConfigSO)}.");
                _cached = CreateInstance<CrystalCaptureConfigSO>();
            }
            return _cached;
        }
    }
}
