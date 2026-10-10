using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C7 session 2: the trigger pass's two per-step costs on a grown arena.
//   • GameObject.activeInHierarchy is a field kept up to date by SetActive, a scene's root
//     activation and every reparent, where it was a memo against one global epoch that any
//     SetActive (each pooled clone's included) invalidated for every object.
//   • A collider keeps its world shape with the transform stamp and the fields it was built
//     from, and the trigger pass rebuilds it only when one of those changed.
// Both must be invisible: the same answers as the chain walk and as a fresh ShapeMath.TryBuild,
// bit for bit, and the same enter/exit stream.
// ─────────────────────────────────────────────────────────────────────────────
public class TriggerPassActivityAndShapeCacheTests
{
    // The definition the field replaces: every ancestor's activeSelf and the object's own.
    static bool Walk(GameObject go)
    {
        for (var t = go.transform; t is not null; t = t.parent)
            if (!t.gameObject.activeSelf) return false;
        return true;
    }

    static void AssertAllMatchTheWalk(IEnumerable<GameObject> all)
    {
        foreach (var go in all)
            Assert.True(go.activeInHierarchy == (!go.IsDestroyed && Walk(go)), $"'{go.name}': activeInHierarchy {go.activeInHierarchy}, chain walk {Walk(go)}");
    }

    /// <summary>root → a → b → c → d plus a sibling under each level (a2 under root, b2 under a, ...).</summary>
    static List<GameObject> Tree(out GameObject root, out GameObject[] chain)
    {
        var all = new List<GameObject>();
        root = new GameObject("root"); all.Add(root);
        chain = new GameObject[5];
        chain[0] = root;
        for (int i = 1; i < chain.Length; i++)
        {
            chain[i] = new GameObject($"n{i}");
            chain[i].transform.SetParent(chain[i - 1].transform, false);
            var sibling = new GameObject($"s{i}");
            sibling.transform.SetParent(chain[i - 1].transform, false);
            all.Add(chain[i]); all.Add(sibling);
        }
        return all;
    }

    [Fact]
    public void ActiveInHierarchy_EqualsTheChainWalk_AfterEveryKindOfChange()
    {
        using var loop = new GameLoop();
        var all = Tree(out var root, out var chain);
        AssertAllMatchTheWalk(all);

        chain[2].SetActive(false); AssertAllMatchTheWalk(all);       // a middle node: its subtree goes
        chain[4].SetActive(false); AssertAllMatchTheWalk(all);       // a leaf under it: nothing effective changes
        chain[2].SetActive(true); AssertAllMatchTheWalk(all);        // back: the leaf stays off, the rest returns
        Assert.False(chain[4].activeInHierarchy); Assert.True(chain[3].activeInHierarchy);
        chain[4].SetActive(true); AssertAllMatchTheWalk(all);
        root.SetActive(false); AssertAllMatchTheWalk(all);           // the root: everything off
        Assert.All(all, go => Assert.False(go.activeInHierarchy));
        root.SetActive(true); AssertAllMatchTheWalk(all);

        // Reparenting under an inactive parent, then under an active one, then to the top level.
        var parked = new GameObject("parked"); all.Add(parked);
        parked.SetActive(false);
        chain[1].transform.SetParent(parked.transform, false); AssertAllMatchTheWalk(all);
        Assert.False(chain[4].activeInHierarchy);
        chain[1].transform.SetParent(root.transform, false); AssertAllMatchTheWalk(all);
        Assert.True(chain[4].activeInHierarchy);
        chain[3].transform.SetParent(null, false); AssertAllMatchTheWalk(all);
        chain[3].transform.SetParent(chain[2].transform, false); AssertAllMatchTheWalk(all);

        // A subtree activated under an inactive parent stays off until the parent comes back.
        parked.transform.SetParent(chain[2].transform, false);
        var under = new GameObject("under"); all.Add(under);
        under.transform.SetParent(parked.transform, false);
        under.SetActive(false); under.SetActive(true); AssertAllMatchTheWalk(all);
        Assert.False(under.activeInHierarchy);
        parked.SetActive(true); AssertAllMatchTheWalk(all);
        Assert.True(under.activeInHierarchy);

        // A destroyed node is inactive; its former children (destroyed with it) too.
        Object.Destroy(chain[2]);
        loop.Tick(0.02f);
        AssertAllMatchTheWalk(all);
        Assert.False(chain[3].activeInHierarchy);
    }

