using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// Shape-content groundwork: SpawnableShapeBase + 8 shape spawnables (Circle,
// Ellipsoid, Helix, Cylinder, Diamond, Arrow, Heart, FiveRings) + ShapeDefinition +
// ShapeCollisionTrigger + ShapeSign(Events) ported. Engine gains Bounds +
// Renderer.bounds (unit-cube convention) + a kinematic Rigidbody placeholder.
public class SpawnableShapeTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    static void SetField(object target, string name, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{name}' not found on {target.GetType().Name}.");
    }

    T MakeShape<T>() where T : SpawnableShapeBase
    {
        var go = new GameObject(typeof(T).Name);
        spawned.Add(go);
        return go.AddComponent<T>();
    }

    [Fact]
    public void Circle_GeneratesClosedLoopOfScaledBlockCount()
    {
        var circle = MakeShape<SpawnableCircle>();
        SetField(circle, "baseBlockCount", 10);
        circle.intensityLevel = 1;

        var data = circle.GetTrailData();

        Assert.Single(data);
        Assert.True(data[0].IsLoop); // circle closes
        // intensity 1 → baseBlockCount × (1 + 2) = 30 points
        Assert.Equal(30, data[0].Points.Length);

        // All points lie on the scaled radius (16 × sqrt(3)) in the XY plane.
        float expectedRadius = 16f * Mathf.Sqrt(3f);
        foreach (var p in data[0].Points)
        {
            Assert.Equal(0f, p.Position.z, 3);
            Assert.Equal(expectedRadius, p.Position.magnitude, 2);
        }
    }

    [Fact]
    public void Shape_IntensityScalesBlockCountAndSize()
    {
        var circle = MakeShape<SpawnableCircle>();
        SetField(circle, "baseBlockCount", 10);

        circle.intensityLevel = 1;
        int count1 = circle.GetTrailData()[0].Points.Length;

        circle.intensityLevel = 3; // hash includes intensity → regenerates
        int count3 = circle.GetTrailData()[0].Points.Length;

        Assert.Equal(30, count1); // 10 × 3
        Assert.Equal(50, count3); // 10 × 5
    }

    [Fact]
    public void Spawn_InstantMode_AttachesReadyTriggerWithRigidbodyAndPrisms()
    {
        var circle = MakeShape<SpawnableCircle>();
        SetField(circle, "baseBlockCount", 4);
        SetField(circle, "spawnInterval", 0f); // instant spawning
        SetField(circle, "prism", PrismTestRig.CreatePrismAt(Vector3.zero, "circle-prism-prefab"));
        SetField(circle, "shapeDefinition", ScriptableObject.CreateInstance<ShapeDefinition>()); // trigger requires one

        var container = circle.Spawn(1);
        spawned.Add(container);

        var trigger = container.GetComponent<ShapeCollisionTrigger>();
        Assert.NotNull(trigger);
        Assert.NotNull(container.GetComponent<Rigidbody>());      // RequireComponent satisfied
        var sphere = container.GetComponent<SphereCollider>();
        Assert.NotNull(sphere);
        Assert.True(sphere.isTrigger);

        // 12 prisms (4 × 3 at intensity 1) spawned as children.
        Assert.Equal(12, circle.GetTrails()[0].TrailList.Count);
    }

    // ── Engine Bounds ───────────────────────────────────────────────────────

    [Fact]
    public void Bounds_MinMaxEncapsulateIntersects()
    {
        var b = new Bounds(Vector3.zero, new Vector3(2f, 4f, 6f));
        Assert.Equal(new Vector3(-1f, -2f, -3f), b.min);
        Assert.Equal(new Vector3(1f, 2f, 3f), b.max);

        b.Encapsulate(new Vector3(5f, 0f, 0f));
        Assert.Equal(5f, b.max.x, 3);
        Assert.Equal(-1f, b.min.x, 3);

        Assert.True(b.Contains(new Vector3(4f, 1f, 1f)));
        Assert.False(b.Contains(new Vector3(0f, 3f, 0f)));

        Assert.True(b.Intersects(new Bounds(new Vector3(5f, 0f, 0f), Vector3.one)));
        Assert.False(b.Intersects(new Bounds(new Vector3(50f, 0f, 0f), Vector3.one)));
    }

    [Fact]
    public void RendererBounds_FollowsTransformPoseAndScale()
    {
        var go = new GameObject("scaled");
        spawned.Add(go);
        go.transform.position = new Vector3(10f, 0f, 0f);
        go.transform.localScale = new Vector3(2f, 4f, 6f);
        var renderer = go.AddComponent<MeshRenderer>();

        var bounds = renderer.bounds;
        Assert.Equal(new Vector3(10f, 0f, 0f), bounds.center);
        Assert.Equal(new Vector3(1f, 2f, 3f), bounds.extents);
    }
}
