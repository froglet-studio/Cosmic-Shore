using System;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C3 — oriented boxes and capsules (Physics/ShapeMath.cs).
//
// Box and mesh colliders are oriented boxes (a mesh is the oriented box of its local
// bounds) and capsules take part, in the trigger pass, in every query and in
// Collider.bounds / Collider.ClosestPoint. Each case below is placed so that the old
// unrotated-AABB answer and the oriented answer DIFFER.
// ─────────────────────────────────────────────────────────────────────────────

public class OrientedShapeTests
{
    const float Dt = 0.02f;

    /// <summary>A long thin box (10 x 1 x 1) rotated 45° about Z: its diagonal runs through (3,3,0).</summary>
    static BoxCollider DiagonalBox(string name, bool isTrigger = true)
    {
        var go = new GameObject(name);
        go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(10f, 1f, 1f);
        box.isTrigger = isTrigger;
        return box;
    }

    [Fact]
    public void RotatedBox_TriggersOnItsDiagonal_NotOnItsUnrotatedFootprint()
    {
        using var loop = new GameLoop();
        var box = DiagonalBox("box");
        var recorder = box.gameObject.AddComponent<TriggerRecorder>();

        // On the rotated diagonal, 4.2 m out: inside the oriented box (half-length 5), but far
        // outside the unrotated 10x1x1 footprint in y.
        var (onDiagonal, _, _) = ContactRig.MakeProbe("diag", new Vector3(3f, 3f, 0f), radius: 0.2f);
        // On the unrotated x axis, 4 m out: inside the old AABB, outside the rotated box.
        var (onAxis, _, _) = ContactRig.MakeProbe("axis", new Vector3(4f, 0f, 0f), radius: 0.2f);

        loop.Tick(Dt);

        Assert.Single(recorder.Events);
        Assert.Same(onDiagonal, recorder.Events[0].other.gameObject);
        Assert.DoesNotContain(recorder.Events, e => e.other.gameObject == onAxis);
    }

    [Fact]
    public void RotatedBoxes_SeparatingAxis_FindsTheGapAnAabbTestMisses()
    {
        using var loop = new GameLoop();
        var a = DiagonalBox("a");
        var recorder = a.gameObject.AddComponent<TriggerRecorder>();

        // A unit cube at (3.5, 0.5, 0): the two AABBs overlap, the oriented boxes do not.
        var goB = new GameObject("b");
        goB.transform.position = new Vector3(3.5f, 0.5f, 0f);
        goB.AddComponent<BoxCollider>();
        loop.Tick(Dt);
        Assert.Empty(recorder.Events);

        // Move it onto the diagonal: now they overlap.
        goB.transform.position = new Vector3(2.5f, 2.5f, 0f);
        loop.Tick(Dt);
        Assert.Single(recorder.Events);
        Assert.Equal("enter", recorder.Events[0].evt);
    }

    [Fact]
    public void Capsule_TakesPartInTriggers_AlongItsAxis()
    {
        using var loop = new GameLoop();
        var go = new GameObject("capsule");
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.radius = 0.5f;
        capsule.height = 6f; // segment y in [-2.5, 2.5]
        capsule.direction = 1;
        capsule.isTrigger = true;
        var recorder = go.AddComponent<TriggerRecorder>();

        var (probe, _, _) = ContactRig.MakeProbe("probe", new Vector3(0.8f, 2.4f, 0f), radius: 0.4f);
        loop.Tick(Dt);
        Assert.Single(recorder.Events); // 0.8 from the axis < 0.5 + 0.4

        probe.transform.position = new Vector3(0f, 3.5f, 0f); // 1.0 past the top cap's centre > 0.9
        loop.Tick(Dt);
        Assert.Equal(2, recorder.Events.Count);
        Assert.Equal("exit", recorder.Events[1].evt);
    }