    [Fact]
    public void ActiveInHierarchy_FollowsASceneRootActivation()
    {
        using var loop = new GameLoop();
        var all = Tree(out var root, out var chain);
        root.SetActive(false);
        chain[3].SetActive(false);
        Assert.All(all, go => Assert.False(go.activeInHierarchy));

        GameObject.ActivateSceneRoots(new[] { root }, beforeLifecycle: null);
        AssertAllMatchTheWalk(all);
        Assert.True(chain[2].activeInHierarchy);
        Assert.False(chain[3].activeInHierarchy);
        Assert.False(chain[4].activeInHierarchy);
    }

    [Fact]
    public void TriggerPass_DropsAndRestoresADeepSubtreesColliders_OnItsRootsActivity()
    {
        using var loop = new GameLoop();
        Time.fixedDeltaTime = 0.02f;
        var (_, _, recorderA) = ContactRig.MakeProbe("A", Vector3.zero, isTrigger: true);
        Tree(out var root, out var chain);
        var (goB, colliderB, _) = ContactRig.MakeProbe("B", Vector3.zero, body: false);
        goB.transform.SetParent(chain[4].transform, false);

        loop.Tick(0.02f);
        Assert.Equal("enter", Assert.Single(recorderA.Events).evt);

        root.SetActive(false);            // five levels above B
        loop.Tick(0.02f);
        Assert.Equal("exit", recorderA.Events[^1].evt);
        Assert.Equal(2, recorderA.Events.Count);

        root.SetActive(true);
        loop.Tick(0.02f);
        Assert.Equal("enter", recorderA.Events[^1].evt);
        Assert.Equal(3, recorderA.Events.Count);

        // Reparent B's branch under a parked (inactive) object and back.
        var parked = new GameObject("parked");
        parked.SetActive(false);
        chain[1].transform.SetParent(parked.transform, false);
        loop.Tick(0.02f);
        Assert.Equal("exit", recorderA.Events[^1].evt);
        chain[1].transform.SetParent(root.transform, false);
        loop.Tick(0.02f);
        Assert.Equal("enter", recorderA.Events[^1].evt);
        Assert.Equal(5, recorderA.Events.Count);
        Assert.Same(colliderB, recorderA.Events[^1].other);
    }

