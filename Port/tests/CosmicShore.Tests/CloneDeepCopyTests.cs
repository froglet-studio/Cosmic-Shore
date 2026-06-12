using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// E16 deep-clone prerequisite (C6 follow-up): plain [Serializable] object graphs
// must deep-clone engine-inline style. Before this, CloneGameObject remapped
// in-tree Component/GameObject/Transform references but copied plain-object
// collections and plain serializable objects BY REFERENCE — so every vessel
// spawned from one template shared its ResourceSystem.Resources list (and the
// Resource instances inside it) with the template and with each other.
//
// Semantics under test (see ObjectUtilities.RemapValue):
//   • plain [Serializable] class fields → independent deep clone per field path
//     (the original engine inlines plain classes by value — template aliasing
//     does not survive);
//   • List<T>/T[] → fresh containers always; plain-class elements deep-cloned,
//     in-tree engine refs remapped, external refs + nulls verbatim;
//   • ScriptableObject references → kept (assets are shared by design);
//   • depth cap 10 (the engine's serialization depth limit) truncates nested
//     plain graphs to null — doubling as the cycle guard;
//   • delegate/event fields inside deep-cloned plain objects reset to null
//     (runtime wiring is never serialized);
//   • non-[Serializable] plain classes keep the port's reference-copy behavior.
// ─────────────────────────────────────────────────────────────────────────────
public class CloneDeepCopyTests
{
    // ══════════════════════════════════════════════════════════════════════
    // The C6 scenario — the REAL ResourceSystem/Resource types.
    // ══════════════════════════════════════════════════════════════════════

    static (GameObject Template, ResourceSystem System) BuildVesselTemplate()
    {
        var root = new GameObject("VesselTemplate");
        root.SetActive(false); // template rigs are configuration, not live objects — keep Awake quiet
        var system = root.AddComponent<ResourceSystem>();
        system.Resources = new List<Resource>
        {
            new() { Name = "boost", resourceGainRate = 0.04f },
            new() { Name = "ammo", resourceGainRate = 0.10f },
        };
        return (root, system);
    }

    [Fact]
    public void Clone_VesselResourceSystem_GetsItsOwnResourceInstances_C6Scenario()
    {
        using var loop = new GameLoop();
        var (template, templateSystem) = BuildVesselTemplate();
        templateSystem.Resources[0].CurrentAmount = 0.5f;

        var clone = Object.Instantiate(template);
        var cloneSystem = clone.GetComponent<ResourceSystem>();

        // Fresh list, fresh Resource instances.
        Assert.NotSame(templateSystem.Resources, cloneSystem.Resources);
        Assert.Equal(2, cloneSystem.Resources.Count);
        Assert.NotSame(templateSystem.Resources[0], cloneSystem.Resources[0]);
        Assert.NotSame(templateSystem.Resources[1], cloneSystem.Resources[1]);

        // Authored data carried over (copy-everything pragmatics include runtime state).
        Assert.Equal("boost", cloneSystem.Resources[0].Name);
        Assert.Equal(0.04f, cloneSystem.Resources[0].resourceGainRate);
        Assert.Equal(1f, cloneSystem.Resources[0].MaxAmount);
        Assert.Equal(0.5f, cloneSystem.Resources[0].CurrentAmount);

        // The C6 corruption: mutating the clone's resource must not touch the template's…
        cloneSystem.Resources[0].CurrentAmount = 0.9f;
        cloneSystem.Resources[0].resourceGainRate = 99f;
        Assert.Equal(0.5f, templateSystem.Resources[0].CurrentAmount);
        Assert.Equal(0.04f, templateSystem.Resources[0].resourceGainRate);

        // …and vice versa.
        templateSystem.Resources[1].CurrentAmount = 0.25f;
        Assert.Equal(0f, cloneSystem.Resources[1].CurrentAmount);
    }

