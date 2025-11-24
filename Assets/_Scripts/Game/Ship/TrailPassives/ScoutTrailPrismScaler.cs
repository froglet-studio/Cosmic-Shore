using CosmicShore;
using CosmicShore.Game;
using UnityEngine;

/// <summary>
/// Dynamically scales trail prisms based on surrounding open space.
/// Uses adaptive OverlapSphere sampling to determine if the player is in open or confined areas.
/// Injects scale into VesselPrismController for consistent prism scaling.
/// Designed for scout vessels that reward flying in open spaces.
/// 
/// Configuration is stored in a ScoutTrailPrismScalerConfig ScriptableObject,
/// allowing easy tweaking and multiple presets.
/// </summary>
[RequireComponent(typeof(VesselStatus))]
public class ScoutTrailPrismScaler : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private ScoutTrailPrismScalerConfig config;

    [Header("Runtime Overrides (Optional)")]
    [Tooltip("If set, overrides the config's detection layers at runtime")]
    [SerializeField] private bool useOverrideDetectionLayers;
    [SerializeField] private LayerMask overrideDetectionLayers;

    // References
    private IVesselStatus vesselStatus;

    // Current state
    private float currentDetectionRadius;
    private float targetNormalizedScale;
    private float currentNormalizedScale;
    private float scaleVelocity;
    private float timeSinceLastUpdate;
    private Collider[] hitBuffer;

    // Cached config values (for runtime efficiency)
    private LayerMask activeDetectionLayers;

    // Public read-only access
    public float CurrentNormalizedScale => currentNormalizedScale;
    public float CurrentRadius => currentDetectionRadius;
    public ScoutTrailPrismScalerConfig Config => config;

    private void Awake()
    {
        vesselStatus = GetComponent<VesselStatus>();

        if (config == null)
        {
            Debug.LogError($"[{nameof(ScoutTrailPrismScaler)}] No config assigned on {gameObject.name}! Disabling.", this);
            enabled = false;
            return;
        }

        InitializeFromConfig();
    }

    /// <summary>
    /// Initialize or reinitialize state from the current config.
    /// Call this if you swap configs at runtime.
    /// </summary>
    public void InitializeFromConfig()
    {
        if (config == null) return;

        currentDetectionRadius = config.startRadius;
        currentNormalizedScale = 0.5f; // Start at middle scale
        targetNormalizedScale = currentNormalizedScale;
        scaleVelocity = 0f;
        timeSinceLastUpdate = 0f;

        // Reallocate buffer if size changed
        if (hitBuffer == null || hitBuffer.Length != config.maxColliders)
        {
            hitBuffer = new Collider[config.maxColliders];
        }

        // Cache detection layers
        activeDetectionLayers = useOverrideDetectionLayers ? overrideDetectionLayers : config.detectionLayers;
    }

    /// <summary>
    /// Swap to a different config at runtime.
    /// </summary>
    public void SetConfig(ScoutTrailPrismScalerConfig newConfig)
    {
        if (newConfig == null)
        {
            Debug.LogWarning($"[{nameof(ScoutTrailPrismScaler)}] Attempted to set null config.", this);
            return;
        }

        config = newConfig;
        InitializeFromConfig();
    }

    private void Update()
    {
        if (config == null) return;

        timeSinceLastUpdate += Time.deltaTime;

        if (timeSinceLastUpdate >= config.updateInterval)
        {
            timeSinceLastUpdate = 0f;
            UpdateDetection();
        }

        // Smooth scale changes
        float previousScale = currentNormalizedScale;
        currentNormalizedScale = Mathf.SmoothDamp(
            currentNormalizedScale,
            targetNormalizedScale,
            ref scaleVelocity,
            config.scaleSmoothTime
        );

        // Only inject if scale actually changed (avoid unnecessary updates)
        if (!Mathf.Approximately(currentNormalizedScale, previousScale))
        {
            InjectScale();
        }
    }

    private void UpdateDetection()
    {
        // Check current radius for obstacles
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            currentDetectionRadius,
            hitBuffer,
            activeDetectionLayers
        //QueryTriggerInteraction.Ignore
        );

        bool hitSomething = hitCount > 0;

        if (hitSomething)
        {
            // Shrink detection radius
            currentDetectionRadius = Mathf.Max(
                config.minRadius,
                currentDetectionRadius - config.radiusShrinkRate * config.updateInterval
            );
        }
        else
        {
            // Grow detection radius
            currentDetectionRadius = Mathf.Min(
                config.maxRadius,
                currentDetectionRadius + config.radiusGrowthRate * config.updateInterval
            );
        }

        // Map radius to normalized scale [0-1]
        float normalizedRadius = Mathf.InverseLerp(config.minRadius, config.maxRadius, currentDetectionRadius);
        targetNormalizedScale = config.scaleByRadius.Evaluate(normalizedRadius);
    }

    private void InjectScale()
    {
        vesselStatus?.VesselPrismController?.SetNormalizedXScale(currentNormalizedScale);
    }

    private void OnDisable()
    {
        // Reset to default scale when disabled
        vesselStatus?.VesselPrismController?.SetNormalizedXScale(0.5f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || config == null) return;

        // Draw current detection sphere
        Gizmos.color = Color.Lerp(Color.red, Color.green, currentNormalizedScale);
        Gizmos.DrawWireSphere(transform.position, currentDetectionRadius);

        // Draw min/max ranges
        Gizmos.color = new Color(1, 0, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, config.minRadius);

        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, config.maxRadius);

        // Draw scale indicator
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 5f,
            $"Scale: {currentNormalizedScale:F2}\nRadius: {currentDetectionRadius:F1}m\nConfig: {config.name}"
        );
    }
#endif
}