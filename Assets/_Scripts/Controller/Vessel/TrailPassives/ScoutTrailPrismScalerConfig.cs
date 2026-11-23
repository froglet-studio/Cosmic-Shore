using UnityEngine;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Configuration data for ScoutTrailPrismScaler.
    /// Create instances via Assets > Create > Cosmic Shore > Scout Trail Prism Config
    /// </summary>
    [CreateAssetMenu(fileName = "ScoutTrailPrismConfig", menuName = "ScriptableObjects/Scout Trail Prism Config")]
    public class ScoutTrailPrismScalerConfig : ScriptableObject
    {
        [Header("Scale Settings")]
        [Tooltip("Maps normalized radius (0-1) to output scale (0-1)")]
        [SerializeField] AnimationCurve scaleByRadius = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve ScaleByRadius => scaleByRadius;

        [Header("Detection Settings")]
        [Tooltip("Initial detection radius on startup")]
        [SerializeField] float startRadius = 150f;
        public float StartRadius => startRadius;

        [Tooltip("Minimum detection radius (corresponds to scale 0)")]
        [SerializeField] float minRadius = 20f;
        public float MinRadius => minRadius;

        [Tooltip("Maximum detection radius (corresponds to scale 1)")]
        [SerializeField] float maxRadius = 100f;
        public float MaxRadius => maxRadius;

        [Tooltip("How fast the radius grows when no obstacles detected (units/sec)")]
        [SerializeField] float radiusGrowthRate = 6000f;
        public float RadiusGrowthRate => radiusGrowthRate;

        [Tooltip("How fast the radius shrinks when obstacles detected (units/sec)")]
        [SerializeField] float radiusShrinkRate = 6000f;
        public float RadiusShrinkRate => radiusShrinkRate;

        [Header("Smoothing")]
        [Tooltip("Time to smooth scale transitions")]
        [SerializeField] float scaleSmoothTime = 0.2f;
        public float ScaleSmoothTime => scaleSmoothTime;

        [Header("Performance")]
        [Tooltip("How often to run obstacle detection (seconds)")]
        [SerializeField] float updateInterval = 0.05f;
        public float UpdateInterval => updateInterval;

        /// <summary>
        /// Validates and clamps values to reasonable ranges.
        /// Called automatically when values change in the inspector.
        /// </summary>
        private void OnValidate()
        {
            minRadius = Mathf.Max(0.1f, minRadius);
            maxRadius = Mathf.Max(minRadius + 0.1f, maxRadius);
            startRadius = Mathf.Clamp(startRadius, minRadius, maxRadius);
            radiusGrowthRate = Mathf.Max(0f, radiusGrowthRate);
            radiusShrinkRate = Mathf.Max(0f, radiusShrinkRate);
            scaleSmoothTime = Mathf.Max(0f, scaleSmoothTime);
            updateInterval = Mathf.Max(0.01f, updateInterval);
        }
    }
}