    [Fact]
    public void CapsuleAgainstRotatedBox_UsesTheSegmentToBoxDistance()
    {
        using var loop = new GameLoop();
        var box = DiagonalBox("box");
        var recorder = box.gameObject.AddComponent<TriggerRecorder>();

        var go = new GameObject("capsule");
        var capsule = go.AddComponent<CapsuleCollider>();
        capsule.radius = 0.25f;
        capsule.height = 4f;
        capsule.direction = 0; // along X: segment x in [-1.75, 1.75]
        // Segment x in [2.25, 5.75] at y = 0.5: inside the box's AABB, but its nearest end is
        // (2.25 - 0.5)/sqrt2 - 0.5 = 0.74 from the oriented box, more than the 0.25 radius.
        go.transform.position = new Vector3(4f, 0.5f, 0f);
        loop.Tick(Dt);
        Assert.Empty(recorder.Events);

        // At y = 2.5 the segment crosses the diagonal.
        go.transform.position = new Vector3(4f, 2.5f, 0f);
        loop.Tick(Dt);
        Assert.Single(recorder.Events);
    }

    [Fact]
    public void MeshCollider_IsTheOrientedBoxOfItsBounds()
    {
        using var loop = new GameLoop();
        var go = new GameObject("mesh");
        go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        var mesh = new Mesh();
        mesh.vertices = new[] { new Vector3(-5f, -0.5f, -0.5f), new Vector3(5f, 0.5f, 0.5f) };
        mesh.RecalculateBounds();
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.isTrigger = true;
        var recorder = go.AddComponent<TriggerRecorder>();

        ContactRig.MakeProbe("diag", new Vector3(3f, 3f, 0f), radius: 0.2f);
        ContactRig.MakeProbe("axis", new Vector3(4f, 0f, 0f), radius: 0.2f);
        loop.Tick(Dt);

        Assert.Single(recorder.Events);
        Assert.Equal("diag", recorder.Events[0].other.gameObject.name);
    }

    [Fact]
    public void Bounds_EnclosesTheRotatedBox()
    {
        using var loop = new GameLoop();
        var box = DiagonalBox("box");
        var b = box.bounds;
        // Rotated 45°: half-extent on x and y = (5 + 0.5) / sqrt2.
        float expected = 5.5f / MathF.Sqrt(2f);
        Assert.Equal(expected, b.extents.x, 3);
        Assert.Equal(expected, b.extents.y, 3);
        Assert.Equal(0.5f, b.extents.z, 3);
    }

    [Fact]
    public void ClosestPoint_IsOnTheSurface_OrThePointWhenInside()
    {
        using var loop = new GameLoop();

        var sphereGo = new GameObject("s");
        var sphere = sphereGo.AddComponent<SphereCollider>();
        sphere.radius = 1f;
        AssertNear(new Vector3(1f, 0f, 0f), sphere.ClosestPoint(new Vector3(5f, 0f, 0f)));
        AssertNear(new Vector3(0.2f, 0f, 0f), sphere.ClosestPoint(new Vector3(0.2f, 0f, 0f)));

        var box = DiagonalBox("box");
        // A point straight "above" the diagonal: the nearest point is on the box's top face.
        var p = box.ClosestPoint(new Vector3(0f, 2f, 0f));
        var up = Quaternion.Euler(0f, 0f, 45f) * Vector3.up;
        Assert.Equal(0.5f, Vector3.Dot(p, up), 3);

        var capGo = new GameObject("c");
        var cap = capGo.AddComponent<CapsuleCollider>();
        cap.radius = 0.5f; cap.height = 4f; cap.direction = 1;
        AssertNear(new Vector3(0.5f, 1f, 0f), cap.ClosestPoint(new Vector3(3f, 1f, 0f)));
        AssertNear(new Vector3(0f, 2f, 0f), cap.ClosestPoint(new Vector3(0f, 9f, 0f)));
    }

