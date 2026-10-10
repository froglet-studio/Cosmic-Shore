using System.Collections.Generic;
using CosmicShore.Engine;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C7 (2026-10-10): the prefab clone path resolves its field set once per type and
// copies through a compiled copier (ObjectUtilities.ClonePlan), AddComponent constructs
// through a cached constructor, and Instantiate(Mesh) clones a mesh by value. These
// tests hold the CONTRACT those changes must not move:
//   • every candidate field copies — public, [SerializeField] private, protected in a
//     base class, value types (struct, enum, float) by value, initonly fields included;
//   • references into the cloned tree remap to the clone, references outside it stay;
//   • containers are fresh per clone (the E16 rule), whatever the field's visibility;
//   • a component with a private constructor and a [RequireComponent] still adds, twice;
//   • a Mesh clone shares no buffer with its source and carries the "(Clone)" suffix.
// ─────────────────────────────────────────────────────────────────────────────

enum PlanColour { Red = 0, Green = 1 }

class PlanProbeBase : MonoBehaviour
{
    [SerializeField] protected int baseValue;
    public int BaseValue { get => baseValue; set => baseValue = value; }
}

class PlanProbe : PlanProbeBase
{
    public int publicInt;
    [SerializeField] float privateFloat;
    [SerializeField] Vector3 structField;
    [SerializeField] PlanColour colour;
    [SerializeField] string text;
    public readonly List<int> readonlyList = new();
    public PlanProbe self;              // intra-tree: must remap to the clone's own component
    public BoxCollider sibling;         // intra-tree: must remap to the clone's sibling
    public GameObject outsider;         // outside the tree: shared

    public float PrivateFloat { get => privateFloat; set => privateFloat = value; }
    public Vector3 StructField { get => structField; set => structField = value; }
    public PlanColour Colour { get => colour; set => colour = value; }
    public string Text { get => text; set => text = value; }
}

[RequireComponent(typeof(BoxCollider))]
class PrivateCtorProbe : MonoBehaviour
{
    public int Constructed;
    PrivateCtorProbe() { Constructed = 7; }
}

public class InstantiateClonePlanTests : System.IDisposable
{
    readonly GameLoop loop = new();
    public void Dispose() => loop.Dispose();

    static (GameObject template, PlanProbe probe, GameObject outsider) BuildTemplate()
    {
        var outsider = new GameObject("Outsider");
        var template = new GameObject("Template");
        template.SetActive(false);
        var collider = template.AddComponent<BoxCollider>();
        var probe = template.AddComponent<PlanProbe>();
        probe.publicInt = 42;
        probe.PrivateFloat = 1.5f;
        probe.StructField = new Vector3(1, 2, 3);
        probe.Colour = PlanColour.Green;
        probe.Text = "authored";
        probe.BaseValue = 9;
        probe.readonlyList.Add(5);
        probe.readonlyList.Add(6);
        probe.self = probe;
        probe.sibling = collider;
        probe.outsider = outsider;
        return (template, probe, outsider);
    }

    [Fact]
    public void Clone_CopiesEveryCandidateField_ByValue()
    {
        var (template, _, outsider) = BuildTemplate();

        var clone = Object.Instantiate(template);
        var copy = clone.GetComponent<PlanProbe>();

        Assert.Equal(42, copy.publicInt);
        Assert.Equal(1.5f, copy.PrivateFloat);
        Assert.Equal(new Vector3(1, 2, 3), copy.StructField);
        Assert.Equal(PlanColour.Green, copy.Colour);
        Assert.Equal("authored", copy.Text);
        Assert.Equal(9, copy.BaseValue);
        Assert.Equal(new[] { 5, 6 }, copy.readonlyList);
        Assert.Same(outsider, copy.outsider);
    }

    [Fact]
    public void Clone_RemapsIntraTreeReferences_AndKeepsContainersFresh()
    {
        var (template, probe, _) = BuildTemplate();

        var clone = Object.Instantiate(template);
        var copy = clone.GetComponent<PlanProbe>();

        Assert.NotSame(probe, copy);
        Assert.Same(copy, copy.self);
        Assert.Same(clone.GetComponent<BoxCollider>(), copy.sibling);
        Assert.NotSame(probe.readonlyList, copy.readonlyList); // an initonly container is still a fresh container
        copy.readonlyList.Add(7);
        Assert.Equal(2, probe.readonlyList.Count);
    }

    [Fact]
    public void Clone_OfTheSameTypeTwice_IsIndependent()
    {
        // The per-type plan is shared; the clones it produces are not.
        var (template, probe, _) = BuildTemplate();
        var first = Object.Instantiate(template).GetComponent<PlanProbe>();
        probe.publicInt = 43;
        probe.StructField = new Vector3(4, 5, 6);
        var second = Object.Instantiate(template).GetComponent<PlanProbe>();

        Assert.Equal(42, first.publicInt);
        Assert.Equal(43, second.publicInt);
        Assert.Equal(new Vector3(1, 2, 3), first.StructField);
        Assert.Equal(new Vector3(4, 5, 6), second.StructField);
        first.StructField = Vector3.zero;
        Assert.Equal(new Vector3(4, 5, 6), probe.StructField);
    }

    [Fact]
    public void AddComponent_PrivateConstructor_AndRequireComponent_StillHold()
    {
        // [RequireComponent] is enforced by the player, off by default for the hand-assembled
        // test rigs (RequireComponentTests flips it the same way).
        GameObject.EnforceRequireComponent = true;
        try
        {
            var go = new GameObject("Probe");
            var first = go.AddComponent<PrivateCtorProbe>();
            Assert.Equal(7, first.Constructed);
            Assert.NotNull(go.GetComponent<BoxCollider>());

            // The cached constructor and the cached [RequireComponent] serve the second add too.
            var other = new GameObject("Other");
            var second = other.AddComponent<PrivateCtorProbe>();
            Assert.Equal(7, second.Constructed);
            Assert.NotSame(first, second);
            Assert.NotNull(other.GetComponent<BoxCollider>());
            Assert.Single(other.GetComponents<BoxCollider>());
        }
        finally { GameObject.EnforceRequireComponent = false; }
    }

    [Fact]
    public void Instantiate_Mesh_ClonesByValue()
    {
        var source = new Mesh { name = "Body" };
        source.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        source.triangles = new[] { 0, 1, 2 };
        source.bindposes = new[] { Matrix4x4.identity };
        source.boneWeights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1f }, default, default };
        source.RecalculateBounds();

        var clone = Object.Instantiate(source);

        Assert.NotSame(source, clone);
        Assert.Equal("Body(Clone)", clone.name);
        Assert.Equal(source.vertices, clone.vertices);
        Assert.Equal(source.triangles, clone.triangles);
        Assert.Equal(source.bounds, clone.bounds);
        Assert.Single(clone.bindposes);
        Assert.Equal(3, clone.boneWeights.Length);

        // Independent buffers: the baker rewrites the twin's bindposes and bone weights.
        clone.bindposes = new[] { Matrix4x4.identity, Matrix4x4.Translate(new Vector3(1, 0, 0)) };
        clone.boneWeights = new[] { new BoneWeight { boneIndex0 = 1, weight0 = 1f }, default, default };
        Assert.Single(source.bindposes);
        Assert.Equal(0, source.boneWeights[0].boneIndex0);
    }
}
