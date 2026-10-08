using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every dial of the Flail: the chain physics (embedded <see cref="FlailDials"/>, in sandbox
    /// units and scaled by its own <c>gameCruise / sandboxCruise</c>), the impact feel, and the look.
    /// One asset per hull; read by <see cref="FlailExecutor"/>. See <c>FLAIL.md</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "FlailConfig", menuName = "ScriptableObjects/Vessel Actions/Flail Config")]
    public sealed class FlailConfigSO : ScriptableObject
    {
        [Tooltip("The chain physics, authored in the 2D sandbox's units. See FlailDials.")]
        [SerializeField] FlailDials dials = new FlailDials();

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
        [Tooltip("Shake for a chip, as a fraction of shakeMin.")]
        [SerializeField, Range(0f, 1f)] float chipShakeFraction = 0.35f;
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
        [Tooltip("Gold flicker rate of the chain while READY (Hz).")]
        [SerializeField, Min(0f)] float readyFlickerHz = 14f;

        [Header("Look")]
        [Tooltip("Rendered ball radius as a multiple of the physics radius.")]
        [SerializeField, Min(0.1f)] float ballVisualScale = 1.25f;
        [Tooltip("Cosmetic verlet chain links between the hull and the ball.")]
        [SerializeField, Range(2, 32)] int chainLinks = 12;
        [Tooltip("Chain width as a fraction of the ball's physics radius.")]
        [SerializeField, Min(0f)] float chainWidthFraction = 0.18f;
        [Tooltip("Gauge ring radius as a multiple of the rendered ball radius.")]
        [SerializeField, Min(1f)] float gaugeRadiusScale = 1.45f;
        [Tooltip("Iron colour of a cold ball.")]
        [SerializeField] Color ironColor = new Color(0.10f, 0.09f, 0.09f);
        [Tooltip("Heat colour at smash speed.")]
        [SerializeField] Color goldColor = new Color(1f, 0.72f, 0.18f);
        [Tooltip("Heat colour when white-hot.")]
        [SerializeField] Color whiteHotColor = new Color(1f, 0.97f, 0.9f);
        [Tooltip("Skid-trail lifetime while planting (seconds).")]
        [SerializeField, Min(0f)] float skidTrailSeconds = 0.6f;

        public FlailDials Dials => dials;
        public float HitStopMinSeconds => hitStopMinSeconds;
        public float HitStopMaxSeconds => Mathf.Max(hitStopMinSeconds, hitStopMaxSeconds);
        public float HitStopTimeScale => hitStopTimeScale;
        public float ShakeMin => shakeMin;
        public float ShakeMax => Mathf.Max(shakeMin, shakeMax);
        public float ShakeSeconds => shakeSeconds;
        public float ChipShakeFraction => chipShakeFraction;
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
        public Color IronColor => ironColor;
        public Color GoldColor => goldColor;
        public Color WhiteHotColor => whiteHotColor;
        public float SkidTrailSeconds => skidTrailSeconds;
    }
}
