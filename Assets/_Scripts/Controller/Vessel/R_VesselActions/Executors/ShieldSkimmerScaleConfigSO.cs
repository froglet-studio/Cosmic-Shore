using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for the Rhino energy sword (RHINO_ENERGY_SWORD.md): the scale mapping the
    /// blade's resting length follows from stored energy, the energize ritual (the
    /// supershield key), the elemental-crystal burst, and every blade-look/juice knob
    /// (heat ramp, energized white-hot, impact flashes, crackle, camera shake).
    /// One shared asset (`_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset`)
    /// drives every Rhino, read by <see cref="ShieldSkimmerScaleDriver"/> and
    /// <see cref="RhinoSwordFXController"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ShieldSkimmerScaleConfig",
        menuName = "ScriptableObjects/Skimmer/ShieldSkimmerScaleConfigSO")]
    public sealed class ShieldSkimmerScaleConfigSO : ScriptableObject
    {
        [Header("Scale Mapping (world units, blade length)")]
        [Tooltip("Resting length fallback when no Skimmer provides a live elemental (Space) scale.")]
        [SerializeField] private float baseScale = 30f;      // energy = 0
        [Tooltip("Blade length at FULL energy.")]
        [SerializeField] private float maxScale  = 120f;     // energy = 1
        [Tooltip("Legacy prism-growth cap, kept only so ApplyMaxSizeDebuff keeps its historical meaning; the driver no longer reads it.")]
        [SerializeField] private float prismMaxScale = 100f;

        [Header("Length Smoothing (world units/sec)")]
        [Tooltip("How fast the resting length grows toward the stored-energy target.")]
        [SerializeField] private float prismGrowSpeed = 30f;
        [Tooltip("How fast the resting length eases back down (crystal drain, debuffs).")]
        [SerializeField] private float shrinkSpeed = 10f;

        [Header("Energize (hold the both-triggers chop stance — the supershield key)")]
        [Tooltip("Fraction of max energy consumed each time the blade energizes.")]
        [SerializeField, Range(0f, 1f)] private float energizeCostFraction = 0.1f;
        [Tooltip("Seconds the lower/chop stance must be held to energize.")]
        [SerializeField] private float energizeHoldSeconds = 1f;
        [Tooltip("Seconds the blade stays energized AFTER leaving the stance (holding the stance keeps it lit).")]
        [SerializeField] private float energizedTailSeconds = 5f;
        [Tooltip("Seconds of cooldown after the energized tail before the blade can charge again.")]
        [SerializeField] private float energizeCooldownSeconds = 5f;

        [Header("Crystal Burst (all three dimensions)")]
        [Tooltip("Uniform size factor the blade bursts to on a FULL-energy elemental-crystal hit, " +
                 "applied to the authored X/Y/Z silhouette (not just length). Less energy scales " +
                 "the factor down toward 1 (no burst).")]
        [SerializeField] private float crystalBurstFactorAtFullEnergy = 4f;
        [Tooltip("Seconds the burst holds at full size before easing back.")]
        [SerializeField] private float crystalBurstHoldSeconds = 2.5f;
        [Tooltip("Local-scale units/sec the blade grows into the burst shape.")]
        [SerializeField] private float crystalBurstGrowSpeed = 600f;
        [Tooltip("Local-scale units/sec the blade eases back from the burst to its resting size.")]
        [SerializeField] private float crystalBurstReturnSpeed = 150f;

        [Header("Blade Look — heat ramp")]
        [Tooltip("Brightness multiplier always applied to the blade's colour. Bloom (threshold 0.2, clamp 0.5) " +
                 "does the rest, so this compounds with fullEnergyBrightness FAST - past ~3 combined the " +
                 "blade stops reading as a blade and becomes a white blob.")]
        [SerializeField] private float visibilityMultiplier = 1.2f;
        [Tooltip("The blade's RESTING colour — deliberately NOT the pilot's domain colour: the sword " +
                 "friendly-fires, so a team-coloured blade would read as safe to allies. White-hot says " +
                 "'this cuts anything'. (Authored here, not read off FresnelMaterial, so the blade's " +
                 "meaning can never drift with a shared material's tint.)")]
        [ColorUsage(true, true)] [SerializeField] private Color restingBladeColor = Color.white;
        [Tooltip("Colour the blade blends toward as stored energy fills. Keep it the SAME HUE as the " +
                 "resting colour — energy is read as BRIGHTNESS (fullEnergyBrightness); HUE is reserved " +
                 "for the energized state below, so the two signals can never be confused.")]
        [ColorUsage(true, true)] [SerializeField] private Color fullEnergyColor = Color.white;
        [Tooltip("Extra brightness multiplier at FULL energy (lerped from 1 at empty). Keep ≤4 — bloom clamps beyond that.")]
        [SerializeField] private float fullEnergyBrightness = 1.8f;

        [Header("Blade Look — energized (the danger colour)")]
        [Tooltip("Blade colour while ENERGIZED — the shared DANGER colour (SO_ColorSet.Danger, the same " +
                 "domain-independent red a danger prism's rim wears). An energized blade destroys hardened " +
                 "mass and still friendly-fires, so it speaks the platform's existing 'this hurts' language " +
                 "rather than inventing one. Multiplied by visibilityMultiplier at runtime.")]
        [ColorUsage(true, true)] [SerializeField] private Color energizedColor = new Color(1.4979111f, 0.0058463f, 0.0068495f, 1f);
        [Tooltip("Seconds to blend the blade + its tracer between the heat ramp and the danger colour " +
                 "on energize/de-energize. The tracer is tinted from the same live colour, so the streak " +
                 "changes with the sword through every state.")]
        [SerializeField] private float colorTransitionSeconds = 0.25f;

        [Header("Blade Look — crackle (ForcefieldCrackleController on the blade)")]
        [Tooltip("Arc intensity of the IGNITION burst the instant the blade energizes.")]
        [SerializeField] private float igniteCrackleIntensity = 2.5f;
        [Tooltip("Seconds the ignition arcs persist.")]
        [SerializeField] private float igniteCrackleSeconds = 0.9f;
        [Tooltip("How many arc sites the ignition seeds along the blade (spread hilt→tip).")]
        [SerializeField, Range(1, 8)] private int igniteCrackleSites = 5;
        [Tooltip("Seconds between the small anticipation arcs while CHARGING (holding the stance).")]
        [SerializeField] private float chargeCrackleInterval = 0.18f;
        [Tooltip("Arc intensity of the charging arcs at full charge (ramps up from a third of this).")]
        [SerializeField] private float chargeCrackleIntensity = 1.1f;
        [Tooltip("Arc intensity of the contact spark when the sword destroys a prism.")]
        [SerializeField] private float sparkIntensity = 1.6f;
        [Tooltip("Seconds a contact spark persists.")]
        [SerializeField] private float sparkSeconds = 0.45f;
        [Tooltip("World-unit ripple reach of a contact spark on the blade surface.")]
        [SerializeField] private float sparkWorldRadius = 14f;
        [Tooltip("Arc intensity of the DENIED spark — a non-energized blade touching a super-shielded " +
                 "prism (the bounce). Dim on purpose: it teaches, it doesn't reward.")]
        [SerializeField] private float deniedSparkIntensity = 0.7f;

        [Header("Blade Look — impact flashes")]
        [Tooltip("Flash strength (0..1) when the sword destroys a normal prism.")]
        [SerializeField, Range(0f, 1f)] private float hitFlashAmount = 0.35f;
        [Tooltip("Flash strength (0..1) when the sword pops a super-shielded prism or bursts on a crystal.")]
        [SerializeField, Range(0f, 1f)] private float popFlashAmount = 1f;
        [Tooltip("Seconds a full-strength flash takes to decay back to the heat-ramp colour.")]
        [SerializeField] private float flashDecaySeconds = 0.35f;
        [Tooltip("Colour the blade flashes toward on impacts (bright so bloom sells the hit).")]
        [ColorUsage(true, true)] [SerializeField] private Color flashColor = new Color(2f, 2f, 2f, 1f);

        [Header("Camera Shake (local pilot only)")]
        [Tooltip("Shake intensity when the sword pops a super-shielded prism.")]
        [SerializeField] private float popShakeIntensity = 1.2f;
        [SerializeField] private float popShakeDuration = 0.25f;
        [Tooltip("Shake intensity of a FULL-energy crystal burst (lerped from 0 by the energy consumed).")]
        [SerializeField] private float burstShakeMaxIntensity = 2.5f;
        [SerializeField] private float burstShakeDuration = 0.4f;

        // Runtime-only debuff multiplier for max sizes
        [NonSerialized] private bool  _isMaxSizeDebuffed;
        [NonSerialized] private float _maxScaleMultiplier = 1f;

        // --- Public API ---

        public float BaseScale => baseScale;

        public float MaxScale => Mathf.Max(
            baseScale,
            maxScale * Mathf.Max(0.01f, _maxScaleMultiplier));

        public float PrismMaxScale => Mathf.Max(
            baseScale,
            prismMaxScale * Mathf.Max(0.01f, _maxScaleMultiplier));

        public float PrismGrowSpeed => prismGrowSpeed;
        public float ShrinkSpeed    => shrinkSpeed;

        public float CrystalBurstFactorAtFullEnergy => Mathf.Max(1f, crystalBurstFactorAtFullEnergy);
        public float CrystalBurstHoldSeconds        => Mathf.Max(0f, crystalBurstHoldSeconds);
        public float CrystalBurstGrowSpeed          => Mathf.Max(0.01f, crystalBurstGrowSpeed);
        public float CrystalBurstReturnSpeed        => Mathf.Max(0.01f, crystalBurstReturnSpeed);

        public float EnergizeCostFraction    => Mathf.Clamp01(energizeCostFraction);
        public float EnergizeHoldSeconds     => Mathf.Max(0.01f, energizeHoldSeconds);
        public float EnergizedTailSeconds    => Mathf.Max(0f, energizedTailSeconds);
        public float EnergizeCooldownSeconds => Mathf.Max(0f, energizeCooldownSeconds);

        public float VisibilityMultiplier => Mathf.Max(0f, visibilityMultiplier);
        public Color RestingBladeColor    => restingBladeColor;
        public Color FullEnergyColor      => fullEnergyColor;
        public float FullEnergyBrightness => Mathf.Max(1f, fullEnergyBrightness);

        public Color EnergizedColor         => energizedColor;
        public float ColorTransitionSeconds => Mathf.Max(0.0001f, colorTransitionSeconds);

        public float IgniteCrackleIntensity => Mathf.Max(0f, igniteCrackleIntensity);
        public float IgniteCrackleSeconds   => Mathf.Max(0.05f, igniteCrackleSeconds);
        public int   IgniteCrackleSites     => Mathf.Clamp(igniteCrackleSites, 1, 8);
        public float ChargeCrackleInterval  => Mathf.Max(0.05f, chargeCrackleInterval);
        public float ChargeCrackleIntensity => Mathf.Max(0f, chargeCrackleIntensity);
        public float SparkIntensity         => Mathf.Max(0f, sparkIntensity);
        public float SparkSeconds           => Mathf.Max(0.05f, sparkSeconds);
        public float SparkWorldRadius       => Mathf.Max(0.5f, sparkWorldRadius);
        public float DeniedSparkIntensity   => Mathf.Max(0f, deniedSparkIntensity);

        public float HitFlashAmount    => hitFlashAmount;
        public float PopFlashAmount    => popFlashAmount;
        public float FlashDecaySeconds => Mathf.Max(0.0001f, flashDecaySeconds);
        public Color FlashColor        => flashColor;

        public float PopShakeIntensity      => Mathf.Max(0f, popShakeIntensity);
        public float PopShakeDuration       => Mathf.Max(0f, popShakeDuration);
        public float BurstShakeMaxIntensity => Mathf.Max(0f, burstShakeMaxIntensity);
        public float BurstShakeDuration     => Mathf.Max(0f, burstShakeDuration);

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
