using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// Engine spatial query: Physics.OverlapSphereNonAlloc backed by the TriggerPass
// collider registry. This is the original-contract query Boid behavior scans with
// (cohesion/separation/prism interaction) — prerequisite for the concrete fauna arc.
public class PhysicsOverlapTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    GameObject MakeSphere(Vector3 position, float radius = 1f, bool trigger = false)
    {
        var go = new GameObject($"sphere@{position}");
        spawned.Add(go);
        go.transform.position = position;
        var collider = go.AddComponent<SphereCollider>();
        collider.radius = radius;
        collider.isTrigger = trigger;
        return go;
    }

    GameObject MakeBox(Vector3 position, Vector3 size)
    {
        var go = new GameObject($"box@{position}");
        spawned.Add(go);
        go.transform.position = position;
        var collider = go.AddComponent<BoxCollider>();
        collider.size = size;
        return go;
    }

    [Fact]
    public void OverlapSphere_FindsTriggerAndNonTriggerCollidersInRange()
    {
        var near = MakeSphere(new Vector3(3f, 0f, 0f));                    // non-trigger, in range
        var nearTrigger = MakeSphere(new Vector3(0f, 3f, 0f), trigger: true);
        var nearBox = MakeBox(new Vector3(0f, 0f, 4f), new Vector3(2f, 2f, 2f));
        MakeSphere(new Vector3(100f, 0f, 0f));                             // out of range

        var results = new Collider[8];
        int count = Physics.OverlapSphereNonAlloc(Vector3.zero, 5f, results);

        Assert.Equal(3, count);
        var hits = new HashSet<GameObject>();
        for (int i = 0; i < count; i++) hits.Add(results[i].gameObject);
        Assert.Contains(near, hits);
        Assert.Contains(nearTrigger, hits);
        Assert.Contains(nearBox, hits);
    }

    [Fact]
    public void OverlapSphere_TruncatesAtBufferCapacity_InRegistrationOrder()
    {
        var first = MakeSphere(new Vector3(1f, 0f, 0f));
        var second = MakeSphere(new Vector3(2f, 0f, 0f));
        MakeSphere(new Vector3(3f, 0f, 0f));

        var results = new Collider[2];
        int count = Physics.OverlapSphereNonAlloc(Vector3.zero, 10f, results);

        Assert.Equal(2, count);
        Assert.Same(first, results[0].gameObject);   // deterministic registration order
        Assert.Same(second, results[1].gameObject);
    }

    [Fact]
    public void OverlapSphere_SkipsDisabledColliders_AndMissesByRadius()
    {
        var active = MakeSphere(new Vector3(2f, 0f, 0f));
        var disabled = MakeSphere(new Vector3(1f, 0f, 0f));
        disabled.SetActive(false);

        var results = new Collider[4];
        int count = Physics.OverlapSphereNonAlloc(Vector3.zero, 4f, results);
        Assert.Equal(1, count);
        Assert.Same(active, results[0].gameObject);

        // Sphere surfaces 1 apart with radii 1+0.5: query radius 0.4 misses (gap 1 - 1 - 0.4 > 0... )
        Assert.Equal(0, Physics.OverlapSphereNonAlloc(new Vector3(10f, 0f, 0f), 0.5f, results));
    }

    [Fact]
    public void OverlapSphere_BoxEdgeContact_CountsAsOverlap()
    {
        // Box spans x ∈ [4, 6]; query sphere of radius 4 from origin touches at x=4 exactly.
        MakeBox(new Vector3(5f, 0f, 0f), new Vector3(2f, 2f, 2f));
        var results = new Collider[2];
        Assert.Equal(1, Physics.OverlapSphereNonAlloc(Vector3.zero, 4f, results));
        Assert.Equal(0, Physics.OverlapSphereNonAlloc(Vector3.zero, 3.9f, results));
    }
}
