using CosmicShore.Game.ShapeDrawing;
using CosmicShore.Game.Spawning;
using UnityEngine;

/// <summary>
/// Abstract base for spawnable shapes that trigger shape drawing mode on vessel collision.
/// Extends SpawnableBase to generate prism trails in recognizable 2D shapes,
/// then attaches a SphereCollider trigger + ShapeCollisionTrigger to the container.
///
/// Subclasses implement GeneratePoints() / GenerateTrailData() + GetParameterHash().
///
/// Intensity scaling: higher intensity = more blockCount via GetScaledBlockCount().
/// Base block count is 30; intensity 2 = 60, intensity 3 = 90, etc.
/// </summary>
public abstract class SpawnableShapeBase : SpawnableBase
{
    [Header("Shape Identity")]
    [Tooltip("The ShapeDefinition SO that gets passed to ShapeDrawingManager when this shape is hit.")]
    [SerializeField] protected ShapeDefinition shapeDefinition;

    [Header("Shape Collision")]
    [Tooltip("Radius of the trigger sphere around the shape. Auto-calculated from bounding box if 0.")]
    [SerializeField] protected float triggerRadius;

    [Header("Shape Scale")]
    [Tooltip("Base number of prism blocks at intensity 1.")]
    [SerializeField] protected int baseBlockCount = 30;

    /// <summary>
    /// Returns block count scaled by current intensity level.
    /// Intensity 1 = baseBlockCount, 2 = baseBlockCount * 2, etc.
    /// </summary>
    protected int GetScaledBlockCount()
    {
        return baseBlockCount * Mathf.Max(1, intensityLevel);
    }

    public override GameObject Spawn(int intensity = 1)
    {
        var container = base.Spawn(intensity);
        AttachTrigger(container);
        return container;
    }

    void AttachTrigger(GameObject container)
    {
        if (shapeDefinition == null) return;

        // Add kinematic Rigidbody so trigger events fire
        var rb = container.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Add sphere trigger sized to shape bounds
        var sphere = container.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = triggerRadius > 0 ? triggerRadius : CalculateBoundingRadius(container);

        // Add collision handler
        var trigger = container.AddComponent<ShapeCollisionTrigger>();
        trigger.Initialize(shapeDefinition);
    }

    float CalculateBoundingRadius(GameObject container)
    {
        float maxDist = 0f;
        var center = container.transform.position;

        foreach (var renderer in container.GetComponentsInChildren<Renderer>())
        {
            var bounds = renderer.bounds;
            float dist = Vector3.Distance(center, bounds.center) + bounds.extents.magnitude;
            if (dist > maxDist) maxDist = dist;
        }

        // Fallback minimum radius
        return Mathf.Max(maxDist, 20f);
    }
}