    [Fact]
    public void LiveSet_MaintainedByEvents_EqualsTheWalkOverEveryRegisteredCollider()
    {
        using var loop = new GameLoop();
        var pass = loop.Triggers;
        var all = Tree(out var root, out var chain);
        var colliders = new List<Collider>();
        foreach (var go in all) colliders.Add(go.AddComponent<BoxCollider>());
        void Check(int expectedLive)
        {
            Assert.True(pass.LiveSetMatchesWalk(out int live, out int walked), $"live set {live} vs walk {walked}");
            Assert.Equal(expectedLive, live);
        }
        Check(9);

        // all = root, n1, s1, n2, s2, n3, s3, n4, s4 (colliders[k] sits on all[k]); chain = root, n1, n2, n3, n4.
        chain[2].SetActive(false); Check(4);                 // n2's subtree (n2, n3, s3, n4, s4) leaves
        colliders[3].enabled = false; Check(4);              // n2's collider, on an inactive object: no change
        chain[2].SetActive(true); Check(8);                  // the subtree returns, minus the disabled one
        colliders[3].enabled = true; Check(9);
        colliders[0].enabled = false; Check(8);              // the root's collider
        root.SetActive(false); Check(0);
        root.SetActive(true); Check(8);

        var parked = new GameObject("parked"); parked.SetActive(false);
        chain[1].transform.SetParent(parked.transform, false); Check(1);   // n1's subtree (7) leaves: s1 remains
        chain[1].transform.SetParent(root.transform, false); Check(8);

        // A collider added to an inactive object becomes live with its activation; one added to
        // an active object is live at once; a destroyed object's colliders are gone with it.
        var late = new GameObject("late"); late.SetActive(false);
        late.transform.SetParent(chain[3].transform, false);
        var lateCollider = late.AddComponent<SphereCollider>(); Check(8);
        late.SetActive(true); Check(9);
        chain[4].AddComponent<SphereCollider>(); Check(10);
        Object.Destroy(chain[3]);                            // n3, n4 (two colliders), s4, late
        loop.Tick(0.02f);
        Check(5);
        Assert.False(lateCollider.isActiveAndEnabled);

        // A scene root activation.
        var fresh = new GameObject("fresh"); fresh.SetActive(false);
        var freshChild = new GameObject("freshChild"); freshChild.transform.SetParent(fresh.transform, false);
        freshChild.AddComponent<BoxCollider>();
        Check(5);
        GameObject.ActivateSceneRoots(new[] { fresh }, beforeLifecycle: null);
        Check(6);
    }

    static void AssertCachedEqualsFresh(Collider c)
    {
        bool cached = c.TryGetShape(out var fromCache);
        bool fresh = ShapeMath.TryBuild(c, out var built);
        Assert.Equal(fresh, cached);
        if (fresh) Assert.True(ShapeMath.Same(in fromCache, in built), $"cached shape of '{c.name}' differs from a fresh build");
        Assert.True(c.ShapeCacheHolds);
    }

