using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
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
