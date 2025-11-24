using UnityEngine;

/// <summary>
/// Configuration data for ScoutTrailPrismScaler.
/// Create instances via Assets > Create > Cosmic Shore > Scout Trail Prism Config
/// </summary>
[CreateAssetMenu(fileName = "ScoutTrailPrismConfig", menuName = "Cosmic Shore/Scout Trail Prism Config")]
public class ScoutTrailPrismScalerConfig : ScriptableObject
{
    [Header("Scale Settings")]
    [Tooltip("Maps normalized radius (0-1) to output scale (0-1)")]
    public AnimationCurve scaleByRadius = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Detection Settings")]
    [Tooltip("Initial detection radius on startup")]
    public float startRadius = 150f;

    [Tooltip("Minimum detection radius (corresponds to scale 0)")]
    public float minRadius = 20f;

    [Tooltip("Maximum detection radius (corresponds to scale 1)")]
    public float maxRadius = 100f;

    [Tooltip("How fast the radius grows when no obstacles detected (units/sec)")]
    public float radiusGrowthRate = 6000f;

    [Tooltip("How fast the radius shrinks when obstacles detected (units/sec)")]
    public float radiusShrinkRate = 6000f;

    [Tooltip("Physics layers to detect as obstacles")]
    public LayerMask detectionLayers;

    [Header("Smoothing")]
    [Tooltip("Time to smooth scale transitions")]
    public float scaleSmoothTime = 0.2f;

    [Header("Performance")]
    [Tooltip("How often to run obstacle detection (seconds)")]
    public float updateInterval = 0.05f;

    [Tooltip("Max colliders to check per update (preallocated buffer size)")]
    public int maxColliders = 1;

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
        maxColliders = Mathf.Max(1, maxColliders);
    }
}