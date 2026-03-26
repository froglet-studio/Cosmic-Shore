using System.Collections;
using CosmicShore.Core;
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
///
/// Prisms are spawned gradually (one at a time) for a procedural reveal effect.
/// The collision trigger is disabled until all prisms have spawned.
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

    [Header("Gradual Spawning")]
    [Tooltip("Seconds between each prism spawn during gradual reveal. 0 = all at once.")]
    [SerializeField] protected float spawnInterval = 0.03f;

    /// <summary>
    /// Returns block count scaled by current intensity level.
    /// Base output at intensity 1 = 3x baseBlockCount (matches old intensity 3),
    /// then grows further: intensity 2 = 4x, intensity 3 = 5x, etc.
    /// </summary>
    protected int GetScaledBlockCount()
    {
        return baseBlockCount * Mathf.Max(1, intensityLevel + 2);
    }

    /// <summary>
    /// Returns a size multiplier that grows with intensity so prisms don't overlap.
    /// Base output at intensity 1 = sqrt(3) ≈ 1.73x (matches old intensity 3),
    /// then scales further. Uses square root scaling for proportional spacing.
    /// </summary>
    protected float GetIntensitySizeMultiplier()
    {
        return Mathf.Sqrt(Mathf.Max(1, intensityLevel + 2));
    }

    public override GameObject Spawn(int intensity = 1)
    {
        intensityLevel = intensity;
        trails.Clear();

        var container = new GameObject(name);
        var trailData = GetTrailData();

        // Attach trigger immediately but keep it disabled until spawning finishes
        var trigger = AttachTrigger(container);

        if (spawnInterval > 0f)
        {
            // Gradual spawning via coroutine
            StartCoroutine(GradualSpawnCoroutine(trailData, container, trigger));
        }
        else
        {
            // Instant spawning (legacy behavior)
            SpawnLeafObjects(trailData, container);
            if (trigger) trigger.SetReady(true);
        }

        return container;
    }

    IEnumerator GradualSpawnCoroutine(SpawnTrailData[] trailData, GameObject container, ShapeCollisionTrigger trigger)
    {
        foreach (var td in trailData)
        {
            var prismPrefab = GetPrismPrefab();
            if (prismPrefab == null) continue;

            // Treat SpawnPoint.Scale as a multiplier on the prefab's authored scale
            // so Vector3.one means "100% of prefab size" rather than absolute (1,1,1).
            var prefabScale = prismPrefab.transform.localScale;

            var trail = new Trail(td.IsLoop);
            var actualDomain = td.Domain;

            for (int i = 0; i < td.Points.Length; i++)
            {
                if (!container) yield break; // Container was destroyed

                var point = td.Points[i];
                var block = Instantiate(prismPrefab, container.transform);
                block.ChangeTeam(actualDomain);
                block.ownerID = $"{container.name}::{i}";
                block.transform.localPosition = point.Position;
                block.transform.localRotation = point.Rotation;
                block.TargetScale = Vector3.Scale(point.Scale, prefabScale);
                block.Trail = trail;
                block.Initialize();
                trail.Add(block);

                yield return new WaitForSeconds(spawnInterval);
            }

            trails.Add(trail);
        }

        // All prisms spawned — enable collision
        if (trigger)
        {
            // Recalculate radius now that all renderers exist
            if (triggerRadius <= 0f && container)
            {
                var sphere = container.GetComponent<SphereCollider>();
                if (sphere) sphere.radius = CalculateBoundingRadius(container);
            }
            trigger.SetReady(true);
        }
    }

    /// <summary>
    /// Returns the Prism prefab for leaf spawning. Subclasses with a prism field
    /// can override this. Default falls back to leafPrefab.
    /// </summary>
    protected virtual Prism GetPrismPrefab()
    {
        return leafPrefab ? leafPrefab.GetComponent<Prism>() : null;
    }

    ShapeCollisionTrigger AttachTrigger(GameObject container)
    {
        if (shapeDefinition == null) return null;

        // Add kinematic Rigidbody so trigger events fire
        var rb = container.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Add sphere trigger sized to shape bounds
        var sphere = container.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = triggerRadius > 0 ? triggerRadius : 20f; // Will be recalculated after spawn

        // Add collision handler — starts disabled
        var trigger = container.AddComponent<ShapeCollisionTrigger>();
        trigger.Initialize(shapeDefinition, domain);
        trigger.SetReady(false);

        return trigger;
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
