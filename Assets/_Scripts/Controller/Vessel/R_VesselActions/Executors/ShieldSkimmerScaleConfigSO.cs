using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Gameplay;
using System.Linq;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "ShieldSkimmerScaleConfig",
        menuName = "ScriptableObjects/Skimmer/ShieldSkimmerScaleConfigSO")]
    public sealed class ShieldSkimmerScaleConfigSO : ScriptableObject
    {
        [Header("Scale Mapping (world, uniform XYZ)")]
        [SerializeField] private float baseScale = 30f;      // Shield=0
        [SerializeField] private float maxScale  = 120f;     // Shield=1
        [SerializeField] private float prismMaxScale = 100f; // max scale reachable via prisms (unless crystal)

        [Header("Tick Behaviour")]
        [Tooltip("Must match prism growth in WORLD scale units (e.g. +5 per prism). Decay uses same units per tick.")]
        [SerializeField] private float stepScaleUnits = 5f;

        [SerializeField] private float tickSeconds = 0.25f;
        [SerializeField] private float resetDelaySeconds = 1.0f;

        [Header("Crystal")]
        [SerializeField] private float crystalHoldSeconds = 5f;

        [Header("Smoothing (world units/sec)")]
        [SerializeField] private float prismGrowSpeed   = 300f;
        [SerializeField] private float crystalGrowSpeed = 800f;
        [SerializeField] private float shrinkSpeed      = 400f;

        [Tooltip("How close (world units) we must get to MaxScale to consider it 'reached' for starting crystal hold.")]
        [SerializeField] private float maxHoldEpsilon = 0.05f;

        // ─── Energy sword (RHINO_ENERGY_SWORD.md) ────────────────────────────────
        // Energy is the Shield resource (normalized 0..1): gained per prism a slash
        // destroys, spent on the crystal burst (drains all) and on energizing (a fixed
        // fraction). All fields below carry working defaults so the untouched asset runs.

        [Header("Energy — Crystal Burst (all three dimensions)")]
        [Tooltip("Uniform size factor the blade bursts to on a FULL-energy crystal hit, applied to the " +
                 "authored X/Y/Z silhouette (not just length). Default = maxScale/baseScale, so a full burst " +
                 "reaches today's max length while scaling width/depth by the same factor. Less energy " +
                 "scales the factor down toward 1 (no burst).")]
        [SerializeField] private float crystalBurstFactorAtFullEnergy = 4f;

        [Tooltip("Seconds the burst holds at full size before easing back.")]
        [SerializeField] private float crystalBurstHoldSeconds = 2.5f;

        [Tooltip("Local-scale units/sec the blade grows into the burst shape.")]
        [SerializeField] private float crystalBurstGrowSpeed = 600f;

        [Tooltip("Local-scale units/sec the blade eases back from the burst to its resting size.")]
        [SerializeField] private float crystalBurstReturnSpeed = 150f;

        [Header("Energy — Energize (hold the lower/chop stance)")]
        [Tooltip("Fraction of max energy consumed to energize the blade (1/10 of maximum energy).")]
        [SerializeField, Range(0f, 1f)] private float energizeCostFraction = 0.1f;

        [Tooltip("Seconds the lower stance must be held to energize.")]
        [SerializeField] private float energizeHoldSeconds = 1f;

        [Tooltip("Seconds the blade stays energized AFTER leaving the stance.")]
        [SerializeField] private float energizedTailSeconds = 5f;

        [Tooltip("Seconds of cooldown after the energized tail before the blade can be energized again " +
                 "(tail + cooldown = the full lockout after leaving the stance).")]
        [SerializeField] private float energizeCooldownSeconds = 5f;

        [Header("Energy — Slashing")]
        [Tooltip("Seconds between damaging slashes when NOT energized. Energized sets this to 0 (frenzy).")]
        [SerializeField] private float slashCooldownSeconds = 1f;

        [Header("Energy — Blade visuals")]
        [Tooltip("Brightness multiplier applied to the blade's authored colour so it reads twice as visible.")]
        [SerializeField] private float visibilityMultiplier = 2f;

        [Tooltip("Blade colour while energized (blends from the authored teal to this). Kept bright so bloom " +
                 "sells the 'hot' blade; multiplied by visibilityMultiplier at runtime.")]
        [ColorUsage(true, true)] [SerializeField] private Color energizedColor = Color.white;

        [Tooltip("Seconds to blend the blade + tracers between blue and white on energize/de-energize.")]
        [SerializeField] private float colorTransitionSeconds = 0.25f;

        [Tooltip("Draw motion tracers along the blade's edges (recolour with the blade).")]
        [SerializeField] private bool tracersEnabled = true;

        [Tooltip("Tracer streak width in world units (scaled with the blade).")]
        [SerializeField] private float tracerWidth = 2f;

        [Tooltip("Tracer streak persistence in seconds.")]
        [SerializeField] private float tracerTimeSeconds = 0.3f;

        // Runtime-only debuff multiplier for max sizes
        [NonSerialized] private bool  _isMaxSizeDebuffed;
        [NonSerialized] private float _maxScaleMultiplier = 1f;

        // --- Public API used by the driver ---

        public float BaseScale => baseScale;

        public float MaxScale => Mathf.Max(
            baseScale,
            maxScale * Mathf.Max(0.01f, _maxScaleMultiplier));

        public float PrismMaxScale => Mathf.Max(
            baseScale,
            prismMaxScale * Mathf.Max(0.01f, _maxScaleMultiplier));

        public float StepScaleUnits     => stepScaleUnits;
        public float TickSeconds        => tickSeconds;
        public float ResetDelaySeconds  => resetDelaySeconds;
        public float CrystalHoldSeconds => crystalHoldSeconds;

        public float PrismGrowSpeed     => prismGrowSpeed;
        public float CrystalGrowSpeed   => crystalGrowSpeed;
        public float ShrinkSpeed        => shrinkSpeed;

        public float MaxHoldEpsilon     => maxHoldEpsilon;

        // ─── Energy sword accessors ───
        public float CrystalBurstFactorAtFullEnergy => Mathf.Max(1f, crystalBurstFactorAtFullEnergy);
        public float CrystalBurstHoldSeconds        => Mathf.Max(0f, crystalBurstHoldSeconds);
        public float CrystalBurstGrowSpeed          => Mathf.Max(0.01f, crystalBurstGrowSpeed);
        public float CrystalBurstReturnSpeed        => Mathf.Max(0.01f, crystalBurstReturnSpeed);

        public float EnergizeCostFraction    => Mathf.Clamp01(energizeCostFraction);
        public float EnergizeHoldSeconds      => Mathf.Max(0f, energizeHoldSeconds);
        public float EnergizedTailSeconds     => Mathf.Max(0f, energizedTailSeconds);
        public float EnergizeCooldownSeconds  => Mathf.Max(0f, energizeCooldownSeconds);

        public float SlashCooldownSeconds     => Mathf.Max(0f, slashCooldownSeconds);

        public float VisibilityMultiplier     => Mathf.Max(0f, visibilityMultiplier);
        public Color EnergizedColor           => energizedColor;
        public float ColorTransitionSeconds   => Mathf.Max(0.0001f, colorTransitionSeconds);
        public bool  TracersEnabled           => tracersEnabled;
        public float TracerWidth              => Mathf.Max(0f, tracerWidth);
        public float TracerTimeSeconds        => Mathf.Max(0.01f, tracerTimeSeconds);

        /// <summary>
        /// Temporarily scales the effective max sizes (MaxScale & PrismMaxScale)
        /// by <paramref name="sizeMultiplier"/> and restores after <paramref name="durationSeconds"/>.
        /// NOTE: this mutates THIS ScriptableObject; if multiple skimmers share it, they share the debuff too.
        /// </summary>
        public async UniTaskVoid ApplyMaxSizeDebuff(float sizeMultiplier, float durationSeconds)
        {
            if (_isMaxSizeDebuffed)
                return;

            _isMaxSizeDebuffed = true;

            // Store old multiplier in case we want nested debuffs later.
            float previous = _maxScaleMultiplier;
            _maxScaleMultiplier = Mathf.Max(0.01f, sizeMultiplier);

            await UniTask.Delay(TimeSpan.FromSeconds(durationSeconds));

            _maxScaleMultiplier = previous;
            _isMaxSizeDebuffed = false;
        }
    }
}