    [Fact]
    public void ShapeCache_IsInvalidatedByAnyAncestorPoseOrOwnFieldChange_AndIsBitIdentical()
    {
        using var loop = new GameLoop();
        var top = new GameObject("top");
        var mid = new GameObject("mid"); mid.transform.SetParent(top.transform, false);
        var leaf = new GameObject("leaf"); leaf.transform.SetParent(mid.transform, false);
        top.transform.localPosition = new Vector3(1f, 2f, 3f);
        top.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
        mid.transform.localScale = new Vector3(2f, 1f, 0.5f);
        leaf.transform.localPosition = new Vector3(0.3f, -0.7f, 0.1f);

        var box = leaf.AddComponent<BoxCollider>();
        var sphere = leaf.AddComponent<SphereCollider>();
        var capsule = leaf.AddComponent<CapsuleCollider>();
        var meshCollider = leaf.AddComponent<MeshCollider>();
        var mesh = new Mesh { bounds = new Bounds(new Vector3(0.5f, 0f, 0f), new Vector3(2f, 4f, 6f)) };
        meshCollider.sharedMesh = mesh;
        var colliders = new Collider[] { box, sphere, capsule, meshCollider };

        foreach (var c in colliders) { Assert.False(c.ShapeCacheHolds); AssertCachedEqualsFresh(c); }

        // The grandparent moves: every cached shape is stale, and the rebuilt ones match a fresh build.
        top.transform.localPosition = new Vector3(5f, 2f, 3f);
        foreach (var c in colliders) Assert.False(c.ShapeCacheHolds);
        foreach (var c in colliders) AssertCachedEqualsFresh(c);
        mid.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        foreach (var c in colliders) { Assert.False(c.ShapeCacheHolds); AssertCachedEqualsFresh(c); }
        mid.transform.localScale = new Vector3(3f, 3f, 3f);
        foreach (var c in colliders) { Assert.False(c.ShapeCacheHolds); AssertCachedEqualsFresh(c); }
        // Reparenting changes the world pose too.
        leaf.transform.SetParent(top.transform, false);
        foreach (var c in colliders) { Assert.False(c.ShapeCacheHolds); AssertCachedEqualsFresh(c); }
        // A write of the same value is not a change (the world cache compares bitwise).
        top.transform.localPosition = new Vector3(5f, 2f, 3f);
        foreach (var c in colliders) Assert.True(c.ShapeCacheHolds);

        // Own fields.
        box.size = new Vector3(2f, 2f, 2f); Assert.False(box.ShapeCacheHolds); Assert.True(sphere.ShapeCacheHolds); AssertCachedEqualsFresh(box);
        box.center = new Vector3(0f, 1f, 0f); Assert.False(box.ShapeCacheHolds); AssertCachedEqualsFresh(box);
        box.isTrigger = true; Assert.False(box.ShapeCacheHolds); AssertCachedEqualsFresh(box);
        Assert.True(box.TryGetShape(out var boxShape) && boxShape.Trigger);
        sphere.radius = 3f; Assert.False(sphere.ShapeCacheHolds); AssertCachedEqualsFresh(sphere);
        sphere.center = new Vector3(1f, 0f, 0f); Assert.False(sphere.ShapeCacheHolds); AssertCachedEqualsFresh(sphere);
        capsule.height = 5f; Assert.False(capsule.ShapeCacheHolds); AssertCachedEqualsFresh(capsule);
        capsule.direction = 0; Assert.False(capsule.ShapeCacheHolds); AssertCachedEqualsFresh(capsule);
        capsule.radius = 0.25f; Assert.False(capsule.ShapeCacheHolds); AssertCachedEqualsFresh(capsule);
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 1f)); Assert.False(meshCollider.ShapeCacheHolds); AssertCachedEqualsFresh(meshCollider);
        meshCollider.sharedMesh = new Mesh { bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 1f)) }; Assert.False(meshCollider.ShapeCacheHolds); AssertCachedEqualsFresh(meshCollider);
        meshCollider.sharedMesh = null; Assert.False(meshCollider.ShapeCacheHolds);
        Assert.False(meshCollider.TryGetShape(out _));
        Assert.True(meshCollider.ShapeCacheHolds);
    }

    [Fact]
    public void TriggerPass_SeesAnAncestorsMoveAndScale_ThroughTheCachedShape()
    {
        using var loop = new GameLoop();
        Time.fixedDeltaTime = 0.02f;
        var (_, _, recorderA) = ContactRig.MakeProbe("A", Vector3.zero, isTrigger: true);
        var carrier = new GameObject("carrier");
        carrier.transform.position = new Vector3(10f, 0f, 0f);
        var (goB, _, _) = ContactRig.MakeProbe("B", Vector3.zero, body: false);
        goB.transform.SetParent(carrier.transform, false);

        loop.Tick(0.02f);
        Assert.Empty(recorderA.Events);

        carrier.transform.position = new Vector3(1.5f, 0f, 0f);     // the parent moves; B's own transform is untouched
        loop.Tick(0.02f);
        Assert.Equal("enter", Assert.Single(recorderA.Events).evt);

        carrier.transform.position = new Vector3(10f, 0f, 0f);
        loop.Tick(0.02f);
        Assert.Equal("exit", recorderA.Events[^1].evt);

        carrier.transform.localScale = new Vector3(12f, 12f, 12f);  // B's world radius grows to 12: it reaches A again
        loop.Tick(0.02f);
        Assert.Equal("enter", recorderA.Events[^1].evt);
        Assert.Equal(3, recorderA.Events.Count);
    }

    [Fact]
    public void TriggerPass_LeavesNoReadOnlyPassOpen()
    {
        using var loop = new GameLoop();
        Time.fixedDeltaTime = 0.02f;
        var (go, _, _) = ContactRig.MakeProbe("P", Vector3.zero);
        loop.Tick(0.02f);
        go.transform.position = new Vector3(7f, 0f, 0f);
        Assert.Equal(7f, go.transform.position.x);
        go.transform.position = new Vector3(8f, 0f, 0f);
        Assert.Equal(8f, go.transform.position.x);
    }
}
