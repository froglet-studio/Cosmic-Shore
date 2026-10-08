using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every Tether dial, one asset, shared by both triggers and the auto-tethers.
    ///
    /// <b>Units.</b> The long tether was prototyped and tuned in a browser sandbox whose cruise was
    /// 380 u/s. Every SPEED and LENGTH here is that sandbox value × <c>cruise / 380</c> (= 80/380 =
    /// 0.2105 at the shipped cruise), so the swing takes the same TIME and the same number of
    /// cruise-multiples as it did there. Rates in 1/s, angles, fractions and seconds carry over
    /// unchanged. TETHER.md lists the sandbox originals beside each scaled default.
    ///
    /// <b>Stateless</b>, like every vessel action asset: it is read at use time and never written.
    /// </summary>
    [CreateAssetMenu(fileName = "TetherConfig", menuName = "ScriptableObjects/Vessel Actions/Tether Config")]
    public class TetherConfigSO : ScriptableObject
    {
        [Header("Reference")]
        [Tooltip("The cruise every speed and length below was scaled to (u/s) — the hull's neutral-stick speed " +
                 "(DefaultMinimumSpeed + 0.5 × DefaultThrottleScaler on the prefab). Also the speed above which the " +
                 "long tether's hook window starts moving outward. Change it together with the prefab's throttle.")]
        [SerializeField, Min(1f)] float cruiseSpeed = 80f;

        [Header("Search plane")]
        [Tooltip("How far off the plane through the hull's forward and right axes a long-tether target may sit, " +
                 "degrees. Rolling the hull tilts this plane — it is the aiming control.")]
        [SerializeField, Range(1f, 60f)] float planeToleranceDegrees = 20f;

        [Header("Auto-tethers — planting (starting guesses; not prototyped)")]
        [Tooltip("Distance ahead an anchor is planted, as seconds of flight at current speed.")]
        [SerializeField, Min(0.05f)] float anchorLead = 0.8f;
        [Tooltip("Angle off the nose, within the search plane, that an anchor is planted at. Alternates left/right.")]
        [SerializeField, Range(0f, 80f)] float anchorAngle = 20f;
        [Tooltip("Seconds between anchors.")]
        [SerializeField, Min(0.05f)] float anchorPeriod = 0.35f;
        [Tooltip("Size of an anchor prism. Anchors are ordinary trail prisms of this hull's own pool.")]
        [SerializeField] Vector3 anchorScale = new Vector3(3f, 3f, 3f);
        [Tooltip("Below this speed (u/s) no anchors are planted — a stationary hull has nothing to pull toward.")]
        [SerializeField, Min(0f)] float minPlantSpeed = 3f;

        [Header("Auto-tethers — pulling")]
        [Tooltip("The tethers never pull speed past this × the throttle's cruise target. They hold the hull a " +
                 "little ABOVE cruise; they cannot accelerate it forever. Bounds gain only — never brakes.")]
        [SerializeField, Min(1f)] float autoSpeedTarget = 1.15f;
        [Tooltip("Spring stiffness, 1/s². Acceleration = stiffness × stretch past rest. The surge's strength.")]
        [SerializeField, Min(0f)] float autoStiffness = 0.8f;
        [Tooltip("Damping on STRETCHING only, 1/s (closing on the anchor is never damped — that is cruise).")]
        [SerializeField, Min(0f)] float autoDamping = 4f;
        [Tooltip("Rest length as a fraction of the planted distance. Lower = a fresh tether starts more stretched " +
                 "and yanks harder.")]
        [SerializeField, Range(0f, 1f)] float autoRestFraction = 0.5f;
        [Tooltip("How fast momentum re-aligns with the nose while an auto-tether is taut, 1/s. Lower lets the " +
                 "alternating pulls sway the hull visibly (the sideways parts still cancel); higher flies closer " +
                 "to the fleet's snap-to-nose.")]
        [SerializeField, Min(0f)] float autoNoseGrip = 6f;

        [Header("Long tether — hook (sandbox 440 / 130 u, × cruise/380)")]
        [Tooltip("Furthest a long-tether target may be, u, at cruise.")]
        [SerializeField, Min(1f)] float range = 93f;
        [Tooltip("Closer than this is skipped, u, at cruise.")]
        [SerializeField, Min(0f)] float minHook = 27f;
        [Tooltip("How fast the hook window moves outward with speed: scale = 1 + growth × (speed/cruise − 1). " +
                 "Keeps fast arcs readable rather than dizzy.")]
        [SerializeField, Min(0f)] float windowGrowth = 0.5f;
        [Tooltip("Where in [minHook, range] the distance score peaks, 0..1.")]
        [SerializeField, Range(0f, 1f)] float idealDistance01 = 0.5f;
        [Tooltip("Score weight on being out to the side (abeam).")]
        [SerializeField, Min(0f)] float sideWeight = 1f;
        [Tooltip("Score weight on being ahead.")]
        [SerializeField, Min(0f)] float aheadWeight = 0.6f;
        [Tooltip("Score weight on sitting near the ideal distance.")]
        [SerializeField, Min(0f)] float distanceWeight = 0.5f;
        [Tooltip("Score weight on sitting close to the search plane.")]
        [SerializeField, Min(0f)] float planeWeight = 0.3f;
        [Tooltip("Crystals count as long-tether targets as well as prisms.")]
        [SerializeField] bool hookCrystals = true;
        [Tooltip("Seconds between ghost-marker searches while no trigger is held (the held search runs every frame).")]
        [SerializeField, Min(0f)] float ghostSearchInterval = 0.1f;

        [Header("Long tether — swing (sandbox 190 / 35 / 90 u, 1700 u/s)")]
        [Tooltip("Stick-up reel-in rate, u/s. Stick down pays out at the same rate.")]
        [SerializeField, Min(0f)] float reelRate = 40f;
        [Tooltip("Reel-in with no input, u/s.")]
        [SerializeField, Min(0f)] float autoReel = 7.4f;
        [Tooltip("Shortest the line can be reeled to, u.")]
        [SerializeField, Min(0.1f)] float minLength = 19f;
        [Tooltip("Spin limit, revolutions/s. Reeling stops here, so one swing pays ~1x → ~2.2x cruise, not the cap.")]
        [SerializeField, Min(0f)] float maxSpin = 0.6f;
        [Tooltip("Speed cap, u/s (sandbox 1700 ≈ 4.5 × cruise).")]
        [SerializeField, Min(1f)] float maxSpeed = 358f;
        [Tooltip("How fast the nose swings onto the swing's tangent while hooked, 1/s.")]
        [SerializeField, Min(0f)] float noseFollowRate = 18f;

        [Header("Long tether — release")]
        [Tooltip("Boost on release past a half turn, fraction (0.2 = +20%), shrinking as speed nears the cap.")]
        [SerializeField, Min(0f)] float releaseBoost = 0.2f;
        [Tooltip("Seconds after a release during which extra speed fades far more slowly — a quick re-hook carries it.")]
        [SerializeField, Min(0f)] float glide = 1.6f;
        [Tooltip("Overspeed decay multiplier during the glide (0.15 = 15% of the normal decay).")]
        [SerializeField, Range(0f, 1f)] float glideDecayFactor = 0.15f;

        [Header("Cutting — the light sword (Rhino sword rules)")]
        [Tooltip("Radius of the beam's cutting capsule, u.")]
        [SerializeField, Min(0.1f)] float cutRadius = 2.5f;
        [Tooltip("Debris velocity = contact velocity × this (the sword's proportional-debris restitution).")]
        [SerializeField, Min(0f)] float restitution = 1f / 3f;
        [Tooltip("Debris speed limit passed to the slice (the sword ships 200).")]
        [SerializeField, Min(0f)] float debrisSpeedLimit = 200f;
        [Tooltip("Most sub-segments a beam's sweep is sampled at per frame. A long fast beam sweeps a wide quad.")]
        [SerializeField, Range(1, 8)] int maxSweepSamples = 4;

        [Header("Looks — long tether (the hero)")]
        [SerializeField, Min(0f)] float longCoreWidth = 0.5f;
        [SerializeField, Min(0f)] float longHaloWidth = 3.2f;
        [SerializeField, Range(0f, 1f)] float longAlpha = 1f;

        [Header("Looks — auto-tethers (thinner, calmer)")]
        [SerializeField, Min(0f)] float autoCoreWidth = 0.18f;
        [SerializeField, Min(0f)] float autoHaloWidth = 1.3f;
        [SerializeField, Range(0f, 1f)] float autoAlpha = 0.55f;

        [Header("Looks — shared")]
        [Tooltip("Width multiplier at full tension (widths × (1 + gain × tension)).")]
        [SerializeField, Min(0f)] float tensionWidthGain = 0.8f;
        [Tooltip("How far the core is blended from team colour toward white, 0..1.")]
        [SerializeField, Range(0f, 1f)] float coreWhiteness = 0.6f;
        [Tooltip("Halo opacity at rest and at full tension (team colour; channels stay ≤ 1 — bloom clamps at 0.5).")]
        [SerializeField] Vector2 haloAlphaRestToTaut = new Vector2(0.18f, 0.45f);
        [SerializeField, Min(0.01f)] float sparkLifetime = 0.22f;
        [Tooltip("Spark travel along the beam, beam-lengths per second.")]
        [SerializeField, Min(0f)] float sparkSpeed = 1.6f;
        [SerializeField, Min(0f)] float sparkLength = 3f;
        [SerializeField, Min(0.01f)] float bracketFlashSeconds = 0.25f;
        [SerializeField, Min(0f)] float bracketRadius = 2.4f;
        [SerializeField, Min(0.01f)] float retractSeconds = 0.14f;
        [SerializeField, Min(0f)] float ghostRadius = 3f;
        [Tooltip("The release line shows where a release throws you: exit speed × this many seconds.")]
        [SerializeField, Min(0f)] float releaseLineSeconds = 0.6f;
        [SerializeField] Color halfTurnColor = new Color(1f, 0.78f, 0.25f, 1f);

        [Header("Feel — hook snap")]
        [Tooltip("Camera shake at a full yank (scaled by how much velocity the hook redirected).")]
        [SerializeField, Min(0f)] float hookShakeIntensity = 0.6f;
        [SerializeField, Min(0f)] float hookShakeDuration = 0.18f;
        [Tooltip("Haptic strength at a full yank (HapticController.PlayBind). A hook never feels weaker than 30% of it.")]
        [SerializeField, Range(0f, 1f)] float hookHapticStrength = 0.8f;
        [SerializeField, Min(0.01f)] float hookFlashSeconds = 0.2f;

        // ------------------------------------------------------------------ accessors

        public float CruiseSpeed => cruiseSpeed;
        public float PlaneToleranceDegrees => planeToleranceDegrees;

        public float AnchorLead => anchorLead;
        public float AnchorAngle => anchorAngle;
        public float AnchorPeriod => anchorPeriod;
        public Vector3 AnchorScale => anchorScale;
        public float MinPlantSpeed => minPlantSpeed;
        public float AutoSpeedTarget => autoSpeedTarget;
        public float AutoStiffness => autoStiffness;
        public float AutoDamping => autoDamping;
        public float AutoRestFraction => autoRestFraction;
        public float AutoNoseGrip => autoNoseGrip;

        public float HookRange => range;
        public float MinHook => minHook;
        public float WindowGrowth => windowGrowth;
        public bool HookCrystals => hookCrystals;
        public float GhostSearchInterval => ghostSearchInterval;

        public float ReelRate => reelRate;
        public float AutoReel => autoReel;
        public float MinLength => minLength;
        public float MaxSpinRevPerSec => maxSpin;
        public float MaxSpeed => maxSpeed;
        public float NoseFollowRate => noseFollowRate;

        public float ReleaseBoost => releaseBoost;
        public float Glide => glide;
        public float GlideDecayFactor => glideDecayFactor;

        public float CutRadius => cutRadius;
        public float Restitution => restitution;
        public float DebrisSpeedLimit => debrisSpeedLimit;
        public int MaxSweepSamples => maxSweepSamples;

        public float LongCoreWidth => longCoreWidth;
        public float LongHaloWidth => longHaloWidth;
        public float LongAlpha => longAlpha;
        public float AutoCoreWidth => autoCoreWidth;
        public float AutoHaloWidth => autoHaloWidth;
        public float AutoAlpha => autoAlpha;
        public float TensionWidthGain => tensionWidthGain;
        public float CoreWhiteness => coreWhiteness;
        public Vector2 HaloAlphaRestToTaut => haloAlphaRestToTaut;
        public float SparkLifetime => sparkLifetime;
        public float SparkSpeed => sparkSpeed;
        public float SparkLength => sparkLength;
        public float BracketFlashSeconds => bracketFlashSeconds;
        public float BracketRadius => bracketRadius;
        public float RetractSeconds => retractSeconds;
        public float GhostRadius => ghostRadius;
        public float ReleaseLineSeconds => releaseLineSeconds;
        public Color HalfTurnColor => halfTurnColor;

        public float HookShakeIntensity => hookShakeIntensity;
        public float HookShakeDuration => hookShakeDuration;
        public float HookHapticStrength => hookHapticStrength;
        public float HookFlashSeconds => hookFlashSeconds;

        /// <summary>The long tether's hook window at <paramref name="speed"/> — the outward move
        /// with speed applied.</summary>
        public TetherMath.HookWindow HookWindowAt(float speed)
        {
            float scale = TetherMath.WindowScale(speed, cruiseSpeed, windowGrowth);
            return new TetherMath.HookWindow
            {
                MinDistance = minHook * scale,
                MaxDistance = range * scale,
                PlaneToleranceDegrees = planeToleranceDegrees,
                IdealDistance01 = idealDistance01,
                SideWeight = sideWeight,
                AheadWeight = aheadWeight,
                DistanceWeight = distanceWeight,
                PlaneWeight = planeWeight,
            };
        }

        /// <summary>The swing's per-step numbers, with the line allowed to pay out to the hook
        /// window's current reach.</summary>
        public LongTetherTuning SwingTuningAt(float speed) => new LongTetherTuning
        {
            ReelRate = reelRate,
            AutoReel = autoReel,
            MinLength = minLength,
            MaxLength = range * TetherMath.WindowScale(speed, cruiseSpeed, windowGrowth),
            MaxSpinRadPerSec = maxSpin * 2f * Mathf.PI,
            MaxSpeed = maxSpeed,
        };
    }
}
