using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for the Rhino energy sword (RHINO_ENERGY_SWORD.md): the scale mapping the
    /// blade's resting length follows from stored energy, the elemental-crystal burst, and
    /// every blade-look/juice knob (heat ramp, impact flashes, tip tracers, camera shake).
    /// One shared asset (`_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset`)
    /// drives every Rhino, read by <see cref="ShieldSkimmerScaleDriver"/> and
    /// <see cref="RhinoSwordVisualizer"/>.
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
        [Tooltip("Brightness multiplier always applied to the blade's authored colour so the sword reads clearly.")]
        [SerializeField] private float visibilityMultiplier = 2f;
        [Tooltip("Colour the blade blends toward as stored energy fills (teal at empty → this at full).")]
        [ColorUsage(true, true)] [SerializeField] private Color fullEnergyColor = Color.white;
        [Tooltip("Extra brightness multiplier at FULL energy (lerped from 1 at empty). Keep ≤4 — bloom clamps beyond that.")]
        [SerializeField] private float fullEnergyBrightness = 2.5f;

        [Header("Blade Look — impact flashes")]
        [Tooltip("Flash strength (0..1) when the sword destroys a normal prism.")]
        [SerializeField, Range(0f, 1f)] private float hitFlashAmount = 0.35f;
        [Tooltip("Flash strength (0..1) when the sword pops a super-shielded prism or bursts on a crystal.")]
        [SerializeField, Range(0f, 1f)] private float popFlashAmount = 1f;
        [Tooltip("Seconds a full-strength flash takes to decay back to the heat-ramp colour.")]
        [SerializeField] private float flashDecaySeconds = 0.35f;
        [Tooltip("Colour the blade flashes toward on impacts (bright so bloom sells the hit).")]
        [ColorUsage(true, true)] [SerializeField] private Color flashColor = new Color(3f, 3f, 3f, 1f);

        [Header("Tip Tracers")]
        [Tooltip("Draw motion tracers streaking from the blade's two tips (recolour with the blade heat).")]
        [SerializeField] private bool tracersEnabled = true;
        [Tooltip("Authored tracer material (instanced per sword at runtime so tinting never touches the shared asset). " +
                 "When empty a runtime unlit fallback is created instead.")]
        [SerializeField] private Material tracerMaterial;
        [Tooltip("Tracer streak width in world units.")]
        [SerializeField] private float tracerWidth = 2f;
        [Tooltip("Tracer streak persistence in seconds.")]
        [SerializeField] private float tracerTimeSeconds = 0.3f;

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

        public float VisibilityMultiplier => Mathf.Max(0f, visibilityMultiplier);
        public Color FullEnergyColor      => fullEnergyColor;
        public float FullEnergyBrightness => Mathf.Max(1f, fullEnergyBrightness);

        public float HitFlashAmount    => hitFlashAmount;
        public float PopFlashAmount    => popFlashAmount;
        public float FlashDecaySeconds => Mathf.Max(0.0001f, flashDecaySeconds);
        public Color FlashColor        => flashColor;

        public bool     TracersEnabled    => tracersEnabled;
        public Material TracerMaterial    => tracerMaterial;
        public float    TracerWidth       => Mathf.Max(0f, tracerWidth);
        public float    TracerTimeSeconds => Mathf.Max(0.01f, tracerTimeSeconds);

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