    [Fact]
    public void Clone_ManyVesselsFromOneTemplate_HaveIndependentResourceState()
    {
        using var loop = new GameLoop();
        var (template, templateSystem) = BuildVesselTemplate();

        var a = Object.Instantiate(template).GetComponent<ResourceSystem>();
        var b = Object.Instantiate(template).GetComponent<ResourceSystem>();
        var c = Object.Instantiate(template).GetComponent<ResourceSystem>();

        a.Resources[0].CurrentAmount = 0.1f;
        b.Resources[0].CurrentAmount = 0.5f;
        c.Resources[0].CurrentAmount = 1.0f;

        Assert.Equal(0.1f, a.Resources[0].CurrentAmount);
        Assert.Equal(0.5f, b.Resources[0].CurrentAmount);
        Assert.Equal(1.0f, c.Resources[0].CurrentAmount);
        Assert.Equal(0f, templateSystem.Resources[0].CurrentAmount);
    }

    [Fact]
    public void Clone_ResourceEvents_DoNotCarryTemplateSubscribers()
    {
        using var loop = new GameLoop();
        var (template, templateSystem) = BuildVesselTemplate();

        int templateFired = 0;
        templateSystem.Resources[0].OnResourceChange += _ => templateFired++;

        var cloneSystem = Object.Instantiate(template).GetComponent<ResourceSystem>();

        // Would invoke the template's handler if the delegate field had been memberwise-carried.
        cloneSystem.Resources[0].CurrentAmount = 0.7f;
        Assert.Equal(0, templateFired);

        // Template wiring still intact for the template's own resource.
        templateSystem.Resources[0].CurrentAmount = 0.3f;
        Assert.Equal(1, templateFired);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Synthetic rig for nested graphs, aliasing, depth cap, and the C1
    // engine-reference regression guard.
    // ══════════════════════════════════════════════════════════════════════

    [Serializable]
    class TuningData
    {
        public int value;
    }

    struct PackedData
    {
        public int a;
        public float b;
    }

    [Serializable]
    class PrismProperties
    {
        public string label = "authored";
        public float mass = 1f;
        public PackedData packed;
        public TuningData tuning;
        public Transform attachPoint;          // in-tree engine ref inside a plain object
        public RigPart externalPart;           // external engine ref inside a plain object
        public ScriptableObject sharedConfig;  // asset ref inside a plain object
    }

    [Serializable]
    class ChainNode
    {
        public int id;
        public ChainNode next;
    }

    class RuntimeOnly // intentionally NOT [Serializable]
    {
        public int marker;
    }

    class RigPart : MonoBehaviour { }

    class DeepRig : MonoBehaviour
    {
        public PrismProperties prismProperties;
        public PrismProperties sharedA;        // sharedA and sharedB alias ONE template instance
        public PrismProperties sharedB;
        public List<PrismProperties> propsList;
        public PrismProperties[] propsArray;
        public ChainNode chain;
        public ChainNode cycle;
        public RuntimeOnly runtimeOnly;
        public List<float> floatList;
        public int[] intArray;
        public RigPart inTreePart;             // C1 regression guard: direct component ref
        public List<RigPart> partList;         // C1 regression guard: list of component refs
        public ScriptableObject configAsset;
    }

    sealed class Fixture
    {
        public GameObject Root;
        public DeepRig Rig;
        public Transform AttachPoint;
        public RigPart InTreePart;
        public RigPart ExternalPart;
        public ScriptableObject Config;
        public PrismProperties Shared;
    }

    static Fixture BuildDeepRig()
    {
        var f = new Fixture();
        f.Root = new GameObject("DeepRig");
        f.Rig = f.Root.AddComponent<DeepRig>();

        var attachGO = new GameObject("AttachPoint");
        attachGO.transform.SetParent(f.Root.transform, worldPositionStays: false);
        f.AttachPoint = attachGO.transform;
        f.InTreePart = attachGO.AddComponent<RigPart>();

        var externalGO = new GameObject("ExternalPart");
        f.ExternalPart = externalGO.AddComponent<RigPart>();

        f.Config = ScriptableObject.CreateInstance<SO_ProfileIconList>();

        f.Shared = new PrismProperties { label = "shared", mass = 7f, tuning = new TuningData { value = 3 } };

        f.Rig.prismProperties = new PrismProperties
        {
            label = "primary",
            mass = 2.5f,
            packed = new PackedData { a = 4, b = 1.5f },
            tuning = new TuningData { value = 11 },
            attachPoint = f.AttachPoint,
            externalPart = f.ExternalPart,
            sharedConfig = f.Config,
        };
        f.Rig.sharedA = f.Shared;
        f.Rig.sharedB = f.Shared;
        f.Rig.propsList = new List<PrismProperties>
        {
            new() { label = "L0", tuning = new TuningData { value = 1 } },
            null,
            f.Shared,
        };
        f.Rig.propsArray = new[] { new PrismProperties { label = "A0" }, null };
        f.Rig.chain = BuildChain(12);
        f.Rig.cycle = new ChainNode { id = 100 };
        f.Rig.cycle.next = f.Rig.cycle;
        f.Rig.runtimeOnly = new RuntimeOnly { marker = 5 };
        f.Rig.floatList = new List<float> { 1f, 2f, 3f };
        f.Rig.intArray = new[] { 10, 20 };
        f.Rig.inTreePart = f.InTreePart;
        f.Rig.partList = new List<RigPart> { f.InTreePart, f.ExternalPart, null };
        f.Rig.configAsset = f.Config;
        return f;
    }

    static ChainNode BuildChain(int length)
    {
        var head = new ChainNode { id = 1 };
        var current = head;
        for (int i = 2; i <= length; i++)
        {
            current.next = new ChainNode { id = i };
            current = current.next;
        }
        return head;
    }

    /// <summary>Walk a chain, stopping at null or the first revisited node (cyclic templates).</summary>
    static List<ChainNode> Walk(ChainNode head)
    {
        var seen = new List<ChainNode>();
        var visited = new HashSet<ChainNode>();
        for (var node = head; node is not null && visited.Add(node); node = node.next)
            seen.Add(node);
        return seen;
    }

    [Fact]
    public void Clone_PlainSerializableField_DeepCopies_NestedGraphAndValues()
    {
        using var loop = new GameLoop();
        var f = BuildDeepRig();

        var cloneRig = Object.Instantiate(f.Root).GetComponent<DeepRig>();

        // New instance at every plain-object level…
        Assert.NotSame(f.Rig.prismProperties, cloneRig.prismProperties);
        Assert.NotSame(f.Rig.prismProperties.tuning, cloneRig.prismProperties.tuning);

        // …with strings/primitives/structs value-copied.
        Assert.Equal("primary", cloneRig.prismProperties.label);
        Assert.Equal(2.5f, cloneRig.prismProperties.mass);
        Assert.Equal(4, cloneRig.prismProperties.packed.a);
        Assert.Equal(1.5f, cloneRig.prismProperties.packed.b);
        Assert.Equal(11, cloneRig.prismProperties.tuning.value);

        // Mutating the clone's nested graph leaves the template untouched.
        cloneRig.prismProperties.tuning.value = 999;
        cloneRig.prismProperties.mass = -1f;
        cloneRig.prismProperties.packed.a = -1;
        Assert.Equal(11, f.Rig.prismProperties.tuning.value);
        Assert.Equal(2.5f, f.Rig.prismProperties.mass);
        Assert.Equal(4, f.Rig.prismProperties.packed.a);
    }

    [Fact]
    public void Clone_EngineRefsInsidePlainObjects_RemapInTree_KeepExternalAndAssets()
    {
        using var loop = new GameLoop();
        var f = BuildDeepRig();

        var clone = Object.Instantiate(f.Root);
        var cloneRig = clone.GetComponent<DeepRig>();
        var cloneAttach = clone.transform.GetChild(0);

        // In-tree Transform reference inside the deep-cloned plain object points at the
        // CLONE's own child, not the template's.
        Assert.Same(cloneAttach, cloneRig.prismProperties.attachPoint);
        Assert.NotSame(f.AttachPoint, cloneRig.prismProperties.attachPoint);

        // External component and ScriptableObject asset references stay shared.
        Assert.Same(f.ExternalPart, cloneRig.prismProperties.externalPart);
        Assert.Same(f.Config, cloneRig.prismProperties.sharedConfig);

        // Template untouched.
        Assert.Same(f.AttachPoint, f.Rig.prismProperties.attachPoint);
    }

    [Fact]
    public void Clone_CollectionsOfPlainObjects_GetFreshContainersAndElements()
    {
        using var loop = new GameLoop();
        var f = BuildDeepRig();
        var templateList = f.Rig.propsList;
        var templateArray = f.Rig.propsArray;

        var cloneRig = Object.Instantiate(f.Root).GetComponent<DeepRig>();

        // List: fresh container, fresh element instances, null preserved.
        Assert.NotSame(templateList, cloneRig.propsList);
        Assert.Equal(3, cloneRig.propsList.Count);
        Assert.NotSame(templateList[0], cloneRig.propsList[0]);
        Assert.Equal("L0", cloneRig.propsList[0].label);
        Assert.NotSame(templateList[0].tuning, cloneRig.propsList[0].tuning);
        Assert.Equal(1, cloneRig.propsList[0].tuning.value);
        Assert.Null(cloneRig.propsList[1]);
        Assert.NotSame(f.Shared, cloneRig.propsList[2]);
        Assert.Equal("shared", cloneRig.propsList[2].label);

        // Array: same contract.
        Assert.NotSame(templateArray, cloneRig.propsArray);
        Assert.Equal(2, cloneRig.propsArray.Length);
        Assert.NotSame(templateArray[0], cloneRig.propsArray[0]);
        Assert.Equal("A0", cloneRig.propsArray[0].label);
        Assert.Null(cloneRig.propsArray[1]);

        // Mutating clone elements leaves the template alone.
        cloneRig.propsList[0].tuning.value = 42;
        Assert.Equal(1, templateList[0].tuning.value);

        // Template containers and instances untouched.
        Assert.Same(templateList, f.Rig.propsList);
        Assert.Same(f.Shared, templateList[2]);
        Assert.Same(templateArray, f.Rig.propsArray);
    }

    [Fact]
    public void Clone_TemplateAliasing_DoesNotSurvive_PerFieldIndependentCopies()
    {
        using var loop = new GameLoop();
        var f = BuildDeepRig();
        Assert.Same(f.Rig.sharedA, f.Rig.sharedB); // precondition: one template instance, two fields

        var cloneRig = Object.Instantiate(f.Root).GetComponent<DeepRig>();

        // Engine-inline semantics: each field path gets its OWN copy.
        Assert.NotSame(f.Shared, cloneRig.sharedA);
        Assert.NotSame(f.Shared, cloneRig.sharedB);
        Assert.NotSame(cloneRig.sharedA, cloneRig.sharedB);
        Assert.NotSame(cloneRig.sharedA, cloneRig.propsList[2]);

        // All copies carry the same authored values.
        Assert.Equal("shared", cloneRig.sharedA.label);
        Assert.Equal("shared", cloneRig.sharedB.label);
        Assert.Equal(7f, cloneRig.sharedB.mass);
        Assert.Equal(3, cloneRig.sharedB.tuning.value);

        // Template aliasing intact.
        Assert.Same(f.Rig.sharedA, f.Rig.sharedB);
    }

    [Fact]
    public void Clone_DepthCap_TruncatesNestedPlainGraphAtTenLevels()
    {
        using var loop = new GameLoop();
        var f = BuildDeepRig();

        var cloneRig = Object.Instantiate(f.Root).GetComponent<DeepRig>();

        // The engine's serialization depth limit is 10: levels 1..10 clone, level 11 drops to null.
        var cloneChain = Walk(cloneRig.chain);
        Assert.Equal(10, cloneChain.Count);
        for (int i = 0; i < 10; i++)
            Assert.Equal(i + 1, cloneChain[i].id);
        Assert.Null(cloneChain[^1].next);

        // Template chain still has all 12 levels.
        var templateChain = Walk(f.Rig.chain);
        Assert.Equal(12, templateChain.Count);
        Assert.Equal(12, templateChain[^1].id);
    }

    [Fact]
    public void Clone_CyclicPlainGraph_TerminatesViaDepthGuard()
    {
        using var loop = new GameLoop();
        var f = BuildDeepRig();

        // Must not stack-overflow.
        var cloneRig = Object.Instantiate(f.Root).GetComponent<DeepRig>();

        // Per-field independent copies unroll the cycle into a 10-deep chain, then truncate.
        var unrolled = Walk(cloneRig.cycle);
        Assert.Equal(10, unrolled.Count);
        Assert.Null(unrolled[^1].next);
        foreach (var node in unrolled)
        {
            Assert.Equal(100, node.id);
            Assert.NotSame(f.Rig.cycle, node);
        }

        // Template cycle untouched.
        Assert.Same(f.Rig.cycle, f.Rig.cycle.next);
    }

    [Fact]
    public void Clone_NonSerializablePlainClass_KeepsReferenceCopy()
    {
        using var loop = new GameLoop();
        var f = BuildDeepRig();

        var cloneRig = Object.Instantiate(f.Root).GetComponent<DeepRig>();

        // Existing port semantics for shapes the engine never serialized: reference copy.
        Assert.Same(f.Rig.runtimeOnly, cloneRig.runtimeOnly);
    }

    [Fact]
    public void Clone_PrimitiveCollections_GetFreshContainersWithValueCopies()
    {
        using var loop = new GameLoop();
        var f = BuildDeepRig();
        var templateFloats = f.Rig.floatList;
        var templateInts = f.Rig.intArray;

        var cloneRig = Object.Instantiate(f.Root).GetComponent<DeepRig>();

        Assert.NotSame(templateFloats, cloneRig.floatList);
        Assert.Equal(new List<float> { 1f, 2f, 3f }, cloneRig.floatList);
        Assert.NotSame(templateInts, cloneRig.intArray);
        Assert.Equal(new[] { 10, 20 }, cloneRig.intArray);

        // Mutating the clone's containers leaves the template alone (the pre-fix bug
        // shape for primitives, same as C6 for Resources).
        cloneRig.floatList.Add(4f);
        cloneRig.floatList[0] = -1f;
        cloneRig.intArray[0] = 999;
        Assert.Equal(new List<float> { 1f, 2f, 3f }, templateFloats);
        Assert.Equal(new[] { 10, 20 }, templateInts);
    }

    [Fact]
    public void Clone_InTreeComponentRefs_StillRemap_C1RegressionGuard()
    {
        using var loop = new GameLoop();
        var f = BuildDeepRig();

        var clone = Object.Instantiate(f.Root);
        var cloneRig = clone.GetComponent<DeepRig>();
        var clonePart = clone.GetComponentInChildren<RigPart>(includeInactive: true);

        // Direct component ref → clone's own part.
        Assert.Same(clonePart, cloneRig.inTreePart);
        Assert.NotSame(f.InTreePart, cloneRig.inTreePart);

        // List of components: fresh container, in-tree remapped, external + null verbatim.
        Assert.NotSame(f.Rig.partList, cloneRig.partList);
        Assert.Equal(3, cloneRig.partList.Count);
        Assert.Same(clonePart, cloneRig.partList[0]);
        Assert.Same(f.ExternalPart, cloneRig.partList[1]);
        Assert.Null(cloneRig.partList[2]);

        // ScriptableObject reference on the component itself stays shared.
        Assert.Same(f.Config, cloneRig.configAsset);

        // Template untouched.
        Assert.Same(f.InTreePart, f.Rig.partList[0]);
    }
}
