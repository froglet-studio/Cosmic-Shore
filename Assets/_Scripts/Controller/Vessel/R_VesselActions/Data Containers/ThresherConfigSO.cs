using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every dial of the Thresher: the chain physics (embedded <see cref="ThresherDials"/>, in
    /// sandbox units and scaled by its own <c>gameCruise / sandboxCruise</c>), the four element
    /// scalings, the smash explosion, the chain cut, the impact feel, and the look. One asset per
    /// hull; read by <see cref="ThresherExecutor"/>. See <c>THRESHER.md</c>.
    ///
    /// Element map (approved 2026-10-08): CHARGE = the ball's threat (explosion size; L5 "Lit"),
    /// MASS = the ball's weight and size (L5 "Wrecker"), SPACE = the chain's reach (L5 "Reaper
    /// Chain"), TIME = the plant's spin (L5 "Slingshot"). Every scaling is an
    /// <see cref="ElementalFloat"/> read through <c>EvaluateLive</c> at the point of use.
    /// </summary>
    [CreateAssetMenu(fileName = "ThresherConfig", menuName = "ScriptableObjects/Vessel Actions/Thresher Config")]
    public sealed class ThresherConfigSO : ScriptableObject
    {
        [Tooltip("The chain physics, authored in the 2D sandbox's units. See ThresherDials.")]
        [SerializeField] ThresherDials dials = new ThresherDials();

        [Header("Elements")]
        [Tooltip("CHARGE: multiplier on the smash explosion's diameter.")]
        [SerializeField] ElementalFloat explosionSizeMultiplier = ElementalFloat.Multiplier(1f, 2f, Element.Charge, 0.5f);
        [Tooltip("MASS: multiplier on the ball's mass (heavier tug, the same plough). The ball's radius " +
                 "grows with the CUBE ROOT of it, because Mass is volume.")]
        [SerializeField] ElementalFloat ballMassMultiplier = ElementalFloat.Multiplier(1f, 1.75f, Element.Mass, 0.5f);
        [Tooltip("SPACE: multiplier on the let-out chain length (maxLen). The reeled-in length is fixed.")]
        [SerializeField] ElementalFloat chainReachMultiplier = ElementalFloat.Multiplier(1f, 1.5f, Element.Space, 0.6f);
        [Tooltip("TIME: multiplier on the planted orbit's spin-up rate AND its cap (lockSpin, lockMax).")]
        [SerializeField] ElementalFloat spinMultiplier = ElementalFloat.Multiplier(1f, 1.75f, Element.Time, 0.5f);

        [Header("Smash explosion (ball at or above smash speed)")]
        [Tooltip("Explosion prefab(s) spawned where a smashing ball hits. Destroys every domain's prisms " +
                 "(AffectSelfOverride true) until the Charge L5 'Lit' is active, when it spares the pilot's own " +
                 "domain (AffectSelfOverride false) - the fleet's domain-sparing-in-the-explosion-layer rule.")]
        [SerializeField] AOEExplosion[] smashExplosions;
        [Tooltip("Explosion DIAMETER at rest Charge (world units); x explosionSizeMultiplier.")]
        [SerializeField, Min(0f)] float explosionDiameter = 16f;
        [Tooltip("At most one explosion per this many seconds, so a hot ball ploughing a row makes a " +
                 "string of blasts rather than one per prism.")]
        [SerializeField, Min(0f)] float explosionMinInterval = 0.08f;

        [Header("Chain cut")]
        [Tooltip("The chain's cutting half-width (world units). It cuts RIVAL prisms it touches while taut; " +
                 "never its own domain's.")]
        [SerializeField, Min(0f)] float chainCutRadius = 0.6f;
        [Tooltip("Debris velocity = the chain point's own velocity x this.")]
        [SerializeField, Min(0f)] float chainDebrisRestitution = 0.5f;

        [Header("Impact - hit-stop (one per whip, on its first smash)")]
        [Tooltip("Hit-stop for a ball that only just reached smash speed (seconds).")]
        [SerializeField, Min(0f)] float hitStopMinSeconds = 0.03f;
        [Tooltip("Hit-stop for a white-hot ball (seconds).")]
        [SerializeField, Min(0f)] float hitStopMaxSeconds = 0.09f;
        [Tooltip("Time scale during a SOLO hit-stop. Multiplayer never touches Time.timeScale; it freezes this hull and its ball instead.")]
        [SerializeField, Range(0f, 1f)] float hitStopTimeScale = 0.05f;

        [Header("Impact - shake, haptics, debris")]
        [Tooltip("Camera shake for a smash at smash speed.")]
        [SerializeField, Min(0f)] float shakeMin = 0.35f;
        [Tooltip("Camera shake for a white-hot smash.")]
        [SerializeField, Min(0f)] float shakeMax = 1.8f;
        [Tooltip("Camera shake duration (seconds).")]
        [SerializeField, Min(0f)] float shakeSeconds = 0.22f;
        [Tooltip("Shake for a crush (a below-smash kill), as a fraction of shakeMin.")]
        [SerializeField, Range(0f, 1f)] float crushShakeFraction = 0.35f;
        [Tooltip("Haptic strength floor (0-1) for a smash; rises to 1 when white-hot.")]
        [SerializeField, Range(0f, 1f)] float hapticFloor01 = 0.45f;
        [Tooltip("Debris velocity = ball velocity x this (the fleet's 'proportional' restitution).")]
        [SerializeField, Min(0f)] float debrisRestitution = 1f / 3f;
        [Tooltip("Debris speed ceiling (u/s). Must be > 0 or the prefab's ~33 u/s cap flattens a fast ball's debris.")]
        [SerializeField, Min(0f)] float debrisSpeedLimit = 200f;

        [Header("Combo")]
        [Tooltip("A whip ends (and the combo counter resets) once the ball has stayed below smash speed this long (seconds).")]
        [SerializeField, Min(0f)] float comboGraceSeconds = 0.4f;

        [Header("READY cue")]
        [Tooltip("READY shows while the predicted reel-in crack is at least this multiple of smash speed.")]
        [SerializeField, Min(0f)] float readyMargin = 1.05f;
        [Tooltip("Lime flicker rate of the chain while READY (Hz). The lime is the palette's CTA colour.")]
        [SerializeField, Min(0f)] float readyFlickerHz = 14f;

        [Header("Look")]
        [Tooltip("Rendered ball radius as a multiple of the physics radius.")]
        [SerializeField, Min(0.1f)] float ballVisualScale = 1.25f;
        [Tooltip("Chain links (gameplay: the chain cuts; and drawn).")]
        [SerializeField, Range(2, 32)] int chainLinks = 12;
        [Tooltip("Drawn chain width as a fraction of the ball's physics radius.")]
        [SerializeField, Min(0f)] float chainWidthFraction = 0.18f;
        [Tooltip("Gauge ring radius as a multiple of the rendered ball radius.")]
        [SerializeField, Min(1f)] float gaugeRadiusScale = 1.45f;
        [Tooltip("The chain's own colour (iron).")]
        [SerializeField] Color chainColor = new Color(0.22f, 0.2f, 0.2f);
        [Tooltip("Fallback ball colour when the palette cannot be read (e.g. no theme loaded).")]
        [SerializeField] Color fallbackDomainColor = new Color(0.3f, 0.9f, 0.85f);
        [Tooltip("Fallback hot colour when the palette has no danger colour.")]
        [SerializeField] Color fallbackDangerColor = new Color(1f, 0.05f, 0.05f);
        [Tooltip("Fallback READY colour when the palette has no CTA colour.")]
        [SerializeField] Color fallbackReadyColor = new Color(0.55f, 0.95f, 0.15f);
        [Tooltip("Skid-trail lifetime while planting (seconds).")]
        [SerializeField, Min(0f)] float skidTrailSeconds = 0.6f;

        public ThresherDials Dials => dials;
        public ElementalFloat ExplosionSizeMultiplier => explosionSizeMultiplier;
        public ElementalFloat BallMassMultiplier => ballMassMultiplier;
        public ElementalFloat ChainReachMultiplier => chainReachMultiplier;
        public ElementalFloat SpinMultiplier => spinMultiplier;
        public AOEExplosion[] SmashExplosions => smashExplosions;
        public float ExplosionDiameter => explosionDiameter;
        public float ExplosionMinInterval => explosionMinInterval;
        public float ChainCutRadius => chainCutRadius;
        public float ChainDebrisRestitution => chainDebrisRestitution;
        public float HitStopMinSeconds => hitStopMinSeconds;
        public float HitStopMaxSeconds => Mathf.Max(hitStopMinSeconds, hitStopMaxSeconds);
        public float HitStopTimeScale => hitStopTimeScale;
        public float ShakeMin => shakeMin;
        public float ShakeMax => Mathf.Max(shakeMin, shakeMax);
        public float ShakeSeconds => shakeSeconds;
        public float CrushShakeFraction => crushShakeFraction;
        public float HapticFloor01 => hapticFloor01;
        public float DebrisRestitution => debrisRestitution;
        public float DebrisSpeedLimit => debrisSpeedLimit;
        public float ComboGraceSeconds => comboGraceSeconds;
        public float ReadyMargin => readyMargin;
        public float ReadyFlickerHz => readyFlickerHz;
        public float BallVisualScale => ballVisualScale;
        public int ChainLinks => chainLinks;
        public float ChainWidthFraction => chainWidthFraction;
        public float GaugeRadiusScale => gaugeRadiusScale;
        public Color ChainColor => chainColor;
        public Color FallbackDomainColor => fallbackDomainColor;
        public Color FallbackDangerColor => fallbackDangerColor;
        public Color FallbackReadyColor => fallbackReadyColor;
        public float SkidTrailSeconds => skidTrailSeconds;
    }
}
