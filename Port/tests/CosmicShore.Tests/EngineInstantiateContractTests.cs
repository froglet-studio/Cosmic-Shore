using System.Collections.Generic;
using CosmicShore.Engine;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Engine Instantiate — ORIGINAL-CONTRACT fixes (freestyle arc):
//
//  1. Deferred clone activation. The original engine initializes the ENTIRE clone
//     (hierarchy + serialized data) before any lifecycle hook runs, and
//     Instantiate(original, position, rotation) applies the pose BEFORE Awake.
//     The previous per-component AddComponent path fired Awake/OnEnable mid-clone
//     on ACTIVE templates — before CopyFields — so hooks saw default fields, and
//     CopyFields then clobbered anything Awake had cached (children were built as
//     free-standing active roots, hitting this even for the root-deferred case).
//
//  2. HashSet<T> joins the E16 container rule (Array/List/Dictionary): the
//     original engine never shares a mutable container instance between a
//     template and its clone. Found via BranchingFlora.activeBranches — every
//     flora clone grew (and dropped its guaranteed initial leaf) on ONE shared
//     trunk set, so two of three flora were born leafless and failsafe-died.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Caches its serialized fields at Awake/OnEnable time — the observer for fix 1.</summary>
class InstantiateOrderProbe : MonoBehaviour
{
    [SerializeField] public int authored;                 // set on the template AFTER AddComponent
    [SerializeField] public BoxCollider sibling;          // intra-tree ref, wired on the template

    public int AwakeCount;
    public int AuthoredAtAwake = -1;
    public BoxCollider SiblingAtAwake;
    public Vector3 PositionAtAwake;
    public int AuthoredAtEnable = -1;

    void Awake()
    {
        AwakeCount++;
        AuthoredAtAwake = authored;
        SiblingAtAwake = sibling;
        PositionAtAwake = transform.position;
    }

    void OnEnable() => AuthoredAtEnable = authored;
}

/// <summary>Runtime HashSet field (initializer-created, like BranchingFlora.activeBranches).</summary>
class HashSetOwnerProbe : MonoBehaviour
{
    public HashSet<string> Names = new();
    [SerializeField] public HashSet<GameObject> Members = new();
}

public class EngineInstantiateContractTests : System.IDisposable
{
    readonly GameLoop loop = new();
    public void Dispose() => loop.Dispose();

    static (GameObject template, InstantiateOrderProbe probe) BuildActiveTemplate()
    {
        // Active template under an INACTIVE shelf: activeSelf true, lifecycle inert —
        // the prefab-shelf pattern the freestyle client uses for ecology templates.
        var shelf = new GameObject("shelf");
        shelf.SetActive(false);
        var template = new GameObject("template");
        template.transform.SetParent(shelf.transform, false);
        var collider = template.AddComponent<BoxCollider>();
        var probe = template.AddComponent<InstantiateOrderProbe>();
        probe.authored = 42;          // authored AFTER AddComponent — only CopyFields carries it
        probe.sibling = collider;     // intra-tree ref — must remap to the clone's collider
        return (template, probe);
    }

    [Fact]
    public void ActiveTemplateClone_RunsAwakeOnce_AfterFieldsAndPoseAreInPlace()
    {
        var (template, _) = BuildActiveTemplate();

        var clone = Object.Instantiate(template, new Vector3(10f, 20f, 30f), Quaternion.identity);
        var probe = clone.GetComponent<InstantiateOrderProbe>();

        Assert.True(clone.activeInHierarchy);
        Assert.Equal(1, probe.AwakeCount);
        Assert.Equal(42, probe.AuthoredAtAwake);                       // fields copied BEFORE Awake
        Assert.Equal(42, probe.AuthoredAtEnable);
        Assert.Same(clone.GetComponent<BoxCollider>(), probe.SiblingAtAwake); // remapped BEFORE Awake
        Assert.Equal(new Vector3(10f, 20f, 30f), probe.PositionAtAwake);      // pose applied BEFORE Awake
        Assert.Equal(42, probe.authored);
    }