    [Fact]
    public void Raycast_HitsRotatedBoxFaces_AndCapsules_WithTheirNormals()
    {
        using var loop = new GameLoop();
        var box = DiagonalBox("box", isTrigger: false);
        loop.Tick(Dt);

        // Straight down onto the diagonal from (0, 5): the top face, 0.5/cos45 above the origin.
        Assert.True(Physics.Raycast(new Vector3(0f, 5f, 0f), Vector3.down, out var hit, 100f));
        Assert.Same(box, hit.collider);
        Assert.Equal(5f - 0.5f * MathF.Sqrt(2f), hit.distance, 3);
        var up = Quaternion.Euler(0f, 0f, 45f) * Vector3.up;
        AssertNear(up, hit.normal);

        // Inside the box's AABB but below the diagonal: no hit (an AABB test would say yes).
        Assert.False(Physics.Raycast(new Vector3(3.5f, 1f, 0f), Vector3.down, out _, 4f));

        var capGo = new GameObject("cap");
        capGo.transform.position = new Vector3(20f, 0f, 0f);
        var cap = capGo.AddComponent<CapsuleCollider>();
        cap.radius = 1f; cap.height = 6f; cap.direction = 1;
        Physics.SyncTransforms();
        Assert.True(Physics.Raycast(new Vector3(10f, 1.5f, 0f), Vector3.right, out var capHit, 100f));
        Assert.Same(cap, capHit.collider);
        Assert.Equal(9f, capHit.distance, 3);
        AssertNear(Vector3.left, capHit.normal);
    }

    [Fact]
    public void OverlapBox_And_CheckBox_HonourOrientation()
    {
        using var loop = new GameLoop();
        var (target, _, _) = ContactRig.MakeProbe("t", new Vector3(3f, 3f, 0f), radius: 0.2f);
        loop.Tick(Dt);

        var half = new Vector3(5f, 0.5f, 0.5f);
        var rot = Quaternion.Euler(0f, 0f, 45f);
        Assert.Contains(target.GetComponent<SphereCollider>(), Physics.OverlapBox(Vector3.zero, half, rot));
        Assert.True(Physics.CheckBox(Vector3.zero, half, rot));
        Assert.False(Physics.CheckBox(Vector3.zero, half)); // unrotated: 3 m above a 0.5 half-height

        // The NonAlloc form (NestedGyroidFlora's overlap audit): same hits into the caller's buffer.
        var buffer = new Collider[4];
        Assert.Equal(1, Physics.OverlapBoxNonAlloc(Vector3.zero, half, buffer, rot, ~0, QueryTriggerInteraction.Collide));
        Assert.Same(target.GetComponent<SphereCollider>(), buffer[0]);
        Assert.Equal(0, Physics.OverlapBoxNonAlloc(Vector3.zero, half, buffer, Quaternion.identity));
        Assert.Equal(0, Physics.OverlapBoxNonAlloc(Vector3.zero, half, System.Array.Empty<Collider>(), rot)); // no room: none written
    }

    [Fact]
    public void OverlapCapsule_IsExact_AgainstARotatedBox()
    {
        using var loop = new GameLoop();
        var box = DiagonalBox("box", isTrigger: false);
        loop.Tick(Dt);
        var results = new Collider[4];
        // A vertical capsule at x = 3.5 reaching y in [-1, 1]: inside the AABB, clear of the oriented box.
        Assert.Equal(0, Physics.OverlapCapsuleNonAlloc(new Vector3(3.5f, -1f, 0f), new Vector3(3.5f, 1f, 0f), 0.1f, results));
        // At x = 3 reaching up to y = 3.2: crosses the diagonal.
        Assert.Equal(1, Physics.OverlapCapsuleNonAlloc(new Vector3(3f, 1f, 0f), new Vector3(3f, 3.2f, 0f), 0.1f, results));
        Assert.Same(box, results[0]);
    }

    static void AssertNear(Vector3 expected, Vector3 actual, float tol = 1e-3f)
        => Assert.True((expected - actual).magnitude <= tol, $"expected {expected}, got {actual}");
}