    [Fact]
    public void ActiveTemplateChildComponents_AlsoAwakeAfterCopy()
    {
        // The child variant — the actual freestyle bug: children were built as
        // free-standing ACTIVE roots pre-parenting, so their Awake fired at
        // AddComponent time and CopyFields clobbered whatever Awake cached.
        var shelf = new GameObject("shelf");
        shelf.SetActive(false);
        var template = new GameObject("root");
        template.transform.SetParent(shelf.transform, false);
        var child = new GameObject("child");
        child.transform.SetParent(template.transform, false);
        var childCollider = child.AddComponent<BoxCollider>();
        var childProbe = child.AddComponent<InstantiateOrderProbe>();
        childProbe.authored = 7;
        childProbe.sibling = childCollider;

        var clone = Object.Instantiate(template, new Vector3(1f, 2f, 3f), Quaternion.identity);
        var cloneProbe = clone.GetComponentInChildren<InstantiateOrderProbe>(true);

        Assert.Equal(1, cloneProbe.AwakeCount);
        Assert.Equal(7, cloneProbe.AuthoredAtAwake);
        Assert.Same(cloneProbe.GetComponent<BoxCollider>(), cloneProbe.SiblingAtAwake);
    }

    [Fact]
    public void InactiveTemplateClone_StaysInactive_NoHooksRun()
    {
        // Regression guard for the pre-existing prefab flows (SkimRace/C6): an
        // inactive template clones inactive; lifecycle waits for SetActive(true).
        var (template, _) = BuildActiveTemplate();
        template.SetActive(false);

        var clone = Object.Instantiate(template);
        var probe = clone.GetComponent<InstantiateOrderProbe>();

        Assert.False(clone.activeSelf);
        Assert.Equal(0, probe.AwakeCount);

        clone.SetActive(true);
        Assert.Equal(1, probe.AwakeCount);
        Assert.Equal(42, probe.AuthoredAtAwake);
    }

    [Fact]
    public void HashSetFields_GetFreshContainersPerClone()
    {
        var shelf = new GameObject("shelf");
        shelf.SetActive(false);
        var template = new GameObject("owner");
        template.transform.SetParent(shelf.transform, false);
        var owner = template.AddComponent<HashSetOwnerProbe>();
        owner.Names.Add("seed");
        var insider = new GameObject("insider");
        insider.transform.SetParent(template.transform, false);
        var outsider = new GameObject("outsider");
        owner.Members.Add(insider);
        owner.Members.Add(outsider);

        var cloneA = Object.Instantiate(template).GetComponent<HashSetOwnerProbe>();
        var cloneB = Object.Instantiate(template).GetComponent<HashSetOwnerProbe>();

        // Fresh container per clone — never the template's reference (the
        // BranchingFlora.activeBranches cross-clone contamination).
        Assert.NotSame(owner.Names, cloneA.Names);
        Assert.NotSame(cloneA.Names, cloneB.Names);
        Assert.Contains("seed", cloneA.Names);

        cloneA.Names.Add("a-only");
        Assert.DoesNotContain("a-only", owner.Names);
        Assert.DoesNotContain("a-only", cloneB.Names);

        // Element rules match List/Array: intra-tree refs remap to the clone's
        // counterpart; outside refs stay shared.
        Assert.NotSame(owner.Members, cloneA.Members);
        GameObject cloneInsider = null;
        foreach (Transform child in cloneA.transform)
            if (child.gameObject.name == "insider") cloneInsider = child.gameObject;
        Assert.NotNull(cloneInsider);
        Assert.Contains(cloneInsider, cloneA.Members);
        Assert.Contains(outsider, cloneA.Members);
        Assert.DoesNotContain(insider, cloneA.Members);
    }
}
